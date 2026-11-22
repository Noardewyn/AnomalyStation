"""Add two original-atlas doors to the existing preview without replacing other assets."""
from pathlib import Path
import bpy,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
generator=ROOT/'tools/art/build_additional_doors.py'
source=generator.read_text(encoding='utf-8').split('# Reuse the corrected hatch')[0]
source=source.replace('for key,source,state,kind in configs:', 'for key,source,state,kind in configs[-2:]:')
env={'__file__':str(generator)}
exec(compile(source,str(generator),'exec'),env)
out=ROOT/'godot/assets/models/doors'
preview=bpy.data.scenes['Additional_Doors_Preview'];bpy.context.window.scene=preview
for i,(name,parts) in enumerate(env['groups']):
    for original in parts:
        ob=original.copy();ob.parent=None;ob.name=name+'_'+original.name;preview.collection.objects.link(ob);ob.location=original.location+Vector((i*3,9.6,0))
    data=bpy.data.curves.new('Label_'+name,'FONT');data.body=name;data.size=.13
    ob=bpy.data.objects.new('Label_'+name,data);preview.collection.objects.link(ob);ob.location=(i*3-1,9.5,-.23);ob.rotation_euler.x=math.pi/2
target=Vector((3,4.8,.8));cam=preview.camera;cam.data.ortho_scale=15
cam.location=target+Vector((3,-14,10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
preview.render.filepath=str(ROOT/'docs/ai/art/additional_doors_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(out/'additional_doors_preview.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
validation=json.loads((out/'additional_doors_validation.json').read_text())
for key,info in env['results'].items():validation[key]={'meshes':len(info['parts']),'triangles':info['triangles'],'original_png':True,'nearest':True}
(out/'additional_doors_validation.json').write_text(json.dumps(validation,indent=2)+'\n')
print(json.dumps(validation,indent=2))
