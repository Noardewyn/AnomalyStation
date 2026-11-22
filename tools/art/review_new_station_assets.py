"""Render compact front/rear/state review pages outside the repository."""
import bpy, math
from mathutils import Vector
from pathlib import Path

def review_wave(key):
    ns=bpy.app.driver_namespace[key+'_build'];items=list(ns['assets'].items());pages=[]
    for page in range((len(items)+5)//6):
        scene=bpy.data.scenes.new(key+'_Review_'+str(page));bpy.context.window.scene=scene
        for index,(name,(original,objects)) in enumerate(items[page*6:(page+1)*6]):
            for row in [0,1,2]:
                bpy.context.window.scene=original
                rest={ob:(ob.location.copy(),ob.rotation_euler.copy(),ob.scale.copy()) for ob in objects}
                tracks=[t for ob in objects if ob.animation_data for t in ob.animation_data.nla_tracks]
                if row==2:
                    clips={t.name for t in tracks}
                    wanted=next((c for c in ['open','board_installed','glassed','broken','scanning','venue_open','maintenance_open'] if c in clips),None)
                    for t in tracks:t.mute=t.name!=wanted
                    end=max([s.frame_end for t in tracks if not t.mute for s in t.strips],default=0)
                    original.frame_set(max(1,int(end)-1))
                    for ob in objects:ob.update_tag(refresh={'OBJECT'})
                    original.view_layers[0].update()
                original.view_layers[0].update()
                graph=original.view_layers[0].depsgraph;graph.update()
                for source in objects:
                    ev=source.evaluated_get(graph)
                    ob=source.copy();ob.animation_data_clear();ob.parent=None;scene.collection.objects.link(ob)
                    ob.matrix_world=ev.matrix_world.copy()
                    if row==1:
                        from mathutils import Matrix
                        ob.matrix_world=Matrix.Rotation(math.pi,4,'Z')@ob.matrix_world
                    ob.location+=Vector((index*2.6,row*3.0,0))
                for t in tracks:t.mute=True
                original.frame_set(0)
                for ob,(loc,rot,scale) in rest.items():ob.location=loc;ob.rotation_euler=rot;ob.scale=scale
                original.view_layers[0].update()
        world=bpy.data.worlds.new('Review_World');world.use_nodes=True;scene.world=world
        next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.65
        target=Vector((6.5,3.,.8))
        for loc,power in [((0,-5,10),2300),((13,8,9),2400)]:
            ld=bpy.data.lights.new('Review_Area','AREA');ld.energy=power;ld.size=8
            ob=bpy.data.objects.new('Review_Area',ld);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
        cd=bpy.data.cameras.new('Review_Camera');cd.type='ORTHO';cd.ortho_scale=18
        cam=bpy.data.objects.new('Review_Camera',cd);scene.collection.objects.link(cam);cam.location=target+Vector((2,-14,11));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
        scene.render.engine='CYCLES';scene.cycles.samples=20;scene.render.resolution_x=1900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
        bpy.context.window.scene=scene
        scene.render.filepath=str(ns['REVIEW']/('review_'+str(page)+'.png'));bpy.ops.render.render(write_still=True)
        pages.append({'path':scene.render.filepath,'columns':[n for n,_ in items[page*6:(page+1)*6]]})
    return pages

result={key:review_wave(key) for key in ['computers_construction','station_extras']}
bpy.context.window.scene=bpy.app.driver_namespace['computers_construction_build']['preview']
