// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/trait_addition_and_removal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// Simple Unit Test to ensure multiple methods of adding/removing a trait work as intended.
// tg: code/modules/unit_tests/trait_addition_and_removal.dm:2 (/datum/unit_test/trait_addition_and_removal; class named by rules 2.3)
public sealed class datum_unit_test_trait_addition_and_removal : TgUnitTest
{
    // tg: code/modules/unit_tests/trait_addition_and_removal.dm:4-12 (#define'd for this file and #undef'd at :86-94,
    // so the defines converter does not emit them: they are constants of the test)
    private const string TRAIT_UNIT_TEST_MAIN = "trait_main";
    private const string TRAIT_UNIT_TEST_ALT = "trait_alternate";
    private const string TRAIT_UNIT_TEST_A = "trait_a";
    private const string TRAIT_UNIT_TEST_B = "trait_b";
    private const string TRAIT_UNIT_TEST_C = "trait_c";
    private const string UNIT_TEST_SOURCE_MAIN = "source_main";
    private const string UNIT_TEST_SOURCE_ALT = "source_alt";
    private const string UNIT_TEST_SOURCE_A = "source_a";
    private const string UNIT_TEST_SOURCE_B = "source_b";

    // tg: code/modules/unit_tests/trait_addition_and_removal.dm:14
    public override async DmTask Run()
    {
        datum trait_target = allocate<datum>(); // traits work on the datum-level, so use a datum to ensure that it'll work on all children

        // We want to ensure that what we add is also removed when we're done with our "block" of tests, if it starts to get messy we might just have to reset the datum between tests to ensure cleanliness

        // The basics, if these fail burn down the codebase
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_A);
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_A), Dm.text("Basic trait addition failed for [], source being []!", TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_A));

        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B);
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Basic trait addition failed for [], source being []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B));
        TEST_ASSERT(HAS_TRAIT_FROM(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B), Dm.text("Failed to verify source for [], expected source being []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B));
        TEST_ASSERT(HAS_TRAIT_NOT_FROM(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_A), Dm.text("Failed to verify source for [], expected source being [] but was actually []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B, UNIT_TEST_SOURCE_A));

        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Basic trait removal failed for [], source being []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_B));
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_A), Dm.text("Trait [] was removed when it shouldn't have been!", TRAIT_UNIT_TEST_A));
        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_A);

        // Test adding the trait multiple times from different sources
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_A);
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_B);
        TEST_ASSERT(!HAS_TRAIT_FROM_ONLY(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_A), Dm.text("Failed to recognize that [] was added by both [] and []!", TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_A, UNIT_TEST_SOURCE_B));

        // as well as its removal
        REMOVE_TRAIT_NOT_FROM(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_A);
        TEST_ASSERT(!HAS_TRAIT_FROM(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_B), Dm.text("Failed to remove [] with [] when removal was expected!", TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_B));
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_MAIN), Dm.text("[] was completely removed when it should not have been!", TRAIT_UNIT_TEST_MAIN));
        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_MAIN, UNIT_TEST_SOURCE_A);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_MAIN), Dm.text("[] was not removed when it should have been!", TRAIT_UNIT_TEST_MAIN));

        // Test adding multiple traits from the same source
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN);
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN);
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Failed to add [] with common source []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN));

        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_C, UNIT_TEST_SOURCE_ALT);
        REMOVE_TRAITS_NOT_IN(trait_target, UNIT_TEST_SOURCE_MAIN);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_C), Dm.text("Failed to remove [] when expected to be removed with remove_traits_NOT_IN(), was added with source []!", TRAIT_UNIT_TEST_C, UNIT_TEST_SOURCE_ALT));

        // as well as its removal
        REMOVE_TRAITS_IN(trait_target, UNIT_TEST_SOURCE_MAIN);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_A), Dm.text("Failed to remove [] with common source []!", TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN));
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Failed to remove [] with common source []!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN));

        // Lastly, let's ensure that adding/removing traits using lists still works.
        // var/static/list/standardized_traits_list (:60): Run__standardized_traits_list below.
        trait_target.add_traits(Run__standardized_traits_list, UNIT_TEST_SOURCE_MAIN);
        foreach (object trait in Dm.each(Run__standardized_traits_list))
            TEST_ASSERT(HAS_TRAIT(trait_target, trait), Dm.text("Failed to add [] when using add_traits() using []!", trait, UNIT_TEST_SOURCE_MAIN));
        trait_target.remove_traits(Run__standardized_traits_list, UNIT_TEST_SOURCE_MAIN);
        foreach (object trait in Dm.each(Run__standardized_traits_list))
            TEST_ASSERT(!HAS_TRAIT(trait_target, trait), Dm.text("Failed to remove [] when using remove_traits() using []!", trait, UNIT_TEST_SOURCE_MAIN));

        // As well as ensure mixing-and-matching types of trait addition/removal works.
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN);
        ADD_TRAIT(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN);
        trait_target.remove_traits(Run__standardized_traits_list, UNIT_TEST_SOURCE_MAIN);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_A), Dm.text("Failed to remove [] when using remove_traits() using [] (was added using ADD_TRAIT())!", TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN));
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Failed to remove [] when using remove_traits() using [] (was added using ADD_TRAIT())!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN));
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_C), Dm.text("[] somehow came into existence when using remove_traits() using [] (what the actual fuck)!", TRAIT_UNIT_TEST_C, UNIT_TEST_SOURCE_MAIN));

        trait_target.add_traits(Run__standardized_traits_list, UNIT_TEST_SOURCE_MAIN);
        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN);
        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN);
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_A), Dm.text("Failed to remove [] when using REMOVE_TRAIT() using [] (was added using add_traits())!", TRAIT_UNIT_TEST_A, UNIT_TEST_SOURCE_MAIN));
        TEST_ASSERT(!HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_B), Dm.text("Failed to remove [] when using REMOVE_TRAIT() using [] (was added using add_traits())!", TRAIT_UNIT_TEST_B, UNIT_TEST_SOURCE_MAIN));
        TEST_ASSERT(HAS_TRAIT(trait_target, TRAIT_UNIT_TEST_C), Dm.text("[] was unexpectedly removed when using REMOVE_TRAIT() using [] (was added using add_traits())!", TRAIT_UNIT_TEST_C, UNIT_TEST_SOURCE_MAIN));
        REMOVE_TRAIT(trait_target, TRAIT_UNIT_TEST_C, UNIT_TEST_SOURCE_MAIN); //just for cleanliness+completeness

        TEST_ASSERT(!Dm.truthy(length(trait_target._status_traits)), "Failed to clean up all status traits at the end of the unit test!");
    }

    // tg: code/modules/unit_tests/trait_addition_and_removal.dm:60  var/static/list/standardized_traits_list
    // A proc-level static lives in the per-world state (rules 10.6: an accessor <proc>__<var> over a TgState field).
    // TgState is a partial class of the Tg assembly and cannot get a part here, so the test keeps the static in a
    // per-world extension of its own (World.GetExtension), which has the same lifetime: created once per world.
    private static ref DmList Run__standardized_traits_list => ref World.Current.GetExtension(static w => new Statics()).Run__standardized_traits_list;

    private sealed class Statics
    {
        public DmList Run__standardized_traits_list = Dm.list(TRAIT_UNIT_TEST_A, TRAIT_UNIT_TEST_B, TRAIT_UNIT_TEST_C);
    }
}
