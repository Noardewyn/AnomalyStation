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
/// Silicons SI6 (D326) through the real server on MetaStation with headless clients: Gina ghosts, clicks her HUD's settings button, signs
/// up as a pAI in the GhostMenu window and submits the PaiSubmit form; Bob's pAI card shows her in its PaiCard window and his Download
/// makes her the pAI in his card; she opens her software window from her HUD, buys the door jack and unfolds her holochassis.
/// </summary>
public class PaiGameplayTests(ITestOutputHelper output)
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

    // the player's mob, whatever it is (MobOf answers living mobs only)
    private static mob MobOfKey(RealHeadlessWorld w, string key) => In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == key).mob);

    private static TguiWindows Windows(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static UiNode Window(RealHeadlessWorld w, RealHeadlessWorld.Player p, TguiWindows windows, string ui)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        UiWindowState state = p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == ui);
        Assert.True(state != null, "the " + ui + " window is open");
        return windows.Get(state.Window).Content;
    }

    private static void ClickScreen(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom a)
    {
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, a.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
    }

    [MetaServerFact]
    public void A_ghost_signs_up_and_bob_downloads_her_into_his_card()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 80, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player gina = w.Join("Gina");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);

        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Do(w, () => ((mob_living)w.MobOf(gina)).ghostize(TRUE));
        w.Run(2 * Second);
        var ghost = Assert.IsType<mob_dead_observer>(MobOfKey(w, "Gina"));

        // the ghost HUD's settings button opens the ghost menu; Sign up as pAI opens the candidacy form
        atom settings = In(w, () => ghost.hud_used.screen_objects[HUD_GHOST_SETTINGS] as atom);
        Assert.NotNull(settings);
        ClickScreen(w, gina, settings);
        TguiWindows ginaWindows = Windows(gina);
        Window(w, gina, ginaWindows, "GhostMenu").Walk().Single(n => n.Key == "signup_pai").OnClick();
        UiNode form = Window(w, gina, ginaWindows, "PaiSubmit");
        form.Walk().Single(n => n.Key == "input:name").OnInput("Pip");
        form.Walk().Single(n => n.Key == "input:description").OnInput("Helpful");
        form.Walk().Single(n => n.Key == "submit").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy((SSpai.candidates[ghost.ckey] as datum_pai_candidate)?.ready)), "submitted; runtimes: " + Runtimes());

        // Bob's card lists her; his Download makes her his pAI
        obj_item_pai_card card = null;
        Do(w, () =>
        {
            card = Dm.@new<obj_item_pai_card>(bobMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobMob.put_in_hands(card))));
            Waiting.Detach(card.attack_self(bobMob));
        });
        TguiWindows bobWindows = Windows(bob);
        UiNode cardWindow = Window(w, bob, bobWindows, "PaiCard");
        Assert.Contains(cardWindow.Walk(), n => n.Text == "Pip");
        cardWindow.Walk().Single(n => n.Key == "dl").OnClick();
        w.Run(2 * Second);
        var pai = In(w, () => card.pai);
        Assert.True(pai != null, "downloaded; runtimes: " + Runtimes());
        Assert.Same(pai, MobOfKey(w, "Gina"));
        Assert.Equal("Pip", In(w, () => pai.name));
        output.WriteLine("the pAI " + In(w, () => pai.name) + " in " + In(w, () => card.name));

        // her HUD's software button opens her interface; she buys the door jack
        atom software = In(w, () => pai.hud_used.screen_objects[HUD_PAI_SOFTWARE] as atom);
        ClickScreen(w, gina, software);
        UiNode iface = Window(w, gina, ginaWindows, "PaiInterface");
        iface.Walk().Single(n => n.Key == "tab3").OnClick();
        Window(w, gina, ginaWindows, "PaiInterface").Walk().Single(n => n.Key == "sw:Door Jack").Walk().Single(n => n.Key == "buy").OnClick();
        w.Run(Second);
        Assert.Contains("Door Jack", In(w, () => Dm.each(pai.installed_software).Select(Dm.str).ToArray()));

        // the holochassis unfolds once the emitters are ready (HOLOCHASSIS_INIT_TIME after the download)
        w.Run((int)(HOLOCHASSIS_INIT_TIME / 10 * Second));
        atom shell = In(w, () => pai.hud_used.screen_objects[HUD_PAI_SHELL] as atom);
        ClickScreen(w, gina, shell);
        Assert.True(In(w, () => Dm.truthy(pai.holoform)), "unfolded; runtimes: " + Runtimes());
        Assert.True(In(w, () => pai.loc is turf));
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
