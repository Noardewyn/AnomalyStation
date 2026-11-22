# Accumulated gameplay bugs, 2026-10-23

Reference: read-only `../tgstation`, snapshot `1d99bd7f`. Gameplay changes use the translated tg call paths. The owner approved temporary EVA parts in MOD storage and requested English conversation for this task.

| Report | Change and reference |
|---|---|
| Orbit camera jumps during noclip | Camera collision uses the replicated `MotionParams.NoClip` flag. |
| Missing suffocation audio | The existing `handle_suffocation` 25% gasp branch initially played the reference gasp sounds directly. After merging M9, it uses tg's full `emote("gasp")` path, including its sound and cooldown rules. |
| Platform-edge movement pauses | The attempted omission of `drift_handler/glide_to_halt`'s final input lock was reverted after the owner reported continued drift on solid surfaces and after landing. The original edge-stutter report remains open. |
| Red EVA slots in engineering | tg's `suit_storage_unit/engine` contains a MOD controller, not separate suit/helmet objects. Add EVA parts while MOD deployment is inert; owner approved. |
| Multitool does nothing | Connect `attempt_wire_interaction`, `_wires.dm`'s UI, and airlock/APC/airalarm/firealarm wire handlers. Cut and pulse still require held tool qualities; the server validates UI status. Assemblies and photo-based blueprint study remain deferred. |
| Toolbelt equip | New regression covers a full engineering belt through the actual E keybinding and belt HUD slot, over a uniform with an empty belt slot, including HUD contents after equipping. These pass; the owner's silent failure remains unreproduced. Dragging storage onto oneself opens it in tg; this was not changed into an equip action. |
| FOV obscures sky | Empty depth does not receive the geometry visibility mask. |
| Unsupported wall fixtures | Run the mount-only part of the corresponding tg initializers for inert/skeleton fixtures, including camera's directional mount rule. Unported deconstruction removes the unsupported object rather than leaving it hanging. |
| Gas sensors stand up | `/obj/machinery/air_sensor` and all subtypes use the floor equipment rule. |
| Attack effect drawn on wall/at feet | Attack flick visuals carry a 3D line through the existing effect geometry field; the client draws the swing from attacker to impact, preserving tg's attack decisions. Local aimed impact points refine the endpoint. |
| No victim impact | A short visual shake accompanies the existing server hit flag. |
| Dev flight climbs furniture | Dev flight clears/avoids the climb-walkable trait; the own rendered elevation requires the replicated climbing state. |
| Third-person self interaction | Pointer picking includes the visible own character. Crosshair picking still excludes it. |
| Tab prevents world actions | Pointer aiming, clicks and held mouse events work in both play views, with HUD precedence and existing drag gestures. |
| Atmos cleanup NullReferenceException | Both base nodes and component parents/airs tolerate absent lists during cleanup. |

## Verification

Focused gameplay regressions are in `tests/Anomaly.Core.Tests/M7/BugBatchTests.cs` and `ToolKitTests.cs`. Client wire controls and sensor classification have engine-free tests.

- Final normal solution build, isolated solution build and Godot C# build: 0 warnings, 0 errors.
- Core gameplay and generated-metadata selection: 53 passed, 0 failed, 0 skipped. The final belt HUD-content assertion also passed in a separate single-test run.
- Client Tgui, World, Interaction and ShotGeometryMirror selection: 533 passed, 0 failed, 0 skipped.
- Headless Godot smoke: 3,373 playing frames, 526 deltas, first-to-third-person switch, 13 audio events with no missing files, exit 0. The dummy renderer/audio driver cannot verify appearance or audibility.
- Full Server suite: 2,858 passed, 4 skipped, 2 failed in 25 minutes 33 seconds. Failures were `LiteNetSocketTests.TwoManualClientsConnectExchangeMessagesInBothDirectionsAndDisconnect` (timeout under concurrent load) and `FakeMapTests.MetaStationReadsFast` (31.7 seconds against its 15-second limit). Both passed together on an isolated rerun; the map test also passed separately in about four seconds. The original full run remains non-green.
- Full Core suite: 6,495 passed, 5 skipped, 4 failed in 31 minutes 16 seconds. All failures concern generated metadata (`GeneratedOnlyMarksExactlyTheClassesWithoutAHandWrittenPart`, `DmInertIsExactlyOnTheTypesTheRuleMakesInert`, `TheIndexTranslatedColumnIsWhatTheTgAssemblyDeclares`, `ARunOfTheToolIsByteEqualToTheCheckedInOutputs`). This run started before final regeneration. The final metadata-only rerun passed all 18 tests, including the converter's byte-equality check. No gameplay failures were reported in the full run.

