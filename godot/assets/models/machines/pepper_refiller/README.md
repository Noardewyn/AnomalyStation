# Wall pepper spray refiller

One original static appearance: pepper_spray_refiller.glb and editable pepper_spray_refiller.blend. Preview and library BLEND files included. No existing model found; stationary tanks were not modified.

Source: code/modules/reagents/reagent_dispenser.dm, /obj/structure/reagent_dispensers/wall/peppertank. This is wall mounted, anchored, non-dense, and cannot be tanked. It dispenses condensed capsaicin. One original state, pepper, one direction and frame, 32 x 32 tile at (128,32). Source declares no separate visual charge states, overlays or moving controls for this subtype. Rotate the reusable asset for wall orientation.

Continuous L-shaped housing with a lined reservoir recess and dispensing niche. Three individual closed reservoirs have a stepped rounded cross-section and stepped caps aligned to pixel boundaries, with original red/gray texture stripes around the circumference. Reservoir origins sit at their lower centers; separation is for editing, not an invented removal mechanic. Nozzle is a separate closed component inside the dispensing niche. The depth, hidden rear and nozzle are 3D interpretations of the supplied single view. No glass is assumed.

All parts use slot 0, Pepper_Refiller_Original_Atlas: chemical_tanks.png unchanged, nearest filtering and flat shading. Front features use their original source regions; hidden, side, underside and rear surfaces reuse original opaque trim patches. No derived textures or flat-color-only materials.

Scale 0.90 m / 32 pixels: width 0.675 m, height 0.646875 m, depth 0.16875 m. Origin centered on the rear mount plane; front Blender -Y / Godot +Z. Source wall-placement offsets are not baked into the mesh.

260 triangles, all physical parts closed/manifold. Multi-angle material inspection completed. 2,600 opaque UV samples: zero transparent-source hits and zero degenerate triangles. Exact embedded original PNG bytes and nearest samplers verified. See mesh_integrity.json, model_info.json, source_audit.json and export_validation.json. Godot runtime/import not tested. No animation, collisions or gameplay integration.

Build/export with tools/art/build_pepper_refiller.py and tools/art/export_pepper_refiller.py through Blender MCP. PNG: docs/ai/art/pepper_refiller_preview.png. The global validator still reports unrelated older treasure_chest, locker_freezer and mini_fridge failures.
