# External airlock

Three meshes: the fixed `Frame`, the upper leaf `Door_Upper` with glass and the lower leaf `Door_Lower` with the AIRLOCK lettering. In the source `opening` frames the leaves part vertically; the model repeats this construction.

- `external_airlock.glb`: the base closed model, 3 meshes, 312 triangles (frame 112, upper leaf 156, lower 44). The frame is built only from the visible pixels of `open` (alpha ≥ 0.5): tg's rows 3 and 28 are nearly transparent and are covered by the leaves.
- `external_airlock.blend`: the editable model with the original atlas packed.
- `model_info.json`: dimensions, glass parameters, opening directions and the checksum of the source texture.

Size with the frame: 2×2 m, depth 0.28 m. The leaves are 0.16 m thick. The glass is a closed volume 0.0533 m thick in the middle of the upper leaf; the front and back recesses are as deep. The glass sits in an opening with opaque metal walls. Dark grey-green tint, alpha 0.35, a BLEND material with back faces culled. The glass is part of the upper leaf's mesh with its own material, not a fourth object.

The UVs use the unchanged `res://assets/tg/icons/obj/doors/airlocks/external/external.png`: the leaves use the first tile, `closed`, the frame the second, `open`. At the back the UVs are mirrored so that AIRLOCK reads correctly from both sides. Closest filtering in Blender and NEAREST in the GLB, without mipmap filtering. The source PNG is embedded in the GLB byte for byte, unchanged.

Opening: in Blender the upper leaf moves along Z by +0.875 m, the lower by −0.8125 m. In Godot these are Y offsets from the starting positions. The frame does not move. The leaves go into the ceiling/floor beyond the doorway; the demo scene shows the whole mechanism without surrounding walls. There are no keyframe animations, collisions or game logic.

Checked: the use of the external atlas in `tgstation/code/game/machinery/doors/airlock.dm` and the opening/closing frames from the metadata. Separate signal and service overlays are not baked into the model.

Generator: `tools/art/build_external_airlock.py`, run in Blender. Checked: all components closed, three exported meshes, 232 triangles, BLEND glass, nearest filters and the embedded PNG matching the source. Opening in game in Godot was not tested.
