using System.Globalization;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>Where a coordinate of a <c>screen_loc</c> is measured from.</summary>
public enum ScreenAnchor : byte
{
    /// <summary>A tile number from 1 (<c>"12,1"</c>).</summary>
    Absolute,

    /// <summary><c>WEST</c> and <c>SOUTH</c> (also <c>LEFT</c> and <c>BOTTOM</c>): the first column or row.</summary>
    Low,

    /// <summary><c>CENTER</c>.</summary>
    Center,

    /// <summary><c>EAST</c> and <c>NORTH</c> (also <c>RIGHT</c> and <c>TOP</c>): the last column or row.</summary>
    High,
}

/// <summary>One coordinate of a <c>screen_loc</c>: an anchor, an offset in tiles and an offset in pixels (tg's 32 per tile), or a share of the
/// screen (BYOND's <c>LEFT+50%</c>: tg's cinematic, syndicate N1a).</summary>
public readonly record struct ScreenAxis(ScreenAnchor Anchor, int Tile, int Pixels, int Percent = 0);

/// <summary>A parsed <c>screen_loc</c>: the tile at (X, Y), or, with <see cref="HasRange"/>, the rectangle of tiles from it to (X2, Y2).</summary>
public readonly record struct ScreenLocSpec(ScreenAxis X, ScreenAxis Y, bool HasRange, ScreenAxis X2, ScreenAxis Y2);

/// <summary>A rectangle in screen pixels, y down.</summary>
public readonly record struct HudRect(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;

    public float Bottom => Top + Height;
}

/// <summary>The screen the HUD is drawn on.</summary>
/// <param name="Width">Pixels.</param>
/// <param name="Height">Pixels.</param>
/// <param name="Scale">The player's HUD scale (1 is normal).</param>
public readonly record struct HudMetrics(float Width, float Height, float Scale)
{
    /// <summary>The tiles of tg's view, which the HUD grid is: 15 x 15 (<c>hud.dm</c>: "in our case 15x15").</summary>
    public const int GridTiles = 15;

    /// <summary>Pixels of a tg icon (32 per tile).</summary>
    public const float IconPixels = 32f;

    /// <summary>Pixels of one HUD tile: tg's 15 tiles span the screen's height, so the grid sits on the screen edges.</summary>
    public float Tile => Height / GridTiles * Scale;

    /// <summary>Screen pixels per tg pixel.</summary>
    public float PixelScale => Tile / IconPixels;

    /// <summary>The screen height from which the HUD's text has its full base size (twice P7's sizes, the owner's 2026-10-09).</summary>
    public const float TextReferenceHeight = 1080f;

    /// <summary>
    /// The size factor of the HUD's text (the chat, the screentip, F4's corner, the version tag): twice P7's sizes times the
    /// player's HUD scale (the owner, 2026-10-18: the text scales with the HUD). A screen lower than
    /// <see cref="TextReferenceHeight"/> shrinks it with its height; a higher one does not grow it (the owner, 2026-10-18:
    /// on a 4K screen text grown with the height took the whole screen).
    /// </summary>
    public float TextScale => 2f * Scale * Math.Min(1f, Height / TextReferenceHeight);
}

/// <summary>
/// tg's <c>screen_loc</c> (BYOND's screen location text) mapped from tg's 15 x 15 view grid onto the client's screen (D107).
/// The grid is anchored to the screen's edges instead of being a square in the middle: <c>WEST</c> is the left edge,
/// <c>EAST</c> the right edge, <c>CENTER</c> the middle, <c>SOUTH</c> the bottom and <c>NORTH</c> the top; a HUD tile is one
/// fifteenth of the screen's height (times the player's scale), so on a 16:9 screen the right-hand status column sits at the
/// right edge instead of inside a square. Forms handled (tg's <c>__DEFINES/hud.dm</c>): <c>"EAST-1:28,CENTER-2:17"</c>,
/// <c>"WEST:6,SOUTH:5"</c>, <c>"CENTER:-2"</c>, spaces after the comma, <c>"12,1"</c>, and a range
/// <c>"WEST,SOUTH to EAST,NORTH"</c>; <c>BOTTOM</c>, <c>TOP</c>, <c>LEFT</c> and <c>RIGHT</c> are the same as their compass
/// words, and an axis pair given y first is swapped.
/// </summary>
public static class ScreenLoc
{
    /// <summary>Parses a <c>screen_loc</c>; false for an empty or unreadable one (the element is not drawn).</summary>
    public static bool TryParse(string text, out ScreenLocSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string t = text.Trim();
        int to = t.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
        if (to < 0)
        {
            if (!TryParsePair(t, out ScreenAxis x, out ScreenAxis y))
                return false;
            spec = new ScreenLocSpec(x, y, false, default, default);
            return true;
        }
        if (!TryParsePair(t.Substring(0, to), out ScreenAxis x1, out ScreenAxis y1)
            || !TryParsePair(t.Substring(to + 4), out ScreenAxis x2, out ScreenAxis y2))
            return false;
        spec = new ScreenLocSpec(x1, y1, true, x2, y2);
        return true;
    }

