# M4v: the animations of the ported mechanics (D142, D143)

Status: written with M4v slice 4, 2026-10-14. This is the demo check of ROADMAP M4v: every mechanic ported in M2 to M4 with the
animations tg shows for it, whether the 3D client shows them now and since which step, or the gap and its reason. The wire and the
client's drawing are in `docs/ai/specs/protocol.md` sections 17 to 20; the deferred and not-translated sites are listed in
`docs/ai/specs/not-translated.md`.

## 1. The steps

| Step | What it made reach the client and play there |
|---|---|
| 164e3eb (protocol version 8) | World sprites animate by their icon states' frames (static props: billboards, items, wall and floor decals); BYOND's `flick` on an entity (section 17) |
| Slice 1, dda8647 (version 9) | `animate()` chains of entities and static billboards, items and resting machines (section 18) |
| Slice 2, 11a6408 | Icon state animations and flicks on moving entities, shaped pieces (doors, firelocks) and turfs (section 17.4) |
| Slice 3, b9174f4 (version 10) | `flick_overlay_view`'s short-lived visuals with their chains (section 19) |
| Slice 4 (version 11) | A screen object's flick and chains (the HUD, section 20); chains on wall and floor decals (section 18.3); the airlock's floor lights; the last deferred sites translated (below) |

## 2. The demo check

Each row: the mechanic (milestone), tg's animations for it (icon state frames, `flick`, `flick_overlay_view`, `animate()`, `do_animate`),
and what the client shows. "Shown" means sent and drawn; the step in brackets.

