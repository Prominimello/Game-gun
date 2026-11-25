# BREACHPOINT: TASK FORCE

A top-down tactical squad shooter with a management layer. You command a tactical intervention unit: customize operators, build weapons in a detailed gunsmith, plan breaches with GO codes, clear multi-floor buildings, rescue hostages, arrest suspects, collect evidence, and take down four criminal organizations.

**No install, no build, no external assets.** Every visual is drawn in code on canvas, and every sound (gunshots, breaches, flashbangs, radio, ambience, music) is synthesized live with WebAudio. Voice callouts use the browser's speech synthesis.

## Run it

Open `index.html` in a modern desktop browser (Chrome, Edge or Firefox). That's it. It also works when served from any static web server:

```bash
python3 -m http.server 8000
```

Then browse to http://localhost:8000. The game saves to browser `localStorage`.

## Android / mobile

- **APK:** every push builds a signed APK with GitHub Actions (`.github/workflows/android-apk.yml`) and publishes it on the repository's **Releases** page ("Breachpoint for Android"). Download the `.apk` on an Android 7.0+ device, open it and allow installing from that source.
- **Phone browser:** the web version is touch-enabled and can be added to the home screen (web app manifest included) when served over HTTPS.
- **Touch controls:** tap an operator to select (toggle *Multi* to select several, *All* for the squad); tap ground, a door, a suspect or an item to order; long-press (or toggle *+Waypoint*) to queue waypoints; drag to pan, pinch to zoom, twist to rotate; *Orders* opens the command panel. The Android back button closes menus and pauses missions.
- **Native wrapper:** `android/` is a minimal Java WebView app (fullscreen landscape, native text-to-speech for callouts, auto-pause when backgrounded). Build locally with `cd android && ./gradlew assembleDebug` (needs the Android SDK).
- **Signing:** with no secrets configured, CI generates a key once and keeps it in the Actions cache. For a permanent key, add repository secrets `BP_KEYSTORE_BASE64`, `BP_KEYSTORE_PASSWORD`, `BP_KEY_ALIAS`, `BP_KEY_PASSWORD`.

## Game loop

