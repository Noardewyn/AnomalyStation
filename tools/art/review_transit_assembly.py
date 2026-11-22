"""Connected modular layout and an illustrative station-to-station animation."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2]
ns=bpy.app.driver_namespace['transit_tubes_wave_build'];OUT=ns['OUT'];connections=json.loads((OUT/'connections.json').read_text())
review=bpy.data.scenes.new('Transit_Tube_Assembly_And_Motion');instances={};clones={}
def instance(name,label,xy,angle=0):
    source,objects=ns['assets'][name];source.frame_set(0)
    for ob in objects:ob.update_tag(refresh={'OBJECT'})
    source.view_layers[0].update()
    root=bpy.data.objects.new(label,None);review.collection.objects.link(root);root.location=(xy[0],xy[1],0);root.rotation_euler.z=angle
    nodes=[]
    for ob in objects:
        copy=ob.copy();copy.data=ob.data;copy.animation_data_clear();copy.parent=None;review.collection.objects.link(copy);copy.matrix_world=ob.matrix_world.copy();copy.parent=root;nodes.append(copy)
    instances[label]=(name,Vector((xy[0],xy[1],0)),Matrix.Rotation(angle,4,'Z'));clones[label]=(root,nodes)
    return root,nodes

instance('tube_station','Departure',(0,0))
instance('tube_straight','Line_1',(2,0),-math.pi/2)
instance('tube_straight_crossing','Line_2',(4,0),-math.pi/2)
instance('tube_terminus','Arrival',(6,0),math.pi)
pod,podparts=instance('transit_pod','Moving_Pod',(0,0),-math.pi/2)
for frame,x in [(1,0),(30,0),(150,6),(210,6)]:pod.location.x=x;pod.keyframe_insert(data_path='location',frame=frame)
for label,keys in [('Departure',[(1,-120),(20,-120),(30,0)]),('Arrival',[(1,0),(150,0),(168,-120),(210,-120)])]:
    door=next(ob for ob in clones[label][1] if ob.name.startswith('Boarding_Door_front'))
    for frame,angle in keys:door.rotation_euler.x=math.radians(angle);door.keyframe_insert(data_path='rotation_euler',frame=frame)
instance('tube_diagonal','Curve_Diagonal',(-2,3))
instance('tube_curve_left','Curve',(0,5))
instance('tube_straight','Curve_North',(0,7))
instance('tube_junction','Fork',(5,5))
instance('tube_straight','Fork_North',(5,7))
instance('tube_diagonal','Fork_SW',(3,3))
instance('tube_diagonal','Fork_SE',(7,3),math.pi/2)

links=[('Departure','E','Line_1','S'),('Line_1','N','Line_2','S'),('Line_2','N','Arrival','E'),('Curve_Diagonal','NE','Curve','SW'),('Curve','N','Curve_North','S'),('Fork','N','Fork_North','S'),('Fork','SW','Fork_SW','NE'),('Fork','SE','Fork_SE','NE')]
checks=[]
for a,ap,b,bp in links:
    def port(label,direction):
        name,loc,rot=instances[label];p=next(p for p in connections[name] if p['direction']==direction)
        return loc+rot@Vector(p['position_blender']),rot@Vector(p['outward_blender'])
    pa,ta=port(a,ap);pb,tb=port(b,bp);gap=(pa-pb).length;alignment=ta.normalized().dot(tb.normalized());assert gap<1e-6 and alignment<-.99999,(a,b,gap,alignment)
    checks.append({'connection':a+'/'+ap+' to '+b+'/'+bp,'gap_m':gap,'opposing_tangent_dot':alignment})
# Check actual curve vertex end planes as well as metadata, avoiding secant/tangent mismatch.
scene,objects=ns['assets']['tube_curve_left'];shell=next(o for o in objects if o.name.startswith('Curved_Glass_Shell'))
for point,tangent in [((-1,-1,.94),(-1,-1,0)),((0,1,.94),(0,1,0))]:
    p=Vector(point);t=Vector(tangent).normalized();onplane=[v.co for v in shell.data.vertices if abs((v.co-p).dot(t))<1e-6];assert len(onplane)>=24,len(onplane)
    checks.append({'curve_port':point,'vertices_on_exact_end_plane':len(onplane)})
(OUT/'assembly_checks.json').write_text(json.dumps({'connections':checks,'pod_outer_radius_m':.568,'tube_inner_radius_m':.685,'radial_clearance_m':.117},indent=2))

review.world=ns['preview'].world
for source in ns['preview'].objects:
    if source.type=='LIGHT':ob=source.copy();review.collection.objects.link(ob)
target=Vector((2.5,3.2,.7));cd=bpy.data.cameras.new('Assembly_Camera');cd.type='ORTHO';cd.ortho_scale=15;cam=bpy.data.objects.new('Assembly_Camera',cd);review.collection.objects.link(cam);cam.location=target+Vector((5,-13,13));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();review.camera=cam
review.render.engine='CYCLES';review.cycles.samples=24;review.render.resolution_x=1800;review.render.resolution_y=1400;review.render.resolution_percentage=100;review.render.fps=30;review.frame_start=1;review.frame_end=210;review.frame_set(75)
review['animation_note']='Illustrative motion only. Runtime routing, boarding, cooldowns and atmosphere are not implemented by this asset demo.'
bpy.context.window.scene=review
bpy.data.libraries.write(str(OUT/'transit_tubes_assembly.blend'),{review},path_remap='RELATIVE',fake_user=True,compress=True)
review.render.filepath=str(ROOT/'docs/ai/art/transit_tubes_assembly.png');bpy.ops.render.render(write_still=True)
for scr in bpy.data.screens:
    for area in scr.areas:
        if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_location=target;area.spaces.active.region_3d.view_distance=16
ns['assembly']=review
result={'connections_checked':len(links),'assembly':str(OUT/'transit_tubes_assembly.blend')}
