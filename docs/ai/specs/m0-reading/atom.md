# R2-atom: atom base, init flow, appearance machinery (tg snapshot 1d99bd7f)

> Reading-pass report. `docs/ai/translation-rules.md` and `docs/ai/specs/*.md` win on conflict; the C# names and shapes here (section 14 and elsewhere) are non-binding sketches (see README.md).

Paths are relative to `tgstation/code/` unless absolute. Counts are `grep` line counts over `*.dm` (approx, they include comments).
Scope read in full: 8 files, 2352 DM lines. Plus supporting reads: `controllers/subsystem/overlays.dm`, `__DEFINES/{subsystems,_atoms,overlays,_flags}.dm`, `__HELPERS/_planes.dm`, `modules/mapping/preloader.dm`, `controllers/master.dm:485-540`, turf/area/mob/obj `New`/`Initialize`.

## 0. Findings that change the plan

1. `atom/New` is only 10 lines (`game/atom/atoms_initializing_EXPENSIVE.dm:65-75`); all real work is `SSatoms.InitAtom` -> `Initialize`. A C# constructor must NOT contain DM `New` logic and never touches the World: the factory is `Dm.@new(type, args)` (wave-0 decision), which allocates (field initializers = prototype defaults), assigns `dm_id = World.Current.AllocateId()`, registers the object in the world-owned id-ordered registry, runs the engine pre-New hook (silent `loc` placement for atoms, object-creating var defaults), then calls `New` through the same binder as `Dm.Call` (the map preloader is tg code inside `atom/New`, step 1 of 3.1, not part of the factory); the whole subclass `New`-override chain (28 atom-type overrides, e.g. `mob/New` runs `GenerateTag()` BEFORE `..()`, `area/New` registers globals BEFORE `..()`) runs around the `Initialize` that fires inside `atom/New`.
2. `new T(loc)` sets `loc` silently: no `Entered`/`Exited` fire. Evidence: tg adds the post-init signal `COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON` (InitAtom:47-50) and callers do `forceMove(src) // trigger storage.handle_entered` (`modules/fishing/aquarium/aquarium_kit.dm:43`). `loc` is already valid inside `Initialize` (it is read there), so the factory assigns `loc`/`contents` BEFORE `New`.
3. DM runtime errors terminate only the failing proc and return its `.` to the caller. tg depends on it: `InitAtom` maps a null `Initialize` result to `BAD_INIT_NO_HINT` and carries on (InitAtom:26-38), and the comment at `atoms.dm:57` says a runtime in `CreateAtoms` must not break `initialized`. The port deliberately deviates (wave-0 decision, recorded in `docs/ai/DEVIATIONS.md`, allowed by D86 because runtimes are bugs): a C# exception unwinds through its callers to the nearest isolation boundary, which logs it through `World.ReportRuntime` and carries on; `InitAtom` during atom initialization is one of those boundaries (see H1).
4. Map load defers `Initialize`: while `SSatoms.initialized == INITIALIZATION_INSSATOMS (0)`, `New` does nothing; the loader later calls `SSatoms.InitializeAtoms(list)` (areas + turfs + movables, `modules/mapping/map_template.dm:92`). `initialized` is a TRI-STATE INT (0/2/1), stacked by `set_tracked_initalized`. It must not become a C# `bool` (Master also sets `initialized = TRUE`(=1) after an SS init, `controllers/master.dm:520`; `init_subsystem` also skips any SS whose `initialized` is already truthy, :494).
5. `atom_appearance.dm` is pure logic over signals and the builtin `overlays` list; it needs only an immutable interned `Appearance` value type, not the renderer. 2860 `update_appearance(` call sites, 514 `update_icon_state` overrides and 467 `update_overlays` overrides make it the highest-fan-out file here: translate it whole in M0.
6. `atom_defense.dm`: ROADMAP puts it in M5, but `atom/Initialize` reads `uses_integrity`, `max_integrity`, `atom_integrity`, `armor` (atoms_initializing:143-145), so M0 declares those fields. The file itself is 166 lines, pure, small deps: cheap to pull forward if the owner wants a "damage a wall" toy demo (owner decision, do not do silently).

## 1. Files

| File | Lines | Role |
|---|---|---|
| game/atom/_atom.dm | 1007 | vars block 7-151; `Destroy` 163-207; CanPass/CanAllowThrough 225/243; Entered/Exit/Exited 620/629/643; setDir 579; set_density 726; has_gravity 778; the rest is gameplay helpers + UI (screentips 868-983) |
| game/atom/atoms_initializing_EXPENSIVE.dm | 175 | `SSatoms.InitAtom` 2-54, `atom/New` 65-75, `atom/Initialize` 115-159, `LateInitialize` 172 |
| controllers/subsystem/atoms.dm | 229 | `SUBSYSTEM_DEF(atoms)`: InitializeAtoms 47, CreateAtoms 89, tracked-init state 137-171, prepare_deletion 219 |
| game/atom/atom_appearance.dm | 115 | update_appearance/name/desc/icon/icon_state/overlays + `managed_overlays` diffing (update_icon 40-104) |
| game/atom/atom_invisibility.dm | 72 | priority-based `invisibility` sources (SetInvisibility/RemoveInvisibility/RecalculateInvisibility) |
| game/atom/atom_act.dm | 263 | `*_act` reactions (fire/ex/blob/emp/bullet/hitby/acid/emag/...), mostly "send signal, return" |
| game/atom/atom_tool_acts.dm | 325 | `base_item_interaction` 8, `tool_act` 80, ranged variants 188, 14 empty `<tool>_act[_secondary]` virtuals 272-325 |
| game/atom/atom_defense.dm | 166 | integrity, `take_damage` 17, `update_integrity` 63, `run_atom_armor` 92, `atom_break/fix/destruction` |

## 2. Atom state and the C# mapping

