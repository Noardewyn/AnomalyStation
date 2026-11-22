using System;
using System.Numerics;

namespace Anomaly.Client.Shared;

/// <summary>Whether a cell's ship is in hyperspace, and where the view streams: tg's transit tiles around it name the flight's direction.</summary>
public static class HyperspaceSky
{
    /// <summary>How far a ship's hull may stand from the tile looked from (the largest shuttles are about 40 tiles across).</summary>
    private const int Reach = 48;

    /// <summary>
    /// The way hyperspace streams past (X east, Z south; opposite the flight, as tg's cling pulls toward <c>REVERSE_DIR(dir)</c>), or null with
    /// no transit tile the first space met along the four directions from (<paramref name="x"/>, <paramref name="y"/>). <paramref name="turfAt"/> gives a cell's turf path.
    /// </summary>
    public static Vector3? Flow(int x, int y, Func<int, int, string> turfAt)
    {
        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            for (int step = 0; step <= Reach; step++)
            {
                string path = turfAt(x + dx * step, y + dy * step);
                if (path == null)
                    break;
                if (path.StartsWith(Transit, StringComparison.Ordinal))
                    return FlowOf(path);
                // plain space first: a ship parked there, however near another ship's transit on the reserved level
                if (path.StartsWith("/turf/open/space", StringComparison.Ordinal))
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// The tile box of the ship the cell (<paramref name="x"/>, <paramref name="y"/>) stands in: every cell reached from it through turfs that
    /// are not space, within <see cref="Reach"/>. In hyperspace the view ends a little past it (the owner, 2026-11-02: other ships and
    /// their gas on the reserved level showed through the tunnel).
    /// </summary>
    public static (int MinX, int MinY, int MaxX, int MaxY) ShipBounds(int x, int y, Func<int, int, string> turfAt)
    {
        (int minX, int minY, int maxX, int maxY) = (x, y, x, y);
        var seen = new System.Collections.Generic.HashSet<(int, int)> { (x, y) };
        var open = new System.Collections.Generic.Stack<(int, int)>();
        open.Push((x, y));
        while (open.Count > 0)
        {
            (int cx, int cy) = open.Pop();
            minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
            minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                (int nx, int ny) = (cx + dx, cy + dy);
                if (Math.Abs(nx - x) > Reach || Math.Abs(ny - y) > Reach || !seen.Add((nx, ny)))
                    continue;
                string path = turfAt(nx, ny);
                if (path != null && !path.StartsWith("/turf/open/space", StringComparison.Ordinal))
                    open.Push((nx, ny));
            }
        }
        return (minX, minY, maxX, maxY);
    }

    private const string Transit = "/turf/open/space/transit";

    private static Vector3 FlowOf(string path) => path.Substring(Transit.Length).TrimStart('/') switch
    {
        "north" => Vector3.UnitZ,
        "east" => -Vector3.UnitX,
        "west" => Vector3.UnitX,
        _ => -Vector3.UnitZ, // south, tg's default dir
    };
}
