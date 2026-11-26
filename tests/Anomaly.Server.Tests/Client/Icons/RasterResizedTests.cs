using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Icons;

/// <summary>A raster resized by nearest pixels: a whole multiple repeats each pixel, a smaller size keeps every n-th one.</summary>
public sealed class RasterResizedTests
{
    [Fact]
    public void AWholeMultipleRepeatsEachPixelAndASmallerSizeKeepsTheNearest()
    {
        var picture = new Raster(2, 3);
        picture.Set(0, 0, 0x110000FFu);
        picture.Set(1, 0, 0x220000FFu);
        picture.Set(0, 2, 0x330000FFu);
        picture.Set(1, 2, 0x440000FFu);

        Raster big = picture.Resized(8, 6);
        Assert.Equal((8, 6), (big.Width, big.Height));
        Assert.Equal(0x110000FFu, big.Get(3, 1));
        Assert.Equal(0x220000FFu, big.Get(4, 0));
        Assert.Equal(0u, big.Get(0, 2)); // the middle row, left transparent
        Assert.Equal(0x440000FFu, big.Get(7, 5));

        Raster small = picture.Resized(1, 2);
        Assert.Equal(0x110000FFu, small.Get(0, 0));
        Assert.Equal(0u, small.Get(0, 1)); // row 1 of the 3
    }
}