**Builtin atom vars** (engine-owned; the shim defines them): `loc`, `contents`, `x y z` (turf-derived), `name desc gender suffix`, `icon icon_state dir`, `density opacity luminosity`, `invisibility`, `layer plane alpha color`, `pixel_x pixel_y pixel_w pixel_z`, `transform`, `blend_mode appearance_flags mouse_opacity`, `overlays underlays vis_contents vis_locs filters appearance`, `render_source render_target`, `maptext*`, `verbs`, `bound_*`/`locs` (42/61 mentions, multi-tile objects), `type parent_type vars tag`.
**tg vars in the assigned files**: `_atom.dm:7-151` (about 55 vars: `flags_1`, `pass_flags_self`, `interaction_flags_*`, light block `light_system/range/power/color/on/flags/angle/dir/height` + `light`/`light_sources`, `base_pixel_x/y/w/z`, `smoothing_*`, `reagents`, `wires`, `ai_controller`, `forensics`, `hud_list`, `can_astar_pass`, ...), `atom_appearance.dm:3-6` (`managed_overlays`, `post_init_icon_state`), `atom_defense.dm:3-14`, `atom_invisibility.dm:6-7` (`invisibility_sources`, `current_invisibility_priority`), `controllers/subsystem/overlays.dm:142-145` (`realized_overlays/underlays`, debug only).
**Rules**
- Var declarations and type defaults (`layer = ABOVE_NORMAL_TURF_LAYER`, `plane = GAME_PLANE`, `appearance_flags = TILE_BOUND|LONG_GLIDE`, `abstract_type = /atom`, lines 8-11) come from the converter ("Prototype", D83); procs are translated. `initial(x)` reads the prototype table.
- Simulation reads many visual vars (dir, density, opacity, invisibility, name/desc, icon_state, color, alpha, plane/layer), so they are plain fields, NOT sink-only. Only WRITES are presentation events (section 4).
- `VAR_PRIVATE/VAR_PROTECTED` (664 uses with the PROC variants) are lint-only; `atom_integrity` is VAR_PRIVATE (atom_defense.dm:8) yet `turf/Initialize` writes it (turf.dm:188). Translate as a `public` field plus a `[DmPrivate]`/`[DmProtected]` marker (translation-rules.md section 3), never C# `private`/`protected`.
- Flags are 24-bit ints (`ALL = ~0`, `_flags.dm:1`); keep `int`, `updates &= ~SEND_SIGNAL(...)` is used (atom_appearance:21,44).
- `light_*` stay plain fields; `update_light()` becomes a hook (modules/lighting is not ported, ROADMAP M3).

## 3. Lifecycle, exact order

### 3.1 `new T(loc, a, b)` -> `atom/New(loc, ...)` (atoms_initializing:65-75)
0. Engine (`Dm.@new`): allocate with prototype defaults, assign the id and register the object, pre-New hook sets `loc`/`contents` (silent, no Entered); (map loader: `GLOB.use_preloader` set by `preloader_setup`).
1. `if(GLOB.use_preloader && type == GLOB._preloader_path) world.preloader_load(src)` -> `what.vars[attr] = value` per attribute, deep-copying lists (`modules/mapping/preloader.dm:17-33`). Needs reflective var table.
2. `do_initialize = SSatoms.initialized`; if it is not `INSSATOMS`: `args[1] = (do_initialize == INNEW_MAPLOAD)` (overwrites `loc` in the arg list), `if(SSatoms.InitAtom(src, FALSE, args)) return`.
3. `INITIALIZE_IMMEDIATE(X)` (`__DEFINES/subsystems.dm:96-105`, 16 uses: landmark, `/atom/movable/screen`, `/mob/dead`, `/obj/item/organ`, dummy human, splashscreen, ...) appends to subtype `New`: after `..()`, if not yet initialized, force state MAPLOAD, `args[1]=TRUE`, `InitAtom`, restore state. Applies to the type and all subtypes.

### 3.2 `SSatoms.InitAtom(A, from_template, arguments)` (2-54)
1. `QDELING(A)` (`gc_destroyed` truthy) -> if `gc_destroyed > init_start_time` set `BAD_INIT_QDEL_BEFORE`; return TRUE.
2. `result = A.Initialize(arglist(arguments))` (UNIT_TESTS: sleep detection via `world.time`, `BAD_INIT_SLEPT`).
3. `switch(result)`: `NORMAL(0)` nothing; `LATELOAD(1)`: `arguments[1]` (mapload) ? `late_loaders += A` : `A.LateInitialize()` immediately; `QDEL(2)`: `qdel(A)`; else (including null) `BAD_INIT_NO_HINT`.
4. `!A` (hard-deleted) -> qdeleted; else `!(flags_1 & INITIALIZED_1)` -> `BAD_INIT_DIDNT_INIT`; else in order: `SEND_SIGNAL(A, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE)`, `SEND_GLOBAL_SIGNAL(COMSIG_GLOB_ATOM_AFTER_POST_INIT, A)`, `SEND_SIGNAL(loc, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, A, mapload)` if `loc`, then `created_atoms += A.get_all_contents()` iff `created_atoms && from_template && ispath(type, /atom/movable)`.
5. Returns `qdeleted || QDELING(A)`.

### 3.3 `atom/Initialize(mapload, ...)` body order (115-159)
`SHOULD_NOT_SLEEP`, `SHOULD_CALL_PARENT`. 1 stack_trace if already `INITIALIZED_1`, then set it. 2 `SET_PLANE_IMPLICIT(src, plane)`. 3 `hud_possible = string_assoc_list(...)`. 4 `update_greyscale()` if `greyscale_config && greyscale_colors`. 5 `add_atom_colour(color, FIXED_COLOUR_PRIORITY)` if `color`. 6 `update_light()` if COMPLEX_LIGHT with power and range. 7 `icon_state = post_init_icon_state`. 8 `SETUP_SMOOTHING()`. 9 `atom_integrity = max_integrity` if `uses_integrity`. 10 `initialize_materials(custom_materials)` (MUST follow 9). 11 `ai_controller = new ai_controller(src)` if it is a path. 12 `setup_reskins()`. Returns `INITIALIZE_HINT_NORMAL`.
Overrides: 4327 `Initialize` overrides; 3239 `(mapload)`, 8 `(mapload, ...)`, 630 `(mapload, named_extra...)`. Parent-skipping copies (must be kept in sync with the body above): `turf/Initialize` (`game/turfs/turf.dm:139-203`, skips most of steps 2-8 and 11-12 and re-implements its own smoothing, light, integrity and materials code), `turf/open/space` (`space_EXPENSIVE.dm:14`), `__HELPERS/mobs.dm:683`, `__HELPERS/spatial_info.dm:36`, `objects/effects/{misc,decal}`, `mob/dead`. `atom/movable/Initialize` runs `..()` in the middle (emissive blocker, light_blocking element, overlay_lighting component after it, `game/atoms_movable.dm:149-213`); `mob/Initialize` does ~10 things BEFORE `..()` (`modules/mob/mob.dm:81-97`).

