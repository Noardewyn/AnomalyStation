"""Low-poly crates from original atlas. Run in Blender; leaves other scenes intact."""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib,re,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/crates';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'godot/assets/tg/icons/obj/storage/crates.png'
STATES={s['name']:s for s in json.loads(SOURCE.with_suffix('.json').read_text())['states']}
atlas=bpy.data.images.load(str(SOURCE),check_existing=True);atlas.pack();W,H=atlas.size
def enum(obj,prop,value):
    return next(i.identifier for i in obj.bl_rna.properties[prop].enum_items if i.identifier==value)
def uv(state,rect,u,v):
    x,y,_,_=STATES[state]['rects'][0];a,b,w,h=rect
    return ((x+a+.01+u*(w-.02))/W,1-(y+b+.01+v*(h-.02))/H)
materials={}
def material(state,base,role):
    key=(state,base,role)
    if key in materials:return materials[key]
    mat=bpy.data.materials.new('Crate_'+role+'_'+state);mat.use_nodes=True
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.85
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.interpolation=enum(tex,'interpolation','Closest')
    co=mat.node_tree.nodes.new('ShaderNodeTexCoord');mp=mat.node_tree.nodes.new('ShaderNodeMapping')
    a=STATES[state]['rects'][0];b=STATES[base]['rects'][0]
    mp.inputs['Location'].default_value=((a[0]-b[0])/W,-(a[1]-b[1])/H,0)
    for fr,to in [(co.outputs['UV'],mp.inputs['Vector']),(mp.outputs['Vector'],tex.inputs['Vector']),(tex.outputs['Color'],bs.inputs['Base Color'])]:mat.node_tree.links.new(fr,to)
    materials[key]=mat;return mat
class Mesh:
    def __init__(self):self.v=[];self.faces=[];self.uvs=[];self.slots=[]
    def face(self,ids,rect,slot=0,coords=None):
        self.faces.append(ids);self.slots.append(slot)
        self.uvs.append([uv(BASES[slot],rect,u,v) for u,v in (coords or [(0,1),(1,1),(1,0),(0,0)])])
    def box(self,x0,x1,y0,y1,z0,z1,rect,slot=0,front=None,back=None,top=None,bottom=None):
        k=len(self.v);self.v.extend((x,y,z) for z in (z0,z1) for x,y in [(x0,y0),(x1,y0),(x1,y1),(x0,y1)])
        for ids,spec in [([3,2,1,0],bottom),([4,5,6,7],top),([0,1,5,4],front),([1,2,6,5],None),([2,3,7,6],back),([3,0,4,7],None)]:
            r,s=spec if spec else (rect,slot);self.face([k+i for i in ids],r,s)
    def object(self,name,scene,mats,pivot=(0,0,0)):
        me=bpy.data.meshes.new(name);me.from_pydata([Vector(v)-Vector(pivot) for v in self.v],[],self.faces)
        for mat in mats:me.materials.append(mat)
        layer=me.uv_layers.new(name='Original_Crates_Atlas')
        for p,coords,slot in zip(me.polygons,self.uvs,self.slots):
            p.material_index=slot
            for i,c in zip(p.loop_indices,coords):layer.data[i].uv=c
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(me);bm.free()
        me.update();me.calc_loop_triangles();ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.location=pivot;return ob

