using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Render;

/// <summary>
/// The meshes of the world renderer (D113): the tile kit (a floor, a ceiling and a wall box in one mesh, an instance draws one part),
/// the unit picture quad of billboards and wall decals, and the shaped pieces (windows, bars, a table, a rail, flaps, a light box, two
/// airlock panels, a pod door slab, a windoor pane) in one mesh with a part id per vertex (UV2.x). A part's vertices carry its
/// own uv; the shaders move the vertices of the parts an instance does not draw out of the view.
/// </summary>
public static class WorldMeshes
{
    private sealed class Builder
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Vector2> Uv2 = new();
        public readonly List<int> Indices = new();

        // A quad listed counter-clockwise seen from outside (along its normal); Godot's front faces are clockwise.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, float part, float arm = 0f)
        {
            int i = Vertices.Count;
            Vertices.AddRange(new[] { a, b, c, d });
            Normals.AddRange(new[] { normal, normal, normal, normal });
            Uvs.AddRange(new[] { ua, ub, uc, ud });
            var p = new Vector2(part, arm); // UV2.x the part, UV2.y the arm or corner post a face belongs to (0: always drawn; M3 R3)
            Uv2.AddRange(new[] { p, p, p, p });
            Indices.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        public ArrayMesh Build()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = Normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = Uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = Uv2.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = Indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }

    private static Vector3 V(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Vector2 V(System.Numerics.Vector2 v) => new(v.X, v.Y);

    private static ArrayMesh FromQuads(List<GeoQuad> quads)
    {
        var b = new Builder();
        foreach (GeoQuad q in quads)
            b.Quad(V(q.A), V(q.B), V(q.C), V(q.D), V(q.Normal), V(q.Ua), V(q.Ub), V(q.Uc), V(q.Ud), q.Part, q.Arm);
        return b.Build();
    }

    /// <summary>The tile kit (<see cref="WorldGeometry.TileKit"/>): a floor, a ceiling and a wall box in one mesh, an instance draws one part. Every face maps its sprite once.</summary>
    public static ArrayMesh TileKit() => FromQuads(WorldGeometry.TileKit());

    /// <summary>The unit picture quad of billboards and wall decals (<see cref="WorldGeometry.PictureQuad"/>).</summary>
    public static ArrayMesh PictureQuad() => FromQuads(WorldGeometry.PictureQuad());

    /// <summary>The shaped pieces (<see cref="WorldGeometry.Pieces"/>): windows, bars, a table, a rail, flaps, a light box, two airlock panels, a pod door slab, a windoor pane.</summary>
    public static ArrayMesh Pieces() => FromQuads(WorldGeometry.Pieces());

    /// <summary>The occluder mesh of wall boxes: closed boxes as high as the wall (<see cref="WorldScale.WallHeight"/>).</summary>
    public static ArrayOccluder3D Occluder(IEnumerable<WallBox> boxes)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        foreach (WallBox box in boxes)
        {
            float x0 = (box.MinX - 1) * TileSpace.TileSize, x1 = box.MaxX * TileSpace.TileSize;
            float zNorth = -box.MaxY * TileSpace.TileSize, zSouth = -(box.MinY - 1) * TileSpace.TileSize;
            float y0 = TileSpace.WorldY(box.Z), y1 = y0 + TileSpace.LevelHeight;
            int i = vertices.Count;
            vertices.AddRange(new Vector3[]
            {
                new(x0, y0, zNorth), new(x1, y0, zNorth), new(x1, y0, zSouth), new(x0, y0, zSouth),
                new(x0, y1, zNorth), new(x1, y1, zNorth), new(x1, y1, zSouth), new(x0, y1, zSouth),
            });
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 7, 6, 5 }, new[] { 0, 4, 5, 1 }, new[] { 1, 5, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 3, 7, 4, 0 },
            };
            foreach (int[] f in faces)
            {
                indices.AddRange(new[] { i + f[0], i + f[1], i + f[2], i + f[0], i + f[2], i + f[3] });
            }
        }
        var occluder = new ArrayOccluder3D();
        occluder.SetArrays(vertices.ToArray(), indices.ToArray());
        return occluder;
    }
}