### 3.4 `LateInitialize` (172-175)
`set waitfor = FALSE`, `SHOULD_CALL_PARENT(FALSE)`; base only `stack_trace`. 71 overrides, and all of them inherit waitfor=FALSE (OpenDream `DMCodeTree.Procs.cs:36-41` copies `DisableWaitfor` from the overridden proc into every override). Called inline for runtime-created atoms and from `InitializeAtoms:64-72` for mapload (`late_loaders`, in `Initialize`-completion order; loop bound `late_loaders.len` is read once).

### 3.5 `atom/Destroy(force)` (163-207) order
`alternate_appearances` -> `remove_atom_from_hud`; `QDEL_NULL` of `reagents`, `forensics`, `atom_storage`, `wires`; `orbiters = null`; `overlays.Cut()` if non-empty; `LAZYNULL(managed_overlays)`; `QDEL_NULL(ai_controller)`, `QDEL_NULL(light)`; `light_sources.Cut()`; `SSicon_smooth.remove_from_queues` if `SMOOTH_QUEUED`; DREAMLUAU block (drop); `return ..()` -> `datum/Destroy` (`datums/datum.dm:125`: clears tag/weakref, timers, components, signals; returns `QDEL_HINT_QUEUE`). `atom/movable/Destroy` runs its cleanup BEFORE `. = ..()` and the content-deletion/`moveToNullspace` AFTER it (`atoms_movable.dm:215-273`). 1761 Destroy overrides. `QDEL_NULL(x)` is `qdel(x); x = null` (`__DEFINES/qdel.dm:63`).

### 3.6 SSatoms state machine (`controllers/subsystem/atoms.dm`)
- Vars: `initialized` (starts `INSSATOMS`=0), `initialized_state` stack of `[source, state]`, `base_initialized`, `active_initializations`, `late_loaders`, `BadInitializeCalls` (type -> bitmask), `created_atoms`, `queued_deletions`, `init_start_time`. `ss_flags = SS_NO_FIRE`; `dependencies` = reagents, fluids, mapping, job SS.
- `Initialize()` (37-45): `init_start_time = world.time`, `setupGenetics()`, state MAPLOAD, `InitializeAtoms()`, state REGULAR, return `SS_INIT_SUCCESS`.
- `InitializeAtoms(atoms, atoms_to_return)` (47-86): return if state INSSATOMS; unique source `"subsystem init [uid]"` (`WRAP_UID`); push MAPLOAD; `CreateAtoms`; pop; `SSicon_smooth.free_deferred(source)`; run `late_loaders`; append `created_atoms`; `qdel` each `queued_deletions` (WeakRef Destroy qdels the target, `datums/weakrefs.dm`); clear.
- `CreateAtoms` (89-135): with a list: skip already-`INITIALIZED_1`, `InitAtom(A, TRUE, list(TRUE))`; without: `for(var/atom/A as anything in world)`, `InitAtom(A, FALSE, ...)`. `TICK_CHECK` -> pop state, `stoplag()`, push state (108-112, 129-133).
- `map_loader_begin/stop(source)` push/pop INSSATOMS (`modules/mapping/reader.dm:317-344,500,656,1106`). `get_initialized_source` is read by `icon_smooth.dm:80`.
- Other users of SSatoms state: `turf/open/_open.dm:247`, `lobby_menu.dm:235-267`, `assistant.dm:102`, `mob_helpers.dm:298`, `lazy_template.dm:117`, `shuttles/_shuttle.dm:90`, `mapping.dm:695`, `early_assets.dm:21`.
- M0 toy-world flow: stub Master -> (toy map loader creates atoms under INSSATOMS, they register in the world registry) -> `SSatoms.Initialize()` -> from then on `new` initializes inline with `mapload=false`.

## 4. Appearance machinery and `IAppearanceSink`

### 4.1 Flow (`atom_appearance.dm`)
`update_appearance(updates=ALL)`: `updates &= ~SEND_SIGNAL(COMSIG_ATOM_UPDATE_APPEARANCE, updates)`; then `update_name` / `update_desc` / `update_icon` if their bits remain; return OR of results. `update_icon`: signal may mask `UPDATE_ICON_STATE`/`UPDATE_OVERLAYS`; `update_icon_state()` (signal only); for overlays: remove `managed_vis_overlays` via `SSvis_overlays`, `new_overlays = update_overlays()` (handlers append to the returned list through the signal), convert each entry (`istext`/`isicon` kept, else `.appearance`), drop nulls, compare to `managed_overlays` (a single item is stored bare, not as a list), and if different either `overlays = null` (when `length(overlays)` equals the managed count: "full control", then `POST_OVERLAY_CHANGE`) or `cut_overlay(managed)`; then `add_overlay(new)`, `managed_overlays = new`. Finally `COMSIG_ATOM_UPDATED_ICON`. Overlay primitives live in `controllers/subsystem/overlays.dm:62-110` (`add_overlay`, `cut_overlay`, `cut_overlays`, `copy_overlays`, `build_appearance_list`: text -> `iconstate2appearance(icon, text)`, icon -> `icon2appearance`), with `MAX_ATOM_OVERLAYS 100` guard (`__DEFINES/overlays.dm:5-15`) and `POST_OVERLAY_CHANGE` -> `alternate_appearances` copy.

### 4.2 Value types the shim needs
- `Appearance`: immutable, interned (reference equality == value equality), fields = icon, icon_state, dir, layer, plane, color, alpha, pixel_x/y/w/z, transform, blend_mode, appearance_flags, render_*, maptext, mouse_opacity, filters, overlays, underlays. `atom.appearance`, `image`, `mutable_appearance` (889 `mutable_appearance(` uses) snapshot into it. `managed_overlays[i] != new_overlays[i]` and `overlays -= x` rely on this equality.
- `OverlayList`: special list type; reads never null; `+=` accepts text / icon / appearance / atom / list and converts; `-=` by appearance equality; `= null` clears; `Cut()`.

