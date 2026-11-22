using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>tg's flight events on the emergency shuttle in transit: a carp event spawns around it (turbulence is only added, its knockdown is not checked).</summary>
public class ShuttleEventTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void Events_run_while_the_shuttle_flies()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        datum_shuttle_event_simple_spawner_carp_friendly carp = null;
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 600;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 12000;
            SSshuttle.emergency.event_list.Cut();
            carp = (datum_shuttle_event_simple_spawner_carp_friendly)SSshuttle.emergency.add_shuttle_event(typeof(datum_shuttle_event_simple_spawner_carp_friendly));
            SSshuttle.emergency.add_shuttle_event(typeof(datum_shuttle_event_turbulence));
        }
        alice.Session.DevCommand("shuttle call");
        for (int i = 0; i < 2000 && !In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)); i++)
            w.Run(1);
        Assert.True(In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)));
        Assert.True(In(w, () => carp.spawning_turfs_hit.len > 0 && carp.spawning_turfs_miss.len > 0));
        int before = In(w, () => Dm.each<mob_living_basic_carp_passive>(Dm.world).Count());
        for (int i = 0; i < 200 && In(w, () => Dm.each<mob_living_basic_carp_passive>(Dm.world).Count()) == before; i++)
            w.Run(1);
        Assert.True(In(w, () => Dm.each<mob_living_basic_carp_passive>(Dm.world).Count()) > before, "the event spawns carp in hyperspace");
    }
}
