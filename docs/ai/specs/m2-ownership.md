# M2 file ownership

Status: v1 (the M2 contracts step, M2.0), 2026-10-07. Who edits which file while the M2 packages work in parallel (the lead's
plan, the scout's `scope.md` section 3): **P1** the living core, **P2** carbon, human, body and breath, **P3** appearance and
HUD state, **P4** the motion core, the bridge and the clicks, **P5** protocol version 2 and replication, **P6** the server
host, the test room, the dev commands and the bots, **P7** icons and rendering, **P8** client play, and the testers **T1**
(medical, after P1 and P2) and **T2** (locomotion and appearance, after P3 and P4), then the lead's **integration** step.
The contracts commits declared everything two packages share, so that every package compiles against the others'
declarations and edits only its own files. Paths are repository paths; `Tg/` is `src/Core/Tg/`.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new files inside the folders its rows
  give it, and new mirror files of tg files that no row names inside its own tg area (the "Area" column of the tg table),
  and lists them in its report. A file that no row names is not edited: report the needed member to the lead (a
  `// GAP(<owner>): ...` comment at the call site), or, inside the Tg tree, declare what you need in a new file of your own
  only when rules 2.3 put it there.
- **Contracts are frozen.** Every public type, member and signature the contracts step wrote (the Motion API, the protocol
  version 2 types and codec entry points, `ReplicaWorld`'s version-2 API, `IReplicator`'s version-2 members,
  `InputJitterBuffer`, `QueuedCommand`'s M2 fields, `ServerConfig.DevCommands` and `MoveSpeedScale`, `LocomotionSeam`,
  `ClientSeam`'s action and click entry points, `DevCommands`, the client seams `IPoseSource`, `IAppearanceTextures`,
  `IWorldFeed`'s version-2 members, the icon tool's command line, `IDmAppearance`, and every tg skeleton with its `slots.tsv`
  signature) changes only through the lead. The owner fills the bodies (`throw new NotImplementedException("M2 Pn")` in new
  logic, `Dm.NotTranslated` in tg translation, rules 1.4), may add private members and new types, and may add public
  members a contract does not have (it reports them).
- **The meaning in the doc comments is the contract**: the units and invariants of `Anomaly.Core.Motion` (tiles, ticks, 1/256
  tile quantization, the capsule centre's tile equals `loc` at the end of every tick, only `+ - * / sqrt` on floats), the
  private block's D40 rule (public versus private tables), the order of the locomotion phase, the jitter buffer's rules.
  A package that finds one wrong for the real code reports it before working around it.
- **Overrides are not declared** (as in wave 4 and M1). The skeletons declare the roots of the slots their files define;
  an override (`/mob/living/carbon/human/Initialize`, `/mob/living/Life`, `/mob/living/carbon/updatehealth`, the keybindings'
  `down`, the status effects' `on_apply`, ...) is added by the owner of its file, so that the base keeps running until then.
  A root whose `Dm.NotTranslated` text says "not in M2 (...)" is outside the scout's translate-now list; its owner may
  translate it all the same when the M2 path needs it (for example `set_combat_mode` for the combat-mode toggle).
- **Bodies throw until their owner lands.** A test that reaches another package's skeleton fails, as intended. Test your
  own code with your own fixtures. The M2 paths run with `DmWorldState.NotTranslatedThrows = true` in tests, so no leaf is
  reached silently. **Merge order: P5, P4, P1 (T1 starts after P2), P2, P3 (T2 starts after P3 and P4), P6, P7, P8**, then
  T1 and T2 as they finish, then the integration (Opus, with Sonnet helpers).
- **Specs.** P5 writes `docs/ai/specs/protocol.md` (version 2) and `docs/ai/specs/replication.md` (appearance interning, positions,
  private blocks, MapGrid version 2); P4 writes a new `docs/ai/specs/locomotion.md` (the controller, the bridge, prediction, DV15
  to DV17 in practice); P6 rewrites `docs/ai/specs/apps.md` section 1 (the server); P8 rewrites section 2 (the client) except
  a subsection "2.9 Rendering" at its end, which P7 writes (P8 does not edit it, P7 nothing else of the file); the
  integration writes section 3.
- **Shared documents** (`docs/ai/DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, `DEVIATIONS.md`, `docs/ai/translation-rules.md`
  Appendix A and C, `docs/ai/specs/object-model.md`, `presentation-seam.md`, `waiting-model.md`, `shim-ownership.md`,
  `dm-conformance.md`, `not-translated.md`, this file): the lead at merge; packages report additions (a new deviation with its
  `// DEVIATION(DVn)` text, a decided slot, a new public name of the Kernel, the shim or the Tg infrastructure, a deferred
  leaf's row). DV15, DV16 and DV17 are written (D105); their code carries `// DEVIATION(DVn)` at the sites.
- **The shim, the Kernel and the atom engine parts** (`src/Core/DmShim/`, `src/Core/Kernel/`, `Tg/Builtin/`) are not edited
  by the packages, with the exceptions in the table (P3's overlay snapshot); a missing member is reported
  (`// SHIM-GAP: ...` at the site).
- **The solutions and project files** (`AnomalyStation.sln`, `godot/Anomaly.Client.sln`, every `.csproj`,
  `Directory.Build.props`) are the lead's, except `godot/Anomaly.Client.csproj` (P8), `tools/Anomaly.Tools.Bots/*.csproj` (P6) and
  `tools/Anomaly.Tools.Icons/*.csproj` (P7). A package that needs a new project or package reference asks the lead.
- **D31 everywhere new.** No wall clock, thread or static mutable state in the core or the motion core
  (`CoreHasNoWallClockTests`, `NoStaticStateTests`); the host may use a wall clock for pacing and its input clock; whatever
  enters the world is a command applied at a tick in a deterministic order (a frame the jitter buffer gives is a recorded
  command of its tick). The locomotion seam's state is world state and hashed.

## Things every package should know (contracts findings)

- **The motion core** (`src/Core/Motion`, `Anomaly.Core.Motion`) references nothing and compiles optimized in every
  configuration, like the core. Positions are tiles with the tile centres at whole numbers (a tile x covers
  [x - 0.5, x + 0.5)), stored as 1/256-tile integers (`MotionMath.Quantize`, `TileOf`); time is ticks; yaw is a ushort compass
  bearing (0 north, clockwise). Protocol, Tg, the server, the bots, the Godot client and the tests reference it; the wire
  carries `MotionInput`, `MotionState` and `MotionParams` as they are (no copy types).
- **Protocol version 2** (`ProtocolVersion.Current` is 2; `MessagesV2.cs`, `Codec.V2.cs`, `ReplicaWorld.V2.cs`): until P5's
  codec lands, the bytes are still version 1's (both ends of this build agree). `NetField` is a `uint` mask with `Pos`, `Yaw`,
  `Posture`, `Appearance`; `NetField.ChangeableV1` and `AllV1` keep the M1 codec, replicator and their tests on the version-1
  set; P5 moves them to `Changeable` and `All` and removes the V1 names. Message ids 9 (`MotionInput`) and 10 (`Action`) are
  declared; `WireReader.IsKnownMessageId` still says 1 to 8 until P5.
- **D40 and the HUD.** The private block (`OwnBlock`) carries the session's own tables: what only its owner may see (the
  health doll's per-zone states, the health indicator, alerts) is interned into private string, resource and appearance
  tables (`TableRef.Private`), never into the public ones, so no other session's bytes reveal a health level. P5's
  no-hidden-data test covers it.
- **Appearances.** The replicator reads appearances through `Anomaly.Core.DmShim.IDmAppearance` (the `/atom` engine part, `image`,
  `mutable_appearance`, and P3's overlay snapshot implement it). Until P3's snapshot lands, `overlays` hold what tg added
  (`mutable_appearance` and `image` objects, icon-state text): P5 interns those by their fields, and P3's snapshots are
  `IDmAppearance` too. `atom.name` is a hinted engine property (`AppearanceAspect.Name`); `atom.transform` reads give
  copies (BYOND), the `IDmAppearance` view gives the stored matrix.
- **The locomotion phase** runs after the host's command phase (`LocomotionSeam.Install` right after `world.AddPhase("player
  commands", ...)`): the command phase applies the Motion commands (`LocomotionSeam.SetFrame`) and the Actions, then the
  phase steps every registered capsule mob in `dm_id` order. `LocomotionSeam.IsSolid` is the one solidity predicate:
  P4's controller and P5's MapGrid version 2 use it. `LocomotionSeam.TryGetState` is read outside the world after the step
  for the private block.
- **Actions.** `ClientSeam.Rest`, `ToggleMoveIntent` (tg's `toggle_move_intent_alternative`: press to cycle) and
  `ToggleCombatMode` are implemented: tg's keyDown path for one keybinding (`can_use`, `down`), with one keybinding datum
  per type per world until `GLOB.keybindings_by_name` (M9). They stop at the keybinding skeletons until P1. `ClientSeam.Click`
  is P4's; `ClientSeam.ClickParams` builds tg's params text.
- **The human's init path** has no throwing boundary left of the four the scout found: `grant_language`, `visible_message`
  (both), `do_attack_animation` and `drop_all_held_items` are deferred leaves (`docs/ai/specs/not-translated.md`).
  `M2Seams/HumanSpawnTests` spawns a human and a `human/consistent` with `NotTranslatedThrows` on; it runs the `/mob` chain
  today and must stay green as P1 and P2 add the human's overrides (every `Dm.NotTranslated` they leave on the path fails
  it). Known boundaries still ahead of the real init path (scope 2e): `setup_mood` (M9, keep `mob_mood` null),
  `randomize_human_normie` and `GLOB.species_prototypes` (narrow to the human, Q7), the empty reagent holder (M8),
  `breathing_loop`, `prepare_data_huds` and the HUD adds, the crafting, strippable, ridable, footstep and bloodysoles
  elements, the second `/obj/item/organ` var block of `organs/external/_visual_organs.dm:6` (`bodypart_overlay`, read by the
  organ's `Initialize`), and `/datum/species/var/inert_mutation`'s default (`/datum/mutation/dwarfism`: declaring it gives
  `/datum/mutation` a subtype and makes `SSatoms.setupGenetics` call `generate_gene_sequence`, so it waits for P2's genetics
  stub).
- **The var blocks are translated in full** (contracts deliverable 6): `/mob/living`, `/mob/living/carbon`, `/mob/living/
  carbon/human`, `/datum/species` and `/datum/species/human`, `/datum/dna`, `/obj/item`, `/obj/item/bodypart` with the head,
  chest, arms and legs, `/obj/item/organ` with the 9 human organs and the smoker lungs, `/datum/gas_mixture`, `/datum/gas` and
  its 20 gases, the immutable mixtures, `/datum/status_effect` and the M2 effects, `/datum/keybinding` and the four living
  bindings, the 50 movespeed modifiers of `mobs.dm`, `/datum/blood_type` and the human blood types, `/datum/hud` and
  `/datum/hud/human`, `/atom/movable/screen` with the health doll, the health indicator, the walk/run button, the
  fullscreens and the alerts, `SSmobs`' vars and `movetype_handler`. Their tests are `tests/Anomaly.Core.Tests/M2Contracts/
  VarBlocks/`. The owner of a file fixes a var it finds wrong and reports it.
- **Three gaps the var blocks left** (reported, not blocking): `examine_thats` (an `/atom` var of `game/atom/atom_examine.dm`,
  which has no mirror; `human_defines.cs`), the keybinding defines `CATEGORY_MISC`, `CATEGORY_HUMAN`, `WEIGHT_LOWEST` and
  `WEIGHT_MOB` (global `#define`s of `datums/keybinding/_defines.dm`, outside `__DEFINES`, which the defines converter does
  not emit: their values are written with GAP comments in `datums/keybinding/*.cs`), and `inert_mutation` above.
- **The toy world's hash moved** (seed 1): `0xd045d48cfe847de7` on main, then `0x6d867f433b4ffffe` with the shim's image
  vars (the toy world holds images), `0x21a2dba3f72bcc62` with the `SSmobs` and `SSpriority_effects` stubs, `0x295820c62c13170a`
  with the `name` property, and `0x536c05a38057eec2` with the var blocks and the three BYOND mouse vars of `/atom` (`mouse_over_pointer`, `mouse_drop_pointer`, `mouse_drop_zone`: every atom hashes them), deterministic over two runs. A package that adds world content
  (new instances, subsystems, fields of instantiated types) records the new value and why.

## Files by owner

### The motion core, protocol, server, tools and client

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `src/Core/Motion/**` (`MotionTypes.cs`: `MotionState`, `MotionInput`, `MotionParams`, `Posture`, `MotionDriver`, `Slide`, `MotionDir`, `MotionMath`; `MotionStep.cs`: `ICollisionView`, `TileEdges`, `CapsuleObstacle`, `MotionEvent(s)`, `MotionStep`; `MotionPredictor.cs`: `MotionPredictor`, `Reconciliation`, `CorrectionKind`, `PredictionCounters`, `InputClock`) | P4 | contracts: the types and `MotionMath` complete; `Step`, `ClampToTile`, `Displace`, the predictor and the input clock throw | P5, P6, P8, T2 |
| `tests/Anomaly.Core.Tests/Motion/**` (new files) | P4 | `MotionContractTests.cs` (lead, complete) | - |
| `docs/ai/specs/locomotion.md` (new) | P4 | not created | all |
| `src/Protocol/**` (`MessagesV2.cs`, `Codec.V2.cs`, `ReplicaWorld.V2.cs`, the version-2 fields of `Messages.cs`, `NetField.cs`, `Limits.cs`, `MessageId.cs`, `ProtocolVersion.cs`; the M1 files) | P5 | contracts: types, fields, limits and doc complete; the version-2 codec and replica members throw | P6, P7, P8 |
| `src/Server/Replication/**` (`IReplicator.cs`: `PrivateInputs`, `PrivateFrame`, `BuildPrivate`, the version-2 `BuildJoin`; `Replicator.cs`, `ReplicatedFields.cs`, `ReplicatorOptions.cs`) | P5 | the version-2 members are default members that throw | P6 |
| `tests/Anomaly.Server.Tests/Protocol/**`, `Replication/**`, `Net/**`; `docs/ai/specs/protocol.md`, `docs/ai/specs/replication.md` | P5 | M1's (the version-1 tests adjusted to `ProtocolVersion.Current` and the V1 field sets) | - |
| `src/Server/**` except `Replication/` (`InputJitterBuffer.cs`; `Commands.cs`: `CommandKind.Motion`, `Action`, `QueuedCommand`'s M2 fields; `ServerConfig.cs`: `DevCommands`, `DevCommandsEnabled`, `MoveSpeedScale`; `ServerCodec.cs` and `ClientEntryPoints.cs`: the version-2 default members; `ServerHost*`, `Session`, `CommandLog`, `Program`, ...) | P6 | contracts: the jitter buffer throws; the config options exist, parsing them is P6's; the M1 host runs unchanged (version-1 path, D98's pace still on until P6 removes it) | - |
| `Tg/Infrastructure/TestRoom.cs` (the `rooms` layout and its air), `Tg/Infrastructure/DevCommands.cs` (the bodies on tg's procs) | P6 | `DevCommands` throws | the integration |
| `config/**`, `tools/Anomaly.Tools.Bots/**`, `tools/soak/**`, `tests/Anomaly.Server.Tests/Server/**`, `Bots/**`; `docs/ai/specs/apps.md` section 1 | P6 | M1's | - |
| `tools/Anomaly.Tools.Icons/**` (`IconToolOptions`, `IconConverter`, the csproj) | P7 | the command line complete (`IconToolOptions.TryParse`, tested by the lead's `tests/Anomaly.Core.Tests/Tools/IconToolOptionsTests.cs`); `IconConverter.Run` throws | the smoke and soak scripts |
| `godot/scripts/Render/**` (`IAppearanceTextures.cs`: `IAppearanceTextures`, `ComposedAppearance`; the IconAtlas, the compositor, GAGS, billboards, lying quads, the turf kit), `godot/scripts/Shared/Icons/**` (new: the engine-free atlas metadata and composition logic), `godot/scripts/EntityLayer.cs`, `godot/scripts/MapRenderer.cs`, `godot/tools/smoke.sh` (runs the icon tool first), `tests/Anomaly.Server.Tests/Client/Render/**` and `tests/Anomaly.Core.Tests/Tools/Icons/**` (new); apps.md subsection 2.9 | P7 | the interface; M1's entity layer and map renderer | P8 (the HUD draws screen objects with `IAppearanceTextures`) |
| `godot/**` else: `godot/scripts/GameScene.cs`, `CameraRig.cs`, `App.cs`, `ConnectScreen.cs`, `DebugHud.cs`, new `godot/scripts/Hud/**` and `godot/scripts/Play/**`, the scenes (`scenes/*.tscn`: P7 adds its nodes from code or asks P8), `godot/scripts/Shared/**` but `Icons/` and `IPoseSource.cs` (`IWorldFeed.cs` with its version-2 members, `GameClient.cs`, `FakeFeed.cs` version 2, `ViewInput.cs`, `RenderClock.cs`, ...), `godot/Anomaly.Client.csproj` (keeps the Motion, Protocol and Net references and the ExportRelease mapping), `tests/Anomaly.Server.Tests/Client/**` but `Render/`; apps.md section 2 but 2.9 | P8 | M1's client with the feed's version-2 default members (they throw) | P7 |
| `godot/scripts/Shared/IPoseSource.cs` (`IPoseSource`, `RenderPose`) | contracts (frozen) | complete | P8 implements, P7 calls |

### The tg tree (`src/Core/Tg/`): mirror files and their areas

The **Area** a package may create new mirror files in is the folder of its rows. Type-only skeletons that the var blocks
created in other folders (food, stacks, tanks, reagents, actions, materials, ... under "Type-only" below) belong to the
package whose var block named the type, which may add the members it needs there.

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `modules/mob/living/*.cs` (top level, not `*update_icons*`): `living_defines.cs` (the var block, complete), `living.cs`, `damage_procs.cs`, `status_procs.cs`, `init_signals.cs`, `blood.cs`, `blood_types.cs` (var blocks complete), `living_defense.cs` (`disarm`, `can_disarm`: DV17), `death.cs`, `life.cs`, `living_movement.cs` | P1 | skeletons: the roots of the files, `Dm.NotTranslated` | P2, P3, P4, T1 |
| `datums/status_effects/**`, `modules/movespeed/**`, `datums/keybinding/**`, `datums/elements/movetype_handler.cs`, `controllers/subsystem/mobs.cs` (`SSmobs`, an inert stub with its vars), `controllers/subsystem/processing/priority_effects.cs` (`SSpriority_effects`, an inert stub) | P1 | var blocks complete; roots skeletons; the two stubs `STUB(M2)` | P2, P4, the action entry points |
| `modules/mob/mob_movement.cs` (M1's `client/Move` and `/mob/proc/toggle_move_intent`), `modules/mob/mob.cs` (its `visible_message` stays a deferred leaf until M9) and `mob_defines.cs` where the living path needs a `/mob` member, `modules/mob/inventory.cs` (`drop_all_held_items` stays deferred) | P1 | M1's | P2, P4 |
| `modules/mob/mob_movement.capsule.cs` (new: `client/capsule_move_gate`, the client/Move tail after a crossing: DV15), `modules/client/client_procs.click.cs` (new: tg's `/client/Click`, `client_procs.dm:866`) | P4 | not created | - |
| `modules/mob/living/carbon/**` (not `*update_icons*`): `carbon_defines.cs`, `human/human_defines.cs` (var blocks complete), `carbon.cs`, `damage_procs.cs`, `life.cs`, `init_signals.cs`, `status_procs.cs`, `human/human.cs`, `life.cs`, `death.cs`, `init_signals.cs`, `status_procs.cs`, `human_defense.cs` (`disarm_precollide`, `disarm_collision`), `human_helpers.cs` (`update_visible_name`), `_species.cs` (the var block complete; `spec_attack_hand`'s shove and help branches), `species_types/humans.cs`, `dummy.cs` (`human/consistent`); `datums/dna/**`; `modules/surgery/**` but `bodyparts/_bodyparts.icons.cs` and `bodyparts/head_hair_and_lips.cs` (`_bodyparts.cs` with the var block, `default_parts.cs`, `head.cs`, `dismemberment.cs`, `helpers.cs`, the organs' files: var blocks complete); `modules/mob/living/brain/brain_item.cs`; `modules/atmospherics/gasmixtures/**` (var blocks complete), `modules/atmospherics/environmental/LINDA_turf_tile.cs` (`assume_air`, `remove_air`, `return_air`), `controllers/subsystem/air.cs` (`parse_gas_string`: the stub keeps its flags); `game/objects/items.cs` (the `/obj/item` var block complete); `modules/admin/create_mob.cs` (new: `randomize_human_normie`), `modules/reagents/chemistry/holder/holder.cs` (new: the empty holder `death` reads) | P2 | var blocks complete; roots skeletons | P1, P3, P4, T1 |
| `modules/mob/living/living_update_icons.cs`, `carbon/carbon_update_icons.cs`, `carbon/human/human_update_icons.cs`, `modules/surgery/bodyparts/_bodyparts.icons.cs` (the appearance procs of `_bodyparts.dm`: `get_bodypart_damage_state`, `update_limb`, `update_draw_color`, `get_limb_icon`, the overlays and textures), `bodyparts/head_hair_and_lips.cs`, `datums/mutable_appearance.cs` (tg's `mutable_appearance()` proc), `__HELPERS/matrices.cs` (tg's `/matrix` procs as `GlobalProcs` extension members, receiver `src`), `_onclick/hud/**` (`hud.cs`, `human.cs`, `living.cs`, the screen objects with their var blocks complete, `alert.cs`), `game/objects/items/devices/scanners/health_analyzer.cs` (`healthscan`) | P3 | var blocks complete; roots skeletons | P1, P2, P5, P8, T2 |
| the overlay snapshot (BYOND: an overlay is a snapshot of the item added; dm-conformance "M2 contracts"): new files `src/Core/DmShim/Appearance*.cs` (the snapshot type implementing `IDmAppearance`, its per-world interning) and `Tg/Builtin/atom.overlays.builtin.cs` (the conversion of what tg adds to `overlays` and `underlays`), plus one hook in `src/Core/DmShim/DmList.cs` (an item conversion for engine lists, reported to the lead with its tests) | P3 | not created | P5 |
| `_onclick/click.cs` (`ClickOn`, `changeNext_move`, `/atom/Click`), `_onclick/other_mobs.cs` (`UnarmedAttack`, `right_click_attack_chain`, `attack_hand`), `Tg/Infrastructure/Locomotion/**` (`LocomotionSeam`: the phase, registration, frames, state, speed scale, `IsSolid`), `Tg/Infrastructure/ClientSeam.Click.cs` | P4 | skeletons and contracts that throw | P6, T2 |
| `Tg/Infrastructure/ClientSeam.Actions.cs` (rest, walk/run, combat mode, `ClickParams`), `Tg/Infrastructure/ClientSeam.cs` (M1's, made partial) | contracts (complete) | complete; the keybinding procs it calls are P1's | P6 |
| Type-only skeletons the var blocks needed: `datums/actions/**`, `armor/`, `bodypart_overlays/`, `brain_damage/`, `elements/immerse.cs`, `embedding.cs`, `looping_sounds/**`, `martial/`, `materials/`, `mood.cs`, `outfit.cs`; `game/machinery/washing_machine.cs`, `game/objects/effects/**` (new files), `game/objects/items/hand_items.cs`, `stacks/sheets/leather.cs`, `tanks/tanks.cs`, `game/turfs/open/water.cs`; `modules/food_and_drinks/**`, `modules/language/_language.cs`, `modules/reagents/chemistry/reagents/**`, `modules/spatial_grid/cell_tracker.cs`, `modules/surgery/organs/external/wings/wings.cs`, `bodyparts/species_parts/misc_bodyparts.cs`, `stumps.cs`, `worn_feature_offset.cs`, `modules/mob/living/carbon/carbon_defense.cs`, `Builtin/particles.builtin.cs`; the type-only classes appended to `_onclick/other_mobs.cs`, `__HELPERS/names.cs`, `modules/language/_language_holder.cs`, `game/objects/effects/effects.cs`, `modules/mob/living/living.cs` | the package of the area that named them (P2 for the body, food, reagent, stack, tank and language types; P1 for mood, actions, cell tracker, particle holder, water, washing machine; P3 for the HUD's); the lead for `Builtin/particles.builtin.cs` | type only | - |
| `game/atoms_movable.cs` (`do_attack_animation` and `grant_language` are deferred leaves; a package that needs another `/atom/movable` member reports it) | lead | M0 translation plus the deferred leaves | all |

### Tests, the shim and the lead

| File or folder | Owner | State | Others use |
|---|---|---|---|
| `tests/Anomaly.Core.Tests/Tg/M2Medical/P1/**` (P1's own tests), `Tg/M2Medical/P2/**` (P2's), `Tg/M2Appearance/P3/**` (P3's), `Tg/M2Locomotion/P4/**` (P4's tests of the bridge and the clicks). The contracts named them `Tg/M2Living`, `Tg/M2Body`, `Tg/M2Appearance` and `Tg/M2Bridge`; the packages put them under the tester's folder of their area, as listed here (M2 integration) | P1, P2, P3, P4 | complete | - |
| `tests/Anomaly.Core.Tests/Tg/M2Medical/T1/**` (DM-behaviour tests of P1 and P2, the tg tests of scope 2g: `mob_damage`, `stuns`, `oxyloss_suffocation`, `full_heal`, `status_effect_validity`, `spawn_humans`, in `PortedTgTests.cs` (rules 15: a tester's ported tg tests), the crit and death timings) | T1 | complete | - |
| `tests/Anomaly.Core.Tests/Tg/M2Locomotion/T2/**` (the bridge goldens against `move-chain.md` T1 and T2, `disarm` and `combat_help`), `Tg/M2Appearance/T2/**` (the appearance goldens), `tests/Anomaly.Server.Tests/M2T2/**` | T2 | complete | - |
| `tests/Anomaly.Core.Tests/M2Seams/**`, `M2Contracts/**`, `Motion/MotionContractTests.cs`, `Tools/IconToolOptionsTests.cs`, `Contracts/**` | lead | complete | all |
| `tests/Anomaly.Server.Tests/Integration/**` (the end-to-end and headless prediction tests), `ProjectsLoadTests.cs`, `tests/Anomaly.Core.Tests/Tg/M2Integration/**` (the integration's own core tests) | the integration | complete | - |
| `src/Core/DmShim/**` (`IDmAppearance.cs`, `image.cs` with the appearance vars, `mutable_appearance.cs`, `icon.cs` (a type only), `Dm.ListMath.cs` (`values_sum`, `values_dot`, `values_cut_under`, `values_cut_over`, `values_product`), `Presentation.cs` (`AppearanceAspect.Name`)), `src/Core/Kernel/**`, `Tg/Builtin/**` (`atom.builtin.cs`: the `name` property, `transform`'s copy semantics, `IDmAppearance`, the mouse pointer vars) except P3's two new files, `Tg/Infrastructure/*` except the files above, `Tg/Generated/**` | lead | complete | all |
| `AnomalyStation.sln`, `godot/Anomaly.Client.sln`, the `.csproj` files but P6's, P7's and P8's, `Directory.Build.props`, `.gitignore` (`godot/assets/tg/`); this file; `docs/ai/specs/apps.md` section 3 | lead | - | all |

## The interfaces each package implements or calls

| Contract | File | Implemented by | Called by |
|---|---|---|---|
| `MotionStep.Step`, `ClampToTile`, `Displace` | `src/Core/Motion/MotionStep.cs` | P4 | P4's bridge, P8's and P6's predictors |
| `MotionPredictor`, `InputClock` | `src/Core/Motion/MotionPredictor.cs` | P4 | P8 (the client), P6 (the predicting bot) |
| `ICollisionView` | `src/Core/Motion/MotionStep.cs` | P4 (`LocomotionSeam.CollisionView`, the server), P5 (`ReplicaWorld.CollisionView`, a client) | `MotionStep` |
| `LocomotionSeam` (`Install`, `Register`, `Unregister`, `SetFrame`, `TryGetState`, `SpeedScale`, `SetSpeedScale`, `IsSolid`, `CollisionView`) | `Tg/Infrastructure/Locomotion/LocomotionSeam.cs` | P4 | P6 (the host), P5 (`IsSolid` for MapGrid version 2), `DevCommands.SpeedScale` |
| `ClientSeam.Click` | `Tg/Infrastructure/ClientSeam.Click.cs` | P4 | P6 (`ActionKind.Click`) |
| `ClientSeam.Rest`, `ToggleMoveIntent`, `ToggleCombatMode`, `ClickParams` | `Tg/Infrastructure/ClientSeam.Actions.cs` | contracts (complete) | P6 |
| `DevCommands` (`Damage`, `Heal`, `FullHeal`, `Kill`, `Revive`, `Knockdown`, `Stun`, `Bleed`, `Blood`, `Gas`, `Analyze`, `SpawnHuman`, `SpeedScale`, `GodMode`) | `Tg/Infrastructure/DevCommands.cs` | P6 | P6 (the console and `ActionKind.DevCommand` when `DevCommandsEnabled`) |
| `Codec`'s version-2 entry points (`MotionInputMessage`, `ActionMessage`, `EncodeOwnBlock`, `EncodeDelta` with a private block, `MaxOwnBlockSize`) and the version-2 layout of the existing ones | `src/Protocol/Codec.V2.cs`, `Codec.cs` | P5 | P6, P8, the bots |
| `ReplicaWorld`'s version-2 members (tables, `TryGetAppearance`, `ResolveString`, `ResolveResource`, `IsSolid`, `TurfAppearance`, `CellsChanged`, `Own`, `OwnStateReceived`, `Screen`, `ScreenEntryChanged`, `ScreenEntryRemoved`, `CollisionView`; `ReplicaEntity.HasPos`) | `src/Protocol/ReplicaWorld.V2.cs` | P5 | P7, P8, the bots |
| `IReplicator.BuildPrivate`, `BuildJoin(tick, PrivateInputs)`; the version-2 Delta body (appearance interning, `Pos`, `Yaw`, `Posture`, `Appearance`) | `src/Server/Replication/IReplicator.cs` | P5 | P6 |
| `InputJitterBuffer` | `src/Server/InputJitterBuffer.cs` | P6 | P6 |
| `IServerCodec`'s and `IClientEntryPoints`' version-2 members (defaults forward to `Codec` and `ClientSeam`) | `src/Server/ServerCodec.cs`, `ClientEntryPoints.cs` | contracts (forwarders) | P6 |
| `IWorldFeed.Replica`, `SendMotion`, `SendAction` | `godot/scripts/Shared/IWorldFeed.cs` | P8 (`GameClient`, `FakeFeed`) | P7, P8 |
| `IPoseSource` | `godot/scripts/Shared/IPoseSource.cs` | P8 | P7 (`EntityLayer`) |
| `IAppearanceTextures` | `godot/scripts/Render/IAppearanceTextures.cs` | P7 | P8 (the HUD), P7 |
| `IconToolOptions`, `IconConverter.Run` | `tools/Anomaly.Tools.Icons/Program.cs` | P7 (the conversion) | `godot/tools/smoke.sh`, `tools/soak` |
| `IDmAppearance` | `src/Core/DmShim/IDmAppearance.cs` | contracts (atom, image, mutable_appearance), P3 (the overlay snapshot) | P5 |
| every tg root skeleton (`slots.tsv` signatures) | the mirror files above | the file's owner | every package |
