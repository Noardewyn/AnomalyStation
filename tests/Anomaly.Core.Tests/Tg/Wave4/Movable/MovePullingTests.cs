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
/// Pulling (atoms_movable.dm:525-662, :1723-1763; T11 of the goldens): start_pulling, stop_pulling, set_pulling,
/// set_pulledby, can_be_pulled, setGrabState, check_pulling, Move_Pulled and the pull-follow block of Move with
/// move_from_pull. Written from the DM; the pullers are mobs because only mobs pull in tg (`pulledby` is a /mob).
/// </summary>
public class MovePullingTests
{
    private static object Pull(atom_movable puller, atom_movable pullee, object state = null, object force = null, float? supress = null)
        => Waiting.NoSleep(puller.start_pulling(pullee, state ?? GRAB_PASSIVE, force, supress));

    // ---- start_pulling and can_be_pulled ----

    [Fact]
    public void StartPullingLinksBothEndsSendsStartPullWithItsThreeArgumentsAndReturnsTrue()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(puller, COMSIG_ATOM_START_PULL, "start_pull");
            map.Trace.Clear();

            AssertNumber(TRUE, Pull(puller, crate));

            Assert.Same(crate, puller.pulling);
            Assert.Same(puller, crate.pulledby);
            // SEND_SIGNAL(src, COMSIG_ATOM_START_PULL, pulled_atom, state, force) with force defaulting to move_force (:526)
            Assert.Equal(new[] { "start_pull " + D(puller) + " " + D(crate) + " 0 1000" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void StartPullingRefusesADeletedOrNullThingAndWhatCanBePulledSaysNoTo()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));

            AssertNumber(FALSE, Pull(puller, null)); // QDELETED(null)

            qdel(crate);
            AssertNumber(FALSE, Pull(puller, crate));

            var anchored = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            anchored.anchored = TRUE;
            AssertNumber(FALSE, Pull(puller, anchored));

