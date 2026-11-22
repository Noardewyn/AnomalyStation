"""Low-poly tg metal chair, reconstructed from the four `chair` sprite directions."""
from pathlib import Path
import bpy
import bmesh
import json
import struct
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'godot/assets/tg/icons/obj/chairs.png'
OUT = ROOT / 'godot/assets/models/furniture/metal_chair'
OUT.mkdir(parents=True, exist_ok=True)
original = bpy.data.images.load(str(SOURCE), check_existing=True)
source_pixels = list(original.pixels)
sw, sh = original.size

def sample(frame, x, y):
    offset = ((sh - 1 - y) * sw + frame * 32 + x) * 4
    rgba = source_pixels[offset:offset + 4]
    return rgba if rgba[3] > 0.5 else [31/255,31/255,38/255,1]

# A compact atlas made only from source sprite pixels; no painted AI replacement.
size = 64
pixels = [31/255,31/255,38/255,1] * (size * size)
patches = {}

def put(x, y, rgba):
    offset = ((size - 1 - y) * size + x) * 4
    pixels[offset:offset + 4] = rgba

def patch(name, ox, oy, width, height, frame, sx, sy, source_width, source_height):
    patches[name] = (ox,oy,width,height)
    for y in range(-1,height+1):
        for x in range(-1,width+1):
            px = min(width-1,max(0,x))
            py = min(height-1,max(0,y))
            color = sample(frame, sx+min(source_width-1,px*source_width//width),
                           sy+min(source_height-1,py*source_height//height))
            put(ox+x,oy+y,color)

patch('back_front',1,1,15,15,4,8,3,15,15)
patch('back_rear',19,1,15,15,5,8,9,15,15)
patch('seat_top',1,19,17,17,4,7,18,17,7)
patch('seat_front',1,39,17,3,4,7,24,17,3)
patch('seat_side',21,19,17,3,6,9,23,17,3)
patch('leg',42,1,3,12,4,7,25,3,6)
palette = {'dark':(31,31,38),'edge':(76,79,82),'middle':(108,113,115),
           'silver':(142,149,150),'bright':(175,179,179)}
for n,(name,rgb) in enumerate(palette.items()):
    x,y=22+n*8,39
    patches[name]=(x,y,4,4)
    for py in range(y-1,y+5):
        for px in range(x-1,x+5):
            put(px,py,[c/255 for c in rgb]+[1])

image = bpy.data.images.new('Metal_Chair_Pixel_Atlas', width=size, height=size, alpha=False)
image.pixels.foreach_set(pixels)
image.update()
image.filepath_raw = str(OUT / 'metal_chair_albedo.png')
image.file_format = next(i.identifier for i in image.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
image.save()
image.pack()

material = bpy.data.materials.new('Metal_Chair_Pixel_Metal')
material.use_nodes = True
material.use_backface_culling = True
shader = next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Roughness'].default_value = 0.85
shader.inputs['Metallic'].default_value = 0.10
shader.inputs['Specular IOR Level'].default_value = 0.15
texture = material.node_tree.nodes.new('ShaderNodeTexImage')
texture.image = image
texture.interpolation = next(i.identifier for i in texture.bl_rna.properties['interpolation'].enum_items if i.identifier.lower()=='closest')
material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])

if bpy.context.object and bpy.context.object.mode!='OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.data.scenes.new('Metal_Chair')
bpy.context.window.scene = scene
vertices, faces, uv_faces = [], [], []

def uv_patch(name,u,v):
    x,y,w,h=patches[name]
    # Pixel-boundary mapping with a tiny inset prevents adjacent patch sampling.
    return ((x+0.001+u*(w-0.002))/size,1-(y+0.001+v*(h-0.002))/size)

def face(ids, name, coords=None):
    faces.append(ids)
    if coords is None:
        uv_faces.append([uv_patch(name,0.5,0.5)]*len(ids))
    else:
        uv_faces.append([uv_patch(name,u,v) for u,v in coords])

def octagon(x0,x1,y0,y1,corner):
    return [(x0+corner,y0),(x1-corner,y0),(x1,y0+corner),(x1,y1-corner),
            (x1-corner,y1),(x0+corner,y1),(x0,y1-corner),(x0,y0+corner)]

# The seat is a thin pressed-metal tray. Its three ribs live in the texture.
seat = octagon(-.272,.272,-.25,.23,.032)
base = len(vertices)
for z in [.418,.45]:
    vertices.extend((x,y,z) for x,y in seat)
face([base+i for i in reversed(range(8))],'dark')
face([base+8+i for i in range(8)],'seat_top',
     [((x+.272)/.544,(.23-y)/.48) for x,y in seat])
for i in range(8):
    j=(i+1)%8
    x0,y0=seat[i];x1,y1=seat[j]
    if max(y0,y1)<-.20:
        name='seat_front';u0=(x0+.272)/.544;u1=(x1+.272)/.544
    elif min(abs(x0),abs(x1))>.23:
        name='seat_side';u0=(y0+.25)/.48;u1=(y1+.25)/.48
    else:
        name='edge';u0=0;u1=1
    face([base+i,base+j,base+8+j,base+8+i],name,[(u0,1),(u1,1),(u1,0),(u0,0)])

# Chamfered corners follow the sprite silhouette; a slight backwards lean reads in profile.
back = octagon(-.24,.24,.49,.97,.032)
base = len(vertices)
for side in [-1,1]:
    vertices.extend((x,.205+(z-.49)*.10+side*.016,z) for x,z in back)
face([base+i for i in range(8)],'back_front',[((x+.24)/.48,(.97-z)/.48) for x,z in back])
face([base+8+i for i in reversed(range(8))],'back_rear',
     [((.24-back[i][0])/.48,(.97-back[i][1])/.48) for i in reversed(range(8))])
for i in range(8):
    j=(i+1)%8
    name='bright' if min(back[i][1],back[j][1])>.93 else 'edge'
    face([base+i,base+8+i,base+8+j,base+j],name)

def leg(name, bottom, top, width=.048, depth=.040):
    base=len(vertices)
    for center in (bottom,top):
        x,y,z=center
        vertices.extend([(x-width/2,y-depth/2,z),(x+width/2,y-depth/2,z),
                         (x+width/2,y+depth/2,z),(x-width/2,y+depth/2,z)])
    face([base+3,base+2,base+1,base],'dark')
    face([base+4,base+5,base+6,base+7],'edge')
    for i in range(4):
        j=(i+1)%4
        face([base+i,base+j,base+4+j,base+4+i],'leg',[(0,1),(1,1),(1,0),(0,0)])

for side in (-1,1):
    leg('Front', (side*.247,-.225,0),(side*.232,-.204,.435))
    leg('Rear', (side*.239,.230,0),(side*.217,.207,.508))

mesh=bpy.data.meshes.new('Metal_Chair_Mesh')
mesh.from_pydata(vertices,[],faces)
mesh.materials.append(material)
uv=mesh.uv_layers.new(name='Chair_Atlas_UV')
for polygon,values in zip(mesh.polygons,uv_faces):
    for loop,value in zip(polygon.loop_indices,values):
        uv.data[loop].uv=value
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges)
bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
obj=bpy.data.objects.new('Metal_Chair',mesh)
scene.collection.objects.link(obj)
obj['source_icon_state']='chair'
obj['source_frames']=[4,5,6,7]
obj['seat_height_m']=.45
obj['description']='Low-poly metal chair; original sprite pixels; four legs; solid ribbed seat and back'
scene['source_atlas']=str(SOURCE.relative_to(ROOT))
scene.unit_settings.system='METRIC'
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.shading.type='MATERIAL'
            space.region_3d.view_location=Vector((0,0,.48))
            space.region_3d.view_distance=1.8
            space.region_3d.view_rotation=Vector((3,-6,3)).to_track_quat('Z','Y')
            space.region_3d.view_perspective='ORTHO'
            space.overlay.show_floor=False

bpy.ops.export_scene.gltf(filepath=str(OUT/'metal_chair.glb'),use_selection=True,
                          use_active_scene=True,export_animations=False,export_extras=True)
path=OUT/'metal_chair.glb';blob=path.read_bytes();length=struct.unpack_from('<I',blob,12)[0]
gltf=json.loads(blob[20:20+length])
for sampler in gltf.get('samplers',[]):
    sampler.update(minFilter=9728,magFilter=9728)
payload=json.dumps(gltf,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
tail=blob[20+length:]
path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(payload)+len(tail))
                 +struct.pack('<II',len(payload),0x4E4F534A)+payload+tail)
bpy.data.libraries.write(str(OUT/'metal_chair.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.context.view_layer.update()
stats={'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'objects':1,
       'materials':1,'texture_px':[64,64],'dimensions_m':[round(v,4) for v in obj.dimensions],
       'source_frames':[4,5,6,7],'non_manifold_edges':0}
(OUT/'mesh_stats.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
print(json.dumps(stats,indent=2))
