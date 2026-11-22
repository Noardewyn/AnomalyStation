# Public glass airlock

Two independent meshes: `Door_Left` and `Door_Right`. The glass inserts are part of their leaves with a separate material. This model has no fixed frame.

- `public_glass_airlock.glb`: the base closed door for import.
- `public_glass_airlock.blend`: the editable model with the texture packed.
- `model_info.json`: model parameters and the checksum of the source atlas.

Closed door size: 2×2 m, leaf thickness 0.16 m. 854 triangles in all: left leaf 438, right 416. The stepped edges of the diagonal bars keep the pixel silhouette.

The first 32×32 tile, `closed`, of the original `res://assets/tg/icons/obj/doors/airlocks/public/glass.png` is used. The texture is unchanged and embedded in the GLB byte for byte. Closest/NEAREST filtering without mipmap filtering. The back side uses the same physical pixels, so the asymmetric windows and indicators match the geometry.

The glass is closed volumes 0.0533 m thick in the middle of the leaf; the recesses on each side are also 0.0533 m deep. The openings have opaque metal walls. Grey-green glass: alpha 0.35, roughness 0.22, BLEND, back faces culled. These are neither empty through-holes nor single planes.

Opening along X: move the left leaf by −1.125 m and the right by +1.125 m from the closed position. The X axis is the same in Blender and Godot. The leaf roots start at −0.5 and +0.5 m. No animation clips, collisions or game logic are added.

Checked: closed components, two exported meshes, the original PNG preserved and nearest filtering. The model was viewed in Blender. Opening in game in Godot was not tested.

Generator: `tools/art/build_public_glass_airlock.py`. The use of the source atlas is in `tgstation/code/game/machinery/doors/airlock.dm`; the states and frames come from the neighbouring `glass.json`.
