// Tactical mission simulation: units, orders, AI, ballistics, grenades, objectives, recording
'use strict';
BP.sim = (() => {
  const T = BP.T, TS = BP.TS, D = BP.D, U = BP.util;
  const A = () => BP.audio.S;
  let M = null;
  const R = BP.R;

  // ---------------- Map queries ----------------
  const tileAt = (f, x, y) => (x < 0 || y < 0 || x >= M.map.w || y >= M.map.h) ? T.WALL : M.map.floors[f].t[y * M.map.w + x];
  const doorAt = (f, x, y) => { const id = M.map.floors[f].door[y * M.map.w + x]; return id >= 0 ? M.map.doors[id] : null; };
  const toTile = v => Math.floor(v / TS);
  const ctr = v => (v + 0.5) * TS;
  function blocksVision(f, x, y) {
    const t = tileAt(f, x, y);
    if (t === T.WALL || t === T.HIGH) return true;
    if (t === T.DOOR) { const d = doorAt(f, x, y); return d && d.state === 'closed'; }
    return false;
  }
  function walkable(f, x, y, who) {
    const t = tileAt(f, x, y);
    if (t === T.GROUND || t === T.FLOOR || t === T.STAIRS || t === T.BWIN) return true;
    if (t === T.DOOR) { const d = doorAt(f, x, y); return !(d.barricaded && d.state === 'closed'); }
    if (t === T.WINDOW) return who === 'op';
    return false;
  }
  function smokeBlocks(f, x0, y0, x1, y1) {
    for (const s of M.smokes) {
      if (s.f !== f || s.r < 8) continue;
      const dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy || 1;
      let t = ((s.x - x0) * dx + (s.y - y0) * dy) / l2; t = U.clamp(t, 0, 1);
      const px = x0 + dx * t - s.x, py = y0 + dy * t - s.y;
      if (px * px + py * py < (s.r * 0.85) ** 2) return true;
    }
    return false;
  }
  function los(f, x0, y0, x1, y1, ignoreSmoke) {
    const d = Math.hypot(x1 - x0, y1 - y0); const n = Math.ceil(d / 8);
    let lx = -1, ly = -1;
    for (let i = 1; i < n; i++) {
      const tx = toTile(x0 + (x1 - x0) * i / n), ty = toTile(y0 + (y1 - y0) * i / n);
      if (tx === lx && ty === ly) continue; lx = tx; ly = ty;
      if (blocksVision(f, tx, ty)) return false;
    }
    return ignoreSmoke || !smokeBlocks(f, x0, y0, x1, y1);
  }

  // ---------------- Pathfinding (multi-floor A*) ----------------
  function findPath(f0, x0, y0, f1, x1, y1, who = 'op', maxIter = 30000) {
    const m = M.map, W = m.w, H = m.h, N = W * H * m.floors.length;
    if (!walkable(f1, x1, y1, who)) { const alt = nearestWalkable(f1, x1, y1, who, 3); if (!alt) return null; [x1, y1] = alt; }
    const g = new Float32Array(N).fill(1e9), from = new Int32Array(N).fill(-1), closed = new Uint8Array(N);
    const start = f0 * W * H + y0 * W + x0, goal = f1 * W * H + y1 * W + x1;
    const heap = []; const push = (n, p) => { heap.push([p, n]); let i = heap.length - 1; while (i > 0) { const pa = (i - 1) >> 1; if (heap[pa][0] <= heap[i][0]) break; [heap[pa], heap[i]] = [heap[i], heap[pa]]; i = pa; } };
    const pop = () => { const top = heap[0]; const last = heap.pop(); if (heap.length) { heap[0] = last; let i = 0; for (; ;) { const l = 2 * i + 1, r = l + 1; let s = i; if (l < heap.length && heap[l][0] < heap[s][0]) s = l; if (r < heap.length && heap[r][0] < heap[s][0]) s = r; if (s === i) break; [heap[s], heap[i]] = [heap[i], heap[s]]; i = s; } } return top; };
    const hfn = (f, x, y) => { const dx = Math.abs(x - x1), dy = Math.abs(y - y1); return Math.max(dx, dy) + 0.414 * Math.min(dx, dy) + Math.abs(f - f1) * 4; };
    g[start] = 0; push(start, hfn(f0, x0, y0));
    let it = 0;
    while (heap.length && it++ < maxIter) {
      const [, n] = pop(); if (closed[n]) continue; closed[n] = 1;
      if (n === goal) break;
      const f = Math.floor(n / (W * H)), rem = n % (W * H), y = Math.floor(rem / W), x = rem % W;
      const tHere = tileAt(f, x, y);
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        if (!dx && !dy) continue;
        const nx = x + dx, ny = y + dy;
        if (!walkable(f, nx, ny, who)) continue;
        const tn = tileAt(f, nx, ny);
        let c = 1;
        if (dx && dy) {
          if (!walkable(f, x + dx, y, who) || !walkable(f, x, y + dy, who)) continue;
          const ta = tileAt(f, x + dx, y), tb = tileAt(f, x, y + dy);
          if (tn === T.DOOR || tHere === T.DOOR || ta === T.DOOR || tb === T.DOOR || tn === T.WINDOW || tHere === T.WINDOW) continue;
          c = 1.414;
        }
        if (tn === T.WINDOW) c += 7;
        if (tn === T.DOOR) { c += 0.3; const d = doorAt(f, nx, ny); if (who === 'op' && d.state === 'closed' && d.knownLocked) c += 6; }
        const nn = f * W * H + ny * W + nx;
        if (closed[nn]) continue;
        const ng = g[n] + c;
        if (ng < g[nn]) { g[nn] = ng; from[nn] = n; push(nn, ng + hfn(f, nx, ny)); }
      }
      if (tHere === T.STAIRS) for (const of of [f - 1, f + 1]) {
        if (of < 0 || of >= m.floors.length || tileAt(of, x, y) !== T.STAIRS) continue;
        const nn = of * W * H + y * W + x; if (closed[nn]) continue;
        const ng = g[n] + 3; if (ng < g[nn]) { g[nn] = ng; from[nn] = n; push(nn, ng + hfn(of, x, y)); }
      }
    }
    if (from[goal] < 0 && goal !== start) return null;
    const path = []; let c = goal;
    while (c !== start && c >= 0) { const f = Math.floor(c / (W * H)), rem = c % (W * H); path.push({ f, x: rem % W, y: Math.floor(rem / W) }); c = from[c]; }
    path.reverse();
    return path;
  }
  function nearestWalkable(f, x, y, who, rad) {
    for (let r = 1; r <= rad; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const t = tileAt(f, x + dx, y + dy);
      if (walkable(f, x + dx, y + dy, who) && t !== T.DOOR && t !== T.WINDOW) return [x + dx, y + dy];
    }
    return null;
  }

  // ---------------- Logging / voice / fx ----------------
  function log(text, color = '#c9d3dc') { M.log.push({ t: M.t, text, color }); if (M.log.length > 80) M.log.shift(); }
  function voice(u, cat, prio = false) {
    const lines = D.VOICE[cat]; if (!lines) return;
    const line = lines[Math.floor(R() * lines.length)];
    if (u) log(`${u.name}: "${line}"`, u.color || '#9fb0bf');
    if (M.replayMode) return;
    BP.audio.say(line, { pitch: u ? u.pitch : 1, prio });
  }
  const fx = e => { M.fx.push(e); };
  function noise(x, y, f, rTiles, kind = 'noise', src = null) { M.noises.push({ x, y, f, r: rTiles * TS * (M.rain ? 0.8 : 1), kind, src }); if (rTiles >= 10) M.heard.push({ x, y, f, t: M.t, kind }); }

  // ---------------- Unit construction ----------------
  function lookFromOp(op) {
    const L = op.look, uc = BP.art.uniformColors(L);
    const helmetOn = op.loadout.helmet !== 'none' && L.helmet && uc.helmet;
    return { shirt: uc.shirt, pants: uc.pants, carrier: uc.carrier, helmet: helmetOn ? uc.helmet : null, skin: D.SKIN[L.skin], hair: D.HAIR_COLORS[L.hairColor], glove: uc.glove, camo: uc.camo, balaclava: L.balaclava, build: L.build };
  }
  function mkWeapon(inst) {
    const st = BP.state.weaponStats(inst);
    const reserveMags = st.cat === 'hg' ? 3 : st.cat === 'sg' ? 0 : st.cat === 'sr' ? 4 : 5;
    return { inst, st, mag: st.mag, reserve: st.cat === 'sg' ? 28 : st.mag * reserveMags, fired: 0 };
  }
  function baseUnit(o) {
    return Object.assign({
      id: U.uid('u'), x: 0, y: 0, floor: 0, angle: 0, hp: 100, maxHp: 100, alive: true, dead: false, downed: false, incap: false,
      orders: [], path: null, pi: 0, act: null, target: null, aimT: 0, reactT: 0.5, fireCd: 0, burst: 0, burstPause: 0, recoilHeat: 0, reloadT: 0, jamT: 0,
      stun: 0, gas: 0, stress: 0, stance: 'stand', pace: 'walk', speedMult: 1, armor: 0, helmet: 0, weapons: [], wi: 0, stepT: 0, status: '', visible: false,
      lastSeen: null, kills: 0, hits: 0, shots: 0, look: {},
    }, o);
  }
  function makeOperator(op, x, y, f, ang) {
    const L = op.loadout, P = BP.state;
    const arm = D.ARMORD[L.armor] || D.ARMOR[1], hel = D.HELMD[L.helmet] || D.HELMETS[0];
    const ws = [];
    const prim = L.primary && P.weaponByUid(L.primary), sec = L.secondary && P.weaponByUid(L.secondary);
    if (prim) ws.push(mkWeapon(prim)); if (sec) ws.push(mkWeapon(sec));
    if (!ws.length) ws.push(mkWeapon(P.newWeapon('g17')));
    const shield = L.tool === 'shield';
    const pers = D.PERS[op.personality] || D.PERSONALITIES[0];
    const maxHp = P.opMaxHP(op);
    const u = baseUnit({
      team: 'op', kind: 'operator', name: op.callsign, fullName: op.name, op, x: ctr(x), y: ctr(y), floor: f, angle: ang,
      hp: maxHp, maxHp, armor: arm.prot, helmet: hel.prot, speedMult: arm.speed * (hel.speed || 1) * pers.speed * (op.perks.includes('runner') ? 1.1 : 1) * (0.85 + op.stats.mob / 333),
      weapons: ws, wi: shield && ws.length > 1 ? ws.findIndex(w => w.st.cat === 'hg') : 0, shield,
      util: L.util.slice(), tool: L.tool, toolUses: L.tool === 'medkit' ? 3 : L.tool === 'charge' ? 0 : 99,
      color: op.color, look: lookFromOp(op), stats: op.stats, perks: op.perks, pers, ammo: D.AMMOD[L.ammo] || D.AMMO[0],
      pitch: 0.75 + (U.hashStr(op.id) % 50) / 100 + (op.gender === 'f' ? 0.35 : 0), female: op.gender === 'f',
      bleed: 0, stable: false, follow: null, watchAngle: null, goWait: null, hold: false, damageTaken: 0, arrests: 0, rescues: 0, evidence: 0,
    });
    if (u.wi < 0) u.wi = 0;
    if (u.shield && u.weapons[u.wi].st.cat !== 'hg') { u.shield = false; }
    return u;
  }
  function enemyLook(kind, rng) {
    const shirts = kind === 'pro' ? ['#1e2126', '#2a2f26', '#24282e'] : kind === 'inmate' ? ['#d86a1f', '#cf6a24'] : ['#7a3030', '#30507a', '#555', '#2d4a2d', '#6a5a3a', '#8a8a8a', '#3a2a4a', '#b0a080', '#222'];
    return { shirt: rng.pick(shirts), pants: rng.pick(['#2d3a55', '#333', '#4a4030', '#222']), skin: rng.pick(D.SKIN), hair: rng.pick(D.HAIR_COLORS), helmet: kind === 'pro' && rng.chance(0.5) ? '#2a2d30' : null, balaclava: (kind === 'crim' || kind === 'pro') && rng.chance(0.4) };
  }
  function makeEnemy(kind, x, y, f, rng, opts = {}) {
    const E = D.ENEMY[kind];
    let wid = rng.pick(E.weapons);
    if (opts.armed && wid === 'none') wid = 'g17';
    const armed = wid !== 'none';
    const ws = [];
    if (armed) {
      const inst = { uid: 'e', base: wid, att: {}, condition: 70 + rng.int(0, 30) };
      if (kind === 'pro' || (kind === 'crim' && rng.chance(0.4))) inst.att = { optic: D.WEAPON[wid].cat === 'hg' ? 'rmr' : 'holo', side: 'light' };
      if (kind === 'pro' && rng.chance(0.3) && D.WEAPON[wid].cat !== 'hg') inst.att.muzzle = 'supp';
      ws.push(mkWeapon(inst));
    }
    const tierArmor = E.armor + (M.tier >= 3 && kind !== 'associate' && kind !== 'hvt' ? 15 : 0);
    const e = baseUnit({
      team: 'enemy', kind, name: opts.name || E.name, x: ctr(x), y: ctr(y), floor: f, angle: rng.range(-Math.PI, Math.PI),
      weapons: ws, wi: 0, armed, armor: tierArmor, helmet: kind === 'pro' ? 40 : 0, look: enemyLook(kind, rng), speedMult: 1,
      acc: E.acc, react: E.react,
      ai: { state: 'idle', post: { x: ctr(x), y: ctr(y), f }, style: opts.style || rng.weighted(['rusher', 'holder', 'hider'], s => s === 'rusher' ? E.aggr : s === 'holder' ? 0.6 : (1 - E.aggr) * 0.7), morale: E.morale + rng.int(-10, 10), radio: E.radio, patrol: null, pi: 0, waitT: rng.range(1, 4), decideT: 0, shouted: 0, lostT: 0, lastKnown: null, execT: -1, guard: opts.guard || null, alerted: false, investigate: null, surrT: 0, checkT: 0 },
    });
    e.angle = rng.range(-Math.PI, Math.PI);
    if (opts.hvt) { e.hvt = true; }
    if (opts.lieutenant) { e.lieutenant = true; }
    if (opts.dummy) { e.ai.dummy = true; }
    return e;
  }
  function makeCiv(kind, x, y, f, rng) {
    const c = baseUnit({ team: 'civ', kind, name: kind === 'hostage' ? 'Hostage' : 'Civilian', x: ctr(x), y: ctr(y), floor: f, angle: rng.range(-3, 3), look: { shirt: rng.pick(['#c8c0a0', '#4a7ab0', '#b04a4a', '#e0e0e0', '#5a8a5a', '#d0a040', '#8a5ab0', '#2a3a4a']), pants: rng.pick(['#2d3a55', '#555', '#8a7a5a']), skin: rng.pick(D.SKIN), hair: rng.pick(D.HAIR_COLORS) }, speedMult: 1, ai: { state: 'idle', waitT: rng.range(2, 6) } });
    if (kind === 'hostage') { c.hostageBound = true; c.kneel = true; }
    if (M.cfg.type === 'prison' && kind === 'hostage') { c.name = 'Corrections Officer'; c.look.shirt = '#3a4a6a'; }
    return c;
  }

  // ---------------- Mission creation ----------------
  function create(cfg) {
    const rng = U.rng(cfg.placeSeed || (Date.now() & 0xffffff));
    const map = cfg.map;
    M = {
      cfg, map, rng, t: 0, paused: true, phase: 'plan', speed: 1, units: [], items: [], grenades: [], smokes: [], gases: [], fx: [], noises: [], heard: [], log: [],
      night: !!cfg.night, rain: !!cfg.rain, diff: D.DIFFICULTY[cfg.difficulty] || D.DIFFICULTY.standard, tier: cfg.tier || 1,
      frames: [], events: [], recT: 0, objT: 0, ended: false, result: null, goCodes: {}, camSources: [], allClearAnnounced: false,
      stats: { shotsOp: 0, hitsOp: 0, civKilledByOp: 0, roe: 0, flashes: 0, smokes: 0, charges: 0, escaped: 0 },
    };
    M.map.doors.forEach(d => { d.state0 = d.state; });
    // operators at spawns
    cfg.squad.forEach((s, i) => {
      const sp = map.spawns[s.entry] || map.spawns[0];
      const slots = spawnSlots(sp, cfg.squad.length + 4);
      const k = cfg.squad.slice(0, i).filter(q => q.entry === s.entry).length;
      const [x, y] = slots[k] || [sp.x, sp.y];
      const u = makeOperator(s.op, x, y, 0, sp.ang);
      u.entry = s.entry; u.slot = i;
      M.units.push(u);
    });
    populate(rng);
    M.exits = computeExits();
    log(`Operation ${cfg.name} — planning phase. Issue orders, then EXECUTE.`, '#e2a33a');
    return M;
  }
  function spawnSlots(sp, n) {
    const out = [];
    for (let r = 0; r < 6 && out.length < n; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const x = sp.x + dx, y = sp.y + dy;
      if (tileAt(0, x, y) === T.GROUND && !out.some(o => o[0] === x && o[1] === y)) out.push([x, y]);
    }
    return out;
  }
  function computeExits() {
    const m = M.map, ex = [];
    for (let x = 1; x < m.w - 1; x++) for (const y of [1, m.h - 2]) if (tileAt(0, x, y) === T.GROUND) ex.push([x, y]);
    for (let y = 1; y < m.h - 1; y++) for (const x of [1, m.w - 2]) if (tileAt(0, x, y) === T.GROUND) ex.push([x, y]);
    return ex;
  }

  // distance field from spawns (BFS across floors)
  function distField() {
    const m = M.map, W = m.w, H = m.h, N = W * H * m.floors.length;
    const dist = new Int32Array(N).fill(-1), q = [];
    for (const s of m.spawns) { const n = s.y * W + s.x; dist[n] = 0; q.push(n); }
    for (let qi = 0; qi < q.length; qi++) {
      const n = q[qi]; const f = Math.floor(n / (W * H)), rem = n % (W * H), y = Math.floor(rem / W), x = rem % W;
      const nb = [[f, x + 1, y], [f, x - 1, y], [f, x, y + 1], [f, x, y - 1]];
      if (tileAt(f, x, y) === T.STAIRS) { if (f > 0 && tileAt(f - 1, x, y) === T.STAIRS) nb.push([f - 1, x, y]); if (f + 1 < m.floors.length && tileAt(f + 1, x, y) === T.STAIRS) nb.push([f + 1, x, y]); }
      for (const [nf, nx, ny] of nb) {
        if (!walkable(nf, nx, ny, 'enemy')) continue;
        const nn = nf * W * H + ny * W + nx; if (dist[nn] >= 0) continue;
        dist[nn] = dist[n] + 1; q.push(nn);
      }
    }
    return dist;
  }

  function populate(rng) {
    const cfg = M.cfg, m = M.map, W = m.w, H = m.h;
    const dist = distField();
    const occupied = new Set();
    const key = (f, x, y) => f + ':' + x + ':' + y;
    const roomTiles = r => { const out = []; for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) { const t = tileAt(r.f, x, y); if ((t === T.FLOOR) && !occupied.has(key(r.f, x, y)) && dist[r.f * W * H + y * W + x] > 6) out.push([x, y]); } return out; };
    const rooms = m.rooms.filter(r => r.tag !== 'corridor' || r.w * r.h > 20).map(r => {
      let dsum = 0, n = 0; for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) { const d = dist[r.f * W * H + y * W + x]; if (d >= 0) { dsum += d; n++; } }
      return { r, depth: n ? dsum / n : 0, reachable: n > 0 };
    }).filter(o => o.reachable);
    rooms.sort((a, b) => b.depth - a.depth);
    if (!rooms.length) return;
    const pickTile = (room, near) => {
      const tiles = roomTiles(room); if (!tiles.length) return null;
      let t = rng.pick(tiles);
      if (near) { tiles.sort((a, b) => Math.hypot(a[0] - near[0], a[1] - near[1]) - Math.hypot(b[0] - near[0], b[1] - near[1])); t = tiles[Math.min(tiles.length - 1, rng.int(1, 3))]; }
      occupied.add(key(room.f, t[0], t[1])); return t;
    };
    const deep = rooms.slice(0, Math.max(1, Math.ceil(rooms.length * 0.4)));
    const enemyKind = () => {
      const org = cfg.org; const tier = M.tier;
      if (cfg.type === 'prison') return 'inmate';
      const pool = tier <= 1 ? ['associate', 'associate', 'guard', 'guard'] : tier === 2 ? ['associate', 'guard', 'guard', 'crim'] : tier === 3 ? ['guard', 'crim', 'crim', 'pro'] : ['crim', 'pro', 'pro', 'crim'];
      return rng.pick(pool);
    };
    let enemiesLeft = cfg.enemies;
    const nameSuspect = () => `${rng.pick(D.SUSPECT_FIRST)} "${rng.pick(D.SUSPECT_NICK)}" ${rng.pick(D.SURNAMES)}`;
    // training dummies
    if (cfg.training) {
      for (let i = 0; i < cfg.enemies; i++) { const room = rng.pick(rooms).r; const t = pickTile(room); if (!t) continue; const e = makeEnemy('guard', t[0], t[1], room.f, rng, { dummy: true, armed: true }); e.name = 'Target'; M.units.push(e); }
      for (let i = 0; i < cfg.civilians; i++) { const room = rng.pick(rooms).r; const t = pickTile(room); if (!t) continue; const c = makeCiv('civilian', t[0], t[1], room.f, rng); c.ai.dummy = true; c.name = 'No-Shoot Target'; M.units.push(c); }
      return;
    }
    // hostages
    const hostRooms = [];
    for (let i = 0; i < cfg.hostages; i++) {
      let hr = hostRooms.length && rng.chance(0.55) ? rng.pick(hostRooms) : rng.pick(deep).r;
      if (!hostRooms.includes(hr)) hostRooms.push(hr);
      const t = pickTile(hr); if (!t) continue;
      const h = makeCiv('hostage', t[0], t[1], hr.f, rng); h.room = hr.id;
      M.units.push(h);
    }
    for (const hr of hostRooms) {
      if (enemiesLeft <= 0) break;
      const hs = M.units.filter(u => u.kind === 'hostage' && u.room === hr.id);
      const t = pickTile(hr, hs[0] ? [toTile(hs[0].x), toTile(hs[0].y)] : null); if (!t) continue;
      const e = makeEnemy(enemyKind() === 'associate' ? 'guard' : enemyKind(), t[0], t[1], hr.f, rng, { guard: hs[0] || null, style: 'holder', armed: true });
      e.ai.guardList = hs; e.angle = Math.atan2(hs[0].y - e.y, hs[0].x - e.x) + Math.PI;
      M.units.push(e); enemiesLeft--;
    }
    // HVT
    if (cfg.hvt) {
      const hr = deep[0].r; const t = pickTile(hr);
      if (t) { const e = makeEnemy('hvt', t[0], t[1], hr.f, rng, { hvt: true, name: cfg.hvtName || nameSuspect(), armed: true }); e.ai.style = 'hider'; M.units.push(e); M.hvt = e; }
      if (enemiesLeft > 0) { const t2 = pickTile(hr); if (t2) { M.units.push(makeEnemy(M.tier >= 3 ? 'pro' : 'guard', t2[0], t2[1], hr.f, rng, { style: 'holder', armed: true })); enemiesLeft--; } }
    }
    if (cfg.lieutenant && enemiesLeft > 0) {
      const hr = rng.pick(rooms.slice(0, Math.ceil(rooms.length * 0.7))).r; const t = pickTile(hr);
      if (t) { const e = makeEnemy('lieutenant', t[0], t[1], hr.f, rng, { lieutenant: true, name: nameSuspect(), armed: true }); M.units.push(e); enemiesLeft--; }
    }
    // remaining enemies
    const outdoorTiles = [];
    for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) if (tileAt(0, x, y) === T.GROUND && dist[y * W + x] > 16) outdoorTiles.push([x, y]);
    const hasYard = ['compound', 'rural', 'shipyard', 'hangar', 'mansion', 'bunker', 'prison'].includes(m.type);
    while (enemiesLeft > 0) {
      const kind = enemyKind();
      const roll = rng();
      if (hasYard && roll < 0.3 && outdoorTiles.length) {
        const [x, y] = rng.pick(outdoorTiles);
        const e = makeEnemy(kind, x, y, 0, rng, { style: rng.chance(0.5) ? 'rusher' : 'holder' });
        // patrol between outdoor points
        e.ai.patrol = [[x, y], rng.pick(outdoorTiles), rng.pick(outdoorTiles)].filter(p => Math.hypot(p[0] - x, p[1] - y) < 25);
        M.units.push(e);
      } else {
        const ro = rng.chance(0.6) ? rng.pick(deep) : rng.pick(rooms);
        const t = pickTile(ro.r); if (!t) { enemiesLeft--; continue; }
        const e = makeEnemy(kind, t[0], t[1], ro.r.f, rng);
        if (rng.chance(0.25)) { // indoor patrol
          const other = rng.pick(rooms).r; if (other.f === ro.r.f) { const t2 = roomTiles(other); if (t2.length) e.ai.patrol = [t, rng.pick(t2)]; }
        }
        if (e.ai.style === 'hider' && rng.chance(0.5)) { e.ai.state = 'hide'; e.stance = 'crouch'; }
        M.units.push(e);
      }
      enemiesLeft--;
    }
    // civilians
    for (let i = 0; i < cfg.civilians; i++) { const ro = rng.pick(rooms); const t = pickTile(ro.r); if (!t) continue; M.units.push(makeCiv('civilian', t[0], t[1], ro.r.f, rng)); }
    // evidence & contraband
    const evRooms = rooms.filter(o => ['office', 'bedroom', 'lab', 'storage', 'vault', 'living', 'bunker', 'guard', 'hotel'].includes(o.r.tag));
    const conRooms = rooms.filter(o => ['lab', 'storage', 'vault', 'hall', 'barn', 'bunker'].includes(o.r.tag));
    const placeItem = (list, mk) => { const ro = (list.length ? rng.pick(list) : rng.pick(rooms)); const t = pickTile(ro.r); if (t) M.items.push(Object.assign({ id: U.uid('it'), x: ctr(t[0]), y: ctr(t[1]), f: ro.r.f, collected: false }, mk())); };
    for (let i = 0; i < cfg.evidence; i++) placeItem(evRooms, () => { const e = rng.pick(D.EVIDENCE_TYPES); return { kind: 'evidence', type: e.icon, name: e.name, intel: e.intel, evType: e.id }; });
    for (let i = 0; i < cfg.contraband; i++) placeItem(conRooms, () => { const c = rng.pick(D.CONTRABAND_TYPES); return { kind: 'contraband', type: c.icon, name: c.name, value: c.value }; });
  }

  // ---------------- Vision ----------------
  function visRange(u) {
    let r = 22;
    const w = u.weapons[u.wi];
    if (M.night && !(M.map.type === 'bunker' && u.floor === 1)) r = w && w.st.light ? 12 : 6;
    if (u.team === 'op') { if (w && !M.night) r += w.st.range * 0.5; if (u.perks.includes('eagle')) r += 3; if (u.op.role === 'marksman' && !u.path) r += 4; }
    if (u.team === 'enemy' && M.night) r = 8;
    if (u.gas > 0) r *= 0.5;
    return r * TS;
  }
  function fovHalf(u) { return u.team === 'op' ? (u.weapons[u.wi] && u.weapons[u.wi].st.range > 4 ? 0.85 : 1.05) : 0.95; }
  function canSee(a, b) {
    if (a.floor !== b.floor) return false;
    const dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy);
    let range = visRange(a);
    if (b.team === 'op' && M.night && b.weapons[b.wi] && b.weapons[b.wi].st.light) range += 6 * TS;
    if (b.ai && b.ai.state === 'hide') range = Math.min(range, (a.perks && a.perks.includes('eagle') ? 8 : 4.5) * TS);
    if (d > range) return false;
    if (d > 1.7 * TS && Math.abs(U.angDiff(a.angle, Math.atan2(dy, dx))) > fovHalf(a)) return false;
    return los(a.floor, a.x, a.y, b.x, b.y);
  }
  function visionPoly(u, rays = 72) {
    const f = u.floor, R = visRange(u), half = fovHalf(u), near = 1.7 * TS;
    const pts = [];
    const cast = (a, maxR) => {
      const dx = Math.cos(a), dy = Math.sin(a);
      let d = 0;
      for (; d < maxR; d += 5) {
        const tx = toTile(u.x + dx * d), ty = toTile(u.y + dy * d);
        if (blocksVision(f, tx, ty)) { d += 2; break; }
      }
      d = Math.min(d, maxR);
      // smoke
      for (const s of M.smokes) { if (s.f !== f || s.r < 8) continue; const ox = s.x - u.x, oy = s.y - u.y; const tp = ox * dx + oy * dy; if (tp < 0 || tp > d) continue; const pd = Math.abs(ox * dy - oy * dx); if (pd < s.r * 0.85) { const back = Math.sqrt(Math.max(0, (s.r * 0.85) ** 2 - pd * pd)); d = Math.max(0, Math.min(d, tp - back + s.r * 0.3)); } }
      pts.push(u.x + dx * d, u.y + dy * d);
    };
    for (let i = 0; i <= rays; i++) cast(u.angle - half + (2 * half) * i / rays, R);
    const back = 24;
    for (let i = 1; i < back; i++) cast(u.angle + half + (Math.PI * 2 - 2 * half) * i / back, near);
    return pts;
  }

  // ---------------- Orders (API used by UI) ----------------
  const ops = () => M.units.filter(u => u.team === 'op');
  function canAct(u) { return u.alive && !u.downed && !u.dead; }
  function clearOrders(u) { u.orders = []; u.path = null; u.act = null; u.follow = null; u.goWait = null; u.hold = false; }
  function give(u, order, append) {
    if (!canAct(u)) return;
    if (!append) { clearOrders(u); }
    u.orders.push(order);
  }
  function orderMove(units, tx, ty, f, append) {
    const list = units.filter(canAct);
    // spread destinations for groups
    const spots = list.length > 1 ? groupSpots(tx, ty, f, list.length) : [[tx, ty]];
    list.forEach((u, i) => give(u, { t: 'move', x: spots[i][0], y: spots[i][1], f, pace: u.pace }, append));
    if (list[0] && !append && M.phase === 'live') voice(list[0], 'moving');
  }
  function groupSpots(tx, ty, f, n) {
    const out = [[tx, ty]];
    for (let r = 1; r < 5 && out.length < n; r++) for (let dy = -r; dy <= r && out.length < n; dy++) for (let dx = -r; dx <= r && out.length < n; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const x = tx + dx, y = ty + dy; const t = tileAt(f, x, y);
      if ((t === T.FLOOR || t === T.GROUND || t === T.STAIRS) && los(f, ctr(tx), ctr(ty), ctr(x), ctr(y), true)) out.push([x, y]);
    }
    while (out.length < n) out.push([tx, ty]);
    return out;
  }
  function doorSides(d) {
    const horizPass = walkable(d.f, d.x - 1, d.y, 'op') || walkable(d.f, d.x + 1, d.y, 'op');
    const vertPass = walkable(d.f, d.x, d.y - 1, 'op') || walkable(d.f, d.x, d.y + 1, 'op');
    if (horizPass && (!vertPass || tileAt(d.f, d.x, d.y - 1) === T.WALL)) return { a: [d.x - 1, d.y], b: [d.x + 1, d.y], axis: 'x' };
    return { a: [d.x, d.y - 1], b: [d.x, d.y + 1], axis: 'y' };
  }
  function planPos(u) {
    for (let i = u.orders.length - 1; i >= 0; i--) { const o = u.orders[i]; if (o.t === 'move') return [o.f, o.x, o.y]; }
    return [u.floor, toTile(u.x), toTile(u.y)];
  }
  function nearSide(u, d, useQueue) {
    const s = doorSides(d);
    const [pf, px, py] = useQueue ? planPos(u) : [u.floor, toTile(u.x), toTile(u.y)];
    const pa = findPath(pf, px, py, d.f, s.a[0], s.a[1], 'op');
    const pb = findPath(pf, px, py, d.f, s.b[0], s.b[1], 'op');
    const la = pa ? pa.filter(n => !(n.x === d.x && n.y === d.y && n.f === d.f)).length + (pa.some(n => n.x === d.x && n.y === d.y && n.f === d.f) ? 100 : 0) : 1e9;
    const lb = pb ? pb.filter(n => !(n.x === d.x && n.y === d.y && n.f === d.f)).length + (pb.some(n => n.x === d.x && n.y === d.y && n.f === d.f) ? 100 : 0) : 1e9;
    return la <= lb ? { near: s.a, far: s.b, axis: s.axis } : { near: s.b, far: s.a, axis: s.axis };
  }
  function stackTiles(d, side, n) {
    const [nx, ny] = side.near; const out = [];
    const dirX = nx - d.x, dirY = ny - d.y; // pointing away from door
    const perp = side.axis === 'x' ? [[0, -1], [0, 1]] : [[-1, 0], [1, 0]];
    const cands = [[nx + perp[0][0], ny + perp[0][1]], [nx + perp[1][0], ny + perp[1][1]], [nx + dirX + perp[0][0], ny + dirY + perp[0][1]], [nx + dirX + perp[1][0], ny + dirY + perp[1][1]], [nx + dirX, ny + dirY], [nx + dirX * 2, ny + dirY * 2], [nx + dirX * 2 + perp[0][0], ny + dirY * 2 + perp[0][1]]];
    for (const c of cands) if (walkable(d.f, c[0], c[1], 'enemy') && tileAt(d.f, c[0], c[1]) !== T.DOOR) out.push(c);
    while (out.length < n) out.push([nx + dirX * (out.length), ny + dirY * (out.length)]);
    return out;
  }
  function roomPoints(d, side, n) {
    // points inside the room beyond the door, spread to corners
    const [fx, fy] = side.far; const f = d.f;
    const rid = M.map.floors[f].room[fy * M.map.w + fx];
    const tiles = [];
    if (rid >= 0) { const r = M.map.rooms[rid]; for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) if (walkable(f, x, y, 'enemy') && tileAt(f, x, y) !== T.DOOR) tiles.push([x, y]); }
    else { for (let y = fy - 4; y <= fy + 4; y++) for (let x = fx - 4; x <= fx + 4; x++) if (walkable(f, x, y, 'enemy') && tileAt(f, x, y) !== T.DOOR && los(f, ctr(fx), ctr(fy), ctr(x), ctr(y), true)) tiles.push([x, y]); }
    if (!tiles.length) return Array(n).fill(side.far);
    const out = [];
    const dirX = fx - d.x, dirY = fy - d.y;
    // first point: 2 tiles straight in, then corners alternating
    const scored = tiles.map(t => { const lx = (t[0] - fx) * dirX + (t[1] - fy) * dirY; const ly = (t[0] - fx) * (side.axis === 'x' ? 0 : 1) + (t[1] - fy) * (side.axis === 'x' ? 1 : 0); return { t, lx, ly }; });
    const pickBest = fn => { let best = null, bs = -1e9; for (const s of scored) { if (out.some(o => o[0] === s.t[0] && o[1] === s.t[1])) continue; const v = fn(s); if (v > bs) { bs = v; best = s.t; } } if (best) out.push(best); };
    const fns = [s => -Math.abs(s.ly) * 2 - Math.abs(s.lx - 2), s => -s.ly + s.lx * 0.7 - Math.abs(s.lx - 3), s => s.ly + s.lx * 0.7 - Math.abs(s.lx - 3), s => s.lx - Math.abs(s.ly) * 0.5, s => -s.ly * 2 + s.lx, s => s.ly * 2 + s.lx, s => s.lx, s => -Math.abs(s.ly)];
    for (let i = 0; i < n; i++) pickBest(fns[i % fns.length]);
    while (out.length < n) out.push(side.far);
    return out;
  }
  function hasUtil(u, id) { return u.util.includes(id); }
  function orderDoor(units, d, action, append) {
    const list = units.filter(canAct); if (!list.length) return;
    list.sort((a, b) => Math.hypot(a.x - ctr(d.x), a.y - ctr(d.y)) - Math.hypot(b.x - ctr(d.x), b.y - ctr(d.y)));
    if (action === 'flashclear' || action === 'charge') {
      const need = action === 'flashclear' ? 'flash' : 'charge';
      const idx = list.findIndex(u => hasUtil(u, need));
      if (idx < 0) { log(`No ${need === 'flash' ? 'flashbangs' : 'breaching charges'} carried by selected operators.`, '#d9534f'); A().error(); return; }
      if (idx > 0) list.unshift(list.splice(idx, 1)[0]);
    }
    if (action === 'pick' || action === 'cam' || action === 'ram' || action === 'shotgun') {
      const need = { pick: u => u.tool === 'lockpick', cam: u => u.tool === 'doorcam', ram: u => u.tool === 'ram', shotgun: u => u.weapons.some(w => w.st.cat === 'sg') }[action];
      const idx = list.findIndex(need);
      if (idx < 0) { log('Selected operators lack the required equipment.', '#d9534f'); A().error(); return; }
      if (idx > 0) list.unshift(list.splice(idx, 1)[0]);
    }
    const leader = list[0];
    const side = nearSide(leader, d, append);
    const stack = stackTiles(d, side, list.length);
    const clear = ['openclear', 'flashclear', 'kickclear', 'charge', 'shotgun', 'ram'].includes(action);
    const pts = clear ? roomPoints(d, side, list.length) : null;
    const prim = { openclear: 'entry', flashclear: 'entry', kickclear: 'kick', charge: 'charge', shotgun: 'shotgun', ram: 'ram', pick: 'pick', cam: 'cam', open: 'open', close: 'close', kick: 'kick', stack: null }[action];
    list.forEach((u, i) => {
      if (!append) clearOrders(u); else { u.hold = false; u.follow = null; }
      if (i === 0) {
        u.orders.push({ t: 'move', x: side.near[0], y: side.near[1], f: d.f, pace: 'walk' });
        if (prim) u.orders.push({ t: 'door', door: d.id, a: prim });
        if (action === 'flashclear') {
          const fx_ = ctr(side.far[0]) + (side.far[0] - d.x) * TS * 1.6, fy_ = ctr(side.far[1]) + (side.far[1] - d.y) * TS * 1.6;
          u.orders.push({ t: 'throw', g: 'flash', x: fx_, y: fy_, f: d.f });
          u.orders.push({ t: 'wait', dur: 1.25 });
        }
        if (action === 'charge') u.orders.push({ t: 'wait', dur: 0.4 });
        if (clear) u.orders.push({ t: 'move', x: pts[0][0], y: pts[0][1], f: d.f, pace: 'walk', clearing: true });
      } else {
        u.orders.push({ t: 'move', x: stack[i - 1][0], y: stack[i - 1][1], f: d.f, pace: 'walk' });
        if (clear) {
          u.orders.push({ t: 'waitdoor', door: d.id, leader: leader.id, delay: (action === 'flashclear' ? 1.4 : 0.2) + (i - 1) * 0.35 });
          u.orders.push({ t: 'move', x: pts[i][0], y: pts[i][1], f: d.f, pace: 'walk', clearing: true });
        }
      }
    });
    if (M.phase === 'live') voice(leader, action === 'charge' || action === 'kickclear' ? 'moving' : 'moving');
  }
  function orderThrow(u, g, wx, wy, f, append) {
    if (!canAct(u)) return;
    if (!hasUtil(u, g)) { log(`${u.name} has no ${D.ITEM[g].name}.`, '#d9534f'); A().error(); return; }
    give(u, { t: 'throw', g, x: wx, y: wy, f }, append);
  }
  function orderInteract(units, target, kind, append) {
    const list = units.filter(canAct); if (!list.length) return;
    let best = list[0]; let bd = 1e9;
    for (const u of list) { const d = Math.hypot(u.x - target.x, u.y - target.y) + (u.floor !== (target.floor ?? target.f) ? 2000 : 0); if (kind === 'heal' && u.tool === 'medkit') { best = u; break; } if (d < bd) { bd = d; best = u; } }
    give(best, { t: 'interact', target: target.id, kind }, append);
  }
  function orderHold(units) { for (const u of units) if (canAct(u)) { clearOrders(u); u.hold = true; } }
  function orderFollow(units, leader) { for (const u of units) if (canAct(u) && u !== leader) { clearOrders(u); u.follow = leader.id; } }
  function orderFace(units, wx, wy) { for (const u of units) if (canAct(u)) { u.watchAngle = Math.atan2(wy - u.y, wx - u.x); if (!u.orders.length) u.angle = u.watchAngle; } }
  function orderGo(units, append = true) { for (const u of units) give(u, { t: 'go' }, append); }
  function go() { let n = 0; for (const u of ops()) { if (u.orders[0] && u.orders[0].t === 'go') { u.orders.shift(); n++; } } if (n) { log('GO GO GO!', '#e2a33a'); BP.audio.say('Go, go, go!', { prio: true }); } return n; }
  function orderCancel(units) { for (const u of units) if (canAct(u)) clearOrders(u); }
  function setPace(units, p) { for (const u of units) { u.pace = p; for (const o of u.orders) if (o.t === 'move') o.pace = p; } }
  function toggleCrouch(units) { const c = units.some(u => u.stance !== 'crouch'); for (const u of units) u.stance = c ? 'crouch' : 'stand'; }
  function orderCover(units) {
    for (const u of units) {
      if (!canAct(u)) continue;
      const tx0 = toTile(u.x), ty0 = toTile(u.y); let best = null, bs = -1e9;
      const threats = M.units.filter(e => e.team === 'enemy' && e.lastSeen && e.alive && !e.restrained && e.floor === u.floor);
      for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) {
        const x = tx0 + dx, y = ty0 + dy;
        if (!walkable(u.floor, x, y, 'enemy') || tileAt(u.floor, x, y) === T.DOOR) continue;
        let cover = 0;
        for (const [ax, ay] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const t = tileAt(u.floor, x + ax, y + ay); if (t === T.FURN) cover += 2; if (t === T.WALL || t === T.HIGH) cover += 1; }
        let exposed = 0;
        for (const e of threats) if (los(u.floor, ctr(x), ctr(y), e.lastSeen.x, e.lastSeen.y, true)) exposed++;
        const s = cover * 2 - exposed * 3 - Math.hypot(dx, dy) * 0.6;
        if (s > bs) { bs = s; best = [x, y]; }
      }
      clearOrders(u);
      if (best) u.orders.push({ t: 'move', x: best[0], y: best[1], f: u.floor, pace: 'run' });
      u.stance = 'crouch';
    }
  }

  // ---------------- Combat ----------------
  function curW(u) { return u.weapons[u.wi]; }
  function isThreat(e) { return e.team === 'enemy' && e.alive && !e.dead && !e.incap && !e.restrained && e.armed && e.ai.state !== 'surrender' && !e.ai.dummy || (e.ai && e.ai.dummy && e.alive && !e.dead); }
  function opSpread(u, w) {
    let s = w.st.spread * (1.55 - u.stats.acc / 100) - u.pers.acc * 0.03;
    if (u.path) s *= u.pace === 'run' ? 2.2 : u.pace === 'slow' ? 1.05 : 1.45;
    if (u.stance === 'crouch') s *= 0.8;
    s *= 1 + u.stress / 140;
    if (u.hp < u.maxHp * 0.5) s *= 1.3;
    if (u.stun > 0) s *= 3;
    if (u.gas > 0) s *= 1.15;
    s += u.recoilHeat * (1 - w.st.ergonomics / 220) * (u.perks.includes('steady') ? 0.75 : 1);
    return Math.max(0.15, s);
  }
  function enemySpread(e, w) {
    let s = w.st.spread * (1 + (1 - e.acc) * 2.2) / M.diff.enemyAcc;
    const tg = e.target; if (tg) { if (tg.path) s *= tg.pace === 'run' ? 1.45 : 1.2; if (tg.stance === 'crouch') s *= 1.15; const d = Math.hypot(tg.x - e.x, tg.y - e.y) / TS; if (d > 12) s *= 1 + (d - 12) * 0.04; }
    if (e.path) s *= 1.6;
    if (e.gas > 0) s *= 3; if (e.stun > 0) s *= 4;
    if (e.hp < 50) s *= 1.3;
    s += e.recoilHeat * 1.2;
    return s;
  }
  function fire(u, tx, ty) {
    const w = curW(u); if (!w) return false;
    if (u.reloadT > 0 || u.jamT > 0 || u.fireCd > 0) return false;
    if (w.mag <= 0) { startReload(u); return false; }
    // malfunction
    const jamP = (1 - w.st.reliability / 100) * 0.015;
    if (R() < jamP) { u.jamT = 1.6; A().dryfire(u.x, u.y); if (u.team === 'op') voice(u, 'jam'); return false; }
    w.mag--; w.fired++; u.shots++;
    const base = Math.atan2(ty - u.y, tx - u.x);
    const spread = (u.team === 'op' ? opSpread(u, w) : enemySpread(u, w)) * Math.PI / 180;
    const ammo = u.ammo || D.AMMO[0];
    const dmg = w.st.dmg * ammo.dmg, pen = w.st.pen * ammo.pen;
    for (let p = 0; p < w.st.pellets; p++) {
      const a = base + R.gauss() * spread * 0.5 + (w.st.pellets > 1 ? R.gauss() * 0.045 : 0);
      traceShot(u, a, dmg, pen);
    }
    u.recoilHeat = Math.min(8, u.recoilHeat + w.st.recoil / 100 * (w.st.mode === 'auto' ? 1.2 : 0.8));
    const rpmCd = 60 / w.st.rpm;
    u.fireCd = rpmCd;
    const ammoQuiet = ammo.id === 'sub' && w.st.suppressed ? 0.6 : 1;
    const loud = w.st.suppressed ? 6 * ammoQuiet : (w.st.cal === '7.62' || w.st.cal === '.338' || w.st.cal === '12g') ? 32 : 26;
    noise(u.x, u.y, u.floor, loud, 'gunshot', u);
    if (u.team === 'enemy') for (const o of M.units) {
      if (o.team !== 'op' || !canAct(o) || o.floor !== u.floor || o.target) continue;
      const d = Math.hypot(o.x - u.x, o.y - u.y);
      if (d < 18 * TS && los(u.floor, o.x, o.y, u.x, u.y, true)) { o.alertAng = Math.atan2(u.y - o.y, u.x - o.x); o.alertT = M.t; }
    }
    fx({ type: 'muzzle', x: u.x + Math.cos(base) * 20, y: u.y + Math.sin(base) * 20, f: u.floor, ang: base, sup: w.st.suppressed, src: u.team });
    if (w.st.mode !== 'pump' && w.st.mode !== 'bolt') fx({ type: 'shell', x: u.x, y: u.y, f: u.floor, ang: base + Math.PI / 2 });
    if (!M.replayMode) { A().shot(w.st.cal, w.st.suppressed, u.x, u.y, isIndoor(u)); if (w.st.mode === 'pump' || w.st.mode === 'bolt') A().pump(u.x, u.y); else if (R() < 0.5) A().shell(u.x, u.y, w.st.cal === '12g'); }
    if (u.team === 'op') M.stats.shotsOp += 1;
    return true;
  }
  function isIndoor(u) { const t = tileAt(u.floor, toTile(u.x), toTile(u.y)); return t !== T.GROUND; }
  function startReload(u) {
    const w = curW(u);
    if (u.reloadT > 0) return;
    if (w.reserve <= 0) {
      // switch weapon
      const alt = u.weapons.findIndex((x, i) => i !== u.wi && (x.mag > 0 || x.reserve > 0) && !(u.shield && x.st.cat !== 'hg'));
      if (alt >= 0) { u.wi = alt; u.reloadT = 0.6; u.reloadKind = 'switch'; }
      return;
    }
    const need = w.st.mag - w.mag; if (need <= 0) return;
    u.reloadT = w.st.cat === 'sg' ? Math.min(need, w.reserve) * 0.45 + 0.3 : w.st.reload * (w.mag === 0 ? 1.15 : 1);
    u.reloadKind = 'reload';
    if (!M.replayMode) A().reload(u.x, u.y);
    if (u.team === 'op' && (u.target || R() < 0.4)) voice(u, 'reload');
  }
  function finishReload(u) {
    const w = curW(u);
    if (u.reloadKind === 'reload') { const need = w.st.mag - w.mag; const take = Math.min(need, w.reserve); w.mag += take; w.reserve -= take; }
    u.reloadKind = null;
  }
  function traceShot(sh, ang, dmg, pen) {
    const f = sh.floor, dx = Math.cos(ang), dy = Math.sin(ang);
    const sx = sh.x + dx * 8, sy = sh.y + dy * 8, range = 45 * TS;
    let wallT = range, kind = 'none';
    for (let t = 0; t < range; t += 4) {
      const x = sx + dx * t, y = sy + dy * t; const tx = toTile(x), ty = toTile(y);
      const tile = tileAt(f, tx, ty);
      if (tile === T.WALL || tile === T.HIGH) { wallT = t; kind = tile === T.HIGH ? 'metal' : 'wall'; break; }
      if (tile === T.DOOR) { const d = doorAt(f, tx, ty); if (d.state === 'closed') { wallT = t; kind = 'door'; break; } }
      if (tile === T.WINDOW) { M.map.floors[f].t[ty * M.map.w + tx] = T.BWIN; fx({ type: 'glass', x, y, f }); if (!M.replayMode) A().glass(x, y); M.events.push({ t: M.t, type: 'tile', f, x: tx, y: ty, v: T.BWIN }); }
      if (tile === T.FURN && t > 26 && R() < 0.035) { wallT = t; kind = 'furn'; break; }
    }
    let best = null, bestT = wallT;
    for (const u of M.units) {
      if (u === sh || u.floor !== f || !u.alive || u.incap || u.downed) continue;
      const ox = u.x - sx, oy = u.y - sy; const tp = ox * dx + oy * dy;
      if (tp < 0 || tp > bestT + 10) continue;
      const pd = Math.abs(ox * dy - oy * dx);
      const r = (u.stance === 'crouch' || u.kneel) ? 7 : 9;
      if (pd < r && tp < bestT) { best = u; bestT = tp; }
      else if (pd < 42 && u.team !== sh.team) nearMiss(u, sh);
    }
    const ex = sx + dx * bestT, ey = sy + dy * bestT;
    fx({ type: 'tracer', x0: sx, y0: sy, x1: ex, y1: ey, f, team: sh.team });
    M.events.push({ t: M.t, type: 'shot', f, x0: sx | 0, y0: sy | 0, x1: ex | 0, y1: ey | 0, team: sh.team });
    if (best) {
      if (best.shield && Math.abs(U.angDiff(best.angle, ang + Math.PI)) < 0.95 && R() < 0.9) {
        fx({ type: 'impact', x: ex, y: ey, f, kind: 'metal' }); if (!M.replayMode) A().ricochet(ex, ey); best.stress += 3; return;
      }
      applyDamage(best, dmg, pen, sh, ang);
    } else if (kind !== 'none') {
      fx({ type: 'impact', x: ex, y: ey, f, kind, ang });
      if (!M.replayMode && R() < 0.5) A().impact(ex, ey, kind === 'metal');
    }
  }
  function nearMiss(u, sh) {
    if (u.team === 'op' && sh && !u.target) { u.alertAng = Math.atan2(sh.y - u.y, sh.x - u.x); u.alertT = M.t; }
    if (u.team === 'op') u.stress = Math.min(100, u.stress + 4 * u.pers.stress * (u.perks.includes('iron') ? 0.5 : 1));
    if (u.team === 'enemy') { u.ai.morale -= 1.5; if (u.ai.state !== 'combat' && u.ai.state !== 'surrender' && !u.ai.dummy) enterCombat(u, sh, true); }
    if (u.team === 'civ' && u.ai.state === 'idle') civPanic(u);
  }
  function applyDamage(u, dmg, pen, sh, ang) {
    if (u.ai && u.ai.dummy) { u.hp = 0; u.alive = false; u.dead = true; fx({ type: 'impact', x: u.x, y: u.y, f: u.floor, kind: 'wood' }); if (sh && sh.team === 'op') { sh.hits++; M.stats.hitsOp++; if (u.team === 'civ') { M.stats.civKilledByOp++; log(`${sh.name} hit a no-shoot target!`, '#d9534f'); } else sh.kills++; } return; }
    const roll = R();
    let loc = roll < 0.12 ? 'head' : roll < 0.7 ? 'torso' : 'limb';
    let blocked = false;
    if (loc === 'torso' && u.armor > 0) {
      const stop = U.clamp(0.2 + (u.armor - pen) / 70, 0.03, 0.95);
      if (R() < stop) { dmg *= 0.12; blocked = true; } else dmg *= 1 - u.armor / 300;
    } else if (loc === 'head') {
      if (u.helmet > 0 && R() < U.clamp(0.1 + (u.helmet - pen) / 80, 0.02, 0.9)) { dmg *= 0.25; blocked = true; } else dmg *= 2.4;
    } else if (loc === 'limb') dmg *= 0.55;
    if (u.team === 'op') dmg *= M.diff.dmgTaken;
    u.hp -= dmg;
    if (sh && sh.team === 'op' && u.team !== 'op') { sh.hits++; M.stats.hitsOp++; }
    fx({ type: blocked ? 'impact' : 'blood', x: u.x, y: u.y, f: u.floor, ang, kind: blocked ? 'metal' : 'flesh' });
    if (!M.replayMode) { if (blocked) A().ricochet(u.x, u.y); else { A().flesh(u.x, u.y); if (u.hp > 0 && R() < 0.6) A().grunt(u.x, u.y, u.female); } }
    if (u.team === 'op') {
      if (sh && !u.target) { u.alertAng = Math.atan2(sh.y - u.y, sh.x - u.x); u.alertT = M.t; }
      u.damageTaken += dmg; u.stress = Math.min(100, u.stress + 18 * u.pers.stress * (u.perks.includes('iron') ? 0.5 : 1));
      if (u.hp <= 0) downOp(u);
      else if (!blocked && R() < 0.6) voice(u, 'hurt');
      return;
    }
    if (u.team === 'enemy') {
      u.ai.morale -= blocked ? 6 : 20;
      if (u.ai.state !== 'combat' && u.ai.state !== 'surrender' && u.hp > 0 && sh) enterCombat(u, sh, true);
      if (u.hp <= 0) {
        if (u.ai.state === 'surrender' && sh && sh.team === 'op') { M.stats.roe++; log(`ROE VIOLATION: ${sh.name} shot a surrendering suspect.`, '#d9534f'); }
        if (u.hp > -18 && loc !== 'head' && R() < 0.55) { u.incap = true; u.alive = true; log(`${u.name} incapacitated.`, '#e2a33a'); }
        else { u.dead = true; u.alive = false; }
        u.target = null; u.path = null;
        if (u.armed) { u.armed = false; M.items.push({ id: U.uid('drop'), kind: 'drop', type: 'weapon', x: u.x + R.range(-10, 10), y: u.y + R.range(-10, 10), f: u.floor }); }
        if (sh && sh.team === 'op') { sh.kills++; if (R() < 0.7) voice(sh, 'down'); }
        if (!M.replayMode) A().fall(u.x, u.y);
        fx({ type: 'pool', x: u.x, y: u.y, f: u.floor });
        // nearby enemies lose morale
        for (const o of M.units) if (o.team === 'enemy' && o.alive && o !== u && o.floor === u.floor && Math.hypot(o.x - u.x, o.y - u.y) < 8 * TS) o.ai.morale -= 12;
        if (u.hvt) log(`HVT ${u.name} ${u.dead ? 'KILLED' : 'down'}.`, '#d9534f');
      }
      return;
    }
    if (u.team === 'civ') {
      if (u.hp <= 0) {
        u.dead = true; u.alive = false; fx({ type: 'pool', x: u.x, y: u.y, f: u.floor }); if (!M.replayMode) A().fall(u.x, u.y);
        if (sh && sh.team === 'op') { M.stats.civKilledByOp++; log(`${u.name} killed by friendly fire!`, '#d9534f'); }
        else log(`${u.name} killed!`, '#d9534f');
        if (u.kind === 'hostage') BP.audio.say('Hostage down!', { prio: true });
      } else civPanic(u);
    }
  }
  function downOp(u) {
    u.downed = true; u.hp = 0; u.path = null; u.orders = []; u.act = null; u.target = null; u.follow = null;
    u.bleed = 60 * (u.perks.includes('tough') ? 2 : 1);
    log(`${u.name} is DOWN!`, '#d9534f'); voice(u, 'mandown', true);
    fx({ type: 'pool', x: u.x, y: u.y, f: u.floor });
    if (!M.replayMode) A().fall(u.x, u.y);
    for (const o of ops()) if (o !== u && canAct(o)) o.stress = Math.min(100, o.stress + 22 * o.pers.stress);
  }

  // ---------------- Grenades ----------------
  function throwGrenade(u, g, tx, ty) {
    const i = u.util.indexOf(g); if (i < 0) return false;
    u.util.splice(i, 1);
    let maxR = (u.perks.includes('grenadier') ? 13 : 10) * TS;
    let dx = tx - u.x, dy = ty - u.y, d = Math.hypot(dx, dy);
    if (d > maxR) { tx = u.x + dx / d * maxR; ty = u.y + dy / d * maxR; d = maxR; }
    // clip to walls
    const n = Math.ceil(d / 6); let lx = u.x, ly = u.y;
    for (let k = 1; k <= n; k++) { const x = u.x + (tx - u.x) * k / n, y = u.y + (ty - u.y) * k / n; if (blocksVision(u.floor, toTile(x), toTile(y))) { tx = lx; ty = ly; break; } lx = x; ly = y; }
    M.grenades.push({ g, x: u.x, y: u.y, f: u.floor, sx: u.x, sy: u.y, tx, ty, t: 0, travel: Math.max(0.25, Math.hypot(tx - u.x, ty - u.y) / 420), fuse: g === 'flash' ? 1.5 : g === 'stinger' ? 1.4 : 1.2, owner: u.id, bounced: false });
    if (!M.replayMode) A().pin(u.x, u.y);
    if (g === 'flash') { voice(u, 'flash'); M.stats.flashes++; }
    if (g === 'smoke') M.stats.smokes++;
    return true;
  }
  function updateGrenades(dt) {
    for (const gr of M.grenades) {
      gr.t += dt;
      const k = Math.min(1, gr.t / gr.travel);
      gr.x = gr.sx + (gr.tx - gr.sx) * k; gr.y = gr.sy + (gr.ty - gr.sy) * k;
      gr.z = Math.sin(k * Math.PI) * 18;
      if (k >= 1 && !gr.bounced) { gr.bounced = true; if (!M.replayMode) A().bounce(gr.x, gr.y); }
      if (gr.t >= gr.fuse) { detonate(gr); gr.done = true; }
    }
    M.grenades = M.grenades.filter(g => !g.done);
    for (const s of M.smokes) { s.t -= dt; s.r = s.t > 2 ? Math.min(s.maxR, s.r + dt * s.maxR / 1.8) : s.maxR * Math.max(0, s.t / 2); }
    M.smokes = M.smokes.filter(s => s.t > 0);
    for (const g of M.gases) g.t -= dt;
    M.gases = M.gases.filter(g => g.t > 0);
  }
  function detonate(gr) {
    const owner = M.units.find(u => u.id === gr.owner);
    M.events.push({ t: M.t, type: 'boom', g: gr.g, x: gr.x | 0, y: gr.y | 0, f: gr.f });
    if (gr.g === 'flash' || gr.g === 'stinger') {
      if (!M.replayMode) A().flashbang(gr.x, gr.y);
      fx({ type: 'flash', x: gr.x, y: gr.y, f: gr.f, big: gr.g === 'flash' });
      noise(gr.x, gr.y, gr.f, gr.g === 'flash' ? 30 : 34, gr.g === 'stinger' ? 'diversion' : 'explosion');
      const R0 = (gr.g === 'flash' ? (owner && owner.perks.includes('grenadier') ? 7.5 : 6) : 2.2) * TS;
      for (const u of M.units) {
        if (!u.alive || u.dead || u.floor !== gr.f) continue;
        const d = Math.hypot(u.x - gr.x, u.y - gr.y); if (d > R0) continue;
        if (!los(gr.f, gr.x, gr.y, u.x, u.y, true)) continue;
        const facing = Math.abs(U.angDiff(u.angle, Math.atan2(gr.y - u.y, gr.x - u.x))) < 1.2 ? 1 : 0.55;
        let s = (1.5 + 5 * (1 - d / R0)) * facing;
        if (u.team === 'op') s *= 0.35;
        u.stun = Math.max(u.stun, s);
        if (u.team === 'enemy') { u.ai.morale -= 18; if (u.ai.state !== 'surrender') { u.ai.state = u.ai.state === 'idle' ? 'alert' : u.ai.state; } u.aimT = 0; }
        if (u.team === 'civ' && !u.hostageBound) { u.ai.state = 'cower'; u.stance = 'crouch'; u.path = null; }
      }
    } else if (gr.g === 'smoke') {
      if (!M.replayMode) A().smoke(gr.x, gr.y);
      M.smokes.push({ x: gr.x, y: gr.y, f: gr.f, r: 4, maxR: 3.3 * TS, t: 26 });
      M.events.push({ t: M.t, type: 'smoke', x: gr.x | 0, y: gr.y | 0, f: gr.f });
    } else if (gr.g === 'gas') {
      if (!M.replayMode) A().smoke(gr.x, gr.y);
      M.gases.push({ x: gr.x, y: gr.y, f: gr.f, r: 3.6 * TS, t: 22 });
    }
  }
  function updateGas(dt) {
    for (const u of M.units) {
      if (!u.alive || u.dead) continue;
      let inGas = false;
      for (const g of M.gases) if (g.f === u.floor && Math.hypot(u.x - g.x, u.y - g.y) < g.r * Math.min(1, (22 - g.t) / 2 + 0.3)) inGas = true;
      if (inGas) {
        if (u.gas <= 0 && !M.replayMode && u.team !== 'op') A().cough(u.x, u.y);
        u.gas = u.team === 'op' ? 0.5 : 4;
        if (u.team === 'enemy') u.ai.morale -= 8 * dt;
      } else u.gas = Math.max(0, u.gas - dt);
    }
  }

  // ---------------- Doors ----------------
  function setDoor(d, state) {
    d.state = state; M.events.push({ t: M.t, type: 'door', id: d.id, state });
  }
  function doorWorld(d) { return [ctr(d.x), ctr(d.y)]; }
  function doorBlast(d, power) {
    const [dx, dy] = doorWorld(d);
    for (const u of M.units) {
      if (!u.alive || u.dead || u.floor !== d.f) continue;
      const dd = Math.hypot(u.x - dx, u.y - dy);
      if (dd < 3 * TS * power) {
        u.stun = Math.max(u.stun, (u.team === 'op' ? 0.6 : 3.5) * power * (1 - dd / (3 * TS * power) + 0.3));
        if (dd < 1.3 * TS && power >= 1) { u.hp -= 30; if (u.hp <= 0) { if (u.team === 'op') downOp(u); else { u.dead = true; u.alive = false; } } }
        if (u.team === 'enemy') u.ai.morale -= 15;
      }
    }
  }
  function doDoorAction(u, d, a, dt) {
    // returns 'done' | 'fail' | 'busy'
    const act = u.act;
    if (!act) {
      const brk = u.perks.includes('breacher') ? 0.6 : 1;
      let dur = 0.4;
      if (a === 'entry') {
        if (d.state !== 'closed') return 'done';
        if (d.barricaded) { log(`${u.name}: Door is barricaded — need a breaching charge.`, '#d9534f'); voice(u, 'locked'); d.knownLocked = true; return 'fail'; }
        if (d.locked && !d.knownLocked) { d.knownLocked = true; voice(u, 'locked'); u.act = { type: 'tryopen', t: 0, dur: 0.5 }; return 'busy'; }
        a = d.locked ? (u.tool === 'ram' ? 'ram' : u.weapons.some(w => w.st.cat === 'sg') ? 'shotgun' : 'kick') : 'open';
      }
      if (a === 'open') { if (d.state !== 'closed') return 'done'; if (d.barricaded) { d.knownLocked = true; log(`${u.name}: barricaded!`, '#d9534f'); return 'fail'; } if (d.locked) { d.knownLocked = true; voice(u, 'locked'); log(`${u.name}: "Locked."`, '#e2a33a'); return 'fail'; } dur = 0.4; }
      else if (a === 'close') { if (d.state !== 'open') return 'done'; dur = 0.4; }
      else if (a === 'kick') { if (d.state === 'broken') return 'done'; if (d.barricaded) { log('Barricaded — kick won\'t work.', '#d9534f'); return 'fail'; } dur = 1.0 * brk; }
      else if (a === 'ram') { if (d.state === 'broken') return 'done'; if (d.barricaded) { log('Barricaded — need a charge.', '#d9534f'); return 'fail'; } dur = 1.3 * brk; }
      else if (a === 'shotgun') { if (d.state === 'broken') return 'done'; if (d.barricaded) return 'fail'; dur = 0.9 * brk; const sg = u.weapons.findIndex(w => w.st.cat === 'sg'); if (sg >= 0) u.wi = sg; }
      else if (a === 'charge') { if (d.state === 'broken') return 'done'; if (!hasUtil(u, 'charge')) { log(`${u.name}: no breaching charge.`, '#d9534f'); return 'fail'; } dur = (2.8 - u.stats.tec / 100) * brk; voice(u, 'breach'); }
      else if (a === 'pick') { if (!d.locked || d.state !== 'closed') return 'done'; if (u.tool !== 'lockpick') return 'fail'; dur = Math.max(2, 7 - u.stats.tec / 18) * (u.perks.includes('locksmith') ? 0.5 : 1); }
      else if (a === 'cam') { if (d.state !== 'closed') return 'done'; if (u.tool !== 'doorcam') return 'fail'; dur = 1.4; }
      u.act = { type: a, t: 0, dur, door: d.id, tries: 0 };
      if (a === 'kick' || a === 'ram') voice(u, 'breach');
      return 'busy';
    }
    act.t += dt;
    if (act.type === 'pick' && Math.floor(act.t * 3) !== Math.floor((act.t - dt) * 3) && !M.replayMode) A().pick(u.x, u.y);
    if (act.t < act.dur) return 'busy';
    u.act = null;
    switch (act.type) {
      case 'tryopen': return 'busy';
      case 'open': setDoor(d, 'open'); if (!M.replayMode) A().doorOpen(ctr(d.x), ctr(d.y)); noise(ctr(d.x), ctr(d.y), d.f, 3, 'door', u); return 'done';
      case 'close': setDoor(d, 'closed'); if (!M.replayMode) A().doorClose(ctr(d.x), ctr(d.y)); return 'done';
      case 'kick': case 'ram': {
        const p = !d.locked ? 1 : act.type === 'ram' ? 0.9 : (u.perks.includes('breacher') ? 0.85 : 0.45);
        const ok = R() < p;
        if (!M.replayMode) A().kick(ctr(d.x), ctr(d.y), ok);
        noise(ctr(d.x), ctr(d.y), d.f, 16, 'breach');
        if (ok) { setDoor(d, 'broken'); d.locked = false; doorBlast(d, 0.35); fx({ type: 'splinters', x: ctr(d.x), y: ctr(d.y), f: d.f }); return 'done'; }
        u.act = { type: act.type, t: 0, dur: act.type === 'ram' ? 1.1 : 0.9, door: d.id };
        return 'busy';
      }
      case 'shotgun': {
        const w = curW(u);
        if (w && w.mag > 0) { w.mag--; A().shot('12g', false, ctr(d.x), ctr(d.y)); }
        noise(ctr(d.x), ctr(d.y), d.f, 26, 'gunshot');
        setDoor(d, 'broken'); d.locked = false; doorBlast(d, 0.4); fx({ type: 'splinters', x: ctr(d.x), y: ctr(d.y), f: d.f }); fx({ type: 'muzzle', x: ctr(d.x), y: ctr(d.y), f: d.f, ang: u.angle });
        return 'done';
      }
      case 'charge': {
        u.util.splice(u.util.indexOf('charge'), 1); M.stats.charges++;
        if (!M.replayMode) A().explosion(ctr(d.x), ctr(d.y), 0.8);
        noise(ctr(d.x), ctr(d.y), d.f, 36, 'explosion');
        setDoor(d, 'broken'); d.locked = false; d.barricaded = false;
        fx({ type: 'explosion', x: ctr(d.x), y: ctr(d.y), f: d.f, r: 50 }); fx({ type: 'splinters', x: ctr(d.x), y: ctr(d.y), f: d.f });
        M.events.push({ t: M.t, type: 'boom', g: 'charge', x: ctr(d.x), y: ctr(d.y), f: d.f });
        doorBlast(d, 1);
        return 'done';
      }
      case 'pick': d.locked = false; d.knownLocked = false; log(`${u.name}: lock picked.`, '#5cb85c'); setDoor(d, 'open'); if (!M.replayMode) A().doorOpen(ctr(d.x), ctr(d.y)); return 'done';
      case 'cam': {
        const side = nearSide(u, d); const [fx_, fy_] = side.far;
        M.camSources.push({ x: ctr(fx_), y: ctr(fy_), f: d.f, angle: Math.atan2(fy_ - d.y, fx_ - d.x), t: 3.5, cam: true });
        log(`${u.name}: inspecting under the door…`, '#4fc3f7');
        return 'done';
      }
    }
    return 'done';
  }

  // ---------------- Movement ----------------
  function moveSpeed(u) {
    let s = u.team === 'op' ? (u.pace === 'run' ? 150 : u.pace === 'slow' ? 55 : 88) * u.speedMult : u.team === 'enemy' ? (u.ai.state === 'flee' || (u.ai.state === 'combat' && u.ai.style === 'rusher') ? 125 : 70) : (u.ai.state === 'flee' ? 120 : 60);
    if (u.stance === 'crouch') s *= 0.55;
    if (u.shield) s *= 0.8;
    if (u.hp < u.maxHp * 0.5) s *= 0.8;
    if (u.stun > 0) s *= 0.3;
    if (u.gas > 0 && u.team !== 'op') s *= 0.6;
    if (u.team === 'op' && u.target && u.pace !== 'run') s *= 0.75;
    return s;
  }
  function setPath(u, f, x, y) {
    const p = findPath(u.floor, toTile(u.x), toTile(u.y), f, x, y, u.team === 'op' ? 'op' : 'enemy');
    u.path = p; u.pi = 0;
    return !!p;
  }
  // returns 'done' | 'moving' | 'blocked'
  function followPath(u, dt) {
    if (!u.path) return 'done';
    if (u.act) {
      const r = actOnTile(u, dt); if (r !== 'done') return r === 'fail' ? 'blocked' : 'moving';
    }
    if (u.pi >= u.path.length) { u.path = null; return 'done'; }
    const n = u.path[u.pi];
    if (n.f !== u.floor) { u.floor = n.f; u.pi++; u.act = { type: 'stairs', t: 0, dur: 0.5 }; return 'moving'; }
    const t = tileAt(n.f, n.x, n.y);
    if (t === T.DOOR) {
      const d = doorAt(n.f, n.x, n.y);
      if (d.state === 'closed') {
        if (d.barricaded) { d.knownLocked = true; if (u.team === 'op') log(`${u.name}: door barricaded.`, '#d9534f'); return 'blocked'; }
        if (d.locked && u.team === 'op') { d.knownLocked = true; voice(u, 'locked'); return 'blocked'; }
        u.act = { type: 'open', t: 0, dur: u.team === 'op' ? 0.45 : 0.6, door: d.id };
        return 'moving';
      }
    }
    if (t === T.WINDOW) { u.act = { type: 'vault', t: 0, dur: 1.0, tx: n.x, ty: n.y }; return 'moving'; }
    const gx = ctr(n.x), gy = ctr(n.y);
    const dx = gx - u.x, dy = gy - u.y, d = Math.hypot(dx, dy);
    const sp = moveSpeed(u) * dt;
    if (d <= sp) { u.x = gx; u.y = gy; u.pi++; }
    else { u.x += dx / d * sp; u.y += dy / d * sp; }
    u.moveAng = Math.atan2(dy, dx);
    // footsteps
    u.stepT -= dt;
    if (u.stepT <= 0) {
      u.stepT = u.pace === 'run' ? 0.3 : u.pace === 'slow' ? 0.65 : 0.45;
      const r = u.team === 'op' ? (u.pace === 'run' ? 5 : u.pace === 'slow' ? 1 : 2.2) : 2;
      noise(u.x, u.y, u.floor, r, 'step', u);
      if (!M.replayMode && (u.team === 'op' || u.visible) && R() < 0.8) A().step(u.x, u.y, u.pace === 'run');
    }
    return 'moving';
  }
  function actOnTile(u, dt) {
    const a = u.act; a.t += dt;
    if (a.t < a.dur) return 'busy';
    u.act = null;
    if (a.type === 'open') { const d = M.map.doors[a.door]; if (d.state === 'closed') { setDoor(d, 'open'); if (!M.replayMode) A().doorOpen(ctr(d.x), ctr(d.y)); noise(ctr(d.x), ctr(d.y), d.f, 3, 'door', u); } }
    if (a.type === 'vault') { if (tileAt(u.floor, a.tx, a.ty) === T.WINDOW) { M.map.floors[u.floor].t[a.ty * M.map.w + a.tx] = T.BWIN; M.events.push({ t: M.t, type: 'tile', f: u.floor, x: a.tx, y: a.ty, v: T.BWIN }); if (!M.replayMode) A().glass(ctr(a.tx), ctr(a.ty)); noise(ctr(a.tx), ctr(a.ty), u.floor, 12); fx({ type: 'glass', x: ctr(a.tx), y: ctr(a.ty), f: u.floor }); } }
    return 'done';
  }
  function turnTo(u, ang, dt, rate = 9) {
    const d = U.angDiff(u.angle, ang); const s = rate * dt;
    u.angle = Math.abs(d) <= s ? ang : U.angNorm(u.angle + Math.sign(d) * s);
    return Math.abs(U.angDiff(u.angle, ang));
  }
  function separate() {
    const list = M.units.filter(u => u.alive && !u.dead && !u.downed && !u.incap);
    for (let i = 0; i < list.length; i++) for (let j = i + 1; j < list.length; j++) {
      const a = list[i], b = list[j]; if (a.floor !== b.floor) continue;
      const dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy);
      if (d > 0.01 && d < 15) {
        const push = (15 - d) * 0.25; const nx = dx / d * push, ny = dy / d * push;
        const am = a.path || a.team !== 'op' ? 1 : 0.4, bm = b.path || b.team !== 'op' ? 1 : 0.4;
        if (!a.hostageBound && walkable(a.floor, toTile(a.x - nx), toTile(a.y - ny), 'enemy')) { a.x -= nx * am; a.y -= ny * am; }
        if (!b.hostageBound && walkable(b.floor, toTile(b.x + nx), toTile(b.y + ny), 'enemy')) { b.x += nx * bm; b.y += ny * bm; }
      }
    }
  }

  // ---------------- Operator logic ----------------
  function opThink(u, dt) {
    if (u.dead) return;
    if (u.downed) { u.status = u.stable ? 'DOWN (stable)' : `DOWN — bleeding ${Math.ceil(u.bleed)}s`; if (!u.stable) { u.bleed -= dt; if (u.bleed <= 0) { u.dead = true; u.alive = false; log(`${u.name} has died.`, '#d9534f'); BP.audio.say('We lost him.', { prio: true }); } } return; }
    if (u.stun > 0) u.stun -= dt;
    if (u.jamT > 0) { u.jamT -= dt; }
    if (u.reloadT > 0) { u.reloadT -= dt; if (u.reloadT <= 0) finishReload(u); }
    u.fireCd -= dt; u.recoilHeat = Math.max(0, u.recoilHeat - dt * 4);
    // stress decay (leader aura)
    const aura = ops().some(o => o !== u && canAct(o) && (o.op.role === 'leader' || o.pers.aura) && o.floor === u.floor && Math.hypot(o.x - u.x, o.y - u.y) < 7 * TS);
    u.stress = Math.max(0, u.stress - dt * (u.target ? 0.5 : 3) * (aura ? 1.8 : 1) * (1 + (u.stats.lea || 0) / 200));

    // perception
    let best = null, bd = 1e9;
    for (const e of M.units) {
      if (e.team === 'op' || !e.alive || e.dead) continue;
      if (e.floor !== u.floor) continue;
      if (!canSee(u, e)) continue;
      e.seenNow = true;
      if (e.team === 'enemy' && !e.spotted) { e.spotted = true; if (!M.replayMode && e.armed && !e.ai.dummy && e.ai.state !== 'surrender') { voice(u, 'contact'); u.stress = Math.min(100, u.stress + 6 * u.pers.stress); } }
      // civilians: order them down
      if (e.team === 'civ' && !e.hostageBound && e.ai.state !== 'secured' && e.ai.state !== 'cower' && !e.ai.dummy && Math.hypot(e.x - u.x, e.y - u.y) < 9 * TS && (!e.ai.toldT || M.t - e.ai.toldT > 4)) {
        e.ai.toldT = M.t; if (R() < 0.85) { e.ai.state = 'cower'; e.stance = 'crouch'; e.path = null; } else civPanic(e);
      }
      if (e.team !== 'enemy') continue;
      const d = Math.hypot(e.x - u.x, e.y - u.y);
      // compliance shout
      if (!e.ai.dummy && e.alive && !e.incap && !e.restrained && e.ai.state !== 'surrender' && d < 12 * TS) {
        const canShout = !e.ai.shouted || (e.stun > 0 && M.t - e.ai.shouted > 2.5) || (!e.armed && M.t - e.ai.shouted > 3);
        if (canShout && (e.ai.state !== 'combat' || e.stun > 0 || !e.armed || e.ai.aimingAt !== u.id)) {
          e.ai.shouted = M.t; e.ai.decideT = 0.35 + R() * 0.45; e.ai.shouter = u.id;
          voice(u, 'comply');
        }
      }
      if (isThreat(e) && d < bd) { bd = d; best = e; }
    }
    // engage
    if (best) {
      if (u.target !== best) {
        u.target = best; u.aimT = 0;
        const quick = u.perks.includes('quickdraw') ? 0.8 : 1;
        const w = curW(u);
        u.reactT = Math.max(0.12, (0.5 - u.stats.awa * 0.0035 + u.pers.react + u.stress * 0.002) * quick * (1.15 - w.st.aimSpeed / 250) * (u.op.role === 'point' ? 0.9 : 1));
      }
      const ang = Math.atan2(best.y - u.y, best.x - u.x);
      const w = curW(u);
      const rem = turnTo(u, ang, dt, 6 + w.st.handling / 12);
      u.aimT += dt;
      const holdForComply = best.ai.decideT > 0 && best.ai.state !== 'combat' && !(u.pers.dis && R() < 0.002);
      const aggressiveFire = best.ai.state === 'combat' || best.ai.dummy || best.stun <= 0 && best.ai.aimingAt;
      if (u.aimT >= u.reactT && rem < 0.12 && !holdForComply && (aggressiveFire || !best.ai.decideT) && u.stun <= 0) {
        if (lineClear(u, best)) {
          if (w.mag <= 0) startReload(u);
          else if (u.burstPause > 0) u.burstPause -= dt;
          else if (fire(u, best.x, best.y)) {
            const d = Math.hypot(best.x - u.x, best.y - u.y) / TS;
            if (w.st.mode === 'auto') { u.burst = (u.burst || 0) + 1; if (u.burst >= (d < 5 ? 3 : d < 12 ? 2 : 1)) { u.burst = 0; u.burstPause = d < 5 ? 0.12 : 0.28; } }
            else u.fireCd = Math.max(u.fireCd, w.st.mode === 'semi' ? 0.16 + d * 0.01 : u.fireCd);
          }
        }
      }
      u.status = u.reloadT > 0 ? 'Reloading' : 'Engaging';
    } else {
      if (u.target) { u.target = null; u.burst = 0; }
      const w = curW(u);
      if (w && u.reloadT <= 0 && w.mag < w.st.mag * 0.35 && w.reserve > 0 && !u.act) startReload(u);
    }

    // orders
    if (u.stun > 0 && !best) { u.status = 'Stunned'; }
    const o = u.orders[0];
    if (o) {
      const r = execOrder(u, o, dt, !!best);
      if (r === 'done') { u.orders.shift(); u.path = null; }
      else if (r === 'fail') { u.orders = []; u.path = null; u.act = null; }
    } else if (u.follow) {
      followTick(u, dt, best);
    } else {
      u.status = u.reloadT > 0 ? 'Reloading' : u.hold ? 'Holding' : best ? 'Engaging' : 'Idle';
      if (!best) { if (u.watchAngle !== null && u.watchAngle !== undefined) turnTo(u, u.watchAngle, dt, 5); else idleScan(u, dt); }
      // auto restrain / secure nearby
      if (!best && !u.hold) autoInteract(u);
    }
    // react to incoming fire
    if (!best && u.alertT !== undefined && M.t - u.alertT < 2.5) { turnTo(u, u.alertAng, dt, 11); return; }
    // facing while moving
    if (!best && u.path && u.moveAng !== undefined && !u.act) {
      // check corners: when slow, look toward open doorways nearby
      turnTo(u, u.moveAng, dt, 8);
    }
  }
  function idleScan(u, dt) {
    // face nearest unexplored doorway / last noise
    if (!u.scanT || u.scanT < M.t) {
      u.scanT = M.t + 2 + R() * 2;
      let best = null, bd = 1e9;
      for (const d of M.map.doors) { if (d.f !== u.floor || d.state === 'closed') continue; const dd = Math.hypot(ctr(d.x) - u.x, ctr(d.y) - u.y); if (dd < 7 * TS && dd > TS && dd < bd && los(u.floor, u.x, u.y, ctr(d.x), ctr(d.y), true)) { bd = dd; best = d; } }
      u.scanAng = best ? Math.atan2(ctr(best.y) - u.y, ctr(best.x) - u.x) : null;
    }
    if (u.scanAng !== null && u.scanAng !== undefined) turnTo(u, u.scanAng, dt, 4);
  }
  function lineClear(u, target) {
    // hold fire if friendly / civilian in the line
    const dx = target.x - u.x, dy = target.y - u.y, d = Math.hypot(dx, dy);
    for (const o of M.units) {
      if (o === u || o === target || o.floor !== u.floor || o.dead) continue;
      if (o.team === 'enemy' && !o.restrained && o.ai.state !== 'surrender') continue;
      if (o.downed || o.incap) continue;
      const ox = o.x - u.x, oy = o.y - u.y; const tp = (ox * dx + oy * dy) / d;
      if (tp < 0 || tp > d) continue;
      if (Math.abs(ox * dy - oy * dx) / d < 12) return false;
    }
    return true;
  }
  function autoInteract(u) {
    if (u.act) return;
    for (const e of M.units) {
      if (e.floor !== u.floor || e.dead) continue;
      const d = Math.hypot(e.x - u.x, e.y - u.y); if (d > 3.2 * TS) continue;
      if (e.team === 'enemy' && e.alive && !e.restrained && (e.ai.state === 'surrender' || e.incap) && los(u.floor, u.x, u.y, e.x, e.y, true)) { u.orders.push({ t: 'interact', target: e.id, kind: 'arrest', auto: true }); return; }
      if (e.team === 'civ' && e.alive && !e.ai.dummy && e.ai.state !== 'secured' && (e.hostageBound || e.ai.state === 'cower') && los(u.floor, u.x, u.y, e.x, e.y, true)) { u.orders.push({ t: 'interact', target: e.id, kind: 'secure', auto: true }); return; }
    }
    for (const it of M.items) {
      if (it.collected || it.f !== u.floor || (it.kind !== 'evidence' && it.kind !== 'contraband')) continue;
      if (Math.hypot(it.x - u.x, it.y - u.y) < 1.6 * TS && los(u.floor, u.x, u.y, it.x, it.y, true)) { u.orders.push({ t: 'interact', target: it.id, kind: 'collect', auto: true }); return; }
    }
  }
  function followTick(u, dt, threat) {
    const L = M.units.find(x => x.id === u.follow);
    if (!L || !canAct(L)) { u.follow = null; return; }
    const followers = ops().filter(o => o.follow === L.id);
    const i = followers.indexOf(u) + 1;
    const form = M.formation || 'stack';
    const back = L.angle + Math.PI;
    let ox, oy;
    if (form === 'stack') { ox = Math.cos(back) * i * 1.1 * TS; oy = Math.sin(back) * i * 1.1 * TS; }
    else { const side = i % 2 ? 1 : -1, row = Math.ceil(i / 2); ox = Math.cos(back) * row * TS + Math.cos(back + Math.PI / 2) * side * row * TS; oy = Math.sin(back) * row * TS + Math.sin(back + Math.PI / 2) * side * row * TS; }
    let tx = toTile(L.x + ox), ty = toTile(L.y + oy);
    if (!walkable(L.floor, tx, ty, 'enemy') || !los(L.floor, L.x, L.y, ctr(tx), ctr(ty), true)) { tx = toTile(L.x); ty = toTile(L.y); }
    const far = Math.hypot(ctr(tx) - u.x, ctr(ty) - u.y) > TS * 0.9 || u.floor !== L.floor;
    u.followT = (u.followT || 0) - dt;
    if (far && (u.followT <= 0 || !u.path)) { u.followT = 0.5; setPath(u, L.floor, tx, ty); }
    if (u.path) { u.pace = L.pace; followPath(u, dt); }
    else if (!threat) turnTo(u, L.angle + (i % 2 ? 0.6 : -0.6), dt, 4);
    u.status = 'Following ' + L.name;
  }

  function execOrder(u, o, dt, engaged) {
    switch (o.t) {
      case 'move': {
        if (!o.started) { o.started = true; if (!setPath(u, o.f, o.x, o.y)) { log(`${u.name}: no route.`, '#d9534f'); return 'fail'; } }
        u.pace = o.pace || u.pace;
        u.status = o.clearing ? 'Clearing' : u.pace === 'run' ? 'Running' : u.pace === 'slow' ? 'Moving (slow)' : 'Moving';
        // slow down when engaging and not running
        const r = followPath(u, dt);
        if (r === 'blocked') { log(`${u.name}: path blocked.`, '#e2a33a'); return 'fail'; }
        if (r === 'done') {
          if (o.clearing) maybeRoomClear(u);
          return 'done';
        }
        return 'busy';
      }
      case 'door': {
        const d = M.map.doors[o.door];
        if (engaged && !['charge'].includes(o.a)) { u.status = 'Engaging'; return 'busy'; }
        // face door
        turnTo(u, Math.atan2(ctr(d.y) - u.y, ctr(d.x) - u.x), dt, 8);
        const r = doDoorAction(u, d, o.a, dt);
        u.status = { entry: 'Opening', open: 'Opening door', kick: 'Kicking door', ram: 'Ramming', shotgun: 'Shotgun breach', charge: 'Setting charge', pick: 'Picking lock', cam: 'Door camera', close: 'Closing door' }[o.a] || 'Door';
        if (r === 'fail') return 'fail';
        return r === 'done' ? 'done' : 'busy';
      }
      case 'throw': {
        if (o.f !== u.floor) return 'done';
        const ang = Math.atan2(o.y - u.y, o.x - u.x);
        const rem = turnTo(u, ang, dt, 10);
        if (!o.t0) o.t0 = 0; o.t0 += dt;
        u.status = 'Throwing';
        if (rem < 0.2 && o.t0 > 0.35) { if (!throwGrenade(u, o.g, o.x, o.y)) { log(`${u.name}: none left.`, '#d9534f'); } return 'done'; }
        return 'busy';
      }
      case 'wait': { o.el = (o.el || 0) + dt; u.status = 'Waiting'; return o.el >= o.dur ? 'done' : 'busy'; }
      case 'go': { u.status = 'WAITING FOR GO'; return 'busy'; }
      case 'waitdoor': {
        const d = M.map.doors[o.door];
        const L = M.units.find(x => x.id === o.leader);
        u.status = 'Stacked up';
        if (d.state !== 'closed') { o.el = (o.el || 0) + dt; return o.el >= (o.delay || 0) ? 'done' : 'busy'; }
        if (!L || !canAct(L) || !L.orders.some(x => x.t === 'door' && x.door === d.id)) return 'fail';
        return 'busy';
      }
      case 'interact': return execInteract(u, o, dt, engaged);
    }
    return 'done';
  }
  function maybeRoomClear(u) {
    const rid = M.map.floors[u.floor].room[toTile(u.y) * M.map.w + toTile(u.x)];
    if (rid < 0) return;
    const r = M.map.rooms[rid];
    const threats = M.units.some(e => e.team === 'enemy' && e.floor === u.floor && isThreat(e) && toTile(e.x) >= r.x && toTile(e.x) < r.x + r.w && toTile(e.y) >= r.y && toTile(e.y) < r.y + r.h);
    if (!threats && (!M.lastClear || M.t - M.lastClear > 3)) { M.lastClear = M.t; voice(u, 'clear'); }
  }
  function execInteract(u, o, dt, engaged) {
    const tgt = o.kind === 'collect' ? M.items.find(i => i.id === o.target) : M.units.find(x => x.id === o.target);
    if (!tgt) return 'done';
    const tf = tgt.floor ?? tgt.f;
    // validity
    if (o.kind === 'arrest' && (tgt.dead || tgt.restrained || !(tgt.ai.state === 'surrender' || tgt.incap || tgt.stun > 0))) return o.kind === 'arrest' && tgt.ai && tgt.ai.state === 'combat' && !tgt.stun ? 'fail' : 'done';
    if (o.kind === 'secure' && (tgt.dead || tgt.ai.state === 'secured')) return 'done';
    if (o.kind === 'heal' && (tgt.dead || (!tgt.downed && tgt.hp >= tgt.maxHp * 0.9) || (tgt.downed && tgt.stable && u.tool !== 'medkit'))) return 'done';
    if (o.kind === 'collect' && tgt.collected) return 'done';
    const d = Math.hypot(tgt.x - u.x, tgt.y - u.y);
    if (tf !== u.floor || d > 1.35 * TS) {
      if (!o.pathed || (!u.path && !u.act)) { o.pathed = true; if (!setPath(u, tf, toTile(tgt.x), toTile(tgt.y))) return 'fail'; }
      // stop at adjacency
      const r = followPath(u, dt); u.status = 'Moving';
      if (r === 'blocked') return 'fail';
      if (r === 'done' && (tf !== u.floor || Math.hypot(tgt.x - u.x, tgt.y - u.y) > 1.35 * TS)) { o.pathed = false; }
      return 'busy';
    }
    u.path = null;
    if (engaged) { u.status = 'Engaging'; return 'busy'; }
    turnTo(u, Math.atan2(tgt.y - u.y, tgt.x - u.x), dt, 8);
    if (!o.el) { o.el = 0; o.dur = { arrest: 1.5, secure: 1.2, collect: 1.6, heal: (u.perks.includes('medic') ? 2 : 4) * (1.3 - u.stats.med / 200) * (u.op.role === 'medic' ? 0.75 : 1) }[o.kind]; if (o.kind === 'arrest' || o.kind === 'secure') { if (!M.replayMode) A().cuffs(tgt.x, tgt.y); } }
    o.el += dt;
    u.status = { arrest: 'Restraining', secure: 'Securing civilian', collect: 'Bagging evidence', heal: 'Treating ' + (tgt.name || '') }[o.kind];
    if (o.el < o.dur) return 'busy';
    if (o.kind === 'arrest') {
      tgt.restrained = true; tgt.ai.state = 'restrained'; tgt.surrendered = true; tgt.stun = 0; tgt.path = null;
      if (tgt.armed) { tgt.armed = false; M.items.push({ id: U.uid('drop'), kind: 'drop', type: 'weapon', x: tgt.x + 8, y: tgt.y + 6, f: tgt.floor }); }
      u.arrests++; voice(u, 'arrest'); log(`${tgt.name} restrained by ${u.name}.`, '#5cb85c');
      if (tgt.hvt) { log(`HVT ${tgt.name} CAPTURED ALIVE.`, '#5cb85c'); BP.audio.say('Primary target in custody!', { prio: true }); }
    } else if (o.kind === 'secure') {
      tgt.ai.state = 'secured'; tgt.path = null; tgt.stance = 'crouch'; tgt.kneel = true; tgt.restrained = true; tgt.hostageBound = false;
      u.rescues++; voice(u, 'secure'); log(`${tgt.name} secured by ${u.name}.`, '#5cb85c');
    } else if (o.kind === 'collect') {
      tgt.collected = true; u.evidence++; voice(u, 'evidence'); log(`${u.name} recovered ${tgt.name}.`, '#4fc3f7'); if (!M.replayMode) A().equip();
    } else if (o.kind === 'heal') {
      const kit = u.tool === 'medkit' && u.toolUses > 0;
      if (tgt.downed) {
        if (kit) { u.toolUses--; tgt.downed = false; tgt.hp = 35 + u.stats.med / 5; tgt.stable = false; log(`${u.name} revived ${tgt.name}.`, '#5cb85c'); }
        else { tgt.stable = true; log(`${u.name} stabilized ${tgt.name}.`, '#5cb85c'); }
      } else if (kit) { u.toolUses--; tgt.hp = Math.min(tgt.maxHp, tgt.hp + 30 + u.stats.med / 4); log(`${u.name} treated ${tgt.name}.`, '#5cb85c'); }
    }
    return 'done';
  }

  // ---------------- Enemy AI ----------------
  function enterCombat(e, tgt, surprised) {
    const ai = e.ai;
    if (ai.dummy) return;
    if (!e.armed) { if (ai.state !== 'flee' && ai.state !== 'surrender') { if (R() < 0.6) { surrender(e); } else startFlee(e); } return; }
    const wasCalm = ai.state === 'idle' || ai.state === 'hide';
    ai.state = 'combat'; ai.lostT = 0;
    if (tgt) ai.lastKnown = { x: tgt.x, y: tgt.y, f: tgt.floor };
    e.target = null; e.aimT = 0;
    if (ai.radio && !ai.radioed) { ai.radioed = true; ai.radioT = 1.5; }
    if (e.hvt && R() < 0.75) startFlee(e);
    if (ai.guard && ai.execT < 0) ai.execT = M.diff.execTime + R() * 4;
    if (wasCalm && surprised) e.reactBonus = 0.35;
  }
  function alertEnemy(e, pos, via) {
    const ai = e.ai; if (ai.dummy || !e.alive || e.incap || e.restrained || ai.state === 'combat' || ai.state === 'surrender' || ai.state === 'flee') return;
    ai.alerted = true;
    if (ai.guard && ai.execT < 0 && via !== 'step') ai.execT = M.diff.execTime + R() * 4;
    if (e.hvt) { if (via !== 'step') startFlee(e); return; }
    if (!e.armed) { if (R() < 0.5) startFlee(e); else { ai.state = 'hide'; e.stance = 'crouch'; } return; }
    if (ai.style === 'rusher' || via === 'diversion' || (via === 'step' && R() < 0.5)) { ai.state = 'investigate'; ai.investigate = pos; e.path = null; ai.invT = 0; }
    else if (ai.style === 'hider') { ai.state = 'hide'; e.stance = 'crouch'; ai.faceTo = Math.atan2(pos.y - e.y, pos.x - e.x); }
    else { ai.state = 'alert'; ai.faceTo = pos.f === e.floor ? Math.atan2(pos.y - e.y, pos.x - e.x) : e.angle; }
  }
  function surrender(e) {
    const ai = e.ai;
    ai.state = 'surrender'; e.surrendered = true; e.target = null; e.path = null; e.stance = 'stand'; ai.surrT = 0;
    if (e.armed) { e.armed = false; M.items.push({ id: U.uid('drop'), kind: 'drop', type: 'weapon', x: e.x + R.range(-12, 12), y: e.y + R.range(-12, 12), f: e.floor }); }
    log(`${e.name} is surrendering.`, '#5cb85c');
  }
  function startFlee(e) {
    const ai = e.ai; ai.state = 'flee'; e.path = null; e.stance = 'stand';
    // choose exit far from operators' spawn
    let best = null, bs = -1;
    const opsList = ops();
    for (let i = 0; i < 24; i++) {
      const ex = M.exits[Math.floor(R() * M.exits.length)]; if (!ex) break;
      let md = 1e9; for (const o of opsList) md = Math.min(md, Math.hypot(ctr(ex[0]) - o.x, ctr(ex[1]) - o.y));
      if (md > bs) { bs = md; best = ex; }
    }
    ai.exit = best;
  }
  function surrenderChance(e) {
    const E = D.ENEMY[e.kind] || D.ENEMY.guard;
    let p = E.surrender;
    if (e.stun > 0) p += 0.45; if (e.gas > 0) p += 0.3; if (e.hp < 60) p += 0.2;
    const seeing = ops().filter(o => canAct(o) && o.floor === e.floor && Math.hypot(o.x - e.x, o.y - e.y) < 10 * TS && los(e.floor, e.x, e.y, o.x, o.y)).length;
    p += 0.08 * Math.max(0, seeing - 1);
    const sh = M.units.find(x => x.id === e.ai.shouter);
    if (sh && sh.perks && sh.perks.includes('negotiator')) p += 0.25;
    if (sh && sh.op) p += (sh.stats.lea - 50) / 400;
    p *= (1.3 - U.clamp(e.ai.morale, 0, 100) / 100);
    if (!e.armed) p += 0.35;
    if (M.cfg.difficulty === 'veteran') p -= 0.05; if (M.cfg.difficulty === 'milsim') p -= 0.1;
    return U.clamp(p, 0.02, 0.95);
  }
  function enemyThink(e, dt) {
    const ai = e.ai;
    if (!e.alive || e.dead || e.restrained || e.incap) return;
    if (ai.dummy) return;
    e.fireCd -= dt; e.recoilHeat = Math.max(0, e.recoilHeat - dt * 3);
    if (e.reloadT > 0) { e.reloadT -= dt; if (e.reloadT <= 0) finishReload(e); }
    if (e.jamT > 0) e.jamT -= dt;
    if (e.stun > 0) { e.stun -= dt; e.status = 'stunned'; if (ai.decideT > 0) { ai.decideT -= dt; if (ai.decideT <= 0) decide(e); } return; }
    // radio broadcast
    if (ai.radioT > 0) { ai.radioT -= dt; if (ai.radioT <= 0) radioAlert(e); }
    // surrender behaviour
    if (ai.state === 'surrender') {
      ai.surrT += dt;
      const watched = ops().some(o => canAct(o) && o.floor === e.floor && Math.hypot(o.x - e.x, o.y - e.y) < 12 * TS && los(e.floor, o.x, o.y, e.x, e.y, true));
      if (watched) ai.unwatched = 0; else ai.unwatched = (ai.unwatched || 0) + dt;
      if (ai.unwatched > 14 && R() < dt * 0.15) { ai.state = 'flee'; e.surrendered = false; startFlee(e); log(`${e.name} is fleeing!`, '#e2a33a'); }
      return;
    }
    // decision after shout
    if (ai.decideT > 0) { ai.decideT -= dt; if (ai.decideT <= 0) { decide(e); if (ai.state === 'surrender') return; } }
    // executioner
    if (ai.guard && ai.execT >= 0 && ai.state !== 'surrender') {
      ai.execT -= dt;
      if (ai.execT <= 0) {
        const h = (ai.guardList || [ai.guard]).find(x => x.alive && !x.dead && x.ai.state !== 'secured' && x.floor === e.floor && los(e.floor, e.x, e.y, x.x, x.y, true));
        if (h && e.armed) {
          const rem = turnTo(e, Math.atan2(h.y - e.y, h.x - e.x), dt, 8);
          if (rem < 0.1 && e.fireCd <= 0) { fire(e, h.x, h.y); if (!ai.execLogged) { ai.execLogged = true; log(`${e.name} is executing a hostage!`, '#d9534f'); } }
          return;
        } else ai.execT = 4;
      }
    }
    // perception
    let tgt = null, td = 1e9;
    for (const o of M.units) {
      if (o.team !== 'op' || !canAct(o) || o.floor !== e.floor) continue;
      if (!canSee(e, o)) continue;
      const d = Math.hypot(o.x - e.x, o.y - e.y); if (d < td) { td = d; tgt = o; }
    }
    if (ai.state === 'flee') {
      if (tgt && e.armed && td < 6 * TS && ai.style !== 'hider' && R() < 0.4 * dt) { enterCombat(e, tgt); }
      else { fleeTick(e, dt); return; }
    }
    if (tgt) {
      if (ai.state !== 'combat') {
        // detection delay: suspects need a moment to recognise a threat
        const calm = ai.state === 'idle';
        const thr = (ai.state === 'hide' ? 0.12 : calm ? 0.35 : 0.15) + (td / TS) * (calm ? 0.06 : 0.03) * (M.night ? 1.4 : 1) * (tgt.stance === 'crouch' ? 1.3 : 1);
        ai.noticeT = (ai.noticeT || 0) + dt;
        turnTo(e, Math.atan2(tgt.y - e.y, tgt.x - e.x), dt, calm ? 2.5 : 5);
        if (ai.noticeT < thr) { e.status = 'noticing'; return; }
        enterCombat(e, tgt, calm || ai.state === 'hide');
      }
      if (ai.state !== 'combat') return;
      ai.lastKnown = { x: tgt.x, y: tgt.y, f: tgt.floor }; ai.lostT = 0;
      if (e.target !== tgt) { e.target = tgt; e.aimT = 0; e.reactT = (e.react * M.diff.enemyReact + (e.reactBonus || 0)) * (0.8 + R() * 0.5); e.reactBonus = 0; }
      ai.aimingAt = tgt.id;
      const ang = Math.atan2(tgt.y - e.y, tgt.x - e.x);
      const rem = turnTo(e, ang, dt, 6);
      e.aimT += dt;
      // movement in combat
      if (ai.style === 'rusher' && td > 4 * TS && !e.path && R() < dt * 0.6) setPath(e, tgt.floor, toTile(tgt.x), toTile(tgt.y));
      if (e.path && td < 4 * TS) e.path = null;
      if (e.path) followPath(e, dt);
      if (e.aimT >= e.reactT && rem < 0.15 && e.armed) {
        const w = curW(e);
        if (w.mag <= 0) { if (w.reserve > 0) startReload(e); else { e.armed = false; ai.state = 'alert'; } }
        else if (e.burstPause > 0) e.burstPause -= dt;
        else if (fire(e, tgt.x, tgt.y)) { e.burst++; const mx = w.st.mode === 'auto' ? 2 + Math.floor(R() * 3) : 1; if (e.burst >= mx) { e.burst = 0; e.burstPause = w.st.mode === 'auto' ? 0.35 + R() * 0.4 : 0.25 + R() * 0.45; } }
      }
      // periodic surrender / break check
      ai.checkT -= dt;
      if (ai.checkT <= 0) {
        ai.checkT = 1.6;
        if ((e.hp < 70 || ai.morale < 30 || e.gas > 0) && R() < surrenderChance(e) * 0.35) { surrender(e); return; }
        if (ai.morale < 15 && R() < 0.3) { startFlee(e); return; }
      }
      e.status = 'combat';
      return;
    }
    ai.aimingAt = null; e.target = null; ai.noticeT = Math.max(0, (ai.noticeT || 0) - dt * 0.5);
    if (ai.state === 'combat') {
      ai.lostT += dt;
      const lk = ai.lastKnown;
      if (ai.style === 'rusher' && lk && ai.lostT > 0.8) {
        if (!e.path && lk) setPath(e, lk.f, toTile(lk.x), toTile(lk.y));
        if (e.path) { if (followPath(e, dt) !== 'moving') e.path = null; }
      } else if (lk && lk.f === e.floor) turnTo(e, Math.atan2(lk.y - e.y, lk.x - e.x), dt, 4);
      if (ai.lostT > 10) { ai.state = 'alert'; e.path = null; }
      return;
    }
    if (ai.state === 'investigate') {
      const p = ai.investigate;
      if (!e.path && !ai.invArrived) { if (!setPath(e, p.f, toTile(p.x), toTile(p.y))) { ai.state = 'alert'; return; } }
      if (e.path) { const r = followPath(e, dt); if (r !== 'moving') { e.path = null; ai.invArrived = true; } if (e.path && e.moveAng !== undefined && !e.act) turnTo(e, e.moveAng, dt, 6); }
      else { ai.invT += dt; e.angle += dt * 1.2 * Math.sin(ai.invT * 1.3); if (ai.invT > 5) { ai.state = 'alert'; ai.invArrived = false; } }
      return;
    }
    if (ai.state === 'alert' || ai.state === 'hide') {
      if (ai.faceTo !== undefined) turnTo(e, ai.faceTo, dt, 4);
      return;
    }
    // idle: patrol
    if (ai.patrol && ai.patrol.length > 1) {
      if (!e.path) {
        ai.waitT -= dt;
        if (ai.waitT <= 0) { ai.pi = (ai.pi + 1) % ai.patrol.length; const p = ai.patrol[ai.pi]; setPath(e, e.floor, p[0], p[1]); ai.waitT = 2 + R() * 4; }
        else e.angle += dt * 0.3 * Math.sin(M.t + e.x);
      } else { if (followPath(e, dt) !== 'moving') e.path = null; if (e.moveAng !== undefined) turnTo(e, e.moveAng, dt, 5); }
    } else {
      ai.waitT -= dt;
      if (ai.waitT <= 0) { ai.waitT = 3 + R() * 5; ai.faceTo = e.angle + R.range(-1.5, 1.5); }
      if (ai.faceTo !== undefined) turnTo(e, ai.faceTo, dt, 2);
    }
  }
  function decide(e) {
    const ai = e.ai;
    if (ai.state === 'surrender' || !e.alive) return;
    const p = surrenderChance(e);
    if (R() < p) surrender(e);
    else {
      const sh = M.units.find(x => x.id === ai.shouter);
      if (e.armed) { enterCombat(e, sh, true); e.reactBonus = 0.2; }
      else startFlee(e);
    }
  }
  function radioAlert(src) {
    if (!src.alive || src.dead || src.restrained || src.stun > 0) return;
    let n = 0;
    for (const e of M.units) {
      if (e.team !== 'enemy' || e === src || !e.ai.radio) continue;
      if (['combat', 'surrender', 'flee', 'restrained'].includes(e.ai.state)) continue;
      alertEnemy(e, src.ai.lastKnown || { x: src.x, y: src.y, f: src.floor }, 'radio'); n++;
    }
    if (n) log('Intercepted suspect radio traffic — they know you\'re here.', '#d9534f');
  }
  function fleeTick(e, dt) {
    const ai = e.ai;
    if (!ai.exit) startFlee(e);
    if (!ai.exit) return;
    if (!e.path) { if (!setPath(e, 0, ai.exit[0], ai.exit[1])) { ai.state = 'hide'; e.stance = 'crouch'; return; } }
    const r = followPath(e, dt);
    if (e.moveAng !== undefined) turnTo(e, e.moveAng, dt, 8);
    if (r !== 'moving') e.path = null;
    if (e.floor === 0 && (toTile(e.x) <= 1 || toTile(e.y) <= 1 || toTile(e.x) >= M.map.w - 2 || toTile(e.y) >= M.map.h - 2)) {
      e.escaped = true; e.alive = false; M.stats.escaped++;
      log(`${e.name} ESCAPED the perimeter.`, '#d9534f');
    }
    e.status = 'fleeing';
  }

  // ---------------- Civilians ----------------
  function civPanic(c) {
    if (c.ai.dummy || c.hostageBound || c.ai.state === 'secured' || !c.alive) return;
    if (c.ai.state === 'panic' || c.ai.state === 'flee' || c.ai.state === 'cower') return;
    if (!M.replayMode && R() < 0.5) A().scream(c.x, c.y);
    if (R() < 0.55) { c.ai.state = 'cower'; c.stance = 'crouch'; c.path = null; }
    else { c.ai.state = 'flee'; startFleeCiv(c); }
  }
  function startFleeCiv(c) {
    let best = null, bd = 1e9;
    for (let i = 0; i < 20; i++) { const ex = M.exits[Math.floor(R() * M.exits.length)]; if (!ex) break; const d = Math.hypot(ctr(ex[0]) - c.x, ctr(ex[1]) - c.y); if (d < bd) { bd = d; best = ex; } }
    c.ai.exit = best; c.path = null;
  }
  function civThink(c, dt) {
    if (!c.alive || c.dead || c.ai.dummy) return;
    if (c.stun > 0) { c.stun -= dt; return; }
    const ai = c.ai;
    if (ai.state === 'flee') {
      if (!ai.exit) { ai.state = 'cower'; return; }
      if (!c.path) { if (!setPath(c, 0, ai.exit[0], ai.exit[1])) { ai.state = 'cower'; c.stance = 'crouch'; return; } }
      const r = followPath(c, dt); if (c.moveAng !== undefined) turnTo(c, c.moveAng, dt, 8);
      if (r !== 'moving') c.path = null;
      if (c.floor === 0 && (toTile(c.x) <= 1 || toTile(c.y) <= 1 || toTile(c.x) >= M.map.w - 2 || toTile(c.y) >= M.map.h - 2)) { c.evacuated = true; c.alive = false; log('A civilian fled the area.', '#9fb0bf'); }
      return;
    }
    if (ai.state === 'idle' && !c.hostageBound) {
      ai.waitT -= dt; if (ai.waitT <= 0) { ai.waitT = 3 + R() * 5; c.angle += R.range(-1.5, 1.5); }
    }
  }

  // ---------------- Noise processing ----------------
  function processNoises() {
    for (const n of M.noises) {
      for (const u of M.units) {
        if (u.team === 'op' || !u.alive || u.dead || u === n.src) continue;
        if ((n.kind === 'step' || n.kind === 'door') && (!n.src || n.src.team !== 'op')) continue;
        const sameF = u.floor === n.f;
        const d = Math.hypot(u.x - n.x, u.y - n.y);
        let r = n.r * (sameF ? 1 : 0.35);
        if (d > r) continue;
        if (sameF && !los(u.floor, u.x, u.y, n.x, n.y, true)) r *= 0.6;
        if (d > r) continue;
        if (u.team === 'enemy') {
          if (n.kind === 'step' && u.ai.state !== 'idle') continue;
          alertEnemy(u, { x: n.x, y: n.y, f: n.f }, n.kind);
        } else if (u.team === 'civ' && (n.kind === 'gunshot' || n.kind === 'explosion' || n.kind === 'breach') && u.ai.state === 'idle') civPanic(u);
      }
    }
    M.noises = [];
  }

  // ---------------- Objectives ----------------
  function objectives() {
    const cfg = M.cfg; const out = [];
    const enemies = M.units.filter(u => u.team === 'enemy');
    const hostages = M.units.filter(u => u.kind === 'hostage');
    const civs = M.units.filter(u => u.kind === 'civilian');
    const ev = M.items.filter(i => i.kind === 'evidence'), con = M.items.filter(i => i.kind === 'contraband');
    const active = enemies.filter(e => e.alive && !e.restrained && !e.escaped && !e.dead && !e.incap);
    const neutral = enemies.length - active.length;
    if (cfg.training) {
      const dummies = enemies.length, hit = enemies.filter(e => e.dead).length;
      out.push({ id: 'neutralize', text: `Hit all targets (${hit}/${dummies})`, done: hit === dummies, req: true });
      out.push({ id: 'noshoot', text: `Avoid no-shoot targets (${M.stats.civKilledByOp} hit)`, done: M.stats.civKilledByOp === 0, req: false, fail: M.stats.civKilledByOp > 0 });
      return out;
    }
    out.push({ id: 'neutralize', text: `Neutralize suspects (${neutral}/${enemies.length})`, done: active.length === 0, req: true });
    if (hostages.length) {
      const saved = hostages.filter(h => h.alive && h.ai.state === 'secured').length, dead = hostages.filter(h => h.dead).length;
      out.push({ id: 'rescue', text: `Rescue hostages (${saved}/${hostages.length}${dead ? `, ${dead} KIA` : ''})`, done: saved + dead === hostages.length && saved > 0, req: true, fail: dead > hostages.length / 2 });
    }
    if (M.hvt) out.push({ id: 'capture', text: `Capture ${M.hvt.name} alive`, done: M.hvt.restrained && !M.hvt.dead, req: false, fail: M.hvt.dead || M.hvt.escaped });
    if (ev.length) out.push({ id: 'evidence', text: `Recover evidence (${ev.filter(i => i.collected).length}/${ev.length})`, done: ev.every(i => i.collected), req: false });
    if (con.length) out.push({ id: 'contraband', text: `Seize contraband (${con.filter(i => i.collected).length}/${con.length})`, done: con.every(i => i.collected), req: false });
    if (civs.length) { const s = civs.filter(c => c.alive && (c.ai.state === 'secured' || c.evacuated)).length; out.push({ id: 'civs', text: `Secure civilians (${s}/${civs.length})`, done: s === civs.filter(c => !c.dead).length, req: false, fail: civs.some(c => c.dead) }); }
    const arrestable = enemies.filter(e => e.alive && (e.ai.state === 'surrender' || e.incap) && !e.restrained).length;
    if (arrestable) out.push({ id: 'restrain', text: `Restrain surrendered/incapacitated suspects (${arrestable})`, done: false, req: false });
    return out;
  }
  function checkEnd(dt) {
    M.objT -= dt; if (M.objT > 0) return;
    M.objT = 0.5;
    const opsAlive = ops().filter(canAct);
    if (!opsAlive.length) { finish(false, 'All operators down.'); return; }
    const objs = objectives();
    M.objectives = objs;
    const reqDone = objs.filter(o => o.req).every(o => o.done);
    const failed = objs.find(o => o.req && o.fail);
    if (failed && !M.failNotified) { M.failNotified = true; log('MISSION FAILURE CONDITION: too many hostages lost.', '#d9534f'); }
    if (reqDone && !M.allClearAnnounced) {
      M.allClearAnnounced = true; M.canComplete = true;
      log('Area secure. Complete remaining objectives or end the operation.', '#5cb85c');
      BP.audio.say(D.VOICE.allclear[0], { prio: true });
    }
  }

  // ---------------- Main update ----------------
  function update(dt) {
    if (!M || M.ended || M.paused) return;
    dt = Math.min(dt, 0.05) * M.speed;
    M.t += dt;
    for (const u of M.units) u.seenNow = false;
    for (const u of M.units) {
      if (u.team === 'op') opThink(u, dt);
      else if (u.team === 'enemy') enemyThink(u, dt);
      else civThink(u, dt);
    }
    // visibility flags (for render), last seen
    for (const u of M.units) {
      if (u.team === 'op') { u.visible = true; continue; }
      let vis = u.seenNow;
      if (!vis) for (const c of M.camSources) if (c.f === u.floor && Math.hypot(u.x - c.x, u.y - c.y) < 8 * TS && los(c.f, c.x, c.y, u.x, u.y)) vis = true;
      if (M.diff.showEnemies && u.team === 'enemy' && u.alive) vis = vis || false;
      u.visible = vis || (u.dead && u.wasSeen) || (u.restrained);
      if (vis) { u.wasSeen = true; u.lastSeen = { x: u.x, y: u.y, f: u.floor, t: M.t }; }
    }
    for (const c of M.camSources) c.t -= dt;
    M.camSources = M.camSources.filter(c => c.t > 0);
    processNoises();
    updateGrenades(dt);
    updateGas(dt);
    separate();
    M.heard = M.heard.filter(h => M.t - h.t < 2.5);
    // record
    M.recT -= dt;
    if (M.recT <= 0) { M.recT = 0.15; record(); }
    checkEnd(dt);
  }
  function refreshVisibility() {
    if (!M) return;
    for (const e of M.units) {
      if (e.team === 'op') { e.visible = true; continue; }
      let vis = false;
      for (const o of M.units) if (o.team === 'op' && canAct(o) && canSee(o, e)) { vis = true; break; }
      e.visible = vis || (e.dead && e.wasSeen) || e.restrained;
      if (vis) { e.wasSeen = true; e.lastSeen = { x: e.x, y: e.y, f: e.floor, t: M.t }; }
    }
  }
  function record() {
    M.frames.push({ t: M.t, u: M.units.map(u => [u.x | 0, u.y | 0, u.floor, Math.round(u.angle * 100), stateCode(u), Math.max(0, u.hp | 0)]) });
  }
  function stateCode(u) {
    return (u.dead ? 1 : 0) | (u.downed ? 2 : 0) | (u.incap ? 4 : 0) | (u.restrained ? 8 : 0) | (u.surrendered || (u.ai && u.ai.state === 'surrender') ? 16 : 0) | (u.stun > 0 ? 32 : 0) | (u.escaped || u.evacuated ? 64 : 0) | (u.stance === 'crouch' ? 128 : 0) | (u.armed === false ? 256 : 0) | (u.hostageBound ? 512 : 0) | (u.kneel ? 1024 : 0);
  }

  function start() { M.phase = 'live'; M.paused = false; log('EXECUTE. All teams go.', '#e2a33a'); BP.audio.say('All teams, execute!', { prio: true }); }

  // ---------------- Evaluation ----------------
  function finish(success, reason) {
    if (M.ended) return M.result;
    M.ended = true; M.paused = true; record();
    const cfg = M.cfg;
    const enemies = M.units.filter(u => u.team === 'enemy');
    const hostages = M.units.filter(u => u.kind === 'hostage');
    const civs = M.units.filter(u => u.kind === 'civilian');
    const opsL = ops();
    const ev = M.items.filter(i => i.kind === 'evidence'), con = M.items.filter(i => i.kind === 'contraband');
    const arrested = enemies.filter(e => e.restrained && !e.dead).length;
    const killed = enemies.filter(e => e.dead).length;
    const escaped = enemies.filter(e => e.escaped).length;
    const activeLeft = enemies.filter(e => e.alive && !e.restrained && !e.escaped && !e.dead && !e.incap).length;
    const hostSaved = hostages.filter(h => h.alive && h.ai.state === 'secured').length;
    const hostDead = hostages.filter(h => h.dead).length;
    const civDead = civs.filter(c => c.dead).length;
    const opsDown = opsL.filter(o => o.downed || o.dead).length, opsDead = opsL.filter(o => o.dead).length;
    const opsInjured = opsL.filter(o => !o.dead && o.hp < o.maxHp).length;
    const evFound = ev.filter(i => i.collected).length, conFound = con.filter(i => i.collected).length;
    const allDown = opsL.every(o => !canAct(o));
    const reqOK = activeLeft === 0 && (hostages.length === 0 || hostSaved > 0);
    let failed = allDown || !reqOK || hostDead > hostages.length / 2 || !success && !reqOK;
    if (cfg.training) failed = allDown;
    const S = [];
    const add = (name, pts, max, detail) => S.push({ name, pts: Math.round(pts), max, detail });
    add('Mission completion', failed ? 0 : 25, 25, failed ? (reason || 'Objectives incomplete') : 'Primary objectives complete');
    add('Hostages rescued', hostages.length ? 15 * hostSaved / hostages.length : 15, 15, hostages.length ? `${hostSaved}/${hostages.length} rescued` : 'No hostages');
    add('Suspects apprehended', enemies.length ? 15 * (arrested + killed * 0.45) / enemies.length - escaped * 3 : 15, 15, `${arrested} arrested, ${killed} killed, ${escaped} escaped`);
    add('Evidence recovered', (ev.length + con.length) ? 15 * (evFound + conFound) / (ev.length + con.length) : 15, 15, `${evFound}/${ev.length} evidence, ${conFound}/${con.length} contraband`);
    add('Civilian safety', Math.max(-20, 10 - civDead * 5 - hostDead * 4 - M.stats.civKilledByOp * 10 - M.stats.roe * 6), 10, `${civDead + hostDead} civilian casualties${M.stats.civKilledByOp ? `, ${M.stats.civKilledByOp} by friendly fire` : ''}${M.stats.roe ? `, ${M.stats.roe} ROE violations` : ''}`);
    add('Operator injuries', Math.max(-15, 10 - opsInjured * 2 - opsDown * 4 - opsDead * 8), 10, `${opsInjured} injured, ${opsDown} downed, ${opsDead} KIA`);
    const acc = M.stats.shotsOp ? M.stats.hitsOp / M.stats.shotsOp : 1;
    add('Ammunition expenditure', U.clamp(5 * Math.min(1, acc * 2.2), 0, 5), 5, `${M.stats.shotsOp} rounds, ${(acc * 100).toFixed(0)}% hits`);
    add('Equipment losses', opsDead ? 0 : 2, 2, opsDead ? 'Operator equipment lost' : 'No losses');
    const par = cfg.parTime || 240;
    add('Time taken', U.clamp(3 * (1.6 - M.t / par), 0, 3), 3, `${U.fmtTime(M.t)} (par ${U.fmtTime(par)})`);
    const total = S.reduce((a, s) => a + s.pts, 0);
    let rank = failed ? 'F' : total >= 90 ? 'S' : total >= 76 ? 'A' : total >= 60 ? 'B' : 'C';
    if (!failed && M.hvt && (M.hvt.dead || M.hvt.escaped) && (rank === 'S' || rank === 'A')) rank = 'B';
    M.result = {
      failed, rank, total, score: S, time: M.t, reason, arrested, killed, escaped, hostSaved, hostDead, civDead, evFound, conFound,
      evidence: ev.filter(i => i.collected).map(i => ({ name: i.name, intel: i.intel, evType: i.evType })),
      contraband: con.filter(i => i.collected).map(i => ({ name: i.name, value: i.value })),
      hvtCaptured: M.hvt ? (M.hvt.restrained && !M.hvt.dead) : null, hvtName: M.hvt ? M.hvt.name : null,
      lieutenantCaptured: enemies.some(e => e.lieutenant && e.restrained && !e.dead),
      ops: opsL.map(o => ({ id: o.op.id, name: o.name, hp: o.hp, maxHp: o.maxHp, downed: o.downed, dead: o.dead, stable: o.stable, kills: o.kills, arrests: o.arrests, rescues: o.rescues, shots: o.shots, hits: o.hits, evidence: o.evidence, util: o.util.slice(), toolUses: o.toolUses, fired: o.weapons.map(w => ({ uid: w.inst.uid, fired: w.fired, cal: w.st.cal })) })),
      utilUsed: { flash: M.stats.flashes, smoke: M.stats.smokes, charge: M.stats.charges },
      roe: M.stats.roe, civKilledByOp: M.stats.civKilledByOp,
    };
    return M.result;
  }

  return {
    create, update, start, finish, refreshVisibility, objectives, get M() { return M; }, set M(v) { M = v; },
    canSee, los, visionPoly, visRange, findPath, tileAt, doorAt, toTile, ctr, walkable,
    orderMove, orderDoor, orderThrow, orderInteract, orderHold, orderFollow, orderFace, orderGo, go, orderCancel, setPace, toggleCrouch, orderCover,
    canAct, isThreat, curW, log, nearSide, doorSides,
  };
})();
