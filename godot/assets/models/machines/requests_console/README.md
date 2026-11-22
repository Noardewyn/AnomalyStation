# Requests console

Six GLB presets with matching editable Blender files, a shared-geometry library
and a Blender preview scene. Only the original
`res://assets/tg/icons/obj/machines/wallmounts.png` is used: 640 x 608 pixels,
32 x 32 tiles, nearest filtering and flat shading. Embedded PNG bytes match
the source; its RGBA pixels match the original tgstation DMI.

## Presets and source behavior

Reference: `tgstation/code/game/machinery/requests_console.dm`,
`update_overlays` and `crowbar_act`.

| Suffix after requests_console_ | Composition |
|---|---|
| off | req_comp_off |
| normal | off + req_comp0 |
| new_message | off + req_comp1 |
| high_priority | off + req_comp2 |
| emergency | off + req_comp3 |
| panel_open | off + geometry based on req_comp_open |

Opening the service panel or losing power suppresses both screen overlays.
Emergency/extreme priority selects state 3, high priority selects state 2,
normal message priority selects state 1, and the idle display uses state 0.
Hacking has no separate visual in the inspected source. The wallframe item
reuses the off state. All source frames and rectangles remain documented in
`source_states.json`; presets use the first frame, with no animations added.

## Geometry and materials

Scale is 0.90 / 32 m per pixel. The main housing is 0.675 m wide,
0.590625 m high, and 0.196875 m deep at the projecting keyboard.
Back is at Blender Y = 0; front faces Blender -Y / Godot +Z.
Rotate this reusable model for other wall orientations.

`Continuous_Housing` is a closed stepped mesh with lined screen recesses and
a service opening. `Keyboard_Panel` follows the lower housing slope.
`Status_Glass` and `Message_Glass` are separate closed tinted slabs with
0.00365625 m thickness and alpha 0.25. The two independent emissive screen
overlays sit just ahead of the glass to preserve crisp pixel colors.

`Service_Panel` is parented to `Service_Panel_Hinge`, rotating about Blender X;
the open preset uses +110 degrees. Its pivot is
(-0.3234375, -0.11390625, -0.16875) m. `Service_Wiring` sits inside the lined
opening. Identical geometry is shared between variants in the library.

Each mesh has one material slot: `Requests_Console_Housing_Original` (opaque),
`Requests_Console_Glass_Original` (alpha 0.25), or
`Requests_Console_Overlay_Original` (source alpha mask, emission strength 1).
Sides, roof, rear, underside and recess liners reference original opaque atlas
regions. Rear bands reuse housing and keyboard pixels; the source provides no
rear-view sprite. No derived texture was created.

## Validation and rebuilding

All six exports have 226-232 triangles. `mesh_integrity.json` verifies closed
solid meshes and flat shading; screen overlay sheets are intentionally open.
`export_validation.json` verifies UV samples, nondegenerate triangles, nearest
samplers and unchanged embedded PNG bytes. Front, back, side, top and angled
views were inspected. Godot runtime was not run. No collisions, animation or
gameplay integration are included.

Build through Blender MCP with `tools/art/build_requests_console.py`, then
`tools/art/export_requests_console.py`. Preview image:
`docs/ai/art/requests_console_preview.png`.
