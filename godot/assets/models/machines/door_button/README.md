# Door buttons

21 GLB presets with matching editable Blender files, a shared-geometry library
and a preview scene. Duplicate audit found no previous door button model;
the existing light switch is a separate object. This family covers 108 mapped
buttons and related subtypes in the wave-two table.

## Variants and source behavior

Reference: `tgstation/code/game/machinery/buttons.dm`, `update_icon_state`,
`update_overlays`, `update_appearance` and interaction feedback.

Both standard and warning skins have these suffixes:
`normal`, `nopower`, `success`, `error`, `open_empty`, `open_board`,
`open_device`, `open_signaler`, `open_board_device`, `open_board_signaler`.
Names begin with `door_button_` or `door_button_warning_`.
`door_button_assembly` is the additional unassembled preset.

Open-panel appearance takes priority over power state. NOPOWER and BROKEN use
the same unpowered skin. Closed powered buttons use `button-light-mask`;
success/error feedback is an independent original overlay. Open panels show
the access board only when present, and either the standard device or signaler
as appropriate. These three modules remain separate closed meshes.

Presets use the first source frame. Both feedback frames and all original state
rectangles remain in `source_states.json`; no animation was added.

## Geometry and textures

`Continuous_Housing` is a closed solid with a lined recessed compartment.
`Service_Cover` and `Push_Button` are separate meshes, parented to
`Cover_Hinge`. The lower hinge is at Blender (0, -0.05765625, -0.0703125) m;
open presets rotate +110 degrees about X. The button records its local press
axis and 0.003 m travel as metadata only. No press animation is included.

Scale is 0.90 / 32 m per pixel. Closed housing dimensions are 0.28125 m wide,
0.196875 m high and 0.05625 m deep, with the cover and button projecting
slightly farther. The upper perspective rows map to the physical roof.
Back lies at Blender Y = 0; front faces Blender -Y / Godot +Z. Rotate the same
model for wall orientations. Identical meshes are shared across library scenes.

The sole texture is the original 640 x 608 atlas
`res://assets/tg/icons/obj/machines/wallmounts.png`, with 32 x 32 tiles.
All sides, rear and liners reuse opaque regions of that atlas. Sampling is
nearest, shading flat. No derived texture is used. Blender references the
original atlas relatively, and GLB embeds its unchanged PNG bytes.

Physical parts use `Door_Button_Original`. `Glow_Mask_Overlay` keeps original
skin colors and applies the original light-mask alpha through separate emission
strengths (alpha times 0.4); it does not paint the white mask over the artwork.
`Feedback_Overlay` retains source color and alpha in its own material and sits
ahead of the glow layer to prevent coplanar overlap. Effect sheets are
intentionally open; all physical meshes are closed. There is no glass.

## Verification and rebuild

58-186 triangles per preset. All 21 exports passed UV sampling, nondegenerate
triangle checks, original embedded PNG verification and nearest-filter checks.
Physical meshes are closed and flat shaded. The source PNG matches DMI pixels.
Open and closed appearances were reviewed, including front, rear, side, top
and angled views. Godot runtime was not run. No collisions or gameplay
integration were added.

Reports: `source_audit.json`, `mesh_integrity.json`, `export_validation.json`.
Build through Blender MCP with `tools/art/build_door_button.py`, followed by
`tools/art/export_door_button.py`. Preview: `docs/ai/art/door_button_preview.png`.
