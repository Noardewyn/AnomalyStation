// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/slapcrafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M5 slice 8): tg slaps a toy Odysseus (the parent) with a sheet of paper (the slapper); both types are inert in the
// port (D132) and the toy's own item_interaction is not translated, so the hit is refused before attackby (DV27). The
// translated stand-ins: a glass shard slapped onto a crowbar, the recipe's requirements changed to match; its result stays
// tg's toy Reticence (an inert result, D132). click_wrapper is tg's (unit_test.dm:201); the room is the toy room.

// tg: code/modules/unit_tests/slapcrafting.dm:1 (/datum/unit_test/slapcrafting; class named by rules 2.3)
public sealed class datum_unit_test_slapcrafting : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        // construct_item records each craft in SSblackbox (tg's unit test skips it: PERFORM_ALL_TESTS(crafting), FALSE in the
        // port); tg's MC always creates it
        if (SSblackbox == null)
            Dm.@new<datum_controller_subsystem_blackbox>();
    }

    // tg: code/modules/unit_tests/slapcrafting.dm:3
    public override async DmTask Run()
    {
        mob_living_carbon_human expert = allocate<mob_living_carbon_human_consistent>();
        obj_item disguise = allocate<obj_item_shard>(); // ADAPTED: tg's /obj/item/paper
        obj_item cool_robot = allocate<obj_item_crowbar>(); // ADAPTED: tg's /obj/item/toy/mecha/odysseus

        DmList slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_slapcraft_tester));
        await cool_robot.AddElement(typeof(datum_element_slapcrafting), slapcraft_recipe_list);

        await expert.put_in_active_hand(disguise);
        await click_wrapper(expert, cool_robot);
        // the craft runs detached (INVOKE_ASYNC of slapcraft_async): let the world run it
        await Dm.sleep(2);
        obj_item_toy_mecha_reticence cooler_robot = Dm.locate<obj_item_toy_mecha_reticence>(run_loc_floor_bottom_left);
        TEST_ASSERT_NOTNULL(cooler_robot, "Slapcrafting did not successfully produce an item");
        qdel(cooler_robot);
    }

    // tg: code/modules/unit_tests/unit_test.dm:201
    private static async DmTask click_wrapper(mob_living clicker, atom clicked_on)
    {
        clicker.next_click = -1;
        clicker.next_move = -1;
        await clicker.ClickOn(clicked_on, list2params(Dm.list(Dm.kv(LEFT_CLICK, 1), Dm.kv(BUTTON, LEFT_CLICK))));
    }
}

// tg: code/modules/unit_tests/slapcrafting.dm:17 (test code, no DM type)
public class datum_crafting_recipe_slapcraft_tester : datum_crafting_recipe
{
    public datum_crafting_recipe_slapcraft_tester()
    {
        name = "Cooler Robot";
        result = typeof(obj_item_toy_mecha_reticence);
        time = 0;
        category = CAT_ENTERTAINMENT;
        crafting_flags = CRAFT_SKIP_MATERIALS_PARITY;
    }

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        reqs = Dm.list(
            Dm.kv(typeof(obj_item_shard), 1), // ADAPTED: tg's /obj/item/paper
            Dm.kv(typeof(obj_item_crowbar), 1) // ADAPTED: tg's /obj/item/toy/mecha/odysseus
        );
    }
}
