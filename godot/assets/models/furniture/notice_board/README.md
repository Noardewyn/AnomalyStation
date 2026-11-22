# Wall notice board

Source: /obj/structure/noticeboard, code/game/objects/structures/noticeboard.dm.
Wall mounting is explicit in initialization and wallframe. Up to eight papers
or photos can be pinned. update_overlays selects notices_1 through notices_8
according to count. Department variants change access/name, not artwork.
No existing matching model was found.

Nine presets: empty and 1-8 notices. Base board and each connected paper shape
are separate closed meshes. Paper follows the original pixel silhouette,
including pin cutouts; its thickness is 0.000984375 m. Each paper origin is
at its top attachment center, removal local -Y. No animations or gameplay.
Source count overlays are preserved as separate physical paper meshes, not
painted into the board. All states use the exact source overlay artwork.

Original icons/obj/wallmounts.png, 288 x 288, 32 x 32 tiles, one direction/frame
per state. Pixels match original DMI; GLBs embed unchanged original PNG bytes.
BLEND references original atlas. Nearest filtering, flat shading. All surfaces,
including paper edges, rear, sides and roof have original atlas UVs.
Roof uses the top perspective rows. Rear uses original cork; unseen depth is
an interpretation. No new texture images, glass, collisions or directional
copies. Rotate the same asset for different walls.

Continuous board body is shared across all presets. Matching paper silhouettes
share meshes in the variants library. Slot 0: Notice_Board_Housing_Original
or Notice_Board_<count>_Notices_Original. Paper states use atlas offsets via
KHR_texture_transform. Wood/cork border detail stays in the original texture.

Scale 0.90 / 32 m per pixel. Board width 0.675 m, height 0.421875 m,
depth 0.05625 m. Rear-center origin, front Blender -Y / Godot +Z.
12-148 triangles per preset. All nine pass original PNG, nearest, opaque UV,
nondegenerate geometry and manifold checks. Front, above, rear and side
visually reviewed. Godot import/runtime not tested. Unrelated legacy assets
still fail the global validator; this family passes.

Built through Blender Lab MCP with tools/art/build_notice_board.py and
export_notice_board.py. Details in source_audit.json, model_info.json,
mesh_integrity.json and export_validation.json.
