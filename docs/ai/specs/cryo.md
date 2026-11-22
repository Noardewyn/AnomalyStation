# M8x Cryo cells and sleepers: design and plan

**Revised (D216, the owner, 2026-10-22): the sleeper is not ported, since MetaStation maps none. M8x is the cryo cell alone; the sleeper parts below (slice B's sleeper, its window, D206, D207, D213) are superseded. The Cryo window and the beaker display from slice B stay.**

Written 2026-10-22 against main (0.8.0, protocol 35). tg = `../tgstation/code` (snapshot 1d99bd7f). Paths without a prefix are in the repo,
`Tg/` = `src/Core/Tg/`, `Gen/` = `src/Core/Tg/Generated/Types/`. Run autonomously under D178: every choice is a "my choice" row (section 6),
numbered D205-D215 here (DV116+ as the slices write them). Format follows `docs/ai/specs/medicine.md`; the spec lands as `docs/ai/specs/cryo.md`.

## 0. What already exists (verified)

Generated stubs (all `DmGeneratedOnly, DmInert`: vars and constructor only, no procs):
- `/obj/machinery/cryo_cell` `Gen/obj.machinery.g.cs:10245-10256` (autoeject, efficiency, heat_capacity 20000, conduction_coefficient 0.3,
  beaker, occupant_vis, treating_wounds, internal_connector). Note: in this snapshot the cell is NOT a unary atmos device: it is a plain
  machine that owns a `/datum/gas_machine_connector` (tg `cryo.dm:68`, `:117`; `machine_connector.dm`, 146 lines), stub `Gen/datum.g.cs:34431-34437`.
- `/atom/movable/visual/cryo_occupant` generated (tg `cryo.dm:14-65`); `/datum/looping_sound/cryo_cell` `Gen/datum.looping_sound.g.cs:898`
  (cryo_1..10.ogg; not inert, the looping-sound base is translated, `Tg/datums/looping_sounds/_looping_sound.cs`).
- `/obj/machinery/sleeper` `Gen/obj.machinery.g.cs:4324-4359` (possible_chems tiers, min_health -25, efficiency) and `sleeper/syndie` `:4363`.
- Boards: `circuitboard/machine/cryo_tube` `Gen/obj.item.circuitboard.g.cs:1930` (matter bin, cable, 4 glass; tg `machine_circuitboards.dm:853`),
  `circuitboard/machine/sleeper` `:2053` (matter bin, servo, cable, 2 glass; tg `:922`), syndie/fullupgrade/party `:2073-2102`.
- Stock parts `datum_stock_part_matter_bin`/`servo` tiers generated (`Gen/datum.g.cs:53505`, `:53603`); M5 slice 7 frames build a machine
  from its board and default parts (`Tg/game/machinery/_machinery.cs:16-66`, `:926`, `:1059`).
- The drag receivers of both machines are refused rows (`docs/ai/specs/not-translated.md:707`, `UndeclaredOverrides.g.cs`).
- GAPs waiting for the type: `Tg/modules/mob/living/carbon/human/life.cs:167` (handle_environment skipped in a cell, tg `life.dm:109`) and
  `human/_species.cs:1255` (body temperature regulation suspended in a cell, tg `_species.dm:1048`).
Machine base (translated, M5/M7): `set_occupant` `_machinery.cs:218`, `begin_processing` `:227`, `open_machine` `:341`,
  `dump_inventory_contents` `:384`, `close_machine` `:436`, `update_use_power` `:479`, `default_pry_open` `:857`,
  `default_deconstruction_screwdriver` `:1099`, `default_change_direction_wrench` `:1111`. Pattern for an occupant machine with a drag
  receiver: `Tg/game/machinery/suit_storage_unit.cs`. `GLOB.notcontained_state` `Tg/modules/tgui/states/notcontained.cs:17`.
Atmos (M4/M7): unary base `Tg/modules/atmospherics/machinery/components/unary_devices/unary_devices.cs:16` (the connector's
  `gas_connector` is a bare unary); the freezer `thermomachine.cs` (498 lines; window `:372`; tier-1 min 73.15 K, tg `thermomachine.dm:95`);
  `unsafe_pressure_release` (components_base.cs:435 caller); trinary filter `trinary_devices/filter.cs`; anesthetic-mix canister
  `Tg/modules/atmospherics/machinery/portable/canister.cs:594`; N2O sleep in the lungs `lungs/_lungs.cs:793 too_much_n2o`.
Breathing and temperature (M2/M4): breathing from the container `carbon/life.cs:119-194` -> `obj.handle_internal_lifeform` `Tg/game/objects/objs.cs:234`;
  `adjust_bodytemperature` `Tg/modules/mob/status_procs.cs:33`, `adjust_coretemperature` `human/life.cs:174`, `get_cold_protection`
  `carbon/life.cs:494` / `human/life.cs:357`; `IS_UNCONSCIOUS` `Tg/__DEFINES/mobs.cs:43`; `extinguish_mob` `living.cs:1763`.
  NOT translated: `/mob/living/proc/get_organic_health` (tg `living.dm:611`, returns health), `aas_config_announce` (tg
  `announcement_system.dm:233`; `/datum/aas_config_entry` inert `Gen/datum.g.cs:10669`).
Reagents (M8): holder `trans_to` with methods `Tg/modules/reagents/chemistry/holder/holder.cs:184`, `expose` `:515`; wounds' `on_xadone`
  `Tg/datums/wounds/_wounds.cs:683`. Cryoxadone `Gen/datum.reagent.medicine.g.cs:424` and pyroxadone `:443` are inert stubs (base behaviour,
  medicine.md:249 R4); mutadone `:1076` inert. Every tier-1 sleeper chem is translated (D182: epinephrine, morphine, convermol, libital,
  aiuri). GLOB.chemical_reagents_list is not built (D197): the type prototype stands in (sleeper `ui_data` reads it).
Stasis: `/datum/status_effect/grouped/stasis` inert `Gen/datum.status_effect.g.cs:436`; `TRAIT_STASIS` checked `_species.cs:1258`. Neither
  machine uses stasis in this snapshot (grep of `cryo.dm`, `sleepers.dm`: no stasis); only `machinery/stasis.dm` and body bags do.
tgui: windows are C# renders keyed by interface name, `godot/scripts/Shared/Tgui/TguiWindows.cs:18-37`; pattern
  `Interfaces/ThermoMachine.cs` (88 lines, a translated `ThermoMachine.jsx`). UiKind has Section, LabeledList, ProgressBar, Button, Box, NoticeBox
  (`UiNode.cs:12-122`); tg's AnimatedNumber renders as text. Not ported: `interfaces/Cryo.tsx` (150), `common/BeakerDisplay.tsx` (145),
  `Sleeper.jsx` (123).
Client/replication: a mob inside a machine is not an entity (`docs/ai/specs/replication.md:612`); the replicator already sends atoms in a
  host's `vis_contents` as visuals with `NetField.VisHost` (protocol 10; `src/Server/Replication/Replicator.cs:857-868` NoteVisuals, today only
  flick visuals and firing effects; client `godot/scripts/EntityLayer.cs:40`, `Shared/World/VisualAnchor.cs`). Sprites present:
  `godot/assets/tg/icons/obj/medical/cryogenics.{png,json}` (pod-off/on/open, pod-panel, cover-off/on, pod-open-anim, pod-close-anim, mask,
  mask_bg), `icons/obj/machines/sleeper.{png,json}` (sleeper, -anim, -open, -o, sleeper_s...). Sounds: `godot/assets/tg/sound/machines/cryo/`
  and `machines/cryo_warning.ogg`. No 3D model for either (`godot/assets/models/machines/` has none).
