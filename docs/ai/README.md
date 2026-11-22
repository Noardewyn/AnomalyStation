# docs/ai: documents for AI agents

Most of Anomaly Station so far has been ported with AI coding agents. Along the way they wrote, and kept reading back, a
large body of documents: the owner's decision log, the rules for translating tg's DM code to C#, design specs for every
system, the register of deviations from tg, port checklists, research notes, work logs and model-work reports.

These documents are written for an agent, not for a person. They are long (several are hundreds of kilobytes), dense
with decision numbers (`D86`, `DV503`), milestone names and cross-references, and they record every detail an agent needs
to keep the port consistent from one session to the next. A person trying to learn the project from them would struggle.

So they live here, apart from the documentation for people:

- **For people:** the [README](../../README.md), [CONTRIBUTING.md](../../CONTRIBUTING.md) and the guides in [docs/](..)
  (building, running, testing, hosting, mapping, the roadmap).
- **For agents:** this folder. Agents find it through their own instruction files ([CLAUDE.md](../../CLAUDE.md)), and the
  code's comments point here when they cite a decision or a spec. The human documentation does not link here.

Nothing here is required reading for a contributor. Read it if you want to know why something was done a certain way, or
if you work with an agent and want to check what it was told.

## What is here

| Path | What it is |
|---|---|
| [DECISIONS.md](DECISIONS.md) | The owner's decision log (`D1`, `D2`, ...): the source of truth agents follow |
| [translation-rules.md](translation-rules.md) | The binding conventions for translating tg's DM code to C# |
| [DEVIATIONS.md](DEVIATIONS.md) | The register of deliberate deviations from tg's logic (`DV1`, `DV2`, ...) |
| [specs/](specs/) | Design specs of the systems (the waiting model, the protocol, the client, the map editor, ...) and generated indexes the tools and tests read |
| [PORT-CHECKLIST.md](PORT-CHECKLIST.md) | How much of tg's code is ported, recounted by `tools/checklist/refresh-port-checklist.py` |
| [PLAN.md](PLAN.md), [WORKLOG.md](WORKLOG.md) | The early plan and architecture; the log of finished work |
| [required-models.md](required-models.md), [model-work-progress.md](model-work-progress.md), [tram-model-inventory.md](tram-model-inventory.md), [art/](art/) | The 3D model backlog, model work reports, audits and previews |
| [research/](research/) | Research done before the port: SS3D, SS14, BYOND, Godot, 3D design, licensing |
| `C*-checklist.md`, `C0-report.md`, `handoff-*`, `bugfix-batch-*` | Checklists, hand-offs and reports of finished stages |

Tools and tests read some files here (`specs/generated/`, `specs/not-translated.md`, `specs/live-partial.txt`,
`translation-rules.md`, `specs/waiting-model.md`, `PORT-CHECKLIST.md`): move or rename them only together with their
readers.
