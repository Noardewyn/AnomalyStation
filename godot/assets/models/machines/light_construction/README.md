# Wall light construction stages

Four new presets: `light_tube_construction_stage_1`, `light_tube_construction_stage_2`, `light_bulb_construction_stage_1`, `light_bulb_construction_stage_2`. Matching editable BLEND and GLB files, shared library and preview scene are included. Finished empty fixtures already exist in `../lighting`; no duplicates were exported. Floor construction is deferred under the current user preference.

## Source and assembly

Original lighting.png and adjacent JSON were checked against tgstation lighting.dmi: exact RGBA equality, 32 x 32 tiles, four directions and one frame per construction state. Use rotation for direction, not duplicate models. Source: code/modules/power/lighting/light_construct.dm.

Stage 1 is the empty frame. Adding one cable changes it to stage 2; wirecutters remove the wiring. Screwdriver completion creates the corresponding existing empty fixture, which subsequently accepts a tube or bulb. The construction frame itself rejects lamps. Backup cells can be inserted in source but have no visual overlay; no additional battery or invented visible state was added.

Frame/socket geometry follows the existing lighting kit and uses its exact local scale and insertion positions. Continuous closed housings have lined recesses; the tube has a rear support rail. Separate Live_Wire and Neutral_Wire meshes represent the visible stage-2 wiring. Their origins remain at the fixture mounting origin for removal as a set. No hinges are invented. Future_Lamp_Insert records the eventual mating transform; it does not imply the unfinished fixture accepts a lamp.

## Texture and scale

One material slot per mesh: Light_Construction_Original_Atlas. Original lighting.png only, no derived PNGs. Opaque, flat shading, nearest sampling; all sides and recess surfaces use atlas patches. Wire artwork comes from stage 2, not a generated overlay texture. Hidden/back surfaces reuse opaque original regions. Source sprites do not supply a separate rear view.

0.90 m / 32 pixels. Tube width 0.675 m, depth 0.140625 m, height 0.1125 m. Bulb width/depth 0.16875 m, frame height 0.084375 m; wires extend to 0.1125 m. Rear mounting plane is Blender Y=0, front -Y (Godot +Z). Eventual tube insert: (0,-0.0703125,0.0421875) m; bulb: (0,-0.084375,0.05625) m in Blender.

## Validation

Original embedded PNG bytes and nearest samplers verified in all four GLBs. Closed manifold physical parts, no degenerate triangles or transparent-source hits on opaque geometry. Multi-angle material review completed. Detailed counts and UV results: model_info.json, mesh_integrity.json and export_validation.json. Godot runtime/import not tested. No animation, collision or gameplay integration.

Preview: docs/ai/art/light_construction_preview.png. Build/export scripts: tools/art/build_light_construction.py and tools/art/export_light_construction.py via live Blender MCP. Global validator still flags unrelated older treasure_chest, locker_freezer and mini_fridge models.
