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
/// Silicons SI1 (D326) through the real server on MetaStation with a headless client: the AI upload's core module rack holds the
/// round default's Asimov module welded into its core slot; Alice writes a freeform law on a module (tg's text prompt), opens the
/// rack's window by using the module on it (the upload's turrets switched off), slots it into the first law slot from the window and the rack's combined lawset gains
/// her law; with a screwdriver in hand the window's screw turns it in, and the module can no longer be pulled out.
/// </summary>
public class LawRackGameplayTests(ITestOutputHelper output)
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

    private static UiNode Shown(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, string ui)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        UiWindowState state = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        return windows.Get(state.Window).Content;
    }

    [MetaServerFact]
    public void Alice_slots_a_freeform_law_into_the_core_rack_and_screws_it_in()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var chat = new List<ServerText>();
        alice.Client.ServerTextReceived += t => chat.Add(t);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // Meta's AI upload has one core rack with the round default (Asimov) welded into its core slot.
        var rack = In(w, () => Dm.each<obj_machinery_ai_law_rack_base_core>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base_core))).Single());
        output.WriteLine("rack " + In(w, () => Dm.str(rack.name)) + " at " + In(w, () => $"{rack.x},{rack.y},{rack.z}"));
        Assert.IsType<obj_item_ai_module_law_core_full_asimov>(In(w, () => rack.ai_modules[1]));
        Assert.Equal(2f /* MODULE_WELDED */, In(w, () => Dm.num(rack.ai_modules[rack.ai_modules[1]])));
        Assert.Equal(3, In(w, () => rack.combined_lawset.inherent.len));
        Assert.True(In(w, () => Dm.truthy(rack.anchored) && Dm.truthy(rack.secured)));

        // The upload's turrets stun anyone without access (tg): switched off for the test.
        Do(w, () =>
        {
            foreach (obj_machinery_porta_turret turret in Dm.each<obj_machinery_porta_turret>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_porta_turret))))
            {
                if (get_area(turret) == get_area(rack))
                    turret.toggle_on(FALSE);
            }
        });

        // Alice stands by the rack with a freeform module and writes her law on it through tg's text prompt.
        var freeform = In(w, () =>
        {
            turf spot = Dm.each<turf>(Dm.list(get_step(rack, SOUTH), get_step(rack, NORTH), get_step(rack, EAST), get_step(rack, WEST)))
                .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            Waiting.NoSleep(aliceMob.forceMove(spot));
            var module = Dm.@new<obj_item_ai_module_law_supplied_freeform>(spot);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(module))));
            return module;
        });
        Do(w, () => Waiting.Detach(freeform.attack_self(aliceMob)));
        w.Run(2 * Second);
        UiWindowState prompt = alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "TextInputModal");
        Assert.True(alice.Session.UiAct(prompt.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of("Alice is the only crewmember with all access.")))));
        w.Run(2 * Second);
        Assert.Equal("Alice is the only crewmember with all access.", In(w, () => Dm.str(freeform.laws[1])));

        // Using the module on the rack opens its window; the first law slot (the window's second) takes it.
        Do(w, () => Waiting.NoSleep(rack.item_interaction(aliceMob, freeform)));
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        UiNode ui = Shown(w, windows, alice, "LawRack");
        Assert.Equal("Welded", ui.Walk().Single(n => n.Key == "weld0").Text);
        ui.Walk().Single(n => n.Key == "module1").OnClick();
        w.Run(2 * Second);
        Assert.True(In(w, () => rack.ai_modules[2]) == freeform, "chat: " + string.Join(" | ", chat.Select(t => t.Html)) + "\nruntimes: " + string.Join("\n----\n", runtimes));
        Assert.Contains("Alice is the only crewmember with all access.", In(w, () => Dm.each(rack.combined_lawset.supplied).Select(Dm.str).ToList()));

        // A screwdriver turns the screws in; a screwed module stays put.
        Do(w, () =>
        {
            var screwdriver = Dm.@new<obj_item_screwdriver>(aliceMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(screwdriver))));
        });
        ui = Shown(w, windows, alice, "LawRack");
        Assert.False(ui.Walk().Single(n => n.Key == "screwL1").Disabled);
        ui.Walk().Single(n => n.Key == "screwL1").OnClick();
        w.Run(5 * Second);
        Assert.Equal(1f /* MODULE_SCREWED */, In(w, () => Dm.num(rack.ai_modules[freeform])));
        Do(w, () => Waiting.NoSleep(aliceMob.dropItemToGround(aliceMob.get_active_held_item() as obj_item)));
        ui = Shown(w, windows, alice, "LawRack");
        Assert.True(ui.Walk().Single(n => n.Key == "module1").Disabled);
        Assert.Contains(GLOB_lawchanges(w), l => l.Contains("added law to", StringComparison.Ordinal));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }

    private static List<string> GLOB_lawchanges(RealHeadlessWorld w) => In(w, () => Dm.each(GLOB.lawchanges).Select(Dm.str).ToList());
}
