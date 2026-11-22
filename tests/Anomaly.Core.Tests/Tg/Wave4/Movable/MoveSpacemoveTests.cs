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
/// Process_Spacemove and newtonian_move (atoms_movable.dm:1296-1333) and the conditions under which Moved calls them
/// (:896-901). A turf with force_no_gravity has no gravity, so the first branch of Process_Spacemove falls through; the last
/// one, handle_spacemove_grabbing's lattice search, looks for a lattice within one tile.
/// </summary>
public class MoveSpacemoveTests
{
    private static turf Weightless(int x, int y, int z = 1)
    {
        turf t = locate(x, y, z);
        t.force_no_gravity = TRUE;
        return t;
    }

    private static object Spacemove(atom_movable m, float dir = 0, float? continuous = null) => Waiting.NoSleep(m.Process_Spacemove(dir, continuous));

    // ---- Process_Spacemove ----

    [Fact]
    public void WithGravityProcessSpacemoveIsTrueAndNewtonianMoveDoesNothing()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            AssertNumber(TRUE, Spacemove(thing, EAST));
            AssertNumber(FALSE, Waiting.NoSleep(thing.newtonian_move(90))); // an angle: 90 degrees is EAST
            Assert.Null(thing.drift_handler);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ASpacemoveSignalThatStopsTheDriftAnswersTrueAndOtherBitsDoNot()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            var listener = On(thing, COMSIG_MOVABLE_SPACEMOVE, "spacemove", COMSIG_MOVABLE_STOP_SPACEMOVE);
            map.Trace.Clear();

            AssertNumber(TRUE, Spacemove(thing, EAST, TRUE));
            Assert.Equal(new[] { "spacemove " + D(thing) + " 4 1 null" }, map.Trace); // (movement_dir, continuous_move)

