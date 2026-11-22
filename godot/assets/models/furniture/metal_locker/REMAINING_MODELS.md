# Remaining storage models

All models use the unchanged original `res://assets/tg/icons/obj/storage/closet.png`. Each GLB embeds the byte-identical PNG with nearest filtering. Doors and lids are separate meshes with pivots on their hinges; models are exported closed without baked animations or collisions.

| File | Contents | Triangles |
|---|---|---:|
| `wood_cabinet.glb` | Two doors, divided interior and shelves | 144 |
| `gun_cabinet.glb` | Two mesh-screen doors, rack supports | 156 |
| `abductor_locker.glb` | Chamfered header, two doors, shelves | 116 |
| `mini_fridge.glb` | Small refrigerator, door, shelf | 128 |
| `magic_locker.glb` | Green special locker, door, floor tray | 84 |
| `old_locker.glb` | Old locker with separate door | 72 |
| `cardboard_box.glb` | Hollow box, four folding flaps | 108 |
| `metal_box.glb` | Hollow metal box, two lid panels | 84 |

`remaining_storage_library.blend` contains the eight source models and 24 appearance/frame entries, each closed and open. Counts include all seven frames of each green special-locker animation, not 99 distinct shapes.

## Appearance changes in Godot

`remaining_variants.json` maps each appearance to a shared model and original sprite rectangles. `godot_material_uv_offsets` provides Godot UV1 offsets relative to that GLB's base materials, keyed by source state. Keep mesh resources shared; duplicate/cache materials per appearance and apply surface overrides. Preserve separate front and reverse materials. Set UV scale to 1 and texture filtering to nearest. The gun cabinet requires alpha blending on its screen material.

Cardboard, agentbox and cardboard_roroco share one model. Cursed and decursed share one model, with seven texture frames each. `source_frame_delays` preserves source timing metadata; texture playback is not implemented in game. Fullcabinet is the source precomposed appearance of the wooden cabinet and shares its model/artwork.

Moving parts have `open_axis` and `open_degrees` custom properties in Blender coordinates. On Godot import, Blender Z maps to Godot Y, Blender X to Godot X, and Blender Y to negative Godot Z. The gallery shows opening examples.

## Source limitations

Hidden surfaces and depth are reconstructed from the front-facing artwork. Oldcloset has no open-body sprite, so its hollow interior is reconstructed. Wooden and abductor door backs have no separate reverse sprite and use solid samples from their original body. Gun cabinet screens retain source transparency on both faces. Weapon sprites are contents and are not baked into the cabinet; cardboard_special is an alert overlay, not another box.

Build with `tools/art/build_remaining_storage.py` in Blender, then `tools/art/build_storage_complete_showcase.py` after the original metal gallery exists. `validation.json` records mesh counts, triangle counts, nearest filtering and source PNG equality. Exports and rendered appearance were checked; Godot runtime behavior has not been tested.

