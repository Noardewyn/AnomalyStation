# Metal chair

Low-poly reconstruction of `chair` from `assets/tg/icons/obj/chairs.png`, frames 4–7 (zero-based). Original source atlas is unchanged. The derived 64×64 atlas reuses its pixel colors and patterns, with edge padding and nearest filtering.

- 104 triangles, 64 modeling vertices, one mesh, one opaque material.
- Dimensions: 0.544 × 0.519 × 0.970 m; seat height 0.45 m.
- Four legs, solid seat and backrest, flat shading. Ribs are textured rather than modeled.
- Origin at floor level. Blender: Z up, front −Y. GLB uses glTF Y-up conversion.
- Closed mesh components; no collision or animation included.

Use `metal_chair.glb` in Godot. Its texture is embedded and sampler uses nearest minification/magnification. `metal_chair.blend` is the editable source with packed texture. Export validation confirms one mesh/material and 104 triangles; game integration is not included.

Generator: `tools/art/build_metal_chair.py` (run in Blender; creates a new asset scene). Preview: `tools/art/preview_metal_chair.py`.
