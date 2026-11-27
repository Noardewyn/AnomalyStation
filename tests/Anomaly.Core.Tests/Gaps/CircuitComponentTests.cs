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
/// The circuits' acting and sensing components in a booted toy room with Bob as a player, each in a circuit put into a bot shell: the
/// speech component's line reaches Bob; the hear component hears Bob; a radio component's signal reaches another on its frequency and
/// code; the GPS, pinpointer and direction components read coordinates; the health and species components read Bob; the ID
/// components read a card's owner and accesses, and the access checker compares them; an NTNet send reaches a receiver with the same
/// key, and a wirenet send reaches a receiver on the same cable; the sensors read the room's air; the light lights its shell; the sound
/// emitter plays; the laser pointer flashes its dot on the target's tile; the admin components read and write a var.
/// </summary>
public class CircuitComponentTests
{
    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_circuit_component_speech), typeof(obj_item_circuit_component_soundemitter), typeof(obj_item_circuit_component_light),
            typeof(obj_item_circuit_component_laserpointer), typeof(obj_item_circuit_component_pull), typeof(obj_item_circuit_component_radio),
            typeof(obj_item_circuit_component_pathfind), typeof(obj_item_circuit_component_camera), typeof(obj_item_circuit_component_equipment_action),
            typeof(obj_item_circuit_component_mmi), typeof(obj_item_circuit_component_direction), typeof(obj_item_circuit_component_gps),
            typeof(obj_item_circuit_component_health), typeof(obj_item_circuit_component_compare_health_state), typeof(obj_item_circuit_component_hear),
            typeof(obj_item_circuit_component_matscanner), typeof(obj_item_circuit_component_pinpointer), typeof(obj_item_circuit_component_reagentscanner),
            typeof(obj_item_circuit_component_remotecam_drone), typeof(obj_item_circuit_component_remotecam_airlock), typeof(obj_item_circuit_component_remotecam_polaroid),
            typeof(obj_item_circuit_component_self), typeof(obj_item_circuit_component_species), typeof(obj_item_circuit_component_compare_access),
            typeof(obj_item_circuit_component_id_access_reader), typeof(obj_item_circuit_component_id_getter), typeof(obj_item_circuit_component_id_info_reader),
            typeof(obj_item_circuit_component_ntnet_receive), typeof(obj_item_circuit_component_ntnet_send), typeof(obj_item_circuit_component_list_literal_ntnet_send),
            typeof(obj_item_circuit_component_pressuresensor), typeof(obj_item_circuit_component_tempsensor), typeof(obj_item_circuit_component_view_sensor),
            typeof(obj_item_circuit_component_wirenet_receive), typeof(obj_item_circuit_component_wirenet_send), typeof(obj_item_circuit_component_list_literal_wirenet_send),
            typeof(obj_item_circuit_component_get_variable), typeof(obj_item_circuit_component_set_variable), typeof(obj_item_circuit_component_spawn_atom),
            typeof(obj_item_circuit_component_to_type), typeof(obj_item_circuit_component_save_shell), typeof(obj_item_circuit_component_input_request),
            typeof(obj_item_circuit_component_begin_animation), typeof(obj_item_circuit_component_filter_adder), typeof(obj_item_circuit_component_signal_handler),
            typeof(datum_component_boris_circuit_container), typeof(datum_component_shuttle_move_deferred_checks),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheSpeechComponentSaysItsMessageToAJoinedPlayer()
    {
        var (r, feed, bob) = CircuitTestRig.Room(10701);
        var speech = CircuitTestRig.Component<obj_item_circuit_component_speech>(r);
        var (bot, _) = CircuitTestRig.InBot(r, 5, 4, speech);
        r.In(() => speech.message.set_input("Hello, crew."));
        CircuitTestRig.Fire(r, speech);
        r.Step(4);
        Assert.Contains(feed.Chat, c => ReferenceEquals(c.Target, bob) && c.Html.Contains("Hello, crew.", StringComparison.Ordinal));

        // the shell's cooldown: a second line right away is not said
        r.In(() => speech.message.set_input("Again."));
        CircuitTestRig.Fire(r, speech);
        r.Step(2);
        Assert.False(feed.Any("Again."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHearComponentHearsBobsLine()
    {
        var (r, _, bob) = CircuitTestRig.Room(10702);
        var hear = CircuitTestRig.Component<obj_item_circuit_component_hear>(r);
        CircuitTestRig.InBot(r, 5, 4, hear);
        r.In(() => Waiting.NoSleep(bob.say("open the pod bay doors")));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal("Open the pod bay doors", Dm.str(hear.message_port.value)); // as say() treats it
            Assert.Same(bob, hear.speaker_port.value);
            Assert.Equal(Dm.str(bob.get_voice()), Dm.str(hear.speaker_name.value));
            Assert.Equal("Galactic Common", Dm.str(hear.language_port.value));
            Assert.NotNull(hear.trigger_port.value);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARadioComponentsSignalReachesAnotherOnTheSameFrequencyAndCode()
    {
        var (r, _, _) = CircuitTestRig.Room(10703);
        var sender = CircuitTestRig.Component<obj_item_circuit_component_radio>(r);
        var receiver = CircuitTestRig.Component<obj_item_circuit_component_radio>(r);
        var other = CircuitTestRig.Component<obj_item_circuit_component_radio>(r);
        CircuitTestRig.InBot(r, 3, 6, sender);
        CircuitTestRig.InBot(r, 10, 6, receiver);
        CircuitTestRig.InBot(r, 10, 2, other);
        r.In(() => other.code.set_input(7)); // another code on the same frequency
        r.Step(2);
        CircuitTestRig.Fire(r, sender);
        r.Step(4);
        r.In(() =>
        {
            Assert.NotNull(receiver.trigger_output.value);
            Assert.Null(other.trigger_output.value);
            Assert.Null(sender.trigger_output.value); // a radio does not hear itself
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGpsPinpointerAndDirectionComponentsReadCoordinates()
    {
        var (r, _, bob) = CircuitTestRig.Room(10704);
        var gps = CircuitTestRig.Component<obj_item_circuit_component_gps>(r);
        var pinpointer = CircuitTestRig.Component<obj_item_circuit_component_pinpointer>(r);
        var direction = CircuitTestRig.Component<obj_item_circuit_component_direction>(r);
        CircuitTestRig.InBot(r, 7, 4, gps, pinpointer, direction);
        CircuitTestRig.Fire(r, gps);
        r.In(() =>
        {
            pinpointer.target.set_input(bob);
            direction.input_port.set_input(bob);
        });
        CircuitTestRig.Fire(r, pinpointer);
        CircuitTestRig.Fire(r, direction);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal((7f, 4f, 1f), (Dm.num(gps.x_pos.value), Dm.num(gps.y_pos.value), Dm.num(gps.z_pos.value)));
            Assert.Equal((4f, 4f, 1f), (Dm.num(pinpointer.x_pos.value), Dm.num(pinpointer.y_pos.value), Dm.num(pinpointer.z_pos.value)));
            Assert.Equal("west", Dm.str(direction.output.value));
            Assert.Equal(3f, Dm.num(direction.distance.value));
            Assert.NotNull(direction.west.value);
            Assert.Null(direction.east.value);
        });

        // out of the pinpointer's range: no coordinates and its Failed signal
        r.In(() => pinpointer.target.set_input(Dm.@new<obj_item_toy_crayon_red>(locate(1, 1, 1))));
        r.In(() => pinpointer.max_range = 2);
        CircuitTestRig.Fire(r, pinpointer);
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(pinpointer.x_pos.value);
            Assert.NotNull(pinpointer.on_error.value);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHealthAndSpeciesComponentsReadBob()
    {
        var (r, _, bob) = CircuitTestRig.Room(10705);
        var health = CircuitTestRig.Component<obj_item_circuit_component_health>(r);
        var species = CircuitTestRig.Component<obj_item_circuit_component_species>(r);
        CircuitTestRig.InBot(r, 5, 4, health, species);
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_brute_loss(12));
            health.input_port.set_input(bob);
            species.input_port.set_input(bob);
        });
        CircuitTestRig.Fire(r, health);
        CircuitTestRig.Fire(r, species);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(12f, Dm.num(health.brute.value));
            Assert.Equal(0f, Dm.num(health.burn.value));
            Assert.Equal(bob.health, Dm.num(health.health.value));
            Assert.EndsWith("Human", Dm.str(species.output.value)); // tg's "\improper Human"
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIdComponentsReadACardAndTheAccessCheckerComparesItsAccesses()
    {
        var (r, _, bob) = CircuitTestRig.Room(10706);
        var info = CircuitTestRig.Component<obj_item_circuit_component_id_info_reader>(r);
        var reader = CircuitTestRig.Component<obj_item_circuit_component_id_access_reader>(r);
        var getter = CircuitTestRig.Component<obj_item_circuit_component_id_getter>(r);
        var checker = CircuitTestRig.Component<obj_item_circuit_component_compare_access>(r);
        CircuitTestRig.InBot(r, 5, 4, info, reader, getter, checker);
        var card = r.In(() =>
        {
            var c = Dm.@new<obj_item_card_id_advanced>(bob.loc);
            c.registered_name = "Bob";
            c.assignment = "Scientist";
            c.registered_age = 31;
            c.access = Dm.list(ACCESS_RESEARCH, ACCESS_SCIENCE);
            Waiting.NoSleep(bob.put_in_hands(c));
            return c;
        });
        r.Step(2);
        r.In(() =>
        {
            getter.target.set_input(bob);
            getter.prioritize_hands.set_input(TRUE);
        });
        CircuitTestRig.Fire(r, getter);
        r.Step(2);
        Assert.Same(card, r.In(() => getter.id_port.value));

        r.In(() =>
        {
            info.target.set_input(card);
            reader.target.set_input(card);
        });
        CircuitTestRig.Fire(r, info);
        CircuitTestRig.Fire(r, reader);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(("Bob", "Scientist", 31f), (Dm.str(info.name_port.value), Dm.str(info.rank_port.value), Dm.num(info.age_port.value)));
            Assert.Equal(new[] { ACCESS_RESEARCH, ACCESS_SCIENCE }.OrderBy(a => a), Dm.each<string>(reader.access_port.value).OrderBy(a => a));
        });

        // the checker: its window sets the required accesses (all of them, or one); the card's accesses are checked against them
        r.In(() => Waiting.NoSleep(checker.ui_perform_action(bob, "access")));
        r.Step(4);
        var payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("CircuitAccessChecker", InterfaceOf(payload));
        Assert.True(payload.GetProperty("static_data").GetProperty("regions").GetArrayLength() > 3);
        Act(r, bob, window, "set", ("access", ACCESS_RESEARCH));
        Act(r, bob, window, "set", ("access", ACCESS_ROBOTICS));
        r.In(() =>
        {
            Assert.Equal(new[] { ACCESS_RESEARCH, ACCESS_ROBOTICS }, Dm.each<string>(checker.required_accesses.value).ToArray());
            checker.subject_accesses.set_input(reader.access_port.value);
            checker.compare.set_input(COMPONENT_SIGNAL);
        });
        r.Step(2);
        Assert.Equal(0f, r.In(() => Dm.num(checker.result.value))); // not all of them
        Act(r, bob, window, "one_access");
        r.In(() => checker.compare.set_input(COMPONENT_SIGNAL));
        r.Step(2);
        Assert.Equal(1f, r.In(() => Dm.num(checker.result.value))); // one of them
        Act(r, bob, window, "clear_all");
        Assert.Empty(r.In(() => Dm.each(checker.required_accesses.value).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnNtnetPackageReachesAReceiverWithTheSameKey()
    {
        var (r, _, _) = CircuitTestRig.Room(10707);
        Place<obj_machinery_ntnet_relay>(r, 11, 4); // NTNet needs a working relay on the station
        var send = CircuitTestRig.Component<obj_item_circuit_component_ntnet_send>(r);
        var receive = CircuitTestRig.Component<obj_item_circuit_component_ntnet_receive>(r);
        var stranger = CircuitTestRig.Component<obj_item_circuit_component_ntnet_receive>(r);
        CircuitTestRig.InBot(r, 3, 6, send);
        CircuitTestRig.InBot(r, 10, 6, receive);
        CircuitTestRig.InBot(r, 10, 2, stranger);
        r.In(() =>
        {
            send.enc_key.set_input("secret");
            receive.enc_key.set_input("secret");
            stranger.enc_key.set_input("other");
            send.data_package.set_input(Dm.list("alpha", 2));
        });
        r.Step(2);
        CircuitTestRig.Fire(r, send);
        r.Step(4);
        r.In(() =>
        {
            // the receiver's list type is its first option, a list of strings: the number arrives as text, as in tg
            Assert.Equal(new[] { "alpha", "2" }, Dm.each(receive.data_package.value).Select(Dm.str).ToArray());
            Assert.NotNull(receive.trigger_output.value);
            Assert.Empty(Dm.each(stranger.data_package.value)); // another key: nothing arrives
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWirenetPackageReachesAReceiverOnTheSameCable()
    {
        // a cable laid with the map (its powernet is made at the round's start)
        var (r, _, _) = CircuitTestRig.Room(10708, mapExtras: w =>
        {
            for (int x = 3; x <= 9; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, 6, 1));
                Dm.@new<obj_structure_cable>(locate(x, 6, 1));
            }
        });
        var send = CircuitTestRig.Component<obj_item_circuit_component_wirenet_send>(r);
        var receive = CircuitTestRig.Component<obj_item_circuit_component_wirenet_receive>(r);
        var unwired = CircuitTestRig.Component<obj_item_circuit_component_wirenet_receive>(r);
        var (sendBot, _) = CircuitTestRig.InBot(r, 3, 6, send);
        var (receiveBot, _) = CircuitTestRig.InBot(r, 9, 6, receive);
        CircuitTestRig.InBot(r, 9, 2, unwired); // no cable under it, not anchored (anchored, tg's wirenet link runtimes without a cable)
        // a wirenet component links to the cable under its shell once the shell is anchored
        r.In(() =>
        {
            Waiting.NoSleep(sendBot.set_anchored(TRUE));
            Waiting.NoSleep(receiveBot.set_anchored(TRUE));
        });
        r.In(() => send.data_package.set_input(Dm.list("ping")));
        r.Step(2);
        CircuitTestRig.Fire(r, send);
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal(new[] { "ping" }, Dm.each(receive.data_package.value).Select(Dm.str).ToArray());
            Assert.NotNull(receive.trigger_output.value);
            Assert.Empty(Dm.each(unwired.data_package.value));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSensorsReadTheRoomsAir()
    {
        var (r, _, _) = CircuitTestRig.Room(10709);
        var temperature = CircuitTestRig.Component<obj_item_circuit_component_tempsensor>(r);
        var pressure = CircuitTestRig.Component<obj_item_circuit_component_pressuresensor>(r);
        CircuitTestRig.InBot(r, 5, 4, temperature, pressure);
        CircuitTestRig.Fire(r, temperature);
        CircuitTestRig.Fire(r, pressure);
        r.Step(2);
        r.In(() =>
        {
            var air = (datum_gas_mixture)Waiting.NoSleep(locate(5, 4, 1).return_air());
            Assert.Equal(round(air.temperature, 1), Dm.num(temperature.result.value));
            Assert.Equal(round(Dm.num(air.return_pressure()), 1), Dm.num(pressure.result.value));
            Assert.True(Dm.num(pressure.result.value) > 90, "pressure " + pressure.result.value);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLightComponentLightsItsShellAndTheSoundEmitterPlays()
    {
        var (r, _, _) = CircuitTestRig.Room(10710);
        var light = CircuitTestRig.Component<obj_item_circuit_component_light>(r);
        var emitter = CircuitTestRig.Component<obj_item_circuit_component_soundemitter>(r);
        var (bot, _) = CircuitTestRig.InBot(r, 5, 4, light, emitter);
        r.In(() =>
        {
            light.red.set_input(255);
            light.green.set_input(0);
            light.blue.set_input(0);
            light.brightness.set_input(3);
            light.on.set_input(TRUE); // the light has no trigger port: each input runs it
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bot.light_on));
            Assert.Equal(3f, Dm.num(bot.light_power));
            Assert.StartsWith("#", Dm.str(bot.light_color));
            Assert.NotEqual("#ffffff", Dm.str(bot.light_color));
        });

        int before = r.Sounds.Heard.Count;
        r.In(() => emitter.sound_file.set_input("Buzz"));
        CircuitTestRig.Fire(r, emitter);
        r.Step(2);
        Assert.Contains(r.Sounds.Heard.Skip(before), h => h.File?.Contains("buzz", StringComparison.OrdinalIgnoreCase) == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLaserPointerFlashesItsDotOnTheTarget()
    {
        var (r, _, _) = CircuitTestRig.Room(10713);
        var laser = CircuitTestRig.Component<obj_item_circuit_component_laserpointer>(r);
        CircuitTestRig.InBot(r, 5, 4, laser);
        var crayon = Place<obj_item_toy_crayon_red>(r, 7, 4);
        r.In(() =>
        {
            laser.lasercolour_option.set_input("green");
            laser.target_input.set_input(crayon);
        });
        CircuitTestRig.Fire(r, laser);
        r.Step(1);
        List<string> Dots() => r.In(() => Dm.each(locate(7, 4, 1).vis_contents).OfType<atom_movable_flick_visual>()
            .Select(v => (v.appearance as DmAppearance)?.icon_state ?? (v.appearance as image)?.icon_state).ToList());
        Assert.Equal(new[] { "green_laser" }, Dots());
        r.Step(2 * Second); // tg's one second
        Assert.Empty(Dots());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdminVarComponentsReadAndWriteAVar()
    {
        var (r, _, bob) = CircuitTestRig.Room(10711);
        var getvar = CircuitTestRig.Component<obj_item_circuit_component_get_variable>(r);
        var setvar = CircuitTestRig.Component<obj_item_circuit_component_set_variable>(r);
        var circuit = CircuitTestRig.Circuit(r, getvar, setvar);
        r.In(() =>
        {
            circuit.admin_only = TRUE;
            getvar.entity.set_input(bob);
            getvar.variable_name.set_input("real_name");
            setvar.entity.set_input(bob);
            setvar.variable_name.set_input("desc");
            setvar.new_value.set_input("A tester.");
        });
        CircuitTestRig.Fire(r, getvar);
        CircuitTestRig.Fire(r, setvar);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(bob.real_name, Dm.str(getvar.output_value.value));
            Assert.Equal("A tester.", Dm.str(bob.desc));
        });
        r.AssertNoRuntimes();
    }
}

/// <summary>The circuit tests' room, components, circuits and shells.</summary>
internal static class CircuitTestRig
{
    public static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13, int height = 9, Action<World> mapExtras = null)
    {
        var r = new AudioRoom(width, height, seed: seed, mapExtras: mapExtras);
        // the toy room has no APC: free power
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    public static T Component<T>(AudioRoom r) where T : obj_item_circuit_component => r.In(() => Dm.@new<T>(locate(8, 4, 1)));

    /// <summary>A circuit at the tile with a high capacity cell, switched on, with these components.</summary>
    public static obj_item_integrated_circuit Circuit(AudioRoom r, params obj_item_circuit_component[] components) => r.In(() =>
    {
        var circuit = Dm.@new<obj_item_integrated_circuit>(locate(8, 4, 1));
        var cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit);
        circuit.set_cell(cell);
        foreach (obj_item_circuit_component component in components)
            Assert.True(Dm.truthy(Waiting.NoSleep(circuit.add_component(component))), component.GetType().Name + " was not added");
        circuit.set_on(TRUE);
        return circuit;
    });

    public static datum_component_shell ShellOf(AudioRoom r, atom shell) => r.In(() => shell.GetComponent(typeof(datum_component_shell)) as datum_component_shell);

    /// <summary>Puts the circuit into the shell, as a click with it does.</summary>
    public static void Attach(AudioRoom r, atom shell, obj_item_integrated_circuit circuit)
    {
        datum_component_shell component = ShellOf(r, shell);
        Assert.NotNull(component);
        r.In(() => Waiting.NoSleep(component.attach_circuit(circuit)));
        r.Step(2);
        r.In(() => Assert.Same(shell, circuit.shell));
    }

    /// <summary>A bot shell at the tile with a circuit of these components.</summary>
    public static (obj_structure_bot Bot, obj_item_integrated_circuit Circuit) InBot(AudioRoom r, int x, int y, params obj_item_circuit_component[] components)
    {
        var bot = Place<obj_structure_bot>(r, x, y);
        var circuit = Circuit(r, components);
        Attach(r, bot, circuit);
        return (bot, circuit);
    }

    /// <summary>Fires a component's trigger input (tg's Trigger port).</summary>
    public static void Fire(AudioRoom r, obj_item_circuit_component c) => r.In(() => c.trigger_input.set_input(COMPONENT_SIGNAL));

    /// <summary>
    /// The live types (atoms) tg defines in the acting and sensing components' folders, the shells' and the presets' files, from
    /// types-index.tsv.
    /// </summary>
    public static List<Type> NewlyLiveAtoms()
    {
        string[] folders =
        {
            "code/modules/wiremod/components/action/", "code/modules/wiremod/components/atom/", "code/modules/wiremod/components/id/",
            "code/modules/wiremod/components/ntnet/", "code/modules/wiremod/components/sensors/", "code/modules/wiremod/components/wirenet/",
            "code/modules/wiremod/components/admin/", "code/modules/wiremod/shell/", "code/modules/wiremod/preset/",
        };
        return Tools.Types.TypesIndex.Index.Types
            .Where(t => t.State == "live" && folders.Any(f => t.Location.StartsWith(f, StringComparison.Ordinal)))
            .Select(t => typeof(atom).Assembly.GetType(typeof(atom).Namespace + "." + t.Class))
            .Where(t => t != null && typeof(atom).IsAssignableFrom(t) && !t.IsAbstract && !InertSeam.IsInertType(t))
            .Distinct()
            .ToList();
    }
}
