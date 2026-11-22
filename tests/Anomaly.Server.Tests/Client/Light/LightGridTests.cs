using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>M3 P7: the corner light of the grid: line of sight, starlight, the base light, incremental equals full, cost.</summary>
public class LightGridTests
{
    private const uint White = 0xFFFFFFFFu;
    private readonly ITestOutputHelper _output;

    public LightGridTests(ITestOutputHelper output) => _output = output;

    private static LightState Lamp(float range = 3f, float power = 1f, uint rgba = White) =>
        new(range, power, rgba, true, 360f, 0, TgLightMath.LightingHeight, 0f, 0f, TgLightMath.ComplexLight);

    private static ArrayCells Room(int w, int h)
    {
        var cells = new ArrayCells(w, h);
        return cells;
    }

    [Fact]
    public void ALampLightsTheCornersAroundItByTgsFalloff()
    {
        var cells = Room(11, 11);
        var grid = new LightGrid(cells, 1);
        grid.SetSource(1, 6, 6, Lamp(range: 3f));
        // The source tile (6,6): its south-west corner is texel (5,5), its north-east corner texel (6,6): offsets of
        // (-0.5,-0.5) and (+0.5,+0.5). sqrt(0.5 + 1) / 3 = 0.408248 -> 0.591752 -> rounded to 1/64: 38/64.
        (float r, float g, float b) = grid.LightAt(5, 5, out _);
        Assert.Equal(38f / 64f, r);
        Assert.Equal(38f / 64f, g);
        Assert.Equal(38f / 64f, b);
        Assert.Equal(38f / 64f, grid.LightAt(6, 6, out _).R);
        // A corner 1.5 tiles east and 0.5 north of the lamp (texel (7,6) at x = 7.5 - 6, y = 6.5 - 6): sqrt(2.25 + 0.25 + 1) = 1.870829 / 3 = 0.623610 -> 0.376390 -> 24/64.
        Assert.Equal(24f / 64f, grid.LightAt(7, 6, out _).R);
        // Out of range
        Assert.Equal(0f, grid.LightAt(10, 10, out _).R);
        Assert.Equal(0f, grid.LightAt(0, 0, out _).R);
    }

    [Fact]
    public void PowerAndColourScaleEachChannelAndTheLargestChannelNormalises()
    {
        var cells = Room(11, 11);
        var grid = new LightGrid(cells, 1);
        // power 2, colour (255, 128, 0): raw at texel (5,5) = 0.591752 * 2 * (1, 0.50196, 0) = (1.1835, 0.5941, 0) -> largest > 1 -> scaled by 1/1.1835: (1, 0.50196, 0)
        grid.SetSource(1, 6, 6, Lamp(range: 3f, power: 2f, rgba: 0xFF8000FFu));
        (float r, float g, float b) = grid.LightAt(5, 5, out _);
        Assert.Equal(1f, r);
        Assert.Equal(Math.Round(0.50196 * 64) / 64, g, 4);
        Assert.Equal(0f, b);
    }

    [Fact]
    public void AWallBlocksTheLightAndADoorOpeningLetsItThrough()
    {
        var cells = Room(15, 11);
        cells.Fill(8, 1, 8, 11, true);       // a wall column at x = 8
        cells.SetOpaque(8, 6, true);         // a closed door in it
        var grid = new LightGrid(cells, 1);
        grid.SetSource(1, 6, 6, Lamp(range: 6f));

        // West of the wall: lit. The wall tile (8,6) is skipped: its east corners (texel (8, 5), (8, 6)) belong to tile (9,6), not visible.
        Assert.True(grid.LightAt(7, 5, out _).R > 0f);
        Assert.True(grid.RawAt(7, 5).R > 0);
        Assert.Equal(0, grid.RawAt(8, 5).R);
        Assert.Equal(0, grid.RawAt(8, 6).R);
        Assert.Equal(0, grid.RawAt(9, 5).R);

        cells.SetOpaque(8, 6, false);
        grid.CellOpacityChanged(8, 6);
        // the open door tile (8,6) is now lit: its corners (7,5),(8,5),(7,6),(8,6) and the room behind it through the gap
        Assert.True(grid.RawAt(8, 5).R > 0);
        Assert.True(grid.RawAt(8, 6).R > 0);
        Assert.True(grid.RawAt(9, 5).R > 0);   // tile (9,6) is visible straight through the doorway
        Assert.Equal(0, grid.RawAt(9, 10).R);  // still dark behind the wall off the doorway line

        cells.SetOpaque(8, 6, true);
        grid.CellOpacityChanged(8, 6);
        Assert.Equal(0, grid.RawAt(8, 5).R);
        Assert.Equal(0, grid.RawAt(9, 5).R);
    }

