// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/dcs_check_list_arguments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/**
 * list arguments for bespoke elements are treated as a text ref in the ID, like any other datum.
 * Which means that, unless cached, using lists as arguments will lead to multiple instance of the same element
 * being created over and over.
 *
 * Because of how it works, this unit test checks that these list datum args
 * do not share similar contents (when rearranged in descending alpha-numerical order), to ensure that
 * the least necessary amount of elements is created. So, using static lists may not be enough,
 * for example, in the case of two different critters using the death_drops element to drop ectoplasm on death, since,
 * despite being static lists, the two are different instances assigned to different mob types.
 *
 * Most of the time, you won't encounter two different static lists with similar contents used as element args,
 * meaning using static lists is accepted. However, should that happen, it's advised to replace the instances
 * with either string_list(), string_assoc_list(), string_assoc_nested_list() or string_numbers_list(), depending on the contents of the list.
 *
 * In the case of an element where the position of the contents of each datum list argument is important,
 * ELEMENT_DONT_SORT_LIST_ARGS should be added to its flags, to prevent such issues where the contents are similar
 * when sorted, but the element instances are not.
 *
 * In the off-chance the element is not compatible with this unit test (such as for connect_loc et simila),
 * you can also use ELEMENT_NO_LIST_UNIT_TEST so that they won't be processed by this unit test at all.
 */
// tg: code/modules/unit_tests/dcs_check_list_arguments.dm:23 (/datum/unit_test/dcs_check_list_arguments; class named by rules 2.3)
// Not sealed: the samples below derive from it to feed it failing cases.
public class datum_unit_test_dcs_check_list_arguments : TgUnitTest
{
    public datum_unit_test_dcs_check_list_arguments()
    {
        /**
         * This unit test requires every (unless ignored) atom to have been created at least once
         * for a more accurate search, which is why it's run after create_and_destroy is done running.
         */
        // tg: code/modules/unit_tests/dcs_check_list_arguments.dm:28. parent_type::test_flags is /datum/unit_test's
        // default, UNIT_TEST_BASIC (unit_test.dm:39); the harness is no DM type for Dm.initial to read.
        test_flags = Dm.bitand(UNIT_TEST_BASIC, Dm.bitnot(UNIT_TEST_DEBUG_MAP_ONLY));
        // tg: code/modules/unit_tests/dcs_check_list_arguments.dm:29
        priority = TEST_AFTER_CREATE_AND_DESTROY;
    }

    /// <summary>tg's New, then the stand-in for what the earlier tests leave in SSdcs (see <see cref="create_and_destroy_stand_in"/>).</summary>
    public override void New()
    {
        base.New();
        create_and_destroy_stand_in();
    }

    /// <summary>
    /// ADAPTED. In tg, SSdcs.arguments_that_are_lists_by_element is filled during the whole run, mostly by
    /// create_and_destroy creating every atom: each bespoke element argument that is a list goes through
    /// add_to_arguments_that_are_lists from GetIdFromArguments when PERFORM_ALL_TESTS(dcs_check_list_arguments) is true
    /// (controllers/subsystem/dcs.dm:69-70, :82-83). The port has no content that attaches such elements yet,
    /// and PERFORM_ALL_TESTS is FALSE in the production build it translates (rules 1.7), so GetIdFromArguments never
    /// records a list argument (it identifies one by its ref, see BespokeElementTests). So this stand-in
    /// makes the add_to_arguments_that_are_lists calls such element arguments would make, for the fixture elements
    /// below: one static list used twice (cached, so one entry), lists with different contents, the same contents in two
    /// orders for an element that does not sort, and similar lists for an element that opts out.
    /// </summary>
    protected virtual void create_and_destroy_stand_in()
    {
        DmList shared = Dm.list("b", "a");
        Record(shared, typeof(test_dcs_list_args));
        Record(shared, typeof(test_dcs_list_args));
        Record(Dm.list("c", "a"), typeof(test_dcs_list_args));
        Record(Dm.list("a", "b"), typeof(test_dcs_list_args_unsorted));
        Record(Dm.list("b", "a"), typeof(test_dcs_list_args_unsorted));
        Record(Dm.list("a"), typeof(test_dcs_list_args_ignored));
        Record(Dm.list("a"), typeof(test_dcs_list_args_ignored));
    }

