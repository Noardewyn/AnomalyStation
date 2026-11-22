"""Run through Blender MCP. Flat-color concept SMES, <=600 mesh vertices."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'godot/assets/models/machines/smes'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('SMES_Concept_Asset')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 120
col = bpy.data.collections.new('SMES_Game_Asset')
scene.collection.children.link(col)
parts = []

def linear(v):
    return v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4

palette = ['EFEFEF','BBBBBB','888888','444444','33CC33','CC3333']
mats = []
for name,h in zip(['Ivory','LightGray','MidGray','Graphite','Green','Red'],palette):
    m=bpy.data.materials.new('SMES_'+name+'_'+h)
    m.use_nodes=True
    rgb=tuple(linear(int(h[i:i+2],16)/255) for i in (0,2,4))
    m.diffuse_color=(*rgb,1)
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=.88
    p.inputs['Specular IOR Level'].default_value=.15
    mats.append(m)

root=bpy.data.objects.new('SMES',None); col.objects.link(root)
root['source_type']='/obj/machinery/power/smes'
root['palette_hex']=', '.join('#'+h for h in palette)
root['vertex_limit']=600
root['front']='Blender -Y / Godot +Z; origin at floor center'

def mesh(name,vs,fs,mi=0,parent=root,face_mats=None):
    me=bpy.data.meshes.new(name); me.from_pydata(vs,[],fs); me.update()
    for m in mats: me.materials.append(m)
    if face_mats:
        for p,i in zip(me.polygons,face_mats): p.material_index=i
    else:
        for p in me.polygons:p.material_index=mi
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);ob.parent=parent;parts.append(ob)
    return ob

def box(name,center,size,mi=0,parent=root):
    x,y,z=center;a,b,c=[v/2 for v in size]
    return mesh(name,[(x+i*a,y+j*b,z+k*c) for k in [-1,1] for j in [-1,1] for i in [-1,1]],
        [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mi,parent)

def outline(r,a):return [(-a,-r),(a,-r),(r,-a),(r,a),(a,r),(-a,r),(-r,a),(-r,-a)]

def loft(name,levels,mi=0,cap=True,skipfront=False,side_mats=None):
    vs=[(x,y,z) for r,a,z in levels for x,y in outline(r,a)]
    fs=[];fm=[]
    for j in range(len(levels)-1):
        for i in range(8):
            if skipfront and i==0:continue
            fs.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i));fm.append(side_mats[j] if side_mats else mi)
    if cap:fs.extend([tuple(reversed(range(8))),tuple((len(levels)-1)*8+i for i in range(8))]);fm.extend([mi,mi])
    return mesh(name,vs,fs,mi,face_mats=fm)

# Shell has a real front opening and a lined service cavity.
loft('Octagonal_Core',[(.451,.251,.11),(.451,.251,.89)],3,cap=True,skipfront=True)
loft('Beveled_Foot',[(.443,.25,0),(.463,.259,.04),(.463,.259,.135)],3)
for i,z in enumerate([.18,.32,.46,.60,.74]):
    # Closed U section; the front interruption clears the service door.
    paths=[[(x,y,z) for x,y in outline(.451,.251)],
           [(x,y,z) for x,y in outline(.49,.27)],
           [(x,y,z+.071) for x,y in outline(.49,.27)],
           [(x,y,z+.071) for x,y in outline(.451,.251)]]
    vs=[p for loop in paths for p in loop];fs=[]
    for j in range(4):
        for k in range(1,8):fs.append((j*8+k,j*8+(k+1)%8,((j+1)%4)*8+(k+1)%8,((j+1)%4)*8+k))
    fs.extend([(0,8,16,24),(1,25,17,9)])
    mesh(f'Cooling_Rib_{i+1:02}',vs,fs,0)
loft('Recessed_Top_Rim',[(.451,.251,.87),(.49,.27,.885),(.49,.27,.973),(.405,.223,.973),(.405,.223,.922)],0,cap=False,side_mats=[1,0,0,2])
loft('Top_Well',[(.405,.223,.917),(.405,.223,.922)],2)
loft('Top_Octagonal_Plug',[(.153,.084,.922),(.153,.084,.939),(.133,.074,.956)],1,side_mats=[2,0])

# Rectangular service cavity: outer edge, inner lip, back wall.
def rect_loop(x,z0,z1,y):return [(-x,y,z0),(x,y,z0),(x,y,z1),(-x,y,z1)]
loops=[rect_loop(.259,.133,.862,-.463),rect_loop(.221,.175,.819,-.466),rect_loop(.221,.175,.819,-.352)]
vs=[v for lp in loops for v in lp]
fs=[(j*4+k,j*4+(k+1)%4,(j+1)*4+(k+1)%4,(j+1)*4+k) for j in range(2) for k in range(4)]
fs.append((8,9,10,11))
mesh('Service_Cavity_Frame',vs,fs,1,face_mats=[0]*4+[3]*5)
box('Internal_Control_Board',(0,-.361,.56),(.37,.012,.26),2)
for i,z in enumerate([.49,.56,.63]):box(f'Internal_Bus_Rail_{i+1}',(0,-.377,z),(.33,.018,.025),1)
for i,x in enumerate([-.105,.105]):
    ob=loft('Internal_Cell_'+str(i+1),[(.049,.024,.215),(.049,.024,.39)],2)
    for v in ob.data.vertices:v.co+=Vector((x,-.397,0))
box('Internal_Terminal_Block',(0,-.396,.765),(.31,.03,.047),1)
for i,x in enumerate([-.11,0,.11]):box('Terminal_Contact_'+str(i+1),(x,-.416,.765),(.025,.012,.025),3)
for i,z in enumerate([.245,.745]):box('Door_Hinge_Mount_'+str(i+1),(.257,-.472,z),(.026,.04,.064),2)

hinge=bpy.data.objects.new('Service_Door_Hinge',None);col.objects.link(hinge);hinge.parent=root
hinge.location=(.258,-.48,.49);hinge['open_degrees']=112.0;hinge['source_state']='smes-o'
doorparts=[]
door=box('Service_Door',(0,-.481,.496),(.51,.03,.714),0);doorparts.append(door)
# Raised bevel around inset controls: precise 16 vertices.
dl=[rect_loop(.176,.255,.723,-.503),rect_loop(.156,.275,.703,-.516)]
mesh_ob=mesh('Control_Recess_Bevel',[v for p in dl for v in p],[(i,(i+1)%4,4+(i+1)%4,4+i) for i in range(4)],1)
doorparts.append(mesh_ob)
doorparts.append(box('Control_Recess',(0,-.505,.489),(.31,.012,.428),1))
doorparts.append(box('Charge_Gauge_Bezel',(-.076,-.518,.489),(.112,.016,.362),3))
for i,z in enumerate([.35,.418,.486,.554,.622]):
    ob=mesh(f'Charge_Level_{i+1}',[(-.111,-.533,z-.026),(-.041,-.533,z-.026),(-.041,-.533,z+.026),(-.111,-.533,z+.026)],[(0,1,2,3)],5 if i<3 else 4)
    ob['source_overlay']='smes-og'+str(i+1);doorparts.append(ob)
for label,z,mi in [('Upper_Green',.585,4),('Lower_Red',.394,5)]:
    doorparts.append(box(label+'_Socket',(.087,-.522,z),(.114,.025,.114),0))
    doorparts.append(box(label+'_Lens',(.087,-.541,z),(.075,.018,.075),mi))
    ob=mesh(label+'_Output_Highlight',[(.048,-.552,z-.039),(.126,-.552,z-.039),(.126,-.552,z+.039),(.048,-.552,z+.039),(.054,-.553,z-.033),(.12,-.553,z-.033),(.12,-.553,z+.033),(.054,-.553,z+.033)],[(i,(i+1)%4,4+(i+1)%4,4+i) for i in range(4)],0)
    doorparts.append(ob)
    # White source-oc pulse, mapped to the concept's permitted palette.
    pulse=mesh(label+'_Input_Pulse',[(.037,-.554,z-.05),(.137,-.554,z-.05),(.137,-.554,z+.05),(.037,-.554,z+.05),(.05,-.555,z-.037),(.124,-.555,z-.037),(.124,-.555,z+.037),(.05,-.555,z+.037)],[(i,(i+1)%4,4+(i+1)%4,4+i) for i in range(4)],0)
    doorparts.append(pulse)
for i,(x,z) in enumerate([(-.213,.185),(.213,.185),(-.213,.805),(.213,.805)]):
    doorparts.append(box(f'Captive_Screw_{i+1}',(x,-.502,z),(.024,.013,.024),3))
for ob in doorparts:
    ob.parent=hinge
    for v in ob.data.vertices:v.co-=hinge.location

# Emissive surfaces preserve the same six palette colors. Three punctual
# lights provide actual spill in Blender and in glTF/Godot, without vertices.
glow={}
for name,index in [('Green',4),('Red',5),('Ivory',0)]:
    m=mats[index].copy();m.name='SMES_'+name+'_Emit'
    shader=m.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Emission Color'].default_value=m.diffuse_color
    shader.inputs['Emission Strength'].default_value=.8
    glow[name]=m
for ob in parts:
    if ob.name.startswith('Charge_Level_'):
        mat=glow['Red' if int(ob.name[-1])<4 else 'Green']
    elif ob.name.endswith('_Input_Pulse'):mat=glow['Ivory']
    elif ob.name.endswith('_Output_Highlight'):mat=glow['Green' if ob.name.startswith('Upper') else 'Red']
    else:continue
    ob.data.materials.append(mat)
    for p in ob.data.polygons:p.material_index=len(ob.data.materials)-1
indicator_lights={}
for label,x,z in [('Upper_Green',.087,.585),('Lower_Red',.087,.394),('Gauge',-.076,.49)]:
    data=bpy.data.lights.new('SMES_'+label+'_Spill','POINT');data.energy=.55;data.shadow_soft_size=.055
    data.cutoff_distance=.9;data.use_custom_distance=True
    ob=bpy.data.objects.new(data.name,data);col.objects.link(ob);ob.parent=hinge
    ob.location=Vector((x,-.595,z))-hinge.location
    indicator_lights[label]=ob

def set_state(level=5,output=False,input=True,panel=False,operational=True,lights=True,phase=True):
    active=not panel and operational and lights
    hinge.rotation_euler.z=math.radians(112 if panel else 0)
    for i in range(1,6):
        ob=bpy.data.objects[f'Charge_Level_{i}'];ob.scale=(1,1,1) if active and i<=level and (level!=1 or phase) else (.001,.001,.001)
    for label in ['Upper_Green','Lower_Red']:
        ob=bpy.data.objects[label+'_Output_Highlight'];ob.scale=(1,1,1) if active and ((label=='Upper_Green')==output) else (.001,.001,.001)
        ob=bpy.data.objects[label+'_Input_Pulse'];ob.scale=(1,1,1) if active and phase and (not input or label=='Lower_Red') else (.001,.001,.001)
        pulse=active and phase and (not input or label=='Lower_Red')
        light=indicator_lights[label].data
        light.energy=.55 if active and (pulse or ((label=='Upper_Green')==output)) else 0
        light.color=glow['Ivory' if pulse else 'Green' if label=='Upper_Green' else 'Red'].diffuse_color[:3]
    indicator_lights['Gauge'].data.energy=.35 if active and level>0 and (level!=1 or phase) else 0
    indicator_lights['Gauge'].data.color=mats[4 if level>=4 else 5].diffuse_color[:3]

set_state(5,False,True,phase=False)
vertices=sum(len(o.data.vertices) for o in parts)
assert vertices<=600,vertices
root['mesh_vertices']=vertices
root['charge_level']=5;root['inputting']=True;root['outputting']=False

# Exportable actions: door movement and the actual DMI overlay cadence.
def clip(ob,name,path,values,interpolation='CONSTANT'):
    ob.animation_data_create()
    for f,value in values:
        setattr(ob,path,value);ob.keyframe_insert(data_path=path,frame=f)
    action=ob.animation_data.action;action.name=name
    for layer in action.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points:kp.interpolation=interpolation
    track=ob.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,int(action.frame_range[0]),action);strip.action_slot=ob.animation_data.action_slot
    track.mute=True;ob.animation_data.action=None

clip(hinge,'Panel_Open','rotation_euler',[(1,(0,0,0)),(16,(0,0,math.radians(112)))],'LINEAR')
clip(hinge,'Panel_Close','rotation_euler',[(1,(0,0,math.radians(112))),(16,(0,0,0))],'LINEAR')
clip(bpy.data.objects['Charge_Level_1'],'Low_Charge_Blink','scale',[(1,(1,1,1)),(31,(.001,.001,.001)),(61,(1,1,1))])
for label in ['Upper_Green','Lower_Red']:
    clip(bpy.data.objects[label+'_Input_Pulse'],'Input_Pulse_'+label,'scale',[(1,(1,1,1)),(16,(.001,.001,.001)),(31,(1,1,1))])
scene.frame_set(1);set_state(5,False,True,phase=False)

# Viewport and review studio live in a separate scene, outside game export.
scene.world=bpy.data.worlds.new('SMES_Studio_World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.24,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
scene.view_settings.view_transform='Standard'
scene.view_settings.exposure=-.65
scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
studio=bpy.data.collections.new('SMES_Preview_Only');scene.collection.children.link(studio)
def studio_object(name,data):
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);return ob
cam=studio_object('SMES_Review_Camera',bpy.data.cameras.new('SMES_Review_Camera'));scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=1.57
def camera_at(loc,target=(0,0,.48)):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
camera_at((1.65,-2.9,1.85))
for name,loc,energy,size in [('Key',(-2,-3,4),300,4),('Fill',(3,-1,2.5),160,3),('Rim',(0,3,3),250,3)]:
    light=studio_object('SMES_'+name,bpy.data.lights.new('SMES_'+name,'AREA'));light.location=loc
    light.data.energy=energy;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
floor_mesh=bpy.data.meshes.new('SMES_Studio_Floor');floor_mesh.from_pydata([(-200,-200,-.009),(200,-200,-.009),(200,200,-.009),(-200,200,-.009)],[],[(0,1,2,3)])
floor=studio_object('SMES_Studio_Floor',floor_mesh)
floor_mat=bpy.data.materials.new('SMES_Studio_Backdrop');floor_mat.use_nodes=True
floor_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.065,.072,.085,1)
floor_mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.95
floor_mesh.materials.append(floor_mat)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='MATERIAL'
        area.spaces.active.region_3d.view_distance=1.9
        area.spaces.active.region_3d.view_location=(0,0,.48)
        area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
        area.spaces.active.overlay.show_floor=False
        area.spaces.active.overlay.show_extras=False
for ob in bpy.context.selected_objects:ob.select_set(False)
door.select_set(True);bpy.context.view_layer.objects.active=door
for o in studio.objects:o.hide_set(True)

def export(name):
    for o in bpy.context.selected_objects:o.select_set(False)
    root.select_set(True);hinge.select_set(True)
    for o in parts:o.select_set(True)
    for o in indicator_lights.values():o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,
        export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=False,
        use_active_scene=True,export_normals=False,export_extras=True,export_texcoords=False,
        export_materials='EXPORT',export_lights=True,export_cameras=False)
export('smes')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'smes.blend'))

# Keep helpers available for subsequent MCP review and state exports.
bpy.app.driver_namespace['smes_build'] = globals().copy()
(OUT/'model_info.json').write_text(json.dumps(dict(vertices=vertices,limit=600,dimensions_m=[.98,.98,.973],palette=palette,
    parts={o.name:len(o.data.vertices) for o in parts},source_animations={'smes-og1':[1,1],'smes-oc0':[.5,.5],'smes-oc1':[.5,.5]},
    adaptation='Only concept palette. Source amber oc flashes become ivory. Gauge uses three red and two green segments. Door swing is a 3D interpretation of static smes-o.'),indent=2))
result={'file':str(OUT/'smes.blend'),'vertices':vertices,'mesh_parts':len(parts),'scene':scene.name}
