using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>DV62: where another holder's flashlight spot is drawn and which way it points.</summary>
public class FlashlightBeamTests
{
    private static LightState Beam(float ox, float oy, byte system = TgLightMath.OverlayLightDirectional, bool on = true) =>
        new(4f, 1f, 0xffc44dffu, on, 360f, 0, 1f, ox, oy, system);

    [Fact]
    public void OnlyALitDirectionalOverlayLightIsABeam()
    {
        Assert.True(FlashlightBeam.IsBeam(Beam(-2f, 0f)));
        Assert.True(FlashlightBeam.IsBeam(Beam(-2f, 0f, TgLightMath.OverlayLightBeam)));
        Assert.False(FlashlightBeam.IsBeam(Beam(-2f, 0f, on: false)));
        Assert.False(FlashlightBeam.IsBeam(Beam(0f, 0f, TgLightMath.ComplexLight)));
        Assert.False(FlashlightBeam.IsBeam(LightState.None));
    }

    [Theory]
    [InlineData(0f, -2f, 0f)]    // cast north: the centre is 2 tiles north, the offset -2
    [InlineData(-2f, 0f, 64f)]   // east
    [InlineData(0f, 2f, 128f)]   // south
    [InlineData(2f, 0f, 192f)]   // west
    public void WithoutAYawTheBeamPointsTheWayTheServerCastIt(float ox, float oy, float expected)
    {
        Assert.Equal(expected, FlashlightBeam.YawByte(false, 0f, Beam(ox, oy), 0), 3);
    }

    [Fact]
    public void ACapsuleHoldersYawWinsAndAnUncastLightFallsBackToItsDir()
    {
        Assert.Equal(100f, FlashlightBeam.YawByte(true, 100f, Beam(-2f, 0f), 0));
        Assert.Equal(192f, FlashlightBeam.YawByte(false, 0f, Beam(0f, 0f), 8), 3); // WEST
    }

    [Fact]
    public void ACarriedBeamStartsAtTheHandAheadOfItsHolderAndAFloorOneLow()
    {
        (float x, float y, float h) = FlashlightBeam.Origin(10f, 20f, 64f, carried: true, lying: false); // facing east
        Assert.Equal(10f + FlashlightBeam.Ahead, x, 3);
        Assert.Equal(20f, y, 3);
        Assert.Equal(FlashlightBeam.HandHeight, h);
        Assert.Equal(FlashlightBeam.LyingHeight, FlashlightBeam.Origin(10f, 20f, 64f, true, lying: true).Height);
        Assert.Equal((10f, 20f, FlashlightBeam.FloorHeight), FlashlightBeam.Origin(10f, 20f, 64f, carried: false, lying: false));
    }

    [Fact]
    public void TheSpotReachesAsFarAsTgsLight()
    {
        Assert.Equal(7f, FlashlightBeam.RangeTiles(Beam(-2f, 0f)));
        Assert.Equal(FlashlightBeam.EnergyPerPower, FlashlightBeam.Energy(Beam(-2f, 0f)));
        (float r, float g, float b) = FlashlightBeam.Color(Beam(-2f, 0f));
        Assert.Equal((1f, 0xc4 / 255f, 0x4d / 255f), (r, g, b));
    }

    [Fact]
    public void AFlashlightIsDrawnWhiterThanTgsOrangeAndOtherLightsAsTheyAre()
    {
        LightState white = FlashlightBeam.Presented(Beam(-2f, 0f));
        (float r, float g, float b) = FlashlightBeam.Color(white);
        Assert.Equal(1f, r);
        Assert.Equal((0xc4 + (255 - 0xc4) * FlashlightBeam.Whiten) / 255f, g, 2);
        Assert.Equal((0x4d + (255 - 0x4d) * FlashlightBeam.Whiten) / 255f, b, 2);
        Assert.Equal(0xffu, white.Rgba & 255u);
        LightState lamp = Beam(0f, 0f, TgLightMath.ComplexLight);
        Assert.Equal(lamp, FlashlightBeam.Presented(lamp));
    }
}
