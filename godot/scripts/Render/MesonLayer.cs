using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// DEVIATION(DV81): a mob with SEE_TURFS (tg's TRAIT_MESON_VISION: the optical meson scanner) sees every turf of its view through the walls and
// the dark: BYOND's client draws the walls and floors with the sight flag whatever is in the way. The 3D client draws the turfs within
// eleven tiles of the wearer on his level as a light green outline through everything (depth test off), over the third-person sight shade
// (D289): each wall run as a translucent green block with its edges, each open floor as a faint square with its edges and a fainter ceiling
// square over it, so the wearer sees where the rooms, corridors and walls are behind the walls of the room he stands in and in the dark. Nothing else (mobs, objects) is shown: those are SEE_MOBS and SEE_OBJS.

/// <summary>The meson goggles' view of the turfs round the wearer, redrawn when he moves a tile, when the sight changes or the map does.</summary>
public partial class MesonLayer : Node3D
{
    private const float Lift = 0.03f;
    // D289: drawn over the third-person sight shade (FovShadow, a step below): the dark stays, the walls show through it
    public const int Priority = (int)Material.RenderPriorityMax;
    private MeshInstance3D _wallFaces, _wallEdges, _floorFaces, _floorEdges;
    private MesonCell[] _drawn;
    private (int X, int Y, int Z) _at;
    private double _poll;

    /// <summary>Whether the layer is drawing now.</summary>
    public bool Active => Visible;

    public override void _Ready()
    {
        Visible = false;
        // D289: the walls solid enough to read at a glance, the floor grid and the ceiling faint behind them
        _wallFaces = Part("WallFaces", new Color(0.3f, 1f, 0.4f, 0.28f));
        _wallEdges = Part("WallEdges", new Color(0.5f, 1f, 0.6f, 0.85f));
        _floorFaces = Part("FloorFaces", new Color(0.35f, 1f, 0.45f, 0.03f));
        _floorEdges = Part("FloorEdges", new Color(0.45f, 1f, 0.55f, 0.2f));
    }

    private MeshInstance3D Part(string name, Color color)
    {
        // IgnoreOcclusionCulling: the walls' occluders must not cull what is drawn through them
        var part = new MeshInstance3D { Name = name, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = SeeThrough(color), IgnoreOcclusionCulling = true };
        AddChild(part);
        return part;
    }

