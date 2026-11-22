import bpy,bmesh
from mathutils import Vector
o=bpy.data.objects['Suit_Storage_Shell'];bm=bmesh.new();bm.from_mesh(o.data)
remove=[v for v in bm.verts if v.co.x>.60 and .8<v.co.z<1.4]
bmesh.ops.delete(bm,geom=remove,context='VERTS')
def vertex(x,y,z):
 for v in bm.verts:
  if (v.co-Vector((x,y,z))).length<1e-5:return v
 return bm.verts.new((x,y,z))
def face(vs,mat):
 f=bm.faces.new(vs);f.material_index=mat;return f
poly=[(.48,-.26),(.48,.26),(.78,.26),(.78,-.47),(.48,-.47)]
lower=[vertex(x,y,.85) for x,y in poly];upper=[vertex(x,y,1.32) for x,y in poly]
face(list(reversed(lower)),0);face(upper,0)
front=None
for i in range(1,5):
 j=(i+1)%5;f=face([lower[i],lower[j],upper[j],upper[i]],0)
 if i==3:front=f
# A recessed front-facing control surface, integrated with the shell.
bmesh.ops.inset_individual(bm,faces=[front],thickness=.035,depth=0,use_even_offset=True)
front.material_index=3
edges=[e for e in front.edges if abs(e.verts[0].co.x-e.verts[1].co.x)<1e-5]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
buttons=[f for f in bm.faces if f.material_index==3 and all(abs(v.co.y+.47)<1e-5 for v in f.verts)]
for f in buttons:
 bmesh.ops.inset_individual(bm,faces=[f],thickness=.04,depth=0,use_even_offset=True)
 verts=list(f.verts);center=f.calc_center_median();material=4 if center.z>1.085 else 5
 bm.faces.remove(f)
 raised=[bm.verts.new(v.co+Vector((0,-.025,0))) for v in verts]
 for i in range(4):j=(i+1)%4;face([verts[i],verts[j],raised[j],raised[i]],2)
 cap=[bm.verts.new(Vector((center.x+(v.co.x-center.x)*.90,v.co.y-.006,center.z+(v.co.z-center.z)*.90))) for v in raised]
 for i in range(4):j=(i+1)%4;face([raised[i],raised[j],cap[j],cap[i]],material)
 face(cap,material)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
bpy.context.window.scene=bpy.data.scenes['Suit_Storage_Classic_Review']
exec(bpy.data.texts['Suit_Storage_State_Controller'].as_string())
cam=bpy.context.scene.camera;cam.location=(2.2,-4,2.1);cam.rotation_euler=(Vector((.40,-.1,1.15))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.5
bpy.context.view_layer.update()
bm=bmesh.new();bm.from_mesh(o.data)
result={'shell_vertices':len(bm.verts),'non_manifold_edges':sum(not e.is_manifold for e in bm.edges),'buttons_front_y':-.501,'front_direction':'-Y'}
bm.free()

