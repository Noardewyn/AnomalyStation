"""Original-atlas door buttons, service covers and interchangeable electronics."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/station_intercom';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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


metal=material('Intercom_Original_Atlas')
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-15)*S)
for mode,overlays in [('listening',['intercom_s']),('broadcasting',['intercom_m']),('two_way',['intercom_s','intercom_m']),('quiet',[]),('off',[])]:
 st='intercom-p' if mode=='off' else 'intercom';key='station_intercom_'+mode
 scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':overlays}
 # The upper three sprite rows depict the roof. Front silhouette retains the stepped chin.
 cells={(x,d,32-y-1) for y in range(12,22) for x in range(6,26) for d in range(3) if pixel(st,x,y)[3]>.99}
 # Trim the sprite's detached cast-shadow pixels; sink the screen one whole pixel.
 cells-={(x,2,32-y-1) for y in range(13,16) for x in range(12,20)}
 def tex(p,c,a,sg):
  x,d,z=p
  if a==1 and sg==1:return uv(st,x,32-z)
  if a==2 and sg==1 and c[2]==19:return patch(st,(7,9,25,12),(x-6)/20,d/3)
  if a==0:return patch(st,(6 if sg<0 else 24,12,8 if sg<0 else 26,20),d/3,(20-z)/10)
  if a==1:return patch(st,(6,12,12,18),(x-6)/20,(20-z)/10)
  return patch(st,(7,19,12,20),(x-6)/20,d/3)
 solid('Continuous_Intercom_Housing',cells,trans,tex,metal)
 screen={(x,0,z) for x in range(12,20) for z in range(16,19)}
 def screen_uv(p,c,a,sg):return uv(st,p[0],32-p[2])
 ob=solid('Recessed_Display',screen,lambda p:trans((p[0],2.02+p[1]*.15,p[2])),screen_uv,metal);ob['source_state']=st
 for ov in overlays:
  coords=[(x,y) for y in range(32) for x in range(32) if pixel(ov,x,y)[3]>.99]
  x0=min(x for x,y in coords);x1=max(x for x,y in coords)+1;y0=min(y for x,y in coords);y1=max(y for x,y in coords)+1
  def led_uv(p,c,a,sg):return patch(ov,(x0,y0,x1,y1),(p[0]-x0)/(x1-x0),(32-y0-p[2])/(y1-y0))
  ob=solid('Speaker_Indicator' if ov=='intercom_s' else 'Microphone_Indicator',{(x,0,31-y) for x,y in coords},lambda p:trans((p[0],3.01+p[1]*.08,p[2])),led_uv,metal);ob['source_state']=ov
# Geometry and UVs are shared by states; detachable electronics stay separate.
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('intercom')]}
bpy.app.driver_namespace['station_intercom_build']=globals();print('Built',len(ASSETS),'station intercom presets')
