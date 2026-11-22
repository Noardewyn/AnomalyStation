# Builds tg's portable canister (canister.dm) as a low-poly flat-colour model, one GLB per GAGS pattern
# (canister, canister/stripe, canister/double_stripe, canister/hazard). The shell's colour is the GAGS key's first colour (Canister_Flat_Gags1),
# the stripes its second (Canister_Flat_Gags2); the renderer takes them from the thing's key (docs/ai/specs/models.md).
# Run inside Blender through the live MCP: exec(open(path).read(), {'__file__': path}).
# Units: "u" = pixels of the owner's front reference (1143 u = full height), scaled by S.
# Axes: Blender front = -Y, up = +Z, origin on the floor at the centre.
import bpy, bmesh, math, os
from pathlib import Path
from mathutils import Matrix, Vector

S = 0.85 / 1143.0
OUT = str(Path(__file__).resolve().parents[2] / 'godot/assets/models/station_props/canister')

# preview colours of each pattern (a tg canister of it); the game uses the thing's own GAGS colours
PATTERNS = {
    "default": ("c6c0b5", "c6c0b5"),        # air
    "stripe": ("2786e5", "e8fefe"),         # oxygen
    "double_stripe": ("e9ff5c", "f4fce8"),  # nitrogen
    "hazard": ("f62800", "000000"),         # plasma
}
# body rings (u from the floor) and each band's look: 1 shell, 2 stripe
BANDS = {
    "default": ([143, 669], [1]),
    "stripe": ([143, 265, 385, 669], [1, 2, 1]),
    "double_stripe": ([143, 290, 520, 669], [2, 1, 2]),
    # two thick solid stripes as double_stripe's, not tg's diagonal hazard stripes (the owner, 2026-10-20)
    "hazard": ([143, 290, 520, 669], [2, 1, 2]),
}

COLORS = {  # sRGB hex sampled from the reference renders (the cell's lights and the shield from tg's cell_hatch and shielding), emission strength, alpha
    "Gags1": ("c6c0b5", 0), "Gags2": ("c6c0b5", 0), "Dark": ("5a5653", 0), "Deep": ("3a3836", 0),
    "Cap": ("958d87", 0), "Panel": ("7c7772", 0), "Black": ("121110", 0),
    # "Tint_": blended by the renderer toward a colour of the thing's (the held tank's sprite, the gases in the window)
    "Tint_Tank": ("8a9096", 0), "Tint_Window": ("c8d0d0", 0),
    "Cell": ("6f736f", 0), "Emission_CellRed": ("ff3300", 2.0), "Emission_CellGreen": ("00b401", 2.0),
    "Emission_Shield": ("14ffed", 2.5), "Emission_ShieldGlass": ("14ffed", 1.5, 0.22),
    "Emission_LED": ("22ff2e", 3.0), "Emission_PortRed": ("ff261f", 2.0), "Emission_Red": ("b01210", 1.5), "Emission_Amber": ("7a3a20", 1.0),
    "Emission_Olive": ("4a5a22", 1.0), "Emission_Green": ("22ff2e", 3.0),
}
NAMES = list(COLORS)
M_ = {n: i for i, n in enumerate(NAMES)}


def lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, hexrgb, emit, alpha=1.0):
    full = "Canister_Flat_" + name  # "Flat": drawn in its authored colour (docs/ai/specs/models.md)
    m = bpy.data.materials.get(full) or bpy.data.materials.new(full)
    rgb = [lin(int(hexrgb[i:i + 2], 16)) for i in (0, 2, 4)]
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        m.surface_render_method = "BLENDED"  # glTF BLEND: the game's see-through stipple
    p.inputs["Roughness"].default_value = 0.75
    p.inputs["Metallic"].default_value = 0.0
    p.inputs["Emission Color"].default_value = (*rgb, 1)
    p.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1)
    return m


# ---------- ring / loft helpers (all in u) ----------
def zring(n, r, z, phase=0.0):  # around Z, angle from -Y toward +X
    return [(r * math.sin(phase + 2 * math.pi * k / n), -r * math.cos(phase + 2 * math.pi * k / n), z) for k in range(n)]


