using Anomaly.Client.Shared.Light;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>M3 P7: tg's falloff, cone and corner normalisation against values worked by hand from the DM formulas.</summary>
public class TgLightMathTests
{
    // lighting_source.dm:278: multiplier = 1 - CLAMP01(sqrt(x**2 + y**2 + z**2 + height) / max(1, range))

    [Fact]
    public void FalloffAtCornerOfTheSourceTile()
    {
        // x = y = 0.5, height 1, range 4: sqrt(0.25 + 0.25 + 1) = 1.224745; / 4 = 0.306186; 1 - that = 0.693814.
        Assert.Equal(0.693814f, TgLightMath.FalloffAtCoord(0.5f, 0.5f, 0f, 4f, 0, 360f, 1f), 5);
    }

    [Fact]
    public void FalloffAtAnotherCorner()
    {
        // x = 1.5, y = 0.5, range 6: sqrt(2.25 + 0.25 + 1) = 1.870829; / 6 = 0.311805; 1 - that = 0.688195.
        Assert.Equal(0.688195f, TgLightMath.FalloffAtCoord(1.5f, 0.5f, 0f, 6f, 0, 360f, 1f), 4);
    }

    [Fact]
    public void FalloffIsClampedAtTheRangeAndRangeBelowOneDividesByOne()
    {
        Assert.Equal(0f, TgLightMath.FalloffAtCoord(10f, 0f, 0f, 4f, 0, 360f, 1f));
        // range 0.5 -> divisor max(1, 0.5) = 1; x = y = 0.25: sqrt(0.0625 + 0.0625 + 1) = 1.06066 -> clamped to 1 -> 0.
        Assert.Equal(0f, TgLightMath.FalloffAtCoord(0.25f, 0.25f, 0f, 0.5f, 0, 360f, 1f));
        // x = 0.1, y = 0: sqrt(0.01 + 1) = 1.004988 -> clamped -> 0; with height -0.5 (space): sqrt(0.01 - 0.5) is NaN in
        // sqrt, so tg's starlight is only asked at corners where x*x+y*y >= 0.5 (corners are at half-tile offsets).
        Assert.Equal(1f, TgLightMath.FalloffAtCoord(0.5f, 0.5f, 0f, 2f, 0, 360f, TgLightMath.LightingHeightSpace), 5);
    }

    [Fact]
    public void AngleOf360Or0IsOmnidirectional()
    {
        float round = TgLightMath.FalloffAtCoord(3f, 0f, 0f, 8f, 1, 360f, 1f);
        Assert.Equal(round, TgLightMath.FalloffAtCoord(3f, 0f, 0f, 8f, 1, 0f, 1f), 6);
        Assert.True(round > 0f);
    }

    [Fact]
    public void ConeKeepsTheLightInsideHalfTheAngle()
    {
        // light_angle 90, light_dir NORTH (angle 0): a corner at (1, 1) is 45 degrees off: exactly half the angle, uncut.
        float omni = TgLightMath.FalloffAtCoord(1f, 1f, 0f, 8f, 1, 360f, 1f);
        Assert.Equal(omni, TgLightMath.FalloffAtCoord(1f, 1f, 0f, 8f, 1, 90f, 1f), 5);
        // straight ahead too
        float ahead = TgLightMath.FalloffAtCoord(0f, 3f, 0f, 8f, 1, 360f, 1f);
        Assert.Equal(ahead, TgLightMath.FalloffAtCoord(0f, 3f, 0f, 8f, 1, 90f, 1f), 5);
    }

