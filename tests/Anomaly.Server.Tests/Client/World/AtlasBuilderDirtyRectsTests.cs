using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>The atlas tells the renderer the rectangles drawn since its last upload, one per shelf, so a new picture uploads alone.</summary>
public sealed class AtlasBuilderDirtyRectsTests
{
    [Fact]
    public void ThePicturesOfAShelfMakeOneRectangleThatCoversTheirPixelsAndBorders()
    {
        var atlas = new AtlasBuilder();
        atlas.Add(Raster.Solid(30, 30, 0xFF0000FFu), 0, 0, trim: false);
        atlas.TakeDirtyRects(); // what the first upload took
        int a = atlas.Add(Raster.Solid(30, 30, 0x00FF00FFu), 0, 0, trim: false);
        int b = atlas.Add(Raster.Solid(30, 28, 0x0000FFFFu), 0, 0, trim: false);
        int tall = atlas.Add(Raster.Solid(10, 90, 0xFFFFFFFFu), 0, 0, trim: false); // too tall for the first shelf: a shelf of its own

        var rects = atlas.TakeDirtyRects();
        Assert.Equal(2, rects.Count);
        foreach (int id in new[] { a, b, tall })
        {
            AtlasEntry e = atlas.Entries[id];
            // the entry's pixels and the one pixel border around them lie in one rectangle
            Assert.Contains(rects, r => r.Page == e.Y / AtlasBuilder.PageHeight && r.X <= e.X - 1 && e.X + e.Width + 1 <= r.X + r.Width
                && r.Y <= e.Y % AtlasBuilder.PageHeight - 1 && e.Y % AtlasBuilder.PageHeight + e.Height + 1 <= r.Y + r.Height);
        }
        Assert.Empty(atlas.TakeDirtyRects());
        Assert.Empty(atlas.TakeDirtyPages());
    }
}
