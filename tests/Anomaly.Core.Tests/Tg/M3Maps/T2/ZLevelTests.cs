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
/// M3 T2: z-levels, traits and the map's place in the world against tg's DM (mapping.dm LoadGroup, loadWorld,
/// add_new_zlevel, manage_z_level, calculate_z_level_gravity; space_management/{space_level,traits,zlevel_manager}.dm;
/// map_config.dm LoadConfig). Fixtures: zlevels (6 x 4, json traits), zlevelsbad (a Baseturf that is no type),
/// zlevels2 (two levels in one file).
/// </summary>
public class ZLevelTests
{
    [Fact]
    public void The_station_level_is_a_space_level_datum_with_the_json_traits_and_the_station_trait()
    {
        World world = T2Maps.Boot("zlevels", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            // D108: the station level and tg's first transit level (M10 slice G1; tg also adds ruin and empty levels)
            Assert.Equal(2f, Dm.world.maxz);
            Assert.Equal(2f, SSmapping.z_list.len);
            Assert.Equal(1f, SSmapping.station_start);
            var level = Assert.IsType<datum_space_level>(SSmapping.z_list[1f]);
            Assert.Equal("Station", level.name); // LoadGroup names the first level "Station"
            Assert.Equal(1f, level.z_value);
            Assert.Equal(CROSSLINKED, level.linkage);
            // LoadConfig adds Station = TRUE to a traits entry that lacks it (map_config.dm:166-170)
            Assert.True(Dm.eq(level.traits[ZTRAIT_STATION], TRUE));
            Assert.Equal(CROSSLINKED, level.traits[ZTRAIT_LINKAGE]);
            Assert.True(Dm.eq(level.traits["Gravity"], 1f));
            Assert.True(Dm.eq(level.traits["Bombcap Multiplier"], 2f));
            Assert.Equal("/turf/open/floor/plating", level.traits[ZTRAIT_BASETURF]);
        }
    }

    [Fact]
    public void level_trait_reads_the_traits_and_is_null_for_a_bad_z_and_empty_for_an_unmanaged_one()
    {
        World world = T2Maps.Boot("zlevels", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.True(Dm.eq(SSmapping.level_trait(1f, ZTRAIT_STATION), TRUE));
            Assert.True(Dm.eq(SSmapping.level_trait(1f, ZTRAIT_GRAVITY), 1f));
            Assert.Null(SSmapping.level_trait(1f, ZTRAIT_CENTCOM));
            Assert.Null(SSmapping.level_trait(1f, "No Such Trait"));
            // traits.dm:3-4: not a number, or below 1
            Assert.Null(SSmapping.level_trait(0f, ZTRAIT_STATION));
            Assert.Null(SSmapping.level_trait(-3f, ZTRAIT_STATION));
            Assert.Null(SSmapping.level_trait("1", ZTRAIT_STATION));
            Assert.Null(SSmapping.level_trait(null, ZTRAIT_STATION));
            // traits.dm:6-8: a z beyond z_list is a stack_trace and an empty list
            int before = log.Runtimes.Count;
            var unmanaged = Assert.IsType<DmList>(SSmapping.level_trait(3f, ZTRAIT_STATION)); // z 2 is the transit level
            Assert.Equal(0f, unmanaged.len);
            Assert.True(log.Runtimes.Count > before, "tg's stack_trace for an unmanaged z-level");
            Assert.True(Dm.truthy(SSmapping.level_trait(1f, ZTRAIT_STATION))); // is_station_level(1) is `!!level_trait(1, ZTRAIT_STATION)`
        }
    }

