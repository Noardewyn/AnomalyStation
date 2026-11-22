# Additional doors

Ten models are made from scratch; the CentCom hatch uses the shared hatch geometry fixed earlier. The smooth_structures folder is left out at the user's request.

| File | Construction | Triangles |
|---|---|---:|
| centcom/centcom.glb | Two side leaves | 152 |
| highsec/highsec.glb | Two side leaves with a shaped seam | 120 |
| firelock_heavy/firelock_heavy.glb | Frame, opaque upper and lower leaves | 302 |
| firelock_glass/firelock_glass.glb | Frame, upper leaf with three windows, lower leaf | 640 |
| shutters/shutters.glb | One panel of a closed shutter | 12 |
| shutters_window/shutters_window.glb | One panel with a polycarbonate window | 188 |
| windoor/windoor.glb | Glass sliding door on a cell border | 60 |
| morgue/morgue.glb | One panel of the morgue door | 12 |
| shutters_radiation/shutters_radiation.glb | One panel of a closed radiation shutter | 12 |
| blastdoor/blastdoor.glb | Two separate side leaves | 24 |
| hatch/hatch_airlock.glb | Four sections with teeth along the black line, existing model | 208 |

Next to each new GLB are an editable `.blend` and `model_info.json`: the source file, the state, the UV rectangle, the triangle count and the opening offsets in Godot. The new windoor's height is restored from what the object is: the source sprite is a narrow symbol of a door on a cell border, not a front view.

## How the game uses them

- `tgstation/code/game/machinery/doors/airlock.dm`: CentCom/grunge use centcom/centcom.dmi, highsecurity uses highsec/highsec.dmi; both types forbid glass variants. The moving parts are split as in the opening frames.
- `firedoor.dm`: the plain firelock uses doorfireglass.dmi and `glass=TRUE`; heavy uses doorfire.dmi and `glass=FALSE`. The geometry comes from door_closed; the opening direction was checked against door_opening.
- `shutters.dm`: shutters and windowed shutters; the windowed one has `glass=TRUE` and `opacity=FALSE`. At the user's request only the closed tile is modelled, without the slat roll-up mechanism.
- `windowdoor.dm`: the windoor is ON_BORDER_1 with the base state left; right and secure are separate states of the same atlas. Only the base left is made here; the four directions are not multiplied into separate models.
- `door.dm`: the morgue uses doormorgue.dmi. door_closed is used; in the opening frames the panel moves up.

The paths `icons/obj/doors/...` and `hatch/centcom.png` given from the project root do not exist. The existing originals in `godot/assets/tg/icons/obj/doors/` and `godot/assets/tg/icons/obj/doors/airlocks/hatch/centcom.png` are used.

## Materials and movement

The original PNGs are unchanged and embedded in the GLB byte for byte. All textures use NEAREST without mipmap filtering; normals are flat. The original state rectangles are used; for directional sprites the first direction is taken, and the game gets the others by rotating the model.

The firelock frame is built only from the visible pixels of `door_open` (alpha ≥ 0.5): tg's nearly transparent pixels (alpha 2, for clicking only) are discarded by the shader, and the face above them was a hole. Frame pixels over the leaves become a thin step behind the leaves; the pale ring pixels fill the leaves. Building one door: `DOORS=firelock_glass,firelock_heavy blender -b -P tools/art/build_additional_doors.py`.

The closed doorway is 2×2 m. The base root is at floor level. CentCom and highsec part along X, the firelock leaves move up/down along Y in Godot; the firelock frame does not move. model_info.json gives the offsets from the closed position. Shutters/morgue have an optional upward shift of the whole panel; folding or shape change is not simulated. The windoor slides left along X; the game places it on the right cell border.

The glass has alpha 0.35 and a closed thickness, and is part of the leaf mesh as a separate surface. The metal window walls are closed. Firelock/shutters keep the source glass pattern; the windoor uses a flat grey-blue tint, since the source has no front view of a large window.

There are no animation clips, collisions, game factories or control code. The geometry and placement of hidden faces are reconstructed. Checked: closed components, the number of exported meshes/triangles, matching embedded PNGs and nearest filters; the models were viewed in Blender. No game scenario was run in Godot.

Generator: `tools/art/build_additional_doors.py`. When building the shared scene it reuses `hatch/hatch_airlock.blend` and does not change the shared hatch geometry.

## Seams on the pixel grid

CentCom, Highsec and both firelocks were fixed from the user's diagrams: split strictly along whole-pixel borders, with rectangular steps. On CentCom the outline stays on the left leaf; on Highsec the central lock with its outline is entirely on the right; on the firelock the dark line stays on the upper leaf. Checked: integer coordinates of all vertices on the 32×32 grid and closed meshes.

Radiation shutters: closed [0,0,32,32], one panel by the same rule as shutters. In shutters.dm radiation inherits poddoor/shutters. Blastdoor: poddoor.dm, closed [64,96,32,32]; opening shows two leaves parting horizontally. Blastdoor thickness 0.28 m, radiation shutters 0.12 m; size 2×2 m. The original PNGs are embedded unchanged, nearest filtering. No new animation clips.
