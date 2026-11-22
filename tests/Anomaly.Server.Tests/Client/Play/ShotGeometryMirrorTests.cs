using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>The server's copies of the client's drawing that an aimed shot is tested against (ShotGeometry) match the client's own.</summary>
public class ShotGeometryMirrorTests
{
    [Fact]
    public void TheLadderHatchOpeningIsTheClients()
    {
        Assert.Equal(WorldGeometry.HatchWest, ShotGeometry.HatchWest);
        Assert.Equal(WorldGeometry.HatchEast, ShotGeometry.HatchEast);
        Assert.Equal(WorldGeometry.HatchSouth, ShotGeometry.HatchSouth);
        Assert.Equal(WorldGeometry.HatchNorth, ShotGeometry.HatchNorth);
    }

    [Theory]
    [InlineData(1, 0f, -0.5f)]
    [InlineData(1, 0.3f, 0.2f)]
    [InlineData(2, 0f, 0.25f)]
    [InlineData(4, 0.4f, -0.1f)]
    [InlineData(8, -0.2f, 0.4f)]
    [InlineData(4, 0.9f, 0f)]
    public void TheStairsFlightRisesAsTheClientDrawsIt(byte dir, float east, float north)
        => Assert.Equal(StairRamp.Progress(dir, 5 + east, 5 + north, 5, 5), ShotGeometry.FlightProgress(dir, east, north), 4);
}
