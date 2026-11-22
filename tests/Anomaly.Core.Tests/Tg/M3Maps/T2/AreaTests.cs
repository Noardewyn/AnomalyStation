using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: the areas of a map load against tg's DM (reader.dm build_coordinate: loaded_areas, GLOB.areas_by_type; areas.dm
/// /area/New: GLOB.areas, the UNIQUE_AREA registration; mapping.dm build_area_turfs, generate_station_area_list).
/// Fixture: tests/fixtures/maps/t2/areas.dmm, 8 x 6; the hall area is in three separate groups, the escape shuttle area
/// (a non-unique area: shuttles.dm:13 `area_flags_mapping = NONE`) in two.
/// </summary>
public class AreaTests
{
    private static readonly Type Hall = typeof(area_station_hallway_primary_central);
    private static readonly Type Bridge = typeof(area_station_command_bridge);
    private static readonly Type Escape = typeof(area_shuttle_escape);

    private static IEnumerable<turf> AllTurfs()
    {
        for (int y = 1; y <= Dm.world.maxy; y++)
            for (int x = 1; x <= Dm.world.maxx; x++)
                yield return locate(x, y, 1);
    }

    [Fact]
    public void Every_area_type_of_the_map_has_one_object_even_across_separate_groups_of_tiles()
    {
        World world = T2Maps.Boot("areas", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            // the loaded_areas cache of build_coordinate: the second and third group of the hall reuse the first's object
            area hall = (area)locate(2, 5, 1).loc;
            Assert.IsType(Hall, hall);
            Assert.Same(hall, locate(3, 3, 1).loc);
            Assert.Same(hall, locate(7, 2, 1).loc);
            // the non-unique shuttle area: never in areas_by_type, but still one object per load (loaded_areas)
            area escape = (area)locate(6, 5, 1).loc;
            Assert.IsType(Escape, escape);
            Assert.Same(escape, locate(7, 4, 1).loc);
            Assert.Same(escape, locate(8, 1, 1).loc);

            var perType = GLOB.areas.Items().Cast<area>().GroupBy(a => a.GetType()).ToDictionary(g => g.Key, g => g.Count());
            Assert.Equal(1, perType[Hall]);
            Assert.Equal(1, perType[Bridge]);
            Assert.Equal(1, perType[Escape]);
            Assert.Equal(1, perType[typeof(area_space)]);
            Assert.Equal(1, perType[typeof(area_space_nearstation)]);
            Assert.All(perType, p => Assert.Equal(1, p.Value));
            // GLOB.areas lists each area object once
            Assert.Equal(GLOB.areas.len, GLOB.areas.Items().Distinct().Count());
        }
    }

