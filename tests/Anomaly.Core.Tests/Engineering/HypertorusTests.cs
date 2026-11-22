using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E12b: the hypertorus fusion reactor (code/modules/atmospherics/machinery/components/fusion/) in a booted toy room.
/// The core box, multitooled with the eight boxes around it, turns them to tg's directions (a corner faces the side clockwise of it, a
/// body faces away from the core) and builds the machine; the interface, multitooled, checks the 3x3 layout and links the parts (their
/// _active overlays, the soundloop, the guide paper); a part turned the wrong way stops it. The fusion run follows tg's formulas: the
/// first tick's energy, heat output and fuel use from 1000 moles of hydrogen and tritium, the heat output held at the heat limiter, the
/// heating conductor raising it, the fusion level at 500 K and the parts locked once fusion has started. The window's data and acts, the
/// fuel injection from the input port, the moderator's cooling, the crack overlay and the core's deconstruction releasing its gases.
/// </summary>
public class HypertorusTests
{
    private const int Second = 20;
    private const int X = 5, Y = 5; // the core

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) { }

        public string To(object who) => string.Join("\n", Chat.Where(c => ReferenceEquals(c.Target, who) || (c.Target is client cl && ReferenceEquals(cl.mob, who))).Select(c => c.Html));
    }

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 11, seed: seed);
        PresentationSinks.Of(r.World).Messages = new Feed();
        mob_living_carbon_human bob = r.Join(X, Y + 2, "Bob"); // beside the interface
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static string ChatOf(AudioRoom r, mob who) => ((Feed)PresentationSinks.Of(r.World).Messages).To(who);

    // Meta's kit laid out on the floor: the core box in the middle, the corners on the diagonals, the four bodies beside it
    private static obj_item_hfr_box_core PlaceBoxes(AudioRoom r, Type north = null, Type east = null, Type south = null, Type west = null)
    {
        obj_item_hfr_box_core core = r.In(() =>
        {
            foreach ((int dx, int dy) in new[] { (1, 1), (1, -1), (-1, -1), (-1, 1) })
                Dm.@new<obj_item_hfr_box_corner>(locate(X + dx, Y + dy, 1));
            Dm.@new(north ?? typeof(obj_item_hfr_box_body_interface), locate(X, Y + 1, 1));
            Dm.@new(east ?? typeof(obj_item_hfr_box_body_moderator_input), locate(X + 1, Y, 1));
            Dm.@new(south ?? typeof(obj_item_hfr_box_body_waste_output), locate(X, Y - 1, 1));
            Dm.@new(west ?? typeof(obj_item_hfr_box_body_fuel_input), locate(X - 1, Y, 1));
            return Dm.@new<obj_item_hfr_box_core>(locate(X, Y, 1));
        });
        r.Step(Second);
        return core;
    }

    private static T At<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().SingleOrDefault());

    // the boxes deployed by a multitool on the core box, then the machine linked by a multitool on the interface
    private static obj_machinery_atmospherics_components_unary_hypertorus_core Build(AudioRoom r, mob_living_carbon_human bob)
    {
        obj_item_hfr_box_core box = PlaceBoxes(r);
        var multitool = WallTests.Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.NoSleep(box.multitool_act(bob, multitool)));
        r.Step(Second);
        var core = At<obj_machinery_atmospherics_components_unary_hypertorus_core>(r, X, Y);
        Assert.NotNull(core);
        var hfr_interface = At<obj_machinery_hypertorus_interface>(r, X, Y + 1);
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(hfr_interface.multitool_act(bob, multitool))));
        r.Step(Second);
        Assert.Equal(1f, r.In(() => Dm.num(core.active)));
        return core;
    }

    private static object Act(AudioRoom r, obj_machinery_hypertorus_interface ui_host, mob who, string action, params (string Key, object Value)[] args) => r.In(() =>
    {
        usr = who;
        datum_tgui ui = (datum_tgui)SStgui.get_open_ui(who, ui_host);
        if (ui == null)
        {
            Waiting.NoSleep(ui_host.ui_interact(who));
            ui = (datum_tgui)SStgui.get_open_ui(who, ui_host);
        }
        DmList parameters = Dm.list();
        foreach ((string key, object value) in args)
            parameters[key] = value;
        return Waiting.NoSleep(ui_host.ui_act(action, parameters, ui, null));
    });

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void TheCoreBoxTurnsTheBoxesToTgsDirectionsAndBuildsTheThreeByThreeMachine()
    {
        var (r, bob) = Room(12201);
        obj_item_hfr_box_core box = PlaceBoxes(r);

        // a multitool on the core box with only seven boxes around does nothing
        var stray = At<obj_item_hfr_box_corner>(r, X + 1, Y + 1);
        r.In(() => Waiting.NoSleep(stray.forceMove(locate(X + 3, Y + 3, 1))));
        var multitool = WallTests.Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.NoSleep(box.multitool_act(bob, multitool)));
        r.Step(Second);
        Assert.False(r.In(() => QDELETED(box)));
        Assert.Null(At<obj_machinery_atmospherics_components_unary_hypertorus_core>(r, X, Y));

        // with the eighth back, the boxes become the machine (hfr_parts.dm:462, :490)
        r.In(() => Waiting.NoSleep(stray.forceMove(locate(X + 1, Y + 1, 1))));
        r.In(() => Waiting.NoSleep(box.multitool_act(bob, multitool)));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(box)));
        Assert.Empty(r.In(() => Dm.each_cast<atom>(RANGE_TURFS(1, locate(X, Y, 1))).SelectMany(t => Dm.each_cast<atom>(t.contents)).OfType<obj_item_hfr_box>().ToList()));
        Assert.NotNull(At<obj_machinery_atmospherics_components_unary_hypertorus_core>(r, X, Y));
        // the bodies face away from the core, the corners the side clockwise of them (NE east, SE south, SW west, NW north)
        Assert.Equal(NORTH, r.In(() => At<obj_machinery_hypertorus_interface>(r, X, Y + 1).dir));
        Assert.Equal(EAST, r.In(() => At<obj_machinery_atmospherics_components_unary_hypertorus_moderator_input>(r, X + 1, Y).dir));
        Assert.Equal(SOUTH, r.In(() => At<obj_machinery_atmospherics_components_unary_hypertorus_waste_output>(r, X, Y - 1).dir));
        Assert.Equal(WEST, r.In(() => At<obj_machinery_atmospherics_components_unary_hypertorus_fuel_input>(r, X - 1, Y).dir));
        Assert.Equal(EAST, r.In(() => At<obj_machinery_hypertorus_corner>(r, X + 1, Y + 1).dir));
        Assert.Equal(SOUTH, r.In(() => At<obj_machinery_hypertorus_corner>(r, X + 1, Y - 1).dir));
        Assert.Equal(WEST, r.In(() => At<obj_machinery_hypertorus_corner>(r, X - 1, Y - 1).dir));
        Assert.Equal(NORTH, r.In(() => At<obj_machinery_hypertorus_corner>(r, X - 1, Y + 1).dir));
        Assert.Equal(("fuel_input", "corner", "interface"), r.In(() => (At<obj_machinery_atmospherics_components_unary_hypertorus_fuel_input>(r, X - 1, Y).icon_state,
            At<obj_machinery_hypertorus_corner>(r, X + 1, Y + 1).icon_state, At<obj_machinery_hypertorus_interface>(r, X, Y + 1).icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheInterfaceLinksAWellBuiltMachineAndRefusesAPartTurnedTheWrongWay()
    {
        var (r, bob) = Room(12202);
        obj_item_hfr_box_core box = PlaceBoxes(r);
        var multitool = WallTests.Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.NoSleep(box.multitool_act(bob, multitool)));
        r.Step(Second);
        var core = At<obj_machinery_atmospherics_components_unary_hypertorus_core>(r, X, Y);
        var hfr_interface = At<obj_machinery_hypertorus_interface>(r, X, Y + 1);
        var corner = At<obj_machinery_hypertorus_corner>(r, X + 1, Y + 1);

        // a corner facing the wrong side: "Check all parts and then try again."
        r.In(() => corner.dir = NORTH);
        r.In(() => Waiting.NoSleep(hfr_interface.multitool_act(bob, multitool)));
        Assert.Contains("Check all parts and then try again.", ChatOf(r, bob));
        Assert.Equal(0f, r.In(() => Dm.num(core.active)));
        // the window does not open on an inactive machine (hfr_parts.dm:170)
        r.In(() => Waiting.NoSleep(hfr_interface.ui_interact(bob)));
        Assert.Contains("Activate the machine first by using a multitool on the interface.", ChatOf(r, bob));
        Assert.Null(r.In(() => SStgui.get_open_ui(bob, hfr_interface)));

        // turned back, the parts link: every part active, its _active overlay, the hum, the guide paper on the interface's tile
        r.In(() => corner.dir = EAST);
        r.In(() => Waiting.NoSleep(hfr_interface.multitool_act(bob, multitool)));
        r.Step(Second);
        Assert.Contains("You link all parts toghether.", ChatOf(r, bob));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(core.active));
            Assert.Same(core, hfr_interface.connected_core);
            Assert.Same(hfr_interface, core.linked_interface);
            Assert.NotNull(core.linked_input);
            Assert.NotNull(core.linked_output);
            Assert.NotNull(core.linked_moderator);
            Assert.Equal(4, core.corners.len);
            Assert.Equal(3, core.machine_parts.len);
            Assert.NotNull(core.soundloop);
            Assert.Equal(1f, Dm.num(corner.active));
            Assert.Single(Dm.each_cast<atom>(hfr_interface.loc.contents).OfType<obj_item_paper_guides_jobs_atmos_hypertorus>());
        });
        Assert.Contains("core_active", Overlays(r, core));
        Assert.Contains("interface_active", Overlays(r, hfr_interface));
        Assert.Contains("corner_active", Overlays(r, corner));
        Assert.Contains("fuel_input_active", Overlays(r, r.In(() => core.linked_input)));

        // a second multitool: already active, no second paper
        r.In(() => Waiting.NoSleep(hfr_interface.multitool_act(bob, multitool)));
        Assert.Contains("You already activated the machine.", ChatOf(r, bob));
        Assert.Single(r.In(() => Dm.each_cast<atom>(hfr_interface.loc.contents).OfType<obj_item_paper_guides_jobs_atmos_hypertorus>().ToList()));

        // the window opens now, with tg's recipes in its static data
        r.In(() => Waiting.NoSleep(hfr_interface.ui_interact(bob)));
        Assert.Equal("Hypertorus", r.In(() => Assert.IsType<datum_tgui>(SStgui.get_open_ui(bob, hfr_interface)).@interface));
        DmList fuels = r.In(() => (DmList)((DmList)Waiting.NoSleep(hfr_interface.ui_static_data(bob)))["selectable_fuel"]);
        Assert.Equal(8, fuels.len); // "Nothing" and tg's seven recipes
        Assert.Equal("Nothing", Dm.str(((DmList)fuels[1])["name"]));
        Assert.Contains(Dm.each_cast<DmList>(fuels), f => Dm.str(f["id"]) == "h2_t2_fuel" && Dm.str(f["name"]) == "Hydrogen + Tritium fuel");

        // a part turned away: the next atmos tick's check fails and deactivates the machine, dropping the overlays
        r.In(() => corner.dir = SOUTH);
        r.Step(2 * Second);
        Assert.Equal(0f, r.In(() => Dm.num(core.active)));
        Assert.Null(r.In(() => core.linked_interface));
        Assert.DoesNotContain("corner_active", Overlays(r, corner));
        Assert.DoesNotContain("core_active", Overlays(r, core));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHydrogenTritiumRunHeatsTheMixByTgsFormulasUpToTheFirstFusionLevel()
    {
        var (r, bob) = Room(12203);
        var core = Build(r, bob);
        var hfr_interface = r.In(() => core.linked_interface);

        Assert.Equal(TRUE, Act(r, hfr_interface, bob, "fuel", ("mode", "h2_t2_fuel")));
        Assert.Equal("h2_t2_fuel", r.In(() => core.selected_fuel.id));
        Assert.Equal(TRUE, Act(r, hfr_interface, bob, "start_power"));
        Assert.Equal((1f, (float)ACTIVE_POWER_USE), r.In(() => (core.start_power, Dm.num(core.use_power))));

        r.In(() =>
        {
            // 1000 moles of each fuel at room temperature straight into the fusion mix (the cooling and the injection are off)
            core.internal_fusion.assert_gases(typeof(datum_gas_hydrogen), typeof(datum_gas_tritium));
            core.internal_fusion.moles[typeof(datum_gas_hydrogen)] = 1000f;
            core.internal_fusion.moles[typeof(datum_gas_tritium)] = 1000f;
            core.internal_fusion.temperature = T20C;

            // tick 1 (hfr_main_processes.dm:45): the volume 5000 L at a constrictor of 100, the scale 2500, each fuel's (1000 - 25) / 2500
            float scaled = (1000f - FUSION_MOLE_THRESHOLD) / 2500f;
            float energy_modifiers = scaled + scaled; // no moderator, no helium yet
            float heat_modifier = clamp(scaled * 1.15f, 0.25f, 100);
            float expected_energy = (energy_modifiers * Dm.pow(LIGHT_SPEED, 2)) * max(T20C * heat_modifier / 100, 1);
            // no energy stored yet: no internal power, the core at TCMB; the conduction of the mix's heat is the whole power output
            float conduction = -(T20C - TCMB) * (100 * 0.001f);
            float power_output = VOID_CONDUCTION * (0 - conduction - 0);
            float expected_heat = clamp(power_output * heat_modifier / 200, -0.05f, 5);

            core.process_atmos(1f);
            Assert.Equal(0f, core.power_level);
            Assert.Equal(expected_energy, core.energy, expected_energy * 1e-5f);
            Assert.Equal(0f, core.internal_power);
            Assert.Equal(TCMB, core.core_temperature);
            Assert.Equal(expected_heat, core.heat_output, 1e-6f);
            Assert.Equal(-0.05f, core.heat_output_min, 1e-6f); // - 5 * 10 ** 0 * (100 / 100) * 0.01 times the recipe's 1
            Assert.Equal(5f, core.heat_output_max);
            Assert.Equal(T20C + expected_heat, core.internal_fusion.temperature, 1e-3f);
            // power level 0 burns the minimum 0.05 mol/s, 0.85 of it from each fuel, half of that into helium
            Assert.Equal(1000f - 0.05f * 0.85f, Dm.num(core.internal_fusion.moles[typeof(datum_gas_hydrogen)]), 1e-3f);
            Assert.Equal(0.05f * 0.85f * 0.5f, Dm.num(core.internal_fusion.moles[typeof(datum_gas_helium)]), 1e-4f);

            // tick 2: the stored energy powers the core, and the heat output stops at the limiter's 5 K/s
            float t1 = core.internal_fusion.temperature;
            core.process_atmos(1f);
            Assert.True(core.internal_power > 0, "the energy of the first tick powers the core");
            Assert.True(core.core_temperature > TCMB);
            Assert.Equal(core.heat_output_max, core.heat_output);
            Assert.Equal(t1 + 5, core.internal_fusion.temperature, 1e-3f);
            Assert.True(core.instability * 0.5f < FUSION_INSTABILITY_ENDOTHERMALITY, "exothermic");
        });

        // the heating conductor at 500 (the guide's kick-start) raises the limiter fivefold, past what the reaction gives
        Assert.Equal(TRUE, Act(r, hfr_interface, bob, "heating_conductor", ("heating_conductor", "500")));
        Assert.Equal(500f, r.In(() => core.heating_conductor.Value));
        r.In(() =>
        {
            float before = core.internal_fusion.temperature;
            float energy_before = core.energy;
            core.process_atmos(1f);
            Assert.True(core.energy > energy_before, "a mix 5 K hotter holds more energy");
            Assert.Equal(25f, core.heat_output_max);
            // the reaction's own output (power output times the heat modifier over 200) now passes the old 5 K/s cap
            Assert.InRange(core.heat_output, 5.01f, 25f);
            Assert.Equal(before + core.heat_output, core.internal_fusion.temperature, 1e-3f);

            // up to 500 K the fusion level stays 0; past it the next tick reads level 1
            for (int i = 0; i < 20 && core.internal_fusion.temperature <= 500; i++)
                core.process_atmos(1f);
            Assert.True(core.internal_fusion.temperature > 500);
            core.process_atmos(1f);
            Assert.Equal(1f, core.power_level);
            Assert.Equal(1f, core.fusion_started);
            Assert.Equal(1f, core.linked_input.fusion_started);
            Assert.Equal(250f, core.heat_output_max); // 5 * 10 ** 1 * 5
        });

        // with fusion started the parts no longer open, and the power cannot be cut (the window's button is disabled at power > 0)
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(core.screwdriver_act(bob, screwdriver))));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(hfr_interface.screwdriver_act(bob, screwdriver))));
        DmList data = r.In(() => (DmList)Waiting.NoSleep(hfr_interface.ui_data(bob)));
        Assert.Equal(1f, Dm.num(data["power_level"]));
        Assert.Equal("h2_t2_fuel", Dm.str(data["selected"]));
        Assert.Equal(100f, Dm.num(data["integrity"]));
        Assert.True(Dm.num(data["energy_level"]) > 1e15f);
        Assert.True(Dm.num(data["internal_fusion_temperature"]) > 500);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowsActsSetTgsClampedValuesAndTheInjectionAndCoolingMoveTheGases()
    {
        var (r, bob) = Room(12204);
        var core = Build(r, bob);
        var hfr_interface = r.In(() => core.linked_interface);

        // tg's clamps (hfr_parts.dm:304)
        Act(r, hfr_interface, bob, "magnetic_constrictor", ("magnetic_constrictor", "5000"));
        Act(r, hfr_interface, bob, "fuel_injection_rate", ("fuel_injection_rate", "0.1"));
        Act(r, hfr_interface, bob, "moderator_injection_rate", ("moderator_injection_rate", "40"));
        Act(r, hfr_interface, bob, "current_damper", ("current_damper", "-3"));
        Act(r, hfr_interface, bob, "mod_filtering_rate", ("mod_filtering_rate", "300"));
        Act(r, hfr_interface, bob, "cooling_volume", ("cooling_volume", "10"));
        r.In(() =>
        {
            Assert.Equal((1000f, 0.5f, 40f, 0f, 200f), (core.magnetic_constrictor.Value, core.fuel_injection_rate.Value, core.moderator_injection_rate.Value, core.current_damper.Value, core.moderator_filtering_rate));
            Assert.Equal(50f, ((datum_gas_mixture)core.airs[1]).volume);
        });
        // the moderator filter: helium by default, plasma toggled in and helium out
        Act(r, hfr_interface, bob, "filter", ("mode", "plasma"));
        Act(r, hfr_interface, bob, "filter", ("mode", "helium"));
        r.In(() => Assert.Equal(new object[] { typeof(datum_gas_plasma) }, Dm.each(core.moderator_scrubbing).ToArray()));
        DmList data = r.In(() => (DmList)Waiting.NoSleep(hfr_interface.ui_data(bob)));
        Assert.Contains(Dm.each_cast<DmList>((DmList)data["filter_types"]), f => Dm.str(f["gas_id"]) == "plasma" && Dm.num(f["enabled"]) == 1);
        Assert.Contains(Dm.each_cast<DmList>((DmList)data["filter_types"]), f => Dm.str(f["gas_id"]) == "helium" && Dm.num(f["enabled"]) == 0);
        Assert.Equal(50f, Dm.num(data["cooling_volume"]));

        // fuel in the input port, plasma in the moderator port, cold plasma in the core's cooling port
        Act(r, hfr_interface, bob, "fuel", ("mode", "h2_t2_fuel"));
        Act(r, hfr_interface, bob, "fuel_injection_rate", ("fuel_injection_rate", "25"));
        Act(r, hfr_interface, bob, "start_power");
        Act(r, hfr_interface, bob, "start_cooling");
        Act(r, hfr_interface, bob, "start_fuel");
        Act(r, hfr_interface, bob, "start_moderator");
        r.In(() =>
        {
            var input = (datum_gas_mixture)core.linked_input.airs[1];
            input.assert_gases(typeof(datum_gas_hydrogen), typeof(datum_gas_tritium));
            input.moles[typeof(datum_gas_hydrogen)] = 100f;
            input.moles[typeof(datum_gas_tritium)] = 100f;
            input.temperature = T20C;
            var moderator = (datum_gas_mixture)core.linked_moderator.airs[1];
            moderator.assert_gas(typeof(datum_gas_plasma));
            moderator.moles[typeof(datum_gas_plasma)] = 500f;
            moderator.temperature = T20C;
            var coolant = (datum_gas_mixture)core.airs[1];
            coolant.assert_gas(typeof(datum_gas_plasma));
            coolant.moles[typeof(datum_gas_plasma)] = 200f;
            coolant.temperature = 100;

            core.process_atmos(1f);
            // tg's injection: the fuel rate split between the two fuels, the moderator rate of the moderator port's mix
            Assert.Equal(12.5f, Dm.num(core.internal_fusion.moles[typeof(datum_gas_hydrogen)]), 1e-3f);
            Assert.Equal(12.5f, Dm.num(core.internal_fusion.moles[typeof(datum_gas_tritium)]), 1e-3f);
            Assert.Equal(87.5f, Dm.num(input.moles[typeof(datum_gas_hydrogen)]), 1e-3f);
            Assert.Equal(40f, Dm.num(core.moderator_internal.moles[typeof(datum_gas_plasma)]), 1e-2f);
            // the coolant loop took heat out of the moderator: colder than the room
            Assert.True(core.moderator_internal.temperature < T20C, "the coolant cools the moderator: " + core.moderator_internal.temperature);
            Assert.True(coolant.temperature > 100, "and warms up itself");
        });

        // a recipe change dumps the fusion mix into the waste port
        Act(r, hfr_interface, bob, "fuel", ("mode", "plasma_o2_fuel"));
        r.In(() =>
        {
            Assert.Equal("plasma_o2_fuel", core.selected_fuel.id);
            Assert.Equal(0f, Dm.num(core.internal_fusion.total_moles()));
            Assert.True(Dm.num(((datum_gas_mixture)core.linked_output.airs[1]).moles[typeof(datum_gas_hydrogen)]) > 12);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrackShowsAndTheCoreComesApartReleasingItsGases()
    {
        var (r, bob) = Room(12205);
        var core = Build(r, bob);

        // a crack on a port: tg's crack overlay in the part's direction, and a moderator spill from it at low pressure
        var cracked = r.In(() => (obj_machinery_atmospherics_components_unary_hypertorus)core.create_crack());
        Assert.Equal(1f, r.In(() => cracked.cracked));
        Assert.Contains("crack", Overlays(r, cracked));
        Assert.Same(cracked, r.In(() => core.check_cracked_parts()));

        // the integrity getters (hfr_procs.dm:279, :315)
        r.In(() =>
        {
            core.critical_threshold_proximity = 450;
            Assert.Equal(50f, Dm.num(core.get_integrity_percent()));
            Assert.Equal((float)HYPERTORUS_WARNING, Dm.num(core.get_status()));
            core.critical_threshold_proximity = 860;
            Assert.Equal((float)HYPERTORUS_MELTING, Dm.num(core.get_status()));
            core.critical_threshold_proximity = 0;
            Assert.Equal((float)HYPERTORUS_INACTIVE, Dm.num(core.get_status()));
        });

        // Bob on a corner's tile, beside the core
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(X - 1, Y - 1, 1))));
        // the core with gas inside: a screwdriver opens it, a crowbar warns and takes it apart, its gases going to the floor
        r.In(() =>
        {
            core.moderator_internal.assert_gas(typeof(datum_gas_nitrogen));
            core.moderator_internal.moles[typeof(datum_gas_nitrogen)] = 100f;
            core.moderator_internal.temperature = T20C;
        });
        // the room's nitrogen, summed over its floor (the released gas spreads from the core's tile)
        float RoomNitrogen() => r.In(() => Dm.each_cast<turf>(RANGE_TURFS(5, locate(X, Y, 1))).OfType<turf_open>()
            .Sum(t => Dm.num(((datum_gas_mixture)Waiting.NoSleep(t.return_air())).moles[typeof(datum_gas_nitrogen)])));
        float before = RoomNitrogen();
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(core.screwdriver_act(bob, screwdriver)));
        Assert.Equal(1f, r.In(() => Dm.num(core.panel_open)));
        Assert.Equal("core_open", r.In(() => core.icon_state));
        var crowbar = WallTests.Hold<obj_item_crowbar>(r, bob);
        r.In(() => Waiting.Detach(core.crowbar_act(bob, crowbar)));
        r.Step(4 * Second);
        Assert.True(r.In(() => QDELETED(core)));
        Assert.Contains("WARNING - Core can contain hazardous gases, deconstruct with caution!", ChatOf(r, bob));
        Assert.Equal(before + 100, RoomNitrogen(), 1f);
        r.In(() => Assert.Contains(Dm.each_cast<atom>(locate(X, Y, 1).contents), a => a is obj_item_circuitboard_machine_HFR_core or obj_structure_frame_machine));
        r.AssertNoRuntimes();
    }
    [Fact]
    public void PastTheMeltingPointTheCountdownEndsInTgsMeltdownThatSpillsTheMixAndRemovesTheCore()
    {
        var (r, bob) = Room(12206);
        var core = Build(r, bob);
        var hfr_interface = r.In(() => core.linked_interface);
        Act(r, hfr_interface, bob, "fuel", ("mode", "h2_t2_fuel"));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(1, 1, 1)))); // out of the blast
        float RoomTritium() => r.In(() => Dm.each_cast<turf>(RANGE_TURFS(5, locate(X, Y, 1))).OfType<turf_open>()
            .Sum(t => Dm.num(((datum_gas_mixture)Waiting.NoSleep(t.return_air())).moles[typeof(datum_gas_tritium)])));
        float before = RoomTritium();
        r.In(() =>
        {
            core.internal_fusion.assert_gases(typeof(datum_gas_hydrogen), typeof(datum_gas_tritium));
            core.internal_fusion.moles[typeof(datum_gas_tritium)] = 200f;
            core.internal_fusion.temperature = T20C;
            // integrity gone (hfr_procs.dm:343): over the melting point the alert starts the countdown
            core.critical_threshold_proximity = 901;
            core.check_alert();
            Assert.Equal(1f, core.final_countdown);
            Assert.Equal(0f, Dm.num(core.get_integrity_percent()));
        });
        // tg's 30 s countdown, then the meltdown: the explosion, the radiation and EMP pulses of the recipe, the mix spilled around
        r.Step(25 * Second);
        Assert.False(r.In(() => QDELETED(core)));
        r.Step(10 * Second);
        Assert.True(r.In(() => QDELETED(core)));
        // tg's pockets (hfr_procs.dm:522): a fifth of the mix split into 7 pockets, each 1/7 of what is left of that fifth, so (6/7)^7 of it
        // is never released; the rest goes to the core's floor
        Assert.Equal(before + 200 - 40 * MathF.Pow(6f / 7, 7), RoomTritium(), 1f);
        r.AssertNoRuntimes();
    }
}
