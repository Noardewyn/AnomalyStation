"""Seven sprite-referenced chairs. Run in the live Blender through MCP.

Only new scenes are created. Original atlas pixels are embedded without alteration.
Pixel masks become closed rectilinear prisms, including the wooden backrest holes.
"""
from pathlib import Path
import bpy, bmesh, json, struct, hashlib, math, ast
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'godot/assets/tg/icons/obj/chairs.png'
OUT = ROOT / 'godot/assets/models/furniture/chair_family'
OUT.mkdir(parents=True, exist_ok=True)
META = json.loads(SRC.with_suffix('.json').read_text())
STATES = {s['name']: s for s in META['states']}
SOURCE_HASH = hashlib.sha256(SRC.read_bytes()).hexdigest()
IM = bpy.data.images.load(str(SRC), check_existing=True)
IM.pack()
W, H = IM.size
PIXELS = list(IM.pixels)


def enum(obj, prop, value):
    items=[i.identifier for i in obj.bl_rna.properties[prop].enum_items]
    if value in items:return value
    # Dynamic OCIO enums are absent from RNA; Blender reports the actual choices.
    try:setattr(obj,prop,'__inspect_available__')
    except TypeError as error:
        items=ast.literal_eval(str(error).split('not found in ',1)[1])
    return next(item for item in items if item==value)


def pixel(state, x, y, direction=0):
    rx, ry, _, _ = STATES[state]['rects'][direction]
    n = ((H - 1 - ry - int(y)) * W + rx + int(x)) * 4
    return PIXELS[n:n+4]


def uv(state, x, y, direction=0):
    rx, ry, _, _ = STATES[state]['rects'][direction]
    return ((rx+x)/W, 1-(ry+y)/H)


MATERIALS = {}


def material(name, tint=(1, 1, 1)):
    if name in MATERIALS:
        return MATERIALS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value = .86
    bs.inputs['Specular IOR Level'].default_value = .12
    t = m.node_tree.nodes.new('ShaderNodeTexImage')
    t.image = IM
    t.interpolation = enum(t, 'interpolation', 'Closest')
    if tint == (1, 1, 1):
        m.node_tree.links.new(t.outputs['Color'], bs.inputs['Base Color'])
    else:
        mult = m.node_tree.nodes.new('ShaderNodeMixRGB')
        mult.blend_type = enum(mult, 'blend_type', 'MULTIPLY')
        mult.inputs[0].default_value = 1
        # BYOND tint multiplication is in sRGB; convert tint to scene-linear.
        mult.inputs[2].default_value = tuple(((c+.055)/1.055)**2.4 if c > .04045 else c/12.92 for c in tint)+(1,)
        m.node_tree.links.new(t.outputs['Color'], mult.inputs[1])
        m.node_tree.links.new(mult.outputs[0], bs.inputs['Base Color'])
    m['source_atlas'] = 'res://assets/tg/icons/obj/chairs.png'
    m['source_tint_srgb'] = list(tint)
    MATERIALS[name] = m
    return m


ATLAS = material('Chair_Original_Atlas')
UPHOLSTERY = material('Comfy_Upholstery_Tint')
ARMREST = material('Comfy_Armrest_Overlay_Tint')


