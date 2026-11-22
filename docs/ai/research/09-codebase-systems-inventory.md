# 09. Inventory of tgstation systems and estimate of the 3D port scope

Snapshot: `../tgstation`, shallow clone, HEAD `1d99bd7f` (2026-10-04), read-only. All numbers were obtained locally (grep/find/wc and Python scripts, see Appendix A). No external sources were used in this direction, except the license information from the local README/LICENSE. Confidence: **high** — direct count; **medium** — regex heuristic; **low** — estimate/assumption.

## Summary

- `code/`: 7,476 `.dm` files (7,472 in the listed subfolders and `_js`, +4 files directly in `code/`; corrected during verification), 1,226,880 lines (of which ~976k are neither blank nor `//` comments, verified: 975,940). 58% falls on `code/modules`. That is ~41,100 declared types, of which ~64% are "data-only" (they override vars, no procs). The data needs to be converted, not rewritten by hand.
- The simulation is tick-based: `world.fps = 20` (50 ms). ⚠️ There are not 128 subsystems but **140**: 128 are declared via `SUBSYSTEM_DEF`/`PROCESSING_SUBSYSTEM_DEF`, another 12 via specialized macros (`MOVEMENT_/TIMER_/VERB_MANAGER_/FLUID_/AI_CONTROLLER_SUBSYSTEM_DEF`, see Appendix B). About 91 of the 128 actually "fire" (estimate, not recomputed). The main mob and machine logic runs once every 2 s, atmos once every 0.5 s. ⚠️ Ticker subsystems (`SS_TICKER`, wait in ticks) are not 13 but at least 22 (21 every tick + `dbcore` every 10 ticks = 0.5 s): movement, projectiles, throws, timers, input, etc.
- The architecture is already close to "entity + components + events": 427 component types, 244 element types, 1,514 signals, ~5,000 subscriptions, 1,924 `AddElement`. Signals are cancellable and return flags; this is not just pub/sub.
- Main difficulties of the port: (1) the "infectious" `sleep` (800 `do_after`, 1,114 blocking `tgui_*`); (2) type as a value (`initial()` 3,436 times, `typesof` 876); (3) the 2D display layer (48.7k icon_state, overlays, vis_contents); (4) tgui (⚠️ ≈122k lines of `.ts/.tsx` in 908 files, ≈141k together with 111 `.jsx`; ≈446 entries in `interfaces/`; the original "130,798 lines / 916 files" were not reproduced); (5) rigid binding to the tile grid (`get_turf` 3,836, `locate` 1,824, `view/range` 877).
- The executable specification is sparse: 453 test classes (≈1,946 assert). About 2/3 check behavior, and all of them are integration tests inside a live world. Atmos is covered by two tests.
- Rough effort estimate (low confidence): vertical slice 160–290 person-weeks (PW) (≈3–5.5 person-years), full parity 700–2,000 PW (≈13–40 person-years), the main uncertainties are art and the UI strategy.
- License: the code is AGPLv3 (after commit 333c566b of 2014-12-31; before it GPLv3), the assets are CC BY-SA 3.0 "unless otherwise indicated" (README; also confirmed on github.com). A derivative port inherits copyleft (AGPL: code translation/adaptation = "modify"; CC BY-SA 3.0 §4(b): adaptations only under the same license). ⚠️ **The exceptions are real and significant for a commercial port:** (a) ≥28 entries with noncommercial licenses (CC BY-NC 3.0/4.0, CC BY-NC-SA 3.0, "Noncommercial 4.0") in 11 attribution files in `sound/`; (b) BBC SFX sounds with "© BBC" in `sound/attributions.txt`; (c) the CAS deck (`code/modules/cards/deck/cas.dm`) — CC BY-NC-SA 2.0; (d) `code/datums/martial/wrestling.dm` — Goonstation code (CC-BY-NC-SA 3.0), relicensed under AGPL with a mandatory header; (e) TGS DMAPI — MIT; (f) fonts in `interface/fonts` — CC BY 4.0 and "free for commercial use" without a license text; (g) some sounds are CC-BY/CC0 (attribution needed). An asset audit is mandatory before any monetization decision.

## Findings

### F1. Size and composition (high)

Commands: `find code -name '*.dm' | xargs wc -l`, a loop over `code/*/`. The table below covers the subfolders (sum 7,470 files; another 2 in `code/_js` and 4 directly in `code/` — 7,476 in total; lines: the total 1,226,880 matches when `_js` 85 and root 299 lines are included).

|Area|files|lines|
|---|---|---|
|`code/modules`|4,184|709,566|
|`code/game`|943|237,092|
|`code/datums`|1,515|173,096|
|`code/__DEFINES`|411|34,045|
|`code/controllers`|173|33,827|
|`code/__HELPERS`|122|19,574|
|`code/_onclick`|61|12,623|
|`code/_globalvars`|61|6,673|

Largest second-level folders (lines): `game/objects` 146k, `modules/mob` 105k, `antagonists` 62k, `game/machinery` 62k, `datums/components` 55k, `reagents` 44k, `food_and_drinks` 36k, `research` 32k, `admin` 31k, `clothing` 27k, `surgery` 26k.

Split by port value (medium, overlaps accounted for manually): core and T2 ≈ 899k lines (73%); long tail T3 (heretic, wizard, cult, changeling, fishing, mafia, bitrunning, wiremod, modular_computers, etc.) ≈ 180k (15%); replaceable (admin, tgs, client, unit_tests, lighting, mapping-tools) ≈ 87k (7%); defines/helpers/globals ≈ 60k (5%).

