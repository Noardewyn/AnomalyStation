using Godot;

namespace Anomaly.Client.Render;

// tg draws its explosion (/obj/effect/explosion, effects_explosion.dm) as a 96-pixel sprite on the epicentre for a second. A flat sprite reads as paper in first person (D303),
// so the client draws the same second as a burst of pixel puffs (shaders/blast_puff.gdshader) that reaches as far as tg's sprite.

/// <summary>An explosion's second: a shared mesh of puffs, its age the only state.</summary>
public partial class BlastVolume : MeshInstance3D
{
    /// <summary>tg's explosion.</summary>
    public const string BlastType = Anomaly.Client.Shared.World.StaticPropRule.BlastType;

    private const int Puffs = 26;
    private const float Reach = 3f; // metres: half of tg's 96 pixels on 2 m tiles
    private const double Seconds = 1.0; // QDEL_IN(src, 10)
    private const double FrameSeconds = 0.1; // tg's 10 frames a second

    private static ArrayMesh _mesh;
    private static ShaderMaterial _material;

    private double _age;
    private double _shownAge = -1;

    /// <summary>Makes the burst under <paramref name="parent"/> (the explosion's node, at the epicentre's floor; the shader lifts it to the storey's middle).</summary>
    public static BlastVolume Create(Node3D parent)
    {
        var blast = new BlastVolume
        {
            Name = "Blast",
            Mesh = _mesh ??= Burst(),
            MaterialOverride = _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/blast_puff.gdshader") },
            CastShadow = ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-6, -1, -6), new Vector3(12, 9, 12)), // the shader throws the puffs out
        };
        parent.AddChild(blast);
        return blast;
    }

    /// <summary>The blast's size, from tg's transform.</summary>
    public void SetScale(float scale) => SetInstanceShaderParameter("blast_scale", scale);

    /// <summary>DV578: how high over the floor its middle is, metres (a storey's middle, 1 m, unless it went off under the ceiling).</summary>
    public void SetLift(float metres) => SetInstanceShaderParameter("lift", metres);

    public override void _Process(double delta)
    {
        _age += delta;
        double stepped = Mathf.Floor(_age / FrameSeconds) * FrameSeconds;
        if (stepped == _shownAge)
            return;
        _shownAge = stepped;
        SetInstanceShaderParameter("life", (float)Mathf.Min(stepped / Seconds, 1.0));
    }

    // Each puff a quad whose corners all lie on where it is thrown to (the shader spreads them by UV): red numbers it, green is its size over 3 m.
    private static ArrayMesh Burst()
    {
        var random = new RandomNumberGenerator { Seed = 11 };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 at = Vector3.Zero;
        for (int puff = 0; puff < Puffs; puff++)
        {
            // every other puff mirrors the one before across the epicentre, so the burst is centred on it (the owner, 2026-11-01)
            if (puff % 2 == 0)
            {
                Vector3 way = new Vector3(random.RandfRange(-1f, 1f), random.RandfRange(-0.35f, 0.9f), random.RandfRange(-1f, 1f)).Normalized();
                at = way * random.RandfRange(0.2f, 1f) * Reach;
            }
            else
                at = -at;
            var color = new Color(puff / 255f, random.RandfRange(0.5f, 1f), 0f);
            foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) })
            {
                surface.SetColor(color);
                surface.SetUV(corner);
                surface.AddVertex(at);
            }
        }
        return surface.Commit();
    }
}
