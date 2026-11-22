"""Labeled, grouped open/closed comparison scene with shared locker meshes."""
from pathlib import Path
from math import radians, ceil
import bpy, json
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/metal_locker'
manifest=json.loads((OUT/'variants.json').read_text())
source=bpy.data.scenes.get('Atlas_Locker_All_Variants')
if source is None:
    with bpy.data.libraries.load(str(OUT/'metal_locker_atlas_library.blend'),link=False) as (available,loaded):
        loaded.scenes=['Atlas_Locker_All_Variants']
    source=loaded.scenes[0]
scene=bpy.data.scenes.new('Metal_Locker_Showcase')
bpy.context.window.scene=scene
label_mat=bpy.data.materials.new('Locker_Showcase_Label');label_mat.use_nodes=True
bsdf=next(n for n in label_mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bsdf.inputs['Base Color'].default_value=(.8,.86,.92,1)
bsdf.inputs['Emission Color'].default_value=(.8,.86,.92,1)
bsdf.inputs['Emission Strength'].default_value=.5
def label(text,position,size,collection):
    data=bpy.data.curves.new('Label_'+text[:40],'FONT');data.body=text;data.size=size
    data.materials.append(label_mat)
    ob=bpy.data.objects.new('Label_'+text[:40],data);collection.objects.link(ob)
    ob.location=position;ob.rotation_euler.x=radians(90)
    return ob
mesh_objects=[];index=[];row_offset=0
for fam in ['wardrobe','shelves','rack','freezer']:
    variants=[v for v in manifest['variants'] if v['family']==fam]
    coll=bpy.data.collections.new(fam.upper());scene.collection.children.link(coll)
    label(fam.upper()+'  /  '+str(len(variants))+' variants',(0,row_offset-1.25,2.3),.42,coll)
    for i,v in enumerate(variants):
        x=(i%6)*3.6;y=row_offset+(i//6)*3.0
        root=bpy.data.objects.new(v['id'],None);coll.objects.link(root)
        root['body_state']=v['body_state'];root['door_front_state']=v['door_front_state']
        root['door_back_state']=v['door_back_state'];root['atlas_only']=v['atlas_only']
        for state,dx,angle in [('Closed',0,0),('Open',1.8,-123 if fam=='freezer' else -140)]:
            pivot=bpy.data.objects.new(v['id']+'_'+state,None);coll.objects.link(pivot);pivot.parent=root
            pivot.location=(x+dx,y,0)
            for part in ['Locker_Body','Locker_Door']:
                original=next(o for o in source.objects if o.name.startswith(v['id']+'__'+part))
                ob=original.copy();ob.name=v['id']+'_'+state+'_'+part
                coll.objects.link(ob);ob.parent=pivot
                ob.location=(-.45,-.2825,0) if part=='Locker_Door' else (0,0,0)
                ob.rotation_euler=(0,0,radians(angle) if part=='Locker_Door' else 0)
                mesh_objects.append(ob)
        title=v['body_state']+' / '+v['door_front_state'].removesuffix('_door')
        label(title, (x-.45,y-1.12,.13),.115,coll)
        label('closed                 open', (x-.45,y-1.12,.015),.085,coll)
        index.append({'id':v['id'],'family':fam,'closed_position':[x,y,0],'open_position':[x+1.8,y,0]})
    row_offset+=ceil(len(variants)/6)*3.0+1.8
assert len(mesh_objects)==4*len(manifest['variants'])
assert len({o.data.as_pointer() for o in mesh_objects})==8
stage=bpy.data.collections.new('SHOWCASE_LIGHTING');scene.collection.children.link(stage)
world=bpy.data.worlds.new('Locker_Showcase_World');world.use_nodes=True
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.07,.085,.11,1);bg.inputs['Strength'].default_value=.6;scene.world=world
for name,rot,energy in [('Key',(25,-25,-25),2),('Fill',(45,20,140),.8)]:
    data=bpy.data.lights.new(name,'SUN');data.energy=energy;data.angle=.25
    ob=bpy.data.objects.new(name,data);stage.objects.link(ob);ob.rotation_euler=[radians(v) for v in rot]
target=Vector((9,(row_offset-4)/2,.65))
camdata=bpy.data.cameras.new('Showcase_Overview');camdata.type=next(i.identifier for i in camdata.bl_rna.properties['type'].enum_items if i.identifier=='ORTHO')
camdata.ortho_scale=max(26,row_offset*1.12)
cam=bpy.data.objects.new('Showcase_Overview',camdata);stage.objects.link(cam)
cam.location=target+Vector((8,-34,42));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
scene.render.resolution_x=1500;scene.render.resolution_y=1700;scene.render.resolution_percentage=100
scene.render.image_settings.file_format=next(i.identifier for i in scene.render.image_settings.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
scene.render.filepath=str(ROOT/'docs/ai/art/metal_locker_showcase.png')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.shading.type=next(i.identifier for i in space.shading.bl_rna.properties['type'].enum_items if i.identifier=='MATERIAL')
            space.region_3d.view_location=target;space.region_3d.view_distance=max(30,row_offset)
            space.region_3d.view_rotation=Vector((2,-6,8)).to_track_quat('Z','Y');space.region_3d.view_perspective='ORTHO'
            space.overlay.show_floor=False
scene['description']=str(len(manifest['variants']))+' appearances, closed and open; grouped by geometry; original atlas'
bpy.data.libraries.write(str(OUT/'metal_locker_showcase.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'showcase_index.json').write_text(json.dumps(index,indent=2)+'\n')
bpy.ops.render.render(write_still=True)
print(json.dumps({'variants':len(index),'lockers':len(index)*2,'mesh_objects':len(mesh_objects),'unique_meshes':8,'scene':scene.name,'height':row_offset}))
