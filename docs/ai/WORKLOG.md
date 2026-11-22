# Work log

The running record of the work: what was done in which wave, choices made by the assistant that wait for the owner (checkpoint C0, D94), and open questions. Newest entries first. Decisions themselves live in `docs/ai/DECISIONS.md`; deviations in `docs/ai/DEVIATIONS.md`.

## Open questions for the owner

1. **The M2 feel check** (ROADMAP M2's last "Done when" clause, D33, D103) and **the owner hours for M2** (`docs/ai/estimate/owner-hours.csv`).
2. **The provisional M3 decisions D108-D118** and **the provisional M4a decisions D124-D133** (`docs/ai/DECISIONS.md`, "Provisional"): the lead proceeds with them (D123, the owner's go-ahead of 2026-10-09); confirm or change any of them.
3. **The M3 acceptance walk** (ROADMAP M3 "Done when"): all of MetaStation on the real server with a second client; the Windows bench once on the PC (`dist/README-windows.txt` of I1d). M4 does not wait for it (D123).

## Choices waiting for confirmation at C0

None. The owner confirmed all ten at C0 (D95, 2026-10-07; `docs/ai/C0-report.md` section 4).

## 2026-10-10 — M4b slices 1 and 2: the air moves; pipes, vents, alarms, firelocks, canisters

The owner paused M4a and started M4b, the atmosphere (D134). The lead works alone, with no subagents.

**Slice 1 (owner-tested 2026-10-09, "everything seems to work").** tg's LINDA runs on Meta (c5488df): active turfs, excited groups, superconduction, space as the vacuum. The air acts on a human: pressure damage, body temperature, alerts and the space wind. Zero-gravity drift works too (2070241). New dev commands: `breach` and `patch`.

**Slice 2** (one commit per part, each followed by its own types-regeneration commit):
- pipenets (b4ca53b): `/datum/pipeline` and SSair's pipenet setup and rebuilds, plus Meta's live devices (vents, scrubbers, pumps, gates, valves, filters, mixers, injectors, connector ports, heat-exchange pipes). Meta builds 122 pipenets, and the distro reaches one atmosphere within a minute. DV31: a device that is still inert is no pipe node.
- firelocks and fire alarms (f992669): a hot or cold neighbour closes a firelock's whole row (tg reacts to temperature, not pressure). Meta has 390 live firelocks, and a breach next to one closes its row.
- air alarms (15ea79c): thresholds, danger levels, the area's atmos alarm and every mode. Meta has 198 alarms, none in danger once the air settles.
- canisters and portable machines (73aa2c1): gas canisters, the portable pumps and scrubbers, and connector ports.
- dev commands for the play test (the tgui windows come in M9): `valve WHO|X Y [Z]` toggles a canister's valve, and `airmode WHO MODE` sets the air alarm of WHO's area (apps.md 1.6a).

**Still inert:** the thermomachine (its RefreshParts is M5), the airlock pump, the atmos tank, the crystallizer and the tank compressor. Gas visuals, fire and plasma (the reactions and hotspots) come next.

**Suites at the slice-2 handoff:** Core 5412 passed (7 skipped), Server 1802 passed (4 skipped), 0 warnings; the client builds.

## 2026-10-09 — M3 integration: the real server is walkable; autonomous mode; M4a scoped

**The owner's first real-server walk** (2026-10-08 evening) found: the map empty (no walls, no floors), the character rolled back on every step, pipes, cables and lattices standing as billboards, and no way back after a client crash. **Fixed and merged** (main 3dadfcb, verified green on 6c9ff94: Core 5076 + 1, Server 1613 + 4, Tools.Maps 21, Analyzers 34, Debug and Release, 0 warnings; smoke OK; toy hash 0x51a3f3774a8ba8d8, identical twice):
- **R4:** the replicator built its cell tables at attach, before SSmapping loaded Meta: it now rebuilds on a size change and re-reads the map when joins open (a joiner gets 3,835 walls and 12,193 floors, was 0 and 65,025 bare turfs); Meta was weightless: tg's station gravity from the gravity generator (`gravitygenerator.dm`), corrections on a walk 97 -> 0 (2-3 on door edges); the client keeps its reconnect token per server and name (`user://reconnect-tokens.txt`).
- **R5 (D122):** tg's floor layers lie flat in tg's plane and layer order (6,124 of Meta's things), undertile in the fake feed, tables at half the eye (0.6 m), pod doors and shutters roll up into the ceiling; the third walk's "hatch" was the holopad (flat, D122 amended).
- **R6:** directional windows, windoors and border firelocks draw their full-tile counterpart's picture (tg's directional sprite is a 7-row top-down band: the slime pens were 0.44 m high).
- **I1a:** the generator types a var as a value when its kinds differ (Meta's boot runtimes 8 -> 3), posters pick a poster, the maintenance loot and spawners, the cleanable decals.
- **I1b:** tg's pipe node search and pictures (`setup_atmos_machinery`, D123: no gas), the stalls (1.2-3.6 s steps) were `locate(tag)` scanning every object for each weakref: a tag index; stall credit for the rate buckets (the owner's "crash" was a Flooding kick after a stall); text2num's exponent; the step profiler.
- **I1c:** a lost connection returns to the connect screen with the server's words; mobs and items lit by the light grid (they were black); `post_init_icon_state` in the fake feed; the Windows export script; Mac numbers on the real server with Meta: 249 FPS avg / 142 1 % low (one client), map load 5.5 s; light shots.
- **T2 (phases A and B):** 141 DM-behaviour tests of the map load and the structures (smoothing oracle over all 256 patterns, passage, spawners, undertile, decals); fixed D2 (turf weakrefs), D4, D5; reported B1-B3 and D3, D6 (`scratchpad/m3/requests.md`).

**The owner's decisions:** D122 (the under-floor layers), D123 (the pipes' pictures in M3, M4 without waiting for the acceptance walk, the Windows export templates downloaded: Windows only, 616 MB). **Process (2026-10-09):** autonomous mode, no lead context ceiling; 2 agents at once, a 3rd only with usage room.

**Running:** I1d (the join cost, two walking clients, the cameras' and canisters' pictures, the client's pipe states, a self-contained Windows zip, flaky wall-clock tests, `DmList.RemoveLast`) and T1 (doors, machinery, lights tester).

**M4a scoped** (`scratchpad/m4/scope-a.md`, plan `m4/plan-a.md`, provisional D124-D133): contracts C, then P1 click chain, P6 protocol v4, P2 hands and inventory, P7 client interaction, P3 storage, P5 ID cards, P4 tools, testers and I1; about 52k weighted lines. C starts when I1d merges.

## 2026-10-08 — M3 wave 1 merged: Meta loads on the real server; the owner's walks 1-3; paused for the owner's server test

**Merged into main** (a3f6447, verified green: Core 5059 + 1 slow skipped, Server 1514 + 4, Tools.Maps 21, Analyzers 34, Debug and Release, 0 warnings; smoke OK; toy hash 0xe7d751ff3c54212f, identical twice):
- **C phase B:** the tg proc skeletons of P2, P3 and P4's files (406 roots) on the generated classes, the first live-partial rows.
- **P2, maps and world load:** MetaStation loads through tg's own code (SSmapping's station path, `parsed_map`/`build_coordinate`, the preloader, ChangeTurf and ScrapeAway, z-levels and traits, landmarks): 65,025 turfs, 171 areas, 35,364 movables; two loads give the same digest and state hash; boot 9.6 s with +392 MB (Release). The server's `--map metastation`. DV26: late joiners use the Assistant start landmarks (no arrivals shuttle, D108). object-model 7.1: TURF-IDENTITY.
- **P3, machinery, doors, access, lights:** about 3,600 tg lines: machinery and SSmachines, lighting_atom, access, the door family with tg's timings (firelocks inert and open, D117), wires, the light fixture, every mapping helper (183 access helpers), place_on_top, the builtin range. On Meta: 511 airlocks, 143 windoors, 299 pod doors, 390 firelocks, 641 lights, none with a runtime.
- **P4, turfs and structures:** walls, floors with broken and burnt overlays, windows, grilles, tables, railings, flaps, icon smoothing (SSicon_smooth), the spawners, turf decals and the decal element, undertile (10,610 on Meta, 0 wrong), atom colours, the builtins orange and view, the inert init (`dm_inert_initialize`, InertSeam on `[DmInert]`).
- **The lead's shim fixes:** the world's atom order is BYOND's (mobs, other movables, areas, turfs; 46c0a44, tg's SSatoms relies on it), and `ref()` of a resource (0x27, d286630).
- **R2 and R3, the owner's walks 1-3 on the fake feed:** the root pixel offset applied once (wall mounts on walls), the fake type source's merge (preopen doors open), firelocks as 3D pieces, doors across their passage, joined windows with corner posts, one straight-run picture for windows and the walls' sides, items at half scale on their support, vents and scrubbers flat, bump-open doors on the fake feed, D120 (the eye at 1.2 m) and D121.

