# The types converter and the generated classes

Status: v1, 2026-10-08. The M3 contracts step (M3.0) wrote the skeleton and sections 4-7; **M3 P1 owns this spec**:
sections 1-3, 8 and 9 describe the tool as it is, and P1 keeps every section true to it. Sections 4 to 9 are contracts
other packages code against (changes go through the lead). Decisions: D95(4), D96, D110, D111, D112; DV25 (section 8). Design
basis: the M3 scout's `scope.md` 2a, 2c ("The inert prop") and 2d; the lead's plan (generated var blocks from M3 on);
`docs/ai/translation-rules.md` 10.2 as amended in M3.0.

## 1. What it is

`tools/Anomaly.Tools.Types` (M3 P1) compiles tg at the snapshot with OpenDream's DM compiler front end. The front end is
source-included from a pinned local clone: `$ANOMALY_OPENDREAM`, or the nearest `OpenDream/` folder above the project (the
clone next to the repository, which the main checkout and its worktrees share), at commit `1362abc5` with RobustToolbox
`af2a7d04` (D110). The tool writes these outputs, all checked in, so that the default build needs neither clone:

- `src/Core/Tg/Generated/Types/<path>.g.cs`: one partial C# class per DM type of tg (46,051 classes at the
  snapshot), holding the var block only (section 3);
- `src/Core/Tg/Generated/LivePartial/<top>.g.cs`: the generated stubs of `live-partial.txt` (section 5);
- `docs/ai/specs/generated/types-index.tsv`: every type and proc of tg with its translation state, which is the tracker
  (section 3.9);
- `src/Core/Tg/Generated/TypeOrder.g.cs`, regenerated from OpenDream's preorder, with each type's parent and class for
  the registry (sections 3.10, 9.4);
- `src/Core/Tg/Generated/Types/TypeConstants.g.cs`: tg's compile-time values for `T::var` reads (section 9.2);
- `docs/ai/specs/generated/live-base.txt` (section 4.1), frozen at the first run;
- `tests/Anomaly.Core.Tests/Tools/Types/handwritten-defaults.tsv` and `handwritten-mismatches.tsv`: the data of the
  var-block conformance test (section 3.8).

Every output starts with a header that names the tool, the tg commit, the OpenDream and RobustToolbox commits and the define
set. A second run is byte-equal: the output is sorted and stable, and nothing in it depends on time or the machine.

**Running.** The tool is not in `AnomalyStation.sln`. It builds on demand and references `Anomaly.Core.Tg`, which it reads by
reflection (section 3.1):

```
dotnet run -c Release --project tools/Anomaly.Tools.Types -- --tg <tg clone> --opendream <OpenDream clone> --out <repo>
    [--check | --summary | --model-only] [--scratch <dir>] [--no-cache]
```

- Without a flag it writes every output and deletes stale `.g.cs` files in its two folders. `--check` compares the outputs
  instead of writing them and exits 1 on a difference. `--summary` prints the tracker's totals from the checked-in index
  and needs no clone. `--model-only` compiles tg and caches the model.
- The spellings `generate`, `check`, `model` and `summary` as the first argument work too.
- **Clone checks.** It refuses an OpenDream, RobustToolbox or tg clone at another commit than the pin.
- **The scratch folder** (`--scratch`, default `$TMPDIR/anomaly-types`) holds a symbolic-link farm of tg plus a copy of
  `tgstation.dme`. OpenDream writes its JSON there, never into tg, and the tool deletes that JSON after the compile. The
  folder also holds the cached model (`model.json`, keyed by the tg and OpenDream commits and the define set) and the review
  report (`types-report.txt`).
