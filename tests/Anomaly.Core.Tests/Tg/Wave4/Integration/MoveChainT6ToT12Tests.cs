using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// T6 to T9, T11 and T12 of docs/ai/specs/move-chain.md section 11 on the toy room, against tg's real types: the diagonal Move
/// (3f), the contents order and the snapshot Enter iterates (E1, 3b), the cyclic loc refusal of doMove (3e), abstract_move (3e),
/// the pull that follows its puller (3a step 5) and movement_detector across container nesting (section 6). T5 is the ported tg
/// unit test movement_order_sanity (UnitTests/movement_order_sanity.cs) and T10 the do_after tests (DoAfterToyRoomTests).
/// </summary>
public class MoveChainT6ToT12Tests
{
    // ---- T6: diagonal order ----

    [Fact]
    public void T6_ANortheastStepIsTheNorthStepThenTheEastStepEachWithItsOwnFullChain()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            var diagonal_flags = new List<float>();
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3, act: _ => diagonal_flags.Add(m.moving_diagonally));
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, NORTHEAST));

            AssertTrace(new[]
            {
                // no ATTEMPTED_MOVE for the diagonal itself: def2 splits it before def1 (atoms_movable.dm:771-829)
                "attempted mover (3,4) 1",
                "pre_dir mover 2 1",
                "moved mover (3,3) 1 0",
                "attempted mover (4,4) 4",
                "pre_dir mover 1 4",
                "moved mover (3,4) 4 0",
            }, Trace(room.World));
            Assert.Equal(new float[] { FIRST_DIAG_STEP, SECOND_DIAG_STEP }, diagonal_flags);   // seen by the handlers of the two Moved calls
            Assert.Same(locate(4, 4, 1), m.loc);
            Assert.Equal(EAST, m.dir);
            AssertNumber(EAST, m.last_move);   // the second cardinal step's def2 tail; the diagonal returns before its own
            Assert.Equal(0f, m.moving_diagonally);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T6_WhenTheNorthStepIsBlockedTheEastStepComesFirstAndNorthSecond()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            var north = Thing("north", locate(3, 4, 1), dense: true);
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, NORTHEAST));

            AssertTrace(new[]
            {
                "attempted mover (3,4) 1",
                "pre_dir mover 2 1",           // the failed step turned the mover
                "bump mover north",
                "attempted mover (4,3) 4",     // `else if (moving_diagonally && step(src, EAST))`
                "pre_dir mover 1 4",
                "moved mover (3,3) 4 0",
                "attempted mover (4,4) 1",     // then the other half, NORTH
                "pre_dir mover 4 1",
                "moved mover (4,3) 1 0",
            }, Trace(room.World));
            Assert.Same(locate(4, 4, 1), m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T6_TgQuirkADiagonalMoveWhoseBothFirstStepsFailReturnsNullNotFalse()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            Thing("north", locate(3, 4, 1), dense: true);
            Thing("east", locate(4, 3, 1), dense: true);
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
            Trace(room.World).Clear();

            object result = Step(m, NORTHEAST);

            Assert.Null(result);   // `.` is only assigned in the success branches (move-chain.md 3f)
            AssertTrace(new[] { "attempted mover (3,4) 1", "attempted mover (4,3) 4" }, Trace(room.World));
            Assert.Same(a, m.loc);
            Assert.Equal(0f, m.moving_diagonally);
            Assert.Equal(EAST, m.dir);   // the last failed attempt turned it
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T6_WhenTheSecondStepIsBlockedTheDiagonalReturnsFalseAndTheMoverTurnsBackToTheFirstStepsDirection()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            m.dir = SOUTH;
            var east_of_north = Thing("blocker", locate(4, 4, 1), dense: true);
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            object result = Step(m, NORTHEAST);

            AssertNumber(FALSE, result);   // the second step's result, FALSE
            AssertTrace(new[]
            {
                "attempted mover (3,4) 1",
                "pre_dir mover 2 1",
                "attempted mover (4,4) 4",
                "pre_dir mover 1 4",
                "bump mover blocker",
                "pre_dir mover 4 1",           // `if(!. && set_dir_on_move && update_dir) setDir(first_step_dir)`
            }, Trace(room.World));
            Assert.Same(locate(3, 4, 1), m.loc);   // it did get the first step
            Assert.Equal(NORTH, m.dir);
            Assert.Equal(0f, m.moving_diagonally);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T6_AForceMoveInsideTheFirstStepsMovedHandlerAbortsTheSecondHalfAndTheDiagonalReturnsNull()
    {
        // "The `&& moving_diagonally` checks are so that a forceMove taking place due to a Crossed, Bumped, etc. call will
        // interrupt the second half of the diagonal movement" (atoms_movable.dm:775-778)
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);
            bool done = false;
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3, act: _ =>
            {
                if (done)
                    return;
                done = true;
                Force(m, locate(6, 6, 1));
            });
            Trace(room.World).Clear();

            object result = Step(m, NORTHEAST);

            Assert.Null(result);
            AssertTrace(new[]
            {
                "attempted mover (3,4) 1",
                "moved mover (3,3) 1 0",
                "moved mover (3,4) 0 1",    // the forceMove's own Moved, from where the first step left it
            }, Trace(room.World));
            Assert.Same(locate(6, 6, 1), m.loc);   // no second half
            Assert.Equal(0f, m.moving_diagonally);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T7: contents order and the snapshot Enter iterates ----

    [Fact]
    public void T7_EnterIteratesASnapshotOfTheContentsSoAThingArrivingDuringTheLoopIsNotCrossed()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var m = Thing("mover", a);
            var o1 = Thing("o1", b);
            var o2 = Thing("o2", b);
            var late = Thing("late", null, dense: true);
            On(late, COMSIG_MOVABLE_CROSS, "cross_late", 1);
            On(o1, COMSIG_MOVABLE_CROSS, "cross_o1", 1, act: _ => Force(late, b));   // arrives on b while Enter is looping
            On(o2, COMSIG_MOVABLE_CROSS, "cross_o2", 1);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));

            AssertTrace(new[] { "cross_o1 o1 mover", "cross_o2 o2 mover" }, Trace(room.World));   // `late` was not in the snapshot
            Assert.Equal(new object[] { o1, o2, late, m }, Items(b.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T7_TheContentsOfATurfListThingsInTheOrderTheyArrivedAndAnItemKeepsItsPlaceInAContainer()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var first = Thing("first", a);
            var second = Thing("second", b);
            var third = Thing("third", a);
            Force(first, b);
            Force(third, b);
            Assert.Equal(new object[] { second, first, third }, Items(b.contents));
            Assert.Empty(Items(a.contents));
            var box = Thing("box", b);
            Force(second, box);
            Force(first, box);
            Assert.Equal(new object[] { second, first }, Items(box.contents));
            Assert.Equal(new object[] { third, box }, Items(b.contents));
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T8: the cyclic loc refusal ----

    [Fact]
    public void T8_ForceMovingAContainerIntoItsOwnContentsIsRefusedWithAStackTraceAndLeavesAPendingMoved()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var box = Thing("box", a);
            var item = Thing("item", box);
            On(box, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(item, COMSIG_ATOM_ENTERED, "entered_item", 2);
            Trace(room.World).Clear();

            object result = Force(box, item);   // BYOND silently refuses: loc is left alone (E1)

            AssertNumber(FALSE, result);
            Assert.Same(a, box.loc);
            Assert.Same(box, item.loc);
            Assert.Empty(Trace(room.World));
            var runtime = Assert.Single(room.Runtimes);
            Assert.Contains("was rejected by BYOND, possibly due to cyclic contents", runtime.Message);
            Assert.NotNull(box.active_movement);   // TgQuirk: SET_ACTIVE_MOVEMENT ran before the refusal and nothing resolved it

            // the next movement resolves the pending Moved first, after its ATTEMPTED_MOVE
            On(box, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            AssertNumber(TRUE, Step(box, EAST));
            AssertTrace(new[]
            {
                "attempted box (4,3) 4",
                "moved box (3,3) 0 1 null 1",   // the stale one: forced = TRUE, no direction
                "exited (3,3) box 4",
                "moved box (3,3) 4 0 null 1",
            }, Trace(room.World));
            Assert.Same(b, box.loc);
            Assert.Null(box.active_movement);
        }
    }

    [Fact]
    public void T8_ForceMovingAMovableIntoItselfIsRefusedTheSameWay()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1);
            var m = Thing("mover", a);

            AssertNumber(FALSE, Force(m, m));

            Assert.Same(a, m.loc);
            Assert.Single(room.Runtimes);
        }
    }

    [Fact]
    public void T8_PuttingAnItemIntoABoxAndBackOutIsNotARefusal()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var box = Thing("box", locate(3, 3, 1));
            var item = Thing("item", locate(3, 3, 1));

            AssertNumber(TRUE, Force(item, box));   // an item into a box is fine
            AssertNumber(TRUE, Force(item, locate(3, 3, 1)));
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T9: abstract_move ----

    [Fact]
    public void T9_AbstractMoveOfAnObjSetsLocAndCallsMovedWithForcedAndNoMomentumAndNothingElse()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var annex = Dm.@new<area_misc_testroom>();   // a second testroom for the other end, so a move changes area too
            annex.name = "annex";
            turf a = locate(3, 3, 1), b = locate(5, 4, 1);
            b.change_area((area)b.loc, annex);
            var m = Thing("mover", a);
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            On(a, COMSIG_ATOM_EXIT, "exit", 2);
            On(a, COMSIG_ATOM_EXITED, "exited", 2);
            On(b, COMSIG_ATOM_ENTERED, "entered", 3);
            On(room.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            On(annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            On(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            Trace(room.World).Clear();

            Waiting.NoSleep(m.abstract_move(b));

            AssertTrace(new[]
            {
                "moved mover (3,3) 5 1 null 0",            // Moved(old_loc, direction, TRUE, momentum_change = FALSE)
                "abstract_exited (3,3) mover 5",
                "abstract_entered (5,4) mover (3,3) null",
            }, Trace(room.World));
            Assert.Same(b, m.loc);
            Assert.Equal(new object[] { m }, Items(b.contents));
            Assert.Empty(Items(a.contents));
            Assert.Null(m.active_movement);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T9_AbstractMoveFromNullspaceAndToNullspaceSendsOnlyTheHalfThatHasALoc()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf b = locate(4, 4, 1);
            var m = Thing("mover", null);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 5);
            On(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            On(b, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            Trace(room.World).Clear();

            Waiting.NoSleep(m.abstract_move(b));
            Waiting.NoSleep(m.abstract_move(null));

            AssertTrace(new[]
            {
                "moved mover null 0 1 null 0",
                "abstract_entered (4,4) mover null null",
                "moved mover (4,4) 0 1 null 0",
                "abstract_exited (4,4) mover 0",
            }, Trace(room.World));
            Assert.Null(m.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T11: a pull follows its puller ----

    private static mob_living Puller(TgToyRoom room, turf where)
    {
        var puller = Dm.@new<mob_living>(where);
        puller.name = "puller";
        return puller;
    }

    [Fact]
    public void T11_AStepOfThePullerMovesTheCrateIntoThePullersOldTurfRightAfterThePullersOwnMoved()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf behind = locate(3, 3, 1), here = locate(4, 3, 1), ahead = locate(5, 3, 1);
            mob_living puller = Puller(room, here);
            var crate = Thing("crate", behind);
            // tg's /mob/living/start_pulling (living.dm:392, pulling 2026-10-15) returns nothing for a pulled object and sends
            // COMSIG_LIVING_START_PULL, not the movable's COMSIG_ATOM_START_PULL
            On(puller, COMSIG_LIVING_START_PULL, "start_pull", 3);
            Assert.Null(Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE)));
            Assert.Same(crate, puller.pulling);
            Assert.Same(puller, crate.pulledby);
            object from_pull_during_move = null;
            On(puller, COMSIG_MOVABLE_MOVED, "moved_puller", 3);
            On(crate, COMSIG_MOVABLE_MOVED, "moved_crate", 3, act: _ => from_pull_during_move = crate.moving_from_pull);
            On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(puller, EAST));

            AssertTrace(new[]
            {
                "moved_puller puller (4,3) 4 0",
                "moved_crate crate (3,3) 4 0",    // move_from_pull: crate.Move(the puller's old turf, get_dir(crate, it))
            }, Trace(room.World));
            Assert.Same(ahead, puller.loc);
            Assert.Same(here, crate.loc);
            Assert.Same(puller, from_pull_during_move);   // moving_from_pull is set for the crate's own Move...
            Assert.Null(crate.moving_from_pull);          // ...and cleared after
            Assert.Same(crate, puller.pulling);           // the pull survived the move (check_pulling: distance 1)
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T11_APullerThatWasMovedFarAwayLosesTheCrateAtItsNextMoveBeforeAnythingFollows()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            mob_living puller = Puller(room, locate(4, 3, 1));
            var crate = Thing("crate", locate(3, 3, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            // a living mob's forceMove lets go at once (living.dm:1970, pulling 2026-10-15): abstract_move moves it without a check
            Waiting.NoSleep(puller.abstract_move(locate(6, 6, 1)));
            Assert.Same(crate, puller.pulling);   // the move did not check the pull (the next Move does)
            On(crate, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled", 1);
            On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling", 1);
            On(puller, COMSIG_MOVABLE_MOVED, "moved_puller", 3);
            On(crate, COMSIG_MOVABLE_MOVED, "moved_crate", 3);
            Trace(room.World).Clear();

            Step(puller, WEST);

            AssertTrace(new[]
            {
                "no_longer_pulled crate puller",          // check_pulling(z_allowed = TRUE) at the top of Move (:766)
                "no_longer_pulling puller crate",
                "moved_puller puller (6,6) 8 0",
            }, Trace(room.World));
            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);
            Assert.Same(locate(3, 3, 1), crate.loc);
            Assert.Empty(room.Runtimes);
        }
    }

    // ---- T12: movement_detector across container nesting ----

    private static bool Hooked(datum d) => Dm.truthy(d._listen_lookup?[COMSIG_MOVABLE_MOVED]);

    [Fact]
    public void T12_TheDetectorFollowsTheTrackedItemThroughTwoContainersAndRehooksAsItIsTakenOut()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var outer = Thing("outer", a);
            var inner = Thing("inner", outer);
            var item = Thing("item", inner);
            var recorder = Dm.@new<datum_test_integration_recorder>();
            var detector = Dm.@new<datum_movement_detector>(item, Dm.@new<datum_callback>(recorder, nameof(datum_test_integration_recorder.on_tracked_moved)));
            Assert.True(Hooked(item) && Hooked(inner) && Hooked(outer));   // track(): every movable up to the turf
            Trace(room.World).Clear();

            // the outer box takes a step: reported once, with the box as mover and the turf as oldloc
            AssertNumber(TRUE, Step(outer, EAST));
            AssertTrace(new[] { "tracked item outer (3,3) 4" }, Trace(room.World));
            Trace(room.World).Clear();

            // the inner box is taken out: the outer one is no longer between the item and the turf, so it is unhooked
            Force(inner, b);
            AssertTrace(new[] { "tracked item inner outer 0" }, Trace(room.World));
            Assert.True(Hooked(item) && Hooked(inner));
            Assert.False(Hooked(outer));
            Trace(room.World).Clear();

            // the item is taken out last: the inner box is unhooked, only the item stays hooked
            Force(item, b);
            AssertTrace(new[] { "tracked item item inner 0" }, Trace(room.World));
            Assert.True(Hooked(item));
            Assert.False(Hooked(inner));
            Trace(room.World).Clear();

            // and a plain step of the item is reported again
            Step(item, EAST);
            AssertTrace(new[] { "tracked item item (4,3) 4" }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void T12_ADetectorOfAMoverThatIsPutInsideAContainerRehooksTheNewChainAndStopsWhenDestroyed()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), b = locate(4, 3, 1);
            var box = Thing("box", b);
            var item = Thing("item", a);
            var recorder = Dm.@new<datum_test_integration_recorder>();
            var detector = Dm.@new<datum_movement_detector>(item, Dm.@new<datum_callback>(recorder, nameof(datum_test_integration_recorder.on_tracked_moved)));
            Assert.True(Hooked(item));
            Assert.False(Hooked(box));
            Trace(room.World).Clear();

            Force(item, box);   // the item enters the box: the item's own Moved; oldloc is a turf, tracked.loc is the box now
            AssertTrace(new[] { "tracked item item (3,3) 0" }, Trace(room.World));
            Assert.True(Hooked(box));    // re-registered along mover.loc with override
            Trace(room.World).Clear();

            Step(box, WEST);
            AssertTrace(new[] { "tracked item box (4,3) 8" }, Trace(room.World));
            Trace(room.World).Clear();

            qdel(detector);   // Destroy: untrack() unhooks the whole chain
            Assert.False(Hooked(item));
            Assert.False(Hooked(box));
            Step(box, WEST);
            Assert.Empty(Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }
}
