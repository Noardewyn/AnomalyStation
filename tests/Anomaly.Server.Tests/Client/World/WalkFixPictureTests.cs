using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 R2: no root pixel offset in a composed picture (the placement applies it once).</summary>
public sealed class WalkFixPictureTests
{
    private const uint White = 0xFFFFFFFF, Red = 0xFF0000FF;

    private static (AppearanceCompositor Compositor, TestTables Tables, FakeIcon Icon) Compositor()
    {
        var library = new IconLibrary(new MemoryIconFiles());
        var icon = new FakeIcon();
        library.Register("a.dmi", icon);
        return (new AppearanceCompositor(library), new TestTables(), icon);
    }

    [Fact]
    public void AComposedPropPictureCarriesNoRootPixelOffsetButChildrenKeepTheirs()
    {
        (AppearanceCompositor compositor, TestTables tables, FakeIcon icon) = Compositor();
        icon.Solid("base", White, 4).Solid("dot", Red, 2);
        // A wall mount: the planner moves it 3 pixels east and 1 north; the picture must not move it again.
        TableRef mounted = tables.Add("a.dmi", "base", e => { e.PixelX = 3; e.PixelY = 1; });
        Assert.True(compositor.TryCompose(tables, mounted, ByondDir.South, 0, out ComposedRaster composed));
        Assert.Equal((4, 4, 0, 0), (composed.Image.Width, composed.Image.Height, composed.OriginX, composed.OriginY));
        Assert.Equal(White, composed.Image.Get(0, 0));
        Assert.Equal(White, composed.Image.Get(3, 3));

        // An overlay keeps its own offset relative to the root: 3 pixels right, so a 2 x 2 dot sticks out one pixel on the right (width 5), whatever the root's offset.
        TableRef dot = tables.Add("a.dmi", "dot", d => d.PixelX = 3);
        TableRef withOverlay = tables.Add("a.dmi", "base", e => { e.PixelX = 8; e.PixelY = 6; e.Overlays.Add(dot); });
        Assert.True(compositor.TryCompose(tables, withOverlay, ByondDir.South, 0, out ComposedRaster overlaid));
        Assert.Equal((5, 4, 0, 0), (overlaid.Image.Width, overlaid.Image.Height, overlaid.OriginX, overlaid.OriginY));
    }

    [Fact]
    public void TheHudsPictureKeepsItsRootOffsetAndTheTwoPicturesAreCachedApart()
    {
        (AppearanceCompositor compositor, TestTables tables, FakeIcon icon) = Compositor();
        icon.Solid("base", White, 4);
        TableRef shifted = tables.Add("a.dmi", "base", e => e.PixelX = 3);
        Assert.True(compositor.TryCompose(tables, shifted, ByondDir.South, 0, out ComposedRaster plain));
        Assert.True(compositor.TryCompose(tables, shifted, ByondDir.South, 0, out ComposedRaster hud, rootOffset: true));
        Assert.NotSame(plain, hud);
        Assert.Equal((4, 0), (plain.Image.Width, plain.OriginX));
        // The offset shows as the square staying where it is and the picture reaching 3 pixels to its right.
        Assert.Equal((7, 0), (hud.Image.Width, hud.OriginX));
        Assert.Equal(0u, hud.Image.Get(0, 0));
        Assert.Equal(White, hud.Image.Get(3, 0));
        Assert.True(compositor.TryCompose(tables, shifted, ByondDir.South, 0, out ComposedRaster again));
        Assert.Same(plain, again);
    }
}