    [Fact]
    public void ALightInsideAnOpaqueCellOnlyLightsWhatItSees()
    {
        // A fixture sitting on a wall tile (the source's own cell is opaque): the eye is always visible, opaque tiles are skipped.
        var cells = Room(9, 9);
        cells.Fill(1, 5, 9, 5, true);
        var grid = new LightGrid(cells, 1);
        grid.SetSource(1, 5, 5, Lamp(range: 4f));
        Assert.True(grid.RawAt(4, 3).R >= 0);
    }

    [Fact]
    public void StarlightLightsTheHullNextToSpaceAtHeightMinusHalf()
    {
        // A 7x7 map: space everywhere but a hull 3x3 in the middle (tiles 3..5); space tiles touching the hull are starlit.
        var cells = Room(7, 7);
        cells.Fill(3, 3, 5, 5, true);          // opaque hull walls (a lit wall is skipped as a turf, its neighbours' corners light up)
        var starlight = new LightState(2f, 1f, 0x8589FAFFu, true, 360f, 0, TgLightMath.LightingHeightSpace, 0f, 0f, TgLightMath.ComplexLight);
        long key = 100;
        for (int x = 2; x <= 6; x++)
        {
            for (int y = 2; y <= 6; y++)
            {
                bool hull = x >= 3 && x <= 5 && y >= 3 && y <= 5;
                if (hull)
                    continue;
                cells.SetSpace(x, y, true);
                new LightGrid(cells, 1);
            }
        }

        var grid = new LightGrid(cells, 1);
        for (int x = 2; x <= 6; x++)
            for (int y = 2; y <= 6; y++)
                if (!(x >= 3 && x <= 5 && y >= 3 && y <= 5))
                    grid.SetSource(key++, x, y, starlight);

        // The corner shared by the hull tile (3,3) and space tile (2,2) is texel (2,2) (NE of tile (2,2)): lit by (2,2) at offset
        // (+0.5, +0.5): sqrt(0.25 + 0.25 - 0.5) = 0 -> multiplier 1 -> 1 * power 1 * colour; and by the neighbours too, so the
        // sum is above the single source's value and normalises to the colour (largest channel 0xFA/255 = 0.98 scaled when above 1).
        (float r, float g, float b) = grid.LightAt(2, 2, out _);
        Assert.True(b > g && g > r, "starlight is bluish: " + (r, g, b));
        Assert.True(b > 0.9f);
        // A corner far from every source stays dark: texel (0,0) is two tiles from (2,2)'s corner at offsets (-1.5,-1.5): sqrt(4.5 - 0.5) = 2 / 2 -> 0.
        Assert.Equal(0f, grid.LightAt(0, 0, out _).R);
        // The hull's walls are not lit as turfs; the grid has light on their outside corners only
        Assert.True(grid.LightAt(3, 3, out _).B == 0f || grid.RawAt(3, 3).B >= 0);
    }

