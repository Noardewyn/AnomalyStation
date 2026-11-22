using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
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
/// M7 slice G1 (solar.dm, tracker.dm, sun.dm) on MetaStation through the real server and a client's own stack: the Port Quarter array
/// (32 panels of the "aftport" id, a tracker, a control computer and an SMES in its maintenance room). MetaStation's map (not the port)
/// leaves the arrays unwired: the cable of every group of panels, the tracker and the maintenance room's cable stop short of each other
/// (a gap of plating or of space between them), so the test lays the missing cable, as the engineers do before an array works. Then:
/// the panels stand at azimuth 0 and make nothing at the sun's place; a player opens the control's window, scans for hardware (the panels
/// and the tracker are found), sets tracking to Auto: the panels turn to the sun, the client sees each panel's overlay turned by tg's
/// transform and the control's screen; the panels' power reaches the network and charges the array's SMES.
/// </summary>
public class SolarGameplayTests(ITestOutputHelper output)
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

    // What the client draws of a panel or a tracker: the azimuth of its first overlay that carries tg's transform and an icon state with that prefix
    // (the same rule as the 3D model's, MapRenderer.SunAzimuth), or null when the client has the thing but no turned overlay.
    private static float? TurnOf(ReplicaWorld replica, atom a, string prefix)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return null;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay) && overlay.Transform is { Length: 6 } t && (replica.ResolveString(overlay.IconState) ?? "").StartsWith(prefix, StringComparison.Ordinal))
                return ModelRules.AzimuthOf(t[0], t[1]);
        }
        return null;
    }

    private static string IconOfOverlay(ReplicaWorld replica, atom a, string prefix)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return null;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay) && (replica.ResolveString(overlay.IconState) ?? "").StartsWith(prefix, StringComparison.Ordinal))
                return replica.ResolveResource(overlay.Icon);
        }
        return null;
    }

    private static List<obj_structure_cable> CablesOf(datum_powernet net) => Dm.each_cast<obj_structure_cable>(net.cables).ToList();

    // Lays cable between the two nearest cables of two networks, along an L of tiles (the engineers' coil on plating or a catwalk); the map's own
    // cable is left alone.
    private static int Lay(List<obj_structure_cable> from, List<obj_structure_cable> to)
    {
        (obj_structure_cable a, obj_structure_cable b) best = default;
        int distance = int.MaxValue;
        foreach (obj_structure_cable a in from)
        {
            foreach (obj_structure_cable b in to)
            {
                int d = (int)(Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y));
                if (d < distance)
                    (distance, best) = (d, (a, b));
            }
        }
        int laid = 0;
        int x = (int)best.a.x, y = (int)best.a.y;
        while (x != (int)best.b.x || y != (int)best.b.y)
        {
            if (x != (int)best.b.x)
                x += Math.Sign((int)best.b.x - x);
            else
                y += Math.Sign((int)best.b.y - y);
            turf here = locate(x, y, 1);
            if (!Dm.each_cast<atom>(here.contents).Any(c => c is obj_structure_cable))
            {
                Dm.@new<obj_structure_cable>(here);
                laid++;
            }
        }
        return laid;
    }

    [MetaServerFact]
    public void The_port_quarter_array_is_wired_scanned_and_set_to_auto_by_a_player_and_charges_its_smes()
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

        // the array: the "aftport" control, its 32 panels, the nearest tracker and the SMES by the control
        obj_machinery_power_solar_control control = In(w, () => Dm.each_cast<obj_machinery_power_solar_control>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_solar_control)))
            .Single(c => Dm.str(c.id) == "aftport"));
        List<obj_machinery_power_solar> panels = In(w, () => Dm.each_cast<obj_machinery_power_solar>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_solar)))
            .Where(s => Dm.str(s.id) == "aftport").ToList());
        Assert.Equal(32, panels.Count);
        obj_machinery_power_tracker tracker = In(w, () => Dm.each_cast<obj_machinery_power_tracker>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_tracker)))
            .OrderBy(t => Math.Abs(t.x - panels[0].x) + Math.Abs(t.y - panels[0].y)).First());
        obj_machinery_power_smes smes = In(w, () => Dm.each_cast<obj_machinery_power_smes>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_smes)))
            .OrderBy(s => Math.Abs(s.x - control.x) + Math.Abs(s.y - control.y)).First());
        output.WriteLine($"control at {In(w, () => control.x)},{In(w, () => control.y)}, tracker at {In(w, () => tracker.x)},{In(w, () => tracker.y)}, smes at {In(w, () => smes.x)},{In(w, () => smes.y)}");
        Assert.Equal(2, In(w, () => Dm.each_cast<atom>(control.loc.contents).Count(a => a is obj_structure_cable || a is obj_machinery_power_solar_control)));

        // as the map leaves it: every group of panels, the tracker and the maintenance room are networks of their own
        datum_powernet controlNet = In(w, () => control.powernet as datum_powernet);
        List<datum_powernet> panelNets = In(w, () => panels.Select(s => s.powernet as datum_powernet).Distinct().ToList());
        datum_powernet trackerNet = In(w, () => tracker.powernet as datum_powernet);
        Assert.NotNull(controlNet);
        Assert.DoesNotContain(controlNet, panelNets);
        Assert.NotSame(controlNet, trackerNet);
        Assert.All(panelNets, n => Assert.NotSame(trackerNet, n));
        Assert.Same(controlNet, In(w, () => smes.terminal.powernet)); // the array's SMES takes its input from the room's network
        output.WriteLine($"networks: control {In(w, () => controlNet.cables.len)} cables, {panelNets.Count} of panels, the tracker's {In(w, () => trackerNet.cables.len)}");

        // the engineers lay the missing cable (Prim's way: the nearest group to the room's network, again and again), and the networks are made again
        // as a loaded template's are (setup_template_powernets)
        int laid = In(w, () =>
        {
            var joined = CablesOf(controlNet);
            var rest = panelNets.Append(trackerNet).Select(CablesOf).ToList();
            int total = 0;
            while (rest.Count > 0)
            {
                var next = rest.OrderBy(group => joined.Min(a => group.Min(b => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y)))).First();
                total += Lay(joined, next);
                joined.AddRange(next);
                rest.Remove(next);
            }
            SSmachines.makepowernets();
            return total;
        });
        output.WriteLine($"{laid} lengths of cable laid");
        Assert.True(laid > 0);
        datum_powernet array = In(w, () => control.powernet as datum_powernet);
        Assert.Same(array, In(w, () => tracker.powernet));
        Assert.All(panels, s => Assert.Same(array, In(w, () => s.powernet)));
        Assert.Same(array, In(w, () => smes.terminal.powernet));

        // DV197: each panel starts at its own bearing; turned back to tg's 0 for the measures below
        Assert.All(panels, s => Assert.Equal(obj_machinery_power_solar.anomaly_start_azimuth(s.dm_id), In(w, () => s.azimuth_current)));
        In(w, () => panels.ForEach(s => s.azimuth_current = s.azimuth_target = 0));

        // the sun stands in the open direction of the array (the port side); the panels are still at azimuth 0 and found by no computer
        In(w, () =>
        {
            SSsun.azimuth = 270;
            SSsun.azimuth_mod = 1;
            SSsun.next_fire = Dm.world.time + 1000000; // it does not move during the test
            SSsun.complete_movement();
        });
        w.Run(6 * Second);
        Assert.Equal(0f, In(w, () => control.connected_panels.len));
        Assert.Equal(0f, In(w, () => panels[0].azimuth_current));
        Assert.Equal(0f, In(w, () => panels[0].sunfrac)); // cos(270) is nothing
        Assert.Equal(0f, In(w, () => array.avail));
        float charge0 = In(w, () => Dm.num(smes.total_charge()));

        // a player beside the control opens its window
        turf stand = In(w, () => Beside(control));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, control.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("SolarControl", window.Interface);
        Assert.Equal(0, window.Merged.Num("connected_panels"));
        Assert.Equal(0, window.Merged.Num("tracking_state"));
        Assert.Equal(12, window.Merged.Num("max_rotation_rate"));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal(330, windows.Get(window.Window).Context.WindowWidth);
        Assert.Contains("Scan for new hardware", content.AllText());
        Assert.Contains("N/A", content.AllText()); // no tracker yet
        Assert.True(ByKey(content, "auto").Disabled); // Auto needs a tracker
        Assert.Equal("bad", ByKey(content, "panels").Color);
        Assert.True(ByKey(content, "off").Selected);

        // the scan finds the 32 panels and the tracker
        ByKey(content, "refresh").OnClick();
        w.Run(Second);
        Assert.Equal(32f, In(w, () => control.connected_panels.len));
        Assert.Same(tracker, In(w, () => control.connected_tracker));
        Assert.Equal(32, replica.UiWindows[window.Window].Merged.Num("connected_panels"));
        Assert.Equal(32 * 2500, replica.UiWindows[window.Window].Merged.Num("capacity"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("32", ByKey(content, "panels").Text);
        Assert.Equal("good", ByKey(content, "panels").Color);
        Assert.Equal("OK", ByKey(content, "tracker").Text);
        Assert.False(ByKey(content, "auto").Disabled);

        // timed tracking shows the rate (tg's two number fields) and the azimuth set by hand turns the panels without the sun's help
        ByKey(content, "timed").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Assert.Equal(1, replica.UiWindows[window.Window].Merged.Num("tracking_state"));
        UiNode rate = ByKey(content, "rate");
        Assert.Equal("+6", rate.Format(rate.Value));
        Assert.Equal("°/m", rate.Unit);
        ByKey(content, "azimuth").OnChange(180);
        w.Run(Second);
        Assert.Equal(180f, In(w, () => control.azimuth_target));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(180, ByKey(content, "arrow").Rotation);
        Assert.Equal(180, ByKey(content, "azimuth").Value);

        // auto: the tracker reads the sun, the computer sets every panel to it
        ByKey(content, "auto").OnClick();
        w.Run(Second);
        Assert.Equal(2f, In(w, () => control.track));
        Assert.Equal(270f, In(w, () => control.azimuth_target));
        content = Shown(windows, replica, window.Window);
        Assert.True(ByKey(content, "auto").Selected);
        Assert.Equal("270 ° (auto)", ByKey(content, "auto-azimuth").Text);
        Assert.Equal(270, ByKey(content, "arrow").Rotation);
        w.Run(6 * Second); // three SSmachines cycles: the panels turn, see the sun and make power
        Assert.All(panels, s => Assert.Equal(270f, In(w, () => s.azimuth_current)));
        Assert.Equal(270f, In(w, () => Dm.num(tracker.azimuth_current)));
        List<float> fractions = In(w, () => panels.Select(s => s.sunfrac).ToList());
        int lit = fractions.Count(f => f == 1f);
        output.WriteLine($"{lit} of 32 panels face a sun that nothing hides, {In(w, () => panels.Count(s => Dm.truthy(s.obscured)))} are in its shadow; network power {In(w, () => array.avail)}");
        Assert.True(lit > 0, "some panel of the array sees the sun");
        Assert.Equal(lit * 5000f, In(w, () => array.avail), 1); // power_to_energy(2500 W) over SSmachines' 2 seconds, at full exposure
        Assert.Equal(lit * 2500f, In(w, () => control.lastgen), 1);
        Assert.True(In(w, () => Dm.num(smes.total_charge())) > charge0, "the array's SMES charges from its panels");
        float charge1 = In(w, () => Dm.num(smes.total_charge()));
        w.Run(6 * Second);
        Assert.True(In(w, () => Dm.num(smes.total_charge())) > charge1);
        Assert.Equal(lit * 2500, replica.UiWindows[window.Window].Merged.Num("supply"));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("good", ByKey(content, "output").Ranges.Where(r => r.From == 0.66).Select(r => r.Color));
        Assert.Contains($"{TguiFormat.FormatPower(lit * 2500)} of {TguiFormat.FormatPower(32 * 2500)}", ByKey(content, "output").Text);

        // the client sees the control's screen and keyboard, and the window's history chart
        Assert.Contains("solar", OverlaysOf(replica, control));
        Assert.Contains("power_key", OverlaysOf(replica, control));
        UiNode chart = ByKey(content, "chart");
        Assert.Equal(2, chart.Children.Count);
        Assert.Equal("rgba(235, 210, 52, 1)", ByKey(chart, "supply").StrokeColor);

        // a player beside a panel sees it turned: each panel's two overlays carry tg's transform, read back as the sun's azimuth
        obj_machinery_power_solar nearest = panels.Where(s => In(w, () => Beside(s) != null)).OrderBy(s => In(w, () => Math.Abs(s.x - control.x) + Math.Abs(s.y - control.y))).First();
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(nearest))));
        w.Run(2 * Second);
        Assert.NotNull(replica.Entities.FirstOrDefault(e => e.Id == nearest.dm_id));
        Assert.Contains("solar_panel_glass", OverlaysOf(replica, nearest));
        Assert.Contains("solar_panel_glass_edge", OverlaysOf(replica, nearest));
        Assert.Equal("icons/obj/machines/solar.dmi", IconOfOverlay(replica, nearest, "solar_panel_glass")); // VIS_INHERIT_ICON: the panel's own sheet
        float? turned = TurnOf(replica, nearest, "solar_panel_");
        Assert.NotNull(turned);
        Assert.Equal(270f, (turned.Value + 360f) % 360f, 1);

        // the sun moves a minute later: the tracker follows it and the computer turns the panels again; the client sees the new azimuth
        In(w, () =>
        {
            SSsun.azimuth_mod = 1;
            SSsun.next_fire = Dm.world.time;
        });
        w.Run(5 * Second);
        float moved = In(w, () => SSsun.azimuth);
        Assert.Equal(276f, moved);
        w.Run(4 * Second);
        Assert.Equal(276f, In(w, () => control.azimuth_target));
        Assert.Equal(276f, In(w, () => nearest.azimuth_current));
        Assert.Equal(276f, (TurnOf(replica, nearest, "solar_panel_").Value + 360f) % 360f, 1);
        Assert.Equal(276f, replica.SunAzimuth, 2); // protocol 47: the sky's sun stands where the panels look

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
