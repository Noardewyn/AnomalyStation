using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Package E12a in a booted toy room: the crystallizer (gas_recipe_machines/crystallizer.dm, atmos_machines_recipes.dm) draws the
/// selected recipe's gases from its input port, makes tg's product south of itself at tg's rate, with tg's quality name, consumes
/// the recipe's moles, loses quality outside the temperature window, exchanges heat with its coolant port, dumps its gases back on
/// a recipe change, and is taken apart into its board with its gas released; the products of crystallizer_items.dm work (the
/// hyper-noblium crystal on a canister and on clothing). The electrolyzer (electrolyzer.dm) runs on an inserted cell only with its
/// panel closed, turns the tile's water vapour into hydrogen and oxygen at its parts' rate, draws tg's power from the cell, ejects
/// the cell from its window and stops without one, and runs on the area's power when wrenched down.
/// </summary>
public class CrystallizerElectrolyzerTests
{
    private const int Second = 20;
    private const int MX = 6, MY = 5; // the machine; its products land at (MX, MY - 1)

    private static obj_machinery_atmospherics_components_binary_crystallizer Crystallizer(AudioRoom r)
    {
        var machine = r.In(() => Dm.@new<obj_machinery_atmospherics_components_binary_crystallizer>(locate(MX, MY, 1), TRUE, EAST));
        r.In(() => SSair.setup_template_machinery(Dm.list(machine)));
        r.Step(Second);
        return machine;
    }

    private static object Act(AudioRoom r, obj_machinery machine, mob who, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return r.In(() => Waiting.NoSleep(machine.ui_act(action, p, Dm.@new<datum_tgui>(who, machine, "test", "test"), null)));
    }

    private static void Feed(AudioRoom r, obj_machinery_atmospherics_components_binary_crystallizer c, int port, string mix) => r.In(() =>
        ((datum_gas_mixture)c.airs[port]).merge(SSair.parse_gas_string(mix) as datum_gas_mixture));

    private static float Moles(datum_gas_mixture air, Type gas) => Dm.num(air.moles[gas]);

