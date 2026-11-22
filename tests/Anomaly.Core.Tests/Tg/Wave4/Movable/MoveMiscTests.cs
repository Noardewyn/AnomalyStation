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
/// The rest of the chain: iteration of a turf's contents while handlers change them (T7), the five arguments of the Moved
/// signal, buckled mobs that travel with their seat (handle_buckled_mob_movement) and the buckling procs of package A
/// (buckling.dm:66, :203), and the type-level facts of the movable var block.
/// </summary>
public class MoveMiscTests
{
    // ---- T7: snapshot iteration ----

    [Fact]
    public void T7_ATurfsEnterAsksEveryOccupantEvenWhenAHandlerMovesAnotherOneAwayOrAddsOneDuringTheLoop()
    {
        var map = MovableWorld.Create(floor: typeof(turf_test_movable_floor_tg_enter));
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            var a = Dm.@new<obj_test_movable_thing>(there);
            var b = Dm.@new<obj_test_movable_thing>(there);
            var c = Dm.@new<obj_test_movable_thing>(there);
            atom_movable late = null;
            On(a, COMSIG_MOVABLE_CROSS, "cross_a", act: (source, crossing) =>
            {
                Force(b, far); // b leaves `there` while the loop over its contents is running
                late = Dm.@new<obj_test_movable_thing>(there); // and a new one arrives
            });
            On(b, COMSIG_MOVABLE_CROSS, "cross_b");
            On(c, COMSIG_MOVABLE_CROSS, "cross_c");
            map.Trace.Clear();

            AssertNumber(TRUE, Step(mover, EAST));

            // `for(var/atom/movable/thing as anything in contents)` reads a copy (BYOND ref, for list proc; move-chain.md E5)
            Assert.Equal(new[] { "cross_a", "cross_b", "cross_c" }, Kinds(map.Trace.Where(l => l.StartsWith("cross_"))));
            Assert.Same(far, b.loc);
            Assert.Equal(new object[] { a, c, late, mover }, EngineTestHelpers.Items(there.contents));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T7_AContainersContentsAreDeletedOneByOneEvenThoughEachDeletionEditsTheList()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var items = Enumerable.Range(0, 5).Select(_ => Dm.@new<obj_test_movable_thing>(box)).ToArray();

            qdel(box); // `for(var/movable_content in contents) qdel(movable_content)` (:258)

            Assert.All(items, i => Assert.True(QDELETED(i)));
            Assert.Empty(EngineTestHelpers.Items(box.contents));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- the arguments of Moved and its signal ----

    [Fact]
    public void TheMovedSignalCarriesTheOldLocTheDirectionForcedOldLocsAndMomentumChange()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1), far = locate(3, 3, 1);
            var mover = Dm.@new<obj_test_movable_thing>(here);
            datum_test_movable_wide_listener.On(mover, COMSIG_MOVABLE_MOVED, "moved");
            map.Trace.Clear();

            Step(mover, EAST);
            Force(mover, far);
            Waiting.NoSleep(mover.abstract_move(there));

