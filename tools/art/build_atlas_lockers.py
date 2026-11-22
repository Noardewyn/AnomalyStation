"""Four shared locker geometries with UVs directly on the unmodified TG atlas.

Source appearance rules: closets.dm closet_update_overlays / animate_door.
"""
from pathlib import Path
from math import radians
import bpy, bmesh, json, struct, hashlib, re
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/metal_locker'
OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'godot/assets/tg/icons/obj/storage/closet.png'
META=json.loads(SOURCE.with_suffix('.json').read_text())
STATES={s['name']:s for s in META['states']}
W,H=META['sheetWidth'],META['sheetHeight']
source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
atlas=bpy.data.images.load(str(SOURCE),check_existing=True)
atlas.pack()

# Resolve literal type defaults, including inherited icon_door separately from body.
dmroot=ROOT.parent/'tgstation'
folder=dmroot/'code/game/objects/structures/crates_lockers'
types={}
for f in [folder/'closets.dm',*(folder/'closets').rglob('*.dm')]:
    current=None
    for lineno,line in enumerate(f.read_text().splitlines(),1):
        if line.startswith('/'):
            current=line.split('//')[0].strip()
            if not current.startswith('/obj/structure/closet') or '(' in current:current=None
            elif current not in types:types[current]={'source':str(f.relative_to(dmroot)),'line':lineno}
        elif current:
            m=re.match(r'^\t(?:var/)?(icon_state|icon_door|icon|material_drop|parent_type|enable_door_overlay)\s*=\s*(.*)',line)
            if m:types[current][m[1]]=m[2].split('//')[0].strip().strip('"\'')
def resolve(t):
    if t not in types:return resolve(t.rsplit('/',1)[0]) if '/' in t else {}
    r=types[t];parent=r.get('parent_type',t.rsplit('/',1)[0])
    return dict(resolve(parent),**r)
resolved={t:resolve(t) for t in types}
body_names=['generic','armory','atmos','bio','bomb','cap','ce','chem_secure','cmo',
            'emergency','eng','eng_secure','fire','freezer','hop','hos','hydro','med',
            'med_secure','paramed_secure','qm','rack','rd','science','sec','secure',
            'syndicate','syndicate_chemical','tac','warden','alien','mining','shelves','cargo']
def family(body):
    if body=='freezer':return 'freezer'
    if body in ('med','chem_secure','shelves'):return 'shelves'
    if body in ('rack','tac'):return 'rack'
    return 'wardrobe'
pairs={}
excluded=[]
for t,r in resolved.items():
    b=r.get('icon_state','generic');d=r.get('icon_door')
    d=b if not d or d=='null' else d
    if r.get('icon')=='icons/obj/storage/closet.dmi' and b in body_names and d+'_door' in STATES:
        pairs.setdefault((b,d),[]).append(t)
    else:excluded.append({'type':t,'body':b,'reason':'Outside full-height single metal locker atlas family'})
# Also expose valid atlas-only appearances; these are explicitly distinguished from DM types.
for b in body_names:
    d=b if b+'_door' in STATES else 'generic'
    if b+'_back' in STATES:pairs.setdefault((b,d),[])
pairs.setdefault(('generic','mining_wardrobe'),[])
BASE={'wardrobe':('generic','generic'),'shelves':('med','med'),'rack':('rack','red'),'freezer':('freezer','freezer')}
def back_state(body):
    # This source snapshot has no syndicate_chemical_back, despite animate_door requesting it.
    # The explicitly recorded fallback uses the matching syndicate family, never a blank recolor.
    return body+'_back' if body+'_back' in STATES else 'syndicate_back'
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
materials={}
def material(state,base,role):
    key=(state,base,role)
    if key in materials:return materials[key]
    mat=bpy.data.materials.new('Atlas_'+role+'_'+state);mat.use_nodes=True;mat.use_backface_culling=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value=.85
    shader.inputs['Metallic'].default_value=.1
    shader.inputs['Specular IOR Level'].default_value=.15
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    tex.interpolation=next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier=='Closest')
    coord=mat.node_tree.nodes.new('ShaderNodeTexCoord')
    mapping=mat.node_tree.nodes.new('ShaderNodeMapping')
    a=STATES[state]['rects'][0];b=STATES[base]['rects'][0]
    mapping.inputs['Location'].default_value=((a[0]-b[0])/W,-(a[1]-b[1])/H,0)
    mat.node_tree.links.new(coord.outputs['UV'],mapping.inputs['Vector'])
    mat.node_tree.links.new(mapping.outputs['Vector'],tex.inputs['Vector'])
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    mat['atlas_state']=state;materials[key]=mat;return mat
def uv(state,x,y):
    ox,oy,_,_=STATES[state]['rects'][0]
    return ((ox+x)/W,1-(oy+y)/H)
