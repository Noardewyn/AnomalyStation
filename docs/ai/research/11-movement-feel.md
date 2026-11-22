# Movement feel: what we have, what to change (2026-11-03)

**Outcome (same day, D314, DV263):** the owner chose stages 1, 2 and 4 together. They are built as stage 4 describes, in the
server's controller rather than as a client ramp, so the velocity and the feel travel with the prediction and replay bit for bit
(protocol version 66, docs/ai/specs/locomotion.md section 2); `InputRamp` was removed. The client-side options slider of stage 1 is not
built: the server's `ServerConfig.MoveFeel` and the dev command `movefeel ACCEL DECEL BACK STRAFE` tune it. Stage 3 (sprint) waits.

The owner's request: the player starts and stops instantly; there is no build-up or decay of speed; backward speed equals
forward speed; a sprint with stamina may be worth having. This note assesses the current locomotion, compares it with
similar sandbox games and proposes a staged plan. Nothing here is decided: every item that changes tg's behaviour needs
the owner's yes and a deviation entry.

## 1. What the game does now

- **Controller** (`src/Core/Motion/MotionStep.cs`, `docs/ai/specs/locomotion.md`): a kinematic capsule with no velocity in
  its state. Each tick (20 Hz) it moves by the wish vector times `MotionParams.TilesPerTick`; the wish is 0 or full, so
  speed jumps 0 -> max -> 0 within one tick (50 ms).
- **Speed** (D106, DV15, DV70): tg's tiles per second times `MOVE_SPEED_SCALE` 0.5. Run (tg's `RUN_DELAY` 1.5 ds) is
  3.33 tiles/s = 6.7 m/s; walk (4 ds) is 1.25 tiles/s = 2.5 m/s; tg's slowdowns apply (damage and stamina at
  `max(brute+burn deficiency, staminaloss) >= 40`, `human.dm:962`; crawling; limps). Same speed in every direction (DV15).
- **Walk/run**: tg's move intent, toggled with C. In tg walking has real effects: no slip on wet floors, caltrops,
  tripwires and motion flashers ignore walkers, embedded objects do not tear (`MOVE_INTENT_WALK` uses).
- **Acceleration option exists but is unused**: `InputRamp` (`src/Core/Motion/MotionFacing.cs:60`) ramps the wish on the
  client before it is predicted and sent, so the server applies the ramped frame and prediction stays exact (scope 2b:
  "acceleration is a feel option, off by default"). It is tested (`MotionPredictorTests`) but not wired into the client.
  It clamps each world axis separately (a diagonal ramps faster than a cardinal, a turn bends the path).
- **Feel layer**: head bob (`CameraLayer`, scaled by speed), view-model sway (`HeldPose.Bob`), footsteps from the server.
- **No sprint, no jump** (tg has neither). tg's stamina exists and is ported: `staminaloss`, the 10 s reset after the last
  stamina damage (`stamina_regen_time`, `damage_procs.dm:488`), stamcrit, and the HUD's `/atom/movable/screen/stamina`.

## 2. Other games

| Game | Speeds | Acceleration | Backward | Sprint / stamina |
|---|---|---|---|---|
| Space Station 14 (the closest: SS13 remake) | walk 2.5, run 4.5 tiles/s (1 m tiles) | yes: `Acceleration 20`, `Friction 2.5` (Quake-style: fast start, soft exponential stop); weightless accel 1 | same | run is the default, Shift walks; **no run stamina** (stamina is a combat resource only) |
| Minecraft | walk 4.3, sprint 5.6 m/s (x1.3) | small momentum | same | sprint forward only, costs hunger, impossible on low hunger |
| Counter-Strike / Source | ~6.35 m/s with a knife | `sv_accelerate 5.5`, `sv_friction 4`: full speed in ~0.1-0.15 s, stop ~0.1 s | same | none (walk key is quiet) |
| PUBG, Tarkov, DayZ, Arma | sprint ~6-7 m/s | noticeable to heavy; Arma/DayZ are known for "sluggish" controls | slower | sprint forward only, stamina-limited |
| GTFO | sprint 6 -> 4 u/s as stamina drops | light | | sprint slows gradually instead of cutting off |
| SS13 downstreams (Citadel, Skyrat/Nova) | | none (tile movement) | | sprint with a buffer, then it costs `staminaloss` |

Lessons:
1. Every first-person game has *some* acceleration, but games built on precise positioning (CS, SS14) keep it short:
   reaching full speed in 0.1-0.25 s. Long inertia (Arma, DayZ) is the most common complaint. SS13 play is precise:
   1-tile corridors, airlock doorways, dodging, pulling, clicking adjacent tiles. So: short ramp, crisp stop.
2. Slower backward movement is a convention of tactical/survival games and makes running away from a pursuer while
   looking at them cost something. It is cheap to add and is the most "realistic" item here.
3. Sprint-with-stamina is common in survival games, but the closest analogue (SS14) chose not to put stamina on running,
   because chase balance (security vs antagonists) and stun combat both rest on equal, predictable speed. Where SS13
   servers added sprint (Citadel), they tied it to the combat stamina, which made it a balance subject of its own.

## 3. Assessment of the owner's ideas