# Each specification describes a genuinely different silhouette or opening mechanism.
specs={
 'metal_crate':('crate','crateopen','hinge',1.0,.78,.62),
 'ribbed_crate':('weaponcrate','weaponcrateopen','ribbed',1.0,.78,.68),
 'wooden_crate':('wooden','woodenopen','wood',1.0,.78,.62),
 'freezer_crate':('freezer','freezeropen','freezer',1.0,.78,.62),
 'mining_cart':('miningcar','miningcaropen','cart',1.0,.80,.70),
 'trash_cart':('trashcart','trashcartopen','split',1.0,.80,.86),
 'laundry_cart':('laundry','laundryopen','laundry',1.0,.80,.74),
 'large_wooden_crate':('largecrate','largecrate','large',1.0,.84,1.15),
 'animal_crate':('crittercrate','crittercrate_door','animal',1.0,.82,1.04),
 'necropolis_chest':('necrocrate','necrocrateopen','necro',1.0,.78,.70),
 'coffin':('coffin','coffinopen','coffin',1.8,.70,.48),
 'treasure_chest':('treasure','treasureopen','treasure',1.0,.78,.72),
 'mail_crate':('mailsealed','mailopen','mail',.96,.72,.72),
 'cardboard_crate':('cardboard','cardboardopen','cardboard',.94,.78,.78),
 'ancient_sarcophagus':('necro_bubblegum','necro_bubblegumopen','sarcophagus',1.50,.72,.62),
 'trash_bin':('trashbin','trashbinopen','bin',.52,.52,.88),
}
models={};stats={}
for name,(closed,opened,kind,w,d,h) in specs.items():
    BASES=[closed,opened];mats=[material(s,s,r) for s,r in zip(BASES,['exterior','interior'])]
    scene=bpy.data.scenes.new('Crate_Model_'+name);bpy.context.window.scene=scene
    a=w/2;b=d/2;t=.035;floor=.035;body_top=h-.055
    if kind in ['cart','split','laundry']:floor=.14
    front=(2,38,27,8);top=(2,25,27,12);inside=(4,15,23,11);bottom=(5,30,21,8);side=(3,39,8,6);plain=(5,40,1,1)
    if kind=='animal':front=(4,29,24,16);top=(4,18,24,9);side=(3,20,2,23);plain=(4,25,1,1);bottom=(7,39,18,5)
    if kind=='large':front=(6,28,23,18);top=(6,18,23,10);side=(7,30,7,15);plain=(10,33,1,1)
    if kind=='cardboard':front=(5,34,24,13);top=(5,26,24,7);side=(6,35,7,10);plain=(8,35,1,1);bottom=(8,30,16,5)
    if kind=='mail':front=(5,35,22,9);top=(4,24,24,11);side=(6,36,7,7);plain=(8,38,1,1);bottom=(6,26,20,7)
    if kind=='sarcophagus':front=(4,36,23,8);top=(3,28,25,7);side=(4,37,5,6);plain=(5,38,1,1);bottom=(5,29,20,5)
    if kind=='necro':front=(4,36,24,9);top=(4,27,24,8);inside=(4,20,24,9);side=(4,37,5,7);plain=(4,40,1,1)
    if kind=='treasure':front=(4,39,24,7);top=(4,30,24,8);inside=(6,24,20,10);side=(4,40,5,6);plain=(6,42,1,1)
    if kind=='cart':front=(5,30,22,6);top=(2,21,28,8);side=(7,31,7,4);plain=(10,33,1,1);inside=(2,14,28,7);bottom=(6,24,20,4)
    if kind=='laundry':front=(4,35,24,8);top=(3,25,26,9);side=(4,36,7,6);plain=(5,39,1,1)
    if kind=='split':front=(2,34,28,11);top=(2,21,28,12);side=(4,35,9,9);plain=(5,38,1,1);inside=(2,14,12,14)
    m=Mesh();moving=[]
    def panel(mesh,suffix,pivot,axis='X',angle=-110,motion='hinge'):
        ob=mesh.object(name+'_'+suffix,scene,mats,pivot);ob['motion']=motion;ob['open_axis']=axis;ob['open_degrees']=angle;moving.append(ob);return ob
    if kind in ['coffin','bin']:
        if kind=='coffin':
            # Long coffin, wider at the shoulders.
            outline=[(-a,-b*.62),(-a*.50,-b),(a*.55,-b),(a,-b*.55),(a,b*.55),(a*.55,b),(-a*.50,b),(-a,b*.62)]
            top=(2,27,28,20);bottom=(3,31,26,15);plain=(13,33,1,1);side=(13,34,6,6)
        else:
            outline=[(a*math.cos(i*math.tau/8),b*math.sin(i*math.tau/8)) for i in range(8)]
            front=(10,31,11,13);top=(9,25,13,2);bottom=(11,30,9,5);side=front;plain=(12,37,1,1)
        n=len(outline);inner=[(x*(1-t/a),y*(1-t/b)) for x,y in outline]
        m.v=[(x,y,z) for z,ring in [(floor,outline),(body_top,outline),(body_top,inner),(floor+t,inner)] for x,y in ring]
        coords=lambda ring:[((x+a)/w,(b-y)/d) for x,y in ring]
        m.face(list(reversed(range(n))),plain,0,coords(outline));m.face(list(range(3*n,4*n)),bottom,1,coords(inner))
        for i in range(n):
            j=(i+1)%n
            m.face([i,j,n+j,n+i],side);m.face([n+i,n+j,2*n+j,2*n+i],plain)
            m.face([2*n+i,2*n+j,3*n+j,3*n+i],plain)
        lid=Mesh();lid.v=[(x,y,z) for z in (body_top,h) for x,y in outline]
        lid.face(list(reversed(range(n))),plain,0,coords(outline));lid.face(list(range(n,2*n)),top,0,coords(outline))
        for i in range(n):j=(i+1)%n;lid.face([i,j,n+j,n+i],plain)
        panel(lid,'Lid',(0,b,body_top),motion='lift' if kind=='bin' else 'hinge')
    else:
        # Five walls with actual inner faces; no solid block behind the cavity.
        m.box(-a,a,-b,b,floor,floor+t,plain,top=(bottom,1 if kind not in ['animal','large'] else 0))
        m.box(-a,a,b-t,b,floor+t,body_top,side,front=((6,30,19,4),1) if kind not in ['large','animal','cardboard','mail','sarcophagus'] else None)
        m.box(-a,-a+t,-b,b-t,floor+t,body_top,side)
        m.box(a-t,a,-b,b-t,floor+t,body_top,side)
        if kind=='animal':
            m.box(-a+t,a-t,-b,-b+t,floor+t,.16,plain)
            m.box(-a+t,a-t,-b,-b+t,.78,h,plain,front=(top,0))
            m.box(-a,a,-b,b,h-.035,h,plain,top=((5,19,5,6),0))
            # Original oxygen tank at the right of the animal carrier.
            m.box(a,a+.07,b-.28,b-.07,.23,.72,(29,29,2,11))
            door=Mesh();door.box(-a+t,a-t,-b-.035,-b,.16,.78,plain,front=(front,1),back=(front,1))
            panel(door,'Door',(-a+t,-b,.16),'Z',-115)
        else:
            m.box(-a+t,a-t,-b,-b+t,floor+t,body_top,side,front=(front,0),back=((7,32,17,3),1) if kind not in ['large','cardboard','mail','sarcophagus'] else None)
            count=4 if kind=='cardboard' else 2 if kind=='split' else 1
            for i in range(count):
                lid=Mesh();x0,x1=-a,a;y0,y1=-b,b;pivot=(0,b,body_top);axis='X';angle=-110
                if kind=='split':x0,x1=(-a,0) if i==0 else (0,a);pivot=((x0+x1)/2,b,body_top)
                if kind=='cardboard':
                    if i<2:y0,y1=(-b,0) if i==0 else (0,b);pivot=(0,-b if i==0 else b,body_top);angle=110 if i==0 else -110
                    else:x0,x1=(-a,0) if i==2 else (0,a);pivot=(-a if i==2 else a,0,body_top);axis='Y';angle=-110 if i==2 else 110
                reverse=inside if kind in ['hinge','ribbed','wood','freezer','split','necro','treasure','cart'] else plain
                revslot=1 if reverse==inside else 0
                lidtop=(top[0]+i*top[2]/2,top[1],top[2]/2,top[3]) if kind=='split' else top
                lid.box(x0,x1,y0,y1,body_top,h,plain,top=(lidtop,0),bottom=(reverse,revslot))
                if kind in ['ribbed','split','treasure','large']:
                    for u in [.1,.45,.8]:
                        rx=x0+(x1-x0)*u;lid.box(rx,rx+.055,y0,y1,h,h+.045,(4,27,2,9))
                panel(lid,'Lid' if count==1 else 'Lid_'+str(i+1),pivot,axis,0 if kind=='split' and i==1 else angle,'remove' if kind in ['large','mail','sarcophagus'] else 'hinge')
        if kind=='cart':
            m.v=[(x*(.76+.24*(z-floor)/(body_top-floor)),y*(.83+.17*(z-floor)/(body_top-floor)),z) for x,y,z in m.v]
        if kind in ['cart','split','laundry']:
            for x in [-a+.10,a-.10]:
                for y in [-b+.12,b-.12]:
                    # Eight-sided wheel, axle along X; flat shading keeps the pixel-art form.
                    k=len(m.v);m.v.extend((xx,y+.085*math.cos(i*math.tau/8),.085+.085*math.sin(i*math.tau/8)) for xx in (x-.045,x+.045) for i in range(8))
                    wr=(7,36,2,2) if kind=='cart' else (5,44,2,2)
                    m.face(list(reversed(range(k,k+8))),wr,coords=[(.5+.5*math.cos(i*math.tau/8),.5+.5*math.sin(i*math.tau/8)) for i in reversed(range(8))])
                    m.face(list(range(k+8,k+16)),wr,coords=[(.5+.5*math.cos(i*math.tau/8),.5+.5*math.sin(i*math.tau/8)) for i in range(8)])
                    for i in range(8):j=(i+1)%8;m.face([k+i,k+j,k+8+j,k+8+i],wr)
        if kind=='large':
            for x in [-a,a-.07]:m.box(x,x+.07,-b-.02,b+.02,0,h+.06,(7,19,2,8))
        if kind=='necro':
            for x in [-a,a-.055]:m.box(x,x+.055,-b,b,.05,h+.06,(3,29,2,8))
        if kind=='freezer':
            # Low shelf rails distinguish the insulated freezer compartment.
            for x in [-a+.12,a-.15]:m.box(x,x+.03,-b+.10,b-.10,floor+t,floor+.10,(6,38,2,4),1)
    body=m.object(name+'_Body',scene,mats);parts=[body,*moving]
    root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
    for ob in parts:ob.parent=root
    root['source_closed']=closed;root['source_open']=opened;root['source_atlas']='crates.png'
    for ob in scene.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=body
    path=OUT/(name+'.glb');bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
    for node in g['nodes']:node['name']=node['name'].split('.')[0]
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    for im in g['images']:
        view=g['bufferViews'][im['bufferView']];offset=view.get('byteOffset',0);assert tail[8+offset:8+offset+view['byteLength']]==SOURCE.read_bytes()
    assert len(g['meshes'])==len(parts)
    stats[name]={'file':path.name,'triangles':sum(len(o.data.loop_triangles) for o in parts),'parts':[o.name.split('.')[0] for o in parts],'base_states':BASES.copy(),'png_byte_identical':True,'nearest':True}
    models[name]=(parts,mats,scene)

