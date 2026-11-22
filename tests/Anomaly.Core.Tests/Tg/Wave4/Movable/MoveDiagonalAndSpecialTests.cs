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
/// T6 (diagonal order), T8 (cyclic loc refusal), T9 (abstract_move) of the move-chain goldens, the direction and glide size
/// handling of Move, and the multi-tile branch of the first Move definition with its tg quirks. Written from
/// atoms_movable.dm:687-880 (Move), :676 (abstract_move), :1169-1253 (doMove) and move-chain.md 3e, 3f.
/// </summary>
public class MoveDiagonalAndSpecialTests
{
    // ---- T6: diagonal moves ----

    [Fact]
    public void T6_ADiagonalMoveIsTheNorthStepThenTheEastStepAndMovingDiagonallyIsOneThenTwoThenZero()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(start);
            var seen = new List<float>();
            On(mover, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => seen.Add(((atom_movable)source).moving_diagonally));
            On(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, NORTHEAST));

            Assert.Equal(new[] { (float)FIRST_DIAG_STEP, SECOND_DIAG_STEP }, seen); // :787 and :789
            Assert.Equal(0f, mover.moving_diagonally);
            Assert.Same(locate(2, 2, 1), mover.loc);
            // The diagonal Move itself sends no ATTEMPTED_MOVE (it never reaches the first definition); each cardinal step does.
            AssertLines(new[]
            {
                "witness_move " + D(mover) + " " + D(locate(2, 2, 1)) + " 5",
                "witness_move " + D(mover) + " " + D(locate(1, 2, 1)) + " 1",
                "attempted " + D(mover) + " " + D(locate(1, 2, 1)) + " 1 null",
                "witness_moved " + D(mover) + " " + D(start) + " 1 0 null 1",
                "moved " + D(mover) + " " + D(start) + " 1 0",
                "witness_move " + D(mover) + " " + D(locate(2, 2, 1)) + " 4",
                "attempted " + D(mover) + " " + D(locate(2, 2, 1)) + " 4 null",
                "witness_moved " + D(mover) + " " + D(locate(1, 2, 1)) + " 4 0 null 1",
                "moved " + D(mover) + " " + D(locate(1, 2, 1)) + " 4 0",
            }, map.Trace.Where(l => !l.StartsWith("Enter ")));
            Assert.Equal(EAST, mover.dir);
            AssertNumber(EAST, mover.last_move); // set by the second cardinal step; the diagonal Move returns before the tail
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Theory]
    [InlineData(NORTHEAST, 1, 1, 2, 2, NORTH, EAST)]
    [InlineData(NORTHWEST, 2, 1, 1, 2, NORTH, WEST)]
    [InlineData(SOUTHEAST, 1, 2, 2, 1, SOUTH, EAST)]
    [InlineData(SOUTHWEST, 2, 2, 1, 1, SOUTH, WEST)]
    public void T6_EachDiagonalTriesTheNorthOrSouthStepFirst(int diagonal, int x, int y, int ex, int ey, int first, int second)
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_witness>(locate(x, y, 1));
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, diagonal));

            Assert.Same(locate(ex, ey, 1), mover.loc);
            string[] moves = Lines(map.Trace, "witness_move").Select(l => l.Split(' ')[3]).ToArray();
            Assert.Equal(new[] { diagonal.ToString(), first.ToString(), second.ToString() }, moves);
            Assert.Equal(second, mover.dir);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T6_WhenTheNorthStepIsBlockedTheEastStepComesFirst()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            ((turf_test_movable_floor)locate(1, 2, 1)).enter_result = FALSE;
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, NORTHEAST));

            Assert.Same(locate(2, 2, 1), mover.loc);
            // north (fails), east, north again: `else if (moving_diagonally && step(src, EAST))` (:793-796)
            Assert.Equal(new[] { "5", "1", "4", "1" }, Lines(map.Trace, "witness_move").Select(l => l.Split(' ')[3]).ToArray());
            Assert.Equal(NORTH, mover.dir);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T6_WhenTheSecondStepFailsTheDiagonalReturnsFalseAndTheMoverFacesTheFirstStepsDirection()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            ((turf_test_movable_floor)locate(2, 2, 1)).enter_result = FALSE; // east of the north step's turf
            On(locate(2, 1, 1), COMSIG_ATOM_ENTERED, "never"); // the east-first order is not tried: north succeeded
            map.Trace.Clear();

            object result = Step(mover, NORTHEAST);

            AssertNumber(FALSE, result); // `. = step(src, EAST)` is the failed second step's FALSE (:790)
            Assert.Same(locate(1, 2, 1), mover.loc);
            Assert.Equal(NORTH, mover.dir); // `if(!. && set_dir_on_move && update_dir) setDir(first_step_dir)` (:822-823)
            Assert.Equal(0f, mover.moving_diagonally);
            Assert.Equal(0, Count(map.Trace, "never"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T6_WhenBothFirstStepsFailTheDiagonalReturnsNullAndMovingDiagonallyIsReset()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            ((turf_test_movable_floor)locate(1, 2, 1)).enter_result = FALSE;
            ((turf_test_movable_floor)locate(2, 1, 1)).enter_result = FALSE;

            Assert.Null(Chain.Move(mover, locate(2, 2, 1), NORTHEAST)); // not even FALSE (move-chain.md 3f)

            Assert.Same(locate(1, 1, 1), mover.loc);
            Assert.Equal(0f, mover.moving_diagonally);
            Assert.Equal(EAST, mover.dir); // each failed attempt turned it: north, then east
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T6_AForceMoveFromAHandlerOfTheFirstStepAbortsTheSecondAndTheDiagonalReturnsNull()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            bool done = false;
            On(mover, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) =>
            {
                if (done) return;
                done = true;
                Force((atom_movable)source, far); // doMove sets moving_diagonally = 0 (:1186)
            });
            map.Trace.Clear();

            // The comment at :771-775: "a forceMove taking place due to a Crossed, Bumped, etc. call will interrupt the
            // second half of the diagonal movement". `. = step(src, EAST)` is never run, so `.` stays null.
            Assert.Null(Step(mover, NORTHEAST));

            Assert.Same(far, mover.loc);
            Assert.Equal(0f, mover.moving_diagonally);
            Assert.Equal(new[] { "5", "1" }, Lines(map.Trace, "witness_move").Select(l => l.Split(' ')[3]).ToArray()); // no east step
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T6_AStepDiagonalFromAContainerWorksOutTheTargetFromTheOutermostTurf()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(box);

            // The item is in a box: its first step (north) takes it out onto the turf (1, 2), the second east to (2, 2).
            AssertNumber(TRUE, Step(item, NORTHEAST));
            Assert.Same(locate(2, 2, 1), item.loc);
            Assert.Same(locate(1, 1, 1), box.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- T8: a cyclic loc is refused silently by the engine ----

    [Fact]
    public void T8_MovingABoxIntoWhatItContainsIsRefusedWithAStackTraceAndFalse()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var item = Dm.@new<obj_test_movable_thing>(box);
            map.Trace.Clear();

            AssertNumber(FALSE, Force(box, item));

            Assert.Same(here, box.loc);
            Assert.Same(box, item.loc);
            Assert.Equal(new object[] { box }, EngineTestHelpers.Items(here.contents));
            // stack_trace: reported and the proc goes on (rules 9.5)
            var report = Assert.Single(map.Runtimes);
            Assert.Contains("Attempt to move", report.Message);
            Assert.Contains("rejected by BYOND, possibly due to cyclic contents", report.Message);
            map.Runtimes.Clear();
        }
    }

    [Fact]
    public void T8_MovingAThingIntoItselfOrDeepIntoItsOwnNestIsRefusedToo()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var outer = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var middle = Dm.@new<obj_test_movable_thing>(outer);
            var inner = Dm.@new<obj_test_movable_thing>(middle);

            AssertNumber(FALSE, Force(outer, outer));
            AssertNumber(FALSE, Force(outer, inner));
            AssertNumber(FALSE, Force(middle, inner));
            Assert.Same(locate(1, 1, 1), outer.loc);
            Assert.Same(outer, middle.loc);
            Assert.Same(middle, inner.loc);
            Assert.Equal(3, map.Runtimes.Count);
            map.Runtimes.Clear();
        }
    }

    [Fact]
    public void T8_TgQuirk_ARefusedMoveLeavesItsMovedPendingAndTheNextMovementFiresItFirst()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var box = Dm.@new<obj_test_movable_witness>(here);
            var item = Dm.@new<obj_test_movable_thing>(box);

            Force(box, item); // refused: `return FALSE` at :1195, after SET_ACTIVE_MOVEMENT (:1176)
            map.Runtimes.Clear();
            Assert.NotNull(box.active_movement); // "active_movement stays set until next RESOLVE" (move-chain.md 3e)
            Assert.Equal(4, box.active_movement.Count);
            Assert.Same(here, box.active_movement[1]);
            map.Trace.Clear();

            Force(box, there);

            // The stale Moved(here, NONE, TRUE, null) fires while the box is still on `here`, then the real one.
            AssertLines(new[]
            {
                "witness_moved " + D(box) + " " + D(here) + " 0 1 null 1",
                "witness_moved " + D(box) + " " + D(here) + " 0 1 null 1",
            }, Lines(map.Trace, "witness_moved"));
            Assert.Null(box.active_movement);
            Assert.Same(there, box.loc);
        }
    }

    // ---- T9: abstract_move ----

    [Fact]
    public void T9_AbstractMoveFromNullspaceHasNoOldLocAndOnlyEnteredSignals()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf there = locate(2, 2, 1);
            var mover = Dm.@new<obj_test_movable_witness>();
            On(there, COMSIG_ATOM_ENTERED, "entered");
            On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
            On(mover, COMSIG_MOVABLE_Z_CHANGED, "z_changed");
            map.Trace.Clear();

            Waiting.NoSleep(mover.abstract_move(there));

            Assert.Same(there, mover.loc);
            AssertLines(new[]
            {
                "witness_moved " + D(mover) + " null 0 1 null 0", // get_dir(null, there) is 0; momentum_change is FALSE
                "abstract_entered " + D(there) + " " + D(mover) + " null null",
                "z_changed " + D(mover) + " null " + D(there) + " 1",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T9_AbstractMoveToNullspaceSendsAbstractExitedAndAZChangeToNoTurf()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            On(here, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
            On(mover, COMSIG_MOVABLE_Z_CHANGED, "z_changed");
            On(here, COMSIG_ATOM_EXITED, "exited");
            map.Trace.Clear();

            Waiting.NoSleep(mover.abstract_move(null));

            Assert.Null(mover.loc);
            AssertLines(new[]
            {
                "witness_moved " + D(mover) + " " + D(here) + " 0 1 null 0",
                "abstract_exited " + D(here) + " " + D(mover) + " 0 null",
                "z_changed " + D(mover) + " " + D(here) + " null 1",
            }, map.Trace); // and no Exited: "movement with zero side effects"
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T9_AbstractMoveAcrossAreasCallsNoAreaProcAndDoesNotTurnOrMarkTheMover()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            MovableWorld.NewArea<area_misc_testroom_test_movable_room_recording_other>(there);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.dir = SOUTH;
            On(here, COMSIG_ATOM_EXIT, "exit");
            On(here, COMSIG_ATOM_EXITED, "exited");
            On(there, COMSIG_ATOM_ENTERED, "entered");
            map.Trace.Clear();

            Waiting.NoSleep(mover.abstract_move(there));

            Assert.Empty(map.Trace);
            Assert.Equal(SOUTH, mover.dir);
            Assert.Null(mover.last_move);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- direction and glide size ----

    [Fact]
    public void ASuccessfulMoveTurnsTheMoverBeforeExitAndAFailedOneToo()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.dir = SOUTH;
            On(mover, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir_change");
            On(mover, COMSIG_ATOM_DIR_CHANGE, "dir_change");
            On(mover, COMSIG_ATOM_POST_DIR_CHANGE, "post_dir_change");
            On(here, COMSIG_ATOM_EXIT, "exit");
            map.Trace.Clear();

            Step(mover, EAST);

            // PRE_DIR_CHANGE(dir, newdir), DIR_CHANGE(dir, newdir), POST_DIR_CHANGE(old, new): before the Exit of the move
            // (:700-701), and no second change at the tail because the dir already is `direct` (:870).
            Assert.Equal(new[] { "pre_dir_change", "dir_change", "post_dir_change", "exit" }, Kinds(map.Trace.Where(l => !l.StartsWith("Enter "))));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void UpdateDirFalseAndSetDirOnMoveFalseKeepTheMoversDirection()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            mover.dir = SOUTH;

            AssertNumber(TRUE, Chain.Move(mover, locate(2, 1, 1), EAST, null, FALSE)); // update_dir = FALSE
            Assert.Equal(SOUTH, mover.dir);
            AssertNumber(EAST, mover.last_move);

            mover.set_dir_on_move = FALSE; // tg's directional windows
            AssertNumber(TRUE, Step(mover, EAST));
            Assert.Equal(SOUTH, mover.dir);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AMoverWhoseDirectionChangeIsBlockedByASignalStillMoves()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            mover.dir = SOUTH;
            On(mover, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir_change", COMPONENT_ATOM_BLOCK_DIR_CHANGE);
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            Assert.Same(locate(2, 1, 1), mover.loc);
            Assert.Equal(SOUTH, mover.dir); // setDir returned at the block (_atom.dm:581-583)
            Assert.Equal(2, Count(map.Trace, "pre_dir_change")); // asked at the top of Move and again at its tail
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AHandlerThatChangesTheDirectionMidMoveIsOverriddenByTheTailsSetDir()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            On(mover, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => ((atom_movable)source).dir = NORTH);

            Step(mover, EAST);

            Assert.Equal(EAST, mover.dir); // `if(set_dir_on_move && dir != direct && update_dir) setDir(direct)` (:870)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AGlideOverrideIsAppliedEarlyOnlyWhenItDiffersAndAlwaysLateAndTheNoGlideTraitBlocksIt()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            mover.set_glide_size(3);
            On(mover, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, "glide");
            map.Trace.Clear();

            Chain.Move(mover, locate(2, 1, 1), EAST, 3); // already 3: only the late re-apply (:864) sends the signal
            Assert.Equal(new[] { "glide " + D(mover) + " 3 null null" }, Lines(map.Trace, "glide"));

            map.Trace.Clear();
            Chain.Move(mover, locate(3, 1, 1), EAST, 5); // differs: early and late
            Assert.Equal(2, Count(map.Trace, "glide"));
            Assert.Equal(5f, mover.glide_size);

            map.Trace.Clear();
            Chain.Move(mover, locate(2, 1, 1), WEST); // no override (0): no glide signal at all
            Assert.Equal(0, Count(map.Trace, "glide"));
            Assert.Equal(5f, mover.glide_size);

            // TRAIT_NO_GLIDE: set_glide_size returns at once (:663-664): no signal, no change.
            ADD_TRAIT(mover, TRAIT_NO_GLIDE, "test");
            Chain.Move(mover, locate(1, 1, 1), WEST, 7);
            Assert.Equal(0, Count(map.Trace, "glide"));
            Assert.Equal(5f, mover.glide_size);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void SetGlideSizeDefaultsToEightAndPassesItToBuckledMobs()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var rider = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            chair.buckled_mobs = Dm.list(rider);
            chair.glide_size = 2;

            chair.set_glide_size();
            Assert.Equal(8f, chair.glide_size);
            Assert.Equal(8f, rider.glide_size);

            chair.set_glide_size(4);
            Assert.Equal(4f, rider.glide_size);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- the multi-tile branch of the first Move definition ----

    [Fact]
    public void TgQuirk_AMultiTileMoveChecksOneExtraTilePerAxisBecauseBlockIsInclusive()
    {
        var map = MovableWorld.Create(5, 3);
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.bound_width = 64; // two tiles wide, one high: the footprint of one more tile east
            On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            // block(dx, dy, dz, dx + ceil(64 / 32), dy + ceil(32 / 32), dz) = x 2..4, y 1..2 (:718-720), x fastest: each tile
            // gets Enter and then PRE_MOVE, in that order.
            AssertLines(new[]
            {
                "Enter " + D(locate(2, 1, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(2, 1, 1)) + " null null",
                "Enter " + D(locate(3, 1, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(3, 1, 1)) + " null null",
                "Enter " + D(locate(4, 1, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(4, 1, 1)) + " null null",
                "Enter " + D(locate(2, 2, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(2, 2, 1)) + " null null",
                "Enter " + D(locate(3, 2, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(3, 2, 1)) + " null null",
                "Enter " + D(locate(4, 2, 1)) + " " + D(mover), "pre_move " + D(mover) + " " + D(locate(4, 2, 1)) + " null null",
            }, map.Trace);
            Assert.Same(there, mover.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_AMultiTileMoveSendsEnteredToEveryPredictedTileExceptTheOldLocWithTheOldLocsList()
    {
        var map = MovableWorld.Create(5, 3);
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var mover = Dm.@new<obj_test_movable_witness>(here);
            mover.bound_width = 64;
            On(here, COMSIG_ATOM_EXIT, "exit");
            On(here, COMSIG_ATOM_EXITED, "exited");
            foreach (turf t in new[] { locate(2, 1, 1), locate(3, 1, 1), locate(4, 1, 1), locate(2, 2, 1), locate(3, 2, 1), locate(4, 2, 1) })
                On(t, COMSIG_ATOM_ENTERED, "entered");
            map.Trace.Clear();

            Step(mover, EAST);

            // Exit of every old loc first; after loc = newloc `old_locs - new_locs` get Exited (here: the old turf), and
            // `new_locs - old_locs` get Entered(src, oldloc, old_locs) (:747-756); Moved gets old_locs as well. The mover's
            // 64-pixel bounds cover (1,1) and (2,1) (BYOND's locs, the M5 leftovers' multi-tile bounds), so (2,1), in both
            // lists, is neither left nor entered.
            string old_locs = "list(" + D(here) + ", " + D(locate(2, 1, 1)) + ")";
            AssertLines(new[]
            {
                "exit " + D(here) + " " + D(mover) + " 4 null",
                "exited " + D(here) + " " + D(mover) + " 4 null",
                "entered " + D(locate(3, 1, 1)) + " " + D(mover) + " " + D(here) + " " + old_locs,
                "entered " + D(locate(4, 1, 1)) + " " + D(mover) + " " + D(here) + " " + old_locs,
                "entered " + D(locate(2, 2, 1)) + " " + D(mover) + " " + D(here) + " " + old_locs,
                "entered " + D(locate(3, 2, 1)) + " " + D(mover) + " " + D(here) + " " + old_locs,
                "entered " + D(locate(4, 2, 1)) + " " + D(mover) + " " + D(here) + " " + old_locs,
                "witness_moved " + D(mover) + " " + D(here) + " 4 0 " + old_locs + " 1",
            }, map.Trace.Where(l => !l.StartsWith("Enter ") && !l.StartsWith("witness_move ")));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ARefusedTileAnywhereInTheMultiTileFootprintStopsTheMove()
    {
        var map = MovableWorld.Create(5, 3);
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            mover.bound_width = 64;
            ((turf_test_movable_floor)locate(4, 1, 1)).enter_result = FALSE; // the third predicted tile
            map.Trace.Clear();

            AssertNumber(FALSE, Step(mover, EAST));

            Assert.Same(locate(1, 1, 1), mover.loc);
            Assert.Equal(3, Count(map.Trace, "Enter")); // stopped at the first refusal: tiles (2,1), (3,1), (4,1)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_AMultiTileDoMoveTellsOnlyTheTurfsOfItsOffByOneBlockPastItsBounds()
    {
        var map = MovableWorld.Create(5, 3);
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            mover.bound_width = 64;
            On(locate(3, 3, 1), COMSIG_ATOM_ENTERED, "entered_corner");
            On(locate(5, 3, 1), COMSIG_ATOM_ENTERED, "entered_past");
            map.Trace.Clear();

            Force(mover, locate(3, 3, 1));

            // atoms_movable.dm:1200-1213: locs is read after loc is set, so locs - new_locs is empty and new_locs - locs is the column
            // block() adds one past the bounds (x 5; the row above is off the map); the turfs it now covers are not told
            Assert.Same(locate(3, 3, 1), mover.loc);
            Assert.Equal(1, Count(map.Trace, "entered_past"));
            Assert.Equal(0, Count(map.Trace, "entered_corner"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_AMultiTileMoveToNullspaceSendsNoTurfExitedBecauseLocsIsReadAfterLocIsCleared()
    {
        var map = MovableWorld.Create(room: typeof(area_misc_testroom_test_movable_room_recording));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var roomA = (area)here.loc;
            var mover = Dm.@new<obj_test_movable_thing>(here);
            mover.bound_width = 64;
            On(here, COMSIG_ATOM_EXITED, "exited");
            map.Trace.Clear();

            AssertNumber(TRUE, Waiting.NoSleep(mover.moveToNullspace()));

            // `loc = null` comes first (:1233), so `locs` is empty (BYOND ref, locs var) and no turf is told; the area is.
            AssertLines(new[] { "area_exited " + D(roomA) + " " + D(mover) + " 0" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