| Idea | Verdict | Why |
|---|---|---|
| Speed build-up and decay | **Yes, do it** | The infrastructure is already there (`InputRamp`): client-only, no protocol change, prediction stays exact, the server and tg's logic are untouched. Keep it short (~0.2 s up, ~0.12 s down) so tile precision survives. |
| Slower backward | **Yes, cheap** | One scale in the client's `MoveWish` (e.g. back x0.7, strafe x0.9). Deviation from DV15 ("same in every direction"); not cheat-proof until enforced on the server. Read "back faster" in the request as "back slower" (the convention). |
| Sprint + stamina | **Maybe, after a playtest of the two above** | Our own mechanic (tg has none, D86 asks for few of those), touches balance. The current run is already 6.7 m/s, about Minecraft's sprint and above CS's top speed; a sprint on top would be ~8.5-9 m/s, which may feel too fast in corridors. Two variants are below. |

## 4. Proposed plan

### Stage 1: acceleration (client feel, no protocol change)

1. Rework `InputRamp` from a per-axis clamp to a vector ramp: move the sent wish toward the held wish by at most
   `accel` per tick while speeding up and `decel` per tick while slowing down or reversing (the length of the change
   clamped, so diagonals and turns behave like cardinals). Keep the turn responsive: a rotated wish of the same length
   is allowed to turn at once (only its length ramps), else mouse turning while running feels like ice.
2. Defaults: full speed in 4 ticks (0.2 s), stop in 2-3 ticks (0.1-0.15 s). An options slider (off / short / long)
   in `PlayOptions`, as the head bob option.
3. Wire it in the client where the wish is turned into the wire frame (`MoveWish.ToWire` caller), before
   `MotionPredictor.Push`. `Reset()` on a snap, a teleport, a respawn, a driver change (ZeroG keeps tg's own drift:
   the ramp is off there) and while `CanMove` is false.
4. Tests: the ramp's vector rules (diagonal = cardinal time, a turn keeps speed, a reversal passes zero), and the
   existing predictor test that walking gives zero corrections with the ramp on.
5. A gameplay test on the test server's own port: walk a corridor, stop at a door, check the capsule stops within the
   tile it was told to.

Risk: a modified client can skip the ramp (instant start is a small advantage). Acceptable now; the cheat-proof form is
stage 4.

### Stage 2: backward and strafe speed (client, deviation entry)

1. `MoveWish.FromKeys` scales the back component (x0.7) and the strafe component (x0.9) before normalizing; tg's
   movespeed modifiers still set the top speed on the server.
2. Amend DV15 (or a new DV row): "speed depends on the direction relative to the view".
3. Tests in `MoveWish` tests; playtest the numbers with the owner.

### Stage 3: sprint (owner decision first)

- **Variant A, the tg-native one (recommended if a sprint is wanted)**: a held key (Shift: `ViewPetCommands` holds it
  today, so a rebind is needed) adds a tg movespeed modifier with a negative `multiplicative_slowdown` (~x1.3 speed),
  forward-only (wish within ~45 degrees of the view). Each tile crossed while sprinting adds a few points of tg's
  `staminaloss` (e.g. 2 per tile); the sprint stops by itself when `staminaloss >= 40`, the point where tg's own damage
  slowdown starts. Recovery is tg's: 10 s after the last stamina damage it resets. The stamina HUD screen object shows
  it. Consequences: a baton or disabler hit also blocks sprinting; sprinting into a fight leaves you closer to stamcrit.
  Needs: a `MotionInputFlags` bit (protocol version bump), the predictor's sprint speed (as `IntentTilesPerTick` does
  for the walk/run toggle), a sprint sound (breathing, from tg's sounds) and a stronger head bob/FOV kick while sprinting.
- **Variant B, no stamina**: three intents (walk / run / sprint) where the new default run is slower (~5 m/s) and the
  sprint is today's 6.7 m/s. Simpler, no balance changes, but no resource to manage.
- **Variant C**: no sprint; tune `MOVE_SPEED_SCALE` and the ramp instead.

### Stage 4 (later, only if needed): server-side momentum

Velocity in `MotionState`, the ramp and backward scale applied in `MotionStep` (deterministic: a yaw lookup table
instead of `Math.Sin`), protocol bump, predictor replays velocity. Makes stages 1-2 cheat-proof and allows per-surface
traction (ice, lube: tg's slips stay as they are). Larger work; worth it only once the feel values are settled.

### Other small feel items (cheap, optional)

- Head bob and view-model sway already scale with speed; with the ramp they will ease in and out by themselves.
- A slight FOV widen at full run / sprint (+3-5 degrees), behind the head bob option.
- A landing/stop footstep scuff on a hard stop (tg has no such sound: skip unless a tg sound fits).

## Sources

- SS14 defaults: `Content.Shared/Movement/Components/MovementSpeedModifierComponent.cs` (space-wizards/space-station-14).
- Minecraft speeds and sprint hunger: minecraft.wiki (Sprinting, Hunger management).
- GTFO stamina: gtfo.wiki.gg/wiki/Stamina. Squad/Arma sprint discussion: feedback.bistudio.com/T143573.
- tg: `human.dm:962` (damage slowdown), `damage_procs.dm:488` and `living_defines.dm:254` (stamina reset),
  `_onclick/hud/human.dm:33` (stamina HUD), uses of `MOVE_INTENT_WALK`.
