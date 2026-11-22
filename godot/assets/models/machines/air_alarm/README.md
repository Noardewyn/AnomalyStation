# Air Alarm — original atlas

11 editable Blender models and matching GLBs. All faces use the unchanged
`res://assets/tg/icons/obj/machines/wallmounts.png`; tiles are 32 × 32.
Nearest filtering, flat shading; no generated texture, animation or collision.

## Variants

| Files (`air_alarm_` prefix) | Base | Overlay |
|---|---|---|
| normal / warning / hazard | alarmp | alarm0 / alarm2 / alarm1 |
| unpowered | alarmp | none |
| remote_normal / remote_warning / remote_hazard | alarmp_remote | alarm0 / alarm2 / alarm1 |
| remote_unpowered | alarmp_remote | none |
| frame | alarm_b1 | none |
| circuit_unwired | alarm_b2 | none |
| panel_open | alarmx | none |

Source behavior: `tgstation/code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm`,
`update_icon_state` and `update_overlays`. `alarmx` is an open service panel,
not a broken state. Broken, shorted and unpowered suppress status overlays;
reuse the unpowered model. Open construction states also suppress overlays.
Animated source overlays use their first frame; all frame rectangles remain in
`source_states.json` for reference. No animation clips were added.

## Geometry and materials

Scale is 0.90 / 32 m per source pixel. Closed width is 0.646875 m.
The housing has a lined cavity; cover, glass, removable circuit board, lower
fins, terminals and wires are separate parts. Identical geometry is shared
`Service_Cover_Hinge` rotates about local Blender X at (0, -0.106875, 0.140625) m;
open variants show a -110 degree cover rotation. Front faces Blender -Y / Godot +Z
after standard glTF axis conversion. Rotate the reusable asset for placement.

Material slots: `Air_Alarm_Housing_Original` (opaque),
`Air_Alarm_Glass_Original` (alpha 0.88, closed thickness),
`Air_Alarm_Overlay_Original` (source alpha mask, emission 1.2).
Display and upper-terminal overlays remain separate meshes. Hidden faces reuse
opaque regions of the source atlas; these are reconstructed views.

## Validation and preview

`mesh_integrity.json` records manifold checks for solids; status overlays are
intentional surface meshes. `export_validation.json` verifies original embedded
PNG bytes, nearest samplers, UV samples and nondegenerate triangles for all 11
exports. No transparent pixels were sampled on opaque surfaces. Inspected the
front, back, top, angled and service views in Blender. Godot runtime was not run.

Build/export scripts: `tools/art/build_air_alarm.py`, `tools/art/export_air_alarm.py`.
