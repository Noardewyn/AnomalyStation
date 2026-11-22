using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// D176 (the owner, 2026-10-22): tg's projectile sprites do not work in first person (a laser's flat sprite least of all), so the client draws its
// own 3D shots, procedurally, in tg's colours (ShotLook): a bolt is a stretched glowing capsule along its flight, a spark, an ion bolt or a
// temperature bolt a glowing ball, a bullet a thin streak, a hitscan tracer a glowing cylinder between its two ends; a shot with a light in tg
// carries a point light of tg's range, power and colour. The meshes and the material are shared by every shot (the colour and the strength are
// instance uniforms); the lights are unshadowed and at most MaxLights at once (the shadow budget, LightLayer, is not touched).

/// <summary>The 3D body of one shot, a child of its entity node.</summary>
public partial class ShotVisual : Node3D
{
    /// <summary>The most shot and flash lights lit at once (a burst of pellets or a full-auto stream does not light dozens).</summary>
    public const int MaxLights = 8;

    private const float HaloScale = 3f; // the halo's width to the body's

    private static int _lightsLit;
    private static ShaderMaterial _material;
    private static CapsuleMesh _capsule;
    private static SphereMesh _sphere;
    private static SphereMesh _mote;
    private static CylinderMesh _cylinder;

    private ShotKind _kind;
    private MeshInstance3D _body;
    private MeshInstance3D _halo;
    private OmniLight3D _light;
    private bool _holdsLight;
    private CpuParticles3D _trail;
    private Color _colour = new(-1f, -1f, -1f);
    private float _alpha = -1f;

    /// <summary>The shared material of the shots and the flashes' motes (<c>shot_glow.gdshader</c>).</summary>
    internal static ShaderMaterial Material => _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/shot_glow.gdshader") };

    /// <summary>Takes one of the <see cref="MaxLights"/>; false when they are all lit.</summary>
    internal static bool TakeLight()
    {
        if (_lightsLit >= MaxLights)
            return false;
        _lightsLit++;
        return true;
    }

    /// <summary>Gives back a light <see cref="TakeLight"/> gave.</summary>
    internal static void GiveLight() => _lightsLit = Math.Max(0, _lightsLit - 1);

    /// <summary>A shot of <paramref name="kind"/> under <paramref name="parent"/> (the entity's node).</summary>
    public static ShotVisual Create(Node3D parent, ShotKind kind)
    {
        var shot = new ShotVisual { Name = "Shot", _kind = kind };
        shot._body = new MeshInstance3D
        {
            Name = "Body",
            Mesh = MeshOf(kind),
            MaterialOverride = Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        shot._body.SetInstanceShaderParameter("look", kind switch { ShotKind.Spark => 1f, ShotKind.Ion => 2f, ShotKind.Temp => 3f, _ => 0f });
        shot.AddChild(shot._body);
        if (kind is not (ShotKind.Bullet or ShotKind.Pellet))
        {
            // a soft glow around an energy shot (a bullet's streak is only itself)
            shot._halo = new MeshInstance3D
            {
                Name = "Halo",
                Mesh = shot._body.Mesh,
                MaterialOverride = Material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            shot._halo.SetInstanceShaderParameter("look", 4f);
            shot.AddChild(shot._halo);
        }
        if (kind == ShotKind.Temp)
            shot.AddChild(shot._trail = Trail());
        parent.AddChild(shot);
        return shot;
    }

    private static Mesh MeshOf(ShotKind kind) => kind switch
    {
        ShotKind.Spark or ShotKind.Ion or ShotKind.Temp => _sphere ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 6 },
        ShotKind.Tracer => _cylinder ??= new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 1, CapTop = false, CapBottom = false },
        _ => _capsule ??= new CapsuleMesh { Radius = 0.5f, Height = 2f, RadialSegments = 12, Rings = 4 },
    };

    // The temperature bolt's trail: motes left behind in the world that shrink away.
    private static CpuParticles3D Trail()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f));
        curve.AddPoint(new Vector2(1f, 0f));
        return new CpuParticles3D
        {
            Name = "Trail",
            Amount = 24,
            Lifetime = 0.2,
            LocalCoords = false,
            Mesh = _mote ??= new SphereMesh { Radius = 0.02f, Height = 0.04f, RadialSegments = 6, Rings = 3 },
            MaterialOverride = Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.05f,
            Spread = 180f,
            InitialVelocityMin = 0.05f,
            InitialVelocityMax = 0.3f,
            Gravity = Vector3.Zero,
            ScaleAmountCurve = curve,
        };
    }

    /// <summary>
    /// Draws the shot: its middle (relative to its node), its way (a unit vector), its length in metres (a tracer's span; the projectiles take
    /// their own size, <see cref="ShotLook.Size"/>, unless <paramref name="ownSize"/> is false: a bolt stretched back to its gun), its colour, its
    /// alpha and tg's light of the shot (none: no light).
    /// </summary>
    /// <summary>The body's size beside its kind's own (<see cref="ShotLook.Calibre"/>).</summary>
    public float Calibre = 1f;

    public void Set(Color colour, Vector3 middle, Vector3 way, float length, float alpha, in LightState light, bool ownSize = true)
    {
        (float ownLength, float radius) = ShotLook.Size(_kind);
        (ownLength, radius) = (ownLength * Calibre, radius * Calibre);
        if (_kind != ShotKind.Tracer && ownSize)
            length = ownLength;
        if (colour != _colour)
        {
            _colour = colour;
            _body.SetInstanceShaderParameter("tint", colour);
            _halo?.SetInstanceShaderParameter("tint", colour);
            _trail?.SetInstanceShaderParameter("tint", colour);
        }
        if (alpha != _alpha)
        {
            _alpha = alpha;
            _body.SetInstanceShaderParameter("strength", alpha);
            _halo?.SetInstanceShaderParameter("strength", alpha);
        }
        Vector3 across = way.Cross(Mathf.Abs(way.Y) < 0.99f ? Vector3.Up : Vector3.Right).Normalized();
        Vector3 third = across.Cross(way);
        float d = 2f * radius;
        // the unit meshes: the capsule 1 m wide and 2 m long, the sphere and the cylinder 1 m
        float along = _kind switch { ShotKind.Spark or ShotKind.Ion or ShotKind.Temp => d, ShotKind.Tracer => Mathf.Max(length, 0.01f), _ => length / 2f };
        // the node stays unscaled (its light's range is its own); the body and its halo take the shot's size
        Position = middle;
        _body.Basis = new Basis(across * d, way * along, third * d);
        if (_halo != null)
            _halo.Basis = new Basis(across * d * HaloScale, way * along * (_kind == ShotKind.Tracer ? 1f : 1.2f), third * d * HaloScale);
        SetLight(light, colour);
    }

    private void SetLight(in LightState light, Color colour)
    {
        if (light.IsNone || _kind == ShotKind.Tracer)
            return;
        if (_light == null)
        {
            if (!TakeLight())
                return;
            _holdsLight = true;
            _light = new OmniLight3D { Name = "Light", ShadowEnabled = false, LightSpecular = 0.2f, LightVolumetricFogEnergy = 0f };
            AddChild(_light);
        }
        _light.OmniRange = Mathf.Max(light.Range, 1f) * TileSpace.TileSize;
        _light.LightEnergy = light.Power;
        _light.LightColor = colour;
    }

    public override void _ExitTree()
    {
        if (_holdsLight)
            GiveLight();
        _holdsLight = false;
    }
}
