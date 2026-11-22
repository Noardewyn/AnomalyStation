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

/// <summary>tg's ordinary shuttle console flies the mining shuttle from its window; an escape pod's console and safe open only on red alert.</summary>
public class ShuttleConsoleGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static TguiWindow OpenWindow(RealHeadlessWorld w, RealHeadlessWorld.Player player, atom console)
    {
        Assert.True(player.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => player.Session.UiAct(window, action, parameters), SendClose = window => player.Session.UiClose(window) };
        windows.Sync(player.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.First(x => x.Interface == "ShuttleConsole");
    }

    private static void StandBy(mob_living_carbon_human me, atom thing)
    {
        turf_open front = Dm.each<turf>(orange(1, thing)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
        Waiting.NoSleep(me.forceMove(front));
    }

    [MetaServerFact]
    public void The_mining_console_flies_the_shuttle_to_its_dock_from_the_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        obj_docking_port_mobile shuttle = null;
        var console = In(w, () =>
        {
            shuttle = (obj_docking_port_mobile)SSshuttle.getShuttle("mining");
            var c = Dm.each<obj_machinery_computer_shuttle_mining>(Dm.world).First(m => get_area(m) is area_shuttle_mining && Dm.eq(m.shuttleId, "mining"));
            StandBy(me, c);
            return c;
        });
        w.Run(10);

        TguiWindow window = OpenWindow(w, alice, console);
        Assert.Contains("lavaland mine", window.Content.AllText());
        window.Context.Act("move", ("shuttle_id", "mining_away"));
        for (int i = 0; i < 2000 && !In(w, () => shuttle.get_docked() is obj_docking_port_stationary d && Dm.eq(d.shuttle_id, "mining_away")); i++)
            w.Run(1);
        Assert.Equal("mining_away", In(w, () => Dm.str(((obj_docking_port_stationary)shuttle.get_docked()).shuttle_id)));
        Assert.True(In(w, () => get_area(console) is area_shuttle_mining), "the console flew with the shuttle");
    }

    [MetaServerFact]
    public void A_pod_stays_locked_until_red_alert_then_launches_and_its_safe_opens()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 42, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_storage_pod safe = null;
        var console = In(w, () =>
        {
            var c = Dm.each<obj_machinery_computer_shuttle_pod>(Dm.world).First();
            safe = Dm.each<obj_item_storage_pod>(Dm.world).First(s => get_area(s) == get_area(c));
            StandBy(me, c);
            return c;
        });
        w.Run(10);
        Assert.True(In(w, () => Dm.truthy(console.locked) && Dm.truthy(safe.atom_storage.locked) && Dm.str(safe.icon_state) == "wall_safe_locked"));
        Assert.Equal(14f, In(w, () => (float)safe.contents.len));

        TguiWindow window = OpenWindow(w, alice, console);
        Assert.Contains("Locked", window.Content.AllText());

        using (w.World.Enter())
            SSsecurity_level.set_level(SEC_LEVEL_RED);
        w.Run(10);
        Assert.True(In(w, () => !Dm.truthy(console.locked) && !Dm.truthy(safe.atom_storage.locked) && Dm.str(safe.icon_state) == "wall_safe"));

        var pod = In(w, () => (obj_docking_port_mobile)SSshuttle.getShuttle(console.shuttleId));
        object dock = In(w, () => Dm.text("[]_away", console.shuttleId));
        window = OpenWindow(w, alice, console);
        window.Context.Act("move", ("shuttle_id", dock));
        for (int i = 0; i < 2000 && !In(w, () => pod.get_docked() is obj_docking_port_stationary d && Dm.eq(d.shuttle_id, dock)); i++)
            w.Run(1);
        Assert.Equal(dock, In(w, () => (object)Dm.str(((obj_docking_port_stationary)pod.get_docked()).shuttle_id)));
        Assert.True(In(w, () => Dm.eq(pod.launch_status, EARLY_LAUNCHED)));
    }
}
