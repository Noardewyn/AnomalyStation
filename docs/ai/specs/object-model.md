# Object model: datums, identity, creation and per-world state

Status: v1 (wave 0), 2026-10-05. Contract for the DmShim dynamic package (`Dm.@new`, the binder, `DmTypes`, prototypes and `initial()`), for the Kernel (the object registry and the per-world extension slot) and for the Tg infrastructure (the engine parts of `datum` and `atom`, `TgState`, `GlobalProcs`). Translation forms are in `docs/ai/translation-rules.md` (sections 3, 7, 10); the engine contract of `loc`, `contents` and `Move` is in `docs/ai/specs/move-chain.md`; waiting is in `docs/ai/specs/waiting-model.md`. Two choices here wait for the owner at checkpoint C0 (D94): direct references with world ids instead of D45's handles, and C# type defaults instead of D45/D19 YAML prototypes (translation-rules.md 0.3).

## 1. Layers

| Layer | Namespace | Knows | Owns |
|---|---|---|---|
| Kernel | `Anomaly.Core.Kernel` | nothing above it | `World` (tick, RNG, scheduler, ids), the object registry, per-world extension slots, the state hash |
| DmShim | `Anomaly.Core.DmShim` | the Kernel; tg objects only through the interfaces of section 1.1 | DM language semantics: values, `DmList`, text, `Dm.@new`, the binder, `DmTypes`, prototypes, `initial()`, presentation sinks |
| Tg | `Anomaly.Core.Tg` | both | translated classes, their engine parts (`src/Core/Tg/Builtin/`), `GlobalProcs`, `Builtins`, `TgState`, generated `Defines` |

References point down only (Tg -> DmShim -> Kernel).

### 1.1 Interfaces the shim defines and Tg implements

- `IDmObject`, implemented by `datum` (the engine part `datum.builtin.cs`):
  - `long dm_id { get; }`: the world id, 0 while the object is a prototype or not yet created by `Dm.@new`;
  - `void dm_assign_id(long id)`: called once by `Dm.@new`; a second call throws;
  - `void dm_pre_new(DmArgs args)`: the pre-New hook (section 4); `datum` runs `dm_init_vars()`, `atom` adds the silent placement.
- `IDmAtom`, implemented by `atom`: `DmList contents { get; }`, `string name { get; }`, `string gender { get; }`. The shim uses it for `in` and `for (... in atom)` (contents), `locate(T) in atom`, and for rendering an atom inside text (`\the`, articles, `\he`/`\his`, proper nouns).

Builtin value types that the shim implements itself (`DmList`, `regex`, `matrix`, `exception`) need no interface.

---

## 2. Classes and builtin vars

`/datum` is `public partial class datum : IDmObject, IStateHashable`; every DM type is a class named and placed by translation-rules.md 2.3, carrying `[DmPath]` once (a BYOND builtin type on its engine part `src/Core/Tg/Builtin/<class>.builtin.cs`, since BYOND declares it before the `.dme`). C# constructors are parameterless and public; classes are never `abstract` or `sealed`.

BYOND builtin vars are declared once, in the engine parts, with these C# types. "Property" means a property with engine behaviour in its setter; everything else is a plain public field like any tg var.

