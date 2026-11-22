# Wall entertainment monitor

Source: /obj/machinery/computer/security/telescreen/entertainment,
code/game/machinery/computer/telescreen.dm. Wall-mounted television with audio
from its internal radio. Distinct enclosure from standard status displays and
camera telescreens. No existing entertainment enclosure was found.

Four static presets: unpowered (no screen overlay), standby (entertainment_blank),
broadcast (entertainment frame 0), holiday (entertainment_xmas frame 0).
Broadcast notification selects on/off screen artwork; base computer code clears
the screen overlay on power loss. Christmas code changes icon_state_on to
entertainment_xmas. No separate mute artwork is defined. No broken frame exists
in this atlas; none invented. All 35 frames and delays [2,...] of each broadcast
are preserved in metadata, not animated. No collisions or gameplay integration.

Original status_display.png: 1088 x 1088, 32 x 32 tiles. Pixels match source DMI.
GLB embeds unchanged original PNG bytes; BLEND references the same source atlas.
Nearest filtering, flat shading. No derived textures. Front, roof, sides, rear
and underside use original casing pixels. Rear/depth are 3D interpretations.

Closed connected housing with lined screen recess and pixel-stepped lower
corners. Upper perspective band maps to roof depth. Separate closed tinted
glass slab: alpha 0.15, thickness 0.003375 m. Screen_Overlay stays separate,
behind glass, with original alpha and emission strength 1 to match emissive
source screens. It is an intentional open sheet. Housing and glass are shared
by all presets, screen mesh shared by the three powered presets. Slot 0:
Entertainment_Monitor_Housing_Original, Entertainment_Monitor_Glass_Original,
or Entertainment_Monitor_<state>_Overlay. Screen variants use atlas offsets
exported as KHR_texture_transform. No moving parts or direction duplicates.

Scale 0.90 / 32 m per pixel: housing width 0.84375 m, height 0.5625 m,
depth 0.1125 m. Rear-center origin, front Blender -Y / Godot +Z.
All four exports passed original-PNG, nearest, opaque-UV and degenerate-triangle
checks. Physical meshes are closed/manifold. Front, above, rear and side visually
reviewed. Godot import/runtime not tested. Unrelated legacy models still fail
the global validator; this family passes. See export_validation.json.

Built through Blender Lab MCP using build_entertainment_monitor.py and
export_entertainment_monitor.py in tools/art.