All six failures observed in the original full runs passed subsequent focused reruns. The complete suites have not been repeated together against the final binaries. The converter test's redirected output remained open in orphaned MSBuild workers after the converter exited; stopping only those workers let the final passing result return.

Visual playtest remains necessary: cross walls in third-person noclip; walk along and across a platform edge without boots/jetpack; check the shadow boundary against the sky; attack a wall fixture and a mob at different aimed heights; click one's third-person body and use world controls with Tab enabled. Also check engineering EVA dispensing and cut/mend/pulse after opening a door or alarm panel. The toolbelt report remains open pending a reproducible in-game case.

## Follow-up: private-image disconnect

The owner reported `private image 20 names an appearance the table does not have`. An empty override image reproduces this exact exception in both snapshot and delta application. Protocol 35's codec and server already permit such images: they hide their host rather than draw a picture. The replica's two validation paths still rejected index 0 unconditionally. Both now accept it only with `Override`; positive appearance indices still require a table entry, and ordinary images still require a nonempty appearance. The original log does not identify which gameplay effect created image 20.

Reference: `tgstation/code/modules/hallucination/eyes_in_dark.dm:60` creates an image with no icon and `override = TRUE`; the existing protocol contract is in section 44 of `docs/ai/specs/protocol.md`. No gameplay logic or wire layout changed.

Verification: the two new snapshot/live-update tests failed with the exact reported message before the fix. After the fix, all 33 selected replica, replicator, protocol-35 and hallucination gameplay tests pass. This includes an actual replicator/codec/client-replica check that an empty override reaches only its owner. Godot C# build: 0 warnings, 0 errors. The owner subsequently reported that the original gameplay fixes appeared to work; the toolbelt-specific failure was not separately reproduced here.

## Follow-up: ghost vision, pointing and APC bearing

- Corpse and ghost sight flags no longer activate meson outlines. The corpse keeps the existing fullscreen death shade without added tint/blur/lighting lift. Ghosts receive virtual night-vision goggles with tg's normal `toggle_nv` item-action icon, initially off, and the real goggles' color cutoffs `(10,25,10)`, light-green client colour, on/off sprites and activation sounds. Living meson users keep their original behavior. The earlier local fullbright text button was removed following the owner's clarification; DV174 records the observer adaptation.
- Shift-middle pointing uses a full-view ray independently of interaction/throw reach, in first person, third person and cursor mode. Walls still stop the ray, and the server retains tg's view checks. Point arrows are spawned on the pointer's turf in tg and displaced using `pixel_x`/`pixel_y`; the renderer now applies that displacement to the world node before visibility checks, without applying item scale or replaying the displacement a second time. Reference: `code/modules/point/point.dm:10`.
- The multitool scan retains tg's area APC lookup, distance bands, 2-second cooldown and 1.5-second lifetime. Its 96x96 sprite is presented as an opaque, outlined geometric arrow on a dark backing; the bearing is computed directly from the server transform relative to the camera, around the crosshair or free cursor. Reference: `code/game/objects/items/devices/multitool.dm:67`; DV175.

