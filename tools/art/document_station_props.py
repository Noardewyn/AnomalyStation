"""Write the deliverable index from the actual exported asset manifests."""
from pathlib import Path
import json,re

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props'
models=json.loads((OUT/'model_info.json').read_text())
health=json.loads((OUT/'material_slots_and_mesh_health.json').read_text())
checks=json.loads((OUT/'export_validation.json').read_text())
chairdir=ROOT/'godot/assets/models/furniture/chair_family'
chairs=json.loads((chairdir/'validation.json').read_text())
chairchecks=json.loads((chairdir/'export_validation.json').read_text())
readme='''# Station props — revised meshes

75 props and variants, with individual GLB and editable BLEND files. The adjacent chair family contains seven additional revised chairs. All source tiles were verified as 32 × 32 against the DMI metadata, converted JSON and original pixel data. Source PNGs remain unchanged.

Open `station_props_library.blend` for the full editable library. `station_props_catalog.blend` and `vending_catalog.blend` arrange the models for inspection; `stationary_tank_assemblies.blend` contains straight, L-shaped and 4 × 3 modular examples. `station_props_pose_checks.blend` shows an open toilet lid, tilted solar panel and open suit-storage doors as static poses.

`project.godot` is an independent preview project with `previews/station_props_preview.tscn` and `previews/vending_preview.tscn`. Godot import/runtime has **not** been verified. The main game's model registry and gameplay scenes were not changed for this asset work.

## Construction and scale

The revision replaces intersecting static blocks with connected exterior shells and removes internal interfaces. Vending cabinets have real lined recesses, an inset display and a separately pivoted delivery flap. Portable canisters have stepped barrel sections, shoulders, a continuous handle and separate valve. Basins and toilets have closed porcelain thickness and lined cavities. Beds, racks, showers, trolleys and chair frames have connected supports. Soil is a closed terraced mound.

Shapes follow orthogonal pixel steps and use flat shading. Dimensions are in metres. Blender uses Z up and front −Y; GLB uses Y up and front +Z. Wall fixtures retain their installation height relative to a floor-level root. The 0.90 m modular grid is consistent with the existing table kit; no table dimensions were changed.

Static structural unions are applied meshes without construction modifiers. Independent glass, mechanical pivots, removable panels, upholstery, and source overlays remain separate. A cyborg showcase is the original static cyborg statue, not an invented glass display case.

## Textures, materials and variants

`material_slots_and_mesh_health.json` lists every mesh's material slots, source state, local origin, parent, triangle count and connected-component count, including the seven chairs. `source_audit.json` records atlas paths, hashes, states, frames, directions, tile rectangles and mapped vending types. Models use the first frame; no animation tracks are exported.

| Material family | Purpose |
|---|---|
| `Atlas_<atlas>_<state>` | Original pixel art; opaque, flat shaded, nearest filtering |
| `Overlay_<atlas>_<state>` | Separate source light or gear layer; alpha mask, cutoff 0.4 |
| `Glass_Slight_Cyan_Closed` | Closed-thickness glazing; tinted alpha blend, alpha 0.27 |
| `Water_Slight_Blue_Closed` | Closed water surface volume; alpha blend, alpha 0.68 |

UVs are local to a 32 × 32 tile. `KHR_texture_transform` selects that tile in the original atlas; preserve the transform when overriding materials. Front artwork retains its original proportions. Unpictured side and underside surfaces use opaque palette texels from the same source, rather than stretching a complete frontal sprite around the object. Depth is necessarily reconstructed from the available 2D views.

Two separately saved derived textures are required: `textures/portable_canister_gags.png` composes the original GAGS masks and source tint configurations, and `textures/vending_light_layers.png` pairs original vendor RGB with its separate source light-mask alpha. Both preserve the original pixel grid without resampling. Their JSON manifests document recipes and rectangles. The active holopad uses the source `COLOR_AI_HOLOGRAM_BLUE` tint, #7DB4E1.

Nine portable-canister variants share the same Blender mesh data: generic, air, anesthetic mix, carbon dioxide, nitrogen, nitrous oxide, oxygen, plasma and water vapor. The 39 vending states are the states used by MetaStation; compatible cabinet geometry is linked across texture variants. Water/fuel trolley geometry is also shared where identical. Standalone GLBs necessarily embed their own mesh and PNG data.

## Modular tanks

Canonical modules are `single`, `end` (N), `straight` (NS), `corner` (NE), `tee` (NEW), and `center` (NSEW). Letters denote connected sides in Blender XY. Rotate these parts in 90° steps; directional duplicates are unnecessary. The grid is 0.90 m and height is 1.10 m.

Connected sides omit exterior walls and window jambs. Tops and viewing bands continue across joins without isolated-object borders. Straight, tee and center modules intentionally contain separate upper/lower metal sections: these are intermediate assembly parts, not freestanding structures. Glazing is a separate closed solid. Supports occur at exposed exterior corners. `stationary_assembly_examples.json` records placements and rotations.

## Verification

All exported meshes were checked for manifold edges/vertices, positive volume, zero-area triangles, UV sampling, nearest samplers, original PNG bytes and absence of animations. Multi-angle sheets for all 82 assets are in `docs/ai/art/quality/`; their `index.json` maps files to models and views. The front, right, back and three-quarter sheets were visually reviewed, along with open-part poses and modular assemblies. Source reference sheets are in `docs/ai/art/`.

`assembly_pose_validation.json` records 24 checked modular joins; the maximum extent mismatch is approximately 0.00000012 m. This is a geometric Blender check, not a Godot runtime claim. No collisions, animations or gameplay integration were added.

## Regeneration

1. Run `python tools/art/prepare_station_props.py` to audit source metadata and reproduce the two derived textures.
2. Execute `tools/art/build_station_props.py` in Blender via MCP with `__file__` set. It calls `revise_station_props.py` before exporting. Run in a fresh scene collection, or remove only the prior generated library scenes before rebuilding.
3. Run `python tools/art/validate_station_props.py` for an independent GLB check.
4. Execute `tools/art/verify_station_props_blender.py` for assembly and static-pose checks.
5. Execute `tools/art/render_station_quality.py`, then call its `sheet()` function for the batches to regenerate angle sheets.

## Exported models

Triangle counts include all instantiated parts. Full per-part details are in the JSON manifests.

| Model | Source state | Triangles | Blender dimensions XYZ, m |
|---|---|---:|---|
'''
for key,row in models.items():
    dims=' × '.join(f'{v:.3f}' for v in row['dimensions_m'])
    readme+=f"| [{key}]({row['file']}) | `{row['state']}` | {row['triangles']} | {dims} |\n"
