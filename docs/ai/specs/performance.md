# Client and server performance: measurements and plan (2026-10-29, draft for the owner)

The owner (2026-10-29): the FPS fell a lot, and the frame stalls at the join and while walking about. Ask: find the causes, look for other
ways to optimize, make a plan. Nothing here is built yet; every item waits for the owner's choice.

## 1. What was measured

Release server on MetaStation (`--realtime --map metastation --no-lobby`, port 7791), client run the way the editor runs it (`godot-mono --path
godot`: a **Debug** C# build), M1 Pro, Metal, Forward+, 1600 x 900, the same Mac. The walk: `"--dev-after=3,god me" "--dev-after=4,tp me 131
127" --autowalk` (fly, noclip, godmode, from the station's centre; the owner, 2026-10-29: the spawn at the map's edge mostly walks space),
`ANOMALY_FRAMEPROBE=1 ANOMALY_FRAMEPROBE_MS=30`, 116 s. The FPS: `--bench=20 --bench-at=131,127` after the same teleport (a 3-tile square, the
interest around the camera).

**Join.** `MapRenderer.Build` takes 13.1-13.9 s every run (M3 R2: 2.3 s), all on the main thread, timed by section with temporary probes:

| Part | ms |
|---|---|
| plan, kits, tile kit, chunk nodes | 640 |
| the records' loop (`AddRecord`, `AttachTurfs`) | 13,100 |
| of it: **30 directional windows' pictures** (`BorderCounterparts` / `BorderWindowPicture`, e3de35b5) | **6,926 (230 ms each)** |
| of it: 1,248 models (`TryBuildModel`): model textures 1,490, GLB reads 140, meshes 56, catalog lookups 170 | 3,990 |
| of it: icon sheets decoded synchronously (247) | 950 |
| batch rebuild, atlas flush, turfs | 120 |

The first game frame after it is 370 ms (all lamps, shadows and entities at once).

**Walking** (from the centre, 116 s): average 52 FPS (5,997 frames); 203 frames over 30 ms, 91 over 50 ms, 55 over 100 ms, max 303 ms.
In the long frames: `map/models.WarmUp` 5.3 s in all (its budget is 3 ms a frame; measured 95-100 ms a frame for runs of frames),
`entities/AddPending` 3.7 s (budget 4 ms; one frame 296 ms), `map/atlas.Flush` 0.65 s, `map/batch.Rebuild` 0.18 s, two frames of 195-262
ms in the renderer alone (no script time). Draw calls: median 4,967, max 8,664 (M3's bench: 76).

**Steady FPS at the centre, per preset** (`lamps_now` 26-37 lamps near):

| Preset | Shadowed lamps | avg FPS | p99 FPS | Draw calls |
|---|---|---|---|---|
| low | 4 (dual paraboloid) | 157 | 100 | 1,042 |
| medium, shadowed lamps 0 | 0 | 121 | 93 | 1,616 |
| medium | 8 | 76 | 58 | 6,666 |
| high (the owner's options: medium with 16 lamps) | 16 | 49 | 43 | 11,300 |
| max | 32 | 29 | 24 | 21,505 |

Each shadowed lamp adds about 650 draw calls **every frame**: the lamps' cube shadows (6 passes each) are drawn again each frame instead of
being kept. Godot keeps a positional light's shadow until the light or something paired with it changes, so something changes every frame.

**Server.** Not the cause of the client's FPS: steady tick p99 24.5 ms at 100 bots (multiplayer.md 14). But it sends every new spawn of a
session in one tick's Delta, sorted by id, with no budget (`Replicator.Interest.cs:313-351`): a step into a new area is 400-700 spawns
(7-25 KB, 8-26 fragments) in one reliable message, and their despawns 2 s later as one more burst. The only hysteresis is the 2 s hold;
there is none in space (each chunk crossing re-sends a 3-chunk near column and a 17-chunk far column of lights and doors), and a door
cycle re-sends whole rooms.

## 2. The causes found in the code (two read-only audits, 2026-10-29; the engine-side ones are to be confirmed by A/B)

- Lamp shadows dirtied every frame (suspected, the main FPS cost): the own body's shadow-only picture moved every frame
  (EntityLayer.cs:265-275), the own flashlight and others' beams moved every frame; blinking and spinning model parts written every frame
  with no change check (MapRenderer.Models.cs:461-469, 548-571), each write re-uploading the chunk's whole MultiMesh (StaticProps.cs:369-379),
  whose `MultiMesh` has no `CustomAabb` (so the box is recomputed and reported changed); `ShadowEnabled` set every frame of a fade and for
  every beam (LightLayer.cs:929, 1311), the shadow mode set on every flicker (953-999); lamps re-chosen every 0.2 s and every 20 degrees of
  turn, a lamp newly shadowed drawing its cube from scratch. The lamps' cull mask is every layer.
- The sun's shadow (directional, never kept by Godot) drawn every frame while any open-sky cell is near: medium 4096 px, 2 splits, 60 m.
- No cap on lamps; lamps freed and created while walking (LightLayer.cs:866-974); each model light part its own OmniLight.
- The spawn budget covers only node building: the first picture composition and `ImageTexture.CreateFromImage` run after it, unbudgeted
  (EntityLayer.cs:262, 561; AppearanceTextures.cs:110); mobs and visuals skip the queue; every billboard has its own QuadMesh and 1-2 own
  ShaderMaterials (Billboard.cs:117, 161; EntityLayer.cs:1533-1556), so they never batch; a lit spawn recomputes the light grid inline.
- `PropAtlas.Flush` uploads every dirty 2048 x 2048 page whole (16 MB) for one new picture; a new page re-uploads all (PropAtlas.cs:452-487).
- Despawn: `_list.Remove` is a linear scan per entity (O(n^2) per burst, EntityLayer.cs:1583), `QueueFree` unbudgeted, LINQ `RemoveAll` per
  chunk (StaticProps.cs:202-210); chunk rebuilds capped by count (6 a frame), not time.
- A door opening or closing redoes the whole level's door cells: `DoorCellLight.Fill`, a full image upload, `PlaceDoorBlocks` resetting the
  MultiMesh's count and setting each transform (a GPU read-back after a reallocation; LightLayer.cs:593-728).
- The light grid (now only a 0.3 bounce) re-uploads the whole level texture (0.5 MB) on any change (LightLayer.cs:434-436, 638-648).
- Join: `EntityLayer.Bind` adds all ~5,300 entities at once (EntityLayer.cs:159-160); icon sheets load synchronously until the build ends
  (GameScene.cs:172-173).
- Smaller per frame: several engine calls per entity per frame in `EntityLayer.Update`, a name tag string per mob per frame (1660), the pick
  walking every entity up to twice a frame (1407-1436), `AirflowLayer` rebuilding its whole mesh each frame (1005-1011), small LINQ lists
  (GasLayer.cs:213, TguiHost.cs:127, SoundPlayer.cs:197), a font override every frame with zone labels on (HudLayer.cs:298).
- The editor's run is a Debug build (and the core libraries build Debug in every configuration but ExportRelease): the CPU numbers above
  are higher than a player's export.

## 3. The plan

Each item: what, the expected gain, hours (AI work, with tests and a measurement before and after). No item changes the wire protocol, and
none changes tg logic. Order recommended: P0, P1.1, P2.1, P3.1-P3.2, P3.7-P3.8, then the rest by measurement.

### P0. A repeatable measurement (3-4 h)
1. `tools/perf/walk.sh`: the server on Meta, the walk above with a fixed seed, and the bench at the centre per preset; one summary line each
   (frame time p50/p95/p99/max, frames over 33/50/100 ms, draw calls, the join's build time) written next to the run. 2 h.
2. Keep the build's section probes (FrameProbe, off by default) and log the slowest kind in `AddPending` and `StepWarmUp`. 1 h.
3. Baselines: Debug (the editor) and ExportRelease (the players'). 0.5-1 h.

### P1. The join: 13.9 s to under 3 s (5-8 h)
1. The directional windows' pictures: 230 ms each, 6.9 s for 30. Find the uncached step (the border picture or the junction's smoothing
   state) and cache it. -6.9 s. 1-2 h.
2. The models at the join (4 s): decode every model texture and icon sheet the plan needs on the thread pool before the records' loop
   (the warm-up's decode tasks exist). -2 to -3 s. 2-3 h.
3. The first frames: the join's entities through the nearest-first queue instead of `Bind`'s all at once; the first lamps and shadows
   created a few a frame. The 370 ms first frame to under 50. 2-3 h.

### P2. The steady FPS: medium 76 to about 110, the owner's 16 lamps 49 to about 80 (11-16 h)
1. Find and stop what redraws the lamps' shadows each frame (A/B one by one): set `ShadowEnabled` and the shadow mode only when they change;
   `CustomAabb` on the chunks', tiles' and door blocks' MultiMeshes; write blinking and spinning parts only when they change, and let them
   cast no shadow; keep the own body and characters out of the lamps' cull mask but the few that should show them. Standing still the draw
   calls should fall toward the 1,600 of no lamp shadows. 4-6 h.
2. The lamps' choice: a shadow kept longer once given (a margin), at most about 2 new shadows a selection (turning round stalls today), cube
   shadows for the nearest few and dual paraboloid (2 passes, not 6) for the rest. 2-3 h.
3. The sun's shadow: A/B first; a shorter distance and 1 split on medium, on only near the sky. 1-2 h.
4. The main pass (1,000-1,600 calls against M3's 76): billboards share one QuadMesh per size and one material per kind; a cap on unshadowed
   lamps (the 32 nearest) and a pool of light nodes instead of free and create. 4-5 h.

### P3. The stalls while walking: no frame over 50 ms in the walk (22-33 h)
Client:
1. Real spawn budget: the first picture and texture of a new entity inside the 4 ms, mobs included; a thing that alone blows the budget is
   named in the log and fixed. 3-4 h.
2. Real warm-up budget: `StepWarmUp` in smaller steps (it runs 100 ms frames today), the mesh build (plain C#) on the thread pool too. 3-4 h.
3. Despawns: swap-remove by index, sets instead of list scans, `QueueFree` and chunk rebuilds under a time budget. 2-3 h.
4. The atlas: flush at most once every few frames during a burst, and smaller pages so one picture does not upload 16 MB (Godot has no
   partial texture update; a page array of 512 px layers or several small textures). 3-4 h.
5. Doors: the door blocks per chunk, filled from one buffer; the door cells' light updated for the changed cell only. 2-3 h.
6. The light grid: the dirty rows only, or at 5 Hz. 1-2 h.
Server (no protocol change):
7. A per-session spawn queue, nearest first, with a budget a tick (about 150 entities or 6 KB, so a Delta stays within 8 fragments); the
   own mob, mobs and doors skip it; the join's entities through it as well. 4-6 h, with `InterestGameplayTests`.
8. Hysteresis in space: enter at the 3 x 3 near chunks, leave at 5 x 5; far lights and doors leave at `FarTiles` + 8; a room stays linked
   some seconds after its door shuts. 3-4 h.
9. Optional: fetch one chunk ahead in the walking direction. 2 h.

### P4. Small per-frame costs (4-6 h)
`EntityLayer.Update`'s engine calls only when a value changed, the name tags' string, the pick's walk, the airflow mesh, the small LINQ
lists, the zone labels' font. Then the per-frame script time is measured again in ExportRelease.

### P5. Optional: the server's allocation (8-16 h; the infrastructure pass D161 left it)
1.7 MB a tick at 100 players and a gen2 collection every 2,000-2,500 ticks followed by a dozen 40-60 ms ticks: pooled `DmList`s for
`has_gravity` and `playsound`, fewer boxed numbers. It shows to players as a short rubber band, not as low FPS.

**Total:** P0-P4 45-67 h; with P5 53-83 h.

## 4. Reproducing

```
dotnet build src/Server -c Release && dotnet build godot/Anomaly.Client.csproj
dotnet src/Server/bin/Release/net10.0/Anomaly.Server.dll --realtime --map metastation --port 7791 --no-lobby
ANOMALY_FRAMEPROBE=1 ANOMALY_FRAMEPROBE_MS=30 godot-mono --path godot --resolution 1600x900 -- --connect=127.0.0.1:7791 --name=Walk1 \
  --token-store=/tmp/walk1.txt --option=shadowed_lights=16 "--dev-after=3,god me" "--dev-after=4,tp me 131 127" --autowalk --seed=11 --quit-after=130
godot-mono --path godot --resolution 1600x900 -- --connect=127.0.0.1:7791 --name=Bench1 --token-store=/tmp/bench1.txt \
  "--dev-after=1,tp me 131 127" --bench=20 --bench-presets=medium,low,medium,high,max --bench-at=131,127 --bench-out=/tmp/bench1.txt
```

Each run needs a new `--name` (the server keeps a name for its token). The first preset of a bench overlaps the join's streaming; read the
second.

## 5. Status and results

The owner (2026-10-29): the whole plan, in the order of section 3. The server items P3.7 and P3.8 were built on their own branch
(`perf/server-interest`) while the client items went on, and merged after them (the server's part below).

**P0 done.** `tools/perf/walk.sh` (section 4's runs in one script, `summary.txt`); the frame probe's whole-run summary
(`[frameprobe] summary`: the frame time percentiles, the frames over 33/50/100 ms, the draw calls; the first 60 frames left out), the
build's steps (`[frameprobe] build/... at N ms`) and the queues' slow items (`spawn TYPE ms`, `warm FAMILY/PART ms`, over 2 ms) in
the long frames' lines. Baseline (main 7fc89e6a, Debug client, medium):

| Run | Result |
|---|---|
| join | build 13.3-13.5 s |
| walk 109 s | 58.3 FPS, p50 15.7 ms, p95 25.5, p99 69.9, max 138; 144 frames over 33 ms, 81 over 50, 50 over 100; draws p50 4,588, max 7,805 |
| bench low / medium / high / max | 160.5 / 74.5 / 47.6 / 27.2 FPS; p99 106 / 53 / 41 / 24; draws 1,108 / 8,350 / 12,524 / 25,186 |

The walk's slow items: the bar sign family's 50 models warmed at about 100 ms each (4.9 s), the first bar sign 292 ms, every vending
machine 47-55 ms, the computers and telescreens 60-78 ms.

**Done on the client (2026-10-29; Debug client, M1 Pro, the same walk; the machine was shared with other runs, load average 5 to 20,
so single numbers move by 10-20 %):**

| Item | What was found and changed | Measured |
|---|---|---|
| P1.1 | The directional windows' renderer asked for `<parent>/fulltile` paths tg has not got; the first miss built `ReflectionTypeSource`'s full index of every class (7.2 s). A path not in `TypeOrder.Paths` is a miss at once. | join build 13.5 to 6.5 s |
| P2.1 | Every in-place upload of a chunk's MultiMesh (an animation frame, a blink) measured its box again and told the lamps, so each shadowed lamp near any animating thing drew its cube shadow every frame. The MultiMeshes have their own box; only a move, a door's open fraction or a refill tells the lamps. Unchanged writes are skipped; a light's shadow switch, range and kind are set only on a change; the own first-person body is paired only with the lamps that cast it. | bench medium 74.5 to 113.5 FPS, high 47.6 to 80.5 |
| P3.1, P3.2, P1.2 | A model's first build decoded its textures (the managed PNG decoder) and hashed every material image and tg sheet to match frames, on the main thread; the bar sign family's 50 models were warmed whole; the atlas copied pixel by pixel. In play the textures and hashes are worked out on the thread pool and the model is built when they are in; frames are compared only for sheets of the image's size; families over 16 variants are not warmed whole; the atlas copies rows. The join starts every decode in parallel. | walk p99 69.9 to 26.5 ms, max 138 to 75, frames over 50 ms 81 to 3, over 100 ms 50 to 0; join 6.5 to 4.3 s |
| P1.3 | The join's things go through the spawn queue (mobs at once). | first frame's entity work spread |
| P2.2 | At most two lamps gain a new shadow a selection (effects at once), a shadow is kept until another lamp is 3 tiles nearer (was 1). Dual paraboloid shadows for far lamps were not taken: their projection bends the 2 m wall quads' edges. | turning round no longer starts several cube shadows at once |
| P3.3-P3.5 | Despawns swap entries out by index (no list scan); the prop atlas's array keeps two spare layers (a new page is one upload); the shut doors' shadow blocks are one MultiMesh per chunk, refilled where a door changed (one level-wide MultiMesh told every lamp to redraw on any door). | no despawn or door frame in the long frames |

After all of them, before the server's part (walk 118 s): 69.5 FPS, p50 13.2 ms, p95 24.0, p99 27.1, max 73.4; 16 frames over 33 ms, 1 over 50, none over 100.
A steady frame's script time is 7.2 ms, 4.3 of it the spawn queue's budget in use while walking; every other section is under 1 ms
(so P4's small costs were measured and left).

**Done on the server (P3.7, P3.8; multiplayer.md section 6):** a session's new entities are queued and spawned nearest the eye
first, 150 a tick (1,000 in the join's Snapshot); the own mob, the mobs, the near chunks' doors, the visuals and what the world made
this tick come at once, a door with its tile's things. The sets are left past a margin (the near chunks at the 5 x 5, the far lights
and doors at 72 tiles, a room 5 s after its door shuts), and what a hold lets go is despawned 300 a tick (the margins made the leaves
rarer and larger: a room set left whole was 7,366 despawns in one tick, 119 ms of the client's Debug frame). Meta,
`InterestGameplayTests`:

| Case | Before | After |
|---|---|---|
| the teleport from the arrivals to the centre, its first tick | 4,843 entities, 188 KB | 268, 9.8 KB; the rest over 32 ticks, no Delta above 7.8 KB |
| the join's Snapshot | 5,307 entities, 422 KB | 1,029, 271 KB; the rest over 30 ticks |
| the teleport's despawns of the arrivals | all in one tick | 1,491 over 5 ticks, 300 a tick |

**Found by the final runs, fixed (2026-10-30):**
- P3.1's waiting models were drawn flat: a thing arriving in play while its model's textures decoded had no instance yet, and
  `MapRenderer.Claim` (which knew only icons still loading) handed it to the entity layer, which drew its 2D picture for good. With
  the server's queue nearly everything arrives in play, so it showed (the owner, 2026-10-30). It now waits for its textures
  (`waited_textures` in the client's report: about 290 things in the walk); the numbers above P3.1's row were measured with them flat.
- `EntityLayer.AddPending` sorted the whole pending set with a comparison delegate each frame, inside its 4 ms budget: in the Debug
  build the sort alone took the budget once the server's queue kept the set full, one thing was built a frame and the set only grew
  (8.1 ms a frame). Each distance is worked out once and sorted by the runtime's key sort (0.6 ms).
- A 13 s freeze under load: `IconLibrary.TryGetIcon` waited for a sheet's background load, which had not started, behind the thread
  pool's whole queue. A load's read is now a `Lazy`: whoever comes first does it, the main thread at once if no worker has started.
- A door's closed fallback picture loaded its sheet with that blocking call while a spawn or a retry could have waited for it
  (`MapRenderer._iconsCanWait`): the door now waits like any model. The join's build still blocks, as a record with no instance has no
  retry there.
- A batch of despawns redrew each window's neighbours per despawn, about 40 ms for the 300 of `DespawnBudget`: the cells and borders are
  now collected and redrawn once a frame (`RedrawAroundWhatLeft`).
- The summary's `max_ms` and `over100` came from `_Process`'s delta, which Godot clamps at about 140 ms: they could not show a longer
  frame. They now use wall time, so the baseline's "max 138" is a capped figure, and its real longest frames are unknown.
- `RealHeadlessWorld.Join` waited out the server's queue for about 25 ticks a player with the world running, and eight gameplay tests
  that count their time from the joins failed. The harness's default options carry a join's whole Snapshot, as before P3.7; a test that
  sets its own options keeps the budgets.

**Final (commit 2a0ae574; M1 Pro, load average about 5; bench medium is the second medium run, the first overlaps the join's stream):**

| Run | Baseline (Debug) | Debug | Optimized (`--optimize`) |
|---|---|---|---|
| join | build 13.5 s | 2.5 s | 2.6 s |
| walk 119 s | 58.3 FPS, p99 69.9 ms, max 138 (capped); 81 over 50 ms, 50 over 100 (capped) | 79.1 FPS, p99 28.8, max 71.7; 5 over 50, none over 100 | 83.0 FPS, p99 28.0, max 49.6; none over 50 |
| bench low / medium / high / max | 160.5 / 74.5 / 47.6 / 27.2 FPS | 167.1 / 117.0 / 82.8 / 58.1 | 174.2 / 117.7 / 84.2 / 54.9 |

A steady frame's script time is 3.5-4.5 ms. P3's goal (no frame over 50 ms in the walk) holds in the optimized build, a player's; the
Debug build's longest frames are first-time work: `map/atlas.Flush` 41.7 ms in the 71.7 ms frame. Under heavy load (about 25) first-time
model builds took 130-190 ms; they did not show in these runs. The first frames of the join are left out of the walk: frame 1 is 320-392 ms.

**Not done, and why:**
- P3.6 (the light grid's upload): never in the measured long frames. P3.9 (fetching ahead): the server's queue made it unneeded.

**The owner's open items, measured (2026-11-01, main 08e2e3ff, optimized client, M1 Pro, load average 4 to 8; temporary switches only, kept
as `tools/perf/results/owner-choices-2026-11-01/temp-prototype.patch`, with the raw lines and screenshots, in the git-ignored results folder):**

*View distance: the upper bound of unloading far client tiles.* The walk on medium, the camera's far plane set, two interleaved rounds:

| Far plane | FPS | p99 ms | max ms | over 50 ms | draws p50 |
|---|---|---|---|---|---|
| 100 m (today) | 80.4, 80.5 | 29.4, 29.4 | 61.7, 61.1 | 7, 4 | 4,530, 4,401 |
| 50 m | 93.8, 86.0 | 29.6, 27.1 | 1,004 (one frame, see below), 60.9 | 10, 5 | 3,663, 3,937 |
| 30 m | 92.6, 88.2 | 25.3, 26.5 | 55.6, 56.9 | 1, 3 | 3,837, 3,811 |

So at most 7 to 17 % FPS (about 1.4 ms a frame), and little for the stalls. The script time falls only from 3.7 to 3.3 ms. The 1,004 ms frame had
2.9 ms of script and 20.7 ms of engine process, no collection, and did not repeat: taken as an outside pause, not explained. The far plane
culls the drawing, not the nodes, so real unloading would also save memory and some engine culling (not measured), and it would add its own
streaming stalls. Going below 100 m shows a hard edge at the cut; the Graphics tab already offers 60 m.

*P2.3, the sun's shadow.* The bench on medium, average of two rounds, interleaved (avg FPS, p99 FPS):

| Sun shadow | Near windows (45,133) | Station centre (131,127) |
|---|---|---|
| off | 119.9, 59 | 110.9, 72 |
| level 1 (2048 px, 40 m) | 116.0, 58 | 108.2, 73 |
| 4096 px, 40 m | 110.9, 59 | 101.0, 69 |
| level 2, medium today (4096 px, 60 m) | 110.5, 57 | 103.8, 74 |

The 15-30 % measured earlier did not repeat: level 2 costs 8 % against no sun shadow near windows, 6 % at the centre, and going to level 1
gains 5 % and 4 % (0.4 ms), with p99 unchanged. The atlas's size is the cost, not the distance. Screenshots at the arrivals and from the hull
outside (`shots/`) show no visible difference between level 2, level 1 and off in those views, so they do not settle the look: no shot found a
surface in direct sun. Level 1 on medium would also tie low, which `ShadowBudgetTests` forbids, so low would have to go to 0.
Recommendation: leave the presets as they are. The owner (2026-11-01): agreed, medium keeps level 2.

*P2.4, baking static models per chunk.* A prototype merged every chunk's plain model parts into one mesh per shadow layer, once, 20 s after the
join, with the per-instance data per vertex. The picture stayed right in the shot checked (`shots/bake-proto.png`). Bench at the centre, three
rounds interleaved (medium), two (high); p50 is read because the merge's stall falls into the prototype's run:

| Case | p50 FPS | p99 FPS | Draws (mean) | Max draws | Video memory |
|---|---|---|---|---|---|
| medium, today | 106.7 | 71.5 | 1,860 | 8,450 | about 497 MB |
| medium, baked | 120.7 | 87.3 | 227 | 450 | about 609 MB |
| medium, models hidden (the ceiling of any gain) | 144.6 | 103.5 | 173 | 350 | about 493 MB |
| high, today | 71.5 | 54.1 | 5,100 | 10,900 | not read |
| high, baked | 76.8 | 61.4 | 471 | 655 | not read |

Draws fall 8 to 10 times, yet a frame gains only about 1 ms: +13 % on medium, +7 % on high. What is left is the geometry (1.17 million triangles
if all were drawn), not the calls. Not measured: the walk, where lamps redraw shadows (draws p50 4,400), may gain more, but the prototype cannot
stream. Baking concerns geometry alone: the lights and shadows stay dynamic, the light grid and the pick (which reads the instance records) are
not touched by it, though the pick was not tested. Costs and risks: about 110 MB more video memory; the whole map took 7.1-7.5 s on the main
thread, about 50 ms a chunk, so each change in a chunk rebuilds a mesh, and nearly everything now arrives in play through the server's queue,
which threatens P3's goal (a thread-pool build and an upload budget are needed); doors, blinking, spinning and hinged parts must stay out or get
per-vertex updates; the shader's floor lift reads local height. Estimate 20-30 h. Recommendation: no-go for now. The owner (2026-11-01): not now.

- P2.3 and P2.4: decided by the owner above (the presets stay, no baking for now); nothing changed.
