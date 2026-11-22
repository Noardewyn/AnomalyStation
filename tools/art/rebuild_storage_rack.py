"""Closed rack exterior on the source pixel grid, with original atlas UVs.

Run in Blender via MCP. Build first; call export() after visual inspection.
"""
from pathlib import Path
import bpy,bmesh,json,math,hashlib,struct,importlib.util
from collections import defaultdict
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/storage_rack'
ATLAS=ROOT/'godot/assets/tg/icons/obj/structures.png'
META=json.loads(ATLAS.with_suffix('.json').read_text())
assert META['width']==META['height']==32
first=0
for state in META['states']:
    if state['name']=='rack':break
    first+=state.get('dirs',1)*max(1,len(state.get('delays') or [1]))
else:raise ValueError('Missing rack state')
TX=(first%(META['sheetWidth']//32))*32;TY=(first//(META['sheetWidth']//32))*32
assert (TX,TY)==(352,288)
SOURCE_HASH=hashlib.sha256(ATLAS.read_bytes()).hexdigest()
spec=importlib.util.spec_from_file_location('rack_mesh_kernel',ROOT/'tools/art/station_prop_geometry.py')
G=importlib.util.module_from_spec(spec);spec.loader.exec_module(G)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.data.scenes.new('Storage_Rack');bpy.context.window.scene=scene
scene.unit_settings.system=G.enum(scene.unit_settings,'system','METRIC')
PX=1/16;cells={}
def fill(x0,x1,y0,y1,z0,z1,kind):
    for x in range(x0,x1):
        for y in range(y0,y1):
            for z in range(z0,z1):cells[x,y,z]=kind

# Closed shallow tray. Rectilinear shoulders replace the previous diagonal cuts.
for z in range(10):
    half=14 if z<5 else 14-(z-3)//2
    for x in range(-half,half):
        for y in range(-10,10):
            if z<2 or x<-half+2 or x>=half-2 or y in [-10,9]:cells[x,y,z]=0
# Five transverse boards and their stepped ends; true gaps between the boards.
fill(-11,11,9,10,10,11,1)
for y0 in [-10,-6,-2,2,6]:
    fill(-11,11,y0,y0+3,10,11,1)
    for z in range(5,10):
        half=14-(z-3)//2
        fill(-half-1,-half+1,y0,y0+3,z,z+1,1)
        fill(half-1,half+1,y0,y0+3,z,z+1,1)
# Two pairs of narrow longitudinal rails; rear lifting loops only.
for x in [-9,-7,6,8]:fill(x,x+1,-10,10,11,12,2)
for x0 in [-9,6]:
    fill(x0,x0+1,9,10,12,14,2)
    fill(x0+2,x0+3,9,10,12,14,2)
    fill(x0,x0+3,9,10,14,15,2)

# Extract only exposed faces, then merge coplanar cells before creating topology.
groups=defaultdict(set)
for p,kind in cells.items():
    for axis in range(3):
        uvaxes=[i for i in range(3) if i!=axis]
        for sign in [-1,1]:
            neighbor=list(p);neighbor[axis]+=sign
            if tuple(neighbor) in cells:continue
            surface=kind
            if kind==0 and axis==2 and sign==1:surface=3
            groups[axis,p[axis]+(sign>0),sign,surface].add((p[uvaxes[0]],p[uvaxes[1]]))
quads=[]
for (axis,level,sign,kind),area in groups.items():
    while area:
        u,v=min(area);w=1
        while (u+w,v) in area:w+=1
        h=1
        while all((u+i,v+h) in area for i in range(w)):h+=1
        area.difference_update((u+i,v+j) for i in range(w) for j in range(h))
        axes=[i for i in range(3) if i!=axis];points=[]
        for a,b in [(u,v),(u+w,v),(u+w,v+h),(u,v+h)]:
            p=[0,0,0];p[axis]=level;p[axes[0]]=a;p[axes[1]]=b;points.append(tuple(p))
        quads.append((points,kind,axes,(u,v,w,h)))
corners={p for points,*_ in quads for p in points}
patches={0:(3,21,29,29),1:(10,11,15,13),2:(7,8,9,10),3:(11,12,15,14)}
mesh=G.Mesh()
for points,kind,axes,(u,v,w,h) in quads:
    split=[]
    for a,b in zip(points,points[1:]+points[:1]):
        axis=next(i for i in range(3) if a[i]!=b[i]);fixed=[i for i in range(3) if i!=axis]
        edge=[p for p in corners if all(p[i]==a[i] for i in fixed) and min(a[axis],b[axis])<=p[axis]<=max(a[axis],b[axis])]
        edge.sort(key=lambda p:(p[axis]-a[axis])/(b[axis]-a[axis]));split.extend(edge[:-1])
    x0,y0,x1,y1=patches[kind];coords=[]
    for p in split:
        s=(p[axes[0]]-u)/w;t=(p[axes[1]]-v)/h
        coords.append(((TX+x0+.5+s*(x1-x0-1))/META['sheetWidth'],1-(TY+y1-.5-t*(y1-y0-1))/META['sheetHeight']))
    mesh.face([tuple(c*PX for c in p) for p in split],coords)
mat=bpy.data.materials.new('Storage_Rack_Original_Atlas');mat.use_nodes=True;mat.use_backface_culling=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.8
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ATLAS),check_existing=True);tex.image.pack();tex.interpolation=G.enum(tex,'interpolation','Closest')
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
obj=mesh.object('Storage_Rack',scene,mat);obj['source_state']='rack';obj['support_height_m']=.6875
bpy.context.view_layer.update()
bm=bmesh.new();bm.from_mesh(obj.data)
assert all(e.is_manifold for e in bm.edges) and all(v.is_manifold for v in bm.verts)
assert bm.calc_volume(signed=True)>0;bm.free()
# Verify existing game support points actually lie on occupied board tops.
slots=[(-.25,.25),(.25,-.25),(-.25,-.3),(.25,.3)]
for x,godot_z in slots:
    cell=(math.floor(x/PX),math.floor(-godot_z/PX),10)
    assert cell in cells,(cell,'unsupported item slot')
