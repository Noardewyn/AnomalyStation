using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Deathmatch;

/// <summary>
/// The deathmatch arena (docs/ai/specs/deathmatch): the map boots, its weapon pads hold an item and refill 5 seconds after it is taken (DV584), and the map editor
/// reads and writes it unchanged.
/// </summary>
public class DeathmatchArenaTests
{
    private static string Folder()
    {
        string folder = MetaMaps.Folder();
        return folder != null && File.Exists(Path.Combine(folder, "map_jsons", "deathmatch_arena.json")) ? folder : null;
    }

    private static World Boot(string folder, out TgTestWorld.Log log)
        => TgTestWorld.Boot(out log, configure: w => MapSeam.Select(w, new MapSelection(folder, "deathmatch_arena")), compiledMap: TgWorld.CompiledMap, maxTicks: 400);

    private static List<obj_effect_spawner_random_deathmatch_item_pad> Pads()
    {
        var pads = new List<obj_effect_spawner_random_deathmatch_item_pad>();
        for (int z = 1; z <= Dm.num(world.maxz); z++)
        {
            if (!Dm.truthy(is_station_level(z)))
                continue;
            for (int y = 1; y <= Dm.num(world.maxy); y++)
                for (int x = 1; x <= Dm.num(world.maxx); x++)
                    foreach (obj_effect_spawner_random_deathmatch_item_pad pad in Dm.each<obj_effect_spawner_random_deathmatch_item_pad>(locate(x, y, z)))
                        pads.Add(pad);
        }
        return pads;
    }

    [Fact]
    public void TheArenaBootsWithItsPadsStocked()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.True(Dm.num(Dm.world.maxz) >= 6); // the six levels of the station
            List<obj_effect_spawner_random_deathmatch_item_pad> pads = Pads();
            Assert.Equal(21, pads.Count);
            foreach (obj_effect_spawner_random_deathmatch_item_pad pad in pads)
            {
                Assert.NotNull(pad.held_item);
                Assert.Same(pad.loc, pad.held_item.loc);
                Assert.IsAssignableFrom<obj_item>(pad.held_item);
            }
            AssertNoArenaAirReachesSpace();
        }
    }

    // a flood from every space turf along the atmos links must not reach the arena's air (a corridor once vented through a maintenance roof)
    private static void AssertNoArenaAirReachesSpace()
    {
        var seen = new HashSet<turf>();
        var queue = new Queue<turf>();
        for (int z = 1; z <= Dm.num(world.maxz); z++)
        {
            if (!Dm.truthy(is_station_level(z)))
                continue;
            for (int y = 1; y <= Dm.num(world.maxy); y++)
                for (int x = 1; x <= Dm.num(world.maxx); x++)
                    if (locate(x, y, z) is turf_open_space s && seen.Add(s))
                        queue.Enqueue(s);
        }
        while (queue.Count > 0)
            foreach (turf next in Dm.each<turf>(queue.Dequeue().atmos_adjacent_turfs))
                if (seen.Add(next))
                {
                    Assert.False(next.loc is area_deathmatch, $"arena air at {next.x},{next.y},{next.z} is linked to space");
                    queue.Enqueue(next);
                }
    }

    [Fact]
    public void APadRefillsFiveSecondsAfterItsItemIsTaken()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        obj_effect_spawner_random_deathmatch_item_pad pad;
        atom_movable taken;
        using (world.Enter())
        {
            pad = Pads()[0];
            taken = pad.held_item;
            Waiting.NoSleep(taken.forceMove(get_step(pad, EAST)));
            Assert.Null(pad.held_item);
        }
        world.Step(80); // 4 s at 20 ticks a second: not yet
        using (world.Enter())
            Assert.Null(pad.held_item);
        world.Step(30);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.NotNull(pad.held_item);
            Assert.NotSame(taken, pad.held_item);
            Assert.Same(pad.loc, pad.held_item.loc);
            Assert.Same(get_step(pad, EAST), taken.loc); // the taken item is not called back
        }
    }

    [Fact]
    public void TheMapEditorWritesTheArenaAsItReadIt()
    {
        string folder = Folder();
        if (folder == null)
            return;
        string text = File.ReadAllText(Path.Combine(folder, "map_files", "deathmatch", "arena.as3dmap"));
        MapDocument document = MapDocument.Parse(text, "arena");
        Assert.True(text == document.ToText(), "the arena changed on a read and a write");
        Assert.False(document.IsDirty);
    }
}
