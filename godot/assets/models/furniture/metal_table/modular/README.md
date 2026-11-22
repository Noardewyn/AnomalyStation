# Modular metal table — compact kit

Seven unique modules, rotated in 90-degree steps. Supports are included in the module mesh; no separate leg asset is required. All origins are at floor level and cell center. Pitch: 0.90 m; tabletop height: 0.75 m. One material, shared 128×128 nearest-filtered atlas, embedded in each GLB.

| File | Purpose | Legs | Triangles | Connections at zero rotation (Godot) |
|---|---|---:|---:|---|
| `table_single_4legs.glb` | Standalone table | 4 | 60 | None |
| `table_end_2legs.glb` | Supported end | 2 | 34 | East (+X) |
| `table_top_straight.glb` | Intermediate straight top | 0 | 8 | East + west |
| `table_corner_1leg.glb` | Outer corner, one outer support | 1 | 20 | North (−Z) + east |
| `table_corner_2legs.glb` | Turn, outer and diagonally opposite inner support | 2 | 32 | North + east |
| `table_top_tee.glb` | Intermediate T junction / edge of a wide top | 0 | 6 | North + east + west |
| `table_top_center.glb` | Interior filler | 0 | 4 | All four |

Connection sides have no dark border or vertical closing face. Exposed sides retain the original sprite rim. Choose the correct shape and rotate the entire module around its origin. Positive 90-degree Godot Y rotation moves the east connection to north. Rotations, filenames and cell coordinates for six validated examples are recorded in `modules.json`.

## Assembly

Three-cell row along +X: `table_end_2legs` at X=0, `table_top_straight` at X=0.9, and `table_end_2legs` rotated 180 degrees at X=1.8. This produces four legs and 76 triangles. Extend the row by inserting additional straight tops.

For a 2×2 square, use four `table_corner_1leg` modules rotated toward the neighboring cells. This produces four outer legs and 80 triangles. For a three-cell L, use one `table_corner_2legs` and two rotated end modules: six legs and 100 triangles. The second corner leg is inside the corner module, near the reentrant corner. Larger spans can use the supported variants where extra supports are desired.

`modular_table_kit.blend` contains the seven parts laid out for inspection; individual GLBs have zero translation. Old directional GLB duplicates and the standalone leg GLB have been removed.

The 0.90 m art pitch retains the approved furniture proportions. It is not the project's 2 m world grid; adapting to world cells requires explicit footprint and support-position changes while retaining height. Runtime selection and collisions are not implemented here.

Generator: `tools/art/build_modular_metal_table.py`, with build stage `tools/art/modular_table_compact_tail.py`. Preview: `tools/art/preview_modular_metal_table.py`. Exported mesh counts and triangle counts are checked, example supports have no duplicate positions, and top seams are visually checked from above and in perspective.


Updated thickness: tabletop 0.09 m (underside Z=0.66 m), legs 0.10 x 0.10 m. Height 0.75 m and pitch 0.90 m unchanged.
