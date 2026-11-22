using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The do_after scenarios of scope.md section 3 C (a) to (f), written from datums/timed_actions.dm and the timelines of
/// docs/ai/specs/m0-reading/timed-actions-helpers.md (sections 1 and 8) and waiting-model.md 7.3. They run in a booted world
/// with the real Master: SStimed_actions polls once per tick, and the player's command is a world phase (BYOND's step 6,
/// after the sleepers and the Master woke), so the Master polls before the awaiting proc wakes in every later tick. T is
/// the tick of the command; one second is 10 ds, which is 20 ticks of 0.5 ds.
/// </summary>
public class DoAfterScenarioTests
{
    /// <summary>A booted room, a mob with tg's actionspeed (slowdown 1), a target obj, a listener on the mob and a script of commands by tick.</summary>
    private sealed class Rig
    {
        public readonly BootedRoom Room;
        public TgMob M;
        public obj Target;
        public datum_test_mob_check Check;
        public datum_test_mob_do_after_listener Listener;
        public readonly long T;
        private readonly Dictionary<long, List<Action>> script = new();

        public Rig(ulong seed = 5)
        {
            Room = new BootedRoom(seed);
            Room.In(() =>
            {
                M = Dm.@new<TgMob>(locate(3, 3, 1));
                Target = Dm.@new<obj>(locate(3, 3, 1));
                Check = Dm.@new<datum_test_mob_check>();
                Listener = Dm.@new<datum_test_mob_do_after_listener>();
                Listener.RegisterSignal(M, COMSIG_DO_AFTER_BEGAN, nameof(datum_test_mob_do_after_listener.on_began));
                Listener.RegisterSignal(M, COMSIG_DO_AFTER_ENDED, nameof(datum_test_mob_do_after_listener.on_ended));
            });
            T = Room.World.Tick + 1;
            Room.World.AddPhase("player commands", w =>
            {
                if (script.TryGetValue(w.Tick, out var actions))
                    foreach (var action in actions)
                        action();
            });
        }

        /// <summary>Runs <paramref name="command"/> in the command phase of tick T + <paramref name="offset"/>.</summary>
        public void At(long offset, Action command)
        {
            if (!script.TryGetValue(T + offset, out var actions))
                script[T + offset] = actions = new List<Action>();
            actions.Add(command);
        }

        public datum_callback Counting() => Dm.@new<datum_callback>(Check, nameof(datum_test_mob_check.check));

        /// <summary>Steps the world until tick T + <paramref name="offset"/> has run.</summary>
        public void RunTo(long offset)
        {
            while (Room.World.Tick < T + offset)
                Room.World.Step();
        }

        public DmList Processing => SStimed_actions.processing;
    }

    // ---- (a) completes ----

