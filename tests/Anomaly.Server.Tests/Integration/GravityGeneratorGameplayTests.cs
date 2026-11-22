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
/// Engineering package E4 (gravitygenerator.dm:281-310, GravityGenerator.jsx) on MetaStation through the real server and a client's own
/// stack: an engineer stands by the station's gravity generator and opens its window with an empty hand (tg's GravityGenerator
/// interface, translated). The breaker switches the generator off: it discharges two points every SSmachines cycle (two seconds), the
/// charging pulse sounds, the middle part's overlays follow the charge (activated, activating, idle, startup, none), and when the
/// charge is gone the level loses its gravity with tg's alert sound and the generator's picture goes dark (off_8). The breaker back on
/// charges it again and the gravity returns.
/// </summary>
public class GravityGeneratorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
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
    public void An_engineer_flips_the_gravity_generators_breaker_in_its_window_and_the_level_loses_and_regains_gravity_on_tgs_timing()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 40000 },
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
        // the generator's graviton field (six times the gravity within two tiles) would crush an engineer who stays beside it for the
        // two minutes of this test, as tg's paper warns; the dev godmode keeps him at the window (the same as EnergyBallGameplayTests)
        using (w.World.Enter())
            ADD_TRAIT(me, TRAIT_GODMODE, ADMIN_TRAIT);
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        obj_machinery_gravity_generator_main gen = In(w, () => Assert.Single(Dm.each_cast<obj_machinery_gravity_generator_main>((DmList)GLOB.gravity_generators["1"])));
        obj_machinery_gravity_generator_part center = In(w, () => gen.center_part);
        float Gravity() => In(w, () => Dm.num(SSmapping.gravity_by_z_level[1]));
        Assert.Equal(1f, Gravity());

        // an engineer beside the generator opens its window with an empty hand
        turf stand = In(w, () => Beside(gen));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        Assert.Equal((0f, 1f), In(w, () => (Dm.num(gen.charging_state), Dm.num(gen.on)))); // idle, on
        string litIcon = IconOf(replica, gen);
        Assert.Equal("on_8", litIcon);
        Assert.Contains("activated", OverlaysOf(replica, center)); // the full charge's overlay (gravitygenerator.dm:166)
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, gen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("GravityGenerator", window.Interface);
        Assert.Equal((1, 100, 0, 1, 1), ((int)window.Merged.Num("breaker"), (int)window.Merged.Num("charge_count"), (int)window.Merged.Num("charging_state"), (int)window.Merged.Num("on"), (int)window.Merged.Num("operational")));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((400, 155), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal(("On", "Fully Charged"), (ByKey(content, "power").Text, ByKey(content, "mode").Text));

        // the breaker off: the generator discharges two points a cycle, the gravity stays until the charge is gone
        ByKey(content, "power").OnClick();
        w.Run(Second / 2);
        Assert.Equal(0f, In(w, () => gen.breaker));
        Assert.Equal(2f, In(w, () => Dm.num(gen.charging_state)));
        w.Run(5 * Second);
        float charge = In(w, () => gen.charge_count);
        output.WriteLine($"after about 5.5 s of discharge: {charge}");
        Assert.InRange(charge, 90f, 98f);
        Assert.Equal(charge, replica.UiWindows[window.Window].Merged.Num("charge_count"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("Off", "Discharging"), (ByKey(content, "power").Text, ByKey(content, "mode").Text));
        Assert.Equal(1f, Gravity());

        // the charge runs down through tg's five overlays (activated 81-100, activating 61-80, idle 41-60, startup 21-40, none)
        var seen = new List<string>();
        for (int i = 0; i < 70 && In(w, () => gen.charge_count) > 0; i++)
        {
            w.Run(2 * Second);
            foreach (string o in OverlaysOf(replica, center).Where(o => o is "activated" or "activating" or "idle" or "startup"))
            {
                if (!seen.Contains(o))
                    seen.Add(o);
            }
        }
        output.WriteLine($"overlays seen while discharging: {string.Join(", ", seen)}");
        Assert.Equal(new[] { "activated", "activating", "idle", "startup" }, seen.OrderBy(o => new[] { "activated", "activating", "idle", "startup" }.ToList().IndexOf(o)));
        Assert.Equal(0f, In(w, () => gen.charge_count));
        Assert.Contains(heard, h => h.File == "sound/effects/empulse.ogg"); // the pulse that lets everyone know it is discharging (:425)

        // the next cycle shuts it down: no gravity on the level, tg's alert sound, the picture goes dark
        heard.Clear();
        w.Run(4 * Second);
        Assert.Equal(0f, Gravity());
        Assert.Equal(0f, In(w, () => gen.on));
        Assert.Contains(heard, h => h.File == "sound/effects/alert.ogg");
        Assert.Equal(0f, In(w, () => Dm.num(me.gravity_state))); // the field went with the generator (refresh_gravity in shake_everyone)
        w.Run(2 * Second);
        Assert.Equal("off_8", IconOf(replica, gen));
        Assert.DoesNotContain(OverlaysOf(replica, center), o => o is "activated" or "activating" or "idle" or "startup");
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("Off", "Not Charging"), (ByKey(content, "power").Text, ByKey(content, "mode").Text));

        // the breaker on: it charges again, and the gravity returns when it is full
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => gen.breaker));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("On", "Charging"), (ByKey(content, "power").Text, ByKey(content, "mode").Text));
        Assert.Equal(0f, Gravity());
        heard.Clear();
        for (int i = 0; i < 70 && Gravity() == 0f; i++)
            w.Run(2 * Second);
        Assert.Equal(1f, Gravity());
        Assert.Equal(100f, In(w, () => gen.charge_count));
        Assert.Contains(heard, h => h.File == "sound/effects/alert.ogg");
        w.Run(4 * Second);
        Assert.Equal(6f, In(w, () => Dm.num(me.gravity_state))); // back in the field's very high gravity beside the generator
        Assert.Equal("on_8", IconOf(replica, gen));
        Assert.Contains("activated", OverlaysOf(replica, center));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(("On", "Fully Charged"), (ByKey(content, "power").Text, ByKey(content, "mode").Text));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