Validation: 38 selected server/client tests passed (vision policy, camera-relative bearings, distant ray selection and wall occlusion, replicated distant-arrow position, meson gameplay and private HUD replication), plus 10 existing core tool tests including APC scan colors/cooldown/lifetime. Godot C# build: 0 warnings, 0 errors. Headless smoke: 1,862 playing frames, first/third-person and Tab switches, exit 0; shutdown reported four ObjectDB instances remaining. The dummy renderer cannot verify contrast or placement visually, so an in-game visual check remains necessary.

Third-person follow-up: extracted the existing camera-to-character projection into `WorldPicker.CameraLead` so the actual orbit path and regression tests share the calculation. Four additional cases cover level and elevated cameras, an off-center cursor, own-body exclusion, wall occlusion, and the view-range limit measured from the character. All 34 selected picking/indicator/arrow-replication tests pass; Godot C# build has 0 warnings and errors. An 18-second headless orbit run exercised real Shift-middle input with Tab off and on: the client sent the crosshair target `(16,5,1)` and the off-center cursor target `(20,2,1)`, respectively; 2,510 playing frames, exit 0. The fake server validates the client input path here, not the full server pointing mechanic or rendered appearance.

Ghost clarification and name placement: removed the local fullbright text button. Observer Login now grants a real `datum_action_item_action_toggle_nv` targeting virtual `/obj/item/clothing/glasses/night` goggles, initially off. Its ordinary HUD action button, sprites, activation sounds, green channel cutoffs and client colour are reused; observers have no eye slot, so the seam applies those values to the observer after the existing action toggles. The client's corpse policy recognizes limp body sight with all three corpse sight flags and suppresses mesons, added color/blur filters and lighting lift while retaining existing death fullscreens. No night-vision action is granted to the corpse.

Name tags are bottom-aligned over the current composed sprite's opaque top, with a head gap and a ceiling allowance; the raster's transparent margin is cached, and lying or elevated sprite positions update the label each frame. Tests cover standing, lying and raised sprite heights. Validation: 18 core ghost/action-button tests and 11 selected client/real-server ghost/meson tests pass; the enhanced ghost HUD test also passed after adding icon visibility and return-to-corpse assertions. Godot C# build: 0 warnings, 0 errors. Both server and client must restart for the observer action change.

## Follow-up: space movement rollback

Removed the continuous-capsule early return added to `drift_handler.glide_to_halt` in `14474dad`. Compared against `../tgstation/code/datums/drift_handler.dm:200`: capsules again use the original loop shutdown, `block_inputs_until`, delayed handler deletion and PRE_MOVE input blocker. DV168 is withdrawn. The previous test asserted immediate deletion, which did not validate landing behavior; it now checks the original input blocker and eventual cleanup.

Added repeated landing tests with a connected human, no boots or jetpack, crossing open space toward either a floor with gravity or a lattice handhold. Each case repeats three times and verifies stopping position, handler cleanup, zero replicated drift velocity and no movement during a further idle second. These controlled cases pass; they do not establish the cause of the owner's intermittent in-game continued flight.

The original platform-edge stutter remains open. Investigate the bridge/prediction boundary before changing tg's drift mechanics: `StepCapsule` caches the zero-gravity gate result until the next move delay, while `PreviewCanMove` does not represent `allow_final_movement`'s input lock. These are code-level candidates, not proven causes. A replacement should measure server/client positions and gate state while repeatedly crossing and walking along an edge, preserve `Process_Spacemove` and the original stopping lifecycle, and retain landing coverage.

Validation: 18 selected core drift, motion and bug-batch tests pass; four real-server free-flight and zero-gravity prediction tests pass, including 0 and 100 ms latency. Godot client build: 0 warnings, 0 errors. Restart both server and client to load the rollback.

## Follow-up: continuous walking-to-space direction (2026-10-24)