1. **HQ → Command Center**: pick a campaign operation, a daily operation, the weekly challenge, or build a **Sandbox** mission.
2. **Briefing**: study the blueprint, pick your squad (your vehicle's seats limit squad size), adjust loadouts, and choose entry points.
3. **Planning phase**: time is frozen. Give orders, queue waypoints, set up door breaches and GO codes.
4. **Execute**: real time with pause. Re-plan whenever you like.
5. **Debrief**: S/A/B/C/Failed rating across 9 categories, pay, XP, injuries, evidence, and a mission timeline.
6. **Replay**: watch the whole operation, or any operator's **body cam** view.
7. Back at HQ: heal, train, buy gear, tune weapons, recruit, and expand.

## Controls (mission)

| Input | Action |
|---|---|
| LMB / drag | Select operator / box-select |
| 1–8 (twice = center) | Select operator |
| ` or Ctrl+A | Select all |
| RMB | Move (Shift+RMB adds a waypoint; RMB-drag sets facing) |
| RMB on a door | Breach menu: Open & Clear, Flashbang & Clear, Kick, Breaching Charge, Shotgun, Ram, Pick Lock, Door Camera, Stack Up, Close |
| RMB on a suspect / civilian / item / teammate | Arrest / Secure / Collect evidence / Treat or revive |
| Space | Pause (you can give orders while paused) |
| Enter | Execute (planning) / **GO** (release GO-code waits) |
| B | Queue a "wait for GO" point |
| G / T / Y / U | Throw flashbang / smoke / tear gas / diversion, then click the target |
| H / F / R / V / C / O | Hold / Follow first selected / Regroup / Take cover / Crouch / Face cursor |
| Z / N / X | Cycle pace (slow, walk, run) / Formation (stack, wedge) / Cancel orders |
| WASD, arrows, MMB-drag, wheel | Pan / zoom |
| Q / E | Rotate camera |
| Tab, PgUp/PgDn | Change floor |
| L | Lock camera to selected operator |
| [ / ] | Time scale |
| F1 or ? | Controls overlay |

## Features

- **Campaign:** 4 fictional organizations (Eastside Distribution Network, Blackwell Logistics, The Syndicate, The Meridian Network), 16 operations. Evidence builds intel on each org. Low intel makes takedowns harder, and capturing a boss alive weakens the next org.
- **Operations mode:** 14 mission types: warrant, hostage rescue, drug lab, warehouse, industrial compound, bank robbery, airport cargo, hotel, underground bunker, shipping yard, rural compound, prison riot, convenience store, mansion. Layouts and placements are procedurally generated.
- **Sandbox:** choose the map, enemy count and gear tier, hostages, civilians, evidence, HVT, time of day, weather and difficulty.
- **Difficulty:** Casual / Standard / Veteran / Milsim, plus **Ironman** (permadeath).
- **Operators:** 8 attributes, 7 personalities, 7 roles, levels with skill points, 11 unlockable abilities, morale, fatigue, injuries with recovery time, bonds between teammates, and a memorial wall for the fallen.
- **Appearance:** gender, faces, skin tones, hairstyles, facial hair, builds, 8 uniform presets plus custom colors, helmet, balaclava, eye protection, headsets, gloves, boots, kneepads, watch, patches and unit ID.
- **Gunsmith:** 26 firearms in 6 categories and 35 attachments across 10 slots (optic, muzzle, underbarrel, rail, stock, grip, handguard, barrel, magazine, sling). Each changes weight, handling, recoil, accuracy, ergonomics, aim speed, reliability, suppression and visibility. Also finishes and camo (including custom), engraving, serial numbers, and wear and dirt tied to condition and maintenance. A rotatable, zoomable inspection view.
- **Equipment:** flashbangs, smoke, tear gas, diversionary devices, breaching charges, medkits, lockpicks, door cameras, ballistic shields and rams. 5 armor levels, 4 helmets, and 4 ammo types (FMJ / JHP / AP / subsonic).
- **AI:** suspects hear gunfire, doors and running footsteps. They take time to notice you, use radios, investigate, hold angles, rush, hide and ambush, surrender, fake-surrender and flee. They also escape the perimeter, and hostage guards execute hostages when alerted. Civilians panic, cower or flee.
- **Combat:** hitscan ballistics with armor and helmet penetration, hit locations, cover, glass, wall-blocked shots, and friendly-fire checks. Also suppression stress, malfunctions, reloads and weapon switching, plus downed operators who bleed out unless stabilized or revived.
- **Presentation:** pseudo-3D extruded walls, fog of war from real vision cones, night ops with weapon lights, and rain. Also muzzle flashes, tracers, shell casings, impacts, blood, scorch decals, smoke clouds, screen shake and flash-blindness.
- **HQ:** Command Center, Barracks, Armory, Training Facility (stat courses + live kill-house exercise), Medical Bay, Garage (vehicles, paint, markings, lights, accessories) and Evidence Room. Training and Medical can be upgraded.

## Code layout

| File | Purpose |
|---|---|
| `js/data.js` | Weapons, attachments, gear, operators, orgs, campaign, mission types |
| `js/state.js` | Profile, saving, weapon stat derivation, progression |
| `js/mapgen.js` | Procedural multi-floor building and site generation |
| `js/sim.js` | Mission simulation: orders, pathfinding, AI, ballistics, grenades, scoring, recording |
| `js/render.js` | Map layers, extruded walls, fog of war, particles, decals |
| `js/art.js` | Procedural portraits, weapon renders, sprites, vehicles, icons |
| `js/audio.js` | Synthesized SFX, ambience, music, voice |
| `js/mission_ui.js` | Mission input, camera, HUD and loop |
| `js/hq.js`, `js/brief.js`, `js/main.js` | HQ screens, briefing and debrief, title and replay |

## Scope notes

This is a complete, playable game, but a few items from the full design are simplified:

- The camera is a rotatable, zoomable top-down 2.5D view (extruded walls), not a full 3D engine.
- Floors connect by stairs only (no elevators). Room light switches are not implemented; night missions rely on weapon lights.
- Voice lines use the browser's text-to-speech, not recorded actors.
