// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/movement_order_sanity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED: tg runs this test on its unit test map; the room is the toy room (TgToyRoom.Install: real /turf/open/indestructible
// floors and /turf/closed/indestructible walls with tg's own Enter, the real /area/misc/testroom, SSatoms initialized), whose
// bottom left floor (2, 2) is run_loc_floor_bottom_left. The movement_tester's Move override keeps tg's fourth parameter
// name z_movement_flags (rules 7.2: the slot's name for it is update_dir). The test also asserts the four log lines of
// move-chain.md 3g exactly, as the move-chain spec asks: their words are the spec's, their coordinates are the toy room's
// (the rim of walls moves the corner from (1, 1) to (2, 2)).

// tg: code/modules/unit_tests/movement_order_sanity.dm:1 (/datum/unit_test/movement_order_sanity; class named by rules 2.3)
public sealed class datum_unit_test_movement_order_sanity : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:1
    public override async DmTask Run()
    {
        obj_movement_tester test_obj = allocate<obj_movement_tester>(run_loc_floor_bottom_left);
        DmList movement_cache = test_obj.movement_order;

        obj_movement_interceptor interceptor = allocate<obj_movement_interceptor>();
        await interceptor.forceMove(locate(((turf)run_loc_floor_bottom_left).x + 1, ((turf)run_loc_floor_bottom_left).y, ((turf)run_loc_floor_bottom_left).z));

        object did_move = await step(test_obj, EAST);

        TEST_ASSERT(Dm.truthy(did_move), "Object did not move at all.");
        TEST_ASSERT(QDELETED(test_obj), "Object was not qdeleted.");
        TEST_ASSERT(length(movement_cache) == 4, Dm.text("Movement order length was not the expected value of 4, got: [].\nMovement Log\n[]", length(movement_cache), jointext(movement_cache, "\n")));

        // Due to when the logging takes place, it will always be Move Move > Moved Moved instead of the reality of
        // Move > Moved > Move > Moved
        TEST_ASSERT(findtext((string)movement_cache[1], "Moving from") != 0, Dm.text("Movement step 1 was not a Move attempt.\nMovement Log\n[]", jointext(movement_cache, "\n")));
        TEST_ASSERT(findtext((string)movement_cache[2], "Moving from") != 0, Dm.text("Movement step 2 was not a Move attempt.\nMovement Log\n[]", jointext(movement_cache, "\n")));
        TEST_ASSERT(findtext((string)movement_cache[3], "Moved from") != 0, Dm.text("Movement step 3 was not a Moved() call.\nMovement Log\n[]", jointext(movement_cache, "\n")));
        TEST_ASSERT(findtext((string)movement_cache[4], "Moved from") != 0, Dm.text("Movement step 4 was not a Moved() call.\nMovement Log\n[]", jointext(movement_cache, "\n")));

        // The exact lines (move-chain.md 3g): the comment above says "Move Move > Moved Moved" because findtext also
        // matches the doMove line, which is "Abstractly Moving from". The bottom left floor is (2, 2) in the toy room.
        TEST_ASSERT_EQUAL((string)movement_cache[1], "Moving from (2, 2) to (3, 2)");
        TEST_ASSERT_EQUAL((string)movement_cache[2], "Abstractly Moving from (3, 2) to NULL");
        TEST_ASSERT_EQUAL((string)movement_cache[3], "Moved from (2, 2) to (3, 2)");
        TEST_ASSERT_EQUAL((string)movement_cache[4], "Moved from (3, 2) to NULL");
    }
}

// tg: code/modules/unit_tests/movement_order_sanity.dm:24 (/obj/movement_tester)
[DmPath("/obj/movement_tester")]
public class obj_movement_tester : obj
{
    // tg: code/modules/unit_tests/movement_order_sanity.dm:25
    public obj_movement_tester()
    {
        name = "movement debugger";
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:26 (an object-creating default, rules 10.2)
    public DmList movement_order;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        movement_order = Dm.list();
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:28
    [DmParams("newloc", "direct", "glide_size_override", "z_movement_flags")]
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        Dm.append(ref movement_order, Dm.text("Moving from ([], []) to []", loc.x, loc.y, Dm.truthy(newloc) ? Dm.text("([], [])", newloc.x, newloc.y) : "NULL"));
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:32
    public override async DmTask<object> doMove(atom destination = null)
    {
        Dm.append(ref movement_order, Dm.text("Abstractly Moving from ([], []) to []", loc.x, loc.y, Dm.truthy(destination) ? Dm.text("([], [])", destination.x, destination.y) : "NULL"));
        return await base.doMove(destination);
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:36
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        Dm.append(ref movement_order, Dm.text("Moved from ([], []) to []", old_loc.x, old_loc.y, Dm.truthy(loc) ? Dm.text("([], [])", loc.x, loc.y) : "NULL"));
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }
}

// tg: code/modules/unit_tests/movement_order_sanity.dm:40 (/obj/movement_interceptor)
[DmPath("/obj/movement_interceptor")]
public class obj_movement_interceptor : obj
{
    // tg: code/modules/unit_tests/movement_order_sanity.dm:41
    public obj_movement_interceptor()
    {
        name = "movement interceptor";
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_crossed)))));
        return dot;
    }

    // tg: code/modules/unit_tests/movement_order_sanity.dm:47
    [SignalHandler]
    public virtual void on_crossed(datum source = null, atom_movable arrived = null)
    {
        if (this == arrived)
            return;

        qdel(arrived);
    }
}