**The lead's first real run** (main a3f6447): `--map metastation` boots with 9 runtimes (5 preloader `id`, add_item_action, set_custom_materials, the monitored_area component) and opens joins at tick 20; a client joined: 21,767 entities, 0 corrections, a map load of 3.0 s. Seen: the floor at the spawn (41,131) looks untextured on the real server (to check in I1).

**Not merged:** T2 phase A (97 tests, three `Fix translation:` commits; branch m3/t2-tests at 0764ef1): its Meta assertions predate P3 and P4, so its phase B merges main first.

**The owner's process changes** (2026-10-08): at most 2 agents at once; the lead session compacts at about 400k; Sonnet for the mechanical part of porting; short agent sessions with handoff notes, narrow reading, one full suite run at the end (the token analysis: cost is calls times context).

**Next, after the owner's test and the compaction:**
1. The owner's walk-3 fixes, one client package: a hatch type still a billboard, lower tables, pod doors and shutters into the ceiling (D121).
2. T2 phase B, then T1 (doors).
3. I1, the integration: `requests.md`, the real-server walk, maintenance loot, posters, the generator's var typing, the Windows export (after the owner's OK to download the export templates).

## 2026-10-08 — M3 checkpoint: the types, the client world and light, protocol v3; paused for the owner's walk

The owner asked for a pause point (no new agents, a session compaction, a walk of the map). Main a98a245.

- **Merged into main:**
  - **The contracts' phase A.** Specs: `map-format.md`, `types-converter.md`, `light-grid.md`, `m3-ownership.md`. Also the protocol v3 types, the face API, the inert seam, the map loading seams, the client seams, dev access, DV22-DV24, and rules 10.2 (var blocks come from the generator from M3 on).
  - **P1, the types.**
    - `tools/Ss3d.Tools.Types` with OpenDream `1362abc5` source-included (D110).
    - 46,051 generated classes, one per tg type (D111).
    - `types-index.tsv` (the translation tracker), `[DmInert]` and `live-base.txt` (D112), and a compile-time table for `type::var`.
    - Boot loops skip the generated-only types (DV25).
    - Boot 0.7 s became 1.0 s and the heap +30 MB became +54 MB, after fixing a first 20 s boot (a prototype and a compiled factory per type). The Release core suite went from 1 m 30 s to 2 m 10 s.
  - **P5, protocol v3.** Cell flags (faces, opacity, starlight), base lights, the `Light` field and turf type changes; the replicator v3; face blocking in motion (server and predictor bit-equal); bots v3 with a door-aware `tour`.
  - **P6, the world renderer.**
    - 16x16 chunks with Texture2DArray tiles, an atlas prop batch, D113 pieces, wall decals, door animation, the starfield, occluders, presets and `--bench`.
    - FakeFeed v3 serves converted Meta.
    - The owner's texture report is fixed: one whole sprite per face.
  - **P7, the light grid.** tg's corner falloff and cone, BYOND view LOS (from OpenDream's implementation), area base light, starlight, exact incremental updates, and the shadowed lights by preset; a dev flashlight.
  - **R1: walls, doors, windows and the ceiling as high as the tile (2 m, D119, the owner's decision).** It is one client setting, `--wall-height`.
  - **P2, steps 1-3:**
    - the area-membership index: boot of 255x255 with areas 243 s became 1.45 s, and 128x128 9.6 s became 0.76 s;
    - the map converter `tools/Ss3d.Tools.Maps` (Meta in 0.25 s; all 12 station maps convert and validate);
    - the engine map reader (Meta parsed in 60 ms).
- **Tests:** Core 4976, Server 1343 (+4 benches), Tools.Maps 21, Analyzers 34, green in Debug and Release; the Godot smoke passes. Toy-world seed-1 hash 0x242eb2a7580781bb (moved by P1: complement fields, generated config entries and languages).
- **Measured:** the client on FakeFeed v3 Meta (M1 Pro, 1080p, the light grid, 20 bots), average FPS low 396, medium 315 to 337 (after R1), high 296; the map builds in 0.5 s. A generated 255x255 MapGrid v3 is 161 KB.
- **The lead's choices:**
  - P2, P5, P6 and P7 started before their plan wave, because their inputs were ready.
  - P1's typesof stopgap (DV25).
  - The DmShim perf fixes in P1 (Activator factories, the compile-time table, the table registry); the core suite stays +45 % (the target was +25 %).
  - FakeFeed's path rules (tg's state after init) come before the generated classes' pre-init defaults.
