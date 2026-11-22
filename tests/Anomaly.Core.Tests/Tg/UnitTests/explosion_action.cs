// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/explosion_action.dm (the turf and obj parts)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// Tests the EX_ACT macro on several different types of atoms to ensure that it still works as expected.
// tg: code/modules/unit_tests/explosion_action.dm:2 (/datum/unit_test/explosion_action; class named by rules 2.3)
//
// ADAPTED (M5x slice 1): the turf part (execute_turf_tests) and the obj part (execute_obj_tests). The mob part
// (execute_mob_tests: simple and basic animals, aliens, the armoured corgi and the bomb-immune human) waits for the mobs'
// ex_act (M5x slice 3; simple_animal, basic mobs, aliens and the corgi's inventory are not ported, M10+). A turf reference
// after its ChangeTurf is the replaced object in the port (object-model.md 7.1), so where tg goes on using test_open_turf and
// test_closed_turf the port takes the turf ChangeTurf returned or the turf now at the cell (TURF-IDENTITY). The room is the
// toy room (TgToyRoom); its turf contents_explosion queues into SSexplosions, which tg's MC always creates.
public sealed class datum_unit_test_explosion_action : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        if (SSexplosions == null)
            Dm.@new<datum_controller_subsystem_explosions>();
    }

    // tg: code/modules/unit_tests/explosion_action.dm:10
    public override async DmTask Run()
    {
        // We split up this `Run()` into multiple parts based on the over-arching parent type. This is because all of them have different core implementations of `EX_ACT()`, and we want to test all.
        // execute_mob_tests(): ADAPTED, M5x slice 3 (see the header)
        await execute_turf_tests();
        await execute_obj_tests();
    }

    // tg: code/modules/unit_tests/explosion_action.dm:111  #define OPEN_FLOOR_TYPE /turf/open/floor (file-local, #undef at :169)
    private static readonly Type OPEN_FLOOR_TYPE = typeof(turf_open_floor);
    // tg: code/modules/unit_tests/explosion_action.dm:112  #define CLOSED_FLOOR_TYPE /turf/closed/wall (file-local, #undef at :170)
    private static readonly Type CLOSED_FLOOR_TYPE = typeof(turf_closed_wall);

    /// Tests the `EX_ACT()` macro on turf subtypes to ensure some level of the underlying framework still functions.
    // tg: code/modules/unit_tests/explosion_action.dm:115
    private async DmTask execute_turf_tests()
    {
        turf run_loc_floor_bottom_left = this.run_loc_floor_bottom_left as turf;
        turf run_loc_floor_top_right = this.run_loc_floor_top_right as turf;
        float bl_x = run_loc_floor_bottom_left.x, bl_y = run_loc_floor_bottom_left.y, bl_z = run_loc_floor_bottom_left.z;
        float tr_x = run_loc_floor_top_right.x, tr_y = run_loc_floor_top_right.y, tr_z = run_loc_floor_top_right.z;
        turf test_open_turf = run_loc_floor_bottom_left; // we'll clean this up later like Create and Destroy dw
        Type original_open_turf_type = run_loc_floor_bottom_left.type;
        // ADAPTED: ChangeTurf's new_baseturfs is a list in the port (a single baseturf type is its one-entry list)
        DmList original_open_baseturfs = islist(run_loc_floor_bottom_left.baseturfs) ? ((DmList)run_loc_floor_bottom_left.baseturfs).Copy() : Dm.list(run_loc_floor_bottom_left.baseturfs);

        test_open_turf = await test_open_turf.ChangeTurf(OPEN_FLOOR_TYPE) as turf;
        await EX_ACT(test_open_turf, EXPLODE_NONE, test_open_turf); // regardless of severity, this should scrape away the floor
        test_open_turf = locate(bl_x, bl_y, bl_z);
        TEST_ASSERT_NOTEQUAL(test_open_turf.type, OPEN_FLOOR_TYPE, "EX_ACT() with EXPLODE_NONE severity should have scraped away the floor, but instead saw zero changes!");
        test_open_turf = await test_open_turf.ChangeTurf(original_open_turf_type, original_open_baseturfs) as turf; // reset it back to original state before we go again to clear up any potential mess

        test_open_turf = await test_open_turf.ChangeTurf(OPEN_FLOOR_TYPE) as turf;
        await EX_ACT(test_open_turf, EXPLODE_DEVASTATE); // we should scrape away to space here, devestation severity has no probability of altering what it does.
        test_open_turf = locate(bl_x, bl_y, bl_z);
        TEST_ASSERT_NOTEQUAL(test_open_turf.type, OPEN_FLOOR_TYPE, "EX_ACT() with EXPLODE_DEVASTATE severity should have scraped away the floor, but instead saw zero changes!");
        await test_open_turf.ChangeTurf(original_open_turf_type, original_open_baseturfs);

        // invert the order of ourselves setting up the `/turf/closed` checks because we don't automatically start off as an open turf here
        run_loc_floor_top_right = locate(tr_x, tr_y, tr_z);
        Type original_closed_turf_type = run_loc_floor_top_right.type; // should just be /turf/closed/wall but lets be hardy against changes to the map template should they arise
        DmList original_closed_baseturfs = islist(run_loc_floor_top_right.baseturfs) ? ((DmList)run_loc_floor_top_right.baseturfs).Copy() : Dm.list(run_loc_floor_top_right.baseturfs);
        turf_closed_wall test_closed_turf = await run_loc_floor_top_right.ChangeTurf(CLOSED_FLOOR_TYPE) as turf_closed_wall;

        await EX_ACT(test_closed_turf, EXPLODE_NONE, test_closed_turf); // regardless of severity, this should dismantle the wall
        TEST_ASSERT_NOTEQUAL(locate(tr_x, tr_y, tr_z).type, CLOSED_FLOOR_TYPE, "EX_ACT() with EXPLODE_NONE severity (setting itself as the target) should have eviscerated the wall, but instead saw zero changes!");
        await locate(tr_x, tr_y, tr_z).ChangeTurf(original_closed_turf_type, original_closed_baseturfs);

        test_closed_turf = await locate(tr_x, tr_y, tr_z).ChangeTurf(CLOSED_FLOOR_TYPE) as turf_closed_wall;
        float cached_hardness = test_closed_turf.hardness;
        test_closed_turf.hardness = 100; // we'll set the hardness to 100 so we don't get errant failures
        await EX_ACT(test_closed_turf, EXPLODE_LIGHT);
        TEST_ASSERT_NOTEQUAL(locate(tr_x, tr_y, tr_z).type, CLOSED_FLOOR_TYPE, "EX_ACT() with EXPLODE_LIGHT severity should have dismantled the wall, but instead saw zero changes!");
        await locate(tr_x, tr_y, tr_z).ChangeTurf(original_closed_turf_type, original_closed_baseturfs);

        // just to make sure the hardness is what we wanted it to be, there's no real reason why the hardness should inherit between changeturfs but by God we should guard against it.
        test_closed_turf = await locate(tr_x, tr_y, tr_z).ChangeTurf(CLOSED_FLOOR_TYPE) as turf_closed_wall;
        test_closed_turf.hardness = cached_hardness;
        await EX_ACT(test_closed_turf, EXPLODE_HEAVY); // wall will be dismantled at the very least
        TEST_ASSERT_NOTEQUAL(locate(tr_x, tr_y, tr_z).type, CLOSED_FLOOR_TYPE, "EX_ACT() with EXPLODE_HEAVY severity should have dismantled the wall, but instead saw zero changes!");
        await locate(tr_x, tr_y, tr_z).ChangeTurf(original_closed_turf_type, original_closed_baseturfs);

        test_closed_turf = await locate(tr_x, tr_y, tr_z).ChangeTurf(CLOSED_FLOOR_TYPE) as turf_closed_wall;
        await EX_ACT(test_closed_turf, EXPLODE_DEVASTATE); // yeah we're definitely not seeing the wall anymore
        TEST_ASSERT_NOTEQUAL(locate(tr_x, tr_y, tr_z).type, CLOSED_FLOOR_TYPE, "EX_ACT() with EXPLODE_DEVASTATE severity should have eviscerated the wall, but instead saw zero changes!");
        await locate(tr_x, tr_y, tr_z).ChangeTurf(original_closed_turf_type, original_open_baseturfs);

        // to be super duper sooper safe, clean up both turfs we changed a second time to ensure we aren't yonking downstream tests by invoking them on the actual turfs rather than what we casted
        await locate(bl_x, bl_y, bl_z).ChangeTurf(original_open_turf_type, original_open_baseturfs);
        await locate(tr_x, tr_y, tr_z).ChangeTurf(original_closed_turf_type, original_closed_baseturfs);
    }

    /// Tests the `EX_ACT()` macro on objs to ensure some level of the underlying framework still functions.
    // tg: code/modules/unit_tests/explosion_action.dm:172
    private async DmTask execute_obj_tests()
    {
        // we're using the abstract type here because we don't need anything stronger for this test.
        obj test_object = allocate<obj>();
        // cached integrity value for use throughout the proc.
        float cached_max_integrity = test_object.max_integrity; // Done like this to be hardy in case of changes to integrity framework (anything is possible) - we want to get the intialize-time value, not compile-time.

        test_object.update_integrity(cached_max_integrity);

        await EX_ACT(test_object, EXPLODE_NONE);
        TEST_ASSERT_EQUAL(test_object.get_integrity(), cached_max_integrity, "EX_ACT() with EXPLODE_NONE severity should not have altered the integrity of the target, but instead saw a change!");
        test_object.update_integrity(cached_max_integrity); // just here for cleanliness

        await EX_ACT(test_object, EXPLODE_LIGHT); // can do anywhere from 10 to 90 damage, let's just care if it's not equal or not
        TEST_ASSERT_NOTEQUAL(test_object.get_integrity(), cached_max_integrity, "EX_ACT() with EXPLODE_LIGHT severity should have altered the integrity of the target, but instead saw no change!");
        test_object.update_integrity(cached_max_integrity);

        await EX_ACT(test_object, EXPLODE_HEAVY); // can do anywhere from 100 to 250 damage
        TEST_ASSERT_NOTEQUAL(test_object.get_integrity(), cached_max_integrity, "EX_ACT() with EXPLODE_HEAVY severity should have altered the integrity of the target, but instead saw no change!");
        test_object.update_integrity(cached_max_integrity);

        await EX_ACT(test_object, EXPLODE_DEVASTATE); // does an INFINITE amount of damage, will trigger a qdel()
        TEST_ASSERT(QDELETED(test_object), "EX_ACT() with EXPLODE_DEVASTATE severity should have deleted the target, but instead saw no change!");
    }
}
