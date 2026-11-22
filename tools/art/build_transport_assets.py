"""Build original-atlas railings, conveyors and plastic flaps in live Blender."""
from pathlib import Path
import bpy, bmesh, json, math, struct, hashlib, re, time
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/structures/transport';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32
ASSETS={};IM={};META={};PIX={};MATS={}
for key in ['railings','machines/recycling','structures']:
 p=ROOT/('godot/assets/tg/icons/obj/'+key+'.png')
 im=bpy.data.images.load(str(p),check_existing=False);IM[key]=im;PIX[key]=list(im.pixels)
 META[key]=json.loads(p.with_suffix('.json').read_text())
def state(key,name):return next(s for s in META[key]['states'] if s['name']==name)
# A state's rects run frame by frame within each direction: its first frame of direction di.
def rect(key,name,di):
 st=state(key,name);return st['rects'][di*st['frames']]
def rgba(key,name,x,y,di=0):
 tx,ty,w,h=rect(key,name,di);im=IM[key];i=((im.size[1]-1-ty-y)*im.size[0]+tx+x)*4
 return PIX[key][i:i+4]
def uv(key,name,x,y,di=0):
 tx,ty,w,h=rect(key,name,di);im=IM[key]
 return ((tx+x)/im.size[0],1-(ty+y)/im.size[1])
def mat(name,key,alpha=1):
 if name in MATS:return MATS[name]
 m=bpy.data.materials.new(name);m.use_nodes=True
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.7;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM[key];t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 MATS[name]=m;return m
railmat=mat('Railing_Original_Atlas','railings')
chassismat=mat('Conveyor_Chassis_Original_Atlas','machines/recycling')
beltmat=mat('Conveyor_Belt_Original_Atlas','machines/recycling')
framemat=mat('Flaps_Frame_Original_Atlas','structures')
flapmat=mat('Flaps_Original_Atlas','structures')
kitchenmat=mat('Flaps_Kitchen_Original_Atlas','structures',150/255)
def begin(name,family,description):
 global scene,parts
 scene=bpy.data.scenes.new(name);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ASSETS[name]={'scene':scene,'parts':parts,'family':family,'description':description}
def solid(name,cells,transform,tex,material,simplify=True):
 cells=set(cells);verts=[];faces=[];uvs=[];lut={}
 def vi(p):
  if p not in lut:lut[p]=len(verts);verts.append(transform(p))
  return lut[p]
 for c in sorted(cells):
  for axis in range(3):
   for sign in [-1,1]:
    neighbor=list(c);neighbor[axis]+=sign
    if tuple(neighbor) in cells:continue
    other=[i for i in range(3) if i!=axis];corners=[]
    for a,b in [(0,0),(1,0),(1,1),(0,1)]:
     p=list(c);p[axis]+=sign>0;p[other[0]]+=a;p[other[1]]+=b;corners.append(tuple(p))
    faces.append([vi(p) for p in corners]);uvs.append([tex(p,c,axis,sign) for p in corners])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(material);layer=me.uv_layers.new(name='Original_Atlas_UV')
 for face,coords in zip(me.polygons,uvs):
  for li,co in zip(face.loop_indices,coords):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 assert all(e.is_manifold for e in bm.edges),(name,'initial non-manifold')
 if simplify:bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'})
 bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.dissolve_degenerate(bm,dist=.0000001,edges=list(bm.edges));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 assert all(e.is_manifold for e in bm.edges),(name,'non-manifold')
 bm.to_mesh(me);bm.free();me.update();me.calc_loop_triangles()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);return ob
def rail_cells(length=32):
 # Original straight silhouette: three posts, two continuous rails, stepped feet.
 return {(x,y,z) for x in range(length) for y in range(2) for z in range(14)
         if rgba('railings','railing',x,47-z)[3]>.99}
def railtex(p,c,a,sign):
 x,y,z=p
 if a==1 and c[1]<2:return uv('railings','railing',x+.001*(16-x),48-z+.001*(z-7))
 if a==2:return uv('railings','railing',1.05+(x%32)*.93,34.05+(y%2)*.4)
 return uv('railings','railing',1.1+(y%2)*.8,46.8-z*.75)
