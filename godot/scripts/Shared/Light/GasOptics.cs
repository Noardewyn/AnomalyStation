using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Light;

// Engine-free (compiled into the client and, linked, into tests/Anomaly.Server.Tests/Client/Light).
// DEVIATION(DV62): tg's light ignores the air. Here a beam is seen in the air it lights (BeamFog), and the gases the client
// sees (tg's visible gases, the overlays of update_visuals) change it: each scatters and absorbs light in its own measure,
// in the colour of its sprite. Water vapour scatters almost all it stops (a bright, thick beam), plasma absorbs most of what
// is not its purple (a purple beam and a purple spot), miasma absorbs (the beam dies early). A gas's strength per metre is the
// GasLayer haze's own (DV32: tg's overlay alpha reached over TilesToOpacity tiles), so the beam and the haze agree.

/// <summary>How a gas treats light: of the light it stops, the share it scatters (seen as a glow) and the share it absorbs.</summary>
/// <param name="Scatter">Scattering per unit of the gas's strength, in the gas's colour.</param>
/// <param name="Absorb">Absorption per unit of strength, of the colours the gas is not.</param>
public readonly record struct GasOptic(float Scatter, float Absorb);

/// <summary>A cell's optics: the fog it adds (Godot's density and albedo) and a beam's extinction per metre in each channel.</summary>
public readonly record struct CellOptics(float FogDensity, float AlbedoR, float AlbedoG, float AlbedoB, float ExtR, float ExtG, float ExtB)
{
    /// <summary>No gas.</summary>
    public static CellOptics Clear => default;

    /// <summary>Whether the cell has any gas that touches light.</summary>
    public bool Any => ExtR > 0f || ExtG > 0f || ExtB > 0f;
}

/// <summary>The optics of tg's visible gases (DV62).</summary>
public static class GasOptics
{
    /// <summary>The haze's DV32 length: tg's overlay opacity is reached over this many tiles (GasLayer's).</summary>
    public const float TilesToOpacity = 4f;

    /// <summary>The thickest a gas stack counts as (GasLayer's MaxOpacity).</summary>
    public const float MaxOpacity = 0.9f;

    /// <summary>The share of a gas's strength that goes into the fog (the haze already draws the rest of its look).</summary>
    public const float FogGain = 2f;

    /// <summary>
    /// The optics of a gas by its overlay state (tg's <c>gas_overlay</c>, what <see cref="MapGrid.GasKinds"/> names). Our own
    /// table: tg has none.
    /// </summary>
    public static GasOptic Of(string overlay) => overlay != null && Table.TryGetValue(overlay, out GasOptic o) ? o : Unknown;

    /// <summary>Whether the table has a row of its own for the gas (every visible gas of tg's should).</summary>
    public static bool Known(string overlay) => overlay != null && Table.ContainsKey(overlay);

    private static readonly GasOptic Unknown = new(0.6f, 0.6f);

    private static readonly Dictionary<string, GasOptic> Table = new()
    {
        ["water_vapor"] = new(1.0f, 0.1f),     // steam: a white, bright, thick beam
        ["freon"] = new(0.9f, 0.2f),           // freon and hyper-noblium: an icy fog
        ["nitrous_oxide"] = new(0.8f, 0.3f),   // a pale haze
        ["plasma"] = new(0.5f, 1.0f),          // purple: the rest absorbed
        ["tritium"] = new(0.6f, 0.7f),         // green
        ["nitrium"] = new(0.5f, 0.8f),
        ["healium"] = new(0.6f, 0.6f),
        ["halon"] = new(0.6f, 0.6f),
        ["proto_nitrate"] = new(0.5f, 0.8f),
        ["zauker"] = new(0.4f, 1.0f),
        ["antinoblium"] = new(0.3f, 1.2f),
        ["miasma"] = new(0.2f, 2.0f),          // dark: it eats the light
    };

