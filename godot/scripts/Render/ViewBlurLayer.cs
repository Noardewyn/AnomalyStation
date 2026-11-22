using System;
using System.Collections.Generic;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;
using ViewBlur = Anomaly.Client.Shared.ViewBlur;

namespace Anomaly.Client.Render;

// Protocol version 29 (the eye blur, protocol.md section 38): tg blurs the whole game view by a gaussian blur filter on the hud's
// game plane master controller (screen_blur.dm); the 3D client has no planes, so the replica's game plane filters
// (ReplicaWorld.GamePlaneFilters) are drawn as a screen-filling blur over the 3D view, under the HUD, the windows and the crosshair
// (canvas layer 1; the HUD is 2 and up). Version 31 adds the plane's colour matrix filters (a client colour: the mesons' tint). Nothing is
// drawn while the plane has neither.

/// <summary>The view's blur for the game plane's filters, read from the replica every frame.</summary>
public partial class ViewBlurLayer : CanvasLayer
{
    private static Shader _shader;
    private ColorRect _rect;
    private ShaderMaterial _material;

    /// <summary>The standard deviation drawn now, in screen pixels (0: none).</summary>
    public float Sigma { get; private set; }

    /// <summary>The colour matrix drawn now (20 numbers), or null.</summary>
    public float[] Tint { get; private set; }

    public override void _Ready()
    {
        Layer = 1;
        _material = new ShaderMaterial { Shader = _shader ??= GD.Load<Shader>("res://shaders/view_blur.gdshader") };
        _rect = new ColorRect { Name = "Blur", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    /// <summary>The colour steps drawn now (gaps RG3: each in its colour space), empty for none.</summary>
    public IReadOnlyList<(float[] Matrix, byte Space)> Steps { get; private set; } = Array.Empty<(float[], byte)>();

    /// <summary>The waves drawn now (gaps RG3), empty for none.</summary>
    public IReadOnlyList<ViewBlur.ViewWave> Waves { get; private set; } = Array.Empty<ViewBlur.ViewWave>();

    /// <summary>The radial blur's size drawn now (gaps RG3), 0 for none.</summary>
    public float RadialSize { get; private set; }

    /// <summary>Follows the game plane of <paramref name="replica"/> (null: none).</summary>
    public void Update(ReplicaWorld replica)
    {
        if (_rect == null)
            return;
        float height = GetViewport()?.GetVisibleRect().Size.Y ?? 0f;
        IReadOnlyList<FilterEntry> filters = replica?.GamePlaneFilters;
        float sigma = ViewBlur.Sigma(filters, height);
        Sigma = sigma;
        // protocol version 31 (DV81): the game plane's colour matrices (tg's client colours: the mesons' light green) tint the view as
        // well; gaps RG3: the drugs' colour filters in other colour spaces, their waves and radial blur
        Tint = ViewBlur.ColorMatrix(filters);
        IReadOnlyList<(float[] Matrix, byte Space)> steps = ViewBlur.ColorSteps(filters);
        IReadOnlyList<ViewBlur.ViewWave> waves = ViewBlur.Waves(filters, height);
        (float radialSize, float radialX, float radialY, float radialOffset) = ViewBlur.Radial(filters, height);
        Steps = steps;
        Waves = waves;
        RadialSize = radialSize;
        _rect.Visible = sigma > 0f || steps.Count > 0 || waves.Count > 0 || radialSize > 0f;
        if (!_rect.Visible)
            return;
        _material.SetShaderParameter("sigma", sigma);
        var rows = new Vector4[4 * ViewBlur.MaxColorSteps];
        var spaces = new int[ViewBlur.MaxColorSteps];
        for (int i = 0; i < steps.Count; i++)
        {
            float[] m = steps[i].Matrix;
            rows[i * 4] = new Vector4(m[0], m[1], m[2], 0f);
            rows[i * 4 + 1] = new Vector4(m[4], m[5], m[6], 0f);
            rows[i * 4 + 2] = new Vector4(m[8], m[9], m[10], 0f);
            rows[i * 4 + 3] = new Vector4(m[12] + m[16], m[13] + m[17], m[14] + m[18], 0f); // alpha 1: the alpha row is constant
            spaces[i] = steps[i].Space;
        }
        _material.SetShaderParameter("color_steps", steps.Count);
        _material.SetShaderParameter("color_rows", rows);
        _material.SetShaderParameter("color_space", spaces);
        var waveA = new Vector4[ViewBlur.MaxWaves];
        var waveB = new Vector4[ViewBlur.MaxWaves];
        for (int i = 0; i < waves.Count; i++)
        {
            ViewBlur.ViewWave w = waves[i];
            waveA[i] = new Vector4(w.DirX, w.DirY, w.Period, w.Amplitude);
            waveB[i] = new Vector4(w.Phase, w.Sideways ? 1f : 0f, 0f, 0f);
        }
        _material.SetShaderParameter("wave_count", waves.Count);
        _material.SetShaderParameter("wave_a", waveA);
        _material.SetShaderParameter("wave_b", waveB);
        _material.SetShaderParameter("radial_size", radialSize);
        _material.SetShaderParameter("radial_centre", new Vector2(radialX, radialY));
        _material.SetShaderParameter("radial_offset", radialOffset);
    }
}
