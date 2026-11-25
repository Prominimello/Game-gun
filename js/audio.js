// Procedural audio engine: every sound is synthesized with WebAudio at runtime.
'use strict';
BP.audio = (() => {
  const U = BP.util;
  let ctx = null, master, sfxBus, ambBus, musBus, reverb, revSend, noiseBuf, brownBuf;
  const settings = { master: 0.8, sfx: 0.9, ambient: 0.55, music: 0.35, voice: true };
  const listener = { x: 0, y: 0, range: 900, rot: 0 };
  let ambNodes = [], ambTimers = [], musNodes = [], musTimer = null, lastRing = 0;

  function load() {
    try { Object.assign(settings, JSON.parse(localStorage.getItem('bp_audio') || '{}')); } catch (e) { }
  }
  function save() { localStorage.setItem('bp_audio', JSON.stringify(settings)); applyVol(); }
  function applyVol() {
    if (!ctx) return;
    master.gain.value = settings.master; sfxBus.gain.value = settings.sfx;
    ambBus.gain.value = settings.ambient; musBus.gain.value = settings.music;
  }

  function init() {
    if (ctx) { if (ctx.state === 'suspended') ctx.resume(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    ctx = new AC();
    master = ctx.createGain(); master.connect(ctx.destination);
    const comp = ctx.createDynamicsCompressor(); comp.threshold.value = -14; comp.ratio.value = 6;
    comp.connect(master);
    sfxBus = ctx.createGain(); sfxBus.connect(comp);
    ambBus = ctx.createGain(); ambBus.connect(comp);
    musBus = ctx.createGain(); musBus.connect(comp);
    // white noise
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    // brown noise
    brownBuf = ctx.createBuffer(1, ctx.sampleRate * 4, ctx.sampleRate);
    const b = brownBuf.getChannelData(0); let last = 0;
    for (let i = 0; i < b.length; i++) { last = (last + 0.02 * (Math.random() * 2 - 1)) / 1.02; b[i] = last * 3.5; }
    // reverb impulse (indoor room)
    reverb = ctx.createConvolver();
    const len = ctx.sampleRate * 1.6, ir = ctx.createBuffer(2, len, ctx.sampleRate);
    for (let c = 0; c < 2; c++) { const ch = ir.getChannelData(c); for (let i = 0; i < len; i++) ch[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 3.2); }
    reverb.buffer = ir;
    revSend = ctx.createGain(); revSend.gain.value = 0.35;
    revSend.connect(reverb); reverb.connect(comp);
    applyVol();
  }

  function spatial(x, y) {
    if (x === undefined) return { vol: 1, pan: 0 };
    const dx = x - listener.x, dy = y - listener.y;
    const d = Math.hypot(dx, dy);
    const vol = U.clamp(1 - d / (listener.range * 1.6), 0.04, 1);
    const sx = Math.cos(listener.rot) * dx - Math.sin(listener.rot) * dy;
    return { vol, pan: U.clamp(sx / (listener.range * 0.7), -0.9, 0.9) };
  }
  function out(x, y, vol, rev = 0.3, bus) {
    const s = spatial(x, y);
    const g = ctx.createGain(); g.gain.value = vol * s.vol;
    const p = ctx.createStereoPanner(); p.pan.value = s.pan;
    g.connect(p); p.connect(bus || sfxBus);
    if (rev > 0) { const r = ctx.createGain(); r.gain.value = rev; p.connect(r); r.connect(revSend); }
    return g;
  }
  function env(g, t, a, peak, dec) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + a + dec);
  }
  function noise(dest, t, { type = 'lowpass', f = 2000, q = 0.7, a = 0.002, dec = 0.2, vol = 1, rate = 1, f2 = null, buf = null } = {}) {
    const src = ctx.createBufferSource(); src.buffer = buf || noiseBuf; src.playbackRate.value = rate;
    const fl = ctx.createBiquadFilter(); fl.type = type; fl.frequency.setValueAtTime(f, t); fl.Q.value = q;
    if (f2) fl.frequency.exponentialRampToValueAtTime(f2, t + a + dec);
    const g = ctx.createGain(); env(g, t, a, vol, dec);
    src.connect(fl); fl.connect(g); g.connect(dest);
    src.start(t, Math.random() * 1.5); src.stop(t + a + dec + 0.05);
    return src;
  }
  function tone(dest, t, { type = 'sine', f = 440, f2 = null, a = 0.002, dec = 0.2, vol = 0.5, filt = null } = {}) {
    const o = ctx.createOscillator(); o.type = type; o.frequency.setValueAtTime(f, t);
    if (f2) o.frequency.exponentialRampToValueAtTime(Math.max(1, f2), t + a + dec);
    const g = ctx.createGain(); env(g, t, a, vol, dec);
    if (filt) { const fl = ctx.createBiquadFilter(); fl.type = 'bandpass'; fl.frequency.value = filt; fl.Q.value = 3; o.connect(fl); fl.connect(g); }
    else o.connect(g);
    g.connect(dest); o.start(t); o.stop(t + a + dec + 0.05);
    return o;
  }
  const ok = () => ctx && ctx.state === 'running';
  const now = () => ctx.currentTime;
  const rnd = (a, b) => a + Math.random() * (b - a);

  // ---------- SFX ----------
  const S = {};
  S.shot = (cal, suppressed, x, y, indoor = true) => {
    if (!ok()) return; const t = now();
    const o = out(x, y, 1, indoor ? 0.45 : 0.15);
    if (suppressed) {
      noise(o, t, { type: 'lowpass', f: rnd(700, 1000), dec: 0.07, vol: 0.35 });
      noise(o, t, { type: 'highpass', f: 3000, dec: 0.015, vol: 0.25 });
      tone(o, t + 0.03, { type: 'square', f: 1800, f2: 900, dec: 0.02, vol: 0.05 });
      return;
    }
    const heavy = cal === '7.62' || cal === '.338', sg = cal === '12g', pistol = cal === '9mm' || cal === '.45' || cal === '5.7' || cal === '4.6';
    const lp = sg ? 1800 : heavy ? 3200 : pistol ? 2600 : 4200;
    const dec = sg ? 0.42 : heavy ? 0.36 : pistol ? 0.16 : 0.24;
    noise(o, t, { type: 'lowpass', f: lp * rnd(0.9, 1.1), f2: 300, dec, vol: sg ? 1.1 : heavy ? 1.05 : 0.85 });
    noise(o, t, { type: 'highpass', f: 2200, dec: 0.04, vol: heavy ? 0.7 : 0.45 });
    tone(o, t, { f: sg ? 60 : heavy ? 70 : 95, f2: 30, dec: sg ? 0.3 : 0.18, vol: sg || heavy ? 0.9 : 0.55 });
  };
  S.shell = (x, y, sg) => {
    if (!ok()) return; const t = now() + rnd(0.25, 0.5); const o = out(x, y, 0.5, 0.05);
    tone(o, t, { f: sg ? rnd(700, 900) : rnd(3800, 5600), dec: 0.05, vol: 0.06 });
    tone(o, t + rnd(0.06, 0.12), { f: sg ? rnd(600, 800) : rnd(3500, 5200), dec: 0.04, vol: 0.035 });
  };
  S.reload = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.7, 0.2);
    noise(o, t, { type: 'bandpass', f: 2500, q: 2, dec: 0.03, vol: 0.4 });
    noise(o, t + 0.35, { type: 'bandpass', f: 1600, q: 2, dec: 0.04, vol: 0.5 });
    noise(o, t + 0.75, { type: 'bandpass', f: 3200, q: 3, dec: 0.03, vol: 0.5 });
  };
  S.pump = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.7, 0.2);
    noise(o, t + 0.1, { type: 'bandpass', f: 1200, q: 2, dec: 0.06, vol: 0.5 });
    noise(o, t + 0.25, { type: 'bandpass', f: 1800, q: 2, dec: 0.05, vol: 0.5 });
  };
  S.dryfire = (x, y) => { if (!ok()) return; const t = now(); tone(out(x, y, 0.5, 0.1), t, { type: 'square', f: 2200, dec: 0.015, vol: 0.12 }); };
  S.step = (x, y, run) => {
    if (!ok()) return; const t = now(); const o = out(x, y, run ? 0.22 : 0.12, 0.05);
    noise(o, t, { type: 'lowpass', f: rnd(300, 520), dec: 0.07, vol: 1 });
    noise(o, t, { type: 'bandpass', f: rnd(1500, 2500), q: 1, dec: 0.02, vol: 0.2 });
  };
  S.doorOpen = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.5, 0.3);
    tone(o, t, { type: 'square', f: 1400, dec: 0.02, vol: 0.12 });
    const osc = ctx.createOscillator(); osc.type = 'sawtooth'; osc.frequency.setValueAtTime(rnd(160, 220), t);
    osc.frequency.linearRampToValueAtTime(rnd(260, 340), t + 0.25); osc.frequency.linearRampToValueAtTime(rnd(180, 240), t + 0.5);
    const lfo = ctx.createOscillator(); lfo.frequency.value = 23; const lg = ctx.createGain(); lg.gain.value = 30; lfo.connect(lg); lg.connect(osc.frequency);
    const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 900; bp.Q.value = 6;
    const g = ctx.createGain(); env(g, t + 0.03, 0.05, 0.18, 0.5);
    osc.connect(bp); bp.connect(g); g.connect(o); osc.start(t); lfo.start(t); osc.stop(t + 0.7); lfo.stop(t + 0.7);
  };
  S.doorClose = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.5, 0.3); tone(o, t, { f: 110, f2: 60, dec: 0.12, vol: 0.5 }); noise(o, t, { f: 900, dec: 0.08, vol: 0.4 }); };
  S.kick = (x, y, success) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 1, 0.4);
    tone(o, t, { f: 100, f2: 40, dec: 0.22, vol: 0.9 });
    noise(o, t, { type: 'lowpass', f: 1400, dec: 0.12, vol: 0.7 });
    if (success) { noise(o, t + 0.02, { type: 'bandpass', f: 2200, q: 0.8, dec: 0.25, vol: 0.6 }); for (let i = 0; i < 4; i++) tone(o, t + rnd(0.02, 0.2), { type: 'triangle', f: rnd(300, 700), dec: 0.05, vol: 0.1 }); }
  };
  S.explosion = (x, y, size = 1) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 1.3 * size, 0.5);
    noise(o, t, { type: 'lowpass', f: 1200, f2: 80, dec: 1.4 * size, vol: 1.3 });
    noise(o, t, { type: 'highpass', f: 1500, dec: 0.12, vol: 0.9 });
    tone(o, t, { f: 60, f2: 22, dec: 1.0, vol: 1.2 });
    for (let i = 0; i < 8; i++) noise(o, t + rnd(0.1, 0.8), { type: 'bandpass', f: rnd(800, 3000), q: 2, dec: 0.04, vol: 0.15 });
  };
  S.flashbang = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 1.4, 0.6);
    noise(o, t, { type: 'highpass', f: 400, dec: 0.5, vol: 1.3 });
    tone(o, t, { f: 80, f2: 30, dec: 0.4, vol: 1 });
    const s = spatial(x, y);
    if (s.vol > 0.55 && performance.now() - lastRing > 3000) { lastRing = performance.now(); tone(sfxBus, t + 0.05, { f: 3900, a: 0.05, dec: 2.8, vol: 0.05 * s.vol }); }
  };
  S.smoke = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.4, 0.2); tone(o, t, { type: 'square', f: 600, dec: 0.03, vol: 0.2 }); noise(o, t + 0.05, { type: 'highpass', f: 2500, a: 0.1, dec: 2.0, vol: 0.4 }); };
  S.pin = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.4, 0.1); tone(o, t, { type: 'triangle', f: 3000, dec: 0.04, vol: 0.2 }); tone(o, t + 0.15, { type: 'triangle', f: 2200, dec: 0.05, vol: 0.15 }); };
  S.bounce = (x, y) => { if (!ok()) return; const t = now(); tone(out(x, y, 0.4, 0.2), t, { type: 'triangle', f: rnd(500, 800), dec: 0.05, vol: 0.3 }); };
  S.glass = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.8, 0.3);
    noise(o, t, { type: 'highpass', f: 3500, dec: 0.35, vol: 0.6 });
    for (let i = 0; i < 9; i++) tone(o, t + rnd(0, 0.25), { f: rnd(2500, 7500), dec: rnd(0.04, 0.15), vol: 0.08 });
  };
  S.impact = (x, y, metal) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.4, 0.1);
    if (metal) tone(o, t, { type: 'triangle', f: rnd(1800, 3000), f2: rnd(900, 1400), dec: 0.15, vol: 0.2 });
    else noise(o, t, { type: 'bandpass', f: rnd(900, 1500), q: 1, dec: 0.05, vol: 0.5 });
  };
  S.flesh = (x, y) => { if (!ok()) return; const t = now(); noise(out(x, y, 0.6, 0.1), t, { type: 'lowpass', f: 700, dec: 0.08, vol: 0.8 }); };
  S.grunt = (x, y, female) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.5, 0.2);
    const f = female ? rnd(260, 320) : rnd(120, 160);
    tone(o, t, { type: 'sawtooth', f, f2: f * 0.75, a: 0.02, dec: 0.2, vol: 0.35, filt: female ? 900 : 650 });
  };
  S.scream = (x, y) => {
    if (!ok()) return; const t = now(); const o = out(x, y, 0.35, 0.3);
    const osc = ctx.createOscillator(); osc.type = 'sawtooth'; osc.frequency.setValueAtTime(rnd(500, 600), t); osc.frequency.linearRampToValueAtTime(rnd(800, 950), t + 0.3); osc.frequency.linearRampToValueAtTime(rnd(600, 700), t + 0.7);
    const lfo = ctx.createOscillator(); lfo.frequency.value = 7; const lg = ctx.createGain(); lg.gain.value = 25; lfo.connect(lg); lg.connect(osc.frequency);
    const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 1300; bp.Q.value = 2;
    const g = ctx.createGain(); env(g, t, 0.05, 0.3, 0.7); osc.connect(bp); bp.connect(g); g.connect(o);
    osc.start(t); lfo.start(t); osc.stop(t + 0.9); lfo.stop(t + 0.9);
  };
  S.fall = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.6, 0.2); tone(o, t, { f: 75, f2: 40, dec: 0.18, vol: 0.6 }); noise(o, t, { f: 350, dec: 0.2, vol: 0.5 }); };
  S.cuffs = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.5, 0.1); for (let i = 0; i < 7; i++) noise(o, t + i * 0.03, { type: 'bandpass', f: 3500, q: 4, dec: 0.015, vol: 0.4 }); };
  S.pick = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.3, 0.05); for (let i = 0; i < 4; i++) tone(o, t + i * rnd(0.08, 0.2), { type: 'triangle', f: rnd(3500, 5000), dec: 0.015, vol: 0.12 }); };
  S.cough = (x, y) => { if (!ok()) return; const t = now(); const o = out(x, y, 0.4, 0.2); for (let i = 0; i < 3; i++) noise(o, t + i * 0.22, { type: 'bandpass', f: rnd(400, 700), q: 1.5, a: 0.01, dec: 0.12, vol: 0.6 }); };
  S.ricochet = (x, y) => { if (!ok()) return; const t = now(); tone(out(x, y, 0.4, 0.2), t, { f: rnd(2500, 4000), f2: rnd(900, 1400), dec: 0.25, vol: 0.12 }); };
  S.beep = (f = 1200) => { if (!ok()) return; tone(sfxBus, now(), { type: 'square', f, dec: 0.05, vol: 0.06 }); };
  S.radio = () => {
    if (!ok()) return; const t = now();
    noise(sfxBus, t, { type: 'bandpass', f: 2200, q: 1.5, dec: 0.06, vol: 0.12 });
    tone(sfxBus, t + 0.01, { type: 'square', f: 1450, dec: 0.04, vol: 0.03 });
  };
  S.ui = () => { if (!ok()) return; tone(sfxBus, now(), { type: 'triangle', f: 1300, dec: 0.03, vol: 0.08 }); };
  S.confirm = () => { if (!ok()) return; const t = now(); tone(sfxBus, t, { type: 'triangle', f: 880, dec: 0.06, vol: 0.1 }); tone(sfxBus, t + 0.07, { type: 'triangle', f: 1320, dec: 0.1, vol: 0.1 }); };
  S.error = () => { if (!ok()) return; const t = now(); tone(sfxBus, t, { type: 'square', f: 220, dec: 0.15, vol: 0.06 }); };
  S.cash = () => { if (!ok()) return; const t = now(); tone(sfxBus, t, { type: 'triangle', f: 1600, dec: 0.08, vol: 0.12 }); tone(sfxBus, t + 0.09, { type: 'triangle', f: 2400, dec: 0.2, vol: 0.12 }); noise(sfxBus, t, { type: 'bandpass', f: 4000, q: 3, dec: 0.05, vol: 0.2 }); };
  S.equip = () => { if (!ok()) return; const t = now(); noise(sfxBus, t, { type: 'bandpass', f: 1800, q: 2, dec: 0.04, vol: 0.4 }); noise(sfxBus, t + 0.12, { type: 'bandpass', f: 2600, q: 3, dec: 0.03, vol: 0.4 }); };
  S.rankStamp = () => { if (!ok()) return; const t = now(); tone(sfxBus, t, { f: 90, f2: 40, dec: 0.4, vol: 0.8 }); noise(sfxBus, t, { f: 1500, dec: 0.2, vol: 0.5 }); };
  S.heartbeat = () => { if (!ok()) return; const t = now(); tone(musBus, t, { f: 55, f2: 35, dec: 0.15, vol: 0.5 }); tone(musBus, t + 0.22, { f: 50, f2: 32, dec: 0.15, vol: 0.35 }); };

  // ---------- Ambience ----------
  function stopAmbient() {
    for (const n of ambNodes) { try { n.stop(); } catch (e) { } }
    ambNodes = []; ambTimers.forEach(clearInterval); ambTimers = [];
  }
  function loopNoise(buf, type, f, vol, q = 0.7) {
    const src = ctx.createBufferSource(); src.buffer = buf; src.loop = true;
    const fl = ctx.createBiquadFilter(); fl.type = type; fl.frequency.value = f; fl.Q.value = q;
    const g = ctx.createGain(); g.gain.value = vol;
    src.connect(fl); fl.connect(g); g.connect(ambBus); src.start(); ambNodes.push(src);
    return { src, fl, g };
  }
  function carPass() {
    const t = now(), d = rnd(3, 5);
    const src = ctx.createBufferSource(); src.buffer = noiseBuf; src.loop = true;
    const fl = ctx.createBiquadFilter(); fl.type = 'bandpass'; fl.Q.value = 1.2; fl.frequency.setValueAtTime(180, t); fl.frequency.linearRampToValueAtTime(rnd(500, 750), t + d / 2); fl.frequency.linearRampToValueAtTime(160, t + d);
    const g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(rnd(0.04, 0.09), t + d / 2); g.gain.linearRampToValueAtTime(0.0001, t + d);
    const p = ctx.createStereoPanner(); p.pan.setValueAtTime(-0.8, t); p.pan.linearRampToValueAtTime(0.8, t + d);
    src.connect(fl); fl.connect(g); g.connect(p); p.connect(ambBus); src.start(t); src.stop(t + d + 0.1);
  }
  function siren() {
    const t = now(), d = rnd(5, 9);
    const o = ctx.createOscillator(); o.type = 'sine';
    const lfo = ctx.createOscillator(); lfo.frequency.value = rnd(0.25, 0.45); const lg = ctx.createGain(); lg.gain.value = 260;
    o.frequency.value = 880; lfo.connect(lg); lg.connect(o.frequency);
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 1400;
    const g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.018, t + d * 0.4); g.gain.linearRampToValueAtTime(0.0001, t + d);
    o.connect(lp); lp.connect(g); g.connect(ambBus); o.start(t); lfo.start(t); o.stop(t + d); lfo.stop(t + d);
  }
  function helicopter() {
    const t = now(), d = rnd(10, 16);
    const src = ctx.createBufferSource(); src.buffer = noiseBuf; src.loop = true;
    const fl = ctx.createBiquadFilter(); fl.type = 'lowpass'; fl.frequency.value = 260;
    const am = ctx.createGain(); am.gain.value = 0.5;
    const lfo = ctx.createOscillator(); lfo.type = 'square'; lfo.frequency.value = rnd(11, 15); const lg = ctx.createGain(); lg.gain.value = 0.5; lfo.connect(lg); lg.connect(am.gain);
    const g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.22, t + d * 0.45); g.gain.linearRampToValueAtTime(0.0001, t + d);
    src.connect(fl); fl.connect(am); am.connect(g); g.connect(ambBus); src.start(t); lfo.start(t); src.stop(t + d); lfo.stop(t + d);
  }
  function startAmbient(kind, { rain = false, night = false } = {}) {
    if (!ctx) return; stopAmbient();
    if (kind === 'hq') {
      loopNoise(brownBuf, 'lowpass', 220, 0.12);
      const hum = ctx.createOscillator(); hum.frequency.value = 60; const hg = ctx.createGain(); hg.gain.value = 0.008; hum.connect(hg); hg.connect(ambBus); hum.start(); ambNodes.push(hum);
      ambTimers.push(setInterval(() => { if (Math.random() < 0.25) S.radio(); }, 7000));
      return;
    }
    if (kind === 'bunker') {
      loopNoise(brownBuf, 'lowpass', 120, 0.25);
      const hum = ctx.createOscillator(); hum.frequency.value = 50; const hg = ctx.createGain(); hg.gain.value = 0.02; hum.connect(hg); hg.connect(ambBus); hum.start(); ambNodes.push(hum);
    } else if (kind === 'rural') {
      const w = loopNoise(noiseBuf, 'bandpass', 380, 0.07, 0.6);
      const lfo = ctx.createOscillator(); lfo.frequency.value = 0.07; const lg = ctx.createGain(); lg.gain.value = 180; lfo.connect(lg); lg.connect(w.fl.frequency); lfo.start(); ambNodes.push(lfo);
      if (night) ambTimers.push(setInterval(() => { const t = now(); for (let i = 0; i < 3; i++) tone(ambBus, t + i * 0.12, { f: rnd(4200, 4800), dec: 0.05, vol: 0.008 }); }, 1700));
    } else {
      loopNoise(brownBuf, 'lowpass', 300, 0.3);
      ambTimers.push(setInterval(() => { if (Math.random() < 0.5) carPass(); }, 6000));
      ambTimers.push(setInterval(() => { if (Math.random() < 0.35) siren(); }, 21000));
    }
    ambTimers.push(setInterval(() => { if (Math.random() < 0.3) helicopter(); }, 30000));
    if (rain) { loopNoise(noiseBuf, 'highpass', 1400, 0.09); loopNoise(noiseBuf, 'lowpass', 600, 0.05); }
  }

  // ---------- Music ----------
  function stopMusic() { for (const n of musNodes) { try { n.stop(); } catch (e) { } } musNodes = []; if (musTimer) clearInterval(musTimer); musTimer = null; }
  const NOTE = n => 440 * Math.pow(2, (n - 69) / 12);
  function pad(chord, t, d, vol, cut) {
    for (const n of chord) for (const det of [-6, 5]) {
      const o = ctx.createOscillator(); o.type = 'sawtooth'; o.frequency.value = NOTE(n); o.detune.value = det;
      const f = ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.setValueAtTime(cut * 0.6, t); f.frequency.linearRampToValueAtTime(cut, t + d / 2); f.frequency.linearRampToValueAtTime(cut * 0.6, t + d);
      const g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(vol, t + d * 0.3); g.gain.linearRampToValueAtTime(0.0001, t + d + 0.5);
      o.connect(f); f.connect(g); g.connect(musBus); o.start(t); o.stop(t + d + 0.6);
    }
  }
  function startMusic(kind) {
    if (!ctx) return; stopMusic();
    if (kind === 'hq') {
      const prog = [[45, 52, 57, 60], [41, 48, 53, 57], [48, 55, 60, 64], [43, 50, 55, 59]];
      let i = 0;
      const step = () => { pad(prog[i % 4], now(), 6.5, 0.012, 900); if (i % 2 === 0) tone(musBus, now(), { f: NOTE(prog[i % 4][0] - 12), a: 0.4, dec: 5, vol: 0.05 }); i++; };
      step(); musTimer = setInterval(step, 6000);
    } else if (kind === 'mission') {
      const prog = [[38, 45, 50], [38, 44, 50], [36, 43, 48], [37, 44, 49]]; let i = 0;
      const step = () => { pad(prog[i % 4], now(), 8.5, 0.008, 500); i++; };
      step(); musTimer = setInterval(step, 8000);
    } else if (kind === 'debrief') {
      const prog = [[48, 55, 60, 64, 67], [45, 52, 57, 60, 64]]; let i = 0;
      const step = () => { pad(prog[i % 2], now(), 7, 0.01, 1400); i++; };
      step(); musTimer = setInterval(step, 6500);
    }
  }

  // ---------- Voice (speech synthesis with radio squelch) ----------
  let lastSpeak = 0;
  function say(text, { pitch = 1, rate = 1.15, prio = false, radio = true } = {}) {
    if (!settings.voice || !window.speechSynthesis) return;
    const nowMs = performance.now();
    if (speechSynthesis.speaking) { if (!prio) return; speechSynthesis.cancel(); }
    if (!prio && nowMs - lastSpeak < 900) return;
    lastSpeak = nowMs;
    if (radio) S.radio();
    const u = new SpeechSynthesisUtterance(text);
    u.pitch = pitch; u.rate = rate; u.volume = 0.55 * settings.master;
    const voices = speechSynthesis.getVoices().filter(v => /en[-_]/i.test(v.lang));
    if (voices.length) u.voice = voices[Math.floor(pitch * 997) % voices.length];
    try { speechSynthesis.speak(u); } catch (e) { }
  }

  load();
  return { init, settings, save, listener, S, startAmbient, stopAmbient, startMusic, stopMusic, say, get ctx() { return ctx; } };
})();
