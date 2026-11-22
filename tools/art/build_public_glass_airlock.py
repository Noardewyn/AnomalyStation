"""Public glass airlock: two independent glazed sliding leaves. Run in Blender."""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/doors/public_glass';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'godot/assets/tg/icons/obj/doors/airlocks/public/glass.png'
def enum(obj,prop,value):return next(i.identifier for i in obj.bl_rna.properties[prop].enum_items if i.identifier==value)
scene=bpy.data.scenes.new('Public_Glass_Airlock');bpy.context.window.scene=scene
scene['source_atlas']=str(SOURCE.relative_to(ROOT));scene['parts']='Door_Left, Door_Right'
atlas=bpy.data.images.load(str(SOURCE),check_existing=True);atlas.pack();W,H=atlas.size
def mat(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=True
    s=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');s.inputs['Base Color'].default_value=(*color,1);s.inputs['Roughness'].default_value=.8
    return m,s
face,bs=mat('Public_Glass_Original_Atlas',(.3,.3,.3))
tex=face.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.interpolation=enum(tex,'interpolation','Closest');face.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
edge,_=mat('Public_Glass_Dark_Edge',(.025,.028,.03))
red,_=mat('Public_Glass_Steel_Edge',(.13,.13,.14))
glass,bs=mat('Public_Glass_Tinted_Glass',(.10,.14,.15));bs.inputs['Alpha'].default_value=.35;bs.inputs['Roughness'].default_value=.22
glass.surface_render_method=enum(glass,'surface_render_method','BLENDED');glass['opacity']=.35;glass['thickness_m']=.16/3
mats=[face,edge,red,glass]
def create(name,verts,faces,slots,origin=(0,0,0),tile=0):
    mesh=bpy.data.meshes.new(name+'_Mesh');mesh.from_pydata([Vector(v)-Vector(origin) for v in verts],[],faces)
    for m in mats:mesh.materials.append(m)
    for p,s in zip(mesh.polygons,slots):p.material_index=s
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit={'MATERIAL'})
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges)
    bm.to_mesh(mesh);bm.free();mesh.update()
    layer=mesh.uv_layers.new(name='Original_Atlas_UV')
    for p in mesh.polygons:
        p.use_smooth=False
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co+Vector(origin)
            px=(co.x+1)*16
            layer.data[li].uv=((tile+px)/W,1-(2-co.z)*16/H)
    mesh.calc_loop_triangles();ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.location=origin;return ob
def xyz(p,depth):return (p[0]/16-1,depth,2-p[1]/16)
# Pixel-aligned metal and closed glass volumes, joined into two leaf objects.
pixels=list(atlas.pixels)
window={(x,y) for y in range(32) for x in range(32) if pixels[((H-1-y)*W+x)*4+3]<.5}
leaves=[]
for left in [True,False]:
    name='Door_Left' if left else 'Door_Right';origin=(-.5 if left else .5,0,0)
    split=lambda y:17 if 6<=y<12 or 20<=y<26 else 15 if 12<=y<20 else 16
    region={(x,y) for x in range(32) for y in range(32) if (x<split(y))==left and min(x+y,(31-x)+y,x+(31-y),(31-x)+(31-y))>=2}
    steel=region-window;verts=[];faces=[];slots=[];lookup={}
    def v(x,y,d,component):
        key=(x,y,d,component)
        if key not in lookup:lookup[key]=len(verts);verts.append(xyz((x,y),d))
        return lookup[key]
    def quad(points,slot,component):faces.append([v(*p,component) for p in points]);slots.append(slot)
    for cells,depth,component in [(steel,.08,'metal'),(region&window,.16/6,'glass')]:
        for x,y in sorted(cells):
            for side in [-1,1]:quad([(x,y,side*depth),(x+1,y,side*depth),(x+1,y+1,side*depth),(x,y+1,side*depth)],0 if component=='metal' else 3,component)
            for dx,dy,a,b in [(-1,0,(x,y),(x,y+1)),(1,0,(x+1,y),(x+1,y+1)),(0,-1,(x,y),(x+1,y)),(0,1,(x,y+1),(x+1,y+1))]:
                if (x+dx,y+dy) not in cells:
                    quad([(*a,-depth),(*b,-depth),(*b,depth),(*a,depth)],3 if component=='glass' else 1 if (x+dx,y+dy) in window else 2,component)
    ob=create(name,verts,faces,slots,origin)
    ob['opening_axis']='X';ob['godot_opening_axis']='X';ob['open_offset_m']=-1.125 if left else 1.125
    ob['glass_thickness_m']=.16/3;leaves.append(ob)
