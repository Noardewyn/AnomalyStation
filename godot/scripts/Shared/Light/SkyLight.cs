namespace Anomaly.Client.Shared.Light;

// Engine-free. DEVIATION(DV221), D288: the light of space on what is open to the sky, drawn by the materials (light_grid.gdshaderinc's anomaly_sky_light):
// an ambient cube of the stars (tg's COLOR_STARLIGHT, space.dm) from above and the sides and of the lava planet under the station
// (Starfield) from below, on the cells nothing covers. It stands in for tg's starlight cells (and for six directional lights, which it
// equals for unshadowed light at no per-light cost); the sun is a Godot light of its own.

/// <summary>The colours of the light of space and the per-cell mask of where it reaches.</summary>
public static class SkyLight
{
    /// <summary>tg's <c>COLOR_STARLIGHT</c> (#8589fa).</summary>
    public static readonly (float R, float G, float B) Star = (0x85 / 255f, 0x89 / 255f, 0xfa / 255f);

    /// <summary>The lava planet's glow (the owner, 2026-10-29: redder than tg's <c>LIGHT_COLOR_LAVA</c>, a planet of magma).</summary>
    public static readonly (float R, float G, float B) Planet = (1f, 0.3f, 0.08f);

    /// <summary>The ambient cube's strengths: the stars on a face turned up and sideways, the planet on one turned sideways and down.</summary>
    public const float StarUp = 0.9f, StarSide = 0.7f, PlanetSide = 0.2f, PlanetDown = 1.1f;

    /// <summary>The cube's faces at a <paramref name="gain"/> (1: the defaults; 0: no light from space).</summary>
    public static ((float R, float G, float B) Up, (float R, float G, float B) Side, (float R, float G, float B) Down) Cube(float gain) =>
        (Scale(Star, StarUp * gain),
         (gain * (Star.R * StarSide + Planet.R * PlanetSide), gain * (Star.G * StarSide + Planet.G * PlanetSide), gain * (Star.B * StarSide + Planet.B * PlanetSide)),
         Scale(Planet, PlanetDown * gain));

    // DEVIATION(DV256): tg lights nothing on Lavaland but its light sources.
    /// <summary>D305: Lavaland's sky (tg's ZTRAIT_NOPARALLAX levels): no stars and no sun, only the ash clouds lit faintly by the lava below them
    /// (tg's <c>LIGHT_COLOR_LAVA</c>), so the wastes are dark but not black; the cube's faces at a <paramref name="gain"/>.</summary>
    public static ((float R, float G, float B) Up, (float R, float G, float B) Side, (float R, float G, float B) Down) AshCube(float gain) =>
        (Scale(Lava, AshUp * gain), Scale(Lava, AshSide * gain), (0f, 0f, 0f));

    /// <summary>tg's <c>LIGHT_COLOR_LAVA</c> (#c48a18).</summary>
    public static readonly (float R, float G, float B) Lava = (0xc4 / 255f, 0x8a / 255f, 0x18 / 255f);

    /// <summary>The ash cube's strengths, a fraction of the stars' (the light of space is gone; only a glow under the clouds stays).</summary>
    public const float AshUp = 0.3f, AshSide = 0.2f;

    private static (float, float, float) Scale((float R, float G, float B) c, float k) => (c.R * k, c.G * k, c.B * k);

    /// <summary>A cell's mask byte: 255 where it is open to the sky (<see cref="ILightCells.IsOpenToSky"/>), else 0.</summary>
    public static byte At(ILightCells cells, int x, int y, int z) => cells.IsOpenToSky(x, y, z) ? (byte)255 : (byte)0;

    /// <summary>Fills <paramref name="texels"/> (<c>(MaxX + 1) * (MaxY + 1)</c> bytes, texel (x, y) is tile (x, y)) with level <paramref name="z"/>'s mask.</summary>
    public static void Fill(byte[] texels, ILightCells cells, int z)
    {
        int w = cells.MaxX + 1;
        Array.Clear(texels);
        for (int y = 1; y <= cells.MaxY; y++)
            for (int x = 1; x <= cells.MaxX; x++)
                texels[y * w + x] = At(cells, x, y, z);
    }
}
