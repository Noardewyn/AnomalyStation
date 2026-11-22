"""Render the generated chair without adding staging objects to the asset."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
output = ROOT / 'docs/ai/art'
output.mkdir(parents=True, exist_ok=True)
asset = bpy.data.scenes['Modular_Table_Compact_Examples']
preview = bpy.data.scenes.new('Modular_Table_Preview_Temporary')
for obj in asset.objects:
    preview.collection.objects.link(obj)
bpy.context.window.scene = preview
created_objects = []

def stage_object(name, data):
    obj = bpy.data.objects.new(name, data)
    preview.collection.objects.link(obj)
    created_objects.append(obj)
    return obj

try:
    world = bpy.data.worlds.new('Modular_Table_Preview_World')
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs['Color'].default_value = (0.09,0.11,0.14,1)
    background.inputs['Strength'].default_value = 0.5
    preview.world = world
    camera = stage_object('Preview_Camera', bpy.data.cameras.new('Preview_Camera'))
    camera.data.type = next(i.identifier for i in camera.data.bl_rna.properties['type'].enum_items if i.identifier == 'ORTHO')
    camera.data.ortho_scale = 7.6
    camera.location = (6,-8,11)
    camera.rotation_euler = (Vector((2.15,2.2,.4))-camera.location).to_track_quat('-Z','Y').to_euler()
    preview.camera = camera
    for name, position, energy, size in [
        ('Key',(-3,-4,5),1300,7), ('Fill',(4,-2,3),1000,7), ('Rim',(1,3,4),1300,7)]:
        data = bpy.data.lights.new('Preview_' + name, 'AREA')
        data.energy, data.shape, data.size = energy, 'DISK', size
        light = stage_object('Preview_' + name, data)
        light.location = position
        light.rotation_euler = (Vector((2.15,2.2,.4))-light.location).to_track_quat('-Z','Y').to_euler()
    preview.render.resolution_x = 1200
    preview.render.resolution_y = 1100
    preview.render.resolution_percentage = 100
    preview.render.image_settings.file_format = next(i.identifier for i in preview.render.image_settings.bl_rna.properties['file_format'].enum_items if i.identifier == 'PNG')
    preview.render.filepath = str(output / 'modular_table_preview.png')
    bpy.ops.render.render(write_still=True)
    print('Preview saved:',preview.render.filepath)
finally:
    bpy.context.window.scene = asset
    for obj in created_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(preview)