    [Fact]
    public void ScenarioA_AOneSecondDoAfterStartedByAPlayerCommandCompletesTrueAtTPlus20WithTwentyPolls()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting())));

        rig.RunTo(0);
        Assert.False(call.Done);
        rig.Room.In(() =>
        {
            // the command phase runs after the Master fired this tick: the action is registered but not polled yet
            Assert.Equal(1f, rig.Processing.len);
            Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 1));
            Assert.Equal(0f, rig.Check.polls);
            Assert.True(Dm.truthy(Dm.bitand(((datum_timed_action)rig.Processing[1]).datum_flags, DF_ISPROCESSING)));
        });

        rig.RunTo(1);
        rig.Room.In(() => Assert.Equal(1f, rig.Check.polls)); // the Master polls first in every later tick (waiting-model.md 7.3)

        rig.RunTo(19);
        Assert.False(call.Done);
        rig.Room.In(() => Assert.Equal(19f, rig.Check.polls));

        rig.RunTo(20);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 20, call.DoneTick);
        Assert.True(Dm.eq(call.Result, TRUE));
        rig.Room.In(() =>
        {
            Assert.Equal(20f, rig.Check.polls); // the 20th poll, by the Master, saw world.time >= end_time (timed_actions.dm:128)
            Assert.Equal(0f, rig.Processing.len); // process() returned PROCESS_KILL, then qdel(src) -> STOP_PROCESSING
            Assert.Null(rig.M.do_afters); // LAZYREMOVE emptied the list (UNSETEMPTY)
        });
        Assert.Equal(new[] { $"{rig.T}:BEGAN", $"{rig.T + 20}:ENDED" }, rig.Listener.heard);

        // nothing else polls afterwards
        rig.RunTo(25);
        rig.Room.In(() => Assert.Equal(20f, rig.Check.polls));
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioA_TheCogbarOfAOneSecondDoAfterIsMadeAndDeletedItselfWithoutACog()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        datum_cogbar cogbar = null;
        rig.At(0, () =>
        {
            call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target));
            cogbar = ((datum_timed_action)rig.Processing[1]).cogbar;
        });
        rig.RunTo(0);
        rig.Room.In(() =>
        {
            Assert.NotNull(cogbar); // delay >= 1 SECONDS and the default cog_icon is non-null (timed_actions.dm:40)
            Assert.False(QDELETED(cogbar));
            Assert.Null(((datum_timed_action)rig.Processing[1]).progressbar); // no client: no progress bar (:37)
            Assert.Contains("code/datums/cogbar.dm:24", DmWorldState.Of(rig.Room.World).DeferredSites); // the DV13 seam: New is deferred
        });
        rig.RunTo(20);
        Assert.True(call.Done);
        rig.Room.In(() => Assert.True(QDELETED(cogbar))); // cogbar.remove() with no cog: qdel(src) (cogbar.dm:84-86)
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioA_AShortDoAfterMakesNoCogbar()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 9, rig.Target)));
        rig.RunTo(0);
        rig.Room.In(() =>
        {
            Assert.Null(((datum_timed_action)rig.Processing[1]).cogbar); // 9 ds < 1 SECONDS (timed_actions.dm:40)
        });
        rig.RunTo(18);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 18, call.DoneTick);
        Assert.True(Dm.eq(call.Result, TRUE));
        Assert.Empty(rig.Room.RuntimeText);
    }

    // ---- (b) a step cancels ----

    [Fact]
    public void ScenarioB_AStepAfterTPlus5CancelsTheDoAfterWithFalseAtTPlus6()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        object stepped = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting())));
        rig.At(5, () => stepped = Waiting.NoSleep(step(rig.M, EAST)));

        rig.RunTo(5);
        Assert.False(call.Done);
        rig.Room.In(() =>
        {
            Assert.NotNull(stepped); // step returns what Move returned (TRUE: the move happened)
            Assert.True(Dm.truthy(stepped));
            Assert.Same(locate(4, 3, 1), rig.M.loc);
            // Moved reached on_user_moved synchronously inside the chain: cancel() = FAILED + STOP_PROCESSING (timed_actions.dm:95-101)
            Assert.Equal(0f, rig.Processing.len);
            Assert.False(Dm.truthy(Dm.bitand(rig.M.datum_flags, DF_ISPROCESSING)));
            Assert.Equal(5f, rig.Check.polls);
            Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 1)); // the awaiting proc has not noticed yet
        });

        rig.RunTo(6); // the awaiting proc wakes: one tick of cancel latency
        Assert.True(call.Done);
        Assert.Equal(rig.T + 6, call.DoneTick);
        Assert.True(Dm.eq(call.Result, FALSE)); // FALSE, not null
        Assert.NotNull(call.Result);
        rig.Room.In(() =>
        {
            Assert.Null(rig.M.do_afters); // cleared (timed_actions.dm:245-251)
            Assert.Equal(5f, rig.Check.polls); // no poll after the cancel
        });
        Assert.Equal(new[] { $"{rig.T}:BEGAN", $"{rig.T + 6}:ENDED" }, rig.Listener.heard); // COMSIG_DO_AFTER_ENDED is sent on failure too
        rig.RunTo(25);
        rig.Room.In(() => Assert.Equal(5f, rig.Check.polls));
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioB_AStepInTheSameTickAsTheCommandCancelsWithFalseAtTPlus1()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        rig.At(0, () =>
        {
            call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting()));
            Waiting.NoSleep(step(rig.M, NORTH));
        });
        rig.RunTo(1);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 1, call.DoneTick);
        Assert.True(Dm.eq(call.Result, FALSE));
        rig.Room.In(() => Assert.Equal(0f, rig.Check.polls)); // cancelled before the Master's first poll
        Assert.Empty(rig.Room.RuntimeText);
    }

    // ---- (c) the cap ----

    [Fact]
    public void ScenarioC_TheCapReturnsNullAndSendsNoBeganForTheSecondDoAfterOnTheSameTarget()
    {
        var rig = new Rig();
        DoAfterCall first = null, second = null;
        rig.At(0, () =>
        {
            first = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target));
            second = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)); // max_interact_count = 1 (default)
        });
        rig.RunTo(0);
        Assert.True(second.Done); // a bare `return` before anything sleeps (timed_actions.dm:236)
        Assert.Equal(rig.T, second.DoneTick);
        Assert.Null(second.Result); // null, not FALSE
        Assert.False(first.Done);
        Assert.Equal(new[] { $"{rig.T}:BEGAN" }, rig.Listener.heard); // the capped one sent no BEGAN (and will send no ENDED)
        rig.Room.In(() =>
        {
            Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 1)); // the capped call did not raise the count
            Assert.Equal(1f, rig.Processing.len); // and made no timed action
        });
        rig.RunTo(20);
        Assert.True(Dm.eq(first.Result, TRUE));
        Assert.Equal(new[] { $"{rig.T}:BEGAN", $"{rig.T + 20}:ENDED" }, rig.Listener.heard);
        rig.Room.In(() => Assert.Null(rig.M.do_afters));
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioC_ADifferentTargetIsNotCappedAndBothRunInParallel()
    {
        var rig = new Rig();
        DoAfterCall first = null, second = null;
        obj other = null;
        rig.At(0, () =>
        {
            other = Dm.@new<obj>(locate(3, 3, 1));
            first = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target));
            second = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, other));
        });
        rig.RunTo(0);
        Assert.False(second.Done);
        Assert.Equal(new[] { $"{rig.T}:BEGAN", $"{rig.T}:BEGAN" }, rig.Listener.heard);
        rig.Room.In(() =>
        {
            Assert.Equal(2f, rig.Processing.len);
            Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 1));
            Assert.True(Dm.eq(rig.M.do_afters[other], 1));
        });
        rig.RunTo(20);
        Assert.True(Dm.eq(first.Result, TRUE));
        Assert.True(Dm.eq(second.Result, TRUE));
        rig.Room.In(() =>
        {
            Assert.Null(rig.M.do_afters);
            Assert.Equal(0f, rig.Processing.len);
        });
        Assert.Equal(new[] { $"{rig.T}:BEGAN", $"{rig.T}:BEGAN", $"{rig.T + 20}:ENDED", $"{rig.T + 20}:ENDED" }, rig.Listener.heard);
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioC_MaxInteractCountTwoAllowsTwoAndTheCountStepsDownAsEachEnds()
    {
        var rig = new Rig();
        DoAfterCall first = null, second = null, third = null;
        rig.At(0, () => first = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, max_interact_count: 2)));
        rig.At(3, () =>
        {
            second = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, max_interact_count: 2));
            third = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, max_interact_count: 2));
        });
        rig.RunTo(3);
        Assert.True(third.Done);
        Assert.Null(third.Result); // 2 >= 2: at the cap
        rig.Room.In(() => Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 2)));
        rig.RunTo(21); // the first ended at T+20: "Not done yet!" -> the count goes 2 -> 1, the key stays (timed_actions.dm:246-248)
        Assert.True(first.Done);
        Assert.False(second.Done);
        rig.Room.In(() => Assert.True(Dm.eq(rig.M.do_afters[rig.Target], 1)));
        rig.RunTo(23);
        Assert.True(second.Done);
        Assert.True(Dm.eq(second.Result, TRUE));
        rig.Room.In(() => Assert.Null(rig.M.do_afters));
        Assert.Empty(rig.Room.RuntimeText);
    }

    // ---- (d) determinism ----

    /// <summary>One run of a busy scenario: several mobs, a cancel, a cap, a completion. Returns the state hash of every tick.</summary>
    private static SortedDictionary<int, ulong> RunBusyScenario(ulong seed, bool cancelWithStep = true)
    {
        var rig = new Rig(seed);
        TgMob walker = null;
        var hashes = new SortedDictionary<int, ulong>();
        rig.At(0, () =>
        {
            DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting()));
            DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)); // capped
            walker = Dm.@new<TgMob>(locate(5, 5, 1));
            DoAfterCall.Start(() => do_after(walker, 2 * SECONDS, rig.Target));
        });
        rig.At(7, () =>
        {
            if (cancelWithStep)
                Waiting.NoSleep(step(walker, WEST));
        });
        for (int i = 1; i <= 45; i++)
        {
            rig.Room.World.Step();
            if (i % 3 == 0 || i is >= 6 and <= 9 || i is 21 or 22 or 41) // the command, the step and its cancel, the ends
                hashes[i] = rig.Room.World.ComputeStateHash();
        }
        Assert.Empty(rig.Room.RuntimeText);
        return hashes;
    }

    [Fact]
    public void ScenarioD_TheSameDoAfterScenarioTwiceGivesTheSameStateHash()
    {
        var a = RunBusyScenario(5);
        var b = RunBusyScenario(5);
        Assert.Equal(a, b);
        Assert.True(a.Values.Distinct().Count() > 12, "the world state changes while the do_afters run");
    }

    [Fact]
    public void ScenarioD_TheStateHashSeesWhetherTheStepCancelledTheDoAfter()
    {
        var stepped = RunBusyScenario(5, cancelWithStep: true);
        var still = RunBusyScenario(5, cancelWithStep: false);
        Assert.NotEqual(stepped[45 / 3 * 3], still[45 / 3 * 3]);
        Assert.Equal(stepped[6], still[6]); // identical until the step, which the script makes at T + 7
    }

    // ---- (e) a same-loc move does not cancel ----

    [Fact]
    public void ScenarioE_AForceMoveToTheSameLocDoesNotCancel()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        float movedSignals = 0;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting())));
        rig.At(5, () =>
        {
            var spy = Dm.@new<datum_test_mob_moved_spy>();
            spy.RegisterSignal(rig.M, COMSIG_MOVABLE_MOVED, nameof(datum_test_mob_moved_spy.on_moved));
            // doMove sets the active movement and resolves it even when the destination is the current loc: Moved(same_loc)
            Assert.True(Dm.truthy(Waiting.NoSleep(rig.M.forceMove(rig.M.loc))));
            movedSignals = spy.heard;
        });
        rig.RunTo(5);
        Assert.Equal(1f, movedSignals); // the Moved signal did fire, with old_loc == loc: on_user_moved returned at its guard (:172)
        rig.Room.In(() => Assert.Equal(1f, rig.Processing.len));
        rig.RunTo(20);
        Assert.True(call.Done);
        Assert.True(Dm.eq(call.Result, TRUE));
        Assert.Equal(rig.T + 20, call.DoneTick);
        rig.Room.In(() => Assert.Equal(20f, rig.Check.polls));
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioE_AMoveToTheSameTurfReturnsNullAndDoesNotCancel()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        object moved = TRUE;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)));
        rig.At(4, () => moved = Waiting.NoSleep(rig.M.Move(rig.M.loc, 0)));
        rig.RunTo(4);
        Assert.Null(moved); // move-chain.md 3a: Move(newloc == loc) returns null
        rig.RunTo(20);
        Assert.True(Dm.eq(call.Result, TRUE));
        Assert.Equal(rig.T + 20, call.DoneTick);
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioE_AStepIntoADenseObjDoesNotMoveTheUserAndDoesNotCancel()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        object stepped = null;
        rig.At(0, () =>
        {
            Dm.@new<obj_test_mob_wall>(locate(4, 3, 1));
            call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target));
        });
        rig.At(5, () => stepped = Waiting.NoSleep(step(rig.M, EAST)));
        rig.RunTo(5);
        Assert.False(Dm.truthy(stepped)); // turf.Enter refused: no Moved is sent
        rig.Room.In(() => Assert.Same(locate(3, 3, 1), rig.M.loc));
        rig.RunTo(20);
        Assert.True(Dm.eq(call.Result, TRUE));
        Assert.Equal(rig.T + 20, call.DoneTick);
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioE_IgnoreUserLocChangeLetsTheUserWalkAway()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, timed_action_flags: IGNORE_USER_LOC_CHANGE)));
        rig.At(5, () => Waiting.NoSleep(step(rig.M, EAST)));
        rig.At(9, () => Waiting.NoSleep(step(rig.M, EAST)));
        rig.RunTo(20);
        Assert.True(call.Done);
        Assert.True(Dm.eq(call.Result, TRUE));
        rig.Room.In(() => Assert.Same(locate(5, 3, 1), rig.M.loc));
        Assert.Empty(rig.Room.RuntimeText);
    }

    // ---- (f) no initialize_actionspeed ----

    [Fact]
    public void ScenarioF_AMobWithoutInitializeActionspeedGetsAnInstantDoAfter()
    {
        var rig = new Rig();
        mob_test_mob_no_actionspeed lazy = null;
        DoAfterCall call = null;
        rig.At(0, () =>
        {
            lazy = Dm.@new<mob_test_mob_no_actionspeed>(locate(2, 2, 1));
            Assert.Null(lazy.cached_multiplicative_actions_slowdown); // never set (mob_defines.dm:63)
            call = DoAfterCall.Start(() => do_after(lazy, 5 * SECONDS, rig.Target, extra_checks: rig.Counting()));
        });
        rig.RunTo(0);
        // delay *= null is 0 (timed_actions.dm:50): the do_after is over before it ever sleeps
        Assert.True(call.Done);
        Assert.Equal(rig.T, call.DoneTick);
        Assert.True(Dm.eq(call.Result, TRUE));
        rig.Room.In(() =>
        {
            Assert.Equal(1f, rig.Check.polls); // await's own last process() (:110-111) is the only poll
            Assert.Equal(0f, rig.Processing.len);
            Assert.Null(lazy.do_afters);
        });
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioF_AnInitializedMobHasSlowdownOneAndItsDoAfterTakesTheFullDelay()
    {
        var rig = new Rig();
        rig.Room.In(() => Assert.True(Dm.eq(rig.M.cached_multiplicative_actions_slowdown, 1))); // initialize_actionspeed (package D)
        DoAfterCall call = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 3 * SECONDS, rig.Target)));
        rig.RunTo(59);
        Assert.False(call.Done);
        rig.RunTo(60);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 60, call.DoneTick);
        Assert.Empty(rig.Room.RuntimeText);
    }

    [Fact]
    public void ScenarioF_TheInstantDoAfterStillCancelsOnAFailedExtraCheck()
    {
        var rig = new Rig();
        mob_test_mob_no_actionspeed lazy = null;
        DoAfterCall call = null;
        rig.At(0, () =>
        {
            lazy = Dm.@new<mob_test_mob_no_actionspeed>(locate(2, 2, 1));
            rig.Check.answer = FALSE;
            call = DoAfterCall.Start(() => do_after(lazy, 5 * SECONDS, rig.Target, extra_checks: rig.Counting()));
        });
        rig.RunTo(0);
        Assert.True(call.Done);
        Assert.True(Dm.eq(call.Result, FALSE)); // process() ran its extra check first (:123) and cancelled
        Assert.Empty(rig.Room.RuntimeText);
    }
}

/// <summary>Counts the COMSIG_MOVABLE_MOVED signals its source sends.</summary>
[DmPath("/datum/test_mob_moved_spy")]
public class datum_test_mob_moved_spy : datum
{
    [NotHashed] public float heard;

    [SignalHandler]
    public virtual void on_moved(datum source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null) => heard++;
}
