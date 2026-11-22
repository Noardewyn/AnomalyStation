using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// DEVIATION(DV395): the levels of a multi-z stack around a turf, for the blasts and pulls the port makes three-dimensional.

public static class LinkedLevels
{
    /// <summary>
    /// The level offsets (non-zero) of the levels linked to <paramref name="center"/>'s within <paramref name="range"/> of it, nearest first, up before down
    /// (tg's <c>GET_TURF_ABOVE</c> / <c>GET_TURF_BELOW</c>: a level not linked, or another map's, ends the walk).
    /// </summary>
    public static List<int> Around(turf center, int range)
    {
        var offsets = new List<int>();
        if (center == null || range <= 0)
            return offsets;
        turf up = center, down = center;
        for (int d = 1; d <= range; d++)
        {
            up = up == null ? null : GlobalProcs.GET_TURF_ABOVE(up) as turf;
            down = down == null ? null : GlobalProcs.GET_TURF_BELOW(down) as turf;
            if (up != null)
                offsets.Add(d);
            if (down != null)
                offsets.Add(-d);
            if (up == null && down == null)
                break;
        }
        return offsets;
    }
}