            Assert.Null(puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CanBePulledChecksEachConditionOfTheDmInOrder()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));

            AssertNumber(TRUE, crate.can_be_pulled(puller, 1000));
            AssertNumber(FALSE, crate.can_be_pulled(crate, 1000)); // src == user

            crate.move_resist = 1001; // `force < move_resist * MOVE_FORCE_PULL_RATIO`
            AssertNumber(FALSE, crate.can_be_pulled(puller, 1000));
            AssertNumber(TRUE, crate.can_be_pulled(puller, 1001));
            crate.move_resist = MOVE_RESIST_DEFAULT;

            crate.anchored = TRUE;
            AssertNumber(FALSE, crate.can_be_pulled(puller, 1000));
            crate.anchored = FALSE;

            crate.throwing = Dm.@new<datum_thrownthing>();
            AssertNumber(FALSE, crate.can_be_pulled(puller, 1000));
            crate.throwing = null;

            var cant = On(crate, COMSIG_ATOM_CAN_BE_PULLED, "can_be_pulled", COMSIG_ATOM_CANT_PULL);
            AssertNumber(FALSE, crate.can_be_pulled(puller, 1000));
            cant.result = 2; // another bit is not COMSIG_ATOM_CANT_PULL
            AssertNumber(TRUE, crate.can_be_pulled(puller, 1000));

            var in_box = Dm.@new<obj_test_movable_thing>(crate);
            AssertNumber(FALSE, in_box.can_be_pulled(puller, 1000)); // !isturf(loc)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void StartPullingWithAnExplicitForceCanMoveAHeavierThing()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var heavy = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            heavy.move_resist = 2500;

            AssertNumber(FALSE, Pull(puller, heavy)); // move_force 1000 < 2500
            AssertNumber(TRUE, Pull(puller, heavy, force: 2500));
            Assert.Same(heavy, puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void PullingSomethingElseDropsTheFirstThingAndPullingAThingAlreadyPulledByAnotherMobTakesItOverFromThem()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var alice = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var bob = Dm.@new<mob_test_movable_mob>(locate(1, 2, 1));
            var one = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var two = Dm.@new<obj_test_movable_thing>(locate(2, 2, 1));
            On(alice, COMSIG_ATOM_NO_LONGER_PULLING, "alice_no_longer_pulling");
            On(bob, COMSIG_ATOM_NO_LONGER_PULLING, "bob_no_longer_pulling");
            Pull(alice, one);

            // "If we're pulling something then drop what we're currently pulling and pull this instead." (:532-546). The state
            // must not be 0 for that: `if(state == 0) { stop_pulling(); return FALSE }` (:533), and GRAB_PASSIVE is 0.
            map.Trace.Clear();
            AssertNumber(TRUE, Pull(alice, two, GRAB_AGGRESSIVE));
            Assert.Same(two, alice.pulling);
            Assert.Null(one.pulledby);
            Assert.Equal(new[] { "alice_no_longer_pulling " + D(alice) + " " + D(one) + " null null" }, map.Trace);

            // an object can't be pulled by two mobs at once (:548-550); log_combat is a deferred leaf (M2)
            map.Trace.Clear();
            AssertNumber(TRUE, Pull(bob, two, GRAB_AGGRESSIVE));
            Assert.Null(alice.pulling);
            Assert.Same(two, bob.pulling);
            Assert.Same(bob, two.pulledby);
            Assert.Equal(new[] { "alice_no_longer_pulling " + D(alice) + " " + D(two) + " null null" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void PullingWhatItAlreadyPullsOnlyCyclesTheGrabStateAndStateZeroLetsGo()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var other = Dm.@new<obj_test_movable_thing>(locate(1, 2, 1));
            On(puller, COMSIG_ATOM_START_PULL, "start_pull");
            Pull(puller, crate);
            map.Trace.Clear();

            // pulled_atom == pulling: setGrabState(state) and TRUE, no new START_PULL (:536-542)
            AssertNumber(TRUE, Pull(puller, crate, GRAB_AGGRESSIVE));
            Assert.Equal(GRAB_AGGRESSIVE, puller.grab_state);
            Assert.Equal(0, Count(map.Trace, "start_pull"));
            Assert.Same(crate, puller.pulling);

            // `if(state == 0) { stop_pulling(); return FALSE }` when pulling something else (:533-535)
            AssertNumber(FALSE, Pull(puller, other, 0));
            Assert.Null(puller.pulling);
            Assert.Null(other.pulledby);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- stop_pulling, set_pulling, set_pulledby ----

    [Fact]
    public void StopPullingWithNothingPulledDoesNothingAndOtherwiseSendsBothSignalsInOrder()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling");
            On(crate, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled");
            map.Trace.Clear();

            Waiting.NoSleep(puller.stop_pulling());
            Assert.Empty(map.Trace);

            Pull(puller, crate);
            map.Trace.Clear();
            Waiting.NoSleep(puller.stop_pulling());
            Assert.Equal(new[]
            {
                "no_longer_pulled " + D(crate) + " " + D(puller) + " null null",
                "no_longer_pulling " + D(puller) + " " + D(crate) + " null null",
            }, map.Trace);
            Assert.Null(crate.pulledby);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void SetPullingReturnsFalseForNoChangeAndTheOldValueForAChangeIncludingNull()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var one = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var two = Dm.@new<obj_test_movable_thing>(locate(2, 2, 1));

            // "null signals there was a change, be sure to return FALSE if none happened here." (:581)
            AssertNumber(FALSE, Waiting.NoSleep(puller.set_pulling(null))); // null -> null: no change
            Assert.Null(Waiting.NoSleep(puller.set_pulling(one)));          // null -> one: the old value, null
            Assert.Same(one, puller.pulling);
            Assert.Same(puller, one.pulledby);
            AssertNumber(FALSE, Waiting.NoSleep(puller.set_pulling(one)));  // no change
            Assert.Same(one, Waiting.NoSleep(puller.set_pulling(two)));     // one -> two: the old value
            Assert.Null(one.pulledby);
            Assert.Same(puller, two.pulledby);
            Assert.Same(two, Waiting.NoSleep(puller.set_pulling(null)));
            Assert.Null(two.pulledby);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void SetPulledbyReturnsFalseForNoChangeAndTheOldPullerForAChange()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var a = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var b = Dm.@new<mob_test_movable_mob>(locate(1, 2, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));

            AssertNumber(FALSE, crate.set_pulledby(null));
            Assert.Null(crate.set_pulledby(a));
            Assert.Same(a, crate.pulledby);
            AssertNumber(FALSE, crate.set_pulledby(a));
            Assert.Same(a, crate.set_pulledby(b));
            Assert.Same(b, crate.set_pulledby(null));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ASoftCritMobThatIsPulledIsImmobilizedAndTheStatChangeSignalTogglesTheTrait()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var victim = Dm.@new<mob_living_test_movable_living>(locate(2, 1, 1));
            victim.stat = SOFT_CRIT;

            AssertNumber(TRUE, Pull(puller, victim, supress: TRUE)); // a mob: log_combat is deferred, no visible_message
            Assert.True(HAS_TRAIT_FROM(victim, TRAIT_IMMOBILIZED, "pulled_while_softcrit")); // :603-604

            // COMSIG_MOB_STATCHANGE is registered on the pulled mob (:601): back to conscious removes the trait...
            victim._SendSignal(COMSIG_MOB_STATCHANGE, Dm.list(victim, STABLE));
            Assert.False(HAS_TRAIT(victim, TRAIT_IMMOBILIZED));
            // ...and into soft crit adds it again.
            victim._SendSignal(COMSIG_MOB_STATCHANGE, Dm.list(victim, SOFT_CRIT));
            Assert.True(HAS_TRAIT_FROM(victim, TRAIT_IMMOBILIZED, "pulled_while_softcrit"));

            // Letting go unregisters the signal and removes the trait (:594-597).
            Waiting.NoSleep(puller.stop_pulling());
            Assert.False(HAS_TRAIT(victim, TRAIT_IMMOBILIZED));
            Assert.False(Dm.truthy(victim._listen_lookup?[COMSIG_MOB_STATCHANGE]));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- setGrabState ----

    [Fact]
    public void SetGrabStateAddsAndRemovesTheChokeholdTraitsOnThePulledThingAndReturnsTheOldState()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(puller, COMSIG_MOVABLE_SET_GRAB_STATE, "set_grab_state");
            On(crate, COMSIG_MOVABLE_CHANGED_GRABBED_STATE, "changed_grabbed_state");
            Pull(puller, crate);
            map.Trace.Clear();

            Assert.Null(Waiting.NoSleep(puller.setGrabState(GRAB_PASSIVE))); // same state: a bare return

            // passive -> aggressive: immobilized and hands blocked, from CHOKEHOLD_TRAIT (:1750-1755)
            AssertNumber(GRAB_PASSIVE, Waiting.NoSleep(puller.setGrabState(GRAB_AGGRESSIVE)));
            Assert.True(HAS_TRAIT_FROM(crate, TRAIT_IMMOBILIZED, CHOKEHOLD_TRAIT));
            Assert.True(HAS_TRAIT_FROM(crate, TRAIT_HANDS_BLOCKED, CHOKEHOLD_TRAIT));
            Assert.False(HAS_TRAIT(crate, TRAIT_FLOORED));
            Assert.Equal(new[]
            {
                "set_grab_state " + D(puller) + " 1 null null",
                "changed_grabbed_state " + D(crate) + " 1 null null",
            }, map.Trace);

            // aggressive -> neck: floored as well
            AssertNumber(GRAB_AGGRESSIVE, Waiting.NoSleep(puller.setGrabState(GRAB_NECK)));
            Assert.True(HAS_TRAIT_FROM(crate, TRAIT_FLOORED, CHOKEHOLD_TRAIT));
            Assert.True(HAS_TRAIT(crate, TRAIT_IMMOBILIZED));

            // neck -> kill: nothing more to add; kill -> aggressive: a downgrade removes only the floor
            Waiting.NoSleep(puller.setGrabState(GRAB_KILL));
            AssertNumber(GRAB_KILL, Waiting.NoSleep(puller.setGrabState(GRAB_AGGRESSIVE)));
            Assert.False(HAS_TRAIT(crate, TRAIT_FLOORED));
            Assert.True(HAS_TRAIT(crate, TRAIT_IMMOBILIZED));

            // back to passive removes the rest
            Waiting.NoSleep(puller.setGrabState(GRAB_PASSIVE));
            Assert.False(HAS_TRAIT(crate, TRAIT_IMMOBILIZED));
            Assert.False(HAS_TRAIT(crate, TRAIT_HANDS_BLOCKED));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void StoppingAGrabResetsTheGrabStateToPassiveBeforeTheLinkIsCut()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller, crate, GRAB_NECK);
            Assert.True(HAS_TRAIT(crate, TRAIT_FLOORED));

            Waiting.NoSleep(puller.stop_pulling()); // set_pulling(null): setGrabState(GRAB_PASSIVE) while `pulling` is still set (:582-583)

            Assert.Equal(GRAB_PASSIVE, puller.grab_state);
            Assert.False(HAS_TRAIT(crate, TRAIT_FLOORED));
            Assert.False(HAS_TRAIT(crate, TRAIT_IMMOBILIZED));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- check_pulling ----

    [Fact]
    public void CheckPullingLetsGoOfAThingMoreThanOneTileAwayOrOnAnotherZLevelUnlessZAllowed()
    {
        var map = MovableWorld.Create(3, 3, 2);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller, crate);

            Waiting.NoSleep(puller.check_pulling());
            Assert.Same(crate, puller.pulling); // adjacent: nothing happens

            crate.loc = locate(2, 1, 2); // raw: the other z level, same x and y
            // get_dist across z levels is 0 in BYOND for the same x and y?: (2,1,2) vs (1,1,1) is 1 tile away in x
            Waiting.NoSleep(puller.check_pulling(z_allowed: TRUE));
            Assert.Same(crate, puller.pulling); // `z != pulling.z && !z_allowed` is false with z_allowed

            Waiting.NoSleep(puller.check_pulling());
            Assert.Null(puller.pulling); // z differs and z is not allowed

            Pull(puller, crate); // can_be_pulled does not look at z
            crate.loc = locate(3, 3, 2);
            Waiting.NoSleep(puller.check_pulling(z_allowed: TRUE));
            Assert.Null(puller.pulling); // get_dist > 1 whatever z_allowed says
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CheckPullingLetsGoWhenThePullerIsNotOnATurfOrThePulleeIsAnchoredOrTooHeavy()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));

            // !isturf(loc)
            Pull(puller, crate);
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            puller.loc = box; // raw
            Waiting.NoSleep(puller.check_pulling());
            Assert.Null(puller.pulling);

            // pulling.anchored
            puller.loc = locate(1, 1, 1);
            Pull(puller, crate);
            crate.anchored = TRUE;
            Waiting.NoSleep(puller.check_pulling());
            Assert.Null(puller.pulling);
            crate.anchored = FALSE;

            // pulling.move_resist > move_force
            Pull(puller, crate);
            crate.move_resist = 1001;
            Waiting.NoSleep(puller.check_pulling());
            Assert.Null(puller.pulling);
            crate.move_resist = MOVE_RESIST_DEFAULT;
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CheckPullingLetsGoWhenThePulleeIsInANonTurfLocOtherThanThePullersOwnWithADebugLogLine()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var box = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller, crate);
            crate.loc = box; // raw, as "all code that changes an object's loc uses forceMove()" says it should not be

            Waiting.NoSleep(puller.check_pulling());

            Assert.Null(puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void CheckPullingFromThePulleesSideLetsTheOtherEndGoUnlessOnlyPullingOrMidDiagonalOrZAllowed()
    {
        var map = MovableWorld.Create(3, 3, 2);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller, crate);
            crate.loc = locate(3, 3, 1); // out of reach, unnoticed

            // only_pulling: the crate's own check does not look at its puller (:658)
            Waiting.NoSleep(crate.check_pulling(only_pulling: TRUE));
            Assert.Same(crate, puller.pulling);

            // in the middle of a diagonal move of the pulled thing the puller is not dropped
            crate.moving_diagonally = FIRST_DIAG_STEP;
            Waiting.NoSleep(crate.check_pulling());
            Assert.Same(crate, puller.pulling);
            crate.moving_diagonally = 0;

            Waiting.NoSleep(crate.check_pulling());
            Assert.Null(puller.pulling);
            Assert.Null(crate.pulledby);

            // the z level of the puller matters only without z_allowed
            Pull(puller, crate);
            crate.loc = locate(2, 1, 2);
            Waiting.NoSleep(crate.check_pulling(z_allowed: TRUE));
            Assert.Same(crate, puller.pulling);
            Waiting.NoSleep(crate.check_pulling());
            Assert.Null(puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Move: the pull-follow block (T11) ----

    [Fact]
    public void WhenThePullerStepsAwayThePulledThingFollowsIntoTheTurfTheyLeftThroughMoveFromPull()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            Pull(puller, crate);
            bool from_pull_seen = false;
            On(crate, COMSIG_MOVABLE_MOVED, "crate_moved", act: (source, old_loc) => from_pull_seen = ((atom_movable)source).moving_from_pull == puller);
            map.Trace.Clear();

            AssertNumber(TRUE, Step(puller, EAST));

            Assert.Same(locate(3, 1, 1), puller.loc);
            Assert.Same(locate(2, 1, 1), crate.loc); // the turf the puller left (current_turf, :838)
            Assert.Same(crate, puller.pulling);
            Assert.True(from_pull_seen); // moving_from_pull is the puller while the pulled thing's Move runs
            Assert.Null(crate.moving_from_pull);
            // the crate's Move is called by move_from_pull as Move(target_turf, get_dir(src, target_turf), glide_size)
            Assert.Equal("witness_move " + D(crate) + " " + D(locate(2, 1, 1)) + " 4", Lines(map.Trace, "witness_move")[0]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ThePulledThingDoesNotFollowWhileItStaysAdjacentOnACardinalSide()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_witness>(locate(1, 1, 1));
            Pull(puller, crate);
            map.Trace.Clear();

            // The puller steps onto the crate's own turf: distance 0, get_dir(pulling, src) is 0 (not diagonal): no follow.
            AssertNumber(TRUE, Step(puller, WEST));

            Assert.Same(locate(1, 1, 1), puller.loc);
            Assert.Same(locate(1, 1, 1), crate.loc);
            Assert.Same(crate, puller.pulling);
            Assert.Empty(Lines(map.Trace, "witness_move"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ACrateDiagonalToThePullerAfterItsStepFollowsEvenThoughItIsAdjacent()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 2, 1));
            var crate = Dm.@new<obj_test_movable_witness>(locate(1, 2, 1)); // west of the puller
            Pull(puller, crate);
            map.Trace.Clear();

            Step(puller, NORTH); // the crate is now south-west of the puller: diagonal (:850 `ISDIAGONALDIR(pull_dir)`)

            Assert.Same(locate(2, 3, 1), puller.loc);
            Assert.Same(locate(2, 2, 1), crate.loc);
            Assert.Same(crate, puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AThreeMobChainFollowsAliceBobCharlieAsInChainPullThroughSpaceBeforeItTeleports()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            // tg: modules/unit_tests/chain_pull_through_space.dm:68-86 (the part before the space turf): alice pulls bob,
            // who pulls charlie, all adjacent in a row; alice.Move(turf) with no direction.
            var alice = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));
            var bob = Dm.@new<mob_test_movable_mob>(locate(4, 1, 1));
            var charlie = Dm.@new<mob_test_movable_mob>(locate(5, 1, 1));
            Pull(alice, bob, supress: TRUE);
            Pull(bob, charlie, supress: TRUE);

            AssertNumber(TRUE, Chain.Move(alice, locate(2, 1, 1)));

            Assert.Equal(2f, alice.x);
            Assert.Equal(3f, bob.x); // "During normal move, Bob was not at the correct x"
            Assert.Equal(4f, charlie.x); // "During normal move, Charlie was not at the correct x"
            Assert.Same(bob, alice.pulling);
            Assert.Same(charlie, bob.pulling);
            AssertNumber(TRUE, Waiting.NoSleep(bob.Adjacent(alice)));
            AssertNumber(TRUE, Waiting.NoSleep(charlie.Adjacent(bob)));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ThePullFollowStopsAfterAFailedMoveAndWhenTheMoverLosesThePullDuringIt()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Pull(puller, crate);
            ((turf_test_movable_floor)locate(3, 1, 1)).enter_result = FALSE;

            AssertNumber(FALSE, Step(puller, EAST)); // `if(. && pulling && ...)`: not on a failed move

            Assert.Same(locate(1, 1, 1), crate.loc);
            Assert.Same(crate, puller.pulling);

            // A handler that drops the pull during the move: `pulling == pullee` fails, so nothing follows (:834)
            ((turf_test_movable_floor)locate(3, 1, 1)).enter_result = TRUE;
            On(puller, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => Waiting.NoSleep(((atom_movable)source).stop_pulling()));
            AssertNumber(TRUE, Step(puller, EAST));
            Assert.Same(locate(1, 1, 1), crate.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void TheOuroborosGuardKeepsAPulledThingFromFollowingWhenWhatItPullsIsAlreadyMovingFromAPull()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var alice = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));
            var bob = Dm.@new<obj_test_movable_witness>(locate(2, 1, 1));
            var charlie = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            Pull(alice, bob);
            // charlie is "already moving from a pull" (bob's own pulled thing): bob must not follow alice (:841)
            bob.pulling = charlie; // raw: no link needed, the guard reads only these fields
            charlie.moving_from_pull = alice;
            map.Trace.Clear();

            Step(alice, EAST);

            Assert.Same(locate(2, 1, 1), bob.loc); // did not follow
            Assert.Empty(Lines(map.Trace, "witness_move"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnAnchoredPulleeEndsThePullAndSetAnchoredOnAPulledThingDropsItFromItsPuller()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(crate, COMSIG_MOVABLE_SET_ANCHORED, "set_anchored");
            Pull(puller, crate);
            map.Trace.Clear();

            Assert.Null(Waiting.NoSleep(crate.set_anchored(FALSE))); // no change: a bare return, no signal
            Assert.Empty(map.Trace);

            // `if(anchored && pulledby) pulledby.stop_pulling()` (:1146) and the signal with the new value
            AssertNumber(FALSE, Waiting.NoSleep(crate.set_anchored(TRUE)));
            Assert.Null(puller.pulling);
            Assert.Equal(new[] { "set_anchored " + D(crate) + " 1 null null" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MoveChecksThePullsFirstEvenWhenItFailsButNotWhenItIsAMoveFromAPull()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller, crate);
            crate.loc = locate(5, 1, 1); // out of reach, unnoticed
            On(locate(1, 1, 1), COMSIG_ATOM_EXIT, "exit", COMPONENT_ATOM_BLOCK_EXIT);

            // The puller's move is refused by the Exit, but `check_pulling(z_allowed = TRUE)` ran first (:766).
            AssertNumber(FALSE, Step(puller, EAST));
            Assert.Null(puller.pulling);

            // moving_from_pull skips it: the crate's own Move does not look at its (distant) puller
            var puller2 = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate2 = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Pull(puller2, crate2);
            crate2.loc = locate(5, 1, 1);
            crate2.moving_from_pull = puller2;
            AssertNumber(TRUE, Chain.Move(crate2, locate(4, 1, 1), WEST));
            Assert.Same(crate2, puller2.pulling);
            crate2.moving_from_pull = null;
            AssertNumber(TRUE, Chain.Move(crate2, locate(3, 1, 1), WEST)); // without the flag the puller is dropped
            Assert.Null(puller2.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void APulledThingThatBecomesAnchoredDuringTheMoveIsReleasedAtTheEndOfIt()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Pull(puller, crate);
            On(puller, COMSIG_MOVABLE_MOVED, "moved", act: (source, old_loc) => crate.anchored = TRUE); // raw: no set_anchored

            AssertNumber(TRUE, Step(puller, EAST));

            Assert.Null(puller.pulling); // `if(pulling.anchored) stop_pulling()` (:837-838)
            Assert.Same(locate(1, 1, 1), crate.loc); // and it did not follow
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AMoverThatIsItselfPulledIsNotCheckedAfterItsMoveButOneWithNoPullerIs()
    {
        foreach (bool has_puller in new[] { true, false })
        {
            var map = MovableWorld.Create(5, 1);
            using (map.World.Enter())
            {
                var alice = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
                var bob = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
                var charlie = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));
                if (has_puller)
                    Pull(alice, bob, supress: TRUE);
                Pull(bob, charlie, supress: TRUE);
                // Charlie cannot follow bob into the turf bob leaves, so he is two tiles away after bob's step.
                ((turf_test_movable_floor)locate(2, 1, 1)).enter_result = FALSE;

                AssertNumber(TRUE, Step(bob, WEST));

                Assert.Same(locate(3, 1, 1), charlie.loc);
                // Bob's Move ends with `if(pulledby) { (only when the puller is z-moving) check_pulling } else check_pulling()`
                // (:849-857): with a puller there is no check at all ("it breaks stair conga lines"), so charlie stays
                // pulled although he is out of reach; with no puller bob lets him go.
                Assert.Equal(has_puller, charlie.pulledby != null);
                Assert.Equal(has_puller, bob.pulling != null);
                MovableWorld.AssertNoRuntimes(map);
            }
        }
    }

    [Fact]
    public void AMoverWhosePullerIsZMovingChecksItsPullWithZAllowedAfterItsMove()
    {
        var map = MovableWorld.Create(5, 1);
        using (map.World.Enter())
        {
            var alice = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var bob = Dm.@new<mob_test_movable_mob>(locate(2, 1, 1));
            var charlie = Dm.@new<mob_test_movable_mob>(locate(3, 1, 1));
            Pull(alice, bob, supress: TRUE);
            Pull(bob, charlie, supress: TRUE);
            ((turf_test_movable_floor)locate(2, 1, 1)).enter_result = FALSE;
            alice.currently_z_moving = CURRENTLY_Z_MOVING_GENERIC; // `if (pulledby.currently_z_moving) check_pulling(z_allowed = TRUE)`

            AssertNumber(TRUE, Step(bob, WEST));

            Assert.Null(charlie.pulledby); // checked, and out of reach
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Move_Pulled (tg: modules/unit_tests/move_pulled.dm, adapted: a mob fixture and no click) ----

    [Fact]
    public void MovePulledRefusesToMoveTheCrateOntoThePullersOwnTurfWhenItIsDenseAndMovesItTowardsAFarTurfOtherwise()
    {
        var map = MovableWorld.Create(3, 3);
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1);
            var puller = Dm.@new<mob_test_movable_mob>(start);
            var crate = Dm.@new<obj_test_movable_block>(start); // a dense crate on the puller's turf
            Pull(puller, crate);
            Assert.Same(crate, puller.pulling);

            // "The crate should not have moved from clicking on the crate's turf." (move_pulled.dm:17)
            AssertNumber(FALSE, Waiting.NoSleep(puller.Move_Pulled(start)));
            Assert.Same(start, crate.loc);

            // "The crate should be located at the northeast of the puller." (:23)
            turf second = get_step(puller, NORTHEAST);
            AssertNumber(TRUE, Waiting.NoSleep(puller.Move_Pulled(second)));
            Assert.Same(second, crate.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovePulledOntoThePullersTurfIsOnlyRefusedForADenseThing()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf start = locate(1, 1, 1);
            var puller = Dm.@new<mob_test_movable_mob>(start);
            var rug = Dm.@new<obj_test_movable_witness>(start); // not dense
            Pull(puller, rug);
            map.Trace.Clear();

            // `if(get_turf(moving_atom) == loc && pulling.density) return FALSE` (:629): not dense, so it goes on; move_dir is 0
            // (get_dir of a turf to itself) and get_step(loc, 0) is the same turf, so Move gets its own loc and does nothing.
            AssertNumber(TRUE, Waiting.NoSleep(puller.Move_Pulled(start)));

            Assert.Same(start, rug.loc);
            Assert.Equal(new[] { "witness_move " + D(rug) + " " + D(start) + " 0" }, Lines(map.Trace, "witness_move"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovePulledStopsThePullOfAThingOutOfReachOrAnchoredOrTooHeavyAndFailsWithoutAPull()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            AssertNumber(FALSE, Waiting.NoSleep(puller.Move_Pulled(locate(2, 2, 1)))); // `!pulling`

            Pull(puller, crate);
            crate.move_resist = 1001; // > move_force
            AssertNumber(FALSE, Waiting.NoSleep(puller.Move_Pulled(locate(2, 2, 1))));
            Assert.Null(puller.pulling);
            crate.move_resist = MOVE_RESIST_DEFAULT;

            Pull(puller, crate);
            crate.loc = locate(3, 3, 1); // not adjacent any more
            AssertNumber(FALSE, Waiting.NoSleep(puller.Move_Pulled(locate(2, 2, 1))));
            Assert.Null(puller.pulling);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void MovePulledMovesTheThingToTheTurfBetweenAndPassesTheMoversGlideSize()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_witness>(locate(2, 1, 1));
            puller.glide_size = 6;
            Pull(puller, crate);
            map.Trace.Clear();

            // move_dir = get_dir(pulling.loc, moving_atom); pulling.Move(get_step(pulling.loc, move_dir), move_dir, glide_size)
            AssertNumber(TRUE, Waiting.NoSleep(puller.Move_Pulled(locate(2, 2, 1))));

            Assert.Same(locate(2, 2, 1), crate.loc);
            Assert.Equal(6f, crate.glide_size);
            Assert.Equal("witness_move " + D(crate) + " " + D(locate(2, 2, 1)) + " 1", Lines(map.Trace, "witness_move")[0]);
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
