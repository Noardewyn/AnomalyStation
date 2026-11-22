# Picture Frame

Runtime integration adds `picture_frame_photo_surface.glb`, a two-triangle surface inside the photo opening. It uses the unchanged original signs atlas and the game's composed appearance, so inserted photographs remain visible without replacing the frame geometry. Rebuild it with `python tools/art/build_photo_surface.py`.

Four persistent showroom photo frames share one empty frame. Bar portrait randomly selects monkey, fruit, rat or Ratvar. Five presets cover those appearances. Continuous stepped wood frame, removable rear board, independent portrait insert; photo variant additionally preserves the white source mat. Photo_Overlay_Slot marks the runtime photo placement; no fabricated photograph provided. Source portraits are described as glued; separate inserts support texture replacement, not a claimed gameplay action. No glass is invented because these source frames do not define a glass overlay.

Source: `code/modules/photography/photos/frame.dm`. MetaStation placements: 5.

## Geometry and textures

Frame_<variant>_Original shifts UVs from frame-monkey to frame-overlay/fruit/rat/ratvar; Frame_Backboard_Original uses frame-empty border pixels. Shape-identical inserts and borders share geometry.

Scale 0.90/32 metres per source pixel. Blender front is -Y, vertical Z. Flat shading, nearest filtering. Pixel boundaries preserved in silhouettes; original top projection is mapped onto physical roof surfaces for safe and machine. Side, back and cavity textures reuse original opaque casing regions; these views are reconstructions because the source has no rear view. No derived PNGs. Source atlas RGBA matches TG DMI, and exact original PNG bytes are verified in every GLB.

Identical geometry shares mesh datablocks in the library, with per-object material overrides. Individual GLBs contain their own geometry. Editable BLEND and GLB per preset plus shared library and preview BLEND. No animations, collisions or gameplay integration.

## Verification

All physical parts manifold; no degenerate triangles or sampled transparent-pixel hits on opaque geometry. Original PNG bytes and nearest samplers verified. Preview reviewed from front, oblique, side and rear; safe also open. Godot runtime/import not tested. Global validator still reports unrelated existing asset failures; family export_validation.json passes.

| Preset | Triangles |
|---|---:|
| picture_frame_photo_empty | 154 |
| picture_frame_portrait_fruit | 120 |
| picture_frame_portrait_monkey | 120 |
| picture_frame_portrait_rat | 120 |
| picture_frame_portrait_ratvar | 120 |
