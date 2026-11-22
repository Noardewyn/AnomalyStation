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
/// The z-move family of atoms_movable.dm (:354-471, :1255-1294; Moved's z-level handling :912-918): set_currently_z_moving,
/// can_z_move, zMove, get_z_move_affected, try_step_multiz and on_changed_z_level. The floors are
/// <see cref="turf_test_movable_floor_zpass"/>, which answers package B's zPassIn and zPassOut.
/// </summary>
public class MoveZTests
{
    private static EngineWorld ZWorld(int maxz = 3)
    {
        var map = MovableWorld.Create(3, 3, maxz, floor: typeof(turf_test_movable_floor_zpass));
        using (map.World.Enter())
            MovableWorld.LinkZLevels();
        return map;
    }

    private static object CanZMove(atom_movable mover, object direction, turf start = null, turf destination = null, float? flags = null)
        => mover.can_z_move(direction, start, destination, flags);

    // ---- set_currently_z_moving ----

    [Fact]
    public void SetCurrentlyZMovingKeepsTheHighestPriorityAndReportsWhetherItRoseAndForcedSetsItOutright()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Null(thing.currently_z_moving); // no default

            AssertNumber(TRUE, thing.set_currently_z_moving(CURRENTLY_Z_FALLING));      // max(null, 1) = 1 > null
            AssertNumber(FALSE, thing.set_currently_z_moving(CURRENTLY_Z_FALLING));     // unchanged
            AssertNumber(TRUE, thing.set_currently_z_moving(CURRENTLY_Z_ASCENDING));    // 4
            AssertNumber(FALSE, thing.set_currently_z_moving(CURRENTLY_Z_MOVING_GENERIC)); // lower priority: ignored
            AssertNumber(CURRENTLY_Z_ASCENDING, thing.currently_z_moving);

