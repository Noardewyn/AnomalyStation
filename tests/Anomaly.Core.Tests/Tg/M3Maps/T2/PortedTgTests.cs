using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: tg's map unit tests (code/modules/unit_tests) on the hand-written fixture maps, each with a positive control (a
/// map that must pass) and a negative control (a map or a corruption the test must catch), so that a check that cannot
/// fail is noticed. The same checks run on converted Meta in MetaChecksTests. P2's earlier ports are in
/// Tg/M3Maps/P2/MetaLoadTests.cs and stay there (duplicates: area_contents, mapload_space_verification, mapping,
/// the map_landmarks subset, the baseturfs unmodified scrape; this folder's versions are the fuller ones).
/// </summary>
public class PortedTgTests
{
    // ---- maptest_area_contents

    [Theory]
    [InlineData("turfs")]
    [InlineData("areas")]
    [InlineData("zlevels")]
    [InlineData("zlevels2")]
    [InlineData("landmarks")]
    [InlineData("nearstation")]
    public void Ported_maptest_area_contents_passes_on_a_loaded_fixture(string fixture)
    {
        World world = T2Maps.Boot(fixture);
        using (world.Enter())
            Assert.Empty(TgMapChecks.AreaContents());
    }

    [Fact]
    public void Ported_maptest_area_contents_catches_a_shared_a_duplicate_a_missing_and_a_foreign_entry()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            var hall = (area)GLOB.areas_by_type[typeof(area_station_hallway_primary_central)];
            var bridge = (area)GLOB.areas_by_type[typeof(area_station_command_bridge)];
            var hall_turfs = (DmList)hall.turfs_by_zlevel[1f];
            var bridge_turfs = (DmList)bridge.turfs_by_zlevel[1f];
            object a_bridge_turf = bridge_turfs[1];
            object a_hall_turf = hall_turfs[1];
            hall_turfs.Add(a_bridge_turf);            // listed in two areas, and registered in neither of the listings' area
            hall_turfs.Add(a_hall_turf);              // listed twice in its own area
            bridge_turfs.Remove(bridge_turfs[2]);     // another bridge turf is missing from every listing
            hall_turfs.Add("not a turf");             // not a turf at all
            List<string> fails = TgMapChecks.AreaContents();
            Assert.Contains(fails, f => f.StartsWith("Found a duplicate turf", StringComparison.Ordinal));
            Assert.Contains(fails, f => f.StartsWith("Found a turf", StringComparison.Ordinal) && f.Contains("which is IN", StringComparison.Ordinal));
            Assert.Contains(fails, f => f.Contains("NOT stored in any area's turf listing", StringComparison.Ordinal));
            Assert.Contains(fails, f => f.StartsWith("Found a not a turf", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Ported_maptest_area_contents_catches_a_turf_in_the_wrong_area_after_a_runtime_area_change()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            // move a turf's loc without touching the lists, as an engine bug would
            turf t = locate(2, 5, 1);
            var bridge = (area)GLOB.areas_by_type[typeof(area_station_command_bridge)];
            bridge.contents.Add(t);
            Assert.Same(bridge, t.loc);
            List<string> fails = TgMapChecks.AreaContents();
            Assert.Contains(fails, f => f.Contains("which is IN /area/station/command/bridge, but is registered as being in /area/station/hallway/primary/central", StringComparison.Ordinal));
        }
    }

    // ---- maptest_mapload_space_verification

    [Theory]
    [InlineData("areas")]
    [InlineData("zlevels")]
    [InlineData("zlevels2")]
    [InlineData("landmarks")]
    [InlineData("init")]
    public void Ported_maptest_mapload_space_verification_passes_on_a_clean_fixture(string fixture)
    {
        World world = T2Maps.Boot(fixture);
        using (world.Enter())
            Assert.Empty(TgMapChecks.MaploadSpaceVerification());
    }

