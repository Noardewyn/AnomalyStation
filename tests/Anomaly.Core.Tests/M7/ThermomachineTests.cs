using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice 4: the temperature control unit (atmospherics/machinery/components/unary_devices/thermomachine.dm) and the heat exchanger
/// (heat_exchanger.dm) in a booted toy room whose area requires power (an APC on a cell, as ApcTests). A freezer on a line of pipes
/// takes its port's gas to the target at tg's rate (the port's heat capacity against the unit's own, CALCULATE_CONDUCTION_ENERGY),
/// draws power through the APC as tg computes it, is clamped by the parts (RefreshParts), is switched and set from its window, and a
/// pair of heat exchangers shares heat between two pipelines. The unit tests of tg (machine_disassembly) are redone for the freezer.
/// </summary>
public class ThermomachineTests
{
    private const int Second = 20;
    private const int ApcX = 3, ApcY = 2, SourceX = 7, UnitX = 5, UnitY = 5;
    private const string Air = "o2=21;n2=79;TEMP=293.15";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc, area Room);

    private static Rig Build(ulong seed, Action<AudioRoom> populate = null)
    {
        obj_machinery_power_apc apc = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= SourceX; x++)
                Dm.@new<obj_structure_cable>(locate(x, ApcY, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(ApcX, ApcY, 1));
            Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, ApcY, 1)); // adds nothing (output_energy 0): the APC runs on its cell
        });
        mob_living_carbon_human bob = r.Join(ApcX, ApcY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var rig = new Rig(r, bob, apc, r.In(() => (area)GLOB.areas_by_type[typeof(area_misc_testroom)]));
        populate?.Invoke(r);
        return rig;
    }

    /// <summary>A unit at (UnitX, UnitY) facing east and a line of pipes east of it, made and joined the way a map template's machines are.</summary>
    private static T Line<T>(AudioRoom r, int pipes = 5) where T : obj_machinery_atmospherics_components_unary_thermomachine
    {
        var machines = new List<obj_machinery_atmospherics>();
        T unit = r.In(() => Dm.@new<T>(locate(UnitX, UnitY, 1), TRUE, EAST));
        machines.Add(unit);
        for (int i = 1; i <= pipes; i++)
            machines.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(UnitX + i, UnitY, 1), TRUE, EAST)));
        r.In(() => SSair.setup_template_machinery(Dm.list(machines.ToArray())));
        r.Step(Second);
        return unit;
    }

    private static datum_gas_mixture Port(AudioRoom r, obj_machinery_atmospherics_components_unary u) => r.In(() => (datum_gas_mixture)u.airs[1]);

    private static datum_pipeline Net(AudioRoom r, obj_machinery_atmospherics_components_unary u) => r.In(() => (datum_pipeline)u.parents[1]);

    /// <summary>The unit's port gets exactly this gas (it starts empty at the cosmic background).</summary>
    private static void Fill(AudioRoom r, obj_machinery_atmospherics_components_unary u, string mix = Air) => r.In(() =>
    {
        datum_gas_mixture port = (datum_gas_mixture)u.airs[1];
        Clear(port);
        port.merge(SSair.parse_gas_string(mix) as datum_gas_mixture);
    });

    private static void Fill(AudioRoom r, datum_pipeline net, string mix = Air) => r.In(() =>
    {
        net.air.merge(SSair.parse_gas_string(mix) as datum_gas_mixture);
        net.update = TRUE; // the next SSair cycle reconciles the machines' airs with the pipes' (datum_pipeline.dm:reconcile_air)
    });

    // an examine's lines: a list, or the text the lines concatenate to when the parent's examine gave nothing (DM's null + text)
    private static string Said(object examined) => examined is DmList l ? string.Join(" ", Dm.each(l).Select(Dm.str)) : Dm.str(examined);

    private static void Clear(datum_gas_mixture gas)
    {
        if (Dm.num(gas.total_moles()) > 0)
            gas.remove(gas.total_moles());
    }

    private static datum_tgui Window(AudioRoom r, mob who, atom machine, string ui) => r.In(() => Dm.@new<datum_tgui>(who, machine, ui, "Temperature control unit"));

    private static object Act(AudioRoom r, obj_machinery_atmospherics_components_unary_thermomachine unit, datum_tgui ui, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return r.In(() => Waiting.NoSleep(unit.ui_act(action, p, ui, null)));
    }

        [Fact]
    public void AFreezerHasItsPartsRatingFromTheBoardAndTheMapDefaults()
    {
        var g = Build(7401);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            // the board's default parts (circuitboard.dm:141): two matter bins, two micro lasers, cable and glass; RefreshParts (thermomachine.dm:85)
            Assert.IsType<obj_item_circuitboard_machine_thermomachine>(unit.circuit_value);
            Assert.Equal(2, Dm.each<datum_stock_part_matter_bin>(unit.component_parts).Count());
            Assert.Equal(2, Dm.each<datum_stock_part_micro_laser>(unit.component_parts).Count());
            Assert.Equal(5000f, unit.heat_capacity); // 5000 * (2 - 1) ** 2
            Assert.Equal(73.15f, unit.min_temperature, 2); // T0C - (170 + 2 * 15): "73.15K with T1 stock parts"
            Assert.Equal(573.15f, unit.max_temperature, 2); // T20C + 140 * 2
            Assert.Equal(T20C, unit.target_temperature);
            Assert.Equal(0f, unit.on); // a plain freezer starts off
            Assert.Equal(EAST, unit.dir);
            Assert.NotNull(unit.nodes[1]);
            Assert.Equal("thermo_base", unit.icon_state);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerCoolsItsPortsGasTowardsTheTargetAtTgsRate()
    {
        var g = Build(7402);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        var ui = Window(g.R, g.Bob, unit, "ThermoMachine");
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "power")));
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "target", ("target", 100f))));
        Fill(g.R, unit);
        g.R.In(() =>
        {
            Assert.Equal(1f, unit.on);
            Assert.Equal(100f, unit.target_temperature);
            // process_atmos (thermomachine.dm:173): the port's temperature moves by the unit's share of the combined heat capacity
            datum_gas_mixture port = (datum_gas_mixture)unit.airs[1];
            float temperature = 293.15f;
            for (int i = 0; i < 5; i++)
            {
                float c1 = Dm.num(port.heat_capacity());
                float cf = unit.heat_capacity;
                float delta = 100f - port.temperature;
                float heat = delta * (c1 * (cf / (c1 + cf))); // CALCULATE_CONDUCTION_ENERGY
                float expected = Math.Max((port.temperature * c1 + heat) / c1, TCMB);
                unit.process_atmos();
                Assert.Equal(expected, port.temperature, 3);
                Assert.True(port.temperature < temperature && port.temperature > 100f, "the port cools towards the target: " + port.temperature);
                temperature = port.temperature;
            }
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerNeverCoolsThePortBelowTheCosmicBackground()
    {
        var g = Build(7403);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            unit.target_temperature = 0; // the window clamps to min_temperature; this is the process's own floor
            Fill(g.R, unit, "o2=0.01;TEMP=3");
            unit.heat_capacity = 1e9f;
            unit.set_on(TRUE);
        });
        g.R.In(() =>
        {
            unit.set_on(TRUE);
            unit.process_atmos();
            Assert.Equal(TCMB, Port(g.R, unit).temperature, 3);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerDrawsPowerThroughTheApcAsTgComputesIt()
    {
        var g = Build(7404);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        var ui = Window(g.R, g.Bob, unit, "ThermoMachine");
        Act(g.R, unit, ui, "power");
        Act(g.R, unit, ui, "target", ("target", 100f));
        Fill(g.R, unit);
        // stop the area's own cycle from moving the counters between the reads: the unit processes by hand, in one tick
        g.R.In(() =>
        {
            datum_gas_mixture port = (datum_gas_mixture)unit.airs[1];
            float c1 = Dm.num(port.heat_capacity());
            float heat = (100f - port.temperature) * (c1 * (unit.heat_capacity / (c1 + unit.heat_capacity)));
            // thermomachine.dm:197-203
            float heatAmount = Math.Min(Math.Abs(heat), 1e8f) * 0.01f;
            float expected = unit.idle_power_usage + (float)Math.Pow(heatAmount * 0.05f, 1.05f - (5e7f * 0.16f / Math.Max(heatAmount, 5e7f)));
            float usedBefore = Dm.num(g.Room.energy_usage[unit.power_channel]);
            float cellBefore = g.Apc.cell.charge;
            unit.process_atmos();
            float used = Dm.num(g.Room.energy_usage[unit.power_channel]) - usedBefore;
            Assert.True(expected > unit.idle_power_usage, "a cooling unit draws more than its idle power: " + expected);
            Assert.InRange(used, expected - 0.5f, expected + 0.5f); // the area's counters are floats of a million: quarter-joule steps
            Assert.InRange(cellBefore - g.Apc.cell.charge, expected - 0.5f, expected + 0.5f); // no grid power: the APC's cell pays (power.dm:159)
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnIdleOrEmptyFreezerDrawsNoProcessPower()
    {
        var g = Build(7405);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            float before = Dm.num(g.Room.energy_usage[unit.power_channel]);
            unit.process_atmos(); // off
            Assert.Equal(before, Dm.num(g.Room.energy_usage[unit.power_channel]));
            unit.set_on(TRUE);
            Clear((datum_gas_mixture)unit.airs[1]);
            unit.process_atmos(); // "Nothing to cool? go home lad"
            Assert.Equal(before, Dm.num(g.Room.energy_usage[unit.power_channel]));
            Assert.Equal(1f, unit.on);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnpoweredUnitSwitchesItselfOff()
    {
        var g = Build(7406);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        Fill(g.R, unit);
        g.R.In(() =>
        {
            unit.set_on(TRUE);
            Waiting.NoSleep(unit.set_machine_stat(Dm.bitor(unit.machine_stat, NOPOWER)));
            Assert.False(Dm.truthy(unit.is_operational));
            unit.process_atmos();
            Assert.Equal(0f, unit.on); // thermomachine.dm:181
            Assert.Equal("thermo_base", unit.icon_state);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASwitchedOnFreezerCoolsItsPipelineOverSeconds()
    {
        var g = Build(7407);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        var net = Net(g.R, unit);
        Fill(g.R, net);
        var ui = Window(g.R, g.Bob, unit, "ThermoMachine");
        Act(g.R, unit, ui, "target", ("target", 150f));
        g.R.Step(Second);
        float start = g.R.In(() => net.air.temperature);
        Assert.Equal(293.15f, start, 1);
        Act(g.R, unit, ui, "power");
        g.R.Step(Second);
        float t1 = g.R.In(() => net.air.temperature);
        g.R.Step(10 * Second);
        float t2 = g.R.In(() => net.air.temperature);
        Assert.True(t1 < start, $"the pipeline cools: {start} -> {t1}");
        Assert.True(t2 < t1 && t2 > 150f, $"and keeps cooling towards 150 K: {t1} -> {t2}");
        // the picture: lit and green-blue for a cold target, the pipe overlay
        g.R.In(() =>
        {
            Assert.Equal("thermo_1", unit.icon_state);
            Assert.Equal(COLOR_BLUE, unit.greyscale_colors);
            Assert.NotEmpty(Dm.each(unit.managed_overlays is DmList l ? l : Dm.list(unit.managed_overlays)).ToList()); // the pipe's picture (update_overlays)
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AHeaterHeatsToItsTargetAndStopsAtItsMaximum()
    {
        var g = Build(7408);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_heater>(g.R);
        var ui = Window(g.R, g.Bob, unit, "ThermoMachine");
        Act(g.R, unit, ui, "power");
        Act(g.R, unit, ui, "target", ("target", 5000f));
        Fill(g.R, unit);
        g.R.In(() =>
        {
            Assert.Equal(unit.max_temperature, unit.target_temperature); // the window clamps (thermomachine.dm:319)
            Assert.Equal(573.15f, unit.max_temperature, 2);
            datum_gas_mixture port = (datum_gas_mixture)unit.airs[1];
            float before = port.temperature;
            unit.process_atmos();
            Assert.True(port.temperature > before, "the heater heats: " + port.temperature);
            for (int i = 0; i < 400; i++)
                unit.process_atmos();
            Assert.InRange(port.temperature, 573f, 573.16f);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void BetterPartsChangeTheRateAndTheRange()
    {
        var g = Build(7409);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        var better = Line2<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            Fill(g.R, unit);
            Fill(g.R, better);
            // tier 3 bins and lasers in place of the board's (RefreshParts reads the datums in component_parts)
            for (int i = 1; i <= better.component_parts.len; i++)
            {
                if (better.component_parts[i] is datum_stock_part_matter_bin)
                    better.component_parts[i] = GLOB.stock_part_datums[typeof(datum_stock_part_matter_bin_tier3)];
                else if (better.component_parts[i] is datum_stock_part_micro_laser)
                    better.component_parts[i] = GLOB.stock_part_datums[typeof(datum_stock_part_micro_laser_tier3)];
            }
            Waiting.NoSleep(better.RefreshParts());
            Assert.Equal(125000f, better.heat_capacity); // 5000 * (3 + 3 - 1) ** 2
            Assert.Equal(13.15f, better.min_temperature, 2); // T0C - (170 + 6 * 15)
            Assert.Equal(1133.15f, better.max_temperature, 2); // T20C + 140 * 6
            foreach (var u in new[] { unit, better })
            {
                u.set_on(TRUE);
                u.target_temperature = 100f;
            }
            unit.process_atmos();
            better.process_atmos();
            // each unit moves the port by its share of the combined capacity: cf / (c1 + cf)
            float c1 = Dm.num(Port(g.R, unit).heat_capacity());
            float cooledPlain = 293.15f - Port(g.R, unit).temperature;
            float cooledBetter = 293.15f - Port(g.R, better).temperature;
            Assert.Equal(193.15f * 5000f / (c1 + 5000f), cooledPlain, 2);
            Assert.Equal(193.15f * 125000f / (c1 + 125000f), cooledBetter, 2);
            Assert.True(cooledBetter > cooledPlain, $"the unit with the larger heat capacity cools faster: {cooledBetter} against {cooledPlain}");
        });
        g.R.AssertNoRuntimes();
    }

    private static T Line2<T>(AudioRoom r) where T : obj_machinery_atmospherics_components_unary_thermomachine
    {
        // a second unit with its own pipes, three rows up
        var machines = new List<obj_machinery_atmospherics>();
        T unit = r.In(() => Dm.@new<T>(locate(UnitX, UnitY + 2, 1), TRUE, EAST));
        machines.Add(unit);
        for (int i = 1; i <= 3; i++)
            machines.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(UnitX + i, UnitY + 2, 1), TRUE, EAST)));
        r.In(() => SSair.setup_template_machinery(Dm.list(machines.ToArray())));
        r.Step(Second);
        return unit;
    }

    [Fact]
    public void TheWindowSwitchesAndSetsTheTargetAsTgsActsDo()
    {
        var g = Build(7410);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        var ui = Window(g.R, g.Bob, unit, "ThermoMachine");
        g.R.In(() =>
        {
            var data = (DmList)Waiting.NoSleep(unit.ui_data(g.Bob));
            Assert.Equal(0f, Dm.num(data["on"]));
            Assert.Equal(73.15f, Dm.num(data["min"]), 2);
            Assert.Equal(573.15f, Dm.num(data["max"]), 2);
            Assert.Equal(T20C, Dm.num(data["target"]));
            Assert.Equal(T20C, Dm.num(data["initial"]));
            Assert.Equal(Dm.num(Port(g.R, unit).return_pressure()), Dm.num(data["pressure"]));
        });
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "power")));
        g.R.In(() =>
        {
            Assert.Equal(1f, unit.on);
            Assert.Equal(ACTIVE_POWER_USE, unit.use_power); // update_use_power(ACTIVE_POWER_USE) (thermomachine.dm:302)
            Assert.Equal("thermo_1", unit.icon_state);
        });
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "power")));
        g.R.In(() =>
        {
            Assert.Equal(0f, unit.on);
            Assert.Equal(IDLE_POWER_USE, unit.use_power);
            Assert.Equal("thermo_base", unit.icon_state);
        });
        // "adjust" adds to the target, a number sets it, both are clamped to the range
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "target", ("target", 0f), ("adjust", -50f))));
        Assert.Equal(243.15f, g.R.In(() => Dm.num(unit.target_temperature)), 2);
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "target", ("target", 20f))));
        Assert.Equal(73.15f, g.R.In(() => Dm.num(unit.target_temperature)), 2);
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "target", ("target", "400"))));
        Assert.Equal(400f, g.R.In(() => Dm.num(unit.target_temperature)));
        Assert.True(Dm.truthy(Act(g.R, unit, ui, "target", ("target", 9999f))));
        Assert.Equal(573.15f, g.R.In(() => Dm.num(unit.target_temperature)), 2);
        // a target that is no number changes nothing
        Assert.False(Dm.truthy(Act(g.R, unit, ui, "target", ("target", "warm"))));
        Assert.Equal(573.15f, g.R.In(() => Dm.num(unit.target_temperature)), 2);
        // the target picks the colour of the unit (update_icon_state: hot target red, cold blue)
        g.R.In(() => Assert.Equal(COLOR_ORANGE, unit.greyscale_colors));
        Act(g.R, unit, ui, "target", ("target", 150f));
        g.R.In(() => Assert.Equal(COLOR_BLUE, unit.greyscale_colors));
        Act(g.R, unit, ui, "target", ("target", 100f));
        g.R.In(() => Assert.Equal(COLOR_VIOLET, unit.greyscale_colors)); // below BODYTEMP_COLD_WARNING_3
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheClicksTheToolsAndThePanelFollowTg()
    {
        var g = Build(7411);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            // ctrl-click toggles (thermomachine.dm:324), alt-click cycles T20C -> max -> min -> T20C (:154)
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(unit.click_ctrl(g.Bob)));
            Assert.Equal(1f, unit.on);
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(unit.click_alt(g.Bob)));
            Assert.Equal(unit.max_temperature, unit.target_temperature);
            Waiting.NoSleep(unit.click_alt(g.Bob));
            Assert.Equal(unit.min_temperature, unit.target_temperature);
            Waiting.NoSleep(unit.click_alt(g.Bob));
            Assert.Equal(T20C, unit.target_temperature);
            // the screwdriver wants the unit off and anchored (:207), then opens the panel, which closes the window and the clicks
            var driver = Dm.@new<obj_item_screwdriver>(g.Bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(unit.screwdriver_act(g.Bob, driver)));
            Assert.False(Dm.truthy(unit.panel_open)); // it is on: "turn off!"
            Waiting.NoSleep(unit.click_ctrl(g.Bob));
            Assert.Equal(0f, unit.on);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(unit.screwdriver_act(g.Bob, driver)));
            Assert.True(Dm.truthy(unit.panel_open));
            Assert.Equal("thermo-open", unit.icon_state);
            Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(unit.click_ctrl(g.Bob)));
            Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(unit.click_alt(g.Bob)));
            // the multitool changes the layer and the colour with the panel open (:223, :234)
            var tool = Dm.@new<obj_item_multitool>(g.Bob.loc);
            Assert.Equal(3f, Dm.num(unit.piping_layer));
            Waiting.NoSleep(unit.multitool_act(g.Bob, tool));
            Assert.Equal(4f, Dm.num(unit.piping_layer));
            float color = unit.color_index;
            Waiting.NoSleep(unit.multitool_act_secondary(g.Bob, tool));
            Assert.Equal(color + 1, unit.color_index);
            // the wrench turns it
            var wrench = Dm.@new<obj_item_wrench>(g.Bob.loc);
            float dir = unit.dir;
            Waiting.NoSleep(unit.wrench_act(g.Bob, wrench));
            Assert.NotEqual(dir, unit.dir);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePanelClosesTheWindowAndTheCircuitBoardCyclesItsLayer()
    {
        var g = Build(7412);
        var unit = Line<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(g.R);
        g.R.In(() =>
        {
            // ui_interact opens nothing with the panel open (:272)
            unit.set_panel_open(TRUE);
            Waiting.NoSleep(unit.ui_interact(g.Bob));
            Assert.Null(SStgui.get_open_ui(g.Bob, unit));
            unit.set_panel_open(FALSE);
            // a unit made inactive for the mapmakers (interactive = FALSE) shows no window: ui_status closes
            unit.interactive = FALSE;
            Assert.Equal(UI_CLOSE, Waiting.NoSleep(unit.ui_status(g.Bob, null)));
            unit.interactive = TRUE;

            var board = unit.circuit_value as obj_item_circuitboard_machine_thermomachine;
            Assert.Equal(3f, board.pipe_layer);
            var tool = Dm.@new<obj_item_multitool>(g.Bob.loc);
            for (int expected = 4; expected <= 5; expected++)
            {
                Waiting.NoSleep(board.multitool_act(g.Bob, tool));
                Assert.Equal((float)expected, board.pipe_layer);
            }
            Waiting.NoSleep(board.multitool_act(g.Bob, tool));
            Assert.Equal(1f, board.pipe_layer); // PIPING_LAYER_MIN after the maximum
            Assert.Contains("It is set to layer 1.", Said(Waiting.NoSleep(board.examine(g.Bob))));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerBuiltFromTheBoardTakesItsLayerAndTheDisassemblyLeavesTheBoard()
    {
        // tg's unit test machine_disassembly (code/modules/unit_tests/machine_disassembly.dm): the freezer taken apart leaves its circuit board
        var g = Build(7413);
        var board = g.R.In(() => Dm.@new<obj_item_circuitboard_machine_thermomachine>(locate(8, 7, 1)));
        g.R.In(() => board.pipe_layer = 5);
        var built = g.R.In(() =>
        {
            var freezer = Dm.@new<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(locate(8, 8, 1), board);
            Waiting.NoSleep(freezer.on_construction(g.Bob));
            return freezer;
        });
        g.R.In(() =>
        {
            Assert.Equal(5f, Dm.num(built.piping_layer)); // on_construction (:72): the board's layer
            Assert.Same(board, built.circuit_value);
            Waiting.NoSleep(built.deconstruct(TRUE));
        });
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.True(QDELETED(built));
            Assert.Contains(Dm.each_cast<atom>(locate(8, 8, 1).contents).ToList(), a => a is obj_item_circuitboard_machine_thermomachine);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerThatIsOnAtTheMapStartsAtItsLowestTemperature()
    {
        var g = Build(7414);
        var on = g.R.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_thermomachine_freezer_on>(locate(8, 6, 1), TRUE, EAST));
        var cold = g.R.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_thermomachine_freezer_on_coldroom>(locate(8, 8, 1), TRUE, EAST));
        var heater = g.R.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_thermomachine_heater_on>(locate(10, 6, 1), TRUE, EAST));
        g.R.In(() =>
        {
            Assert.Equal(1f, on.on);
            Assert.Equal(on.min_temperature, on.target_temperature); // freezer/on (thermomachine.dm:363)
            Assert.Equal(COLD_ROOM_TEMP, cold.target_temperature); // coldroom (:376)
            // the target is set after the init's update_appearance, as tg: the colour follows with the next update
            Assert.Equal(COLOR_VIBRANT_LIME, cold.greyscale_colors);
            cold.update_appearance(UPDATE_ICON);
        });
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Equal(COLOR_CYAN, cold.greyscale_colors);
            Assert.Equal(1f, heater.on);
            Assert.Equal(T20C, heater.target_temperature); // a heater that is on keeps the room temperature target
            Assert.Equal("thermo_1", on.icon_state);
        });
        g.R.AssertNoRuntimes();
    }

    // ---- the heat exchanger (heat_exchanger.dm) ----

    [Fact]
    public void AHeatExchangerPairSharesHeatBetweenTwoPipelines()
    {
        var g = Build(7415);
        var r = g.R;
        // A faces east, B west of it faces west: back to back, each with a pipe line on its own side
        var a = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_heat_exchanger>(locate(7, 5, 1), TRUE, EAST));
        var b = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_heat_exchanger>(locate(6, 5, 1), TRUE, WEST));
        var machines = new List<obj_machinery_atmospherics> { a, b };
        for (int x = 8; x <= 10; x++)
            machines.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, 5, 1), TRUE, EAST)));
        for (int x = 3; x <= 5; x++)
            machines.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, 5, 1), TRUE, EAST)));
        r.In(() => SSair.setup_template_machinery(Dm.list(machines.ToArray())));
        r.Step(Second);
        var netA = r.In(() => (datum_pipeline)a.parents[1]);
        var netB = r.In(() => (datum_pipeline)b.parents[1]);
        Assert.NotNull(netA);
        Assert.NotNull(netB);
        Assert.NotSame(netA, netB);
        r.In(() =>
        {
            Assert.Same(b, a.partner_ref.resolve()); // atmos_init (heat_exchanger.dm:37): each is the other's partner
            Assert.Same(a, b.partner_ref.resolve());
        });
        Fill(r, netA, "o2=100;TEMP=400");
        Fill(r, netB, "o2=100;TEMP=200");
        r.In(() =>
        {
            // process_atmos (:51): both air contents take the capacity-weighted mean temperature at once
            datum_gas_mixture airA = (datum_gas_mixture)a.airs[1];
            datum_gas_mixture airB = (datum_gas_mixture)b.airs[1];
            Clear(airA);
            Clear(airB);
            airA.merge(SSair.parse_gas_string("o2=10;TEMP=400") as datum_gas_mixture);
            airB.merge(SSair.parse_gas_string("n2=30;TEMP=200") as datum_gas_mixture);
            float ca = Dm.num(airA.heat_capacity()), cb = Dm.num(airB.heat_capacity());
            float mean = (400 * ca + 200 * cb) / (ca + cb);
            a.update_cycle = SSair.times_fired - 1;
            a.process_atmos();
            Assert.Equal(mean, airA.temperature, 3);
            Assert.Equal(mean, airB.temperature, 3);
            // once per SSair cycle: the partner's call in the same cycle does nothing (update_cycle)
            airA.temperature = 500;
            b.process_atmos();
            Assert.Equal(500f, airA.temperature);
        });
        // and in time the two pipelines meet in between
        float hot0 = r.In(() => netA.air.temperature), cold0 = r.In(() => netB.air.temperature);
        r.Step(60 * Second);
        float hot = r.In(() => netA.air.temperature), cold = r.In(() => netB.air.temperature);
        Assert.True(hot < hot0 && cold > cold0, $"the pipelines share heat: {hot0} -> {hot}, {cold0} -> {cold}");
        Assert.True(hot - cold < (hot0 - cold0) / 2, $"and the gap closes: {hot0 - cold0} -> {hot - cold}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeatExchangerWithoutAPartnerDoesNothing()
    {
        var g = Build(7416);
        var a = g.R.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_heat_exchanger>(locate(7, 5, 1), TRUE, EAST));
        g.R.Step(Second);
        g.R.In(() =>
        {
            Assert.Null(a.partner_ref);
            ((datum_gas_mixture)a.airs[1]).merge(SSair.parse_gas_string("o2=10;TEMP=400") as datum_gas_mixture);
            a.process_atmos();
            Assert.Equal(400f, ((datum_gas_mixture)a.airs[1]).temperature, 3);
            Assert.Equal("he0", a.icon_state); // no pipe on its node: update_icon_state (:26)
        });
        g.R.AssertNoRuntimes();
    }

    // ---- the meter (meter.dm) ----

    [Fact]
    public void AMeterReadsItsPipesPressureAndTemperatureAndShowsThemInItsPicture()
    {
        var g = Build(7417);
        var r = g.R;
        var pipe = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(8, 5, 1), TRUE, EAST));
        var pipe2 = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(9, 5, 1), TRUE, EAST));
        r.In(() => SSair.setup_template_machinery(Dm.list(pipe, pipe2)));
        var meter = r.In(() => Dm.@new<obj_machinery_meter>(locate(8, 5, 1)));
        r.Step(Second);
        r.In(() => Assert.Same(pipe, meter.target));
        r.In(() => Assert.Contains("The pressure gauge reads", Dm.str(meter.status())));
        Fill(r, r.In(() => pipe.parent), "o2=2000;TEMP=500");
        r.Step(2 * Second);
        r.In(() =>
        {
            float pressure = Dm.num(pipe.parent.air.return_pressure());
            Assert.True(pressure > 0.15f * ONE_ATMOSPHERE, "the pipe holds gas: " + pressure);
            Assert.StartsWith("meter", meter.icon_state);
            Assert.NotEqual("meter0", meter.icon_state);
            Assert.Equal(COLOR_ORANGE, meter.greyscale_colors); // 500 K is between BODYTEMP_HEAT_WARNING_2 and _3
            Assert.Contains("500", Dm.str(meter.status()));
        });
        // cut the pipe out: the meter drops (drop_meter, :61) as a meter fitting
        r.In(() => qdel(pipe));
        r.Step(2);
        r.In(() => Assert.True(QDELETED(meter)));
        Assert.Contains(r.In(() => Dm.each_cast<atom>(locate(8, 5, 1).contents).ToList()), a => a is obj_item_pipe_meter);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchTakesAMeterOffItsPipeAsAFittingAndTheFittingGoesBackOn()
    {
        var g = Build(7418);
        var r = g.R;
        var pipe = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(8, 5, 1), TRUE, EAST));
        var pipe2 = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(9, 5, 1), TRUE, EAST));
        r.In(() => SSair.setup_template_machinery(Dm.list(pipe, pipe2)));
        var meter = r.In(() => Dm.@new<obj_machinery_meter>(locate(8, 5, 1)));
        r.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(8, 6, 1))));
        r.Step(Second);
        r.In(() => Assert.Same(pipe, meter.target));
        // the wrench (meter.dm:171): 40 deciseconds, then the meter comes off as its fitting (:182)
        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, meter, FurnitureTests.Left, 5);
        r.In(() => Assert.True(QDELETED(meter)));
        var fitting = Assert.Single(FurnitureTests.On<obj_item_pipe_meter>(r, 8, 5));
        // the fitting on the pipe: wrench (construction.dm:469) fastens a meter on the pipe of its layer
        r.In(() => fitting.setAttachLayer(3f));
        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, fitting, FurnitureTests.Left, 1);
        r.In(() => Assert.True(QDELETED(fitting)));
        var again = Assert.Single(FurnitureTests.On<obj_machinery_meter>(r, 8, 5));
        r.Step(Second);
        r.In(() => Assert.Same(pipe, again.target));
        // and with a screwdriver a fitting is fastened to the floor as a turf meter that reads the tile's air (:484, meter.dm:240)
        var spare = r.In(() => Dm.@new<obj_item_pipe_meter>(locate(10, 6, 1)));
        r.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(10, 7, 1))));
        WallTests.Hold<obj_item_screwdriver>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, spare, FurnitureTests.Left, 1);
        var onFloor = Assert.Single(FurnitureTests.On<obj_machinery_meter_turf>(r, 10, 6));
        r.Step(Second);
        r.In(() => Assert.Same(locate(10, 6, 1), onFloor.target_turf));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerIsBuiltFromAFrameItsBoardAndItsPartsAndTakesTheBoardsLayer()
    {
        var g = Build(7419);
        var r = g.R;
        r.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(7, 8, 1))));
        // a wired, anchored machine frame (machine_frame.dm) with the board set to layer 4
        var frame = r.In(() =>
        {
            var f = Dm.@new<obj_structure_frame_machine>(locate(8, 8, 1));
            f.state = FRAME_STATE_WIRED;
            Waiting.NoSleep(f.set_anchored(TRUE));
            f.update_appearance();
            return f;
        });
        var board = WallTests.Hold<obj_item_circuitboard_machine_thermomachine>(r, g.Bob);
        r.In(() => board.pipe_layer = 4);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        Assert.Equal(FRAME_STATE_BOARD_INSTALLED, r.In(() => Dm.num(frame.state)));
        // the board's parts (circuitboard req_components, machine_circuitboards.dm:417): 2 matter bins, 2 micro lasers, a cable, a glass sheet
        WallTests.Hold<obj_item_stock_parts_matter_bin>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        WallTests.Hold<obj_item_stock_parts_matter_bin>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        WallTests.Hold<obj_item_stock_parts_micro_laser>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        WallTests.Hold<obj_item_stock_parts_micro_laser>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        WallTests.Hold<obj_item_stack_cable_coil>(r, g.Bob, 5);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        WallTests.Hold<obj_item_stack_sheet_glass>(r, g.Bob, 5);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        r.In(() => Assert.All(Dm.each(frame.req_components).Select(k => Dm.num(frame.req_components[k])).ToList(), n => Assert.Equal(0f, n)));
        WallTests.Hold<obj_item_screwdriver>(r, g.Bob);
        FurnitureTests.Click(r, g.Bob, frame, FurnitureTests.Left, 1);
        r.In(() => Assert.True(QDELETED(frame)));
        var built = Assert.Single(FurnitureTests.On<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(r, 8, 8));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(board, built.circuit_value);
            Assert.Equal(4f, Dm.num(built.piping_layer)); // on_construction (thermomachine.dm:72): the board's layer
            Assert.Equal(5000f, built.heat_capacity); // RefreshParts: two tier-1 bins
            Assert.Equal(73.15f, built.min_temperature, 2);
            Assert.True(Dm.truthy(built.anchored));
            Assert.False(Dm.truthy(built.panel_open));
            Assert.Equal(2, Dm.each<datum_stock_part_matter_bin>(built.component_parts).Count());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFreezerBuiltOnAPipeOfItsOwnLayerComesUnanchoredWithItsPanelOpen()
    {
        var g = Build(7420);
        var r = g.R;
        var pipe = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(9, 8, 1), TRUE, EAST)); // layer 3, like the board's default
        var board = r.In(() => Dm.@new<obj_item_circuitboard_machine_thermomachine>(locate(9, 7, 1)));
        var freezer = r.In(() =>
        {
            var f = Dm.@new<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(locate(9, 8, 1), board);
            Waiting.NoSleep(f.on_construction(g.Bob));
            return f;
        });
        r.In(() =>
        {
            Assert.True(Dm.truthy(freezer.check_pipe_on_turf())); // thermomachine.dm:247
            Assert.False(Dm.truthy(freezer.anchored));
            Assert.True(Dm.truthy(freezer.panel_open)); // "the port is already in use!" (:80)
            Assert.Equal("thermo-open", freezer.icon_state);
            // an unanchored unit is no pipe's neighbour (is_connectable, :67), and the secondary wrench refuses with the pipe there
            Assert.False(Dm.truthy(freezer.is_connectable(pipe, 3)));
            var wrench = Dm.@new<obj_item_wrench>(g.Bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(freezer.wrench_act_secondary(g.Bob, wrench)));
            Assert.False(Dm.truthy(freezer.anchored));
        });
        r.AssertNoRuntimes();
    }
}
