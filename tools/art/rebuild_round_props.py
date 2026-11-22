"""Reconstruct 13 props as volumes, with source-derived full-surface UVs.

Run through Blender MCP. Does not modify stationary tanks or source PNGs.
"""
from pathlib import Path
import bpy, bmesh, math, json, hashlib, shutil, datetime
from mathutils import Vector

ns=bpy.app.driver_namespace['station_props_build']
G=ns['G']; OUT=ns['OUT']; ROOT=ns['ROOT']
KEYS=['pacman_generator','disposal_unit']+[k for k,a in ns['ASSETS'].items() if a['category']=='tanks']
BACKUP=bpy.app.driver_namespace.get('round_rebuild',{}).get('BACKUP') or OUT/'backups'/('before_volume_rebuild_'+datetime.datetime.now().strftime('%Y%m%d_%H%M%S'))
BACKUP.mkdir(parents=True,exist_ok=True)
for key in KEYS:
    a=ns['ASSETS'][key]
    for ext in ['.blend','.glb']:
        p=OUT/a['category']/(key+ext)
        if not (BACKUP/p.name).exists():shutil.copy2(p,BACKUP/p.name)
for name in ['model_info.json','export_validation.json']:
    if not (BACKUP/name).exists():shutil.copy2(OUT/name,BACKUP/name)
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')

RECIPES={}; OLD={}; SHARED={}

