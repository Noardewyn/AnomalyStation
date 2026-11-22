# 12. Full 3D: what moving the mechanics off the tiles would take

Status: research note, 2026-11-18, for the owner's decision. Nothing here is decided. The estimates are the lead's guesses and have not been measured.

The question (the owner, 2026-11-18): movement, shooting and interaction still work by tiles, not by distance, and moving between levels follows special-case rules. What would it take for the game to play in real 3D, roughly like Space Engineers, and what would it cost?

## 1. Where the game stands today

The port is more 3D than it looks, but only for the things that were moved off the grid one by one.

| Area | Now | Basis |
|---|---|---|
| Player movement on a level | A continuous capsule. Positions are fixed-point 1/256 tile, with prediction that replays bit-exactly. Collision is against a per-tile solidity grid plus tile faces. The plane is 2D: `MotionState` has `X`, `Y` and an integer level `Z`. | D105, DV15, locomotion.md 1-2 |
| Ghosts, zero gravity, exosuits | Continuous capsules too, but on the level's plane. In zero gravity, the drift is tg's drift loop turned into a velocity. | D160, DV68, DV69, DV358 |
| NPCs, bots, animals | tg's tile steps (`move_manager` loops, about 40 files), drawn as glides. The plan to make them capsules exists but has not started. | memory, free-movement plan |
| Vertical | An integer level, 2 m high. tg's `up`/`down` are on Space/Ctrl for the mobs listed in `ClientSeam.ChangesLevel` (ghost, eye, AI, incorporeal, on a ladder, flying, floating, weightless). Stairs are a ramp only in the drawing and for shots. Falls and lifts are drawn as eased rides. | DV422, DV370, DV371, DV526 |
| Aimed shots | Already 3D: a ray from the eye along the crosshair, hits on body-part pixels, wall and floor hits at height, lag compensation. NPC and turret shots stay tg's 2D. | DV94-DV97, DV526, DV527 |
| Throws, 40mm | Ballistic 3D arcs with a rest point. | DV416, DV528 |
| Reach (click, melee, pick-up) | tg's `Adjacent` on tiles: adjacent turfs, corners via tg's rules. The crosshair only picks the target. | interaction.md 4.3, `_onclick/adjacent.cs` |
| Hearing, sight, ranges | tg's `view()`, `range()` and `get_dist()` on tiles (Chebyshev distance, levels included). | `Builtin/map.builtin.cs` |
| Station, atmos, power, pipes | The tile grid. | by design |

The translated tg code is about 860k hand-written lines. Rough call-site counts: `get_turf` 2,096, `range`/`view` 420 each, `get_step` 374, `get_dir` 305, `get_dist` 295, `Adjacent` 216. Most of these do not need to change (section 3).

## 2. What "like Space Engineers" means, split into parts

Space Engineers is itself a grid game: ships and stations are blocks on a grid. What feels free there is the character and the dynamic things moving through the grid. So the target is not "remove the tiles". It is:

