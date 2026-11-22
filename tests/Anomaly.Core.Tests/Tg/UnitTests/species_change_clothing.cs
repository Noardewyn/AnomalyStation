// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/species_change_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Gaps SP1 (D338): the whole file.
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install), a world without a Master, whose SSid_access
// is not initialized: the consistent assistant is dressed with visuals_only (no ID trim to apply, outfit.dm:213), as the preferences'
// dummies are.

///Gives a Human lizard-incompatible shoes, then changes their species over to see if they drop the now incompatible shoes, testing if Digitigrade feet works.
///Gives a Monkey a collar, then changes their species to Human to see if item's restrictions works on species change.
// tg: code/modules/unit_tests/species_change_clothing.dm:3 (/datum/unit_test/species_change_clothing; class named by rules 2.3)
public sealed class datum_unit_test_species_change_clothing : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/species_change_clothing.dm:5
    public override async DmTask Run()
    {
        // Test lizards as their own thing so we can get more coverage on their features
        mob_living_carbon_human morphing_human = allocate<mob_living_carbon_human_dummy_consistent>();

        await morphing_human.equipOutfit(typeof(datum_outfit_job_assistant_consistent), visuals_only: TRUE);
        morphing_human.dna.features[FEATURE_LEGS] = DIGITIGRADE_LEGS; //you WILL have digitigrade legs

        obj_item human_shoes = morphing_human.get_item_by_slot(ITEM_SLOT_FEET) as obj_item;
        human_shoes.supports_variations_flags = NONE; //do not fit lizards at all costs.
        morphing_human.set_species(typeof(datum_species_lizard));
        obj_item lizard_shoes = morphing_human.get_item_by_slot(ITEM_SLOT_FEET) as obj_item;

        TEST_ASSERT_NOTEQUAL(human_shoes, lizard_shoes, "Lizard still has shoes after changing species.");

        // Testing whether item-species restrictions properly blocks changing into a blacklisted species.
        morphing_human.set_species(typeof(datum_species_monkey));

        obj_item_clothing_neck_petcollar collar = Dm.@new<obj_item_clothing_neck_petcollar>();
        await morphing_human.equip_to_slot_or_del(collar, ITEM_SLOT_NECK);

        obj_item equipped_collar = morphing_human.get_item_by_slot(ITEM_SLOT_NECK) as obj_item;
        morphing_human.set_species(typeof(datum_species_human));
        obj_item human_collar = morphing_human.get_item_by_slot(ITEM_SLOT_NECK) as obj_item;

        TEST_ASSERT_NOTEQUAL(equipped_collar, human_collar, "Human still has a Monkey collar after changing species.");
    }
}

///Gives a Human items in both hands, then swaps them to be another species. Held items should remain.
// tg: code/modules/unit_tests/species_change_clothing.dm:33 (/datum/unit_test/species_change_held_items; class named by rules 2.3)
public sealed class datum_unit_test_species_change_held_items : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/species_change_clothing.dm:35
    public override async DmTask Run()
    {
        mob_living_carbon_human morphing_human = allocate<mob_living_carbon_human_dummy_consistent>();
        obj_item item_a = allocate<obj_item_storage_toolbox>();
        obj_item item_b = allocate<obj_item_melee_baton_security_loaded>();
        await morphing_human.put_in_hands(item_a);
        await morphing_human.put_in_hands(item_b);

        object pre_change_num = Dm.length(morphing_human.get_empty_held_indexes());
        TEST_ASSERT_EQUAL(pre_change_num, 0, "Human had empty hands before the species change happened.");

        morphing_human.set_species(typeof(datum_species_lizard));

        object post_change_num = Dm.length(morphing_human.get_empty_held_indexes());
        TEST_ASSERT_EQUAL(post_change_num, 0, "Human had empty hands after the species change happened, but they should've kept their items.");
    }
}
