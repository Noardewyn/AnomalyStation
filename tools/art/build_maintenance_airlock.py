"""Build the two-leaf maintenance airlock in Blender; run with bpy available."""
from pathlib import Path
import bpy
import bmesh
import json
import struct
import runpy
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'godot/assets/tg/icons/obj/doors/airlocks/station/maintenance.png'
OUT = ROOT / 'godot/assets/models/doors/maintenance'
OUT.mkdir(parents=True, exist_ok=True)
BODY_THICKNESS = 0.16
GLASS_THICKNESS = BODY_THICKNESS / 3
HALF_BODY = BODY_THICKNESS / 2
HALF_GLASS = GLASS_THICKNESS / 2
HALF_RIM = 0.068
HALF_PANEL = 0.10

# Keep the source scene intact; this scene contains exactly the two door leaves.
previous = bpy.data.scenes.get('Maintenance_Airlock')
if previous and previous.get('source_atlas') == str(SOURCE.relative_to(ROOT)):
    for obj in list(previous.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(previous)
for mesh in list(bpy.data.meshes):
    if mesh.name.startswith(('Maintenance_Left_Mesh', 'Maintenance_Right_Mesh')) and mesh.users == 0:
        bpy.data.meshes.remove(mesh)
for mat in list(bpy.data.materials):
    if mat.name.startswith('Maintenance_') and mat.users == 0:
        bpy.data.materials.remove(mat)
scene = bpy.data.scenes.new('Maintenance_Airlock')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
atlas = bpy.data.images.load(str(SOURCE), check_existing=True)
atlas.pack()
w, h = atlas.size
pixels = list(atlas.pixels)
mask = {(x, y) for y in range(32) for x in range(32)
        if pixels[((h - 1 - y) * w + x) * 4 + 3] > 0.5}

def solid_material(name, rgb):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    mat.node_tree.nodes.clear()
    shader = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    output = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = 1
    mat.diffuse_color = (*rgb, 1)
    return mat

face_mat = solid_material('Maintenance_Original_Pixel_Atlas', (0.3, 0.3, 0.3))
nodes = face_mat.node_tree.nodes
shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
tex = nodes.new('ShaderNodeTexImage')
tex.image = atlas
tex.interpolation = next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items
                         if i.identifier.lower() == 'closest')
tex.label = 'Original atlas: top-left 32 x 32; nearest filtering'
face_mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
tex.location = (-320, 100)
face_mat['filtering'] = 'NEAREST; no texture smoothing'
steel = solid_material('Maintenance_Dark_Steel_Edges', (0.063, 0.063, 0.063))
yellow = solid_material('Maintenance_Ochre_Edges', (0.39, 0.29, 0.045))
glazing = solid_material('Maintenance_Translucent_Inserts', (0.14, 0.19, 0.20))
glass_shader = next(n for n in glazing.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
glass_shader.inputs['Alpha'].default_value = 0.35
glass_shader.inputs['Roughness'].default_value = 0.4
glazing.diffuse_color = (0.14, 0.19, 0.20, 0.35)
glazing['opacity'] = 0.35
glazing['thickness_m'] = GLASS_THICKNESS
glazing.surface_render_method = next(i.identifier for i in glazing.bl_rna.properties['surface_render_method'].enum_items
                                     if i.identifier == 'BLENDED')

def depth(x, y):
    # Three restrained depth levels, in metres. No per-pixel surface noise.
    if (x, y) not in mask:
        return HALF_GLASS
    if 22 <= x < 28 and 14 <= y < 21:
        return HALF_PANEL
    if x < 2 or x >= 30 or y < 2 or y >= 30:
        return HALF_RIM
    return HALF_BODY

leaves = []
stats = []
split = [16,16,16,16,16,17,18,18,18,18,18,18,17,16,16,16,
         16,16,16,17,18,18,18,18,18,18,17,16,16,16,16,16]
depth_levels = [-HALF_PANEL,-HALF_BODY,-HALF_RIM,-HALF_GLASS,
                HALF_GLASS,HALF_RIM,HALF_BODY,HALF_PANEL]
for label, lo, hi, origin_x in [('Left', 0, 16, -0.5), ('Right', 16, 32, 0.5)]:
    region = {(x, y) for x in range(32) for y in range(32)
              if (x < split[y]) == (label == 'Left')}
    vertices, faces, mats, lookup = [], [], [], {}
    def vertex(x, y, d):
        key = (x, y, d)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append((x / 16 - 1 - origin_x, d, 2 - y / 16))
        return lookup[key]
    def quad(coords, material):
        faces.append([vertex(*p) for p in coords])
        mats.append(material)
    for x, y in sorted(region):
        d = depth(x, y)
        for side in (-1, 1):
            quad([(x,y,side*d),(x+1,y,side*d),(x+1,y+1,side*d),(x,y+1,side*d)],
                 0 if (x, y) in mask else 3)
        for dx, dy, a, b in [(-1,0,(x,y),(x,y+1)), (1,0,(x+1,y),(x+1,y+1)),
                             (0,-1,(x,y),(x+1,y)), (0,1,(x,y+1),(x+1,y+1))]:
            neighbor = (x+dx, y+dy)
            nd = depth(*neighbor) if neighbor in region else 0
            if nd >= d:
                continue
            intervals = [(-d, d)] if nd == 0 else [(-d, -nd), (nd, d)]
            edge_mat = 2 if (dx and (x < 2 or x >= 30) and y in (14,15,16,18,19,20)) else 1
            for d0, d1 in intervals:
                cuts = [d0] + [v for v in depth_levels if d0 < v < d1] + [d1]
                for low, high in zip(cuts, cuts[1:]):
                    quad([(*a,low),(*b,low),(*b,high),(*a,high)], edge_mat)
    mesh = bpy.data.meshes.new('Maintenance_' + label + '_Mesh')
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.clear()
    for mat in (face_mat, steel, yellow, glazing):
        mesh.materials.append(mat)
    for poly, material in zip(mesh.polygons, mats):
        poly.material_index = material
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    bmesh.ops.dissolve_limit(bm, angle_limit=0.001, use_dissolve_boundaries=False,
                           verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    uv = mesh.uv_layers.new(name='Original_Atlas_UV')
    for poly in mesh.polygons:
        poly.use_smooth = False
        for loop in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop].vertex_index].co
            # Both faces sample the same physical half, retaining asymmetrical openings.
            # From the back this naturally reads as the reverse of the same door.
            px = (co.x + origin_x + 1) * 16
            py = (2 - co.z) * 16
            uv.data[loop].uv = (px / w, 1 - py / h)
    mesh.update()
    obj = bpy.data.objects.new('Door_' + label, mesh)
    scene.collection.objects.link(obj)
    obj.location.x = origin_x
    obj['opening_axis'] = 'X'
    obj['open_offset_m'] = -1.125 if label == 'Left' else 1.125
    obj['source_tile_px'] = [0, 0, 32, 32]
    obj['description'] = 'Independent sliding leaf; recessed translucent inserts; pixel atlas on both faces'
    leaves.append(obj)
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    stats.append({'object':obj.name, 'vertices':len(mesh.vertices),
                  'triangles':len(mesh.loop_triangles),
                  'non_manifold_edges':sum(not e.is_manifold for e in bm.edges)})
    bm.free()

