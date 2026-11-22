import bpy,bmesh,math
from mathutils import Vector
scene=bpy.data.scenes['Stasis_Capsule_Review'];bpy.context.window.scene=scene
a=bpy.data.collections['Stasis_Asset'];root=bpy.data.objects['Stasis_Root']
dark=bpy.data.materials['Stasis_Graphite'];silver=bpy.data.materials['Stasis_Silver']
def meshobj(name,vs,fs,mat):
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update()
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 o=bpy.data.objects.new(name,m);a.objects.link(o);o.parent=root;o.data.materials.append(mat);return o
# Replace the inclined thin rear shell with a deep wedge: its back is exactly vertical.
old=bpy.data.objects['Stasis_Head_Backplate'];bpy.data.objects.remove(old,do_unlink=True)
lo=.80;hi=1.39;back=1.46
front=lambda z:1.125+(z-1.1)*math.tan(math.radians(20))
vs=[(x,y,z) for x in [-.56,.56] for y,z in [(front(lo),lo),(back,lo),(back,hi),(front(hi),hi)]]
fs=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
meshobj('Stasis_Head_Backplate',vs,fs,dark)
bpy.data.objects['Stasis_Head_White_Plate'].location.y=1.084
# Rear pedestals now support the deeper housing. No shared exterior planes with rails.
for name in ['Stasis_Leg.001','Stasis_Leg.003']:
 o=bpy.data.objects[name];o.location.y=1.30;o.scale.x=1.25;o.scale.y=.32/.19
for side in [-1,1]:
 o=bpy.data.objects['Stasis_Pedestal_Red_'+str(side)];o.location.x=side*.565;o.location.y=1.25
# Recess deck boundaries under the side fascia to remove coincident exposed faces.
deck=bpy.data.objects['Stasis_Mattress_Deck']
for v in deck.data.vertices:
 v.co.x=math.copysign(.455 if abs(v.co.x)>.5 else .35,v.co.x)
# Replace overlapping front trim cubes with ONE watertight ring.
for o in list(a.objects):
 if o.name.startswith(('Stasis_Foot_Crossbar','Stasis_Foot_Lower_Trim','Stasis_Foot_Trim')):
  bpy.data.objects.remove(o,do_unlink=True)
outer=[(-.44,.50),(.44,.50),(.44,.82),(-.44,.82)]
inner=[(-.375,.56),(.375,.56),(.375,.73),(-.375,.73)]
vs=[(x,y,z) for y in [-1.18,-1.10] for loop in [outer,inner] for x,z in loop]
fs=[]
for i in range(4):
 j=(i+1)%4
 fs += [(i,j,j+4,i+4),(i+8,i+12,j+12,j+8),(i,i+8,j+8,j),(i+4,j+4,j+12,i+12)]
meshobj('Stasis_Foot_Frame',vs,fs,silver)
for o in a.objects:
 if o.name.startswith(('Stasis_Level_Rail','Stasis_Continuous_Fascia')):
  for v in o.data.vertices:
   if v.co.y< -1: v.co.y=-1.095
for name in ['Stasis_Leg','Stasis_Leg.002']:bpy.data.objects[name].location.y=-.98
o=bpy.data.objects['Stasis_Foot_Inset'];o.location.y=-1.145;o.scale.x=.74/.73
bpy.context.view_layer.update()
meshes=[o for o in a.objects if o.type=='MESH']
result={'vertices':sum(len(o.data.vertices) for o in meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'rear_plane_y':back,'rear_depth_bottom':back-front(lo),'rear_depth_top':back-front(hi)}

