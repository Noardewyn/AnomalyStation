using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// Package T, slice T4a (docs/ai/specs/ui-overhaul.md section 8, DV30): the zone of every pixel of a body picture, the eyes, the mouth and the groin
// included. tg's own regions are those of its targeting doll (screen_objects.dm, /atom/movable/screen/zone_sel/proc/get_zone_at), whose doll is the
// human sprite seen from the front: on it the head is drawn in rows 23 to 29 and columns 12 to 20, and get_zone_at's eyes are rows 25 to 27, columns
// 15 to 17 (14 to 18 on row 26), its mouth rows 23 and 24, columns 15 to 17, its groin rows 10 to 13. Here they are placed relative to the head and
// the chest as they are drawn in the frame, so a short, a tall or a non-human body keeps them on its face and hips. Hands and feet are no regions:
// nothing in tg reads its precise hand and foot zones as a selected zone, so they stay part of the arm and the leg.

/// <summary>
/// The zone of each pixel of a body picture's untransformed icon square in one frame (tg's icon coordinates, 1 to <see cref="Side"/> from the lower
/// left): the body part drawn on top there, refined by tg's eyes, mouth and groin. Made once per picture and frame (<see cref="BodyZoneAim.ZoneMap"/>);
/// a click or a look is then a lookup.
/// </summary>
public sealed class BodyZoneMap
{
    /// <summary>The icon square's side (tg's 32 pixels).</summary>
    public const int Side = AppearanceCompositor.DefaultIconSize;

    // tg's get_zone_at, relative to the head's top row t and middle column c: the eyes t-4 to t-2 (and c-2, c+2 on t-3), the mouth t-6 and t-5
    private const int EyesTop = 2, EyesBottom = 4, EyeLine = 3, MouthTop = 5, MouthBottom = 6;

    // tg's groin: the chest's lowest four rows (get_zone_at's rows 10 to 13)
    private const int GroinRows = 4;

    private readonly AimZone[] _zones;

    private BodyZoneMap(AimZone[] zones) => _zones = zones;

    /// <summary>The zone at tg's (<paramref name="x"/>, <paramref name="y"/>) of the untransformed icon square; None outside it or where no body part is drawn.</summary>
    public AimZone At(int x, int y) => x >= 1 && x <= Side && y >= 1 && y <= Side ? _zones[Index(x, y)] : AimZone.None;

    private static int Index(int x, int y) => x - 1 + (y - 1) * Side;

    /// <summary>
    /// The map of a frame (<paramref name="dir"/>, BYOND's direction) from <paramref name="onTop"/>, the body part drawn on top at each pixel (six zones,
    /// index <c>(x - 1) + (y - 1) * Side</c>). The head and the chest are where they are drawn on top. The eyes and the mouth are placed on the head (the
    /// front frame: about its middle column; the side frames: the three head pixels nearest the face's front edge on each row; from behind: none), the
    /// groin on the chest's lowest rows, and each applies only where that part is on top.
    /// </summary>
    public static BodyZoneMap Build(AimZone[] onTop, byte dir)
    {
        var zones = (AimZone[])onTop.Clone();
        if (Bounds(onTop, AimZone.Head, out int left, out int right, out _, out int top))
        {
            for (int y = top - MouthBottom; y <= top - EyesTop; y++)
            {
                AimZone zone = y >= top - EyesBottom ? AimZone.Eyes : AimZone.Mouth;
                switch (dir)
                {
                    case 2: // SOUTH: the face, as on tg's doll
                        int c = (left + right) / 2;
                        for (int x = c - 1; x <= c + 1; x++)
                            Paint(x, y, zone);
                        if (y == top - EyeLine)
                        {
                            Paint(c - 2, y, zone);
                            Paint(c + 2, y, zone);
                        }
                        break;
                    case 4: // EAST: the face is the head's east edge
                    case 8: // WEST: its west edge
                        int step = dir == 4 ? -1 : 1, edge = 0;
                        for (int x = dir == 4 ? Side : 1; x >= 1 && x <= Side; x += step)
                            if (y >= 1 && y <= Side && onTop[Index(x, y)] == AimZone.Head)
                            {
                                edge = x;
                                break;
                            }
                        if (edge != 0)
                            for (int i = 0; i < 3; i++)
                                Paint(edge + i * step, y, zone);
                        break;
                    // NORTH (and anything else): the face cannot be seen, no eyes and no mouth
                }
            }
        }
        if (Bounds(onTop, AimZone.Chest, out _, out _, out int bottom, out _))
            for (int y = bottom; y < bottom + GroinRows && y <= Side; y++)
                for (int x = 1; x <= Side; x++)
                    if (onTop[Index(x, y)] == AimZone.Chest)
                        zones[Index(x, y)] = AimZone.Groin;
        return new BodyZoneMap(zones);

        void Paint(int x, int y, AimZone zone)
        {
            if (x >= 1 && x <= Side && y >= 1 && y <= Side && onTop[Index(x, y)] == AimZone.Head)
                zones[Index(x, y)] = zone;
        }
    }

    // The bounds of the pixels where part is on top; false when there are none.
    private static bool Bounds(AimZone[] onTop, AimZone part, out int left, out int right, out int bottom, out int top)
    {
        (left, right, bottom, top) = (Side + 1, 0, Side + 1, 0);
        for (int y = 1; y <= Side; y++)
            for (int x = 1; x <= Side; x++)
                if (onTop[Index(x, y)] == part)
                    (left, right, bottom, top) = (Math.Min(left, x), Math.Max(right, x), Math.Min(bottom, y), Math.Max(top, y));
        return right != 0;
    }

    /// <summary>The limb a zone belongs to, tg's <c>deprecise_zone</c>: the eyes and the mouth are the head's, the groin the chest's; any other zone is its own.</summary>
    public static AimZone LimbOf(AimZone zone) => zone switch
    {
        AimZone.Eyes or AimZone.Mouth => AimZone.Head,
        AimZone.Groin => AimZone.Chest,
        _ => zone,
    };
}
