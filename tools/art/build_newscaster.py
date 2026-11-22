"""Pixel-boundary APC enclosure with original-atlas removable covers and cell."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/newscaster';OUT.mkdir(parents=True,exist_ok=True)
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
metal=material('Newscaster_Housing_Original');glass=material('Newscaster_Glass_Original',alpha=.80);glow=material('Newscaster_Overlay_Original',True)
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

crackmat=material('Newscaster_Overlay_Unlit');bs=next(n for n in crackmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');tx=next(n for n in crackmat.node_tree.nodes if n.type=='TEX_IMAGE');crackmat.node_tree.links.new(tx.outputs['Alpha'],bs.inputs['Alpha'])
def front(z):
 if z>=11:return 4
 if z>=8:return 4+(11-z)*1.1
 return 7.3-(8-z)*.35

def form(p):return trans((p[0],p[1]/4*front(p[2]),p[2]))
def layer(st,name):
 vs=[];fs=[];tc=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(vs)
   depth=4.04 if st.startswith('crack') or y<8 else 4.005
   vs.extend([trans((xx,depth,32-yy)) for xx,yy in cr]);fs.append(tuple(range(n,n+4)));tc.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(crackmat if st.startswith('crack') else glow);ul=me.uv_layers.new(name='Original_Atlas_UV')
 for f,co in zip(me.polygons,tc):
  for li,v in zip(f.loop_indices,co):ul.data[li].uv=v
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st
variants=[('off',None,None),('normal','newscaster_normal',None),('wanted','newscaster_wanted',None),('wanted_alert','newscaster_wanted','newscaster_alert')]
for i in range(1,4):
 if i<3:variants.append(('normal_crack_'+str(i),'newscaster_normal','crack'+str(i)))
 variants.append(('off_crack_'+str(i),None,'crack'+str(i)))
variants.append(('assembly',None,None))
for label,screen,extra in variants:
 key='newscaster_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];base='newscaster_assembly' if label=='assembly' else 'newscaster_off';assembly=label=='assembly'
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlay_states':[v for v in [screen,extra] if v]}
 cells={(x,y,31-row) for row in range(32) for x in range(32) for y in range(4) if pixel(base,x,row)[3]>.99}
 if not assembly:
  cells-={(x,3,z) for x in range(7,25) for z in range(15,24)}
  cells-={(x,y,6) for x in range(9,18) for y in [2,3]}
 def housing(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1:
   if not assembly and z<=7 and y<=2:return patch('newscaster_off',(7,20,25,22),(x-7)/18,(7-z)/2)
   return uv(base,x,32-z)
  if a==1:
   if 16<=c[2]<18:return patch('newscaster_off',(8,22,19,24),(x-4)/24,(18-z)/2)
   return patch('newscaster_off',(7,3,23,7),(x-4)/24,(29-z)/25)
  if a==2:return patch('newscaster_off',(6,4,23,7),(x-4)/24,y/4)
  return patch('newscaster_off',(5,7,7,20),y/4,(29-z)/25)
 solid('Continuous_Housing',cells,form,housing,metal)
 if not assembly:
  ob=solid('Recessed_Display_Glass',{(x,0,z) for x in range(7,25) for z in range(15,24)},lambda p:trans((p[0],3.88+p[1]*.10,p[2])),lambda p,c,a,sg:uv('newscaster_off',p[0],32-p[2]),glass)
  # Keyboard lies on the sloping lower face, so top-down sprite perspective becomes real depth.
  solid('Keyboard_Panel',{(x,0,z) for x in range(8,19) for z in range(8,10)},lambda p:form((p[0],4.015+p[1]*.04,p[2])),lambda p,c,a,sg:uv('newscaster_off',p[0],32-p[2]),metal)
  if screen:layer(screen,'Screen_Overlay')
  if extra:layer(extra,'Damage_Overlay' if extra.startswith('crack') else 'Alert_Overlay')
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if h in cache:
   old=ob.data;ob.data=cache[h]
   if old!=ob.data and old.users==0:bpy.data.meshes.remove(old)
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith(('newscaster','crack'))]}
bpy.app.driver_namespace['newscaster_build']=globals();print('Built',len(ASSETS),'Newscaster presets')

