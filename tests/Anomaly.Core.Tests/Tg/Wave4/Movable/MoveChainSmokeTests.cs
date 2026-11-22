using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>
/// Smoke tests of the translated Move chain (game/atoms_movable.dm, package A of wave 4) on fixture turfs, written from
/// tg's code and docs/ai/specs/move-chain.md section 3. The independent test-writer and W4.M (Integration/) write the goldens (T1-T12).
/// </summary>
public class MoveChainSmokeTests
{
    private static string D(object value) => EngineTrace.D(value);

    // The signal lines of the trace: without the fixture floors' Enter records, and without what an area sends (written
    // when the areas still took /atom's Entered and Exited; /area's own, areas.dm:581-606, send other signals).
    private static List<string> Signals(EngineWorld map) =>
        map.Trace.Where(l => !l.StartsWith("Enter ") && !l.Contains("area_")).ToList();

    // Listens to every signal of the cardinal chain (move-chain.md 3a) on the mover and the two turfs.
    private static void ListenToTheChain(atom_movable mover, turf here, turf there)
    {
        datum_test_movable_listener.On(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted");
        datum_test_movable_listener.On(here, COMSIG_ATOM_EXIT, "exit");
        datum_test_movable_listener.On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move");
        datum_test_movable_listener.On(here, COMSIG_ATOM_EXITED, "exited");
        datum_test_movable_listener.On(mover, COMSIG_ATOM_EXITING, "exiting");
        datum_test_movable_listener.On(there, COMSIG_ATOM_ENTERED, "entered");
        datum_test_movable_listener.On(mover, COMSIG_ATOM_ENTERING, "entering");
        datum_test_movable_listener.On(mover, COMSIG_MOVABLE_MOVED, "moved");
        datum_test_movable_listener.On(here, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
        datum_test_movable_listener.On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
    }

    [Fact]
    public void AStepRunsTheCardinalChainInTgsOrder()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);
            map.Trace.Clear();

            object result = Waiting.NoSleep(step(mover, EAST));

            Assert.True(Dm.eq(result, TRUE));
            Assert.Same(there, mover.loc);
            Assert.Equal(EAST, mover.dir);
            Assert.True(Dm.eq(mover.last_move, EAST));
            Assert.Null(mover.active_movement);
            Assert.Equal(new[]
            {
                "attempted " + D(mover) + " " + D(there) + " 4 null",
                "exit " + D(here) + " " + D(mover) + " 4 null",
                "Enter " + D(there) + " " + D(mover),
                "pre_move " + D(mover) + " " + D(there) + " null null",
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "exiting " + D(mover) + " " + D(here) + " 4 null",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "entering " + D(mover) + " " + D(there) + " " + D(here) + " null",
                "moved " + D(mover) + " " + D(here) + " 4 0",
                "abstract_exited " + D(here) + " " + D(mover) + " 4 null",
                "abstract_entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MoveReturnsNullForItsOwnLocAndFalseWithoutADestination()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            // move-chain.md 3a edge: newloc == loc skips the first definition, `.` is never assigned (null), and the
            // second definition's tail still runs (last_move, setDir).
            object same = Waiting.NoSleep(mover.Move(here, NORTH));
            Assert.Null(same);
            Assert.True(Dm.eq(mover.last_move, NORTH));
            Assert.Equal(NORTH, mover.dir);

            object nowhere = Waiting.NoSleep(mover.Move(null, NORTH));
            Assert.NotNull(nowhere);
            Assert.True(Dm.eq(nowhere, FALSE));
            Assert.Empty(map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnExitVetoTurnsTheMoverButLeavesItWhereItWas()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);
            datum_test_movable_listener.On(here, COMSIG_ATOM_EXIT, "veto", COMPONENT_ATOM_BLOCK_EXIT);
            map.Trace.Clear();

            object result = Waiting.NoSleep(step(mover, EAST));

            Assert.True(Dm.eq(result, FALSE));
            Assert.NotNull(result);
            Assert.Same(here, mover.loc);
            Assert.Equal(EAST, mover.dir); // setDir comes before Exit (move-chain.md 3a, step 5)
            Assert.True(Dm.eq(mover.last_move, 0)); // the second definition's bail (:831-834)
            Assert.Equal(new[] { "attempted", "exit", "veto" }, map.Trace.Select(l => l.Split(' ')[0]));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AFailedEnterAndAPreMoveVetoBothStopTheMoveAfterEnter()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var there = (turf_test_movable_floor)locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);

            there.enter_result = FALSE;
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(step(mover, EAST)), FALSE));
            Assert.Equal(new[] { "attempted", "exit", "Enter" }, map.Trace.Select(l => l.Split(' ')[0]));

