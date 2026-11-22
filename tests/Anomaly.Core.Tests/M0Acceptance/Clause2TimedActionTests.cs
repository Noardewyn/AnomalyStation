using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M0Acceptance;

/// <summary>
/// ROADMAP M0, "Done when", clause 2: "a scripted timed action completes through a continuation and is cancelled by a
/// movement signal". Each test runs on a booted world (<see cref="BootedToyWorld"/>: the real Master polls SStimed_actions every
/// tick at the game run level) and starts tg's own <c>do_after</c> from a command phase, as a verb would. The caller is a real
/// <c>/mob</c> and the action is a real <c>/datum/timed_action</c>; the caller is a continuation (<see cref="DoAfterCall"/> awaits
/// <c>do_after</c>: it is suspended, not blocked, and the world goes on).
/// <para>
/// Ticks: a tick is 0.5 ds, so a 10 ds action is 20 ticks, and T is the tick of the command. The expected polls and ticks are
/// written from docs/ai/specs/waiting-model.md 7.3 (an action started by a command phase: the Master polls first in every later
/// tick, so the last poll, the one that completes it, is the Master's, in tick T + n) and from timed_actions.dm (the cancel is a
/// movement handler: <c>cancel()</c> inside the <c>Moved</c> signal, the awaiting proc notices one tick later).
/// </para>
/// </summary>
public class Clause2TimedActionTests
{
    private sealed class Rig
    {
        public readonly BootedToyWorld W = new();
        public mob User;
        public obj Target;
        public datum_test_integration_check Check;

        public Rig()
        {
            W.In(() =>
            {
                User = BootedToyWorld.Mob("user", 3, 3);
                Target = BootedToyWorld.Obj("target", 3, 3);
                Check = Dm.@new<datum_test_integration_check>();
                Toy.On(User, COMSIG_DO_AFTER_BEGAN, "began", 0, stamp_tick: true);
                Toy.On(User, COMSIG_DO_AFTER_ENDED, "ended", 0, stamp_tick: true);
                Toy.On(User, COMSIG_MOVABLE_MOVED, "moved", 2, stamp_tick: true);
                Heard.Clear();
            });
        }

        public long T => W.T;

        public List<string> Heard => Toy.Trace(W.World);

        /// <summary>An extra_checks callback that counts the polls (the Master's calls of <c>process()</c>) and says yes.</summary>
        public datum_callback Counting() => W.In(() => Dm.@new<datum_callback>(Check, nameof(datum_test_integration_check.check)));

        public float Polls => W.In(() => Check.polls);

        public float Processing => W.In(() => SStimed_actions.processing.len);

        /// <summary>Starts <c>do_after(User, delay_ds, Target, extra_checks = Counting())</c> in the command phase of tick T + offset.</summary>
        public DoAfterHandle Start(long offset, float delay_ds, mob user = null, atom target = null, datum_callback checks = null)
        {
            var handle = new DoAfterHandle();
            W.At(offset, () => handle.Call = DoAfterCall.Start(() => do_after(user ?? User, delay_ds, target ?? Target, extra_checks: checks ?? Counting())));
            return handle;
        }
    }

    /// <summary>The call a scripted command made, filled in when the command runs.</summary>
    private sealed class DoAfterHandle
    {
        public DoAfterCall Call;
        public bool Done => Call?.Done == true;
        public long DoneTick => Call.DoneTick;
        public object Result => Call.Result;
    }

    // ---- completion through a continuation ----

