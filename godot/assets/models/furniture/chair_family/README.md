# Chair family

Seven low-poly reconstructions of `icons/obj/chairs.dmi`, with the original 416×384 PNG embedded unchanged in every GLB. Tiles are 32×32, direction order is south, north, east, west; all selected states have four directions and one frame. No derived textures are used.

| Model filename, without extension | Sprite state | Triangles | Dimensions XYZ in Blender, m | Seat height, m |
|---|---|---:|---|---:|
| `metal_stool` | `stool` | 192 | 0.544 × 0.480 × 0.522 | 0.45 |
| `office_chair_dark` | `officechair_dark` | 320 | 0.522 × 0.503 × 0.990 | 0.45 |
| `office_chair_white` | `officechair_white` | 320 | 0.522 × 0.503 × 0.990 | 0.45 |
| `comfy_chair` | `comfychair` | 168 | 0.684 × 0.580 × 1.008 | 0.45 |
| `bar_stool` | `bar` | 196 | 0.468 × 0.430 × 0.836 | 0.72 |
| `wooden_chair` | `wooden_chair` | 372 | 0.540 × 0.450 × 1.020 | 0.45 |
| `wooden_chair_wings` | `wooden_chair_wings` | 484 | 0.756 × 0.450 × 1.020 | 0.45 |

Every model has an individual `.glb` and editable `.blend`. `chair_family_preview.tscn` is a standalone Godot staging scene with the seven GLB instances, a camera, floor, and lighting. It is not connected to the game or model registry.

## Source and construction

`source_audit.json` records source hashes, exact rectangles, DMI/JSON agreement, and tg type references. The original PNG was compared pixel-for-pixel with the original DMI. Reference contact sheet: `docs/ai/art/chair_family_reference.png`; studio render: `docs/ai/art/chair_family_preview.png`.

The tg behavior was inspected in `code/game/objects/structures/beds_chairs/chair.dm`: the office light subtype selects `officechair_white`; wood/wings selects its own back silhouette; comfy colors multiply the grayscale atlas; `update_overlays()` places `comfychair_armrest` above an occupied chair. The 3D armrest parts remain visible as physical geometry, with depth handling the overlap with a seated character. They retain separate meshes and material slots.

Back and seat outlines follow pixel boundaries. Wooden slat gaps are actual through-holes with closed lining, rather than transparent cards. Broad caps use the original sprite pixels; thin edges and secondary surfaces sample opaque texels from the same state. Top-down seat depth is reconstructed from the front sprite's projected cushion region. Depth, underside structure, and caster placement are 3D interpretations of the four views.

Scale follows the existing metal chair's 0.45 m seat and the project's 0.75 m table. The bar seat is higher, at 0.72 m. Origins are at floor level, Blender Z up and front −Y. The GLBs use Y up, with front +Z. Rotate one asset for other directions; no directional mesh copies are supplied.

## Meshes, pivots and material slots

Each mesh has one material in slot 0. All meshes are flat shaded and opaque. Texture samplers use nearest minification and magnification, with no mipmap filter. None of these sprites requires glass or blended transparency.

| Material name | Assigned parts | Variant handling |
|---|---|---|
| `Chair_Original_Atlas` | Stool, bar stool, wood, dark office, white office base and casters | Original atlas UVs |
| `Office_White_Original_Atlas` | White office seat, backrest, support | Same mesh data as dark office; UV translation selects the white tiles |
| `Comfy_Upholstery_Tint` | Comfy body, seat, back, rear support, feet | White base; multiply by a source tint |
| `Comfy_Armrest_Overlay_Tint` | `Armrest_Overlay_Left`, `Armrest_Overlay_Right` | Separate original `comfychair_armrest` side layer; use the same tint as the body |

The office white material uses `KHR_texture_transform` with glTF UV offset `[-288/416, 32/384]`. Preserve that transform when replacing its material. Mesh data is genuinely linked between the two office scenes in the Blender library. The wooden variants share their seat and connected lower frame; only the backrest changes. Standalone GLBs necessarily contain their own copy of their geometry.

Office `Swivel_Pivot` is at `(0, 0, 0.385)` in Blender, parenting the seat, back and support. The pedestal and caster base remain fixed. Four individual `Caster_*` meshes have origins at their axle centers, local X axis. Seat and backrest origins are at their lower support points. Other chairs are rigid structures with separately editable components. No animations or collision geometry are included.

`variants.json` lists the office texture variants and the comfy white, brown, beige, teal, black, and lime tints from tg. Only the white comfy GLB is exported; the five other colors are material overrides on the same geometry. Apply a tint to both comfy material slots. The manifest stores original sRGB bytes; use the appropriate color-space conversion when setting a linear shader factor. No alternate PNGs are required.

## Verification and regeneration

All seven models were visually inspected in Blender from front, back, right, and three-quarter views. Checks cover sprite contours, slat openings, thickness, support contacts, and exposed texture faces. These are individual chairs, so modular surface joints do not apply.

`validation.json` records Blender dimensions, triangle counts, linked geometry and manifold checks. `export_validation.json` independently decodes the GLBs: 41,580 interior UV samples, zero transparent pixel hits, zero degenerate triangles, nearest samplers, no animations, and exact embedded PNG bytes. Each solid mesh component has closed edges and manifold vertices; mechanical parts meet or overlap at their support contacts.

The Godot preview has not been imported or run. The isolated Godot check was blocked by temporary-directory permissions, and the request to run it outside the sandbox was declined. Its runnable verifier is provided below; no Godot runtime success is claimed.

- Build via Blender MCP: execute `tools/art/build_chair_family.py` with `__file__` set to that script's absolute path. It creates new scenes and exports the models and preview scenes. Render the `Chair_Family_Preview` scene for the PNG.
- Audit sources and GLBs: `python tools/art/audit_chair_family.py`.
- Optional Godot import/material/preview check: `python tools/art/verify_chair_family_godot.py`. Uses a temporary project and does not edit the main Godot project.

## Connected-frame revision

Static frames now have internal intersections removed and connected supports. The metal stool has a continuous supporting frame beneath its seat. Office upper assemblies retain their swivel parent; pedestal/base is one connected mesh, while wheels remain separate. Comfy armrest overlays retain their independent materials. Wooden lower frames are connected and shared between both backrest variants. All 82 chair/prop part slots and component counts are listed in `../../station_props/material_slots_and_mesh_health.json`.
