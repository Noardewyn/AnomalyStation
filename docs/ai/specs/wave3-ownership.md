# Wave 3 file ownership

Status: v1 (wave-3 contracts, W3.0), 2026-10-06. Who edits which file while the six wave-3 packages translate in
parallel: A mc, B timers and processing, C boot, D logging and chat, E atoms, F engine (the scout's scope report, section 2,
and the lead's plan for wave 3). The W3.0 contracts commit declared everything two packages share: each file below exists
(as a skeleton, a partial mirror or complete) before the packages start, so that every package compiles against the
others' declarations and edits only its own files. Paths are under `src/Core/Tg/` unless they start with `src/`, `docs/`
or `tests/`.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new mirror files that no row names
  (for example a skeleton of a type its code needs, a test fixture, a stub subsystem nobody else names) and lists them in
  its report.
- **Skeleton signatures are frozen.** A skeleton (`// SKELETON:`) or partial mirror (`// PARTIAL:`) declares members
  with the signatures of `docs/ai/specs/generated/slots.tsv` and the rules; other packages call them. The owner completes the
  bodies and may add members, but changes no declared signature, type or name without the lead (report it). A needed
  member in a file a package does not own is reported to the lead, not added.
- **Bodies are `Dm.NotTranslated` until their owner lands.** A test that reaches another package's skeleton member
  throws, as rules 1.4 intends. Test your own code with fixtures (A: fixture subsystems through the per-world
  subsystem-type filter `TgState.SubsystemTypeFilter`; E: fixture atoms; F: fixture turfs), or, for a path that crosses
  another package before the merge, run that world with `DmWorldState.NotTranslatedThrows = false` and assert only on
  what your package owns. Merge order: F, A, B, E, D, C (C owns the "boot to first fire" test that needs every package).
- **Deferred leaves and stubs.** `Dm.Deferred` (rules 1.4) only for leaves whose effects nothing in M0 reads; inert stub
  subsystems keep tg's `[DmPath]`, `name`, `init_stage`, `dependencies`, set `SS_NO_INIT | SS_NO_FIRE` and carry a
  `// STUB(Mx)` line. Report every new one: the lead adds the rows of `docs/ai/specs/not-translated.md` at the merge.
- **Shared documents** (`docs/ai/specs/not-translated.md`, `docs/ai/DEVIATIONS.md`, `docs/ai/translation-rules.md` Appendix A and
  C, `docs/ai/specs/dm-conformance.md`, `docs/ai/specs/shim-ownership.md`): the lead at merge; packages report additions
  (a new deviation with its `// DEVIATION(DVn)` text, a decided slot, a new public name).
- **Test seams** in translated code are marked `// PORT(test seam): ...` (the subsystem-type filter in `Master/New`).
- **The shim** (`src/Core/DmShim/`) is not edited by the packages (`docs/ai/specs/shim-ownership.md`); a missing shim member is
  reported to the lead.

## Files by owner

| File (port) | Owner | State after W3.0 | Others use |
|---|---|---|---|
| `controllers/master.cs`, `controller.cs`, `subsystem.cs` | A | skeletons: every var and proc (the Master's statics are `TgState` fields); `subsystem.cs` keeps the translated `PreInit` | B, C, D, E |
| `controllers/failsafe.cs` | A | skeleton, final in M0 (DV10): `New` registers `Failsafe`, `processing_interval` 0 | - |
| `__DEFINES/MC.cs` | A | skeleton: `START_PROCESSING`, `STOP_PROCESSING`, `MC_RUNNING` | B, D, C |
| `__DEFINES/_tick.cs` | A | partial: `TICK_LIMIT_RUNNING` translated; `TICK_CHECK`, `CHECK_TICK` skeletons | all |
| `__HELPERS/stoplag.cs` | A | skeleton: `stoplag` | all |
| `__HELPERS/time.cs` | A | skeleton: `server_timestamp`, `update_midnight_rollover` (narrowed to `float`, Appendix A) | C |
| `__DEFINES/_flags.cs` | A | skeleton; A adds `GLOB.bitflags` | C (globals) |
| `__DEFINES/maths.cs`, `__DEFINES/time.cs` | A | partial: `REALTIMEOFDAY`, `CEILING`, `ROUND_UP`, `WRAP_UP`, `WRAP_UID`, `SIMPLIFY_DEGREES`, `GET_ANGLE_OF_INCIDENCE`, `DS2TICKS`, `TICKS2DS` skeletons | B, E |
| `datums/helper_datums/stack_end_detector.cs` | A | skeleton: the type | - |
| `controllers/subsystem/garbage.cs` (fire, HandleQueue) | A | as before W3.0 | - |
| stub `controllers/subsystem/ticker.cs` (+ its `[DmPath]` part `__HELPERS/hearted.cs`), `controllers/subsystem/blackbox.cs` | A | inert stubs; `SSticker.timeLeft`, `start_at`; `record_feedback` deferred | C, D |
| `controllers/subsystem/timer.cs` | B | skeleton: the types, `SStimer`, `_addtimer`, `deltimer`, `timeleft`, `updatetimedelay`, `reset_buckets` | A, C, E |
| `controllers/subsystem/processing/processing.cs`, `fastprocess.cs`, `obj.cs`, `timed_actions.cs` | B | skeletons: the types, their globals, the processing vars, the root of `/datum/proc/process` | A (fires them, `START_PROCESSING` reads `processing` and `currentrun`), E |
| `__DEFINES/subsystems.cs` (non-atom members) | B | not created yet | A, E |
| `Infrastructure/TgState.cs` (`InitStatics`), `Infrastructure/GlobalProcs.cs` | C | as before W3.0 | all |
| `controllers/globals.cs` | C | skeleton holding the `[DmPath]` of `/datum/controller/global_vars` (rules 2.3) | all |
| `game/world.cs` | C | skeleton: `change_fps`, `get_world_state_for_logging` | A, D |
| `controllers/configuration/configuration.cs`, `config_entry.cs`, `entries/general.cs` | C | skeletons: `Get`, `Set`; the entry base types; every entry type of `general.dm` | A, B, D |
| `_globalvars/configuration.cs` | C | partial: `GLOBAL_REAL(config)` | A, D |
| `_globalvars/lists/mobs.cs`, `_globalvars/lists/mapping.cs` | C | skeletons: `GLOB.clients`, `GLOB.admins`, `GLOB.areas_by_type` (fields only) | D, F |
| `__DEFINES/tgs.cs` | C | partial: `TgsInitializationComplete` as a no-op seam | A |
| `__DEFINES/configuration.cs`, `__HELPERS/global_lists.cs`, the bootstrap entry | C | not created yet | - |
| stub `controllers/subsystem/dbcore.cs` | C | inert stub; `InitializeRound` deferred | - |
| `__HELPERS/logging/debug.cs`, `_logging.cs`, `game.cs` | D | skeletons: `log_world`, `log_game`, `log_config`, `log_mapping`, `log_qdel`, `log_runtime`, `log_signal`, `warning`, `notice` | all |
| `modules/logging/log_holder.cs` (and the rest of `modules/logging/**`) | D | skeleton: the type, `logger`, `init_logging`, `shutdown_logging`, `Log` | C |
| `_globalvars/logging.cs` | D | skeleton: `GLOB.round_id`, `log_directory`, `picture_log_directory`, `picture_logging_prefix` (fields only); `world._initialize_log_files` | C, E |
| `modules/tgchat/to_chat.cs`, `modules/admin/admin.cs` | D | skeletons: `to_chat`, `message_admins` | all |
| `__DEFINES/logging.cs`, `__DEFINES/rust_g_overrides.cs` (extend), the rust-g file procs over the shim's file seam | D | as before W3.0 / not created yet | - |
| stub `controllers/subsystem/chat.cs` | D | inert stub; D replaces it with the SSchat shim of `docs/ai/specs/presentation-seam.md` | - |
| `game/atom/_atom.cs`, `game/atom/atoms_initializing_EXPENSIVE.cs`, `modules/mapping/preloader.cs`, `_globalvars/genetics.cs`, `__HELPERS/_planes.cs` | E | `_atom.cs` an empty skeleton (its builtin vars moved to the engine part); the others not created yet | F (no edits) |
| `controllers/subsystem/atoms.cs` | E | skeleton: the type and `SSatoms` | - |
| `__DEFINES/is_helpers.cs` | E (wave 3) | as before W3.0 (pilot partial); E adds the turf helpers it needs (`isspaceturf`) | all |
| stubs `controllers/subsystem/mapping.cs`, `job.cs` (+ its `[DmPath]` part `controllers/configuration/entries/jobs.cs`, with `load_jobs_from_config`), `processing/reagents.cs`, `fluids.cs`, `processing/station.cs`, `icon_smooth.cs` | E | inert stubs; `SSmapping.max_plane_offset`; `free_deferred`, `remove_from_queues` deferred | C (`SSjob`) |
| `Builtin/atom.builtin.cs`, `atom_movable.builtin.cs`, `obj.builtin.cs`, `mob.builtin.cs`, `turf.builtin.cs`, `area.builtin.cs`, `map.builtin.cs`, `Infrastructure/Builtins.cs`, the map grid (new files) | F | skeletons: the builtin vars of object-model.md section 2 (engine members `Dm.NotTranslated`, appearance vars stored), the builtin procs `Enter`, `Cross`, `Crossed`, `Uncross`, `Uncrossed`, the map builtins `get_step`, `get_dir`, `isturf`, `block`, `locate(x, y, z)` | E, C |
| `__DEFINES/turfs.cs` | F | skeleton: `get_turf` | E |
| `controllers/subsystem/air.cs` (`/turf/open`, `/turf/open/space`), `game/turfs/open/space/space.cs`, `game/area/areas.cs` (`/area/space`, `area.turfs_by_zlevel`), `modules/mob/dead/dead.cs`, `_onclick/other_mobs.cs` (`/mob/dead/new_player`) | lead | skeletons of the types of tg's `/world` defaults (`code/world.dm`) in the files that declare them first (rules 2.3); no wave-3 package edits them | C (world defaults, `increase_max_x/y`), F |
| `Infrastructure/TgState.Subsystems.cs` (the subsystem-type filter, `global_vars__subsystems`), `Infrastructure/AssemblyInfo.cs` | contracts (complete) | complete | A |
| `src/Core/DmShim/**` (S1-S8: `world.map_cpu`, the `/world` vars, the proc-path `typesof`, exact `log`, the file seam, `rand_seed`, the map grid hook `DmWorldState.MapResized`, `Dm.Deferred`) | contracts (complete) | complete (`docs/ai/specs/shim-ownership.md`) | all |
| `docs/ai/specs/not-translated.md`, `docs/ai/DEVIATIONS.md`, Appendix A and C, `docs/ai/specs/dm-conformance.md` | lead at merge | - | packages report additions |
