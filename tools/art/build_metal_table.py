"""Build a 60-triangle metal table from the original isolated table sprite."""
from pathlib import Path
import bpy
import bmesh
import json
import struct
from math import pi
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'godot/assets/models/furniture/metal_table'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'godot/assets/tg/icons/obj/smooth_structures/table.png'
source = bpy.data.images.load(str(SOURCE), check_existing=True)
sw, sh = source.size
sp = list(source.pixels)
def sample(x, y):
    k = ((sh-1-y)*sw+x)*4
    return sp[k:k+4]

# Extract the original palette and strips, keeping the source untouched.
size = 64
pixels = sample(4,24)*(size*size)
def put(x, y, color):
    k = ((size-1-y)*size+x)*4
    pixels[k:k+4] = color
patches = {}
def patch(name, x, y, w, h, sampler):
    patches[name] = (x,y,w,h)
    for py in range(-1,h+1):
        for px in range(-1,w+1):
            put(x+px,y+py,sampler(min(w-1,max(0,px)),min(h-1,max(0,py))))
patch('top',1,1,28,28,lambda x,y: sample(2,2) if x in (0,27) or y in (0,27) else sample(4,4))
patch('rim',1,32,28,3,lambda x,y: sample(x+2,y+22))
patch('leg',32,1,3,6,lambda x,y: sample(x+3,y+25) if sample(x+3,y+25)[3] else sample(4,24))
patch('dark',38,1,4,4,lambda x,y: sample(4,24))
image = bpy.data.images.new('Metal_Table_Pixel_Atlas',width=size,height=size,alpha=False)
image.pixels.foreach_set(pixels)
image.update()
image.filepath_raw = str(OUT/'metal_table_albedo.png')
image.file_format = next(i.identifier for i in image.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
image.save()
image.pack()
mat = bpy.data.materials.new('Metal_Table_Pixel_Metal')
mat.use_nodes = True
mat.use_backface_culling = True
bsdf = next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bsdf.inputs['Roughness'].default_value = .85
bsdf.inputs['Metallic'].default_value = .1
bsdf.inputs['Specular IOR Level'].default_value = .15
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = image
tex.interpolation = next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier=='Closest')
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
if bpy.context.object and bpy.context.object.mode!='OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.data.scenes.new('Metal_Table')
bpy.context.window.scene = scene
verts, faces, uvs = [], [], []
def face(ids, patch_name, coords=((0,1),(1,1),(1,0),(0,0))):
    faces.append(ids)
    x,y,w,h = patches[patch_name]
    uvs.append([((x+.001+u*(w-.002))/size,1-(y+.001+v*(h-.002))/size) for u,v in coords])
def box(x0,x1,y0,y1,z0,z1,top,side):
    n = len(verts)
    verts.extend([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),
                  (x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)])
    face([n+3,n+2,n+1,n],'dark')
    face([n+4,n+5,n+6,n+7],top)
    for i in range(4):
        j = (i+1)%4
        face([n+i,n+j,n+4+j,n+4+i],side)
box(-.45,.45,-.45,.45,.66,.75,'top','rim')
for x in (-.385,.385):
    for y in (-.385,.385):
        box(x-.05,x+.05,y-.05,y+.05,0,.668,'dark','leg')
mesh = bpy.data.meshes.new('Metal_Table_Mesh')
mesh.from_pydata(verts,[],faces)
mesh.materials.append(mat)
uv = mesh.uv_layers.new(name='Table_Atlas_UV')
for poly,coords in zip(mesh.polygons,uvs):
    for loop,coord in zip(poly.loop_indices,coords):
        uv.data[loop].uv = coord
bm = bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges)
bm.to_mesh(mesh)
bm.free()
mesh.update()
mesh.calc_loop_triangles()
obj = bpy.data.objects.new('Metal_Table',mesh)
scene.collection.objects.link(obj)
obj['source_atlas'] = str(SOURCE.relative_to(ROOT))
obj['source_tile'] = 0
obj['chair_seat_height_m'] = .45
scene.unit_settings.system = next(i.identifier for i in scene.unit_settings.bl_rna.properties['system'].enum_items if i.identifier=='METRIC')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.context.view_layer.update()
bpy.ops.export_scene.gltf(filepath=str(OUT/'metal_table.glb'),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
path = OUT/'metal_table.glb'
blob = path.read_bytes()
length = struct.unpack_from('<I',blob,12)[0]
gltf = json.loads(blob[20:20+length])
for sampler in gltf.get('samplers',[]):
    sampler.update(minFilter=9728,magFilter=9728)
payload = json.dumps(gltf,separators=(',',':')).encode()
payload += b' '*(-len(payload)%4)
tail = blob[20+length:]
path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4E4F534A)+payload+tail)
bpy.data.libraries.write(str(OUT/'metal_table.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
stats = {'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),
         'objects':1,'materials':1,'texture_px':[64,64],
         'dimensions_m':[round(v,4) for v in obj.dimensions],
         'underside_height_m':.66,'non_manifold_edges':0}
(OUT/'mesh_stats.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')

# A separate live comparison scene leaves both original assets unchanged.
comparison = bpy.data.scenes.new('Metal_Table_Chair_Comparison')
comparison.collection.objects.link(obj)
chair = bpy.data.objects.get('Metal_Chair')
if chair:
    reference = chair.copy()
    reference.name = 'Chair_Scale_Reference'
    reference.location = (-.79,0,0)
    reference.rotation_euler = (0,0,pi/2)
    comparison.collection.objects.link(reference)
bpy.context.window.scene = comparison
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space = area.spaces.active
            space.shading.type = next(i.identifier for i in space.shading.bl_rna.properties['type'].enum_items if i.identifier=='MATERIAL')
            space.region_3d.view_location = Vector((-.25,0,.45))
            space.region_3d.view_distance = 2.8
            space.region_3d.view_rotation = Vector((2,-4,2.7)).to_track_quat('Z','Y')
            space.region_3d.view_perspective = 'ORTHO'
print(json.dumps(stats))
