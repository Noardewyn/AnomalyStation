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
/// M10 slice G (D272, D273) through the real server on MetaStation with headless clients: Alice, with an ID of command access, logs in
/// at the bridge's communications console (tg's CommunicationsConsole window), calls the emergency shuttle with a reason and recalls
/// it; the dev command <c>shuttle call</c> (tg's admin Call Shuttle) calls it again. The shuttle flies from its CentCom stand-in
/// dock (DV190) and docks at Meta's evac bay, Bob boards it, it leaves into a transit dock of the reserved level with him (his own
/// client follows him onto z 2) and docks at CentCom: SHUTTLE_ENDGAME. The test shortens tg's call, dock and escape times as an admin
/// would (10 s, 20 s, 5 s).
/// </summary>
public class ShuttleGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static TguiWindow Window(TguiWindows windows, ReplicaWorld replica, string @interface)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static string Mode(RealHeadlessWorld w) => In(w, () => Dm.str(SSshuttle.emergency.mode));

    private static void RunUntil(RealHeadlessWorld w, Func<bool> until, int maxTicks, string what)
    {
        for (int i = 0; i < maxTicks && !In(w, until); i++)
            w.Run(1);
        Assert.True(In(w, until), what + $" (mode {Mode(w)}, timer {In(w, () => SSshuttle.emergency.timer)})");
    }

    [MetaServerFact]
    public void Alice_calls_and_recalls_the_shuttle_at_the_bridge_console_and_the_called_shuttle_takes_Bob_to_centcom()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* Bob is followed from the bay into transit */, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);

        obj_machinery_computer_communications console = In(w, () => Dm.each<obj_machinery_computer_communications>(Dm.world)
            .First(c => get_area(c) is area_station_command_bridge));
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 400; // more than ENGINE_START_TIME (10 s), so it docks before it ignites
            SSshuttle.emergency_escape_time = 100;
            // tg's shuttle_refuel_delay: no call in the round's first 20 minutes (canEvac); the round started 20 minutes ago
            SSticker.round_start_time = Dm.num(Dm.world.time) - 20 * 60 * 10;
            turf front = get_step(console, console.dir) as turf;
            Assert.IsAssignableFrom<turf_open>(front);
            Waiting.NoSleep(aliceMob.forceMove(front));
            var id = Dm.@new<obj_item_card_id_advanced_debug>(front);
            Waiting.NoSleep(aliceMob.put_in_hands(id));
        }
        w.Run(Second);

        // the console's window: logged out, then tg's toggleAuthentication reads the held ID (command access)
        TguiWindows windows = WindowsOf(alice);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        TguiWindow comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
        Assert.NotNull(comms);
        Assert.False(comms.Context.Data.Truthy("authenticated"));
        Assert.Contains("Log In", comms.Content.AllText());
        comms.Context.Act("toggleAuthentication");
        w.Run(Second / 2);
        comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
        Assert.True(comms.Context.Data.Truthy("authenticated"));
        Assert.Equal("main", comms.Context.Data.Str("page"));
        Assert.Equal("green", comms.Context.Data.Str("alertLevel"));
        string shown = comms.Content.AllText();
        output.WriteLine(shown);
        Assert.Contains("Call Emergency Shuttle", shown);
        Assert.Contains("Set Status Display", shown);

        // the call: tg's reason length rule, then the shuttle is on its way and the console offers the recall
        comms.Context.Act("callShuttle", ("reason", "The station is on fire and the crew must leave."));
        w.Run(Second / 2);
        Assert.Equal(SHUTTLE_CALL, Mode(w));
        Assert.Contains(texts, t => t.Contains("emergency shuttle has been called", StringComparison.OrdinalIgnoreCase));
        comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
        Assert.True(comms.Context.Data.Truthy("shuttleCalled"));
        Assert.Contains("Recall Emergency Shuttle", comms.Content.AllText());
        comms.Context.Act("recallShuttle");
        w.Run(Second / 2);
        Assert.Equal(SHUTTLE_RECALL, Mode(w));
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE), 20 * Second, "the recalled shuttle is back at CentCom");

        // the dev command: tg's admin Call Shuttle
        texts.Clear();
        alice.Session.DevCommand("shuttle call");
        w.Run(Second / 2);
        Assert.Contains(texts, t => t.Contains("the emergency shuttle is called", StringComparison.Ordinal));
        Assert.Equal(SHUTTLE_CALL, Mode(w));
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED), 30 * Second, "the shuttle docks at the evac bay");

        // Bob boards: a chair of the docked shuttle on the station level
        using (w.World.Enter())
        {
            var seat = Dm.each<obj_structure_chair>(Dm.world).First(c => get_area(c) is area_shuttle_escape && c.z == 1);
            Waiting.NoSleep(bobMob.forceMove(seat.loc));
        }
        w.Run(Second);
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE), 40 * Second, "the shuttle leaves into transit");
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(is_reserved_level(bobMob.z))), "Bob rides along onto the reserved level");
        (float _, float _, int bz) = bob.Session.Prediction.RenderPosition();
        Assert.Equal(In(w, () => (int)bobMob.z), bz); // his client follows him there

        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME), 20 * Second, "the shuttle docks at CentCom");
        Assert.True(In(w, () => Dm.truthy(SSticker.check_finished())), "the round's state is the endgame");
        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
