"""Original-atlas wall pepper spray refiller; three closed stepped reservoirs."""
from pathlib import Path
import bpy,bmesh,ast,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/pepper_refiller';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/medical/chemical_tanks.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['uv','pixel','patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
metal=material('Pepper_Refiller_Original_Atlas');st='pepper'
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-11.5)*S)
key='pepper_spray_refiller';scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[]}
cells={(x,y,z) for x in range(4,28) for y in range(6) for z in range(23) if (z>=8 or x>=18) and not(y>=1 and ((6<=x<18 and 10<=z<21) or (20<=x<26 and 3<=z<13)))}
def bodyuv(p,c,a,sg):
 x,y,z=p
 if a==1 and sg==1 and c[1]==5:return uv(st,x,27-z)
 if a==1 and sg==1 and c[1]==0:
  if c[0]<18:return patch(st,(4,5,6,18),(x-6)/12,(21-z)/11)
  return patch(st,(21,15,25,24),(x-20)/6,(13-z)/10)
 if a==2:return patch(st,(4,4,28,6),(x-4)/24,y/6)
 return patch(st,(20,14,28,27),y/6 if a==0 else (x-4)/24,(23-z)/23)
solid('Continuous_Wall_Housing_With_Recesses',cells,trans,bodyuv,metal)
for i in range(3):
 left=6+i*4
 cross={(x,y) for x in range(4) for y in range(4) if (x,y) not in [(0,0),(0,3),(3,0),(3,3)]}
 reservoir={(left+x,1+y,z) for x,y in cross for z in range(10,21) if z not in [10,20] or (1<=x<3 and 1<=y<3)}
 def tankuv(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1:return patch(st,(left,6,left+4,17),(x-left)/4,(21-z)/11)
  if a==0 or a==1:return patch(st,(left+1,6,left+3,17),y/5 if a==0 else (x-left)/4,(21-z)/11)
  return patch(st,(left+1,7,left+3,10),(x-left)/4,y/5)
 ob=solid('Reservoir_'+str(i+1),reservoir,trans,tankuv,metal);pivot=Vector(trans((left+2,3,10)))
 for v in ob.data.vertices:v.co-=pivot
 ob.location=pivot;ob['source_state']='pepper';ob['note']='Separate structural reservoir; no removal interaction or animation is asserted by source.'
solid('Recessed_Dispensing_Nozzle',{(x,y,z) for x in range(22,24) for y in range(1,5) for z in range(11,13)},trans,lambda p,c,a,sg:patch(st,(21,14,25,16),(p[0]-22)/2,(p[1]-1)/4),metal)
bpy.app.driver_namespace['pepper_refiller_build']=globals();print('Built pepper spray refiller')
