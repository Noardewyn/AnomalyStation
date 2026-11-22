# Original atlas audit — 2026-10-17

Follow-up: both console bodies were rebuilt on 2026-10-17. The `console_bronze` UV defect mentioned below is now resolved. Both console exports pass opacity and degenerate-triangle checks; the three unrelated storage-model defects remain. See `godot/assets/models/machines/consoles/export_validation.json`.

All 201 exported GLBs use original atlas PNG bytes. All 150 saved Blender files reference original atlas paths. All 48 Godot material overrides reference `res://assets/tg/`. The 42 atlases embedded in GLBs have identical dimensions and RGBA pixels to their source DMI files in the adjacent tgstation checkout.

125 GLBs had derived images at the start. Their UVs/materials were migrated, including 43 table modules, 67 vending machines, 11 portable tanks, generator, disposal, metal chair and standalone metal table. 92 Blender files were migrated; one additional storage library had its stale image path corrected. Geometry and pivots were preserved. Stationary tank assets were not modified.

The GLB format embeds the original PNG bytes for portability; these are not repainted or cropped replacements. Blender uses original image paths. Solid glass/tint materials remain material parameters. Separate light layers use the original emissive sprite tile. Side/back textures reuse source pixel regions; they no longer use invented panel artwork.

Evidence:

- `original_atlas_audit.json`: all 201 GLBs, SHA-256 matches, no unmatched images.
- `blend_atlas_audit.json`: all 150 Blender files, source paths and image hashes.
- `original_atlas_sources.json`: 42 DMI pixel comparisons and 48 material paths.
- `original_atlas_changes.json`: migrated Blender files.
- `all_models_uv_validation.json`: additional UV/geometry inspection of all 201 exports.

The migrated vending and volume-prop groups pass opacity/geometry checks. The broader UV audit also found pre-existing transparent-source pixels sampled by opaque faces in four original-atlas models: treasure_chest (264 samples), locker_freezer (18), mini_fridge (81), and console_bronze (2). These are separate UV defects, not replacement-texture dependencies, and were not altered in this atlas migration. No degenerate triangles were found across all 201 exports. Stationary tanks retain their existing nearest-mipmap-nearest minification setting.

Updated preview: `vending_original_atlas_preview.png`. Saved catalog and assembly Blender scenes use the corrected materials. Older preview PNGs document the previous texture revision. Godot runtime/import has not been tested.

Legacy generation scripts and unused derived PNGs are retained as historical build inputs. Do not run legacy builders and publish their output without running the original-atlas audit and UV migration. The saved BLEND files are the current editable source of truth; no current exported model or saved Blender file uses those derived PNGs.
