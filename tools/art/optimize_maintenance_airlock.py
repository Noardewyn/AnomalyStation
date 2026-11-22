"""Reduce inset glazing to double-sided sheets while retaining the metal shell."""
import bpy
import bmesh


def flatten_rim(objects):
    result = []
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        before = len(mesh.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        glass = next(i for i, mat in enumerate(mesh.materials)
                     if mat.name == 'Maintenance_Translucent_Inserts')
        metal_vertices = [v for v in bm.verts if any(f.material_index != glass for f in v.link_faces)]
        for vertex in metal_vertices:
            if abs(abs(vertex.co.y) - 0.085) < 0.00001:
                vertex.co.y = 0.1 if vertex.co.y > 0 else -0.1
        bmesh.ops.remove_doubles(bm, verts=metal_vertices, dist=0.000001)
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
        bm.normal_update()
        bmesh.ops.dissolve_limit(bm, angle_limit=0.001, use_dissolve_boundaries=False,
                               verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
        uv = bm.loops.layers.uv.active
        closed_x = -0.5 if obj.name == 'Door_Left' else 0.5
        for face in bm.faces:
            if face.material_index == 0:
                for loop in face.loops:
                    loop[uv].uv = ((loop.vert.co.x + closed_x + 1) * 16 / 192,
                                   1 - (2 - loop.vert.co.z) * 16 / 160)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        mesh.calc_loop_triangles()
        result.append({'object':obj.name, 'before':before, 'after':len(mesh.loop_triangles)})
    return result


def optimized_mesh(obj):
    source = obj.data
    glass = next(i for i, mat in enumerate(source.materials)
                 if mat.name == 'Maintenance_Translucent_Inserts')
    vertices, faces, materials, uvs, lookup = [], [], [], [], {}
    seen_glass = set()
    source_uv = source.uv_layers.active.data
    for polygon in source.polygons:
        coordinates = []
        face_uv = []
        for loop_index in polygon.loop_indices:
            v = source.vertices[source.loops[loop_index].vertex_index].co.copy()
            if abs(abs(v.y) - 0.008) < 0.00001:
                v.y = 0
            coordinates.append(tuple(v))
            face_uv.append(tuple(source_uv[loop_index].uv))
        is_glass = polygon.material_index == glass
        if is_glass:
            signature = tuple(sorted(coordinates))
            if signature in seen_glass:
                continue
            seen_glass.add(signature)
        face = []
        for co in coordinates:
            # Separate glazing boundaries from the watertight opaque frame.
            key = (co, is_glass)
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(co)
            face.append(lookup[key])
        if len(set(face)) < 3:
            continue
        faces.append(face)
        materials.append(polygon.material_index)
        uvs.append(face_uv)
    mesh = bpy.data.meshes.new(source.name + '_Optimized')
    mesh.from_pydata(vertices, [], faces)
    for material in source.materials:
        mesh.materials.append(material)
    uv_layer = mesh.uv_layers.new(name='Original_Atlas_UV')
    for polygon, mat, face_uv in zip(mesh.polygons, materials, uvs):
        polygon.material_index = mat
        for index, value in zip(polygon.loop_indices, face_uv):
            uv_layer.data[index].uv = value if mat == 0 else (0,0)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    bmesh.ops.dissolve_limit(bm, angle_limit=0.001, use_dissolve_boundaries=False,
                           verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
    # Restore exact planar atlas projection after surface simplification.
    uv = bm.loops.layers.uv.active
    closed_x = -0.5 if obj.name == 'Door_Left' else 0.5
    for face in bm.faces:
        if face.material_index == 0:
            for loop in face.loops:
                loop[uv].uv = ((loop.vert.co.x + closed_x + 1) * 16 / 192,
                               1 - (2 - loop.vert.co.z) * 16 / 160)
        face.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    mesh.calc_loop_triangles()
    return mesh


def optimize_leaves(objects):
    result = []
    for obj in objects:
        before = obj.data
        before.calc_loop_triangles()
        mesh = optimized_mesh(obj)
        result.append({'object': obj.name, 'before': len(before.loop_triangles),
                       'after': len(mesh.loop_triangles)})
        obj.data = mesh
        glass = next(m for m in mesh.materials if m.name == 'Maintenance_Translucent_Inserts')
        glass.use_backface_culling = False
        obj['description'] = 'Independent sliding leaf; recessed double-sided translucent inserts; original pixel atlas'
    return result
