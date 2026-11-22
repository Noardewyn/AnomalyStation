"""Save fixture variants, replacement parts, and a functional assembly preview."""
import bpy,json,struct,re,hashlib,time
from mathutils import Vector
r=bpy.app.driver_namespace['lighting_build'];ROOT=r['ROOT'];OUT=r['OUT'];ASSETS=r['ASSETS'];REPORT={}
def clean(s):return re.sub(r'\.\d{3}$','',s)
def save_glb(path):
 for attempt in range(5):
  try:
   bpy.ops.export_scene.gltf(filepath=path.as_posix(),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True);break
  except RuntimeError:
   if attempt==4:raise
   time.sleep(.5)
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
 for node in g['nodes']:node['name']=clean(node.get('name',''))
 for m in g.get('materials',[]):
  if 'Overlay' in m.get('name',''):m.update(alphaMode='MASK',alphaCutoff=.1)
 payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4);path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
for key,a in ASSETS.items():
 bpy.context.window.scene=a['scene'];bpy.ops.object.select_all(action='SELECT');save_glb(OUT/(key+'.glb'));bpy.data.libraries.write(str(OUT/(key+'.blend')),{a['scene']},path_remap='RELATIVE',fake_user=True,compress=True)
 parts=[]
 for ob in a['parts']:
  ob.data.calc_loop_triangles();parts.append({'name':clean(ob.name),'mesh':ob.data.name,'triangles':len(ob.data.loop_triangles),'pivot_blender_m':list(ob.location),'materials':[m.name for m in ob.data.materials],'source_state':ob.get('source_state')})
 REPORT[key]={'family':a['family'],'status':a['status'],'insert_blender_m':a['insert'],'insert_godot_m':[a['insert'][0],a['insert'][2],-a['insert'][1]],'parts':parts,'triangles':sum(p['triangles'] for p in parts)}
# Export each replacement at its insertion origin; geometry is identical to the assembled part.
for family,partname in [('tube','Removable_Tube'),('bulb','Removable_Bulb')]:
 for status in ['off','burned','broken']:
  source=ASSETS['light_'+family+'_'+status];ob=next(o for o in source['parts'] if clean(o.name)==partname)
  key='replacement_'+family+('_'+status if status!='off' else '')
  sc=bpy.data.scenes.new(key);copy=ob.copy();copy.parent=None;copy.location=(0,0,0);sc.collection.objects.link(copy);bpy.context.window.scene=sc;bpy.ops.object.select_all(action='SELECT');save_glb(OUT/(key+'.glb'));bpy.data.libraries.write(str(OUT/(key+'.blend')),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
sc=bpy.data.scenes.new('Lighting_Assembly_Preview');bpy.context.window.scene=sc
world=bpy.data.worlds.new('Lighting_Studio');world.use_nodes=True;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.13,.15,.19,1);sc.world=world
labels= r['material']('Preview_Label')
def text(label,x,z):
 cu=bpy.data.curves.new(label,'FONT');cu.body=label;cu.size=.065;cu.align_x='CENTER';ob=bpy.data.objects.new(label,cu);sc.collection.objects.link(ob);ob.location=(x,-.5,z);ob.rotation_euler=(math.pi/2,0,0)
import math
for row,family in enumerate(['tube','bulb','floor']):
 for col,status in enumerate(['on','empty','off','broken']):
  src=ASSETS['light_'+family+'_'+status];offset=Vector((col*1.05,0,(2-row)*.7))
  for original in src['parts']:
   ob=original.copy();ob.parent=None;ob.location=original.location+offset
   if status=='off' and clean(original.name).startswith(('Removable_','Emission_')):ob.location.z+=.20
   sc.collection.objects.link(ob)
  text(family+' / '+('removed' if status=='off' else status),offset.x,offset.z-.15 if family!='floor' else offset.z-.33)
for name,loc,power in [('Key',(-2,-4,6),950),('Fill',(5,-1,3),650)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=5;ob=bpy.data.objects.new(name,data);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((1.5,0,.8))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Lighting_Camera');data.type='ORTHO';data.ortho_scale=4.55;cam=bpy.data.objects.new('Lighting_Camera',data);sc.collection.objects.link(cam);target=Vector((1.55,0,.8));cam.location=target+Vector((1,-6,3.2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
sc.render.resolution_x=1600;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.render.filepath=str(ROOT/'docs/ai/art/lighting_assembly_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'lighting_assembly_preview.blend'),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.data.libraries.write(str(OUT/'lighting_variants_library.blend'),{a['scene'] for a in ASSETS.values()},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'model_info.json').write_text(json.dumps(REPORT,indent=2)+'\n')
print('Saved 21 fixtures, 6 replacement parts and preview/library scenes')
