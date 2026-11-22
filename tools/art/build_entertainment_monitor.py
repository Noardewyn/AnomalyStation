"""Wall entertainment monitor with original screen layers and shared enclosure."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/entertainment_monitor';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[];screen_mesh=None
p=ROOT/'godot/assets/tg/icons/obj/machines/status_display.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-10)*S)
mat=material('Entertainment_Monitor_Housing_Original');glass=material('Entertainment_Monitor_Glass_Original',alpha=.15);glass.surface_render_method='BLENDED'
for mode,screen in [('unpowered',None),('standby','entertainment_blank'),('broadcast','entertainment'),('holiday','entertainment_xmas')]:
 key='entertainment_monitor_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'entertainment_frame','overlay_states':[screen] if screen else [],'frame':0}
 if not shared:
  st='entertainment_frame'
  cells={(x,y,z) for x in range(1,31) for y in range(4) for z in range(20) if not(z==0 and x in (1,30))}
  cells-={(x,3,z) for x in range(3,29) for z in range(2,19)}
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,26-z)
   if a==2 and sg==1:return patch(st,(3,4,29,7),(x-1)/30,y/4)
   if a==0:return patch(st,(1,7,3,25),y/4,(20-z)/20)
   if a==2:return patch(st,(3,24,29,26),(x-1)/30,y/4)
   return patch(st,(1,7,3,25),(x-1)/30,(20-z)/20)
  solid('Continuous_Entertainment_Monitor_Housing',cells,trans,tex,mat)
  solid('Closed_Screen_Glass',{(x,0,z) for x in range(3,29) for z in range(2,19)},lambda p:trans((p[0],3.72+p[1]*.12,p[2])),lambda p,c,a,sg:patch(st,(10,10,20,18),(p[0]-3)/26,(19-p[2])/17),glass)
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
 if screen:
  picture=material('Entertainment_Monitor_'+mode+'_Overlay',emission=True)
  bs=next(n for n in picture.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in picture.node_tree.nodes if n.type=='TEX_IMAGE');picture.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);picture.surface_render_method='BLENDED';bs.inputs['Emission Strength'].default_value=1
  nodes=picture.node_tree.nodes;links=picture.node_tree.links;co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx,ty,_,_=ST[screen]['rects'][0];mp.inputs['Location'].default_value=((tx-224)/IM.size[0],(160-ty)/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
  # Four-corner screen surface, full original screen alpha, distinct from glass.
  corners=[(3,24),(29,24),(29,7),(3,7)];me=bpy.data.meshes.new('Screen_Overlay');me.from_pydata([trans((x,3.60,26-y)) for x,y in corners],[],[(0,1,2),(0,2,3)]);me.materials.append(picture);u=me.uv_layers.new(name='Original_Atlas_UV')
  for poly in me.polygons:
   for li in poly.loop_indices:
    x,y=corners[me.loops[li].vertex_index];u.data[li].uv=uv('entertainment',x,y)
  ob=bpy.data.objects.new('Screen_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=screen
  if screen_mesh is None:screen_mesh=me
  else:
   ob.data=screen_mesh;ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=picture
   bpy.data.meshes.remove(me)
 for ob in parts:
  if 'source_state' not in ob:ob['source_state']='entertainment_frame'
bpy.app.driver_namespace['entertainment_monitor_build']=globals()
