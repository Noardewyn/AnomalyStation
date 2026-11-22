"""Blender MCP build: rounded, diagonal-aware stationary pressure tank modules.

Tile = 2m. The offset contours are identical on shared boundaries. Seven height
rings form a circular barrel section; only exterior surfaces are generated.
"""
import bpy, bmesh, math, json, contextlib, io
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props/stationary_tanks/rounded'
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Stationary_Pressure_Tanks_Rounded')
bpy.context.window.scene=scene;scene.unit_settings.system='METRIC'
assets=bpy.data.collections.new('Pressure_Tank_Modules');scene.collection.children.link(assets)
N,S,E,W=1,2,4,8
DIRS=[E,N,W,S];DIAGS=[16,128,64,32]
THETA=[-90,-60,-30,0,30,60,90]

def turn(mask,steps=1):
    for _ in range(steps%4):
        mask=sum(b for a,b in [(E,N),(N,W),(W,S),(S,E),(16,128),(128,64),(64,32),(32,16)] if mask&a)
    return mask
def choose(mask):
    c=mask&15;n=c.bit_count()
    if n==0:return 'single',0
    if n==4:return 'center',0
    base=E if n==1 else (E|W if c in [E|W,N|S] else N|E) if n==2 else N|E|W
    for t in range(4):
        if turn(base,t)==c:
            part='end' if n==1 else 'tee' if n==3 else 'straight' if base==E|W else 'corner' if mask&turn(16,t) else 'cornerOpen'
            return part,t
    raise ValueError(mask)
source=json.loads((ROOT/'godot/assets/tg/icons/obj/pipes_n_cables/stationary_canisters.json').read_text())
states=[int(s['name'].split('-')[-1]) for s in source['states'] if s['name'].startswith('canister-')]
groups={}
for mask in states:
    part,t=choose(mask);canonical=turn(mask,-t);groups.setdefault(canonical,[]).append(mask)

def material(name,h,alpha=1):
    m=bpy.data.materials.new(name);m.use_nodes=True
    rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    m.diffuse_color=(*rgb,alpha);p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Roughness'].default_value=.58;p.inputs['Metallic'].default_value=.15;p.inputs['Alpha'].default_value=alpha
    if alpha<1:m.surface_render_method='DITHERED'
    return m
