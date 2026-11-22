using Godot;
using Anomaly.Client.Shared.Play;

namespace Anomaly.Client.Render;

// The owner's play test (2026-10-09): the hit effect. Where tg shows its attack visual on the attacked atom (EntityFlags.Hit for
// one tick, HitEffectSeam on the server), the client throws a short burst of tg-pixel-sized cubes from the picture,
// coloured and thrown by what the thing is made of (HitMaterials, the owner 2026-10-29).

/// <summary>A one-shot burst of pixel particles that frees itself when it is done.</summary>
public partial class HitBurst : CpuParticles3D
{
    /// <summary>One tg pixel at 2 m tiles (2 / 32 m).</summary>
    public const float PixelMetres = 2f / 32f;

    // shared by every burst (D176: a burst of every impact)
    private static BoxMesh _mesh;
    private static StandardMaterial3D _material;
    private static Curve _shrink;

    /// <summary>Starts a material's burst (HitMaterials.Look) at <paramref name="at"/> (world space) under <paramref name="parent"/>, with its sparks.</summary>
    public static void Spawn(Node parent, Vector3 at, HitDebris look)
    {
        Spawn(parent, at, new Color(look.R, look.G, look.B), look.Amount, look.SpeedMin, look.SpeedMax, look.Spread, look.Lifetime, look.Gravity, 1f);
        if (look.Sparks > 0)
            Sparks(parent, at, new Color(HitMaterials.Spark.R, HitMaterials.Spark.G, HitMaterials.Spark.B), look.Sparks);
    }

    /// <summary>Bright sparks: fast, small and short-lived.</summary>
    public static void Sparks(Node parent, Vector3 at, Color color, int amount)
        => Spawn(parent, at, color, amount, 2.6f, 4.2f, 80f, 0.22f, 6f, 0.5f);

    private static void Spawn(Node parent, Vector3 at, Color color, int amount, float speedMin, float speedMax, float spread, float lifetime, float gravity, float scale)
    {
        var burst = new HitBurst
        {
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 1f,
            Emitting = false,
            Mesh = _mesh ??= new BoxMesh { Size = new Vector3(PixelMetres, PixelMetres, PixelMetres) },
            MaterialOverride = _material ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                AlbedoColor = Colors.White,
            },
            EmissionShape = EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.12f,
            Direction = Vector3.Up,
            Spread = spread,
            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector3(0, -gravity, 0),
            ScaleAmountMin = 0.7f * scale,
            ScaleAmountMax = 1.3f * scale,
            ScaleAmountCurve = _shrink ??= Shrink(),
            Color = color,
            HueVariationMin = -0.02f,
            HueVariationMax = 0.02f,
        };
        parent.AddChild(burst);
        burst.GlobalPosition = at;
        burst.Emitting = true;
        burst.Finished += burst.QueueFree;
    }

    // Full size for the first half of a particle's life, then down to nothing.
    private static Curve Shrink()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f));
        curve.AddPoint(new Vector2(0.5f, 1f));
        curve.AddPoint(new Vector2(1f, 0f));
        return curve;
    }
}
