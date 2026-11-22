using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>The atlas copies a picture a row at a time (performance.md P3): the same pixels as the pixel by pixel copy it replaced.</summary>
public sealed class AtlasBuilderCopyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARowCopyLaysThePixelsAndTheBorderAsThePixelByPixelCopy(bool trim)
    {
        var random = new Random(7);
        var image = new Raster(37, 23);
        random.NextBytes(image.Pixels);
        for (int i = 3; i < image.Pixels.Length; i += 4)
            if (i / 4 % 37 < 3 || i / 4 / 37 > 19)
                image.Pixels[i] = 0; // clear columns and rows for the trim to cut
        var atlas = new AtlasBuilder();
        atlas.Add(Raster.Solid(5, 5, 0xFF0000FFu), 0, 0, trim: false); // something before it, so it is not at the corner
        int id = atlas.Add(image, 0, 0, trim);

        AtlasEntry entry = atlas.Entries[id];
        int tx = 0, ty = 0, tw = image.Width, th = image.Height;
        if (trim)
            AtlasBuilder.TrimBounds(image, out tx, out ty, out tw, out th);
        Assert.Equal((tw, th), (entry.Width, entry.Height));
        // the replaced copy: each pixel of the rectangle and its one pixel border from the nearest edge pixel, the border's alpha 0
        for (int y = -1; y <= th; y++)
        {
            int sy = Math.Clamp(ty + y, ty, ty + th - 1);
            for (int x = -1; x <= tw; x++)
            {
                int sx = Math.Clamp(tx + x, tx, tx + tw - 1);
                uint want = image.Get(sx, sy);
                if (x < 0 || y < 0 || x >= tw || y >= th)
                    want &= 0xFFFFFF00u;
                Assert.Equal(want, atlas.Get(entry.X + x, entry.Y + y));
            }
        }
    }
}
