"""Reload corrected atlases and update existing assets in place."""
from pathlib import Path
import bpy,bmesh,json,struct
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/table_kits'
variants=json.loads((OUT/'variants.json').read_text())
for prefix,file in [('Table_Families_Kit','table_families_kit.blend'),('All_Table_Variants','all_table_variants.blend'),('Table_Family_Assembly_Examples','table_family_examples.blend')]:
 if not any(s.name.startswith(prefix) for s in bpy.data.scenes):
  with bpy.data.libraries.load(str(OUT/file),link=False) as (available,loaded):loaded.scenes=[n for n in available.scenes if n.startswith(prefix)][:1]
for name,v in variants.items():
 path=OUT/v['texture'];fresh=bpy.data.images.load(str(path),check_existing=False);fresh.pack()
 for mat in bpy.data.materials:
  if not mat.use_nodes:continue
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image and Path(bpy.path.abspath(node.image.filepath)).resolve()==path.resolve():node.image=fresh
def latest(prefix):return sorted([s for s in bpy.data.scenes if s.name.startswith(prefix)],key=lambda s:s.name)[-1]
# A concave L needs a return of the rim at its inner corner. Its two-leg
# module has a dedicated texture slot; a one-leg outer corner remains open.
for me in bpy.data.meshes:
 if 'table_corner_2legs' not in me.name or me.get('inner_corner_fixed'):continue
 uv=me.uv_layers.active
 for loop in uv.data:
  if 91/128-.0001<=loop.uv.x<=119/128+.0001 and 1-29/128-.0001<=loop.uv.y<=1-1/128+.0001:loop.uv.y-=30/128
 if me.name.startswith(('glass_','framed_glass_')):
  bm=bmesh.new();bm.from_mesh(me);layer=bm.loops.layers.uv.active
  b=.075 if me.name.startswith('framed_glass_') else .045
  coords=[(.45-b,.45-b),(.45,.45-b),(.45,.45),(.45-b,.45)]
  face=bm.faces.new([bm.verts.new((x,y,.7501)) for x,y in coords]);face.material_index=0
  for loop,(x,y) in zip(face.loops,coords):loop[layer].uv=((91+.001+(x+.45)/.9*27.998)/128,1-(31+.001+(.45-y)/.9*27.998)/128)
  bm.to_mesh(me);bm.free()
 me['inner_corner_fixed']=True;me.calc_loop_triangles()
kit=latest('Table_Families_Kit');bpy.context.window.scene=kit
for r in json.loads((OUT/'modules.json').read_text())['modules']:
 name=Path(r['file']).stem;ob=next(o for o in kit.objects if o.name.split('.')[0]==name)
 old=ob.location.copy()
 for o in kit.objects:o.select_set(False)
 ob.location=(0,0,0);ob.select_set(True);bpy.context.view_layer.objects.active=ob
 path=OUT/r['file']
 try:bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
 finally:ob.location=old
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
 for nd in g['nodes']:nd['name']=nd['name'].split('.')[0]
 raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
for prefix,file in [('Table_Families_Kit','table_families_kit.blend'),('All_Table_Variants','all_table_variants.blend'),('Table_Family_Assembly_Examples','table_family_examples.blend')]:
 scene=latest(prefix);bpy.data.libraries.write(str(OUT/file),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
scene=latest('All_Table_Variants');bpy.context.window.scene=scene;bpy.ops.render.render(write_still=True)
# Top-down inspection: shared mesh instances from the actual assembly scene.
source=latest('Table_Family_Assembly_Examples');check=bpy.data.scenes.new('Table_Seams_Verification')
for i,family in enumerate(['framed_glass','reinforced']):
 for col,shape in enumerate(['Square','L']):
  objs=[o for o in source.objects if o.name.startswith(family+'_'+shape+'_')]
  lowx=min(o.location.x for o in objs);lowy=min(o.location.y for o in objs)
  for src in objs:
   ob=src.copy();check.collection.objects.link(ob);ob.location=src.location-Vector((lowx,lowy,0))+Vector((col*3.8,i*3.8,0))
check.world=scene.world
for src in scene.objects:
 if src.type=='LIGHT':check.collection.objects.link(src)
data=bpy.data.cameras.new('Seams_Camera');data.type=next(i.identifier for i in data.bl_rna.properties['type'].enum_items if i.identifier=='ORTHO');data.ortho_scale=7.7
cam=bpy.data.objects.new('Seams_Camera',data);check.collection.objects.link(cam);cam.location=(2.4,2.8,12);cam.rotation_euler=(0,0,0);check.camera=cam
check.render.resolution_x=1400;check.render.resolution_y=1400;check.render.resolution_percentage=100;check.render.filepath=str(ROOT/'docs/ai/art/table_seams_corrected.png')
bpy.context.window.scene=check;bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'table_seams_verification.blend'),{check},path_remap='RELATIVE',fake_user=True,compress=True)
print('Updated 36 GLBs, 3 library scenes, and seam verification render.')
