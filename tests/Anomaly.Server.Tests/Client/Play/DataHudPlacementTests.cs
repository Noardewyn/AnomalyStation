using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>DV231: the security HUD's job icon is drawn over the head's top right; the other data HUD images stay at tg's pixels.</summary>
public class DataHudPlacementTests
{
    // a picture of width x height with the given box drawn (rows from the top), the icon square's lower left at (originX, originY)
    private static ComposedRaster Picture(int width, int height, int originX, int originY, int left, int top, int boxWidth, int boxHeight)
    {
        var image = new Raster(width, height);
        for (int row = top; row < top + boxHeight; row++)
            for (int col = left; col < left + boxWidth; col++)
                image.Pixels[(row * width + col) * 4 + 3] = 255;
        return new ComposedRaster { Image = image, OriginX = originX, OriginY = originY };
    }

    [Fact]
    public void The_job_icon_moves_over_the_head_and_the_other_hud_images_stay()
    {
        Assert.Equal((DataHudPlacement.JobShiftRight, DataHudPlacement.JobShiftUp), DataHudPlacement.Shift(Picture(32, 32, 0, 0, 0, 8, 8, 8))); // hudmedicaldoctor
        Assert.Equal((DataHudPlacement.JobShiftRight, DataHudPlacement.JobShiftUp), DataHudPlacement.Shift(Picture(32, 64, 0, 0, 0, 40, 8, 8))); // a taller picture
        Assert.Equal((0, 0), DataHudPlacement.Shift(Picture(32, 32, 0, 0, 8, 0, 15, 3))); // hudhealth100
        Assert.Equal((0, 0), DataHudPlacement.Shift(Picture(32, 32, 0, 0, 0, 15, 8, 8))); // hudwanted
        Assert.Equal((0, 0), DataHudPlacement.Shift(Picture(32, 32, 0, 0, 0, 0, 0, 0))); // nothing drawn
    }
}
