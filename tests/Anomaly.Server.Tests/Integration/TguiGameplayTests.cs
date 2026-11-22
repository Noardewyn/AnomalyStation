using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 3 through the real server on MetaStation (protocol version 16, D148, DV44): tg's Canister window. A click on the air canister
/// in the atmospherics gas storage (156,125) opens tg's tgui window on the client (a UiWindow message: the "Canister" interface with tg's
/// ui_data and ui_static_data); the client's translated interface shows it (the valve button "Closed", the regulator at one atmosphere)
/// and its buttons send tg's acts: the release pressure set to 1000 kPa through the window and the valve opened, the canister releases
/// into the room (the turf's pressure rises, the window's tank pressure falls as SStgui updates it); the valve closed again. Walking
/// away closes the window (tg's physical_state: further than 5 tiles is UI_CLOSE); the close button closes a reopened one.
/// </summary>
public class TguiGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public TguiGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y, int z) where T : class
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().FirstOrDefault());

    private static float TurfPressure(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.num(((turf_open)Builtins.locate(x, y, z)).air.return_pressure()));

    // What the client's translated interface draws from the replica's window (TguiWindows renders it as the Godot host does).
    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [MetaServerFact]
    public void A_click_opens_the_canisters_window_whose_buttons_set_the_pressure_and_open_the_valve()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Atmostech");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        var canister = Find<obj_machinery_portable_atmospherics_canister_air>(w, 156, 125, z);
        Assert.NotNull(canister);
        Put(w, me, 156, 126); // north of it, in the room

        // A click on the canister: tg's interact -> ui_interact -> a "Canister" window on the client.
        Assert.True(p.Session.Click(canister.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        _output.WriteLine($"window {window.Window}: {window.Interface} '{window.Title}' status {window.Status}: {window.Merged.ToJson()}");
        Assert.Equal("Canister", window.Interface);
        Assert.Equal("Air canister", window.Title);
        Assert.Equal(2, window.Status);
        Assert.Equal(0, window.Merged.Num("valveOpen"));
        Assert.Equal(101, window.Merged.Num("releasePressure"));
        Assert.Equal(2533, window.Merged.Num("maxReleasePressure"));
        double tankStart = window.Merged.Num("tankPressure");
        Assert.InRange(tankStart, 4000, 5000);

        // The translated interface: the valve button reads "Closed", the gauge shows the tank, the regulator one atmosphere.
        UiNode content = Shown(windows, replica, window.Window);
        UiNode valveButton = content.Walk().Single(n => n.Key == "valve");
        Assert.Equal("Closed", valveButton.Text);
        Assert.Equal(tankStart, content.Walk().Single(n => n.Key == "tankPressure").Value);
        Assert.Equal(101, content.Walk().Single(n => n.Key == "releasePressure").Value);
        Assert.Equal(350, windows.Get(window.Window).Context.WindowWidth);

        // The regulator set to 1000 kPa through the knob (tg's act("pressure", {pressure})), then the valve button.
        content.Walk().Single(n => n.Key == "releasePressure").OnChange(1000);
        w.Run(Second);
        Assert.Equal(1000f, In(w, () => canister.release_pressure));
        Assert.Equal(1000, replica.UiWindows[window.Window].Merged.Num("releasePressure"));

        float roomBefore = TurfPressure(w, 156, 125, z);
        Shown(windows, replica, window.Window).Walk().Single(n => n.Key == "valve").OnClick();
        w.Run(5 * Second);
        Assert.True(In(w, () => Dm.truthy(canister.valve_open)), "the window's valve button opened the valve");
        UiValue data = replica.UiWindows[window.Window].Merged;
        Assert.Equal(1, data.Num("valveOpen"));
        Assert.Equal("Open", Shown(windows, replica, window.Window).Walk().Single(n => n.Key == "valve").Text);
        float roomAfter = TurfPressure(w, 156, 125, z);
        _output.WriteLine($"the room at the canister: {roomBefore:0.00} -> {roomAfter:0.00} kPa; the window's tank {tankStart} -> {data.Num("tankPressure")} kPa");
        Assert.True(roomAfter > roomBefore + 1, $"the gas rises: {roomBefore} -> {roomAfter}");
        Assert.True(data.Num("tankPressure") < tankStart, "SStgui sends the falling tank pressure");

        // The released gas pushes the player off (tg's pressure difference movement): they step back beside the canister and press
        // the valve button as soon as the window takes input again, as a player fighting the wind would.
        _output.WriteLine($"the player after the release: {In(w, () => me.x)},{In(w, () => me.y)}");
        for (int attempt = 0; attempt < 10 && In(w, () => Dm.truthy(canister.valve_open)); attempt++)
        {
            using (w.World.Enter())
                Waiting.NoSleep(me.forceMove(Builtins.locate(156, 126, z)));
            for (int t = 0; t < 2 * Second && replica.UiWindows[window.Window].Status != 2; t++)
                w.Run(1);
            Shown(windows, replica, window.Window).Walk().Single(n => n.Key == "valve").OnClick();
            w.Run(4);
        }
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(canister.valve_open)), "closed again from the window");
        Assert.Equal(0, replica.UiWindows[window.Window].Merged.Num("valveOpen"));

        // Walking away: two tiles leave it updating only, three disable it, further than five closes it (tg's physical_state).
        // the released gas spreads out first: until then its pressure difference pushes the player off the tiles below
        w.Run(10 * Second);
        Put(w, me, 156, 127);
        w.Run(Second);
        Assert.Equal((156f, 127f), In(w, () => (Dm.num(me.x), Dm.num(me.y))));
        Assert.Equal(1, replica.UiWindows[window.Window].Status);
        Put(w, me, 156, 128);
        w.Run(Second);
        Assert.Equal(0, replica.UiWindows[window.Window].Status);
        Put(w, me, 156, 133);
        w.Run(2 * Second);
        Assert.Empty(replica.UiWindows.Tgui());

        // Reopened, and closed with the window's close button (tg's "close" message).
        Put(w, me, 156, 126);
        Assert.True(p.Session.Click(canister.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState again = Assert.Single(replica.UiWindows.Tgui());
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        windows.Close(windows.Get(again.Window));
        w.Run(Second);
        Assert.Empty(replica.UiWindows.Tgui());
        Assert.Equal(0, In(w, () => Dm.length(GlobalProcs.SStgui.all_uis)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
