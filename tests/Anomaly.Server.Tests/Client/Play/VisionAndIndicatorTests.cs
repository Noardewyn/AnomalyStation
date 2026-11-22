using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

public class VisionAndIndicatorTests
{
    [Fact]
    public void NamesClearTheHeadAndFollowALyingOrRaisedSprite()
    {
        float standing = NameTagPlacement.Height(1.45f, 2f);
        Assert.InRange(standing, 1.57f, 1.7f);
        Assert.True(standing + 0.16f < 1.85f); // Text remains below the ceiling with a visible gap.
        float lying = NameTagPlacement.Height(0.45f, 2f);
        Assert.InRange(lying, 0.57f, 0.7f);
        Assert.True(lying < standing - 0.8f);
        Assert.True(NameTagPlacement.Height(0.85f, 2f) > lying);
    }

    [Fact]
    public void CorpseSightDoesNotEnableMesonsAndGhostUsesTheReplicatedGoggleCutoffs()
    {
        var mesons = new VisionState(SightFlags.SeeTurfs, 10, 0, 30, 0);
        Assert.True(ObserverVision.Mesons(mesons, false));
        Assert.False(ObserverVision.Mesons(mesons, true));
        var deadSight = new VisionState(SightFlags.SeeMobs | SightFlags.SeeObjs, 0, 10, 25, 10);
        byte deadPosture = PostureBits.Encode(true, 90, true);
        Assert.True(ObserverVision.CorpseSight(deadSight, false, deadPosture));
        Assert.False(ObserverVision.Mesons(deadSight, false, deadPosture));
        Assert.False(ObserverVision.CorpseSight(deadSight, true, deadPosture));
        Assert.False(ObserverVision.CorpseSight(mesons, false, PostureBits.Encode(false, 0, false)));
    }

    [Fact]
    public void ThermalSightSeesMobsThroughWallsButNotAGhostsOrACorpses()
    {
        var thermals = new VisionState(SightFlags.SeeMobs, 0, 25, 8, 5);
        Assert.True(ObserverVision.SeesMobs(thermals, false));
        Assert.False(ObserverVision.SeesMobs(thermals, true));
        Assert.False(ObserverVision.SeesMobs(new VisionState(SightFlags.SeeTurfs, 0, 0, 0, 0), false));
        var deadSight = new VisionState(SightFlags.SeeMobs | SightFlags.SeeObjs, 0, 0, 0, 0);
        Assert.False(ObserverVision.SeesMobs(deadSight, false, PostureBits.Encode(true, 90, true)));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(90, 0, 90)]
    [InlineData(0, 90, -90)]
    [InlineData(90, 90, 0)]
    [InlineData(180, 90, 90)]
    public void ApcBearingFollowsTheCamera(double target, double camera, double expected)
    {
        double angle = target * Math.PI / 180;
        var transform = new Affine(Math.Cos(angle), Math.Sin(angle), 0, -Math.Sin(angle), Math.Cos(angle), 0);
        Assert.Equal(expected, ApcIndicator.Angle(transform, camera * Math.PI / 180) * 180 / Math.PI, 6);
    }

    [Fact]
    public void PointArrowUsesWorldMapPixelsRatherThanItsItemScale()
    {
        Assert.Equal((10f, 7f), PointArrowPlacement.Target(4, 4, new AppearanceEntry { PixelX = 6 * 32, PixelY = 3 * 32 }));
    }
}
