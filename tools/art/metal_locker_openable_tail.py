"""Openable locker geometry stage; uses the shared texture builder context."""
from math import radians
scene=bpy.data.scenes.new('Metal_Locker_Opening');bpy.context.window.scene=scene
outer=[(-.39,0),(.39,0),(.45,.0643),(.45,1.7357),(.39,1.8),(-.39,1.8),(-.45,1.7357),(-.45,.0643)]
inner=[(-.38,.045),(.38,.045),(.405,.07),(.405,1.73),(.38,1.755),(-.38,1.755),(-.405,1.73),(-.405,.07)]
def uv(name,u,v):
    x,y,w,h=patches[name]
    return ((x+.001+u*(w-.002))/64,1-(y+.001+v*(h-.002))/64)
def create_mesh(name,verts,faces,coords):
    mesh=bpy.data.meshes.new(name+'_Shared_Mesh');mesh.from_pydata(verts,[],faces);mesh.materials.append(mat)
    layer=mesh.uv_layers.new(name='Locker_Shared_UV')
    for poly,values in zip(mesh.polygons,coords):
        for index,value in zip(poly.loop_indices,values):layer.data[index].uv=value
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges)
    bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    return obj
def rectangle(p):return [uv(p,0,1),uv(p,1,1),uv(p,1,0),uv(p,0,0)]
# A continuous thick shell with front rim, cavity walls and inner/outer back.
verts=[(x,y,z) for shape,y in [(outer,-.262),(outer,.3),(inner,-.262),(inner,.255)] for x,z in shape]
faces=[];coords=[]
def face(ids,p,values=None):faces.append(ids);coords.append(values or rectangle(p))
face(list(reversed(range(8,16))),'back',[uv('back',(.45-outer[i][0])/.9,1-outer[i][1]/1.8) for i in reversed(range(8))])
face(list(range(24,32)),'interior',[uv('interior',(x+.405)/.81,1-(z-.045)/1.71) for x,z in inner])
for i in range(8):
    j=(i+1)%8
    face([i,8+i,8+j,j],'top' if i in (3,4,5) else 'side')
    face([i,j,16+j,16+i],'side',[uv('side',0,.5)]*4)
    face([16+i,16+j,24+j,24+i],'inner_plain')
def shelf(z):
    n=len(verts)
    verts.extend([(x,y,h) for h in (z-.025,z) for x,y in [(-.405,-.225),(.405,-.225),(.405,.255),(-.405,.255)]])
    face([n+3,n+2,n+1,n],'inner_plain')
    face([n+4,n+5,n+6,n+7],'shelf')
    for i in range(4):
        j=(i+1)%4;face([n+i,n+j,n+4+j,n+4+i],'shelf_edge')
shelf(.40);shelf(1.29)
body=create_mesh('Locker_Body',verts,faces,coords)
# Door local origin is the left hinge. Closed transform is zero rotation.
hinge=Vector((-.45,-.2825,0))
verts=[tuple(Vector((x,y,z))-hinge) for y in (-.3,-.265) for x,z in outer]
faces=[list(range(8)),list(reversed(range(8,16)))]
coords=[[uv('front',(x+.45)/.9,1-z/1.8) for x,z in outer],
        [uv('door_inside',(.45-outer[i][0])/.9,1-outer[i][1]/1.8) for i in reversed(range(8))]]
for i in range(8):
    j=(i+1)%8;faces.append([i,8+i,8+j,j]);coords.append(rectangle('side'))
door=create_mesh('Locker_Door',verts,faces,coords);door.location=hinge
door['open_angle_degrees']=-105.0
root=bpy.data.objects.new('Metal_Locker',None);scene.collection.objects.link(root)
body.parent=root;door.parent=root
root['variant_manifest']='variants.json'
root['door_axis_blender']='Z';root['door_axis_godot']='Y'
for ob in scene.objects:ob.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.context.view_layer.update()
bpy.ops.export_scene.gltf(filepath=str(OUT/'metal_locker.glb'),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
path=OUT/'metal_locker.glb';blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n])
for sampler in g.get('samplers',[]):sampler.update(minFilter=9728,magFilter=9728)
payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4);tail=blob[20+n:]
path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4E4F534A)+payload+tail)
triangles=sum(len(o.data.loop_triangles) for o in (body,door))
assert len(g['meshes'])==2 and len(g['materials'])==1
assert sum(g['accessors'][p['indices']]['count']//3 for m in g['meshes'] for p in m['primitives'])==triangles
bpy.data.libraries.write(str(OUT/'metal_locker.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
demo=bpy.data.scenes.new('Metal_Locker_Opening_Variants')
for i,name in enumerate(['generic','blue','green','yellow']):
    offset=Vector((i*1.65,0,0))
    variant_mat=mat.copy();variant_mat.name='Locker_Open_Material_'+name
    next(n for n in variant_mat.node_tree.nodes if n.type=='TEX_IMAGE').image=images[name]
    for source in (body,door):
        ob=source.copy();ob.parent=None;ob.name=name+'_'+source.name
        ob.location=source.location+offset;demo.collection.objects.link(ob)
        ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=variant_mat
        if source==door:ob.rotation_euler.z=radians(-105 if i!=3 else 0)
bpy.context.window.scene=demo
bpy.data.libraries.write(str(OUT/'metal_locker_variants.blend'),{demo},path_remap='RELATIVE',fake_user=True,compress=True)
manifest={'mesh':'metal_locker.glb','mesh_parts':['Locker_Body','Locker_Door'],'triangles':triangles,
          'body_triangles':len(body.data.loop_triangles),'door_triangles':len(door.data.loop_triangles),
          'dimensions_m':[.9,.6,1.8],'shelf_heights_m':[.4,1.29],
          'door_hinge_blender':list(hinge),'door_open_godot_y_degrees':-105,
          'texture_size':[64,64],'variant_count':len(records),'variants':records,'source_sha256':source_hash}
(OUT/'variants.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_hash
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            spc=area.spaces.active;spc.region_3d.view_location=Vector((2.3,-.4,.9));spc.region_3d.view_distance=9
            spc.region_3d.view_rotation=Vector((2,-7,3)).to_track_quat('Z','Y');spc.region_3d.view_perspective='ORTHO'
print(json.dumps({'triangles':triangles,'body':len(body.data.loop_triangles),'door':len(door.data.loop_triangles),'variants':len(records),'source_unchanged':True}))
