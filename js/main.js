// Boot, title screen, screen management, settings, replay viewer
'use strict';
BP.replay = (() => {
  const TS = BP.TS, U = BP.util, sim = BP.sim, R = BP.render;
  let RM = null, t = 0, playing = true, speed = 1, follow = -1, cam, scene, running = false, lastT = 0, onBack = null, doorEvIdx = 0, evIdx = 0, canvas, ctx, ghosts = [], floor = 0, dpr = 1;
  function open(M, back) {
    onBack = back; RM = M; t = 0; playing = true; speed = 1; follow = -1; floor = 0; evIdx = 0;
    BP.main.screen('replay');
    canvas = document.getElementById('replay-canvas'); ctx = canvas.getContext('2d');
    dpr = Math.min(2, window.devicePixelRatio || 1);
    canvas.width = canvas.clientWidth * dpr; canvas.height = canvas.clientHeight * dpr;
    cam = { x: M.map.w * TS / 2, y: M.map.h * TS / 2, zoom: Math.min(canvas.clientWidth / (M.map.w * TS), canvas.clientHeight / (M.map.h * TS)) * 1.1, rot: 0 };
    scene = { particles: [], flashes: [], shake: 0 };
    R.resetDecals(M.map);
    // pseudo units inheriting static props from originals
    ghosts = M.units.map(u => Object.create(u));
    buildUI();
    running = true; lastT = performance.now();
    canvas.onwheel = e => { e.preventDefault(); cam.zoom = U.clamp(cam.zoom * (e.deltaY < 0 ? 1.12 : 0.89), 0.2, 3); };
    let drag = null;
    const pts = new Map(); let pinch = null;
    canvas.onpointerdown = e => { pts.set(e.pointerId, [e.clientX, e.clientY]); try { canvas.setPointerCapture(e.pointerId); } catch (er) { } if (pts.size === 1) drag = { x: e.clientX, y: e.clientY }; else { drag = null; const [a, b] = [...pts.values()]; pinch = { d: Math.hypot(a[0] - b[0], a[1] - b[1]), z: cam.zoom }; } };
    canvas.onpointerup = canvas.onpointercancel = e => { pts.delete(e.pointerId); drag = null; pinch = null; };
    canvas.addEventListener('pointermove', e => { if (!pts.has(e.pointerId)) return; pts.set(e.pointerId, [e.clientX, e.clientY]); if (pinch && pts.size >= 2) { const [a, b] = [...pts.values()]; cam.zoom = U.clamp(pinch.z * Math.hypot(a[0] - b[0], a[1] - b[1]) / Math.max(10, pinch.d), 0.2, 3); } });
    canvas.onpointermove = e => { if (!drag || follow >= 0) return; const c = Math.cos(-cam.rot), s = Math.sin(-cam.rot); const dx = (drag.x - e.clientX) / cam.zoom, dy = (drag.y - e.clientY) / cam.zoom; cam.x += dx * c - dy * s; cam.y += dx * s + dy * c; drag = { x: e.clientX, y: e.clientY }; };
    requestAnimationFrame(loop);
  }
  function buildUI() {
    const ui = document.getElementById('replay-ui'); ui.innerHTML = '';
    const el = U.el;
    const ops = RM.units.map((u, i) => [u, i]).filter(([u]) => u.team === 'op');
    const camSel = el('select', { id: 'rp-cam', onchange: e => { follow = +e.target.value; scene.particles = []; } });
    camSel.append(el('option', { value: -1 }, 'Overview (all units)'));
    for (const [u, i] of ops) camSel.append(el('option', { value: i }, `Body cam — ${u.name}`));
    const floorSel = el('select', { id: 'rp-floor', onchange: e => floor = +e.target.value }); RM.map.labels.forEach((l, i) => floorSel.append(el('option', { value: i }, l)));
    ui.append(el('span', { class: 'rec' }, '● REC'), el('button', { id: 'rp-play', onclick: () => { playing = !playing; } }, '⏯'),
      el('button', { onclick: () => speed = speed >= 4 ? 0.5 : speed * 2, id: 'rp-spd' }, '×1'),
      el('input', { type: 'range', id: 'rp-t', min: 0, max: Math.round(RM.t * 10), value: 0, oninput: e => seek(e.target.value / 10) }),
      el('span', { class: 'mono', id: 'rp-clock' }, '00:00'), camSel, floorSel,
      el('select', { onchange: e => { if (e.target.value !== '') seek(+e.target.value); } }, el('option', { value: '' }, 'Jump to event…'), ...RM.log.map(l => el('option', { value: Math.max(0, l.t - 1.5) }, `${U.fmtTime(l.t)} ${l.text.slice(0, 50)}`))),
      el('button', { class: 'primary', onclick: close }, 'Exit Replay'));
  }
  function close() { running = false; RM.map.doors.forEach(d => d.state = d.finalState || d.state); if (onBack) onBack(); }
  function seek(nt) { t = U.clamp(nt, 0, RM.t); scene.particles = []; evIdx = RM.events.findIndex(e => e.t >= t); if (evIdx < 0) evIdx = RM.events.length; }
  function frameAt(time) {
    const F = RM.frames; if (!F.length) return null;
    let lo = 0, hi = F.length - 1;
    while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (F[mid].t <= time) lo = mid; else hi = mid - 1; }
    const a = F[lo], b = F[Math.min(F.length - 1, lo + 1)];
    const k = b.t > a.t ? U.clamp((time - a.t) / (b.t - a.t), 0, 1) : 0;
    return { a, b, k };
  }
  function applyDoors(time) {
    for (const d of RM.map.doors) { if (d.finalState === undefined) d.finalState = d.state; d.state = d.state0 || 'closed'; }
    for (const e of RM.events) { if (e.t > time) break; if (e.type === 'door') RM.map.doors[e.id].state = e.state; }
  }
  function loop(now) {
    if (!running) return;
    const dt = Math.min(0.05, (now - lastT) / 1000); lastT = now;
    if (playing) { t += dt * speed; if (t >= RM.t) { t = RM.t; playing = false; } }
    const fr = frameAt(t);
    if (fr) {
      ghosts.forEach((g, i) => {
        const A = fr.a.u[i], B = fr.b.u[i]; if (!A) return;
        const sameF = A[2] === B[2];
        g.x = sameF ? U.lerp(A[0], B[0], fr.k) : A[0]; g.y = sameF ? U.lerp(A[1], B[1], fr.k) : A[1]; g.floor = A[2];
        g.angle = A[3] / 100 + U.angDiff(A[3] / 100, B[3] / 100) * fr.k;
        const s = A[4]; g.dead = !!(s & 1); g.downed = !!(s & 2); g.incap = !!(s & 4); g.restrained = !!(s & 8); g.surrendered = !!(s & 16); g.stun = s & 32 ? 1 : 0; g.escaped = !!(s & 64); g.evacuated = false; g.stance = s & 128 ? 'crouch' : 'stand'; g.armed = !(s & 256); g.hostageBound = !!(s & 512); g.kneel = !!(s & 1024); g.hp = A[5]; g.alive = !g.dead && !g.escaped;
        g.weaponCat = g.armed !== false && g.weapons && g.weapons[g.wi] ? g.weapons[g.wi].st.cat : null;
        g.ai = g.ai ? Object.assign(Object.create(Object.getPrototypeOf(g).ai || {}), { state: g.surrendered ? 'surrender' : 'idle', execT: -1, guard: null }) : g.ai;
        g.visible = true;
      });
    }
    applyDoors(t);
    // events -> particles
    while (evIdx < RM.events.length && RM.events[evIdx].t <= t) {
      const e = RM.events[evIdx++];
      if (e.type === 'shot') { R.spawnFx(scene, { type: 'tracer', x0: e.x0, y0: e.y0, x1: e.x1, y1: e.y1, f: e.f, team: e.team }, null); R.spawnFx(scene, { type: 'muzzle', x: e.x0, y: e.y0, f: e.f, ang: Math.atan2(e.y1 - e.y0, e.x1 - e.x0) }, null); if (playing && speed <= 1) BP.audio.S.shot('5.56', false, e.x0, e.y0); }
      if (e.type === 'boom') { R.spawnFx(scene, { type: e.g === 'charge' ? 'explosion' : 'flash', x: e.x, y: e.y, f: e.f, big: true, r: 50 }, null); }
    }
    R.updateParticles(scene, playing ? dt * speed : 0, null);
    let view;
    if (follow >= 0) {
      const g = ghosts[follow];
      cam.x = g.x; cam.y = g.y; cam.rot = -g.angle - Math.PI / 2; cam.zoom = 1.4; floor = g.floor;
      const saved = sim.M; sim.M = { map: RM.map, smokes: [], night: RM.night, units: ghosts };
      const poly = g.dead || g.downed ? [] : [sim.visionPoly(g)];
      for (const o of ghosts) o.visible = o === g || (!g.dead && sim.canSee(g, o));
      sim.M = saved;
      view = { map: RM.map, floor, cam, units: ghosts, items: [], scene, t: now / 1000, time: t, vision: poly, night: RM.night, showAll: false, dpr };
    } else {
      view = { map: RM.map, floor, cam, units: ghosts, items: RM.items.map(i => ({ ...i, collected: false, seen: true })), scene, t: now / 1000, time: t, vision: [], night: false, showAll: true, dpr };
    }
    const L = BP.audio.listener; L.x = cam.x; L.y = cam.y; L.range = canvas.clientWidth / 2 / cam.zoom; L.rot = cam.rot;
    R.draw(ctx, view);
    // overlay text
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = 'rgba(255,255,255,0.85)'; ctx.font = `${14 * dpr}px monospace`; ctx.textAlign = 'left';
    ctx.fillText(follow >= 0 ? `BODY CAM — ${ghosts[follow].name.toUpperCase()}  ${U.fmtTime(t)}` : `MISSION REPLAY  ${U.fmtTime(t)}`, 16 * dpr, 26 * dpr);
    if (follow >= 0) { ctx.strokeStyle = 'rgba(255,255,255,0.2)'; ctx.lineWidth = 2; ctx.strokeRect(10 * dpr, 10 * dpr, canvas.width - 20 * dpr, canvas.height - 80 * dpr); ctx.fillStyle = Math.floor(now / 500) % 2 ? '#d9534f' : 'transparent'; ctx.beginPath(); ctx.arc(canvas.width - 30 * dpr, 30 * dpr, 7 * dpr, 0, 7); ctx.fill(); }
    // recent log line
    const recent = RM.log.filter(l => l.t <= t && t - l.t < 3);
    recent.slice(-3).forEach((l, i) => { ctx.fillStyle = l.color; ctx.font = `${12 * dpr}px monospace`; ctx.fillText(l.text, 16 * dpr, (50 + i * 18) * dpr); });
    const sl = document.getElementById('rp-t'); if (sl && document.activeElement !== sl) sl.value = Math.round(t * 10);
    const ck = document.getElementById('rp-clock'); if (ck) ck.textContent = `${U.fmtTime(t)} / ${U.fmtTime(RM.t)}`;
    const sp = document.getElementById('rp-spd'); if (sp) sp.textContent = '×' + speed;
    const fs = document.getElementById('rp-floor'); if (fs && +fs.value !== floor) fs.value = floor;
    requestAnimationFrame(loop);
  }
  return { open };
})();

