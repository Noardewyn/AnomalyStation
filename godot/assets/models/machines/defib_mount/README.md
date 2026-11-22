# Wall defibrillator mount

18 representative presets: standard and charging PENLITE mounts, each
empty, unpowered, ready, locked, charge25/50/75, and emagged frames 0/1.
Ready/locked/emagged use charge100. Other combinations reuse the supplied parts.
Each preset has a GLB and editable BLEND;
shares meshes across scenes.
The mobile floor stand is excluded. No previous matching model was found.

Source: code/game/machinery/defibrillator_mount.dm. Original atlas:
godot/assets/tg/icons/obj/machines/defib_mount.png, 128 x 96, 32 x 32 tiles.
PNG pixels match the original DMI. Exports reference the canonical original PNG directly.
No derived textures. Nearest filtering, flat shading, opaque physical surfaces.

The plate has its original stepped silhouette and mounting details in its
texture. The mounted defibrillator is a separate closed volume, with removal
origin at its lower rear center (0, -0.05625, -0.1265625) m in Blender; removal
is toward -Y, away from the wall. Independent left/right locking clamps have
origins at their rear attachment, and source-described release axis Z.
Clamps are shown only in the locked preset. The four individual emagged presets include a two-phase indicator clip with 0.3 s per frame. The shared library and preview display static poses.
The mount sprite does not change when paddles are taken: no invented paddles
or extra mount states are provided. This is the mounted depiction of the
device, not an exhaustive standalone handheld-defibrillator asset.

The original online/emagged and charge overlays are separate pixel-aligned
surface meshes parented to the device. They use their original opaque pixels.
Source shows safety and charge only when defib.powered. Charge is rounded up
to a multiple of 25; no charge0 state exists. PENLITE adds charging behavior
in the source but no separate charging-light overlay. No gameplay integration.

Material slot 0: Defib_Mount_Original_Atlas for ordinary plate, device, clamps
and indicator layers. PENLITE plate slot 0: PENLITE_Mount_Original_Atlas,
using the same plate mesh and an atlas UV offset (KHR_texture_transform).
The rear mounting panel and its four fasteners use opaque pixels from the standard or PENLITE plate region.
Hidden depth is a 3D interpretation. Scale is 0.90 / 32 m per pixel.
Plate width 0.50625 m, height 0.365625 m, depth 0.05625 m. Device extends
to 0.140625 m from wall; locked clamps extend to 0.16875 m.
Origin is rear-center; front Blender -Y / Godot +Z. Rotate reusable models.

44–160 triangles per preset. Physical parts are closed/manifold; indicator
effect sheets are intentionally open. Initial exports passed original embedded
PNG identity, nearest sampler, opaque UV and degenerate triangle checks.
Preview reviewed from front, above, side and rear. Godot runtime/import not
tested. Global validator has unrelated legacy failures; this family passes.
Reports: source_audit.json, mesh_integrity.json, export_validation.json.

Build/export through Blender Lab MCP: tools/art/build_defib_mount.py and
tools/art/export_defib_mount.py. Preview: docs/ai/art/defib_mount_preview.png.
