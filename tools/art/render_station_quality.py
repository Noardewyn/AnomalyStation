"""Save multi-angle reference sheets from the actual revised Blender meshes."""
import bpy,math,json
from mathutils import Matrix,Vector

ns=bpy.app.driver_namespace['station_props_build']
chairs=bpy.app.driver_namespace.get('chair_revision_build')
OUT=ns['ROOT']/'docs/ai/art/quality'
OUT.mkdir(parents=True,exist_ok=True)


def sheet(keys,index):
    scene=bpy.data.scenes.new('Asset_Quality_'+str(index));bpy.context.window.scene=scene
    scene.world=ns['PROP_PREVIEW'].world
    rotations=[Matrix.Identity(4),Matrix.Rotation(math.pi/2,4,'Z'),Matrix.Rotation(math.pi,4,'Z'),
               Matrix.Rotation(math.radians(22),4,'X')@Matrix.Rotation(math.radians(-32),4,'Z')]
    mat=ns['solid_material']('Quality_Label',(.6,.7,.8),roughness=1)
    for row,key in enumerate(keys):
        a=ns['ASSETS'].get(key) or chairs['ASSETS'][key]
        for col,rot in enumerate(rotations):
            trans=Matrix.Translation((col*2.25,0,-row*2.2))@rot
            for original in a['parts']:
                ob=original.copy();ob.parent=None;ob.matrix_world=trans@original.matrix_world
                scene.collection.objects.link(ob)
        curve=bpy.data.curves.new('Quality_Label','FONT');curve.body=key;curve.size=.115;curve.align_x='CENTER';curve.materials.append(mat)
        label=bpy.data.objects.new(curve.name,curve);scene.collection.objects.link(label);label.location=(3.375,-.9,-row*2.2-.19);label.rotation_euler.x=math.pi/2
    target=Vector((3.375,0,-(len(keys)-1)*1.1+.7))
    for tag,location,power in [('Key',(-3,-7,7),950),('Fill',(10,-5,2),700)]:
        data=bpy.data.lights.new('Quality_'+tag,'AREA');data.energy=power;data.size=8
        ob=bpy.data.objects.new(data.name,data);scene.collection.objects.link(ob);ob.location=target+Vector(location)
        ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Quality_Camera');data.type=ns['G'].enum(data,'type','ORTHO');data.ortho_scale=max(9.3,len(keys)*2.2+1)*1.05
    cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=target+Vector((0,-20,0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    scene.render.engine=ns['PROP_PREVIEW'].render.engine
    scene.render.resolution_x=1600;scene.render.resolution_y=int(1600*(len(keys)*2.2)/9.3);scene.render.resolution_percentage=100
    scene.eevee.taa_render_samples=64
    scene.view_settings.view_transform=ns['G'].enum(scene.view_settings,'view_transform','Standard')
    scene.render.image_settings.file_format=ns['G'].enum(scene.render.image_settings,'file_format','PNG')
    scene.render.filepath=str(OUT/('asset_quality_'+str(index).zfill(2)+'.png'))
    bpy.ops.render.render(write_still=True)
    return {'file':scene.render.filepath,'models':keys,'views':['front','right','back','three_quarter']}


KEYS=list(ns['ASSETS'])+list(chairs['ASSETS'] if chairs else [])
BATCHES=[KEYS[i:i+5] for i in range(0,len(KEYS),5)]
