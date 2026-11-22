using Anomaly.Core.Motion;

namespace Anomaly.Core.Tests.Motion;

/// <summary>DV534: a weightless capsule's hand reaches half a tile (1 m) from its centre to a tile's square.</summary>
public class HandReachTests
{
    private const int C = MotionMath.SubunitsPerTile;

    [Fact]
    public void AWallBesideTheTilesCentreIsJustInReach()
    {
        Assert.True(MotionMath.InHandReach(15 * C, 10 * C, 16, 10)); // a metre from the wall's face
        Assert.False(MotionMath.InHandReach(15 * C - 1, 10 * C, 16, 10)); // a subunit further
        Assert.True(MotionMath.InHandReach(15 * C + 100, 10 * C, 16, 10));
        Assert.True(MotionMath.InHandReach(15 * C - 120, 10 * C + 77, 15, 10)); // the own tile always
        Assert.False(MotionMath.InHandReach(15 * C, 10 * C, 16, 11)); // a corner from a tile's centre is 1.4 m away
        Assert.True(MotionMath.InHandReach(15 * C + 60, 10 * C + 60, 16, 11));
    }
}
