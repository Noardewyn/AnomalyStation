using Godot;
using Anomaly.Client.Shared.Play;

namespace Anomaly.Client.Render;

// DV687 (protocol version 99): an atom's BYOND particles (tg's particle holders: a cigarette's smoke) drawn as Godot particles at the
// atom, from the set the server sends (ParticleSet): its icon state's picture on camera-facing quads, spawned at BYOND's rate, moving by
// its velocity, gravity and friction, growing by its grow and fading in and out over its fadein and fade.

/// <summary>A particle set's emitter, a child of the entity's node.</summary>
public partial class ParticleEmitter : CpuParticles3D
{
    /// <summary>The set's JSON this emitter was made from (a changed one makes a new emitter).</summary>
    public string Source { get; private set; }

    /// <summary>Makes the emitter of <paramref name="set"/> under <paramref name="parent"/>, its picture <paramref name="picture"/> (null: a soft square).</summary>
    public static ParticleEmitter Create(Node3D parent, string source, ParticleSet set, Texture2D picture)
    {
        var v = set.VelocityMetres;
        var g = set.GravityMetres;
        var p = set.PositionMetres;
        Vector3 velocity = new((float)v.X, (float)v.Y, (float)v.Z);
        float pixels = picture != null ? Mathf.Max(picture.GetWidth(), picture.GetHeight()) : 8;
        float size = (float)(pixels * ParticleSet.PixelMetres);
        Color tint = new((set.Rgba >> 24 & 0xFF) / 255f, (set.Rgba >> 16 & 0xFF) / 255f, (set.Rgba >> 8 & 0xFF) / 255f, 1f);
        var emitter = new ParticleEmitter
        {
            Source = source,
            Amount = set.Amount,
            Lifetime = set.LifetimeSeconds,
            Emitting = set.Emits,
            LocalCoords = false,
            Position = new Vector3((float)p.X, (float)p.Y, (float)p.Z),
            Mesh = new QuadMesh { Size = new Vector2(size, size) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                VertexColorUseAsAlbedo = true,
                AlbedoTexture = picture,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            EmissionShape = EmissionShapeEnum.Point,
            Direction = velocity.LengthSquared() > 0 ? velocity.Normalized() : Vector3.Up,
            Spread = 0f,
            InitialVelocityMin = velocity.Length(),
            InitialVelocityMax = velocity.Length(),
            Gravity = new Vector3((float)g.X, (float)g.Y, (float)g.Z),
            DampingMin = (float)set.DampingMetres,
            DampingMax = (float)set.DampingMetres,
            AngleMin = (float)set.Rotation,
            AngleMax = (float)set.Rotation,
            AngularVelocityMin = (float)(set.Spin / ParticleSet.TickSeconds),
            AngularVelocityMax = (float)(set.Spin / ParticleSet.TickSeconds),
            ScaleAmountMin = (float)set.StartScale,
            ScaleAmountMax = (float)set.StartScale,
            ScaleAmountCurve = Growth(set.EndScaleRatio),
            Color = tint,
            ColorRamp = Fading(set),
        };
        parent.AddChild(emitter);
        return emitter;
    }

    // BYOND's grow: the scale over the particle's life, a straight line from its start to its end
    private static Curve Growth(double endRatio)
    {
        float end = (float)Math.Max(0, endRatio);
        var curve = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, end) };
        curve.AddPoint(new Vector2(0f, 1f));
        curve.AddPoint(new Vector2(1f, end));
        return curve;
    }

    // BYOND's fadein and fade with the colour's alpha, sampled along the life
    private static Gradient Fading(ParticleSet set)
    {
        var gradient = new Gradient();
        const int samples = 8;
        var offsets = new float[samples + 1];
        var colors = new Color[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            float life = i / (float)samples;
            offsets[i] = life;
            colors[i] = new Color(1f, 1f, 1f, (float)set.AlphaAt(life));
        }
        gradient.Offsets = offsets;
        gradient.Colors = colors;
        return gradient;
    }
}
