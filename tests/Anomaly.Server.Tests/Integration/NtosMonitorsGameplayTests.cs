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
/// Gaps S4b (D327) through the real server on MetaStation: Alice runs AmpCheck on an engineering console and sees its powernet with the
/// areas' APCs; then Alice and Bob, each at a civilian console, meet in an NTNRC channel Alice creates and talk.
/// </summary>
public class NtosMonitorsGameplayTests(ITestOutputHelper output)
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
    public void Ampcheck_shows_the_powernet_and_two_consoles_chat_over_ntnrc()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 4210, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        // AmpCheck on an engineering console
        var engineering = In(w, () => Dm.each_cast<obj_machinery_modular_computer_preset_engineering>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_engineering))).First(Working));
        TguiWindows aliceWindows = Windows(alice);
        UiWindowState window = Open(w, alice, aliceMob, engineering);
        ByKey(ByKey(Shown(aliceWindows, alice.Session.Replica, window.Window), "program:ampcheck"), "run").OnClick();
        w.Run(Second);
        Assert.Equal("NtosPowerMonitor", alice.Session.Replica.UiWindows[window.Window].Interface);
        UiValue power = alice.Session.Replica.UiWindows[window.Window].Merged;
        Assert.True(power.Truthy("attached"), "AmpCheck found no powernet");
        Assert.NotEmpty(power.Arr("areas"));
        // tg's loading screen until the program's process tick records the first sample (record(), powermonitor.dm:62)
        w.Run(6 * Second);
        power = alice.Session.Replica.UiWindows[window.Window].Merged;
        Assert.NotEmpty(power.Get("history").Arr("supply"));
        Assert.DoesNotContain(Shown(aliceWindows, alice.Session.Replica, window.Window).Walk(), n => n.Key == "loading");

        // two civilian consoles: Alice makes a channel, Bob joins it and says hello
        var civilians = In(w, () => Dm.each_cast<obj_machinery_modular_computer_preset_civilian>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_civilian))).Where(Working).Take(2).ToList());
        Assert.Equal(2, civilians.Count);
        window = Open(w, alice, aliceMob, civilians[0]);
        ByKey(ByKey(Shown(aliceWindows, alice.Session.Replica, window.Window), "program:ntnrc_client"), "run").OnClick();
        w.Run(Second);
        Assert.Equal("NtosNetChat", alice.Session.Replica.UiWindows[window.Window].Interface);
        ByKey(Shown(aliceWindows, alice.Session.Replica, window.Window), "newchannel").OnCommit("ops");
        w.Run(Second);

        TguiWindows bobWindows = Windows(bob);
        UiWindowState bobWindow = Open(w, bob, bobMob, civilians[1]);
        ByKey(ByKey(Shown(bobWindows, bob.Session.Replica, bobWindow.Window), "program:ntnrc_client"), "run").OnClick();
        w.Run(Second);
        UiNode chat = Shown(bobWindows, bob.Session.Replica, bobWindow.Window);
        ByKey(chat, "chan:ops").OnClick();
        w.Run(Second);
        chat = Shown(bobWindows, bob.Session.Replica, bobWindow.Window);
        ByKey(chat, "say").OnInput("hello ops");
        Shown(bobWindows, bob.Session.Replica, bobWindow.Window).Walk().First(n => n.Key == "say").OnEnter();
        w.Run(2 * Second);

        UiNode aliceChat = Shown(aliceWindows, alice.Session.Replica, window.Window);
        Assert.Contains(aliceChat.Walk(), n => n.Text != null && n.Text.EndsWith(": hello ops", StringComparison.Ordinal));
        Assert.Contains(aliceChat.Walk(), n => n.Key != null && n.Key.StartsWith("client:", StringComparison.Ordinal) && n.Key != "client:" + aliceChat.Walk().First(x => x.Key == "username").TextValue);

        output.WriteLine(string.Join("\n", errors.Select(e => e.Message)));
        Assert.DoesNotContain(errors, e => e.StackTrace != null && (e.StackTrace.Contains("chatclient", StringComparison.Ordinal) || e.StackTrace.Contains("power_monitor", StringComparison.Ordinal) || e.StackTrace.Contains("ntnet_conversation", StringComparison.Ordinal)));
    }
}
