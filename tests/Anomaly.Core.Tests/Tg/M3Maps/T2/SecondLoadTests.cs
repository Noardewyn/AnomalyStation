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
/// M3 T2: a second load of a map into the loaded world (tg's load_map with no_changeturf and new_z off, the way map templates
/// and ruins load, reader.dm:151-174, :326-372, build_coordinate :892-982): the area cache across loads, ChangeTurf per
/// tile, the area lists' old-area bookkeeping. The station path (new_z, no_changeturf) never takes these branches. Fixture:
/// areas (8 x 6) loaded as the station in a 24 x 12 world (offset 9,4), then again at (17,1).
/// </summary>
public class SecondLoadTests
{
    private const string MapFile = "_maps/map_files/T2Areas/T2Areas.dmm"; // where MapSeam.Select mounted it

    private static World BootBigWorld(out TgTestWorld.Log log) =>
        T2Maps.Boot("areas", out log, compiledMap: w =>
        {
            Dm.world.maxx = 24;
            Dm.world.maxy = 12;
        });

    // Loads the fixture again at (x_offset, y_offset) like a template: ChangeTurf per tile, then the new atoms are initialised
    // (map_template.dm initTemplateBounds does the same with SSatoms.InitializeAtoms).
    private static datum_parsed_map LoadAgain(float x_offset, float y_offset)
    {
        var pm = (datum_parsed_map)Waiting.NoSleep(load_map(Dm.file(MapFile), x_offset, y_offset, 1));
        var atoms = Dm.list();
        for (float y = y_offset; y < y_offset + 6; y++)
        {
            for (float x = x_offset; x < x_offset + 8; x++)
            {
                turf t = locate(x, y, 1);
                atoms.Add(t);
                foreach (object o in t.contents.Items()) atoms.Add(o);
            }
        }
        SSatoms.InitializeAtoms(atoms);
        return pm;
    }

    [Fact]
    public void A_second_load_reuses_the_unique_areas_and_makes_a_new_object_of_each_non_unique_one()
    {
        World world = BootBigWorld(out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            var hall = (area)GLOB.areas_by_type[typeof(area_station_hallway_primary_central)];
            var first_escape = (area)locate(14, 8, 1).loc; // the first load's (6,5) at offset (9,4)
            Assert.IsType<area_shuttle_escape>(first_escape);

            LoadAgain(17, 1);
            Assert.DoesNotContain(log.Runtimes, r => r is not DmNotTranslatedException);

            // the unique areas: tg's `GLOB.areas_by_type[area_type]` (reader.dm:915-917) gives the existing object
            Assert.Same(hall, locate(17 + 1, 1 + 4, 1).loc);   // the file's (2,5)
            Assert.Same(hall, GLOB.areas_by_type[typeof(area_station_hallway_primary_central)]);
            // the non-unique shuttle area is in no areas_by_type, so the second load's loaded_areas makes its own object (one for the load)
            var second_escape = (area)locate(17 + 5, 1 + 4, 1).loc; // the file's (6,5)
            Assert.IsType<area_shuttle_escape>(second_escape);
            Assert.NotSame(first_escape, second_escape);
            Assert.Same(second_escape, locate(17 + 6, 1 + 3, 1).loc);
            Assert.Same(second_escape, locate(17 + 7, 1, 1).loc);
            Assert.Equal(2, GLOB.areas.Items().Count(a => a is area_shuttle_escape));
            Assert.Equal(1, GLOB.areas.Items().Count(a => a is area_station_hallway_primary_central));
        }
    }

    [Fact]
    public void The_second_load_changes_the_turfs_through_ChangeTurf_so_space_basic_becomes_space()
    {
        World world = BootBigWorld(out _);
        using (world.Enter())
        {
            // first load: the space key tiles keep the world's /turf/open/space/basic
            Assert.IsType<turf_open_space_basic>(locate(9, 4, 1));
            LoadAgain(17, 1);
            // second load: no space key (copy() builds the model cache with no_changeturf unset), so every tile is built and a
            // /turf/open/space/basic model becomes /turf/open/space through ChangeTurf (change_turf.dm:66-68)
            Assert.IsType<turf_open_space>(locate(17, 1, 1));
            Assert.IsType<turf_open_indestructible>(locate(17 + 1, 1 + 4, 1));
            Assert.IsType<turf_closed_indestructible>(locate(17 + 2, 1 + 3, 1));
            Assert.IsType<obj_item_crowbar>(Assert.Single(locate(17 + 7, 1, 1).contents.Items()));
            Assert.IsType<obj_structure_lattice>(Assert.Single(locate(17 + 3, 1 + 1, 1).contents.Items()));
            // and the loaded atoms are initialised
            Assert.True(Dm.truthy(Dm.bitand(locate(17 + 7, 1, 1).flags_1, INITIALIZED_1)));
        }
    }

