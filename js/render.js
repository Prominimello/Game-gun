// Mission renderer: static floor layers, extruded walls, fog of war, particles & decals
'use strict';
BP.render = (() => {
  const T = BP.T, TS = BP.TS, MAT = BP.MAT, F = BP.FURN, U = BP.util, art = BP.art;
  const layers = new WeakMap();

  // ---------- static layers ----------
  function getLayers(map) {
    let L = layers.get(map);
    if (!L) {
      L = { floors: map.floors.map((_, f) => ({ base: null, decal: null })), wallCache: [] };
      layers.set(map, L);
    }
    return L;
  }
  function makeCanvas(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }

  const MATCOL = { [MAT.grass]: '#36502a', [MAT.asphalt]: '#2c2e31', [MAT.dirt]: '#5a4a35', [MAT.gravel]: '#66625a', [MAT.sidewalk]: '#80807b', [MAT.wood]: '#6e4c2f', [MAT.tile]: '#9ea6a8', [MAT.carpet]: '#4a3e46', [MAT.concrete]: '#62666a', [MAT.marble]: '#cdc8be', [MAT.metal]: '#4b5155', [MAT.rock]: '#1d1b19' };
  function drawFloorTile(ctx, map, f, x, y, mat, rng) {
    const px = x * TS, py = y * TS;
    let c = MATCOL[mat] || '#555';
    const fl = map.floors[f];
    const room = fl.room[y * map.w + x];
    if (mat === MAT.carpet && room >= 0) c = ['#4a3e46', '#3c4656', '#4c4a3a', '#3a4a40', '#5a3a36'][room % 5];
    if (mat === MAT.wood && room >= 0) c = ['#6e4c2f', '#7a5634', '#5e4128'][room % 3];
    ctx.fillStyle = c; ctx.fillRect(px, py, TS, TS);
    switch (mat) {
      case MAT.grass: for (let i = 0; i < 10; i++) { ctx.fillStyle = rng() < 0.5 ? 'rgba(80,110,50,0.5)' : 'rgba(30,45,20,0.5)'; ctx.fillRect(px + rng() * TS, py + rng() * TS, 2, 3); } break;
      case MAT.asphalt: for (let i = 0; i < 8; i++) { ctx.fillStyle = rng() < 0.5 ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.15)'; ctx.fillRect(px + rng() * TS, py + rng() * TS, 2, 2); } break;
      case MAT.dirt: case MAT.gravel: for (let i = 0; i < 12; i++) { ctx.fillStyle = rng() < 0.5 ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.18)'; ctx.beginPath(); ctx.arc(px + rng() * TS, py + rng() * TS, rng() * 1.8 + 0.4, 0, 7); ctx.fill(); } break;
      case MAT.sidewalk: ctx.strokeStyle = 'rgba(0,0,0,0.18)'; ctx.strokeRect(px + 0.5, py + 0.5, TS - 1, TS - 1); break;
      case MAT.wood: ctx.fillStyle = 'rgba(0,0,0,0.18)'; for (let i = 0; i < 4; i++) ctx.fillRect(px, py + i * 8, TS, 1); ctx.fillStyle = 'rgba(255,255,255,0.04)'; ctx.fillRect(px + ((x * 7 + y * 3) % 4) * 8, py, 1, TS); break;
      case MAT.tile: ctx.fillStyle = (x + y) % 2 ? 'rgba(0,0,0,0.06)' : 'rgba(255,255,255,0.05)'; ctx.fillRect(px, py, TS, TS); ctx.strokeStyle = 'rgba(0,0,0,0.15)'; ctx.strokeRect(px + 0.5, py + 0.5, 15, 15); ctx.strokeRect(px + 16.5, py + 16.5, 15, 15); break;
      case MAT.carpet: for (let i = 0; i < 6; i++) { ctx.fillStyle = 'rgba(255,255,255,0.03)'; ctx.fillRect(px + rng() * TS, py + rng() * TS, 3, 3); } break;
      case MAT.concrete: ctx.fillStyle = 'rgba(0,0,0,0.06)'; ctx.fillRect(px + rng() * 20, py + rng() * 20, 12, 8); ctx.strokeStyle = 'rgba(0,0,0,0.1)'; if ((x + y) % 4 === 0) ctx.strokeRect(px + 0.5, py + 0.5, TS * 2, TS * 2); break;
      case MAT.marble: ctx.strokeStyle = 'rgba(120,110,100,0.25)'; ctx.beginPath(); ctx.moveTo(px + rng() * TS, py); ctx.quadraticCurveTo(px + rng() * TS, py + TS / 2, px + rng() * TS, py + TS); ctx.stroke(); ctx.strokeStyle = 'rgba(0,0,0,0.08)'; ctx.strokeRect(px + 0.5, py + 0.5, TS - 1, TS - 1); break;
      case MAT.metal: ctx.strokeStyle = 'rgba(255,255,255,0.06)'; for (let i = 0; i < TS; i += 8) { ctx.beginPath(); ctx.moveTo(px + i, py); ctx.lineTo(px + i + 4, py + 4); ctx.stroke(); } break;
      case MAT.rock: for (let i = 0; i < 6; i++) { ctx.fillStyle = 'rgba(255,255,255,0.03)'; ctx.beginPath(); ctx.arc(px + rng() * TS, py + rng() * TS, rng() * 5, 0, 7); ctx.fill(); } break;
    }
  }
  function drawFurn(ctx, type, x, y, rng, map, f) {
    const px = x * TS, py = y * TS, cx = px + TS / 2, cy = py + TS / 2;
    const R = (c, a, b, w, h, r = 2) => { ctx.fillStyle = c; art.rr(ctx, px + a, py + b, w, h, r); ctx.fill(); };
    ctx.save();
    ctx.fillStyle = 'rgba(0,0,0,0.3)'; ctx.fillRect(px + 4, py + 5, TS - 4, TS - 4);
    switch (type) {
      case F.sofa: { const col = ['#5a4a6a', '#6a3a2a', '#3a4a5a', '#4a5a3a'][Math.floor(rng() * 4)]; R(col, 2, 3, 28, 26, 5); R(U.shade(col, 0.12), 6, 7, 20, 18, 3); R(U.shade(col, -0.2), 2, 3, 28, 6, 3); break; }
      case F.table: R('#6a4a2a', 3, 3, 26, 26, 3); R('#7d5a35', 5, 5, 22, 22, 2); ctx.fillStyle = '#ddd'; ctx.beginPath(); ctx.arc(cx - 4, cy, 3, 0, 7); ctx.fill(); break;
      case F.bed: R('#e0ddd5', 2, 2, 28, 28, 3); R(['#4a5a8a', '#8a4a4a', '#4a7a5a', '#7a6a4a'][Math.floor(rng() * 4)], 2, 12, 28, 18, 2); R('#fff', 6, 4, 20, 6, 3); break;
      case F.desk: R('#5a3e26', 2, 4, 28, 22, 2); R('#1a1c20', 8, 6, 14, 4, 1); R('#3a7ab0', 9, 6.5, 12, 3, 0); R('#ddd', 18, 14, 8, 10, 0); break;
      case F.shelf: R('#555a5e', 2, 6, 28, 20, 1); for (let i = 0; i < 5; i++) R(['#c84', '#48c', '#c44', '#8a4', '#ccc'][Math.floor(rng() * 5)], 4 + i * 5, 9 + (i % 2) * 7, 4, 6, 0.5); break;
      case F.counter: R('#a9adb0', 1, 1, 30, 30, 1); R('#c7cacc', 3, 3, 26, 26, 1); if (rng() < 0.3) { ctx.fillStyle = '#8a9296'; ctx.beginPath(); ctx.arc(cx, cy, 7, 0, 7); ctx.fill(); } break;
      case F.crate: R('#7a5a32', 3, 3, 26, 26, 1); ctx.strokeStyle = '#4e3a1e'; ctx.lineWidth = 2; ctx.strokeRect(px + 4, py + 4, 24, 24); ctx.beginPath(); ctx.moveTo(px + 4, py + 4); ctx.lineTo(px + 28, py + 28); ctx.moveTo(px + 28, py + 4); ctx.lineTo(px + 4, py + 28); ctx.stroke(); break;
      case F.cabinet: R('#3e2c1c', 2, 4, 28, 22, 1); ctx.fillStyle = '#6a5038'; ctx.fillRect(px + 15, py + 6, 1, 18); ctx.fillStyle = '#ccc'; ctx.fillRect(px + 12, py + 14, 2, 3); ctx.fillRect(px + 18, py + 14, 2, 3); break;
      case F.lab: R('#8a9298', 1, 2, 30, 26, 1); for (let i = 0; i < 4; i++) { ctx.fillStyle = ['#3c6', '#c93', '#39c', '#ddd', '#c3c'][Math.floor(rng() * 5)]; ctx.globalAlpha = 0.8; ctx.beginPath(); ctx.arc(px + 7 + rng() * 18, py + 7 + rng() * 14, 2.5 + rng() * 2, 0, 7); ctx.fill(); } ctx.globalAlpha = 1; R('#eee', 4, 20, 10, 6, 0); break;
      case F.barrel: { const col = ['#2a4a7a', '#7a2a2a', '#4a5a2a', '#555'][Math.floor(rng() * 4)]; ctx.fillStyle = col; ctx.beginPath(); ctx.arc(cx, cy, 12, 0, 7); ctx.fill(); ctx.strokeStyle = U.shade(col, 0.25); ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(cx, cy, 9, 0, 7); ctx.stroke(); ctx.fillStyle = '#111'; ctx.beginPath(); ctx.arc(cx + 4, cy - 3, 2, 0, 7); ctx.fill(); break; }
      case F.toilet: ctx.fillStyle = '#f0f0ee'; ctx.beginPath(); ctx.ellipse(cx, cy + 3, 7, 9, 0, 0, 7); ctx.fill(); R('#f0f0ee', 8, 4, 16, 6, 2); ctx.fillStyle = '#bcd'; ctx.beginPath(); ctx.ellipse(cx, cy + 4, 4, 6, 0, 0, 7); ctx.fill(); break;
      case F.tree: { ctx.fillStyle = 'rgba(0,0,0,0.3)'; ctx.beginPath(); ctx.arc(cx + 5, cy + 6, 20, 0, 7); ctx.fill(); const g = ['#23401e', '#2c4a22', '#1e3a1a'][Math.floor(rng() * 3)]; for (let i = 0; i < 6; i++) { ctx.fillStyle = i < 3 ? g : U.shade(g, 0.15); ctx.beginPath(); ctx.arc(cx + (rng() - 0.5) * 16, cy + (rng() - 0.5) * 16, 10 + rng() * 6, 0, 7); ctx.fill(); } break; }
      case F.bush: { const g = '#2f4f24'; for (let i = 0; i < 4; i++) { ctx.fillStyle = i % 2 ? g : U.shade(g, 0.12); ctx.beginPath(); ctx.arc(cx + (rng() - 0.5) * 12, cy + (rng() - 0.5) * 12, 7 + rng() * 4, 0, 7); ctx.fill(); } break; }
      case F.rack: R('#3e4a56', 0, 4, 32, 24, 0); ctx.strokeStyle = '#7a8894'; ctx.lineWidth = 2; ctx.strokeRect(px + 1, py + 5, 30, 22); for (let i = 0; i < 2; i++) R(['#b08a5a', '#9a7448', '#c0a070'][Math.floor(rng() * 3)], 3 + i * 14, 8, 12, 16, 1); break;
      case F.container: R('#6a6a6a', 0, 0, 32, 32, 0); break;
      case F.hay: R('#c8a84a', 2, 4, 28, 24, 2); ctx.strokeStyle = 'rgba(120,90,30,0.6)'; for (let i = 0; i < 6; i++) { ctx.beginPath(); ctx.moveTo(px + 4 + i * 4, py + 6); ctx.lineTo(px + 6 + i * 4, py + 26); ctx.stroke(); } break;
      case F.chair: R('#3a3a3a', 9, 9, 14, 14, 3); R('#2a2a2a', 9, 7, 14, 4, 1); break;
      case F.plant: ctx.fillStyle = '#6a4a3a'; ctx.beginPath(); ctx.arc(cx, cy, 8, 0, 7); ctx.fill(); for (let i = 0; i < 7; i++) { ctx.fillStyle = i % 2 ? '#3a7a3a' : '#2a6a2a'; ctx.beginPath(); ctx.ellipse(cx + Math.cos(i) * 6, cy + Math.sin(i) * 6, 6, 3, i, 0, 7); ctx.fill(); } break;
      case F.safe: R('#2e3236', 3, 3, 26, 26, 2); R('#4a5056', 5, 5, 22, 22, 1); ctx.fillStyle = '#aaa'; ctx.beginPath(); ctx.arc(cx, cy, 5, 0, 7); ctx.fill(); break;
      case F.bunk: R('#777', 3, 2, 26, 28, 1); R('#9a9a8a', 5, 4, 22, 24, 1); R('#ccc', 7, 5, 18, 5, 2); break;
      case F.locker: for (let i = 0; i < 3; i++) R('#5a6670', 2 + i * 10, 6, 9, 20, 1); ctx.fillStyle = '#222'; for (let i = 0; i < 3; i++) ctx.fillRect(px + 4 + i * 10, py + 10, 5, 1); break;
      default: R('#555', 4, 4, 24, 24, 2);
    }
    ctx.restore();
  }
  function buildBase(map, f) {
    const c = makeCanvas(map.w * TS, map.h * TS), ctx = c.getContext('2d');
    const fl = map.floors[f];
    const rng = U.rng(map.seed * 7 + f + 1);
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) {
      const k = y * map.w + x, t = fl.t[k];
      if (t === T.VOID) continue;
      if (t === T.WALL && fl.mat[k] === MAT.rock) { drawFloorTile(ctx, map, f, x, y, MAT.rock, rng); continue; }
      drawFloorTile(ctx, map, f, x, y, fl.mat[k], rng);
      if (t === T.WATER) { ctx.fillStyle = f === 0 && map.type === 'shipyard' ? '#1d3a52' : '#3a8ab8'; ctx.fillRect(x * TS, y * TS, TS, TS); ctx.strokeStyle = 'rgba(255,255,255,0.12)'; ctx.beginPath(); ctx.moveTo(x * TS + rng() * 10, y * TS + 10 + rng() * 10); ctx.quadraticCurveTo(x * TS + 16, y * TS + rng() * 30, x * TS + 30, y * TS + 12); ctx.stroke(); }
      if (t === T.STAIRS) { ctx.fillStyle = '#4a4e52'; ctx.fillRect(x * TS, y * TS, TS, TS); ctx.fillStyle = '#6a6e72'; for (let i = 0; i < 6; i++) ctx.fillRect(x * TS + 2, y * TS + 2 + i * 5, TS - 4, 3); ctx.fillStyle = '#e2a33a'; ctx.font = 'bold 9px Arial'; ctx.fillText('▲▼', x * TS + 9, y * TS + 20); }
      if (t === T.FENCE) { ctx.strokeStyle = '#9aa0a4'; ctx.lineWidth = 1; for (let i = 0; i < TS; i += 6) { ctx.beginPath(); ctx.moveTo(x * TS + i, y * TS); ctx.lineTo(x * TS + i + 6, y * TS + TS); ctx.moveTo(x * TS + i + 6, y * TS); ctx.lineTo(x * TS + i, y * TS + TS); ctx.stroke(); } ctx.fillStyle = '#7a8084'; ctx.fillRect(x * TS + 14, y * TS + 14, 4, 4); }
    }
    // furniture
    const furnRng = U.rng(map.seed * 13 + f);
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) {
      const k = y * map.w + x, t = fl.t[k];
      if ((t === T.FURN || t === T.HIGH) && fl.furn[k] && fl.furn[k] !== F.car && fl.furn[k] !== F.tree) {
        if (fl.furn[k] === F.container && map.decor.some(d => d.type === 'container' && x >= d.x && x < d.x + d.w && y >= d.y && y < d.y + d.h)) continue;
        drawFurn(ctx, fl.furn[k], x, y, furnRng, map, f);
      }
    }
    // decor (cars, containers) on ground floor
    if (f === 0) for (const d of map.decor) {
      if (d.type === 'car') {
        const horiz = d.w > d.h;
        art.vehicle(ctx, { len: (horiz ? d.w : d.h) * 0.95, wid: (horiz ? d.h : d.w) * 0.9, color: d.color, armored: false }, { paint: d.color, lights: 'none' }, (d.x + d.w / 2) * TS, (d.y + d.h / 2) * TS, horiz ? 0 : Math.PI / 2);
      } else if (d.type === 'container') {
        const px = d.x * TS, py = d.y * TS, w = d.w * TS, h = d.h * TS;
        ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.fillRect(px + 6, py + 6, w, h);
        ctx.fillStyle = d.color; ctx.fillRect(px, py, w, h);
        ctx.strokeStyle = 'rgba(0,0,0,0.25)'; for (let i = 4; i < w; i += 6) { ctx.beginPath(); ctx.moveTo(px + i, py); ctx.lineTo(px + i, py + h); ctx.stroke(); }
        ctx.strokeStyle = 'rgba(255,255,255,0.15)'; ctx.strokeRect(px + 1, py + 1, w - 2, h - 2);
      }
    }
    // trees last (canopies)
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) { const k = y * map.w + x; if (fl.furn[k] === F.tree) drawFurn(ctx, F.tree, x, y, furnRng, map, f); }
    return c;
  }
  function base(map, f) { const L = getLayers(map); if (!L.floors[f].base) L.floors[f].base = buildBase(map, f); return L.floors[f].base; }
  function decal(map, f) { const L = getLayers(map); if (!L.floors[f].decal) L.floors[f].decal = makeCanvas(map.w * TS, map.h * TS); return L.floors[f].decal; }
  function resetDecals(map) { const L = getLayers(map); L.floors.forEach(fl => fl.decal = null); }

  // ---------- particles ----------
  function spawnFx(scene, e, map) {
    const P = scene.particles, R = Math.random;
    const add = p => P.push(Object.assign({ vx: 0, vy: 0, life: 0.3, max: 0.3, size: 2 }, p));
    switch (e.type) {
      case 'muzzle': add({ type: 'flash', x: e.x, y: e.y, f: e.f, life: 0.06, max: 0.06, ang: e.ang, sup: e.sup }); add({ type: 'smoke', x: e.x, y: e.y, f: e.f, vx: Math.cos(e.ang) * 20, vy: Math.sin(e.ang) * 20, life: 0.6, max: 0.6, size: 4, alpha: 0.2 }); break;
      case 'shell': add({ type: 'shell', x: e.x, y: e.y, f: e.f, vx: Math.cos(e.ang) * (60 + R() * 50), vy: Math.sin(e.ang) * (60 + R() * 50), life: 0.35, max: 0.35, size: 2, stamp: '#c9a24a' }); break;
      case 'tracer': add({ type: 'tracer', x: e.x0, y: e.y0, x1: e.x1, y1: e.y1, f: e.f, life: 0.07, max: 0.07, team: e.team }); break;
      case 'impact': {
        const col = e.kind === 'metal' ? '#ffd27a' : e.kind === 'door' || e.kind === 'furn' || e.kind === 'wood' ? '#8a6a40' : '#a8a49c';
        for (let i = 0; i < 5; i++) { const a = (e.ang || 0) + Math.PI + (R() - 0.5) * 2; add({ type: 'spark', x: e.x, y: e.y, f: e.f, vx: Math.cos(a) * (60 + R() * 120), vy: Math.sin(a) * (60 + R() * 120), life: 0.25, max: 0.25, size: e.kind === 'metal' ? 1.5 : 2, color: col }); }
        add({ type: 'smoke', x: e.x, y: e.y, f: e.f, life: 0.5, max: 0.5, size: 5, alpha: 0.25, color: '#bbb' });
        stamp(map, e.f, ctx => { ctx.fillStyle = 'rgba(20,20,20,0.7)'; ctx.beginPath(); ctx.arc(e.x, e.y, 1.6, 0, 7); ctx.fill(); });
        break;
      }
      case 'blood': for (let i = 0; i < 7; i++) { const a = (e.ang || 0) + (R() - 0.5) * 1.2; add({ type: 'drop', x: e.x, y: e.y, f: e.f, vx: Math.cos(a) * (40 + R() * 140), vy: Math.sin(a) * (40 + R() * 140), life: 0.25 + R() * 0.2, max: 0.45, size: 1.5 + R() * 1.5, stamp: 'rgba(110,10,10,0.8)' }); } break;
      case 'pool': add({ type: 'pool', x: e.x + (R() - 0.5) * 6, y: e.y + (R() - 0.5) * 6, f: e.f, life: 4, max: 4, size: 14 + R() * 6 }); break;
      case 'glass': for (let i = 0; i < 10; i++) { const a = R() * Math.PI * 2; add({ type: 'drop', x: e.x, y: e.y, f: e.f, vx: Math.cos(a) * (30 + R() * 80), vy: Math.sin(a) * (30 + R() * 80), life: 0.3, max: 0.3, size: 1.5, stamp: 'rgba(170,210,230,0.8)', color: '#bde' }); } break;
      case 'splinters': for (let i = 0; i < 14; i++) { const a = R() * Math.PI * 2; add({ type: 'drop', x: e.x, y: e.y, f: e.f, vx: Math.cos(a) * (40 + R() * 120), vy: Math.sin(a) * (40 + R() * 120), life: 0.35, max: 0.35, size: 2, stamp: '#5a3e22', color: '#8a6a40' }); } add({ type: 'smoke', x: e.x, y: e.y, f: e.f, life: 1, max: 1, size: 14, alpha: 0.35, color: '#aaa' }); break;
      case 'explosion':
        add({ type: 'fire', x: e.x, y: e.y, f: e.f, life: 0.35, max: 0.35, size: e.r || 50 });
        for (let i = 0; i < 10; i++) { const a = R() * Math.PI * 2; add({ type: 'smoke', x: e.x, y: e.y, f: e.f, vx: Math.cos(a) * 40, vy: Math.sin(a) * 40, life: 2 + R(), max: 3, size: 16 + R() * 10, alpha: 0.5, color: '#666' }); }
        stamp(map, e.f, ctx => { const g = ctx.createRadialGradient(e.x, e.y, 2, e.x, e.y, 36); g.addColorStop(0, 'rgba(0,0,0,0.6)'); g.addColorStop(1, 'rgba(0,0,0,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(e.x, e.y, 36, 0, 7); ctx.fill(); });
        scene.shake = Math.max(scene.shake || 0, 10);
        break;
      case 'flash':
        add({ type: 'bang', x: e.x, y: e.y, f: e.f, life: 0.25, max: 0.25, size: e.big ? 140 : 60 });
        for (let i = 0; i < 6; i++) add({ type: 'smoke', x: e.x, y: e.y, f: e.f, vx: (R() - 0.5) * 50, vy: (R() - 0.5) * 50, life: 2, max: 2, size: 10, alpha: 0.35, color: '#ccc' });
        scene.flashes.push({ x: e.x, y: e.y, f: e.f, t: 0 });
        scene.shake = Math.max(scene.shake || 0, 5);
        stamp(map, e.f, ctx => { ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.beginPath(); ctx.arc(e.x, e.y, 9, 0, 7); ctx.fill(); });
        break;
    }
  }
  function stamp(map, f, fn) { if (!map) return; const c = decal(map, f).getContext('2d'); fn(c); }
  function updateParticles(scene, dt, map) {
    for (const p of scene.particles) {
      p.life -= dt; p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.9; p.vy *= 0.9;
      if (p.type === 'smoke') { p.size += dt * 10; }
      if (p.type === 'pool' && p.life <= 0) stamp(map, p.f, ctx => { const g = ctx.createRadialGradient(p.x, p.y, 1, p.x, p.y, p.size); g.addColorStop(0, 'rgba(90,5,5,0.85)'); g.addColorStop(0.7, 'rgba(80,5,5,0.7)'); g.addColorStop(1, 'rgba(60,0,0,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(p.x, p.y, p.size, p.size * 0.8, p.x % 3, 0, 7); ctx.fill(); });
      else if (p.stamp && p.life <= 0) stamp(map, p.f, ctx => { ctx.fillStyle = p.stamp; ctx.fillRect(p.x - p.size / 2, p.y - p.size / 2, p.size, p.size * (p.type === 'shell' ? 0.5 : 1)); });
    }
    scene.particles = scene.particles.filter(p => p.life > 0);
    for (const fl of scene.flashes) fl.t += dt;
    scene.flashes = scene.flashes.filter(f => f.t < 1.2);
    if (scene.shake) scene.shake = Math.max(0, scene.shake - dt * 25);
  }
  function drawParticles(ctx, scene, floor) {
    for (const p of scene.particles) {
      if (p.f !== floor) continue;
      const k = p.life / p.max;
      switch (p.type) {
        case 'tracer': { ctx.strokeStyle = p.team === 'op' ? `rgba(255,230,160,${k})` : `rgba(255,150,110,${k})`; ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x1, p.y1); ctx.stroke(); break; }
        case 'spark': case 'drop': case 'shell': ctx.fillStyle = p.color || (p.type === 'shell' ? '#d9b25a' : '#8a1010'); ctx.globalAlpha = Math.min(1, k * 2); ctx.fillRect(p.x - p.size / 2, p.y - p.size / 2, p.size, p.size); ctx.globalAlpha = 1; break;
        case 'smoke': ctx.fillStyle = p.color || '#999'; ctx.globalAlpha = (p.alpha || 0.3) * k; ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, 7); ctx.fill(); ctx.globalAlpha = 1; break;
        case 'pool': { const s = p.size * (1 - k * 0.8); ctx.fillStyle = 'rgba(90,5,5,0.8)'; ctx.beginPath(); ctx.ellipse(p.x, p.y, s, s * 0.8, 0, 0, 7); ctx.fill(); break; }
      }
    }
  }
  function drawLights(ctx, scene, floor) {
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    for (const p of scene.particles) {
      if (p.f !== floor) continue;
      const k = p.life / p.max;
      if (p.type === 'flash' && !p.hidden) {
        const r = p.sup ? 10 : 34;
        const g = ctx.createRadialGradient(p.x, p.y, 1, p.x, p.y, r); g.addColorStop(0, `rgba(255,230,150,${0.9 * k})`); g.addColorStop(1, 'rgba(255,150,50,0)');
        ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, r, 0, 7); ctx.fill();
        if (!p.sup) { ctx.fillStyle = `rgba(255,240,200,${k})`; ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.ang); ctx.beginPath(); ctx.moveTo(0, -3); ctx.lineTo(14, 0); ctx.lineTo(0, 3); ctx.fill(); ctx.restore(); }
      }
      if (p.type === 'fire') { const g = ctx.createRadialGradient(p.x, p.y, 2, p.x, p.y, p.size); g.addColorStop(0, `rgba(255,240,180,${k})`); g.addColorStop(0.4, `rgba(255,140,40,${0.8 * k})`); g.addColorStop(1, 'rgba(120,30,0,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, 7); ctx.fill(); }
      if (p.type === 'bang') { const g = ctx.createRadialGradient(p.x, p.y, 2, p.x, p.y, p.size); g.addColorStop(0, `rgba(255,255,255,${k})`); g.addColorStop(1, 'rgba(255,255,255,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, 7); ctx.fill(); }
    }
    ctx.restore();
  }

  // ---------- doors / walls ----------
  function doorAxis(map, d) {
    const fl = map.floors[d.f];
    const t = (x, y) => (x < 0 || y < 0 || x >= map.w || y >= map.h) ? T.WALL : fl.t[y * map.w + x];
    const isW = v => v === T.WALL || v === T.WINDOW || v === T.BWIN || v === T.DOOR;
    // slab runs along the wall line
    if (isW(t(d.x - 1, d.y)) && isW(t(d.x + 1, d.y))) return 'h';
    if (isW(t(d.x, d.y - 1)) && isW(t(d.x, d.y + 1))) return 'v';
    return isW(t(d.x - 1, d.y)) || isW(t(d.x + 1, d.y)) ? 'h' : 'v';
  }
  function drawDoors(ctx, map, floor, doors, t) {
    for (const d of doors) {
      if (d.f !== floor) continue;
      const ax = doorAxis(map, d);
      const px = d.x * TS, py = d.y * TS;
      // frame floor
      ctx.fillStyle = '#4a4440'; ctx.fillRect(px, py, TS, TS);
      const target = d.state === 'closed' ? 0 : 1;
      d.anim = d.anim === undefined ? target : d.anim + (target - d.anim) * 0.25;
      ctx.save();
      if (d.state === 'broken') {
        ctx.fillStyle = '#5a3e22';
        ctx.translate(px + TS / 2, py + TS / 2); ctx.rotate(ax === 'h' ? 0.5 : 2.1); ctx.fillRect(-14, -3, 22, 5); ctx.rotate(1.2); ctx.fillRect(-4, -2, 12, 3);
        ctx.restore(); continue;
      }
      const hinge = d.hinge || 1;
      if (ax === 'h') { ctx.translate(hinge > 0 ? px : px + TS, py + TS / 2); ctx.rotate(hinge * d.anim * Math.PI / 2 * (hinge > 0 ? 1 : -1)); ctx.fillStyle = d.heavy ? '#5a6068' : '#7a5530'; ctx.fillRect(hinge > 0 ? 0 : -TS, -3, TS, 6); ctx.fillStyle = d.heavy ? '#8a9098' : '#9a7040'; ctx.fillRect(hinge > 0 ? 2 : -TS + 2, -2, TS - 4, 1.5); }
      else { ctx.translate(px + TS / 2, hinge > 0 ? py : py + TS); ctx.rotate(-hinge * d.anim * Math.PI / 2 * (hinge > 0 ? 1 : -1)); ctx.fillStyle = d.heavy ? '#5a6068' : '#7a5530'; ctx.fillRect(-3, hinge > 0 ? 0 : -TS, 6, TS); ctx.fillStyle = d.heavy ? '#8a9098' : '#9a7040'; ctx.fillRect(-2, hinge > 0 ? 2 : -TS + 2, 1.5, TS - 4); }
      ctx.restore();
      if (d.state === 'closed' && (d.knownLocked) && d.locked !== false) { ctx.fillStyle = d.barricaded ? '#d9534f' : '#e2a33a'; ctx.font = 'bold 11px Arial'; ctx.textAlign = 'center'; ctx.fillText(d.barricaded ? '✖' : '🔒', px + TS / 2, py + TS / 2 + 4); }
    }
  }
  function wallTiles(map, f) {
    const L = getLayers(map);
    if (L.wallCache[f]) return L.wallCache[f];
    const fl = map.floors[f], list = [];
    const t = (x, y) => (x < 0 || y < 0 || x >= map.w || y >= map.h) ? -1 : fl.t[y * map.w + x];
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) {
      const v = fl.t[y * map.w + x];
      if (v === T.WALL && fl.mat[y * map.w + x] !== MAT.rock) {
        const e = [t(x, y - 1) !== T.WALL, t(x + 1, y) !== T.WALL, t(x, y + 1) !== T.WALL, t(x - 1, y) !== T.WALL];
        list.push({ x, y, e, high: false });
      } else if (v === T.HIGH && fl.furn[y * map.w + x] !== F.tree) {
        const e = [t(x, y - 1) !== T.HIGH, t(x + 1, y) !== T.HIGH, t(x, y + 1) !== T.HIGH, t(x - 1, y) !== T.HIGH];
        list.push({ x, y, e, high: true, furn: fl.furn[y * map.w + x] });
      } else if (v === T.WALL && fl.mat[y * map.w + x] === MAT.rock) {
        // rock: only edges next to non-rock
        const n = [t(x, y - 1), t(x + 1, y), t(x, y + 1), t(x - 1, y)];
        if (n.some(k => k !== T.WALL && k !== -1)) list.push({ x, y, e: n.map(k => k !== T.WALL && k !== -1), rock: true });
      }
    }
    L.wallCache[f] = list;
    return list;
  }
  function drawWalls(ctx, view, toScreen, Wd, Ht) {
    const map = view.map, f = view.floor;
    const list = wallTiles(map, f);
    const cxs = Wd / 2, cys = Ht / 2;
    const h = 0.038 * Math.min(1.5, Math.max(0.7, view.cam.zoom)) / (view.dpr || 1);
    const sides = [], tops = [];
    const decorCols = {};
    for (const d of map.decor) if (d.type === 'container') for (let yy = d.y; yy < d.y + d.h; yy++) for (let xx = d.x; xx < d.x + d.w; xx++) decorCols[xx + ',' + yy] = d.color;
    for (const w of list) {
      const x0 = w.x * TS, y0 = w.y * TS;
      const p = [toScreen(x0, y0), toScreen(x0 + TS, y0), toScreen(x0 + TS, y0 + TS), toScreen(x0, y0 + TS)];
      // cull
      if (p.every(q => q[0] < -60 || q[0] > Wd + 60 || q[1] < -60 || q[1] > Ht + 60)) continue;
      const hh = w.high ? h * (w.furn === F.container ? 0.9 : 0.75) : w.rock ? h * 0.5 : h;
      const top = p.map(q => [cxs + (q[0] - cxs) * (1 + hh), cys + (q[1] - cys) * (1 + hh)]);
      const edges = [[0, 1], [1, 2], [2, 3], [3, 0]];
      for (let i = 0; i < 4; i++) if (w.e[i]) sides.push({ a: p[edges[i][0]], b: p[edges[i][1]], ta: top[edges[i][0]], tb: top[edges[i][1]], i, w });
      tops.push({ top, w });
    }
    // sides
    for (const s of sides) {
      const shade = [0.75, 0.55, 0.45, 0.65][s.i];
      let base = s.w.high ? (decorCols[s.w.x + ',' + s.w.y] || (s.w.furn === F.rack ? '#46525c' : '#6a6a6a')) : s.w.rock ? '#2a2725' : (view.wallColor || '#8d8a84');
      ctx.fillStyle = U.shade(base, -1 + shade);
      ctx.beginPath(); ctx.moveTo(s.a[0], s.a[1]); ctx.lineTo(s.b[0], s.b[1]); ctx.lineTo(s.tb[0], s.tb[1]); ctx.lineTo(s.ta[0], s.ta[1]); ctx.closePath(); ctx.fill();
    }
    for (const t of tops) {
      const w = t.w;
      ctx.fillStyle = w.high ? (decorCols[w.x + ',' + w.y] ? U.shade(decorCols[w.x + ',' + w.y], 0.1) : w.furn === F.rack ? '#5a6672' : '#7a7a7a') : w.rock ? '#24211f' : (view.wallTop || '#cfcac0');
      ctx.beginPath(); ctx.moveTo(t.top[0][0], t.top[0][1]); for (let i = 1; i < 4; i++) ctx.lineTo(t.top[i][0], t.top[i][1]); ctx.closePath(); ctx.fill();
      if (w.high && w.furn === F.rack) { ctx.strokeStyle = 'rgba(200,170,110,0.5)'; ctx.lineWidth = 1; ctx.stroke(); }
    }
  }
  function drawWindows(ctx, map, f) {
    const fl = map.floors[f];
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) {
      const v = fl.t[y * map.w + x];
      if (v !== T.WINDOW && v !== T.BWIN) continue;
      const px = x * TS, py = y * TS;
      const horiz = fl.t[y * map.w + x - 1] === T.WALL || fl.t[y * map.w + x + 1] === T.WALL || fl.t[y * map.w + x - 1] === T.WINDOW;
      ctx.fillStyle = '#5a5650';
      if (horiz) { ctx.fillRect(px, py + 11, TS, 10); ctx.fillStyle = v === T.WINDOW ? 'rgba(150,200,230,0.8)' : 'rgba(150,200,230,0.25)'; ctx.fillRect(px + 1, py + 14, TS - 2, 4); }
      else { ctx.fillRect(px + 11, py, 10, TS); ctx.fillStyle = v === T.WINDOW ? 'rgba(150,200,230,0.8)' : 'rgba(150,200,230,0.25)'; ctx.fillRect(px + 14, py + 1, 4, TS - 2); }
    }
  }

  // ---------- main draw ----------
  function pointInPoly(px, py, pts) {
    let inside = false;
    for (let i = 0, j = pts.length - 2; i < pts.length; j = i, i += 2) {
      const xi = pts[i], yi = pts[i + 1], xj = pts[j], yj = pts[j + 1];
      if (((yi > py) !== (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) inside = !inside;
    }
    return inside;
  }
  let fogCanvas = null;
  function draw(ctx, view) {
    const { map, floor, cam } = view;
    const Wd = ctx.canvas.width, Ht = ctx.canvas.height;
    const dpr = view.dpr || 1;
    const scene = view.scene;
    const shake = scene && scene.shake ? scene.shake : 0;
    const sx = shake ? (Math.random() - 0.5) * shake : 0, sy = shake ? (Math.random() - 0.5) * shake : 0;
    const z = cam.zoom * dpr;
    const setWorld = c => { c.setTransform(1, 0, 0, 1, 0, 0); c.translate(Wd / 2 + sx, Ht / 2 + sy); c.scale(z, z); c.rotate(cam.rot); c.translate(-cam.x, -cam.y); };
    const cr = Math.cos(cam.rot), sr = Math.sin(cam.rot);
    const toScreen = (x, y) => { const dx = x - cam.x, dy = y - cam.y; return [Wd / 2 + sx + (dx * cr - dy * sr) * z, Ht / 2 + sy + (dx * sr + dy * cr) * z]; };
    view.toScreen = toScreen;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = '#07090b'; ctx.fillRect(0, 0, Wd, Ht);
    setWorld(ctx);
    // lower floor ghost
    if (floor > 0 && map.type !== 'bunker') { ctx.globalAlpha = 0.28; ctx.drawImage(base(map, 0), 0, 0); ctx.globalAlpha = 1; }
    ctx.drawImage(base(map, floor), 0, 0);
    ctx.drawImage(decal(map, floor), 0, 0);
    if (view.vehicle && floor === 0) art.vehicle(ctx, view.vehicle.def, view.vehicle.inst, view.vehicle.x, view.vehicle.y, view.vehicle.ang, view.t);
    // spawns
    if (view.showSpawns) for (const s of map.spawns) { ctx.strokeStyle = 'rgba(226,163,58,0.7)'; ctx.setLineDash([4, 4]); ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc((s.x + 0.5) * TS, (s.y + 0.5) * TS, 2.5 * TS, 0, 7); ctx.stroke(); ctx.setLineDash([]); ctx.fillStyle = '#e2a33a'; ctx.font = 'bold 12px Arial'; ctx.textAlign = 'center'; ctx.fillText('ENTRY ' + s.name.toUpperCase(), (s.x + 0.5) * TS, (s.y + 0.5) * TS - 2.8 * TS); }
    drawWindows(ctx, map, floor);
    drawDoors(ctx, map, floor, map.doors, view.t);
    // vision polys (world coords)
    const polys = view.vision || [];
    // items
    for (const it of view.items || []) {
      if (it.f !== floor || it.collected) continue;
      if (!it.seen && !view.showAll) { for (const p of polys) if (pointInPoly(it.x, it.y, p)) { it.seen = true; break; } }
      if (!it.seen && !view.showAll) continue;
      if (it.kind !== 'drop') { ctx.strokeStyle = it.kind === 'evidence' ? 'rgba(79,195,247,0.8)' : 'rgba(226,163,58,0.8)'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(it.x, it.y, 14 + Math.sin(view.t * 4) * 2, 0, 7); ctx.stroke(); }
      art.itemIcon(ctx, it.type, it.x, it.y, view.t);
    }
    // gas clouds (under)
    for (const g of view.gases || []) { if (g.f !== floor) continue; const gr = ctx.createRadialGradient(g.x, g.y, 4, g.x, g.y, g.r); gr.addColorStop(0, 'rgba(200,210,150,0.4)'); gr.addColorStop(1, 'rgba(200,210,150,0)'); ctx.fillStyle = gr; ctx.beginPath(); ctx.arc(g.x, g.y, g.r, 0, 7); ctx.fill(); }
    // units
    const units = view.units;
    const drawList = units.filter(u => u.floor === floor && !u.escaped && !u.evacuated && (u.team === 'op' || u.visible || view.showAll));
    drawList.sort((a, b) => (b.dead || b.downed || b.incap ? 1 : 0) - (a.dead || a.downed || a.incap ? 1 : 0));
    if (view.preUnits) view.preUnits(ctx);
    for (const u of drawList) {
      const fu = u.drawData || u;
      art.unitTop(ctx, fu, view.t);
      if (u.team === 'op' && !u.dead) { ctx.strokeStyle = u.color; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(u.x, u.y, 12, 0, 7); ctx.stroke(); }
      if (u.team === 'enemy' && u.ai && u.ai.state === 'surrender' && !u.restrained) { ctx.fillStyle = '#e2a33a'; ctx.font = 'bold 10px Arial'; ctx.textAlign = 'center'; ctx.fillText('SURRENDER', u.x, u.y - 18); }
      if (u.team === 'enemy' && u.ai && u.ai.guard && u.ai.execT >= 0 && u.alive && !u.restrained && u.ai.state !== 'surrender' && u.visible) { ctx.fillStyle = '#ff4040'; ctx.font = 'bold 11px Arial'; ctx.textAlign = 'center'; ctx.fillText('⚠ ' + Math.max(0, u.ai.execT).toFixed(0), u.x, u.y - 18); }
      if (u.hvt && !u.dead) { ctx.strokeStyle = '#ff4040'; ctx.lineWidth = 1.5; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.arc(u.x, u.y, 15, 0, 7); ctx.stroke(); ctx.setLineDash([]); ctx.fillStyle = '#ff6060'; ctx.font = 'bold 9px Arial'; ctx.textAlign = 'center'; ctx.fillText('HVT', u.x, u.y + 24); }
      if (u.kind === 'hostage' && u.alive && u.ai.state !== 'secured') { ctx.fillStyle = '#4fc3f7'; ctx.font = 'bold 9px Arial'; ctx.textAlign = 'center'; ctx.fillText('HOSTAGE', u.x, u.y + 22); }
    }
    // casual: show enemies faintly
    if (view.showHints) for (const u of units) if (u.floor === floor && u.team === 'enemy' && u.alive && !u.visible && !u.restrained) { ctx.strokeStyle = 'rgba(255,80,80,0.35)'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(u.x, u.y, 8, 0, 7); ctx.stroke(); }
    // grenades
    for (const g of view.grenades || []) { if (g.f !== floor) continue; ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.beginPath(); ctx.arc(g.x + 2, g.y + 3, 3.5, 0, 7); ctx.fill(); ctx.fillStyle = g.g === 'flash' ? '#cfd6d0' : g.g === 'smoke' ? '#6a8a5a' : g.g === 'gas' ? '#b0b05a' : '#d06040'; ctx.beginPath(); ctx.arc(g.x, g.y - (g.z || 0) * 0.6, 3.5, 0, 7); ctx.fill(); }
    if (scene) drawParticles(ctx, scene, floor);
    // smoke clouds
    for (const s of view.smokes || []) { if (s.f !== floor) continue; for (let i = 0; i < 7; i++) { const a = i * 0.9 + view.t * 0.15, r = s.r * 0.45; ctx.fillStyle = 'rgba(190,195,190,0.42)'; ctx.beginPath(); ctx.arc(s.x + Math.cos(a) * r, s.y + Math.sin(a) * r, s.r * 0.7, 0, 7); ctx.fill(); } }
    // fog of war
    if (!view.showAll) {
      if (!fogCanvas || fogCanvas.width !== Wd || fogCanvas.height !== Ht) { fogCanvas = makeCanvas(Wd, Ht); }
      const fc = fogCanvas.getContext('2d');
      fc.setTransform(1, 0, 0, 1, 0, 0); fc.globalCompositeOperation = 'source-over'; fc.clearRect(0, 0, Wd, Ht);
      fc.fillStyle = view.night ? 'rgba(2,4,8,0.9)' : 'rgba(5,8,12,0.66)'; fc.fillRect(0, 0, Wd, Ht);
      fc.globalCompositeOperation = 'destination-out';
      setWorld(fc);
      for (const p of polys) { fc.beginPath(); fc.moveTo(p[0], p[1]); for (let i = 2; i < p.length; i += 2) fc.lineTo(p[i], p[i + 1]); fc.closePath(); fc.fillStyle = '#000'; fc.fill(); }
      for (const c of view.camVision || []) { fc.beginPath(); fc.arc(c.x, c.y, 6 * TS, 0, 7); fc.fillStyle = 'rgba(0,0,0,0.75)'; fc.fill(); }
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.drawImage(fogCanvas, 0, 0);
      // night light cones tint
      if (view.night) { setWorld(ctx); ctx.globalCompositeOperation = 'lighter'; for (const p of polys) { ctx.beginPath(); ctx.moveTo(p[0], p[1]); for (let i = 2; i < p.length; i += 2) ctx.lineTo(p[i], p[i + 1]); ctx.closePath(); ctx.fillStyle = 'rgba(40,38,25,0.08)'; ctx.fill(); } ctx.globalCompositeOperation = 'source-over'; }
    }
    // walls
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    drawWalls(ctx, view, toScreen, Wd, Ht);
    // lights
    setWorld(ctx);
    if (scene) {
      for (const p of scene.particles) if (p.type === 'flash' && p.hidden === undefined) { p.hidden = !view.showAll && !polys.some(pp => pointInPoly(p.x, p.y, pp)); }
      drawLights(ctx, scene, floor);
    }
    // heard markers
    for (const h of view.heard || []) {
      if (h.f !== floor) continue;
      const age = (view.time - h.t); if (age < 0 || age > 2.5) continue;
      ctx.strokeStyle = `rgba(255,120,80,${0.7 * (1 - age / 2.5)})`; ctx.lineWidth = 2 / cam.zoom;
      for (let i = 0; i < 3; i++) { ctx.beginPath(); ctx.arc(h.x, h.y, 10 + age * 40 + i * 10, 0, 7); ctx.stroke(); }
    }
    // last known markers
    for (const u of units) {
      if (u.team !== 'enemy' || u.visible || !u.lastSeen || u.dead || u.restrained || u.escaped || view.showAll) continue;
      if (u.lastSeen.f !== floor || view.time - u.lastSeen.t > 25) continue;
      ctx.strokeStyle = 'rgba(255,80,80,0.55)'; ctx.setLineDash([3, 3]); ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(u.lastSeen.x, u.lastSeen.y, 10, 0, 7); ctx.stroke(); ctx.setLineDash([]);
      ctx.fillStyle = 'rgba(255,100,100,0.8)'; ctx.font = 'bold 11px Arial'; ctx.textAlign = 'center'; ctx.fillText('?', u.lastSeen.x, u.lastSeen.y + 4);
    }
    // overlays from UI (paths, selection)
    if (view.overlay) { setWorld(ctx); view.overlay(ctx, setWorld, toScreen); }
    // weather / screen effects
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    if (view.rain) { ctx.strokeStyle = 'rgba(170,190,220,0.25)'; ctx.lineWidth = 1; ctx.beginPath(); const r = view.rainSeed || 0; for (let i = 0; i < 160; i++) { const x = ((i * 97 + view.t * 900 * (1 + (i % 3) * 0.2)) % (Wd + 100)) - 50, y = ((i * 211 + view.t * 1300) % (Ht + 100)) - 50; ctx.moveTo(x, y); ctx.lineTo(x - 4, y + 14); } ctx.stroke(); }
    if (scene) for (const fl of scene.flashes) {
      if (fl.f !== floor) continue;
      const [x, y] = toScreen(fl.x, fl.y); const d = Math.hypot(x - Wd / 2, y - Ht / 2);
      const a = Math.max(0, (1 - fl.t / 1.2)) * Math.max(0.15, 1 - d / Math.max(Wd, Ht)) * 0.7;
      ctx.fillStyle = `rgba(255,255,255,${a})`; ctx.fillRect(0, 0, Wd, Ht);
    }
    if (view.night) { const g = ctx.createRadialGradient(Wd / 2, Ht / 2, Math.min(Wd, Ht) * 0.3, Wd / 2, Ht / 2, Math.max(Wd, Ht) * 0.75); g.addColorStop(0, 'rgba(0,10,30,0)'); g.addColorStop(1, 'rgba(0,10,30,0.45)'); ctx.fillStyle = g; ctx.fillRect(0, 0, Wd, Ht); }
  }

  // Briefing map preview (whole map, top-down, no fog)
  function preview(ctx, map, floor, opts = {}) {
    const Wd = ctx.canvas.width, Ht = ctx.canvas.height;
    const s = Math.min(Wd / (map.w * TS), Ht / (map.h * TS)) * 0.95;
    ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.fillStyle = '#080b0e'; ctx.fillRect(0, 0, Wd, Ht);
    ctx.setTransform(s, 0, 0, s, (Wd - map.w * TS * s) / 2, (Ht - map.h * TS * s) / 2);
    if (floor > 0 && map.type !== 'bunker') { ctx.globalAlpha = 0.25; ctx.drawImage(base(map, 0), 0, 0); ctx.globalAlpha = 1; }
    ctx.drawImage(base(map, floor), 0, 0);
    // blueprint tint
    ctx.fillStyle = 'rgba(20,60,110,0.35)'; ctx.fillRect(0, 0, map.w * TS, map.h * TS);
    const fl = map.floors[floor];
    for (let y = 0; y < map.h; y++) for (let x = 0; x < map.w; x++) {
      const t = fl.t[y * map.w + x];
      if (t === T.WALL && fl.mat[y * map.w + x] !== MAT.rock) { ctx.fillStyle = '#cfe3f5'; ctx.fillRect(x * TS, y * TS, TS, TS); }
      if (t === T.WINDOW) { ctx.fillStyle = '#6fb7d8'; ctx.fillRect(x * TS + 8, y * TS + 8, 16, 16); }
      if (t === T.HIGH && fl.furn[y * map.w + x] !== F.tree) { ctx.fillStyle = 'rgba(160,190,215,0.6)'; ctx.fillRect(x * TS + 2, y * TS + 2, TS - 4, TS - 4); }
    }
    for (const d of map.doors) if (d.f === floor) { ctx.fillStyle = d.exterior ? '#e2a33a' : '#8fb3d0'; ctx.fillRect(d.x * TS + 6, d.y * TS + 6, TS - 12, TS - 12); }
    for (const s2 of map.spawns) { ctx.fillStyle = '#e2a33a'; ctx.beginPath(); ctx.arc((s2.x + 0.5) * TS, (s2.y + 0.5) * TS, 22, 0, 7); ctx.fill(); ctx.fillStyle = '#111'; ctx.font = 'bold 26px Arial'; ctx.textAlign = 'center'; ctx.fillText(s2.name[0], (s2.x + 0.5) * TS, (s2.y + 0.5) * TS + 9); }
    if (opts.markers) for (const mk of opts.markers) { if (mk.f !== floor) continue; ctx.fillStyle = mk.color; ctx.beginPath(); ctx.arc(mk.x, mk.y, 12, 0, 7); ctx.fill(); }
    if (opts.labels !== false) for (const b of map.buildings) if (b.name && b.floors.includes(floor)) { ctx.fillStyle = 'rgba(255,255,255,0.8)'; ctx.font = 'bold 28px Arial'; ctx.textAlign = 'center'; ctx.fillText(b.name.toUpperCase(), (b.x + b.w / 2) * TS, (b.y - 0.6) * TS); }
    ctx.setTransform(1, 0, 0, 1, 0, 0);
  }

  return { draw, preview, spawnFx, updateParticles, resetDecals, pointInPoly, base };
})();
