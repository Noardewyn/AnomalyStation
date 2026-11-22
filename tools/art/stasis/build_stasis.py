import bpy, math, os, json
from mathutils import Vector
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../godot/assets/models/machines/stasis')
os.makedirs(out, exist_ok=True)
scene = bpy.data.scenes.new("Stasis_Capsule_Review")
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
asset = bpy.data.collections.new("Stasis_Asset")
scene.collection.children.link(asset)
studio = bpy.data.collections.new("Stasis_Studio")
scene.collection.children.link(studio)
root = bpy.data.objects.new("Stasis_Root", None)
asset.objects.link(root)
root["head_direction"] = "+Y"
root["state"] = "normal"
root["notes"] = "Mattress emission is independent of patient occupancy. Maintenance panels are separate objects."
def mat(name, color, metal=0, emission=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal
    p.inputs['Roughness'].default_value=.48
    p.inputs['Emission Color'].default_value=(*color,1)
    p.inputs['Emission Strength'].default_value=emission
    return m
dark=mat("Stasis_Graphite",(.035,.043,.059),.2)
silver=mat("Stasis_Silver",(.58,.63,.70),.35)
white=mat("Stasis_Head_Ivory",(.82,.85,.9),.12)
teal=mat("Stasis_Mattress_Emission",(.012,.24,.22))
red=mat("Stasis_Status_Red",(.8,.008,.018),0,1)
inner=mat("Stasis_Service_Interior",(.015,.022,.03))
def finish(o, name, material, collection=asset):
    o.name=name
    for c in list(o.users_collection): c.objects.unlink(o)
    collection.objects.link(o)
    o.data.materials.append(material)
    if collection==asset: o.parent=root
    return o
def box(name,loc,size,material,bevel=0,rot=0,collection=asset):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=finish(bpy.context.object,name,material,collection)
    o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new("Single_edge_chamfer",'BEVEL');m.width=bevel;m.segments=1
        bpy.ops.object.modifier_apply(modifier=m.name)
    o.rotation_euler.x=rot
    return o
def prism(name, poly, low, high, material):
    n=len(poly); verts=[(x,y,z) for z in [low,high] for x,y in poly]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);asset.objects.link(o);o.parent=root;o.data.materials.append(material)
    return o
# Length 2.35m, body width .88m, widened head 1.08m.
outline=[(-.44,-1.12),(.44,-1.12),(.44,.48),(.54,.60),(.54,1.08),(-.54,1.08),(-.54,.60),(-.44,.48)]
prism("Stasis_Mattress_Deck",outline,.55,.64,dark)
for side in [-1,1]:
    poly=[(side*.44,-1.12),(side*.44,.48),(side*.54,.60),(side*.54,1.08),(side*.46,1.08),(side*.46,.63),(side*.36,.51),(side*.36,-1.12)]
    if side==1: poly.reverse()
    prism("Stasis_Level_Rail_"+str(side),poly,.73,.82,silver)
    poly2=[(side*.435,-1.08),(side*.435,.48),(side*.535,.60),(side*.535,1.08),(side*.465,1.08),(side*.465,.63),(side*.365,.51),(side*.365,-1.08)]
    if side==1: poly2.reverse()
    prism("Stasis_Continuous_Fascia_"+str(side),poly2,.48,.73,dark)
    for y,x,h in [(-1.03,.35,.64),(1.04,.46,.85)]:
        box("Stasis_Leg", (side*x,y,h/2),(.16,.19,h),dark,.012)
    box("Stasis_Side_Red_"+str(side),(side*.439,-.17,.59),(.008,.22,.045),red)
    panel=box("Stasis_Service_Panel_"+str(side),(side*.44,.12,.595),(.012,.51,.21),dark)
    panel["open_rotation_axis"]="Y"
    # outward red indicator remains visible in normal state
    box("Stasis_Pedestal_Red_"+str(side),(side*.545,1.04,.70),(.008,.045,.13),red)
# front rim and dark teal inset
box("Stasis_Foot_Crossbar",(0,-1.12,.775),(.88,.08,.09),silver,.008)
box("Stasis_Foot_Lower_Trim",(0,-1.124,.53),(.88,.08,.06),silver)
for x in [-.41,.41]: box("Stasis_Foot_Trim",(x,-1.124,.65),(.06,.08,.24),silver)
box("Stasis_Foot_Inset",(0,-1.13,.645),(.73,.024,.16),teal)
# five regular pads and one wider head pad
for i in range(5):
    box("Stasis_Mattress_Pad_%02d"%i,(0,-.917+i*.302,.707),(.705,.28,.13),teal,.018)
box("Stasis_Mattress_Head",(0,.815,.707),(.90,.45,.13),teal,.018)
angle=math.radians(-20)
box("Stasis_Head_Backplate",(0,1.135,1.075),(1.00,.12,.57),dark,.015,angle)
box("Stasis_Head_White_Plate",(0,1.065,1.10),(.91,.07,.50),white,.015,angle)
# integration anchors
for name,loc in [("Patient_Anchor",(0,0,.79)),("Head_Anchor",(0,.8,.79))]:
    o=bpy.data.objects.new(name,None);asset.objects.link(o);o.parent=root;o.location=loc;o.empty_display_size=.06
root["powered"]=False
root["broken"]=False
# material driver, independent of occupancy
p=teal.node_tree.nodes.get('Principled BSDF')
f=p.inputs['Emission Strength'].driver_add('default_value')
d=f.driver;d.expression="1.8 * powered * (1 - broken)"
for name in ["powered","broken"]:
    v=d.variables.new();v.name=name;v.targets[0].id=root;v.targets[0].data_path='["'+name+'"]'
floor=mat("Stasis_Studio_Floor",(.24,.265,.30))
box("Stasis_Studio_Floor",(0,0,-.045),(200,200,.08),floor,collection=studio)
world=bpy.data.worlds.new("Stasis_Studio_World");world.use_nodes=True
world.node_tree.nodes["Background"].inputs[0].default_value=(.45,.50,.60,1)
world.node_tree.nodes["Background"].inputs[1].default_value=.4
scene.world=world
for name,loc,power,size in [("Key",(-3,-4,6),650,4),("Fill",(4,-1,4),450,3),("Rim",(1,4,5),750,3)]:
    data=bpy.data.lights.new("Stasis_"+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(data.name,data);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.65))-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new("Stasis_Review_Camera");camdata.type='ORTHO';camdata.ortho_scale=3.5
cam=bpy.data.objects.new(camdata.name,camdata);studio.objects.link(cam)
cam.location=(3,-4,3.1);cam.rotation_euler=(Vector((0,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True);bpy.context.view_layer.objects.active=root
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
        area.spaces.active.region_3d.view_distance=4.0
        area.spaces.active.region_3d.view_location=(0,0,.65)
        area.spaces.active.shading.color_type='MATERIAL'
meshes=[o for o in asset.objects if o.type=='MESH']
stats={"vertices":sum(len(o.data.vertices) for o in meshes),"triangles":sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),"mesh_objects":len(meshes)}
root["mesh_vertices"]=stats["vertices"]
with open(out+"/stats.json","w") as f:json.dump(stats,f,indent=2)
bpy.data.libraries.write(out+"/stasis_capsule_v1.blend",{scene},fake_user=True)
result=stats

