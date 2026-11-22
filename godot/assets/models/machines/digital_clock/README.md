# Digital wall clock

Four static GLB + editable BLEND presets: digital_clock_1256, digital_clock_0347, digital_clock_8901 (emagged example), digital_clock_off (also broken / wall-frame appearance). Preview scene and PNG are included. No previous clock model was found.

## Original source and display layers

Source: tgstation code/game/machinery/digital_clock.dm and icons/obj/digital_clock.dmi. Original atlas: godot/assets/tg/icons/obj/digital_clock.png; 128 x 96, tiles 32 x 32, one direction/frame per state. Original pixels preserved; exact embedded PNG bytes verified. No derived texture.

Source update_overlays omits digits when NOPOWER or BROKEN. Normal display uses round time; EMAGGED chooses random hours and minutes from 0 to 99. These are static examples, not clock gameplay. The source currently conditions hours-tens on station_minutes >= 10; no runtime logic or bug fix is introduced here.

Separate Hours_Tens_Overlay, Hours_Ones_Overlay, Minutes_Tens_Overlay, Minutes_Ones_Overlay and Separator_Overlay. Original pixel_w offsets are -14, -10, -4, 0 and 0. All ten digit glyphs 0-9 occur across the three illuminated presets and remain independently editable in the library. Equal glyphs share local mesh data where possible. Layer origins retain the source tile coordinate convention; offsets are object transforms.

## Geometry and material slots

Continuous_Stepped_Housing: closed shell with a lined display recess and pixel-aligned stepped corners. Recessed_Display: separate closed dark screen. Closed_Tinted_Glass: separate closed lens, 0.003375 m thick, source screen tint and alpha 0.10. Digit layers are intentional open pixel sheets. No invented movable lid or hinge.

- Digital_Clock_Original_Atlas: original opaque base atlas, flat shading.
- Digital_Clock_Glass: same original atlas, blended alpha 0.10, roughness 0.22.
- Digital_Clock_Digit_Overlay: original blue glyph pixels, emission 1.2 for presentation; source has blue light, but no engine light nodes are exported.

All physical faces have original-atlas UVs, including sides, top, back and underside. The rear reuses the original blank framed-panel artwork; no distinct rear sprite exists. Nearest sampling in Blender and GLB. The same housing/display/glass geometry is shared across presets.

Scale: 0.90 m / 32 pixels. Width 0.646875 m, height 0.365625 m, depth 0.084375 m. Origin at rear mount center, front Blender -Y / Godot +Z. Rotate this one model for wall directions.

## Checks and output

Multi-angle material review complete. Four exports passed original PNG byte identity and nearest sampler checks, with 3,120 opaque UV samples, zero transparent-source hits and zero degenerate triangles. All solid parts are manifold; overlay sheets are intentionally open. Counts: model_info.json; geometry: mesh_integrity.json; UV: export_validation.json. Godot runtime/import not tested. No animation, collisions or gameplay integration.

Build/export through Blender MCP: tools/art/build_digital_clock.py and tools/art/export_digital_clock.py. PNG: docs/ai/art/digital_clock_preview.png. Global validation still reports unrelated earlier treasure_chest, locker_freezer and mini_fridge failures.