# Source audit records explicit assignments; inheritance still follows the DM hierarchy.
uses={}
ref=ROOT.parent/'tgstation/code'
for path in ref.rglob('*.dm'):
    current=''
    for lineno,line in enumerate(path.read_text(encoding='utf-8-sig',errors='replace').splitlines(),1):
        if line.startswith('/'):current=line.split('(')[0].strip()
        match=re.match(r'\s*(?:base_icon_state|icon_state)\s*=\s*"([^"]+)"',line)
        if match and match[1] in STATES and '/closet/crate' in current:uses.setdefault(match[1],[]).append({'type':current,'file':str(path.relative_to(ref)),'line':lineno})
special={v[0]:k for k,v in specs.items()};special.update({'plasmacrate':'ribbed_crate','securetrashcart':'trash_cart','interdynefreezer':'freezer_crate','cardboard_moth':'cardboard_crate','cardboard_tiziran':'cardboard_crate'})
excluded={'grave','grave_lead','grave_fresh'}
variants=[]
for state in STATES:
    if state in excluded:continue
    if state+'open' not in STATES and state not in special:continue
    family=special.get(state,'metal_crate');base_closed,base_open=specs[family][:2]
    opened=state+'open' if state+'open' in STATES else base_open
    if family=='animal_crate':opened='crittercrate_door'
    if family=='cardboard_crate':opened='cardboardopen'
    # mail is the contents sprite, whereas mailsealed is the closed crate.
    if state=='mail':continue
    offsets=[]
    for s,base in zip([state,opened],[base_closed,base_open]):
        a=STATES[s]['rects'][0];b=STATES[base]['rects'][0];offsets.append([(a[0]-b[0])/W,(a[1]-b[1])/H,0])
    variants.append({'id':state,'model':family,'file':family+'.glb','closed_state':state,'open_state':opened,'godot_uv_offsets':dict(zip(['exterior','interior'],offsets)),'source_uses':uses.get('mail' if state=='mailsealed' else state,[]),'atlas_only':not bool(uses.get('mail' if state=='mailsealed' else state))})

