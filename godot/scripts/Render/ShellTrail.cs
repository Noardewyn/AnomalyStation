using Godot;

namespace Anomaly.Client.Render;

// The owner (2026-11-21): a launcher's shell (a 40mm grenade, a rocket; ShotLook.IsShell) flies with a fire trail behind it. tg draws none: its
// shell is a sprite on the map. The trail is the client's own: pixel puffs left in the world behind the shell, burning from yellow through orange
// and red to grey smoke, growing as they fade.

/// <summary>A shell's fire trail: puffs emitted where the shell is drawn, left behind in the world.</summary>
public partial class ShellTrail : CpuParticles3D
{
    private static QuadMesh _puff;
    private static StandardMaterial3D _material;
    private static Gradient _burn;
    private static Curve _grow;

    /// <summary>A trail under <paramref name="parent"/> (the shell's node).</summary>
    public static ShellTrail Create(Node3D parent)
    {
        var trail = new ShellTrail
        {
            Name = "ShellTrail",
            Amount = 90,
            Lifetime = 0.7,
            LocalCoords = false,
            Mesh = _puff ??= new QuadMesh { Size = new Vector2(0.22f, 0.22f) },
            MaterialOverride = _material ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                VertexColorUseAsAlbedo = true,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            EmissionShape = EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.06f,
            Spread = 25f,
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 0.8f,
            Gravity = new Vector3(0f, 0.25f, 0f), // the smoke rises a little
            DampingMin = 0.5f,
            DampingMax = 1f,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.3f,
            ScaleAmountCurve = _grow ??= Grow(),
            ColorRamp = _burn ??= Burn(),
            Emitting = false,
        };
        parent.AddChild(trail);
        return trail;
    }

    /// <summary>Where the shell is drawn (relative to the node) and the way it flies: the puffs leave it backwards; <paramref name="shown"/> false stops them.</summary>
    public void Follow(Vector3 at, Vector3 way, bool shown)
    {
        Position = at;
        if (way.LengthSquared() > 1e-6f)
            Direction = -way.Normalized();
        if (Emitting != shown)
            Emitting = shown;
    }

    private static Gradient Burn()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 0.95f, 0.6f, 1f));
        g.SetOffset(0, 0f);
        g.SetColor(1, new Color(0.35f, 0.33f, 0.32f, 0f));
        g.SetOffset(1, 1f);
        g.AddPoint(0.15f, new Color(1f, 0.7f, 0.15f, 1f));
        g.AddPoint(0.35f, new Color(0.9f, 0.3f, 0.08f, 0.9f));
        g.AddPoint(0.55f, new Color(0.45f, 0.4f, 0.38f, 0.6f));
        return g;
    }

    private static Curve Grow()
    {
        var c = new Curve { MaxValue = 2f };
        c.AddPoint(new Vector2(0f, 0.5f));
        c.AddPoint(new Vector2(1f, 1.8f));
        return c;
    }
}
