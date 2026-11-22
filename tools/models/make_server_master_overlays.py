"""Writes godot/assets/models/machines/communications_wave/research_server_master_overlays.glb (gaps RD2): the master R&D server's
overlays (research.dmi "RD-server-objective-stripes", added at Initialize, server.dm:126, and "RD-server-hdd-panel-open", added
when the front panel's last screw is out or the hard drive is overloaded, server.dm:198 and :258) as simple shapes.

The server's model (research_server.glb) is authored in metres: its chassis spans x -0.82..0.82, y 0.04..1.6, z -0.46..0.46 with the
front at z +0.46 (the status lights at x 0.02..0.72, y 0.48..0.83; the service hatch at x -0.70..-0.12). Objective_Stripes is a band
of yellow and black hazard stripes across the front's top; HDD_Panel_Open is the dark recess of the drive's housing low on the
front with the unscrewed front panel hanging open below it. The models.json entry draws each while the server has its overlay.
These stand in until proper models exist (required-models.md). Run: python tools/models/make_server_master_overlays.py
"""
import json, struct, os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "godot", "assets", "models", "machines", "communications_wave", "research_server_master_overlays.glb")

COLOURS = {
    "hazard_yellow": (0.95, 0.78, 0.08, 1.0),
    "hazard_black": (0.06, 0.06, 0.07, 1.0),
    "recess": (0.03, 0.03, 0.035, 1.0),
    "panel": (0.5, 0.52, 0.56, 1.0),
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
STRIPES = []
for i in range(8):
    STRIPES.append(("hazard_yellow" if i % 2 == 0 else "hazard_black", (-0.7 + 0.2 * i, 0, 0, 0.2, 0.16, 0.012)))
NODES = [
    ("Objective_Stripes", (0, 1.38, 0.466), STRIPES),
    ("HDD_Panel_Open", (0, 0.26, 0.466), [
        ("recess", (0, 0, 0, 1.0, 0.26, 0.012)),
        # the front panel, unscrewed and hanging open below the housing
        ("panel", (0, -0.15, 0.15, 1.04, 0.02, 0.3)),
    ]),
]

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
    "asset": {"version": "2.0", "generator": "Anomaly Station tools/models/make_server_master_overlays.py"},
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
