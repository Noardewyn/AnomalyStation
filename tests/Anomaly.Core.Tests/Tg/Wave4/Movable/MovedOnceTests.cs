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
/// RESOLVE_ACTIVE_MOVEMENT and SET_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm; move-chain.md sections 3e, 3g and 4): Moved
/// fires exactly once per movement, and BEFORE any movement that a handler starts from inside Entered, Exited, Moved or Bump.
/// T3 of the goldens, written from the DM and its re-entrancy design, not from the C#.
/// </summary>
public class MovedOnceTests
{
    // The MOVED lines of a trace ("moved <mover> <old_loc> <dir> <forced>").
    private static string[] MovedLines(EngineWorld map) => Lines(map.Trace, "moved");

    [Fact]
    public void AStepSendsOneMovedAndLeavesNoActiveMovementBehind()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            map.Trace.Clear();

            Step(mover, EAST);

            AssertLines(new[] { "witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1" }, Lines(map.Trace, "witness_moved"));
            Assert.Null(mover.active_movement);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovedIsCalledWithTheFiveArgumentsOfTheActiveMovementAndMomentumChangeTrue()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            map.Trace.Clear();

            Force(mover, locate(2, 2, 1));

            // SET_ACTIVE_MOVEMENT(oldloc, NONE, TRUE, null), then Moved(arglist(...)): the fifth argument falls back to its
            // default TRUE (move-chain.md section 4).
            Assert.Equal(new[] { "witness_moved " + D(mover) + " " + D(here) + " 0 1 null 1" }, Lines(map.Trace, "witness_moved"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveInsideEnteredResolvesTheOuterMovedBeforeTheNestedChainAndEachMovedFiresOnce()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(there, COMSIG_ATOM_ENTERED, "entered", act: (source, arrived) => Force((atom_movable)arrived, far));
            On(mover, COMSIG_ATOM_ENTERING, "entering");
            On(there, COMSIG_ATOM_EXITED, "there_exited");
            On(far, COMSIG_ATOM_ENTERED, "far_entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(far, mover.loc);
            Assert.Equal(new[]
            {
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null", // the handler starts here
                "moved " + D(mover) + " " + D(here) + " 4 0", // RESOLVE_ACTIVE_MOVEMENT at the top of doMove: the outer Moved first
                "there_exited " + D(there) + " " + D(mover) + " 4 null", // then the nested chain
                "far_entered " + D(far) + " " + D(mover) + " " + D(there) + " null",
                "entering " + D(mover) + " " + D(far) + " " + D(there) + " null", // the nested Entered's second signal
                "moved " + D(mover) + " " + D(there) + " 0 1",
                "entering " + D(mover) + " " + D(there) + " " + D(here) + " null", // the first Entered's second signal: stale state
            }, Signals(map));
            Assert.Equal(2, MovedLines(map).Length);
            Assert.Null(mover.active_movement);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveInsideTheOldAreasExitedFiresTheOuterMovedFirstAndThenTheStaleEnteredOfTheTurfAndTheArea()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 1, 1);
            var roomA = (area)here.loc;
            var roomB = MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_other>(there);
            var roomC = MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_third>(far);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            // The turf's Exited runs before the old area's, so a handler on it starts the nested move while Move is between
            // the two (atoms_movable.dm:747-749).
            On(here, COMSIG_ATOM_EXITED, "exited", act: (source, gone) => Force((atom_movable)gone, far));
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(far, mover.loc);
            // Move: here.Exited -> handler -> doMove: RESOLVE (the outer Moved), then the nested chain from there (area B)
            // to far (area C): old_area.Exited(B), destarea.Entered(C, B), the nested Moved. Back in def1 (its areas were
            // read before: A and B): oldarea.Exited(A), then the STALE newloc.Entered and newarea.Entered(B, A), for a mover
            // that is already at far.
            Assert.Equal(new[]
            {
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "moved " + D(mover) + " " + D(here) + " 4 0",
                "area_exited " + D(roomB) + " " + D(mover) + " 4",
                "area_entered " + D(roomC) + " " + D(mover) + " " + D(roomB),
                "moved " + D(mover) + " " + D(there) + " 0 1",
                "area_exited " + D(roomA) + " " + D(mover) + " 4",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "area_entered " + D(roomB) + " " + D(mover) + " " + D(roomA),
            }, map.Trace.Where(l => !l.StartsWith("Enter ")).ToList());
            Assert.Equal(2, MovedLines(map).Length);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveInsideMovedStartsACleanMovementBecauseTheActiveMovementIsPoppedBeforeTheCall()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            bool moved_once = false;
            On(mover, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) =>
            {
                if (moved_once) return;
                moved_once = true;
                Assert.Null(((atom_movable)source).active_movement); // popped before Moved(arglist(...)) (move-chain.md section 4)
                Force((atom_movable)source, far);
            });
            On(there, COMSIG_ATOM_EXITED, "there_exited");
            On(far, COMSIG_ATOM_ENTERED, "far_entered");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(far, mover.loc);
            Assert.Equal(new[]
            {
                "moved " + D(mover) + " " + D(here) + " 4 0", // the first Moved, in whose handler the nested move runs
                "there_exited " + D(there) + " " + D(mover) + " 4 null",
                "far_entered " + D(far) + " " + D(mover) + " " + D(there) + " null",
                "moved " + D(mover) + " " + D(there) + " 0 1",
            }, Signals(map));
            Assert.Null(mover.active_movement);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AStepInsideExitedResolvesTheOuterMovedAtTheTopOfTheNestedMove()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), north = locate(2, 2, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            bool stepped = false;
            On(here, COMSIG_ATOM_EXITED, "exited", act: (source, gone) =>
            {
                if (stepped) return;
                stepped = true;
                Step((atom_movable)gone, NORTH); // from `there` to (2, 2): a full Move from inside the Exited
            });
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(north, mover.loc);
            // The nested Move's RESOLVE_ACTIVE_MOVEMENT (def1 :693) flushes the first movement's Moved, so the Moved of
            // here -> there fires before the Moved of there -> (2, 2), and each exactly once.
            Assert.Equal(new[]
            {
                "moved " + D(mover) + " " + D(here) + " 4 0",
                "moved " + D(mover) + " " + D(there) + " 1 0",
            }, MovedLines(map));
            Assert.Equal(new[]
            {
                "witness_move " + D(mover) + " " + D(there) + " 4", // the outer Move
                "witness_move " + D(mover) + " " + D(north) + " 1", // the nested one, from inside Exited
                "witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1", // the outer movement's Moved, flushed by the nested Move
                "witness_moved " + D(mover) + " " + D(there) + " 1 0 null 1",
            }, map.Trace.Where(l => l.StartsWith("witness_")).ToList());
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // tg: modules/unit_tests/movement_order_sanity.dm, the order of the signals of a qdel inside Entered (move-chain.md 3g).
    [Fact]
    public void AQdelInsideEnteredSendsTheFlushedOuterMovedThenTheNullspaceMovedAndStepStillReturnsTrue()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            // The mover's own signal listeners are gone once it is deleted (datum/Destroy clears them, datum.dm:160), before
            // moveToNullspace runs: the witness hooks Moved itself, as tg's own test object does.
            On(there, COMSIG_ATOM_ENTERED, "interceptor", act: (source, arrived) => qdel(arrived));
            On(there, COMSIG_ATOM_EXITED, "there_exited");
            On(there, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.True(QDELETED(mover));
            Assert.Null(mover.loc);
            Assert.Null(mover.active_movement);
            Assert.Equal(new[]
            {
                "witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1", // flushed by doMove(null) while loc is still `there`
                "there_exited " + D(there) + " " + D(mover) + " 0 null", // moveToNullspace's Exited (NONE)
                "witness_moved " + D(mover) + " " + D(there) + " 0 1 null 1", // the nullspace Moved
                "abstract_exited " + D(there) + " " + D(mover) + " 0 null",
            }, map.Trace.Where(l => l.StartsWith("witness_moved") || l.StartsWith("there_exited") || l.StartsWith("abstract_exited")).ToList());
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AQdelInsideExitedOfTheOldTurfStillEndsInTheOuterMovesRemainingCalls()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            On(here, COMSIG_ATOM_EXITED, "exited", act: (source, gone) => qdel(gone));
            On(there, COMSIG_ATOM_ENTERED, "entered");
            map.Trace.Clear();

            // Def1 has no QDELETED check after the point of no return (:747-759): the stale Entered of `there` is still
            // sent for a mover that is already in nullspace (the doMove(null) inside qdel flushed the outer Moved).
            Assert.True(Dm.truthy(Step(mover, EAST)));

            Assert.True(QDELETED(mover));
            Assert.Null(mover.loc);
            Assert.Equal(1, Count(map.Trace, "entered"));
            Assert.Equal(2, Count(map.Trace, "witness_moved"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveToTheSameLocFiresMovedOnceWithNoExitedOrEntered()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(here, COMSIG_ATOM_ENTERED, "entered");
            On(mover, COMSIG_MOVABLE_MOVED, "moved");
            On(here, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
            On(here, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
            map.Trace.Clear();

            AssertNumber(TRUE, Force(mover, here)); // `same_loc`: no events (:1197), but Moved fires (do_after relies on its guard)

            Assert.Equal(new[]
            {
                "moved " + D(mover) + " " + D(here) + " 0 1",
                "abstract_exited " + D(here) + " " + D(mover) + " 0 null",
                "abstract_entered " + D(here) + " " + D(mover) + " " + D(here) + " null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveBreaksAPullOfTheMovedThingButNotWhenItIsZMoving()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling");
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            map.Trace.Clear();

            // doMove: `if(pulledby && !currently_z_moving) pulledby.stop_pulling()` (:1182).
            Force(crate, locate(2, 2, 1));
            Assert.Null(puller.pulling);
            AssertLines(new[] { "no_longer_pulling " + D(puller) + " " + D(crate) + " null null" }, map.Trace);

            // "zMove already handles whether a pull from another movable should be broken": the mover keeps it.
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            crate.currently_z_moving = CURRENTLY_Z_MOVING_GENERIC;
            Force(crate, locate(1, 2, 1));
            Assert.Same(crate, puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnActiveMovementLeftPendingIsResolvedFirstByTheNextMoveOfAnyKind()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            foreach (string kind in new[] { "Move", "doMove", "abstract_move" })
            {
                var mover = Dm.@new<obj_test_movable_witness>(here);
                mover.active_movement = Dm.list(here, NONE, TRUE, null); // as a refused move leaves it (T8)
                map.Trace.Clear();

                switch (kind)
                {
                    case "Move": Chain.Move(mover, there, EAST); break;
                    case "doMove": Force(mover, there); break;
                    default: Waiting.NoSleep(mover.abstract_move(there)); break;
                }

                // The pending Moved comes first (the same one for each of the three entry points), then the real one.
                string[] witness = Lines(map.Trace, "witness_moved");
                Assert.Equal(2, witness.Length);
                Assert.Equal("witness_moved " + D(mover) + " " + D(here) + " 0 1 null 1", witness[0]);
                Assert.Null(mover.active_movement);
            }
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // The Moved lines and the other signals a trace holds, without the fixture floor's Enter records and the area lines.
    private static List<string> Signals(EngineWorld map) =>
        map.Trace.Where(l => !l.StartsWith("Enter ") && !l.StartsWith("area_") && !l.StartsWith("witness_")).ToList();
}
