using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// The toy room build of scope.md section 7 item 1 ("map without the loader"), the way wave 5's toy builder does it: grow
/// world.maxx/maxy/maxz (the grid creates world.turf turfs in world.area, x fastest), replace cells with
/// <c>new /turf/open/indestructible(locate(...))</c> and <c>/turf/closed/indestructible</c>, make a
/// <c>/area/misc/testroom</c> with a null location and move turfs into it with tg's own <c>change_area</c> (what the map
/// reader does, reader.dm:929-937), then initialize through SSatoms. GLOB.areas, GLOB.areas_by_type and the areas'
/// turfs_by_zlevel lists must come out consistent, and the whole build must be deterministic.
/// </summary>
public class ToyRoomTests
{
    private const int W = 5, H = 4, D = 2;

    private sealed class Toy
    {
        public TurfAreaWorld T;
        public area Space;
        public area_misc_testroom Room;
    }

    // The build of the scope: floors of the world turf type, walls over the rim of every level, SSmapping's
    // build_area_turfs for the world area (mapping.dm:776-781, after the turfs are final), a testroom with the interior floors
    // of every level moved into it by change_area.
    private static Toy Build(ulong seed = 5, bool initialize = true)
    {
        var toy = new Toy { T = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_indestructible), areaType: typeof(area_space), seed: seed) };
        using (toy.T.World.Enter())
        {
            Dm.world.maxx = W;
            Dm.world.maxy = H;
            Dm.world.maxz = D;
            SSmapping.gravity_by_z_level.len = D;   // plan.md "After W4.0": grown to world.maxz, as manage_z_level does
            ToyRoom.LinkLevels(D);
            toy.Space = (area)locate(1, 1, 1).loc;

            for (int z = 1; z <= D; z++)
            {
                for (int y = 1; y <= H; y++)
                {
                    for (int x = 1; x <= W; x++)
                    {
                        if (x == 1 || y == 1 || x == W || y == H)
                            Dm.@new<turf_closed_indestructible>(locate(x, y, z));
                    }
                }
            }

            toy.Space.turfs_by_zlevel.len = D;
            for (int z = 1; z <= D; z++)
                toy.Space.turfs_by_zlevel[(float)z] = block(1, 1, z, W, H, z);

            toy.Room = Dm.@new<area_misc_testroom>();   // a null location: it holds nothing yet
            for (int z = 1; z <= D; z++)
            {
                foreach (turf T in Dm.each<turf>(block(2, 2, z, W - 1, H - 1, z)))
                    T.change_area((area)T.loc, toy.Room);
            }
            if (initialize)
                TurfAreaWorld.InitializeSSatoms();
        }
        return toy;
    }

    private static List<turf> Listed(area a, int z) => TurfAreaLists.Of<turf>(a.turfs_by_zlevel[(float)z]);

    // ---- the grid and the replaced cells ----

    [Fact]
    public void GrowingTheMapFillsEveryCellWithTheWorldTurfInTheWorldAreaXFastest()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_indestructible), areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            Dm.world.maxx = W;
            Dm.world.maxy = H;
            Dm.world.maxz = D;
            Assert.Same(space, GLOB.areas_by_type[typeof(area_space)]);
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, W, H, D)))
            {
                Assert.IsType<turf_open_indestructible>(T);
                Assert.Same(space, T.loc);
            }
            var order = Dm.each<turf>(block(1, 1, 1, W, H, D)).Select(T => (T.x, T.y, T.z)).ToList();
            Assert.Equal((1f, 1f, 1f), order[0]);
            Assert.Equal((2f, 1f, 1f), order[1]);    // x fastest
            Assert.Equal((1f, 2f, 1f), order[W]);
            Assert.Equal((1f, 1f, 2f), order[W * H]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ReplacingACellKeepsItsAreaAndPlacesTheNewTurfAtTheSameCoordinates()
    {
        var t = new TurfAreaWorld(3, 3, 1, turfType: typeof(turf_open_indestructible), areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            turf old = locate(2, 2, 1);
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 2, 1));
            Assert.Same(wall, locate(2, 2, 1));
            Assert.Same(space, wall.loc);
            Assert.Equal(2, wall.x);
            Assert.Equal(2, wall.y);
            Assert.Equal(1, wall.z);
            Assert.Equal(0, old.x);   // the replaced turf is off the map
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void R13ATurfListedBeforeItIsReplacedIsListedAsItsReplacement()
    {
        // scope.md R13: `new /turf/T(old)` is a new object. The area's turfs_by_zlevel kept naming the dead old one until gaps Q1:
        // BYOND keeps such a turf listed because a turf reference is its location, so the replacement now takes the old one's
        // entry (change_turf.cs dm_replace_listed_turf; tg's area_contents test found the dead turfs in Meta's lists).
        var t = new TurfAreaWorld(3, 1, 1, turfType: typeof(turf_open_indestructible), areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            space.turfs_by_zlevel.len = 1;
            space.turfs_by_zlevel[1f] = block(1, 1, 1, 3, 1, 1);
            turf old = locate(2, 1, 1);

            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));

            var listed = Listed(space, 1);
            Assert.DoesNotContain(old, listed);
            Assert.Contains(wall, listed);
            Assert.Equal(3, listed.Count);
            Assert.True(QDELETED(old));
            Assert.Same(space, wall.loc);
        }
    }

    // ---- the build ----

    [Fact]
    public void TheBuildHasTheTypesTheScopeNames()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            for (int z = 1; z <= D; z++)
            {
                for (int y = 1; y <= H; y++)
                {
                    for (int x = 1; x <= W; x++)
                    {
                        bool rim = x == 1 || y == 1 || x == W || y == H;
                        turf T = locate(x, y, z);
                        Assert.Equal(rim ? typeof(turf_closed_indestructible) : typeof(turf_open_indestructible), T.GetType());
                        Assert.Same(rim ? toy.Space : toy.Room, T.loc);
                    }
                }
            }
            Assert.Empty(toy.T.Runtimes);
        }
    }

    [Fact]
    public void GLOBareasAndAreasByTypeListTheWorldAreaAndTheRoom()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            Assert.Equal(new[] { toy.Space, (area)toy.Room }, TurfAreaLists.Of<area>(GLOB.areas));
            Assert.Same(toy.Space, GLOB.areas_by_type[typeof(area_space)]);
            Assert.Same(toy.Room, GLOB.areas_by_type[typeof(area_misc_testroom)]);
            Assert.Null(toy.Room.loc);   // an area is never inside anything
        }
    }

    [Fact]
    public void EveryTurfIsListedInExactlyOneAreaAfterCanonizingAndInTheAreaItsLocNames()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            foreach (area a in TurfAreaLists.Of<area>(GLOB.areas))
                a.cannonize_contained_turfs();

            for (int z = 1; z <= D; z++)
            {
                var all = new List<turf>();
                foreach (area a in TurfAreaLists.Of<area>(GLOB.areas))
                {
                    foreach (turf T in Listed(a, z))
                    {
                        Assert.Same(a, T.loc);          // the area's list names it, its loc is that area
                        Assert.Equal(z, T.z);
                        all.Add(T);
                    }
                }
                var cells = Dm.each<turf>(block(1, 1, z, W, H, z)).ToList();
                Assert.Equal(cells.Count, all.Count);
                Assert.Equal(cells.OrderBy(T => T.y).ThenBy(T => T.x), all.OrderBy(T => T.y).ThenBy(T => T.x));
            }
            Assert.Equal(6, Listed(toy.Room, 1).Count);
            Assert.Equal(6, Listed(toy.Room, 2).Count);
            Assert.Equal(W * H - 6, Listed(toy.Space, 1).Count);
            Assert.Empty(toy.T.Runtimes);
        }
    }

    [Fact]
    public void TheRoomListsItsTurfsInTheOrderTheyWereMovedIn()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            toy.Room.cannonize_contained_turfs();
            var expected = new List<turf>();
            for (int y = 2; y <= H - 1; y++)
            {
                for (int x = 2; x <= W - 1; x++)
                    expected.Add(locate(x, y, 1));
            }
            Assert.Equal(expected, Listed(toy.Room, 1));
            // area.contents is the engine's own view of the same turfs
            Assert.True(Dm.@in(locate(3, 2, 2), toy.Room.contents));
            Assert.False(Dm.@in(locate(1, 1, 1), toy.Room.contents));
        }
    }

    [Fact]
    public void TheWorldAreaLosesTheRoomTurfsLazilyUntilItIsCanonized()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            Assert.Equal(W * H, Listed(toy.Space, 1).Count);                                // still lists every turf
            Assert.Equal(6, ((DmList)toy.Space.turfs_to_uncontain_by_zlevel[1f]).len);      // and names the six that left
            Assert.True(Dm.eq(toy.Space.has_contained_turfs(), TRUE));
            Assert.True(Dm.eq(toy.Space.get_highest_zlevel(), D));
            toy.Space.cannonize_contained_turfs();
            Assert.Equal(W * H - 6, Listed(toy.Space, 1).Count);
        }
    }

    // ---- SSatoms initializes the room ----

    [Fact]
    public void SSatomsInitializesEveryTurfAndTheRoomExactlyOnce()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, W, H, D)))
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)), T.GetType().Name);
            Assert.True(Dm.truthy(Dm.bitand(toy.Room.flags_1, INITIALIZED_1)));
            Assert.True(Dm.truthy(Dm.bitand(toy.Space.flags_1, INITIALIZED_1)));
            Assert.Empty(toy.T.Runtimes);   // no "initialized multiple times" anywhere
        }
    }

    [Fact]
    public void WallsAndFloorsHaveTheirBaseturfsAfterInitialization()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            var floor = locate(3, 2, 1);
            var wall = locate(1, 1, 1);
            Assert.Same(typeof(turf_baseturf_bottom), floor.baseturfs);
            var list = Assert.IsType<DmList>(wall.baseturfs);
            Assert.Same(typeof(turf_baseturf_bottom), list[1f]);
            Assert.Same(typeof(turf_open_indestructible_plating), list[2f]);
            // every wall shares the one interned list (string_list)
            Assert.Same(list, locate(W, H, D).baseturfs);
        }
    }

    [Fact]
    public void TheRoomIsRegisteredInAreasInZAtItsLowestLevelAndCountsItsOpenTurfs()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            // area.z is the lowest z of the turfs it holds (BYOND ref), so a two-level room registers on level 1 only
            Assert.Equal(1, toy.Room.z);
            var level1 = TurfAreaLists.Of<area>(SSmapping.areas_in_z["1"]);
            Assert.Contains(toy.Room, level1);
            Assert.Contains(toy.Space, level1);
            Assert.Null(SSmapping.areas_in_z["2"]);
            Assert.Equal(12, toy.Room.areasize);   // 6 open turfs on each of two levels
            Assert.True(Dm.eq(toy.Room.luminosity, 1));
            Assert.True(Dm.eq(toy.Room.blend_mode, BLEND_MULTIPLY));
        }
    }

    [Fact]
    public void TheRoomHasNoPowerRequirementSoEveryChannelIsPowered()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            Assert.True(Dm.eq(toy.Room.powered(AREA_USAGE_EQUIP), TRUE));
            Assert.True(Dm.eq(toy.Room.powered(AREA_USAGE_LIGHT), TRUE));
            Assert.True(Dm.eq(toy.Room.powered(AREA_USAGE_ENVIRON), TRUE));
            Assert.True(Dm.eq(toy.Room.default_gravity, STANDARD_GRAVITY));
            Assert.True(Dm.eq(toy.Space.powered(AREA_USAGE_EQUIP), FALSE));
        }
    }

    // ---- growing the finished map ----

    [Fact]
    public void IncreaseMaxXAppendsTheNewColumnsToTheWorldAreasListsAndLeavesTheRoomAlone()
    {
        var toy = Build();
        using (toy.T.World.Enter())
        {
            Dm.world.increase_max_x(W + 2);

            for (int z = 1; z <= D; z++)
            {
                for (int y = 1; y <= H; y++)
                {
                    foreach (int x in new[] { W + 1, W + 2 })
                    {
                        turf T = locate(x, y, z);
                        Assert.IsType<turf_open_indestructible>(T);   // world.turf
                        Assert.Same(toy.Space, T.loc);
                        Assert.Contains(T, Listed(toy.Space, z));
                    }
                }
            }
            Assert.Equal(6, Listed(toy.Room, 1).Count);
            Assert.Empty(toy.T.Runtimes);
        }
    }

    // ---- determinism (translation-rules.md 15) ----

    [Fact]
    public void TheSameBuildTwiceGivesTheSameStateHash()
    {
        ulong first, second;
        {
            var toy = Build(seed: 21);
            first = toy.T.World.ComputeStateHash();
        }
        {
            var toy = Build(seed: 21);
            second = toy.T.World.ComputeStateHash();
        }
        Assert.Equal(first, second);
    }

    [Fact]
    public void TheHashSeesWhichAreaATurfIsInAndTheOrderOfTheAreasLists()
    {
        ulong baseline = Build(seed: 21).T.World.ComputeStateHash();

        var moved = Build(seed: 21);
        using (moved.T.World.Enter())
            locate(3, 2, 1).change_area(moved.Room, moved.Space);
        Assert.NotEqual(baseline, moved.T.World.ComputeStateHash());

        var canonized = Build(seed: 21);
        using (canonized.T.World.Enter())
            canonized.Space.cannonize_contained_turfs();   // same turfs in the same places, a shorter list
        Assert.NotEqual(baseline, canonized.T.World.ComputeStateHash());
    }

    [Fact]
    public void ABuildThatCreatesTheTurfsBeforeSSatomsRunsAndOneThatCreatesThemAfterwardsAgreeOnWhatIsInitialized()
    {
        // each new turf's Initialize is deferred to SSatoms when created before it initialized and immediate after
        // (scope.md section 7, item 1)
        var late = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_indestructible), areaType: typeof(area_space));
        using (late.World.Enter())
        {
            Dm.world.maxx = 3;
            Dm.world.maxy = 3;
            TurfAreaWorld.InitializeSSatoms();               // the Master's init, before the toy builder runs
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 2, 1));
            Assert.True(Dm.truthy(Dm.bitand(wall.flags_1, INITIALIZED_1)));       // immediate
            Assert.Same(wall, locate(2, 2, 1));
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 3, 3, 1)))
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)));
            Assert.Empty(late.Runtimes);
        }
    }
}
