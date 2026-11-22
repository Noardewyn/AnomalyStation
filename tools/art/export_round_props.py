"""Validate and export the current volume rebuild; preserve stationary assets."""
import bpy,bmesh,json,shutil,hashlib
from mathutils import Vector
d=bpy.app.driver_namespace['round_rebuild'];ns=d['ns'];OUT=d['OUT'];ROOT=d['ROOT']
protected={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for folder in OUT.glob('stationary_tanks*') if folder.is_dir() for p in folder.rglob('*') if p.is_file()}
disk_report=json.loads((OUT/'model_info.json').read_text())
report={}
for key in d['KEYS']:
    a=ns['ASSETS'][key];bpy.context.window.scene=a['scene'];bpy.context.view_layer.update()
    parts=[]
    for ob in a['parts']:
        bm=bmesh.new();bm.from_mesh(ob.data)
        assert all(e.is_manifold for e in bm.edges),(key,'open edges')
        assert all(v.is_manifold for v in bm.verts),(key,'nonmanifold vertices')
        assert bm.calc_volume(signed=True)>0,(key,'volume')
        assert all(f.calc_area()>1e-12 for f in bm.faces),(key,'zero faces')
        bm.free();ob.data.calc_loop_triangles()
        assert ob.data.uv_layers.active
        areas=[];uvs=ob.data.uv_layers.active.data
        for t in ob.data.loop_triangles:
            p,q,r=[uvs[i].uv for i in t.loops]
            areas.append(abs((q.x-p.x)*(r.y-p.y)-(r.x-p.x)*(q.y-p.y))/2)
        parts.append({'name':ob['part_name'],'triangles':len(ob.data.loop_triangles),'components':ns['revision']['components'](ob.data),'zero_uv_area_triangles':sum(v<1e-12 for v in areas),'material_slots':[s.material.name for s in ob.material_slots],'pivot_local':list(ob.location)})
        assert parts[-1]['zero_uv_area_triangles']==0,(key,'collapsed UV')
        assert parts[-1]['components']==1,(key,'disconnected structure')
    path,g=ns['export_asset'](key,a)
    bounds=[o.matrix_world@Vector(c) for o in a['parts'] for c in o.bound_box]
    rec=ns['REPORT'][key]
    rec.update(description=a['description'],meshes=len(a['parts']),triangles=sum(p['triangles'] for p in parts),dimensions_m=[round(max(v[i] for v in bounds)-min(v[i] for v in bounds),4) for i in range(3)],bounds_min_m=[round(min(v[i] for v in bounds),4) for i in range(3)],bounds_max_m=[round(max(v[i] for v in bounds),4) for i in range(3)],materials=[m['name'] for m in g['materials']],parts=[p['name'] for p in parts])
    report[key]={'parts':parts,'manifold':True,'positive_volume':True,'triangles':rec['triangles'],'dimensions_m':rec['dimensions_m']}
    disk_report[key]=rec
(OUT/'model_info.json').write_text(json.dumps(disk_report,indent=2)+'\n')
(OUT/'volume_rebuild_validation.json').write_text(json.dumps(report,indent=2)+'\n')
preview=ns['make_preview']('Machinery_And_Tanks_Preview',d['KEYS'],4,(1.5,1.7),render_size=(1800,1500))
bpy.data.libraries.write(str(OUT/'machinery_and_tanks_library.blend'),{preview}|{ns['ASSETS'][k]['scene'] for k in d['KEYS']},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.context.window.scene=preview;bpy.ops.render.render(write_still=True)
for p,digest in protected.items():assert hashlib.sha256(__import__('pathlib').Path(p).read_bytes()).hexdigest()==digest
print({k:(v['triangles'],sum(p['zero_uv_area_triangles'] for p in v['parts'])) for k,v in report.items()})
print('Export complete; stationary tank files unchanged.')
