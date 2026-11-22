using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>M3 P2: ChangeTurf and ScrapeAway (change_turf.dm, baseturfs.dm) on the loaded fixture station.</summary>
public class ChangeTurfTests
{
    // tg: code/modules/unit_tests/baseturfs.dm (maptest_baseturfs_unmodified_scrape): an iron floor scrapes away to plating,
    // plating to space, space to space. (The place_on_top and place_on_bottom cases need baseturfs.dm's stack procs, M5.)
    [Fact]
    public void Ported_maptest_baseturfs_unmodified_scrape()
    {
        World world = MapLoadTests.BootFixture(out TgTestWorld.Log log);
        using (world.Enter())
        {
            turf run_loc = locate(5, 3, 1);
            var area_before = (area)run_loc.loc;
            // RESET_TO_EXPECTED: ChangeTurf(/turf/open/floor) and assemble_baseturfs(initial(baseturfs))
            var floor = (turf)Waiting.NoSleep(run_loc.ChangeTurf(typeof(turf_open_floor_iron)));
            Waiting.NoSleep(floor.assemble_baseturfs(Dm.initial(floor, "baseturfs")));
            Assert.IsType<turf_open_floor_iron>(locate(5, 3, 1));
            Assert.Same(area_before, locate(5, 3, 1).loc); // the area keeps its place for the new turf

            Waiting.NoSleep(locate(5, 3, 1).ScrapeAway());
            Assert.IsType<turf_open_floor_plating>(locate(5, 3, 1));

            Waiting.NoSleep(locate(5, 3, 1).ScrapeAway());
            Assert.IsType<turf_open_space>(locate(5, 3, 1));

            Waiting.NoSleep(locate(5, 3, 1).ScrapeAway());
            Assert.IsType<turf_open_space>(locate(5, 3, 1));
            Assert.Same(area_before, locate(5, 3, 1).loc);
        }
    }

    [Fact]
    public void ChangeTurf_keeps_the_movables_and_returns_the_turf_itself_for_a_no_op()
    {
        World world = MapLoadTests.BootFixture(out _);
        using (world.Enter())
        {
            turf t = locate(4, 3, 1);
            object crowbar = Assert.Single(t.contents.Items());
            Assert.Same(t, Waiting.NoSleep(t.dm_change_turf(t.type, t.baseturfs, null))); // same type, same baseturfs: a no-op
            var plating = (turf)Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_plating)));
            Assert.NotSame(t, plating);
            Assert.Same(plating, locate(4, 3, 1));
            Assert.Same(crowbar, Assert.Single(plating.contents.Items()));
            Assert.Equal(0f, t.x); // the replaced turf left the map (TURF-IDENTITY: its reference is the old object)
            Assert.Null(Waiting.NoSleep(plating.ChangeTurf(null)));
        }
    }
}
