# Shuttle equipment and modular seating

19 GLB models with embedded original atlas textures, plus matching Blender sources.
Filtering is nearest, shading is flat. Original game source images are unchanged.
shows assembled L-shaped sofas, a partly opened door and raised seat restraint.

## Files

| Files | Contents |
|---|---|
| `shuttle_seat` | Seat and separate hinged `Safety_Restraint` |
| `escape_pod_airlock`, `escape_shuttle_airlock` | Identical titanium airlock: frame and two sliding leaves |
| `engine_heater` | Heater housing |
| `propulsion_engine`, `_left`, `_right` | Three source appearances with matching geometry |
| `sofa_middle`, `_left`, `_right`, `_corner` | Regular sofa kit |
| `corporate_sofa_middle`, `_left`, `_right`, `_corner` | Corporate sofa kit |
| `wooden_pew_middle`, `_left`, `_right` | Wooden pew kit |
| `bookcase` | Cabinet with real shelves and three removable book-row meshes |

Every listed model has `.glb` and `.blend` files. Triangle counts are in `models.json`:
72–196 for seating and machinery, 384 for the filled bookcase, 684 for each airlock.

## Assembly

Sofa and pew modules use a 0.90 m pitch. Place left/middle/right along X with
0.90 m centre spacing. Repeat middle pieces to extend the seat.
The corner has backrests along local +Y and +X in Blender, with open ends at -X and -Y.
Example L in Blender: left at (-0.9,0), corner at (0,0), middle at (0,-0.9)
rotated -90 degrees around Z, right at (0,-1.8) with the same rotation.
The check scene contains both assembled sofa variants. Pew has no corner in the source.
Use the GLB coordinate conversion when translating these Blender layout coordinates
to Godot: (x,y,z) becomes (x,z,-y).

Seat height is 0.48 m; sofa/pew seat height is 0.46 m. Bookcase height is 1.80 m.
Origins are at floor level. The restraint has a hinge origin; rotate local X by
-110 degrees to raise it. Airlock leaves slide along X (approximately +/-1.3 m
for a full opening). These are movable parts, not authored animation clips.
No gameplay logic or collision shapes are included. Godot runtime import was not tested.

Hide `Books_Row_1` through `Books_Row_3` for an empty bookcase or partial contents.
These are reconstructed book groups, not a one-to-one implementation of all six
2D content-count states.

## Source audit

Sources inspected under `../tgstation`:

- `code/game/objects/structures/beds_chairs/chair.dm`: shuttle chair and up/down restraint overlays.
- `code/game/objects/structures/beds_chairs/sofa.dm`: middle, ends, corner for both sofa families.
- `code/game/objects/structures/beds_chairs/pew.dm`: middle and two ends.
- `code/game/shuttle_engines.dm`: heater and explicit `propulsion`, `propulsion_l`, `propulsion_r` states.
  Heater has engine power zero; these variants are not automatically joined by smoothing code.
- `code/modules/library/bookcase.dm`: finished `book-0` through `book-5`; `bookempty` is unfinished.
  The atlas contains `book_connect`, but current bookcase code does not use it.
- Escape-pod default and emergency Tranquility shuttle maps use `/airlock/titanium` for
  the named escape doors. Airlock code combines `closed` and `fill_closed` for opaque leaves.

`source_states.json` records the original DMI paths, state names, directions, frames
and atlas rectangles. `textures/` contains PNG copies of those original atlas pixels.
Geometry uses UV regions from the original atlases; no replacement texture artwork was generated.

Generators: `tools/art/prepare_shuttle_furniture.py` and `tools/art/build_shuttle_furniture.py`.
Assembly verification: `tools/art/check_shuttle_furniture.py`.

### Propulsion engine wall-mount revision

`tools/art/rebuild_propulsion_engine.py` overrides the three propulsion engine
assets. Execute it through interactive Blender MCP after an older batch rebuild.
Each engine has a 2 x 2 m mounting plate, 0.20 m plate thickness, a centered axis
at 1 m height, no floor legs, and a closed recessed dark nozzle. Each mesh has
204 triangles, plus a separate 28-triangle `Energy_Core` (232 total).
The casing uses the same UV bands around all eight sectors. The mounting plate
uses the original metal panel region of `shuttle.png`.
`Energy_Core` is a recessed closed insert with a blue emissive preview material.
Hide this mesh for the inactive state. Its `active_only` custom property is
descriptive metadata, not an implemented game binding. `Original_Atlas_UV` samples
the source exhaust color; `Effect_UV` is a normalized second UV set for a future
animated energy shader. No runtime animation or integration is added here.
Original `shuttle.png` atlas bytes and nearest sampling are retained.
The revised models were checked in Blender; this revision does not claim in-game
validation. Diagnostic renders belong in the system temporary directory.
