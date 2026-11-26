# M10 Multiplayer maturity and round loop: design and plan

Written 2026-10-24 against main (0.9.0, protocol 41). tg = `../tgstation/code` (snapshot 1d99bd7f). `Tg/` = `src/Core/Tg/`. The scope is
ROADMAP M10 (D15, D23, D37-D40, D73, D76, D103). New choices start at D266, deviations at DV174, not-translated sections at 78, the
protocol at 42. The owner's answers are in section 3 (D266-D269).

## 0. What already exists (verified)

- **Replication** (`src/Server/Replication/Replicator.cs`, `docs/ai/specs/replication.md`): hints, shadows and a sweep build ONE delta body a
  tick, encoded once for every session; each session adds its private block. Every field already carries the tick it last changed
  ("the per-client baselines of M10", replication.md 3.5, 12). Interest filtering is a stub: everyone receives every entity on the map.
  The one per-viewer filter is the observer layer (D151, replication.md 17): entities above `SEE_INVISIBLE_LIVING` go to each session by
  its mob's `see_invisible`, walked per session. Deltas travel on the reliable ordered channel.
- **The network** (`src/Net`): LiteNetLib and a loopback transport whose latency, loss and reorder are seeded (`LoopbackTransport`
  `MinLatencySteps`/`MaxLatencySteps`, loss). LiteNetLib's own simulation is off in its NuGet build, so latency tests run on the loopback.
- **Joining** (`ServerHost.World.ApplyJoin`): "the M10 lobby stand-in": every join spawns a human at the next spawn point, names it
  (`CrewStandIn.Name`) and dresses it as an assistant (`CrewStandIn.Equip`). A reconnect finds the mob that kept its key (tg's SSD) and
  makes nothing. There is no lobby, no ready state, no round start or end, no restart.
- **The round subsystems**: `SSticker` is an inert stub (`controllers/subsystem/ticker.cs`, 74 lines: `start_at`, `round_start_time`);
  `SSjob` has `setup_occupations` and the job lists (M4a), `JoinPlayerHere` and the RETA grants are not translated; the job datums and
  every station outfit equip whole (`MedHudGameplayTests`). Meta has 6 `landmark/start/station_engineer`.
- **Shuttles**: none. Only the docking ports' singularity procs exist (`modules/shuttle/shuttle.cs`). Meta's stationary ports name
  their round-start templates (the arrivals `shuttle/arrival/box`, cargo, mining, labour, the ferry); the port loads none, so Meta's
  arrivals, cargo and evac bays are empty. tg sends a late joiner to the arrivals shuttle's chairs (`job.dm:751`
  `send_to_late_join`, `get_last_resort_spawn_points`: the `area/shuttle/arrival`), and a round ends only when the emergency shuttle
  reaches `SHUTTLE_ENDGAME` (`ticker.dm:195` `check_finished`; the nuke and the revolution are antagonists). No communications console.
