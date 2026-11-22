using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// D176: a shot's flash in the world, once, where tg shows a muzzle or an impact effect: tg's firing effect (temp_visual/dir_setting/firing_effect,
// miscellaneous.dm:60-93, 3 deciseconds) and a hitscan's muzzle (/obj/effect/projectile/muzzle) are a star turned to the eye and a short light;
// an impact (temp_visual/impact_effect, /obj/effect/projectile/impact) a short light by HitBurst's burst. Both fade out and free themselves.

/// <summary>A short-lived star and light (<see cref="ShotVisual.MaxLights"/> counts the light).</summary>
public partial class ShotFlash : Node3D
{
    private static ShaderMaterial _starMaterial;
    private static QuadMesh _quad;

    private MeshInstance3D _star;
    private OmniLight3D _light;
    private float _energy;
    private double _age;
    private double _life;

    /// <summary>
    /// A flash at <paramref name="at"/> (world space) under <paramref name="parent"/>: a star <paramref name="starMetres"/> across (0: none) and a light
    /// of <paramref name="lightMetres"/> range and <paramref name="lightEnergy"/> (0: none), in <paramref name="colour"/>, over <paramref name="life"/> seconds.
    /// </summary>
    public static ShotFlash Spawn(Node parent, Vector3 at, Color colour, float starMetres, float lightMetres, float lightEnergy, double life)
    {
        var flash = new ShotFlash { Name = "ShotFlash", _life = life, _energy = lightEnergy };
        if (starMetres > 0f)
        {
            _starMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/shot_star.gdshader") };
            flash._star = new MeshInstance3D
            {
                Mesh = _quad ??= new QuadMesh { Size = Vector2.One },
                MaterialOverride = _starMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Scale = new Vector3(starMetres, starMetres, 1f),
            };
            flash._star.SetInstanceShaderParameter("tint", colour);
            flash._star.SetInstanceShaderParameter("turn", (float)GD.RandRange(0.0, Math.PI / 2));
            flash.AddChild(flash._star);
        }
        if (lightMetres > 0f && lightEnergy > 0f && ShotVisual.TakeLight())
        {
            flash._light = new OmniLight3D
            {
                ShadowEnabled = false,
                LightSpecular = 0.2f,
                LightVolumetricFogEnergy = 0f,
                OmniRange = lightMetres,
                LightEnergy = lightEnergy,
                LightColor = colour,
            };
            flash.AddChild(flash._light);
        }
        parent.AddChild(flash);
        flash.GlobalPosition = at;
        return flash;
    }

    /// <summary>The flash in another colour (D285: the colour of the shot it fired, which came after it).</summary>
    public void Recolour(Color colour)
    {
        _star?.SetInstanceShaderParameter("tint", colour);
        if (_light != null)
            _light.LightColor = colour;
    }

    public override void _Process(double delta)
    {
        _age += delta;
        float f = ShotLook.Fade(_age, _life);
        if (f <= 0f)
        {
            QueueFree();
            return;
        }
        _star?.SetInstanceShaderParameter("strength", f);
        if (_light != null)
            _light.LightEnergy = _energy * f;
    }

    public override void _ExitTree()
    {
        if (_light != null)
            ShotVisual.GiveLight();
        _light = null;
    }
}