    [Fact]
    public void After_a_second_load_the_area_turf_lists_agree_with_the_cells_again()
    {
        World world = BootBigWorld(out _);
        using (world.Enter())
        {
            Assert.Empty(TgMapChecks.AreaContents());
            LoadAgain(17, 1);
            // tg: build_coordinate files each loaded turf in its new area's turfs_by_zlevel and the old area's
            // turfs_to_uncontain_by_zlevel (reader.dm:928-932); the turf the lists hold is the cell's, which ChangeTurf replaced
            // (BYOND's reference follows the location)
            List<string> fails = TgMapChecks.AreaContents();
            Assert.True(fails.Count == 0, string.Join("\n", fails.Take(10)));
        }
    }

    [Fact]
    public void The_bounds_are_the_loaded_rectangle_and_measure_only_loads_nothing()
    {
        World world = BootBigWorld(out _);
        using (world.Enter())
        {
            // reader.dm:151-174: measure_only parses and returns the map with its unoffset bounds, no tile is touched
            var measured = (datum_parsed_map)Waiting.NoSleep(load_map(Dm.file(MapFile), 17, 1, 1, measure_only: TRUE));
            Assert.Equal(new object[] { 1f, 1f, 1f, 8f, 6f, 1f }, measured.bounds.Items().ToArray().Select(v => (object)Dm.num(v)).ToArray());
            Assert.IsType<turf_open_space_basic>(locate(17, 1, 1));

            // a load: _tgm_load's bounds (min x, min y, min z, max x, max y, max z) in world coordinates
            var loaded = LoadAgain(17, 1);
            Assert.Equal(new[] { 17f, 1f, 1f, 24f, 6f, 1f }, loaded.bounds.Items().Select(v => Dm.num(v)).ToArray());
        }
    }

    [Fact]
    public void crop_map_loads_only_what_fits_and_a_load_without_it_grows_the_world()
    {
        World world = BootBigWorld(out TgTestWorld.Log log);
        using (world.Enter())
        {
            // reader.dm:392-405 and :415-436: crop_map keeps maxx; the map's x 1..4 land on 21..24
            var cropped = (datum_parsed_map)Waiting.NoSleep(load_map(Dm.file(MapFile), 21, 1, 1, crop_map: TRUE));
            Assert.Equal(24f, Dm.world.maxx);
            Assert.Equal(new[] { 21f, 1f, 1f, 24f, 6f, 1f }, cropped.bounds.Items().Select(v => Dm.num(v)).ToArray());
            Assert.IsType<turf_open_indestructible>(locate(22, 5, 1)); // the file's (2,5)
            Assert.IsType<turf_closed_indestructible>(locate(23, 4, 1)); // the file's (3,4)
            Assert.False(Dm.truthy(cropped.expanded_x));
        }
        World grown = BootBigWorld(out _);
        using (grown.Enter())
        {
            // without crop_map the world grows (increase_max_x, game/world.dm:458), filled with the default turf and area
            var loaded = (datum_parsed_map)Waiting.NoSleep(load_map(Dm.file(MapFile), 21, 1, 1));
            Assert.Equal(28f, Dm.world.maxx);
            Assert.True(Dm.truthy(loaded.expanded_x));
            Assert.IsType<turf_open_indestructible>(locate(21 + 1, 5, 1));
            Assert.IsType<turf_closed_indestructible>(locate(21 + 2, 4, 1));
            Assert.IsType<area_space>(locate(28, 12, 1).loc);
        }
    }

    // tg's bounds are not symmetric (reader.dm:411-437, :478-492): y_lower is in the file's rows (y_ending_skip skips rows below it) while
    // x_upper is compared with final_x, which already has the offset in it (an absolute world x), so with the map at x 17..24
    // x_upper = 19 keeps the file's columns 1 to 3.
    [Fact]
    public void x_upper_and_y_lower_cut_the_map_the_way_tgs_loader_reads_them()
    {
        World world = BootBigWorld(out _);
        using (world.Enter())
        {
            Waiting.NoSleep(load_map(Dm.file(MapFile), 17, 1, 1, x_upper: 19, y_lower: 4));
            Assert.IsType<turf_open_indestructible>(locate(18, 5, 1));       // the file's (2,5): inside
            Assert.IsType<turf_closed_indestructible>(locate(19, 4, 1));     // the file's (3,4): inside
            Assert.IsType<turf_open_space_basic>(locate(20, 4, 1));          // the file's (4,4), a wall: past x_upper, left alone
            Assert.IsType<turf_open_space_basic>(locate(18, 2, 1));          // the file's (2,2), the bridge floor: below y_lower, left alone
            // x_upper = 3 compared as an absolute x loads nothing at this offset
            World none = BootBigWorld(out _);
            using (none.Enter())
            {
                Waiting.NoSleep(load_map(Dm.file(MapFile), 17, 1, 1, x_upper: 3));
                Assert.IsType<turf_open_space_basic>(locate(18, 5, 1));
            }
        }
    }
}
