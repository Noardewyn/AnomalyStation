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
/// The USB port in a booted toy room with Bob as a player: Bob plugs a USB cable from a bot shell's circuit into a machine, which adds
/// the machine's own circuit components to the circuit, draws the cable's beam and drops the cable into the circuit; through them the
/// circuit drives the machine (a pressure pump switched on and set, the air alarm's mode set, the fire alarm pulled and reset, the
/// conveyor switch switched, the evac status display written) and reads it back; the cable snaps when the shell is carried away and
/// the machine's components leave the circuit. A computer is a circuit shell with its programs' components; every newly live type is
/// made and deleted.
/// </summary>
public class UsbPortTests
{
    /// <summary>A bot shell at (x, y) with an empty circuit, and a USB cable Bob plugs from it into the machine.</summary>
    private static (obj_structure_bot Bot, obj_item_integrated_circuit Circuit, obj_item_usb_cable Cable) PlugInto(AudioRoom r, mob_living_carbon_human bob, atom machine, int x, int y, ulong seed)
    {
        var (bot, circuit) = CircuitTestRig.InBot(r, x, y);
        var cable = r.In(() => Dm.@new<obj_item_usb_cable>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(cable)));
        // the cable's 1 in 100 "wrong way" fumble
        r.World.Rng.Reseed(seed, 1);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(cable.pre_attack(bot, bob)))));
        r.In(() => Assert.Same(circuit, cable.attached_circuit));
        r.World.Rng.Reseed(seed, 1);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(cable.pre_attack(machine, bob)))));
        r.Step(2);
        r.In(() => Assert.Same(circuit, cable.loc));
        return (bot, circuit, cable);
    }

    private static T Attached<T>(AudioRoom r, obj_item_integrated_circuit circuit) where T : obj_item_circuit_component =>
        r.In(() => Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<T>().FirstOrDefault())
        ?? throw new Xunit.Sdk.XunitException(typeof(T).Name + " is not in the circuit; it holds " +
            r.In(() => string.Join(", ", Dm.each<obj_item_circuit_component>(circuit.attached_components).Select(c => c.GetType().Name))));

    private static datum_component_usb_port PortOf(AudioRoom r, atom machine) => r.In(() => machine.GetComponent(typeof(datum_component_usb_port)) as datum_component_usb_port);

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_component_usb_port), typeof(obj_item_circuit_component_atmos_pump), typeof(obj_item_circuit_component_atmos_volume_pump),
            typeof(obj_item_circuit_component_digital_valve), typeof(obj_item_circuit_component_atmos_meter), typeof(obj_item_circuit_component_air_alarm),
            typeof(obj_item_circuit_component_air_alarm_general), typeof(obj_item_circuit_component_air_alarm_scrubbers), typeof(obj_item_circuit_component_air_alarm_vents),
            typeof(obj_item_circuit_component_firealarm), typeof(obj_item_circuit_component_light_switch), typeof(obj_item_circuit_component_status_display),
            typeof(obj_item_circuit_component_conveyor_switch), typeof(obj_item_circuit_component_vitals_monitor), typeof(obj_item_circuit_component_reflector),
            typeof(obj_item_circuit_component_holo_signboard), typeof(obj_item_circuit_component_bluespace_launchpad), typeof(obj_item_circuit_component_medical_console_data),
            typeof(obj_item_circuit_component_teleporter_control_console), typeof(obj_item_circuit_component_arrest_console_data), typeof(obj_item_circuit_component_arrest_console_arrest),
            typeof(obj_item_circuit_component_advanced_camera), typeof(obj_item_circuit_component_advanced_camera_intercept), typeof(obj_item_circuit_component_hydroponics),
            typeof(obj_item_circuit_component_slime_processor), typeof(obj_item_circuit_component_synth), typeof(obj_item_circuit_component_emitter),
            typeof(obj_item_circuit_component_atmos_temperature_pump), typeof(obj_item_circuit_component_thermomachine), typeof(obj_item_circuit_component_modpc),
            typeof(obj_item_circuit_component_mod_program_status), typeof(obj_item_circuit_component_mod_program_nt_pay), typeof(obj_item_circuit_component_mod_program_signaler),
            typeof(obj_item_circuit_component_mod_program_messenger), typeof(obj_item_circuit_component_mod_program_notepad), typeof(obj_item_circuit_component_mod_program_radar),
            typeof(obj_item_circuit_component_mod_program_radar_medical), typeof(obj_item_circuit_component_mod_program_radar_janitor), typeof(obj_item_circuit_component_mod_program_radar_nukie),
            typeof(obj_item_circuit_component_mod_program_ntnetmonitor), typeof(obj_item_circuit_component_mod_program_borg_monitor),
            typeof(obj_item_circuit_component_mod_program_modsuit_control), typeof(obj_item_circuit_component_mod_program_spectre_meter),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ACableFromACircuitTurnsAPumpOnAndSetsItsPressure()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10801);
        var pump = Place<obj_machinery_atmospherics_components_binary_pump>(r, 5, 5);
        var (bot, circuit, cable) = PlugInto(r, bob, pump, 5, 4, 10801);
        Assert.True(feed.Any("balloon: connected to machine"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        var component = Attached<obj_item_circuit_component_atmos_pump>(r, circuit);
        r.In(() =>
        {
            datum_component_usb_port port = pump.GetComponent(typeof(datum_component_usb_port)) as datum_component_usb_port;
            Assert.Same(circuit, port.attached_circuit);
            Assert.NotNull(port.usb_cable_beam); // the cable's beam from the shell to the pump
            Assert.Same(pump, component.connected_pump);
        });

        r.In(() => component.pressure_value.set_input(250));
        r.Step(2);
        r.In(() => component.on.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(pump.on));
            Assert.Equal(250f, pump.target_pressure);
            Assert.True(Dm.truthy(component.is_active.value));
        });
        r.In(() => component.off.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(pump.on));
            Assert.False(Dm.truthy(component.is_active.value));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCableSnapsWhenTheShellIsCarriedAway()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10802);
        var pump = Place<obj_machinery_atmospherics_components_binary_pump>(r, 5, 5);
        var (bot, circuit, cable) = PlugInto(r, bob, pump, 5, 4, 10802);
        Attached<obj_item_circuit_component_atmos_pump>(r, circuit);

        // one tile away is in range; three tiles is past the cable's two
        r.In(() => Waiting.NoSleep(bot.forceMove(locate(6, 4, 1))));
        r.Step(2);
        Assert.Same(circuit, r.In(() => PortOf(r, pump).attached_circuit));
        r.In(() => Waiting.NoSleep(bot.forceMove(locate(10, 4, 1))));
        r.Step(2);
        r.In(() =>
        {
            datum_component_usb_port port = PortOf(r, pump);
            Assert.Null(port.attached_circuit);
            Assert.Null(port.usb_cable_beam);
            Assert.DoesNotContain(Dm.each<obj_item_circuit_component>(circuit.attached_components), c => c is obj_item_circuit_component_atmos_pump);
            Assert.Same(pump.drop_location(), cable.loc); // the cable falls at the machine
        });
        Assert.True(feed.Any("balloon: *snap*"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACableWithoutAShellIsRefused()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10803);
        var pump = Place<obj_machinery_atmospherics_components_binary_pump>(r, 5, 5);
        var cable = r.In(() => Dm.@new<obj_item_usb_cable>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(cable)));
        r.World.Rng.Reseed(10803, 1);
        r.In(() => Waiting.NoSleep(cable.pre_attack(pump, bob)));
        Assert.True(feed.Any("balloon: connect to a shell first"));
        Assert.Null(r.In(() => PortOf(r, pump).attached_circuit));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAirAlarmsModeIsSetFromACircuit()
    {
        var (r, _, bob) = CircuitTestRig.Room(10804);
        var alarm = Place<obj_machinery_airalarm>(r, 5, 6);
        r.In(() => alarm.locked = FALSE);
        var (_, circuit, _) = PlugInto(r, bob, alarm, 5, 4, 10804);
        var general = Attached<obj_item_circuit_component_air_alarm_general>(r, circuit);
        Attached<obj_item_circuit_component_air_alarm>(r, circuit);
        Attached<obj_item_circuit_component_air_alarm_scrubbers>(r, circuit);
        Attached<obj_item_circuit_component_air_alarm_vents>(r, circuit);
        string before = r.In(() => Dm.str(general.current_mode.value));
        Assert.Equal(r.In(() => Dm.str(alarm.selected_mode.name)), before);

        r.In(() => general.mode.set_input("Siphon"));
        r.Step(2);
        r.In(() => general.set_mode.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_air_alarm_mode_siphon>(alarm.selected_mode);
            Assert.Equal("Siphon", Dm.str(general.current_mode.value));
        });

        // the alarm raised from the circuit
        r.In(() => general.enable_fire_alarm.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(AIR_ALARM_ALERT_HAZARD, alarm.danger_level);
            Assert.True(Dm.truthy(general.fire_alarm_enabled.value));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFireAlarmIsPulledAndResetFromACircuit()
    {
        var (r, _, bob) = CircuitTestRig.Room(10805);
        var firealarm = Place<obj_machinery_firealarm>(r, 6, 6);
        var (_, circuit, _) = PlugInto(r, bob, firealarm, 5, 5, 10805);
        var component = Attached<obj_item_circuit_component_firealarm>(r, circuit);

        r.In(() => component.alarm_trigger.set_input(COMPONENT_SIGNAL));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal(AREA_FAULT_MANUAL, firealarm.my_area.fault_status);
            Assert.Equal(1f, Dm.num(component.is_on.value));
            Assert.NotNull(component.triggered.value);
        });
        r.In(() => component.reset_trigger.set_input(COMPONENT_SIGNAL));
        r.Step(4);
        r.In(() => Assert.Equal(0f, Dm.num(component.is_on.value)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheConveyorSwitchIsSwitchedFromACircuit()
    {
        var (r, _, bob) = CircuitTestRig.Room(10806);
        var conveyorSwitch = r.In(() => Dm.@new<obj_machinery_conveyor_switch>(locate(6, 5, 1), "usb_belt"));
        var belt = r.In(() => Dm.@new<obj_machinery_conveyor>(locate(7, 6, 1), SOUTH, "usb_belt"));
        r.Step(2);
        var (_, circuit, _) = PlugInto(r, bob, conveyorSwitch, 5, 5, 10806);
        var component = Attached<obj_item_circuit_component_conveyor_switch>(r, circuit);
        r.In(() => Assert.NotNull(component.reverse)); // a two-way switch gets the reverse input

        r.In(() => component.active.set_input(COMPONENT_SIGNAL));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal((float)obj_machinery_conveyor.CONVEYOR_FORWARD, Dm.num(conveyorSwitch.position));
            Assert.Equal((float)obj_machinery_conveyor.CONVEYOR_FORWARD, Dm.num(component.direction.value));
            Assert.True(Dm.truthy(belt.operating)); // the linked belt runs
        });
        r.In(() => component.reverse.set_input(COMPONENT_SIGNAL));
        r.Step(4);
        r.In(() => Assert.Equal((float)obj_machinery_conveyor.CONVEYOR_BACKWARDS, Dm.num(conveyorSwitch.position)));
        r.In(() => component.stop.set_input(COMPONENT_SIGNAL));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal((float)obj_machinery_conveyor.CONVEYOR_OFF, Dm.num(conveyorSwitch.position));
            Assert.Equal(0f, Dm.num(component.direction.value));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEvacDisplayIsWrittenFromACircuit()
    {
        var (r, _, bob) = CircuitTestRig.Room(10807);
        var display = Place<obj_machinery_status_display_evac>(r, 6, 6);
        var (_, circuit, _) = PlugInto(r, bob, display, 5, 5, 10807);
        var component = Attached<obj_item_circuit_component_status_display>(r, circuit);

        r.In(() =>
        {
            component.command.set_input("Message");
            component.message1.set_input("HELLO");
            component.message2.set_input("CREW");
        });
        r.Step(2);
        CircuitTestRig.Fire(r, component);
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal("HELLO", display.message1);
            Assert.Equal("CREW", display.message2);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APdaIsAShellWithItsProgramsComponents()
    {
        var (r, _, _) = CircuitTestRig.Room(10808);
        var pda = Place<obj_item_modular_computer_pda>(r, 5, 4);
        Assert.NotNull(CircuitTestRig.ShellOf(r, pda));
        var circuit = CircuitTestRig.Circuit(r);
        CircuitTestRig.Attach(r, pda, circuit);
        r.In(() =>
        {
            // the computer's own component and one for each installed program that has one
            Assert.Contains(Dm.each<obj_item_circuit_component>(circuit.attached_components), c => c is obj_item_circuit_component_modpc);
            var programs = Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_mod_program>().ToList();
            Assert.NotEmpty(programs);
            Assert.All(programs, p => Assert.IsAssignableFrom<datum_computer_file_program>(p.associated_program));
            Assert.Contains(programs, p => p is obj_item_circuit_component_mod_program_messenger);
        });

        // the notepad's component writes the note
        var notepad = r.In(() => Dm.each<obj_item_circuit_component>(circuit.attached_components).OfType<obj_item_circuit_component_mod_program_notepad>().Single());
        r.In(() =>
        {
            Waiting.NoSleep(pda.turn_on(null, FALSE));
            Waiting.NoSleep(pda.open_program(program: notepad.associated_program as datum_computer_file_program, open_ui: FALSE));
        });
        r.Step(2);
        r.In(() => notepad.set_text.set_input("Buy more glass")); // its only input, which triggers it
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("Buy more glass", (notepad.associated_program as datum_computer_file_program_notepad).written_note);
            Assert.Equal("Buy more glass", Dm.str(notepad.updated_text.value));
        });
        r.AssertNoRuntimes();
    }

    /// <summary>The live types tg defines in the USB components' and the USB port's files, and the machines' circuit components.</summary>
    public static List<Type> NewlyLiveTypes()
    {
        string[] files =
        {
            "code/modules/wiremod/components/usb/", "code/datums/components/usb_port.dm", "code/modules/wiremod/components/bci/",
            "code/modules/wiremod/shell/brain_computer_interface.dm", "code/modules/wiremod/shell/implant.dm",
            "code/modules/modular_computers/computers/item/computer_circuit.dm", "code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm",
            "code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm",
        };
        return Tools.Types.TypesIndex.Index.Types
            .Where(t => t.State == "live" && (files.Any(f => t.Location.StartsWith(f, StringComparison.Ordinal)) || t.Path.StartsWith("/obj/item/circuit_component/", StringComparison.Ordinal)))
            .Select(t => typeof(atom).Assembly.GetType(typeof(atom).Namespace + "." + t.Class))
            .Where(t => t != null && typeof(atom).IsAssignableFrom(t) && !t.IsAbstract && !InertSeam.IsInertType(t))
            .Distinct()
            .ToList();
    }

    [Fact]
    public void EveryNewlyLiveTypeIsMadeAndDeletedCleanly()
    {
        var (r, _, _) = CircuitTestRig.Room(10809);
        List<Type> types = NewlyLiveTypes();
        Assert.Contains(typeof(obj_machinery_bci_implanter), types);
        Assert.Contains(typeof(obj_item_organ_cyberimp_bci), types);
        Assert.Contains(typeof(obj_item_implant_circuit), types);
        Assert.Contains(typeof(obj_item_circuit_component_mod_program_messenger), types);
        foreach (Type type in types)
        {
            atom made = r.In(() => Dm.@new(type, locate(6, 6, 1)) as atom);
            r.Step(2);
            Assert.True(made != null, type.Name);
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }
}
