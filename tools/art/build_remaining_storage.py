"""Remaining closet-atlas objects, separate moving panels and unchanged atlas UVs."""
from pathlib import Path
from math import radians
import bpy,bmesh,json,struct,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/metal_locker'
EXTRA=OUT
SOURCE=ROOT/'godot/assets/tg/icons/obj/storage/closet.png'
STATES={s['name']:s for s in json.loads(SOURCE.with_suffix('.json').read_text())['states']}
atlas=bpy.data.images.load(str(SOURCE),check_existing=True);atlas.pack();W,H=atlas.size
def uv(state,rect,u,v,frame=0):
    ox,oy,_,_=STATES[state]['rects'][frame];x,y,w,h=rect
    return ((ox+x+.002+u*(w-.004))/W,1-(oy+y+.002+v*(h-.004))/H)
materials={}
def material(state,base=None,frame=0,alpha=False):
    base=base or state;key=(state,base,frame,alpha)
    if key in materials:return materials[key]
    m=bpy.data.materials.new('Storage_'+state+'_'+str(frame));m.use_nodes=True;m.use_backface_culling=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.85
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.interpolation='Closest'
    coord=m.node_tree.nodes.new('ShaderNodeTexCoord');mapping=m.node_tree.nodes.new('ShaderNodeMapping')
    a=STATES[state]['rects'][frame];b=STATES[base]['rects'][0]
    mapping.inputs['Location'].default_value=((a[0]-b[0])/W,-(a[1]-b[1])/H,0)
    m.node_tree.links.new(coord.outputs['UV'],mapping.inputs['Vector']);m.node_tree.links.new(mapping.outputs['Vector'],tex.inputs['Vector']);m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    if alpha:
        m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha'])
        if hasattr(m,'surface_render_method'):m.surface_render_method=next(i.identifier for i in m.bl_rna.properties['surface_render_method'].enum_items if i.identifier=='DITHERED')
    materials[key]=m;return m
class Mesh:
    def __init__(self):self.v=[];self.f=[];self.uv=[];self.slots=[]
    def face(self,ids,state,rect,slot=0,coords=None):
        self.f.append(ids);coords=coords or [(0,1),(1,1),(1,0),(0,0)]
        self.uv.append([uv(state,rect,u,v) for u,v in coords]);self.slots.append(slot)
    def box(self,x0,x1,y0,y1,z0,z1,state,rect,front=None,back=None,slot=0):
        n=len(self.v);self.v.extend([(x,y,z) for z in (z0,z1) for x,y in [(x0,y0),(x1,y0),(x1,y1),(x0,y1)]])
        for ids,r,s in [([3,2,1,0],rect,slot),([4,5,6,7],rect,slot),([0,1,5,4],front[1] if front else rect,front[2] if front else slot),([1,2,6,5],rect,slot),([2,3,7,6],back[1] if back else rect,back[2] if back else slot),([3,0,4,7],rect,slot)]:
            st=front[0] if front and ids==[0,1,5,4] else back[0] if back and ids==[2,3,7,6] else state
            self.face([n+i for i in ids],st,r,s)
    def object(self,name,scene,mats,pivot=(0,0,0)):
        mesh=bpy.data.meshes.new(name+'_Mesh');mesh.from_pydata([tuple(Vector(v)-Vector(pivot)) for v in self.v],[],self.f)
        for mat in mats:mesh.materials.append(mat)
        layer=mesh.uv_layers.new(name='Original_Atlas_UV')
        for p,coords,slot in zip(mesh.polygons,self.uv,self.slots):
            p.material_index=slot
            for i,c in zip(p.loop_indices,coords):layer.data[i].uv=c
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(mesh);bm.free()
        mesh.update();mesh.calc_loop_triangles()
        ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.location=pivot;return ob
