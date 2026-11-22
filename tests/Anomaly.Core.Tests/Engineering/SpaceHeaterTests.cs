using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E9: the space heater and the improvised chem heater (spaceheater.dm) in a booted toy room. The heater spawns with
/// its high-capacity cell, heats or cools the air of its tile and the open tiles around it by tg's energy (the temperature gap against the
/// air's heat capacity, capped by the parts' heating power and by the cell's charge times the efficiency over the tiles) and draws that
/// from the cell; its modes (auto's one degree of tolerance, heat, cool), the hatch (the cell goes in and out only with it open, the target
/// is set only with it open), the window's data and acts, the parts' RefreshParts, the EMP, the wrench and the deconstruction; the chem
/// heater warms the beaker's reagents through adjust_thermal_energy.
/// </summary>
public class SpaceHeaterTests
{
    private const int Second = 20;
    private const int X = 5, Y = 5;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 11, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T Place<T>(AudioRoom r, int x = X, int y = Y) where T : obj_machinery
    {
        T machine = r.In(() => Dm.@new<T>(locate(x, y, 1)));
        r.Step(Second);
        return machine;
    }

    private static datum_gas_mixture Air(AudioRoom r, atom a) => r.In(() => (datum_gas_mixture)Waiting.NoSleep(((turf)a.loc).return_air()));

    private static DmList Data(AudioRoom r, obj_machinery_space_heater h, mob who) => r.In(() => (DmList)Waiting.NoSleep(h.ui_data(who)));

