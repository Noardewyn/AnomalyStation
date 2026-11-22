"""Wall turret controller with original screen and button states."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/turret_controller';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/machines/turret_control.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-5.5)*S)
mat=material('Turret_Controller_Housing_Original');cache={}
variants=[(mode+'_'+str(f),mode,f,True) for mode in ['standby','stun','kill'] for f in range(2)]+[('unpowered_'+mode,mode,0,False) for mode in ['standby','stun','kill']]
for label,mode,frame,on in variants:
 key='turret_controller_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'control','overlay_states':ovs,'frame':frame}
 if not shared:
  def tx(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv('control',x,23-z)
   if a==2 and sg==1:return patch('control',(7,10,25,12),(x-7)/18,y/2)
   if a==0:return patch('control',(7,12,9,23),y/2,(11-z)/11)
   if a==2:return patch('control',(7,22,25,23),(x-7)/18,y/2)
   return patch('control',(7,10,25,12),(x-7)/18,(11-z)/11)
  solid('Continuous_Turret_Controller_Housing',{(x,y,z) for x in range(7,25) for y in range(2) for z in range(11)},trans,tx,mat)
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
 for i,(x0,x1) in enumerate([(18,20),(21,23)]):
  active=(mode!='standby' and (i==0 or mode=='kill'));state=['button_left','button_right'][i] if active else 'control'
  if active:ovs.append(state)
  bm=material('Turret_Controller_'+label+'_Button_'+str(i+1),emission=on and active)
  # Button faces use exact state pixels; inactive buttons use the underlying base tile.
  def bt(p,c,a,sg):return patch(state,(x0,18,x1,22),(p[0]-x0)/(x1-x0),(5-p[2])/4)
  ob=solid('Button_'+str(i+1),{(x,0,z) for x in range(x0,x1) for z in range(1,5)},lambda p:trans((p[0],2+p[1]*.15,p[2])),bt,bm)
  pivot=Vector(trans(((x0+x1)/2,2,3)))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot;ob['press_axis']='local +Y';ob['source_state']=state
  sig=(i,state)
  if sig in cache:
   old=ob.data;ob.data=cache[sig];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=bm;bpy.data.meshes.remove(old)
  else:cache[sig]=ob.data
 if on:
  ovs.append(mode);screenmat=material('Turret_Controller_'+label+'_Screen_Overlay',emission=True);nodes=screenmat.node_tree.nodes;links=screenmat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Emission Strength'].default_value=1
  co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');sx,sy,_,_=ST[mode]['rects'][frame];mp.inputs['Location'].default_value=((sx-64)/IM.size[0],-sy/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
  corners=[(9,20),(15,20),(15,14),(9,14)];me=bpy.data.meshes.new('Screen_Overlay');me.from_pydata([trans((x,2.01,23-y)) for x,y in corners],[],[(0,1,2),(0,2,3)]);me.materials.append(screenmat);u=me.uv_layers.new()
  for poly in me.polygons:
   for li in poly.loop_indices:
    x,y=corners[me.loops[li].vertex_index];u.data[li].uv=uv('standby',x,y)
  ob=bpy.data.objects.new('Screen_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=mode
  if 'screen' in cache:
   ob.data=cache['screen'];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=screenmat;bpy.data.meshes.remove(me)
  else:cache['screen']=me
 for ob in parts:
  if 'source_state' not in ob:ob['source_state']='control'
bpy.app.driver_namespace['turret_controller_build']=globals()
