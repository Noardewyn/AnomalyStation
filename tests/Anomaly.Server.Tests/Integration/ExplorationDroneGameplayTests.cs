using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The exploration drones through the real server with a headless client: a joined scientist starts a wide scan at the scanner array
/// control console's window, which finds sites once the four dishes have scanned; at the drone control console's window she takes
/// control of the drone standing on its fuelled launcher, fits it with a laser and launches it at a site; the drone leaves the station
/// and arrives at the site, which her window shows. Her client draws both windows from the server's data.
/// </summary>
public class ExplorationDroneGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [Fact]
    public void A_scientist_scans_for_sites_and_sends_a_drone_to_one()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14550, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player sam = w.Join("Sam");
        var me = (mob_living_carbon_human)w.MobOf(sam);
        ReplicaWorld replica = sam.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => sam.Session.UiAct(window, action, parameters),
            SendClose = window => sam.Session.UiClose(window),
        };
        w.Run(Second);

        // the consoles beside her, the launcher with its drone and pellet, and four dishes in the room's corners
        var free = In(w, () => new[] { EAST, WEST, NORTH, SOUTH }.Select(d => get_step(me, d) as turf)
            .Where(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density))).ToList());
        Assert.True(free.Count >= 3, "three free tiles around her");
        var scanConsole = In(w, () => Dm.@new<obj_machinery_computer_exoscanner_control>(free[0]));
        var droneConsole = In(w, () => Dm.@new<obj_machinery_computer_exodrone_control_console>(free[1]));
        var launcher = In(w, () => Dm.@new<obj_machinery_exodrone_launcher>(free[2]));
        var drone = In(w, () => Dm.@new<obj_item_exodrone>(free[2]));
        Do(w, () => launcher.fuel_canister = Dm.@new<obj_item_fuel_pellet>(launcher));
        var dishes = In(w, () =>
        {
            var made = new List<obj_machinery_exoscanner>();
            turf center = me.loc as turf;
            foreach ((int dx, int dy) in new[] { (-6, -6), (6, -6), (-6, 6), (6, 6) })
            {
                turf spot = locate(center.x + dx, center.y + dy, center.z) as turf;
                if (spot is turf_open && !Dm.truthy(spot.density))
                    made.Add(Dm.@new<obj_machinery_exoscanner>(spot));
            }
            return made;
        });
        w.Run(Second);
        float power = In(w, () => Dm.num(GLOB.exoscanner_controller.calculate_scan_power()));
        output.WriteLine($"{dishes.Count} dishes, the array's power {power}");
        Assert.True(power >= 1, "the dishes scan");

        // the scanner console's window: the wide scan
        Assert.True(sam.Session.Click(On(scanConsole), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ExoscannerConsole", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Contains("Configure Wide Scan", content.AllText());
        content.Walk().Single(n => n.Key == "scan" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.NotNull(In(w, () => GLOB.exoscanner_controller.current_scan));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Scan in Progress!", content.AllText());
        Assert.All(dishes, d => Assert.Equal("scanner_on", In(w, () => d.icon_state)));
        int scanTicks = In(w, () => (int)Dm.num(timeleft(GLOB.exoscanner_controller.current_scan.scan_timer))) * 2;
        w.Run(scanTicks + Second);
        int sites = In(w, () => (int)length(GLOB.exploration_sites));
        output.WriteLine($"the wide scan took {scanTicks} ticks and found {sites} sites");
        Assert.InRange(sites, 1, 3);
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Anomaly", content.AllText());
        Assert.True(sam.Session.UiClose(window.Window));
        w.Run(4);

        // the drone console's window: control, a laser, and the launch at the first site
        Assert.True(sam.Session.Click(On(droneConsole), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ExodroneConsole", window.Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Contains(In(w, () => drone.name), content.AllText());
        content.Walk().Single(n => n.Key == "assume").OnClick();
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(drone.controlled)));
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "tools" && n.Kind == UiKind.Button).OnClick();
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "add" && n.Tooltip != null && n.Tooltip.StartsWith("Multipurpose tool suitable for combat", StringComparison.Ordinal)).OnClick();
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(drone.has_tool(EXODRONE_TOOL_LASER))));
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "launch" && !n.Disabled).OnClick();
        w.Run(Second / 2);
        Assert.Equal(EXODRONE_TRAVEL, In(w, () => drone.drone_status));
        Assert.Null(In(w, () => drone.loc));
        Assert.Equal(4f, In(w, () => launcher.fuel_canister.uses));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Travel Time:", content.AllText());

        int travelTicks = In(w, () => (int)Dm.num(timeleft(drone.travel_timer_id))) * 2;
        w.Run(travelTicks + Second);
        Assert.Equal(EXODRONE_EXPLORATION, In(w, () => drone.drone_status));
        content = Shown(windows, replica, window.Window);
        string siteName = In(w, () => Dm.str(drone.location.display_name()));
        Assert.Contains(siteName, content.AllText());
        Assert.Contains("Discovered " + siteName, content.AllText());
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