M={k:material('Pressure_Flat_'+k,h) for k,h in dict(Shell='C9CED0',Feet='707B83',Rim='9AA7AF',Dark='26333B',Tint_Gas='394B55',Highlight='CCDCE3').items()}
M['Glass']=material('Pressure_Flat_Glass','B9D8E4',.16)
objects=[];root=None
def mesh(name,vs,fs,key,smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    me.materials.append(M[key]);o=bpy.data.objects.new(name,me);assets.objects.link(o);o.parent=root;objects.append(o)
    for p in me.polygons:p.use_smooth=smooth
    return o
def box(name,c,sz,key):
    x,y,z=c;a,b,d=[v/2 for v in sz]
    return mesh(name,[(x+i*a,y+j*b,z+k*d) for k in [-1,1] for j in [-1,1] for i in [-1,1]],[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],key)
def contour(mask,t):
    pts=[];a=1-t;k=.30*a
    for q in range(4):
        along=bool(mask&DIRS[q]);across=bool(mask&DIRS[(q+1)%4]);diag=bool(mask&DIAGS[q])
        if along and across:
            p=[(1,1)] if diag else [(1-t*math.sin(j*math.pi/8),1-t*math.cos(j*math.pi/8)) for j in range(5)]
        elif along:p=[(1,a)]
        elif across:p=[(a,1)]
        else:p=[(a-k+k*math.cos(j*math.pi/8),a-k+k*math.sin(j*math.pi/8)) for j in range(5)]
        for x,y in p:
            for _ in range(q):x,y=-y,x
            pts.append((x,y))
    return pts
def on_join(a,b,mask):
    return any(mask&bit and abs(a[axis]-sign)<1e-6 and abs(b[axis]-sign)<1e-6 for bit,axis,sign in [(E,0,1),(W,0,-1),(N,1,1),(S,1,-1)])
def shell(mask):
    vs=[]
    for angle in THETA:
        th=math.radians(angle);inset=.1+.8*(1-math.cos(th));z=1.03+.8*math.sin(th)
        vs.extend((x,y,z) for x,y in contour(mask,inset))
    n=len(vs)//len(THETA);fs=[]
    for j in range(len(THETA)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n
            if not on_join(vs[a],vs[b],mask):fs.append((a,b,b+n,a+n))
    fs.extend([tuple(reversed(range(n))),tuple((len(THETA)-1)*n+i for i in range(n))])
    o=mesh('Pressure_Shell',vs,fs,'Shell',True)
    # Matching normals at cut planes, including cuts next to a concave corner.
    normals=[]
    for v in o.data.vertices:
        x,y,z=v.co;normal=v.normal.copy()
        atx=any(mask&bit and abs(x-sign)<1e-5 for bit,sign in [(E,1),(W,-1)])
        aty=any(mask&bit and abs(y-sign)<1e-5 for bit,sign in [(N,1),(S,-1)])
        if atx or aty:
            sine=max(-1,min(1,(z-1.03)/.8));cosine=math.sqrt(max(0,1-sine*sine))
            normal=Vector((0 if atx else math.copysign(cosine,x),0 if aty else math.copysign(cosine,y),sine))
            if normal.length<1e-5:normal=Vector((0,0,1))
        normals.append(normal.normalized())
    o.data.normals_split_custom_set_from_vertices(normals)
    return o
def sidepoint(q,u,v,depth):
    x,y=u,-depth
    for _ in range(q):x,y=-y,x
    return (x,y,1.03+v)
def disc(name,q,r,depth,key):
    vs=[sidepoint(q,r*math.cos(i*math.pi/5),r*math.sin(i*math.pi/5),depth) for i in range(10)]
    return mesh(name,vs,[tuple(range(10))],key)
def window(q):
    vs=[sidepoint(q,r*math.cos(i*math.pi/5),r*math.sin(i*math.pi/5),dep) for r,dep in [(.20,.901),(.184,.951),(.146,.951),(.146,.929)] for i in range(10)]
    fs=[(j*10+i,j*10+(i+1)%10,(j+1)*10+(i+1)%10,(j+1)*10+i) for j in range(3) for i in range(10)]
    mesh('Porthole_Rim',vs,fs,'Rim')
    disc('Gas_Window',q,.146,.931,'Tint_Gas');disc('Window_Glass',q,.145,.953,'Glass')
    mesh('Glass_Glint',[sidepoint(q,u,v,.954) for u,v in [(-.09,.015),(-.075,.06),(-.005,.105),(.014,.09)]],[(0,1,2,3)],'Highlight')
    # A paired saddle foot on the exposed boundary, kept clear of the seam plane.
    def at(u,dep,z):
        p=sidepoint(q,u,0,dep);return(p[0],p[1],z)
    for u in [0]:
        size=(.16,.26,.46) if q%2==0 else (.26,.16,.46)
        box('Support',at(u,.60,.23),size,'Feet')
        size=(.26,.35,.07) if q%2==0 else (.35,.26,.07)
        box('Footplate',at(u,.60,.035),size,'Feet')

built={};stats=[]
for mask,related in sorted(groups.items()):
    objects=[];part,_=choose(mask)
    root=bpy.data.objects.new('Pressure_Module_%03d'%mask,None);assets.objects.link(root)
    root['tg_junction']=mask;root['source_states']=','.join('canister-'+str(m) for m in related);root['tile_m']=2.;root['volume_l_per_tile']=2500;root['max_pressure_kpa']=46000;root['anchored']=True
    body=shell(mask)
    for q,bit in enumerate([S,E,N,W]):
        if not mask&bit:window(q)
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for o in objects:o.select_set(True)
    filepath=OUT/('pressure_%03d.glb'%mask)
    with contextlib.redirect_stdout(io.StringIO()):
        bpy.ops.export_scene.gltf(filepath=str(filepath),export_format='GLB',use_selection=True,use_active_scene=True,export_extras=True,export_animations=False,export_texcoords=False,export_normals=True)
    vertices=sum(len(o.data.vertices) for o in objects);assert vertices<=500,(mask,vertices)
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    stats.append(dict(canonical=mask,states=related,part=part,vertices=vertices,triangles=triangles,file=filepath.name))
    built[mask]=(root,list(objects))
    # Library modules not in the presentation scene.
    for o in [root]+objects:o.hide_render=True;o.hide_set(True)

# Generate an exact-state catalog block; keep existing renderer and its rotations.
default={p:'pressure_%03d.glb'%m for p,m in [('single',0),('end',4),('straight',12),('corner',21),('cornerOpen',5),('tee',157),('center',255)]}
entries=[]
for stat in stats:
    parts={p:dict(file='station_props/stationary_tanks/rounded/'+f) for p,f in default.items()}
    parts[stat['part']]={'file':'station_props/stationary_tanks/rounded/'+stat['file']}
    entries.append(dict(id='stationary_tank' if stat['canonical']==0 else 'stationary_tank_j'+str(stat['canonical']),icons=['gags:/datum/greyscale_config/stationary_canister|*','icons/obj/pipes_n_cables/stationary_canisters.dmi'],states=['canister-'+str(s) for s in stat['states']],rule='smooth4',texture='model',flatColors=True,source=dict(width=2.0,height=1.83),fit=dict(width='tile',height=1.83),parts=parts))
(OUT/'catalog_entries.json').write_text(json.dumps(entries,indent=2))
(OUT/'model_info.json').write_text(json.dumps(dict(modules=stats,source_states=states,tile_m=2,height_m=1.83,source='tgstation/code/modules/atmospherics/machinery/components/tank.dm',runtime='DmInert: ports, gas visuals and cracks require simulation port; gas material is separate',gas_material='Pressure_Flat_Tint_Gas'),indent=2))

# Assembly examples use the same exported meshes and the actual smoothing rules.
review=bpy.data.collections.new('Pressure_Tank_Review');scene.collection.children.link(review)
def helper(name,data):
    o=bpy.data.objects.new(name,data);review.objects.link(o);return o
palette=[material('Preview_Gas_'+str(i),h) for i,h in enumerate(['77ADCF','AF77CB','DAA755','72AD89'])]
layouts=[('01  SINGLE',[(0,0)],(0,0)),('02  1 x 4',[(x,0) for x in range(4)],(4,0)),('03  2 x 3',[(x,y) for x in range(3) for y in range(2)],(0,4)),('04  L / INNER CORNER',[(0,0),(1,0),(2,0),(2,1),(2,2)],(8,4)),('05  T JUNCTION',[(-1,0),(0,0),(1,0),(0,1)],(2,11))]
assembly=[]
for li,(name,cells,offset) in enumerate(layouts):
    cellset=set(cells)
    for x,y in cells:
        mask=sum(bit for bit,dx,dy in [(E,1,0),(N,0,1),(W,-1,0),(S,0,-1)] if (x+dx,y+dy) in cellset)
        for bit,dx,dy,a,b in [(16,1,1,N,E),(128,-1,1,N,W),(32,1,-1,S,E),(64,-1,-1,S,W)]:
            if mask&a and mask&b and (x+dx,y+dy) in cellset:mask|=bit
        part,t=choose(mask);canonical=turn(mask,-t);original,obs=built[canonical]
        r=helper(name+'_'+str((x,y)),None);r.location=(offset[0]+2*x,offset[1]+2*y,0);r.rotation_euler.z=t*math.pi/2
        for src in obs:
            o=src.copy();review.objects.link(o);o.parent=r;o.hide_render=False;o.hide_set(False)
            if o.name.startswith('Gas_Window'):o.material_slots[0].link='OBJECT';o.material_slots[0].material=palette[li%4]
        assembly.append(dict(layout=name,cell=[x,y],junction=mask,canonical=canonical,turns=t))
    d=bpy.data.curves.new('Pressure_Label','FONT');d.body=name;d.size=.30;d.align_x='CENTER';d.materials.append(M['Highlight'])
    o=helper('Pressure_Label',d);o.location=(offset[0]+sum(x for x,y in cells)/len(cells)*2,offset[1]-1.5,.015)
(OUT/'assembly_examples.json').write_text(json.dumps(assembly,indent=2))
cam=helper('Pressure_Review_Camera',bpy.data.cameras.new('Pressure_Review_Camera'));scene.camera=cam
target=Vector((5.3,5.6,.6));cam.location=(19,-19,24);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=22
for name,loc,power,size in [('Key',(-5,-8,18),4400,12),('Fill',(16,0,12),3200,10),('Rim',(5,18,14),4800,10)]:
    o=helper(name,bpy.data.lights.new('Pressure_'+name,'AREA'));o.location=loc;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Pressure_Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.20,.26,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
me=bpy.data.meshes.new('Pressure_Floor');me.from_pydata([(-100,-100,-.012),(100,-100,-.012),(100,100,-.012),(-100,100,-.012)],[],[(0,1,2,3)]);me.materials.append(material('Pressure_Studio_Floor','252E3B'));helper('Pressure_Floor',me)
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1600;scene.render.resolution_y=1400;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'docs/ai/art/stationary_pressure_tanks_preview.png')
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_location=target;area.spaces.active.region_3d.view_distance=23;area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.shading.type='MATERIAL'
bpy.data.libraries.write(str(OUT/'stationary_pressure_tanks.blend'),{scene},fake_user=True,compress=True)
result=dict(modules=len(stats),states=len(states),max_vertices=max(s['vertices'] for s in stats),path=str(OUT))
