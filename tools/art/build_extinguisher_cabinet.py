"""Pixel-boundary APC enclosure with original-atlas removable covers and cell."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/structures/extinguisher_cabinet';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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
metal=material('Cabinet_Original');glass=material('Cabinet_Glass_Original',alpha=.18);glow=material('Cabinet_Overlay_Original',True)
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-16)*S)
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

for kind in ['empty','default','advanced','mini','crafted']:
 for opened in [False,True]:
  key='extinguisher_cabinet_'+kind+('_open' if opened else '_closed');state='extinguisher'+('' if kind=='empty' else '_'+kind)
  scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':state,'overlay_states':['door_open' if opened else 'door']}
  cells={(x,y,z) for x in range(10,22) for y in range(6) for z in range(6,23)}-{(x,y,z) for x in range(12,20) for y in range(1,6) for z in range(7,22)}
  def housing(p,c,a,sg):
   if a==1 and sg==1:return uv('extinguisher',p[0],32-p[2])
   if a==1:return patch('extinguisher',(10,9,12,26),(p[0]-10)/12,(23-p[2])/17)
   if a==2:return patch('extinguisher',(11,7,21,9),(p[0]-10)/12,p[1]/6)
   return patch('extinguisher',(10,9,12,26),p[1]/6,(23-p[2])/17)
  solid('Lined_Cabinet',cells,trans,housing,metal)
  # Stepped shelf and mounting clips sit inside the lined recess.
  box('Bottom_Cradle',13,19,7,9,1,2,'extinguisher',(13,22,19,25))
  for x in [13,18]:box('Mounting_Clip',x,x+1,17,20,1,.5,'extinguisher',(x,12,x+1,15))
  if kind!='empty':
   root=bpy.data.objects.new('Removable_Extinguisher',None);scene.collection.objects.link(root);start=len(parts)
   xa=15 if kind=='mini' else 14;bottom=9 if kind=='mini' else 8;top=15 if kind=='mini' else 17
   cross={(x,y) for x in range(xa,xa+4) for y in range(1,5) if not(x in [xa,xa+3] and y in [1,4])}
   body={(x,y,z) for x,y in cross for z in range(bottom,top)}|{(x,y,z) for x in range(xa+1,xa+3) for y in [2,3] for z in [bottom-1,top]}
   def cylindertex(p,c,a,sg):
    if a==1:return uv(state,p[0],32-p[2])
    return patch(state,(xa,32-top,xa+4,32-bottom), (p[1]-1)/4 if a==0 else (p[0]-xa)/4,(top-p[2])/(top-bottom))
   solid('Stepped_Cylinder',body,trans,cylindertex,metal)
   box('Valve_Neck',xa+1,xa+3,top+1,top+2,2,2,state,(xa,32-top-3,xa+4,32-top))
   handle={(x,0,z) for x in range(xa,xa+5) for z in range(top+2,top+4) if z==top+3 or x in [xa,xa+4]}
   solid('Trigger_Handle',handle,lambda p:trans((p[0],2.5+p[1],p[2])),lambda p,c,a,sg:patch(state,(13,10,19,14),(p[0]-xa)/5,(top+4-p[2])/2),metal)
   hose={(xa-1,0,z) for z in range(bottom+2,top+2)}|{(x,0,top+2) for x in range(xa-1,xa+2)}
   solid('Stepped_Hose',hose,lambda p:trans((p[0],2+p[1],p[2])),lambda p,c,a,sg:patch(state,(12,14,14,21),.5,(top+3-p[2])/(top-bottom+1)),metal)
   pivot=Vector(trans((xa+2,3,bottom-1)));root.location=pivot
   for ob in parts[start:]:attach(ob,root,pivot)
   trigger=next(o for o in parts[start:] if o.name.startswith('Trigger_Handle'))
   trigger_pivot=Vector(trans((xa+2,3,top+3)))-pivot
   for v in trigger.data.vertices:v.co-=trigger_pivot
   trigger.location=trigger_pivot;trigger['pivot_purpose']='Trigger lever axis X'
  # Right-hand hinge follows the door_open source; rotate the same assembly.
  dh,dp=hinge('Door_Hinge',(22,6.05,14));dh['axis']='Z'
  frame={(x,0,z) for x in range(10,22) for z in range(6,23)}-{(x,0,z) for x in range(13,19) for z in range(8,21)}
  def dooruv(p,c,a,sg):
   if a==1:return uv('door',p[0],32-p[2])
   return patch('door',(10,9,13,25),(p[1]),(23-p[2])/17)
  ob=solid('Door_Frame',frame,lambda p:trans((p[0],6.05+p[1]*.35,p[2])),dooruv,metal);attach(ob,dh,dp)
  ob=solid('Window_Glass',{(x,0,z) for x in range(13,19) for z in range(8,21)},lambda p:trans((p[0],6.15+p[1]*.15,p[2])),lambda p,c,a,sg:patch('door',(13,11,19,24),(p[0]-13)/6,(21-p[2])/13),glass);attach(ob,dh,dp)
  ob=box('Door_Handle',10,11,12,17,6.4,.6,'door',(10,15,12,20));attach(ob,dh,dp)
  if opened:dh.rotation_euler.z=math.radians(105)
key='extinguisher_cabinet_frame';scene=bpy.data.scenes.new(key);parts=[]
for src in ASSETS['extinguisher_cabinet_empty_open']['parts']:
 if src.parent:continue
 ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
ASSETS[key]={'scene':scene,'parts':parts,'base_state':'extinguisher','overlay_states':[]}
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if h in cache:
   old=ob.data;ob.data=cache[h]
   if old!=ob.data and old.users==0:bpy.data.meshes.remove(old)
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith(('extinguisher','door'))]}
bpy.app.driver_namespace['extinguisher_build']=globals();print('Built',len(ASSETS),'cabinet presets')
