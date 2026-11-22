import bpy,math
from mathutils import Vector
r=bpy.app.driver_namespace['bar_sign_build'];ROOT=r['ROOT'];OUT=r['OUT'];ASSETS=r['ASSETS']
def save_blend(path,scenes):bpy.data.libraries.write(str(path),scenes,path_remap='RELATIVE',fake_user=True,compress=True)
sc=bpy.data.scenes.new('Bar_Sign_Shaped_Preview');bpy.context.window.scene=sc
world=bpy.data.worlds.new('Camera_Studio');world.use_nodes=True;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.12,.15,.19,1);sc.world=world
items=[('bar_sign_'+st,st.replace('_',' ').upper(),0) for st in r['special_variants']]+[('bar_sign_pun-puns-pub','REAR',math.pi),('bar_sign_maltroach','SIDE',math.pi/2)]
for i,(key,label,angle) in enumerate(items):
 offset=Vector(((i%5)*2.05,0,-(i//5)*1.20));mapping={}
 # Keep actual parent pivots and local transforms in all preview copies.
 for src in ASSETS[key]['scene'].objects:
  ob=src.copy();sc.collection.objects.link(ob);mapping[src]=ob
 anchor=bpy.data.objects.new(label+'_Display',None);sc.collection.objects.link(anchor);anchor.location=offset;anchor.rotation_euler.z=angle
 for src,ob in mapping.items():ob.parent=mapping.get(src.parent,anchor)
 cu=bpy.data.curves.new(label,'FONT');cu.body=label;cu.size=.034;cu.align_x='CENTER';ob=bpy.data.objects.new(label,cu);sc.collection.objects.link(ob);ob.location=offset+Vector((0,-.10,-.46));ob.rotation_euler=(math.pi/2,0,0)
target=Vector((4.1,0,-.60))
for name,loc,power in [('Key',(-2,-3,4),300),('Fill',(3,1,2),180)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=3;ob=bpy.data.objects.new(name,data);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Camera_Review');data.type='ORTHO';data.ortho_scale=10.7;cam=bpy.data.objects.new('Camera_Review',data);sc.collection.objects.link(cam);cam.location=target+Vector((1,-6,2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
sc.render.resolution_x=2500;sc.render.resolution_y=1000;sc.render.resolution_percentage=100;sc.render.filepath=str(ROOT/'docs/ai/art/bar_sign_shaped_preview.png');bpy.ops.render.render(write_still=True)
save_blend(OUT/'bar_sign_shaped_preview.blend',{sc})
print('Saved',len(ASSETS),'Bar_Sign variants and preview')
