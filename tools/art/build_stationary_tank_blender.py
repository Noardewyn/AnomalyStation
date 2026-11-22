"""Builds tg's stationary gas tank as 3D modules in Blender, drawn with tg's own pixels (run in Blender, via the Blender MCP:
exec(open(path).read(), {'__file__': path})).

tg's tank (code/modules/atmospherics/machinery/components/tank.dm) is the GAGS sheet icons/obj/pipes_n_cables/stationary_canisters.dmi (canister-N, tg's
smoothing junction), white (greyscale_colors "#ffffff"), with the window overlays window-bg and window-fg of stationary_canisters_misc.dmi. Its sprite is
a three-quarter view: rows 4-10 of a tile are the tank's top, rows 11-26 its front, rows 27-29 the legs. 32 px = 2 m, so a pixel is 1/16 m.

Each module is a box whose every face shows tg's pixels one to one:
- an exterior side shows rows 11-29 of the tile tg draws for a tank whose left and right neighbours are joined as this side's are (canister-0, -4, -8 or
  -12: from the viewer's side, its left and right), so a run of tanks reads as tg's run from any side; the legs are the sprite's own pixels, the rest
  of those rows transparent (cut by the alpha);
- the top shows rows 0 (4 on a free north side) to 10 of the tile of the module's own junction, stretched over its depth;
- the window overlays lie on each exterior side as tg draws them on the tile.
A joined side has no face: the body meets its neighbour's. The modules follow the client's smooth4 rule unturned (docs/ai/specs/models.md): an end joins
east, a straight run east and west, a corner north and east, a tee north, east and west, the centre all four.

Blender: Z up, front -Y; the GLB is Y up, front +Z (Godot's). Writes godot/assets/models/station_props/stationary_tanks/stationary_tank_<module>.glb
and stationary_tanks.blend.
"""
import json
import os

import bmesh
import bpy

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ICONS = os.path.join(REPO, 'godot', 'assets', 'tg', 'icons', 'obj', 'pipes_n_cables')
OUT = os.path.join(REPO, 'godot', 'assets', 'models', 'station_props', 'stationary_tanks')

PX = 2.0 / 32
INSET = 1.0 - 2 * PX  # a free side stands 2 px inside the tile's edge, as the sprite's body
FRONT_TOP, FRONT_BOTTOM, BODY_BOTTOM = 11, 30, 27  # sprite rows: the front starts at 11, the legs end at 29, the body ends at 26
MODULES = {'single': '', 'end': 'E', 'straight': 'EW', 'corner': 'NE', 'tee': 'NEW', 'center': 'NESW'}
BITS = {'N': 1, 'S': 2, 'E': 4, 'W': 8}  # tg's junction bits


