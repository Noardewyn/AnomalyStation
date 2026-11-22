"""Original-atlas vault door and shared four-section hatch. Run in Blender."""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ASSETS=ROOT/'godot/assets/tg/icons/obj/doors/airlocks'
def enum(obj,prop,val):return next(i.identifier for i in obj.bl_rna.properties[prop].enum_items if i.identifier==val)
def material(name,path):
    image=bpy.data.images.load(str(path),check_existing=True);image.pack()
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.75
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation=enum(tex,'interpolation','Closest');m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    return m
def mesh_object(scene,name,cells,origin,mat,offset,lock=False):
    verts=[];faces=[];lookup={}
    unit=.5 if name.startswith('Hatch_') else 1
    def vertex(x,y,d):
        key=(x,y,d)
        if key not in lookup:lookup[key]=len(verts);verts.append((x*unit/16-1-origin[0],d,2-y*unit/16-origin[2]))
        return lookup[key]
    def face(points):faces.append([vertex(*p) for p in points])
    depth=.11 if name.startswith('Vault_') else .08
    for x,y in sorted(cells):
        for side in [-1,1]:face([(x,y,side*depth),(x+1,y,side*depth),(x+1,y+1,side*depth),(x,y+1,side*depth)])
        for dx,dy,a,b in [(-1,0,(x,y),(x,y+1)),(1,0,(x+1,y),(x+1,y+1)),(0,-1,(x,y),(x+1,y)),(0,1,(x,y+1),(x+1,y+1))]:
            if (x+dx,y+dy) not in cells:face([(*a,-depth),(*b,-depth),(*b,depth),(*a,depth)])
    if lock:
        # A shallow sixteen-sided raised locking boss, part of the right leaf mesh.
        for sign in [-1,1]:
            rings=[]
            for d in [sign*.109,sign*.155]:
                rings.append([vertex(16+5.4*math.cos(i*math.tau/16),16+5.4*math.sin(i*math.tau/16),d) for i in range(16)])
            faces.extend([rings[0][::-1],rings[1]])
            for i in range(16):j=(i+1)%16;faces.append([rings[0][i],rings[0][j],rings[1][j],rings[1][i]])
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(verts,[],faces);me.materials.append(mat)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(me);bm.free();me.update()
    uv=me.uv_layers.new(name='Original_Atlas_UV')
    for poly in me.polygons:
        poly.use_smooth=False
        for li in poly.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co+Vector(origin)
            # Dark atlas pixel on hidden thickness surfaces, same material for all skins.
            uv.data[li].uv=((co.x+1)*16/128,1-(2-co.z)*16/128) if abs(poly.normal.y)>.9 else (15.5/128,1-15.5/128)
    me.calc_loop_triangles();ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.location=origin;ob['open_offset_blender']=offset;ob['open_offset_godot']=[offset[0],offset[2],-offset[1]];return ob
def export(scene,parts,path,source):
    bpy.context.window.scene=scene
    for ob in scene.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for s in g['samplers']:s.update(minFilter=9728,magFilter=9728)
    for node in g['nodes']:node['name']=node['name'].split('.')[0]
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    assert len(g['meshes'])==len(parts)
    for im in g['images']:
        v=g['bufferViews'][im['bufferView']];o=v.get('byteOffset',0);assert tail[8+o:8+o+v['byteLength']]==source.read_bytes()
    return {o.name.split('.')[0]:{'triangles':len(o.data.loop_triangles),'open_offset_godot':list(o['open_offset_godot'])} for o in parts}
