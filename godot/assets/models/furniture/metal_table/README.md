# Metal table

Standalone square table reconstructed from the first 32×32 tile of `assets/tg/icons/obj/smooth_structures/table.png`. Original atlas is unchanged. The 64×64 derived texture uses its palette, rim and leg strips with nearest filtering.

- Dimensions: 0.90 × 0.90 × 0.75 m; underside at 0.705 m.
- Scaled to the metal chair's 0.45 m seat height (0.255 m clearance to tabletop underside).
- 60 triangles, 40 modeling vertices, one mesh and one opaque material.
- Four square legs and a thin rectangular top; edge details are textured.
- Floor-level origin; Blender Z-up, exported glTF Y-up.
- Closed mesh components. No collision or animation included.

Texture is embedded; minification and magnification use nearest filtering. GLB structure, triangle count and filtering have been checked; game integration is not included.

Build: `tools/art/build_metal_table.py`. It creates the asset plus a separate live comparison scene with a copy of the chair. Preview: `tools/art/preview_metal_table.py`, output `docs/ai/art/metal_table_preview.png`.


Updated thickness: tabletop 0.09 m (underside Z=0.66 m), legs 0.10 x 0.10 m. Height 0.75 m and pitch 0.90 m unchanged.
