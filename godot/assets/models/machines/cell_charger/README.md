# Cell charger

Twelve presets reconstructed from the original `icons/obj/machines/cell_charger.png` and adjacent JSON. Atlas pixels match the tgstation DMI exactly. Original 32 x 32 tiles; no derived textures. All 16 MetaStation charger entries also contain a table (see map audit). The APC internal battery uses a different source depiction and remains unchanged.

## Presets

- `cell_charger_empty`: continuous empty dock.
- `cell_charger_level_0` through `cell_charger_level_4`: standard cell with five charge levels; stepped original dock indicator clips.
- `cell_charger_off`: inserted partially charged cell, connector and cell indicator remain; dock status overlay omitted, following source NOPOWER behavior.
- `cell_charger_hcell`, `scell`, `hpcell`, `icell`, `bscell`: five additional original standard-size skins at full charge. Standard `cell` is supplied by the level presets.

Source update_overlays uses round(percent * 4 / 100) for the dock, partial/full cell lights with the 99.5% threshold, and connector-specific overlays. Crystal, slime, lead-acid, grown and large/small battery shapes are outside this batch. Metadata retains all original states and frames; the five level presets and five full-charge skins include timed dock clips. Off and empty presets are static. The preview and shared geometry library show static poses.

## Meshes, scale and materials

Continuous closed L-shaped dock, removable closed battery and contacts, two closed stepped connector leads. The battery pivot is at its lower center; contacts and cell indicator are parented to it. Removal direction is Blender +Z / Godot +Y. Leads belong to the dock and are separate removable meshes. Indicators are intentional transparent effect sheets; physical geometry is closed and flat shaded.

Scale: 0.90 m per 32 source pixels; dock width 0.534375 m, depth 0.28125 m, height 0.365625 m. Origin lies on its support surface; place at 0.75 m for the existing table kit.

Material slots: `Cell_Charger_Original_Atlas` (opaque original atlas) and `Cell_Charger_Indicator_Overlay` (source alpha, slight 0.4 emission for readability). Emission is a presentation choice, not a source light parameter. Blender may append numeric material suffixes. Every physical face, including back and underside, samples the original atlas. Nearest filtering is explicit in Blender and GLB. Back/side UVs reuse appropriate source patches because the source supplies no independent rear view.

The common dock and identical state meshes share Blender mesh datablocks; skin variants share topology but require separate UVs to reference their original atlas rectangles. 28 triangles empty, up to 292 with battery and overlays.

## Deliverables and checks

Each preset has editable BLEND and GLB. PNG: `docs/ai/art/cell_charger_preview.png`.

Multi-angle material review completed. All 12 GLBs passed the original texture identity and nearest filtering, no degenerate triangles and 24,040 opaque UV samples with zero transparent-pixel hits. Closed physical mesh checks are in `mesh_integrity.json`; UV results in `export_validation.json`. The GLBs reference the canonical TG atlas directly. All 38 indicator frames in the ten animated exports were checked against the atlas pixels and timings. The global validator still reports pre-existing failures in treasure_chest, locker_freezer and mini_fridge outside this batch.