### 4.3 What must go through `IAppearanceSink`
The sink is the one-way presentation boundary: the simulation WRITES, nothing in the simulation reads back through it. Proposed M0 surface (all default methods are no-ops; `NullAppearanceSink`, `RecordingAppearanceSink` for tests, later a replication sink in M1 and the Godot sink in M3):
```
interface IAppearanceSink {
  void VarChanged(Atom a, VisualVar v);              // icon, icon_state, dir, plane, layer, pixel_*, color, alpha, transform, name/desc (for UI), invisibility, ...
  void OverlaysChanged(Atom a, OverlayKind k);        // overlays/underlays/managed vis overlays (carries Appearance[] snapshot)
  void VisContentsChanged(Atom a);                    // vis_contents / vis_locs, SSvis_overlays
  void Animate(Atom a, in AnimateSpec s);             // animate(alpha=0,time=1 SECONDS) atom_act.dm:80  (944 uses)
  void Flick(Atom a, string state, float ds);         // flick() 198 uses
  void LightChanged(Atom a);                          // from update_light / set_light_*
  void SmoothingRequested(Atom a);                    // QUEUE_SMOOTH; no-op in 3D
}
```
Separate sinks (NOT part of this interface): sound (`playsound` 3727), chat/balloons (`to_chat`, `balloon_alert` 4275, `visible_message` 3959), tgui, per-client images (`hud_list`, `active_hud_list`, `alternate_appearances`).
Placement rule: setters on `Atom` mark a dirty bit and the world flushes the dirty set to the sink once per tick (D-replication friendly); `update_icon` calls `OverlaysChanged` once after its diff.
In headless M0 nothing renders; tests assert on the `RecordingAppearanceSink` event list.

### 4.4 `atom_invisibility.dm` (pure logic, 81 external uses)
`invisibility_sources` is a MIXED list: anonymous entries `list(value, priority)` (no id) and assoc `id -> list(value, priority)`; `RecalculateInvisibility` distinguishes them with `islist(entry)`. `highest_priority` is never initialised: `null > priority` is evaluated with null = 0, so a first entry with a NEGATIVE priority is skipped (then `highest_priority_invisibility_data[...]` is a null deref). Keep null-as-zero comparison for fidelity.

## 5. Signals this area emits (tg order matters)
- Move-side hooks (M0 core): `Exit`: `COMSIG_ATOM_EXIT` on the old container, blocked by `COMPONENT_ATOM_BLOCK_EXIT`; `Entered`: `COMSIG_ATOM_ENTERED` on the container THEN `COMSIG_ATOM_ENTERING` on the mover; `Exited`: `COMSIG_ATOM_EXITED` on the container THEN `COMSIG_ATOM_EXITING` on the mover (`_atom.dm:620-645`). `Exit` deliberately does not call `..()` (would call `Uncross`).
- `CanPass` (225): `COMSIG_ATOM_TRIED_PASS` (permit bit) -> `PHASING` -> `CanAllowThrough` (`pass_flags`, `LETPASSTHROW`, `!density`) -> `mover.CanPassThrough(src, REVERSE_DIR(dir), .)` unless `generic_canpass`.
- `setDir` (579): `PRE_DIR_CHANGE` (can block), `DIR_CHANGE`, assign, `POST_DIR_CHANGE`, border-object smoothing. `set_density`: `DENSITY_CHANGED`. `Bumped`: `COMSIG_ATOM_BUMPED`.
- Init: `AFTER_SUCCESSFUL_INITIALIZE`, global `ATOM_AFTER_POST_INIT`, `AFTER_SUCCESSFUL_INITIALIZED_ON` (61 non-define uses of the three).
- Appearance: `UPDATE_APPEARANCE/NAME/DESC/ICON/ICON_STATE/OVERLAYS`, `UPDATED_ICON`; the return value of the first two is a MASK of `UPDATE_*` bits to skip.
- Reactions in `atom_act.dm`: fire, extinguish, ex, blob, pre_emp/emp, pre_bullet/bullet, hitby, acid, emag, narsie, sing_pull, rcd, hulk. `atom_defense.dm`: take_damage, integrity_changed, break, fix, destruction.
- `COMSIG_ATOM_TOOL_ACT(tooltype)` / `..SECONDARY_TOOL_ACT` / `COMSIG_TOOL_ATOM_ACTED_*(tooltype)` build signal names at runtime (`"tool_act_[tooltype]"`). `COMSIG_TOOL_ATOM_ACTED_SECONDARY` has the same string as PRIMARY in this snapshot (`__DEFINES/dcs/signals/signals_tools.dm:5-7`): preserve.
- `SEND_SIGNAL` is a macro: arguments are evaluated ONLY if a listener exists, and out-params are lists filled by handlers (`forced_gravity` 785, `processing_recipes` 91, the `.` list in `update_overlays` 115). `||` chains in `base_item_interaction` rely on short-circuit (28-30).

## 6. Procs that sleep / spawn / are waitfor=FALSE / async
- `SSatoms.CreateAtoms` `stoplag()` on `TICK_CHECK` (atoms.dm:110,131); callers (`Initialize`, map load) already wait. M0: deterministic tick, `TickCheck()` = false, translate synchronously (mark `// tg: atoms.dm:110 stoplag omitted`).
- `atom/LateInitialize` (`set waitfor = FALSE`, 173); 71 overrides, many sleep. waitfor=FALSE is inherited by every override (OpenDream `DMCodeTree.Procs.cs:36-41`), so the whole slot is translated with the callee-side waitfor=FALSE wrapper of `docs/ai/specs/waiting-model.md`: callers call it like a normal proc and get its `.` at its first suspension.
- `atom/Bumped` (`set waitfor = FALSE`, 408; 44 overrides) and `atom/ex_act` (`set waitfor = FALSE`, atom_act:41; 92 overrides): same, inherited by all overrides.
- `atom/Initialize`, `New`, `InitAtom`, `update_appearance` family: `SHOULD_NOT_SLEEP`, so plain synchronous methods; `BAD_INIT_SLEPT` cannot occur by construction.
- `StartProcessingAtom` (_atom.dm:677) -> `use_tool(...)` -> `do_after` sleeps (callers: `process_recipes` <- `tool_act` <- `base_item_interaction`). `ShowProcessingGui` -> `show_radial_menu` waits for input. So the click chain `base_item_interaction`/`tool_act`/`*_act` (601 `item_interaction` overrides, 800 `do_after(` sites) must be wait-capable. Colouring is per C# virtual slot (root declaration + name), computed once for the whole tree by `tools/coloring` (`docs/ai/specs/generated/colored-slots.tsv`, `docs/ai/specs/colored-procs.md`).
- `hitby` (atom_act:149) schedules `hitby_react` via `addtimer(CALLBACK(...), 0.2 SECONDS)`: needs the timer subsystem, not a sleep. `emp_act` (atom_act:80-81) uses `animate(...)` + `QDEL_IN(src, 1 SECONDS)`.
- `on_mouse_enter` -> `INVOKE_ASYNC(set_hover_maptext)` (_atom:973): client UI, skip.
- `atom/Topic` and `MouseEntered` (833, 862) are engine callbacks using `usr`: skip.

