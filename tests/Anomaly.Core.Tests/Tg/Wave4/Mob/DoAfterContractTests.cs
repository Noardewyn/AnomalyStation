using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Movable;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

// ANOM0001 (docs/ai/specs/waiting-model.md 5.6): these tests start do_afters they never await and watch them run (the
// timed_action, the cogbar, the interaction counts, the signals); dropping the task is the point, so the whole file
// suppresses the rule.
#pragma warning disable ANOM0001

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The contract of do_after and /datum/timed_action (datums/timed_actions.dm), one proc at a time, in a world where the
/// test steps the clock and fires SStimed_actions itself after the sleepers woke (<see cref="MobRoom.Tick"/>): the order
/// in which an await started inside the Master wakes before the subsystem polls (waiting-model.md 7.3, second row).
/// </summary>
public class DoAfterContractTests
{
    private static object Result(DmTask<object> task)
    {
        Assert.True(task.IsCompleted, "the do_after has not finished");
        Assert.True(task.TryGetResult(out object value));
        return value;
    }

    private static datum_timed_action Action() => (datum_timed_action)SStimed_actions.processing[1];

    /// <summary>Whether <paramref name="listener"/> is registered for <paramref name="signal"/> on <paramref name="source"/>.</summary>
    private static bool Listens(datum source, object signal, datum listener)
    {
        object entry = source._listen_lookup?[signal];
        if (entry == null)
            return false;
        if (entry is DmList list)
            return Dm.@in(listener, list);
        return ReferenceEquals(entry, listener);
    }

    private static datum_callback Check(datum_test_mob_check check) => Dm.@new<datum_callback>(check, nameof(datum_test_mob_check.check));

    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    // ---- order of the await and the subsystem ----

