# Security camera — original atlas model

Ten GLB files with matching editable Blender files: ordinary and X-ray cameras, each in `on`, `in_use`, `off`, `emp`, and `panel_open` states. its PNG is `docs/ai/art/security_camera_preview.png`.

## Source and state composition

Source image: `godot/assets/tg/icons/obj/machines/camera.png`, original 672x672 atlas, 32x32 tiles. The adjacent JSON orders rectangles by direction, then frame: `rects[direction * frames + frame]`. Source directions are S, N, E, W, SE, SW, NE, NW. All seven scan frames and four EMP frames remain documented in `source_states.json`; the model has no animation.

Checked against `tgstation/code/game/machinery/camera/camera.dm`, especially `update_icon_state` and `update_overlays`, and `camera_construction.dm`:

- `camera` / `xraycamera`: enabled base body. These are not merely unpowered sprites.
- `_on`: independent green emission overlay, plus the X-ray blue indicator where applicable.
- `_in_use`: independent red emission overlay when the in-use lights are active.
- `_off`: disabled or EMP base; the exported head rests at one end of its scan arc, without an enabled indicator.
- `_emp`: separate discharge overlay on the disabled camera; first original frame, static and removable.
- `camera_panel`: separate open-panel overlay, also used by the X-ray type. In 3D it follows the hinged service door.

Enabled variants use the central scan frame (3) to define the neutral pose and overlay placement. Off pose rotation is a 3D interpretation of the source end pose. Body faces reuse opaque gray housing patches; their upper, side, rear and underside UVs remain in the original atlas. State overlays are not baked into the reusable body. The disabled lens uses the original dark red lens pixel. The upgrade's extra blue pixel sits on an additional one-pixel-wide X-ray module dock, rather than floating outside the housing.

## Parts, scale and pivots

Grid-derived unit: 0.90/32 = 0.028125 m per pixel. Ordinary camera dimensions are approximately 0.197 m wide, 0.338 m deep, 0.197 m high. Front head is five pixels wide and six deep, with one-pixel square steps. Housing is a closed solid with a genuinely recessed, lined lens opening. Glass is a separate closed volume, alpha 0.88.

- `Wall_Plate_and_Stepped_Arm`: one continuous mesh, including the lined service pocket.
- `Head_Pan_Pivot`: empty at Blender (0, -0.196875, 0.028125), local Z rotation. Head, glass, module and enabled overlays follow it.
- `Stepped_Camera_Head`: closed head shell; flat shading, no round bevels.
- `Recessed_Lens_Glass`: finite thickness inside the aperture.
- `Service_Panel`: local Z hinge at Blender (0.0421875, -0.084375, 0); shown rotated -100 degrees in panel-open variants.
- `Status_Emission_Overlay`, `Service_Panel_State_Overlay`, `EMP_Discharge_Overlay`: independent source layers.
- `Xray_Module_Dock`: only in upgraded variants, shared between their states.

Origin is the centre of the wall mounting plate. The rear mounting plane is Blender Y=0; the camera projects toward negative Y, with Z up. Godot converts (x,y,z) to (x,z,-y). Rotate the whole asset for other wall directions; no directional duplicate models. Mounting height belongs to the scene placement, not this asset.

## Materials and validation

Single material slot per part: `Camera_Housing_Original`, `Camera_Lens_Glass_Original`, or `Camera_State_Overlay_Original`. Reruns may add Blender numeric suffixes; `model_info.json` records actual exported slots and part hierarchy. Nearest filtering, no smooth normals. No generated/derived textures: Blender refers to the original PNG, GLB embeds its exact bytes.

`source_audit.json` verifies pixel equality with the original DMI. `export_validation.json` records independent PNG hash, nearest sampler, UV and degenerate-triangle checks. `mesh_integrity.json` checks closed manifold geometry, with intentional open sheets only for original visual-effect overlays. Front, side, rear, top and oblique views were inspected. No animations, collisions or gameplay integration were added.

Rebuild through Blender MCP with `tools/art/build_security_camera.py`, then `tools/art/export_security_camera.py`. The builder reuses only the boundary-mesh function from `build_transport_assets.py`; it does not rebuild transport assets.
