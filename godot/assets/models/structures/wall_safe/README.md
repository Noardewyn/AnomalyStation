# Wall Safe

Common wall_safe mesh covers /obj/structure/secure_safe and /hos (7+1 Meta placements). Captain spare safe has density TRUE and tabletop offset: excluded. Three source states: wall_safe (unlocked), wall_safe_locked, wall_safe_broken. Open is an additional static inspection pose, not a separate source state. Panel_open in source is a service-access flag without separate artwork. Door hinge on left edge opens outward by -105 degrees about Blender Z; rotating pixel-stepped dial uses local Y; nine keys are separate. Shell is recessed with closed back and lined walls.

Source: `code/game/objects/structures/secure_safe.dm; code/datums/components/lockable_storage.dm`. MetaStation placements: 8.

## Geometry and textures

Wall_Safe_Shell_Original uses unchanged storage atlas; Safe_Door_<variant>_Original shifts UVs between three original tiles. Rear door uses blank metal pixels without repeating the status light.

Scale 0.90/32 metres per source pixel. Blender front is -Y, vertical Z. Flat shading, nearest filtering. Pixel boundaries preserved in silhouettes; original top projection is mapped onto physical roof surfaces for safe and machine. Side, back and cavity textures reuse original opaque casing regions; these views are reconstructions because the source has no rear view. No derived PNGs. Source atlas RGBA matches TG DMI, and exact original PNG bytes are verified in every GLB.

Identical geometry shares mesh datablocks in the library, with per-object material overrides. Individual GLBs contain their own geometry. Editable BLEND and GLB per preset plus shared library and preview BLEND. No animations, collisions or gameplay integration.

## Verification

All physical parts manifold; no degenerate triangles or sampled transparent-pixel hits on opaque geometry. Original PNG bytes and nearest samplers verified. Preview reviewed from front, oblique, side and rear; safe also open. Godot runtime/import not tested. Global validator still reports unrelated existing asset failures; family export_validation.json passes.

| Preset | Triangles |
|---|---:|
| wall_safe_broken_lock | 232 |
| wall_safe_locked | 232 |
| wall_safe_open | 232 |
| wall_safe_unlocked | 232 |
