"""Reusable seven-module table families and material-only appearance variants."""
from pathlib import Path
import bpy,bmesh,json,struct,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/table_kits'
variants=json.loads((OUT/'variants.json').read_text())
specs=[('table_single_4legs',0,[(-.385,-.385),(.385,-.385),(.385,.385),(-.385,.385)]),('table_end_2legs',2,[(-.385,-.385),(-.385,.385)]),('table_top_straight',10,[]),('table_corner_1leg',3,[(-.385,-.385)]),('table_corner_2legs',3,[(-.385,-.385),(.385,.385)]),('table_top_tee',11,[]),('table_top_center',15,[])]
def en(o,p,v):return next(i.identifier for i in o.bl_rna.properties[p].enum_items if i.identifier==v)
materials={}
for name,info in variants.items():
 im=bpy.data.images.load(str(OUT/info['texture']),check_existing=True);im.pack();pair=[]
 for glass in [False,True]:
  m=bpy.data.materials.new(name+('_Glass' if glass else '_Frame'));m.use_nodes=True
  bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.25 if glass else .8
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;t.interpolation=en(t,'interpolation','Closest');m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
  if glass:bs.inputs['Alpha'].default_value=.42;m.surface_render_method=en(m,'surface_render_method','BLENDED');m.use_backface_culling=True
  pair.append(m)
 materials[name]=pair
 # External Godot materials can be swapped without making copies of the mesh.
 for slot in ['frame','glass']:
  text='[gd_resource type="StandardMaterial3D" load_steps=2 format=3]\n\n[ext_resource type="Texture2D" path="res://assets/models/furniture/table_kits/'+info['texture']+'" id="1"]\n\n[resource]\ntexture_filter = 0\nalbedo_texture = ExtResource("1")\nroughness = '+('0.25' if slot=='glass' else '0.8')+'\n'
  if slot=='glass':text+='transparency = 1\nalbedo_color = Color(1, 1, 1, 0.42)\n'
  (OUT/(name+'_'+slot+'.tres')).write_text(text)
