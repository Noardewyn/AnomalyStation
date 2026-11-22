using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package E9 (docs/ai/specs/engineering.md) on MetaStation through the real server and a client's own stack: an engineer puts a space heater
/// on the open floor, switches it on with a right click, opens its window (tg's SpaceHeater interface, translated), opens the hatch with a
/// screwdriver, sets the heat mode and a target temperature in the window and sees the room's air warm up while the cell drains (the
/// window's bar and the picture follow), then the cool mode cools it; the cell ejects from the window. A drained power cell put into a
/// cell charger by hand charges from the grid and the charger's pictures follow the level; the hand takes it out again.
/// </summary>
public class SpaceHeaterChargerGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20; // ticks per second
    private const int StandX = 45, StandY = 154; // the Auxiliary Base Construction zone's plating (CanisterVisualsGameplayTests)

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

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        return shown;
    }

    private sealed class Setup : IDisposable
    {
        public RealHeadlessWorld W { get; }
        public RealHeadlessWorld.Player P { get; }
        public mob_living_carbon_human Me { get; }
        public ReplicaWorld Replica { get; }
        public TguiWindows Windows { get; }
        public int Z { get; }
        public int BootRuntimes { get; }

        public Setup(ulong seed, ITestOutputHelper output)
        {
            W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 24000 },
                MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
            P = W.Join("Engineer");
            Me = (mob_living_carbon_human)W.MobOf(P);
            Replica = P.Session.Replica;
            Windows = new TguiWindows
            {
                SendAct = (window, action, parameters) => P.Session.UiAct(window, action, parameters),
                SendClose = window => P.Session.UiClose(window),
            };
            BootRuntimes = W.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
            W.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e.Message.Split('\n')[0]);
            Z = In(W, () => (int)Me.z);
            using (W.World.Enter())
                Waiting.NoSleep(Me.forceMove(locate(StandX, StandY, Z)));
        }

        public void Dispose() => W.Dispose();

        public T Place<T>(int dx, int dy = 0) where T : atom
        {
            T thing = In(W, () =>
            {
                turf at = locate(StandX + dx, StandY + dy, Z) as turf;
                Assert.True(at is turf_open && !Dm.truthy(at.density), "an open floor beside the engineer: " + at);
                return Dm.@new<T>(at);
            });
            W.Run(2 * Second);
            return thing;
        }

        public void DropHeld()
        {
            using (W.World.Enter())
                foreach (object held in Dm.each(Me.held_items.Copy()))
                    if (held is obj_item old)
                        Waiting.NoSleep(Me.dropItemToGround(old));
        }

        public void Click(atom target, ClickButton button = ClickButton.Left)
        {
            Assert.True(P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None));
            W.Run(Second);
        }

        // an item made in the empty active hand
        public T Hold<T>(string path) where T : obj_item
        {
            DropHeld();
            In(W, () => DevCommands.Give(Me, path));
            T item = In(W, () => Me.get_active_held_item() as T);
            Assert.NotNull(item);
            return item;
        }

        public UiWindowState Window(string @interface) => Assert.Single(Replica.UiWindows.Tgui(), v => v.Interface == @interface);

        public UiNode Content(UiWindowState window) => Shown(Windows, Replica, window.Window);
    }

    // the mean temperature of the tiles within three of the heater: the room's air, which the heater's own tile soon equalizes with
    private static float RoomTemperature(RealHeadlessWorld w, atom at) => In(w, () =>
    {
        var temperatures = new List<float>();
        foreach (turf t in Dm.each(range(3, at)).OfType<turf>())
            if (t is turf_open)
                temperatures.Add(((datum_gas_mixture)Waiting.NoSleep(t.return_air())).temperature);
        return temperatures.Average();
    });

    [MetaServerFact]
    public void An_engineer_runs_a_space_heater_from_its_window_and_the_room_warms_and_cools_while_the_cell_drains()
    {
        using var s = new Setup(91, output);
        RealHeadlessWorld w = s.W;
        var heater = s.Place<obj_machinery_space_heater>(1);
        Assert.Equal("sheater-off", IconOf(s.Replica, heater));
        Assert.Equal(100000f, In(w, () => heater.cell_value.maxcharge)); // the high-capacity cell it spawns with
        // the heater's efficiency turns a joule of the cell into 2000 of heat: a full cell would not show a drop in the whole-percent bar of
        // the window within the test, so the cell starts at a hundredth of its charge
        In(w, () => heater.cell_value.charge = 1500);
        float start = RoomTemperature(w, heater);
        output.WriteLine($"the room before: {start} K");

        // a right click with the empty hand switches it on (tg's attack_hand_secondary); inside the one degree of tolerance it idles
        s.Click(heater, ClickButton.Right);
        Assert.Equal(1f, In(w, () => heater.on));
        Assert.Equal("sheater-standby", IconOf(s.Replica, heater));
        Assert.Equal(1.5f, In(w, () => (float)Dm.num(heater.cell_value.percent())));

        // the window with an empty hand
        s.DropHeld();
        s.Click(heater);
        UiWindowState window = s.Window("SpaceHeater");
        UiNode content = s.Content(window);
        Assert.Equal((400, 305), (s.Windows.Get(window.Window).Context.WindowWidth, s.Windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal((1, 0, "auto", 1), ((int)window.Merged.Num("on"), (int)window.Merged.Num("open"), window.Merged.Get("mode").Text, (int)window.Merged.Num("hasPowercell")));
        Assert.Equal(2, window.Merged.Num("powerLevel")); // round(1.5, 1): the nearest whole percent
        Assert.Equal("On", ByKey(content, "power").Text);
        Assert.Equal("2%", ByKey(content, "cell").Text);
        Assert.Equal("20°C", ByKey(content, "target").Text); // the hatch is shut: text, not a field
        Assert.Equal("Auto", ByKey(content, "mode").Text);
        Assert.True(ByKey(content, "eject").Disabled);

        // a screwdriver opens the hatch; the open window gets the target field, the modes and the cell's eject
        s.Hold<obj_item_screwdriver>("/obj/item/screwdriver");
        s.Click(heater);
        Assert.Equal(1f, In(w, () => heater.panel_open));
        Assert.Contains("sheater-open", OverlaysOf(s.Replica, heater));
        Assert.Equal(1, s.Replica.UiWindows[window.Window].Merged.Num("open"));
        content = s.Content(window);
        Assert.False(ByKey(content, "eject").Disabled);
        Assert.Equal(new[] { "Auto", "Heat", "Cool" }, content.Walk().Where(n => n.Key?.StartsWith("mode:") == true).Select(n => n.Text));

        // heat mode, 40 degrees (tg floors the 313.15 K the field sends to 313, which the window rounds back to 40)
        ByKey(content, "mode:heat").OnClick();
        w.Run(Second);
        Assert.Equal("heat", In(w, () => heater.set_mode));
        content = s.Content(window);
        Assert.True(ByKey(content, "mode:heat").Selected);
        ByKey(content, "target").OnChange(40);
        w.Run(Second);
        Assert.Equal(313f, In(w, () => Dm.num(heater.target_temperature)));
        content = s.Content(window);
        Assert.Equal(40, ByKey(content, "target").Value); // tg shows round(313 - T0C, 1): the nearest whole degree

        // the room warms up, the cell drains, the picture lights
        var icons = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            w.Run(Second);
            icons.Add(IconOf(s.Replica, heater));
        }
        float warm = RoomTemperature(w, heater);
        float charge = In(w, () => heater.cell_value.charge);
        output.WriteLine($"after 10 s of heating: the room {warm} K, the cell {charge}, pictures {string.Join(", ", icons)}");
        Assert.True(warm > start + 3f, $"the room warms up: {start} -> {warm}");
        Assert.True(charge < 1500f - 100f, "the cell paid for it: " + charge);
        Assert.Contains("sheater-heat", icons);
        double level = s.Replica.UiWindows[window.Window].Merged.Num("powerLevel");
        Assert.True(level < 2, "the window's bar follows the cell: " + level);
        content = s.Content(window);
        Assert.Equal((level / 100, level + "%"), (ByKey(content, "cell").Value, ByKey(content, "cell").Text));
        Assert.True(s.Replica.UiWindows[window.Window].Merged.Num("currentTemp") > start - 273.15f + 2);

        // cool mode, 0 degrees (the range's floor): the room cools
        ByKey(content, "mode:cool").OnClick();
        w.Run(Second);
        ByKey(content, "target").OnChange(0);
        w.Run(Second);
        Assert.Equal("cool", In(w, () => heater.set_mode));
        Assert.Equal(273.15f, In(w, () => Dm.num(heater.target_temperature)), 0.01);
        icons.Clear();
        for (int i = 0; i < 10; i++)
        {
            w.Run(Second);
            icons.Add(IconOf(s.Replica, heater));
        }
        float cool = RoomTemperature(w, heater);
        output.WriteLine($"after 10 s of cooling: the room {cool} K, pictures {string.Join(", ", icons)}");
        Assert.True(cool < warm - 3f, $"the room cools down: {warm} -> {cool}");
        Assert.Contains("sheater-cool", icons);

        // the cell ejects into the engineer's free hand, and the heater switches itself off for want of it
        ByKey(s.Content(window), "eject").OnClick();
        w.Run(2 * Second);
        Assert.Null(In(w, () => heater.cell_value));
        Assert.Equal(1, In(w, () => Dm.each<obj_item_stock_parts_power_store_cell>(s.Me.held_items).Count()));
        Assert.Equal(0f, In(w, () => heater.on));
        content = s.Content(window);
        Assert.Equal("None", ByKey(content, "cell").Text);
        Assert.True(ByKey(content, "power").Disabled);

        Assert.Equal(s.BootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void An_engineer_puts_a_drained_power_cell_into_a_cell_charger_and_it_charges_and_the_charger_shows_the_level()
    {
        using var s = new Setup(92, output);
        RealHeadlessWorld w = s.W;
        var charger = s.Place<obj_machinery_cell_charger>(1);
        Assert.Equal("ccharger", IconOf(s.Replica, charger));
        Assert.Empty(OverlaysOf(s.Replica, charger));
        output.WriteLine($"the charger's area: {In(w, () => (get_area(charger) as area)?.name)}, equipment power {In(w, () => (get_area(charger) as area)?.power_equip)}, rate {In(w, () => charger.charge_rate)}");

        // a drained high-capacity cell in the hand, a click on the charger puts it in
        var cell = s.Hold<obj_item_stock_parts_power_store_cell>("/obj/item/stock_parts/power_store/cell/high");
        In(w, () => cell.charge = 0);
        s.Click(charger);
        Assert.Same(cell, In(w, () => charger.charging));
        Assert.Same(charger, In(w, () => cell.loc));
        Assert.Null(In(w, () => s.Me.get_active_held_item()));
        List<string> shown = OverlaysOf(s.Replica, charger);
        output.WriteLine("the charger shows: " + string.Join(", ", shown));
        Assert.Contains("ccharger-o0", shown);
        Assert.Contains("ccharger-standard-on", shown);
        Assert.Contains("hcell", shown); // the cell's own picture

        // it charges from the grid, a cycle at a time, and the level overlay climbs
        var levels = new List<string> { "ccharger-o0" };
        float last = 0;
        for (int i = 0; i < 60; i++)
        {
            w.Run(2 * Second);
            float now = In(w, () => cell.charge);
            string level = OverlaysOf(s.Replica, charger).FirstOrDefault(o => o.StartsWith("ccharger-o"));
            if (level != null && level != levels[^1])
                levels.Add(level);
            if (i % 10 == 0)
                output.WriteLine($"{i * 2} s: charge {now} ({level})");
            last = now;
            if (now >= 100000f)
                break;
        }
        output.WriteLine($"levels seen: {string.Join(", ", levels)}; the cell holds {last}");
        Assert.True(last > 5000f, "the cell charges from the grid: " + last);
        Assert.True(levels.Count >= 2, "the level overlay follows the charge: " + string.Join(", ", levels));
        Assert.Contains(OverlaysOf(s.Replica, charger), o => o.StartsWith("cell-standard-o")); // the charge light of tg

        // the hand takes it out: in the hand, the charger bare again
        s.Click(charger);
        Assert.Null(In(w, () => charger.charging));
        Assert.Same(cell, In(w, () => s.Me.get_active_held_item()));
        Assert.Empty(OverlaysOf(s.Replica, charger));

        Assert.Equal(s.BootRuntimes, w.World.RuntimeErrorCount);
    }
}
