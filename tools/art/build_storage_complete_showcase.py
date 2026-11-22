"""Append remaining cupboards and boxes to the labeled shared-mesh gallery."""
from pathlib import Path
from math import radians
import bpy,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/metal_locker'
original=sorted([s for s in bpy.data.scenes if s.name.startswith('Metal_Locker_Showcase')],key=lambda s:s.name)[-1]
extras=bpy.data.scenes['Remaining_Storage_Library']
meta=json.loads((OUT/'remaining_variants.json').read_text(encoding='utf-8-sig'))
complete=bpy.data.scenes.new('Storage_Complete_Showcase')
for collection in original.collection.children:complete.collection.children.link(collection)
collection=bpy.data.collections.new('REMAINING_CUPBOARDS_AND_BOXES');complete.collection.children.link(collection)
for ob in extras.objects:
    copy=ob.copy();copy.parent=None;copy.location=ob.location+Vector((24,0,0));collection.objects.link(copy)
labelmat=next(m for m in bpy.data.materials if m.name.startswith('Locker_Showcase_Label'))
def label(text,position,size):
    data=bpy.data.curves.new('Storage_Label','FONT');data.body=text;data.size=size;data.materials.append(labelmat)
    ob=bpy.data.objects.new(text,data);collection.objects.link(ob);ob.location=position;ob.rotation_euler.x=radians(90)
label('CUPBOARDS / BOXES / MAGIC FRAMES',(24,-1.2,2.6),.38)
for i,v in enumerate(meta['variants']):label(v['id'],(24+(i%5)*3.4-.5,(i//5)*3-1.3,.10),.13)
complete.world=original.world
camera=original.camera.copy();camera.data=original.camera.data.copy();complete.collection.objects.link(camera)
target=Vector((20,22,.7));camera.location=target+Vector((9,-36,46));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=61;complete.camera=camera
complete.render.resolution_x=1700;complete.render.resolution_y=1700;complete.render.resolution_percentage=100
complete['appearance_entries']=99;complete['description']='75 metal locker variants plus 24 remaining object/frame entries; each closed and open'
bpy.context.window.scene=complete
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            r=area.spaces.active.region_3d;r.view_location=Vector((20,19,.9));r.view_distance=65;r.view_rotation=Vector((2,-6,8)).to_track_quat('Z','Y');r.view_perspective='ORTHO'
bpy.data.libraries.write(str(OUT/'storage_complete_showcase.blend'),{complete},path_remap='RELATIVE',fake_user=True,compress=True)
# Save the combined result under the previous showcase path as well.
bpy.data.libraries.write(str(OUT/'metal_locker_showcase.blend'),{complete},path_remap='RELATIVE',fake_user=True,compress=True)
preview=bpy.data.scenes.new('Remaining_Storage_Preview')
wanted=['wood_cabinet','gun_cabinet','abductor_locker','mini_fridge','cursed_1','decursed_1','old_locker','cardboard','agentbox','cardboard_roroco','metalbox']
for i,id in enumerate(wanted):
    group=[o for o in extras.objects if o.name.startswith(id+'_Closed_') or o.name.startswith(id+'_Open_')]
    anchor=next(o for o in group if '_Closed_' in o.name and '_Body' in o.name)
    offset=Vector(((i%4)*3.6,(i//4)*3.4,0))-anchor.location
    for source in group:
        ob=source.copy();ob.parent=None;preview.collection.objects.link(ob);ob.location=source.location+offset
preview.world=original.world
for source in original.objects:
    if source.type=='LIGHT':preview.collection.objects.link(source)
data=bpy.data.cameras.new('Remaining_Preview_Camera');data.type='ORTHO';data.ortho_scale=16.5
cam=bpy.data.objects.new('Remaining_Preview_Camera',data);preview.collection.objects.link(cam)
target=Vector((6.2,3.2,.85));cam.location=target+Vector((5,-16,13));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.resolution_x=1500;preview.render.resolution_y=1050;preview.render.resolution_percentage=100
preview.render.image_settings.file_format='PNG';preview.render.filepath=str(ROOT/'docs/ai/art/remaining_storage_preview.png')
bpy.context.window.scene=preview;bpy.ops.render.render(write_still=True)
bpy.context.window.scene=complete
assert len([o for o in complete.objects if o.type=='MESH'])==424
print('Complete gallery: 99 appearance/frame entries, 198 closed/open objects; 424 mesh objects with shared geometry.')