Tests to lean on: `tests/Anomaly.Core.Tests/M7/ThermomachineTests.cs:475` (machine_disassembly redone), `tests/Anomaly.Server.Tests/Integration/
  ThermomachineGameplayTests.cs` (a real client opens the freezer window), `M8ScenarioTests.cs:119` (`InMedbay<T>` finder, `Beside`).

MetaStation (tg map coords, from `MetaStation.dmm`): the Cryo room `/area/station/medical/cryo` is x78-83, y107-112, north of the Treatment
Center (y97-105). Cells at 80,111 (key xYM) and 82,111 (hCy), default dir SOUTH onto the cyan manifolds at 80,110/82,110. The cyan loop
(visible manifolds 79-83, y107-110) carries the freezer at 79,107 (dir EAST, off at round start), two anesthetic-mix canisters on
portables connectors at 81,107 and 83,107 (dir NORTH: they fill the loop with O2 + N2O), and a CO2 filter at 81,110 to the scrubbers. The
glass table at 79,111 holds 4 `beaker/cryoxadone`, a mannitol pill bottle and a dropper; 80,107 a medical wrench and 2 stethoscopes; vitals
displays at 79,111 and 83,111. Meta maps NO sleeper: its only "sleeper" is a `showcase/machinery/oldpod` "decommissioned sleeper" (dmm:56510).
Stasis beds at 79,105 and 85,105 (Treatment Center) stay inert.

