// Headquarters: command center, barracks, armory/gunsmith, training, medical, garage, evidence
'use strict';
BP.hq = (() => {
  const D = BP.D, U = BP.util, ST = BP.state, el = U.el;
  let tab = 'command', selOp = null, selWeapon = null, armTab = 'weapons', yaw = 0.15, wzoom = 3.2, storeCat = 'ar';
  const S = () => BP.audio.S;
  const P = () => ST.P;
  const TABS = [
    ['command', 'Command Center', 'Missions & intelligence'], ['barracks', 'Barracks', 'Operators & customization'], ['armory', 'Armory', 'Gunsmith & equipment'],
    ['training', 'Training Facility', 'Skills & drills'], ['medical', 'Medical Bay', 'Injuries & fatigue'], ['garage', 'Garage', 'Vehicles'], ['evidence', 'Evidence Room', 'Case files'],
  ];

  function show(t) {
    if (t) tab = t;
    BP.main.screen('hq');
    renderStats(); renderNav(); renderTab();
  }
  function renderStats() {
    const p = P();
    const d = ST.diff();
    requestAnimationFrame(() => { const tb = document.querySelector('.topbar'); if (tb) document.documentElement.style.setProperty('--tb', tb.offsetHeight + 'px'); });
    document.getElementById('hq-stats').innerHTML =
      `<span>${p.unitName}</span><span>Funds <b>${U.fmtMoney(p.money)}</b></span><span>Day <b>${p.day}</b></span><span>Tier <b>${ST.tier()}</b></span><span>Roster <b>${p.operators.length}</b></span><span>${d.name}${p.ironman ? ' · <b style="color:#d9534f">IRONMAN</b>' : ''}</span>`;
  }
  function renderNav() {
    const nav = document.getElementById('hq-nav'); nav.innerHTML = '';
    for (const [id, name, sub] of TABS) nav.append(el('button', { class: tab === id ? 'active' : '', onclick: () => { tab = id; S().ui(); renderNav(); renderTab(); } }, name, el('span', { class: 'nav-sub' }, sub)));
  }
  function renderTab() {
    renderStats();
    const main = document.getElementById('hq-main'); main.innerHTML = '';
    ({ command, barracks, armory, training, medical, garage, evidence })[tab](main);
  }
  const refresh = () => { ST.save(); renderTab(); };
  function spend(amount, what) {
    if (P().money < amount) { ST.toast('Insufficient funds.'); S().error(); return false; }
    P().money -= amount; S().cash(); if (what) ST.toast(`${what} — ${U.fmtMoney(amount)}`); return true;
  }
  function modal(content) {
    const m = document.getElementById('modal'); m.innerHTML = ''; m.classList.remove('hidden');
    const box = el('div', { class: 'modal-box' }, content);
    m.append(box); m.onclick = e => { if (e.target === m) m.classList.add('hidden'); };
    return { close: () => m.classList.add('hidden'), box };
  }
  BP.ui = { modal };

  // ================= Mission configs =================
  const GEN_ORG_A = ['Crimson', 'Northside', 'Iron', 'Silver', 'Black Lotus', 'Harbor', 'Red Line', 'Sable', 'Cobalt', 'Grey Wolf', 'Ashfall', 'Saint'];
  const GEN_ORG_B = ['Cartel', 'Brotherhood', 'Syndicate', 'Crew', 'Outfit', 'Network', 'Collective', 'Family', 'Ring', 'Company'];
  function missionFromCampaign(m) {
    const p = P(); const org = D.ORG[m.org]; const T = D.MTYPES[m.type];
    let enemies = m.enemies, intelNote = '';
    const orgIdx = D.ORGS.findIndex(o => o.id === m.org);
    if (m.takedown && p.campaign.intel[m.org] < m.intelReq) { enemies += 2; intelNote = `LIMITED INTEL: only ${p.campaign.intel[m.org]}/${m.intelReq} intel gathered on ${org.short}. Expect more resistance.`; }
    if (m.idx === 0 && orgIdx > 0 && p.campaign.hvtCaptured[D.ORGS[orgIdx - 1].id]) { enemies = Math.max(2, enemies - 1); intelNote = `Interrogation of ${D.ORGS[orgIdx - 1].boss} gave us an edge: suspect count reduced.`; }
    return {
      id: m.id, campaignId: m.id, name: m.name, type: m.type, mapType: T.map, org: m.org, orgName: org.name, seed: m.seed, floors: m.floors, small: m.small,
      enemies, hostages: m.hostages || 0, civilians: m.civilians ?? 0, evidence: m.evidence || 0, contraband: m.contraband || 0,
      hvt: !!m.hvt, hvtName: m.hvt ? org.boss : null, lieutenant: !!m.lieutenant, night: !!m.night, rain: !!m.rain,
      reward: m.reward, parTime: 120 + enemies * 22, brief: T.brief, intel: m.intel, intelNote, tier: org.tier, takedown: !!m.takedown, obj: T.obj,
    };
  }
  function genOperation(rng, opts = {}) {
    const keys = Object.keys(D.MTYPES).filter(k => k !== 'mansion' || ST.tier() >= 2);
    const type = opts.type || rng.pick(keys);
    const T = D.MTYPES[type];
    const tier = opts.tier || Math.max(1, Math.min(4, ST.tier() + (rng.chance(0.25) ? 1 : 0) - (rng.chance(0.25) ? 1 : 0)));
    const sc = 0.85 + tier * 0.15;
    const enemies = Math.round(rng.int(T.enemies[0], T.enemies[1]) * sc);
    const hostages = rng.int(T.hostages[0], T.hostages[1]);
    const orgName = P().campaign.complete || rng.chance(0.4) ? `${rng.pick(GEN_ORG_A)} ${rng.pick(GEN_ORG_B)}` : rng.pick(D.ORGS.filter(o => o.tier <= tier)).name;
    const hvt = opts.hvt || !!T.hvt;
    const night = rng.chance(0.3), rain = rng.chance(0.2);
    const reward = Math.round((5000 + enemies * 1300 + hostages * 1500 + (hvt ? 8000 : 0)) * (0.8 + tier * 0.3) / 100) * 100;
    return {
      id: 'op_' + rng.int(1, 1e9), name: opts.name || `${T.name}`, type, mapType: T.map, org: null, orgName, seed: rng.int(1, 1e9), floors: undefined, small: rng.chance(0.5),
      enemies, hostages, civilians: rng.int(T.civilians[0], T.civilians[1]), evidence: rng.int(T.evidence[0], T.evidence[1]), contraband: rng.int(T.contraband[0], T.contraband[1]),
      hvt, hvtName: hvt ? `${rng.pick(D.SUSPECT_FIRST)} "${rng.pick(D.SUSPECT_NICK)}" ${rng.pick(D.SURNAMES)}` : null, lieutenant: rng.chance(0.3), night, rain,
      reward, parTime: 120 + enemies * 22, brief: T.brief, intel: `Suspects linked to ${orgName}. Threat tier ${tier}.`, tier, obj: T.obj, weekly: opts.weekly,
    };
  }
  function opsBoard() {
    const p = P();
    if (!p.opsBoard || p.opsDay !== p.day) {
      const rng = U.rng(p.day * 7919 + 13);
      p.opsBoard = [genOperation(rng), genOperation(rng), genOperation(rng)];
      if (Object.keys(p.campaign.done).length >= 4) p.opsBoard.push(genOperation(rng, { type: 'mansion', hvt: true, name: 'HVT Contract' }));
      p.opsDay = p.day; ST.save();
    }
    return p.opsBoard;
  }
  function weekly() {
    const p = P(); const wk = Math.floor((p.day - 1) / 7);
    const rng = U.rng(wk * 104729 + 7);
    const m = genOperation(rng, { tier: Math.min(4, ST.tier() + 1), weekly: wk, name: 'Weekly Challenge' });
    m.reward = Math.round(m.reward * 1.8);
    m.night = true;
    return m;
  }

  // ================= Command Center =================
  function command(main) {
    const p = P();
    main.append(el('h2', {}, 'Campaign — Criminal Organizations'));
    const grid = el('div', { class: 'grid4' });
    for (const org of D.ORGS) {
      const col = el('div', { class: 'org-col' });
      const tk = D.CAMPAIGN.find(m => m.org === org.id && m.takedown);
      const intel = p.campaign.intel[org.id];
      col.append(el('div', { class: 'org-head', style: `border-top-color:${org.color}` },
        el('div', { style: 'font-weight:700' }, org.name), el('div', { class: 'dim', style: 'font-size:11px;margin:4px 0' }, org.desc),
        el('div', { style: 'font-size:11px' }, `Intel: `, el('b', { class: intel >= tk.intelReq ? 'green' : 'acc' }, `${intel}/${tk.intelReq}`), p.campaign.hvtCaptured[org.id] ? el('span', { class: 'tag green' }, 'BOSS IN CUSTODY') : '')));
      for (const m of D.CAMPAIGN.filter(x => x.org === org.id)) {
        const avail = ST.campaignAvailable(m), done = p.campaign.done[m.id];
        const card = el('div', { class: 'card mission-card' + (avail ? '' : ' locked') + (done ? ' done' : ''), onclick: () => { if (!avail) return; S().ui(); BP.brief.open(missionFromCampaign(m)); } },
          el('h4', {}, `${m.idx + 1}. ${m.name}`), el('div', { class: 'dim', style: 'font-size:11px' }, D.MTYPES[m.type].name + (m.night ? ' · Night' : '') + (m.takedown ? ' · TAKEDOWN' : '')),
          el('div', { style: 'font-size:11px;margin-top:4px' }, avail ? m.intel.slice(0, 90) + (m.intel.length > 90 ? '…' : '') : 'Locked — complete previous operation.'),
          el('div', { class: 'price', style: 'margin-top:4px' }, U.fmtMoney(m.reward)));
        if (done) card.append(el('div', { class: 'rank' }, done));
        col.append(card);
      }
      grid.append(col);
    }
    main.append(grid);
    if (p.campaign.complete) main.append(el('div', { class: 'panel', style: 'margin-top:12px;border-color:#5cb85c' }, el('h2', { style: 'color:#5cb85c' }, 'Campaign Complete'), el('p', {}, 'The Meridian Network has been dismantled. The task force remains on call: procedurally generated operations, HVT contracts and weekly challenges continue below.')));
    // Operations
    main.append(el('h2', { style: 'margin-top:18px' }, 'Operations Board'), el('div', { class: 'dim', style: 'margin-bottom:8px' }, 'Standalone operations with randomized layouts and placements. Board refreshes daily.'));
    const og = el('div', { class: 'grid4' });
    const board = [...opsBoard(), weekly()];
    for (const m of board) {
      const doneW = m.weekly !== undefined && p.weeklyDone === m.weekly;
      og.append(el('div', { class: 'card mission-card' + (doneW ? ' locked' : ''), style: m.weekly !== undefined ? 'border-color:#a45bc8' : '', onclick: () => { if (doneW) return; S().ui(); BP.brief.open(m); } },
        el('h4', {}, m.name + (m.weekly !== undefined ? ' ★' : '')), el('div', { class: 'dim', style: 'font-size:11px' }, `${D.MTYPES[m.type].name} · Tier ${m.tier}${m.night ? ' · Night' : ''}${m.rain ? ' · Rain' : ''}`),
        el('div', { style: 'font-size:11px;margin:4px 0' }, `${m.orgName}. ~${m.enemies} suspects${m.hostages ? `, ${m.hostages} hostages` : ''}${m.hvt ? ', HVT present' : ''}.`),
        el('div', { class: 'price' }, doneW ? 'COMPLETED THIS WEEK' : U.fmtMoney(m.reward))));
    }
    main.append(og);
    main.append(el('div', { class: 'row', style: 'margin-top:14px' },
      el('button', { onclick: sandbox }, 'Sandbox — Build Custom Mission'),
      el('button', { onclick: () => { tab = 'training'; renderNav(); renderTab(); } }, 'Kill House Exercise'),
      el('button', { onclick: () => { ST.advanceDay(1); p.opsBoard = null; ST.toast('A day passes. Operators rest.'); refresh(); } }, 'Stand Down (advance 1 day)')));
  }
  function sandbox() {
    const f = {};
    const sel = (id, opts, val) => { const s = el('select', { id }); for (const [v, n] of opts) s.append(el('option', { value: v, selected: v === val }, n)); f[id] = s; return s; };
    const num = (id, v, min, max) => { const i = el('input', { type: 'number', value: v, min, max, style: 'width:70px' }); f[id] = i; return i; };
    const types = Object.entries(D.MTYPES).map(([k, v]) => [k, v.name]);
    const box = el('div', {}, el('h2', {}, 'Sandbox Mission Builder'),
      el('div', { class: 'formgrid' },
        'Map / scenario', sel('type', types, 'warehouse'), 'Floors (houses/hotel)', num('floors', 2, 1, 3),
        'Enemies', num('enemies', 6, 1, 24), 'Enemy gear tier', sel('tier', [[1, 'Tier 1 — Street'], [2, 'Tier 2 — Organized'], [3, 'Tier 3 — Professional'], [4, 'Tier 4 — Elite']], '2'),
        'Hostages', num('hostages', 1, 0, 8), 'Civilians', num('civilians', 1, 0, 8), 'Evidence', num('evidence', 2, 0, 8), 'Contraband', num('contraband', 1, 0, 8),
        'High-value target', sel('hvt', [['0', 'No'], ['1', 'Yes']], '0'), 'Time of day', sel('night', [['0', 'Day'], ['1', 'Night']], '0'), 'Weather', sel('rain', [['0', 'Clear'], ['1', 'Rain']], '0'),
        'Difficulty', sel('diff', Object.entries(D.DIFFICULTY).map(([k, v]) => [k, v.name]), P().difficulty), 'Seed', num('seed', Math.floor(Math.random() * 99999), 1, 999999)),
      el('p', { class: 'dim' }, 'Sandbox missions pay a reduced reward but operators still gain experience and can be injured.'),
      el('div', { class: 'row' }, el('button', { class: 'primary', onclick: () => {
        const type = f.type.value, T = D.MTYPES[type];
        const m = { id: 'sb', name: 'Sandbox: ' + T.name, type, mapType: T.map, org: null, orgName: 'Sandbox', seed: +f.seed.value, floors: +f.floors.value, enemies: +f.enemies.value, hostages: +f.hostages.value, civilians: +f.civilians.value, evidence: +f.evidence.value, contraband: +f.contraband.value, hvt: f.hvt.value === '1', hvtName: 'Sandbox Target', night: f.night.value === '1', rain: f.rain.value === '1', reward: 2000 + +f.enemies.value * 400, parTime: 120 + +f.enemies.value * 22, brief: T.brief, intel: 'Custom scenario.', tier: +f.tier.value, obj: T.obj, sandbox: true, diffOverride: f.diff.value };
        mm.close(); BP.brief.open(m);
      } }, 'Continue to Briefing'), el('button', { onclick: () => mm.close() }, 'Cancel')));
    const mm = modal(box);
  }

  // ================= Barracks =================
  function portraitCanvas(op, w, h) { const c = el('canvas', { width: w * 2, height: h * 2, style: `width:${w}px;height:${h}px` }); BP.art.portrait(c.getContext('2d'), op, w * 2, h * 2); return c; }
  function opStatusTag(op) {
    if (op.status === 'injured' || op.injury > 0) return el('span', { class: 'tag red' }, `INJURED ${op.injury}d`);
    if (op.fatigue > 60) return el('span', { class: 'tag acc' }, 'FATIGUED');
    return el('span', { class: 'tag green' }, 'READY');
  }
  function barracks(main) {
    const p = P();
    if (!selOp || !p.operators.includes(selOp)) selOp = p.operators[0];
    const wrap = el('div', { class: 'split' });
    const left = el('div', { class: 'col' });
    left.append(el('div', { class: 'row sp' }, el('h2', {}, 'Roster'), el('button', { class: 'sm', onclick: recruit }, '+ Recruit')));
    const list = el('div', { class: 'op-list scroll', style: 'max-height:calc(100vh - 140px)' });
    for (const op of p.operators) {
      list.append(el('div', { class: 'op-row' + (op === selOp ? ' sel' : ''), onclick: () => { selOp = op; S().ui(); renderTab(); } },
        portraitCanvas(op, 44, 52),
        el('div', { class: 'grow' }, el('div', { style: 'font-weight:600' }, `${op.callsign}`, el('span', { class: 'dim', style: 'font-weight:400' }, ` ${op.name}`)),
          el('div', { class: 'dim', style: 'font-size:11px' }, `${D.ROLE[op.role].name} · Lv ${op.level}${op.points ? ` · ${op.points} pts` : ''}${op.perkPicks ? ' · perk!' : ''}`), opStatusTag(op))));
    }
    left.append(list);
    if (p.fallen.length) left.append(el('div', { class: 'dim', style: 'font-size:11px;margin-top:6px' }, `In memoriam: ${p.fallen.map(f => f.callsign).join(', ')}`));
    wrap.append(left);
    wrap.append(selOp ? opDetail(selOp) : el('div', {}, 'No operators. Recruit some.'));
    main.append(wrap);
  }
  function opDetail(op) {
    const p = P();
    const right = el('div', { class: 'col' });
    // header
    const pc = portraitCanvas(op, 150, 180);
    const hdr = el('div', { class: 'panel row', style: 'align-items:flex-start;gap:14px' }, pc,
      el('div', { class: 'col grow' },
        el('div', { class: 'row' }, el('input', { value: op.callsign, style: 'font-size:18px;font-weight:700;width:160px', onchange: e => { op.callsign = e.target.value.slice(0, 14) || op.callsign; refresh(); } }),
          el('input', { value: op.name, style: 'width:200px', onchange: e => { op.name = e.target.value.slice(0, 30) || op.name; refresh(); } }), opStatusTag(op)),
        el('div', { class: 'row' }, 'Role ', roleSelect(op), el('span', { class: 'tag acc' }, D.PERS[op.personality].name), el('span', { class: 'dim' }, D.PERS[op.personality].desc)),
        el('div', { class: 'dim', style: 'font-size:12px' }, op.bio + ` Age ${op.age}.`),
        el('div', { class: 'row', style: 'font-size:12px' }, `Level ${op.level}`, el('div', { class: 'bar', style: 'width:200px' }, el('i', { style: `width:${Math.min(100, (op.xp - ST.xpFor(op.level)) / (ST.xpFor(op.level + 1) - ST.xpFor(op.level)) * 100)}%` })), el('span', { class: 'dim' }, `${op.xp}/${ST.xpFor(op.level + 1)} XP`)),
        el('div', { class: 'kv' }, el('span', {}, 'Morale'), el('span', {}, `${Math.round(op.morale)}`), el('span', {}, 'Fatigue'), el('span', {}, `${Math.round(op.fatigue)}`), el('span', {}, 'Service'), el('span', {}, `${op.missions} ops · ${op.kills} kills · ${op.arrests} arrests · ${op.rescues} rescues · ${op.shots ? Math.round(op.hits / op.shots * 100) : 0}% accuracy`),
          el('span', {}, 'Trusted partners'), el('span', {}, Object.entries(op.bonds).sort((a, b) => b[1] - a[1]).slice(0, 3).map(([id, v]) => { const o = ST.opById(id); return o ? `${o.callsign} (${v})` : null; }).filter(Boolean).join(', ') || '—')),
        el('div', { class: 'row' }, el('button', { class: 'sm danger', onclick: () => { if (p.operators.length <= 1) return; if (!confirm(`Dismiss ${op.callsign}?`)) return; unassign(op); p.operators = p.operators.filter(o => o !== op); selOp = null; refresh(); } }, 'Dismiss'))));
    right.append(hdr);
    const g = el('div', { class: 'grid2' });
    // stats
    const stp = el('div', { class: 'panel' }, el('h3', {}, `Attributes ${op.points ? `— ${op.points} points to spend` : ''}`));
    for (const s of D.STATS) {
      stp.append(el('div', { class: 'statbar', title: s.desc }, el('span', {}, s.name), el('div', { class: 'bar' }, el('i', { style: `width:${op.stats[s.id]}%` })),
        el('span', { class: 'row' }, String(op.stats[s.id]), op.points && op.stats[s.id] < 99 ? el('button', { class: 'sm', style: 'padding:0 5px', onclick: () => { op.points--; op.stats[s.id]++; refresh(); } }, '+') : '')));
    }
    stp.append(el('h3', { style: 'margin-top:10px' }, 'Abilities'));
    stp.append(el('div', {}, op.perks.length ? op.perks.map(pk => el('span', { class: 'tag acc', title: D.PERK[pk].desc }, D.PERK[pk].name)) : el('span', { class: 'dim' }, 'None yet — unlock one every 3 levels.')));
    if (op.perkPicks > 0) {
      const ps = el('div', { class: 'row wrap', style: 'margin-top:6px' }, el('span', { class: 'acc' }, 'Choose ability: '));
      for (const pk of D.PERKS.filter(x => !op.perks.includes(x.id))) ps.append(el('button', { class: 'sm', title: pk.desc, onclick: () => { op.perks.push(pk.id); op.perkPicks--; refresh(); } }, pk.name));
      stp.append(ps);
    }
    g.append(stp);
    g.append(loadoutPanel(op));
    right.append(g);
    right.append(appearancePanel(op));
    return right;
  }
  function roleSelect(op) {
    const s = el('select', { onchange: e => { op.role = e.target.value; refresh(); } });
    for (const r of D.ROLES) s.append(el('option', { value: r.id, selected: r.id === op.role }, r.name));
    return s;
  }
  function unassign(op) { op.loadout.primary = null; op.loadout.secondary = null; }
  function assignedTo(uid) { return P().operators.find(o => o.loadout.primary === uid || o.loadout.secondary === uid); }
  function loadoutPanel(op) {
    const p = P(), L = op.loadout;
    const pan = el('div', { class: 'panel' }, el('h3', {}, 'Loadout'));
    const wsel = (slot, filter) => {
      const s = el('select', { onchange: e => { const v = e.target.value || null; const other = v && assignedTo(v); if (other && other !== op) { if (other.loadout.primary === v) other.loadout.primary = null; if (other.loadout.secondary === v) other.loadout.secondary = null; } L[slot] = v; S().equip(); refresh(); } });
      s.append(el('option', { value: '' }, '— none —'));
      for (const w of p.weapons.filter(filter)) { const a = assignedTo(w.uid); s.append(el('option', { value: w.uid, selected: L[slot] === w.uid }, `${D.WEAPON[w.base].name} ${w.engraving ? '"' + w.engraving + '" ' : ''}[${w.serial}]${a && a !== op ? ' (' + a.callsign + ')' : ''}`)); }
      return s;
    };
    const opts = (list, val, fn, labelFn) => { const s = el('select', { onchange: e => { fn(e.target.value); S().equip(); refresh(); } }); for (const x of list) s.append(el('option', { value: x.id, selected: x.id === val }, labelFn ? labelFn(x) : x.name)); return s; };
    pan.append(el('div', { class: 'formgrid' },
      'Primary', wsel('primary', w => D.WEAPON[w.base].cat !== 'hg'),
      'Secondary', wsel('secondary', w => D.WEAPON[w.base].cat === 'hg'),
      'Armor', opts(D.ARMOR.filter(a => p.armorOwned.includes(a.id)), L.armor, v => L.armor = v, a => `${a.name} (prot ${a.prot}, speed ×${a.speed})`),
      'Helmet', opts(D.HELMETS.filter(a => p.helmetsOwned.includes(a.id)), L.helmet, v => L.helmet = v, a => `${a.name} (prot ${a.prot})`),
      'Ammunition', opts(D.AMMO.filter(a => p.ammoOwned.includes(a.id)), L.ammo, v => L.ammo = v),
      'Tool', opts([{ id: '', name: '— none —' }, ...D.ITEMS.filter(i => i.kind === 'tool' && (p.stock[i.id] || 0) > 0)], L.tool || '', v => L.tool = v || null, i => i.id ? `${i.name} (${p.stock[i.id]} owned)` : i.name),
    ));
    const us = el('div', { class: 'row wrap', style: 'margin-top:6px' }, el('span', { class: 'dim' }, 'Utility (4 slots):'));
    for (let i = 0; i < 4; i++) {
      const s = el('select', { style: 'width:120px', onchange: e => { const v = e.target.value; const arr = L.util.slice(); if (v) arr[i] = v; else arr.splice(i, 1); L.util = arr.filter(Boolean).slice(0, 4); S().equip(); refresh(); } });
      s.append(el('option', { value: '' }, '— empty —'));
      for (const it of D.ITEMS.filter(x => x.kind === 'util')) s.append(el('option', { value: it.id, selected: L.util[i] === it.id }, `${it.name} (${p.stock[it.id] || 0})`));
      us.append(s);
    }
    pan.append(us);
    if (L.tool === 'shield') pan.append(el('div', { class: 'dim', style: 'font-size:11px;margin-top:4px' }, 'Shield operators fight with their secondary (handgun).'));
    // weapon preview
    const pw = L.primary && ST.weaponByUid(L.primary);
    if (pw) { const c = el('canvas', { width: 640, height: 200, style: 'width:320px;height:100px;margin-top:8px;background:#0a0e12;border:1px solid #222c36' }); BP.art.weapon(c.getContext('2d'), pw, 320, 100, 2.8, 0.1); pan.append(c); }
    return pan;
  }
  function appearancePanel(op) {
    const L = op.look;
    const pan = el('div', { class: 'panel' }, el('h3', {}, 'Appearance & Uniform'));
    const sel = (label, arr, key, isIndex = true) => { const s = el('select', { onchange: e => { L[key] = isIndex ? +e.target.value : e.target.value; refresh(); } }); arr.forEach((v, i) => s.append(el('option', { value: isIndex ? i : v, selected: (isIndex ? i : v) === L[key] }, typeof v === 'string' ? v : v.name))); return [label, s]; };
    const toggle = (label, key) => [label, el('input', { type: 'checkbox', checked: !!L[key], onchange: e => { L[key] = e.target.checked; refresh(); } })];
    const uc = BP.art.uniformColors(L);
    const color = (label, key, def) => [label, el('span', { class: 'row' }, el('input', { type: 'color', value: (L[key] || def || '#333333').slice(0, 7), onchange: e => { L[key] = e.target.value; refresh(); } }), L[key] ? el('button', { class: 'sm', onclick: () => { L[key] = null; refresh(); } }, 'reset') : '')];
    const genderSel = ['Gender', (() => { const s = el('select', { onchange: e => { op.gender = e.target.value; refresh(); } }); s.append(el('option', { value: 'm', selected: op.gender === 'm' }, 'Male'), el('option', { value: 'f', selected: op.gender === 'f' }, 'Female')); return s; })()];
    const skin = ['Skin tone', el('span', { class: 'row' }, ...D.SKIN.map((c, i) => el('span', { style: `display:inline-block;width:18px;height:18px;background:${c};border:2px solid ${i === L.skin ? '#e2a33a' : '#222'};cursor:pointer`, onclick: () => { L.skin = i; refresh(); } })))];
    const hairC = ['Hair color', el('span', { class: 'row' }, ...D.HAIR_COLORS.map((c, i) => el('span', { style: `display:inline-block;width:16px;height:16px;background:${c};border:2px solid ${i === L.hairColor ? '#e2a33a' : '#222'};cursor:pointer`, onclick: () => { L.hairColor = i; refresh(); } })))];
    const uni = ['Uniform preset', (() => { const s = el('select', { onchange: e => { L.uniform = e.target.value; L.shirt = L.pants = L.carrier = null; refresh(); } }); D.UNIFORMS.forEach(u => s.append(el('option', { value: u.id, selected: u.id === L.uniform }, u.name))); return s; })()];
    const unitId = ['Unit ID', el('input', { value: L.unitId || '', style: 'width:100px', onchange: e => { L.unitId = e.target.value.slice(0, 8); refresh(); } })];
    const rows = [genderSel, sel('Face', D.FACES, 'face'), skin, sel('Hairstyle', D.HAIR, 'hair'), hairC, sel('Facial hair', D.FACIAL, 'facial'), sel('Build', D.BUILDS, 'build'),
      uni, color('Shirt color', 'shirt', uc.shirt), color('Pants color', 'pants', uc.pants), color('Plate carrier', 'carrier', uc.carrier), toggle('Wear helmet', 'helmet'), toggle('Balaclava', 'balaclava'),
      sel('Eye protection', D.EYEPRO, 'eyepro'), sel('Headset', D.HEADSET, 'headset'), sel('Gloves', D.GEAR_EXTRAS.gloves, 'gloves'), sel('Boots', D.GEAR_EXTRAS.boots, 'boots'), sel('Kneepads', D.GEAR_EXTRAS.kneepads, 'kneepads'),
      sel('Watch', D.GEAR_EXTRAS.watch, 'watch'), sel('Shoulder patch', D.PATCHES, 'patch'), unitId];
    const grid = el('div', { style: 'display:grid;grid-template-columns:repeat(3, 110px 1fr);gap:6px 10px;align-items:center;font-size:12px' });
    for (const [a, b] of rows) grid.append(el('span', { class: 'dim' }, a), b);
    pan.append(grid);
    pan.append(el('div', { class: 'row', style: 'margin-top:8px' }, el('span', { class: 'dim' }, 'Apply uniform to whole roster:'), el('button', { class: 'sm', onclick: () => { for (const o of P().operators) { o.look.uniform = L.uniform; o.look.shirt = L.shirt; o.look.pants = L.pants; o.look.carrier = L.carrier; o.look.unitId = L.unitId; } refresh(); ST.toast('Uniform applied to all operators.'); } }, 'Apply')));
    return pan;
  }
  function recruit() {
    const p = P(); const rng = U.rng(p.day * 31 + p.operators.length * 7);
    const q = 0.9 + ST.tier() * 0.15;
    const cands = [0, 1, 2].map(i => { const o = ST.genOperator(rng, { quality: q + (i === 2 ? 0.4 : 0) }); for (let k = 0; k < 20 && p.operators.some(x => x.callsign === o.callsign); k++) o.callsign = rng.pick(D.CALLSIGNS); o.cost = Math.round((3000 + Object.values(o.stats).reduce((a, b) => a + b, 0) * 12) / 100) * 100; return o; });
    const box = el('div', {}, el('h2', {}, 'Recruitment'), el('p', { class: 'dim' }, `Roster ${p.operators.length}/12. Candidates refresh daily.`));
    const row = el('div', { class: 'grid3' });
    for (const c of cands) {
      const card = el('div', { class: 'panel col' }, portraitCanvas(c, 120, 140), el('b', {}, `${c.name} "${c.callsign}"`), el('div', { class: 'dim' }, `${D.ROLE[c.role].name} · ${D.PERS[c.personality].name}`), el('div', { class: 'dim', style: 'font-size:11px' }, c.bio));
      for (const s of D.STATS) card.append(el('div', { class: 'statbar' }, el('span', {}, s.name), el('div', { class: 'bar' }, el('i', { style: `width:${c.stats[s.id]}%` })), el('span', {}, c.stats[s.id])));
      card.append(el('button', { class: 'primary', disabled: p.operators.length >= 12, onclick: () => { if (!spend(c.cost, `Hired ${c.callsign}`)) return; delete c.cost; c.color = ST.OP_COLORS[p.operators.length % ST.OP_COLORS.length]; c.loadout.util = ['flash']; p.operators.push(c); selOp = c; m.close(); refresh(); } }, `Hire — ${U.fmtMoney(c.cost)}`));
      row.append(card);
    }
    box.append(row, el('button', { style: 'margin-top:10px', onclick: () => m.close() }, 'Close'));
    const m = modal(box);
  }

  // ================= Armory =================
  function armory(main) {
    const sub = el('div', { class: 'row', style: 'margin-bottom:10px' });
    for (const [id, n] of [['weapons', 'Gunsmith'], ['store', 'Weapon Store'], ['equip', 'Equipment'], ['armor', 'Armor & Ammunition']]) sub.append(el('button', { class: armTab === id ? 'on' : '', onclick: () => { armTab = id; renderTab(); } }, n));
    main.append(sub);
    if (armTab === 'weapons') gunsmith(main);
    if (armTab === 'store') store(main);
    if (armTab === 'equip') equipment(main);
    if (armTab === 'armor') armorShop(main);
  }
  function gunsmith(main) {
    const p = P();
    if (!selWeapon || !p.weapons.includes(selWeapon)) selWeapon = p.weapons[0];
    const wrap = el('div', { class: 'split' });
    const list = el('div', { class: 'op-list scroll', style: 'max-height:calc(100vh - 150px)' });
    for (const w of p.weapons) {
      const a = assignedTo(w.uid), b = D.WEAPON[w.base];
      const c = el('canvas', { width: 180, height: 60, style: 'width:90px;height:30px' }); BP.art.weapon(c.getContext('2d'), w, 90, 30, 0.75, 0, { wear: false });
      list.append(el('div', { class: 'op-row' + (w === selWeapon ? ' sel' : ''), onclick: () => { selWeapon = w; renderTab(); } }, c, el('div', { class: 'grow' }, el('div', { style: 'font-weight:600' }, b.name + (w.engraving ? ` "${w.engraving}"` : '')), el('div', { class: 'dim', style: 'font-size:11px' }, `${D.CATS[b.cat].name} · ${w.serial}${a ? ' · ' + a.callsign : ''} · ${Math.round(w.condition)}%`))));
    }
    wrap.append(list);
    if (selWeapon) wrap.append(gunsmithDetail(selWeapon));
    main.append(wrap);
  }
  function statRows(cur, basew) {
    const rows = [['Weight', cur.weight, basew.weight, 8, true, 'kg'], ['Handling', cur.handling, basew.handling, 100], ['Recoil', cur.recoil, basew.recoil, 100, true], ['Accuracy', cur.accuracy, basew.accuracy, 100], ['Ergonomics', cur.ergonomics, basew.ergonomics, 100], ['Aim Speed', cur.aimSpeed, basew.aimSpeed, 100], ['Reliability', cur.reliability, basew.reliability, 100], ['Suppression', Math.max(0, cur.suppression), Math.max(0, basew.suppression), 100], ['Visibility', cur.visibility, basew.visibility, 100, true]];
    const box = el('div', {});
    for (const [n, v, b, max, lowerBetter, unit] of rows) {
      const better = lowerBetter ? v < b : v > b, worse = lowerBetter ? v > b : v < b;
      box.append(el('div', { class: 'statbar' }, el('span', {}, n), el('div', { class: 'bar' }, el('i', { style: `width:${Math.min(100, Math.min(v, b) / max * 100)}%` }), Math.abs(v - b) > 0.01 ? el('i', { class: better ? 'plus' : 'minus', style: `left:${Math.min(v, b) / max * 100}%;width:${Math.abs(v - b) / max * 100}%` }) : ''), el('span', { class: better ? 'green' : worse ? 'red' : '' }, unit ? v.toFixed(2) : Math.round(v))));
    }
    box.append(el('div', { class: 'kv', style: 'margin-top:6px' }, el('span', {}, 'Caliber'), el('span', {}, D.CAL[cur.cal].name), el('span', {}, 'Capacity'), el('span', {}, `${cur.mag} rds`), el('span', {}, 'Rate of fire'), el('span', {}, `${cur.rpm} rpm (${cur.mode})`), el('span', {}, 'Damage / Pen'), el('span', {}, `${cur.dmg.toFixed(0)} × ${cur.pellets} / ${cur.pen}`), el('span', {}, 'Reload'), el('span', {}, `${cur.reload.toFixed(2)} s`), el('span', {}, 'Weapon light'), el('span', {}, cur.light ? 'Yes' : 'No')));
    return box;
  }
  function gunsmithDetail(w) {
    const p = P(), b = D.WEAPON[w.base];
    const col = el('div', { class: 'col' });
    const cv = el('canvas', { width: 1400, height: 520, style: 'width:100%;max-width:700px;height:auto;aspect-ratio:1400/520;background:radial-gradient(ellipse,#1d2833,#0a0e12);border:1px solid #26313d;cursor:grab' });
    const drawW = () => { const c = cv.getContext('2d'); c.clearRect(0, 0, cv.width, cv.height); c.strokeStyle = 'rgba(255,255,255,0.03)'; for (let i = 0; i < cv.width; i += 40) { c.beginPath(); c.moveTo(i, 0); c.lineTo(i, cv.height); c.stroke(); } for (let i = 0; i < cv.height; i += 40) { c.beginPath(); c.moveTo(0, i); c.lineTo(cv.width, i); c.stroke(); } BP.art.weapon(c, w, cv.width / 2, cv.height / 2, wzoom * 1.6, yaw); c.fillStyle = 'rgba(200,210,220,0.5)'; c.font = '22px monospace'; c.fillText(`${b.name} · ${w.serial}`, 20, 34); };
    let dragX = null;
    cv.style.touchAction = 'none';
    cv.onpointerdown = e => { dragX = e.clientX; cv.style.cursor = 'grabbing'; try { cv.setPointerCapture(e.pointerId); } catch (er) { } };
    cv.onpointerup = cv.onpointercancel = () => { dragX = null; cv.style.cursor = 'grab'; };
    cv.onpointermove = e => { if (dragX === null) return; yaw += (e.clientX - dragX) * 0.012; dragX = e.clientX; drawW(); };
    cv.onwheel = e => { e.preventDefault(); wzoom = U.clamp(wzoom * (e.deltaY < 0 ? 1.1 : 0.9), 1, 5); drawW(); };
    setTimeout(drawW, 0);
    col.append(el('div', { class: 'panel' }, el('div', { class: 'row sp' }, el('h2', {}, b.name), el('span', { class: 'dim' }, 'Drag to rotate · wheel to zoom')), cv,
      el('div', { class: 'row' }, 'Rotate', el('input', { type: 'range', min: -314, max: 314, value: Math.round(yaw * 100), oninput: e => { yaw = e.target.value / 100; drawW(); } }), 'Zoom', el('input', { type: 'range', min: 100, max: 500, value: Math.round(wzoom * 100), oninput: e => { wzoom = e.target.value / 100; drawW(); } }))));
    const g = el('div', { class: 'grid2' });
    const slots = el('div', { class: 'panel' }, el('h3', {}, 'Attachments'));
    const tier = ST.tier();
    for (const slot of D.CATS[b.cat].slots) {
      const s = el('select', { onchange: e => {
        const v = e.target.value; const a = D.ATT[v];
        if (v && a && !spend(a.price, `${a.name} installed`)) { renderTab(); return; }
        if (v) w.att[slot] = v; else delete w.att[slot];
        S().equip(); refresh();
      } });
      s.append(el('option', { value: '' }, slot === 'optic' ? 'Iron sights' : 'Standard / none'));
      for (const a of D.ATTACH.filter(a => a.slot === slot && a.cats.includes(b.cat))) {
        const locked = a.tier > tier;
        s.append(el('option', { value: a.id, selected: w.att[slot] === a.id, disabled: locked }, `${a.name} — ${w.att[slot] === a.id ? 'installed' : U.fmtMoney(a.price)}${locked ? ` (Tier ${a.tier})` : ''}`));
      }
      const desc = w.att[slot] ? D.ATT[w.att[slot]].desc : '';
      slots.append(el('div', { class: 'slot-row' }, el('span', { class: 'dim' }, D.SLOT_NAMES[slot]), el('div', {}, s, desc ? el('div', { class: 'dim', style: 'font-size:10px' }, desc) : '')));
    }
    g.append(slots);
    const cur = ST.weaponStats(w), basew = ST.weaponStats({ ...w, att: {} });
    g.append(el('div', { class: 'panel' }, el('h3', {}, 'Statistics (vs. stock configuration)'), statRows(cur, basew)));
    col.append(g);
    // appearance
    const fin = el('select', { onchange: e => { w.finish = e.target.value; refresh(); } });
    for (const f of D.FINISHES) fin.append(el('option', { value: f.id, selected: f.id === w.finish }, f.name));
    const a = assignedTo(w.uid);
    const value = ST.weaponValue(w);
    const maint = Math.round((100 - w.condition) * b.price / 200 / 10) * 10;
    col.append(el('div', { class: 'panel' }, el('h3', {}, 'Finish, Markings & Condition'),
      el('div', { class: 'formgrid' }, 'Finish', fin,
        ...(w.finish === 'custom' ? ['Camo colors', el('span', { class: 'row' }, el('input', { type: 'color', value: w.camo[0], onchange: e => { w.camo[0] = e.target.value; refresh(); } }), el('input', { type: 'color', value: w.camo[1], onchange: e => { w.camo[1] = e.target.value; refresh(); } }))] : []),
        'Engraving', el('input', { value: w.engraving, maxlength: 22, placeholder: 'e.g. "Lucky"', onchange: e => { w.engraving = e.target.value.slice(0, 22); refresh(); } }),
        'Serial number', el('span', { class: 'mono' }, w.serial),
        'Condition', el('span', { class: 'row' }, el('div', { class: 'bar hp', style: 'width:160px' }, el('i', { style: `width:${w.condition}%` })), `${Math.round(w.condition)}% · ${w.roundsFired} rounds fired`, maint > 0 ? el('button', { class: 'sm', onclick: () => { if (spend(maint, 'Weapon serviced')) { w.condition = 100; refresh(); } } }, `Maintain ${U.fmtMoney(maint)}`) : ''),
        'Assigned to', el('span', {}, a ? `${a.callsign} (${a.name})` : '— unassigned —'),
        'Value', el('span', { class: 'price' }, U.fmtMoney(value))),
      el('div', { class: 'row', style: 'margin-top:8px' }, el('button', { class: 'sm danger', disabled: !!a, onclick: () => { if (!confirm('Sell this weapon for 40% of its value?')) return; p.money += Math.round(value * 0.4); p.weapons = p.weapons.filter(x => x !== w); selWeapon = null; S().cash(); refresh(); } }, `Sell (${U.fmtMoney(Math.round(value * 0.4))})`),
        el('button', { class: 'sm', onclick: () => { const c = JSON.parse(JSON.stringify(w)); const cost = ST.weaponValue(c); if (!spend(cost, 'Duplicate build purchased')) return; c.uid = U.uid('w'); c.serial = 'TF-' + Math.floor(10000 + Math.random() * 89999); c.condition = 100; c.roundsFired = 0; p.weapons.push(c); refresh(); } }, `Buy duplicate of this build (${U.fmtMoney(value)})`))));
    return col;
  }
  function store(main) {
    const p = P(), tier = ST.tier();
    const tabs = el('div', { class: 'row' });
    for (const [k, c] of Object.entries(D.CATS)) tabs.append(el('button', { class: storeCat === k ? 'on' : '', onclick: () => { storeCat = k; renderTab(); } }, c.name));
    main.append(tabs);
    const g = el('div', { class: 'grid3', style: 'margin-top:10px' });
    for (const w of D.WEAPONS.filter(x => x.cat === storeCat)) {
      const locked = w.tier > tier;
      const owned = p.weapons.filter(x => x.base === w.base || x.base === w.id).length;
      const inst = ST.newWeapon(w.id);
      const c = el('canvas', { width: 600, height: 200, style: 'width:100%;height:auto;aspect-ratio:3/1;background:#0a0e12' }); BP.art.weapon(c.getContext('2d'), inst, 300, 100, 2.2, 0, { wear: false });
      const st = ST.weaponStats(inst);
      g.append(el('div', { class: 'panel col' + (locked ? ' locked' : ''), style: locked ? 'opacity:.5' : '' }, el('div', { class: 'row sp' }, el('b', {}, w.name), el('span', { class: 'dim' }, `${D.CAL[w.cal].name} · ${owned} owned`)), c,
        el('div', { class: 'kv' }, el('span', {}, 'Capacity'), el('span', {}, w.mag), el('span', {}, 'RPM'), el('span', {}, `${w.rpm} ${w.mode}`), el('span', {}, 'Accuracy'), el('span', {}, Math.round(st.accuracy)), el('span', {}, 'Handling'), el('span', {}, Math.round(st.handling)), el('span', {}, 'Recoil'), el('span', {}, Math.round(st.recoil)), el('span', {}, 'Reliability'), el('span', {}, Math.round(st.reliability)), el('span', {}, 'Weight'), el('span', {}, st.weight.toFixed(1) + ' kg')),
        locked ? el('div', { class: 'red' }, `Requires supplier license — Tier ${w.tier} (dismantle ${D.ORGS[w.tier - 2] ? D.ORGS[w.tier - 2].name : 'previous org'})`) :
          el('button', { class: 'primary', onclick: () => { if (spend(w.price, `${w.name} purchased`)) { const n = ST.newWeapon(w.id); p.weapons.push(n); selWeapon = n; refresh(); } } }, `Buy — ${U.fmtMoney(w.price)}`)));
    }
    main.append(g);
  }
  function equipment(main) {
    const p = P();
    const t = el('table', { class: 't' }, el('tr', {}, el('th', {}, 'Item'), el('th', {}, 'Description'), el('th', {}, 'In stock'), el('th', {}, 'Price'), el('th', {}, '')));
    for (const it of D.ITEMS) {
      const price = Math.round(it.price * ST.diff().cost);
      t.append(el('tr', {}, el('td', {}, el('b', {}, it.name), el('div', { class: 'dim', style: 'font-size:10px' }, it.kind === 'util' ? 'Consumable' : 'Tool (reusable)')), el('td', { class: 'dim' }, it.desc), el('td', { class: 'mono' }, p.stock[it.id] || 0), el('td', { class: 'price' }, U.fmtMoney(price)),
        el('td', {}, el('button', { class: 'sm', onclick: () => { if (spend(price)) { p.stock[it.id] = (p.stock[it.id] || 0) + 1; refresh(); } } }, '+1'), it.kind === 'util' ? el('button', { class: 'sm', onclick: () => { if (spend(price * 5)) { p.stock[it.id] = (p.stock[it.id] || 0) + 5; refresh(); } } }, '+5') : '')));
    }
    main.append(el('div', { class: 'panel' }, el('h2', {}, 'Tactical Equipment'), el('p', { class: 'dim' }, 'Consumables are drawn from stock when you deploy; anything not used is returned afterwards. Tools are assigned to one operator each.'), t));
  }
  function armorShop(main) {
    const p = P();
    const sec = (title, list, owned, label) => {
      const t = el('table', { class: 't' });
      for (const a of list) {
        const has = owned.includes(a.id);
        t.append(el('tr', {}, el('td', {}, el('b', {}, a.name)), el('td', { class: 'dim' }, label(a)), el('td', { class: 'price' }, has ? 'OWNED' : U.fmtMoney(a.price)),
          el('td', {}, has ? '' : el('button', { class: 'sm primary', onclick: () => { if (spend(a.price, `${a.name} unlocked for the unit`)) { owned.push(a.id); refresh(); } } }, 'Purchase'))));
      }
      return el('div', { class: 'panel' }, el('h3', {}, title), t);
    };
    main.append(el('div', { class: 'grid2' },
      sec('Body Armor (unit-wide license)', D.ARMOR, p.armorOwned, a => `Protection ${a.prot} · speed ×${a.speed} · stamina ×${a.stam}`),
      sec('Helmets (unit-wide)', D.HELMETS, p.helmetsOwned, a => `Protection ${a.prot}`),
      sec('Ammunition Types', D.AMMO, p.ammoOwned, a => `${a.desc} Damage ×${a.dmg}, penetration ×${a.pen}, cost ×${a.cost}`)));
  }

  // ================= Training =================
  function training(main) {
    const p = P();
    const lvl = p.hq.training;
    main.append(el('h2', {}, `Training Facility — Level ${lvl}`));
    const courses = [['acc', 'Marksmanship Range'], ['awa', 'Threat Recognition'], ['mob', 'Agility Course'], ['end', 'Endurance PT'], ['dis', 'ROE & Discipline'], ['med', 'Tactical Medicine'], ['tec', 'Breaching & Electronics'], ['lea', 'Leadership Course']];
    const t = el('table', { class: 't' }, el('tr', {}, el('th', {}, 'Operator'), ...courses.map(c => el('th', {}, c[1])), el('th', {}, 'Fatigue')));
    for (const op of p.operators) {
      const row = el('tr', {}, el('td', {}, el('b', {}, op.callsign), el('div', { class: 'dim' }, D.ROLE[op.role].name)));
      for (const [id] of courses) {
        const cost = Math.round((800 + op.stats[id] * 25) / (1 + (lvl - 1) * 0.2) / 50) * 50;
        const can = op.injury <= 0 && op.fatigue < 80 && op.stats[id] < 95;
        row.append(el('td', {}, el('div', {}, op.stats[id]), el('button', { class: 'sm', disabled: !can, title: `+${2 + lvl}–${4 + lvl}`, onclick: () => { if (!spend(cost)) return; const g = 2 + lvl + Math.floor(Math.random() * 3); op.stats[id] = Math.min(99, op.stats[id] + g); op.fatigue += 20; ST.addXP(op, 40); ST.toast(`${op.callsign}: ${D.STATS.find(s => s.id === id).name} +${g}`); refresh(); } }, U.fmtMoney(cost))));
      }
      row.append(el('td', {}, Math.round(op.fatigue)));
      t.append(row);
    }
    main.append(el('div', { class: 'panel scroll' }, t));
    const upCost = lvl * 20000;
    main.append(el('div', { class: 'grid2', style: 'margin-top:12px' },
      el('div', { class: 'panel' }, el('h3', {}, 'Kill House Exercise'), el('p', { class: 'dim' }, 'Run a live squad drill with training ammunition against paper targets and no-shoot targets. Practices room-clearing coordination. Grants experience; no injuries.'),
        el('button', { class: 'primary', onclick: () => { const rng = U.rng(Date.now()); BP.brief.open({ id: 'train', name: 'Kill House Exercise', type: 'warrant', mapType: 'training', org: null, orgName: 'Training', seed: rng.int(1, 99999), enemies: 8, hostages: 0, civilians: 4, evidence: 0, contraband: 0, reward: 0, parTime: 120, brief: 'Clear the kill house. Hit all targets, avoid the no-shoot targets. Training rounds — no live fire injuries.', intel: 'Facility exercise.', tier: 1, obj: ['neutralize'], training: true }); } }, 'Start Exercise')),
      el('div', { class: 'panel' }, el('h3', {}, 'Facility Upgrade'), el('p', { class: 'dim' }, 'Higher facility level makes courses cheaper and more effective.'), lvl < 3 ? el('button', { onclick: () => { if (spend(upCost, 'Training facility upgraded')) { p.hq.training++; refresh(); } } }, `Upgrade to Level ${lvl + 1} — ${U.fmtMoney(upCost)}`) : el('span', { class: 'green' }, 'Maximum level'))));
  }

  // ================= Medical =================
  function medical(main) {
    const p = P(), lvl = p.hq.medical;
    main.append(el('h2', {}, `Medical Bay — Level ${lvl}`));
    const t = el('table', { class: 't' }, el('tr', {}, el('th', {}, 'Operator'), el('th', {}, 'Status'), el('th', {}, 'Injury'), el('th', {}, 'Recovery'), el('th', {}, 'Fatigue'), el('th', {}, 'Morale'), el('th', {}, '')));
    for (const op of p.operators) {
      const exp = Math.round(1500 / (1 + (lvl - 1) * 0.25));
      t.append(el('tr', {}, el('td', {}, el('b', {}, op.callsign), ' ', el('span', { class: 'dim' }, op.name)), el('td', {}, opStatusTag(op)), el('td', {}, op.injuryDesc || '—'), el('td', {}, op.injury > 0 ? `${op.injury} days` : '—'),
        el('td', {}, el('div', { class: 'bar', style: 'width:100px' }, el('i', { style: `width:${op.fatigue}%;background:#e2a33a` }))), el('td', {}, Math.round(op.morale)),
        el('td', {}, op.injury > 0 ? el('button', { class: 'sm', onclick: () => { if (spend(exp, 'Treatment expedited')) { op.injury--; if (op.injury <= 0) { op.injury = 0; op.status = 'active'; op.injuryDesc = ''; } refresh(); } } }, `Expedite 1 day — ${U.fmtMoney(exp)}`) : op.fatigue > 0 ? el('button', { class: 'sm', onclick: () => { if (spend(400, 'Physio session')) { op.fatigue = Math.max(0, op.fatigue - 30); refresh(); } } }, 'Physio −30 fatigue ($400)') : '')));
    }
    main.append(el('div', { class: 'panel' }, t));
    main.append(el('div', { class: 'grid2', style: 'margin-top:12px' },
      el('div', { class: 'panel' }, el('h3', {}, 'Rest'), el('p', { class: 'dim' }, 'Advance one day: injuries heal by a day, fatigue drops, morale recovers.'), el('button', { onclick: () => { ST.advanceDay(1 + (lvl - 1) * 0); p.opsBoard = null; refresh(); } }, 'Rest one day')),
      el('div', { class: 'panel' }, el('h3', {}, 'Upgrade'), lvl < 3 ? el('button', { onclick: () => { if (spend(lvl * 18000, 'Medical bay upgraded')) { p.hq.medical++; refresh(); } } }, `Upgrade to Level ${lvl + 1} — ${U.fmtMoney(lvl * 18000)}`) : el('span', { class: 'green' }, 'Maximum level'))));
    if (p.fallen.length) {
      const mem = el('div', { class: 'panel', style: 'margin-top:12px' }, el('h3', {}, 'Memorial Wall'));
      for (const f of p.fallen) mem.append(el('div', { class: 'row' }, portraitCanvas(f, 40, 48), el('div', {}, el('b', {}, `${f.name} "${f.callsign}"`), el('div', { class: 'dim' }, `${f.missions} operations · fell on day ${f.diedDay} — ${f.diedMission}`))));
      main.append(mem);
    }
  }

  // ================= Garage =================
  function garage(main) {
    const p = P();
    main.append(el('h2', {}, 'Garage'), el('p', { class: 'dim' }, 'The active vehicle determines how many operators you can deploy.'));
    const g = el('div', { class: 'grid2' });
    for (const v of p.vehicles) {
      const def = D.VEH[v.id];
      const c = el('canvas', { width: 520, height: 220, style: 'width:260px;height:110px;background:#2c2e31' });
      const ctx = c.getContext('2d'); ctx.setTransform(1.1, 0, 0, 1.1, 0, 0); BP.art.vehicle(ctx, def, v, 520 / 2.2, 220 / 2.2, 0, 0);
      const active = p.activeVehicle === v.uid;
      g.append(el('div', { class: 'panel' + (active ? ' sel' : ''), style: active ? 'border-color:#e2a33a' : '' }, el('div', { class: 'row sp' }, el('b', {}, def.name), el('span', { class: 'tag' + (active ? ' acc' : '') }, active ? 'ACTIVE' : `${def.seats} seats`)), c,
        el('div', { class: 'formgrid', style: 'margin-top:6px' }, 'Seats', el('span', {}, def.seats + (def.armored ? ' · armored' : '')),
          'Paint', el('input', { type: 'color', value: v.paint, onchange: e => { v.paint = e.target.value; refresh(); } }),
          'Unit markings', el('input', { value: v.markings, maxlength: 10, onchange: e => { v.markings = e.target.value; refresh(); } }),
          'Light config', (() => { const s = el('select', { onchange: e => { v.lights = e.target.value; refresh(); } }); for (const [k, n] of [['none', 'None'], ['hidden', 'Hidden grille lights'], ['full', 'Full light bar']]) s.append(el('option', { value: k, selected: v.lights === k }, n)); return s; })(),
          'Accessories', el('span', { class: 'row' }, ...['pushbar', 'ladder'].map(a => el('label', {}, el('input', { type: 'checkbox', checked: v.accessories.includes(a), onchange: e => { v.accessories = e.target.checked ? [...v.accessories, a] : v.accessories.filter(x => x !== a); refresh(); } }), a === 'pushbar' ? ' Push bar ' : ' Ladder rack ')))),
        active ? '' : el('button', { style: 'margin-top:6px', onclick: () => { p.activeVehicle = v.uid; refresh(); } }, 'Set as deployment vehicle')));
    }
    main.append(g);
    const shop = el('div', { class: 'panel', style: 'margin-top:12px' }, el('h3', {}, 'Vehicle Dealer'));
    for (const def of D.VEHICLES.filter(d => !p.vehicles.some(v => v.id === d.id))) shop.append(el('div', { class: 'row sp', style: 'margin:4px 0' }, el('span', {}, el('b', {}, def.name), el('span', { class: 'dim' }, ` — ${def.seats} seats${def.armored ? ', armored' : ''}`)), el('button', { class: 'sm primary', onclick: () => { if (spend(def.price, `${def.name} purchased`)) { p.vehicles.push({ id: def.id, uid: U.uid('v'), paint: def.color, markings: 'TF-1', lights: 'hidden', accessories: [] }); refresh(); } } }, U.fmtMoney(def.price))));
    main.append(shop);
  }

  // ================= Evidence =================
  function evidence(main) {
    const p = P();
    main.append(el('h2', {}, 'Evidence Room — Case Files'));
    const g = el('div', { class: 'grid2' });
    for (const org of D.ORGS) {
      const items = p.evidence.filter(e => e.org === org.id);
      const tk = D.CAMPAIGN.find(m => m.org === org.id && m.takedown);
      const intel = p.campaign.intel[org.id];
      const reveals = [[1, `Network size estimated. Front businesses identified.`], [2, `Lieutenants identified by name.`], [3, `Principal: ${org.boss}. Known locations mapped.`], [tk.intelReq, `Takedown warrant approved — full floor plans and guard rotations available.`]];
      const pan = el('div', { class: 'panel' }, el('div', { class: 'row sp' }, el('h3', { style: `color:${org.color}` }, org.name), el('span', { class: 'tag' }, `INTEL ${intel}/${tk.intelReq}`)),
        el('div', { class: 'bar', style: 'margin:6px 0' }, el('i', { style: `width:${Math.min(100, intel / tk.intelReq * 100)}%;background:${org.color}` })));
      for (const [n, txt] of reveals) pan.append(el('div', { style: `font-size:12px;color:${intel >= n ? '#c9d3dc' : '#4a5560'}` }, intel >= n ? '▸ ' + txt : '▸ [REDACTED — need ' + n + ' intel]'));
      const list = el('div', { style: 'margin-top:8px;max-height:150px;overflow:auto' });
      for (const e of items.slice().reverse()) list.append(el('div', { style: 'font-size:11px', class: 'dim' }, `Day ${e.day} · ${e.mission}: `, el('span', { style: 'color:#c9d3dc' }, e.name)));
      if (!items.length) list.append(el('div', { class: 'dim', style: 'font-size:11px' }, 'No evidence collected yet.'));
      pan.append(list);
      g.append(pan);
    }
    main.append(g);
    const other = p.evidence.filter(e => !e.org);
    const seized = p.stats.seized || 0;
    main.append(el('div', { class: 'panel', style: 'margin-top:12px' }, el('h3', {}, 'Other Operations'), el('div', { class: 'dim' }, `${other.length} items logged from standalone operations. Total contraband seized: ${U.fmtMoney(seized)} street value.`)));
  }

  return { show, renderTab, missionFromCampaign, modal, refresh, get tab() { return tab; } };
})();