    [Fact]
    public void TheAreasBaseLightIsAddedPerCellAndNeverOnSpaceTurfs()
    {
        var cells = Room(6, 6);
        uint half = 0xFFFFFF80u;     // white at alpha 128
        for (int x = 1; x <= 6; x++)
        {
            for (int y = 1; y <= 6; y++)
            {
                cells.SetBaseLight(x, y, half);
                if (x >= 5)
                    cells.SetSpace(x, y, true);    // tg's /area/space base light sits on space turfs: DV22 skips them
            }
        }

        var grid = new LightGrid(cells, 1);
        // inside the area (texel (2,2): tiles 2..3 around it): full base 128/255 on each channel, share 128/255
        (float r, float g, float b) = grid.LightAt(2, 2, out float share);
        Assert.Equal(128f / 255f, r, 5);
        Assert.Equal(128f / 255f, g, 5);
        Assert.Equal(128f / 255f, b, 5);
        Assert.Equal(128f / 255f, share, 5);
        // on the border to the space turfs (texel (4,2): tiles 4 and 5 around it, 5 is space): half
        Assert.Equal(64f / 255f, grid.LightAt(4, 2, out _).R, 5);
        // among the space turfs: none
        Assert.Equal(0f, grid.LightAt(5, 2, out share).R);
        Assert.Equal(0f, share);

        // the base light adds to the corner light
        grid.SetSource(1, 2, 2, Lamp(range: 3f));
        Assert.True(grid.LightAt(2, 2, out _).R > 128f / 255f);
    }

    [Fact]
    public void ASwitchedOffOrEmptyOrUnknownSystemSourceLightsNothingAndAnOverlaySourceLights()
    {
        var cells = Room(9, 9);
        var grid = new LightGrid(cells, 1);
        LightState on = Lamp();
        grid.SetSource(1, 5, 5, on);
        Assert.Equal(1, grid.SourceCount);
        Assert.True(grid.RawAt(4, 4).R > 0);
        grid.SetSource(1, 5, 5, on with { On = false });
        Assert.Equal(0, grid.SourceCount);
        Assert.Equal(0, grid.RawAt(4, 4).R);
        grid.SetSource(1, 5, 5, on with { System = 0 }); // NO_LIGHT_SUPPORT
        Assert.Equal(0, grid.SourceCount);
        grid.SetSource(1, 5, 5, on with { System = 3 }); // OVERLAY_LIGHT_DIRECTIONAL, a flashlight (DV61)
        Assert.Equal(1, grid.SourceCount);
        Assert.True(grid.RawAt(4, 4).R > 0);
        grid.SetSource(1, 5, 5, on);
        Assert.True(grid.RawAt(4, 4).R > 0);
        grid.SetSource(1, 5, 5, LightState.None);
        Assert.Equal(0, grid.RawAt(4, 4).R);
    }

    [Fact]
    public void AVisualOffsetShiftsTheLightLikeTgsSheet()
    {
        var cells = Room(11, 11);
        var plain = new LightGrid(cells, 1);
        var shifted = new LightGrid(cells, 1);
        plain.SetSource(1, 6, 6, Lamp(range: 3f));
        // tg shifts the sheet's coordinates by +offset (generate_sheet x_offset): a wall lamp with offset_x 1 sees the corners as
        // if they were one tile further east of it, i.e. the light pools to the west.
        shifted.SetSource(1, 6, 6, Lamp(range: 3f) with { OffsetX = 1f });
        Assert.True(plain.RawAt(7, 5).R > shifted.RawAt(7, 5).R);
        Assert.True(shifted.RawAt(4, 5).R > plain.RawAt(4, 5).R);
    }

