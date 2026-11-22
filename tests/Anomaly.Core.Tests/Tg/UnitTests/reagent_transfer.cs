// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/reagent_transfer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// The whole test (M8 slice A).

/// Tests transferring reagents between two reagents datums.
// tg: code/modules/unit_tests/reagent_transfer.dm:2
public sealed class datum_unit_test_reagent_transfer : TgUnitTest
{
    // tg: code/modules/unit_tests/reagent_transfer.dm:4
    public override async DmTask Run()
    {
        datum_reagents source_reagents = allocate<datum_reagents>(100);
        datum_reagents target_reagents = allocate<datum_reagents>(100);

        // Quick test to make sure reagents add properly.
        await source_reagents.add_reagent(typeof(datum_reagent_water), 10);
        TEST_ASSERT_EQUAL(length(source_reagents.reagent_list), 1, Dm.text("Source reagents has [] unique reagents (expected 1).", length(source_reagents.reagent_list)));
        TEST_ASSERT_EQUAL(source_reagents.total_volume, 10, Dm.text("Source reagents has incorrect total_volume [] (expected 10).", source_reagents.total_volume));

        // Test to make sure the water reagent was added correctly.
        datum_reagent_water water_reagent = source_reagents.reagent_list[1] as datum_reagent_water;
        TEST_ASSERT(water_reagent != null, Dm.text("Incorrect reagent type detected source reagents: [] (expected /datum/reagent/water).", (source_reagents.reagent_list[1] as datum)?.type));
        TEST_ASSERT_EQUAL(water_reagent.volume, 10, Dm.text("Source reagents has [] reagent volume (expected 10).", water_reagent.volume));

        // Test to make sure reagents transfer properly.
        await source_reagents.trans_to(target_reagents, 10);
        TEST_ASSERT_EQUAL(length(source_reagents.reagent_list), 0, Dm.text("Source reagents has [] unique reagents after transfer (expected 0, possible duplication?)", length(source_reagents.reagent_list)));
        TEST_ASSERT_EQUAL(length(target_reagents.reagent_list), 1, Dm.text("Target reagents has [] unique reagents after transfer (expected 1).", length(target_reagents.reagent_list)));
        TEST_ASSERT_EQUAL(target_reagents.total_volume, 10, Dm.text("Target reagents has incorrect total_volume [] (expected 10).", source_reagents.total_volume));

        water_reagent = target_reagents.reagent_list[1] as datum_reagent_water;
        TEST_ASSERT(water_reagent != null, Dm.text("Incorrect reagent type detected in target reagents after transfer: [] (should be /datum/reagent/water).", (target_reagents.reagent_list[1] as datum)?.type));
        TEST_ASSERT_EQUAL(water_reagent.volume, 10, Dm.text("Target reagents has [] reagent volume (expected 10)", water_reagent.volume));
    }
}
