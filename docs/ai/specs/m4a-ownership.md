# M4a file ownership

Status: v1 (the M4a contracts step, M4a.0), 2026-10-09. Who edits which file while the M4a packages work in waves of two (the
lead's `m4/plan-a.md`, the scout's `m4/scope-a.md` section 3): **P1** the click and item chain, screentips, the minimal
examine and the inert guard; **P2** hands, inventory, the HUD slots and throwing; **P3** storage, closets and crates;
**P4** tools, welder fuel and the door, table and button acts; **P5** ID cards, trims, `SSid_access`, the job datums, the
outfit and the dev path; **P6** protocol version 4, the server host, the replicator, the client seam and the bots; **P7** the
client's interaction; the testers **T1** (ported tg tests and DM-behaviour tests; after P1, P2, P3, P5) and **T2** (the demo
scenario, the version-4 fuzz, the bot soak; after P4, P6, P7); and the integration **I1**. **C** is the contracts step itself
(its files are frozen once it merges). Paths are repository paths; `Tg/` is `src/Core/Tg/`. The format follows
`m3-ownership.md`. Design: `docs/ai/specs/interaction.md`.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new files inside the folders its rows
  give it, and new mirror files of the tg files its rows name, and lists them in its report. A file no row names is not
  edited: report the needed member to the lead (a `// GAP(<owner>): ...` comment at the call site).
- **Per-proc lists (risk 1).** The section "tg procs by package" names every tg proc an M4a package translates, by file. A
  proc is **translated whole** (every statement of tg's proc; a call into a later milestone's mechanic may be a rule-1.4
  `Dm.NotTranslated` or `Dm.Deferred` block with its `not-translated.md` row) or **left** as it is (a `Dm.NotTranslated`
  root, an undeclared override, or a `live-partial.txt` stub) with its milestone. No package translates half a proc, and no
  package grows its list without the lead (report the proc you need; the lead adds it to a list or gives it a GAP).
- **Contracts are frozen.** Every public type, member and signature the contracts step wrote (the version-4 protocol types,
  fields, enum values and caps; `ActionLimits.Keybinding` and `Hover`; `ReplicaWorld`'s version-4 API; `IClientEntryPoints`'
  `Keybinding` and `Hover`; `ClientSeam.Keybinding`, `Hover`, `KeybindingType` and the version-4 `ClickParams`;
  `ScreentipSeam` and `IScreentipSink`; `InertSeam.GuardedProcs`, `HasUndeclaredOverride` and `MissingOverride`;
  `DevCommands.Give` and the `give` syntax; the client's `IWorldPicker`, `IHudPicker`, `PickHit`, `PickRay`, `PickSurface`,
  `IContainerView`, `ContainerCell`, `ContainerCellRole`; `FakeServerOptions.Inventory`; `IWorldFeed.ContextHintsReceived`;
  every tg skeleton's signature; the specs' contract sections) changes only through the lead. The owner fills the bodies
  (`throw new NotImplementedException("M4a Pn")` in new logic, `Dm.NotTranslated` in tg translation, rules 1.4), may add
  private members and new types, and may add public members a contract lacks (reported).
- **The meaning in the doc comments is the contract**: the target kinds and the server's checks (interaction.md 2), the
  params keys (3), the pick's surfaces and order (4.1), the HUD groups, the private images' attachment, the `Thrown` flag,
  the screentip seam's order of lines, the guard's walk and its "nothing happened" values (7).
- **Colour (risk 4): packages never recolour.** Every slot already has its `slots.tsv` signature: the contracts step checked
  the whole Tg assembly (5,174 declared slot methods, 0 mismatches after it fixed the last 3, section "Contracts findings")
  and declared the M4a roots with `DmTask` exactly where `suspends = yes`. A translator writes `await` / `Waiting.NoSleep` /
  `Waiting.Detach` as the analyzer demands; a slot whose colour looks wrong is reported, never changed.
- **Generated var blocks (D111, rules 10.2).** No package writes a var block for a type the generator covers or edits
  `Tg/Generated/**`; translators add procs in partial classes beside the generated part, rerun the types tool after adding
  procs (types-converter.md 1) and commit the regenerated files in their own commit. A type becomes live by
  types-converter.md section 6, with rows in the package's own section of `docs/ai/specs/live-partial.txt`. **When a new root
  makes the build fail** (CS0114 in `Generated/LivePartial/**`: a stub declared `virtual` under a root that now exists), run
  the tool with `-p:TreatWarningsAsErrors=false` once; it regenerates the stubs as `override`.
- **Subsystems.** A subsystem type with a hand-written part is created at boot (DV25's filter). `SSmouse_entered` (P1),
  `SSthrowing` (P2) and `SSid_access` (P5) are generated-only and got no skeleton: each package adds its subsystem with its
  translation, so that no boot reaches a `Dm.NotTranslated` `Initialize` or `fire`. `SSjob` is M2's inert stub subsystem
  (`SS_NO_INIT | SS_NO_FIRE`, `controllers/subsystem/job.cs`); its roots got no skeleton either, and P5 turns the stub into
  tg's subsystem (tg's flags and `Initialize`) with them.
- **Bodies throw until their owner lands.** A test that reaches another package's body fails, as intended. Test your own
  code with your own fixtures. **Merge order (plan-a.md): C, P6, P1, P2, P3, P5, P4; P7 any time after P6; then T1 and T2 as
  they finish, then I1.**
- **Specs.** P1 writes `types-converter.md` section 10's emitter facts and keeps `interaction.md` sections 6-7 true to its
  code; P6 fills `protocol.md` and `replication.md` section 14 and `apps.md` section 1 (the version-4 server); P7 writes
  `apps.md` section 2 (the M4a client: the pick, the radial, cursor mode, the windows, the view-model, the bindings) and keeps
  `interaction.md` sections 4, 5 and 8 true to its code; I1 writes `apps.md` section 4 (M4a). A change of a contract
  section goes through the lead.
