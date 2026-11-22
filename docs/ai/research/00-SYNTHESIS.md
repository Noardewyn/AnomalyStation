# 00. Research Synthesis: Plan for Porting Space Station 13 to 3D

Date: 2026-10-05. Author: architect-analyst. Inputs: reports `01`–`10` (including the "Verification" sections) and the decision log `docs/ai/DECISIONS.md` (D1–D22). Baseline: tgstation, HEAD `1d99bd7f` from 2026-10-04, shallow clone, read-only. Stack (D1): Godot 4.7.2 mono + C# on .NET 10, with the simulation core as a separate library with no dependency on Godot.

How to read this document.
- References like [06 F2] mean report 06, section F2. References like D4 mean a decision from `DECISIONS.md`.
- Claims that received an unsupported or unverifiable verdict in verification are not included in the conclusions. Their list and the reasons are in Appendix A.
- The (estimate) marker means a number that is an assumption of the report's author, not a measurement. The (inference) marker means my own inference by analogy, not a fact from a source. Confidence levels are inherited from the reports.
- I ran four checks myself in the tg clone to resolve discrepancies between the reports (section 8): `*.bt.json` = 343 files, `Run()` in unit tests about 439 (top-level lines with `/datum/unit_test`: 1,109), 446 entries in `tgui/packages/tgui/interfaces`, 129 `light_switch` switches in MetaStation.
- This document is not legal advice. All points where the conclusion depends on jurisdiction are collected in [08, "What to check with a lawyer"].

## Table of contents

0. Summary
1. Lessons from past attempts
2. Problems of the original and what carries over to us
3. Top risks and mitigations
4. What must not be lost
5. Architecture options for the port
6. Decision map (6.0 accepted, 6.A-6.F open) and 6.1 priorities
7. Draft plan phases with completion criteria
8. Contradictions between reports and tensions with decisions
- Appendix A: what the plan does not rely on
- Appendix B: key numbers
- Appendix C: research gaps and where they get closed

---

## 0. Summary

1. **No precedent.** We found no playable 3D port of SS13 with dozens of players in live multiplayer (a negative result, the search was incomplete, [03 Gaps], [06 Summary 2]). The only mass-scale remake, SS14, is two-dimensional: from the restart (May 2017) to "barebones" rounds with the first atmosphere took about 3.4 years, to thousands of concurrent players about 8-9 years, with hundreds of contributors involved [02 F2, F3]. The main 3D attempt, RE:SS3D, remained pre-alpha after 7 years: 710 commits in `develop`, release 0.0.7.6 from April 2025 [01].
2. **The accepted decisions D1-D22 are, taken together, very ambitious.** First person (D4), 30-100+ players on dedicated servers (D6), a destructible world with durability on every object (D12), pipes, gases and reactions (D20, D21), "space wind" with physics (D22), proximity voice and local TTS (D13, D16), low-poly characters with dismemberment and blood (D7), and all of it by one person with AI (D8). The estimate in report 09: the vertical slice is 160-290 PW (≈3.1-5.6 person-years, low confidence, ±50%, uncalibrated), and D12-D22 make the slice wider than the slice assumed in that estimate (inference).
3. **The architectural foundation of D1 is confirmed by the research.** The Engine/Shared/Server/Client split is confirmed by SS14 practice [02 F4], a pure .NET server without GodotSharp is recommended for three independent reasons [06 Implications 1], and the structure "core as a `net10.0` library plus a Godot project" builds and runs headless on 4.7.2 [06 F13]. Caveat: officially Godot 4.7.x targets `net8.0`, and the minimum TFM is raised to `net10.0` only in 4.8 [06 F1].
4. **Five technical risks determine success:** (a) replication with interest management and prediction under first person, physics and "wind" (Godot has no spatial PVS/LOS layer [04 F2], and SS14 spent three years on PVS and redid it twice [02 F7]); (b) art volume and character/animations (animations have blocked SS3D since 2023 [01 F4]); (c) a model of "actions over time" instead of `sleep`/`do_after` (800 `do_after`, 1,114 blocking `tgui_*` [09 F7]); (d) lighting and rendering of a large station (641-757 light fixtures, limits of 512/88 per frame [06 F7, F8]); (e) license cleanliness (≥28 NC entries in sounds, 1,448 `.dmi` files without license metadata, 1,291 `vox_fem` files without a source [08 F9, F10]).
5. **Tension between the slice and the "essence of SS13."** The D12 slice (one role, no antagonist, no radio) does not test the social deduction that [05 F1, F14-F16] call the core of the game, whereas [01 Implications 1] advises building the slice with at least 3-5 roles and one antagonist. This is question Q2.
6. **First person (D4) has no live precedent.** Centration was abandoned in 2015, SpessVR (first person, VR and desktop) was not released on Steam, and the "safe" option recommended in the sources is a tilted rotating top-down camera [07 Implications 2]. The decision has been made, so the plan reduces the risk with an early prototype and a gate rather than challenging D4.
7. **Recommended architecture: option V2, "data converter plus hand-written C# core."** Data (maps, types, reagents, recipes, strings, icons) is converted automatically (64% of `/obj` and `/datum` types have not a single procedure [09 F3]), and behavior is written anew from the algorithms. OpenDream is not adopted as a runtime (2D, early stage, protocol tied to RobustToolbox), but it is suitable as an optional oracle for golden tests. Fallback: narrowing to an "inspired-by game" in the spirit of Stationeers/Barotrauma [03 §9].
8. **License path:** D3 effectively chooses path A from [08] (an open AGPLv3 port), but "no commercialization" does not remove the problem of NC materials: [04 Q10] and [08] consider NC sounds incompatible with both a commercial release and a share-alike mode (to be confirmed by a lawyer). An asset allowlist and a CI license check are needed from day one.
9. **There are eight blocking questions for you** (section 6.1): time budget and stop criteria (Q1), the composition of "v1" and the social loop (Q2), the order of pillars when resources run short (Q3), the choice of architecture option (Q5), the time and action model (Q7), where physics lives (Q9), world scale as an art contract (Q10), confirmation of the license path (Q28).
10. **Next step:** close the blocking decisions, then the spike phase with numeric thresholds (section 7, phase 1) and gate G1. There are no calendar dates in the plan: there is no data on available hours (Q1).

---

## 1. Lessons from past attempts

### 1.1 Timeline and outcomes

