using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Movable.Chain;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>
/// The goldens of docs/ai/specs/move-chain.md section 11 that need neither package B's /turf/Enter nor package C's mob: T1
/// (step, same area), T2 (step across areas, forceMove across areas, moveToNullspace), T2b (step with direction 0, off the
/// map edge, from nullspace never calls Move) and the null-versus-FALSE returns of Move (3a, 3f, 3g). Written from tg's
/// game/atoms_movable.dm and the order of events of move-chain.md section 3, on fixture floors (their Enter stands in for
/// /turf/Enter) and fixture areas (their Entered and Exited record the calls Move and doMove make to /area's).
/// </summary>
public class MoveChainGoldenTests
{
    // ---- T1: step() in one area ----

    [Fact]
    public void T1_AStepOutOfAContainerLeavesThroughTheContainersExitAndExitedAndTheTurfGetsTheEntered()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var item = Dm.@new<obj_test_movable_thing>(box);
            On(box, COMSIG_ATOM_EXIT, "exit");
            On(box, COMSIG_ATOM_EXITED, "exited");
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(item, COMSIG_MOVABLE_MOVED, "moved");
            On(box, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
            On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
            map.Trace.Clear();

            // get_step(item, EAST) resolves the item to the turf its box is on (move-chain.md E3), so the step is a move
            // out of the box to the next turf.
            AssertNumber(TRUE, Step(item, EAST));

            Assert.Same(there, item.loc);
            Assert.Equal(new[]
            {
                "exit " + D(box) + " " + D(item) + " 4 null",
                "Enter " + D(there) + " " + D(item),
                "exited " + D(box) + " " + D(item) + " 4 null",
                "entered " + D(there) + " " + D(item) + " " + D(box) + " null",
                "moved " + D(item) + " " + D(box) + " 4 0",
                "abstract_exited " + D(box) + " " + D(item) + " 4 null",
                "abstract_entered " + D(there) + " " + D(item) + " " + D(box) + " null",
            }, map.Trace);
            Assert.DoesNotContain(item, Items(box.contents));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T1_TheMoverEndsAsTheLastItemOfTheTurfsContentsAndTheTurfsContentsKeepTheirOrder()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var a = Dm.@new<obj_test_movable_thing>(there);
            var b = Dm.@new<obj_test_movable_thing>(there);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var c = Dm.@new<obj_test_movable_thing>(here);

            Step(mover, EAST);
            Assert.Equal(new object[] { a, b, mover }, Items(there.contents)); // arrival order (move-chain.md E1)
            Assert.Equal(new object[] { c }, Items(here.contents));

            // Leaving and coming back puts a thing last again.
            Step(a, WEST);
            Step(a, EAST);
            Assert.Equal(new object[] { b, mover, a }, Items(there.contents));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T1_AStepSetsTheDirectionLastMoveAndLeavesNothingPending()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(2, 2, 1));
            mover.dir = SOUTH;

            AssertNumber(TRUE, Step(mover, NORTH));
            Assert.Equal(NORTH, mover.dir);
            AssertNumber(NORTH, mover.last_move);
            Assert.Null(mover.active_movement);
            Assert.Equal(0f, mover.moving_diagonally);
            Assert.Same(locate(2, 3, 1), mover.loc);
            Assert.Equal(new float[] { 2, 3, 1 }, new[] { mover.x, mover.y, mover.z });
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- T2: areas ----

