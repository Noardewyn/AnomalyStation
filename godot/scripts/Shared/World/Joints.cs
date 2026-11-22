namespace Anomaly.Client.Shared.World;

// Owner: M3 R3 (D121). Engine-free. The joints of the flat things in 3D: tg draws a window, a grille or a wall in the top-down view as one of 47 junction pictures, one
// per way the neighbours join; in first person the owner wants ONE picture and the joint as geometry: a full-tile window or grille is a centre post plus an arm
// toward each connected neighbour, and the border pieces (directional windows, railings, border firelocks) that meet at a corner get a corner post.

/// <summary>
/// The arms of a full-tile window or a grille. The bits are the BYOND directions (<see cref="North"/> 1, <see cref="South"/> 2, <see cref="East"/> 4, <see cref="West"/> 8);
/// the mesh's arm parts carry the same numbers (<c>GeoQuad.Arm</c>) and the shader draws the arms whose bit the instance has.
/// </summary>
public static class Joints
{
    public const byte North = ByondDirConst.North, South = ByondDirConst.South, East = ByondDirConst.East, West = ByondDirConst.West;

    /// <summary>Both arms of the east-west run, and of the north-south one.</summary>
    public const byte EastWest = East | West, NorthSouth = North | South;

    /// <summary>
    /// The arms of the window or grille at (x, y, z): one toward each neighbour <paramref name="connects"/> says joins (a wall, a full-tile window, a grille, a door
    /// frame). A run that joins only along one axis keeps its other arm too (the end of a run still spans its tile, as the old slab did); a thing with no neighbour
    /// at all takes the axis the door rule gives it (<see cref="DoorAxis.Choose"/>: across the passage it closes).
    /// </summary>
    public static byte Arms(int x, int y, int z, Func<int, int, int, bool> connects, Func<int, int, int, CellClass> classify)
    {
        int arms = (connects(x, y + 1, z) ? North : 0) | (connects(x, y - 1, z) ? South : 0) | (connects(x + 1, y, z) ? East : 0) | (connects(x - 1, y, z) ? West : 0);
        if (arms == 0)
            return DoorAxis.Choose(x, y, z, classify) == ByondDirConst.East ? NorthSouth : EastWest;
        bool northSouth = (arms & NorthSouth) != 0, eastWest = (arms & EastWest) != 0;
        if (northSouth && !eastWest)
            arms |= NorthSouth;
        else if (eastWest && !northSouth)
            arms |= EastWest;
        return (byte)arms;
    }
}

/// <summary>
/// M5 slice 5 (D142): tg's smoothing junction of a full-tile window (<c>icon_state = "[base_icon_state]-[smoothing_junction]"</c>, icon_smoothing.dm:285; the
/// cardinal bits are the BYOND directions). The client joins a window to its neighbours by the D121 rule (<see cref="Joints.Arms"/>); between two full-tile windows
/// the arm follows tg's junction, which the server's smoothing keeps: a window does not smooth with an unanchored neighbour (icon_smoothing.dm:201), so a window
/// placed on a grille, or unscrewed from the floor, stands loose until it is fastened (its neighbours' arms toward it go).
/// </summary>
public static class WindowJunction
{
    /// <summary>The junction of a smoothed icon state (<c>"window-12"</c> is 12); false when the state carries none.</summary>
    public static bool TryParse(string iconState, out int junction)
    {
        junction = 0;
        if (string.IsNullOrEmpty(iconState))
            return false;
        int dash = iconState.LastIndexOf('-');
        return dash >= 0 && dash < iconState.Length - 1
            && int.TryParse(iconState.AsSpan(dash + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out junction);
    }

    /// <summary>The planner's "joins" test for the full-tile window <paramref name="e"/>: a neighbouring full-tile window joins only when the junction has its bit.</summary>
    public static Func<int, int, int, bool> Joins(PlanEntity e, PlanProbe probe)
    {
        Func<int, int, int, bool> joins = probe.Joins;
        if (probe.IsFullWindow == null || !TryParse(e.IconState, out int junction))
            return joins;
        return (x, y, z) =>
        {
            if (!joins(x, y, z))
                return false;
            if (!probe.IsFullWindow(x, y, z))
                return true;
            int bit = y > e.Y ? ByondDirConst.North : y < e.Y ? ByondDirConst.South : x > e.X ? ByondDirConst.East : ByondDirConst.West;
            return (junction & bit) != 0;
        };
    }
}

/// <summary>
/// Where the border pieces of a map meet (the directional windows, the railings, the border firelocks and the windoors: a piece stands on the edge of its tile its
/// dir faces). A piece's two ends are at tile corners; where a piece of the OTHER axis also ends at that corner (in the same tile or the neighbouring ones) the two
/// would leave a notch, so the piece gets a corner post there. A corner is named in doubled tile coordinates (2 x + 1 is the east edge of column x), so that it is an exact integer.
/// </summary>
public sealed class BorderCorners
{
    /// <summary>The post of the low (local -x) end and of the high (+x) end of a piece (the bits of <see cref="Placed.Joints"/> of a border piece).</summary>
    public const byte LowEnd = 1, HighEnd = 2;

