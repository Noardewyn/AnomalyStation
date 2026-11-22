using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// The host helper <see cref="ToyRoom"/> and its test wrapper <see cref="TgToyRoom"/> (W4.M, scope.md section 7 item 1):
/// the room is built from tg's real types in the order tg's own map code uses (<c>world.turf</c> first, the walls over the
/// rim, the per-z lists as <c>manage_z_level</c> grows them, one <c>/area/misc/testroom</c> with a null location and
/// <c>change_area</c> per turf), in a world that does not boot and as the <c>compiledMap</c> step of <c>TgWorld.Boot</c>.
/// </summary>
public class ToyRoomBuilderTests
{
    private const int W = 7, H = 6;

    private static bool Initialized(atom a) => Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1));

    private static List<turf> Listed(area a, int z) => Dm.each(a.turfs_by_zlevel[(float)z]).Cast<turf>().ToList();

    // ---- the build ----

    [Fact]
    public void TheRimIsIndestructibleWallAndTheInteriorIndestructibleFloorOnEveryLevel()
    {
        var toy = TgToyRoom.Create(W, H, levels: 2);
        toy.In(() =>
        {
            for (int z = 1; z <= 2; z++)
            {
                for (int y = 1; y <= H; y++)
                {
                    for (int x = 1; x <= W; x++)
                    {
                        bool rim = x == 1 || y == 1 || x == W || y == H;
                        turf t = locate(x, y, z);
                        Assert.Equal(rim ? typeof(turf_closed_indestructible) : typeof(turf_open_indestructible), t.GetType());
                        Assert.Equal(new float[] { x, y, z }, new[] { t.x, t.y, t.z });
                    }
                }
            }
            Assert.Equal(typeof(turf_open_indestructible), Dm.world.turf);   // R13: world.turf was the floor type before the map grew
            Assert.Equal(new float[] { W, H, 2 }, new[] { Dm.world.maxx, Dm.world.maxy, Dm.world.maxz });
        });
        Assert.Empty(toy.Runtimes);
    }

    [Fact]
    public void EveryTurfOfTheRoomWallsIncludedIsInTheOneTestroomAndTheWorldAreaHoldsNone()
    {
        var toy = TgToyRoom.Create(W, H);
        toy.In(() =>
        {
            area_misc_testroom room = toy.Room;
            Assert.Same(room, GLOB.areas_by_type[typeof(area_misc_testroom)]);
            Assert.Null(room.loc);   // an area is never inside anything: created with a null location
            foreach (turf t in Dm.each<turf>(block(1, 1, 1, W, H, 1)))
                Assert.Same(room, t.loc);
            // GLOB.areas: the world area (tg's /area/space) and the room, in creation order
            Assert.Equal(new area[] { (area)GLOB.areas_by_type[typeof(area_space)], room }, Dm.each(GLOB.areas).Cast<area>().ToArray());
            Assert.Equal(W * H, room.contents.Count);
        });
    }

    [Fact]
    public void TheAreasListsStayConsistentTheRoomListsItsTurfsInChangeAreaOrderAndNoListedTurfIsADeadReplacedOne()
    {
        var toy = TgToyRoom.Create(W, H);
        toy.In(() =>
        {
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            // build_area_turfs listed every cell for the world area, after the walls replaced the rim (R13)
            Assert.Equal(W * H, Listed(space, 1).Count);
            Assert.DoesNotContain(Listed(space, 1), t => QDELETED(t));
            // change_area queued every turf of the world area for removal, in the order it moved them (block order, x fastest)
            Assert.Equal(W * H, ((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len);
            Assert.Equal(Dm.each<turf>(block(1, 1, 1, W, H, 1)).ToList(), Listed(toy.Room, 1));

            space.cannonize_contained_turfs();   // what SSarea_contents would do
            toy.Room.cannonize_contained_turfs();
            Assert.Empty(Listed(space, 1));
            Assert.Equal(W * H, Listed(toy.Room, 1).Count);
        });
        Assert.Empty(toy.Runtimes);
    }

    [Fact]
    public void SSmappingsPerZListsAreGrownToTheWorldsMaxzAsManageZLevelDoesIt()
    {
        var toy = TgToyRoom.Create(5, 5, levels: 3);
        toy.In(() =>
        {
            Assert.Equal(3, SSmapping.gravity_by_z_level.len);
            Assert.Equal(3, SSmapping.z_level_to_plane_offset.len);
            Assert.Equal(3, SSmapping.z_level_to_lowest_plane_offset.len);
            for (int z = 1; z <= 3; z++)
            {
                Assert.True(Dm.eq(SSmapping.z_level_to_plane_offset[(float)z], 0));        // mapping.dm:767
                Assert.True(Dm.eq(SSmapping.z_level_to_lowest_plane_offset[(float)z], 0)); // mapping.dm:768
            }
            // the levels are separate: one entry per level, linked neither up nor down (ToyRoom.LinkLevels, mapping.dm:226)
            Assert.Equal(3, SSmapping.multiz_levels.len);
            Assert.False(Dm.truthy(((DmList)SSmapping.multiz_levels[1f])[(float)Z_LEVEL_UP]));
        });
    }

    [Fact]
    public void ARealObjStandingOnTheFloorHasGravityBecauseTheRoomIsStandardGravity()
    {
        var toy = TgToyRoom.Create(W, H);
        toy.In(() =>
        {
            var thing = Dm.@new<obj>(ToyRoom.FloorBottomLeft());
            Assert.True(Dm.eq(thing.has_gravity(), STANDARD_GRAVITY));
            Assert.True(Dm.eq(toy.Room.default_gravity, STANDARD_GRAVITY));
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            Assert.True(Dm.truthy(Dm.bitand(space.area_flags, NO_GRAVITY)));   // tg's world area is the one without
        });
    }

    [Fact]
    public void TheRunLocsAreTheBottomLeftAndTopRightFloorOfAnyLevel()
    {
        var toy = TgToyRoom.Create(9, 8, levels: 2);
        toy.In(() =>
        {
            Assert.Same(locate(2, 2, 1), ToyRoom.FloorBottomLeft());
            Assert.Same(locate(8, 7, 1), ToyRoom.FloorTopRight());
            Assert.Same(locate(2, 2, 2), ToyRoom.FloorBottomLeft(2));
            Assert.Same(locate(8, 7, 2), ToyRoom.FloorTopRight(2));
            Assert.IsType<turf_open_indestructible>(ToyRoom.FloorBottomLeft());
            Assert.IsType<turf_open_indestructible>(ToyRoom.FloorTopRight());
        });
    }

    [Fact]
    public void SSatomsInitializedEveryTurfAndTheRoomExactlyOnceWithNoRuntime()
    {
        var toy = TgToyRoom.Create(W, H);
        toy.In(() =>
        {
            foreach (turf t in Dm.each<turf>(block(1, 1, 1, W, H, 1)))
                Assert.True(Initialized(t), t.GetType().Name + " at " + t.x + "," + t.y);
            Assert.True(Initialized(toy.Room));
            Assert.True(Initialized((area)GLOB.areas_by_type[typeof(area_space)]));
            Assert.Equal((W - 2) * (H - 2), toy.Room.areasize);   // update_areasize counts the open turfs: the floor, not the walls
        });
        Assert.Empty(toy.Runtimes);
    }

    // ---- guards of the host helper ----

    [Fact]
    public void TheRoomMustBeTheWholeMapSoABuildInAWorldThatHasAMapRefuses()
    {
        var toy = TgToyRoom.Create(W, H);
        toy.In(() =>
        {
            Assert.Throws<InvalidOperationException>(() => ToyRoom.Build(W, H));
        });
    }

    [Theory]
    [InlineData(2, 5, 1)]
    [InlineData(5, 2, 1)]
    [InlineData(5, 5, 0)]
    public void ARoomWithoutAFloorTileOrALevelIsRefused(int w, int h, int levels)
    {
        var toy = new World(1);
        using (toy.Enter())
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ToyRoom.Build(w, h, levels));
        }
    }

    [Fact]
    public void TheSmallestRoomIsARimAroundOneFloorTile()
    {
        var toy = TgToyRoom.Create(3, 3);
        toy.In(() =>
        {
            Assert.Same(ToyRoom.FloorBottomLeft(), ToyRoom.FloorTopRight());
            Assert.IsType<turf_open_indestructible>(locate(2, 2, 1));
            Assert.Equal(8, Dm.each<turf>(block(1, 1, 1, 3, 3, 1)).Count(t => t is turf_closed_indestructible));
        });
        Assert.Empty(toy.Runtimes);
    }

    // ---- as the boot's compiledMap step ----

    [Fact]
    public void AsTheCompiledMapStepOfTheBootTheRealMastersSSatomsInitializesTheRoomOnceAtTickTwenty()
    {
        var toy = TgToyRoom.Boot(W, H, game: false);
        toy.In(() =>
        {
            foreach (turf t in Dm.each<turf>(block(1, 1, 1, W, H, 1)))
                Assert.True(Initialized(t), t.GetType().Name + " at " + t.x + "," + t.y);
            Assert.True(Initialized(toy.Room));
            Assert.Equal((W - 2) * (H - 2), toy.Room.areasize);
            Assert.Same(toy.Room, GLOB.areas_by_type[typeof(area_misc_testroom)]);
        });
        Assert.Empty(toy.Runtimes);
    }

    [Fact]
    public void TheBootedRoomHasGravityAndRunsAtTheGameRunLevel()
    {
        var toy = TgToyRoom.Boot(W, H);
        toy.In(() =>
        {
            var thing = Dm.@new<obj>(ToyRoom.FloorBottomLeft());
            Assert.True(Dm.eq(thing.has_gravity(), STANDARD_GRAVITY));
            Assert.True(Dm.eq(Master.current_runlevel, log(2, RUNLEVEL_GAME) + 1));   // SetRunLevel (master.dm:558)
        });
        Assert.Empty(toy.Runtimes);
    }

    [Fact]
    public void ARoomOfMoreThanOneHundredTurfsIsCanonizedBySSareaContentsUnderTheRealMaster()
    {
        // change_area queues every moved turf in the world area's turfs_to_uncontain_by_zlevel; past ALLOWED_LOOSE_TURFS (100)
        // SSarea_contents cleans the lists (area_contents.dm), which a 12 x 12 room (144 turfs) makes it do
        var toy = TgToyRoom.Boot(12, 12, game: false);
        toy.Step(120);
        toy.In(() =>
        {
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            Assert.True(SSarea_contents.times_fired > 0);
            Assert.Equal(0, SSarea_contents.marked_for_clearing.len);
            Assert.DoesNotContain(Dm.each(space.turfs_to_uncontain_by_zlevel).Cast<object>(), l => length(l) != 0);   // nothing is left to uncontain
            Assert.Empty(Listed(space, 1));                      // the world area lists none of the room's turfs any more
            Assert.Equal(12 * 12, Listed(toy.Room, 1).Count);    // and the room lists all of them, walls included
            Assert.Equal(10 * 10, toy.Room.areasize);
        });
        Assert.Empty(toy.Runtimes);
    }

    // ---- determinism ----

    [Fact]
    public void TheSameBuildTwiceGivesTheSameStateHashAndABiggerRoomAnotherOne()
    {
        var a = TgToyRoom.Create(W, H, seed: 5);
        var b = TgToyRoom.Create(W, H, seed: 5);
        var bigger = TgToyRoom.Create(W + 1, H, seed: 5);
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
        Assert.NotEqual(a.World.ComputeStateHash(), bigger.World.ComputeStateHash());
    }

    [Fact]
    public void TheSameBootedBuildTwiceGivesTheSameStateHashAfterTheClockRuns()
    {
        var a = TgToyRoom.Boot(W, H, seed: 8);
        var b = TgToyRoom.Boot(W, H, seed: 8);
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
        a.Step(40);
        b.Step(40);
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
    }
}
