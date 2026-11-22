using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>
/// M3 P2: the station path on a small converted map (tests/fixtures/maps/p2load, from p2_load.dmm): MapSeam.Select mounts
/// it, TgWorld.CompiledMap sizes the world, SSmapping's PreInit reads its config, Initialize loads it through
/// LoadGroup, parsed_map and build_coordinate, and SSatoms initializes what it made.
/// </summary>
public class MapLoadTests
{
    internal static World BootFixture(out TgTestWorld.Log log, ulong seed = 5, string name = "p2load")
    {
        string folder = Path.Combine(MapFixtures.FixtureFolder(), name);
        return TgTestWorld.Boot(out log, seed, configure: w => MapSeam.Select(w, new MapSelection(folder, name)), compiledMap: TgWorld.CompiledMap);
    }

    [Fact]
    public void The_station_level_loads_with_its_turfs_areas_and_landmarks()
    {
        World world = BootFixture(out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.Equal((7f, 6f, 2f), (Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)); // the station and tg's first transit level (M10 slice G1)
            Assert.Equal("P2 Load", SSmapping.current_map.map_name);
            Assert.Equal(0f, SSmapping.current_map.defaulted);
            Assert.Equal(2f, SSmapping.z_list.len);
            var level = (datum_space_level)SSmapping.z_list[1f];
            Assert.Equal("Station", level.name);
            Assert.Equal(Dm.TRUE, SSmapping.level_trait(1f, ZTRAIT_STATION));
            Assert.Equal(CROSSLINKED, SSmapping.level_trait(1f, ZTRAIT_LINKAGE));
            Assert.Equal(1f, SSmapping.station_start);

            // the turfs and areas of the model lines; the space tiles kept the world's turf (the space key skip)
            Assert.IsType<turf_open_space_basic>(locate(1, 1, 1));
            Assert.IsType<turf_closed_indestructible>(locate(2, 2, 1));
            Assert.IsType<turf_open_indestructible>(locate(3, 3, 1));
            var hall = (area)locate(3, 3, 1).loc;
            Assert.IsType<area_station_hallway_primary_central>(hall);
            Assert.Same(hall, GLOB.areas_by_type[typeof(area_station_hallway_primary_central)]);
            Assert.IsType<area_space_nearstation>(locate(2, 6, 1).loc);
            Assert.IsType<area_space>(locate(1, 1, 1).loc);
            Assert.Equal(20, hall.contents.Count); // 5 x 4 turfs
            Assert.Equal(20f, ((DmList)hall.turfs_by_zlevel[1f]).len);
            Assert.Contains(typeof(area_station_hallway_primary_central), GLOB.the_station_areas.Items());

            // the movables, in member order on their turfs
            Assert.IsType<obj_structure_lattice>(Assert.Single(locate(2, 6, 1).contents.Items()));
            var crowbar = (obj_item_crowbar)Assert.Single(locate(4, 3, 1).contents.Items());
            Assert.Equal(-4f, crowbar.pixel_x);
            Assert.Equal("Bob's bar", DmTextRender.StripMarkers(crowbar.name));
            Assert.Equal(DmTextRender.ImproperMarker, crowbar.name[0]);

            // the landmarks: the start landmarks stay, the late-join ones qdel themselves after adding their turf
            Assert.Equal(2f, GLOB.start_landmarks_list.len);
            Assert.All(GLOB.start_landmarks_list.Items(), l => Assert.IsType<obj_effect_landmark_start_assistant>(l));
            Assert.Empty(locate(3, 3, 1).contents.Items().OfType<obj_effect_landmark_latejoin>());
            Assert.Equal(new[] { locate(3, 3, 1), locate(5, 4, 1) }, SSjob.latejoin_trackers.Items().ToArray());
        }
        Assert.Equal(new[] { new SpawnPoint(3, 3, 1), new SpawnPoint(5, 4, 1) }, MapSeam.LatejoinSpawns(world));
    }
}
