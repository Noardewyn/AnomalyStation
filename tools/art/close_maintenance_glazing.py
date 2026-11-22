"""Close glass volumes and line the window tunnels without adding objects."""
from collections import Counter
import bpy
import bmesh


def close_glazing(objects):
    report = []
    for obj in objects:
        if obj.get('glazing_closed_volume'):
            continue
        source = obj.data
        glass = next(i for i, m in enumerate(source.materials)
                     if m.name == 'Maintenance_Translucent_Inserts')
        steel = next(i for i, m in enumerate(source.materials)
                     if m.name == 'Maintenance_Dark_Steel_Edges')
        vertices, faces, indices, face_uvs, lookup = [], [], [], [], {}
        uv = source.uv_layers.active.data

        def vertex(index, is_glass):
            key = (index, is_glass)
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(tuple(source.vertices[index].co))
            return lookup[key]

        front_edges = Counter()
        back = {}
        for face in source.polygons:
            is_glass = face.material_index == glass
            faces.append([vertex(v, is_glass) for v in face.vertices])
            indices.append(face.material_index)
            face_uvs.append([tuple(uv[i].uv) for i in face.loop_indices])
            if is_glass and face.normal.y < -0.9:
                ids = list(face.vertices)
                for a, b in zip(ids, ids[1:] + ids[:1]):
                    front_edges[tuple(sorted((a, b)))] += 1
            elif is_glass and face.normal.y > 0.9:
                for index in face.vertices:
                    co = source.vertices[index].co
                    back[(round(co.x,6), round(co.z,6))] = index
        contour_count = 0
        for (a, b), count in front_edges.items():
            if count != 1:
                continue
            ca, cb = source.vertices[a].co, source.vertices[b].co
            aa = back[(round(ca.x,6), round(ca.z,6))]
            bb = back[(round(cb.x,6), round(cb.z,6))]
            # Independent closed shells, sharing the contact position only.
            # The opaque liner hides the metal cavity through the transparent glass.
            for is_glass, material in ((False, steel), (True, glass)):
                faces.append([vertex(v, is_glass) for v in (a,b,bb,aa)])
                indices.append(material)
                face_uvs.append([(0,0)] * 4)
            contour_count += 1
        mesh = bpy.data.meshes.new(source.name + '_Closed_Glass')
        mesh.from_pydata(vertices, [], faces)
        for mat in source.materials:
            mesh.materials.append(mat)
        uv_layer = mesh.uv_layers.new(name='Original_Atlas_UV')
        for face, mat, values in zip(mesh.polygons, indices, face_uvs):
            face.material_index = mat
            for loop, value in zip(face.loop_indices, values):
                uv_layer.data[loop].uv = value
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.normal_update()
        bmesh.ops.dissolve_limit(bm, angle_limit=0.001, use_dissolve_boundaries=False,
                               verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
        assert all(e.is_manifold for e in bm.edges)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        mesh.calc_loop_triangles()
        obj.data = mesh
        obj['glazing_closed_volume'] = True
        report.append({'object':obj.name, 'closed_contour_segments':contour_count,
                       'triangles':len(mesh.loop_triangles)})
    return report
