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
/// A circuit shell through the real server on MetaStation with a headless client: Bob finishes a bot shell assembly with a
/// screwdriver by clicking it, puts a circuit with a speech component into the bot with a click, sets its access checker's required
/// access in the CircuitAccessChecker window, and pushes the bot with an empty hand: the bot's button fires the speech component and
/// Bob's chat shows the bot's line.
/// </summary>
public class CircuitShellGameplayTests(ITestOutputHelper output)
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

    private static UiNode ByKey(UiNode tree, string key)
    {
        UiNode found = tree.Walk().First(n => n.Key == key);
        while (found.Walk().Skip(1).FirstOrDefault(n => n.Key == key) is UiNode inner)
            found = inner;
        return found;
    }

    private static void ClickOn(RealHeadlessWorld.Player p, atom target) =>
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    private static turf FreeTileBy(atom a) => Dm.each<turf>(Dm.list(get_step(a, SOUTH), get_step(a, NORTH), get_step(a, EAST), get_step(a, WEST)))
        .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(c => Dm.truthy(c.density)));

    private static void Empty(mob_living_carbon_human who)
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items))
            qdel(held);
    }

    [MetaServerFact]
    public void Bob_finishes_a_bot_shell_loads_a_circuit_and_the_bot_speaks_when_pushed()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10750, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new List<string>();
        bob.Client.ServerTextReceived += t => chat.Add(t.Html);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => bob.Session.UiAct(window, action, parameters),
            SendClose = window => bob.Session.UiClose(window),
        };

        // A bot shell assembly beside Bob; a click with a screwdriver finishes it (tg's three seconds).
        turf spot = In(w, () => FreeTileBy(bobMob));
        var assembly = In(w, () => Dm.@new<obj_item_shell_bot>(spot));
        Do(w, () =>
        {
            Empty(bobMob);
            Waiting.NoSleep(bobMob.put_in_hands(Dm.@new<obj_item_screwdriver>(bobMob.loc)));
        });
        w.Run(Second);
        ClickOn(bob, assembly);
        w.Run(5 * Second);
        Assert.True(In(w, () => QDELETED(assembly)), "the assembly was not finished; runtimes: " + string.Join("\n", runtimes));
        var bot = In(w, () => Dm.each<atom>(spot.contents).OfType<obj_structure_bot>().Single());
        Assert.Contains(chat, t => t.Contains("You finish", StringComparison.Ordinal));

        // A circuit with a cell, a speech component and an access checker; a click with it puts it into the bot.
        var circuit = In(w, () => Dm.@new<obj_item_integrated_circuit>(bobMob.loc));
        var speech = In(w, () => Dm.@new<obj_item_circuit_component_speech>(bobMob.loc));
        var checker = In(w, () => Dm.@new<obj_item_circuit_component_compare_access>(bobMob.loc));
        Do(w, () =>
        {
            circuit.set_cell(Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit));
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(speech))));
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(checker))));
            speech.message.set_input("Beep boop, crew.");
            Empty(bobMob);
            Waiting.NoSleep(bobMob.put_in_hands(circuit));
        });
        w.Run(Second);
        ClickOn(bob, bot);
        w.Run(Second);
        Assert.Same(bot, In(w, () => circuit.shell));
        var button = In(w, () => Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_bot>().Single());
        Do(w, () => speech.trigger_input.connect(button.signal)); // the bot's Signal wired to the speech's Trigger

        // The access checker's window: one of the accesses is enough.
        Do(w, () => Waiting.Detach(checker.ui_perform_action(bobMob, "access")));
        UiNode ui = Shown(w, windows, bob, "CircuitAccessChecker");
        Assert.Equal("All", ByKey(ui, "one_access").Text);
        ByKey(ui, "one_access").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => Dm.num(checker.check_any.value)));
        ui = Shown(w, windows, bob, "CircuitAccessChecker");
        Assert.Equal("One", ByKey(ui, "one_access").Text);

        // An empty hand pushes the bot's button: the bot says its line.
        Do(w, () => Empty(bobMob));
        w.Run(Second);
        ClickOn(bob, bot);
        w.Run(Second);
        Assert.Contains(chat, t => t.Contains("Beep boop, crew.", StringComparison.Ordinal));
        output.WriteLine(chat.Last(t => t.Contains("Beep boop", StringComparison.Ordinal)));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
