using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// T2's bridge goldens against docs/ai/specs/move-chain.md section 3a/3d and its goldens T1 (step in one area) and T2 (step
/// across areas), written from the DM order of events (atoms_movable.dm:687-760 and :763-870, turf.dm:434, areas.dm:581-606,
/// mob_movement.dm:135-145), not from the bridge: a capsule crossing a tile border is tg's real Move(new turf, dir, 0,
/// update_dir = FALSE), so it must send exactly the signals of tg's step(), in tg's order, and then client/Move's tail
/// (COMSIG_MOB_CLIENT_MOVED). The mover faces the way it goes, so tg's step() sends no direction-change signals either and
/// the two traces can be compared whole.
/// </summary>
public class CrossingGoldenTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    private static (T2Room Room, mob_living_carbon_human Mover, turf From, turf To) Stage(ulong seed, int x = 5, int y = 5, float dir = EAST, ushort? yaw = null)
    {
        var r = new T2Room(seed);
        mob_living_carbon_human m = r.Join(x, y, "Alice", name: "mover");
        r.Go(m, 0, 0, yaw ?? T2Room.Yaw(dir), 2); // look the way it will go; the facing settles first
        Assert.Equal(dir, r.In(() => m.dir));
        r.AssertNoRuntimes();
        return (r, m, r.In(() => locate(x, y, 1)), r.In(() => locate(x + (dir == EAST ? 1 : dir == WEST ? -1 : 0), y + (dir == NORTH ? 1 : dir == SOUTH ? -1 : 0), 1)));
    }

    // ---- T1: a step in one area ----

    [Fact]
    public void T1_ACrossingInOneAreaSendsEverySignalOfTgsStepInTgsOrderThenClientMovesTail()
    {
        var (r, m, a, b) = Stage(101);
        r.In(() =>
        {
            T2Room.WatchChain(m, a, b);
            On(m, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3);
        });
        r.Trace.Clear();

        // 1/3 tile per tick from the tile centre (1280): 1365, 1450, then 1535 crosses the border at 1408.
        r.Go(m, 127, 0, East, 3);

        Assert.Equal((6, 5, 1), r.LocOf(m));
        AssertTrace(new[]
        {
            "attempted mover (6,5) 4",           // move-chain 3a.2: before anything, with the direction asked for
            // no pre_dir/dir/post_dir: update_dir = FALSE (3a.5), and the mover already faces east
            "exit (5,5) mover 4",                // 3a.6: loc.Exit(src, direction)
            "tried_pass_turf (6,5) mover 8",     // 3a.7 / 3b: newloc.Enter -> CanPass, border_dir = get_dir(turf, mover) = WEST
            "pre_move mover (6,5)",              // 3a.8: after Enter
            "exited (5,5) mover 4",              // 3a.10: point of no return passed: oldloc.Exited
            "exiting mover (5,5) 4",
            "entered (6,5) mover (5,5) null",    // 3a.12: newloc.Entered(src, oldloc, old_locs)
            "entering mover (6,5) (5,5) null",
            "moved mover (5,5) 4 0 null 1",      // 3a.14 / 3d: Moved(oldloc, direction, FALSE, old_locs, momentum_change = TRUE)
            "abstract_exited (5,5) mover 4",     // 3d.3
            "abstract_entered (6,5) mover (5,5) null",
            "client_moved mover 4 4 (5,5)",      // mob_movement.dm:141: SEND_SIGNAL(mob, COMSIG_MOB_CLIENT_MOVED, direct, old_dir, old_loc)
        }, r.Trace);
        Assert.Equal(EAST, r.In(() => m.dir));
        AssertNumber(EAST, r.In(() => m.last_move));            // def2's tail: last_move = direct
        Assert.Null(r.In(() => m.active_movement));
        Assert.Equal(0f, r.In(() => m.moving_diagonally));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void T1_TheBridgesTraceEqualsADirectStepOfAnIdenticalMoverSignalForSignal()
    {
        // the golden by construction: the same mover, the same probes, tg's own step() in a world with no bridge
        var twin = new T2Room(102);
        mob_living_carbon_human t = twin.Dummy(5, 5, name: "mover");
        twin.In(() => t.dir = EAST);
        twin.In(() => T2Room.WatchChain(t, locate(5, 5, 1), locate(6, 5, 1)));
        twin.Trace.Clear();
        AssertNumber(TRUE, twin.In(() => Step(t, EAST)));
        List<string> expected = twin.Trace.ToList();

        var (r, m, a, b) = Stage(103);
        r.In(() => T2Room.WatchChain(m, a, b));
        r.Trace.Clear();
        r.Go(m, 127, 0, East, 3);

        Assert.True(expected.Count >= 11, "the twin's step sent " + expected.Count + " signals");
        AssertTrace(expected, r.Trace);
    }

    [Fact]
    public void T1_TheMoverEndsAsTheLastItemOfTheTurfsContentsAndAThingThatWasThereKeepsItsPlace()
    {
        var (r, m, a, b) = Stage(104);
        obj rug = r.In(() => Thing("rug", b)); // not dense: crossed, not bumped (3b)
        r.In(() => Thing("mat", b));
        r.Go(m, 127, 0, East, 3);
        object[] contents = r.In(() => Items(b.contents));
        Assert.Equal(3, contents.Length);
        Assert.Same(m, contents[2]);        // arrival order (move-chain E1)
        Assert.Same(rug, contents[0]);
        Assert.DoesNotContain(m, r.In(() => Items(a.contents)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void T1_AnOccupantOfTheNextTileIsCrossedWithItsCrossSignalsBeforePreMove()
    {
        var (r, m, a, b) = Stage(105);
        obj rug = r.In(() => Thing("rug", b));
        r.In(() =>
        {
            T2Room.WatchChain(m, a, b);
            On(rug, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(rug, COMSIG_ATOM_TRIED_PASS, "tried_pass_rug", 2);
        });
        r.Trace.Clear();
        r.Go(m, 127, 0, East, 3);
        // 3b.2: the turf's CanPass first, then for each occupant its Cross (its signal, the mover's), then the occupant's CanPass
        AssertTrace(new[]
        {
            "attempted mover (6,5) 4", "exit (5,5) mover 4",
            "tried_pass_turf (6,5) mover 8",
            "cross rug mover", "cross_over mover rug", "tried_pass_rug rug mover 8",
            "pre_move mover (6,5)",
        }, r.Trace.Take(7));
        r.AssertNoRuntimes();
    }

    // ---- T2: a step across areas ----

    [Fact]
    public void T2_ACrossingIntoAnotherAreaAddsTheAreaSignalsAfterTheTurfsOnes()
    {
        var r = new T2Room(106);
        mob_living_carbon_human m = r.Join(7, 5, "Alice", name: "mover");
        area annex = r.In(() =>
        {
            area outer = (area)locate(7, 5, 1).loc;
            outer.name = "hall";
            var other = Dm.@new<area_misc_testroom>();
            other.name = "annex";
            for (int y = 2; y <= 15; y++)
                for (int x = 8; x <= 15; x++)
                    Dm.append(other.contents, locate(x, y, 1)); // x 8 and east belong to the annex
            return other;
        });
        r.Go(m, 0, 0, East, 2);
        r.In(() => T2Room.WatchChain(m, locate(7, 5, 1), locate(8, 5, 1)));
        r.Trace.Clear();

        r.Go(m, 127, 0, East, 3); // 1792 + 3 * 85 = 2047 > 1920 (the border between tile 7 and 8 is at 7 * 256 + 128 = 1920)

        Assert.Equal((8, 5, 1), r.LocOf(m));
        AssertTrace(new[]
        {
            "attempted mover (8,5) 4",
            "exit (7,5) mover 4",
            "tried_pass_turf (8,5) mover 8",
            "pre_move mover (8,5)",
            "exited (7,5) mover 4",
            "exiting mover (7,5) 4",
            "area_exited area:hall mover 4",           // 3a.11: oldarea.Exited(src, direction) after oldloc.Exited
            "exited_area mover area:hall 4",           // COMSIG_MOVABLE_EXITED_AREA(src; area, dir)
            "entered (8,5) mover (7,5) null",
            "entering mover (8,5) (7,5) null",
            "area_entered area:annex mover area:hall", // 3a.13: newarea.Entered(src, oldarea) after newloc.Entered
            "moved mover (7,5) 4 0 null 1",
            "abstract_exited (7,5) mover 4",
            "abstract_entered (8,5) mover (7,5) null",
        }, r.Trace);
        Assert.Same(annex, r.In(() => get_area(m)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void T2_AnAreaBorderCrossingEqualsADirectStepToo()
    {
        List<string> Direct()
        {
            var twin = new T2Room(107);
            mob_living_carbon_human t = twin.Dummy(7, 5, name: "mover");
            twin.In(() =>
            {
                ((area)locate(7, 5, 1).loc).name = "hall";
                var other = Dm.@new<area_misc_testroom>();
                other.name = "annex";
                Dm.append(other.contents, locate(8, 5, 1));
                t.dir = EAST;
                T2Room.WatchChain(t, locate(7, 5, 1), locate(8, 5, 1));
            });
            twin.Trace.Clear();
            AssertNumber(TRUE, twin.In(() => Step(t, EAST)));
            return twin.Trace.ToList();
        }
        List<string> expected = Direct();

        var r = new T2Room(108);
        mob_living_carbon_human m = r.Join(7, 5, "Alice", name: "mover");
        r.In(() =>
        {
            ((area)locate(7, 5, 1).loc).name = "hall";
            var other = Dm.@new<area_misc_testroom>();
            other.name = "annex";
            Dm.append(other.contents, locate(8, 5, 1));
        });
        r.Go(m, 0, 0, East, 2);
        r.In(() => T2Room.WatchChain(m, locate(7, 5, 1), locate(8, 5, 1)));
        r.Trace.Clear();
        r.Go(m, 127, 0, East, 3);
        Assert.Contains("area_exited area:hall mover 4", expected);
        AssertTrace(expected, r.Trace);
    }

    // ---- T6: the diagonal ----

    [Fact]
    public void T6_AnExactCornerCrossingIsTwoWholeCardinalChainsNorthSouthFirstEqualToTwoSteps()
    {
        // tg's diagonal Move is two step()s, N/S component first (move-chain 3f); on the bridge the exact corner gives the
        // same two cardinal Moves in the same order
        var twin = new T2Room(109);
        mob_living_carbon_human t = twin.Dummy(5, 5, name: "mover");
        twin.In(() =>
        {
            t.dir = NORTH;
            T2Room.WatchChain(t, locate(5, 5, 1), locate(5, 6, 1));
            T2Room.WatchTurfs(locate(5, 6, 1), locate(6, 6, 1));
        });
        twin.Trace.Clear();
        AssertNumber(TRUE, twin.In(() => Step(t, NORTH)));
        AssertNumber(TRUE, twin.In(() => Step(t, EAST)));
        List<string> expected = twin.Trace.ToList();

        var r = new T2Room(110);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, T2Room.Yaw(NORTH), 2);
        r.In(() =>
        {
            T2Room.WatchChain(m, locate(5, 5, 1), locate(5, 6, 1));
            T2Room.WatchTurfs(locate(5, 6, 1), locate(6, 6, 1));
        });
        r.Trace.Clear();
        r.Go(m, 127, 127, T2Room.Yaw(NORTH), 3); // 60 subunits per axis per tick: both borders (1408) are crossed in tick 3

        Assert.Equal((6, 6, 1), r.LocOf(m));
        Assert.Equal(NORTH, r.In(() => m.dir)); // a diagonal wish does not turn the mob (update_dir = FALSE)
        // the dir of the second step differs: tg's step(EAST) turns the twin east, the bridge's Move does not
        List<string> got = r.Trace.ToList();
        Assert.Equal(expected.Count - 3, got.Count);
        Assert.Equal(expected.Where(l => !l.StartsWith("pre_dir", StringComparison.Ordinal) && !l.StartsWith("dir ", StringComparison.Ordinal) && !l.StartsWith("post_dir", StringComparison.Ordinal)), got);
        int north = got.IndexOf("pre_move mover (5,6)");
        int east = got.IndexOf("pre_move mover (6,6)");
        Assert.True(north >= 0 && north < east, string.Join("\n", got));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void T6_ADiagonalWalkMovesWestAndSouthInTheSameOrder()
    {
        var r = new T2Room(111);
        mob_living_carbon_human m = r.Join(9, 9, "Alice", name: "mover");
        r.In(() =>
        {
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
        });
        r.Go(m, -127, -127, T2Room.Yaw(NORTH), 3);
        Assert.Equal((8, 8, 1), r.LocOf(m));
        // N/S first: the south step (9,8), then the west step (8,8)
        AssertTrace(new[] { "pre_move mover (9,8)", "pre_move mover (8,8)" }, r.Trace.Where(l => l.StartsWith("pre_move", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    // ---- a long walk against a chain of steps ----

    [Fact]
    public void AWalkAcrossTheRoomMakesTheSameMovesAsTheChainOfStepsOfItsTiles()
    {
        // one pre_move per tile crossed, each into the neighbour of the last one, none missing and none repeated
        var r = new T2Room(112);
        mob_living_carbon_human m = r.Join(2, 2, "Alice", name: "mover");
        var tiles = new List<string>();
        r.In(() => On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1));
        r.Go(m, 0, 127, T2Room.Yaw(NORTH), 3 * 6);   // six tiles north: 1/3 tile per tick
        r.Go(m, 127, 0, East, 3 * 5);
        r.Go(m, 0, -127, T2Room.Yaw(SOUTH), 3 * 4);
        r.Go(m, -127, 0, T2Room.Yaw(WEST), 3 * 3);
        string[] moves = r.Trace.Where(l => l.StartsWith("pre_move", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[]
        {
            "pre_move mover (2,3)", "pre_move mover (2,4)", "pre_move mover (2,5)", "pre_move mover (2,6)", "pre_move mover (2,7)", "pre_move mover (2,8)",
            "pre_move mover (3,8)", "pre_move mover (4,8)", "pre_move mover (5,8)", "pre_move mover (6,8)", "pre_move mover (7,8)",
            "pre_move mover (7,7)", "pre_move mover (7,6)", "pre_move mover (7,5)", "pre_move mover (7,4)",
            "pre_move mover (6,4)", "pre_move mover (5,4)", "pre_move mover (4,4)",
        }, moves);
        Assert.Equal((4, 4, 1), r.LocOf(m));
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }
}
