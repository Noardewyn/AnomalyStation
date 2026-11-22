# DmShim file ownership (wave 1)

Status: v1 (wave 1, contracts package), 2026-10-06. Which package edits which file of `src/Core/DmShim/`, so that the
**values** and **dynamic** packages, which build in parallel on the frozen API, never edit the same file. The API
itself is `docs/ai/translation-rules.md` Appendix C; the semantics are the rules, `docs/ai/specs/object-model.md` and
`docs/ai/specs/presentation-seam.md`.

## Rules for every package

- **The public surface is frozen.** Every public name and signature that exists now is the contract translated code
  and the other package build on. Bodies may change freely; a signature change, a rename or a removal goes through the
  lead (and Appendix C). Adding an overload is allowed when it creates no ambiguity for the call shapes of the rules
  (run `AppendixCNameTests` and the probe `tests/Anomaly.Core.Tests/Contracts/TgNamespaceProbe.cs`).
- **Owner "values" or "dynamic" with "written by contracts"** means: the code there is complete and tested
  (`tests/Anomaly.Core.Tests/Contracts/`), the owner hardens it and may rewrite its internals without changing behaviour
  the other package relies on.
- A skeleton body throws `NotImplementedException("wave1:values")` or `("wave1:dynamic")`; the tag names the owner.
  `"later: presentation"` marks names no wave-1 package implements.
- **New files**: a package adds its own files freely (`Dm.<Topic>.cs` parts of the static partial class `Dm`,
  internal helpers) and lists them in the same commit as new rows at the end of its own table below (above its
  append marker), so that the parallel packages never edit the same lines of this file. Per-world state goes into a new part of the
  partial class `DmWorldState` in the package's own file (`DmWorldState.Values.cs`, `DmWorldState.Dynamic.cs`); state
  that affects the simulation registers its own hash contributor (`World.AddStateHasher`), as `DmWorldState.cs` does,
  and the contributor adds nothing while that state is the default, so creating it (a host reading a value or
  installing a sink) never changes the state hash.
- **No name of tg.** No public member of `Dm` (and no public type of the shim) may be named like a tg `#define` or
  global proc (rules 1.6). `DefineClashTests` scans the tg build (`tgstation.dme` and every `.dm` under `code/`,
  `interface/` and `_maps/`) and fails otherwise.
- **A null `params` array means one null argument** (`Dm.list(null)`, `pick(null)`, `Dm.Call(x, "f", null)` arrive
  with a null array). Every variadic member treats it as `new object[] { null }` (`Dm.OneNull()`).
- Tests: conformance tests go to `tests/Anomaly.Core.Tests/Dm/` (values: `Dm/Values*.cs`, dynamic: `Dm/Dynamic*.cs`),
  one row per settled behaviour in `docs/ai/specs/dm-conformance.md`, in the package's own section.
- `src/Core/DmShim/AssemblyInfo.cs` makes internals visible to `Anomaly.Core.Tests`; the core internals both packages use
  are `Dm.TryGetNumber`, `Dm.TypeMismatch`, `Dm.KindOf`, `Dm.OneNull`, `DmList.AppendItem`, `DmList.TryGetAssociated`,
  `DmList.IndexOfItem`, `DmList.AcquireSnapshot` / `ReleaseSnapshot`, `DmList._items` / `_count` / `_assoc` / `_isAlist`.

## `src/Core/DmShim/`

One owner per file; every `.cs` file of the folder is listed (the project file stays with the lead). The three tables are kept apart (blank lines and an append
marker each) so that values and dynamic, adding rows for their new files, never touch the same lines.

### Contracts (complete)

