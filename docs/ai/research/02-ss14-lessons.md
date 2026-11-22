# 02. Space Station 14 as a Remake of SS13: Lessons for a 3D Port

Research date: 2026-10-05. Direction: space-wizards (SS14, RobustToolbox), devlogs, dev wiki, forum.
All sources in square brackets were actually opened; the URL list is at the end of the file. Third-party text is paraphrased in our own words.
Status: passed adversarial verification on 2026-10-05, corrections applied in place (the ⚠️ mark = not confirmed or corrected), results in the "Verification" section at the end.

## Summary

SS14 is the only SS13 remake that has reached a mass audience (thousands of concurrent players on Steam), but it took about 8 years. The game is 2D; there is no 3D in it and never was, so it has no direct experience of a "3D port of SS13". It does have experience with everything else: ECS, server-authoritative netcode with PVS, client-side prediction, YAML prototypes, rewrites of atmos/power/physics/medicine, community migration, licenses, infrastructure and project management.

Key points:
1. The Engine / Content and Client / Shared / Server split is confirmed by practice. Everything that must be predicted lives in Shared. This matches your plan of a "Godot-free core + Godot client".
2. SS14 used Godot 3 as render + audio + GUI + input (Dec. 2017 — May 2019) and abandoned it: Godot "got in the way" (UI, 2D performance, Mono crashes, hacks via custom binaries). The criticism has to be rechecked on Godot 4.7, but the "simulation / Godot" boundary is the main architectural risk.
3. PVS, netcode optimizations and prediction were added late and painfully. Movement prediction shipped ~3 years after the project restart (May 2017 → July 2020), and in the prediction tracker (Oct. 2025, as of the check) ~77 of 104 items are not marked as done.
4. Every major SS13 subsystem (power, physics, body/medicine, atmos) was rewritten 2–3 times. Rewriting the algorithms in C# rather than translating DM works, but is expensive in terms of schedule.
5. Licenses: the SS14 code is MIT, assets are mostly CC-BY-SA 3.0 with metadata on every sprite. The tgstation code is AGPLv3 (checked in the local clone): a faithful translation of tg code (DM -> C#) would make the port a derivative AGPL work.
6. Organizational risks are real: in 2026 the project has a public governance conflict and an infrastructure outage. "The curse of remakes" is about people and scope no less than about technology.

## Findings

### A. BYOND, timelines, people

**F1. Reasons for leaving BYOND.** The authors name: no client-side prediction (movement lags by at least the ping; S7), almost no non-ASCII support (0xFF is reserved for control codes and breaks the Cyrillic letter "ya" in Windows-1251; S5), practically no shaders (only presets such as color matrices; S2), pixel-perfect clicks with pixel hunting (S2). The general formula: SS13 "outgrew" BYOND (S18). [S18, S5, S7, S2] Confidence: high for what is listed.
⚠️ Corrected during verification: this previously included "proper 4K scaling" and "closed nature and poor Wine/Linux support". In S5, the 4K scaling problem refers to the Godot UI, while the author explicitly calls image scaling in BYOND good; nothing about closed nature and Wine/Linux as reasons for leaving BYOND appears in any of the opened devlogs or in S18. These two items were removed as unconfirmed.

**F2. Chronology.** 2011 — a closed remake by former Goonstation developers (S18). Jan. 2015 — code opened (S18, S19). After that the project almost died: S19 says "within a year and a half", S18 — "within half a year" (the sources disagree). Restart: May 2017 per S18 (PJB3005 and friends), S19 says "around the start of 2017". ⚠️ "Nightly builds in Oct. 2017" is not confirmed (nightly builds are mentioned only in S3, Sept. 2018). Dec. 2017 — Godot branch (S1); ⚠️ "Apr. 2018 — Godot merged" is not confirmed (S2, June 2018, merely says the Godot refactor has already been done); Feb. 2019 — decision to write its own renderer (S4, 23.02.2019); May 2019 — Godot removed (S5, 12.05.2019). Feb. 2020 — Colonial Marines stress test (S6). June 2020 — Steam page, Patreon (S7). July 2020 — movement prediction (S8). Oct. 2020 — "playable, albeit barebones" rounds and the first atmosphere (S9). Feb. 2021 — Steam Playtest announcement (S22). End of 2021 — stable round-the-clock online (S18). Apr. 2022 — 110 players in a live round (S13). July 2022 — surpassed tgstation by merged PRs over 30 days (520 vs 326, excluding RobustToolbox; S16). Jan. 2025 — "thousands of concurrent players" (S19), feature parity is still not reached.
Total (⚠️ wording corrected): from the restart (May 2017) to the "barebones" rounds with the first atmosphere (Oct. 2020) ~3.4 years; but the game was already called "playable" earlier (S9), and the CM stress test with ~200 players took place in Feb. 2020 (~2.75 years after the restart), so "~3.5 years to the first playable round" cannot be used as a fact. To a stable round-the-clock population ~4.5 years, to today ~9 years since the restart. [S19, S18, S9, S13, S16, S22] Confidence: high on dates, medium on interpretation.

**F3. Team size (estimate).** There are no official figures. In 2017–2019 the core is a handful of people (3–6 names in devlogs). Feb. 2020: ~20 contributors in a month. Autumn 2021: ~315 contributions and ~700 commits in 5 weeks. Nov.–Dec. 2022: 773 PRs from 75 authors over two months (⚠️ it used to say "80+", recounted from the author list in S17). The authors say "hundreds of contributors". Funding is Patreon (hosting, art), everything else is volunteers. [S6, S12, S17, S19] Confidence: medium.

### B. Architecture

**F4. Engine / Content and Client / Shared / Server.** The RobustToolbox engine (C#, .NET 10 SDK) and the game as a "content pack". Shared cannot reference Client/Server; it is needed for prediction and common network types. Code is grouped by game systems, not by file types as in SS13 `code/`. The engine is attached as a git submodule, version at the time of reading 292.0.0 (major bumps on every breaking change). Content is released once a month from the stable branch, hotfixes overnight. [D6, E4] Confidence: high.

**F5. ECS.** A component is data only, a system is all the logic, communication goes through directed/broadcast events (cancellable, handled). The old style of "logic in components (E/C)" is forbidden for new PRs. The reason is the typical SS13 problems with hierarchies (a borg is a mob, a machine and buckleable at the same time). Entities are described by YAML prototypes (inheritance, a set of components), YAML hot reload since 2021. The body is "a bag of containers with organ entities", events are relayed to organs. In 2022 the maintainers discussed a two-layer ECS (archetypes + sparse), experiments with FLECS, and the performance of the current implementation. [D1, D8, S10, E1] Confidence: high (docs); I did not find the duration of the ECS migration (Gaps).

**F6. Networking.** Server-authoritative; the transport is a fork of Lidgren (UDP); replication via ComponentState, the source generator `[AutoGenerateComponentState]` + `[AutoNetworkedField]`, `Dirty()` / `DirtyField()`; the client and server have different `EntityUid`s, linked through `NetEntity`. ZStd compression, XChaCha20-Poly1305 encryption instead of the broken AES-CBC. The authors themselves call the connection handshake convoluted and grown over the years. Documentation advice: do not call Dirty every tick, send "value + rate of change" (food, charge). [D3, D4, S15] Confidence: high.

**F7. Netcode evolution and pitfalls.** 2018: they serialized the state of all entities (~50 kB), made a binary diff, which at 60 Hz is 3 MB/s and a "burning" CPU; moving to change tracking cut the server load by 6x. Feb. 2020: PVS ("bubbling") added, but buggy. CM stress test: an implicit limit of 32 connections in Lidgren and a copy-paste bug that sent large states over a reliable channel; not noticed on localhost. Dec. 2021 and Apr. 2022: two PVS refactors (S21, S13), 110 players in a live round without server dips, target of 300 players on large stations (S13). Stress test of 9 Apr. 2022 (S14): at 30 TPS the server held 200+ players without slowing down, the peak of ~250 was reached after dropping to 20 TPS; the authors expected the server to handle 350+, but noted network problems causing rubber-banding. ⚠️ It used to say "~250 at 30 TPS (20 TPS also fine)", which distorted the source. Stutters when creating entities at PVS chunk boundaries (S16). [S2, S6, S21, S13, S14, S16] Confidence: high.

**F8. PVS and hidden information.** By default a 25x25 square around the player entity; outside PVS, entities on the client are frozen and go to nullspace. For hidden state: mind entities in nullspace, `SendOnlyToOwner`, `SessionSpecific`, PVS overrides (session, global, view subscriber). A direct consequence from the documentation: atmos and power cannot be predicted. [D2] Confidence: high.

**F9. Prediction.** The client rolls the state back to the tick of the server state, applies it and re-runs the unconfirmed input; the client runs "ahead" of the server. This is possible only because the team owns the game loop (the authors call this the key advantage of their own engine). Movement — July 2020 (S8); weapons, melee, airlocks, stack merging were converted piece by piece through 2023 and beyond (⚠️ not rechecked; in tracker E2 the server-side part of GunSystem as of Oct. 2025 is still marked "extreme difficulty" and not checked off, so weapons are only partially predicted). Rules: new code is predicted if possible; the problems are randomness, UI/sound/popups (duplicated on re-run), reference types (`IRobustCloneable`), timers (2020 doors were incompatible with rewind). In tracker E2 (opened 2 Oct. 2025; recounted 2026-10-05) there are 104 items, of which 27 are marked done and 77 are not (⚠️ it used to say "~80+ systems without prediction"; some items group many systems, for example Chemistry, Cargo, and the list was compiled by Content.Server, so client UI is accounted for incompletely). The same issue explicitly lists what is not predicted in principle: atmos and Pow3r (they need whole-station data, which is incompatible with PVS), ChatSystem, ThrowingSystem for other people's entities, explosions (tied to atmos), radiation. [S7, S8, D2, E2] Confidence: high.

**F10. Distribution and security.** The server serves the game build to the client (Automatic Client Zip), hence the sandbox (verifiable IL + an API allowlist). There is a launcher (macOS since May 2019), a central auth server, bans by IP/ID, a server hub (E16), delta updates. It pays off for the "anyone can host a fork" model, but is excessive for a single official game. [D5, S9, S15, S5] Confidence: high.

### C. The Godot episode

**F11. Godot 3 as render/audio/GUI/input, then abandonment.** They started because writing their own OpenGL renderer for 2D+3D was too complex (OpenGL drivers on Mac are terrible), and Godot 3 promised C# via Mono. Feb. 2019 — decision to write Clyde: frequent crashes (many related to Mono), hacks up to distributing custom Godot binaries, weak 2D performance (no sprite batching; about 30 FPS on Intel UHD 630 with lighting enabled), forward lighting for light, a weak UI theme and HiDPI. Clyde gave ~2.5x FPS on integrated graphics, but the comparison is against Godot without lighting (unlit) and against an early renderer made of plain quads; this is not an equal comparison. May 2019 — the Godot backend was removed because the dual backend slowed UI changes (S5). The author believes choosing Godot was not a mistake, but that Godot wants to do things "its own way" (S4). Important for evaluating Godot 4.7: the author himself notes that some crashes are related to Mono, which Godot 3 marked as unstable, and does not blame them on the engine (S4). On 3D: the SS3D model import test in Godot succeeded (S1), but there is no 3D code in SS14, and the author is not ready to write a 3D renderer on OpenGL (S4).
Caveat: this is Godot 3.0–3.1, a 2D game, 2018–2019. Godot 4.7 (Vulkan, .NET) cannot be considered identical; the criticism has not been verified on 4.x. [S1, S4, S5] Confidence: high (facts), low (applicability to 4.7).

### D. Rewriting systems

**F12. Subsystems and timelines (from devlogs, approximate).**
- Power: 2018 (watts/joules, the network computes consumption), 2020 "from scratch" (wireless APCs and areas removed), 2021 Pow3r (ramping, batteries). Three iterations in 3 years.
- Physics: rewritten June 2020, then a Box2D-like one in 2021 (the previous one was unstable); the broadphase port from Box2D (Feb. 2020) removed the problem of ~30% of client CPU being spent on finding the object under the mouse.
- Atmosphere: first version Oct. 2020 (zumos, inspired by monstermos), faster than LINDA/ZAS. 3.4 years after the restart. The number of gas slots is limited "for performance" (D13). A rework plan document exists, date not stated (E15).
- Shuttles/grids: a grid is an entity, the rest are stored relative to it (5000+ shuttle entities are not updated one by one); grid splitting takes ~250 ms.
- Explosions: raycast replaced by flood-fill (2022), the bottleneck is entity spawn/deletion.
- Body/medicine: the 2020 backend was "35% done", later rebuilt ("nubody"), the docs still say "in its infancy".
- Chemistry: started 2020, fixed-point reagents; construction — state graphs (D14, S12); AI — utility AI instead of FSM.
[S2, S6, S8, S11, S7, S9, S10, S14, D7, D8] Confidence: medium-high.

**F13. Performance is a problem even for 2D.** In 2022 the authors said: it works better than SS13, but with "many problems". Stutters due to expensive entity creation, event bus optimization (linear lists in one buffer instead of a HashSet per entity), a profiler was added. Maintainers noted performance as a barrier to player inflow. Client minimum requirements: 2 GB RAM, OpenGL 3.1+ (a typical 2D budget). [S16, E1, E12] Confidence: medium.

### E. Content, design, tools

**F14. Not parity, but reimagining.** Combat mode instead of intents; click by bounding box (later texture sampling) instead of pixel-perfect; APC without areas; maps are edited inside the game (a 2018 decision: not to write a separate editor) and stored in YAML, there is a merge driver for edit conflicts (S22); the RSI format (analogous to DMI) with a mandatory SPDX license on every sprite; Fluent localization as part of the strategy for attracting the Russian-speaking community. Principles: "mechanics over rules", mechanics are not tied to a specific place on the station, everything must be rebuildable. Early FAQ: ~5% of the content of an average SS13 codebase (no date). In 2021 the sprites were switched from CEV-Eris to tgstation (CC-BY-SA). The first mode is a temporary TTT-like one, until the systems matured. [S3, S5, S6, S12, D9, D10, E8] Confidence: high, "5%" low.

**F15. Tools.** ViewVariables, C# console, Toolshed, RSI editor with DMI import, YAML hot reload, Tools build for mapping, integration tests, benchmarks, dev wiki (HackMD → wiki.js → mdbook), PR standards, work groups. Some of the docs are marked outdated. [D12, S7, S6] Confidence: high.

### F. Community, license, sustainability

**F16. Community migration went through communities and forks, not through an "SS13 exodus".** Colonial Marines (on average ~200 players) held a stress test in Feb. 2020; the SS14 Discord almost doubled; there is now RMC-14, inspired by CMSS13 and SS13 (I did not check the link between the teams; ⚠️ per its README, RMC-14 is a game on Robust Toolbox with its own content pack, not necessarily a "fork of SS14"). The Russian-speaking community was the first to "take off" (a video by a major YouTuber, E7; S18 mentions YouTubers and other content creators and dates the surge to the end of 2021); there are many forks (Delta-V, Frontier, Einstein Engines; Corvax and SS220 — from search results, I did not open them). Steam: page since June 2020 (S7), Steam Playtest announced in Feb. 2021 (S22; ⚠️ it used to say "since late 2020", but a third-party tracker shows data from Dec. 2020, the discrepancy is unresolved); peak of 4,703 concurrent players in Apr. 2026 per the third-party tracker E9 (⚠️ the tracker is kept for the Playtest app, and its page is internally inconsistent: "updated 10 May 2026", but the table runs to September 2026; "confirmed by a forum reply" was not rechecked). The game is free. The thesis "SS13 players don't switch, SS14 is too soft" is only an opinion from Reddit, I do not use it as a fact. [S6, E7, E10, E9, E5, S9] Confidence: medium.

**F17. Licenses.** Content code is MIT. Engine: GPLv3 until 13 March 2019, MIT after; relicensing was agreed with each contributor by name. Assets are mostly CC-BY-SA 3.0, some CC-BY-NC-SA 3.0 (for commercial use they must be removed). The project rejects low-quality and "wholesale"-generated AI code, art and audio (README; only one-line autocompletions are allowed). Relicensing in legal.md is named for only one author (PJB3005): the rest of the engine code before 13.03.2019 remains GPLv3. Forks are advised to use the same MIT and are warned about the AGPL/MPL of other forks. tgstation: code after 31.12.2014 under AGPLv3, before that GPLv3; assets (icons, sounds) under CC BY-SA 3.0 unless stated otherwise; the TGS DMAPI under MIT (checked against the local README). The conclusion "code translation of DM makes the port an AGPL derivative" is a legal interpretation, not a fact from the sources. Not legal advice. [E3, E4, D11, E14] Confidence: high.

**F18. What they themselves call pain/mistakes.** The renderer changed three times (SFML, Godot, Clyde); in 2018 there were five sprite components, a "complete mess", which were merged; the netcode "worked because localhost is a perfect connection"; prediction was added after the logic already sat in the Server assembly, and the migrations take years; the connection handshake "grew over the years"; a forum participant (Apr. 2026, low) talks about old code that few want to rewrite; "The Curse" — announcements of remakes that vanish after screenshots (a 2016 essay, snippet only). There is no official post-mortem. [S18, S2, S6, D4, E6, E13] Confidence: medium.

**F19. Governance and sustainability in 2026.** On 20 Feb. 2026 a letter to the leadership was signed by 34 people, 4 of them anonymously (⚠️ it used to say "34 staff members"; the leader himself calls the signatories staff "of all levels"): no accountability, the keys to infrastructure and accounts are held by three "Wizards", there is no process for handing them over, a legal entity and work groups were proposed. Before that the project leader (PJB) had stepped away; on 2 May 2026 in his own post he announced a return to leadership. In the reply the demanded legal entity with bylaws as governance rules was rejected (called insulting and unrealistic), formal accountability mechanisms were rejected, but it was stated that a narrower nonprofit organization for finances and protection against a missing key holder would be created later; it was announced that all signatory staff members who did not subsequently apologize would be demoted (⚠️ it used to say "rejected some of the demands", which hid the harshness of the reply and the announced sanctions). This is the position of one side in its own post, not independently verified; the text of the letter is quoted in the same post. The leader himself notes that after his departure the project core, including the engine, was left without maintenance. By 27 July 2026 (the date of the S20 post; the source gives no date for the start of the outage, ⚠️ it used to say "were unavailable in July 2026") the official servers had returned after downtime during a provider change (the previous provider greatly delayed the delivery of hardware): the game, site, forum and authentication were on one host; an unofficial community server helped temporarily. There were no progress reports from March 2023 to July 2026; the team announced the formation of a legal organization. [E5, S20] Confidence: medium.

## Implications for the 3D port

1. **Take Engine / Shared / Server / Client.** Shared is a deterministic simulation without Godot types, suitable for rewind/replay. Take time, RNG and physics only from the core.
2. **Settle the "simulation / Godot" boundary before the first line of gameplay.** SS14 left Godot to control the loop, UI and rendering. For you Godot is only the view: meshes, light, sound, UI, input; core entities are displayed through proxies; Godot scenes hold no authoritative state. Do not keep two backends, do not fork Godot for hacks.
3. **Check Godot 4.7 with a prototype, not with 2019 reviews.** Things to check: C#/.NET 10 stability, node count and instantiation cost (SS14 suffered even in 2D), MultiMesh/instancing, lighting on macOS, HiDPI and UI themes. Render the station statics in chunks (a mesh per chunk), not a node per object. This is a conclusion by analogy, not a fact from SS14.
4. **Prediction in 3D is harder than in SS14.** The "rollback and replay" scheme requires physics to live in the simulation (SS14 built in Box2D-like physics). Godot physics cannot be rolled back, so you need your own kinematic controller and simple physics in the core, or giving up prediction for physical objects (a conclusion by analogy). Every system in Shared must be rewind-safe: no timer callbacks, RNG seeded per tick, deep copying of reference types. Predict the minimum (movement, hands, doors); SS14 does not predict atmos and power at all.
5. **Interest management from day one.** PVS in SS14 appeared after ~3 years and was redone twice. In 3D a 25x25 tile square does not work: you need rooms/decks/occlusion and protection of hidden information (antagonists, pockets). Copy the techniques: mind in a separate space, `SendOnlyToOwner`, session-specific components, overrides for cameras, the "value + rate" replication pattern. Test with artificial latency and a real remote server.
6. **ECS discipline right away.** Data in components, logic in systems, directed events, code generation for networked fields, text prototypes with inheritance, validation in CI, hot reload. Decide in advance how to map the tg typepath hierarchy (`/obj/item/...`) onto "parent prototype + components". In the local tg clone (HEAD of 2026-10-04) there are 7494 .dm files (of which 7476 in code/), 446 top-level entries in tgui/interfaces (401 files + 45 folders; recursively 881 files, 762 .tsx/.jsx), 1457 .dmi (1448 in icons/), 632 .dmm (⚠️ the numbers were recounted, previously only part of the .dm and .dmi files was given): data (sprites, maps, values) must be converted automatically, code written by hand.
7. **Rewrite algorithms, do not transliterate DM.** SS14 rewrote atmos (monstermos-like), power on an energy basis, chemistry with fixed-point, the body as organ entities, the construction graph, utility AI, flood-fill explosions. Each took 2–3 iterations and years. Rank the systems in advance and accept a core without parity. Atmosphere remains a cellular grid per deck and is not predicted. Z-levels do not appear in the SS14 materials found; in 3D multi-deck layouts affect atmos, PVS and physics.
8. **Calibrate schedules.** 3–6 volunteers spent ~3.4 years to reach "barebones" rounds with the first atmosphere (Oct. 2020; ⚠️ previously "to the first playable round": the game was called "playable" earlier, and the CM stress test took place in Feb. 2020, i.e. ~2.75 years after the restart) without 3D; the team size is an estimate (F3). You need a vertical slice: movement, hands, items, power, atmosphere, basic medicine, one antag. SS14 started with a temporary mode. Giving up parity (intents, areas, pixel hunting) was deliberate.
9. **Clicks and visibility in 3D.** SS14 solved pixel hunting with bounding boxes. The 3D analog: raycast against simplified colliders, highlighting of the candidate, a context list of objects under the cursor. Separate player visibility (FOV, what the server sees) from visual visibility (culling).
10. **Do not postpone tooling.** A text, git-friendly map format and merge driver, an in-game editor or .dmm import, SPDX metadata on every asset, integration tests that instantiate every prototype, a ViewVariables-like debugger, a console, a profiler. There are no 3D assets of compatible license at this scale; this is a separate risk (see the other reports in the series).
11. **Decide on the license and code provenance right away.** SS14 spent effort on the GPL→MIT switch. A faithful translation of tg code (DM -> C#) will make your code an AGPL derivative. With a commercial goal, record the provenance of assets and avoid NC.
12. **Do not copy the infrastructure wholesale.** The launcher, auth, hub, delta updates and code sandbox are needed by the "anyone can host a fork" model. For a single official game, Steam is enough. Operational lessons: do not keep the site, forum, auth and servers with one provider; create a legal entity and a key handover process earlier; localization (Fluent, UTF-8) from the first year.
13. **Community.** Growth came through specific communities (CM, Russian-speaking), public stress tests and open code, not through an "SS13 exodus". Plan playtests and an inflow of contributors.
14. **Alternative: reuse RobustToolbox.** MIT, .NET 10, ready-made networking/PVS/prediction/ECS/prototypes. But it is tailored to 2D (tile grid, Box2D, sprite layers) and runs its own window/render, so plugging in Godot as a 3D view is nontrivial. This is an option to evaluate, not a recommendation. OpenDream (running tg DM code on Robust, MIT, early stage) is useful as a "behavior oracle". [E11]

## Open questions

1. Is prediction needed, and for which systems? Is "server-authoritative + interpolation" acceptable, with prediction only for movement and hands?
2. Target load: 30, 100 or 250 players per round?
3. Level of parity with tg: an idea-level copy (like SS14), a playable core, or maximum content via automatic conversion?
4. Server mods and forks (content packs, sandbox, launcher, hub) or a single official game?
5. Own ECS in the core, an off-the-shelf ECS library, or parts of RobustToolbox?
6. Are multi-deck stations needed?
7. Target player platforms (Windows/Linux/macOS)?
8. Map editor: in-game, a Godot editor, or .dmm import?
9. License of the repository and assets; is porting tg code allowed (AGPL); policy on AI-generated code and art (SS14 rejects low-quality and "wholesale" AI contributions, and allows one-line autocompletions).
10. Accounts and servers: Steam accounts/lobbies or our own infrastructure; a legal entity from the very start?
11. Localization (Russian/English) and Unicode chat from day one?

## Gaps

- The WebSearch limit for the session is exhausted; Reddit is unavailable (including mirrors); wiki.spacestation14.com returns 530; the treeform essay on Medium returns 403 (snippet only). Details on Centration/TechCompliant/SS3D and "Curse" were not verified.
- There is no official SS14 post-mortem; the "mistakes" were collected from devlogs, docs and the forum.
- I did not find an explicit statement of why 3D was postponed: it is known that in 2017–2018 they planned 2D+3D, and in 2019 they bet on a 2D renderer.
- The duration and scope of the ECS migration (2020–2022) were not found, and no tracking issue was found.
- Team size and person-years are estimated indirectly from commit authors.
- Content parity in 2026 was not measured; the "5%" comes from an FAQ without a date.
- SS14 client performance (FPS/memory) for 2024–2026 was not found; the applicability of the Godot 3 criticism to Godot 4.7 was not verified.
- Steam data: a third-party tracker; the Steam page as returned by WebFetch shows "Coming soon (Early Access)", which does not agree with Playtest; not confirmed.
- The 2026 governance dispute was read from the first message of the thread and the leader's reply; the sides were not independently verified.
- Corvax/SS220/forks: search results only. The link between RMC-14 and the Colonial Marines team was not verified.

## Sources (actually opened)

devlog spacestation14.com:
- S1 https://spacestation14.com/post/18-02-26-progress-report-5/
- S2 https://spacestation14.com/post/18-06-24-progress-report-7/
- S3 https://spacestation14.com/post/18-09-19-progress-report-8/
- S4 https://spacestation14.com/post/19-02-20-progress-report-10/
- S5 https://spacestation14.com/post/19-05-12-progress-report-15/
- S6 https://spacestation14.com/post/20-02-24-progress-report-21/
- S7 https://spacestation14.com/post/20-06-07-progress-report-22/
- S8 https://spacestation14.com/post/20-07-22-progress-report-23/
- S9 https://spacestation14.com/post/20-10-16-progress-report-24/
- S10 https://spacestation14.com/post/21-05-07-progress-report-26/
- S11 https://spacestation14.com/post/21-10-01-progress-report-27/
- S12 https://spacestation14.com/post/21-11-24-progress-report-28/
- S13 https://spacestation14.com/post/22-04-01-progress-report-32/
- S14 https://spacestation14.com/post/22-04-30-progress-report-33/
- S15 https://spacestation14.com/post/22-05-25-progress-report-34/
- S16 https://spacestation14.com/post/22-07-27-progress-report-36/
- S17 https://spacestation14.com/post/23-03-06-progress-report-39/
- S18 https://spacestation14.com/about/about
- S19 https://spacestation14.com/post/25-01-19-10-years-of-space-station-14/
- S20 https://spacestation14.com/updates/26-07-27-wizden/
- S21 https://spacestation14.com/post/21-12-09-progress-report-29/
- S22 https://spacestation14.com/post/21-02-12-progress-report-25/

dev wiki (sources at https://raw.githubusercontent.com/space-wizards/docs/master/src/en/ ; rendered at docs.spacestation14.com/en/):
- D1 robust-toolbox/ecs.md
- D2 ss14-by-example/prediction-guide.md
- D3 ss14-by-example/basic-networking-and-you.md
- D4 robust-toolbox/netcode/net-entities.md, robust-toolbox/netcode/connection-sequence.md
- D5 robust-toolbox/sandboxing.md, robust-toolbox/acz.md
- D6 general-development/codebase-info/codebase-organization.md, general-development/codebase-info/releases.md
- D7 robust-toolbox/transform/grids.md
- D8 space-station-14/core-tech/body.md
- D9 specifications/robust-station-image.md
- D10 space-station-14/core-design/design-principles.md
- D11 general-development/tips/forking.md
- D12 space-station-14/mapping/guides/general-guide.md and ../SUMMARY.md (in src/)
- D13 space-station-14/departments/atmos.md
- D14 space-station-14/core-tech/construction.md

other:
- E1 https://docs.spacestation14.com/en/maintainer-meetings/maintainer-meeting-2022-06-25.html and ...-2022-07-16.html
- E2 https://github.com/space-wizards/space-station-14/issues/40655
- E3 https://github.com/space-wizards/space-station-14 (README, license)
- E4 https://raw.githubusercontent.com/space-wizards/RobustToolbox/master/legal.md , .../global.json , .../RELEASE-NOTES.md
- E5 https://forum.spacestation14.com/t/where-has-pjb-been/27331
- E6 https://forum.spacestation14.com/t/wizard-general-state/27111/2
- E7 https://forum.spacestation14.com/t/why-is-there-so-many-russian-servers/16165
- E8 https://forum.spacestation14.com/t/information-faq/2624
- E9 https://live-player-count.com/game/space-station-14-playtest
- E10 https://github.com/RMC-14/RMC-14
- E11 https://github.com/OpenDreamProject/OpenDream
- E12 https://store.steampowered.com/app/1255460/Space_Station_14/
- E13 https://medium.com/@treeform/space-station-13-remakes-9b985e2269da (search snippet only)
- E15 https://docs.spacestation14.com/en/space-station-14/departments/atmos/proposals/atmos-rework.html
- E16 https://docs.spacestation14.com/en/general-proposals/robusthub.html
- E14 ../tgstation/README.md (local, read-only)

## Verification

Date: 2026-10-05. Adversarial recheck: claims were re-checked against the primary sources (devlog and about pages downloaded in full and searched by text, forum.spacestation14.com, GitHub pages and raw files, live-player-count.com), and local numbers were recounted with commands on the tgstation clone. Corrections were applied in the body of the report; the ⚠️ mark means corrected or not confirmed. Verdicts: supported, partially_supported, unsupported, unverifiable.

| № | Claim | Verdict | What the check showed |
|---|---|---|---|
| 1 | F1: reasons for leaving BYOND (prediction, non-ASCII/0xFF, shaders, pixel-perfect, 4K scaling, closed nature, Wine/Linux) | partially_supported | Confirmed: prediction (S7), 0xFF/Windows-1251 (S5), shaders and pixel hunting (S2). Not confirmed and removed: "4K scaling" (in S5 this is a complaint about the Godot UI, while BYOND upscaling is praised there) and "closed nature, Wine/Linux" (these appear nowhere in the opened devlogs or in S18). |
| 2 | F2/Implication 8: "from the restart to the first playable round ~3.5 years" | partially_supported | The dates are right: Oct. 2020 "playable, albeit barebones" (S9) is ~3.4 years after May 2017. But S9 explicitly says the game had been "playable" earlier, and the CM stress test with ~200 players took place in Feb. 2020. The wording "first playable round" was corrected. Also S18 ("faded within half a year") and S19 ("within a year and a half") disagree, and so does the restart date: May 2017 (S18) versus "around the start of 2017" (S19). Not confirmed: nightly builds in Oct. 2017 and "Godot merged in Apr. 2018". |
| 3 | F2: Jan. 2015 — code opened; 2011 — closed remake by former Goonstation developers; "thousands of concurrent players" in Jan. 2025; in July 2022 520 merged PRs vs 326 for tg | supported | S18, S19, S16. The 520 and 326 are specifically over 30 days and exclude RobustToolbox. |
| 4 | F11: reasons for abandoning Godot 3 (Mono crashes, custom binaries, no sprite batching, forward lighting, ~30 FPS on an iGPU, weak UI theme/HiDPI, Clyde ~2.5x), "the choice was not a mistake", the SS3D test, unwillingness to write 3D on OpenGL | supported | S4, S5, S1 confirm this. Corrections: ~30 FPS was obtained with lighting enabled, while the 2.5x is relative to Godot without lighting; the author himself attributes many crashes to unstable Mono rather than to Godot (important for evaluating 4.7). The only reason for removal given in S5 is the UI layer that got in the way. |
| 5 | F7: netcode (~50 kB, 3 MB/s, 6x after change tracking; limit of 32 in Lidgren; copy-paste bug; localhost) | supported | S2 and S6 confirm it, matching the source closely in meaning. |
| 6 | F7: stress test ~250 players at 30 TPS, 20 TPS also fine, target 300 | partially_supported | Per S14: 200+ players at 30 TPS without slowdown, peak ~250 already at 20 TPS; the target of 300 players and the 110 players in a live round are from S13. Wording corrected. |
| 7 | F9: movement prediction in July 2020; "~80 systems still not predicted" (E2) | partially_supported | July 2020 is confirmed (S8, including the incompatibility of door timers). In E2 at the time of the check there are 104 items, 27 done and 77 not; "still" was too strong, as items are partly grouped. Confirmed: atmos and Pow3r are not predicted (E2, D2). |
| 8 | F4/F5/F6: RobustToolbox 292.0.0, .NET 10 SDK, ECS without logic in components, XChaCha20-Poly1305 instead of AES-CBC, PVS 25x25, prediction rules | supported | Version 292.0.0 is the latest release in RELEASE-NOTES (only Master is above it); global.json: SDK 10.0.100; ecs.md, S15 and prediction-guide confirm the rest. The handshake being "messy and grown over the years" is confirmed (D4). |
| 9 | F17: engine code GPLv3 until 13.03.2019, MIT after; content code MIT; assets mostly CC-BY-SA 3.0, some CC-BY-NC-SA 3.0; rejection of low-quality AI | supported | legal.md and the space-station-14 README. Clarified: only PJB3005 was relicensed to MIT by name; AI: low-effort/wholesale contributions are rejected, one-line autocompletions are acceptable (Open Questions previously said "forbids"). |
| 10 | F17: tgstation — AGPLv3 after 31.12.2014, GPLv3 before (local) | supported | Local tgstation README: commit 333c566b of 2014/12/31. Added: the tg assets are under CC BY-SA 3.0. The conclusion "code translation makes the port an AGPL derivative" is a legal interpretation, flagged as such. |
| 11 | F19: letter in Feb. 2026, "34 staff members", keys held by three Wizards, no handover process, "the leader rejected some of the demands" | partially_supported | The letter of 20.02.2026, 34 signatures (4 of them anonymous), keys held by PJB, DrSmugleaf, Zoldorf, and the absence of a handover process are confirmed. The leader's reply of 02.05.2026 was described too mildly: the legal entity in the proposed form and formal accountability were rejected, the demotion of signatories who did not apologize and a return to leadership were announced; a narrower nonprofit was promised for the future. All details come from one side's post. |
| 12 | F19: infrastructure outage in July 2026, game, site, forum, auth on one host; unofficial server; no progress reports since 6.03.2023; a legal entity is being formed | supported | S20 (post of 27.07.2026). Clarified: the source gives no date for the start of the downtime, only that the servers had been restored by 27 July. |
| 13 | F3: headcount and contribution (20 contributors in Feb. 2020; 315 contributions and ~700 commits from 29.09 to 3.11.2021; 773 PRs from "80+" people) | partially_supported | The first two are confirmed (S6, S12). In S17 the 773 PRs were made over Nov.–Dec. 2022 by 75 authors, not 80+; corrected. |
| 14 | F16: Steam peak ~4.7k in Apr. 2026; Playtest "since late 2020"; growth through the Russian-speaking community; RMC-14 | partially_supported | Tracker E9 shows 4,703 (Apr. 2026), but the tracker data is internally contradictory (updated 10.05.2026, table through September). The Playtest announcement in S22 is from Feb. 2021. The Russian-speaking surge is confirmed (E7, S18, end of 2021). RMC-14 per its README is a game on Robust Toolbox with a content pack; "fork of SS14" is not confirmed. |
| 15 | Local tgstation numbers (HEAD 2026-10-04): .dm, tgui interfaces, .dmi, .dmm | partially_supported | Recount: .dm 7494 in total (7476 in code/ only), .dmi 1457 in total (1448 in icons/ only), .dmm 632 (correct), tgui/interfaces — 446 top-level entries (401 files + 45 folders; recursively 881 files) correct. |
| 16 | F12/F13: timelines of power/physics/body/chemistry, client minimum requirements, Pow3r, profiler, etc. | unverifiable | Not rechecked in this pass, except: watts/joules (S2), 30% CPU on finding the object under the mouse and the broadphase port from Box2D (S6), 520/326 and expensive entity creation/event bus (S16), gas slots limited for performance with no number given (D13), five sprite components in 2018 (S2). |

Overall assessment: the facts on chronology, licenses, architecture and technology (netcode, prediction, ECS, Godot) are mostly confirmed by the primary sources. Errors found: two unconfirmed items in the list of reasons for leaving BYOND, the exaggeration "first playable round", a distortion of the stress test conditions, inaccurate numbers (80+ systems, 80+ contributors, 7476/1448 as totals), a softened description of the leader's reply, imprecise dating of Playtest and of the infrastructure outage. The conclusions for the 3D port (the Implications section) are analogies and the author's recommendations, not facts from the sources; the only numerical claims in it were rechecked.
