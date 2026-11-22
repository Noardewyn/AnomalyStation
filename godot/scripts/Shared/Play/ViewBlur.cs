using System;
using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Protocol version 29 (the eye blur, protocol.md section 38). Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). tg blurs a player's whole game view by a gaussian blur filter on its hud's game plane master
// controller (screen_blur.dm: size 0.6 to 3 BYOND pixels, more the longer the eye blur lasts); the replica carries that plane's
// filters (ReplicaWorld.GamePlaneFilters), and the client draws the blur over its 3D view under the HUD (Render/ViewBlurLayer).

/// <summary>How strongly the client blurs its view for the game plane's filters.</summary>
public static class ViewBlur
{
    /// <summary>The height of BYOND's map view in its pixels: tg's view is 15 tiles high (the widescreen 19x15), 32 pixels a tile.</summary>
    public const float ByondViewPixels = 15 * 32;

    /// <summary>
    /// The standard deviation of the view's gaussian blur, in screen pixels of a view <paramref name="viewHeight"/> pixels high, for the
    /// game plane's <paramref name="filters"/>: the blur filters' sizes in BYOND pixels (D156: a blur of size s is a gaussian of standard
    /// deviation s / 2; two blurs add as variances), scaled from BYOND's 480-pixel-high view to this one. 0 for no blur.
    /// </summary>
    public static float Sigma(IReadOnlyList<FilterEntry> filters, float viewHeight)
    {
        if (filters == null || filters.Count == 0 || !(viewHeight > 0))
            return 0f;
        double variance = 0;
        foreach (FilterEntry f in filters)
        {
            if (f == null || f.Kind != FilterKind.Blur || !(f.Size > 0))
                continue;
            double sigma = f.Size / 2.0;
            variance += sigma * sigma;
        }
        if (variance <= 0)
            return 0f;
        return (float)(Math.Sqrt(variance) * viewHeight / ByondViewPixels);
    }

    /// <summary>
    /// Protocol version 31 (DV81, the mesons' tint): the colour matrix the game plane's colour filters make together, in the order tg
    /// lists them (tg's client colours, <c>/mob/proc/update_client_colour</c>, add one colour matrix filter per priority group to the
    /// game plane): a 4 x 5 matrix of 20 numbers laid out as BYOND's <c>color</c> list (rows <c>rr rg rb ra, gr gg gb ga, br bg bb ba, ar ag ab aa,
    /// cr cg cb ca</c>; an output channel is the input times its column plus the constant row). Null for none.
    /// Only the RGB ones (<c>space</c> FILTER_COLOR_RGB): <see cref="ColorSteps"/> keeps the others (gaps RG3).
    /// </summary>
    public static float[] ColorMatrix(IReadOnlyList<FilterEntry> filters)
    {
        float[] total = null;
        if (filters == null)
            return null;
        foreach (FilterEntry f in filters)
        {
            if (f == null || f.Kind != FilterKind.Color || f.ColorMatrix == null || f.ColorMatrix.Length != 20 || f.Space != 0)
                continue;
            total = total == null ? (float[])f.ColorMatrix.Clone() : Then(total, f.ColorMatrix);
        }
        return total;
    }

