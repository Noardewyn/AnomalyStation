// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/light_replacer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M7 power helpers slice): the room is the toy room (TgToyRoom.Install); click_wrapper is tg's (unit_test.dm:201); the
// fixtures /obj/machinery/light/unbroken, /obj/machinery/light/floor/unbroken and /obj/machinery/light/small/unbroken are the
// test's own types (light_replacer.dm:46-52, test code, no DM type of the port). The test waits 2 deciseconds after a click, as
// the slapcrafting port does, for a detached part of the click to finish.

/// Tests whether a [normal/bluespace] light replacer is capable of replacing a [broken/absent/burnt] light within a [near/far] [normal/bulb/floor] light fixure, and that resources are properly consumed, and all that.
// tg: code/modules/unit_tests/light_replacer.dm:2 (/datum/unit_test/light_replacer)
public sealed class datum_unit_test_light_replacer : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/light_replacer.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human janitor = allocate<mob_living_carbon_human_consistent>();
        obj_item_lightreplacer replacer = allocate<obj_item_lightreplacer>();
        obj_item_lightreplacer_blue rangedreplacer = allocate<obj_item_lightreplacer_blue>();
        obj_machinery_light current_close_light = allocate<obj_machinery_light_unbroken>(run_loc_floor_bottom_left);
        obj_machinery_light far_bulb = allocate<obj_machinery_light_small_unbroken>(run_loc_floor_top_right);

        await janitor.put_in_active_hand(replacer);
        await far_bulb.break_light_tube(TRUE);

        await click_wrapper(janitor, current_close_light);
        TEST_ASSERT_EQUAL(replacer.uses, Dm.initial(replacer, (obj_item_lightreplacer r) => r.uses), "Light replacer consumed resources replacing an already intact tube");

        await click_wrapper(janitor, far_bulb);
        TEST_ASSERT_EQUAL(far_bulb.status, LIGHT_BROKEN, "Normal light replacer fixed a light from range");

        await current_close_light.break_light_tube(TRUE);
        await click_wrapper(janitor, current_close_light);
        TEST_ASSERT_EQUAL(current_close_light.status, LIGHT_OK, "Light replacer failed to replace a tube");
        TEST_ASSERT_NOTEQUAL(replacer.uses, Dm.initial(replacer, (obj_item_lightreplacer r) => r.uses), "Light replacer did not consume resources replacing a tube");

        await janitor.put_in_active_hand(rangedreplacer, TRUE);
        qdel(current_close_light);
        current_close_light = allocate<obj_machinery_light_floor_unbroken>(run_loc_floor_bottom_left);
        await current_close_light.burn_out();

        await click_wrapper(janitor, far_bulb);
        TEST_ASSERT_EQUAL(far_bulb.status, LIGHT_OK, "Ranged light replacer failed to replace broken bulb from range");

        await click_wrapper(janitor, current_close_light);
        TEST_ASSERT_EQUAL(current_close_light.status, LIGHT_OK, "Ranged light replacer failed to replace burnt floor bulb in melee");
    }

    // tg: code/modules/unit_tests/unit_test.dm:201
    private static async DmTask click_wrapper(mob_living clicker, atom clicked_on)
    {
        clicker.next_click = -1;
        clicker.next_move = -1;
        await clicker.ClickOn(clicked_on, list2params(Dm.list(Dm.kv(LEFT_CLICK, 1), Dm.kv(BUTTON, LEFT_CLICK))));
    }
}

// tg: code/modules/unit_tests/light_replacer.dm:46 (test code, no DM type of the port)
[DmPath("/obj/machinery/light/unbroken")]
public class obj_machinery_light_unbroken : obj_machinery_light
{
    public obj_machinery_light_unbroken()
    {
        allow_break_on_init = FALSE;
    }
}

// tg: code/modules/unit_tests/light_replacer.dm:49
[DmPath("/obj/machinery/light/floor/unbroken")]
public class obj_machinery_light_floor_unbroken : obj_machinery_light_floor
{
    public obj_machinery_light_floor_unbroken()
    {
        allow_break_on_init = FALSE;
    }
}

// tg: code/modules/unit_tests/light_replacer.dm:52
[DmPath("/obj/machinery/light/small/unbroken")]
public class obj_machinery_light_small_unbroken : obj_machinery_light_small
{
    public obj_machinery_light_small_unbroken()
    {
        allow_break_on_init = FALSE;
    }
}
