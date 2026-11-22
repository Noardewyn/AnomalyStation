// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/blood_volume_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// The whole test (M6 slice 1, the ROADMAP's done-when test of M6; its saline part, blood_volume_procs.dm:94-110, with M8 slice B:
// the salglu solution, its dilution status effect and the diluted volume).
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

// tg: code/modules/unit_tests/blood_volume_procs.dm:1
public sealed class datum_unit_test_blood_volume_procs : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/blood_volume_procs.dm:3
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();

        // Test initial blood status.
        TEST_ASSERT(Dm.truthy(dummy.can_have_blood()), "Initialization of blood volume status is screwed up.");
        TEST_ASSERT(Dm.truthy(CAN_HAVE_BLOOD(dummy)), "Caching of blood volume status is screwed up.");

        // Test initial blood volume.
        TEST_ASSERT_EQUAL(dummy.default_blood_volume, BLOOD_VOLUME_NORMAL, "Default blood volume is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), dummy.default_blood_volume, "Blood volume isn't initialized properly.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(apply_modifiers: TRUE), dummy.get_blood_volume(), "Blood volume is modified on initialization.");

        float set_amount = 400;

        // Test setting blood volume.
        TEST_ASSERT_EQUAL(dummy.set_blood_volume(set_amount), set_amount, "Set proc return value is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), set_amount, "Final blood volume is different from what was expected.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        float adjustment_amount = 100;
        float expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;

        // Test increasing blood volume.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount), adjustment_amount, "Adjustment proc return value is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Final blood volume is different from what was expected.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = -100;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;

        // Test decreasing blood volume.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount), adjustment_amount, "Adjustment proc return value is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Final blood volume is different from what was expected.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = 100;
        float expected_adjustment = 50;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + expected_adjustment;

        // Test increasing blood volume, clamped to a maximum.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, maximum: expected_final_volume), expected_adjustment, "Clamped adjustment proc return value is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Clamped final blood volume is different from what was expected.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = -100;
        expected_adjustment = -50;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + expected_adjustment;

        // Test decreasing blood volume, clamped to a minimum.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, minimum: expected_final_volume), expected_adjustment, "Clamped adjustment proc return value is incorrect.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Clamped final blood volume is different from what was expected.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = 100;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;
        float minimum = BLOOD_VOLUME_NORMAL + 200;

        // Test if increasing an existing volume that is below the minimum causes it to jump to the minimum.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, minimum: minimum), adjustment_amount, "When existing volume is below the minimum, adjustment the proc return value after trying to increase it is unexpected. (likely jumped to minimum)");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "When existing volume is below the minimum, the final volume after trying to increase it is unexpected. (likely jumped to minimum)");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = -100;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;
        float maximum = BLOOD_VOLUME_NORMAL - 200;

        // Test if decreasing an existing volume that is above the maximum causes it to jump to the maximum.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, maximum: maximum), adjustment_amount, "When existing volume is above the maximum, the adjustment proc return value after trying to decrease it is unexpected. (likely jumped to maximum)");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "When existing volume is above the maximum, the final volume after trying to decrease it is unexpected. (likely jumped to maximum)");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = BLOOD_VOLUME_MAXIMUM * 10;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;

        // Test increasing blood volume beyond BLOOD_VOLUME_MAXIMUM by setting the maximum to INFINITY. This is allowed. (e.g. setting it to BLOOD_VOLUME_MAX_LETHAL)
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, maximum: float.PositiveInfinity), adjustment_amount, "Setting adjustment proc maximum to INFINITY results in an unexpected adjustment proc return value.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Setting adjustment proc maximum to INFINITY results in an unexpected final volume.");

        dummy.set_blood_volume(BLOOD_VOLUME_NORMAL);
        adjustment_amount = BLOOD_VOLUME_MAXIMUM * -10;
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + adjustment_amount;

        // Test decreasing blood volume below 0 by setting the minimum to -INFINITY. Shouldn't be used, but I want to verify that bypassing the default minimum works as expected.
        TEST_ASSERT_EQUAL(dummy.adjust_blood_volume(adjustment_amount, minimum: float.NegativeInfinity), adjustment_amount, "Setting adjustment proc minimum to -INFINITY results in an unexpected adjustment proc return value.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), expected_final_volume, "Setting adjustment proc minimum to -INFINITY results in an unexpected final volume.");

        await ((datum_reagents)dummy.reagents).add_reagent(typeof(datum_reagent_medicine_salglu_solution), 10);
        var saline = ((datum_reagents)dummy.reagents).has_reagent(typeof(datum_reagent_medicine_salglu_solution)) as datum_reagent_medicine_salglu_solution;
        dummy.set_blood_volume(SALINE_DILUTION_CAP);

        // Test if saline dilutes blood volume beyond the dilution cap.
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(apply_modifiers: TRUE), SALINE_DILUTION_CAP, "Saline goes above or below its dilution cap.");

        var dilution = dummy.has_status_effect(typeof(datum_status_effect_stacking_saline_glucose_dilution)) as datum_status_effect_stacking_saline_glucose_dilution;
        TEST_ASSERT_NOTNULL(dilution, "Saline did not apply its dilution effect when entering a mob");

        dummy.set_blood_volume(BLOOD_VOLUME_BAD);
        float expected_dilution = Dm.num(saline.volume) * dilution.get_blood_multiplier();
        expected_final_volume = Dm.num(dummy.get_blood_volume()) + expected_dilution;

        // Test if saline dilutes low blood volume properly.
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(apply_modifiers: TRUE), expected_final_volume, "Saline didn't dilute low blood by the expected amount.");

        ADD_TRAIT(dummy, TRAIT_NOBLOOD, TRAIT_GENERIC);

        // Test if adding TRAIT_NOBLOOD works properly.
        TEST_ASSERT(!Dm.truthy(dummy.can_have_blood()), "Adding TRAIT_NOBLOOD didn't make the mob have no blood.");
        TEST_ASSERT(!Dm.truthy(CAN_HAVE_BLOOD(dummy)), "Caching of blood volume status is screwed up after the addition of TRAIT_NOBLOOD.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), 0, "Blood volume wasn't emptied after the addition of TRAIT_NOBLOOD.");

        REMOVE_TRAIT(dummy, TRAIT_NOBLOOD, TRAIT_GENERIC);

        // Test if removing TRAIT_NOBLOOD works properly.
        TEST_ASSERT(Dm.truthy(dummy.can_have_blood()), "Removing TRAIT_NOBLOOD didn't make the mob have blood again.");
        TEST_ASSERT(Dm.truthy(CAN_HAVE_BLOOD(dummy)), "Caching of blood volume status is screwed up after the removal of TRAIT_NOBLOOD.");
        TEST_ASSERT_EQUAL(dummy.get_blood_volume(), dummy.default_blood_volume, "Blood volume wasn't fixed after the removal of TRAIT_NOBLOOD.");
    }
}