## 1. Scope boundary (D205)

IN:
- `machine_connector.dm` whole (the connector datum: New, Destroy, the move/rotate/deconstruct handlers, reconnect).
- `cryo.dm` but `aas_config_announce` calls (Deferred, D209), `on_changed_z_level` (one z), `contents_explosion` (explosions out) and the
  hydroponics hook: the occupant visual (`:14-65`), Initialize/Destroy, deconstruct, `Exited`, `examine`, icon state and overlays (`:214-228`),
  beaker insert (`:230`), screwdriver/crowbar/wrench (`:248-319`), `set_occupant`, `RefreshParts` (`:330`), `dump_inventory_contents`,
  `set_on` with `COMSIG_CRYO_SET_ON`, begin/end processing (sound loop), `on_set_is_operational`, `process` (`:387`: dead/fully healed/wounds,
  autoeject, the beaker's VAPOR transfer `CRYO_TX_QTY / (efficiency * 25)` per second at multiplier `efficiency * 25`), `process_atmos` (`:441`:
  CRYO_MIN_GAS_MOLES 5, burn above 2000 K, the conduction share with the body and core temperature), `handle_internal_lifeform`,
  `assume_air`, `return_temperature`, open/close with flicks and auto-on, `container_resist_act` (30 s), the tgui (`:530-618`: Cryo window,
  power/door/autoeject/eject), ctrl-click (on), alt-click (door), drag receiver (`:645`), `get_remote_view_fullscreens`; `add_context` only if
  screentips exist (else a row). Close the two GAPs (`human/life.cs:167`, `_species.cs:1255`); translate `get_organic_health`.
- `sleepers.dm`: the sleeper and `syndie` (vars only, controls inside), all procs: Initialize (mapload board removal), `RefreshParts` (tiers),
  icon state, resist/Exited/relaymove exits, open/close with flick and enter message, `emp_act`, drag receiver, tools, `ui_data`/`ui_act`
  (door, inject: min_health and the epinephrine exception, `chem_allowed` 20 x efficiency cap), `emag_act` (scrambled buttons), `inject_chem`,
  `reset_chem_buttons`, `process` (power). Party/old/self_control/survival_pod variants stay generated (not mapped).
- Reagents: cryoxadone and pyroxadone `on_mob_life` (`medicine_reagents.dm:154-216`: temperature-gated power, x2 unconscious, wounds' xadone
  progress, disfigurement removal). Cryoxadone heals only below T0C (273.15 K); its metabolization rate scales with T^2. Pyroxadone above
  BODYTEMP_HEAT_DAMAGE_LIMIT (none on Meta; translated for completeness, 30 lines).
- Boards and construction: both boards' build path and parts through M5's frames; deconstruction gives board, parts and frame.
- tgui: `Cryo.tsx` + `common/BeakerDisplay.tsx` (`BeakerSectionDisplay`) and `Sleeper.jsx` as C# renders.
- Client: the cell's and the sleeper's icon states, overlays and flicks; the patient visible in the cell (D210); the impaired fullscreen
  inside; the cryo sound loop, `cryo_warning.ogg`, the machines' open/close sounds as tg plays them.
OUT (D205-D213): stasis beds and the stasis effect (D208), the vitals display (D212), cryo radio announcements (D209), the party pod and
other variants, the RPED part swap (D132 inert), mutadone's own effect (D213), cryostylane and other cold reagents, chemistry machines.

## 2. Slices (dependency order)

Agent fit (2 at a time: one Opus + one Sonnet, no helpers). A (atmos seam, temperature, strict-world leaves) and C (replication and client
seam) take judgement -> Opus; B is mechanical against tg -> Sonnet. Order: A (Opus) beside B (Sonnet) -> C (Opus) -> closing (main).
Every slice: D142 visuals/sounds with the mechanic, CHANGELOG line, not-translated/live-partial rows closed or retagged, filtered tests;
full Core and Server suites only at the close.