| File | Owner | Holds |
|---|---|---|
| `AssemblyInfo.cs` | contracts | `InternalsVisibleTo("Anomaly.Core.Tests")` |
| `Attributes.cs` | contracts | `[DmPath]`, `[DmParams]`, `[ShouldCallParent]`, `[ShouldNotSleep]`, `[SignalHandler]`, `[ShouldNotOverride]`, `[ShouldBePure]`, `[DmProtected]`, `[DmPrivate]`, `[DmFinal]`, `[NotHashed]`, `[DmGlobalProcs]`, `[DmTypeOrder]` (complete) |
| `IDmObject.cs` | contracts | `IDmObject`, `IDmAtom` (object-model.md 1.1) |
| `Dm.cs` | contracts | the declaration and class doc of the static partial class `Dm`; `OneNull()` |
| `Dm.Errors.cs` | contracts | `Dm.CRASH`, `Dm.EXCEPTION`, `Dm.exception`, `Dm.throw_value`, `Dm.NotTranslated` (complete) |
| `DmExceptions.cs` | contracts | `DmRuntimeException` (the base of every DM runtime error the shim raises), `DmCrashException`, `DmThrowException`, `DmNotTranslatedException`, the `/exception` datum `exception` (complete) |
| `Dm.Waiting.cs` | contracts | `sleep`, `spawn` (forwards to the Kernel), `input`, `alert` (stubs completing with null until the UI milestone) (complete) |
| `DmWorld.cs` | contracts | `Dm.world`, the facade class `DmWorld` (`time`, `tick_lag`, `fps`, `tick_usage`, `cpu`, `realtime`, `timeofday`, `maxx`/`maxy`/`maxz`, `log`, `contents`; W3.0: `map_cpu`, `turf`, `area`, `mob`, `name`, `view`, `params`, `system_type`, `sleep_offline`, and the map grid hook call in the `maxx`/`maxy`/`maxz` setters), `DmWorldLog` (complete for rules 10.8; more `/world` vars are added here by contracts or the lead) |
| `DmWorldState.cs` | contracts | the per-world shim state: sinks, `WorldLog`, `NotTranslatedThrows`, `RealtimeStart`, map size; its hash contributor. Partial: other packages add their own parts |
| `Presentation.cs` | contracts | `IMessageSink`, `ISoundSink`, `IAppearanceSink`, `AppearanceAspect`, `NullSinks`, `PresentationSinks`, `RecordingSinks` (complete) |
| `Dm.Presentation.cs` | contracts | `Dm.output_to`, `animate`, `flick`, `sound(...)` (complete); `image(...)` (W4.0, decision D3: the minimal positional and named forms tg's M0 callers use, anything else a runtime error; tests `tests/Anomaly.Core.Tests/Tg/Wave4/Contracts/ImageTests.cs`); `icon` (later: presentation) |
| `sound.cs` | contracts | the `/sound` datum with BYOND's var defaults (complete) |
| `image.cs` | contracts (W4.0) | the minimal `/image` value class (decision D3): BYOND's vars `icon`, `icon_state`, `loc`, `layer`, `plane`, `dir`, `pixel_x`, `pixel_y`, `appearance_flags`, `alpha`, `color`, `invisibility`, `override`; more vars are added with the presentation work |
| `I18n.cs` | contracts | `I18n.T` (complete: returns `Dm.text(template, args)`) |
| `DmWorldState.World.cs` | contracts (W3.0, complete) | the storage of the `/world` vars of `code/world.dm` (`turf`, `area`, `mob`, `name`, `view`, `params`, `system_type`, `sleep_offline`) with their hash contributor, the map grid hook `MapResized`, the file seam `Files`, the `Dm.Deferred` counters |
| `DmFiles.cs` | contracts (W3.0, complete) | the host file seam: `IDmFileSystem`, `DmMemoryFiles` (the per-world default), `DmDirectoryFiles` (a host's real directory), `DmFileRef` (what `file()` returns) |
| `Dm.Files.cs` | contracts (W3.0, complete) | `file`, `fexists`, `fdel`, `fcopy`, `file2text`, `text2file`, `flist` over the file seam |

### Values

| File | Owner | Holds |
|---|---|---|
| `Dm.Constants.cs` | values (written by contracts) | `TRUE`, `FALSE`, directions and the other BYOND stddef constants tg uses and does not define (complete) |
| `Dm.Conversions.cs` | values (core written by contracts) | `TryGetNumber` (numeric normalization of boxed int/double/bool/...), `num`, `num_or_null`, `truthy`, `not`, `eq` (all overloads), `isnum`, `istext`, `islist` (complete); `isfile`, `isicon`; `TypeMismatch`, `KindOf` |
| `DmList.cs` | values (core written by contracts) | `DmList`: construction, `Count`, `len`, the number and value indexers (positional and associative get/set, alist mode), the copy-on-write snapshot; `Add`, `Remove`, `RemoveAll`, `Cut`, `Copy`, `Find`, `Insert`, `Join`, `Swap`, `Splice` (complete: each keeps the invariant that every associated key occurs in the items, as the positional setter and `len` do) |
| `DmEach.cs` | values (core written by contracts) | `DmEach<T>` and its enumerator for `each`, `each<T>`, `each_cast<T>` over lists, atoms and the world, and for `each<T>()`, the loop without `in` over the world registry; `DmPairs<T>`; `DmKeyValue`; the `DmAs` flags (complete) |
| `Dm.Lists.cs` | values (partly written by contracts) | `Dm.list`, `Dm.alist`, `Dm.kv`, `Dm.list_n`, `Dm.each`, `Dm.each<T>`, `Dm.each<T>()`, `Dm.each_cast<T>`, `Dm.@in`, `Dm.in_to`, `Dm.each_as`, `Dm.pairs`, `Dm.pairs<T>` (complete) |
| `Dm.Operators.cs` | values | `equiv`, `lt`/`le`/`gt`/`ge`, `cmp`, `add`/`sub`/`mul`/`div`/`neg`/`mod`/`fmod`/`pow`, `inc`/`dec`/`post*`/`pre*`, `append`/`remove`/`combine`/`mask`/`bitxor_assign` (ref and engine-list overloads), the `*_at` forms, `bitand`/`bitor`/`bitxor`/`bitnot`/`shl`/`shr` |
| `Dm.Math.cs` | values | `round`, `floor`, `ceil`, `trunc`, `fract`, `sign`, `abs`, `sqrt`, `min`, `max`, `clamp`, `log`, `lerp`, trigonometry |
| `Dm.Random.cs` | values | `prob`, `rand`, `pick`, `pick_weighted`, `roll` (world RNG) |
| `Dm.Text.cs` | values | `Dm.str`, `Dm.text` and the text builtins with their `_char` variants |
| `matrix.cs` | values | the `/matrix` class (components a-f) and the `matrix(...)` overloads |
| `DmTextRender.cs` | values | internal: how values render as text (`Dm.str`, `Dm.text` templates and text macros, DM's number format, the `\proper`/`\improper` marker characters, DM paths of types) |
| `DmTextUnits.cs` | values | internal: the UTF-16 / character positions of the text procs and their `_char` variants (DV4) |
| `DmJson.cs` | values | internal: `json_encode`, `json_decode` (System.Text.Json), `params2list`, `list2params` |
| `DmTime.cs` | values | internal: `time2text` without a wall clock (DV3) |
| `DmColor.cs` | values | internal: `rgb`, `rgb2num` (RGB, HSV, HSL, the named HTML colors) |
| `Dm.Directions.cs` | values (written in wave 1b) | `turn(Dir, Angle)` of a direction (complete); the matrix and icon forms are planned with the presentation seam |
<!-- values: append rows for new files above this comment -->



### Dynamic

| File | Owner | Holds |
|---|---|---|
| `Dm.Objects.cs` | dynamic (`named`, `arglist` written by contracts) | `Dm.@new`, `Dm.@new<T>`, `Dm.new_async`, `Dm.Call`, `Dm.arg`, `Dm.arg<T>`, `Dm.forward`, `Dm.args`, `Dm.initial` (both forms), `Dm.get_var`, `Dm.set_var`, `Dm.get_var_or_null`, `Dm.locate<T>`, `Dm.locate_ref`, `Dm.parent_type` |
| `Dm.Types.cs` | dynamic | `istype(x, T)`, `astype(x, T)`, `ispath`, `typesof`, `text2path`, `hascall`, `@ref` |
| `DmTypes.cs` | dynamic | the type registry (`PathOf`, `FromPath`, `ParentOf`, `Subtypes`, `TreeOrdinal` frozen; more may be added) |
| `DmArgs.cs` | dynamic (data holders written by contracts) | `DmNamedArg`, `DmArgList`, `DmArgs` |
| `DmTypeRegistry.cs` | dynamic | `DmTypeRegistry` (one immutable DM type tree: paths, parents, children, tree ordinals, subtree cache) and `DmAssemblyScan` (the assemblies that reference the shim, the `[DmGlobalProcs]` holders) |
| `DmTypeOrder.cs` | dynamic | `IDmTypeOrder`, `DmTypeOrder`: the DM object-tree order and `typesof(/t/proc)` lists, read by reflection from the classes that `[assembly: DmTypeOrder]` names (the generated `Anomaly.Core.Tg.TypeOrder` / `ProcOrder`) |
| `DmBinder.cs` | dynamic | the binder: name resolution (`x__proc`, proc paths, global procs), positional / named / arglist binding, compiled invokers, builtin factories |
| `DmConvert.cs` | dynamic | the conversions of rules 3.5 for `object` values (binder arguments, `Dm.arg<T>`, `Dm.set_var`) |
| `DmPrototypes.cs` | dynamic | the compiled construction factories and the prototypes behind `initial()` |
| `DmVars.cs` | dynamic | cached var accessors behind `get_var`, `set_var`, `get_var_or_null` and `initial(x, "var")` |
| `DmRefs.cs` | dynamic (written in v1.2) | internal: the text form of references behind `@ref` and `Dm.locate_ref`: the ref kinds of lists, alists, typepaths and `world`, the ids `Dm.@new` never gives a datum, and the lookup of a ref by kind (object-model.md section 3) |
| `DmWorldState.Refs.cs` | dynamic (written in v1.2) | the `DmWorldState` part for list refs: the per-world list ref numbers, allocated on a list's first `ref()` and held weakly, and their hash contribution (the next numbers, nothing while unused) |
<!-- dynamic: append rows for new files above this comment -->

## Other files of the contracts package

| File | Owner | Holds |
|---|---|---|
| `src/Core/Kernel/ObjectRegistry.cs` | contracts (Kernel) | `World.Objects`: `ObjectRegistry`, `IStateHashable` |
| `src/Core/Kernel/World.cs` (additions) | contracts (Kernel) | `World.Objects`, `World.GetExtension<T>`, the registry in `ComputeStateHash` |
| `src/Core/Tg/Infrastructure/TgState.cs` | contracts, then the pilot | `TgState` infrastructure part: `Current`, `Of`, `GLOB`, `Master`, `InitStatics` |
| `src/Core/Tg/Infrastructure/GlobalProcs.cs` | contracts, then the pilot | `[assembly: DmGlobalProcs]`, the `GLOB`, `Master` and `usr` accessors and the accessor pattern |
| `src/Core/Tg/Infrastructure/Builtins.cs`, `Defines.cs` | contracts | empty parts (members come from the atom wave and the defines converter) |
| `src/Core/Tg/Infrastructure/TypeOrderSource.cs` | lead (wave 1b) | `[assembly: DmTypeOrder(typeof(TypeOrder), typeof(ProcOrder))]`, the explicit link from the shim's type registry to the generated order |
| `src/Core/Tg/Infrastructure/ForwardDeclarations.cs` | removed (W3.0) | held empty parts of `mob`, `datum_controller_global_vars`, `datum_controller_master` until each class got a translated or skeleton part; W3.0 removed the file with the last one (`datum_controller_master`, now `controllers/master.cs`) |
| `src/Core/Tg/Infrastructure/AssemblyInfo.cs` | contracts (W3.0) | `InternalsVisibleTo("Anomaly.Core.Tests")`: tests reach the internal `TgState` fields and test seams |
| `src/Core/Tg/Infrastructure/TgState.Subsystems.cs` | contracts (W3.0, complete) | the per-world subsystem-type filter (`TgState.SubsystemTypeFilter`, `FilterSubsystemTypes`, lead decision W3 G2) and `GlobalProcs.global_vars__subsystems` (the `global.vars` scan of `Master/New`, G3), both internal, used by the mc package. The other Tg files of wave 3: `docs/ai/specs/wave3-ownership.md` |

## Open items

- `/icon`: object-model.md section 9 makes it a Tg builtin part (tg adds procs to it), so the shim's `icon(...)` cannot
  return it and is typed `object` for now. Same question for tg's procs on `/matrix` (`Shear`, `TurnTo`, ...), whose
  class is the shim's.
- Closed for `/image` (W4.0, decision D3): the class is the shim's (`image.cs`), like `/sound` and `/matrix`, and
  `Dm.image(...)` returns it. A class named `image` in the namespace of translated code (`Anomaly.Core.Tg`) would hide the
  bare builtin `image(...)`: C# finds a type of the current namespace before a `using static` member, so `image(...)`
  would not compile in a translated file. tg's procs on `/image` become C# extension members in Tg when they are
  translated.
- Closed (translation rules v1.2): `/world`. tg's procs on `/world` (`push_usr`, `incrementMaxZ`, the `world.New`
  boot) are C# extension methods of the facade `DmWorld`, `public static` in `GlobalProcs` in the mirror file of their
  `.dm` (translation-rules.md 1.5, object-model.md section 9); the binder (`DmBinder.cs`) resolves a name called on
  `world` against them, passing `world` first (object-model.md section 12).
- Closed (translation rules v1.2): the field hasher. `DmFieldHasher` (object-model.md section 8) stays in the Tg
  assembly (`src/Core/Tg/Builtin/datum.builtin.cs`); it uses only public shim members, so no shim file holds it.
