using Anomaly.Client.Shared;
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
/// The brain-computer interface and the USB cable through the real server on MetaStation with a headless client: Bob loads a BCI
/// into an implanter with a click, opens it, steps in and shuts it with clicks, and comes out with the BCI in his head and its action
/// buttons; then he plugs a USB cable from a bot shell's circuit into a pressure pump with two clicks, and pushing the bot, whose
/// button is wired to the pump's component, switches the pump on.
/// </summary>
public class BrainComputerInterfaceGameplayTests(ITestOutputHelper output)
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

    private static void ClickOn(RealHeadlessWorld.Player p, atom target) =>
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    private static List<turf> FreeTilesBy(atom a) => Dm.each<turf>(Dm.list(get_step(a, SOUTH), get_step(a, NORTH), get_step(a, EAST), get_step(a, WEST)))
        .Where(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(c => Dm.truthy(c.density))).ToList();

    private static void Empty(mob_living_carbon_human who)
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items))
            qdel(held);
    }

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human who, obj_item item) => Do(w, () =>
    {
        Empty(who);
        Waiting.NoSleep(who.put_in_hands(item));
    });

    [MetaServerFact]
    public void Bob_gets_a_bci_at_the_implanter_and_switches_a_pump_on_through_a_usb_cable()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10850, TimeoutSteps = 30000 },
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

        // An implanter beside Bob; a click with the BCI loads it.
        turf start = In(w, () => bobMob.loc as turf);
        turf spot = In(w, () => FreeTilesBy(bobMob).First());
        var implanter = In(w, () => Dm.@new<obj_machinery_bci_implanter>(spot));
        var bci = In(w, () => Dm.@new<obj_item_organ_cyberimp_bci>(bobMob.loc));
        w.Run(2);
        Hold(w, bobMob, bci);
        w.Run(Second);
        ClickOn(bob, implanter);
        w.Run(Second);
        Assert.Same(bci, In(w, () => implanter.bci_to_implant));

        // An empty hand opens it; Bob steps in, and a click shuts it on him.
        ClickOn(bob, implanter);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(implanter.state_open)), "the implanter did not open; runtimes: " + string.Join("\n", runtimes));
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(spot)));
        w.Run(Second);
        ClickOn(bob, implanter);
        w.Run(Second / 2);
        Assert.Same(bobMob, In(w, () => implanter.occupant));
        w.Run(5 * Second);
        Do(w, () =>
        {
            Assert.Same(bobMob, bci.owner);
            Assert.Contains(Dm.each<datum_action>(bobMob.actions), a => a is datum_action_innate_bci_charge_action);
            Assert.Contains(Dm.each<datum_action>(bobMob.actions), a => a is datum_action_innate_circuit_equipment_action);
        });
        Assert.Contains(chat, t => t.Contains("Occupant has been injected with", StringComparison.Ordinal));
        Do(w, () => Waiting.NoSleep(implanter.open_machine()));
        w.Run(Second);

        // A bot shell with a circuit, and a pump beside it.
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(start)));
        w.Run(Second);
        // the bot on the open implanter's tile beside Bob, the pump at his feet
        var bot = In(w, () => Dm.@new<obj_structure_bot>(spot));
        var pump = In(w, () => Dm.@new<obj_machinery_atmospherics_components_binary_pump>(start));
        Do(w, () => Waiting.NoSleep(pump.on_construction(bobMob))); // set up as a placed pipe is: its pipelines, which an unbuilt pump lacks
        var circuit = In(w, () => Dm.@new<obj_item_integrated_circuit>(bobMob.loc));
        Do(w, () => circuit.set_cell(Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit)));
        Hold(w, bobMob, circuit);
        w.Run(Second);
        ClickOn(bob, bot);
        w.Run(Second);
        Assert.Same(bot, In(w, () => circuit.shell));

        var cable = In(w, () => Dm.@new<obj_item_usb_cable>(bobMob.loc));
        Hold(w, bobMob, cable);
        w.Run(Second);
        w.World.Rng.Reseed(10850, 1); // the cable's 1 in 100 fumble
        ClickOn(bob, bot);
        w.Run(Second);
        Assert.Same(circuit, In(w, () => cable.attached_circuit));
        w.World.Rng.Reseed(10851, 1);
        ClickOn(bob, pump);
        w.Run(Second);
        var component = In(w, () => Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_atmos_pump>().SingleOrDefault());
        Assert.True(component != null, "the pump's component was not added; runtimes: " + string.Join("\n", runtimes));
        Assert.Same(circuit, In(w, () => cable.loc));
        var button = In(w, () => Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_bot>().Single());
        Do(w, () => component.on.connect(button.signal));

        // An empty hand pushes the bot: the pump switches on.
        Do(w, () => Empty(bobMob));
        w.Run(Second);
        ClickOn(bob, bot);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(pump.on)), "the pump is off");
        output.WriteLine(string.Join("\n", chat.TakeLast(6)));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
