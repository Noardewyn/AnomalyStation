import bpy,bmesh,math,os,json
from mathutils import Vector
out=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../godot/assets/models/machines/thermomachine');os.makedirs(out,exist_ok=True)
scene=bpy.data.scenes.new('Thermomachine_Review');bpy.context.window.scene=scene;scene.unit_settings.system='METRIC'
asset=bpy.data.collections.new('Thermomachine_Asset');scene.collection.children.link(asset)
studio=bpy.data.collections.new('Thermomachine_Studio');scene.collection.children.link(studio)
root=bpy.data.objects.new('Thermomachine_Root',None);asset.objects.link(root)
def mat(n,c,metal=0,emit=0):
 m=bpy.data.materials.new(n);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.48
 p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emit;return m
mats=[mat('Thermo_Porcelain',(.63,.67,.70),.2),mat('Thermo_Trim',(.32,.37,.42),.35),mat('Thermo_Graphite',(.025,.035,.045)),mat('Thermo_Cold_Stripe',(.018,.32,.62)),mat('Thermo_Warm_Stripe',(.9,.43,.035)),mat('Thermo_Display',(.015,.04,.055)),mat('Thermo_Green',(.012,.5,.025)),mat('Thermo_Red',(.6,.005,.009)),mat('Thermo_Service',(.11,.008,.35)),mat('Thermo_Target_Tint',(.63,.67,.70),.15)]
class Build:
 def __init__(self):self.v=[];self.f=[];self.m=[]
 def vert(self,p):
  for i,q in enumerate(self.v):
   if (Vector(p)-Vector(q)).length<1e-7:return i
  self.v.append(tuple(p));return len(self.v)-1
 def ring(self,ps):return [self.vert(p) for p in ps]
 def face(self,vs,m=0):self.f.append(tuple(vs));self.m.append(m)
 def bridge(self,a,b,m=0):
  for i in range(len(a)):j=(i+1)%len(a);self.face([a[i],a[j],b[j],b[i]],m)
 def inset(self,vs,scale=.8,depth=0,m=1,cap=2,normal=None):
  c=sum((Vector(self.v[i]) for i in vs),Vector())/len(vs)
  n=normal or (Vector(self.v[vs[1]])-Vector(self.v[vs[0]])).cross(Vector(self.v[vs[2]])-Vector(self.v[vs[1]])).normalized()
  r=self.ring([c+(Vector(self.v[i])-c)*scale+n*depth for i in vs]);self.bridge(vs,r,m);self.face(r,cap);return r
 def obj(self,n,collection=asset,parent=root):
  me=bpy.data.meshes.new(n);me.from_pydata(self.v,[],self.f);me.update()
  for m in mats:me.materials.append(m)
  for f,i in zip(me.polygons,self.m):f.material_index=i
  bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
  o=bpy.data.objects.new(n,me);collection.objects.link(o);o.parent=parent;return o
b=Build()
xy=[(-.44,-.43),(-.13,-.43),(-.13,-.50),(-.09,-.59),(0,-.62),(.09,-.59),(.13,-.50),(.13,-.43),(.44,-.43),(.49,-.38),(.49,-.05),(.49,.38),(.44,.43),(-.44,.43),(-.49,.38),(-.49,-.05),(-.49,-.38)]
zs=[.09,.13,.30,.34,.38,.42,.64,.68]
rings=[b.ring([(x,y,z) for x,y in xy]) for z in zs]
# Connected underside and four corner feet extruded from its border.
inner=b.ring([(x*.75,y*.75,.09) for x,y in xy]);b.face(tuple(reversed(inner)),2)
for i in range(17):
 j=(i+1)%17;f=[rings[0][i],inner[i],inner[j],rings[0][j]]
 if i in [8,11,13,16]:
  low=b.ring([(b.v[v][0],b.v[v][1],.015) for v in f]);b.bridge(f,low,2);b.face(low,2)
 else:b.face(f,1)