    private static object Act(AudioRoom r, obj_machinery_space_heater h, mob who, string action, params (string Key, object Value)[] args) => r.In(() =>
    {
        usr = who;
        datum_tgui ui = (datum_tgui)SStgui.get_open_ui(who, h);
        if (ui == null)
        {
            Waiting.NoSleep(h.ui_interact(who));
            ui = (datum_tgui)SStgui.get_open_ui(who, h);
        }
        DmList parameters = Dm.list();
        foreach ((string key, object value) in args)
            parameters[key] = value;
        return Waiting.NoSleep(h.ui_act(action, parameters, ui, null));
    });

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void AHeaterSpawnsWithAHighCapacityCellAndTgsPartsAndShowsItInTheWindow()
    {
        var (r, bob) = Room(9101);
        var h = Place<obj_machinery_space_heater>(r);
        r.In(() =>
        {
            Assert.IsType<obj_item_stock_parts_power_store_cell_high>(h.cell_value);
            Assert.Same(h, h.cell_value.loc);
            Assert.Same(h.cell_value, h.get_cell());
            Assert.Equal((0f, 1f, "sheater-off"), (Dm.num(h.anchored), Dm.num(h.density), h.icon_state));
            // the board's default parts: one micro laser and one capacitor of tier 1 (RefreshParts, :179)
            Assert.Equal((40000f, 30f, 2000f), (h.heating_energy, h.settable_temperature_range, h.efficiency));
        });

        // ui_interact opens tg's window, ui_data gives what SpaceHeater.jsx reads
        r.In(() =>
        {
            Waiting.NoSleep(h.ui_interact(bob));
            Assert.Equal("SpaceHeater", Assert.IsType<datum_tgui>(SStgui.get_open_ui(bob, h)).@interface);
        });
        DmList data = Data(r, h, bob);
        Assert.Equal((0f, 0f, "auto", 1f, 0f), (Dm.num(data["open"]), Dm.num(data["on"]), Dm.str(data["mode"]), Dm.num(data["hasPowercell"]), Dm.num(data["chemHacked"])));
        Assert.Equal(100f, Dm.num(data["powerLevel"]));
        Assert.Equal((20f, 0f, 60f), (Dm.num(data["targetTemp"]), Dm.num(data["minTemp"]), Dm.num(data["maxTemp"])));
        Assert.Equal(20f, Dm.num(data["currentTemp"]), 0.1); // the toy room's air at 293.15 K
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeatingCycleAddsTgsEnergyToEveryOpenTileAndTakesItFromTheCell()
    {
        var (r, bob) = Room(9102);
        var h = Place<obj_machinery_space_heater>(r);
        r.In(() =>
        {
            h.target_temperature = 313.15f; // 20 degrees over the room
            h.set_mode = "heat";
            h.toggle_power(); // no user: no balloon alert
            Assert.Equal((1f, "standby"), (Dm.num(h.on), h.mode));

            datum_gas_mixture air = (datum_gas_mixture)Waiting.NoSleep(((turf)h.loc).return_air());
            float t0 = air.temperature, capacity = Dm.num(air.heat_capacity()), charge0 = h.cell_value.charge;
            int tiles = (int)Dm.length(((turf)h.loc).atmos_adjacent_turfs) + 1;
            Assert.Equal(5, tiles); // the tile and its four open neighbours
            // tg: min(gap * capacity, heating_energy, charge * efficiency / tiles); the last term is far over
            float required = Math.Min(Math.Min(Math.Abs(t0 - 313.15f) * capacity, h.heating_energy), charge0 * h.efficiency / tiles);
            Assert.Equal(h.heating_energy, required); // the room's heat capacity is over 2000 J/K: the parts' power is the limit

            Assert.NotEqual(PROCESS_KILL, h.process_atmos(0.5f));
            Assert.Equal("heat", h.mode);
            Assert.Equal(t0 + required / capacity, air.temperature, 0.01);
            Assert.Equal(charge0 - required * tiles / h.efficiency, h.cell_value.charge, 0.01);
            Assert.Equal("sheater-heat", h.icon_state);
            // a neighbour got the same energy: its temperature rose by required over its own capacity
            foreach (turf_open next in Dm.each<turf_open>(((turf)h.loc).atmos_adjacent_turfs))
            {
                datum_gas_mixture other = (datum_gas_mixture)Waiting.NoSleep(next.return_air());
                Assert.True(other.temperature > t0 + 1, "the neighbouring tile is warmed: " + other.temperature);
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheModesHeatCoolAndTheAutoToleranceDecideWhatTheHeaterDoes()
    {
        var (r, bob) = Room(9103);
        var h = Place<obj_machinery_space_heater>(r);
        datum_gas_mixture air = Air(r, h);
        r.In(() => h.toggle_power());

        // auto at the default 20 degrees in a 20 degree room: inside the one degree of tolerance, standby, nothing spent
        float charge = r.In(() => h.cell_value.charge);
        r.Step(3 * Second);
        Assert.Equal("standby", r.In(() => h.mode));
        Assert.Equal("sheater-standby", r.In(() => h.icon_state));
        Assert.Equal(charge, r.In(() => h.cell_value.charge));

        // a target 10 degrees over: auto heats the tile to it at once (the parts' power over the tile's capacity), the cold room then draws
        // the heat off and the heater tops it up again, never over the target
        r.In(() => h.target_temperature = 303.15f);
        var seen = new HashSet<string>();
        float peak = 0;
        for (int i = 0; i < 12 * Second; i++)
        {
            r.Step();
            seen.Add(r.In(() => h.icon_state));
            peak = Math.Max(peak, r.In(() => air.temperature));
        }
        Assert.Contains("sheater-heat", seen);
        Assert.Contains("sheater-standby", seen);
        Assert.InRange(peak, 302.15f, 303.65f);
        float warmed = r.In(() => air.temperature);
        Assert.True(warmed > 295f, "the heated air stays warm: " + warmed);
        Assert.True(r.In(() => h.cell_value.charge) < charge, "the cell paid for it");

        // heat only never cools: a target under the air leaves the heater idle and the cell untouched
        float spent = r.In(() => h.cell_value.charge);
        r.In(() =>
        {
            h.set_mode = "heat";
            h.target_temperature = 283.15f;
        });
        r.Step(3 * Second);
        Assert.Equal("standby", r.In(() => h.mode));
        Assert.Equal("sheater-standby", r.In(() => h.icon_state));
        Assert.Equal(spent, r.In(() => h.cell_value.charge));

        // cool takes the tile down to the target at once, never under it
        r.In(() => h.set_mode = "cool");
        seen.Clear();
        float low = float.MaxValue;
        for (int i = 0; i < 12 * Second; i++)
        {
            r.Step();
            seen.Add(r.In(() => h.icon_state));
            low = Math.Min(low, r.In(() => air.temperature));
        }
        Assert.Contains("sheater-cool", seen);
        Assert.InRange(low, 282.15f, 284.15f);
        Assert.True(r.In(() => h.cell_value.charge) < spent, "cooling costs the cell too");

        // and a target over the air is left to the heat mode
        r.In(() => h.target_temperature = 313.15f);
        r.Step(3 * Second);
        Assert.Equal("standby", r.In(() => h.mode));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADrainedOrMissingCellSwitchesTheHeaterOffAndStopsItsProcessing()
    {
        var (r, bob) = Room(9104);
        var h = Place<obj_machinery_space_heater>(r);
        r.In(() =>
        {
            h.toggle_power();
            h.cell_value.charge = 1; // tg: charge <= 1 is empty
            Assert.Equal(PROCESS_KILL, h.process_atmos(0.5f));
            Assert.Equal(0f, Dm.num(h.on));
            Assert.Equal("sheater-off", h.icon_state);

            // without a cell the same, and the window disables its power button
            h.cell_value.charge = 100000;
            h.toggle_power();
            qdel(h.cell_value);
        });
        r.Step();
        r.In(() =>
        {
            Assert.Null(h.cell_value);
            Assert.Equal(PROCESS_KILL, h.process_atmos(0.5f));
            Assert.Equal(0f, Dm.num(h.on));
        });
        Assert.Equal(0f, Dm.num(Data(r, h, bob)["hasPowercell"]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowSwitchesThePowerSetsTheModeAndSetsTheTargetOnlyWithTheHatchOpen()
    {
        var (r, bob) = Room(9105);
        var h = Place<obj_machinery_space_heater>(r);

        Assert.True(Dm.truthy(Act(r, h, bob, "power")));
        Assert.Equal(1f, r.In(() => h.on));
        Assert.True(Dm.truthy(Act(r, h, bob, "mode", ("mode", "cool"))));
        Assert.Equal("cool", r.In(() => h.set_mode));
        Assert.Equal("cool", Dm.str(Data(r, h, bob)["mode"]));

        // the target needs the hatch: closed, the act is not handled
        Assert.False(Dm.truthy(Act(r, h, bob, "target", ("target", 40))));
        Assert.Equal(293.15f, r.In(() => Dm.num(h.target_temperature)));
        // and the eject of the cell too
        Assert.False(Dm.truthy(Act(r, h, bob, "eject")));
        Assert.NotNull(r.In(() => h.cell_value));

        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(h.screwdriver_act(bob, screwdriver))));
        Assert.Equal(1f, r.In(() => h.panel_open));
        Assert.Contains("sheater-open", Overlays(r, h));
        Assert.Equal(1f, Dm.num(Data(r, h, bob)["open"]));

        // 40 degrees: 313.15 K, which DM's one-argument round floors to 313; over the range it is clamped (the range 30 around 30 degrees)
        Assert.True(Dm.truthy(Act(r, h, bob, "target", ("target", 40))));
        Assert.Equal(313f, r.In(() => Dm.num(h.target_temperature)));
        Assert.True(Dm.truthy(Act(r, h, bob, "target", ("target", 500))));
        Assert.Equal(333.15f, r.In(() => Dm.num(h.target_temperature)), 0.001);
        Assert.True(Dm.truthy(Act(r, h, bob, "target", ("target", -100))));
        Assert.Equal(273.15f, r.In(() => Dm.num(h.target_temperature)), 0.001);
        Assert.False(Dm.truthy(Act(r, h, bob, "target", ("target", "warm")))); // not a number

        // the cell comes out into the hand; the heater stays on but has nothing to burn
        obj_item_stock_parts_power_store_cell cell = r.In(() => h.cell_value);
        Assert.True(Dm.truthy(Act(r, h, bob, "eject")));
        Assert.Contains(cell, r.In(() => Dm.each(bob.held_items).ToList())); // the screwdriver holds the active hand
        Assert.Null(r.In(() => h.cell_value));
        Assert.Equal(0f, Dm.num(Data(r, h, bob)["hasPowercell"]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACellGoesInOnlyThroughAnOpenHatchAndOnlyIfThereIsNone()
    {
        var (r, bob) = Room(9106);
        var h = Place<obj_machinery_space_heater>(r);
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(h.screwdriver_act(bob, screwdriver)));
        r.In(() => usr = bob);
        Assert.True(Dm.truthy(Act(r, h, bob, "eject")));
        obj_item_stock_parts_power_store_cell cell = r.In(() => Dm.each<obj_item_stock_parts_power_store_cell>(bob.held_items).Single());

        // hatch shut: "The hatch must be open to insert a power cell!"
        r.In(() => Waiting.NoSleep(h.screwdriver_act(bob, screwdriver)));
        Assert.Equal(0f, r.In(() => h.panel_open));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(h.item_interaction(bob, cell, Dm.list()))));
        Assert.Null(r.In(() => h.cell_value));

        // hatch open: in it goes, with the window refreshed; a second cell is turned away
        r.In(() => Waiting.NoSleep(h.screwdriver_act(bob, screwdriver)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(h.item_interaction(bob, cell, Dm.list()))));
        Assert.Same(cell, r.In(() => h.cell_value));
        Assert.Same(h, r.In(() => cell.loc));
        var spare = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell_high>(get_turf(bob)));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(h.item_interaction(bob, spare, Dm.list()))));
        Assert.Same(cell, r.In(() => h.cell_value));

        // anything else is not the heater's to take
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(get_turf(bob)));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(h.item_interaction(bob, wrench, Dm.list()))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARightClickTogglesThePowerWithAnEmptyHand()
    {
        var (r, bob) = Room(9107);
        var h = Place<obj_machinery_space_heater>(r);
        r.In(() => { Waiting.NoSleep(bob.forceMove(locate(X - 1, Y, 1))); });
        r.Step(Second);

        r.Command(() => ClientSeam.Click(bob.client, h, "right=1;button=right"));
        r.Step(Second);
        Assert.Equal((1f, "standby"), r.In(() => (Dm.num(h.on), h.mode)));
        r.Command(() => ClientSeam.Click(bob.client, h, "right=1;button=right"));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => h.on));
        Assert.Equal("sheater-off", r.In(() => h.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmpDrainsTheCellAndAnAnchoringWrenchAndACrowbarWorkLikeTgs()
    {
        var (r, bob) = Room(9108);
        var h = Place<obj_machinery_space_heater>(r);

        float before = r.In(() => h.cell_value.charge);
        r.In(() => Waiting.NoSleep(h.emp_act(EMP_HEAVY)));
        Assert.Equal(before - STANDARD_CELL_CHARGE / EMP_HEAVY * 3, r.In(() => h.cell_value.charge), 0.01); // the cell's emp_act: use(STANDARD_CELL_CHARGE / severity * emp_damage_modifier), 3 for the high-capacity cell

        // the wrench anchors it (two seconds) and unanchors it again
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, h, 3);
        Assert.Equal(1f, r.In(() => h.anchored));
        WallTests.Click(r, bob, h, 3);
        Assert.Equal(0f, r.In(() => h.anchored));

        // a crowbar takes it apart only with the hatch open: the cell, the board and the parts drop to the floor
        var crowbar = WallTests.Hold<obj_item_crowbar>(r, bob);
        obj_item_stock_parts_power_store_cell cell = r.In(() => h.cell_value);
        r.In(() => Assert.NotEqual(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(h.crowbar_act(bob, crowbar))));
        Assert.False(r.In(() => QDELETED(h)));
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(h.screwdriver_act(bob, screwdriver)));
        crowbar = WallTests.Hold<obj_item_crowbar>(r, bob);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(h.crowbar_act(bob, crowbar))));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(h)));
        Assert.False(r.In(() => QDELETED(cell)));
        Assert.Equal(r.In(() => locate(X, Y, 1)), r.In(() => cell.loc));
        r.In(() => Assert.Contains(Dm.each_cast<atom>(locate(X, Y, 1).contents), a => a is obj_item_circuitboard_machine_space_heater or obj_structure_frame_machine));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BetterPartsWidenTheRangeAndRaiseThePowerAndTheEfficiency()
    {
        var (r, bob) = Room(9109);
        var h = Place<obj_machinery_space_heater>(r);
        r.In(() =>
        {
            h.target_temperature = 200f;
            for (int i = 1; i <= h.component_parts.Count; i++)
            {
                if (h.component_parts[i] is datum_stock_part_micro_laser)
                    h.component_parts[i] = GLOB.stock_part_datums[typeof(datum_stock_part_micro_laser_tier3)];
                else if (h.component_parts[i] is datum_stock_part_capacitor)
                    h.component_parts[i] = GLOB.stock_part_datums[typeof(datum_stock_part_capacitor_tier3)];
            }
            Waiting.NoSleep(h.RefreshParts());
            // laser 3: 3 * 40 kJ; capacitor 3: range 3 * 30, efficiency (3 + 1) * 2000 * 0.5; the target is pulled into the new range
            Assert.Equal((120000f, 90f, 4000f), (h.heating_energy, h.settable_temperature_range, h.efficiency));
            Assert.Equal(213.15f, Dm.num(h.target_temperature), 0.001); // 303.15 - 90
        });
        DmList data = Data(r, h, bob);
        Assert.Equal(-60f, Dm.num(data["minTemp"]), 0.01);
        Assert.Equal(120f, Dm.num(data["maxTemp"]), 0.01);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImprovisedChemHeaterWarmsTheBeakersReagentsFromItsCell()
    {
        var (r, bob) = Room(9110);
        var h = Place<obj_machinery_space_heater_improvised_chem_heater>(r);
        r.In(() =>
        {
            Assert.Equal((1f, null), (Dm.num(h.panel_open), h.cell_value));
            Assert.Equal(0.1f, h.beaker_conduction_power, 0.0001); // (1 + 1) * 0.5 * 0.1, under 1 SECONDS / the machines' wait
            Assert.Equal(50f, h.settable_temperature_range);
        });
        DmList data = Data(r, h, bob);
        Assert.Equal((1f, "N/A", 0f), (Dm.num(data["chemHacked"]), Dm.str(data["currentTemp"]), Dm.num(data["hasPowercell"])));

        // a power cell goes in by hand (the hatch is always open), then a beaker of water
        var cell = WallTests.Hold<obj_item_stock_parts_power_store_cell_high>(r, bob);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(h.item_interaction(bob, cell, Dm.list()))));
        Assert.Same(cell, r.In(() => h.cell_value));
        var beaker = WallTests.Hold<obj_item_reagent_containers_cup_beaker>(r, bob);
        r.In(() => Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 50)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(h.item_interaction(bob, beaker, Dm.list()))));
        Assert.Same(beaker, r.In(() => h.beaker));
        Assert.Same(h, r.In(() => beaker.loc));
        Assert.Contains("sheater-off-beaker", Overlays(r, h));
        Assert.Contains("sheater-rigged", Overlays(r, h));
        Assert.Contains("sheater-open", Overlays(r, h));
        Assert.Equal((1f, 26f), (Dm.num(Data(r, h, bob)["chemHacked"]), Dm.num(Data(r, h, bob)["currentTemp"]))); // water joins at 300 K: 26.85 degrees, floored

        // 60 degrees in heat mode: one machines cycle moves the reagents by tg's energy and spends the cell's
        r.In(() =>
        {
            h.target_temperature = 333.15f;
            h.set_mode = "heat";
            h.toggle_power();
            datum_reagents reagents = (datum_reagents)beaker.reagents;
            float t0 = reagents.chem_temp, capacity = Dm.num(reagents.heat_capacity()), charge0 = h.cell_value.charge;
            float required = Math.Min(Math.Min(Math.Abs(333.15f - t0) * 0.1f * 2f * capacity, h.heating_energy), charge0 * h.efficiency);
            Assert.NotEqual(PROCESS_KILL, h.process(2f));
            Assert.Equal(t0 + required / capacity, reagents.chem_temp, 0.01);
            Assert.Equal(charge0 - required / h.efficiency, h.cell_value.charge, 0.01);
            Assert.True(reagents.chem_temp > t0);
            Assert.Equal("sheater-heat", h.icon_state);
        });
        Assert.Contains("sheater-heat-beaker", Overlays(r, h));

        // run on its own, the reagents keep climbing to the target
        float start = r.In(() => ((datum_reagents)beaker.reagents).chem_temp);
        r.Step(40 * Second);
        float now = r.In(() => ((datum_reagents)beaker.reagents).chem_temp);
        Assert.True(now > start + 5, $"the water warmed {start} -> {now}");
        Assert.True(now <= 333.15f + 0.01f);

        // the window's beaker button and the alt click give the beaker back; a heater without a beaker or cell stops
        r.In(() => usr = bob);
        Assert.True(Dm.truthy(Act(r, h, bob, "ejectBeaker")));
        Assert.Contains(beaker, r.In(() => Dm.each(bob.held_items).ToList()));
        Assert.Null(r.In(() => h.beaker));
        Assert.Equal(PROCESS_KILL, r.In(() => h.process(2f)));
        Assert.Equal(0f, r.In(() => h.on));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACraftedChemHeaterIsMovedToTheCellNearItsCrafterAndTakesIt()
    {
        var (r, bob) = Room(9111);
        var h = Place<obj_machinery_space_heater_improvised_chem_heater>(r, X + 2, Y + 2);
        var cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell_high>(get_turf(bob)));
        r.In(() =>
        {
            usr = bob; // tg's range(1) is centred on the crafter, the usr
            Waiting.NoSleep(h.on_craft_completion(Dm.list(), null, bob));
        });
        r.Step();
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => h.loc)); // forceMove(get_turf(cell))
        Assert.Same(cell, r.In(() => h.cell_value));
        Assert.Same(h, r.In(() => cell.loc));
        r.AssertNoRuntimes();
    }
}
