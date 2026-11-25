// Static game data: weapons, attachments, gear, operators, organizations, missions
'use strict';
BP.D = (() => {
  const CAL = {
    '5.56': { name: '5.56×45mm', dmg: 34, pen: 32, cost: 1.0 },
    '5.45': { name: '5.45×39mm', dmg: 32, pen: 34, cost: 0.9 },
    '7.62': { name: '7.62×51mm', dmg: 47, pen: 46, cost: 1.8 },
    '9mm': { name: '9×19mm', dmg: 25, pen: 14, cost: 0.5 },
    '.45': { name: '.45 ACP', dmg: 31, pen: 11, cost: 0.7 },
    '4.6': { name: '4.6×30mm', dmg: 22, pen: 32, cost: 1.0 },
    '5.7': { name: '5.7×28mm', dmg: 22, pen: 30, cost: 1.0 },
    '12g': { name: '12 Gauge', dmg: 13, pen: 6, cost: 1.2, pellets: 9 },
    '.338': { name: '.338 Lapua', dmg: 78, pen: 66, cost: 4.0 },
  };

  const CATS = {
    ar: { name: 'Assault Rifles', reload: 2.4, slots: ['optic', 'muzzle', 'under', 'side', 'stock', 'grip', 'hand', 'barrel', 'mag', 'sling'] },
    smg: { name: 'Submachine Guns', reload: 2.1, slots: ['optic', 'muzzle', 'under', 'side', 'stock', 'grip', 'mag', 'sling'] },
    br: { name: 'Battle Rifles', reload: 2.8, slots: ['optic', 'muzzle', 'under', 'side', 'stock', 'grip', 'hand', 'barrel', 'mag', 'sling'] },
    sg: { name: 'Shotguns', reload: 0.5, slots: ['optic', 'side', 'stock', 'barrel', 'mag', 'sling'] },
    sr: { name: 'Sniper Rifles', reload: 3.2, slots: ['optic', 'muzzle', 'under', 'stock', 'barrel', 'mag', 'sling'] },
    hg: { name: 'Handguns', reload: 1.6, slots: ['optic', 'muzzle', 'side', 'mag'] },
  };

  // geo: drawing parameters for the procedural weapon renderer
  const W = (id, name, cat, cal, o) => ({ id, name, cat, cal, ...o });
  const WEAPONS = [
    W('m4a1', 'M4A1', 'ar', '5.56', { mode: 'auto', rpm: 800, mag: 30, spread: 2.2, recoil: 42, handling: 68, weight: 3.1, rel: 85, price: 1400, tier: 1, geo: { recv: 30, barrel: 34, stock: 'collapsible', magT: 'stanag', hand: 30, rail: true } }),
    W('hk416', 'HK416', 'ar', '5.56', { mode: 'auto', rpm: 850, mag: 30, spread: 2.0, recoil: 38, handling: 66, weight: 3.5, rel: 95, price: 2600, tier: 2, geo: { recv: 32, barrel: 32, stock: 'collapsible', magT: 'stanag', hand: 32, rail: true } }),
    W('scarl', 'SCAR-L', 'ar', '5.56', { mode: 'auto', rpm: 625, mag: 30, spread: 2.0, recoil: 36, handling: 64, weight: 3.3, rel: 92, price: 2400, tier: 2, geo: { recv: 40, barrel: 30, stock: 'folding', magT: 'stanag', hand: 26, rail: true, bulky: true } }),
    W('mcx', 'MCX', 'ar', '5.56', { mode: 'auto', rpm: 850, mag: 30, spread: 2.1, recoil: 35, handling: 74, weight: 2.9, rel: 88, price: 3200, tier: 3, geo: { recv: 30, barrel: 28, stock: 'folding', magT: 'stanag', hand: 30, rail: true } }),
    W('ak105', 'AK-105', 'ar', '5.45', { mode: 'auto', rpm: 600, mag: 30, spread: 2.6, recoil: 48, handling: 62, weight: 3.2, rel: 97, price: 1100, tier: 1, geo: { recv: 36, barrel: 28, stock: 'folding', magT: 'curved', hand: 22, rail: false, ak: true } }),
    W('g36c', 'G36C', 'ar', '5.56', { mode: 'auto', rpm: 750, mag: 30, spread: 2.3, recoil: 34, handling: 72, weight: 2.8, rel: 88, price: 1900, tier: 2, geo: { recv: 36, barrel: 22, stock: 'folding', magT: 'translucent', hand: 22, rail: true, bulky: true } }),

    W('mp5', 'MP5', 'smg', '9mm', { mode: 'auto', rpm: 800, mag: 30, spread: 2.6, recoil: 22, handling: 82, weight: 2.5, rel: 88, price: 900, tier: 1, geo: { recv: 32, barrel: 12, stock: 'collapsible', magT: 'curvedsmg', hand: 18, rail: false } }),
    W('mp7', 'MP7', 'smg', '4.6', { mode: 'auto', rpm: 950, mag: 40, spread: 2.4, recoil: 20, handling: 88, weight: 1.9, rel: 90, price: 2200, tier: 2, geo: { recv: 26, barrel: 10, stock: 'pdw', magT: 'grip', hand: 12, rail: true } }),
    W('ump45', 'UMP45', 'smg', '.45', { mode: 'auto', rpm: 600, mag: 25, spread: 2.8, recoil: 28, handling: 80, weight: 2.3, rel: 90, price: 1200, tier: 1, geo: { recv: 34, barrel: 12, stock: 'folding', magT: 'box', hand: 16, rail: true, bulky: true } }),
    W('p90', 'P90', 'smg', '5.7', { mode: 'auto', rpm: 900, mag: 50, spread: 2.5, recoil: 18, handling: 84, weight: 2.6, rel: 86, price: 2600, tier: 3, geo: { recv: 46, barrel: 6, stock: 'bullpup', magT: 'top', hand: 0, rail: true, bulky: true } }),
    W('vector', 'Vector', 'smg', '.45', { mode: 'auto', rpm: 1200, mag: 25, spread: 2.7, recoil: 15, handling: 86, weight: 2.7, rel: 82, price: 3000, tier: 3, geo: { recv: 34, barrel: 10, stock: 'folding', magT: 'grip', hand: 14, rail: true, bulky: true } }),

    W('scarh', 'SCAR-H', 'br', '7.62', { mode: 'auto', rpm: 600, mag: 20, spread: 1.6, recoil: 60, handling: 52, weight: 3.9, rel: 92, price: 3600, tier: 2, geo: { recv: 42, barrel: 36, stock: 'folding', magT: 'box', hand: 28, rail: true, bulky: true } }),
    W('hk417', 'HK417', 'br', '7.62', { mode: 'auto', rpm: 600, mag: 20, spread: 1.5, recoil: 58, handling: 50, weight: 4.4, rel: 94, price: 4200, tier: 3, geo: { recv: 34, barrel: 40, stock: 'collapsible', magT: 'box', hand: 34, rail: true } }),
    W('sr25', 'SR-25', 'br', '7.62', { mode: 'semi', rpm: 300, mag: 20, spread: 1.2, recoil: 55, handling: 48, weight: 4.9, rel: 90, price: 4600, tier: 3, geo: { recv: 34, barrel: 46, stock: 'fixed', magT: 'box', hand: 36, rail: true } }),
    W('fal', 'FAL', 'br', '7.62', { mode: 'auto', rpm: 650, mag: 20, spread: 1.9, recoil: 66, handling: 46, weight: 4.3, rel: 86, price: 2200, tier: 2, geo: { recv: 40, barrel: 40, stock: 'fixed', magT: 'box', hand: 24, rail: false, wood: true } }),

    W('m4super', 'Benelli M4', 'sg', '12g', { mode: 'semi', rpm: 300, mag: 7, spread: 5.5, recoil: 70, handling: 60, weight: 3.8, rel: 90, price: 1900, tier: 2, geo: { recv: 36, barrel: 40, stock: 'collapsible', magT: 'tube', hand: 26, rail: true } }),
    W('r870', 'Remington 870', 'sg', '12g', { mode: 'pump', rpm: 70, mag: 6, spread: 6.5, recoil: 72, handling: 58, weight: 3.6, rel: 96, price: 500, tier: 1, geo: { recv: 34, barrel: 42, stock: 'fixed', magT: 'tube', hand: 22, rail: false, wood: true } }),
    W('m590', 'Mossberg 590', 'sg', '12g', { mode: 'pump', rpm: 75, mag: 8, spread: 6.2, recoil: 70, handling: 58, weight: 3.4, rel: 96, price: 700, tier: 1, geo: { recv: 34, barrel: 44, stock: 'fixed', magT: 'tube', hand: 22, rail: false } }),

    W('m24', 'M24', 'sr', '7.62', { mode: 'bolt', rpm: 45, mag: 5, spread: 0.4, recoil: 80, handling: 30, weight: 5.4, rel: 96, price: 3000, tier: 2, geo: { recv: 40, barrel: 54, stock: 'fixed', magT: 'internal', hand: 40, rail: false, wood: false, bolt: true } }),
    W('axmc', 'AXMC', 'sr', '.338', { mode: 'bolt', rpm: 40, mag: 5, spread: 0.3, recoil: 85, handling: 28, weight: 6.8, rel: 97, price: 7500, tier: 4, geo: { recv: 44, barrel: 56, stock: 'chassis', magT: 'box', hand: 40, rail: true, bolt: true } }),
    W('m110', 'M110', 'sr', '7.62', { mode: 'semi', rpm: 200, mag: 20, spread: 0.7, recoil: 60, handling: 38, weight: 5.3, rel: 90, price: 5200, tier: 3, geo: { recv: 34, barrel: 48, stock: 'fixed', magT: 'box', hand: 38, rail: true } }),

    W('g17', 'Glock 17', 'hg', '9mm', { mode: 'semi', rpm: 400, mag: 17, spread: 3.2, recoil: 25, handling: 90, weight: 0.9, rel: 92, price: 500, tier: 1, geo: { recv: 22, barrel: 0, stock: 'none', magT: 'pistol', hand: 0, pistol: true } }),
    W('g19', 'Glock 19', 'hg', '9mm', { mode: 'semi', rpm: 400, mag: 15, spread: 3.3, recoil: 24, handling: 92, weight: 0.85, rel: 92, price: 550, tier: 1, geo: { recv: 20, barrel: 0, stock: 'none', magT: 'pistol', hand: 0, pistol: true } }),
    W('p320', 'SIG P320', 'hg', '9mm', { mode: 'semi', rpm: 400, mag: 17, spread: 3.0, recoil: 24, handling: 90, weight: 0.95, rel: 93, price: 650, tier: 1, geo: { recv: 22, barrel: 0, stock: 'none', magT: 'pistol', hand: 0, pistol: true } }),
    W('usp45', 'USP45', 'hg', '.45', { mode: 'semi', rpm: 360, mag: 12, spread: 2.9, recoil: 30, handling: 86, weight: 1.0, rel: 95, price: 700, tier: 2, geo: { recv: 24, barrel: 0, stock: 'none', magT: 'pistol', hand: 0, pistol: true } }),
    W('fn57', 'Five-seveN', 'hg', '5.7', { mode: 'semi', rpm: 420, mag: 20, spread: 3.0, recoil: 20, handling: 90, weight: 0.75, rel: 90, price: 900, tier: 2, geo: { recv: 23, barrel: 0, stock: 'none', magT: 'pistol', hand: 0, pistol: true } }),
  ];
  const WEAPON = Object.fromEntries(WEAPONS.map(w => [w.id, w]));

  const LONG = ['ar', 'br', 'smg', 'sg', 'sr'];
  const RIFLE = ['ar', 'br'];
  const A = (id, slot, name, cats, price, tier, mods, desc) => ({ id, slot, name, cats, price, tier, mods, desc });
  const ATTACH = [
    // optics
    A('rds', 'optic', 'Micro Red Dot', ['ar', 'smg', 'br', 'sg'], 350, 1, { aimSpeed: 7, accuracy: 4, weight: 0.1 }, 'Fast, unmagnified reflex sight.'),
    A('holo', 'optic', 'Holographic Sight', ['ar', 'smg', 'br', 'sg'], 650, 1, { aimSpeed: 9, accuracy: 6, weight: 0.3, ergonomics: 2 }, 'Large window, fast target transitions.'),
    A('prism', 'optic', '4× Prism Combat Optic', ['ar', 'br'], 1200, 2, { accuracy: 14, aimSpeed: -6, weight: 0.4, range: 3 }, 'Fixed magnification. Extends engagement range.'),
    A('lpvo', 'optic', '1–6× Variable Optic', ['ar', 'br', 'sr'], 1800, 3, { accuracy: 17, aimSpeed: -3, weight: 0.6, range: 4 }, 'Close-quarters at 1×, precision at 6×.'),
    A('scope', 'optic', '5–25× Precision Scope', ['sr', 'br'], 2200, 2, { accuracy: 26, aimSpeed: -14, handling: -6, weight: 0.9, range: 8 }, 'Long-range magnified optic. Slow up close.'),
    A('rmr', 'optic', 'Pistol Reflex Sight', ['hg'], 400, 1, { aimSpeed: 8, accuracy: 6, weight: 0.05 }, 'Slide-mounted red dot.'),
    // muzzle
    A('brake', 'muzzle', 'Muzzle Brake', ['ar', 'br', 'sr'], 300, 1, { recoil: -12, visibility: 15, suppression: -15 }, 'Vents gas to reduce recoil. Loud.'),
    A('comp', 'muzzle', 'Compensator', ['ar', 'br', 'smg', 'hg'], 250, 1, { recoil: -8, accuracy: 2, visibility: 6 }, 'Reduces muzzle climb.'),
    A('supp', 'muzzle', 'Rifle Suppressor', ['ar', 'br', 'sr'], 1100, 2, { suppression: 70, visibility: -40, weight: 0.5, handling: -6, recoil: -4 }, 'Greatly reduces report and flash.'),
    A('psupp', 'muzzle', 'Pistol-Caliber Suppressor', ['smg', 'hg'], 600, 1, { suppression: 75, visibility: -40, weight: 0.25, handling: -4, recoil: -3 }, 'Quiet, flashless fire with pistol calibers.'),
    // underbarrel
    A('vfg', 'under', 'Vertical Foregrip', LONG.filter(c => c !== 'sg'), 120, 1, { recoil: -8, handling: -2, weight: 0.15 }, 'Strong recoil control.'),
    A('afg', 'under', 'Angled Foregrip', LONG.filter(c => c !== 'sg'), 150, 1, { recoil: -4, aimSpeed: 4, ergonomics: 5, weight: 0.1 }, 'Natural wrist angle, faster on target.'),
    A('stop', 'under', 'Hand Stop', LONG.filter(c => c !== 'sg'), 60, 1, { recoil: -3, ergonomics: 6, weight: 0.03 }, 'Index point for a consistent grip.'),
    A('bipod', 'under', 'Bipod', ['br', 'sr'], 350, 2, { accuracy: 7, handling: -10, weight: 0.4 }, 'Stable when braced. Clumsy indoors.'),
    // side rail
    A('light', 'side', 'Weapon Light', ['ar', 'smg', 'br', 'sg', 'hg'], 200, 1, { light: 1, visibility: 10, weight: 0.15 }, 'Illuminates dark rooms. Makes you visible.'),
    A('laser', 'side', 'Laser Module', ['ar', 'smg', 'br', 'sg', 'hg'], 350, 1, { accuracy: 5, aimSpeed: 5, visibility: 5, weight: 0.12 }, 'Visible aiming laser.'),
    A('combo', 'side', 'Light/Laser Combo', ['ar', 'smg', 'br', 'sg', 'hg'], 600, 2, { light: 1, accuracy: 5, aimSpeed: 5, visibility: 12, weight: 0.25 }, 'Both in one housing.'),
    // stock
    A('ctr', 'stock', 'Adjustable Stock', ['ar', 'smg', 'br', 'sg'], 180, 1, { ergonomics: 5, recoil: -4, weight: 0.05 }, 'Better cheek weld and shoulder fit.'),
    A('pdw', 'stock', 'PDW Stock', ['ar', 'smg'], 450, 2, { handling: 9, aimSpeed: 6, recoil: 6, weight: -0.2 }, 'Compact. Faster, kicks harder.'),
    A('precstock', 'stock', 'Precision Stock', ['ar', 'br', 'sr'], 600, 2, { accuracy: 6, recoil: -8, handling: -6, weight: 0.4 }, 'Adjustable comb and length of pull.'),
    // grip
    A('ergo', 'grip', 'Ergonomic Grip', ['ar', 'smg', 'br'], 90, 1, { ergonomics: 6, aimSpeed: 2 }, 'Comfortable texture and angle.'),
    A('steep', 'grip', 'Steep-Angle Grip', ['ar', 'smg', 'br'], 110, 1, { handling: 4, ergonomics: 3 }, 'Suits a compressed, square stance.'),
    A('rubber', 'grip', 'Rubberized Grip', ['ar', 'smg', 'br'], 70, 1, { recoil: -2, ergonomics: 2 }, 'Sure grip with wet gloves.'),
    // handguard
    A('mlok', 'hand', 'Free-Float Handguard', ['ar', 'br'], 400, 1, { accuracy: 4, weight: -0.1, ergonomics: 2 }, 'Barrel does not touch the handguard.'),
    A('shorthg', 'hand', 'Short Handguard', ['ar', 'br'], 300, 1, { handling: 6, accuracy: -2, weight: -0.15 }, 'Compact, CQB-friendly.'),
    A('longhg', 'hand', 'Extended Handguard', ['ar', 'br'], 380, 1, { recoil: -3, accuracy: 3, handling: -4, weight: 0.15 }, 'More real estate, better leverage.'),
    // barrel
    A('shortb', 'barrel', 'Short Barrel (CQB)', ['ar', 'br', 'sg'], 500, 1, { handling: 10, accuracy: -8, damage: -5, weight: -0.3, suppression: -5 }, 'Shorter and handier, lower velocity.'),
    A('longb', 'barrel', 'Long Barrel', ['ar', 'br', 'sr', 'sg'], 600, 2, { accuracy: 10, handling: -8, damage: 5, weight: 0.4 }, 'Higher velocity and precision.'),
    A('heavyb', 'barrel', 'Heavy Profile Barrel', ['ar', 'br', 'sr'], 700, 2, { reliability: 5, accuracy: 6, handling: -6, weight: 0.6 }, 'Holds zero under sustained fire.'),
    // magazine
    A('extmag', 'mag', 'Extended Magazine', ['ar', 'smg', 'br', 'hg', 'sg', 'sr'], 150, 1, { magMult: 0.5, handling: -4, reliability: -3, weight: 0.2 }, '+50% capacity.'),
    A('coupler', 'mag', 'Magazine Coupler', ['ar', 'smg', 'br'], 120, 1, { ergonomics: 10, weight: 0.3 }, 'Two mags clamped side-by-side. Faster reloads.'),
    A('drum', 'mag', 'Drum Magazine', ['ar', 'smg'], 900, 3, { magMult: 1.0, handling: -10, reliability: -8, weight: 0.9 }, 'Double capacity. Heavy and finicky.'),
    // sling
    A('sling1', 'sling', 'Single-Point Sling', LONG, 60, 1, { handling: 3, ergonomics: 2 }, 'Quick transitions.'),
    A('sling2', 'sling', 'Two-Point Sling', LONG, 70, 1, { ergonomics: 4, recoil: -1 }, 'Stable carry, adjustable.'),
    A('sling3', 'sling', 'Three-Point Sling', LONG, 80, 1, { ergonomics: 2, handling: 1 }, 'Old-school, secure.'),
  ];
  const ATT = Object.fromEntries(ATTACH.map(a => [a.id, a]));
  const SLOT_NAMES = { optic: 'Optic', muzzle: 'Muzzle', under: 'Underbarrel', side: 'Rail (side)', stock: 'Stock', grip: 'Pistol Grip', hand: 'Handguard', barrel: 'Barrel', mag: 'Magazine', sling: 'Sling' };

  const FINISHES = [
    { id: 'black', name: 'Black', c: '#1f2124' },
    { id: 'fde', name: 'Flat Dark Earth', c: '#9a8361' },
    { id: 'od', name: 'OD Green', c: '#4f5a3a' },
    { id: 'grey', name: 'Tactical Grey', c: '#62676c' },
    { id: 'multi', name: 'Multi-Terrain Camo', c: '#8a7b5b', camo: ['#5e5a3d', '#a8946b', '#3f3b2b', '#71704f', '#c3b28a'] },
    { id: 'urban', name: 'Urban Camo', c: '#6d7278', camo: ['#2c2f33', '#9ca1a6', '#4a4e53'] },
    { id: 'wood', name: 'Woodland Camo', c: '#5b6340', camo: ['#2c2a1c', '#7b6a43', '#3d4a2b'] },
    { id: 'custom', name: 'Custom Camo', c: '#555555', camo: [] },
  ];
  const FINISH = Object.fromEntries(FINISHES.map(f => [f.id, f]));

  const AMMO = [
    { id: 'fmj', name: 'Standard (FMJ)', dmg: 1.0, pen: 1.0, cost: 1.0, price: 0, desc: 'Balanced full metal jacket.' },
    { id: 'jhp', name: 'Hollow Point (JHP)', dmg: 1.25, pen: 0.6, cost: 1.4, price: 2500, desc: 'High stopping power, poor vs armor. Low over-penetration.' },
    { id: 'ap', name: 'Armor Piercing (AP)', dmg: 0.92, pen: 1.6, cost: 2.2, price: 6000, desc: 'Defeats body armor. Expensive.' },
    { id: 'sub', name: 'Subsonic', dmg: 0.9, pen: 0.85, cost: 1.5, price: 3000, desc: 'Much quieter through a suppressor.' },
  ];
  const AMMOD = Object.fromEntries(AMMO.map(a => [a.id, a]));

  const ARMOR = [
    { id: 'none', name: 'No Armor', prot: 0, speed: 1.04, stam: 1.0, price: 0 },
    { id: 'light', name: 'Lightweight Carrier', prot: 30, speed: 1.0, stam: 1.0, price: 0 },
    { id: 'standard', name: 'Standard Plates', prot: 55, speed: 0.93, stam: 1.15, price: 6000 },
    { id: 'heavy', name: 'Heavy Plates', prot: 75, speed: 0.85, stam: 1.35, price: 14000 },
    { id: 'full', name: 'Full Ballistic Kit', prot: 90, speed: 0.74, stam: 1.6, price: 26000 },
  ];
  const ARMORD = Object.fromEntries(ARMOR.map(a => [a.id, a]));
  const HELMETS = [
    { id: 'none', name: 'None', prot: 0, price: 0 },
    { id: 'bump', name: 'Bump Helmet', prot: 12, price: 0 },
    { id: 'ballistic', name: 'Ballistic Helmet', prot: 40, price: 4000 },
    { id: 'heavyhelm', name: 'Heavy Helmet + Visor', prot: 70, price: 11000, speed: 0.97 },
  ];
  const HELMD = Object.fromEntries(HELMETS.map(a => [a.id, a]));

  const ITEMS = [
    { id: 'flash', name: 'Flashbang', kind: 'util', price: 60, desc: 'Stuns everyone in line of sight.' },
    { id: 'smoke', name: 'Smoke Grenade', kind: 'util', price: 40, desc: 'Blocks line of sight for ~25s.' },
    { id: 'gas', name: 'Tear Gas', kind: 'util', price: 75, desc: 'Suspects choke, lose accuracy, surrender more.' },
    { id: 'stinger', name: 'Diversionary Device', kind: 'util', price: 50, desc: 'Loud distraction. Draws suspects toward it.' },
    { id: 'charge', name: 'Breaching Charge', kind: 'util', price: 150, desc: 'Destroys a door, stuns those behind it.' },
    { id: 'medkit', name: 'Medical Kit', kind: 'tool', price: 300, desc: 'Revive downed operators, treat wounds. 3 uses.' },
    { id: 'lockpick', name: 'Lockpick Kit', kind: 'tool', price: 400, desc: 'Silently open locked doors.' },
    { id: 'doorcam', name: 'Door Inspection Camera', kind: 'tool', price: 900, desc: 'See under a door before entering.' },
    { id: 'shield', name: 'Ballistic Shield', kind: 'tool', price: 2500, desc: 'Stops frontal fire. Pistol only. Slow.' },
    { id: 'ram', name: 'Battering Ram', kind: 'tool', price: 350, desc: 'Reliable manual breach of locked doors.' },
  ];
  const ITEM = Object.fromEntries(ITEMS.map(i => [i.id, i]));

  const VEHICLES = [
    { id: 'suv', name: 'Unmarked SUV', seats: 4, price: 0, len: 4.6, wid: 1.9, color: '#20252b', armored: false },
    { id: 'psuv', name: 'Police Interceptor SUV', seats: 5, price: 9000, len: 4.8, wid: 2.0, color: '#e7e9ec', armored: false },
    { id: 'van', name: 'Tactical Van', seats: 6, price: 18000, len: 5.6, wid: 2.1, color: '#2b2f35', armored: false },
    { id: 'truck', name: 'Armored Tactical Truck', seats: 8, price: 48000, len: 6.2, wid: 2.5, color: '#1d2125', armored: true },
    { id: 'transport', name: 'Personnel Transport', seats: 8, price: 30000, len: 6.8, wid: 2.4, color: '#3a3f2f', armored: false },
  ];
  const VEH = Object.fromEntries(VEHICLES.map(v => [v.id, v]));

  const UNIFORMS = [
    { id: 'swat', name: 'SWAT', shirt: '#1c2430', pants: '#1c2430', carrier: '#141a22', helmet: '#151a20', glove: '#111', patch: 'POLICE', balaclava: false },
    { id: 'fed', name: 'Federal Tactical', shirt: '#2a2d33', pants: '#6d6a55', carrier: '#2f3e31', helmet: '#2a2d30', glove: '#2a2a2a', patch: 'FEDERAL' },
    { id: 'sof', name: 'Special Operations', shirt: '#7d7356', pants: '#7d7356', carrier: '#4b5640', helmet: '#6b6650', glove: '#5e5440', patch: 'TF', camo: true },
    { id: 'ct', name: 'Counterterrorism', shirt: '#131417', pants: '#131417', carrier: '#0d0e10', helmet: '#0f1012', glove: '#0a0a0a', patch: 'CT', balaclava: true },
    { id: 'uc', name: 'Undercover', shirt: '#5a3b3b', pants: '#2d3a55', carrier: '#2a2a2a', helmet: null, glove: '#333', patch: '' },
    { id: 'plain', name: 'Plainclothes', shirt: '#365a7a', pants: '#a89a78', carrier: '#2b2b2b', helmet: null, glove: '#333', patch: 'POLICE' },
    { id: 'heavy', name: 'Heavy Armor', shirt: '#2c3036', pants: '#2c3036', carrier: '#1f2226', helmet: '#22262a', glove: '#111', patch: 'TF', balaclava: true },
    { id: 'recon', name: 'Light Recon', shirt: '#4c5538', pants: '#5a5a42', carrier: '#4a5236', helmet: '#4c5538', glove: '#3d3a2c', patch: 'RECON' },
  ];
  const UNIF = Object.fromEntries(UNIFORMS.map(u => [u.id, u]));

  const SKIN = ['#f2d0b5', '#e5b896', '#d29e78', '#b07a52', '#8d5a3a', '#6a4129', '#4a2c1c'];
  const HAIR_COLORS = ['#1a1310', '#3b2618', '#5e3b22', '#8a6038', '#c79a5a', '#d9c08a', '#6b6b6b', '#b8b8b8', '#7a2a1a'];
  const HAIR = ['Buzz', 'Short', 'Crew', 'Side Part', 'Slick Back', 'Bun', 'Ponytail', 'Long', 'Shaved', 'Curly', 'Braids'];
  const FACIAL = ['None', 'Stubble', 'Mustache', 'Goatee', 'Short Beard', 'Full Beard'];
  const FACES = ['Angular', 'Round', 'Square', 'Narrow', 'Broad', 'Weathered'];
  const BUILDS = ['Lean', 'Average', 'Athletic', 'Heavy'];
  const EYEPRO = ['None', 'Shooting Glasses', 'Goggles', 'Dark Lenses'];
  const HEADSET = ['None', 'Comms Earpiece', 'Active Headset'];
  const GEAR_EXTRAS = { gloves: ['Black', 'Coyote', 'Olive', 'Grey'], boots: ['Black', 'Coyote', 'Olive', 'Grey'], kneepads: ['None', 'Black', 'Coyote'], watch: ['None', 'Digital', 'Analog'] };
  const PATCHES = ['None', 'Flag', 'Unit Crest', 'Blood Type', 'Callsign', 'Skull', 'Medic Cross'];

  const STATS = [
    { id: 'acc', name: 'Accuracy', desc: 'Weapon handling and precision' },
    { id: 'awa', name: 'Awareness', desc: 'Detecting threats, reaction time' },
    { id: 'mob', name: 'Mobility', desc: 'Movement speed' },
    { id: 'end', name: 'Endurance', desc: 'Fatigue resistance, carrying heavy armor' },
    { id: 'dis', name: 'Discipline', desc: 'Following ROE, command response' },
    { id: 'med', name: 'Medical', desc: 'Treating injuries' },
    { id: 'tec', name: 'Technical', desc: 'Locks, charges and electronics' },
    { id: 'lea', name: 'Leadership', desc: 'Squad coordination and morale' },
  ];

  const PERSONALITIES = [
    { id: 'calm', name: 'Calm Under Pressure', desc: 'Stress builds slowly.', stress: 0.5, react: 0, acc: 0, speed: 1 },
    { id: 'aggr', name: 'Aggressive', desc: 'Fast and decisive, sometimes reckless.', stress: 0.9, react: -0.08, acc: -2, speed: 1.06, dis: -10 },
    { id: 'meth', name: 'Methodical', desc: 'Slow but very precise.', stress: 0.8, react: 0.03, acc: 6, speed: 0.95 },
    { id: 'exp', name: 'Experienced', desc: 'Seen it all before.', stress: 0.7, react: -0.06, acc: 3, speed: 1 },
    { id: 'inexp', name: 'Inexperienced', desc: 'Still learning the job.', stress: 1.3, react: 0.1, acc: -4, speed: 1 },
    { id: 'stressed', name: 'Easily Stressed', desc: 'Performance drops quickly under fire.', stress: 1.7, react: 0.02, acc: 0, speed: 1 },
    { id: 'leader', name: 'Natural Leader', desc: 'Squad morale and coordination boost.', stress: 0.8, react: 0, acc: 0, speed: 1, aura: true },
  ];
  const PERS = Object.fromEntries(PERSONALITIES.map(p => [p.id, p]));

  const ROLES = [
    { id: 'leader', name: 'Team Leader', desc: 'Commands the squad. Leadership aura reduces squad stress.' },
    { id: 'point', name: 'Point', desc: 'Leads the stack. Faster threat reaction when first through a door.' },
    { id: 'assault', name: 'Assault', desc: 'Main combat personnel.' },
    { id: 'shield', name: 'Shield', desc: 'Carries the ballistic shield.' },
    { id: 'marksman', name: 'Marksman', desc: 'Overwatch. Extended vision when holding.' },
    { id: 'medic', name: 'Medic', desc: 'Treats injured personnel faster.' },
    { id: 'tech', name: 'Technical Specialist', desc: 'Locks, charges and electronics.' },
  ];
  const ROLE = Object.fromEntries(ROLES.map(r => [r.id, r]));

  const PERKS = [
    { id: 'quickdraw', name: 'Quick Draw', desc: 'Aim acquisition time -20%.' },
    { id: 'breacher', name: 'Breacher', desc: 'Breaching 40% faster, kicks rarely fail.' },
    { id: 'medic', name: 'Combat Medic', desc: 'Treatment twice as fast and more effective.' },
    { id: 'eagle', name: 'Eagle Eye', desc: '+3 tiles vision, spots hidden suspects at range.' },
    { id: 'steady', name: 'Steady Hands', desc: 'Recoil -25%.' },
    { id: 'iron', name: 'Iron Will', desc: 'Stress gain halved.' },
    { id: 'runner', name: 'Fleet Foot', desc: 'Movement speed +10%.' },
    { id: 'negotiator', name: 'Negotiator', desc: 'Suspects more likely to surrender (+25%).' },
    { id: 'locksmith', name: 'Locksmith', desc: 'Lockpicking twice as fast.' },
    { id: 'grenadier', name: 'Grenadier', desc: 'Longer throws, bigger flashbang radius.' },
    { id: 'tough', name: 'Tough as Nails', desc: 'Bleed-out time doubled, +15 max HP.' },
  ];
  const PERK = Object.fromEntries(PERKS.map(p => [p.id, p]));

  const NAMES_M = ['James', 'Marcus', 'Daniel', 'Ethan', 'Luis', 'Andre', 'Tyler', 'Kevin', 'Ryan', 'Omar', 'Victor', 'Sam', 'Nathan', 'Derek', 'Caleb', 'Mateo', 'Isaac', 'Jordan', 'Hector', 'Wes', 'Malik', 'Grant', 'Felix', 'Owen', 'Rafael', 'Trent', 'Cole', 'Dmitri', 'Kenji', 'Bo'];
  const NAMES_F = ['Sarah', 'Maya', 'Elena', 'Jess', 'Nadia', 'Rachel', 'Ava', 'Lena', 'Carmen', 'Tasha', 'Grace', 'Ivy', 'Kara', 'Rosa', 'Dana', 'Mei', 'Priya', 'Leah', 'Nina', 'Jo', 'Alexis', 'Sofia', 'Erin', 'Vera', 'Quinn'];
  const SURNAMES = ['Walker', 'Reyes', 'Novak', 'Brennan', 'Okafor', 'Castillo', 'Hughes', 'Kowalski', 'Tran', 'Mercer', 'Vasquez', 'Holt', 'Barnes', 'Lindqvist', 'Park', 'Doyle', 'Ramos', 'Fischer', 'Grady', 'Sato', 'Murphy', 'Adeyemi', 'Cruz', 'Volkov', 'Hayes', 'Ortega', 'Kane', 'Bishop', 'Shaw', 'Ibarra', 'Whitlock', 'Nash', 'Duarte', 'Keller', 'Rowe'];
  const CALLSIGNS = ['Hawk', 'Ghost', 'Rook', 'Viper', 'Tank', 'Doc', 'Sparks', 'Echo', 'Saint', 'Wolf', 'Brick', 'Jinx', 'Nomad', 'Reaper', 'Mav', 'Kodiak', 'Dutch', 'Fox', 'Blade', 'Flint', 'Sarge', 'Lucky', 'Hex', 'Atlas', 'Moose', 'Ranger', 'Patch', 'Juno', 'Cypher', 'Bear', 'Tex', 'Pike', 'Stitch', 'Ace', 'Shadow'];
  const HOMETOWNS = ['Cedar Falls', 'Port Halden', 'Redwater', 'Marlow City', 'Fort Ashby', 'Lakeview', 'Grayson', 'North Bend', 'Silver Creek', 'Harbor Point'];
  const BACKGROUNDS = ['former Army infantry', 'ex-Marine', 'twelve years municipal SWAT', 'former state trooper', 'ex-Navy corpsman', 'former border patrol tactical unit', 'former military police', 'ex-Air Force security forces', 'former county deputy', 'former private security contractor', 'former fire department paramedic', 'former military combat engineer'];

  const SUSPECT_FIRST = ['Tony', 'Rico', 'Dale', 'Vince', 'Manny', 'Lenny', 'Sergei', 'Dre', 'Cody', 'Hank', 'Lou', 'Mick', 'Tre', 'Boris', 'Javi', 'Nico', 'Rusty', 'Earl', 'Gus', 'Yuri', 'Tasha', 'Lana', 'Roxy', 'Kat'];
  const SUSPECT_NICK = ['Spider', 'Snake', 'Two-Tone', 'Big Lou', 'Smiley', 'Chopper', 'Ice', 'Moth', 'Junior', 'Tank', 'Duke', 'Ghost', 'Lucky', 'Razor', 'Kingfish', 'Boots'];

  // Organizations (fictional)
  const ORGS = [
    { id: 'ldn', name: 'Eastside Distribution Network', short: 'EDN', tier: 1, color: '#c8694b', desc: 'Small-time street-level operation moving product through stash houses and storage units on the east side of Marlow City.', boss: 'Darnell "Kingfish" Pryor' },
    { id: 'bwl', name: 'Blackwell Logistics', short: 'BWL', tier: 2, color: '#4b8fc8', desc: 'A trucking and freight company acting as the transportation backbone for regional trafficking. Warehouses, depots and an industrial compound.', boss: 'Gordon Blackwell' },
    { id: 'syn', name: 'The Syndicate', short: 'SYN', tier: 3, color: '#a45bc8', desc: 'A disciplined organized crime family with legitimate fronts, armed security and financial operations spanning the state.', boss: 'Victor Vane' },
    { id: 'mer', name: 'The Meridian Network', short: 'MER', tier: 4, color: '#c8b44b', desc: 'International trafficking network with cells across multiple countries. Well-funded, well-armed, and extremely dangerous.', boss: '"The Architect"' },
  ];
  const ORG = Object.fromEntries(ORGS.map(o => [o.id, o]));

  // Mission type templates for operations/sandbox
  const MTYPES = {
    warrant: { name: 'Residential Warrant', map: 'house', obj: ['neutralize', 'evidence'], enemies: [3, 5], hostages: [0, 0], civilians: [1, 3], evidence: [2, 3], contraband: [0, 1], brief: 'A wanted suspect is believed to be inside a suburban home with associates. Locate the suspect, recover evidence, and protect any occupants.' },
    hostage: { name: 'Hostage Rescue', map: 'house', obj: ['rescue', 'neutralize'], enemies: [4, 6], hostages: [2, 3], civilians: [0, 0], evidence: [1, 2], contraband: [0, 0], brief: 'An armed group has barricaded itself inside a residence with hostages. Expect hostage-takers to execute captives if alerted for long.' },
    druglab: { name: 'Drug Laboratory Raid', map: 'house2', obj: ['neutralize', 'contraband'], enemies: [4, 6], hostages: [0, 0], civilians: [0, 2], evidence: [2, 3], contraband: [2, 4], brief: 'A residential property is being used to cook and package narcotics. Secure the lab, seize the product and arrest the cooks.' },
    warehouse: { name: 'Warehouse Raid', map: 'warehouse', obj: ['neutralize', 'evidence', 'contraband'], enemies: [6, 9], hostages: [0, 1], civilians: [0, 2], evidence: [2, 3], contraband: [2, 4], brief: 'A suspected distribution warehouse. Secure the property, locate contraband and recover shipping documents.' },
    compound: { name: 'Industrial Compound', map: 'compound', obj: ['neutralize', 'evidence'], enemies: [8, 12], hostages: [0, 1], civilians: [0, 1], evidence: [3, 4], contraband: [1, 2], brief: 'A heavily guarded industrial facility linked to organized crime. Multiple structures. Locate leadership and records.' },
    bank: { name: 'Bank Robbery Response', map: 'bank', obj: ['rescue', 'neutralize', 'evidence'], enemies: [4, 6], hostages: [3, 5], civilians: [0, 0], evidence: [1, 2], contraband: [0, 0], brief: 'Armed robbers are holding employees and customers inside a bank branch. Rescue civilians, secure the building and recover stolen property.' },
    airport: { name: 'Airport Cargo Interdiction', map: 'hangar', obj: ['neutralize', 'contraband', 'evidence'], enemies: [7, 10], hostages: [0, 0], civilians: [1, 3], evidence: [2, 3], contraband: [3, 4], brief: 'An illicit shipment is moving through an airport cargo facility. Identify the shipment, locate suspects, secure the area.' },
    hotel: { name: 'Hotel Hostage Situation', map: 'hotel', obj: ['rescue', 'neutralize'], enemies: [6, 9], hostages: [2, 4], civilians: [2, 4], evidence: [1, 2], contraband: [0, 0], brief: 'Armed suspects have taken hostages across multiple floors of a hotel. Clear floor by floor.' },
    bunker: { name: 'Underground Bunker Raid', map: 'bunker', obj: ['neutralize', 'evidence'], enemies: [7, 10], hostages: [0, 1], civilians: [0, 0], evidence: [3, 4], contraband: [1, 2], brief: 'A hidden underground facility beneath a forest cabin. Limited intelligence. Expect tight corridors.' },
    shipping: { name: 'Shipping Container Inspection', map: 'shipyard', obj: ['neutralize', 'contraband'], enemies: [6, 9], hostages: [0, 2], civilians: [0, 1], evidence: [2, 2], contraband: [2, 4], brief: 'Containers at the port are being used to move contraband. Clear the yard and the dock office.' },
    rural: { name: 'Rural Compound Assault', map: 'rural', obj: ['neutralize', 'evidence'], enemies: [6, 9], hostages: [0, 1], civilians: [0, 2], evidence: [2, 3], contraband: [1, 2], brief: 'A remote farm used as a hideout. Farmhouse, barn and outbuildings. Approach under darkness.' },
    prison: { name: 'Detention Riot Intervention', map: 'prison', obj: ['rescue', 'neutralize'], enemies: [7, 10], hostages: [2, 3], civilians: [0, 0], evidence: [0, 1], contraband: [1, 2], brief: 'Inmates have seized a detention block and are holding correctional officers. Restore order and rescue the officers.' },
    store: { name: 'Convenience Store Robbery', map: 'store', obj: ['rescue', 'neutralize'], enemies: [2, 3], hostages: [1, 3], civilians: [0, 1], evidence: [1, 1], contraband: [0, 0], brief: 'An armed robbery in progress at a corner store. Small space, civilians present.' },
    mansion: { name: 'Mansion Raid', map: 'mansion', obj: ['neutralize', 'capture', 'evidence'], enemies: [8, 11], hostages: [0, 1], civilians: [1, 3], evidence: [3, 4], contraband: [1, 2], hvt: true, brief: 'A high-value target is residing in a guarded mansion. Capture alive if possible.' },
  };

  // Campaign: 4 orgs x 4 missions
  const CM = (id, org, idx, name, type, o) => ({ id, org, idx, name, type, ...o });
  const CAMPAIGN = [
    CM('c1_1', 'ldn', 0, 'Corner Stash', 'warrant', { seed: 1101, enemies: 3, civilians: 2, evidence: 2, contraband: 1, reward: 9000, lieutenant: true, intel: 'A low-level dealer named in a traffic stop is storing product at his mother\'s house. Expect 2–4 associates, mostly handguns.' }),
    CM('c1_2', 'ldn', 1, 'Stash House on Pine', 'druglab', { seed: 1202, enemies: 5, civilians: 1, evidence: 3, contraband: 3, reward: 12000, floors: 2, intel: 'Ledger from Corner Stash points to a two-storey stash house on Pine Street, where product is cut and packaged.' }),
    CM('c1_3', 'ldn', 2, 'Riverside Storage', 'warehouse', { seed: 1303, enemies: 6, hostages: 1, evidence: 3, contraband: 3, reward: 15000, small: true, intel: 'The network rents a riverside storage warehouse. A courier who wanted out is being held there.' }),
    CM('c1_4', 'ldn', 3, 'Kingfish', 'hostage', { seed: 1404, enemies: 7, hostages: 2, evidence: 3, reward: 22000, hvt: true, floors: 2, takedown: true, intelReq: 4, intel: 'Darnell "Kingfish" Pryor is holed up in his duplex with his crew and two hostages. Take him alive.' }),

    CM('c2_1', 'bwl', 0, 'Bay 9', 'warehouse', { seed: 2101, enemies: 8, evidence: 3, contraband: 4, reward: 18000, intel: 'Kingfish\'s phone links his supply to Blackwell Logistics freight. Bay 9 at their north terminal.' }),
    CM('c2_2', 'bwl', 1, 'Vehicle Depot', 'compound', { seed: 2202, enemies: 9, evidence: 3, contraband: 2, reward: 21000, lieutenant: true, intel: 'Trucks are fitted with hidden compartments at a fenced depot outside town.' }),
    CM('c2_3', 'bwl', 2, 'Night Shift', 'shipping', { seed: 2303, enemies: 9, hostages: 2, evidence: 2, contraband: 3, reward: 24000, night: true, intel: 'Two port inspectors who refused bribes have gone missing near Blackwell\'s container stack.' }),
    CM('c2_4', 'bwl', 3, 'Blackwell Compound', 'compound', { seed: 2404, enemies: 12, hostages: 1, evidence: 4, contraband: 2, reward: 34000, hvt: true, takedown: true, intelReq: 5, intel: 'Gordon Blackwell is at the company\'s industrial compound under heavy security.' }),

    CM('c3_1', 'syn', 0, 'First National', 'bank', { seed: 3101, enemies: 5, hostages: 4, evidence: 2, reward: 26000, intel: 'A robbery crew tied to Syndicate money-laundering has taken a bank branch. This is a hit on a rival vault.' }),
    CM('c3_2', 'syn', 1, 'The Grand Halden', 'hotel', { seed: 3202, enemies: 8, hostages: 3, civilians: 3, evidence: 2, reward: 30000, lieutenant: true, intel: 'Syndicate enforcers have seized floors of a hotel to silence a witness.' }),
    CM('c3_3', 'syn', 2, 'Safehouse', 'warrant', { seed: 3303, enemies: 6, evidence: 4, contraband: 2, reward: 28000, night: true, floors: 2, rain: true, intel: 'A Syndicate bookkeeper uses a suburban safehouse. Raid at night, in the rain.' }),
    CM('c3_4', 'syn', 3, 'Vane Estate', 'mansion', { seed: 3404, enemies: 11, civilians: 2, evidence: 4, contraband: 2, reward: 45000, hvt: true, takedown: true, intelReq: 6, intel: 'Victor Vane at his estate, with professional security. Capture him.' }),

    CM('c4_1', 'mer', 0, 'Cargo Terminal 4', 'airport', { seed: 4101, enemies: 10, civilians: 2, evidence: 3, contraband: 4, reward: 40000, intel: 'Vane\'s ledgers point to Meridian shipments through the airport cargo terminal.' }),
    CM('c4_2', 'mer', 1, 'Dry Creek Farm', 'rural', { seed: 4202, enemies: 9, hostages: 1, evidence: 3, contraband: 2, reward: 42000, night: true, lieutenant: true, intel: 'A Meridian cell is using a remote farm as a waypoint.' }),
    CM('c4_3', 'mer', 2, 'The Hole', 'bunker', { seed: 4303, enemies: 10, hostages: 1, evidence: 4, contraband: 2, reward: 48000, intel: 'Beneath a hunting cabin lies a bunker used as Meridian\'s regional command post.' }),
    CM('c4_4', 'mer', 3, 'Architect', 'compound', { seed: 4404, enemies: 14, hostages: 2, evidence: 4, contraband: 3, reward: 80000, hvt: true, takedown: true, intelReq: 6, night: true, intel: 'The Architect is personally overseeing a handoff at a fortified facility. Final operation.' }),
  ];

  const EVIDENCE_TYPES = [
    { id: 'docs', name: 'Documents', icon: 'docs', intel: 1 },
    { id: 'laptop', name: 'Laptop', icon: 'laptop', intel: 2 },
    { id: 'phone', name: 'Burner Phone', icon: 'phone', intel: 1 },
    { id: 'ledger', name: 'Ledger', icon: 'docs', intel: 2 },
    { id: 'drive', name: 'Hard Drive', icon: 'laptop', intel: 2 },
    { id: 'manifest', name: 'Shipping Manifest', icon: 'docs', intel: 1 },
  ];
  const CONTRABAND_TYPES = [
    { id: 'drugs', name: 'Narcotics', icon: 'drugs', value: 2500 },
    { id: 'cash', name: 'Cash Bundle', icon: 'cash', value: 4000 },
    { id: 'guns', name: 'Weapons Cache', icon: 'guns', value: 3000 },
  ];

  const DIFFICULTY = {
    casual: { name: 'Casual', dmgTaken: 0.6, enemyAcc: 0.7, enemyReact: 1.3, showEnemies: true, cost: 0.8, pay: 0.9, death: 'never', execTime: 18 },
    standard: { name: 'Standard', dmgTaken: 1.0, enemyAcc: 1.0, enemyReact: 1.0, showEnemies: false, cost: 1.0, pay: 1.0, death: 'never', execTime: 12 },
    veteran: { name: 'Veteran', dmgTaken: 1.15, enemyAcc: 1.15, enemyReact: 0.88, showEnemies: false, cost: 1.15, pay: 1.15, death: 'chance', execTime: 9 },
    milsim: { name: 'Milsim', dmgTaken: 1.35, enemyAcc: 1.25, enemyReact: 0.8, showEnemies: false, cost: 1.3, pay: 1.3, death: 'always', execTime: 7 },
  };

  // Enemy archetypes
  const ENEMY = {
    associate: { name: 'Associate', acc: 0.32, react: 0.95, morale: 35, aggr: 0.3, radio: false, surrender: 0.6, weapons: ['g17', 'g17', 'r870', 'none'], armor: 0 },
    guard: { name: 'Armed Guard', acc: 0.48, react: 0.75, morale: 60, aggr: 0.55, radio: true, surrender: 0.33, weapons: ['m590', 'ak105', 'mp5', 'g19'], armor: 0 },
    crim: { name: 'Organized Criminal', acc: 0.62, react: 0.6, morale: 75, aggr: 0.75, radio: true, surrender: 0.18, weapons: ['m4a1', 'ak105', 'ump45', 'g36c'], armor: 30 },
    pro: { name: 'Professional Security', acc: 0.72, react: 0.5, morale: 85, aggr: 0.8, radio: true, surrender: 0.1, weapons: ['hk416', 'mcx', 'scarl', 'mp7'], armor: 55 },
    hvt: { name: 'High-Value Target', acc: 0.4, react: 0.8, morale: 30, aggr: 0.2, radio: true, surrender: 0.45, weapons: ['g19', 'usp45'], armor: 0 },
    lieutenant: { name: 'Lieutenant', acc: 0.55, react: 0.65, morale: 65, aggr: 0.6, radio: true, surrender: 0.28, weapons: ['m4a1', 'ump45'], armor: 30 },
    inmate: { name: 'Rioting Inmate', acc: 0.3, react: 0.9, morale: 45, aggr: 0.65, radio: false, surrender: 0.5, weapons: ['g17', 'r870', 'none'], armor: 0 },
  };

  const VOICE = {
    contact: ['Contact!', 'Contact front!', 'Tango!', 'Got one!'],
    comply: ['Police! Show me your hands!', 'Police! Drop the weapon!', 'Hands! Let me see your hands!', 'Get down! Get on the ground!'],
    down: ['Suspect down.', 'Tango down.', 'He\'s down.'],
    clear: ['Room clear.', 'Clear!', 'Clear.'],
    reload: ['Reloading!', 'Changing mags!', 'Cover me, reloading!'],
    hurt: ['I\'m hit!', 'Taking fire!', 'Hit! I\'m hit!'],
    mandown: ['Man down!', 'Operator down!', 'We got a man down!'],
    flash: ['Flashbang out!', 'Bang out!', 'Flash!'],
    breach: ['Breaching!', 'Breach, breach!', 'Door\'s coming down!'],
    arrest: ['Suspect in custody.', 'Cuffed.', 'Restrained.'],
    secure: ['Civilian secured.', 'Hostage secure.', 'You\'re safe, stay down.'],
    evidence: ['Evidence bagged.', 'Got something here.', 'Securing evidence.'],
    locked: ['Door\'s locked.', 'It\'s locked.'],
    moving: ['Moving.', 'Copy, moving.', 'On it.', 'Roger.'],
    jam: ['Malfunction!', 'Jammed!'],
    allclear: ['Area secure. All threats neutralized.'],
  };

  return { CAL, CATS, WEAPONS, WEAPON, ATTACH, ATT, SLOT_NAMES, FINISHES, FINISH, AMMO, AMMOD, ARMOR, ARMORD, HELMETS, HELMD, ITEMS, ITEM, VEHICLES, VEH, UNIFORMS, UNIF, SKIN, HAIR, HAIR_COLORS, FACIAL, FACES, BUILDS, EYEPRO, HEADSET, GEAR_EXTRAS, PATCHES, STATS, PERSONALITIES, PERS, ROLES, ROLE, PERKS, PERK, NAMES_M, NAMES_F, SURNAMES, CALLSIGNS, HOMETOWNS, BACKGROUNDS, SUSPECT_FIRST, SUSPECT_NICK, ORGS, ORG, MTYPES, CAMPAIGN, EVIDENCE_TYPES, CONTRABAND_TYPES, DIFFICULTY, ENEMY, VOICE };
})();
