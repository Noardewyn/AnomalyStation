using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The brain-computer interface in a booted toy room with Bob as a player: its designs are offered; its assembly finishes into the BCI
/// with its circuit; at the BCI implanter, with the BCI inserted and Bob shut inside, the machine raises, works and lowers its arm and
/// implants it, giving Bob the circuit's action button and the charge meter; the circuit then runs on Bob's thoughts (the thought
/// listener asks him in a text window), injects the BCI's reagents into him and draws an overlay that only Bob sees; the implanter
/// takes an implanted BCI back out. The circuit implant shell, put in from its case, injects into its host.
/// </summary>
public class BrainComputerInterfaceTests
{
    private static datum_port_input Port(obj_item_circuit_component c, string name) =>
        Dm.each<datum_port_input>(c.input_ports).First(p => Dm.str(p.name) == name);

    private static obj_item_integrated_circuit CircuitOf(AudioRoom r, atom shell) => r.In(() => (shell.GetComponent(typeof(datum_component_shell)) as datum_component_shell).attached_circuit);

    // the answer to tg's input window of that interface the player has open
    private static void Answer(AudioRoom r, mob who, string iface, object entry)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

    /// <summary>A BCI with these components added to its circuit, implanted into Bob at an implanter beside him, at (5, 4).</summary>
    private static (obj_machinery_bci_implanter Implanter, obj_item_organ_cyberimp_bci Bci) Implanted(AudioRoom r, Feed feed, mob_living_carbon_human bob, params obj_item_circuit_component[] components)
    {
        var implanter = Place<obj_machinery_bci_implanter>(r, 5, 4);
        var bci = r.In(() => Dm.@new<obj_item_organ_cyberimp_bci>(bob.loc));
        r.Step(2);
        obj_item_integrated_circuit circuit = CircuitOf(r, bci);
        Assert.NotNull(circuit);
        r.In(() =>
        {
            circuit.set_cell(Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit)); // a BCI is printed without a cell
            foreach (obj_item_circuit_component component in components)
                Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(component))), component.GetType().Name + " was not added");
        });

        // the BCI goes into the machine from Bob's hand
        r.In(() => Waiting.NoSleep(bob.put_in_hands(bci)));
        r.Step(Second);
        Click(r, bob, implanter);
        r.Step(2);
        Assert.True(feed.Any("balloon: inserted bci"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => Assert.Same(bci, implanter.bci_to_implant));

        // Bob opens it, steps in, and it shuts on him
        r.Step(Second);
        Click(r, bob, implanter);
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(implanter.state_open)));
        r.In(() => Waiting.NoSleep(bob.forceMove(implanter.loc)));
        r.In(() => Waiting.NoSleep(implanter.interact(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(bob, implanter.occupant);
            Assert.Equal("bci_implanter_occupied", implanter.icon_state);
        });

        // a second later the arm raises, works and lowers; at three it implants
        r.Step(Second + Second / 2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(implanter.busy));
            Assert.True(Dm.truthy(implanter.locked));
            Assert.Equal("bci_implanter_raising", implanter.icon_state);
        });
        r.Step(Second);
        r.In(() => Assert.Equal("bci_implanter_active", implanter.icon_state));
        r.Step(Second);
        r.In(() => Assert.Equal("bci_implanter_falling", implanter.icon_state));
        r.Step(Second + 2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(implanter.busy));
            Assert.False(Dm.truthy(implanter.locked));
            Assert.Same(bob, bci.owner);
            Assert.Same(bci, bob.get_organ_by_type(typeof(obj_item_organ_cyberimp_bci)));
            Assert.Null(implanter.bci_to_implant);
        });
        Assert.True(feed.Any("Occupant has been injected with"));
        return (implanter, bci);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_organ_cyberimp_bci), typeof(obj_item_circuit_component_bci_core), typeof(datum_action_innate_bci_charge_action),
            typeof(obj_machinery_bci_implanter), typeof(obj_item_implant_circuit), typeof(obj_item_circuit_component_implant_core),
            typeof(datum_action_innate_implant_charge_action), typeof(obj_item_circuit_component_install_detector), typeof(obj_item_circuit_component_reagent_injector),
            typeof(obj_item_circuit_component_thought_listener), typeof(obj_item_circuit_component_vox), typeof(obj_item_circuit_component_object_overlay),
            typeof(obj_item_circuit_component_object_overlay_bar), typeof(obj_item_circuit_component_counter_overlay), typeof(obj_item_circuit_component_target_intercept),
            typeof(obj_item_circuit_component_remotecam_bci),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheBcisDesignsAreOffered()
    {
        var (r, _, _) = CircuitTestRig.Room(10811);
        r.In(() =>
        {
            foreach (Type design in new[]
            {
                typeof(datum_design_wiremod_shell_bci), typeof(datum_design_wiremod_shell_implant), typeof(datum_design_board_bci_implanter),
                typeof(datum_design_component_bci_bci_camera), typeof(datum_design_component_bci_object_overlay), typeof(datum_design_component_bci_bar_overlay),
                typeof(datum_design_component_bci_vox), typeof(datum_design_component_bci_thought_listener), typeof(datum_design_component_bci_target_intercept),
                typeof(datum_design_component_bci_counter_overlay), typeof(datum_design_component_bci_reagent_injector), typeof(datum_design_component_bci_install_detector),
            })
            {
                var d = SSresearch.techweb_designs[design] as datum_design;
                Assert.NotNull(d);
                Assert.False(d.builds_inert_item(), design.Name + " builds an inert item");
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImplanterImplantsTheBciAndGivesItsOwnerTheButtons()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10812);
        var (implanter, bci) = Implanted(r, feed, bob);
        r.In(() =>
        {
            var actions = Dm.each<datum_action>(bob.actions).ToList();
            Assert.Contains(actions, a => a is datum_action_innate_circuit_equipment_action);
            Assert.Contains(actions, a => a is datum_action_innate_bci_charge_action);
            var core = Dm.each<obj_item_circuit_component>(CircuitOf(r, bci).attached_components).OfType<obj_item_circuit_component_bci_core>().Single();
            Assert.Same(bob, core.user_port.value);
        });

        // the charge meter reads the BCI's cell
        var charge = r.In(() => Dm.each<datum_action>(bob.actions).OfType<datum_action_innate_bci_charge_action>().Single());
        r.In(() => Waiting.NoSleep(charge.Trigger(bob)));
        r.Step(2);
        Assert.True(feed.Any("charge left"), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        // a message from the circuit is heard in Bob's head
        var core2 = r.In(() => Dm.each<obj_item_circuit_component>(CircuitOf(r, bci).attached_components).OfType<obj_item_circuit_component_bci_core>().Single());
        r.In(() => core2.message.set_input("Mind the gap"));
        r.Step(2);
        r.In(() => core2.send_message_signal.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        Assert.Contains(feed.Chat, c => ReferenceEquals(c.Target, bob) && c.Html.Contains("Mind the gap", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheThoughtListenerAsksTheOwnerAndPassesHisThought()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10813);
        var listener = CircuitTestRig.Component<obj_item_circuit_component_thought_listener>(r);
        var (_, bci) = Implanted(r, feed, bob, listener);
        r.In(() => listener.input_name.set_input("Password"));
        r.Step(2);
        CircuitTestRig.Fire(r, listener);
        Answer(r, bob, "TextInputModal", "swordfish");
        r.In(() =>
        {
            Assert.Equal("swordfish", Dm.str(listener.output.value));
            Assert.True(Dm.truthy(listener.ready));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReagentInjectorInjectsTheBcisReagentsIntoItsOwner()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10814);
        var injector = CircuitTestRig.Component<obj_item_circuit_component_reagent_injector>(r);
        var (_, bci) = Implanted(r, feed, bob, injector);
        r.In(() =>
        {
            Assert.NotNull(bci.reagents);
            Waiting.NoSleep(((datum_reagents)bci.reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 10));
        });
        r.In(() => injector.inject.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(0f, ((datum_reagents)bci.reagents).total_volume);
            Assert.True(Dm.truthy(((datum_reagents)bob.reagents).has_reagent(typeof(datum_reagent_consumable_sugar))));
            Assert.NotNull(injector.injected.value);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnObjectOverlayIsShownToTheOwnerOnly()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10815);
        mob_living_carbon_human alice = r.Join(3, 6, "Alice");
        r.Step(Second);
        var crate = Place<obj_structure_closet_crate>(r, 8, 6);
        var overlay = CircuitTestRig.Component<obj_item_circuit_component_object_overlay>(r);
        Implanted(r, feed, bob, overlay);
        r.In(() =>
        {
            overlay.target.set_input(crate);
            overlay.object_overlay_options.set_input("Circle (Red)");
        });
        r.Step(2);
        r.In(() => overlay.signal_on.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            image shown = Dm.each<image>(bob.client.images).FirstOrDefault(i => Dm.str(i.icon_state) == "hud_circle_red");
            Assert.NotNull(shown);
            Assert.Same(crate, shown.loc);
            Assert.DoesNotContain(Dm.each<image>(alice.client.images), i => Dm.str(i.icon_state) == "hud_circle_red");
        });
        r.In(() => overlay.signal_off.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() => Assert.DoesNotContain(Dm.each<image>(bob.client.images), i => Dm.str(i.icon_state) == "hud_circle_red"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImplanterTakesAnImplantedBciBackOut()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10816);
        var (implanter, bci) = Implanted(r, feed, bob);

        // with no BCI loaded, a second run takes Bob's out into the machine's storage
        r.Step(Second);
        r.In(() => Waiting.NoSleep(implanter.open_machine()));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.forceMove(implanter.loc)));
        r.In(() => Waiting.NoSleep(implanter.interact(bob)));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.Null(bci.owner);
            Assert.Null(bob.get_organ_by_type(typeof(obj_item_organ_cyberimp_bci)));
            Assert.Same(bci, implanter.bci_to_implant);
            Assert.Same(implanter, bci.loc);
            Assert.DoesNotContain(Dm.each<datum_action>(bob.actions), a => a is datum_action_innate_bci_charge_action);
        });
        Assert.True(feed.Any("transferred to internal storage unit"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyImplanterRefusesAnOccupantWithoutABci()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10817);
        var implanter = Place<obj_machinery_bci_implanter>(r, 6, 4);
        r.In(() => Waiting.NoSleep(implanter.open_machine()));
        r.In(() => Waiting.NoSleep(bob.forceMove(implanter.loc)));
        r.In(() => Waiting.NoSleep(implanter.interact(bob)));
        r.Step(5 * Second);
        Assert.True(feed.Any("No brain-computer interface inserted"));
        r.In(() => Assert.False(Dm.truthy(implanter.busy)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCircuitImplantInjectsIntoItsHost()
    {
        var (r, _, bob) = CircuitTestRig.Room(10818);
        var implant = r.In(() => Dm.@new<obj_item_implant_circuit>(bob.loc));
        r.Step(2);
        var injector = CircuitTestRig.Component<obj_item_circuit_component_reagent_injector>(r);
        var circuit = CircuitTestRig.Circuit(r, injector);
        CircuitTestRig.Attach(r, implant, circuit);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(implant.implant(bob, bob)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(bob, implant.imp_in);
            Assert.Contains(Dm.each<datum_action>(bob.actions), a => a is datum_action_innate_implant_charge_action);
            Waiting.NoSleep(((datum_reagents)implant.reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 5));
        });
        r.In(() => injector.inject.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(((datum_reagents)bob.reagents).has_reagent(typeof(datum_reagent_consumable_sugar)))));

        // a second circuit implant is refused
        var second = r.In(() => Dm.@new<obj_item_implant_circuit>(bob.loc));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(second.implant(bob, bob)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCircuitImplantCaseHoldsALiveImplant()
    {
        var (r, _, _) = CircuitTestRig.Room(10819);
        var implantCase = Place<obj_item_implantcase_circuit>(r, 6, 4);
        r.In(() =>
        {
            Assert.IsType<obj_item_implant_circuit>(implantCase.imp);
            Assert.False(InertSeam.IsInert(implantCase.imp));
            Assert.NotNull((implantCase.imp as atom).GetComponent(typeof(datum_component_shell)));
        });
        r.AssertNoRuntimes();
    }
}