Non-DM: tgui ⚠️ 121,884 lines of `.ts/.tsx` in 908 files (+18,753 lines in 111 `.jsx`; React `^19.1.0` confirmed; the original "130,798 / 916" is not reproducible); 632 `.dmm` (36 MB, confirmed); 1,448 `.dmi` in `icons/` (confirmed); ⚠️ in `sound/` 4,423 files, of which audio 4,326 (3,793 `.ogg`, 531 `.mid`, 2 other), the remaining 97 are text attribution files (121 MB confirmed); SQL schema of 38 tables; `config/` ~1 MB; rust-g (⚠️ 76 `rustg_*` in `code/__DEFINES/rust_g.dm`, not ≈85).

### F2. Subsystems (high; full list in Appendix B)

`SUBSYSTEM_DEF` + `PROCESSING_SUBSYSTEM_DEF` = **128** (confirmed: 96 + 32). ⚠️ These are not all subsystems: another 12 are declared by other macros (`VERB_MANAGER_SUBSYSTEM_DEF`: input, speech_controller; `TIMER_SUBSYSTEM_DEF`: runechat, sound_loops; `MOVEMENT_SUBSYSTEM_DEF`: newtonian_movement, conveyors, hyperspace_drift, cliff_falling, ai_movement; `FLUID_SUBSYSTEM_DEF`: smoke, foam; `AI_CONTROLLER_SUBSYSTEM_DEF`: low_priority_ai_controllers), **140** in total. Distribution within the 128: ≈37–40 without `fire` (init/data; heuristic, a recount gives 40), 13 tickers (every 50 ms; ⚠️ of which `dbcore` with wait=10 ticks = 0.5 s, i.e. 12 run every tick), 21 faster than 1 s, 43 in the 1–5 s range, 13 slower than 5 s, 1 other. Counting the 12 additional subsystems, at least 22 are tickers (`input`, `speech_controller`, `runechat`, `sound_loops` and 5 movement subsystems inherit `SS_TICKER`, wait=1). The Master Controller scheduler is single-threaded and cooperative: `CHECK_TICK` at 235 and `MC_TICK_CHECK` at 83 places. `START_PROCESSING`: SSobj 195, SSfastprocess 44, SSprocessing 43, SSdcs 26, SSmachines 17. Time scales via `seconds_per_tick` (3,786 occurrences), i.e. the logic is already delta-time based.

Coupling (number of mentions): SSshuttle 604, SStgui 543, SSmapping 436, SSobj 407, SSticker 365, SSdbcore 333, SSblackbox 326. SStgui, SSdbcore and SSblackbox are cross-cutting dependencies; they need to be hidden behind interfaces (UI, DB, telemetry).

### F3. Types and hierarchy (high/medium)

- ~41,100 types are declared (regex over path lines in column 0, medium): `/datum` 19,829, `/obj` 18,085, `/area` 1,031, `/turf` 863, `/mob` 686, `/atom` 538. The atom tree: 21,203 types, median path depth 5 segments, maximum 10 (`/obj/item/stack/tile/carpet/neon/simple/pink/nodots/sixty`).
- Large branches: `/obj/item` 11,539 (clothing 1,782, food 1,282), `/obj/machinery` 1,700, `/obj/structure` 2,167, `/obj/effect` 2,143, `/mob/living/basic` 562, `/datum/reagent` 955, `/datum/design` 1,270, `/datum/crafting_recipe` 1,287, `/datum/action` 872.
- **~64% of `/obj` and `/datum` types have not a single proc** (upper bound; `/datum/design` 99%, `crafting_recipe` 98%, `/area` 98%, `/obj/item/clothing` 72%). The base classes are heavy: field declarations in the file ~112 (atom) + 181 (movable) + 223 (item); a human has more than 500 fields in total.
- `initial(var)` 3,436 times, `ispath` 830, `typesof/subtypesof` 876, `istype` 6,269, `new` ~8,500: DM uses a type both as a prototype and as a value.

### F4. Patterns: components, elements, signals (high)

|Metric|Value|
|---|---|
|`SEND_SIGNAL(`|1,539 calls in 561 files (+54 global)|
|`COMSIG_*` definitions|1,514 (13,001 tokens in code)|
|`RegisterSignal(s)` / `UnregisterSignal`|4,665+359 / 2,333|
|`SIGNAL_HANDLER` (the handler does not sleep)|4,677|
|`AddComponent` / `AddElement` / `LoadComponent`|1,572 / 1,924 / 21|
|component / element / status_effect types|427 / 244 / 423|
|trait: definitions / `HAS_TRAIT` / `ADD_TRAIT`|798 / 3,052 / 1,239|
|`/datum/ai_controller` types|235|