- **Shared documents** (`docs/ai/DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, `DEVIATIONS.md`, `docs/ai/translation-rules.md` and
  its Appendix A and C, `object-model.md`, `presentation-seam.md`, `waiting-model.md`, `shim-ownership.md`,
  `dm-conformance.md`, `not-translated.md`, this file): the lead at merge; packages report additions (a deviation with its
  `// DEVIATION(DVn)` text, a decided slot, a new public Kernel/shim/Tg-infrastructure name, a deferred leaf's row). DV27
  (the inert guard) and DV28 (the context hints) are written; DV23 is marked "retired by P5" and P5 retires it (D130, plan
  note 5: only after P5's ID cards open Meta's doors in a test).
- **The shim and the Kernel** (`src/Core/DmShim/`, `src/Core/Kernel/`) are not edited by the packages. **The atom engine
  parts** (`Tg/Builtin/`) only as the rows below give them.
- **The solutions and project files** (`AnomalyStation.sln`, `godot/Anomaly.Client.sln`, every `.csproj`,
  `Directory.Build.props`, `.gitignore`) are the lead's, except `tools/Anomaly.Tools.Types/*` (P1, outside the solution),
  `tools/Anomaly.Tools.Bots/*.csproj` (P6) and `godot/Anomaly.Client.csproj` (P7).
- **D31 everywhere new.** No wall clock, thread or static mutable state in the core (`NoStaticStateTests`: a static list is
  a `ReadOnlyCollection`, a frozen set or an immutable array); the host may use a wall clock for pacing.
- **Runs** (D102): any server, client, bot or bench run lasts at most 15 minutes.
- **The toy hash** (seed 1) after the contracts step is in `m4/results/c.json`; a package that adds live world content
  (new live types, subsystems, fields) moves it and records the new value and why.

## Contracts findings (what every package should know)

- **Protocol version 4** (`ProtocolVersion.Current` is 4; `MessagesV4.cs`, the version-4 members of `ActionMessage`,
  `ScreenEntry`, `EntityState`, `EntityChange` and `OwnBlock`, `Codec.V4.cs`, `ReplicaWorld.V4.cs`): until P6's codec lands
  the bytes are version 3's; the new members are neither written nor read, `Codec`'s `ContextHintsMessage` entry points and
  the replica's `Images`, `ImageChanged`, `ImageRemoved` and `ScreenGroup` throw. `NetField.Flags` (bit 15) is in
  `Changeable`/`All`; the codec, `ReplicatedFields` and `WhitelistTests` use `ChangeableV3`/`AllV3` until P6 (P6 removes those
  names). M3's clients and bots build and work unchanged (a version-3 click is an entity click).
- **The host's version-4 actions** come through `IClientEntryPoints.Keybinding(client, code, down)` and `Hover(client,
  atom)` (default members forwarding to `ClientSeam`), and `Click(client, atom, params)` with a turf or a screen object as the
  atom. `ClientSeam.ClickParams(button, shift, ctrl, alt, iconX, iconY, screenLoc)` adds tg's `icon-x`, `icon-y` and
  `screen-loc` (P6).
- **The screentip seam is complete** (`Tg/Infrastructure/ScreentipSeam.cs`): P1's `on_mouse_enter` calls
  `ScreentipSeam.Publish(client, atom, used_name, context)` where tg composes its maptext; P6 installs an `IScreentipSink` on
  the server's world (`ScreentipSeam.Install`) that sends `ContextHintsMessage`. Contract tests:
  `tests/Anomaly.Core.Tests/M4aContracts/ScreentipSeamContractTests.cs`.
- **The guard's API** (`InertSeam.GuardedProcs`, `HasUndeclaredOverride`, `MissingOverride`) is frozen; P1 implements it
  over the generated `UndeclaredOverrides` table (types-converter.md 10) and puts the P1 sites in; P2, P3 and P4 put their
  sites in (interaction.md 7's table) once P1 has merged (before that, a site is a `// GAP(P1): guard` comment).
- **`give WHO TYPE`** parses (`DevCommandLine`, `DevVerb.Give`, `DevCommand.TypePath`) and dispatches to
  `DevCommands.Give`, which throws until P5. `access` and `dm_dev_access` stay until P5 retires them (D130).
- **Colour.** The M4a chain's slots (`attack_hand`, `attackby`, `item_interaction`, `tool_act`, `attack_self`,
  `melee_attack_chain`, `interact_with_atom`, `UnarmedAttack`, `ClickOn`, `put_in_hands`, `pickup`, `equipped`, `dropped`,
  `use_tool`, the `*_act`s ...) were already `DmTask` in M2 and M3's declarations, as `slots.tsv` says. The contracts step's
  check over the whole Tg assembly found 3 declarations against their slot, and recoloured them with their callers:
  `cleanable.add_diseases` and `init_reagents` (`game/objects/effects/decals/cleanable.cs`) and `atom_mounted.drop_wallmount`
  (`datums/components/atom_mounted.cs`). The check is `m4/c/colorcheck.py` over a reflection dump (the lead's scratchpad); I1
  reruns it.
- **The skeletons** (410 roots and global procs of the M4a files, `Dm.NotTranslated("<file:line>", "<proc>: M4a Pn
  (skeleton)")`, signatures from `slots.tsv`) are appended to the existing mirrors or in new `// SKELETON` mirrors. Overrides
  are not declared, so the inherited code keeps running until each package translates them; the regenerated index changed
  one type's state (`/atom/movable/screen/xenobio_potion`, not on Meta, inert to live: all its procs are roots now declared).
  Subsystem types, `GLOBAL_*` initializers and `/world` procs got none (see Rules).
- **Live-partial stubs became overrides.** 90 generated stubs of `Generated/LivePartial/{obj,turf}.g.cs` were roots
  (`virtual`) because the slot's root was undeclared; with the new roots (`/atom/examine`, `attackby`, `item_interaction`,
  the `*_act`s, `click_ctrl`, `attacked_by`) they are `override`s now. Nothing that runs changed.

## Files by owner

