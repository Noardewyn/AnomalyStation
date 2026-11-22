# All-in-one reagent grinder

Four static combinations: reagent_grinder_empty_off, empty_ready, beaker_off and beaker_ready. Each has GLB and editable BLEND; shared library and preview scene included. No duplicate grinder/juicer model was found.

Source: code/modules/reagents/chemistry/machinery/reagentgrinder.dm; kitchen.png and adjacent JSON. All three states juicer, juicer-on and juicer-beaker are 32 x 32, single-direction, single-frame. Source PNG matches the original DMI pixels exactly and is embedded unchanged. No derived texture.

The juicer-on overlay indicates anchored, panel closed and operational, independently of the operating flag. The loaded beaker overlay is separate. Source places a large beaker in maploaded machines and allows replacement/removal while not operating. The ready+beaker preset corresponds to a functioning maploaded machine. Unpowered, unanchored and service-open conditions reuse the off appearance; source supplies no separate maintenance-panel artwork. No dynamic fill-level overlay is declared, so no invented liquid is added.

Continuous solid stepped base, support column and upper hopper. Circular profiles follow integer pixel steps rather than diagonal cuts. The upper cap is separate for editing; no cap interaction is asserted by the game source. Removable_Open_Beaker is a hollow open vessel with integral bottom and closed wall geometry, one source pixel thick. Its pivot is Blender (0.05625,-0.084375,0.225) m, removal axis +Z / Godot +Y. Ready_Overlay is an intentional single-pixel effect sheet on the lower control face.

Materials, slot 0 per part:
- Grinder_Original_Atlas: original kitchen.png, opaque, flat and nearest.
- Grinder_Beaker_Glass: same original beaker sprite, blue tint from source pixels, alpha 0.25, roughness 0.20, blended.
- Grinder_Ready_Overlay: original green pixel, emission 0.4 for readability, no light nodes.

All physical faces, including rear/underside and the circumference of the stepped vessels, sample original atlas patches. Hidden surfaces are reconstructed from original trim regions; there is no separate rear sprite. No flat-color-only material.

Scale 0.90 m / 32 pixels. Width/depth of the circular base 0.39375 m; total height 0.84375 m. Beaker outer width/depth 0.16875 m, height 0.225 m, wall/bottom thickness 0.028125 m. Origin centered on support level; front Blender -Y / Godot +Z. Place at the existing table height 0.75 m.

Four GLBs passed original PNG byte identity, nearest sampling, opaque UV and degenerate-triangle checks. All solid parts manifold; indicator intentionally open. Triangle counts 792-888, including the pixel-stepped circular silhouettes; exact details in model_info.json and export_validation.json. Multi-angle material review complete. Godot runtime/import not tested. No animation, collisions or gameplay integration. Older unrelated global-validator failures remain in treasure_chest, locker_freezer and mini_fridge.

Build/export: tools/art/build_reagent_grinder.py and tools/art/export_reagent_grinder.py through live Blender MCP. PNG: docs/ai/art/reagent_grinder_preview.png.
