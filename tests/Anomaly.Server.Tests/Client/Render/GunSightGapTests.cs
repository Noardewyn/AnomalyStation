using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>DV96: the gun crosshair's gap is the held gun's cone on the screen, opened for a moment by a shot.</summary>
public class GunSightGapTests
{
    [Fact]
    public void TheGapIsTheConeAtTheFieldOfViewAndAShotOpensIt()
    {
        // 90 degrees of view on 1000 pixels: tan(45) = 1, so a cone of 5.71 degrees (tan 0.1) is 50 pixels off the middle
        Assert.Equal(50f, GunSightGap.Pixels(5.7106f, 90f, 1000f, 10), 1);
        Assert.Equal(GunSightGap.MinPixels, GunSightGap.Pixels(0f, 90f, 1000f, 10)); // a gun with no spread
        Assert.Equal(GunSightGap.MinPixels + GunSightGap.BloomPixels, GunSightGap.Pixels(0f, 90f, 1000f, 0)); // just fired
        Assert.Equal(GunSightGap.MinPixels + GunSightGap.BloomPixels / 2, GunSightGap.Pixels(0f, 90f, 1000f, GunSightGap.BloomSeconds / 2), 3);
        Assert.True(GunSightGap.Pixels(5.7106f, 45f, 1000f, 10) > 100f); // a narrower view (a scope) shows the same cone wider
        Assert.Equal(500f, GunSightGap.Pixels(80f, 60f, 1000f, 10)); // never past the screen's edge
    }
}
