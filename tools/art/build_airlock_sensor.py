"""Wall airlock pressure console: original atlas states and shared physical parts."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/airlock_sensor';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):return ((32+x)/IM.size[0],1-y/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-2.5)*S)
for mode,st,frame in [('standby','airlock_sensor_standby',0),('off','airlock_sensor_off',0)]+[('cycle_'+str(i),'airlock_sensor_cycle',i) for i in range(2)]+[('alert_'+str(i),'airlock_sensor_alert',i) for i in range(3)]:
 key='airlock_sensor_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':frame}
 mat=material('Airlock_Sensor_'+mode+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx,ty,_,_=ST[st]['rects'][frame];mp.inputs['Location'].default_value=((tx-32)/IM.size[0],-ty/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 if not shared:
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,20-z)
   if a==2 and sg==1:return patch(st,(11,12,21,15),(x-11)/10,y/2)
   if a==0:return patch(st,(19,15,21,20),y/2,(5-z)/5)
   if a==2:return patch(st,(11,19,21,20),(x-11)/10,y/2)
   return patch(st,(19,12,21,20),(x-11)/10,(5-z)/5)
  solid('Continuous_Sensor_Housing',{(x,y,z) for x in range(11,21) for y in range(2) for z in range(5)},trans,tex,mat)
  for name,box,depth in [('Status_Indicator',(17,16,19,18),.05),('Press_Button_1',(13,16,14,18),.15),('Press_Button_2',(15,16,16,18),.15)]:
   x0,y0,x1,y1=box
   def ft(p,c,a,sg):
    if a==1 and sg==1:return uv(st,p[0],20-p[2])
    return patch(st,box,(p[0]-x0)/(x1-x0),(20-y0-p[2])/(y1-y0))
   ob=solid(name,{(x,0,z) for x in range(x0,x1) for z in range(20-y1,20-y0)},lambda p:trans((p[0],2+p[1]*depth,p[2])),ft,mat);pivot=Vector(trans(((x0+x1)/2,2,20-(y0+y1)/2)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot
   if name.startswith('Press_Button'):ob['press_axis']='local +Y; no animation'
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob);ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 # The source bakes its flashing halo into the frame. Preserve only pixels
 # outside the physical housing as a separate alpha effect mesh.
 pixels=list(IM.pixels);verts=[];faces=[];coords=[]
 for yy in range(32):
  for xx in range(32):
   if 11<=xx<21 and 12<=yy<20:continue
   alpha=pixels[((IM.size[1]-1-ty-yy)*IM.size[0]+tx+xx)*4+3]
   if alpha<=0:continue
   corners=[(xx,yy),(xx+1,yy),(xx+1,yy+1),(xx,yy+1)];n=len(verts)
   verts.extend([trans((x,2.02,17-y)) for x,y in corners]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,x,y) for x,y in corners])
 if faces:
  effect=mat.copy();effect.name='Airlock_Sensor_'+mode+'_Halo_Overlay'
  bs=next(n for n in effect.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in effect.node_tree.nodes if n.type=='TEX_IMAGE');effect.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);effect.surface_render_method='BLENDED'
  me=bpy.data.meshes.new('Halo_Overlay');me.from_pydata(verts,[],faces);me.materials.append(effect);layer=me.uv_layers.new()
  for poly,cc in zip(me.polygons,coords):
   for li,co in zip(poly.loop_indices,cc):layer.data[li].uv=co
  ob=bpy.data.objects.new('Halo_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ASSETS[key]['overlay_states']=[st+' (external halo pixels)']
 for ob in parts:ob['source_state']=st;ob['source_frame']=frame
bpy.app.driver_namespace['airlock_sensor_build']=globals()