    [Fact]
    public void TheRecipeListHoldsTgsCrystallizerRecipesAndTheWindowOffersThem()
    {
        var (r, bob) = Room(12001);
        var c = Crystallizer(r);
        r.In(() =>
        {
            // subtypesof(/datum/gas_recipe) holds the crystallizer's own base type too (id "", name ""), so tg's window offers a
            // blank button after Nothing
            Assert.Equal(18f, GLOB.gas_recipe_meta.len);
            Assert.IsType<datum_gas_recipe_crystallizer>(GLOB.gas_recipe_meta[""]);
            var recipe = (datum_gas_recipe)GLOB.gas_recipe_meta["hyper_crystalium"];
            Assert.Equal(("Hyper-Noblium Crystal", "Crystallizer", 3f, 250f, -250000f), (recipe.name, recipe.machine_type, recipe.min_temp, recipe.max_temp, recipe.energy_release));
            Assert.Equal(1000f, Dm.num(recipe.requirements[typeof(datum_gas_oxygen)]));
            Assert.Equal(1f, ((datum_gas_recipe)GLOB.gas_recipe_meta["crystal_shard"]).dangerous);

            var data = (DmList)Waiting.NoSleep(c.ui_static_data(bob));
            DmList offered = (DmList)data["selected_recipes"];
            Assert.Equal(19f, offered.len);
            Assert.Equal("Nothing", Dm.str(((DmList)offered[1])["name"]));
            Assert.Equal("", Dm.str(((DmList)offered[1])["id"]));
            Assert.Contains(Dm.each_cast<DmList>(offered), o => Dm.str(o["id"]) == "plasma_sheet" && Dm.str(o["name"]) == "Solidified Plasma");

            // its picture: off, the two coloured pipe stubs (update_overlays :70)
            Assert.Equal("crystallizer-off", Dm.str(c.icon_state));
            var data2 = (DmList)Waiting.NoSleep(c.ui_data(bob));
            Assert.Equal("Select a recipe to see the requirements", Dm.str(data2["requirements"]));
            Assert.Equal("", Dm.str(data2["selected"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrystallizerMakesAHyperNobliumCrystalFromItsInputAtTheRightTemperature()
    {
        var (r, bob) = Room(12002);
        var c = Crystallizer(r);
        Act(r, c, bob, "recipe", ("mode", "hyper_crystalium"));
        Act(r, c, bob, "gas_input", ("gas_input", 1));
        Act(r, c, bob, "power");
        r.In(() =>
        {
            Assert.Equal(1f, c.on);
            Assert.Equal("crystallizer-on", Dm.str(c.icon_state));
            Assert.Equal(1085f, c.total_recipe_moles); // moles_calculations (:155)
            var data = (DmList)Waiting.NoSleep(c.ui_data(bob));
            Assert.Equal("To create Hyper-Noblium Crystal you will need:\n-1000 moles of Oxygen\n-85 moles of Hyper-Noblium\n"
                + "In a temperature range between 3 K and 250 K\nThe crystallization reaction will be endothermic", Dm.str(data["requirements"]));
        });
        Feed(r, c, 2, "o2=1100;hypernoblium=90;TEMP=100");

        // one SSair cycle draws the input in (gas_input 1: all of it) and starts the progress (MIN_PROGRESS_AMOUNT * 5 / log10(108.5))
        r.Step(Second);
        float firstProgress = r.In(() => c.progress_bar);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(((datum_gas_mixture)c.airs[2]).total_moles()), 2);
            Assert.True(firstProgress > 0, "the progress starts");
            Assert.True(c.@internal.temperature < 100, "the endothermic reaction cools the mix");
        });
        Assert.Empty(On<obj_item_hypernoblium_crystal>(r, MX, MY - 1));

        // at 100% the crystal is made south of the machine, named by its quality (50 - 0: "Ok"), and the recipe's moles are used up
        for (int i = 0; i < 30 && On<obj_item_hypernoblium_crystal>(r, MX, MY - 1).Count == 0; i++)
            r.Step(Second / 2);
        var crystal = Assert.Single(On<obj_item_hypernoblium_crystal>(r, MX, MY - 1));
        r.In(() =>
        {
            Assert.StartsWith("Ok ", Dm.str(crystal.name));
            Assert.EndsWith("Hyper-Noblium crystal", Dm.str(crystal.name));
            Assert.Equal(100f, Moles(c.@internal, typeof(datum_gas_oxygen)), 1);
            Assert.Equal(5f, Moles(c.@internal, typeof(datum_gas_hypernoblium)), 1);
            Assert.True(c.progress_bar < 100);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OutsideTheWindowTheProgressFallsTheQualityDropsAndTheCoolantPortExchangesHeat()
    {
        var (r, bob) = Room(12003);
        var c = Crystallizer(r);
        Act(r, c, bob, "recipe", ("mode", "plasma_sheet")); // 450 plasma at 10-20 K
        Act(r, c, bob, "gas_input", ("gas_input", 1));
        Act(r, c, bob, "power");
        Feed(r, c, 2, "plasma=500;TEMP=300");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(0f, c.progress_bar);
            Assert.True(c.quality_loss >= 0.5f, "too warm: quality_loss + 0.5 a cycle (:181)");
        });

        // a cold coolant on the other port (airs[1]) pulls the mix down at HIGH_CONDUCTIVITY_RATIO
        Feed(r, c, 1, "n2=2000;TEMP=5");
        float before = r.In(() => c.@internal.temperature);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(c.@internal.temperature < before - 50, "the coolant takes the heat"); // 0.95 * dT * the reduced heat capacity (:150)
            Assert.True(((datum_gas_mixture)c.airs[1]).temperature > 5, "and warms");
        });

        // a recipe change dumps the mix back into the input and resets the progress and quality (:313)
        float plasma = r.In(() => Moles(c.@internal, typeof(datum_gas_plasma)));
        Act(r, c, bob, "recipe", ("mode", ""));
        r.In(() =>
        {
            Assert.Null(c.selected_recipe);
            Assert.Equal(0f, Dm.num(c.@internal.total_moles()));
            Assert.Equal(plasma, Moles((datum_gas_mixture)c.airs[2], typeof(datum_gas_plasma)), 1);
            Assert.Equal((0f, 0f), (c.quality_loss, c.progress_bar));
        });

        // the gas input is clamped to 0-250 (:327)
        Act(r, c, bob, "gas_input", ("gas_input", 9000));
        Assert.Equal(250f, r.In(() => c.gas_input));
        Act(r, c, bob, "gas_input", ("gas_input", -3));
        Assert.Equal(0f, r.In(() => c.gas_input));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScrewdriverOpensAnIdleCrystallizerAndTakingItApartReleasesItsGasAndLeavesTheBoard()
    {
        var (r, bob) = Room(12004);
        var c = Crystallizer(r);
        Act(r, c, bob, "power");
        r.In(() =>
        {
            var driver = Dm.@new<obj_item_screwdriver>(bob.loc);
            Assert.Equal(NONE, Waiting.NoSleep(c.screwdriver_act(bob, driver))); // on: refused (:61)
            c.set_on(FALSE);
            Waiting.NoSleep(c.screwdriver_act(bob, driver));
            Assert.Equal(1f, c.panel_open);
            Assert.Equal("crystallizer-open", Dm.str(c.icon_state));
            // ctrl-click with the panel open is blocked (:91)
            Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(c.click_ctrl(bob)));
            Assert.Equal(0f, c.on);

            c.@internal.merge(SSair.parse_gas_string("n2=50;TEMP=293.15") as datum_gas_mixture);
        });
        float n2Before = r.In(() => Moles((datum_gas_mixture)Waiting.NoSleep(locate(MX, MY, 1).return_air()), typeof(datum_gas_nitrogen)));
        r.In(() => Waiting.NoSleep(c.deconstruct(TRUE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(c));
            Assert.Contains(Dm.each_cast<atom>(locate(MX, MY, 1).contents).ToList(), a => a is obj_item_circuitboard_machine_crystallizer);
            float n2After = Moles((datum_gas_mixture)Waiting.NoSleep(locate(MX, MY, 1).return_air()), typeof(datum_gas_nitrogen));
            Assert.True(n2After > n2Before + 40, "the internal gas reaches the tile (on_deconstruction :43)");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHyperNobliumCrystalStopsACanistersReactionsAndPressureProofsClothing()
    {
        var (r, bob) = Room(12005);
        var can = Place<obj_machinery_portable_atmospherics_canister>(r, 5, 5);
        var gloves = Place<obj_item_clothing_gloves_color_black>(r, 4, 5);
        var crystal = Hold<obj_item_hypernoblium_crystal>(r, bob);
        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(crystal.interact_with_atom(can, bob)));
            Assert.Equal(1f, can.nob_crystal_inserted);
            Assert.True(QDELETED(crystal), "one use");
        });
        var second = Hold<obj_item_hypernoblium_crystal>(r, bob);
        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(second.interact_with_atom(can, bob)));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(second.interact_with_atom(gloves, bob)));
            Assert.StartsWith("pressure-resistant ", Dm.str(gloves.name));
            Assert.Equal(SPACE_SUIT_MIN_TEMP_PROTECT, Dm.num(gloves.min_cold_protection_temperature));
            Assert.True(Dm.truthy(Dm.bitand(gloves.clothing_flags, STOPSPRESSUREDAMAGE)));
            Assert.Equal(gloves.body_parts_covered, Dm.num(gloves.cold_protection));
            Assert.NotNull(gloves.atom_colours[FIXED_COLOUR_PRIORITY]); // the cyan HSL filter (color_transition_filter)
        });
        r.AssertNoRuntimes();
    }

    // ---- the electrolyzer

    // the tester stands beside the machine before the vapour comes: tg's water_vapor reaction wets the floors it spreads to below
    // 303.15 K, and walking onto a wet floor slips
    private static obj_machinery_electrolyzer Electrolyzer(AudioRoom r, mob_living_carbon_human bob, string air)
    {
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(MX - 1, MY, 1))));
        r.In(() =>
        {
            var t = (turf_open)locate(MX, MY, 1);
            t.air = SSair.parse_gas_string(air) as datum_gas_mixture;
            SSair.add_to_active(t);
        });
        var e = r.In(() => Dm.@new<obj_machinery_electrolyzer>(locate(MX, MY, 1), TRUE));
        r.Step(Second);
        return e;
    }

    private static float RoomMoles(Type gas)
    {
        float total = 0;
        foreach (turf_open t in Dm.each_cast<atom>(block(locate(1, 1, 1), locate(Dm.world.maxx, Dm.world.maxy, 1))).OfType<turf_open>())
            total += t.air == null ? 0 : Moles(t.air, gas);
        return total;
    }

    private static datum_gas_mixture TileAir(AudioRoom r) => r.In(() => ((turf_open)locate(MX, MY, 1)).air);

    private static List<string> Overlays(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays)).Select(o => o switch
        {
            string s => s,
            mutable_appearance m => Dm.str(m.icon_state),
            DmAppearance d => Dm.str(d.icon_state),
            _ => null,
        }).Where(s => s != null).ToList());

    [Fact]
    public void TheElectrolyzerSplitsTheTilesWaterVapourOnItsCellAndStopsWhenTheCellIsEjected()
    {
        var (r, bob) = Room(12006);
        var e = Electrolyzer(r, bob, "water_vapor=100;TEMP=350");
        r.In(() =>
        {
            // the board's two servos and two capacitors (RefreshParts :135)
            Assert.Equal((2f, 1.5f), (e.working_power, e.efficiency));
            Assert.Equal("electrolyzer-off", Dm.str(e.icon_state));
            Assert.Null(e.cell);
            var data = (DmList)Waiting.NoSleep(e.ui_data(bob));
            Assert.Equal(0f, Dm.num(data["hasPowercell"]));
        });
        // its reaction at the parts' working power: 2 H2O -> 2 H2 + O2, at most 2.5 * 2 ** 2 = 10 a cycle (h2o_conversion)
        r.In(() =>
        {
            var mix = SSair.parse_gas_string("water_vapor=100;TEMP=350") as datum_gas_mixture;
            e.call_reactions(mix);
            Assert.Equal((80f, 20f, 10f), (Moles(mix, typeof(datum_gas_water_vapor)), Moles(mix, typeof(datum_gas_hydrogen)), Moles(mix, typeof(datum_gas_oxygen))));
        });
        // no cell and not anchored: it will not turn on (toggle_power :193)
        Act(r, e, bob, "power");
        Assert.Equal(0f, r.In(() => e.on));

        // the screwdriver opens the panel (its overlay), the cell goes in, the panel closes
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, e, Left, 1);
        Assert.Equal(1f, r.In(() => e.panel_open));
        Assert.Contains("electrolyzer-open", Overlays(r, e));
        var cell = Hold<obj_item_stock_parts_power_store_cell_high>(r, bob);
        Click(r, bob, e, Left, 1);
        Assert.Same(cell, r.In(() => e.cell));
        // alt-click with the panel open is refused (:186)
        r.Command(() => ClientSeam.Click(bob.client, e, "left=1;alt=1;button=left;icon-x=16;icon-y=16"));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => e.on));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, e, Left, 1);
        Assert.Equal(0f, r.In(() => e.panel_open));
        Assert.DoesNotContain("electrolyzer-open", Overlays(r, e));

        float charge = r.In(() => cell.charge);
        r.Command(() => ClientSeam.Click(bob.client, e, "left=1;alt=1;button=left;icon-x=16;icon-y=16"));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.Equal(1f, e.on);
            Assert.Equal(("working", "electrolyzer-working"), (e.mode, Dm.str(e.icon_state)));
            datum_gas_mixture air = TileAir(r);
            Assert.True(Moles(air, typeof(datum_gas_hydrogen)) > 0, "hydrogen appears");
            Assert.True(RoomMoles(typeof(datum_gas_hydrogen)) > 0.5f, "and spreads");
            // each working cycle draws (5 * 3 * 2 * 2) / (1.5 + 2) J from the cell (:126)
            float cycles = (charge - cell.charge) / (60f / 3.5f);
            Assert.True(cycles >= 1);
            Assert.Equal(MathF.Round(cycles), cycles, 2);
        });

        // the window's eject wants the panel open; then the cell drops and the machine stops at its next cycle
        Act(r, e, bob, "eject");
        Assert.Same(cell, r.In(() => e.cell));
        r.In(() => e.toggle_panel_open());
        Act(r, e, bob, "eject");
        r.In(() =>
        {
            Assert.Null(e.cell);
            Assert.Same(locate(MX, MY, 1), get_turf(cell));
        });
        r.Step(Second);
        Assert.Equal((0f, "electrolyzer-off"), r.In(() => (e.on, Dm.str(e.icon_state))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchedDownElectrolyzerRunsWithoutACell()
    {
        var (r, bob) = Room(12007);
        var e = Electrolyzer(r, bob, "water_vapor=40;TEMP=350");
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, e, Left, 3);
        Assert.Equal(1f, r.In(() => e.anchored));
        Act(r, e, bob, "power");
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(("working", 1f), (e.mode, e.on));
            Assert.True(Moles(TileAir(r), typeof(datum_gas_hydrogen)) > 0);
            var data = (DmList)Waiting.NoSleep(e.ui_data(bob));
            Assert.Equal((0f, 1f, 1f), (Dm.num(data["hasPowercell"]), Dm.num(data["anchored"]), Dm.num(data["on"])));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrystalCellIsLiveAndStartsFull()
    {
        // the crystal_cell recipe's product (cell.dm:248): live since its grind_results is translated, so Initialize fills it
        var (r, _) = Room(12008);
        var cell = Place<obj_item_stock_parts_power_store_cell_crystal_cell>(r, 5, 5);
        r.In(() =>
        {
            Assert.Equal(STANDARD_CELL_CHARGE * 50, Dm.num(cell.maxcharge));
            Assert.Equal(Dm.num(cell.maxcharge), Dm.num(cell.charge));
        });
        r.AssertNoRuntimes();
    }
}
