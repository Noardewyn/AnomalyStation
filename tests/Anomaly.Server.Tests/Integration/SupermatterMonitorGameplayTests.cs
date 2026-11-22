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
/// The engineering package E2 (D287) on MetaStation through the real server and a client's own stack: an engineer clicks one of Meta's
/// engineering consoles (modular_computer/preset/engineering) with an empty hand, which boots NtOS (the console's screen goes from tg's
/// standby to the menu), starts NT CIMS from NtosMain and sees Meta's crystal in the NtosSupermatter window; its Details show the
/// crystal's live numbers, which change as the energised crystal's energy decays, and the console's screen shows tg's smmon picture of
/// the crystal's status. Meta's five incident displays count 0 shifts since the last delamination (tg's shame screen and digits, read by
/// the client), and a new count shows its digits. A ghost (the engineer, ghostized) clicks the crystal and gets tg's Supermatter window.
/// No new runtime.
/// </summary>
public class SupermatterMonitorGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static AppearanceEntry Look(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
    }

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        AppearanceEntry look = Look(replica, a);
        return look == null ? null : replica.ResolveString(look.IconState);
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        AppearanceEntry look = Look(replica, a);
        if (look == null)
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    [MetaServerFact]
    public void An_engineer_reads_the_crystal_on_an_engineering_console_the_incident_displays_count_and_a_ghost_opens_the_crystals_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 287, TimeoutSteps = 20000 },
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
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => GLOB.main_supermatter_engine);
        Assert.NotNull(sm);

        // Meta's two engineering consoles; the engineer takes the one nearest the crystal
        List<obj_machinery_modular_computer_preset_engineering> consoles = In(w, () =>
            Dm.each_cast<obj_machinery_modular_computer_preset_engineering>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_engineering)))
                .OrderBy(c => Dm.num(get_dist(c, sm))).ToList());
        Assert.Equal(2, consoles.Count);
        obj_machinery_modular_computer_preset_engineering console = consoles[0];
        output.WriteLine($"the consoles at {string.Join(" and ", consoles.Select(c => In(w, () => $"{c.x},{c.y} ({Dm.str(((area)get_area(c)).name)})")))}; the crystal at {In(w, () => $"{sm.x},{sm.y}")}");
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(console))));
        w.Run(Second);
        Assert.Equal("console-off", IconOf(replica, console));
        Assert.Contains("standby", OverlaysOf(replica, console));

        // an empty hand boots NtOS: its main menu, the screen lit
        Assert.True(p.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("NtosMain", window.Interface);
        Assert.Equal("console", IconOf(replica, console));
        Assert.Contains("menu", OverlaysOf(replica, console));
        UiNode content = Shown(windows, replica, window.Window);
        UiNode cims = ByKey(content, "program:ntcims");
        Assert.Contains("NT CIMS", cims.AllText());

        // NT CIMS: the crystal in its list
        ByKey(cims, "run").OnClick();
        w.Run(Second);
        window = replica.UiWindows[window.Window];
        Assert.Equal("NtosSupermatter", window.Interface);
        UiValue listed = Assert.Single(window.Merged.Arr("sm_data"));
        Assert.Equal(In(w, () => sm.uid), listed.Num("uid"));
        Assert.Equal(100, listed.Num("integrity"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Detected Supermatters", ByKey(content, "detected").Title);
        Assert.Contains("smmon_0", OverlaysOf(replica, console));

        // the crystal energised (as the emitters' beams do): the Details show its energy, which decays live
        In(w, () => { sm.activation_logged = TRUE; sm.internal_energy = 500; }); // energised by hand: no logged source (supermatter.dm:747)
        w.Run(Second);
        ByKey(content, "details").OnClick();
        content = Shown(windows, replica, window.Window);
        UiValue first = replica.UiWindows[window.Window].Merged.Arr("sm_data")[0];
        double energy1 = first.Num("internal_energy");
        output.WriteLine($"after a second: {energy1} MeV ({ByKey(content, "internal_energy").Text}), zaps {ByKey(content, "zap_transmission").Text}");
        Assert.InRange(energy1, 1, 500);
        Assert.Equal(TguiFormat.ToFixed(first.Num("internal_energy_coefficient"), 3) + first.Str("internal_energy_unit"), ByKey(content, "internal_energy").Text);
        Assert.Equal("1. " + first.Str("area_name"), content.Walk().First(n => n.Kind == UiKind.Section && n.Key == "sm").Title);
        Assert.Equal(1f, In(w, () => sm.uid)); // Meta's one crystal
        w.Run(3 * Second);
        content = Shown(windows, replica, window.Window);
        double energy2 = replica.UiWindows[window.Window].Merged.Arr("sm_data")[0].Num("internal_energy");
        output.WriteLine($"three seconds later: {energy2} MeV ({ByKey(content, "internal_energy").Text})");
        Assert.True(energy2 < energy1, $"the crystal's energy decays live: {energy1} -> {energy2}");
        // the console's screen: tg's smmon picture of the crystal's status (normal: energised)
        Assert.Equal(SUPERMATTER_NORMAL, (int)In(w, () => Dm.num(sm.get_status())));
        Assert.Contains("smmon_1", OverlaysOf(replica, console));

        // Meta's incident displays: 0 shifts since the last delamination (SSpersistence's counters without saved files)
        List<obj_machinery_incident_display> displays = In(w, () =>
            Dm.each_cast<obj_machinery_incident_display>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_incident_display))).OrderBy(d => d.dm_id).ToList());
        Assert.Equal(5, displays.Count);
        Assert.All(displays, d => Assert.Equal((0f, "display_shame"), In(w, () => (d.last_delam, d.icon_state))));
        obj_machinery_incident_display display = displays.First(d => d is obj_machinery_incident_display_delam);
        In(w, () => Waiting.NoSleep(me.forceMove(display.loc)));
        // past its SAFETY MOTH advert (a flick that hides the digits for 7 seconds every 30 or so)
        for (int i = 0; i < 20 && In(w, () => display.active_advert != 0); i++)
            w.Run(Second);
        w.Run(Second / 2);
        List<string> shown = OverlaysOf(replica, display);
        output.WriteLine($"the display at {In(w, () => $"{display.x},{display.y}")}: {IconOf(replica, display)} with {string.Join(" ", shown)}");
        Assert.Equal("display_shame", IconOf(replica, display));
        Assert.Equal(new[] { "overlay_delam", "num_0", "num_0", "rising" }, shown);
        In(w, () => display.update_delam_count(12, 12));
        w.Run(Second / 2);
        Assert.Equal("display_normal", IconOf(replica, display));
        Assert.Equal(new[] { "overlay_delam", "num_2", "num_1", "rising" }, OverlaysOf(replica, display));

        // a ghost clicks the crystal: tg's Supermatter window (observers only)
        var ghost = In(w, () => me.ghostize(TRUE) as mob_dead_observer);
        Assert.NotNull(ghost);
        In(w, () => Waiting.NoSleep(ghost.forceMove(get_step(sm, SOUTH))));
        w.Run(Second);
        foreach (UiWindowState open in replica.UiWindows.Tgui().ToList())
            p.Session.UiClose(open.Window);
        w.Run(Second);
        Assert.True(p.Session.Click(On(sm), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState smWindow = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Supermatter", smWindow.Interface);
        Assert.Equal(In(w, () => sm.uid), smWindow.Merged.Arr("sm_data")[0].Num("uid"));
        Assert.True(smWindow.Merged.Get("gas_metadata")?.Fields.Count > 0);
        content = Shown(windows, replica, smWindow.Window);
        Assert.Contains("Internal Energy", content.AllText());

        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(3).Select(e => e.Message)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
