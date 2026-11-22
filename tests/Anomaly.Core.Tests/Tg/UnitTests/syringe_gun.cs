// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/syringe_gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice C): the room is the toy room (TgToyRoom.Install); EASY_ALLOCATE() is allocate of the declared type;
// click_wrapper is tg's (unit_test.dm:201).

/// Tests the ability to load syringe into a syringe gun
// tg: code/modules/unit_tests/syringe_gun.dm:2 (/datum/unit_test/load_syringe)
public sealed class datum_unit_test_load_syringe : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/syringe_gun.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human_consistent chemist = allocate<mob_living_carbon_human_consistent>();
        obj_item_gun_syringe syringe_gun = allocate<obj_item_gun_syringe>();
        obj_item_reagent_containers_syringe syringe = allocate<obj_item_reagent_containers_syringe>();

        await chemist.put_in_active_hand(syringe, forced: TRUE);
        await chemist.put_in_inactive_hand(syringe_gun, forced: TRUE);

        await click_wrapper(chemist, syringe_gun);

        TEST_ASSERT_EQUAL(syringe.loc, syringe_gun, "Syringe was not added to syringe gun when clicking on it to load it.");
    }

    // tg: code/modules/unit_tests/unit_test.dm:201
    private static async DmTask click_wrapper(mob_living clicker, atom clicked_on)
    {
        clicker.next_click = -1;
        clicker.next_move = -1;
        await clicker.ClickOn(clicked_on, list2params(Dm.list(Dm.kv(LEFT_CLICK, 1), Dm.kv(BUTTON, LEFT_CLICK))));
    }
}