    private const byte Horizontal = 1, Vertical = 2;

    private readonly Dictionary<(int X, int Y, int Z), byte> _axes = new();

    /// <summary>The corners that have a piece of an axis (for the statistics and tests).</summary>
    public int CornerCount => _axes.Count;

    /// <summary>True for the shapes whose pieces stand on a tile edge (they count at a corner), whether or not they get posts of their own.</summary>
    public static bool IsBorderShape(RenderKind shape) => shape is RenderKind.DirectionalWindow or RenderKind.Railing or RenderKind.FirelockBorder or RenderKind.Windoor;

    /// <summary>True for the shapes that get corner posts (a windoor is a door: it stands at a corner but has none).</summary>
    public static bool GetsPosts(RenderKind shape) => shape is RenderKind.DirectionalWindow or RenderKind.Railing or RenderKind.FirelockBorder;

    /// <summary>The ends of a piece on the tile (x, y) at the face <paramref name="face"/>: its low end and its high end as corners.</summary>
    public static ((int X, int Y) Low, (int X, int Y) High) Ends(int x, int y, byte face)
    {
        (int outX, int outY) = WallMounts.Step(face);
        // The piece is built for the NORTH face and turned to its own: its +x runs east for NORTH, west for SOUTH, south for EAST and north for WEST.
        (int alongX, int alongY) = face switch
        {
            ByondDirConst.North => (1, 0),
            ByondDirConst.South => (-1, 0),
            ByondDirConst.East => (0, -1),
            _ => (0, 1),
        };
        int cx = 2 * x + outX, cy = 2 * y + outY;
        return ((cx - alongX, cy - alongY), (cx + alongX, cy + alongY));
    }

    /// <summary>Registers a border piece on the tile (x, y, z) at <paramref name="face"/>.</summary>
    public void Add(int x, int y, int z, byte face)
    {
        ((int X, int Y) low, (int X, int Y) high) = Ends(x, y, face);
        byte axis = face is ByondDirConst.North or ByondDirConst.South ? Horizontal : Vertical;
        foreach ((int cx, int cy) in new[] { low, high })
            _axes[(cx, cy, z)] = (byte)(_axes.GetValueOrDefault((cx, cy, z)) | axis);
    }

    /// <summary>The posts the piece on (x, y, z) at <paramref name="face"/> gets: <see cref="LowEnd"/> and <see cref="HighEnd"/> where a piece of the other axis ends at that corner.</summary>
    public byte PostsFor(int x, int y, int z, byte face)
    {
        ((int X, int Y) low, (int X, int Y) high) = Ends(x, y, face);
        byte other = face is ByondDirConst.North or ByondDirConst.South ? Vertical : Horizontal;
        int posts = 0;
        if ((_axes.GetValueOrDefault((low.X, low.Y, z)) & other) != 0)
            posts |= LowEnd;
        if ((_axes.GetValueOrDefault((high.X, high.Y, z)) & other) != 0)
            posts |= HighEnd;
        return (byte)posts;
    }
}
