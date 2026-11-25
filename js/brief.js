// Mission briefing (squad, loadouts, entry points), results processing and debrief
'use strict';
BP.brief = (() => {
  const D = BP.D, U = BP.util, ST = BP.state, el = U.el;
  let cfg = null, map = null, squad = [], viewFloor = 0, entries = {};
  const S = () => BP.audio.S;
  const P = () => ST.P;

  function seats() {
    const p = P(); const v = p.vehicles.find(v => v.uid === p.activeVehicle) || p.vehicles[0];
    return D.VEH[v.id].seats;
  }
  function available(op) { return op.injury <= 0 && op.status !== 'kia'; }

  function open(mcfg) {
    cfg = mcfg;
    map = BP.mapgen.generate(cfg.mapType, cfg.seed, { floors: cfg.floors, small: cfg.small });
    viewFloor = 0;
    const p = P();
    const avail = p.operators.filter(available).sort((a, b) => a.fatigue - b.fatigue);
    squad = avail.slice(0, Math.min(seats(), 4)).map(o => o.id);
    entries = {}; squad.forEach(id => entries[id] = 0);
    BP.main.screen('brief');
    render();
  }

  function render() {
    const p = P();
    const root = document.getElementById('scr-brief'); root.innerHTML = '';
    const left = el('div', { class: 'brief-left' });
    const cv = el('canvas', {});
    const floorBtns = el('div', { class: 'row', style: 'position:absolute;left:10px;top:10px' });
    map.floors.forEach((_, i) => floorBtns.append(el('button', { class: 'sm' + (i === viewFloor ? ' on' : ''), onclick: () => { viewFloor = i; render(); } }, map.labels[i])));
    left.append(cv, floorBtns);
    const diff = D.DIFFICULTY[cfg.diffOverride || p.difficulty];
    const intelEnemies = cfg.training ? `${cfg.enemies} targets` : diff.showEnemies || (cfg.campaignId && p.campaign.intel[cfg.org] >= 2) ? `${cfg.enemies} suspects` : p.difficulty === 'milsim' ? 'Unknown' : `${Math.max(1, cfg.enemies - 2)}–${cfg.enemies + 2} suspects (estimated)`;
    left.append(el('div', { class: 'brief-info' },
      el('h2', {}, cfg.name), el('div', { class: 'dim' }, `${D.MTYPES[cfg.type] ? D.MTYPES[cfg.type].name : ''} · ${cfg.orgName || ''} · Threat tier ${cfg.tier}`),
      el('p', {}, cfg.brief), el('p', { class: 'acc' }, cfg.intel), cfg.intelNote ? el('p', { class: 'red' }, cfg.intelNote) : '',
      el('div', { class: 'kv' }, el('span', {}, 'Hostiles'), el('span', {}, intelEnemies), el('span', {}, 'Hostages'), el('span', {}, cfg.hostages ? String(cfg.hostages) : 'None reported'), el('span', {}, 'Civilians'), el('span', {}, cfg.civilians ? 'Possible' : 'None expected'),
        el('span', {}, 'High-value target'), el('span', {}, cfg.hvt ? cfg.hvtName + ' — capture alive' : '—'), el('span', {}, 'Conditions'), el('span', {}, `${cfg.night ? 'Night' : 'Day'}${cfg.rain ? ', rain' : ', clear'}`),
        el('span', {}, 'Building'), el('span', {}, `${map.floors.length} level${map.floors.length > 1 ? 's' : ''}, ${map.doors.length} doors, ${map.doors.filter(d => d.exterior).length} exterior entrances`),
        el('span', {}, 'Payment'), el('span', { class: 'price' }, U.fmtMoney(cfg.reward)), el('span', {}, 'Difficulty'), el('span', {}, diff.name))));
    root.append(left);
    // right column
    const right = el('div', { class: 'brief-right' });
    const max = seats();
    right.append(el('div', { class: 'row sp' }, el('h2', {}, 'Squad Selection'), el('span', { class: 'dim' }, `${squad.length}/${max} seats (${D.VEH[(p.vehicles.find(v => v.uid === p.activeVehicle) || p.vehicles[0]).id].name})`)));
    for (const op of p.operators) {
      const inSquad = squad.includes(op.id); const ok = available(op);
      const c = el('canvas', { width: 72, height: 84, style: 'width:36px;height:42px' }); BP.art.portrait(c.getContext('2d'), op, 72, 84);
      const row = el('div', { class: 'op-row' + (inSquad ? ' sel' : ''), style: ok ? '' : 'opacity:.4', onclick: e => { if (e.target.tagName === 'SELECT' || e.target.tagName === 'OPTION') return; if (!ok) return; if (inSquad) squad = squad.filter(x => x !== op.id); else if (squad.length < max) { squad.push(op.id); entries[op.id] = entries[op.id] || 0; } else { ST.toast('Vehicle is full.'); return; } S().ui(); render(); } },
        el('input', { type: 'checkbox', checked: inSquad, disabled: !ok }), c,
        el('div', { class: 'grow' }, el('div', {}, el('b', {}, op.callsign), el('span', { class: 'dim' }, ` ${D.ROLE[op.role].name} · Lv${op.level}`), ok ? '' : el('span', { class: 'tag red' }, `INJURED ${op.injury}d`), op.fatigue > 60 ? el('span', { class: 'tag acc' }, 'FATIGUED') : ''),
          inSquad ? miniLoadout(op) : el('div', { class: 'dim', style: 'font-size:11px' }, loadoutSummary(op))));
      right.append(row);
    }
    // supplies
    const need = {}; for (const id of squad) { const op = ST.opById(id); for (const u of op.loadout.util) need[u] = (need[u] || 0) + 1; }
    const short = Object.entries(need).filter(([k, n]) => (p.stock[k] || 0) < n);
    const toolsNeed = {}; for (const id of squad) { const t = ST.opById(id).loadout.tool; if (t) toolsNeed[t] = (toolsNeed[t] || 0) + 1; }
    const toolShort = Object.entries(toolsNeed).filter(([k, n]) => (p.stock[k] || 0) < n);
    right.append(el('div', { class: 'panel' }, el('h3', {}, 'Supplies'),
      el('div', { style: 'font-size:12px' }, Object.entries(need).map(([k, n]) => `${D.ITEM[k].name}: ${n} needed / ${p.stock[k] || 0} in stock`).join(' · ') || 'No consumables assigned.'),
      short.length || toolShort.length ? el('div', { class: 'red', style: 'font-size:12px;margin-top:4px' }, 'Shortage: ' + [...short, ...toolShort].map(([k]) => D.ITEM[k].name).join(', ') + ' — excess will be removed from loadouts at deployment.') : ''));
    right.append(el('div', { class: 'row brief-deploy', style: 'margin-top:auto' },
      el('button', { onclick: () => { BP.hq.show(); } }, '◀ Back to HQ'),
      el('button', { class: 'primary grow', disabled: !squad.length, onclick: deploy }, `DEPLOY ${squad.length} OPERATOR${squad.length === 1 ? '' : 'S'} ▶`)));
    root.append(right);
    requestAnimationFrame(() => {
      cv.width = cv.clientWidth * 2; cv.height = cv.clientHeight * 2;
      const markers = [];
      if (cfg.training || D.DIFFICULTY[p.difficulty].showEnemies) { /* no exact markers, intel only */ }
      BP.render.preview(cv.getContext('2d'), map, viewFloor, { markers });
    });
  }
  function loadoutSummary(op) {
    const L = op.loadout; const pw = L.primary && ST.weaponByUid(L.primary), sw = L.secondary && ST.weaponByUid(L.secondary);
    return `${pw ? D.WEAPON[pw.base].name : '—'} / ${sw ? D.WEAPON[sw.base].name : '—'} · ${D.ARMORD[L.armor].name} · ${L.util.map(u => D.ITEM[u].name).join(', ') || 'no utility'}${L.tool ? ' · ' + D.ITEM[L.tool].name : ''}`;
  }
  function miniLoadout(op) {
    const p = P(), L = op.loadout;
    const box = el('div', { class: 'loadout-mini' });
    const sel = (label, options, val, fn) => { const s = el('select', { onchange: e => { fn(e.target.value); ST.save(); render(); } }); for (const [v, n] of options) s.append(el('option', { value: v, selected: v === val }, n)); return el('label', {}, el('span', { class: 'dim' }, label), s); };
    const assigned = uid => p.operators.find(o => o !== op && (o.loadout.primary === uid || o.loadout.secondary === uid));
    const wopts = hg => [['', '—'], ...p.weapons.filter(w => (D.WEAPON[w.base].cat === 'hg') === hg).map(w => [w.uid, `${D.WEAPON[w.base].name}${w.engraving ? ' "' + w.engraving + '"' : ''}${assigned(w.uid) ? ' (' + assigned(w.uid).callsign + ')' : ''}`])];
    const takeW = (slot, uid) => { const o = uid && assigned(uid); if (o) { if (o.loadout.primary === uid) o.loadout.primary = null; if (o.loadout.secondary === uid) o.loadout.secondary = null; } L[slot] = uid || null; };
    box.append(sel('Primary', wopts(false), L.primary || '', v => takeW('primary', v)), sel('Secondary', wopts(true), L.secondary || '', v => takeW('secondary', v)),
      sel('Armor', D.ARMOR.filter(a => p.armorOwned.includes(a.id)).map(a => [a.id, a.name]), L.armor, v => L.armor = v),
      sel('Tool', [['', '—'], ...D.ITEMS.filter(i => i.kind === 'tool' && (p.stock[i.id] || 0) > 0).map(i => [i.id, i.name])], L.tool || '', v => L.tool = v || null),
      sel('Utility', [['', 'Keep: ' + (L.util.map(u => D.ITEM[u].name.split(' ')[0]).join(', ') || 'none')], ['flash,flash', '2× Flashbang'], ['flash,flash,smoke', '2× Flash + Smoke'], ['flash,charge', 'Flash + Charge'], ['charge,charge', '2× Charge'], ['flash,flash,flash,flash', '4× Flashbang'], ['smoke,gas', 'Smoke + Gas'], ['flash,stinger', 'Flash + Diversion'], ['none', 'None']], '', v => { if (v === 'none') L.util = []; else if (v) L.util = v.split(','); }),
      sel('Entry', map.spawns.map((s, i) => [String(i), 'Entry ' + s.name]), String(entries[op.id] || 0), v => entries[op.id] = +v));
    return box;
  }

  function deploy() {
    const p = P();
    // draw consumables from stock
    const squadOps = squad.map(id => ST.opById(id)).filter(Boolean);
    const stockLeft = { ...p.stock };
    const toolUse = {};
    const prepared = squadOps.map(op => {
      const util = [];
      for (const u of op.loadout.util) if ((stockLeft[u] || 0) > 0) { stockLeft[u]--; util.push(u); }
      let tool = op.loadout.tool;
      if (tool) { toolUse[tool] = (toolUse[tool] || 0) + 1; if (toolUse[tool] > (p.stock[tool] || 0)) tool = null; }
      return { op, util, tool };
    });
    // temporarily apply trimmed loadouts for the sim (restored after)
    const saved = prepared.map(x => ({ op: x.op, util: x.op.loadout.util, tool: x.op.loadout.tool }));
    prepared.forEach(x => { x.op.loadout = { ...x.op.loadout, util: x.util, tool: x.tool }; });
    for (const k in p.stock) if (D.ITEM[k] && D.ITEM[k].kind === 'util') p.stock[k] = stockLeft[k];
    const veh = p.vehicles.find(v => v.uid === p.activeVehicle) || p.vehicles[0];
    const scfg = {
      ...cfg, map, squad: prepared.map(x => ({ op: x.op, entry: entries[x.op.id] || 0 })), difficulty: cfg.diffOverride || p.difficulty, vehicle: veh,
      placeSeed: (Date.now() & 0xffffff) ^ cfg.seed,
    };
    ST.save();
    BP.main.startMission(scfg, (M, result) => {
      saved.forEach(s => { s.op.loadout = { ...s.op.loadout, util: s.util, tool: s.tool }; });
      const summary = applyResults(scfg, M, result);
      showDebrief(scfg, M, result, summary);
    });
  }

  // ---------- results ----------
  function applyResults(cfg, M, r) {
    const p = P(), diff = D.DIFFICULTY[cfg.difficulty] || ST.diff();
    const sum = { lines: [], opLines: [], unlocks: [], deaths: [] };
    const tierBefore = ST.tier();
    // money
    let pay = 0;
    if (!cfg.training) {
      const base = cfg.reward * diff.pay * (r.failed ? 0.15 : 0.5 + r.total / 100) * (cfg.sandbox ? 0.4 : 1);
      pay += base; sum.lines.push(['Operational funding', base]);
      const conVal = r.contraband.reduce((a, c) => a + c.value, 0);
      if (conVal) { pay += conVal * 0.25; sum.lines.push(['Asset forfeiture share (25%)', conVal * 0.25]); }
      const intelV = r.evidence.reduce((a, e) => a + (e.intel || 1), 0);
      if (intelV) { pay += intelV * 350; sum.lines.push(['Intelligence bonus', intelV * 350]); }
      if (r.hvtCaptured) { pay += cfg.reward * 0.25; sum.lines.push(['HVT captured alive', cfg.reward * 0.25]); }
      if (r.lieutenantCaptured) { pay += 2500; sum.lines.push(['Lieutenant in custody', 2500]); }
    }
    // ammo costs
    let ammo = 0;
    for (const o of r.ops) { const op = ST.opById(o.id); const am = op ? D.AMMOD[op.loadout.ammo] || D.AMMO[0] : D.AMMO[0]; for (const f of o.fired) ammo += f.fired * D.CAL[f.cal].cost * am.cost * 1.1 * diff.cost * (cfg.training ? 0.3 : 1); }
    if (ammo) sum.lines.push(['Ammunition expended', -ammo]);
    const usedUtil = r.utilUsed;
    const consum = (usedUtil.flash || 0) * 60 + (usedUtil.smoke || 0) * 40 + (usedUtil.charge || 0) * 150;
    pay -= ammo;
    p.money += Math.round(pay);
    sum.net = Math.round(pay);
    // return unused util to stock & medkits
    for (const o of r.ops) {
      for (const u of o.util) p.stock[u] = (p.stock[u] || 0) + 1;
      const op = ST.opById(o.id);
      if (op && op.loadout.tool === 'medkit' && o.toolUses <= 0) { p.stock.medkit = Math.max(0, (p.stock.medkit || 0) - 1); sum.lines.push(['Medical kit depleted', 0]); }
    }
    // operators
    const rankXP = { S: 160, A: 110, B: 60, C: 30, F: 0 }[r.rank] || 0;
    const ids = r.ops.map(o => o.id);
    const lostNames = [];
    for (const o of r.ops) {
      const op = ST.opById(o.id); if (!op) continue;
      let xp = Math.round((cfg.training ? 60 : 120) + o.kills * 25 + o.arrests * 60 + o.rescues * 50 + o.evidence * 25 + rankXP);
      if (r.failed) xp = Math.round(xp * 0.4);
      op.missions++; op.kills += o.kills; op.arrests += o.arrests; op.rescues += o.rescues; op.shots += o.shots; op.hits += o.hits;
      let injuryTxt = '';
      if (!cfg.training) {
        if (o.dead) {
          const rule = diff.death;
          const dies = p.ironman || rule === 'always' || (rule === 'chance' && Math.random() < 0.5);
          if (dies) { op.status = 'kia'; injuryTxt = 'KILLED IN ACTION'; lostNames.push(op.callsign); }
          else { op.injury = 9; op.status = 'injured'; op.injuryDesc = 'Critical gunshot wounds'; injuryTxt = 'Critically wounded — 9 days'; }
        } else if (o.downed) { op.injury = 5; op.status = 'injured'; op.injuryDesc = 'Gunshot wound, surgery'; injuryTxt = 'Seriously wounded — 5 days'; }
        else if (o.hp < o.maxHp) { const dmg = o.maxHp - o.hp; op.injury = Math.max(1, Math.ceil(dmg / 22)); op.status = 'injured'; op.injuryDesc = dmg > 40 ? 'Gunshot wound' : 'Grazing wound / blunt trauma'; injuryTxt = `Wounded — ${op.injury} day${op.injury > 1 ? 's' : ''}`; }
      }
      const armor = D.ARMORD[op.loadout.armor];
      op.fatigue = Math.min(100, op.fatigue + (cfg.training ? 12 : 22) * armor.stam * (1.3 - op.stats.end / 200));
      op.morale = U.clamp(op.morale + (r.failed ? -10 : r.rank === 'S' ? 10 : r.rank === 'A' ? 6 : 3), 0, 100);
      for (const other of ids) if (other !== op.id) op.bonds[other] = (op.bonds[other] || 0) + (r.failed ? 1 : 2);
      const lv = op.level; const leveled = ST.addXP(op, xp);
      sum.opLines.push({ op, xp, leveled, newLevel: op.level, injuryTxt, kills: o.kills, arrests: o.arrests, rescues: o.rescues });
    }
    // deaths
    for (const name of lostNames) {
      const op = p.operators.find(o => o.callsign === name && o.status === 'kia');
      if (!op) continue;
      op.diedDay = p.day; op.diedMission = cfg.name;
      p.fallen.push(op); p.operators = p.operators.filter(o => o !== op);
      for (const other of p.operators) if (other.bonds[op.id]) other.morale = Math.max(0, other.morale - 10 - other.bonds[op.id] * 2);
      for (const other of p.operators) other.morale = Math.max(0, other.morale - 8);
      sum.deaths.push(op);
      p.stats.lost++;
    }
    // weapons wear
    for (const o of r.ops) for (const f of o.fired) { const w = ST.weaponByUid(f.uid); if (w) { w.roundsFired += f.fired; w.condition = Math.max(25, w.condition - f.fired * 0.05 - (cfg.training ? 0 : 0.5)); } }
    // evidence & campaign
    if (!cfg.training) {
      for (const e of r.evidence) p.evidence.push({ name: e.name, org: cfg.org, mission: cfg.name, day: p.day });
      p.stats.seized = (p.stats.seized || 0) + r.contraband.reduce((a, c) => a + c.value, 0);
      if (cfg.org) {
        const intel = r.evidence.reduce((a, e) => a + (e.intel || 1), 0) + (r.lieutenantCaptured ? 2 : 0) + (r.hvtCaptured ? 2 : 0);
        p.campaign.intel[cfg.org] += intel;
        if (intel) sum.lines.push([`Intel gathered on ${D.ORG[cfg.org].short}: +${intel}`, 0]);
      }
      if (cfg.campaignId && !r.failed) {
        const prev = p.campaign.done[cfg.campaignId];
        const order = ['S', 'A', 'B', 'C'];
        if (!prev || order.indexOf(r.rank) < order.indexOf(prev)) p.campaign.done[cfg.campaignId] = r.rank;
        if (r.hvtCaptured && cfg.org) p.campaign.hvtCaptured[cfg.org] = true;
        if (cfg.campaignId === 'c4_4') p.campaign.complete = true;
      }
      if (cfg.weekly !== undefined && !r.failed) p.weeklyDone = cfg.weekly;
      p.stats.missions++; if (!r.failed) p.stats.success++;
      p.stats.arrests += r.arrested; p.stats.kills += r.killed; p.stats.rescued += r.hostSaved;
      ST.advanceDay(1); p.opsBoard = null;
    }
    const tierAfter = ST.tier();
    if (tierAfter > tierBefore) sum.unlocks.push(`Supplier tier ${tierAfter} unlocked: new weapons, attachments and equipment available in the Armory.`);
    ST.save();
    return sum;
  }

  // ---------- debrief ----------
  function showDebrief(cfg, M, r, sum) {
    BP.main.screen('debrief');
    BP.audio.startMusic('debrief'); BP.audio.startAmbient('hq');
    const root = document.getElementById('scr-debrief'); root.innerHTML = '';
    const rankEl = el('div', { class: 'rank-big' + (r.failed ? ' fail' : '') }, r.failed ? 'MISSION FAILED' : r.rank);
    setTimeout(() => S().rankStamp(), 300);
    const scoreBox = el('div', { class: 'panel' }, el('h3', {}, `Evaluation — ${r.total} / 100`));
    for (const s of r.score) scoreBox.append(el('div', { class: 'score-row' }, el('span', {}, s.name), el('div', {}, el('div', { class: 'bar' }, el('i', { style: `width:${Math.max(0, s.pts / s.max * 100)}%;background:${s.pts >= s.max * 0.8 ? '#5cb85c' : s.pts > 0 ? '#e2a33a' : '#d9534f'}` })), el('div', { class: 'dim', style: 'font-size:11px' }, s.detail)), el('span', { class: 'mono' }, `${s.pts}/${s.max}`)));
    const money = el('div', { class: 'panel' }, el('h3', {}, 'Finances'));
    for (const [n, v] of sum.lines) money.append(el('div', { class: 'row sp', style: 'font-size:12px' }, el('span', {}, n), el('span', { class: 'price', style: v < 0 ? 'color:#d9534f' : '' }, v ? U.fmtMoney(v) : '')));
    money.append(el('div', { class: 'row sp', style: 'border-top:1px solid #26313d;margin-top:6px;padding-top:6px' }, el('b', {}, 'Net'), el('b', { class: 'price' }, U.fmtMoney(sum.net))));
    const opsBox = el('div', { class: 'panel' }, el('h3', {}, 'Operators'));
    for (const l of sum.opLines) {
      const c = el('canvas', { width: 80, height: 96, style: 'width:40px;height:48px' }); BP.art.portrait(c.getContext('2d'), l.op, 80, 96);
      opsBox.append(el('div', { class: 'row', style: 'margin:4px 0' }, c, el('div', { class: 'grow' }, el('b', {}, l.op.callsign), el('span', { class: 'dim' }, ` +${l.xp} XP${l.leveled ? ` — LEVEL UP (${l.newLevel})!` : ''}`), el('div', { style: 'font-size:11px' }, `${l.kills} kills · ${l.arrests} arrests · ${l.rescues} rescues`, l.injuryTxt ? el('span', { class: 'red' }, ' · ' + l.injuryTxt) : ''))));
    }
    for (const d of sum.deaths) opsBox.append(el('div', { class: 'red', style: 'margin-top:6px;font-weight:700' }, `✝ ${d.name} "${d.callsign}" — killed in action. ${d.missions} operations served.`));
    const evBox = el('div', { class: 'panel' }, el('h3', {}, 'Evidence & Seizures'),
      ...(r.evidence.length ? r.evidence.map(e => el('div', { style: 'font-size:12px' }, '▸ ' + e.name)) : [el('div', { class: 'dim' }, 'No evidence recovered.')]),
      ...r.contraband.map(c => el('div', { style: 'font-size:12px', class: 'acc' }, `▸ ${c.name} (${U.fmtMoney(c.value)})`)),
      r.hvtName ? el('div', { style: 'margin-top:6px' }, `HVT ${r.hvtName}: `, el('b', { class: r.hvtCaptured ? 'green' : 'red' }, r.hvtCaptured ? 'CAPTURED' : 'NOT CAPTURED')) : '',
      ...sum.unlocks.map(u => el('div', { class: 'green', style: 'margin-top:6px' }, '★ ' + u)));
    // timeline
    const tl = el('div', { class: 'panel scroll', style: 'max-height:260px' }, el('h3', {}, 'Mission Timeline'));
    for (const l of M.log) tl.append(el('div', { style: `font-size:11px;font-family:var(--mono);color:${l.color}` }, `${U.fmtTime(l.t)}  ${l.text}`));
    root.append(el('div', { class: 'row', style: 'align-items:flex-end;gap:30px;margin-bottom:16px' }, rankEl, el('div', {}, el('h2', {}, cfg.name), el('div', { class: 'dim' }, `${U.fmtTime(r.time)} · ${r.arrested} arrested · ${r.killed} killed · ${r.escaped} escaped · ${r.hostSaved} hostages rescued`), r.reason ? el('div', { class: 'red' }, r.reason) : '')),
      el('div', { class: 'grid2' }, el('div', { class: 'col' }, scoreBox, money), el('div', { class: 'col' }, opsBox, evBox, tl)),
      el('div', { class: 'row', style: 'margin-top:16px' }, el('button', { onclick: () => BP.replay.open(M, () => showDebrief(cfg, M, r, sum)) }, '▶ Watch Replay / Body Cams'), el('button', { class: 'primary', onclick: () => { BP.audio.startMusic('hq'); BP.hq.show(); } }, 'Return to HQ')));
  }

  return { open, applyResults };
})();
