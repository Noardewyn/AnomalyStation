# M3 file ownership

Status: v1.1 (the M3 contracts step, M3.0: phase A, and phase B's tg skeletons on the generated classes), 2026-10-08. Who edits which file while the M3 packages work in parallel
(the lead's `plan.md`, the scout's `scope.md` section 3): **P1** the types converter and the generated classes, **P2** maps
and world load, **P3** machinery, doors, access and lights, **P4** turfs, structures, smoothing, spawners and decals, **P5**
protocol version 3, the replicator, motion faces and bots, **P6** client world rendering, **P7** the client light grid, the
testers **T1** (doors, machinery, lights; after P3) and **T2** (map load and structures; after P2 and P4), and the
integration **I1**. **C** is the contracts step itself (its files are frozen once it merges). Paths are repository paths;
`Tg/` is `src/Core/Tg/`. The format follows `m2-ownership.md`.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new files inside the folders its rows
  give it, and new mirror files of tg files that no row names inside its own tg area, and lists them in its report. A file
  no row names is not edited: report the needed member to the lead (a `// GAP(<owner>): ...` comment at the call site).
- **Contracts are frozen.** Every public type, member and signature the contracts step wrote (protocol version 3's types,
  fields, `Limits` and `ReplicaWorld`'s version-3 API; `TileFaces` and `ICollisionView.IsFaceBlocked`; `LocomotionSeam.FacesOf`
  and `IsFaceBlocked`; `CellState`; `InertSeam` and `dm_inert_initialize`; the map loader's `MapText`, `MapValue`,
  `ParsedMapText`, `MapVars`, `MapSeam`, `MapSelection`, `HeldSet`; `ServerConfig.Map` and `MapsFolder`; `ServerHost.SpawnPoints`;
  `DevCommands.Access` and the `access` syntax; `ReplicatedFields.MaxEntityInvisibility`; the light grid's uniforms, include
  and `LightLayer`; `FakeMap`, `FakeMapEntity` and `FakeServerOptions.Map`; the specs' contract sections) changes only
  through the lead. The owner fills the bodies (`throw new NotImplementedException("M3 Pn")` in new logic, `Dm.NotTranslated`
  in tg translation, rules 1.4), may add private members and new types, and may add public members a contract lacks
  (reported).
- **The meaning in the doc comments is the contract**: the face invariant (`TileFaces`: a face is blocked when either tile's
  `TileEdges` has it toward the other; the controller crosses a border exactly when the neighbour is not solid and the face
  is not blocked, on the server and in the predictor alike), the cell flags' bits, the light record's presence rule
  (light-grid.md 1), the inert init's line list (types-converter.md 7), the map format's grammar and order (map-format.md 4).
- **Generated var blocks (D111, rules 10.2 as amended).** No package writes a var block for a type the generator covers or
  edits `Tg/Generated/**`; translators add procs in partial classes beside the generated part, rerun P1's tool after adding
  procs (`dotnet run --project tools/Anomaly.Tools.Types -- ...`, types-converter.md 1) and commit the regenerated files in their
  own commit. A type becomes live by types-converter.md section 6, with rows in the package's own section of
  `docs/ai/specs/live-partial.txt`.
- **Bodies throw until their owner lands.** A test that reaches another package's body fails, as intended. Test your own
  code with your own fixtures. **Merge order (plan.md): P1, C, then P5, P2, P4, P3, then P6, P7, then T1 and T2 as they
  finish, then I1.** P3 goes last of the server packages because its helpers' `LateInitialize` runs in P2's load order.
- **Specs.** P1 fills `types-converter.md` sections 1-3 (4-7 are contracts); P2 keeps `map-format.md` true to the converter
  (a change of the format is the lead's) and adds the `MAP` option to `apps.md` section 1; P5 writes the version-3 sections of
  `protocol.md`, `replication.md` and `locomotion.md` (faces); P6 writes `apps.md` section 2 (the M3 renderer, 2.9, presets,
  `--bench`); P7 fills `light-grid.md` sections 3-6; I1 writes `apps.md` section 4 (M3).
- **Shared documents** (`docs/ai/DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, `DEVIATIONS.md`, `docs/ai/translation-rules.md` and its
  Appendix A and C, `object-model.md`, `presentation-seam.md`, `waiting-model.md`, `shim-ownership.md`, `dm-conformance.md`,
  `not-translated.md`, this file): the lead at merge; packages report additions (a deviation with its `// DEVIATION(DVn)`
  text, a decided slot, a new public Kernel/shim/Tg-infrastructure name, a deferred leaf's row). DV22-DV24 are written
  (D116, D117, D115); their code carries `// DEVIATION(DVn)` at the sites (P7's space base light, P3's `access`, P5's entity
  rule).
- **The shim and the Kernel** (`src/Core/DmShim/`, `src/Core/Kernel/`) are not edited by the packages, except P1's new
  `DmShim/DmInertAttribute.cs` (the lead adds `[DmInert]` to Appendix C at P1's merge). **The atom engine parts**
  (`Tg/Builtin/`) only as the rows below give them.
- **The solutions and project files** (`AnomalyStation.sln`, `godot/Anomaly.Client.sln`, every `.csproj`,
  `Directory.Build.props`, `.gitignore`) are the lead's, except `tools/Anomaly.Tools.Types/*` (P1, outside the solution),
  `tools/Anomaly.Tools.Maps/*.csproj` (P2: the lead adds it to the solution at merge; `/maps/` is already in `.gitignore`),
  `tools/Anomaly.Tools.Bots/*.csproj` (P5) and `godot/Anomaly.Client.csproj` (P6).
- **D31 everywhere new.** No wall clock, thread or static mutable state in the core, the motion core or the map loader
  (`NoStaticStateTests`); the host may use a wall clock for pacing. The map load is one tick and deterministic per seed.
- **Runs** (D102): any server, client, bot or bench run lasts at most 15 minutes.

## Things every package should know (contracts findings)

- **Protocol version 3** (`ProtocolVersion.Current` is 3; `MessagesV3.cs`, `ReplicaWorld.V3.cs`): until P5's codec lands the
  bytes are still version 2's (both ends of this build agree). `NetField.Light` (bit 14) is declared and in `Changeable` and
  `All`; `NetField.ChangeableV2` and `AllV2` keep the codec (`Codec.cs` `WriteChange`), the replicator's whitelist and their
  tests on the version-2 set. P5 moves them to `Changeable`/`All` and removes the V2 names, as M2 P5 removed the V1 ones.
  `MapGrid` and `Delta` are `partial` classes now (their version-3 parts in `MessagesV3.cs`); `EntityState.Light` and
  `EntityChange.Light` sit in `Messages.cs` (a partial struct cannot hold fields in two parts without CS0282).
- **The cell flags byte** (`CellFlags`): bits 0-3 the blocked faces with `TileEdges`' values (N 1, S 2, E 4, W 8), bit 4
  opaque, bit 5 starlit space. `MapGrid.FlagRuns` and `BaseLightRuns` are runs over the cells in `Runs`' order; `ExpandFlags`
  and `ExpandBaseLights` are complete (contract tests). Turf type changes extend the MapGrid's turf type table through
  `Delta.NewTurfTypes`.
- **The face API** (`src/Core/Motion/TileFaces.cs`, complete and frozen): `ICollisionView.IsFaceBlocked(x, y, z, dir)` is a
  default interface member over `EdgesOf` (no view overrides it), and M2's `MotionStep` already reads `EdgesOf` on both sides
  in its sweeps, presses and crossings, so face blocking works the moment a view reports faces. The server's
  `LocomotionSeam.FacesOf` (P5) and the replica's `FacesOf` (P5) are the two sources; P5 switches `ServerView.EdgesOf`,
  `ReplicaWorld`'s collision view, the bots' and `OwnPrediction`'s wrappers to them, and pins bit-equality of server and
  predictor on a fixture map with border windows and a windoor.
- **The entity threshold** (D115, DV24): `ReplicatedFields.MaxEntityInvisibility` is 25; P5 switches `IsEntity` from
  `< INVISIBILITY_ABSTRACT` to `<= MaxEntityInvisibility` (M2's world has no movable between 26 and 100, so nothing moves).
- **The inert seam** (D112): `SSatoms.InitAtom` (`game/atom/atoms_initializing_EXPENSIVE.cs`) already calls
  `InertSeam.IsInert(A) ? A.dm_inert_initialize(arguments) : Initialize`. `InertSeam.IsInertType` still returns false
  although P1's `DmInertAttribute` is on main: switched on with the throwing bodies, every inert atom a test creates would
  fail (the create-and-destroy tests make the hand-written but inert skeleton types). **P4 switches it** to
  `type.IsDefined(typeof(DmInertAttribute), inherit: false)` in the commit that fills `dm_inert_initialize` (phase B's
  decision; `InertSeam.cs` is P4's for that line). P2's Meta load needs both: P2 reports it if P4 has not landed. `atom.dm_inert_initialize` and
  its `turf` and `area` overrides (`Tg/Builtin/atom.inert.builtin.cs`) are **P4's** (they share `SETUP_SMOOTHING` with
  `/atom/Initialize`). P4 switched it on (main `f2a5049`): an inert turf runs `/turf/Initialize` through tg's own
  `/turf/open/Initialize` when it is an open turf (its air, `LINDA_turf_tile.dm:59`), through `/turf/open/space/Initialize`
  when it is space; an inert area runs `/area/Initialize` (types-converter.md 7).
- **The live base** (types-converter.md 4.1): every class with a hand-written part at main `68f3a0d` is live by definition,
  so the M0-M2 worlds run as before; types translated from M3 on are live only when every proc of their segment is declared
  (translated, or a `live-partial.txt` stub). P1 freezes the list into `docs/ai/specs/generated/live-base.txt`.
- **The map loading seams** (P2): `MapText.Parse` (no World) gives `ParsedMapText` with `MapValue`s that become DM values only
  through `MapValue.ToDm()` inside the world; `MapVars.TrySet` is the preloader's by-name var set; `MapSeam.Select` is how a
  host picks the map before `TgWorld.Boot` (SSmapping's translated `loadWorld` loads it in `SSmapping.Initialize`, D108: the
  station level only), and `MapSeam.LatejoinSpawns` gives the late-join turfs. `ServerConfig.Map`/`MapsFolder` (`--map`,
  `--maps-folder`, `MAP`, `MAPS_FOLDER`) are parsed; `ServerHost.SpawnPoints` is what the join, the console spawn and the dev
  spawn use (the test room's today; P2 returns the map's late-join spawns when a map is set). `HeldSet` is the area-membership
  index's API: P2's first commit switches `atom`'s held list to it and measures the boot at 128 x 128 and 255 x 255 with
  areas (the scout: 9.0 s at 128 x 128 before).
- **The access dev command** (D117, DV23): `access WHO all|none|ACCESS...` parses (`DevCommandLine`, contract tests); an
  `ACCESS_*` define name becomes its text value; `ServerHost.Dev` calls `DevCommands.Access(target, null for all | list)`,
  which P3 implements with the `/mob` engine field `dm_dev_access` in its new `Tg/Builtin/mob.access.builtin.cs` (a new
  field of every mob moves the toy world's hash: P3 records it).
- **No light dev command on the server**: the light state is tg's (fixtures on through tg's defaults and area power, scope
  2g); the camera flashlight is a client dev toggle (`LightLayer.ToggleFlashlight`, P7; P6 binds the key).
- **The client's static-versus-moving rule** (P6): an entity is a **static prop**, drawn in its chunk's batched billboard
  MultiMesh (atlas UV and direction frame per instance), when it is not a mob, has no `Pos` (no capsule) and has received no
  X/Y/Z change since it spawned; the first position change (or a despawn and respawn) takes it out of the batch for good, as
  a node of M2's `EntityLayer` kind (mobs are always nodes). Doors, windows, grilles, tables, railings, flaps and wall
  fixtures are drawn as geometry by their type (D113), not as billboards; a door's animation follows its appearance's
  `icon_state` (`opening`/`closing`/`deny`) and ends when the cell's solidity flips.
- **The light grid's interface** (P6 and P7, light-grid.md 7): `LightGridUniforms` (names), `res://shaders/light_grid.gdshaderinc`
  (the globals and `vec3 anomaly_grid_light(vec3 world_pos)`, which returned `vec3(0.0)` until P7 filled it; it now returns the grid light), the six globals in
  `project.godot`'s `[shader_globals]` (frozen; P6 owns the rest of the file), `LightLayer` (`Attach`, `SetPreset`,
  `ToggleFlashlight`: no-ops until P7). P6's materials include the file and add `ALBEDO * anomaly_grid_light(world_pos)` to
  `EMISSION`; P6's `GameScene` creates the `LightLayer` and replaces M2's "Sun" and ambient 0.4 with P7's light.
- **FakeFeed version 3** (P6 implements, P7 uses): `FakeMap.Load(path)` reads a converted map with the engine reader (the
  scout's probe output `scratchpad/m3/scout-scratch/probes/meta.as3dmap.b` is a valid file: `source` is optional) and builds
  the version-3 MapGrid and entities from the generated classes' constructor defaults without booting tg;
  `FakeServerOptions.Map` makes the fake server serve it (it throws until P6). P6 and P7 code against it before P2's tool.
- **The toy world's hash** (seed 1) after phase A: `0x7975f279dbfbbf0f`, unchanged from main (nothing new is instantiated).
- **Phase B: the tg skeletons** (the file rows below; written by a one-off script of the contracts step from `types-index.tsv` and `slots.tsv`).
  - Every root proc and global proc of the M3 files that no part declared yet is declared with its `slots.tsv` signature
    (`DmTask`, `async`, exactly where `suspends = yes`) and a `Dm.NotTranslated("<tg file:line>", "<proc>: M3 Pn (skeleton)")`
    body (rules 1.4), in the file's mirror, in a partial class beside the generated var block: 406 roots in 32 files
    (new mirrors: `airlock`, `windowdoor`, `poddoor`, `firedoor`, `access`, `access_helpers`, `mapping_helpers` (the airlock
    helpers only), `power/lighting/light`, `walls`, `floor`, `window`, `grille`, `railings`, `plasticflaps`,
    `spawners/structure`, `spawners/random/_random`, `effects/misc` (the spawner base only), `elements/decals/_decal`,
    `elements/undertile`, `reader`, `change_turf`, `datums/map_config`, `landmarks`; added to the existing `_machinery`,
    `door`, `lighting_atom`, `icon_smoothing`, `tables_racks`, `decal`, `mapping`, `traits`, `zlevel_manager`). No var
    block is hand-written and no `[DmPath]` is added: the generated part keeps it (types-converter.md 3.2, HandProcs).
  - **Overrides are not declared** (as in M2): the inherited code keeps running until the owner translates the override,
    so nothing that runs today changes. A root skeleton the owner decides to leave for a later milestone becomes a
    `Dm.Deferred` body with its milestone, by hand (a root that a hand-written part declares cannot be a `live-partial.txt`
    row).
  - **Left out on purpose:** `controllers/subsystem/machines.dm` (a hand-written part would make `SSmachines` a written
    type, and DV25's subsystem filter would create it at boot with tg's flags, so it would fire `/datum/controller/
    subsystem/fire`'s CRASH: P3 adds it with its translation, `New`, `Initialize`, `fire`, `register_machine`); the
    `GLOBAL_*` initializers of these files (`InitGlobal*` of `/datum/controller/global_vars`: `GLOB.Initialize` calls every
    declared one, so each is translated with its file); the `/world` procs of `preloader.dm` (translated already, as
    `DmWorld` extensions); `shutters.dm`, `reinf_walls.dm`, `space_level.dm` and the turf decal files (no roots); the
    other mapping helpers (air alarm, APC, injectors, holidays).
  - `/obj/machinery/door`'s duplicate `try_to_crowbar_secondary` (`door.dm:434` and `:449`) is `try_to_crowbar_secondary__d0`
    (non-virtual) and the slot (rules 2.5).
  - **What the skeletons changed:** 36 classes went from generated-only to HandProcs (no `[DmGeneratedOnly]`; the
    regenerated outputs); `/datum/map_config` became live (all its procs are roots now declared); no atom type changed
    between inert and live. The types that the create-and-destroy tests now create (door, window, grille, railing, flap
    and table subtypes) reach their tg data's leaf (`initialize_materials`, the door's `EMISSIVE_BLOCK_UNIQUE`): rows in
    `tests/Anomaly.Core.Tests/Tools/Types/VarBlockLeaves.cs`, which the leaf's owner removes. DV25's wrapped sites are
    unchanged (no new hand-written type is a subsystem, language holder, mutation, blood type or species).
  - **`live-partial.txt`'s first rows** (202: P3 99, P4 101, P2 2) list the later-milestone procs of the types M3 makes
    live (airlock, windoor, pod door, the light fixture; window, grille, railing, flaps, wall, floor, iron, plating; the
    landmarks): AI and cyborg clicks, shuttles, tgui, examine, admin and saving, parity-tier interactions (`M10+`), emag,
    tools and items (`not_translated`, M4a), non-human attacks, drag and drop, construction and destruction (M5), wires
    and shocks, EMP, power use and cells (M7), z changes (M4c). What is left of each type's segment is the package's M3
    translation (or more rows): the airlock's `Initialize`, `bumpopen`, `open`, `close`, bolts, power, animation and
    appearance; the window's `CanAllowThrough`, the atmos procs; the walls' and floors' `Initialize`, `broken_states`,
    `burnt_states`, `ChangeTurf`; the landmarks' `New`, `Initialize`, `Destroy`.
  - **The types converter's stubs** were read as hand-written parts on a second run (the stubbed classes lost their base
    and `[DmGeneratedOnly]`); `tools/Anomaly.Tools.Types/HandWritten.cs` now skips `Generated/LivePartial/` as it skips
    `Generated/Types/`, and a second run equals the first (`--check`).
  - The toy world's seed-1 hash after phase B: `0x242eb2a7580781bb`, equal to main's (P1 moved it; the skeletons add only
    methods).

## Files by owner

### Protocol, server, motion, tools

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `src/Protocol/**` (`MessagesV3.cs`, `ReplicaWorld.V3.cs`, `NetField.cs`'s `Light` and V2 names, `Limits.cs`' version-3 caps, `ProtocolVersion.cs`, the version-2 files, a new `Codec.V3.cs`) | P5 | contracts: types, fields, caps and docs complete; the replica's version-3 members throw; the bytes are version 2's | P6, P7, the bots |
| `src/Server/Replication/**` (`ReplicatedFields.MaxEntityInvisibility`, `EntityRuleV3`; the Light row; MapGrid version 3 in the join; the cell changes) | P5 | the threshold declared | P2 (the join with a map) |
| `src/Core/Motion/**` but `TileFaces.cs` (face blocking in `MotionStep`, the predictor) | P5 | M2's, which already reads `EdgesOf` on both sides | P6 |
| `src/Core/Motion/TileFaces.cs`, `ICollisionView.IsFaceBlocked` (in `MotionStep.cs`) | C (frozen) | complete | P5, the bots |
| `Tg/Infrastructure/Locomotion/**` (`FacesOf`, `IsFaceBlocked`, the server view's `EdgesOf`), `Tg/Infrastructure/CellState.cs` | P5 | `FacesOf` and `CellState` throw; `IsFaceBlocked` complete | P2 (tests), the bots |
| `tools/Anomaly.Tools.Bots/**` (door-aware BFS, `tour`, coverage, space avoidance) | P5 | M2's | I1 |
| `src/Server/ServerCodec.cs`, `ClientEntryPoints.cs`, `ServerHost.Network.cs`, `Session.cs`, `Metrics.cs` | P5 | M2's | - |
| `docs/ai/specs/protocol.md`, `replication.md`, `locomotion.md`; `tests/Anomaly.Server.Tests/Protocol/**`, `Replication/**`, `Net/**`, `Bots/**`; `tests/Anomaly.Core.Tests/Motion/**`; `tests/Anomaly.Core.Tests/Tg/M3Locomotion/P5/**` (new) | P5 | M2's (`CodecV2Tests` and `WhitelistTests` moved to the V2 names) | - |
| `src/Server/ServerHost.cs`, `ServerHost.World.cs`, `ServerHost.Console.cs` (world creation with `MAP`, `MapSeam.Select`, `SpawnPoints` from the map), `ServerConfig.cs` (`Map`, `MapsFolder`: parsed), `CommandLog.cs` (records the map), `Program.cs`, `config/**` | P2 | the options parsed; `SpawnPoints` returns the room's | P3, I1 |
| `src/Server/ServerHost.Dev.cs`, `DevCommandLine.cs` (the `access` verb: parsed and dispatched) | P3 | parse complete; `DevCommands.Access` throws | I1 |
| `src/Server/**` else (`InputJitterBuffer.cs`, `Commands.cs`, `TickPacer.cs`, `TokenBucket.cs`, `SmokeLoad.cs`, `LocomotionEntryPoints.cs`) | lead | M2's | - |
| `tools/Anomaly.Tools.Maps/**` (new: the `.dmm`/TGM converter, map-format.md 3), `tools/convert-maps.sh` (new) | P2 | not created | I1, the scripts |
| `tools/Anomaly.Tools.Types/**` (new, outside the solution) | P1 | not created | everyone (regeneration) |
| `tools/soak/**`, `tools/Anomaly.Tools.ToyWorld/**` | I1 | M2's | - |

### The tg tree (`src/Core/Tg/`) and its engine parts

The **Area** a package may create new mirror files in is the folder of its rows.

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `Generated/Types/**`, `Generated/LivePartial/**`, `Generated/TypeOrder.g.cs` (regenerated), `src/Core/DmShim/DmInertAttribute.cs` (new) | P1 | not created (phase B adds the skeletons beside them) | everyone |
| `Builtin/HeldSet.cs` (the membership index), `Builtin/atom.builtin.cs` (the held list becomes a `HeldSet`; nothing else), `Builtin/area.builtin.cs`, `Builtin/MapGrid.cs` (growth for the loader), `Builtin/turf.builtin.cs` | P2 | `HeldSet` declared, throws | P4, P3 |
| `Infrastructure/MapLoader/**` (`MapText.cs`, `MapVars.cs`, `MapSeam.cs`), `Infrastructure/TgWorld.cs`, `Infrastructure/MapView.cs` | P2 | declared, throw | P5, P6 (`FakeMap` uses `MapText`) |
| `modules/mapping/reader.cs` (new: `load_map`, `parsed_map` with `load`, `_load_impl`, `build_coordinate`, `create_atom`, `instance_atom`), `modules/mapping/preloader.cs`, `modules/mapping/map_template.cs`, `modules/mapping/space_management/**` (`space_level`, `zlevel_manager`, `traits`), `controllers/subsystem/mapping.cs` (the station path), `datums/map_config.cs` (new), `game/turfs/change_turf.cs` (new), `game/turfs/turf.cs` (`levelupdate`, `ScrapeAway`, `change_area`), `game/area/areas.cs` (all but the power procs), `game/objects/effects/landmarks.cs` (subset), `modules/lighting/lighting_turf.cs` (`transfer_area_lighting`) | P2 | M2's skeletons, or not created | P3, P4, T2 |
| `game/machinery/_machinery.cs`, `controllers/subsystem/machines.cs` (new), `modules/power/power.cs` (subset: `powered`, `use_energy`), `game/area/areas.power.cs` (the power procs of `areas.dm:492-567`, `powered`, `power_change`, `addStaticPower`, `removeStaticPower`, `clear_usage`, `use_energy`, already translated; the contracts step moved them unchanged out of P2's `areas.cs` into this partial `area` part, `// PARTIAL` header), `game/machinery/doors/**` (`door`, `airlock`, `windowdoor`, `poddoor`, `shutters`, `firedoor` inert or thin), `modules/jobs/access.cs`, `modules/mapping/access_helpers.cs`, `modules/mapping/mapping_helpers.cs` (the airlock helpers, `cyclelink_helper`, `broken_floor`/`burnt_floor`), `modules/power/lighting/**` (`light.dm` and its mapping helpers), `modules/lighting/**` but `lighting_turf.cs` (`lighting_atom.cs`: `set_light*`, `update_light` as `LightChanged`), `datums/wires/**` (stubs), `modules/mob/living/carbon/human/human_helpers.cs` (the human's `GetAccess`), `Builtin/mob.access.builtin.cs` (new: `dm_dev_access`), `Infrastructure/DevCommands.cs` (`Access`), `datums/elements/atmos_sensitive.cs` (new PARTIAL, the lead's assignment of 2026-10-08: only the `/atom` roots `should_atmos_process`, `atmos_expose` and `atmos_end`, which P3's light fixture and windoor and P4's window override; the element is M4); two lines of the lead's `game/atoms_movable.cs` (the `EMISSIVE_BLOCK_UNIQUE` leaves of `/atom/movable/Initialize` and `update_emissive_block`, `atoms_movable.dm:172` and `:284`, made `Dm.Deferred` under DV12 by the lead's decision, with their not-translated rows); one site of the W4.0 shared callees, `initialize_materials` in `game/atom/atom_materials.cs` (`atom_materials.dm:32`, made `Dm.Deferred` until M4a by the lead's assignment of 2026-10-08, with its not-translated row; no material effects in M3); for the abandoned airlock helper (the lead's assignment of 2026-10-08, P2 being finished): `place_on_top` in P2's `game/turfs/baseturfs.cs` (`baseturfs.dm:31`), the `TURF-IDENTITY` replacement of the replaced turf's entries in its area's `turfs_by_zlevel` and `turfs_to_uncontain_by_zlevel` in P2's `game/turfs/change_turf.cs` (object-model.md 7.1's M5 obligation, met for those area lists because the wall helper changes station turfs after the load), the `added_layer` parameter of P2's `place_on_top_react` in `game/area/areas.cs` (`Type`, Appendix A), and the builtin `range` beside P4's `orange` in `Builtin/map.builtin.cs` (sharing its private helpers) | P3 | M2's skeletons (`_machinery.cs` 20 lines, `door.cs` 18, `lighting_atom.cs`); `DevCommands.Access` throws | P2, P4, T1 |
| `__HELPERS/icon_smoothing.cs`, `__DEFINES/icon_smoothing.cs` (`QUEUE_SMOOTH`, `QUEUE_SMOOTH_NEIGHBORS`, `SET_SMOOTHING_GROUPS`), `controllers/subsystem/icon_smooth.cs`, `game/atom/atoms_initializing_EXPENSIVE.cs` (`SETUP_SMOOTHING`; the inert line of `InitAtom` is frozen), `game/atom/_atom.cs` (the smoothing calls in `setDir` and `Destroy`), `Builtin/atom.inert.builtin.cs` (`dm_inert_initialize` of atom, turf, area), `game/turfs/closed/**` (`_closed`, `walls`, `reinf_walls`), `game/turfs/open/floor/**` and `open/floor.cs` (subset), `game/objects/structures.cs`, `game/objects/structures/{window,grille,tables_racks,railings,plasticflaps}.cs`, `game/objects/effects/spawners/structure.cs`, `spawners/random/**`, `game/objects/effects/misc.cs` (the spawner base `/obj/effect/spawner`; the file's other effects come with their milestones), `game/objects/effects/decals/**`, `datums/elements/decals/_decal.cs`, `datums/elements/undertile.cs`, `game/atom/atom_invisibility.cs` (new: the invisibility sources the undertile element sets), the init and undertile lines of the hidden infrastructure (live-partial; new files): `modules/atmospherics/machinery/atmosmachinery.cs`, `modules/atmospherics/machinery/pipes/{pipes,layermanifold,bridge_pipe}.cs`, `modules/power/cable.cs` (with the multilayer hub), `modules/recycling/disposal/{pipe,pipe_sorting}.cs` (with the trunk and the mail sorting junction) and `_globalvars/lists/objects.cs` (`GLOB.cable_list`), `add_blueprints` and `add_blueprints_preround` of `game/turfs/turf.cs` (the infrastructure's init reaches them), `game/turfs/open/_open.cs`'s `update_overlays` (the broken and burnt tile overlays; the lead's assignment of 2026-10-08), `game/turfs/open/dirtystation.cs` and `__HELPERS/_level_traits.cs`'s `is_station_level` (the floors' `MakeDirty`), `__HELPERS/turfs.cs` (new, partial: `valid_build_direction`), `Builtin/map.builtin.cs`'s builtin `view` and its private helpers (OpenDream's `ViewAlgorithm`; the random spawners' locations), `get_line` in P3's `__HELPERS/maths.cs` (the random spawners' unblocked lines), the `update_atom_colour` and `QUEUE_SMOOTH` line of `update_greyscale` in `game/atom/atom_greyscale.cs` (inert atoms with greyscale configs reached a `Dm.NotTranslated` before `SETUP_SMOOTHING`; P2's Meta cast), `game/atom/atom_color.cs` (the atom colours the inert line list and the live map types reach; the lead's assignment of 2026-10-08), `Builtin/map.builtin.cs`'s builtin `orange` and its private helpers (the range square without its centre, OpenDream's order; `QUEUE_SMOOTH_NEIGHBORS` uses it; the lead's assignment of 2026-10-08), `__HELPERS/bitflag_lists.cs` (new: `GLOB.bitflag_lists`, `SET_SMOOTHING_GROUPS`, which tg defines there), `game/objects/structures.cs` (new, partial), `get_smooth_underlay_icon` of `game/turfs/turf.cs` and `game/turfs/closed/_closed.cs` and `SETUP_SMOOTHING` in `turf.cs`'s `Initialize` (icon smoothing reaches them) | P4 | M2's skeletons (`icon_smooth.cs` 84 lines); `dm_inert_initialize` throws | P2, P3, T2 |
| `Infrastructure/InertSeam.cs` | C (frozen API); P4 switches `IsInertType` to the attribute with its bodies | no type inert until then | P4, P1 |
| `game/atoms_movable.cs`, `game/objects/objs.cs`, `Infrastructure/*` not above (`TestRoom`, `ToyRoom`, `ClientSeam*`, `TgState*`, `GlobalProcs.cs`, `Builtins.cs`), `Builtin/**` not above | lead | M2's | all |
| `__HELPERS/*.cs` and `__DEFINES/*.cs` mirrors of the global procs and inline macros M2 P3 left as local copies (`ATAN2`, `capitalize`, `round_timestamp` and the others of the M2 open item): the mechanical cleanup of rules 11.1 | I1 | M2's | all |

### The client

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `godot/scripts/MapRenderer.cs`, `EntityLayer.cs`, `GameScene.cs`, `App.cs`, `CameraRig.cs`, `ConnectScreen.cs`, `DebugHud.cs`, `Hud/**`, `Render/**` (chunks, texture arrays, the static-prop batch, wall mounts, doors, windows, grilles, tables, per-tile ceiling, starfield, occluders, presets, `--bench`), `godot/scripts/Shared/**` but `Shared/Light/**` (`FakeMap.cs`, `FakeFeed.cs`, `FakeServer.cs`, `ClientArgs.cs`, `Icons/TurfKitPlan*`, ...), `godot/scenes/**`, `godot/shaders/**` but `light_grid.gdshaderinc`, `godot/project.godot` but its `[shader_globals]` block, `godot/Anomaly.Client.csproj`, `godot/tools/**` (`smoke.sh` converts the maps too) | P6 | M2's; `FakeMap` declared (throws) | P7 |
| `godot/scripts/Light/**` (`LightLayer.cs`: no-op members), `godot/scripts/Shared/Light/**` but `LightGridUniforms.cs` (the engine-free grid: tg's falloff, cone, LOS, starlight, incremental updates), `godot/shaders/light_grid.gdshaderinc` (the body), `docs/ai/specs/light-grid.md` sections 3-6 | P7 | declared | P6 |
| `godot/scripts/Shared/Light/LightGridUniforms.cs`, `project.godot`'s `[shader_globals]` block | C (frozen) | complete | P6, P7 |
| `tests/Anomaly.Server.Tests/Client/**` but `Client/Light/**` (the chunk and atlas planner, wall-mount placement, the door state machine, `FakeMap`) | P6 | M2's | - |
| `tests/Anomaly.Server.Tests/Client/Light/**` (new) | P7 | not created | - |
| `docs/ai/specs/apps.md` section 2 (2.9 and the M3 renderer) | P6 | M2's | - |

### Tests, specs and the lead

| File or folder | Owner | State | Others use |
|---|---|---|---|
| `tests/Anomaly.Core.Tests/Tools/Types/**` (the generator's tests, the var-block conformance test) | P1 | - | - |
| `tests/fixtures/maps/**` (hand-written TGM/DMM fixtures), `tests/Anomaly.Core.Tests/Tools/Maps/**`, `tests/Anomaly.Core.Tests/Tg/M3Maps/P2/**`, `tests/Anomaly.Server.Tests/M3Maps/P2/**` (the server's map world, added by P2), `tools/Anomaly.Tools.Maps/Tests/**` (the converter's tests) | P2 | - | T2 (reads the fixtures, adds its own under `tests/fixtures/maps/t2/`) |
| `tests/Anomaly.Core.Tests/Tg/M3Doors/P3/**` | P3 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M3Structures/P4/**` | P4 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M3Doors/T1/**` (DM-behaviour tests of P3: the bump gate, `req_access`/`req_one_access`, bolts, the 0.5 s and 0.2 s density timing, autoclose, the helpers' payloads; ported `door_access`, `interaction_door` or deferred, `wallmount`, in `PortedTgTests.cs`) | T1 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M3Maps/T2/**`, `Tg/M3Structures/T2/**`, `tests/fixtures/maps/t2/**` (P2 and P4 behaviour: smoothing junctions, window face blocking, spawner output; ported `area_contents`, `mapload_space_verification`, `baseturfs`, `map_landmarks`, `mapping`, `required_map_items` subset, `turf_icons`) | T2 | - | - |
| `tests/Anomaly.Server.Tests/Integration/**` (M3 end-to-end), `tests/Anomaly.Core.Tests/Tg/M3Integration/**`, `docs/ai/specs/apps.md` section 4 (M3), the Windows export and run sheet | I1 | M2's | - |
| `tests/Anomaly.Core.Tests/M3Contracts/**`, `tests/Anomaly.Server.Tests/M3Contracts/**` | C | complete | all |
| `docs/ai/specs/types-converter.md` | P1 (sections 1-3); sections 4-7 are contracts | skeleton | all |
| `docs/ai/specs/map-format.md` | P2 keeps it true to the tool; the format is a contract | complete | P6 (`FakeMap`) |
| `docs/ai/specs/live-partial.txt` | each package its own section (`## P2`, `## P3`, `## P4`, `## I1`); the file's format is a contract | sections empty (phase B adds the M3 rows) | P1's generator |
| `docs/ai/specs/m3-ownership.md`, `DEVIATIONS.md`, `translation-rules.md`, `not-translated.md`, `DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, solutions, `.gitignore` | lead | - | all |

T1 and T2 may fix defects in their packages' files (`Fix translation:` commits) after those packages merged.

P2 edited one test file outside its rows, with the lead's agreement at the merge: `tests/Anomaly.Core.Tests/Tg/Wave3/Atoms/AtomDefaultsTests.cs`
(the lead's), whose preloader test asserted the untranslated `vars[]` write (a `Dm.NotTranslated` runtime) and now asserts the
var is set (`preloader_load` through `MapVars.TrySet`, M3 P2). P2 also put tg's ported map tests (`area_contents`,
`mapload_space_verification`, `mapping`, the `map_landmarks` subset, the `baseturfs` unmodified scrape) in
`Tg/M3Maps/P2/` as its package file asked; T2 extends them in its own folder.

## The interfaces each package implements or calls

| Contract | File | Implemented by | Called by |
|---|---|---|---|
| The codec's version-3 layout (MapGrid's flags, base lights and starlight; the Delta's cell, base-light, turf-type and starlight records; `NetField.Light` in entity records), behind the existing entry points (`Codec.Encode`/`TryDecode` of `MapGrid`, `Snapshot`, `Delta`; `EncodeDeltaBody`, `EncodeDelta`) | `src/Protocol/Codec*.cs` | P5 | the server, the client, the bots |
| `ReplicaWorld`'s version-3 members (`CellFlagsAt`, `FacesOf`, `IsOpaque`, `IsStarlit`, `BaseLights`, `BaseLightIndexAt`, `Starlight`, `TurfTypes`, `TurfTypeAt`, `LightChanged`, `StarlightChanged`; `ReplicaEntity.Light`, `HasLight`) | `src/Protocol/ReplicaWorld.V3.cs` | P5 | P6, P7, the bots |
| `LocomotionSeam.FacesOf`; `ServerView.EdgesOf` through it | `Tg/Infrastructure/Locomotion/LocomotionSeam*.cs` | P5 | `MotionStep`, the replicator |
| `CellState.IsOpaque`, `IsStarlit`, `BaseLightOf`, `Starlight` | `Tg/Infrastructure/CellState.cs` | P5 | the replicator |
| `ReplicatedFields.MaxEntityInvisibility` in `IsEntity`; the `Light` row | `src/Server/Replication/ReplicatedFields.cs` | P5 | the replicator |
| `ICollisionView.IsFaceBlocked`, `TileFaces` | `src/Core/Motion/MotionStep.cs`, `TileFaces.cs` | C (complete) | P5, the bots, tests |
| `MapText.Parse`, `MapValue.Parse`, `MapValue.ToDm`, `ParsedMapText.ModelAt`, `Lines` | `Tg/Infrastructure/MapLoader/MapText.cs` | P2 | P2's reader, P6's `FakeMap`, the converter's tests |
| `MapVars.TrySet` | `Tg/Infrastructure/MapLoader/MapVars.cs` | P2 | P2's preloader translation |
| `MapSeam.Select`, `Selected`, `LatejoinSpawns`; `MapSelection` | `Tg/Infrastructure/MapLoader/MapSeam.cs` | P2 | the server host (P2), I1, tests |
| `HeldSet` (the area-membership index) | `Tg/Builtin/HeldSet.cs` | P2 | the atom engine part |
| `ServerConfig.Map`, `MapsFolder`; `ServerHost.SpawnPoints` | `src/Server/ServerConfig.cs`, `ServerHost.cs` | C (parse, room spawns), P2 (the map world) | the host, I1 |
| `InertSeam.IsInert`, `IsInertType` | `Tg/Infrastructure/InertSeam.cs` | C (the API), P4 (the switch to the attribute, with `dm_inert_initialize`) | `SSatoms.InitAtom` |
| `atom.dm_inert_initialize` (and the turf and area overrides) | `Tg/Builtin/atom.inert.builtin.cs` | P4 | `SSatoms.InitAtom` |
| `[DmInert]` (`DmInertAttribute`), the generated classes, `live-partial.txt` stubs, `types-index.tsv`, `live-base.txt` | `src/Core/DmShim/DmInertAttribute.cs`, `Tg/Generated/**` | P1 | everyone |
| `DevCommands.Access` and the `dm_dev_access` field read by `GetAccess` | `Tg/Infrastructure/DevCommands.cs`, `Tg/Builtin/mob.access.builtin.cs` | P3 | `ServerHost.Dev` |
| `LightGridUniforms`, `light_grid.gdshaderinc`'s `anomaly_grid_light`, the `[shader_globals]` | `godot/scripts/Shared/Light/LightGridUniforms.cs`, `godot/shaders/`, `godot/project.godot` | C (names), P7 (the body and the values) | P6's materials |
| `LightLayer.Attach`, `SetPreset`, `ToggleFlashlight` | `godot/scripts/Light/LightLayer.cs` | P7 | P6's `GameScene` |
| `FakeMap` (`Load`, `Parse`, `Source`, `MapGrid`, `Entities`, `SpawnCells`), `FakeMapEntity`, `FakeServerOptions.Map` | `godot/scripts/Shared/FakeMap.cs`, `FakeServer.cs` | P6 | P6, P7, their tests |
| every tg skeleton (phase B; `slots.tsv` signatures, `DmTask` exactly where `suspends = yes`) | the mirror files above | the file's owner | every package |

## `live-partial.txt` sections

| Section | Owner | Expected rows (phase B lists the first ones) |
|---|---|---|
| `## P2 (M3)` | P2 | landmark types that must be live with untranslated procs (the start and late-join landmarks' non-M3 procs) |
| `## P3 (M3)` | P3 | the airlock's wires, shocks, AI control, emag, crowbar, welding, seals, notes and sounds; the windoor's and the door's equivalents; `/obj/machinery`'s UI, `RefreshParts`, `deconstruct`, EMP; the light fixture's construction, breaking and nightshift; the access helpers' non-payload procs |
| `## P4 (M3)` | P4 | the window's, grille's and table's construction and destruction (M5), the pipes', cables' and disposals' procs other than the undertile lines (M4b, M7, M8), the spawners' non-loot procs |
| `## I1 (M3)` | I1 | whatever the integration finds |
