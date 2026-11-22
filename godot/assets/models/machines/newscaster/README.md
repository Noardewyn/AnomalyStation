# Newscaster

Ten GLB presets with matching editable Blender files, plus a shared-geometry
library and preview scene. Uses only the untouched original
`res://assets/tg/icons/obj/machines/wallmounts.png`, a 640 × 608 atlas of 32 × 32
tiles. PNG bytes embedded in GLB match the source; pixels match tg's DMI.
Nearest filtering, flat shading, no generated textures.

## Presets and source behavior

Reference: `tgstation/code/game/machinery/newscaster/newscaster_machine.dm`,
`update_overlays`, `print_paper`, and `/obj/item/wallframe/newscaster`.

| File suffix (`newscaster_`) | Composition |
|---|---|
| off | newscaster_off |
| normal | off + newscaster_normal |
| wanted | off + newscaster_wanted |
| wanted_alert | off + wanted + newscaster_alert |
| normal_crack_1 / normal_crack_2 | normal + crack1 / crack2 |
| off_crack_1 / off_crack_2 / off_crack_3 | off + matching crack |
| assembly | newscaster_assembly construction item |

Screen and alert are emissive independent layers; cracks are independent,
non-emissive alpha-masked layers in front of the screen. Power loss or breakage
suppresses the display and alert. The alert is shown only for an active wanted
issue with `alert` set. Integrity bands choose one crack layer, never accumulate
all three. The broken preview is off + crack3; the break threshold is 25%.
Crack layers can also be combined with wanted screens using the shared meshes.

The normal, wanted and alert textures use their first source frame; no animation
was added. All frames/delays/rectangles remain in `source_states.json`.
`newscaster_bounty` exists in the atlas but is not referenced by the inspected
tg code; it remains documented as source metadata, not an invented runtime state.

## Shape and parts

Scale is 0.90 / 32 m per pixel. The installed terminal is 0.61875 m wide,
0.703125 m high and approximately 0.205 m deep at the keyboard. Its origin is
the wall reference, back at Blender Y = 0; front faces Blender -Y / Godot +Z.
Rotate the same model to face different walls.

`Continuous_Housing` is one closed manifold shell with a recessed screen,
stepped silhouette, projecting sloped keyboard deck and lined paper slot.
`Keyboard_Panel` follows the slope; the original perspective becomes geometry.
The source can accept paper and print newspapers; no paper prop, animation,
collision or gameplay integration is included.

`Recessed_Display_Glass` has closed 0.0028125 m thickness, original dark-green
tint and alpha 0.80. `Screen_Overlay` sits just ahead of the glass to retain crisp
pixel art without transparency noise. `Alert_Overlay` follows the upper housing;
`Damage_Overlay` is in front of both. No moving mechanism or invented hinge.

All sides, back, roof, underside and slot liners use original opaque atlas
regions. Rear metal bands and ventilation pattern are reconstructed from the
original housing and keyboard regions; there is no source rear-view sprite.

## Material slots and validation

Each part has one slot: `Newscaster_Housing_Original` (opaque),
`Newscaster_Glass_Original` (alpha 0.80), `Newscaster_Overlay_Original` (source
alpha mask and emission 1), or `Newscaster_Overlay_Unlit` (cracks, no emission).

`mesh_integrity.json` checks closed solids and flat shading; overlay sheets are
intentionally open. `export_validation.json` checks UV samples, nondegenerate
triangles, nearest samplers and unchanged embedded PNG bytes. Visual inspection
covers front, back, side, top and angled views. Godot runtime was not run.

Build via Blender MCP: `tools/art/build_newscaster.py`, then
`tools/art/export_newscaster.py`. Preview: `docs/ai/art/newscaster_preview.png`.
