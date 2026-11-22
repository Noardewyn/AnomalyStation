"""Wall airlock pressure console: original atlas states and shared physical parts."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/airlock_pressure_controller';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):return ((256+x)/IM.size[0],1-y/IM.size[1])
def trans(p):return ((p[0]-15.5)*S,-p[1]*S,(p[2]-5.5)*S)
for mode,st,frame in [('standby','airlock_control_standby',0),('off','airlock_control_off',0),('process_0','airlock_control_process',0),('process_1','airlock_control_process',1)]:
 key='airlock_pressure_controller_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':frame}
 mat=material('Airlock_Pressure_Controller_'+mode+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx,ty,_,_=ST[st]['rects'][frame];mp.inputs['Location'].default_value=((tx-256)/IM.size[0],-ty/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 if not shared:
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,23-z)
   if a==2 and sg==1:return patch(st,(10,9,21,12),(x-10)/11,y/3)
   if a==0:return patch(st,(10,9,12,23),y/3,(11-z)/11)
   if a==2:return patch(st,(10,22,21,23),(x-10)/11,y/3)
   return patch(st,(10,9,21,12),(x-10)/11,(11-z)/11)
  solid('Continuous_Pressure_Console_Housing',{(x,y,z) for x in range(10,21) for y in range(3) for z in range(11)},trans,tex,mat)
  for name,box,depth in [('Screen',(12,15,16,17),.04),('Status_Indicator',(17,13,19,15),.05)]+[('Push_Button_'+str(i+1),(x,y,x+1,y+1),.15) for i,(x,y) in enumerate((x,y) for y in [18,20] for x in [12,14,16,18])]:
   x0,y0,x1,y1=box
   def ft(p,c,a,sg):
    if a==1 and sg==1:return uv(st,p[0],23-p[2])
    return patch(st,box,(p[0]-x0)/(x1-x0),(23-y0-p[2])/(y1-y0))
   ob=solid(name,{(x,0,z) for x in range(x0,x1) for z in range(23-y1,23-y0)},lambda p:trans((p[0],3+p[1]*depth,p[2])),ft,mat);pivot=Vector(trans(((x0+x1)/2,3,23-(y0+y1)/2)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot
   if name.startswith('Push_Button'):ob['press_axis']='local +Y; no animation'
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob);ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 for ob in parts:ob['source_state']=st;ob['source_frame']=frame
bpy.app.driver_namespace['airlock_pressure_controller_build']=globals()
