using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.Maptext;

// Gaps W1 (protocol version 74): where a world atom's maptext is drawn. BYOND draws an atom's maptext in its text box (maptext_x, maptext_y, maptext_width,
// maptext_height from the icon's lower left), clipped to the box, and an alpha mask filter on the entry clips it again (tg's status display masks its
// scrolling lines to the screen with its "outline" state). The client draws each box on one quad: the box itself, or with a mask the mask's opaque bounds,
// where the text moves while its maptext_x animates.

/// <summary>
/// One world maptext's quad: the rectangle it covers in the root icon's pixels (y up from the icon's bottom), and where that rectangle's top left is in
/// the text box (pixels from the box's top left, y down), whose size is <see cref="BoxWidth"/> x <see cref="BoxHeight"/>.
/// </summary>
public readonly record struct WorldTextRegion(float Left, float Bottom, float Width, float Height, float BoxLeft, float BoxTop, int BoxWidth, int BoxHeight)
{
    /// <summary>The rectangle's centre in icon pixels.</summary>
    public float CenterX => Left + Width / 2f;

    public float CenterY => Bottom + Height / 2f;
}

/// <summary>The opaque bounds of a mask picture, in its pixels from its lower left: x from <see cref="X0"/> to <see cref="X1"/> (exclusive), y the same.</summary>
public readonly record struct MaskBounds(int X0, int Y0, int X1, int Y1);

public static class WorldMaptextPlacement
{
    /// <summary>
    /// The quad of <paramref name="item"/>: its box, or with a mask (<see cref="MaptextItem.Mask"/> and its <paramref name="mask"/> bounds) the mask's opaque
    /// bounds inside the box's rows. False when nothing of it can show.
    /// shortcut: a mask is its opaque bounding rectangle (tg's status display's is one, less a pixel at each corner); a box that animates without a mask is
    /// clipped to where it rests.
    /// </summary>
    public static bool TryRegion(in MaptextItem item, MaskBounds? mask, out WorldTextRegion region)
    {
        region = default;
        int boxWidth = MaptextClip.BoxWidth(item.Width), boxHeight = MaptextClip.BoxHeight(item.Height);
        float left = item.X, right = item.X + boxWidth, bottom = item.Y, top = item.Y + boxHeight;
        if (item.Mask is MaptextMask m && mask is MaskBounds b)
        {
            left = m.X + b.X0;
            right = m.X + b.X1;
            bottom = Math.Max(bottom, m.Y + b.Y0);
            top = Math.Min(top, m.Y + b.Y1);
        }
        if (right <= left || top <= bottom)
            return false;
        region = new WorldTextRegion(left, bottom, right - left, top - bottom, left - item.X, item.Y + boxHeight - top, boxWidth, boxHeight);
        return true;
    }

    /// <summary>
    /// How a model shows its sprite's pixels: the sprite's opaque bounds (<paramref name="sprite"/>, its base state's) stretched over the model's front
    /// rectangle (<paramref name="minX"/> to <paramref name="maxY"/>, metres in its own frame), so an icon pixel (x, y from the icon's lower left) lies at
    /// <c>(OffsetX + x * PerPixelX, OffsetY + y * PerPixelY)</c>. The modelled housing is the sprite's (docs/ai/specs/models.md), so its screen is where the
    /// sprite's is.
    /// </summary>
    public static (float OffsetX, float OffsetY, float PerPixelX, float PerPixelY) FaceMapping(MaskBounds sprite, float minX, float minY, float maxX, float maxY)
    {
        float perX = (maxX - minX) / Math.Max(1, sprite.X1 - sprite.X0), perY = (maxY - minY) / Math.Max(1, sprite.Y1 - sprite.Y0);
        return (minX - sprite.X0 * perX, minY - sprite.Y0 * perY, perX, perY);
    }

    /// <summary>The bounds of the pixels of <paramref name="picture"/> that are not fully clear; null for none.</summary>
    public static MaskBounds? OpaqueBounds(Raster picture)
    {
        if (picture == null)
            return null;
        int minX = int.MaxValue, minRow = int.MaxValue, maxX = -1, maxRow = -1;
        for (int row = 0; row < picture.Height; row++)
        {
            for (int x = 0; x < picture.Width; x++)
            {
                if (picture.Pixels[(row * picture.Width + x) * 4 + 3] == 0)
                    continue;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
            }
        }
        // rows count from the top; the bounds from the bottom
        return maxX < 0 ? null : new MaskBounds(minX, picture.Height - 1 - maxRow, maxX + 1, picture.Height - minRow);
    }
}
