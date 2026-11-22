"""Render both console bodies from four angles, with atlas alpha enabled for QA."""
import bpy, math, re
from mathutils import Vector
r=bpy.app.driver_namespace['console_rebuild'];sc=bpy.data.scenes.new('Console_Sides_QA');bpy.context.window.scene=sc
for row,(key,a) in enumerate(r['ASSETS'].items()):
 for col,angle in enumerate([.7,-.7,math.pi-.7,math.pi+.7]):
  root=bpy.data.objects.new(key+'_QA',None);sc.collection.objects.link(root);root.location=(col*1.8,0,(1-row)*1.8);root.rotation_euler.z=angle
  for src in a['parts']:
   ob=src.copy();ob.parent=root;sc.collection.objects.link(ob)
   if src==a['parts'][0]:
    ob.data=src.data.copy();mat=src.data.materials[0].copy();ob.data.materials.clear();ob.data.materials.append(mat)
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');tex=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE');mat.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha'])
world=bpy.data.worlds.new('Console_QA_World');world.use_nodes=True;sc.world=world;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.12,.15,.19,1)
target=Vector((2.7,0,1.55))
for loc,power in [((-3,-5,7),1800),((7,2,6),1400)]:
 d=bpy.data.lights.new('QA_Light','AREA');d.energy=power;d.size=6;o=bpy.data.objects.new('QA_Light',d);sc.collection.objects.link(o);o.location=loc;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('QA_Camera');d.type='ORTHO';d.ortho_scale=7.2;o=bpy.data.objects.new('QA_Camera',d);sc.collection.objects.link(o);o.location=target+Vector((0,-9,3));o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();sc.camera=o
sc.render.resolution_x=1800;sc.render.resolution_y=1000;sc.render.resolution_percentage=100;sc.render.filepath=str(r['ROOT']/'docs/ai/art/console_sides_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(r['OUT']/'console_sides_preview.blend'),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
