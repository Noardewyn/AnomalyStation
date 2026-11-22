namespace Anomaly.Client.Shared.World;

// Owner: D298. Engine-free. The post-processing look's choices (the owner, 2026-10-30, picking from the lead's survey: the colour
// grade, the dark distance fog, the film grain and vignette, the bloom of bright things and the edge outlines, each with a setting).

/// <summary>The Look tab's choices and the numbers they stand for.</summary>
public static class PostLook
{
    /// <summary>Godot's tonemappers by the options' value: linear (the look before D298), filmic, AgX.</summary>
    public static readonly (string Label, int Value)[] Tonemaps = { ("linear", 0), ("filmic", 1), ("AgX", 2) };

    /// <summary>The dark distance fog: where it starts and where it is whole, in tiles (0: off).</summary>
    public static readonly (string Label, int Tiles)[] Fogs = { ("off", 0), ("near (6 to 16 tiles)", 16), ("medium (10 to 24 tiles)", 24), ("far (16 to 40 tiles)", 40) };

    /// <summary>The bloom's threshold: how bright a pixel must be to glow (the scene's own is 1.3).</summary>
    public static readonly (string Label, int Percent)[] BloomThresholds = { ("low (0.9)", 90), ("medium (1.3)", 130), ("high (1.8)", 180) };

    /// <summary>The edge outlines' width in pixels at 1080 lines (0: off).</summary>
    public static readonly (string Label, int Width)[] Outlines = { ("off", 0), ("thin", 1), ("thick", 2) };

    /// <summary>Where a fog of <paramref name="tiles"/> starts, in tiles: two fifths of the way.</summary>
    public static int FogStart(int tiles) => tiles * 2 / 5;
}