BP.main = (() => {
  const U = BP.util, ST = BP.state, el = U.el, D = BP.D;
  const screens = ['title', 'hq', 'brief', 'mission', 'debrief', 'replay'];
  let titleAnim = null;
  function screen(name) {
    for (const s of screens) document.getElementById('scr-' + s).classList.toggle('hidden', s !== name);
    document.getElementById('modal').classList.add('hidden');
    if (name !== 'title' && titleAnim) { cancelAnimationFrame(titleAnim); titleAnim = null; }
    if (name === 'hq') { BP.audio.startAmbient('hq'); }
  }
  function title() {
    screen('title');
    const menu = document.getElementById('title-menu'); menu.innerHTML = '';
    const has = ST.hasSave();
    if (has) { const p = ST.load(); menu.append(el('button', { onclick: () => { BP.audio.init(); BP.audio.startMusic('hq'); BP.hq.show('command'); } }, `Continue — ${p ? p.unitName + ', day ' + p.day : ''}`)); }
    menu.append(el('button', { onclick: newGameDialog }, 'New Campaign'));
    menu.append(el('button', { onclick: () => { if (!ST.hasSave()) { ST.newGame({}); } BP.audio.init(); BP.audio.startMusic('hq'); BP.hq.show('command'); } }, 'Operations & Sandbox'));
    menu.append(el('button', { onclick: settings }, 'Settings'));
    menu.append(el('button', { onclick: howTo }, 'How to Play'));
    animateTitle();
  }
  function animateTitle() {
    const c = document.getElementById('title-bg'); const ctx = c.getContext('2d');
    const map = BP.mapgen.generate('compound', 4404);
    const start = performance.now();
    const loop = now => {
      c.width = c.clientWidth; c.height = c.clientHeight;
      const tt = (now - start) / 1000;
      const cam = { x: map.w * BP.TS / 2 + Math.cos(tt * 0.05) * 300, y: map.h * BP.TS / 2 + Math.sin(tt * 0.04) * 200, zoom: 0.9, rot: tt * 0.02 };
      BP.render.draw(ctx, { map, floor: 0, cam, units: [], items: [], scene: null, t: tt, time: tt, vision: [], showAll: true, night: true });
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      const g = ctx.createLinearGradient(0, 0, c.width, 0); g.addColorStop(0, 'rgba(8,10,14,0.95)'); g.addColorStop(0.55, 'rgba(8,10,14,0.55)'); g.addColorStop(1, 'rgba(8,10,14,0.2)');
      ctx.fillStyle = g; ctx.fillRect(0, 0, c.width, c.height);
      // radar sweep
      const cx = c.width * 0.75, cy = c.height * 0.5, r = Math.min(c.width, c.height) * 0.3;
      ctx.strokeStyle = 'rgba(226,163,58,0.25)'; ctx.lineWidth = 1; for (let i = 1; i <= 3; i++) { ctx.beginPath(); ctx.arc(cx, cy, r * i / 3, 0, 7); ctx.stroke(); }
      const a = tt * 1.2; const sg = ctx.createConicGradient ? ctx.createConicGradient(a - 0.6, cx, cy) : null;
      if (sg) { sg.addColorStop(0, 'rgba(226,163,58,0)'); sg.addColorStop(0.09, 'rgba(226,163,58,0.25)'); sg.addColorStop(0.1, 'rgba(226,163,58,0)'); ctx.fillStyle = sg; ctx.beginPath(); ctx.arc(cx, cy, r, 0, 7); ctx.fill(); }
      titleAnim = requestAnimationFrame(loop);
    };
    if (titleAnim) cancelAnimationFrame(titleAnim);
    titleAnim = requestAnimationFrame(loop);
  }
  function newGameDialog() {
    BP.audio.init();
    let diff = 'standard', iron = false;
    const name = el('input', { value: 'Task Force Breachpoint', style: 'width:260px' });
    const dsel = el('div', { class: 'col' });
    const draw = () => { dsel.innerHTML = ''; for (const [k, d] of Object.entries(D.DIFFICULTY)) dsel.append(el('div', { class: 'card' + (diff === k ? ' sel' : ''), onclick: () => { diff = k; draw(); } }, el('b', {}, d.name), el('div', { class: 'dim', style: 'font-size:11px' }, { casual: 'Forgiving damage. Hidden suspects are hinted. Cheaper equipment.', standard: 'Realistic tactical combat. Balanced AI and costs.', veteran: 'Limited intel, deadlier suspects. Downed operators may die.', milsim: 'Realistic damage, scarce resources. Operator death is permanent.' }[k]))); };
    draw();
    const box = el('div', {}, el('h2', {}, 'New Campaign'), el('div', { class: 'formgrid' }, 'Unit name', name), el('h3', { style: 'margin-top:10px' }, 'Difficulty'), dsel,
      el('label', { class: 'row', style: 'margin-top:10px' }, el('input', { type: 'checkbox', onchange: e => iron = e.target.checked }), el('span', {}, el('b', { class: 'red' }, 'Ironman'), ' — one life per operator, death is permanent on any difficulty, single autosave.')),
      ST.hasSave() ? el('p', { class: 'red' }, 'Warning: this will overwrite your current save.') : '',
      el('div', { class: 'row', style: 'margin-top:12px' }, el('button', { class: 'primary', onclick: () => { ST.newGame({ difficulty: diff, ironman: iron, unitName: name.value || 'Task Force Breachpoint' }); m.close(); BP.audio.startMusic('hq'); BP.hq.show('command'); BP.state.toast('Welcome, Commander. Start with "Corner Stash" in the Command Center.'); } }, 'Begin'), el('button', { onclick: () => m.close() }, 'Cancel')));
    const m = BP.ui.modal(box);
  }
  function settings() {
    BP.audio.init();
    const s = BP.audio.settings;
    const slider = (label, key) => [label, el('input', { type: 'range', min: 0, max: 100, value: Math.round(s[key] * 100), oninput: e => { s[key] = e.target.value / 100; BP.audio.save(); } })];
    const rows = [slider('Master volume', 'master'), slider('Effects', 'sfx'), slider('Ambience', 'ambient'), slider('Music', 'music'), ['Voice callouts', el('input', { type: 'checkbox', checked: s.voice, onchange: e => { s.voice = e.target.checked; BP.audio.save(); } })]];
    const box = el('div', {}, el('h2', {}, 'Settings'), el('div', { class: 'formgrid' }, ...rows.flat()),
      el('div', { class: 'row', style: 'margin-top:12px' }, el('button', { onclick: () => { BP.audio.S.flashbang(BP.audio.listener.x, BP.audio.listener.y); } }, 'Test sound'), el('button', { onclick: () => BP.audio.say('Radio check. Police! Show me your hands!', { prio: true }) }, 'Test voice'),
        ST.P ? el('button', { onclick: () => { ST.save(); ST.toast('Game saved.'); } }, 'Save now') : '',
        ST.hasSave() ? el('button', { class: 'danger', onclick: () => { if (confirm('Delete save permanently?')) { ST.wipe(); m.close(); title(); } } }, 'Delete save') : '',
        el('button', { class: 'primary', onclick: () => m.close() }, 'Close')));
    const m = BP.ui.modal(box);
  }
  function howTo() {
    const box = el('div', { style: 'max-width:720px', html: `<h2>How to Play</h2>
      <p><b class="acc">The loop.</b> From HQ pick an operation in the Command Center, choose your squad (vehicle seats limit size), tune loadouts and entry points, then deploy.</p>
      <p><b class="acc">Planning phase.</b> Missions start frozen. Right-click to set waypoints (Shift to chain), right-click doors for breach options (open, kick, flashbang &amp; clear, charge, shotgun, ram, pick lock, door camera). Press <b>B</b> to insert a "wait for GO" point, then <b>Enter</b> to execute. During the operation, <b>Space</b> pauses so you can re-plan at any time.</p>
      <p><b class="acc">Rules of engagement.</b> Operators shout compliance commands. Many suspects surrender — especially when flashed, gassed, hurt or outnumbered. Arrest them (right-click) for a much better rating than killing them. Shooting a surrendering suspect is an ROE violation. Operators hold fire when civilians or teammates are in the line of fire.</p>
      <p><b class="acc">Suspects</b> hear gunfire, doors and running footsteps, talk on radios, investigate, hide, ambush, flee and — if guarding hostages — may execute them when alerted (⚠ timer). Move quietly (slow pace), pick locks and use suppressors to keep surprise.</p>
      <p><b class="acc">Evidence</b> builds intelligence on each criminal organization. Enough intel makes takedown missions safer; capturing bosses alive weakens the next organization.</p>
      <p><b class="acc">Consequences.</b> Injuries take days to heal, fatigue builds, weapons wear. On Veteran/Milsim/Ironman, operators can die permanently.</p>
      <p><b class="acc">After action.</b> Each mission is rated S–C or Failed. Watch the replay from any operator's body cam.</p>
      <p class="dim">Press F1 or ? in a mission for the full control list.</p>` });
    box.append(el('button', { class: 'primary', onclick: () => m.close() }, 'Got it'));
    const m = BP.ui.modal(box);
  }
  function startMission(cfg, onEnd) {
    screen('mission');
    BP.audio.init();
    requestAnimationFrame(() => BP.missionUI.start(cfg, onEnd));
  }
  function boot() {
    document.getElementById('btn-settings').onclick = settings;
    document.getElementById('btn-quit').onclick = () => { ST.save(); BP.audio.stopAmbient(); title(); };
    document.addEventListener('click', () => BP.audio.init(), { once: true });
    if (window.speechSynthesis) speechSynthesis.getVoices();
    title();
  }
  return { screen, title, startMission, boot, settings };
})();
window.addEventListener('load', () => BP.main.boot());
// Called by the Android wrapper when the hardware back button is pressed. Returns true if handled.
window.bpBack = () => {
  const modal = document.getElementById('modal');
  if (!modal.classList.contains('hidden')) { modal.classList.add('hidden'); return true; }
  const menu = document.querySelector('.ctxmenu'); if (menu) { menu.remove(); return true; }
  const vis = id => !document.getElementById(id).classList.contains('hidden');
  if (vis('scr-mission')) { window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Escape' })); if (BP.sim.M && BP.sim.M.phase === 'live' && !BP.sim.M.paused) window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Space' })); return true; }
  if (vis('scr-brief')) { BP.hq.show(); return true; }
  if (vis('scr-replay')) { const b = [...document.querySelectorAll('#replay-ui button')].find(x => x.textContent.includes('Exit')); if (b) b.click(); return true; }
  if (vis('scr-hq')) { BP.state.save(); BP.main.title(); return true; }
  return false;
};