class Mesh:
    def __init__(self):
        self.v, self.f, self.uvs = [], [], []
        self.lookup = {}

    def face(self, points, coords):
        ids = []
        for point in points:
            point = tuple(round(c, 7) for c in point)
            if point not in self.lookup:
                self.lookup[point] = len(self.v)
                self.v.append(point)
            ids.append(self.lookup[point])
        self.f.append(ids)
        self.uvs.append(coords)

    def box(self, bounds, state, sample, front=None):
        x0, x1, y0, y1, z0, z1 = bounds
        v = [(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),
             (x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)]
        constant = uv(state, sample[0]+.5, sample[1]+.5)
        assert pixel(state, *sample)[3] > .99
        for n, ids in enumerate([(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]):
            coords = [constant]*4
            if n == 2 and front:
                a,b,c,d = front
                coords = [uv(state,a,d),uv(state,c,d),uv(state,c,b),uv(state,a,b)]
            self.face([v[i] for i in ids], coords)

    def profile(self, state, rect, transform, thickness, sample, rear_direction=None, rear_shift=0):
        """Greedy cap rectangles plus merged boundary edges; split all T junctions."""
        x0,y0,x1,y1 = rect
        cells = {(x,y) for y in range(y0,y1) for x in range(x0,x1) if pixel(state,x,y)[3] > .99}
        pending = set(cells)
        caps = []
        while pending:
            x,y = min(pending,key=lambda p:(p[1],p[0]))
            right=x+1
            while (right,y) in pending: right+=1
            bottom=y+1
            while all((u,bottom) in pending for u in range(x,right)): bottom+=1
            caps.append([(x,y),(right,y),(right,bottom),(x,bottom)])
            pending.difference_update((u,v) for v in range(y,bottom) for u in range(x,right))
        edges=[]
        for x,y in cells:
            for neighbor,a,b in [((x,y-1),(x,y),(x+1,y)),((x+1,y),(x+1,y),(x+1,y+1)),
                                  ((x,y+1),(x+1,y+1),(x,y+1)),((x-1,y),(x,y+1),(x,y))]:
                if neighbor not in cells: edges.append((a,b))
        remaining=set(edges);edges=[]
        while remaining:
            a,b=remaining.pop();dx,dy=b[0]-a[0],b[1]-a[1]
            while (b,(b[0]+dx,b[1]+dy)) in remaining:
                nxt=(b[0]+dx,b[1]+dy);remaining.remove((b,nxt));b=nxt
            while ((a[0]-dx,a[1]-dy),a) in remaining:
                prev=(a[0]-dx,a[1]-dy);remaining.remove((prev,a));a=prev
            edges.append((a,b))
        # Merge only straight runs; every turn remains exactly on a pixel boundary.
        corners=set(p for cap in caps for p in cap) | set(p for edge in edges for p in edge)
        def split(poly):
            result=[]
            for a,b in zip(poly,poly[1:]+poly[:1]):
                dx,dy=b[0]-a[0],b[1]-a[1]
                line=[p for p in corners if (p[0]-a[0])*dy == (p[1]-a[1])*dx and
                      min(a[0],b[0])<=p[0]<=max(a[0],b[0]) and min(a[1],b[1])<=p[1]<=max(a[1],b[1])]
                line.sort(key=lambda p:(p[0]-a[0])*dx+(p[1]-a[1])*dy)
                result+=line[:-1]
            return result
        const=uv(state,sample[0]+.5,sample[1]+.5)
        assert pixel(state,*sample)[3] > .99
        for cap in caps:
            poly=split(cap)
            coords=[]
            cx=sum(x for x,y in cap)/4;cy=sum(y for x,y in cap)/4
            for x,y in poly:
                coords.append(uv(state,x,y))
            self.face([transform(x,y,0) for x,y in poly],coords)
            if rear_direction is None:
                rear=[const]*len(poly)
            else:
                rear=[uv(state,x,y+rear_shift,rear_direction) for x,y in poly]
            self.face([transform(x,y,thickness) for x,y in poly],rear)
        for a,b in edges:
            dx,dy=b[0]-a[0],b[1]-a[1]
            line=[p for p in corners if (p[0]-a[0])*dy==(p[1]-a[1])*dx and
                  min(a[0],b[0])<=p[0]<=max(a[0],b[0]) and min(a[1],b[1])<=p[1]<=max(a[1],b[1])]
            line.sort(key=lambda p:(p[0]-a[0])*dx+(p[1]-a[1])*dy)
            points=[transform(*p,0) for p in line]+[transform(*p,thickness) for p in reversed(line)]
            self.face(points,[const]*len(points))

    def object(self, name, scene, mat=ATLAS, pivot=(0,0,0)):
        me=bpy.data.meshes.new(name+'_Mesh')
        me.from_pydata([tuple(v[i]-pivot[i] for i in range(3)) for v in self.v],[],self.f)
        me.materials.append(mat)
        layer=me.uv_layers.new(name='Original_Atlas_UV')
        for p,coords in zip(me.polygons,self.uvs):
            for li,co in zip(p.loop_indices,coords): layer.data[li].uv=co
        bm=bmesh.new();bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        # Preserve silhouette steps; dissolve only redundant straight side seams.
        bmesh.ops.dissolve_limit(bm,angle_limit=.00001,verts=list(bm.verts),edges=list(bm.edges),use_dissolve_boundaries=False,delimit={'UV'})
        bad=[e for e in bm.edges if not e.is_manifold]
        assert not bad,(name,len(bad))
        assert all(v.is_manifold for v in bm.verts),(name,'non-manifold vertex')
        bm.to_mesh(me);bm.free();me.update();me.calc_loop_triangles()
        for p in me.polygons: p.use_smooth=False
        ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.location=pivot
        return ob


ASSETS={}


def new_asset(key,state):
    scene=bpy.data.scenes.new('Chair_'+key)
    scene.unit_settings.system=enum(scene.unit_settings,'system','METRIC')
    bpy.context.window.scene=scene
    root=bpy.data.objects.new(key,None);scene.collection.objects.link(root)
    root['source_state']=state;root['source_atlas']='res://assets/tg/icons/obj/chairs.png'
    root['front_axis']='-Y in Blender; +Z after glTF conversion'
    root['seat_height_m']=.72 if key=='bar_stool' else .45
    ASSETS[key]={'scene':scene,'root':root,'parts':[],'state':state}
    return scene,root


def add(key,mesh,name,mat=ATLAS,pivot=(0,0,0),parent=None):
    asset=ASSETS[key]
    ob=mesh.object(name,asset['scene'],mat,pivot)
    ob.parent=parent or asset['root']
    if parent: ob.location-=parent.location
    asset['parts'].append(ob)
    return ob


def box_part(key,name,bounds,state,sample,front=None,mat=ATLAS):
    m=Mesh();m.box(bounds,state,sample,front)
    return add(key,m,name,mat)


def seat(key,state,rect,height,depth,scale,thick,sample,mat=ATLAS):
    x0,y0,x1,y1=rect
    m=Mesh()
    m.profile(state,rect,lambda x,y,t:((x-(x0+x1)/2)*scale, depth/2-(y-y0)/(y1-y0)*depth, height-t),thick,sample)
    ob=add(key,m,'Seat',mat,pivot=(0,0,height-thick))
    ob['source_rect_local_px']=list(rect)
    return ob


# Square red stool with four steel legs and a low rear grab rail.
key='metal_stool';state='stool';new_asset(key,state)
seat(key,state,(8,17,24,25),.45,.48,.034,.07,(10,24))
for sx in (-1,1):
    for sy in (-1,1):
        box_part(key,'Leg_'+str(sx)+'_'+str(sy),(sx*.218-.025,sx*.218+.025,sy*.19-.025,sy*.19+.025,0,.395),state,(8,28),front=(7.01,26.01,9.99,30.99))
for sx in (-1,1):
    box_part(key,'Rear_Rail_Post_'+str(sx),(sx*.21-.017,sx*.21+.017,.19,.224,.39,.51),state,(12,16))
box_part(key,'Rear_Grab_Rail',(-.227,.227,.19,.224,.488,.522),state,(12,16))


# Office upper components remain under a floor-normal swivel pivot.
key='office_chair_dark';state='officechair_dark';scene,root=new_asset(key,state)
swivel=bpy.data.objects.new('Swivel_Pivot',None);scene.collection.objects.link(swivel);swivel.parent=root;swivel.location=(0,0,.385)
upper=[]
upper.append(seat(key,state,(9,15,22,26),.45,.46,.04,.075,(11,24)))
m=Mesh();m.profile(state,(10,2,21,13),lambda x,y,t:((x-15.5)*.04,.192+t,.99-(y-2)*.04),.065,(11,5),1,4)
upper.append(add(key,m,'Backrest',pivot=(0,.192,.55)))
upper.append(box_part(key,'Backrest_Support',(-.035,.035,.202,.25,.39,.58),state,(15,14)))
bpy.context.view_layer.update()
for ob in upper:
    world=ob.matrix_world.copy();ob.parent=swivel;ob.matrix_world=world
box_part(key,'Pedestal',(-.031,.031,-.031,.031,.09,.394),state,(15,27))
base=Mesh()
# Four reusable orthogonal spokes. The source shows a compact four-foot base.
# A single watertight cross, without intersecting boxes.
cross=[(-.24,-.026),(-.026,-.026),(-.026,-.24),(.026,-.24),(.026,-.026),(.24,-.026),
       (.24,.026),(.026,.026),(.026,.24),(-.026,.24),(-.026,.026),(-.24,.026)]
cu=uv(state,12.5,28.5)
for z in (.065,.10):base.face([(x,y,z) for x,y in cross],[cu]*len(cross))
for a,b in zip(cross,cross[1:]+cross[:1]):base.face([(*a,.065),(*b,.065),(*b,.10),(*a,.10)],[cu]*4)
add(key,base,'Caster_Base')
for n,(x,y) in enumerate([(-.225,0),(.225,0),(0,-.225),(0,.225)]):
    m=Mesh();m.box((x-.036,x+.036,y-.021,y+.021,0,.08),state,(10,30))
    wheel=add(key,m,'Caster_'+str(n+1),pivot=(x,y,.04));wheel['rotation_axis_local']='X'

# Identical geometry; only the upholstery UVs change to the adjacent white state.
key2='office_chair_white';scene2,root2=new_asset(key2,'officechair_white')
src_asset=ASSETS[key]
WHITE=material('Office_White_Original_Atlas')
tex=next(n for n in WHITE.node_tree.nodes if n.type=='TEX_IMAGE')
mapping=WHITE.node_tree.nodes.new('ShaderNodeMapping')
mapping.inputs['Location'].default_value=(-288/W,-32/H,0)
coord=WHITE.node_tree.nodes.new('ShaderNodeTexCoord')
WHITE.node_tree.links.new(coord.outputs['UV'],mapping.inputs['Vector'])
WHITE.node_tree.links.new(mapping.outputs['Vector'],tex.inputs['Vector'])
sw2=bpy.data.objects.new('Swivel_Pivot',None);scene2.collection.objects.link(sw2);sw2.parent=root2;sw2.location=swivel.location
for original in src_asset['parts']:
    ob=original.copy();ob.name=original.name.split('.')[0];scene2.collection.objects.link(ob)
    if original in upper:
        ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=WHITE
        ob.parent=sw2
    else:
        ob.parent=root2
    ASSETS[key2]['parts'].append(ob)


# Soft chair: body, cushion, backrest and two solid armrest overlay parts.
key='comfy_chair';state='comfychair';new_asset(key,state)
box_part(key,'Upholstered_Base',(-.306,.306,-.25,.25,.07,.35),state,(10,28),front=(7.01,25.01,24.99,30.99),mat=UPHOLSTERY)
seat(key,state,(10,18,22,24),.45,.44,.036,.10,(12,23),UPHOLSTERY)
m=Mesh();m.profile(state,(9,4,23,18),lambda x,y,t:((x-16)*.036,.19+t,1.008-(y-4)*.04),.12,(12,10),1,7)
add(key,m,'Backrest',UPHOLSTERY,pivot=(0,.19,.448))
box_part(key,'Rear_Upholstered_Support',(-.24,.24,.19,.31,.30,.46),state,(12,12),mat=UPHOLSTERY)
for sx in (-1,1):
    m=Mesh()
    # The E-direction overlay contains the outer arm profile. Its pixel outline is solid geometry.
    # Read the side overlay rather than the intentionally blank south overlay.
    overlay_state='_comfy_side'
    STATES[overlay_state]=dict(STATES['comfychair_armrest'],rects=[STATES['comfychair_armrest']['rects'][2]])
    m.profile(overlay_state,(12,20,25,26),lambda x,y,t:(sx*(.222+t),.25-(x-12)*.04,.64-(y-20)*(.29/6)),.12,(18,23),0,0)
    ob=add(key,m,'Armrest_Overlay_'+('Left' if sx<0 else 'Right'),ARMREST,pivot=(sx*.222,0,.35))
    ob['source_overlay_state']='comfychair_armrest';ob['source_direction']='E, mirrored for opposite side'
for sx in (-1,1):
    for sy in (-1,1):
        box_part(key,'Foot_'+str(sx)+'_'+str(sy),(sx*.27-.025,sx*.27+.025,sy*.21-.025,sy*.21+.025,0,.08),state,(8,30),mat=UPHOLSTERY)


# Bar stool: red stepped cushion, steel pedestal and low rear loop.
key='bar_stool';state='bar';new_asset(key,state)
seat(key,state,(9,13,22,23),.72,.43,.036,.085,(11,20))
box_part(key,'Pedestal',(-.026,.026,-.026,.026,.045,.65),state,(15,25),front=(14.01,23.01,16.99,25.99))
# Stepped square foot echoes the pixel silhouette without a bevel or smooth disk.
foot_state='_bar_foot';STATES[foot_state]=dict(STATES[state],rects=[STATES[state]['rects'][0]])
m=Mesh();m.profile(foot_state,(10,26,21,32),lambda x,y,t:((x-15.5)*.035,.19-(y-26)/6*.38,.045-t),.045,(14,29))
add(key,m,'Pedestal_Foot')
for sx in (-1,1):
    box_part(key,'Rear_Loop_Post_'+str(sx),(sx*.09-.018,sx*.09+.018,.14,.176,.65,.83),state,(15,11))
box_part(key,'Rear_Loop_Rail',(-.108,.108,.14,.176,.80,.836),state,(15,11))


# Wooden seats and frames are shared between the ordinary and winged back variants.
for key,state in [('wooden_chair','wooden_chair'),('wooden_chair_wings','wooden_chair_wings')]:
    new_asset(key,state)
    seat(key,state,(8,19,23,26),.45,.45,.036,.065,(12,25))
    m=Mesh();rect=(5,3,26,18) if key.endswith('wings') else (9,3,22,18)
    m.profile(state,rect,lambda x,y,t:((x-15.5)*.036,.17+t,1.02-(y-3)*.036),.055,(11,15),1,6)
    back=add(key,m,'Winged_Backrest' if key.endswith('wings') else 'Slatted_Backrest',pivot=(0,.17,.48))
    back['openings']='Original alpha gaps are closed, lined through-holes; no alpha cards'
    for sx in (-1,1):
        for sy in (-1,1):
            ztop=.60 if sy>0 else .40
            box_part(key,'Leg_'+str(sx)+'_'+str(sy),(sx*.234-.025,sx*.234+.025,sy*.19-.025,sy*.19+.025,0,ztop),state,(9,29),front=(8.01,27.01,10.99,30.99))
    box_part(key,'Front_Apron',(-.21,.21,-.21,-.17,.335,.39),state,(15,26),front=(11.01,25.01,19.99,26.99))
    for sx in (-1,1):
        box_part(key,'Side_Apron_'+str(sx),(sx*.21-.022,sx*.21+.022,-.17,.17,.335,.39),state,(12,26))


# Actual linked meshes in the library for shared wooden lower components.
for ob1,ob2 in zip(ASSETS['wooden_chair']['parts'],ASSETS['wooden_chair_wings']['parts']):
    if 'Backrest' not in ob1.name:ob2.data=ob1.data


revision_path=ROOT/'tools/art/revise_station_props.py'
revision={'__file__':str(revision_path)}
exec(compile(revision_path.read_text(encoding='utf-8'),str(revision_path),'exec'),revision)
revision['revise_chairs'](globals())


def patch_glb(path):
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0]
    g=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for sampler in g.get('samplers',[]): sampler.update(minFilter=9728,magFilter=9728,wrapS=33071,wrapT=33071)
    for mat in g.get('materials',[]):
        mat['alphaMode']='OPAQUE';mat['doubleSided']=False;mat['name']=mat['name'].split('.')[0]
    for node in g.get('nodes',[]): node['name']=node.get('name','').split('.')[0]
    for image in g.get('images',[]):
        bv=g['bufferViews'][image['bufferView']];start=8+bv.get('byteOffset',0)
        assert hashlib.sha256(tail[start:start+bv['byteLength']]).hexdigest()==SOURCE_HASH
    assert not g.get('animations')
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    return g