- **Time and memory** (the owner's M1 Pro, 2026-10-08): about 50 s and 2.7 GB of RSS for the OpenDream compile, then about
  10 s for the generation. A run from the cached model takes about 10 s. Building the tool takes a few seconds once Tg is
  built. Building RobustToolbox's `Robust.Shared.Maths` writes only into the clone's ignored `bin/` and `obj/` folders.
- **When the generated code does not compile** (a hand-written change clashes with it), the tool cannot build either,
  because it references Tg. Restore `src/Core/Tg/Generated/Types` from git (`git checkout -- src/Core/Tg/Generated`), fix
  the hand-written side, and rerun.

## 2. When it runs

- After a translator adds, removes or changes the body of a translated proc in the Tg tree (the inert marks and the
  tracker follow the code; a skeleton filled in, or a `Dm.NotTranslated` added or removed, changes the index's `body`
  column, which `InertMarkTests` does not check), and after a change of `live-partial.txt`. A package that changes either reruns the tool before its commit, commits the
  regenerated files in their own commit, and reports the inert set's change in its result.
- A regeneration test (`RegenerationTests`, skipped without the clones) runs the tool's `--check` and fails when the
  checked-in outputs differ from a fresh run. `InertMarkTests` fails without the clones when the index is stale: its
  `translated` column must equal what the Tg assembly declares.
- Without the clones, `--check` cannot run; the inert marks can still be checked from the Tg assembly alone (section 4.4).

## 3. The generator's rules

### 3.1 The model (OpenDream as a library)

- **The compile.** The tool compiles OpenDream's `DMCompiler/**/*.cs` (but `Program.cs`) into itself, so that the
  front end's `internal` types are reachable. It compiles the farm's `tgstation.dme` with `DMCompilerSettings` (DM_VERSION
  516, DM_BUILD 1685) and these macro defines: `CBT`, `DISABLE_DREAMLUAU` (tg's production set of rules 1.7, as
  `tools/coloring` uses it) and `CIBUILDING`.
  - Why `CIBUILDING`: OpenDream always defines `OPENDREAM`, and without `CIBUILDING` tg would define `OPENDREAM_REAL`. That
    would drop BYOND-only code: the tracy admin verbs, the debugger hooks, `debug.dm:901`. Under `OPENDREAM`,
    `CIBUILDING` does not define `UNIT_TESTS` (`_compile_options.dm:162`).
  - What `OPENDREAM` still changes: only code inside procs and one global proc (`/proc/count_atoms`, `debug.dm:1030`), plus
    `lootpanel` branches, the rust-g library name and TGS's engine type. No type and no var changes.
- **What it reads.**
  - The object tree: `DMObject` with its parent (`parent_type` applied), `Variables`, `VariableOverrides`,
    `InitializationProcAssignments` and `Procs`.
  - The code tree's var nodes, through a `partial` part of OpenDream's own `DMCodeTree` class compiled into the tool
    (`OpenDream/CodeTreeOrder.cs`; OpenDream's sources are not edited). These nodes give tg's declaration order (`.dme`
    include order, then line order) and the file:line of every var. OpenDream's dictionaries are filled in its multi-pass
    order, so the order comes from the code tree, not from them.
- **Excluded.**
  - The root `/`.
  - OpenDream's 8 artefact types: a `VAR_PRIVATE` block parsed as a type named `var`, as in
    `/datum/component/leash/var` (`leash.dm:16`). They hold no vars and no procs; their vars are correctly on the owning
    type.
  - Every var and proc declared by DMStandard (BYOND's own). The engine parts declare those (rules 3.2, "builtin vars").
- **The result.** 46,051 types, 25,671 tg var declarations, 198,656 overrides, 21,490 init-proc assignments, 64,049 tg
  procs at types and 1,596 global procs. Against M0's regex scan (`tools/coloring`), the tree has the 8 real types the scan
  missed (section 3.10) and the same order for every common type.
- **The translation state** comes from reflection over the Tg assembly that the tool is built against (section 4.1). A
  method that `[DmGeneratedStub]` marks does not count, since `live-partial.txt` decides those (section 5).
- **The IL classification (the index's `body` column).** For each declared method the tool reads its IL, or the IL of
  the state machine's `MoveNext` for an async method. The value is `full` when the IL has no `Dm.NotTranslated` call,
  `partial` when it also calls something else, `skeleton` when `Dm.NotTranslated` is its only call (async and `DmTask`
  plumbing do not count), and `deferred` for a whole-body `Dm.Deferred`. The column informs the tracker; the inert rule
  ignores it.

### 3.2 Hand-written classes: four modes

The tool reads the hand-written parts of `src/Core/Tg` (every `.cs` file but `Generated/Types/**`) as C# syntax with Roslyn.
For each type it notes the class name, its `[DmPath]`, its fields and properties, its methods, and whether it has a
parameterless constructor, a `dm_init_vars` override or a `dm_tg_vars` body. Each type then gets one of four modes:

| Mode | Which types | The generated part |
|---|---|---|
| **Hand** (572) | a hand-written var block: a field or property for a var tg declares at the type, a parameterless constructor, a `dm_init_vars` override or a `dm_tg_vars` body | only the **complement**: fields for the vars tg declares at the type that no hand-written part declares, with their default; a `dm_init_vars` for their list initializers when the hand-written class has none, else a `// GAP(types)` line per initializer. No constructor: the hand-written overrides are checked by the conformance test (3.8) |
| **HandProcs** (340) | a hand-written class with procs and no var block, M2's skeletons included | the full var block (fields, constructor, `dm_init_vars`), without `[DmPath]` when a hand-written part has it (rules 2.3) |
| **Generated** (45,120) | no hand-written part | the whole class, with `[DmPath]`, `[DmGeneratedOnly]` (section 8) and `[DmInert]` when section 4 makes it inert |
| **Skip** (19) | the shim's types (`/list`, `/sound`, `/matrix`, `/exception`, `/icon`, `/image`, `/mutable_appearance`, `/world`: no part), and BYOND types with no engine part and their subtypes (`/savefile`, `/generator`, `/regex` and `/regex/format_component`, `/database` and `/database/query`, `/dm_filter`, `/pixloc`, `/alist`, `/callee`, `/vector`) | none |

- **The complement changes M0-M2 classes.** The vars tg declares at a hand-written type and that the hand-written class
  lacks become fields of it. Example: `/atom`'s vars from modules that are not translated yet. Generated subtypes assign
  them, so they must exist.
  - The complement moves the state hash: the field hasher hashes the new fields, and their defaults are not null.
  - A complement field is never read by hand-written code.
- **Data reaching untranslated leaves.** A HandProcs class whose var block was left for later (an M2 skeleton:
  `/obj/structure/table`, `/obj/machinery/door`, items) now has tg's data. Its creation can reach a leaf that a later
  package translates: `custom_materials`, a light, a colour, an item action. Those types and leaves are listed in
  `tests/Anomaly.Core.Tests/Tools/Types/VarBlockLeaves.cs`. The M2 creation tests expect exactly the listed leaf of each
  listed type, and the owner of the leaf removes its row.

### 3.3 File layout

- **The split.** A subtree of at most 600 types is one file. In a bigger subtree, the root, together with its child
  subtrees of at most 60 types each, goes into the root's own file, and its bigger children are split the same way.
- **Names.** A file is named by its root's path, as in `Types/obj.item.food.g.cs`. The folder is flat: a folder named `obj/`
  would fall under `.gitignore`'s `obj/` rule.
- **Stability.** A new type changes only the file of its subtree: 189 files today, the biggest about 2 MB.
- **Inside a file.** Classes follow DM object-tree order. A class with nothing to say is one line
  (`public partial class x : y { }`).
- **Usings.** Each file imports `Anomaly.Core.DmShim` and `Anomaly.Core.Kernel`. Values are folded, so no `Defines` are needed.

### 3.4 Naming

- **Class names** follow rules 2.3: the path without the first `/`, with `/` replaced by `_`. Hand-written and shim
  classes keep their names.
  - Collisions: when two paths map to one name, the one with fewer segments gets `__2`. At the snapshot that is
    `/obj/projectile/energy/tesla_cannon` and `/datum/action/innate/mimic_lock`.
  - No generated name equals a non-DM class of the Tg assembly.
- **Member names** follow rules 2.2: a C# keyword gets `@`.
- **Clashes.** A var whose name clashes is not declared. The report lists it (3.11) and the collision test checks that no
  generated field shares a name with a method of its class or of an ancestor. A var name clashes when:
  - it is the name of a hand-written member of the class, of an ancestor, or of a hand-written subtype (rules 2.4 asks
    translators to rename the proc);
  - it is a method of the nearest existing class, property accessors included;
  - it is a `System.Object` member, or the class's own name.
  At the snapshot the only clashes are the two `set_color` vars of `/obj/item/circuit_component/holo_signboard` and
  `/obj/item/circuit_component/modpc`, which would hide `atom.color`'s setter.

### 3.5 Field types (rules 3.2, decided once here)

Each declared var's C# type is fixed by the first matching rule:

| Rule | When | C# type |
|---|---|---|
| V1 | `var/list/x`, `var/x[n]`, `var/list/<type>/x`, `var/alist/x` | `DmList` |
| V3 | the default is a typepath; or the default is null, tg assigns a typepath literal to the name somewhere (the V3 grep), and every value tg gives the var in the declaring type's subtree is null or a typepath | `Type` |
| V2 | a declared type (`var/obj/item/x`) with a class | that class |
| V2' | a declared type whose default is of another kind (a number, text, resource, list), or whose class is missing (`/regex`) | `object` |
| V4 | an untyped numeric default | `float`, or `float?` when the null-use grep matches; `object` for the vars of the TimerIdVars table (a stoppable timer's text id after a number default, silicons SI8) |
| V5 | untyped text or a proc reference | `string` |
| V6 | an untyped list default (`list()`, `alist()`, `var/L[a][b]`, `new /list`) | `DmList` |
| V7 | anything else | `object` |

- **The two greps of rules 3.2.** The tool runs them once over `tg/code/**/*.dm`, as name sets, in capture forms that match
  the same lines as the rules' greps. At the snapshot 5,044 names are null-used and 1,313 are typepath-assigned.
  - **Compile-time var reads are not typepaths (M3 I1a).** The V3 capture drops `NAME = /x::y`
    (`grep -vE "\bNAME\s*=\s*(/[A-Za-z_0-9]+)+::"`): `/turf/open/floor/wood::base_icon_state` is that var's value
    (rules 10.3), not a typepath. With those lines the set had 1,448 names, and about 90 vars were `Type` that hold text
    or numbers: the datums' `name`, `icon`, `icon_state` and `color` vars, and `/datum/element/decal.base_icon_state`
    (`deathmatch_mapping.dm:32`), whose element had to keep the icon state in a private field. The rules' grep needs the
    same exclusion (the lead's text).
- **Why V3 checks the subtree.** The V3 grep is by name over the whole tree, so without that condition every
  untyped `name` would be `Type`: somewhere tg writes `name = /...`.
- **Widening: one type per var for every kind it holds (M3 I1a).** After the rule, every kind of value the var can
  hold must fit its C# type. A null into a `float` makes it `float?`; any other misfit makes it `object`, DM's dynamic
  value (rules 3.2, "typed vars that hold wider values"). The kinds come from three deterministic sources:
  1. **tg's tree**: the var's own default, and the overrides and init assignments of the declaring type and its
     subtypes;
  2. **the station maps' var edits** of the type or a subtype: every map file of every map json in `_maps/map_jsons`
     (the maps `config/maps.txt` rotates to), read by the map converter's own reader (`tools/Anomaly.Tools.Maps/DmmFile.cs`,
     linked into the tool), each literal classified by the map loader's `parse_constant` rules (`MapValue.Parse`), so a
     kind here is what the preloader assigns (map-format.md 4.4);
  3. **tg's kind tests** in the var's declaring file: `istext`, `isnum`, `ispath` or `islist` of the bare name or
     `src.name` says that tg's code expects that kind there (`pipe_sorting.dm:42` tests `istext(sortType)`, the map
     editor's `"1;2"` tags).
  The report lists each widening with its source: 69 at the snapshot. 39 come from the tree, for example
  `/obj/structure/window.break_sound` (a text default, resource overrides). 12 come from the maps, for example
  `/obj/machinery/power/solar_control.id` and `/obj/structure/bodycontainer/crematorium.id` (numeric defaults, text
  edits on Meta: the preloader's 5 "expected a number, got text" runtimes before the rule). 18 come from kind tests, for
  example `/obj/structure/disposalpipe/sorting/mail.sortType`.
  - **Hand-written members** keep the translator's C# type. A map edit that one cannot hold is a `map misfit` line of the
    report, for the class's owner: 6 at the snapshot, none on MetaStation (a null `pixel_x`/`pixel_y` on Catwalk, text
    `req_access` on Tram, a numeric `name` on Catwalk).
  - **Not covered:** a declared class var with a typepath default (`var/obj/item/circuitboard/circuit = /obj/...`)
    stays `Type` by V3's order (rules 3.2), although tg stores an instance there at init; such sites keep a
    `GAP(types)` comment (`_machinery.cs`, `light.cs`).
  - Shuttle and template maps join source 2 when the port loads them.
- **Hand-written members** keep their declared C# type, read by reflection. A tg value that does not fit one is not
  generated: the generator writes a `// GAP(types)` line, the report lists it, and section 3.6 has the one exception.

### 3.6 Values (OpenDream's folded constants)

| DM value | C# |
|---|---|
| a number | an integer literal when integral and in int range (rules 3.1), else a `float` literal with `f`; `float.PositiveInfinity` |
| text | a C# string literal. OpenDream stores BYOND's text macros as `0xFFxx` characters (`StringFormatEncoder`); text that holds them is written `Dm.text("\\improper ...")` in DM spelling (rules 5.1, 5.4), with `\` `[` `]` escaped |
| a resource `'icons/x.dmi'` | `Dm.resource("icons/x.dmi")` |
| a typepath | `typeof(<class>)`; `typeof(DmList)` for `/list`. A path without a class is a gap |
| a proc reference (`GLOBAL_PROC_REF`, `/datum/x/proc/y`) | its path as text (rules 3.2, 7.7) |
| `list(...)`, `alist(...)` | `Dm.list(...)` and `Dm.alist(...)`, with `Dm.kv(key, value)` for associations (rules 6.1) |
| `new /x(args)` | `Dm.@new<x>(args)`; `Dm.list()` or `Dm.list_n(n)` for `/list` |
| `var/L[a][b]` | `Dm.@new<DmList>(a, b)` |
| `matrix()`, `file(x)` | `Dm.matrix()`, `Dm.file(x)` |

- **Define names are lost** in the folding (`layer = 2.9f` where tg writes `BELOW_OBJ_LAYER`). This is acceptable in data
  (rules 10.2); proc code keeps symbolic defines.
- **The one exception (3.5).** tg's `NONE` or `FALSE` (0) into a hand-written reference-typed member that the generator
  cannot widen is stored as `null`, with a `// GAP(types)` comment: both are falsy and neither is the default. At the
  snapshot this is only `/datum/status_effect/wound.alert_type = NONE` (`wound_effects.dm:209`), where the hand-written
  `datum_status_effect.alert_type` is `Type`.

### 3.7 Constructors and `dm_init_vars` (rules 10.2)

- **Declared vars.** A constant default is a field initializer.
- **Overrides** are assignments in the parameterless constructor, one per var, in tg's declaration order. A var
  overridden twice in one type keeps its last value at the place of its last override.
- **Init-proc assignments** (`InitializationProcAssignments`) go into `protected internal override void dm_init_vars()`
  after `base.dm_init_vars()`, in declaration order.
  - This covers the list and object initializers and, as OpenDream does, a constant override of a var that an ancestor
    initializes there. Such an override is not repeated in the constructor.
  - A builtin var tg overrides with an object (`transform = matrix()`) goes there too.
- **Non-constant initializers.** There are 126 in all, counting the builtin calls (`matrix()`, `generator()`, `file()`)
  and the lists that hold them; the scout's 32 did not count those calls. Each one is either translated (written as C# by
  3.6) or deferred: `Dm.Deferred("<file:line>", "<type>.<var> = <expr>: initializer not generated (<why>)")` in
  `dm_init_vars`. In both cases the line keeps its `// tg:` comment. Per case:
  - **Translated** (47, the report's "translated init" lines):
    - every `new /x` with constant arguments (`SSassets.transport`, `fax_paper` of the admin fax panel, the admin
      ticket statclicks, the anomaly event's `placer`, the AI's, the cyborg's and the AI brain's actions, `keyslot2` of
      the AI headsets, the janicart upgrades, `air_contents` of the transit tube pod, the musical chairs' particles, the
      syndicate syringe gun's `syringes`);
    - `new /list` (`cam_hotkeys`, `grenades`, `smoke_effects`);
    - the dimensional lists of generated types (`drone_overlays`, `genetic_makeup_buffer`, `stack_list`);
    - every `matrix()` (the mineral, rock, cliff and basalt turfs' `transform`);
    - `file()` (`SSdiscord.notify_file`, and the font and tgui asset lists of `/datum/asset/simple/*`).
  - **In hand-written classes**, which keep their own initializers: `/datum/hud.screen_groups`, the DNA's
    `mutation_index` and `default_mutation_genes`, `/client.selected_target` and `/mob/living/carbon.overlays_standing`.
  - **Deferred**:
    - `/datum/component/sign_language.omissions = new /regex(...)`: `/regex` has no class (Skip);
    - `/datum/controller/subsystem/materials.base_stack_recipes`: a list of `new /datum/stack_recipe(...)` with named
      arguments.
  - **No member** (a `// GAP(types)` line; the report counts 160): every `generator()` of the `/particles` subtypes, whose
    BYOND vars (`position`, `drift`, `spin`, ...) the `particles.builtin.cs` skeleton does not declare yet.
  - **Hand-written initializers** (a `// GAP(types)` line in the complement, 57): the list vars that the hand-written
    `SSjob`, `SSair`, `SSmapping`, `SSspatial_grid`, `SSicon_smooth`, `SSgreyscale`, `/client` and `/mob` do not declare.
    The hand-written class has its own `dm_init_vars`, so the generated complement cannot add one; that class's owner
    initializes them when its code reads them.
- **Builtin types' `dm_tg_vars`.** The builtin engine parts are Hand mode: their `dm_tg_vars` stays hand-written and is
  checked by 3.8.

### 3.8 The var-block conformance test

- **What is checked.** For every Hand-mode class (572), the tool writes one row for each var tg declares at the type that
  the class declares, and for each override tg makes at the type. A row holds the type, the var, where tg sets it
  (`decl`, `override` or `init`), tg's file:line, and OpenDream's value as JSON. That is 3,627 rows in
  `handwritten-defaults.tsv`.
- **The check** (`VarBlockConformance.cs`). The tool and the test share it as source.
  - It builds the class with its constructor (no World) and, for `init` rows, calls `dm_init_vars` inside a fresh
    `World`.
  - It compares by DM value: numbers by value whatever their box, text after the macro mapping, resources by path,
    typepaths through `DmTypes`, and lists item by item.
  - `new`, `call` and `code` values are not compared.
- **The mismatch list.** `handwritten-mismatches.tsv` lists the mismatches found at generation (99 at the snapshot), with
  the actual value. The test fails on a mismatch that is not listed and on a listed one that is fixed; rerun the tool after
  a fix.
- **Each listed mismatch is the lead's to decide** (rules 10.2): either a translation defect, which the file's owner fixes,
  or an OpenDream folding difference.

### 3.9 The index (`types-index.tsv`) and the summary

- **Order.** Header lines start with `#`. The global procs (owner `/`) come first, then one `type` row per type in DM
  object-tree order, each followed by the `proc` rows of the procs and verbs tg defines at it, in file and line order.
- `type	path	parent	file:line	state	class	hand	meta`:
  - `file:line` is the first tg place that declares something on the type, or "(implied by a subtype's path)";
  - `state` is `base` (the live base), `live`, `inert` (section 4.2) or `none` (Skip);
  - `class` is the C# class or `-`;
  - `hand` is 1 when a hand-written part exists;
  - `meta` is 1 for MetaStation's 2,702 types and their ancestors (3,710 types).
- `proc	owner	name	file:line	args	translated	body	kind`:
  - `translated` is `yes` (the class declares the method), `stub` (a `live-partial.txt` row) or `no` (section 4.1);
  - `body` is 3.1's IL classification, or the live-partial kind;
  - `kind` is `proc` or `verb`, plus ` root` for the root definition of the slot; a global proc's kind is `global`.
- **`--summary`** prints, for all types and for the Meta closure: the live (base, by the rule) and inert types; the procs,
  declared (translated, partly translated, deferred, skeletons), stubbed and not declared; the global procs; and the
  hand-written types that are inert.

### 3.10 `TypeOrder.g.cs`

- **What it is.** OpenDream's preorder: children by type id, and type ids follow compile order. It is written in
  `tools/coloring`'s encoding and lists the 46,051 types. Each line also names the type's class when it is not the
  rules-2.3 name, or `-` when the Tg assembly has no class for it. The decoder exposes `Parents` and `Classes` (internal),
  which the registry reads (9.4).
- **Against the regex scan's order.** It adds 8 types: `/datum/status_effect/speech/stutter/anxiety`, `.../obsession`,
  `.../derpspeech`, and `/datum/status_effect/speech/slurring` with `generic`, `drunk`, `cult` and `heretic`
  (`speech_debuffs.dm:104` onward, which the regex scan did not parse). Every common type keeps its position.
- **Why the scout's count was higher.** Its compile used `CIBUILDING` alone, which added 4 lua types (no
  `DISABLE_DREAMLUAU`), and it counted the 8 artefacts.
- **Ownership.** `tools/coloring/color_procs.py` still writes this file; its `--check` reports it as changed. The
  coloring tool should stop writing it (the lead's).

### 3.11 The report

Each run writes `types-report.txt` into the scratch folder: one line per decision the lead reviews. The lines cover the
skipped types, the name collisions, the clashes (3.4), the widenings (3.5), every gap (an override, declaration or init that
is not generated, with the reason), the deferred and translated non-constant initializers, the vars that hand-written code
declares at another type than tg, the conformance totals, the live-base freeze and the hand-written-but-inert types. The
package result quotes it; it is not checked in.

## 4. The inert rule (D112) and `[DmInert]`

### 4.1 Definitions

- **A tg proc of a type**: a proc or verb that tg's source defines at that type (a new proc or an override), as OpenDream
  lists it for the type (`DMObject` procs with a definition at that type, not inherited ones).
- **Declared**: the type's C# class declares (`BindingFlags.DeclaredOnly`, public or not) a method whose DM name is the
  proc's name, by the binder's mapping (rules 2.2 `@`, 2.4 `__proc` stripped; an earlier duplicate `__d0` does not count).
  A declared method counts whatever its body is: a translation, a `Dm.NotTranslated` skeleton, or a generated
  `live-partial` stub (section 5). Declaring a proc is a translator's deliberate choice, which rules 1.4 and
  `docs/ai/specs/not-translated.md` already track; the inert rule only catches what nobody declared.
- **The live base**: the types that were live before this rule existed, so that M0-M2 keep running as they did:
  - the BYOND builtin types with an engine part (`/datum`, `/atom`, `/atom/movable`, `/obj`, `/mob`, `/turf`, `/area`,
    `/client`, `/image`, `/mutable_appearance`, ... every class of `src/Core/Tg/Builtin/` and `src/Core/DmShim/`);
  - every type whose class had a hand-written part at the M3 start (main `68f3a0d`: the 828 classes with a hand-written
    var block and the classes with hand-written procs only).

  P1 writes the list once into `docs/ai/specs/generated/live-base.txt` (one type path per line, sorted) and the generator
  reads it from then on; nobody adds to it. Types translated from M3 on become live by section 4.2 only. (P1: frozen at
  `68f3a0d`'s hand-written classes, 912 types, plus the 8 shim types: 920 rows.)

### 4.2 The rule

A type `T` is **live** when it is in the live base, or when its parent is live and every tg proc of `T` is declared.
Otherwise `T` is **inert**. Equivalently: `T` is inert when some tg proc defined at `T`, or at an ancestor of `T` below `T`'s
nearest live ancestor, is not declared.
The builtin `/regex`, which the shim implements (`DmShim/regex.cs`) and the frozen live base does not list, counts as a live
parent (gaps CI1: wiremod's `/regex/format_component`).

- So a var-only subtype of a live type is live (`/obj/machinery/door/airlock/command` once `/obj/machinery/door/airlock`
  is), and every subtype of an inert type is inert (`/obj/item/crowbar/red` while `/obj/item/crowbar`'s procs are not
  declared): a subtype of an inert type would otherwise run its ancestors' C# where tg runs the untranslated code.
- Overrides do not make an ancestor live: declaring `T/Initialize` does nothing for `T`'s parent.
- A type whose class has a hand-written part from M3 on but is still inert (a package translated some procs and forgot the
  rest) is listed by `--summary` as "hand-written but inert": its translated procs do not run at init. Its package lists the
  missing procs in `live-partial.txt` or translates them.

### 4.3 `[DmInert]`

- `Anomaly.Core.DmShim.DmInertAttribute` (class target, `Inherited = false`, no arguments), declared by P1 in a new file
  `src/Core/DmShim/DmInertAttribute.cs`, written by the generator on the generated part of every inert type and on no other
  class. It is never written by hand and never removed by hand: rerun the tool.
- Each inert class carries its own mark (the attribute is not inherited), so `InertSeam.IsInert(atom)` (section 7) tests
  the runtime type alone: `A.GetType().IsDefined(typeof(DmInertAttribute), inherit: false)`.

### 4.4 Tests (P1)

The mark equals the rule for every type (computed from the Tg assembly by reflection and `types-index.tsv`, so it runs
without the clones); hand-picked cases: an airlock subtype that defines no procs (inert now, live when the airlock is), the
M2 human and its organs (live base), a var-only subtype of a live base type (live), a type with one undeclared proc
(inert).

## 5. `live-partial.txt`: live while partly translated

- **Where:** `docs/ai/specs/live-partial.txt`, read by the generator. One section per package, `## <package> (<milestone>)`, and
  rows under it. **A package owns the rows of its own section** (`docs/ai/specs/m3-ownership.md` lists the sections) and edits
  no other section; the lead moves rows between sections.
- **A row**, tab-separated, `#` starts a comment line:

  ```
  <type path>	<proc name>	<milestone that translates it>	<kind>	<why>
  /obj/machinery/door/airlock	shock	M4a	deferred	shocks and electrification (wires: M4a/M7)
  /obj/machinery/door/airlock	attack_ai	M7	not_translated	AI control
  ```

  `kind` is `deferred` (the stub is `Dm.Deferred`: nothing in the milestone reads its effect, rules 1.4) or `not_translated`
  (the stub is `Dm.NotTranslated`: it throws in tests, logs once in a running world). The proc must be a tg proc of that
  type (section 4.1) that no hand-written part declares; the generator refuses the file otherwise.
- **The stub** the generator writes for a row, in `src/Core/Tg/Generated/LivePartial/**.g.cs` (a part of the type's class):
  the slot's signature from `docs/ai/specs/generated/slots.tsv` (`override` when an ancestor defines the slot, else the root
  declaration; `DmTask` exactly where `suspends = yes`), with the tg file:line comment, the body `Dm.Deferred("<file:line>",
  "<proc>: <why> (<milestone>)")` or `Dm.NotTranslated(...)` and the return rules 1.4 give; a deferred `examine` that overrides returns `base.examine(...)` (tg's examine is `. = ..()` plus its own lines, so the name and description still show; the engineering campaign, D287). (P1: when the nearest
  class that exists already declares the method, its signature is copied from the Tg assembly by reflection, which is the
  same as `slots.tsv`'s unless Appendix A changed it; a row without a slot is refused. A stub carries
  `[DmGeneratedStub]`; the generator reads the rows, not those methods, so removing a row takes effect at once.) A stub counts as declared, so the
  type turns live when every other proc of its segment is translated.
- **Not hand-written.** The hand-written scan (3.2) skips `Generated/LivePartial/` as it skips `Generated/Types/`, so a stub
  never turns its class into a HandProcs class (M3 contracts, phase B: before, a second run differed from the first).
- **not-translated.md:** one row covers every generated stub ("live-partial stubs: `docs/ai/specs/live-partial.txt`"); a stub
  needs no row of its own.

## 6. How a type becomes live

1. Translate its procs in a partial class beside the generated part (rules 10.2 as amended), in the mirror file of each
   `.dm`, with tg's slot signatures.
2. List the procs you leave out in your section of `live-partial.txt`, with their milestone.
3. Rerun the tool: `[DmInert]` goes from the type (and from its var-only subtypes) and the tracker counts the procs.
4. Its `Initialize` now runs at init instead of `InertInitialize` (section 7). Test it like any translated code.

The tracker (`types-index.tsv`) has one row per type (path, parent, file:line, `live`/`inert`/`base`) and one per tg proc
(owner, name, file:line, argument names, `translated`: `yes` for a hand-written declaration, `stub` for a generated one,
`no`). `--summary` prints the totals overall and for Meta's closure (3,710 types). (P1: the full column list, with the
extra `none` state, the `hand` and `meta` columns and the IL `body` column, is section 3.9.)

## 7. `InertInitialize`: what an inert atom runs at init (the inert seam)

`SSatoms.InitAtom` (`game/atom/atoms_initializing_EXPENSIVE.cs`, tg `:2`) calls, where tg calls `A.Initialize(arglist(args))`:

```csharp
result = InertSeam.IsInert(A) ? A.dm_inert_initialize(arguments) : Waiting.NoSleep(Dm.Call(A, nameof(atom.Initialize), Dm.arglist(arguments)));
```

- `InertSeam.IsInert(atom)` (`Tg/Infrastructure/InertSeam.cs`): section 4.3's test on the runtime type. It returns false
  for every atom until M3 P4 fills `dm_inert_initialize` and switches it to the attribute in the same commit (the contracts
  step's phase B left it off: with the throwing bodies every inert atom a test creates would fail).
- `atom.dm_inert_initialize(DmList arguments)` (engine part `Tg/Builtin/atom.inert.builtin.cs`, **M3 P4 implements**): an
  `internal virtual object` of `/atom`, with overrides for `/turf` and `/area` in the same file. `arguments[1]` is `mapload`
  as tg passes it. It never sleeps and returns an initialize hint.
- **What it runs, for an atom that is not a turf or an area:** the presentation part of tg's `/atom/Initialize`
  (`atoms_initializing_EXPENSIVE.dm:115-159`), in tg's order, as its translation in `atom.Initialize` writes it:
  1. the `INITIALIZED_1` check (`stack_trace` on a second init) and `flags_1 |= INITIALIZED_1` (`:118-121`);
  2. `SET_PLANE_IMPLICIT(src, plane)` (`:123`);
  3. `hud_possible`'s `string_assoc_list` (`:125-126`);
  4. `update_greyscale()` when `greyscale_config` and `greyscale_colors` (`:128-129`);
  5. `add_atom_colour(color, FIXED_COLOUR_PRIORITY)` when `color` (`:132-133`);
  6. `update_light()` when `light_system == COMPLEX_LIGHT` and `light_power` and `light_range` (`:135-136`): the light
     state the replicator sends (`docs/ai/specs/light-grid.md`), so inert machines still light;
  7. `icon_state = post_init_icon_state` when set (`:138-139`);
  8. `SETUP_SMOOTHING()` (`:141`; P4 translates it in M3, for both paths);
  9. `atom_integrity = max_integrity` when `uses_integrity` (`:143-144`);
  10. returns `INITIALIZE_HINT_NORMAL`.
- **What it skips:** `initialize_materials(custom_materials)` (`:151-152`, M4a), the `ai_controller` creation (`:154-155`,
  basic mobs stand still until their milestone), `setup_reskins()` (`:157`), `/atom/movable/Initialize`'s lines (the
  faction list, the emissive blocker and the lighting elements, all deferred or presentation the port does not have), and
  every `Initialize` override between the live base and the type (`/obj/Initialize`, `/obj/item/Initialize`,
  `/obj/machinery/Initialize`, ...): none of the type's own code runs, which is the point of D112. No `LateInitialize`, no
  `INITIALIZE_HINT_QDEL`: an inert mapping helper or spawner stays in the world, invisible by its `invisibility` (D115).
- **Turfs and areas:** a turf or area type is inert by the same rule, but the map's structure must still initialize
  (baseturfs, air, smoothing, the area's registration and power flags). So `turf.dm_inert_initialize` runs tg's
  `/turf/Initialize` as translated (`game/turfs/turf.cs`): for a `/turf/open` subtype through tg's `/turf/open/Initialize`
  (`LINDA_turf_tile.cs`, `LINDA_turf_tile.dm:59`), which creates the turf's air and then calls `..()`, tg's own path; for a
  `/turf/open/space` subtype, tg's `/turf/open/space/Initialize` of `space_EXPENSIVE.cs` (which does not call its parent).
  `area.dm_inert_initialize` runs tg's `/area/Initialize` (`game/area/areas.cs`). Each is called non-virtually on the inert
  instance (an engine helper of P4's file: a dynamic method with a `call`), so no untranslated subtype code runs. (M3 P4,
  confirmed by the lead: the open-turf path gives the air this section asks for.)
- **Movement and density stay tg's base behaviour**: an inert atom's other procs dispatch to its translated ancestors as
  any C# virtual does (`CanAllowThrough` by density, `Bumped`, `Destroy`), so an inert dense object blocks its tile, and an
  inert `ON_BORDER_1` object blocks a face only through the motion faces (`LocomotionSeam.FacesOf`), not through tg's own
  `CanAllowThrough` (P4 translates those of the border types that are live in M3).
- **Tests (P4):** an inert obj gets `INITIALIZED_1`, its light and its smoothing, and none of its type's code (a probe
  subtype whose `Initialize` would throw); an inert turf gets its baseturfs and air; an inert area registers; the hint is
  `NORMAL`; a live subtype of the same base runs its own `Initialize`.

## 8. Generated-only types at boot enumerations (DV25)

**The rule (for DV25).** Boot code that creates one object per type of a `typesof` or `subtypesof` list (tg's "one of
every subtype" loops: the subsystems, the language holders, the mutations, ...) skips the generated-only types, which are
the classes no translator has written a part of, until they are translated. Lookups, type caches and `pick()` over such
lists keep every type, as tg does. Each skipping site carries the line
`// GAP(M3 P1 -> <owner>): generated-only types skipped until translated (DV25; types-converter.md 8).`

- **Why.** In tg every type is translated. M0-M2's boot code relied on only declared classes existing; plan Q7's
  human-only species list, for example, is "the declared species types". With all 46,051 classes, a generated-only subtype
  would run its translated ancestors' code with its own tg data. Two examples: a timer subsystem subtype runs `SStimer`'s
  `New`, and with it `Recover`; the lizard language holder reaches `grant_partial_language`. That is the problem D112
  avoids for map atoms, and before this rule it failed about 1,800 core tests.
- **The mark.** `[DmGeneratedOnly]` (`Anomaly.Core.Tg.DmGeneratedOnlyAttribute`, not inherited) is on the generated part of
  every Mode-Generated class (3.2) and on no other class. It is separate from `[DmInert]`: a generated-only type can be live
  (a var-only subtype of a live type), and a hand-written type can be inert (4.2).
- **The helper.** `GlobalProcs.typesof_written(list)` is the list without the generated-only types, in its order, as a new
  list; `GlobalProcs.subtypesof_written(T)` is `typesof_written(subtypesof(T))`. Both are in
  `src/Core/Tg/Generated/Types/GeneratedOnly.cs` (P1).
- **The wrapped sites.** These are the boot loops that create objects and broke with every type in them:

| Site | File | Creates | Owner |
|---|---|---|---|
| `TgState.FilterSubsystemTypes`, the default filter of `Master/New` (`master.dm:110`) | `src/Core/Tg/Infrastructure/TgState.Subsystems.cs` | one subsystem per type | lead |
| `init_language_holder_prototypes` (`_language_holder.dm:343`) | `src/Core/Tg/modules/language/_language_holder.cs` | one holder per type; unwrapped in gaps E2c (DV25): every holder is data and live, as tg creates them | lead |
| `SSatoms.setupGenetics` (`atoms.dm:184`) | `src/Core/Tg/controllers/subsystem/atoms.cs` | one mutation per type | lead |
| `init_blood_types` (`_globalvars/lists/mobs.dm:72`) | `src/Core/Tg/_globalvars/lists/mobs.cs` | one blood type per type | lead |
| `init_species_prototypes` (`_globalvars/lists/mobs.dm:89`) | `src/Core/Tg/_globalvars/lists/mobs.cs` | one species per type | lead |
| `build_chemical_reactions_lists` (`_globalvars/lists/reagents.dm:99`, gaps F1) | `src/Core/Tg/_globalvars/lists/reagents.cs` | one reaction per type; since 2026-11-03 (the owner) it skips the inert reactions (section 4) instead of the generated-only ones: a var-only reaction under a live parent reacts as tg, an inert one would react with tg's numbers and none of its own procs | lead |

- **The same rule in the tests** that create "every translated type", with the same comment:
  - the create_and_destroy port (`tests/Anomaly.Core.Tests/Tg/UnitTests/create_and_destroy.cs`);
  - `Wave4/Integration/CreateAndDestroyTests.cs` and `Wave4/Mob/MobLifecycleTests.cs`, through the port's type list;
  - `Wave4/TurfArea/EveryTurfAndAreaTypeTests.cs`.
- **Not wrapped** (lookups, data, `pick()`): `init_species_list`, `init_all_languages`, `init_language_types_by_name`,
  the gas types, the organ slots, the random jumpsuits (F1), the log categories, `get_abstract_types` and every type cache.
  More data-only subtypes there are tg's.
- **How a site loses its wrap.** Its owner translates or defers the leaves that its types reach, removes the wrap, reruns
  the suites, and the generated-only types join as in tg.
- **Loops that work with every type stay unwrapped.** The configuration loader (`configuration.dm:242`) creates every
  generated-only config entry type, and `init_language_prototypes` (`languages.dm:13`) every language, as tg does; their
  tests pass.
- **The guard.** `GeneratedTypeTests.TheWrappedBootLoopsCreateNoGeneratedOnlyObject` fails when a booted test room holds a
  generated-only object of a wrapped loop's root type.

## 9. Compile-time reads and the registry at 46k classes (lead-authorised, M3 P1 perf)

**Why.** The first run with all classes in booted the default world in about 20 s, against 0.7 s before. Two costs did it:
- **Class loading.** Touching every class costs about 2.5 s: the runtime loads 46k types, whether through
  `Assembly.GetTypes()` in the registry or through `typesof(/datum)`.
- **Prototype factories.** `Dm.initial` on a `Type` built the type's prototype, with an `Expression.Compile` factory per
  type: 18-21 s and about 300 MB for all of them.

tg's `get_abstract_types` reads `sometype::abstract_type` for every `/datum` subtype at each boot, through
`valid_subtypesof`, so every boot paid both. Four changes fix it.

### 9.1 Prototype and instance factories

`DmPrototypes` (DmShim) creates instances with `Activator.CreateInstance`, so no per-type compile is paid. The prototypes
of all 46k types then take 0.4 s (about 178 MB, which is why 9.2 avoids building them).

### 9.2 The compile-time table (`Generated/Types/TypeConstants.g.cs`)

BYOND's `T::var` is a compile-time read, and the port answers it from a generated table where it can.
- **What the table holds.**
  - The vars: every var that translated code reads with `Dm.initial(x, (C p) => p.var)`. The tool scans the
    hand-written sources for these reads; 67 vars at the snapshot.
  - The values: what each generated part gives the var, which is a field's initializer (the C# default when it has none)
    or a constructor assignment.
  - Which classes: those whose var block is all generated (Mode Generated and HandProcs, 3.2).
  - Stops: the classes with a hand-written var block, the builtin engine types and the shim's types.
  - At the snapshot: 64,367 values, 2.4 MB of source. It is part of `TypeOrder`, decoded once on first use and immutable.
- **The read.** For a `Type` value, the shim's `Dm.initial(T, read)`:
  - finds the member the read lambda loads, from its IL (`ldarg; ldfld F` or `call get_P; ret`), once per lambda;
  - asks `TypeOrder.Constant(path, var)`, which walks the path up to the first type that sets the var (the table's value)
    or to the first stop. At a stop, that class's prototype answers: the generated parts in between do not set the
    var, so the value is the one `T`'s prototype would give, and `T`'s prototype is not built.
- **What keeps the prototype path.** A var the table does not cover, a read on an instance (`initial(x.var)`), a lambda
  that is not a plain member read, and the untyped `Dm.initial(x, "var")`.
- **Equivalence.** The table holds exactly what the generated C# assigns, so it equals the prototype.
  `TypeConstantsTests` checks this on a sample of types against freshly built objects.
- **After adding a read of a new var**, the read takes the prototype path (correct, slower) until the tool is rerun.

### 9.3 `get_abstract_types`

tg's loop runs over `subtypesof(/datum)`; the port's loop runs over `abstract_type_candidates()`. That is
`src/Core/Tg/Generated/Types/GeneratedOnly.cs`, which takes from the table, in tree order:
- the types whose generated part sets `abstract_type` to their own path or to a subtype's;
- every stop class.

tg's own test (`sometype == sometype::abstract_type`) then runs on each candidate. The result equals tg's: a type that
sets nothing inherits its parent's value, which is never its own path. The test assemblies' fixture types are not
candidates. The site is `__HELPERS/abstract_types.cs` (owner: lead), under a
`GAP(M3 P1 -> lead): lead-authorised (M3 P1 perf)` comment.

### 9.4 The registry from the generated table

- **The table.** `TypeOrder` carries each path's parent index and class name: a tab and the name in its encoded line
  when the name is not rules 2.3's, or a tab and `-` when the Tg assembly has no class for it.
- **How DmTypes uses it.** `DmTypes` registers the Tg assembly's classes from that table, by path, without loading them. A
  class is loaded on first use: `FromPath`, `Subtypes`, `ParentOf`, `TypeAtOrdinal`, or a `PathOf` on an object, which
  reads the class's own `[DmPath]` once and caches it.
- **Other assemblies** (the shim's builtin types, test fixtures) are still found by reflection.
- **Cost.** Building the registry takes about 0.16 s. `typesof(/datum)` still loads every class (about 2.5 s), and no boot
  code calls it any more.

### 9.5 Numbers

Measured on the owner's M1 Pro, Release, while other packages were building on the machine. The boot probe boots the
default world (48 x 48 rooms) in a fresh process.

| Step | Registry | Boot | Managed heap | Process |
|---|---|---|---|---|
| before P1 (918 classes) | 0.10 s | 0.66-0.72 s | +29.5 MB | 0.82-0.88 s |
| 46k classes, no fix | 2.4-3.0 s | 19.9-21.3 s | +331 MB | 22.8-24.5 s |
| + 9.1 (Activator) | 2.4-3.2 s | (prototypes 0.4 s) | (+178 MB of prototypes) | - |
| + 9.2, 9.3, 9.4 | 0.16 s | 0.93-1.09 s | +53.5 MB | 1.17-1.31 s |

Per world, after the process's first boot (16 x 16 classic room): boot 11.4 ms before and 17.8 ms after; state hash about
70 ms before and 94 ms after; 654 objects before and 840 after. The extra objects are the generated config entries and
languages that tg's loops create.

### 9.6 The rules and specs to amend (the lead's)

- `translation-rules.md` 10.3, the paragraph under the table ("`Dm.initial` reads the cached prototype of the value's
  runtime type ..."): add "for a `Type` value whose var tg's compile-time table covers, it reads the table
  (`docs/ai/specs/types-converter.md` 9.2) and builds no prototype".
- `object-model.md` 5 ("Prototypes"), and 9 ("Built once per process by reflection over the assemblies"): the Tg
  assembly's classes are registered from the generated table and loaded on use.

## 10. The `UndeclaredOverrides` table (M4a; the rule by the M4a contracts step, the emitter by M4a P1)

**Why.** The interaction guard of D132 (`docs/ai/specs/interaction.md` section 7) must know, for an object about to run a
chain proc, whether tg's own definition of that proc at the object's type (or an ancestor below the C# class that would
run) is missing from the port. The inert mark (section 4) is not enough: a live or base type can still lack an override
(the PDA and the headset are base types), and an inert type's class answers only "something is missing", not what.

**The rule.**
- **The names:** exactly `Anomaly.Core.Tg.InertSeam.GuardedProcs` (the DM names; the generator reads the list from the Tg
  assembly by reflection, so the two never differ).
- **A row** is one tg definition of a guarded name (a new proc or an override, section 4.1's "tg proc of a type") at a type
  `T` whose C# class does not declare it (section 4.1's "declared", by the binder's mapping: `@`, `__proc`; an earlier
  duplicate `__d0` does not count; a generated live-partial stub counts as declared, so a `live-partial.txt` row is how a
  package says "deliberately a stub" and the guard then lets the stub run, which throws or defers as its row says).
- **Scope:** every type under `/atom` (46k classes are not all atoms; datums never meet the chain's guard).
- **The output:** `src/Core/Tg/Generated/Types/UndeclaredOverrides.g.cs`, an `internal static partial class
  UndeclaredOverrides` with one sorted, stable table of `(type path, DM proc name)` rows (path order of `TypeOrder`, then the
  name), the header of section 1, and one lookup member the guard calls, `bool Has(Type type, string proc)`, answered from a
  frozen set built once on first use (no per-call allocation; no mutable static state, `NoStaticStateTests`).
- **The guard's walk** (`InertSeam.HasUndeclaredOverride`, P1): from the object's runtime class up to (not including) the
  class that declares the C# method the call would run (the most derived declaration of the slot's method on the chain),
  any class whose DM path has a row for the name makes the answer true.
- **Regeneration:** like every output (section 2): the table changes when a translator declares or removes one of the
  guarded procs; `--check` compares it; `RegenerationTests` cover it.

**Tests (P1):** a second run is byte-equal; hand-picked rows (an inert toolbox subtype's `attack_self` override, a base
type's undeclared `equipped` override, a declared live type has no row); the guard's walk on fixture classes (an override
below the declaring class is found, one above it is not).

**The emitter (M4a P1).** `Generator.Overrides.cs`: the scope is `/atom` and every type below it (`/area`, `/turf`, `/obj`,
`/mob` are its children); for each type with a class, each tg definition of a guarded name whose state is "not declared"
(section 4.1) and that has no `live-partial.txt` row gives one row, once per (type, name). The file holds the rows as
`(typeof(class), "proc")` with the type path and tg's location in a comment, in `TypeOrder`'s path order and then by name,
and builds a `FrozenSet<(Type, string)>` in the type initializer; `Has(Type, string)` and `Count` read it. At the M4a P1
regeneration: 2,614 rows over 1,534 atom types. The report (`types-report.txt`) carries the row and type counts. The
clone-free test `NoLivePartialStubHidesAnInheritedRoot` (tests/Anomaly.Core.Tests/Tg/M4aClick/P1) and the regeneration test
cover it besides `--check`.

**Live-partial stubs under a new root (M4a P1, section 5).** The live-partial files start with
`#pragma warning disable CS0114`: a stub generated as its slot's root (`public virtual`) no longer stops the Tg build when a
translator later declares the root above it; the build, and with it the tool, keeps working, and the next run regenerates
the stub as an `override`. A stale stub hides the root until then; `NoLivePartialStubHidesAnInheritedRoot` fails on one,
without the clones. `-p:TreatWarningsAsErrors=false` is no longer needed for this case.

