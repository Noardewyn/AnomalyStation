import bpy,bmesh,math,os,json
from mathutils import Vector
out=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../godot/assets/models/machines/suit_storage_classic')
os.makedirs(out,exist_ok=True)
scene=bpy.data.scenes.new('Suit_Storage_Classic_Review');bpy.context.window.scene=scene;scene.unit_settings.system='METRIC'
asset=bpy.data.collections.new('Suit_Storage_Asset');scene.collection.children.link(asset)
studio=bpy.data.collections.new('Suit_Storage_Studio');scene.collection.children.link(studio)
root=bpy.data.objects.new('Suit_Storage_Root',None);asset.objects.link(root)
def mat(name,c,metal=0,emission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=.46;p.inputs['Metallic'].default_value=metal
 p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emission
 return m
mats=[mat('Suit_Ochre',(.63,.30,.018),.25),mat('Suit_Graphite',(.025,.033,.055),.2),mat('Suit_Rail_Steel',(.20,.25,.34),.35),mat('Suit_Interior',(.012,.018,.030)),mat('Suit_Status_Ready',(.015,.6,.04),0,1),mat('Suit_Status_Cycle',(.8,.43,.008),0,1),mat('Suit_Door_Lights',(.8,.38,.008),0,.5)]
class Builder:
 def __init__(self):self.v=[];self.f=[];self.mi=[]
 def vert(self,p):self.v.append(tuple(p));return len(self.v)-1
 def face(self,f,m=1):self.f.append(tuple(f));self.mi.append(m)
 def ring(self,pts):return [self.vert(p) for p in pts]
 def bridge(self,p,q,m):
  for i in range(len(p)):j=(i+1)%len(p);self.face((p[i],p[j],q[j],q[i]),m)
 def obj(self,name):
  me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update()
  for m in mats:me.materials.append(m)
  for p,i in zip(me.polygons,self.mi):p.material_index=i
  bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
  o=bpy.data.objects.new(name,me);asset.objects.link(o);o.parent=root;return o
outline=[(-.34,-.40),(.34,-.40),(.48,-.26),(.48,.26),(.34,.40),(-.34,.40),(-.48,.26),(-.48,-.26)]
levels=[(0,.97),(.04,1.09),(.16,1.09),(.22,1),(.25,1),(.85,1),(1.32,1),(1.55,1),(1.60,1),(1.98,1),(2.02,1),(2.08,1.09),(2.22,1.09),(2.28,.97)]
b=Builder();rings=[b.ring([(x*s,y*s,z) for x,y in outline]) for z,s in levels]
b.face(tuple(reversed(rings[0])),0);b.face(rings[-1],0)
for k in range(len(rings)-1):
 z0=levels[k][0];z1=levels[k+1][0]
 for i in range(8):
  j=(i+1)%8;face=(rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i])
  body=z0>=.25 and z1<=1.98
  if body and i==0:continue
  # A true service opening in back wall, closed by the independent hatch.
  if i==4 and z0==.85 and z1==1.32:continue
  if i==2 and z0==.85 and z1==1.32:
   outer=b.ring([(b.v[v][0]+.17,b.v[v][1],b.v[v][2]) for v in face]);b.bridge(face,outer,0)
   inset=b.ring([(.65,-.205,.90),(.65,.205,.90),(.65,.205,1.27),(.65,-.205,1.27)])
   b.bridge(outer,inset,0);b.face(inset,3)
  else:b.face(face,0 if z1<=.22 or z0>=2.02 else (2 if i in [1,7] else 1))
innerpts=[(-.285,-.34),(.285,-.34),(.42,-.205),(.42,.205),(.285,.34),(-.285,.34),(-.42,.205),(-.42,-.205)]
inds=[4,5,6,7,8,9];ins=[b.ring([(x,y,levels[k][0]) for x,y in innerpts]) for k in inds]
b.face(ins[0],3);b.face(tuple(reversed(ins[-1])),3)
for q in range(len(ins)-1):
 for i in range(1,8):
  j=(i+1)%8;face=(ins[q][i],ins[q+1][i],ins[q+1][j],ins[q][j])
  z=levels[inds[q]][0]
  if i==4 and z==.85:
   outer=(rings[inds[q]][i],rings[inds[q]][j],rings[inds[q+1]][j],rings[inds[q+1]][i])
   inner=(ins[q][i],ins[q][j],ins[q+1][j],ins[q+1][i])
   b.bridge(outer,inner,3);continue
  if i==4 and z==1.55:
   # Shelf extruded directly from inside back wall: connected to shell.
   ext=b.ring([(b.v[v][0],-.12,b.v[v][2]) for v in face]);b.bridge(face,ext,2);b.face(ext,2)
  else:b.face(face,3)
 # front jambs joining inner and outer skin
 for i in [0,1]:
  b.face((rings[inds[q]][i],rings[inds[q+1]][i],ins[q+1][i],ins[q][i]),2)
# horizontal portal threshold and lintel
for q in [0,len(ins)-1]:
 k=inds[q];b.face((rings[k][0],rings[k][1],ins[q][1],ins[q][0]),2)