## 7. Dynamic / DM-specific features (file:line)
- `args` + `arglist`: `New(loc, ...)` (65), `args[1] = ...` (72), `A.Initialize(arglist(arguments))` (17), `INITIALIZE_IMMEDIATE`. Project-wide `arglist(` 130, `args` 267.
- `initial()`: `initial(atom_to_create.name/.icon/.icon_state/.gender)` (_atom:666-667,699), `initial(invisibility)` (atom_invisibility:14); 3230 uses project-wide.
- `new <var>(...)`: `new ai_controller(src)` (atoms_initializing:155), `new atom_to_create(drop_location())` (_atom:689), `new path ()` in `setupGenetics` (atoms.dm:190).
- `vars[]`: preloader (`modules/mapping/preloader.dm:33`), 118 `vars[` uses. Needs a per-type var table (get/set by name).
- `subtypesof()`/`typesof()`: atoms.dm:185 (946 uses). Type registry with `abstract_type` (`_atom.dm:8`).
- Typecache: `typecacheof(list(...))` inside `var/static/list` (_atom:302), `is_type_in_typecache` (308), `is_type_in_list(m, assoc, zebra = TRUE)` (376).
- `PROC_REF` callbacks (8702 uses), `CALLBACK` = `new /datum/callback` (`__DEFINES/callbacks.dm:3`), `INVOKE_ASYNC` (942).
- `world` as a list: `for(var/atom/A as anything in world)` (atoms.dm:121); 57 `in world`.
- Typed vars that hold a PATH until init: `ai_controller` (_atom:127,154), `armor_type` -> `armor`, `smoothing_groups`/`canSmoothWith` (lists rewritten in place into bitfield strings by `SETUP_SMOOTHING`, with `canSmoothWith[1] == "-"` string test), `hud_possible` (shared string-list cache), `custom_materials` (path->amount rewritten by `initialize_materials`).
- `istype(x)` with ONE arg uses the variable's declared type: `istype(target)` (_atom:490), `istype(wires)` (atom_act:72), `istype(colors)` (atom_greyscale:13).
- Default args that read `src`: `update_inhand_icon(mob/target = loc)` (488). Named args at call sites (`SetInvisibility(INVISIBILITY_NONE, id=type)`).
- `static` proc vars: `var/static/uid` (atoms.dm:52), `var/static/list/syndie_typecache` (_atom:302).
- `.` return variable with bare `return` returning `.` (`take_damage` 17-34, `set_density` 730).
- Mixed assoc/non-assoc lists: `invisibility_sources` (above); `initialized_state += list(list(source, state))` (atoms.dm:155); `list.Cut(i, i+1)`, `for(i in n to 1 step -1)` (161-162).
- Compile-time guards to drop: `#ifdef UNIT_TESTS`, `#ifdef TESTING`, `PROFILE_MAPLOAD_INIT_ATOM`, `DREAMLUAU_CLEAR_REF_USERDATA`.

## 8. Hazards and handling
- H1 Runtime isolation: DM terminates the failing proc and returns its `.` (often null) to the caller. The port deviates (wave-0 decision, `docs/ai/DEVIATIONS.md`; never call this "DM semantics"): an exception unwinds to the nearest isolation boundary, where `World.ReportRuntime` logs it and execution continues. Boundaries: each scheduler entry, `Waiting.Detach`, subsystem `fire`/`ignite`, each timer callback, each signal handler call inside `_SendSignal`, each callback `Invoke`, per-item `process()`/`Life()` in processing subsystems, and `InitAtom` during atom initialization. Everything else unwinds. Where tg inspects `.` after a runtime, the translator writes an explicit try/catch at that site. Never wrap `active_initializations++/--` in finally (DM does not).
- H2 `Initialize` result is `int?`, not enum/bool: null must stay distinct from `INITIALIZE_HINT_NORMAL = 0` (null -> `BAD_INIT_NO_HINT`).
- H3 DM `||`/`&&` return operand VALUES; `SEND_SIGNAL(a) || SEND_SIGNAL(b) || ...` must short-circuit and keep the int. Procs like `is_injectable()` (`reagents && (flags & X)`, _atom:417) return a number or null: type them `int`, null -> 0 (documented loss of null/0 distinction).
- H4 Float32 semantics: `round(x, 0.01)` (atom_defense:89,104), `receive_ricochet_damage_coeff = 0.33`, `world.time` in deciseconds (float per D64). Provide `Dm.round(a, b)` doing float32 math.
- H5 Hard delete: BYOND nulls every reference to a `del()`'d datum; `if(!A)` (InitAtom:40) cannot work in C#. Translate as `A == null || A.Deleted`; `QDELETED(x)` is `isnull(x) || gc_destroyed`.
- H6 Parent-call contract: 449 `SHOULD_CALL_PARENT(TRUE)`. C# cannot enforce; add `[ShouldCallParent]` plus an IL-scan test (or analyzer) so missed `base.X()` fails in `dotnet test`.
- H7 tg never runs BYOND's builtin `Move`: `/atom/movable/Move` def1 (atoms_movable.dm:687) is a full reimplementation that never calls `..()`, and def2 (:763) wraps it. The shim only provides the raw, silent `loc` assignment; `Crossed`, `Uncross` and `Uncrossed` must never be invoked (`Crossed` is a CRASH stub, atoms_movable.dm:942-944; `/atom/Exit` deliberately skips `..()`, _atom.dm:630). Argument counts: Move def1 calls `newloc.Entered(src, oldloc, old_locs)` (3 args, :755) and doMove calls `destination.Entered(src, oldloc)` (2 args, :1222). The order of events is owned by `docs/ai/specs/move-chain.md`; this file only supplies the hooks.
- H8 Function-like macros are not constants: the Defines tool covers constants only. Needs a hand table for the macros used here: `SEND_SIGNAL, SEND_GLOBAL_SIGNAL, QDELING, QDELETED, QDEL_NULL, LAZY*, POST_OVERLAY_CHANGE, VALIDATE_OVERLAY_LIMIT, SET_PLANE_IMPLICIT, SETUP_SMOOTHING, QUEUE_SMOOTH(_NEIGHBORS), TEST_ONLY_ASSERT, EMPTY_BLOCK_GUARD, WRAP_UID, TICK_CHECK, REF, WEAKREF, REVERSE_DIR, PENETRATE_ARMOUR, GET_ANGLE_OF_INCIDENCE, COMSIG_*(tooltype)`.
- H9 `POST_OVERLAY_CHANGE` reads `alternate_appearances` of `src` in its `if` but iterates `changed_on` (`__DEFINES/overlays.dm:17-24`): quirk; only matters when `changed_on != src` (the `/image` variants). Keep as written.
- H10 `add_overlay`/`cut_overlay` guard `if(!overlays) return` (overlays.dm:70,79) tests the atom's builtin list, which is never null: effectively dead. Translate as a null check of the argument (same outcome).
- H11 Multiple things mutate shared globals re-entrantly (`late_loaders`, `created_atoms`, `queued_deletions`) when a template loads inside `Initialize`/`LateInitialize`; port as written, do not "fix".
- H12 Planes: `SET_PLANE_IMPLICIT` consults `SSmapping.max_plane_offset`; in 3D rendering this is irrelevant. Keep `max_plane_offset = 0` in the world so the macro reduces to `plane = new_value`.
- H13 Stringification: `"[src]"` yields `name`; null interpolates as empty; type paths as `/obj/foo`; `REF(x)` must be a deterministic id (used as map-loader source keys, trait sources).
- H14 `INITIALIZE_IMMEDIATE` is inherited by subtypes: implement as a type attribute resolved once in the TypeRegistry.
- H15 Atom `New` overrides whose first parameter is not `loc` (e.g. `/obj/effect/projectile/New(angle_override, ...)`, `buildmode/New(bld)`): about 5 of the 28 (projectile, 3 buildmode buttons, gas overlay); about 15 more declare NO params (`mob/New()`, `area/New()`, 8 telecomms presets, `space/basic`) and rely on H16. The engine takes `loc` from `args[1]` only when it is an atom; the factory must not coerce non-atoms.
- H16 Implicit `..()` forwards ALL actual arguments, including ones beyond the declared params: `mob/New()` (`modules/mob/mob.dm:59`) declares none, yet `..()` must hand `loc` to `atom/New(loc, ...)`, otherwise `args[1] = ...` (atoms_initializing:72) would index an empty list. Explicit `..(loc)` (`turf/New`, turf.dm:119-123) forwards only what is written. The C# chain is `New(DmArgs args)` -> `base.New(args)`; the translator binds declared params from `args` and forwards the same object unless an explicit arg list is written.

