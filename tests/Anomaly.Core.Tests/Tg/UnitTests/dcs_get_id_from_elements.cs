// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/dcs_get_id_from_elements.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// Tests that DCS' GetIdFromArguments works as expected with standard and odd cases
// tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:2 (/datum/unit_test/dcs_get_id_from_arguments; class named by rules 2.3)
public sealed class datum_unit_test_dcs_get_id_from_arguments : TgUnitTest
{
    // tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:4
    public override async DmTask Run()
    {
        // ADAPTED: in tg src is the test, a /datum/unit_test; the harness's test is not a datum and has no world id for
        // REF(), so an allocated datum stands for it.
        datum src = allocate<datum>();

        assert_equal(Dm.list(1), Dm.list(1));
        assert_equal(Dm.list(1, 2), Dm.list(1, 2));
        assert_equal(Dm.list(src), Dm.list(src));

        assert_equal(
            Dm.list(Dm.kv("a", "x"), Dm.kv("b", "y"), Dm.kv("c", "z")),
            Dm.list(Dm.kv("b", "y"), Dm.kv("a", "x"), Dm.kv("c", "z")),
            Dm.list(Dm.kv("c", "z"), Dm.kv("a", "x"), Dm.kv("b", "y"))
        );

        TEST_ASSERT_NOTEQUAL(get_id_from_arguments(Dm.list(1, 2)), get_id_from_arguments(Dm.list(2, 1)), "Swapped arguments should not return the same id");
        TEST_ASSERT_NOTEQUAL(get_id_from_arguments(Dm.list(1, Dm.kv("a", "x"))), get_id_from_arguments(Dm.list(1)), "Named arguments were ignored when creating ids");
        TEST_ASSERT_NOTEQUAL(get_id_from_arguments(Dm.list(1, Dm.kv("a", "x"))), get_id_from_arguments(Dm.list(Dm.kv("a", "x"))), "Unnamed arguments were ignored when creating ids");
        TEST_ASSERT_NOTEQUAL(get_id_from_arguments(Dm.list(src)), get_id_from_arguments(Dm.list(world)), "References to different datums should not return the same id");

        TEST_ASSERT_NOTEQUAL(get_id_from_arguments(Dm.list()), SSdcs.GetIdFromArguments(Dm.list(typeof(datum_element_dcs_get_id_from_arguments_mock_element2))), "Different elements should not match the same id");
    }

    // tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:22
    public void assert_equal(object reference = null, params object[] __extra)
    {
        object result = get_id_from_arguments(reference as DmList);

        // Start at 1 so the 2nd argument is 2
        float index = 1;

        DmList args = Dm.args(reference, __extra);
        foreach (object other_case in Dm.each(args))
        {
            index += 1;

            object other_result = get_id_from_arguments(other_case as DmList);

            if (Dm.eq(other_result, result))
                continue;

            TEST_FAIL(Dm.text("Case #[] produces a different GetIdFromArguments result from the first. [] != []", index, other_result, result));
        }
    }

    // tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:38
    public object get_id_from_arguments(DmList arguments = null)
    {
        return SSdcs.GetIdFromArguments(Dm.add(Dm.list(typeof(datum_element_dcs_get_id_from_arguments_mock_element)), arguments) as DmList);
    }
}

// The two mock elements are tg's own (declared by the unit test file, so compiled only with UNIT_TESTS): they keep
// their tg paths.

// Necessary because GetIdFromArguments uses argument_hash_start_idx from an element type
// tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:42
[DmPath("/datum/element/dcs_get_id_from_arguments_mock_element")]
public partial class datum_element_dcs_get_id_from_arguments_mock_element : datum_element
{
    public datum_element_dcs_get_id_from_arguments_mock_element()
    {
        argument_hash_start_idx = 2;
    }
}

// tg: code/modules/unit_tests/dcs_get_id_from_elements.dm:45
[DmPath("/datum/element/dcs_get_id_from_arguments_mock_element2")]
public partial class datum_element_dcs_get_id_from_arguments_mock_element2 : datum_element
{
    public datum_element_dcs_get_id_from_arguments_mock_element2()
    {
        argument_hash_start_idx = 2;
    }
}