- **Admin**: the server console has `kick` (tg's `boot2`, `ServerHost.Console.ApplyKick`), `list`, `status`, `teleport`, `spawn`, the
  dev commands; `log_admin` exists. No ban, no notes, no SQLite, no admin ranks (the host's console is the only authority).
- **Prediction**: the own capsule (D103, `godot/scripts/Shared/Play/OwnPrediction.cs`); predicting bots (`--predict`) count corrections.
  Hands and doors wait for the server.
- **Bots and soak** (`tools/Anomaly.Tools.Bots`, `tools/soak/run.sh`): wander, rest, shove, fiddle, churn, reconnect; JSON reports with tick
  and RTT percentiles. The last recorded soaks are M2's on the toy room (p99 5.7 ms, 150 bots 10.9 ms). **No bot run on MetaStation with
  the M4-M9 systems has been measured**, so the 30 ms p99 target's headroom is unknown.
- **Sizes in tg**: `ticker.dm` 882, `job.dm` 1010, `new_player/*` 1.4k, `lobby_menu` 0.4k (HTML), `latejoin_menu.dm` 222 (tgui
  `JobSelection`), `outfit.dm` 532 (ported), `__HELPERS/roundend.dm` 770, `preferences.dm` 596 (plus the preference datums and the tgui
  `PreferencesMenu`), `sql_ban_system.dm` 1.1k, `sql_message_system.dm` 0.8k, `IsBanned.dm` 0.3k, `admin_ranks.dm` 0.4k, `holder2.dm`
  0.5k, `communications.dm` 1k, `modules/shuttle` 7k, `CameraConsole` (tgui) with `camera.dm`.

## 1. Slices (in order; each is a branch, reviewed, tested and merged before the next)

Hours are my working time, rough; the owner's questions (section 3) change B2, F2, G and the soak lengths.

**A. Baseline on MetaStation (2-3 h; done, section 4).** Bots on Meta (16, 50, 100, 150) with the current single body: tick p50/p99/max, the subsystems'
share (`StepProfiler`), bytes per player per second, join time, memory, Meta's boot time (the restart's cost). Short runs (D102).
Output: a table in this file and the optimisation budget for J. No code beyond what the bots need to walk Meta.

**B. Per-client baselines and unreliable deltas (8-12 h, protocol 42; done as measured, section 5).** replication.md 12 as planned: a client acknowledges the ticks it
applied; its delta is built from the shadows' stamps since its last acknowledged tick (spawns and despawns since), so a lost delta is
covered by the next one and never resent. Deltas move to the unreliable sequenced channel with our own splitting at the MTU; joins,
chat, tgui and the private tables' new entries stay reliable. Sessions with the same acknowledged tick share an encoded body (the cost
at 100 players stays near today's). Tests: the loopback at 100 ms and 2% loss, a replica identical to the server's after the losses.

**C. Interest management (10-14 h, protocol bump if C changes the wire; done, section 6, no wire change).** D23 and D37: each session's interest set is the union of its
eyes' sets; an eye's set is chunks (8x8 tiles) within a radius whose tiles the eye sees by line of sight over the opacity grid (the
server's own `view`), plus the room (area) the eye stands in, with a margin and hysteresis so nothing pops at the edge. Entering the set
is a spawn for that client, leaving it a despawn after the hysteresis. The static map (cells) stays whole (D23). Sounds, hearing and
radio do not depend on it (D23). The `see_invisible` layer (D151) folds into the same filter. Client: entities leaving fade rather than
vanish. Tests: tg's D40 "no hidden data" check (a client behind a wall never receives the entity, its name or its appearance); the
bandwidth guard at 150 movers drops to the players in view; a walk through Meta's corridors shows no pop-in (a gameplay test and the
owner's look).

**D. Eyes (4-6 h; 10-14 h with the camera console, question 3; done, section 7).** D23's eye model: a session has a list of eyes (the mob, the ghost,
`client.eye` when tg moves it: `reset_perspective`, remote views), each with its own interest set; the camera follows `client.eye`.
Question 3: tg's security camera console (`CameraConsole`, `camera.dm`, the `cam_screen` map view) as the second real eye, drawn in a
second 3D viewport in its window.

**E. Prediction stage 1: hands and doors (10-14 h).** D38 stage 1. The client predicts, keyed by its input sequence: swapping hands,
the active hand, picking an item up from within reach, dropping, putting on and taking off through the HUD slots, throw mode; an airlock
the player's ID opens (the client receives its own access list privately) starts opening at the bump or click, with tg's timings, and
its cell becomes passable for the own capsule. The server's acknowledgement of that sequence confirms or overrides each prediction
(a refused door closes again, a refused pick-up returns the item). Tests: the loopback at 100 ms and 2% loss with predicting bots;
the walking correction measured (the done-when "no correction larger than a few centimetres", tuned with the owner, D64).

**F. The ticker, the lobby and spawning (12-16 h; +10-14 h for tg's whole preferences menu, question 2; done, section 9, no protocol change).**
- F1: `SSticker` translated for M10's states (`GAME_STATE_STARTUP`, `PREGAME`, `SETTING_UP`, `PLAYING`, `FINISHED`): the pregame
  countdown (`start_at`, tg's 120 s; config), the ready count, `setup`, `create_characters`, `equip_characters`, `transfer_characters`,
  the round-start announcement, the round's start time. `/mob/dead/new_player` with tg's lobby menu (`modules/lobby_menu`, its HTML in
  our browser window: Ready, Join Game, Observe hidden by D151's scope, Setup Character, the countdown), the title screen and the lobby
  music.
- F2: the character: question 2.
- F3: `SSjob` round-start assignment with one joinable job, the station engineer (`divide_occupations` with the other jobs closed, the
  overflow role the engineer), `JoinPlayerHere` on the engineer's start landmarks, `equip_rank` with tg's engineer outfit and its ID
  trim; late join: tg's `JobSelection` window and `AttemptLateSpawn`, `send_to_late_join` (the arrivals shuttle's chairs: needs slice
  G's round-start templates; until then tg's last resort, documented). The M2 stand-in (`CrewStandIn`) goes.
- Reconnect keeps working: a key with a living body goes back to it (tg's SSD), in the lobby a key goes back to its lobby mob.

**G. Shuttles (question 1: 3-5 h minimal, 12-16 h docked, 24-32 h full).** The round's end in tg is the emergency shuttle.
Recommended "docked": SSshuttle's template loading for the stationary ports' round-start templates (the arrivals with its chairs, the
cargo, the mining, the labour shuttles stand at Meta's docks as tg's do, not moving); tg's communications console (`communications.dm`,
the tgui `CommunicationsConsole`: call and recall the shuttle, the messages; its security level parts as far as the core version has
them), `requestEvac`, tg's timers and announcements, the auto-call (`CheckAutoEvac`); the emergency shuttle's template appears in Meta's
evac bay when it docks, and when its docked time runs out it leaves (the bay is emptied) and the round ends (`SHUTTLE_ENDGAME`): no
transit space, no CentCom, no moving shuttle (DV). "Full" adds the ride: the shuttle's turfs move into transit and to CentCom with the
people on board (`modules/shuttle`'s move code, CentCom's map). "Minimal": no shuttles; late joiners at tg's last resort; the round ends
by tg's restart vote (`datums/votes/restart_vote.dm`) or the admin command.

**G2. The heads of staff (6-10 h; D273, section 11).** The command jobs open in `OPEN_JOBS`: the six heads' job datums and outfits,
tg's captaincy (the round start's spare ID candidates by the chain of command, the late joiner's captaincy, `promote_to_captain`, the
new head's AAS announcement) and the captain's spare ID safe on the bridge (`secure_safe`, `lockable_storage`, tg's LockedSafe window).

**H. The round end and a clean restart (10-14 h, protocol bump).** `declare_completion` with tg's round-end report (`roundend.dm`: the
round's length, the survivors and the dead, the escapees as far as G has them; the antagonist parts are the alpha's), tg's end screen
and the reboot countdown (`reboot_hud`), then the restart: the server builds a fresh world from the map in-process (D15, no state kept),
the connections stay, every client clears its replica and receives a fresh join into the new round's lobby. Bots and players reconnect
transparently. Tests: two rounds back to back in one server process, the second's state hash at its start equal to a fresh boot's.

**I. Admin tools (6-9 h).** D73, D76: an embedded SQLite (`Microsoft.Data.Sqlite`, MIT) with schema migrations; tg's admin ranks
(`admins.txt` and `admin_ranks.txt` as tg's legacy text files; the host's console is always an admin); bans from tg's
`sql_ban_system.dm` (by key and by IP, temporary and permanent, server-wide; the ban check at connect with tg's messages, `IsBanned.dm`),
unban; notes from `sql_message_system.dm` (add, list, delete; the note shown to admins when the player connects); the round id
(`SSdbcore` round numbers) for the logs. The commands work from the server console and, for the keys with admin rights, from the
in-game console (D40: the same typed commands with authorisation). tg's ban and note panels are HTML; a console form is enough for
the core version unless the owner asks for the panels.

**J. Bots and load (8-12 h plus optimisation as A measures).** The bots go through the lobby (ready up, late join), play on Meta, ride the
restart; the soak on Meta with 100 bots and the 150-bot validation (D39); whatever A and the soak show is optimised until the p99 tick is
under 30 ms (PLAN 2.4) or the owner is asked (ROADMAP risk table: optimise, cut replication, relax the target).

**K. The closing (4-6 h).** The demo check: a round with 8-16 bots and two clients from the lobby to the restart on MetaStation without a
crash and without runtimes beyond the boot's; the owner's checklist (`docs/ai/C6-checklist.md`, the core version's playtest, ROADMAP 8's
"C3" checkpoint); the sites still labelled M10 re-tagged (not-translated.md); the full suites; release 1.0.0-pre-alpha (D280; planned as 0.10.0).

**Total**: about 95-125 h with the minimal options; about 110-150 h with the recommended ones (docked shuttles, the camera console, a
simple character setup).

## 2. Left out (ROADMAP M10)

Other roles than the station engineer, antagonists, ghosts beyond D151 (so no lobby Observe), rollback beyond stage 1 (D38 stages 2-3),
voice. Recorded in not-translated.md as each slice meets them.

## 3. The owner's answers (2026-10-24)

1. **How a round ends (G): docked shuttles** (D266).
2. **The character in the lobby (F2): tg's random character with the name, gender and age editable** (D267).
3. **The second eye (D): the eye model plus tg's security camera console** (D268).
4. **Long runs: 15 minutes at most during M10; one one-hour 100-bot soak and one 1-2 hour round at its end** (D269).

With these the total is about 110-150 h.

## 4. Slice A: the baseline on MetaStation (2026-10-24, done)

`tools/soak/run.sh --bots N --minutes 3 --godot 0 --server-args "--map metastation"` (16 bots for 2 minutes), mixed behaviour (wander,
rest, shove), one bot process, Release, on the development Mac (10 cores, other loads running). "Steady" is every tick from 20 s after
the last bot joined; the join burst before it is listed apart. Times in ms; bytes are protocol payload down per bot.

| Bots | Tick p50 | Tick p99 | Tick max | Step p99 | Replication p50 / p99 | Send p99 | Down per bot | Gen2 GCs | Heap |
|---|---|---|---|---|---|---|---|---|---|
| 16 | 4.1 | 10.3 | 22.8 | 6.9 | 2.6 / 4.5 | 0.7 | 12.0 KB/s | 0 | 754 MB |
| 50 | 4.2 | 9.9 | 14.0 | 6.8 | 1.8 / 4.9 | 1.3 | 16.7 KB/s | 0 | 827 MB |
| 100 | 8.3 | 16.9 | 47.2 | 12.4 | 2.0 / 5.6 | 4.7 | 25.1 KB/s | 0 | 951 MB |
| 150 | 10.9 | 21.3 | 82.2 | 15.3 | 2.1 / 6.9 | 6.9 | 32.6 KB/s | 0 | 853 MB |

- **The target holds today in the steady state**: p99 16.9 ms at 100 bots and 21.3 ms at 150, against 30 ms (PLAN 2.4). The whole-run
  p99 (36 ms at 100) comes from the join burst: a join costs 8-10 ms of the tick (a new human, `client/New`, the outfit), the first one
  about 250 ms (JIT), and up to five land in one tick. M10's round start creates every character in `SSticker.setup` at once, so slice
  F measures that tick on its own (tg's round start stalls too).
- **SSmobs** (DV3, every living mob's `Life` in one tick): p99 16.8 ms at 100 bots in the steady state, the largest single cost in the step.
- **Bandwidth** is linear in the movers (everyone sees everyone): 25 KB/s per player at 100, 33 at 150. Slice C cuts it to the players
  in view.
- **Boot**: about 14 s (atoms 6.5-7.3 s, the map 4.8-5.4 s, lighting 0.5 s), 2.2 s of it GC pauses; heap about 650 MB after the boot,
  750-950 MB with the players. Slice H's restart must keep the old world from doubling the memory and must not stall the connections
  for those 14 s (the clients see tg's "rebooting" state meanwhile).
- **The bots' own process** was late in a third of its iterations at 100 bots and in more than half at 150 (each bot decodes every
  mover), so the server's load at 100+ is somewhat understated; slice J runs the bots in several processes.
- The boot's two runtimes are the known not-translated sites (`_organ.dm:102` visual organs, `areas.dm:228` the monitored areas of
  cameras: the latter is slice D's); no runtime, desync or decode error during the runs.

The optimisation budget for J is therefore the join and round-start burst and SSmobs, not the steady tick; J re-measures after C and E.

## 5. Slice B: what the losses do and the Motion message (2026-10-24, done)

Measured first (`Integration/LossyNetworkTests`: the test room, one player walking and turning, one standing, 100 ms each way, 2% loss,
a retransmission after one round trip as LiteNetLib's):

| | Own walking | The Delta in the other's replica | The walker's position in the other's replica |
|---|---|---|---|
| Before (all reliable) | 0 corrections, 0 cm | 28 waits over 100 ms a minute, up to 350-400 ms | the same waits (it was in the Delta) |
| After (version 42) | 0 corrections, 0 cm | 26 waits, up to 400 ms (unchanged) | no wait over 100 ms (the 100 ms render delay covers it) |

- **Own walking was already exact** under loss: the predictor reconciles by input sequence, so a late Delta delays the check without
  moving the capsule. D38's reconciliation criterion holds without changing the channel.
- **What the losses froze was everyone else**: the reliable channel's head-of-line wait stops the other players' positions for longer
  than the client's render delay. So only the capsule mobs' positions and yaws moved to the unreliable channel, resent for 20 ticks after
  their last change (protocol.md section 51), instead of rebuilding the whole Delta on acknowledged baselines (replication.md 12's plan):
  doors, items and the tables still wait on a loss, which a player sees as a door that opens up to 400 ms late at 2% loss.
- The per-client part that interest management needs (each session its own entities) is slice C's, on the reliable channel; the Motion
  messages are already built per session and take the same filter.
- Cost on Meta (100 bots, 3 minutes, as section 4): tick p50 7.2 ms, p99 13.2 ms (16.9 before, within the runs' noise), replication p99
  4.5 ms, 26.4 KB/s down per bot (25.1 before: the resends after a mob stops and the messages' own headers, about 5%); no desync.

## 6. Slice C: interest management (2026-10-24, done)

`src/Server/Replication/Replicator.Interest.cs`, on with `ReplicatorOptions.Interest` (the server's default; `--no-interest` turns it
off, and the replicator's unit-test harness keeps it off so the shared body's tests still read every entity there).

- **The wire does not change.** With interest on, the shared Delta body carries no entity (the tables and the map's cells only); every
  entity goes through each session's private block, in the spawn, change and despawn lists the observer layer has used since version 22
  (replication.md 17). A Snapshot lists no entity and its block spawns what the joining client wants. The Motion messages (slice B) name
  only the entities the client holds.
- **What a session wants** (its eye: tg's `client.eye`, else the mob; D's several eyes join here):
  - the chunks (8 by 8 tiles) next to the eye's chunk, `NearChunks` (1: the 3 by 3 chunks, at least 8 tiles each way), whatever the
    walls, so nothing pops in round a corner close by;
  - within `FarTiles` (64 tiles, 128 m: past the client's medium view distance of 100 m) the entities of the rooms the eye sees: tg's
    areas, linked where two tiles of different areas lie side by side and neither is opaque (a window, an open door, a glass airlock),
    up to `RoomDepth` (2) links away; a door's opening runs while the room beyond arrives, so it does not pop in either;
  - within `FarTiles` every light source that is not a mob (the client lights the map from them, D24);
  - within `FarTiles` every door (the cell-polled types: airlocks, firelocks, windoors, mineral doors, flaps; the owner, 2026-10-26: seen
    far down a corridor, a door appeared only as he walked up to it);
  - the own mob always, and a layer entity (a ghost) only within the mob's `see_invisible`, as before; a flick visual with its host.
  An entity it stops wanting is despawned after `HoldTicks` (40, 2 s).
- **The rooms' links** are counted per pair of areas from the opacity bit the cells already keep (`CellFlags.Opaque`): a door that opens
  or closes, a wall that falls, adds or removes its pairs, and a session recomputes its rooms when the links change.
- **Indexes**: the entities by chunk and by area, kept on spawn, move and despawn; a session checks the entities of the chunks and rooms
  that entered or left its sets, plus the tick's spawns and moves, so the per-tick cost follows the changes, not the map.
- **Measured** (`Integration/InterestGameplayTests`, MetaStation): a client in a corridor holds 5,764 of the map's 20,829 entities;
  Bob in a room it does not see, beyond the near chunks, never reaches it, not even walking (D40), and a light fixture behind the walls
  does; an opaque airlock keeps the room beyond out, opened it lets the room and Bob in, closed again they leave after the hold. 100
  bots on Meta (as section 4): 13.4 KB/s down per bot (26.4 with everyone seeing everyone; the bots crowd from a few spawn points, so
  spread players save more), tick p99 16.2 ms (the per-session filter moved part of the cost into the send: p50 3.3 ms).
- **Known limits**: the client's high preset draws to 200 m, beyond the 128 m far radius, so what leaves the radius there vanishes at
  that distance (`FarTiles` is a setting; a fade on the client was not needed at the default presets); an area changed in place (tg's
  area editing, rare) keeps its old tiles until the map's cells are rebuilt.
- **The client's hitches** (the owner, 2026-10-26; measured with `ANOMALY_FRAMEPROBE` on Meta, a noclip walk across the station): the
  interest moved the building of the map's things from the join into play, and a step into a new part of the map stalled the
  frame. Three causes, three fixes: (1) a wall fixture's first sight prepared its whole model family at once, about 70 textures
  decoded at 45 ms each (3.3 s): the family's warm-up now decodes on the thread pool and builds within 3 ms a frame
  (`ModelLibrary.StepWarmUp`), and a part's mesh no longer depends on the tint; (2) the 400-700 things a step brings were built in
  one frame (60-280 ms): they are built over the next frames within 4 ms a frame, nearest first (`EntityLayer.AddPending`; mobs and
  visuals at once); (3) an animated thing's picture set in place on a chunk just refilled made Godot read the MultiMesh buffer back
  from the GPU (35 ms): in-place changes go to the batch's own copy and are uploaded once a frame (`ChunkBatch.UploadPending`). After:
  no frame above 80 ms but the first; a thing whose own model texture is new still costs its decode (20-60 ms, once per texture).
- **The join's loading** (D352, the owner 2026-11-25; DV597; measured on Meta without the lobby, RTX 4080, `ANOMALY_FRAMEPROBE_MS=33`):
  before, `MapRenderer.Build` ran inside `_Ready` for 5.2 s (10 s on the owner's machine) behind a frozen window while the server had
  already late-joined the character, and the first frames after it took 462 ms and then 44-82 ms. Now the client preloads at its start
  (`AssetPreload`: 3,789 sounds, 105 MB; 998 models with their 160 distinct textures, 117 MB decoded, a kit's copies of one atlas shared;
  54 MB of sheet bytes; 0.5-3.7 s on worker threads). The game scene shows `Hud/LoadingCover` and builds the map in steps of 40 ms
  (`MapRenderer.BuildSteps`, 3.4-3.7 s over about 70 frames), decodes the round's sheets and `IconPrefetch`'s common folders ahead
  (about 930 sheets, 646 MB; none over 32 MB but the combat list's), warms every shader and effect up (`ShaderWarmUp`: surface pipelines
  35 to 133, draw 8 to 67, specialization 14 to 90), then sends `Loaded` (protocol 97): the server late-joins the body only then, and the
  cover goes once the things it streams around the body have about stopped coming (about 1.1 s after the build). Under the cover a few
  frames still take 0.5-0.8 s (the scene's making, its first full frame). After the reveal the worst of the first 60 frames is 127 ms,
  and the miss log (`preload miss: ...`) names 5 first-time sheet loads under 1 ms each (564 with `ANOMALY_PRELOAD=0`), no model and no
  sound. The 130 s walk: p50 16.7 ms, p95 25.3 (33 before), p99 39.6 (42), worst 78 (108), 29 frames over 50 ms (37), none over 100.
  The cost is memory: the working set at the reveal is 3.6 GB against 2.2 GB with the preload off, mostly the sheets decoded ahead. Left:
  the engine-side stalls (45-55 ms with no script time) and the streaming costs (`map/atlas.Flush`, `map/RebuildTurfs`).
- **The streaming hitches** (the owner, 2026-11-26: FPS fell to 10-15 while flying into parts of the station not seen before, and
  not on the way back): measured with Tracy (`tools/perf/fly.sh`, docs/testing.md "Profiling"), a noclip flight at the normal speed
  along row 127 from the west edge to the east and back, Meta without the lobby, RTX 4080. The display's refresh turns a frame of
  20-30 ms into 33 or 50 ms, so the slow frames come in runs at 30 or 20 FPS. Found and changed: (1) the prop atlas uploaded a whole
  2048 x 2048 layer (16 MB) for each new picture, 7-14 ms a frame while things arrived, about 1 s of the first leg's slow frames: the
  atlas is the RenderingDevice's own texture array and a flush uploads the shelves' new rectangles alone, copied into place on the GPU
  (`PropAtlas.FlushRects`; Godot's `Texture2DArray` updates whole layers only); (2) a door or a window that arrived rebuilt its
  neighbours' models at once, inside the message or the tick's scan (frames of 20-50 ms): `Replace` is queued and drawn within the
  warm-up's budget (`StepReplace`); (3) turf chunks were rebuilt four a frame (20 ms) and each made a new occluder, which makes Godot
  build its occlusion culling's scene again: a time budget, and the occluder is kept while the chunk's walls are the same; (4) the
  runtime runs in `SustainedLowLatency`. Standing still the render passes of 35-45 ms every 4.8 s are gone (2 in 45 s). First leg,
  with vsync: frames over 33 ms 379 to 287, over 50 ms 33 to 23, p99 47 to 44 ms; the game's own time in the slow frames 4.3 s to
  1.7 s (atlas 1.06 s to 0, model builds 0.58 to 0.19 s, spawns 0.76 to 0.24 s). Then, profiled inside the engine (a Godot built
  with Tracy, `tools/perf/godot-tracy.patch`): the clusters of four render passes of 35-50 ms about every 5 s were Godot preparing
  every glass mesh instance again (`_update_dirty_geometry_instances`, 886 instances at first and 9,000 once the station was
  seen, so it grew with the flight, and twice a time). The own mob's appearance turns to a private one and back about every 4.6 s
  (a server-side thing not looked into), and each turn set the reflection's four `anomaly_self_*` globals to new textures: Godot
  makes a new uniform set for each material that reads a global texture set anew, and that marks every instance drawn with it
  dirty. The four textures are now made once at 128 x 128 and their pixels updated (`EntityLayer.PublishSelfReflection`); the prop
  atlas's metrics texture, made anew every 1,024 pictures with the same cost, starts at 16 rows and doubles. Full flight, vsync
  off: frames over 50 ms 33 and 46 (first leg and back) to 3 and 2, p99 47 and 51 ms to 31 and 30. A directional window that
  left redrew the panes around it at once, each pane once for every neighbour that left (1,158 model builds, a frame of 100 ms on
  the way back): `RedrawBorderWindowsAround` queues them for `StepReplace` as `Rejoin` does. With the stock engine and vsync, as
  played: frames over 33 ms 379 and 206 to 33 and 12, over 50 ms 33 and 44 to 3 and 0, p99 47 and 49 ms to 32 and 29. Left are
  the game's own costs: a thing's first model build (8-35 ms), a first spawn of a type (11-17 ms, some of it the code's first
  run), `map/Scan` walking every static record each server tick (2.6-6 ms, 21 times a second) and the turf rebuilds (up to 27 ms).
- **The spawn queue and the leave margins** (performance.md P3.7, P3.8, 2026-10-29): what the sets bring in is queued and spawned
  nearest the eye first, `SpawnBudget` (150) a tick, `JoinSpawnBudget` (1,000) in the join's Snapshot; the own mob, the mobs, the
  doors of the near chunks, the visuals and what the world made this tick skip the queue, and what lies on a spawned door's tile comes
  with it (the client draws a door across a window or under shutters from its tile, `MapRenderer.AcrossGlass`). The sets are left
  past a margin: a near chunk at `NearChunks` + `NearLeaveChunks` (the 5 by 5), a far light or door at `FarTiles` + `FarLeaveTiles`
  (72 tiles), and a room the eye no longer sees (its door shut) keeps what the client holds of it, but its mobs, for `RoomHoldTicks`
  (100, 5 s), so walking to and fro over a border or through a door's cycle does not send the same things again. What a hold lets
  go is despawned `DespawnBudget` (300) a tick, the rest staying held for the next ticks: a room set left whole was 7,000 despawns in
  one Delta, 119 ms of the client's frame (each window's neighbours redrawn). What the world deletes leaves at once. Measured on Meta (`InterestGameplayTests`): the teleport from the arrivals to the centre sent
  4,843 entities (188 KB) in one tick, now 268 (9.8 KB) and the rest over 32 ticks with no Delta above 7.8 KB; the join's Snapshot
  held 5,307 entities in 422 KB, now 1,029 in 271 KB with the rest over 30 ticks. The tests' rig (`RealHeadlessWorld.Join`) waits
  until the queue is empty.

## 7. Slice D: the eyes and the camera console (2026-10-24, done)

- **The eyes** (D23): a session's interest is the union of its eyes: tg's `client.eye` (the mob, or what `reset_perspective` gave it:
  the ghost, a locker, a mech) with its chunks and rooms (section 6), and every map view on the client's screen: the turfs in its
  `vis_contents` (tg's way of showing a second map: `atom/movable/screen/map_view`, registered with `client.register_map_obj`), whose
  entities are wanted wherever the main eye is. The camera console is the first map view; the advanced console, the NtOS camera app and
  the gateway use the same path when they come.
- **The camera console** (D268): tg's `computer/camera.dm` with SScameras' lists and the camera's `camera.dm` procs it needs
  (not-translated.md 78). tg's window, `CameraConsole.tsx`, translated (the search, the list, the previous and next cameras, the notice);
  its ByondUi map is a `UiKind.ByondUi` node the client draws as `TguiMapView`: a SubViewport sharing the game's 3D world, its camera at the
  active camera's entity (found by the REF in the window's data, `[0x...]` naming the entity's id), near the wall the camera hangs on,
  under the ceiling, looking into the room and tilted down (`CameraViewPose`); no camera, or a disabled one, shows tg's static as a dark
  screen. The view is kept across the window's rebuilds (tgui re-renders every 0.9 s).
- **DV44, widened**: the native windows never send tgui's `visible` message; the server's window answers it at once with `ready`, so tg's
  `map_view/display_to` (which waits for it) registers the map.
- **Tests**: `Integration/CameraConsoleGameplayTests` (MetaStation: 283 cameras, 5 off at the round start; the console's window lists 248
  named cameras sorted as tg's; Alice picks one 40+ tiles away, the window names it and its map is drawn from its REF; Bob in front of it
  reaches her client through the camera's view and leaves it when she closes the window); `Client/CameraConsoleTests` (tg's previous and
  next cameras, the search and sort, the view's pose). The 3D view itself is the owner's look (C6).

## 8. Slice E: predicting hands and doors (2026-10-24, done; protocol 43, DV179)

D38 stage 1. The client does not run tg's procs: it guesses the visible outcome of a few instant actions from what it holds, shows it
at once, and lets the server's next word replace the guess. A guess that the server refused disappears (the item is back where it was,
the door slides shut again, the own capsule is corrected out of the doorway).

- **Protocol 43.** The private block carries `ActionsDone` (the count of the connection's Action messages the server has applied or
  dropped; reliable ordered, so the n-th action is settled once the count reaches n) and, when it changes, the own mob's access list
  (tg's `get_access()`, read by the host inside the world once a tick). An airlock carries a public `Door` field: tg's `req_access`,
  `req_one_access`, `unres_sides` and whether it would open for an authorised bump now (`requiresID`, power, not operating, bolted,
  welded, sealed or emagged), and `emergency`. A screen entry carries its inventory box's `slot_id` or hand's `held_index`, and an item
  entry its `slot_flags` and whether it has an `equip_delay_self`.
- **What is guessed** (each keyed by the action's number or by the motion frame's seq):
  - swapping hands (X, a click on the other hand's box): the `lhandactive`/`rhandactive` overlay moves to the other box;
  - throw mode (R): the throw button's `act_throw_on`/`act_throw_off`;
  - picking up an item next to the player with an empty active hand (a left click without modifiers on an `/obj/item`): it leaves the
    world and appears in the active hand's box;
  - dropping (Q): the active hand's item leaves the hand (it lands in the world when the server says so);
  - putting on: a click on an empty inventory box with an item whose `slot_flags` fit and that has no `equip_delay_self` (the belt, the
    ID and the pockets need a jumpsuit, the suit storage a suit, as tg's `can_equip`); taking off: a click on a worn item with an empty
    active hand puts it in the hand;
  - an airlock whose `Door` says it opens for the player's access (tg's `check_access_list`, `emergency`, or the unrestricted side),
    bumped (the predicted capsule's press event, as the server's bridge raises tg's `Bump`, at most once per 0.3 s) or clicked with an
    empty hand: it starts opening at once, and its cell becomes passable for the own capsule from the frame tg's
    `AIRLOCK_OPENING_PASSABLE` (0.5 s) falls on. The collision view answers by the frame being stepped, so the predictor's replays agree
    with the server's ticks.
- **Settling.** A hand or inventory guess stands until `ActionsDone` passes its action (then the replica holds the server's answer). A
  door guess is checked when the server has applied the bump's frame (or the click's action): accepted when the door's appearance is
  opening or open, refused otherwise; an accepted door keeps its predicted passable frames until the replica's cell turns passable.
- **Not guessed** (the server's answer arrives after a round trip): what lands in the world (a dropped item), the inventory boxes'
  own full/empty pictures, items in storage windows, double-tile airlocks' second cell, windoors, firelocks and shutters, closing a door,
  every attack or use of an item.

Estimates: protocol 43 (2 h), the server's fields (2.5 h), the replica's predicted layer (3 h), the guesses and their hooks (3 h), the
renderer's door (1 h), the tests and the doorway's calibration on the lossy loopback (3 h).

- **Found while building it**: tg's held and worn items are not screen objects of their own but the boxes' `vis_contents` (inventory_slot.dm
  `update_slot_icon_contents`), so an item's slots travel with the box's vis child, and a guess moves the child and its flattened
  picture between boxes. tg's collapsed inventory hides the clothing's boxes, so a hidden slot counts as worn for the belt's and the
  pockets' jumpsuit rule. A worn or held storage (a toolbelt, a backpack) opens at a hand rather than coming off, and is not guessed.
- **Measured** (`Integration/PredictionGameplayTests`, MetaStation, 100 ms each way, 2% loss): the pick-up, both swaps, throw mode, the
  belt's equip and unequip and the drop show in the step the player acts, about 200 ms before the server's answer, and the answer agrees
  (no refusal); an anchored wrench is guessed and comes back. A public airlock is guessed at the bump's frame, its cell turns passable
  on the server in the tick that applies the guessed frame (`PassableFrames` 10: 0.5 s), the server's opening reaches the client 6 steps after the guess, and the walk through has no correction; an airlock
  the access does not open is never guessed; an electrified one is guessed, tg shocks and keeps it shut, and the refusal arrives before the
  guessed passable frame, so no correction either.
- **Not done here**: a walk on through Meta's external airlock into space was corrected by 2.3 m once the capsule was weightless (the
  zero-g drift, not the doors: DV69/DV177's prediction; the two `BridgeTests` failing since b4db6855 are in the same area).

## 9. Slice F: the ticker, the lobby and spawning (2026-10-25, done; no protocol change, D270, DV180-DV182, DV186)

tg's round loop up to the round in progress. The server's `LOBBY` key (on in the shipped config/server.txt) chooses the mode; the
tests and the test room run without it.

- **With the lobby.** BYOND's `client/New` makes a key that no mob holds tg's `/mob/dead/new_player` (the host no longer makes a
  human). tg's lobby (`modules/lobby_menu`, tgui-lobby drawn natively: its sprites, flicks and sounds) shows the pregame: Ready, the
  120 s countdown, Setup Character (tg's PreferencesMenu: the name, the gender and the age of a random character, D267). When the
  countdown ends, tg's ticker sets the round up (`setup`, `create_characters`, `equip_characters`, `transfer_characters`): the ready
  players spawn as station engineers, the one open job (the host writes `OVERFLOW_JOB Station Engineer`, `LOAD_JOBS_FROM_TXT` and a
  `jobs.txt` that closes the others, DV181), at an engineer's start landmark in tg's outfit. After the start, Join opens tg's late
  join menu (JobSelection). The assistant stays open there by tg's newbie rule: without a database the account's age is unknown (-1).
- **Without the lobby.** The round starts at the ticker's first fire (tg's `start_immediately`, the lobby's Start Now) and every join
  late-joins as the overflow job, the assistant, through tg's `AttemptLateSpawn`, at the host's next spawn point (the job's spawn
  override for that one join).
- **`OPEN_JOBS`** (`--open-jobs`, with the lobby; off in the shipped config; D271): the host's `jobs.txt` closes only the heads of
  staff (tg's seven `HEAD_OF_STAFF_JOB_FLAGS` jobs: the captaincy is not ported) and the silicons (AI, cyborg, personal AI: no silicon
  mob is ported), so the late join menu offers every other job with tg's own slots (a job the file does not list keeps them, DV181).
  The round start is unchanged (the ready players are the overflow's station engineers). `LobbyGameplayTests`: Bob late-joins as a
  medical doctor in tg's outfit, the captain and the cyborg closed.
- **Coming back during the round**: the reconnect token takes the body back (tg's SSD, BYOND's client/New finds the keyed mob), and
  tg's `lobby_menu/New` sends its `init` only while the mob is a new player (DV44: an init opens the client's lobby window, where tg's
  skin selector would keep the map screen). A player who left from the lobby gets a new lobby (tg's `new_player/Logout` deletes the new
  player) whose Join still late-joins. `LobbyGameplayTests.A_player_who_leaves_during_the_round_comes_back_into_the_body_without_the_lobby`.
- **`startnow`** (dev command, apps.md): tg's admin Start Now. With the lobby, the round starts at the ticker's next fire; a second use
  before then cancels it (3 minutes left, tg's message and sound).
- **The round start deletes the start landmarks** (tg's `after_round_start`). The late join's last resort (DV180: no arrivals shuttle
  until slice G) and the host's spawn points (DV26: the Assistant starts) keep their turfs: `SSjob.last_resort_turfs` (and `SSjob.job_start_turfs` by job: a late joiner arrives at its own job's start, where its access opens the doors) and
  `MapSeam.NoteStartLandmark` (in the map's order; tg shuffles the list at the round start).
- **What the slice changed elsewhere**: a player's preferences are tg's (`client.prefs`), so the ghosts' accessories, others and
  lighting, the double-click examine and the tooltips' theme (the UI style's, Midnight by default) read tg's preferences; the host's
  config now has tg's shipped `OOC_DURING_ROUND` (tg mutes OOC at the round start without it). The worlds of the tests mount tg's
  `strings/` (the ticker's lobby music and the station name read them), and the test worlds that stand in for the ticker
  (`TgTestWorld.RunLevel`, the toy scenario) pause it with tg's `can_fire`.
- **Measured** (`Integration/LobbyGameplayTests`, MetaStation, the lobby on): Alice joins into the lobby (countdown 118 s), renames her
  character Ada Lovelace, female, 40, readies, starts the round with `startnow`, and spawns as the station engineer at an engineer's
  start in tg's engineer outfit, on the crew manifest; Bob joins after the start, the job menu lists the engineer and the assistant
  open and the other jobs closed, and he late-joins as an engineer. No runtime past the boot's.
- **Found while finishing it**: tg's `check_wounding` loses an attack's damage when the only candidate wound weighs 0 (an injury roll
  of 110 to 115 on a head above hard crit: the cranial fissure alone; DV186). A dimensional anomaly's clown theme makes bananium
  floors, which squeak and slip (tg's `/datum/component/squeak`, translated).
- **Not done here**: the real Godot client's lobby screen is not checked yet; observing from the lobby (DV182) and the arrivals
  shuttle (slice G).

## 10. Slice G: shuttles through tg's machinery (plan, 2026-10-25, D272)

The survey: in tg the emergency, cargo and ferry shuttles start at CentCom's docks (`CentCom.dmm`'s stationary ports with the json keys
`emergency`, `cargo`, `ferry`, which SSmapping's map config names), which D108 does not load; every shuttle template loads into a turf
reservation and docks with the move code (`initiate_docking`), which rotates it to the dock (Meta's mining docks: the delta mining
template by 180 degrees, the common one by 90). The owner chose tg's machinery (D272) over placing the templates by hand. Steps, each
tested and committed:

- **G1 reservations** (`space_reservation.dm`, SSmapping's `add_reservation_zlevel`, `request_turf_block_reservation`,
  `initialize_reserved_level`, `reserve_turfs` and its fire, the transit turfs `transit.dm`): tg's first transit level is made at
  SSmapping's initialize (the station level is 1, the reserved level 2). The client receives the level in its map (the cells of a space
  level are cheap) and shows the level its own mob is on.
- **G2 templates**: `map_template.dm`, `datums/shuttles` (the round-start ones: arrival, cargo, mining, labour, escape pod, aux base,
  emergency), SSmapping's `preloadShuttleTemplates`; the map converter also writes `_maps/shuttles/*.as3dmap` and the host mounts them.
- **G3 docking and the move code**: `shuttle.dm`, the stationary ports (`stationary_port.dm`, `port_types.dm`, the mining and labour
  docks), `mobile_port.dm`, `shuttle_move.dm`, the move and rotate callbacks, `areas/shuttles.dm`, SSshuttle's init, `setup_shuttles`,
  `action_load`, `load_template` and fire. Meta's round-start shuttles stand at its docks as tg's do; the arrivals shuttle's chairs are
  the late join's spawns (`latejoin_trackers`, `arrivals.dm`), which retires DV180's fallback and DV26.
- **G4 the CentCom stand-in and the emergency shuttle** (a DV): the CentCom docks that the map config names (`emergency_away`,
  `cargo_away`, `ferry_away`, tg's sizes and directions) are stationary ports in a reservation of the reserved level; the emergency
  template loads there as tg's does at CentCom. `emergency.dm` (request, cancel, check: call, docked, igniting, escape, endgame),
  SSshuttle's `requestEvac`, `cancel_evac`, `canEvac`, `CheckAutoEvac`, `autoEvac`, the transit docks (`generate_transit_dock`), tg's
  timers and announcements. The shuttle docks at Meta's evac bay when its call timer ends, leaves into transit with its passengers when
  its docked time runs out, and on reaching the stand-in dock the mode is `SHUTTLE_ENDGAME` and tg's ticker ends the round (the end
  screen and the restart are slice H). The cargo shuttle starts docked at Meta's cargo bay (D266; no cargo console yet).
- **G5 the communications console** (`communications.dm`, tg's `CommunicationsConsole` window translated natively): call and recall the
  shuttle with tg's access, reasons, cooldowns and messages; the security level parts as far as the core version has them. A dev
  command `shuttle call|recall` (tg's admin Call/Recall Shuttle, D273) calls it while the heads of staff are closed; the heads open
  in their own slice after G (D273).
- **Tests**: gameplay tests on MetaStation (the docked shuttles and their rotation, a late joiner on an arrivals chair, Alice calls the
  shuttle from the bridge, it docks, Bob boards, it leaves with him into transit, the round's state is endgame), the full suites.

Done (2026-10-25): G1-G5 as planned, committed together (the owner wanted no step commits). Deviations DV188 (only the converted
templates preload), DV189 (the arrivals shuttle stays docked, D273) and DV190 (CentCom's docks in reservations; the cargo shuttle at
the station). Notes: SSshuttle makes no stand-in and does not fire in a world without a selected map; the shuttles' and reservations'
turf changes leave SSair a few thousand active space turfs for its first fire (about two seconds, as tg's add_to_active takes space);
the console's printed copies wait for the paperwork; the round-start command report waits for the threat report; tg's 20-minute
refuel keeps the console's call shut early in the round (the dev command bypasses it, as tg's admin verb does). Tests:
`tests/Anomaly.Core.Tests/Tg/M10Shuttles/*`, `ShuttleGameplayTests` (the console, the call, the recall, the dev call, the boarding, the
transit on z 2, the endgame).

## 11. Slice G2: the heads of staff (plan, 2026-10-26, D273, D274)

The survey: the six heads' job datums (`captain.dm`, `head_of_personnel.dm`, `head_of_security.dm`, `chief_engineer.dm`,
`chief_medical_officer.dm`, `research_director.dm`) are inert for want of one to three procs each (the captaincy announcement, the
captain's radio note, the CE's and HoS's `after_spawn`, the HoP's traitor objective and mail goodies); the outfits of the CE, CMO, HoS
and RD are live, the captain's and HoP's want their `pre_equip`/`post_equip`. tg's captaincy is NotTranslated at its three sites
(`ticker.dm` equip_characters, `new_player.dm` AttemptLateSpawn, `job.dm` promote_to_captain) and `announce_head`; the spare ID safe
(`secure_safe.dm`, `lockable_storage.dm`, tg's LockedSafe window) is inert. The paperwork and the mob memories are not ported, so the
spare ID safe's code reaches the captain in chat (DV191) instead of a confidential folder and a memory. Steps:

- **H1 the jobs and outfits**: the six job datums and the two outfits live (the station charter stays an inert item in the captain's
  bag: its rename of the station is the admins'/after M10).
- **H2 the captaincy**: the round start's spare ID candidates by the chain of command and the promotion of one (the captain, or the
  highest head as acting captain), the late joiner's captaincy with tg's announcements, `promote_to_captain` (the code in chat, DV191;
  the ID's command access), the heads' AAS announcement (`newhead`).
- **H3 the spare ID safe**: `/obj/structure/secure_safe` and its `caps_spare`, the `lockable_storage` component with its keypad
  (tg's LockedSafe window natively); the hacking with a screwdriver and multitool comes with it, the emag is the antagonists'.
- **H4 OPEN_JOBS**: the heads open (D271's closed list keeps the silicons).
- **Tests**: Core (the captaincy at the round start and at a late join, the safe's code); a gameplay test on MetaStation: Alice
  late-joins as the captain in her outfit with the announcement, opens the spare ID safe on the bridge with the code she was told,
  takes the spare ID, and logs in at the communications console; Bob late-joins as the Head of Personnel (the acting captain only
  when there is no captain). The full suites.

Done (2026-10-26): H1-H4 as planned, with DV191. Tests: `HeadsGameplayTests` (the captain's announcement, outfit and code, the safe's
keypad, the console's captain login; a lone HoP as acting captain), `LobbyGameplayTests` (the heads open in OPEN_JOBS).

## 12. Slice H: the round end and a clean restart (plan, 2026-10-26)

The survey: tg's ticker ends a round in `declare_completion` (`__HELPERS/roundend.dm`: the round-end callbacks, the reboot timer HUD,
the title music, the round-end feedback and the station integrity (`station_integrity.dm`, counted at the round start), the
survivor and personal reports shown in a browser window, the observers made visible, the end of the logs), then `standard_reboot`,
the ticker's `Reboot` (tg's 90 s `ROUND_END_COUNTDOWN`, the "Rebooting World in ..." line) and `world.Reboot`. The port's client has no
HTML browser window (`/datum/browser` is inert), so the report reaches each player in chat (DV192). Steps:

- **H1 the round end in the world**: `station_integrity.dm` and the round start's `GLOB.start_state`; `declare_completion` with
  `gather_roundend_feedback`'s counts, `survivor_report`, `personal_report`, `display_report` (DV192), `standard_reboot`, the
  ticker's `Reboot`, `reboot_callback`, `cancel_reboot` and the reboot HUD's countdown in `fire`; `set_observer_default_invisibility`.
  Left: the antagonists' parts (the alpha), the credits, the achievements and medals, the persistence, the database and the logs
  (slice I).
- **H2 the restart**: `world.Reboot` asks the host for a new round: the host builds a fresh world from the map in-process (D15),
  keeps the connections and re-joins every session into the new round's lobby; protocol bump: a Welcome after the play has begun
  means a new round, so the client clears its replica and its prediction and joins again (the bots and the headless client too).
- **Tests**: Core: the report's counts and texts on MetaStation; Server: a round that ends at CentCom, the report in chat, the
  countdown, the restart into a fresh lobby with both clients still connected, a second round whose start is a fresh boot's.

Done (2026-10-26): H1 as planned with DV192, and DV190's `onCentCom` (an escaped shuttle at a CentCom stand-in on the reserved
level counts). H2 differs from the plan in one point: BYOND's reboot makes the clients reconnect, and the host does the same (protocol 45:
`DisconnectReason.RoundRestart`; the client's `JoinAttempt` rejoins with its token and retries `NotReady` while the new world boots)
instead of keeping the connections, so the client starts the new round with a fresh replica. A replay is one round. The bots
ride the restart in slice J. Tests: `RoundEndGameplayTests`.

## 13. Slice I: admin tools (plan, 2026-10-26, D275)

The survey: tg keeps its admins in `admins.txt` and `admin_ranks.txt` (`admin_ranks.dm`: the rank keywords to `R_*` flags, `load_admins`)
and the database's admin tables; a `/datum/admins` holder (`holder2.dm`) associates with its client at `client/New` and
`check_rights` reads its flags. Bans (`sql_ban_system.dm`) are rows of the `ban` table keyed by ckey, IP and computer id, checked
at connect by `world/IsBanned` (`IsBanned.dm`) with tg's refusal text; notes are rows of the `messages` table
(`sql_message_system.dm`: notes, watchlist entries, memos), a player's unread non-secret notes are shown at connect and the
admins hear of a watchlisted player's connection; `SSdbcore.InitializeRound` gives the round id. SQL and the admin panels are not
translated (rules 13), and the port has no BYOND hub that proves a key. Steps:

- **I1 the database** (server side, `Microsoft.Data.Sqlite`, MIT): one file (`DATABASE`, default `data/anomaly.sqlite`; in memory
  without one, the tests), schema migrations by `PRAGMA user_version`, tg's `round`, `player`, `ban` and `messages` tables with
  the columns the port fills. The round id: `SSdbcore`'s `InitializeRound`, `SetRoundStart`, `SetRoundEnd` through a host seam
  (the world asks, the host inserts the row), so `GLOB.round_id` names the round in the logs and the report.
- **I2 keys** (D275): a key is the player's name with its reconnect token, kept in the `player` table: the first connection of a
  name claims it, later connections of that name need the token (the client keeps it), across restarts of the server. tg's
  `player` row (first and last seen, last IP) is updated at each connection.
- **I3 admins**: tg's `admin_ranks.dm` and `holder2.dm` translated for the text files (`config/admins.txt`,
  `config/admin_ranks.txt`, tg's legacy system; the shipped ranks are tg's), `client/New`'s association, `check_rights` and
  `check_rights_for`. The server console is always an admin. A player's console command needs the right of tg's matching verb
  (spawn `R_SPAWN`; teleport, list, kick, the shuttle `R_ADMIN`; quit `R_SERVER`; status, hash and the dev commands `R_DEBUG`;
  bans `R_BAN`; notes `R_ADMIN`), or the server's switches that open it to everyone (`DEV_COMMANDS`, `CLIENT_CONSOLE`) as before.
- **I4 bans**: `ban KEY DURATION REASON` (the key and its last IP, tg's default "use last connection"), `ban-ip IP DURATION REASON`,
  `unban ID`, `bans [KEY|IP]`; DURATION is `perm` or minutes (`30`, `2h`, `7d`). tg's `create_ban` (its note, the logs,
  `message_admins`, the banned player told and disconnected), `unban`, and `IsBanned`'s check at connect with tg's text
  (protocol 46: `DisconnectReason.Banned`); an admin passes a ban that does not apply to admins. Server bans only (role bans
  wait for the jobs that read them).
- **I5 notes**: `note KEY [secret] TEXT`, `watch KEY TEXT` (a watchlist entry), `notes KEY`, `note-delete ID`: tg's `create_message`
  and `delete_message`, `display_unread_notes` at the player's connection, `scream_about_watchlists` to the admins.
- **Tests**: Core: tg's rank file parsed into flags, `check_rights_for`; Server: the migrations, a key's token across two hosts on
  one file, a ban refusing a key and an IP with tg's text and expiring, unban, an admin's bypass, the rights of a player's
  commands, a note shown at connection and a watchlist line to an admin, round ids across a restart.

Done (2026-10-26): as planned, with DV193 (tg's legacy admin files only), DV194 (server bans without the computer id; console forms) and
DV195 (notes and watchlist entries only), protocol 46. Also translated on the way: the admins' auto deadmin of tg's job and lobby
(`handle_auto_deadmin_roles`, `auto_deadmin`: tg's default preferences deadmin an admin who takes a head job), the ticker's `Reboot`
rights, `rights2text`, `log_suspicious_login`. The round id is in the round-end report and is recorded in the command log (`round N`)
for a replay. Tests: Core `AdminRankTests`; Server `AdminGameplayTests`.

## 14. Slice J: bots and load (2026-10-27, done)

**The bots in the round loop.** A bot reads tg's lobby window (`LobbyMenu`, tgui): in the pregame it readies up, after the start it presses
Join and picks the station engineer in tg's `JobSelection`; it does not walk while its lobby is open. A `RoundRestart` close makes it join
the next round with its token, retrying `NotReady` while the new world boots. The runner sends scheduled dev commands from the first joined
bot (`--dev T:COMMAND`: `startnow`, `shuttle call`, and the new `reboot [SECONDS]`, tg's admin Reboot World). `run.sh` gives the server
its own database per run (`soak.sqlite`) and, on Windows (no SIGTERM for a native process), stops it with the console's `quit`. Tests:
`BotRoundGameplayTests` (ready, late join, the restart into the next lobby, the old world collected).

**What the load showed and what was changed** (measured on the owner's Windows desktop, 32 threads, Release, with other programs running;
`tools/soak/run.sh ... --dev 20:startnow`; profiles with `dotnet-trace`'s sampled thread time and `gc-verbose`):

- Footsteps: every step of every walker reaches every hearer, and the occlusion search (DV39) ran again for each session every tick: a
  quarter of the tick at 100 players. The search is now kept while the listener stays on its tile, for at most 10 ticks
  (`SoundFeed.SearchReuseTicks`; a door that opens or closes reaches the occlusion within half a second). 27.6% of the tick to 8.3%.
- Interest management (slice C): `HashSet.Clear` zeroes its whole bucket array, which grew to the thousands of a chunk change: every
  session paid milliseconds a tick. The check list is a list now.
- Allocation (957 MB to 534 MB per 15 s at 100 players): the gravity mask of the capsule prediction (protocol 48, 48 `has_gravity` calls
  per mob per tick) is kept while the mob stays on its tile and that tile's gravity holds, for at most 10 ticks; the Motion messages are
  reused call to call.
- The restart (slice H) kept the old world twice over: the closed sessions held their clients (and a key's rejoin then ran the old
  client's `client/Del` in the new world: 100 runtimes an hour), and the first replicator, still referenced from the server's `Main`,
  held round 1's world for the rest of the run. Both let it go now (verified with `dotnet-gcdump`: one `World` after a restart).
- Server GC was tried (`DOTNET_gcServer=1`): slower at every percentile; the workstation GC with `SustainedLowLatency` stays.

**Results** (steady state: from tick 1200 of each round, while players are connected; times in ms):

| Run | Tick p50 | p95 | p99 | p99.9 | Overruns | Down per player | Gen2 GCs |
|---|---|---|---|---|---|---|---|
| 100 bots, before (5 min) | 17.0 | 25.1 | 30.1 | - | 12 | 22.5 KB/s | 8 |
| 100 bots, 1 hour, round 1 (30 min) | 12.0 | 17.9 | 24.5 | 50.9 | 11 | 20.5 KB/s | 11 |
| 100 bots, 1 hour, round 2 (30 min) | 12.4 | 19.0 | 27.1 | 63.1 | 22 | 20.9 KB/s | 13 |
| 150 bots (6 min) | 16.5 | 25.5 | 33.1 | - | 8 | 29.6 KB/s | 10 |

The one-hour 100-bot soak (D269) rode two round ends and their restarts (the shuttle called at 2 and at 32.5 minutes): 300 joins, no
desync, no bot exception, every bot back in the next lobby within about 10 s of each restart (the rejoins meet `NotReady`, a refused
connection and, once, `ServerFull` while the new world boots). It ran before the restart fix: its second round's heap was 2.5-2.9 GB
against 1.1-1.4 GB in the first, and its 100 runtimes were the old clients' `client/Del` (fixed above). PLAN's targets: 100 players p99
at most 30 ms holds (24.5 and 27.1); 150 players p99 at most 50 ms holds (33.1); the average traffic is at the 20 KB/s line (20.5-20.9 KB/s);
the gen2 collections (one every 2,000-2,500 ticks, followed by a dozen ticks of 40-60 ms) and the allocation (about 1.7 MB a tick
against PLAN's 10 KB) do not.

**Known, not changed:**
- The round start creates every character in one tick (tg's `SSticker.setup`): 0.8 s for 84 players, 2.4 s for 150.
- The shuttles' moves (the emergency shuttle's arrival and departure) take 0.8-1.9 s in `SSshuttle` and up to 1.7 s more in `Area
  Contents` after them: a few ticks a round, as in tg.
- About 3 ms a tick at 100 players wait on LiteNetLib's per-peer locks while its own thread sends (the main thread's `Send`).
- The allocation is spread over the translated code (`DmList`s of `has_gravity`, `playsound`'s lists, boxed numbers); the gen2 bursts
  follow from it. Left for the infrastructure pass after M10 (D161) unless the owner asks earlier.

## 15. Slice K: the closing (2026-10-27, done; released as 1.0.0-pre-alpha, D280)

**The demo check** (`tests/Anomaly.Server.Tests/Integration/M10ScenarioTests.cs`, about a minute): MetaStation with the shipped lobby config
(the lobby on, open jobs). Ten bots ready up in tg's pregame lobby; Alice names her character in tg's character setup, sets the medical
doctor to High and readies; Bob joins through the connect screen and stays not ready. Alice's `startnow` starts the round: she plays her
character in tg's doctor outfit, every bot is a living human with the job tg's `DivideOccupations` gave it. Bob late-joins from the lobby's
Join and tg's job menu as a station engineer on the arrivals shuttle. After half a minute of play the emergency shuttle (tg's timers
shortened) ends the round with tg's report and the reboot countdown; the host restarts, both clients come back with their tokens into the
new round's lobby, as every bot does. No runtime beyond the boots', and the old world is collected. SSgarbage's `Shutdown` is translated
for it (`garbage.dm:82-115`, the qdel log), so a restart no longer raises a not-translated runtime.

**The re-tag** (not-translated.md 84): 364 hand-written sites and 135 rows of `live-partial.txt` still labelled M10 became `M10+`. Seven sites
inside M10's scope were translated: a shuttle's airlocks, blast doors and buttons take the shuttle's id (`connect_to_shuttle`); the
supermatter cascade strands the emergency shuttle (`effect_strand_shuttle`) and its rift's end sends an escaping shuttle the backup way; a
postgame login sees the reboot countdown; the health analyzer and the time of death read the station clock (`round_timestamp`).

**The suites** (Debug, the M1 Pro): Core 6,565 passed, 5 skipped, 1 failed; Server 3,170 passed, 4 skipped, 2 failed (both also failed at
f52ef981, before slice K). The three are fixed and pass in filtered reruns: the shock test picked a cable in Port Bow Solar Array, whose
space kept freezing the player (it picks a pressurized room now); MetaStation's genetics monkeys (inert, no DNA, DV83) thrown by the
singularity or pushed into an airlock reached the fingerprint and the stun code (DV83 covers both now).

**The long rounds** (`tools/soak/run.sh`, Release on the M1 Pro, 16 `mixed` bots, `--dev 20:startnow --dev "3600:shuttle call"`):

| Run | Length | Tick p50 | p95 | p99 | p99.9 | Overruns | Down per player | Gen2 GCs | Desyncs | Restart ridden |
|---|---|---|---|---|---|---|---|---|---|---|
| Seed 10 (the full suites ran beside it) | 82 min | 7.1 | 35.4 | 75.9 | 167 | 552 | 5.6 KB/s | 54 | 0 | no |
| Seed 11 | 95 min | 6.5 | 11.5 | 15.0 | 24.0 | 23 | 4.2 KB/s | 62 | 0 | 16 of 16 |

The first run ended before its shuttle came: at green alert tg doubles the call time (`shuttle_call_time_mod`, 20 minutes). The second
was timed for it: the shuttle was called at 60 minutes, docked at 80, left at 82.8, the round ended at 85 and the world restarted at 85.5;
every bot rode the restart into round 2's lobby (the rejoins meet the booting world's refusals, 16 `ConnectFailed`, as in J) and readied
up again. No decode error, no bot exception. Its one runtime is the boot's `monitored_area` (the cameras' motion areas, not translated),
once per world. The first run also had a boot runtime of our own: the model rules added `simple_rotation` to an emergency closet that
tg's Initialize had just qdeleted (its 1%, `utility_closets.dm:30`); the rules skip a qdeleted atom now, and `stack_trace`'s runtime
carries its caller's stack (it was never thrown, so the log had none). The longest ticks are where J found them: the map's init at boot
(21 s, 9.8 s for round 2, before anyone joins), the round start (1.1 s), the shuttle's dock (0.4 s), its launch (1.35 s, then a few ticks of
0.6-0.9 s) and the round's end (0.3 s).

**ROADMAP M10's done-when**: the round from the lobby to the restart on MetaStation with 16 bots and no crash (the seed 11 run, 85 minutes,
and the demo check with two clients); the one-hour 100-bot soak (slice J: p99 24.5 and 27.1 ms against 30); hands and doors predicted with
no visible correction at 100 ms and 2% loss (slice E). The owner's playtest is `docs/ai/C6-checklist.md` (ROADMAP 8's C3).