## 9. Builtin/idiom shim list (most used first)
| Name | Uses | Notes | C# |
|---|---|---|---|
| `SEND_SIGNAL` | 1534 (+54 global) | lazy args, int bitfield result, list out-params | `Signals.Send(target, sig, ...)` with generic arities + `HasListener` fast path |
| `RegisterSignal` | 4665 | handler = `PROC_REF` | `Signals.Register(target, sig, handler)` |
| `istype`/`ispath` | 6014/808 | 1-arg form uses declared type | `x is T` / `TypeRegistry.IsSubtype` |
| `isnull`, `length` | 4888/3799 | `length(null)==0` | `Dm.IsNull`, `Dm.Length` |
| `get_turf` | 3743 | walks `loc` to a turf; null in nullspace | `Dm.GetTurf(atom)` |
| `loc`, `contents` | 5488/1626 | silent re-parent, ordered append | `Atom.Loc` setter via `World.SetLoc`; `ContentsList` |
| `qdel`/`QDEL_NULL` | 3565/1393 | hint protocol (`QDEL_HINT_*`) | `Dm.Qdel(datum, force)` |
| `initial()` | 3230 | prototype default | `TypeRegistry.Initial(type, name)` |
| `locate` | 1796 | by type/tag/xyz | `World.Locate` |
| `new` (var type) | 4934 | typepath variable | `Dm.@new(type, args)` |
| `prob`/`rand`/`pick` | 2003/2118/2520 | seeded RNG | `Dm.Prob/Rand/Pick` on world RNG |
| `world.time` | 1197 | deciseconds, float | `world.time` (`float` per D64, 0.5 ds per 20 TPS tick, exact up to 2^23 ds, about 9.7 days) |
| `animate`/`flick` | 944/198 | presentation | sink calls |
| `addtimer` | 1612 | `CALLBACK`, flags | `Timers.Add` |
| `sleep`/`spawn`/`INVOKE_ASYNC` | 347/201/942 | waiting model | continuation API |
| `update_appearance` | 2860 | bitmask protocol | method on `Atom` |
| `overlays`/`underlays`/`vis_contents`/`filters` | 792/126/271/135 | special lists | `OverlayList`, `VisContents` |
| `appearance` | 90 | immutable snapshot | `Appearance` |
| `step`/`get_step`/`get_dir`/`turn` | 82/564/391/160 | direction math, `step` calls `Move` | `Dm.*` |
| `typesof`/`subtypesof`/`typecacheof` | 946/449 | registry | `TypeRegistry` |
| `arglist`/`args` | 130/267 | vararg forwarding | `DmArgs` |
| `stack_trace`/`CRASH` | 909/939 | log vs runtime | `stack_trace` logs and returns, `CRASH` throws `DmCrashException` |
| `REF`/`WEAKREF` | 1571/512 | deterministic ids | `Datum.Ref`, `WeakRef` |
| `HAS_TRAIT`/`ADD_TRAIT` | 2873/1239 | trait datum | traits module |
| `LAZYACCESS/ADD/LEN/NULL/...` | 2305 | null-safe list helpers | `Dm.Lazy*` |
| `round(a, b)` | n/a | float32 | `Dm.Round` |
| `-INFINITY` | 1 | `1e31` | `Dm.Infinity` |
| `src in container` | 50 | membership in atom = its contents | `Dm.In(x, atom)` |
| `world` iteration | 57 | all atoms | live enumeration in creation order, NOT a snapshot. BYOND ref, for list proc: "with the exception of looping through world, you're actually looping through a copy of that list"; OpenDream `WorldContentsEnumerator` (DreamEnumerators.cs:138) enumerates `atomManager.EnumerateAtoms` live. `CreateAtoms` (atoms.dm:121) is unaffected in practice: atoms created during the loop initialize inline (state MAPLOAD) and are skipped as INITIALIZED_1 |