| Mechanic | tg's animations | The client now |
|---|---|---|
| Walking (M2) | The glide between tiles (`glide_size`); the dir turn | Shown: the client's own interpolation of the replicated position (D103), the dir picks the picture |
| Posture, lying and standing (M2) | `update_transform`'s turn of the transform with `dir`; the lying offset's `animate` of `pixel_w/x/y/z` (0.2 s) | Shown: the posture draws the lying turn (the chain's transform is not sent, section 18.1); the offset slides (slice 1) |
| Help-shaking a mob up (M2) | `shake_up_animation`: `pixel_w` +4, -8, +4 over 0.3 s (carbon_defense.dm:404) | Shown (slice 4: translated; it plays as any entity chain) |
| Stagger (M2, M4a) | `do_stagger_animation`: a sideways sway of `pixel_w` | Shown (slice 1) |
| Floating, zero gravity (M2) | `DO_FLOATING_ANIM` (the 2 px bob, for ever) and `STOP_FLOATING_ANIM` | Shown (slice 1; an interrupted bob may jump up to 2 px: every chain starts as with `ANIMATION_END_NOW`) |
| Melee and hits (M4a) | `do_attack_animation`'s lunge; `do_item_attack_animation`'s effect image and `animate_attack`'s parallel tracks; the hit | Shown: the lunge as `EntityFlags.Lunge` (M4a); an attack on the own tile as a chain (slice 1); the attack images as visuals (slice 3); the owner's particle burst (`HitEffectSeam`, 2026-10-09) beside them |
| Items and hands (M4a) | `do_pickup_animation` (an image flying to the picker); `do_drop_animation` (from the mob's tile, turned, small, settling over 0.3 s) | Shown: the pickup visual (slice 3), the drop glide (slice 1). An item in a hand is not a world entity: its own chains and visuals are not drawn |
| Throwing (M4a) | `SpinAnimation(5, 1)`: one turn in 0.5 s | Shown by the client: D127's tumble of every non-mob entity flagged Thrown (about two turns a second for the whole flight) covers it; the chain is never sent (atoms_movable.dm:1450, a note) |
| Storage (M4a) | `animate_parent`: the storage item squashes 1.07 x 0.9 and back | Shown on a storage item on the floor or a table (slice 1); not in a hand (not an entity) |
| Clothing and the worn look (M4a) | The worn icon states (still); the welding visor's flip changes states | Shown as pictures (D142); no animated state in the ported set |
| Progress bar of a timed action (M2, M4a) | The bar image's slide and fade (`progressbar.dm`) | Drawn another way: the client's own bar from the replicated progress (ChatLines); the slide and fade are not played |
| Busy cog over a mob (M2) | `/datum/cogbar`: the cog image and its fade on `remove()` | Gap (DV13): the cog is never created (`/datum/cogbar/New` deferred); the 3D client is to draw the busy state from replicated state, so there is nothing to fade |
| Doors and airlocks (M3, M4a) | `do_animate`: opening and closing frames, `deny` (`lights_denied`, 6 x 0.1 s); `lights_emergency` and `lights_reta` blink; `sparks_*`; bolt, panel, welded and sealed overlays; the `unres_*` floor lights | Shown: the panels slide (DoorAnimation, M3) with the closed position's picture, the deny lights blink, every overlay's frames by tg's delays (slice 2); the floor lights lie on the next tile, bright in the dark (slice 4). Reachable later: the emergency and RETA lights need AI control (M10+), the damage sparks atom integrity (M5). Gap: the emissive layers (DV12) |
| Pod doors and shutters (M3) | The slab's opening and closing frames | Shown: the slab rises into the ceiling (M3 R5) with the closed position's picture (slice 2) |
| Firelocks (M4b) | `door_opening`, `door_closing` (6 x 0.2 s), `door_deny` (3 x 0.1 s), the alarm lights (`firelock_alarm_type_*`, for ever) | Shown: a full firelock's slab plays all of them (slice 2). Gap: a border firelock's alarm overlay (its picture is its full counterpart's state, a library state with no overlays; MetaStation has none) |
| Windoors (M3) | Their own states (a top-down band); `leftdeny`; the `spark` flick on an emag | Shown: the deny flash (DoorAnimation). Gap, a 3D limitation: tg's windoor states, the spark too, are the door seen from above (a band along the tile edge), which the 3D glass panel cannot show; it draws its window counterpart (D121, M3 R6) |
| Windows, grilles, tables, railings, flaps (M3) | No animated states | Still, as in tg |
| Buttons (M4a) | `attempt_press`'s success and error flashes (`flick_overlay_view`) | Shown on the button's wall face (slice 3) |
| Lights and lamps (M3) | The on, off, empty and broken states (still); `flicker()`'s `_flickering` overlay | Shown: the states and the glow (D142). Gap: `flicker()` (light.dm:542) is not translated: its callers are events (the grey tide and others, M10+) |
| Closets and crates (M4a) | `animate_door`: the door object (`/obj/effect/overlay/closet_door` in `vis_contents`) swings by four transform steps of a tick with an icon state change at 90 degrees; the contents' masked clones while it closes | Partly: the door object is drawn as part of the closet (`vis_contents` flattened) at its end transform for the 0.2 s, then the open or closed picture. Gap: the swing itself changes `icon_state` mid-chain (the front picture to `_back`), which the wire's animate vars do not carry (section 18.1); the clones need an alpha mask filter (filters are not ported; the 3D client has none) |
| Solar panels and trackers (M7) | `visually_turn_part`: the glass and edge overlays (and the tracker's dish) turn to the sun by `animate(transform = ...)`, two cubic steps of 2.5 s, an `animate()` of a `vis_contents` child | Partly: the replicator flattens the overlays with their end transform (the animate chain of a part of a thing is not sent) and the 3D model's head turns to that azimuth in one step (`ModelRule.Sun`, DV74) |
| Atmos machines: vents and scrubbers (M4b) | The fan and pump icon states (animated); `vent_out-shutdown` flick when the pipe goes | Shown: the frames (164e3eb) and the flick (version 8) |
| Atmos machines: pumps, filters, mixers, connectors, air alarms (M4b) | Their on states' frames; the air alarm's screen states | Shown by the icon states' frames (164e3eb) |
| Pipes (M4b) | Still; a hot heat-exchanging pipe glows: `animate(color = heat2colour(...), time = 2 s, SINE_EASING)` (he_pipes.dm:62) | Shown (slice 4: translated; the floor decal's colour fades in over 2 s) |
| Canisters (M4b) | The pressure light and valve overlays (still); `canister_break`'s tip-over: `animate(transform = turn(transform, rand(-179, 180)), 0.5 s, BOUNCE_EASING)` | Shown: the overlays; the tip-over is translated (slice 4) and plays on the canister's billboard, reached when a canister breaks (atom integrity, M5) |
| Fire (M4b) | The hotspot's fire icon states and its colour from `heat2colour` | Drawn another way: the client's burning air from the replicated hotspot (D18, D334, FireVolume); the colour is tg's |
| Gas haze (M4b) | The gas overlays' icon states (plasma, water vapour) | Drawn another way: the client's gas haze from the replicated gas cells (D18) |
| Wet floors (M4b) | The wet overlay (still) | Shown (still) |
| Space wind (M4b) | None in tg (the pull is movement) | The client's airflow streams (D141) |
| Cameras (M3) | The camera's pan frames | Shown: the fin plays its side view's frames (17.2) |
| Animated turfs (M3) | `recharge_floor`, the river water, the glowing basalt | Shown (slice 2); space and the circuit floors are still, as in tg |
| The HUD: fullscreens (M2, M4a) | `clear_fullscreen`'s fade to alpha 0 over a second | Shown (slice 4) |
| The HUD: alerts (M2, M4b) | `throw_alert`'s slide in from 32 px with an elastic easing | Shown (slice 4; an alert's sprite at its `screen_loc`) |
| The HUD: buttons (M4a) | The resist button's `flick("act_resist_on")` | Shown (slice 4); clicking it reaches tg's `resist()`, which is M9 |
| Death (M2) | `gib_animation`, `dust_animation` | Gap: gibbing and dusting are not ported (M6 gib and spill, M5 dust) |
| Eyes (M2) | `animate_eyelids`: the blink | Gap: the eyelid overlays are the face's appearance work (M9) |
| Electrocution (M2) | `electrocution_animation` (the skeleton overlay, 4 s) | Gap: the human's `electrocute_act` (human_defense.dm:378) is not ported; electrified doors and grilles need power (M5, M8) |
| High gravity (M2) | `gravity_animate`: a motion blur filter's animate | Gap: filters are not ported (the 3D client has none) and no ported mechanic raises gravity above standard |
| Slapping (M4a) | `do_slap_animation` | Not reached: no tg code calls it at the snapshot (living_defense.dm:759) |
| Falls (M5) | `onZImpact`'s `SpinAnimation(5, 2)` | Translated with `SpinAnimation` (M7, D169): a chain of three turns, sent as any animation |

## 3. What the renderer still does not animate

- `animate()` on the shaped pieces (doors, windows, tables, lamps) and on the turfs: no ported code animates one; a piece's parts are boxes
  that a 2D transform does not fit (only a colour or alpha would carry), and the turfs' chains are not sent (section 18.1).
- A colour matrix animation (an appearance with a list `color` counts as white for the playback).
- A chain that changes `icon_state`, `dir` or `layer` over time (not on the wire: section 18.1); the closet door is the one ported user.
- The chains and visuals of a non-entity host: an item in a hand, an `image` in `client.images` (the progress bar, the cog).
- BYOND's interrupt semantics: every chain starts from the vars' values (as `ANIMATION_END_NOW`), not from where it is drawn.

## 4. Tests

The tests of each step are listed with it: protocol.md sections 17.3, 17.4, 18.4, 19.4 and 20.4, and
`tests/Anomaly.Core.Tests/Tg/M4v/AnimationSiteTests.cs` for slice 4's translated sites.
