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
/// Silicons SI7 (D326) through the real server on MetaStation with a headless client: Meta's drone shell dispenser stands in its place;
/// Gina ghosts, clicks a drone shell and answers tg's prompt, and wakes as a maintenance drone with its laws, its built-in tools in its
/// internal storage slot and its HUD.
/// </summary>
public class DroneGameplayTests(ITestOutputHelper output)
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

    private static mob MobOfKey(RealHeadlessWorld w, string key) => In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == key).mob);

    [MetaServerFact]
    public void A_ghost_takes_a_drone_shell_and_wakes_as_a_drone()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 82, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player gina = w.Join("Gina");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);
        var texts = new List<string>();
        gina.Client.ServerTextReceived += t => texts.Add(t.Html);

        // tg's Meta has a drone shell dispenser
        Assert.True(In(w, () => Dm.each<obj_machinery_drone_dispenser>().Any()), "Meta's drone dispenser");

        Do(w, () => ((mob_living)w.MobOf(gina)).ghostize(TRUE));
        w.Run(2 * Second);
        var ghost = Assert.IsType<mob_dead_observer>(MobOfKey(w, "Gina"));
        obj_effect_mob_spawn_ghost_role_drone shell = null;
        Do(w, () => shell = Dm.@new<obj_effect_mob_spawn_ghost_role_drone>(get_step(ghost.loc, NORTH)));
        w.Run(Second);

        // a ghost's click on the shell (tg's attack_ghost) asks; Yes
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Entity, shell.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        UiWindowState prompt = gina.Session.Replica.UiWindows.Values.Single(v => v.Interface == "AlertModal");
        Assert.True(gina.Session.UiAct(prompt.Window, "choose", UiValue.ObjectOf(("choice", UiValue.Of("Yes")))));
        w.Run(3 * Second);

        var drone = Assert.IsType<mob_living_basic_drone>(MobOfKey(w, "Gina"));
        output.WriteLine("the drone " + In(w, () => drone.name));
        Assert.True(In(w, () => QDELETED(shell)), "the shell is used up; runtimes: " + Runtimes());
        Assert.IsType<obj_item_storage_drone_tools>(In(w, () => drone.internal_storage));
        Assert.Contains(texts, t => t.Contains("Drone Laws"));
        Assert.NotNull(In(w, () => drone.hud_used));
        // the drone's HUD (drones.dm): the internal storage slot with the built-in tools in it and the head slot
        w.Run(Second);
        var screen = gina.Session.Replica.Screen.Values.ToList();
        ScreenEntry storage = Assert.Single(screen, e => e.ScreenLoc == ui_drone_storage);
        long tools = In(w, () => drone.internal_storage.dm_id);
        Assert.True(storage.VisChildren?.Contains(tools) == true, "the built-in tools sit in the storage slot");
        Assert.Single(screen, e => e.ScreenLoc == ui_drone_head);
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
