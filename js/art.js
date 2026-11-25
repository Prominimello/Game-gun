// Procedural art: portraits, weapon renders, top-down sprites, vehicles, icons
'use strict';
BP.art = (() => {
  const D = BP.D, U = BP.util;

  function rr(ctx, x, y, w, h, r) {
    r = Math.min(r, Math.abs(w) / 2, Math.abs(h) / 2);
    ctx.beginPath(); ctx.moveTo(x + r, y); ctx.lineTo(x + w - r, y); ctx.quadraticCurveTo(x + w, y, x + w, y + r);
    ctx.lineTo(x + w, y + h - r); ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h); ctx.lineTo(x + r, y + h);
    ctx.quadraticCurveTo(x, y + h, x, y + h - r); ctx.lineTo(x, y + r); ctx.quadraticCurveTo(x, y, x + r, y); ctx.closePath();
  }

  function uniformColors(look) {
    const u = D.UNIF[look.uniform] || D.UNIFORMS[0];
    return { shirt: look.shirt || u.shirt, pants: look.pants || u.pants, carrier: look.carrier || u.carrier, helmet: u.helmet ? (look.helmetColor || u.helmet) : null, glove: u.glove, patch: u.patch, camo: u.camo };
  }

  function camoFill(ctx, colors, seed, x, y, w, h, size = 8) {
    const r = U.rng(seed);
    for (let i = 0; i < (w * h) / (size * size) * 1.4; i++) {
      ctx.fillStyle = colors[i % colors.length];
      ctx.beginPath();
      const cx = x + r() * w, cy = y + r() * h, rx = size * r.range(0.5, 1.3), ry = size * r.range(0.3, 0.9);
      ctx.ellipse(cx, cy, rx, ry, r() * 3, 0, Math.PI * 2); ctx.fill();
    }
  }

  // ---------- Portrait ----------
  function portrait(ctx, op, W, H, { bg = true, gear = true } = {}) {
    const L = op.look, uc = uniformColors(L);
    const skin = D.SKIN[L.skin] || D.SKIN[2];
    const hairC = D.HAIR_COLORS[L.hairColor] || '#222';
    const seed = U.hashStr(op.id || op.name);
    ctx.save();
    if (bg) {
      const g = ctx.createLinearGradient(0, 0, 0, H); g.addColorStop(0, '#2a3440'); g.addColorStop(1, '#10151b');
      ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
      ctx.strokeStyle = 'rgba(255,255,255,0.04)'; for (let i = 0; i < H; i += 4) { ctx.beginPath(); ctx.moveTo(0, i); ctx.lineTo(W, i); ctx.stroke(); }
    }
    const s = W / 100; ctx.scale(s, H / 100 * (100 / 100));
    const sy = H / s / 100; // keep proportional
    ctx.setTransform(s, 0, 0, s, 0, (H - 100 * s));
    const build = [0.88, 1, 1.08, 1.18][L.build] || 1;
    const fem = op.gender === 'f';
    // shoulders / torso
    const sw = 42 * build * (fem ? 0.9 : 1);
    ctx.fillStyle = uc.shirt;
    ctx.beginPath(); ctx.moveTo(50 - sw, 100); ctx.quadraticCurveTo(50 - sw, 72, 50 - 14, 68); ctx.lineTo(50 + 14, 68); ctx.quadraticCurveTo(50 + sw, 72, 50 + sw, 100); ctx.closePath(); ctx.fill();
    if (uc.camo) { ctx.save(); ctx.clip(); camoFill(ctx, ['#5e5a3d', '#a8946b', '#3f3b2b', '#71704f'], seed, 0, 60, 100, 40, 6); ctx.restore(); }
    // plate carrier
    if (gear) {
      ctx.fillStyle = uc.carrier; rr(ctx, 50 - 24 * build, 76, 48 * build, 30, 4); ctx.fill();
      ctx.fillStyle = U.shade(uc.carrier, -0.25);
      for (let i = 0; i < 3; i++) { rr(ctx, 50 - 20 * build + i * 14 * build, 86, 11 * build, 14, 2); ctx.fill(); }
      ctx.fillStyle = U.shade(uc.carrier, 0.15); ctx.fillRect(50 - 24 * build, 76, 48 * build, 3);
      // shoulder straps
      ctx.fillStyle = uc.carrier; ctx.fillRect(50 - 20 * build, 68, 7, 10); ctx.fillRect(50 + 13 * build, 68, 7, 10);
      // patch text on carrier
      const patchTxt = uc.patch;
      if (patchTxt) { ctx.fillStyle = '#d8d8d0'; ctx.font = 'bold 5px Arial'; ctx.textAlign = 'center'; ctx.fillText(patchTxt, 50, 83); }
      // shoulder patch
      if (L.patch > 0) {
        const px = 50 - sw + 6, py = 80;
        ctx.fillStyle = ['#000', '#2b4a7a', '#7a6a2b', '#8a2b2b', '#333', '#222', '#eee'][L.patch];
        rr(ctx, px, py, 9, 7, 1.5); ctx.fill();
        if (L.patch === 1) { ctx.fillStyle = '#c33'; ctx.fillRect(px + 1, py + 4, 7, 1); ctx.fillStyle = '#fff'; ctx.fillRect(px + 1, py + 2, 7, 1); }
        if (L.patch === 6) { ctx.fillStyle = '#c33'; ctx.fillRect(px + 3.5, py + 1, 2, 5); ctx.fillRect(px + 2, py + 2.5, 5, 2); }
        if (L.patch === 5) { ctx.fillStyle = '#ddd'; ctx.beginPath(); ctx.arc(px + 4.5, py + 3, 2, 0, 7); ctx.fill(); }
      }
    }
    // neck
    ctx.fillStyle = U.shade(skin, -0.12); ctx.fillRect(42, 56, 16, 16);
    // head
    const faceW = [17, 19, 19.5, 16, 20, 18][L.face] * (fem ? 0.93 : 1);
    const jaw = [0.75, 0.95, 1, 0.7, 1, 0.85][L.face];
    const hx = 50, hy = 40;
    ctx.fillStyle = skin;
    ctx.beginPath();
    ctx.moveTo(hx - faceW, hy - 6);
    ctx.quadraticCurveTo(hx - faceW, hy - 28, hx, hy - 28);
    ctx.quadraticCurveTo(hx + faceW, hy - 28, hx + faceW, hy - 6);
    ctx.quadraticCurveTo(hx + faceW, hy + 12, hx + faceW * jaw * 0.7, hy + 18);
    ctx.quadraticCurveTo(hx, hy + 26 - (fem ? 2 : 0), hx - faceW * jaw * 0.7, hy + 18);
    ctx.quadraticCurveTo(hx - faceW, hy + 12, hx - faceW, hy - 6);
    ctx.fill();
    // ears
    ctx.beginPath(); ctx.ellipse(hx - faceW, hy, 3, 5, 0, 0, 7); ctx.ellipse(hx + faceW, hy, 3, 5, 0, 0, 7); ctx.fill();
    // shading
    ctx.fillStyle = 'rgba(0,0,0,0.08)'; ctx.beginPath(); ctx.ellipse(hx + faceW * 0.5, hy + 4, faceW * 0.4, 18, 0, 0, 7); ctx.fill();
    if (L.face === 5) { ctx.strokeStyle = 'rgba(0,0,0,0.15)'; ctx.lineWidth = 0.6; ctx.beginPath(); ctx.moveTo(hx - 10, hy - 14); ctx.lineTo(hx - 3, hy - 15); ctx.moveTo(hx + 4, hy - 15); ctx.lineTo(hx + 10, hy - 14); ctx.stroke(); }
    // hair (back / top)
    const helmet = gear && L.helmet && uc.helmet;
    if (!helmet && !L.balaclava) drawHair(ctx, L.hair, hairC, hx, hy, faceW, fem);
    // eyes
    const ey = hy - 4;
    ctx.fillStyle = '#f4f1ea'; ctx.beginPath(); ctx.ellipse(hx - 7, ey, 3.4, 1.9, 0, 0, 7); ctx.ellipse(hx + 7, ey, 3.4, 1.9, 0, 0, 7); ctx.fill();
    const iris = ['#4a3020', '#2d5a8a', '#3d6b3a', '#6b5a3a', '#222'][seed % 5];
    ctx.fillStyle = iris; ctx.beginPath(); ctx.arc(hx - 6.6, ey, 1.6, 0, 7); ctx.arc(hx + 7.4, ey, 1.6, 0, 7); ctx.fill();
    ctx.fillStyle = '#000'; ctx.beginPath(); ctx.arc(hx - 6.6, ey, 0.7, 0, 7); ctx.arc(hx + 7.4, ey, 0.7, 0, 7); ctx.fill();
    // brows
    ctx.strokeStyle = hairC; ctx.lineWidth = fem ? 1.2 : 1.9; ctx.lineCap = 'round';
    const browTilt = (seed >> 3) % 3 - 1;
    ctx.beginPath(); ctx.moveTo(hx - 11, ey - 5 + browTilt * 0.5); ctx.lineTo(hx - 3, ey - 5.5); ctx.moveTo(hx + 3, ey - 5.5); ctx.lineTo(hx + 11, ey - 5 + browTilt * 0.5); ctx.stroke();
    // nose & mouth
    ctx.strokeStyle = U.shade(skin, -0.35); ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(hx, ey + 1); ctx.lineTo(hx - 2, ey + 10); ctx.lineTo(hx + 1.5, ey + 11); ctx.stroke();
    ctx.strokeStyle = fem ? '#9a4a4a' : U.shade(skin, -0.45); ctx.lineWidth = fem ? 1.6 : 1.2;
    ctx.beginPath(); ctx.moveTo(hx - 5, ey + 17); ctx.quadraticCurveTo(hx, ey + 18 + (seed % 2), hx + 5, ey + 17); ctx.stroke();
    // facial hair
    if (!L.balaclava) drawFacial(ctx, L.facial, hairC, hx, hy, faceW, jaw);
    // balaclava
    if (L.balaclava && gear) {
      ctx.fillStyle = '#16181b';
      ctx.beginPath();
      ctx.moveTo(hx - faceW - 3, hy + 30); ctx.lineTo(hx - faceW - 3, hy - 8); ctx.quadraticCurveTo(hx - faceW - 2, hy - 31, hx, hy - 31);
      ctx.quadraticCurveTo(hx + faceW + 2, hy - 31, hx + faceW + 3, hy - 8); ctx.lineTo(hx + faceW + 3, hy + 30); ctx.closePath();
      // eye cutout
      ctx.moveTo(hx + 13, ey - 7); ctx.lineTo(hx - 13, ey - 7); ctx.lineTo(hx - 13, ey + 4); ctx.lineTo(hx + 13, ey + 4); ctx.closePath();
      ctx.fill('evenodd');
    }
    // helmet
    if (helmet) {
      ctx.fillStyle = uc.helmet;
      ctx.beginPath(); ctx.moveTo(hx - faceW - 4, hy - 6); ctx.quadraticCurveTo(hx - faceW - 4, hy - 36, hx, hy - 36); ctx.quadraticCurveTo(hx + faceW + 4, hy - 36, hx + faceW + 4, hy - 6);
      ctx.lineTo(hx + faceW + 1, hy - 6); ctx.lineTo(hx + faceW - 2, hy - 14); ctx.lineTo(hx - faceW + 2, hy - 14); ctx.lineTo(hx - faceW - 1, hy - 6); ctx.closePath(); ctx.fill();
      ctx.fillStyle = U.shade(uc.helmet, 0.12); ctx.beginPath(); ctx.ellipse(hx - 6, hy - 28, 8, 3, -0.3, 0, 7); ctx.fill();
      ctx.fillStyle = U.shade(uc.helmet, -0.3); ctx.fillRect(hx - faceW - 3, hy - 16, 4, 9); ctx.fillRect(hx + faceW - 1, hy - 16, 4, 9);
      ctx.fillStyle = '#111'; rr(ctx, hx - 5, hy - 36, 10, 5, 1); ctx.fill(); // NVG mount
    }
    // eyepro
    if (gear && L.eyepro > 0) {
      if (L.eyepro === 2) { ctx.fillStyle = 'rgba(30,30,30,0.95)'; rr(ctx, hx - 15, ey - 6, 30, 11, 4); ctx.fill(); ctx.fillStyle = 'rgba(170,200,220,0.55)'; rr(ctx, hx - 13, ey - 4.5, 26, 8, 3); ctx.fill(); ctx.fillStyle = '#111'; ctx.fillRect(hx - faceW - 3, ey - 2, 6, 3); ctx.fillRect(hx + faceW - 3, ey - 2, 6, 3); }
      else {
        ctx.fillStyle = L.eyepro === 3 ? 'rgba(15,15,20,0.88)' : 'rgba(200,220,235,0.35)';
        rr(ctx, hx - 13, ey - 4, 11.5, 7, 2.5); ctx.fill(); rr(ctx, hx + 1.5, ey - 4, 11.5, 7, 2.5); ctx.fill();
        ctx.strokeStyle = '#222'; ctx.lineWidth = 0.8; ctx.beginPath(); ctx.moveTo(hx - 1.5, ey - 2); ctx.lineTo(hx + 1.5, ey - 2); ctx.moveTo(hx - 13, ey - 2); ctx.lineTo(hx - faceW, ey - 1); ctx.moveTo(hx + 13, ey - 2); ctx.lineTo(hx + faceW, ey - 1); ctx.stroke();
      }
    }
    // headset
    if (gear && L.headset === 2) { ctx.fillStyle = '#2a2d2a'; rr(ctx, hx - faceW - 6, hy - 8, 8, 15, 3); ctx.fill(); rr(ctx, hx + faceW - 2, hy - 8, 8, 15, 3); ctx.fill(); ctx.strokeStyle = '#222'; ctx.lineWidth = 1.2; ctx.beginPath(); ctx.moveTo(hx - faceW + 1, hy + 5); ctx.quadraticCurveTo(hx - 12, hy + 16, hx - 6, hy + 15); ctx.stroke(); }
    else if (gear && L.headset === 1) { ctx.fillStyle = '#222'; ctx.beginPath(); ctx.arc(hx + faceW, hy, 2.2, 0, 7); ctx.fill(); ctx.strokeStyle = '#333'; ctx.lineWidth = 0.7; ctx.beginPath(); ctx.moveTo(hx + faceW, hy + 2); ctx.lineTo(hx + faceW - 2, hy + 22); ctx.stroke(); }
    ctx.restore();
  }

  function drawHair(ctx, style, c, hx, hy, fw, fem) {
    ctx.fillStyle = c;
    const top = () => { ctx.beginPath(); ctx.moveTo(hx - fw - 1, hy - 4); ctx.quadraticCurveTo(hx - fw - 2, hy - 32, hx, hy - 31); ctx.quadraticCurveTo(hx + fw + 2, hy - 32, hx + fw + 1, hy - 4); ctx.lineTo(hx + fw - 1, hy - 12); ctx.quadraticCurveTo(hx, hy - 24, hx - fw + 1, hy - 12); ctx.closePath(); ctx.fill(); };
    switch (style) {
      case 0: ctx.globalAlpha = 0.55; top(); ctx.globalAlpha = 1; break; // buzz
      case 8: break; // shaved
      case 1: case 2: top(); break;
      case 3: top(); ctx.beginPath(); ctx.ellipse(hx + 6, hy - 25, 10, 5, 0.3, 0, 7); ctx.fill(); break;
      case 4: top(); ctx.beginPath(); ctx.ellipse(hx, hy - 28, fw, 6, 0, 0, 7); ctx.fill(); break;
      case 5: top(); ctx.beginPath(); ctx.arc(hx, hy - 34, 7, 0, 7); ctx.fill(); break; // bun
      case 6: top(); ctx.beginPath(); ctx.ellipse(hx + fw + 3, hy + 6, 4, 14, -0.2, 0, 7); ctx.fill(); break; // pony
      case 7: top(); ctx.fillRect(hx - fw - 3, hy - 10, 6, 34); ctx.fillRect(hx + fw - 3, hy - 10, 6, 34); break; // long
      case 9: for (let i = 0; i < 14; i++) { ctx.beginPath(); ctx.arc(hx - fw + (i % 7) * fw / 3, hy - 22 - Math.floor(i / 7) * 6 - Math.sin(i) * 2, 5, 0, 7); ctx.fill(); } break; // curly
      case 10: top(); for (let i = 0; i < 5; i++) ctx.fillRect(hx - fw + i * fw / 2, hy - 30, 3, 10); ctx.fillRect(hx + fw - 2, hy - 8, 4, 30); break; // braids
    }
  }
  function drawFacial(ctx, f, c, hx, hy, fw, jaw) {
    if (!f) return;
    ctx.fillStyle = c;
    const ey = hy - 4;
    if (f === 1) { ctx.globalAlpha = 0.28; ctx.beginPath(); ctx.moveTo(hx - fw + 1, hy + 2); ctx.quadraticCurveTo(hx - fw, hy + 14, hx - fw * jaw * 0.7, hy + 18); ctx.quadraticCurveTo(hx, hy + 27, hx + fw * jaw * 0.7, hy + 18); ctx.quadraticCurveTo(hx + fw, hy + 14, hx + fw - 1, hy + 2); ctx.lineTo(hx + 8, hy + 10); ctx.lineTo(hx - 8, hy + 10); ctx.closePath(); ctx.fill(); ctx.globalAlpha = 1; }
    if (f === 2 || f === 3 || f >= 4) { ctx.beginPath(); ctx.moveTo(hx - 7, ey + 15); ctx.quadraticCurveTo(hx, ey + 12, hx + 7, ey + 15); ctx.lineTo(hx + 6, ey + 16.5); ctx.quadraticCurveTo(hx, ey + 14, hx - 6, ey + 16.5); ctx.closePath(); ctx.fill(); }
    if (f === 3) { ctx.beginPath(); ctx.moveTo(hx - 4, ey + 19); ctx.lineTo(hx + 4, ey + 19); ctx.lineTo(hx + 2, ey + 26); ctx.lineTo(hx - 2, ey + 26); ctx.closePath(); ctx.fill(); }
    if (f >= 4) {
      ctx.globalAlpha = f === 4 ? 0.75 : 0.95;
      ctx.beginPath(); ctx.moveTo(hx - fw + 1, hy); ctx.quadraticCurveTo(hx - fw, hy + 16, hx - fw * jaw * 0.6, hy + 22 + (f === 5 ? 3 : 0)); ctx.quadraticCurveTo(hx, hy + 32 + (f === 5 ? 6 : 0), hx + fw * jaw * 0.6, hy + 22 + (f === 5 ? 3 : 0)); ctx.quadraticCurveTo(hx + fw, hy + 16, hx + fw - 1, hy);
      ctx.lineTo(hx + fw - 4, hy + 4); ctx.quadraticCurveTo(hx + 6, hy + 10, hx + 6, ey + 19); ctx.lineTo(hx - 6, ey + 19); ctx.quadraticCurveTo(hx - 6, hy + 10, hx - fw + 4, hy + 4); ctx.closePath(); ctx.fill(); ctx.globalAlpha = 1;
    }
  }

  // ---------- Weapon side-view ----------
  function weaponParts(w) {
    const b = D.WEAPON[w.base], g = b.geo, att = w.att || {};
    const P = []; // {role, kind, args}
    const rect = (role, x, y, ww, hh, r = 1.5) => P.push({ role, k: 'r', a: [x, y, ww, hh, r] });
    const poly = (role, pts) => P.push({ role, k: 'p', a: pts });
    const circ = (role, x, y, r) => P.push({ role, k: 'c', a: [x, y, r] });
    if (g.pistol) {
      const L = g.recv * 2.6;
      // frame + slide
      poly('poly', [[-6, 4], [L - 6, 4], [L - 6, 10], [8, 10], [2, 30], [-10, 30], [-6, 10]]);
      rect('body', -12, -6, L + 2, 11, 2);
      for (let i = 0; i < 5; i++) rect('dark', -9 + i * 3, -4, 1.2, 7, 0);
      rect('dark', L - 14, -2, 8, 3, 1);
      poly('dark', [[6, 10], [12, 10], [14, 17], [6, 17]]); // trigger guard
      if (att.mag === 'extmag') rect('poly', -10, 29, 11, 9, 1);
      if (att.optic === 'rmr') { rect('metal', 2, -12, 14, 7, 2); rect('glass', 4, -11, 3, 5, 0); }
      if (att.side) { rect('dark', L * 0.45, 10, 16, 8, 2); if (att.side !== 'laser') circ('light', L * 0.45 + 16, 14, 3); if (att.side !== 'light') circ('laser', L * 0.45 + 3, 16, 1.6); }
      if (att.muzzle === 'psupp') rect('dark', L - 10, -6, 42, 10, 3);
      else if (att.muzzle === 'comp') rect('metal', L - 10, -6, 9, 10, 1);
      return { parts: P, len: L + 40, minX: -14 };
    }
    const R = g.recv * 1.55, Hd = (g.hand || 0) * 1.55;
    let barrel = g.barrel * 1.55;
    if (att.barrel === 'shortb') barrel *= 0.55; if (att.barrel === 'longb') barrel *= 1.3;
    const x0 = 0, x1 = R; // receiver
    const bulky = g.bulky;
    // stock
    const stockType = att.stock === 'pdw' ? 'pdw' : att.stock === 'precstock' ? 'prec' : att.stock === 'ctr' ? 'ctr' : g.stock;
    if (stockType === 'collapsible' || stockType === 'ctr') { rect('dark', -34, -2, 36, 5, 1); poly(g.wood ? 'wood' : 'poly', [[-58, -6], [-28, -6], [-26, 0], [-26, 10], [-34, 14], [-58, 16]]); if (stockType === 'ctr') rect('poly', -54, -10, 18, 5, 2); }
    else if (stockType === 'folding') { poly('poly', [[-56, -5], [-4, -4], [-4, 4], [-20, 4], [-48, 14], [-56, 14]]); rect('dark', -48, -1, 26, 4, 1); }
    else if (stockType === 'fixed') poly(g.wood ? 'wood' : 'poly', [[-66, -6], [-2, -4], [-2, 6], [-24, 10], [-66, 20]]);
    else if (stockType === 'chassis' || stockType === 'prec') { poly('body', [[-70, -8], [-2, -5], [-2, 6], [-30, 8], [-50, 22], [-70, 22]]); rect('dark', -66, -14, 16, 6, 2); }
    else if (stockType === 'pdw') { rect('dark', -26, -1, 26, 3, 1); rect('dark', -26, 1, 4, 12, 1); rect('poly', -30, 0, 6, 16, 2); }
    else if (stockType === 'bullpup') { poly('body', [[-36, -8], [0, -8], [0, 14], [-36, 18]]); }
    // receiver
    if (g.ak) { poly('body', [[x0, -8], [x1, -8], [x1 + 4, -2], [x1, 6], [x0, 6]]); rect('dark', x0 + 4, -11, R - 8, 3, 1); }
    else rect('body', x0, -8, R, bulky ? 17 : 14, 2);
    rect('dark', x0 + R * 0.35, -5, R * 0.3, 4, 1); // ejection port
    if (g.rail || att.optic) rect('dark', x0 + 2, -12, R + Hd - 2, 4, 0.5); // top rail
    if (g.bolt) { rect('metal', x0 + R * 0.25, -11, 10, 3, 1); circ('metal', x0 + R * 0.25 + 4, 0, 3); }
    // pistol grip
    const gripT = att.grip;
    if (stockType !== 'bullpup' && !(g.magT === 'grip')) {
      const ang = gripT === 'steep' ? 4 : 10;
      poly(gripT === 'rubber' ? 'rubber' : 'poly', [[x0 + 6, 6], [x0 + 17, 6], [x0 + 14 - ang * 0.3, 30], [x0 + 3 - ang, 30]]);
    }
    poly('dark', [[x0 + 17, 6], [x0 + 30, 6], [x0 + 28, 14], [x0 + 18, 14]]); // trigger guard
    // magazine
    const mt = g.magT, magExt = att.mag === 'extmag' ? 1.4 : att.mag === 'drum' ? 2 : 1;
    const mx = x0 + R * 0.52;
    if (att.mag === 'drum' && (mt === 'stanag' || mt === 'curved' || mt === 'curvedsmg' || mt === 'translucent')) { rect('dark', mx - 3, 6, 12, 8, 1); circ('poly', mx + 3, 30, 15); circ('dark', mx + 3, 30, 4); }
    else if (mt === 'stanag' || mt === 'translucent') { poly(mt === 'translucent' ? 'trans' : 'mag', [[mx - 2, 6], [mx + 12, 6], [mx + 15, 6 + 28 * magExt], [mx + 1, 8 + 28 * magExt]]); if (att.mag === 'coupler') poly('mag2', [[mx + 4, 8], [mx + 16, 8], [mx + 19, 34], [mx + 7, 36]]); }
    else if (mt === 'curved' || mt === 'curvedsmg') { const k = mt === 'curvedsmg' ? 0.8 : 1; P.push({ role: 'mag', k: 'q', a: [mx - 2, 6, mx + 12, 6, (mx + 30) * 1, 6 + 30 * k * magExt, mx + 18, 10 + 32 * k * magExt] }); }
    else if (mt === 'box') { rect('mag', mx - 2, 6, 15, 20 * magExt + 4, 1.5); }
    else if (mt === 'tube') { rect('dark', x1, 4, Math.min(barrel + Hd, (g.hand + 20) * 1.55) * (att.mag === 'extmag' ? 1.2 : 0.85), 6, 2); }
    else if (mt === 'grip') { poly('poly', [[x0 + 8, 6], [x0 + 20, 6], [x0 + 17, 30 + 10 * (magExt - 1)], [x0 + 4, 30 + 10 * (magExt - 1)]]); }
    else if (mt === 'top') { rect('trans', x0 - 20, -14, R + 10, 6, 2); }
    else if (mt === 'internal') { rect('dark', mx, 6, 14, 5, 1); }
    // handguard
    let hd = Hd; if (att.hand === 'shorthg') hd *= 0.7; if (att.hand === 'longhg') hd *= 1.25;
    if (hd > 0) {
      const role = g.wood && !att.hand ? 'wood' : att.hand === 'mlok' ? 'bodyalt' : 'body';
      rect(role, x1, -7, hd, bulky ? 14 : 12, 2);
      if (att.hand === 'mlok' || g.rail) for (let i = 8; i < hd - 6; i += 9) rect('dark', x1 + i, -3, 5, 3, 1);
    }
    // barrel
    const bx = x1 + Math.max(hd, 4);
    const bl = Math.max(6, barrel - hd * 0.5);
    rect('metal', bx - 2, -3, bl + 2, att.barrel === 'heavyb' ? 6 : 4, 0.5);
    const mzx = bx + bl;
    if (!att.muzzle && b.cat !== 'sg') rect('dark', mzx, -4, 8, 6, 1);
    if (att.muzzle === 'supp' || att.muzzle === 'psupp') { rect('dark', mzx - 2, -6, att.muzzle === 'supp' ? 46 : 36, 10, 3); for (let i = 6; i < 40; i += 8) rect('metal', mzx + i, -6, 1, 10, 0); }
    if (att.muzzle === 'brake') { rect('metal', mzx, -5, 13, 8, 1); rect('dark', mzx + 3, -5, 2, 8, 0); rect('dark', mzx + 8, -5, 2, 8, 0); }
    if (att.muzzle === 'comp') { rect('metal', mzx, -5, 11, 8, 1); circ('dark', mzx + 7, -3, 1.3); }
    // front sight (irons) if no optic
    if (!att.optic) { rect('dark', bx - 4, -16, 3, 8, 0); rect('dark', x0 + 4, -16, 6, 5, 1); }
    // optics
    const ox = x0 + R * 0.25;
    if (att.optic === 'rds') { rect('dark', ox, -22, 18, 11, 3); rect('glass', ox + 14, -20, 3, 7, 1); }
    if (att.optic === 'holo') { rect('dark', ox, -24, 30, 13, 2); rect('glass', ox + 4, -23, 22, 8, 1); }
    if (att.optic === 'prism') { rect('dark', ox - 2, -25, 38, 13, 3); circ('glass', ox + 36, -18, 5); rect('body', ox + 10, -30, 10, 5, 1); }
    if (att.optic === 'lpvo' || att.optic === 'scope') {
      const len = att.optic === 'scope' ? 80 : 58, rr2 = att.optic === 'scope' ? 8 : 6;
      rect('dark', ox - 10, -16, 8, 4, 0); rect('dark', ox + len - 24, -16, 8, 4, 0);
      rect('dark', ox - 14, -16 - rr2 * 2, len, rr2 * 1.6, 3); rect('dark', ox - 20, -18 - rr2 * 2.2, 14, rr2 * 2.4, 3); rect('dark', ox + len - 22, -18 - rr2 * 2.2, 16, rr2 * 2.4, 3);
      rect('metal', ox + len * 0.35, -24 - rr2 * 2, 8, 6, 1);
      circ('glass', ox + len - 6, -16 - rr2 * 1.2, rr2 * 0.9);
    }
    // underbarrel
    const ux = x1 + hd * 0.45;
    if (att.under === 'vfg') rect('poly', ux, 5, 9, 22, 3);
    if (att.under === 'afg') poly('poly', [[ux - 10, 5], [ux + 14, 5], [ux + 14, 10], [ux - 4, 14]]);
    if (att.under === 'stop') rect('poly', ux + hd * 0.25, 5, 5, 6, 2);
    if (att.under === 'bipod') { rect('dark', ux + hd * 0.3, 5, 10, 5, 1); poly('metal', [[ux + hd * 0.3 + 2, 9], [ux + hd * 0.3 + 5, 9], [ux + hd * 0.3 - 10, 38], [ux + hd * 0.3 - 13, 38]]); poly('metal', [[ux + hd * 0.3 + 5, 9], [ux + hd * 0.3 + 8, 9], [ux + hd * 0.3 + 18, 38], [ux + hd * 0.3 + 15, 38]]); }
    // side rail devices
    if (att.side) { const sx = x1 + Math.max(10, hd - 26); rect('dark', sx, 0, 22, 9, 2); if (att.side !== 'laser') { circ('light', sx + 22, 4.5, 3.6); } if (att.side !== 'light') circ('laser', sx + 4, 6, 1.6); }
    // sling
    if (att.sling) { P.push({ role: 'sling', k: 'sl', a: [-50, 10, x1 + 10, 6] }); }
    return { parts: P, len: mzx + 50, minX: -72 };
  }

  const ROLE_COL = (role, fin, w) => {
    switch (role) {
      case 'body': return fin.c; case 'bodyalt': return U.shade(fin.c, 0.08);
      case 'poly': return w.finish === 'black' || !fin.camo ? U.shade(fin.c, -0.1) : '#2a2b2d';
      case 'dark': return '#1a1b1d'; case 'metal': return '#3d4043'; case 'glass': return '#6fb7d8';
      case 'wood': return '#6b4425'; case 'rubber': return '#151515'; case 'mag': return '#232426'; case 'mag2': return '#1c1d1f';
      case 'trans': return 'rgba(140,140,120,0.85)'; case 'light': return '#f3f0c8'; case 'laser': return '#e33';
      default: return '#333';
    }
  };
  function tracePart(ctx, p) {
    const a = p.a;
    if (p.k === 'r') rr(ctx, a[0], a[1], a[2], a[3], a[4]);
    else if (p.k === 'p') { ctx.beginPath(); ctx.moveTo(a[0][0], a[0][1]); for (let i = 1; i < a.length; i++) ctx.lineTo(a[i][0], a[i][1]); ctx.closePath(); }
    else if (p.k === 'c') { ctx.beginPath(); ctx.arc(a[0], a[1], a[2], 0, Math.PI * 2); }
    else if (p.k === 'q') { ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.lineTo(a[2], a[3]); ctx.quadraticCurveTo(a[4] - 10, a[5] * 0.6, a[4] - 8, a[5]); ctx.lineTo(a[6] - 10, a[7]); ctx.quadraticCurveTo(a[6] - 14, a[7] * 0.5, a[0], a[1] + 2); ctx.closePath(); }
  }

  function weapon(ctx, w, cx, cy, scale = 1, yaw = 0, { wear = true } = {}) {
    const fin = D.FINISH[w.finish] || D.FINISHES[0];
    const finUse = w.finish === 'custom' ? { ...fin, c: w.camo[0], camo: [w.camo[1], U.shade(w.camo[0], -0.3), U.shade(w.camo[1], 0.2)] } : fin;
    const { parts, len, minX } = weaponParts(w);
    const mid = (len + minX) / 2;
    const cy0 = Math.cos(yaw), depth = Math.sin(yaw) * 7;
    ctx.save();
    ctx.translate(cx, cy); ctx.scale(scale * cy0, scale); ctx.translate(-mid, 0);
    // back layer (thickness)
    ctx.save(); ctx.translate(depth / Math.max(0.2, Math.abs(cy0)) * 0.6, -Math.abs(depth) * 0.25);
    for (const p of parts) { if (p.k === 'sl') continue; tracePart(ctx, p); ctx.fillStyle = 'rgba(0,0,0,0.55)'; ctx.fill(); }
    ctx.restore();
    // sling
    for (const p of parts) if (p.k === 'sl') { ctx.strokeStyle = '#3a3a2e'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(p.a[0], p.a[1]); ctx.quadraticCurveTo((p.a[0] + p.a[2]) / 2, 46, p.a[2], p.a[3]); ctx.stroke(); }
    const seed = U.hashStr(w.uid || w.base);
    for (const p of parts) {
      if (p.k === 'sl') continue;
      tracePart(ctx, p);
      ctx.fillStyle = ROLE_COL(p.role, finUse, w); ctx.fill();
      if ((p.role === 'body' || p.role === 'bodyalt' || (p.role === 'poly' && finUse.camo)) && finUse.camo && finUse.camo.length) {
        ctx.save(); ctx.clip(); camoFill(ctx, finUse.camo, seed, -80, -40, len + 80, 80, 7); ctx.restore();
      }
      // highlight
      ctx.save(); ctx.clip(); const gr = ctx.createLinearGradient(0, -30, 0, 40); gr.addColorStop(0, 'rgba(255,255,255,0.12)'); gr.addColorStop(0.4, 'rgba(255,255,255,0)'); gr.addColorStop(1, 'rgba(0,0,0,0.25)'); ctx.fillStyle = gr; ctx.fillRect(-100, -60, len + 120, 120); ctx.restore();
      ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.lineWidth = 0.6; ctx.stroke();
    }
    // wear & dirt
    if (wear) {
      const r = U.rng(seed + 7), wearAmt = (100 - (w.condition ?? 100)) / 100 + 0.08;
      ctx.save();
      ctx.beginPath(); for (const p of parts) if (p.role === 'body' || p.role === 'poly' || p.role === 'mag') { tracePart(ctx, p); ctx.save(); ctx.restore(); }
      for (const p of parts) if (p.role === 'body') { tracePart(ctx, p); ctx.save(); ctx.clip();
        ctx.strokeStyle = 'rgba(200,200,190,0.35)'; ctx.lineWidth = 0.5;
        for (let i = 0; i < 18 * wearAmt; i++) { const x = r.range(-60, len), y = r.range(-20, 20); ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x + r.range(-6, 6), y + r.range(-2, 2)); ctx.stroke(); }
        ctx.fillStyle = 'rgba(90,70,40,0.25)';
        for (let i = 0; i < 60 * wearAmt; i++) { ctx.beginPath(); ctx.arc(r.range(-60, len), r.range(-20, 30), r.range(0.4, 1.6), 0, 7); ctx.fill(); }
        ctx.restore(); }
      ctx.restore();
    }
    // engraving
    if (w.engraving) { ctx.save(); ctx.fillStyle = 'rgba(230,220,190,0.75)'; ctx.font = 'italic 5px Georgia'; ctx.fillText(w.engraving.slice(0, 22), 4, 2); ctx.restore(); }
    if (w.serial) { ctx.save(); ctx.fillStyle = 'rgba(220,220,220,0.4)'; ctx.font = '3.2px monospace'; ctx.fillText(w.serial, 6, D.WEAPON[w.base].geo.pistol ? -1 : 4.5); ctx.restore(); }
    ctx.restore();
  }

  // ---------- Top-down unit sprite ----------
  // u: {team, kind, angle, ...}, look: appearance info
  function unitTop(ctx, u, t) {
    const look = u.look || {};
    const r = 9;
    ctx.save();
    ctx.translate(u.x, u.y);
    if (u.dead || u.incap || u.downed) {
      ctx.rotate(u.angle + Math.PI);
      ctx.fillStyle = look.shirt || '#555';
      rr(ctx, -16, -7, 22, 14, 6); ctx.fill();
      ctx.fillStyle = look.carrier || look.shirt || '#444'; if (u.team === 'op') { rr(ctx, -12, -6, 13, 12, 3); ctx.fill(); }
      ctx.fillStyle = look.pants || '#333'; rr(ctx, -30, -6, 16, 5, 2); ctx.fill(); rr(ctx, -30, 1, 16, 5, 2); ctx.fill();
      ctx.fillStyle = look.head || look.skin || '#c90'; ctx.beginPath(); ctx.arc(9, 0, 5.5, 0, 7); ctx.fill();
      if (u.restrained) { ctx.fillStyle = '#ddd'; ctx.fillRect(-12, -2, 4, 4); }
      ctx.restore();
      if (u.downed && !u.dead && u.team === 'op') { ctx.save(); ctx.translate(u.x, u.y); ctx.strokeStyle = u.stable ? '#5cb85c' : (Math.floor(t * 4) % 2 ? '#d9534f' : '#7a2020'); ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(0, 0, 16, 0, 7); ctx.stroke(); ctx.restore(); }
      return;
    }
    // shadow
    ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.beginPath(); ctx.ellipse(2, 3, r + 2, r, 0, 0, 7); ctx.fill();
    ctx.rotate(u.angle);
    const kneel = u.kneel || u.hostageBound;
    const sc = kneel ? 0.85 : 1; ctx.scale(sc, sc);
    // weapon
    const hasW = u.weaponCat && !u.surrendered && !u.restrained && !u.hostageBound;
    if (hasW) {
      const L = { hg: 14, smg: 20, ar: 26, br: 30, sg: 27, sr: 34 }[u.weaponCat] || 22;
      ctx.fillStyle = '#101112'; ctx.fillRect(4, -1.6, L, 3.2);
      if (u.weaponCat !== 'hg') ctx.fillRect(-2, -1.2, 8, 2.4);
      if (u.suppressed) ctx.fillRect(L + 2, -2, 8, 4);
      ctx.fillStyle = look.glove || look.skin || '#333';
      ctx.beginPath(); ctx.arc(u.weaponCat === 'hg' ? 9 : 14, 0, 2.8, 0, 7); ctx.arc(5, u.weaponCat === 'hg' ? 0 : 3, 2.6, 0, 7); ctx.fill();
      if (u.light && u.lightOn) { ctx.fillStyle = '#fffbe0'; ctx.fillRect(L + 2, -1, 2, 2); }
    }
    // shield
    if (u.shield) { ctx.fillStyle = '#26292d'; ctx.strokeStyle = '#555'; ctx.lineWidth = 1; ctx.beginPath(); ctx.arc(-2, 0, 15, -1.0, 1.0); ctx.arc(-2, 0, 11.5, 1.0, -1.0, true); ctx.closePath(); ctx.fill(); ctx.stroke(); ctx.fillStyle = '#7ec8e3'; ctx.fillRect(11, -3, 2, 6); }
    // body
    ctx.fillStyle = look.shirt || '#666';
    ctx.beginPath(); ctx.ellipse(0, 0, 6.5, r + (look.build || 0) * 0.6, 0, 0, 7); ctx.fill();
    if (look.camo) { ctx.save(); ctx.clip(); ctx.fillStyle = 'rgba(60,55,35,0.6)'; ctx.fillRect(-4, -8, 3, 5); ctx.fillRect(1, 2, 4, 5); ctx.fillStyle = 'rgba(170,150,105,0.5)'; ctx.fillRect(-3, 3, 4, 4); ctx.restore(); }
    if (look.carrier && u.team === 'op') { ctx.fillStyle = look.carrier; rr(ctx, -5.5, -7, 11, 14, 2.5); ctx.fill(); ctx.fillStyle = 'rgba(0,0,0,0.3)'; ctx.fillRect(1, -6, 3, 12); }
    else if (u.armor > 0) { ctx.fillStyle = '#2a2d2a'; rr(ctx, -4.5, -6, 9, 12, 2); ctx.fill(); }
    // hands
    if (u.surrendered && !u.restrained) { ctx.fillStyle = look.skin || '#c96'; ctx.beginPath(); ctx.arc(3, -10.5, 2.6, 0, 7); ctx.arc(3, 10.5, 2.6, 0, 7); ctx.fill(); }
    if (u.restrained || u.hostageBound) { ctx.fillStyle = look.skin || '#c96'; ctx.beginPath(); ctx.arc(-6, -2.5, 2.2, 0, 7); ctx.arc(-6, 2.5, 2.2, 0, 7); ctx.fill(); ctx.fillStyle = '#f2f2f2'; ctx.fillRect(-7.2, -1, 2.4, 2); }
    if (!hasW && !u.surrendered && !u.restrained && !u.hostageBound && !u.shield) { ctx.fillStyle = look.skin || '#c96'; ctx.beginPath(); ctx.arc(2, -8, 2.4, 0, 7); ctx.arc(2, 8, 2.4, 0, 7); ctx.fill(); }
    // head
    ctx.fillStyle = look.skin || '#c96'; ctx.beginPath(); ctx.arc(1, 0, 5.2, 0, 7); ctx.fill();
    if (look.helmet) { ctx.fillStyle = look.helmet; ctx.beginPath(); ctx.arc(0.5, 0, 5.6, 0, 7); ctx.fill(); ctx.fillStyle = 'rgba(255,255,255,0.12)'; ctx.beginPath(); ctx.arc(-0.5, -1.5, 2.5, 0, 7); ctx.fill(); ctx.fillStyle = '#111'; ctx.fillRect(3.5, -1.2, 2, 2.4); }
    else if (look.hair) { ctx.fillStyle = look.hair; ctx.beginPath(); ctx.arc(-0.5, 0, 5, Math.PI * 0.35, Math.PI * 1.65); ctx.fill(); }
    if (look.balaclava && !look.helmet) { ctx.fillStyle = '#18191b'; ctx.beginPath(); ctx.arc(1, 0, 5.3, 0, 7); ctx.fill(); }
    ctx.restore();
    // stun indicator
    if (u.stun > 0) {
      ctx.save(); ctx.translate(u.x, u.y - 16); ctx.strokeStyle = '#ffeb3b'; ctx.lineWidth = 1.5;
      for (let i = 0; i < 3; i++) { const a = t * 5 + i * 2.1; ctx.beginPath(); ctx.arc(Math.cos(a) * 7, Math.sin(a) * 3, 1.6, 0, 7); ctx.stroke(); }
      ctx.restore();
    }
  }

  // ---------- Vehicles (top-down) ----------
  function vehicle(ctx, vdef, vinst, x, y, ang, t = 0) {
    const L = vdef.len * BP.TS, Wd = vdef.wid * BP.TS;
    ctx.save(); ctx.translate(x, y); ctx.rotate(ang);
    ctx.fillStyle = 'rgba(0,0,0,0.4)'; rr(ctx, -L / 2 + 4, -Wd / 2 + 5, L, Wd, 10); ctx.fill();
    const paint = (vinst && vinst.paint) || vdef.color;
    ctx.fillStyle = paint; rr(ctx, -L / 2, -Wd / 2, L, Wd, 12); ctx.fill();
    ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.lineWidth = 1.5; ctx.stroke();
    // wheels
    ctx.fillStyle = '#0c0c0c';
    for (const sx of [-L * 0.32, L * 0.3]) for (const sy of [-1, 1]) { rr(ctx, sx - 9, sy * Wd / 2 - (sy > 0 ? 4 : 2), 18, 6, 2); ctx.fill(); }
    // windshield, roof
    ctx.fillStyle = '#1b2733'; rr(ctx, L * 0.12, -Wd / 2 + 6, L * 0.14, Wd - 12, 4); ctx.fill();
    ctx.fillStyle = '#16202a'; rr(ctx, -L * 0.42, -Wd / 2 + 7, L * 0.08, Wd - 14, 3); ctx.fill();
    ctx.fillStyle = U.shade(paint, vdef.armored ? -0.1 : 0.08); rr(ctx, -L * 0.33, -Wd / 2 + 5, L * 0.44, Wd - 10, 5); ctx.fill();
    if (vdef.armored) { ctx.strokeStyle = 'rgba(0,0,0,0.4)'; for (let i = -L * 0.3; i < L * 0.1; i += 10) { ctx.beginPath(); ctx.moveTo(i, -Wd / 2 + 6); ctx.lineTo(i, Wd / 2 - 6); ctx.stroke(); } }
    // accessories
    const acc = (vinst && vinst.accessories) || [];
    if (acc.includes('pushbar')) { ctx.fillStyle = '#111'; ctx.fillRect(L / 2 - 2, -Wd / 2 + 8, 6, Wd - 16); }
    if (acc.includes('ladder')) { ctx.strokeStyle = '#888'; ctx.lineWidth = 2; ctx.strokeRect(-L * 0.3, -Wd / 2 + 9, L * 0.38, Wd - 18); for (let i = -L * 0.3; i < L * 0.08; i += 8) { ctx.beginPath(); ctx.moveTo(i, -Wd / 2 + 9); ctx.lineTo(i, Wd / 2 - 9); ctx.stroke(); } }
    // markings
    if (vinst && vinst.markings) { ctx.save(); ctx.rotate(Math.PI / 2); ctx.fillStyle = paint === '#e7e9ec' || U.hexToRgb(paint).reduce((a, b) => a + b) > 500 ? '#1a2a4a' : '#e0e0e0'; ctx.font = 'bold 11px Arial'; ctx.textAlign = 'center'; ctx.fillText(vinst.markings.slice(0, 10), 0, L * 0.12 + 4); ctx.restore(); }
    // light bar
    const lights = vinst ? vinst.lights : 'none';
    if (lights === 'full' || lights === 'hidden') {
      const on = Math.floor(t * 6) % 2;
      if (lights === 'full') { ctx.fillStyle = '#222'; ctx.fillRect(L * 0.02, -Wd / 2 + 6, 6, Wd - 12); }
      ctx.fillStyle = on ? '#ff2a2a' : '#4a0000'; ctx.fillRect(lights === 'full' ? L * 0.02 : L * 0.12, -Wd / 2 + 7, 5, (Wd - 14) / 2);
      ctx.fillStyle = on ? '#001a4a' : '#2a6aff'; ctx.fillRect(lights === 'full' ? L * 0.02 : L * 0.12, 0, 5, (Wd - 14) / 2);
      if (t > 0) { ctx.globalCompositeOperation = 'lighter'; const g = ctx.createRadialGradient(L * 0.05, 0, 2, L * 0.05, 0, 60); g.addColorStop(0, on ? 'rgba(255,40,40,0.25)' : 'rgba(40,80,255,0.25)'); g.addColorStop(1, 'rgba(0,0,0,0)'); ctx.fillStyle = g; ctx.fillRect(-60, -60, 130, 120); ctx.globalCompositeOperation = 'source-over'; }
    }
    // headlights
    ctx.fillStyle = '#fff6c8'; ctx.fillRect(L / 2 - 4, -Wd / 2 + 4, 3, 7); ctx.fillRect(L / 2 - 4, Wd / 2 - 11, 3, 7);
    ctx.restore();
  }

  // ---------- Item icons (top-down) ----------
  function itemIcon(ctx, type, x, y, t) {
    ctx.save(); ctx.translate(x, y);
    const pulse = 0.5 + 0.5 * Math.sin(t * 4);
    switch (type) {
      case 'docs': ctx.fillStyle = '#eee8d8'; ctx.rotate(0.2); ctx.fillRect(-7, -9, 14, 18); ctx.fillStyle = '#ddd5c0'; ctx.fillRect(-5, -7, 14, 18); ctx.fillStyle = '#888'; for (let i = 0; i < 5; i++) ctx.fillRect(-3, -4 + i * 3, 9, 1); break;
      case 'laptop': ctx.fillStyle = '#3a3d42'; ctx.fillRect(-10, -7, 20, 14); ctx.fillStyle = '#5ab0ff'; ctx.globalAlpha = 0.5 + pulse * 0.3; ctx.fillRect(-8, -5, 16, 9); ctx.globalAlpha = 1; break;
      case 'phone': ctx.fillStyle = '#111'; ctx.fillRect(-3.5, -6, 7, 12); ctx.fillStyle = '#3a8'; ctx.fillRect(-2.5, -4.5, 5, 7); break;
      case 'drugs': ctx.fillStyle = '#f2f2ea'; for (let i = 0; i < 4; i++) { ctx.fillRect(-10 + (i % 2) * 10, -8 + Math.floor(i / 2) * 9, 9, 8); } ctx.strokeStyle = '#c9a'; ctx.strokeRect(-10, -8, 19, 17); break;
      case 'cash': ctx.fillStyle = '#5e8a4a'; for (let i = 0; i < 3; i++) ctx.fillRect(-9 + i * 6, -6 + (i % 2) * 2, 6, 11); ctx.fillStyle = '#e8e0c0'; ctx.fillRect(-9, -1, 18, 2); break;
      case 'guns': ctx.fillStyle = '#5a4a2e'; ctx.fillRect(-12, -8, 24, 16); ctx.fillStyle = '#111'; ctx.fillRect(-10, -5, 20, 2.5); ctx.fillRect(-10, 0, 18, 2.5); break;
      case 'weapon': ctx.fillStyle = '#111'; ctx.rotate(0.6); ctx.fillRect(-9, -1.5, 18, 3); ctx.fillRect(-4, 0, 3, 5); break;
    }
    ctx.restore();
  }

  return { rr, portrait, weapon, unitTop, vehicle, itemIcon, uniformColors, camoFill };
})();
