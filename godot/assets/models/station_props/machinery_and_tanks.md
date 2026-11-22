# Volume reconstruction: generator, disposal and portable tanks

The water/fuel trolleys from this older batch have been replaced by the eight [chemical tank variants](chemical_tanks/README.md). Their four standalone GLB/BLEND files were removed; the shared historical preview/library also contains other machinery and is retained.

This revision replaces the 13 individual BLEND/GLB files listed below. Superseded general libraries and images were removed; use these files for this batch. Stationary tank files are untouched.

The original sprites are oblique references, not frontal photographs. Generator jackets now have full depth, stepped shoulders, vents and coil geometry. Chemical trolleys contain two cylindrical vessels with bands and supports. Portable canisters have circumferential barrel UVs, stepped shoulders, a regulator, bridge handle and separate pressure knob. The disposal has a closed shell and a lined, deep hopper. Unseen sides are reconstructions from the visible construction and source palette.

All structural bodies are connected, manifold solids with internal intersections removed. Working controls and hatches retain separate pivots. Each part has one material in slot 0, named `Surface_<model>`. The nine portable variants share their actual Blender mesh data; water/fuel geometry is shared as well. Flat shading and nearest filtering are retained.

Original PNGs are unchanged. Thirteen separate 64 × 64 derived surface sheets are stored in `textures/*_surfaces.png`. Each sheet contains sixteen 16 × 16 cells. `surface_rebuild_recipes.json` records the source state and pixel rectangles. Source fragments are sampled with nearest filtering; circumference patches use reflected repetition, and transparent silhouette margins use the nearest opaque source texel. Top, bottom, side, band and control surfaces have their own UV regions. The disposal lever is physical geometry, so its printed duplicate is removed from the derived front patch. No AI-generated image textures were used.

Validation: 13 GLBs, 11,128 instantiated triangles, 111,280 opaque UV samples, no transparent pixel hits, no degenerate triangles, no collapsed UV triangles, positive volumes and manifold edges/vertices. Each exported part has one connected component. The models were inspected from front, right, back, three-quarter, top and bottom views (top/bottom by geometry family). No animations, collisions or gameplay integration were added. Godot runtime/import remains untested.

The Godot preview is `previews/machinery_and_tanks_preview.tscn`, relative to the independent `project.godot` in this directory.

## Models

| Model | Triangles | Dimensions XYZ (m) |
|---|---:|---|
| [pacman_generator](power/pacman_generator.glb) | 1078 | 0.995 × 0.71 × 0.83 |
| [disposal_unit](utility/disposal_unit.glb) | 68 | 0.64 × 0.67 × 0.88 |

## Review sheets

See `docs/ai/art/quality/machinery_and_tanks.png`, `tank_variants_1.png`, and `tank_variants_2.png`. Original references are in `docs/ai/art/machinery_and_tanks_reference.png`.

## Reproduction

In the existing Blender builder session, run `tools/art/rebuild_round_props.py`, inspect the models, then run `tools/art/export_round_props.py`. The scripts use the live `station_props_build` namespace and affect only this batch. Run `python tools/art/validate_station_props.py --volume-only` for independent GLB checks. Do not run the older full-library builder to reproduce this batch: it contains the superseded shapes.

Superseded files and temporary backups were removed after acceptance. See `docs/ai/art/asset_cleanup_manifest.json` for the cleanup inventory.
