// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/connect_loc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (wave 4, package D) for the whole file:
// - tg runs these tests after SSatoms initialized, on the unit test map. Until the map loader exists (M3), the room is the toy
//   room (TgToyRoom.Install, W4.M: real floors and walls with tg's own Enter, the real /area/misc/testroom, SSatoms
//   initialized), whose bottom left and top right floors are run_loc_floor_bottom_left and run_loc_floor_top_right.
// - /obj/item/watches_mock_calls is the obj fixture /obj/test_support_watches_mock_calls: /obj/item's Initialize is
//   translated with the items (later), and nothing here reads an item var.
// - connect_loc_change_turf (:24-47) needs ChangeTurf (M3): not ported yet.

/// Test that the connect_loc element handles basic movement cases
// tg: code/modules/unit_tests/connect_loc.dm:4 (/datum/unit_test/connect_loc_basic)
public sealed class datum_unit_test_connect_loc_basic : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/connect_loc.dm:6
    public override async DmTask Run()
    {
        obj_test_support_watches_mock_calls watcher = allocate<obj_test_support_watches_mock_calls>();

        turf current_turf = get_turf(watcher);

        if (Dm.truthy(current_turf._listen_lookup?[obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL]))
            current_turf._SendSignal(obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL, Dm.list(current_turf));
        TEST_ASSERT_EQUAL(watcher.times_called, 1, "After firing mock signal, connect_loc didn't send it");

        await watcher.forceMove(run_loc_floor_top_right as atom);

        if (Dm.truthy(current_turf._listen_lookup?[obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL]))
            current_turf._SendSignal(obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL, Dm.list(current_turf));
        TEST_ASSERT_EQUAL(watcher.times_called, 1, "Mock signal was fired on old turf, but connect_loc still picked it up");

        current_turf = get_turf(watcher);
        if (Dm.truthy(current_turf._listen_lookup?[obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL]))
            current_turf._SendSignal(obj_test_support_watches_mock_calls.COMSIG_MOCK_SIGNAL, Dm.list(current_turf));
        TEST_ASSERT_EQUAL(watcher.times_called, 2, "Mock signal was fired after turf move, but it wasn't picked up");
    }
}

/// Tests that multiple objects can have connect_loc on the same turf without runtimes.
// tg: code/modules/unit_tests/connect_loc.dm:50 (/datum/unit_test/connect_loc_multiple_on_turf)
public sealed class datum_unit_test_connect_loc_multiple_on_turf : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/connect_loc.dm:52
    public override async DmTask Run()
    {
        obj_test_support_watches_mock_calls watcher_one = allocate<obj_test_support_watches_mock_calls>(run_loc_floor_bottom_left);
        qdel(watcher_one);

        obj_test_support_watches_mock_calls watcher_two = allocate<obj_test_support_watches_mock_calls>(run_loc_floor_bottom_left);
        qdel(watcher_two);
    }
}

/// <summary>
/// tg's <c>/obj/item/watches_mock_calls</c> (connect_loc.dm:59) as an obj fixture (ADAPTED, see the note at the top).
/// </summary>
// tg: code/modules/unit_tests/connect_loc.dm:59
[DmPath("/obj/test_support_watches_mock_calls")]
public class obj_test_support_watches_mock_calls : obj
{
    // tg: code/modules/unit_tests/connect_loc.dm:1  #define COMSIG_MOCK_SIGNAL "mock_signal" (file-local, #undef at :75)
    internal const string COMSIG_MOCK_SIGNAL = "mock_signal";

    // tg: code/modules/unit_tests/connect_loc.dm:60
    public object times_called;

    // tg: code/modules/unit_tests/connect_loc.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        Initialize__connections ??= Dm.list(Dm.kv(COMSIG_MOCK_SIGNAL, nameof(on_receive_mock_signal)));

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__connections));
        return dot;
    }

    // tg: code/modules/unit_tests/connect_loc.dm:71
    [SignalHandler]
    public virtual void on_receive_mock_signal(datum source = null)
    {
        Dm.append(ref times_called, 1);
    }

    // The proc-level static of Initialize (var/static/list/connections, :65) in a per-world extension (rules 15).
    private sealed class Statics
    {
        public DmList connections;
    }

    private static ref DmList Initialize__connections => ref World.Current.GetExtension(static _ => new Statics()).connections;
}
