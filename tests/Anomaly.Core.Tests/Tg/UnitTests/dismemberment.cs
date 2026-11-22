// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/dismemberment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// M6 slice 4 (dismemberment, the ROADMAP's done-when test of M6): the whole file.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install), and tg's strings/ files (the scar
// texts of the regrown limbs) are mounted as the server mounts them.

/**
 * Unit test to check that held items are dropped correctly when we are dismembered.
 *
 * Also tests for edge cases such as undroppable items.
 */
// tg: code/modules/unit_tests/dismemberment.dm:6 (/datum/unit_test/dismemberment; class named by rules 2.3)
public sealed class datum_unit_test_dismemberment : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world_under_test).Files, TgClone.Root);
    }

    // tg: code/modules/unit_tests/dismemberment.dm:6
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        obj_item testing_item = allocate<obj_item_analyzer>();
        testing_item.name = "testing item";

        // Standard situation: We're holding a normal item and get dismembered.
        await test_item(dummy, testing_item);

        // Abnormal situation: We're holding an undroppable item and get dismembered.
        ADD_TRAIT(testing_item, TRAIT_NODROP, TRAIT_GENERIC);
        await test_item(dummy, testing_item, status_text: " after applying TRAIT_NODROP to the testing item");
    }

    // tg: code/modules/unit_tests/dismemberment.dm:20
    private async DmTask test_item(mob_living_carbon_human dummy, obj_item testing_item, string status_text = "")
    {
        //Check both to make sure being the active hand doesn't make a difference.
        await dummy.put_in_l_hand(testing_item);
        await check_dismember(dummy, BODY_ZONE_L_ARM, status_text);

        await dummy.put_in_r_hand(testing_item);
        await check_dismember(dummy, BODY_ZONE_R_ARM, status_text);
    }

    // tg: code/modules/unit_tests/dismemberment.dm:29
    private async DmTask check_dismember(mob_living_carbon_human dummy, string which_arm, string status_text)
    {
        obj_item_bodypart dismembered_limb = (obj_item_bodypart)dummy.get_bodypart(which_arm);
        obj_item held_item = dummy.get_item_for_held_index(dismembered_limb.held_index) as obj_item;

        await dismembered_limb.dismember();
        TEST_ASSERT(Dm.@in(held_item, dummy.loc), Dm.text("Dummy did not drop [] when [] was dismembered[].", held_item, dismembered_limb, status_text));
        // Clean up after ourselves
        qdel(dismembered_limb);
        await dummy.fully_heal(HEAL_ALL);
    }
}