obj.data.calc_loop_triangles()
report={'source_atlas':str(ATLAS.relative_to(ROOT)),'source_sha256':SOURCE_HASH,'state':'rack','tile_rect':[TX,TY,32,32],'dirs':state.get('dirs',1),'frames':max(1,len(state.get('delays') or [1])),'dimensions_m':list(obj.dimensions),'triangles':len(obj.data.loop_triangles),'support_height_m':.6875,'support_slots_checked':4,'manifold':True,'material_slots':{'0':'Storage_Rack_Original_Atlas'},'texture_patches_xyxy':patches}

def preview():
    sc=bpy.data.scenes.new('Storage_Rack_Preview');bpy.context.window.scene=sc
    world=bpy.data.worlds.new('Storage_Rack_World');world.use_nodes=True;sc.world=world
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.07,.09,.12,1);bg.inputs['Strength'].default_value=.6
    copy=obj.copy();sc.collection.objects.link(copy)
    for label,loc,power in [('Key',(-3,-4,5),700),('Fill',(3,-1,3),450),('Rim',(1,4,4),500)]:
        light=bpy.data.lights.new('Rack_'+label,'AREA');light.energy=power;light.size=4
        ob=bpy.data.objects.new(light.name,light);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((0,0,.4))-ob.location).to_track_quat('-Z','Y').to_euler()
    camdata=bpy.data.cameras.new('Rack_Camera');camdata.type=G.enum(camdata,'type','ORTHO');camdata.ortho_scale=2.9
    cam=bpy.data.objects.new(camdata.name,camdata);sc.collection.objects.link(cam);cam.location=(3,-4,3);cam.rotation_euler=(Vector((0,0,.4))-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
    sc.render.engine='BLENDER_EEVEE_NEXT';sc.render.resolution_x=1200;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
    sc.view_settings.view_transform=G.enum(sc.view_settings,'view_transform','Standard');sc.render.image_settings.file_format=G.enum(sc.render.image_settings,'file_format','PNG');sc.render.filepath=str(ROOT/'docs/ai/art/storage_rack_preview.png')
    bpy.ops.render.render(write_still=True);return sc

def export():
    bpy.context.window.scene=scene;bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    path=OUT/'storage_rack.glb'
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];data=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for sampler in data.get('samplers',[]):sampler.update(minFilter=9728,magFilter=9728,wrapS=33071,wrapT=33071)
    raw=json.dumps(data,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    bpy.data.libraries.write(str(OUT/'storage_rack.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
    sc=preview();bpy.data.libraries.write(str(OUT/'storage_rack_preview.blend'),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
    (OUT/'mesh_validation.json').write_text(json.dumps(report,indent=2)+'\n')
    assert hashlib.sha256(ATLAS.read_bytes()).hexdigest()==SOURCE_HASH
    print('EXPORTED',report)

bpy.app.driver_namespace['storage_rack_rebuild']=globals()
print('BUILT',report)
