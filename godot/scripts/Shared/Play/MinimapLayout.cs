using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// D315: what the minimap draws of a level's <see cref="MinimapMessage"/>, made once (the map is static): the picture's pixels, the area
/// under a tile (tg's hover text) and the department labels (not maintenance's), each on the area's cell nearest its middle, the largest
/// areas first.
/// </summary>
public sealed class MinimapLayout
{
    /// <summary>A department's label: its code on the area's cell (world tiles) nearest the area's middle, and the area's size.</summary>
    public readonly record struct Label(string Code, int X, int Y, int Cells, int Width, int Height);

    /// <summary>The zoom steps of the mouse wheel, screen pixels a tile in per cent.</summary>
    public static readonly int[] ZoomSteps = { 100, 150, 200, 300, 400, 600, 800 };

    private readonly int[] _areas;

    public MinimapMessage Map { get; }

    /// <summary>The department labels, the largest area first.</summary>
    public IReadOnlyList<Label> Labels { get; }

    public MinimapLayout(MinimapMessage map)
    {
        Map = map;
        _areas = map.ExpandAreas();
        int count = map.AreaNames.Count;
        var sumX = new long[count + 1];
        var sumY = new long[count + 1];
        var cells = new int[count + 1];
        var box = new (int MinX, int MinY, int MaxX, int MaxY)[count + 1];
        Array.Fill(box, (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue));
        for (int i = 0; i < _areas.Length; i++)
        {
            int a = _areas[i], x = i % map.Width, y = i / map.Width;
            sumX[a] += x;
            sumY[a] += y;
            cells[a]++;
            box[a] = (Math.Min(box[a].MinX, x), Math.Min(box[a].MinY, y), Math.Max(box[a].MaxX, x), Math.Max(box[a].MaxY, y));
        }
        var labels = new List<Label>();
        for (int a = 1; a <= count; a++)
        {
            // the owner (2026-11-04): maintenance's tunnels are many and their labels hid the rooms; their grey says enough
            if (cells[a] == 0 || string.IsNullOrEmpty(map.AreaCodes[a - 1]) || map.AreaCodes[a - 1] == "MNT")
                continue;
            double midX = (double)sumX[a] / cells[a], midY = (double)sumY[a] / cells[a];
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int y = box[a].MinY; y <= box[a].MaxY; y++)
            {
                for (int x = box[a].MinX; x <= box[a].MaxX; x++)
                {
                    double d = (x - midX) * (x - midX) + (y - midY) * (y - midY);
                    if (_areas[y * map.Width + x] == a && d < bestDistance)
                        (best, bestDistance) = (y * map.Width + x, d);
                }
            }
            labels.Add(new Label(map.AreaCodes[a - 1], map.MinX + best % map.Width, map.MinY + best / map.Width, cells[a],
                box[a].MaxX - box[a].MinX + 1, box[a].MaxY - box[a].MinY + 1));
        }
        labels.Sort((l, r) => r.Cells.CompareTo(l.Cells));
        Labels = labels;
    }

    /// <summary>The picture as RGBA8 bytes, the top row (the highest y, north) first, for an image.</summary>
    public byte[] Rgba()
    {
        int[] pixels = Map.ExpandPixels();
        var bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == 0)
                continue;
            uint c = Map.Colours[pixels[i] - 1];
            int row = Map.Height - 1 - i / Map.Width;
            int at = (row * Map.Width + i % Map.Width) * 4;
            (bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]) = ((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
        }
        return bytes;
    }

    /// <summary>The name of the area on the world tile (x, y), tg's hover text; null where nothing is drawn.</summary>
    public string AreaNameAt(int x, int y)
    {
        int cx = x - Map.MinX, cy = y - Map.MinY;
        if (cx < 0 || cy < 0 || cx >= Map.Width || cy >= Map.Height)
            return null;
        int a = _areas[cy * Map.Width + cx];
        return a == 0 ? null : Map.AreaNames[a - 1];
    }

    /// <summary>
    /// The labels to draw at <paramref name="pixelsPerTile"/>: a label shows when its text fits its area's bounds and covers no label of a
    /// larger area; each comes with its text's centre in world tiles.
    /// </summary>
    public IEnumerable<Label> Visible(float pixelsPerTile, Func<string, float> textWidth, float textHeight)
    {
        var taken = new List<(float X0, float Y0, float X1, float Y1)>();
        foreach (Label label in Labels)
        {
            float w = textWidth(label.Code) / pixelsPerTile, h = textHeight / pixelsPerTile;
            if (w > label.Width || h > label.Height)
                continue;
            (float X0, float Y0, float X1, float Y1) rect = (label.X - w / 2, label.Y - h / 2, label.X + w / 2, label.Y + h / 2);
            if (taken.Exists(t => rect.X0 < t.X1 && t.X0 < rect.X1 && rect.Y0 < t.Y1 && t.Y0 < rect.Y1))
                continue;
            taken.Add(rect);
            yield return label;
        }
    }

    /// <summary>
    /// D315's dev trackers, tg's hover text of a blip (minimap_display.dm <c>get_hover_text</c>): the name of the nearest mob within a tile
    /// both ways of (x, y), by the sum of the two distances, the first of equals; null for none.
    /// </summary>
    public static string MobAt(MinimapMobsMessage mobs, int x, int y)
    {
        string closest = null;
        int best = int.MaxValue;
        foreach (MinimapMob mob in mobs?.Mobs ?? new List<MinimapMob>())
        {
            int dx = Math.Abs(mob.X - x), dy = Math.Abs(mob.Y - y);
            if (string.IsNullOrEmpty(mob.Name) || dx > 1 || dy > 1)
                continue;
            if (dx + dy < best)
                (best, closest) = (dx + dy, mob.Name);
        }
        return closest;
    }

    /// <summary>The zoom step after <paramref name="percent"/> toward <paramref name="direction"/> (+1 in, -1 out), kept in the steps.</summary>
    public static int Zoom(int percent, int direction)
    {
        int at = Array.FindIndex(ZoomSteps, s => s >= percent);
        if (at < 0)
            at = ZoomSteps.Length - 1;
        return ZoomSteps[Math.Clamp(at + Math.Sign(direction), 0, ZoomSteps.Length - 1)];
    }
}
