"""Pixel-boundary requests terminal with original-atlas screens and service panel."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/requests_console';OUT.mkdir(parents=True,exist_ok=True)
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
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1
 return m
metal=material('Requests_Console_Housing_Original');glass=material('Requests_Console_Glass_Original',alpha=.25);glow=material('Requests_Console_Overlay_Original',True)
def trans(p):return ((p[0]-16.5)*S,-p[1]*S,(p[2]-16)*S)
def attach(ob,parent,pivot):
 for v in ob.data.vertices:v.co-=pivot
 ob.parent=parent;ob.location=(0,0,0)
def hinge(name,xyz):
 ob=bpy.data.objects.new(name,None);scene.collection.objects.link(ob);ob.location=trans(xyz);ob['axis']='X';return ob,Vector(ob.location)
def box(name,x0,x1,z0,z1,depth,thickness,st,rect):
 return solid(name,{(x,0,z) for x in range(x0,x1) for z in range(z0,z1)},lambda p:trans((p[0],depth+p[1]*thickness,p[2])),lambda p,c,a,sg:patch(st,rect,(p[0]-x0)/(x1-x0),(z1-p[2])/(z1-z0)),metal)
def overlay(st,name,depth,parent=None,pivot=None):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,4.015 if y>=20 else depth,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(glow);layer=me.uv_layers.new(name='Original_Atlas_UV')
 for f,tc in zip(me.polygons,coords):
  for li,co in zip(f.loop_indices,tc):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st
 if parent:attach(ob,parent,pivot)
 return ob


def front(z):return 4 if z>=11 else 4+(11-max(8,z))*1.0

def form(p):return trans((p[0],p[1]/4*front(p[2]),p[2]))
def screenlayer(st,left):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if (x<16)!=left or pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,4.015,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new('Screen');me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,co in zip(me.polygons,coords):
  for li,v in zip(f.loop_indices,co):u.data[li].uv=v
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new('Status_Overlay' if left else 'Message_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st
for label,st in [('off',None),('normal','req_comp0'),('new_message','req_comp1'),('high_priority','req_comp2'),('emergency','req_comp3'),('panel_open',None)]:
 key='requests_console_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'req_comp_off','overlay_states':[st] if st else (['req_comp_open'] if label=='panel_open' else [])}
 pixels={(x,31-y) for y in range(32) for x in range(32) if pixel('req_comp_off',x,y)[3]>.99}
 cells={(x,y,z) for x,z in pixels for y in range(4)}
 cells-={(x,3,z) for x in list(range(6,12))+list(range(16,26)) for z in range(14,22)}
 cells-={(x,3,11) for x in range(7,10)}
 def casing(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1:return uv('req_comp_off',x,32-z)
  if a==1:
   if 16<=c[2]<18:return patch('req_comp_off',(13,22,25,24),(x-4)/24,(18-z)/2)
   return patch('req_comp_off',(5,6,27,9),(x-4)/24,(27-z)/21)
  if a==2:return patch('req_comp_off',(5,6,27,9),(x-4)/24,y/4)
  return patch('req_comp_off',(4,9,6,20),y/4,(27-z)/21)
 solid('Continuous_Housing',cells,form,casing,metal)
 for name,x0,x1 in [('Status_Glass',6,12),('Message_Glass',16,26)]:
  solid(name,{(x,0,z) for x in range(x0,x1) for z in range(14,22)},lambda p:trans((p[0],3.85+p[1]*.13,p[2])),lambda p,c,a,sg:uv('req_comp_off',p[0],32-p[2]),glass)
 solid('Keyboard_Panel',{(x,0,z) for x in range(13,26) for z in range(8,10)},lambda p:form((p[0],4.015+p[1]*.04,p[2])),lambda p,c,a,sg:uv('req_comp_off',p[0],32-p[2]),metal)
 box('Service_Wiring',6,10,10,12,3.05,.35,'req_comp_open',(6,21,10,23))
 ph,pp=hinge('Service_Panel_Hinge',(5,4.05,10))
 ob=box('Service_Panel',5,11,10,12,4.05,.25,'req_comp_off',(6,20,12,22));attach(ob,ph,pp)
 if label=='panel_open':ph.rotation_euler.x=math.radians(110)
 if st:screenlayer(st,True);screenlayer(st,False)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if h in cache:
   old=ob.data;ob.data=cache[h]
   if old!=ob.data and old.users==0:bpy.data.meshes.remove(old)
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('req_comp')]}
bpy.app.driver_namespace['requests_console_build']=globals();print('Built',len(ASSETS),'requests console presets')
