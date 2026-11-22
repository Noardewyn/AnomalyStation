using System.Linq;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>The shape the map editor's place and erase tools work by (docs/ai/specs/map-editor.md 5.5).</summary>
public enum MapShape
{
    /// <summary>Every tile the mouse crosses.</summary>
    Free,

    /// <summary>The tiles from the one pressed to the one released.</summary>
    Line,

    /// <summary>The border of the rectangle between the two.</summary>
    Box,

    /// <summary>Every tile of that rectangle.</summary>
    FilledBox,

    /// <summary>The border of the circle (an ellipse when the rectangle is not a square) that fits in it.</summary>
    Circle,

    /// <summary>Every tile of that circle.</summary>
    FilledCircle,

    /// <summary>The zone of the clicked tile: the tiles joined to it by their sides that have its turf and its area.</summary>
    Zone,
}

/// <summary>The tiles of the shapes, as (x, y) of one level.</summary>
public static class MapShapes
{
    /// <summary>The most tiles a zone is filled over: a click on the space around a station is not an edit of every tile of the map.</summary>
    public const int ZoneLimit = 4096;

    /// <summary>
    /// The tiles of a straight line, in order from the first to the last. <paramref name="orthogonal"/>: the next tile always shares a side with the one
    /// before it (a run of pipe turns by the tile at a step's corner).
    /// </summary>
    public static List<(int X, int Y)> Line(int x0, int y0, int x1, int y1, bool orthogonal = false)
    {
        var cells = new List<(int, int)> { (x0, y0) };
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx - dy;
        (int x, int y) = (x0, y0);
        while (x != x1 || y != y1)
        {
            int twice = 2 * error;
            bool alongX = twice > -dy, alongY = twice < dx;
            if (alongX)
            {
                error -= dy;
                x += sx;
            }
            if (alongX && alongY && orthogonal)
                cells.Add((x, y));
            if (alongY)
            {
                error += dx;
                y += sy;
            }
            cells.Add((x, y));
        }
        return cells;
    }

    /// <summary>The tiles of the rectangle between two tiles: all of them, or its border.</summary>
    public static List<(int X, int Y)> Box(int x0, int y0, int x1, int y1, bool filled)
    {
        (int left, int right, int bottom, int top) = (Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
        var cells = new List<(int, int)>();
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (filled || x == left || x == right || y == bottom || y == top)
                    cells.Add((x, y));
            }
        }
        return cells;
    }

    /// <summary>
    /// The tiles of the circle that fits in the rectangle between two tiles (an ellipse when it is not a square): the tiles whose middle is inside
    /// it, or the ones of them at its border.
    /// </summary>
    public static List<(int X, int Y)> Circle(int x0, int y0, int x1, int y1, bool filled)
    {
        (int left, int right, int bottom, int top) = (Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
        double cx = (left + right) / 2.0, cy = (bottom + top) / 2.0, rx = (right - left + 1) / 2.0, ry = (top - bottom + 1) / 2.0;
        bool Inside(int x, int y)
            => x >= left && x <= right && y >= bottom && y <= top && (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1.0;
        var cells = new List<(int, int)>();
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (Inside(x, y) && (filled || !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1)))
                    cells.Add((x, y));
            }
        }
        return cells;
    }

    /// <summary>The tiles of a shape dragged from one tile to another (not the zone's: <see cref="Zone"/>).</summary>
    public static List<(int X, int Y)> Cells(MapShape shape, int x0, int y0, int x1, int y1, bool orthogonal = false) => shape switch
    {
        MapShape.Line => Line(x0, y0, x1, y1, orthogonal),
        MapShape.Box => Box(x0, y0, x1, y1, filled: false),
        MapShape.FilledBox => Box(x0, y0, x1, y1, filled: true),
        MapShape.Circle => Circle(x0, y0, x1, y1, filled: false),
        MapShape.FilledCircle => Circle(x0, y0, x1, y1, filled: true),
        _ => new List<(int, int)> { (x1, y1) },
    };

    /// <summary>
    /// The zone of a tile: the tiles joined to it by their sides whose turf and area are its own (their types: a room's floor up to its walls, a
    /// wall's run, the space around a station). Null when the zone has more than <paramref name="limit"/> tiles.
    /// </summary>
    public static List<(int X, int Y)> Zone(MapDocument map, int x, int y, int z, int limit = ZoneLimit)
    {
        ArgumentNullException.ThrowIfNull(map);
        var cells = new List<(int, int)>();
        if (!map.Contains(x, y, z))
            return cells;
        var kinds = new Dictionary<MapModel, string>(ReferenceEqualityComparer.Instance);
        string KindOf(MapModel tile)
        {
            if (!kinds.TryGetValue(tile, out string kind))
                kinds[tile] = kind = string.Join(" | ", tile.Members.Where(m => !MapEdits.IsThing(m.Path)).Select(m => m.Path));
            return kind;
        }
        string wanted = KindOf(map[x, y, z]);
        var seen = new HashSet<(int, int)> { (x, y) };
        var open = new Queue<(int X, int Y)>();
        open.Enqueue((x, y));
        while (open.Count > 0)
        {
            (int cx, int cy) = open.Dequeue();
            cells.Add((cx, cy));
            if (cells.Count > limit)
                return null;
            foreach ((int nx, int ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
            {
                if (map.Contains(nx, ny, z) && seen.Add((nx, ny)) && KindOf(map[nx, ny, z]) == wanted)
                    open.Enqueue((nx, ny));
            }
        }
        return cells;
    }
}
