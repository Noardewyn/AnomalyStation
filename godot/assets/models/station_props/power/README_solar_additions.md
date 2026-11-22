# Solar additions: missing variants only

Duplicate audit found `solar_panel.glb/.blend` and
`solar_panel_frame.glb/.blend` already present. They were reused as the
reference and were not regenerated or overwritten. The three additions live
beside them, rather than in a duplicate solar asset directory.

| New asset | Source composition | Triangles |
|---|---|---:|
| solar_panel_broken | sp_base + solar_panel_glass-b | 948 |
| solar_tracker | tracker_base + tracker_edge + tracker | 990 |
| solar_tracker_frame | tracker_base | 192 |

Each has GLB and editable Blender exports.

## Original source and behavior

References: tgstation `code/modules/power/solar.dm` and `tracker.dm`.
The broken panel uses the damaged state for both panel and edge, as in
`update_overlays`. The tracker uses independent `tracker` and `tracker_edge`
layers above `tracker_base`; the unmounted assembly uses only `tracker_base`.
The source rotates the tracker layers in the floor plane and offsets them
above the pedestal. Accordingly, the 3D tracker head is horizontal, with its
own yaw pivot. No separate broken tracker sprite exists in the inspected code.
This batch covers the table's ordinary glass panel, not other material types.

Only the original 128 x 128 `res://assets/tg/icons/obj/machines/solar.png` atlas
is used, with 32 x 32 tiles, nearest filtering and flat shading. No derived
textures were created. Embedded PNG bytes and source DMI pixel equality are
verified in `solar_additions_source_audit.json` and the export report.

## Geometry and reuse

Pedestal geometry, cable geometry and yaw/tilt hierarchy come from the existing
solar panel. The tracker pedestal is retextured with `tracker_base`, and its
shared pedestal mesh is reused by the tracker frame. The original pedestal is
0.80 m across the feet and 0.58 m high; the head pivot stays at 0.64 m.
New head parts use the existing 0.046 m per source pixel scale, preserving
compatibility with the previously authored solar kit.

The broken panel's missing corner follows whole pixel boundaries. Its backplate,
photovoltaic layer and closed 0.006 m glass cover are separate meshes. Glass
alpha is 0.22. Top and underside use the source damaged artwork; thin edge
walls sample opaque boundary pixels, with no transparent UV holes.

The tracker edge and top surface are separate closed parts using their original
silhouettes and atlas states. The top layer retains the source's cyan cable
detail. Existing yaw and tilt pivots remain editable; no animations were added.
Frame assets omit unused head pivots and swivel hub.

Materials: inherited `Atlas_solar_sp_base` for the reused blue pedestal;
`Solar_Additions_Original` for new opaque surfaces and retextured tracker metal;
`Solar_Tracker_Base_Original` for the reused tracker cable;
`Solar_Additions_Tinted_Glass` for the closed damaged glass cover.

## Validation and rebuild

All three additions passed 18,820 opaque UV samples, nondegenerate triangle
checks, nearest-filter checks and original embedded PNG verification. Physical
meshes are closed and flat shaded. Both assembled variants were inspected from
front, back, side, top and angled views. Godot runtime was not run. No collisions
or gameplay integration were added.

Build through Blender MCP: `tools/art/build_solar_additions.py`, then
`tools/art/export_solar_additions.py`. Preview:
`docs/ai/art/solar_additions_preview.png`. Reports use the `solar_additions_` prefix
to avoid replacing reports for other assets in this existing directory.