def export(scene,objects,name):
    bpy.context.window.scene=scene
    for ob in scene.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    path=EXTRA/(name+'.glb');bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n])
    for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
    for node in g.get('nodes',[]):node['name']=node.get('name','Node').split('.')[0]
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);tail=blob[20+n:]
    path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4E4F534A)+raw+tail)
    assert len(g['meshes'])==len(objects)
    binary=tail[8:]
    for image in g['images']:
        view=g['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
        assert binary[start:start+view['byteLength']]==SOURCE.read_bytes()
    return {'file':path.name,'triangles':sum(len(o.data.loop_triangles) for o in objects),'parts':[o.name.split('.')[0] for o in objects]}

# Shape settings: width, depth, height, door opening (bottom,top), body/front texture regions.
specs={
 'wood_cabinet':('cabinet','cabinet_door',1.2,.65,2.0,.20,1.58,(6,20,19,23),2),
 'gun_cabinet':('shotguncase','shotguncase_door',1.2,.55,2.0,.17,1.58,(7,20,17,24),2),
 'abductor_locker':('abductor','abductor_door',1.0,.65,1.8,.10,1.15,(9,28,14,18),2),
 'mini_fridge':('mini_fridge','mini_fridge_door',.65,.55,.80,.04,.72,(9,25,13,14),1),
 'magic_locker':('cursed','cursed_door',.90,.60,1.8,.04,1.49,(9,23,14,24),1),
 'old_locker':('oldcloset','oldcloset',.95,.60,1.8,.05,1.65,(8,20,18,27),1),
}
models={};stats={}
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
for name,(body,door,w,d,h,lo,hi,doorrect,count) in specs.items():
    scene=bpy.data.scenes.new('Remaining_'+name);bpy.context.window.scene=scene
    back=body+'_back' if body+'_back' in STATES else body
    mats=[material(body),material(door,alpha=name=='gun_cabinet'),material(back)]
    m=Mesh();a=w/2;f=-d/2;b=d/2;t=.045
    # Plain exterior/inner colors sample the source body; front rim and pediment retain artwork.
    solid={'wood_cabinet':(6,32,1,1),'gun_cabinet':(6,32,1,1),'abductor_locker':(8,34,1,1),'mini_fridge':(10,30,1,1),'magic_locker':(10,32,1,1),'old_locker':(10,24,1,1)}[name]
    inner={'wood_cabinet':(10,29,1,1),'gun_cabinet':(10,28,1,1),'abductor_locker':(15,32,1,1),'mini_fridge':(15,29,1,1),'magic_locker':(14,30,1,1),'old_locker':solid}[name]
    rounded=name in ('abductor_locker','mini_fridge')
    wall_height=hi if rounded else h
    m.box(-a,a,b-t,b,0,wall_height,body,solid,front=(body,inner,0))
    m.box(-a,-a+t,f,b,0,wall_height,body,solid);m.box(a-t,a,f,b,0,wall_height,body,solid)
    m.box(-a+t,a-t,f,b-t,0,lo,body,solid)
    header={'wood_cabinet':(5,12,21,8),'gun_cabinet':(5,12,21,8),'abductor_locker':(9,18,14,10),'mini_fridge':(10,22,11,3),'magic_locker':(9,17,14,6),'old_locker':(9,17,16,3)}[name]
    if rounded:
        c=.16 if name=='abductor_locker' else .06
        shape=[(-a,hi),(a,hi),(a,h-c),(a-c,h),(-a+c,h),(-a,h-c)]
        k=len(m.v);m.v.extend((x,y,z) for y in (f,b) for x,z in shape)
        m.face([k+i for i in range(6)],body,header,coords=[((x+a)/w,(h-z)/(h-hi)) for x,z in shape])
        m.face([k+6+i for i in reversed(range(6))],body,solid,coords=[(0,0)]*6)
        for i in range(6):
            j=(i+1)%6;m.face([k+i,k+6+i,k+6+j,k+j],body,solid)
    else:m.box(-a+t,a-t,f,b-t,hi,h,body,solid,front=(body,header,0))
    if name=='wood_cabinet':
        m.box(-.025,.025,f+.03,b-t,lo,hi,body,solid)
        for z in [.50,.86,1.23]:m.box(.025,a-t,f+.04,b-t,z-.03,z,body,(17,31,6,1))
        m.box(-a+t,-.025,f+.04,b-t,1.38,1.41,body,solid)
    elif name=='gun_cabinet':
        m.box(-a+t,a-t,f+.04,b-t,1.20,1.23,body,(8,25,15,1))
        for x in [-.36,-.12,.12,.36]:m.box(x-.018,x+.018,b-.17,b-.10,.22,.95,body,(11,34,1,6))
        m.box(-a+t,a-t,b-.22,b-.08,.27,.30,body,solid)
    elif name=='mini_fridge':
        m.box(-a+t,a-t,f+.04,b-t,.35,.37,body,(11,33,9,1))
        # Small wire supports in the source miniature compartment.
        for x in [-.18,0,.18]:m.box(x-.01,x+.01,b-.10,b-.07,.40,.63,body,(13,28,1,4))
    elif name=='abductor_locker':
        for z in [.40,.75]:m.box(-a+t,a-t,f+.08,b-t,z-.025,z,body,(12,33,8,1))
    elif name=='magic_locker':m.box(-a+t,a-t,f+.04,b-t,.16,.19,body,(11,43,10,2))
    parts=[m.object(name+'_Body',scene,mats)]
    for side in range(count):
        x0=-a if side==0 else 0;x1=0 if count==2 and side==0 else a
        hinge=(x0 if side==0 else x1,f-.015,0)
        rect=(doorrect[0]+side*doorrect[2]/count,doorrect[1],doorrect[2]/count,doorrect[3])
        rear=(door,rect,1) if name=='gun_cabinet' else (back,rect,2) if back!=body else (body,solid,0)
        panel=Mesh();panel.box(x0+.004,x1-.004,f-.03,f,lo,hi,door,(rect[0],rect[1]+2,1,1),front=(door,rect,1),back=rear,slot=1)
        ob=panel.object(name+('_Door_Left' if side==0 else '_Door_Right'),scene,mats,hinge)
        ob['open_axis']='Z';ob['open_degrees']=-115 if side==0 else 115;parts.append(ob)
    root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
    for ob in parts:ob.parent=root
    stats[name]=export(scene,parts,name);models[name]=(parts,mats,scene)

# Cardboard and metal boxes are hollow, with top panels pivoted on their own edges.
for name,body,flaps in [('cardboard_box','cardboard',4),('metal_box','metalbox',2)]:
    scene=bpy.data.scenes.new('Remaining_'+name);bpy.context.window.scene=scene
    mats=[material(body)];m=Mesh();a=.50;b=.42;h=.82;t=.025;plain=(8,32,1,1)
    m.box(-a,a,-b,b,0,t,body,plain)
    m.box(-a,a,-b,-b+t,t,h,body,plain,front=(body,(2,25,28,22),0))
    m.box(-a,a,b-t,b,t,h,body,plain,back=(body,(2,25,28,22),0))
    m.box(-a,-a+t,-b+t,b-t,t,h,body,plain,front=(body,(2,25,28,22),0))
    m.box(a-t,a,-b+t,b-t,t,h,body,plain)
    parts=[m.object(name+'_Body',scene,mats)]
    for i in range(flaps):
        panel=Mesh()
        if i<2:
            y0,y1=(-b,0) if i==0 else (0,b);pivot=(0,-b if i==0 else b,h)
            panel.box(-a,a,y0,y1,h,h+.015,body,(3,16,26,8));axis='X';angle=110 if i==0 else -110
        else:
            x0,x1=(-a,0) if i==2 else (0,a);pivot=(-a if i==2 else a,0,h)
            panel.box(x0,x1,-b,b,h-.02,h-.005,body,(3,16,26,8));axis='Y';angle=-110 if i==2 else 110
        ob=panel.object(name+'_Flap_'+str(i+1),scene,mats,pivot);ob['open_axis']=axis;ob['open_degrees']=angle;parts.append(ob)
    root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
    for ob in parts:ob.parent=root
    stats[name]=export(scene,parts,name);models[name]=(parts,mats,scene)

library=bpy.data.scenes.new('Remaining_Storage_Library');records=[]
def add_variant(id,model,states,frame=0,notes=''):
    parts,base_mats,_=models[model]
    if model=='magic_locker':
        ms=[material(states[0],'cursed',frame),material(states[1],'cursed_door',frame),material(states[2],'cursed_back')]
    elif model=='cardboard_box':ms=[material(states[0],'cardboard')]
    else:ms=base_mats
    i=len(records);position=Vector(((i%5)*3.4,(i//5)*3.0,0))
    for pose,dx in [('Closed',0),('Open',1.6)]:
        for source in parts:
            ob=source.copy();ob.parent=None;ob.name=id+'_'+pose+'_'+source.name;library.collection.objects.link(ob)
            ob.location=source.location+position+Vector((dx,0,0))
            for slot,mat in zip(ob.material_slots,ms):slot.link='OBJECT';slot.material=mat
            if pose=='Open' and 'open_axis' in source:ob.rotation_euler['XYZ'.index(source['open_axis'])]=radians(source['open_degrees'])
    records.append({'id':id,'model':model,'file':stats[model]['file'],'states':states,'frame':frame,'notes':notes,
                    'rects':{s:STATES[s]['rects'][frame if STATES[s]['frames']>1 else 0] for s in states}})
for name in specs:
    if name=='magic_locker':continue
    b,d,*_=specs[name];add_variant(name,name,[b,d],notes='Hidden reverse uses a solid pixel from the original body; no reverse sprite exists.')
add_variant('fullcabinet','wood_cabinet',['cabinet','cabinet_door'],notes='fullcabinet is the precomposed closed rendering of the same two-door cabinet; shared geometry/artwork.')
for state in ['cursed','decursed']:
    for frame in range(7):add_variant(state+'_'+str(frame+1),'magic_locker',[state,state+'_door',state+'_back'],frame,notes='Original animated texture frame; same shared geometry for all seven frames.')
for state in ['cardboard','agentbox','cardboard_roroco']:add_variant(state,'cardboard_box',[state])
add_variant('metalbox','metal_box',['metalbox'])
for variant in records:
    base_states=(['cursed','cursed_door','cursed_back'] if variant['model']=='magic_locker'
                 else ['cardboard'] if variant['model']=='cardboard_box' else variant['states'])
    offsets={}
    for state,base in zip(variant['states'],base_states):
        current=variant['rects'][state];origin=STATES[base]['rects'][0]
        offsets[state]=[(current[0]-origin[0])/W,(current[1]-origin[1])/H,0]
    variant['godot_material_uv_offsets']=offsets
    variant['source_frame_delays']=STATES[variant['states'][0]]['delays']
bpy.context.window.scene=library
bpy.data.libraries.write(str(EXTRA/'remaining_storage_library.blend'),{library,*[v[2] for v in models.values()]},path_remap='RELATIVE',fake_user=True,compress=True)
manifest={'atlas':'res://assets/tg/icons/obj/storage/closet.png','source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
          'models':stats,'variants':records,'overlay_only':['cardboard_special','shotgun','egun','wt550','cursed_whole'],
          'notes':['cardboard_special is an alert overlay, not an extra box.',
                   'Weapon sprites are contents, not part of the gun cabinet.',
                   'Old closet has no open-body sprite; its plain hollow interior is a reconstruction.',
                   'Abductor and wooden door reverse surfaces have no separate _back sprite; opaque body samples are used.',
                   'Gun-cabinet screen retains the original alpha=192 texels, rather than opaque black.']}
(EXTRA/'remaining_variants.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'models':stats,'appearances':len(records),'mesh_objects':len(library.objects)}))
