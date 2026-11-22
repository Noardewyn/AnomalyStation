"""Continue the existing exterior edge materials across the mating surfaces."""
import bpy
import bmesh


def apply_seam_finish(objects, atlas=None):
    changed = {}
    for obj in objects:
        mesh = obj.data
        steel = mesh.materials.find('Maintenance_Dark_Steel_Edges')
        yellow = mesh.materials.find('Maintenance_Ochre_Edges')
        assert steel >= 0 and yellow >= 0
        closed_x = -0.5 if obj.name == 'Door_Left' else 0.5
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bm.normal_update()

        def is_seam(face):
            return (abs(face.normal.y) < 0.01
                    and 0.0001 < face.calc_center_median().z < 1.9999
                    and all(-0.0001 <= v.co.x + closed_x <= 0.1251 for v in face.verts))

        # Split only the meeting faces at stripe boundaries; shared edges remain welded.
        for row in (14, 17, 18, 21):
            faces = [f for f in bm.faces if is_seam(f)]
            edges = {e for f in faces for e in f.edges}
            verts = {v for e in edges for v in e.verts}
            bmesh.ops.bisect_plane(bm, geom=faces + list(edges) + list(verts),
                                  dist=0.000001, plane_co=(0,0,2-row/16),
                                  plane_no=(0,0,1), clear_inner=False, clear_outer=False)
            bm.normal_update()
        count = 0
        for face in bm.faces:
            if is_seam(face):
                row = (2 - face.calc_center_median().z) * 16
                face.material_index = yellow if (14 <= row < 17 or 18 <= row < 21) else steel
                count += 1
        bm.to_mesh(mesh)
        bm.free()
        old_slot = mesh.materials.find('Maintenance_Black_Seam_With_Yellow_Bands')
        if old_slot >= 0:
            mesh.materials.pop(index=old_slot)
        mesh.update()
        changed[obj.name] = count
    # Remove only the obsolete generated strip, never the original atlas.
    old = bpy.data.materials.get('Maintenance_Black_Seam_With_Yellow_Bands')
    if old and old.users == 0:
        bpy.data.materials.remove(old)
    old_image = bpy.data.images.get('Maintenance_Seam_Strip')
    if old_image and old_image.users == 0:
        bpy.data.images.remove(old_image)
    return changed
