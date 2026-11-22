using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// D285: a shot's tracer (a bullet's; since 2026-11-11 every projectile's, ShotLook.Trail), drawn the way shooters draw theirs: a world effect of its own (not a child of the bullet's node), a ribbon that turns
// to face the eye about its line (shot_trace.gdshader) and keeps a least on-screen width. It stretches from the gun's muzzle to the bullet's head
// while the bullet flies (an own shot flies straight away from the eye, so only the line from the muzzle, off to the side, shows its way), and
// when the bullet's node goes (it often lives only a few frames at DV211's speed) its tail runs up to the last head as it fades.

/// <summary>One bullet's tracer in the world.</summary>
public partial class ShotTrace : MeshInstance3D
{
    private static ShaderMaterial _material;
    private static ArrayMesh _ribbon;

    private Node3D _bullet;
    private Vector3 _origin, _head, _tail;
    private Vector3 _start, _ahead, _last; // the shot's true line (from the eye, not the muzzle) and the farthest place on it the server named
    private bool _seen, _landed;
    private static readonly List<ShotTrace> Live = new();
    private float _dying = -1f;
    private float _fadeSeconds;

    /// <summary>
    /// A tracer of the shot whose node is <paramref name="bullet"/>, leaving <paramref name="origin"/> (the muzzle, world space), <paramref name="width"/>
    /// metres wide, that dies over <paramref name="fadeSeconds"/> after its shot (ShotLook.Trail). The shot's true line starts at
    /// <paramref name="start"/> and runs along <paramref name="ahead"/> (a unit vector): a hit is matched against it (<see cref="Land"/>).
    /// </summary>
    public static ShotTrace Create(Node parent, Node3D bullet, Vector3 origin, Vector3 start, Vector3 ahead, Color colour, float width, float fadeSeconds)
    {
        var trace = new ShotTrace
        {
            Name = "Trace",
            Mesh = _ribbon ??= Ribbon(),
            MaterialOverride = _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/shot_trace.gdshader") },
            CastShadow = ShadowCastingSetting.Off,
            ExtraCullMargin = 1f, // the shader turns the flat ribbon to the eye, out of its own bounds
            Visible = false,
            _bullet = bullet,
            _origin = origin,
            _start = start,
            _ahead = ahead,
            _last = start,
            _fadeSeconds = fadeSeconds,
        };
        trace.SetInstanceShaderParameter("tint", colour);
        trace.SetInstanceShaderParameter("width", width);
        parent.AddChild(trace);
        Live.Add(trace);
        return trace;
    }

    public override void _ExitTree() => Live.Remove(this);

    /// <summary>
    /// A shot hit at <paramref name="at"/> (world space): the tracer whose line leads there ends there, not at its shot's last seen place (a fast
    /// shot is last seen a metre or more short of what it hit).
    /// </summary>
    public static void Land(Vector3 at)
    {
        // The newest first (a burst's hits come in its shots' order, each to the last tracer made). The hit is matched against the shot's true
        // line: the drawn one, from the muzzle to a head a metre out, points well off it and missed a hit some metres on (the tracer stayed short).
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            ShotTrace trace = Live[i];
            Vector3 to = at - trace._start;
            float along = to.Dot(trace._ahead);
            if (!trace._seen || trace._landed || along < 0.05f || (to - trace._ahead * along).Length() > LandReach)
                continue;
            trace._head = at;
            trace._landed = true;
            if (trace._dying < 0f)
                trace.Place(0f);
            return;
        }
    }

    private const float LandReach = 0.6f; // the hit point is a little off the line (tg's impact pixel is random on the hit tile)

    // A unit ribbon: x across (-0.5..0.5), y along from the tail (0) to the head (1), a few rows so the width follows the distance along it.
    private static ArrayMesh Ribbon()
    {
        const int rows = 8;
        var verts = new Vector3[(rows + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var index = new int[rows * 6];
        for (int i = 0; i <= rows; i++)
        {
            float y = i / (float)rows;
            verts[i * 2] = new Vector3(-0.5f, y, 0f);
            verts[i * 2 + 1] = new Vector3(0.5f, y, 0f);
            uvs[i * 2] = new Vector2(0f, y);
            uvs[i * 2 + 1] = new Vector2(1f, y);
            if (i == rows)
                break;
            int a = i * 2;
            index[i * 6] = a;
            index[i * 6 + 1] = a + 1;
            index[i * 6 + 2] = a + 2;
            index[i * 6 + 3] = a + 1;
            index[i * 6 + 4] = a + 3;
            index[i * 6 + 5] = a + 2;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = index;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>
    /// The bullet's head is now at <paramref name="head"/> (world space): the tracer runs back from it to the muzzle. <paramref name="last"/> is
    /// the place on the line of the tile the server last named: the drawn head glides behind it, so a shot that goes with no hit effect (one
    /// that hit a body) ends there, not short of it.
    /// </summary>
    public void Follow(Vector3 head, Vector3 last)
    {
        if (_dying >= 0f || _landed)
            return;
        _last = last;
        _head = head;
        _tail = _origin;
        _seen = true;
        Place(0f);
    }

    /// <summary>The shot turned at <paramref name="at"/> (a ricochet): this tracer ends there and dies now; the shot's way on has a tracer of its own.</summary>
    public void Finish(Vector3 at)
    {
        if (_seen && !_landed)
            _head = at;
        _landed = true;
        _bullet = null;
    }

    public override void _Process(double delta)
    {
        if (_dying < 0f)
        {
            if (IsInstanceValid(_bullet) && _bullet.IsInsideTree())
                return;
            _dying = 0f;
            if (!_seen)
            {
                QueueFree();
                return;
            }
            if (!_landed && (_last - _start).Dot(_ahead) > (_head - _start).Dot(_ahead))
                _head = _last;
        }
        _dying += (float)delta;
        float t = _dying / _fadeSeconds;
        if (t >= 1f)
        {
            QueueFree();
            return;
        }
        Place(t); // the tail dissolves up to the head (the shader's `gone`); the ribbon itself stays where the shot flew
    }

    // The node runs from the tail (its origin) to the head (its +y); its x and z stay a metre (the ribbon's bounds; the shader sets the width).
    private void Place(float gone)
    {
        Vector3 span = _head - _tail;
        float length = span.Length();
        if (length > ShotLook.TrailMaxMetres)
            (span, length) = (span * (ShotLook.TrailMaxMetres / length), ShotLook.TrailMaxMetres);
        Visible = length > 0.05f;
        if (!Visible)
            return;
        Vector3 along = span / length;
        Vector3 across = along.Cross(Mathf.Abs(along.Y) < 0.99f ? Vector3.Up : Vector3.Right).Normalized();
        GlobalTransform = new Transform3D(new Basis(across, span, across.Cross(along)), _head - span);
        SetInstanceShaderParameter("gone", gone);
    }
}
