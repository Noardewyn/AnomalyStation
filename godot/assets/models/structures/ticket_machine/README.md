# Ticket Machine

Three original states: ticketmachine, ticketmachine_empty and ticketmachine_off (wallframe artwork). Source ready/empty differs in exactly two indicator pixels. Lower bracket remains present in all three states; it is not a removable paper ticket. Separate display enclosure, recessed display, dispenser with lined outlet, rear mounting rail and lower bracket. Queue_Number_Overlay_Slot preserves space for MAPTEXT(current_number); numbers are runtime-generated text, absent from atlas and intentionally not fabricated. Off is supplied atlas artwork, not asserted to be automatically selected by runtime power handling. Existing door_button assets cover remote control button; no duplicate button added.

Source: `code/modules/paperwork/ticketmachine.dm`. MetaStation placements: 1.

## Geometry and textures

Ticket_Machine_<variant>_Original and Ticket_Display_<variant>_Original use offsets into the original bureaucracy atlas. Powered display emission 0.25 for preview; off display unlit.

Scale 0.90/32 metres per source pixel. Blender front is -Y, vertical Z. Flat shading, nearest filtering. Pixel boundaries preserved in silhouettes; original top projection is mapped onto physical roof surfaces for safe and machine. Side, back and cavity textures reuse original opaque casing regions; these views are reconstructions because the source has no rear view. No derived PNGs. Source atlas RGBA matches TG DMI, and exact original PNG bytes are verified in every GLB.

Identical geometry shares mesh datablocks in the library, with per-object material overrides. Individual GLBs contain their own geometry. Editable BLEND and GLB per preset plus shared library and preview BLEND. No animations, collisions or gameplay integration.

## Verification

All physical parts manifold; no degenerate triangles or sampled transparent-pixel hits on opaque geometry. Original PNG bytes and nearest samplers verified. Preview reviewed from front, oblique, side and rear; safe also open. Godot runtime/import not tested. Global validator still reports unrelated existing asset failures; family export_validation.json passes.

| Preset | Triangles |
|---|---:|
| ticket_machine_empty | 182 |
| ticket_machine_off | 182 |
| ticket_machine_ready | 182 |
