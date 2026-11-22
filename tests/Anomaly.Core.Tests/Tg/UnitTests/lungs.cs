// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/lungs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// PARTIAL (M8 slice D): the standard lungs' case (lungs_sanity) with the shared check; the plasmaman and lavaland lungs stay
// out (other species). ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install).

/// Tests the standard, plasmaman, and lavaland lungs organ to ensure breathing and suffocation behave as expected.
/// Performs a check on each main (can be life-sustaining) gas, and ensures gas alerts are only thrown when expected.
// tg: code/modules/unit_tests/lungs.dm:11
public sealed class datum_unit_test_lungs_lungs_sanity : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/lungs.dm:1  #define TEST_CHECK_BREATH_MESSAGE(lungs_organ, message) "[lungs_organ.type]/check_breath() [message]"
    private static string TEST_CHECK_BREATH_MESSAGE(obj_item_organ_lungs lungs_organ, string message) => Dm.text("[]/check_breath() []", lungs_organ.type, message);

    // tg: code/modules/unit_tests/lungs.dm:4  #define GET_MOLES(gas_mixture, gas_type) (gas_mixture.moles[gas_type] || 0)
    private static float GET_MOLES(datum_gas_mixture gas_mixture, Type gas_type) => Dm.num(gas_mixture.moles?[gas_type]);

    // tg: code/modules/unit_tests/lungs.dm:11
    public override async DmTask Run()
    {
        // "Standard" form of breathing.
        // 2500 Litres of O2/N2 gas mix, ideal for life.
        datum_gas_mixture test_mix = create_standard_mix();
        mob_living_carbon_human lab_rat = allocate<mob_living_carbon_human_consistent>();
        var test_lungs = allocate<obj_item_organ_lungs>();
        // Test one breath of O2/N2 mix.
        await lungs_test_check_breath("standard gas mixture", lab_rat, test_lungs, test_mix);

        // Suffocation with an empty gas mix.
        var empty_test_mix = allocate<datum_gas_mixture>();
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        test_lungs = allocate<obj_item_organ_lungs>();
        // Test one breath of nothing. Suffocate due to the breath being empty.
        await lungs_test_check_breath("empty gas mixture", lab_rat, test_lungs, empty_test_mix, expect_failure: true);

        // Suffocation with null. This does indeed happen normally.
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        test_lungs = allocate<obj_item_organ_lungs>();
        // Test one breath of nothing. Suffocate due to the breath being null.
        await lungs_test_check_breath("null", lab_rat, test_lungs, null, expect_failure: true);

        // Suffocation with Nitrogen.
        datum_gas_mixture nitro_test_mix = create_nitrogen_mix();
        lab_rat = allocate<mob_living_carbon_human_consistent>();
        test_lungs = allocate<obj_item_organ_lungs>();
        // Test one breath of Nitrogen. Suffocate due to the breath being 100% N2.
        await lungs_test_check_breath("pure Nitrogen", lab_rat, test_lungs, nitro_test_mix, expect_failure: true);
    }

    /// Comprehensive unit test for [/obj/item/organ/lungs/proc/check_breath()]
    /// If "expect_failure" is set to TRUE, the test ensures the given Human suffocated.
    // tg: code/modules/unit_tests/lungs.dm:73
    private async DmTask lungs_test_check_breath(string test_name, mob_living_carbon_human lab_rat, obj_item_organ_lungs test_lungs, datum_gas_mixture test_mix, bool expect_failure = false)
    {
        // Setup a small volume of gas which represents one "breath" from test_mix.
        datum_gas_mixture test_breath = null;

        if (test_mix != null)
        {
            float total_moles = Dm.num(test_mix.total_moles());
            if (total_moles > 0)
                test_breath = test_mix.remove(total_moles * BREATH_PERCENTAGE) as datum_gas_mixture;
        }

        if (test_breath == null)
            test_breath = allocate<datum_gas_mixture>(BREATH_VOLUME);

        // Backup of the breath mixture, to compare against after testing check_breath().
        datum_gas_mixture test_breath_backup = test_breath.copy() as datum_gas_mixture;

        // Get partial pressures for each "main" gas.
        float oxygen_pp = 0;
        float nitro_pp = 0;
        float co2_pp = 0;
        float plasma_pp = 0;
        if (Dm.num(test_breath.total_moles()) > 0)
        {
            oxygen_pp = Dm.num(test_breath.get_breath_partial_pressure(GET_MOLES(test_breath, typeof(datum_gas_oxygen))));
            nitro_pp = Dm.num(test_breath.get_breath_partial_pressure(GET_MOLES(test_breath, typeof(datum_gas_nitrogen))));
            co2_pp = Dm.num(test_breath.get_breath_partial_pressure(GET_MOLES(test_breath, typeof(datum_gas_carbon_dioxide))));
            plasma_pp = Dm.num(test_breath.get_breath_partial_pressure(GET_MOLES(test_breath, typeof(datum_gas_plasma))));
        }

        // Minimum and maximum gas tolerances for the 4 main life-sustaining gases.
        float min_oxygen = Dm.num(test_lungs.safe_oxygen_min);
        float min_nitro = Dm.num(test_lungs.safe_nitro_min);
        float min_plasma = Dm.num(test_lungs.safe_plasma_min);
        float max_oxygen = Dm.num(test_lungs.safe_oxygen_max);
        float max_co2 = Dm.num(test_lungs.safe_co2_max);
        float max_plasma = Dm.num(test_lungs.safe_plasma_max);

        // Test a single "breath" of air.
        object status_code = await test_lungs.check_breath(test_breath, lab_rat);

        // Ensures failed_last_breath is set as we expect, and that check_breath returns a corollary status code.
        if (expect_failure)
        {
            TEST_ASSERT(!Dm.truthy(status_code), TEST_CHECK_BREATH_MESSAGE(test_lungs, "returned truthy / status code 1 (success) when it wasn't expected."));
            TEST_ASSERT(Dm.truthy(lab_rat.failed_last_breath), TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("should suffocate from [].", test_name)));
        }
        else
        {
            TEST_ASSERT(Dm.truthy(status_code), TEST_CHECK_BREATH_MESSAGE(test_lungs, "returned falsy / status code 0 (failure) when it wasn't expected."));
            TEST_ASSERT(!Dm.truthy(lab_rat.failed_last_breath), TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("can't get a full breath from [].", test_name)));
        }

        // Checks each "main" gas to ensure gas alerts are thrown/inhibited when expected.
        lungs_test_alert_min(lab_rat, test_lungs, ALERT_NOT_ENOUGH_OXYGEN, min_oxygen, oxygen_pp);
        lungs_test_alert_max(lab_rat, test_lungs, ALERT_TOO_MUCH_OXYGEN, max_oxygen, oxygen_pp);

        lungs_test_alert_min(lab_rat, test_lungs, ALERT_NOT_ENOUGH_NITRO, min_nitro, nitro_pp);

        lungs_test_alert_max(lab_rat, test_lungs, ALERT_TOO_MUCH_CO2, max_co2, co2_pp);

        lungs_test_alert_min(lab_rat, test_lungs, ALERT_NOT_ENOUGH_PLASMA, min_plasma, plasma_pp);
        lungs_test_alert_max(lab_rat, test_lungs, ALERT_TOO_MUCH_PLASMA, max_plasma, plasma_pp);

        // Track the volumes of O2 and CO2 which are expected to be exhaled.
        float expected_oxygen = GET_MOLES(test_breath_backup, typeof(datum_gas_oxygen));
        float expected_nitro = GET_MOLES(test_breath_backup, typeof(datum_gas_nitrogen));
        float expected_co2 = GET_MOLES(test_breath_backup, typeof(datum_gas_carbon_dioxide));
        float expected_plasma = GET_MOLES(test_breath_backup, typeof(datum_gas_plasma));

        // Setup expectations for main gas exchange tests.
        if (min_oxygen != 0)
        {
            expected_co2 += expected_oxygen;
            expected_oxygen = 0;
        }
        if (min_nitro != 0)
        {
            expected_co2 += GET_MOLES(test_breath_backup, typeof(datum_gas_nitrogen));
            expected_nitro = 0;
        }
        if (min_plasma != 0)
        {
            expected_co2 += GET_MOLES(test_breath_backup, typeof(datum_gas_plasma));
            expected_plasma = 0;
        }

        // Validate conversion of inhaled gas to exhaled gas.
        if (min_oxygen != 0)
        {
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_oxygen)), expected_oxygen)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should consume all Oxygen initially present in the breath."));
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_carbon_dioxide)), expected_co2)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should convert Oxygen into an equivalent volume of CO2."));
        }
        if (min_nitro != 0)
        {
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_nitrogen)), expected_nitro)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should consume all Nitrogen initially present in the breath."));
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_carbon_dioxide)), expected_co2)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should convert Nitrogen into an equivalent volume of CO2."));
        }
        if (min_plasma != 0)
        {
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_plasma)), expected_plasma)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should consume all Plasma initially present in the breath."));
            TEST_ASSERT(Dm.truthy(molar_cmp_equals(GET_MOLES(test_breath, typeof(datum_gas_carbon_dioxide)), expected_co2)), TEST_CHECK_BREATH_MESSAGE(test_lungs, "should convert Plasma into an equivalent volume of CO2."));
        }
    }

    /// Tests minimum gas alerts by comparing gas pressure.
    // tg: code/modules/unit_tests/lungs.dm:162
    private void lungs_test_alert_min(mob_living_carbon_human lab_rat, obj_item_organ_lungs test_lungs, string alert_name, float min_pressure, float pressure)
    {
        bool alert_thrown = Dm.truthy(lab_rat.has_alert(alert_name));
        bool pressure_safe = (pressure >= min_pressure) || (min_pressure == 0);
        TEST_ASSERT(!pressure_safe && alert_thrown || pressure_safe, TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("failed to throw alert [] when expected.", alert_name)));
        TEST_ASSERT(pressure_safe && !alert_thrown || !pressure_safe, TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("threw alert [] when it wasn't expected.", alert_name)));
    }

    /// Tests maximum gas alerts by comparing gas pressure.
    // tg: code/modules/unit_tests/lungs.dm:169
    private void lungs_test_alert_max(mob_living_carbon_human lab_rat, obj_item_organ_lungs test_lungs, string alert_name, float max_pressure, float pressure)
    {
        bool alert_thrown = Dm.truthy(lab_rat.has_alert(alert_name));
        bool pressure_safe = (pressure <= max_pressure) || (max_pressure == 0);
        TEST_ASSERT(!pressure_safe && alert_thrown || pressure_safe, TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("failed to throw alert [] when expected.", alert_name)));
        TEST_ASSERT(pressure_safe && !alert_thrown || !pressure_safe, TEST_CHECK_BREATH_MESSAGE(test_lungs, Dm.text("threw alert [] when it wasn't expected.", alert_name)));
    }

    /// Set up a 2500-Litre gas mixture with the given gases and percentages.
    // tg: code/modules/unit_tests/lungs.dm:176
    private datum_gas_mixture create_gas_mix(DmList gas_to_percent)
    {
        var test_mix = allocate<datum_gas_mixture>(2500);
        test_mix.temperature = T20C;
        foreach ((object gas_type, object percent) in Dm.pairs(gas_to_percent))
        {
            test_mix.add_gas(gas_type);
            test_mix.moles[gas_type] = ONE_ATMOSPHERE * 2500 / (R_IDEAL_GAS_EQUATION * T20C) * Dm.num(percent);
        }
        return test_mix;
    }

    /// Set up an O2/N2 gas mix which is "ideal" for organic life.
    // tg: code/modules/unit_tests/lungs.dm:185
    private datum_gas_mixture create_standard_mix() => create_gas_mix(Dm.list(Dm.kv(typeof(datum_gas_oxygen), O2STANDARD), Dm.kv(typeof(datum_gas_nitrogen), N2STANDARD)));

    /// Set up a pure Nitrogen gas mix.
    // tg: code/modules/unit_tests/lungs.dm:189
    private datum_gas_mixture create_nitrogen_mix() => create_gas_mix(Dm.list(Dm.kv(typeof(datum_gas_nitrogen), 1)));
}
