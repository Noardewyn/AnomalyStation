"""Atlas-based, closed rectilinear mesh primitives for Blender asset generation."""
from pathlib import Path
import bpy, bmesh, ast, json, math
from mathutils import Vector

ATLAS=None
SOURCE=None
STATE=None
DIR=0
EXCLUDE=None


def enum(obj,prop,value):
    values=[v.identifier for v in obj.bl_rna.properties[prop].enum_items]
    if value not in values:
        try:setattr(obj,prop,'__inspect_available__')
        except TypeError as error:values=ast.literal_eval(str(error).split('not found in ',1)[1])
    return next(v for v in values if v==value)


def pixel(state,x,y,direction=0):
    if EXCLUDE and EXCLUDE(x,y):return (0,0,0,0)
    rect=SOURCE['states'][state]['rects'][DIR*SOURCE['states'][state]['frames']]
    w,h=SOURCE['image'].size
    pos=((h-1-rect[1]-int(y))*w+rect[0]+int(x))*4
    return SOURCE['pixels'][pos:pos+4]


def uv(state,x,y,direction=0):
    # State selection lives in the material, allowing mesh sharing across variants.
    return (x/32,1-y/32)


def stepped_rectangle(width,depth,step):
    x=width/2;y=depth/2;s=step
    return [(-x+s,-y),(x-s,-y),(x-s,-y+s),(x,-y+s),(x,y-s),(x-s,y-s),
            (x-s,y),(-x+s,y),(-x+s,y-s),(-x,y-s),(-x,-y+s),(-x+s,-y+s)]


def prism(mesh,plan,z0,z1,state,sample,top_patch=None):
    const=uv(state,sample[0]+.5,sample[1]+.5)
    mesh.face([(x,y,z0) for x,y in plan],[const]*len(plan))
    coords=[const]*len(plan)
    if top_patch:
        x0,y0,x1,y1=top_patch
        minx=min(p[0] for p in plan);maxx=max(p[0] for p in plan)
        miny=min(p[1] for p in plan);maxy=max(p[1] for p in plan)
        coords=[uv(state,x0+(x-minx)/(maxx-minx)*(x1-x0),y1-(y-miny)/(maxy-miny)*(y1-y0)) for x,y in plan]
    mesh.face([(x,y,z1) for x,y in plan],coords)
    for a,b in zip(plan,plan[1:]+plan[:1]):mesh.face([(*a,z0),(*b,z0),(*b,z1),(*a,z1)],[const]*4)


def vessel(mesh,outer,inner,z0,z1,floor,state,sample,inside_sample=None):
    """Closed thickness, open top, lined interior; matching rectilinear boundary loops."""
    c=uv(state,sample[0]+.5,sample[1]+.5)
    ci=uv(state,(inside_sample or sample)[0]+.5,(inside_sample or sample)[1]+.5)
    mesh.face([(*p,z0) for p in outer],[c]*len(outer))
    mesh.face([(*p,floor) for p in inner],[ci]*len(inner))
    for i in range(len(outer)):
        j=(i+1)%len(outer);a,b=outer[i],outer[j];aa,bb=inner[i],inner[j]
        mesh.face([(*a,z0),(*b,z0),(*b,z1),(*a,z1)],[c]*4)
        mesh.face([(*a,z1),(*b,z1),(*bb,z1),(*aa,z1)],[c]*4)
        mesh.face([(*aa,z1),(*bb,z1),(*bb,floor),(*aa,floor)],[ci]*4)


# The PixelMesh implementation below uses the same verified cap/boundary algorithm
# as the chair family, including T-junction splitting and UV-preserving simplification.

