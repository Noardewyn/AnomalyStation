import bpy,bmesh
from mathutils import Vector
o=bpy.data.objects['Suit_Storage_Shell'];bm=bmesh.new();bm.from_mesh(o.data)
remove=[v for v in bm.verts if .8<v.co.z<1.4 and (v.co.x>.48001 or (v.co.x>.479 and v.co.y<-.26001))]
bmesh.ops.delete(bm,geom=remove,context='VERTS')
def vtx(p):
 for v in bm.verts:
  if (v.co-Vector(p)).length<1e-6:return v
 return bm.verts.new(p)
def face(vs,m):
 f=bm.faces.new(vs);f.material_index=m;return f
def bridge(a,b,m):
 for i in range(len(a)):
  j=(i+1)%len(a);face([a[i],a[j],b[j],b[i]],m)
xy=[(.48,-.26),(.48,.26),(.65,.04),(.65,-.31),(.48,-.31)]
lo=[vtx((x,y,z)) for (x,y),z in zip(xy,[.85,.85,.94,.94,.94])]
hi=[vtx((x,y,z)) for (x,y),z in zip(xy,[1.32,1.32,1.34,1.34,1.34])]
face(list(reversed(lo)),0);face(hi,0)
for i in [1,2,4]:
 j=(i+1)%5;face([lo[i],lo[j],hi[j],hi[i]],0)
outer=[lo[3],lo[4],hi[4],hi[3]]
inner=[vtx(p) for p in [(.63,-.31,.965),(.50,-.31,.965),(.50,-.31,1.315),(.63,-.31,1.315)]]
bridge(outer,inner,0);front=face(inner,3)
bm.normal_update()
edges=[e for e in front.edges if abs(e.verts[0].co.x-e.verts[1].co.x)<1e-5]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
targets=[f for f in bm.faces if f.material_index==3 and all(abs(v.co.y+.31)<1e-5 and v.co.x>.49 for v in f.verts)]
for f in targets:
 verts=list(f.verts);c=f.calc_center_median();bm.faces.remove(f)
 inset=[bm.verts.new((c.x+(.026 if v.co.x>c.x else -.026),-.31,c.z+(.029 if v.co.z>c.z else -.029))) for v in verts]
 bridge(verts,inset,3)
 raised=[bm.verts.new(v.co+Vector((0,-.009,0))) for v in inset];bridge(inset,raised,2)
 cap=[bm.verts.new((c.x+(v.co.x-c.x)*.86,-.323,c.z+(v.co.z-c.z)*.86)) for v in raised]
 material=4 if c.z>1.14 else 5;bridge(raised,cap,material);face(cap,material)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
result={'shell_vertices':len(bm.verts),'non_manifold':sum(not e.is_manifold for e in bm.edges),'zero_area_faces':sum(f.calc_area()<1e-10 for f in bm.faces),'panel_width_m':.17,'button_width_m':.045,'button_protrusion_m':.013}
bm.to_mesh(o.data);bm.free();o.data.update()
cam=bpy.context.scene.camera;cam.location=(3,-5,3.5);cam.rotation_euler=(Vector((.05,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.15
bpy.context.view_layer.update()