### Slice A. The cryo cell and its reagents - Opus, tg ~0.75k
- tg: `machine_connector.dm` 146; `cryo.dm` ~560 of 690 (section 1); `medicine_reagents.dm:143-216`; `living.dm:611`; the two GAP sites.
- Ours: new `Tg/modules/atmospherics/machinery/components/unary_devices/machine_connector.cs` and `cryo.cs` (drop `DmInert` from both stubs
  via the converter's inert list); `Tg/modules/reagents/chemistry/reagents/medicine_reagents.cs` (two procs); `living.cs`; `human/life.cs`,
  `_species.cs` GAP closures; the cell's tgui data server side (window name "Cryo"). Every proc on the path that reaches a NotTranslated leaf
  (log_message, aas, balloon alerts if any) gets `Dm.Deferred` + a row.
- Core tests `tests/Anomaly.Core.Tests/M8x/CryoCellTests.cs`: the connector joins a pipe and shares one pipenet (volume CELL_VOLUME/2); closing
  on an occupant turns it on, opening turns it off; under 5 moles it turns off and ejects; the body/air conduction step equals tg's
  `CALCULATE_CONDUCTION_ENERGY` formula (`cryo.dm:470-477`) and core temperature follows; the beaker gives 0.04 u out, 1 u in per 2 s at
  efficiency 1; cryoxadone heals by `-0.00003 T^2 + 3` (x2 unconscious) times the damage factors and none at T >= 273.15; pyroxadone's
  three bands; fully healed -> off + autoeject; wounds only -> keeps treating (treating_wounds); dead -> eject; resist breaks out after 30 s;
  occupant skips handle_environment and body regulation; tg `machine_disassembly` redone for the cell (board, matter bin, frame) and built
  back from a frame. A Meta-boot test: both cells' connectors find their cyan manifold and hold more than 5 moles at round start.
- Visuals/sounds: server side only (icon states, overlays, flicks and the sound loop are tg calls; C draws them).

### Slice B. The sleeper and both windows - Sonnet, tg ~0.3k + tsx ~0.42k (beside A)
- tg: `sleepers.dm:1-305`; `tgui/.../interfaces/Sleeper.jsx`, `Cryo.tsx`, `common/BeakerDisplay.tsx` (the Cryo keys are tg's `ui_data`,
  `cryo.dm:543-585`, so B does not wait for A).
- Ours: new `Tg/game/machinery/sleepers.cs`; `godot/scripts/Shared/Tgui/Interfaces/Sleeper.cs`, `Cryo.cs`, `Common/BeakerDisplay.cs`,
  registered in `TguiWindows.cs`.
- Core tests `M8x/SleeperTests.cs`: tier-1 parts give the five tier-1 chems; tier-3 servos add tier 2 and 3 lists; below min_health only
  epinephrine is allowed; a chem over `20 x efficiency - 10` in the blood is refused; inject adds 10 u; the door, relaymove and resist exits; emag
  scrambles the buttons but checks the intended chem; the window refuses the occupant (notcontained); a mapped sleeper has no board, a built one
  does; `machine_disassembly` redone. Client: a render test of each window from a captured `ui_data` (as the ThermoMachine one).
- Visuals/sounds: none server side beyond tg's calls (flick `sleeper-anim`, state -open/-o); C draws them.

### Slice C. Client visuals, sounds and gameplay tests - Opus, ours ~0.4k (after A and B)
- Replication: the cell's `occupant_vis` and the mob in its `vis_contents` become visuals on the cell (extend `NoteVisuals`,
  `Replicator.cs:857`), the bob `animate` (pixel_y 22 <-> 24, 2 s each way) through the existing AnimationRecorder; a protocol bump (36) only if
  a field is missing (D210). Client (`EntityLayer`): the cell's sprite states and `cover-on/off` overlay over the patient, the patient's 3D
  body standing in the cell (TRAIT_FORCED_STANDING) clipped to the glass, the flicks `pod-open-anim`/`pod-close-anim`; the sleeper's
  `sleeper`/`sleeper-open`/`sleeper-o` with `sleeper-anim`, its occupant hidden as tg; the impaired fullscreen for the occupant; the sounds.
- Gameplay tests (`tests/Anomaly.Server.Tests/Integration/`, real server, real clients, as ThermomachineGameplayTests):
  `CryoGameplayTests` on MetaStation: a doctor opens the cryo room, turns the freezer on at 73.15 K in its window, inserts a mapped
  cryoxadone beaker into 80,111 by a click, drags the patient in, sees pod-off -> pod-on and the occupant visual, hears the loop, reads the
  Cryo window (temperature, beaker), ctrl/alt clicks work, the warning sound on full heal and the autoeject. `SleeperGameplayTests`: a
  sleeper built from a frame, its board and parts in the Treatment Center (D207), a drag in, the window's door and injections, flicks seen.