kit=bpy.data.scenes.new('Table_Families_Kit');bpy.context.window.scene=kit
def build(family,name,mask,legs,variant):
 verts=[];faces=[];uvs=[];slots=[]
 def face(ids,patch,slot=0,coords=None):
  faces.append(ids);slots.append(slot)
  if isinstance(patch,int) and 'corner_2legs' in name:patch=7
  if isinstance(patch,int):r=(1+30*(patch%4),1+30*(patch//4),28,28)
  else:r={'rim':(122,12,1,3),'leg':(122,1,3,6),'dark':(122,19,2,2),'skirt':(122,25,3,28)}[patch]
  if coords is None:coords=[(0,1),(1,1),(1,0),(0,0)]
  if patch=='skirt':coords=[(v,u) for u,v in coords]
  x,y,w,h=r;uvs.append([((x+.001+u*(w-.002))/128,1-(y+.001+v*(h-.002))/128) for u,v in coords])
 def prism(poly,z0,z1,top='dark',side='leg',slot=0):
  n=len(verts);count=len(poly);verts.extend([(x,y,z) for z in [z0,z1] for x,y in poly])
  coords=[((x+.45)/.9,(.45-y)/.9) for x,y in poly]
  face([n+i for i in reversed(range(count))],top,slot,list(reversed(coords)))
  face([n+count+i for i in range(count)],top,slot,coords)
  for i in range(count):j=(i+1)%count;face([n+i,n+j,n+count+j,n+count+i],side,slot)
 def box(x0,x1,y0,y1,z0,z1,top='dark',side='leg',slot=0):prism([(x0,y0),(x1,y0),(x1,y1),(x0,y1)],z0,z1,top,side,slot)
 poly=[(-.45,-.45),(.45,-.45),(.45,.45),(-.45,.45)]
 if family in ['wood','rolling']:
  corners=[]
  for i,(x,y) in enumerate(poly):
   bits=(8 if x<0 else 2)|(4 if y<0 else 1)
   if mask&bits==0:
    prev=Vector(poly[(i-1)%4]);nxt=Vector(poly[(i+1)%4]);p=Vector((x,y));corners.extend([tuple(p+(prev-p).normalized()*.065),tuple(p+(nxt-p).normalized()*.065)])
   else:corners.append((x,y))
  poly=corners
 if family in ['glass','framed_glass']:
  border=.045 if family=='glass' else .075
  x0=-.45+(border if not mask&8 else 0);x1=.45-(border if not mask&2 else 0)
  y0=-.45+(border if not mask&4 else 0);y1=.45-(border if not mask&1 else 0)
  box(x0,x1,y0,y1,.66,.75,mask,'rim',1)
  if not mask&8:box(-.45,x0,-.45,.45,.66,.75,mask,'rim')
  if not mask&2:box(x1,.45,-.45,.45,.66,.75,mask,'rim')
  if not mask&4:box(x0,x1,-.45,y0,.66,.75,mask,'rim')
  if not mask&1:box(x0,x1,y1,.45,.66,.75,mask,'rim')
 else:prism(poly,.66,.75,mask,'rim')
 for x,y in legs:
  box(x-.05,x+.05,y-.05,y+.05,.10 if family=='rolling' else 0,.668)
  if family in ['reinforced','framed_glass']:box(x-.065,x+.065,y-.065,y+.065,0,.09)
  if family=='rolling':
   # Eight-sided wheel, axis across X. 28 triangles per wheel.
   n=len(verts)
   for xx in [x-.035,x+.035]:
    verts.extend([(xx,y+.065*math.sin(j*math.tau/8),.065+.065*math.cos(j*math.tau/8)) for j in range(8)])
   face([n+j for j in reversed(range(8))],'dark',coords=[(0,0)]*8);face([n+8+j for j in range(8)],'dark',coords=[(0,0)]*8)
   for j in range(8):k=(j+1)%8;face([n+j,n+k,n+8+k,n+8+j],'dark')
 if family in ['cloth','reinforced','rolling']:
  low=.48 if family=='cloth' else .57
  for bit in [1,2,4,8]:
   if mask&bit:continue
   if bit in [1,4]:
    y=.435 if bit==1 else -.435;box(-.45,.45,y-.015,y+.015,low,.66,'dark','skirt' if family=='cloth' else 'rim')
   else:
    x=.435 if bit==2 else -.435;box(x-.015,x+.015,-.42,.42,low,.66,'dark','skirt' if family=='cloth' else 'rim')
 if family in ['glass','framed_glass'] and 'corner_2legs' in name:
  b=.075 if family=='framed_glass' else .045
  coords=[(.45-b,.45-b),(.45,.45-b),(.45,.45),(.45-b,.45)]
  n=len(verts);verts.extend((x,y,.7501) for x,y in coords)
  face(list(range(n,n+4)),7,0,[((x+.45)/.9,(.45-y)/.9) for x,y in coords])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces)
 mesh['inner_corner_fixed']=True
 for mat in materials[variant]:mesh.materials.append(mat)
 uv=mesh.uv_layers.new(name='Shared_Table_Atlas_UV')
 for p,coords,slot in zip(mesh.polygons,uvs,slots):
  p.material_index=slot
  for li,c in zip(p.loop_indices,coords):uv.data[li].uv=c
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert sum(not e.is_manifold for e in bm.edges)==(4 if family in ['glass','framed_glass'] and 'corner_2legs' in name else 0);bm.to_mesh(mesh);bm.free();mesh.calc_loop_triangles()
 ob=bpy.data.objects.new(name,mesh);kit.collection.objects.link(ob);ob['grid_pitch_m']=.9;ob['neighbor_mask']=mask;ob['leg_count']=len(legs);ob['top_thickness_m']=.09;ob['leg_section_m']=.1;return ob
def export(ob):
 bpy.context.window.scene=kit
 for o in kit.objects:o.select_set(False)
 ob.select_set(True);bpy.context.view_layer.objects.active=ob
 path=OUT/(ob.name+'.glb');bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
 raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
 assert len(g['meshes'])==1
 assert sum(g['accessors'][p['indices']]['count']//3 for m in g['meshes'] for p in m['primitives'])==len(ob.data.loop_triangles)
modules={};records=[]
for family in ['wood','cloth','glass','framed_glass','reinforced','rolling']:
 variant=next(k for k,v in variants.items() if v['family']==family)
 for name,mask,legs in (specs[:1] if family=='rolling' else specs):
  ob=build(family,family+'_'+name,mask,legs,variant);export(ob);modules[(family,name)]=ob
  records.append({'file':ob.name+'.glb','family':family,'mask':mask,'legs':len(legs),'triangles':len(ob.data.loop_triangles),'default_variant':variant})
for i,ob in enumerate(modules.values()):ob.location=((i%7)*1.25,(i//7)*1.4,0)
bpy.data.libraries.write(str(OUT/'table_families_kit.blend'),{kit},path_remap='RELATIVE',fake_user=True,compress=True)
preview=bpy.data.scenes.new('All_Table_Variants');bpy.context.window.scene=preview
for i,(variant,info) in enumerate(variants.items()):
 family=info['family']
 if family=='metal':
  # Reuse the updated metal base mesh; generated variant atlases share its UV layout.
  metal_scene=sorted([s for s in bpy.data.scenes if s.name.startswith('Modular_Table_Compact_Kit')],key=lambda s:s.name)[-1]
  src=next(o for o in metal_scene.objects if o.name.startswith('table_single_4legs'))
 else:src=modules[(family,'table_single_4legs')]
 ob=src.copy();ob.parent=None;ob.name=variant;preview.collection.objects.link(ob);ob.location=((i%6)*1.35,(i//6)*1.55,0)
 for j,slot in enumerate(ob.material_slots):slot.link='OBJECT';slot.material=materials[variant][min(j,1)]
 data=bpy.data.curves.new('Label_'+variant,'FONT');data.body=variant;data.size=.075;label=bpy.data.objects.new('Label_'+variant,data);preview.collection.objects.link(label);label.location=(ob.location.x-.48,ob.location.y-.65,.01)
world=bpy.data.worlds.new('Tables_World');world.use_nodes=True;preview.world=world;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.7
for name,loc,power in [('Key',(-4,-3,9),1700),('Fill',(8,5,8),1200)]:
 ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=7;ob=bpy.data.objects.new(name,ld);preview.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((3,2,0))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Tables_Camera');data.type=en(data,'type','ORTHO');data.ortho_scale=11;cam=bpy.data.objects.new('Tables_Camera',data);preview.collection.objects.link(cam);target=Vector((3.3,2.3,.3));cam.location=target+Vector((3,-9,10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.resolution_x=1800;preview.render.resolution_y=1300;preview.render.resolution_percentage=100;preview.render.image_settings.file_format=en(preview.render.image_settings,'file_format','PNG');preview.render.filepath=str(ROOT/'docs/ai/art/all_table_variants.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'all_table_variants.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'modules.json').write_text(json.dumps({'pitch_m':.9,'height_m':.75,'top_thickness_m':.09,'leg_section_m':.1,'modules':records},indent=2)+'\n')
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.shading.type=en(sp.shading,'type','MATERIAL');sp.region_3d.view_location=target;sp.region_3d.view_distance=12;sp.region_3d.view_rotation=Vector((3,-9,10)).to_track_quat('Z','Y')
print(json.dumps({'new_modules':len(records),'appearances':len(variants),'triangles':{r['file']:r['triangles'] for r in records}}))
