using System.Diagnostics;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The size and cost of a version-3 join on a generated 255 x 255 station (the largest map the protocol takes), recorded in protocol.md section 13.</summary>
public class JoinSizeV3Tests
{
    private readonly ITestOutputHelper _output;

    public JoinSizeV3Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    // A Meta-like generated map: a grid of 15 x 15 rooms with walls and doors, corridors, a margin of space, 40 areas with 6 base
    // lights, window panes along some walls, per-room floor variants and wall smoothing states (the appearance runs).
    internal static MapGrid GeneratedStation(int size, out int cells)
    {
        var grid = new MapGrid { Tick = 100, Width = size, Height = size, Levels = 1 };
        grid.TurfTypes.AddRange(new[] { "/turf/open/space/basic", "/turf/open/floor/iron", "/turf/closed/wall", "/turf/open/floor/plating", "/turf/closed/wall/r_wall" });
        grid.BaseLights.AddRange(new[] { new BaseLight(0xFFFFFFFF), new BaseLight(0xFFF2C880), new BaseLight(0xA0C0FFC0), new BaseLight(0x8589FAFF), new BaseLight(0xFF8040A0), new BaseLight(0x00000000 | 0x10) });
        var rng = new Random(255);
        cells = size * size;
        var type = new int[cells];
        var appearance = new int[cells];
        var flags = new byte[cells];
        var baseLight = new int[cells];
        var solid = new byte[(cells + 7) / 8];
        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                int i = (x - 1) + (y - 1) * size;
                bool margin = x <= 8 || y <= 8 || x > size - 8 || y > size - 8;
                if (margin)
                {
                    type[i] = 0;
                    appearance[i] = 1 + rng.Next(25); // space dust variants
                    flags[i] = (byte)(((x == 9 || y == 9) ? 0 : 0) | (rng.Next(4) == 0 ? CellFlags.Starlight : 0));
                    continue;
                }

                bool wall = (x - 9) % 15 == 0 || (y - 9) % 15 == 0;
                bool door = wall && ((x - 9) % 15 == 0 ? (y - 9) % 15 == 7 : (x - 9) % 15 == 7);
                type[i] = wall && !door ? (rng.Next(6) == 0 ? 4 : 2) : (rng.Next(9) == 0 ? 3 : 1);
                appearance[i] = wall && !door ? 30 + rng.Next(48) : 100 + (x / 15 + y / 15) % 7 + rng.Next(2);
                if (wall && !door)
                {
                    flags[i] |= CellFlags.Opaque;
                    solid[i >> 3] |= (byte)(1 << (i & 7));
                }
                else if (!wall && rng.Next(40) == 0)
                {
                    flags[i] |= (byte)(CellFlags.FaceNorth << rng.Next(4));
                }

                baseLight[i] = 1 + ((x / 15) * 3 + y / 15) % 6;
            }
        }

        AddRuns(grid.Runs, type);
        AddRuns(grid.AppearanceRuns, appearance);
        AddRuns(grid.FlagRuns, Array.ConvertAll(flags, f => (int)f));
        AddRuns(grid.BaseLightRuns, baseLight);
        grid.SolidBits = solid;
        grid.Starlight = new LightState(2f, 1f, 0x8589FAFFu, true, 360f, 0, -0.5f, 0f, 0f, 1);
        return grid;
    }

    private static void AddRuns(List<TurfRun> runs, int[] values)
    {
        int start = 0;
        for (int i = 1; i <= values.Length; i++)
        {
            if (i < values.Length && values[i] == values[start]) continue;
            runs.Add(new TurfRun(i - start, values[start]));
            start = i;
        }
    }

    [Fact]
    public void AGenerated255By255StationJoinsInAFewHundredKilobytes()
    {
        MapGrid grid = GeneratedStation(255, out int cells);
        var watch = Stopwatch.StartNew();
        byte[] bytes = Codec.Encode(grid);
        double encodeMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        Assert.True(Codec.TryDecode(bytes, out MapGrid back, out ProtocolError error), error.ToString());
        double decodeMs = watch.Elapsed.TotalMilliseconds;
        Msg.AssertEqual(grid, back);

        // The same map without the version-3 parts, for the cost of version 3.
        var v2 = new MapGrid { Tick = grid.Tick, Width = 255, Height = 255, Levels = 1, SolidBits = grid.SolidBits };
        v2.TurfTypes.AddRange(grid.TurfTypes);
        v2.Runs.AddRange(grid.Runs);
        v2.AppearanceRuns.AddRange(grid.AppearanceRuns);
        int v2Bytes = Codec.Encode(v2).Length;
        int v3Bytes = bytes.Length;
        _output.WriteLine($"JOINSIZE cells={cells} mapgrid_bytes={v3Bytes} v2_part_bytes={v2Bytes} v3_extra_bytes={v3Bytes - v2Bytes} runs={grid.Runs.Count} appearance_runs={grid.AppearanceRuns.Count} flag_runs={grid.FlagRuns.Count} base_light_runs={grid.BaseLightRuns.Count} encode_ms={encodeMs:F2} decode_ms={decodeMs:F2}");
        Assert.True(v3Bytes < 1_000_000);
        Assert.True(v3Bytes - v2Bytes < 200_000);
    }
}