            listener.result = 2; // not the stop bit: the next checks are made, down to the lattice search, which finds none
            AssertNumber(FALSE, Spacemove(thing, EAST));
        }
    }

    [Fact]
    public void BeingPulledStopsTheDriftUnlessThePullerIsItselfPulledByThePulledThingAndNotMovingFromAPull()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(Weightless(1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(Weightless(2, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));

            AssertNumber(TRUE, Spacemove(crate)); // `pulledby && (pulledby.pulledby != src || moving_from_pull)` (:1308)

            // The puller is itself pulled by somebody else: `pulledby.pulledby != src` is true for the crate, still a stop.
            var other = Dm.@new<mob_test_movable_mob>(Weightless(3, 1));
            puller.set_pulledby(other);
            AssertNumber(TRUE, Spacemove(crate));

            // Two mobs that pull each other (raw): `pulledby.pulledby == src` and no stop...
            var a = Dm.@new<mob_test_movable_mob>(Weightless(1, 2));
            var b = Dm.@new<mob_test_movable_mob>(Weightless(2, 2));
            a.pulledby = b;
            b.pulledby = a;
            // ...so the search for something to push off is reached (translated in M4b slice 1): its answer decides
            object backup = Waiting.NoSleep(a.get_spacemove_backup(0, FALSE));
            AssertNumber(backup != null ? TRUE : FALSE, Spacemove(a));
            a.moving_from_pull = b;
            AssertNumber(TRUE, Spacemove(a)); // `|| moving_from_pull`
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AThrownThingAndAThingInsideAContainerNeedNoPushOff()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thrown = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            thrown.throwing = Dm.@new<datum_thrownthing>();
            AssertNumber(TRUE, Spacemove(thrown));

            var box = Dm.@new<obj_test_movable_thing>(Weightless(2, 2));
            var inside = Dm.@new<obj_test_movable_thing>(box);
            AssertNumber(TRUE, Spacemove(inside)); // `!isturf(loc)`
            AssertNumber(FALSE, Waiting.NoSleep(inside.newtonian_move(0))); // not on a turf: nothing happens
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AFreeFloatingThingStopsOnlyWithALatticeWithinOneTile()
    {
        var map = MovableWorld.Create(5, 5);
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(2, 2));
            AssertNumber(FALSE, Spacemove(thing));
            Assert.Null(thing.handle_spacemove_grabbing());

            // `locate(/obj/structure/lattice) in range(1, get_turf(src))` (:1318): two tiles away is too far, a diagonal neighbour is in range
            var lattice = Dm.@new<obj_structure_lattice>(Weightless(4, 4));
            AssertNumber(FALSE, Spacemove(thing));
            Waiting.NoSleep(lattice.forceMove(Weightless(3, 3)));
            AssertNumber(TRUE, Spacemove(thing));
            AssertNumber(TRUE, thing.handle_spacemove_grabbing());
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void HasGravityIsFalseInNullspaceAndOnAWeightlessTurfAndTrueOnAnOrdinaryOne()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var lost = Dm.@new<obj_test_movable_thing>();
            AssertNumber(FALSE, lost.has_gravity()); // "no gravity in nullspace" (_atom.dm:781)
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            AssertNumber(FALSE, thing.has_gravity());
            AssertNumber(TRUE, Dm.@new<obj_test_movable_thing>(locate(2, 2, 1)).has_gravity());
        }
    }

    // ---- when Moved calls newtonian_move (:899-901): `!moving_diagonally && !inertia_moving && momentum_change && movement_dir` ----

    [Fact]
    public void AStepInNoGravityStartsDriftingSoMovedReachesProcessSpacemove()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            Weightless(2, 1);

            On(thing, COMSIG_MOVABLE_SPACEMOVE, "spacemove", COMSIG_MOVABLE_STOP_SPACEMOVE); // this world runs no SSnewtonian_movement
            map.Trace.Clear();

            AssertNumber(TRUE, Step(thing, EAST));

            Assert.Same(locate(2, 1, 1), thing.loc);
            // Moved's newtonian_move(dir2angle(EAST)) asks Process_Spacemove(angle2dir(90), continuous_move = TRUE)
            Assert.Equal(new[] { "spacemove " + D(thing) + " 4 1 null" }, map.Trace.Where(line => line.StartsWith("spacemove ")));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ForceMoveAbstractMoveAndAMoverWithInertiaNeverStartNewtonianMovement()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            Weightless(2, 1);
            Weightless(3, 1);

            AssertNumber(TRUE, Force(thing, locate(2, 1, 1))); // movement_dir is NONE: `&& movement_dir` is false
            Waiting.NoSleep(thing.abstract_move(locate(3, 1, 1))); // momentum_change = FALSE

            thing.inertia_moving = TRUE; // a thing already drifting
            AssertNumber(TRUE, Step(thing, WEST));
            Assert.Same(locate(2, 1, 1), thing.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovedWithMomentumChangeFalseDoesNotStartDriftingAndTheDefaultTrueDoes()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));

            On(thing, COMSIG_MOVABLE_SPACEMOVE, "spacemove", COMSIG_MOVABLE_STOP_SPACEMOVE); // this world runs no SSnewtonian_movement
            map.Trace.Clear();

            Waiting.NoSleep(thing.Moved(locate(2, 1, 1), EAST, FALSE, null, FALSE)); // an "already existing" force
            Assert.Empty(map.Trace);

            Waiting.NoSleep(thing.Moved(locate(2, 1, 1), EAST, FALSE, null));
            Assert.Equal(new[] { "spacemove " + D(thing) + " 4 1 null" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADiagonalMoveStartsNewtonianMovementOnceAtTheEndNotInItsCardinalSteps()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            Weightless(1, 2);
            Weightless(2, 2);
            var calls = new List<string>();
            On(thing, COMSIG_MOVABLE_SPACEMOVE, "spacemove", COMSIG_MOVABLE_STOP_SPACEMOVE); // answers every Process_Spacemove
            map.Trace.Clear();

            AssertNumber(TRUE, Step(thing, NORTHEAST));

            // Both cardinal steps skip newtonian_move (moving_diagonally is 1, then 2: :899); the diagonal Move calls it
            // once after them with dir2angle(direct) (:823-826): one Process_Spacemove for the whole move.
            Assert.Equal(1, Count(map.Trace, "spacemove"));
            Assert.Equal("spacemove " + D(thing) + " 5 1 null", Lines(map.Trace, "spacemove")[0]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ADiagonalMoveOfAMoverWithInertiaNeverStartsNewtonianMovement()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(Weightless(1, 1));
            Weightless(1, 2);
            Weightless(2, 2);
            thing.inertia_moving = TRUE;
            AssertNumber(TRUE, Step(thing, NORTHEAST)); // `else if(!inertia_moving) newtonian_move(...)` (:825)
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
