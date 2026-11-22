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
/// The waiting model against the Move chain (docs/ai/specs/waiting-model.md; move-chain.md 2 E6 and hazard 8): the chain is
/// made of DmTask slots, so a chain that does not sleep completes synchronously, an override that sleeps suspends the
/// Move (the caller waits for it, with the movement half done), and an override with <c>set waitfor = FALSE</c>
/// (/area/Entered, areas.dm:582) never blocks it.
/// </summary>
public class MoveWaitingTests
{
    // ---- a waitfor = FALSE Entered that sleeps does not block Move ----

    [Fact]
    public void AnAreaEnteredThatSleepsWithWaitforFalseDoesNotBlockMoveOrReportAnError()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var sleepy = MovableWorld.NewArea<area_misc_testroom_test_movable_room_sleepy>(there);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            map.Trace.Clear();

            object result = Waiting.NoSleep(step(mover, EAST)); // NoSleep reports a runtime if the chain slept

            AssertNumber(TRUE, result);
            Assert.Same(there, mover.loc);
            Assert.Null(mover.active_movement);
            // the area's Entered started, Move went on to resolve its Moved, and the area's body is still asleep
            AssertLines(new[]
            {
                "sleepy_area_entered start " + D(mover),
                "witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1",
            }, map.Trace.Where(l => !l.StartsWith("Enter ") && !l.StartsWith("witness_move ")));
            MovableWorld.AssertNoRuntimes(map);

