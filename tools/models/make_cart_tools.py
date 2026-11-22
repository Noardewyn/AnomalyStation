"""Writes godot/assets/models/station_props/furniture_wave/janitorial_cart_tools.glb (gaps H4): the things a janitorial cart carries,
tg's overlays of the cart (janitor.dmi "cart_mop", "cart_broom", "cart_garbage", "cart_bluespace_garbage", "cart_spray",
"cart_replacer", "cart_sign1" to "cart_sign4"; janitor.dm:359 update_overlays) as simple shapes.

The cart's model (janitorial_cart.glb) is authored in metres with empty anchor nodes where its author meant the tools to hang: Mop
(-0.57, 0.5, -0.4), Broom (-0.57, 0.5, -0.2), Trash_Bag (0.57, 0.55, -0.2), Spray (0.45, 0.75, 0), Light_Replacer (0.45, 0.7, 0.2)
and Signs (0, 0.55, -0.75). Each thing here is a node of that name at that anchor, built of boxes in flat colours: the mop and the
push broom stand in their clips by the cart's side, the trash bag hangs on the other side (a blue one for the bluespace bag), the
spray bottle and the light replacer sit on the top shelf, and up to four yellow wet floor signs lean in the rack at the back
(Sign_1 to Sign_4, each drawn while the cart holds at least that many). The models.json entry draws each while the cart has its
overlay. These stand in until proper models exist (required-models.md). Run: python tools/models/make_cart_tools.py
"""
import json, struct, os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "godot", "assets", "models", "station_props", "furniture_wave", "janitorial_cart_tools.glb")

COLOURS = {
    "handle": (0.55, 0.55, 0.6, 1.0),
    "mop_head": (0.85, 0.85, 0.8, 1.0),
    "broom_head": (0.35, 0.25, 0.15, 1.0),
    "bag": (0.08, 0.08, 0.09, 1.0),
    "bag_bluespace": (0.15, 0.35, 0.9, 1.0),
    "spray": (0.3, 0.55, 0.95, 1.0),
    "nozzle": (0.95, 0.95, 0.95, 1.0),
    "replacer": (0.75, 0.7, 0.25, 1.0),
    "sign": (0.98, 0.82, 0.1, 1.0),
}
MATERIALS = list(COLOURS)


def box(cx, cy, cz, sx, sy, sz):
    """A box centred at (cx, cy, cz) with sizes (sx, sy, sz): 24 vertices (flat normals), 36 indices."""
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    faces = [
        ((1, 0, 0), [(hx, -hy, -hz), (hx, hy, -hz), (hx, hy, hz), (hx, -hy, hz)]),
        ((-1, 0, 0), [(-hx, -hy, hz), (-hx, hy, hz), (-hx, hy, -hz), (-hx, -hy, -hz)]),
        ((0, 1, 0), [(-hx, hy, -hz), (-hx, hy, hz), (hx, hy, hz), (hx, hy, -hz)]),
        ((0, -1, 0), [(-hx, -hy, hz), (-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz)]),
        ((0, 0, 1), [(hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz), (-hx, -hy, hz)]),
        ((0, 0, -1), [(-hx, -hy, -hz), (-hx, hy, -hz), (hx, hy, -hz), (hx, -hy, -hz)]),
    ]
    pos, nrm, idx = [], [], []
    for n, quad in faces:
        base = len(pos)
        for (x, y, z) in quad:
            pos.append((cx + x, cy + y, cz + z))
            nrm.append(n)
        idx += [base, base + 2, base + 1, base, base + 3, base + 2]
    return pos, nrm, idx


# Each node: (name, anchor, [(material, box)]); boxes are relative to the anchor.
NODES = [
    ("Mop", (-0.57, 0.5, -0.4), [("handle", (0, 0.15, 0, 0.03, 1.1, 0.03)), ("mop_head", (0, -0.42, 0, 0.14, 0.12, 0.14))]),
    ("Broom", (-0.57, 0.5, -0.2), [("handle", (0, 0.15, 0, 0.03, 1.1, 0.03)), ("broom_head", (0, -0.42, 0, 0.06, 0.08, 0.32))]),
    ("Trash_Bag", (0.57, 0.55, -0.2), [("bag", (0.02, 0, 0, 0.2, 0.42, 0.3))]),
    ("Trash_Bag_Bluespace", (0.57, 0.55, -0.2), [("bag_bluespace", (0.02, 0, 0, 0.2, 0.42, 0.3))]),
    ("Spray", (0.45, 0.75, 0), [("spray", (-0.2, 0.43, 0, 0.07, 0.18, 0.07)), ("nozzle", (-0.2, 0.55, 0.02, 0.04, 0.05, 0.08))]),
    ("Light_Replacer", (0.45, 0.7, 0.2), [("replacer", (-0.2, 0.46, 0, 0.1, 0.1, 0.26))]),
]
for i in range(4):
    NODES.append((f"Sign_{i + 1}", (0, 0.55, -0.75), [("sign", (0, 0.05, -0.03 * i, 0.34, 0.5, 0.02))]))

bin_ = bytearray()
views, accessors, meshes, nodes = [], [], [], []


def add(data, kind, count, extra=None, target=34962):
    views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data), "target": target})
    bin_.extend(data)
    bin_.extend(b"\0" * (-len(bin_) % 4))
    accessors.append({"bufferView": len(views) - 1, "componentType": 5126 if kind != "SCALAR" else 5125, "count": count, "type": kind,
                      **(extra or {})})
    return len(accessors) - 1


for name, (ax, ay, az), parts in NODES:
    primitives = []
    for material, (cx, cy, cz, sx, sy, sz) in parts:
        pos, nrm, idx = box(cx, cy, cz, sx, sy, sz)
        xs, ys, zs = [p[0] for p in pos], [p[1] for p in pos], [p[2] for p in pos]
        p_acc = add(b"".join(struct.pack("<3f", *p) for p in pos), "VEC3", len(pos),
                    {"min": [min(xs), min(ys), min(zs)], "max": [max(xs), max(ys), max(zs)]})
        n_acc = add(b"".join(struct.pack("<3f", *n) for n in nrm), "VEC3", len(nrm))
        i_acc = add(b"".join(struct.pack("<I", i) for i in idx), "SCALAR", len(idx), target=34963)
        primitives.append({"attributes": {"POSITION": p_acc, "NORMAL": n_acc}, "indices": i_acc, "material": MATERIALS.index(material)})
    meshes.append({"name": name, "primitives": primitives})
    nodes.append({"name": name, "mesh": len(meshes) - 1, "translation": [ax, ay, az]})

gltf = {
    "asset": {"version": "2.0", "generator": "Anomaly Station tools/models/make_cart_tools.py"},
    "scene": 0, "scenes": [{"nodes": list(range(len(nodes)))}],
    "nodes": nodes,
    "meshes": meshes,
    "materials": [{"name": m, "pbrMetallicRoughness": {"baseColorFactor": list(COLOURS[m]), "metallicFactor": 0.0, "roughnessFactor": 0.7}}
                  for m in MATERIALS],
    "buffers": [{"byteLength": len(bin_)}],
    "bufferViews": views,
    "accessors": accessors,
}
js = json.dumps(gltf, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_))
out += struct.pack("<II", len(js), 0x4E4F534A) + js
out += struct.pack("<II", len(bin_), 0x004E4942) + bytes(bin_)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, "wb").write(out)
print(f"wrote {OUT} ({len(out)} bytes, {len(nodes)} nodes)")
