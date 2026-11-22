using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// The map builtins of rules 1.5 (Builtins) against the BYOND reference (get_step, get_dir, get_dist, block, locate,
/// isturf, isarea, ismob, ismovable, isloc) and tg's macros over them (get_turf, get_area: __DEFINES/turfs.dm:79, 86).
/// Every helper works through the loc chain to the outermost turf (move-chain.md E3).
/// </summary>
public class MapBuiltinTests
{
    [Fact]
    public void GetStepMovesOneCellInEachDirection()
    {
        var t = new EngineWorld(3, 3, 3);
        using (t.World.Enter())
        {
            turf c = locate(2, 2, 2);
            Assert.Same(locate(2, 3, 2), get_step(c, Dm.NORTH));
            Assert.Same(locate(2, 1, 2), get_step(c, Dm.SOUTH));
            Assert.Same(locate(3, 2, 2), get_step(c, Dm.EAST));
            Assert.Same(locate(1, 2, 2), get_step(c, Dm.WEST));
            Assert.Same(locate(3, 3, 2), get_step(c, Dm.NORTHEAST));
            Assert.Same(locate(1, 3, 2), get_step(c, Dm.NORTHWEST));
            Assert.Same(locate(3, 1, 2), get_step(c, Dm.SOUTHEAST));
            Assert.Same(locate(1, 1, 2), get_step(c, Dm.SOUTHWEST));
            // UP and DOWN change z: tg's GET_TURF_ABOVE/BELOW are get_step(turf, UP/DOWN) (__DEFINES/maps.dm).
            Assert.Same(locate(2, 2, 3), get_step(c, Dm.UP));
            Assert.Same(locate(2, 2, 1), get_step(c, Dm.DOWN));
            Assert.Same(locate(3, 3, 3), get_step(c, Dm.bitor(Dm.NORTHEAST, Dm.UP)));
            // Dir 0 is the turf itself (tg's get_turf); opposite bits cancel (OpenDream GetStep).
            Assert.Same(c, get_step(c, 0));
            Assert.Same(c, get_step(c, Dm.bitor(Dm.NORTH, Dm.SOUTH)));
            Assert.Same(locate(3, 2, 2), get_step(c, Dm.bitor(Dm.bitor(Dm.NORTH, Dm.SOUTH), Dm.EAST)));
            Assert.Same(c, get_step(c, Dm.bitor(Dm.UP, Dm.DOWN)));
        }
    }

    [Fact]
    public void GetStepIsNullOffTheMapAndFollowsTheOutermostTurf()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf corner = locate(3, 3, 1);
            Assert.Null(get_step(corner, Dm.EAST));
            Assert.Null(get_step(corner, Dm.NORTH));
            Assert.Null(get_step(corner, Dm.NORTHEAST));
            Assert.Null(get_step(corner, Dm.SOUTHEAST)); // x leaves the map even though y stays on it
            Assert.Null(get_step(corner, Dm.UP)); // no z level 2
            Assert.Null(get_step(locate(1, 1, 1), Dm.DOWN));
            Assert.Null(get_step(locate(1, 1, 1), Dm.SOUTHWEST));

            var mob = Dm.@new<mob_test_engine_mob>(locate(2, 2, 1));
            var bag = Dm.@new<obj_test_engine_box>(mob);
            var coin = Dm.@new<obj_test_engine_box>(bag);
            Assert.Same(locate(2, 2, 1), get_step(coin, 0));
            Assert.Same(locate(1, 3, 1), get_step(coin, Dm.NORTHWEST));

