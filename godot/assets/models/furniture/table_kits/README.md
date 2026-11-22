# Modular tables: all smooth_structures types

24 source atlases are reduced to 7 geometry families. Shared dimensions: cell 0.90×0.90 m, height 0.75 m, tabletop thickness 0.09 m, leg section 0.10×0.10 m. That is twice the former 0.045 m and 0.05 m. The height and leg positions are kept. Reinforced supports also have 0.13 m wide feet.

| Family / GLB prefix | Texture variants | Construction |
|---|---|---|
| metal, existing folder `../metal_table/modular` | table, table_greyscale | Rectangular metal tabletop |
| wood | wood_table, poker_table | Bevelled outer corners, wooden supports |
| cloth | fancy_table, black, blue, cyan, green, orange, purple, red, royalblack, royalblue | Hanging tablecloth edge on open sides only |
| glass | glass_table, plasmaglass_table | Solid glass with a narrow opaque edge |
| framed_glass | rglass_table, rplasmaglass_table, titaniumglass_table, plastitaniumglass_table, alien_table | Glass, wide frame and reinforced supports |
| reinforced | reinforced_table, brass_table | Solid tabletop, lower rails and reinforced supports |
| rolling | rollingtable | Standalone table with four low-poly wheels |

alien_table has a shape similar to the reinforced glass tables, so it uses that geometry as an artistic reconstruction. This is not a claim that their game properties are identical. In the game rollingtable has `smoothing_flags = NONE`, so it has only the single module.

## Files and assembly

All new GLBs are directly in this folder, with no separate files per colour. The prefix names the family; the rest of the name matches the metal set:

| Suffix | Legs | Connections at zero rotation in Godot |
|---|---:|---|
| table_single_4legs | 4 | None |
| table_end_2legs | 2 | East +X |
| table_top_straight | 0 | East + west |
| table_corner_1leg | 1 | North −Z + east |
| table_corner_2legs | 2 | North −Z + east |
| table_top_tee | 0 | North + east + west |
| table_top_center | 0 | All four sides |

Modules rotate by 90°; there are no directional duplicates. The legs are part of the mesh. The origin is the cell centre on the floor. Tabletops without legs can be added between supported ends. The one-leg corner suits the outer corners of a large surface, the two-leg one a turn with an extra inner support. The glass is closed, alpha 0.42. The new parts keep closed end faces, which are hidden at the joints of an assembled table.

- `table_families_kit.blend`: 36 new base parts.
- `modules.json`: triangle counts, connections and legs.
- `assembly_examples.json`: ready coordinates and rotations of parts for assembly.
- `variants.json`: which family each variant belongs to, its source PNG and checksum.

## Textures

The UVs point straight into tg's original sheets under `assets/tg/icons/obj/smooth_structures` and pick its smoothing states directly (the original-atlas pass, `docs/ai/art/original_atlas_report.md`). Each GLB embeds its family's base tg PNG byte for byte. The game draws every other variant of a family on the same meshes with that variant's own sheet: its entry in `godot/assets/models/models.json` names the `.dmi` as `image`. Nearest filtering.

Glass parts have separate `..._Frame` and `..._Glass` surfaces; the central glass filler may have only the glass surface. Transparency is a parameter of the glass material only, not of the frame or legs. `table_greyscale` uses the modular metal kit (`../metal_table/modular`) with the greyscale sheet.

An earlier version drew the joints with adapted per-variant textures and `.tres` materials; the original-atlas pass replaced them, and they were removed on 2026-10-18.

## Checks

Checked: closed components of the new meshes, one exported mesh per part, GLB triangle counts, nearest filters and 15 assemblies without duplicated supports. All textures were viewed in Blender. The new parts have 12–236 triangles; separate texture variants add no geometry. The game joins neighbours with the `smooth4` rule in `models.json`; collisions and wheel physics are not modelled.

Source of behaviour: `tgstation/code/game/objects/structures/tables_racks.dm`; alien: `code/modules/antagonists/abductor/abductor_structures.dm`. Generators: `tools/art/prepare_table_variants.py`, `build_table_families.py`, `table_family_examples.py`.

