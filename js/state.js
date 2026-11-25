// Persistent profile state: roster, armory, campaign, economy, saving
'use strict';
BP.state = (() => {
  const D = BP.D, U = BP.util;
  const KEY = 'breachpoint_save_v1';
  let P = null; // profile

  const OP_COLORS = ['#4fc3f7', '#ffb74d', '#81c784', '#e57373', '#ba68c8', '#fff176', '#4db6ac', '#f06292', '#a1887f', '#90a4ae', '#dce775', '#7986cb'];

  function newWeapon(base, opts = {}) {
    const w = D.WEAPON[base];
    return { uid: U.uid('w'), base, att: {}, finish: 'black', camo: ['#3a3a3a', '#777777'], engraving: '', serial: 'TF-' + Math.floor(10000 + Math.random() * 89999), condition: 100, roundsFired: 0, ...opts, _cat: w.cat };
  }

  function genOperator(rng, opts = {}) {
    rng = rng || BP.R;
    const gender = opts.gender || (rng.chance(0.3) ? 'f' : 'm');
    const first = rng.pick(gender === 'f' ? D.NAMES_F : D.NAMES_M);
    const last = rng.pick(D.SURNAMES);
    const quality = opts.quality ?? 1;
    const stats = {};
    for (const s of D.STATS) stats[s.id] = Math.round(U.clamp(rng.int(30, 60) + (quality - 1) * 12 + rng.gauss() * 6, 15, 85));
    const role = opts.role || rng.pick(['assault', 'assault', 'point', 'medic', 'tech', 'marksman']);
    if (role === 'medic') stats.med = Math.max(stats.med, 60 + rng.int(0, 15));
    if (role === 'tech') stats.tec = Math.max(stats.tec, 60 + rng.int(0, 15));
    if (role === 'marksman') stats.acc = Math.max(stats.acc, 58 + rng.int(0, 15));
    if (role === 'leader') stats.lea = Math.max(stats.lea, 60 + rng.int(0, 15));
    if (role === 'point') stats.awa = Math.max(stats.awa, 55 + rng.int(0, 15));
    const op = {
      id: U.uid('op'), name: first + ' ' + last, callsign: rng.pick(D.CALLSIGNS), gender,
      look: {
        face: rng.int(0, D.FACES.length - 1), skin: rng.int(0, D.SKIN.length - 1),
        hair: gender === 'f' ? rng.pick([1, 5, 6, 7, 9, 10]) : rng.pick([0, 1, 2, 3, 4, 8, 9]),
        hairColor: rng.int(0, D.HAIR_COLORS.length - 1),
        facial: gender === 'f' ? 0 : rng.pick([0, 0, 1, 1, 2, 3, 4, 5]),
        build: rng.int(0, 3), uniform: 'swat', shirt: null, pants: null, carrier: null,
        helmet: true, balaclava: rng.chance(0.25), eyepro: rng.int(0, 3), headset: rng.int(1, 2),
        gloves: 0, boots: 0, kneepads: rng.int(0, 2), watch: rng.int(0, 2), patch: rng.int(0, 6), unitId: 'TF-1',
      },
      stats, personality: opts.personality || rng.pick(D.PERSONALITIES).id, role,
      xp: 0, level: 1, points: 0, perks: [], perkPicks: 0,
      morale: 70, fatigue: 0, injury: 0, injuryDesc: '', status: 'active',
      missions: 0, kills: 0, arrests: 0, rescues: 0, shots: 0, hits: 0, bonds: {},
      bio: `${first} ${last} is a ${rng.pick(D.BACKGROUNDS)} from ${rng.pick(D.HOMETOWNS)}.`,
      age: rng.int(24, 44), color: null,
      loadout: { primary: null, secondary: null, armor: 'light', helmet: 'bump', ammo: 'fmj', util: ['flash', 'flash'], tool: null },
      history: [],
    };
    return op;
  }

  function newGame({ difficulty = 'standard', ironman = false, unitName = 'Task Force Breachpoint' } = {}) {
    const rng = U.rng(Date.now());
    P = {
      version: 1, unitName, difficulty, ironman, day: 1, money: 25000,
      operators: [], fallen: [],
      weapons: [], stock: { flash: 10, smoke: 4, gas: 0, stinger: 2, charge: 3, medkit: 2, lockpick: 1, doorcam: 0, shield: 0, ram: 1 },
      armorOwned: ['none', 'light'], helmetsOwned: ['none', 'bump'], ammoOwned: ['fmj'],
      vehicles: [{ id: 'suv', uid: U.uid('v'), paint: '#20252b', markings: 'TF-1', lights: 'hidden', accessories: [] }],
      activeVehicle: null,
      campaign: { done: {}, intel: { ldn: 0, bwl: 0, syn: 0, mer: 0 }, hvtCaptured: {}, complete: false },
      evidence: [], log: [], hq: { training: 1, medical: 1, armory: 1 },
      stats: { missions: 0, success: 0, arrests: 0, kills: 0, rescued: 0, lost: 0 },
      opsBoard: null, opsDay: 0, weeklyDone: -1,
    };
    P.activeVehicle = P.vehicles[0].uid;
    const roles = ['leader', 'point', 'assault', 'assault', 'medic', 'tech'];
    const used = new Set();
    roles.forEach((r, i) => {
      const op = genOperator(rng, { role: r, quality: 1 });
      while (used.has(op.callsign)) op.callsign = rng.pick(D.CALLSIGNS);
      used.add(op.callsign);
      op.color = OP_COLORS[i % OP_COLORS.length];
      P.operators.push(op);
    });
    // starting armory
    const add = (b, n) => { for (let i = 0; i < n; i++) P.weapons.push(newWeapon(b)); };
    add('m4a1', 3); add('mp5', 2); add('r870', 1); add('g17', 6);
    P.weapons.filter(w => w.base === 'm4a1').forEach(w => w.att = { optic: 'rds', side: 'light' });
    P.weapons.filter(w => w.base === 'mp5').forEach(w => w.att = { side: 'light' });
    // auto-assign loadouts
    const prim = P.weapons.filter(w => D.WEAPON[w.base].cat !== 'hg');
    const sec = P.weapons.filter(w => D.WEAPON[w.base].cat === 'hg');
    P.operators.forEach((op, i) => {
      op.loadout.primary = prim[i] ? prim[i].uid : null;
      op.loadout.secondary = sec[i] ? sec[i].uid : null;
      if (op.role === 'medic') op.loadout.tool = 'medkit';
      if (op.role === 'tech') op.loadout.tool = 'lockpick';
      if (op.role === 'point') op.loadout.tool = 'ram';
    });
    save();
    return P;
  }

  function save() { if (P) try { localStorage.setItem(KEY, JSON.stringify(P)); } catch (e) { console.warn('save failed', e); } }
  function load() {
    try { const s = localStorage.getItem(KEY); if (!s) return null; P = JSON.parse(s); return P; } catch (e) { return null; }
  }
  function hasSave() { return !!localStorage.getItem(KEY); }
  function wipe() { localStorage.removeItem(KEY); P = null; }

  const weaponByUid = uid => P.weapons.find(w => w.uid === uid);
  const opById = id => P.operators.find(o => o.id === id);
  const diff = () => D.DIFFICULTY[P.difficulty];

  // Derived weapon statistics from base + attachments + condition
  function weaponStats(w) {
    const b = D.WEAPON[w.base];
    const m = { weight: 0, handling: 0, recoil: 0, accuracy: 0, aimSpeed: 0, ergonomics: 0, reliability: 0, suppression: 0, visibility: 0, light: 0, range: 0, damage: 0, magMult: 0 };
    for (const slot in w.att) {
      const a = D.ATT[w.att[slot]]; if (!a) continue;
      for (const k in a.mods) m[k] = (m[k] || 0) + a.mods[k];
    }
    const weight = Math.max(0.5, b.weight + m.weight);
    const handling = U.clamp(b.handling + m.handling - (weight - b.weight) * 6, 5, 100);
    const recoil = U.clamp(b.recoil + m.recoil, 5, 100);
    const accuracy = U.clamp(100 - b.spread * 12 + m.accuracy, 5, 100);
    const aimSpeed = U.clamp(handling * 0.8 + m.aimSpeed + 10, 5, 100);
    const ergonomics = U.clamp(50 + m.ergonomics + (b.cat === 'hg' ? 20 : 0) + (b.cat === 'smg' ? 10 : 0), 5, 100);
    const condF = 0.6 + 0.4 * (w.condition / 100);
    const reliability = U.clamp((b.rel + m.reliability) * condF, 5, 100);
    const suppression = U.clamp(m.suppression, -30, 90);
    const visibility = U.clamp(50 + m.visibility, 0, 100);
    const cal = D.CAL[b.cal];
    return {
      name: b.name, cat: b.cat, cal: b.cal, mode: b.mode, rpm: b.rpm,
      mag: Math.round(b.mag * (1 + m.magMult)),
      spread: b.spread * (1 - m.accuracy / 100 * 0.9),
      weight, handling, recoil, accuracy, aimSpeed, ergonomics, reliability, suppression, visibility,
      light: m.light > 0, range: m.range, dmg: cal.dmg * (1 + m.damage / 100), pen: cal.pen, pellets: cal.pellets || 1,
      suppressed: suppression > 50, noise: (1 - suppression / 100),
      reload: D.CATS[b.cat].reload * (1.25 - ergonomics / 200),
      price: b.price,
    };
  }
  function weaponValue(w) {
    let v = D.WEAPON[w.base].price;
    for (const s in w.att) { const a = D.ATT[w.att[s]]; if (a) v += a.price; }
    return v;
  }

  // Unlock tier = 1 + number of orgs completed (campaign); endgame = 4
  function tier() {
    const c = P.campaign.done;
    let t = 1;
    for (const o of D.ORGS) { const tk = D.CAMPAIGN.find(m => m.org === o.id && m.takedown); if (c[tk.id]) t++; }
    return Math.min(4, t + (P.stats.missions >= 6 ? 0 : 0));
  }
  function campaignAvailable(m) {
    if (P.campaign.done[m.id]) return true;
    const orgIdx = D.ORGS.findIndex(o => o.id === m.org);
    if (orgIdx > 0) { const prevTk = D.CAMPAIGN.find(x => x.org === D.ORGS[orgIdx - 1].id && x.takedown); if (!P.campaign.done[prevTk.id]) return false; }
    if (m.idx > 0) { const prev = D.CAMPAIGN.find(x => x.org === m.org && x.idx === m.idx - 1); if (!P.campaign.done[prev.id]) return false; }
    return true;
  }

  function addXP(op, xp) {
    op.xp += xp;
    let leveled = false;
    while (op.xp >= xpFor(op.level + 1)) {
      op.level++; op.points += 3; leveled = true;
      if (op.level % 3 === 0) op.perkPicks++;
    }
    return leveled;
  }
  const xpFor = lvl => Math.round(250 * (lvl - 1) * (1 + (lvl - 1) * 0.35));

  function opMaxHP(op) { return 100 + (op.perks.includes('tough') ? 15 : 0); }

  function advanceDay(days = 1) {
    P.day += days;
    for (const op of P.operators) {
      if (op.injury > 0) { op.injury = Math.max(0, op.injury - days); if (op.injury === 0) { op.status = 'active'; op.injuryDesc = ''; } }
      op.fatigue = Math.max(0, op.fatigue - 18 * days);
      op.morale = U.clamp(op.morale + (op.morale < 60 ? 3 : 0) * days, 0, 100);
    }
  }

  function toast(msg) {
    const t = document.getElementById('toast');
    const d = document.createElement('div'); d.textContent = msg; t.append(d);
    setTimeout(() => d.remove(), 3100);
  }

  return {
    get P() { return P; }, set P(v) { P = v; }, newGame, save, load, hasSave, wipe, genOperator, newWeapon, weaponByUid, opById, weaponStats, weaponValue,
    diff, tier, campaignAvailable, addXP, xpFor, opMaxHP, advanceDay, toast, OP_COLORS,
  };
})();
