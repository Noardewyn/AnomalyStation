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
/// M7 slice 2 (smes.dm, port_gen.dm, powernet.dm:87) on MetaStation through the real server and a client's own stack: the round
/// starts on tg's charged SMES, whose output is the station network's power (avail on the powernet, measured every cycle); a
/// player opens the Engineering SMES's window and turns its output off (tg's Smes interface, translated): the power on the network
/// drops by what the unit put out. A player then loads plasma sheets into a PACMAN generator on a cable (anchored with a wrench),
/// starts it in its window (tg's PortableGenerator): the network gains what the generator adds, and the client sees its running
/// icon and hears its hum.
/// </summary>
public class SmesPacmanGameplayTests(ITestOutputHelper output)
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

    // An open tile beside the thing for the player to stand on.
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
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    [MetaServerFact]
    public void The_station_runs_on_the_charged_smes_a_player_switches_its_output_off_and_a_pacman_adds_power()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 8000 },
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
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // the round starts on tg's charged SMES: the Engineering units (90 kW each), the full ones and the super one; the station's
        // network (the biggest) carries the three Engineering units' output
        List<obj_machinery_power_smes> units = In(w, () => Dm.each_cast<obj_machinery_power_smes>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_smes))).ToList());
        Assert.NotEmpty(units);
        Assert.All(units, u => Assert.True(In(w, () => u.terminal != null && !Dm.truthy(Dm.bitand(u.machine_stat, BROKEN))), "every unit has its terminal"));
        List<obj_machinery_power_smes_engineering> engineering = units.OfType<obj_machinery_power_smes_engineering>().ToList();
        Assert.Equal(3, engineering.Count);
        obj_machinery_power_smes first = engineering[0];
        datum_powernet grid = In(w, () => first.powernet as datum_powernet);
        Assert.NotNull(grid);
        Assert.All(engineering, u => Assert.Same(grid, In(w, () => u.powernet)));
        // full at the map load; by the join (M10 slice F: after tg's round start) each has put out its 90 kW for a few seconds
        Assert.All(engineering, u => Assert.InRange(In(w, () => Dm.num(u.total_charge())), 50000000f - 90000f * 10, 50000000f));
        w.Run(10 * Second);
        float Avail() => In(w, () => grid.avail);
        for (int i = 0; i < 4; i++)
        {
            output.WriteLine($"station network: {In(w, () => grid.cables.len)} cables, {In(w, () => grid.nodes.len)} machines, avail {Avail()}, netexcess {In(w, () => grid.netexcess)}");
            w.Run(2 * Second);
        }
        float onGrid = Avail();
        Assert.True(onGrid > 0, "the SMES's output reaches the station network");
        float perUnit = In(w, () => Dm.num(power_to_energy(first.output_level)));
        Assert.Equal(180000f, perUnit); // 90 kW over SSmachines' 2 seconds
        Assert.True(onGrid >= 3 * perUnit, $"the three Engineering units alone put out {3 * perUnit}, the network has {onGrid}");
        output.WriteLine($"the station network has {onGrid} J of power each cycle; one Engineering unit puts out {perUnit}");

        // a player beside the first Engineering unit opens its window
        turf stand = In(w, () => Beside(first));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, first.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Smes", window.Interface);
        Assert.Equal(1, window.Merged.Num("outputAttempt"));
        Assert.Equal(90000, window.Merged.Num("outputLevel"));
        Assert.Equal(200000, window.Merged.Num("outputLevelMax"));
        Assert.Equal(50000000, window.Merged.Num("capacity"));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Contains("Stored Energy", content.AllText());
        Assert.Contains("Sending", content.AllText());
        UiNode slider = ByKey(ByKey(content, "outputLevel"), "slider");
        Assert.Equal(90, slider.Value); // the output slider: 90 kW of 200 kW
        Assert.Equal(200, slider.MaxValue);
        Assert.Equal("90.00 kW", slider.Format(slider.Value));
        Assert.Equal(340, windows.Get(window.Window).Context.WindowWidth);
        // the client sees the unit's lights: output, input and the charge (smes.dm:140)
        Assert.Contains("smes-op1", OverlaysOf(replica, first));
        Assert.Contains("smes-og4", OverlaysOf(replica, first));

        // output Off: the unit stops feeding the network, which loses what it put out
        ByKey(content, "tryoutput").OnClick();
        w.Run(4 * Second);
        Assert.Equal(0f, In(w, () => first.output_attempt));
        Assert.Equal(0, replica.UiWindows[window.Window].Merged.Num("outputAttempt"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Off", ByKey(content, "tryoutput").Text);
        Assert.Contains("Not Sending", content.AllText()); // charge left, not sending
        Assert.Contains("smes-op0", OverlaysOf(replica, first));
        float afterOff = Avail();
        output.WriteLine($"after the output was switched off: {afterOff} (was {onGrid})");
        Assert.Equal(onGrid - perUnit, afterOff, 1);
        // and on again: the window's output level is set to its maximum and the unit feeds the grid at that level
        ByKey(content, "tryoutput").OnClick();
        w.Run(2 * Second);
        content = Shown(windows, replica, window.Window);
        ByKey(ByKey(content, "outputLevel"), "max").OnClick();
        w.Run(4 * Second);
        Assert.Equal(200000f, In(w, () => first.output_level));
        float perUnitMax = In(w, () => Dm.num(power_to_energy(first.output_level)));
        Assert.Equal(400000f, perUnitMax);
        Assert.Equal(onGrid - perUnit + perUnitMax, Avail(), 1);

        // the PACMAN: a generator with no fuel, on a cable node of the map, unanchored as Meta places it
        obj_machinery_power_port_gen_pacman gen = In(w, () => Dm.each_cast<obj_machinery_power_port_gen_pacman>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_port_gen_pacman)))
            .Where(g => g.GetType() == typeof(obj_machinery_power_port_gen_pacman) && get_turf(g).get_cable_node(CABLE_LAYER_ALL) != null && Beside(g) != null).OrderBy(g => g.dm_id).FirstOrDefault());
        Assert.NotNull(gen);
        output.WriteLine($"the PACMAN at {In(w, () => gen.x)},{In(w, () => gen.y)} in {In(w, () => Dm.str(((area)get_area(gen)).name))}: sheets {In(w, () => gen.sheets)}");
        Assert.False(In(w, () => Dm.truthy(gen.anchored)));
        Assert.Null(In(w, () => gen.powernet));
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(gen))));
        w.Run(Second);
        Assert.Equal("portgen0_0", IconOf(replica, gen)); // idle: tg's icon_state (port_gen.dm:60)

        // a wrench anchors it on the cable and it joins that cable's network
        var wrench = In(w, () => Dm.@new<obj_item_wrench>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(wrench)))));
        w.Run(2);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, gen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(gen.anchored)));
        datum_powernet net = In(w, () => gen.powernet as datum_powernet);
        Assert.NotNull(net);
        In(w, () => qdel(wrench));
        w.Run(2);

        // 30 plasma sheets in the hand go into it
        var sheets = In(w, () => Dm.@new<obj_item_stack_sheet_mineral_plasma>(me.loc, 30f));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(sheets)))));
        w.Run(2);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, gen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(30f, In(w, () => gen.sheets ?? 0));
        Assert.True(In(w, () => QDELETED(sheets)), "the sheets are used up");

        // the empty hand opens its window (tg's PortableGenerator)
        float netBefore = In(w, () => net.avail);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, gen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState genWindow = replica.UiWindows.Values.Single(x => x.Interface == "PortableGenerator");
        UiNode genContent = Shown(windows, replica, genWindow.Window);
        Assert.Equal(450, windows.Get(genWindow.Window).Context.WindowWidth);
        Assert.DoesNotContain("Generator not anchored.", genContent.AllText());
        Assert.Equal("30", ByKey(genContent, "sheets").Text);
        Assert.Equal("Off", ByKey(genContent, "power").Text);
        Assert.False(ByKey(genContent, "power").Disabled); // anchored and fuelled: ready to boot
        Assert.Equal("portgen0_0", IconOf(replica, gen));
        heard.Clear();

        // power on: the generator runs, the network gains its level each cycle, the client sees the icon and hears the hum
        ByKey(genContent, "power").OnClick();
        w.Run(6 * Second);
        Assert.True(In(w, () => Dm.truthy(gen.active)));
        Assert.Equal("portgen0_1", IconOf(replica, gen));
        float level = In(w, () => Dm.num(power_to_energy(gen.power_gen * gen.power_output)));
        Assert.Equal(20000f, level);
        float netRunning = In(w, () => net.avail);
        output.WriteLine($"the PACMAN's network: avail {netBefore} before, {netRunning} running");
        Assert.Equal(netBefore + level, netRunning, 1);
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").Contains("machines/generator/generator_start"));
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").Contains("machines/generator/generator_mid"));
        genContent = Shown(windows, replica, genWindow.Window);
        Assert.Equal("On", ByKey(genContent, "power").Text);
        Assert.Equal(29, replica.UiWindows[genWindow.Window].Merged.Num("sheets")); // the first pass opened a sheet (port_gen.dm:139)
        Assert.Equal("29", ByKey(genContent, "sheets").Text);
        Assert.True(In(w, () => gen.current_heat) > 0);

        // a higher level: twice the power
        ByKey(genContent, "higher").OnClick();
        w.Run(4 * Second);
        Assert.Equal(2f, In(w, () => gen.power_output));
        Assert.Equal(netBefore + 2 * level, In(w, () => net.avail), 1);

        // off: the icon goes, the end sound plays, the network loses it
        heard.Clear();
        genContent = Shown(windows, replica, genWindow.Window);
        ByKey(genContent, "power").OnClick();
        w.Run(4 * Second);
        Assert.False(In(w, () => Dm.truthy(gen.active)));
        Assert.Equal("portgen0_0", IconOf(replica, gen));
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").Contains("machines/generator/generator_end"));
        Assert.Equal(netBefore, In(w, () => net.avail), 1);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
