"""Update four existing live assets and all their preview instances in place."""
from pathlib import Path
import bpy,json,re
ROOT=Path(__file__).resolve().parents[2]
generator=ROOT/'tools/art/build_additional_doors.py'
source=generator.read_text(encoding='utf-8').split('# Reuse the corrected hatch')[0]
source=source.replace('for key,source,state,kind in configs:', 'for key,source,state,kind in configs[:4]:')
source=source.replace("scene=bpy.data.scenes.new(key+'_Door');bpy.context.window.scene=scene;objects=[];glass=set()", "scene=bpy.data.scenes[key+'_Door'];bpy.context.window.scene=scene;old_objects=[o for o in scene.objects if o.type=='MESH'];objects=[];glass=set()")
source=source.replace("root=bpy.data.objects.new(key,None);scene.collection.objects.link(root)", """root=next(o for o in scene.objects if o.type=='EMPTY')
    for new in objects:
        old=next(o for o in old_objects if o.name.split('.')[0]==new.name.split('.')[0])
        old_mesh=old.data
        for instance in list(bpy.data.objects):
            if instance.data==old_mesh:instance.data=new.data
        bpy.data.objects.remove(new,do_unlink=True)
    objects=old_objects
    for ob in scene.objects:ob.select_set(False)""")
env={'__file__':str(generator)}
exec(compile(source,str(generator),'exec'),env)
out=ROOT/'godot/assets/models/doors'
validation=json.loads((out/'additional_doors_validation.json').read_text())
doc=(out/'ADDITIONAL_DOORS.md').read_text(encoding='utf-8')
for key,info in env['results'].items():
    validation[key]={'meshes':len(info['parts']),'triangles':info['triangles'],'original_png':True,'nearest':True,'seam':'whole pixel boundaries following user sketches'}
    doc=re.sub(r'(\| '+key+r'/[^\n]+\| )\d+( \|)',lambda m:m[1]+str(info['triangles'])+m[2],doc)
(out/'additional_doors_validation.json').write_text(json.dumps(validation,indent=2)+'\n')
(out/'ADDITIONAL_DOORS.md').write_text(doc,encoding='utf-8')
preview=bpy.data.scenes['Additional_Doors_Preview'];bpy.context.window.scene=preview
bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(out/'additional_doors_preview.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
print(json.dumps(validation,indent=2))
