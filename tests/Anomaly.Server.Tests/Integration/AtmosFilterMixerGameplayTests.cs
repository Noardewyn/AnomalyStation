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
/// Package E1 (filter.dm, mixer.dm: ui_interact, ui_data, ui_act) on MetaStation through the real server and a client's own stack: an
/// engineer clicks the supermatter room's gas filter with an empty hand, tg's AtmosFilter window opens, and its power button, transfer
/// rate field, Max button and gas toggles change the machine; then the same with an atmospherics gas mixer's AtmosMixer window (power,
/// output pressure, the two nodes' concentrations).
/// </summary>
public class AtmosFilterMixerGameplayTests(ITestOutputHelper output)
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
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void An_engineer_sets_the_supermatter_rooms_gas_filter_and_an_atmospherics_mixer_in_their_windows()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 57, TimeoutSteps = 12000 },
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

        // ---- the gas filter of the supermatter room (Meta's filter/flipped/critical: off, nothing selected, 200 L/s)
        var filter = In(w, () => Dm.each<obj_machinery_atmospherics_components_trinary_filter>(Dm.world)
            .Where(f => get_area(f) is area_station_engineering_supermatter_room && Beside(f) != null).OrderBy(f => f.dm_id).First());
        output.WriteLine($"the filter {In(w, () => filter.GetType().Name)} at {In(w, () => $"{filter.x},{filter.y}")}");
        In(w, () =>
        {
            Assert.Equal(0f, filter.on);
            Assert.Equal(0f, filter.filter_type.len);
            Assert.Equal(200f, filter.transfer_rate);
        });
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(filter))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(filter), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AtmosFilter", window.Interface);
        Assert.Equal(0, window.Merged.Num("on"));
        Assert.Equal(200, window.Merged.Num("rate"));
        Assert.Equal(200, window.Merged.Num("max_rate"));
        int gases = (int)In(w, () => ((DmList)GLOB.meta_gas_info[META_GAS_ID]).len);
        IReadOnlyList<UiValue> offered = window.Merged.Arr("filter_types");
        Assert.Equal(gases, offered.Count);
        Assert.All(offered, f => Assert.False(f.Truthy("enabled")));
        Assert.Contains(offered, f => f.Str("gas_id") == "plasma");
        Assert.All(offered, f => Assert.NotEqual("None", TguiFormat.GetGasLabel(f.Str("gas_id")))); // every gas of tg has a label in the client
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((440.0, 240.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Contains("Gas Filter", content.AllText());
        Assert.Equal("Off", ByKey(content, "power").Text);
        Assert.Equal(gases, content.Walk().Count(n => n.Kind == UiKind.Button && n.Icon == "square-o"));

        // power
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => filter.on));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("On", ByKey(content, "power").Text);

        // the transfer rate: a typed number, the clamp both ways, a non-number ignored, and Max
        ByKey(content, "rate").OnChange(120);
        w.Run(Second);
        Assert.Equal(120f, In(w, () => filter.transfer_rate));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(120, ByKey(content, "rate").Value);
        Assert.False(ByKey(content, "rate-max").Disabled);
        ByKey(content, "rate").OnChange(9000);
        w.Run(Second);
        Assert.Equal(200f, In(w, () => filter.transfer_rate));
        ByKey(Shown(windows, replica, window.Window), "rate").OnChange(-4);
        w.Run(Second);
        Assert.Equal(0f, In(w, () => filter.transfer_rate));
        p.Session.UiAct(window.Window, "rate", UiValue.FromJson("{\"rate\":\"banana\"}")); // tg: text2num("banana") is null, nothing changes
        w.Run(Second);
        Assert.Equal(0f, In(w, () => filter.transfer_rate));
        ByKey(Shown(windows, replica, window.Window), "rate-max").OnClick();
        w.Run(Second);
        Assert.Equal(200f, In(w, () => filter.transfer_rate));
        Assert.True(ByKey(Shown(windows, replica, window.Window), "rate-max").Disabled);

        // the gas toggles: plasma and N2 on (the list holds them), plasma off again; an unknown id changes nothing
        ByKey(Shown(windows, replica, window.Window), "plasma").OnClick();
        ByKey(Shown(windows, replica, window.Window), "n2").OnClick();
        w.Run(Second);
        Assert.Equal(2f, In(w, () => filter.filter_type.len));
        Assert.True(In(w, () => Dm.@in(typeof(datum_gas_plasma), filter.filter_type)));
        Assert.True(In(w, () => Dm.@in(typeof(datum_gas_nitrogen), filter.filter_type)));
        content = Shown(windows, replica, window.Window);
        Assert.True(ByKey(content, "plasma").Selected);
        Assert.Equal("check-square-o", ByKey(content, "plasma").Icon);
        Assert.Equal("Plasma", ByKey(content, "plasma").Text);
        Assert.Equal(2, replica.UiWindows[window.Window].Merged.Arr("filter_types").Count(f => f.Truthy("enabled")));
        ByKey(content, "plasma").OnClick();
        p.Session.UiAct(window.Window, "toggle_filter", UiValue.FromJson("{\"val\":\"not_a_gas\"}"));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => filter.filter_type.len));
        Assert.False(In(w, () => Dm.@in(typeof(datum_gas_plasma), filter.filter_type)));
        content = Shown(windows, replica, window.Window);
        Assert.False(ByKey(content, "plasma").Selected);
        Assert.Equal("square-o", ByKey(content, "plasma").Icon);
        Assert.True(ByKey(content, "n2").Selected);

        // off again
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => filter.on));
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- an atmospherics gas mixer (Meta's mixers: 50% on each node, one atmosphere out)
        var mixer = In(w, () => Dm.each<obj_machinery_atmospherics_components_trinary_mixer>(Dm.world)
            .Where(m => Beside(m) != null).OrderBy(m => m.dm_id).First());
        output.WriteLine($"the mixer {In(w, () => mixer.GetType().Name)} at {In(w, () => $"{mixer.x},{mixer.y}")} in {In(w, () => Dm.str(((area)get_area(mixer)).name))}");
        In(w, () =>
        {
            Assert.Equal(0f, mixer.on);
            Assert.Equal(0.5f, mixer.node1_concentration);
            Assert.Equal(0.5f, mixer.node2_concentration);
        });
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(mixer))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(mixer), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AtmosMixer", window.Interface);
        Assert.Equal(0, window.Merged.Num("on"));
        Assert.Equal(Math.Floor(In(w, () => Dm.num(mixer.target_pressure))), window.Merged.Num("set_pressure"));
        Assert.Equal(MAX_OUTPUT_PRESSURE, window.Merged.Num("max_pressure"));
        Assert.Equal(50, window.Merged.Num("node1_concentration"));
        Assert.Equal(50, window.Merged.Num("node2_concentration"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal((370.0, 165.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal(new[] { "Power", "Output Pressure", "Main Node", "Side Node" }, content.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));

        // power
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => mixer.on));
        Assert.Equal("On", ByKey(Shown(windows, replica, window.Window), "power").Text);

        // the output pressure: a typed number, the clamp, and Max
        ByKey(Shown(windows, replica, window.Window), "pressure").OnChange(1500);
        w.Run(Second);
        Assert.Equal(1500f, In(w, () => mixer.target_pressure));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(1500, ByKey(content, "pressure").Value);
        Assert.False(ByKey(content, "pressure-max").Disabled);
        ByKey(content, "pressure").OnChange(99999);
        w.Run(Second);
        Assert.Equal((float)MAX_OUTPUT_PRESSURE, In(w, () => mixer.target_pressure));
        ByKey(Shown(windows, replica, window.Window), "pressure").OnChange(10);
        w.Run(Second);
        ByKey(Shown(windows, replica, window.Window), "pressure-max").OnClick();
        w.Run(Second);
        Assert.Equal((float)MAX_OUTPUT_PRESSURE, In(w, () => mixer.target_pressure));
        Assert.True(ByKey(Shown(windows, replica, window.Window), "pressure-max").Disabled);

        // the nodes: one set, the other follows (adjust_node1_value)
        ByKey(Shown(windows, replica, window.Window), "node1").OnChange(30);
        w.Run(Second);
        Assert.Equal(0.3f, In(w, () => mixer.node1_concentration), 4);
        Assert.Equal(0.7f, In(w, () => mixer.node2_concentration), 4);
        content = Shown(windows, replica, window.Window);
        Assert.Equal(30, ByKey(content, "node1").Value);
        Assert.Equal(70, ByKey(content, "node2").Value);
        ByKey(content, "node2").OnChange(25);
        w.Run(Second);
        Assert.Equal(0.75f, In(w, () => mixer.node1_concentration), 4);
        Assert.Equal(0.25f, In(w, () => mixer.node2_concentration), 4);
        content = Shown(windows, replica, window.Window);
        Assert.Equal(75, ByKey(content, "node1").Value);
        Assert.Equal(25, ByKey(content, "node2").Value);

        // off again
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => mixer.on));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
