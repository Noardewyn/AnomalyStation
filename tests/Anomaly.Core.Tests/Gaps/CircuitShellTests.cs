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
/// The circuits' shells in a booted toy room with Bob as a player: the shell assemblies finish into their shells with a screwdriver
/// (the brain-computer interface's still refuses); the bot signals Bob's push; the drone walks by its circuit; the airlock shell opens
/// and bolts by its circuit; the dispenser takes an item and vends it; the money bot takes cash and pays out a holochip; the scanner
/// gate scans who walks through it with its scanning overlay; the circuit gun fires and reports what it hit; the handheld scanner
/// reports what Bob uses it on; a circuit in a bot draws
/// its cell while an anchored airlock shell's circuit runs on the area's power; the presets load their wired components; the shells'
/// research designs are offered; every newly live circuit type is made and deleted cleanly.
/// </summary>
public class CircuitShellTests
{
    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items))
                qdel(held);
        });
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static T ShellPart<T>(AudioRoom r, atom shell) where T : obj_item_circuit_component =>
        r.In(() => Dm.each<obj_item_circuit_component>(CircuitTestRig.ShellOf(r, shell).unremovable_circuit_components).OfType<T>().Single());

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_structure_bot), typeof(obj_structure_money_bot), typeof(mob_living_circuit_drone), typeof(obj_structure_server),
            typeof(obj_machinery_door_airlock_shell), typeof(obj_structure_dispenser_bot), typeof(obj_structure_scanner_gate_shell),
            typeof(obj_item_gun_energy_wiremod_gun), typeof(obj_item_keyboard_shell), typeof(obj_item_wiremod_scanner), typeof(obj_item_assembly_wiremod),
            typeof(obj_item_mod_module_circuit), typeof(obj_item_undertile_circuit), typeof(obj_structure_wallmount_circuit),
            typeof(obj_item_integrated_circuit_loaded_hello_world), typeof(obj_item_integrated_circuit_loaded_speech_relay),
            typeof(obj_item_circuit_component_bot), typeof(obj_item_circuit_component_bot_circuit), typeof(obj_item_circuit_component_airlock),
            typeof(obj_item_circuit_component_airlock_access_event), typeof(obj_item_circuit_component_dispenser_bot), typeof(obj_item_circuit_component_vendor_component),
            typeof(obj_item_circuit_component_money_bot), typeof(obj_item_circuit_component_money_dispenser), typeof(obj_item_circuit_component_scanner_gate),
            typeof(obj_item_circuit_component_wiremod_gun), typeof(obj_item_circuit_component_keyboard_shell), typeof(obj_item_circuit_component_wiremod_scanner),
            typeof(obj_item_circuit_component_assembly_input), typeof(obj_item_circuit_component_assembly_output), typeof(obj_item_circuit_component_mod_adapter_core),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheShellAssembliesFinishIntoTheirShellsWithAScrewdriver()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10731);
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        (Type Assembly, Type Shell)[] kinds =
        {
            (typeof(obj_item_shell_bot), typeof(obj_structure_bot)), (typeof(obj_item_shell_money_bot), typeof(obj_structure_money_bot)),
            (typeof(obj_item_shell_drone), typeof(mob_living_circuit_drone)), (typeof(obj_item_shell_server), typeof(obj_structure_server)),
            (typeof(obj_item_shell_airlock), typeof(obj_machinery_door_airlock_shell)), (typeof(obj_item_shell_dispenser), typeof(obj_structure_dispenser_bot)),
            (typeof(obj_item_shell_scanner_gate), typeof(obj_structure_scanner_gate_shell)),
        };
        int x = 5;
        foreach ((Type assemblyType, Type shellType) in kinds)
        {
            var assembly = r.In(() => Dm.@new(assemblyType, locate(5, 4, 1)) as obj_item_shell);
            r.Step(2);
            r.In(() => Waiting.Detach(assembly.screwdriver_act(bob, screwdriver)));
            r.Step(11 * Second); // three seconds, ten for the server and the airlock
            Assert.True(r.In(() => QDELETED(assembly)), assemblyType.Name + " was not finished");
            atom shell = r.In(() => locate(5, 4, 1).dm_held.FirstOrDefault(a => a.GetType() == shellType));
            Assert.True(shell != null, shellType.Name + " not made; the tile holds " + r.In(() => string.Join(", ", locate(5, 4, 1).dm_held.Select(a => a.GetType().Name))));
            Assert.NotNull(CircuitTestRig.ShellOf(r, shell));
            r.In(() => Waiting.NoSleep((shell as atom_movable).forceMove(locate(x++, 7, 1)))); // out of the way of the next one
        }
        Assert.True(feed.Any("You finish"), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        // the brain-computer interface's assembly waits for its shell
        var bci = r.In(() => Dm.@new<obj_item_shell_bci>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(bci.screwdriver_act(bob, screwdriver)));
        r.Step(6 * Second);
        Assert.False(r.In(() => QDELETED(bci)));
        Assert.True(feed.Any("balloon: not ported yet"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBotSignalsWhenBobPushesIt()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10732);
        var toggle = CircuitTestRig.Component<obj_item_circuit_component_compare_toggle>(r);
        var (bot, _) = CircuitTestRig.InBot(r, 5, 4, toggle);
        var button = ShellPart<obj_item_circuit_component_bot>(r, bot);
        r.In(() => (toggle.input_ports[2] as datum_port_input).connect(button.signal)); // Toggle And Compare from the bot's signal
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(bob.held_items))
                qdel(held);
        });
        r.Step(Second);
        int sounds = r.Sounds.Heard.Count;
        Click(r, bob, bot);
        r.Step(2);
        Assert.True(feed.Any("balloon: pushed button"));
        Assert.Same(bob, r.In(() => button.entity.value));
        Assert.Equal(1f, r.In(() => toggle.toggle_state));
        Assert.Contains(r.Sounds.Heard.Skip(sounds), h => h.File != null);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDroneWalksByItsCircuit()
    {
        var (r, _, _) = CircuitTestRig.Room(10733);
        var drone = Place<mob_living_circuit_drone>(r, 7, 4);
        var circuit = CircuitTestRig.Circuit(r);
        CircuitTestRig.Attach(r, drone, circuit);
        var legs = ShellPart<obj_item_circuit_component_bot_circuit>(r, drone);
        r.In(() => legs.north.set_input(COMPONENT_SIGNAL));
        r.Step(Second);
        Assert.Equal((7f, 5f), r.In(() => (drone.x, drone.y)));
        r.In(() => legs.east.set_input(COMPONENT_SIGNAL));
        r.Step(Second);
        Assert.Equal((8f, 5f), r.In(() => (drone.x, drone.y)));
        // the move delay: two moves north at once take one step
        r.In(() =>
        {
            legs.north.set_input(COMPONENT_SIGNAL);
            legs.north.set_input(COMPONENT_SIGNAL);
        });
        r.Step(2);
        Assert.Equal((8f, 6f), r.In(() => (drone.x, drone.y)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAirlockShellOpensAndBoltsByItsCircuit()
    {
        var (r, _, _) = CircuitTestRig.Room(10734);
        var airlock = Place<obj_machinery_door_airlock_shell>(r, 7, 4);
        var circuit = CircuitTestRig.Circuit(r);
        CircuitTestRig.Attach(r, airlock, circuit);
        var control = ShellPart<obj_item_circuit_component_airlock>(r, airlock);
        Assert.True(r.In(() => Dm.truthy(airlock.density)));
        r.In(() => control.open.set_input(COMPONENT_SIGNAL));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(airlock.density), "the airlock did not open");
            Assert.Equal(1f, Dm.num(control.is_open.value));
            Assert.NotNull(control.opened.value);
        });
        r.In(() => control.close.set_input(COMPONENT_SIGNAL));
        r.Step(2 * Second);
        r.In(() => control.bolt.set_input(COMPONENT_SIGNAL));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(airlock.density));
            Assert.True(Dm.truthy(airlock.locked));
            Assert.Equal(1f, Dm.num(control.is_bolted.value));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDispenserTakesAnItemAndVendsIt()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10735);
        var dispenser = Place<obj_structure_dispenser_bot>(r, 5, 4);
        CircuitTestRig.Attach(r, dispenser, CircuitTestRig.Circuit(r));
        var part = ShellPart<obj_item_circuit_component_dispenser_bot>(r, dispenser);
        var crayon = Hold<obj_item_toy_crayon_red>(r, bob);
        r.Step(Second);
        Click(r, bob, dispenser);
        r.Step(2);
        Assert.True(feed.Any("balloon: inserted item"));
        r.In(() =>
        {
            Assert.Same(dispenser, crayon.loc);
            Assert.Contains(crayon, Dm.each<obj_item>(part.item_list.value));
            Assert.NotNull(part.on_item_added.value);
        });

        // the dispenser's plus button adds a vendor component; its Vend Item drops the item at the dispenser
        r.In(() => Waiting.NoSleep(part.ui_perform_action(bob, "add_vend_component")));
        r.Step(2);
        var vendor = r.In(() => Dm.each<obj_item_circuit_component>(part.parent.attached_components).OfType<obj_item_circuit_component_vendor_component>().Single());
        r.In(() =>
        {
            vendor.item_to_vend.set_input(crayon);
            vendor.vend_item.set_input(COMPONENT_SIGNAL);
        });
        r.Step(2);
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => crayon.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMoneyBotTakesCashAndPaysOutAHolochip()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10736);
        var moneyBot = Place<obj_structure_money_bot>(r, 5, 4);
        CircuitTestRig.Attach(r, moneyBot, CircuitTestRig.Circuit(r));
        var counter = ShellPart<obj_item_circuit_component_money_bot>(r, moneyBot);
        var payer = ShellPart<obj_item_circuit_component_money_dispenser>(r, moneyBot);
        Hold<obj_item_stack_spacecash_c100>(r, bob);
        r.Step(Second);
        Click(r, bob, moneyBot);
        r.Step(2);
        Assert.True(feed.Any("balloon: inserted 100"));
        r.In(() =>
        {
            Assert.Equal(100f, Dm.num(moneyBot.stored_money));
            Assert.Equal(100f, Dm.num(counter.total_money.value));
            Assert.Equal(100f, Dm.num(counter.money_input.value));
            Assert.Same(bob, counter.entity.value);
        });

        r.In(() => payer.dispense_amount.set_input(30.7f));
        CircuitTestRig.Fire(r, payer);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(70f, Dm.num(moneyBot.stored_money));
            var chip = locate(5, 4, 1).dm_held.OfType<obj_item_holochip>().SingleOrDefault();
            Assert.True(chip != null, "no holochip; the tile holds " + string.Join(", ", locate(5, 4, 1).dm_held.Select(a => a.GetType().Name)));
            Assert.Equal(30f, Dm.num(chip.credits));
        });
        // the dispense cooldown
        CircuitTestRig.Fire(r, payer);
        r.Step(1);
        Assert.NotNull(r.In(() => payer.on_fail.value));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannerGateScansWhoWalksThroughIt()
    {
        var (r, _, bob) = CircuitTestRig.Room(10737);
        var gate = Place<obj_structure_scanner_gate_shell>(r, 5, 4);
        Assert.Contains("passive", r.In(() => Dm.each(gate.overlays).Select(o => (o as DmAppearance)?.icon_state).ToList()));
        var wrench = Hold<obj_item_wrench>(r, bob);
        r.In(() => Waiting.NoSleep(gate.wrench_act(bob, wrench)));
        Assert.True(r.In(() => Dm.truthy(gate.anchored)));
        var circuit = CircuitTestRig.Circuit(r);
        CircuitTestRig.Attach(r, gate, circuit);
        var scanner = ShellPart<obj_item_circuit_component_scanner_gate>(r, gate);

        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST)));
        r.Step(1);
        r.In(() =>
        {
            Assert.Same(bob, scanner.scanned.value);
            Assert.NotNull(scanner.trigger_output.value);
            Assert.Contains("scanning", Dm.each(gate.overlays).Select(o => (o as DmAppearance)?.icon_state).ToList());
        });
        r.Step(Second);
        Assert.Contains("passive", r.In(() => Dm.each(gate.overlays).Select(o => (o as DmAppearance)?.icon_state).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCircuitGunFiresAndReportsWhatItHit()
    {
        var (r, _, bob) = CircuitTestRig.Room(10738);
        var gun = Hold<obj_item_gun_energy_wiremod_gun>(r, bob);
        var circuit = CircuitTestRig.Circuit(r);
        CircuitTestRig.Attach(r, gun, circuit);
        var part = ShellPart<obj_item_circuit_component_wiremod_gun>(r, gun);
        var target = r.Dummy(7, 4);
        r.Step(Second);
        int sounds = r.Sounds.Heard.Count;
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, bob, "icon-x=16;icon-y=16")));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(target, part.shot.value);
            Assert.Same(bob, part.shooter.value);
            Assert.NotNull(part.signal.value);
            Assert.Equal(100f, Dm.num(target.health)); // the scanning beam does no harm
        });
        Assert.Contains(r.Sounds.Heard.Skip(sounds), h => h.File?.Contains("blaster", StringComparison.Ordinal) == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannerShellScansWhatBobUsesItOn()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10743);
        var scanner = Hold<obj_item_wiremod_scanner>(r, bob);
        CircuitTestRig.Attach(r, scanner, CircuitTestRig.Circuit(r));
        var part = ShellPart<obj_item_circuit_component_wiremod_scanner>(r, scanner);
        var crayon = Place<obj_item_toy_crayon_red>(r, 5, 4);
        r.Step(Second);
        Click(r, bob, crayon);
        r.Step(2);
        Assert.True(feed.Any("balloon: scanned object"));
        r.In(() =>
        {
            Assert.Same(bob, part.attacker.value);
            Assert.Same(crayon, part.attacking.value);
            Assert.NotNull(part.signal.value);
            Assert.Same(r.In(() => locate(5, 4, 1)), crayon.loc); // scanned, not picked up
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABotsCircuitDrawsItsCellAndAnAnchoredAirlocksRunsOnTheAreasPower()
    {
        var (r, _, _) = CircuitTestRig.Room(10739);
        var botToggle = CircuitTestRig.Component<obj_item_circuit_component_compare_toggle>(r);
        var (_, botCircuit) = CircuitTestRig.InBot(r, 5, 4, botToggle);
        var airlock = Place<obj_machinery_door_airlock_shell>(r, 9, 4);
        var airlockToggle = CircuitTestRig.Component<obj_item_circuit_component_compare_toggle>(r);
        var airlockCircuit = CircuitTestRig.Circuit(r, airlockToggle);
        CircuitTestRig.Attach(r, airlock, airlockCircuit);
        float botCharge = r.In(() => botCircuit.cell.charge);
        float airlockCharge = r.In(() => airlockCircuit.cell.charge);
        r.In(() =>
        {
            (botToggle.input_ports[2] as datum_port_input).set_input(COMPONENT_SIGNAL); // Toggle And Compare
            (airlockToggle.input_ports[2] as datum_port_input).set_input(COMPONENT_SIGNAL);
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(botCharge - botToggle.energy_usage_per_input, botCircuit.cell.charge, 3);
            Assert.Equal(airlockCharge, airlockCircuit.cell.charge);
            Assert.Equal(1f, botToggle.toggle_state);
            Assert.Equal(1f, airlockToggle.toggle_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePresetsLoadTheirWiredComponents()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10740);
        var hello = r.In(() => Dm.@new<obj_item_integrated_circuit_loaded_hello_world>(locate(8, 4, 1)));
        var relay = r.In(() => Dm.@new<obj_item_integrated_circuit_loaded_speech_relay>(locate(8, 4, 1)));
        r.Step(2);
        var helloSpeech = r.In(() => Dm.each<obj_item_circuit_component>(hello.attached_components).OfType<obj_item_circuit_component_speech>().Single());
        Assert.Equal("Hello World", r.In(() => Dm.str(helloSpeech.message.value)));
        Assert.NotNull(r.In(() => hello.cell)); // the loaded circuits come with a cell

        // the relay in a bot takes what it hears as its speech's message; its speech says it once triggered (tg leaves that wire to
        // the player)
        var bot = Place<obj_structure_bot>(r, 5, 4);
        CircuitTestRig.Attach(r, bot, relay);
        r.In(() => Waiting.NoSleep(bob.say("testing the relay")));
        r.Step(4);
        var relaySpeech = r.In(() => Dm.each<obj_item_circuit_component>(relay.attached_components).OfType<obj_item_circuit_component_speech>().Single());
        Assert.Equal("Testing the relay", r.In(() => Dm.str(relaySpeech.message.value))); // as say() treats it
        int heard = feed.Chat.Count(c => ReferenceEquals(c.Target, bob) && c.Html.Contains("Testing the relay", StringComparison.Ordinal));
        CircuitTestRig.Fire(r, relaySpeech);
        r.Step(4);
        Assert.Equal(heard + 1, feed.Chat.Count(c => ReferenceEquals(c.Target, bob) && c.Html.Contains("Testing the relay", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASavedBotCircuitLoadsIntoANewBot()
    {
        var (r, _, _) = CircuitTestRig.Room(10744);
        const string json = "{\"components\":{\"s\":{\"type\":\"/obj/item/circuit_component/speech\",\"connections\":{\"Message\":{\"stored_data\":\"Hi\"}}}},"
            + "\"external_objects\":{\"x\":{\"type\":\"/obj/structure/bot\"}}}";
        var circuit = r.In(() => Dm.@new<obj_item_integrated_circuit_loaded>(locate(6, 6, 1)));
        DmList errors = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(circuit.load_circuit_data(json, errors)));
        r.Step(2);
        Assert.Empty(r.In(() => Dm.each(errors).Select(Dm.str).ToList()));
        var bot = r.In(() => locate(6, 6, 1).dm_held.OfType<obj_structure_bot>().SingleOrDefault());
        Assert.True(bot != null, "no bot; the tile holds " + r.In(() => string.Join(", ", locate(6, 6, 1).dm_held.Select(a => a.GetType().Name))));
        r.In(() =>
        {
            Assert.Same(bot, circuit.shell);
            var speech = Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_speech>().Single();
            Assert.Equal("Hi", Dm.str(speech.message.value));
            Assert.Single(Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_bot>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShellsAndComponentsResearchDesignsAreOffered()
    {
        var (r, _, _) = CircuitTestRig.Room(10741);
        r.In(() =>
        {
            foreach (Type design in new[]
            {
                typeof(datum_design_wiremod_shell_bot), typeof(datum_design_wiremod_shell_money_bot), typeof(datum_design_wiremod_shell_drone),
                typeof(datum_design_wiremod_shell_server), typeof(datum_design_wiremod_shell_airlock), typeof(datum_design_wiremod_shell_dispenser),
                typeof(datum_design_wiremod_shell_scanner_gate), typeof(datum_design_wiremod_shell_gun), typeof(datum_design_wiremod_shell_keyboard),
                typeof(datum_design_wiremod_shell_scanner), typeof(datum_design_wiremod_shell_assembly), typeof(datum_design_wiremod_shell_mod_module),
                typeof(datum_design_wiremod_shell_undertile), typeof(datum_design_wiremod_shell_wallmount), typeof(datum_design_component_action_speech),
                typeof(datum_design_component_entity_hear), typeof(datum_design_component_action_radio), typeof(datum_design_component_entity_gps),
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
    public void EveryNewlyLiveCircuitTypeIsMadeAndDeletedCleanly()
    {
        var (r, _, _) = CircuitTestRig.Room(10742);
        List<Type> types = CircuitTestRig.NewlyLiveAtoms();
        Assert.True(types.Count > 70, types.Count + " newly live circuit types");
        foreach (Type type in types)
        {
            atom made = r.In(() => Dm.@new(type, locate(6, 6, 1)) as atom);
            r.Step(2);
            r.In(() => qdel(made));
            r.Step(2);
            Assert.True(r.In(() => QDELETED(made)), type.Name);
        }
        r.AssertNoRuntimes();
    }
}
