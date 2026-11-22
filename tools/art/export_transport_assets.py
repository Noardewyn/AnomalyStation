"""Export, document and render reusable transport assets without game integration."""
import bpy, json, struct, re, math, time
from mathutils import Vector
r=bpy.app.driver_namespace['transport_build'];ROOT=r['ROOT'];OUT=r['OUT'];ASSETS=r['ASSETS']
def clean(s):return re.sub(r'\.\d{3}$','',s)
report={}
for key,a in ASSETS.items():
 bpy.context.window.scene=a['scene'];bpy.ops.object.select_all(action='SELECT');path=OUT/(key+'.glb')
 for attempt in range(5):
  try:
   bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True);break
  except RuntimeError:
   if attempt==4:raise
   time.sleep(.5)
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for sampler in g.get('samplers',[]):sampler.update(minFilter=9728,magFilter=9728)
 for node in g['nodes']:node['name']=clean(node.get('name',''))
 for mat in g.get('materials',[]):
  if 'Kitchen' in mat.get('name',''):mat['alphaMode']='BLEND'
 payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
 path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
 bpy.data.libraries.write(str(OUT/(key+'.blend')),{a['scene']},path_remap='RELATIVE',fake_user=True,compress=True)
 rec={'family':a['family'],'description':a['description'],'parts':[]}
 for ob in a['parts']:
  ob.data.calc_loop_triangles();rec['parts'].append({'name':clean(ob.name),'shared_mesh':ob.data.name,'triangles':len(ob.data.loop_triangles),'materials':[m.name for m in ob.data.materials],'pivot_blender_m':list(ob.location)})
 rec['triangles']=sum(p['triangles'] for p in rec['parts'])
 if 'state' in a:rec.update(source_state=a['state'],source_direction_index=a['source_direction_index'])
 report[key]=rec
(OUT/'model_info.json').write_text(json.dumps(report,indent=2)+'\n')
(OUT/'source_states.json').write_text(json.dumps({k:v for k,v in r['META'].items() if k!='structures'}|{'plasticflaps':r['state']('structures','plasticflaps')},indent=2)+'\n')
bpy.data.libraries.write(str(OUT/'transport_variants_library.blend'),{a['scene'] for a in ASSETS.values()},path_remap='RELATIVE',fake_user=True,compress=True)

def setup(name):
 sc=bpy.data.scenes.new(name);bpy.context.window.scene=sc
 world=bpy.data.worlds.new(name+'_World');world.use_nodes=True;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.15,.18,.24,1);sc.world=world
 sc.render.resolution_x=1500;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
 return sc
def copy(key,loc=(0,0,0),rot=0,strip_angle=0):
 objs=[]
 for src in ASSETS[key]['parts']:
  ob=src.copy();ob.parent=None;v=src.location;ob.location=(loc[0]+math.cos(rot)*v.x-math.sin(rot)*v.y,loc[1]+math.sin(rot)*v.x+math.cos(rot)*v.y,loc[2]+v.z);ob.rotation_euler.z=rot
  if strip_angle and 'Hinged_Strip' in ob.name:ob.rotation_euler.x=strip_angle
  bpy.context.scene.collection.objects.link(ob);objs.append(ob)
 return objs
def finish(sc,name,target,scale,angle=(3,-5,3)):
 target=Vector(target)
 for label,loc,power in [('Key',(-3,-4,7),1500),('Fill',(5,2,5),1000)]:
  data=bpy.data.lights.new(label,'AREA');data.energy=power;data.size=5;ob=bpy.data.objects.new(label,data);sc.collection.objects.link(ob);ob.location=target+Vector(loc);ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new(name+'_Camera');data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(name+'_Camera',data);sc.collection.objects.link(cam);cam.location=target+Vector(angle);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
 sc.render.filepath=str(ROOT/('docs/ai/art/'+name+'.png'));bpy.ops.render.render(write_still=True)
 bpy.data.libraries.write(str(OUT/(name+'.blend')),{sc},path_remap='RELATIVE',fake_user=True,compress=True)

sc=setup('Railing_Assembly_Preview')
# Closed 2.7 m square: four corner modules and one intermediate per side.
for turn,angle in [((1.35,-1.35),0),((1.35,1.35),math.pi/2),((-1.35,1.35),math.pi),((-1.35,-1.35),3*math.pi/2)]:
 origin=(turn[0]-.45*math.cos(angle),turn[1]-.45*math.sin(angle),0)
 copy('railing_corner',origin,angle)
for loc,angle in [((0,-1.35,0),0),((1.35,0,0),math.pi/2),((0,1.35,0),math.pi),((-1.35,0,0),3*math.pi/2)]:copy('railing_straight',loc,angle)
copy('railing_straight',(-.9,-2.55,0));copy('railing_straight',(0,-2.55,0));copy('railing_straight',(.9,-2.55,0))
finish(sc,'railing_assembly_preview',(0,-.55,.32),5.2)
sc=setup('Conveyor_Assembly_Preview')
# A south/west elbow with three tiles on each open port; exact 0.90 m tile pitch.
copy('conveyor_corner_stopped')
for i in [1,2,3]:
 copy('conveyor_straight_stopped',(0,-.9*i,0))
 copy('conveyor_straight_stopped',(-.9*i,0,0),-math.pi/2)
copy('conveyor_corner_flipped_forward',(1.4,-.5,0))
copy('conveyor_straight_forward',(1.4,-1.4,0))
copy('conveyor_straight_reverse',(1.4,-2.3,0))
finish(sc,'conveyor_assembly_preview',(-.5,-1.15,0),6.8,(2,-4,5.5))
sc=setup('Plastic_Flaps_Preview')
copy('plastic_flaps_standard',(-1.15,0,0));copy('plastic_flaps_kitchen',(0,0,0));copy('plastic_flaps_opaque',(1.15,0,0),strip_angle=.28)
# A railing behind the kitchen curtain makes its real transparency visible.
copy('railing_straight',(0,.2,.3))
finish(sc,'plastic_flaps_preview',(0,0,.9),4.2,(1,-6,2))
print('Exported',len(report),'GLB/BLEND assets and three assembly previews')