### Protocol, server, tools

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `src/Protocol/**`: `MessagesV4.cs`, `ReplicaWorld.V4.cs`, `Codec.V4.cs`, the version-4 members in `MessagesV2.cs`, `Messages.cs`, `NetField.cs`, `Limits.cs`, `MessageId.cs`, `ProtocolVersion.cs`; `Codec*.cs` | P6 | contracts complete; the codec's new entry points and the replica's version-4 members throw; the bytes are version 3's | P7, the bots |
| `src/Server/ServerHost.Network.cs`, `ServerHost.World.cs` (the version-4 actions: validation, the turf and screen targets, keybinding, hover one per tick, the screentip sink, `click_ms`), `Commands.cs`, `CommandLog.cs` (the version-4 fields recorded and replayed), `TokenBucket.cs` (`ActionLimits.Keybinding`, `Hover`: declared), `Session.cs` (the new buckets and counters), `ClientEntryPoints.cs` (C's two default members, frozen), `ServerCodec.cs` | P6 | M3's, plus the contracts | P1 (the hover path's tests), I1 |
| `src/Server/Replication/**` (the group byte, the private images, the `Flags` row, `Thrown`) | P6 | M3's | - |
| `Tg/Infrastructure/ClientSeam.Click.cs`, `ClientSeam.Actions.cs` (bodies of `Keybinding`, `Hover`, the version-4 `ClickParams`, `Click`'s location for a screen object; `KeybindingType` is C's, frozen) | P6 | declared, throw | P1, P2 (keybinding datums) |
| `tools/Anomaly.Tools.Bots/**` (version 4: the `fiddle` behaviour, abuse payloads for every new field) | P6 | M3's | I1, T2 |
| `src/Server/ServerHost.Dev.cs`, `DevCommandLine.cs` (`give`: parsed and dispatched by C; `access` removed by P5), `Tg/Infrastructure/DevCommands.cs` (`Give`; `Access` removed) | P5 | `give` parses; `DevCommands.Give` throws | I1, T2 |
| `src/Server/**` else (`ServerHost.cs`, `ServerHost.Console.cs`, `ServerConfig.cs`, `Program.cs`, `InputJitterBuffer.cs`, `TickPacer.cs`, `SmokeLoad.cs`, `LocomotionEntryPoints.cs`, `Metrics.cs`, `StepProfiler.cs`) | lead | M3's | - |
| `tools/Anomaly.Tools.Types/**` (the `UndeclaredOverrides` emitter, types-converter.md 10) | P1 | M3's | everyone (regeneration) |
| `tools/soak/**`, `tools/Anomaly.Tools.ToyWorld/**` | I1 | M3's | - |
| `docs/ai/specs/protocol.md`, `replication.md` (section 14: skeleton by C); `tests/Anomaly.Server.Tests/Protocol/**`, `Replication/**`, `Net/**`, `Bots/**`, `Server/**`; `tests/Anomaly.Server.Tests/M4aServer/P6/**` (new) | P6 | section 14 skeletons; M3's tests (CodecV2Tests and WhitelistTests moved to the V3 names) | - |

### The tg tree (`src/Core/Tg/`) and its engine parts

The **area** a package may create new mirror files in is the folder of its rows. The procs each package translates are in
"tg procs by package" below.

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `_onclick/click.cs` (the M4a rest of `ClickOn` and the others of the list), `_onclick/click_ctrl.cs`, `_onclick/click_alt.cs`, `_onclick/item_attack.cs`, `_onclick/other_mobs.cs` (the rest), `game/atom/atom_tool_acts.cs`, `game/atom/atom_examine.cs`, `datums/screentips/**`, `datums/elements/screentips/**`, `_onclick/hud/screen_objects/screentip.cs`, `controllers/subsystem/mouse_entered.cs` (new), `modules/balloon_alert/balloon_alert.cs`, `game/atom/_atom.cs` (only `MouseEntered`, `on_mouse_enter`, `set_hover_maptext`), `__HELPERS/turfs.cs` (only `get_turf_pixel`), `Infrastructure/InertSeam.cs` (the bodies of `HasUndeclaredOverride` and `MissingOverride`; the rest frozen), `Generated/Types/UndeclaredOverrides.g.cs` (generated) | P1 | skeletons (roots), M2's `ClickOn` with 7 `NotTranslated` item branches | P2, P3, P4 (the guard, the chain) |
| `modules/mob/inventory.cs`, `modules/mob/living/carbon/inventory.cs`, `modules/mob/living/carbon/human/inventory.cs`, `game/objects/items.cs` (incl. `use_tool` and the tool checks P4 calls, `/obj/item/examine`'s weight line), `modules/mob/mob.cs` (the hand procs, `examinate`, `visible_message`'s self message, `show_message`, `attack_ui`), `datums/keybinding/{mob,human,living}.cs`, `modules/mob/living/living_item_handling.cs`, `controllers/subsystem/throwing.cs` (with the subsystem), the throw and attack-animation procs of `game/atoms_movable.cs` (the lead's file: only those procs), `game/atom/atom_act.cs` (only `hitby`, `hitby_react`), the `hitby` subsets of `modules/mob/living/living_defense.cs`, `living/carbon/carbon_defense.cs` and `living/carbon/human/human_defense.cs`, `_onclick/hud/{hud,human,inventory_slot}.cs`, `_onclick/hud/screen_objects/screen_objects.cs` (the whole file, also tg's `screen/storage` and `close` that P3 calls), the held-item and offset gaps of `modules/mob/living/carbon/carbon_update_icons.cs` and `living/carbon/human/human_update_icons.cs` | P2 | skeletons (roots), M2's | P1 (the chain), P3 (storage), P5 (the outfit's equip) |
| `datums/storage/**`, `game/atom/atom_storage.cs`, `game/objects/items/storage/**` (the base, backpacks, toolboxes, boxes, belts and medkits Meta uses, their `PopulateContents`), `game/objects/structures/crates_lockers/**` (closets, crates, secure closets, Meta's closet types' `PopulateContents`) | P3 | skeletons (roots) | P2 (`show_contents` entries), P4 (weld shut) |
| `game/objects/items/cards_ids.cs`, `datums/id_trim/**`, `controllers/subsystem/networks/id_access.cs` (new, with the subsystem), `controllers/subsystem/job.cs` (`SetupOccupations` subset, with the subsystem), `datums/outfit.cs` (the deferred slots), `modules/jobs/job_types/_job.cs` (`post_equip`), `modules/mob/living/living.cs` (only `get_access`, `get_idcard`, `get_id_in_hand`), `Infrastructure/CrewStandIn.cs`, `Builtin/mob.access.builtin.cs` (deleted by P5), the human's `get_access` override in `modules/mob/living/carbon/human/human_helpers.cs` (removed by P5) | P5 | skeletons (roots); M2's outfit | P2 (equip), P4 (doors' `allowed`) |
| `game/objects/items/tools/engineering/{weldingtool,wrench,screwdriver,crowbar,wirecutters}.cs`, `modules/reagents/chemistry/holder/holder.cs` (only `add_reagent`, `remove_reagent` and their callees in the file), `modules/reagents/chemistry/holder/reactions.cs` (only `handle_reactions` as a deferred leaf), `modules/reagents/chemistry/reagents.cs` (the `/datum/reagent` base: `New`, `Destroy`, `on_new`, `on_merge`), the welding fuel reagent's file (`modules/reagents/chemistry/reagents/other_reagents.cs`, only `/datum/reagent/fuel`), `game/machinery/doors/{door,airlock,windowdoor,poddoor}.cs` (the tool acts, `item_interaction`, the pod door's open path), `game/objects/structures/tables_racks.cs` (placement), `game/machinery/buttons.cs`, `modules/assembly/{assembly,doorcontrol}.cs` | P4 | skeletons (roots), M3's doors and tables | P1 (the tool chain) |
| `Infrastructure/ScreentipSeam.cs`, `Infrastructure/InertSeam.cs`'s API, `ClientSeam.KeybindingType` | C (frozen) | complete | P1, P6 |
| `game/objects/effects/decals/cleanable.cs`, `datums/components/atom_mounted.cs` | lead (the contracts step recoloured 3 methods) | complete | - |
| `__HELPERS/*.cs`, `__DEFINES/*.cs` mirrors of the global procs and inline macros M2 and M3 left as local copies, the regeneration of `Generated/**` at the integration, `game/atoms_movable.cs` and `game/objects/objs.cs` but P2's procs, `Infrastructure/*` not above, `Builtin/**` not above | I1 / lead | M3's | all |

### The client

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `godot/scripts/Shared/Play/{WorldPicker,HudPicker,RadialModel,ThrowVisual,ContainerLayout}.cs` (new), `Shared/FakeServer.cs` (`Inventory`), `Shared/FakeFeed.cs`, `Shared/GameClient.cs` (`ContextHintsReceived`), `Shared/IWorldFeed.cs` (P7 implements C's event), `Shared/Play/PlayOptions.cs` (the bindings and the migration of `Help`/`Shove`), `Shared/Play/PlaySession.cs`, `GameScene.cs` (input, cursor mode), `EntityLayer.cs` (highlight, the thrown arc and spin), `Render/StaticProps.cs` (the pick list, a highlight flag), `MapRenderer.cs` (the cell pick), `Hud/**` (`HudLayer`, new `ContainerWindow`, `InventoryPanel`, `Radial`, `ChatLines`, `ViewModel`), `godot/scenes/**`, `godot/Anomaly.Client.csproj`, `godot/tools/**` | P7 | M3's | - |
| `godot/scripts/Shared/Play/IWorldPicker.cs`, `IContainerView.cs` | C (frozen) | complete | P7 |
| `tests/Anomaly.Server.Tests/Client/Interaction/**` (new: the pick, the radial timing, the HUD pick, the container layout from real entries, the bindings' migration) | P7 | not created | - |
| `docs/ai/specs/apps.md` section 2 (the M4a client) | P7 | M3's | - |

### Tests, specs and the lead

| File or folder | Owner | State | Others use |
|---|---|---|---|
| `tests/Anomaly.Core.Tests/Tg/M4aClick/P1/**` (the chain's branches on a fixture room with stub items, the screentips, `get_turf_pixel` with tg's test, the minimal examine), `tests/Anomaly.Core.Tests/Tools/Types/**` (the `UndeclaredOverrides` emitter: a second run byte-equal) | P1 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aHands/P2/**` (put, drop, swap, equip and unequip per slot, throw flights, the HUD entries per slot) | P2 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aStorage/P3/**` (tg's `storage` and `closets` tests, insert and remove limits, `show_contents` entries, a closet's dump, lock and weld; Meta's boot cost measured) | P3 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aTools/P4/**` (weld shut and unweld with fuel, the panel, pry refused, table placement by `icon-x`, a Meta button opens its pod doors) | P4 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aAccess/P5/**` (tg's `id_access` and `door_access` subsets, the outfit's 7 items, the assistant opens maintenance and not security), the migration of `tests/Anomaly.Core.Tests/Tg/M3Doors/**/DoorTests.cs`, `tests/Anomaly.Server.Tests/**/MetaWalkTests.cs` and `tests/Anomaly.Server.Tests/M3Contracts/ServerOptionsContractTests.cs`'s `access` cases to `give` | P5 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aInteraction/T1/**` (ported tg tests: `storage`, `closets`, `interaction_door`, `door_access`, `id_access`, `combat_welder`, `held_slowdown`, `outfit_sanity` (assistant), `interaction_structures`, `clothing_drops_items`, `combat` subsets; the DM-behaviour tests of scope 2k; the guard over every Meta item type, marked slow) | T1 | - | - |
| `tests/Anomaly.Server.Tests/Integration/M4aScenarioTests.cs` (the demo on a fixture map with the headless `PlaySession`), `tests/Anomaly.Server.Tests/M4aInteraction/T2/**` (version-4 fuzz, forged targets, the secret test over storage and images, the bots' soak) | T2 | - | - |
| `tests/Anomaly.Core.Tests/Tg/M4aIntegration/**`, `tests/Anomaly.Server.Tests/Integration/**` but T2's file, `docs/ai/specs/apps.md` section 4 (M4a), the run sheet, the measurements (`click_ms`, boot, heap, the 50- and 150-bot soak) | I1 | - | - |
| `tests/Anomaly.Core.Tests/M4aContracts/**`, `tests/Anomaly.Server.Tests/M4aContracts/**` | C | complete | all |
| `docs/ai/specs/interaction.md` | C (contract); P1 sections 6-7 and P7 sections 4, 5, 8 keep it true to their code | complete | all |
| `docs/ai/specs/types-converter.md` section 10 | C (the rule); P1 (the emitter's facts) | complete | P1 |
| `docs/ai/specs/live-partial.txt` | each package its own section (`## P1 (M4a)` ... `## I1 (M4a)`); the format is a contract | sections empty | the generator |
| `docs/ai/specs/m4a-ownership.md`, `DEVIATIONS.md`, `translation-rules.md`, `not-translated.md`, `DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, solutions, `.gitignore` | lead | - | all |

T1 and T2 may fix defects in their packages' files (`Fix translation:` commits) after those packages merged.

## tg procs by package (risk 1)

Notation: `type.proc` with tg's type path shortened (`I` = `/obj/item`, `S` = `/atom/movable/screen`, `L` = `/mob/living`,
`C` = `/mob/living/carbon`, `H` = `/mob/living/carbon/human`, `CL` = `/obj/structure/closet`, `D` = `/datum`). "M4a" lists
the procs the package translates whole. "Later" names what stays as it is (a `NotTranslated` root, an undeclared override
or a stub), by milestone; the listed milestone goes into the proc's `live-partial.txt` row when a live type needs the stub,
or into `not-translated.md` when a translated proc calls it. Procs of a file translated before M4a (M2, M3) are not
repeated; a package that finds one of them reaches an M4a gap reports it.

### P1

- **`_onclick/click.dm`**: M4a: `/mob.ClickOn` (its 7 item branches: the held item's `melee_attack_chain`, `attack_self`
  via `mode`, `throw_item` in throw mode, `base_ranged_item_interaction`, `RangedAttack` with an item, the `W == A` branch),
  `/mob.ShiftClickOn`, `/atom.ShiftClick`, `/proc/RangedReachCheck`, `/proc/CheckToolReach` (the partial ones completed),
  `/mob.MiddleClickOn` (check). Later: `/mob.ShiftMiddleClickOn` (pointing, M9), `S/click_catcher.*` (never as such: the 3D
  pick replaces the view's click catcher, rules 13), `/mob/dead/observer.MouseWheelOn` (ghosts, M10).
- **`_onclick/click_ctrl.dm`**: M4a: all of it: `/mob.CtrlClickOn`, `/mob.base_click_ctrl`, `L.base_click_ctrl` (its `grab`
  and `pulled` calls are `NotTranslated` blocks, M9), `/mob.CtrlMiddleClickOn` (the admin tag block `NotTranslated`, M10),
  `/atom.click_ctrl`, `/mob.CtrlShiftClickOn`, `/mob.base_click_ctrl_shift`, `/atom.click_ctrl_shift`.
- **`_onclick/click_alt.dm`**: M4a: `/mob.AltClickOn`, `/mob.base_click_alt`, `L.base_click_alt`, `/atom.click_alt`,
  `/mob.AltClickSecondaryOn`, `/mob.base_click_alt_secondary`, `/atom.click_alt_secondary`, `/mob.alt_shift_click_on`,
  `L.alt_shift_click_on`. Later: `L.try_open_loot_panel_on` (the loot panel, tgui, M9: its call is a block).
- **`_onclick/item_attack.dm`**: M4a: the whole file: `I.melee_attack_chain`, `attack_self`, `attack_self_secondary`,
  `pre_attack`, `pre_attack_secondary`, `/atom.attackby`, `attackby_secondary`, `/obj.attackby`, `L.item_interaction`,
  `L.attackby`, `L.attackby_secondary`, `I.attack`, `attack_secondary`, `attack_atom`, `/atom.attacked_by`, `/area.attacked_by`,
  `L.attacked_by`, `L.attack_effects`, `C.attack_effects`, `H.attack_effects` (blood on the attacker and wounds are blocks: M6),
  `I.afterattack`, `get_clamped_volume`, `L.send_item_attack_message`, `L.get_hit_area_message`.
- **`_onclick/other_mobs.dm`**: M4a: `/atom._try_interact`, `/atom.interact` (its `ui_interact` call is a block, tgui M9).
  Later: `/atom.ui_status` (tgui, M9), the larva, drone, pAI and hostile simple-animal overrides (their mobs, after the core
  version).
- **`game/atom/atom_tool_acts.dm`**: M4a: the whole file (25 procs): `base_item_interaction`, `tool_act`,
  `item_interaction`, `item_interaction_secondary`, `I.interact_with_atom`, `interact_with_atom_secondary`,
  `base_ranged_item_interaction`, `ranged_item_interaction`, `ranged_item_interaction_secondary`,
  `I.ranged_interact_with_atom`, `ranged_interact_with_atom_secondary`, and the 14 `*_act`/`*_act_secondary` bases
  (crowbar, multitool, screwdriver, wrench, wirecutter, welder, analyzer).
- **`game/atom/atom_examine.dm`**: M4a (D128's minimal examine): `/atom.examine`, `examine_tags`, `examine_descriptor`,
  `examine_post_descriptor`, `get_examine_name`, `L.get_examine_name`, `examine_title`, `get_name_chaser`. Later:
  `get_sniff_examine`, `examine_more`, `get_id_examine_strings`, `get_examine_icon` (identity and the examine panel, M9).
- **`datums/screentips/{atom_context,item_context,screentips}.dm`**: M4a: `/atom.register_context`, `/atom.add_context`,
  `I.register_item_context`, `I.add_item_context`, `/proc/build_context`, `/proc/prepare_screentip_context_icons` (the icon
  images are a deferred leaf: the client draws the buttons).
- **`datums/elements/screentips/*.dm`** (5 files): M4a: each element's `Attach`, `Detach` and
  `on_requesting_context_from_item` (`_from_mob` for the mob typechecks).
- **`_onclick/hud/screen_objects/screentip.dm`**: M4a: `S/screentip.Initialize`, `update_view` (its maptext stays empty: the
  seam carries the text, interaction.md 6.1).
- **`controllers/subsystem/mouse_entered.dm`**: M4a: the subsystem (`SUBSYSTEM_DEF`'s `New`, `fire`).
- **`modules/balloon_alert/balloon_alert.dm`**: M4a: `/atom.balloon_alert`, `balloon_alert_to_viewers`,
  `balloon_alert_to_hearers`, `balloon_alert_perform` (its image and `animate` end at the message sink's `Balloon`),
  `forget_balloon_alert`.
- **`game/atom/_atom.dm`**: M4a: `/atom.MouseEntered`, `/atom.on_mouse_enter` (the maptext composition replaced by
  `ScreentipSeam.Publish`, DV28), `/atom.set_hover_maptext` (a deferred leaf: presentation).
- **`__HELPERS/turfs.dm`**: M4a: `/proc/get_turf_pixel` (with tg's `get_turf_pixel` unit test).
- **The guard:** `InertSeam.HasUndeclaredOverride`, `MissingOverride` and P1's sites (interaction.md 7); the
  `UndeclaredOverrides` emitter.

### P2

- **`modules/mob/inventory.dm`**: M4a: `get_empty_held_index_for_side`, `get_held_items_for_side`,
  `get_empty_held_indexes`, `get_hand_zone_of_item`, `get_num_held_items`, `is_holding`, `is_holding_item_of_type`,
  `is_holding_item_of_types`, `is_holding_tool_quality`, `get_held_index_name`, `can_put_in_hand`, `put_in_hand`,
  `put_in_l_hand`, `put_in_r_hand`, `put_in_hand_check`, `L.put_in_hand_check`, `put_in_active_hand`,
  `put_in_inactive_hand`, `put_in_hands`, `is_holding_items`, `drop_all_held_items`, `canUnEquip`,
  `putItemFromInventoryInHandIfPossible`, `dropItemToGround` (tg's `rand(-6, 6)` offsets on the world RNG),
  `transfer_item_to_turf`, `transferItemToLoc`, `temporarilyRemoveItemFromInventory`, `doUnEquip`, `has_unequipped`,
  `equip_to_appropriate_slot`, `I.equip_to_best_slot`, `equip_to_storage`, `quick_equip`, `execute_quick_equip`,
  `getBackSlot`. Later: `L.unequip_everything`, `L.get_all_gear`, `L.gather_belongings`, `L.drop_everything`,
  `L.get_all_cells` (stripping and gibbing: M9, M6), `change_number_of_hands` (M6).
- **`modules/mob/living/carbon/inventory.dm`**: M4a: `/proc/hidden_slots_to_inventory_slots`, `C.get_slot_by_item`,
  `get_visible_items`, `equip_in_one_of_slots`, `equip_to_slot`, `has_unequipped`, `doUnEquip`, `remove_item_coverage`,
  `refresh_obscured`, `item_coverage_changed`, `get_holding_bodypart_of_item`, `get_covered_body_zones`,
  `get_all_covered_flags`, `is_location_accessible`. Later: the internals (`open_internals`, `try_open_internals`,
  `close_internals`, `close_externals`, `close_all_airtanks`, `toggle_open_internals`, `toggle_close_internals`,
  `toggle_internals`, `toggle_externals`: M4c), `equip_conspicuous_item` (roles, M10).
- **`modules/mob/living/carbon/human/inventory.dm`**: M4a: `H.get_slot_by_item`, `get_visible_items`, `equip_to_slot`,
  `doUnEquip`, `item_coverage_changed`. Later: `smart_equip_targeted` (the unbound belt and bag bindings, M9),
  `equip_species_outfit`, `delete_equipment` (M10), `change_number_of_hands` (M6), `toggle_internals`, `toggle_externals`,
  `can_breathe_helmet`, `can_breathe_mask`, `can_breathe_internals` (M4b/M4c).
- **`game/objects/items.dm`**: M4a: `I.attack_hand`, `attempt_pickup`, `can_mob_unequip`, `pickup`, `dropped`, `equipped`
  (check), `on_found`, `play_pickup_sound`, `play_drop_sound`, `play_equip_sound`, `play_throw_drop_sound`,
  `play_mob_throw_hit_sound`, `mob_throw_hit_sound_chain`, `throw_drop_sound_chain`, `sound_chain` (the sounds are deferred
  leaves until M4b's audio), `do_pickup_animation`, `do_drop_animation`, `update_slot_icon`, `on_exit_storage`,
  `remove_item_from_storage`, `throw_at`, `pre_impact`, `throw_impact`, `after_throw`, `on_thrown`, `hitby`, `hit_reaction`,
  `click_ctrl`, `item_ctrl_click`, `MouseEntered`, `MouseExited` (the outline: `apply_outline` a deferred leaf),
  `examine` (the weight line, D128), `examine_tags`, `examine_descriptor`, `use_tool`, `tool_start_check`, `tool_use_check`,
  `use`, `play_tool_sound`, `play_tool_operating_sound`, `tool_check_callback`, `get_temperature`, `get_sharpness`,
  `/atom/movable.do_item_attack_animation`, `animate_attack`. Later: actions (`add_item_action`, `grant_action_to_bearer`,
  `remove_item_action`, `on_action_deleted`, `give_item_action`, `item_action_slot_check`, `ui_action_click`,
  `update_item_action_buttons`: M9), stripping (`canStrip`, `doStrip`: M9), offering (`on_offered`, `on_offer_taken`: M9),
  `examine_more` (M9), grinding and juicing (M8), materials (`apply_*_material_effect*`, `change_material_*`: M5), embedding
  (`get_embed`, `set_embed`, `force_embed`: M6), burning and acid (`burn`, `acid_melt`, `microwave_act`: M5/M8), fire
  (`open_flame`, `ignition_effect`: M4b), fantasy and admin (`modify_fantasy_variables`, `apply_fantasy_bonuses`,
  `vv_*`, `ui_act`: M10), `suicide_act` (M10), the rest of the file's procs (their milestones in their rows).
- **`modules/mob/mob.dm`**: M4a: `/mob.examinate`, `run_examinate`, `/atom.visible_message` and `/mob.visible_message` (the
  self message; the viewers' delivery stays a deferred leaf until M9), `/mob.show_message`, `attack_ui`, `swap_hand`,
  `perform_hand_swap`, `activate_hand`, `mode`, `execute_mode`. Later: `audible_message` (M9), everything else of the file
  that is not translated (its milestones).
- **`datums/keybinding/{mob,human,living}.dm`**: M4a: `mob/swap_hands.down`, `mob/select_hand.down`,
  `mob/activate_inhand.down`, `mob/drop_item.down`, `human.can_use`, `human/quick_equip.down`,
  `living/toggle_throw_mode.down`, `living/hold_throw_mode.down`, `hold_throw_mode.up`. Later: `stop_pulling`,
  `drop_item_specific`, `prevent_movement`, `quick_equip_belt`, `resist`, `give`, `cancel_interactions` (M9), `target.down`
  (the zone keys, M6), `look_up`, `look_down` (multi-z, after the core version), `enable_combat_mode`,
  `disable_combat_mode` (not bound in M4a: later).
- **`modules/mob/living/living_item_handling.dm`**: M4a: `L.toggle_throw_mode`, `throw_mode_off`, `throw_mode_on`,
  `/mob.throw_item`, `L.throw_item`. Later: `give`, `take`, `L.click_ctrl_shift` (offering, M9).
- **`controllers/subsystem/throwing.dm`**: M4a: the subsystem (`New`, `fire`, `stat_entry` a deferred leaf),
  `D/thrownthing.New`, `Destroy`, `on_thrownthing_qdel`, `get_thrower`, `tick`, `finalize`.
- **`game/atoms_movable.dm`** (only these): M4a: `throw_impact`, `pre_impact`, `hitby`, `safe_throw_at`, `throw_at`,
  `do_attack_animation` (the deferred leaf translated: `animate` reaches the appearance sink).
- **`game/atom/atom_act.dm`** (only these): M4a: `/atom.hitby`, `hitby_react`.
- **The `hitby` subsets**: M4a: `L.hitby`, `try_catch_item`, `can_catch_item`, `get_volume_by_throwforce_and_or_w_class`,
  `log_hit_combat` (logging a deferred leaf) of `living_defense.dm`; `C.hitby`, `try_catch_item`, `can_catch_item` of
  `carbon_defense.dm`. Nothing of `human_defense.dm` beyond M2's.
- **`_onclick/hud/hud.dm`**: M4a: `build_hand_slots`, `create_inventory_slots`, `update_locked_slots`, `inventory_update`,
  `update_inventory_slot`, `show_hud` (the inventory, hotkey and storage branches). Later: actions and landings
  (`position_action*`, `hide_actions`, `generate_landings`, `hide_landings`, `build_action_groups`: M9), plane masters
  (`get_planes_from`, `on_plane_increase`: never as such, rules 13).
- **`_onclick/hud/human.dm`**: M4a: `D/hud/human.initialize_screen_objects` (the M4a buttons: hands, swap, drop,
  throw_catch, resist, the inventory toggle, the zone selector, `combattoggle/flashy`), `update_locked_slots`. Later:
  `H.toggle_hotkey_verbs` (verbs, M9).
- **`_onclick/hud/inventory_slot.dm`**: M4a: the whole file (`initialize_inventory_slots`, `D/inventory_slot` and
  `/hands`, `/human` slot datums of `hud/human.dm`).
- **`_onclick/hud/screen_objects/screen_objects.dm`**: M4a: `S/swap_hand.Click`, `S/inventory.Click`, `MouseEntered`,
  `MouseExited`, `update_icon_state`, `add_overlays`, `S/inventory/hand.update_overlays`, `Click`, `S/close.Initialize`,
  `Click`, `S/storage_up.Initialize`, `Click`, `S/drop.Click`, `S/combattoggle.Initialize`, `Click`, `update_icon_state`,
  `S/combattoggle/flashy.update_overlays`, `S/resist.Click` (its `resist` call is a block, M9), `S/throw_catch.Click`,
  `S/zone_sel.Initialize`, `Click`, `MouseEntered`, `MouseMove`, `MouseExited`, `get_zone_at`, `set_selected_zone`,
  `update_overlays`, `S/storage.Initialize`, `Click`, `S.examine`. Later: `S/storage/cell.mouse_drop_receive` (drag, M9),
  `S/pull.*` (M9), `S/rest.*`, `S/sleep.*` (M9), `S/navigate`, `area_creator`, `language_menu`, `memories`, `floor_changer`,
  `component_button` (after the core version), `S/spacesuit.*` (M4c), `S/hunger*`, `blood_level` (M8), `S/splash`,
  `combo`, `slime_power`, `xenobio_*` (after the core version).
- **The update-icons gaps**: M4a: `C.get_held_overlays`, `H.get_held_overlays`, `I.get_worn_offsets`, and the M4a gaps of
  `H.update_worn_id`, `update_worn_gloves`, `update_worn_shoes`, `update_worn_belt`, `update_worn_back`, `update_worn_ears`
  (the outfit's items: the glove and foot offsets, the held index offsets).

### P3

- **`datums/storage/storage.dm`**: M4a: `New`, `Destroy`, `item_init`, `handle_enter`, `handle_exit`, `set_parent`,
  `set_real_location`, `real_location_deleted`, `handle_examination`, `set_holdable`, `can_insert`, `get_exception_count`,
  `get_total_weight`, `attempt_insert`, `item_insertion_feedback`, `attempt_remove`, `remove_all`, `remove_single`,
  `remove_type`, `mass_empty`, `return_inv`, `on_preattack`, `item_interact_insert`, `on_attack`, `process_numerical_display`,
  `open_storage_on_signal`, `on_click_alt`, `open_storage`, `put_in_hands_async`, `close_distance`, `can_be_reached_by`,
  `close_all`, `close_all_non_wearers`, `close_all_recursive`, `refresh_views`, `can_see_contents`, `show_contents`,
  `hide_contents`, `orient_storage`, `animate_parent`, `contents_changed_w_class`, `set_locked`. Later: `on_deconstruct`
  (M5), `topic_handle`, `handle_extra_examination` (M9), `update_actions`, `action_trigger`, `action_deleted`,
  `toggle_collection_mode`, `handle_mass_pickup`, `collect_on_turf`, `collect_on_turf_loop` (the item actions and bags'
  collection, M9), `mousedrop_receive`, `on_mousedrop_onto`, `dump_content_at`, `on_mousedropped_onto` (drag, M9),
  `on_emp_act` (M7).
- **`datums/storage/storage_interface.dm`**: M4a: `D/storage_interface.New`, `list_ui_elements`, `Destroy`,
  `update_position`, `add_items`. Later: `/silicon` (cyborgs, after the core version).
- **`datums/storage/subtypes/*.dm`**: M4a: the `New` of every storage subtype an item type on Meta uses (backpacks, boxes,
  toolboxes, belts, medkits, pockets; P3 lists them from the map). Later: the others (their items stay inert).
- **`game/atom/atom_storage.dm`**: M4a: `create_storage`, `clone_storage`.
- **`game/objects/items/storage/storage.dm`**: M4a: `I/storage.Initialize`, `create_storage`, `PopulateContents`,
  `AllowDrop`, `emptyStorage`, `get_types_to_preload`, `quick_remove_item`. Later: `contents_explosion` (M5), `canStrip`,
  `doStrip` (M9), the fantasy procs (M10), `used_in_craft` (M5).
- **`game/objects/items/storage/{backpack,toolboxes/*,boxes/*,belt,medkit,...}.dm`**: M4a: the procs of the types Meta uses:
  `I/storage/backpack.Initialize`, the satchels' `Initialize` and `PopulateContents`, `I/storage/toolbox.Initialize`,
  `update_overlays`, `interact_with_atom`, `tool_act`, `use_tool_on` (the latches and the toolbox's own chain overrides),
  every Meta toolbox's and box's `PopulateContents` (D133), `I/storage/box.Initialize`, `update_overlays`, `attack_self`,
  the survival boxes' `create_storage`, `PopulateContents`, `apply_overrides`. Later: `suicide_act` (M10), bags of holding,
  the santa bag, the meat backpack, the clown, mime and hug boxes, miner modkits, skillchips, contractor boxes, the old
  toolbox's `calc_damage` (their milestones; their types stay inert).
- **`game/objects/structures/crates_lockers/closets.dm`**: M4a: `CL.Initialize`, `LateInitialize`, `PopulateContents`,
  `populate_contents_immediate`, `Destroy`, `update_appearance`, `update_icon`, `update_overlays`,
  `closet_update_overlays`, `animate_door`, `end_door_animation`, `get_door_transform`, `examine`, `add_context`,
  `CanAllowThrough`, `can_open`, `can_close`, `dump_contents`, `take_contents`, `before_open`, `open`, `after_open`,
  `insert`, `insertion_allowed`, `before_close`, `close`, `after_close`, `toggle`, `set_access`, `item_interaction`
  (putting an item into the open closet), `welder_act` (weld shut), `after_weld`, `relaymove`, `attack_hand`,
  `attack_hand_secondary`, `attack_paw`, `Exit`, `can_unlock`, `togglelock`, `lock`, `unlock`, `play_closet_lock_sound`,
  `AllowDrop`, `add_to_roundstart_list`, `CL/secure_closet.attacked_by`, `hitby`. Later: deconstruction and electronics
  (`handle_deconstruct`, `atom_break`, `on_craft_completion`, `inherit_airlock_electronics_access`, `multitool_act`,
  `item_interaction_secondary`, `can_install_airlock_electronics`, `can_unscrew_airlock_electronics`,
  `can_install_card_reader`, `can_pryout_card_reader`, `screwdriver_act`, `crowbar_act`, `wrench_act_secondary`: M5),
  air (`return_air`, `process`, `process_internal_air`, `return_temperature`: M4b), resisting and shaking
  (`container_resist_act`, `relay_container_resist_act`, `check_if_shake`, `bust_open`: M9), `mouse_drop_receive` (M9),
  `attack_robot*`, `attack_self_tk` (after the core version), `verb_toggleopen` (verbs, M9), `emag_act`, `on_magic_unlock`,
  `locker_living` (after the core version), `emp_act` (M7), `contents_explosion`, `singularity_act` (M5), `get_save_vars`,
  `vv_edit_var`, `rename_check` (M10), `get_remote_view_fullscreens` (M9).
- **`crates_lockers/crates.dm`**: M4a: `CL/crate.Initialize`, `Destroy`, `CanAllowThrough`, `update_icon_state`,
  `closet_update_overlays`, `attack_hand`, `after_open`, `after_close`, `CL.populate_with_random_maint_loot`,
  `CL/crate.add_to_roundstart_list`, and the `PopulateContents` of Meta's crate types. Later: `tear_manifest` (cargo, M10),
  the trash cart, freezers' air (M4b), market, glitter and gold crates (after the core version).
- **`crates_lockers/closets/secure/*.dm`, `closets/*.dm`**: M4a: `CL/secure_closet.Initialize` and every Meta closet type's
  `PopulateContents` (D133, data-like). Later: `grey_tide` (an event, M10), the rest of each file.

### P5

- **`game/objects/items/cards_ids.dm`**: M4a: `I/card.update_overlays`, `get_displayed_name`, `I/card/id.Initialize`,
  `Destroy`, `Moved`, `equipped`, `dropped` (both definitions: `dropped__d0` and `dropped`), `equip_to_item_loc`,
  `unequip_from_item_loc`, `on_loc_equipped`, `on_loc_dropped`, `retrieve_access`, `can_add_wildcards`, `add_wildcards`,
  `remove_wildcards`, `add_access`, `remove_access`, `set_access`, `clear_access`, `build_access_lists`, `attack_self`,
  `add_context`, `add_item_context`, `item_interaction` (the money branch a deferred leaf, M10), `examine`, `GetAccess`,
  `GetID`, `remove_id`, `update_in_wallet`, `update_label`, `update_honorific`, `get_trim_assignment`,
  `get_trim_sechud_icon`, `get_trim_sechud_icon_state`, `I/card/id/advanced.Initialize`, `Destroy`, `add_context`,
  `item_interaction`, `on_loc_equipped`, `on_loc_dropped`, `update_intern_status`, `remove_intern_status`, `update_label`,
  `update_overlays`, `get_trim_assignment`, `get_trim_sechud_icon_state`, the `Initialize` of `/advanced/platinum`, `gold`,
  `debug` and `debug.alt_click_can_use_id`, `can_be_used_in_payment`. Later: `get_cached_flat_icon` (presentation, a
  deferred leaf), the holopay, money and account procs (`try_project_paystand`, `can_proj_holopay`, `set_holopay_*`,
  `insert_money`, `mass_insert_money`, `alt_click_can_use_id`, `set_new_account`, `click_alt`, `click_alt_secondary`,
  `pay_debt`, `set_account`, `clear_account`, `can_be_used_in_payment`, `drop_card`, `interact_with_atom`,
  `interact_with_atom_secondary`, `attack_self_secondary`, `item_ctrl_click`, the departmental budget card: M10),
  `examine_more`, `get_id_examine_strings`, `get_examine_icon`, `on_pointed` (M9), `apply_fantasy_bonuses`,
  `vv_edit_var`, `suicide_act` (M10), `recolor_id`, `after_input_checks` (tgui, M9), the prisoner, plainclothes, chameleon
  and cardboard cards (after the core version or M10).
- **`datums/id_trim/_id_trim.dm`, `jobs.dm`**: M4a: `D/id_trim.find_job`, `D/id_trim/job.New`, `refresh_trim_access`,
  `find_job`, `/job/assistant.refresh_trim_access`, `/job/captain.New`, `I.get_sechud_job_icon`,
  `get_sechud_job_icon_state`. Later: the detective's, head of security's, security officer's, veteran advisor's and
  warden's `refresh_trim_access` and `get_random_bounty_type` (their jobs, M10; their trims are data).
- **`controllers/subsystem/networks/id_access.dm`** (with the subsystem): M4a: `New`, `Initialize`,
  `refresh_job_trim_singletons`, `setup_access_flags`, `setup_region_lists`, `setup_trim_singletons`,
  `setup_wildcard_dict`, `get_access_flag`, `get_region_access_list`, `get_flag_access_list`, `apply_trim_to_card`,
  `remove_trim_from_card`, `add_trim_access_to_card`, `tally_access`, and `setup_tgui_lists`, `setup_access_descriptions`,
  `get_access_desc` as deferred leaves (TGUI's lists, M9). Later: `apply_trim_override`, `remove_trim_override`
  (chameleon, after the core version), `__in_character_record_id_information` (M10).
- **`controllers/subsystem/job.dm`** (with the subsystem): M4a: `New`, `Initialize`'s `setup_occupations` path,
  `setup_occupations` (the job datums, with the config and map checks), `get_job`, `get_job_type`,
  `get_department_type`, `setup_job_lists`. Later: the lobby and round-start assignment, late join, heads and officers,
  the spare ID, `Recover` (M10).
- **`datums/outfit.dm`**: M4a: `equip` (the deferred slots: suit, belt, gloves, shoes, head, mask, neck, ears, glasses,
  back, ID, suit storage, pockets, the backpack and belt contents, the items in hands), `post_equip` (check). Later:
  `apply_fingerprints` (forensics, M9: stays deferred), `get_chameleon_disguise_info` (after the core version),
  `get_types_to_preload`, `get_json_data`, `copy_from`, `save_to_file`, `load_from`, `vv_*` (admin, M10).
- **`modules/jobs/job_types/_job.dm`**: M4a: `D/job.New`, `config_check`, `map_check` (`setup_occupations` reads them),
  `D/outfit/job.pre_equip` (check), `post_equip` (the ID's `registered_name`, `update_label`, `update_ID_card`; the bank
  account a deferred leaf, M10; the PDA imprint a deferred leaf, modular computers after the core version). Later: the
  spawn, announcement, late join, mail, lobby and traitor procs (M10).
- **`modules/mob/living/living.dm`** (only these): M4a: `L.get_access` (tg's `COMSIG_MOB_RETRIEVE_ACCESS`), `get_idcard`,
  `get_id_in_hand`. Later: `get_bank_account` (M10).
- **The dev path:** `DevCommands.Give` (`new TYPE(turf)` then `put_in_hands`); retiring `access`, `dm_dev_access`, the
  human's `get_access` override and DV23 once a P5 test has the card open Meta's doors (plan-a note 5).

### P4

- **`game/objects/items/tools/engineering/weldingtool.dm`**: M4a: `I/weldingtool.Initialize`, `Destroy`,
  `update_icon_state`, `update_overlays`, `process`, `attack_self`, `get_fuel`, `use`, `set_welding`, `check_fuel`,
  `switched_on`, `switched_off`, `light_updated`, `examine`, `get_temperature`, `isOn`, `tool_use_check`, `use_tool`. Later:
  `item_interaction` (refuelling from a tank: M8), `interact_with_atom`, `try_heal_loop` (repairing robotic limbs, M6),
  `afterattack` (igniting, M4b), `screwdriver_act`, `flamethrower_screwdriver`, `flamethrower_rods` (flamethrower assembly,
  M5), `ignition_effect` (M4b), `suicide_act` (M10), `cyborg_unequip` (after the core version), the abductor's and
  experimental welder's `process` (after the core version).
- **`wrench.dm`, `screwdriver.dm`, `crowbar.dm`, `wirecutters.dm`**: M4a: the base tools' `Initialize` (`I/wrench`,
  `I/screwdriver`, `I/crowbar`, `I/wirecutters`), `I/crowbar/large/old.Initialize`. Later: the power tools, the combat
  wrench, the medical wrench, the hammer, the mech removal crowbar, `suicide_act` (after the core version or M10).
- **`modules/reagents/chemistry/holder/holder.dm`** (only these): M4a: `add_reagent`, `remove_reagent`. **`reactions.dm`**:
  `handle_reactions` as a deferred leaf (M8; a test asserts that fuel alone has no reaction). **`reagents.dm`**: M4a:
  `D/reagent.New`, `Destroy`, `on_new`, `on_merge`. **`reagents/other_reagents.dm`**: the welding fuel's data (and nothing
  of its procs: its `expose_*` and `on_mob_life` are M8). Later: the rest of the holder (`trans_to`, `expose`, `multiply`,
  ...: M8).
- **`game/machinery/doors/{door,airlock,windowdoor}.dm`**: M4a: the tool acts and `item_interaction` the live-partial M4a
  stubs name (`welder_act`, `welder_act_secondary`, `try_to_weld`, `try_to_weld_secondary`, `screwdriver_act`,
  `crowbar_act`, `try_to_crowbar`, `wrench_act`, `wirecutter_act` with its wires call a block (hacking, M5/M7),
  `item_interaction` with the ID swipe `try_to_activate_door`), and `poddoor.dm`'s open path from a button.
- **`game/objects/structures/tables_racks.dm`**: M4a: the table's `item_interaction` placement (`table_place` by `icon-x`
  and `icon-y`). Later: deconstruction (M5).
- **`game/machinery/buttons.dm`**: M4a: `/obj/machinery/button.Initialize`, `Destroy`, `setup_device`, `update_icon_state`,
  `update_appearance`, `update_overlays`, `interact`, `attack_hand_secondary`, `attempt_press`, `examine`, `add_context`,
  `/button/door.setup_device`. Later: the panel, assembly and electronics acts and the construction
  (`on_set_panel_open`, `item_interaction`, `assembly_act`, `airlock_electronics_act`, `screwdriver_act`, `wrench_act`,
  `base_item_interaction`, `remove_assembly`, `remove_airlock_electronics`, `on_deconstruction`, `dump_inventory_contents`,
  `I/wallframe/button.*`: M5), `emag_act`, `attack_ai`, `attack_robot` (after the core version), `connect_to_shuttle`
  (shuttles, M10), `get_save_vars` (M10), `/button/curtain.setup_device` (after the core version).
- **`modules/assembly/assembly.dm`, `doorcontrol.dm`**: M4a: `I/assembly.on_attach`, `on_detach`, `pulsed`, `pulse`,
  `activate`, `is_secured`, `toggle_secure`, `Destroy`, `examine`, `I/assembly/control.Initialize`, `examine`, `add_context`,
  `activate`, `/control/blast_door.activate`, `examine`, `/control/airlock.activate`. Later: `holder_movement`,
  `get_part_rating`, `attack_hand`, `item_interaction`, `screwdriver_act`, `ui_host`, `ui_state` (assemblies, M5),
  `multitool_act` (M5), `interact_with_atom` (M5), the mass driver, igniter, flasher, crematorium and curtain controls
  (after the core version).

### P6, P7, T1, T2, I1

P6 and P7 translate no tg procs. T1 and T2 translate none (they test). I1 picks up the M2/M3 `GlobalProcs` mirror cleanup of
the files the M4a packages touched and whatever the integration finds (with the lead's agreement).

## The interfaces each package implements or calls

| Contract | File | Implemented by | Called by |
|---|---|---|---|
| The codec's version-4 layout (the Action's members, the screen entry's group, the private images, the entity flags) behind the existing entry points; `Codec.Encode`/`TryDecode` of `ContextHintsMessage` | `src/Protocol/Codec*.cs` | P6 | the server, the client, the bots |
| `ReplicaWorld.Images`, `ImageChanged`, `ImageRemoved`, `ScreenGroup`; `ReplicaEntity.Flags`, `IsThrown` (complete) | `src/Protocol/ReplicaWorld.V4.cs` | P6 | P7, the bots |
| The host's version-4 actions: validation (interaction.md 2), `ActionLimits.Keybinding`/`Hover`, one hover per tick, the screentip sink, `click_ms` | `src/Server/ServerHost.*.cs`, `Session.cs`, `TokenBucket.cs` | P6 | the clients, the bots |
| `IClientEntryPoints.Keybinding`, `Hover` (default members) | `src/Server/ClientEntryPoints.cs` | C (complete) | P6 |
| `ClientSeam.Keybinding`, `Hover`, `ClickParams(button, shift, ctrl, alt, iconX, iconY, screenLoc)`; `Click` with turf and screen targets | `Tg/Infrastructure/ClientSeam.*.cs` | P6 | the host |
| `ClientSeam.KeybindingType` (the code table) | `Tg/Infrastructure/ClientSeam.Actions.cs` | C (complete) | P6 |
| `ScreentipSeam.Publish`, `Install`, `SinkOf`, `TryKey`, `ContextKeys`; `IScreentipSink`; `ScreentipHint` | `Tg/Infrastructure/ScreentipSeam.cs` | C (complete) | P1 (`on_mouse_enter`), P6 (the sink) |
| `InertSeam.GuardedProcs` (complete), `HasUndeclaredOverride`, `MissingOverride` | `Tg/Infrastructure/InertSeam.cs` | P1 | P1, P2, P3, P4 (the sites), T1 |
| `Generated/Types/UndeclaredOverrides.g.cs` (`Has(Type, string)`) | the types tool | P1 | `InertSeam` |
| `DevCommands.Give` | `Tg/Infrastructure/DevCommands.cs` | P5 | `ServerHost.Dev` |
| `IWorldPicker.Pick`, `IHudPicker.PickScreen`; `PickRay`, `PickHit`, `PickSurface` | `godot/scripts/Shared/Play/IWorldPicker.cs` | P7 | P7's `GameScene`, the radial, tests |
| `IContainerView` (`Rebuild`, `Cells`, `Columns`, `Rows`, `IsOpen`, `Group`); `ContainerCell`, `ContainerCellRole` | `godot/scripts/Shared/Play/IContainerView.cs` | P7 | P7's windows, tests |
| `FakeServerOptions.Inventory` and the fake's version-4 behaviour (interaction.md 8) | `godot/scripts/Shared/FakeServer.cs` | P7 | P7's tests, `--fake-server` |
| `IWorldFeed.ContextHintsReceived` (default accessors) | `godot/scripts/Shared/IWorldFeed.cs` | P7 (`GameClient`, `FakeFeed`) | P7's HUD and radial |
| every tg skeleton (`slots.tsv` signatures, `DmTask` exactly where `suspends = yes`) | the mirror files above | the file's owner | every package |

## `live-partial.txt` sections

| Section | Owner | Expected rows |
|---|---|---|
| `## P1 (M4a)` | P1 | the screentip elements' and the click chain's leaves on types P1 makes live (few: P1 mostly translates bases) |
| `## P2 (M4a)` | P2 | the screen objects P2 makes live with later procs (`S/inventory`'s and `S/zone_sel`'s drag procs, M9), the HUD datums' action procs (M9) |
| `## P3 (M4a)` | P3 | the closet's, crate's and storage item types' later procs (deconstruction and electronics M5, air M4b, resisting M9, EMP M7, admin M10), so that Meta's closets, crates, toolboxes, backpacks, boxes and belts are live |
| `## P4 (M4a)` | P4 | the welder's and the tools' later procs (flamethrower M5, healing M6, igniting M4b), the button's and the assemblies' construction (M5), so that the welder, the four tools, the wirecutters and Meta's buttons are live; P4 also rewrites M3's `## P3 (M3)` rows of the door family that M4a translates (with the lead: the lead moves those rows to P4's section first) |
| `## P5 (M4a)` | P5 | the ID card's and the trims' later procs (holopay and money M10, chameleon, prisoner), so that `/obj/item/card/id` and `/advanced` with the silver, gold and debug subtypes are live |
| `## I1 (M4a)` | I1 | whatever the integration finds |