Correspondence: component ≈ ECS component with state and subscriptions; element ≈ stateless behavior, one instance per argument set (in C# a singleton system or mixin); trait ≈ a tag that tracks its sources; signal ≈ a typed event with handler ordering and returned flags (`COMPONENT_CANCEL_ATTACK_CHAIN`). A pure "data-oriented" ECS maps poorly onto 41k types and deep inheritance, whereas a hybrid of Entity + Prototype + Components + EventBus maps well. Signals are documented in comments; some of the events can be generated by the converter.

### F5. loc/contents, movement, collisions (high)

The container model is built into BYOND: `loc`/`contents`. `forceMove` is called 1,653 times (`doMove` manually calls `Exited/Entered` for the old and new loc, the area and all tiles of multi-tile objects). Overrides: `Entered` 61, `Exited` 199, `Moved` 80, `Move` 60, `Bump` 30, `Bumped` 44, `CanAllowThrough` 65; `Crossed` — 1, it was replaced by the `connect_loc` component (186 uses). Signals: `COMSIG_MOVABLE_MOVED` 580, `ATOM_ENTERED` 181, `ATOM_EXITED` 88, `PRE_MOVE` 53. Other: `.loc` 2,456, `.contents` 454, `get_dir/get_dist` 888, `get_step` 577, `dir` 2,075 mentions, `pixel_x/y/w/z` 3,159 (sub-tile offsets), `bound_*` 72 (multi-tile), multi-z (`zMove`/openspace) 358. The map is in `SIDE_MAP`, i.e. already 2.5D. Inertia and drift: `newtonian_move` + 17 `move_loop` types (SSmovement, ticker). Everything is stored in a discrete tile grid, and atmos, adjacency, doors and construction depend on it.

### F6. Click and interaction (high)

Chain: `ClickOn` (8 definitions) → `UnarmedAttack` / `item.melee_attack_chain` → `target.base_item_interaction` (results `ITEM_INTERACT_SUCCESS/BLOCKING`) → `*_act` by tool → `pre_attack` → `attackby` → `attack`. The new API has already displaced the old one: `item_interaction` 601 definitions, `interact_with_atom` 229, `ranged_interact_with_atom` 59; tool hooks `wrench_act` 222, `screwdriver_act` 246, `crowbar_act` 186, `welder_act` 100, `wirecutter_act` 58, `multitool_act` 130; only 40 old `attackby` and 33 `afterattack` remain. Others: `attack_hand` 285, `attack_self` 448, `interact` 84. Combat mode: `combat_mode` 652 mentions, there are no intents (`a_intent`). Click modifiers (right/alt/ctrl/shift/middle) 286+148 places. Targeting a body zone: 13 `BODY_ZONE_*` (6 main + 7 precise). The chain is well formalized and ports almost one-to-one as an "interaction pipeline" with events. But it assumes a click on an atom and `Adjacent()`; in 3D this is a ray from the camera and a distance.

### F7. Asynchrony and timers (high; share of "sleeping" procs — medium)

|Mechanism|Uses|
|---|---|
|`sleep(` / `stoplag`|344 (153 files) / 66|
|`do_after` (inside `/datum/timed_action.await()`)|800|
|`tgui_alert/input_*` (block until the player responds)|1,114; plus native `input()/alert()` 269|
|`INVOKE_ASYNC` / `set waitfor = FALSE` / `spawn(`|942 / 125 / 12|
|`addtimer` / `deltimer` / `CALLBACK`|1,612 / 191 / 2,285|

Directly "sleeping" procs: 1,548 out of 64,123 parsed (2.4%). But because of virtual dispatch by proc name, up to 43% are potentially blocking (upper estimate, medium): any `attack_hand` that waits for a `tgui_alert` makes the whole call chain "sleeping". Porting needs an explicit model: either `async/await` on a single-threaded game SynchronizationContext, or `DoAfter` as data with continuations. Threads are not suitable for gameplay. It is good that the 4,677 signal handlers and the 62 procs marked `SHOULD_NOT_SLEEP` are synchronous.

### F8. Global state and style (high)

~1,115 `GLOBAL_*` declarations (522 `LIST_INIT`, 293 `LIST_EMPTY`, 76 `VAR_INIT`, 75 `DATUM_INIT`), `GLOB.` uses 6,745; `usr` 3,115 (implicit actor); `world.time` 1,267 (deciseconds). Mutable global state is critical for testability and multiple worlds: in C# an explicit `World/Context` is needed instead of statics. Lifecycle: `Initialize` 4,331 definitions, `Destroy` 1,761, `qdel` 3,525, weakref 515+727. This is manual lifetime management (hard-delete/GC in SSgarbage); in C# it is replaced by an explicit `Delete()` + `Dispose` of subscriptions.

### F9. Display layer (high)

`overlays` 821, `add_overlay` 351, `mutable_appearance` 890, `update_appearance` 2,856, `vis_contents` 275, `render_target/source` 199, `filters` 562, `particles` 353, `animate()` 944, `plane` 1,030, `layer` 1,816, `icon_state` 28,115 mentions. Auto-smoothing of walls/tables: 1,080 `SMOOTH_*` uses, 495 smoothing groups. Display procedures (`update_icon*`, `examine`, `ui_data`) make up ~6% of proc lines (by a narrow list of names). Text output is huge: `span_*` 20,855, `to_chat` 9,816, `balloon_alert` 4,519, `visible_message` 3,959, `playsound` 3,725. Assets: 1,448 DMI, 48,691 icon_state (obj 25,090, mob 12,041, turf 6,424, effects 1,767, hud 1,695), 138,892 frames (directions × frames); 1,229 DMI are 32×32 in size. There is no automatic sprite→3D model conversion. The 3D layer must be designed as a separate client, and the simulation must output state, not an image. For lighting, the logic almost does not depend on visuals (`get_lumcount` only 7 times), so the engine will replace it.

### F10. Inventory of gameplay systems (high on LOC and counts; complexity 1–5 is the author's estimate, low)

|System|LOC (DM)|Content scale|Depends on|Cmplx|
|---|---|---|---|---|
|Atmosphere (LINDA + pipenets)|22.5k|20 gases, ≈25 reactions, 296 atmos components, SSair 0.5 s; `Atmospherics.md` exists|turf, area, machines, breathing, fire|5|
|Mob health (carbon/human, organs, wounds, statuses, DNA)|67.5k|6 damage types (brute/burn/tox/oxy/stamina/brain), 13 zones, 166 bodypart, 323 organ, 33 wound, 31 species, 423 status effect, 87 trauma|reagents, atmos, brain/mood|5|
|Surgery|6.7k operations (25.8k in the module)|120 operation types|health, tools|4|
|Chemistry/metabolism|43.7k|959 reagent, 717 reaction types (equilibrium/pH/purity), `on_mob_life` 383|health, containers, plumbing 2.5k|4|
|Food/hydroponics/fishing|36.4k / 11.5k / 12.6k|1,282 food, 158 seeds, 73 soup recipes|reagents|2–3|
|Diseases|6.8k|51 disease, 50 symptom|health, reagents|3|
|Power grid (cable/APC/SMES/solar/SM)|16.1k (SM 2.6k, singularity 2.3k)|`powernet`, 454 power calls|machines, area|3 (SM 4)|
|Construction/deconstruction|stacks 9.6k + crafting 4.8k|633 stack, 1,287 crafting, 365 circuitboard, 55 construction component, 24 RCD|tools, power|3|
|Machines (base)|61.6k|1,700 types, 251 computer, 98 airlock, 81 vending|power, access, UI|3|
|Research/techweb|32.3k (+2.0k experisci)|127 node, 1,275 design, 31 fabricator|machines, materials|3|
|Cargo and economy|13.5k + 1.2k|402 supply pack, 194 bounty, 185 export, stock market|shuttle, ID|2|
|Jobs and access|7.3k + 2.3k|≈35 station jobs (⚠️ out of 96 direct subtypes of `/datum/job`, 116 including nested; not 90), 156 id_trim, 122 ACCESS|SSjob, ID|2|
|Antagonists|62.4k|47 folders, 142 antag types, 158 objectives, 365 uplink; heretic 20.6k, wizard 5.9k, cult 5.5k|dynamic, mind, everything|4|
|Dynamic, round flow, events|3.8k + 0.9k + 9.3k + 2.1k|50 rulesets (16 roundstart / 29 midround / 4 latejoin), 114 events, 80 station traits|jobs, antags|3|
|Shuttles|9.4k|`onShuttleMove` family 74 occurrences|turf, area, atmos|5|
|Tram|6.8k||shuttle-like mechanics|4|
|Communications/radio/language|2.3k + 2.0k|17 channels, 26 languages, 228 emotes|speech|2|
|AI and cyborgs|9.6k|31 law sets, 13 borg models, 31 bots|cameras, machines|4|
|Mechs / MOD suits|14.5k / 14.3k|27 mechs, 245 mod types|power|3|
|NPCs and AI controllers|10.8k + basic 46.5k|235 controllers, 473 `bt_node`, 91 `.bt.json`, 562 basic mobs|pathfinder|3|
|Wires and hacking|2.3k (+wiremod 14.1k)|48 `/datum/wires`|machines|2 (wiremod 4)|
|Lighting|1.7k + 1.4k|`set_light` 502|engine|2|
|Fire and explosions|~2k|SSexplosions, LINDA_fire, burning|atmos, health|3|
|Vacuum/pressure/zero gravity|inside atmos and movement|`pressure` 73, `newtonian` 355|atmos, movement|4|
|Observer/admin|3.1k / 30.9k|340 `ADMIN_VERB`|everything|2 (rewrite)|
|DB and persistence|0.7k + 0.8k|38 tables, 13 persistence modules||2|
|Configs|2.6k|`config/` ~1 MB, TOML/JSON/txt||1–2|
|tgui|DM 3.2k + ⚠️ TS/TSX 121.9k (+JSX 18.8k; was 130.8k)|≈446 entries in `interfaces/` (401 files + 45 folders; some are not interfaces but helpers); `ui_data` 416, `ui_act` 414|SStgui|4–5|
|Clothing/storage/inventory|27.3k / 4.0k|21 slots, 161 storage types||3|
|Weapons/projectiles|21.5k|335 gun, 397 projectile||3|

Note: the UI protocol (`ui_data` → JSON → `ui_act`) already separates the UI from the simulation. This means the UI can be replaced without touching the logic.

### F11. Unit tests as a specification (high; classification — medium)

`code/modules/unit_tests`: 277 files, 16,236 lines, 453 `/datum/unit_test` classes (444 `Run()`), 1,946 `TEST_ASSERT*/TEST_FAIL`, 88 reference PNGs for screenshot tests. By domain (classes / asserts): combat, damage, stuns, wounds 86/276; surgery, organs, health 42/293; nutrition, construction, machines, access 32/120; antags and spells 28/118; speech and language 27/98; hydroponics, fish, mining 19/104; reagents and food 16/104; cargo 13/72; mechs, MOD, borgs 6/34; **atmosphere 2/9** (⚠️ by names: `atmospherics_sanity`, `gas_transfer`; `breath`/`lungs` also touch the gas mixture but are classified under health — the classification boundary is arbitrary); the rest (icons, spritesheets, DCS, subsystems, GC) 182/712. That is, behavioral tests are about 2/3 (≈300). All of them are integration tests: they require a live world and real objects (`allocate`). Coverage is ~1 class per 140 procs. Conclusion: the tests are a useful source of scenarios (especially health, surgery, combat), but for atmos and most systems we will have to write our own golden tests (for example, traces from the original server).

### F12. DM features that are hard to carry over to C# (high on counts)

|Feature|Uses|What to do in C#|
|---|---|---|
|Blocking calls (sleep, do_after, tgui_input)|344 / 800 / 1,114|async on the game loop or continuation objects|
|Proc override + `..()`|23,190 `..()` calls; `SHOULD_CALL_PARENT` 449|prototypes + components; virtual methods only in the core|
|Type as a value, `initial()`|3,436; `typesof` 876; `ispath` 830|prototype registry + codegen from DM|
|Global lists/variables|1,115 declarations, `GLOB.` 6,745|`World` + DI; no statics|
|Overlays/appearance/vis_contents/filters|821 / 890 / 275 / 562|Node3D tree + shaders; the sim outputs state|
|Verbs/Topic/href|`GAME_VERB*` ≈150, `ADMIN_VERB` 340, `Topic(` 45, `href=` 460, `href_list` 855, `winset` 70|commands/RPC + native UI|
|Implicit `usr`|3,115|explicit `Actor` in the command context|
|Spatial queries `view/range/oview/hearers`|877|spatial grid (`SSspatial_grid` exists)|
|Dynamic typing, `null` semantics, 1-indexed lists|everywhere|this difference produces silent bugs, a converter with checks is needed|

### F13. Maps (high)

Seven station `.dmm` files; each z-plane is 255×255. MetaStation: 1 z, 65,025 cells (confirmed), **16,028 non-empty tiles** (⚠️ not reproduced: the definition of "non-empty" is not given in the report; a recount gives 15,734 tiles with a non-space turf, 18,012 with a non-space area, 18,340 "turf or object" — on the order of 16–18k), 2,702 distinct prototypes (recount by used keys: 2,655), 35,326 objects (recount: 34,801 `/obj`+`/mob`, discrepancy ≈1.5%; 11,727 machinery — recount 11,550; the rest of the breakdown was not rechecked: 6,626 atmos; 3,631 cable; 10,986 structure; 2,577 item; 1,354 door/511 airlock; 770 light fixtures). Others: Delta 19.7k non-empty tiles and 52.5k objects; Kilo 22.7k and 38.4k; Tram (2 z) 38.4k and 36.0k; Nebula (2 z) 27.6k and 73.8k; Icebox 3 z. The TGM format is read by a simple parser. The repository has `tools/mapmerge2`, `tools/dmi`, `tools/maplint`. For the slice, the ~2.7k prototypes of one station are enough, not 41k.

### F14. Configuration, DB, integrations (high)

`config/`: 39 files and folders (game_options, jobconfig.toml, dynamic.toml, etc.). DB: 38 tables (bans, players, rounds, deaths, polls, achievements, library, etc.), accessed through rust-g/MySQL. Of the 76 rust-g functions (⚠️ was "≈85"; counted by `#define rustg_*`) almost all are replaced by the .NET standard library (the author's assumption, not verified); only `iconforge/dmi` is specific (the asset pipeline). Integrations: Lua, TGS, Discord, TTS.

## Implications for the 3D port

1. **Do not rewrite line by line.** ~64% of types are data, so a converter from DM types to prototypes (YAML/JSON) that preserves the inheritance hierarchy is needed, while the behavior core (~15–25% of the code) is written by hand. ⚠️ Python parsers for DMM (`tools/mapmerge2/dmm.py`) and DMI (`tools/dmi/__init__.py`) already exist in `tools/`; there is no DM parser in `tools/` — it is the external SpacemanDMM (version `suite-1.11` pinned in `dependencies.sh`, config `SpacemanDMM.toml`), and its suitability for a converter must be verified separately.
2. **Simulation core without Godot**: `World` (no statics), a prototype registry, an `EventBus` with cancellable ordered events, the `loc/contents` container model, a scheduler with three frequencies (20 Hz movement, 0.5–2 s for the rest, delta time), an interaction pipeline modeled on `melee_attack_chain`.
3. **Decide the world model before code.** A discrete tile grid as the authority (atmos, adjacency, construction, maps) plus continuous/smooth movement and 3D visuals is the most compatible option. Fully physical 3D breaks atmos, doors, `Adjacent()` and all 2,075 `dir` places.
4. **Asynchrony is architectural decision No. 1.** Define `DoAfter`, dialogs and requests to the player as first-class objects with a continuation. If `async/await` is chosen, then only on a deterministic game loop.
5. **Separate visuals from logic.** The sim outputs state (`open`, `damaged`, `on`), and the client maps it to a model/animation. Art is the main expense: 48.7k icon states. A strategy is needed (a kit of modular models, billboard sprites from DMI for the long tail, generation).
6. **UI.** Keep the `ui_data/ui_act` protocol. Embedding tgui (web) or rewriting it natively in Godot is a separate decision. The slice needs ≈20 screens, parity ≈440.
7. **Tests.** Port the health/surgery/combat scenarios as core tests. For atmos and shuttles, collect reference traces from the original. Use `Atmospherics.md` as a specification.
8. **Slice.** One station (Meta, ~16k tiles, ~2.7k prototypes), ~10 jobs, 5 damage types, organs, construction, nutrition, basic atmos, shuttle evacuation. Without T3, chemistry, research and antagonists (at most 1–2 basic ones).
9. **Avoid:** shared global statics, preserving 2D-dependent mechanics (`pixel_x`, `plane`, smoothing), porting admin/`Topic`/verbs, carrying over `sleep` semantics "as is".
10. **License and attribution**: AGPLv3 and CC BY-SA 3.0 (README); remodeled assets are probably adaptations within the meaning of CC BY-SA 3.0 (the definition of "Adaptation" includes translation/derivative work; §4(b) requires the same license) — the legal interpretation of 3D redrawing was not checked, and this is not legal advice. For a networked game, AGPL §13 requires offering users the source of the modified version. ⚠️ The "unless otherwise indicated" exceptions **were found** (see Summary: NC sounds, BBC, CAS, Goonstation code, MIT TGS, fonts); NC assets are incompatible with monetization and require replacement or exclusion.

### Effort estimate (low confidence, ±50%)

Assumptions: 1 person-week (PW) = a senior C#/Godot engineer with tests and review; "manual" throughput ~600 lines of C# per PW, "with LLM assistance and a converter" ~1,800; 1 DM line ≈ 1 C# line; the figures have not been validated in practice.

**Vertical slice** (1 station, ~10 roles, basic health/construction/atmos/nutrition, ≈45k lines of logic + 15k infrastructure, ~350 art assets, ~20 UI screens):

|Block|PW|
|---|---|
|Core: prototypes, events, scheduler, containers, movement, interaction|24–40|
|DMM import → 3D + a set of station modules|12–20|
|Logic: health 12–20, atmos 8–14, nutrition 4–6, construction 8–12, doors/access/roles 4–6, radio 3–5, round flow/shuttle 6–10|45–73|
|Client: controller, camera, inventory, HUD, ~20 screens|20–32|
|Art, animations, audio|24–45|
|Networking (if multiplayer; otherwise 0)|16–40|
|QA, tooling, CI, integration (~15%)|20–35|
|**Total**|**≈160–290**|

**Full parity**: ≈480k lines of "active" core and T2 logic + ≈130k T3 (⚠️ the origin of 480k/130k is not shown in the report: per F1 the core and T2 = 899k, T3 = 180k, and non-blank/non-comment lines are ≈80% of all lines; the coefficient ≈0.53–0.72 is not justified, so the resulting 700–2,000 PW is unstable with respect to this assumption); converter/data pipeline 20–40 PW; UI ≈440 interfaces (0.12–0.35 PW per interface); art 8–10k models (0.5–2 h per model); networking 40–60 PW; QA +18%. Manual scenario ≈ 2,000 PW, with automation ≈ 700 PW. Reduction levers: dropping T3 (−10–15%), billboard sprites instead of models (art −70%), embedded tgui (UI −60–80%), a single-player game without networking.

## Open questions (for the user to decide)

1. Multiplayer (server-authoritative, how many players) or a single-player/co-op game? Affects 16–60 PW and the architecture.
2. Fidelity target: full parity (140 subsystems, 41k types) or "inspired by SS13" with trimmed content? Which stations and jobs are mandatory in v1?
3. World model: tile authority with smooth visuals, or continuous 3D physics (ray-based shooting, melee by hitboxes)?
4. Camera and input: first person, third person, or top-down 3D? The interaction chain depends on this.
5. Art: hand-made models, ready-made kits, generation, billboard sprites from DMI for the long tail? Style? Who makes the art?
6. UI: reuse tgui (web embedding) or rewrite natively?
7. Resources: team size, deadline, is LLM automation of code translation acceptable and how to verify the result.
8. Is the AGPLv3/CC BY-SA license and open source code acceptable?
9. Are a DB, accounts, bans, admin tools, persistence needed in v1?
10. Is moddability needed (content in YAML for the community)?
11. Target platforms (PC only or also web/mobile)? This needs to be checked for compatibility with the Godot 4 C# stack.

## Gaps

- External sources were not opened: comparison with other engines, the experience of earlier ports, and known problems of the original are moved to other directions. The time estimates are not calibrated against real projects.
- Type counting (regex over column 0), the "data-only" share (upper bound), the share of "sleeping" procs (by names), the classification of tests by file names are heuristics (medium).
- Subsystem frequency is taken from declaration lines; 1 `wait` out of 128 was not recognized (clock_component); for subsystems without `wait` the default of 2 s is assumed. ⚠️ Subsystems from 5 specialized macros (12 total) were not counted; the inherited `ss_flags`/`wait` of base types (`timer`, `movement`, `verb_manager`) were not expanded either.
- External sources are not claimed in the report, so verification of external claims was limited to licenses (CC BY-SA 3.0 legalcode, GNU AGPLv3, the repository page on github.com); there are no claims about the causes of project failures or the statuses of ports in this report.
- Not analyzed: balance and the game economy, the contents of `.dmm` beyond statistics, CPU load, the internals of rust-g, `tools/`.
- The 1–5 complexity ratings and PW are expert estimates, not measurements.

## Appendix A. Main commands

- Lines/files: `find code -name '*.dm' -print0 | xargs -0 cat | wc -l`; by area — a `for d in code/*/` loop; without blank and `//` lines: `grep -v -E '^\s*(//.*)?$'`.
- Pattern counters: `grep -rhoE '<regex>' code --include='*.dm' | wc -l`.
- Types: Python over `^/path$` lines (column 0); data-only — types without `/path/proc` and `/path/name(`; "sleeping" procs — parsing of definitions and propagation by name.
- Subsystems: Python over `SUBSYSTEM_DEF|PROCESSING_SUBSYSTEM_DEF` (`name/wait/ss_flags`). Maps: a TGM parser. Assets: reading the PNG `Description` chunk in DMI. tgui: `find tgui/packages -name '*.ts*' -print0 | xargs -0 cat | wc -l` (⚠️ a recount with this command gives 121,884 lines in 908 files; the original value 130,798 is not reproducible, and the cause of the discrepancy was not established).

## Appendix B. 128 subsystems from `SUBSYSTEM_DEF`/`PROCESSING_SUBSYSTEM_DEF` (name, period; empty = 2 s default)

⚠️ The list is incomplete: 12 subsystems from specialized macros are not included: input, speech_controller (tickers, wait=1), runechat, sound_loops (`timer` subtypes, tickers), newtonian_movement, conveyors, hyperspace_drift, cliff_falling, ai_movement (`movement` subtypes, tickers), smoke, foam (`fluids` subtypes, wait is chosen automatically), low_priority_ai_controllers (`ai_controllers` subtype, 0.25 s, background). In the "Every tick" group below, `dbcore` actually fires once every 10 ticks (0.5 s).

**Every tick, 50 ms (13):** chat — output to the client; dbcore (10 ticks) — SQL queue; explosions — explosions by ticks; icon_smooth — auto-tiling; lighting — light recalculation; mouse_entered — hover dedup; movement — auto-movement loops; projectiles — projectile flight; sound_tokens — positional sounds; throwing — throws; timed_actions — do_after; timer — `addtimer`; verb_manager — deferred client commands.

**Faster than 1 s (21):** ai_controllers 0.25 — mob AI; air 0.5 — atmos (LINDA, pipenets); aura 0.3 — auras; circuit_component 0.1 — circuits; fastprocess 0.2 — fast objects; fishing 0.05 — fishing; fluids 0 — fluid flows; instruments 0.05 — music; looting 0.5 — loot panel icons; lua 0.1 — Lua scripts; pathfinder 0.05 — JPS; priority_effects 0.2 — priority statuses; radiation 0.5 — radiation; reagents 0.25 — reactions in containers; server_maint 0.6 — client maintenance; singuloprocess 0.05 — singularity; statpanels 0.4 — stat panel; supermatter_cascade 0.5 — supermatter cascade; tgui 0.9 — UI updates; transport 0.05 — tram; tts 0.05 — speech synthesis.

**1–5 s (43):** acid 2 — acid on items; ambience 1 — ambient sound; antag_hud 2 — antag HUD; area_contents 2 — area tile lists; asset_loading 2 — asset preloading; augury 2 — observer ghosts; ban_cache 2 — ban cache; basic_avoidance 2 — AI obstacle avoidance; blood_drying 4 — blood drying; burning 2 — burning; dcs 1 — component processing; digital_clock 1 — digital clock; escape_menu 2 — menu; events 2 — random events; garbage 2 — garbage collection/qdel; greyscale 3 — greyscale sprites; lobby_menu 1 — lobby; machines 2 — machines; manufacturing 1 — conveyors; mapping 2 — loading maps and z-levels; market 2 — black market; mobs 2 — mob `Life()`; mood 1 — mood; newplayer_info 1 — new players; npcpool 2 — NPC pool; obj 2 — object processing; personalities 3 — personalities; ping 4 — ping; plumbing 1 — plumbing; polling 1 — ghost polls; processing 1 — general processing; quirks 1 — quirks; research 1 — research; security_level 2 — alert level; shuttle 1 — shuttles; spatial_grid 2 — spatial grid; station 5 — station goals and traits; ticker 2 — round stages; turrets 2 — turrets; vote 1 — votes; wardrobe 1 — object cache; weather 1 — weather; wet_floors 1 — wet floors.

**Slower than 5 s (14):** blackbox 600 — round telemetry; cameras 120 — camera cache; discord 300 — integration; dynamic 300 — ruleset selection; economy 300 — salaries; modular_computers 60 — NTNet network; ore_generation 60 — ore; profiler 300 — profiler; radioactive_nebula 30 — nebula; stock_market 60 — stock market; sun 60 — sun angle; time_track 10 — tick metrics; vis_overlays 60 — vis cache; clock_component — clocks (`COMP_CLOCK_DELAY`).

**Without `fire` (37, init and data only):** accessories (sprite accessories), achievements, admin_verbs, assets, atoms (atom initialization), bitrunning, disease (registry), early_assets, greyscale_previews, id_access (registry of IDs and accesses), init_profiler, ipintel, job (job assignment), lag_switch, library, map_previews, map_vote, materials, minor_mapping, overlays (appearance cache), pai, parallax, persistence (13 modules), persistent_paintings, points_of_interest, queuelinks, radio (channels), restaurant, skills, sounds, stickyban, title, trading_card_game, traitor, tutorials, verbs, wiremod_composite.

## Verification

Adversarial check of 2026-10-05: the local numbers were recounted with independent commands/scripts on the same snapshot (HEAD `1d99bd7f`), and the license claims were checked against README/LICENSE/attribution files and against external primary sources (creativecommons.org/licenses/by-sa/3.0/legalcode, gnu.org/licenses/agpl-3.0, github.com/tgstation/tgstation). Corrections were made in the text above and are marked with ⚠️.

|No.|Claim|Verdict|What the check showed|
|---|---|---|---|
|1|7,472 `.dm` files, 1,226,880 lines, ~976k without blanks/comments|partially confirmed|The line counts are correct (1,226,880; without blanks and `//` — 975,940). There are 7,476 files: the F1 table gives 7,470, +2 in `code/_js`, +4 in the root of `code/`. Corrected.|
|2|128 subsystems, of which 13 tickers|partially confirmed|Confirmed 128 = 96 `SUBSYSTEM_DEF` + 32 `PROCESSING_SUBSYSTEM_DEF`. But there are 140 in total: another 12 via `MOVEMENT_/TIMER_/VERB_MANAGER_/FLUID_/AI_CONTROLLER_SUBSYSTEM_DEF`. Tickers are not 13 but ≥22; `dbcore` (wait=10 ticks) is not "every tick". `world.fps = 20` confirmed (`code/world.dm:19`). Corrected in Summary, F2, Open questions, Appendix B.|
|3|tgui: 130,798 lines of TS/TSX in 916 files; ≈440 interfaces|not confirmed (number)|Recount: 121,884 lines in 908 `.ts/.tsx`; together with 111 `.jsx` — 140,637 lines. Interfaces: 446 entries in `interfaces/` (401 files + 45 folders). React `^19.1.0` confirmed. The number is corrected; the cause of the original discrepancy was not established.|
|4|License: AGPLv3 (code), CC BY-SA 3.0 (assets); the port inherits copyleft|partially confirmed|The basics are confirmed in README, LICENSE and on github.com (code after 2014-12-31 — AGPL, before — GPLv3; assets — CC BY-SA 3.0 "unless otherwise indicated"). Confirmed externally: CC BY-SA 3.0 §4(b) requires the same license for adaptations; AGPLv3 "modify" includes adaptation, and §13 requires source for network users. Not stated in the report: ≥28 NC entries in 11 attribution files in `sound/`, BBC sounds, the CAS deck CC BY-NC-SA 2.0, Goonstation code with a special header, TGS DMAPI under MIT, fonts CC BY 4.0. Added to Summary and Implications #10. The legal interpretation of 3D redrawing of sprites was not checked.|
|5|Blocking constructs: `sleep(` 344, `do_after` 800, `tgui_*` 1,114, `input/alert` 269|confirmed|Exact match: 344, 800, 1,114, 269. Minor discrepancies: `stoplag` 69 (was 66), files with `sleep(` 154 (was 153). `INVOKE_ASYNC` 945 (was 942), `set waitfor = FALSE` 125, `spawn(` 12.|
|6|~41,100 types, ~64% data-only; component 427 / element 244 / ai_controller 235; atom tree 21,203|confirmed|41,093 types; 63.9% of `/obj`+`/datum` without their own procs (upper bound, as stated); 427 / 244 / 235 exactly; atom tree 21,197, median depth 5, maximum 10, the example path exists (`tile_types.dm:1064`).|
|7|MetaStation: 16,028 non-empty tiles, 2,702 prototypes, 35,326 objects; 7 station maps|partially confirmed|65,025 cells is correct. "Non-empty" is not defined: the recount gives 15,734–18,340 depending on the definition; objects 34,801 (≈1.5% lower); prototypes 2,655. For Delta/Kilo/Tram/Nebula objects are within ±2%. 7 station maps in `_maps/map_files` confirmed (including Catwalk). The conclusions for the slice do not change.|
|8|Unit tests: 453 classes, 1,946 asserts, 277 files / 16,236 lines, atmos covered by two tests|partially confirmed|Confirmed: 454 declarations (453 + base), 1,946 `TEST_ASSERT*/TEST_FAIL`, 444 `Run()`, 277 files, 16,236 lines, 88 PNG. "Two atmos tests" depends on an arbitrary classification: there are also `breath.dm`, `lungs.dm`; a note was added.|
|9|rust-g ≈85 functions; DM/DMM/DMI parsers already exist in `tools/`|not confirmed (number) / partially|`code/__DEFINES/rust_g.dm` has 76 `rustg_*` defines. Python DMM and DMI parsers exist in `tools/`, there is no DM parser: that is the external SpacemanDMM `suite-1.11`. Corrected.|
|10|Effort estimates (160–290 PW; 700–2,000 PW)|arithmetic confirmed, basis not verifiable|The table sums 161–285 PW and the conversion to person-years (3.1–5.6; 13.5–38.5) agree. It is not shown where ≈480k "active" lines of core and T2 come from, given 899k per F1 (≈80% non-comment would give ≈720k). The estimates remain low confidence, as stated; ⚠️ added.|
|11|Other checked numbers|confirmed|LOC of the 12 largest subfolders (146,095; 105,173; 62,384; 61,647; 54,989; 43,676; 36,390; 32,338; 30,923; 27,332; 25,819; 20,553 and others) exact; coupling SSshuttle 604, SStgui 543, SSmapping 436, SSobj 407, SSticker 365, SSdbcore 333, SSblackbox 326 exact; `START_PROCESSING` 195/44/43/26/17 exact; `initial(` 3,436, `typesof` 876, `ispath` 830, `istype` 6,269, `SEND_SIGNAL` 1,539, `forceMove` 1,653, `get_turf` 3,836, `locate` 1,824, `GLOB.` 6,745, `usr` 3,115, `qdel` 3,525, `Initialize` 4,331 exact; `icon_state` 48,691 in 1,448 `.dmi` exact; `a_intent` 0, `SIDE_MAP` and `Atmospherics.md` exist; Dynamic 16/29/4 rulesets and 114 events confirmed; `.dmm` 632 and 36 MB.|
|12|≈35 station jobs out of 90 `/datum/job`|partially|`/datum/job` subtypes: 96 direct, 116 including nested (not 90); ≈35 station jobs is plausible from the list. The 4,423 sounds include 97 text files (audio 4,326). Corrected.|

Not verified (the author's heuristics, remain medium/low): the share of "sleeping" procs 2.4% / up to 43%; ≈91 "firing" subsystems; the classification of tests into behavioral (~2/3); complexity 1–5; the split into tiers T2/T3 (it arithmetically sums to the 1,226k total); the `CHECK_TICK` recount gave 217 instead of 235 (regex-dependent, not critical).

Overall assessment: the code counters are mostly reproducible to within 0–2%, and the errors are concentrated in four places (the number of subsystems and tickers, the tgui volume, the number of rust-g functions, an incomplete picture of licenses); the report's conclusion that the port should be built around a data converter and an explicit asynchrony model is not changed by these corrections.