scene['source_atlas'] = str(SOURCE.relative_to(ROOT))
scene['dimensions_m'] = [2.0, HALF_PANEL * 2, 2.0]
scene['body_thickness_m'] = BODY_THICKNESS
scene['glass_thickness_m'] = GLASS_THICKNESS
scene['source_tile_top_left_px'] = [0, 0, 32, 32]
scene['notes'] = 'Two leaves only. Slide Door_Left -X and Door_Right +X. Front is -Y; Z is up.'
for obj in leaves:
    obj.select_set(True)
bpy.context.view_layer.objects.active = leaves[0]

# Stage the live viewport without adding a floor, lights, or camera to the asset.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.overlay.show_floor = False
            space.overlay.show_axis_x = False
            space.overlay.show_axis_y = False
            space.region_3d.view_distance = 4.2
            space.region_3d.view_location = Vector((0,0,1))
            space.region_3d.view_rotation = Vector((3,-7,2.5)).to_track_quat('Z','Y')
            space.region_3d.view_perspective = 'ORTHO'

seam_tools = runpy.run_path(str(Path(__file__).with_name('maintenance_seam.py')))
seam_tools['apply_seam_finish'](leaves, atlas)
glass_tools = runpy.run_path(str(Path(__file__).with_name('close_maintenance_glazing.py')))
glass_tools['close_glazing'](leaves)
for obj, entry in zip(leaves, stats):
    obj.data.calc_loop_triangles()
    entry['vertices'] = len(obj.data.vertices)
    entry['triangles'] = len(obj.data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    entry['non_manifold_edges'] = sum(not e.is_manifold for e in bm.edges)
    entry['opaque_non_manifold_edges'] = sum(not e.is_manifold and any(f.material_index != 3 for f in e.link_faces) for e in bm.edges)
    bm.free()

# Selected-only export guarantees exactly two mesh nodes and embedded original PNG.
bpy.ops.export_scene.gltf(filepath=str(OUT / 'maintenance_airlock.glb'),
    use_selection=True, use_active_scene=True, export_animations=False, export_extras=True)
# Blender exports Closest with nearest mipmaps. Force plain NEAREST at all distances
# to avoid atlas mipmap bleed and preserve the user's unsmoothed pixel-art request.
glb_path = OUT / 'maintenance_airlock.glb'
blob = glb_path.read_bytes()
json_size = struct.unpack_from('<I', blob, 12)[0]
gltf = json.loads(blob[20:20 + json_size])
for sampler in gltf.get('samplers', []):
    sampler['magFilter'] = 9728
    sampler['minFilter'] = 9728
encoded = json.dumps(gltf, separators=(',', ':')).encode('utf-8')
encoded += b' ' * (-len(encoded) % 4)
tail = blob[20 + json_size:]
glb_path.write_bytes(struct.pack('<III', 0x46546C67, 2, 20 + len(encoded) + len(tail))
                     + struct.pack('<II', len(encoded), 0x4E4F534A) + encoded + tail)
assert len(gltf['meshes']) == 2
assert sum(s['opaque_non_manifold_edges'] for s in stats) == 0
assert sum(s['non_manifold_edges'] for s in stats) == 0
# Write only the asset scene and its dependencies; the user's original scene is untouched.
bpy.data.libraries.write(str(OUT / 'maintenance_airlock.blend'), {scene},
                         path_remap='RELATIVE', fake_user=True, compress=True)
(OUT / 'mesh_stats.json').write_text(json.dumps(stats, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'output':str(OUT), 'stats':stats}, indent=2))