            Assert.Null(get_step(Dm.@new<obj_test_engine_box>(), 0)); // nullspace
            Assert.Null(get_step(Dm.@new<obj_test_engine_box>(Dm.@new<obj_test_engine_box>()), Dm.NORTH)); // a container in nullspace
            Assert.Null(get_step(null, 0));
            Assert.Null(get_step(Dm.@new<datum>(), 0));
            Assert.Null(get_step("text", Dm.NORTH));
        }
    }

    [Fact]
    public void E2_StepsGuardsSeeNullAtTheMapEdgeAndTheOwnLocWithDirZero()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            // move-chain.md E2: step(ref, dir) is `t = get_step(ref, dir); if(!t || t == ref.loc) return 0; return
            // ref.Move(t, dir)`, so Move is not called at the map edge or with dir 0 (OpenDream's BYOND-parity test
            // move.dm with world.maxx = 2). The engine's half of that contract is get_step's value in both cases.
            var protag = Dm.@new<mob_test_engine_mob>(locate(2, 1, 1));
            turf off_edge = get_step(protag, Dm.EAST);
            Assert.True(off_edge == null || off_edge == protag.loc);
            Assert.Null(off_edge);
            turf in_place = get_step(protag, 0);
            Assert.Same(protag.loc, in_place);
            turf west = get_step(protag, Dm.WEST);
            Assert.False(west == null || west == protag.loc);
            Assert.Same(locate(1, 1, 1), west);
        }
    }

    [Fact]
    public void GetDirGivesACardinalOnARowOrColumnAndTheDiagonalOfTheQuadrantOtherwise()
    {
        var t = new EngineWorld(9, 9, 1);
        using (t.World.Enter())
        {
            turf c = locate(5, 5, 1);
            Assert.Equal(Dm.NORTH, get_dir(c, locate(5, 9, 1)));
            Assert.Equal(Dm.SOUTH, get_dir(c, locate(5, 1, 1)));
            Assert.Equal(Dm.EAST, get_dir(c, locate(6, 5, 1)));
            Assert.Equal(Dm.WEST, get_dir(c, locate(1, 5, 1)));
            Assert.Equal(Dm.NORTHEAST, get_dir(c, locate(6, 6, 1)));
            Assert.Equal(Dm.NORTHWEST, get_dir(c, locate(4, 6, 1)));
            Assert.Equal(Dm.SOUTHEAST, get_dir(c, locate(6, 4, 1)));
            Assert.Equal(Dm.SOUTHWEST, get_dir(c, locate(4, 4, 1)));
            // BYOND ref get_dir: "if Loc2 is mostly north but a little to the east of Loc1, the direction returned will be
            // NORTHEAST" (never NORTH): any offset on both axes gives the diagonal.
            Assert.Equal(Dm.NORTHEAST, get_dir(c, locate(6, 9, 1)));
            Assert.Equal(Dm.SOUTHWEST, get_dir(c, locate(1, 4, 1)));
            Assert.Equal(0f, get_dir(c, c)); // the same place
            Assert.Equal(Dm.SOUTH, get_dir(locate(5, 9, 1), c));
        }
    }

    [Fact]
    public void GetDirFollowsTheOutermostTurfAndIsZeroOffTheMap()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            var mob = Dm.@new<mob_test_engine_mob>(locate(1, 1, 1));
            var held = Dm.@new<obj_test_engine_box>(mob);
            var target = Dm.@new<obj_test_engine_box>(locate(3, 1, 1));
            // doMove computes get_dir(src, dest) for a movable that may be inside a container (atoms_movable.dm:1186).
            Assert.Equal(Dm.EAST, get_dir(held, target));
            Assert.Equal(Dm.WEST, get_dir(target, held));
            Assert.Equal(0f, get_dir(mob, held)); // the same turf
            Assert.Equal(0f, get_dir(held, Dm.@new<obj_test_engine_box>()));
            Assert.Equal(0f, get_dir(Dm.@new<obj_test_engine_box>(), target));
            Assert.Equal(0f, get_dir(null, target));
            // Across z levels the planar direction (RULE-GAP of the engine package: tg's get_dir_multiz,
            // multiz_helpers.dm:11-29, ORs UP/DOWN with get_dir of turfs on two levels; OpenDream returns 0).
            Assert.Equal(Dm.NORTHEAST, get_dir(locate(1, 1, 1), locate(2, 2, 2)));
            Assert.Equal(0f, get_dir(locate(1, 1, 1), locate(1, 1, 2)));
        }
    }

    [Fact]
    public void GetDistIsTheLargestAxisDistanceIncludingZ()
    {
        var t = new EngineWorld(9, 9, 3);
        using (t.World.Enter())
        {
            // BYOND ref get_dist: "the number of full-tile movements (disregarding any obstacles and allowing diagonal moves
            // including across Z levels) ... the max of their x, y, and z distances".
            turf a = locate(2, 3, 1);
            Assert.Equal(0f, get_dist(a, Dm.@new<obj_test_engine_box>(a)));
            Assert.Equal(1f, get_dist(a, locate(3, 4, 1)));
            Assert.Equal(5f, get_dist(a, locate(7, 4, 1)));
            Assert.Equal(6f, get_dist(a, locate(4, 9, 1)));
            Assert.Equal(2f, get_dist(a, locate(2, 3, 3)));
            Assert.Equal(2f, get_dist(a, locate(3, 4, 3)));
            Assert.Equal(get_dist(a, locate(8, 1, 2)), get_dist(locate(8, 1, 2), a));

            var mob = Dm.@new<mob_test_engine_mob>(locate(5, 5, 1));
            var item = Dm.@new<obj_test_engine_box>(Dm.@new<obj_test_engine_box>(mob)); // in a bag the mob holds
            Assert.Equal(0f, get_dist(mob, item));
            Assert.Equal(3f, get_dist(item, a));
        }
    }

    [Fact]
    public void GetDistIsMinusOneForTheSameObjectAndInfiniteOffTheMap()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            // BYOND ref get_dist (515+): "get_dist() will return -1 when Loc1 and Loc2 are the same object. If one or both
            // of them is not on the map, an infinite value is returned." tg's range checks (`get_dist(a, b) <= 1`) pass
            // for an atom and itself and fail for anything in nullspace.
            turf a = locate(1, 1, 1);
            var mob = Dm.@new<mob_test_engine_mob>(a);
            Assert.Equal(-1f, get_dist(a, a));
            Assert.Equal(-1f, get_dist(mob, mob));
            var lost = Dm.@new<obj_test_engine_box>();
            Assert.Equal(float.PositiveInfinity, get_dist(mob, lost));
            Assert.Equal(float.PositiveInfinity, get_dist(lost, mob));
            Assert.Equal(float.PositiveInfinity, get_dist(mob, null));
            Assert.Equal(float.PositiveInfinity, get_dist(mob, Dm.@new<obj_test_engine_box>(lost)));
            Assert.True(get_dist(mob, lost) > 1000000);
        }
    }

    [Fact]
    public void BlockListsTheInclusiveBoxXFastestThenYThenZ()
    {
        var t = new EngineWorld(4, 4, 3);
        using (t.World.Enter())
        {
            // BYOND ref block: "The list of turfs in the 3D block defined by Start and End (inclusive)".
            var expected = new List<object>();
            for (int z = 2; z <= 3; z++)
                for (int y = 1; y <= 2; y++)
                    for (int x = 2; x <= 4; x++)
                        expected.Add(locate(x, y, z));
            Assert.Equal(expected.ToArray(), Items(block(locate(2, 1, 2), locate(4, 2, 3))));
            Assert.Equal(expected.ToArray(), Items(block(locate(4, 2, 3), locate(2, 1, 2)))); // corners in any order
            Assert.Equal(expected.ToArray(), Items(block(locate(2, 2, 3), locate(4, 1, 2))));
            Assert.Equal(expected.ToArray(), Items(block(2, 1, 2, 4, 2, 3)));
            Assert.Equal(expected.ToArray(), Items(block(4, 2, 3, 2, 1, 2)));
            Assert.Equal(new object[] { locate(3, 3, 1) }, Items(block(locate(3, 3, 1), locate(3, 3, 1))));
        }
    }

    [Fact]
    public void BlockByCoordinatesDefaultsTheMissingEndToTheStartAndCutsToTheMap()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            // BYOND ref block: "you can leave any of the EndX, EndY, or EndZ values as null, and omit the last arguments
            // entirely; they will default to using the corresponding StartX, StartY, or StartZ."
            Assert.Equal(new object[] { locate(1, 1, 1), locate(2, 1, 1), locate(1, 2, 1), locate(2, 2, 1) }, Items(block(1, 1, 1, 2, 2)));
            Assert.Equal(new object[] { locate(2, 3, 2) }, Items(block(2, 3, 2)));
            Assert.Equal(new object[] { locate(1, 2, 1), locate(1, 3, 1) }, Items(block(1, 2, 1, null, 3)));
            Assert.Equal(9, block(0, 0, 2, 9, 9).Count); // cells off the map are left out
            Assert.Empty(Items(block(1, 1, 3, 3, 3, 5)));
            Assert.Empty(Items(block(4, 4, 1, 9, 9, 1)));
            DmList fresh = block(1, 1, 1);
            Assert.NotSame(fresh, block(1, 1, 1)); // a new list each call
        }
    }

    [Fact]
    public void BlockOfSomethingThatIsNotTwoTurfsIsEmpty()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            // OpenDream NativeProc_block: a turf with a non-turf (block("cat", turf)) gives an empty list.
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            Assert.Empty(Items(block(locate(1, 1, 1), box)));
            Assert.Empty(Items(block(box, locate(2, 2, 1))));
            Assert.Empty(Items(block(locate(1, 1, 1), null)));
            Assert.Empty(Items(block(null, null)));
        }
    }

    [Fact]
    public void TgQuirk_TheMultiTileBlockPredictionCoversOneExtraTilePerAxis()
    {
        var t = new EngineWorld(6, 6, 1);
        using (t.World.Enter())
        {
            // Move def1 (atoms_movable.dm:718-720) predicts a multi-tile footprint with
            // block(dx, dy, dz, dx + ceil(bound_width / 32), dy + ceil(bound_height / 32), dz); block is inclusive, so a
            // 64x64 atom (2x2 tiles) is predicted on 3x3 tiles (move-chain.md hazard 10, a tg quirk to keep).
            float dx = 2, dy = 2, dz = 1, bound_width = 64, bound_height = 64;
            DmList predicted = block(dx, dy, dz, dx + Dm.ceil(bound_width / 32), dy + Dm.ceil(bound_height / 32), dz);
            Assert.Equal(9, predicted.Count);
            Assert.Same(locate(4, 4, 1), predicted[9f]);
        }
    }

    [Fact]
    public void TheTypeTestsOfTheEngine()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf cell = locate(1, 1, 1);
            area room = (area)cell.loc;
            var box = Dm.@new<obj_test_engine_box>(cell);
            var mob = Dm.@new<mob_test_engine_mob>(cell);
            var effect = Dm.@new<atom_movable_test_engine_effect>(cell);
            var d = Dm.@new<datum>();
            object[] values = { cell, room, box, mob, effect, d, null, Dm.list(), "turf", 1f, typeof(turf), typeof(mob) };
            // BYOND ref: isturf, isarea, ismob; ismovable "valid objs or mobs"; isloc "mobs, objs, turfs, or areas". A plain
            // /atom/movable counts as movable and as a loc (OpenDream: "You might think this excludes /atom/movable, but it
            // does not"); tg's /atom/movable/screen and lighting objects rely on ismovable.
            Assert.Equal(new[] { true, false, false, false, false, false, false, false, false, false, false, false }, values.Select(isturf).ToArray());
            Assert.Equal(new[] { false, true, false, false, false, false, false, false, false, false, false, false }, values.Select(isarea).ToArray());
            Assert.Equal(new[] { false, false, false, true, false, false, false, false, false, false, false, false }, values.Select(ismob).ToArray());
            Assert.Equal(new[] { false, false, true, true, true, false, false, false, false, false, false, false }, values.Select(ismovable).ToArray());
            Assert.Equal(new[] { true, true, true, true, true, false, false, false, false, false, false, false }, values.Select(isloc).ToArray());
            Assert.Same(cell, effect.loc);
            Assert.Equal(1f, effect.x);
        }
    }

    [Fact]
    public void GetTurfAndGetAreaReachThroughContainers()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf cell = locate(2, 3, 1);
            area room = (area)cell.loc;
            var closet = Dm.@new<obj_test_engine_box>(cell);
            var mob = Dm.@new<mob_test_engine_mob>(closet);
            var held = Dm.@new<obj_test_engine_box>(mob);

            // __DEFINES/turfs.dm:79 `get_turf(A) (get_step(A, 0))` and :86 `get_area(A) (isarea(A) ? A : get_step(A, 0)?.loc)`.
            Assert.Same(cell, get_turf(held));
            Assert.Same(cell, get_turf(cell));
            Assert.Same(room, get_area(held));
            Assert.Same(room, get_area(cell));
            Assert.Same(room, get_area(room));
            Assert.Null(get_turf(null));
            Assert.Null(get_area(null));
            Assert.Null(get_turf(Dm.@new<datum>()));

            var hall = Dm.@new<area_test_engine_hall>();
            Dm.append(hall.contents, cell); // change_area: get_area follows at once
            Assert.Same(hall, get_area(held));
        }
    }

    [Fact]
    public void TgQuirk_GetTurfOfAnAreaIsItsLowestTurf()
    {
        var t = new EngineWorld(3, 3, 2);
        using (t.World.Enter())
        {
            // get_turf(area) is get_step(area, 0): an area's position is its turf with the lowest z, y, x (BYOND ref x var),
            // so tg's get_turf of an area on the map is that turf, and of an area off the map null.
            var hall = Dm.@new<area_test_engine_hall>();
            Assert.Null(get_turf(hall));
            Dm.append(hall.contents, locate(3, 2, 2));
            Dm.append(hall.contents, locate(1, 3, 2));
            Assert.Same(locate(3, 2, 2), get_turf(hall));
            Assert.Equal(2f, get_dist(hall, locate(1, 1, 1)));
        }
    }
}
