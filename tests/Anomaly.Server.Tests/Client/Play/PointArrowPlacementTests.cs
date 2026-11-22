using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>DV232: tg's point arrow slides from the pointer to the point (point.dm's animate, 1.7 ds, EASE_OUT).</summary>
public class PointArrowPlacementTests
{
    [Fact]
    public void The_arrow_slides_to_the_point_fast_first_and_stays_there()
    {
        Assert.Equal(0f, PointArrowPlacement.Slide(0));
        Assert.Equal(0.75f, PointArrowPlacement.Slide(PointArrowPlacement.SlideSeconds / 2), 3); // eased out: most of the way at half time
        Assert.Equal(1f, PointArrowPlacement.Slide(PointArrowPlacement.SlideSeconds));
        Assert.Equal(1f, PointArrowPlacement.Slide(2));
        Assert.Equal(0f, PointArrowPlacement.Slide(-1));
    }

    [Fact]
    public void The_arrow_keeps_its_size_on_the_screen()
    {
        float near = PointArrowPlacement.ScreenScale(1, 0.3f), far = PointArrowPlacement.ScreenScale(10, 0.3f);
        Assert.Equal(0.3f, PointArrowPlacement.ScreenScale(PointArrowPlacement.ScreenScaleMetres, 0.3f), 4);
        Assert.Equal(near / 1, far / 10, 4); // scale over distance: the angle it covers
        Assert.True(PointArrowPlacement.ScreenScale(0, 0.3f) > 0);
    }
}
