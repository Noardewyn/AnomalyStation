using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>A door opened on a hull in hyperspace vents the ship: transit tiles are space.</summary>
public class HyperAirTests
{
    [MetaServerFact]
    public void An_airlock_opened_in_hyperspace_vents_the_ship()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 76, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 6000;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 12000;
        }
        alice.Session.DevCommand("shuttle call");
        for (int i = 0; i < 4000; i++)
        {
            w.Run(1);
            using (w.World.Enter())
                if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE) && SSshuttle.emergency.z == 3) break;
        }
        w.Run(40);
        turf_open inside = null;
        obj_machinery_door door = null;
        float before = 0;
        string info;
        using (w.World.Enter())
        {
            foreach (turf t in Dm.each<turf>(SSshuttle.emergency.return_turfs()))
            {
                door = Dm.each<obj_machinery_door>(t).FirstOrDefault();
                if (door == null) continue;
                foreach (float d in new float[] { 1, 2, 4, 8 })
                    if (get_step(t, d) is turf_open_space_transit && get_step(t, d <= 2 ? 3 - d : 12 - d) is turf_open o && o is not turf_open_space) inside = o;
                if (inside != null) break;
            }
            Assert.NotNull(inside);
            before = Dm.num(inside.air.return_pressure());
            Waiting_Open(door);
            info = $"door {door.GetType().Name} at {door.x},{door.y},{door.z} pressure {before}";
        }
        w.Run(100);
        using (w.World.Enter())
        {
            turf dt = get_turf(door) as turf;
            float after = Dm.num(inside.air.return_pressure());
            string neigh = string.Join(",", Dm.each<object>(((turf_open)dt).atmos_adjacent_turfs ?? Dm.list()).Select(o => o.GetType().Name));
            Assert.True(after < before / 2, $"{info} -> {after}; door density {door.density} blocks_air {dt.blocks_air} door tile air {((turf_open)dt).air?.return_pressure()} adj [{neigh}]");
        }
    }

    private static void Waiting_Open(obj_machinery_door door) => Anomaly.Core.Kernel.Waiting.Detach(door.open());
}
