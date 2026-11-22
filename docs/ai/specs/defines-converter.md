# The defines converter

Status: v1, 2026-10-06. Owner: the defines package (`tools/Anomaly.Tools.Defines`). It implements translation-rules.md 1.5 ("every `#define` constant: the generated `public static partial class Defines`"), 1.6 (the name-clash test), 1.7 (conditional compilation), 3.1 (number typing), 4.4 (24-bit bit operations), 11.2 (postfix units), on the design of `docs/ai/specs/m0-reading/defines.md` (survey, classification, traps). Where that report and the rules differ, the rules win; the differences are listed in section 9.

## 1. Running it

```
dotnet run --project tools/Anomaly.Tools.Defines -- --out src/Core/Tg/Generated/Defines [--tg <clone>] [--check] [--verbose]
```

- The tgstation clone is `--tg`, else `$ANOMALY_TGSTATION`, else `../tgstation` next to the repository (the directory holding `AnomalyStation.sln`), else, in a git worktree, `../tgstation` next to the main checkout (read from `.git` and `commondir`, no git process).
- It runs in about half a second, writes only the files whose text changed and deletes generated files (`*.g.cs`, the two manifests) it no longer produces. Output is byte-identical from run to run (ordinal file order, preprocessing order inside a file, `\n` line ends, UTF-8 without BOM; C# string literals and the JSON manifests escape everything outside printable ASCII, tg's comments are copied as they are).
- `--check` writes nothing and exits with 1 when the output directory differs from what would be generated. The test `DefinesManifestTests.TheCommittedOutput_IsUpToDate` does the same check in `dotnet test`.
- `--verbose` lists the warnings (an `#undef` of a name that is not a macro, bit operands outside 24 bits, lexer oddities; none at the snapshot). Errors (a shift by 24 or more bits: DM loses every bit, BYOND ref "<< shift operator", so `(1<<24)` is 0, which in a tg flag would be a bug; tg never does it) stop the tool before it writes.
- The classes are a library: `DefinesConverter.Generate(tgRoot)` and `DefinesConverter.ConvertText(dm)` return the classification, the C# files and the manifests; the tests call them directly.

## 2. Scope

The preprocessor runs `tgstation.dme` from its first line through the last `code/__DEFINES` include: the `.dme`'s own `#define FILE_DIR .` and `#define DEBUG`, `__odlint.dm`, `_maps/_basemap.dm` (map includes skipped), `code/__byond_version_compat.dm`, `code/_compile_options.dm`, `code/_experiments.dm`, `code/world.dm`, then the 411 `code/__DEFINES` files in `.dme` order: 419 files and 16,496 `#define` lines at the snapshot (16,456 of them in `code/__DEFINES`, the count of the survey). Every one is in the manifest.

Not in scope: the 3,882 `#define`s elsewhere in `code/` (`__HELPERS`, `modules`, `game`, ...). Most are file-local (`#undef` at the end of their file); the global ones (`LAZYADD`, ...) are function-like macros that translators write as `GlobalProcs` members or expand inline (translation-rules.md 11.1). They can be added by extending the scope to the whole `.dme` once a translator needs one; the converter already handles file-local definitions.

## 3. Configuration

The production build (translation-rules.md 1.7), held in `PreprocessorConfig`:

| Symbol | Value | Source |
|---|---|---|
| `DM_VERSION` | 516 | `dependencies.sh` `BYOND_MAJOR=516` |
| `DM_BUILD` | 1685 | `dependencies.sh` `BYOND_MINOR=1685` (`__byond_version_compat.dm` rejects < 1659 and 1660) |
| `CBT` | defined | tg's build passes it to every compile (`tools/build/build.ts:229`); selects the compile-time side of `MAP_SWITCH`, `WHEN_MAP`, `WHEN_COMPILE` (`map_switch.dm`) |
| `DISABLE_DREAMLUAU` | defined | translation-rules.md 1.7 (the Lua admin scripting is not ported) |
| `DEBUG`, `FILE_DIR` | as in the `.dme` | read from `tgstation.dme` |
| `code/_compile_options.dm` | as shipped | read from the file (`TESTING`, `UNIT_TESTS`, `REFERENCE_TRACKING`, ... stay undefined) |
| `OPENDREAM`, `SPACEMAN_DMM`, `CIBUILDING`, `TGS` | undefined | a BYOND production build |

BYOND's own `stddef.dm` constants (`TRUE`, `NORTH`, `TOPDOWN_LAYER`, `SEE_MOBS`, ...) are predefined macros so tg's defines fold over them (`ALL_CARDINALS (NORTH|SOUTH|EAST|WEST)` is 15), and so are its function-like `ASSERT`, `EXCEPTION`, `REGEX_QUOTE` and `REGEX_QUOTE_REPLACEMENT` (tg's `#undef ASSERT`, `assert.dm:1`, ends the builtin one before tg defines its own). Values and bodies come from OpenDream's `DMCompiler/DMStandard/Defines.dm` (`ByondBuiltins.cs`). They are never emitted: they belong to the shim (translation-rules.md 1.6, Appendix C.1).

## 4. The preprocessor

- **Lexing** follows OpenDream's `DMPreprocessorLexer`: block comments nest, and a `//` inside a block comment hides the rest of that line; text literals (`"..."`, document strings `{"..."}`, the three raw forms, resource literals `'...'`) are single tokens whose embedded `[expressions]` are lexed as tokens, so comment markers inside text are text (`byond://` links in `vv.dm` and `admin.dm` survive); numbers are lexed greedily (`0x1F`, `1e-3`, `1.#INF`); `#keyword`, `#x` and `##x` are their own tokens. Comments are kept so their text can become doc comments.
- **Directives** are recognised at the start of a logical line (backslash line splices join lines). `#if`/`#elif` evaluate with `defined(X)`, macro expansion, DM operators, and unknown names as 0. `#error`/`#warn` in active code are reported, never fatal. `#include` resolves against the including file's directory.
- **Inactive branches**: every `#define` in a skipped branch is recorded with the directive that skipped it (`#ifdef UNIT_TESTS`, `#else of #ifdef CBT`, ...): 68 at the snapshot. A name defined in both branches of a conditional is emitted once, from the active branch.
- **Lifetimes**: each definition lives from its directive to its `#undef` or redefinition. DM expands a macro where it is used, so a define is evaluated with the definitions live at the end of the include order (it may name defines that come later); a file-local define (one that is `#undef`ed) is evaluated with the definitions live at its `#undef`. A use after an `#undef` sees no macro, as in DM. A name that is defined, `#undef`ed and defined again is emitted once (the definition live at the end, else the last); the others are `superseded`.
- **Expansion** is textual, as in DM: an object-like macro is replaced by its body and rescanned; a function-like macro is replaced only when `(` follows, its arguments are substituted as written (also inside embedded text expressions) and rescanned; `#p` makes text of an argument, `##p` pastes it onto the preceding identifier, the last `p...` parameter takes the remaining arguments and an empty `##p` removes the comma before it (BYOND ref "#define directive"). A macro is not expanded inside its own expansion (C's rule; only a guarantee of termination).
- **Doc comments**: the dmdoc comments (`///`, `/** */`) on the lines directly above a define, plus any comment on its own line, with the markers and block gutters stripped. A blank line, a code line or a plain `//` line between them breaks the association.

## 5. Classification and typing

Each define is classified by what its body expands to, never by its name or file. Counts at the snapshot (16,496 seen, 15,266 emitted):

| Kind | Count | Emitted as | Example |
|---|---|---|---|
| `int` | 5,065 | `public const int` | `INITIALIZED_1 (1<<5)` = 32 |
| `float` | 407 | `public const float` | `LIGHTING_ROUND_VALUE (1 / 64)` = `0.015625f` |
| `string` | 9,610 | `public const string` | `COMSIG_MOVABLE_MOVED "movable_moved"`, `SMOOTH_GROUP_TURF_OPEN S_TURF(0)` = `"0,"` |
| `unit` | 32 (31 emitted) | the multiplier | `SECONDS *10` = 10; `TICKS *world.tick_lag` is not a constant |
| `function` | 1,119 (153 emitted) | `GlobalProcs` methods for the trivially safe `span_*` | `span_notice(str)`; `SEND_SIGNAL`, `ADD_TRAIT`, `QDEL_NULL` are listed |
| `list` | 86 | not emitted | `COMMON_ACCESS list(...)` (a new list at every use, translation-rules.md 6.1) |
| `runtime` | 47 | not emitted | `TICK_USAGE world.tick_usage`, `SSMOBS_DT (SSmobs.wait/10)`, `CALLBACK new /datum/callback` |
| `statement` | 17 | not emitted | `RESOLVE_ACTIVE_MOVEMENT`, `VAR_FINAL var`, `MAGAZINE_TYPE_*` |
| `resource` | 14 | not emitted | `'icons/x.dmi'` |
| `empty` | 9 | not emitted | `AI_VOX`, `SIGNAL_HANDLER` (expands to nothing in production) |
| `typepath`, `type_var` | 8, 3 | not emitted | `RESERVED_TURF_TYPE /turf/open/space/basic`, `GAS_META /datum/gas_mixture::gas_meta` (need the type tree) |
| `null` | 5 | not emitted | `UNAFFECTED null` (write `null`) |
| `unresolved` | 3 | not emitted | `RUSTG_CALL call_ext` (names something that is not a macro) |
| `comma_splice` | 2 | not emitted | `ID_ICON_BORDERS 1, 9, 32, 24` |
| `poison` | 1 | not emitted | `TURF_LAYER 2 #error ...` |
| `inactive` | 68 | not emitted | in a skipped branch |

Other kinds the classifier can give: `fragment` (not an expression), `error` (DM would raise: division by zero, `"text" + 1`), `text_macro` (a `\the`, `\improper`, ... in the text), `text_format` (an empty `[]`), `location` (`__FILE__`, `__LINE__`), `superseded`.

**Number typing.** A folded number is a `const int` when it is an integer within DM's exact range, |v| <= 2^24 (translation-rules.md 3.1: integer literals and bit flags are `int`), and a `const float` otherwise. So `(128 / 2)` is `int` 64, `(1 / 64)` is `float`, `7.000` is `int` 7 (seven such dotted literals at the snapshot), and a whole value beyond 2^24 is a `float` because DM does not hold it exactly (`YEARS` 315360000f, `GIGA` 1E+09f, `INFINITY` 1E+31f). Floats are written as the shortest literal that round-trips (`2.35f`), text as a regular C# literal with `\uXXXX` outside printable ASCII.

**Folding** (`ConstFolder`) uses DM's run-time semantics, one binary32 rounding per operation (C# `float` arithmetic): `/` is real division; `%` truncates both operands to integers and keeps the dividend's sign; `%%` is `B * fract(A / B)`; `**` is right-associative; `&`, `|`, `^`, `~`, `<<`, `>>` work on 24 bits (`~0` is 16777215; a shift by 24 or more is a hard error); comparisons give 1 or 0; `&&` and `||` return an operand; null is 0 in arithmetic and nothing in text; text `+` text concatenates, text `+` number is a type mismatch (not emitted); `rgb(r, g, b)` with constant 0-255 arguments gives `"#rrggbb"` in lower case (BYOND ref "rgb proc"). Text escapes follow the BYOND ref ("macros (text)"): `\n`, `\t`, `\"`, `\\`, `\[`, `\]` give the character, `\<`/`\>` give `&lt;`/`&gt;`, `\xNN`/`\uNNNN` the character with that code; a letter run after `\` that is not a text macro starts with the `n` or `t` escape (tg's songs write `\nC4`). A number embedded in text is printed like DM: six significant digits, `%g` style (`"[1000000]"` is `1e+06`). Parsing uses DM precedence (OpenDream's `DMParser`; shifts bind more loosely than comparisons).

**Postfix units** (translation-rules.md 11.2). A body that expands to a leading `*` (or `/` and a number) is a unit; its constant is the multiplier, `1 <body>` folded: `SECONDS` 10, `MINUTES` 600, `HOURS` 36000, `DAYS` 864000, `YEARS` 315360000f, `DECISECONDS` 1, `MILLISECONDS` 0.01f, `KILO` 1000, `MEGA` 1000000, `GIGA` and up `float`, `NEWTONS` 1, `EVENT_SECONDS` 0.5f. A define that uses a unit is folded with the textual expansion: `(0.25 SECONDS)` is 2.5f, and `(80 / 8 SECONDS)` would be 100, not 1. `TICKS` (`*world.tick_lag`) is a run-time value and is not emitted; translation-rules.md 11.2 rewrites `<operand> TICKS` as its expansion, `<operand> * world.tick_lag`, through the shim's `world` facade. One consequence of emitting a chained unit as one constant: tg's `x MINUTES` is `x *10*60`, two binary32 roundings, while `x * MINUTES` is one; for a non-integer `x` the two can differ in the last bit (never for integers below 2^24 / 600).

**The `span_*` methods.** A `span_*` macro whose body is text literals and parameters joined by `+`, each parameter used once, becomes `public static string span_x(string p) => "..." + p + "...";` in `GlobalProcs` (153 of the 157 in `span.dm`, `span_tooltip` and `span_tooltip_subtle` included). C# string `+` is DM's text `+` for text and null arguments; a number argument does not compile, where DM raises a type mismatch. `span_class` and the three `*_ml` helpers put a parameter inside `[...]`, which formats any value like DM text; they stay in the manifest for hand translation.

**Notes for translators** (doc `<remarks>` and the manifest's `notes`): `unparenthesised body` (71 numeric defines such as `ZAP_DEFAULT_FLAGS ZAP_MOB_STUN | ZAP_MOB_DAMAGE | ZAP_OBJ_DAMAGE` or `TOPDOWN_WATER_LEVEL_LAYER 100 + TOPDOWN_LAYER`: the constant is the folded body, but tg's textual expansion binds differently next to a stronger operator, so each use site must be checked, translation-rules.md 4.11), `file-local in tg: #undef at ...` (`ACCURACY`), `tg typo` (`MESSAGE_TYPE_ENTERTAINMENT = "entertainment"`: emitted as its text as the survey recommends; it is unused at the snapshot and unusable as a value in DM), `postfix unit`, and `Indented under COMSIG_X in tg` / `Indented under it in tg (...)` (353 defines that tg indents under a signal: its return values, such as `COMPONENT_MOVABLE_BLOCK_PRE_MOVE (1<<0)`, `NONE` or an alias, and for some signals the indexes of its argument list, such as `HEARING_RAW_MESSAGE 3` under `COMSIG_MOVABLE_HEAR` or `SPEECH_SPANS 3` under `COMSIG_MOB_SAY`; the define's own doc tells which).

## 6. Output

`src/Core/Tg/Generated/Defines/`, one C# file per tg source file with emitted members, mirroring its path without `code/` (translation-rules.md 1.1): `code/__DEFINES/dcs/signals/signals_atom/signals_atom_movable.dm` gives `__DEFINES/dcs/signals/signals_atom/signals_atom_movable.g.cs`. Each file is a part of `public static partial class Defines` (namespace `Anomaly.Core.Tg`) and, for `span.dm`, of `GlobalProcs`. Names are verbatim (a C# keyword would get `@`; none occurs). Every member carries its DM location and body and the tg doc:

```csharp
    // tg: code/__DEFINES/dcs/signals/signals_atom/signals_atom_movable.dm:8  #define COMSIG_MOVABLE_PRE_MOVE "movable_pre_move"
    /// <summary>
    /// from base of atom/movable/Moved(): (/atom)
    /// </summary>
    /// <remarks>
    /// Indented under it in tg (return values or argument indexes): COMPONENT_MOVABLE_BLOCK_PRE_MOVE.
    /// </remarks>
    public const string COMSIG_MOVABLE_PRE_MOVE = "movable_pre_move";
```

**`defines.manifest.json`** accounts for every `#define` the preprocessor saw: a header (snapshot, configuration, files, counts by kind) and one entry per line, in preprocessing order, with `name`, `file`, `line`, `args` (function-like; the variadic one with `...`), `body` (as written, with each run of blanks, comments and line splices between tokens collapsed to one space; a text literal is kept as written inside, so one continued over a line break keeps its `\`, line break and indentation, as in `VALIDATE_OVERLAY_LIMIT`), `kind`, `status` (`emitted` or `not_emitted`), for emitted ones `cs` (`const int`, `const float`, `const string` or the `GlobalProcs` signature) and `value`, otherwise `reason`, and when they apply `condition` (the directive that skipped it), `end` (`#undef at ...` or `redefined at ...`), `indented_under` (the signal tg indents the define under), `notes` and `features`. `features` describe a function-like macro for translation-rules.md 11.1. `variadic`, `stringify` (`#p`) and `paste` (`##p`) describe its own definition; `location` (`__FILE__`, `__LINE__`, `__PROC__`, `__TYPE__`), `src`, `usr`, `dot` (the caller's `.`, as in `VV_DROPDOWN_OPTION`'s `. += ...`), `return` and `statement` (a `;` or a statement keyword) describe what it expands to where it is used: its body and, as DM rescans the expansion, the bodies of the macros it names (an object-like one, or a function-like one followed by `(`, live at the end of the include order), so `QDEL_IN` has `location` through `addtimer` (`subsystems.dm:218`). A parameter is a placeholder, not a name: `COORD(src)` has no `src`. Statement translators consult the manifest to decide, for an identifier, between a constant, a macro to expand and a plain name.

**`signals.manifest.json`** lists the `COMSIG_*` defines that are signals (text value, 1,474) or signal name factories (function-like, 26), with `value`, location, the full dmdoc `doc`, the `source` and `args` parsed from its `...: (args)` line when there is one, and `indented`, the defines tg indents under it (return values and argument indexes; the survey counted them all as return flags). Signals are keyed by name: distinct signals that share one text value (`COMSIG_PROJECTILE_PREHIT` and `COMSIG_PROJECTILE_SELF_PREHIT`, six pairs) stay distinct, and numeric `COMSIG_*` defines (return flags, aliases) are not signals.

The generated project compiles in about 1 s with the compiler server warm and 4 s cold (`Anomaly.Core.Tg` with its two dependencies, `--no-incremental`, at 15,113 constants and 153 methods).

## 7. Tests

`tests/Anomaly.Core.Tests/Tools/`: the 25 tricky examples of the survey and its honourable mentions (`DefinesTrickyExampleTests`), the numeric and text traps (`DefinesNumericTrapTests`), preprocessor cases (`DefinesPreprocessorTests`), 367 hand-picked constants checked by reflection against values worked out from the DM source (`DefinesValueTests`), manifest completeness against a line scan of the converted files, determinism, the up-to-date check and the signals manifest (`DefinesManifestTests`), and the translation-rules.md 1.6 rule that `Dm` and `Builtins` define no name tg defines (`ShimNameClashTests`; it looks the classes up by name, and since wave 1 both exist). Tests that need the clone are skipped when it is not found.

## 8. Changing it

Regenerate after any change to the tool or to the clone and commit the output with the change. A new kind of body that the classifier does not recognise lands in the manifest as `fragment` or `unresolved` with its reason, never silently dropped. When the scope grows past `code/__DEFINES`, file-local defines of different files may share a name; the emission rule (one constant per name, the one live at the end) will then need a per-file decision.

## 9. Differences from the survey and open points

- Typing is by the folded value within 2^24 (section 5), where the survey sketched C#-like static typing (`any /` gives `float`). The rules (3.1) and the task ask for integers and flags as `int` and fractional values as `float`.
- Names are flat constants of `Defines`; the survey's `ReturnFlags` members and `SignalInfo` registry became doc remarks and the signals manifest. The survey took every define indented under a signal for a return flag; about 50 of the 353 are indexes into the signal's arguments or into a list it passes (`HEARING_*`, `SPEECH_*`, `ARG_PRAYER_*`, `ELEVATION_*`, ...), so the converter only records that a define is indented under a signal.
- Lists, typepaths, `::` type vars, resources and run-time expressions are listed, not emitted as properties (survey 6.6, 6.7): they need `DmList`, the type tree and the `world` facade, which are other packages. `TICKS` likewise.
- `CBT` is defined (tg's build defines it), as translation-rules.md 1.7 now says; `tools/coloring` uses the same configuration (`DM_BUILD` 1685 and `CBT`; its output did not change when it was aligned).
- A define that is used only inside its own file but names a file-local macro (`#undef`ed later) would be `unresolved` at the end of the include order; none exists at the snapshot (the two `#undef`s are `ACCURACY`, used inside a proc, and `ASSERT`).
