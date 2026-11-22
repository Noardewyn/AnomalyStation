using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Lavaland;

/// <summary>Lavaland L2b (D304): tg's stacking machine and its console, the furnace and its console, the unloading machine and the labour camp's collection unit and consoles.</summary>
public class MineralMachinesTests
{
    private static object Act(obj_machinery console, mob user, string action, DmList p = null)
    {
        World.Current.usr = user;
        return Waiting.NoSleep(console.ui_act(action, p ?? Dm.list(), Dm.@new<datum_tgui>(user, console, "test"), null));
    }

    [Fact]
    public void The_stacking_machine_stacks_sheets_from_its_input_and_releases_them_from_its_console()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var stacker = Dm.@new<obj_machinery_mineral_stacking_machine>(locate(6, 5, 1));
            var console = Dm.@new<obj_machinery_mineral_stacking_unit_console>(locate(3, 3, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(4, 4, 1));
            Assert.Same(stacker, console.machine); // the console finds the machine in its area
            Assert.Same(console, stacker.console);

            // sheets on its input (east) are stacked; each 50 go out of its output (west)
            var first = Dm.@new<obj_item_stack_sheet_iron>(locate(2, 2, 1), 30);
            Waiting.NoSleep(first.forceMove(locate(7, 5, 1)));
            Assert.True(QDELETED(first));
            Assert.Empty(Dm.each<obj_item_stack_sheet_iron>(locate(5, 5, 1)));
            Waiting.NoSleep(Dm.@new<obj_item_stack_sheet_iron>(locate(2, 2, 1), 30).forceMove(locate(7, 5, 1)));
            Assert.Equal(new[] { 50f }, Dm.each<obj_item_stack_sheet_iron>(locate(5, 5, 1)).Select(s => Dm.num(s.amount)));

            var data = (DmList)Waiting.NoSleep(console.ui_data(miner));
            Assert.Equal((50f, "east", "west"), (Dm.num(data["stacking_amount"]), (string)data["input_direction"], (string)data["output_direction"]));
            var row = Assert.Single(Dm.each<DmList>(data["contents"]));
            Assert.Equal(("Iron", 10f), ((string)row["name"], Dm.num(row["amount"])));

            // the console releases what is held and turns the input a quarter (counter-clockwise, as DM's turn)
            Assert.Equal(TRUE, Act(console, miner, "release", Dm.list(Dm.kv("type", "/obj/item/stack/sheet/iron"))));
            Assert.Equal(60f, Dm.each<obj_item_stack_sheet_iron>(locate(5, 5, 1)).Sum(s => Dm.num(s.amount)));
            Assert.Null(Act(console, miner, "release", Dm.list(Dm.kv("type", "/obj/item/stack/sheet/glass")))); // nothing of it held
            Act(console, miner, "rotate", Dm.list(Dm.kv("input", "1")));
            Assert.Equal(NORTH, stacker.input_dir);
            Act(console, miner, "rotate", Dm.list(Dm.kv("input", "0")));
            Assert.Equal(SOUTH, stacker.output_dir);

            // linked to a silo, the sheets go in it
            var silo = Dm.@new<obj_machinery_ore_silo>(locate(2, 7, 1));
            var multitool = Dm.@new<obj_item_multitool>(locate(4, 4, 1));
            multitool.set_buffer(silo);
            Assert.Equal(ITEM_INTERACT_SUCCESS, stacker.materials.OnMultitool(stacker, miner, multitool));
            Waiting.NoSleep(Dm.@new<obj_item_stack_sheet_iron>(locate(2, 2, 1), 7).forceMove(locate(6, 6, 1)));
            var iron = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron)));
            Assert.Equal(7f * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(silo.materials.get_material_amount(iron)));
        });
        // tg's stacker makes its store with new type(src, 0), which the stack refuses with a trace (stack.dm:71)
        Assert.All(r.Runtimes, e => Assert.Contains("invalid amount 0!", e.Message));
    }

    [Fact]
    public void The_furnace_takes_ore_and_smelts_the_chosen_material_into_sheets()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var furnace = Dm.@new<obj_machinery_mineral_processing_unit>(locate(6, 5, 1));
            var console = Dm.@new<obj_machinery_mineral_processing_unit_console>(locate(4, 5, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(4, 4, 1));
            Assert.Same(console, furnace.mineral_machine);
            var iron = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron)));
            var glass = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_glass)));
            Assert.Same(iron, furnace.selected_material);

            // ore on its input (north) goes into its store
            Dm.@new<obj_item_stack_ore_iron>(locate(6, 6, 1), 12);
            Assert.Empty(Dm.each<obj_item_stack_ore_iron>(locate(6, 6, 1)));
            float stored = Dm.num(furnace.materials.materials[iron]);
            Assert.True(stored >= 12 * SHEET_MATERIAL_AMOUNT / 2f);

            var stat = (DmList)Waiting.NoSleep(console.ui_static_data(miner));
            Assert.Contains(Dm.each<DmList>(stat["materialIcons"]), i => Dm.eq(i["id"], REF(iron)) && Dm.eq(i["icon_state"], "sheet-metal"));
            Assert.Equal(SHEET_MATERIAL_AMOUNT, Dm.num(stat["SHEET_MATERIAL_AMOUNT"]));
            var data = (DmList)Waiting.NoSleep(console.ui_data(miner));
            Assert.Equal(("iron", 0f), ((string)data["selectedMaterial"], Dm.num(data["state"])));

            // on, it smelts tg's 5 sheets a second (10 a machine tick) out of its output (south), and stops when it runs dry
            Assert.Equal(TRUE, Act(console, miner, "toggle"));
            Assert.Equal(1f, furnace.on);
            Assert.Null(furnace.process(2f));
            float sheets = Dm.each<obj_item_stack_sheet_iron>(locate(6, 4, 1)).Sum(s => Dm.num(s.amount));
            Assert.Equal(Math.Min(10f, MathF.Floor(stored / SHEET_MATERIAL_AMOUNT)), sheets);
            while (Dm.truthy(furnace.on))
                furnace.process(2f);
            Assert.Equal(PROCESS_KILL, furnace.process(2f));

            // the console picks another material
            Assert.Equal(TRUE, Act(console, miner, "setMaterial", Dm.list(Dm.kv("value", REF(glass)))));
            Assert.Same(glass, furnace.selected_material);

            // silicons SI3: the alloys are the smelter techweb's (tg's one initial SMELTER design, reinforced glass): it smelts them out of
            // its store; a design the techweb does not have stops it
            data = (DmList)Waiting.NoSleep(console.ui_data(miner));
            Assert.Contains(Dm.each<DmList>(data["alloys"]), a => Dm.eq(a["name"], "Reinforced Glass") && Dm.eq(a["path"], typeof(datum_design_material_rglass)));
            stat = (DmList)Waiting.NoSleep(console.ui_static_data(miner));
            Assert.Contains(Dm.each<DmList>(stat["alloyIcons"]), i => Dm.eq(i["id"], "/datum/design/material/rglass") && Dm.eq(i["icon_state"], "sheet-rglass"));
            Waiting.NoSleep(furnace.materials.insert_amount_mat(4 * SHEET_MATERIAL_AMOUNT, iron));
            Waiting.NoSleep(furnace.materials.insert_amount_mat(4 * SHEET_MATERIAL_AMOUNT, glass));
            Act(console, miner, "setAlloy", Dm.list(Dm.kv("new_alloy", "/datum/design/material/rglass")));
            Assert.Null(furnace.selected_material);
            Act(console, miner, "toggle");
            furnace.process(2f);
            Assert.True(Dm.each<obj_item_stack_sheet_rglass>(locate(6, 4, 1)).Sum(st => Dm.num(st.amount)) > 0, "the furnace smelts reinforced glass");
            Act(console, miner, "setAlloy", Dm.list(Dm.kv("new_alloy", "/datum/design/alloy/plasteel_alloy")));
            furnace.process(2f);
            Assert.Equal(0f, furnace.on);

            // a console with no furnace in range deletes itself
            var lonely = Dm.@new<obj_machinery_mineral_processing_unit_console>(locate(10, 10, 1));
            Assert.True(QDELETED(lonely));
        });
        Assert.Empty(r.Runtimes);
    }

    [Fact]
    public void The_unloading_machine_empties_ore_boxes_and_passes_ore_on()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var unloader = Dm.@new<obj_machinery_mineral_unloading_machine>(locate(5, 5, 1));
            var box = Dm.@new<obj_structure_ore_box>(locate(2, 2, 1));
            Waiting.NoSleep(Dm.@new<obj_item_stack_ore_iron>(locate(2, 2, 1), 5).forceMove(box));
            Waiting.NoSleep(Dm.@new<obj_item_stack_ore_plasma>(locate(2, 2, 1), 3).forceMove(box));

            // the box rolls onto its input (west): its ore comes out of the output (east)
            Waiting.NoSleep(box.forceMove(locate(4, 5, 1)));
            Assert.Empty(Dm.each<object>(box.contents));
            Assert.Equal(8f, Dm.each<obj_item_stack_ore>(locate(6, 5, 1)).Sum(o => Dm.num(o.amount)));

            // loose ore is passed on as it is
            Waiting.NoSleep(Dm.@new<obj_item_stack_ore_glass>(locate(2, 2, 1), 2).forceMove(locate(4, 5, 1)));
            Assert.Single(Dm.each<obj_item_stack_ore_glass>(locate(6, 5, 1)));
        });
        Assert.Empty(r.Runtimes);
    }

    [Fact]
    public void The_labour_camp_counts_stamped_sheets_and_the_prisoner_claims_them_on_the_console()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var stacker = Dm.@new<obj_machinery_mineral_stacking_machine_laborstacker>(locate(6, 5, 1));
            var console = Dm.@new<obj_machinery_mineral_labor_claim_console>(locate(4, 5, 1));
            var checker = Dm.@new<obj_machinery_mineral_labor_points_checker>(locate(4, 3, 1));
            var prisoner = Dm.@new<mob_living_carbon_human_consistent>(locate(5, 4, 1));
            Assert.Same(stacker, console.stacking_machine);
            Assert.Same(console, stacker.labor_console);
            var id = Dm.@new<obj_item_card_id_advanced_prisoner>(locate(5, 4, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(prisoner.put_in_hands(id))));
            id.goal = 100;

            // only sheets from the camp's furnace (stamped) are worth points; a sheet handed in is stacked too
            var stamped = Dm.@new<obj_item_stack_sheet_iron>(locate(2, 2, 1), 4);
            stamped.manufactured = TRUE;
            float worth = stamped.gulag_value * 4;
            Waiting.NoSleep(stamped.forceMove(locate(7, 5, 1)));
            var plain = Dm.@new<obj_item_stack_sheet_iron>(locate(5, 4, 1), 6);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(stacker.base_item_interaction(prisoner, plain)));
            Assert.True(QDELETED(plain));
            Assert.Equal(worth, stacker.points);

            var data = (DmList)Waiting.NoSleep(console.ui_data(prisoner));
            Assert.Equal(Dm.text("You are [] points away.", 100f), data["status_info"]);
            Assert.Equal((worth, 0f), (Dm.num(data["unclaimed_points"]), Dm.num(data["can_go_home"])));
            Assert.Equal(0f, Dm.num(((DmList)Waiting.NoSleep(console.ui_static_data(prisoner)))["shuttle_exists"])); // no camp shuttle here

            // the claim moves the points to the worn prisoner ID
            Assert.Equal(TRUE, Act(console, prisoner, "claim_points"));
            Assert.Equal((worth, 0f), (id.points, stacker.points));
            Assert.Null(Act(console, prisoner, "move_shuttle")); // "Shuttle not found."

            // the goal met, the prisoner may go home; an emagged console lets anyone go
            id.points = 100;
            Assert.Equal("Goal met!", ((DmList)Waiting.NoSleep(console.ui_data(prisoner)))["status_info"]);
            Assert.Equal(1f, Dm.num(((DmList)Waiting.NoSleep(console.ui_data(prisoner)))["can_go_home"]));
            id.points = 0;
            Waiting.NoSleep(console.emag_act(prisoner, null));
            Assert.Equal(1f, Dm.num(((DmList)Waiting.NoSleep(console.ui_data(prisoner)))["can_go_home"]));

            // the checker reads the ID out; anything else is not its business
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(checker.item_interaction(prisoner, id)));
            Assert.Equal(NONE, Waiting.NoSleep(checker.item_interaction(prisoner, Dm.@new<obj_item_multitool>(locate(5, 4, 1)))));

            // a claim console with no collection unit near deletes itself
            Assert.True(QDELETED(Dm.@new<obj_machinery_mineral_labor_claim_console>(locate(10, 10, 1))));
        });
        Assert.All(r.Runtimes, e => Assert.Contains("invalid amount 0!", e.Message));
    }
}
