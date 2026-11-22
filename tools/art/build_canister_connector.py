"""Original-atlas floor vent pumps and scrubbers."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/canister_connector';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/atmospherics/unary_devices.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'boundary_mesh','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def patch(st,box,u,v):
 x0,y0,x1,y1=box;return uv(st,x0+.02+(x1-x0-.04)*max(0,min(1,u)),y0+.02+(y1-y0-.04)*max(0,min(1,v)))
def material(name,emission=False,alpha=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.7;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4
 return m



metal=material('Connector_Original_Atlas')
def trans(p):return ((p[0]-15.5)*S,(16-p[1])*S,p[2]*S)
for mode in ['floor','cap','pipe']:
 key='canister_connector_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'connector','overlay_states':[] if mode=='floor' else ['connector_cap']}
 occupied={(x,y) for x in range(32) for y in range(32) if pixel('connector',x,y)[3]>.99}
 cells={(x,y,0) for x,y in occupied}
 # Raised outer shell, continuous closed bottom and lined central docking recess.
 cells|={(x,y,1) for x,y in occupied if not (10<=x<21 and 10<=y<22)}
 def tex(p,c,a,sg):
  if a==2 and sg==1:return uv('connector',p[0],p[1])
  if a==0:return patch('connector',(6,7,9,25),p[2]/2,(p[1]-6)/20)
  if a==1:return patch('connector',(7,6,24,9),(p[0]-6)/19,p[2]/2)
  return patch('connector',(9,9,22,23),(p[0]-6)/19,(p[1]-6)/20)
 solid('Continuous_Docking_Port',cells,trans,tex,metal)
 if mode!='floor':
  cap={(x,y,0) for x in range(32) for y in range(32) if pixel('connector_cap',x,y)[3]>.99}
  def capuv(p,c,a,sg):
   if a==2 and sg==1:return uv('connector_cap',p[0],p[1])
   return patch('connector_cap',(12,26,19,27),(p[0]-11)/9,p[2])
  solid('Pipe_Cap',cap,lambda p:trans((p[0],p[1],.15+p[2]*.65)),capuv,metal)
 if mode=='pipe':
  # Reuse the already-made original-atlas output pipe from the gas pump library.
  path=ROOT/'godot/assets/models/machines/gas_pumps/pressure_gas_pump_off.blend'
  with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.objects=[n for n in src.objects if n.startswith('Output_Pipe_Underlay')]
  ob=dst.objects[0];scene.collection.objects.link(ob)
  # Keep the underlay at floor level, beneath the docking plate rather than through it.
  for v in ob.data.vertices:v.co.z*=.15
  ob.name='Connected_Pipe_Underlay';parts.append(ob)
 anchor=bpy.data.objects.new('Portable_Device_Mount',None);scene.collection.objects.link(anchor);anchor.location=(0,0,0);anchor['purpose']='Portable device shares this cell origin; no connected-state model needed.'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('connector')]}
bpy.app.driver_namespace['canister_connector_build']=globals();print('Built',len(ASSETS),'connector presets')
