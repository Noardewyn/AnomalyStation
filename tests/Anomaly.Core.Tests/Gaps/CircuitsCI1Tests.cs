using System.Text.Json;
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
/// Gaps package CI1 (D339, D341, the circuits' core and editor) in a booted toy room with Bob as a player: the component printer prints
/// a researched component from the silo's materials through its window and straight into a linked circuit; the IntegratedCircuit window
/// wires ports of matching datatypes and refuses the others; programs of math, string, list, variable, delay and clock components
/// compute; a circuit in a compact remote fires when the remote is used in hand, drawing its cell's power until the cell is empty; the
/// module duplicator scans a circuit and prints a copy; the circuit multitool marks a thing; a circuit saves to JSON and loads back;
/// every live circuit type is made and deleted cleanly; tg's circuit_component_category unit test.
/// </summary>
public class CircuitsCI1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        // the toy room has no APC: free power
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(new object[] { who.loc }.Concat(args).ToArray()));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items))
            qdel(held);
    });

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    /// <summary>The flicks (DM's builtin flick) the room's atoms show.</summary>
    private sealed class FlickRecorder : IAppearanceSink
    {
        public List<(object Target, string State)> Flicks { get; } = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void Flick(object target, object state) => Flicks.Add((target, Dm.str(state)));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    private static FlickRecorder RecordFlicks(AudioRoom r)
    {
        var recorder = new FlickRecorder();
        PresentationSinks.Of(r.World).Appearance = recorder;
        return recorder;
    }

    private static List<string> Flicks(FlickRecorder recorder, atom a) => recorder.Flicks.Where(f => ReferenceEquals(f.Target, a)).Select(f => f.State).ToList();

    /// <summary>A circuit at the tile with a high capacity cell, switched on (as a shell does), with these components.</summary>
    private static obj_item_integrated_circuit Circuit(AudioRoom r, params obj_item_circuit_component[] components) => r.In(() =>
    {
        var circuit = Dm.@new<obj_item_integrated_circuit>(locate(8, 4, 1));
        var cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit);
        circuit.set_cell(cell);
        foreach (obj_item_circuit_component component in components)
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(component))));
        circuit.set_on(TRUE);
        return circuit;
    });

    private static T Component<T>(AudioRoom r) where T : obj_item_circuit_component => r.In(() => Dm.@new<T>(locate(8, 4, 1)));

    private static datum_port_input In(obj_item_circuit_component c, int i) => c.input_ports[i + 1] as datum_port_input; // 0-based here, DM lists from 1

    private static datum_port_output Out(obj_item_circuit_component c, int i) => c.output_ports[i + 1] as datum_port_output;

    /// <summary>Fires a component's trigger input (tg's Trigger port).</summary>
    private static void Fire(AudioRoom r, obj_item_circuit_component c) => r.In(() => c.trigger_input.set_input(COMPONENT_SIGNAL));

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_integrated_circuit), typeof(obj_item_integrated_circuit_loaded), typeof(obj_item_integrated_circuit_module),
            typeof(obj_item_circuit_component), typeof(obj_item_circuit_component_arithmetic), typeof(obj_item_circuit_component_compare_comparison),
            typeof(obj_item_circuit_component_compare_logic), typeof(obj_item_circuit_component_compare_toggle), typeof(obj_item_circuit_component_random),
            typeof(obj_item_circuit_component_trigonometry), typeof(obj_item_circuit_component_binary_conversion), typeof(obj_item_circuit_component_decimal_conversion),
            typeof(obj_item_circuit_component_list_literal), typeof(obj_item_circuit_component_assoc_literal), typeof(obj_item_circuit_component_filter_list),
            typeof(obj_item_circuit_component_foreach), typeof(obj_item_circuit_component_format), typeof(obj_item_circuit_component_format_assoc),
            typeof(obj_item_circuit_component_index), typeof(obj_item_circuit_component_index_assoc_string), typeof(obj_item_circuit_component_list_pick),
            typeof(obj_item_circuit_component_variable_list_listadd), typeof(obj_item_circuit_component_concat), typeof(obj_item_circuit_component_compare_contains_replace),
            typeof(obj_item_circuit_component_textcase), typeof(obj_item_circuit_component_tostring), typeof(obj_item_circuit_component_tonumber),
            typeof(obj_item_circuit_component_select), typeof(obj_item_circuit_component_get_column), typeof(obj_item_circuit_component_index_table),
            typeof(obj_item_circuit_component_variable_getter), typeof(obj_item_circuit_component_variable_setter), typeof(obj_item_circuit_component_variable_setter_trigger),
            typeof(obj_item_circuit_component_clock), typeof(obj_item_circuit_component_delay), typeof(obj_item_circuit_component_router),
            typeof(obj_item_circuit_component_router_multiplexer), typeof(obj_item_circuit_component_timepiece), typeof(obj_item_circuit_component_typecast),
            typeof(obj_item_circuit_component_compare_typecheck), typeof(obj_item_circuit_component_nfc_send), typeof(obj_item_circuit_component_nfc_receive),
            typeof(obj_item_circuit_component_list_literal_nfc_send), typeof(obj_item_circuit_component_wire_bundle), typeof(obj_item_circuit_component_module),
            typeof(obj_item_circuit_component_compact_remote), typeof(obj_item_circuit_component_controller), typeof(obj_item_compact_remote), typeof(obj_item_controller),
            typeof(obj_item_multitool_circuit), typeof(obj_item_usb_cable), typeof(obj_machinery_component_printer), typeof(obj_machinery_debug_component_printer),
            typeof(obj_machinery_module_duplicator), typeof(obj_item_shell), typeof(datum_component_shell), typeof(datum_port), typeof(datum_port_input),
            typeof(datum_port_output), typeof(datum_port_input_option), typeof(datum_circuit_variable), typeof(datum_circuit_datatype_number),
            typeof(datum_circuit_datatype_composite_instance), typeof(datum_controller_subsystem_circuit_component), typeof(datum_controller_subsystem_wiremod_composite),
            typeof(datum_controller_subsystem_processing_clock_component), typeof(datum_wires_wire_bundle_component), typeof(regex_format_component),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ThePrinterPrintsAResearchedComponentFromItsWindowAndIntoALinkedCircuit()
    {
        var (r, feed, bob) = Room(10601);
        var silo = Place<obj_machinery_ore_silo>(r, 8, 6);
        var printer = Place<obj_machinery_component_printer>(r, 5, 4);
        // the station's techweb: the starting circuit designs
        r.In(() =>
        {
            Assert.NotNull(printer.techweb);
            Assert.True(Dm.truthy(printer.current_unlocked_designs[typeof(obj_item_circuit_component_arithmetic)]));
        });

        // a multitool links the silo; glass goes into it through the printer (a component's design costs half a sheet of glass)
        var multitool = Hold<obj_item_multitool>(r, bob);
        r.In(() => multitool.set_buffer(silo));
        Use(r, bob, printer);
        Assert.Same(silo, r.In(() => printer.materials.silo));
        EmptyHands(r, bob);
        Hold<obj_item_stack_sheet_glass>(r, bob, 10);
        Use(r, bob, printer);
        EmptyHands(r, bob);

        // the ComponentPrinter window lists the arithmetic component's design; printing it spends the glass and drops it
        Take(bob);
        Use(r, bob, printer);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ComponentPrinter", InterfaceOf(payload));
        JsonElement design = payload.GetProperty("static_data").GetProperty("designs").GetProperty("/datum/design/component/math/arithmetic");
        Assert.Equal("Arithmetic Component", design.GetProperty("name").GetString());
        float glass = r.In(() => Dm.num(Waiting.NoSleep(silo.materials.get_material_amount(Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_glass)))))));
        Act(r, bob, window, "print", ("design_path", "/datum/design/component/math/arithmetic"));
        var printed = r.In(() => Dm.each<obj_item_circuit_component_arithmetic>(printer.loc).SingleOrDefault());
        Assert.NotNull(printed);
        Assert.True(r.In(() => Dm.num(Waiting.NoSleep(silo.materials.get_material_amount(Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_glass))))))) < glass);

        // a circuit hit onto the printer links it; its window then prints a component straight into the circuit
        var circuit = Hold<obj_item_integrated_circuit>(r, bob);
        Use(r, bob, printer);
        Assert.Same(printer, r.In(() => circuit.linked_component_printer?.resolve()));
        Take(bob);
        r.Command(() => Waiting.Detach(circuit.ui_interact(bob)));
        r.Step(4);
        payload = LastUpdate(Take(bob), out string editor);
        Assert.Equal("IntegratedCircuit", InterfaceOf(payload));
        Assert.True(payload.GetProperty("static_data").GetProperty("stored_designs").TryGetProperty("/obj/item/circuit_component/arithmetic", out _));
        Assert.Contains(payload.GetProperty("static_data").GetProperty("circuit_components").EnumerateArray(), c => c.GetProperty("name").GetString() == "Arithmetic");
        Act(r, bob, editor, "print_component", ("component_to_print", "/obj/item/circuit_component/arithmetic"), ("rel_x", 40), ("rel_y", 30));
        r.In(() =>
        {
            obj_item_circuit_component added = Dm.each<obj_item_circuit_component>(circuit.attached_components).Single();
            Assert.IsType<obj_item_circuit_component_arithmetic>(added);
            Assert.Same(circuit, added.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEditorWiresPortsOfMatchingDatatypesAndRefusesTheOthers()
    {
        var (r, _, bob) = Room(10602);
        var number = Component<obj_item_circuit_component_arithmetic>(r);
        var text = Component<obj_item_circuit_component_tostring>(r);
        var list = Component<obj_item_circuit_component_list_literal>(r);
        var concat = Component<obj_item_circuit_component_concat>(r);
        var circuit = Circuit(r, number, text, list, concat);
        // a circuit outside a shell is worked on in hand (tg's hands_state)
        r.In(() => Waiting.NoSleep(bob.put_in_hands(circuit)));
        r.In(() => Assert.Same(circuit, bob.get_active_held_item()));
        Take(bob);
        r.Command(() => Waiting.Detach(circuit.ui_interact(bob)));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        JsonElement components = payload.GetProperty("data").GetProperty("components");
        Assert.Equal(4, components.GetArrayLength());
        Assert.Equal("number", components[0].GetProperty("output_ports")[0].GetProperty("type").GetString());

        int Id(obj_item_circuit_component c) => r.In(() => (int)circuit.attached_components.Find(c));
        void Wire(obj_item_circuit_component from, int outPort, obj_item_circuit_component to, int inPort) => Act(r, bob, window, "add_connection",
            ("input_component_id", Id(to)), ("input_port_id", inPort), ("output_component_id", Id(from)), ("output_port_id", outPort));

        // a number into a string port, a string into the concat's string port: both wired
        Wire(number, 1, concat, 1);
        Wire(text, 1, concat, 2);
        // a string into a number port, a list into a number port: refused
        Wire(text, 1, number, 2);
        Wire(list, 1, number, 3);
        r.In(() =>
        {
            Assert.Contains(Out(number, 0), Dm.each<datum_port_output>(In(concat, 0).connected_ports));
            Assert.Contains(Out(text, 0), Dm.each<datum_port_output>(In(concat, 1).connected_ports));
            Assert.Empty(Dm.each<datum_port_output>(In(number, 1).connected_ports));
            Assert.Empty(Dm.each<datum_port_output>(In(number, 2).connected_ports));
        });

        // a value set from the window travels the wire at the next circuit tick
        Act(r, bob, window, "set_component_input", ("component_id", Id(number)), ("port_id", 2), ("input", 4));
        Act(r, bob, window, "set_component_input", ("component_id", Id(number)), ("port_id", 3), ("input", 5));
        Fire(r, number);
        r.Step(2);
        Assert.Equal(9f, r.In(() => Dm.num(Out(number, 0).value)));
        Assert.Equal("9", r.In(() => In(concat, 0).value));

        // a right click on the input removes its wires
        Act(r, bob, window, "remove_connection", ("component_id", Id(concat)), ("is_input", 1), ("port_id", 1));
        r.In(() => Assert.Empty(Dm.each<datum_port_output>(In(concat, 0).connected_ports)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ProgramsOfMathStringListVariableDelayAndClockComponentsCompute()
    {
        var (r, _, _) = Room(10603);
        var add = Component<obj_item_circuit_component_arithmetic>(r);
        var compare = Component<obj_item_circuit_component_compare_comparison>(r);
        var concat = Component<obj_item_circuit_component_concat>(r);
        var literal = Component<obj_item_circuit_component_list_literal>(r);
        var length = Component<obj_item_circuit_component_length>(r);
        var delay = Component<obj_item_circuit_component_delay>(r);
        var clock = Component<obj_item_circuit_component_clock>(r);
        var circuit = Circuit(r, add, compare, concat, literal, length, delay, clock);

        // 2 + 3 + 4 (the add port button gives a third input)
        // the title row's plus button: the add port component's action (integrated_circuit.dm's perform_action)
        r.In(() => add.SEND_SIGNAL(COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION, Dm.list(add, null, "add")));
        r.In(() =>
        {
            Assert.Equal(5f, Dm.length(add.input_ports)); // the option, three numbers and the trigger (order 2, last)
            In(add, 1).set_input(2);
            In(add, 2).set_input(3);
            In(add, 3).set_input(4);
        });
        Fire(r, add);
        Assert.Equal(9f, r.In(() => Dm.num(Out(add, 0).value)));
        r.In(() => In(add, 0).set_input("Multiply"));
        Fire(r, add);
        Assert.Equal(24f, r.In(() => Dm.num(Out(add, 0).value)));

        // comparisons: 24 > 10, and its true signal
        r.In(() =>
        {
            In(compare, 0).set_input(COMP_COMPARISON_GREATER_THAN);
            In(compare, 1).set_input(24);
            In(compare, 2).set_input(10);
        });
        r.Step(2); // the datatypes change to numbers on the first input, then the compare signal (a compare component has no trigger port)
        r.In(() => In(compare, 3).set_input(COMPONENT_SIGNAL));
        r.Step(2);
        Assert.Equal(1f, r.In(() => Dm.num(compare.result.value)));

        // strings join, a list literal counts its entries
        r.In(() =>
        {
            In(concat, 0).set_input("circuit ");
            In(concat, 1).set_input("works");
            In(literal, 1).set_input("a");
            In(literal, 2).set_input(null);
        });
        Fire(r, concat);
        Fire(r, literal);
        Assert.Equal("circuit works", r.In(() => Out(concat, 0).value));
        r.In(() => In(length, 0).connect(literal.list_output));
        r.Step(2);
        Fire(r, length);
        Assert.Equal(1f, r.In(() => Dm.num(Out(length, 0).value)));

        // a setter writes a circuit variable, a getter reads it
        r.In(() =>
        {
            var variable = Dm.@new<datum_circuit_variable>("count", PORT_TYPE_NUMBER);
            circuit.circuit_variables["count"] = variable;
            circuit.modifiable_circuit_variables["count"] = variable;
        });
        var setter = Component<obj_item_circuit_component_variable_setter>(r);
        var getter = Component<obj_item_circuit_component_variable_getter>(r);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(setter))));
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(getter))));
            setter.variable_name.set_input("count");
            getter.variable_name.set_input("count");
            setter.input_port.set_input(42);
        });
        r.Step(2); // the plain setter writes on its input (only the trigger setter waits for a signal)
        Assert.Equal(42f, r.In(() => Dm.num((circuit.circuit_variables["count"] as datum_circuit_variable).value)));
        Assert.Equal(42f, r.In(() => Dm.num(getter.value.value)));

        // a delay passes its signal on after the time given, a clock processes while on
        r.In(() =>
        {
            delay.delay_amount.set_input(1); // seconds
            delay.trigger.set_input(COMPONENT_SIGNAL);
        });
        r.Step(10);
        Assert.Null(r.In(() => delay.output.value));
        r.Step(Second);
        Assert.NotNull(r.In(() => delay.output.value)); // a signal: the time it was sent (COMPONENT_SIGNAL)
        r.In(() => clock.on.set_input(TRUE));
        r.Step(2);
        Assert.True(r.In(() => Dm.@in(clock, SSclock_component.processing)));
        r.Step(Second);
        Assert.NotNull(r.In(() => clock.signal.value));
        r.In(() => clock.on.set_input(FALSE));
        r.Step(2);
        Assert.False(r.In(() => Dm.@in(clock, SSclock_component.processing)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACompactRemoteFiresItsCircuitInHandUntilTheCellIsEmpty()
    {
        var (r, feed, bob) = Room(10604);
        var remote = Place<obj_item_compact_remote>(r, 5, 4);
        var compactComponent = r.In(() => Dm.each<obj_item_circuit_component_compact_remote>(Dm.each<datum_component_shell>(remote.GetComponent(typeof(datum_component_shell)) is datum_component_shell s ? Dm.list(s) : Dm.list()).Single().unremovable_circuit_components).Single());
        var toggle = Component<obj_item_circuit_component_compare_toggle>(r);

        // the circuit with a nearly empty cell goes into the remote: the remote's own component joins it
        var circuit = Hold<obj_item_integrated_circuit>(r, bob);
        var cell = r.In(() =>
        {
            var c = Dm.@new<obj_item_stock_parts_power_store_cell>(circuit);
            circuit.set_cell(c);
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(toggle))));
            return c;
        });
        Use(r, bob, remote);
        r.In(() =>
        {
            Assert.Same(remote, circuit.loc);
            Assert.Same(remote, circuit.shell);
            Assert.True(Dm.truthy(circuit.on));
            Assert.Contains(compactComponent, Dm.each<obj_item_circuit_component>(circuit.attached_components));
            In(toggle, 1).connect(compactComponent.signal); // Toggle And Compare from the remote's signal
        });

        // used in hand: the balloon, the terminal typing sound, the user and the signal out, the toggle flips
        EmptyHands(r, bob);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(remote)));
        r.Step(Second);
        r.Command(() => ClientSeam.Keybinding(bob.client, 4, true)); // activate_inhand (Z): attack_self
        r.Step(4);
        Assert.True(feed.Any("clicked primary button"));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("terminal") == true || h.File?.Contains("keyboard") == true || h.File?.Contains("typing") == true);
        Assert.Same(bob, r.In(() => compactComponent.entity.value));
        Assert.Equal(1f, r.In(() => toggle.toggle_state));

        // every input draws the cell's energy; once it is empty the circuit stops
        r.In(() => cell.charge = toggle.energy_usage_per_input * 0.5f);
        r.Step(Second);
        r.Command(() => ClientSeam.Keybinding(bob.client, 4, true));
        r.Step(4);
        Assert.Equal(1f, r.In(() => toggle.toggle_state)); // no power: no flip
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheModuleDuplicatorScansACircuitAndPrintsACopy()
    {
        var (r, feed, bob) = Room(10605);
        FlickRecorder flicks = RecordFlicks(r);
        var silo = Place<obj_machinery_ore_silo>(r, 8, 6);
        var duplicator = Place<obj_machinery_module_duplicator>(r, 5, 4);
        r.In(() => duplicator.materials.silo = null);
        var multitool = Hold<obj_item_multitool>(r, bob);
        r.In(() => multitool.set_buffer(silo));
        Use(r, bob, duplicator);
        EmptyHands(r, bob);
        Hold<obj_item_stack_sheet_glass>(r, bob, 10);
        Use(r, bob, duplicator);
        EmptyHands(r, bob);
        Hold<obj_item_stack_sheet_iron>(r, bob, 10); // a circuit's copy also costs the circuit design's iron
        Use(r, bob, duplicator);
        EmptyHands(r, bob);

        var add = Component<obj_item_circuit_component_arithmetic>(r);
        var circuit = Hold<obj_item_integrated_circuit>(r, bob);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(add))));
            circuit.set_display_name("Adder");
        });
        Use(r, bob, duplicator);
        Assert.Contains("module-fab-scan", Flicks(flicks, duplicator));
        r.Step(2 * Second);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("ping") == true);

        // the window lists the scanned circuit; printing makes a copy with its component
        EmptyHands(r, bob);
        Take(bob);
        Use(r, bob, duplicator);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ComponentPrinter", InterfaceOf(payload));
        JsonElement design = payload.GetProperty("static_data").GetProperty("designs").EnumerateObject().Single().Value; // tg's ui_static_data
        Assert.Equal("Adder", design.GetProperty("name").GetString());
        Act(r, bob, window, "print", ("design_path", design.GetProperty("id").GetString()));
        Assert.Contains("module-fab-print", Flicks(flicks, duplicator));
        r.Step(2 * Second);
        r.In(() =>
        {
            var copy = Dm.each<obj_item_integrated_circuit>(duplicator.loc).Single(c => c != circuit);
            Assert.Equal("Adder", copy.display_name);
            Assert.IsType<obj_item_circuit_component_arithmetic>(Dm.each<obj_item_circuit_component>(copy.attached_components).Single());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCircuitMultitoolMarksAThingWithItsFlickSoundAndOverlay()
    {
        var (r, feed, bob) = Room(10606);
        FlickRecorder flicks = RecordFlicks(r);
        var marker = Hold<obj_item_multitool_circuit>(r, bob);
        var target = Place<obj_item_wrench>(r, 5, 4);
        Use(r, bob, target);
        r.In(() =>
        {
            Assert.Same(target, marker.marked_atom);
            Assert.Contains(Dm.each(marker.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)), s => s == "marked_overlay");
        });
        Assert.Contains("multitool_circuit_flick", Flicks(flicks, marker));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("compiler-stage2") == true);
        Assert.True(feed.Any("Marked"));

        // the mark goes into an entity port from the editor; using the multitool in hand clears it
        var tostring = Component<obj_item_circuit_component_tostring>(r);
        var circuit = Circuit(r, tostring);
        // the circuit in the other hand (tg's hands_state), the multitool still in the active one
        r.In(() => Waiting.NoSleep(bob.put_in_hands(circuit)));
        r.In(() => Assert.Same(marker, bob.get_active_held_item()));
        Take(bob);
        r.Command(() => Waiting.Detach(circuit.ui_interact(bob)));
        r.Step(4);
        LastUpdate(Take(bob), out string window);
        Act(r, bob, window, "set_component_input", ("component_id", 1), ("port_id", 1), ("marked_atom", 1));
        Assert.Same(target, r.In(() => In(tostring, 0).value));
        r.Step(Second);
        r.Command(() => ClientSeam.Keybinding(bob.client, 4, true));
        r.Step(4);
        Assert.Null(r.In(() => marker.marked_atom));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACircuitSavesToJsonAndLoadsBack()
    {
        var (r, _, _) = Room(10607);
        var add = Component<obj_item_circuit_component_arithmetic>(r);
        var text = Component<obj_item_circuit_component_tostring>(r);
        var circuit = Circuit(r, add, text);
        string json = r.In(() =>
        {
            circuit.set_display_name("Calculator");
            In(add, 1).set_input(6);
            In(text, 0).connect(Out(add, 0));
            add.rel_x = 120;
            var variable = Dm.@new<datum_circuit_variable>("speed", PORT_TYPE_NUMBER);
            circuit.circuit_variables["speed"] = variable;
            circuit.modifiable_circuit_variables["speed"] = variable;
            return Dm.str(circuit.convert_to_json());
        });
        Assert.Contains("Calculator", json);
        Assert.Contains("/obj/item/circuit_component/arithmetic", json);

        var loaded = r.In(() =>
        {
            var copy = Dm.@new<obj_item_integrated_circuit>(locate(6, 6, 1));
            DmList errors = Dm.list();
            Waiting.NoSleep(copy.load_circuit_data(json, errors));
            Assert.Equal(0f, Dm.length(errors));
            return copy;
        });
        r.In(() =>
        {
            Assert.Equal("Calculator", loaded.display_name);
            var components = Dm.each<obj_item_circuit_component>(loaded.attached_components).ToList();
            Assert.Equal(2, components.Count);
            var loadedAdd = Assert.IsType<obj_item_circuit_component_arithmetic>(components[0]);
            var loadedText = Assert.IsType<obj_item_circuit_component_tostring>(components[1]);
            Assert.Equal(120f, loadedAdd.rel_x);
            Assert.Equal(6f, Dm.num(In(loadedAdd, 1).value));
            Assert.Contains(Out(loadedAdd, 0), Dm.each<datum_port_output>(In(loadedText, 0).connected_ports));
            Assert.True(Dm.@in("speed", loaded.circuit_variables));
        });

        // a component the port has not translated is skipped with an error (DV569)
        string inert = json.Replace("/obj/item/circuit_component/tostring", "/obj/item/circuit_component/proccall");
        r.In(() =>
        {
            var copy = Dm.@new<obj_item_integrated_circuit>(locate(6, 6, 1));
            DmList errors = Dm.list();
            Waiting.NoSleep(copy.load_circuit_data(inert, errors));
            Assert.Contains(Dm.each(errors), e => Dm.str(e).Contains("not ported yet", StringComparison.Ordinal));
            Assert.Single(Dm.each<obj_item_circuit_component>(copy.attached_components));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryLiveCircuitTypeIsMadeAndDeletedCleanly()
    {
        var (r, _, _) = Room(10608);
        var types = r.In(() => Dm.each<Type>(typesof(typeof(obj_item_circuit_component))).Concat(Dm.each<Type>(typesof(typeof(obj_item_integrated_circuit))))
            .Concat(new[] { typeof(obj_item_compact_remote), typeof(obj_item_controller), typeof(obj_item_multitool_circuit), typeof(obj_item_usb_cable),
                typeof(obj_machinery_component_printer), typeof(obj_machinery_debug_component_printer), typeof(obj_machinery_module_duplicator) })
            .Cast<Type>()
            .Where(t => !InertSeam.IsInertType(t) && !t.IsAbstract)
            .ToList());
        Assert.True(types.Count > 60, types.Count + " live circuit types");
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

    // tg: code/modules/unit_tests/circuit_component_category.dm (tg's loop walks typepaths with a typed var and checks nothing: the
    // live components' designs are checked here; tg's data leaves two components Unassigned, the trigger variable setter and the
    // equipment action, both live and skipped here as tg has them)
    [Fact]
    public void EveryComponentDesignHasACategory()
    {
        var (r, _, _) = Room(10609);
        r.In(() =>
        {
            int checkedDesigns = 0;
            foreach (Type design in Dm.each<Type>(subtypesof(typeof(datum_design_component))))
            {
                Type path = Dm.initial(design, (datum_design d) => d.build_path) as Type;
                if (path == null || InertSeam.IsInertType(path) || path == typeof(obj_item_circuit_component_variable_setter_trigger)
                    || path == typeof(obj_item_circuit_component_equipment_action))
                    continue;
                checkedDesigns++;
                Assert.False(Dm.eq(Dm.initial(path, (obj_item_circuit_component c) => c.category), COMPONENT_DEFAULT_CATEGORY),
                    $"{path.Name} has a category of '{COMPONENT_DEFAULT_CATEGORY}' when it has a research design that players can potentially access!");
            }
            Assert.True(checkedDesigns > 40, "checked " + checkedDesigns);
        });
    }
}
