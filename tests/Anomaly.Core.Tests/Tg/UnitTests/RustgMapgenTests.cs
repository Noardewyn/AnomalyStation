using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// <summary>D304: the rust-g map generation functions Lavaland's caves call (Builtin/rust_g.mapgen.builtin.cs).</summary>
public class RustgMapgenTests
{
    // Lavaland's generator settings (datums/mapgen/CaveGenerator.dm)
    private static string Cave(ulong seed)
    {
        var w = new World(seed);
        using (w.Enter())
            return GlobalProcs.rustg_cave_system_generator_generate("255", "255", "[]", "25", "1.5", "1", "30", "1", "15", "51", "8", "6", "4", "1");
    }

    [Fact]
    public void A_cave_is_one_connected_open_space_with_walls_and_follows_the_world_seed()
    {
        string cave = Cave(7);
        Assert.Equal(255 * 255, cave.Length);
        Assert.All(cave, c => Assert.True(c is '0' or '1'));
        int open = cave.Count(c => c == '1');
        Assert.InRange(open, 255 * 255 / 5, 255 * 255 * 4 / 5);
        Assert.Equal(cave, Cave(7));
        Assert.NotEqual(cave, Cave(8));

        // every open cell is reachable from every other (no prefab here keeps a pocket)
        var seen = new bool[cave.Length];
        var queue = new Queue<int>();
        int first = cave.IndexOf('1');
        seen[first] = true;
        queue.Enqueue(first);
        int reached = 1;
        while (queue.TryDequeue(out int i))
        {
            int x = i % 255, y = i / 255;
            foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (nx < 0 || ny < 0 || nx >= 255 || ny >= 255) continue;
                int n = ny * 255 + nx;
                if (seen[n] || cave[n] != '1') continue;
                seen[n] = true;
                reached++;
                queue.Enqueue(n);
            }
        }
        Assert.Equal(open, reached);
    }

    [Fact]
    public void An_enclosed_prefab_stays_wall()
    {
        var w = new World(3);
        using (w.Enter())
        {
            string cave = GlobalProcs.rustg_cave_system_generator_generate("100", "100", "[{\"x\":11,\"y\":21,\"w\":5,\"h\":4,\"isEnclosed\":1}]",
                "25", "1.5", "1", "30", "1", "15", "51", "8", "6", "4", "1");
            for (int y = 20; y < 24; y++)
                for (int x = 10; x < 15; x++)
                    Assert.Equal('0', cave[y * 100 + x]);
            Assert.Null(GlobalProcs.rustg_cave_system_generator_generate("x", "100", "[]", "", "", "", "", "", "", "", "", "", "", ""));
        }
    }

    [Fact]
    public void Dbp_noise_depends_on_its_seed_only_and_the_bands_split_the_plane()
    {
        string high = GlobalProcs.rustg_dbp_generate("1234", "60", "60", "255", "0.15", "1.1");
        string medium = GlobalProcs.rustg_dbp_generate("1234", "60", "60", "255", "-0.3", "0.15");
        Assert.Equal(255 * 255, high.Length);
        Assert.Equal(high, GlobalProcs.rustg_dbp_generate("1234", "60", "60", "255", "0.15", "1.1"));
        Assert.NotEqual(high, GlobalProcs.rustg_dbp_generate("99", "60", "60", "255", "0.15", "1.1"));
        Assert.Contains('1', high);
        Assert.Contains('0', high);
        Assert.DoesNotContain(Enumerable.Range(0, high.Length), i => high[i] == '1' && medium[i] == '1'); // [lower, upper) bands do not overlap
    }
}
