# Chemical tanks — concept family

Created through the connected Blender MCP, 2026-10-23. Shared linked mesh parts,
eight material/configuration variants, and one independent neutral reagent overlay.
`chemical_tanks.blend` contains the review scene; each GLB contains only its game asset
at the origin. The ninth review object demonstrates the overlay on the storage body;
`tank_chem_overlay.glb` contains only the overlay, not a ninth tank.

## Geometry and coordinates

- Portable: 348 authored vertices, 498 triangles.
- Stationary: 364 authored vertices, 522 triangles.
- Reagent overlay: 32 vertices, 60 triangles; storage plus overlay: 396 vertices.
- Counts include all visible model parts. GPU/glTF vertices can be higher because
  flat normals split vertices at hard edges. The requested budget is interpreted
  as mesh points in Blender, not GPU vertex records.
- Height 1.53 m; origin at floor centre. Blender front -Y, glTF/Godot front +Z.
- The registry fits the measured 1.531 m source height to 1.6 m in-game; every variant stays below the 2 m walls.
- Flat materials, no external textures; common geometry is linked in Blender.
- Main and auxiliary vessels, cap, tap, straps, chassis, wheels/feet and connector
  remain separate named parts. The auxiliary vessel is visual detail, not a second
  gameplay reagent holder.

## State contract

| icon_state | Capacity | Reagent | Configuration |
|---|---:|---|---|
| water | 1000 | water | portable, blue |
| fuel | 1000 | fuel | portable, orange/grey |
| water_high | 100000 | water | portable, green/blue |
| fuel_high | 5000 | fuel | portable, orange/red |
| foam | 500 | firefighting_foam | portable, white |
| fuel_stationary | 1000 | fuel | fixed feet + outlet, orange/red |
| water_stationary | 1000 | water | fixed feet + outlet, blue |
| tank_stationary | 1000 | initially empty | fixed feet + outlet, ivory |
| tank_chem_overlay | — | dynamic mixture | overlay only |

Capacities and initial flags are sourced from
`src/Core/Tg/Generated/Types/obj.structure.g.cs` (reagent_dispensers and subtypes).
The ordinary fuel and all stationary tanks inherit the base 1000-unit capacity.
Portable tanks are initially unanchored; all five are openable and climbable.
Fuel accepts a rig. Existing fuel explosion/leak interactions are implemented in
`src/Core/Tg/modules/reagents/reagent_dispenser.cs`; model recolours do not alter them.
Properties are also exported as GLB extras and in `model_info.json`.

The reagent display mounts `tank_chem_overlay.glb` at the same transform
as `tank_stationary.glb`. Tint `CT_Flat_Tint_Reagent` using the replicated colour from
`mix_color_from_reagents(reagents.reagent_list)`; preserve opacity 0.62.
The white export is tintable. Green in the review scene is demonstration only.
Do not scale the shell to represent volume: the source overlay represents colour,
not a liquid level. Empty storage has no overlay.

## Project integration and limits

`models.json` maps the eight body states to their corresponding coloured GLBs.
The overlay is a conditional part of `tank_stationary`, shown only when the source
appearance contains `tank_chem_overlay`. `tintOverlay` reads its own replicated colour;
separate instances keep separate mixtures. Current plumbed types remain `DmInert`:
this rendering integration does not implement plumbing or generate new simulation states.
No gameplay values, explosions, movement rules or source-generated classes changed.

Verified by Blender/GLB checks, model placement tests and the Godot ModelShowcase
(all eight colours, empty/filled storage, independent mixture colours and repeated state changes).
The old standalone `tanks/water_tank` and `tanks/fuel_tank` assets were removed.
Rebuild script: `tools/art/build_chemical_tanks_concept.py` (authoring only; preserve the runtime bindings).

## Frame correction, 2026-10-29
All eight body exports now use one continuous Support_Frame mesh. Exact unions remove intersecting upright/grip/shoe faces; rear uprights extend into the chassis. Frame is closed with outward normals; coplanar same-facing overlap audit returned zero for the reviewed water model. Front and rear reviewed in Blender. Existing vessel shells and painted overlays were not rebuilt in this focused frame repair. Updated counts are in model_info.json (under 500 authored vertices per body). No game integration changed.
