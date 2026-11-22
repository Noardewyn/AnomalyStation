# Locomotion: the capsule controller, the bridge to tg's Move, prediction

Status: v3 (M4c slice 7: zero gravity's slides and own view, sections 2-4), 2026-10-15; v2 (M3 P5: faces, section 4.1), 2026-10-08; v1 was M2 P4, 2026-10-07. The design as built: the engine-free motion core (`src/Core/Motion`, `Anomaly.Core.Motion`),
the server's locomotion bridge (`src/Core/Tg/Infrastructure/Locomotion/`, with the tg-side procs in
`src/Core/Tg/modules/mob/mob_movement.capsule.cs`) and the client-side predictor. Decisions: D105 (the bridge, design
B1), D106 (speed), D103 (own-capsule prediction), D51 (solid players, prone ones are not), D31 and D64 (determinism).
Deviations: DV15, DV16, DV17 (`docs/ai/DEVIATIONS.md`). Basis: the M2 scout's report (scope 2a-2d) and the lead's plan.

## 1. Units, state and invariants

- **Positions** are in tiles on one map level, BYOND's coordinates: the centre of the tile with BYOND x is exactly x, so
  tile x covers `[x - 0.5, x + 0.5)`. East is +x, north is +y. A tile is 2 m (D33).
- **Quantized state.** Positions are stored as integers in 1/256 tile ("subunits": the tile centre x is `256 * x`;
  `MotionMath.TileOf(q) = (q + 128) >> 8`). Every step ends on the grid, so every replay starts from the same bits.
