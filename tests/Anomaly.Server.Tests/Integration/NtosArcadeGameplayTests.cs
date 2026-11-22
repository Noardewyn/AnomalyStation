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
/// Gaps S4c (D327) through the real server on MetaStation: Alice plays the Donksoft Micro Arcade on a civilian console: a new game, an
/// attack that hurts the boss with tg's hit sound, and the boss's picture in her window.
/// </summary>
public class NtosArcadeGameplayTests(ITestOutputHelper output)
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

    private static UiWindowState Open(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human who, obj_machinery m)
    {
        foreach (UiWindowState open in p.Session.Replica.UiWindows.Tgui().ToList())
            p.Session.UiClose(open.Window);
        w.Run(Second / 2);
        In(w, () => { Waiting.NoSleep(who.forceMove(Beside(m))); return 0; });
        w.Run(Second / 2);
        Assert.True(p.Session.Click(On(m), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(p.Session.Replica.UiWindows.Tgui());
        Assert.Equal("NtosMain", window.Interface);
        return window;
    }

    private static TguiWindows Windows(RealHeadlessWorld.Player p) => new TguiWindows
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static bool Working(obj_machinery m) => !Dm.truthy(Dm.bitand(m.machine_stat, Dm.bitor(NOPOWER, BROKEN))) && Beside(m) != null;

    [MetaServerFact]
    public void Alice_plays_a_turn_of_the_arcade_on_a_civilian_console()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 4310, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var sounds = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => sounds.AddRange(events);
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        var console = In(w, () => Dm.each_cast<obj_machinery_modular_computer_preset_civilian>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_civilian))).First(Working));
        TguiWindows windows = Windows(alice);
        UiWindowState window = Open(w, alice, me, console);
        ByKey(ByKey(Shown(windows, alice.Session.Replica, window.Window), "program:dsarcade"), "run").OnClick();
        w.Run(Second);
        Assert.Equal("NtosArcade", alice.Session.Replica.UiWindows[window.Window].Interface);
        ByKey(Shown(windows, alice.Session.Replica, window.Window), "attack").OnClick();
        w.Run(4 * Second);
        UiValue data = alice.Session.Replica.UiWindows[window.Window].Merged;
        Assert.True(data.Num("Hitpoints") < 45, "the attack did not hurt the boss");
        Assert.Contains(sounds, e => e.File == "sound/machines/arcade/hit.ogg");
        Assert.StartsWith("icons/ui/arcade/boss", ByKey(Shown(windows, alice.Session.Replica, window.Window), "bossimage").DmIcon);

        output.WriteLine(string.Join("\n", errors.Select(e => e.Message)));
        Assert.DoesNotContain(errors, e => e.StackTrace != null && e.StackTrace.Contains("program_arcade", StringComparison.Ordinal));
    }
}
