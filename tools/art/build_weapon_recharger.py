"""Original-atlas floor vent pumps and scrubbers."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/weapon_recharger';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/sec.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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



metal=material('Recharger_Original_Atlas');glow=material('Recharger_Indicator_Original',True)
def trans(p):return ((p[0]-16)*S,(14-p[1])*S,p[2]*S)
for mode in ['off','empty','charging','full','open','fail']:
 key='weapon_recharger_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ov=None if mode=='off' else 'recharger-'+mode;ASSETS[key]={'scene':scene,'parts':parts,'base_state':'recharger','overlay_states':[ov] if ov else []}
 cells={(x,y,z) for x in range(11,21) for y in range(9,19) for z in range(4)}
 cells-={(x,y,z) for x in range(15,17) for y in range(12,18) for z in [2,3]}
 cells-={(x,18,z) for x in range(12,20) for z in [1,2]}
 def tex(p,c,a,sg):
  x,y,z=p
  if a==2 and sg==1:return uv('recharger',x,y)
  if a==1 and sg==1:return uv('recharger',x,23-z)
  if a==0:return patch('recharger',(11 if sg<0 else 19,9,13 if sg<0 else 21,19),z/4,(y-9)/10)
  if a==1:return patch('recharger',(11,19,21,23),(x-11)/10,1-z/4)
  return patch('recharger',(11,19,21,23),(x-11)/10,(y-9)/10)
 solid('Continuous_Charging_Dock',cells,trans,tex,metal)
 hinge=bpy.data.objects.new('Service_Panel_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((16,19.02,1));hinge['axis']='X';hinge['note']='3D service-cover pivot; source uses an open overlay.'
 def cover_uv(p,c,a,sg):
  if a==1 and sg==1:return uv('recharger',p[0],23-p[2])
  return patch('recharger',(12,21,20,22),(p[0]-12)/8,(p[2]-1)/2)
 ob=solid('Service_Panel',{(x,0,z) for x in range(12,20) for z in [1,2]},lambda p:trans((p[0],19.02+p[1]*.12,p[2])),cover_uv,metal)
 for v in ob.data.vertices:v.co-=Vector(hinge.location)
 ob.parent=hinge;ob.location=(0,0,0)
 if mode=='open':
  hinge.rotation_euler.x=math.radians(105)
  def boarduv(p,c,a,sg):return patch('recharger-open',(12,20,20,22),(p[0]-12)/8,(3-p[2])/2)
  solid('Service_Electronics',{(x,0,z) for x in range(12,20) for z in [1,2]},lambda p:trans((p[0],18.2+p[1]*.2,p[2])),boarduv,metal)
 elif ov:
  verts=[];faces=[];coords=[]
  for y in range(32):
   for x in range(32):
    if pixel(ov,x,y)[3]<.99:continue
    corners=[(x,y),(x+1,y),(x+1,y+1),(x,y+1)];n=len(verts)
    verts.extend([trans((xx,yy,2.005)) if yy<=19 else trans((xx,19.15,23-yy)) for xx,yy in corners]);faces.append(tuple(range(n,n+4)));coords.append([uv(ov,xx,yy) for xx,yy in corners])
  me=bpy.data.meshes.new('Status_Overlay');me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
  for f,cc in zip(me.polygons,coords):
   for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
  bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
  ob=bpy.data.objects.new('Status_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=ov
 anchor=bpy.data.objects.new('Charging_Item_Mount',None);scene.collection.objects.link(anchor);anchor.location=trans((16,15,2));anchor['insertion_axis']='Z';anchor['purpose']='Optional future inserted item; no weapon included.'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('recharger')]}
bpy.app.driver_namespace['weapon_recharger_build']=globals();print('Built',len(ASSETS),'weapon recharger presets')
