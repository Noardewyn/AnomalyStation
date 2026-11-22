using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M10Shuttles;

/// <summary>
/// M10 slice G4 (D272): tg's emergency shuttle on MetaStation with the CentCom stand-in (DV190). Called (SSshuttle.call_evac_shuttle,
/// what the communications console's call does), it flies from its CentCom dock and docks at Meta's evac bay when its call timer ends;
/// when its docked time runs out it ignites, leaves into a transit dock of the reserved level with what is aboard, and when its
/// escape timer ends it docks at CentCom's dock: SHUTTLE_ENDGAME. The test shortens tg's timers (10, 3 and 2 minutes) as an admin would.
/// </summary>
public class EmergencyShuttleTests
{
    private readonly ITestOutputHelper _output;

    public EmergencyShuttleTests(ITestOutputHelper output) => _output = output;

    private static string Run(World world, Func<bool> until, int maxSteps)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            using (world.Enter())
            {
                if (until())
                    return null;
            }
            world.Step();
        }
        using (world.Enter())
            return $"timed out at {Dm.world.time}: mode {SSshuttle.emergency.mode}, timer {SSshuttle.emergency.timer}";
    }

    [MetaFact]
    public void The_called_emergency_shuttle_docks_at_the_evac_bay_then_leaves_into_transit_and_ends_at_centcom()
    {
        World world = MetaMaps.Boot(out TgTestWorld.Log log);
        int bootErrors = log.Runtimes.Count(e => !e.Message.Contains("not translated yet"));
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME); // SSshuttle fires in the setup and game runlevels
        obj_docking_port_mobile_emergency shuttle;
        obj_structure_chair seat;
        using (world.Enter())
        {
            shuttle = SSshuttle.emergency;
            SSshuttle.emergency_call_time = 30;
            SSshuttle.emergency_dock_time = 200; // more than ENGINE_START_TIME (10 s), so it docks before it ignites
            SSshuttle.emergency_escape_time = 50;
            SSshuttle.call_evac_shuttle("Testing the escape.", null);
            Assert.Equal(SHUTTLE_CALL, shuttle.mode);
        }

        Assert.Null(Run(world, () => Dm.eq(shuttle.mode, SHUTTLE_DOCKED), 400));
        using (world.Enter())
        {
            var home = (obj_docking_port_stationary)SSshuttle.getDock("emergency_home");
            Assert.Same(home, shuttle.get_docked());
            Assert.Equal((home.x, home.y, 1f), (shuttle.x, shuttle.y, shuttle.z));
            Assert.Equal(home.dir, shuttle.dir); // the template's port faces south as the evac bay's: no turn
            // the shuttle's cabin stands in the evac bay: its chairs and turfs on the station level
            seat = Dm.each<obj_structure_chair>(Dm.world).FirstOrDefault(c => get_area(c) is area_shuttle_escape && c.z == 1);
            Assert.NotNull(seat);
            _output.WriteLine($"docked at {home.shuttle_id} ({home.x},{home.y}) at {Dm.world.time}; a seat at {seat.x},{seat.y}");
        }

        Assert.Null(Run(world, () => Dm.eq(shuttle.mode, SHUTTLE_ESCAPE), 600));
        using (world.Enter())
        {
            Assert.IsType<obj_docking_port_stationary_transit>(shuttle.get_docked());
            Assert.True(Dm.truthy(is_reserved_level(shuttle.z)), "in transit on the reserved level");
            Assert.True(Dm.truthy(is_reserved_level(seat.z)), "what is aboard rides along");
            Assert.IsType<area_shuttle_escape>(get_area(seat));
            // the evac bay is empty again: the dock's turfs are space (the shuttle's skipover scraped away)
            var home = (obj_docking_port_stationary)SSshuttle.getDock("emergency_home");
            Assert.IsAssignableFrom<turf_open_space>(get_turf(home));
            _output.WriteLine($"in transit at {shuttle.x},{shuttle.y},{shuttle.z} at {Dm.world.time}");
        }

        Assert.Null(Run(world, () => Dm.eq(shuttle.mode, SHUTTLE_ENDGAME), 400));
        using (world.Enter())
        {
            Assert.Equal("emergency_away", Dm.str(((obj_docking_port_stationary)shuttle.get_docked()).shuttle_id));
            List<string> errors = log.Runtimes.Where(e => !e.Message.Contains("not translated yet")).Skip(bootErrors).Select(e => e.ToString().Split('\n')[0]).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
    }
}
