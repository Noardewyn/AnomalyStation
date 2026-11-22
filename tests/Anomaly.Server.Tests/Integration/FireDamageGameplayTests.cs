using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// A real plasma fire through the real server (tg's LINDA_fire.dm, burning.dm): what stands in it burns, a paper to ash, and a human in it catches
/// fire and takes burns; the client sees the hotspots.
/// </summary>
public class FireDamageGameplayTests
{
    private readonly ITestOutputHelper _output;

    public FireDamageGameplayTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void A_plasma_fire_burns_what_stands_in_it_and_the_human_too()
    {
        using var w = new RealHeadlessWorld(strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(10);

        float x, y, z;
        obj_item_paper paper;
        obj_structure_table_wood table;
        obj_structure_closet closet;
        using (w.World.Enter())
        {
            var at = (turf_open)me.loc;
            (x, y, z) = (at.x, at.y, at.z);
            paper = Dm.@new<obj_item_paper>(at);
            table = Dm.@new<obj_structure_table_wood>(at);
            closet = Dm.@new<obj_structure_closet>(at);
        }
        w.Host.EnqueueConsoleLine($"air add plasma 20 {x} {y} {z} 3");
        w.Run(4);
        w.Host.EnqueueConsoleLine($"ignite {x} {y} {z}");

        int seen = 0;
        float burn = 0;
        bool onFire = false, burnt = false;
        for (int second = 0; second < 90; second++)
        {
            w.Run(20);
            int now = p.Session.Replica.Entities.Count(e => e.TypePath == "/obj/effect/hotspot");
            seen = Math.Max(seen, now);
            _output.WriteLine($"second {second}: {now} hotspots");
            using (w.World.Enter())
            {
                burn = Math.Max(burn, Dm.num(me.get_fire_loss()));
                onFire |= Dm.truthy(me.on_fire);
                burnt |= Enumerable.Range(-3, 7).SelectMany(dx => Enumerable.Range(-3, 7).Select(dy => Builtins.locate(x + dx, y + dy, z))).OfType<turf_open>().Any(t => Dm.truthy(t.burnt));
            }
        }
        using (w.World.Enter())
        {
            int ash = Dm.each(((turf)Builtins.locate(x, y, z)).contents).Count(a => a is obj_effect_decal_cleanable_ash);
            string Left(object a, string name) => $"{name}: {(QDELETED(a) ? "destroyed" : "integrity " + Dm.num(((obj)a).get_integrity()))}";
            _output.WriteLine($"hotspots seen {seen}; burn {burn}; on fire {onFire}; floor burnt {burnt}; ash {ash}");
            _output.WriteLine($"{Left(paper, "paper")}; {Left(table, "table")}; {Left(closet, "closet")}");
            Assert.True(seen > 0, "the client sees the flames");
            Assert.True(onFire, "the human catches fire");
            Assert.True(burn > 0f, "the human takes burns");
            Assert.True(QDELETED(paper), "the paper burns up");
            Assert.True(ash > 0, "the paper leaves ash");
        }
    }
}
