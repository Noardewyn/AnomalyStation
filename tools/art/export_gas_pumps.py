"""Save original-atlas Gas_Pumps states and a multi-angle review scene."""
import bpy,bmesh,json,math,struct,re,time
from mathutils import Vector,Matrix
r=bpy.app.driver_namespace['gas_pumps_build'];ROOT=r['ROOT'];OUT=r['OUT'];ASSETS=r['ASSETS']
def clean(n):return re.sub(r'\.\d{3}$','',n)
def save_blend(path,scenes):
 for attempt in range(5):
  try:
   bpy.data.libraries.write(str(path),scenes,path_remap='RELATIVE',fake_user=True,compress=True);return
  except OSError:
   if attempt==4:raise
   time.sleep(.5)
report={};health={}
for key,a in ASSETS.items():
 sc=a['scene'];bpy.context.window.scene=sc;bpy.ops.object.select_all(action='SELECT');path=OUT/(key+'.glb')
 for attempt in range(5):
  try:
   bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True);break
  except RuntimeError:
   if attempt==4:raise
   time.sleep(.5)
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
 for node in g['nodes']:node['name']=clean(node.get('name',''))
 for m in g.get('materials',[]):
  if 'Overlay' in m.get('name',''):m.update(alphaMode='BLEND',doubleSided=True)
 payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
 path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)
 save_blend(OUT/(key+'.blend'),{sc})
 parts=[];health[key]=[]
 for ob in a['parts']:
  me=ob.data;me.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(me);bad=sum(not e.is_manifold for e in bm.edges);bm.free()
  assert not bad or 'Overlay' in ob.name,(key,ob.name,bad)
  assert all(not p.use_smooth for p in me.polygons)
  parts.append({'name':clean(ob.name),'triangles':len(me.loop_triangles),'shared_mesh':me.name,'materials':[m.name for m in me.materials],'parent':clean(ob.parent.name) if ob.parent else None,'pivot_blender_m':list(ob.location),'source_state':ob.get('source_state')})
  health[key].append({'part':clean(ob.name),'nonmanifold_edges':bad,'intentional_effect_sheet':'Overlay' in ob.name})
 report[key]={'base_state':a['base_state'],'overlays':a['overlay_states'],'parts':parts,'triangles':sum(p['triangles'] for p in parts)}
(OUT/'model_info.json').write_text(json.dumps(report,indent=2)+'\n');(OUT/'mesh_integrity.json').write_text(json.dumps(health,indent=2)+'\n');(OUT/'source_states.json').write_text(json.dumps(r['META'],indent=2)+'\n')
save_blend(OUT/'gas_pumps_variants_library.blend',{a['scene'] for a in ASSETS.values()})
sc=bpy.data.scenes.new('Gas_Pumps_Preview');bpy.context.window.scene=sc
world=bpy.data.worlds.new('Camera_Studio');world.use_nodes=True;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.12,.15,.19,1);sc.world=world
items=[('pressure_gas_pump_off','PRESSURE / OFF',0),('pressure_gas_pump_on','PRESSURE / ON',0),('pressure_gas_pump_on','REAR / SIDE',math.pi/2),('volume_gas_pump_off','VOLUME / OFF',0),('volume_gas_pump_on','VOLUME / ON',0),('volume_gas_pump_overclocked','OVERCLOCKED',0)]
for i,(key,label,angle) in enumerate(items):
 offset=Vector(((i%3)*.8,(i//3)*1.25,0));mapping={}
 # Keep actual parent pivots and local transforms in all preview copies.
 for src in ASSETS[key]['scene'].objects:
  ob=src.copy();sc.collection.objects.link(ob);mapping[src]=ob
 anchor=bpy.data.objects.new(label+'_Display',None);sc.collection.objects.link(anchor);anchor.location=offset;anchor.rotation_euler.z=angle
 for src,ob in mapping.items():ob.parent=mapping.get(src.parent,anchor)
 cu=bpy.data.curves.new(label,'FONT');cu.body=label;cu.size=.045;cu.align_x='CENTER';ob=bpy.data.objects.new(label,cu);sc.collection.objects.link(ob);ob.location=offset+Vector((0,-.57,0));ob.rotation_euler=(0,0,0)
target=Vector((.8,.58,.05))
for name,loc,power in [('Key',(-2,-3,4),300),('Fill',(3,1,2),180)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=3;ob=bpy.data.objects.new(name,data);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Camera_Review');data.type='ORTHO';data.ortho_scale=3.1;cam=bpy.data.objects.new('Camera_Review',data);sc.collection.objects.link(cam);cam.location=target+Vector((1,-4,5));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam
sc.render.resolution_x=1600;sc.render.resolution_y=1150;sc.render.resolution_percentage=100;sc.render.filepath=str(ROOT/'docs/ai/art/gas_pumps_preview.png');bpy.ops.render.render(write_still=True)
save_blend(OUT/'gas_pumps_preview.blend',{sc})
print('Saved',len(report),'Gas_Pumps variants and preview')
