# Builds tg's supermatter crystal (supermatter.dm, icon_state "sm") as a low-poly flat-colour model: the grated base and the yellow crystal
# cluster, plus the nodes of tg's overlays: "sm-psy" (a psychologist near), "causality_field" (the final countdown, sm_delam.overlays), "holiday_lights"
# and "santa_hat" (the festive season). The game draws the crystal and the field by shaders of their own, which play tg's frames ("sm", "sm-glow",
# "causality_field") from the catalog (models.json "shader", "pulse").
# Run inside Blender through the live MCP: exec(open(path).read(), {'__file__': path}).
# Units: "W" = the base's width, measured from the owner's four reference renders; S metres per W.
# Axes: Blender front = -Y, up = +Z, origin on the floor at the centre.
import bpy, bmesh, math, os
from pathlib import Path
from mathutils import Matrix, Vector

# The tip stands at 1.94 m, under the 2 m default wall (fit "wall"): the reference's crystal is 1.145 W above a 0.165 W base, so the base is
# 1.48 m, 0.74 of tg's tile where tg's sprite fills 0.94 of it (a compromise: the reference's proportions over the sprite's footprint).
S = 1.94 / 1.31
OUT = str(Path(__file__).resolve().parents[2] / 'godot/assets/models/machines/supermatter')
NAME = "Supermatter"

COLORS = {  # sRGB hex sampled from the references (the crystal's mid tone, the frame, the grating), tg's sprites for the other states; emission, alpha
    "Frame": ("39393a", 0), "Plate": ("303032", 0), "Deep": ("151515", 0), "Slat": ("2c2c2e", 0), "Foot": ("3c3c3e", 0),
    # the crystal: the game draws it see-through by its own shader (the owner, 2026-10-21; models.json "shader": "crystal"), which also plays sm-glow's
    # colours as factors on it (the catalog's "pulse"), so the file has no glow node
    "Crystal": ("d7a02a", 0),
    # sm-psy: tg lays (169,73,43) over the crystal at psy_coeff's alpha; drawn half way (the renderer has no overlay alpha)
    "CrystalPsy": ("c0742a", 0),
    # causality_field: the sprite's sides and its lighter top, see-through (its frames' alpha 0.29 .. 0.69)
    "Emission_Field": ("2c6cb5", 1.0, 0.45), "Emission_FieldTop": ("349bd8", 1.0, 0.45),
    # holiday_lights: the string's wire and the bulbs' colours of tg's sprite
    "Wire": ("19322a", 0), "Emission_Pink": ("f02d7f", 2.0), "Emission_Cyan": ("1fc7fe", 2.0), "Emission_Green": ("23fb92", 2.0),
    "Emission_Yellow": ("fee45e", 2.0),
    # santa_hat
    "HatRed": ("b00c0d", 0), "HatWhite": ("d3d3d3", 0),
}
NAMES = list(COLORS)
M_ = {n: i for i, n in enumerate(NAMES)}


def lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, hexrgb, emit, alpha=1.0):
    full = NAME + "_Flat_" + name  # "Flat": drawn in its authored colour, shaded by its normals (docs/ai/specs/models.md)
    m = bpy.data.materials.get(full) or bpy.data.materials.new(full)
    rgb = [lin(int(hexrgb[i:i + 2], 16)) for i in (0, 2, 4)]
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Alpha"].default_value = alpha
    m.surface_render_method = "BLENDED" if alpha < 1.0 else "DITHERED"  # glTF BLEND: the game's see-through stipple
    p.inputs["Roughness"].default_value = 0.6
    p.inputs["Metallic"].default_value = 0.0
    p.inputs["Emission Color"].default_value = (*rgb, 1)
    p.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1)
    return m


def zring(n, r, z, phase=0.0):  # around Z, angle from -Y toward +X
    return [(r * math.sin(phase + 2 * math.pi * k / n), -r * math.cos(phase + 2 * math.pi * k / n), z) for k in range(n)]


def loft(bm, rings, mats, M=Matrix(), cap0=None, cap1=None, apex=None, apex_mat=None):
    vs = [[bm.verts.new((M @ Vector(p)) * S) for p in r] for r in rings]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = vs[i], vs[i + 1]
        mi = mats[i] if isinstance(mats, (list, tuple)) else mats
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])).material_index = M_[mi]
    if cap0:
        bm.faces.new(vs[0][::-1]).material_index = M_[cap0]
    if cap1:
        bm.faces.new(vs[-1]).material_index = M_[cap1]
    if apex is not None:
        t = bm.verts.new((M @ Vector(apex)) * S)
        last = vs[-1]
        for k in range(n):
            bm.faces.new((last[k], last[(k + 1) % n], t)).material_index = M_[apex_mat or mats]


def box(bm, x0, x1, y0, y1, z0, z1, mat, M=Matrix()):
    r = [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0)]
    loft(bm, [[(x, y, z0) for x, y, _ in r], [(x, y, z1) for x, y, _ in r]], mat, M, mat, mat)


