"""Original-atlas door buttons, service covers and interchangeable electronics."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/mounted_flash';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'boundary_mesh','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][FRAME if st in ['mflash1','mflash_flash'] else 0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][FRAME if st in ['mflash1','mflash_flash'] else 0];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def patch(st,box,u,v):
 x0,y0,x1,y1=box;return uv(st,x0+.02+(x1-x0-.04)*max(0,min(1,u)),y0+.02+(y1-y0-.04)*max(0,min(1,v)))
def material(name,emission=False,alpha=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.7;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4
 return m


FRAME=0
metal=material('Mounted_Flash_Original_Atlas')
ready=material('Mounted_Flash_Lamp_Original',True)
bright=material('Mounted_Flash_Burst_Original',True);next(n for n in bright.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Strength'].default_value=1.5
glass=material('Mounted_Flash_Glass_Original',False,.22)
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-16)*S)
variants=[('ready','mflash1',0),('ready_alternate','mflash1',1),('flash_start','mflash_flash',0),('flash_yellow','mflash_flash',1),('flash_white','mflash_flash',2),('off','mflash1-p',0),('frame','mflash_frame',0)]
for mode,st,FRAME in variants:
 key='mounted_flash_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[]}
 # Upper row is the roof; the front retains whole-pixel lower corners.
 outline={(x,31-y) for x in range(11,21) for y in range(12,20) if pixel('mflash1-p',x,y)[3]>.99}
 lens={(x,31-y) for y in range(12,17) for x in range(14,18) if (y not in [12,16] or x in [15,16])}
 cells={(x,d,z) for x,z in outline for d in [0,1]}-{(x,1,z) for x,z in lens}
 def tex(p,c,a,sg):
  x,d,z=p
  if a==1 and sg==1:return uv('mflash_frame',x,32-z)
  if a==2:return patch('mflash1-p',(12,11,20,12),(x-11)/10,d/2)
  return patch('mflash1-p',(12,12,14,17),d/2 if a==0 else (x-11)/10,(20-z)/8)
 solid('Continuous_Wall_Housing',cells,trans,tex,metal)
 if mode!='frame':
  def lt(p,c,a,sg):
   if a==1 and sg==1:return uv(st,p[0],32-p[2])
   return patch(st,(15,13,17,16),(p[0]-14)/4,(20-p[2])/5)
  lamp=solid('Replaceable_Flashbulb',{(x,0,z) for x,z in lens},lambda p:trans((p[0],1.05+p[1]*1.15,p[2])),lt,metal if mode=='off' else bright if mode.startswith('flash_') else ready)
  pivot=Vector(trans((16,1.05,17.5)))
  for v in lamp.data.vertices:v.co-=pivot
  lamp.location=pivot;lamp['removal_axis']='local -Y';lamp['source_state']=st;lamp['source_frame']=FRAME
  cover=solid('Closed_Glass_Lens',{(x,0,z) for x,z in lens},lambda p:trans((p[0],2.21+p[1]*.14,p[2])),lt,glass)
  for v in cover.data.vertices:v.co-=pivot
  cover.parent=lamp;cover.location=(0,0,0)
  # Status strip is independent; the frame retains the original grey strip.
  indicator={(x,0,13) for x in range(13,19)}
  def it(p,c,a,sg):return patch(st,(13,18,19,19),(p[0]-13)/6,14-p[2])
  solid('Status_Indicator',indicator,lambda p:trans((p[0],2.01+p[1]*.08,p[2])),it,metal if mode=='off' else ready)
 scene['source_frame']=FRAME
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('mflash')]}
bpy.app.driver_namespace['mounted_flash_build']=globals();print('Built',len(ASSETS),'mounted flash presets')
