# Checkpoint C0: end of M0

Status: report for the owner's decision, 2026-10-07. Checkpoint C0 (ROADMAP section 8) has three tasks:
- record the first measured speed V;
- recompute the time estimate of ROADMAP section 6;
- present the technical choices M0 made for confirmation (D94).

The owner decides whether to continue to M1 or to adjust (D29: no silent cuts). The record of the work is `docs/ai/WORKLOG.md`.

## 1. M0 against its "Done when"

| Clause (ROADMAP M0) | State | Evidence |
|---|---|---|
| `dotnet build && dotnet test` is green | done: 3,776 tests, Debug and Release, 0 warnings, none skipped | the whole suite |
| A headless world with turfs and atoms moves an atom through the translated `Move` chain, with signals in tg's order | done | `tests/Anomaly.Core.Tests/M0Acceptance/Clause1MoveChainTests` (booted world), move-chain.md goldens T1-T12 in `Tg/Wave4/Integration`, tg's own `movement_order_sanity` test ported |
| A scripted timed action completes through a continuation, is cancelled by a movement signal, and the toy world runner shows it | done | `M0Acceptance/Clause2TimedActionTests`; the runner `tools/Anomaly.Tools.ToyWorld` (below) |
| The same scenario run twice gives an identical state hash | done | `M0Acceptance/Clause3DeterminismTests` (hashes equal at 21 sampled ticks; another seed, a missing command or an extra step change it); `ToyWorld --twice` |
| The first measured V is recorded | done | section 2 |

