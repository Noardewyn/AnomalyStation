using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>The emergency shuttle keeps its air through the dock at the station and in transit: its cabin is sealed, as in tg.</summary>
public class ShuttleAirGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static List<turf_open> Cabin(obj_docking_port_mobile port)
    {
        var cabin = new List<turf_open>();
        foreach (turf t0 in Dm.each<turf>(port.return_turfs()))
            if (locate(t0.x, t0.y, t0.z) is turf_open o and not turf_open_space and not turf_open_floor_plating_airless)
                cabin.Add(o);
        return cabin;
    }

    private static float Lowest(RealHeadlessWorld w) => In(w, () => Cabin(SSshuttle.emergency).Min(o => Dm.num(((datum_gas_mixture)o.air).return_pressure())));

    [MetaServerFact]
    public void The_cabin_holds_its_air_docked_at_the_station_and_in_transit()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        var bob = w.Join("Bob");
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 400;
            SSshuttle.emergency_escape_time = 200;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 20 * 60 * 10;
        }
        bob.Session.DevCommand("shuttle call");
        for (int i = 0; i < 800 && !In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED)); i++)
            w.Run(1);
        w.Run(60);
        Assert.True(Lowest(w) > 90, $"docked: lowest cabin pressure {Lowest(w)} kPa");
        for (int i = 0; i < 1200 && !In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)); i++)
            w.Run(1);
        w.Run(40);
        Assert.True(Lowest(w) > 90, $"in transit: lowest cabin pressure {Lowest(w)} kPa");

        // tg's hyperspace drift: off the hull a crewman is pulled away from the flight, and he holds on beside the hull
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        (int x0, int y0, int z0) = In(w, () =>
        {
            obj_docking_port_mobile port = SSshuttle.emergency;
            turf_open_space_transit open = Dm.each<turf_open_space_transit>(Dm.world).First(t => t.z == port.z
                && Math.Abs(t.x - port.x) > 25 && Math.Abs(t.y - port.y) > 25 && Math.Abs(t.x - port.x) < 40 && Math.Abs(t.y - port.y) < 40);
            Waiting.NoSleep(bobMob.forceMove(open));
            return ((int)open.x, (int)open.y, (int)open.z);
        });
        Assert.True(In(w, () => HAS_TRAIT(bobMob, TRAIT_HYPERSPACED)), "the transit tile gives its drift");
        w.Run(10);
        (int x1, int y1) = In(w, () => ((int)bobMob.x, (int)bobMob.y));
        Assert.True(x1 != x0 || y1 != y0, "the drift pulls him away");
    }
}
