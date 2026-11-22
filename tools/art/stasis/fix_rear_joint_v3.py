import bpy
from mathutils import Vector
scene=bpy.data.scenes['Stasis_Capsule_Review'];bpy.context.window.scene=scene
a=bpy.data.collections['Stasis_Asset']
for o in a.objects:
 if o.name.startswith(('Stasis_Level_Rail_','Stasis_Continuous_Fascia_')) or o.name=='Stasis_Mattress_Deck':
  if o.data.shape_keys:
   for key in o.data.shape_keys.key_blocks:
    for v in key.data:
     if abs(v.co.y-1.08)<.0001:v.co.y=1.18
  else:
   for v in o.data.vertices:
    if abs(v.co.y-1.08)<.0001:v.co.y=1.18
  o.data.update()
# The pedestal penetrates the housing internally; exposed surfaces are offset, not coplanar.
for name in ['Stasis_Leg.001','Stasis_Leg.003']:
 o=bpy.data.objects[name];o.scale.x=.195/.16;o.scale.y=.31/.19;o.scale.z=.83/.85;o.location.z=.415
# Seat the white plate on the wedge instead of floating ahead of it.
bpy.data.objects['Stasis_Head_White_Plate'].location.y=1.096
bpy.context.view_layer.update()
cam=scene.camera;cam.location=(3.2,-.65,1.7);cam.rotation_euler=(Vector((0,.9,.80))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.85
result={'fascia_end_y':1.18,'pedestal_front_y':1.145,'joint_overlap_m':.035,'pedestal_top_z':.83,'housing_bottom_z':.8}

