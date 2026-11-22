# M0 reading pass

> **Precedence.** `docs/ai/translation-rules.md` and the specs in `docs/ai/specs/*.md` (for example `waiting-model.md`, `move-chain.md`) win over these reports wherever they disagree. The C# sketches and proposed C# shapes in the reports are **non-binding** and predate the rules: class and member names, PascalCase or camelCase spellings, properties instead of fields, `double` time, the placement of helpers (`Dm.qdel`, `Dm.SendSignal`, `World.New`, `DMList`, `DM.*` and so on) and the namespaces they mention are all superseded by the rules and by the wave-0 decisions (shim namespace `Anomaly.Core.DmShim` with the static class `Dm` for true BYOND builtins only, tg global procs in `Anomaly.Core.Tg.GlobalProcs`, `Dm.@new` as the object factory, global state in per-World containers, `float` time). Use the reports for tg facts: the order of events, file:line references, hazards and scope advice.

Reports written before the first translated file, from reading the tg base files in full (snapshot `1d99bd7f`): what each area needs from a C# shim, the exact order of events, hazards and the scope recommended for M0. They are reference material for translators; `docs/ai/translation-rules.md` holds the resulting rules. The reports were corrected after the wave-0 review: DM behaviour that could be settled from the BYOND reference (https://www.byond.com/docs/ref/) or the OpenDream sources is quoted in place, and the remaining open items are still marked "verify".

| File | Area |
|---|---|
| datum-signals-components.md | datum, signals, callbacks, weakrefs, components, elements, traits, qdel, GLOB |
| atom.md | atom base, initialization flow (SSatoms), appearance machinery |
| [../move-chain.md](../move-chain.md) | atom/movable, the Move chain and its exact order of signals (`movable.md` here is only a pointer to it) |
| turf-area-obj-mob.md | turf, area, obj, mob bases, area contents bookkeeping, mob vs client |
| controllers.md | Master Controller, subsystems, timer, processing, the deterministic executor spec |
| timed-actions-helpers.md | do_after, stoplag, waiting primitives, helper inventory, DM semantic traps |
| defines.md | `__DEFINES` survey and the defines converter design |

The scripts behind the counts in these reports were prototypes in a session scratch folder and were not kept. The general DM scanners of the research phase live in `docs/ai/research/10-scripts/`; the defines converter (`tools/Anomaly.Tools.Defines`) and the colouring tool (`tools/coloring`, output `docs/ai/specs/generated/colored-slots.tsv` and `docs/ai/specs/colored-procs.md`) supersede the prototypes for the numbers they produce.
