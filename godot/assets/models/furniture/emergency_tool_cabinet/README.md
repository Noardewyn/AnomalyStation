# Wall emergency tool cabinets

Three source types: /obj/structure/fireaxecabinet, /mechremoval and
/jawsofrecovery in code/game/objects/structures/fireaxe.dm. Explicit wall mount
and wallframes. Mech-removal and rescue-jaws cabinets occur on MetaStation.
No existing matching models were found.

30 presets: ten configurations per type: empty locked, loaded locked, loaded
unlocked, open loaded, open empty, three closed crack levels, broken loaded,
and broken open loaded. Source damage thresholds are 80%, 60%, 40%, then broken.
Not every empty/damage/lock permutation has a separate file: editable parts can
be reused. No gameplay, collisions or animations. Glass_raised artwork records
source open perspective; 3D opening uses the actual glass mesh rotated -105 deg
around its upper local-X hinge. The original raised state metadata is retained.

Source wallmounts.png: 288 x 288, tiles 32 x 32. Pixel-identical to DMI; GLBs
embed unchanged original PNG bytes. BLEND references the original atlas.
Nearest filtering, flat shading, no derived textures. Original atlas UVs cover
front, roof, rear, exterior sides and lined interior. Hidden depth/rear views
are interpretations of original casing strips.

Continuous closed shell with back and all four recess walls. Housing geometry
shared across colors via material atlas offsets (KHR_texture_transform).
Tools are separate closed meshes with original pixel silhouettes, 0.0225 m
thickness, removable along local -Y. Origins are at the rear center of each tool.
Upper_Door_Hinge is a separate empty parent for glass, handle and lock light.
Glass uses original tint/alpha, closed thickness 0.003375 m. Broken holes and
crack artwork follow source states, with closed thickness around each surviving
piece. Corner-only contacts keep separate vertex sectors for manifold surfaces.
Opaque Door_Handle fills the source handle cutout. Lock_Overlay stays a separate
alpha/emissive sheet; open pose carries the green unlocked light, matching the
raised artwork. Source update_overlays emits no extra lock overlay when open.

Slot 0: Cabinet_<family>_Original for shell; Cabinet_<axe|crowbar|jaws>_Original
for tool; Cabinet_<glass state>_Glass_Original for glass; Cabinet_Handle_Original
for handle; Cabinet_<locked|unlocked>_Overlay for lock. Matching geometry/UVs
share mesh data with per-object material overrides in the variants library.
No directional copies. Front Blender -Y / Godot +Z, wall at Y=0.
Scale 0.90/32 m per pixel: shell width 0.7875 m, height 0.421875 m,
depth 0.1125 m. Door/handle project slightly forward of the shell.

All 30 pass opaque UV, original PNG bytes, nearest sampler and nondegenerate
triangle checks. Physical pieces are manifold, lock sheets intentionally open.
Front, above, open interior, glass damage, rear and side visually reviewed.
Godot import/runtime not tested. Global validation still has unrelated legacy
failures; this family passes. See export_validation.json and mesh_integrity.json.

Built through Blender Lab MCP using tools/art/build_emergency_tool_cabinet.py
and export_emergency_tool_cabinet.py.