def octa(bm, c, r, mat):  # a bulb
    x, y, z = c
    vs = [bm.verts.new(Vector(p) * S) for p in ((x + r, y, z), (x, y + r, z), (x - r, y, z), (x, y - r, z), (x, y, z + r), (x, y, z - r))]
    for k in range(4):
        bm.faces.new((vs[k], vs[(k + 1) % 4], vs[4])).material_index = M_[mat]
        bm.faces.new((vs[(k + 1) % 4], vs[k], vs[5])).material_index = M_[mat]


def finish(bm, name, coll, mats):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


# ---------- measurements (W) ----------
HB = 0.165          # base height (the side views: 70 of 427 px)
FOOT = 0.005        # the four pads under it (the bottom view: 0.1 W squares 0.095 W in from the edges; the side views hide them)
RIM = 0.074         # the top frame's flat (the top view: 30 of 406 px)
SLOPE = 0.05        # its inner chamfer down into the grating
DEPTH = 0.045       # the grating's floor under the frame's top
TOPZ = HB - 0.03    # where the crystals stand (on the slats)

# the crystal cluster, from the top view's blobs (u right, v back) and the side views' heights: (u, v, radius, height, lean deg, twist deg, sides)
MAIN = (0.0, 0.0, 0.21, 1.145 + HB - TOPZ)
CRYSTALS = [
    (-0.21, 0.21, 0.13, 0.62, 20, 10, 6),    # back left, leaning out (the left view's leftmost)
    (0.03, 0.27, 0.12, 0.46, 15, 25, 6),     # back
    (0.20, 0.20, 0.115, 0.74, 9, 5, 6),      # back right, the tall one behind the main (the right view)
    (-0.27, -0.02, 0.14, 0.46, 18, 40, 6),   # left
    (0.28, 0.03, 0.13, 0.54, 16, 15, 6),     # right
    (0.32, -0.18, 0.085, 0.27, 24, 0, 6),    # right front, low
    (-0.20, -0.22, 0.13, 0.58, 19, 20, 6),   # front left (the right view's leftmost)
    (-0.04, -0.30, 0.115, 0.36, 20, 35, 6),  # front
    (0.17, -0.23, 0.12, 0.75, 9, 30, 6),     # front right, the tall one behind the main (the left view)
    (-0.36, 0.21, 0.075, 0.19, 28, 0, 6),    # small ones round the foot
    (0.09, -0.39, 0.07, 0.17, 28, 0, 6),
    (0.38, 0.18, 0.07, 0.18, 28, 0, 6),
]


def crystal(bm, u, v, r, h, lean, twist, n, mat):
    """A hexagonal column, widest at 0.42 of its height, its tip from 0.66 (the side views), leaning away from the centre."""
    d = math.atan2(u, -v) if (u or v) else 0.0  # the direction away from the centre, as zring's angle
    M = Matrix.Translation((u, v, TOPZ)) @ Matrix.Rotation(d, 4, "Z") @ Matrix.Rotation(math.radians(lean), 4, "X") \
        @ Matrix.Rotation(math.radians(twist), 4, "Z")
    # leaning about X tips +Z toward -Y; turned by d, -Y points away from the centre
    loft(bm, [zring(n, r * 0.8, -0.06), zring(n, r, 0.42 * h), zring(n, r * 0.94, 0.66 * h)], mat, M, apex=(0, 0, h), apex_mat=mat)


def build_crystals(bm, mat):
    u, v, r, h = MAIN  # an octagonal column, its shoulder 0.89 W over the base (the side views)
    loft(bm, [zring(8, r * 0.9, -0.06 + TOPZ), zring(8, r, 0.35 + TOPZ), zring(8, r * 0.95, HB + 0.89)], mat, apex=(0, 0, h + TOPZ), apex_mat=mat)
    for c in CRYSTALS:
        crystal(bm, *c, mat)


