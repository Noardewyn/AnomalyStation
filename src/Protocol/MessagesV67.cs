namespace Anomaly.Protocol;

// Protocol version 67 (D315, docs/ai/specs/protocol.md section 68; the codec: Codec.V67.cs).
// - MinimapMessage (server to client, State, after the tick's Delta): the minimap of a z-level, tg's load_z picture (minimap.dm) with each
//   drawn cell's area and the areas' departments, sent once per level and connection when the player's mob first stands on that level.

/// <summary>Version 67: a z-level's minimap, made once a round (static: later changes of the map are not in it).</summary>
public sealed class MinimapMessage
{
    /// <summary>The z-level, 1 to <see cref="Limits.MaxMapLevels"/>.</summary>
    public int Z { get; set; }

    /// <summary>The world x of the picture's first column (tg's <c>min_x</c>).</summary>
    public int MinX { get; set; }

    /// <summary>The world y of its first row (tg's <c>min_y</c>).</summary>
    public int MinY { get; set; }

    /// <summary>Columns; <c>MinX + Width - 1</c> is at most <see cref="Limits.MaxMapSize"/>.</summary>
    public int Width { get; set; }

    /// <summary>Rows; <c>MinY + Height - 1</c> is at most <see cref="Limits.MaxMapSize"/>.</summary>
    public int Height { get; set; }

    /// <summary>The colour table, <c>0xRRGGBBAA</c>: a pixel run's value 1 is <c>Colours[0]</c>, 0 is a cell tg did not draw.</summary>
    public List<uint> Colours { get; } = new();

    /// <summary>Each cell's colour as runs over the cells, x fastest, then y (an empty list is all undrawn).</summary>
    public List<TurfRun> PixelRuns { get; } = new();

    /// <summary>The areas' names (tg's hover text of a cell): an area run's value 1 is <c>AreaNames[0]</c>, 0 no area.</summary>
    public List<string> AreaNames { get; } = new();

    /// <summary>The areas' departments, one per <see cref="AreaNames"/> entry ("CMD", "SEC", ...; "" for none).</summary>
    public List<string> AreaCodes { get; } = new();

    /// <summary>Each cell's area as runs over the cells, in the order of <see cref="PixelRuns"/>.</summary>
    public List<TurfRun> AreaRuns { get; } = new();

    /// <summary><c>Width * Height</c>.</summary>
    public int CellCount => Width * Height;

    /// <summary>The pixel runs expanded, one colour index per cell (an empty list of runs is all zero).</summary>
    public int[] ExpandPixels() => Expand(PixelRuns);

    /// <summary>The area runs expanded, one area index per cell (an empty list of runs is all zero).</summary>
    public int[] ExpandAreas() => Expand(AreaRuns);

    private int[] Expand(List<TurfRun> runs)
    {
        var cells = new int[CellCount];
        int at = 0;
        foreach (TurfRun run in runs)
        {
            Array.Fill(cells, run.TypeIndex, at, run.Count);
            at += run.Count;
        }
        return cells;
    }
}