    private static bool TryParsePair(string text, out ScreenAxis x, out ScreenAxis y)
    {
        x = y = default;
        string[] parts = text.Split(',');
        if (parts.Length != 2)
            return false;
        if (!TryParseAxis(parts[0], out ScreenAxis first, out bool firstIsY) || !TryParseAxis(parts[1], out ScreenAxis second, out bool secondIsY))
            return false;
        // "BOTTOM+1:8,RIGHT-1:28" (tg's ui_floor_change) names the y axis first.
        if (firstIsY && !secondIsY)
        {
            x = second;
            y = first;
        }
        else
        {
            x = first;
            y = second;
        }
        return true;
    }

    // One coordinate: ANCHOR[+-tiles][:pixels]. isY is true for the words that only make sense on the y axis.
    private static bool TryParseAxis(string text, out ScreenAxis axis, out bool isY)
    {
        axis = default;
        isY = false;
        // M4a P7: tg builds some locs by string interpolation: "CENTER+-1:16" (ui_hand_position with a negative offset) means CENTER-1, and ui_inv2 has "CENTER-1 :16".
        string t = text.Replace(" ", string.Empty).Replace("+-", "-").Replace("-+", "-");
        if (t.Length == 0)
            return false;
        int i = 0;
        while (i < t.Length && char.IsAsciiLetter(t[i]))
            i++;
        ScreenAnchor anchor;
        int absolute = 0;
        if (i > 0)
        {
            switch (t.Substring(0, i).ToUpperInvariant())
            {
                case "WEST" or "LEFT": anchor = ScreenAnchor.Low; break;
                case "EAST" or "RIGHT": anchor = ScreenAnchor.High; break;
                case "SOUTH" or "BOTTOM": anchor = ScreenAnchor.Low; isY = true; break;
                case "NORTH" or "TOP": anchor = ScreenAnchor.High; isY = true; break;
                case "CENTER": anchor = ScreenAnchor.Center; break;
                default: return false;
            }
        }
        else
        {
            int digits = 0;
            while (digits < t.Length && char.IsAsciiDigit(t[digits]))
                digits++;
            if (digits == 0 || !int.TryParse(t.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out absolute) || absolute > 1000)
                return false;
            anchor = ScreenAnchor.Absolute;
            i = digits;
        }
        int tiles = 0, pixels = 0, percent = 0;
        // An optional signed number of tiles (or a percentage of the screen, BYOND's "LEFT+50%"), then an optional :pixels (which may be negative).
        if (i < t.Length && (t[i] == '+' || t[i] == '-'))
        {
            int start = i;
            i++;
            while (i < t.Length && char.IsAsciiDigit(t[i]))
                i++;
            if (!int.TryParse(t.AsSpan(start, i - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out tiles) || Math.Abs(tiles) > 1000)
                return false;
            if (i < t.Length && t[i] == '%')
            {
                percent = tiles;
                tiles = 0;
                i++;
            }
        }
        if (i < t.Length && t[i] == ':')
        {
            if (!int.TryParse(t.AsSpan(i + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out pixels) || Math.Abs(pixels) > 100000)
                return false;
            i = t.Length;
        }
        if (i != t.Length)
            return false;
        axis = new ScreenAxis(anchor, anchor == ScreenAnchor.Absolute ? absolute - 1 + tiles : tiles, pixels, percent);
        return true;
    }

    /// <summary>
    /// The rectangle of an element: <paramref name="pixelWidth"/> x <paramref name="pixelHeight"/> tg pixels whose own 32 x 32
    /// icon square has its lower left corner at (<paramref name="originX"/>, <paramref name="originY"/>) of the texture
    /// (<c>ComposedAppearance</c>), on the tile <paramref name="spec"/> names. An element of 15 x 15 tiles or more (tg's
    /// fullscreen overlays, <c>CENTER-7,CENTER-7</c>) covers the whole screen; a range stretches over its tiles.
    /// <paramref name="fullscreen"/> says which.
    /// </summary>
    public static HudRect Place(in ScreenLocSpec spec, in HudMetrics screen, int pixelWidth, int pixelHeight, int originX, int originY, out bool fullscreen)
    {
        fullscreen = pixelWidth >= HudMetrics.GridTiles * 32 && pixelHeight >= HudMetrics.GridTiles * 32;
        if (fullscreen)
            return new HudRect(0, 0, screen.Width, screen.Height);

        float u = screen.Tile, s = screen.PixelScale;
        float left = TileStart(spec.X, screen.Width, u, s);
        float bottom = TileStart(spec.Y, screen.Height, u, s);
        if (spec.HasRange)
        {
            float right = TileStart(spec.X2, screen.Width, u, s) + u;
            float top = TileStart(spec.Y2, screen.Height, u, s) + u;
            return new HudRect(left, screen.Height - top, Math.Max(right - left, 0f), Math.Max(top - bottom, 0f));
        }
        float width = pixelWidth * s, height = pixelHeight * s;
        float l = left - originX * s;
        float b = bottom - originY * s;
        return new HudRect(l, screen.Height - (b + height), width, height);
    }

    /// <summary>
    /// The slot (1 to 5, tg's alert index) of one of tg's alert screen_locs, <c>EAST-1:28,CENTER+(6 - i):(29 - 2i)</c> (<c>alert.dm</c>
    /// get_ui_alert_placement); 0 for any other.
    /// </summary>
    public static int AlertSlot(in ScreenLocSpec spec)
        => !spec.HasRange && spec.X == new ScreenAxis(ScreenAnchor.High, -1, 28) && spec.Y.Anchor == ScreenAnchor.Center
           && spec.Y.Tile is >= 1 and <= 5 && spec.Y.Pixels == 17 + 2 * spec.Y.Tile ? 6 - spec.Y.Tile : 0;

    /// <summary>
    /// DEVIATION(DV82): an alert's place in the right-hand column. The first sits in the top right corner as far from the top as tg's
    /// <c>EAST-1:28</c> is from the right (4 tg pixels), the next ones under it one tile and two pixels apart as tg spaces them; when
    /// <paramref name="slots"/> of them would reach <paramref name="floor"/> (the top of the column's next icon below, the pulse: screen pixels
    /// from the top), the column and its icons shrink to end that margin above it. tg counts them from the screen's centre, and at a HUD
    /// scale above 1 they went past the top and onto the pulse (the owner, 2026-10-20).
    /// </summary>
    public static HudRect PlaceAlert(int slot, int slots, in HudMetrics screen, float floor)
    {
        float s = screen.PixelScale, margin = 4f * s, size = HudMetrics.IconPixels * s, step = screen.Tile + 2f * s;
        float needed = (Math.Max(slots, slot) - 1) * step + size;
        float fit = needed > 0f ? Math.Clamp((floor - 2f * margin) / needed, 0.2f, 1f) : 1f;
        float shown = size * fit;
        return new HudRect(screen.Width - margin - shown, margin + (slot - 1) * step * fit, shown, shown);
    }

    /// <summary>Where a coordinate's tile starts (its left or bottom edge), in pixels from the left or the bottom of the screen.</summary>
    private static float TileStart(in ScreenAxis axis, float extent, float u, float s)
    {
        float tile = axis.Anchor switch
        {
            ScreenAnchor.Center => extent / 2f - u / 2f + axis.Tile * u,
            ScreenAnchor.High => extent - u + axis.Tile * u,
            _ => axis.Tile * u,
        };
        return tile + axis.Pixels * s + axis.Percent / 100f * extent;
    }
}