gallery=bpy.data.scenes.new('Crates_All_Variants');bpy.context.window.scene=gallery
def instance(v,scene,pos,opened=False):
    parts,_,_=models[v['model']];bases=stats[v['model']]['base_states']
    mats=[material(s,b,r) for s,b,r in zip([v['closed_state'],v['open_state']],bases,['exterior','interior'])]
    for original in parts:
        ob=original.copy();ob.parent=None;ob.name=v['id']+('_Open_' if opened else '_Closed_')+original.name;scene.collection.objects.link(ob);ob.location=original.location+Vector(pos)
        for slot,mat in zip(ob.material_slots,mats):slot.link='OBJECT';slot.material=mat
        if opened and 'motion' in ob:
            if ob['motion'] in ['remove','lift']:ob.location+=Vector((0,.55,.55))
            else:ob.rotation_euler['XYZ'.index(ob['open_axis'])]=math.radians(ob['open_degrees'])
    return
for i,v in enumerate(variants):
    x=(i%6)*3.5;y=(i//6)*2.6
    instance(v,gallery,(x,y,0));instance(v,gallery,(x+1.75,y,0),True)
    data=bpy.data.curves.new('Crate_Label','FONT');data.body=v['id'];data.size=.13
    ob=bpy.data.objects.new('Label_'+v['id'],data);gallery.collection.objects.link(ob);ob.location=(x-.5,y-.8,.01);ob.rotation_euler.x=math.radians(90)
preview=bpy.data.scenes.new('Crates_Preview')
for i,family in enumerate(specs):
    v=next(v for v in variants if v['model']==family);pos=((i%4)*3.6,(i//4)*2.8,0)
    instance(v,preview,pos);instance(v,preview,(pos[0]+1.8,pos[1],0),True)
def stage(scene,target,scale,res):
    world=bpy.data.worlds.new('Crates_World');world.use_nodes=True;scene.world=world
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.095,.11,.14,1);bg.inputs['Strength'].default_value=.6
    for name,rot,energy in [('Key',(25,-25,-25),2),('Fill',(45,20,140),1)]:
        light_type=next(i.identifier for i in bpy.types.Light.bl_rna.properties['type'].enum_items if i.identifier=='SUN')
        data=bpy.data.lights.new('Crates_'+name,light_type);data.energy=energy;data.angle=.25
        ob=bpy.data.objects.new(data.name,data);scene.collection.objects.link(ob);ob.rotation_euler=[math.radians(r) for r in rot]
    data=bpy.data.cameras.new('Crates_Camera');data.type=enum(data,'type','ORTHO');data.ortho_scale=scale
    cam=bpy.data.objects.new('Crates_Camera',data);scene.collection.objects.link(cam);target=Vector(target);cam.location=target+Vector((4,-14,14));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    scene.render.resolution_x=res[0];scene.render.resolution_y=res[1];scene.render.resolution_percentage=100
stage(gallery,(9.7,14.2,.3),38,(1600,1700));stage(preview,(5.8,4.2,.3),17,(1600,1250))
manifest={'atlas':'res://assets/tg/icons/obj/storage/crates.png','source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'models':stats,'variants':variants,
 'excluded_non_crates':['grave','grave_lead','grave_fresh'],
 'notes':['Ordinary crates: base_icon_state + open suffix. Lock lamps, manifest, welded marks are independent overlays.',
 'Mail: mailsealed closed; mailopen empty; mail filled with contents; separate maillid overlay.',
 'Animal carrier: crittercrate body plus crittercrate_door. Cardboard: unchanged body plus cardboardopen overlay.',
 'Large crate is dismantled on opening in game. Its separate top is supplied for optional debris, not a hinged game lid.',
 'Mail and ancient sarcophagus have removable lids. Grave terrain, keys, effects and contents overlays are excluded.',
 'Coffin directional frames and bubblegum animated frames are represented by the first frame; original geometry is shared.',
 'Hidden surfaces and depth are reconstructed. No source atlas was repainted. No runtime integration or baked animation.']}
(OUT/'variants.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
bpy.context.window.scene=gallery
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=enum(sp.shading,'type','MATERIAL');sp.region_3d.view_location=(9.7,14.2,0);sp.region_3d.view_distance=38;sp.region_3d.view_rotation=Vector((2,-6,8)).to_track_quat('Z','Y');sp.overlay.show_floor=False
bpy.data.libraries.write(str(OUT/'crates_library.blend'),{gallery,preview,*[m[2] for m in models.values()]},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.context.window.scene=preview;preview.render.image_settings.file_format=enum(preview.render.image_settings,'file_format','PNG');preview.render.filepath=str(ROOT/'docs/ai/art/crates_preview.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=gallery
print(json.dumps({'models':stats,'variants':len(variants),'scene':gallery.name}))