objects=leaves
root=bpy.data.objects.new('Public_Glass_Airlock',None);scene.collection.objects.link(root)
for ob in objects:ob.parent=root;ob.select_set(True)
root.select_set(True);root['dimensions_m']=[2,.16,2];root['opening']='Horizontal: left -X and right +X in Blender and Godot.'
bpy.context.view_layer.objects.active=leaves[0]
path=OUT/'public_glass_airlock.glb';bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
for sampler in g['samplers']:sampler.update(minFilter=9728,magFilter=9728)
for node in g['nodes']:node['name']=node['name'].split('.')[0]
raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
assert len(g['meshes'])==2
for im in g['images']:
    view=g['bufferViews'][im['bufferView']];start=view.get('byteOffset',0);assert tail[8+start:8+start+view['byteLength']]==SOURCE.read_bytes()
assert any(m.get('alphaMode')=='BLEND' for m in g['materials'])
stats={ob.name.split('.')[0]:{'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices)} for ob in objects}
(OUT/'model_info.json').write_text(json.dumps({'parts':stats,'atlas_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'glass_alpha':.35,'glass_thickness_m':.16/3,'leaf_thickness_m':.16,'opening_offsets_godot_x':[-1.125,1.125],'nearest':True,'original_png_embedded':True},indent=2)+'\n')
bpy.data.libraries.write(str(OUT/'public_glass_airlock.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
# Separate preview scene keeps the actual asset at exactly two meshes.
preview=bpy.data.scenes.new('Public_Glass_Airlock_Preview');bpy.context.window.scene=preview
for pose,x in [('Closed',-2.5),('Open',1.4)]:
    for original in objects:
        ob=original.copy();ob.parent=None;preview.collection.objects.link(ob);ob.name=pose+'_'+original.name;ob.location=original.location+Vector((x,0,0))
        if pose=='Open' and 'open_offset_m' in ob:ob.location.x+=ob['open_offset_m']
world=bpy.data.worlds.new('Public_Glass_Preview_World');world.use_nodes=True;preview.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.15,.19,1);bg.inputs['Strength'].default_value=.7
for name,rot,energy in [('Key',(25,-30,-25),2),('Fill',(45,20,140),1)]:
    ld=bpy.data.lights.new(name,next(i.identifier for i in bpy.types.Light.bl_rna.properties['type'].enum_items if i.identifier=='SUN'));ld.energy=energy;ld.angle=.25
    ob=bpy.data.objects.new(name,ld);preview.collection.objects.link(ob);ob.rotation_euler=[math.radians(v) for v in rot]
data=bpy.data.cameras.new('Public_Glass_Preview');data.type=enum(data,'type','ORTHO');data.ortho_scale=8.4
cam=bpy.data.objects.new('Public_Glass_Preview',data);preview.collection.objects.link(cam);target=Vector((0,0,1));cam.location=target+Vector((2,-10,3));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.resolution_x=1400;preview.render.resolution_y=950;preview.render.resolution_percentage=100;preview.render.image_settings.file_format=enum(preview.render.image_settings,'file_format','PNG');preview.render.filepath=str(ROOT/'docs/ai/art/public_glass_airlock_preview.png')
bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'public_glass_airlock_preview.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.context.window.scene=scene
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=enum(sp.shading,'type','MATERIAL');sp.region_3d.view_location=(0,0,1);sp.region_3d.view_distance=4;sp.region_3d.view_rotation=Vector((2,-8,2)).to_track_quat('Z','Y');sp.overlay.show_floor=False
print(json.dumps({'parts':stats,'triangles':sum(v['triangles'] for v in stats.values()),'scene':scene.name}))

