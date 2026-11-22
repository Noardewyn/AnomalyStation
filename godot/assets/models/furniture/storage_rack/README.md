# Storage rack

Rebuilt as one connected, closed mesh: a stepped tray, five transverse boards with actual openings, longitudinal rails and two rear lifting loops. All shoulders follow the source pixel grid. No diagonal chamfers or intersecting internal boxes remain.

- `storage_rack.glb`: current export, 920 triangles.
- `storage_rack.blend`: editable source.
- `mesh_validation.json` and `export_validation.json`: dimensions, source metadata and validation.

Source: original `structures.png`, state `rack`, one 32 × 32 tile at (352, 288), one frame and direction. The atlas remains unchanged and is embedded byte-for-byte in GLB. Material slot 0 is `Storage_Rack_Original_Atlas`; every surface has non-collapsed atlas UVs, including interior, rear and underside. Flat shading, nearest sampling, opaque material.

Dimensions are 1.75 × 1.25 × 0.9375 m, Blender Z up, front −Y. The board support height remains 0.6875 m; all four existing item slots in `models.json` were checked against solid board tops. The earlier diagonal protrusions were removed, reducing width from approximately 1.857 m. The root remains at floor level. No gameplay, collision or animation changes.

Verified: manifold edges and vertices, positive volume, 9,200 opaque UV samples, no transparent-pixel hits, no degenerate geometry or UV triangles. Visually inspected from front, right, back, top, underside and three-quarter views. Godot import/runtime was not exercised.

Build via Blender MCP using `tools/art/rebuild_storage_rack.py`, inspect, then call `bpy.app.driver_namespace['storage_rack_rebuild']['export']()`. Audit with `python tools/art/validate_storage_rack.py`.