body=b.obj('Suit_Storage_Shell')
# Split control face with consistent shared edges, inset two integrated light lenses.
bm=bmesh.new();bm.from_mesh(body.data)
f=next(f for f in bm.faces if all(abs(v.co.x-.65)<1e-5 for v in f.verts) and f.material_index==3)
bmesh.ops.subdivide_edges(bm,edges=list(f.edges),cuts=1,use_grid_fill=True)
targets=[f for f in bm.faces if all(abs(v.co.x-.65)<1e-5 for v in f.verts) and f.material_index==3 and f.calc_center_median().y<0]
for f in targets:
 z=f.calc_center_median().z
 ret=bmesh.ops.inset_individual(bm,faces=[f],thickness=.055,depth=0,use_even_offset=True)
 f.material_index=4 if z>1.085 else 5
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
# Door: one closed connected mesh. Grid permits real recessed slots and a central seam.
d=Builder();xs=[-.331,-.235,-.185,-.018,.018,.185,.235,.331]
zs=[.258,.38,.55,.68,.85,.98,1.15,1.28,1.45,1.58,1.75,1.97]
grid=[[d.vert((x,-.425,z)) for x in xs] for z in zs]
for iz in range(len(zs)-1):
 for ix in range(len(xs)-1):
  f=[grid[iz][ix],grid[iz][ix+1],grid[iz+1][ix+1],grid[iz+1][ix]]
  slot=ix in [1,5] and iz in [1,3,5,7,9]
  seam=ix==3
  if slot or seam:
   dep=.014 if slot else .004
   r=d.ring([(d.v[v][0],-.425+dep,d.v[v][2]) for v in f])
   d.bridge(f,r,1);d.face(r,3)
  else:d.face(f,1 if ix not in [0,6] else 2)
# Back face follows full boundary to avoid T-junctions.
boundary=grid[0]+[row[-1] for row in grid[1:]]+list(reversed(grid[-1][:-1]))+[row[0] for row in reversed(grid[1:-1])]
back=d.ring([(d.v[v][0],-.36,d.v[v][2]) for v in boundary]);d.bridge(boundary,back,1);d.face(tuple(reversed(back)),3)
door=d.obj('Suit_Storage_Door')
hinge=Vector((-.338,-.40,0))
for v in door.data.vertices:v.co-=hinge
door.location=hinge;door['open_angle_degrees']=-110
# Service hatch: chamfered rectangular closed mesh, own hinge.
h=Builder()
poly=[(-.326,.866),(.326,.866),(.326,1.304),(-.326,1.304)]
r0=h.ring([(x,.397,z) for x,z in poly]);r1=h.ring([(x,.412,z) for x,z in poly])
r2=h.ring([(x*.96,.424,1.085+(z-1.085)*.94) for x,z in poly])
h.face(tuple(reversed(r0)),1);h.bridge(r0,r1,1);h.bridge(r1,r2,2);h.face(r2,1)
panel=h.obj('Suit_Storage_Service_Hatch')
hinge2=Vector((0,.397,.866))
for v in panel.data.vertices:v.co-=hinge2
panel.location=hinge2
for name,pos in [('Content_Suit',(0,.05,.95)),('Content_Helmet',(0,.05,1.70)),('Content_Storage',(0,-.05,.42))]:
 o=bpy.data.objects.new(name,None);asset.objects.link(o);o.parent=root;o.location=pos;o.empty_display_size=.07
root['front_direction']='-Y';root['contents']='empty; dynamic item anchors only'
root['state']='closed';root['door_open']=False
# Studio
world=bpy.data.worlds.new('Suit_Studio_World');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.45,.50,.60,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
me=bpy.data.meshes.new('Suit_Studio_Ground');me.from_pydata([(-100,-100,-.006),(100,-100,-.006),(100,100,-.006),(-100,100,-.006)],[],[(0,1,2,3)])
o=bpy.data.objects.new(me.name,me);studio.objects.link(o);me.materials.append(mat('Suit_Studio_Ground',(.24,.265,.30)))
for name,loc,power,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-1,4),450,3),('Rim',(1,4,5),750,3)]:
 data=bpy.data.lights.new('Suit_'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
 o=bpy.data.objects.new(data.name,data);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Suit_Review_Camera');data.type='ORTHO';data.ortho_scale=3.1
cam=bpy.data.objects.new(data.name,data);studio.objects.link(cam);cam.location=(3,-5,3.5);cam.rotation_euler=(Vector((0,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
bpy.context.view_layer.update()
def audit(o):
 bm=bmesh.new();bm.from_mesh(o.data);todo=set(bm.verts);components=0
 while todo:
  components+=1;stack=[todo.pop()]
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in todo:todo.remove(w);stack.append(w)
 info={'vertices':len(bm.verts),'faces':len(bm.faces),'components':components,'non_manifold_edges':sum(not e.is_manifold for e in bm.edges),'triangles':sum(len(f.verts)-2 for f in bm.faces)}
 bm.free();return info
report={o.name:audit(o) for o in asset.objects if o.type=='MESH'}
result=report

