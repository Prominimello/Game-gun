// In-mission UI: input, camera, HUD, orders visualization, main loop
'use strict';
BP.missionUI = (() => {
  const TS = BP.TS, T = BP.T, U = BP.util, D = BP.D, sim = BP.sim, R = BP.render;
  let touch = { pts: new Map(), start: null, lp: null, pinch: null, append: false, multi: false }, isTouch = false;
  let canvas, ctx, hud, M = null, cam, scene, selected = [], floorView = 0, running = false, keys = {}, drag = null, mode = null, menu = null, lastT = 0, lockCam = false, mouse = { x: 0, y: 0, wx: 0, wy: 0 }, hudT = 0, visT = 0, onEnd = null, vehicle = null, help = false, banner = null, bannerT = 0, lastLogLen = 0, dpr = 1;
  const S = () => BP.audio.S;

  function start(cfg, endCb) {
    onEnd = endCb;
    canvas = document.getElementById('game'); ctx = canvas.getContext('2d');
    hud = document.getElementById('hud');
    M = sim.create(cfg);
    R.resetDecals(M.map);
    scene = { particles: [], flashes: [], shake: 0 };
    const opsL = M.units.filter(u => u.team === 'op');
    const sp = M.map.spawns[opsL[0] ? opsL[0].entry : 0] || M.map.spawns[0];
    const ox = opsL.reduce((a, u) => a + u.x, 0) / opsL.length, oy = opsL.reduce((a, u) => a + u.y, 0) / opsL.length;
    const b0 = M.map.buildings[0];
    const bx = b0 ? (b0.x + b0.w / 2) * TS : M.map.w * TS / 2, by = b0 ? (b0.y + b0.h / 2) * TS : M.map.h * TS / 2;
    cam = { x: ox * 0.6 + bx * 0.4, y: oy * 0.6 + by * 0.4, zoom: 1.0, rot: 0, tRot: 0 };
    selected = opsL.slice();
    floorView = 0; mode = null; drag = null; lockCam = false; help = false; banner = 'PLANNING PHASE'; bannerT = 3;
    if (cfg.vehicle) {
      const vdef = D.VEH[cfg.vehicle.id];
      const ang = sp.ang;
      vehicle = { def: vdef, inst: cfg.vehicle, x: (sp.x + 0.5) * TS - Math.cos(ang) * TS * 2.2, y: (sp.y + 0.5) * TS - Math.sin(ang) * TS * 2.2, ang: ang + Math.PI / 2 };
      // keep vehicle on map
      vehicle.x = U.clamp(vehicle.x, TS * 2, M.map.w * TS - TS * 2); vehicle.y = U.clamp(vehicle.y, TS * 2, M.map.h * TS - TS * 2);
    } else vehicle = null;
    buildHUD();
    resize();
    bind();
    BP.audio.startAmbient(M.map.type === 'bunker' ? 'bunker' : M.map.type === 'rural' ? 'rural' : 'city', { rain: M.rain, night: M.night });
    BP.audio.startMusic('mission');
    running = true; lastT = performance.now();
    lastLogLen = 0;
    requestAnimationFrame(loop);
  }
  function stop() { running = false; unbind(); closeMenu(); }

  function resize() {
    dpr = Math.min(2, window.devicePixelRatio || 1);
    canvas.width = Math.floor(canvas.clientWidth * dpr); canvas.height = Math.floor(canvas.clientHeight * dpr);
  }

  // ---------- coordinate helpers ----------
  function screenToWorld(sx, sy) {
    const x = (sx * dpr - canvas.width / 2) / (cam.zoom * dpr), y = (sy * dpr - canvas.height / 2) / (cam.zoom * dpr);
    const c = Math.cos(-cam.rot), s = Math.sin(-cam.rot);
    return [cam.x + x * c - y * s, cam.y + x * s + y * c];
  }
  function worldToScreen(wx, wy) {
    const dx = wx - cam.x, dy = wy - cam.y, c = Math.cos(cam.rot), s = Math.sin(cam.rot);
    return [canvas.clientWidth / 2 + (dx * c - dy * s) * cam.zoom, canvas.clientHeight / 2 + (dx * s + dy * c) * cam.zoom];
  }
  const ops = () => M.units.filter(u => u.team === 'op');
  const sel = () => selected.filter(u => sim.canAct(u));

  // ---------- input ----------
  const handlers = {};
  function bind() {
    handlers.md = e => onMouseDown(e); handlers.mu = e => onMouseUp(e); handlers.mm = e => onMouseMove(e);
    handlers.wh = e => onWheel(e); handlers.kd = e => onKey(e, true); handlers.ku = e => onKey(e, false);
    handlers.cm = e => e.preventDefault(); handlers.rs = () => resize();
    canvas.addEventListener('mousedown', handlers.md); window.addEventListener('mouseup', handlers.mu); window.addEventListener('mousemove', handlers.mm);
    canvas.addEventListener('wheel', handlers.wh, { passive: false }); window.addEventListener('keydown', handlers.kd); window.addEventListener('keyup', handlers.ku);
    canvas.addEventListener('contextmenu', handlers.cm); window.addEventListener('resize', handlers.rs);
    handlers.ts = e => onTouchStart(e); handlers.tm = e => onTouchMove(e); handlers.te = e => onTouchEnd(e);
    canvas.addEventListener('touchstart', handlers.ts, { passive: false }); canvas.addEventListener('touchmove', handlers.tm, { passive: false });
    canvas.addEventListener('touchend', handlers.te, { passive: false }); canvas.addEventListener('touchcancel', handlers.te, { passive: false });
  }
  function unbind() {
    canvas.removeEventListener('mousedown', handlers.md); window.removeEventListener('mouseup', handlers.mu); window.removeEventListener('mousemove', handlers.mm);
    canvas.removeEventListener('wheel', handlers.wh); window.removeEventListener('keydown', handlers.kd); window.removeEventListener('keyup', handlers.ku);
    canvas.removeEventListener('contextmenu', handlers.cm); window.removeEventListener('resize', handlers.rs);
    canvas.removeEventListener('touchstart', handlers.ts); canvas.removeEventListener('touchmove', handlers.tm);
    canvas.removeEventListener('touchend', handlers.te); canvas.removeEventListener('touchcancel', handlers.te);
  }

  // ---------- touch input ----------
  // 1 finger: tap operator = select, tap ground/door/target = order (like right-click), drag = pan, long-press = add waypoint.
  // 2 fingers: pinch = zoom, twist = rotate, drag = pan.
  function tpos(t) { const r = canvas.getBoundingClientRect(); return [t.clientX - r.left, t.clientY - r.top]; }
  function onTouchStart(e) {
    e.preventDefault(); BP.audio.init(); closeMenu();
    if (!isTouch) { isTouch = true; document.getElementById('scr-mission').classList.add('touch'); buildTouchBar(); }
    for (const t of e.changedTouches) touch.pts.set(t.identifier, tpos(t));
    clearTimeout(touch.lp);
    if (touch.pts.size === 1) {
      const [x, y] = [...touch.pts.values()][0];
      touch.start = { x, y, t: performance.now(), moved: false, world: screenToWorld(x, y) };
      mouse.x = x; mouse.y = y; [mouse.wx, mouse.wy] = screenToWorld(x, y);
      touch.lp = setTimeout(() => { if (touch.start && !touch.start.moved && touch.pts.size === 1) { touch.start.used = true; if (navigator.vibrate) navigator.vibrate(25); tapAction(touch.start.x, touch.start.y, true); } }, 480);
    } else if (touch.pts.size === 2) {
      touch.start = null;
      const [a, b] = [...touch.pts.values()];
      touch.pinch = { d: Math.hypot(a[0] - b[0], a[1] - b[1]), ang: Math.atan2(b[1] - a[1], b[0] - a[0]), zoom: cam.zoom, rot: cam.tRot, mid: screenToWorld((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) };
    }
  }
  function onTouchMove(e) {
    e.preventDefault();
    for (const t of e.changedTouches) if (touch.pts.has(t.identifier)) touch.pts.set(t.identifier, tpos(t));
    if (touch.pts.size === 1 && touch.start) {
      const [x, y] = [...touch.pts.values()][0];
      if (!touch.start.moved && Math.hypot(x - touch.start.x, y - touch.start.y) > 12) { touch.start.moved = true; clearTimeout(touch.lp); }
      if (touch.start.moved) { const [ax, ay] = touch.start.world; const [bx, by] = screenToWorld(x, y); cam.x += ax - bx; cam.y += ay - by; }
      mouse.x = x; mouse.y = y; [mouse.wx, mouse.wy] = screenToWorld(x, y);
    } else if (touch.pts.size >= 2 && touch.pinch) {
      const [a, b] = [...touch.pts.values()];
      const d = Math.hypot(a[0] - b[0], a[1] - b[1]), ang = Math.atan2(b[1] - a[1], b[0] - a[0]);
      cam.zoom = U.clamp(touch.pinch.zoom * d / Math.max(10, touch.pinch.d), 0.35, 3);
      const dr = U.angNorm(ang - touch.pinch.ang);
      if (Math.abs(dr) > 0.15) { cam.tRot = touch.pinch.rot + dr; cam.rot = cam.tRot; }
      const [mx, my] = screenToWorld((a[0] + b[0]) / 2, (a[1] + b[1]) / 2);
      cam.x += touch.pinch.mid[0] - mx; cam.y += touch.pinch.mid[1] - my;
    }
  }
  function onTouchEnd(e) {
    e.preventDefault();
    clearTimeout(touch.lp);
    const st = touch.start;
    for (const t of e.changedTouches) touch.pts.delete(t.identifier);
    if (touch.pts.size < 2) touch.pinch = null;
    if (touch.pts.size === 1) { const [x, y] = [...touch.pts.values()][0]; touch.start = { x, y, t: performance.now(), moved: true, world: screenToWorld(x, y) }; return; }
    if (!st || st.moved || st.used || e.type === 'touchcancel') { touch.start = null; return; }
    touch.start = null;
    tapAction(st.x, st.y, false);
  }
  function tapAction(x, y, long) {
    const [wx, wy] = screenToWorld(x, y);
    if (mode) { doThrow(wx, wy, touch.append || long); if (!touch.append) mode = null; updateTouchBar(); return; }
    const u = ops().find(o => o.floor === floorView && Math.hypot(o.x - wx, o.y - wy) < 22);
    if (u && !long) {
      if (touch.multi) selected = selected.includes(u) ? selected.filter(s => s !== u) : [...selected, u];
      else selected = [u];
      S().ui(); return;
    }
    if (!sel().length) return;
    rightClick(wx, wy, touch.append || long, x, y, false);
  }
  function buildTouchBar() {
    const old = document.getElementById('h-touch'); if (old) old.remove();
    const el = BP.util.el;
    const bar = el('div', { class: 'hud-touch', id: 'h-touch' },
      el('button', { id: 't-all', onclick: () => { selected = ops().filter(sim.canAct); S().ui(); } }, 'All'),
      el('button', { id: 't-multi', onclick: () => { touch.multi = !touch.multi; updateTouchBar(); } }, 'Multi'),
      el('button', { id: 't-wp', onclick: () => { touch.append = !touch.append; updateTouchBar(); } }, '+Waypoint'),
      el('button', { id: 't-cam', onclick: () => { lockCam = !lockCam; updateTouchBar(); } }, 'Cam Lock'),
      el('button', { id: 't-cmd', onclick: () => { document.getElementById('scr-mission').classList.toggle('cmd-open'); updateTouchBar(); } }, 'Orders ▴'));
    hud.append(bar); updateTouchBar();
  }
  function updateTouchBar() {
    const t = id => document.getElementById(id);
    if (!t('h-touch')) return;
    t('t-multi').className = touch.multi ? 'on' : ''; t('t-wp').className = touch.append ? 'on' : ''; t('t-cam').className = lockCam ? 'on' : '';
    t('t-cmd').className = document.getElementById('scr-mission').classList.contains('cmd-open') ? 'on' : '';
  }
  function onMouseMove(e) {
    const r = canvas.getBoundingClientRect(); mouse.x = e.clientX - r.left; mouse.y = e.clientY - r.top;
    [mouse.wx, mouse.wy] = screenToWorld(mouse.x, mouse.y);
    if (drag && drag.btn === 1) { const [ax, ay] = drag.world; const [bx, by] = screenToWorld(mouse.x, mouse.y); cam.x += ax - bx; cam.y += ay - by; }
    if (drag && drag.btn === 2) drag.moved = Math.hypot(mouse.x - drag.x, mouse.y - drag.y) > 18;
  }
  function onMouseDown(e) {
    BP.audio.init();
    closeMenu();
    const r = canvas.getBoundingClientRect(); const x = e.clientX - r.left, y = e.clientY - r.top;
    const [wx, wy] = screenToWorld(x, y);
    if (e.button === 0) {
      if (mode) { doThrow(wx, wy, e.shiftKey); if (!e.shiftKey) mode = null; return; }
      drag = { btn: 0, x, y, shift: e.shiftKey };
    } else if (e.button === 1) { drag = { btn: 1, x, y, world: [wx, wy] }; e.preventDefault(); }
    else if (e.button === 2) { if (mode) { mode = null; return; } drag = { btn: 2, x, y, wx, wy, shift: e.shiftKey, ctrl: e.ctrlKey || e.altKey }; }
  }
  function onMouseUp(e) {
    if (!drag) return;
    const d = drag; drag = null;
    const r = canvas.getBoundingClientRect(); const x = e.clientX - r.left, y = e.clientY - r.top;
    if (d.btn === 0) {
      if (Math.hypot(x - d.x, y - d.y) < 6) {
        const [wx, wy] = screenToWorld(x, y);
        const u = ops().find(o => o.floor === floorView && Math.hypot(o.x - wx, o.y - wy) < 16);
        if (u) { if (d.shift) { selected = selected.includes(u) ? selected.filter(s => s !== u) : [...selected, u]; } else selected = [u]; S().ui(); }
      } else {
        const x0 = Math.min(x, d.x), x1 = Math.max(x, d.x), y0 = Math.min(y, d.y), y1 = Math.max(y, d.y);
        const inBox = ops().filter(o => { if (o.floor !== floorView) return false; const [sx, sy] = worldToScreen(o.x, o.y); return sx >= x0 && sx <= x1 && sy >= y0 && sy <= y1; });
        if (inBox.length) { selected = d.shift ? [...new Set([...selected, ...inBox])] : inBox; S().ui(); }
      }
    } else if (d.btn === 2) {
      if (d.moved) { // right-drag: move then face drag direction
        const [ex, ey] = screenToWorld(x, y);
        rightClick(d.wx, d.wy, d.shift, x, y, d.ctrl, Math.atan2(ey - d.wy, ex - d.wx));
      } else rightClick(d.wx, d.wy, d.shift, x, y, d.ctrl);
    }
  }
  function onWheel(e) {
    e.preventDefault();
    const [bx, by] = screenToWorld(mouse.x, mouse.y);
    cam.zoom = U.clamp(cam.zoom * (e.deltaY < 0 ? 1.12 : 1 / 1.12), 0.35, 3);
    const [ax, ay] = screenToWorld(mouse.x, mouse.y);
    cam.x += bx - ax; cam.y += by - ay;
  }
  function onKey(e, down) {
    if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) return;
    keys[e.code] = down;
    if (!down) return;
    const k = e.code;
    const s = sel();
    if (k === 'Space') { e.preventDefault(); togglePause(); }
    else if (k.startsWith('Digit') && +k.slice(5) >= 1 && +k.slice(5) <= 8) { const i = +k.slice(5) - 1; const u = ops()[i]; if (u) { if (e.shiftKey) selected = selected.includes(u) ? selected.filter(x => x !== u) : [...selected, u]; else { if (selected.length === 1 && selected[0] === u) centerOn(u); selected = [u]; } if (u.floor !== floorView) floorView = u.floor; S().ui(); } }
    else if (k === 'Backquote' || (k === 'KeyA' && e.ctrlKey)) { e.preventDefault(); selected = ops().filter(sim.canAct); }
    else if (k === 'KeyH') { sim.orderHold(s); S().ui(); }
    else if (k === 'KeyF') { if (s.length > 1) { sim.orderFollow(s.slice(1), s[0]); sim.log(`${s.slice(1).map(u => u.name).join(', ')} following ${s[0].name}.`); S().ui(); } }
    else if (k === 'KeyR') regroup();
    else if (k === 'KeyV') { sim.orderCover(s); S().ui(); }
    else if (k === 'KeyC') { sim.toggleCrouch(s); S().ui(); }
    else if (k === 'KeyO') { sim.orderFace(s, mouse.wx, mouse.wy); S().ui(); }
    else if (k === 'KeyG') setMode('flash');
    else if (k === 'KeyT') setMode('smoke');
    else if (k === 'KeyY') setMode('gas');
    else if (k === 'KeyU') setMode('stinger');
    else if (k === 'KeyB') { sim.orderGo(s, true); sim.log('GO-code wait added.', '#e2a33a'); S().ui(); }
    else if (k === 'Enter' || k === 'NumpadEnter') { if (M.phase === 'plan') execute(); else sim.go(); }
    else if (k === 'KeyX' || k === 'Delete') { sim.orderCancel(s); S().ui(); }
    else if (k === 'KeyZ') cyclePace();
    else if (k === 'KeyN') { M.formation = M.formation === 'wedge' ? 'stack' : 'wedge'; sim.log('Formation: ' + M.formation.toUpperCase()); }
    else if (k === 'Tab' || k === 'PageUp' || k === 'PageDown') { e.preventDefault(); const n = M.map.floors.length; floorView = (floorView + (k === 'PageDown' ? n - 1 : 1)) % n; S().ui(); }
    else if (k === 'KeyQ') cam.tRot -= Math.PI / 4;
    else if (k === 'KeyE') cam.tRot += Math.PI / 4;
    else if (k === 'KeyL') { lockCam = !lockCam; sim.log('Camera lock ' + (lockCam ? 'ON' : 'OFF')); }
    else if (k === 'Escape') { mode = null; closeMenu(); }
    else if (k === 'F1' || k === 'Slash') { e.preventDefault(); help = !help; buildHelp(); }
    else if (k === 'BracketLeft') setSpeed(Math.max(0.25, M.speed / 2));
    else if (k === 'BracketRight') setSpeed(Math.min(2, M.speed * 2));
  }
  function setMode(g) {
    const s = sel(); if (!s.length) return;
    if (!s.some(u => u.util.includes(g))) { sim.log(`No ${D.ITEM[g].name} carried by selected.`, '#d9534f'); S().error(); return; }
    mode = { g }; S().ui();
  }
  function doThrow(wx, wy, append) {
    const s = sel().filter(u => u.util.includes(mode.g));
    if (!s.length) return;
    s.sort((a, b) => Math.hypot(a.x - wx, a.y - wy) - Math.hypot(b.x - wx, b.y - wy));
    sim.orderThrow(s[0], mode.g, wx, wy, floorView, append || M.phase === 'plan');
    S().ui();
  }
  function regroup() {
    const all = sel().length > 1 ? sel() : ops().filter(sim.canAct);
    const leader = all.find(u => u.op.role === 'leader') || all[0];
    if (!leader) return;
    sim.orderFollow(all.filter(u => u !== leader), leader);
    sim.log(`Regroup on ${leader.name}.`, '#e2a33a'); S().ui();
  }
  function cyclePace() {
    const s = sel(); if (!s.length) return;
    const order = ['slow', 'walk', 'run']; const p = order[(order.indexOf(s[0].pace) + 1) % 3];
    sim.setPace(s, p); sim.log('Pace: ' + p.toUpperCase()); S().ui();
  }
  function setSpeed(v) { M.speed = v; sim.log('Time scale ×' + v); }
  function togglePause() {
    if (M.phase === 'plan') { execute(); return; }
    M.paused = !M.paused; banner = M.paused ? 'PAUSED' : null; bannerT = M.paused ? 999 : 0; S().ui();
  }
  function execute() { BP.audio.init(); sim.start(); banner = 'EXECUTE'; bannerT = 1.5; S().confirm(); }
  function centerOn(u) { cam.x = u.x; cam.y = u.y; floorView = u.floor; }

  function rightClick(wx, wy, append, sx, sy, ctrl, faceAng) {
    const s = sel(); if (!s.length) return;
    const tx = sim.toTile(wx), ty = sim.toTile(wy), f = floorView;
    if (ctrl) { sim.orderFace(s, wx, wy); S().ui(); return; }
    // targets
    const units = M.units.filter(u => u.floor === f && !u.dead && (u.visible || u.team === 'op') && Math.hypot(u.x - wx, u.y - wy) < 16);
    const tgt = units[0];
    if (tgt) {
      if (tgt.team === 'enemy' && tgt.alive && !tgt.restrained && (tgt.ai.state === 'surrender' || tgt.incap || tgt.stun > 0)) { sim.orderInteract(s, tgt, 'arrest', append); S().ui(); return; }
      if (tgt.team === 'civ' && tgt.alive && tgt.ai.state !== 'secured') { sim.orderInteract(s, tgt, 'secure', append); S().ui(); return; }
      if (tgt.team === 'op' && (tgt.downed && !tgt.dead || tgt.hp < tgt.maxHp * 0.9) && !selected.includes(tgt)) { sim.orderInteract(s, tgt, 'heal', append); S().ui(); return; }
    }
    const it = M.items.find(i => i.f === f && !i.collected && i.seen && (i.kind === 'evidence' || i.kind === 'contraband') && Math.hypot(i.x - wx, i.y - wy) < 16);
    if (it) { sim.orderInteract(s, it, 'collect', append); S().ui(); return; }
    const d = sim.doorAt(f, tx, ty);
    if (d && sim.tileAt(f, tx, ty) === T.DOOR) { openDoorMenu(d, sx, sy, append); return; }
    if (!sim.walkable(f, tx, ty, 'op') && sim.tileAt(f, tx, ty) !== T.WINDOW) { S().error(); return; }
    sim.orderMove(s, tx, ty, f, append);
    if (faceAng !== undefined) for (const u of s) u.watchAngle = faceAng;
    S().ui();
  }

  // ---------- context menu ----------
  function closeMenu() { if (menu) { menu.remove(); menu = null; } }
  function openDoorMenu(d, sx, sy, append) {
    closeMenu();
    const s = sel();
    const has = fn => s.some(fn);
    const items = [];
    const st = d.state;
    items.push(['hdr', d.exterior ? 'Exterior Door' : 'Door' + (d.knownLocked ? (d.barricaded ? ' (BARRICADED)' : d.locked ? ' (LOCKED)' : '') : '')]);
    if (st === 'closed') {
      items.push(['openclear', 'Open & Clear']);
      if (has(u => u.util.includes('flash'))) items.push(['flashclear', 'Flashbang & Clear']);
      items.push(['kickclear', 'Kick & Clear']);
      if (has(u => u.util.includes('charge'))) items.push(['charge', 'Breaching Charge & Clear']);
      if (has(u => u.weapons.some(w => w.st.cat === 'sg'))) items.push(['shotgun', 'Shotgun Breach & Clear']);
      if (has(u => u.tool === 'ram')) items.push(['ram', 'Ram & Clear']);
      if (has(u => u.tool === 'lockpick')) items.push(['pick', 'Pick Lock (quiet)']);
      if (has(u => u.tool === 'doorcam')) items.push(['cam', 'Inspect (Door Camera)']);
      items.push(['open', 'Open Only']);
    } else {
      items.push(['openclear', 'Clear Room']);
      if (has(u => u.util.includes('flash'))) items.push(['flashclear', 'Flashbang & Clear']);
      if (st === 'open') items.push(['close', 'Close Door']);
    }
    items.push(['stack', 'Stack Up']);
    menu = BP.util.el('div', { class: 'ctxmenu', style: `left:${Math.min(sx, canvas.clientWidth - 200)}px;top:${Math.min(sy, canvas.clientHeight - items.length * 30)}px` });
    for (const [id, label] of items) {
      if (id === 'hdr') { menu.append(BP.util.el('div', { class: 'hdr' }, label)); continue; }
      menu.append(BP.util.el('button', { onclick: () => { sim.orderDoor(s, d, id, append || keys.ShiftLeft || keys.ShiftRight); closeMenu(); S().ui(); } }, label));
    }
    document.getElementById('scr-mission').append(menu);
  }

  // ---------- HUD ----------
  function buildHUD() {
    hud.innerHTML = '';
    const el = BP.util.el;
    hud.append(el('div', { class: 'hud-top', id: 'h-top' },
      el('span', { class: 'phase', id: 'h-phase' }, 'PLANNING'),
      el('span', { class: 'clock', id: 'h-clock' }, '00:00'),
      el('button', { class: 'primary', id: 'h-exec', onclick: () => execute() }, 'EXECUTE ▶'),
      el('button', { id: 'h-pause', onclick: () => togglePause() }, 'Pause ⏸'),
      el('button', { id: 'h-go', onclick: () => sim.go() }, 'GO ⏎'),
      el('button', { class: 'sm', id: 'h-spd', onclick: () => setSpeed(M.speed >= 2 ? 0.5 : M.speed * 2) }, '×1'),
      el('button', { class: 'sm', id: 'h-complete', onclick: () => endMission(true) }, 'Complete Op'),
      el('button', { class: 'sm danger', onclick: () => confirmAbort() }, 'Abort'),
      el('button', { class: 'sm ghost', onclick: () => { help = !help; buildHelp(); } }, '?'),
    ));
    hud.append(el('div', { class: 'hud-obj', id: 'h-obj' }));
    const fl = el('div', { class: 'hud-floor', id: 'h-floor' });
    M.map.floors.forEach((_, i) => fl.append(el('button', { class: 'sm', 'data-f': i, onclick: () => { floorView = i; } }, M.map.labels[i] || 'F' + i)));
    hud.append(fl);
    hud.append(el('div', { class: 'hud-log', id: 'h-log' }));
    hud.append(el('div', { class: 'hud-ops', id: 'h-ops' }));
    const cmds = [['Hold', 'H', () => sim.orderHold(sel())], ['Follow', 'F', () => onKey({ code: 'KeyF' }, true)], ['Regroup', 'R', regroup], ['Cover', 'V', () => sim.orderCover(sel())],
      ['Crouch', 'C', () => sim.toggleCrouch(sel())], ['Observe', 'O', () => sim.orderFace(sel(), mouse.wx, mouse.wy)], ['Pace', 'Z', cyclePace], ['Cancel', 'X', () => sim.orderCancel(sel())],
      ['Flash', 'G', () => setMode('flash')], ['Smoke', 'T', () => setMode('smoke')], ['Gas', 'Y', () => setMode('gas')], ['Divert', 'U', () => setMode('stinger')],
      ['Wait GO', 'B', () => sim.orderGo(sel(), true)], ['GO', '⏎', () => sim.go()], ['Formation', 'N', () => onKey({ code: 'KeyN' }, true)], ['Floor', 'Tab', () => onKey({ code: 'Tab', preventDefault() { } }, true)]];
    const cb = el('div', { class: 'hud-cmd' });
    for (const [n, k, fn] of cmds) cb.append(el('button', { onclick: () => { fn(); S().ui(); } }, n, el('kbd', {}, k)));
    hud.append(cb);
    hud.append(el('button', { class: 'hud-objtoggle', onclick: () => document.getElementById('scr-mission').classList.toggle('obj-open') }, 'Objectives'));
    if (isTouch) buildTouchBar();
    buildHelp();
  }
  function buildHelp() {
    const old = document.getElementById('h-help'); if (old) old.remove();
    if (!help) return;
    hud.append(BP.util.el('div', { class: 'hud-help', id: 'h-help', html: `<b class="acc">TOUCH</b><br><b>Tap operator</b> select (Multi toggle to add) · <b>Tap ground/door/suspect/item</b> give order · <b>Long-press</b> add waypoint · <b>Drag</b> pan · <b>Pinch</b> zoom · <b>Twist</b> rotate · <b>Orders</b> button opens the command panel<br><br><b class="acc">CONTROLS</b><br>
      <b>LMB</b> select / drag-box · <b>1–8</b> select operator (twice = center)<br>
      <b>RMB</b> move · <b>Shift+RMB</b> add waypoint · <b>RMB-drag</b> move + facing<br>
      <b>RMB door</b> breach menu · <b>RMB suspect/civ/item/teammate</b> arrest / secure / collect / treat<br>
      <b>Space</b> pause (orders while paused) · <b>[ ]</b> time scale<br>
      <b>G/T/Y/U</b> flash/smoke/gas/diversion then click<br>
      <b>B</b> queue wait-for-GO · <b>Enter</b> GO / Execute<br>
      <b>H</b> hold · <b>F</b> follow first selected · <b>R</b> regroup · <b>V</b> cover · <b>C</b> crouch · <b>O</b> face cursor · <b>Z</b> pace · <b>N</b> formation · <b>X</b> cancel<br>
      <b>WASD/Arrows</b> pan · <b>wheel</b> zoom · <b>MMB</b> drag pan · <b>Q/E</b> rotate · <b>Tab</b> floor · <b>L</b> camera lock` }));
  }
  function updateHUD() {
    const $ = id => document.getElementById(id);
    $('h-phase').textContent = M.phase === 'plan' ? 'PLANNING' : M.paused ? 'PAUSED' : 'LIVE';
    $('h-phase').style.color = M.phase === 'plan' ? '#4fc3f7' : M.paused ? '#e2a33a' : '#5cb85c';
    $('h-clock').textContent = U.fmtTime(M.t);
    $('h-exec').style.display = M.phase === 'plan' ? '' : 'none';
    $('h-pause').style.display = M.phase === 'plan' ? 'none' : '';
    $('h-spd').textContent = '×' + M.speed;
    $('h-complete').className = 'sm' + (M.canComplete ? ' primary' : '');
    const waiting = ops().some(u => u.orders[0] && u.orders[0].t === 'go');
    $('h-go').className = waiting ? 'primary' : '';
    // floors
    for (const b of document.querySelectorAll('#h-floor button')) { const f = +b.dataset.f; const n = ops().filter(u => u.floor === f && !u.dead).length; b.className = 'sm' + (f === floorView ? ' on' : ''); b.textContent = (M.map.labels[f] || 'F' + f) + (n ? ` (${n})` : ''); }
    // objectives
    const objs = M.objectives || sim.objectives();
    $('h-obj').innerHTML = `<h3>${M.cfg.name}</h3>` + objs.map(o => `<div style="color:${o.done ? '#5cb85c' : o.fail ? '#d9534f' : o.req ? '#c9d3dc' : '#8a96a2'}">${o.done ? '✔' : o.fail ? '✖' : '☐'} ${o.text}${o.req ? '' : ' <span class="dim">(opt)</span>'}</div>`).join('') + (M.night ? '<div class="dim">🌙 Night operation</div>' : '') + (M.rain ? '<div class="dim">🌧 Rain — reduced hearing</div>' : '');
    // log
    if (M.log.length !== lastLogLen) {
      lastLogLen = M.log.length;
      $('h-log').innerHTML = M.log.slice(-14).reverse().map(l => `<div style="color:${l.color}"><span class="dim">${U.fmtTime(l.t)}</span> ${l.text}</div>`).join('');
    }
    // op cards
    const oc = $('h-ops');
    const list = ops();
    if (oc.children.length !== list.length) {
      oc.innerHTML = '';
      list.forEach((u, i) => {
        const c = BP.util.el('div', { class: 'opcard', onclick: e => { if (e.shiftKey) selected = selected.includes(u) ? selected.filter(x => x !== u) : [...selected, u]; else selected = [u]; if (u.floor !== floorView) floorView = u.floor; S().ui(); }, ondblclick: () => centerOn(u) });
        c.innerHTML = `<div class="key">${i + 1}</div><div class="nm"><span class="swatch" style="background:${u.color}"></span>${u.name} <span class="dim" style="font-weight:400">${D.ROLE[u.op.role].name}</span></div><div class="hpb"><i></i></div><div class="ammo"></div><div class="st"></div>`;
        oc.append(c);
      });
    }
    list.forEach((u, i) => {
      const c = oc.children[i]; if (!c) return;
      c.className = 'opcard' + (selected.includes(u) ? ' sel' : '') + (u.downed || u.dead ? ' down' : '');
      const hp = Math.max(0, u.hp / u.maxHp);
      const bar = c.querySelector('.hpb i'); bar.style.width = (hp * 100) + '%'; bar.style.background = hp > 0.6 ? '#5cb85c' : hp > 0.3 ? '#e2a33a' : '#d9534f';
      const w = u.weapons[u.wi];
      const utilC = {}; u.util.forEach(x => utilC[x] = (utilC[x] || 0) + 1);
      const utilTxt = Object.entries(utilC).map(([k, v]) => `${{ flash: 'FB', smoke: 'SM', gas: 'CS', stinger: 'DV', charge: 'C4' }[k] || k}×${v}`).join(' ');
      c.querySelector('.ammo').innerHTML = `${w.st.name} <b>${w.mag}</b>/${w.reserve} ${u.pace === 'run' ? '»' : u.pace === 'slow' ? '·' : '›'}${u.stance === 'crouch' ? ' ⤓' : ''}<br><span class="dim">${utilTxt || '—'}${u.tool ? ' · ' + D.ITEM[u.tool].name + (u.tool === 'medkit' ? '(' + u.toolUses + ')' : '') : ''}</span>`;
      c.querySelector('.st').textContent = u.dead ? 'KIA' : (u.status || '') + (u.orders.length > 1 ? ` [+${u.orders.length - 1}]` : '') + (u.stress > 50 ? ' · stressed' : '');
    });
  }
  function confirmAbort() {
    const m = document.getElementById('modal');
    m.innerHTML = ''; m.classList.remove('hidden');
    const box = BP.util.el('div', { class: 'modal-box' }, BP.util.el('h2', {}, 'Abort operation?'), BP.util.el('p', {}, 'The squad will extract immediately. Remaining objectives count as failed.'),
      BP.util.el('div', { class: 'row' }, BP.util.el('button', { class: 'danger', onclick: () => { m.classList.add('hidden'); endMission(false, 'Operation aborted'); } }, 'Abort & Extract'), BP.util.el('button', { onclick: () => m.classList.add('hidden') }, 'Continue')));
    m.append(box);
  }
  function endMission(success, reason) {
    if (M.ended && M.result) { finishUp(); return; }
    sim.finish(success, reason);
    finishUp();
  }
  function finishUp() {
    stop();
    BP.audio.stopAmbient();
    if (window.speechSynthesis) speechSynthesis.cancel();
    if (onEnd) onEnd(M, M.result);
  }

  // ---------- overlay drawing ----------
  function overlay(c, setWorld, toScreen) {
    const t = performance.now() / 1000;
    for (const u of ops()) {
      if (u.floor !== floorView && !u.orders.length) continue;
      const isSel = selected.includes(u);
      const alpha = isSel ? 0.95 : 0.4;
      c.strokeStyle = u.color; c.fillStyle = u.color; c.globalAlpha = alpha; c.lineWidth = isSel ? 2 : 1.3;
      let px = u.x, py = u.y, pf = u.floor;
      // current path
      if (u.path && u.floor === floorView) {
        c.setLineDash([2, 4]); c.beginPath(); c.moveTo(u.x, u.y);
        for (let i = u.pi; i < u.path.length; i++) { const n = u.path[i]; if (n.f !== floorView) break; c.lineTo((n.x + 0.5) * TS, (n.y + 0.5) * TS); }
        c.stroke(); c.setLineDash([]);
      }
      let n = 0;
      for (const o of u.orders) {
        n++;
        if (o.t === 'move') {
          const x = (o.x + 0.5) * TS, y = (o.y + 0.5) * TS;
          if (o.f === floorView) {
            if (pf === floorView && !(n === 1 && u.path)) { c.beginPath(); c.moveTo(px, py); c.lineTo(x, y); c.stroke(); }
            c.beginPath(); c.arc(x, y, 5, 0, 7); c.fill();
            if (o.pace === 'run') { c.font = 'bold 10px Arial'; c.fillText('»', x + 7, y - 4); }
          }
          px = x; py = y; pf = o.f;
        } else if (o.t === 'door') {
          const d = M.map.doors[o.door]; if (d.f !== floorView) continue;
          const x = (d.x + 0.5) * TS, y = (d.y + 0.5) * TS;
          c.save(); c.globalAlpha = alpha; c.fillStyle = '#111'; c.beginPath(); c.arc(x, y, 9, 0, 7); c.fill(); c.fillStyle = u.color; c.font = 'bold 10px Arial'; c.textAlign = 'center';
          c.fillText({ entry: 'O', open: 'O', kick: 'K', ram: 'R', shotgun: 'S', charge: 'C4', pick: 'L', cam: '◉', close: 'X' }[o.a] || 'D', x, y + 4); c.restore();
        } else if (o.t === 'throw') {
          if (o.f !== floorView) continue;
          c.setLineDash([3, 3]); c.beginPath(); c.moveTo(px, py); c.quadraticCurveTo((px + o.x) / 2, (py + o.y) / 2 - 30, o.x, o.y); c.stroke(); c.setLineDash([]);
          c.beginPath(); c.arc(o.x, o.y, 6, 0, 7); c.stroke(); c.font = 'bold 9px Arial'; c.textAlign = 'center'; c.fillText({ flash: 'FLASH', smoke: 'SMOKE', gas: 'GAS', stinger: 'DIV' }[o.g], o.x, o.y - 9);
        } else if (o.t === 'go' || o.t === 'waitdoor') {
          if (pf !== floorView) continue;
          c.save(); c.globalAlpha = alpha; c.fillStyle = o.t === 'go' ? '#e2a33a' : '#4fc3f7'; c.font = 'bold 10px Arial'; c.textAlign = 'center'; c.fillText(o.t === 'go' ? 'GO?' : 'STACK', px, py - 10); c.restore();
        } else if (o.t === 'interact') {
          const tgt = o.kind === 'collect' ? M.items.find(i => i.id === o.target) : M.units.find(x => x.id === o.target);
          if (tgt && (tgt.floor ?? tgt.f) === floorView) { c.setLineDash([1, 3]); c.beginPath(); c.moveTo(px, py); c.lineTo(tgt.x, tgt.y); c.stroke(); c.setLineDash([]); c.font = 'bold 9px Arial'; c.textAlign = 'center'; c.fillText(o.kind.toUpperCase(), tgt.x, tgt.y - 14); }
        }
      }
      c.globalAlpha = 1;
      if (u.floor !== floorView) continue;
      // selection
      if (isSel) { c.strokeStyle = '#fff'; c.globalAlpha = 0.6 + 0.3 * Math.sin(t * 5); c.lineWidth = 1.5; c.beginPath(); c.arc(u.x, u.y, 15, 0, 7); c.stroke(); c.globalAlpha = 1; }
      if (u.watchAngle !== null && u.watchAngle !== undefined && isSel) { c.strokeStyle = u.color; c.beginPath(); c.moveTo(u.x, u.y); c.lineTo(u.x + Math.cos(u.watchAngle) * 40, u.y + Math.sin(u.watchAngle) * 40); c.stroke(); }
      if (u.act && u.act.dur) { const k = Math.min(1, u.act.t / u.act.dur); c.strokeStyle = '#e2a33a'; c.lineWidth = 3; c.beginPath(); c.arc(u.x, u.y, 19, -Math.PI / 2, -Math.PI / 2 + k * Math.PI * 2); c.stroke(); }
      const io = u.orders[0]; if (io && io.t === 'interact' && io.el) { const k = Math.min(1, io.el / io.dur); c.strokeStyle = '#5cb85c'; c.lineWidth = 3; c.beginPath(); c.arc(u.x, u.y, 19, -Math.PI / 2, -Math.PI / 2 + k * Math.PI * 2); c.stroke(); }
      if (u.reloadT > 0) { c.fillStyle = '#e2a33a'; c.font = 'bold 9px Arial'; c.textAlign = 'center'; c.fillText('RELOAD', u.x, u.y + 26); }
    }
    // hover door highlight
    const tx = sim.toTile(mouse.wx), ty = sim.toTile(mouse.wy);
    if (sim.tileAt(floorView, tx, ty) === T.DOOR) { c.strokeStyle = '#e2a33a'; c.lineWidth = 2; c.strokeRect(tx * TS, ty * TS, TS, TS); }
    // throw mode preview
    if (mode) {
      const s = sel().filter(u => u.util.includes(mode.g) && u.floor === floorView);
      const u = s.sort((a, b) => Math.hypot(a.x - mouse.wx, a.y - mouse.wy) - Math.hypot(b.x - mouse.wx, b.y - mouse.wy))[0];
      if (u) {
        c.strokeStyle = '#e2a33a'; c.setLineDash([4, 4]); c.beginPath(); c.arc(u.x, u.y, (u.perks.includes('grenadier') ? 13 : 10) * TS, 0, 7); c.stroke(); c.setLineDash([]);
        c.beginPath(); c.moveTo(u.x, u.y); c.quadraticCurveTo((u.x + mouse.wx) / 2, (u.y + mouse.wy) / 2 - 30, mouse.wx, mouse.wy); c.stroke();
        c.strokeStyle = 'rgba(255,255,255,0.5)'; c.beginPath(); c.arc(mouse.wx, mouse.wy, mode.g === 'flash' ? 6 * TS : mode.g === 'smoke' ? 3.3 * TS : mode.g === 'gas' ? 3.6 * TS : 2 * TS, 0, 7); c.stroke();
      }
    }
    // drag box
    if (drag && drag.btn === 0 && Math.hypot(mouse.x - drag.x, mouse.y - drag.y) > 6) {
      c.setTransform(1, 0, 0, 1, 0, 0);
      c.strokeStyle = '#e2a33a'; c.lineWidth = 1; c.fillStyle = 'rgba(226,163,58,0.08)';
      const x0 = Math.min(mouse.x, drag.x) * dpr, y0 = Math.min(mouse.y, drag.y) * dpr, w = Math.abs(mouse.x - drag.x) * dpr, h = Math.abs(mouse.y - drag.y) * dpr;
      c.fillRect(x0, y0, w, h); c.strokeRect(x0, y0, w, h);
    }
    // ops on other floors indicator
    c.setTransform(1, 0, 0, 1, 0, 0);
    // banner
    if (banner && bannerT > 0) { c.fillStyle = 'rgba(226,163,58,' + Math.min(1, bannerT) + ')'; c.font = `800 ${28 * dpr}px Arial`; c.textAlign = 'center'; c.fillText(banner, canvas.width / 2, canvas.height * 0.22); if (M.phase === 'plan') { c.font = `${13 * dpr}px Arial`; c.fillStyle = 'rgba(200,210,220,0.85)'; c.fillText('Time is frozen. Plan routes, breaches and GO-codes, then press EXECUTE (Enter).', canvas.width / 2, canvas.height * 0.22 + 26 * dpr); } }
    if (M.canComplete && M.phase === 'live') { c.fillStyle = '#5cb85c'; c.font = `700 ${15 * dpr}px Arial`; c.textAlign = 'center'; c.fillText('AREA SECURE — press "Complete Op" when ready', canvas.width / 2, 72 * dpr); }
  }

  // ---------- loop ----------
  function loop(now) {
    if (!running) return;
    const rdt = Math.min(0.05, (now - lastT) / 1000); lastT = now;
    // camera keys
    const pan = 600 * rdt / cam.zoom;
    let mx = 0, my = 0;
    if (keys.KeyW || keys.ArrowUp) my -= pan; if (keys.KeyS || keys.ArrowDown) my += pan;
    if (keys.KeyA && !keys.ControlLeft || keys.ArrowLeft) mx -= pan; if (keys.KeyD || keys.ArrowRight) mx += pan;
    if (mx || my) { const c = Math.cos(-cam.rot), s = Math.sin(-cam.rot); cam.x += mx * c - my * s; cam.y += mx * s + my * c; }
    cam.rot += (cam.tRot - cam.rot) * Math.min(1, rdt * 8);
    const prevFloors = new Map(selected.map(u => [u, u.floor]));
    sim.update(rdt);
    // auto-follow floor of the primary selected operator
    if (selected[0] && prevFloors.get(selected[0]) !== selected[0].floor && prevFloors.get(selected[0]) === floorView) floorView = selected[0].floor;
    if (lockCam && selected[0]) { cam.x += (selected[0].x - cam.x) * Math.min(1, rdt * 6); cam.y += (selected[0].y - cam.y) * Math.min(1, rdt * 6); floorView = selected[0].floor; }
    cam.x = U.clamp(cam.x, 0, M.map.w * TS); cam.y = U.clamp(cam.y, 0, M.map.h * TS);
    // fx
    for (const e of M.fx) R.spawnFx(scene, e, M.map);
    M.fx = [];
    R.updateParticles(scene, M.paused ? 0 : rdt * M.speed, M.map);
    if (M.paused) { visT -= rdt; if (visT <= 0) { visT = 0.25; sim.refreshVisibility(); } }
    bannerT -= rdt;
    // audio listener
    const L = BP.audio.listener; L.x = cam.x; L.y = cam.y; L.range = Math.max(canvas.clientWidth, canvas.clientHeight) / 2 / cam.zoom; L.rot = cam.rot;
    // vision polys
    const vision = ops().filter(u => u.floor === floorView && sim.canAct(u)).map(u => sim.visionPoly(u));
    const view = {
      map: M.map, floor: floorView, cam, units: M.units, items: M.items, grenades: M.grenades, smokes: M.smokes, gases: M.gases, scene, t: now / 1000, time: M.t,
      vision, camVision: M.camSources.filter(c => c.f === floorView), night: M.night, rain: M.rain, heard: M.heard, overlay, showAll: false, showHints: M.diff.showEnemies,
      vehicle, showSpawns: M.phase === 'plan', dpr,
    };
    R.draw(ctx, view);
    hudT -= rdt;
    if (hudT <= 0) { hudT = 0.1; updateHUD(); }
    if (M.ended) { finishUp(); return; }
    requestAnimationFrame(loop);
  }

  return { start, stop, get M() { return M; }, w2s: (x, y) => worldToScreen(x, y) };
})();
