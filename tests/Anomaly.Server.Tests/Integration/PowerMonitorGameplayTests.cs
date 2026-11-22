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
/// M7 power monitor slice (monitor.dm, _computer.dm, PowerMonitor.tsx) on MetaStation through the real server and a client's own
/// stack: the map's two power monitoring consoles each sit on a cable of the station's network. An engineer opens the Bridge console
/// and the client gets the PowerMonitor window: the supply and demand history, and a row for every APC of the network with its
/// charge, load and channels; the client draws the console's screen and keyboard. A channel switched off at an APC and the APC's
/// breaker shown in the next update; the sort buttons order the rows.
/// </summary>
public class PowerMonitorGameplayTests(ITestOutputHelper output)
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

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    [MetaServerFact]
    public void An_engineer_opens_the_bridge_power_monitor_and_sees_the_stations_apcs_and_a_changed_channel_in_the_next_update()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // the station's two consoles, both live and each on a cable (CentCom has its own, D299)
        List<obj_machinery_computer_monitor> monitors = In(w, () => Dm.each_cast<obj_machinery_computer_monitor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_monitor))).Where(m => m.z == 1).ToList());
        Assert.Equal(2, monitors.Count);
        obj_machinery_computer_monitor bridge = monitors.Single(m => In(w, () => Dm.str(m.name)) == "Bridge Power Monitoring Console");
        Assert.All(monitors, m => Assert.True(In(w, () => Dm.@in(m, SSmachines.processing_late) && m.get_powernet() is datum_powernet)));
        // (the other console is the AI satellite's, on the satellite's own network)
        // an engineer beside it (M10 slice C: a client holds what is around it)
        turf stand = In(w, () => Beside(bridge));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(10 * Second);

        // the client draws the console's screen and keyboard (the computer base, _computer.dm:67)
        Assert.Contains("power", OverlaysOf(replica, bridge));
        Assert.Contains("power_key", OverlaysOf(replica, bridge));

        // the engineer opens its window
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, bridge.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("PowerMonitor", window.Interface);
        Assert.Equal(1, window.Merged.Num("attached"));
        Assert.Equal(60, window.Merged.Num("stored"));
        Assert.Equal(5, window.Merged.Num("interval"));
        Assert.Contains("W", window.Merged.Str("supply"));
        IReadOnlyList<UiValue> rows = window.Merged.Arr("areas");
        output.WriteLine($"the Bridge console lists {rows.Count} APCs; supply {window.Merged.Str("supply")}, demand {window.Merged.Str("demand")}");
        Assert.True(rows.Count >= 20, "a row for every APC of the station's network");
        Assert.All(rows, r => Assert.Equal(7, r.Fields.Count));
        obj_machinery_power_apc apc = In(w, () => ((area)get_area(bridge)).apc);
        Assert.NotNull(apc);
        string areaName = In(w, () => Dm.str(apc.area.name)).TrimStart('\uE001'); // "\improper" marks the name; the wire drops it
        UiValue bridgeRow = rows.First(r => r.Str("name") == areaName);
        Assert.InRange(bridgeRow.Num("charge"), 1, 100);
        Assert.Equal(3, bridgeRow.Num("eqp"));
        Assert.Equal(3, bridgeRow.Num("lgt"));
        Assert.Equal(3, bridgeRow.Num("env"));
        IReadOnlyList<UiValue> supply = window.Merged.Get("history").Arr("supply");
        Assert.NotEmpty(supply);
        Assert.NotEmpty(window.Merged.Get("history").Arr("demand"));
        Assert.True(supply.Any(v => v.AsNum() > 0), "the station's SMES feed the network");

        // the window: the history lines, the progress bars and a table row for every APC with its three channel boxes
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal(550, windows.Get(window.Window).Context.WindowWidth);
        Assert.Equal(2, content.Walk().Count(n => n.Kind == UiKind.ChartLine));
        Assert.Equal(rows.Count, content.Walk().Count(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("area:")));
        Assert.Equal(3 * rows.Count, content.Walk().Count(n => n.Kind == UiKind.ColorBox));

        // the room's APC: its breaker off (the APC has to be operational; it is unlocked by the engineer's ID for the test)
        float chargeBefore = (float)bridgeRow.Num("charge");
        string loadBefore = bridgeRow.Str("load");
        In(w, () => Assert.Equal(1f, Dm.num(apc.operating)));
        In(w, () => Waiting.NoSleep(apc.toggle_breaker(me)));
        // a channel set to off by hand shows in the row as the boxes do (APC_CHANNEL_OFF 0, auto-off 1)
        In(w, () =>
        {
            apc.equipment = 0;
            apc.update();
        });
        w.Run(4 * Second);
        rows = replica.UiWindows[window.Window].Merged.Arr("areas");
        UiValue after = rows.First(r => r.Str("name") == bridgeRow.Str("name"));
        output.WriteLine($"the Bridge APC row: before load {loadBefore}, charge {chargeBefore}; after load {after.Str("load")}, charge {after.Num("charge")}, eqp {after.Num("eqp")}");
        Assert.Equal(0f, In(w, () => Dm.num(apc.operating))); // the breaker is off
        Assert.NotEqual(loadBefore, after.Str("load")); // and the APC draws nothing
        Assert.Equal(0, after.Num("eqp"));
        Assert.Equal(In(w, () => Dm.num(apc.equipment)), after.Num("eqp"));
        Assert.Equal(In(w, () => Dm.str(display_power(apc.lastused_total))), after.Str("load"));
        content = Shown(windows, replica, window.Window);
        UiNode areaRow = content.Walk().First(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("area:" + bridgeRow.Str("name")));
        UiNode eqpBox = ByKey(ByKey(areaRow, "eqp"), "box");
        Assert.Equal("bad", eqpBox.Color);
        Assert.Equal("M", eqpBox.Text); // manual
        Assert.Equal("Off [manual]", eqpBox.Tooltip);
        Assert.Equal("good", ByKey(ByKey(areaRow, "env"), "box").Color);

        // the sort buttons
        ByKey(content, "sort:name").OnClick();
        w.Run(2);
        content = Shown(windows, replica, window.Window);
        List<string> names = content.Walk().Where(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("area:")).Select(n => ByKey(n, "name").Text).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal).ToList(), names);
        Assert.True(ByKey(content, "sort:name").Checkbox);

        // closed (_computer.dm:177 sets idle use, process_late sets it active again while the console has its network)
        p.Session.UiClose(window.Window);
        w.Run(Second);
        Assert.Empty(replica.UiWindows.Tgui());

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
