// Procedural multi-floor map generation
'use strict';
BP.T = { GROUND: 0, FLOOR: 1, WALL: 2, WINDOW: 3, DOOR: 4, FURN: 5, HIGH: 6, STAIRS: 7, BWIN: 8, FENCE: 9, VOID: 10, WATER: 11 };
BP.MAT = { grass: 0, asphalt: 1, dirt: 2, gravel: 3, sidewalk: 4, wood: 10, tile: 11, carpet: 12, concrete: 13, marble: 14, metal: 15, rock: 16 };
BP.FURN = { sofa: 1, table: 2, bed: 3, desk: 4, shelf: 5, counter: 6, crate: 7, cabinet: 8, lab: 9, barrel: 10, toilet: 11, car: 12, tree: 13, rack: 14, container: 15, hay: 16, chair: 17, plant: 18, safe: 19, bunk: 20, locker: 21, bush: 22 };

BP.mapgen = (() => {
  const T = BP.T, M = BP.MAT, F = BP.FURN;

  function makeMap(w, h, nf, ground, labels) {
    const floors = [];
    for (let f = 0; f < nf; f++) {
      floors.push({
        t: new Uint8Array(w * h).fill(f === 0 ? T.GROUND : T.VOID),
        mat: new Uint8Array(w * h).fill(f === 0 ? ground : M.concrete),
        furn: new Uint8Array(w * h), room: new Int16Array(w * h).fill(-1), door: new Int16Array(w * h).fill(-1),
      });
    }
    return { w, h, floors, doors: [], rooms: [], spawns: [], stairs: [], decor: [], buildings: [], labels: labels || floors.map((_, i) => i === 0 ? 'Ground' : i === 1 ? 'Upper' : 'Floor ' + (i + 1)), outdoorLight: true };
  }
  const I = (m, x, y) => y * m.w + x;
  const inb = (m, x, y) => x >= 0 && y >= 0 && x < m.w && y < m.h;
  const get = (m, f, x, y) => inb(m, x, y) ? m.floors[f].t[I(m, x, y)] : T.WALL;
  const set = (m, f, x, y, v) => { if (inb(m, x, y)) m.floors[f].t[I(m, x, y)] = v; };
  const setMat = (m, f, x, y, v) => { if (inb(m, x, y)) m.floors[f].mat[I(m, x, y)] = v; };
  const passable = t => t === T.GROUND || t === T.FLOOR || t === T.DOOR || t === T.STAIRS || t === T.BWIN;

  function fillRect(m, f, x, y, w, h, t, mat) {
    for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) { set(m, f, xx, yy, t); if (mat !== undefined) setMat(m, f, xx, yy, mat); }
  }

  function addDoor(m, f, x, y, opts = {}) {
    const passH = passable(get(m, f, x - 1, y)) || get(m, f, x - 1, y) === T.FURN;
    const orient = (passable(get(m, f, x, y - 1)) && passable(get(m, f, x, y + 1))) ? 'h' : (passH ? 'v' : 'v');
    const d = { id: m.doors.length, f, x, y, orient, state: 'closed', locked: !!opts.locked, barricaded: !!opts.barricaded, exterior: !!opts.exterior, hinge: Math.random() < 0.5 ? 1 : -1, open: 0, heavy: !!opts.heavy, knownLocked: false };
    m.doors.push(d);
    set(m, f, x, y, T.DOOR); m.floors[f].door[I(m, x, y)] = d.id;
    return d;
  }

  // BSP subdivision of a rectangular interior
  function bsp(m, f, x, y, w, h, minS, rng, depth, rooms, splits, maxArea) {
    const canV = w >= minS * 2 + 1, canH = h >= minS * 2 + 1;
    const area = w * h;
    if ((!canV && !canH) || (depth > 1 && area <= maxArea && rng.chance(0.55))) { rooms.push({ f, x, y, w, h }); return; }
    const vert = canV && (!canH || (w > h ? rng.chance(0.8) : w === h ? rng.chance(0.5) : rng.chance(0.2)));
    if (vert) {
      const p = rng.int(x + minS, x + w - 1 - minS);
      for (let yy = y; yy < y + h; yy++) set(m, f, p, yy, T.WALL);
      splits.push({ f, vert: true, p, a: y, b: y + h - 1 });
      bsp(m, f, x, y, p - x, h, minS, rng, depth + 1, rooms, splits, maxArea);
      bsp(m, f, p + 1, y, x + w - 1 - p, h, minS, rng, depth + 1, rooms, splits, maxArea);
    } else {
      const p = rng.int(y + minS, y + h - 1 - minS);
      for (let xx = x; xx < x + w; xx++) set(m, f, xx, p, T.WALL);
      splits.push({ f, vert: false, p, a: x, b: x + w - 1 });
      bsp(m, f, x, y, w, p - y, minS, rng, depth + 1, rooms, splits, maxArea);
      bsp(m, f, x, p + 1, w, y + h - 1 - p, minS, rng, depth + 1, rooms, splits, maxArea);
    }
  }
  function doorsForSplits(m, splits, rng, lockChance = 0.12) {
    for (const s of splits) {
      const cands = [];
      for (let q = s.a; q <= s.b; q++) {
        const [x, y] = s.vert ? [s.p, q] : [q, s.p];
        if (get(m, s.f, x, y) !== T.WALL) continue;
        const a = s.vert ? get(m, s.f, x - 1, y) : get(m, s.f, x, y - 1);
        const b = s.vert ? get(m, s.f, x + 1, y) : get(m, s.f, x, y + 1);
        if (a === T.FLOOR && b === T.FLOOR) cands.push([x, y]);
      }
      if (!cands.length) continue;
      const [x, y] = rng.pick(cands);
      const d = addDoor(m, s.f, x, y, { locked: rng.chance(lockChance) });
      if (rng.chance(0.35) && !d.locked) { d.state = 'open'; d.open = 1; }
    }
  }

  function flood(m, f, sx, sy) {
    const seen = new Uint8Array(m.w * m.h); const st = [[sx, sy]]; seen[I(m, sx, sy)] = 1; let n = 0;
    while (st.length) {
      const [x, y] = st.pop(); n++;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy; if (!inb(m, nx, ny)) continue;
        const k = I(m, nx, ny); if (seen[k]) continue;
        const t = m.floors[f].t[k];
        if (passable(t) || t === T.WINDOW) { seen[k] = 1; st.push([nx, ny]); }
      }
    }
    seen.count = n; return seen;
  }
  function ensureConnected(m, f, sx, sy, rect) {
    for (let iter = 0; iter < 60; iter++) {
      const seen = flood(m, f, sx, sy);
      let missing = false; const cands = [];
      const x0 = rect ? rect.x : 0, y0 = rect ? rect.y : 0, x1 = rect ? rect.x + rect.w : m.w, y1 = rect ? rect.y + rect.h : m.h;
      for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
        const k = I(m, x, y), t = m.floors[f].t[k];
        if ((t === T.FLOOR) && !seen[k]) missing = true;
        if (t !== T.WALL) continue;
        if (rect && (x === rect.x || y === rect.y || x === rect.x + rect.w - 1 || y === rect.y + rect.h - 1)) continue;
        for (const [dx, dy] of [[1, 0], [0, 1]]) {
          const ax = x - dx, ay = y - dy, bx = x + dx, by = y + dy;
          const ta = get(m, f, ax, ay), tb = get(m, f, bx, by);
          if (ta !== T.FLOOR || tb !== T.FLOOR) continue;
          const sa = seen[I(m, ax, ay)], sb = seen[I(m, bx, by)];
          if (sa !== sb) cands.push([x, y]);
        }
      }
      if (!missing || !cands.length) return;
      const [x, y] = cands[Math.floor(Math.random() * cands.length)];
      addDoor(m, f, x, y);
    }
  }

  function registerRooms(m, rooms, tagFn, matFn, rng, bId) {
    for (const r of rooms) {
      const id = m.rooms.length; r.id = id; r.building = bId;
      r.tag = tagFn ? tagFn(r, rng) : 'room';
      m.rooms.push(r);
      const mat = matFn ? matFn(r, rng) : M.wood;
      for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) {
        const k = I(m, x, y); if (m.floors[r.f].t[k] === T.FLOOR) { m.floors[r.f].room[k] = id; m.floors[r.f].mat[k] = mat; }
      }
    }
  }

  function outerShell(m, f, b, underground) {
    fillRect(m, f, b.x, b.y, b.w, b.h, T.WALL, M.concrete);
    fillRect(m, f, b.x + 1, b.y + 1, b.w - 2, b.h - 2, T.FLOOR, M.wood);
  }

  function exteriorOpenings(m, f, b, rng, { doors = 2, windows = 0.35, lockChance = 0.4, ground = true }) {
    const edge = [];
    for (let x = b.x + 1; x < b.x + b.w - 1; x++) { edge.push([x, b.y, 0, -1]); edge.push([x, b.y + b.h - 1, 0, 1]); }
    for (let y = b.y + 1; y < b.y + b.h - 1; y++) { edge.push([b.x, y, -1, 0]); edge.push([b.x + b.w - 1, y, 1, 0]); }
    const outsideOK = t => t === T.GROUND || t === T.VOID;
    if (ground && doors > 0) {
      const sides = rng.shuffle([[0, 1], [0, -1], [1, 0], [-1, 0]]);
      let placed = 0;
      for (const [sx, sy] of sides) {
        if (placed >= doors) break;
        const c = edge.filter(([x, y, dx, dy]) => dx === sx && dy === sy && get(m, f, x - dx, y - dy) === T.FLOOR && outsideOK(get(m, f, x + dx, y + dy)) && get(m, f, x, y) === T.WALL);
        if (!c.length) continue;
        const [x, y] = c[Math.floor(c.length * rng.range(0.25, 0.75))];
        addDoor(m, f, x, y, { exterior: true, locked: rng.chance(lockChance), barricaded: rng.chance(0.08) });
        placed++;
      }
    }
    if (windows > 0) {
      for (const [x, y, dx, dy] of edge) {
        if (get(m, f, x, y) !== T.WALL) continue;
        if (get(m, f, x - dx, y - dy) !== T.FLOOR) continue;
        if (!outsideOK(get(m, f, x + dx, y + dy))) continue;
        let near = false;
        for (let k = -1; k <= 1; k++) { const nx = x + (dy !== 0 ? k : 0), ny = y + (dx !== 0 ? k : 0); const t = get(m, f, nx, ny); if (t === T.DOOR || t === T.WINDOW) near = true; }
        if (!near && rng.chance(windows)) set(m, f, x, y, T.WINDOW);
      }
    }
  }

  function linkStairs(m, fLo, fHi, b, rng, forcePos) {
    let pos = forcePos;
    if (!pos) {
      const cands = [];
      for (let y = b.y + 2; y < b.y + b.h - 2; y++) for (let x = b.x + 2; x < b.x + b.w - 3; x++) {
        if (get(m, fLo, x, y) === T.FLOOR && get(m, fLo, x + 1, y) === T.FLOOR) {
          let bad = false;
          for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 2; dx++) { const t = get(m, fLo, x + dx, y + dy); if (t === T.DOOR || t === T.STAIRS || t === T.WINDOW) bad = true; }
          const tl = get(m, fLo, x - 1, y), tr = get(m, fLo, x + 2, y);
          if (!(tl === T.FLOOR || tr === T.FLOOR)) bad = true;
          if (!bad) cands.push([x, y]);
        }
      }
      pos = cands.length ? rng.pick(cands) : [b.x + 2, b.y + 2];
    }
    const [x, y] = pos;
    for (const f of [fLo, fHi]) {
      for (const xx of [x, x + 1]) { set(m, f, xx, y, T.STAIRS); m.floors[f].mat[I(m, xx, y)] = M.concrete; m.floors[f].furn[I(m, xx, y)] = 0; }
      // ensure access on hi floor
      const nbrs = [[x - 1, y], [x + 2, y], [x, y - 1], [x + 1, y - 1], [x, y + 1], [x + 1, y + 1]];
      if (!nbrs.some(([a, c]) => get(m, f, a, c) === T.FLOOR)) {
        for (const [a, c] of [[x - 1, y], [x + 2, y]]) if (a > b.x && a < b.x + b.w - 1) set(m, f, a, c, T.FLOOR);
      }
    }
    m.stairs.push({ x, y, f0: fLo, f1: fHi }, { x: x + 1, y, f0: fLo, f1: fHi });
    return [x, y];
  }

  // Furniture placement with connectivity preservation
  const FURN_SETS = {
    living: [F.sofa, F.sofa, F.table, F.cabinet, F.plant, F.chair], bedroom: [F.bed, F.bed, F.cabinet, F.desk, F.chair], kitchen: [F.counter, F.counter, F.counter, F.table, F.chair],
    office: [F.desk, F.desk, F.cabinet, F.chair, F.shelf, F.plant], bath: [F.toilet, F.counter], storage: [F.crate, F.crate, F.shelf, F.barrel, F.shelf],
    lab: [F.lab, F.lab, F.lab, F.barrel, F.counter, F.crate], cell: [F.bunk, F.toilet], guard: [F.desk, F.locker, F.locker, F.chair], vault: [F.safe, F.safe, F.safe, F.shelf],
    lobby: [F.plant, F.sofa, F.chair, F.table], hotel: [F.bed, F.bed, F.cabinet, F.chair, F.desk], bunker: [F.crate, F.locker, F.desk, F.shelf, F.barrel], shop: [F.shelf, F.shelf, F.counter],
    barn: [F.hay, F.hay, F.crate, F.barrel, F.hay], hall: [F.crate, F.barrel], room: [F.table, F.chair, F.cabinet],
  };
  function furnish(m, rng, refPoints) {
    for (const r of m.rooms) {
      const set_ = FURN_SETS[r.tag] || FURN_SETS.room;
      const n = Math.max(1, Math.round(r.w * r.h / (r.tag === 'cell' ? 4 : 9)));
      const cands = [];
      for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) {
        if (get(m, r.f, x, y) !== T.FLOOR) continue;
        let wallAdj = false, bad = false;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const t = get(m, r.f, x + dx, y + dy); if (t === T.WALL || t === T.WINDOW) wallAdj = true; if (t === T.DOOR || t === T.STAIRS) bad = true; }
        for (const [dx, dy] of [[1, 1], [-1, 1], [1, -1], [-1, -1]]) { const t = get(m, r.f, x + dx, y + dy); if (t === T.DOOR || t === T.STAIRS) bad = true; }
        if (!bad && (wallAdj || (r.w >= 5 && r.h >= 5 && rng.chance(0.15)))) cands.push([x, y]);
      }
      rng.shuffle(cands);
      let placed = 0;
      const ref = refPoints[r.f];
      for (const [x, y] of cands) {
        if (placed >= n) break;
        const before = ref ? flood(m, r.f, ref[0], ref[1]).count : 0;
        set(m, r.f, x, y, T.FURN);
        const after = ref ? flood(m, r.f, ref[0], ref[1]).count : 0;
        if (ref && after < before - 1) { set(m, r.f, x, y, T.FLOOR); continue; }
        m.floors[r.f].furn[I(m, x, y)] = rng.pick(set_);
        placed++;
      }
    }
  }

  // ---------- Building styles ----------
  function styleRooms(m, f, b, rng, { minS = 3, maxArea = 30, tags, mats } = {}) {
    const rooms = [], splits = [];
    bsp(m, f, b.x + 1, b.y + 1, b.w - 2, b.h - 2, minS, rng, 0, rooms, splits, maxArea);
    doorsForSplits(m, splits, rng);
    return rooms;
  }
  function styleCorridor(m, f, b, rng, { roomW = [4, 6], cellTag = 'hotel' } = {}) {
    const rooms = [];
    const horiz = b.w >= b.h;
    const ix = b.x + 1, iy = b.y + 1, iw = b.w - 2, ih = b.h - 2;
    if (horiz) {
      const cy = iy + Math.floor(ih / 2) - 1;
      for (let x = ix; x < ix + iw; x++) { set(m, f, x, cy - 1, T.WALL); set(m, f, x, cy + 2, T.WALL); }
      rooms.push({ f, x: ix, y: cy, w: iw, h: 2, tag: 'corridor' });
      for (const [ry, rh, dy] of [[iy, cy - 1 - iy, cy - 1], [cy + 3, iy + ih - (cy + 3), cy + 2]]) {
        let x = ix;
        while (x < ix + iw) {
          let w = rng.int(roomW[0], roomW[1]); if (ix + iw - (x + w) < roomW[0]) w = ix + iw - x;
          if (rh > 0) rooms.push({ f, x, y: ry, w, h: rh });
          if (x + w < ix + iw) for (let yy = ry; yy < ry + rh; yy++) set(m, f, x + w, yy, T.WALL);
          const dx = x + rng.int(0, Math.max(0, w - 1));
          if (rh > 0) { const d = addDoor(m, f, dx, dy, { locked: rng.chance(0.25) }); if (rng.chance(0.2) && !d.locked) { d.state = 'open'; d.open = 1; } }
          x += w + 1;
        }
      }
    } else {
      const cx = ix + Math.floor(iw / 2) - 1;
      for (let y = iy; y < iy + ih; y++) { set(m, f, cx - 1, y, T.WALL); set(m, f, cx + 2, y, T.WALL); }
      rooms.push({ f, x: cx, y: iy, w: 2, h: ih, tag: 'corridor' });
      for (const [rx, rw, dx] of [[ix, cx - 1 - ix, cx - 1], [cx + 3, ix + iw - (cx + 3), cx + 2]]) {
        let y = iy;
        while (y < iy + ih) {
          let h = rng.int(roomW[0], roomW[1]); if (iy + ih - (y + h) < roomW[0]) h = iy + ih - y;
          if (rw > 0) rooms.push({ f, x: rx, y, w: rw, h });
          if (y + h < iy + ih) for (let xx = rx; xx < rx + rw; xx++) set(m, f, xx, y + h, T.WALL);
          const dy = y + rng.int(0, Math.max(0, h - 1));
          if (rw > 0) { const d = addDoor(m, f, dx, dy, { locked: rng.chance(0.25) }); if (rng.chance(0.2) && !d.locked) { d.state = 'open'; d.open = 1; } }
          y += h + 1;
        }
      }
    }
    for (const r of rooms) if (!r.tag) r.tag = cellTag;
    return rooms;
  }
  function styleHall(m, f, b, rng, { officeDepth = 5, shelves = true, hallTag = 'hall' } = {}) {
    const rooms = [];
    // office strip along top wall
    const oy = b.y + 1, oh = officeDepth;
    for (let x = b.x + 1; x < b.x + b.w - 1; x++) set(m, f, x, oy + oh, T.WALL);
    const splits = [], orooms = [];
    bsp(m, f, b.x + 1, oy, b.w - 2, oh, 3, rng, 0, orooms, splits, 24);
    doorsForSplits(m, splits, rng);
    for (const r of orooms) { r.tag = rng.chance(0.6) ? 'office' : 'storage'; rooms.push(r); }
    // doors from offices to hall
    for (const r of orooms) {
      const dx = r.x + rng.int(0, r.w - 1);
      if (get(m, f, dx, oy + oh + 1) === T.FLOOR) addDoor(m, f, dx, oy + oh, { locked: rng.chance(0.15) });
    }
    const hall = { f, x: b.x + 1, y: oy + oh + 1, w: b.w - 2, h: b.h - 2 - oh - 1, tag: hallTag };
    rooms.push(hall);
    if (shelves) {
      // racks: rows of HIGH with aisles
      const rowGap = 4;
      for (let y = hall.y + 3; y < hall.y + hall.h - 3; y += rowGap) {
        let x = hall.x + 3;
        while (x < hall.x + hall.w - 4) {
          const len = rng.int(4, 8);
          if (x + len > hall.x + hall.w - 3) break;
          if (rng.chance(0.85)) for (let k = 0; k < len; k++) { set(m, f, x + k, y, T.HIGH); m.floors[f].furn[I(m, x + k, y)] = F.rack; }
          x += len + rng.int(2, 3);
        }
      }
      // scattered crates
      for (let i = 0; i < hall.w * hall.h / 30; i++) {
        const x = rng.int(hall.x + 1, hall.x + hall.w - 2), y = rng.int(hall.y + 1, hall.y + hall.h - 2);
        if (get(m, f, x, y) === T.FLOOR && get(m, f, x, y - 1) !== T.HIGH && get(m, f, x, y + 1) !== T.HIGH) { set(m, f, x, y, T.FURN); m.floors[f].furn[I(m, x, y)] = rng.pick([F.crate, F.crate, F.barrel]); }
      }
    }
    return rooms;
  }

  // Build a full building
  function building(m, b, floors, style, rng, o = {}) {
    const bId = m.buildings.length;
    m.buildings.push({ ...b, floors: floors.slice(), id: bId, name: o.name || '' });
    const allRooms = [];
    floors.forEach((f, i) => {
      outerShell(m, f, b);
      let rooms;
      if (style === 'rooms') rooms = styleRooms(m, f, b, rng, o);
      else if (style === 'corridor') rooms = styleCorridor(m, f, b, rng, o);
      else if (style === 'hall') rooms = i === 0 ? styleHall(m, f, b, rng, o) : styleRooms(m, f, b, rng, { minS: 3, maxArea: 30 });
      else if (style === 'open') { rooms = [{ f, x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2, tag: o.openTag || 'hall' }]; }
      else if (style === 'bank') rooms = i === 0 ? styleBank(m, f, b, rng) : styleRooms(m, f, b, rng, { minS: 3, maxArea: 28 });
      else if (style === 'shop') rooms = styleShop(m, f, b, rng);
      rooms.forEach(r => r.floorIdx = i);
      allRooms.push(...rooms);
    });
    // stairs
    for (let i = 0; i < floors.length - 1; i++) {
      const p = linkStairs(m, floors[i], floors[i + 1], b, rng);
      if (o.doubleStairs && b.w > 24) linkStairs(m, floors[i], floors[i + 1], b, rng);
    }
    // exterior openings
    floors.forEach((f, i) => exteriorOpenings(m, f, b, rng, { doors: i === 0 && !o.noExtDoors ? (o.extDoors || 2) : 0, windows: o.windows ?? (i === 0 ? 0.3 : 0.4), lockChance: o.lockChance ?? 0.4, ground: i === 0 }));
    // connectivity per floor
    floors.forEach((f, i) => {
      let sx = -1, sy = -1;
      const st = m.stairs.find(s => (s.f0 === f || s.f1 === f) && s.x > b.x && s.x < b.x + b.w && s.y > b.y && s.y < b.y + b.h);
      if (st) { sx = st.x; sy = st.y; }
      else { for (let y = b.y + 1; y < b.y + b.h - 1 && sx < 0; y++) for (let x = b.x + 1; x < b.x + b.w - 1; x++) if (get(m, f, x, y) === T.DOOR || get(m, f, x, y) === T.FLOOR) { sx = x; sy = y; break; } }
      if (sx >= 0) ensureConnected(m, f, sx, sy, b);
    });
    const tagFn = o.tagFn || ((r, rng) => {
      if (r.tag) return r.tag;
      const a = r.w * r.h;
      if (a <= 9) return rng.pick(['bath', 'storage']);
      if (a >= 30) return rng.pick(['living', 'kitchen', 'office']);
      return rng.pick(['bedroom', 'office', 'kitchen', 'living', 'storage']);
    });
    const matFn = o.matFn || ((r, rng) => ({ bath: M.tile, kitchen: M.tile, storage: M.concrete, hall: M.concrete, office: M.carpet, bedroom: M.carpet, living: M.wood, lab: M.tile, cell: M.concrete, corridor: o.corridorMat ?? M.carpet, hotel: M.carpet, vault: M.metal, lobby: M.marble, bunker: M.concrete, barn: M.dirt, shop: M.tile, guard: M.concrete }[r.tag] ?? (o.floorMat ?? M.wood)));
    registerRooms(m, allRooms, tagFn, matFn, rng, bId);
    return allRooms;
  }

  function styleBank(m, f, b, rng) {
    const rooms = [];
    const lobbyH = Math.floor((b.h - 2) * 0.48);
    const ly = b.y + b.h - 1 - lobbyH; // lobby is bottom part
    const lobby = { f, x: b.x + 1, y: ly, w: b.w - 2, h: lobbyH, tag: 'lobby' };
    rooms.push(lobby);
    // teller counter line with gap
    const cy = ly; const gap = b.x + 2 + rng.int(0, 2);
    for (let x = b.x + 1; x < b.x + b.w - 1; x++) { if (x === gap || x === gap + 1) continue; set(m, f, x, cy, T.FURN); m.floors[f].furn[I(m, x, cy)] = F.counter; }
    // back area
    const back = { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: cy - b.y - 1 };
    // teller strip directly behind counter (2 tiles), then wall
    const ty = cy - 2;
    for (let x = back.x; x < back.x + back.w; x++) set(m, f, x, ty - 1, T.WALL);
    rooms.push({ f, x: back.x, y: ty, w: back.w, h: 2, tag: 'office' });
    const splits = [], br = [];
    bsp(m, f, back.x, back.y, back.w, ty - 1 - back.y, 3, rng, 0, br, splits, 26);
    doorsForSplits(m, splits, rng, 0.2);
    // doors from back rooms to teller strip
    for (const r of br) { const dx = r.x + rng.int(0, r.w - 1); if (r.y + r.h === ty - 1 && get(m, f, dx, ty) === T.FLOOR) addDoor(m, f, dx, ty - 1, { locked: rng.chance(0.2) }); }
    // vault = largest back room furthest from lobby
    br.sort((a, c) => (c.w * c.h) - (a.w * a.h));
    if (br.length) br[0].tag = 'vault';
    for (const r of br) if (!r.tag) r.tag = rng.pick(['office', 'office', 'storage']);
    rooms.push(...br);
    return rooms;
  }
  function styleShop(m, f, b, rng) {
    const rooms = [];
    const backD = Math.max(3, Math.floor((b.h - 2) * 0.32));
    const wy = b.y + 1 + backD;
    for (let x = b.x + 1; x < b.x + b.w - 1; x++) set(m, f, x, wy, T.WALL);
    const front = { f, x: b.x + 1, y: wy + 1, w: b.w - 2, h: b.y + b.h - 2 - wy, tag: 'shop' };
    rooms.push(front);
    for (let y = front.y + 2; y < front.y + front.h - 2; y += 3) for (let x = front.x + 3; x < front.x + front.w - 4; x++) { set(m, f, x, y, T.FURN); m.floors[f].furn[I(m, x, y)] = F.shelf; }
    // counter near front corner
    for (let y = front.y + front.h - 3; y < front.y + front.h; y++) { set(m, f, front.x + 1, y, T.FURN); m.floors[f].furn[I(m, front.x + 1, y)] = F.counter; }
    const splits = [], br = [];
    bsp(m, f, b.x + 1, b.y + 1, b.w - 2, backD, 3, rng, 0, br, splits, 20);
    doorsForSplits(m, splits, rng);
    for (const r of br) { r.tag = rng.pick(['storage', 'office', 'bath']); rooms.push(r); }
    const r0 = br[0]; addDoor(m, f, r0.x + Math.floor(r0.w / 2), wy);
    return rooms;
  }

  // ---------- Outdoor helpers ----------
  function road(m, y, h) { fillRect(m, 0, 0, y, m.w, h, T.GROUND, M.asphalt); fillRect(m, 0, 0, y - 1, m.w, 1, T.GROUND, M.sidewalk); }
  function parkCar(m, x, y, horiz, rng, color) {
    const w = horiz ? 4 : 2, h = horiz ? 2 : 4;
    for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) if (get(m, 0, xx, yy) !== T.GROUND) return;
    for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) { set(m, 0, xx, yy, T.FURN); m.floors[0].furn[I(m, xx, yy)] = F.car; }
    m.decor.push({ type: 'car', x, y, w, h, color: color || rng.pick(['#7a1f1f', '#1f3f7a', '#cccccc', '#222', '#4a4a4a', '#6a5a2a', '#2f5a3a']) });
  }
  function scatter(m, n, furn, tile, rng, avoidRects, area) {
    for (let i = 0; i < n * 4 && n > 0; i++) {
      const x = rng.int(area ? area.x : 1, area ? area.x + area.w - 1 : m.w - 2), y = rng.int(area ? area.y : 1, area ? area.y + area.h - 1 : m.h - 2);
      if (get(m, 0, x, y) !== T.GROUND) continue;
      if (avoidRects.some(r => x >= r.x - 2 && x < r.x + r.w + 2 && y >= r.y - 2 && y < r.y + r.h + 2)) continue;
      set(m, 0, x, y, tile); m.floors[0].furn[I(m, x, y)] = furn; n--;
    }
  }
  function fence(m, r, gates, rng) {
    for (let x = r.x; x < r.x + r.w; x++) { set(m, 0, x, r.y, T.FENCE); set(m, 0, x, r.y + r.h - 1, T.FENCE); }
    for (let y = r.y; y < r.y + r.h; y++) { set(m, 0, r.x, y, T.FENCE); set(m, 0, r.x + r.w - 1, y, T.FENCE); }
    for (const g of gates) for (let k = 0; k < g.len; k++) set(m, 0, g.x + (g.horiz ? k : 0), g.y + (g.horiz ? 0 : k), T.GROUND);
  }
  function addSpawns(m, rng, sides) {
    const names = ['Alpha', 'Bravo', 'Charlie', 'Delta'];
    const S = sides || ['s', 'w', 'e', 'n'];
    const pts = { s: [Math.floor(m.w / 2), m.h - 3, -Math.PI / 2], n: [Math.floor(m.w / 2), 2, Math.PI / 2], w: [2, Math.floor(m.h / 2), 0], e: [m.w - 3, Math.floor(m.h / 2), Math.PI] };
    let i = 0;
    for (const s of S) {
      let [x, y, ang] = pts[s];
      // nudge to nearest ground tile
      let best = null, bd = 1e9;
      for (let yy = 1; yy < m.h - 1; yy++) for (let xx = 1; xx < m.w - 1; xx++) {
        if (get(m, 0, xx, yy) !== T.GROUND) continue;
        const d = Math.hypot(xx - x, yy - y); if (d < bd) { bd = d; best = [xx, yy]; }
      }
      if (best && bd < 8) { m.spawns.push({ name: names[i++], x: best[0], y: best[1], ang, side: s }); }
      if (i >= 3) break;
    }
  }

  // ---------- Map types ----------
  function generate(type, seed, opts = {}) {
    const rng = BP.util.rng(seed);
    let m;
    const fl = opts.floors;
    switch (type) {
      case 'house': case 'house2': {
        const nf = fl || (type === 'house2' ? 2 : rng.chance(0.4) ? 2 : 1);
        const bw = rng.int(18, 23), bh = rng.int(13, 16);
        m = makeMap(bw + 14, bh + 15, nf, M.grass);
        road(m, m.h - 4, 4);
        const b = { x: 7, y: 5, w: bw, h: bh };
        building(m, b, [...Array(nf).keys()], 'rooms', rng, { minS: 3, maxArea: 28, extDoors: 2, tagFn: type === 'house2' ? ((r, rg) => r.w * r.h <= 9 ? 'bath' : rg.chance(0.45) ? 'lab' : rg.pick(['living', 'kitchen', 'bedroom', 'storage'])) : null });
        fillRect(m, 0, b.x + 3, b.y + b.h, 3, m.h - 4 - b.y - b.h, T.GROUND, M.asphalt); // driveway
        fillRect(m, 0, b.x + Math.floor(b.w / 2), b.y + b.h, 1, m.h - 5 - b.y - b.h, T.GROUND, M.sidewalk);
        parkCar(m, b.x + 3, b.y + b.h + 2, false, rng);
        parkCar(m, rng.int(2, m.w - 8), m.h - 3, true, rng);
        scatter(m, 6, F.bush, T.FURN, rng, [b]); scatter(m, 4, F.tree, T.HIGH, rng, [b]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'warehouse': {
        const small = opts.small;
        const bw = small ? rng.int(24, 28) : rng.int(32, 38), bh = small ? rng.int(18, 20) : rng.int(22, 26);
        m = makeMap(bw + 16, bh + 14, 1, M.asphalt);
        const b = { x: 8, y: 6, w: bw, h: bh };
        building(m, b, [0], 'hall', rng, { officeDepth: 5, extDoors: 3, windows: 0.15 });
        for (let i = 0; i < 3; i++) parkCar(m, b.x + 2 + i * 6, b.y + b.h + 3, false, rng, '#ddd');
        scatter(m, 10, F.crate, T.FURN, rng, [b]); scatter(m, 5, F.container, T.HIGH, rng, [b]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'compound': {
        m = makeMap(66, 50, 2, M.dirt, ['Ground', 'Upper']);
        fillRect(m, 0, 0, 0, m.w, m.h, T.GROUND, M.grass);
        const yard = { x: 4, y: 4, w: m.w - 8, h: m.h - 8 };
        fillRect(m, 0, yard.x, yard.y, yard.w, yard.h, T.GROUND, M.gravel);
        fence(m, yard, [{ x: Math.floor(m.w / 2) - 2, y: yard.y + yard.h - 1, len: 4, horiz: true }, { x: yard.x, y: Math.floor(m.h / 2) - 1, len: 3, horiz: false }, { x: yard.x + yard.w - 1, y: 12, len: 3, horiz: false }], rng);
        const main = { x: 22, y: 8, w: 24, h: 16 };
        building(m, main, [0, 1], 'rooms', rng, { minS: 3, maxArea: 30, extDoors: 3, name: 'Main Building' });
        const shed1 = { x: 8, y: 9, w: 10, h: 9 }, shed2 = { x: 48, y: 28, w: 12, h: 10 }, gar = { x: 10, y: 28, w: 16, h: 11 };
        building(m, shed1, [0], 'rooms', rng, { minS: 3, maxArea: 20, extDoors: 1, name: 'Guard House' });
        building(m, shed2, [0], 'rooms', rng, { minS: 3, maxArea: 24, extDoors: 2, name: 'Workshop' });
        building(m, gar, [0], 'open', rng, { extDoors: 2, openTag: 'storage', name: 'Garage', windows: 0.1 });
        const bs = [main, shed1, shed2, gar];
        for (let i = 0; i < 4; i++) parkCar(m, rng.int(30, 44), rng.int(28, 40), rng.chance(0.5), rng, rng.pick(['#ddd', '#333', '#5a5a2a']));
        scatter(m, 18, F.crate, T.FURN, rng, bs, yard); scatter(m, 8, F.barrel, T.FURN, rng, bs, yard); scatter(m, 4, F.container, T.HIGH, rng, bs, yard);
        scatter(m, 14, F.tree, T.HIGH, rng, [yard]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'bank': {
        const nf = 2;
        const bw = rng.int(28, 32), bh = rng.int(19, 22);
        m = makeMap(bw + 14, bh + 15, nf, M.sidewalk);
        road(m, m.h - 5, 5);
        const b = { x: 7, y: 5, w: bw, h: bh };
        building(m, b, [0, 1], 'bank', rng, { extDoors: 2, windows: 0.5, lockChance: 0.5 });
        // vault door heavy & locked
        const vault = m.rooms.find(r => r.tag === 'vault');
        if (vault) for (const d of m.doors) if (d.f === 0 && Math.abs(d.x - (vault.x + vault.w / 2)) <= vault.w / 2 + 1 && Math.abs(d.y - (vault.y + vault.h / 2)) <= vault.h / 2 + 1) { d.locked = true; d.heavy = true; d.state = 'closed'; d.open = 0; }
        for (let i = 0; i < 4; i++) parkCar(m, 2 + i * 9, m.h - 4, true, rng);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'hangar': {
        const bw = rng.int(40, 44), bh = rng.int(24, 28);
        m = makeMap(bw + 18, bh + 18, 1, M.asphalt);
        const b = { x: 9, y: 6, w: bw, h: bh };
        building(m, b, [0], 'hall', rng, { officeDepth: 6, extDoors: 3, windows: 0.1 });
        // big hangar door: open wide gap at bottom
        const gx = b.x + Math.floor(b.w / 2) - 3;
        for (let k = 0; k < 6; k++) set(m, 0, gx + k, b.y + b.h - 1, T.FLOOR);
        for (let i = 0; i < 6; i++) { const x = rng.int(4, m.w - 8), y = rng.int(b.y + b.h + 3, m.h - 5); if (get(m, 0, x, y) === T.GROUND) { for (let k = 0; k < 3; k++) if (get(m, 0, x + k, y) === T.GROUND) { set(m, 0, x + k, y, T.HIGH); m.floors[0].furn[I(m, x + k, y)] = F.container; } } }
        scatter(m, 12, F.crate, T.FURN, rng, [b]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'hotel': {
        const nf = fl || 3;
        const bw = rng.int(32, 36), bh = rng.int(15, 17);
        m = makeMap(bw + 14, bh + 15, nf, M.sidewalk, ['Lobby', '2nd Floor', '3rd Floor']);
        road(m, m.h - 5, 5);
        const b = { x: 7, y: 5, w: bw, h: bh };
        building(m, b, [...Array(nf).keys()], 'corridor', rng, { roomW: [4, 6], cellTag: 'hotel', extDoors: 2, windows: 0.45, doubleStairs: true });
        for (let i = 0; i < 4; i++) parkCar(m, 2 + i * 10, m.h - 4, true, rng);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'prison': {
        const bw = rng.int(34, 38), bh = rng.int(16, 18);
        m = makeMap(bw + 14, bh + 16, 2, M.gravel, ['Block A', 'Block A — Upper']);
        const yard = { x: 2, y: 2, w: m.w - 4, h: m.h - 4 };
        fence(m, yard, [{ x: Math.floor(m.w / 2) - 2, y: yard.y + yard.h - 1, len: 4, horiz: true }, { x: yard.x, y: Math.floor(m.h / 2), len: 2, horiz: false }], rng);
        const b = { x: 7, y: 6, w: bw, h: bh };
        building(m, b, [0, 1], 'corridor', rng, { roomW: [3, 4], cellTag: 'cell', extDoors: 2, windows: 0.1, corridorMat: M.concrete, lockChance: 0.6 });
        m.rooms.filter(r => r.tag === 'cell').slice(0, 2).forEach(r => r.tag = 'guard');
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'store': {
        const bw = rng.int(15, 18), bh = rng.int(12, 14);
        m = makeMap(bw + 16, bh + 14, 1, M.sidewalk);
        road(m, m.h - 5, 5);
        const b = { x: 8, y: 4, w: bw, h: bh };
        building(m, b, [0], 'shop', rng, { extDoors: 2, windows: 0.6, lockChance: 0.2 });
        parkCar(m, 3, m.h - 4, true, rng); parkCar(m, m.w - 8, m.h - 4, true, rng);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'mansion': {
        const bw = rng.int(30, 34), bh = rng.int(20, 23);
        m = makeMap(bw + 22, bh + 22, 2, M.grass);
        const yard = { x: 3, y: 3, w: m.w - 6, h: m.h - 6 };
        fence(m, yard, [{ x: Math.floor(m.w / 2) - 2, y: yard.y + yard.h - 1, len: 4, horiz: true }, { x: yard.x, y: Math.floor(m.h / 2), len: 3, horiz: false }, { x: yard.x + yard.w - 1, y: Math.floor(m.h / 2), len: 3, horiz: false }], rng);
        const b = { x: 11, y: 8, w: bw, h: bh };
        building(m, b, [0, 1], 'rooms', rng, { minS: 4, maxArea: 42, extDoors: 3, windows: 0.5, floorMat: M.marble, doubleStairs: true });
        // pool
        fillRect(m, 0, b.x + 4, b.y + b.h + 3, 10, 4, T.WATER, M.tile);
        fillRect(m, 0, Math.floor(m.w / 2) - 1, b.y + b.h, 3, m.h - b.y - b.h - 3, T.GROUND, M.gravel);
        parkCar(m, Math.floor(m.w / 2) + 4, b.y + b.h + 4, false, rng, '#111'); parkCar(m, Math.floor(m.w / 2) + 7, b.y + b.h + 4, false, rng, '#ddd');
        scatter(m, 14, F.tree, T.HIGH, rng, [b], yard); scatter(m, 14, F.bush, T.FURN, rng, [b], yard);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'shipyard': {
        m = makeMap(62, 44, 1, M.asphalt);
        fillRect(m, 0, 0, 0, m.w, 5, T.WATER, M.concrete);
        fillRect(m, 0, 0, 5, m.w, 1, T.GROUND, M.concrete);
        const office = { x: 44, y: 28, w: 14, h: 10 };
        building(m, office, [0], 'rooms', rng, { minS: 3, maxArea: 20, extDoors: 2, windows: 0.4, name: 'Dock Office' });
        const cols = ['#8a3b2b', '#2b5a8a', '#3b7a3b', '#9a7a2a', '#5a5a5a', '#6a2a6a'];
        for (let cy = 8; cy < 38; cy += 5) for (let cx = 4; cx < 40; cx += 9) {
          if (rng.chance(0.25)) continue;
          const c = rng.pick(cols), len = rng.int(5, 7);
          for (let k = 0; k < len; k++) for (let j = 0; j < 2; j++) { if (get(m, 0, cx + k, cy + j) === T.GROUND) { set(m, 0, cx + k, cy + j, T.HIGH); m.floors[0].furn[I(m, cx + k, cy + j)] = F.container; } }
          m.decor.push({ type: 'container', x: cx, y: cy, w: len, h: 2, color: c });
        }
        scatter(m, 14, F.crate, T.FURN, rng, [office]); scatter(m, 6, F.barrel, T.FURN, rng, [office]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'rural': {
        m = makeMap(64, 48, 2, M.grass, ['Ground', 'Upper']);
        fillRect(m, 0, 30, 0, 3, m.h, T.GROUND, M.dirt);
        const house = { x: 12, y: 8, w: 16, h: 12 }, barn = { x: 38, y: 7, w: 17, h: 13 }, shed = { x: 40, y: 30, w: 8, h: 7 };
        building(m, house, [0, 1], 'rooms', rng, { minS: 3, maxArea: 22, extDoors: 2, name: 'Farmhouse' });
        building(m, barn, [0], 'open', rng, { extDoors: 2, openTag: 'barn', name: 'Barn', windows: 0.05 });
        building(m, shed, [0], 'rooms', rng, { minS: 3, maxArea: 20, extDoors: 1, name: 'Shed' });
        for (let i = 0; i < 6; i++) { const x = rng.int(barn.x + 2, barn.x + barn.w - 3), y = rng.int(barn.y + 2, barn.y + barn.h - 3); if (get(m, 0, x, y) === T.FLOOR) { set(m, 0, x, y, T.FURN); m.floors[0].furn[I(m, x, y)] = F.hay; } }
        parkCar(m, 32, 24, false, rng, '#5a3a2a'); parkCar(m, 22, 24, true, rng, '#2a3a5a');
        scatter(m, 40, F.tree, T.HIGH, rng, [house, barn, shed]); scatter(m, 20, F.bush, T.FURN, rng, [house, barn, shed]);
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'bunker': {
        m = makeMap(54, 42, 2, M.dirt, ['Surface', 'Bunker B1']);
        fillRect(m, 0, 0, 0, m.w, m.h, T.GROUND, M.grass);
        fillRect(m, 1, 0, 0, m.w, m.h, T.WALL, M.rock);
        const cabin = { x: 22, y: 14, w: 10, h: 8 };
        building(m, cabin, [0], 'rooms', rng, { minS: 3, maxArea: 30, extDoors: 2, name: 'Hunting Cabin' });
        const bk = { x: 6, y: 5, w: 42, h: 32 };
        building(m, bk, [1], 'rooms', rng, { minS: 4, maxArea: 36, noExtDoors: true, windows: 0, floorMat: M.concrete, tagFn: (r, rg) => r.w * r.h > 36 ? 'bunker' : rg.pick(['bunker', 'office', 'storage', 'guard']), name: 'Bunker' });
        // stairs from cabin to bunker
        let pos = null;
        for (let y = cabin.y + 2; y < cabin.y + cabin.h - 2 && !pos; y++) for (let x = cabin.x + 1; x < cabin.x + cabin.w - 3; x++) if (get(m, 0, x, y) === T.FLOOR && get(m, 0, x + 1, y) === T.FLOOR) { pos = [x, y]; break; }
        linkStairs(m, 0, 1, cabin, rng, pos);
        ensureConnected(m, 1, pos[0], pos[1], bk);
        // second hidden hatch in forest
        scatter(m, 60, F.tree, T.HIGH, rng, [cabin]); scatter(m, 25, F.bush, T.FURN, rng, [cabin]);
        m.outdoorLight = false;
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      case 'training': {
        m = makeMap(34, 26, 1, M.concrete);
        const b = { x: 6, y: 4, w: 22, h: 15 };
        building(m, b, [0], 'rooms', rng, { minS: 3, maxArea: 22, extDoors: 2, lockChance: 0.2, floorMat: M.concrete, matFn: () => M.concrete, tagFn: () => 'room' });
        addSpawns(m, rng, ['s', 'w', 'e']);
        break;
      }
      default: return generate('house', seed, opts);
    }
    // ensure ground floor reachability from first spawn (fix blocked spawn pockets)
    // furniture (after connectivity)
    const refs = {};
    for (let f = 0; f < m.floors.length; f++) {
      const st = m.stairs.find(s => s.f0 === f || s.f1 === f);
      if (f === 0 && m.spawns[0]) refs[0] = [m.spawns[0].x, m.spawns[0].y];
      else if (st) refs[f] = [st.x, st.y];
    }
    furnish(m, rng, refs);
    m.type = type; m.seed = seed;
    return m;
  }

  return { generate, I, get, passable, flood };
})();
