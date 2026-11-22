# Urinal

Rebuilt existing urinal.glb and urinal.blend in place; no duplicate runtime model. Original watercloset.png, tile urinal at 0,0, 32x32, four directions, one frame. PNG RGBA equals TG watercloset.dmi. Uses south artwork; rotate the reusable model for wall direction.

Continuous closed stepped ceramic shell with deep lined bowl; closed back, floor and rim. Separate flush pipe, valve head and drain protector. Source screwdriver interaction removes the drain protector; its pivot is at the cap centre, removal local Z. Hidden item is not a visible source overlay. Source has no separate open state. All material slots use Urinal_Porcelain_Original_Atlas, nearest filtering, flat shading. Front/cavity use original sprite regions; sides and rear reuse original ceramic shading strips. No derived textures.

Scale .90/32 m per pixel, bottom .40 m, top 1.30 m, maximum depth .253125 m. Blender front -Y, wall plane Y=0. 214 triangles. Physical meshes manifold, no degenerate triangles or transparent-pixel hits in opaque UV samples. Exact original PNG bytes embedded, nearest sampler verified. Front, rear, side preview inspected. GLB plus editable BLEND, library and preview scene supplied.

Built with installed Blender 5.2 in background because MCP localhost:9876 was unavailable. No authored animations or collisions.

Runtime integration: `models.json` binds watercloset.dmi's `urinal` state with the `wall` rule. The model retains its authored metre scale and 0.4..1.3 m floor height; its back sits 5 mm off the wall. The old `dir4` binding was rejected for wall decals and left the sprite visible. All four mapped directions are covered by placement tests, and ModelShowcase includes the mounted model for Godot render checks. The source has one visual state; the drain protector and hidden item do not have appearance overlays.