- **Time** is the server's tick (20 per second, 0.5 ds). Speeds are tiles per tick.
- **Yaw** is a `ushort` compass bearing: 0 north, 16384 east, clockwise.
- **`MotionState`**: `X`, `Y`, `Z`, `Yaw`, `Posture` (tg's `body_position`), `Driver` (`Grounded`, `Prone`, `ZeroG` (M4c
  slice 6: a weightless mob moved by tg's tile steps, the capsule only follows `loc`); `Thrown` reserved), `Slide` (a tg-made displacement in progress: target and ticks left), `PressTimer`
  (ticks until the next attempt into a blocked face).
- **`MotionParams`**: `TilesPerTick = 0.5 / cached_multiplicative_slowdown * MOVE_SPEED_SCALE` (a slowdown under one tick
  counts as one tick), `Radius = 0.175` tile (0.35 m), `CanMove` (tg's gate this tick), `MoveDelayTicks =
  max(1, floor(slowdown / 0.5))` with waiting-model.md's epsilon. Running (`RUN_DELAY` 1.5 ds) is 1/3 tile per tick, 85
  subunits, 13.3 m/s; walking (`WALK_DELAY` 4 ds) is 1/8 tile per tick.
- **The invariant** (D105): at the end of every tick the tile of a registered capsule's centre is its mob's `loc`; while
  a slide runs, the slide's target tile is (tg already moved the mob there; the centre is on its way).
- **Determinism.** The controller does its collision work on the integer position; floats appear only where the wish is
  scaled and where a push-out direction is normalized, with `+ - * /` and `sqrt` only, and are quantized at once. The
  assembly references nothing and compiles optimized in every configuration. One machine: a replay is bit-equal to the
  server's step (pinned by `MotionPropertyTests.ARecordedRunGivesTheSameBitsInEveryBuild` in Debug and Release); across
  x64 and arm64 D64 applies (the server corrects).

## 2. The controller (`MotionStep`)

`MotionStep.Step(ref state, in input, in params, view, events[, selfId])`, one tick of one capsule, in this order:

1. `PressTimer` counts down by one; the yaw becomes the input's (also during a slide: looking is not moving).
2. **Slide.** A running slide moves the capsule an equal share of what is left toward its target (integer division, the
   last tick lands exactly on it) and ends the step: no wish, no collision, no event (tg already moved the mob).
3. `Thrown` (reserved) and `ZeroG` stop here: tg's tile steps move those mobs and the capsule only plays their slides.
4. **Wish.** When `CanMove`, `(MoveX, MoveY)` with its length clamped to 127, times `TilesPerTick / 127`, quantized. No
   momentum: the speed is constant, the same in every direction (tg's diagonal step is about 6 % faster: DV15).
5. **Tile sweep**, north/south first, then east/west (tg's diagonal order). Against tiles the capsule is the square of
   half-side `Radius` (45 subunits): its leading face may reach a blocked line but never pass it. A line is blocked when
   the tile across it is solid (`ICollisionView.IsSolid`; outside the map is solid) or the face is blocked
   (`EdgesOf`, M3's border objects; `None` in M2). The other axis keeps its motion: the capsule slides along walls.
6. **Press.** An axis stopped at a blocked face of the centre's own tile (not by a diagonal neighbour's corner), with the
   wish within 45 degrees of the face's normal (`|along| >= |across|`; an exact diagonal into a corner presses both
   faces), is a press. When the timer is 0 every pressed face gives a `Pressing` event (north/south first) and the timer is
   set to `MoveDelayTicks`, so tg's `Bump` comes at tg's move delay, the first one at once (as tg's `client/Move` tries
   the wall every `move_delay`).
7. **Capsules** (a `Grounded` capsule that may move; a `Prone` one takes no part, D51). The view's standing capsules near
   the path (in id order) are circles. A capsule touched (within one subunit) while the wish points toward its centre
   is a `Contact` event. Then the mover is pushed out of every capsule it overlaps, along the line of the centres (a
   coincident centre backs out along the wish, else north), up to four passes, each push swept against the tiles; if the
   result still overlaps a capsule and the start did not, the capsule keeps its start (no motion into a body). A step as
   long as two bodies' touch distance (90 subunits; a run is 85, so only a slowdown under the run delay or
   `MOVE_SPEED_SCALE` above 1 gets there) could land past a capsule's centre and be pushed out on its far side, through the
   body: such a step is cut into equal pieces, each shorter than that distance (a tile a tick at scale 4 is six), and every
   piece is swept against the tiles and pushed out in turn (`PiecesOf`; M2 T2).
8. **Crossings.** Every tile border the centre crossed between the start and the end is a `Crossed` event, one cardinal
   step each, in the order the straight path meets them (exact rationals; a tie, an exact corner, gives north/south
   first). If that order would step through a solid tile or a blocked face (the path cut a wall's corner that the sweep
   went around), the sweep's own order is used (north/south first, else east/west first).

Events come as: the crossings, then the presses, then the contacts. `ClampToTile` puts the centre back inside a tile
(kept off its faces by the radius) and ends a slide. `Displace` starts a slide to a neighbouring tile's centre (cardinal
or diagonal: tg's diagonal `Move` is two cardinal steps in one call) over a glide time, and snaps anything else (a farther
tile, another level, a glide time of 0). M4c slice 7: the bridge lets a weightless capsule's `Displace` reach two tiles (tg can
move it twice in one tick, a client/Move step and the drift's first step, which BYOND glides from where the mob is drawn), over
the glide time of each tile; a walking capsule keeps the one-tile rule.

`MotionFacing.DirOf(yaw, current)` gives tg's `dir`: the current cardinal direction while the yaw stays within 45
degrees plus 5 degrees (910 yaw units) of it, else the nearest (M1's `InputTracker` rule).

**Movement feel (DV263, protocol version 66).** `MotionParams.Feel` (`MoveFeel`: `AccelTicks`, `DecelTicks`, `BackSlow`,
`StrafeSlow`; all zero is tg's behaviour) shapes a walking capsule (`Grounded`, `Prone`) on the server and in the
prediction alike, so a replay stays bit-exact:
- Direction: the clamped wish is split along the view (`MotionMath.Facing(yaw)`, sine and cosine from `+ - *` only) into a
  forward part and a side part; a backward part is scaled by `1 - BackSlow`, the side part by `1 - StrafeSlow`.
- Ramp: the state carries a velocity (`VelX`, `VelY`, 1/65536 tile a tick). Each tick it moves toward the target (the
  wish at the speed): within a quarter turn of the velocity the direction is taken at once and only the speed changes,
  by at most `full / AccelTicks` up or `full / DecelTicks` down; no wish, or one more than a quarter turn away, brakes the
  velocity toward the target by at most `full / DecelTicks`. The step moves the velocity rounded to subunits.
- The velocity drops to zero when the capsule cannot move (`CanMove` false), slides, leaves the walking drivers, is
  displaced or clamped by tg (`Displace`, `ClampToTile`), or wanted to move and did not move at all (a wall or a body
  ahead); a slide along a wall keeps it. A ramped capsule presses into a face by its motion, not by its keys.
- The server's default is `MoveFeel.Default` (`ServerConfig.MoveFeel`, the dev command `movefeel`); Core worlds and the
  tests that build parameters themselves have none.

## 3. Prediction (`MotionPredictor`, `InputClock`)

- The client pushes one frame per tick of its input clock (`Push`: the next seq, a step of the prediction with the newest
  parameters the server sent and the client's collision view, the frame and the state after it kept in a ring of 64) and
  sends `CopyRecentFrames` (the newest four frames pushed, acknowledged or not) on the unreliable Input channel.
- On every private block it calls `Reconcile(state, lastInputSeq, params, view)`: an older acknowledgement is ignored; the
  acknowledged frames are dropped; the server's state is replayed through the frames after it; the result replaces the
  prediction. The error is the distance between the old and the new prediction of the newest frame: under 1 cm
  (0.005 tile) nothing is shown (the new bits are taken), up to a tile the visible error (`VisualOffset`, the old minus the
  new position) decays linearly to zero over 100 ms (`Decay`), more than a tile (or another level) snaps.
- `PredictionCounters`: reconciliations, corrections (an error of 1 cm or more), snaps, mismatches (any bit differs,
  also under 1 cm) and carried (an error of 1 cm or more while `CanMove` is false: in a pod, buckled, stunned, the server alone
  moves the body, so it is no misprediction and is counted apart from the others), the largest error, ring overflows. Walking alone gives zero corrections and zero mismatches
  (`MotionPredictorTests.AReplayThroughTheSameStepGivesTheServersBitsWithLatency`).
- A tg-made displacement arrives as the state's slide and is replayed as the same slide. The camera's partial step of a
  running slide is its share times the part of the tick in progress, whatever the keys (M4c slice 7; before, a held key
  stepped the slide a whole share ahead). While the server's state slides, an error of up to 3 tiles (a diagonal pull, a
  weightless two-tile move) is smoothed, not snapped (2026-10-26).
- **The wind and gravity pulls** (protocol version 72, DV357, the owner's play test of 2026-11-06: every push was a correction and a
  jerk). tg's wind step and the pulls' `step_towards` of a walking capsule mob are not taken; the bridge carries the capsule instead
  (`LocomotionSeam.Wind.cs`): SSair's pass notes each blowing turf's cardinal direction (`NoteWind`, the field is the last finished pass's),
  `experience_pressure_difference` sets the mob's push speed (`Blow`: `move_prob` percent of a tile per air pass, up to 400), and a pull
  its tile and the pace its steps come (`Pull`). The parameters carry them (`Wind`, `Pull*`), `MotionStep` adds them to the wish of a
  walking capsule, so the predictor steps them as the server does; an error up to 3 tiles is smoothed while they are set, and the
  correction shake skips them.
- **Plastic flaps** (protocol version 73, DV360, the owner's play test of 2026-11-07: walking into airtight flaps was corrected). The
  flaps are not dense, so their tile is not solid, but tg's `CanAllowThrough` refuses a standing mob: the client walked in and the
  server's refused `Move` clamped it back. A tile with flaps that `require_resting` has `LocomotionSeam.IsFlaps` (MapGrid's
  `CellFlags.Flaps`), and `MotionStep` gives a standing capsule without `MotionParams.PassesFlaps` (`LocomotionSeam.PassesFlaps`:
  `PASSFLAPS`, `PASSGLASS`, a mulebot's passenger, a tiny mob, a ventcrawler) a view where that tile is solid, on the server and in
  the prediction alike. A lying capsule (its own `Posture`) crawls in; leaving a flaps tile is free; tg's `CanAllowThrough` stays the
  truth for the crossing (a `PASSGLASS` mob's 40% refusal is a correction).
- **Gravity's edge** (protocol version 48, 2026-10-26). The parameters carry the 7 x 7 tiles around the mob where it would
  be weightless (the bridge's `UsesZeroG` there) and its speed on the other kind (`update_movespeed` with `FLOATING`
  flipped: the jetpack's full speed); before each predicted or replayed frame `MotionPredictor.InGravityOf` turns the
  driver and the speed from the capsule's tile as the bridge does at the start of its step, so flying off a floor into
  space and back needs no correction. Whether the gate lets a weightless mob steer (a push-off backup) is not predicted.
- **Climbing** (the owner's play test of 2026-10-27). A step off a table or crate onto a tile without one takes the
  climber's `OnClimbable` away in the predictor (`climb_walkable`'s `on_climb_exit`), so the table stops the next frames
  as it stops the server's. tg's climb itself (`do_climb`'s `forceMove`) is the server's move: a reconciliation whose
  parameters newly say `OnClimbable` counts as carried, and the client does not shake the view for it.
- **Stepping off a floor into space** (protocol version 50, the owner's play test of 2026-10-27: leaving a platform and
  reaching another were corrected). With a gravity mask the parameters say whether tg's gate lets the mob steer on a
  weightless tile with nothing to push off (`OtherCanMove`: `PreviewSpacemove`'s answers that need no backup, a lit
  jetpack's above all) and the speed of a drift from rest (`InertiaTilesPerTick`: `get_loop_delay` of a 1 newton
  `drift_handler`). On a weightless tile reached from a floor the predictor refuses the wish when the mob may not steer and
  nothing around is solid or under its feet (`get_spacemove_backup`; dense movables that are no solid tile are not looked
  at), and the frame that steps off the floor starts that drift along the step, as tg's `Moved` and the bridge's
  `newtonian_move` do. A drift that glides to a halt on a floor blocks the input for its glide (`drift_handler`'s
  `block_inputs_until`); the parameters carry the first next tick whose gate it no longer holds (`UnblockTicks`), from
  which the predictor lets the held wish move. Measured (`MispredictReproTests`, a 4-tile gap, with and without a push-off
  wall or a lit jetpack, at 0 and 100 ms): no correction leaving, crossing or landing.
- **Walk and run** (protocol version 49, the owner's play test of 2026-10-27: toggling while moving was corrected). The
  parameters carry tg's speed and move delay after `toggle_move_intent` (`IntentTilesPerTick`: the `config_walk_run` modifier
  swapped for the cached other one). The client tags the toggle action with the frame it has not pushed yet, which the server's
  command phase applies before the locomotion phase steps that frame, and `MotionPredictor.ToggleIntentAt` steps that frame and
  the later ones at the other speed until the server's parameters after it arrive. A toggle by the HUD's button is a click and
  is not predicted.
- **Zero gravity** (M4c slice 7, D146). The client predicts no step of a weightless capsule: tg's `client/Move` (its
  `move_delay` with the diagonal's `FLOOR(delay * sqrt(2), tick_lag)` and the `old_move_delay` carry), `Process_Spacemove`
  (the push-off's `get_spacemove_backup`, the jetpack's stabilizer and gas) and the drift handler (which blocks the next
  `client/Move` while it drifts) decide every step on the server, and most of that (the backup around the next tile, the
  drift) the client could only guess, so a prediction would be corrected after many steps (the measured S6 baseline: 6
  corrections and a snap in a push-off drift, 11 in an 11-tile flight, each with a camera shake). Instead a reconciliation
  whose state or old prediction is `ZeroG` takes the server's state as it is and counts it in
  `PredictionCounters.ServerDriven` (no correction, no mismatch, no visible offset), and `OwnPrediction` draws the own body
  along the server's path (`OwnGlide`, smoothed after the space play test of 2026-10-15: BYOND's glide toward the newest
  tile stopped for a frame or two at every tile, because tg's steps come 3 and 2 ticks apart for a 2.4-tick delay, and
  changed speed at every tile of a diagonal or zigzag drift and in bursts while a drift sped up). The states' tiles (a
  slide's target, or the centre) make a polyline (a diagonal move of two tiles in a tick is one straight segment; a zigzag
  drift's corners are rounded by averaging the path 0.5 tile either side of the drawn point); the drawn point travels it at
  tg's `glide_size` of the own mob (the replicated `GlideSize`, pixels a tick on each axis as BYOND glides, turned into
  speed along the recent segments; the path's measured rate, else the server slide's, when it is unknown), eased over
  0.1 s; it aims at a schedule line (where the server's steps begin, advancing at that speed, pulled 35 % of the way to each
  new step's start) less a 50 ms margin, closes on it with a 0.15 s time constant, changes speed by at most 60 tiles/s²,
  never passes the newest tile and brakes onto it (400 tiles/s²) when the steps stop. So it is about one step behind the
  server's tile, as BYOND's glide is, and a step that comes a tick early or late bends the aim instead of stopping the body.
  A target more than 2.5 tiles from the path's end or on another level is taken at once; a body more than 5 tiles behind
  (a stall) jumps to a tile behind. Measured per frame (`ZeroGSmoothingTests`, 120 fps): the largest change of the drawn
  speed between two frames fell from 101 % of the mean (a stop at every tile) to 6 % straight and diagonally, from 33 % to
  9 % on a fast zigzag drift and from 71 % to 5 % on a drift speeding up; the view is about 100 ms behind the server's tile
  (it was about 50 ms). Leaving zero gravity, the
  prediction takes over from where the glide drew the body (`MotionPredictor.Blend`: the difference decays as a
  correction's, uncounted). The cost is the round trip: the own body moves when the server's state arrives, as a BYOND
  client's does (measured by `ZeroGPredictionTests`: 0 corrections; the first drawn motion 67 ms after the key at 0 ms
  one way, against 17 ms walking; the view about one tick behind the server's capsule at 0 ms and two at 100 ms).
- `InputClock` sends one frame per server tick of its own time, nudged by up to 5 % so that about `TargetDepth` frames wait
  in the server's jitter buffer: the rate follows a running average of the reported depth (each report moves it a tenth
  of the way; one frame off is half the nudge, two the whole of it). One `Advance` returns at most four frames.

## 4. The bridge (`LocomotionSeam`)

**Install.** `LocomotionSeam.Install(world)` adds two world phases after the host's command phase: `locomotion`, the step
of every registered capsule, and `locomotion follow`, which runs no tg code and only makes every capsule follow its mob's
`loc` again after the drain that follows the first phase (so a `loc` change by zero-delay work the phase issued still
leaves the invariant true at the end of the tick). It registers the seam's state hash contributor (it adds nothing while
no capsule is registered and the scale is 1).

**State** (per world, a Kernel extension; hashed): the capsules in `dm_id` order, each with its mob, its `MotionState`,
its last frame (repeated when none came; a mob that never got one keeps tg's `dir`), tg's slowdown, the parameters for
the next tick (the private block, `TryGetState`), the contact cooldowns per other capsule, and a busy flag; the speed scale
(`SetSpeedScale`, a positive number up to 4, D106).

**The phase**, per capsule in `dm_id` order, each step its own detached call (an isolation boundary, run with `usr` the
mob, as BYOND runs `client/Move`; a step that slept is skipped until it returns):

1. Deleted mobs leave the seam. The posture follows tg's `body_position` (`LYING_DOWN`: lying and prone).
2. **Follow** (the displacement, scope 2a "moved by others"): if `loc` is not the capsule's tile, tg moved the mob (a shove,
   `forceMove`, a teleport, `PushAM`'s follow): a neighbouring turf slides the capsule over tg's glide time for one tile
   (`ceil(32 / glide_size)` ticks: 4 at tg's default `glide_size` 8), anything else snaps. A mob inside an object stands
   on the outer turf and takes no part in collisions; a wish then only runs the gate (tg's relay to the container). When the
   object that carries it moves (a transit tube pod, a pulled locker), the capsule slides with it over that object's glide
   time, its slide stepped each tick as the client's replay steps it (the owner, 2026-10-27: riding a tube mispredicted at
   every tile).
3. **Facing**: `MotionFacing.DirOf(frame yaw, dir)`; `setDir` only on a change (DV15: `dir` follows the view), and only
   where tg would turn the mob: `face_atom`'s rule (not buckled, not `IS_UNCONSCIOUS_OR_CRIT`, `click.dm:426`) or a wish
   the gate's preview lets move (a soft crit crawl). A dead, unconscious or critical body keeps its `dir`.
4. **The gate** (`client/capsule_move_gate`, `mob_movement.capsule.cs`): `client/Move`'s checks in tg's order (`!direct
   || !new_loc`, no `loc`, `TRAIT_NO_TRANSFORM`, the non-living signal, `DEAD` (tg's `ghostize` is a deferred leaf until
   M10), `COMSIG_MOB_CLIENT_PRE_LIVING_MOVE`, incorporeal movement, remote control, the AI, `Process_Grab`, buckled,
   `MOBILITY_MOVE`, inside an object, `Process_Spacemove`, `COMSIG_MOB_CLIENT_PRE_MOVE`), minus the `move_delay`
   bookkeeping and the move itself (DV15); the `move_delay` test (`:47`) stays, since only tg's stops push it for a capsule
   mob (`Process_Grab`'s 1 second, a failed `resist_grab`'s 4 seconds). It runs each tick the wish is not zero, with the
   wish's BYOND direction (a component counts when it is more than 22.5 degrees off the other axis) and the turf it points
   at; TRUE lets the capsule move. The private block's `CanMove` is a side-effect-free preview of the same checks (no
   signals, no grab roll: an aggressive grab previews as held), and false after a tick whose gate refused the held wish.
   **Gravity (M3 R4).** `Process_Spacemove` passes at once only with gravity (`has_gravity`); without it tg looks for
   something to push off (`get_spacemove_backup`, not translated until M4c), so a weightless mob does not move. tg's station
   areas have no gravity of their own (`/area` `default_gravity = ZERO_GRAVITY`); a station level gets it from its gravity
   generator (`modules/power/gravitygenerator.cs`: the generator registers in `GLOB.gravity_generators["z"]` and
   `SSmapping.calculate_z_level_gravity` sets `gravity_by_z_level`). Before R4 the generator was an inert machine, so Meta
   was weightless: the server refused every step and the client's prediction was rolled back to the spawn (the owner's
   `corrections 374`). On Meta now (`MetaWalkTests`, the real client stack headless): a route through the arrivals corridor,
   an access door, two corridors and an airlock gives 2 corrections, both on a door's edge (the client predicts the closed
   door as solid until its opening arrives), none in the corridors.
   **The graviton field (M4c slice 8).** The generator's `enable` makes tg's field (`gravitygenerator.dm:345`,
   `/datum/proximity_monitor/advanced/gravity` with range 2 and gravity 6, its edge counted as the field): every turf within
   two tiles of the generator gets the bespoke `forced_gravity` element, whose `COMSIG_ATOM_HAS_GRAVITY` and
   `COMSIG_TURF_HAS_GRAVITY` answers make `has_gravity` 6 there (the very high gravity alert, the gravity movespeed modifier
   of +5 slowdown and tg's crushing brute in `handle_high_gravity`; the motion blur filter waits for filters, a not-translated
   row). `disable` deletes the field and takes the element off. The proximity monitor hooks the turfs in its range through
   `connect_range` and its host's containers through `connect_containers` (both translated whole).
   **Magboots over open space.** tg's magboots negate gravity only on ground (`_species.dm:1365`, `!isgroundlessturf`): over a
   space tile, lattice and catwalk included, a magbooted mob is weightless and drifts like any other (the M4c demo check
   anchors on the airless plating of `/area/space/nearstation`, `M4cScenarioTests`).
   **Zero gravity (M4c slice 6, D146: "zero-g movement is tg's own tile steps and drift, slid smoothly by the client").** A
   mob on a turf without gravity (`has_gravity` false) that is not buckled, not the dev noclip and not one tg lets move freely
   there (`TRAIT_SPACEWALK`, `FLYING`, `TRAIT_FREE_FLOAT_MOVEMENT`: these keep the capsule) gets the `ZeroG` driver
   (`LocomotionSeam.UsesZeroG`). Instead of the gate the bridge then runs tg's whole `client/Move` each tick the wish is not
   zero, as `keyLoop` calls it (`bindings_atom.dm:27-35`: the camera turn, `get_step` of the held direction): tg's own
   `move_delay` sets the cadence, `Process_Spacemove` decides (the push-off, a lit jetpack's `COMSIG_MOVABLE_SPACEMOVE`
   answer), `glide_size` and `visual_delay` are set, and BYOND's `Move` moves the mob; the drift (`drift_handler`) moves it
   between. The capsule does not move by itself: every `loc` change is a slide over tg's glide time (Follow), and the
   private block carries `ZeroG`, so the client predicts no step of its own and draws the own body by tg's glide
   (section 3). Follow lets such a capsule slide a move of two tiles (a step and the drift's first step in one tick) over
   the glide time of each tile instead of snapping (M4c slice 7). DEVIATION(DV15): that `Move` keeps the dir the view gives (`client.dm_move_keeps_dir`).
   `CanMove` of a `ZeroG` capsule leaves the `move_delay` test out (client/Move's own steps push it) and asks the jetpack
   without sending the signal (`PreviewSpacemoveSignal`: its stabilize answer to a held step, the gas read without
   `allow_thrust`'s turn-off).
   **intended_direction.** Each tick the bridge sets the client's `intended_direction` to the wish's BYOND direction (tg's
   `calculate_move_dir`, `bindings_atom.dm:40`), which the jetpack (`components/jetpack.dm:136`, `:192`), the mob swap
   (`living.dm:310`) and the scope read; `keyLoop` (SSinput) leaves a capsule mob alone, so it is never stepped twice.
5. **Step** with tg's current slowdown, the speed scale and the server's collision view (`IsSolid` and the registered
   standing capsules on a turf; the mover left out by its id).
6. **Events**, in order, as tg calls (D105):
   - `Crossed`: tg's real `Move(new turf, dir, 0, update_dir = FALSE)` with `PASSMOB` added to the mob for that call and
     taken off again if it was not there (DV16: tg's own swap trick, `living.dm:232-245`, so a turf with a standing mob is
     entered, not bumped); when the mob arrived, `client/Move`'s tail (`capsule_move_tail`: `throwing.finalize`,
     `COMSIG_MOB_CLIENT_MOVED` with the crossing's direction and the old `dir` and `loc`, the pull turn). A refused Move (a
     stale grid, a `PRE_MOVE` veto) clamps the capsule back into its tile and ends this step's movement; a mob tg put
     somewhere else ends it too (the capsule follows).
   - Pulling (DV48): before each step of a mob that pulls something the bridge calls tg's `update_pull_movespeed` (tg's living `Move`
     does before every tile step), so a pull's slowdown is in the step's speed and the next parameters from the tick the pull starts;
     the crossing `Move` sets the mob's `glide_size` from the capsule's delay for a tile first (`client/Move`'s `:132`, "in case of
     pulled objects"), so the pulled thing glides a tile behind at the puller's speed; and the tail's pull turn faces the dense thing
     pulled (the crossing's direction turned around), which the facing step then leaves alone (`PullTurnsAround`).
   - `Pressing`: `Move` into the blocked tile, the same way (so `turf/Enter` fails and `Bump` runs: walls, crates with
     `PushAM` in later milestones, airlocks in M3). A press that moves the mob (a stale grid) is followed.
   - `Contact`: `capsule_mob_bump` (MobBump's lines up to its swap and push, `living.dm:178-203`: the walk-intent stop,
     `COMSIG_LIVING_PRE_MOB_BUMP`, `COMSIG_LIVING_MOB_BUMP`, `COMSIG_LIVING_MOB_BUMPED`, the fire spread (a deferred leaf
     until M4b), the disease spread), at most once per move delay per pair. No swap and no walk-push (DV16, D51).
7. Follow again (the invariant), and the parameters for the next tick.

**Solidity** (`LocomotionSeam.IsSolid`, the one predicate of the controller's view and of `MapGrid` version 2's bits): a
tile is solid when it is outside the map, its turf is dense, or a dense movable that is not a mob stands on it; an
`ON_BORDER_1` object blocks a face, not the tile (M3). Mobs are capsules, not solid tiles. It reads the map raw and works
inside the world and after `World.Step`.

**Who is a capsule.** Every living mob that should be solid to players is registered: the players' mobs at join, and the
dev spawn's dummies (with no client and no frames they stand still and keep tg's `dir`). An unregistered mob is no capsule
obstacle (the crossing's `PASSMOB` passes its turf).

**Shove (DV17).** `disarm` (M2 P1) reads the capsule centres through `TryGetState` for the direction; the target's `loc`
change is followed by the bridge as a slide of the full tile over the target's glide time (a diagonal shove too).

### 4.1 Faces (M3 P5)

An `ON_BORDER_1` object blocks a **face**, not the tile. `LocomotionSeam.FacesOf(world, x, y, z)` is the union of
`TileFaces.OfDir(dir)` of every dense movable on the turf that is not a mob and has `ON_BORDER_1` in `flags_1` (a window facing
north blocks the north face; a diagonal `dir` blocks two). `LocomotionSeam.IsSolid` leaves such an object out, so a tile with only
panes stays walkable. `ServerView.EdgesOf` is `FacesOf`; `ReplicaWorld.CollisionView.EdgesOf` is the replica's `FacesOf` (the flags
byte of MapGrid version 3 and the Delta's cell flag changes); the bots' `SelfExcludingView` and the client's `OwnPrediction` wrap a
view and forward `EdgesOf`, so all four read one source per side. The face invariant is `TileFaces`': a face is blocked when
either tile's `TileEdges` has it toward the other, and the controller crosses a border exactly when the neighbour is not solid and
the face is not blocked, on the server and in the predictor alike. `MotionStep` already read `EdgesOf` on both sides (sweeps,
presses, crossings); M3 changed nothing in it.

**What a blocked face means for tg.** Directional windows (`structure/window` with `fulltile = FALSE`), windoors
(`machinery/door/window`), railings and plastic flaps' frames have `ON_BORDER_1`; their `CanAllowThrough(mover, border_dir)` and
`on_exit` decide the crossing `Move` for everything that is not a capsule and for the capsule's crossing as well (a refused `Move`
clamps the capsule back, section 4's rule for a stale solidity bit). The face bits are the *controller's* prediction of that: the
capsule stops at the face with a `Pressing` event (tg's `Bump` at the move delay, so a windoor opens for a bot with access) instead
of crossing and being clamped. A pane in the way of an `ON_BORDER_1` object that is open (a windoor with density 0) is no face.

**Pinned by tests.** `Anomaly.Server.Tests/Replication/FaceMotionTests`: a joined human walks a hand-made room with panes on tile
borders and a pane built after the join, the predictor steps the same inputs over the replica's view, and the two end every tick in
the same `MotionState` (also a 30-leg random walk); `Core.Tests/Motion/FaceSweepTests` (faces from either side, over many ticks);
`ReplicatorV3Tests` (the replica's faces equal `FacesOf` for every cell after any change). A plain `/obj` stands in for the pane:
tg's own `CanAllowThrough` for entering a pane's tile from a free side is P4's, so the fixture does not enter pane tiles.

### 4.2 Exosuits (DV358, D321)

An exosuit registers a capsule of its own (`RegisterMech` at `Initialize`, `MechCapsule`, radius `MotionMath.MechRadius`), stepped in
`RunMechs` after the walkers. Its driver's capsule keeps no part in collisions (it rides inside); the mech reads the driver's frame:
`capsule_may_drive` (the driver can drive), then tg's `vehicle_move` head at its `movedelay` cadence (`capsule_drive_gate`) decides
whether the wish moves it this time, and `capsule_can_move` whether it moves at all. The speed is tg's: half a tile a tick over the
`movedelay` (so the overclock and the Ripley's pressure steps change it at once), the feel the walker's. Crossings are `MechMove`:
`Move` with PASSMOB and PASSVEHICLE added, the step sound only once a tile walked (`IsBridging` tells `on_move`); presses are `Move` and
so `Bump`; a touch of another capsule is `mech.Bump(other)` with `capsule_bump_dir` the push direction. The armour's `dir` follows the
driver's view yaw (`capsule_turn`, tg's turn sound) unless `capsule_facing_locked` (strafing). Mechs are not solid in `IsSolid` and
`IsClimbWalkable`: they are capsule obstacles in `GatherCapsules`, on the server and in the client's replica (by type path).

The client: `OwnBody` is the mech while a mob drives or rides it, and the server sends it as the header's own entity, so the client
predicts the mech's capsule (`TryGetState` of an occupant is the mech's, `CanMove` false for a passenger), hides it in first person, and
`CameraLayer.InExosuit` raises the eye to 1.8 m (under the ceiling) with a bob 3.5 times a walker's, a cycle every two tiles.

## 5. The click layer

`ClientSeam.Click(player, target, params)` runs BYOND's side of a map click: tg's `/client/Click` (`client_procs.dm:866`:
the click intercept, the per-minute and per-second click limits, the verb queue check, which never queues with the
virtual tick usage of 0, DV3, and `COMSIG_CLIENT_CLICK`), BYOND's default after it (`Click__d0`: `object.Click`), tg's
`/atom/Click` (`COMSIG_CLICK`, `usr.ClickOn`) and `ClickOn` (`click.dm:71`: `next_click`, the modifier clicks, the
incapacitated check, `face_atom`, `next_move`, `IsObscured`, hands blocked, direct access, reach by `IsReachableBy` and
`Adjacent` on tiles, `changeNext_move(CLICK_CD_MELEE)`: 16 ticks), then `UnarmedAttack` and the right-click chain
(`other_mobs.dm`: `right_click_attack_chain`, `resolve_right_click_attack`, `attack_hand_secondary`,
`resolve_unarmed_attack`, `attack_hand`), with the location the target's turf and tg's map control. The human's
`attack_hand` override that leads to `spec_attack_hand` and `disarm` is M2 P2's and P1's.

## 6. Deviations in practice

- **DV15**: continuous motion at tg's speed instead of `client/Move`'s steps; the gate keeps only the `move_delay` test; no glide or
  `move_delay` bookkeeping for capsule mobs; `dir` follows the yaw; the crossing `Move` passes `update_dir = FALSE`.
- **DV16**: `PASSMOB` on the bridge's `Move` calls; capsules collide, lying ones do not; a contact runs MobBump's signal
  part only.
- **DV17**: the shove on a shared tile, its direction from the capsule centres (P1's `disarm`).
- **Proposed (a number the lead assigns)**: `IsObscured` reads the atom's own turf where tg reads `get_turf_pixel`
  (the 2D pixel offset of the atom's icon, which needs the `.dmi` metadata; a 3D click hits the atom itself).

## 7. Tests

- `tests/Anomaly.Core.Tests/Motion/`: the controller (speed, isotropy, walls, slides along walls, presses at the move delay,
  crossings and their order, corner cuts, blocked faces, the map's edge, contacts, the push-out, prone capsules, slides,
  clamps, the reserved drivers, purity), the properties over 10,000 random paths (never inside a solid tile or a standing
  capsule; the crossings chain the tiles), quantization, the pinned recorded run (Debug and Release), no mutable static
  state, the predictor (bit-equal replay with latency, smoothing, snapping, decay, stale acknowledgements, the ring, the
  redundancy, reset), the input clock, the facing and the ramp.
- `tests/Anomaly.Core.Tests/Motion/ZeroGMotionTests.cs` (M4c slice 7): the weightless driver ignores the wish, a chain of
  tg's steps never jumps, two tiles slide when allowed, the predictor's server-driven reconciliations and `Blend`;
  `tests/Anomaly.Server.Tests/Client/Play/ZeroGViewTests.cs`: `OwnGlide`'s rate and chains, no jump into or out of zero
  gravity, a shove's partial share, the eye's float and lean and their options; `Client/Play/ZeroGSmoothingTests.cs` (the
  space play test): the drawn speed per frame at 60 and 120 fps over a straight, diagonal, zigzag and speeding-up flight
  and with 20 ms of jitter; `Integration/ZeroGPredictionTests.cs`: on
  MetaStation at 0 and 100 ms, a walk, a push-off drift, a jetpack flight and a drifting item, with their corrections,
  lags and drawn speeds.
- `tests/Anomaly.Core.Tests/Tg/M2Locomotion/P4/`: the bridge on a booted toy room with humans (the crossing `Move` and its
  signals, the client/Move tail, the wall's bump cadence, `PASSMOB`, contacts once per move delay with no swap, walk
  intent, lying mobs, a refused crossing's clamp, a teleport's snap and a Move's slide, the gate, the facing, the speed
  scale, solidity, the view, deletion, the invariant over a random walk with three capsules, the state hash) and the
  click chain (right and left clicks to `attack_hand`, the melee cooldown, reach, the monkey path, incapacitation and
  blocked hands, deleted targets and departed clients, turf clicks). The independent goldens are T2's
  (`M2Locomotion/T2/`).