    [Fact]
    public void T2_AStepAcrossAnAreaBorderCallsTheOldAreasExitedAfterTheTurfsAndTheNewAreasEnteredAfterTheTurfs()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var roomA = (area)here.loc;
            var roomB = MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_other>(there);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Equal(new[]
            {
                "Enter " + D(there) + " " + D(mover),
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "area_exited " + D(roomA) + " " + D(mover) + " 4", // oldarea.Exited(src, direction)
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "area_entered " + D(roomB) + " " + D(mover) + " " + D(roomA), // newarea.Entered(src, oldarea)
                "moved " + D(mover) + " " + D(here) + " 4 0",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2_AStepWithinOneAreaCallsNeitherAreaProc()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            map.Trace.Clear();
            Step(mover, EAST);
            Step(mover, NORTH);
            Assert.Empty(Lines(map.Trace, "area_exited"));
            Assert.Empty(Lines(map.Trace, "area_entered"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2_ForceMoveAcrossAnAreaBorderCallsAreaExitedThenTheDestinationEnteredThenAreaEnteredWithTheOldArea()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(3, 3, 1);
            var roomA = (area)here.loc;
            var roomB = MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_other>(there);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Force(mover, there));

            // doMove (atoms_movable.dm:1218-1224): oldloc.Exited, old_area.Exited, destination.Entered(src, oldloc) with
            // two arguments, destarea.Entered(src, old_area); the movement_dir is get_dir(src, destination) = NORTHEAST.
            Assert.Equal(new[]
            {
                "exited " + D(here) + " " + D(mover) + " 5 null",
                "area_exited " + D(roomA) + " " + D(mover) + " 5",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "area_entered " + D(roomB) + " " + D(mover) + " " + D(roomA),
                "moved " + D(mover) + " " + D(here) + " 0 1", // Moved(oldloc, NONE, TRUE, null)
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2_MoveToNullspaceCallsTheTurfsExitedThenTheAreasExitedBothWithNoDirection()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var roomA = (area)here.loc;
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Waiting.NoSleep(mover.moveToNullspace()));

            Assert.Null(mover.loc);
            Assert.Equal(new[]
            {
                "exited " + D(here) + " " + D(mover) + " 0 null",
                "area_exited " + D(roomA) + " " + D(mover) + " 0", // old_area.Exited(src, NONE) (:1243)
                "moved " + D(mover) + " " + D(here) + " 0 1",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2_ForceMoveOutOfNullspaceHasNoOldLocAndTheNewAreaIsToldItsOldAreaIsNull()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf there = locate(2, 2, 1);
            var roomA = (area)there.loc;
            var mover = Dm.@new<obj_test_movable_thing>(); // no loc: nullspace
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
            On(mover, COMSIG_MOVABLE_Z_CHANGED, "z_changed");
            map.Trace.Clear();

            AssertNumber(TRUE, Force(mover, there));

            // oldloc is null: no Exited calls, no ABSTRACT_EXITED; `destarea && old_area != destarea` is true for a null
            // old_area, so the area's Entered gets null as its old area (atoms_movable.dm:1223-1224). The z change from
            // "no turf" to the turf is reported to the mover.
            Assert.Equal(new[]
            {
                "entered " + D(there) + " " + D(mover) + " null null",
                "area_entered " + D(roomA) + " " + D(mover) + " null",
                "moved " + D(mover) + " null 0 1",
                "abstract_entered " + D(there) + " " + D(mover) + " null null",
                "z_changed " + D(mover) + " null " + D(there) + " 1",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2_AMoverMovedOutOfAContainerOntoAnotherAreaTellsTheAreaOfTheContainersTurf()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var roomA = (area)here.loc;
            var roomB = MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_other>(there);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var item = Dm.@new<obj_test_movable_thing>(box);
            map.Trace.Clear();

            Step(item, EAST); // oldarea = get_area(box) = the area of box's turf (atoms_movable.dm:737)

            Assert.Equal(new[]
            {
                "Enter " + D(there) + " " + D(item),
                "area_exited " + D(roomA) + " " + D(item) + " 4",
                "area_entered " + D(roomB) + " " + D(item) + " " + D(roomA),
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- T2b: step() never calls Move without a destination ----

    [Fact]
    public void T2b_StepWithDirectionZeroNeverCallsMoveOrCheckPulling()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            crate.loc = locate(3, 3, 1); // raw: the pull is now two tiles out of reach, and nothing has noticed
            map.Trace.Clear();

            // get_step(ref, 0) is the mover's own turf, so `t == ref.loc` returns 0 (move-chain.md E2).
            AssertNumber(0, Step(puller, 0));

            Assert.Empty(map.Trace); // no witness_move line
            Assert.Same(crate, puller.pulling); // check_pulling did not run, which Move's first statement would have done
            Assert.Null(puller.last_move);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Theory]
    [InlineData(1, 1, WEST)]
    [InlineData(1, 1, SOUTH)]
    [InlineData(3, 3, NORTH)]
    [InlineData(3, 3, EAST)]
    [InlineData(3, 3, NORTHEAST)]
    [InlineData(1, 1, SOUTHWEST)]
    [InlineData(3, 1, NORTHEAST)] // the diagonal target is off the map although the cell north of the mover is not
    public void T2b_StepOffTheMapEdgeNeverCallsMoveAndReturnsZero(int x, int y, int dir)
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_witness>(locate(x, y, 1));
            map.Trace.Clear();

            AssertNumber(0, Step(mover, dir));

            Assert.Empty(map.Trace);
            Assert.Same(locate(x, y, 1), mover.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2b_StepOffTheEdgeKeepsAPullThatMoveWithANullDestinationWouldHaveBroken()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            crate.loc = locate(3, 3, 1); // out of reach, unnoticed
            On(crate, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled");
            map.Trace.Clear();

            AssertNumber(0, Step(puller, WEST)); // the edge
            Assert.Same(crate, puller.pulling);
            Assert.Empty(map.Trace);

            // Move itself runs check_pulling(z_allowed = TRUE) BEFORE its `!newloc` bail (atoms_movable.dm:766-769): this is
            // what the BYOND-parity rule of step prevents, and the reason the test is not a no-op.
            AssertNumber(FALSE, Chain.Move(puller, null, WEST));
            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);
            Assert.Equal(new[]
            {
                "witness_move " + D(puller) + " null 8",
                "no_longer_pulled " + D(crate) + " " + D(puller) + " null null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T2b_StepOfAMoverInNullspaceNeverCallsMove()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_witness>();
            map.Trace.Clear();

            AssertNumber(0, Step(mover, EAST));

            Assert.Empty(map.Trace);
            Assert.Null(mover.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- what Move returns: TRUE, FALSE and null (3a, 3f, 3g) ----

    [Fact]
    public void MoveReturnsTrueOnlyForAMoveThatHappenedAndFalseForEveryRefusal()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(2, 2, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);

            AssertNumber(TRUE, Chain.Move(mover, locate(3, 2, 1), EAST));
            Assert.Same(locate(3, 2, 1), mover.loc);

            AssertNumber(FALSE, Chain.Move(mover, null, EAST)); // no destination (:770)

            ((turf_test_movable_floor)locate(2, 2, 1)).enter_result = FALSE;
            AssertNumber(FALSE, Chain.Move(mover, locate(2, 2, 1), WEST)); // Enter refused
            Assert.Same(locate(3, 2, 1), mover.loc);

            var vetoer = Chain.On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            ((turf_test_movable_floor)locate(2, 2, 1)).enter_result = TRUE;
            AssertNumber(FALSE, Chain.Move(mover, locate(2, 2, 1), WEST)); // PRE_MOVE refused
            Assert.Same(locate(3, 2, 1), mover.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_MoveToItsOwnLocReturnsNullNotFalseAndOnlyTheFirstDefinitionReturnsFalseForIt()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            map.Trace.Clear();

            // The second definition (:763) never assigns `.` when newloc == loc, so the call returns null...
            object result = Chain.Move(mover, here, NORTH);
            Assert.Null(result);
            AssertLines(new[] { "witness_move " + D(mover) + " " + D(here) + " 1" }, map.Trace); // ...and does not reach Moved

            // ...while the first definition (:687), reached through `..()`, starts with `. = FALSE`.
            object first = Waiting.NoSleep(mover.Move__d0(here, NORTH));
            AssertNumber(FALSE, first);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_AMoveToItsOwnLocStillRunsTheSecondDefinitionsTail()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.dir = SOUTH;
            mover.last_move = 7;
            On(mover, COMSIG_ATOM_DIR_CHANGE, "dir_change");
            map.Trace.Clear();

            Assert.Null(Chain.Move(mover, here, EAST, 3));

            // check_pulling ran, the glide override was applied (twice: early and late), last_move was set and the mover
            // turned, all for a move that did not happen (:763-868; move-chain.md 3a edge note).
            Assert.Equal(3f, mover.glide_size);
            AssertNumber(EAST, mover.last_move);
            Assert.Equal(EAST, mover.dir);
            AssertLines(new[] { "dir_change " + D(mover) + " " + D(SOUTH) + " " + D(EAST) + " null" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AFailedMoveClearsLastMoveAndAFailedMoveFromNullspaceReturnsFalse()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            AssertNumber(TRUE, Step(mover, EAST));
            AssertNumber(EAST, mover.last_move);

            ((turf_test_movable_floor)locate(3, 1, 1)).enter_result = FALSE;
            AssertNumber(FALSE, Step(mover, EAST));
            AssertNumber(0, mover.last_move); // :831-834

            var lost = Dm.@new<obj_test_movable_thing>(); // no loc: `!loc` (:770)
            AssertNumber(FALSE, Chain.Move(lost, locate(1, 1, 1), NORTH));
            Assert.Null(lost.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MoveWithoutADirectionWorksOutTheDirectionFromTheDestination()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted");
            map.Trace.Clear();

            // tg's own chain_pull_through_space calls alice.Move(turf) with no direction (:94): def2 takes the cardinal
            // branch for a null direction (`!(null & (null - 1))`) and def1 fills `direction ||= get_dir(src, newloc)`.
            AssertNumber(TRUE, Chain.Move(mover, locate(2, 1, 1)));
            Assert.Same(locate(2, 1, 1), mover.loc);
            // ATTEMPTED_MOVE is sent before the direction is worked out (:692 versus :698): it carries null.
            Assert.Equal("attempted " + D(mover) + " " + D(locate(2, 1, 1)) + " null null", map.Trace[0]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    private static object[] Items(object listOrAtom) => EngineTestHelpers.Items(listOrAtom);
}