    /// <summary>
    /// D289: the material of what a scanner shows through the walls (see_through.gdshader): over the world, under the things in the own hands;
    /// back faces off, so each face of a wall run is seen once, not through its own far side.
    /// </summary>
    public static ShaderMaterial SeeThrough(Color tint, Texture2D picture = null)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/see_through.gdshader"), RenderPriority = Priority };
        material.SetShaderParameter("tint", tint);
        if (picture != null)
            material.SetShaderParameter("picture", picture);
        return material;
    }

    /// <summary>Follows the own mob's sight in <paramref name="replica"/> and the tile it stands on.</summary>
    public void Update(ReplicaWorld replica, float tileX, float tileY, int z, bool hasOwn, double delta)
    {
        bool on = hasOwn && replica != null && replica.OwnVision.SeesTurfs;
        Visible = on;
        if (!on)
        {
            _drawn = null;
            return;
        }
        var at = ((int)Math.Round(tileX), (int)Math.Round(tileY), z);
        _poll += delta;
        if (_drawn != null && at == _at && _poll < 0.5)
            return;
        _poll = 0;
        MesonCell[] plan = MesonView.Plan(replica, at.Item1, at.Item2, at.Item3);
        if (_drawn != null && at == _at && plan.AsSpan().SequenceEqual(_drawn))
            return;
        _drawn = plan;
        _at = at;
        Build(plan, at.Item1, at.Item2, at.Item3);
    }

    private void Build(MesonCell[] plan, int cx, int cy, int z)
    {
        int radius = MesonView.Radius, side = radius * 2 + 1;
        var wallFaces = new List<Vector3>();
        var wallEdges = new List<Vector3>();
        var floorFaces = new List<Vector3>();
        var floorEdges = new List<Vector3>();
        float floorY = TileSpace.WorldY(z) + Lift, top = TileSpace.WorldY(z) + TileSpace.LevelHeight;
        Vector3 up = Vector3.Up * (top - floorY), ceiling = Vector3.Up * (top - floorY - Lift);
        bool WallAt(int i, int dx, int dy)
        {
            int x = i % side + dx, y = i / side + dy;
            return x >= 0 && y >= 0 && x < side && y < side && plan[y * side + x] == MesonCell.Wall;
        }
        for (int i = 0; i < plan.Length; i++)
        {
            MesonCell cell = plan[i];
            if (cell == MesonCell.None)
                continue;
            int tx = cx + i % side - radius, ty = cy + i / side - radius;
            float x0 = TileSpace.WorldX(tx - 0.5f), x1 = TileSpace.WorldX(tx + 0.5f);
            float z0 = TileSpace.WorldZ(ty - 0.5f), z1 = TileSpace.WorldZ(ty + 0.5f); // z0 is the tile's south edge, z1 its north
            var a = new Vector3(x0, floorY, z0);
            var b = new Vector3(x1, floorY, z0);
            var c = new Vector3(x1, floorY, z1);
            var d = new Vector3(x0, floorY, z1);
            if (cell != MesonCell.Wall)
            {
                Face(floorFaces, a, b, c, d, a - up); // seen from above
                Loop(floorEdges, a, b, c, d);
                Face(floorFaces, a + ceiling, b + ceiling, c + ceiling, d + ceiling, a + up * 2); // D289: the ceiling over the floor, a fill only, seen from below
                continue;
            }
            Vector3 mid = (a + c) / 2 + up / 2;
            Face(wallFaces, a + up, b + up, c + up, d + up, mid);
            // a side between two walls is inside the wall run: not drawn, so a run reads as one block
            // (the replica's tile y grows north, world z south: plan row - 1 is the south neighbour, the a-b side)
            if (!WallAt(i, 0, -1)) Face(wallFaces, a, b, b + up, a + up, mid);
            if (!WallAt(i, 1, 0)) Face(wallFaces, b, c, c + up, b + up, mid);
            if (!WallAt(i, 0, 1)) Face(wallFaces, c, d, d + up, c + up, mid);
            if (!WallAt(i, -1, 0)) Face(wallFaces, d, a, a + up, d + up, mid);
            Loop(wallEdges, a, b, c, d);
            Loop(wallEdges, a + up, b + up, c + up, d + up);
            wallEdges.AddRange(new[] { a, a + up, b, b + up, c, c + up, d, d + up });
        }
        _wallFaces.Mesh = Surface(Mesh.PrimitiveType.Triangles, wallFaces);
        _wallEdges.Mesh = Surface(Mesh.PrimitiveType.Lines, wallEdges);
        _floorFaces.Mesh = Surface(Mesh.PrimitiveType.Triangles, floorFaces);
        _floorEdges.Mesh = Surface(Mesh.PrimitiveType.Lines, floorEdges);
    }

    // A face wound so it faces away from the point given (a wall box's middle) (Godot's front faces are clockwise seen from the front).
    private static void Face(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 middle)
    {
        if ((b - a).Cross(c - a).Dot(a - middle) > 0)
            Quad(v, a, d, c, b);
        else
            Quad(v, a, b, c, d);
    }

    private static void Quad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d) => v.AddRange(new[] { a, b, c, a, c, d });

    private static void Loop(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d) => v.AddRange(new[] { a, b, b, c, c, d, d, a });

    private static ArrayMesh Surface(Mesh.PrimitiveType type, List<Vector3> verts)
    {
        var mesh = new ArrayMesh();
        if (verts.Count == 0)
            return mesh;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        mesh.AddSurfaceFromArrays(type, arrays);
        return mesh;
    }
}
