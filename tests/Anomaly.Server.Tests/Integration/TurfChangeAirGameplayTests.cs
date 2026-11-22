using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's bug (2026-10-17): a wall or a floor spawned with the dev tool on MetaStation's plating and then deleted left a
/// hole to space (the spawned turf took the plating's baseturf, space), and the room's air streamed into it. DV60: the dev
/// spawn builds on top (place_on_top), so the delete gives the plating back with the room's air, and the player's client
/// sees no air flow.
/// </summary>
public class TurfChangeAirGameplayTests(ITestOutputHelper o)
{
    private const int Second = 20;
    private const int X = 47, Y = 155; // plating over space in Meta's Aux base construction zone (the base cleared: M10 slice G3)

    [MetaServerFact]
    public void A_spawned_wall_or_floor_deleted_again_leaves_the_plating_and_the_rooms_air()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 54, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the test works in the empty construction zone
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(10 * Second);
        using (w.World.Enter())
        {
            GlobalProcs.ADD_TRAIT(me, Defines.TRAIT_GODMODE, "test");
            Waiting.NoSleep(me.forceMove(Builtins.locate(X, Y - 2, 1)));
            Assert.IsType<turf_open_floor_plating>(Builtins.locate(X, Y, 1));
            Assert.Equal(typeof(turf_baseturf_bottom), ((turf)Builtins.locate(X, Y, 1)).baseturfs); // the plating lies on the level's bottom, space
        }
        w.Run(2 * Second);

        foreach (Type built in new[] { typeof(turf_closed_wall), typeof(turf_open_floor_iron), typeof(turf_closed_wall_r_wall) })
        {
            using (w.World.Enter())
                DevCommands.Spawn(built, Builtins.locate(X, Y, 1), 1, 0, out _);
            w.Run(2 * Second);
            Assert.IsType(built, In(() => Builtins.locate(X, Y, 1)));
            using (w.World.Enter())
                o.WriteLine(DevCommands.Delete(Builtins.locate(X, Y, 1)));
            int flows = 0;
            for (int i = 0; i < 5 * 2; i++)
            {
                w.Run(Second / 2);
                flows = Math.Max(flows, replica.AirflowCells.Count(c => Math.Abs(c.X - X) <= 6 && Math.Abs(c.Y - Y) <= 6));
            }
            Assert.IsType<turf_open_floor_plating>(In(() => Builtins.locate(X, Y, 1)));
            float here = Kpa(X, Y), beside = Kpa(X, Y - 1);
            o.WriteLine($"{built.Name}: the plating {here:0.0} kPa, beside it {beside:0.0} kPa, flow cells near {flows}");
            Assert.InRange(here, 95f, 110f);
            Assert.InRange(beside, 95f, 110f);
            Assert.Equal(0, flows);
        }

        T In<T>(Func<T> f)
        {
            using (w.World.Enter())
                return f();
        }

        float Kpa(int x, int y) => In(() => Builtins.locate(x, y, 1) is turf_open t ? Dm.num(t.air.return_pressure()) : -1f);
    }
}