- **Not merged, continues after the pause:**
  - the contracts' phase B (tg proc skeletons on the generated classes; branch `m3/contracts`);
  - P2 steps 4-6 (the translated map load, SSmapping's station level, `ChangeTurf`, landmarks, the server's `MAP`; branch `m3/p2-maps`);
  - P3 (machinery, doors, access, lights) and P4 (turfs, structures, smoothing, spawners, decals, the inert init);
  - the testers T1 and T2, and the integration I1 (Meta on the real server, the measurements, the Windows export, which needs Godot's export templates: a download, asked first).
- **Open items** (`scratchpad/m3/requests.md`):
  - 99 var-block mismatches to rule on;
  - five hand-written fields narrower than tg's values;
  - `text2num` ignores exponents;
  - the area-contents drain makes ticks of 133-273 ms after a whole-map area change;
  - doors polled by type for replication (no density hint);
  - the fake feed's bots draw as black capsules (icon state `human` is missing);
  - one Debug run discovered only 4082 of 4930 core tests.
- **How the owner walks the map now:** `apps.md` 2.10-2.11 and the chat note. The client runs Meta on its built-in fake server: the geometry, collision, light and props are real. Doors open by the dev command `door near open`, not by access. The real tg server loads the map after P2 steps 4-6, P3 and P4.

## 2026-10-08 — M2 merged: character, locomotion, basic medical indicators

- **Done:** all eight packages (P1 living core, P2 carbon/human/body/breath, P3 appearance and HUD state, P4 motion core and bridge, P5 protocol v2, P6 server/room/dev/bots, P7 icons and rendering, P8 client play), the testers T1 (medical) and T2 (bridge and appearance), the integration, and F1 (the jumpsuit and the visible name). Main 5511935.
- **What it does** (apps.md section 3): a continuous capsule player bridged to tg's `Move` (D105, DV15-DV17), tg's speeds (13.3 m/s run, 5.0 m/s walk), own-capsule prediction (D103), billboards composed from tg's sprites with GAGS jumpsuits (D104), tg's translated medical core (damage, Life, status effects, blood, 9 organs, breath with a fixed mix), crit and death at tg's timings, the HUD from tg's screen objects (D107), the shove, rest and get-up, CPR, the dev commands and the analyzer. Players join with tg's random name and the assistant's random-colour jumpsuit (a host stand-in for the job system, apps.md 1.4).
- **Tests:** Core 4922, Server 1144 (+4 opt-in benches), Analyzers 34, green in Debug and Release. Ported tg tests: `mob_damage` (carbon subset), `stuns`, `oxyloss_suffocation`, `full_heal`, `status_effect_validity`, `spawn_humans`, `disarm`, `combat_help`. The testers' mutation scores: 41/41 (T1) and 34/34 (T2); together with the integration they found and fixed 13 translation and bridge defects (among them the untranslated `attack_hand` that kept every shove from working, and a capsule step long enough to pass through another player).
- **Measured** (apps.md 3.5): the 15-minute soak (50 mixed bots and 2 Godot windows) tick p99 5.7 ms, 0 runtimes, 0 desyncs, 7.4 KB/s per player; 150 bots for 10 minutes p99 10.9 ms, 20.3 KB/s per player; prediction exact walking alone at 0-200 ms; a recorded session replays to the same hash. Toy-world seed-1 hash 0x7975f279dbfbbf0f.
- **Speed:** about 86.8k weighted lines (`tools/measure_velocity.py --since 2026-10-07T14:12`: 37.7k translated DM lines, 9.0k new logic, 8.3k client, 23.6k tests) in about 10 hours of wall time, with at most 4 agents at once from about 16:15Z (the owner's cap) and two usage-limit stops.
- **Deviations written:** DV15-DV21 (the bridge, the shove on a shared turf with the 8-sector direction, `IsObscured`, the client-side GAGS composition, the leg not split into two masked images, the sleeping effect's lit turf).
- **The lead's choices at the merges (D94 style):**
  - P3 merged alone, before P1 and P2.
  - The server's input jitter buffer holds 4 frames (measured).
  - DV17's shove direction uses the bridge's 8-sector rule.
  - An unchanged input frame is not recorded as a Motion command (a host rule, apps.md 1.8).
  - `add_vomit_floor`, the self-check report and the punch are deferred leaves.
  - Female uniforms are drawn uncut (tg's crop is a server-side icon blend; presentation under D104, no DV).
  - SSwardrobe is a stub (outfit items are made at join, not stocked).
- **Known and open:**
  - Prediction is corrected on most ticks of contact with a moving player (shove bots 37 %), because other capsules are not predicted (D103; M10).
  - Humans are bald (hair is M9); only players and dev-spawned humans get names (tg's `randomize_human_normie` stays deferred).
  - P3's missing GlobalProcs mirrors and inline macros are a mechanical cleanup.
  - Four older deferred sites lack not-translated rows (atoms_movable.dm:211, :213, objs.dm:76, client_procs.dm:939), for the M3 contracts.
  - The owner's feel check is open (question 1).
- **Next:** M3 (map port, placeholders, doors and light). The scout's report is done (OpenDream compiles tg with 0 errors in about 50 s; MetaStation measured; a Godot probe at Meta scale runs 248 FPS with tg-like light and 4 shadowed lights, but 99 FPS if every prop type has its own material). Provisional decisions D108-D118. Wave 0 starts now: P1, the types converter and generated classes for all 46,064 tg types, and C, the contracts.

## 2026-10-07 — M2 started: the scout, the owner's M2 decisions, the contracts step

- **The owner's M2 decisions:**
  - D104: first-approximation art. Billboards from tg's sprites for mobs and items, tg's textures on simple 3D geometry for the rest, lying mobs on a floor quad, a 3D character revisited at C2.
  - D105: the capsule-to-`Move` bridge. Solid standing players without swap or walk-push; a shove also on a shared tile, over tg's full tile.
  - D106: tg's speed, with a scale knob for the feel check.
  - D107: the HUD from tg's own screen objects.
  - M1 owner hours recorded (less than 1 h).
- **M2 scout** (read-only):
  - the medical closure is about 16k DM lines with tg's appearance code: less than the roadmap's 23k but wider (9 organs, species and dna init, status effects, `/obj/item`, the HUD);
  - floats in the motion controller are bit-identical between server and client on one machine (no FMA contraction on .NET 10.0.12 arm64);
  - running at tg's speed is 13.3 m/s at 2 m tiles;
  - critical and dead states take minutes at tg's timings (tox 150: death after about 150 s), so tests fast-forward and demos use dev commands.
- **The lead's choices (D94 style, for the owner to see):**
  - all 9 human organs inserted, with thin bodies where M2 does not need them, and species narrowed to the human;
  - tg's click chain (`ClickOn`/`UnarmedAttack`) translated, so the shove keeps tg's gates and cooldowns;
  - dev commands from a client only when the server enables them (default on for a localhost bind);
  - tg's icons generated from `../tgstation` into a git-ignored folder, not committed until the infrastructure pass (D88);
  - the motion controller and the client predictor in a new engine-free project `Ss3d.Core.Motion`, shared by the server, the client and the bots;
  - protocol v2 (positions, yaw, posture, interned BYOND-style appearances, a private block per session).
- **Plan:**
  - a contracts step (running);
  - eight packages in parallel:
    - P1 living core, P2 carbon/human/body/breath, P3 appearance and HUD state, P4 motion core and bridge: Opus;
    - P5 protocol v2 and replication, P6 server host, room, dev commands and bots, P7 icons and rendering, P8 client play: Sonnet;
  - testers T1 (medical) and T2 (locomotion and appearance) on Sonnet;
  - an Opus integration step, then the owner's feel check.

## 2026-10-07 — M1 merged: the basic server, the Godot client, bots, the soak run

- **Packages merged** (after the contracts step M1.0): P2 protocol and transport, P5 bots and the soak script, P3 the server host, P1 the tg client layer, P4 replication, P6 the Godot client, then the integration step and T1 (the independent tests of P1).
  - **P1, the tg client layer** (about 1,480 DM lines): `client/New`, `Del`, `Destroy`, `persistent_client.dm`, `mob/Login`/`Logout`, the movement and flood parts of `keyDown`/`keyUp`, `keyLoop`, `client/Move`, `SSinput` and `SSverb_manager` as live subsystems, `world/Del`; BYOND's client engine parts (attach by key, `mob.key` moves the client, deleting a client disconnects the player). 51 UI, HUD, preference, database and admin leaves are `Dm.Deferred` rows. DV14: tg's default movement hotkeys stand in while the preferences are deferred.
  - **P2:** a hand-written codec in `Ss3d.Protocol` (caps checked before allocation, seeded fuzz), `ReplicaWorld` with interpolation, LiteNetLib 2.1.4 and a deterministic in-memory loopback in `Ss3d.Net`; `docs/ai/specs/protocol.md`.
  - **P3:** `ServerHost`: config (`config/server.txt`), the 48x48 test room, sessions with a server-issued token, validation and rate limits (D40), the command phase in the next tick, the D98 pace, the console (spawn, teleport, list, kick, status, hash, quit) on tg's own primitives, record and replay, allocation-free metrics, clean shutdown; `docs/ai/specs/apps.md` section 1.
  - **P4:** replication R3 (D99): hint-selected candidates, shadows of the `NetField` whitelist, a sweep, per-field stamps; `docs/ai/specs/replication.md`. **Measured: about 6 µs per tick at 16.5k atoms with 150 movers** against D99's 2 ms threshold, so R3 stays and the owner is not asked again. Only `name` has no engine hint (the sweep catches it within 11 ticks); making `name` a hinted engine property is an M2 contracts item.
  - **P5:** `tools/Ss3d.Tools.Bots` (manual-mode LiteNetLib, wander, idle, churn and five abuse modes) and `tools/soak/run.sh` (D100).
  - **P6:** the Godot client: connect and game scenes, first-person camera with mouse look (D101), view-relative WASD mapped to tile directions, map and capsules interpolated from the replica, a debug HUD with input-to-motion latency, automation arguments; `docs/ai/specs/apps.md` section 2.
  - **Integration:** the replicator wired into the host; the D98 glide (tg's `set_glide_size(DELAY_TO_GLIDE_SIZE(1.5))` on the avatar); `Master.UpdateTickRate`, `log_admin`, `FLOOR`, `isAI` translated; teleport works in a strict world (`level_trait`'s look-up deferred to M3); `SSgarbage.HardDelete` runs BYOND's `client/Del` inside `qdel`; 18 end-to-end tests (10 bots whose replicas equal the server's world at every tick, reconnect, abuse, console, record and replay, real sockets); translation rules v1.3 settle P1's rule gaps; `docs/ai/specs/apps.md` section 3.
  - **T1:** 263 DM-behaviour tests of the client layer, one translation defect found and fixed (a qdeleted client stayed on its mob until later in the tick), 64 of 65 mutations killed (the survivor is equivalent).
- **Tests:** Core 4,048, Server 661 (3 bench tests run on demand), Analyzers 34; Debug and Release, 0 warnings. The toy world's seed-1 hash is now `0xd045d48cfe847de7` (live SSinput and SSverb_manager, the key properties); deterministic.
- **Speed:** about 41,500 weighted lines in M1 so far (3,454 translated C# = 2,791 DM, 12,289 new logic, 3,095 client, 20,266 tests), in about 4 hours of wall time; the roadmap planned about 30,000 weighted lines for M1.
- **Soak run (D100)**, 50 bots and 2 Godot windows, 30 minutes (it was running when the owner set 15 minutes as the limit, D102): 36,219 ticks at 20 TPS, tick total p99 2.5 ms against the 25 ms target (max 135 ms at a gen2 collection, 2 overruns), 0 runtime errors, 0 bot desyncs, 2.2 KB/s down per player; both Godot windows saw all 51 others move. Seven gen2 collections, 603 ms of GC pause in all.
- **150-bot run**, 10 minutes: tick total p99 3.2 ms, max 43 ms, 0 overruns, 0 runtimes, 0 desyncs, 4.8 KB/s down per player.
- **M1's "Done when" is met** (`docs/ai/specs/apps.md` 3.2). The owner played it (2026-10-07): "already good, I see the resemblance to the original"; asked whether movement stays on tiles (answered: no, M2's capsule per D32 and PLAN 2.7, with the bridge to tg's `Move` at tile borders) and chose own-capsule prediction in M2 (D103).
- **Seen in the soak:** the Godot client's worst drawn step over 30 minutes was 90-116 m/s (a fraction of a tile in one frame, probably the render clock catching up after the 135 ms tick); a hitch, not a habit, to look at with M2's locomotion.
- **Owner decision D102:** long test runs at most 15 minutes; the soak script's default is now 15.
- **Open, for later milestones:**
  - M2 contracts: `/atom/var/name` as a hinted engine property (P4).
  - M3: boot time grows superlinearly with room size (`change_area`'s contents removal); `level_trait` and `SSmapping` z-level data.
  - Optional: encode `LastMoveTick` relative to the delta's tick (P2, about a third of a moving entity's record); a quiet-machine rerun of P4's bench.
  - Shim gaps: a public "is reserved id" check for engine-made objects, `world.address` in the facade; `slots.py` still writes `new` on `/client` New and Del (rules v1.3 say not to).
  - Untested because tg does not use them in M1: setting `mob.key` or `client.mob` to null on a connected player.

## 2026-10-07 — M1 started: the analyzer, the scout, the owner's M1 decisions

- **The waiting-model analyzer (D97) is merged.** `tools/Ss3d.Analyzers` reports SS3D0001-SS3D0005 as errors (a dropped task, a stored task and a blocking read in translated code, a foreign awaitable, an await in a `[SignalHandler]`/`[ShouldNotSleep]` method, own or inherited); `docs/ai/specs/waiting-model.md` 5.6. It found no violation in `src/` or `tools/`; 33 deliberate cases in the core tests carry commented suppressions, one accidental dropped task in a test was fixed. 34 analyzer tests; `dotnet test AnomalyStation.sln` runs both suites (CLAUDE.md updated). Cost: about 0.05 s of a clean Tg compile.
- **M1 scout** (read-only, measured on the owner's M1 Pro, Release):
  - the booted tg world with 52 plain `/mob` stepping through tg's `step()`: tick p99 0.26 ms, 26 KB allocated per tick, no gen2 GC; with 152 mobs p99 0.44 ms. The 25 ms target is far away;
  - LiteNetLib 2.1.4 (MIT) restores and runs on .NET 10 and inside the Godot 4.7.2 client;
  - `World.ComputeStateHash` costs 189 ms at 1.5k objects and 4.5 s at 66k: the server's per-second hash must be off by default;
  - **boot scaling:** a 128x128 room boots in 9.7 s, 255x255 in 245 s, because tg's `change_area` removes from a large area `contents` list (`turf.dm:261`). M1 stays at about 48x48; an indexed removal in the engine's contents lists is an M3 prerequisite (the map loader calls `change_area` too).
- **Owner decisions:** D98 (M1 avatar paced at tg's run delay), D99 (hybrid replication, measurements have the final say), D100 (the AI runs the 30-minute soak), D101 (first-person camera already in M1).
- **The lead's choices (D94 style, for the owner to see):**
  - the server runs the booted tg world (real `world/New`, Master, `RUNLEVEL_GAME`) in a host-built room of about 48x48; `--empty` keeps the kernel-only world;
  - a network session maps to a translated tg `/client` plus BYOND engine parts; the avatar is a plain `/mob` keyed by the host before `client/New` (a stand-in for the M10 lobby);
  - movement is tg's tile steps end to end (keyDown/keyUp, `SSinput`, `keyLoop`, `client/Move`, `Move`); the input message is ready for M2 and every delta carries the last applied input sequence, for later prediction;
  - reconnect by name plus a server-issued token; key pairs come with accounts in M10;
  - LiteNetLib with a hand-written codec in a new `Ss3d.Protocol` project; commands apply in the command phase of the next tick;
  - the tests of the protocol, transport, server, replication and bots go into a new `tests/Ss3d.Server.Tests`.
- **Plan:** a contracts step (running), then six packages in parallel: P1 the tg client layer (translation), P2 protocol and transport, P3 the server host, P4 replication with the measurements of D99, P5 bots and the soak script, P6 the Godot client; T1 tests P1 independently. Models: Opus for the contracts, P1, P3 and P4; Sonnet for P2, P5, P6 and T1.

## 2026-10-07 — checkpoint C0 decided

- The owner confirmed M0's ten technical choices (D95), chose spot-check review of about 5% of non-test lines, and set OpenDream for the start of M3 (D96) and the Roslyn analyzer for the start of M1 (D97).
- **M1 is on hold** until the owner has looked at the C0 report and the toy world runner. The owner says when M1 starts.
- **The hash depended on the build** (found by the owner running the toy world in Debug, 2026-10-07): Debug and Release gave different hashes for seed 1, each stable on its own.
  - Cause: the Master's stack canary (`stack_end_detector.dm`), held only by a local of the running `Master.Loop`, was collected in the optimized build. C# keeps an async local across an `await` only if it is read later; BYOND keeps proc vars until the proc returns.
  - Fixes:
    - `GC.KeepAlive(canary)` in the loop, as tg means it;
    - the core assemblies (Kernel, DmShim, Tg) compile optimized in every configuration, so the live set and the hash no longer depend on the build (object-model.md section 8, DV7 extended);
    - a regression test, `ToyWorld/LiveSetTests`.
  - Seed 1 now hashes 0x990262cc003e5804 in both builds.

## 2026-10-07 — wave 5 merged: M0 done, checkpoint C0 report

- **Toy world runner.** `src/Core/Tg/Infrastructure/ToyScenario.cs` holds the scripted M0 scenario: tg's toy room under the real Master, one mob and one obj, steps, a bump into a wall, a `do_after` that completes, and one cancelled by a step. `tools/Ss3d.Tools.ToyWorld` prints it as ASCII frames and a signal log; `--twice` compares the state hashes of two worlds of one seed.
- **M0 acceptance tests.** `tests/Ss3d.Core.Tests/M0Acceptance/` holds one class per ROADMAP "Done when" clause, every test on a booted world: 38 tests. Every clause of M0 is met.
- **Wave-end cleanup:**
  - stale "wave 4" notes now name their milestones;
  - "M0.5", which is not a milestone, became M2 for move loops and M4 for zero-gravity drift;
  - the not-translated and ownership tables were checked.
- **Tests:** 3,776 green in Debug and Release, none skipped. No translation defect was found in wave 5.
- **C0 report.** `docs/ai/C0-report.md` records:
  - the measured V: at least 120,779 weighted lines in the first week, about 1.6 calendar days of work;
  - the owner's time: about 2.5 h;
  - the owner's review choice: about 5% spot checks;
  - the estimate recomputed with two measured scenarios in `docs/ai/estimate/roadmap_estimate.py`: the first demo in about 4.1 months and the core version in about 6.1 months, every milestone limited by the owner's time;
  - the choices to confirm.
- **Open:**
  - `World.ComputeStateHash` forces two full garbage collections and costs 25-30 ms per call. It matters for rollback hashing (M10), not for M0.
  - The test suite takes about 45 s.
- **Next:** the owner's decision at C0, then M1 (the basic server).

## 2026-10-07 — wave 4 merged: the Move chain, the turf, area, obj and mob bases, do_after

- **Contracts first.** A contracts step (W4.0) declared what the four packages share:
  - the var blocks of `atoms_movable.dm`, `turf.dm`, `areas.dm`, `objs.dm` and `mob_defines.dm`, and the signatures the packages call;
  - stub subsystems (`SSspatial_grid`, `SSvis_overlays`, `SSsound_tokens`, `SSair`);
  - `step`, a minimal `/image`, the wave-4 ownership table (`docs/ai/specs/wave4-ownership.md`) and deviations DV12 and DV13.
- **Four packages translated in parallel**, about 3,070 DM code lines in about 45-60 minutes of active time each (the usage limit paused all four for 77 minutes):
  - **support** (about 540): `connect_loc`, `movement_detector`, the movespeed and actionspeed modifiers, the mob lists, string lists, multiz and direction helpers;
  - **movable** (about 830): `atoms_movable.dm` with both `Move` definitions, `doMove`, `forceMove`, `Moved` and the active-movement bookkeeping, `Bump`, `Cross`, pulling, z-moves, plus `adjacent.dm`;
  - **turf and area** (about 980): `turf.dm`, open and closed turfs, `space_EXPENSIVE.dm`, `areas.dm`, `SSarea_contents` (the first live resumable subsystem of the port), the appearance and overlay procs;
  - **mob, obj and timed actions** (about 720): `mob.dm`, `objs.dm`, `timed_actions.dm` with `do_after`, `key_name` and the cogbar and progress bar seam.
- **Tests:** 3,706 green in Debug and Release, none skipped (wave 3 ended at 2,633).
  - Three package test-writers and one integration test-writer, all on Sonnet, added about 940 behaviour tests and found no translation defect.
  - Their own mutation checks (about 80 one-line mutants of the translations) were all killed or shown equivalent.
  - The integration tests run move-chain.md's goldens T1-T12, tg's `movement_order_sanity`, `move_pulled` and `connect_loc` tests, and the `do_after` scenarios on real tg turfs and areas in a toy room. The room builder `ToyRoom` (`src/Core/Tg/Infrastructure/ToyRoom.cs`) is reused by wave 5.
- **Lead decisions:**
  - **BYOND's builtin-type defaults, then tg's, per type.** The engine part keeps the one constructor and calls `dm_tg_vars()`, which the mirror file implements (rules 10.2, "Builtin types"). This settles wave 3's open question.
  - **Resource literals are values.** `'icons/x.dmi'` is `Dm.resource("icons/x.dmi")`, a `DmResource`: it is `isfile`, renders as its path, equals another resource of that path and never equals text (rules 3.7).
  - **DV12:** emissive blockers are not created (lighting is not ported).
  - **DV13:** the server draws no busy indicator for timed actions (the 3D client does).
  - **BYOND's builtin `Bump`** is `Bump__d0` on the engine part (rules 7.5).
  - **Defaults apply to an explicit null argument** (BYOND reference). tg's 49 `do_after(..., cog_icon = null)` calls therefore still get the default cog, a tg quirk pinned by tests.
  - **`client/Move` waits for M1**; tests move mobs with `step`.
  - **`area.contents` stays turfs-only** until a reader appears.
- **Owner's rules for this and later waves:**
  - Opus only for planning, code porting, architecture and information search; Sonnet for everything else.
  - The 400k context ceiling is suspended until the next usage-limit stop.
  - Wave 5 starts right after wave 4.
- **Measured speed** (`tools/measure_velocity.py`, week 2026-W41, three days of work): 114,945 weighted accepted lines. Of these, 30,837 are translated tg code in DM lines (C#/DM ratio 1.22), 22,428 new logic and 61,384 tests. The C0 report puts this against ROADMAP section 6.
- **Still open:**
  - **Shim:** `/regex` (blocks `strings()` and `sanitize_filepath`), `range`/`orange`/`view`, `mutable_appearance`, the `appearance` var, `ref()` of text, text indexing `T[i]`, md5 and the hub vars.
    - The reverse index of `vis_contents` (`vis_locs` is always empty).
    - `image()` shifts a C# named `dir` when its second argument is text.
  - **Engine:** a turf replaced by `new /turf/T(old)` leaves listeners keyed by the dead turf (turf identity comes with `ChangeTurf`, M3). The builtin datum types still do not derive from tg's `datum`.
  - **Not translated yet:**
    - the multi-tile branches of `Move` and `doMove` (tg passes a direction where `Entered` expects an atom; a RULE-GAP);
    - z-moves between two real floors (openspace not translated, `zFall` is M5);
    - the client, mind, HUD and living vars that mob code reads (M1, M2).
  - **BYOND oracle list (additions):**
    - whether an `/atom` override reaches a builtin subtype's own default (a bare turf's layer);
    - whether `step` passes a null `Move` result on;
    - `image()` with text as its icon;
    - resource-versus-text equality;
    - `null == 0` in `start_pulling`;
    - `Move` without a direction;
    - `get_dir` across z-levels;
    - an explicit null argument against a default;
    - the poll order of a `do_after` started by a player command.
  - About 30 skeleton headers still say "wave 3 or later".
- Next: wave 5 (the toy world runner, the M0 acceptance tests, the C0 report).

## 2026-10-06 — wave 3 merged: the world boots and the Master Controller fires

- Six packages translated in parallel worktrees, four of them checked by independent test-writers:
  - **mc**: `master.dm` (minus Recover, the UI and profiling), `subsystem.dm`, `controller.dm`, the MC/tick/stoplag macros and `stack_end_detector.dm`; about 1,030 DM lines.
  - **timers**: `timer.dm` and the processing subsystems; about 460 DM lines.
  - **boot**: `genesis_call`, `world.dm`, most of `game/world.dm`, `globals.dm`, the configuration with all 231 entries; about 1,240 DM lines.
  - **logging**: logging, log categories and `to_chat` over the presentation seam, plus rust-g's file, log and clock functions; about 790 DM lines.
  - **atoms**: SSatoms, atom initialization and `_atom.dm`; about 550 DM lines.
  - **engine**: the atom engine parts, a per-world map grid and the map builtins (`locate`, `block`, `get_step`, `get_dir`, `get_dist`); about 1,350 lines of hand-written C#.
- Speed: about 4,070 DM lines in about 25 minutes of active time per translator. The subscription's usage limit stopped all six agents for 2 h 36 min shortly after the start; they resumed with their context intact.
- Tests: 2,633 green in Debug and Release, none skipped.
  - The test-writers added about 320 behaviour tests and found one translation defect: a runtime in `CreateAtoms` must end only `CreateAtoms` (rules 9.3).
  - The boot integration tests now run: boot to first fire (Genesis, `world/New`, `Master.Initialize` 20 ticks later, the ticker subsystems from tick 21), tg's `subsystem_init` check after boot, and identical state hashes for two boots of one seed.
- Lead fixes at the merges:
  - **GLOB before the datum exists.** `GLOBAL_*` vars are `var/global` in DM, and tg writes and reads them before `GLOB` is created (Genesis, `Master/New`'s subsystem sort). The port now has a pre-GLOB storage object; `GLOB__var` is the DM var (rules 10.6, C0 choice 9).
  - **Contents edits by DM code.** `DmList` now tells its engine owner of every write (`IDmListOwner`). `A.contents += new X` puts X in A even when X's `New` reads the engine (C0 choice 10).
  - **Rule 6.6.** A loop var declared before a numeric `for` takes only in-range values (OpenDream), which `timer.dm` relies on.
  - Isolation boundaries 9.2 #8 and #9 (a subsystem's `Initialize` in `init_subsystem`, each `LateInitialize` in SSatoms) and the 9.3 sites `Master.Loop` and `CreateAtoms`.
  - The binder skips every earlier definition `name__d<n>`.
  - `world.realtime` and `timeofday` tick in whole deciseconds; `world.timezone` is 0.
  - Appendix A rows for the decided slots.
  - The shared test helper `TgTestGenesis` (logger, SSchat and the default configuration for worlds that do not boot).
- Still open:
  - `ref()` of text (a TIMER_UNIQUE timer with a global-proc callback fails; no M0 caller).
  - Text indexing `T[i]`, `/regex`, md5 and the hub world vars in the shim.
  - The builtin datum types do not derive from tg's `datum`.
  - Where tg's own var overrides of BYOND builtin types go: one C# constructor per class; a wave 4 decision.
  - `area.contents` lists only turfs.
  - A sleeping `Initialize` is reported as a runtime, not as a bad init (`BAD_INIT_SLEPT` exists only under `UNIT_TESTS`).
  - `START_PROCESSING` goes through by-name var access (fine for M0).
  - The server boots tg only with `--boot`; the default is still the empty world.
- Next: wave 4 (the Move chain, the turf, area, obj and mob bases, timed actions and `do_after`), then wave 5 (the toy world, the M0 acceptance scenario, the C0 report).

## 2026-10-06 — rules v1.2 and wave 3 contracts merged

- **Rules v1.2.** The lead decided every pilot finding (43 rule decisions, 12 shim and code items); the decisions are written into `docs/ai/translation-rules.md` v1.2 and the specs, so `docs/ai/specs/m0-pilot-findings.md` is deleted. Main points: labelled loops as `goto L__break`; proc references are text (the slot generator now types `/proc/x` defaults as `string`, 9 slots changed); `ASSERT` expands inline; condition-shaped macros (`HAS_TRAIT`) are `bool`; `stack_trace` reports the caller's DM location; TgState fields of translated statics are `internal`; `/world` procs are `DmWorld` extension methods reachable by name; partial mirrors are allowed; any exception is a DM runtime; `[DmPath]` goes on the earliest declaring file (a skeleton mirror when that file is untranslated; macro-expanded declarations do not count); rule 15 covers ported tg unit tests.
- **Shim.** `ref()`/`locate()` of lists, alists, typepaths and `world` (OpenDream's ref kinds; text and numbers stay a runtime); text builtins take any value; the binder reaches `/world` procs; rust-g's `url_encode` in C#.
- **Deviation DV8.** `_component.dm:112` inserts the component itself, not the type key (a tg bug that left a typepath in the parent's component list).
- **Cleanup.** The pilot code follows v1.2 (30 of 38 gap comments resolved; the other 8 sit in wave 3 files).
- **Wave 3 contracts (W3.0).** Skeletons of the Master Controller, the subsystem base and controller, the timer/processing/atoms subsystems, 10 inert stub subsystems for later milestones (`SS_NO_INIT | SS_NO_FIRE`, `STUB(Mx)`), all 231 config entry types, the atom/turf/area engine-part declarations; shim hooks for wave 3 (`world.map_cpu`, `/world` vars, `typesof` of a proc path lists only translated procs, exact `log`, a host file seam, `rand_seed` no-op, a map-resize hook, `Dm.Deferred` for non-throwing boot-path stubs listed in `docs/ai/specs/not-translated.md`); a per-world subsystem-type filter so test fixture subsystems are not created in every world; deviations DV9 (GLOB `vars` self-check), DV10 (Failsafe not ported), DV11 (`rand_seed` no-op). File ownership for the wave: `docs/ai/specs/wave3-ownership.md`.
- Tests: 2,182 green in Debug and Release.
- Lead choices for wave 3 (from a read-only scouting pass): six packages (mc, timers+processing, boot, logging+chat, atoms, engine; about 3,950 DM code lines plus the engine C#), independent test-writers only for mc, timers, atoms and engine (the pilot's test-writers found 1 defect in about 700 tests, at the cost of the translation time again); no virtual tick usage for tests (DV3 stays: the tick-usage pause path is unreachable); `configuration/entries/general.dm` translated whole. Merge order: engine, logging, mc, timers, atoms, boot (boot owns the "boot to first fire" integration test).
- Still open (later): `ref()` of text and numbers; LAZY* overloads for non-`DmList` list vars; whether `[DmParams]` on about 9,000 overrides that omit parameters is worth its cost (rule 7.2 requires it now).
- Next: wave 3 fan-out (six packages in parallel worktrees, then their test-writers), merge, then wave 4.

## 2026-10-06 — pilot (wave 2) merged

- Translated faithfully, each package by one translator and checked by an independent test-writer who wrote tests from the DM source: the datum core (`datum.dm`, `signals.dm`, `qdel` and an SSgarbage shim, `weakrefs.dm`, `callback.dm`, the SEND_SIGNAL/qdel/callback macros, the datum engine part with the state-hash field hasher), list/sort/comparator helpers (`_lists.dm` in full, `cmp.dm`, tg's timsort), traits (`_traits.dm` macros, `__HELPERS/traits.dm`), components, elements and SSdcs (`_component.dm`, `_element.dm`, `dcs.dm`). Later-wave types exist as skeletons (`// SKELETON`).
- Tests: 2,097 green in Debug and Release. Ported tg unit tests: `trait_addition_and_removal`, `component_tests`, `dcs_get_id_from_elements`, `dcs_check_list_arguments`, on a reusable `TgUnitTest` harness. The test-writers found one translation defect in about 700 new behaviour tests (a `(string)` cast in two comparators, fixed).
- **First measured speed.** Fully translated: 27 files, about 5,000 DM code lines; the C# translation is 0.98 C# code lines per DM code line. Translator time: datum core 34 min (about 1,050 DM lines), lists 41 min (about 2,400), traits 13 min (about 200), dcs 25 min (about 720): about 2,300 DM lines per translator-hour. Independent testing took roughly as long again (15-35 min per package). Four packages ran in parallel; the subscription's 5-hour usage limit interrupted the run once (about 30 minutes lost). Totals so far (`tools/measure_velocity.py`): 19.3k new-logic lines, 26.0k test lines, 8.5k translated C# lines (with skeletons), 120k generated; weighted 54k.
- Lead decisions at the merge: qdel's `Destroy` call is an isolation boundary (a runtime in `Destroy` ends `Destroy` and qdel carries on, as in DM); `HardDelete` marks the datum deleted (DV7); a BYOND builtin type carries its `[DmPath]` on its engine part in `Builtin/`.
- The pilot reported about 60 rule gaps and a few shim gaps; they are collected in `docs/ai/specs/m0-pilot-findings.md` for the rules v1.2 pass.
- Owner decisions (2026-10-06): keep the strict milestone order (finish M0 before M1, no parallel network track); work in batches with a session compaction between batches.
- Next batch: rules v1.2 from `docs/ai/specs/m0-pilot-findings.md` (rules text, the shim gaps: `ref()` of lists, typepaths and world; text builtins taking non-text values; `/world` procs reachable by name; Appendix C test exemption for TgState fields of translated statics; the `_component.dm:112` fix as a deviation). Then wave 3: Master Controller, subsystems, timers, processing, the world bootstrap (TgState, GLOB, SSgarbage, SSdcs created at world start), atoms and their initialization (SSatoms), the atom engine part and the map grid. Then wave 4: the Move chain, turf/area/obj/mob bases, timed actions and `do_after`. Then wave 5: the toy world, the M0 acceptance scenario and the C0 report.

## 2026-10-06 — rules v1.1 merged, pilot started

- Alignment pass over the docs and small shim details after wave 1 (one agent, one reviewer): a slot is `DmTask` exactly when `slots.tsv` says so (`suspends = yes`; slots coloured only through `waitfor = FALSE` definitions stay synchronous behind the wrapper); rules 3.3 P1 matches the slot generator; Appendix C lists every public name (a reflection test checks both directions); Appendix A takes the generated values and `slots.tsv` is authoritative; `round(A, B)` in double; number text follows OpenDream's BYOND-checked tests (`"[1000000]"` in full, positive numbers only); `GLOBAL_PROC_REF(x)` is the text `"/proc/x"`; `new /list(a, b)` builds nested lists; `for(var/T/x)` (every datum) differs from `for(var/T/x in world)` (atoms); the `<<` helper is `Dm.output_to` so `output` stays free for BYOND's builtin; `Dm.turn` for directions; an explicit `[DmTypeOrder]` assembly attribute; DV3 covers `time2text` in UTC. 1,299 tests green.
- Decisions the assistant will take when the code arrives (logged here so they are visible): `TYPEID`/`GET_TYPEID`/`IS_NORMAL_LIST` and `GLOBAL_VAR_INIT(refid_filter, TYPEID(filter(...)))` (is_helpers.dm:20) get a fixed per-builtin-kind id table with `ref()` kept strict (object-model.md section 3), before GLOB is translated; `rand_seed(random_seed)` in the Master Controller's `New` (master.dm:98) becomes a no-op recorded as a deviation, because the world RNG is seeded by the host and reseeded every tick (D31).
- Questions only a real BYOND 516 run can settle (no BYOND here; collected for a future oracle): number-to-text for whole numbers 1e6..2^24 and its positive-only asymmetry, `sorttext` case folding, `text2num` exponents, `in` range coercion of non-numbers, BYOND's own type order, the `stddef.dm` values taken from OpenDream's copy.
- Pilot (wave 2) started: datum core (datum, signals, qdel, weakrefs, callback), list/sort/comparator helpers, traits, components and elements; one translator and one independent test-writer per package; the first measurement of translation speed.

## 2026-10-06 — wave 1 merged

- Packages (each implemented by one agent and reviewed by another): **contracts** (the frozen DmShim API of translation-rules Appendix C as a compiling skeleton, the Kernel object registry `World.Objects` and per-world extension slots, Tg infrastructure `GlobalProcs`/`TgState`/`Builtins` partials, `docs/ai/specs/shim-ownership.md`), **values** (`DmList` and the DM value builtins: arithmetic, 24-bit bit operations, comparisons, text, math, RNG, JSON; conformance tests against the BYOND reference and OpenDream's BYOND-checked tests), **dynamic** (`DmTypes`, the binder with `[DmParams]` and named arguments, `Dm.Call` returning `DmTask<object>`, `Dm.@new` with ids and the registry, prototypes and `initial()`, `ref`/`locate`), **defines** (the `#define` converter: 15,113 constants from 16,496 defines, every define accounted for in a manifest; an independent re-derivation found 0 mismatches in 14,818 values), **apps** (headless server `src/Server` at 20 TPS with a determinism check; Godot 4.7.2 mono client `godot/` on net10.0 that runs the core and renders a test scene; both print the same state hash), **typeorder** (DM type order and proc order as generated C#, the full slot registry `docs/ai/specs/generated/slots.tsv`).
- Integration: 1,280 tests green in Debug and Release; the generators are up to date; the Godot smoke run passes. One flaky GC test was fixed at integration.
- Measured so far (`tools/measure_velocity.py`, week 2026-W41, about 1.5 calendar days of work interrupted twice by the subscription's usage limit): 18.8k lines of new logic, 13.6k lines of tests, 0.1k client lines, 119.7k generated lines; weighted 32.9k. No translated tg code yet, so V for translation is still unmeasured (the pilot measures it).
- Notable choices (in the docs; C0 list above unchanged): number text follows OpenDream's BYOND-checked tests where they contradict the reference prose (`"[1000000]"` prints in full); bit operations work modulo 2^24 per operand; `Dm.Call` never throws (errors live in the returned task); prototypes never get ids; type order follows OpenDream.
- Next: rules v1.1 alignment pass (in progress), then the pilot (wave 2).

## 2026-10-06 — wave 0a merged

- Review of the existing M0 work by 10 agents (5 reviewers, 5 adversarial verifiers): about 100 findings, most confirmed. Main ones: a detached call rethrew the callee's error into the caller; the scheduler could drop an entry; adjacent RNG ticks shared shifted streams; DM runtime-error, list `-=`, default-argument and delay-rounding semantics were described wrongly; the namespace `Ss3d.Core.Dm` shadowed the class `Dm`; the async model needs colouring computed up front because 35-54% of tg proc definitions become async.
- Hygiene on main: the shim is `src/Core/DmShim` (`Ss3d.Core.DmShim`); CS4014 is an error everywhere; package versions pinned; brace style pinned; `tools/measure_velocity.py` reports DM-equivalent lines.
- Merged packages: kernel (waiting model v1: `Waiting.Detach`/`NoSleep`/`WaitforFalse`, error capture, tick order with re-drains, BYOND delay rounding, RNG and hash fixes; 182 tests), translation rules v1 with `docs/ai/DEVIATIONS.md`, `docs/ai/specs/object-model.md` and `docs/ai/specs/presentation-seam.md`, corrected reading reports, the colouring scanner `tools/coloring` with `docs/ai/specs/generated/`.
- Next: wave 1 (frozen shim contracts, then the DmShim values and dynamic parts, the defines converter, the server and Godot client skeletons, the type-order generator).
