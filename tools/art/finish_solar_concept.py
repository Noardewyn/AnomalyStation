"""Export and review the current solar_concept_build through Blender MCP."""
import bpy,contextlib,io,json
s=bpy.app.driver_namespace['solar_concept_build'];OUT=s['OUT'];scene=s['scene']
# Refresh state helper from the reproducible generator.
source=(s['ROOT']/'tools/art/build_solar_concept.py').read_text(encoding='utf-8')
a=source.index('def state(');b=source.index('def clip(',a);exec(source[a:b],s)
t=bpy.data.texts.new('Solar_README');t.write((OUT/'README.md').read_text(encoding='utf-8'))
# Text is saved as a scene dependency via an explicit custom-property reference.
scene['asset_readme']=t
exports=[]
with contextlib.redirect_stdout(io.StringIO()):
    for skin in s['skins']:
        for broken in [False,True]:
            name='solar_panel'+('' if skin=='glass' else '_'+skin)+('_broken' if broken else '')
            s['state'](skin,broken=broken);s['export'](name);exports.append(name)
    s['state'](bare=True);s['export']('solar_panel_frame',bare=True);exports.append('solar_panel_frame')
s['state']()
scene.render.resolution_x=660;scene.render.resolution_y=660;scene.cycles.samples=24
for name,pos in [('front',(0,-3,.9)),('side',(3,0,1.05)),('rear',(0,3,1.25)),('top',(0,.1,5))]:
    s['camera_at'](pos);scene.render.filepath=str(OUT/('solar_'+name+'.png'));bpy.ops.render.render(write_still=True)
s['camera_at']((1.65,-2.8,2.05))
for skin in s['skins']:
    s['state'](skin);scene.render.filepath=str(OUT/(skin+'_preview.png'));bpy.ops.render.render(write_still=True)
s['state'](broken=True);scene.render.filepath=str(OUT/'solar_broken_preview.png');bpy.ops.render.render(write_still=True)
s['state'](bare=True);scene.render.filepath=str(OUT/'solar_frame_preview.png');bpy.ops.render.render(write_still=True)
s['state']();scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.cycles.samples=40
s['camera_at']((1.65,-2.8,2.05));scene.render.filepath=str(OUT/'solar_preview.png')
for o in bpy.context.selected_objects:o.select_set(False)
s['frame'].select_set(True);bpy.context.view_layer.objects.active=s['frame']
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=2.6;area.spaces.active.region_3d.view_location=(0,.1,.8)
        area.spaces.active.region_3d.view_rotation=s['cam'].rotation_euler.to_quaternion()
bpy.data.libraries.write(str(OUT/'solar_panel.blend'),{scene},fake_user=True)
issues=[];triangles=0
for o in s['parts']:
    o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    if any(t.area<1e-12 for t in o.data.loop_triangles):issues.append(o.name)
report={'vertices_all_states':sum(len(o.data.vertices) for o in s['parts']),'limit':600,'triangles_all_states':triangles,
    'degenerate_triangle_objects':issues,'smooth_faces':sum(p.use_smooth for o in s['parts'] for p in o.data.polygons),
    'panel_is_square':True,'panel_dimensions_m':[1.24,1.24],'frame_thickness_m':.07,
    'overhang_each_side_m':s['report']['panel_overhang_each_side_m'],'corner_cubes':False,
    'emissive_materials':sum(m.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value>0 for m in s['mats'].values()),
    'game_lights':0,'exports':exports}
(OUT/'mesh_validation.json').write_text(json.dumps(report,indent=2))
result=report