    /// <summary>The matrix that applies <paramref name="first"/> and then <paramref name="second"/>.</summary>
    public static float[] Then(float[] first, float[] second)
    {
        var m = new float[20];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                float sum = 0;
                for (int k = 0; k < 4; k++)
                    sum += first[i * 4 + k] * second[k * 4 + j];
                m[i * 4 + j] = sum;
            }
        for (int j = 0; j < 4; j++)
        {
            float sum = second[16 + j];
            for (int k = 0; k < 4; k++)
                sum += first[16 + k] * second[k * 4 + j];
            m[16 + j] = sum;
        }
        return m;
    }

    /// <summary>The most colour steps the view draws (Render/ViewBlurLayer, shaders/view_blur.gdshader).</summary>
    public const int MaxColorSteps = 4;

    /// <summary>The most waves the view draws.</summary>
    public const int MaxWaves = 2;

    /// <summary>
    /// Gaps RG3 (the drugs' trips): the game plane's colour filters in tg's order as the view draws them, each a 4 x 5 matrix and the
    /// colour space it works in (BYOND <c>space</c>: 0 RGB, 1 HSV, 2 HSL, 3 HCY; the mushroom hallucinogen's hue turn is an HSL matrix,
    /// blastoff's and saturnX's tints HCY ones): a pixel is taken to the space, through the matrix and back (the icons' colour filters
    /// do the same, AppearanceCompositor.SpaceMatrix). Neighbouring RGB matrices are one step; at most <see cref="MaxColorSteps"/> steps
    /// (the later ones dropped). Empty for none.
    /// </summary>
    public static IReadOnlyList<(float[] Matrix, byte Space)> ColorSteps(IReadOnlyList<FilterEntry> filters)
    {
        var steps = new List<(float[] Matrix, byte Space)>();
        if (filters == null)
            return steps;
        foreach (FilterEntry f in filters)
        {
            if (f == null || f.Kind != FilterKind.Color || f.ColorMatrix == null || f.ColorMatrix.Length != 20)
                continue;
            byte space = f.Space <= 3 ? f.Space : (byte)0;
            if (space == 0 && steps.Count > 0 && steps[^1].Space == 0)
            {
                steps[^1] = (Then(steps[^1].Matrix, f.ColorMatrix), 0);
                continue;
            }
            if (steps.Count == MaxColorSteps)
                break;
            steps.Add(((float[])f.ColorMatrix.Clone(), space));
        }
        return steps;
    }

    /// <summary>A colour (each channel 0 to 1, alpha 1) through the colour <paramref name="steps"/>: the colour a pixel gets.</summary>
    public static (float R, float G, float B) ApplySteps(IReadOnlyList<(float[] Matrix, byte Space)> steps, float r, float g, float b)
    {
        if (steps == null)
            return (r, g, b);
        foreach ((float[] m, byte space) in steps)
        {
            if (space == 0)
            {
                (r, g, b) = Apply(m, r, g, b);
                continue;
            }
            (float p, float q, float t) = Anomaly.Client.Shared.Icons.AppearanceCompositor.ToSpace(r, g, b, space);
            float np = p * m[0] + q * m[4] + t * m[8] + m[12] + m[16];
            float nq = p * m[1] + q * m[5] + t * m[9] + m[13] + m[17];
            float nt = p * m[2] + q * m[6] + t * m[10] + m[14] + m[18];
            (r, g, b) = Anomaly.Client.Shared.Icons.AppearanceCompositor.FromSpace(np, nq, nt, space);
        }
        return (r, g, b);
    }

    /// <summary>A wave over the view, in screen pixels: its direction (x right, y up), its period, its amplitude, its phase, and
    /// whether it pushes across its direction (<c>WAVE_SIDEWAYS</c>).</summary>
    public readonly record struct ViewWave(float DirX, float DirY, float Period, float Amplitude, float Phase, bool Sideways);

    /// <summary>
    /// Gaps RG3 (the mushroom hallucinogen's and blastoff's waves): the game plane's wave filters (BYOND "wave": the picture pushed by
    /// size x sin(2 pi (d - offset)), d the periods from the centre along x, y, the period their length) scaled from BYOND's
    /// 480-pixel-high view to the client's <paramref name="viewHeight"/>, centred on the view; at most <see cref="MaxWaves"/>.
    /// </summary>
    public static IReadOnlyList<ViewWave> Waves(IReadOnlyList<FilterEntry> filters, float viewHeight)
    {
        var waves = new List<ViewWave>();
        if (filters == null || !(viewHeight > 0))
            return waves;
        float scale = viewHeight / ByondViewPixels;
        foreach (FilterEntry f in filters)
        {
            if (f == null || f.Kind != FilterKind.Wave)
                continue;
            float period = MathF.Sqrt(f.X * f.X + f.Y * f.Y);
            if (Math.Abs(f.Size) < 0.001f || period < 0.001f)
                continue;
            if (waves.Count == MaxWaves)
                break;
            waves.Add(new ViewWave(f.X / period, f.Y / period, period * scale, f.Size * scale, f.Offset, (f.Flags & FilterFlags.WaveSideways) != 0));
        }
        return waves;
    }

    /// <summary>How far a point of the view, <paramref name="x"/> right and <paramref name="y"/> up from its centre in screen pixels,
    /// reads its colour from: the shader's sum of the waves' pushes.</summary>
    public static (float X, float Y) WavePush(IReadOnlyList<ViewWave> waves, float x, float y)
    {
        float dx = 0f, dy = 0f;
        if (waves == null)
            return (0f, 0f);
        foreach (ViewWave w in waves)
        {
            float along = (x * w.DirX + y * w.DirY) / w.Period;
            float amount = w.Amplitude * MathF.Sin(2f * MathF.PI * (along - w.Phase));
            dx += (w.Sideways ? -w.DirY : w.DirX) * amount;
            dy += (w.Sideways ? w.DirX : w.DirY) * amount;
        }
        return (dx, dy);
    }

    /// <summary>
    /// Gaps RG3 (saturnX's blur): the game plane's radial blur (BYOND "radial_blur": outward from the centre, <c>size</c> per pixel
    /// of distance past <c>offset</c>; the icons' AppearanceCompositor.RadialBlur): its size, its centre's offset from the view's
    /// centre (x right, y up) and the radius left sharp, in screen pixels. The sizes of several add; a size of 0 is none.
    /// </summary>
    public static (float Size, float CentreX, float CentreY, float Offset) Radial(IReadOnlyList<FilterEntry> filters, float viewHeight)
    {
        if (filters == null || !(viewHeight > 0))
            return (0f, 0f, 0f, 0f);
        float scale = viewHeight / ByondViewPixels;
        float size = 0f, cx = 0f, cy = 0f, offset = 0f;
        foreach (FilterEntry f in filters)
        {
            if (f == null || f.Kind != FilterKind.RadialBlur || Math.Abs(f.Size) < 0.00001f)
                continue;
            if (size == 0f)
            {
                cx = f.X * scale;
                cy = f.Y * scale;
                offset = f.Offset * scale;
            }
            size += Math.Abs(f.Size);
        }
        return (size, cx, cy, offset);
    }

    /// <summary>A colour (each channel 0 to 1, alpha 1) through <paramref name="matrix"/>, clamped to 0 to 1: the colour a pixel gets.</summary>
    public static (float R, float G, float B) Apply(float[] matrix, float r, float g, float b)
    {
        if (matrix == null)
            return (r, g, b);
        float Out(int j) => Math.Clamp(r * matrix[j] + g * matrix[4 + j] + b * matrix[8 + j] + matrix[12 + j] + matrix[16 + j], 0f, 1f);
        return (Out(0), Out(1), Out(2));
    }
}