            map.World.Step(25); // 10 ds of sleep at 0.5 ds per tick
            Assert.Equal("sleepy_area_entered woke " + D(mover), map.Trace[^1]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AForceMoveIntoASleepyAreaAlsoCompletesAtOnceAndTheBodyFinishesLater()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf there = locate(3, 3, 1);
            MovableWorld.NewArea<area_misc_testroom_test_movable_room_sleepy>(there);
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            map.Trace.Clear();

            AssertNumber(TRUE, Force(mover, there));
            Assert.Equal(new[] { "sleepy_area_entered" }, Kinds(map.Trace.Where(l => l.StartsWith("sleepy_"))));
            Assert.Equal(1, Count(map.Trace, "sleepy_area_entered"));
            map.World.Step(25);
            Assert.Equal(2, Count(map.Trace, "sleepy_area_entered"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- an Exited that sleeps suspends the Move with the movement half done ----

    [Fact]
    public void ASleepingExitedSuspendsMoveWithLocChangedAndMovedPendingAndItsCallerGetsTheResultWhenItWakes()
    {
        var map = MovableWorld.Create(floor: typeof(turf_test_movable_floor_sleepy_exit));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            On(there, COMSIG_ATOM_ENTERED, "entered");
            map.Trace.Clear();

            DmTask<object> task = step(mover, EAST);

            // Past the point of no return the mover is already at `there` and the Moved is pending, the chain is asleep
            // inside here.Exited (:747); nothing after the Exited has run yet.
            Assert.False(task.IsCompleted);
            Assert.Same(there, mover.loc);
            Assert.NotNull(mover.active_movement);
            Assert.Equal(new[] { "sleepy_exited start " + D(mover) }, map.Trace.Where(l => l.StartsWith("sleepy_") || l.StartsWith("entered") || l.StartsWith("witness_moved")));

            map.World.Step(15); // 5 ds = 10 ticks

            Assert.True(task.IsCompleted);
            Assert.True(task.TryGetResult(out object result));
            AssertNumber(TRUE, result);
            Assert.Null(mover.active_movement);
            AssertLines(new[]
            {
                "sleepy_exited start " + D(mover),
                "sleepy_exited woke " + D(mover),
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1",
            }, map.Trace.Where(l => l.StartsWith("sleepy_") || l.StartsWith("entered") || l.StartsWith("witness_moved")));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AMovementStartedWhileMoveSleepsInExitedResolvesTheSuspendedMovesMovedFirst()
    {
        var map = MovableWorld.Create(floor: typeof(turf_test_movable_floor_sleepy_exit));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            DmTask<object> task = step(mover, EAST);
            Assert.False(task.IsCompleted);
            map.Trace.Clear();

            // A forceMove while the first move is half done: doMove's RESOLVE_ACTIVE_MOVEMENT sends the pending Moved of the
            // suspended Move first (the DM is the same whether or not the first Move is awake). far is a sleepy floor too, and
            // there.Exited sleeps in the nested chain; the nested move is suspended as well.
            DmTask<object> second = mover.forceMove(far);

            Assert.Equal("witness_moved " + D(mover) + " " + D(here) + " 4 0 null 1", Lines(map.Trace, "witness_moved")[0]);
            map.World.Step(40);
            Assert.True(task.IsCompleted);
            Assert.True(second.IsCompleted);
            Assert.Same(far, mover.loc);
            Assert.Null(mover.active_movement);
            // the first Move's remaining calls ran after the nested movement (the stale Entered of `there`), and exactly two
            // Moved fired: one per movement, the pending one first
            Assert.Equal(2, Count(map.Trace, "witness_moved"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AChainThatNeverSleepsCompletesBeforeTheCallReturnsEvenWhenAwaitedThroughTheSlots()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));

            DmTask<object> task = step(mover, EAST);
            Assert.True(task.IsCompleted); // no sleep anywhere: `await` costs nothing (waiting-model.md 3)
            Assert.True(task.TryGetResult(out object result));
            AssertNumber(TRUE, result);

            DmTask<object> forced = mover.forceMove(locate(3, 3, 1));
            Assert.True(forced.IsCompleted);
            DmTask<object> nowhere = mover.moveToNullspace();
            Assert.True(nowhere.IsCompleted);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // DEVIATION(DV5): a synchronous proc (Destroy) that calls a proc that sleeps goes on with a default value, the callee runs on
    // detached and a runtime is reported; DM would block Destroy at the sleep.
    [Fact]
    public void DV5_DeletingAThingWhoseOldTurfSleepsInExitedReportsARuntimeFinishesDestroyAndTheMoveCompletesLater()
    {
        var map = MovableWorld.Create(floor: typeof(turf_test_movable_floor_sleepy_exit));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var thing = Dm.@new<obj_test_movable_witness>(here);
            thing.become_hearing_sensitive();
            map.Trace.Clear();

            qdel(thing);

            Assert.True(QDELETED(thing));
            Assert.Contains("slept", Assert.Single(map.Runtimes).Message); // "proc slept inside a no-sleep context"
            Assert.Equal(INVISIBILITY_ABSTRACT, thing.invisibility); // the rest of Destroy ran
            Assert.Null(thing.important_recursive_contents);         // LAZYNULL after the (detached) move: Destroy did not wait
            Assert.Equal(0, Count(map.Trace, "witness_moved"));      // the pending Moved of doMove(null) is still waiting
            map.Runtimes.Clear();

            map.World.Step(15);

            Assert.Equal(1, Count(map.Trace, "witness_moved")); // the detached remainder of the move finished: RESOLVE_ACTIVE_MOVEMENT
            Assert.Null(thing.loc);
            Assert.Empty(map.Runtimes);
        }
    }

    [Fact]
    public void ARuntimeInsideAnEnteredOverrideIsCapturedInTheMoveTaskAndRethrownToItsCaller()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            On(there, COMSIG_ATOM_ENTERED, "entered", act: (source, arrived) => throw new DmRuntimeException("boom in a handler"));

            // A signal handler's runtime is an isolation boundary (rules 9.2 item 3): the chain goes on, the runtime is reported.
            AssertNumber(TRUE, Step(mover, EAST));
            Assert.Same(there, mover.loc);
            Assert.Single(map.Runtimes);
            Assert.Contains("boom in a handler", map.Runtimes[0].Message);
            map.Runtimes.Clear();
        }
    }
}
