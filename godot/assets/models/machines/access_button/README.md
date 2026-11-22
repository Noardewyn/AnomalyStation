# Airlock access button

Four static presets with matching GLB and editable BLEND:
access_button_standby, access_button_off, access_button_cycle_green,
access_button_cycle_red.

Source: tgstation code/game/machinery/embedded_controller/access_controller.dm,
/obj/machinery/door_buttons/access_button. Four MetaStation map records.
This is distinct from the existing /obj/machinery/button family.
NOPOWER selects off; busy selects cycle; otherwise standby.
No dedicated overlays, service panel or moving cover exist in this source.
Cycle has two frames, delays [5,5], rewind true. Both are static presets;
no animations, collisions or gameplay integration were added.

Original wallmounts.png, 640 x 608, tile 32 x 32. States:
standby (352,0), off (384,0), cycle (416,0) and (448,0).
PNG pixels match the original DMI; GLBs embed unchanged PNG bytes.
Nearest filtering and flat shading. No derived textures.

The original top three silhouette rows map onto the physical roof.
Front artwork preserves the original button and 2 x 2 status pixels.
All sides, roof, underside and rear use opaque regions of the same atlas.
The housing is continuous and closed. Press_Button and Status_Indicator
are separate closed meshes. Button pivot is centered at its mounting plane;
local +Y points into the wall. No specific press travel is asserted.
Depth and hidden surfaces are 3D interpretations.

Scale: 0.90 m / 32 pixels. Housing width 0.225 m, height 0.140625 m,
depth 0.05625 m. Button projects a further 0.0050625 m.
Rear-center origin; front Blender -Y / Godot +Z.
Rotate one reusable asset for wall directions.

Three meshes are shared between all four library scenes.
Housing material slot 0: Access_Button_Housing_Original.
Button and indicator slot 0: Access_Button_<state>_Original.
State materials use UV offsets into the original atlas, exported using
KHR_texture_transform. All materials are opaque; no artificial glow is added.

36 triangles per preset. All four exports passed original-PNG identity,
nearest-sampler, opaque-UV and degenerate-triangle checks (1,440 UV samples).
Physical meshes are closed/manifold and flat shaded. Preview was reviewed
from front, angled, rear, side and above. Godot runtime/import was not tested.
Unrelated legacy assets still fail the global validator; this family passes.

See source_audit.json, model_info.json, mesh_integrity.json and
export_validation.json. Build/export scripts: tools/art/build_access_button.py
and export_access_button.py, executed through Blender Lab MCP.
Preview PNG: docs/ai/art/access_button_preview.png.
