"""Writes godot/assets/models/station_props/bathroom/shower_water.glb (gaps H1): the water a running shower pours, tg's "water" overlay
(watercloset.dmi, shower.dm:203 update_overlays) as two planes crossing under the shower head.

The wall shower's model (wall_shower.glb) is authored in metres, looking south (+Z) with its back on the wall: its head hangs at
y 1.94 m, z 0.06 m. The water falls from just under the head to the floor, 0.7 m wide, centred 0.3 m before the wall, on two
planes crossing at 45 degrees (as the flame planes, D302), so it reads from every side. Each plane shows the south frame of tg's
"water" state (x 0..32, y 128..160 of the 416 x 384 converted sheet) whole; its material blends by the picture's alpha (the
client's stipple glass, models.md), so only tg's drops show. The models.json entry draws it while the shower has its "water"
overlay. Run: python tools/models/make_shower_water.py
"""
import json, math, struct, os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SHEET = os.path.join(ROOT, "godot", "assets", "tg", "icons", "obj", "watercloset.png")  # the converted sheet
SHEET_W, SHEET_H = 416, 384
FRAME = (0, 128)  # the "water" state's first (south) frame
TOP, BOTTOM = 1.88, 0.0
HALF = 0.35
CENTRE_Z = 0.30

pos, nrm, uv = [], [], []
u0, v0 = FRAME[0] / SHEET_W, FRAME[1] / SHEET_H
u1, v1 = (FRAME[0] + 32) / SHEET_W, (FRAME[1] + 32) / SHEET_H
for angle in (math.radians(45), math.radians(-45)):
    dx, dz = math.cos(angle) * HALF, math.sin(angle) * HALF
    nx, nz = -math.sin(angle), math.cos(angle)
    corners = [(-dx, BOTTOM, CENTRE_Z - dz, u0, v1), (dx, BOTTOM, CENTRE_Z + dz, u1, v1),
               (dx, TOP, CENTRE_Z + dz, u1, v0), (-dx, TOP, CENTRE_Z - dz, u0, v0)]
    # both faces, so the water shows from behind as well
    for order, sign in (((0, 1, 2, 0, 2, 3), 1), ((0, 2, 1, 0, 3, 2), -1)):
        for i in order:
            x, y, z, u, v = corners[i]
            pos.append((x, y, z))
            nrm.append((nx * sign, 0.0, nz * sign))
            uv.append((u, v))

png = open(SHEET, "rb").read()
bin_ = bytearray()
views, accessors = [], []
xs, ys, zs = [p[0] for p in pos], [p[1] for p in pos], [p[2] for p in pos]
for data, kind, count, extra in [(b"".join(struct.pack("<3f", *p) for p in pos), "VEC3", len(pos),
                                  {"min": [min(xs), min(ys), min(zs)], "max": [max(xs), max(ys), max(zs)]}),
                                 (b"".join(struct.pack("<3f", *n) for n in nrm), "VEC3", len(nrm), {}),
                                 (b"".join(struct.pack("<2f", *t) for t in uv), "VEC2", len(uv), {})]:
    views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data), "target": 34962})
    bin_ += data
    accessors.append({"bufferView": len(views) - 1, "componentType": 5126, "count": count, "type": kind, **extra})
bin_ += b"\0" * (-len(bin_) % 4)
views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(png)})
bin_ += png

gltf = {
    "asset": {"version": "2.0", "generator": "Anomaly Station tools/models/make_shower_water.py"},
    "scene": 0, "scenes": [{"nodes": [0]}],
    "nodes": [{"name": "Shower water", "mesh": 0}],
    "meshes": [{"name": "Shower water", "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "material": 0}]}],
    "materials": [{"name": "shower_water", "alphaMode": "BLEND", "doubleSided": True,
                   "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 0.4}}],
    "textures": [{"source": 0, "sampler": 0}],
    "samplers": [{"magFilter": 9728, "minFilter": 9728}],
    "images": [{"bufferView": len(views) - 1, "mimeType": "image/png", "name": "watercloset"}],
    "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": len(bin_)}],
}
js = json.dumps(gltf, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
bin_ += b"\0" * (-len(bin_) % 4)
out = os.path.join(ROOT, "godot", "assets", "models", "station_props", "bathroom", "shower_water.glb")
with open(out, "wb") as f:
    f.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(js) + 8 + len(bin_)))
    f.write(struct.pack("<I4s", len(js), b"JSON"))
    f.write(js)
    f.write(struct.pack("<I4s", len(bin_), b"BIN\0"))
    f.write(bin_)
print("wrote", out, len(pos), "vertices")