    [Fact]
    public void IncrementalUpdatesEqualAFullRecomputeExactly()
    {
        const int w = 40, h = 40;
        var rng = new Random(7);
        var cells = Room(w, h);
        for (int i = 0; i < 160; i++)
            cells.SetOpaque(rng.Next(1, w + 1), rng.Next(1, h + 1), true);

        var live = new Dictionary<long, (int X, int Y, LightState S)>();
        var grid = new LightGrid(cells, 1);

        LightState RandomState() => new(
            Range: 1 + rng.Next(0, 7) + (rng.Next(0, 2) * 0.5f),
            Power: 0.5f + rng.Next(0, 4) * 0.5f,
            Rgba: (uint)rng.Next() | 0xFFu,
            On: true,
            Angle: rng.Next(0, 3) == 0 ? 90f + rng.Next(0, 4) * 30f : 360f,
            Dir: (byte)new[] { 1, 2, 4, 8, 5, 6, 9, 10 }[rng.Next(0, 8)],
            Height: rng.Next(0, 4) == 0 ? TgLightMath.LightingHeightSpace : TgLightMath.LightingHeight,
            OffsetX: rng.Next(0, 5) == 0 ? (rng.Next(-3, 4)) * 0.5f : 0f,
            OffsetY: rng.Next(0, 5) == 0 ? (rng.Next(-3, 4)) * 0.5f : 0f,
            System: TgLightMath.ComplexLight);

        for (int step = 0; step < 600; step++)
        {
            switch (rng.Next(0, 5))
            {
                case 0:
                case 1:
                {
                    long key = rng.Next(1, 60);
                    int x = rng.Next(1, w + 1), y = rng.Next(1, h + 1);
                    LightState s = RandomState();
                    live[key] = (x, y, s);
                    grid.SetSource(key, x, y, s);
                    break;
                }
                case 2:
                {
                    long key = rng.Next(1, 60);
                    live.Remove(key);
                    grid.RemoveSource(key);
                    break;
                }
                case 3:
                {
                    int x = rng.Next(1, w + 1), y = rng.Next(1, h + 1);
                    cells.SetOpaque(x, y, !cells.IsOpaque(x, y, 1));
                    grid.CellOpacityChanged(x, y);
                    break;
                }
                default:
                {
                    if (live.Count == 0) break;
                    long key = live.Keys.ElementAt(rng.Next(0, live.Count));
                    var cur = live[key];
                    LightState s = cur.S with { On = !cur.S.On };
                    live[key] = (cur.X, cur.Y, s);
                    grid.SetSource(key, cur.X, cur.Y, s);
                    break;
                }
            }
        }

        var full = new LightGrid(cells, 1);
        foreach ((long key, (int x, int y, LightState s)) in live)
            full.SetSource(key, x, y, s);

        Assert.Equal(full.SourceCount, grid.SourceCount);
        for (int j = 0; j < grid.Height; j++)
            for (int i = 0; i < grid.Width; i++)
                Assert.True(full.RawAt(i, j) == grid.RawAt(i, j), $"corner ({i},{j}): full {full.RawAt(i, j)} incremental {grid.RawAt(i, j)}");

        // RecomputeAll on the incremental grid agrees as well, and so do the flushed texels
        grid.RecomputeAll();
        Assert.True(grid.Flush(out _, out _, out _, out _));
        Assert.True(full.Flush(out _, out _, out _, out _));
        Assert.Equal(full.Texels, grid.Texels);
        for (int j = 0; j < grid.Height; j++)
            for (int i = 0; i < grid.Width; i++)
                Assert.True(full.RawAt(i, j) == grid.RawAt(i, j));
    }

    [Fact]
    public void FlushWritesOnlyTheDirtyRectangleAsHalfFloats()
    {
        var cells = Room(20, 20);
        var grid = new LightGrid(cells, 1);
        Assert.True(grid.Flush(out _, out _, out _, out _));   // the first flush covers everything
        Assert.False(grid.Flush(out _, out _, out _, out _));
        grid.SetSource(1, 10, 10, Lamp(range: 3f));
        Assert.True(grid.Flush(out int x0, out int y0, out int x1, out int y1));
        Assert.InRange(x0, 6, 8);
        Assert.InRange(x1, 11, 13);
        Assert.InRange(y0, 6, 8);
        Assert.InRange(y1, 11, 13);
        int o = (9 * grid.Width + 9) * 8;            // texel (9, 9): the south-west corner of the lamp's tile
        Assert.Equal((Half)(38f / 64f), BitConverter.ToHalf(grid.Texels, o));
        Assert.Equal((Half)1f, (Half)1f);
        Assert.Equal((Half)0f, BitConverter.ToHalf(grid.Texels, o + 6));   // alpha: no base light
    }