            AssertLines(new[]
            {
                "moved " + D(mover) + " " + D(here) + " 4 0 null 1",   // Move: (old_loc, direction, FALSE, old_locs, TRUE)
                "moved " + D(mover) + " " + D(there) + " 0 1 null 1",  // doMove: (oldloc, NONE, TRUE, null), momentum_change defaulted
                "moved " + D(mover) + " " + D(far) + " 10 1 null 0",   // abstract_move: (old_loc, get_dir = SOUTHWEST, TRUE, momentum_change = FALSE)
            }, map.Trace.Where(l => l.StartsWith("moved ")));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovedItselfReturnsTrueAndSendsTheAbstractSignalsToTheRightAtoms()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var mover = Dm.@new<obj_test_movable_thing>(there);
            datum_test_movable_wide_listener.On(here, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited");
            datum_test_movable_wide_listener.On(there, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered");
            var old_locs = Dm.list(here);
            map.Trace.Clear();

            // a direct call: the mover is at `there`, the old_loc argument says it came from `here`
            AssertNumber(TRUE, Waiting.NoSleep(mover.Moved(here, EAST, FALSE, old_locs, TRUE)));

            AssertLines(new[]
            {
                "abstract_exited " + D(here) + " " + D(mover) + " 4 null null null",
                "abstract_entered " + D(there) + " " + D(mover) + " " + D(here) + " list(" + D(here) + ") null null",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- buckled mobs travel with their seat ----

    [Fact]
    public void ABuckledMobIsMovedAlongWithItsSeat()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var rider = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            chair.buckled_mobs = Dm.list(rider);

            AssertNumber(TRUE, Step(chair, EAST));

            Assert.Same(locate(2, 1, 1), chair.loc);
            Assert.Same(locate(2, 1, 1), rider.loc); // handle_buckled_mob_movement: buckled_mob.Move(newloc, direct, glide) (:1467)
            AssertNumber(EAST, rider.last_move);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void WhenTheBuckledMobCannotFollowTheSeatMovesBackToItAndTheMoveFails()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var chair = Dm.@new<obj_test_movable_thing>(here);
            var rider = Dm.@new<mob_living_test_movable_living>(here);
            chair.buckled_mobs = Dm.list(rider);
            // After the chair has left, the rider's own Exit from the old turf is refused.
            var veto = On(here, COMSIG_ATOM_EXIT, "exit");
            On(chair, COMSIG_MOVABLE_MOVED, "chair_moved", act: (source, old_loc) => veto.result = COMPONENT_ATOM_BLOCK_EXIT);

            // `Move(buckled_mob.loc, direct)`: the seat goes back where its rider is, `last_move = buckled_mob.last_move`
            // (:1469-1472), and the first Move reports the failure.
            AssertNumber(FALSE, Step(chair, EAST));

            Assert.Same(here, chair.loc);
            Assert.Same(here, rider.loc);
            // The seat's way back calls the rider's Move once more, now to its own loc: that returns null but still runs
            // the tail of the second definition (`last_move = direct`, move-chain.md 3a edge note), and the seat copies
            // the rider's last_move.
            AssertNumber(EAST, rider.last_move);
            Assert.True(Dm.eq(chair.last_move, rider.last_move));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void BuckledMobsAreCountedByHasBuckledMobsAndUnbuckledOneByOneInOrderWithTheForceFlag()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_chair>(locate(1, 1, 1));
            var a = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            var b = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            AssertNumber(FALSE, chair.has_buckled_mobs());
            chair.unbuckle_all_mobs(); // nothing buckled: nothing happens
            Assert.Empty(map.Trace);

            chair.buckled_mobs = Dm.list(a, b);
            AssertNumber(TRUE, chair.has_buckled_mobs());
            map.Trace.Clear();

            chair.unbuckle_all_mobs(); // INVOKE_ASYNC(src, PROC_REF(unbuckle_mob), m, force) for each (:207-208)
            chair.unbuckle_all_mobs(TRUE);

            AssertLines(new[]
            {
                "unbuckle_mob " + D(chair) + " " + D(a) + " 0",
                "unbuckle_mob " + D(chair) + " " + D(b) + " 0",
                "unbuckle_mob " + D(chair) + " " + D(a) + " 1",
                "unbuckle_mob " + D(chair) + " " + D(b) + " 1",
            }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void DeletingASeatUnbucklesEveryoneWithForce()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var chair = Dm.@new<obj_test_movable_chair>(locate(1, 1, 1));
            var rider = Dm.@new<mob_living_test_movable_living>(locate(1, 1, 1));
            chair.buckled_mobs = Dm.list(rider);
            map.Trace.Clear();

            qdel(chair); // Destroy: unbuckle_all_mobs(force = TRUE) (:222)

            Assert.Equal(new[] { "unbuckle_mob " + D(chair) + " " + D(rider) + " 1" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- var block of the movable (atoms_movable.dm:1-141) ----

    [Fact]
    public void TheMovableVarBlockHasTgsDefaultsAndItsOverridesOfByondsDefaults()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<atom_movable_test_movable_effect>(locate(1, 1, 1));

            Assert.Equal(OBJ_LAYER, thing.layer);              // atoms_movable.dm:3
            Assert.Equal(8f, thing.glide_size);                // :4
            Assert.Equal(Dm.bitor(Dm.bitor(TILE_BOUND, PIXEL_SCALE), LONG_GLIDE), (object)thing.appearance_flags); // :5
            Assert.Equal(typeof(atom_movable), thing.abstract_type);
            Assert.Null(thing.last_move);
            Assert.Null(thing.active_movement);
            Assert.Equal(0f, thing.anchored);
            Assert.Equal((float)MOVE_RESIST_DEFAULT, thing.move_resist);
            Assert.Equal((float)MOVE_FORCE_DEFAULT, thing.move_force);
            Assert.Equal((float)PULL_FORCE_DEFAULT, thing.pull_force);
            Assert.Equal(1f, thing.generic_canpass);
            Assert.Equal(0f, thing.moving_diagonally);
            Assert.Equal((float)GROUND, thing.movement_type);
            Assert.Equal((float)GRAB_PASSIVE, thing.grab_state);
            Assert.Equal(1f, thing.set_dir_on_move);
            Assert.Null(thing.pulledby);
            Assert.Null(thing.pulling);
            Assert.Null(thing.drift_handler);
            Assert.Null(thing.currently_z_moving);
            Assert.Equal(0f, thing.pass_flags);
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
