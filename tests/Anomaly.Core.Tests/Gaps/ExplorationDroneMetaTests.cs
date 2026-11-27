using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The exploration drones on the converted MetaStation: the scanner array, its console, the two launchers with their drones and the
/// drone console boot live; the array's dish joins the scanner controller, the drones take tg's probe names, SSpersistence has loaded
/// tg's five adventures; a wide scan from the console finds sites; a drone fuelled at its launcher flies to one and arrives; and
/// nothing of the drones' machines throws over the ticks.
/// </summary>
public class ExplorationDroneMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ExplorationDroneMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_scanner_array_and_drone_launchers_work()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_item_exodrone drone;
        using (_meta.World.Enter())
        {
            var dishes = Dm.each<obj_machinery_exoscanner>().ToList();
            var scanConsoles = Dm.each<obj_machinery_computer_exoscanner_control>().ToList();
            var launchers = Dm.each<obj_machinery_exodrone_launcher>().ToList();
            var drones = Dm.each<obj_item_exodrone>().ToList();
            var droneConsoles = Dm.each<obj_machinery_computer_exodrone_control_console>().ToList();
            _output.WriteLine($"Meta's exploration: {dishes.Count} dishes, {scanConsoles.Count} scanner consoles, {launchers.Count} launchers, {drones.Count} drones ({string.Join(", ", drones.Select(d => d.name))}), {droneConsoles.Count} drone consoles");
            Assert.Single(dishes);
            Assert.Single(scanConsoles);
            Assert.Equal(2, launchers.Count);
            Assert.Equal(2, drones.Count);
            Assert.Single(droneConsoles);
            foreach (atom thing in dishes.Cast<atom>().Concat(scanConsoles).Concat(launchers).Concat(drones).Concat(droneConsoles))
                Assert.False(InertSeam.IsInert(thing), thing.GetType().Name + " is inert");
            Assert.Equal(2, length(GLOB.exodrone_launchers));
            Assert.Equal(2, length(GLOB.exodrones));
            Assert.Equal(5, length(GLOB.explorer_drone_adventure_db_entries));
            Assert.All(drones, d => Assert.NotEqual("exploration drone", d.name));
            _output.WriteLine($"the dish: {dishes[0].icon_state}, the array's power {Dm.str(GLOB.exoscanner_controller.calculate_scan_power())}");

            // the console's wide scan, resolved here rather than after its two minutes
            obj_machinery_computer_exoscanner_control console = scanConsoles[0];
            _output.WriteLine($"the dish: anchored {Dm.str(dishes[0].anchored)}, operational {Dm.str(dishes[0].is_operational)}, panel {Dm.str(dishes[0].panel_open)}");
            Assert.True(Dm.truthy(dishes[0].is_ready()), "Meta's dish is ready");
            Assert.Equal("scanner_ready", dishes[0].icon_state);
            console.start_wide_scan();
            datum_exoscan scan = GLOB.exoscanner_controller.current_scan;
            Assert.NotNull(scan);
            Assert.Equal("scanner_on", dishes[0].icon_state);
            deltimer(scan.scan_timer);
            Waiting.NoSleep(scan.resolve_scan());
            Assert.True(length(GLOB.exploration_sites) >= 1);
            Assert.Equal("scanner_ready", dishes[0].icon_state);

            // a pellet into the launcher under a drone, and the drone off to the first site
            drone = drones.First(d => Dm.locate<obj_machinery_exodrone_launcher>(d.loc) != null);
            var launcher = Dm.locate<obj_machinery_exodrone_launcher>(drone.loc);
            launcher.fuel_canister = Dm.@new<obj_item_fuel_pellet>(launcher);
            Assert.False(Dm.truthy(drone.travel_error()));
            var site = Dm.each<datum_exploration_site>(GLOB.exploration_sites).First();
            Waiting.NoSleep(drone.launch_for(site));
            Assert.Equal(EXODRONE_TRAVEL, drone.drone_status);
            Assert.Null(drone.loc);
        }
        Step(100);
        using (_meta.World.Enter())
        {
            deltimer(drone.travel_timer_id);
            Waiting.NoSleep(drone.finish_travel());
            Assert.Equal(EXODRONE_EXPLORATION, drone.drone_status);
            drone.explore_site();
            _output.WriteLine("the drone's log: " + string.Join(" | ", Dm.each(drone.drone_log).Select(Dm.str)));
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("explorer_drone", StringComparison.Ordinal) || (e.ToString() ?? "").Contains("exodrone", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains("exoscan", StringComparison.Ordinal) || (e.ToString() ?? "").Contains("exploration", StringComparison.Ordinal))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