            there.enter_result = TRUE;
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_PRE_MOVE, "veto", COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(step(mover, EAST)), FALSE));
            Assert.Equal(new[] { "attempted", "exit", "Enter", "pre_move", "veto" }, map.Trace.Select(l => l.Split(' ')[0]));
            Assert.Same(here, mover.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ForceMoveSkipsExitEnterAndPreMoveAndItsMovedIsForced()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);
            map.Trace.Clear();

            object result = Waiting.NoSleep(mover.forceMove(there));

            Assert.True(Dm.eq(result, TRUE));
            Assert.Same(there, mover.loc);
            Assert.Equal(new[]
            {
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "exiting " + D(mover) + " " + D(here) + " 4 null",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
                "entering " + D(mover) + " " + D(there) + " " + D(here) + " null",
                "moved " + D(mover) + " " + D(here) + " 0 1", // Moved(oldloc, NONE, TRUE, null) (:1176)
                "abstract_exited " + D(here) + " " + D(mover) + " 0 null",
                "abstract_entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
            }, map.Trace);

            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(mover.forceMove(null)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MoveToNullspaceExitsTheOldLocAndSendsAMovedWithoutAnEntered()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);
            map.Trace.Clear();

            object result = Waiting.NoSleep(mover.moveToNullspace());

            Assert.True(Dm.eq(result, TRUE));
            Assert.Null(mover.loc);
            Assert.Equal(new List<string>
            {
                "exited " + D(here) + " " + D(mover) + " 0 null",
                "exiting " + D(mover) + " " + D(here) + " 0 null",
                "moved " + D(mover) + " " + D(here) + " 0 1",
                "abstract_exited " + D(here) + " " + D(mover) + " 0 null",
            }, Signals(map));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // tg: modules/unit_tests/movement_order_sanity.dm, with a listener on the turf in place of connect_loc (package D).
    [Fact]
    public void AQdelInsideEnteredFlushesTheOuterMovedBeforeTheNullspaceOne()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var test_obj = Dm.@new<obj_test_movable_logger>(here);
            datum_test_movable_listener.On(there, COMSIG_ATOM_ENTERED, "interceptor", act: (source, arrived) => qdel(arrived));

            object did_move = Waiting.NoSleep(step(test_obj, EAST));

            Assert.True(Dm.truthy(did_move));
            Assert.True(QDELETED(test_obj));
            Assert.Null(test_obj.loc);
            Assert.Equal(new[]
            {
                "Moving from (1, 1) to (2, 1)",
                "Abstractly Moving from (2, 1) to NULL",
                "Moved from (1, 1) to (2, 1)",
                "Moved from (2, 1) to NULL",
            }, test_obj.movement_order);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // move-chain.md 3g: a forceMove inside an Exited handler fires the outer Moved first, then the nested chain, then the
    // first definition's stale Entered of the turf the mover already left.
    [Fact]
    public void AForceMoveInsideExitedResolvesTheOuterMovedFirstAndKeepsTheStaleEntered()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            datum_test_movable_listener.On(here, COMSIG_ATOM_EXITED, "exited", act: (source, gone) => Waiting.NoSleep(((atom_movable)gone).forceMove(far)));
            datum_test_movable_listener.On(there, COMSIG_ATOM_EXITED, "exited");
            datum_test_movable_listener.On(there, COMSIG_ATOM_ENTERED, "entered");
            datum_test_movable_listener.On(far, COMSIG_ATOM_ENTERED, "entered");
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            object result = Waiting.NoSleep(step(mover, EAST));

            Assert.True(Dm.eq(result, TRUE));
            Assert.Same(far, mover.loc);
            Assert.Equal(new List<string>
            {
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "moved " + D(mover) + " " + D(here) + " 4 0",
                "exited " + D(there) + " " + D(mover) + " 4 null",
                "entered " + D(far) + " " + D(mover) + " " + D(there) + " null",
                "moved " + D(mover) + " " + D(there) + " 0 1",
                "entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
            }, Signals(map));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AbstractMoveSendsOnlyMovedWithoutMomentum()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            ListenToTheChain(mover, here, there);
            map.Trace.Clear();

            Waiting.NoSleep(mover.abstract_move(there));

            Assert.Same(there, mover.loc);
            Assert.Equal(new[]
            {
                "moved " + D(mover) + " " + D(here) + " 4 1",
                "abstract_exited " + D(here) + " " + D(mover) + " 4 null",
                "abstract_entered " + D(there) + " " + D(mover) + " " + D(here) + " null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADiagonalMoveIsTwoCardinalStepsAndReturnsNullWhenBothFirstStepsFail()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(start);
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            object result = Waiting.NoSleep(step(mover, NORTHEAST));

            Assert.True(Dm.eq(result, TRUE));
            Assert.Same(locate(2, 2, 1), mover.loc);
            Assert.Equal(0, mover.moving_diagonally);
            Assert.Equal(new List<string>
            {
                "moved " + D(mover) + " " + D(start) + " 1 0",               // NORTH first (:787)
                "moved " + D(mover) + " " + D(locate(1, 2, 1)) + " 4 0",     // then EAST
            }, Signals(map));

            // move-chain.md 3f: both first steps fail, `.` is never assigned, the diagonal Move returns null.
            ((turf_test_movable_floor)locate(2, 3, 1)).enter_result = FALSE;
            ((turf_test_movable_floor)locate(3, 2, 1)).enter_result = FALSE;
            object blocked = Waiting.NoSleep(mover.Move(locate(3, 3, 1), NORTHEAST));
            Assert.Null(blocked);
            Assert.Same(locate(2, 2, 1), mover.loc);
            Assert.Equal(0, mover.moving_diagonally);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void BumpSendsItsSignalsAndAnInterceptStopsBumped()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var block = Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_BUMP, "bump");
            datum_test_movable_listener.On(block, COMSIG_ATOM_BUMPED, "bumped");
            map.Trace.Clear();

            Assert.Null(Waiting.NoSleep(mover.Bump(block))); // `. = ..()`: BYOND's Bump returns null (move-chain.md E4)
            Assert.Equal(new[] { "bump " + D(mover) + " " + D(block) + " null null", "bumped " + D(block) + " " + D(mover) + " null null" }, map.Trace);

            // A handler that moves the mover away: Bump goes on to Bumped (tg checks only QDELETED(bumped_atom)).
            map.Trace.Clear();
            var mover2 = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            datum_test_movable_listener.On(mover2, COMSIG_MOVABLE_BUMP, "bump", act: (source, bumped) => Waiting.NoSleep(((atom_movable)source).forceMove(locate(3, 3, 1))));
            Waiting.NoSleep(mover2.Bump(block));
            Assert.Same(locate(3, 3, 1), mover2.loc);
            Assert.Equal(new[] { "bump", "bumped" }, map.Trace.Select(l => l.Split(' ')[0]));

            var mover3 = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            datum_test_movable_listener.On(mover3, COMSIG_MOVABLE_BUMP, "bump", COMPONENT_INTERCEPT_BUMPED);
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(mover3.Bump(block)), TRUE));
            Assert.Equal(new[] { "bump" }, map.Trace.Select(l => l.Split(' ')[0]));

            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(mover.Bump(null)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CrossAsksBothSignalsThenCanPassAndTheDeprecatedBuiltinsCrash()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var block = Dm.@new<obj_test_movable_block>(locate(1, 1, 1));
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            datum_test_movable_listener.On(thing, COMSIG_MOVABLE_CROSS, "cross");
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_CROSS_OVER, "cross_over");
            map.Trace.Clear();

            Assert.True(Dm.eq(Waiting.NoSleep(thing.Cross(mover)), TRUE));   // not dense: CanPass gives !density
            Assert.Equal(new[] { "cross " + D(thing) + " " + D(mover) + " null null", "cross_over " + D(mover) + " " + D(thing) + " null null" }, map.Trace);
            Assert.True(Dm.eq(Waiting.NoSleep(block.Cross(mover)), FALSE));  // dense

            datum_test_movable_listener.On(thing, COMSIG_MOVABLE_CROSS, "veto", COMPONENT_BLOCK_CROSS);
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Cross(mover)), FALSE));
            Assert.DoesNotContain(map.Trace, l => l.StartsWith("cross_over"));

            Assert.Throws<DmCrashException>(() => thing.Crossed(mover));
            Assert.Throws<DmCrashException>(() => thing.Uncross(mover));
            Assert.Throws<DmCrashException>(() => thing.Uncrossed(mover));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanPassThroughLetsTheMoverOverruleTheBlocker()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var block = Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            mover.generic_canpass = FALSE;
            // No listener: the blocker's opinion (dense: FALSE).
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(mover, WEST)), FALSE));
            var permit = datum_test_movable_listener.On(mover, COMSIG_MOVABLE_CAN_PASS_THROUGH, "can_pass_through", COMSIG_COMPONENT_PERMIT_PASSAGE);
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(mover, WEST)), TRUE));
            permit.result = COMSIG_COMPONENT_REFUSE_PASSAGE;
            var thing = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1)); // not dense: its opinion is TRUE
            Assert.True(Dm.eq(Waiting.NoSleep(thing.CanPass(mover, WEST)), FALSE));
            // The direction handed to CanPassThrough is reversed (REVERSE_DIR, _atom.dm:234).
            Assert.Contains("can_pass_through " + D(mover) + " " + D(thing) + " 4 null", map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void APulledThingFollowsItsPullerAndStopPullingClearsBothEnds()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            datum_test_movable_listener.On(puller, COMSIG_ATOM_START_PULL, "start_pull");
            datum_test_movable_listener.On(crate, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled");
            datum_test_movable_listener.On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling");

            Assert.True(Dm.eq(Waiting.NoSleep(puller.start_pulling(crate)), TRUE));
            Assert.Same(crate, puller.pulling);
            Assert.Same(puller, crate.pulledby);

            // The puller steps away; the crate is two tiles behind and follows to the puller's old turf (:836-852).
            Assert.True(Dm.eq(Waiting.NoSleep(step(puller, EAST)), TRUE));
            Assert.Same(locate(3, 1, 1), puller.loc);
            Assert.Same(locate(2, 1, 1), crate.loc);
            Assert.Same(crate, puller.pulling);
            Assert.Null(crate.moving_from_pull);

            Waiting.NoSleep(puller.stop_pulling());
            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);
            Assert.Equal(new[] { "start_pull", "no_longer_pulled", "no_longer_pulling" }, Signals(map).Select(l => l.Split(' ')[0]));

            // A thing out of reach is let go by check_pulling.
            Assert.True(Dm.eq(Waiting.NoSleep(puller.start_pulling(crate)), TRUE));
            Waiting.NoSleep(crate.forceMove(locate(1, 3, 1)));
            Waiting.NoSleep(puller.check_pulling());
            Assert.Null(puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void SetGlideSizeSendsItsSignalAndAGlideOverrideIsAppliedByMove()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Equal(8, mover.glide_size); // atoms_movable.dm:4
            datum_test_movable_listener.On(mover, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, "glide");
            mover.set_glide_size(4);
            Assert.Equal(4, mover.glide_size);
            Waiting.NoSleep(mover.Move(locate(2, 1, 1), EAST, 2));
            Assert.Equal(2, mover.glide_size);
            Assert.Equal(new[] { "glide 4", "glide 2", "glide 2" }, Signals(map).Select(l => l.Split(' ')[0] + " " + l.Split(' ')[2]));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void SetAnchoredAndSetCurrentlyZMovingReportTheirChange()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Null(Waiting.NoSleep(mover.set_anchored(FALSE)));        // no change: a bare return
            Assert.True(Dm.eq(Waiting.NoSleep(mover.set_anchored(TRUE)), FALSE)); // the old value
            Assert.Equal(TRUE, mover.anchored);

            Assert.True(Dm.eq(mover.set_currently_z_moving(CURRENTLY_Z_FALLING), TRUE));
            Assert.True(Dm.eq(mover.set_currently_z_moving(CURRENTLY_Z_FALLING), FALSE)); // max(): no rise
            Assert.True(Dm.eq(mover.set_currently_z_moving(FALSE, TRUE), TRUE));
            Assert.True(Dm.eq(mover.currently_z_moving, FALSE));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AdjacentLooksAcrossBorderlessTurfsAndAroundDenseCorners()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var east = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var diagonal = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            var far = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Adjacent(east)), TRUE));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Adjacent(diagonal)), TRUE));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Adjacent(far)), FALSE));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Adjacent(locate(1, 1, 1))), TRUE)); // its own loc

            // Both corners of the diagonal hold a dense object: no way around (adjacent.dm:50-62, ClickCross :97).
            Dm.@new<obj_test_movable_block>(locate(1, 2, 1));
            Dm.@new<obj_test_movable_block>(locate(2, 1, 1));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.Adjacent(diagonal)), FALSE));

            var room = (area)locate(1, 1, 1).loc;
            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(room.Adjacent(thing)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void DestroyMovesTheMovableToNullspaceAndDeletesItsContents()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var inside = Dm.@new<obj_test_movable_thing>(box);
            datum_test_movable_listener.On(here, COMSIG_ATOM_EXITED, "exited");
            map.Trace.Clear();

            qdel(box);

            Assert.True(QDELETED(box));
            Assert.True(QDELETED(inside));
            Assert.Null(box.loc);
            Assert.Equal(INVISIBILITY_ABSTRACT, box.invisibility);
            Assert.Equal(new[] { "exited " + D(here) + " " + D(box) + " 0 null" }, map.Trace);
            Assert.False(Dm.@in(box, here.contents));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void InitializeDefersTheGenericEmissiveBlocker()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            thing.blocks_emissive = EMISSIVE_BLOCK_GENERIC; // as every mob (mob_defines.dm:18); a movable's default is NONE
            int before = DmWorldState.Current.DeferredCount;
            thing.Initialize(FALSE);
            Assert.Contains("code/game/atoms_movable.dm:183", DmWorldState.Current.DeferredSites); // DEVIATION(DV12)
            Assert.True(DmWorldState.Current.DeferredCount > before);
            Assert.Null(thing.update_emissive_block()); // the same leaf (:293)
            Assert.Contains("code/game/atoms_movable.dm:293", DmWorldState.Current.DeferredSites);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void BucklingWithoutBuckledMobsIsANoOp()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.True(Dm.eq(chair.has_buckled_mobs(), FALSE));
            chair.unbuckle_all_mobs(TRUE);
            chair.buckled_mobs = Dm.list();
            Assert.True(Dm.eq(chair.has_buckled_mobs(), FALSE));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TheMovementAndDirectionMacrosComputeTgsValues()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            GLOB.gvars_datum_init_order = Dm.list();
            GLOB.InitGlobalglide_size_multiplier();
            Assert.Equal(16, DELAY_TO_GLIDE_SIZE(1));   // 32 / max(1 / 0.5, 1) * 1
            Assert.Equal(32, DELAY_TO_GLIDE_SIZE(0.1f)); // clamped to MAX_GLIDE_SIZE
            Assert.Equal(1, DELAY_TO_GLIDE_SIZE(1000)); // clamped to MIN_GLIDE_SIZE
            Assert.Equal(8, MOVEMENT_ADJUSTED_GLIDE_SIZE(2, 1)); // 32 / (2 / 0.5) * 1 * 1

            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Equal(NONE, CHECK_MOVE_LOOP_FLAGS(mover, MOVEMENT_LOOP_OUTSIDE_CONTROL));

            Assert.Equal(0, ISDIAGONALDIR(NORTH));
            Assert.NotEqual(0, ISDIAGONALDIR(SOUTHWEST));
            Assert.Equal(NORTH, NSCOMPONENT(NORTHEAST));
            Assert.Equal(EAST, EWCOMPONENT(NORTHEAST));
            Assert.Equal(SOUTHEAST, NSDIRFLIP(NORTHEAST));
            Assert.Equal(NORTHWEST, EWDIRFLIP(NORTHEAST));
            Assert.Equal(1, GET_SPATIAL_INDEX(17));
            Assert.Equal(2, GET_SPATIAL_INDEX(18));
            Assert.Null(HAS_SPATIAL_GRID_CONTENTS(mover));
        }
    }
}