    /// <summary>A gas's strength per metre at its overlay level: the haze's density of that overlay alone.</summary>
    public static float Strength(int level, float coverage, float tileMetres)
    {
        if (level <= 0 || coverage <= 0f || tileMetres <= 0f) return 0f;
        float a = Math.Clamp(GasVisual.Alpha(level) / 255f * coverage, 0f, MaxOpacity);
        return -MathF.Log(1f - a) / (TilesToOpacity * tileMetres);
    }

    /// <summary>
    /// A cell's optics from its gases. <paramref name="look"/> gives a kind's overlay state, its sprite's colour (0..1) and how
    /// much of the tile the sprite covers.
    /// </summary>
    public static CellOptics Cell(IReadOnlyList<GasLevel> gases, Func<int, GasLook> look, float tileMetres)
    {
        if (gases == null || gases.Count == 0) return CellOptics.Clear;
        float er = 0f, eg = 0f, eb = 0f, fog = 0f, sr = 0f, sg = 0f, sb = 0f;
        foreach (GasLevel gas in gases)
        {
            GasLook l = look(gas.Kind);
            float d = Strength(gas.Level, l.Coverage, tileMetres);
            if (d <= 0f) continue;
            GasOptic o = Of(l.Overlay);
            // A channel of the gas's colour is scattered, the rest absorbed.
            er += d * (o.Scatter * l.R + o.Absorb * (1f - l.R));
            eg += d * (o.Scatter * l.G + o.Absorb * (1f - l.G));
            eb += d * (o.Scatter * l.B + o.Absorb * (1f - l.B));
            float stop = d * (o.Scatter + o.Absorb * 0.5f);
            fog += stop;
            sr += d * o.Scatter * l.R;
            sg += d * o.Scatter * l.G;
            sb += d * o.Scatter * l.B;
        }
        if (fog <= 0f) return CellOptics.Clear;
        return new CellOptics(fog * FogGain, Math.Min(1f, sr / fog), Math.Min(1f, sg / fog), Math.Min(1f, sb / fog), er, eg, eb);
    }

    /// <summary>
    /// The share of a beam's light in each channel that gets through the gases from (<paramref name="x"/>, <paramref name="y"/>)
    /// (tiles) along the unit heading for <paramref name="rangeTiles"/> tiles: Beer-Lambert over the cells, sampled every
    /// <paramref name="stepTiles"/>, weighted towards the near part (the far part lights less of what is seen).
    /// </summary>
    public static (float R, float G, float B) Transmittance(Func<int, int, CellOptics> at, float x, float y, float headingX, float headingY,
        float rangeTiles, float tileMetres, float stepTiles = 0.5f)
    {
        float tr = 1f, tg = 1f, tb = 1f;
        float sumR = 0f, sumG = 0f, sumB = 0f, weights = 0f;
        int steps = Math.Max(1, (int)MathF.Ceiling(rangeTiles / stepTiles));
        float metres = stepTiles * tileMetres;
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) * stepTiles;
            int cx = (int)MathF.Round(x + headingX * t), cy = (int)MathF.Round(y + headingY * t);
            CellOptics c = at(cx, cy);
            if (c.Any)
            {
                tr *= MathF.Exp(-c.ExtR * metres);
                tg *= MathF.Exp(-c.ExtG * metres);
                tb *= MathF.Exp(-c.ExtB * metres);
            }
            // the light reaching this far, weighted by how near it is (1 at the start, falling to a third at the end)
            float w = 1f - 0.66f * (i / (float)steps);
            sumR += tr * w;
            sumG += tg * w;
            sumB += tb * w;
            weights += w;
        }
        return (sumR / weights, sumG / weights, sumB / weights);
    }
}

/// <summary>What the client knows of a gas kind: its overlay state, its sprite's mean colour (0..1) and how much of the tile it covers.</summary>
public readonly record struct GasLook(string Overlay, float R, float G, float B, float Coverage);
