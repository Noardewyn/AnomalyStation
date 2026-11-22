"""Original-atlas access buttons with shared geometry and material UV offsets."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/access_button'
OUT.mkdir(parents=True,exist_ok=True);S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png'
IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text())
ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text())
 exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):return ((352+x)/IM.size[0],1-y/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-2.5)*S)
metal=material('Access_Button_Housing_Original')
shared=[]
for mode,st,frame in [('standby','access_button_standby',0),('off','access_button_off',0),('cycle_green','access_button_cycle',0),('cycle_red','access_button_cycle',1)]:
 scene=bpy.data.scenes.new('access_button_'+mode);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ASSETS['access_button_'+mode]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':frame}
 state_mat=material('Access_Button_'+mode+'_Original')
 nodes=state_mat.node_tree.nodes;links=state_mat.node_tree.links
 t=next(n for n in nodes if n.type=='TEX_IMAGE');coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeMapping')
 tx,ty,_,_=ST[st]['rects'][frame];mapping.inputs['Location'].default_value=((tx-352)/IM.size[0],-ty/IM.size[1],0)
 links.new(coord.outputs['UV'],mapping.inputs['Vector']);links.new(mapping.outputs['Vector'],t.inputs['Vector'])
 if not shared:
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,20-z)
   if a==2 and sg==1:return patch(st,(12,12,20,15),(x-12)/8,y/2)
   if a==0:return patch(st,(12,12,14,20),y/2,(5-z)/5)
   if a==2:return patch(st,(12,19,20,20),(x-12)/8,y/2)
   return patch(st,(12,12,20,15),(x-12)/8,(5-z)/5)
  solid('Continuous_Access_Button_Housing',{(x,y,z) for x in range(12,20) for y in range(2) for z in range(5)},trans,tex,metal)
  for name,x0,x1,amount in [('Press_Button',14,15,.18),('Status_Indicator',16,18,.06)]:
   def face(p,c,a,sg):
    if a==1 and sg==1:return uv(st,p[0],20-p[2])
    return patch(st,(x0,16,x1,18),(p[0]-x0)/(x1-x0),(4-p[2])/2)
   ob=solid(name,{(x,0,z) for x in range(x0,x1) for z in range(2,4)},lambda p:trans((p[0],2+p[1]*amount,p[2])),face,state_mat)
   pivot=Vector(trans(((x0+x1)/2,2,3)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot
   if name=='Press_Button':ob['press_axis']='local +Y into wall; no animation'
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
   if ob.name.startswith(('Press_Button','Status_Indicator')):
    ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=state_mat
 for ob in parts:ob['source_state']=st;ob['source_frame']=frame
bpy.app.driver_namespace['access_button_build']=globals()