def yring(n, r, y, phase=0.0, cx=0.0, cz=0.0):  # in the XZ plane at depth y (outward = -Y)
    return [(cx + r * math.sin(phase + 2 * math.pi * k / n), y, cz + r * math.cos(phase + 2 * math.pi * k / n)) for k in range(n)]


def ypoly(pts, y):
    return [(x, y, z) for x, z in pts]


def chamf(w, h, c, cz=0.0, cx=0.0):
    a, b = w / 2, h / 2
    p = [(-a + c, -b), (a - c, -b), (a, -b + c), (a, b - c), (a - c, b), (-a + c, b), (-a, b - c), (-a, -b + c)]
    return [(cx + x, cz + z) for x, z in p]


def loft(bm, rings, mats, M=Matrix(), cap0=None, cap1=None, closed=False):
    vs = [[bm.verts.new((M @ Vector(p)) * S) for p in r] for r in rings]
    n = len(rings[0])
    bands = len(rings) - (0 if closed else 1)
    for i in range(bands):
        a, b = vs[i], vs[(i + 1) % len(vs)]
        mi = mats[i] if isinstance(mats, (list, tuple)) else mats
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])).material_index = M_[mi]
    if cap0:
        bm.faces.new(vs[0]).material_index = M_[cap0]
    if cap1:
        bm.faces.new(vs[-1][::-1]).material_index = M_[cap1]


def box(bm, x0, x1, y0, y1, z0, z1, mat, M=Matrix()):
    r = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    loft(bm, [ypoly(r, y0), ypoly(r, y1)], mat, M, mat, mat)