1. **Free bodies.** Every creature and item has a continuous 3D position. Contact, reach and melee go by distance and line of sight, not by turf adjacency.
2. **Real vertical.** Height is continuous: gravity, falling, jumping, walking up stairs and ramps, stepping onto low things, climbing a ladder by moving, flying up and down freely. No list of who may press Space.
3. **Zero gravity with six degrees of freedom.** A jetpack pushes in any direction with inertia and dampeners, the body can roll, and push-off works from any surface in 3D.
4. **3D bodies to look at.** With free vertical views, billboards seen from above or below fall apart (D104's known drawback).
5. **Moving grids.** Ships as physical, rotating grids. This is Space Engineers' signature, and by far the largest part.

The static world stays on the grid: walls, floors, machines placed on tiles, atmos, power, pipes and explosions. The players do not perceive it, and tg's logic and numbers stay intact.

## 3. Why most of the tg code does not have to change

The bridge from D105 is the reason. The body moves continuously, and tg only sees the turf under the body's centre: every crossing is tg's real `Move`, so `Entered`, `Crossed`, slips, traps, doors and atmos exposure all keep working. That pattern extends to 3D:

- **Crossing a level boundary** becomes tg's z-move (`zMove`/`forceMove` into the turf above or below), just as a crossing on the plane is `Move` today. tg's multi-z code (falling damage, open space, `zfall`) then runs from real motion instead of from key rules.
- **Reach and melee** go through a few central places: `_onclick/adjacent.cs` (`Adjacent`), `IsReachableBy`/`CanReach` in `click.cs`, and the client's reach mirror. Replacing tile adjacency with "within R metres of the eye, with a clear ray" there covers most of the 216 `Adjacent` callers at once.
- **`get_dist`, `view()`, `range()`** can stay on tiles for hearing, sight lists, radiation and the like. A tile is 2 m, which is fine-grained enough that nobody notices. Only the places a player feels physically need distances: reach, melee, grabs and pulls, shoves, buckling, and the crush of doors and shutters.

So the work is concentrated in the motion core, the bridge, collision, prediction and replication, plus a known list of contact mechanics. It is not spread over the 860k lines.

## 4. The work, in stages

Each stage is playable on its own. The numbers are calendar weeks at the project's recent pace (roughly 40-70 commits a day), including the owner's play tests and their fixes. They are not measured.

### Stage 1: free bodies on the plane (1-2 weeks)

This is the free-movement plan that already exists.
- Every `mob_living` becomes a capsule. tg's move loops set a steering target instead of stepping tile by tile, and tg's pathfinding stays on tiles. Idle capsules are skipped, and a capsule grid indexes them per tile.
- Reach becomes a radius from the eye with a line-of-sight ray (`Adjacent`, `CanReach`, the client mirror), and melee follows it.
- Dropped and placed items keep a continuous rest point, extending DV416's rest point to drops and placement.
- NPC and turret shots get DV94's 3D line, aimed from the shooter's eye at the target's body.

Benchmark first: Meta with all mobs as capsules, measuring server tick cost and bandwidth. Changes nothing in the owner's earlier decisions except tg's one-tile reach.

### Stage 2: real vertical (3-5 weeks, the core of the request)

- `MotionState` gets a continuous height and a vertical velocity. The integer level stays as tg's storage (the `loc` turf at the body's feet).
- Collision becomes 3D, but stays a grid. Each tile gets a height profile: floor height, solid span up to the ceiling, the stairs' slope, the tops of tables and crates, and the heights of railings, windows and low walls. Mobs are vertical capsules. This stays the controller's own fixed-point code, so prediction stays bit-exact (the D314 condition: no mispredictions from movement). A physics engine would break that and is not needed for capsules against a grid.
- Gravity, jumping, falling through open space as motion, stairs walked as ramps, step-up onto low things (replacing D239's climb flag), ladders climbed by moving, and flight up and down for flyers and ghosts. `ChangesLevel` and Space/Ctrl as level keys go away (DV422 is revised).
- Aimed shots and throws already work in 3D. They need the new height of the body instead of a body standing on its level's floor.
- The client draws the replicated height directly, so the eased rides of DV371 shrink to lifts only.
- A known constraint: a level is 2 m high (D33) and a person is 1.8 m tall, so a jump inside a room has almost no room. Either the jump stays small or the level height grows, and a taller level changes how every map looks.

### Stage 3: zero gravity with six degrees of freedom (2-3 weeks)

- A weightless body moves with 3D inertia. The jetpack's thrust and stabilizer become forces with dampeners, and push-off works from any surface within reach. This replaces tg's tile drift (DV69) with continuous Newtonian motion: a large deviation from tg's logic (D86), though tg's numbers (thrust, gas use) can be kept.
- The camera can roll and turn freely while weightless and levels out again in gravity. This also needs a comfort option.
- A magnetic walk on walls and ceilings would be a new mechanic tg does not have. Leave it out unless the owner asks.

### Stage 4: 3D bodies (2-6 weeks, mostly art; can come later)

Billboards turn in 90-degree steps and are flat from above (D104). Stages 2 and 3 show bodies from every angle, so this becomes visible. The `exp/3d-player-model` branch is a starting point. This is the C2 revisit that D104 already foresaw.

### Stage 5: moving ship grids (months; not recommended now)

Shuttles as physical, rotating bodies carrying atmos, power and crew would rewrite tg's shuttle system (docking and transit levels), and also the rendering, collision against a moving frame, and prediction on a moving frame. It is a project the size of a milestone ladder, and it belongs after the core version.

## 5. Total and risks

- **Stages 1-3, a character that plays like Space Engineers inside a grid station:** about 6-10 weeks. Stage 4 runs in parallel with the art, and stage 5 is separate.
- **Server cost.** 3D capsules for every mob on Meta, every tick. Stage 1's benchmark answers this before anything else is built.
- **Prediction.** A 3D controller must replay bit-exactly on the client. It is the same technique as now, but with more state, and every new contact (stairs, ledges, railings) is a new source of mispredictions to test.
- **Deviation from tg (D86).** Reach, melee, the zero-gravity drift and level changes move from tg's rules to our own. tg's balance assumes a one-tile reach, so the reach radius needs a decision. Each of these is a new DV entry.
- **Decisions this revises:** DV422 (the owner chose movement on each level's plane and Space/Ctrl to change level; the memory notes said not to propose 3D flight again, but the owner's question reopens it), D146 and D160 (the zero-gravity model), D239 (standing on tables), D104 (billboards, at stage 4), and D105's 2D plane.
- **Gameplay tests.** Each stage needs its own gameplay tests (stairs, falls, jetpack flight, reach) so that regressions in prediction show up.

## 6. Questions for the owner

1. The target: stages 1-3 (a free character in a grid station), or also moving ships (stage 5)?
2. The reach radius for hands and melee: how many metres instead of tg's one tile (one tile is 2 m)?
3. Jumping: a small hop under the 2 m levels, or taller levels?
4. Zero gravity: full six degrees of freedom with roll, or free 3D flight with the horizon kept level?
5. Should 3D bodies come with stage 2, or after it?
