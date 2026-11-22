// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/organ_bodypart_shuffle.dm
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

// M6 slice 5 (gibbing): the whole file.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/// Moves organs in and out of bodyparts, and moves the bodyparts around to see if someone didn't fuck up their movement
// tg: code/modules/unit_tests/organ_bodypart_shuffle.dm:2 (/datum/unit_test/organ_bodypart_shuffle; class named by rules 2.3)
public sealed class datum_unit_test_organ_bodypart_shuffle : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/organ_bodypart_shuffle.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human hollow_boy = allocate<mob_living_carbon_human_consistent>(); //freshly filled with wet insides

        // Test if organs are all properly updating when forcefully removed
        DmList removed_organs = Dm.list();

        // 1. remove all the organs from the mob
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(hollow_boy.organs))
        {
            await organ.moveToNullspace();
            Dm.append(ref removed_organs, organ);
        }

        // 2. ensure removed organs proper disassociate from the mob and the bodypart
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(removed_organs))
        {
            TEST_ASSERT(!Dm.@in(organ, hollow_boy.organs), Dm.text("Organ '[] remained inside human after forceMove into nullspace.", organ.name));
            TEST_ASSERT_NULL(organ.loc, Dm.text("Organ '[] did not move to nullspace after being forced to.", organ.name));
            TEST_ASSERT_NULL(organ.owner, Dm.text("Organ '[] kept reference to human after forceMove into nullspace.", organ.name));
            TEST_ASSERT_NULL(organ.bodypart_owner, Dm.text("Organ '[] kept reference to bodypart after forceMove into nullspace.", organ.name));
        }

        // 3. replace all bodyparts with new ones and place the previously removed organs into the new bodyparts
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(hollow_boy.get_bodyparts()))
        {
            obj_item_bodypart replacement = (obj_item_bodypart)allocate(bodypart.GetType());
            foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(removed_organs))
            {
                if (!Dm.eq(replacement.body_zone, deprecise_zone(organ.zone)))
                    continue;
                await organ.bodypart_insert(replacement);
            }
            if (!Dm.truthy(await replacement.replace_limb(hollow_boy)))
                TEST_FAIL(Dm.text("Failed to replace [] with a new one of the same type.", replacement));
            qdel(bodypart); // it's been replaced, clean up
        }

        // 4. ensure organs are properly associated with the new bodyparts and the mob
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(removed_organs))
        {
            TEST_ASSERT(Dm.@in(organ, hollow_boy.organs), Dm.text("Organ '[] was put in an empty bodypart that replaced a humans, but the organ did not come with.", organ.name));
            TEST_ASSERT(organ.owner == hollow_boy, Dm.text("Organ '[]'s owner was not properly updated to the new human after being placed in a replacement bodypart.", organ.name));
            TEST_ASSERT(Dm.@in(organ.bodypart_owner, hollow_boy.bodyparts), Dm.text("Organ '[]'s bodypart_owner was not properly updated to the new bodypart after being placed in a replacement bodypart.", organ.name));
        }
    }
}

/// Tests that gibbing results in a head with a brain
// tg: code/modules/unit_tests/organ_bodypart_shuffle.dm:44 (/datum/unit_test/gibbing_organ_transfer; class named by rules 2.3)
public sealed class datum_unit_test_gibbing_organ_transfer : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/organ_bodypart_shuffle.dm:46
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();
        DmList bodyparts_for_cleanup = dummy.bodyparts.Copy();
        DmList organs_for_cleanup = dummy.organs.Copy();
        obj_item_organ_brain original_brain = dummy.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        obj_item_bodypart_head original_head = dummy.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        await dummy.gib(ALL);

        TEST_ASSERT(QDELETED(dummy), "Dummy was not deleted after gibbing.");
        TEST_ASSERT(!QDELETED(original_head), "Original head was deleted after gibbing.");
        TEST_ASSERT(!QDELETED(original_brain), "Original brain was deleted after gibbing.");
        TEST_ASSERT(original_brain.loc == original_head, "Original brain was not transferred to the head after gibbing.");

        QDEL_LIST(organs_for_cleanup);
        QDEL_LIST(bodyparts_for_cleanup);
    }
}