- Visual check with its own server and scripted clients (as M8's): screenshots of the cell open/closed/on with a patient, the sleeper
  open/closed, both windows.

### Closing (main session)
- `M8xScenarioTests` (the done-when, D214; seed fixed, run twice: same hash); M8's medical tests and the M7 freezer tests rerun; full Core and
  Server suites; ROADMAP M8x status and the sleeper wording (D206); CHANGELOG; release 0.8.1 (lettered milestone raises PATCH,
  `docs/versioning.md:20`), protocol 36 only if C needed it.

## 3. Done when (ROADMAP M8x) on MetaStation

`M8xScenarioTests`, three real clients (Alice in tg's medical doctor outfit via `outfit`, Bob the patient, Carol a watcher):
1. Sleeper: Bob is put in hard crit by dev commands (D193 pattern: brute, burn, oxyloss; health below -25). A sleeper is spawned on a clear
   Treatment Center tile (D207). Alice drags Bob onto it (tg's drag receiver), closes it with alt-click; her window shows "Critical"/bad
   and only epinephrine allowed; she injects epinephrine, then (health above -25) libital, aiuri, convermol as the buttons allow. Assert:
   every injection is tg's 10 u and respects `chem_allowed`; Bob leaves hard crit and does not fall back for 60 s; the window data reached
   Alice's session only.
2. Cryo: Bob, revived to conscious with brute and burn left (dev commands), is taken by Alice into the cryo room. She turns the freezer at
   79,107 on at its minimum 73.15 K, takes a cryoxadone beaker from the table at 79,111, inserts it into the cell at 80,111, drags Bob in; the
   cell closes and turns on. Assert per SSair/SSmachines step: his body temperature follows tg's conduction formula toward the loop's
   temperature; no healing while above 273.15 K; below it, each metabolism tick heals by cryoxadone's formula at his temperature (x2 while
   the loop's N2O keeps him asleep); fully healed, the cell turns off, plays `cryo_warning.ogg` and ejects him (autoeject). Clients saw the
   occupant visual and pod-on, heard the loop.
3. The treatment part run twice from a fresh boot ends with the same state hash; no runtime errors beyond the boot's. Adapted tg tests pass
   (section 2: machine_disassembly x2; metabolizing/reagent_names now run the two xadones' real procs).

## 4. Risks

- R1 Gas at round start: the cells need more than 5 moles in their connector. The loop is filled only by the two anesthetic canisters on
  connectors; if they do not connect at mapload or the connector's pipenet misses the manifold, the cell refuses. A's Meta-boot test gates it.
- R2 N2O from the anesthetic mix sleeps the patient (`_lungs.cs:793`), doubling cryoxadone's power, and reaches him through
  `handle_internal_lifeform`; asserts read his actual stat each tick rather than assume it.
- R3 Cold injury: if either GAP closure misses, a patient at 73 K takes cold damage tg never gives; CryoCellTests checks no cold damage.
- R4 The occupant visual is new client work (the mob is not an entity inside a machine); if the VisHost path cannot carry a mob's 3D body,
  protocol 36 with one field, recorded as a DV.
- R5 Dragging a downed body onto a machine in first person (DV45 drag message) must reach `mouse_drop_receive` with NEED_DEXTERITY and
  adjacency; the gameplay tests exercise it with real clients.
- R6 Determinism: the cell's SSair machine processing and the sound loop's random file pick go through the world RNG; hash two runs.
- R7 Strict worlds: examine, screentips (`add_context`), `log_message`, `balloon_alert` and aas paths reach leaves; each is translated or
  `Dm.Deferred` with a row.
- R8 The ROADMAP's sleeper text (stasis-like slowing, syringe, beaker) describes an older tg; this snapshot's sleeper has none (D206).

## 5. Size and order summary

| Slice | Agent | tg lines | ours | after | beside |
|---|---|---|---|---|---|
| A cryo cell + connector + xadones | Opus | 0.75k | 0.05k | - | B |
| B sleeper + Cryo/Sleeper/BeakerDisplay windows | Sonnet | 0.3k + 0.42k tsx | - | - | A |
| C client visuals, sounds, gameplay tests | Opus | - | 0.4k | A, B | - |
| Closing (scenario, suites, release) | main | - | 0.2k | all | - |

## 6. Decided

The choices are DECISIONS.md rows D205-D215 (my choices under D178).