    [Fact]
    public void Unique_areas_are_in_areas_by_type_and_the_worlds_default_area_is_the_one_the_space_tiles_have()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            // tg areas.dm:180: only a UNIQUE_AREA registers itself in /area/New
            Assert.Same(locate(2, 5, 1).loc, GLOB.areas_by_type[Hall]);
            Assert.Same(locate(2, 2, 1).loc, GLOB.areas_by_type[Bridge]);
            Assert.Same(locate(4, 2, 1).loc, GLOB.areas_by_type[typeof(area_space_nearstation)]);
            Assert.Null(GLOB.areas_by_type[Escape]);
            // world.area is /area/space: the tiles the space key left out keep the area BYOND made for the world
            var space = (area)GLOB.areas_by_type[Dm.world.area];
            Assert.IsType<area_space>(space);
            Assert.Same(space, locate(1, 1, 1).loc);
            Assert.Same(space, locate(8, 6, 1).loc);
        }
    }

    [Fact]
    public void Contents_and_turfs_by_zlevel_agree_with_the_cells_for_every_area()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            var byArea = AllTurfs().GroupBy(t => (area)t.loc).ToList();
            Assert.Equal(Dm.world.maxx * Dm.world.maxy, byArea.Sum(g => g.Count()));
            Assert.Equal(GLOB.areas.len, byArea.Count); // every area of GLOB.areas has at least one turf here
            foreach (var group in byArea)
            {
                area a = group.Key;
                var expected = new HashSet<turf>(group, ReferenceEqualityComparer.Instance);
                // area.contents lists exactly the turfs whose loc is the area (on the station level: the space area also holds
                // tg's transit level, M10 slice G1)
                var onStation = a.contents.Items().Where(t => ((turf)t).z == 1).ToList();
                Assert.Equal(expected.Count, onStation.Count);
                Assert.All(onStation, t => Assert.Contains((turf)t, expected));
                // the nested list of build_area_turfs (mapping.dm:775-787, run by _load_impl for a new z) lists the same turfs
                var listed = (DmList)a.turfs_by_zlevel[1f];
                Assert.Equal(expected.Count, listed.len);
                Assert.All(listed.Items(), t => Assert.Contains((turf)t, expected));
                Assert.Equal(expected.Count, listed.Items().Distinct().Count());
                Assert.True(Dm.truthy(a.has_contained_turfs()));
                // nothing is queued for removal after a load into a new z (build_coordinate skips it for new_z)
                Assert.True(Dm.length(a.turfs_to_uncontain_by_zlevel) == 0 || Dm.length(((DmList)a.turfs_to_uncontain_by_zlevel[1f])) == 0);
            }
        }
    }

    [Fact]
    public void A_cell_belongs_to_the_area_its_model_names_and_keeps_its_turf_and_movables()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            Assert.IsType<turf_open_indestructible>(locate(2, 5, 1));
            Assert.IsType<turf_closed_indestructible>(locate(3, 4, 1));
            Assert.IsType<turf_open_space_basic>(locate(4, 2, 1));
            Assert.IsType<area_space_nearstation>(locate(4, 2, 1).loc);
            Assert.IsType<obj_structure_lattice>(Assert.Single(locate(4, 2, 1).contents.Items()));
            var crowbar = Assert.IsType<obj_item_crowbar>(Assert.Single(locate(8, 1, 1).contents.Items()));
            // a movable in an area: its loc is the turf, get_area is the turf's area
            Assert.Same(locate(8, 1, 1), crowbar.loc);
            Assert.Same(locate(8, 1, 1).loc, get_area(crowbar));
            Assert.IsType(Escape, get_area(crowbar));
        }
    }

    [Fact]
    public void The_station_area_list_has_the_unique_station_areas_of_the_station_level_only()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            // tg: generate_station_area_list: unique /area/station areas on a station z; shuttles and space are not
            var list = GLOB.the_station_areas.Items().ToList();
            Assert.Equal(2, list.Count);
            Assert.Contains(Hall, list);
            Assert.Contains(Bridge, list);
            // process_teleport_locs: the teleport list holds the station-level areas that are not shuttles
            Assert.True(Dm.truthy(GLOB.teleportlocs[((area)GLOB.areas_by_type[Hall]).name]));
            Assert.True(Dm.truthy(GLOB.teleportlocs[((area)GLOB.areas_by_type[Bridge]).name]));
            Assert.False(Dm.truthy(GLOB.teleportlocs[((area)locate(6, 5, 1).loc).name]));
        }
    }

    [Fact]
    public void GLOB_areas_lists_the_worlds_area_first_and_each_map_area_in_the_order_the_load_first_met_it()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            // areas.dm:179-185 /area/New appends the area to GLOB.areas when build_coordinate makes it (reader.dm:918-925): the
            // columns left to right, each from its top row down. Column x=2 meets the hall (2,5), then the bridge (2,2); the
            // nearstation area comes at (4,2), the escape shuttle area at (6,5). /area/space is world.area: BYOND made it first.
            Assert.Equal(
                new[] { typeof(area_space), Hall, Bridge, typeof(area_space_nearstation), Escape },
                GLOB.areas.Items().Select(a => a.GetType()).ToArray());
        }
    }

    [Fact]
    public void Each_area_registers_itself_with_its_z_level_when_it_initialises()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            // areas.dm:377-390 reg_in_areas_in_z, called by /area/Initialize: SSmapping.areas_in_z["1"] gets every area that has turfs
            var in_z = Assert.IsType<DmList>(SSmapping.areas_in_z["1"]);
            Assert.Equal(GLOB.areas.Items().ToArray(), in_z.Items().ToArray());
        }
    }
}