def palette_texture(key,source,state,patches):
    """Nearest source samples; opaque padding only outside the source silhouette.

    Every cell is an independently unwrapped material surface, not a whole sprite.
    Mirror-repeat avoids a discontinuity on cylindrical wrap seams.
    """
    ns['select'](source,state)
    opaque=[(x,y,tuple(G.pixel(state,x,y))) for y in range(32) for x in range(32) if G.pixel(state,x,y)[3]>.99]
    def pix(x,y):
        c=G.pixel(state,x,y)
        if c[3]<.99: c=min(opaque,key=lambda p:(p[0]-x)**2+(p[1]-y)**2)[2]
        return tuple(c[:3])+(1,)
    pixels=[0.0]*(64*64*4)
    for slot in range(16):
        x0,y0,x1,y1=patches[slot%len(patches)]
        for y in range(16):
            for x in range(16):
                # Four reflected runs form a continuous circumference.
                wrap=('canister' in key or key in ['water_tank','fuel_tank']) and slot in [1,2,6]
                u=((x if x<8 else 15-x)/7) if wrap else x/15
                sx=min(x1-1,x0+int(u*(x1-x0)))
                sy=min(y1-1,y0+int(y*(y1-y0)/16))
                c=pix(sx,sy)
                if key=='disposal_unit' and slot==8 and 18<=sx<=24 and 18<=sy<=20:
                    c=pix(17,21)  # The physical lever supplies this detail.
                i=((63-(slot//4*16+y))*64+slot%4*16+x)*4
                pixels[i:i+4]=c
    im=bpy.data.images.new('Surface_Rebuild_'+key,width=64,height=64,alpha=True)
    im.pixels.foreach_set(pixels)
    path=OUT/'textures'/(key+'_surfaces.png')
    im.filepath_raw=str(path); im.file_format=G.enum(im,'file_format','PNG');im.save();im.pack()
    mat=bpy.data.materials.new('Surface_'+key);mat.use_nodes=True;mat.use_backface_culling=True
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.73;bs.inputs['Specular IOR Level'].default_value=.15
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;tex.interpolation=G.enum(tex,'interpolation','Closest')
    mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    mat['source_state']=state;mat['surface_recipe']=key
    ns['SOURCES']['rebuild_'+key]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    RECIPES[key]={'source':source,'state':state,'patches_xyxy':patches,'texture':str(path.relative_to(OUT)),'method':'nearest source sampling; reflected horizontal repeat; nearest opaque silhouette padding','slots':['metal','body','band','cap','vent','control','hazard','dark','panel','trim','label','hose','base','top','back','underside']}
    return mat

def uv(slot,u,v):
    # Half-pixel inset keeps nearest samples inside each 16px patch.
    return ((slot%4*16+.5+u*15)/64,1-(slot//4*16+.5+(1-v)*15)/64)

def face(m,points,slot=0,coords=None):
    coords=coords or [(0,0),(1,0),(1,1),(0,1)]
    m.face(points,[uv(slot,*p) for p in coords])

def add(m,name,pivot=(0,0,0)):
    a=ns['ASSETS'][CURRENT]; ob=m.object(CURRENT+'_'+name,a['scene'],MAT,pivot);ob.parent=a['root'];ob['part_name']=name
    a['parts'].append(ob);return ob

def box(name,b,slot=0,pivot=(0,0,0),slots=None):
    x0,x1,y0,y1,z0,z1=b
    p=[(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)]
    m=G.Mesh()
    for j,inds in enumerate([(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]): face(m,[p[i] for i in inds],slots[j] if slots else slot)
    return add(m,name,pivot)

def cylinder(name,layers,center=(0,0),slot=1,cap=3,n=16,pivot=(0,0,0)):
    """Closed lathed vessel with a continuous circumference UV strip."""
    m=G.Mesh();cx,cy=center
    rings=[[(cx+r*math.sin(i*2*math.pi/n),cy-r*math.cos(i*2*math.pi/n),z) for i in range(n)] for z,r in layers]
    for ring in [rings[0],rings[-1]]:
        r=max(math.hypot(p[0]-cx,p[1]-cy) for p in ring)
        face(m,ring,cap,[((p[0]-cx)/r/2+.5,(p[1]-cy)/r/2+.5) for p in ring])
    z0,z1=layers[0][0],layers[-1][0]
    for k in range(len(rings)-1):
        band=2 if abs(layers[k+1][0]-layers[k][0])<.0001 else slot
        for i in range(n):
            j=(i+1)%n
            points=[rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i]]
            coords=[(i/n,(layers[k][0]-z0)/(z1-z0)),((i+1)/n,(layers[k][0]-z0)/(z1-z0)),((i+1)/n,(layers[k+1][0]-z0)/(z1-z0)),(i/n,(layers[k+1][0]-z0)/(z1-z0))]
            if band==2 and abs(layers[k+1][0]-layers[k][0])<.0001:
                radius=max(layers[k][1],layers[k+1][1])
                coords=[((p[0]-cx)/radius/2+.5,(p[1]-cy)/radius/2+.5) for p in points]
            face(m,points,band,coords)
    return add(m,name,pivot)

def shell(name,plan,layers,slots):
    """Stepped extrusion; each ring has a scale and height."""
    m=G.Mesh();rings=[[(x*s,y*s,z) for x,y in plan] for z,s in layers]
    for ring in [rings[0],rings[-1]]:
        xs=[p[0] for p in ring];ys=[p[1] for p in ring]
        face(m,ring,3,[((p[0]-min(xs))/(max(xs)-min(xs)),(p[1]-min(ys))/(max(ys)-min(ys))) for p in ring])
    for k in range(len(rings)-1):
        for i in range(len(plan)):
            j=(i+1)%len(plan);face(m,[rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i]],slots[i%len(slots)])
    return add(m,name)

def start(key,patches):
    global CURRENT,MAT
    CURRENT=key;a=ns['ASSETS'][key];OLD[key]=list(a['parts']);a['parts']=[]
    bpy.context.window.scene=a['scene'];MAT=palette_texture(key,a['source'],a['state'],patches)

def finish(key):
    a=ns['ASSETS'][key]
    bpy.context.view_layer.update()
    independent={'Pressure_Knob','Valve_Block','Central_Access_Hatch','Starter_Grip','Flush_Lever','Service_Panel'}
    structural=[o for o in a['parts'] if o['part_name'] not in independent]
    ns['revision']['fuse'](ns,key,structural,'Continuous_Structural_Body')
    bpy.context.view_layer.update()
    ground=min((ob.matrix_world@Vector(c)).z for ob in a['parts'] for c in ob.bound_box)
    for ob in a['parts']:ob.location.z-=ground
    bpy.context.view_layer.update()
    for ob in a['parts']:
        signature=(tuple(tuple(round(c,6) for c in v.co) for v in ob.data.vertices),tuple(tuple(p.vertices) for p in ob.data.polygons),tuple(tuple(v.uv) for v in ob.data.uv_layers.active.data))
        if signature in SHARED:
            ob.data=SHARED[signature];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=MAT
        else:SHARED[signature]=ob.data
    # Replace previews only after all replacement parts exist. Preserve unrelated scenes.
    old_data={o.data for o in OLD[key]}
    for scene in list(bpy.data.scenes):
        if scene==a['scene']:continue
        matches=[o for o in scene.objects if o.type=='MESH' and o.data in old_data and (key in o.name)]
        if not matches:continue
        # Preview regeneration is explicit below; unlink stale copies.
        for ob in matches:scene.collection.objects.unlink(ob) if ob.name in scene.collection.objects else None
    for ob in OLD[key]:bpy.data.objects.remove(ob,do_unlink=True)
    a['description']='Volume reconstruction with full-surface source-derived pixel UVs'
    print('REBUILT',key,len(a['parts']),sum(len(o.data.loop_triangles) for o in a['parts']))

def build_canister(key):
    start(key,[(10,5,20,7),(10,19,22,29),(9,16,23,19),(10,8,22,10),(11,20,14,28),(12,11,21,14),(9,15,23,17),(13,3,19,5)])
    cylinder('Pressure_Vessel',[(.03,.21),(.08,.26),(.08,.28),(.19,.28),(.19,.31),(.63,.31),(.63,.29),(.71,.29),(.71,.25),(.76,.25)],slot=1)
    cylinder('Upper_Regulator',[(.72,.21),(.77,.21),(.77,.255),(.89,.255),(.89,.22),(.94,.22)],slot=0)
    cylinder('Waist_Collar',[(.65,.30),(.69,.30)],slot=2)
    # Real bridge handle, separate side mounting straps and pressure control.
    for s in [-1,1]:
        box('Handle_Upright_'+str(s),(s*.25-.025,s*.25+.025,-.045,.045,.88,1.13),7)
        box('Side_Strap_'+str(s),(s*.30-.025,s*.30+.025,-.065,.065,.34,.82),0)
    box('Carry_Handle',(-.275,.275,-.045,.045,1.10,1.15),7)
    box('Status_Panel',(-.14,.14,-.263,-.252,.80,.87),5)
    cylinder('Pressure_Knob',[(.94,.065),(1.00,.065)],center=(.10,-.09),slot=0,pivot=(.10,-.09,.94),n=8)
    finish(key)

def build_trolley(key):
    start(key,[(5,25,27,28),(6,9,14,24),(5,12,15,15),(7,4,13,8),(20,15,25,23),(19,19,25,24),(19,4,25,13),(3,26,29,28)])
    for s in [-1,1]:
        x=s*.20
        cylinder(('Primary' if s<0 else 'Secondary')+'_Vessel',[(.14,.13),(.18,.16),(.18,.175),(.69,.175),(.69,.15),(.81,.15),(.81,.11),(.85,.11)],center=(x,0),slot=1 if s<0 else 6,cap=3 if s<0 else 6)
        for z in [.31,.50]: cylinder('Steel_Band_'+str(s)+'_'+str(z),[(z,.182),(z+.035,.182)],center=(x,0),slot=2)
        box('Rear_Support_'+str(s),(x-.022,x+.022,.16,.20,.10,.73),0)
        box('Skid_'+str(s),(x-.07,x+.07,-.22,.24,.03,.10),7)
        box('Bottom_Rail_'+str(s),(-.39,.39,s*.18-.024,s*.18+.024,.10,.15),0)
    box('Valve_Block',(.135,.265,-.195,-.17,.61,.69),5,pivot=(.20,-.17,.65))
    finish(key)

def build_generator():
    key='pacman_generator'
    start(key,[(5,12,9,18),(5,13,11,19),(3,22,7,29),(7,5,13,7),(7,7,12,11),(18,15,22,19),(1,12,3,18),(12,24,16,28),(18,19,23,23),(24,9,27,16)])
    # Twin full-depth stepped engine jackets; the sprite's side projection is not a front decal.
    for s in [-1,1]:
        x=s*.265
        contour=[(-.19,.29),(.19,.29),(.19,.71),(.16,.71),(.16,.77),(.13,.77),(.13,.83),(-.13,.83),(-.13,.77),(-.16,.77),(-.16,.71),(-.19,.71)]
        jacket=G.Mesh()
        for y in [-.34,.34]:
            face(jacket,[(x+px,y,z) for px,z in contour],0,[((px+.19)/.38,(z-.29)/.54) for px,z in contour])
        for i,(px,z) in enumerate(contour):
            qx,qz=contour[(i+1)%len(contour)]
            face(jacket,[(x+px,-.34,z),(x+px,.34,z),(x+qx,.34,qz),(x+qx,-.34,qz)],9 if abs(qz-z)>.2 else 3)
        add(jacket,'Engine_Jacket_'+str(s))
        box('Upper_Vent_'+str(s),(x-.12,x+.12,-.355,-.34,.65,.79),4)
        box('Rear_Vent_'+str(s),(x-.12,x+.12,.339,.355,.52,.72),4)
        cylinder('Coil_Core_'+str(s),[(.03,.105),(.36,.105)],center=(x,-.16),slot=2,n=12)
        for i in range(4): cylinder('Coil_Fin_'+str(s)+'_'+str(i),[(.055+i*.07,.13),(.085+i*.07,.13)],center=(x,-.16),slot=2,n=12)
        box('Rear_Foot_'+str(s),(x-.11,x+.11,.14,.31,0,.31),7)
    box('Central_Engine_Block',(-.10,.10,-.27,.29,.22,.68),1)
    box('Central_Access_Hatch',(-.095,.095,-.295,-.27,.33,.61),5,pivot=(-.095,-.28,.33))
    box('Lower_Purple_Band',(-.455,.455,-.35,-.335,.29,.36),8)
    box('Starter_Mount',(-.49,-.455,-.11,.11,.43,.64),6)
    box('Starter_Grip',(-.54,-.49,-.12,.12,.58,.63),6,pivot=(-.49,0,.60))
    finish(key)

def build_disposal():
    key='disposal_unit'
    start(key,[(8,16,15,22),(9,16,15,22),(8,15,24,17),(9,17,15,21),(8,16,14,20),(19,18,24,20),(8,6,24,7),(14,8,20,10),(8,15,24,29),(10,20,15,24)])
    # One watertight shell includes underside, rim, sloping throat and cavity floor.
    m=G.Mesh();outer=[(-.32,-.30),(.32,-.30),(.32,.30),(-.32,.30)]
    inner=[(-.255,-.235),(.255,-.235),(.255,.235),(-.255,.235)]
    throat=[(-.18,-.16),(.18,-.16),(.18,.16),(-.18,.16)]
    face(m,[(*p,.02) for p in outer],0)
    face(m,[(*p,.43) for p in throat],7)
    for i in range(4):
        j=(i+1)%4
        face(m,[(*outer[i],.02),(*outer[j],.02),(*outer[j],.90),(*outer[i],.90)],8 if i==0 else 1)
        face(m,[(*outer[i],.90),(*outer[j],.90),(*inner[j],.90),(*inner[i],.90)],6)
        face(m,[(*inner[i],.90),(*inner[j],.90),(*throat[j],.43),(*throat[i],.43)],7)
    add(m,'Continuous_Hopper_Shell')
    box('Control_Housing',(-.28,.04,-.32,-.30,.38,.65),4)
    box('Flush_Lever',(.09,.28,-.355,-.31,.63,.69),5,pivot=(.09,-.32,.66))
    box('Service_Panel',(-.25,.25,.301,.315,.10,.42),0,pivot=(-.25,.30,.10))
    finish(key)

build_generator();build_disposal()
for key in KEYS:
    if key.startswith('portable_canister_'):build_canister(key)
    elif key in ['water_tank','fuel_tank']:build_trolley(key)
(OUT/'surface_rebuild_recipes.json').write_text(json.dumps(RECIPES,indent=2)+'\n')
bpy.app.driver_namespace['round_rebuild']=globals()
print('BUILT 13; pending visual review and export. Backup:',BACKUP)
