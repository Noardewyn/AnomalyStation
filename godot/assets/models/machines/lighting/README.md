# Modular station lighting

Three fixture families, each with seven static appearance states: `off`, `on`, `empty`, `burned`, `broken`, `nightshift`, `emergency`. Each GLB has a matching editable BLEND.

## Source behavior and assembly

Reviewed tgstation `code/modules/power/lighting/light.dm`, `light_items.dm`, `light_mapping_helpers.dm`, and `light_construct.dm`.

- Tube fixture accepts `/obj/item/light/tube`; bulb and floor fixtures accept `/obj/item/light/bulb`. The floor fixture is walkable and mounted to the floor, not a freestanding lantern (`light.dm:759`).
- Replacement type is checked before insertion; removal creates an item and changes the fixture to EMPTY (`light.dm:385`, `light.dm:657`). Broken and burned inserts remain present until removed/replaced. Their meshes are therefore separate from the housing.
- The empty socket is visibly recessed. Tube end sockets connect through a rear mounting rail; bulb housings have a lined socket; floor housings have a lined recess and internal bulb socket.
- Floor assemblies include a removable protective lens. Its separate construction and the hidden socket depth are 3D interpretations of the walkable floor sprite, not additional tg gameplay steps.
- `Lamp_Insert` is the mating transform. Removable insert origins match this transform. `replacement_tube[...].glb` and `replacement_bulb[...].glb` are exported at that origin and share the assembled geometry. Suffixes `burned` and `broken` provide damaged items.
- Hide/remove `Emission_Overlay` with the lamp. The floor lens is another removable part. No animation or interaction code is added.

## States and materials

`LIGHT_OK`, `LIGHT_EMPTY`, `LIGHT_BURNED`, and `LIGHT_BROKEN` select the base sprite. Light overlays are added only when on and intact. Flicker takes priority over emergency, then nightshift, then normal (`light.dm:173-207`). This set exports static appearance variants only; flicker frames remain available in the original atlas, with no animation clips.

Cold, dim, red, warm and no-nightlight subtypes reuse the corresponding fixture geometry. They mainly change light colour, brightness and nightshift permissions; they do not require directional mesh duplicates. Rotate a reusable wall fixture for wall direction. Preview lights are studio lighting only; GLBs contain no engine light nodes.

| Mesh / material slot 0 | Material | Atlas |
|---|---|---|
| Housing, sockets | Light_Housing_Original | lighting.png |
| Intact insert, floor lens | Light_Frosted_Glass_Original | lighting.png, alpha 0.88, closed volume |
| Burned / broken insert | Light_Damaged_Glass_Original | lighting.png, opaque |
| Emission_Overlay | Light_Overlay_Original | lighting_overlay.png, alpha mask, emission 1.5 |

All PNGs are original, unchanged, and referenced from `assets/tg/icons/obj`. GLBs embed their exact bytes. UVs use original sprite regions on all sides; hidden surfaces reuse opaque source patches. Separate emissive overlays retain normal, nightshift and emergency artwork. No derived textures are used. Flat shading and nearest samplers are preserved.

## Scale and orientation

Scale: 0.90 m / 32 source pixels. Tube housing width 0.675 m; small bulb socket width 0.16875 m; floor housing diameter across its widest steps 0.3375 m. Floor recess depth is 0.253125 m.

Wall roots are on their rear mounting plane, emitting/front face toward Blender -Y (Godot +Z). Floor root is the centre of the floor opening at floor level; its body lies below zero. Fixture direction in tg is the wall/mount direction, so check this convention when later integrating. The contact sheet intentionally shows the normally concealed floor casing. `model_info.json` records exact insert transforms in Blender and Godot coordinates.

## Validation and files

- 21 fixture exports plus 6 replacement-item exports, each with GLB and BLEND.
- `source_audit.json`: atlas metadata, 32x32 frames, source hashes and pixel equality with tgstation DMI.
- `export_validation.json`: 27 exports, 27,060 opaque UV samples, no transparent-source hits or degenerate triangles; nearest samplers and original embedded bytes verified.
- Every housing, socket, glass volume and broken shard mesh is closed/manifold. Glowing overlays are intentionally open surfaces. Meshes are shared where geometry, UVs and material match.
- `model_info.json`: part names, triangle counts, material slots, source states and insertion pivots.
- Preview PNG: `docs/ai/art/lighting_assembly_preview.png`.

Godot runtime/import has not been tested. No collisions, gameplay integration or animations were added. Build with `tools/art/build_lighting_assets.py`, then export with `tools/art/export_lighting_assets.py` through Blender MCP.