scenes=[];groups=[]
for kind in ['vault','hatch']:
    out=ROOT/'godot/assets/models/doors'/kind;out.mkdir(parents=True,exist_ok=True)
    source=ASSETS/('vault/vault.png' if kind=='vault' else 'hatch/centcom.png')
    scene=bpy.data.scenes.new(kind.title()+'_Airlock');scenes.append(scene)
    mat=material(kind.title()+'_Original_Atlas',source);parts=[]
    if kind=='vault':
        # Circular lock stays on the right leaf in the original opening frames.
        for left in [True,False]:
            def seam(y):
                dy=(y+.5)-16
                return min(16,int(16-math.sqrt(max(0,6.5**2-dy**2))+.5)) if abs(dy)<6.5 else 16
            cells={(x,y) for x in range(32) for y in range(32) if (x<seam(y))==left}
            parts.append(mesh_object(scene,'Vault_Left' if left else 'Vault_Right',cells,(-.5 if left else .5,0,0),mat,(-1.5 if left else 1.5,0,0),lock=not left))
    else:
        # Follow the centers of the actual one-pixel black seams. A half-pixel
        # grid leaves half the black line on each adjoining section.
        def vertical_joint(y):
            return 16.5 if 4<=y<12 or 16<=y<20 or 28<=y else 15.5
        def horizontal_joint(x):
            return 16.5 if x<4 or 12<=x<17 or 20<=x<28 else 15.5
        regions=[]
        for left,upper in [(True,True),(False,True),(True,False),(False,False)]:
            cells={(x,y) for x in range(64) for y in range(64) if (x<2*vertical_joint((y+.5)/2))==left and (y<2*horizontal_joint((x+.5)/2))==upper}
            regions.append(cells)
            origin=(-.5 if left else .5,0,1.5 if upper else .5);offset=(-1.05 if left else 1.05,0,1.05 if upper else -1.05)
            parts.append(mesh_object(scene,'Hatch_'+('Upper' if upper else 'Lower')+'_'+('Left' if left else 'Right'),cells,origin,mat,offset))
        assert len(set.union(*regions))==4096
        assert sum(map(len,regions))==4096
    root=bpy.data.objects.new(kind.title()+'_Airlock',None);scene.collection.objects.link(root)
    for ob in parts:ob.parent=root
    stats=export(scene,parts,out/(kind+'_airlock.glb'),source)
    data={'parts':stats,'triangles':sum(s['triangles'] for s in stats.values()),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'source_texture':str(source.relative_to(ROOT/'godot')),'nearest':True,'original_png_embedded':True}
    groups.append((kind,parts,None));save_scenes={scene}
    if kind=='hatch':
        alt_source=ASSETS/'hatch/maintenance.png';altmat=material('Hatch_Maintenance_Original_Atlas',alt_source)
        alt=bpy.data.scenes.new('Hatch_Maintenance');scenes.append(alt);altparts=[]
        for ob in parts:
            copy=ob.copy();copy.parent=None;alt.collection.objects.link(copy);copy.name=ob.name+'_Maintenance';copy.material_slots[0].link='OBJECT';copy.material_slots[0].material=altmat;altparts.append(copy)
        assert {o.data.as_pointer() for o in parts}=={o.data.as_pointer() for o in altparts}
        save_scenes.add(alt);groups.append(('hatch_maintenance',altparts,None))
        data['appearances']={'centcom':str(source.relative_to(ROOT/'godot')),'maintenance':str(alt_source.relative_to(ROOT/'godot'))}
        data['maintenance_sha256']=hashlib.sha256(alt_source.read_bytes()).hexdigest();data['shared_meshes']=4
    (out/'model_info.json').write_text(json.dumps(data,indent=2)+'\n')
    bpy.data.libraries.write(str(out/(kind+'_airlock.blend')),save_scenes,path_remap='RELATIVE',fake_user=True,compress=True)
    print(kind,json.dumps(data))
preview=bpy.data.scenes.new('Vault_Hatch_Preview');bpy.context.window.scene=preview
for row,(kind,parts,_) in enumerate(groups):
    for opened,cx in [(False,-2),(True,2)]:
        for original in parts:
            ob=original.copy();ob.parent=None;preview.collection.objects.link(ob);ob.name=kind+('_Open_' if opened else '_Closed_')+original.name;ob.location=original.location+Vector((cx,0,row*4.5))
            if opened:ob.location+=Vector(original['open_offset_blender'])
world=bpy.data.worlds.new('Vault_Hatch_World');world.use_nodes=True;preview.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.1,.12,.15,1);bg.inputs['Strength'].default_value=.8
for name,rot,energy in [('Key',(25,-30,-25),2),('Fill',(45,20,140),1)]:
    ld=bpy.data.lights.new(name,next(i.identifier for i in bpy.types.Light.bl_rna.properties['type'].enum_items if i.identifier=='SUN'));ld.energy=energy;ld.angle=.25
    ob=bpy.data.objects.new(name,ld);preview.collection.objects.link(ob);ob.rotation_euler=[math.radians(v) for v in rot]
data=bpy.data.cameras.new('Vault_Hatch_Camera');data.type=enum(data,'type','ORTHO');data.ortho_scale=14.5
cam=bpy.data.objects.new('Vault_Hatch_Camera',data);preview.collection.objects.link(cam);target=Vector((0,0,6));cam.location=target+Vector((2,-18,2));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.resolution_x=1200;preview.render.resolution_y=1600;preview.render.resolution_percentage=100
preview.render.image_settings.file_format=enum(preview.render.image_settings,'file_format','PNG');preview.render.filepath=str(ROOT/'docs/ai/art/vault_hatch_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(ROOT/'godot/assets/models/doors/hatch/vault_hatch_preview.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=enum(sp.shading,'type','MATERIAL');sp.region_3d.view_location=target;sp.region_3d.view_distance=15;sp.region_3d.view_rotation=Vector((1,-6,4)).to_track_quat('Z','Y');sp.overlay.show_floor=False

