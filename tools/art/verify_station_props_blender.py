"""Static pose and modular seam verification after the MCP builder has run."""
from pathlib import Path
import bpy,bmesh,json,math
from mathutils import Vector,Matrix

ns=bpy.app.driver_namespace['station_props_build']
OUT=ns['OUT'];ASSETS=ns['ASSETS'];G=ns['G']
SCENE=bpy.data.scenes.new('Station_Props_Pose_Checks');bpy.context.window.scene=SCENE;SCENE.world=ns['PROP_PREVIEW'].world
POSES={}
for i,(key,pose) in enumerate([('toilet','lid_open'),('solar_panel','panel_tilted'),('suit_storage_classic','doors_open')]):
    original=ASSETS[key];copies={}
    for ob in original['scene'].objects:
        clone=ob.copy();clone.parent=None;clone.name='Pose_'+key+'_'+ob.get('part_name',ob.name)
        SCENE.collection.objects.link(clone);copies[ob]=clone
    for ob,clone in copies.items():
        clone.parent=copies.get(ob.parent);clone.matrix_basis=ob.matrix_basis.copy()
    root=copies[original['root']];root.location.x+=i*2.0
    if key=='toilet':
        lid=next(c for o,c in copies.items() if o.get('part_name')=='Seat_Lid');lid.rotation_euler.x=-math.pi/2
    elif key=='solar_panel':
        next(c for o,c in copies.items() if o.get('part_name')=='Panel_Tilt_Pivot').rotation_euler.x=math.radians(30)
    else:
        for ob,clone in copies.items():
            if ob.get('part_name','').startswith('Door_Hinge_'):clone.rotation_euler.z=math.radians(-100 if ob.location.x<0 else 100)
    POSES[key]=pose
for ob in ns['PROP_PREVIEW'].objects:
    if ob.type in ('CAMERA','LIGHT'):
        clone=ob.copy();SCENE.collection.objects.link(clone)
        if clone.type=='CAMERA':
            clone.data=clone.data.copy();clone.data.ortho_scale=7.2;target=Vector((2,0,.85))
            clone.location=target+Vector((3,-7,4));clone.rotation_euler=(target-clone.location).to_track_quat('-Z','Y').to_euler();SCENE.camera=clone
SCENE.render.engine='BLENDER_EEVEE_NEXT';SCENE.render.resolution_x=1500;SCENE.render.resolution_y=800
SCENE.view_settings.view_transform=G.enum(SCENE.view_settings,'view_transform','Standard')
SCENE.render.image_settings.file_format=G.enum(SCENE.render.image_settings,'file_format','PNG')
SCENE.render.filepath=str(ns['ROOT']/'docs/ai/art/station_props_pose_checks.png')
bpy.data.libraries.write(str(OUT/'station_props_pose_checks.blend'),{SCENE},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.ops.render.render(write_still=True)

# Compare actual mesh boundary extents across every connected cell pair.
vectors=ns['direction_vectors'];seams=0;max_gap=0;examples={}
for label,rows in ns['ASSEMBLY_DATA'].items():
    indexed={tuple(r['cell']):r for r in rows};examples[label]={'cells':len(rows),'checked_joins':0}
    for (x,y),row in indexed.items():
        for d in ('N','E'):
            if d not in row['connections']:continue
            dx,dy=vectors[d];other=indexed[(x+dx,y+dy)]
            expected=[]
            for cell,record in [((x,y),row),((x+dx,y+dy),other)]:
                a=ASSETS['stationary_tank_'+record['module']]
                core=next(o for o in a['parts'] if o.get('part_name')=='Continuous_Tank_Shell')
                transform=Matrix.Translation((cell[0]*.9,cell[1]*.9,0))@Matrix.Rotation(math.radians(record['rotation_z_degrees']),4,'Z')
                verts=[transform@core.matrix_world@v.co for v in core.data.vertices]
                axis=0 if d=='E' else 1
                boundary=(x+.5)*.9 if d=='E' else (y+.5)*.9
                coords=[v for v in verts if abs(v[axis]-boundary)<1e-6 and .0399<=v.z<=.3901]
                assert coords,(label,row,d)
                otheraxis=1-axis
                expected.append((min(v[otheraxis] for v in coords),max(v[otheraxis] for v in coords),min(v.z for v in coords),max(v.z for v in coords)))
            gap=max(abs(a-b) for a,b in zip(*expected));max_gap=max(gap,max_gap);assert gap<1e-6,(label,row,d,gap)
            examples[label]['checked_joins']+=1;seams+=1
report={'static_pose_checks':POSES,'assembly_checks':examples,'checked_joins':seams,'maximum_join_extent_mismatch_m':max_gap,
        'connected_side_walls_omitted':True,'window_jambs_omitted_at_adjacent_module_connections':True,
        'glass_and_water':'Closed solids with positive mesh volume and tinted BLEND materials',
        'animations_added':False,'godot_runtime_tested':False}
(OUT/'assembly_pose_validation.json').write_text(json.dumps(report,indent=2)+'\n')
ns['LIBRARY_SCENES'].add(SCENE)
bpy.data.libraries.write(str(OUT/'station_props_library.blend'),ns['LIBRARY_SCENES'],path_remap='RELATIVE',fake_user=True,compress=True)
print(json.dumps(report,indent=2))
