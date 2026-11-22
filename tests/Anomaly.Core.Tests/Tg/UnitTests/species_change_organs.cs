// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/species_change_organs.dm
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
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/**
 * Unit test to ensure that, when a mob changes species,
 * certain aspects are carried over between their old and new set of organs
 * (brain traumas, cybernetics, organ damage, and limb-loss)
 */
// tg: code/modules/unit_tests/species_change_organs.dm:6 (/datum/unit_test/species_change_organs; class named by rules 2.3)
public sealed class datum_unit_test_species_change_organs : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/species_change_organs.dm:8
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        // Give a trauma
        await dummy.gain_trauma(typeof(datum_brain_trauma_severe_blindness));
        // Give a cyber heart
        obj_item_organ_heart_cybernetic cyber_heart = allocate<obj_item_organ_heart_cybernetic>();
        await cyber_heart.Insert(dummy, special: TRUE, movement_flags: DELETE_IF_REPLACED);
        // Give one of their organs a bit of damage
        obj_item_organ_appendix existing_appendix = dummy.get_organ_slot(ORGAN_SLOT_APPENDIX) as obj_item_organ_appendix;
        await existing_appendix.set_organ_damage(25);
        // Dismember a leg and an arm (sorry pal)
        obj_item_bodypart_arm right_arm = dummy.get_bodypart(BODY_ZONE_R_ARM) as obj_item_bodypart_arm;
        obj_item_bodypart_leg left_leg = dummy.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg;
        await right_arm.dismember();
        await left_leg.dismember();

        // Changing species should
        // - Persist brain traumas
        // - Persist cybernetic implants
        // - Persist organ damage to identical types
        // - Persist loss of limbs

        // Set up a species to pass over
        datum_species_lizard changed_species = Dm.@new<datum_species_lizard>();
        // But make sure the lizard's mutant organs are "normal"
        changed_species.mutantheart = ((datum_species)dummy.dna.species).mutantheart;
        changed_species.mutantappendix = ((datum_species)dummy.dna.species).mutantappendix;
        // and make sure they're not a TRAIT_NOBLOOD species so they need a heart
        Dm.remove(ref changed_species.inherent_traits, TRAIT_NOBLOOD);

        // Now make them a lizard
        dummy.set_species(changed_species);
        TEST_ASSERT(dummy.dna.species is datum_species_lizard, "Dummy didn't transform into a lizard when testing species organ changes.");

        // Grab the lizard's appendix for comparison later
        // They should've been given a new one, but our damage should also have transferred over
        obj_item_organ_appendix lizard_appendix = dummy.get_organ_slot(ORGAN_SLOT_APPENDIX) as obj_item_organ_appendix;

        // They should have the trauma still
        TEST_ASSERT(Dm.truthy(dummy.has_trauma_type(typeof(datum_brain_trauma_severe_blindness))), "Dummy, upon changing species, did not carry over their brain trauma!");
        // They should have their cybernetic heart still
        TEST_ASSERT_EQUAL(dummy.get_organ_slot(ORGAN_SLOT_HEART), cyber_heart, "Dummy, upon changing species, did not carry over their cybernetic organs!");
        // They should have appendix damage still
        TEST_ASSERT_EQUAL(lizard_appendix.damage, 25, "Dummy, upon changing species, did not carry over appendix damage!");

        // They should be missing both the right arm and left leg still
        DmList missing_limbs = dummy.get_missing_limbs() as DmList;
        TEST_ASSERT(!Dm.@in(typeof(obj_item_bodypart_arm_right), missing_limbs), "Dummy, upon changing species, regenerated their lost arm!");
        TEST_ASSERT(!Dm.@in(typeof(obj_item_bodypart_leg_left), missing_limbs), "Dummy, upon changing species, regenerated their lost leg!");
    }
}