def patch(state,rect,coords):
    x,y,w,h=rect
    return [uv(state,x+.002+u*(w-.004),y+.002+v*(h-.004)) for u,v in coords]
RECT=[(0,1),(1,1),(1,0),(0,0)]
MODELS={};stats={}
for fam,(body_state,door_state) in BASE.items():
    scene=bpy.data.scenes.new('Atlas_Locker_'+fam);bpy.context.window.scene=scene
    mats=[material(body_state,body_state,'body'),material(door_state+'_door',door_state+'_door','front'),material(body_state+'_back',body_state+'_back','inside')]
    if fam=='freezer':
        outer=[(-.33,0),(.33,0),(.45,.13),(.45,1.60),(.39,1.67),(.27,1.74),(.21,1.8),(-.21,1.8),(-.27,1.74),(-.39,1.67),(-.45,1.60),(-.45,.13)]
    else:
        outer=[(-.39,0),(.39,0),(.45,.0643),(.45,1.7357),(.39,1.8),(-.39,1.8),(-.45,1.7357),(-.45,.0643)]
    inner=[(x*.88,.055+z*(1.69/1.8)) for x,z in outer]
    n=len(outer)
    V=[(x,y,z) for shape,y in [(outer,-.262),(outer,.3),(inner,-.262),(inner,.255)] for x,z in shape]
    F=[];U=[];M=[]
    def face(ids,rect,state=body_state,slot=0,coords=RECT):
        F.append(ids);U.append(patch(state,rect,coords));M.append(slot)
    # Rear exterior uses a solid patch of the CORPUS, never the door's _back state.
    face(list(reversed(range(n,2*n))),(10,14,11,3),coords=[((.45-outer[i][0])/.9,1-outer[i][1]/1.8) for i in reversed(range(n))])
    # Inner back stays dark; separately mapped compartment panels below preserve hooks.
    face(list(range(3*n,4*n)),(12,20,1,1),coords=[((x+.45)/.9,1-z/1.8) for x,z in inner])
    for i in range(n):
        j=(i+1)%n
        # Roof carries the original top stripe, side color is sampled from the body rim.
        top=abs(outer[i][1]-1.8)<.001 and abs(outer[j][1]-1.8)<.001
        bevel=outer[i][1]>1.6 and outer[j][1]>1.6
        roof=(12,14,7,4) if fam=='freezer' else (8,12,15,7)
        face([i,n+i,n+j,j],roof if top else (9,19,1,1) if bevel else (8,22,2,22))
        face([i,j,2*n+j,2*n+i],(9,22,1,1))
        face([2*n+i,2*n+j,3*n+j,3*n+i],(10,32,1,1))
    def box(x0,x1,y0,y1,z0,z1,top_rect,edge_rect=None):
        k=len(V);V.extend([(x,y,z) for z in (z0,z1) for x,y in [(x0,y0),(x1,y0),(x1,y1),(x0,y1)]])
        face([k+3,k+2,k+1,k],(12,20,1,1));face([k+4,k+5,k+6,k+7],top_rect)
        for i in range(4):
            j=(i+1)%4;face([k+i,k+j,k+4+j,k+4+i],edge_rect or top_rect)
    def panel(z0,z1,y0,y1):
        k=len(V);V.extend([(-.385,.253,z0),(.385,.253,z0),(.385,.253,z1),(-.385,.253,z1)])
        face([k,k+1,k+2,k+3],(10,y0,11,y1-y0))
    shelves=[]
    if fam in ('wardrobe','rack'):
        shelves=[1.29]
        box(-.396,.396,-.225,.255,1.265,1.29,(11,24,9,2),(11,26,9,1))
        box(-.396,.396,-.225,.255,.055,.08,(11,39,9,5),(10,44,11,1))
        panel(1.30,1.72,20,24)
        if fam=='wardrobe':panel(.085,1.26,27,39)
        else:
            panel(.085,1.26,27,44)
            # Three raised rack rails match the three vertical supports in rack/tac.
            for x in (-.22,0,.22):box(x-.018,x+.018,.17,.215,.12,.94,(12,32,1,9))
            box(-.32,.32,.14,.22,.20,.23,(11,41,9,1))
    elif fam=='shelves':
        shelves=[.66,1.27]
        for z,row in [(1.27,25),(.66,34)]:box(-.396,.396,-.225,.255,z-.025,z,(11,row,9,2),(11,row+2,9,1))
        box(-.396,.396,-.225,.255,.055,.08,(11,43,9,2),(10,45,11,1))
        panel(1.28,1.72,20,25);panel(.67,1.24,28,34);panel(.085,.63,37,43)
    else:
        shelves=[.66,1.20]
        for z,row in [(1.2,25),(.66,33)]:
            # Slatted freezer trays, with real gaps between the five rails.
            for x in (-.28,-.14,0,.14,.28):box(x-.018,x+.018,-.22,.25,z-.02,z,(12,row,1,4),(12,row+4,1,1))
            for y in (-.22,.23):box(-.36,.36,y,y+.02,z-.025,z,(11,row+4,9,1))
        box(-.35,.35,-.21,.25,.055,.08,(11,42,9,2),(10,44,11,1))
    def mesh_obj(name,verts,faces,uvs,slots):
        mesh=bpy.data.meshes.new(fam+'_'+name);mesh.from_pydata(verts,[],faces)
        for m in mats:mesh.materials.append(m)
        layer=mesh.uv_layers.new(name='Original_Closet_Atlas_UV')
        for p,coords,slot in zip(mesh.polygons,uvs,slots):
            p.material_index=slot
            for i,v in zip(p.loop_indices,coords):layer.data[i].uv=v
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        mesh.update();mesh.calc_loop_triangles()
        ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);return ob
    body=mesh_obj('Locker_Body',V,F,U,M)
    hinge=Vector((-.45,-.2825,0))
    verts=[tuple(Vector((x,y,z))-hinge) for y in (-.3,-.265) for x,z in outer]
    faces=[list(range(n)),list(reversed(range(n,2*n)))];slots=[1,2]
    # Both sides map physical +X to sprite +X. Back image mirrors naturally when viewed from inside,
    # matching animate_door's negative cosine past 90 degrees (handle remains opposite hinge).
    coords=[patch(door_state+'_door',(8,19,15,28),[((x+.45)/.9,1-z/1.8) for x,z in outer]),
            patch(body_state+'_back',(8,19,15,28),[((outer[i][0]+.45)/.9,1-outer[i][1]/1.8) for i in reversed(range(n))])]
    for i in range(n):
        j=(i+1)%n;faces.append([i,n+i,n+j,j]);slots.append(1)
        coords.append(patch(door_state+'_door',(9,22,1,1),RECT))
    door=mesh_obj('Locker_Door',verts,faces,coords,slots);door.location=hinge
    parent=bpy.data.objects.new('Metal_Locker',None);scene.collection.objects.link(parent)
    body.parent=parent;door.parent=parent
    parent['geometry_family']=fam;door['open_degrees']=-123 if fam=='freezer' else -140
    for o in scene.objects:o.select_set(True)
    bpy.context.view_layer.objects.active=body
    path=OUT/('locker_'+fam+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();length=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+length])
    for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
    for node in g.get('nodes',[]):
        for stable in ('Locker_Body','Locker_Door','Metal_Locker'):
            if node.get('name','').startswith(stable):node['name']=stable
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);tail=blob[20+length:]
    path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4E4F534A)+raw+tail)
    assert len(g['meshes'])==2
    MODELS[fam]=(body,door,scene)
    stats[fam]={'file':path.name,'triangles':sum(len(o.data.loop_triangles) for o in (body,door)),
                'shelves_m':shelves,'base_body':body_state,'base_front':door_state+'_door','base_back':body_state+'_back'}

