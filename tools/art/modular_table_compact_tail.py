"""Compact kit build stage; executed by build_modular_metal_table.py."""
from math import pi

specs = [
    ('table_single_4legs',0,[(-.385,-.385),(.385,-.385),(.385,.385),(-.385,.385)]),
    ('table_end_2legs',2,[(-.385,-.385),(-.385,.385)]),
    ('table_top_straight',10,[]),
    ('table_corner_1leg',3,[(-.385,-.385)]),
    ('table_corner_2legs',3,[(-.385,-.385),(.385,.385)]),
    ('table_top_tee',11,[]),
    ('table_top_center',15,[]),
]
modules={};stats=[]
for name,mask,positions in specs:
    top=make(name,mask)
    for other in kit.objects:other.select_set(False)
    top.select_set(True)
    for x,y in positions:
        support=make(name+'_Support')
        support.location=(x,y,0);support.select_set(True)
    bpy.context.view_layer.objects.active=top
    if positions:bpy.ops.object.join()
    top.data.calc_loop_triangles()
    top['leg_count']=len(positions)
    modules[name]=top
    export(top,OUT/(name+'.glb'))
    stats.append({'file':name+'.glb','neighbor_mask':mask,'legs':len(positions),
                  'leg_positions_blender':positions,'triangles':len(top.data.loop_triangles)})
for i,obj in enumerate(modules.values()):obj.location=((i%4)*1.25,(i//4)*1.4,0)
bpy.data.libraries.write(str(OUT/'modular_table_kit.blend'),{kit},path_remap='RELATIVE',fake_user=True,compress=True)

demo=bpy.data.scenes.new('Modular_Table_Compact_Examples')
examples={}
def rotated(mask,turns):
    # Positive Blender Z rotation maps east to north (Godot positive Y).
    for _ in range(turns):mask=((mask>>1)|((mask&1)<<3))&15
    return mask
def assemble(label,cells,offset,corner_legs=2):
    cells=set(cells);records=[];feet=[];total=0
    for x,y in sorted(cells):
        mask=sum(bit for dx,dy,bit in [(0,1,1),(1,0,2),(0,-1,4),(-1,0,8)] if (x+dx,y+dy) in cells)
        found=None
        for name,base,positions in specs:
            if name.startswith('table_corner_') and len(positions)!=corner_legs:continue
            for turns in range(4):
                if rotated(base,turns)==mask:found=(name,turns,positions);break
            if found:break
        assert found,mask
        name,turns,positions=found
        ob=modules[name].copy();ob.name=f'{label}_{x}_{y}_{name}'
        ob.location=(offset[0]+x*.9,offset[1]+y*.9,0)
        ob.rotation_euler=(0,0,turns*pi/2);demo.collection.objects.link(ob)
        total+=len(ob.data.loop_triangles)
        for px,py in positions:
            for _ in range(turns):px,py=-py,px
            feet.append((round(ob.location.x+px,5),round(ob.location.y+py,5)))
        records.append({'cell':[x,y],'file':name+'.glb','godot_y_degrees':turns*90})
    assert len(feet)==len(set(feet)), 'Duplicate supports'
    examples[label]={'modules':records,'legs':len(feet),'triangles':total}
assemble('Single',[(0,0)],(0,0))
assemble('Two',[(0,0),(1,0)],(1.6,0))
assemble('Three',[(0,0),(1,0),(2,0)],(0,1.8))
assemble('Corner',[(0,0),(1,0),(0,1)],(3.4,1.8))
assemble('Square',[(0,0),(1,0),(0,1),(1,1)],(0,4),corner_legs=1)
assemble('Tee',[(0,0),(1,0),(2,0),(1,1)],(3,4))
bpy.context.window.scene=demo
for obj in demo.objects:obj.select_set(False)
manifest={'pitch_m':.9,'height_m':.75,'mask_bits_godot':{'north_-Z':1,'east_+X':2,'south_+Z':4,'west_-X':8},
          'modules':stats,'examples':examples}
(OUT/'modules.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
bpy.data.libraries.write(str(OUT/'modular_table_examples.blend'),{demo},path_remap='RELATIVE',fake_user=True,compress=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.region_3d.view_location=Vector((2.2,2.3,.4));space.region_3d.view_distance=9
            space.region_3d.view_rotation=Vector((3,-7,7)).to_track_quat('Z','Y')
            space.region_3d.view_perspective='ORTHO'
print(json.dumps({'modules':stats,'examples':{k:{'legs':v['legs'],'triangles':v['triangles']} for k,v in examples.items()}}))
