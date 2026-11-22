import bpy,bmesh
asset=bpy.data.collections['Suit_Storage_Asset'];root=bpy.data.objects['Suit_Storage_Root'];mats=list(bpy.data.objects['Suit_Storage_Shell'].data.materials)
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

import bpy,bmesh
body=bpy.data.objects['Suit_Storage_Shell'];door=bpy.data.objects['Suit_Storage_Door']
# Remove segmented overlapping seam walls by rebuilding the same door as one continuous recessed center strip.
old=door.data
# Reuse mesh builder definitions from build script.
d=Builder();xs=[-.331,-.235,-.185,-.018,.018,.185,.235,.331]
zs=[.258,.38,.55,.68,.85,.98,1.15,1.28,1.45,1.58,1.75,1.93]
grid=[[d.vert((x,-.418 if abs(x)<.02 else -.425,z)) for x in xs] for z in zs]
for iz in range(len(zs)-1):
 for ix in range(len(xs)-1):
  f=[grid[iz][ix],grid[iz][ix+1],grid[iz+1][ix+1],grid[iz+1][ix]]
  if ix in [1,5] and iz in [1,3,5,7,9]:
   r=d.ring([(d.v[v][0],-.411,d.v[v][2]) for v in f]);d.bridge(f,r,1);d.face(r,6)
  else:d.face(f,3 if ix==3 else 1 if ix not in [0,6] else 2)
boundary=grid[0]+[row[-1] for row in grid[1:]]+list(reversed(grid[-1][:-1]))+[row[0] for row in reversed(grid[1:-1])]
back=d.ring([(d.v[v][0],-.36,d.v[v][2]) for v in boundary]);d.bridge(boundary,back,1);d.face(tuple(reversed(back)),3)
temp=d.obj('Door_Rebuild');door.data=temp.data;bpy.data.objects.remove(temp,do_unlink=True)
for v in door.data.vertices:v.co-=door.location
for v in body.data.vertices:
 if abs(v.co.z-1.98)<1e-5:v.co.z=1.94
# Pair of top status lights built into the connected shell.
bm=bmesh.new();bm.from_mesh(body.data);bm.normal_update()
face=next(f for f in bm.faces if all(abs(v.co.y+.4)<1e-5 for v in f.verts) and abs(f.calc_center_median().z-1.98)<1e-5)
edges=[e for e in face.edges if abs(e.verts[0].co.z-e.verts[1].co.z)<1e-5]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
faces=[f for f in bm.faces if all(abs(v.co.y+.4)<1e-5 for v in f.verts) and abs(f.calc_center_median().z-1.98)<1e-5]
for f in faces:
 bmesh.ops.inset_individual(bm,faces=[f],thickness=.018,depth=0,use_even_offset=True)
 cx=f.calc_center_median().x
 for v in f.verts:v.co.x=cx+(v.co.x-cx)*.18
 f.material_index=4
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
# Hatch overlaps the rim of the service opening, eliminating light leaks.
panel=bpy.data.objects['Suit_Storage_Service_Hatch']
for v in panel.data.vertices:
 v.co.x*=.35/.326
 worldz=v.co.z+panel.location.z
 v.co.z=1.085+(worldz-1.085)*(.49/.438)-panel.location.z
# Warm saturated cap rather than washed out beige.
p=bpy.data.materials['Suit_Ochre'].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.72,.29,.006,1);p.inputs['Roughness'].default_value=.58
m=bpy.data.materials['Suit_Door_Lights'];p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.009,.012,.020,1);p.inputs['Emission Strength'].default_value=0
bpy.context.view_layer.update()
result={o.name:audit(o) for o in asset.objects if o.type=='MESH'}