def rotz(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


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


def apply_difference(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation, mod.solver, mod.object = "DIFFERENCE", "EXACT", cutter
    mod.material_mode = "TRANSFER"
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(target.evaluated_get(dg))
    old = target.data
    target.modifiers.clear()
    target.data = me
    bpy.data.meshes.remove(old)
    bpy.data.objects.remove(cutter)


P16 = math.pi / 16  # 16-gon with a flat face on the front
DOOR_A, PORT_A, VALVE_A = -45.0, 40.0, 105.0
DOOR_CZ = 395.5


def build(pattern, keep=False):
    c1, c2 = PATTERNS[pattern]
    colors = dict(COLORS, Gags1=(c1, 0), Gags2=(c2, 0))
    mats = [material(n, *colors[n]) for n in NAMES]
    name = "Canister_" + pattern
    scn = bpy.data.scenes.get(name)
    if scn:
        for o in list(scn.collection.all_objects):
            bpy.data.objects.remove(o)
    else:
        scn = bpy.data.scenes.new(name)
    for me in [m for m in bpy.data.meshes if m.name.startswith(("Canister_", "tmp_")) and not m.users]:
        bpy.data.meshes.remove(me)
    coll = scn.collection
    bpy.context.window.scene = scn  # the booleans are evaluated in the window's scene: it must be this one
    fin = lambda bm, nm: finish(bm, nm, coll, mats)

    # base with four corner notches
    bm = bmesh.new()
    loft(bm, [zring(16, 286, 0, P16), zring(16, 300, 10, P16), zring(16, 300, 120, P16),
              zring(16, 312, 122, P16), zring(16, 312, 145, P16)], "Dark", cap0="Dark", cap1="Dark")
    base = fin(bm, "tmp_base")
    bm = bmesh.new()
    for a in (45, 135, 225, 315):
        box(bm, -33, 33, -340, -274, -10, 72, "Deep", rotz(a))
    apply_difference(base, fin(bm, "tmp_notch"))

    # body shell in the pattern's bands, with the tank pocket behind the door
    zs, looks = BANDS[pattern]
    bm = bmesh.new()
    loft(bm, [zring(16, 300, z, P16) for z in zs] + [zring(16, 248, 722, P16)],
         ["Gags%d" % l for l in looks] + ["Gags1"], cap0="Gags1", cap1="Gags1")
    shell = fin(bm, "tmp_shell")
    bm = bmesh.new()
    pocket = chamf(72, 271, 12, DOOR_CZ)
    loft(bm, [ypoly(pocket, -340), ypoly(pocket, -190)], "Deep", rotz(DOOR_A), "Deep", "Deep")
    box(bm, -42, 42, -330, -262, 566, 650, "Deep")  # the power cell's bay behind its hatch (tg's cell_hatch: front, upper body)
    apply_difference(shell, fin(bm, "tmp_pocket"))

    # everything else that never moves
    bm = bmesh.new()
    bm.from_mesh(base.data)
    bm.from_mesh(shell.data)
    # head (chamfered box) and top cap
    hd = lambda i: [(-160 + 38 - i, -169 + i), (160 - 38 + i, -169 + i), (160 - i, -169 + 38 - i), (160 - i, 169 - 38 + i),
                    (160 - 38 + i, 169 - i), (-160 + 38 - i, 169 - i), (-160 + i, 169 - 38 + i), (-160 + i, -169 + 38 - i)]
    loft(bm, [[(x, y, 715) for x, y in hd(0)], [(x, y, 980) for x, y in hd(0)], [(x, y, 992) for x, y in hd(10)]],
         "Gags1", cap0="Gags1", cap1="Gags1")
    loft(bm, [zring(12, 148, 985, math.pi / 12), zring(12, 148, 1015, math.pi / 12), zring(12, 140, 1021, math.pi / 12)],
         "Cap", cap0="Cap", cap1="Cap")
    # handle: front U-frame and the top/back bar
    U = [(-258, 1113), (-228, 1143), (228, 1143), (258, 1113), (258, 892), (212, 785), (158, 785), (158, 1070),
         (-158, 1070), (-158, 785), (-212, 785), (-258, 892)]
    loft(bm, [ypoly(U, -62), ypoly(U, 62)], "Dark", cap0="Dark", cap1="Dark")
    L = [(-193, 1070), (-193, 1143), (190, 1143), (210, 1123), (210, 900), (171, 900), (171, 1070)]
    loft(bm, [[(-42, y, z) for y, z in L], [(42, y, z) for y, z in L]], "Dark", cap0="Dark", cap1="Dark")
    # head front: the gas window's ring (its glass is a part of its own), the level bar's dark bed (the port light and the bar's four lights too)
    g = lambda r, y: yring(8, r, y, math.pi / 8, 58, 921)
    loft(bm, [g(46, -165), g(46, -178), g(24, -178), g(24, -172)], "Deep", cap0="Deep")
    box(bm, -82, 82, -172, -164, 771, 825, "Deep")
    # head back bolts
    for x in (-88, 88):
        loft(bm, [yring(6, 14, 165, 0, x, 830), yring(6, 9, 179, 0, x, 830)], "Deep", cap0="Deep", cap1="Deep")
    # tank port frame and its four bolts
    R = rotz(DOOR_A)
    fo, fi = chamf(124, 341, 22, DOOR_CZ), chamf(76, 275, 14, DOOR_CZ)
    loft(bm, [ypoly(fo, -280), ypoly(fo, -304), ypoly(fi, -304), ypoly(fi, -280)], "Deep", R, closed=True)
    for x, z in ((-28, 549.5), (28, 549.5), (-28, 241.5), (28, 241.5)):
        loft(bm, [yring(4, 6, -303, math.pi / 4, x, z), yring(4, 3, -309, math.pi / 4, x, z)], "Dark", R, cap1="Dark")
    # gas port nut on the front-right
    n = lambda r, y: yring(8, r, y, math.pi / 8, 0, 255)
    loft(bm, [n(92, -262), n(92, -312), n(80, -322), n(46, -322), n(46, -304)], ["Dark", "Dark", "Dark", "Deep"],
         rotz(PORT_A), "Dark", "Black")
    # valve boss on the right side
    b = lambda r, y: yring(8, r, y, math.pi / 8, 0, 504)
    loft(bm, [b(30, -270), b(30, -300)], "Dark", rotz(VALVE_A), "Dark", "Dark")
    bpy.data.objects.remove(base)
    bpy.data.objects.remove(shell)
    body = fin(bm, "Canister_Body")

    # tank port door, shut and open (tg's can-open: a tank is held), origin on its rear hinge edge: it opens about local Z
    HINGE = Vector((-37, -298.5, DOOR_CZ))
    bm = bmesh.new()
    dp = chamf(74, 273, 13, DOOR_CZ)
    loft(bm, [ypoly(dp, -295), ypoly(dp, -302)], "Panel", Matrix.Translation(-HINGE), "Panel", "Panel")
    door = fin(bm, "Canister_TankDoor")
    door.location = (rotz(DOOR_A) @ HINGE) * S
    door.rotation_euler = (0, 0, math.radians(DOOR_A))
    door_open = bpy.data.objects.new("Canister_TankDoorOpen", door.data)
    coll.objects.link(door_open)
    door_open.location = door.location
    door_open.rotation_euler = (0, 0, math.radians(DOOR_A - 100))

    # the held tank standing in the pocket
    bm = bmesh.new()
    t = lambda r, h: [(x, y - 250, h) for x, y, _ in zring(8, r, 0, math.pi / 8)]
    loft(bm, [t(28, 272), t(28, 492), t(14, 508), t(14, 524)], ["Tint_Tank", "Tint_Tank", "Dark"], R, "Tint_Tank", "Dark")
    tank = fin(bm, "Canister_HeldTank")

    # the gas window's glass in the head's ring (tg's update_window: the gases inside show through it)
    bm = bmesh.new()
    loft(bm, [g(24, -172.5), g(24, -173.5)], "Tint_Window", cap0="Tint_Window", cap1="Tint_Window")
    window = fin(bm, "Canister_Window")

    # the power cell's hatch, shut and open (tg's cell_hatch, panel_open), origin on its bottom hinge: it opens about local X
    HATCH = Vector((0, -298, 562))
    bm = bmesh.new()
    box(bm, -46, 46, -301, -295, 562, 654, "Dark", Matrix.Translation(-HATCH))
    hatch = fin(bm, "Canister_CellHatch")
    hatch.location = HATCH * S
    hatch_open = bpy.data.objects.new("Canister_CellHatchOpen", hatch.data)
    coll.objects.link(hatch_open)
    hatch_open.location = hatch.location
    hatch_open.rotation_euler = (math.radians(160), 0, 0)  # hangs down from its hinge
    # the cell in its bay, its two lights as tg's cell_hatch shows them
    bm = bmesh.new()
    box(bm, -26, 26, -292, -268, 575, 640, "Cell")
    box(bm, -16, -8, -295, -292, 624, 632, "Emission_CellRed")
    box(bm, 8, 16, -295, -292, 624, 632, "Emission_CellGreen")
    cell = fin(bm, "Canister_Cell")

    # the powered shielding (tg's shielding overlay): a glowing see-through shell and two light bands that flicker
    bm = bmesh.new()
    loft(bm, [zring(12, 345, -5, math.pi / 12), zring(12, 345, 1150, math.pi / 12), zring(12, 210, 1190, math.pi / 12)],
         "Emission_ShieldGlass", cap1="Emission_ShieldGlass")
    shield = fin(bm, "Canister_Shield")
    bm = bmesh.new()
    for z in (380, 860):
        loft(bm, [zring(12, 350, z, math.pi / 12), zring(12, 350, z + 16, math.pi / 12)], "Emission_Shield")
    shield_bands = fin(bm, "Canister_ShieldBands")

    # valve wheel: origin at the wheel centre, spins about local Y
    VC = Vector((0, -330, 504))
    bm = bmesh.new()
    w = lambda r, y: yring(10, r, y, math.pi / 10)
    loft(bm, [w(105, 11), w(105, -11), w(83, -11), w(83, 11)], "Dark", closed=True)
    for k in range(5):
        a = math.radians(72 * k)
        u, v = Vector((math.sin(a), 0, math.cos(a))), Vector((math.cos(a), 0, -math.sin(a)))
        sp = lambda r: [tuple(r * u + s * 7 * v + Vector((0, q * 6, 0))) for s, q in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        loft(bm, [sp(20), sp(90)], "Dark", cap0="Dark", cap1="Dark")
    h = lambda r, y: yring(8, r, y, math.pi / 8)
    loft(bm, [h(26, 14), h(26, -14), h(18, -20)], "Dark", cap0="Dark", cap1="Dark")
    loft(bm, [yring(6, 12, 14), yring(6, 12, 52)], "Dark", cap0="Dark", cap1="Dark")
    valve = fin(bm, "Canister_ValveWheel")
    valve.location = (rotz(VALVE_A) @ VC) * S
    valve.rotation_euler = (0, 0, math.radians(VALVE_A))

    # the level bar's four lights, left to right: tg's can-0 lights the first, can-3 all four
    lights = []
    for i, (x0, x1, m) in enumerate(((-79, -40, "Emission_Red"), (-40, 0, "Emission_Amber"), (0, 40, "Emission_Olive"),
                                     (40, 79, "Emission_Green"))):
        bm = bmesh.new()
        box(bm, x0 + 0.5, x1 - 0.5, -176, -170, 774, 822, m)
        lights.append(fin(bm, "Canister_Level%d" % i))
    # the port light on the head: red off a connector port, green on one (tg's can-connector: connected_port)
    for nm, m in (("Canister_PortLightRed", "Emission_PortRed"), ("Canister_PortLightGreen", "Emission_LED")):
        bm = bmesh.new()
        loft(bm, [yring(8, 18, -165, 0, -66, 922), yring(8, 18, -175, 0, -66, 922), yring(8, 12, -181, 0, -66, 922)],
             m, cap0=m, cap1=m)
        lights.append(fin(bm, nm))

    parts = [body, door, door_open, tank, valve, window, hatch, hatch_open, cell, shield, shield_bands] + lights
    # drop unused material slots so the GLB carries only what each mesh uses
    for ob in parts:
        if ob in (door_open, hatch_open):
            continue  # shares the shut one's mesh
        used = {p.material_index for p in ob.data.polygons}
        for i in reversed(range(len(ob.data.materials))):
            if i not in used:
                ob.data.materials.pop(index=i)  # pop shifts the polygon indices above i
        ob.data.name = ob.name

    os.makedirs(OUT, exist_ok=True)
    bpy.context.window.scene = scn
    vl = scn.view_layers[0]
    for o in scn.objects:
        o.select_set(o in parts, view_layer=vl)
    with bpy.context.temp_override(scene=scn, view_layer=vl):
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name.lower() + ".glb"), export_format="GLB", use_selection=True,
                                  use_active_scene=True, export_yup=True, export_lights=False, export_cameras=False)
    bpy.data.libraries.write(os.path.join(OUT, name.lower() + ".blend"), {scn}, compress=True)
    # the most drawn at once: one door, one hatch, one port light; the shield apart (an effect)
    drawn = [body, door, tank, valve, window, hatch, cell] + lights[:-1]
    shield_verts = len(shield.data.vertices) + len(shield_bands.data.vertices)
    stats = {"verts_drawn_max": sum(len(o.data.vertices) for o in drawn),
             "tris_drawn_max": sum(len(p.vertices) - 2 for o in drawn for p in o.data.polygons),
             "shield_verts": shield_verts, "nodes": sorted(o.name for o in parts)}
    if not keep:  # object names are global: the next pattern needs them free
        for o in parts:
            bpy.data.objects.remove(o)
        bpy.context.window.scene = next(x for x in bpy.data.scenes if x != scn)
        bpy.data.scenes.remove(scn)
    return stats


old_scene = bpy.data.scenes.get("PortablePump")  # this model's first, pre-canister build
if old_scene:
    for o in list(old_scene.collection.all_objects):
        bpy.data.objects.remove(o)
    bpy.context.window.scene = next(x for x in bpy.data.scenes if x != old_scene)
    bpy.data.scenes.remove(old_scene)
for o in [o for o in bpy.data.objects if o.name.startswith("Canister_")]:  # a kept preview scene of an earlier run
    bpy.data.objects.remove(o)
result ={p: build(p, keep=p == list(PATTERNS)[-1]) for p in PATTERNS}
