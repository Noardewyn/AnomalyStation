// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/area_contents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// Verifies that an area's perception of their "turfs" is correct, and no other area overlaps with them
/// Quite slow, but needed
// tg: code/modules/unit_tests/area_contents.dm:3 (/datum/unit_test/maptest_area_contents; class named by rules 2.3)
//
// ADAPTED (wave 4, package B): tg runs this map test on a loaded station map. Until the map loader exists (M3), New()
// builds a small map the way tg's world and SSmapping leave one: the compiled map's size in world.turf floors inside the
// world.area instance, the turfs registered with that area as SSmapping's build_area_turfs does for a z level filled with
// default tiles (controllers/subsystem/mapping.dm:776-781), the map grown with tg's increase_max_x and increase_max_y
// (game/world.dm:458-490), and a second area that takes some turfs through tg's /turf/proc/change_area, as the map reader
// does (modules/mapping/reader.dm:929-937). Then a turf moves back and forth between the two areas, so that the test
// also sees turfs_to_uncontain_by_zlevel being canonized.
public sealed class datum_unit_test_maptest_area_contents : TgUnitTest
{
    // tg: code/modules/unit_tests/area_contents.dm:4-5
    public datum_unit_test_maptest_area_contents()
    {
        test_flags = UNIT_TEST_MAP_TEST;
        priority = TEST_LONGER;
    }

    public override void New()
    {
        base.New();
        // ADAPTED: the map (see above).
        MapGrid.Attach(World.Current);
        Dm.@new<datum_controller_subsystem_mapping>();
        Dm.world.turf = typeof(turf_open_indestructible);
        Dm.world.area = typeof(area_space);
        Dm.world.maxx = 2;
        Dm.world.maxy = 2;
        Dm.world.maxz = 1;
        // SSmapping's build_area_turfs(1, space_guaranteed = TRUE)
        area global_area = GLOB.areas_by_type[Dm.world.area] as area;
        global_area.turfs_by_zlevel.len = 1;
        global_area.turfs_by_zlevel[1f] = block(1, 1, 1, Dm.world.maxx, Dm.world.maxy, 1);
        Dm.world.increase_max_x(4);
        Dm.world.increase_max_y(3);

        area room = Dm.@new<area_misc_testroom>();
        foreach (turf T in Dm.each_cast<turf>(block(2, 2, 1, 3, 3, 1)))
            T.change_area(T.loc as area, room);
        turf back_and_forth = locate(4, 3, 1);
        back_and_forth.change_area(global_area, room);
        back_and_forth.change_area(room, global_area);
    }

    // tg: code/modules/unit_tests/area_contents.dm:7
    public override async DmTask Run()
    {
        // ADAPTED: tg's /turf/var/in_contents_of exists only with UNIT_TESTS (game/turfs/turf.dm:98-102), which the port
        // never compiles (rules 1.7); the test keeps the same record in its own dictionary.
        var in_contents_of = new Dictionary<turf, area>();

        // First, we check that there are no entries in more then one area
        // That or duplicate entries
        foreach (area area_to_test in Dm.each<area>(GLOB.areas))
        {
            area_to_test.cannonize_contained_turfs();
            for (float i = 1, i__end = area_to_test.turfs_by_zlevel.len; i <= i__end; i++)
            {
                // ADAPTED: the failure messages leave out SSmapping.get_level(i)?.name (z levels are M3) and write the
                // coordinates as (x, y, z) instead of COORD() (admin coordinates, not translated).
                if (!islist(area_to_test.turfs_by_zlevel[i]))
                    TEST_FAIL(Dm.text("zlevel index [] in []'s turf listing is not a list.", i, area_to_test.type));

                foreach (turf turf_to_check in Dm.each_cast<turf>(area_to_test.turfs_by_zlevel[i]))
                {
                    if (!isturf(turf_to_check))
                        TEST_FAIL(Dm.text("Found a [] in []'s turf listing", ((datum)turf_to_check).type, area_to_test.type));

                    if (in_contents_of.TryGetValue(turf_to_check, out area existing))
                    {
                        if (existing == turf_to_check.loc)
                            TEST_FAIL(Dm.text("Found a duplicate turf [] ([], [], []) inside []'s turf listing", turf_to_check.type, turf_to_check.x, turf_to_check.y, turf_to_check.z, area_to_test.type));
                        else
                            TEST_FAIL(Dm.text("Found a shared turf [] ([], [], []) between [] and []'s turf listings", turf_to_check.type, turf_to_check.x, turf_to_check.y, turf_to_check.z, area_to_test.type, existing.type));
                    }

                    area turfs_actual_area = turf_to_check.loc as area;
                    if (turfs_actual_area != area_to_test)
                        TEST_FAIL(Dm.text("Found a turf [] ([], [], []) which is IN [], but is registered as being in []", turf_to_check.type, turf_to_check.x, turf_to_check.y, turf_to_check.z, turfs_actual_area?.type, area_to_test.type));

                    in_contents_of[turf_to_check] = turfs_actual_area;
                }
            }
        }

        // ALL_TURFS() (__DEFINES/turfs.dm:29)
        foreach (turf position in Dm.each<turf>(block(1, 1, 1, Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)))
        {
            if (!in_contents_of.ContainsKey(position))
                TEST_FAIL(Dm.text("Found a turf [] ([], [], []) inside [] that is NOT stored in any area's turf listing", position.type, position.x, position.y, position.z, position.loc?.type));
        }

        // ADAPTED (port check): the map above has every turf of the 4 x 3 map in a listing, and the room holds the four
        // turfs of its block (the turf that came back is in the global area again).
        TEST_ASSERT(in_contents_of.Count == 12, Dm.text("expected 12 listed turfs, found []", in_contents_of.Count));
        TEST_ASSERT(in_contents_of.Values.Count(a => a is area_misc_testroom) == 4, "the testroom should list the four turfs of its block");
    }
}