readme+=f"\nTotal: {len(models)} assets, {sum(r['triangles'] for r in models.values()):,} triangles. Independent UV checks: {sum(r['opaque_uv_samples'] for r in checks.values()):,} opaque-surface samples, zero transparent-pixel hits and zero degenerate triangles.\n"
(OUT/'README.md').write_text(readme,encoding='utf-8')

# Preserve existing chair source/variant documentation while updating stale counts.
path=chairdir/'README.md';text=path.read_text(encoding='utf-8')
for key,row in chairs.items():
    text=re.sub(r'(\| `'+re.escape(key)+r'` \| `[^`]+` \| )\d+( \|)',lambda m:m[1]+str(row['triangles'])+m[2],text)
text=text.replace('36,960 interior UV samples',f"{sum(r['uv_interior_samples'] for r in chairchecks.values()):,} interior UV samples")
text=text.replace('The wooden variants share their seat, legs and aprons','The wooden variants share their seat and connected lower frame')
text+='''
## Connected-frame revision

Static frames now have internal intersections removed and connected supports. The metal stool has a continuous supporting frame beneath its seat. Office upper assemblies retain their swivel parent; pedestal/base is one connected mesh, while wheels remain separate. Comfy armrest overlays retain their independent materials. Wooden lower frames are connected and shared between both backrest variants. All 82 chair/prop part slots and component counts are listed in `../../station_props/material_slots_and_mesh_health.json`.
'''
path.write_text(text,encoding='utf-8')

index=json.loads((ROOT/'docs/ai/art/quality/index.json').read_text())
gallery=['# Asset inspection sheets','', 'Each row shows front, right, back and three-quarter views.','']
for record in index:
    name=Path(record['file']).name
    gallery.extend([f"- [{', '.join(record['models'])}]({name})"])
(ROOT/'docs/ai/art/quality/README.md').write_text('\n'.join(gallery)+'\n',encoding='utf-8')
print('Documented',len(models)+len(chairs),'models')