# Service opening in back; orange side tab extruded from shell.
for k in range(len(rings)-1):
 for i in range(17):
  j=(i+1)%17;f=[rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i]]
  if i==12 and k in range(1,6):continue
  m=3 if k==2 and i<=8 else 4 if k==4 and i<=8 else 1 if i in [8,16] else 0
  if i==10 and k==5:
   c=sum((Vector(b.v[v]) for v in f),Vector())/4
   r=b.ring([c+(Vector(b.v[v])-c)*.50 for v in f]);b.bridge(f,r,0)
   ext=b.ring([Vector(b.v[v])+Vector((.035,0,0)) for v in r]);b.bridge(r,ext,4);b.face(ext,4)
  else:b.face(f,m)
# Back cavity has a physical inset floor, sealed to the perimeter.
boundary=[rings[1][12]]+[rings[k][12] for k in range(2,7)]+[rings[6][13]]+[rings[k][13] for k in range(5,0,-1)]
recess=b.ring([(b.v[v][0],.365,b.v[v][2]) for v in boundary]);b.bridge(boundary,recess,1);b.face(recess,2)
# Central front feature is integrated into body; its dark top is a recessed vent, not the network pipe.
lobe=[rings[-1][i] for i in range(1,8)]
b.inset(lobe,.62,-.025,1,2,Vector((0,0,1)))
# Raised sloping console and flat rear roof.
main=[0,1,7,8,9,10,11,12,13,14,15,16]
height=lambda y:.73+(min(y,-.05)+.43)*(.25/.38)
top={i:b.vert((xy[i][0],xy[i][1],height(xy[i][1]))) for i in main}
for q,i in enumerate(main):
 j=main[(q+1)%len(main)];b.face([rings[-1][i],rings[-1][j],top[j],top[i]],0)
# Roof includes the rear edge subdivisions of the console.
backs=[b.vert((x,-.05,.98)) for x in [.13,-.13]]
b.face([top[10],top[11],top[12],top[13],top[14],top[15],backs[1],backs[0]],0)
outer=[top[0],top[1],top[7],top[8],top[9],top[10],backs[0],backs[1],top[15],top[16]]
xs=[-.40,-.13,.13,.40];ys=[-.36,-.23,-.10]
grid=[[b.vert((x,y,height(y))) for x in xs] for y in ys]
inring=grid[0]+[grid[1][3],grid[2][3],grid[2][2],grid[2][1],grid[2][0],grid[1][0]]
b.bridge(outer,inring,0)
normal=Vector((0,-.25/.38,1)).normalized()
# Left display service well with six boundary vertices.
well=[grid[0][0],grid[0][1],grid[1][1],grid[2][1],grid[2][0],grid[1][0]]
c=sum((Vector(b.v[v]) for v in well),Vector())/6
r=b.ring([c+(Vector(b.v[v])-c)*.91-normal*.035 for v in well]);b.bridge(well,r,1);b.face(r,8)
for row in range(2):
 for col in [1,2]:
  f=[grid[row][col],grid[row][col+1],grid[row+1][col+1],grid[row+1][col]]
  if col==1:
   center=sum((Vector(b.v[v]) for v in f),Vector())/4
   v=b.ring([center+(Vector(b.v[i])-center)*.22 for i in f]);b.bridge(f,v,0);b.face(v,7 if row==0 else 6)
  else:
   center=sum((Vector(b.v[v]) for v in f),Vector())/4
   # narrow elongated meter slots, recessed into console
   ps=[]
   for i in f:
    dv=Vector(b.v[i])-center
    p=center+Vector((dv.x*.84,dv.y*.20,dv.z*.20))-normal*.003;ps.append(p)
   v=b.ring(ps);b.bridge(f,v,0);b.face(v,1)
