namespace Anomaly.Protocol;

// Protocol version 84 (gaps E2a, D327; the codec: Codec.V84.cs).
// - WeatherMessage (server to client, State, after the tick's Delta): the weather running on the level the player's mob stands on, sent
//   when it changes (a weather starts, changes stage, grows or fades, ends) and once when the mob comes to a level. An empty list clears it.

/// <summary>Version 84: the weathers of a level (tg's SSweather.processing on it), each with the cells it covers.</summary>
public sealed class WeatherMessage
{
    /// <summary>The z-level, 1 to <see cref="Limits.MaxMapLevels"/>.</summary>
    public int Z { get; set; }

    /// <summary>The level's running weathers, at most <see cref="Limits.MaxWeatherLayers"/> (none: clear skies).</summary>
    public List<WeatherLayer> Layers { get; } = new();
}

/// <summary>
/// Version 84: one running weather, as tg draws it: the overlay state of its stage (tg's <c>weather_effects.dmi</c> states:
/// <c>ash_storm</c>, <c>rain_high</c>, <c>snow_storm</c>, <c>dust_high</c>, <c>lava</c>, ...), its colour and alpha, whether it glows, and
/// the particle weathers' particles with their severity and wind. <see cref="Cells"/> are runs of 0 (clear) and 1 (covered) over the box
/// <see cref="MinX"/>, <see cref="MinY"/>, <see cref="Width"/>, <see cref="Height"/>, x fastest (an empty list covers nothing).
/// </summary>
public sealed class WeatherLayer
{
    /// <summary>The weather's id, stable while it runs (a new weather has a new id).</summary>
    public int Id { get; set; }

    /// <summary>tg's stage: 1 the telegraph, 2 the main stage, 3 the wind down.</summary>
    public int Stage { get; set; }

    /// <summary>The overlay state of the stage ("" for none: the radiation storm's telegraph).</summary>
    public string State { get; set; } = "";

    /// <summary>The colour, <c>0xRRGGBBAA</c> (0 for none: the state's own colours).</summary>
    public uint Colour { get; set; }

    /// <summary>The overlay's alpha, 0 to 255.</summary>
    public int Alpha { get; set; } = 255;

    /// <summary>Whether tg also draws the state's glowing overlay over the lighting.</summary>
    public bool Glow { get; set; }

    /// <summary>The particle weathers' particle set ("" for none; tg's <c>/particles/weather/...</c> leaf: "rain_storm", "ash_storm", ...).</summary>
    public string Particles { get; set; } = "";

    /// <summary>The particle weathers' severity, 0 to 100.</summary>
    public int Severity { get; set; }

    /// <summary>The wind's sign: -1 blows west, 1 east, 0 none.</summary>
    public int Wind { get; set; }

    /// <summary>The world x of the box's first column.</summary>
    public int MinX { get; set; } = 1;

    /// <summary>The world y of the box's first row.</summary>
    public int MinY { get; set; } = 1;

    /// <summary>Columns of the box.</summary>
    public int Width { get; set; } = 1;

    /// <summary>Rows of the box.</summary>
    public int Height { get; set; } = 1;

    /// <summary>The covered cells of the box as runs of 0 and 1.</summary>
    public List<TurfRun> Cells { get; } = new();

    /// <summary><c>Width * Height</c>.</summary>
    public int CellCount => Width * Height;

    /// <summary>The cells expanded, one 0 or 1 per cell of the box, x fastest (an empty list of runs is all clear).</summary>
    public bool[] ExpandCells()
    {
        var cells = new bool[CellCount];
        int at = 0;
        foreach (TurfRun run in Cells)
        {
            if (run.TypeIndex != 0)
                Array.Fill(cells, true, at, run.Count);
            at += run.Count;
        }
        return cells;
    }
}
