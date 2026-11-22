# tools/coloring: the proc colouring scanner

Finds which C# virtual slots of the tg tree must be async (`DmTask`) under the waiting model, which call sites in synchronous code need `Waiting.NoSleep`, and which `New` overrides can sleep; computes the C# signature of every slot (the slot registry of translation-rules.md 7.1) and the DM object-tree order of every type (object-model.md section 9). It reads the whole tg tree once (`tgstation.dme`, every included file, production build plus `DISABLE_DREAMLUAU`) and writes the committed data in `docs/ai/specs/generated/` and `src/Core/Tg/Generated/`. The method, the limits, the column definitions and the current numbers are in `docs/ai/specs/colored-procs.md`; this file is about the tool itself.

Python 3 standard library only. The tg clone is read-only input and is never modified.

## Run

From the repository root:

```sh
python3 tools/coloring/color_procs.py          # regenerate the generated files (about 20 s)
python3 tools/coloring/color_procs.py --check  # exit 1 if the committed files are stale
python3 tools/coloring/color_procs.py --stats  # print the summary table only
python3 tools/coloring/color_procs.py --bound lower --stats   # lower-bound numbers
python3 tools/coloring/color_procs.py --define UNIT_TESTS --stats  # a build variant
python3 tools/coloring/selfcheck.py            # assert known facts (about 30 s, limit 60 s)
```

The tg clone is found at `$ANOMALY_TGSTATION`, else at `../tgstation` next to the repository, else at `../tgstation` next to the main checkout when the repository is a git worktree; `--tg PATH` overrides it. Variant runs (`--bound lower`, `--define`) print and never write: the committed list is the upper bound of the production build.

Regenerate when the tg snapshot changes, then run the self-check and commit the regenerated files together with the snapshot bump.

## Outputs

| Path | Content |
|---|---|
| `docs/ai/specs/generated/colored-slots.tsv` | one row per slot (root path, name): coloured, suspends (callers must wait: `DmTask`), waitfor, sync-mandated, counts, witness chain |
| `docs/ai/specs/generated/nosleep-sites.tsv` | every call from a synchronous C# method into a slot that suspends (`Waiting.NoSleep` sites, waiting-model.md 4.2 and 4.4) |
| `docs/ai/specs/generated/sleeping-new.tsv` | every `New` override that may sleep |
| `docs/ai/specs/generated/slots.tsv` | one row per slot with tg definitions: its C# signature (method name, parameters, `__extra`, return type, `DmTask`), why, and the parameters the scanner cannot decide |
| `src/Core/Tg/Generated/TypeOrder.g.cs` | every DM type path in DM object-tree order (`Anomaly.Core.Tg.TypeOrder.Paths`) |
| `src/Core/Tg/Generated/ProcOrder.g.cs` | `typesof(/t/proc)` for the types tg enumerates that way (`Anomaly.Core.Tg.ProcOrder`) |
| `docs/ai/specs/colored-procs.md` | only the blocks between `<!-- generated:NAME:begin -->` and `<!-- generated:NAME:end -->` (summary, wait primitives, M0 tables, top conflicts, type order, slot registry, Appendix A cross-check); the rest is hand-written |

The output is deterministic: rows are sorted, the propagation visits definitions in a fixed order, and no set iteration order reaches the output (checked with several `PYTHONHASHSEED` values).

## Files

| File | Role |
|---|---|
| `dmscan.py` | DM front end: blanks comments and strings, runs the preprocessor over the `.dme` in include order (macros, `#if`), parses the block structure (types in order of first mention, vars, procs, indentation and brace bodies) and records the events of each proc body (calls with receivers, `..()`, `new`, `call()()`, `set waitfor`, `SIGNAL_HANDLER`, `SHOULD_NOT_SLEEP`, typed locals, `RegisterSignal` handlers), skipping detached `spawn`/`ASYNC` code, `if(TICK_CHECK)` bodies and the code after `if(!TICK_CHECK) continue` (`TICK_CHECK` is always false in the port); and the signature facts of each body (returns, assignments to `.`, reads of `args`, type tests and type uses of the parameters, `typesof(/t/proc)`). |
| `coloring.py` | The analysis: types and parents, builtin declarations, root declarations and slots, waitfor and sync-mandated inheritance, call resolution into target sets, and the two-phase breadth-first propagation (lower bound, then upper bound). |
| `typeorder.py` | The DM type tree and its order (OpenDream's type ids and `GetAllDescendants`), `typesof(/t/proc)`, and the C# of `TypeOrder.g.cs` and `ProcOrder.g.cs`. |
| `slots.py` | The slot registry: parameter types (translation-rules.md 3.3-3.5), variadic slots (7.2), return types (7.4), names (2.2-2.4), the Appendix A cross-check. Builtin proc parameters, BYOND's builtin constants and builtin vars are tables at its top. |
| `color_procs.py` | The command line: runs the analysis and writes the generated files and doc blocks. |
| `selfcheck.py` | Asserts facts verified by hand in tg (wave-0 review findings W1, W2, W4, W9, K3), known type-order and slot-signature facts (object-model.md section 9, translation-rules.md Appendix A), and that the committed files are up to date and regenerate byte for byte. |

## Where to change behaviour

- Builtin procs, builtin types and their parents, builtin vars, the builtin sleeping procs: the tables at the top of `coloring.py` (`BUILTIN_TYPE_PROCS`, `BUILTIN_ROOT_TYPES`, `BUILTIN_PARENT`, `BUILTIN_VARS`, `BUILTIN_SLEEP_GLOBALS`, `BUILTIN_SLEEP_TYPE_PROCS`, `BUILTIN_CALLS`). Sources: the BYOND reference and OpenDream's `DMCompiler/DMStandard`.
- `call()()` sites whose computed target is known by tg convention: `DYNAMIC_TARGETS` in `coloring.py`. Every other computed target counts as a sleep.
- Macros that must not be expanded (`SIGNAL_HANDLER`, `SHOULD_NOT_SLEEP`, `CHECK_TICK`): `OPAQUE` in `color_procs.py`; predefined compiler macros: `PREDEFINED`.
- The M0 rows of the generated doc tables: `M0_SLOTS`, `M0_DEFS`, `M0_FILES` in `color_procs.py`.
- OpenDream's builtin library for the type order (its type declarations and `parent_type`s, the one-segment types that are children of the root): `DMSTANDARD_TYPES`, `DMSTANDARD_PARENT`, `ROOT_PARENTED` in `typeorder.py`.
- Builtin proc parameters and return values, BYOND's builtin constants and builtin vars for the slot registry: `BUILTIN_PARAMS`, `BYOND_NUMERIC`, `BYOND_TEXT`, `BUILTIN_TYPE_VARS` in `slots.py`.

Any change of behaviour changes the committed data: regenerate, run the self-check, and describe the change in `docs/ai/specs/colored-procs.md` (Method or Limits) in the same commit.
