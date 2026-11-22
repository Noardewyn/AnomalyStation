"""Writes godot/assets/models/structures/stairs/stairs.glb (D325): tg's stairs as one flight that rises a level over its tile.

The flight is 2 x 2 m and 2 m high (the catalog fits it to the tile and the wall height, which is the level height), low end
at -Z and high end at +Z: the catalog's dir4 rule turns +Z (south) to the stairs' dir, the way they go up. Eight solid
steps of steel with a yellow nosing strip. No texture: flat material colours. Run: python tools/models/make_stairs.py
"""
import json, struct, os

STEPS, WIDTH, RUN, RISE = 8, 2.0, 2.0, 2.0
STRIP = 0.05  # the nosing strip's depth (m)


def box(x0, x1, y0, y1, z0, z1):
    """A closed box's 12 triangles as flat positions (no shared corners: the reader makes flat normals)."""
    c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    quads = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 4, 7, 3), (1, 2, 6, 5), (3, 7, 6, 2), (0, 1, 5, 4)]  # -z, +z, -x, +x, +y, -y (outward, CCW)
    tris = []
    for a, b, cc, d in quads:
        tris += [c[a], c[b], c[cc], c[a], c[cc], c[d]]
    return tris


steel, strip = [], []
run, rise, h = RUN / STEPS, RISE / STEPS, WIDTH / 2
for i in range(STEPS):
    z0 = -RUN / 2 + i * run
    top = (i + 1) * rise
    steel += box(-h, h, 0.0, top, z0, z0 + run)
    strip += box(-h, h, top, top + 0.004, z0, z0 + STRIP)

bin_ = bytearray()
views, accessors, prims = [], [], []
for mat, tris in enumerate([steel, strip]):
    data = b"".join(struct.pack("<3f", *p) for p in tris)
    views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data), "target": 34962})
    bin_ += data
    xs, ys, zs = zip(*tris)
    accessors.append({"bufferView": len(views) - 1, "componentType": 5126, "count": len(tris), "type": "VEC3",
                      "min": [min(xs), min(ys), min(zs)], "max": [max(xs), max(ys), max(zs)]})
    prims.append({"attributes": {"POSITION": len(accessors) - 1}, "material": mat})

gltf = {
    "asset": {"version": "2.0", "generator": "Anomaly Station tools/models/make_stairs.py"},
    "scene": 0, "scenes": [{"nodes": [0]}],
    "nodes": [{"name": "Stairs", "mesh": 0}],
    "meshes": [{"name": "Stairs", "primitives": prims}],
    "materials": [
        {"name": "Steel", "pbrMetallicRoughness": {"baseColorFactor": [0.42, 0.44, 0.47, 1.0], "metallicFactor": 0.6, "roughnessFactor": 0.55}},
        {"name": "Nosing", "pbrMetallicRoughness": {"baseColorFactor": [0.86, 0.68, 0.12, 1.0], "metallicFactor": 0.1, "roughnessFactor": 0.7}},
    ],
    "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": len(bin_)}],
}
js = json.dumps(gltf, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
bin_ += b"\0" * (-len(bin_) % 4)
out = os.path.join(os.path.dirname(__file__), "..", "..", "godot", "assets", "models", "structures", "stairs", "stairs.glb")
with open(out, "wb") as f:
    f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)))
    f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
    f.write(struct.pack("<II", len(bin_), 0x004E4942) + bin_)
print("wrote", os.path.normpath(out), 12 + 16 + len(js) + len(bin_), "bytes")
