"""Wall camera telescreen with original atlas and lined screen recess."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/telescreen';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):return (x/IM.size[0],1-(96+y)/IM.size[1])
def trans(p):return ((p[0]-15.5)*S,-p[1]*S,(p[2]-8)*S)
for mode,st in [('normal','telescreen'),('broken','telescreenb')]:
 key='telescreen_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':0}
 mat=material('Telescreen_'+mode+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx,ty,_,_=ST[st]['rects'][0];mp.inputs['Location'].default_value=(tx/IM.size[0],(96-ty)/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 if not shared:
  # One connected shell, stepped bottom corners and fully lined screen recess.
  cells={(x,y,z) for x in range(5,26) for y in range(3) for z in range(16) if not(z==0 and x in (5,25))}
  cells-={(x,2,z) for x in range(8,23) for z in range(3,11)}
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,25-z)
   if a==2 and sg==1:return patch(st,(6,8,25,10),(x-5)/21,y/3)
   if a==0:return patch(st,(5,9,8,24),y/3,(16-z)/16)
   if a==2:return patch(st,(6,23,25,25),(x-5)/21,y/3)
   return patch(st,(5,13,8,24),(x-5)/21,(16-z)/16)
  solid('Continuous_Telescreen_Housing',cells,trans,tex,mat)
  # Original screen artwork remains separate from its transparent protective slab.
  solid('Screen_Panel',{(x,0,z) for x in range(8,23) for z in range(3,11)},lambda p:trans((p[0],2.01+p[1]*.1,p[2])),lambda p,c,a,sg:uv(st,p[0],25-p[2]),mat)
  glass=material('Telescreen_Glass_Original',alpha=.15);glass.surface_render_method='BLENDED'
  solid('Closed_Screen_Glass',{(x,0,z) for x in range(8,23) for z in range(3,11)},lambda p:trans((p[0],2.75+p[1]*.12,p[2])),lambda p,c,a,sg:patch(st,(7,14,8,18),(p[0]-8)/15,(11-p[2])/8),glass)
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
   if 'Glass' not in ob.name:ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 for ob in parts:ob['source_state']=st;ob['source_direction']='S; rotate assembly for other walls'
bpy.app.driver_namespace['telescreen_build']=globals()