REPORT={}
for key,a in ASSETS.items():
    bpy.context.window.scene=a['scene'];bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active=a['parts'][0]
    path=OUT/(key+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    gltf=patch_glb(path)
    bpy.data.libraries.write(str(OUT/(key+'.blend')),{a['scene']},path_remap='RELATIVE',fake_user=True,compress=True)
    bounds=[ob.matrix_world@Vector(c) for ob in a['parts'] for c in ob.bound_box]
    REPORT[key]={'source_state':a['state'],'source_rects':STATES[a['state']]['rects'],
        'dimensions_m':[round(max(p[i] for p in bounds)-min(p[i] for p in bounds),4) for i in range(3)],
        'triangles':sum(len(ob.data.loop_triangles) for ob in a['parts']),
        'meshes':len(a['parts']),'seat_height_m':a['root']['seat_height_m'],
        'materials':sorted(set(slot.material.name.split('.')[0] for ob in a['parts'] for slot in ob.material_slots)),
        'non_manifold_edges':0,'flat_shading':True,'original_png_embedded_verified':True,
        'original_png_sha256':SOURCE_HASH,'samplers_nearest':True,'animations':0}

# Shared data in the editable library, with atlas UVs differing only where necessary.
for left,right in [('office_chair_dark','office_chair_white'),('wooden_chair','wooden_chair_wings')]:
    for ob1,ob2 in zip(ASSETS[left]['parts'],ASSETS[right]['parts']):
        if len(ob1.data.vertices)!=len(ob2.data.vertices): continue
        if all((v1.co-v2.co).length<1e-6 for v1,v2 in zip(ob1.data.vertices,ob2.data.vertices)):
            REPORT[right].setdefault('geometry_shared_with',[]).append(left+':'+ob1.name.split('.')[0])


PREVIEW=bpy.data.scenes.new('Chair_Family_Preview')
bpy.context.window.scene=PREVIEW
TARGETS=[]
for i,(key,a) in enumerate(ASSETS.items()):
    ox=(i%4)*1.25;oy=(i//4)*1.75
    group=[]
    for original in a['parts']:
        ob=original.copy();ob.parent=None;ob.matrix_world=original.matrix_world.copy()
        ob.location+=Vector((ox,oy,0));ob.name='Preview_'+key+'_'+original.name.split('.')[0]
        PREVIEW.collection.objects.link(ob);group.append(ob)
    TARGETS.append((key,group))
world=bpy.data.worlds.new('Chair_Studio');world.use_nodes=True;PREVIEW.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.09,.12,.17,1);bg.inputs['Strength'].default_value=.5
for name,loc,power in [('Chair_Key',(-3,-5,7),1100),('Chair_Fill',(6,-2,5),850),('Chair_Rim',(1,6,5),1000)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=5
    ob=bpy.data.objects.new(name,ld);PREVIEW.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((1.7,.8,.45))-ob.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.cameras.new('Chair_Preview_Camera');camera.type=enum(camera,'type','ORTHO');camera.ortho_scale=6.1
cam=bpy.data.objects.new('Chair_Preview_Camera',camera);PREVIEW.collection.objects.link(cam)
target=Vector((1.8,.8,.48));cam.location=target+Vector((3,-7,4.2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();PREVIEW.camera=cam
PREVIEW.render.engine='BLENDER_EEVEE_NEXT'
PREVIEW.render.resolution_x=1600;PREVIEW.render.resolution_y=1050;PREVIEW.render.resolution_percentage=100
PREVIEW.render.image_settings.file_format=enum(PREVIEW.render.image_settings,'file_format','PNG')
PREVIEW.render.filepath=str(ROOT/'docs/ai/art/chair_family_preview.png')
PREVIEW.view_settings.view_transform=enum(PREVIEW.view_settings,'view_transform','Standard')
# Standalone Godot staging scene; no scripts, collisions or gameplay references.
lines=['[gd_scene load_steps=11 format=3]','']
for i,key in enumerate(ASSETS,1):
    lines.append('[ext_resource type="PackedScene" path="'+key+'.glb" id="'+str(i)+'"]')
lines += ['', '[sub_resource type="Environment" id="Studio"]',
          'background_mode = 1','background_color = Color(0.06, 0.08, 0.11, 1)',
          'ambient_light_color = Color(1, 1, 1, 1)','ambient_light_energy = 0.55',
          '', '[sub_resource type="StandardMaterial3D" id="FloorMaterial"]',
          'albedo_color = Color(0.12, 0.15, 0.19, 1)','roughness = 1.0',
          '', '[sub_resource type="BoxMesh" id="FloorMesh"]',
          'material = SubResource("FloorMaterial")','size = Vector3(5.6, 0.04, 3.5)',
          '', '[node name="ChairFamilyPreview" type="Node3D"]',
          '', '[node name="Environment" type="WorldEnvironment" parent="."]','environment = SubResource("Studio")',
          '', '[node name="Floor" type="MeshInstance3D" parent="."]',
          'position = Vector3(1.8, -0.025, -0.8)','mesh = SubResource("FloorMesh")',
          '', '[node name="Key" type="DirectionalLight3D" parent="."]',
          'rotation_degrees = Vector3(-50, -30, 0)','light_energy = 1.1','shadow_enabled = true',
          '', '[node name="Fill" type="DirectionalLight3D" parent="."]',
          'rotation_degrees = Vector3(-25, 140, 0)','light_energy = 0.5']
convert=Matrix(((1,0,0),(0,0,1),(0,-1,0)))
basis=convert@cam.rotation_euler.to_matrix();position=convert@cam.location
camera_values=[basis[r][c] for c in range(3) for r in range(3)]+list(position)
lines += ['', '[node name="Camera" type="Camera3D" parent="."]',
          'transform = Transform3D('+', '.join(str(round(v,7)) for v in camera_values)+')',
          'projection = 1','size = 4.0','current = true']
for i,key in enumerate(ASSETS):
    lines += ['', '[node name="'+key+'" parent="." instance=ExtResource("'+str(i+1)+'")]',
              'position = Vector3('+str((i%4)*1.25)+', 0, '+str(-(i//4)*1.75)+')']
(OUT/'chair_family_preview.tscn').write_text('\n'.join(lines)+'\n',encoding='utf-8')
bpy.data.libraries.write(str(OUT/'chair_family_preview.blend'),{PREVIEW},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.data.libraries.write(str(OUT/'chair_family_library.blend'),{PREVIEW}|{a['scene'] for a in ASSETS.values()},path_remap='RELATIVE',fake_user=True,compress=True)
assert hashlib.sha256(SRC.read_bytes()).hexdigest()==SOURCE_HASH
(OUT/'validation.json').write_text(json.dumps(REPORT,indent=2)+'\n',encoding='utf-8')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=enum(sp.shading,'type','MATERIAL')
            sp.region_3d.view_location=target;sp.region_3d.view_distance=6.2
            sp.region_3d.view_rotation=Vector((3,-7,4.2)).to_track_quat('Z','Y');sp.region_3d.view_perspective='ORTHO'
print(json.dumps(REPORT,indent=2))