    [Fact]
    public void ConeFadesOverThirtyDegreesAndIsDarkBeyond()
    {
        // (2, 1): atan(2/1) = 63.4349 degrees; delta 63.4349 - 45 = 18.4349; factor 1 - 18.4349/30 = 0.385503.
        // multiplier: sqrt(4 + 1 + 0 + 1) = 2.449490; / 8 = 0.306186; 1 - that = 0.693814. Product 0.267467.
        Assert.Equal(0.267467f, TgLightMath.FalloffAtCoord(2f, 1f, 0f, 8f, 1, 90f, 1f), 4);
        // due east is 90 degrees off: 90 - 45 = 45 > 30 -> 1 - 1.5 < 0 -> clamped to 0
        Assert.Equal(0f, TgLightMath.FalloffAtCoord(3f, 0f, 0f, 8f, 1, 90f, 1f));
        // behind
        Assert.Equal(0f, TgLightMath.FalloffAtCoord(0f, -3f, 0f, 8f, 1, 90f, 1f));
    }

    [Fact]
    public void ConeNormalisesTheDeltaPast180()
    {
        // light_dir NORTH (0), corner at (-1, 1): delta_to_angle = atan(-1) + 360 = 315; |0 - 315| = 315 > 180 -> 180 - 135 = 45.
        // angle 90: 45 - 45 = 0: uncut.
        float omni = TgLightMath.FalloffAtCoord(-1f, 1f, 0f, 8f, 1, 360f, 1f);
        Assert.Equal(omni, TgLightMath.FalloffAtCoord(-1f, 1f, 0f, 8f, 1, 90f, 1f), 5);
        // angle 60: 45 - 30 = 15 -> factor 0.5.
        Assert.Equal(omni * 0.5f, TgLightMath.FalloffAtCoord(-1f, 1f, 0f, 8f, 1, 60f, 1f), 5);
    }

    [Fact]
    public void DirectionsAndBearingsMatchTgsHelpers()
    {
        Assert.Equal(0f, TgLightMath.Dir2Angle(1));
        Assert.Equal(180f, TgLightMath.Dir2Angle(2));
        Assert.Equal(90f, TgLightMath.Dir2Angle(4));
        Assert.Equal(270f, TgLightMath.Dir2Angle(8));
        Assert.Equal(45f, TgLightMath.Dir2Angle(5));
        Assert.Equal(135f, TgLightMath.Dir2Angle(6));
        Assert.Equal(315f, TgLightMath.Dir2Angle(9));
        Assert.Equal(225f, TgLightMath.Dir2Angle(10));
        Assert.Equal(90f, TgLightMath.DeltaToAngle(2f, 0f));
        Assert.Equal(270f, TgLightMath.DeltaToAngle(-2f, 0f));
        Assert.Equal(180f, TgLightMath.DeltaToAngle(0f, -3f), 4);
        Assert.Equal(135f, TgLightMath.DeltaToAngle(1f, -1f), 4);
        Assert.Equal(315f, TgLightMath.DeltaToAngle(-1f, 1f), 4);
    }

    [Fact]
    public void LargestChannelAboveOneScalesAllThreeAndEverythingRoundsToASixtyFourth()
    {
        // lighting_corner.dm update_objects: (2, 1, 0.5) -> factor 0.5 -> (1, 0.5, 0.25)
        TgLightMath.Normalize(2f, 1f, 0.5f, out float r, out float g, out float b);
        Assert.Equal(1f, r);
        Assert.Equal(0.5f, g);
        Assert.Equal(0.25f, b);
        // 0.3 * 64 = 19.2 -> 19 / 64
        TgLightMath.Normalize(0.3f, 0f, 0f, out r, out _, out _);
        Assert.Equal(19f / 64f, r);
        // halves round up (DM round()): 0.5/64 -> 1/64
        Assert.Equal(1f / 64f, TgLightMath.Round(0.5f / 64f));
    }

    [Fact]
    public void RangesFollowTheSource()
    {
        Assert.Equal(1, TgLightMath.VisualOffset(0.4f, 0f));
        Assert.Equal(0, TgLightMath.VisualOffset(0f, 0f));
        Assert.Equal(2, TgLightMath.VisualOffset(-1.2f, 0.5f));
        Assert.Equal(5, TgLightMath.BoundRange(4.5f, 0));
        Assert.Equal(1, TgLightMath.BoundRange(0.5f, 0));
        Assert.Equal(7, TgLightMath.WorkingRange(5.5f, 1));
    }
}
