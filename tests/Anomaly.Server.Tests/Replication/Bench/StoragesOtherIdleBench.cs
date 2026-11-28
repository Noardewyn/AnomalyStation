using System.Diagnostics;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication.Bench;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-1 (DV696): the idle cost of the other container, measured (a bench: skipped unless ANOMALY_BENCH=1, BenchSupport.cs).
/// Fifty raw clients, each a human with a backpack and a box of seven items open beside it; the server's ticks are timed while nothing happens, with
/// the boxes open and then closed, and the one read the other container adds (the replicator's read of tg's state; no reach is polled) is timed
/// alone. No Storages message is rebuilt while nothing happens.
/// </summary>
[Trait("Category", "Bench")]
public class StoragesOtherIdleBench
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public StoragesOtherIdleBench(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    [BenchFact]
    public void Fifty_players_each_with_a_box_open_are_timed_idle_and_nothing_is_rebuilt()
    {
        const int players = 50, ticks = 400;
        using var rig = new IntegrationRig(network: new LoopbackOptions { Seed = 43, TimeoutSteps = 20000 });
        var clients = Enumerable.Range(0, players).Select(i => new RawClient(rig, "P" + i)).ToList();
        rig.Tick(20);
        var bodies = new List<mob_living_carbon_human>();
        var boxes = new List<obj_item>();
        using (rig.World.Enter())
        {
            // open floor with air, two tiles apart (not space, where they would suffocate, nor a wall)
            List<turf> floors = (from x in Enumerable.Range(1, IntegrationRig.RoomSize)
                                 from y in Enumerable.Range(1, IntegrationRig.RoomSize)
                                 where x % 2 == 0 && y % 2 == 0
                                 let t = locate(x, y, 1) as turf
                                 where t is turf_open && t is not turf_open_space && !Dm.truthy(t.density)
                                 select t).ToList();
            Assert.True(floors.Count >= players, floors.Count + " floors");
            for (int i = 0; i < players; i++)
            {
                var body = (mob_living_carbon_human)rig.MobOf("P" + i);
                Waiting.NoSleep(body.forceMove(floors[i]));
                foreach (obj_item worn in new[] { body.back, body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                    if (worn != null)
                        qdel(worn);
                var backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
                Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK));
                for (int k = 0; k < 7; k++)
                    Dm.@new<obj_item_pen>(backpack);
                var box = Dm.@new<obj_item_storage_box>(get_turf(body));
                for (int k = 0; k < 7; k++)
                    Dm.@new<obj_item_pen>(box);
                Waiting.NoSleep(box.atom_storage.open_storage(body));
                bodies.Add(body);
                boxes.Add(box);
            }
        }
        rig.Tick(20);
        using (rig.World.Enter())
            Assert.All(bodies, b => Assert.NotNull(CarriedStorages.OtherInReachForMove(b)));
        int[] builds = clients.Select((_, i) => rig.Replicator.StoragesBuilds(rig.Host.LiveSession("P" + i).Ordinal)).ToArray();
        double open = Time(() => rig.Tick(ticks)) / ticks;
        Assert.Equal(builds, clients.Select((_, i) => rig.Replicator.StoragesBuilds(rig.Host.LiveSession("P" + i).Ordinal)).ToArray());
        // the read alone: the state read the replicator's print makes (no reach is polled)
        double shown = Time(() => { for (int t = 0; t < ticks; t++) foreach (var b in bodies) _ = CarriedStorages.Other(b); }) / ticks;
        // closed: tg's hide_contents for each
        using (rig.World.Enter())
            for (int i = 0; i < players; i++)
                boxes[i].atom_storage.hide_contents(bodies[i]);
        rig.Tick(20);
        double closed = Time(() => rig.Tick(ticks)) / ticks;
        // open again (tg's open_storage), measured after the closed run, so warming up weighs on neither
        using (rig.World.Enter())
            for (int i = 0; i < players; i++)
                Waiting.NoSleep(boxes[i].atom_storage.open_storage(bodies[i]));
        rig.Tick(20);
        using (rig.World.Enter())
            Assert.All(bodies, b => Assert.NotNull(CarriedStorages.OtherInReachForMove(b)));
        double again = Time(() => rig.Tick(ticks)) / ticks;
        _output.WriteLine($"{players} players, idle, a tick: {open:F3} ms with a box open each, {closed:F3} ms with none, {again:F3} ms open again; the state read {shown:F3} ms a tick");
        Assert.True(string.IsNullOrEmpty(rig.Runtimes), rig.Runtimes);
    }

    private static double Time(Action a)
    {
        var watch = Stopwatch.StartNew();
        a();
        return watch.Elapsed.TotalMilliseconds;
    }
}
