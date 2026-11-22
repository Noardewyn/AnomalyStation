namespace Anomaly.Client.Shared.World;

/// <summary>
/// tg's smoothing junction (<c>__DEFINES/icon_smoothing.dm</c>, <c>__HELPERS/icon_smoothing.dm</c>): a bit per neighbour that
/// joins (N 1, S 2, E 4, W 8, NE 16, SE 32, SW 64, NW 128), a diagonal counting only when both of its sides join; the
/// result picks the picture <c>base-junction</c> (wall-0 .. wall-255, 47 of which have a picture). FakeMap uses it for the
/// walls, windows and tables of the scripted map, where the real server runs tg's own SSicon_smooth (M3 P4).
/// </summary>
public static class Smoothing
{
    public const int North = 1, South = 2, East = 4, West = 8, NorthEast = 16, SouthEast = 32, SouthWest = 64, NorthWest = 128;

    /// <summary>The junction of a cell whose eight neighbours join or not.</summary>
    public static int Junction(bool n, bool s, bool e, bool w, bool ne, bool se, bool sw, bool nw)
    {
        int junction = (n ? North : 0) | (s ? South : 0) | (e ? East : 0) | (w ? West : 0);
        if (ne && n && e) junction |= NorthEast;
        if (se && s && e) junction |= SouthEast;
        if (sw && s && w) junction |= SouthWest;
        if (nw && n && w) junction |= NorthWest;
        return junction;
    }

    /// <summary>The junction of cell (x, y) given a predicate "the neighbour at (x, y) joins" (cells outside the map do not).</summary>
    public static int Junction(int x, int y, Func<int, int, bool> joins)
        => Junction(joins(x, y + 1), joins(x, y - 1), joins(x + 1, y), joins(x - 1, y),
            joins(x + 1, y + 1), joins(x + 1, y - 1), joins(x - 1, y - 1), joins(x - 1, y + 1));

    /// <summary>The straight east-west run, EAST | WEST (12): the one picture D121 draws for every window, whatever junction the server or the fake feed sends.</summary>
    public const int EastWest = East | West;

    /// <summary>
    /// The state of a smoothed picture's straight east-west run (D121: ONE picture per window type; the joint is drawn as geometry, not as one of tg's 47 junction pictures):
    /// <c>window-13</c> and <c>window-0</c> are <c>window-12</c>. Null when <paramref name="state"/> is not a base and a junction (no stem and a number), which has no such run.
    /// </summary>
    public static string StraightRunState(string state) => StraightRunState(state, EastWest);

    /// <summary>A wall's stack bits: another wall straight above it, and below it, on a linked level.</summary>
    public const byte Above = 1, Below = 2;

    /// <summary>
    /// The picture of a wall's side (D121's straight run) joined to the walls above and below it (the owner, 2026-11-10: blocks join up and down as they do along a row): the
    /// face's top is tg's NORTH, its bottom SOUTH, and its corners join too, as the row runs on both levels (<c>wall-157</c> under a wall, <c>wall-255</c> between two).
    /// </summary>
    public static int SideJunction(byte stack)
        => EastWest | ((stack & Above) != 0 ? North | NorthEast | NorthWest : 0) | ((stack & Below) != 0 ? South | SouthEast | SouthWest : 0);

    /// <summary>The state of <paramref name="state"/>'s picture with its junction replaced by <paramref name="junction"/>; null as <see cref="StraightRunState(string)"/>.</summary>
    public static string StraightRunState(string state, int junction)
    {
        if (string.IsNullOrEmpty(state))
            return null;
        // a diagonal corner wall (<base>-<junction>-diagonal) stands as the straight run on its sides too
        if (state.EndsWith("-diagonal", StringComparison.Ordinal))
            state = state.Substring(0, state.Length - "-diagonal".Length);
        int dash = state.LastIndexOf('-');
        if (dash < 1 || dash == state.Length - 1)
            return null;
        for (int i = dash + 1; i < state.Length; i++)
        {
            if (state[i] < '0' || state[i] > '9')
                return null;
        }
        return StateOf(state.Substring(0, dash), junction);
    }

    /// <summary>The icon state of a smoothed picture: <c>wall</c> and junction 5 are <c>wall-5</c>.</summary>
    public static string StateOf(string baseState, int junction) => baseState + "-" + junction.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
