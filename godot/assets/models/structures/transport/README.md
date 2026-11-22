# Railings, conveyors and plastic flaps

19 GLB exports with matching editable Blender files. PNG previews are in `docs/ai/art/`.

## Original textures

All material images reference the original files under `godot/assets/tg/icons/obj/`: `railings.png`, `machines/recycling.png`, and `structures.png`. No derived texture files. GLB necessarily embeds the same original PNG bytes. Nearest filtering, flat normals, UVs on every face, closed solid meshes. `source_audit.json` verifies pixel equality against the tgstation DMI files. `source_states.json` retains state, direction, rectangle and frame metadata.

## Railings

- `railing_straight`: 0.90 m repeat length, 0.75 m height, 0.05625 m depth; continuous rails and three posts follow the 32x48 sprite's visible silhouette.
- `railing_corner`: solid union of two perpendicular arms; use for the diagonal `railing` directions. Includes the corner post, no overlapping primitives inside the union.
- `railing_corner_connector`: short corner/post accessory for `railing_corner`.
- `railing_end_cap`: terminal accessory for `railing_end`; rotate 180 degrees for `railing_end_flip`.

Blender coordinates: Z up, a straight rail runs along X with endpoints (-0.45,0) and (0.45,0). Corner endpoints are (-0.45,0) and (0.45,0.90); turn centre is (0.45,0). Rotate around Z and translate by 0.90 m pitches. Godot converts Blender (x,y,z) to (x,z,-y). Each asset origin is on the floor. Place border rails on a tile edge, not its centre. The corner may extend half a rail thickness beyond its centreline. Do not overlay the connector on the complete L module; use it only when assembling separate edge pieces.

The 0.75 m rail height is a 3D interpretation consistent with the existing 0.75 m furniture scale; the vertically compressed oblique sprite does not define a metric height. Geometry corners and joins remain square and stepped. The square preview is 2.70 m per side with four corner pieces and four straight intermediates.

## Conveyors

Two mesh families, `straight` and `corner`, each exported in `stopped`, `forward`, `reverse` and their `flipped` texture variants (12 files). No directional duplicates: rotate a reusable module. The canonical corner uses BYOND SE (direction index 4), with south and west open ports. Its opposite sides carry raised guide rails. The straight uses BYOND S (index 0).

Tile pitch is 0.90 m. Belt surface is 0.05625 m above floor; rail top is 0.084375 m. The one-pixel transparent outside margin belongs to the source silhouette. Parts: `Chassis_and_Guide_Rails` and `Replaceable_Belt`. Chassis geometry and materials are shared across operational variants; every belt face's UVs lie in the original first frame of the requested state and direction, which makes the belt a live frame (`docs/ai/specs/models.md`): the client plays the state's four frames on it. The sequences are documented in `source_states.json`.

Source `code/modules/recycling/conveyor.dm`: `update_move_direction` distinguishes corner ports, inversion and flipping; `update_icon_state` selects `conveyor{0,1,-1}[-flipped]`. These are complete sprite states, not extra overlays. There is no transverse casing border at a connected port. The original belt seams and arrows remain on the belt.

## Plastic flaps

`plastic_flaps_standard`, `plastic_flaps_opaque`, `plastic_flaps_kitchen` share a 0.90 x 1.80 m frame and four separate closed strips, each 0.008 m thick. Height is a doorway-scale interpretation for the project's 2 m rooms. The original front is mapped to both sides, with original trim pixels on thin edges. Each strip origin is at its top hinge; rotate about local X in Blender. The preview's tilted strips are a static demonstration only; exported assets are closed, with no animation.

Source `code/game/objects/structures/plasticflaps.dm`: all variants render the same `plasticflaps` vis-overlay. Base alpha is 255; `opaque` changes sight blocking, not the sprite. Kitchen sets alpha to 150 and does not require crawling. Only kitchen therefore uses a transparent material (150/255, GLB BLEND). Standard and opaque reuse the same mesh/material data. The original's top/bottom and side trim form `Mounting_Frame`; `Hinged_Strip_1` through `4` are independently removable/rotatable overlay geometry. No duplicate hidden full-sprite plane is exported.

## Material slots and checks

Each part has one descriptive material slot: `Railing_Original_Atlas`, `Conveyor_Chassis_Original_Atlas`, `Conveyor_Belt_Original_Atlas`, `Flaps_Frame_Original_Atlas`, `Flaps_Original_Atlas`, or `Flaps_Kitchen_Original_Atlas` (Blender may append numeric suffixes when rerunning the builder). `model_info.json` records exact names, shared meshes, triangle counts and pivots.

All 19 new exports pass independent original-PNG hash, nearest sampler, UV and triangle validation in `export_validation.json`. Construction asserts manifold meshes before and after simplification; normals are recalculated outward. Front, rear, top, side and oblique views were inspected, together with straight/rectangular railing and L conveyor assemblies. No collisions, gameplay integration or animations. Existing unrelated storage UV defects remain outside this batch.

Rebuild using Blender MCP: execute `tools/art/build_transport_assets.py`, then `tools/art/export_transport_assets.py` in the same live Blender session.
