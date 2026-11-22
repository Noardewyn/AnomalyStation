# Wave 4 file ownership

Status: v1 (wave-4 contracts, W4.0), 2026-10-06; the rows for files created later in wave 4 were added at the merges and checked against
the created files at the end of wave 5. Who edits which file while the four wave-4 packages translate in
parallel: A movable (the Move chain), B turf and area, C mob, obj and timed actions, D support (the scout's scope report,
section 2.2, as amended by the lead's plan for wave 4). The W4.0 contracts commit declared everything two packages share:
each file below exists (as a skeleton, a partial mirror or complete) before the packages start, so that every package
compiles against the others' declarations and edits only its own files. Paths are under `src/Core/Tg/` unless they start
with `src/`, `docs/` or `tests/`.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new mirror files that no row names
  (for example a skeleton of a type its code needs, a test fixture, the file of a macro only it uses) and lists them in
  its report. A file that another wave created and no row names here is not edited: report the needed member.
- **Skeleton signatures are frozen.** A skeleton (`// SKELETON:`) or partial mirror (`// PARTIAL:`) declares members
  with the signatures of `docs/ai/specs/generated/slots.tsv` and the rules; other packages call them. The owner completes the
  bodies and may add members (overrides of slots rooted elsewhere, procs of later milestones as `Dm.NotTranslated` with
  the milestone named), but changes no declared signature, type or name without the lead (report it). A needed member in
  a file a package does not own is reported to the lead (a `// GAP(<owner>): ...` comment at the call), not added.
- **Var blocks are done.** W4.0 wrote the var blocks of the six big files and of the toy types as real translation
  (rules 10.2), including tg's overrides of BYOND's builtin types (`dm_tg_vars`, rules 10.2 "Builtin types", lead
  decision D1) and the object-creating defaults (`dm_init_vars`). A package fixes a var it finds wrong in its own file
  and reports it.
- **Overrides are not declared.** The skeletons declare the roots of the slots their files define; an override of a slot
  rooted elsewhere (`Initialize`, `Destroy`, `New`, `Entered`, `Exited`, `Enter`, `CanAllowThrough`, `Move` of a
  subtype, `Process_Spacemove` of `/mob`, ...) is added by the owner of its file, so that the translated base keeps
  running until then.
- **Bodies are `Dm.NotTranslated` until their owner lands.** A test that reaches another package's skeleton member
  throws, as rules 1.4 intends. Test your own code with fixtures (fixture types `/<kind>/test_<package>_<name>`, rules
  15), or, for a path that crosses another package before the merge, run that world with
  `DmWorldState.NotTranslatedThrows = false` and assert only on what your package owns. **Merge order: D, A, B, C**
  (D is pure and unblocks the `movement_order_sanity` fixture; A before B so that B's `Enter` goldens run on the real
  `Move`; C last because `mob/Initialize` reaches D's modifiers and `mob_lists`).
- **Test-writers** (lead plan, "Execution"): independent test-writers (Sonnet) for A, B and C start in their package's
  worktree when its translator finishes; D tests itself (the ported tg tests `connect_loc_basic` and
  `connect_loc_multiple_on_turf`, plus behaviour tests of `movement_detector`, the modifier arithmetic, `mob_lists`,
  `string_list`, `get_nested_locs` and the multiz helpers). After all merges, W4.M (Sonnet, worktree `w4-integration`)
  writes the integration tests on real types (move-chain.md section 11 goldens T1 to T12 on a toy room,
  `movement_order_sanity`, the three `do_after` scenarios of `timed-actions-helpers.md` section 8, the
  `create_and_destroy` extension, determinism) and owns the shared test helper `TgToyRoom`.
- **Deferred leaves and stubs.** `Dm.Deferred` (rules 1.4) only for leaves whose effects nothing in M0 reads; inert stub
  subsystems keep tg's `[DmPath]`, `name`, `dependencies`, set `SS_NO_INIT | SS_NO_FIRE` and carry a `// STUB(Mx)` line.
  A package may append rows to `docs/ai/specs/not-translated.md` for its own; it reports every other new one.
- **Shared documents** (`docs/ai/DEVIATIONS.md`, `docs/ai/translation-rules.md` Appendix A and C, `docs/ai/specs/dm-conformance.md`,
  `docs/ai/specs/shim-ownership.md`): the lead at merge; packages report additions (a new deviation with its
  `// DEVIATION(DVn)` text, a decided slot, a new public name). DV12 (emissive blockers, package A) and DV13 (cogbar and
  progress bar, package C) are already in the register.
- **The shim** (`src/Core/DmShim/`) and the Kernel are not edited by the packages; a missing member is reported to the
  lead (`// SHIM-GAP: ...` at the site).
- **Things every package should know** (W4.0 findings):
  - `/atom/proc/has_gravity` reads `SSmapping.gravity_by_z_level[z]` (`_atom.dm:800`); the SSmapping stub leaves the list
    empty, so a test world must create SSmapping (`Dm.@new<datum_controller_subsystem_mapping>()`) and grow the list to
    `world.maxz`, as tg's `manage_z_level` does (`controllers/subsystem/mapping.dm:756`), or `has_gravity` raises "list
    index out of bounds" (example: `tests/Anomaly.Core.Tests/Tg/Wave4/Contracts/AtomGapEditTests.cs`).
  - A resource literal (`'icons/x.dmi'`, `'sound/y.ogg'`) is written as the text of its path, with a `// RULE-GAP:` line
    (no resource value type yet; W4.0 rule gap). `image()` takes text in the icon position as the icon.
  - `step(Ref, Dir)` (`Builtins`) returns `Move`'s own value, or 0 without calling `Move`.
  - The toy area is `/area/misc/testroom` (STANDARD_GRAVITY); `/area/space` (tg's `world.area`) has NO_GRAVITY.

## Files by owner

| File (port) | Owner | State after W4.0 | Others use |
|---|---|---|---|
| `game/atoms_movable.cs` | A | skeleton: the var block, `dm_tg_vars`, the roots of `atoms_movable.dm` that M0 reaches (`Move` and `Move__d0`, `Moved`, `forceMove`, `moveToNullspace`, `doMove`, `abstract_move`, `Bump`, the pulling procs, `set_glide_size`, the z-move family, the hearing and area sensitivity procs, `Process_Spacemove`, `newtonian_move`, `CanPassThrough`, `has_buckled_mobs` ...) and the `/mob` procs of the file (`enable_client_mobs_in_contents`, `clear_important_client_contents`) | B, C, D |
| `__DEFINES/spatial_gridmap.cs` (partial: `GET_SPATIAL_INDEX`, `HAS_SPATIAL_GRID_CONTENTS`); `_onclick/adjacent.cs` also holds `/turf` and `/area` `Adjacent` and `/turf/ClickCross`; `game/atoms_movable.cs` also holds `/mob/living/Move_Pulled` | A (created or extended in wave 4) | partial | B, C |
| `game/objects/buckling.cs` | A | skeleton: the `/atom/movable` var block, `has_buckled_mobs`, `unbuckle_all_mobs` | C |
| `datums/drift_handler.cs`, `datums/move_manager.cs`, `controllers/subsystem/throwing.cs` | A | skeletons: `datum_drift_handler` (`New`, `newtonian_impulse`: M4), `datum_movement_packet`, `datum_thrownthing` (`finalize`: M5; SSthrowing itself is not declared) | - |
| `__DEFINES/directional.cs` | A | partial: `REVERSE_DIR` translated | B, C, `_atom.cs` |
| `__DEFINES/movement.cs` | A | not created yet (`DELAY_TO_GLIDE_SIZE`, `MOVEMENT_ADJUSTED_GLIDE_SIZE`, `CHECK_MOVE_LOOP_FLAGS`) | - |
| `_onclick/adjacent.cs` | A | skeleton: `/atom/proc/Adjacent` (the pulling code and the timed actions call it) | C |
| `modules/language/_language_holder.cs`, `modules/visuals/render_steps.cs`, `game/objects/structures/lattice.cs`, `datums/elements/climb_walkable.cs` | A | types only (`datum_language_holder`, `atom_movable_render_step_emissive_blocker`, `obj_structure_lattice`, `obj_structure`) | - |
| `game/turfs/turf.cs` | B | skeleton: `GLOB.station_turfs`, the var block, `dm_tg_vars` (with `can_atmos_pass` of `LINDA_system.dm:15`), the roots M0 reaches (`initialize_occupant`, `change_area`, `on_change_area`, `multiz_turf_*`, `is_blocked_turf`, `zPassIn/Out`, `zAirIn/Out`, `zFall`, `zImpact`, `assemble_baseturfs`, `levelupdate`, `RemoveLattice`, `Distance*`) | A, C, D |
| `game/turfs/open/_open.cs`, `game/turfs/closed/_closed.cs`, `game/turfs/closed/indestructible.cs`, `game/turfs/open/space/space.cs` | B | the `/turf/open` var block and constructor, `transfer_occupant`, `/turf/open/indestructible` and its plating; the `/turf/closed` constructor; `/turf/closed/indestructible`; the `/turf/open/space` constructor and `/turf/open/space/basic` (its `New` is B's) | A, C |
| `modules/atmospherics/environmental/LINDA_turf_tile.cs` | B | partial: the `/turf`, `/turf/open` and `/atom/movable` var blocks (`pressure_resistance`), `datum_excited_group` | C (`/mob` overrides `pressure_resistance`) |
| `game/area/areas.cs`, `game/area/areas/misc.cs` | B | skeleton: the var block, `dm_tg_vars` (with `luminosity` of `lighting_area.dm:2`), `dm_init_vars`, `GLOB.teleportlocs`, `process_teleport_locs`, the contents and power roots M0 reaches, `area_space`; partial `misc.cs`: `/area/space`'s constructor, `/area/misc`, `/area/misc/testroom` | A, C |
| `game/turfs/open/space/space_EXPENSIVE.cs`, `controllers/subsystem/area_contents.cs` (live `SSarea_contents`), `controllers/subsystem/overlays.cs` (partial: `add_overlay`, `cut_overlay`), `datums/alarm.cs` (partial), `__HELPERS/areas.cs` (partial), and type-only skeletons for root signatures: `game/objects/items/rcd/RCL.cs`, `game/objects/items/stacks/{stack,rods}.cs`, `stacks/sheets/{sheets,mineral}.cs`, `stacks/tiles/{tile_reskinning,tile_iron}.cs`, `game/objects/effects/decals/{decal,cleanable}.cs`, `decals/cleanable/mess.cs`, `game/machinery/doors/door.cs`, `modules/fishing/fishing_rod.cs`, `modules/power/pipecleaners.cs`, `modules/transport/tram/tram_floors.cs`, `game/area/areas/shuttles.cs` | B (created in wave 4) | complete / partial / type-only | A, C |
| `controllers/subsystem/area_contents.cs` | B | not created yet (a live subsystem: B adds it with its boot test) | - |
| `modules/lighting/lighting_area.cs`, `static_lighting_area.cs`, `lighting_turf.cs` | B | the `/area` lighting var blocks, `update_base_lighting`; `transfer_area_lighting` deferred | - |
| `game/atom/atom_appearance.cs`, `controllers/subsystem/overlays.cs` | B | skeletons: the roots of `update_appearance`, `update_name`, `update_desc`, `update_icon`, `update_icon_state`, `update_overlays`; `build_appearance_list`, `cut_overlays`, `cut_overlay`, `add_overlay` (`/area/Initialize` reaches `update_appearance` through `power_change`; `/turf/Initialize` calls `add_overlay` for space; SSoverlays is not declared) | A, C |
| `datums/alarm.cs`, `__HELPERS/areas.cs`, `game/turfs/baseturf_skipover.cs` | B | `datum_alarm_handler` (type only); `require_area_resort`, `get_sorted_areas`; `turf_baseturf_bottom` (type only) | - |
| `game/objects/objs.cs` | C | skeleton: the var block, `dm_tg_vars`, `GLOB.objects_by_id_tag`, `check_on_table` | A, B |
| `modules/mob/mob_defines.cs` | C | complete (vars only): the var block, `dm_tg_vars`, `dm_init_vars`, `next_mob_id` (the `TgState` field `mob__next_mob_id`) | A, B, D |
| `modules/mob/mob.cs`, `mob_movement.cs`, `modules/keybindings/focus.cs`, `modules/mob/dead/observer/observer.cs`, `modules/mob/inventory.cs`, `controllers/subsystem/ambience.cs` | C | skeletons: the `/mob` and `/atom` roots of `mob.dm` M0 reaches (`set_hud_image_active/inactive`, `prepare_huds`, `visible_message`, `audible_message`, `update_incapacitated`, `reset_perspective`, `spin`, `set_nutrition`, `set_stat`, `setup_hud_traits`, ...); `mob_negates_gravity`; `set_focus`; `ghostize`; `get_active_held_item`; `update_ambience_area` (SSambience itself is not declared) | A, B, D |
| `__DEFINES/client.cs` (partial: `IS_FAKE_KEY`), `__HELPERS/_level_traits.cs` (partial: `is_secret_level`), `modules/awaymissions/cordon.cs` and `game/objects/structures/tables_racks.cs` (type-only skeletons); `mob.cs` also declares `/datum/memory_panel` | C (created in wave 4) | partial / type-only | A, B |
| `modules/mob/login.cs`, `logout.cs`, `__HELPERS/logging/mob.cs` | C | not created yet | - |
| `__HELPERS/logging/_logging.cs` (extend), `__HELPERS/logging/attack.cs` | C | `_logging.cs` as before W4.0 (C adds `key_name` and the others); `attack.cs`: `log_combat` deferred | A |
| `datums/timed_actions.cs`, `datums/cogbar.cs`, `datums/progressbar.cs` | C | skeletons: the types, var blocks, file-local defines, the roots and `do_after` | - |
| `__DEFINES/do_afters.cs`, `__DEFINES/mobs.cs` | C | not created yet (`DOING_INTERACTION*`); C created only `__DEFINES/mobs.cs` (partial: the `DOING_INTERACTION*` macros, `mobs.dm:636-639`), and `do_afters.cs` was never needed | - |
| `datums/elements/connect_loc.cs`, `datums/movement_detector.cs` | D | skeletons: the types (connect_loc's var overrides), var blocks and roots | A, B, C |
| `modules/movespeed/_movespeed_modifier.cs`, `modifiers/mobs.cs`, `modules/actionspeed/_actionspeed_modifier.cs`, `modifiers/base.cs` | D | skeletons: the modifier types and var blocks, the caches, `get_cached_*`, the `/mob` roots (`add_movespeed_modifier`, `update_config_movespeed`, `update_movespeed`, `initialize_actionspeed`, ...); `mob_config_speedmod` and `actionspeed_modifier/base` complete | C |
| `modules/mob/mob_lists.cs`, `__HELPERS/string_lists.cs`, `__HELPERS/_string_lists.cs` (D, whole), `__HELPERS/files.cs` (D, skeleton: `sanitize_filepath`, blocked on the shim's `/regex`), `modules/mapping/space_management/multiz_helpers.cs` | D | skeletons: every `/mob` list proc; `GLOB.string_lists`, `string_list`, `baseturfs_string_list`; `get_step_multiz`, `get_dir_multiz`, `get_lowest_turf`, `get_highest_turf` | A, B, C |
| `__HELPERS/type2type.cs`, `__HELPERS/atoms.cs` (extend) | D | `GLOB.modulo_angle_to_dir` translated; skeleton signatures of `angle2dir`, `dir2angle`, `get_nested_locs` | A, C |
| `Builtin/atom.builtin.cs`, `atom_movable.builtin.cs`, `obj.builtin.cs`, `mob.builtin.cs`, `turf.builtin.cs`, `area.builtin.cs`, `map.builtin.cs` (`step`), `Infrastructure/Builtins.cs` | W4.0 | final in M0: the constructors call `dm_tg_vars()` (rules 10.2, D1); `mouse_drag_pointer` and `vis_locs` added to `atom`; `step` hand-written | all (no edits) |
| `src/Core/DmShim/image.cs`, `Dm.Presentation.cs` (`image`) | W4.0 | complete (decision D3; `docs/ai/specs/shim-ownership.md`) | C |
| `src/Core/DmShim/DmResource.cs` (the value of a resource literal, `Dm.resource`, rules 3.7) | lead (added after W4.0, with the resource literal conversion of the W4.0 sites) | complete | A, B, C |
| `game/atom/_atom.cs`, `modules/mapping/preloader.cs` | W4.0 | the GAP edits done: `CanPass`, `CanAllowThrough`, `has_gravity`; the no-op turf's `bullet_bounce_sound` | - |
| `__DEFINES/is_helpers.cs`, `__DEFINES/multiz.cs`, `__HELPERS/_planes.cs`, `modules/atmospherics/environmental/LINDA_system.cs`, `__HELPERS/string_assoc_lists.cs` | W4.0 | translated: `isatom`, `isliving`, `ishuman`, `isobserver`, `isitem`, `ismachinery`, `is_multi_tile_object`; `GET_TURF_ABOVE`, `GET_TURF_BELOW`; `SET_PLANE`, `MUTATE_PLANE`, `GET_TURF_PLANE_OFFSET`, `GET_LOWEST_STACK_OFFSET`, `PLANE_TO_TRUE`, `IS_TOPDOWN_PLANE`, `GLOB.topdown_planes`; `/atom/var/can_atmos_pass` and `can_atmos_pass__proc`, `air_update_turf` deferred; `string_assoc_list` | A, B, C, D |
| `_globalvars/lists/mobs.cs`, `mapping.cs`, `ambience.cs` (new), `datums/hud.cs` (`all_huds`, `huds_by_category`, `trait_to_hud`, `trait_blockers_to_hud`), `game/atom/alternate_appearance.cs` (`active_alternate_appearances`) | W4.0 | complete (the GLOB lists; the blood, species and DNA initializers deferred) | A, B, C, D |
| `controllers/subsystem/mapping.cs` (members), `air.cs` (`SSair`), `spatial_gridmap.cs`, `vis_overlays.cs`, `sound_tokens.cs`, `modules/mapping/space_management/zlevel_manager.cs`, `traits.cs`, `space_reservation.cs` | W4.0 | inert stubs and their members: `areas_in_z`, `multiz_levels`, `z_level_to_lowest_plane_offset`, `plane_offset_to_true`, `get_reservation_from_turf` (deferred), `get_level`, `level_trait`, `get_turf_above/below` (M3); `SSair.planetary`, `add_to_active` (deferred); the grid and vis-overlay procs deferred | A, B, C |
| `modules/mob/living/living.cs`, `living_defense.cs`, `datums/components/orbiter.cs`, `game/atom/atom_materials.cs` | W4.0 | skeleton signatures: `set_pull_offsets`, `grabbedby` (M2), `end_orbit`, `set_custom_materials` | A, B |
| the type-only skeletons W4.0 created for the var blocks and GLOB lists (`datums/mocking/client.cs`, `modules/client/persistent_client.cs`, `modules/lighting/lighting_object.cs`, `lighting_corner.cs`, `__HELPERS/spatial_info.cs`, `__HELPERS/icon_smoothing.cs` (the `[DmPath]` of `/turf/closed`), `_onclick/click.cs`, `_onclick/ai.cs`, `modules/power/power.cs`, `modules/procedural_mapping/mapGenerator.cs`, `datums/wires/airlock.cs`, `game/objects/effects/effects.cs`, `overlays.cs`, `modules/atmospherics/environmental/LINDA_fire.cs`, `__HELPERS/pronouns.cs`, `modules/mob/living/death.cs` and the other mob skeletons of `GLOB.abstract_mob_types` and `GLOB.megafauna_spawn_list`: `modules/mob/living/carbon/human/dummy.cs`, `modules/mob/living/basic/{cult/constructs/_construct,guardian/guardian,heretic/_heretic_summon,ruin_defender/mimic/mimic}.cs`, `modules/mob/living/simple_animal/hostile/megafauna/{bubblegum,colossus,drake}.cs`, `modules/bitrunning/util/virtual_megafauna.cs`, `game/objects/items/weaponry/melee/soulscythe.cs`) | W4.0 | final in wave 4: the class and its `[DmPath]` (rules 2.3) | - |
| `docs/ai/specs/not-translated.md` | packages append their own rows; the lead at merge | W4.0 rows added | - |
| `docs/ai/DEVIATIONS.md`, Appendix A and C, `docs/ai/specs/dm-conformance.md`, `docs/ai/specs/shim-ownership.md`, `Infrastructure/TgState.cs` | lead at merge | - | packages report additions |
| tests under `tests/Anomaly.Core.Tests/Tg/Wave4/<Pkg>/`; `TgToyRoom` | each package; W4.M | W4.0: `Tg/Wave4/Contracts/`; A: `Movable/`; B: `TurfArea/`; C: `Mob/`; D: `Support/` | - |
| ported tg unit tests under `tests/Anomaly.Core.Tests/Tg/UnitTests/` (created in wave 4: `anchored_mobs.cs` (C), `area_contents.cs` (B), `connect_loc.cs` (D), `move_pulled.cs` and `movement_order_sanity.cs` (A)), and `tests/Anomaly.Core.Tests/Dm/Values/ResourceTests.cs` (the resource literal) | the package named; the lead (`ResourceTests.cs`) | complete | - |