    [Fact]
    public void AnAwaitThatWakesBeforeTheSubsystemMakesTheLastPollItself()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_test_mob_check check = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            check = Dm.@new<datum_test_mob_check>();
            task = do_after(m, 1 * SECONDS, extra_checks: Check(check)); // started between two ticks, no poll yet
            Assert.Equal(0f, check.polls);
        });
        room.Tick(19); // each tick: the await wakes (still working, sleeps again), then SStimed_actions polls
        Assert.False(task.IsCompleted);
        Assert.Equal(19f, check.polls);
        room.World.Step(); // tick 20: the await wakes first, sees world.time >= end_time and polls itself (:110-111)
        Assert.True(Dm.eq(Result(task), TRUE));
        Assert.Equal(20f, check.polls);
        using (room.World.Enter())
        {
            Assert.Equal(0f, SStimed_actions.processing.len);
            SStimed_actions.state = SS_RUNNING;
            Waiting.NoSleep(SStimed_actions.fire(FALSE)); // the subsystem fires afterwards: nothing left to poll
        }
        Assert.Equal(20f, check.polls);
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADoAfterWhoseEndTimeIsBeforeTheNextTickEndsAtTheNextTick()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() => { task = do_after(NewAt<TgMob>(), 0.2f); });
        Assert.False(task.IsCompleted); // delay > 0: the loop sleeps one tick even though 0.2 ds is less than a tick
        room.World.Step();
        Assert.True(Dm.eq(Result(task), TRUE));
    }

    [Fact]
    public void AnInfiniteDoAfterWaitsForeverUntilSomethingCancelsIt()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            task = do_after(m, INFINITY); // 1e31: tg's INFINITY, not float.PositiveInfinity
            Assert.True(Dm.truthy(Action().cogbar));
        });
        room.Tick(200);
        Assert.False(task.IsCompleted);
        using (room.World.Enter())
            Assert.Equal(1f, SStimed_actions.processing.len);
        using (room.World.Enter())
            Waiting.NoSleep(step(m, EAST));
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    [Theory]
    [InlineData(1f, 1f, 0, 2)]
    [InlineData(1.2f, 1f, 0, 3)] // ceil(1.2 / 0.5)
    [InlineData(2.5f, 1f, 0, 5)]
    [InlineData(10f, 2f, 0, 40)] // a slowdown of 2 doubles the delay (timed_actions.dm:50)
    [InlineData(10f, 0.5f, 0, 10)] // and a speedup halves it
    [InlineData(10f, 2f, IGNORE_SLOWDOWNS, 20)]
    public void TheMobsActionSlowdownMultipliesTheDelayUnlessIgnored(float delay, float slowdown, int flags, int ticks)
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.cached_multiplicative_actions_slowdown = slowdown;
            task = do_after(m, delay, timed_action_flags: flags);
        });
        room.Tick(ticks - 1);
        Assert.False(task.IsCompleted);
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), TRUE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ANonMobUserIgnoresTheSlowdownFlagsInsideNewButKeepsThemOutOfItsSignals()
    {
        // tg quirk (timed_actions.dm:27 vs :44-50): New stores the flags in the var FIRST and then ORs IGNORE_HELD_ITEM,
        // IGNORE_INCAPACITATED and IGNORE_SLOWDOWNS into the parameter only, so register_signals (which reads the var)
        // still registers the held item and incapacitated signals for an obj.
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            var task = do_after(o, 1 * SECONDS);
            var a = Action();
            Assert.Equal((float)NONE, a.timed_action_flags);
            Assert.True(Listens(o, COMSIG_MOVABLE_MOVED, a));
            Assert.True(Listens(o, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), a));
            Assert.True(Listens(o, COMSIG_MOB_EQUIPPED_ITEM, a));
            Assert.True(Listens(o, COMSIG_MOB_SWAP_HANDS, a));
            Assert.True(Listens(o, COMSIG_MOB_UNEQUIPPED_ITEM, a));
            Assert.True(Dm.truthy(a.cogbar)); // 1 SECONDS and a default cog_icon
            qdel(a);
        });
    }

    [Fact]
    public void AnObjUsersDoAfterIsNotSlowedAndHasNoInteractionKey()
    {
        var room = new MobRoom();
        DmTask<object> first = default, second = default;
        datum_test_mob_do_after_listener listener = null;
        obj target = null;
        room.In(() =>
        {
            var o = NewAt<obj>();
            target = NewAt<obj>();
            listener = Dm.@new<datum_test_mob_do_after_listener>();
            listener.RegisterSignal(o, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
            first = do_after(o, 5, target);
            second = do_after(o, 5, target); // ismob(user) is false: no key, no cap (timed_actions.dm:222, :234)
            Assert.Equal(2f, SStimed_actions.processing.len);
        });
        Assert.Equal(new[] { "0:BEGAN", "0:BEGAN" }, listener.heard);
        room.Tick(10);
        Assert.True(Dm.eq(Result(first), TRUE));
        Assert.True(Dm.eq(Result(second), TRUE));
        Assert.Empty(room.RuntimeText);
    }

    // ---- guards ----

    [Fact]
    public void ANullUserFailsWithFalseNotNull()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            object r = Result(do_after(null, 10));
            Assert.NotNull(r);
            Assert.True(Dm.eq(r, FALSE));
            Assert.True(Dm.eq(Result(do_after(null, "ten")), FALSE)); // the guard runs before the asserts
        });
    }

    [Theory]
    [InlineData("ten", "do_after was passed a non-number delay: ten.")]
    [InlineData(null, "do_after was passed a non-number delay: null.")]
    public void ANonNumberDelayIsAnAssertionWithTgsMessage(object delay, string message)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var task = do_after(NewAt<TgMob>(), delay);
            Assert.True(task.IsFaulted);
            Assert.IsType<DmCrashException>(task.Exception);
            Assert.Contains("code/datums/timed_actions.dm:215:Assertion failed: isnum(delay) - " + message, task.Exception.Message);
        });
    }

    [Fact]
    public void ANumberAsTheTargetIsAnAssertionNamingTheUserAndTheTarget()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var task = do_after(m, 10, 5);
            Assert.True(task.IsFaulted);
            Assert.Contains("code/datums/timed_actions.dm:216:Assertion failed: !isnum(target) - a do_after created by the mob had a target set as 5 - probably intended to be the time instead.", task.Exception.Message);
            Assert.Null(m.do_afters); // the asserts run before the interaction count
        });
    }

    [Fact]
    public void AnAtomAsTheDelayIsAnAssertionNamingTheUserAndTheDelay()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var o = NewAt<obj>();
            var task = do_after(m, o);
            // isnum(delay) fails first for an atom: the message of the first assert is the one raised
            Assert.True(task.IsFaulted);
            Assert.Contains("code/datums/timed_actions.dm:215:Assertion failed: isnum(delay)", task.Exception.Message);
            Assert.Contains("do_after was passed a non-number delay: the obj.", task.Exception.Message);
        });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void ANonPositiveDelaySucceedsAtOnceWithNoSignalsAndNoCount(float delay)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var target = NewAt<obj>();
            var listener = Dm.@new<datum_test_mob_do_after_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
            listener.RegisterSignal(m, COMSIG_DO_AFTER_ENDED, nameof(datum_test_mob_do_after_listener.on_ended));
            Assert.True(Dm.eq(Result(do_after(m, delay, target)), TRUE)); // timed_actions.dm:219
            Assert.Empty(listener.heard);
            Assert.Null(m.do_afters);
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
    }

    // ---- interaction keys and the cap ----

    [Fact]
    public void TheInteractionKeyDefaultsToTheTargetByReference()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var b = NewAt<obj>();
            do_after(m, 1 * SECONDS, a);
            Assert.Equal(1f, m.do_afters.len);
            Assert.True(Dm.eq(m.do_afters[a], 1));
            Assert.Null(m.do_afters[b]);
            Assert.True(DOING_INTERACTION_WITH_TARGET_LIMIT(m, a, 1));
            Assert.False(DOING_INTERACTION_WITH_TARGET_LIMIT(m, b, 1));
            Assert.True(Dm.eq(DOING_INTERACTION(m, a), 1));
        });
    }

    [Fact]
    public void WithNoTargetThereIsNoKeyAndSoNoCap()
    {
        // tg quirk: `if(!interaction_key && ismob(user))` makes the key from the target, which is null here
        // (timed_actions.dm:222-223), so a do_after without a target is never capped.
        var room = new MobRoom();
        datum_test_mob_do_after_listener listener = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            listener = Dm.@new<datum_test_mob_do_after_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
            var first = do_after(m, 1 * SECONDS);
            var second = do_after(m, 1 * SECONDS);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.Equal(2f, SStimed_actions.processing.len);
            Assert.Null(m.do_afters);
        });
        Assert.Equal(2, listener.heard.Count);
    }

    [Fact]
    public void AListTargetsKeyIsTheSortedRefsJoinedWithDashesWhateverTheListOrder()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var b = NewAt<obj>();
            var first = do_after(m, 1 * SECONDS, Dm.list(a, b));
            string expected = string.Join("-", new[] { Dm.@ref(a), Dm.@ref(b) }.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(1f, m.do_afters.len);
            Assert.True(Dm.eq(m.do_afters[expected], 1));
            // the same atoms in the other order are the same interaction: capped (null)
            var capped = do_after(m, 1 * SECONDS, Dm.list(b, a));
            Assert.True(capped.IsCompleted);
            Assert.Null(Result(capped));
            // another pair is not
            var c = NewAt<obj>();
            var other = do_after(m, 1 * SECONDS, Dm.list(a, c));
            Assert.False(other.IsCompleted);
            Assert.Equal(2f, m.do_afters.len);
        });
    }

    [Fact]
    public void AnExplicitInteractionKeyCapsAcrossDifferentTargets()
    {
        var room = new MobRoom();
        datum_test_mob_do_after_listener listener = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var b = NewAt<obj>();
            listener = Dm.@new<datum_test_mob_do_after_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
            var first = do_after(m, 1 * SECONDS, a, interaction_key: DOAFTER_SOURCE_SURGERY);
            Assert.True(Dm.eq(m.do_afters[DOAFTER_SOURCE_SURGERY], 1));
            var capped = do_after(m, 1 * SECONDS, b, interaction_key: DOAFTER_SOURCE_SURGERY);
            Assert.Null(Result(capped));
            Assert.Null(m.do_afters[b]); // the target is not a key when an explicit key is given
            Assert.Single(listener.heard);
            Assert.True(DOING_INTERACTION_LIMIT(m, DOAFTER_SOURCE_SURGERY, 1));
            Assert.False(DOING_INTERACTION_LIMIT(m, DOAFTER_SOURCE_SURGERY, 2));
        });
    }

    [Fact]
    public void TgQuirk_AMaxInteractCountOfZeroRefusesEvenTheFirstDoAfter()
    {
        // LAZYACCESS of an absent key is null, and DM compares null as 0: `null >= 0` (timed_actions.dm:235).
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var task = do_after(m, 1 * SECONDS, a, max_interact_count: 0);
            Assert.True(task.IsCompleted);
            Assert.Null(Result(task));
            Assert.Null(m.do_afters);
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
    }

    [Fact]
    public void TheCountIsRaisedAndLoweredAroundTheAwait()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        TgMob m = null;
        obj a = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            a = NewAt<obj>();
            task = do_after(m, 5, a, max_interact_count: 3);
            do_after(m, 5, a, max_interact_count: 3);
            Assert.True(Dm.eq(m.do_afters[a], 2));
            Assert.False(DOING_INTERACTION_WITH_TARGET_LIMIT(m, a, 3));
            Assert.True(DOING_INTERACTION_WITH_TARGET_LIMIT(m, a, 2));
        });
        room.Tick(10);
        Assert.True(Dm.eq(Result(task), TRUE));
        using (room.World.Enter())
            Assert.Null(m.do_afters);
    }

    // ---- targets ----

    [Fact]
    public void TheTargetsAreNormalisedToAListAndACallerListIsUsedByReference()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var b = NewAt<obj>();

            // no target: the user is the only target (:29-30)
            do_after(m, 1 * SECONDS);
            var userOnly = Action();
            Assert.Equal(1f, userOnly.targets.len);
            Assert.Same(m, userOnly.targets[1]);
            qdel(userOnly);

            // one target: a one item list (:31-32)
            do_after(m, 1 * SECONDS, a);
            var single = Action();
            Assert.Equal(1f, single.targets.len);
            Assert.Same(a, single.targets[1]);
            qdel(single);

            // a list: the caller's own list (:34)
            var list = Dm.list(a, b);
            do_after(m, 1 * SECONDS, list);
            var listed = Action();
            Assert.Same(list, listed.targets);
            qdel(listed);
        });
    }

    [Fact]
    public void ADeletedTargetOfACallersListIsTakenOutOfThatListToo()
    {
        // tg quirk (timed_actions.dm:34, :141): the action keeps the caller's list, and on_target_deleted does `targets -= source`.
        var room = new MobRoom();
        DmTask<object> task = default;
        DmList list = null;
        obj a = null, b = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            a = NewAt<obj>();
            b = NewAt<obj>();
            list = Dm.list(a, b);
            task = do_after(m, 1 * SECONDS, list);
            var action = Action();
            qdel(a);
            Assert.Equal(1f, list.len);
            Assert.Same(b, list[1]);
            Assert.Equal(1f, action.targets.len);
            Assert.Equal(0f, SStimed_actions.processing.len); // cancel() stopped it
        });
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADeletedTargetCancelsAtOnceAndTheAwaitNoticesAtItsNextWake()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            var a = NewAt<obj>();
            task = do_after(m, 1 * SECONDS, a);
            Assert.False(task.IsCompleted);
            qdel(a);
            Assert.False(task.IsCompleted);
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        using (room.World.Enter())
            Assert.Null(m.do_afters);
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADeletedUserIsNulledOutOfTheActionAndTheDoAfterFailsWithoutARuntime()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_timed_action action = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            task = do_after(m, 1 * SECONDS, a);
            action = Action();
            qdel(m);
            Assert.Null(action.user); // on_user_deleted (:134-137)
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        using (room.World.Enter())
            Assert.True(QDELETED(action));
        Assert.Empty(room.RuntimeText);
    }

    // ---- movement signals ----

    [Fact]
    public void TgQuirk_ATargetThatMovesCancelsEvenWhenItStaysAdjacent()
    {
        // on_target_moved cancels when `isnull(source.drift_handler) || !source.Adjacent(user)` (timed_actions.dm:189): a
        // target without a drift handler is never allowed to move, adjacent or not.
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            task = do_after(m, 1 * SECONDS, a);
            Assert.True(Dm.truthy(Waiting.NoSleep(a.forceMove(MobRoom.T(4, 3))))); // still adjacent to the user
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ATargetForceMovedToItsOwnLocDoesNotCancel()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            task = do_after(m, 1 * SECONDS, a);
            Assert.True(Dm.truthy(Waiting.NoSleep(a.forceMove(a.loc))));
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
        room.Tick(20);
        Assert.True(Dm.eq(Result(task), TRUE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void IgnoreTargetLocChangeLetsTheTargetMove()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            task = do_after(m, 1 * SECONDS, a, timed_action_flags: IGNORE_TARGET_LOC_CHANGE);
            var action = Action();
            Assert.False(Listens(a, COMSIG_MOVABLE_MOVED, action));
            Assert.True(Listens(a, COMSIG_QDELETING, action)); // deletion still cancels
            Waiting.NoSleep(a.forceMove(MobRoom.T(6, 6)));
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
        room.Tick(20);
        Assert.True(Dm.eq(Result(task), TRUE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AUserWithADriftHandlerMayMoveWhileItStaysAdjacentToTheTargetsAndCancelsOtherwise()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>(3, 3);
            var a = NewAt<obj>(3, 3);
            m.drift_handler = Dm.@new<datum_drift_handler_test_movable>(); // the user drifts in zero gravity (M4)
            task = do_after(m, 1 * SECONDS, a);
            // one step: the target is still adjacent: on_user_moved loops over the targets (:177-181)
            Assert.True(Dm.truthy(Waiting.NoSleep(step(m, EAST))));
            Assert.Equal(1f, SStimed_actions.processing.len);
            // two steps away: not adjacent any more
            Assert.True(Dm.truthy(Waiting.NoSleep(step(m, EAST))));
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        using (room.World.Enter())
            m.drift_handler = null;
        Assert.Empty(room.RuntimeText);
    }

    // ---- mob signals ----

    [Fact]
    public void ATraitIncapacitatedAddedToTheUserCancels()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            task = do_after(m, 1 * SECONDS);
            Assert.True(Listens(m, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), Action()));
            ADD_TRAIT(m, TRAIT_INCAPACITATED, "test");
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
    }

    [Fact]
    public void TgQuirk_AUserAlreadyIncapacitatedWhenTheDoAfterBeginsIsNotCancelledByAnotherSource()
    {
        // SIGNAL_ADDTRAIT is only sent when a trait first appears: the action checks no state at its start, only the signal.
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            ADD_TRAIT(m, TRAIT_INCAPACITATED, "first");
            task = do_after(m, 1 * SECONDS);
            ADD_TRAIT(m, TRAIT_INCAPACITATED, "second"); // a second source of a trait the mob has: no signal
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
        room.Tick(20);
        Assert.True(Dm.eq(Result(task), TRUE));
    }

    [Fact]
    public void TgQuirk_AUserWhoDiesDuringADoAfterIsNotCancelledByItsStat()
    {
        // no COMSIG_MOB_STATCHANGE is registered by register_signals (timed_actions.dm:71-93)
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            task = do_after(m, 1 * SECONDS);
            Waiting.NoSleep(m.set_stat(DEAD));
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
        room.Tick(20);
        Assert.True(Dm.eq(Result(task), TRUE));
    }

    [Fact]
    public void IgnoreIncapacitatedKeepsTheActionGoing()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            task = do_after(m, 1 * SECONDS, timed_action_flags: IGNORE_INCAPACITATED);
            Assert.False(Listens(m, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), Action()));
            ADD_TRAIT(m, TRAIT_INCAPACITATED, "test");
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
        room.Tick(20);
        Assert.True(Dm.eq(Result(task), TRUE));
    }

    [Fact]
    public void TgQuirk_AnObjUserIsCancelledByATraitIncapacitatedBecauseTheFlagsStayOutOfRegisterSignals()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var o = NewAt<obj>();
            task = do_after(o, 1 * SECONDS);
            ADD_TRAIT(o, TRAIT_INCAPACITATED, "test");
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
    }

    [Fact]
    public void TheChangeNextMoveSignalIsOnlyRegisteredWithItsFlagAndCancelsOnlyForALaterMove()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 1 * SECONDS);
            Assert.False(Listens(m, COMSIG_LIVING_CHANGENEXT_MOVE, Action()));
            qdel(Action());

            do_after(m, 1 * SECONDS, timed_action_flags: DO_AFTER_CHECK_NEXT_MOVE);
            var action = Action();
            Assert.True(Listens(m, COMSIG_LIVING_CHANGENEXT_MOVE, action));
            m._SendSignal(COMSIG_LIVING_CHANGENEXT_MOVE, Dm.list(m, world.time, 10)); // next_move == world.time: not later
            Assert.Equal(1f, SStimed_actions.processing.len);
            m._SendSignal(COMSIG_LIVING_CHANGENEXT_MOVE, Dm.list(m, Dm.add(world.time, 5), 10));
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
    }

    [Fact]
    public void ASwapOfHandsCancelsUnlessHeldItemsAreIgnored()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 1 * SECONDS);
            m._SendSignal(COMSIG_MOB_SWAP_HANDS, Dm.list(m));
            Assert.Equal(0f, SStimed_actions.processing.len);

            do_after(m, 1 * SECONDS, timed_action_flags: IGNORE_HELD_ITEM);
            var ignoring = Action();
            Assert.False(Listens(m, COMSIG_MOB_SWAP_HANDS, ignoring));
            Assert.False(Listens(m, COMSIG_MOB_EQUIPPED_ITEM, ignoring));
            Assert.False(Listens(m, COMSIG_MOB_UNEQUIPPED_ITEM, ignoring));
            m._SendSignal(COMSIG_MOB_SWAP_HANDS, Dm.list(m));
            Assert.Equal(1f, SStimed_actions.processing.len);
        });
    }

    [Fact]
    public void AnItemEquippedCancelsOnlyWhenItIsTheActiveHeldItem()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_holder>();
            var held = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            var other = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            m.active_item = held;
            do_after(m, 1 * SECONDS);
            m._SendSignal(COMSIG_MOB_EQUIPPED_ITEM, Dm.list(m, other, ITEM_SLOT_BACK));
            Assert.Equal(1f, SStimed_actions.processing.len); // not the active held item
            m._SendSignal(COMSIG_MOB_EQUIPPED_ITEM, Dm.list(m, held, ITEM_SLOT_HANDS));
            Assert.Equal(0f, SStimed_actions.processing.len); // "We picked up an item" (:155-157)
        });
    }

    [Fact]
    public void AnItemDroppedCancelsOnlyWhenItWasInTheActiveHand()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var item = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            m.active_hand_index = 2;
            do_after(m, 1 * SECONDS);
            // args: item_dropping, force, newloc, no_move, invdrop, silent, hand_index (timed_actions.dm:163)
            m._SendSignal(COMSIG_MOB_UNEQUIPPED_ITEM, Dm.list(m, item, FALSE, null, FALSE, TRUE, FALSE, 1));
            Assert.Equal(1f, SStimed_actions.processing.len);
            m._SendSignal(COMSIG_MOB_UNEQUIPPED_ITEM, Dm.list(m, item, FALSE, null, FALSE, TRUE, FALSE, 2));
            Assert.Equal(0f, SStimed_actions.processing.len);
        });
    }

    // ---- register_signals by flags ----

    public static TheoryData<int, bool, bool, bool, bool, bool> UserSignals() => new()
    {
        // flags, moved, incapacitated, items (three), next move, target moved
        { NONE, true, true, true, false, true },
        { IGNORE_USER_LOC_CHANGE, false, true, true, false, true },
        { IGNORE_INCAPACITATED, true, false, true, false, true },
        { IGNORE_HELD_ITEM, true, true, false, false, true },
        { DO_AFTER_CHECK_NEXT_MOVE, true, true, true, true, true },
        { IGNORE_TARGET_LOC_CHANGE, true, true, true, false, false },
        { IGNORE_USER_LOC_CHANGE | IGNORE_INCAPACITATED | IGNORE_HELD_ITEM | IGNORE_TARGET_LOC_CHANGE, false, false, false, false, false },
    };

    [Theory]
    [MemberData(nameof(UserSignals))]
    public void TheFlagsDecideWhichSignalsTheActionRegistersOnTheUserAndTheTarget(int flags, bool moved, bool incapacitated, bool items, bool nextMove, bool targetMoved)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var target = NewAt<obj>();
            do_after(m, 1 * SECONDS, target, timed_action_flags: flags);
            var a = Action();
            Assert.True(Listens(m, COMSIG_QDELETING, a)); // always (:72)
            Assert.Equal(moved, Listens(m, COMSIG_MOVABLE_MOVED, a));
            Assert.Equal(incapacitated, Listens(m, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), a));
            Assert.Equal(items, Listens(m, COMSIG_MOB_EQUIPPED_ITEM, a));
            Assert.Equal(items, Listens(m, COMSIG_MOB_SWAP_HANDS, a));
            Assert.Equal(items, Listens(m, COMSIG_MOB_UNEQUIPPED_ITEM, a));
            Assert.Equal(nextMove, Listens(m, COMSIG_LIVING_CHANGENEXT_MOVE, a));
            Assert.True(Listens(target, COMSIG_QDELETING, a)); // a target other than the user is always watched for deletion (:90)
            Assert.Equal(targetMoved, Listens(target, COMSIG_MOVABLE_MOVED, a));
        });
    }

    [Fact]
    public void TheUserAsItsOwnTargetIsRegisteredOnceNotAsATarget()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 1 * SECONDS, m);
            var a = Action();
            Assert.Equal(1f, a.targets.len);
            Assert.True(Listens(m, COMSIG_QDELETING, a));
            // `if (target == user) continue` (:87-88): no second registration of the action. (M4b audio slice 1: the mob's
            // hearing membership also listens while the spatial grid is not initialized, spatial_gridmap.dm:145, so the entry
            // may be a list; the action is in it once.)
            object listeners = m._listen_lookup[COMSIG_QDELETING];
            Assert.Equal(1, listeners is DmList l ? Dm.each(l).Cast<object>().Count(x => ReferenceEquals(x, a)) : (ReferenceEquals(listeners, a) ? 1 : 0));
        });
    }

    // ---- the action itself ----

    [Fact]
    public void CancelFailsAWorkingActionOnceAndStopsItsProcessing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 1 * SECONDS);
            var a = Action();
            Assert.Equal(0f, a.status);
            Assert.True(Dm.eq(a.cancel(), TRUE));
            Assert.Equal(1f, a.status);
            Assert.Equal(0f, SStimed_actions.processing.len);
            Assert.False(Dm.truthy(Dm.bitand(a.datum_flags, DF_ISPROCESSING)));
            Assert.True(Dm.eq(a.cancel(), FALSE)); // "if (status != ACTION_WORKING) return FALSE"
            Assert.Equal(1f, a.status);
        });
    }

    [Fact]
    public void ProcessReturnsProcessKillOnlyWhenTheEndTimeCameAndAFailedExtraCheckCancels()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var check = Dm.@new<datum_test_mob_check>();
            do_after(m, 1 * SECONDS, extra_checks: Check(check));
            var a = Action();
            Assert.Null(a.process(0.1f)); // before the end time
            Assert.Equal(0f, a.status);
            Assert.Equal(1f, check.polls);
            check.answer = FALSE;
            Assert.Null(a.process(0.1f)); // the failed check cancels and returns at once (:123-125)
            Assert.Equal(1f, a.status);
            Assert.Equal(2f, check.polls);
        });
    }

    [Fact]
    public void ProcessSucceedsAndReturnsProcessKillWhenTheEndTimeCame()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 5);
            var a = Action();
            a.end_time = world.time; // the end time is now
            Assert.True(Dm.eq(a.process(0.1f), PROCESS_KILL));
            Assert.Equal(2f, a.status); // ACTION_SUCCEEDED
        });
    }

    [Fact]
    public void TheActionIsDeletedWhenTheDoAfterReturnsAndItsSignalsAreGone()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_timed_action action = null;
        TgMob m = null;
        obj target = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            target = NewAt<obj>();
            task = do_after(m, 5, target);
            action = Action();
        });
        room.Tick(10);
        Assert.True(Dm.eq(Result(task), TRUE));
        using (room.World.Enter())
        {
            Assert.True(QDELETED(action));
            Assert.Null(action.user);
            Assert.Null(action.targets);
            Assert.Null(action.extra_checks);
            Assert.Null(action.cogbar);
            Assert.False(Listens(m, COMSIG_MOVABLE_MOVED, action));
            Assert.False(Listens(target, COMSIG_QDELETING, action));
            // so that a later move of the user reaches no dead handler (which would read user.loc of null)
            Assert.True(Dm.truthy(Waiting.NoSleep(step(m, EAST))));
        }
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DeletingTheActionWhileItWorksDeletesItsCogbarAndTheDoAfterFailsWithoutARuntime()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_cogbar cogbar = null;
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            task = do_after(m, 1 * SECONDS);
            var action = Action();
            cogbar = action.cogbar;
            Assert.False(QDELETED(cogbar));
            qdel(action); // Destroy while ACTION_WORKING (:63-66)
            Assert.True(QDELETED(cogbar));
            Assert.Null(action.cogbar);
            Assert.Equal(1f, action.status); // ACTION_FAILED
            Assert.Equal(0f, SStimed_actions.processing.len);
            Assert.False(Listens(m, COMSIG_MOVABLE_MOVED, action));
            Waiting.NoSleep(step(m, EAST)); // the dead action's handlers are gone: no runtime
        });
        room.World.Step();
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    // ---- extra_checks ----

    [Fact]
    public void AFailedExtraCheckCancelsAtTheFirstPollAndNeverPollsAgain()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        datum_test_mob_check check = null;
        room.In(() =>
        {
            check = Dm.@new<datum_test_mob_check>();
            check.answer = FALSE;
            task = do_after(NewAt<TgMob>(), 1 * SECONDS, extra_checks: Check(check));
        });
        room.Tick(1); // the await wakes (working), the subsystem polls: the check fails
        Assert.False(task.IsCompleted);
        Assert.Equal(1f, check.polls);
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Equal(1f, check.polls);
    }

    [Fact]
    public void TgQuirk_ACheckThatSleepsIsFalsyAndCancels()
    {
        // extra_checks.InvokeAsync() returns the check's `.` at its first sleep, which is null (timed_actions.dm:123).
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var check = Dm.@new<datum_test_mob_check_ext>();
            task = do_after(NewAt<TgMob>(), 1 * SECONDS, extra_checks: Dm.@new<datum_callback>(check, nameof(datum_test_mob_check_ext.sleepy_check)));
        });
        room.Tick(2);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ACheckThatRuntimesIsFalsyCancelsAndIsReportedOnce()
    {
        var room = new MobRoom();
        DmTask<object> task = default;
        room.In(() =>
        {
            var check = Dm.@new<datum_test_mob_check_ext>();
            task = do_after(NewAt<TgMob>(), 1 * SECONDS, extra_checks: Dm.@new<datum_callback>(check, nameof(datum_test_mob_check_ext.broken_check)));
        });
        room.Tick(2);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Single(room.Runtimes);
        Assert.Contains("broken check", room.Runtimes[0].Message);
    }

    [Fact]
    public void AnObjsUnfastenWrenchCheckAsTheExtraCheckCancelsWhenTheAnchoringChanges()
    {
        // default_unfasten_wrench (objs.dm:287-313) passes CALLBACK(src, PROC_REF(unfasten_wrench_check), prev_anchored, user)
        var room = new MobRoom();
        DmTask<object> task = default;
        obj_test_mob_wrenchable thing = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            thing = NewAt<obj_test_mob_wrenchable>();
            object prev_anchored = thing.anchored;
            task = do_after(m, 1 * SECONDS, thing, extra_checks: Dm.@new<datum_callback>(thing, nameof(obj.unfasten_wrench_check), prev_anchored, m));
        });
        room.Tick(3);
        Assert.False(task.IsCompleted);
        using (room.World.Enter())
            thing.anchored = TRUE; // somebody else anchored it meanwhile
        room.Tick(1); // the subsystem's poll fails the check and cancels; the await, which woke before it, sees that next tick
        Assert.False(task.IsCompleted);
        room.Tick(1);
        Assert.True(Dm.eq(Result(task), FALSE));
        Assert.Empty(room.RuntimeText);
    }

    // ---- the seam: cogbar and progress bar ----

    [Fact]
    public void ShowProgressFalseMakesNoCogbar()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            int before = DmWorldState.Of(room.World).DeferredCount;
            do_after(m, 5 * SECONDS, show_progress: FALSE);
            Assert.Null(Action().cogbar);
            Assert.Equal(before, DmWorldState.Of(room.World).DeferredCount);
        });
    }

    [Fact]
    public void ACogbarIsMadeFromOneSecondUpAndNotBelow()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 9.9f, NewAt<obj>());
            Assert.Null(Action().cogbar);
            qdel(Action());
            do_after(m, 10, NewAt<obj>());
            Assert.NotNull(Action().cogbar); // delay >= 1 SECONDS (:40)
        });
    }

    [Fact]
    public void ACogIconOfNullStillGetsTheDefaultCogUnderTheDefaultValueRule()
    {
        // Pinned per translation-rules.md 7.2 (a DM default applies to an omitted or null argument, OpenDream's
        // behaviour), which the translator's notes record as differing from scope.md's "cog_icon = null never touches it".
        // tg's callers (kitchen_spike, alien_nest, ...) pass cog_icon = null to avoid the cog. Not checked against a BYOND run.
        var room = new MobRoom();
        room.In(() =>
        {
            do_after(NewAt<TgMob>(), 5 * SECONDS, cog_icon: null);
            Assert.NotNull(Action().cogbar);
        });
    }

    [Fact]
    public void AProgressBarIsOnlyMadeForAClientAndShowsOnTheBarOverridesClient()
    {
        // timed_actions.dm:37-38: a progress bar only when the user or the bar_override has a client; it is the
        // bar_override's (the M2 integration translated /datum/progressbar, which was DV13's NotTranslated seam).
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            do_after(m, 5);
            Assert.Null(Action().progressbar);
            var watcher = NewAt<TgMob>(5, 5);
            watcher.client = Dm.@new<client_test_m1_client>();
            var task = do_after(m, 5, bar_override: watcher);
            Assert.False(task.IsFaulted);
            datum_progressbar bar = ((datum_timed_action)SStimed_actions.processing[2]).progressbar;
            Assert.Same(watcher, bar.user);
            Assert.True(Dm.@in(bar.bar, watcher.client.images));
            watcher.client = null;
        });
    }

    [Fact]
    public void TgQuirk_ARuntimeInTheTimedActionsNewLeaksTheInteractionCount()
    {
        // do_after has no try/finally (timed-actions-helpers.md section 1): a runtime between the count and its decrement
        // leaves the count raised, and the key capped, for good; COMSIG_DO_AFTER_ENDED is never sent.
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<obj>();
            var listener = Dm.@new<datum_test_mob_do_after_listener>();
            listener.RegisterSignal(m, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
            listener.RegisterSignal(m, COMSIG_DO_AFTER_ENDED, nameof(datum_test_mob_do_after_listener.on_ended));
            var watcher = NewAt<TgMob>(5, 5);
            watcher.client = Dm.@new<client_test_m1_client>();
            // a runtime inside the timed action's New: the progress bar's target fails (a fixture of the port's own)
            var bad = NewAt<obj_test_mob_bad_icon>();
            var task = do_after(m, 5, Dm.list(bad, a), bar_override: watcher, interaction_key: a);
            Assert.True(task.IsFaulted);
            Assert.True(Dm.eq(m.do_afters[a], 1)); // leaked
            Assert.Equal(new[] { "0:BEGAN" }, listener.heard);
            watcher.client = null;
            // the key is capped from now on
            Assert.Null(Result(do_after(m, 5, a)));
        });
    }
}

/// <summary>An obj whose oversized icon offsets runtime: a progress bar on it fails in its New (a fixture of the port's own).</summary>
[DmPath("/obj/test_mob_bad_icon")]
public class obj_test_mob_bad_icon : obj
{
    public override object get_oversized_icon_offsets() => throw Dm.CRASH("a test fixture's runtime", "code/__HELPERS/atoms.dm", 419);
}

/// <summary>A wrenchable obj: the unwrenching checks of objs.dm say yes (can_be_unfasten_wrench is construction, M5).</summary>
[DmPath("/obj/test_mob_wrenchable")]
public class obj_test_mob_wrenchable : obj
{
    public override object can_be_unfasten_wrench(mob user = null, object silent = null) => SUCCESSFUL_UNFASTEN;
}
