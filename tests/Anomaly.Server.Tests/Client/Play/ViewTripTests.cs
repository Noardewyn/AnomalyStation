using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// Gaps RG3 (the drugs' trips over the view, Render/ViewBlurLayer): the game plane's colour filters in other colour spaces (the
/// mushroom hallucinogen's HSL hue turn, blastoff's HCY hue, saturnX's HCY drained chroma, drug_reagents.dm), its waves and its radial
/// blur, as the client draws them.
/// </summary>
public class ViewTripTests
{
    private static FilterEntry Color(byte space, params float[] matrix) => new() { Kind = FilterKind.Color, ColorMatrix = matrix, Space = space };

    private static float[] Hsl(float hueShift) => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, hueShift, 0, 0, 0 };

    private static void Near((float R, float G, float B) expected, (float R, float G, float B) actual)
    {
        Assert.Equal(expected.R, actual.R, 2);
        Assert.Equal(expected.G, actual.G, 2);
        Assert.Equal(expected.B, actual.B, 2);
    }

    [Fact]
    public void TheMushroomsHueTurnsInHslAndItsFullTurnIsNoTint()
    {
        // col_filter_shift_once: a third of a turn, red to green; col_filter_reset: the full turn, as the identity
        Near((0f, 1f, 0f), ViewBlur.ApplySteps(ViewBlur.ColorSteps(new[] { Color(2, Hsl(0.333f)) }), 1f, 0f, 0f));
        Near((0f, 0f, 1f), ViewBlur.ApplySteps(ViewBlur.ColorSteps(new[] { Color(2, Hsl(0.666f)) }), 1f, 0f, 0f));
        Near((0.8f, 0.3f, 0.1f), ViewBlur.ApplySteps(ViewBlur.ColorSteps(new[] { Color(2, Hsl(1f)) }), 0.8f, 0.3f, 0.1f));
        // an HSL matrix is not an RGB tint: the version-31 matrix leaves it out (it would have painted the view red)
        Assert.Null(ViewBlur.ColorMatrix(new[] { Color(2, Hsl(1f)) }));
    }

    [Fact]
    public void BlastoffSetsOneHueAndSaturnXDrainsTheChromaInHcy()
    {
        // blastoff's col_filter_mid: the hue row zeroed, 0.832 (between red and blue) for every pixel, chroma and luma kept
        var blastoff = Color(3, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.832f, 0, 0, 0);
        (float r, float g, float b) = ViewBlur.ApplySteps(ViewBlur.ColorSteps(new[] { blastoff }), 0f, 0.6f, 0f);
        Assert.True(b > r && b > g && r >= g, $"green became {r}, {g}, {b}");
        Assert.Equal(0.6f * 0.587f, 0.299f * r + 0.587f * g + 0.114f * b, 2); // the luma kept
        // saturnX's col_filter_twothird: 0.68 of the chroma
        var saturnx = Color(3, 1, 0, 0, 0, 0, 0.68f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        (r, g, b) = ViewBlur.ApplySteps(ViewBlur.ColorSteps(new[] { saturnx }), 1f, 0f, 0f);
        Assert.Equal(0.68f, MathF.Max(r, MathF.Max(g, b)) - MathF.Min(r, MathF.Min(g, b)), 2);
        Assert.Equal(0.299f, 0.299f * r + 0.587f * g + 0.114f * b, 2);
    }

    [Fact]
    public void NeighbouringRgbMatricesAreOneStepAndOtherSpacesTheirOwn()
    {
        var green = Color(0, 0.8f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0.8f, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        var steps = ViewBlur.ColorSteps(new[] { green, green, Color(2, Hsl(0.333f)), green });
        Assert.Equal(3, steps.Count);
        Assert.Equal(new byte[] { 0, 2, 0 }, steps.Select(s => s.Space).ToArray());
        Assert.Equal(0.64f, steps[0].Matrix[0], 3);
        Assert.Empty(ViewBlur.ColorSteps(null));
        Assert.Empty(ViewBlur.ColorSteps(new[] { new FilterEntry { Kind = FilterKind.Blur, Size = 2 } }));
    }

    [Fact]
    public void TheMushroomsWaveIsScaledToTheViewAndPushesAlongItsDirection()
    {
        // psilocybin_wave: size 2, x = y = 32 (a period of 45.25 BYOND pixels), on a 960-pixel view: twice that
        var wave = new FilterEntry { Kind = FilterKind.Wave, Size = 2, X = 32, Y = 32 };
        var waves = ViewBlur.Waves(new[] { wave }, 960);
        ViewBlur.ViewWave w = Assert.Single(waves);
        Assert.Equal(MathF.Sqrt(2f) * 64f, w.Period, 2);
        Assert.Equal(4f, w.Amplitude, 3);
        Assert.Equal(MathF.Sqrt(0.5f), w.DirX, 3);
        // a quarter period out along the wave: the full push, along it
        float quarter = w.Period / 4f;
        (float dx, float dy) = ViewBlur.WavePush(waves, quarter * w.DirX, quarter * w.DirY);
        Assert.Equal(4f * w.DirX, dx, 2);
        Assert.Equal(4f * w.DirY, dy, 2);
        (dx, dy) = ViewBlur.WavePush(waves, 0, 0);
        Assert.Equal(0f, dx, 3);
        Assert.Equal(0f, dy, 3);
        // blastoff's wave has BYOND's default size, 1; a wave of no size or no period is none
        Assert.Single(ViewBlur.Waves(new[] { new FilterEntry { Kind = FilterKind.Wave, X = 32, Y = 32 } }, 480));
        Assert.Empty(ViewBlur.Waves(new[] { new FilterEntry { Kind = FilterKind.Wave, Size = 0, X = 32, Y = 32 } }, 480));
        Assert.Empty(ViewBlur.Waves(new[] { new FilterEntry { Kind = FilterKind.Wave, Size = 2 } }, 480));
    }

    [Fact]
    public void SaturnXsRadialBlurIsNoneAtSizeZeroAndScaledOtherwise()
    {
        Assert.Equal(0f, ViewBlur.Radial(new[] { new FilterEntry { Kind = FilterKind.RadialBlur, Size = 0 } }, 960).Size);
        (float size, float cx, float cy, float offset) = ViewBlur.Radial(new[] { new FilterEntry { Kind = FilterKind.RadialBlur, Size = 0.02f, X = 10, Offset = 5 } }, 960);
        Assert.Equal(0.02f, size, 4);
        Assert.Equal(20f, cx, 3);
        Assert.Equal(0f, cy, 3);
        Assert.Equal(10f, offset, 3);
    }
}