body=b.obj('Thermomachine_Shell')
# Display is a separate hinged service lid, not a decal.
d=Build();poly=[(-.395,-.355),(-.135,-.355),(-.135,-.105),(-.395,-.105)]
front=d.ring([Vector((x,y,height(y)))+normal*.004 for x,y in poly])
back=d.ring([Vector((x,y,height(y)))-normal*.009 for x,y in poly])
d.bridge(front,back,1);d.face(tuple(reversed(back)),1)
d.inset(front,.80,0,0,5,normal)
display=d.obj('Thermomachine_Display_Hatch')
pivot=Vector((-.265,-.105,height(-.105)))
for v in display.data.vertices:v.co-=pivot
display.location=pivot;display['hinge_axis']='X'
# Rear service cover with bevelled perimeter.
h=Build()
poly=[(-.436,.135),(.436,.135),(.436,.635),(-.436,.635)]
r0=h.ring([(x,.432,z) for x,z in poly]);r1=h.ring([(x,.442,z) for x,z in poly]);r2=h.ring([(x*.94,.450,.385+(z-.385)*.90) for x,z in poly])
h.face(tuple(reversed(r0)),1);h.bridge(r0,r1,1);h.bridge(r1,r2,0);h.face(r2,0)
cover=h.obj('Thermomachine_Rear_Service_Hatch')
for v in cover.data.vertices:v.co-=Vector((0,.432,.135))
cover.location=(0,.432,.135)
# Network connector is entirely separate and can be rotated/colored independently.
p=Build();N=8
profiles=[(.075,0),(.085,.025),(.085,.19),(.075,.215),(.055,.215),(.055,0)]
loops=[p.ring([(rad*math.cos(2*math.pi*i/N),rad*math.sin(2*math.pi*i/N),z) for i in range(N)]) for rad,z in profiles]
for j in range(len(loops)-1):p.bridge(loops[j],loops[j+1],2 if j in [0,2,3] else 1)
p.bridge(loops[-1],loops[0],2)
pipe=p.obj('Thermomachine_Network_Pipe');pipe.location=(.75,0,.015);pipe['independent_network_layer']=True
for n,loc in [('Pipe_Anchor',(0,0,.05)),('Display_Anchor',(-.265,-.23,height(-.23)))]:
 o=bpy.data.objects.new('Thermomachine_'+n,None);asset.objects.link(o);o.parent=root;o.location=loc;o.empty_display_size=.04
root['state']='off';root['target_temperature_kelvin']=293.15;root['front_direction']='-Y'
# studio
world=bpy.data.worlds.new('Thermo_Studio_World');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.45,.5,.6,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
me=bpy.data.meshes.new('Thermo_Ground');me.from_pydata([(-100,-100,0),(100,-100,0),(100,100,0),(-100,100,0)],[],[(0,1,2,3)]);me.materials.append(mat('Thermo_Studio_Floor',(.24,.265,.30)))
o=bpy.data.objects.new(me.name,me);studio.objects.link(o)
for n,loc,power,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-1,4),450,3),('Rim',(1,4,5),750,3)]:
 l=bpy.data.lights.new('Thermo_'+n,'AREA');l.energy=power;l.shape='DISK';l.size=size
 o=bpy.data.objects.new(l.name,l);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Thermo_Camera');data.type='ORTHO';data.ortho_scale=1.95
cam=bpy.data.objects.new(data.name,data);studio.objects.link(cam);cam.location=(3,-4,2.8);cam.rotation_euler=(Vector((.12,0,.48))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1000;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
def audit(o):
 bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update();todo=set(bm.verts);components=0
 while todo:
  components+=1;stack=[todo.pop()]
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in todo:todo.remove(w);stack.append(w)
 r={'vertices':len(bm.verts),'triangles':sum(len(f.verts)-2 for f in bm.faces),'components':components,'nonmanifold':sum(not e.is_manifold for e in bm.edges),'nonplanar':sum(max(abs((v.co-f.verts[0].co).dot(f.normal)) for v in f.verts)>1e-5 for f in bm.faces),'degenerate':sum(f.calc_area()<1e-10 for f in bm.faces)}
 bm.free();return r
result={o.name:audit(o) for o in asset.objects if o.type=='MESH'}