## 10. External deps (stub or translate)
- datum base: `Destroy` chain, `gc_destroyed`, `datum_flags`, `_listen_lookup`, `_SendSignal`, `weak_reference`, `_active_timers`, `qdel`, `GLOB` (use_preloader, _preloader_*, all_mutations...).
- movable/turf/area: `Move/doMove/forceMove/Moved`, `get_all_contents` (`__HELPERS/atoms.dm:2`), `moveToNullspace`, `turf/Initialize` and `area/Initialize` copies, `area_flags`, `isturf/isspaceturf`.
- SS/MC: `SUBSYSTEM_DEF`, `Master` init loop (sets `initialized = TRUE`), `SS_INIT_*`, `TICK_USAGE`/`Master.current_ticklimit`, `stoplag`, `SStimer` + `datum/callback`, `SSdcs` (global signals), `SSgarbage`.
- `SSmapping`: `max_plane_offset`, `gravity_by_z_level`, `plane_offset_blacklist`, `z_level_to_plane_offset`. `SSicon_smooth` (`add_to_queue`, `remove_from_queues`, `free_deferred`), `SSvis_overlays`, `SSshuttle`, `SSmouse_entered`.
- Atom-adjacent modules touched by Initialize/Destroy (stub as empty `[NotTranslated]` classes so field types resolve): `datum/reagents`, `datum/forensics`, `datum/storage`, `datum/wires`, `datum/ai_controller`, `datum/light_source` + `update_light`, `datum/atom_hud/alternate_appearance`, `datum/component/orbiter`, greyscale (`update_greyscale`), atom_colour (`add_atom_colour`, `update_atom_colour`), materials (`initialize_materials`, `set_custom_materials`), `setup_reskins`, `datum/armor` (`get_armor_rating`).
- Helpers: `string_assoc_list/string_list`, `deep_copy_list`, `is_type_in_typecache`, `is_type_in_list`, `typecacheof`, `dir2angle`, `REVERSE_DIR` (bit trick on direction bits `& 85`/`& 170`), `get_dir`, `step`, `shuffle_inplace`, `testing()`, `log_tool`, `key_name`, `AREACOORD`.
- Out of M0 entirely: `datum/mutation` + `generate_gene_sequence` (setupGenetics), `datum/blood_type`, `obj/projectile`, `datum/crafting_recipe`, `show_radial_menu`, screentip/hud classes, `SSshuttle.mobile_docking_ports`, `is_centcom_level/is_reserved_level`, `heretic` antag datum.

## 11. M0 scope recommendation (per proc)
**Translate fully in M0** (about 950 of 2352 DM lines, 40%):
- `atoms_initializing_EXPENSIVE.dm` all (`InitAtom`, `New`, `Initialize` with stubs for greyscale/colour/light/materials/ai_controller/reskins/smoothing/hud string cache, `LateInitialize`) + `INITIALIZE_IMMEDIATE`.
- `controllers/subsystem/atoms.dm` all except `setupGenetics` (stub), `InitLog` text file write and `Shutdown` (log only), `Recover`, `PROFILE_*`. Keep `initialized_state` stack, `map_loader_begin/stop`, `prepare_deletion`, `BadInitializeCalls`.
- `atom_appearance.dm` all, over `Appearance`/`OverlayList` + `IAppearanceSink`; `controllers/subsystem/overlays.dm` primitives (`add/cut/copy_overlay(s)`, `build_appearance_list`; skip the `realize_*`/`diff_appearances` debug procs).
- `atom_invisibility.dm` all.
- `_atom.dm`: vars block (converter), `Destroy` (null-safe against stub types), `CanPass`, `CanAllowThrough`, `Bumped`, `Entered`, `Exit`, `Exited`, `setDir`, `set_density`, `set_base_pixel_x/y`, `drop_location`, `AllowDrop`, `in_contents_of`, `HasProximity/OnProximityExit`, `return_air`, `has_gravity` (against a toy SSmapping), `relaymove` (balloon_alert stub), `isinspace`, `CanAStarPass`, and the one-line signal wrappers (`intercept_zImpact`, `attempt_charge`, `tamed`, `connect_to_shuttle`, `set_opened/closed`, `set_wires`, `get_dumping_location`, `get_remote_view_fullscreens`, `update_remote_sight`, `return_temperature`, `assume_air/remove_air/return_analyzable_air`).
- `atom_act.dm` signal skeletons: `fire_act`, `extinguish`, `ex_act`, `contents_explosion`, `blob_act`, `hitby` (+`hitby_react`; this is the first `addtimer` user), `acid_act`, `emag_act`, `narsie_act`, `singularity_pull/act`, `zap_act`, `on_log`, `container_resist_act`, `rcd_act/rcd_vals`, `attack_hulk`, `handle_slip/fall`.
- `atom_tool_acts.dm`: only the 14 empty `<tool>_act[_secondary]` + `item_interaction(_secondary)` + `ranged_item_interaction(_secondary)` virtuals (return `NONE`), so subtype overrides compile.
- `atom_defense.dm`: field declarations only (needed by `Initialize`).
**Stub as `NotTranslated`** (compile, throw on call): `emp_act` (wires/holodeck/animate), `projectile_hit`/`bullet_act` (M6 projectiles), `handle_ricochet`, `on_craft_completion`, `process_recipes`/`ShowProcessingGui`/`StartProcessingAtom`/`UsedforProcessing`/`OnCreatedFromProcessing` (crafting, M4a), `base_item_interaction`/`tool_act`/`base_ranged_item_interaction` (M4a, first real `use_tool`), `expose_reagents` and reagent predicates (chem), `onCentCom/onSyndieBase/on_escaped_shuttle/onAwayMission` (shuttle), `wash`, `get_blood_dna_list` x3, `update_inhand_icon`, `rust_heretic_act`/`do_rust_heretic_act`, `Topic`, `MouseEntered`, `on_mouse_enter`, `set_hover_maptext` (client UI).
**Later**: `atom_defense.dm` procs (M5 per ROADMAP, optionally pulled forward), the tool-act machinery (M4a), `handle_ricochet`/projectile reactions (combat).
**Deviations to register (light)**: no `stoplag` in `CreateAtoms` (deterministic load); `SETUP_SMOOTHING`, `update_light`, plane offsets as no-ops (3D, D87); runtime-isolation boundaries (H1); null/0 loss in `&&` returns (H3).