    [Fact]
    public void The_trait_index_and_the_lookups_by_trait_agree_with_the_level()
    {
        World world = T2Maps.Boot("zlevels");
        using (world.Enter())
        {
            // space_level.dm:12-15 z_trait_levels[trait] += list(z); traits.dm
            Assert.Equal(new object[] { 1f }, ((DmList)SSmapping.levels_by_trait(ZTRAIT_STATION)).Items().ToArray());
            Assert.Equal(new object[] { 1f }, ((DmList)SSmapping.levels_by_trait(ZTRAIT_LINKAGE)).Items().ToArray());
            Assert.Equal(0f, ((DmList)SSmapping.levels_by_trait(ZTRAIT_CENTCOM)).len);
            Assert.Equal(new object[] { 1f }, ((DmList)SSmapping.levels_by_any_trait(Dm.list(ZTRAIT_CENTCOM, ZTRAIT_STATION))).Items().ToArray());
            Assert.Equal(new object[] { 1f }, ((DmList)SSmapping.levels_by_all_traits(Dm.list(ZTRAIT_STATION, "Gravity"))).Items().ToArray());
            Assert.Equal(0f, ((DmList)SSmapping.levels_by_all_traits(Dm.list(ZTRAIT_STATION, ZTRAIT_CENTCOM))).len);
            Assert.True(Dm.truthy(SSmapping.level_has_any_trait(1f, Dm.list(ZTRAIT_STATION))));
            Assert.False(Dm.truthy(SSmapping.level_has_any_trait(1f, Dm.list(ZTRAIT_CENTCOM))));
            Assert.True(Dm.truthy(SSmapping.level_has_all_traits(1f, Dm.list(ZTRAIT_STATION, ZTRAIT_LINKAGE))));
            // traits.dm:53-55: the centre of the station z
            var center = Assert.IsAssignableFrom<turf>(SSmapping.get_station_center());
            Assert.Equal((3f, 2f, 1f), (center.x, center.y, center.z)); // 6 x 4: round(maxx * 0.5), round(maxy * 0.5)
        }
    }

    [Fact]
    public void The_levels_gravity_comes_from_its_trait_and_its_linkages_from_up_and_down()
    {
        World world = T2Maps.Boot("zlevels");
        using (world.Enter())
        {
            // mapping.dm:241-254: max_gravity = max generator setting || level_trait(z, ZTRAIT_GRAVITY) || 0 (no generator here)
            Assert.True(Dm.eq(SSmapping.gravity_by_z_level[1f], 1f));
            Assert.True(Dm.eq(SSmapping.calculate_z_level_gravity(1f), 1f));
            Assert.True(Dm.eq(SSmapping.calculate_z_level_gravity(0f), 0f)); // not a level
            // mapping.dm:225-239: multiz_levels[z] = list(up, down) of !!trait; this level has neither
            var links = (DmList)SSmapping.multiz_levels[1f];
            Assert.False(Dm.truthy(links[Z_LEVEL_UP]));
            Assert.False(Dm.truthy(links[Z_LEVEL_DOWN]));
        }
    }

