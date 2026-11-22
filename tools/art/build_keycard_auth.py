"""Wall-mounted authentication reader, original wallmounts atlas."""
from pathlib import Path
import bpy,bmesh,ast,json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/machines/keycard_auth';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png'
IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels)
META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text())
 exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][1 if st=='auth_on' else 0]
 return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def trans(p):return ((p[0]-15.5)*S,-p[1]*S,(p[2]-5)*S)
def depth(x,z):
 if 12<=x<19 and 5<=z<8:return 2
 if 12<=x<19 and z==3:return 2
 return 3
metal=material('Keycard_Reader_Original_Atlas')
glow=material('Keycard_Confirmation_Overlay',True)
bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
t=next(n for n in glow.node_tree.nodes if n.type=='TEX_IMAGE')
glow.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha'])
glow.surface_render_method='BLENDED'
base_mesh=None
for mode in ['idle','confirmation']:
 key='keycard_auth_wall_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene
 scene.unit_settings.system='METRIC';parts=[]
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':'auth_off','overlay_states':['auth_on frame 1'] if mode=='confirmation' else []}
 if base_mesh is None:
  cells={(x,y,z) for x in range(9,22) for z in range(10) for y in range(depth(x,z))}
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv('auth_off',x,21-z)
   if a==2:return patch('auth_off',(9,11,22,13),(x-9)/13,y/3)
   if a==0:return patch('auth_off',(9,12,12,20),y/3,(10-z)/10)
   return patch('auth_off',(9,18,22,21),(x-9)/13,(10-z)/10)
  ob=solid('Continuous_Reader_Housing',cells,trans,tex,metal);base_mesh=ob.data
 else:
  ob=bpy.data.objects.new('Continuous_Reader_Housing',base_mesh);scene.collection.objects.link(ob);parts.append(ob)
 ob['source_state']='auth_off';ob['mount']='Rear-center origin; front Blender -Y'
 if mode=='confirmation':
  vertices=[];faces=[];uvs=[]
  tx,ty,_,_=ST['auth_on']['rects'][1]
  for y in range(12,17):
   for x in range(11,20):
    idx=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4
    if PIX[idx+3]<=0:continue
    z=20-y;d=depth(x,z)+.015
    points=[(x,d,z),(x+1,d,z),(x+1,d,z+1),(x,d,z+1)]
    start=len(vertices);vertices.extend(trans(p) for p in points);faces.append(tuple(range(start,start+4)))
    uvs.extend(uv('auth_on',p[0],21-p[2]) for p in points)
  me=bpy.data.meshes.new('Confirmation_Overlay');me.from_pydata(vertices,[],faces);me.update()
  layer=me.uv_layers.new()
  for poly in me.polygons:
   for li in poly.loop_indices:layer.data[li].uv=uvs[me.loops[li].vertex_index]
  ob=bpy.data.objects.new('Confirmation_Overlay',me);scene.collection.objects.link(ob);me.materials.append(glow);parts.append(ob)
  ob['source_state']='auth_on';ob['source_frame']=1
bpy.app.driver_namespace['keycard_auth_build']=globals()