    [Fact]
    public void Ported_maptest_mapload_space_verification_reports_a_space_turf_in_a_station_area()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.MaploadSpaceVerification();
            string fail = Assert.Single(fails);
            Assert.Contains("Space turf /turf/open/space/basic found in non-allowed area (/area/station/hallway/primary/central)", fail, StringComparison.Ordinal);
            Assert.Contains("(6,4,1)", fail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ported_maptest_mapload_space_verification_takes_the_planetary_branch_when_the_map_is_planetary()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            Assert.False(Dm.truthy(SSmapping.is_planetary())); // map_config.dm:`planetary = FALSE`
            SSmapping.current_map.planetary = TRUE;
            Assert.True(Dm.truthy(SSmapping.is_planetary()));
            // any space turf of a station level fails, in /area/space too (the space the load left in the corners)
            List<string> fails = TgMapChecks.MaploadSpaceVerification();
            Assert.NotEmpty(fails);
            Assert.All(fails, f => Assert.StartsWith("Space turf found on planetary map!", f, StringComparison.Ordinal));
            Assert.Contains(fails, f => f.Contains("(1,1,1)", StringComparison.Ordinal));
            Assert.Contains(fails, f => f.Contains("(6,4,1)", StringComparison.Ordinal));
        }
    }

    // ---- maptest_mapping_nearstation_test

    [Theory]
    [InlineData("areas")]
    [InlineData("turfs")]
    [InlineData("zlevels")]
    public void Ported_maptest_mapping_nearstation_passes_on_a_clean_fixture(string fixture)
    {
        World world = T2Maps.Boot(fixture);
        using (world.Enter())
            Assert.Empty(TgMapChecks.MappingNearstation());
    }

    [Fact]
    public void Ported_maptest_mapping_nearstation_reports_a_movable_in_space_and_a_floor_without_lighting()
    {
        World world = T2Maps.Boot("nearstation");
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.MappingNearstation();
            // (1,1): a crowbar in /area/space; (2,1): a plating turf in /area/space with nothing on it. The lattice in
            // /area/space/nearstation (lit), the landmark (a safe type) and the station floor are fine.
            Assert.Equal(new[]
            {
                "1, 1, 1: /obj/item/crowbar with area of type /area/space",
                "2, 1, 1: /turf/open/floor/plating with area of type /area/space",
            }, fails.ToArray());
        }
    }

    // ---- maptest_log_mapping

    [Fact]
    public void Ported_maptest_log_mapping_only_counts_messages_with_coordinates_on_a_station_level()
    {
        World world = T2Maps.Boot("areas", out TgTestWorld.Log log);
        using (world.Enter())
        {
            // the load itself leaves nothing (InitializeDefaultZLevels' "More or less map attributes pre-defined" line, which
            // D108 causes, has no coordinates and so is not a failure in tg either)
            Assert.Empty(TgMapChecks.MappingLogs(log.Lines));
            Assert.Contains(log.Lines, l => l.Contains("map attributes pre-defined", StringComparison.Ordinal));

            // positive control: a message with an AREACOORD on the station z is caught, one without is not, a z that is no
            // station or mining level (0) is not
            log_mapping("Mapper error at Hallway (3,3,1)");
            log_mapping("Mapper note without coordinates");
            log_mapping("Out of the station (3,3,0)");
            string[] fails = TgMapChecks.MappingLogs(log.Lines).ToArray();
            Assert.Equal("Mapper error at Hallway (3,3,1)", Assert.Single(fails));
        }
    }

    // ---- maptest_baseturfs_* (baseturfs.dm)

    private static object Reset_to_expected(turf t)
    {
        // RESET_TO_EXPECTED: ChangeTurf(EXPECTED_FLOOR_TYPE); assemble_baseturfs(initial(baseturfs)), so the stack is the on-init one
        var floor = (turf)Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor)));
        Waiting.NoSleep(floor.assemble_baseturfs(Dm.initial(floor, "baseturfs")));
        return floor;
    }

    // tg: baseturfs.dm maptest_baseturfs_unmodified_scrape. P2's port used an iron floor; tg's EXPECTED_FLOOR_TYPE is /turf/open/floor.
    [Fact]
    public void Ported_maptest_baseturfs_unmodified_scrape()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf run_loc_floor_bottom_left = locate(2, 4, 1);
            var area_before = run_loc_floor_bottom_left.loc;
            Reset_to_expected(run_loc_floor_bottom_left);
            run_loc_floor_bottom_left = locate(2, 4, 1);
            Assert.Equal(typeof(turf_open_floor), run_loc_floor_bottom_left.GetType());

            Waiting.NoSleep(run_loc_floor_bottom_left.ScrapeAway());
            Assert.Equal(typeof(turf_open_floor_plating), locate(2, 4, 1).GetType()); // Iron floors should scrape away to plating

            Waiting.NoSleep(locate(2, 4, 1).ScrapeAway());
            Assert.Equal(typeof(turf_open_space), locate(2, 4, 1).GetType()); // Plating should scrape away to space

            Waiting.NoSleep(locate(2, 4, 1).ScrapeAway());
            Assert.Equal(typeof(turf_open_space), locate(2, 4, 1).GetType()); // Space should scrape away to space
            Assert.Same(area_before, locate(2, 4, 1).loc);
        }
    }

    [Fact(Skip = "NOT PORTED YET: /turf/proc/place_on_top (baseturfs.dm:30-40) is not translated (baseturfs.dm: everything but ScrapeAway is M5, construction and destruction); tg's maptest_baseturfs_placed_on_top: place_on_top(/turf/closed/wall/rock) makes a rock wall, ScrapeAway returns the expected floor type")]
    public void Ported_maptest_baseturfs_placed_on_top()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf t = (turf)Reset_to_expected(locate(2, 4, 1));
            Waiting.NoSleep(Dm.Call(t, "place_on_top", typeof(turf_closed_wall_rock)));
            Assert.Equal(typeof(turf_closed_wall_rock), locate(2, 4, 1).GetType());
            Waiting.NoSleep(locate(2, 4, 1).ScrapeAway());
            Assert.Equal(typeof(turf_open_floor), locate(2, 4, 1).GetType());
        }
    }

    [Fact(Skip = "NOT PORTED YET: /turf/proc/place_on_bottom (baseturfs.dm:24-28) is not translated (M5); tg's maptest_baseturfs_placed_on_bottom: place_on_bottom(rock wall) leaves the floor, scrapes iron -> plating -> space -> rock wall -> plating")]
    public void Ported_maptest_baseturfs_placed_on_bottom()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf t = (turf)Reset_to_expected(locate(2, 4, 1));
            Waiting.NoSleep(Dm.Call(t, "place_on_bottom", typeof(turf_closed_wall_rock)));
            Assert.Equal(typeof(turf_open_floor), locate(2, 4, 1).GetType());
            foreach (Type expected in new[] { typeof(turf_open_floor_plating), typeof(turf_open_space), typeof(turf_closed_wall_rock), typeof(turf_open_floor_plating) })
            {
                Waiting.NoSleep(locate(2, 4, 1).ScrapeAway());
                Assert.Equal(expected, locate(2, 4, 1).GetType());
            }
        }
    }
}
