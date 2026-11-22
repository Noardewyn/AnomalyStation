"""Writes godot/assets/models/structures/multilevel/lift_indicator.glb (D325): tg's elevator hall lantern (lift_indicator.dmi) as a box.

The box covers the opaque pixels of "lift_indo-base" (x 8..25, rows 1..13 of the 32 x 32 frame) in a 2 x 2 m source frame (1/16 m a pixel),
2 pixels deep, its back at z 0 and its front at +Z. Its texture is the converted tg sheet, byte for byte, and every uv lies in the base frame, so the
client draws it with the indicator's live picture (its arrows, ModelFrame); the faces take the pixel they stand on in the front view, the sides
the edge pixels. The floor number (maptext) goes on the front face. Run: python tools/models/make_lift_indicator.py
"""
import json, struct, os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SHEET = os.path.join(ROOT, "godot", "assets", "tg", "icons", "obj", "machines", "lift_indicator.png")  # the converted sheet
SHEET_SIZE = 160
PX = 1 / 16  # metres a pixel in the 2 m source frame
X0, X1 = -1 + 8 * PX, -1 + 25 * PX
Y0, Y1 = (32 - 13) * PX, (32 - 1) * PX
Z0, Z1 = 0.0, 2 * PX

c = [(X0, Y0, Z0), (X1, Y0, Z0), (X1, Y1, Z0), (X0, Y1, Z0), (X0, Y0, Z1), (X1, Y0, Z1), (X1, Y1, Z1), (X0, Y1, Z1)]
quads = [((0, 3, 2, 1), (0, 0, -1)), ((4, 5, 6, 7), (0, 0, 1)), ((0, 4, 7, 3), (-1, 0, 0)), ((1, 2, 6, 5), (1, 0, 0)),
         ((3, 7, 6, 2), (0, 1, 0)), ((0, 1, 5, 4), (0, -1, 0))]
pos, nrm, uv = [], [], []
for q, n in quads:
    for i in (q[0], q[1], q[2], q[0], q[2], q[3]):
        p = c[i]
        # the pixel the face stands on, a quarter pixel inside it (ModelMesh.SpriteUv)
        ix, iy = p[0] - n[0] * PX / 4, p[1] - n[1] * PX / 4
        pos.append(p)
        nrm.append(n)
        uv.append(((ix + 1) / PX / SHEET_SIZE, (32 - iy / PX) / SHEET_SIZE))

png = open(SHEET, "rb").read()
bin_ = bytearray()
views, accessors = [], []
for data, kind, count, extra in [(b"".join(struct.pack("<3f", *p) for p in pos), "VEC3", len(pos),
                                  {"min": [X0, Y0, Z0], "max": [X1, Y1, Z1]}),
                                 (b"".join(struct.pack("<3f", *n) for n in nrm), "VEC3", len(nrm), {}),
                                 (b"".join(struct.pack("<2f", *t) for t in uv), "VEC2", len(uv), {})]:
    views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data), "target": 34962})
    bin_ += data
    accessors.append({"bufferView": len(views) - 1, "componentType": 5126, "count": count, "type": kind, **extra})
bin_ += b"\0" * (-len(bin_) % 4)
views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(png)})
bin_ += png

gltf = {
    "asset": {"version": "2.0", "generator": "Anomaly Station tools/models/make_lift_indicator.py"},
    "scene": 0, "scenes": [{"nodes": [0]}],
    "nodes": [{"name": "Lift indicator", "mesh": 0}],
    "meshes": [{"name": "Lift indicator", "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "material": 0}]}],
    "materials": [{"name": "lift_indicator", "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 0.8}}],
    "textures": [{"source": 0, "sampler": 0}],
    "samplers": [{"magFilter": 9728, "minFilter": 9728}],
    "images": [{"bufferView": len(views) - 1, "mimeType": "image/png"}],
    "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": len(bin_)}],
}
js = json.dumps(gltf, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
bin_ += b"\0" * (-len(bin_) % 4)
out = os.path.join(ROOT, "godot", "assets", "models", "structures", "multilevel", "lift_indicator.glb")
with open(out, "wb") as f:
    f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)))
    f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
    f.write(struct.pack("<II", len(bin_), 0x004E4942) + bin_)
print("wrote", os.path.normpath(out), 12 + 16 + len(js) + len(bin_), "bytes")
