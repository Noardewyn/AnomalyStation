using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package E5 (atmos_computers: the air sensors, the atmospherics control consoles, the monitored injectors, vents and meters) on
/// MetaStation through the real server and a client's own stack: an engineer opens the oxygen tank's console, reads the tank's sensor,
/// switches the input injector and sets its rate, sets the output vent's pressure and closes it, and the machines change (the client
/// sees the injector's picture change); then the station master console shows the distribution and waste loops' meters and, through
/// Reconnect and tg's list window, any sensor of the station, the supermatter chamber's among them.
/// </summary>
public class AtmosControlConsoleGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    /// <summary>Puts the mob on the first clear tile beside the thing from which it reaches it (a directional window can stand between).</summary>
    private static void Reach(RealHeadlessWorld w, mob_living me, atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST })
        {
            turf t = In(w, () => get_step(thing, dir));
            if (t == null || !In(w, () => Clear(t)))
                continue;
            In(w, () => Waiting.NoSleep(me.forceMove(t)));
            if (In(w, () => Dm.truthy(Waiting.NoSleep(me.Adjacent(thing)))))
                return;
        }
        Assert.Fail($"no tile reaches {thing}");
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void An_engineer_runs_the_oxygen_tank_from_its_console_and_the_master_console_reaches_the_supermatter_chambers_sensor()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 59, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // ---- the oxygen tank: its sensor linked Meta's oxygen_input injector and oxygen_output vent at its late init
        var console = In(w, () => Dm.each<obj_machinery_computer_atmos_control_oxygen_tank>(Dm.world).Single());
        var sensor = In(w, () => Dm.each<obj_machinery_air_sensor_oxygen_tank>(Dm.world).Single());
        var injector = In(w, () => Dm.each<obj_machinery_atmospherics_components_unary_outlet_injector_monitored_oxygen_input>(Dm.world).Single());
        var vent = In(w, () => Dm.each<obj_machinery_atmospherics_components_unary_vent_pump_siphon_monitored_oxygen_output>(Dm.world).Single());
        output.WriteLine($"console at {In(w, () => $"{console.x},{console.y}")}, sensor at {In(w, () => $"{sensor.x},{sensor.y}")}");
        In(w, () =>
        {
            Assert.Equal(injector.id_tag, sensor.inlet_id);
            Assert.Equal(vent.id_tag, sensor.outlet_id);
            Assert.Equal(sensor.id_tag, console.connected_sensors[ATMOS_GAS_MONITOR_O2]);
            Assert.Equal((1f, 200f), (injector.on, injector.volume_rate)); // inlets.dm: monitored injectors start on at the maximum
            Assert.Equal((1f, 4000f), (vent.on, vent.internal_pressure_bound)); // the siphon's bound
            Assert.Equal("gsensor1", sensor.icon_state);
            Assert.Equal(("tank", "atmos_key"), (console.icon_screen, console.icon_keyboard));
        });

        Reach(w, me, console);
        w.Run(Second);
        AppearanceEntry Look(atom a)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
        }
        List<string> OverlaysOf(AppearanceEntry look) => look.Overlays
            .Select(o => replica.TryGetAppearance(o, out AppearanceEntry e) ? replica.ResolveString(e.IconState) : null).Where(s => s != null).ToList();
        Assert.Contains("tank", OverlaysOf(Look(console))); // the console's screen (D142)
        Assert.Contains("atmos_key", OverlaysOf(Look(console)));
        Assert.Equal("gsensor1", replica.ResolveString(Look(sensor).IconState));
        Assert.Equal("inje_on", replica.ResolveString(Look(injector).IconState));

        Assert.True(p.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AtmosControlConsole", window.Interface);
        Assert.Equal((200, 4500, "o2"), (window.Merged.Num("maxInput"), window.Merged.Num("maxOutput"), window.Merged.Str("defaultGas")));
        UiValue chamber = Assert.Single(window.Merged.Arr("chambers"));
        Assert.Equal(("o2", "Oxygen Supply"), (chamber.Str("id"), chamber.Str("name")));
        Assert.True(chamber.Get("gasmix").Num("total_moles") > 1000); // Meta's oxygen tank
        Assert.Equal("o2", chamber.Get("gasmix").Arr("gases")[0].Items[0].AsStr());
        Assert.Equal((true, 200), (chamber.Get("input_info").Truthy("active"), chamber.Get("input_info").Num("amount")));
        Assert.Equal((true, 4000), (chamber.Get("output_info").Truthy("active"), chamber.Get("output_info").Num("amount")));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((550.0, 600.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal("Oxygen Supply", ByKey(content, "reading").Title);
        Assert.Contains("Oxygen", content.AllText());
        Assert.Equal("Injecting", ByKey(content, "toggle_input").Text);
        Assert.Equal("Open", ByKey(content, "toggle_output").Text);

        // the injector off, its rate 120, the vent's pressure 3000 and the vent closed
        ByKey(content, "toggle_input").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        ByKey(content, "adjust_input").OnChange(120);
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        ByKey(content, "adjust_output").OnChange(3000);
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        ByKey(content, "toggle_output").OnClick();
        w.Run(Second);
        In(w, () => Assert.Equal((0f, 120f, 0f, 3000f), (injector.on, injector.volume_rate, vent.on, vent.internal_pressure_bound)));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("Off", "times"), (ByKey(content, "toggle_input").Text, ByKey(content, "toggle_input").Icon));
        Assert.Equal(120, ByKey(content, "adjust_input").Value);
        Assert.Equal(3000, ByKey(content, "adjust_output").Value);
        Assert.Equal("Closed", ByKey(content, "toggle_output").Text);
        Assert.Equal("inje_off", replica.ResolveString(Look(injector).IconState)); // the client sees the injector stop

        // the rate is clamped to tg's maximum
        ByKey(content, "adjust_input").OnChange(900);
        w.Run(Second);
        Assert.Equal(200f, In(w, () => injector.volume_rate));
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- the station master console: the distribution and waste loop meters, then any sensor through Reconnect
        var master = In(w, () => Dm.each<obj_machinery_computer_atmos_control_nocontrol_master>(Dm.world).Single());
        Reach(w, me, master);
        w.Run(Second);
        Assert.True(p.Session.Click(On(master), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AtmosControlConsole", window.Interface);
        Assert.False(window.Merged.Truthy("control"));
        Assert.Equal(new[] { "Distribution Loop", "Waste Loop" }, window.Merged.Arr("chambers").Select(c => c.Str("name")).ToArray());
        // the loops' meters read their pipelines: the distribution loop's air, the waste loop empty at round start
        Assert.True(window.Merged.Arr("chambers")[0].Get("gasmix").Num("total_moles") > 0);
        Assert.Equal(0, window.Merged.Arr("chambers")[1].Get("gasmix").Num("total_moles"));
        Assert.True(window.Merged.Arr("chambers")[1].Get("gasmix").Num("volume") > 0);
        content = Shown(windows, replica, window.Window);
        Assert.DoesNotContain(content.Walk(), n => n.Key == "controls");
        Assert.Equal(new[] { "Distribution Loop", "Waste Loop" }, ByKey(content, "chamber").Options);

        ByKey(content, "reading").Buttons.OnClick(); // Reconnect
        w.Run(Second);
        UiWindowState list = replica.UiWindows.Tgui().Single(u => u.Interface == "ListInputWindow");
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        List<UiNode> entries = windows.Get(list.Window).Content.Walk().Where(n => n.Kind == UiKind.Button && n.OnDoubleClick != null).ToList();
        output.WriteLine("reconnect offers: " + string.Join(", ", entries.Select(n => n.Text)));
        foreach (string offered in new[] { "Oxygen Supply", "Plasma Supply", "Mixed Air Supply", "Nitrogen Supply", "Nitrous Oxide Supply", "Carbon Dioxide Supply",
            "Mix Chamber", "Incinerator Chamber", "Ordnance Burn Chamber", "Ordnance Freezer Chamber", "Supermatter Engine Chamber" })
            Assert.Contains(offered, entries.Select(n => n.Text));
        Assert.DoesNotContain("Distribution Loop", entries.Select(n => n.Text)); // the loops are the master's own
        entries.Single(n => n.Text == "Supermatter Engine Chamber").OnClick();
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        windows.Get(list.Window).Content.Walk().Single(n => n.Key == "submit").OnClick();
        w.Run(Second);

        Assert.Equal("Supermatter Engine Chamber Monitor", In(w, () => Dm.str(master.name)));
        window = replica.UiWindows.Tgui().Single(u => u.Interface == "AtmosControlConsole");
        UiValue engine = window.Merged.Arr("chambers").Single(c => c.Str("id") == ATMOS_GAS_MONITOR_ENGINE);
        Assert.Equal("Supermatter Engine Chamber", engine.Str("name"));
        Assert.NotNull(engine.Get("gasmix")); // the supermatter chamber's sensor
        content = Shown(windows, replica, window.Window);
        ByKey(content, "chamber").OnSelected("Supermatter Engine Chamber");
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Supermatter Engine Chamber", ByKey(content, "reading").Title);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
