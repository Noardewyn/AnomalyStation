# Wall mounted igniter

Source /obj/machinery/sparker in code/game/machinery/igniter.dm. Explicit wall
mounting and wallframe, pixel_shift 26. Two MetaStation definition records.
Distinct from floor igniters. No matching model existed.

Seven static presets: ready, unpowered, disabled, spark_0 through spark_3.
Disabled takes precedence over power; screwdriver disconnects/reconnects it.
ignite() flicks migniter-spark, whose four frames have delays [1,1,1,1].
Separate game spark-spread particles are not recreated. No animations, collisions
or gameplay integration. The unused migniter-b art is preserved in metadata;
update_icon_state does not explicitly select it, so no broken preset is asserted.

Original wallmounts.png: 288 x 288, tiles 32 x 32. Pixels match DMI and GLBs
embed unchanged original PNG bytes. BLEND references original atlas. Nearest
filtering, flat shading, no derived textures.

Physical silhouettes use source alpha above 0.9, treating near-opaque edge
pixels as solid to avoid tiny artificial holes. Lower-alpha source pixels form
separate Effect_Overlay sheets using original alpha. Spark colors inside the
body remain on the source state face texture; external translucent glow is
separate. Opaque source colors are not repainted. No extra glass.

Closed stepped housing and Service_Cover with local-X hinge at its upper rear
attachment. Disabled cover uses the source open silhouette as a separate pose,
not an animated rigid transform. Original side-depth information is limited:
three-pixel depth and rear/side casing UVs are 3D interpretations. Matching
body/cover shapes share meshes; nonfront surfaces always use unpowered casing
pixels, so flashes do not repeat on the back. Front material slot 0:
Mounted_Igniter_<preset>_Original. Nonfront slot 1:
Mounted_Igniter_Casing_Original. Effect sheet slot 0:
Mounted_Igniter_<preset>_Effect_Overlay. Variants use KHR_texture_transform.

Scale 0.90 / 32 m per pixel; physical depth 0.084375 m. Wall at Blender Y=0,
front -Y / Godot +Z. Rotate one model for other walls.
All seven pass opaque UV, original PNG, nearest and degeneracy checks. Physical
meshes are manifold, effect sheets intentionally open. Original near-opaque
alpha samples are recorded in export_validation.json; body material is opaque.
Front, rear, side and above reviewed. Godot import/runtime not tested.
Unrelated legacy models still fail global validation; this family passes.

Built through Blender Lab MCP using tools/art/build_mounted_igniter.py and
export_mounted_igniter.py. See source_audit.json, model_info.json,
mesh_integrity.json and export_validation.json.