for variant in ['straight','corner','corner_connector','end_cap']:
 begin('railing_'+variant,'railing','Reusable border railing; rotate modules in 90 degree increments.')
 cells=rail_cells()
 if variant=='corner':
  # An L lies along two borders of one tile. The joint is a true solid union.
  cells|={(31+y,1+x,z) for x,y,z in rail_cells()}
  cells|={(x,y,z) for x in [31,32] for y in [0,1] for z in range(14)}
 elif variant=='corner_connector':cells={(x,y,z) for x in range(2) for y in range(2) for z in range(14)}|{(x,y,z) for x in range(4) for y in range(2) for z in [5,6,12,13]}|{(x,y,z) for x in range(2) for y in range(4) for z in [5,6,12,13]}
 elif variant=='end_cap':cells={(x,y,z) for x in range(2) for y in range(2) for z in range(14)}|{(x,y,z) for x in range(3) for y in range(2) for z in [5,6,12,13]}
 def rt(p,c,a,sg):
  if variant=='straight':return railtex(p,c,a,sg)
  if variant=='corner' and c[1]<2 and c[0]<31:return railtex(p,c,a,sg)
  if variant=='corner' and c[1]>=2:
   return railtex((p[1]-1,p[0]-31,p[2]),(c[1]-1,c[0]-31,c[2]),{0:1,1:0,2:2}[a],sg)
  # Match vertical post grain and horizontal rail grain on both arms.
  if a==2:return uv('railings','railing',4.1+(p[0]+p[1])%10,34.15)
  if c[2] in [5,6,12,13]:return uv('railings','railing',4.1+(p[0]+p[1])%10,34.1+(13-p[2])%2*.8)
  return uv('railings','railing',1.1+(p[0]+p[1])%2*.8,46.8-p[2]*.75)
 ob=solid('Continuous_Railing',cells,lambda p:((p[0]-16)*S,(p[1]-1)*S,p[2]*(.75/14)),rt,railmat)
 ob['source_states']='railing, railing_corner, railing_end, railing_end_flip';ob['grid_m']=.9

for shape,di in [('straight',0),('corner',4)]:
 occupied={(x,y) for x in range(32) for y in range(32) if rgba('machines/recycling','conveyor0',x,y,di)[3]>.99}
 if shape=='straight':rails={(x,y) for x,y in occupied if x<5 or x>=26}
 else:
  rails={(x,y) for x,y in occupied if y<5 or x>=26 or (x<5 and y>=26) or (y==5 and x>=25)}
 belt=occupied-rails
 for flipped in [False,True]:
  for operating,label in [(0,'stopped'),(1,'forward'),(-1,'reverse')]:
   st='conveyor'+str(operating)+('-flipped' if flipped else '')
   name='conveyor_'+shape+('_flipped' if flipped else '')+'_'+label
   begin(name,'conveyor','Separate belt and chassis; every belt face lies in the first frame of its state, so the client plays the state on it.')
   def trans(p):return ((p[0]-16)*S,(16-p[1])*S,p[2]*S)
   def ct(source,trim='conveyor0',td=0):
    def tc(p,c,a,sg):
     if a==2 and sg==1:return uv('machines/recycling',source,p[0],p[1],di)
     # Original edge rail pixels form side/bottom trim, no solid-color material.
     if a==0:return uv('machines/recycling',trim,1.08+p[2]*.94,p[1],td)
     if a==1:return uv('machines/recycling',trim,1.08+p[2]*.94,p[0],td)
     return uv('machines/recycling',trim,5.1+p[0]*.64,5.1+p[1]*.64,td)
    return tc
   chassis={(x,y,0) for x,y in occupied}|{(x,y,z) for x,y in rails for z in [1,2]}
   solid('Chassis_and_Guide_Rails',chassis,trans,ct('conveyor0'),chassismat)
   ob=solid('Replaceable_Belt',{(x,y,1) for x,y in belt},trans,ct(st,st,di),beltmat);ob['source_state']=st;ob['source_direction_index']=di;ob['frames']=state('machines/recycling',st)['frames']
   ASSETS[name]['state']=st;ASSETS[name]['source_direction_index']=di

for variant,material in [('standard',flapmat),('opaque',flapmat),('kitchen',kitchenmat)]:
 begin('plastic_flaps_'+variant,'plastic_flaps','Four independently hinged strips; original image on front and back.')
 def trans(p):return ((p[0]-16)*S,p[1]*.008,p[2]*(1.8/32))
 def ft(p,c,a,sg):
  if a==1:return uv('structures','plasticflaps',p[0]*.998+.03,32-p[2]*.998-.03)
  if a==2:return uv('structures','plasticflaps',p[0]*.998+.03,1.1+p[1]*.7)
  return uv('structures','plasticflaps',.1+(p[1]+1)*.25,32-p[2]*.998-.03)
 cells={(x,y,z) for x in range(32) for y in [-1,0,1] for z in range(32) if x in [0,1,30,31] or z>=29 or z==0}
 solid('Mounting_Frame',cells,trans,ft,framemat)
 for n,(a,b) in enumerate([(2,9),(9,15),(15,22),(22,30)]):
  ob=solid('Hinged_Strip_'+str(n+1),{(x,0,z) for x in range(a,b) for z in range(1,29)},trans,ft,material)
  pivot=Vector((((a+b)/2-16)*S,0,29*1.8/32))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot;ob['hinge_axis_blender']='X';ob['source_overlay']='plasticflaps';ob['flaps_alpha']=150 if variant=='kitchen' else 255
 ASSETS['plastic_flaps_'+variant]['opacity_blocks_sight']=variant=='opaque'

# Share identical topology, UV and materials across all states and scenes.
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data
  sig=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(u.uv) for u in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['transport_build']=globals()
print('Built',len(ASSETS),'assets;',len(cache),'unique meshes')
