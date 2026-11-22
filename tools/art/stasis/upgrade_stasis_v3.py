import bpy, bmesh, math, os
from mathutils import Vector
scene=bpy.data.scenes['Stasis_Capsule_Review'];bpy.context.window.scene=scene
a=bpy.data.collections['Stasis_Asset'];root=bpy.data.objects['Stasis_Root']
out=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../godot/assets/models/machines/stasis')
def mat(name,color,emit=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.65
 p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emit
 return m
dark=bpy.data.materials['Stasis_Graphite'];silver=bpy.data.materials['Stasis_Silver'];teal=bpy.data.materials['Stasis_Mattress_Emission'];red=bpy.data.materials['Stasis_Status_Red']
for m,c in [(dark,(.016,.022,.035)),(silver,(.29,.34,.42)),(teal,(.006,.075,.058)),(red,(.65,.002,.005))]:
 m.diffuse_color=(*c,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1)
 if m==red:p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=.35
teal.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.08,.8,.55,1)
navy=mat('Stasis_Mattress_Dark_Edges',(.009,.022,.055))
wirem=[mat('Stasis_Wire_Red',(.36,.012,.018)),mat('Stasis_Wire_Green',(.015,.20,.045)),mat('Stasis_Wire_Blue',(.012,.09,.22))]
cavity=mat('Stasis_Cavity',(.004,.006,.012))
def box(name,loc,size,material):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name
 for c in list(o.users_collection):c.objects.unlink(o)
 a.objects.link(o);o.parent=root;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(material);return o
# Dark mattress edges, retain teal upper faces.
for o in a.objects:
 if o.name.startswith('Stasis_Mattress_Pad') or o.name=='Stasis_Mattress_Head':
  o.data.materials.append(navy)
  for p in o.data.polygons:
   if p.normal.z<.5:p.material_index=len(o.data.materials)-1
# Shorter white insert, retaining the broad rear wedge.
bpy.data.objects['Stasis_Head_White_Plate'].scale.z=.84
for o in a.objects:
 if 'Red_' in o.name:o.scale*=.67
# Recessed lower equipment assembly, visually reduces empty gap.
box('Stasis_Underbody_Module',(0,-.02,.405),(.63,1.25,.24),dark)
box('Stasis_Underbody_Tray',(0,-.02,.275),(.70,1.34,.045),silver)
box('Stasis_Underbody_Inset',(0,-.655,.395),(.48,.015,.12),cavity)
# Functional service openings through the fascia, replacing the former solid behind-panel surfaces.
for side in [-1,1]:
 fascia=bpy.data.objects['Stasis_Continuous_Fascia_'+str(side)]
 cutter=box('Service_Cutter',(side*.40,.08,.605),(.30,.64,.18),cavity)
 bpy.ops.object.select_all(action='DESELECT');fascia.select_set(True);bpy.context.view_layer.objects.active=fascia
 mod=fascia.modifiers.new('Actual_service_opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
 panel=bpy.data.objects['Stasis_Service_Panel_'+str(side)]
 # Reset to a simple panel whose origin is at the lower hinge.
 for v in panel.data.vertices:
  v.co.z=(.20 if v.co.z>-.10 else 0)
  v.co.y=math.copysign(.335,v.co.y)
 panel.location=(side*.445,.08,.505)
 panel['hinge_axis']='Y'
 box('Stasis_Service_Back_'+str(side),(side*.322,.08,.60),(.012,.64,.18),cavity)
 for i,material in enumerate(wirem):
  # squared U-shaped cables, each one extruded 8-corner polygon.
  y=-.13+i*.19;x=side*.37
  poly=[(y,.69),(y+.035,.69),(y+.035,.55),(y+.09,.55),(y+.09,.62),(y+.125,.62),(y+.125,.515),(y,.515)]
  vs=[(xx,yy,z) for xx in [x-.012,x+.012] for yy,z in poly];n=len(poly)
  fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
  me=bpy.data.meshes.new('Service_Cable');me.from_pydata(vs,[],fs);me.update()
  bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
  o=bpy.data.objects.new('Stasis_Service_Cable_%s_%s'%(side,i),me);a.objects.link(o);o.parent=root;o.data.materials.append(material)
# Broken shape keys keep topology and anchors stable.
for o in list(a.objects):
 if o.type!='MESH':continue
 if o.name.startswith('Stasis_Level_Rail') or o.name.startswith('Stasis_Mattress_Pad'):
  o.shape_key_add(name='Basis');key=o.shape_key_add(name='Broken')
  if o.name.startswith('Stasis_Level_Rail'):
   # add a deliberate bent shoulder at widened section
   for v in key.data:
    if .45<v.co.y<.65:v.co.z-=.085;v.co.x*=1.10
  else:
   idx=int(o.name[-2:])
   for v in key.data:
    v.co.z += ((v.co.x/.35)*(.025 if idx%2 else -.035)) - (.025 if idx in [1,2,3] else 0)
  fc=key.driver_add('value');d=fc.driver;d.expression='broken';v=d.variables.new();v.name='broken';v.targets[0].id=root;v.targets[0].data_path='["broken"]'
# Jagged dark tears on broken mattress (surface strips follow the pad tilt).
for i in [1,2,3]:
 o=box('Stasis_Damage_Tear_'+str(i),(0,-.905+i*.326,.777),(.30,.018,.008),cavity)
 o.rotation_euler.z=math.radians(25 if i%2 else -30)
 o['broken_only']=True
root['maintenance']=False
root['state']='normal'
root['notes']='Normal / powered / maintenance / broken. Use Stasis_State_Controller text. Emission independent of patient. Four standalone GLB variants exported.'
bpy.context.view_layer.update()
result={'vertices_all_states':sum(len(o.data.vertices) for o in a.objects if o.type=='MESH')}