            AssertNumber(TRUE, thing.set_currently_z_moving(CURRENTLY_Z_FALLING, 1)); // forced: any value, always TRUE
            AssertNumber(CURRENTLY_Z_FALLING, thing.currently_z_moving);
            AssertNumber(TRUE, thing.set_currently_z_moving(FALSE, 1));
            AssertNumber(FALSE, thing.currently_z_moving);
        }
    }

    // ---- can_z_move ----

    [Fact]
    public void CanZMoveReturnsTheDestinationTurfOnlyWhenEveryCheckPasses()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(2, 2, 2), above = locate(2, 2, 3), below = locate(2, 2, 1);
            var flyer = Dm.@new<obj_test_movable_thing>(start);
            flyer.movement_type = FLYING;

            Assert.Same(above, CanZMove(flyer, UP));
            Assert.Same(below, CanZMove(flyer, DOWN));
            Assert.Same(above, CanZMove(flyer, UP, start)); // the start turf may be given
            Assert.Same(above, CanZMove(flyer, null, start, above)); // direction from get_dir_multiz(start, destination)
            AssertNumber(FALSE, CanZMove(flyer, null, start, null)); // no direction and no destination
            AssertNumber(FALSE, CanZMove(flyer, EAST)); // not UP or DOWN
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanZMoveRefusesAGroundMoverWithGravityUnderTheDefaultFlightFlagsButNotWithoutTheFlightCheck()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var walker = Dm.@new<obj_test_movable_thing>(locate(2, 2, 2));

            // ZMOVE_FLIGHT_FLAGS includes ZMOVE_CAN_FLY_CHECKS: not flying while there is gravity (:456-463)
            AssertNumber(FALSE, CanZMove(walker, UP));
            Assert.Same(locate(2, 2, 3), CanZMove(walker, UP, null, null, 0));
            Assert.Same(locate(2, 2, 3), CanZMove(walker, UP, null, null, ZMOVE_STAIRS_FLAGS));

            walker.movement_type = FLOATING; // floating counts as flying
            Assert.Same(locate(2, 2, 3), CanZMove(walker, UP));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanZMoveWithTheFallChecksNeedsGravityAndNoFlightAndNoThrow()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(2, 2, 2));
            Assert.Same(locate(2, 2, 1), CanZMove(thing, DOWN, null, null, ZMOVE_FALL_FLAGS));

            thing.movement_type = FLYING; // it would not fall
            AssertNumber(FALSE, CanZMove(thing, DOWN, null, null, ZMOVE_FALL_FLAGS));
            thing.movement_type = GROUND;

            thing.throwing = Dm.@new<datum_thrownthing>();
            AssertNumber(FALSE, CanZMove(thing, DOWN, null, null, ZMOVE_FALL_FLAGS));
            thing.throwing = null;

            locate(2, 2, 2).force_no_gravity = TRUE; // `!has_gravity(start)`
            AssertNumber(FALSE, CanZMove(thing, DOWN, null, null, ZMOVE_FALL_FLAGS));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanZMoveAsksTheSignalTheObstaclesAndAnchoringAndEachHasItsOwnFlagToIgnoreIt()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(2, 2, 2);
            var flyer = Dm.@new<obj_test_movable_thing>(start);
            flyer.movement_type = FLYING;
            var veto = On(flyer, COMSIG_CAN_Z_MOVE, "can_z_move", COMPONENT_CANT_Z_MOVE);
            AssertNumber(FALSE, CanZMove(flyer, UP));
            veto.result = 2; // not the veto bit
            Assert.Same(locate(2, 2, 3), CanZMove(flyer, UP));
            Assert.Contains("can_z_move " + D(flyer) + " " + D(start) + " " + D(locate(2, 2, 3)) + " null", map.Trace);

            // start.zPassOut and destination.zPassIn (B's procs; the fixture floor answers both)
            ((turf_test_movable_floor_zpass)locate(2, 2, 3)).z_pass = FALSE;
            AssertNumber(FALSE, CanZMove(flyer, UP));
            Assert.Same(locate(2, 2, 3), CanZMove(flyer, UP, null, null, ZMOVE_FLIGHT_FLAGS | ZMOVE_IGNORE_OBSTACLES));
            ((turf_test_movable_floor_zpass)locate(2, 2, 3)).z_pass = TRUE;

            flyer.anchored = TRUE;
            AssertNumber(FALSE, CanZMove(flyer, UP));
            Assert.Same(locate(2, 2, 3), CanZMove(flyer, UP, null, null, ZMOVE_FLIGHT_FLAGS | ZMOVE_ALLOW_ANCHORED));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanZMoveFromTheTopOrTheBottomLevelHasNowhereToGoAndAMoverInNullspaceCannotMove()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var top = Dm.@new<obj_test_movable_thing>(locate(1, 1, 3));
            top.movement_type = FLYING;
            AssertNumber(FALSE, CanZMove(top, UP));
            Assert.Same(locate(1, 1, 2), CanZMove(top, DOWN));

            var lost = Dm.@new<obj_test_movable_thing>();
            lost.movement_type = FLYING;
            AssertNumber(FALSE, CanZMove(lost, UP)); // `if(!start) start = get_turf(src); if(!start) return FALSE`
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- zMove ----

    [Fact]
    public void ZMoveForceMovesTheMoverToTheTurfAboveMarkingItAsZMovingOnlyWhileItMoves()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(2, 2, 1), above = locate(2, 2, 2);
            var flyer = Dm.@new<obj_test_movable_witness>(start);
            flyer.movement_type = FLYING;
            float seen = -1;
            On(flyer, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => seen = Dm.num(((atom_movable)source).currently_z_moving));
            On(flyer, COMSIG_MOVABLE_Z_CHANGED, "z_changed");
            map.Trace.Clear();

            AssertNumber(TRUE, Waiting.NoSleep(flyer.zMove(UP)));

            Assert.Same(above, flyer.loc);
            Assert.Equal(CURRENTLY_Z_MOVING_GENERIC, seen); // `currently_z_moving || CURRENTLY_Z_MOVING_GENERIC` (:397)
            AssertNumber(FALSE, flyer.currently_z_moving); // set_currently_z_moving(FALSE, TRUE) after the forceMove (:399)
            Assert.Equal(1, Count(map.Trace, "z_changed"));
            Assert.Equal("z_changed " + D(flyer) + " " + D(start) + " " + D(above) + " 1", Lines(map.Trace, "z_changed")[0]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ZMoveWithoutAValidTargetClearsTheZMovingFlagAndReturnsFalse()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var walker = Dm.@new<obj_test_movable_thing>(locate(2, 2, 3)); // not flying, top level
            walker.currently_z_moving = CURRENTLY_Z_ASCENDING;

            AssertNumber(FALSE, Waiting.NoSleep(walker.zMove(UP)));

            Assert.Same(locate(2, 2, 3), walker.loc);
            AssertNumber(FALSE, walker.currently_z_moving); // :386-388
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ZMoveToAnExplicitTargetNeedsNoChecksAndKeepsAnExistingZMovingPriority()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var walker = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            walker.currently_z_moving = CURRENTLY_Z_ASCENDING;
            float seen = -1;
            On(walker, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => seen = Dm.num(((atom_movable)source).currently_z_moving));

            AssertNumber(TRUE, Waiting.NoSleep(walker.zMove(null, locate(3, 3, 3))));

            Assert.Same(locate(3, 3, 3), walker.loc); // a ground mover went up two levels: the target was given
            Assert.Equal(CURRENTLY_Z_ASCENDING, seen); // its own priority is kept while moving
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ZMoveCarriesBuckledMobsAndWithIncludePulledThePulledChainToTheSameTurfAndKeepsThePulls()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(2, 2, 1), above = locate(2, 2, 2);
            var puller = Dm.@new<mob_test_movable_mob>(start);
            puller.movement_type = FLYING;
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var rider = Dm.@new<mob_living_test_movable_living>(start);
            puller.buckled_mobs = Dm.list(rider);
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));

            AssertNumber(TRUE, Waiting.NoSleep(puller.zMove(UP, null, ZMOVE_FLIGHT_FLAGS)));

            Assert.Same(above, puller.loc);
            Assert.Same(above, rider.loc);  // `. |= buckled_mobs` (:417)
            Assert.Same(above, crate.loc);  // ZMOVE_INCLUDE_PULLED is in the flight flags (:419-427)
            Assert.Same(crate, puller.pulling); // doMove does not break the pull of a z-moving thing (:1181)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TgQuirk_ZMoveWithoutIncludePulledLeavesThePulledThingBehindAndItsCheckPullsCallSetsOnlyPullingNotZAllowed()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            float flags = ZMOVE_CHECK_PULLING | ZMOVE_IGNORE_OBSTACLES | ZMOVE_ALLOW_ANCHORED;

            AssertNumber(TRUE, Waiting.NoSleep(puller.zMove(UP, locate(2, 2, 2) as turf, flags)));

            Assert.Same(locate(2, 2, 2), puller.loc);
            Assert.Same(locate(2, 1, 1), crate.loc); // not in the moved list: no ZMOVE_INCLUDE_PULLED
            // tg writes `moved_mov.check_pulling(TRUE)` (:401), and the first parameter of check_pulling is only_pulling:
            // z_allowed stays FALSE, so a pull across levels is dropped at once although the crate is one tile away.
            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ZMoveWithoutTheCheckPullsFlagsNeverLooksAtThePulls()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));

            AssertNumber(TRUE, Waiting.NoSleep(puller.zMove(UP, locate(2, 2, 2) as turf, ZMOVE_IGNORE_OBSTACLES)));

            Assert.Same(crate, puller.pulling); // `if(z_move_flags & ZMOVE_CHECK_PULLS)` is false (:400)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ZMoveWithCheckPulledbyLetsGoOfAPullerThatIsLeftBehind()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));

            // the crate alone goes up two levels: its puller is now more than one tile away and on another level
            Waiting.NoSleep(crate.zMove(null, locate(2, 1, 3), ZMOVE_CHECK_PULLEDBY | ZMOVE_IGNORE_OBSTACLES));

            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- get_z_move_affected ----

    [Fact]
    public void GetZMoveAffectedIsTheMoverAndItsBuckledMobsAndOnlyWithIncludePulledTheWholePulledChain()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var a = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            // a living b: since pulling (2026-10-15) a living puller's start_pulling is tg's /mob/living one, whose
            // set_pull_offsets shifts the pulled mob (add_offsets, a living proc: tg pulls no other mob); messages and sounds off
            var b = Dm.@new<mob_living_test_movable_living>(locate(2, 1, 1));
            var c = Dm.@new<obj_test_movable_thing>(locate(3, 1, 1));
            var rider = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            a.buckled_mobs = Dm.list(rider);
            Waiting.NoSleep(a.start_pulling(b, GRAB_PASSIVE, supress_message: TRUE));
            Waiting.NoSleep(b.start_pulling(c, GRAB_PASSIVE, supress_message: TRUE));

            Assert.Equal(new object[] { a, rider }, EngineTestHelpers.Items(a.get_z_move_affected(0)));
            Assert.Equal(new object[] { a, rider, b, c }, EngineTestHelpers.Items(a.get_z_move_affected(ZMOVE_INCLUDE_PULLED)));
            Assert.Equal(new object[] { b, c }, EngineTestHelpers.Items(b.get_z_move_affected(ZMOVE_INCLUDE_PULLED)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void GetZMoveAffectedIncludesWhatARiderPullsAndStopsAtMobsThatPullEachOther()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var rider = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            var bag = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            chair.buckled_mobs = Dm.list(rider);
            Waiting.NoSleep(rider.start_pulling(bag, GRAB_PASSIVE, supress_message: TRUE)); // the living pull's sound needs SSsounds
            Assert.Equal(new object[] { chair, rider, bag }, EngineTestHelpers.Items(chair.get_z_move_affected(ZMOVE_INCLUDE_PULLED))); // :421-423

            // "we pass in the list from this proc to ensure we dont reach an infinite loop due to ... two mobs grabing eachother"
            var x = Dm.@new<mob_test_movable_mob>(locate(1, 2, 1));
            var y = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            x.pulling = y; y.pulling = x; // raw: the two pull each other
            Assert.Equal(new object[] { x, y }, EngineTestHelpers.Items(x.get_z_move_affected(ZMOVE_INCLUDE_PULLED)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TryStepMultizStepsInAPlaneDirectionAndZMovesUpAndDown()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            var flyer = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            flyer.movement_type = FLYING;

            AssertNumber(TRUE, Waiting.NoSleep(flyer.try_step_multiz(EAST)));
            Assert.Same(locate(2, 1, 1), flyer.loc);
            AssertNumber(TRUE, Waiting.NoSleep(flyer.try_step_multiz(UP)));
            Assert.Same(locate(2, 1, 2), flyer.loc);
            AssertNumber(TRUE, Waiting.NoSleep(flyer.try_step_multiz(DOWN)));
            Assert.Same(locate(2, 1, 1), flyer.loc);
            AssertNumber(FALSE, Waiting.NoSleep(flyer.try_step_multiz(DOWN))); // nowhere below
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Moved and on_changed_z_level ----

    [Fact]
    public void MovingBetweenZLevelsSendsZChangedToTheMoverAndToEveryMovableInsideIt()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1), up = locate(1, 1, 2);
            var crate = Dm.@new<obj_test_movable_thing>(start);
            var box = Dm.@new<obj_test_movable_thing>(crate);
            var item = Dm.@new<obj_test_movable_thing>(box);
            foreach (var (thing, label) in new[] { (crate, "crate"), (box, "box"), (item, "item") })
                On(thing, COMSIG_MOVABLE_Z_CHANGED, label);
            map.Trace.Clear();

            Force(crate, up);

            // on_changed_z_level notifies the contents recursively (:1284-1286), with the same turfs
            Assert.Equal(new[]
            {
                "crate " + D(crate) + " " + D(start) + " " + D(up) + " 1",
                "box " + D(box) + " " + D(start) + " " + D(up) + " 1",
                "item " + D(item) + " " + D(start) + " " + D(up) + " 1",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovingWithinOneLevelSendsNoZChangedAndNotifyContentsFalseSkipsTheContents()
    {
        var map = ZWorld();
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1);
            var crate = Dm.@new<obj_test_movable_thing>(start);
            var box = Dm.@new<obj_test_movable_thing>(crate);
            On(crate, COMSIG_MOVABLE_Z_CHANGED, "crate");
            On(box, COMSIG_MOVABLE_Z_CHANGED, "box");
            map.Trace.Clear();

            Step(crate, EAST);
            Assert.DoesNotContain(map.Trace, l => !l.StartsWith("Enter ")); // no signal at all

            Waiting.NoSleep(crate.on_changed_z_level(start, locate(1, 1, 2), TRUE, FALSE)); // notify_contents = FALSE
            Assert.Equal(new[] { "crate " + D(crate) + " " + D(start) + " " + D(locate(1, 1, 2)) + " 1" }, map.Trace.Where(l => !l.StartsWith("Enter ")));
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
