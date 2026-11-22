# C4 charge

Five static GLB presets with matching editable Blender files, a shared-geometry
library and a preview scene. This is a visual game prop only: no animation,
collision or gameplay integration is included.

## Source and variants

References: `tgstation/code/game/objects/items/grenades/plastic.dm` and
`_grenade.dm`. The original atlas is
`res://assets/tg/icons/obj/weapons/grenade.png`, 480 x 480 pixels with 32 x 32
tiles. No texture was generated or modified. GLB embeds the exact original PNG;
Blender stores a relative reference to it. Sampling is nearest, shading flat.

| File suffix after c4_charge_ | Source state / frame (one-based) |
|---|---|
| idle | plastic-explosive0 / 1 |
| active | plastic-explosive0_active / 1 |
| active_bright | plastic-explosive0_active / 2 |
| legacy_green | plastic-explosive1 / 1 |
| legacy_white | plastic-explosive1 / 2 |

The attached-target overlay `plastic-explosive2` has the same pixels as the
two active frames, so it reuses those models. The inspected item code selects
the idle state, inherited active state and attached-target overlay; it does not
explicitly select `plastic-explosive1`. Its two appearances are retained as
legacy/reference presets. These variants differ only in six display pixels.
All source rectangles, delays and state metadata are in `source_states.json`.
There is no extra panel-open appearance: the source exposes its wire panel
without a screwdriver. X4 is a different item and is not included.

## Geometry and material slots

Four closed meshes: `Continuous_Charge_Casing`, `Timer_Module`,
`Stepped_Cable_Loop`, and `Timer_Display`. The casing includes the small stepped
left tabs; the timer projects forward and the cable is a solid stepped loop
with an open center. The display is independently replaceable. No rounded
bevels or silhouette cuts through pixels are used.

Scale is 0.90 / 32 m per pixel. Overall dimensions, including cable and tabs,
are 0.365625 m wide, 0.45 m high and approximately 0.114834 m deep. The local
origin is centered horizontally and vertically on the full prop, with its back
at Blender Y = 0. Front faces Blender -Y / Godot +Z. Rotate the same prop for
placement; no direction duplicates are needed.

`C4_Original_Atlas` is the opaque casing, timer and cable material.
`C4_Display_Original` uses original display colors with emission strength 0.4.
Neither material uses transparency. The casing, timer and cable meshes are
shared across all scenes in the variants library. Each individual GLB is
self-contained. Changing presets does not require replacing the outer shape.

Front pixels follow the reference silhouette. Roof, side and rear UVs reuse
opaque metal, trim and band regions from the same original sprite. The rear
is a 3D interpretation because the source has no rear-facing sprite. Cable
surfaces reuse nearby original cable pixels, including its red segment.

## Verification

252 triangles per preset. All five exports passed 12,600 opaque UV samples,
nondegenerate-triangle checks, nearest-filter checks and embedded PNG-byte
verification. Solid meshes are closed and flat-shaded. Front, rear, side, top
and angled views were inspected. The original PNG's pixels match tg's DMI.
Godot runtime was not run.

Reports: `source_audit.json`, `mesh_integrity.json`, `export_validation.json`,
and `model_info.json`. Build through Blender MCP with
`tools/art/build_c4_charge.py`, then `tools/art/export_c4_charge.py`.
Preview: `docs/ai/art/c4_charge_preview.png`; source contact sheet:
`docs/ai/art/c4_source_reference.png`.