The rest of M0's scope is also done:
- the solution layout, version pinning and the Godot client skeleton;
- the DM shim;
- signals, the subsystem executor (tg's Master Controller itself, translated), timers and traits;
- the waiting model;
- the translation base: `datum`, `atom`, `atom/movable` with the full `Move` chain, `area`, `turf`, `obj`, `mob`, components and elements, `do_after`;
- the defines converter;
- `docs/ai/translation-rules.md`;
- the toy world runner and the velocity script.

The boot also goes further than M0 asked: the real tg `world/New`, Genesis, GLOB and the configuration with all 231 entries, the Master initializing every subsystem, and the ticker subsystems firing ("boot to first fire").

**The toy world runner.** `dotnet run --project tools/Anomaly.Tools.ToyWorld` runs tg's code in a room of 5x5 floor tiles inside walls: one mob and one obj under the real Master. Its output (seed 1) ends:

```
  t35 command step SOUTH -> FALSE
  t35 bump wall(4,1) from (4,2)
  t37 command do_after #1 of 1 second on the obj
  t57 do_after_result #1 -> TRUE (started t37)
  t61 command do_after #2 of 1 second on the obj
  t66 command step EAST -> TRUE
  t67 do_after_ended #2
  t67 do_after_result #2 -> FALSE (started t61)
do_after #1 (completes): TRUE on t57, 20 ticks after it started
do_after #2 (cancelled by a step): FALSE on t67, 6 ticks after it started
state hash 0x990262cc003e5804 at t71
```

The output also shows an ASCII frame of the room after each command. `--twice` runs the scenario in two worlds of one seed and compares the hashes.

## 2. Measured speed

`tools/measure_velocity.py`, from the first commit (2026-10-05 18:41) to HEAD (2026-10-07). This is about 1.6 calendar days of work, with two stops by the subscription's usage limit (2 h 36 min in wave 3, 77 min in wave 4).

| | Lines (net) | Planned for M0 (ROADMAP 6) |
|---|---|---|
| Translated tg code | 38,201 C# lines = 31,215 DM lines (C#/DM ratio 1.22; 17,141 DM code lines are in the translated mirrors at HEAD) | 18,000 DM |
| New logic (kernel, shim, engine parts, tools) | 22,617 | 16,000 |
| Client | 148 | 1,000 |
| Tests | 66,651 | 14,000 |
| **Weighted total** (DM + new logic + 2 x client + tests) | **120,779** | 50,000 |

- **V.** The weighted 120,779 lines all fall in one calendar week, so the measured **V is at least 120,000 weighted lines per week**: ten times the central assumption (12,000) and five times the optimistic one (25,000). The sustained pace is not measured yet: a full week has not passed, and the usage limit would stop a continuous run several times. Section 3 therefore also uses half of it.
- **Owner time.** About 2.5 hours in M0 (owner's estimate: less than 3 h of decisions and reading reports). Recorded in `docs/ai/estimate/owner-hours.csv`.
- **Test volume.** Tests came out three times larger than planned: 1.24 test lines per code line, where the plan assumed about 0.4. The independent test-writers wrote most of them.
  - In waves 4 and 5 they added about 1,000 behaviour tests and found no translation defect.
  - Their own mutation checks (about 100 one-line mutants of the translations) were all killed or shown equivalent.
  - The pilot found 1 defect per about 700 tests, wave 3 1 per about 320.

## 3. The estimate, recomputed

`docs/ai/estimate/roadmap_estimate.py` now has two measured scenarios next to the three original ones:
- V from section 2;
- the owner's choice at C0 to read about 5% of non-test lines (spot checks);
- test volumes tripled.

The fixed owner hours per milestone (playtests, decisions, art checks), the owner's weekly hours H and the rework loss stay central: none of them is measured yet, and M0 had no playtests.

| Scenario | First demo (M0-M6) | Core version (M0-M10) | Owner hours in total |
|---|---|---|---|
| Pessimistic (ROADMAP draft) | 23.2 months | 32.3 months | 978 |
| Central (ROADMAP draft) | 8.6 months | 11.8 months | 559 |
| Optimistic (ROADMAP draft) | 3.9 months | 5.1 months | 302 |
| **Measured** (V 120k, 5% review) | **4.1 months** | **6.1 months** | **308** |
| Measured, half V (60k) | 4.1 months | 6.1 months | 308 |

**What this means.** At the measured speed, every milestone is limited by the owner's time, not the AI's. Halving V changes nothing. The calendar is set by:
- how many hours a week the owner gives;
- the fixed hours each milestone needs for playtests, decisions and art checks;
- how much code the owner reads.

One change at a time (measured scenario, core version):

| Change | Core version | Owner hours |
|---|---|---|
| as is | 6.1 months | 308 |
| fixed hours per milestone 10 instead of 20 | 3.9 months | 196 |
| fixed hours per milestone 35 | 9.5 months | 477 |
| owner 10 h a week instead of 15 (H 8.5) | 9.2 months | 308 |
| owner reads no code (0%) | 4.5 months | 225 |
| owner reads 20% (the draft's central value) | 11.1 months | 559 |
| V only 30k a week | 7.0 months | 308 |

**Caveats.**
- M0 was translation and tests. From M1 on, the client (Godot), art and maps add visual check loops, which the estimate weights double. The AI side may slow there, but stays far from the limit unless V drops below about 15k a week.
- The usage limit of the subscription is the real AI-side constraint. It is not in the model.

## 4. Choices to confirm

These are the choices the assistant made under D94; translation proceeds on them. Details are in `docs/ai/translation-rules.md` 0.3 and `docs/ai/specs/waiting-model.md` section 14. The recommendation for each is to confirm it as it is.

1. **Waiting model.** `async`/`await` on the port's own `DmTask`, with the "colouring" of which procs can sleep computed once for the whole tg tree (about 8,000 slots). M0 shows it working: `do_after`'s sleep loop, the Master's tick and a sleeping `Entered` all run as continuations, deterministically. Rejected alternative: OS-thread coroutines (D31 forbids threads in gameplay).
2. **Runtime errors (DV1).** A runtime error unwinds to an isolation boundary (a subsystem's fire, the scheduler, a detached call) instead of ending only the failing proc.
3. **Object references.** Direct C# references plus world ids, instead of D45's handles.
4. **Type defaults.** Generated C# (constructors and a pre-New hook), not YAML or JSON prototypes. Wave 4 added the per-type order of BYOND's own defaults and tg's (rules 10.2, "Builtin types").
5. **Suspended procs** are not serializable; snapshots and rollback treat them specially.
6. **Localization.** `I18n.T(template, args)` on player-visible text; the client localizes.
7. **Text length** counts UTF-16 code units (DV4).
8. **Delay rounding.** A positive `sleep`/`spawn` delay is `max(1, floor(ds / 0.5))` ticks.
9. **`GLOB` before the controller exists** lives in a per-world storage object.
10. **Contents edits.** An edit of an atom's `contents` through list operators takes effect at the next engine access.

Deviations from tg behaviour so far (`docs/ai/DEVIATIONS.md`, each with its reason): DV1-DV13.
- DV1: runtime unwinding.
- DV2: `QDELETED` at world time 0.
- DV3: tick usage is virtual and 0.
- DV4: text length in UTF-16 units.
- DV5: a sleep inside a synchronous context.
- DV6: fixed 20 fps.
- DV7: no hard delete.
- DV8: a tg bug in component lists, fixed.
- DV9: GLOB's `vars` self-check.
- DV10: no Failsafe.
- DV11: `rand_seed` does nothing.
- DV12: no emissive blockers.
- DV13: no server-drawn busy indicator.

None changes gameplay that M0-M6 shows.

## 5. Open questions for the owner (from `docs/ai/WORKLOG.md`)

1. **OpenDream's compiler as the DM front end.** Clone and build it locally as a tool, never shipped, for exact type trees, var defaults and the call graph. Recommended: yes, at the start of M3 at the latest; earlier if the regex scans start costing time.
2. **A Roslyn analyzer for the waiting model.** It flags a `DmTask` that is neither awaited nor handed on. Recommended: add it early in M1, because a missed `await` is a silent ordering bug.
3. **BYOND oracle.** About 20 small DM behaviours are settled from the reference and OpenDream, but no real BYOND run confirmed them; they are listed in `docs/ai/WORKLOG.md`. The port follows the reference meanwhile. A one-time BYOND 516 run, by the owner or on any machine with BYOND, would settle them; it is not needed for M1.

## 6. Next

M1, the basic server (D87 item 1), per ROADMAP section 3, in the strict milestone order the owner chose.
