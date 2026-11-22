"""Publish current vending manifests, preview scene and material/reuse documentation."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'godot/assets/models/station_props';OUT=BASE/'vending';QA=ROOT/'docs/ai/art/quality'
read=lambda p:json.loads(p.read_text())
def write(p,data):p.write_text(json.dumps(data,indent=2)+'\n')
info=read(OUT/'model_info.json');catalog=read(OUT/'source_catalog.json');slots=read(OUT/'material_slots.json');reuse=read(OUT/'mesh_reuse.json');validation=read(OUT/'export_validation.json')
bodies={key:rows for key,rows in reuse.items() if any('_Cabinet' in r['part'] for r in rows)}
lines=['# Vending machines','',f"67 appearances, 21 construction families, {len(bodies)} distinct cabinet meshes. All 67 appear in the editable [catalog](../vending_catalog.blend) and [preview](../vending_machines_preview.blend). Individual GLB and BLEND files are listed below.",'',
'See [classification](CLASSIFICATION.md) for compatible texture families, [mesh reuse](mesh_reuse.json) for actual linked mesh data and [material slots](material_slots.json) for every part and pivot.',
'','## Source and reconstruction','',
'Original `vending.png` and adjacent JSON are unchanged: 800 x 800 atlas, 32 x 32 tiles, 287 named states. `source_catalog.json` records all 67 appearance states resolved from tgstation vending DM inheritance, source rectangles and light masks. Off, broken, deny and animation frames remain in the source atlas; no animations are created.',
'',
'The upper sprite band is reconstructed as the physical roof. The frontal artwork starts below that band; the right edge strip is excluded from the front projection. Pixel-aligned recesses have real depth and lined walls. Side and rear textures are separate 64 x 64 derived PNGs using source colours, perimeter seams, access panels, bolts and ventilation. They are an interpretation of surfaces not shown in the source sprite. Original front artwork is not repeated on the rear.',
'',
'Scale is 0.06 m per front pixel: common cabinets are 1.20 m wide, 1.56 m tall and 0.66 m deep; wider source silhouettes remain wider. Booze-O-Mat is 1.80 m wide and 0.84 m deep. Wallmed has a shallow 0.30 m body and a 0.90 m mounting offset. Per-model dimensions are in `model_info.json`.',
'','## Materials and moving parts','',
'| Part | Slot | Material / texture |','|---|---:|---|',
'| Cabinet | 0 | `Vending_Front_*`: original atlas, tile transform, opaque |',
'| Cabinet | 1 | `Vending_Service_*`: derived side, rear, recess and underside texture |',
'| Display insert, delivery flap, raised sign | 0 | Original atlas patch |',
'| Display glass | 0 | Slight cyan tint, alpha 0.15, closed 0.012 m thickness |',
'| Light overlay sections | 0 | Separate source-derived 32 x 32 mask, emission 0.4, GLB alpha MASK |',
'',
'All textured materials use nearest filtering. Surfaces are flat shaded. Display, glass and delivery flaps are separate closed meshes; flap pivots lie on their upper edge. Light sections follow the actual display and delivery depths. Geometry and UV data are shared across compatible appearances in the catalog; object material overrides select the appearance. Standalone GLBs necessarily contain their own geometry.',
'','## Validation','',
f"{sum(v['triangles'] for v in validation.values()):,} triangles across 67 machines; range {min(v['triangles'] for v in validation.values())}–{max(v['triangles'] for v in validation.values())} per machine. {sum(v['opaque_uv_samples'] for v in validation.values()):,} opaque UV samples: no transparent pixel hits, collapsed UVs, degenerate triangles or non-manifold edges. Embedded image bytes and nearest samplers were checked independently in every GLB.",
'',
'All appearances have front, front/right, rear and left inspection renders under `docs/ai/art/quality/vending/`. Roofs are visible in the angled renders. Blender source and exported GLBs are checked; Godot runtime/import has not been verified. No collisions, animations or gameplay integration were added.',
'','## Files','',
'| Source state | Family | GLB | BLEND | Triangles |','|---|---|---|---|---:|']
for state,r in sorted(info.items()):lines.append(f"| {state} | {r['family']} | [{r['key']}.glb]({r['key']}.glb) | [BLEND]({r['key']}.blend) | {r['triangles']} |")
(OUT/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
# Refresh summaries so existing paths never point at old geometry statistics.
models=read(BASE/'model_info.json');health=read(BASE/'material_slots_and_mesh_health.json');checks=read(BASE/'export_validation.json')
for state,r in info.items():
    models[r['key']]={'file':'vending/'+r['key']+'.glb','atlas':'vending','state':state,'category':'vending','family':r['family'],'description':'Full-depth source-faithful cabinet with original front/roof and derived service textures','meshes':len(r['parts']),'triangles':r['triangles'],'dimensions_m':r['dimensions_m'],'material_details':'vending/material_slots.json','validation':'vending/export_validation.json'}
    health[r['key']]=slots[state];checks[r['key']]=validation[state]
write(BASE/'model_info.json',models);write(BASE/'material_slots_and_mesh_health.json',health);write(BASE/'export_validation.json',checks)
write(BASE/'vending_light_layers.json',{'source':'vending/source_catalog.json','variants':[{'name':state,'masks':sorted({t['light_mask'] for t in catalog['vendors'][state]['types'] if t.get('light_mask')}),'parts':[p['name'] for p in slots[state] if 'Light_Overlay' in p['name']]} for state in sorted(info)]})
# Godot preview only; gameplay registry remains unchanged.
scene=[f'[gd_scene load_steps={len(info)+2} format=3]','']
for i,(state,r) in enumerate(sorted(info.items()),1):scene.append(f'[ext_resource type="PackedScene" path="../vending/{r["key"]}.glb" id="{i}"]')
scene+=['','[sub_resource type="Environment" id="Environment_1"]','background_mode = 1','background_color = Color(0.08, 0.09, 0.12, 1)','ambient_light_source = 3','ambient_light_color = Color(0.8, 0.85, 1, 1)','ambient_light_energy = 0.7','','[node name="VendingPreview" type="Node3D"]','','[node name="WorldEnvironment" type="WorldEnvironment" parent="."]','environment = SubResource("Environment_1")','','[node name="Sun" type="DirectionalLight3D" parent="."]','rotation_degrees = Vector3(-45, -30, 0)','light_energy = 1.5','','[node name="Camera" type="Camera3D" parent="."]','position = Vector3(24, 24, 31)','rotation_degrees = Vector3(-35, 35, 0)','projection = 1','size = 29.0','current = true']
for i,(state,r) in enumerate(sorted(info.items())):scene+=['',f'[node name="{r["key"]}" parent="." instance=ExtResource("{i+1}")]',f'position = Vector3({i%9*2.3:.2f}, 0, {-(i//9)*2.5:.2f})']
scene=[line.replace('Vector3(-35, 35, 0)','Vector3(-30, 21, 0)') for line in scene]
(BASE/'previews/vending_preview.tscn').write_text('\n'.join(scene)+'\n')
index=read(QA/'index.json');index=[r for r in index if not any(n.startswith('vending_') for n in r['models'])]
names=sorted(info)
for i in range(9):index.append({'file':str(QA/'vending'/f'vending_views_{i+1:02}.png'),'models':[info[s]['key'] for s in names[i*8:(i+1)*8]],'views':['front','front_right','rear','left']})
write(QA/'index.json',index)
(QA/'README.md').write_text('# Current model inspection sheets\n\nAll 67 current vending appearances have four views each. Other retained sheets cover the previously completed assets.\n\n'+'\n'.join(f"- [{Path(r['file']).name}]({Path(r['file']).relative_to(QA).as_posix()}): {', '.join(r['models'])}" for r in index)+'\n')
readme=BASE/'README.md';text=readme.read_text();text=text.replace('current transferred rack.','rebuilt connected rack with its own validation and preview.').replace('- [Vending catalog](vending_catalog.blend) and [vending preview](vending_machines_preview.blend).','- [Vending catalog](vending_catalog.blend) and [vending preview](vending_machines_preview.blend): all 67 appearances. [Classification and files](vending/README.md).')
text=text.replace('their paths were updated without changing model bytes.','their paths were updated during cleanup; the rack was subsequently rebuilt. Stationary tank geometry remains unchanged.')
text=text.replace('cover the earlier generated assets and the updated machinery batch, but omit superseded checks for the transferred rack and stationary tanks.','include the current vending and machinery batches. Rack checks are in its own folder; stationary tanks retain their original geometry.')
for state,r in sorted(info.items()):
    if f"| {r['key']} |" not in text:text+=f"| {r['key']} | [GLB](vending/{r['key']}.glb) | [BLEND](vending/{r['key']}.blend) |\n"
readme.write_text(text)
print('Documented',len(info),'variants;',len(bodies),'cabinet meshes;',len(reuse),'total mesh data blocks')