library=bpy.data.scenes.new('Atlas_Locker_All_Variants');preview=bpy.data.scenes.new('Atlas_Locker_Reference_Preview')
records=[]
def offset(state,base):
    a=STATES[state]['rects'][0];b=STATES[base]['rects'][0]
    # Godot top-down imported UV convention reverses Blender's V translation.
    return [(a[0]-b[0])/W,(a[1]-b[1])/H,0]
def instance(target,body_name,door_name,pos,angle):
    fam=family(body_name);b,d,_=MODELS[fam];base_body,base_door=BASE[fam]
    ms=[material(body_name,base_body,'body'),material(door_name+'_door',base_door+'_door','front'),material(back_state(body_name),base_body+'_back','inside')]
    for original in (b,d):
        ob=original.copy();ob.parent=None;ob.name=body_name+'__'+door_name+'__'+original.name
        ob.location=original.location+Vector(pos);target.collection.objects.link(ob)
        for slot,mat in zip(ob.material_slots,ms):slot.link='OBJECT';slot.material=mat
        if original==d:ob.rotation_euler.z=radians(angle)
for i,((b,d),dmtypes) in enumerate(sorted(pairs.items())):
    fam=family(b);base_body,base_door=BASE[fam]
    assert b in STATES and back_state(b) in STATES and d+'_door' in STATES
    instance(library,b,d,((i%9)*1.6,(i//9)*2.1,0),-140 if fam!='freezer' else -123)
    records.append({'id':b+'__'+d,'family':fam,'model':stats[fam]['file'],'body_state':b,
                    'door_front_state':d+'_door','door_back_state':back_state(b),'requested_door_back_state':b+'_back','back_fallback':back_state(b)!=b+'_back','open_reference_state':b+'_open',
                    'dm_types':dmtypes,'atlas_only':not bool(dmtypes),
                    'godot_uv_offsets':{'body':offset(b,base_body),'front':offset(d+'_door',base_door+'_door'),'inside':offset(back_state(b),base_body+'_back')},
                    'rects':{k:STATES[k]['rects'][0] for k in [b,d+'_door',back_state(b)]}})
for i,(b,d) in enumerate([('generic','blue'),('generic','blue'),('med','med'),('rack','red'),('chem_secure','chem_secure'),('freezer','freezer')]):
    instance(preview,b,d,((i%3)*1.8,(i//3)*2.0,0),0 if i==0 else -140 if b!='freezer' else -123)
bpy.context.window.scene=preview
bpy.data.libraries.write(str(OUT/'metal_locker_atlas_library.blend'),{library,preview,*[v[2] for v in MODELS.values()]},path_remap='RELATIVE',fake_user=True,compress=True)
# Keep the familiar default paths updated to the new direct-atlas wardrobe asset.
(OUT/'metal_locker.glb').write_bytes((OUT/'locker_wardrobe.glb').read_bytes())
bpy.data.libraries.write(str(OUT/'metal_locker.blend'),{MODELS['wardrobe'][2]},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.data.libraries.write(str(OUT/'metal_locker_variants.blend'),{library,preview},path_remap='RELATIVE',fake_user=True,compress=True)
manifest={'atlas':'res://assets/tg/icons/obj/storage/closet.png','atlas_size':[W,H],'source_sha256':source_hash,
          'geometry_families':stats,'variants':records,'variant_count':len(records),
          'rules':{'body':'icon_state','front':'(icon_door or icon_state) + _door','inside':'icon_state + _back','static_open':'icon_state + _open'},
          'notes':['Original atlas is embedded unchanged in each GLB. No recolored or repacked textures.',
                   'UV offsets change materials only; share body and door geometry within each family.',
                   'Runtime DM paint jobs with arbitrary overrides can use the same rect-difference mapping.'],
          'excluded_types':excluded}
(OUT/'variants.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(OUT/'source_type_audit.json').write_text(json.dumps(resolved,indent=2)+'\n',encoding='utf-8')
assert source_hash==hashlib.sha256(SOURCE.read_bytes()).hexdigest()
for scr in bpy.data.screens:
    for area in scr.areas:
        if area.type=='VIEW_3D':
            spc=area.spaces.active;spc.region_3d.view_location=Vector((1.6,.7,.9));spc.region_3d.view_distance=8
            spc.region_3d.view_rotation=Vector((2,-7,4)).to_track_quat('Z','Y');spc.region_3d.view_perspective='ORTHO'
print(json.dumps({'families':stats,'variants':len(records),'original_atlas_unchanged':True}))
