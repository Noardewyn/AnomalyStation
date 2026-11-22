# Wall airlock pressure controller

Source type: /obj/machinery/airlock_controller. This controls paired doors,
airlock pressure sensors and vent pumps. It is distinct from the smaller
/obj/machinery/door_buttons/airlock_controller already modeled in airlock_controller.
Source: code/game/machinery/embedded_controller/airlock_controller.dm.
Incinerator variants share appearance. No duplicate model was found.

Four static presets: standby, process_0, process_1, off.
Source update_icon_state selects process while processing and standby otherwise.
Off is preserved from the atlas; its automatic runtime selection is not asserted.
Process frames have delays [3, 3]. No separate sprite overlays are defined.
No animations, collisions or gameplay integration added.

Original atlas: godot/assets/tg/icons/obj/machines/wallmounts.png, 640 x 608,
32 x 32 tiles. Pixels match the source DMI. GLB embeds unchanged original PNG
bytes; BLEND references the original atlas. No derived textures.
All surfaces have atlas UVs, nearest filtering and flat shading. The upper
three perspective rows become the roof. Side and rear mapping are a 3D
interpretation using original casing pixels, with no new painted detail.

Continuous closed housing; eight separate push buttons, screen and indicator.
Button origins are at rear attachment centers; local +Y points into the body.
No added glass. All eleven meshes are shared across the four library scenes.
Material slot 0: Airlock_Pressure_Controller_<state>_Original on each part.
Variant atlas offsets use KHR_texture_transform on export.

Scale: 0.90 / 32 m per pixel. Housing 0.309375 m wide, 0.309375 m tall,
0.084375 m deep. Buttons protrude 0.00421875 m. Rear-center origin;
front Blender -Y / Godot +Z. Rotate the same model for different walls.

132 triangles per preset. All four pass original PNG bytes, nearest sampler,
opaque UV samples and degenerate-triangle checks. All physical meshes closed
and manifold. Preview reviewed from front, top, rear and side.
Godot runtime/import not tested. Unrelated older assets still fail the global
validator; this family passes. See export_validation.json and mesh_integrity.json.

Matching GLB and editable BLEND files are provided, plus variants_library.blend
Render:
docs/ai/art/airlock_pressure_controller_preview.png.
Built through Blender Lab MCP using tools/art/build_airlock_pressure_controller.py
and tools/art/export_airlock_pressure_controller.py.
