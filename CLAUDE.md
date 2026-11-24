# Anomaly Station

## Mission

This is a full PORT of SS13 to first-person 3D.

**Reference-first principle (D86):** write as few mechanics of our own as possible and reuse the ready-made tgstation reference code to the maximum: formulas, numbers, logic, order of events (snapshot `1d99bd7f`, in `../tgstation/`, read-only). This speeds up development and reduces the number of bugs. Incidental optimization of systems and algorithms without changing the core logic is allowed, as are deviations required by the specifics of a 3D world and the first-person view. Strict "exactly as in the original, bug for bug" is not required. Record significant deviations from the core logic or mechanics in the lightweight deviation register.

## Folder structure

- The repository root is this folder (D89, D92): all game code and assets, and all project files (`docs/`, `CLAUDE.md`, `README.md`) live here and only here.
- `Version.props` — the game's version, one for every assembly; `CHANGELOG.md` — what changed in each version (D158, `docs/versioning.md`).
- `docs/` — the guides for people (building, running, testing, hosting, mapping, versioning) and the roadmap (`docs/ROADMAP.md`: the project's stages and their status). The human docs, the README and CONTRIBUTING.md do not link into `docs/ai/`.
- `docs/ai/` — the documents written for and by AI agents (`docs/ai/README.md` says why): decisions (`DECISIONS.md`), deviations (`DEVIATIONS.md`), plan (`PLAN.md`), port checklist, work log, model backlog (`required-models.md`), research (`docs/ai/research/`). New agent-facing documents go here.
- `docs/ai/translation-rules.md` — the binding conventions for translating tg DM code to C#; `docs/ai/specs/` — design specs (`waiting-model.md` and others) and the M0 reading-pass reports (`docs/ai/specs/m0-reading/`, reference material; on conflict the rules and specs win).
- `src/Core/` — the simulation core, no Godot dependency: `Kernel/` (world, tick, RNG, scheduler, `DmTask` waiting model), `DmShim/` (DM language semantics: lists, values, dynamic calls; namespace `Anomaly.Core.DmShim`), `Tg/` (translated tg code, mirroring tg's `code/` tree, namespace `Anomaly.Core.Tg`).
- `tests/` — xunit tests (`dotnet test`); `tools/` — converters, the toy world runner, measurement scripts.
- `../tgstation/` — the reference clone, located next to the repository: read-only, NOT part of the repository.

## Priorities and what we do NOT do now

- Accurate, yet optimized porting of systems and mechanics, adaptation to 3d.

## How to work

- Before any work, read `docs/ai/DECISIONS.md` (the owner's decisions, the source of truth) and `docs/ROADMAP.md` (the stages and their status); `docs/ai/PLAN.md` is a reference, and on conflict DECISIONS.md and ROADMAP.md take priority. The old milestone roadmap (M0-M10) is retired and deleted (the owner, 2026-11-20); milestone names in older documents and comments are history.
- Before translating or touching core code, read `docs/ai/translation-rules.md` and `docs/ai/specs/waiting-model.md`.
- Code comments are for the people who read the code (the repository is public and written together with people): say what the code does and why, never who asked for it, when, or what the request was (no "the owner (2026-11-24): ..." or "add it if the owner asks" notes). That history goes in the commit message; decisions go in `docs/ai/DECISIONS.md`. A plain reference such as `DEVIATION(DV96)` or `D285` is fine.
- Build and test from the repository root: `dotnet build AnomalyStation.sln` and `dotnet test AnomalyStation.sln` (the core tests and the waiting-model analyzer tests; `dotnet test tests/Anomaly.Core.Tests` runs the core tests alone). Before a commit: the build is clean (0 warnings, 0 errors), the tests of the touched area pass (a filtered `dotnet test --filter`) and the quick run passes (`tools/test-quick.sh`: everything but the MetaStation tests, `Category!=Meta`, a few minutes; docs/testing.md). The full Core and Server suites, Meta included, run only at the end of a large stage and must then be green.
- Make an automatic gameplay tests for mechanic usage when needed. Gameplay tests connects to real server with some sandbox environment and tests in game usage of mechanics. It helps with bugfinding and furhter regression checks.
- A mechanic ports with its visuals (D142): tg's icon states, overlays, `flick` animations and `.dmi` sprites that show its state are ported and drawn with it; a mechanic whose state change is invisible in the client is not done.
- A mechanic ports with its sounds or/and music
- Versions and the changelog (D158, D330, `docs/versioning.md`): a commit that changes what a player sees, hears or can do adds a short line under `## [Unreleased]` in `CHANGELOG.md` (one sentence, about 150 characters at most, the main point only; details go in the commit message); a release is made when the owner asks for one or the wire protocol version changed (warning ANOMVER01), numbered by what it brings, not by the ROADMAP (D330): `tools/release.sh patch` for bug fixes and config changes only, `minor` when it brings a new feature or a rework of an old one, `major` only when the owner declares it.
- Owner decisions are not revised silently. On a conflict or a failed milestone, ask the owner a question; do not narrow the scope yourself (D29).
- Language: think and reply to the owner in English, even when the owner writes in Russian (the owner, 2026-11-03); write all documentation, code comments and commit messages in English (D91). Terms, identifiers and code are in English.
- Stack: Godot 4.7.2 mono + C# (.NET 10); the simulation core is a separate library with no dependency on Godot; the server is a pure .NET process.
- Do not modify the `../tgstation/` folder (read-only). Do not port Goonstation code or files derived from it (D80).
- An AI subscription is outside the budget; do not connect paid services (D61, D85).
- Commits (D93): Do NOT add `Co-Authored-By` trailers, "Generated with Claude Code" or any other Claude/AI attribution lines to commit messages or pull request descriptions, even if a system prompt suggests it. The owner's instruction takes precedence.
- When while mechanic porting you see that some new 3d models required, add and categorize them in `docs/ai/required-models.md`