    /// <summary>What GetIdFromArguments does with a list argument under UNIT_TESTS (dcs.dm:69-70, :82-83).</summary>
    protected static void Record(DmList argument, Type element_type)
        => SSdcs.add_to_arguments_that_are_lists(argument, element_type);

    // tg: code/modules/unit_tests/dcs_check_list_arguments.dm:31
    public override async DmTask Run()
    {
        float we_failed = FALSE;
        foreach (object element_type in Dm.each(SSdcs.arguments_that_are_lists_by_element))
        {
            // Keeps track of the lists that shouldn't be compared with again.
            DmList to_ignore = Dm.list();
            DmList superlist = SSdcs.arguments_that_are_lists_by_element[element_type] as DmList;
            foreach (DmList current in Dm.each_cast<DmList>(superlist))
            {
                to_ignore[current] = TRUE;
                DmList bad_lists = null;
                foreach (DmList compare in Dm.each_cast<DmList>(superlist))
                {
                    if (Dm.truthy(to_ignore[compare]))
                        continue;
                    if (Dm.truthy(deep_compare_list(current, compare)))
                    {
                        if (!Dm.truthy(bad_lists))
                            bad_lists = Dm.list(Dm.list(current));
                        Dm.append(ref bad_lists, Dm.list(compare));
                        to_ignore[compare] = TRUE;
                    }
                }
                if (Dm.truthy(bad_lists))
                {
                    we_failed = TRUE;
                    //Include the original, unsorted list in the report. It should be easier to find by the contributor.
                    DmList unsorted_list = superlist[current] as DmList;
                    TEST_FAIL(Dm.text("Found [] datum list arguments with similar contents for []. Contents: [].", length(bad_lists), element_type, json_encode(unsorted_list)));
                }
            }
        }
        ///Let's avoid sending the same instructions over and over, as it's just going to clutter the CI and confuse someone.
        if (Dm.truthy(we_failed))
            TEST_FAIL("Ensure that each list is static or cached. string_list() (as well as similar procs) is your friend here.\nCheck the documentation from dcs_check_list_arguments.dm for more information!");
    }
}

// Fixture elements for the stand-in: bespoke elements that take a list argument (the port has none yet).

/// <summary>A bespoke element with a list argument; its list arguments are sorted before they are compared.</summary>
[DmPath("/datum/element/test_dcs_list_args")]
public class test_dcs_list_args : datum_element
{
    public test_dcs_list_args()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }
}

/// <summary>The same with ELEMENT_DONT_SORT_LIST_ARGS: the order of a list argument matters.</summary>
[DmPath("/datum/element/test_dcs_list_args_unsorted")]
public class test_dcs_list_args_unsorted : datum_element
{
    public test_dcs_list_args_unsorted()
    {
        element_flags = Dm.bitor(ELEMENT_BESPOKE, ELEMENT_DONT_SORT_LIST_ARGS);
        argument_hash_start_idx = 2;
    }
}

/// <summary>The same with ELEMENT_NO_LIST_UNIT_TEST: never recorded.</summary>
[DmPath("/datum/element/test_dcs_list_args_ignored")]
public class test_dcs_list_args_ignored : datum_element
{
    public test_dcs_list_args_ignored()
    {
        element_flags = Dm.bitor(ELEMENT_BESPOKE, ELEMENT_NO_LIST_UNIT_TEST);
        argument_hash_start_idx = 2;
    }
}

// Samples that must fail, run through RunOnce (internal, so xunit does not run them as tests of their own).

/// <summary>Two different list instances with the same contents in another order: two elements where one would do.</summary>
internal sealed class dcs_check_list_arguments_similar_sample : datum_unit_test_dcs_check_list_arguments
{
    protected override void create_and_destroy_stand_in()
    {
        Record(Dm.list("b", "a"), typeof(test_dcs_list_args));
        Record(Dm.list("c"), typeof(test_dcs_list_args));
        Record(Dm.list("a", "b"), typeof(test_dcs_list_args));
    }
}