The owner confirmed the continued-flight regression was gone, then requested that leaving a platform preserve the freely chosen walking direction. The drift was already applied continuously by DV69, but its initial angle still came from tg's tile crossing: `atoms_movable.dm:900` calls `newtonian_move(dir2angle(movement_dir))`. The last north/south/east/west boundary therefore replaced the walking vector, making flight follow the grid even though the drift loop accepts arbitrary angles.

The locomotion bridge now scopes the actual capsule displacement angle to its `Move` call. The two tg movement-originated inertia calls use this angle during that crossing, falling back to the original tile direction for ordinary tg moves. Existing drift crossings and direct external impulses keep their own angles. This also covers an unstabilized jetpack's movement-to-drift handoff. Force, speed, gravity/handhold/jetpack checks and the restored final input lock remain tg's. No protocol change is needed: the existing drift vector already reaches prediction. DV177 records the adaptation.

Eight walking-angle cases cover both signs, shallow and steep trajectories, releasing keys and turning the view without steering inertia. The first three cases failed before the fix. The added real-server sandbox scenario tests angled flight, per-tick continuous movement, landing and settled client/server agreement at 0 and 100 ms latency. Its platform and space are both airless to isolate movement from decompression's separate cardinal impulse. The original repeated gravity/lattice landing tests are retained.

Validation: 42 selected core drift, motion, crossing, determinism and bug-batch tests pass. Seven selected real-server free-flight, zero-gravity prediction and jetpack tests pass. Godot client build: 0 warnings, 0 errors. Restart both server and client before playtesting.

## Follow-up: jetpack landing, short taps and refused steering

The owner clarified that the missed landing was on a platform without gravity, with the movement keys released. The previous landing checks covered floors with gravity and bare-hand lattice stops. An active unstabilized jetpack adds tg's `TRAIT_NOGRAV_ALWAYS_DRIFT` (`tanks/jetpack.dm:118`); `mob_movement.dm:277` then bypasses the backup checks. The capsule now recognizes a floor or catwalk directly under its feet before that branch, for both voluntary walking and stopping drift. This is explicit 3D contact behavior (DV178), not added gravity or an attraction to nearby platforms. The existing final-glide input blocker and delayed handler cleanup are preserved.

Two additional causes were reproduced:

- A short thrust that stayed within a cell never reached `Moved`'s inertia update. Accepted sub-tile movement now applies tg's `newtonian_move` with the actual angle without forcing the loop timer. A post-move preview guards against fuel depletion during the move; cached permission cannot outlive available thrust or a handhold. Existing fuel-depletion tests pass.
- `OwnPrediction.RenderPosition` took a full drift step when keys were held but `CanMove` was false, whereas idle drift used only the rendered fraction of the tick. Alternating refused input therefore made the camera jump. Refused input now uses that same fractional drift path. The render regression failed with a 0.1015625-tile jump before the fix.

Prediction keeps the new thrust direction across idle inputs and replays it in sequence. It clears drift on contact with floor paths or catwalks already present in map/entity replication. The server remains authoritative for drift speed, initial impulses, fuel and environmental changes. There is no wire-format change.

Network validation uses the actual client, codec, replica, prediction and server: 0, 100 and 200 ms one-way delay; additionally 100–200 ms jitter with 5% packet loss. Refused steering has zero corrections, zero mismatches and zero snaps throughout all measured cases. Short taps and weightless landings have zero snaps; with latency they produce 1–2 transition corrections, then no further mismatches during settled drift or rest. These counters do not justify a claim of universally zero prediction corrections. Two-client scenarios confirm that the remote replica and own prediction agree exactly with the server's stopped position on both weightless floors and catwalks, including jitter/loss.

Checks passed: 44 selected server/client tests (new control scenarios plus existing own-prediction, free-flight, jetpack and zero-gravity tests), then both new two-client cases; 54 selected core drift/jetpack/prediction tests, then 37 prediction/crossing/determinism cases including the new sub-tile replay test (overlapping selections). Godot client build: 0 warnings, 0 errors. Restart server and client to load the changes.
