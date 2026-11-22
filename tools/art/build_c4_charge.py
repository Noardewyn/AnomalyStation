"""Low-poly C4 game prop with original-atlas casing, screen and stepped cable."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/items/c4_charge';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/weapons/grenade.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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


metal=material('C4_Original_Atlas');screenmat=material('C4_Display_Original',True)
BASE='plastic-explosive0'
def trans(p):return ((p[0]-14.5)*S,-p[1]*S,(p[2]-16)*S)
def safe(st,x,y):
 # Side faces reuse individual original pixels, never transparent atlas padding.
 x=max(0,min(31,int(x)));y=max(0,min(31,int(y)))
 if pixel(st,x,y)[3]<.99:
  x,y=min(((xx,yy) for yy in range(32) for xx in range(32) if pixel(st,xx,yy)[3]>.99),key=lambda q:(q[0]-x)**2+(q[1]-y)**2)
 return (x+.1,y+.1,x+.9,y+.9)
def casing(p,c,a,sg):
 x,y,z=p
 if a==1 and sg==1:
  if 10<=c[0]<16 and 12<=c[2]<17:return patch(BASE,(10,10,15,14),(x-10)/6,(17-z)/5)
  return uv(BASE,x,32-z)
 if a==1:return patch(BASE,(9,20,16,23),(x-9)/7,(24-z)/16)
 if a==0:return patch(BASE,(15,9,17,23),y/3,(24-z)/16)
 return patch(BASE,(10,9,15,11),(x-8)/8,y/3)
variants=[('idle',BASE,0),('active','plastic-explosive0_active',0),('active_bright','plastic-explosive0_active',1),('legacy_green','plastic-explosive1',0),('legacy_white','plastic-explosive1',1)]
for label,state,frame in variants:
 key='c4_charge_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':state,'overlay_states':[]}
 cells={(x,y,z) for x in range(9,16) for y in range(3) for z in range(9,23)}|{(x,y,z) for x in range(10,16) for y in range(3) for z in [8,23]}|{(8,y,z) for y in [1,2] for z in [12,14,16]}
 solid('Continuous_Charge_Casing',cells,trans,casing,metal)
 solid('Timer_Module',{(x,y,z) for x in range(10,16) for y in range(2) for z in range(12,17)},lambda p:trans((p[0],3+p[1]*.5,p[2])),lambda p,c,a,sg:uv(BASE,p[0],32-p[2]) if a==1 and sg==1 else patch(BASE,(10,15,12,20),(p[1])/2,(17-p[2])/5),metal)
 # A closed pixel-stepped cable loop in 3D, joined to the casing at both ends.
 route={(16,22),(17,22),(17,21),(18,21),(18,20),(19,20),(19,19),(19,18),(19,17),(19,16),(20,16),(20,15),(19,15),(19,14),(19,13),(18,13),(17,13),(16,13)}
 solid('Stepped_Cable_Loop',{(x,0,z) for x,z in route},lambda p:trans((p[0],1.5+p[1],p[2])),lambda p,c,a,sg:patch(BASE,safe(BASE,c[0],31-c[2]),p[0]-c[0] if a!=0 else p[1],p[2]-c[2] if a!=2 else p[1]),metal,simplify=False)
 # Six source pixels form the separate display. Keep source frame selection static.
 tx,ty,_,_=ST[state]['rects'][frame]
 def displayuv(p,c,a,sg):
  if a==1 and sg==1:return ((tx+p[0])/IM.size[0],1-(ty+32-p[2])/IM.size[1])
  return patch(BASE,(10,15,11,16),p[0]-12,(16-p[2])/3)
 ob=solid('Timer_Display',{(x,0,z) for x in range(12,14) for z in range(13,16)},lambda p:trans((p[0],4.005+p[1]*.08,p[2])),displayuv,screenmat)
 ob['source_state']=state;ob['source_frame']=frame
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('plastic-explosive')]}
bpy.app.driver_namespace['c4_charge_build']=globals();print('Built',len(ASSETS),'C4 presets')
