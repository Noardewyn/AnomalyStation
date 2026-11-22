namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. Occluder boxes from wall runs (scope 2i): Godot's OccluderInstance3D takes a few big boxes much
// better than hundreds of tile boxes, so the walls of a chunk are merged into rectangles.

/// <summary>A box of wall cells, inclusive.</summary>
public readonly record struct WallBox(int MinX, int MinY, int MaxX, int MaxY, int Z)
{
    public int CellCount => (MaxX - MinX + 1) * (MaxY - MinY + 1);
}

/// <summary>Merges wall cells into boxes.</summary>
public static class OccluderPlanner
{
    /// <summary>
    /// Greedy merge: runs of walls along x in each row, then rows with the same run are stacked. Every wall cell is in exactly one
    /// box and no box holds anything else.
    /// </summary>
    public static List<WallBox> Merge(IEnumerable<(int X, int Y)> wallCells, int z)
    {
        var rows = new SortedDictionary<int, List<int>>();
        foreach ((int x, int y) in wallCells)
        {
            if (!rows.TryGetValue(y, out List<int> xs))
                rows[y] = xs = new List<int>();
            xs.Add(x);
        }
        var open = new Dictionary<(int, int), WallBox>(); // (minX, maxX) -> the box still growing upward
        var boxes = new List<WallBox>();
        int lastY = int.MinValue;
        foreach ((int y, List<int> xs) in rows)
        {
            xs.Sort();
            var thisRow = new Dictionary<(int, int), WallBox>();
            int i = 0;
            while (i < xs.Count)
            {
                int j = i;
                while (j + 1 < xs.Count && xs[j + 1] == xs[j] + 1)
                    j++;
                var key = (xs[i], xs[j]);
                if (y == lastY + 1 && open.TryGetValue(key, out WallBox below))
                {
                    thisRow[key] = below with { MaxY = y };
                    open.Remove(key);
                }
                else
                    thisRow[key] = new WallBox(xs[i], y, xs[j], y, z);
                i = j + 1;
            }
            boxes.AddRange(open.Values);
            open = thisRow;
            lastY = y;
        }
        boxes.AddRange(open.Values);
        boxes.Sort((a, b) => a.MinY != b.MinY ? a.MinY.CompareTo(b.MinY) : a.MinX.CompareTo(b.MinX));
        return boxes;
    }
}
