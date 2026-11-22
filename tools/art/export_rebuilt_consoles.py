"""Export reviewed consoles and their editable preview scene."""
import bpy,json,struct,re,time,hashlib
from mathutils import Vector
r=bpy.app.driver_namespace['console_rebuild'];ROOT=r['ROOT'];OUT=r['OUT'];report={}
def export(key,a):
 sc=a['scene'];bpy.context.window.scene=sc;bpy.ops.object.select_all(action='SELECT')
 path=OUT/(key+'.glb')
 for attempt in range(5):
  try:
   bpy.ops.export_scene.gltf(filepath=path.as_posix(),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True);break
  except RuntimeError:
   if attempt==4:raise
   time.sleep(.5)
 raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);tail=raw[20+n:]
 for s in g['samplers']:s.update(minFilter=9728,magFilter=9728)
 for node in g['nodes']:node['name']=re.sub(r'\.\d{3}$','',node.get('name',''))
 for mat in g['materials']:
  if 'Overlay' in mat.get('name',''):mat.update(alphaMode='MASK',alphaCutoff=.1)
 payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
 path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
 bpy.data.libraries.write(str(OUT/(key+'.blend')),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
 for ob in a['parts']:ob.data.calc_loop_triangles()
 report[key]={'base_state':a['base_state'],'screen_state':a['screen_state'],'keyboard_state':a['keyboard_state'],'height_m':a['root']['height_m'],'pedestal_height_m':a['root']['pedestal_height_m'],'width_m':round(a['parts'][0].dimensions.x,3),'triangles':sum(len(o.data.loop_triangles) for o in a['parts']),'body_triangles':len(a['parts'][0].data.loop_triangles),'connected_body_components':1,'screen_recess_m':.024,'original_png_sha256':hashlib.sha256(r['SRC'].read_bytes()).hexdigest(),'surface_texture':'Original atlas on front, top, sides, rear, underside and opening liners','material_slots':{'Body':'Console_Original_Atlas','Screen_Overlay':'Console_Overlay_Original_Atlas','Keyboard_Overlay':'Console_Overlay_Original_Atlas'}}
for key,a in r['ASSETS'].items():export(key,a)
sc=bpy.data.scenes.new('Space_Consoles_Revised_Preview');bpy.context.window.scene=sc
for i,(key,a) in enumerate(r['ASSETS'].items()):
 for src in a['parts']:
  ob=src.copy();ob.parent=None;ob.name=key+'_'+re.sub(r'\.\d{3}$','',src.name);sc.collection.objects.link(ob);ob.location.x+=i*1.65
world=bpy.data.worlds.new('Console_Review_World');world.use_nodes=True;sc.world=world;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.18,.18,.18,1)
for name,loc,power in [('Key',(-3,-4,6),1000),('Fill',(5,0,4),700)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=5;ob=bpy.data.objects.new(name,data);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((.8,0,.5))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Console_Camera');data.type='ORTHO';data.ortho_scale=3.65;cam=bpy.data.objects.new('Console_Camera',data);sc.collection.objects.link(cam);target=Vector((.8,0,.69));cam.location=target+Vector((2,-5,2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
sc.render.resolution_x=1400;sc.render.resolution_y=900;sc.render.resolution_percentage=100;sc.render.filepath=str(ROOT/'docs/ai/art/space_consoles_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'space_consoles_preview.blend'),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'model_info.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))