def tiles(name):
    """The pixel origin of each state's first frame in a converted sheet."""
    meta = json.load(open(os.path.join(ICONS, name + '.json')))
    cols, w, h = meta['sheetWidth'] // meta['width'], meta['width'], meta['height']
    out, index = {}, 0
    for s in meta['states']:
        frames = s.get('dirs', 1) * max(1, len(s.get('delays') or [1]))
        out[s['name']] = (index % cols * w, index // cols * h)
        index += frames
    return out, (meta['sheetWidth'], meta['sheetHeight'])


MAIN, MAIN_SIZE = tiles('stationary_canisters')
MISC, MISC_SIZE = tiles('stationary_canisters_misc')


def y_of(row):
    """The height of a sprite row's top edge: row 30 (below the legs) is the floor."""
    return (FRONT_BOTTOM - row) * PX


def g2b(p):
    """Godot (x, y up, z south) to Blender (x, y north, z up)."""
    return (p[0], -p[2], p[1])


def uv(size, x, y):
    return (x / size[0], 1.0 - y / size[1])


def material(name, image_name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    tex = nodes.new('ShaderNodeTexImage')
    path = os.path.join(ICONS, image_name + '.png')
    image = bpy.data.images.get(image_name + '.png') or bpy.data.images.load(path)
    tex.image = image
    tex.interpolation = 'Closest'
    links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Roughness'].default_value = 0.9
    mat.blend_method = 'CLIP'
    mat.alpha_threshold = 0.5
    return mat


def build(name, joined):
    bm = bmesh.new()
    layer = bm.loops.layers.uv.new('UVMap')

    def quad(points, uvs, mat, outward):
        verts = [bm.verts.new(g2b(p)) for p in points]
        face = bm.faces.new(verts)
        face.material_index = mat
        face.normal_update()
        want = g2b(outward)
        if face.normal.dot(want) < 0:
            face.normal_flip()
            uvs = list(reversed(uvs))
            # normal_flip reverses the loop order: keep each uv on its vertex.
        for loop, (u, v) in zip(face.loops, uvs if face.normal.dot(want) >= 0 else uvs):
            loop[layer].uv = (u, v)

    def face(points, uvs, mat, outward):
        verts = [bm.verts.new(g2b(p)) for p in points]
        f = bm.faces.new(verts)
        f.material_index = mat
        for loop, coord in zip(f.loops, uvs):
            loop[layer].uv = coord
        f.normal_update()
        if f.normal.dot(g2b(outward)) < 0:
            f.normal_flip()  # bmesh keeps each loop's uv with its vertex

    ext = {s: (1.0 if s in joined else INSET) for s in 'NESW'}
    # The sides: (side, outward, a point on the side for viewer coordinate u (-1 left .. 1 right) and height y, the left and right sides).
    sides = {
        'S': ((0, 0, 1), lambda u, y: (u, y, ext['S']), 'W', 'E'),
        'N': ((0, 0, -1), lambda u, y: (-u, y, -ext['N']), 'E', 'W'),
        'E': ((1, 0, 0), lambda u, y: (ext['E'], y, -u), 'S', 'N'),
        'W': ((-1, 0, 0), lambda u, y: (-ext['W'], y, u), 'N', 'S'),
    }
    for side, (outward, at, left, right) in sides.items():
        if side in joined:
            continue
        ul = -1.0 if left in joined else -INSET
        ur = 1.0 if right in joined else INSET
        state = 'canister-%d' % ((8 if left in joined else 0) | (4 if right in joined else 0))
        tx, ty = MAIN[state]
        cl, cr = (ul + 1) / PX, (ur + 1) / PX
        pts = [at(ul, y_of(FRONT_BOTTOM)), at(ur, y_of(FRONT_BOTTOM)), at(ur, y_of(FRONT_TOP)), at(ul, y_of(FRONT_TOP))]
        uvs = [uv(MAIN_SIZE, tx + cl, ty + FRONT_BOTTOM), uv(MAIN_SIZE, tx + cr, ty + FRONT_BOTTOM), uv(MAIN_SIZE, tx + cr, ty + FRONT_TOP), uv(MAIN_SIZE, tx + cl, ty + FRONT_TOP)]
        face(pts, uvs, 0, outward)
        # tg's window overlays, on the tile as tg draws them (cols 6-24, rows 13-18), a hair off the face: the background, then the frame.
        for k, (state_name, lift) in enumerate((('window-bg', 0.002), ('window-fg', 0.004))):
            mx, my = MISC[state_name]
            c0, c1, r0, r1 = 6, 25, 13, 19
            off = [o * lift for o in outward]
            wp = [at(c0 * PX - 1, y_of(r1)), at(c1 * PX - 1, y_of(r1)), at(c1 * PX - 1, y_of(r0)), at(c0 * PX - 1, y_of(r0))]
            wp = [(p[0] + off[0], p[1] + off[1], p[2] + off[2]) for p in wp]
            wuv = [uv(MISC_SIZE, mx + c0, my + r1), uv(MISC_SIZE, mx + c1, my + r1), uv(MISC_SIZE, mx + c1, my + r0), uv(MISC_SIZE, mx + c0, my + r0)]
            face(wp, wuv, 1, outward)
    # The top: rows (0 or 4) to 10 of the module's own junction, north to south.
    junction = sum(BITS[s] for s in joined)
    tx, ty = MAIN['canister-%d' % junction]
    xw, xe, zn, zs = -ext['W'], ext['E'], -ext['N'], ext['S']
    row_n = 0 if 'N' in joined else 4
    top_y = y_of(FRONT_TOP)
    col = lambda x: (x + 1) / PX
    pts = [(xw, top_y, zs), (xe, top_y, zs), (xe, top_y, zn), (xw, top_y, zn)]
    uvs = [uv(MAIN_SIZE, tx + col(xw), ty + FRONT_TOP), uv(MAIN_SIZE, tx + col(xe), ty + FRONT_TOP), uv(MAIN_SIZE, tx + col(xe), ty + row_n), uv(MAIN_SIZE, tx + col(xw), ty + row_n)]
    face(pts, uvs, 0, (0, 1, 0))
    # The bottom: the body's last row (26), under it.
    bottom_y = y_of(BODY_BOTTOM)
    pts = [(xw, bottom_y, zn), (xe, bottom_y, zn), (xe, bottom_y, zs), (xw, bottom_y, zs)]
    tx0, ty0 = MAIN['canister-0']
    uvs = [uv(MAIN_SIZE, tx0 + 3, ty0 + 26.2), uv(MAIN_SIZE, tx0 + 28, ty0 + 26.2), uv(MAIN_SIZE, tx0 + 28, ty0 + 26.8), uv(MAIN_SIZE, tx0 + 3, ty0 + 26.8)]
    face(pts, uvs, 0, (0, -1, 0))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(MAT_MAIN)
    mesh.materials.append(MAT_MISC)
    obj = bpy.data.objects.new(name, mesh)
    return obj


MAT_MAIN = material('Atlas_stationary_canisters', 'stationary_canisters')
MAT_MISC = material('Atlas_stationary_canisters_misc', 'stationary_canisters_misc')


def main():
    os.makedirs(OUT, exist_ok=True)
    scene = bpy.data.scenes.get('Stationary_Tanks_TG') or bpy.data.scenes.new('Stationary_Tanks_TG')
    bpy.context.window.scene = scene
    for o in list(scene.collection.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in [m for m in bpy.data.meshes if m.users == 0 and m.name.startswith('tg_stationary_tank_')]:
        bpy.data.meshes.remove(m)
    built = []
    for i, (module, joined) in enumerate(MODULES.items()):
        obj = build('tg_stationary_tank_' + module, set(joined))
        obj['module'] = module
        scene.collection.objects.link(obj)
        obj.location = (i * 2.5, 0, 0)
        built.append(obj)
    for obj in built:
        bpy.ops.object.select_all(action='DESELECT')
        location = obj.location.copy()
        obj.location = (0, 0, 0)
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, 'stationary_tank_%s.glb' % obj['module']), export_format='GLB', use_selection=True, export_yup=True, use_active_scene=True,
                                  export_apply=True, export_materials='EXPORT', export_image_format='AUTO', export_animations=False)
        obj.location = location
    # Only this scene and what it uses (the session may hold other props' scenes).
    bpy.data.libraries.write(os.path.join(OUT, 'stationary_tanks.blend'), {scene}, fake_user=True, compress=True)
    print('built', [o.name for o in built])


main()
