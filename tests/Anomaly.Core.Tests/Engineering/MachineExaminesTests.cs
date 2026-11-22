using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Package X1 (D291): the examines of the machines and their parts, written from tg's DM: each object is examined in the states its lines
/// depend on and tg's text appears (and does not appear when its condition is false). Bob stands next to the object (in_range) unless a test
/// walks him away.
/// </summary>
public class MachineExaminesTests
{
    private const int X = 5, Y = 5;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 11, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        return (r, bob);
    }

    private static T Make<T>(AudioRoom r) where T : atom => r.In(() => Dm.@new<T>(locate(X, Y, 1)));

    private static string Say(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(Dm.str)));
    private static void Far(AudioRoom r, mob who) => r.In(() => Waiting.NoSleep(who.forceMove(locate(1, 1, 1))));

    [Fact]
    public void TheMachineBaseSaysBrokenAndDamagedAndTheResearchScannerListsTheParts()
    {
        var (r, bob) = Room(9301);
        var charger = Make<obj_machinery_cell_charger>(r);
        string whole = Say(r, charger, bob);
        Assert.Contains("There's no cell in the charger.", whole);
        Assert.Contains("Charging power: <b>2.5 kW</b>", whole); // in range
        Assert.DoesNotContain("damaged", whole);
        Assert.DoesNotContain("broken", whole);

        // the health bands of _machinery.dm:1239: 50 to 99 slightly, 25 to 50 heavily, 0 to 25 falling apart
        void Health(float percent) => r.In(() => charger.atom_integrity = Dm.num(charger.max_integrity) * percent / 100f);
        Health(60);
        Assert.Contains("It looks slightly damaged.", Say(r, charger, bob));
        Health(30);
        Assert.Contains("It appears heavily damaged.", Say(r, charger, bob));
        Health(10);
        Assert.Contains("It's falling apart!", Say(r, charger, bob));
        r.In(() => charger.machine_stat = Dm.num(charger.machine_stat) + BROKEN);
        Assert.Contains("It looks broken and non-functional.", Say(r, charger, bob));

        // far away the status display is not read
        Far(r, bob);
        Assert.DoesNotContain("Charging power", Say(r, charger, bob));

        // examine_more: the research scanner lists the parts (display_parts)
        Assert.Equal("machine", r.In(() => Dm.str(charger.examine_descriptor(bob))));
        string more() => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(charger.examine_more(bob))).Select(Dm.str)));
        Assert.DoesNotContain("It contains the following parts:", more());
        r.In(() => ADD_TRAIT(bob, TRAIT_RESEARCH_SCANNER, "test"));
        string parts = more();
        Assert.Contains("It contains the following parts:", parts);
        Assert.Contains("capacitor", parts); // the board asks for one
    }

    [Fact]
    public void AnAirlockTellsItsCycleItsWeldItsWiresItsEmagAndItsExternalLabel()
    {
        var (r, bob) = Room(9302);
        var airlock = Make<obj_machinery_door_airlock>(r);
        string text = Say(r, airlock, bob);
        Assert.Contains("This airlock does not cycle.", text);
        Assert.Contains("Its maintenance panel is <b>screwed</b> in place", text); // the door's line
        Assert.DoesNotContain("welded shut", text);
        Assert.DoesNotContain("Shift-click", text); // a human is no silicon

        r.In(() => { airlock.welded = 1; airlock.closeOtherId = "main"; airlock.obj_flags = Dm.num(airlock.obj_flags) + EMAGGED; airlock.panel_open = 1; airlock.security_level = 5; });
        text = Say(r, airlock, bob);
        Assert.Contains("This airlock cycles on ID: main.", text);
        Assert.Contains("It's welded shut.", text);
        Assert.Contains("Its access panel is smoking slightly.", text);
        Assert.Contains("Its maintenance panel is open.", text);
        Assert.Contains("There is a welded plasteel cover hiding its wires.", text);
        r.In(() => airlock.panel_open = 0);
        Assert.Contains("It looks very robust.", Say(r, airlock, bob));

        // a note pinned to the front: read only from close by
        var paper = Make<obj_item_paper>(r);
        r.In(() => { airlock.note = paper; paper.name = "memo"; });
        text = Say(r, airlock, bob);
        Assert.Contains("There's a memo pinned to the front...", text);
        Assert.Contains("The attached memo can be taken down with some <b>wirecutters</b>.", text);
        Far(r, bob);
        text = Say(r, airlock, bob);
        Assert.Contains("There's a memo pinned to the front. You can't read it from here.", text);

        var external = Make<obj_machinery_door_airlock_external>(r);
        Assert.DoesNotContain("emergency mechanism", Say(r, external, bob));
        r.In(() => external.space_dir = EAST);
        Assert.Contains("emergency mechanism to open from the east side with <b>just your hands</b>", Say(r, external, bob));
    }

    [Fact]
    public void FireLocksPodDoorsAndWindoorsTellTheirStates()
    {
        var (r, bob) = Room(9303);
        var fire = Make<obj_machinery_door_firedoor>(r);
        r.In(() => { fire.density = 0; });
        Assert.Contains("It is open, but could be <b>pried</b> closed.", Say(r, fire, bob));
        r.In(() => { fire.density = 1; fire.welded = 0; });
        string text = Say(r, fire, bob);
        Assert.Contains("It is closed, but could be <b>pried</b> open.", text);
        Assert.Contains("Prying by <i>right-clicking</i> the firelock will open it permanently.", text);
        r.In(() => { fire.welded = 1; fire.boltslocked = 1; });
        Assert.Contains("The floor bolts have been locked by <b>screws</b>.", Say(r, fire, bob));
        r.In(() => fire.boltslocked = 0);
        Assert.Contains("the bolts themselves are still <b>wrenched</b> to the floor.", Say(r, fire, bob));

        var pod = Make<obj_machinery_door_poddoor>(r);
        Assert.DoesNotContain("blast door controller", Say(r, pod, bob));
        r.In(() => { pod.panel_open = 1; pod.deconstruction = BLASTDOOR_FINISHED; });
        Assert.Contains("could be calibrated to a blast door controller ID", Say(r, pod, bob));
        r.In(() => pod.deconstruction = BLASTDOOR_NEEDS_ELECTRONICS);
        Assert.Contains("The <i>electronics</i> are missing", Say(r, pod, bob));
        r.In(() => pod.deconstruction = BLASTDOOR_NEEDS_WIRES);
        Assert.Contains("ready to be <b>sliced apart</b>", Say(r, pod, bob));

        var window = Make<obj_machinery_door_window>(r);
        r.In(() => { window.density = 0; window.panel_open = 0; });
        Assert.DoesNotContain("levered", Say(r, window, bob));
        r.In(() => window.panel_open = 1);
        Assert.Contains("could be <span class='boldnotice'>levered</span> out.", Say(r, window, bob));
    }

    [Fact]
    public void AlarmsFramesAndTheAirSensorTellTheirConstructionAndAlertState()
    {
        var (r, bob) = Room(9304);
        var fa = Make<obj_machinery_firealarm>(r);
        string text = Say(r, fa, bob);
        Assert.Contains("The local area thermal detection light is lit.", text);
        Assert.DoesNotContain("security alert level", text); // the toy room is no station level
        Assert.DoesNotContain("hazard light is flashing", text);
        r.In(() => { var a = (area)fa.my_area; a.fire = 1; a.fault_status = AREA_FAULT_AUTOMATIC; a.fault_location = "the test"; });
        text = Say(r, fa, bob);
        Assert.Contains("The local area hazard light is flashing.", text);
        Assert.Contains("The fault location display is the test (Automatic Detection).", text);
        Assert.Contains("<b>Right-Click</b> to reset firelocks in this area.", text);

        var air = Make<obj_machinery_airalarm>(r);
        Assert.Contains("It is missing air alarm electronics.", Say(r, air, bob)); // a new alarm starts unbuilt
        r.In(() => air.buildstage = AIR_ALARM_BUILD_COMPLETE);
        Assert.Contains("Right-click to unlock the interface.", Say(r, air, bob)); // locked from the start
        r.In(() => air.locked = 0);
        Assert.Contains("Right-click to lock the interface.", Say(r, air, bob));
        r.In(() => air.buildstage = AIR_ALARM_BUILD_NO_WIRES);
        Assert.Contains("It is missing wiring.", Say(r, air, bob));
        r.In(() => air.buildstage = AIR_ALARM_BUILD_NO_CIRCUIT);
        Assert.Contains("It is missing air alarm electronics.", Say(r, air, bob));

        // a machine frame: empty, wired, then with a board (the parts it still needs)
        var frame = Make<obj_structure_frame_machine>(r);
        text = Say(r, frame, bob);
        Assert.Contains("It can be <b>welded</b> or <b>screwed</b> apart.", text);
        Assert.Contains("It should be <b>wired</b> with 5 cables.", text);
        r.In(() => frame.state = FRAME_STATE_WIRED);
        text = Say(r, frame, bob);
        Assert.Contains("Its wires can be <b>cut</b>.", text);
        Assert.Contains("It's missing a circuit board!", text);
        var board = Make<obj_item_circuitboard_machine_cell_charger>(r);
        r.In(() => Waiting.NoSleep(frame.install_board(bob, board)));
        text = Say(r, frame, bob);
        Assert.Contains("It has Cell Charger (Machine Board) installed.", text); // a capitalised name is proper: no article
        Assert.Contains("It requires 1 capacitor", text);
        Assert.Contains("All the components can be <b>pried</b> out.", text);

        var sensor = Make<obj_machinery_air_sensor>(r);
        Assert.Contains("Use a multitool to link it to an injector, vent, or air alarm, or reset its ports.", Say(r, sensor, bob));
        var item = Make<obj_item_air_sensor>(r);
        Assert.Contains("It should be <b>wrenched</b> in place to turn it on.", Say(r, item, bob));
        r.In(() => item.anchored = 1);
        text = Say(r, item, bob);
        Assert.Contains("It's <b>wrenched</b> in place", text);
        Assert.DoesNotContain("should be", text);
    }

    [Fact]
    public void AtmosphericsMachinesTellTheirLayerLinkWeldAndOverclock()
    {
        var (r, bob) = Room(9305);
        var vent = Make<obj_machinery_atmospherics_components_unary_vent_pump>(r);
        string text = Say(r, vent, bob);
        Assert.Contains("is on layer 3.", text); // atmosmachinery.dm:121
        Assert.Contains("You can link it with an air sensor using a multitool.", text);
        Assert.DoesNotContain("welded shut", text);
        r.In(() => { vent.welded = 1; vent.fan_overclocked = 1; vent.atom_integrity = Dm.num(vent.max_integrity) * 0.6f; vent.on_update_integrity(100, 60); });
        text = Say(r, vent, bob);
        Assert.Contains("It seems welded shut.", text);
        Assert.Contains("It is currently overclocked causing it to take damage over time.", text);
        Assert.Contains("The fan is in okay condition.", text);
        r.In(() => vent.atom_integrity = 0);
        Assert.Contains("The fan is broken.", Say(r, vent, bob));

        var scrubber = Make<obj_machinery_atmospherics_components_unary_vent_scrubber>(r);
        Assert.DoesNotContain("welded shut", Say(r, scrubber, bob));
        r.In(() => scrubber.welded = 1);
        Assert.Contains("It seems welded shut.", Say(r, scrubber, bob));

        Assert.Contains("You can link it with an air sensor using a multitool.", Say(r, Make<obj_machinery_atmospherics_components_unary_outlet_injector>(r), bob));

        var pump = Make<obj_machinery_atmospherics_components_binary_volume_pump>(r);
        Assert.Contains("Its pressure limits could be disabled with a <b>multitool</b>.", Say(r, pump, bob)); // tg's wording: the limits are on, "dis"abled
        r.In(() => { pump.overclocked = 1; pump.on = 1; });
        text = Say(r, pump, bob);
        Assert.Contains("Its pressure limits could be enabled with a <b>multitool</b>.", text);
        Assert.Contains("Its warning light is on and it's spewing gas!", text);

        var torus = Make<obj_machinery_atmospherics_components_unary_hypertorus_fuel_input>(r);
        Assert.Contains("can be rotated by first opening the panel with a screwdriver and then using a wrench on it.", Say(r, torus, bob));
        Assert.Contains("can be rotated by first opening the panel", Say(r, Make<obj_machinery_hypertorus_corner>(r), bob));
    }

    [Fact]
    public void CanistersAndPortableMachinesTellTheirHullHatchHolderAndCrystal()
    {
        var (r, bob) = Room(9306);
        var can = Make<obj_machinery_portable_atmospherics_canister>(r);
        string text = Say(r, can, bob);
        Assert.Contains("MAX SAFE PRESSURE: 500 MPa; MAX SAFE TEMPERATURE: 10 kK", text); // siunit_pressure(500000 kPa), siunit(10000 K)
        Assert.Contains("The hull is <b>welded</b> together and can be cut apart.", text);
        Assert.Contains("Warning, no cell installed, use a screwdriver to open the hatch and insert one.", text);
        Assert.DoesNotContain("Integrity compromised", text);
        Assert.DoesNotContain("Hatch open", text);
        r.In(() => { can.atom_integrity = Dm.num(can.max_integrity) - 1; can.panel_open = 1; });
        text = Say(r, can, bob);
        Assert.Contains("Integrity compromised, repair hull with a welding tool.", text);
        Assert.Contains("Hatch open, close it with a screwdriver.", text);

        // portable_atmospherics.dm: the crystal lines (:86) and the holder (:222)
        var pump = Make<obj_machinery_portable_atmospherics_pipe_scrubber>(r);
        text = Say(r, pump, bob);
        Assert.DoesNotContain("hypernoblium", text);
        Assert.DoesNotContain("hot swap", text);
        var tank = Make<obj_item_tank_internals_oxygen>(r);
        r.In(() => { pump.nob_crystal_inserted = 1; pump.suppress_reactions = 1; pump.holding = tank; });
        text = Say(r, pump, bob);
        Assert.Contains("There is a hypernoblium crystal inside it", text);
        Assert.Contains("indicating reactions inside are currently being suppressed.", text);
        Assert.Contains("Alt-click", text);
        Assert.Contains("with another gas tank to hot swap", text);
    }

    [Fact]
    public void PowerMachinesTellTheirChargeTierLayersBreakageAndLights()
    {
        var (r, bob) = Room(9307);
        var heater = Make<obj_machinery_space_heater>(r);
        string text = Say(r, heater, bob);
        Assert.Contains("is off, and the hatch is closed.", text);
        Assert.Contains("The charge meter reads 100%.", text);
        Assert.Contains("Heating power: <b>", text);
        Assert.Contains("efficiency.</b>", text);
        Assert.Contains("Target temperature: <b>", text);
        Assert.Contains("<b>Right-click</b> to toggle on.", text);
        Far(r, bob);
        text = Say(r, heater, bob);
        Assert.DoesNotContain("Heating power", text);
        Assert.DoesNotContain("Right-click", text);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1))));
        Assert.Contains("Reagent conduction power: <b>", Say(r, Make<obj_machinery_space_heater_improvised_chem_heater>(r), bob));

        var rtg = Make<obj_machinery_power_rtg>(r);
        Assert.Contains("The status display reads: Power generation at <b>1 kW</b>.", Say(r, rtg, bob));

        var rotor = Make<obj_machinery_power_turbine_core_rotor>(r);
        text = Say(r, rotor, bob);
        Assert.Contains("open its panel to change cable layer.", text);
        Assert.Contains("The parts need to be linked via a <b>multitool</b>", text);
        Assert.Contains("Its maintenance panel can be <b>screwed</b> open.", text);
        r.In(() => { rotor.panel_open = 1; rotor.all_parts_connected = 1; });
        text = Say(r, rotor, bob);
        Assert.Contains("The full machine can be <b>pried</b> apart", text);
        Assert.DoesNotContain("linked via a", text);
        var part = Make<obj_item_turbine_parts_rotor>(r);
        text = Say(r, part, bob);
        Assert.Contains("This is a tier 1 turbine part, rated for ", text);
        Assert.Contains("sheets.", text);
        r.In(() => { part.current_tier = TURBINE_PART_TIER_FOUR; });
        Assert.Contains("Is already at max tier.", Say(r, part, bob));

        var grav = Make<obj_machinery_gravity_generator_main>(r);
        Assert.DoesNotContain("seals", Say(r, grav, bob));
        r.In(() => { grav.machine_stat = Dm.num(grav.machine_stat) + BROKEN; grav.broken_state = 1; });
        Assert.Contains("There's lots of broken seals on the framework, it could use some <b>welding</b>.", Say(r, grav, bob));

        var light = Make<obj_machinery_light>(r);
        Assert.Contains("Its backup power charge meter reads 100%.", Say(r, light, bob));
        r.In(() => { light.status = LIGHT_BURNED; });
        Assert.Contains("The tube is burnt out.", Say(r, light, bob));
        r.In(() => { light.status = LIGHT_EMPTY; });
        Assert.Contains("The tube has been removed.", Say(r, light, bob));

        var cable = Make<obj_structure_cable_multilayer>(r);
        r.In(() => cable.cable_layer = CABLE_LAYER_1 | CABLE_LAYER_3);
        text = Say(r, cable, bob);
        Assert.Contains("L1:Connect.", text);
        Assert.Contains("L2:Disconnect.", text);
        Assert.Contains("L3:Connect.", text);
    }

    [Fact]
    public void ManufacturingMachinesAndTheirNeighboursTellWhatTheyMakeAndHold()
    {
        var (r, bob) = Room(9308);
        var crafter = Make<obj_machinery_power_manufacturing_crafter>(r);
        string text = Say(r, crafter, bob);
        Assert.Contains("It receives power via cable, but certain buildings do not need power.", text);
        Assert.Contains("It contains no items.", text);
        Assert.Contains("It is currently manufacturing <b>nothing. Use a multitool to set it</b>.", text);
        Assert.DoesNotContain("It needs:", text);
        var recipe = r.In(() => Dm.each(GLOB.crafting_recipes).OfType<datum_crafting_recipe>().First(c => Dm.truthy(Dm.length(c.reqs))));
        r.In(() => crafter.recipe = recipe);
        text = Say(r, crafter, bob);
        Assert.Contains("It is currently manufacturing <b>" + r.In(() => Dm.str(recipe.name)) + "</b>.", text);
        Assert.Contains("It needs:", text);
        Assert.Contains(r.In(() => Dm.str(Dm.initial(Dm.each(recipe.reqs).First(), "name"))), text);
        var steel = r.In(() => Dm.@new<obj_item_stack_sheet_iron>(crafter.loc, 7));
        r.In(() => Waiting.NoSleep(steel.forceMove(crafter)));
        text = Say(r, crafter, bob);
        Assert.Contains("It contains:", text);
        Assert.Contains("7 iron", text);

        var lathe = Make<obj_machinery_power_manufacturing_lathe>(r);
        Assert.Contains("It is set to print nothing, set with a multitool.", Say(r, lathe, bob));

        var toner = Make<obj_item_toner>(r);
        Assert.Contains("The ink level gauge on the side reads 100%", Say(r, toner, bob));
        r.In(() => toner.charges = 3);
        Assert.Contains("The ink level gauge on the side reads 60%", Say(r, toner, bob)); // 3 of 5

        var sorter = Make<obj_structure_disposalpipe_sorting_mail>(r);
        Assert.Contains("It has no sorting tags set.", Say(r, sorter, bob));
        r.In(() => sorter.sortTypes = Dm.list(1, 2));
        text = Say(r, sorter, bob);
        Assert.Contains("It is tagged with the following tags:", text);
        Assert.Contains("\tDisposals.", text);
        Assert.Contains("\t" + r.In(() => Dm.str(GLOB.TAGGERLOCATIONS[2])) + ".", text);

        var tower = Make<obj_machinery_power_weather_tower>(r);
        Assert.Contains("It has a slot in which you could install a weather anomaly core.", Say(r, tower, bob));

        var arm = Make<obj_machinery_big_manipulator>(r);
        Assert.DoesNotContain("poor", Say(r, arm, bob));
        var monkey = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey>(locate(6, 6, 1)));
        r.In(() => arm.monkey_worker = (datum_weakref)WEAKREF(monkey));
        Assert.Contains("You can see a poor ", Say(r, arm, bob));
        Assert.Contains("buckled to ", Say(r, arm, bob));

        var camera = Make<obj_machinery_camera>(r);
        text = Say(r, camera, bob);
        Assert.Contains("It can be shielded against electromagnetic interference with some <b>plasma</b>.", text);
        Assert.Contains("It can be upgraded with a <b>proximity sensor</b>.", text);
        Assert.DoesNotContain("deactivated", text);
        r.In(() => camera.camera_enabled = 0);
        Assert.Contains("It's currently deactivated.", Say(r, camera, bob));
    }

    [Fact]
    public void ShieldGeneratorsPortableGravityAndFittingsTellTheirState()
    {
        var (r, bob) = Room(9309);
        var gen = Make<obj_machinery_atmos_shield_gen>(r);
        string text = Say(r, gen, bob);
        Assert.Contains("The status display reads:", text);
        Assert.Contains("Currently inactive.", text);
        Assert.Contains("Maximum field length:", text);
        Assert.Contains("Its maintenance panel can be <b>screwed</b> open.", text);
        r.In(() => gen.locked = 1);
        text = Say(r, gen, bob);
        Assert.Contains("LOCKED", text);
        Assert.DoesNotContain("Maximum field length", text);
        Far(r, bob);
        Assert.DoesNotContain("status display", Say(r, gen, bob));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1))));

        var module = Make<obj_machinery_modular_shield_module_relay>(r);
        text = Say(r, module, bob);
        Assert.Contains("It can be connected to a node or generator with a multitool.", text);

        var grav = Make<obj_machinery_power_portagrav>(r);
        text = Say(r, grav, bob);
        Assert.Contains("It is off.", text);
        Assert.Contains("The charge meter reads: 100%.", text);
        Assert.Contains("<b>Right-click</b> to toggle on.", text);

        Assert.Contains("The pipe layer is set to 3.", Say(r, Make<obj_item_pipe>(r), bob));
        Assert.Contains("You can flip the device by Right-Clicking it.", Say(r, Make<obj_item_pipe_trinary_flippable_mixer>(r), bob));
        Assert.Contains("Has a neat <i>selection menu</i> for modifying airlock access levels.", Say(r, Make<obj_item_electronics_airlock>(r), bob));
    }
}
