using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared;

/// <summary>
/// DV231 (the owner, 2026-10-30): the security HUD's job icon (tg's ID_HUD, drawn by hud.dmi in the icon square's left 8 columns, rows 8-15
/// from the top: beside the neck), and the mindshield's frame tg draws on it, are drawn over the head's top right instead (tg's human head is columns 11-20, rows 3-10).
/// </summary>
public static class DataHudPlacement
{
    /// <summary>Pixels the job icon moves right and up.</summary>
    public const int JobShiftRight = 20, JobShiftUp = 15;

    /// <summary>The shift of a data HUD image (right and up, in pixels): the job icon's, none for any other.</summary>
    public static (int Right, int Up) Shift(ComposedRaster raster)
    {
        AtlasBuilder.TrimBounds(raster.Image, out int x, out int y, out int width, out int height);
        if (width == 0)
            return (0, 0);
        int left = x - raster.OriginX;
        int top = y - (raster.Image.Height - raster.OriginY - 32); // rows from the icon square's top
        return left >= 0 && left + width <= 8 && top >= 8 && top + height <= 16 ? (JobShiftRight, JobShiftUp) : (0, 0);
    }
}
