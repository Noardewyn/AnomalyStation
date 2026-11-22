# Wall-mounted first aid station

Reference: tgstation code/game/machinery/wall_healer.dm, cat2_medicine_reagents.dm and other_reagents.dm. No previous wall healer model found. Wall mounted, including locations above tables. No floor model created.

## Presets and layers

wall_healer_level_0 through level_7: all four reservoirs at the same source depletion level (0 full, 7 depleted). wall_healer_mixed: [0,2,4,6]. wall_healer_off: full stores without emission. wall_healer_open: full stores and opened service cover/contacts. These are examples of independently selectable reservoirs, not an exhaustive 8^4 state set. Each reservoir remains a separate mesh and material; states bar0-bar7 share geometry. The source formula is 7 - round(7 * remaining / initial, 1).

Order and original tint: Libital #ECEC8D, Aiuri #8C93FF, Syriniver #8CDF24, Blood #C80000. Source bar overlays shift by 0/4/8/12 pixels. Material colour multipliers reproduce these tints without creating texture copies; GLB factors are written explicitly. Source bar_shadow is a separate alpha overlay. Operational state enables emission on the bar regions identified by bar_emissive; its white mask is not painted over the coloured bars. Non-operational state retains reservoir layers and shadow. Open adds service contacts regardless of stored quantities. No invented healing animation or extra states.

## Geometry and texture slots

Continuous closed shell, four lined recessed display openings, a lined central hand opening, closed back. Four reservoir meshes, four closed glass windows, separate service cover and contacts. Glass thickness 0.003375 m, alpha 0.12. Glass and shadow use blended rendering. Service_Cover_Hinge at Blender (0,-0.0928125,-0.2109375) m, X axis, 105-degree open preset; hinge is the 3D interpretation of the source's small open overlay. Hand_Insertion_Point marks the central mouth. No gameplay integration.

Only res://assets/tg/icons/obj/machines/wall_healer.png (128x96, 32x32 tiles), original PNG bytes and DMI RGBA verified. Nearest textures, flat shading, UVs on roof/sides/back/bottom and internal lining. Material slots: Wall_Healer_Original_Atlas, four named reagent Lit/Unlit materials, Wall_Healer_Glass_Original and Wall_Healer_Shadow_Overlay. Original atlas remains external in blend files and embedded unchanged in GLB. No derived textures. Geometry is shared across presets where UV/material combinations match.

Dimensions: 0.534375 m width, 0.590625 m height, 0.1125 m housing depth, scale 0.90/32 m per pixel. Rear mounting plane at Y=0; front Blender -Y, Godot +Z. Rotate a reusable asset for wall directions.

## Delivery and verification

258 triangles per closed variant; 270 open. Solid meshes including glass checked manifold. Zero transparent UV hits on opaque solids and zero degenerate triangles. Original embedded PNG hashes and nearest samplers pass. Front/back/right/top/three-quarter views reviewed, including glass, opening lining and service access. Batch results in export_validation.json; global validation retains unrelated pre-existing failures. Godot runtime not exercised. No animations, collisions or gameplay integration.