    [Theory]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(25f)]
    public void ADoAfterStartedByACommandPhaseReturnsTrueAfterItsDelayPolledOncePerTickByTheMaster(float delay_ds)
    {
        var rig = new Rig();
        int ticks = (int)(delay_ds * 2);   // 0.5 ds a tick
        DoAfterHandle call = rig.Start(0, delay_ds);

        rig.W.RunTo(0);

        // the command phase runs after the Master fired in tick T: the action is registered, not polled yet, and the caller waits
        Assert.False(call.Done);
        Assert.Equal(1f, rig.Processing);
        Assert.Equal(0f, rig.Polls);
        rig.W.In(() => Toy.AssertNumber(1, rig.User.do_afters[rig.Target]));
        for (int k = 1; k < ticks; k++)
        {
            rig.W.RunTo(k);
            Assert.False(call.Done);                 // still suspended: no result before the delay has passed
            Assert.Equal(k, rig.Polls);              // waiting-model.md 7.3: the Master polls first in each later tick, once a tick
        }

        rig.W.RunTo(ticks);

        Assert.True(call.Done);
        Assert.Equal(rig.T + ticks, call.DoneTick);  // the delay, to the tick
        Toy.AssertNumber(TRUE, call.Result);         // TRUE
        Assert.Equal(ticks, rig.Polls);              // the last poll is the Master's, in the tick the action completes
        Assert.Equal(0f, rig.Processing);            // process() returned PROCESS_KILL: nothing polls it afterwards
        rig.W.In(() =>
        {
            Assert.Null(rig.User.do_afters);         // the count was released (UNSETEMPTY)
            Assert.Same(locate(3, 3, 1), rig.User.loc);
        });
        Toy.AssertTrace(new[] { $"{rig.T}:began user", $"{rig.T + ticks}:ended user" }, rig.Heard);   // BEGAN, then ENDED
        rig.W.RunTo(ticks + 5);
        Assert.Equal(ticks, rig.Polls);
        Assert.True(rig.W.MasterIsRunning());
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void TheCommandPhaseIsNotBlockedByTheActionAndOtherAtomsMovingMeanwhileDoNotCancelIt()
    {
        var rig = new Rig();
        mob bystander = null;
        var bystander_steps = new List<float>();
        rig.W.In(() => bystander = BootedToyWorld.Mob("bystander", 2, 5));
        // The caller and the bystander are driven by one command, so that what happens inside the tick is visible:
        var started = new DoAfterHandle();
        bool done_at_once = true;   // read after the run: an assertion inside a command would only be reported as a runtime
        rig.W.At(0, () =>
        {
            started.Call = DoAfterCall.Start(() => do_after(rig.User, 1 * SECONDS, rig.Target, extra_checks: rig.Counting()));
            // the caller is suspended inside do_after, so this runs at once, in the same command and the same tick
            done_at_once = started.Call.Done;
            bystander_steps.Add(Dm.num(Toy.Step(bystander, EAST)));
        });
        for (int k = 1; k <= 8; k++)
        {
            float dir = k % 2 == 1 ? EAST : WEST;   // the bystander walks to and fro along its row for the whole action
            rig.W.At(k, () => bystander_steps.Add(Dm.num(Toy.Step(bystander, dir))));
        }

        rig.W.RunTo(20);

        Assert.False(done_at_once);
        Assert.Equal(Enumerable.Repeat(TRUE, 9), bystander_steps);   // every step of the bystander worked, in the ticks of the action
        Assert.True(started.Done);
        Assert.Equal(rig.T + 20, started.DoneTick);
        Toy.AssertNumber(TRUE, started.Result);
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void TwoDoAftersOnTwoTargetsAreBothPolledByTheOneSubsystemAndEachCompletesAtItsOwnTick()
    {
        var rig = new Rig();
        obj second_target = null;
        mob second_user = null;
        rig.W.In(() =>
        {
            second_target = BootedToyWorld.Obj("second target", 5, 5);
            second_user = BootedToyWorld.Mob("second user", 5, 5);
            Toy.On(second_user, COMSIG_DO_AFTER_BEGAN, "began2", 0, stamp_tick: true);
            Toy.On(second_user, COMSIG_DO_AFTER_ENDED, "ended2", 0, stamp_tick: true);
        });
        DoAfterHandle first = rig.Start(0, 5f);                                                        // 10 ticks
        DoAfterHandle second = rig.Start(0, 10f, second_user, second_target, rig.W.In(() => Dm.@new<datum_callback>(rig.Check, nameof(datum_test_integration_check.check))));   // 20 ticks

        rig.W.RunTo(0);
        Assert.Equal(2f, rig.Processing);

        rig.W.RunTo(10);
        Assert.True(first.Done);
        Assert.False(second.Done);
        Assert.Equal(rig.T + 10, first.DoneTick);
        Assert.Equal(1f, rig.Processing);

        rig.W.RunTo(20);
        Assert.True(second.Done);
        Assert.Equal(rig.T + 20, second.DoneTick);
        Assert.Equal(0f, rig.Processing);
        Toy.AssertNumber(TRUE, first.Result);
        Toy.AssertNumber(TRUE, second.Result);
        Toy.AssertTrace(new[]
        {
            $"{rig.T}:began user",
            $"{rig.T}:began2 second user",
            $"{rig.T + 10}:ended user",
            $"{rig.T + 20}:ended2 second user",
        }, rig.Heard);
        Assert.Empty(rig.W.Runtimes);
    }

    // ---- cancelled by a movement signal ----

    [Fact]
    public void AStepDuringTheActionCancelsItWithFalseOneTickLaterAndSendsDoAfterEnded()
    {
        var rig = new Rig();
        object stepped = null;
        DoAfterHandle call = rig.Start(0, 10f);
        rig.W.At(5, () => stepped = Toy.Step(rig.User, EAST));

        rig.W.RunTo(5);

        Assert.False(call.Done);
        Toy.AssertNumber(TRUE, stepped);
        rig.W.In(() =>
        {
            Assert.Same(locate(4, 3, 1), rig.User.loc);
            // the Moved signal reached on_user_moved inside the chain, strictly before step returned: cancel() = FAILED and
            // STOP_PROCESSING (timed_actions.dm:95-101), but the awaiting proc has not noticed yet (it wakes next tick)
            Assert.Equal(0f, SStimed_actions.processing.len);
            Toy.AssertNumber(1, rig.User.do_afters[rig.Target]);
        });
        Assert.Equal(5f, rig.Polls);

        rig.W.RunTo(6);

        Assert.True(call.Done);
        Assert.Equal(rig.T + 6, call.DoneTick);
        Toy.AssertNumber(FALSE, call.Result);            // FALSE: not null, not TRUE
        rig.W.In(() => Assert.Null(rig.User.do_afters));    // cleared (timed_actions.dm:245-251)
        Assert.Equal(5f, rig.Polls);                     // no poll after the cancel
        Toy.AssertTrace(new[]
        {
            $"{rig.T}:began user",
            $"{rig.T + 5}:moved user (3,3) 4",           // the movement signal, in the tick of the step,
            $"{rig.T + 6}:ended user",                   // and ENDED when the caller learns of it: it is sent on failure too
        }, rig.Heard);
        rig.W.RunTo(30);
        Assert.Equal(5f, rig.Polls);
        Assert.True(rig.W.MasterIsRunning());
        Assert.Empty(rig.W.Runtimes);
    }

    [Theory]
    [InlineData(1, 2)]    // the step in the first tick after the start: FALSE at T + 2
    [InlineData(10, 11)]
    [InlineData(19, 20)]  // the last tick before the end: still cancelled, noticed at T + 20, where the Master no longer polls it
    public void TheStepCancelsAtAnyTickBeforeTheEndAndTheCallerLearnsOfItOneTickLater(int step_offset, int result_offset)
    {
        var rig = new Rig();
        DoAfterHandle call = rig.Start(0, 10f);
        rig.W.At(step_offset, () => Toy.Step(rig.User, NORTH));

        rig.W.RunTo(30);

        Assert.True(call.Done);
        Assert.Equal(rig.T + result_offset, call.DoneTick);
        Toy.AssertNumber(FALSE, call.Result);
        Assert.Equal(step_offset, rig.Polls);   // the Master polled in every tick up to and including the step's tick, no more
        Assert.Equal(0f, rig.Processing);
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void AStepInTheTickTheActionCompletesComesTooLateAndTheResultStaysTrue()
    {
        // the command phase runs after the Master and the sleepers: in tick T + 20 the Master's last poll and the await have
        // already made the result (TRUE), so a step in that tick's command phase cancels nothing
        var rig = new Rig();
        DoAfterHandle call = rig.Start(0, 10f);
        rig.W.At(20, () => Toy.Step(rig.User, EAST));

        rig.W.RunTo(25);

        Assert.True(call.Done);
        Assert.Equal(rig.T + 20, call.DoneTick);
        Toy.AssertNumber(TRUE, call.Result);
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void ATeleportOfTheUserByForceMoveIsAMovementSignalTooAndCancelsIt()
    {
        // doMove sends Moved with forced = TRUE: on_user_moved looks only at user.loc == old_loc (timed_actions.dm:135-137)
        var rig = new Rig();
        DoAfterHandle call = rig.Start(0, 10f);
        rig.W.At(5, () => Toy.Force(rig.User, locate(6, 6, 1)));

        rig.W.RunTo(6);

        Assert.True(call.Done);
        Assert.Equal(rig.T + 6, call.DoneTick);
        Toy.AssertNumber(FALSE, call.Result);
        Toy.AssertTrace(new[]
        {
            $"{rig.T}:began user",
            $"{rig.T + 5}:moved user (3,3) 0",   // movement_dir NONE: a forceMove
            $"{rig.T + 6}:ended user",
        }, rig.Heard);
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void ATargetThatMovesCancelsItToo()
    {
        // register_signals: COMSIG_MOVABLE_MOVED on each target (unless IGNORE_TARGET_LOC_CHANGE) -> on_target_moved (:179-191)
        var rig = new Rig();
        DoAfterHandle call = rig.Start(0, 10f);
        rig.W.At(7, () => Toy.Step(rig.Target, SOUTH));

        rig.W.RunTo(10);

        Assert.True(call.Done);
        Assert.Equal(rig.T + 8, call.DoneTick);
        Toy.AssertNumber(FALSE, call.Result);
        Assert.Equal(7f, rig.Polls);
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void WhatDoesNotMoveTheUserCancelsNothingABumpASameLocForceMoveAStepWithNoDirectionAndAnotherMobStepping()
    {
        // on_user_moved returns when user.loc == old_loc; a blocked step sends no Moved at all (move-chain.md 3g); a step with
        // no direction never calls Move (E2); the signals of other atoms are not the user's
        var rig = new Rig();
        mob walker = null;
        mob other = null;
        object blocked = null, same_loc = null, no_dir = null;   // asserted after the run: a phase's exception would not fail the test
        rig.W.In(() =>
        {
            walker = BootedToyWorld.Mob("walker", 2, 3);   // next to the wall at (1, 3)
            other = BootedToyWorld.Mob("other", 5, 5);
        });
        DoAfterHandle call = rig.Start(0, 10f, walker);
        rig.W.At(3, () => blocked = Toy.Step(walker, WEST));              // into the wall: Bump, no Moved
        rig.W.At(6, () => same_loc = Toy.Force(walker, walker.loc));      // the same loc: Moved fires, old_loc == loc
        rig.W.At(8, () => no_dir = Toy.Step(walker, 0));                  // dir 0: Move is not called
        rig.W.At(10, () => Toy.Step(other, EAST));                        // another mob's Moved

        rig.W.RunTo(25);

        Toy.AssertNumber(FALSE, blocked);
        Toy.AssertNumber(TRUE, same_loc);
        Toy.AssertNumber(0, no_dir);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 20, call.DoneTick);
        Toy.AssertNumber(TRUE, call.Result);
        Assert.Equal(20f, rig.Polls);   // polled in every tick: none of the four things cancelled it
        rig.W.In(() => Assert.Same(locate(2, 3, 1), walker.loc));
        Assert.Empty(rig.W.Runtimes);
    }

    [Fact]
    public void ACancelledDoAfterAndACompletingOneRunSideBySideAndEachGetsItsOwnResultAndEndedSignal()
    {
        var rig = new Rig();
        mob second_user = null;
        obj second_target = null;
        rig.W.In(() =>
        {
            second_user = BootedToyWorld.Mob("second user", 5, 5);
            second_target = BootedToyWorld.Obj("second target", 5, 5);
            Toy.On(second_user, COMSIG_DO_AFTER_BEGAN, "began2", 0, stamp_tick: true);
            Toy.On(second_user, COMSIG_DO_AFTER_ENDED, "ended2", 0, stamp_tick: true);
            rig.Heard.Clear();
        });
        DoAfterHandle cancelled = rig.Start(0, 10f);
        DoAfterHandle completes = rig.Start(0, 10f, second_user, second_target);
        rig.W.At(5, () => Toy.Step(rig.User, EAST));

        rig.W.RunTo(20);

        Assert.True(cancelled.Done);
        Assert.True(completes.Done);
        Toy.AssertNumber(FALSE, cancelled.Result);
        Toy.AssertNumber(TRUE, completes.Result);
        Assert.Equal(rig.T + 6, cancelled.DoneTick);
        Assert.Equal(rig.T + 20, completes.DoneTick);
        Toy.AssertTrace(new[]
        {
            $"{rig.T}:began user",
            $"{rig.T}:began2 second user",
            $"{rig.T + 5}:moved user (3,3) 4",
            $"{rig.T + 6}:ended user",
            $"{rig.T + 20}:ended2 second user",
        }, rig.Heard);
        Assert.Equal(0f, rig.Processing);
        Assert.True(rig.W.MasterIsRunning());
        Assert.Empty(rig.W.Runtimes);
    }
}