class Mesh:

    def __init__(self):
        self.v, self.f, self.uvs = ([], [], [])
        self.lookup = {}

    def face(self, points, coords):
        ids = []
        for point in points:
            point = tuple((round(c, 7) for c in point))
            if point not in self.lookup:
                self.lookup[point] = len(self.v)
                self.v.append(point)
            ids.append(self.lookup[point])
        self.f.append(ids)
        self.uvs.append(coords)

    def box(self, bounds, state, sample, front=None):
        x0, x1, y0, y1, z0, z1 = bounds
        v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        constant = uv(state, sample[0] + 0.5, sample[1] + 0.5)
        assert pixel(state, *sample)[3] > 0.99
        for n, ids in enumerate([(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]):
            coords = [constant] * 4
            if n == 2 and front:
                a, b, c, d = front
                coords = [uv(state, a, d), uv(state, c, d), uv(state, c, b), uv(state, a, b)]
            self.face([v[i] for i in ids], coords)

    def profile(self, state, rect, transform, thickness, sample, rear_direction=None, rear_shift=0):
        """Greedy cap rectangles plus merged boundary edges; split all T junctions."""
        x0, y0, x1, y1 = rect
        cells = {(x, y) for y in range(y0, y1) for x in range(x0, x1) if pixel(state, x, y)[3] > 0.99}
        pending = set(cells)
        caps = []
        while pending:
            x, y = min(pending, key=lambda p: (p[1], p[0]))
            right = x + 1
            while (right, y) in pending:
                right += 1
            bottom = y + 1
            while all(((u, bottom) in pending for u in range(x, right))):
                bottom += 1
            caps.append([(x, y), (right, y), (right, bottom), (x, bottom)])
            pending.difference_update(((u, v) for v in range(y, bottom) for u in range(x, right)))
        edges = []
        for x, y in cells:
            for neighbor, a, b in [((x, y - 1), (x, y), (x + 1, y)), ((x + 1, y), (x + 1, y), (x + 1, y + 1)), ((x, y + 1), (x + 1, y + 1), (x, y + 1)), ((x - 1, y), (x, y + 1), (x, y))]:
                if neighbor not in cells:
                    edges.append((a, b))
        remaining = set(edges)
        edges = []
        while remaining:
            a, b = remaining.pop()
            dx, dy = (b[0] - a[0], b[1] - a[1])
            while (b, (b[0] + dx, b[1] + dy)) in remaining:
                nxt = (b[0] + dx, b[1] + dy)
                remaining.remove((b, nxt))
                b = nxt
            while ((a[0] - dx, a[1] - dy), a) in remaining:
                prev = (a[0] - dx, a[1] - dy)
                remaining.remove((prev, a))
                a = prev
            edges.append((a, b))
        corners = set((p for cap in caps for p in cap)) | set((p for edge in edges for p in edge))

        def split(poly):
            result = []
            for a, b in zip(poly, poly[1:] + poly[:1]):
                dx, dy = (b[0] - a[0], b[1] - a[1])
                line = [p for p in corners if (p[0] - a[0]) * dy == (p[1] - a[1]) * dx and min(a[0], b[0]) <= p[0] <= max(a[0], b[0]) and (min(a[1], b[1]) <= p[1] <= max(a[1], b[1]))]
                line.sort(key=lambda p: (p[0] - a[0]) * dx + (p[1] - a[1]) * dy)
                result += line[:-1]
            return result
        const = uv(state, sample[0] + 0.5, sample[1] + 0.5)
        assert pixel(state, *sample)[3] > 0.99
        for cap in caps:
            poly = split(cap)
            coords = []
            cx = sum((x for x, y in cap)) / 4
            cy = sum((y for x, y in cap)) / 4
            for x, y in poly:
                coords.append(uv(state, x, y))
            self.face([transform(x, y, 0) for x, y in poly], coords)
            if rear_direction is None:
                rear = [const] * len(poly)
            else:
                rear = [uv(state, x, y + rear_shift, rear_direction) for x, y in poly]
            self.face([transform(x, y, thickness) for x, y in poly], rear)
        for a, b in edges:
            dx, dy = (b[0] - a[0], b[1] - a[1])
            line = [p for p in corners if (p[0] - a[0]) * dy == (p[1] - a[1]) * dx and min(a[0], b[0]) <= p[0] <= max(a[0], b[0]) and (min(a[1], b[1]) <= p[1] <= max(a[1], b[1]))]
            line.sort(key=lambda p: (p[0] - a[0]) * dx + (p[1] - a[1]) * dy)
            points = [transform(*p, 0) for p in line] + [transform(*p, thickness) for p in reversed(line)]
            self.face(points, [const] * len(points))

    def object(self, name, scene, mat=ATLAS, pivot=(0, 0, 0)):
        me = bpy.data.meshes.new(name + '_Mesh')
        me.from_pydata([tuple((v[i] - pivot[i] for i in range(3))) for v in self.v], [], self.f)
        me.materials.append(mat)
        layer = me.uv_layers.new(name='Original_Atlas_UV')
        for p, coords in zip(me.polygons, self.uvs):
            for li, co in zip(p.loop_indices, coords):
                layer.data[li].uv = co
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bmesh.ops.dissolve_limit(bm, angle_limit=1e-05, verts=list(bm.verts), edges=list(bm.edges), use_dissolve_boundaries=False, delimit={'UV'})
        bad = [e for e in bm.edges if not e.is_manifold]
        assert not bad, (name, len(bad))
        assert all((v.is_manifold for v in bm.verts)), (name, 'non-manifold vertex')
        bm.to_mesh(me)
        bm.free()
        me.update()
        me.calc_loop_triangles()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        ob.location = pivot
        return ob


