# Wall access console

Four static presets: airlock_controller_standby, off, process_0 and process_1.
No existing matching asset was found.
This is /obj/machinery/door_buttons/airlock_controller, not access_button or
the other /obj/machinery/airlock_controller types.

Source: code/game/machinery/embedded_controller/access_controller.dm.
The controller operates paired interior/exterior doors. NOPOWER chooses off,
busy chooses process, otherwise standby. Process has two frames at [3,3].
These are static presets only. Source provides no separate overlays or
open-panel sprite; no service animation or internals are invented.

Original wallmounts.png: 640 x 608, tiles 32 x 32. State rectangles and frame
metadata are in source_audit.json and source_states.json. DMI pixels match PNG;
GLBs embed unchanged PNG bytes. No derived textures. Nearest filtering and
flat shading. All physical parts are opaque and closed/manifold.

The upper perspective rows map onto the roof. Front, sides, rear, top and bottom
use original atlas pixels. Four push buttons, screen and indicator are separate
closed meshes. Each push button has its pivot at its rear attachment center;
local +Y points into the housing. No press animation or specified travel.
There is no added glass or emission. Depth and rear surfaces are 3D interpretations.

All seven meshes are shared across the library's four scenes. Each part uses
material slot 0: Airlock_Controller_<state>_Original. State variation uses an
atlas offset, exported as KHR_texture_transform. No additional atlas copies.

Scale 0.90 / 32 m per pixel. Housing width 0.309375 m, height 0.253125 m,
depth 0.084375 m; keys protrude another 0.00421875 m. Rear-center origin;
front Blender -Y / Godot +Z. Rotate one model for different walls.

84 triangles per preset. All four exports passed opaque UV, nearest sampler,
unchanged original PNG and degenerate triangle checks. Front, above, rear and
side visually reviewed. Godot runtime/import not tested. No collisions,
animations or gameplay integration. Other legacy assets still fail the global
validator; this family passes. See mesh_integrity.json and export_validation.json.

Built through Blender Lab MCP with tools/art/build_airlock_controller.py and
export_airlock_controller.py. PNG: docs/ai/art/airlock_controller_preview.png.
