"""Original-atlas tabletop fax with separate handset, trays and item layers."""
from pathlib import Path
import bpy,bmesh,ast,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/fax';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};FRAME={}
p=ROOT/'godot/assets/tg/icons/obj/machines/fax.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][FRAME.get(st,0)];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][FRAME.get(st,0)];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
metal=material('Fax_Original_Atlas')
def trans(p):return ((p[0]-16)*S,(12-p[1])*S,p[2]*S)
variants=[('idle',None),('idle_alternate',None),('service',None),('sending_paper','send_paper'),('receiving_paper','receive_paper')]+[('loaded_'+n,'contain_'+n) for n in ['paper','photo','food','star','id','tcg','cash','pbiscuit']]
for mode,item in variants:
 FRAME={};st='fax'
 if mode=='idle_alternate':FRAME['fax']=1
 if mode in ['sending_paper','receiving_paper']:
  st='fax_receive';FRAME[st]=12;FRAME[item]=5
 key='fax_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs}
 cells={(x,y,z) for x in range(2,30) for y in range(4,24) for z in range(6)}|{(x,y,z) for x in range(10,30) for y in range(4,20) for z in range(6,8)}
 # Lined output slot and maintenance socket are true recesses.
 cells-={(x,y,z) for x in range(10,22) for y in range(20,24) for z in range(2,4)}
 if mode=='service':cells-={(x,y,z) for x in range(24,28) for y in range(22,24) for z in range(2,5)}
 def bodyuv(p,c,a,sg):
  x,y,z=p
  if a==2 and sg==1:
   if c[2]>=6:return patch(st,(10,9,30,21),(x-10)/20,(y-4)/16)
   if c[1]>=20:return patch(st,(2,21,30,25),(x-2)/28,(y-20)/4)
   return patch(st,(2,11,10,24),(x-2)/8,(y-4)/16)
  if a==1 and sg==1:return patch(st,(2,21,30,26),(x-2)/28,(6-z)/6)
  return patch(st,(2,22,30,26),(y-4)/20 if a==0 else (x-2)/28,(8-z)/8 if a!=2 else (y-4)/20)
 solid('Continuous_Fax_Housing',cells,trans,bodyuv,metal)
 # Solid paper guide: retain the stepped outer edge but close the inner hole.
 feed={(x,y,z) for x in range(9,23) for y in [2,3] for z in range(8,15) if pixel(st,x,16-z)[3]>.99}
 feed|={(x,y,z) for x in range(13,19) for y in [2,3] for z in range(9,11)}
 feed|={(x,y,z) for x in list(range(9,12))+list(range(20,23)) for y in [2,3,4] for z in range(5,8)}
 def feeduv(p,c,a,sg):
  if a==1 and 13<=c[0]<19 and 9<=c[2]<11:
   return patch(st,(10,3,22,5),(p[0]-13)/6,(11-p[2])/2)
  return uv(st,p[0],17-p[2]) if a==1 and c[2]>=8 else patch(st,(9,3,12,8),(p[1]-2)/2,(15-p[2])/7)
 solid('Rear_Paper_Support',feed,trans,feeduv,metal)

 # Solid handset: raised end caps and a recessed central grip.
 handset={(x,y,z) for x in range(5,9) for y in range(5,19) for z in range(6,10) if z<8 or y<8 or y>=16}
 ob=solid('Removable_Handset',handset,trans,lambda p,c,a,sg:patch(st,(5,10,9,21),(p[0]-5)/4,(p[1]-5)/14),metal);pivot=Vector(trans((7,12,6)))
 for v in ob.data.vertices:v.co-=pivot
 ob.location=pivot;ob['note']='Separate for editing; source does not implement handset removal.'
 tray={(x,y,z) for x in range(9,23) for y in range(24,29) for z in range(2) if z==0 or x in [9,22] or y==28}
 solid('Lined_Output_Tray',tray,trans,lambda p,c,a,sg:patch(st,(9,25,23,29),(p[0]-9)/14,(p[1]-24)/5),metal)
 if mode=='service':
  ovs.append('fax_panel')
  solid('Service_Wiring_Board',{(x,0,z) for x in range(24,28) for z in range(2,5)},lambda p:trans((p[0],22.1+p[1]*.1,p[2])),lambda p,c,a,sg:patch('fax_panel',(24,21,28,25),(p[0]-24)/4,(5-p[2])/3),metal)
  hinge=bpy.data.objects.new('Service_Panel_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((28,24,2))
  ob=solid('Service_Cover',{(x,0,z) for x in range(24,28) for z in range(2,5)},lambda p:trans((p[0],24+p[1]*.25,p[2])),lambda p,c,a,sg:patch('fax',(24,21,28,25),(p[0]-24)/4,(5-p[2])/3),metal);ob.parent=hinge;ob.location=-hinge.location;hinge.rotation_euler.z=math.radians(105)
 if item:
  ovs.append(item);occupied={(x,y) for y in range(32) for x in range(32) if pixel(item,x,y)[3]>.99}
  # Thin original silhouette; art stays on its own closed layer rather than painted on the machine.
  def itemtrans(p):
   x,depth,y=p
   return trans((x,25+(y-24)*.6,1.2+depth*.12)) if mode=='receiving_paper' else trans((x,2+depth*.12,18-y*.65))
  def itemuv(p,c,a,sg):
   x,depth,y=p
   return uv(item,x,y) if a==1 else patch(item,(c[0],c[2],c[0]+1,c[2]+1),x-c[0],y-c[2])
  solid('Loaded_Item_'+item,{(x,0,y) for x,y in occupied},itemtrans,itemuv,metal)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['fax_build']=globals();print('Built',len(ASSETS),'fax presets')
