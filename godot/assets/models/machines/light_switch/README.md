# Light switch

Three static presets: `light_switch_on`, `light_switch_off`, and
`light_switch_nopower`. Each has a GLB and editable Blender file. A shared-mesh
library and preview scene are included. No animations, collisions or gameplay
integration were added.

## Reference and behavior

Source: `tgstation/code/game/machinery/lightswitch.dm`, `update_icon_state`,
`update_overlays`, `set_lights`, and `/obj/item/wallframe/light_switch`.
The switch controls its assigned area's lights; multiple switches in that area
update together. NOPOWER selects `light-nopower` and suppresses the emissive
overlay. Powered states use `light-on` / `light-off` and their respective
`light-emissive-on` / `light-emissive-off` masks. The unmounted item reuses the
nopower appearance, so it needs no duplicate model. Screwdriver deconstruction
has no separate open-panel sprite.

The original `wallmounts.png` atlas is 640 x 608, with 32 x 32 tiles. All source
states, frame rectangles and timings are retained in `source_states.json`.
These static presets display the first frame of each state; the original
three-frame transitions are not implemented as animations.

## Geometry, UVs and materials

Scale: 0.90 / 32 m per source pixel. The housing is 0.16875 m wide,
0.225 m high and 0.05625 m deep. The sprite's upper two perspective rows map
to the physical top surface instead of increasing the front panel height.
An additional front top row reuses the original dark bottom-border pixels to close the
frame around all four sides, as requested. The one-pixel border is uniform,
with equal one-pixel inner margins above and below the centered rocker.
Dark one-pixel bands also wrap around the top and bottom of both side faces.
The roof and underside use the same original dark border pixels, closing the
trim symmetrically across the housing depth.
The back lies at Blender Y = 0; front faces Blender -Y / Godot +Z.

`Continuous_Housing` is one shared closed manifold mesh with a lined rocker
recess. `Rocker` is a separate closed mesh, parented to `Rocker_Pivot` at
(0, -0.0478125, 0) m, rotating about Blender X. Static on/off angles
are +7/-7 degrees; this small mechanical tilt is a 3D interpretation.
Rotate the whole assembly for other wall orientations.

All faces use original atlas pixels, including the roof, sides, rear and recess
liners. Rear trim reuses original housing bands because no rear sprite exists.
No derived texture is used. GLB embeds the unchanged original PNG; Blender
references it by a relative path. Filtering is nearest and shading is flat.

Solid parts use `Light_Switch_Housing_Original`. Powered variants retain separate
`Frame_Emission_Overlay` and `Rocker_Emission_Overlay` meshes with two UV sets:
original visible color and original emissive mask. Mask alpha is represented by
per-face emission strengths (alpha times 0.12 on the frame, 0.4 on the rocker).
The overlaid base color matches the underlying sprite, preventing the emissive
mask from replacing the artwork. Overlay materials are opaque, avoiding dither
noise; there is no glass or transparent solid on this asset. These emissive
materials do not implement the source's area lighting or point-light behavior.

## Verification and rebuild

`source_audit.json` records the unchanged PNG hash and equality with DMI pixels.
`mesh_integrity.json` checks closed solids and flat shading; effect sheets are
intentionally open. `export_validation.json` checks UV samples, nondegenerate
triangles, nearest samplers and embedded PNG bytes. Front, rear, side, top and
angled views were inspected. Godot runtime was not run.

Build through Blender MCP with `tools/art/build_light_switch.py`, followed by
`tools/art/export_light_switch.py`. Preview: `docs/ai/art/light_switch_preview.png`.