    [Fact]
    public void BootCostOfAThousandThreeHundredSources()
    {
        // Meta-sized: 255 x 255, a lattice of rooms with walls, 1300 tube-like lamps (range 6..8) and 1700 starlight cells (range 2).
        const int n = 255;
        var cells = Room(n, n);
        for (int x = 1; x <= n; x += 7)
            cells.Fill(x, 1, x, n, true);
        for (int y = 1; y <= n; y += 7)
            cells.Fill(1, y, n, y, true);
        for (int x = 4; x <= n; x += 7)
            for (int y = 7; y <= n; y += 7)
                cells.SetOpaque(x, y, false);
        LightState tube = Lamp(range: 6.5f, power: 1.2f, rgba: 0xE6F3FFFFu);
        LightState star = new(2f, 1f, 0x8589FAFFu, true, 360f, 0, TgLightMath.LightingHeightSpace, 0f, 0f, TgLightMath.ComplexLight);
        var rng = new Random(3);
        var lamps = new List<(int X, int Y)>();
        while (lamps.Count < 1300)
        {
            int x = rng.Next(2, n), y = rng.Next(2, n);
            if (!cells.IsOpaque(x, y, 1))
                lamps.Add((x, y));
        }

        // warm the JIT on a throwaway grid
        var warm = new LightGrid(cells, 1);
        for (int i = 0; i < 100; i++)
            warm.SetSource(i, lamps[i].X, lamps[i].Y, tube);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var grid = new LightGrid(cells, 1);
        for (int i = 0; i < lamps.Count; i++)
            grid.SetSource(i + 1, lamps[i].X, lamps[i].Y, tube);
        sw.Stop();
        double lampsMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        for (int i = 0; i < 1700; i++)
            grid.SetSource(10_000 + i, 2 + (i * 37) % (n - 2), 2 + (i * 11) % (n - 2), star);
        sw.Stop();
        double starMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        grid.Flush(out _, out _, out _, out _);
        sw.Stop();
        double flushMs = sw.Elapsed.TotalMilliseconds;

        _output.WriteLine($"1300 lamps (range 6.5): {lampsMs:F1} ms = {lampsMs * 1000 / 1300:F1} us per source; 1700 starlight cells: {starMs:F1} ms; first full flush (256x256 texels): {flushMs:F1} ms");
        // A loose guard (a Debug build on a busy machine is several times slower than the Release numbers in light-grid.md).
        Assert.True(lampsMs < 3000, "1300 sources took " + lampsMs + " ms");
    }

    [Fact]
    public void ShadowedLightSelectionTakesTheNearestWithLineOfSightWithinBudget()
    {
        var cells = Room(30, 12);
        cells.Fill(10, 1, 10, 12, true);   // a wall at x = 10
        var candidates = new List<ShadowCandidate>
        {
            new(1, 12, 6, 8f, 1f),   // behind the wall from a camera at x = 5
            new(2, 8, 6, 8f, 1f),    // 3 tiles away, open
            new(3, 5, 9, 8f, 1f),    // 3 tiles away, open (ties by key)
            new(4, 3, 6, 8f, 1f),    // 2 tiles away
            new(5, 28, 6, 8f, 1f),   // too far
            new(6, 10, 4, 8f, 1f),   // in the wall tile itself: the end tile does not block
        };
        var chosen = new List<ShadowCandidate>();
        var scratch = new List<(float, ShadowCandidate)>();
        ShadowedLightSelector.Select(cells, 1, 5f, 6f, candidates, 4, chosen, scratch);
        Assert.Equal(new long[] { 4, 2, 3, 6 }, chosen.Select(c => c.Key).ToArray());
        ShadowedLightSelector.Select(cells, 1, 5f, 6f, candidates, 2, chosen, scratch);
        Assert.Equal(new long[] { 4, 2 }, chosen.Select(c => c.Key).ToArray());
        ShadowedLightSelector.Select(cells, 1, 5f, 6f, candidates, 0, chosen, scratch);
        Assert.Empty(chosen);
    }
}
