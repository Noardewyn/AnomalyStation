"""Wall defibrillator mounts, original atlas and independent removable parts."""
from pathlib import Path
import bpy,bmesh,ast,json,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/defib_mount';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};CACHE={};FRAME=0
p=ROOT/'godot/assets/tg/icons/obj/machines/defib_mount.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 source=(ROOT/'tools/art'/f).read_text()
 tree=ast.parse(source);exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][FRAME if st=='emagged' else 0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def alpha(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+3]
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-6.5)*S)
metal=material('Defib_Mount_Original_Atlas')
pen=material('PENLITE_Mount_Original_Atlas');nodes=pen.node_tree.nodes;links=pen.node_tree.links
t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');mp.inputs['Location'].default_value=(.5,-64/96,0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
def remember(ob):
 me=ob.data
 sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(u.uv) for u in me.uv_layers.active.data]))
 key=hashlib.sha256(sig.encode()).hexdigest()
 if key in CACHE:ob.data=CACHE[key]
 else:CACHE[key]=me
 return ob
def overlay(st,parent):
 tx,ty,w,h=ST[st]['rects'][FRAME if st=='emagged' else 0];verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+3]<.99:continue
   pts=[(x,5.015,19-y),(x+1,5.015,19-y),(x+1,5.015,20-y),(x,5.015,20-y)];i=len(verts);verts.extend(trans(p) for p in pts);faces.append(tuple(range(i,i+4)));coords.extend(uv(st,p[0],20-p[2]) for p in pts)
 me=bpy.data.meshes.new(st+'_Overlay');me.from_pydata(verts,[],faces);lay=me.uv_layers.new()
 for po in me.polygons:
  for li in po.loop_indices:lay.data[li].uv=coords[me.loops[li].vertex_index]
 me.materials.append(metal);ob=bpy.data.objects.new(st+'_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob.parent=parent;ob.location=-parent.location;ob['source_state']=st;remember(ob)
variants=[('empty',False,None,None,False),('unpowered',True,None,None,False),('ready',True,'online','charge100',False),('locked',True,'online','charge100',True)]+[('charge'+str(n),True,'online','charge'+str(n),False) for n in [25,50,75]]+[('emagged_'+str(n),True,'emagged','charge100',False) for n in [0,1]]
for skin in ['standard','penlite']:
 for mode,loaded,safety,charge,locked in variants:
  FRAME=int(mode[-1]) if mode.startswith('emagged') else 0
  key='defib_mount_'+skin+'_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'penlite_mount' if skin=='penlite' else 'defibrillator_mount','overlay_states':ovs}
  cells={(x,y,z) for x in range(7,25) for z in range(13) for y in range(2) if alpha('defibrillator_mount',x,19-z)>.99}
  def puv(p,c,a,sg):
   if a==1 and sg==1:return uv('defibrillator_mount',p[0],20-p[2])
   return patch('defibrillator_mount',(7,8,10,19),p[1]/2 if a==0 else (p[0]-7)/18,(13-p[2])/13 if a!=2 else p[1]/2)
  ob=solid('Continuous_Wall_Mount',cells,trans,puv,metal)
  remember(ob)
  if skin=='penlite':ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=pen
  if loaded:
   ovs.append('defib')
   def duv(p,c,a,sg):
    x,y,z=p
    if a==1 and sg==1:return uv('defib',x,20-z)
    if a==2 and sg==1:return patch('defib',(9,9,23,10),(x-9)/14,(y-2)/3)
    return patch('defib',(9,15,23,18),(y-2)/3 if a==0 else (x-9)/14,(11-z)/9 if a!=2 else (y-2)/3)
   ob=solid('Removable_Defibrillator',{(x,y,z) for x in range(9,23) for y in range(2,5) for z in range(2,11)},trans,duv,metal);pivot=Vector(trans((16,2,2)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot;ob['removal_axis']='-Y away from wall';remember(ob);device=ob
   for st in [safety,charge]:
    if st:overlay(st,device);ovs.append(st+(' frame '+str(FRAME) if st=='emagged' else ''))
   if locked:
    ovs.append('clamps')
    for left in [True,False]:
     cc={(x,y,19-sy) for x in (range(9,11) if left else range(21,23)) for sy in range(7,20) for y in range(1,6) if alpha('clamps',x,sy)>.99}
     ob=solid('Left_Locking_Clamps' if left else 'Right_Locking_Clamps',cc,trans,lambda p,c,a,sg:uv('clamps',p[0],20-p[2]) if a==1 else patch('clamps',(9,7,10,9),p[1]/6,(13-p[2])/13),metal);pivot=Vector(trans((9 if left else 23,1,6.5)))
     for v in ob.data.vertices:v.co-=pivot
     ob.location=pivot;ob['release_axis']='Z; source describes sliding clamps';remember(ob)
bpy.app.driver_namespace['defib_mount_build']=globals()
