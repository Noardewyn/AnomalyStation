# Status displays

29 GLB presets and matching editable Blender files share one stepped enclosure.
The unchanged `res://assets/tg/icons/obj/machines/status_display.png` is the only
texture: 1088 × 1088, 32 × 32 tiles. Source PNG pixels match the original DMI;
embedded GLB PNG bytes are unchanged. Nearest filtering and flat shading.

## Variants and layers

Presets include blank, text_ready, the eight station pictures (default,
greenalert, bluealert, redalert, deltaalert, biohazard, lockdown, radiation),
and the 19 legacy AI states named `ai_*` without the `ai_status_*` prefix.
All are listed with exact source states in `model_info.json`.
The extended AI-core portraits and entertainment-specific enclosure are not
additional presets in this set; their source rectangles remain in metadata.
They can reuse the picture surface after selecting the appropriate atlas tile.

Reference: `tgstation/code/game/machinery/status_display.dm`,
`code/_globalvars/lists/status_display.dm`, and the AI status display picker.
The `frame` is the base; pictures are separate overlays. `outline` is the
emissive mask, not an opaque colored overlay to paint over the picture.
Picture materials emit their own original colors; `ai_off` is unlit.
Power loss, blank mode and BROKEN clear the picture and show the blank body.
The atlas's `broken` artwork is therefore not labeled as a runtime broken preset.

The current tg implementation renders two dynamic text lines with TinyUnicode
maptext, rather than assembling its old atlas glyph states. `text_ready` has
two independently replaceable blank surface meshes with local UVs and source
maptext offsets (-4 and -11), preserving the 22-pixel static width. They contain
no invented text, font texture or runtime renderer. Text integration is deferred.

All animated pictures use frame zero. Original frames/delays/rectangles are
retained in `source_states.json`; no animation clips were added.

## Geometry and materials

Scale: 0.90 / 32 m per pixel. Body width 0.90 m, height 0.675 m, depth 0.1125 m.
The wall plane is Blender Y = 0. Front faces Blender -Y / Godot +Z; rotate one
assembly for different walls. Pixel-aligned steps preserve the source silhouette.

`Continuous_Frame` is a closed, connected shell with a lined screen recess.
`Recessed_Glass` is a closed slab, 0.00365625 m thick, original dark tint,
alpha 0.25. `Picture_Overlay` is a separate surface just in front of the glass.
Roof, sides, back and underside reuse opaque source frame strips and panel
bands; hidden views are reconstructed from original pixels, not new textures.

Each part has one material slot: `Status_Display_Housing_Original` (opaque),
`Status_Display_Glass_Original` (alpha 0.25),
`Status_Display_Overlay_Original` (source alpha mask, emission 1), or
`Status_Display_Overlay_Unlit` (source alpha mask, no emission).
Identical shell and glass meshes are shared in the variants library.

## Verification and files

176–186 triangles per preset. All solids are manifold and flat-shaded;
picture/text surface sheets are intentionally open. 38,320 opaque UV samples
passed with no transparent-pixel hits or degenerate triangles. Original image
bytes and nearest samplers were verified. See `mesh_integrity.json`,
`source_audit.json`, and `export_validation.json`.
Inspected front, rear, side, top and angled views. Godot runtime was not run.

Build via Blender MCP: `tools/art/build_status_display.py`, then
`tools/art/export_status_display.py`. Preview scene:
PNG: `docs/ai/art/status_display_preview.png`.
No collisions, animations or gameplay integration were added.
