"""Flat-color 4x4 solar panel. Execute through Blender MCP, not standalone Python."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/power/solar_concept'
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Solar_Concept');bpy.context.window.scene=scene
scene.unit_settings.system='METRIC';scene.render.fps=30;scene.frame_start=1;scene.frame_end=151
asset=bpy.data.collections.new('Solar_Game_Asset');scene.collection.children.link(asset)
root=bpy.data.objects.new('Solar_Panel',None);asset.objects.link(root)
root['source_type']='/obj/machinery/power/solar';root['vertex_limit']=600
root['panel_width_m']=1.24;root['front']='Blender -Y / Godot +Z'
root['source_azimuth_at_rest_degrees']=180
parts=[];baseparts=[];headparts=[];cells=[];glints=[];cracks=[];mats={}

def lin(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def mat(name,color,rough=.8):
    m=bpy.data.materials.new('SolarConcept_'+name);m.use_nodes=True
    rgb=tuple(lin(int(color.lstrip('#')[i:i+2],16)/255) for i in [0,2,4]);m.diffuse_color=(*rgb,1)
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=rough;p.inputs['Specular IOR Level'].default_value=.18
    mats[name]=m;return m
mat('Ivory','#DED1CB');mat('Gray','#8F8583');mat('Graphite','#484952');mat('Blue','#164FD8',.48)
mat('Blue_Rim','#99B6FE');mat('Blue_Dark','#0C2B78');mat('Reflection','#E8F0FF',.65)
mat('Cyan','#62B5EB');mat('Plasma','#9D176E',.48);mat('Plasma_Rim','#D872BA');mat('Plasma_Dark','#4D104A')
mat('Titanium','#414743',.48);mat('Titanium_Rim','#B9BDB9');mat('Titanium_Dark','#242C29')
mat('Plastitanium','#151823',.48);mat('Plastitanium_Rim','#65666A');mat('Plastitanium_Dark','#0D1018')
mat('Wire','#688F62')

def mesh(name,vs,fs,material,parent=root,category='base'):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(mats[material]);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(name,me);asset.objects.link(o);o.parent=parent;parts.append(o)
    (baseparts if category=='base' else headparts).append(o)
    return o
def box(name,c,size,material,parent=root,category='base'):
    a,b,d=[s/2 for s in size];x,y,z=c
    return mesh(name,[(x+i*a,y+j*b,z+k*d) for k in [-1,1] for j in [-1,1] for i in [-1,1]],
        [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],material,parent,category)
def cylinder(name,levels,material,n=8,parent=root,category='base'):
    vs=[(r*math.cos(2*math.pi*i/n+math.pi/n),r*math.sin(2*math.pi*i/n+math.pi/n),z) for r,z in levels for i in range(n)]
    fs=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(levels)-1) for i in range(n)]
    fs.extend([tuple(reversed(range(n))),tuple((len(levels)-1)*n+i for i in range(n))])
    return mesh(name,vs,fs,material,parent,category)
def rect8(x,y,c,z):return [(-x+c,-y,z),(x-c,-y,z),(x,-y+c,z),(x,y-c,z),(x-c,y,z),(-x+c,y,z),(-x,y-c,z),(-x,-y+c,z)]
def rect4(x,y,z):return [(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
def face_materials(ob,materials,indices):
    ob.data.materials.clear()
    for name in materials:ob.data.materials.append(mats[name])
    for face,index in zip(ob.data.polygons,indices):face.material_index=index

# Three spokes, fixed to the floor; generous clearance beneath the rotating head.
hub=cylinder('Base_Octagonal_Hub',[(.127,.015),(.135,.038),(.135,.205),(.113,.235)],'Ivory')
face_materials(hub,['Ivory','Graphite'],[1]*8+[0]*16+[1,0])
cylinder('Base_Column',[(.065,.22),(.065,.62)],'Ivory')
cylinder('Base_Neck_Collar',[(.075,.61),(.075,.655)],'Graphite',n=6)
for i,ang in enumerate([210,330,90]):
    a=math.radians(ang);arm=box('Base_Spoke_%d'%(i+1),(0,0,.10),(.415,.085,.10),'Ivory')
    # Box's long X axis runs radially.
    arm.rotation_euler.z=a;arm.location=(.322*math.cos(a),.322*math.sin(a),0)
    lp=rect8(.065,.065,.017,.015)+rect8(.065,.065,.017,.07)+rect8(.053,.053,.014,.145)
    fs=[(j*8+k,j*8+(k+1)%8,(j+1)*8+(k+1)%8,(j+1)*8+k) for j in range(2) for k in range(8)]+[tuple(reversed(range(8))),tuple(range(16,24))]
    foot=mesh('Base_Foot_%d'%(i+1),lp,fs,'Gray');foot.location=(.55*math.cos(a),.55*math.sin(a),0)
    foot.rotation_euler.z=a
    face_materials(foot,['Ivory','Graphite'],[1]*8+[0]*8+[1,0])

yaw=bpy.data.objects.new('Panel_Azimuth_Pivot',None);asset.objects.link(yaw);yaw.parent=root;yaw.location=(0,0,.655)
yaw['axis']='local Z; both surface and edge rotate together; five-second source transition'
yaw['source_angle_mapping']='local rotation Z = radians(180 - source azimuth); rest faces south'
bearing=cylinder('Head_Azimuth_Bearing',[(.078,0),(.078,.065)],'Gray',n=8,parent=yaw,category='head')
tilt=bpy.data.objects.new('Panel_Tilt_Pivot',None);asset.objects.link(tilt);tilt.parent=yaw;tilt.location=(0,0,.025)
tilt.rotation_euler.x=math.radians(42)
tilt['axis']='local X; 42 degree concept mount, optional adjustment; source tilt is a 2.5D transform'
joint=cylinder('Head_Hinge_Axle',[(.072,-.135),(.072,.135)],'Ivory',n=8,parent=tilt,category='head')
joint.rotation_euler.y=math.pi/2
face_materials(joint,['Ivory','Gray'],[0]*8+[1,1])
box('Head_Rear_Saddle',(0,.07,.09),(.22,.16,.17),'Gray',tilt,'head')
board_parts_start=len(headparts)

# The border and backplate are solid. The 16 raised beveled cells sit on it.
loops=[rect4(.62,.62,-.035),rect4(.62,.62,.023),rect4(.608,.608,.035),rect4(.55,.55,.035),rect4(.55,.55,-.035)]
vs=[v for loop in loops for v in loop];fs=[]
for j in range(5):
    for k in range(4):fs.append((j*4+k,j*4+(k+1)%4,((j+1)%5)*4+(k+1)%4,((j+1)%5)*4+k))
frame=mesh('Head_Panel_Edge',vs,fs,'Blue_Rim',tilt,'head');frame['source_layer']='solar_panel_<material>_edge'
face_materials(frame,['Blue_Rim','Blue_Dark'],[1]*4+[0]*8+[1]*8)
back=box('Head_Backplate',(0,0,-.021),(1.10,1.10,.026),'Graphite',tilt,'head')
grid=mesh('Head_Grid_Backing',[(-.55,-.55,.026),(.55,-.55,.026),(.55,.55,.026),(-.55,.55,.026)],[(0,1,2,3)],'Blue_Rim',tilt,'head')
for row in range(4):
    for column in range(4):
        x=-.411+column*.274;y=-.411+row*.274;w=.253;h=.253;b=.012
        bottom=[(x-w/2,y-h/2,.028),(x+w/2,y-h/2,.028),(x+w/2,y+h/2,.028),(x-w/2,y+h/2,.028)]
        top=[(x-w/2+b,y-h/2+b,.050),(x+w/2-b,y-h/2+b,.050),(x+w/2-b,y+h/2-b,.050),(x-w/2+b,y+h/2-b,.050)]
        ob=mesh('Head_Cell_%d_%d'%(row+1,column+1),bottom+top,[(i,(i+1)%4,4+(i+1)%4,4+i) for i in range(4)]+[(4,5,6,7)],'Blue',tilt,'head')
        ob['source_layer']='solar_panel_<material>';cells.append(ob)
        face_materials(ob,['Blue','Blue_Dark'],[1]*4+[0])
        # Four square steps sharing corner vertices, like the pixel concept.
        vs=[];fs=[];lookup={}
        for j in range(4):
            polygon=[]
            for xx,yy in [(j,j),(j+1,j),(j+1,j+1),(j,j+1)]:
                if (xx,yy) not in lookup:lookup[xx,yy]=len(vs);vs.append((x-.056+xx*.028,y-.056+yy*.028,.0503))
                polygon.append(lookup[xx,yy])
            fs.append(tuple(polygon))
        gl=mesh('Head_Reflection_%d_%d'%(row+1,column+1),vs,fs,'Reflection' if (row+column)%3 else 'Cyan',tilt,'head');glints.append(gl)

# Broken overlay: angular open cracks and exposed conductors, not a glow effect.
vs=[];fs=[]
for x,y,dx,dy in [(-.28,.28,.15,-.13),(-.15,.15,-.10,-.22),(-.25,-.07,.14,-.12),(.20,.32,.12,-.18),(.30,.14,-.09,-.20),(.21,-.06,.17,-.13),(-.40,-.31,.16,.06),(.09,-.34,.10,.12)]:
    n=len(vs);vs.extend([(x-.004,y,.0505),(x+.004,y,.0505),(x+dx,y+dy,.0505)]);fs.append((n,n+1,n+2))
damage=mesh('Head_Broken_Cracks',vs,fs,'Graphite',tilt,'head');cracks.append(damage)
wire=mesh('Head_Exposed_Conductors',[(-.44,-.42,.03),(-.32,-.42,.03),(-.32,-.39,.03),(-.41,-.39,.03),(-.41,-.30,.03),(-.44,-.30,.03),(.12,.07,.03),(.15,.07,.03),(.15,.20,.03),(.20,.20,.03),(.20,.23,.03),(.12,.23,.03)],[(0,1,2,3,4,5),(6,7,8,9,10,11)],'Wire',tilt,'head');cracks.append(wire)
for ob in headparts[board_parts_start:]:
    for v in ob.data.vertices:v.co+=Vector((0,.46+.1*math.sin(math.radians(42)),.12+.1*math.cos(math.radians(42))))
for ob in cracks:ob.visible_shadow=False

skins={'glass':('Blue','Blue_Rim',1),'plasmaglass':('Plasma','Plasma_Rim',3),'titanium_glass':('Titanium','Titanium_Rim',2),'plastitanium_glass':('Plastitanium','Plastitanium_Rim',4)}
def assign(ob,material):ob.data.materials[0]=mats[material]
def state(skin='glass',broken=False,bare=False):
    primary,rim,tier=skins[skin]
    root['skin']=skin;root['broken']=broken;root['bare_support']=bare;root['power_tier']=tier;root['nominal_output_watts']=2500*tier
    for o in headparts:o.hide_render=bare;o.hide_set(bare)
    dark=primary+'_Dark' if primary!='Blue' else 'Blue_Dark'
    for o in cells:face_materials(o,[primary,dark],[1]*4+[0])
    face_materials(frame,[rim,dark],[1]*4+[0]*8+[1]*8)
    assign(grid,'Graphite' if broken else rim)
    for o in cracks:o.hide_render=not broken or bare;o.hide_set(not broken or bare)
    for i,(o,g) in enumerate(zip(cells,glints)):
        missing=broken and i in [0,10]
        o.hide_render=bare or missing;o.hide_set(bare or missing)
        g.hide_render=bare or missing or (broken and i in [1,5,11,14]);g.hide_set(g.hide_render)
        o.rotation_euler=(0,0,0);g.rotation_euler=(0,0,0)
    yaw.rotation_euler.z=0;tilt.rotation_euler.x=math.radians(42)

def clip(name,start,end):
    yaw.animation_data_create()
    for f,deg in [(1,start),(76,(start+end)/2),(151,end)]:
        yaw.rotation_euler.z=math.radians(deg);yaw.keyframe_insert(data_path='rotation_euler',frame=f)
    ac=yaw.animation_data.action;ac.name=name
    for la in ac.layers:
        for st in la.strips:
            for cb in st.channelbags:
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points:kp.interpolation='BEZIER';kp.handle_left_type='AUTO_CLAMPED';kp.handle_right_type='AUTO_CLAMPED'
    tr=yaw.animation_data.nla_tracks.new();tr.name=name;strip=tr.strips.new(name,1,ac);strip.action_slot=yaw.animation_data.action_slot;tr.mute=True;yaw.animation_data.action=None
clip('Track_Quarter_Turn_5s',0,90);clip('Track_Reverse_Turn_5s',90,0);clip('Break_Reorient_5s',90,0)
scene.frame_set(1);state()
vertices=sum(len(o.data.vertices) for o in parts);assert vertices<=600,vertices
root['mesh_vertices']=vertices

# Preview studio is excluded from every exported GLB.
studio=bpy.data.collections.new('Solar_Preview_Only');scene.collection.children.link(studio)
def studio_obj(name,data):o=bpy.data.objects.new(name,data);studio.objects.link(o);return o
cam=studio_obj('Solar_Review_Camera',bpy.data.cameras.new('Solar_Review_Camera'));cam.data.type='ORTHO';cam.data.ortho_scale=2.35;scene.camera=cam
def camera_at(pos,target=(0,.10,.84)):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
camera_at((1.65,-2.8,2.05))
scene.world=bpy.data.worlds.new('Solar_Studio_World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.22,.26,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
for name,pos,power,size in [('Key',(-3,-4,5),400,4),('Fill',(3,-1,3),180,3),('Rim',(0,3,4),350,3)]:
    l=studio_obj('Solar_Studio_'+name,bpy.data.lights.new('Solar_Studio_'+name,'AREA'));l.location=pos;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,.6))-l.location).to_track_quat('-Z','Y').to_euler()
floor_mesh=bpy.data.meshes.new('Solar_Studio_Floor');floor_mesh.from_pydata([(-200,-200,-.006),(200,-200,-.006),(200,200,-.006),(-200,200,-.006)],[],[(0,1,2,3)])
floor=studio_obj('Solar_Studio_Floor',floor_mesh);fm=mat('Studio_Backdrop','#30383F');floor.data.materials.append(fm)
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.view_settings.view_transform='Standard';scene.view_settings.exposure=-.70
scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
for o in studio.objects:o.hide_set(True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='SOLID';area.spaces.active.shading.color_type='MATERIAL';area.spaces.active.region_3d.view_distance=2.4
        area.spaces.active.region_3d.view_location=(0,0,.65);area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.overlay.show_floor=False;area.spaces.active.overlay.show_extras=False

def export(name,bare=False):
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in [root,yaw,tilt]+parts:
        if o.type!='MESH' or (not o.hide_render and (not bare or o in baseparts)):o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,use_active_scene=True,
        export_normals=False,export_texcoords=False,export_shared_accessors=True,export_animations=not bare,export_animation_mode='ACTIONS',export_force_sampling=False,
        export_extras=True,export_cameras=False,export_lights=False)
    # Write only this scene and dependencies, preserving the existing SMES file.
    bpy.data.libraries.write(str(OUT/(name+'.blend')),{scene},fake_user=True)

bpy.context.view_layer.update()
base_x=[(o.matrix_world@v.co).x for o in baseparts for v in o.data.vertices]
report={'vertices_all_parts':vertices,'limit':600,'parts':{o.name:len(o.data.vertices) for o in parts},'panel_size_m':[1.24,1.24],
    'base_span_x_m':max(base_x)-min(base_x),'panel_overhang_each_side_m':.62-max(base_x),
    'emission':False,'game_lights':0,'source_turn_seconds':5,'tracking_mount_tilt_degrees':42,
    'source_note':'No animated sprite frames. Motion uses animate() on surface and edge together; tracker is a separate device.'}
(OUT/'model_info.json').write_text(json.dumps(report,indent=2))
bpy.app.driver_namespace['solar_concept_build']=globals().copy()
result={'vertices':vertices,'overhang_each_side_m':report['panel_overhang_each_side_m'],'scene':scene.name}