| Type | Var | C# | Engine behaviour |
|---|---|---|---|
| `/datum` | `type` | `Type type { get; }` = `GetType()` | read-only |
| | `parent_type` | via `Dm.parent_type(type)` | read-only |
| | `tag` | `string` property (backing field `_tag`, hashed) | a registered object with a non-empty tag is held strongly by the registry (`HoldStrongly`; BYOND: a tag is an implicit reference), and `locate(tag)` finds it (the shim scans the registry in id order; a per-world tag index can replace the scan without changing results); translation-rules.md 10.7 |
| | `vars` | not translated | VV and Recover only |
| | `dm_id` | `long` (read-only) | section 3 |
| `/atom` | `loc` | `atom` property | the raw, silent move of move-chain.md: leaves the old `contents`, appends to the new one, fires nothing |
| | `x`, `y`, `z` | `float` properties | derived from `loc` (move-chain.md) |
| | `contents` | `DmList` property | engine-maintained and never null; mutating it through list operators moves atoms (move-chain.md) |
| | `name` | `string` property (backing field `_name`, hashed) | appearance notification (`Changed(Name)` and the replication hint sink; M2: BYOND's name is an appearance var, and tg's humans rename themselves, replication.md 5.2) |
| | `desc`, `gender`, `text`, `suffix` | `string` fields | none |
| | `density`, `luminosity`, `mouse_opacity`, `infra_luminosity` | `float` fields | none |
| | `invisibility`, `opacity` | `float` properties | visibility and light change notification (presentation-seam.md) |
| | `icon` | `object` property | appearance notification |
| | `icon_state`, `maptext` | `string` properties | appearance notification |
| | `dir`, `layer`, `plane`, `alpha`, `pixel_x`, `pixel_y`, `pixel_w`, `pixel_z`, `appearance_flags`, `blend_mode`, `maptext_width`, `maptext_height`, `maptext_x`, `maptext_y`, `vis_flags` | `float` properties | appearance notification |
| | `color`, `transform`, `filters`, `render_target`, `render_source` | `object` properties | appearance notification |
| | `overlays`, `underlays`, `vis_contents` | `DmList` properties (engine-owned lists) | the property never holds null: assigning null clears the list and assigning a list replaces its contents (`overlays = null` in tg's `cut_overlays`); a mutation notifies the appearance sink |
| | `vis_locs` | `DmList` property (a new list on each read) | the reverse of other atoms' `vis_contents`; assigning null takes the atom out of all of them. M4v slice 3: a lazy per-world index (a write to a `vis_contents` marks its holder, a read of any `vis_locs` reads the marked holders' lists; `Builtin/atom.vis_locs.builtin.cs`); empty outside a world |
| | `appearance` | `object` property | assigning an image, mutable appearance, atom or snapshot sets the atom's appearance vars through their setters; a read is a `DmAppearance` snapshot (M4v slice 3, tg's `flick_overlay_view`) |
| | `mouse_drag_pointer` | `object` field (default `MOUSE_INACTIVE_POINTER`, 0) | none (a visual hint) |
| | `mouse_over_pointer`, `mouse_drop_pointer` | `object` fields (defaults `MOUSE_INACTIVE_POINTER`, 0, and `MOUSE_ACTIVE_POINTER`, 1; M2) | none (visual hints) |
| | `mouse_drop_zone` | `float` field (default 0; M2) | none (a visual hint) |
| | `verbs` | not translated until the command layer | |
| `/atom/movable` | `glide_size` | `float` property | appearance notification |
| | `locs` | `DmList` property | `list(loc)` for a movable on a turf, empty otherwise (tg has no multi-tile bounds in the port's M0 scope) |
| | `animate_movement`, `step_x`, `step_y`, `bound_x`, `bound_y`, `bound_width`, `bound_height` | `float` fields | none in M0 |
| | `screen_loc` | `string` field | none in M0 |
| | `particles` | `object` field | none in M0 |
| `/mob` | `ckey`, `key` | `string` fields | none in M0 |
| | `client` | `client` field | none in M0 |
| | `sight`, `see_invisible`, `see_in_dark`, `see_infrared` | `float` fields | none in M0 |

Setters with engine behaviour must not touch the World for an object that is not registered (`dm_id == 0`, a prototype or an object still inside its constructor): they store the value and return. This keeps constructors World-free (section 3).

---

## 3. Identity [C0]

- Every datum created by `Dm.@new` gets `dm_id = World.Current.AllocateId()`: a `long`, unique per world, never reused, increasing in creation order. It is the object's identity for replication (M1), `REF()`, `locate(ref)` and state hashing.
- References between objects are direct C# references, not handles (D45 is reinterpreted, C0 item).
- **C# constructors never touch the World**: no `World.Current`, no ids, no registry, no `Dm.@new`, no sinks. A constructor only runs field initializers (constant defaults) and the subtype's default overrides (translation-rules.md 10.2). This is what makes prototypes safe (section 5) and keeps the id sequence independent of which prototypes happened to be built first.
- The text form of a reference (`REF(x)`, `\ref[x]`, `ref(x)`) is BYOND's shape, `"[0x" + a number in lowercase hex + "]"` (OpenDream `DreamRefManager.GetRefString`), with these numbers (v1.2; `src/Core/DmShim/DmRefs.cs`):

  | Value | Number | Example | `locate()` of it |
  |---|---|---|---|
  | `null` | 0 | `"[0x0]"` | null |
  | a datum with a world id | `dm_id`, no kind prefix | `"[0x1f]"` | the object while it is registered (section 6) |
  | a plain list | `0x0F000000` (the kind of BYOND's and OpenDream's `RefType.DreamObjectList`; tg's `TYPEID_NORMAL_LIST` is `"f"`, `__DEFINES/typeids.dm`) or'd with a per-world list number | `"[0xf000001]"` | the list while it lives |
  | an alist (BYOND 516) | `0x55000000` (OpenDream's `RefType.DreamObjectAssocList`) or'd with a per-world alist number | `"[0x55000001]"` | the alist while it lives |
  | a typepath | `0x09000000` (OpenDream's `RefType.DreamType`, one kind for every typepath where BYOND splits 0x08 to 0x0b) or'd with the type's ordinal in DM object-tree order (`DmTypes.TreeOrdinal`, section 9) | `"[0x9000002]"` for `/datum` | the `Type` |
  | `world` | `0x0E000000` (BYOND's world kind, number 0; tg's Lua helper takes `"[0xe000001]"`, the next number of that kind, for `global`, `_lists.dm:1077`) | `"[0xe000000]"` | `world` |

  A list gets its number on its **first** `ref()` in a world, in the order of the `ref()` calls, so two runs number the same lists alike (deterministic; the numbers are never reused, and the next number of each kind is part of the shim's state hash, `DmWorldState`). The list table holds lists weakly, like the registry (section 6): a list nothing else references is collected and its ref then locates nothing, as a DM list is deleted at reference count 0, with the same GC-timing caveat as for datums. The numbers of a kind stop at `0xFFFFFF` (16.7 million ref'd lists per world, then a runtime error; BYOND's limit counts live lists only). A typepath's number is fixed by the generated type order, the same in every world and run of one build. A resource (an icon or sound file, `DmResource`) has OpenDream's kind `0x27` (`RefType.DreamResource`) and a number the world allocates on the first `ref()` of its path, in `ref()` order (resources are values equal by path and live as long as the world; the counter and the paths are in the state hash once a resource was ref()'d; added in M3: tg's bespoke elements key on `REF()` of an icon argument). So that no datum's ref reads as one of these, `Dm.@new` never gives a datum an id whose top byte is a kind above (`0x09`, `0x0E`, `0x0F`, `0x27`, `0x55`: it skips each such range of 16.7 million ids).
- `locate(text)` parses the number and looks it up by kind: the list table, the type order, `world`, else the registry (section 6); a tag (`locate("My Object")`) is looked up in the tag index. A missing, collected or qdel'd object and a collected list give null.
- Builtin value types (`/list`, `/regex`, `/matrix`, `/icon`, `/image`, `/mutable_appearance`, `/sound`, `/generator`, `/exception`) get no id and are never registered (a list has a ref number all the same, above).
- `ref()` of any other value without a world id (a builtin value such as a filter, an image or a matrix, a prototype, a number, text) is a runtime error, not a ref text that `locate` could not resolve: the port has no number to put in it (wave-1 dynamic package). **Open item:** BYOND also refs text (kind 0x06, the string table) and numbers (kind 0x2A, OpenDream "most numbers clobber the ref type"); tg compares `ref()` of arbitrary values only in the Lua editor helpers (`_lists.dm:1077`, `:1261`, `:1295`), which are not translated, so text and numbers stay strict until a translated caller needs them.
- **Open item: `TYPEID`.** tg reads BYOND's type id out of ref text: `TYPEID(thing)` is `copytext(REF(thing), 4, 6)` (`__DEFINES/_helpers.dm:47`), `GLOBAL_VAR_INIT(refid_filter, TYPEID(filter(type="angular_blur")))` (`__DEFINES/is_helpers.dm:20`) runs during `GLOB` initialization and `isfilter` compares against it, and VV's `IS_NORMAL_LIST` parses `"\ref[L]"` against `TYPEID_NORMAL_LIST` (`__DEFINES/typeids.dm`). Translated as written, the `refid_filter` initializer would raise inside `GLOB.Initialize` (the rule above), so it needs a decision when globals are translated. Proposal: **a fixed per-builtin-type id table.** `TYPEID(thing)` and `IS_NORMAL_LIST(L)` become hand-written `GlobalProcs` members (translation-rules.md 11.1) that take the value and look its kind up in a fixed table instead of cutting a ref (`IS_NORMAL_LIST` hands `"\ref[L]"` to `GET_TYPEID`, which then has no translated use): tg's own values where tg names them (`TYPEID_NULL` "0", `TYPEID_NORMAL_LIST` "f" for a plain list), one distinct constant per other builtin kind tg tells apart (a filter and the `filters` list share one, as in BYOND, `is_helpers.dm:19`; an alist, an image, an appearance, ...) and one for registered datums. `ref()` stays strict, `isfilter` works unchanged (it compares two `TYPEID` results), and nothing parses the port's ref text. (Since v1.2 a list has a ref, so `IS_NORMAL_LIST` as written would already give the right answer for a plain list and an alist, `GET_TYPEID` reading `"f"` and `"55"`; a datum's ref has no kind byte and a filter none at all, so the table is still the proposal.)

---

## 4. Creation: `Dm.@new`

`Dm.@new<T>(params object[] args)` and `Dm.@new(Type type, params object[] args)` (DM `new T(args)`; arguments may contain `Dm.named(...)` items and one `Dm.arglist(L)`). Steps:

1. **Builtin value types** (section 3) are constructed by their own factory from the arguments and returned; steps 2-6 do not apply.
2. **Construct**: a cached factory for the type calls the parameterless constructor: the field initializers (constant declarations; C# runs them before the constructor bodies, and their order does not matter because each var is declared once and its initializer is a constant), then each constructor body from `datum` down (subtype overrides in DM declaration order, so the most derived override wins).
3. **Identity**: `dm_assign_id(World.Current.AllocateId())`, then register the object in the world registry (section 6).
4. **Bind the arguments** of the `New` call (section 12): positional arguments in order, `Dm.named` items by the DM parameter names of the runtime type's most-derived `New` definition (`[DmParams]`), `Dm.arglist(L)` expanded (text keys with values become named arguments). For atoms, the initial location is the first positional argument, or the argument named `loc` if there are no positional arguments, whatever the `New` definition calls its first parameter (BYOND ref "New proc (atom)").
5. **Pre-New hook** `dm_pre_new(args)`:
   1. `dm_init_vars()`: the object-creating var initializers, each class's override calling `base.dm_init_vars()` first (OpenDream's per-type init proc calls its parent's first), so an ancestor's initializer runs before a subtype's (translation-rules.md 10.2);
   2. atoms: the silent placement into the initial location: `loc` is set raw (move-chain.md: appended to `loc.contents`, no `Enter`/`Entered`/signals). BYOND: "By the time New() is called, the object has already been created at the specified location and all of its variables have been initialized." Creating a turf or an area at a location (BYOND replaces the existing turf) belongs to the map spec of M3; in M0 only the test world builder creates turfs.
6. **New**: the binder calls the `New` slot (`public virtual DmTask New(params object[] __extra)` at the snapshot, because the slot suspends its callers; `void` for a slot that does not, `slots.tsv`) on the runtime type with the bound arguments. tg's map preloader (`world.preloader_load`) runs inside tg's own `/atom/New`, which is translated.
7. Return the object.

A runtime error inside `New` unwinds through `Dm.@new` to the creating code's isolation boundary (DV1); the object stays allocated and registered (in DM, `New` would end and `new` would still return the object).

**Sleeping `New`.** In DM the creating proc waits while `New` sleeps. `Dm.@new` is synchronous: when the `New` it calls suspends, the no-sleep rule applies (`Waiting.NoSleep` semantics, DV5): a runtime is reported, `Dm.@new` returns the object and `New` continues detached. `Dm.new_async<T>(...)` / `Dm.new_async(Type, ...)` return `DmTask<T>` / `DmTask<object>`, completed when `New` returns; translated async code uses them for the types whose `New` may sleep (`docs/ai/specs/generated/sleeping-new.tsv`; translation-rules.md 10.1).

---

## 5. Var defaults, prototypes and `initial()`

- **Constant defaults** are C# field initializers in the declaring class; **subtype overrides** are assignments in the subtype's constructor in DM declaration order; **object-creating or non-constant defaults** (`list()`, `new`, calls) are assignments in `dm_init_vars()` (translation-rules.md 10.2). Until the type-tree converter generates these parts, translators write them.
- **Prototypes.** `initial()` and `T::var` read a prototype: one instance per type, built by the cached factory of step 2 only (constructor; no id, no registry, no `dm_init_vars`, no `New`). Prototypes are kept in a process-wide cache keyed by `Type`; the cache is append-only and a prototype is never handed out or mutated (translated code only reads fields through `Dm.initial`), so this is an allowed immutable static (ROADMAP principle 6). A prototype can be built with no ambient World.
- `Dm.initial(x, (C p) => p.var)` uses the prototype of `x`'s runtime type when `x` is an object, of `x` itself when `x` is a `Type`; the lambda reads the field (a static lambda, cached by C#, no allocation). A runtime-initialized var reads null on the prototype, as `initial()` does in DM (its compile-time value is null).
- `parent_type::var` inside a var override reads the parent type's prototype from the subtype's constructor; that builds a different type's prototype, so there is no recursion.

---

## 6. The world registry

The Kernel keeps, per `World`, the registry of every object created by `Dm.@new` (requested Kernel API, `World.Objects`):

| Member | Meaning |
|---|---|
| `Register(long id, object obj)` | called by `Dm.@new` step 3 |
| `Unregister(long id)` | called by tg's translated `qdel` (section 7) |
| `TryGet(long id, out object obj)` | `locate(ref)` |
| `HoldStrongly(long id, bool hold)` | the `tag` setter (a tagged object stays alive) |
| `ForEachLiveInIdOrder(Action<long, object>)` | state hashing, `for (... in world)`, `locate(T)` in the world |

- Entries are **weak** references (strong only while tagged): an object that nothing references any more disappears from the registry when the C# GC collects it, as a DM datum is deleted when its reference count drops to zero. tg relies on that for the many datums it never `qdel`s (callbacks, points, temporary lists of datums).
- Iteration is in id order, which is creation order, except over the world's atoms. `for(var/mob/M in world)` enumerates `world.contents`, the live registered atoms of the loop type, in the world's order: mobs, then the other movables, then areas, then turfs, each group in id order (OpenDream `AtomManager.EnumerateAtoms`; `IDmAtom.dm_world_order`). tg's `SSatoms.InitializeAtoms` relies on it: a map's objects are initialized before the turf whose `levelupdate` hides the undertile ones (found by M3 P4; before, the port walked atoms in id order). It enumerates only atoms whatever the loop type; a loop without `in` (`for(var/datum/D)`, `Dm.each<datum>()`) and `locate(T)` without a container enumerate the atoms of an atom type and the registered non-atom datums of `/datum` or a non-atom type (translation-rules.md 6.5).
- **GC timing.** A non-atom datum that nothing references any more is still registered until the C# GC collects it, so a loop without `in` over datums can find it, where BYOND would have deleted it at reference count 0: such a loop's result depends on GC timing (state hashing forces a full collection first, section 8). Atoms stay reachable through their location, so atom iteration does not.
- Performance: one weak handle per datum. If the handle churn of short-lived datums shows in M1 profiles, a type may opt out with `[DmUnregistered]` (never located by ref, never hashed); no type does so in M0.

### 6.1 Replication seams (M1)

The server's replicator (`Anomaly.Server.Replication.IReplicator`, scope 2d R3) compares only candidate objects with its own shadow copies after `World.Step`. Candidates come from hints, which select and never carry values (presentation-seam.md rule 5), so a missed hint costs at most a sweep's delay and never a desync. The four kinds and their sources, chosen by the M1 contracts step:

| Hint | Source | Why this source |
|---|---|---|
| moved | `IReplicationHintSink.Moved(atom)`, called by the atom engine parts after every raw change of an atom's container: the `loc` write (`dm_set_loc`, which `Move`, `forceMove`, `abstract_move` and the silent placement of a new atom all reach), a DM contents edit when the engine applies it (`dm_reconcile_contents`), and a turf replacement (`dm_swap_held`, `dm_move_contents_to`) | `loc`, `x`, `y`, `z` and `contents` all change only there (move-chain.md E1) |
| appearance changed | `IReplicationHintSink.AppearanceChanged(atom, aspect)` and `VisibilityChanged(atom)`, forwarded from the same points as `IAppearanceSink.Changed` and `VisibilityChanged` (`dir`, `glide_size`, `icon_state`, `opacity`, ...) | the appearance sink stays the presentation events' (`animate`, `flick`); the hint sink only marks |
| created | **no call: a watermark.** `World.Objects.HighestId` is the highest id ever registered. Ids increase and `Dm.@new` registers at allocation, so the objects created since the replicator last read the watermark are the live registered objects above it, which `ObjectRegistry.TryGetNextLive(watermark)` walks in id order | most created datums are not atoms and never replicate; a walk from the watermark costs nothing per creation and filters once per tick, where a call would cost every `Dm.@new` |
| deleted | `IReplicationHintSink.Deleted(id)`, from the registry's `ObjectRegistry.Unregistered` event, which `Unregister` raises after removing an entry (qdel's paths and a replaced turf, section 7); `PresentationSinks.Replication`'s setter subscribes the sink to it | removal from the registry is the one place every deletion passes; an entry the GC collects raises nothing (its timing is not deterministic, and it is never an atom on a turf) |

- **Unregistered handlers.** A handler of `ObjectRegistry.Unregistered` runs inside the world, in the middle of qdel: it only records the id, or queues a raw scheduler entry for later in the same tick (`World.Scheduler.After(0, ...)`: an isolation boundary that runs after the current phase or drain), and never runs world code inline. The `/client` engine part does the latter for the deleted mob of a connected player (`Builtin/client.builtin.cs`, dm-conformance.md "The /client engine part"); a deleted client itself is disconnected inside qdel by `SSgarbage.HardDelete` (section 7).
- **Per world, no static state.** The sink is `PresentationSinks.Replication` of the world's `DmWorldState` (section 10), null by default; the registry event belongs to the world's registry. A world without a sink pays one null check per hint site.
- **Neutral.** A sink is write-only (presentation-seam.md rule 1) and keeps no reference to a world object across ticks (ids, not objects), so a world with a sink computes the same state and the same `World.ComputeStateHash()` as without one. `M1Seams/ReplicationHintHashTests` runs the M0 toy scenario both ways.
- **Read outside the world.** The replicator reads after `World.Step` with no ambient world: engine getters read raw (a DM contents edit not yet applied by the engine reaches the replica one tick later), and the map through `Anomaly.Core.Tg.MapView`. It never writes the world.

---

## 7. qdel and deletion

- tg's `qdel` (`code/controllers/subsystem/garbage.dm:346`), `Destroy` and the `QDEL_*` macros are translated literally (translation-rules.md 10.5). SSgarbage's queues, its hard delete and its reference tracking are not (DV7). The calls that tg's `qdel` makes after `Destroy` go to a small `SSgarbage` shim with tg's names: `Queue(D, level)` does what tg's `Queue` does to the datum (`if (D.gc_destroyed <= 0) D.gc_destroyed = world.time`, garbage.dm:274-275) and then unregisters it; `HardDelete(D)` only unregisters it (no BYOND `del`, no nulling of references), except that a `/client` gets BYOND's `del` of a client first: its `Del` runs and disconnects the player (`client.dm_del`, M1). The `QDEL_HINT_IWILLGC` branch, which in tg only sets `gc_destroyed = world.time` and returns, gets one added line in the translation, `World.Current.Objects.Unregister(to_delete.dm_id); // DEVIATION(DV7)`. `QDEL_HINT_LETMELIVE` without force keeps the registration (tg resets `gc_destroyed` to null).
- A qdel'd datum stays in memory while referenced, exactly like a DM datum with references, and keeps its field values; `QDELETED` (DV2) and `QDELING` see it as deleted. It is no longer hashed, located or iterated.
- `del(x)` (hard delete) is not translated (`Dm.NotTranslated`); tg calls it only from SSgarbage internals and for non-datums in `qdel`.
- tg's `/datum/weakref` is translated as tg code (`resolve()` locates its datum by ref in the registry). It also holds its datum (DV90), so `resolve()` gives it until it is deleted, whenever the GC runs.

### 7.1 A replaced turf (the ChangeTurf identity decision, M3 P2)

BYOND's turf reference is its location: after `new path(T)` replaces the turf at `T`'s cell (tg's `ChangeTurf`,
`change_turf.dm:112`; the map reader's `create_atom` of a tile's turf), every reference that named the old turf, `src`
included, names the new one. The port's references are C# object references, so the engine (`MapGrid.Replace`,
`Builtin/MapGrid.cs`) gives the new turf the cell, the old turf's slot in its area's `contents` (`dm_swap_held`) and the
old turf's movables in their order, and takes the old turf off the map (x = y = z = 0, no area, no contents) and out of the
registry, marked deleted (`gc_destroyed`, unless tg's `ChangeTurf` qdel'd it already). A reference to the old turf then
names a deleted object that is not on the map; the engine links it to the new turf (`dm_replaced_by`, set by `MapGrid.Replace` for
every replacement, `ChangeTurf`'s `CHANGETURF_SKIP` and the map generators' bare `new` included), which `turf.dm_identity` follows.

- **TURF-IDENTITY (the rule for translated code).** Where tg goes on using a turf reference after replacing the turf and so
  reaches the new turf, the translation takes the new turf instead, at the site, marked `// TURF-IDENTITY`: `ChangeTurf`'s
  tail writes `new_turf` (its lighting corners, `lattice_underneath`, `weak_reference`, `blueprint_data`, the starlight and
  smoothing calls, `z_level_to_plane_offset[z]`), `build_coordinate` creates the tile's movables on the new turf, and
  `attempt_lattice_replacement` puts the lattice on the turf `ScrapeAway` left. A reviewer of a translation that replaces a
  turf checks every use of the old reference after the `new`.
- **Lists and vars that held the old turf** keep the deleted object; the engine does not rewrite them (only the area's
  `contents`). On tg's station load this never matters: `build_coordinate` with `new_z` adds no turf to an area's
  `turfs_by_zlevel`, and `_load_impl`'s tail fills those lists from the final turfs (`build_area_turfs`), so no list holds a
  replaced turf after the load.
- **The M5 obligation.** A `ChangeTurf` of a turf that a list holds (an area's `turfs_by_zlevel` and
  `turfs_to_uncontain_by_zlevel`, `SSmapping.unused_turfs`, a machine's or a component's turf list, `GLOB` lists of turfs)
  must replace the entry with the new turf, where tg relies on the location reference: construction and destruction (M5)
  are the first to change station turfs after the load, so M5 finds those lists and replaces the entries at the
  `ChangeTurf` site (or the lead gives the engine an index of turf holders, a contract change). Until then a replaced
  turf in such a list reads as deleted (`QDELETED`), which is what the list's users see after a turf's removal.
  Met so far: the area's two lists (M3 P3, `dm_replace_listed_turf`) and the atmos lists (M4b slice 1,
  `dm_replace_atmos_turf` in `game/turfs/change_turf.cs`): the atmos-adjacent neighbours' `atmos_adjacent_turfs` (the key
  and its value), SSair's `active_turfs`, `currentrun`, `adjacent_rebuild`, `high_pressure_delta`,
  `active_super_conductivity`, `atom_process` and `queued_for_activation`, and the replaced turf's excited group's
  `turf_list` (tg's `CHANGETURF_INHERIT_AIR` hands that group to the new turf).
  M4b slice 2: the `post_change_callbacks` of `COMSIG_TURF_CHANGE` (a callback's bound argument that is the replaced turf
  becomes the new turf before `InvokeAsync`; a firelock's `adjacent_change` binds the changed turf).
  M5 slice 1: the listeners' `_signal_procs` keys (`dm_replace_signal_turf`). M5 slice 4 (walls, girders, floors, lattice): the
  elements that listen to a turf and key their own lists by it, `give_turf_traits`' `trait_sources` (a lattice's or catwalk's turf
  traits, reapplied by its post-change callback from that key) and `footstep_override`'s `occupied_turfs`
  (`dm_replace_listener_turf`); the construction sites that read `src` after a timed step (`try_decon`, the r-wall's stages, the
  plating's reinforcing) treat a replaced turf as no longer theirs (`QDELETED(src)`), and `dismantle_wall`'s
  `QUEUE_SMOOTH_NEIGHBORS`, the plating's sounds, `ChangeToWall`'s return and space's `attempt_lattice_replacement` take the new turf.
  `GLOB.station_turfs` needs nothing: tg's own `Destroy` and `Initialize` of walls and floors take the old turf out and put the new
  one in. Also M5 slice 4: `ScrapeAway` reaches `ChangeTurf` through its overrides (`dm_change_turf_virtual`; it called the body
  directly before, so `/turf/open`'s `CHANGETURF_INHERIT_AIR`, the floor's direction and space's nearstation area were skipped).

---

## 8. State hashing

`World.ComputeStateHash()` combines the Kernel's own parts (seed, tick, RNG state, next id, runtime error count, scheduler queue; waiting-model.md section 13), the world registry and the contributors registered on the world (`World.AddStateHasher`, in ordinal order of their names; the shim's `DmWorldState` registers one). **The Kernel hashes the registry itself** (`ObjectRegistry.HashInto`):

1. a full blocking garbage collection (`GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`), so that weak registry entries reflect reachability and two runs hash the same set of objects;
2. for each live registered object, in id order: its id, then its own state through the Kernel's hook `IStateHashable.HashState(ref StateHasher)` (an object that does not implement it adds its C# type name), then the number of objects hashed.

Tg's `datum` implements `IStateHashable` in its engine part through the **field hasher** `DmFieldHasher` (internal), which lives in `src/Core/Tg/Builtin/datum.builtin.cs` next to its users and stays there: it reads the shim only through public members (`DmTypes.PathOf`, `Dm.pairs`, `Dm.isnum`, `Dm.num`). The field hasher writes the object's DM type path, then every instance field (any visibility) declared by a class that carries `[DmPath]`, in ordinal order of field name (builtin properties are hashed through their backing fields, `_tag`), except fields marked `[NotHashed]` (engine caches). Values: `float` by bits (-0 folded into +0), `float?` with a null marker, `string` by content, null by a marker, a number in any box (`int`, `long`, `float`) as the `float` of the same value (translation-rules.md 3.1), a reference to a registered object by its id, a reference to a **deleted** datum by its id and a "deleted" marker, a `Type` by its DM path, a `DmList` by length and then each item (and its associated value) by these same rules, a builtin value object (`sound`, `image`, `mutable_appearance`, `matrix`, `regex`, ...) by its own fields in name order, nested lists and value objects recursively with a cycle guard. "Deleted" means `gc_destroyed` is set: every path of the port that unregisters a datum (`SSgarbage`'s `Queue` and `HardDelete`, the `QDEL_HINT_IWILLGC` branch of `qdel`, section 7) sets `gc_destroyed` first, and hashing runs with no ambient `World` (outside `World.Enter`), so the hasher reads the field rather than the registry.

`TgState` (section 10) is hashed through its own contributor, which a world's `TgState` registers when it is created (`World.AddStateHasher(nameof(TgState), ...)`): every field that is not at its default (null, 0, an empty list) as its name and its value by the rules above, in ordinal order of name, so that creating `TgState` adds nothing to the hash. What the tests check is determinism: the same scenario twice gives the same `World.ComputeStateHash()`.

**Live objects and the build.**
- *Live* means reachable at the forced collection, including through the locals of suspended async methods. The C# compiler keeps a local of an async method across an `await` only if it is read after it, and a non-optimized (Debug) build keeps every local. So the core assemblies (Kernel, DmShim, Tg) compile with `<Optimize>true</Optimize>` in every configuration: the live set, and the hash, are the same in Debug and Release builds.
- BYOND keeps a proc's vars alive until the proc returns. tg's one pattern that depends on this is the stack canary of `Master.Loop` (`stack_end_detector.dm`), and the translation keeps it alive with `GC.KeepAlive` in the loop. A datum held only by a proc var of a sleeping proc, and not read after the sleep, may otherwise be collected earlier than in BYOND (DV7).
- The hash is compared between runs of one build of the code; nothing compares hashes across versions.

Not hashed: presentation sinks, prototypes, shim caches, the suspended frames of sleeping procs (waiting-model.md section 13). Hashing is a test and diagnostics operation (the forced collection costs milliseconds); nothing in the simulation reads the hash.

---

## 9. The type registry `DmTypes`

- Built once per process by reflection over the assemblies that reference the shim (the Tg assembly, the shim itself, test fixtures) from the `[DmPath]` attributes (an immutable static: allowed). It maps DM path <-> `Type`, knows each type's parent, children and its **tree ordinal**, and answers `typesof`, `subtypesof` (tg), `typecacheof` (tg), `ispath`, `text2path`, `"[type]"`, `parent_type`.
- Classes for implicit types (declared only by a proc or var definition such as `/datum/action/innate/mimic/lock/Activate()`) exist like any other.
- **Order.** tg depends on DM's object-tree order wherever it builds lists with `typesof`/`subtypesof` and iterates them: subsystem creation (`subtypesof(/datum/controller/subsystem)`, master.dm:110, whose order breaks ties of the stable init-stage sort), `GLOB`'s `InitGlobal*` procs (`typesof(/datum/controller/global_vars/proc)`, controllers/globals.dm), and many type caches. The port defines the order explicitly: **depth-first preorder of the type tree, children in the order of their first declaration in compile order** (the `tgstation.dme` include order, then line order within a file). This is OpenDream's order (`DreamObjectTree.GetAllDescendants`, children by type id, ids assigned in compile order); BYOND's exact order has not been checked against a BYOND run, and a difference would only change tie orders, so it is not registered as a deviation.
- **Order source.** The generated `src/Core/Tg/Generated/TypeOrder.g.cs` lists every DM type path of the production build in that order (`TypeOrder.Paths`, `TypeOrder.Count`), and `ProcOrder.g.cs` gives what `typesof(/t/proc)` and `typesof(/t/verb)` return for the types tg enumerates that way (`ProcOrder.Procs`, `ProcOrder.Verbs`, `ProcOrder.ProcsOf`; at the snapshot only `/datum/controller/global_vars`). Both are produced by `tools/coloring` (`typeorder.py`, `docs/ai/specs/colored-procs.md`, "Type order"), and the Tg assembly names them explicitly with `[assembly: DmTypeOrder(typeof(TypeOrder), typeof(ProcOrder))]` (`src/Core/Tg/Infrastructure/TypeOrderSource.cs`); the registry reads the named classes once. Without that attribute on any assembly there is no order source (every type sorts by the rule below); two assemblies with it are an error. A type missing from the generated order (test fixtures, hand-added types) sorts after the listed ones in a path-ordered preorder of the registered tree: a depth-first preorder with siblings by ordinal path, which is ordinal path order wherever paths follow the tree and keeps a type re-parented by `parent_type` inside its parent's subtree. `EveryTgClassIsListedInTheGeneratedTypeOrder` fails when a Tg `[DmPath]` class is missing from the list. `typesof(/t/proc)` for a type that `ProcOrder` does not list falls back to the class's own public methods in declaration order.
- BYOND builtin types map to classes too: `/datum` -> `datum`, `/atom` -> `atom`, `/atom/movable` -> `atom_movable`, `/area`, `/turf`, `/obj`, `/mob` (Tg); `/list` -> `DmList`, `/matrix`, `/exception`, `/sound` -> `sound` and `/world` -> the facade `DmWorld` (DmShim: `ISoundSink.Play` takes the `sound`, and a class named `world` would make the bare `world` ambiguous, translation-rules.md 10.8), `/regex` (DmShim, planned); `/image`, `/mutable_appearance`, `/icon`, `/generator`, `/particles`, `/client` (Tg builtin parts, presentation-seam.md; where `/image` and `/icon` live is an open item of `docs/ai/specs/shim-ownership.md`).
- **Procs on `/world`.** tg defines procs on `/world` (`/world/proc/push_usr`, `__HELPERS/mobs.dm:709`; later `incrementMaxZ` and the `world.New` boot). Since `/world` is the shim's facade `DmWorld`, each is a C# extension method of `DmWorld` (first parameter `this DmWorld world`), `public static` in `GlobalProcs`, in the mirror file of its `.dm` (translation-rules.md 1.5). `world.push_usr(M, this)` reads as in DM; the methods are not virtual (tg never subtypes `/world`); the binder reaches them by name (section 12).

---

## 10. Per-world global and static state

No class in the Tg or DmShim assembly has a mutable `static` field (ROADMAP principle 6: parallel tests use separate worlds). DM global and static state lives in **`TgState`**, one instance per `World`:

```csharp
// src/Core/Tg/Builtin/TgState.builtin.cs
public sealed partial class TgState
{
    public static TgState Current => World.Current.GetExtension(static w => new TgState(w)); // requested Kernel API
    public datum_controller_global_vars GLOB;            // GLOBAL_REAL(GLOB, /datum/controller/global_vars)
    public datum_controller_master Master;               // GLOBAL_REAL(Master, ...)
    ...
}

// src/Core/Tg/controllers/subsystem/timer.cs: each file adds its own part
public sealed partial class TgState
{
    internal datum_controller_subsystem_timer SStimer;   // SUBSYSTEM_DEF(timer)
}
```

The fields that translated files add to `TgState` in their own parts are `internal` (translation-rules.md 10.6): public access goes only through the accessors of the table below, so `AppendixCNameTests`, which requires every public `TgState` member to be named in translation-rules.md Appendix C, needs no exemption. The infrastructure part (`src/Core/Tg/Infrastructure/TgState.cs`) keeps `GLOB` and `Master` public for hosts and tests; they are named in Appendix C.

| DM | Field of `TgState` | Accessor |
|---|---|---|
| `GLOBAL_REAL(X, T)`, `GLOBAL_REAL_VAR(X)`, `SUBSYSTEM_DEF(x)` (`SSx`) | `X` | `public static ref T X => ref TgState.Current.X;` in `GlobalProcs` |
| `GLOBAL_VAR`, `GLOBAL_LIST_*`, `GLOBAL_DATUM_*`, `GLOBAL_ALIST_*` (vars of `/datum/controller/global_vars`) | instance fields of `datum_controller_global_vars` (the object in `TgState.GLOB`; before it exists, a per-world storage object that `GLOB = src` empties into it, translation-rules.md 10.6) | `GLOB.x`; GLOB as a value is `GLOB__var` |
| `var/static/x` and `var/global/x` on type `/a/b` | `a_b__x` | `public static ref T x => ref TgState.Current.a_b__x;` on class `a_b` |
| `var/static/x` in proc `p` of type `/a/b` | `a_b__p__x` | `private static ref T p__x => ref TgState.Current.a_b__p__x;` on class `a_b` |
| `var/static/x` in global proc `/proc/p` | `GlobalProcs__p__x` | `private static ref T p__x => ...` in `GlobalProcs` |

- **Initialization.** BYOND initializes global and static vars at world start, before `world/New`. `TgState` is created on first access within a world; its field initializers hold the constant and the object-creating initializers that create no registered datum (`list()`, `regex`, typecaches). Initializers that create registered datums are assignments in `TgState.InitStatics()`, called once by the world bootstrap before anything else runs, in DM compile order (`ProcOrder.g.cs` order). `GLOB`'s own `InitGlobal*` procs run in tg's order from `/datum/controller/global_vars/Initialize` (translated literally; its `typesof(/datum/controller/global_vars/proc)` iterates `ProcOrder.g.cs`).
- **Shim state** that is per world (presentation sinks, the tag index) lives in the shim's own extension object, `DmWorldState`, reached the same way.
- **Kernel request.** `World.GetExtension<T>(Func<World, T> create) where T : class`: one instance per world and type, created on first use, cheap to read (a slot indexed by a per-type static index). Extensions are not hashed by the Kernel; their owners register hash contributors.
- A reflection test (requested, Tg test project) fails when `Anomaly.Core.Tg` or `Anomaly.Core.DmShim` declares a static field that is not `readonly`, or a `static readonly` field whose type is a mutable collection, outside an allow-list of immutable caches (`DmTypes`, prototypes, binder caches).

---

## 11. Global procs and builtins

- Every tg `/proc/x` and every function-like tg macro that touches tg types is a static member of `public static partial class GlobalProcs` in `Anomaly.Core.Tg`, in the mirror file of its `.dm` file (translation-rules.md 7.8, 11.1). The shim finds it for calls by name (`call(GLOBAL_PROC, name)`, `Dm.Call(null, name)`, `CALLBACK(GLOBAL_PROC, ...)`) through an assembly attribute `[assembly: DmGlobalProcs(typeof(GlobalProcs))]`, read once.
- BYOND builtin procs that need atoms or the map are static members of `public static partial class Builtins` in `Anomaly.Core.Tg` (`src/Core/Tg/Builtin/*.builtin.cs`).
- Only true BYOND builtins and the port's helpers live in `Dm`; nothing there may share a name with a `#define` or a tg global proc (translation-rules.md 1.6).

---

## 12. The binder: `Dm.Call` and `New`

One binder serves calls by name (`call()()`, `PROC_REF` targets, signal handlers, callbacks), `Dm.@new` and `arglist`:

1. **Resolve** the method on the receiver's runtime type by name: the C# name, or the DM name (`x__proc` answers to `x`; `@` is not part of reflected names). A slot is invoked virtually (the most-derived override runs). A global proc is resolved on `GlobalProcs` when the receiver is null or `GLOBAL_PROC`. On the receiver `world` (the `DmWorld` facade) a name resolves against the `GlobalProcs` extension methods whose first parameter is `DmWorld` (tg's `/world` procs, section 9), with `world` passed as that first argument. An unknown name is a runtime error ("undefined proc").
2. **Bind** positional arguments to the C# parameters in order; missing ones are null (the callee's prologue applies DM defaults); extras go to `__extra` when the slot has it, else are dropped. Named arguments (`Dm.named`, text-keyed entries of `Dm.arglist`) are matched against the DM parameter names of the runtime type's most-derived definition of the slot: its `[DmParams]` when present, else the root's C# parameter names. An extra parameter's position in `__extra` follows that `[DmParams]` order. An unknown name is a runtime error (BYOND ref "named arguments").
3. **Convert** each bound argument to the C# parameter type by translation-rules.md 3.5 (`Dm.num`, `Dm.num_or_null`, `as T`, ...).
4. **Invoke** and return a `DmTask<object>`: a `DmTask` slot's task through `IDmTask.AsObjectTask()`, a synchronous slot's value as a completed task (`void` gives null) (waiting-model.md section 4.5).

Invokers are compiled once per (runtime type, name) and cached in an append-only static cache (allowed: immutable entries keyed by `Type`). Signal handlers that declare fewer parameters than the signal sends are served by step 2 (padding and dropping).

---

## 13. Requests and open items

- **Done in wave 1.** The Kernel requests: `World.Objects` (section 6), `World.GetExtension<T>` (section 10), the registry hash with `IStateHashable` (section 8). The tools: `TypeOrder.g.cs` and `ProcOrder.g.cs` (section 9) and `docs/ai/specs/generated/slots.tsv` (translation-rules.md 7.1), from the colouring scan. The tests of the dynamic package: the cross-world identity test (`Dynamic/InitialTests.BuildingPrototypesAndObjectsInOneWorldChangesNoOtherWorld`), the no-static-mutable-state reflection test (`Dynamic/NoStaticStateTests`), named arguments through `Dm.@new`, `loc` taken from the first positional argument or the one named `loc` (`Dynamic/NewTests`), a constant override of a runtime-initialized var (`Dynamic/InitialTests`).
- **Done in the pilot (wave 2).** The field hasher `DmFieldHasher` and `datum`'s `IStateHashable` (section 8); `TgState`'s hash contributor (section 8); `datum`'s `dm_pre_new` running `dm_init_vars()` (section 4); the `tag` property with the strong hold of a tagged object (section 2); `QDELETED` and `QDELING` at world time 0 (DV2, `__DEFINES/qdel.cs`); tg's procs on `/world` as `DmWorld` extension methods (section 9).
- **Open, for wave 3 (atoms).** `atom`'s silent placement from `DmArgs.InitialLocation` in its `dm_pre_new` (section 4).
- **Open, decisions.** The `TYPEID` table (section 3), when globals are translated; where `/image` and `/icon` live (`docs/ai/specs/shim-ownership.md`); BYOND's own type order has not been compared with a BYOND run (section 9).