## 12. Tests worth porting with M0
- `modules/unit_tests/movement_order_sanity.dm` (49 lines): `Move`/`doMove`/`Moved` order plus `connect_loc` + `COMSIG_ATOM_ENTERED` interception: nearly the M0 "done" scenario.
- `create_and_destroy.dm` subset: `new` every translated atom type, `qdel`, then assert `BadInitializeCalls` has no `NO_HINT/QDEL_BEFORE/SLEPT` (lines 140-200).
- `subsystem_init.dm`, `timer_sanity.dm` (3 lines). Own tests: InitAtom state-machine table (INSSATOMS defer then `InitializeAtoms`, `INITIALIZE_IMMEDIATE`, `LATELOAD` at mapload vs runtime, QDEL hint, qdel-before-init), `update_icon` overlay diff table, invisibility priority table (including the negative-priority quirk).

## 13. Usage counts (project-wide, `*.dm` lines)
`/Initialize(` overrides 4327, `INITIALIZE_HINT_QDEL` 174, `_LATELOAD` 78, `_NORMAL` 11; `LateInitialize(` 71; `New(` overrides (atom types) 28; `update_icon_state` overrides 514, `update_overlays` 467, `update_name/desc/icon/appearance` overrides 57/54/55/50; `add_overlay(` 351, `cut_overlay(` 155, `cut_overlays(` 75; `Entered(` 61, `Exited(` 199, `Exit(` 2, `Move(` 60, `CanAllowThrough` 65, `Bumped` 44, `setDir` 33, `set_density(` 92, `set_opacity(` 37; `item_interaction` overrides 601, `screwdriver_act` 246, `wrench_act` 222, `crowbar_act` 186, `multitool_act` 130, `welder_act` 100, `wirecutter_act` 58, `tool_act` 18, `analyzer_act` 7; `take_damage` 38, `atom_destruction` 37, `ex_act` 92, `emp_act` 121, `fire_act` 32, `bullet_act` 31, `hitby` 20.

## 14. Proposed C# shim API and translation rules for this area
```
// Anomaly.Core.DmShim / Anomaly.Core.Tg (sketch, names are proposals and superseded by translation-rules.md)
abstract class Datum { int? GcDestroyed; uint DatumFlags; virtual int? Destroy(bool force=false); string Ref => ...; }
abstract class Atom : Datum {
  Atom? Loc { get; }                 // set only through World.SetLoc / Move chain / New
  ContentsList Contents { get; }     // ordered, insertion order == BYOND contents order
  int Flags1; bool Density, Opacity; int Dir; /* + generated var fields */
  virtual void New(DmArgs args);                     // atom/New 65-75: preloader + InitAtom
  virtual int? Initialize(bool mapload, DmArgs extra); // must call base; returns INITIALIZE_HINT_*
  virtual void LateInitialize();                     // fire-and-forget form (waitfor=FALSE)
  virtual int update_appearance(int updates = ALL);  // names are verbatim (translation-rules.md section 2)
  ...
}
static class World { static Atom New(TypeId t, params object?[] args); IEnumerable<Atom> AllAtoms; float Time; }  // superseded: Dm.@new, per-World state, float time
sealed class SSAtoms : Subsystem { int Initialized; List<(object src,int state)> InitializedState; ... }
readonly record struct InitHint(int Value);   // 0 NORMAL, 1 LATELOAD, 2 QDEL; Initialize returns int? so null != 0
sealed record Appearance(...);                // interned, value equality
interface IAppearanceSink {...}               // section 4.3
```
Rules (R-atom-N):
1. One C# class per DM type, generated skeleton from the converter (vars, defaults, abstract_type, INITIALIZE_IMMEDIATE attribute); proc bodies translated by hand/AI, each with `// tg: path:line` and the snapshot header.
2. C# constructors only initialise fields and never touch the World; every DM `New` body goes to `New`; `Dm.@new` = alloc, id + registry, pre-New hook (silent `loc` set), `New` through the `Dm.Call` binder (wave-0 decision); the preloader runs inside the translated `atom/New`.
3. Names are verbatim (translation-rules.md section 2): `update_appearance` stays `update_appearance`, parameter names keep their DM spelling so named-argument calls (`id=type`) bind; C# keyword collisions get `@`.
4. `. = expr` / bare `return` -> a local `dot` declared where the proc first touches `.` (translation-rules.md section 7.4); `return;` returns it.
5. `SEND_SIGNAL(...)` -> `Signals.Send(...)` and treat the result as `int`; `a || b` on ints -> short-circuit helper that returns the first truthy VALUE.
6. `SHOULD_CALL_PARENT(TRUE)` -> `[ShouldCallParent]`; `SHOULD_NOT_SLEEP` -> plain sync method (a call from it to a proc in a `DmTask` slot is `Waiting.NoSleep(...)`); `set waitfor = FALSE` -> the callee-side wrapper of waiting-model.md, inherited by every override of the slot; everything that can reach `sleep`/`do_after` through waiting calls (a `spawn` body or a waitfor=FALSE callee does not make its caller wait) is coloured per virtual slot (`docs/ai/specs/generated/colored-slots.tsv`), and a slot is `async DmTask` exactly when the generated `docs/ai/specs/generated/slots.tsv` says so (`suspends = yes`; a slot coloured only through `waitfor = FALSE` definitions stays synchronous).
7. `VAR_PRIVATE/PROTECTED` -> public field + `[DmPrivate]`/`[DmProtected]`; `PRIVATE_PROC/PROTECTED_PROC` -> the proc keeps its normal accessibility (`public virtual`) + `[DmPrivate]`/`[DmProtected]` (translation-rules.md sections 3-4): C# `protected` would reject tg's own calls such as `UNLINT(to_delete.Destroy(force))` from the global `qdel` (garbage.dm:377) on `PROTECTED_PROC(TRUE)` `Destroy` (datum.dm:126); `SHOULD_NOT_OVERRIDE` -> a marker attribute, the proc stays `public virtual`.
8. Typed vars that hold a path before init (`ai_controller`, `armor`, `smoothing_groups`, `custom_materials`) -> an `object?` field with typed accessor helpers (`AiControllerPath`, `AiController`), never two diverging fields.
9. Untranslated referenced types -> empty `[NotTranslated]` classes; untranslated procs -> `NotTranslated()` (throws in tests, isolated by the H1 boundaries in a running world).
10. Presentation procs (`animate`, `flick`, `playsound`, `balloon_alert`, `to_chat`, `visible_message`) always go through the matching sink so headless tests can record them.