| Project | Engine, period | Outcome | Causes per sources | What this means for us |
|---|---|---|---|---|
| Standalone SS13 / ss13remake (the identity of the two names is not confirmed) | C#/XNA, Lidgren, 2011-2015 | closed in January 2015, code open; by 2016 "almost dead" | burnout; the author kept the original sprites for himself, the repository has placeholders [03 §2, §11] | art rights and dependence on one person kill a project |
| SS14 | RobustToolbox (C#); earlier SFML, Godot 3, then its own Clyde; since May 2017 | survived: thousands of concurrent players, Steam Playtest | community, incomplete parity, own engine, separation of engine and content, stress tests and forks [02, 03 §3] | a model of success, but 2D and hundreds of people |
| Unitystation | Unity 2D, since November 2016 | alive for 10 years, v0.5, "Coming soon" on Steam | huge scope, mention of burnout, the tg clone from ~2017 is outdated; Patreon 14 paying out of 111 [03 §4] | "alive" does not mean "released" |
| SS3D `*beep` | Unity, early 2018 | the author vanished after a few months, concepts and assets remained | sole developer [01 F1] | bus factor = 1 |
| RE:SS3D | Unity, Mirror then FishNet, 2018/19 to date | pre-alpha, release 0.0.7.6 (April 2025); commits to `develop` by year 20/240/117/27/244/41/6/15; Patreon 8 paying ≈45-50 USD per month, bounties $15-90 | loss of the leader, shortage of programmers amid a surplus of artists, scale too early (containers, atmos, electricity before a playable loop), the 2022 rework froze systems (FOV returned in August 2026, atmos not returned), animation blocks health and combat, CI fixed only in December 2021, review queue, two competing Unity upgrades in the queue [01 F1-F6] | the main source of lessons on process |
| Fork `henkhooft/SS3D` | Unity 6 + URP, 2026, AI-assisted | ≈936 commits ahead of upstream; interactions, health, combat, atmos on ECS/Burst (single z), electricity, `.dmm` import (PR #71, 2026-07-30) | docs-first process; debt: `Human.prefab` of 15,604 lines and 126 components, CI only EditMode, headless server crashed on render-dependent code, host mode masks initialization-order bugs; 58 ms per frame is a measurement in Editor Play Mode, not in a build; no independent verification of gameplay, 0 stars [01 F7] | the closest analog of our "one person plus AI" model, confidence medium |
| SSNT | Rust/Bevy, since 2021 | very early pre-alpha, 18 stars | the former SS3D maintainer left Unity for ideological reasons; uses SS3D models [03 §6] | changing the engine on principle did not speed things up |
| Godot attempts | Godot 4 C#, 2024-2026 | Space-Station-Theta (2 contributors, 4 stars, "no interesting gameplay"), SpaceStation13D (3 commits) and other small ones | not enough developers [03 §7] | no precedent of Godot with a team |
| Primulus | Unity, 2010s | tech demo, abandoned after a failed Kickstarter | two Baystation12 developers (details about the top-down view and atmosphere in the demo on the wiki page are not confirmed [07 A1]) | |
| Centration | Unity, then Unreal, first person | Early Access since April 2014 (essentially a training level and an air simulation), removed from Steam in 2015 | multiplayer in the Unreal version did not work; the wiki gives "supposed technical limitations" of the engine as the reason; the version about a failed Kickstarter is not confirmed [01 F8, 03 §8] | the only first-person attempt with multiplayer that we found, a failure |
| SpessVR | Unity, VR and desktop, first person | free build on itch.io, "Coming soon" on Steam with no date, closed source | deliberately "feel, not parity" [03 §9, 05 F38, 07 A6] | the closest analog of D4, but without live multiplayer at 30-100 |
| Other early ones | 2011-2020 | UnitySS, Space Station: Source, 3D on GLUT/C, Java/LWJGL, C++/OpenGL, Minecraft SS13: abandoned; Bluespess (Node.js, pinned to a single tg commit), Griefly (frozen), Mediator (Go transpiler, "doesn't work yet") | see [03 §1, §11] | |
| OpenDream | C# on RobustToolbox, since 2020 | alive, early stage, 2D client | tg CI only compiles the code with `--suppress-unimplemented` [03 §10] | see option V3 |
| Stationeers | Unity 3D, Early Access since December 2017 | success: 88% positive out of 5,958 reviews | narrow scope (engineering and atmosphere), a studio, no antagonists [03 §9] | scope narrowing as a path |
| Barotrauma | MonoGame 2D, 2014-2023 | released 13 March 2023, 95% out of 28,836 | compact format: submarine, 16 players [03 §9] | |
| Lethal Company | Unity, solo author | ≈10 million copies in 3 months, up to 4 players, low-poly, proximity voice [03 §9, 07 I] | small player count and stylization | |
| Hellion | Unity | development stopped in October 2019, servers shut down in March 2021 | few players, unsolvable technical problems, money [07 I] | |
| Ion (Dean Hall) | SpatialOS/Improbable | cancelled 7 March 2017 | Hall's relocation, RocketWerkz priority change, end of the partnership; this is not an SS13 remake but a game inspired by it [03 §8, 05 F38] | do not use as an argument that "3D remakes fail because of technology" |

### 1.2 Why they died

| # | Pattern | Evidence | What in our decisions is exposed |
|---|---|---|---|
| P1 | Scale ahead of a playable loop | SS3D itself admitted: containers, atmos and power grids first, not a playable loop [01 F6.3]; Centration shipped with one training level; Unitystation: huge scope and burnout | D12, D20-D22 widen the slice |
| P2 | Loss of the leader, "one person carries everything" | `*beep`; in 2020 "technical stagnation" without leaders [01 F6.1]; in 2026 at SS14 a letter from 34 staff members, infrastructure keys held by three people, the project core is left without maintenance when the leader is suspended (one side's position) [02 F19, 05 F24] | D8 (one person) |
| P3 | Rework or engine change freezes progress | SS3D 2022: the old code was called over-engineered, restoring what was lost is still unfinished [01 F6.4]; SS14: SFML, Godot, Clyde; SSNT: Unity, Bevy; Centration: Unity, Unreal [03 Implications 2] | D1: the core/client boundary must be fixed early |
| P4 | Fundamental blocker: animation and atmos | an issue about animations since 2023 (bounty $90) blocks health, combat, sitting, ragdoll; atmos is not resurrected (bounty $60, "rocket science") [01 F4]; the 2025 devblog names "animation and atmosphere" as bottlenecks | D7 (character readability), D12/D20 (atmos) |
| P5 | Networking "later" | Centration Unreal without multiplayer; SS14: netcode "worked because of localhost," PVS after ~3 years and two refactorings, prediction ~3 years after the restart [02 F7, F9, F18]; in SS3D server-authoritative movement is planned for milestone 0.0.8 [01 F5] | D6 |
| P6 | Art and rights to art | RE:SS3D's CC BY-NC-SA 4.0 art closed the path to monetization [08 F22]; ss13remake sprites belong to the author [03 §11]; SS3D has two models per clothing item, shape keys for hair [07 A3] | D7, D3 |
| P7 | Upstream drift | Unitystation cloned tg from ~2017, Bluespess is pinned to one commit, while tg merges about 326 PRs a month (2022) [03 §12] | D2 |
| P8 | Money and the review queue | bounties of $15-90 and ≈45-50 USD a month do not create a team; 10 open PRs, two competing engine upgrades [01 F2] | D8 |
| P9 | Process hygiene | auto-build did not work in 2020, fixed in December 2021 [01 F6.7]; the fork's EditMode-only CI gives a false sense of safety [01 F7] | D8 |

### 1.3 Why they survived

| # | Factor | Evidence |
|---|---|---|
| S1 | Owning the game loop and the engine/content boundary | SS14: Engine/Content, Client/Shared/Server; prediction is possible only because the team owns the loop [02 F4, F9] |
| S2 | Rejecting parity and rethinking | combat mode instead of intents, click by bounding box instead of pixel-perfect, APC without areas; self-assessment of ≈15% of SS13 content after 4 years (2021, medium); in July 2022 SS14 overtook tg by merged PRs over 30 days: 520 vs 326 [02 F14, 03 §3] |
| S3 | Inflow of people through specific communities and open stress tests | Colonial Marines stress test in February 2020 (≈200 players), the Russian-speaking wave of late 2021, forks (RMC-14, Delta-V, Frontier and others) [02 F16] |
| S4 | Open code and clean licenses | MIT code, CC BY-SA 3.0 assets with metadata on every file (RGA, SPDX) [08 F21] |
| S5 | Data instead of code, and tooling | YAML prototypes with inheritance, hot-reload, an in-game map editor, a merge driver for maps, integration tests for every prototype [02 F5, F14, F15] |
| S6 | Narrow scope for commercial successors | Stationeers, Barotrauma, Lethal Company [03 §9] |
| S7 | Docs-first with AI (hypothesis) | SS3D fork: ≈936 commits ahead in a limited time with a set of architecture notes; but there is no independent verification, and debt is growing [01 F7] |

### 1.4 Summary lessons and actions in the plan

| # | Lesson | Action |
|---|---|---|
| L1 | Scope kills more often than technology (inference, [07 Implications 12] marks it as interpretation) | a vertical slice with gates, a "what we cut first" list (Q1, Q3) |
| L2 | People and bus factor: ours equals 1 | documentation and ADRs from day one, automation, separate ownership of domain, repository and infrastructure (Q35) |
| L3 | No rework with a freeze | fix the core API (commands, snapshots, events) early and cover it with tests (phase 2) |
| L4 | Character animation and rig is blocker #1 | spike S5 in phase 1, before the depth of health gameplay |
| L5 | Networking and interest management from day one, tests with latency and a remote server | spike S1, always a dedicated process (phase 1, 5) |
| L6 | Godot only as a view; verify Godot 4.7 with a prototype, not with 2019 reviews (the author attributed many SS14 crashes to unstable Mono) | spikes S2-S4 |
| L7 | Parity is not the goal; deliberate refusals of mechanics | Q4: content tiers |
| L8 | Rewrite algorithms rather than transliterate DM; expect several iterations of large systems (SS14 rewrite timings were not rechecked, take them as a guideline) | option V2 |
| L9 | The art pipeline determines schedules no less than code | a type manifest, kitbash, placeholders on day one (Q15) |
| L10 | Automation and size budgets for scenes and scripts (the 15,604-line mega-prefab as an anti-example) | CI in phase 0, limits on scene and class size |
| L11 | Do not chase tg upstream | a pinned snapshot, restartable converters, no merging |
| L12 | Growth comes through communities and stress tests | documentation for host operators (D10), playtests in phase 6, public stress tests in phase 9 |
| L13 | Money will not come: bounties and Patreon do not create a team | a plan for zero funding; D10 reduces costs |
| L14 | Moderation and content policy kill mature projects (appeal queues of months, ERP, governance) | minimal tooling and a policy before the first public build (Q27, Q31) |
| L15 | "Feel, not parity" (SpessVR) | define recognizability criteria (section 4.4) |

---

## 2. Problems of the original and what carries over to us

Conventional verdicts from [04]: GOES AWAY (disappears on its own), PARTIAL (softens, returns in another form), DESIGN (a deliberate decision is needed), WORSENS (becomes our problem). For social problems the verdict CARRIES OVER is added.

### 2.1 Technical problems of BYOND/DM and the codebase

| # | Problem and facts | Verdict | What we do |
|---|---|---|---|
| T1 | Single-threaded interpreter, world at 20 FPS (TICKLAG 0.5), cooperative Master Controller, `CHECK_TICK` in about 235 places, Failsafe watchdog; client verbs run after the engine and are invisible to the MC [04 F1] | PARTIAL | the single-thread ceiling is lifted (atmos, pathfinding, serialization into workers), but a tick budget and a fixed system order are needed; new: data races. Target 20-30 TPS (tg 20, SS14 30) |
| T2 | SendMaps: ≈0.45% of tick per player (up to 0.6%), 22-30% at 50 players; at 100 players 45-60% (extrapolation, linearity not verified); popcap 100/150/200, lag switch from 75 clients [04 F2] | DESIGN | replication as a first-class module with interest management, priorities and a per-client budget; a "cost per player" metric from day one. SS14 benchmark: 200+ players at 30 TPS, peak ≈250 at 20 TPS (April 2022) [02 F7] |
| T3 | Input via verbs/Topic, latency at least tick_lag plus ping, no movement prediction [04 F3] | DESIGN | prediction and reconciliation for movement (especially in first person), interpolation for the rest, lag compensation for combat |
| T4 | Client rendering: appearances, a limit of 100 overlays per atom, multi-z via vis_contents called "hell" [04 F4] | GOES AWAY partially | the limits go away; new: 2D sprites do not convert to 3D, the Godot core has no WebView, tens of thousands of nodes are expensive (MultiMesh and RenderingServer) |
| T5 | Closed engine, unfixable bugs, a blacklist of builds (1622 and others) [04 F5] | GOES AWAY | new: Godot bugs (528 open `topic:dotnet` issues, 96 `topic:multiplayer`) [06 F3, F12] |
| T6 | One vendor: since 10 May 2025 a prolonged DDoS on BYOND; the state after June 2025 is not verified [04 F6] | GOES AWAY as a dependency, WORSENS as our concern | D10 (self-hosting) removes centralization; the server list, accounts and updates remain a question (Q31) |
| T7 | 32-bit memory, 4 GB ceiling, "Out of resources" [04 F8] | GOES AWAY | new: .NET GC pauses, allocations in the hot path; zero allocation in the core tick |
| T8 | Hard deletes and `qdel` (≈3.5 thousand calls), deleted objects wait up to 5 minutes [04 F9] | PARTIAL | the memory scan goes away, leaks through strong references remain: entities as handles (int plus generation), a leak detector in CI |
| T9 | `sleep`/`spawn`/`waitfor`: 344 `sleep(`, 800 `do_after`, 1,114 `tgui_alert/input_*`, 942-945 `INVOKE_ASYNC`; up to 43% of procedures potentially blocking (upper estimate, medium) [04 F10, 09 F7] | DESIGN | actions as data with continuations, timers on int64 ticks, no `Task` and no threads in gameplay (Q7) |
| T10 | Atmosphere: SSair, a pipeline of ≈9 steps, "there were periods when nobody understood how atmos should work"; auxmos (Rust, i686) is not used by tg; atmos is covered by two tests (the classification boundary is fuzzy) [04 F11, 09 F11] | PARTIAL | SoA and parallel processing are possible; decide whether to reproduce behavior bit for bit (Q4, Q9); golden tests |
| T11 | Exploits: Topic/href forgery, tg rate limit of 10 requests per second and 200 per minute [04 F12] | GOES AWAY partially | typed commands, validation and per-type rate limits, the server does not send hidden information (wallhack); new: the DoS surface of our own stack |
| T12 | Typing and DM debt: `.`, New→Initialize, float32, 1-indexed lists, dynamic `null` semantics [04 F13, 09 F12] | GOES AWAY, but a risk of silent bugs during conversion | a converter with checks |
| T13 | Cross-platform: Windows client (WebView2), Linux only for hosting, macOS not mentioned [04 F14] | GOES AWAY | new: C# in Godot without web, mobile platforms are experimental; running the original's reference server on macOS (Apple silicon) is nontrivial (Q6) |
| T14 | Identity and DB: ckey, computer_id (≈75 mentions in 15 files), stickyban, 38 MariaDB tables [04 F15] | GOES AWAY partially | our own accounts and ban system, anti-ban-evasion without BYOND (Q31) |
| T15 | Tests: 453 classes and 1,946 asserts, all integration tests inside a live world; screenshot tests are unstable on GPU [09 F11, 04 F16] | PARTIAL | headless core tests, golden traces, replay, protocol fuzzing |
| T16 | Implicit engine services: `get_turf` (≈3.8-4.0 thousand), `locate` (≈1.8-2.4 thousand), `view/range/oview/hearers` (≈0.9-1.3 thousand), `loc/contents`, `Bump/Cross/Enter`, LOS, `sound()` [04 F17, 09 F5] | DESIGN | an inventory of the semantics of these services before porting content (phase 0) |
| T17 | Size and shape of the codebase: 7,476 `.dm` files in `code/`, 1,226,880 lines, 140 subsystems, ≈41,100 types, of which ≈64% are `/obj` and `/datum` without procedures (upper bound); 427 components, 244 elements, 1,514 COMSIG, ≈5,000 subscriptions; `GLOB.` 6,745 uses, `usr` 3,115, `initial()` 3,436 [09 F1-F4, F8] | DESIGN | a data converter, behavior by hand; an explicit `World` instead of statics, an explicit `Actor` instead of `usr` |
| T18 | UI: tgui (React/TS) in BYOND's embedded browser; 352 windows (446 entries in `interfaces/`), ≈141 thousand lines; in BYOND 516 WebView2 is Windows-only [10 F6, 04 F4] | REWRITE | the Godot core has no WebView, a third-party plugin is limited (see Q20); native `Control`s for the slice |
| T19 | Appearance model: 48,691 `icon_state` in 1,448 DMI, 75.9% of states are single-direction, 23.6% four-direction [10 F1] | DESIGN | there is no automatic sprite-to-3D conversion (see Q15); the core emits an appearance descriptor and change events |
| T20 | Upstream drift: tg merges ≈326 PRs in 30 days (2022) [03 §12] | DESIGN | a pinned snapshot, restartable converters, no merging (Q4) |
| T21 | Legal layer of the original: origin of SS13 (decompilation), Goon under CC BY-NC-SA 3.0, no CLA, NC sounds [08 F2, F6-F10] | CARRIES OVER fully | Q28, Q29 |

### 2.2 Gameplay and social problems

| # | Problem and facts | Carries over? | How it presses on us |
|---|---|---|---|
| G1 | Learning-curve cliff: a "Dwarf Fortress-level" UI; newcomers mostly do not survive to the end of their first week (words of tg admin MrStonedOne, medium) [05 F6, F9] | yes | in first person and 3D, initial readability matters even more; a tutorial and hints are needed by the first playtests (Q25) |
| G2 | Gating of heavy roles by hours and account age (AI 180 minutes and 30 days, Captain 180 minutes, Security 300 minutes and 7 days) and criticism of the grind [05 F10] | partial | the slice has one role; with growth, short thresholds plus achievements are needed |
| G3 | Gimmick roles ("assistant in a suit"), Security burnout [05 F11, F12] | yes | roles with real mechanics |
| G4 | The round is held up by antagonists: "green shift" as a catastrophe, all antagonists against Security [05 F14] | yes | a slice without an antagonist is itself a "green shift"; a director and PvE goals are needed (Q24) |
| G5 | Rules prop up unfinished mechanics (the "stay within your objectives" rule, Paradise's 9,000 words of rules cut to 5,300 in October 2025) [05 F17] | yes | build constraints into the mechanics and a server policy config |
| G6 | Metagaming, NLR, powergaming, knowledge exchange via Discord [05 F19] | worsens | proximity voice (D13) makes metagaming and harassment hard to moderate [05 Implications] |
| G7 | Admin load: appeal queues of months, an admin application queue of up to 1-2 years (SS14), burnout [05 F21] | yes | moderation tools before the first public build (Q31) |
| G8 | Toxicity and a split by RP level (LRP/MRP/HRP); BeeStation closed Golden in July 2021; Sage's 70-player cap is intentional [05 F23, F25-F27] | partial | a single flagship rule profile in data, do not fragment the early population |
| G9 | ERP and 18+: the SS14 hub delists servers (in August 2025 CSAM was found in the Discord of one server); official servers ban ERP [05 F29-F31] | yes | a policy before the first public build (Q27); D10 shifts responsibility to host operators but does not remove the question |
| G10 | Round rhythm: reactive disasters without counterplay (meteors), a Game Director proposal (beats of about 5 minutes) [05 F33] | yes | a director with counterplay |
| G11 | Death and ghosts: respawn is off in tg (`ALLOW_RESPAWN 0`), ghost roles soften the wait [05 F35] | yes | D15 rounds as in SS13; ghost roles |
| G12 | Population: a "sweet spot" of 30-50 players, tg Dynamic thresholds of 25/40/50 players, a "death loop" at low online (Golden, Vulture) [05 Implications] | worsens | D6 (30-100+) with D8 (one developer): no population for playtests (R14) |
| G13 | Governance and bus factor of a mature project (SS14 2026) [02 F19, 05 F24] | yes | Q35 |
| G14 | First-person specifics (inference, no sources, low): hidden identity via "Unknown" needs a replacement for the name over the head, the field of view narrows, first-person combat requires protecting non-combat roles (analogy with RDM in TTT) [05 Implications] | new | Q17, Q22, Q23 |

### 2.3 Summary: what carries over

- **Goes away on its own:** the single-thread ceiling, 32-bit memory, closed engine bugs, appearance limits, dependence on the BYOND hub, the Windows-only client.
- **Softens:** hard deletes, typing, link forgery, the cost of tests.
- **Must be designed:** tick budget, replication and prediction, the model of actions over time, implicit engine services (LOS, `view`, containers), identity and bans, UI, appearance conversion.
- **Becomes our problem:** our own network stack (DoS, wallhack), GC pauses, Godot thread safety, the absence of built-in culling for tens of thousands of objects, C# bugs in Godot, the server list and DDoS protection with a public hub.
- **All social problems carry over:** onboarding, roles, antagonists as the only engine, moderation, ERP, community split, governance. Code does not solve them; a director, policy and tooling do.

---

## 3. Top risks and mitigations

Severity: critical (can stop the project), high, medium. Links to decisions and phases are in the last column.

| # | Risk | Severity | Basis | Mitigation | Link |
|---|---|---|---|---|---|
| R1 | Scope vs. resources: one person with AI against the D12-D22 slice, multiplayer for 30-100+ and first person | critical | slice 160-290 PW (≈3.1-5.6 person-years, estimate, ±50%); SS14 spent ≈3.4 years on "barebones" without 3D; SS3D 7 years of pre-alpha; full parity 700-2,000 PW, and the origin of the source figures of 480 thousand and 130 thousand lines in the estimate is not shown, so the parity estimate is unstable [09] | a budget in hours and horizon (Q1); a "cut first" list (Q3); gates G1 and G2 with stop criteria; a slice by dependencies, not "everything at once"; maximum automation (converters, generators, tests, D8) | Q1-Q3, phases 0-4 |
| R2 | Networking and replication: interest management with LOS, prediction in first person, physics and "wind", 100+ players | critical | Godot has no spatial PVS/LOS layer [04 F2]; SS14 did PVS after ≈3 years and rewrote it twice [02 F7]; atmos and power in SS14 are not predicted [02 F9]; CipSoft on Godot 4.4.1 hit a wall at 80-100 clients on 2 cores (the cause was not analyzed, an independent analysis points to default settings) [06 F2] | a pure .NET server without GodotSharp; our own protocol (candidate LiteNetLib); interest management by rooms and chunks with LOS from day one; spike S1 with bots, 100 then 150-250, at 20-30 TPS, with artificial latency and a remote VPS; prediction only for movement, hands and doors; atmosphere and wind server-side with smoothing; a "cost per player" metric | Q9, Q11-Q13, phase 5 |
| R3 | Art: volume and characters | critical | ≈10 thousand "model slots" for parity (≈4.5 thousand families); a Meta slice of 250-350 models ≈5 person-months assuming 3 h per asset (low); D8: no outsourcing budget; D7: modular clothing, dismemberment, blood, visible medical parameters; SS3D did not close the basic set in years [10 F2, F7, 07 H4] | a manifest "type path, mesh archetype, material, icon" generated by a script; every type visible from day one as a placeholder; CC0 kitbash (Kenney Space Kit 150 and Quaternius Ultimate Space Kit 92, Modular Sci-Fi MegaKit 270+); 50-150 "hero" items by hand; one species and ≈20 clothing items in the slice; AI generation only as rough blockouts for retopology | Q15, Q16 |
| R4 | Character, rig and animations (blocker #1 for SS3D) | high | the animation issue has been open since 2023 [01 F4]; SS3D has two models per clothing item, shape keys for hair, hiding the body under clothing [07 A3]; ragdoll required four states [07 A4]; multiplayer needs both first-person hands and a full body for others (inference) | a single humanoid skeleton, retargeting via BoneMap, CC0 animations (Quaternius UAL, 120+), layered animations; spike S5 before the depth of health gameplay; skeletal animations for hundreds of characters have not been measured, add to S5 | Q16, Q17, phase 1 |
| R5 | Licenses and provenance of code and assets | high | porting DM to C# is a modification of AGPL code and cannot be relicensed, tg has no CLA [08 F2, F3]; ≥28 NC entries in 11 sound attribution files; 1,448 `.dmi` files without metadata; 1,291 `vox_fem` files without a source; 880 tool files without sources; Goon-derived code (`wrestling.dm`, "Goon chems", hulk swing); the CAS deck under CC BY-NC-SA 2.0; xenomorphs [08 F8-F12] | confirm path A (AGPLv3) (Q28); an asset allowlist (CC0, CC BY, BY-SA, public domain), the rest in quarantine; a CI block on NC/ND/Custom/unknown and on files with no manifest entry; grep for "Goon", "ported from" before code translation; DCO for contributors; do not encrypt the PCK with BY-SA content; a list of questions for the lawyer [08] | Q28, Q29, Q30, phase 0 |
| R6 | First person in an SS13-like game with no live precedent | high | Centration was abandoned; SpessVR did not reach Steam; in 2016 the community considered first-person "not right" for SS13 (Goonstation forum, 15 posts, opinions) [05 Summary 4, 07 A5]; the safe option recommended by the sources is top-down | an early first-person prototype in phase 1 (S5); the camera as a replaceable layer: the simulation does not depend on the camera [07 Implications 1]; a design doc "social mechanics in FP" (identity, field of view, voice, combat); gate G1 with an option to revisit D4 | D4, Q17, Q22 |
| R7 | Asynchrony and `do_after`: the infectious `sleep` | high | 800 `do_after`, 1,114 `tgui_*`, 344 `sleep(`; up to 43% of procedures potentially blocking (upper estimate) [09 F7] | `DoAfter`, dialogs and player prompts as first-class objects with a continuation; time in int64 ticks; no `Task` and no threads in gameplay; event handlers do not block; tests for serialization and cancellation | Q7, phase 2 |
| R8 | Code translation of tg without an executable specification | high | tests: 453 classes and 1,946 asserts, all integration; atmos is practically untested; DM semantics (float32, 1-indexing, `null`, `usr`) yield silent bugs; LLM code translation is not clean-room [09 F11, F12, 08 Implications] | rewrite algorithms from the specification, not the text; golden traces from the reference (OpenDream oracle, spike S9) for atmos and shuttles; property tests; health, surgery and combat scenarios from tg unit tests as core tests | Q5, Q6, phases 2-4 |
| R9 | Atmosphere, destructibility and "wind" as a coupled system | high | complexity 5 out of 5 [09 F10]; pipes are ≈80% of the atmos code (16.4 of 20.5 thousand lines, D20); balance fragility (supermatter) [04 F11]; atmos is not predicted [02 F9]; Stationeers' old model of venting a 5×5×5 m room to vacuum took 13 min 45 s [07 D5] | gas and reaction registry data (D21); active cells with sleep and AOI synchronization (a lesson from the SS3D fork [01 F7]); SoA; separation of simulation and visualization through an interface (D18); scenario tests "breach, wind, repair, pressurization"; a physics body budget (D11) | Q9, Q12, phase 4 |
| R10 | Godot platform and C# | high | officially 4.7.x targets `net8.0`, 4.8 raises the minimum to `net10.0` (locally .NET 10 works) [06 F1]; 528 open C# issues; hot reload preserves only `[Export]`; interface calls generate garbage on strings and Variant collections; UB in interop on Linux x64 (issue #118138, verified on 4.6.2, not verified on 4.7.2) affects the Linux client (D9); exporting C# net10.0 for three OSes is not verified [06 F12] | pin versions; spike S3 (export, #118138); a server without GodotSharp; zero allocations in hot paths; a node budget; MultiMesh/RenderingServer by chunks; updates only at phase boundaries | Q37, phase 1 |
| R11 | Lighting and rendering of the station | high | Meta has 641, Delta 757 light fixtures; per-frame limits: 512 clustered elements and ≈88 shadows in the atlas; baked light is static while the world is destructible; volumetric gas effects (D18) have not been measured; Metal on macOS [06 F7, F8] | a budget of shadow-casting lights near the camera, the rest fake light and distance fade; no LightmapGI; spike S2 on M-series and target GPUs; low-res stylization in the spirit of Lethal Company [07 D4] | Q18, phase 1 |
| R12 | Voice and TTS | high | Godot has microphone capture but no codec, VAD or jitter buffer; options: Concentus (pure C#, ≈40-50% of native speed), libopus via GDExtension (support unclear), Steam voice, EOS RTC [06 F16]; proximity voice is hard to moderate by recording [05 Implications]; TTS (D16) has not been researched at all | voice and TTS after the network is stable (phase 7); a spike on licenses, size, memory, latency and three OSes; mute and report tools before enabling; a content policy | Q26 |
| R13 | Moderation, identity, ERP | high | SS14 has admin queues of months, ERP and CSAM on the hub, a governance crisis; BYOND keys will disappear [05 F21, F29-F31, 04 F15] | a minimal toolset (ahelp, notes, bans, appeals, chat filter, mute) in phase 6; a content policy before the first public build; server-side identity | Q27, Q31 |
| R14 | Cold start of the population | high | D6 requires 30-100+, a single developer has no players; "sweet spot" 30-50; death loop at low online [05 Implications] | a low-online mode (fewer slots, bots on behavior trees: tg has 343 `*.bt.json` files, 235 AI controllers), closed playtests, public stress tests in the SS14 style (L12) | Q13, Q25, phase 6 |
| R15 | Bus factor and governance of an open project | high | the suspension of the SS14 leader and the 2026 conflict; loss of the SS3D leader [05 F24, 01 F6] | docs-first, ADR, separate ownership of domain, repository and infrastructure, DCO, CODEOWNERS, code of conduct | Q35 |
| R16 | Tech debt of AI-assisted development | medium | in the SS3D fork: a mega-prefab, gaps in Play Mode checks, UI fragmentation; the Godot analog is huge scenes and monolithic scripts [01 F7, Implications 10] | limits on scene and class size in CI; integration tests in separate processes; independent verification of critical systems (golden tests) | Q34 |
| R17 | UI volume | medium | 352 windows, ≈141 thousand lines of TS; the slice needs about 20-25 core windows; a native rewrite of all windows is unrealistic [10 F6] | native `Control`s for the slice, a typed "state plus action" protocol (analog of `ui_data` and `ui_act`), windows as thin views; do not count on an embeddable tgui (see section 8, C17) | Q20 |
| R18 | Map conversion and world scale | medium | `directional` helpers: 3,791 on Meta; `pixel_x/pixel_y` offsets ≈1,745 (5% of objects); hidden infrastructure: ≈6.6 thousand atmos machines, 3,631 cables; sprites do not define wall height and tile scale [10 F5, impl. 6] | a multi-pass converter (structure, infrastructure, items) with a type table in data; an art contract (Q10) before production; hidden infrastructure is not rendered by default | Q10, Q33 |

---

## 4. What must not be lost (the essence of SS13)

### 4.1 Pillars

From [05 F1-F3, F15, F35], [04], [09]:

1. **A shared fragile station.** Power, atmosphere, doors, depressurization: everything is interconnected, everything can be broken and repaired; the game takes place on an already built station, rather than building from scratch (imitators break when they replace desperate improvisation on someone else's station with Minecraft-style building, one user's opinion, medium).
2. **Roles with expertise and interdependence.** Engineer, doctor, Security, command: skills matter and are not cosmetic.
3. **Hidden antagonists and social deduction** with escalation and traces for investigation.
4. **Players write stories.** RP, hierarchies, factions; the mechanics support this rather than replace it.
5. **Death is meaningful but is not elimination:** ghosts, ghost roles, observation.
6. **A tone of dark comedy** and the right to absurdity.
7. **Forkability:** rules as config (in tg, the `policy config`).
8. **Two hands, containers, inventory** (the Stationeers designer directly names the influence of SS13 on the "two hands" inventory [03 §9]).
9. **Round structure** with a beginning, escalation and an outcome (D15).
10. **Emergence from modular systems** that players combine themselves (analysis from Game Developer, [05 F1]).

### 4.2 What can be improved or dropped

| Element | Verdict | What to do in the 3D version (per [05 Implications]) |
|---|---|---|
| The "roles plus station plus antagonists" template | carry over | the basis of the slice |
| Antagonists as the sole engine of the round | improve | an event director (Dynamic/Game Director) plus station PvE goals |
| Traitor with an objectives checklist | improve | progression, traces for Security, finale goals (tg's "Progression Traitors" design: finale at 80-110 minutes) |
| Rules as a crutch for mechanics | improve | constraints in the mechanics and a server policy config |
| LRP/MRP/HRP as three separate bases | improve | a single flagship rule profile in data |
| A manual admin as the basis of order | improve | tools first |
| A steep learning cliff | improve | a tutorial, mentors via a ghost role, starter roles |
| Click on a tile and intents | drop as is | contextual actions from first person; tg no longer has intents (`a_intent` 0 uses, `combat_mode` 652 mentions) [09 F6] |
| Gimmick roles without mechanics | drop or rotate | "joker roles" per round |
| Reactive disasters without counterplay | improve | preparation and counterplay |
| ERP and sexual content | drop in the official build | policy and tools before launch |
| Hour grinding as the sole gate | improve | short thresholds plus achievements |

### 4.3 What "do not lose" means for the accepted decisions

- **D12 (slice without an antagonist and radio, one role)** does not test pillars 2, 3, 4, 5 and 10. Interdependence of roles cannot be verified with one role. Recommendation: Q2.
- **D4 (first person)** breaks the identity mechanism: "Unknown" instead of a name when the face is covered (`human_helpers.dm`) must be replaced by a visual equivalent (clothing, silhouette, ID, voice). An inference by analogy, no sources (low).
- **D13 (proximity voice)** amplifies metagaming and complicates moderation; text in tg is well logged and filtered.

### 4.4 Proposed recognizability test (inference, for gate G2)

A check of "this is SS13, not just a space sandbox." Each item is yes or no in a closed playtest:
1. Within the first 10 minutes the player picks up an item in hand, uses a tool, breaks or opens a door and sees the consequences of depressurization.
2. One breakage cascades into at least two systems (breach, pressure, damage, sound, doors).
3. There is a dependence on another role (requires at least two roles).
4. The round has an arc: beginning, escalation, outcome.
5. There is hidden information and a way to reveal it.
6. Death moves the player to an observer with capabilities.
7. A newcomer's mistake is not fatal for the round.
8. Tone: humor in texts and items.

---

## 5. Architecture options for the port

### 5.1 Criteria

Compatibility with the accepted decisions (D4 first person, D6 multiplayer, D8 one person plus AI, D11 physics, D12 destructible world, D14 tile grid, D19 no code mods), cost (estimates [09], low), legal cleanliness ([08]), technical risk, scalability toward parity, dependence on external projects.

### 5.2 Options

**V1. A full C# remake by hand from the tg reference** (the SS14 path: content and behavior are written anew).
- Pros: a clean model (ECS or prototypes, events), full control over networking and prediction, no semantic debt of DM; legally, clean-room is possible (path B [08]).
- Cons: the most expensive: no executable specification (453 integration tests, atmos almost without tests [09 F11]); SS14 rewrote large subsystems several times (timings not rechecked); manual data entry for 41 thousand types is unrealistic (inference).

**V2. Data converter plus new logic (recommended).**
- Automatically: `.dmm` maps (632 files, 36.6 MB, a Python parser reads all of them in 1.2 s; ready-made `tools/mapmerge2/dmm.py` and `tools/dmi` in the repository [10 F5]); types into prototypes (YAML or JSON) preserving inheritance; reagents, gases, recipes, designs, jobs, configs (TOML), strings (133 files, 21,740 lines); 343 AI behavior trees `*.bt.json`; icons and placeholders from DMI; `.ogg` sound is imported directly.
- By hand: the behavior core and systems. A share of ≈15-25% of the code (estimate [09 Implications 1], not verified).
- Logic implementation modes: (i) by hand from an algorithm specification; (ii) LLM code translation with review (this is a modification of AGPL code, acceptable under path A, but not clean-room [08 F3]); (iii) a DM-to-C# compiler (no precedents: the Go transpiler Mediator "doesn't work yet" [03 §11]). We recommend (i) for deep systems, (ii) for simple data-in-code.
- Pros: maximum automation (D8), use of tg maps and sprites under AGPL and share-alike (D3), restartability when the snapshot is updated, a clear "data and code" boundary.
- Cons: converter quality; hidden dependencies (`initial()` 3,436 times, `typesof` 876 [09 F3]); there is no DM parser in `tools/`, an external SpacemanDMM `suite-1.11` or the OpenDream compiler is needed (suitability to be checked separately [09 Implications 1]); license allowlist.

**V3. Hybrid with OpenDream.**
- **V3a. OpenDream as the tg logic server plus our own Godot 3D client.** Pros: preserves 1.23 million lines of DM logic, upstream can be pulled in. Cons: DM atoms are two-dimensional (`icon`/`icon_state`, `.dmm` grid), models, heights and 3D collisions would have to be added as a separate layer; the OpenDream client protocol is tied to RobustToolbox; there is no prediction and no interest management of our own; OD itself calls itself an early project: tg is only compiled in CI with `--suppress-unimplemented`, `dreamluau` and byondapi are not supported, open initialization-order bugs (#2713) and visual ones (#2509, #2506); "runs tg" is confirmed only by screenshots in the README [03 §10, 04 F7]. Incompatible with D4, D6, D11, D12 (inference).
- **V3b. OpenDream as a behavior oracle**: a reference server on Linux or macOS (not confirmed on macOS) for golden traces of atmos, shuttles and so on. Pros: cheap, reduces R8. Cons: a working tg launch is needed; the stability and maturity of OD are unknown. Recommended as an optional spike of 1-2 weeks [03 Implications 5].

**V4. Use RobustToolbox (the SS14 engine) with Godot as the 3D presentation.**
- Pros: ready-made networking, PVS, prediction, ECS, prototypes; MIT, .NET 10 [02 Implications 14].
- Cons: tailored for 2D (tile grid, Box2D, sprite layers), maintains its own window and rendering, attaching Godot as a 3D view is nontrivial; SS14 itself moved away from Godot; dependence on a project with the 2026 governance crisis (per the leader's own post, the core and the engine were left without maintenance). Not recommended; use it as a source of solutions (PVS, prediction guide).

**V5. An inspired-by game instead of a port (narrowing the scope).**
- The path of Stationeers, Barotrauma, Lethal Company: take a subset of SS13, narrow the player count and content [03 Implications 9].
- Pros: realistic for one person. Cons: abandoning the "port" as the goal; losing part of the SS13 community. Consider as a fallback if the gates fail.

### 5.3 Comparison

| Criterion | V1 | V2 | V3a | V3b | V4 | V5 |
|---|---|---|---|---|---|---|
| D4 first person | yes | yes | no | yes | difficult | yes |
| D6 prediction and interest management | own | own | no | own | ready-made (2D) | own |
| D8 one person plus AI | hard | best | hard | cheap as an add-on | hard | realistic |
| Legal clarity | yes (B possible) | path A (AGPL) | path A | path A | MIT engine plus AGPL content | yes |
| Effort (estimate [09], low) | maximum | medium | unpredictable | small add-on | complex integration | minimum |
| Technical risk | high (no specification) | medium (converter) | high | low | high | low |
| Parity in the long run | slow | by tiers | fast, but 2D | not applicable | difficult | not a goal |

### 5.4 Recommendation and cross-cutting principles

**We recommend V2** with an optional V3b spike and V5 as a fallback. The principles are independent of the option and are confirmed by the sources:

| # | Principle | Source |
|---|---|---|
| A1 | The core is a `net10.0` library without GodotSharp, `System.Numerics` types, our own entity IDs; tests via `dotnet test` | [06 F13] |
| A2 | The authoritative server is a console .NET process on the core, Godot is only a client; dedicated mode from day one, host mode masks initialization-order bugs | [06 Implications 1, 01 Implications 5] |
| A3 | Entities as handles (int plus generation), an explicit Deleted, weak references for caches, a leak detector in CI | [04 F9] |
| A4 | Data in prototypes with inheritance, validation in CI; a hybrid "Entity plus Prototype plus Components plus EventBus" fits 41 thousand types better than pure ECS | [09 F4, 02 F5] |
| A5 | Cancellable ordered events with return flags (mapping of COMSIG, 1,514 signals); element as a stateless system; trait as a tag with source tracking | [09 F4] |
| A6 | Fixed tick of 20-30 TPS, time in int64 ticks, RNG seeded from the tick, a budget and overrun metrics | [04 Implications 1] |
| A7 | Actions over time (`do_after`) and dialogs as data with continuations | [09 F7] |
| A8 | Replication as a first-class module: interest management with LOS, priorities, budgets, deltas, per-player metrics | [04 F2, 02 Implications 5] |
| A9 | World: a discrete tile grid as the authority (atmos, adjacency, building, maps), continuous movement and 3D visuals on top | [09 Implications 3, D14] |
| A10 | Visuals separated from logic: the core emits an appearance descriptor and change events | [10 Implications 1] |
| A11 | Only typed commands with validation, per-type rate limit, server-side authorization, hidden information is not sent to the client | [04 Implications 6] |
| A12 | Physics in the core (a kinematic controller on tile collisions and simple physics for a limited number of bodies), the client interpolates; Godot physics cannot be rolled back | [02 Implications 4, 06 F9] |
| A13 | All user-facing strings through keys and parameters, not ready-made strings (in tg `span_*` 20,855, `to_chat` 9,816, `visible_message` 3,959 uses) | [09 F9, D17] |
| A14 | A manifest of asset licenses, a CI block on NC and unknown | [08 Implications] |

### 5.5 What to check before the final choice

Phase 1 spikes (section 7): S1 (network), S2 (station rendering), S3 (export and interop), S5 (first person and character), S8 (converter on Meta), S9 (OD oracle, optional).

---

## 6. Decision map

### 6.0 Already accepted decisions D1-D22 and what to clarify in them

This is not a revision: the decisions are yours. The "Confirm or clarify" column holds what the research puts in question or what requires follow-ups.

| D | Decision | Confirmed by research | Confirm or clarify |
|---|---|---|---|
| D1 | Godot 4.7.2 mono, C#, .NET 10, core separate | yes [06 F13] | the official target platform of 4.7.x is `net8.0`; 4.8 raises the minimum to `net10.0`; C# export for three OSes is not verified (S3) |
| D2 | tg reference (snapshot `1d99bd7f`) | yes | upstream drift: pin the snapshot (Q4) |
| D3 | Open project, AGPLv3, no commercialization | path A [08] | confirm path A and the asset policy (Q28, Q29); NC materials are incompatible even without commercialization (inference of 04, 08); Goon code is not allowed |
| D4 | First person | no precedent [05, 07] | an early prototype (S5) and gate G1; design of identity, field of view, combat (Q17, Q22, Q23) |
| D5 | Vertical slice first | yes [01, 07] | the composition of the slice differs between D5 ("basic roles") and D12 ("one role") (Q2) |
| D6 | Multiplayer 30-100+, dedicated servers | yes as a direction | no precedent on Godot (the search is incomplete), the load threshold is determined by spike S1 (Q13) |
| D7 | Low-poly in the spirit of Lethal Company, readable characters | yes | the cost of modular clothing, dismemberment, blood (Q16) |
| D8 | One person plus AI | the strongest constraint | Q1: hour budget; docs-first and automation are mandatory |
| D9 | Client Windows, Linux, macOS; server Linux and Windows | yes | Linux interop UB #118138 (4.6.2) to be checked on 4.7.2; export without templates was not tested |
| D10 | Self-hosting for anyone, Docker, mods | yes | AGPL §13: every host operator with modifications must offer the source; no hub means no server discovery (Q31) |
| D11 | Hybrid physics: discrete hands and slots plus physical throws, falling, dragging bodies | yes | a physics body budget and where physics lives (Q9) |
| D12 | Slice: electricity, machines, construction, atmosphere, health, one role, destructible world | partial | without an antagonist and radio the slice does not test the essence of SS13 (Q2); durability on every object: 35-52 thousand objects per map [06 F7] |
| D13 | Text plus proximity voice plus TTS | partial | Godot has no codec [06 F16]; voice moderation is hard [05]; TTS has not been researched |
| D14 | Tile grid plus free items | yes | `pixel_x/y` offsets ≈1,745 on Meta, `directional` 3,791 [10 F5] |
| D15 | Rounds as in SS13, ≈1-2 hours | yes | round lifecycle and a clean map reload; a low-online mode (Q25) |
| D16 | TTS locally on the client | not researched | model license, size, memory, latency, three OSes, languages: a spike |
| D17 | English plus i18n from day one, Russian second | yes | messages as keys and parameters (A13) |
| D18 | Simplified tile atmosphere, flat single-level maps, volumetric visuals | yes | of the seven station maps only Meta, Delta and Kilo are single-level; IceBox 3 z, Tram, Nebula, Catwalk 2 z each [10 F5] |
| D19 | No code mods, only data and content packs | yes, removes the need for a sandbox [06 F14] | content pack format and validation (Q32) |
| D20 | Pipes, vents, scrubbers, canisters, a simple pump | yes | in tg this is ≈80% of the atmos code; the slice widens relative to the estimate [09] (atmos 8-14 PW) |
| D21 | Gases O2, N2, CO2, plasma, water vapor, combustion reactions | yes | a data registry |
| D22 | "Space wind" in full | yes | ties together physics, prediction, health, sound; requires deterministic server-side physics (Q9, Q12) |

### 6.A Goal, scope, resources

**Q1. Time budget, horizon and stop criteria** (blocking)
- Question: how many hours per week and for what horizon; what counts as success after 6, 12, 24 months; under what conditions we cut scope or change the architecture option.
- Options: (a) a hard horizon of about a year: the goal is only the slice, leave the rest; (b) a horizon of 24-36 months: the slice plus the social loop and playtests; (c) no horizon: a high risk of eternal pre-alpha (SS3D 7 years).
- Trade-offs: (a) delivers a result, but without the "spirit" of SS13; (b) is more realistic for the essence of the game, but requires gate discipline; (c) the absence of constraints hides degradation.
- Recommendation: (b) with gates G1 after the spikes and G2 after the slice and recorded stop criteria (Q3). Calendar dates cannot be set without your answer. Basis: R1, the estimate of 160-290 PW for the slice (low).

**Q2. Composition and criteria of v1; the social loop in the slice** (blocking)
- Question: what counts as "v1 of an SS13-like game" and whether a minimal role-antagonist-investigation loop is included already in the first playable stage.
- Options: (a) the slice as a tech demo (D12 as is: one role, no antagonist and no radio); (b) the slice plus an alpha with 3-5 roles, one antagonist with progression and traces, and a minimal director (2-3 events with counterplay); (c) moving straight toward parity by tiers.
- Trade-offs: (a) faster, but does not test the "this is SS13" hypothesis and a "green shift" is boring; (b) +time, but tests the essence; (c) sharply increases risk R1.
- Recommendation: (b), but design the antagonist and the director right away, implement in phase 6. Basis: [05 F14-F16, Implications], [01 Implications 1]; tg has 142 antagonist types and 62.4 thousand lines, so a minimal progression is needed, not a checklist.

**Q3. Priority of the pillars when resources run short** (blocking)
- Question: the "cut last" order among the pillars: multiplayer for 100, destructible world, atmosphere with pipes, health with dismemberment, proximity voice, TTS, character readability, first person.
- Options: (a) network and atmosphere last; (b) visuals and characters last; (c) voice and TTS first under the knife.
- Recommendation: the order (inference): core and network, atmosphere and destructibility, health and medicine, characters (after the minimum), voice, TTS, dismemberment, volumetric gas effects. Voice and TTS have already been moved out of the slice (phase 7).

**Q4. Parity after the slice: content tiers and behavior fidelity** (important)
- Question: how we grow content and whether "bug-for-bug" compatibility with tg is needed.
- Options: (a) tiers by importance: core and T2 ≈899 thousand lines (73%), the long tail T3 ≈180 thousand (15%), replaceable ≈87 thousand (7%), defines and helpers ≈60 thousand (5%) [09 F1]; (b) "on community request"; (c) no parity plan.
- Recommendation: (a) with a pinned tg snapshot, no merging; rewrite behavior from the algorithm, strict compatibility only for atmos and the systems that balance depends on (supermatter). Parity is not a goal [03 Implications 1].

### 6.B Architecture

**Q5. Choice of architecture option** (blocking)
- Options: V1, V2, V3a/V3b, V4, V5 (section 5).
- Recommendation: V2 plus V3b as an optional spike, V5 as a fallback.

**Q6. Reference server and the development environment on macOS** (important)
- Question: how to check behavior against the original on Apple silicon.
- Options: (a) OpenDream (not confirmed on macOS); (b) a Linux VM or Docker with x86 emulation for the original; (c) no reference: only golden traces from calculations and tg unit tests.
- Recommendation: spike S9 (1-2 weeks): (a) and (b); if that fails, (c) with an emphasis on scenario tests.

**Q7. Model of time and "actions"** (blocking)
- Question: do we accept a deterministic tick with int64 time, `DoAfter` and dialogs as data, with no `Task` and no threads in gameplay.
- Options: (a) data with continuations (serializable); (b) `async/await` on a single-threaded game SynchronizationContext; (c) coroutines.
- Recommendation: (a): it allows saving, cancellation, replication of the progress bar and rewind-safe prediction [09 F7, 02 F9].

**Q8. Data model and prototype format** (important)
- Options: (a) Entity plus Prototype with inheritance plus Components plus EventBus (YAML, as in SS14); (b) a ready-made ECS library; (c) pure ECS driven by data; format: YAML, JSON or TOML (tg already has TOML and JSON).
- Recommendation: (a), YAML or JSON format with validation in CI; element as a stateless system.

**Q9. Physics: where it lives and who is the authority** (blocking)
- Context: D11 (throws, falling, dragging bodies), D22 (wind); Godot physics cannot be rolled back [02 Implications 4]; a pure .NET server needs its own physics: BepuPhysics v2 (pure C#, Apache-2.0) or JoltPhysicsSharp (net9.0/net10.0) [06 F9].
- Options: (A) simple physics in the core (a kinematic controller on tile collisions plus a limited number of bodies, ballistics and wind forces), the client interpolates; (B) Bepu or Jolt on the server and a duplicate on the client for prediction; (C) Godot physics on the client and the server (requires headless Godot, not recommended).
- Recommendation: (A) with Bepu as a fallback for rigid bodies, a strict physics body budget per server (D11). The number will be determined by a spike.

**Q10. World scale as an art contract** (blocking)
- Question: tile size, wall and ceiling height, door and corridor dimensions, character height. Sprites do not define height [10 Implications 6, 07 Implications 3].
- Options: a 2 m tile (GridMap default, Stationeers, matches the Quaternius modular kits); alternatives: 1.5 m and 2.5 m (Space Engineers' large blocks are 2.5 m).
- Trade-offs: a smaller tile saves art but feels cramped in first person; a larger tile increases distances.
- Recommendation: 2 m, ceiling about 3 m (inference, confirm with prototype S5), fix before art production. Make 1×1 items slightly smaller than a tile [07 A3].

### 6.C Networking and security

**Q11. Network stack, protocol, and interest management** (important, gated by spike S1)
- Options: (1) Godot HLAPI/ENet `SceneMultiplayer`: not to be exposed to untrusted clients [06 F3]; (2) LiteNetLib (pure C#, MIT, reliable and unreliable channels, the README warns about an unstable master); (3) Steam Networking Sockets (GodotSteam moved to Codeberg after the GitHub archival on 2026-09-04, C# support unclear); (4) Photon Fusion for Godot (preview, commercial); (5) Klotho (lockstep, experimental, not authoritative).
- Recommendation: (2), custom snapshots and deltas, interest management by rooms and chunks with LOS (not a 25×25-tile square as in SS14), a 20 TPS tick with the option of 30 [04 Implications 1]. Spike S1 verifies this.

**Q12. What gets predicted and what to do about wind** (important)
- Options: (1) only the player's own movement, hands, and doors; (2) plus picking up and throwing items; (3) full rollback of the Shared simulation (requires rewind-safe systems: no timer callbacks, RNG seeded from the tick).
- Recommendation: (1) at the start, with the Kernel designed to be rewind-safe so it can be extended later; atmosphere and wind are server-side: the client predicts without wind, the server corrects (rubber-banding during depressurization is acceptable, inference).

**Q13. Target load, reference server, and minimum hardware** (important)
- Options: a design budget of 30, 100, or 150-250. Reference points: tg popcap 100/150/200; SS14 200+ at 30 TPS, peak ≈250 at 20 TPS; SS14 target of 300 on large stations [02 F7, 04 F2].
- Recommendation: design budget of 100, validation with bots at 150, small public playtests (8-16); define the reference server in S1 (CipSoft: 2 cores and 2 GB caused failures at 80-100 clients with default settings). Define the minimum client hardware in Q18.

**Q14. Security and anti-cheat** (important for server-side validation, later for client-side anti-cheat)
- Options: (a) server authority, typed commands, per-type rate limiting, hidden information is never sent to the client, protocol fuzzing; (b) client-side anti-cheat (EAC via EOS: "no example" [06 F4]); (c) PCK obfuscation and encryption.
- Recommendation: (a) from day one; (b) defer (servers are self-hosted D10, there is no central anti-cheat); (c) no: the code is open, and encrypting BY-SA assets conflicts with the license's DRM clause [08 F16].

### 6.D Content, art, presentation

**Q15. Art pipeline** (important)
- Options: (1) billboards from DMI and voxelization: unsuitable for first person (D4); (2) kitbash from CC0 packs (Kenney Space Kit 150, Quaternius Ultimate Space Kit 92, Modular Sci-Fi MegaKit 270+) covers architecture and furniture, but does not cover SS13 machines and items; (3) hand-made models (vertical slice: 250-350, ≈5 person-months assuming 3 h per asset); (4) AI generation only as rough blanks: the result is a "welded shell" without structure, retopology is needed; licenses (Hunyuan3D-2 is unavailable in the EU, the UK, and South Korea; TRELLIS is MIT with caveats; Meshy free is CC BY 4.0) [07 H5].
- Recommendation: a "typepath, archetype, material, icon" manifest (50-150 "hero" items by hand, the rest parametric), kitbash for architecture, a low-res style with post-processing, AI only for rough blanks; DMI icons for the UI. Each of the ≈10 thousand types is visible from day one as a placeholder [10 Implications 2].

**Q16. Character: rig, animations, clothing, dismemberment, blood** (important, first spike)
- Data: 23 layers, 575 sprite accessories, 30 species (7 enabled), 3,979 wearable types, 222 hairstyles [10 F3]; SS3D uses two models per clothing item; the body under clothing is hidden because perfect weight painting is unattainable [07 A3].
- Options: (a) modular meshes on a shared skeleton, with body parts hidden by flags and shape keys for hair; (b) separate layers without hiding; (c) pre-rendering: impossible (≈4.6e11 body combinations, ≈8e27 equipment combinations).
- Recommendation: (a); vertical slice: 1 species, 6 body zones, ≈20 clothing items, held items are rigid with grip points and 5-10 poses (a rigged model only for clothing); blood as decals, dismemberment via separate limb meshes and placeholders. CC0 animations from Quaternius UAL (120+) and retargeting [07 F1, F2].

**Q17. First-person body and camera** (important)
- Options: (a) hands as a view-model plus a full body for other players (the multiplayer FPS standard, the architect's judgment, no sources); (b) full body in first person (a single rig, harder IK); (c) view switching for ghosts and observers.
- Recommendation: (a) plus (c). Move visibility and LOS computation into the Kernel, not the renderer; the camera is a replaceable layer.

**Q18. Rendering, lighting, minimum client hardware** (important)
- Options: Forward+ (512 clustered elements, ≈88 shadows by default, VoxelGI and SDFGI only here); Mobile (256+256 per frame, 8+8 per mesh); Compatibility.
- Recommendation: Forward+ with a shadow budget near the camera and fake shadowless light for everything else, no LightmapGI (the world is destructible), define the minimum hardware in S2 on an M-series Mac, a mid-range GPU, and an iGPU. Evaluate volumetric gas effects (D18) in S2.

**Q19. Audio** (important)
- Data: 3,793 `.ogg` files import directly; the code uses 3,507 unique `'sound/...'` literals; `vox_fem` (1,291 words) and 882 instrument files are not needed for the vertical slice [10 F4, Implications 10]; Godot has no occlusion.
- Recommendation: allowlist import (replacing NC recordings and sounds with no known source); occlusion from Kernel data (walls, doors); muffling in vacuum from replicated pressure (D22); the SpatialAudio3D and Raytraced Audio addons have not been tested.

**Q20. UI** (important)
- Options: (a) native `Control` nodes for the vertical slice; (b) a WebView shell with a `window.Byond` shim (≈20 calls): Godot core has no WebView, the third-party godot_wry plugin supports Windows, macOS, and Linux X11, draws on top of everything (cannot be embedded into 3D), and has no transparency on Linux [10 F6, Implications 9]; (c) hybrid.
- Recommendation: (a) for the vertical slice (≈20-25 core windows, [10 F6]), keep the "typed state plus action" protocol, with windows as a thin presentation layer; prototype a chat with 10 thousand lines and dense windows at 4K and HiDPI in S6.

**Q21. Localization** (later)
- Recommendation: keys and parameters in the Kernel (A13), translation tables, Russian as the second language (D17); fonts with a verified license (of tg's five fonts only one has an explicit license, Grand9K Pixel CC BY 4.0 [10 F8]).

### 6.E Gameplay and social

**Q22. First-person interaction** (important)
- Data: the `ClickOn` → `melee_attack_chain` → `item_interaction` chain and the tool hooks (`wrench_act` 222, `screwdriver_act` 246, etc.) port almost one-to-one as an event pipeline, but assume a click on an atom and `Adjacent()` [09 F6]. In SS3D, selection is by the object's color ID and has "offset relative to the cursor" bugs [07 A4, 01 Implications 9].
- Recommendation: a single model: a raycast from the camera against simplified colliders, candidate highlighting, a context radial menu (SS3D: hold the right mouse button), two hands and an active hand; a limited set of interaction archetypes (button, lever, container, door, item, machine with UI) [07 E2, Implications 10].

**Q23. Combat model, health, dismemberment** (important)
- Data: 6 damage types, 13 body zones, 166 bodypart, 323 organ, 33 wound, 423 status effect, 87 trauma [09 F10]; D7 requires visibility.
- Options: (a) aimed shooting and melee against hitboxes; (b) a model based on stats and "intents".
- Recommendation: a subset for the vertical slice: a few damage types, 6 zones, a couple of organs (lungs and heart for the sake of oxygen), bleeding, basic wounds and treatment. Defer combat (there is no antagonist in the vertical slice, damage comes from the environment). For non-combat roles and newcomers, protection through mechanics (stamina, stun) rather than rules (analogy with TTT [05 F20]).

**Q24. Antagonists, event director, round pacing** (important, implementation in phase 6)
- Data: tg Dynamic: 5 chaos levels with weights 2/8/46/36/10, player-count thresholds 25/40/50, an admin cancellation window of 15 seconds; 50 rulesets, 114 events, 80 station traits [05 F18, 09 F10]; SS14 antagonist pillars: spark, escalation, full-time job, back and forth, legible clues, accessibility, "fun to lose" [05 F15].
- Options: (1) a minimal traitor with progression plus 2-3 events with counterplay; (2) only a PvE director without PvP antagonists; (3) full Dynamic.
- Recommendation: (1); not part of the vertical slice.

**Q25. Onboarding, death, low player count** (later, decide the policy before playtests)
- Options: a tutorial (built-in as in Goonstation), safe starting roles, mentors via ghost roles, soft gates by hours played; respawn disabled as in tg; bots and fillers on behavior trees at 5-10 players online.
- Recommendation: a tutorial and safe roles in phase 6; bots for low player counts; ghost roles after stabilization.

**Q26. Voice, radio, TTS** (later; content policy earlier)
- Voice options: Opus via Concentus (pure C#) or libopus (GDExtension); Steam voice; EOS RTC [06 F16]. TTS: OS system APIs (different voices, no single character voice, inference), a local open neural model (license, size, memory, and latency not investigated), server-side synthesis (excluded by D16).
- Recommendation: text and radio channels in phase 6, voice and TTS in phase 7 after the network is stable; mute and report before enabling; a TTS spike for licensing and the three OSes.

**Q27. Content policy: ERP, 18+, age, rules** (important, before the first public build)
- Options: (1) no policy (risk [05 F29-F31]); (2) an official hub with rules and strikes, as in SS14 (18+ label, three strikes, removal of minors); (3) policy in the documentation and in the content license, without a hub.
- Recommendation: (3) plus a ban on ERP in the official build and documentation; if a hub appears, (2). Define the target age explicitly; proximity voice increases the risk.

### 6.F Licenses, assets, law

**Q28. Code provenance and license path** (blocking)
- Options [08]: A) an open AGPLv3 port: code translation and adaptation of tg code are permitted, all derivatives are open; B) clean-room: one person writes the specification, another implements it; LLM translation of the source is not clean-room; C) hybrid: the Kernel is AGPL, the services around it are written separately and may be closed.
- Recommendation: path A (follows from D3), with confirmation that commercialization and closing the code are not planned (otherwise B is needed from day one). Conditions: preserve copyrights and headers, DCO for contributors, do not take Goon code (grep for "Goon", "originally licensed", "ported from"), a source offer for AGPL §13 (including the client and Kernel in one process), questions for a lawyer from [08].

**Q29. Asset policy** (important, before import)
- Options: (a) allowlist: only confirmed assets are imported (CC0, CC BY, BY-SA, public domain), the rest goes to quarantine; (b) everything as is; (c) clean-room assets from scratch.
- Recommendation: (a) plus replacing sounds (at least 28 NC recordings, `vox_fem`, instruments, "assumed CC", music from franchises), tier classification T1-T3 [08]: model everyday objects from real references, unique tg designs as BY-SA adaptations with attribution, replace third-party IP (xenomorphs, Robocop, CAS). Our own original assets: a separate CC BY-SA pack, do not encrypt. Do not use RE:SS3D assets (CC BY-NC-SA 4.0): probably incompatible with the BY-SA regime (inference, for a lawyer).

**Q30. Name, brand, lore** (later, before the public build)
- Options: "Space Station 3D" (overlaps with SS3D and SS13/SS14, needs a clearance search), our own name; Nanotrasen lore (the `common_core` repository under CC0 [08 F13]) or our own.
- Recommendation: rely on CC0 lore and our own inventions; do not use the names tgstation, Goonstation, BYOND as a brand.

**Q31. Accounts, bans, admin tools, server list** (important for the minimum, later for the rest)
- Identity options: (a) server-side (name and local bans, plus an optional external identifier); (b) Steam or OAuth; (c) our own account service (DDoS, privacy, GDPR).
- Recommendation: (a) in the vertical slice and at launch; a minimal set of admin tools (ahelp, notes, bans, appeals, chat filter, mute) in phase 6; no central hub at launch, a static server list; a hub with rules later.

**Q32. Content packs (D19)** (later)
- Recommendation: a format with validation and size limits, content is data only (prototypes, maps, textures, sounds, strings); a license manifest is mandatory for every file.

**Q33. Mapping tools and maps** (important)
- Options: (a) only a converter from `.dmm` (D14) and edits using tg tools (`tools/mapmerge2`, `tools/maplint`); (b) a text, git-friendly map format with a merge driver and an in-game editor (the SS14 path); (c) a Godot editor plugin (GridMap: octants of 8 cells, 24 orientations, no per-item transforms: unsuitable for free-placed items [07 C1]).
- Recommendation: (a) plus our own text chunk format with a stable ordering; the in-game editor after the vertical slice. Start with Meta (1 z-level, 255×255, ≈75% empty space, store sparsely).

**Q34. Tests, CI, releases, the "docs-first with AI" process** (important)
- Recommendation: two pipelines: `dotnet build/test` for the Kernel and server; Godot headless import and client export in Docker with a pinned version; integration tests in separate processes; golden and replay tests; limits on scene and class size; a license linter; release automation from day one [06 Implications 15, 01 Implications 10].

**Q35. Project governance and community** (later, DCO immediately)
- Questions: who owns the domain, the repository, and the infrastructure; code of conduct; policy on AI contributions (SS14 rejects "bulk" and low-quality AI contributions while permitting one-line autocompletions; we use AI ourselves, D8); stance toward RE:SS3D (ignore, cooperate, use the fork as a source of requirements and test data: Metastation import, design specs; check the license of the fork's documents separately).
- Recommendation: several administrators of the organization and the domain, DCO, a public AI policy (disclosure and human review), the SS3D fork as a read-only source of requirements.

**Q36. Persistence and DB** (later)
- D15: rounds are ephemeral. A minimum for notes, bans, and statistics.
- Recommendation: an embedded DB per server (tg has 38 MariaDB tables [04 F15]) with migrations, no external services at launch.

**Q37. Godot and .NET versioning, platforms** (later)
- Recommendation: pin 4.7.2 and `net10.0`, verify export for the three OSes (S3), upgrade at phase boundaries, account for 4.8 (minimum `net10.0`); web and mobile platforms are out of scope [06 F12].

### 6.1 Priorities

| Priority | Questions | Why |
|---|---|---|
| **blocking** (planning is impossible without them) | Q1, Q2, Q3, Q5, Q7, Q9, Q10, Q28 | they set the budget, the slice composition, the Kernel architecture, the art contract, and the legal path: resolved before the first line of code and before art production |
| **important** (decide before the phase where they are needed) | Q4, Q6, Q8, Q11, Q12, Q13, Q14, Q15, Q16, Q17, Q18, Q19, Q20, Q22, Q23, Q24, Q27, Q29, Q31 (minimum), Q33, Q34 | they determine the quality and cost of phases 1-6; some are closed by spikes |
| **later** | Q21, Q25 (policy), Q26, Q30, Q32, Q35 (except DCO), Q36, Q37 | they do not block the vertical slice; decisions and policies are due before the public build |

Proposed order of questions:
1. Round 1 (blocking, product): Q1, Q2, Q3.
2. Round 2 (blocking, technical): Q5, Q7, Q9, Q10.
3. Round 3 (blocking, legal): Q28 with a short check of Q29.
4. Round 4 (before the spikes): Q6, Q11, Q13, Q17, Q18.
5. Round 5 (after gate G1): Q15, Q16, Q19, Q20, Q22, Q23, Q33, Q34.
6. Round 6 (before phase 6): Q24, Q25, Q27, Q31.
7. Round 7 (before the public build): Q26, Q30, Q32, Q35, Q36.

---

## 7. Draft plan phases

There are no calendar deadlines (no input data, Q1). Where the reports give estimates, they are expressed in person-weeks (PW): these are estimates from [09] with low confidence, ±50%, and it is unclear at what working pace they were obtained (manual ≈600 lines of C# per PW, or AI-assisted ≈1,800). The numeric thresholds of the criteria below are my proposals and need to be confirmed.

**Phase 0. Decisions and foundation.**
- Contents: close Q1-Q3, Q5, Q7, Q9, Q10, Q28; the repository skeleton (Core, Server, Client, Tools, Content, Docs); an ADR log; CI (`dotnet build/test`, Godot headless import and export in Docker); a license manifest and linter (block NC, Custom, unknown); DCO; a pinned tg snapshot `1d99bd7f`; an inventory of the semantics of the engine's implicit services (`get_turf`, `locate`, `view/range`, `Bump/Cross/Enter`, LOS, `sound()`) [04 F17].
- Completion criteria: `DECISIONS.md` has entries for all blocking questions; CI is green on the empty skeleton on three OSes; the Kernel's `dotnet test` passes without Godot; the linter fails on an asset without a license; the Definitions of Done for phases 1-4 are agreed.

**Phase 1. Risk spikes (gate G1).** Estimate from report 06: the first 2-4 weeks for spikes 1-6; OpenDream 1-2 weeks [03 Implications 5].
- S1 Network: a pure .NET server, transport (candidate: LiteNetLib), an interest-management stub; 100 bots, then 150-250 at 20-30 TPS; artificial latency and a remote VPS (SS14: the netcode "worked because of localhost").
- S2 Rendering: Meta (35-50 thousand objects, ≈650-760 light fixtures) with chunked meshes and MultiMesh, a first-person camera; M-series Mac, mid-range GPU, iGPU.
- S3 Export and interop: C# `net10.0` on Windows, Linux, macOS; checking #118138 on 4.7.2.
- S4 Entity proxies: 20-50 thousand entities, zero allocations.
- S5 First person and character: a gray-box controller with prediction on tile collisions, a single humanoid rig, retargeting of CC0 animations, modular clothing, limb detachment, hands and items, skeletal animation of a hundred characters in frame.
- S6 UI: a chat of 10 thousand lines, windows at 4K and HiDPI.
- S7 Voice and TTS (may be moved to phase 7): capture, Opus, proximity; TTS candidates: license, size, memory, latency, three OSes.
- S8 Data: a `.dmm`-to-chunks converter on Meta (then Delta, Kilo), extraction of types into prototypes (SpacemanDMM or the OpenDream compiler).
- S9 Oracle: running tg on OpenDream or in Docker; comparison of the atmos trace (optional).
- Completion criteria (proposed): S1: 100 bots without time dilation on the reference server, "cost per player" metrics; S2: the target frame rate is reached on three hardware classes (define the threshold in Q18); S3: the export launches on three OSes; S5: the prototype confirms that first person and the animations are acceptable; S8: the converted Meta loads, the statistics match [10 F5] (65,025 cells, 2,702 types, 165,414 instances).
- Gate G1: if S1 fails, we revisit the target player count (D6) or the interest-management architecture; if S2 or S5 fails, we cut art and lighting or return to the camera question (D4); if both fail, we consider V5.

**Phase 2. Simulation Kernel (headless).**
- Contents: `World` without statics; handle-based entities; prototypes with inheritance and validation; a bus of cancellable ordered events; containers (`loc/contents`, `forceMove`, `Entered/Exited`); a tile grid and areas; a scheduler with a fixed system order, three frequencies (movement 20 Hz, everything else 0.5-2 s), int64 time, RNG seeded from the tick; the `DoAfter` framework; a typed command layer with authorization and rate limiting; a content pack loader (D19); messages as keys and parameters.
- Estimate [09]: Kernel 24-40 PW.
- Criteria: 10 thousand or more entities within the tick budget; a stable tick without allocations (measured); determinism: the same seed and commands give the same state hash; integration tests in a separate process pass; GC leaks are caught by a test.

**Phase 3. Data pipeline and gray-box station.**
- Contents: converters (maps, types to prototypes, strings, reagents, gases, recipes); DMI to icons and placeholders; an asset manifest with license status; sound import via allowlist; Meta loads into Godot with placeholder archetypes for 100% of types; hidden infrastructure (pipes, cables) is not rendered by default [10 Implications 5].
- Estimate [09]: importing maps and modules 12-20 PW.
- Criteria: Meta loads from converted data; the counts of types, cells, and instances match the source; the build contains zero assets with an unknown license; the map format is diff-friendly.

**Phase 4. Single-player vertical slice (client and server as separate processes, one client).**
- Contents by dependency: (1) first-person movement, hands, inventory, containers; (2) the interaction pipeline and tools; (3) doors and access, power (cables, APC, SMES), basic machines; (4) object health, destructible walls and windows, construction and deconstruction; (5) atmosphere: a tile-based simulation of O2, N2, CO2, plasma, water vapor, combustion reactions, pipes, vents, scrubbers, canisters, a pump, "space wind", pressure damage, muffled sound; (6) health and medicine (a subset), visual character states; one role.
- Estimates [09] (vertical slice without D20-D22): logic 45-73 PW (health 12-20, atmos 8-14, power 4-6, construction 8-12, doors, access, and roles 4-6), client 20-32, art, animations, audio 24-45, QA 20-35. D20-D22 expand atmos beyond this line item (inference).
- Criteria: scenario tests pass headless: (a) a hull breach, depressurization, wind moves light objects and the player, pressure damage, sound is muffled on the client; (b) repair and repressurization restore a stable atmosphere; (c) power loss and restoration; (d) injury and treatment; (e) golden tests of the systems; (f) tick and memory budgets are met.

**Phase 5. Multiplayer.**
- Contents: session FSM, lobby; replication with LOS-aware interest management, snapshots and deltas, per-client budgets; prediction and reconciliation (movement, hands, doors), interpolation of everything else; late-join that distinguishes "unknown" from "confirmed empty" (a lesson from the SS3D fork: "miss = space"); a world-readiness graph; reconnect; command validation, rate limiting, DoS hardening, fuzzing; per-player metrics; load tests with 30, 100, 150-250 bots with latency.
- Estimate [09]: networking 16-40 PW.
- Criteria: 100 bots plus humans at 20-30 TPS, stably; an automated check that the client does not receive invisible secrets (a wallhack test); a hash-based desync test; late-join and reconnect pass; a 30-minute soak without leaks.

**Phase 6. Round and social loop (Alpha).**
- Contents: lobby, role selection (3-5 roles), round life cycle (1-2 hours, D15), results, clean restart; text and radio channels; a minimal antagonist with progression and traces; a minimal director (2-3 events with counterplay); ghosts and spectating; minimal admin tools (ahelp, notes, bans, mute); tutorial; a low-player-count mode; content policy; closed playtests with 8-16 players.
- Criteria: 10 full rounds without crashes; the recognizability test (4.4) is passed in a playtest; moderation tools are used in practice; feedback is collected; gate G2 (continue, narrow, or stop).

**Phase 7. Voice, TTS, localization, polish.**
- Contents: proximity voice (Opus), occlusion, radio; local TTS; Russian localization; mute and report; settings.
- Criteria: voice works with 30 clients, bots and humans; TTS on three OSes; not a single unlocalized string in the UI (CI check).

**Phase 8. Expansion toward parity by tier.**
- Contents: T2 and the core (roles and departments, chemistry, research, cargo, shuttles, AI and cyborgs, mechs, antagonists), then T3; additional maps (Delta, Kilo; multi-level IceBox, Tram, Nebula, Catwalk as a separate decision).
- Per-tier criteria: the share of types covered by the converter; scenarios ported from tg unit tests (health, surgery, combat); adherence to tick and network budgets.

**Phase 9. Open release and community.**
- Contents: a Docker image and documentation for host operators (D10); the content pack format and validator (D19); a license audit (assets, sounds, fonts); a source offer for AGPL §13; public stress tests; governance and ownership of the infrastructure; hub rules if a hub appears; release automation.
- Criteria: a clean license audit; a new host operator can bring up a server by following the documentation; a stress test at the target player count.

Cross-phase processes: a monthly review of the risk register (section 3); an ADR for every significant decision; tracking the time budget against Q1; limits on scene and class size; independent verification of critical systems with golden tests.

---

## 8. Contradictions between reports and tensions with decisions

### 8.1 Contradictions between reports

| # | Topic | What the reports say | How it is resolved and what to use |
|---|---|---|---|
| C1 | Light fixtures and doors on MetaStation | [09 F13]: 770 light fixtures, 1,354 doors; [06 F7] and [10 F5]: 641 light fixtures, 1,348 doors | 770 includes 129 `light_switch` switches (local check: 129 found), so use 641 (Meta) and 757 (Delta) |
| C2 | Number of tgui windows | [02], [04], [09]: ≈446 (entries in `interfaces/`, 401 files plus 45 folders); [10 F6]: 352 windows (311 root `.tsx` files plus 41 directories with `index.tsx`), DM references 346 names | 446 entries confirmed locally, but this includes non-windows; use 352 for the scope of work; the UI estimate in [09] (≈440 interfaces) is overstated by roughly a quarter (inference) |
| C3 | AI behavior trees | [09 F10]: 91 `.bt.json` files; [10 F4]: 343 | local check: 343 files; use 343 |
| C4 | Number of unit tests | [04]: ≈1,100 `/datum/unit_test` (1,108); [09 F11]: 453 classes, 444 `Run()` | locally: 1,109 top-level lines with `/datum/unit_test` (including procs and variables) and about 439 `Run()`; a meaningful metric: ≈440-450 tests, use 453 classes |
| C5 | Art cost for parity | [09]: 8-10 thousand models at 0.5-2 h each; [10 F7]: 4.5-10 thousand at 3 h each, total 7-16 person-years; [07 H4]: $30-150 thousand for 300-500 unique meshes | all assumptions are low-confidence; for the vertical slice they agree (250-350 models ≈5 person-months; the art block of 24-45 PW), for parity they diverge by roughly a factor of two due to the per-asset assumption; use as an order of magnitude |
| C6 | SS14 content share relative to SS13 | [02 F14]: an early FAQ says ≈5% (undated, low); [03 §3]: ≈15% after 4 years (a self-assessment by PJB3005 on HN, 2021, medium) | use ≈15% as a 2021 self-assessment; do not use 5% |
| C7 | Reasons SS14 left Godot 3 | [06 F6]: "constant crashes" and "terrible performance" were seen only in a snippet (low); [02 F11], [03 §2], [04 F5]: Mono crashes and the lack of batching are confirmed directly in 2019 posts | crashes are confirmed, but the author attributes many of them to the unstable Mono in Godot 3; applicability to 4.7 is unverified (spikes S2-S4) |
| C8 | Godot in the SS14 main branch | [03 §2]: merged in April 2018 (progress report 6); [02 F2]: "merged in April 2018" is not confirmed | not used, does not affect decisions |
| C9 | Details of Primulus and Centration | [01], [03]: Primulus is top-down 3D, a tech demo with atmosphere; Centration is first person, multiplayer in the Unity version; [07 A1]: these details are not confirmed on the wiki page (taken from snippets) | we use only what is confirmed: Primulus was by two Baystation12 developers, a tech demo, a failed Kickstarter; Centration used Unity then Unreal, was removed from Steam in 2015, per the wiki the reason was "allegedly technical limitations" with no specific engine named; the "first person" format follows from the headline of the GamingOnLinux article (FPS space simulation) [01 F8] |
| C10 | Ion (Dean Hall) | [03] includes it among the 3D inspired-by attempts; [05 F38]: not an SS13 remake, exclude from the argument about remakes failing | Ion is an inspired-by game, cancelled for organizational reasons; do not use it as an argument about technology |
| C11 | SS3D contributors | [03]: 55 (not re-verified); [01]: 55 not reproduced, locally 61 unique emails and 68 names with duplicates | use "≈55-61 over the whole history, uneven contribution" |
| C12 | SpessVR status | [03]: playable for free on itch.io, "Coming soon" on Steam; [05]: "has not even reached Early Access" | consistent: a build exists on itch, there is no Steam release; live multiplayer for 30-100 players is not confirmed |
| C13 | Size of the atmosphere code | [09 F10]: 22.5 thousand lines (LINDA plus pipenets); [05 F4] and D20: 20.5 thousand (of which 16.4 thousand in 66 machinery files, LINDA ≈1.4 thousand) | different definition boundaries; use 20.5 thousand for the `atmospherics` module |
| C14 | Sound: size and attribution files | [10 F4]: 116.5 MB, 82 attribution files; [08 F9], [09]: 121 MB, about 90 (92) files | a matter of units (MB vs MiB) and of the definition of an "attribution file"; does not affect conclusions |
| C15 | Components and elements | [04]: 357 and 235 files; [09]: 427 and 244 types | different units (files vs. types); use the types from [09] |
| C16 | Number of `icon_state` | [07]: 48,712 (including `tools/`, 1,457 DMI); [09], [10]: 48,691 (`icons/`, 1,448 DMI) | different boundaries; use 48,691 |
| C17 | Embedded tgui as a cost-saving lever | [09 F10, effort estimate]: "embedded tgui (UI −60-80%)"; [10 F6, Implications 9]: Godot core has no WebView, the plugin is limited | do not budget a 60-80% saving on UI; use native UI (Q20) |
| C18 | Godot HLAPI and scale | [06 F2]: CipSoft failure at 80-100 clients; an independent analysis of the same problem: default settings; once the settings are changed, hundreds and thousands of connections in a synthetic test | the conclusion "unsuitable" is proven only for the default configuration; for untrusted clients `SceneMultiplayer` is not used anyway (Q11) |

### 8.2 Tensions between DECISIONS.md decisions and report findings

These are not errors in the reports or criticism of the decisions, but places where the decisions need to be confirmed or made more specific.

| # | Tension | Substance | Where addressed |
|---|---|---|---|
| X1 | D4 (first person) and the sources' recommendations | [01 Implications 9], [07 Implications 2]: the least risky basis is a tilted rotating top-down camera with cutaway; [05]: there is no live precedent for first person | R6, Q17, S5, G1 |
| X2 | D5 and D12 | D5: "basic roles", D12: "one role is enough"; D5: "basic atmosphere", D20-D22: pipes, gases, reactions, wind | Q2, Q3 |
| X3 | D12 and the essence of SS13 | a slice without an antagonist and radio does not test social deduction [05 F1, F14-F16] | Q2 |
| X4 | D6 (30-100+) and D8 (one person) | there is no population for playtests, the networking block of 16-40 PW is already in the multiplayer estimate | R14, Q13 |
| X5 | D13 and D16 (voice and TTS) | no codec in Godot, moderation, TTS not investigated | R12, Q26 |
| X6 | D3 ("no commerce") and NC materials | NC sounds are incompatible with the share-alike regime as well (inference, [04 Q10], [08]); RE:SS3D assets under CC BY-NC-SA 4.0 probably cannot be mixed with a BY-SA pack (inference, for a lawyer) | Q28, Q29 |
| X7 | D18 ("tg maps are flat, single-level") | only Meta, Delta, Kilo are single-level; IceBox has 3 z, Tram, Nebula, Catwalk have 2 z each [10 F5, 07 C2] | Q33, phase 8 |
| X8 | D1 (.NET 10) and Godot support | officially 4.7.x targets `net8.0`, .NET 10 works locally, 4.8 requires at least `net10.0` [06 F1] | Q37, S3 |
| X9 | D9 (Linux client) and interop | UB in interop on Linux x64 (#118138) reproduced on 4.6.2, not verified on 4.7.2 | R10, S3 |
| X10 | D12 (durability for every object) | 35-52 thousand objects per map [06 F7]; data is needed on the cost in memory, network, and rendering (no data) | R9, S1, S2 |
| X11 | D11 and D22 (physics) versus D6 (prediction) | Godot physics cannot be rolled back, atmos and wind are not predicted [02 F9] | Q9, Q12 |

---

## Appendix A. What the plan does not rely on

Claims with verdicts of unsupported or unverifiable, or not confirmed by verification. They are not used in the text of the synthesis (or are used only with a caveat).

**[01]** 23 design specs of the fork (in fact 20 in the table, 27 mentioned, 39 in the directory); "the oldest open PR is the traitor UI"; "more than 50 models in a month in November 2019" (an estimate of the order of 40+); "about 17 contributors in Q1 2023"; the exact "55 contributors" (locally 61 unique emails with duplicates); "the `*beep` sources were not published"; spatial hashing on the tilemaps page; "no blockers" for PR #1511; 58 ms per frame as a build metric (this is Editor Play Mode).

**[02]** nightly builds in October 2017; "Godot merged in April 2018"; "4K scaling" and "closed nature, Wine, Linux" as reasons SS14 left BYOND; "~80+ systems without prediction" (fact: 77 of 104 tracker items, some items are grouped); "≈250 players at 30 TPS" (fact: 200+ at 30 TPS, peak ≈250 at 20 TPS); "first playable round after ≈3.5 years"; "80+ authors" (75); a peak of 4,703 per the tracker, with internal contradictions; the timings of the subsystem rewrites F12 and F13 (not re-verified, except for individual items); "RMC-14 is a fork of SS14" (it is a game built on Robust Toolbox).

**[03]** "Kickstarter failure" for Centration; "Ion is an SS13 remake and a solo project without funding"; "Meeple Station in Early Access in February 2019"; "no full playable 3D port exists" as an absolute; "OpenDream runs tg" as our own verification (based on screenshots); 408 SS14 contributors and push dates; the SS14 peak online count of "4,701 versus 21,444".

**[04]** "a BYOND developer considered the DDoS a personal vendetta"; "≈26 comments with bug numbers" (≈4); "Godot has no interest management" (there are per-peer filters, but no spatial PVS/LOS); "weak macOS drivers as the reason SS14 left Godot"; "all previous ports failed because of mechanical density" (the opinion of one commenter); SendMaps linearity of 45-60% at 100 players (an extrapolation); corrected numbers (140 subsystems rather than 147; components, elements, RegisterSignal, range/view, computer_id); not re-verified: the BYOND reference (Threads, 2^24, Topic), the state of BYOND after June 2025, SS14 audio occlusion in workers.

**[05]** "SS14 is bigger than SS13 in player presence"; "Fox McCloud left in 2023 because of harassment"; "an admin strike in 2025"; "zero tolerance for ERP overloads the appeals process"; "Centration is the only first-person attempt"; "Ion is a remake"; "SpessVR is in early access"; "BYOND does not intervene in ERP" as a blanket claim; role distribution and round length (no data); F3, F5, F7, F11-F13, F15, F19, F20, F25, F26, F28, F33, F35 were not re-verified.

**[06]** light fixture counts of 770 and 890 (fact: 641 and 757); "ENet is unsuitable for 100-250 players" as a proven fact; issue #121640 as "remotely exploitable" (not confirmed by the maintainers); "the only multiplayer example among Godot hits"; the forum thread on Cozy Quest scalability (was not opened); ".NET maintainer" (a contributor); not re-verified: "there is no 3D game on Godot with 100+ players", hot reload with an external ProjectReference, exporting C# `net10.0` for three OSes; microbenchmarks (1-3 runs on a single machine) should be used only as a rough guide.

**[07]** "walls block the view", "gravity vs. zero gravity", "atmosphere is more expensive on CPU" as a community opinion (not in the thread); Stationeers "atmosphere in separate threads" and "the bottleneck is lighting and rendering"; Space Engineers "intermediate ship sizes"; "MultiMesh draws everything regardless of visibility" (a forum snippet); manual modeling prices ($50-150 and $200-500, $20 per hour: a vendor FAQ and a single test); Primulus and Centration details from snippets; the Mixamo terms were not verified; view/hearers 527 (370 and 535), pixel_* 2,400 (≈3.2 thousand).

**[08]** Art. 1259(5) of the Civil Code of the Russian Federation (the source did not open); "CONTRIBUTING prohibits Nazi symbols"; "COPYRIGHT.txt is mandatory" (it is recommended); "the consent of all past authors was not obtained when switching to AGPL" (not proven in the PR); the xenomorph and facehugger counts of 61 and 16 (53 and 14); tgui 49 license headers (71); not re-verified: the texts of the Zapsplat and Pixabay licenses, trademarks, the BYOND EULA, compatibility of Steamworks and AGPL, Steam DRM and CC BY-SA.

**[09]** tgui 130,798 lines in 916 files (121,884 lines of `.ts/.tsx` in 908 files plus 18,753 `.jsx`); "128 subsystems, 13 ticker" (140 and at least 22); rust-g ≈85 functions (76); "16,028 non-empty Meta tiles" (not reproducible: 15.7-18.3 thousand depending on the definition); 770 light fixtures and 1,354 doors (641, 1,348); the original figures of 480 thousand and 130 thousand lines of active logic (provenance not shown, the parity estimate of 700-2,000 PW is not robust); "91 `.bt.json` files" (343); "≈440 interfaces" (352 windows); "embedded tgui saves 60-80% of UI" (depends on WebView, which Godot core lacks).

**[10]** "a WebView shell with a shim is cheap" in its original wording; Meshy limits of 100 and 1,000 credits as an API budget; the assumptions "3 h per asset", credits, and hours of manual work (low); tgui windows "446" as the number of windows (352).

---

## Appendix B. Key numbers

| Area | Figure | Source |
|---|---|---|
| tg code | 7,476 `.dm` files in `code/` (7,494 in the whole tree), 1,226,880 lines; 140 subsystems; `world.fps` 20 | [09 F1, F2] |
| Types | ≈41,100 (41,192 paths), ≈64% are `/obj` and `/datum` without procs (upper bound); `/obj/item` ≈11.4 thousand | [09 F3, 10 F2] |
| Patterns | 427 component, 244 element, 1,514 COMSIG, ≈5,024 subscriptions, 1,924 `AddElement`; `initial()` 3,436, `typesof` 876 | [09 F3, F4] |
| Asynchrony | 344 `sleep(`, 800 `do_after`, 1,114 `tgui_*`, 945 `INVOKE_ASYNC`, 1,612 `addtimer` | [09 F7] |
| Maps | 632 `.dmm` (36.6 MB), 7 station maps; Meta 255×255, 1 z, 65,025 cells, ≈75% space, 2,702 types, 165,414 instances, ≈35.4 thousand objects, 641 light fixtures, 3,791 `directional`, ≈1,745 offsets; Delta 52.5 thousand objects, 757 light fixtures | [10 F5, 06 F7] |
| Z-levels | Meta, Delta, Kilo have 1 each; Tram, Nebula, Catwalk have 2 each; IceBox has 3 | [10 F5, 07 C2] |
| Sprites | 1,448 DMI in `icons/` (1,457 total); 48,691 `icon_state`; 138,892 frames; 75.9% single-direction, 23.6% four-direction, 0.5% eight-direction; 91.6% at 32×32 | [10 F1] |
| Visual types | items 6,363, structures 1,541, machines 710, mobs 345, turfs 395, effects 1,014; ≈4.5 thousand families; on Meta 87 families account for 80% of instances, 274 for 90%, 591 for 95% | [10 F2, F5] |
| Character | 23 layers, 575 accessories, 30 species (7 enabled), 3,979 wearable types, 222 hairstyles; bodies ≈4.6e11, equipment ≈8e27 | [10 F3] |
| Sound | 4,423 files in `sound/`: 3,793 ogg, 531 mid; 116.5 MB; 3,507 literals in use | [10 F4] |
| UI | 352 windows (446 entries), ≈141 thousand lines, 416 `ui_data`, 414 `ui_act`, ≈20 `Byond.*` calls; the core of the slice is ≈25 windows | [10 F6] |
| Tests | 453 classes, 1,946 assert, ≈439-444 `Run()`; atmos ≈2 tests (nominal) | [09 F11] |
| System size (DM lines) | atmos 20.5-22.5 thousand; mob health 67.5 thousand; chemistry 43.7 thousand; antagonists 62.4 thousand; machines 61.6 thousand | [09 F10, 05 F4] |
| Slice estimate | 160-290 PW (≈3.1-5.6 person-years), low confidence; slice art 250-350 models | [09, 10 F7] |
| Parity estimate | 700-2,000 PW (≈13-40 person-years), not robust | [09] |
| SS14 | restart in May 2017; barebones build with the first atmosphere in Oct. 2020 (≈3.4 years); stress test in April 2022: 200+ at 30 TPS, peak ≈250 at 20 TPS | [02 F2, F7] |
| tg load | popcap 100/150/200; lag switch from 75 clients; SendMaps ≈0.45-0.6% of a tick per player | [04 F2] |
| Godot | Forward+ 512 clustered elements, ≈88 shadows; an interop call ≈50-90 ns; create, add, and free 10 thousand `Node3D` ≈42-48 ms; 528 open `topic:dotnet` issues | [06 F8, F11, F12] |
| SS3D | 710 commits in `develop`; release 0.0.7.6 in April 2025; Patreon with 8 paying patrons | [01] |

---

## Appendix C. Research gaps and where they are closed

| # | Gap | Where we close it |
|---|---|---|
| 1 | TTS (D16): models, licenses, size, memory, latency, three OSes, languages | spike S7, phase 7 |
| 2 | Proximity voice: codec, jitter buffer, occlusion, moderation | S7, phase 7 |
| 3 | Social dynamics in first person (identity, field of view, combat): no sources | design doc and the closed playtests of phase 6 |
| 4 | Godot: C# export for three OSes; a load test of `SceneMultiplayer` on 4.7.2; rendering benchmarks (641-757 light fixtures, 35-50 thousand objects); #118138 on 4.7.2 | S1-S4 |
| 5 | Real cost of art and animations and the working speed of the AI assistant | measurements in S5, phases 3-4 |
| 6 | Current SS13 population, role distribution, round length | no data; rely on indirect data ([05 Gaps]), a community survey |
| 7 | Legal questions (10 items in [08]) | before the public build |
| 8 | Trademarks and name | Q30 |
| 9 | State of BYOND after June 2025; running tg on OpenDream on macOS; OD performance | S9 |
| 10 | Cost of destructibility of every object (D12) in memory, network, rendering | S1, S2, phase 4 |
| 11 | Skeletal animation of hundreds of characters | S5 |
| 12 | RE:SS3D activity outside GitHub; license of the fork's documents | Q35 |
| 13 | Compatibility of AGPL with Steamworks terms; Steam DRM and CC BY-SA | a lawyer, phase 9 |
| 14 | Hot reload with an external `ProjectReference` | phase 0 |