def build(keep=True):
    mats = [material(n, *COLORS[n]) for n in NAMES]
    scn = bpy.data.scenes.get(NAME)
    if scn:
        for o in list(scn.collection.all_objects):
            bpy.data.objects.remove(o)
    else:
        scn = bpy.data.scenes.new(NAME)
    for me in [m for m in bpy.data.meshes if m.name.startswith(NAME + "_") and not m.users]:
        bpy.data.meshes.remove(me)
    coll = scn.collection
    bpy.context.window.scene = scn  # no booleans here, but the export reads the window's scene
    fin = lambda bm, nm: finish(bm, nm, coll, mats)
    q = lambda half, z: zring(4, half * math.sqrt(2), z, math.pi / 4)  # an axis-aligned square ring

    # the base: chamfered bottom edge, sides, chamfered top edge, the frame's flat, its inner chamfer down to the grating's floor
    bm = bmesh.new()
    loft(bm, [q(0.47, FOOT), q(0.5, FOOT + 0.025), q(0.5, HB - 0.015), q(0.5 - 0.015, HB), q(0.5 - RIM, HB),
              q(0.5 - RIM - SLOPE, HB - DEPTH)], ["Plate", "Frame", "Frame", "Frame", "Frame"], cap0="Plate", cap1="Deep")
    # the grating's slats, running left to right as in the top view
    half = 0.5 - RIM - SLOPE
    for k in range(7):
        y = -0.3 + 0.1 * k
        box(bm, -half, half, y - 0.022, y + 0.022, HB - DEPTH, TOPZ, "Slat")
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (0.5 - 0.095 - 0.05), sy * (0.5 - 0.095 - 0.05)
            box(bm, x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0, FOOT, "Foot")
    base = fin(bm, NAME + "_Base")

    looks = {}
    for nm, mat in (("Crystal", "Crystal"), ("CrystalPsy", "CrystalPsy")):
        bm = bmesh.new()
        build_crystals(bm, mat)
        looks[nm] = fin(bm, NAME + "_" + nm)

    # causality_field: a see-through box round the whole crystal
    bm = bmesh.new()
    F = 0.49
    loft(bm, [q(F, 0.002), q(F, 1.33)], "Emission_Field", cap1="Emission_FieldTop")
    field = fin(bm, NAME + "_CausalityField")

    # holiday_lights: a string round the cluster's foot, its bulbs in tg's four colours. tg's frames alternate 0.1 s with 36 lit pixels and 0.9 s with 90
    # (holiday_lights_e): half the bulbs stay lit (_HolidayLights, with the wire), the other half go out for 0.1 s a second (_HolidayLightsBlink, "blink")
    R, ZL = 0.44, HB + 0.03
    ring = [(R * math.sin(a), -R * math.cos(a), ZL + 0.025 * math.cos(3 * a)) for a in (2 * math.pi * k / 12 for k in range(12))]
    bulbs = ["Emission_Pink", "Emission_Cyan", "Emission_Green", "Emission_Yellow"]
    halves = []
    for half in (0, 1):
        bm = bmesh.new()
        if half == 0:
            loft(bm, [ring, [(x * 1.03, y * 1.03, z + 0.008) for x, y, z in ring]], "Wire")
        for k, (x, y, z) in enumerate(ring):
            if k % 2 == half:
                octa(bm, (x * 1.04, y * 1.04, z - 0.012), 0.024, bulbs[(k + k // 2) % 4])  # all four colours in each half
        halves.append(fin(bm, NAME + ("_HolidayLights", "_HolidayLightsBlink")[half]))
    lights, lights_blink = halves

    # santa_hat: on the main crystal's tip, tipped to the left as tg's sprite
    tip = MAIN[3] + TOPZ
    bm = bmesh.new()
    H = Matrix.Translation((0, 0, tip - 0.12)) @ Matrix.Rotation(math.radians(18), 4, "Y")  # its top stays under the 2 m ceiling
    loft(bm, [zring(8, 0.1, 0.0), zring(8, 0.085, 0.05)], "HatRed", H, apex=(0.05, 0, 0.15), apex_mat="HatRed")
    loft(bm, [zring(8, 0.11, -0.012), zring(8, 0.11, 0.022)], "HatWhite", H, cap0="HatWhite", cap1="HatWhite")
    octa(bm, tuple(H @ Vector((0.06, 0, 0.15))), 0.028, "HatWhite")
    hat = fin(bm, NAME + "_SantaHat")

    parts = [base, *looks.values(), field, lights, lights_blink, hat]
    for ob in parts:  # only the materials each mesh uses
        used = {p.material_index for p in ob.data.polygons}
        for i in reversed(range(len(ob.data.materials))):
            if i not in used:
                ob.data.materials.pop(index=i)
        ob.data.name = ob.name

    os.makedirs(OUT, exist_ok=True)
    vl = scn.view_layers[0]
    for o in scn.objects:
        o.select_set(o in parts, view_layer=vl)
    with bpy.context.temp_override(scene=scn, view_layer=vl):
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "supermatter.glb"), export_format="GLB", use_selection=True,
                                  use_active_scene=True, export_yup=True, export_lights=False, export_cameras=False)
    bpy.data.libraries.write(os.path.join(OUT, "supermatter.blend"), {scn}, compress=True)
    vc = lambda o: len(o.data.vertices)
    stats = {"verts": {o.name: vc(o) for o in parts},
             # the most drawn at once: the base, one crystal look, the field, the lights and the hat
             "verts_drawn_max": vc(base) + vc(looks["Crystal"]) + vc(field) + vc(lights) + vc(lights_blink) + vc(hat),
             "top_m": max((o.matrix_world @ v.co).z for o in parts if o is not field for v in o.data.vertices),
             "extent_m": [max(abs(v.co.x) for v in looks["Crystal"].data.vertices), max(abs(v.co.y) for v in looks["Crystal"].data.vertices)]}
    assert stats["verts_drawn_max"] <= 800, stats
    if not keep:
        for o in parts:
            bpy.data.objects.remove(o)
    return stats


result = build()
