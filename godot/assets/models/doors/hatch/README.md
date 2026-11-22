# Shared hatch: CentCom / maintenance

`hatch_airlock.glb` is one base model for two textures, 208 triangles. Four separate meshes (52, 60, 44 and 52 triangles): `Hatch_Upper_Left`, `Hatch_Upper_Right`, `Hatch_Lower_Left`, `Hatch_Lower_Right`. The closed hatch is 2×2 m, 0.16 m thick. The split into four sections matches the source opening frames. The outer corners are square, not rounded or chamfered. The inner seams run along the middle of the black pixel line of the source sprite and have interlocking teeth and slots; the four sections fill the 32×32 square completely without overlaps.

`hatch_airlock.blend` has two scenes: Hatch_Airlock with CentCom and Hatch_Maintenance. They share the same four mesh datablocks; only the material differs.

Use one GLB in the game. For maintenance set the material's albedo_texture to `res://assets/tg/icons/obj/doors/airlocks/hatch/maintenance.png`, for CentCom to `res://assets/tg/icons/obj/doors/airlocks/hatch/centcom.png`. Both atlases are 128×128 and the UVs use the first 32×32 tile, so no UV remapping is needed. Keep nearest filtering. When several instances have different looks, duplicate/cache the material and keep the shared mesh.

The sections open toward their corners. Demo offsets in Godot: upper left (−1.05,+1.05,0), upper right (+1.05,+1.05,0), lower left (−1.05,−1.05,0), lower right (+1.05,−1.05,0). These are offsets from the starting positions. No animation clips, collisions or game logic are added.

The original atlases are unchanged; CentCom is embedded in the GLB byte for byte, and both atlases are packed in Blender. NEAREST filters, closed components, flat normals. Checked: the source frames and the atlas references in tgstation/code/game/machinery/doors/airlock.dm. Generator: tools/art/build_vault_hatch.py.
