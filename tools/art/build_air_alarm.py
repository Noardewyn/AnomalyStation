"""Original-atlas Air Alarm, including remote sensor and construction states."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/air_alarm';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'shared_boundary_mesh','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def patch(st,box,u,v):
 x0,y0,x1,y1=box;return uv(st,x0+.02+(x1-x0-.04)*u,y0+.02+(y1-y0-.04)*v)
def mat(name,alpha=1,emission=False):
 if name in MATS:return MATS[name]
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.65;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emission:
  m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1.2
 MATS[name]=m;return m
metal=mat('Air_Alarm_Housing_Original');glass=mat('Air_Alarm_Glass_Original',.88);glow=mat('Air_Alarm_Overlay_Original',emission=True)
def trans(p):return ((p[0]-16.5)*S,-p[1]*S,(p[2]-8)*S)
def attach(ob,parent,pivot):
 for v in ob.data.vertices:v.co-=pivot
 ob.parent=parent;ob.location=(0,0,0)
def casing_tex(p,c,a,sg):
 x,y,z=p
 if a==1 and sg==1 and p[1]<=1.001:return patch('alarm_b1',(6,13,27,19),(x-6)/20,(12-z)/7)
 if a==1 and sg==1:return uv('alarmp',x,24-z)
 if a==1:return patch('alarm_b1',(6,13,27,19),(x-5)/23,(13-z)/9)
 if a==2:return patch('alarmp',(6,11,27,13),(x-5)/23,y/4)
 return patch('alarmp',(5,11,7,19),y/4,(13-z)/9)
def overlay_mesh(st,upper,hinge,pivot):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if (y<11)!=upper or pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts)
   verts.extend([trans((xx,3.025 if upper else 4.18,24-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new('Status_Overlay');me.from_pydata(verts,[],faces);me.materials.append(glow);layer=me.uv_layers.new(name='Original_Atlas_UV')
 for face,tc in zip(me.polygons,coords):
  for li,co in zip(face.loop_indices,tc):layer.data[li].uv=co
 ob=bpy.data.objects.new('Terminal_Overlay' if upper else 'Display_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st
 if not upper:attach(ob,hinge,pivot)

variants=[]
for remote in [False,True]:
 for name,ov in [('normal','alarm0'),('warning','alarm2'),('hazard','alarm1'),('unpowered',None)]:variants.append(('remote_'+name if remote else name,'alarmp_remote' if remote else 'alarmp',ov,remote))
variants.extend([('frame','alarm_b1',None,False),('circuit_unwired','alarm_b2',None,False),('panel_open','alarmx',None,False)])
for label,base,ov,remote in variants:
 key='air_alarm_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlay_states':[ov] if ov else []}
 cells={(x,y,z) for x in range(5,28) for y in range(4) for z in range(4,13) if pixel('alarmp',x,23-z)[3]>.99}
 cells-={(x,y,z) for x in range(6,26) for y in [1,2,3] for z in range(5,12)}
 solid('Lined_Housing',cells,trans,casing_tex,metal)
 fins={(x,y,3) for x in range(6,27) for y in range(4)}|{(x,y,z) for x in range(7,27,2) for y in range(4) for z in range(3)}
 def fintex(p,c,a,sg):
  if a==1 and sg==1:return uv('alarmp',p[0],24-p[2])
  return patch('alarmp',(6,20,27,23),(p[0]-6)/21,(3-p[2])/3)
 solid('Lower_Vent_Fins',fins,trans,fintex,metal)
 terminals={(x,y,13) for x in range(6,15) for y in [1,2]}|{(x,y,z) for x in range(7,13) for y in [1,2] for z in range(14,17)}
 def tt(p,c,a,sg):
  if a==1 and sg==1:return uv('alarmp',p[0],24-p[2])
  return patch('alarmp',(7,8,14,11),(p[0]-6)/9,(17-p[2])/4)
 solid('Upper_Sensor_Terminals',terminals,trans,tt,metal)
 connector={(x,y,z) for x in range(18,27) for y in [1,2] for z in [13,14,15]}
 if remote:connector|={(x,y,16) for x in [20,22,24] for y in [1,2]}
 def ct(p,c,a,sg):
  if a==1 and sg==1:return uv('alarmp_remote' if remote else 'alarmp',p[0],24-p[2])
  return patch('alarmp',(18,9,27,11),(p[0]-18)/9,(17-p[2])/4)
 solid('Remote_Connector' if remote else 'Upper_Connector',connector,trans,ct,metal)
 if label!='frame':
  boardstate='alarmx' if label=='panel_open' else 'alarm_b2'
  solid('Removable_Circuit_Board',{(x,0,z) for x in range(6,27) for z in range(5,11)},lambda p:trans((p[0],1+p[1]*.15,p[2])),lambda p,c,a,sg:patch(boardstate,(6,13,27,19),(p[0]-6)/21,(11-p[2])/6),metal)
 pivot=Vector(trans((16.5,3.8,13)));hinge=bpy.data.objects.new('Service_Cover_Hinge',None);scene.collection.objects.link(hinge);hinge.location=pivot;hinge['axis']='X'
 cover={(x,0,z) for x in range(5,28) for z in range(4,13) if pixel('alarmp',x,23-z)[3]>.99} - {(x,0,z) for x in range(8,13) for z in range(7,11)}
 def lidtex(p,c,a,sg):
  if a==1 and sg==1:return uv('alarmp',p[0],24-p[2])
  if a==1:return patch('alarm_b1' if label=='frame' else 'alarm_b2',(6,5,27,12),(p[0]-5)/23,(13-p[2])/9)
  return patch('alarmp',(6,11,27,13),(p[0]-5)/23,(13-p[2])/9)
 ob=solid('AIR_Front_Cover',cover,lambda p:trans((p[0],3.8+p[1]*.2,p[2])),lidtex,metal);attach(ob,hinge,pivot)
 ob=solid('Display_Glass',{(x,0,z) for x in range(8,13) for z in range(7,11)},lambda p:trans((p[0],3.95+p[1]*.2,p[2])),lambda p,c,a,sg:patch('alarmp',(8,13,13,17),(p[0]-8)/5,(11-p[2])/4),glass);attach(ob,hinge,pivot)
 if ov:
  overlay_mesh(ov,False,hinge,pivot);overlay_mesh(ov,True,hinge,pivot)
 if label in ['frame','circuit_unwired','panel_open']:hinge.rotation_euler.x=math.radians(-110)
 if label=='panel_open':
  for side,xs in [('Left',range(3,6)),('Right',range(27,30))]:
   source={(x,23-yy) for x in xs for yy in range(19,29) if pixel('alarmx',x,yy)[3]>.99};cells=set(source)
   while True:
    additions=set()
    for x,z in cells:
     for dx in [-1,1]:
      if (x+dx,z+1) in cells and (x+dx,z) not in cells and (x,z+1) not in cells:additions.add((x+dx,z))
    if not additions:break
    cells|=additions
   def wt(p,c,a,sg):
    x,z=min(source,key=lambda q:abs(q[0]-c[0])+abs(q[1]-c[2]));return uv('alarmx',x+.5,23-z+.5)
   solid('Service_Wire_'+side,{(x,0,z) for x,z in cells},lambda p:trans((p[0],3+p[1]*.2,p[2])),wt,metal,simplify=False)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(u.uv) for u in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['air_alarm_build']=globals();print('Built',len(ASSETS),'Air Alarm variants')
