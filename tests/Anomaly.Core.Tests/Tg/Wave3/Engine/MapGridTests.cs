using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// The map grid behind world.maxx/maxy/maxz and locate(x, y, z) (scope R4, R5): BYOND ref /world/var/maxx, "New
/// territory created by increasing the map boundaries is filled in with the default turf and area (world.turf, and
/// world.area)", and tg's reliance on it in /world/proc/increase_max_x and increase_max_y (game/world.dm:458-490).
/// </summary>
public class MapGridTests
{
    private static string Cell(turf t) => "New " + EngineTrace.D(t) + " loc=" + EngineTrace.D(t.loc) + " xyz=" + EngineTrace.D(t.x) + "," + EngineTrace.D(t.y) + "," + EngineTrace.D(t.z) + " contents=0";

    [Fact]
    public void GrowingTheMapCreatesWorldTurfsInTheWorldAreaXFastestEachAlreadyInItsCellAtNew()
    {
        var t = new EngineWorld(3, 2, 2);
        using (t.World.Enter())
        {
            var cells = new List<turf>();
            for (int z = 1; z <= 2; z++)
                for (int y = 1; y <= 2; y++)
                    for (int x = 1; x <= 3; x++)
                        cells.Add(locate(x, y, z));
            Assert.All(cells, c => Assert.IsType<turf_test_engine_floor>(c));
            Assert.Equal(12, cells.Distinct().Count());

            // Created x fastest, then y, then z: world ids rise in that order and each New already sees the turf in its
            // cell and its area (BYOND ref New proc (atom): "the object has already been created at the specified location").
            for (int i = 1; i < cells.Count; i++) Assert.True(cells[i - 1].dm_id < cells[i].dm_id);
            area room = (area)cells[0].loc;
            Assert.IsType<area_test_engine_room>(room);
            var expected = new List<string> { "New " + EngineTrace.D(room) + " loc=null contents=0" };
            expected.AddRange(cells.Select(Cell));
            Assert.Equal(expected, t.Trace);
            Assert.Equal(cells.Cast<object>().ToArray(), Items(room));
            Assert.Equal(cells.Cast<object>().ToArray(), Dm.each<turf>(Dm.world).Cast<object>().ToArray());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void NoTurfExistsUntilEveryDimensionIsSet()
    {
        var world = new World(3);
        MapGrid.Attach(world);
        using (world.Enter())
        {
            Dm.world.turf = typeof(turf_test_engine_floor);
            Dm.world.area = typeof(area_test_engine_room);
            Dm.world.maxx = 2;
            Dm.world.maxy = 2;
            Assert.Empty(Dm.each<turf>(Dm.world).Cast<object>());
            Assert.Null(locate(1, 1, 1));
            Assert.Empty(Items(block(1, 1, 1, 2, 2, 1)));

            Dm.world.maxz = 1; // BYOND ref maxx: "The default value is 0, indicating no map."
            Assert.Equal(4, Dm.each<turf>(Dm.world).Count());
            Assert.NotNull(locate(2, 2, 1));
        }
    }

    [Fact]
    public void GrowingAgainKeepsEveryOldTurfAndFillsOnlyTheNewCells()
    {
        var t = new EngineWorld(2, 2, 2);
        using (t.World.Enter())
        {
            turf keep = locate(2, 2, 2);
            var box = Dm.@new<obj_test_engine_box>(keep);
            area room = (area)keep.loc;
            t.Trace.Clear();

            Dm.world.maxy = 3;
            // The new row of each z level, x fastest, z by z, appended to the same area.
            var added = new[] { locate(1, 3, 1), locate(2, 3, 1), locate(1, 3, 2), locate(2, 3, 2) };
            Assert.Equal(added.Select(Cell).ToList(), t.Trace);
            Assert.Equal(added.Cast<object>().ToArray(), Items(room).Skip(8).ToArray());
            Assert.Same(keep, locate(2, 2, 2));
            Assert.Same(keep, box.loc);
            Assert.Equal(new[] { 2f, 2f, 2f }, new[] { box.x, box.y, box.z });

            t.Trace.Clear();
            Dm.world.maxz = 3;
            var level3 = new[] { locate(1, 1, 3), locate(2, 1, 3), locate(1, 2, 3), locate(2, 2, 3), locate(1, 3, 3), locate(2, 3, 3) };
            Assert.Equal(level3.Select(Cell).ToList(), t.Trace);
            Assert.Equal(18, room.contents.Count);

            Dm.world.maxx = 2; // the same size: nothing happens
            Assert.Equal(18, room.contents.Count);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheDefaultAreaIsOneInstanceCreatedWithNoLocationAndKeptAcrossGrowths()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            Assert.Null(room.loc);
            Dm.world.maxx = 4;
            Dm.world.maxz = 2;
            // BYOND ref /area: "For each area type defined, one area object is created at runtime."
            Assert.All(Dm.each<turf>(Dm.world).Cast<turf>(), c => Assert.Same(room, c.loc));
            Assert.Single(Dm.each<area>(Dm.world).Cast<object>());
            Assert.Single(t.Trace, l => l.StartsWith("New area_test_engine_room", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheDefaultAreaIsTgsRegisteredInstanceOfWorldArea()
    {
        var world = new World(5);
        MapGrid.Attach(world);
        using (world.Enter())
        {
            // tg's /area/New registers each unique area in GLOB.areas_by_type (areas.dm:177), and increase_max_x reads
            // GLOB.areas_by_type[world.area] as "the global area" the new turfs are in (game/world.dm:465).
            var global_area = Dm.@new<area_test_engine_room>();
            TgState.Current.GLOB = new datum_controller_global_vars(); // set through TgState (lead note: main splits GLOB and GLOB__var)
            TgState.Current.GLOB.areas_by_type = Dm.list(Dm.kv(typeof(area_test_engine_room), global_area));
            Dm.world.turf = typeof(turf_test_engine_floor);
            Dm.world.area = typeof(area_test_engine_room);
            Dm.world.maxx = 2;
            Dm.world.maxy = 1;
            Dm.world.maxz = 1;
            Assert.Same(global_area, locate(1, 1, 1).loc);
            Assert.Same(global_area, locate(2, 1, 1).loc);
            Assert.Single(Dm.each<area>(Dm.world).Cast<object>());
        }
    }

    [Fact]
    public void AChangedWorldAreaHoldsOnlyTheTurfsCreatedAfterTheChange()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            Dm.world.area = typeof(area_test_engine_hall);
            Dm.world.turf = typeof(turf_test_engine_wall);
            Dm.world.maxx = 3;
            Assert.Same(room, locate(2, 1, 1).loc);
            Assert.IsType<turf_test_engine_floor>(locate(2, 1, 1));
            Assert.IsType<turf_test_engine_wall>(locate(3, 1, 1));
            Assert.IsType<area_test_engine_hall>(locate(3, 1, 1).loc);
            Assert.Equal(2, room.contents.Count);
        }
    }

    [Fact]
    public void WithoutWorldTurfTheMapIsMadeOfBuiltinTurfs()
    {
        var world = new World(9) { RuntimeErrorSink = _ => { } };
        MapGrid.Attach(world);
        using (world.Enter())
        {
            // BYOND ref /world/var/turf: "Default value: /turf." The port reads a null world.turf as that default (the
            // bootstrap sets tg's /turf/open/space/basic). No assertion on runtimes: a plain /turf runs tg's /atom/New
            // once the atoms package lands, which needs SSatoms.
            Dm.world.area = typeof(area_test_engine_room);
            Dm.world.maxx = 2;
            Dm.world.maxy = 1;
            Dm.world.maxz = 1;
            Assert.Equal(typeof(turf), locate(1, 1, 1).GetType());
            Assert.Equal(typeof(turf), locate(2, 1, 1).GetType());
            Assert.IsType<area_test_engine_room>(locate(2, 1, 1).loc);
        }
    }

    [Fact]
    public void ShrinkingTheMapIsARuntimeAndLeavesTheMapAsItWas()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            turf corner = locate(3, 3, 2);
            // tg never shrinks the map (scope R4): the port refuses with a runtime and keeps every turf.
            Assert.ThrowsAny<Exception>(() => Dm.world.maxx = 2);
            Assert.ThrowsAny<Exception>(() => Dm.world.maxy = 1);
            Assert.ThrowsAny<Exception>(() => Dm.world.maxz = 0);
            Assert.Equal(new[] { 3f, 3f, 2f }, new[] { Dm.world.maxx, Dm.world.maxy, Dm.world.maxz });
            Assert.Same(corner, locate(3, 3, 2));
            Assert.Equal(18, Dm.each<turf>(Dm.world).Count());

            Dm.world.maxx = 4; // growing still works afterwards
            Assert.NotNull(locate(4, 3, 2));
            Assert.Equal(24, Dm.each<turf>(Dm.world).Count());
        }
    }

    [Fact]
    public void DV1_ARuntimeInATurfsNewIsReportedAndTheMapKeepsFilling()
    {
        var t = new EngineWorld(3, 2, 1, turfType: typeof(turf_test_engine_faulty));
        using (t.World.Enter())
        {
            // A runtime ends that turf's New; BYOND goes on creating the rest of the map (an engine isolation boundary).
            Assert.Equal(2, t.Runtimes.Count);
            Assert.All(t.Runtimes, e => Assert.Contains("faulty turf at x 2", e.Message));
            for (int y = 1; y <= 2; y++)
            {
                for (int x = 1; x <= 3; x++)
                {
                    turf cell = locate(x, y, 1);
                    Assert.IsType<turf_test_engine_faulty>(cell);
                    Assert.Equal(new[] { (float)x, (float)y, 1f }, new[] { cell.x, cell.y, cell.z });
                    Assert.IsType<area_test_engine_room>(cell.loc);
                }
            }
        }
    }

    [Fact]
    public void TgQuirk_IncreaseMaxXReadsTheNewColumnWithBlockRightAfterTheAssignment()
    {
        var t = new EngineWorld(2, 2, 2);
        using (t.World.Enter())
        {
            area global_area = (area)locate(1, 1, 1).loc;
            // game/world.dm:458-472 /world/proc/increase_max_x(new_maxx, map_load_z_cutoff = maxz): the turfs of the new
            // cells must exist as soon as `maxx = new_maxx` has run, since the next lines collect them with block().
            float new_maxx = 4;
            float map_load_z_cutoff = Dm.world.maxz;
            float old_max = Dm.world.maxx;
            Dm.world.maxx = new_maxx;
            var collected = new List<object>();
            for (float zlevel = 1, zlevel__end = map_load_z_cutoff; zlevel <= zlevel__end; zlevel++)
            {
                DmList to_add = block(old_max + 1, 1, zlevel, Dm.world.maxx, Dm.world.maxy, zlevel);
                collected.AddRange(Items(to_add));
            }
            Assert.Equal(new object[]
            {
                locate(3, 1, 1), locate(4, 1, 1), locate(3, 2, 1), locate(4, 2, 1),
                locate(3, 1, 2), locate(4, 1, 2), locate(3, 2, 2), locate(4, 2, 2),
            }, collected.ToArray());
            Assert.All(collected, c => Assert.Same(global_area, ((turf)c).loc));
            Assert.DoesNotContain(null, collected);
        }
    }

    [Fact]
    public void LocateFindsTheTurfOfACellAndNullOutsideTheMap()
    {
        var t = new EngineWorld(4, 3, 2);
        using (t.World.Enter())
        {
            turf cell = locate(4, 3, 2);
            Assert.Equal(new[] { 4f, 3f, 2f }, new[] { cell.x, cell.y, cell.z });
            Assert.Same(locate(1, 1, 1), Dm.each<turf>(Dm.world).First());
            // Out of range on any axis, z = 0, negative, non-finite: null (move-chain.md E3, scope R5).
            Assert.Null(locate(0, 1, 1));
            Assert.Null(locate(1, 0, 1));
            Assert.Null(locate(1, 1, 0));
            Assert.Null(locate(5, 1, 1));
            Assert.Null(locate(1, 4, 1));
            Assert.Null(locate(1, 1, 3));
            Assert.Null(locate(-1, -1, -1));
            Assert.Null(locate(float.NaN, 1, 1));
            Assert.Null(locate(float.PositiveInfinity, 1, 1));
            Assert.Null(locate(1e9f, 1, 1));
            // Non-integer coordinates are floored (scope R5; flagged for the BYOND oracle list).
            Assert.Same(locate(2, 3, 1), locate(2.99f, 3.5f, 1.01f));
            Assert.Null(locate(0.5f, 1, 1));
        }
    }

    [Fact]
    public void EachWorldHasItsOwnMap()
    {
        var small = new EngineWorld(1, 1, 1, seed: 1);
        var large = new EngineWorld(3, 3, 1, seed: 1, turfType: typeof(turf_test_engine_wall));
        turf a;
        turf b;
        using (small.World.Enter())
        {
            a = locate(1, 1, 1);
            Assert.Null(locate(2, 2, 1));
            Assert.Equal(1f, Dm.world.maxx);
        }
        using (large.World.Enter())
        {
            b = locate(1, 1, 1);
            Assert.NotNull(locate(3, 3, 1));
            Assert.Equal(3f, Dm.world.maxx);
        }
        Assert.NotSame(a, b);
        Assert.IsType<turf_test_engine_floor>(a);
        Assert.IsType<turf_test_engine_wall>(b);
    }
}
