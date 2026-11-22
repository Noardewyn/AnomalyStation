using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>tg's emergency shuttle console: command IDs authorize the early launch from its window on the docked shuttle.</summary>
public class EmergencyConsoleGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void An_authorization_in_the_window_starts_the_engines()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 3000;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 12000;
        }
        alice.Session.DevCommand("shuttle call");
        for (int i = 0; i < 800 && !In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED)); i++)
            w.Run(1);
        var console = In(w, () =>
        {
            var c = Dm.each<obj_machinery_computer_emergency_shuttle>(Dm.world).First(m => get_area(m) is area_shuttle_escape);
            c.auth_need = 1;
            turf_open front = Dm.each<turf>(orange(1, c)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
            Waiting.NoSleep(me.forceMove(front));
            var id = Dm.@new<obj_item_card_id_advanced_debug>(front);
            Waiting.NoSleep(me.put_in_inactive_hand(id)); // a click with the card itself only tells to wear it
            return c;
        });
        w.Run(10);

        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "EmergencyShuttleConsole");
        Assert.Contains("No Active Authorizations", window.Content.AllText());
        window.Context.Act("authorize");
        w.Run(10);
        Assert.Equal(1f, In(w, () => (float)console.authorized.len));
        Assert.True(In(w, () => Dm.num(SSshuttle.emergency.timer) - Dm.world.time) <= ENGINE_START_TIME, "the launch is brought forward");
    }
}