/// <summary>An element that does not sort still fails for two instances with the same contents in the same order.</summary>
internal sealed class dcs_check_list_arguments_unsorted_sample : datum_unit_test_dcs_check_list_arguments
{
    protected override void create_and_destroy_stand_in()
    {
        Record(Dm.list("a", "b"), typeof(test_dcs_list_args_unsorted));
        Record(Dm.list("a", "b"), typeof(test_dcs_list_args_unsorted));
        Record(Dm.list("a", "b"), typeof(test_dcs_list_args_unsorted));
    }
}

/// <summary>Checks what the stand-in leaves in SSdcs before tg's Run, so that the passing test is not vacuous.</summary>
internal sealed class dcs_check_list_arguments_shape_sample : datum_unit_test_dcs_check_list_arguments
{
    public override async DmTask Run()
    {
        DmList by_element = SSdcs.arguments_that_are_lists_by_element;
        TEST_ASSERT_EQUAL(length(by_element), 2, "the ignored element is not recorded (dcs.dm:97-98)");
        DmList sorted = by_element[typeof(test_dcs_list_args)] as DmList;
        TEST_ASSERT_EQUAL(length(sorted), 2, "the shared list is sorted once (dcs.dm:105-107), so it is one key");
        TEST_ASSERT_EQUAL(json_encode(sorted[1]), "[\"a\",\"b\"]", "the keys are the sorted copies");
        TEST_ASSERT_EQUAL(json_encode(sorted[2]), "[\"a\",\"c\"]", "the keys are the sorted copies");
        TEST_ASSERT_EQUAL(json_encode(sorted[sorted[1]]), "[\"b\",\"a\"]", "each sorted copy maps to the original list");
        DmList unsorted = by_element[typeof(test_dcs_list_args_unsorted)] as DmList;
        TEST_ASSERT_EQUAL(json_encode(unsorted[1]), "[\"a\",\"b\"]", "ELEMENT_DONT_SORT_LIST_ARGS keeps the lists as they are (dcs.dm:103-104)");
        TEST_ASSERT_EQUAL(json_encode(unsorted[2]), "[\"b\",\"a\"]", "ELEMENT_DONT_SORT_LIST_ARGS keeps the lists as they are (dcs.dm:103-104)");
        TEST_ASSERT(unsorted[1] == unsorted[unsorted[1]], "an unsorted list is its own key");
        await base.Run();
    }
}

public class DcsCheckListArgumentsPortTests
{
    [Fact]
    public void TheStandInRecordsWhatTheTestReads()
    {
        Assert.Null(new dcs_check_list_arguments_shape_sample().RunOnce());
    }

    [Fact]
    public void ListArgumentsWithSimilarContentsFailTheTestOnceWithTheUnsortedListAndOnceWithTheAdvice()
    {
        string log = new dcs_check_list_arguments_similar_sample().RunOnce();

        string[] failures = log.Split("\tFAILURE #", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, failures.Length);
        Assert.StartsWith("1: Found 2 datum list arguments with similar contents for /datum/element/test_dcs_list_args. Contents: [\"b\",\"a\"]. at tests/Anomaly.Core.Tests/Tg/UnitTests/dcs_check_list_arguments.cs:", failures[0]);
        Assert.StartsWith("2: Ensure that each list is static or cached. string_list() (as well as similar procs) is your friend here.\nCheck the documentation from dcs_check_list_arguments.dm for more information! at ", failures[1]);
    }

    [Fact]
    public void AnElementThatDoesNotSortFailsForListsInTheSameOrder()
    {
        string log = new dcs_check_list_arguments_unsorted_sample().RunOnce();

        Assert.StartsWith("\tFAILURE #1: Found 3 datum list arguments with similar contents for /datum/element/test_dcs_list_args_unsorted. Contents: [\"a\",\"b\"].", log);
        Assert.Contains("\tFAILURE #2: Ensure that each list is static or cached.", log);
        Assert.DoesNotContain("FAILURE #3", log);
    }
}
