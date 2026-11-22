using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Render;

/// <summary>
/// D298: the post-processing look. Over the 3D view (canvas layer -2, under every HUD layer) the colour grade,
/// the grain and the vignette (look_grade.gdshader), drawn only when one of them is not neutral; on the world's environment the
/// tonemapper, the dark distance fog and the bloom's strength and threshold (<see cref="ApplyEnvironment"/>).
/// </summary>
public partial class LookLayer : CanvasLayer
{
    private static Shader _shader;
    private ColorRect _rect;
    private ShaderMaterial _material;

    /// <summary>The scene's own bloom intensity (game.tscn), which the bloom setting scales.</summary>
    public const float BaseGlowIntensity = 0.9f;

    public override void _Ready()
    {
        Layer = -2;
        _material = new ShaderMaterial { Shader = _shader ??= GD.Load<Shader>("res://shaders/look_grade.gdshader") };
        _rect = new ColorRect { Name = "Grade", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    /// <summary>Sets the grade, the grain and the vignette from <paramref name="options"/>.</summary>
    public void Configure(PlayOptions options)
    {
        if (_rect == null)
            return;
        _rect.Visible = options.SaturationPercent != 100 || options.ContrastPercent != 100 || options.SplitTonePercent > 0
            || options.GrainPercent > 0 || options.VignettePercent > 0;
        _material.SetShaderParameter("saturation", options.SaturationPercent / 100f);
        _material.SetShaderParameter("contrast", options.ContrastPercent / 100f);
        _material.SetShaderParameter("split", options.SplitTonePercent / 100f);
        _material.SetShaderParameter("grain", options.GrainPercent / 100f);
        _material.SetShaderParameter("vignette", options.VignettePercent / 100f);
    }

    /// <summary>Sets the tonemapper, the fog and the bloom of <paramref name="environment"/> from <paramref name="options"/>.</summary>
    public static void ApplyEnvironment(Godot.Environment environment, PlayOptions options)
    {
        if (environment == null)
            return;
        environment.TonemapMode = options.Tonemap switch
        {
            1 => Godot.Environment.ToneMapper.Filmic,
            2 => Godot.Environment.ToneMapper.Agx,
            _ => Godot.Environment.ToneMapper.Linear,
        };
        // the fog fades the far geometry into the background's dark; the stars through the windows stay
        environment.FogEnabled = options.FogTiles > 0;
        environment.FogMode = Godot.Environment.FogModeEnum.Depth;
        environment.FogLightColor = environment.BackgroundColor;
        environment.FogLightEnergy = 1f;
        environment.FogDensity = 1f;
        environment.FogSkyAffect = 0f;
        environment.FogSunScatter = 0f;
        environment.FogAerialPerspective = 0f;
        environment.FogDepthCurve = 1f;
        environment.FogDepthBegin = PostLook.FogStart(options.FogTiles) * TileSpace.TileSize;
        environment.FogDepthEnd = options.FogTiles * TileSpace.TileSize;
        environment.GlowIntensity = BaseGlowIntensity * options.BloomPercent / 100f;
        environment.GlowHdrThreshold = options.BloomThresholdPercent / 100f;
    }
}