    [Fact]
    public void The_baseturf_trait_text_resolves_through_ChangeTurf_of_the_bottom_marker()
    {
        World world = T2Maps.Boot("zlevels");
        using (world.Enter())
        {
            // change_turf.dm:58-67: the trait is text here (a json string); text2path makes the type
            turf floor = locate(3, 2, 1);
            var changed = Waiting.NoSleep(floor.dm_change_turf(typeof(turf_baseturf_bottom), null, null));
            Assert.IsType<turf_open_floor_plating>(changed);
        }
        World bad = T2Maps.Boot("zlevelsbad", out TgTestWorld.Log log);
        using (bad.Enter())
        {
            // an invalid baseturf: a warning and space (change_turf.dm:62-64)
            var changed = Waiting.NoSleep(locate(3, 2, 1).dm_change_turf(typeof(turf_baseturf_bottom), null, null));
            Assert.IsType<turf_open_space>(changed);
            Assert.Contains(log.Lines, l => l.Contains("invalid baseturf", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_map_smaller_than_the_world_is_centred_as_LoadGroup_computes_it()
    {
        // 6 x 4 map in a 15 x 11 world (mapping.dm:451-452): x_offset = round(maxx / 2 - map_maxx / 2) + 1 = round(4.5) + 1 = 5,
        // y_offset = round(11 / 2 - 4 / 2) + 1 = round(3.5) + 1 = 4 (DM's one-argument round is floor, rules 4.6)
        World world = T2Maps.Boot("zlevels", out TgTestWorld.Log log, compiledMap: w =>
        {
            Dm.world.maxx = 15;
            Dm.world.maxy = 11;
        });
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.Equal((15f, 11f, 2f), (Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)); // and the transit level
            // the map's wall corner (1,1) of the file is at (5,4); its top right (6,4) at (10,7)
            Assert.IsType<turf_closed_indestructible>(locate(5, 4, 1));
            Assert.IsType<turf_open_indestructible>(locate(6, 5, 1));
            Assert.IsType<turf_open_indestructible>(locate(9, 6, 1));
            Assert.IsType<turf_closed_indestructible>(locate(10, 7, 1));
            Assert.IsType<area_station_hallway_primary_central>(locate(10, 7, 1).loc);
            // outside the map: BYOND's default turf and area (world.turf in world.area), untouched by the load
            Assert.IsType<turf_open_space_basic>(locate(4, 4, 1));
            Assert.IsType<turf_open_space_basic>(locate(11, 7, 1));
            Assert.IsType<turf_open_space_basic>(locate(5, 3, 1));
            Assert.IsType<turf_open_space_basic>(locate(5, 8, 1));
            Assert.IsType<area_space>(locate(1, 1, 1).loc);
            // the station area lists cover exactly the map's tiles (build_area_turfs for the new z)
            var hall = (area)GLOB.areas_by_type[typeof(area_station_hallway_primary_central)];
            Assert.Equal(24f, hall.contents.len);
            Assert.Equal(24f, ((DmList)hall.turfs_by_zlevel[1f]).len);
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            Assert.Equal(15 * 11 - 24f, ((DmList)space.turfs_by_zlevel[1f]).len);
        }
    }

    [Fact]
    public void A_two_level_map_file_makes_two_station_levels_with_the_up_and_down_autosetup()
    {
        World world = T2Maps.Boot("zlevels2", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Equal((4f, 3f, 3f), (Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)); // and the transit level
            Assert.Equal(3f, SSmapping.z_list.len);
            var low = Assert.IsType<datum_space_level>(SSmapping.z_list[1f]);
            var high = Assert.IsType<datum_space_level>(SSmapping.z_list[2f]);
            // LoadGroup names a further level "<name> <n>" (mapping.dm:439), and the autosetup sets Up on the bottom level and
            // Down on the top one (mapping.dm:427-435, height_autosetup defaults to TRUE)
            Assert.Equal("Station", low.name);
            Assert.Equal("Station 2", high.name);
            Assert.True(Dm.truthy(low.traits[ZTRAIT_UP]));
            Assert.False(Dm.truthy(low.traits[ZTRAIT_DOWN]));
            Assert.True(Dm.truthy(high.traits[ZTRAIT_DOWN]));
            Assert.False(Dm.truthy(high.traits[ZTRAIT_UP]));
            // both default to ZTRAITS_STATION (a copy each: mapping.dm:421-422 default_traits.Copy())
            Assert.NotSame(low.traits, high.traits);
            Assert.Equal(CROSSLINKED, low.traits[ZTRAIT_LINKAGE]);
            Assert.True(Dm.truthy(SSmapping.level_trait(2f, ZTRAIT_STATION)));
            Assert.Equal(new object[] { 1f, 2f }, ((DmList)SSmapping.levels_by_trait(ZTRAIT_STATION)).Items().ToArray());
            // the links
            Assert.True(Dm.truthy(((DmList)SSmapping.multiz_levels[1f])[Z_LEVEL_UP]));
            Assert.True(Dm.truthy(((DmList)SSmapping.multiz_levels[2f])[Z_LEVEL_DOWN]));
            // the tiles of each file level land on their own z, and the unlisted tiles of the second are its space
            Assert.IsType<turf_closed_indestructible>(locate(1, 1, 1));
            Assert.IsType<turf_open_indestructible>(locate(1, 1, 2));
            Assert.IsType<turf_open_space_basic>(locate(2, 2, 2));
            Assert.DoesNotContain(log.Runtimes, r => !r.Message.Contains("update_plane_tracking", StringComparison.Ordinal));
        }
    }
}
