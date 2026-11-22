using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// The three do_after scenarios of docs/ai/specs/m0-reading/timed-actions-helpers.md section 8 (and move-chain.md T10) with a real
/// <c>/mob</c> in the booted toy room: the real Master polls SStimed_actions every tick at RUNLEVEL_GAME, the command is a world
/// phase (BYOND's step 6, after the sleepers and the Master woke), <c>step</c> runs the real Move chain over real turfs
/// and the real <c>/area/Entered</c>. Package C's tests cover the contract of do_after and the timed action proc by proc
/// (DoAfterContractTests) and these scenarios on its own room (DoAfterScenarioTests); these are the same acceptance
/// traces on the toy room, kept short. T is the tick of the command; one second is 10 ds, which is 20 ticks of 0.5 ds.
/// </summary>
public class DoAfterToyRoomTests
{
    private sealed class Rig
    {
        public readonly TgToyRoom Room;
        public mob M;
        public obj Target;
        public datum_test_integration_check Check;
        public readonly long T;
        private readonly Dictionary<long, List<Action>> script = new();

        public Rig(ulong seed = 5)
        {
            Room = TgToyRoom.Boot(seed: seed);
            Room.In(() =>
            {
                M = Dm.@new<mob>(locate(3, 3, 1));
                M.name = "mob";
                Target = Dm.@new<obj>(locate(3, 3, 1));
                Target.name = "target";
                Check = Dm.@new<datum_test_integration_check>();
                On(M, COMSIG_DO_AFTER_BEGAN, "began", 0, stamp_tick: true);
                On(M, COMSIG_DO_AFTER_ENDED, "ended", 0, stamp_tick: true);
                Trace(Room.World).Clear();
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

        public datum_callback Counting() => Dm.@new<datum_callback>(Check, nameof(datum_test_integration_check.check));

        /// <summary>Steps the world until tick T + <paramref name="offset"/> has run.</summary>
        public void RunTo(long offset)
        {
            while (Room.World.Tick < T + offset)
                Room.World.Step();
        }

        public List<string> Heard => Trace(Room.World);
    }

    // ---- completion ----

    [Fact]
    public void ADoAfterOfOneSecondCompletesTrueAtTPlus20WithTwentyPollsAndSendsBeganThenEnded()
    {
        var rig = new Rig();
        DoAfterCall call = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target, extra_checks: rig.Counting())));

        rig.RunTo(0);
        Assert.False(call.Done);
        rig.Room.In(() =>
        {
            // the command phase runs after the Master fired this tick: registered, not polled yet
            Assert.Equal(1f, SStimed_actions.processing.len);
            AssertNumber(1, rig.M.do_afters[rig.Target]);
            Assert.Equal(0f, rig.Check.polls);
        });

        rig.RunTo(19);
        Assert.False(call.Done);
        rig.Room.In(() => Assert.Equal(19f, rig.Check.polls));   // the Master polls first in every later tick (waiting-model.md 7.3)

        rig.RunTo(20);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 20, call.DoneTick);
        AssertNumber(TRUE, call.Result);
        rig.Room.In(() =>
        {
            Assert.Equal(20f, rig.Check.polls);
            Assert.Equal(0f, SStimed_actions.processing.len);   // process() returned PROCESS_KILL
            Assert.Null(rig.M.do_afters);                       // UNSETEMPTY after the decrement
            Assert.Same(locate(3, 3, 1), rig.M.loc);
        });
        AssertTrace(new[] { $"{rig.T}:began mob", $"{rig.T + 20}:ended mob" }, rig.Heard);
        rig.RunTo(25);
        rig.Room.In(() => Assert.Equal(20f, rig.Check.polls));   // nothing polls afterwards
        Assert.Empty(rig.Room.Runtimes);
    }

    // ---- cancel by step ----

    [Fact]
    public void AStepAfterTPlus5CancelsTheDoAfterWithFalseOneTickLaterAndClearsDoAftersAndSendsEnded()
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
            AssertNumber(TRUE, stepped);   // step returns what Move returned
            Assert.Same(locate(4, 3, 1), rig.M.loc);
            // Moved reached on_user_moved synchronously inside the chain: cancel() = FAILED + STOP_PROCESSING (timed_actions.dm:95-101)
            Assert.Equal(0f, SStimed_actions.processing.len);
            Assert.Equal(5f, rig.Check.polls);
            AssertNumber(1, rig.M.do_afters[rig.Target]);   // the awaiting proc has not noticed yet
        });

        rig.RunTo(6);   // the awaiting proc wakes: one tick of cancel latency
        Assert.True(call.Done);
        Assert.Equal(rig.T + 6, call.DoneTick);
        AssertNumber(FALSE, call.Result);   // FALSE, not null
        rig.Room.In(() =>
        {
            Assert.Null(rig.M.do_afters);                    // cleared (timed_actions.dm:245-251)
            Assert.Equal(5f, rig.Check.polls);               // no poll after the cancel
        });
        AssertTrace(new[] { $"{rig.T}:began mob", $"{rig.T + 6}:ended mob" }, rig.Heard);   // ENDED is sent on failure too
        rig.RunTo(25);
        rig.Room.In(() => Assert.Equal(5f, rig.Check.polls));
        Assert.Empty(rig.Room.Runtimes);
    }

    [Fact]
    public void ATeleportOfTheUserByForceMoveCancelsTheDoAfterToo()
    {
        // doMove sends Moved with forced = TRUE: on_user_moved does not look at it, only at user.loc == old_loc
        var rig = new Rig();
        DoAfterCall call = null;
        object moved = null;
        rig.At(0, () => call = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)));
        rig.At(5, () => moved = Waiting.NoSleep(rig.M.forceMove(locate(6, 6, 1))));

        rig.RunTo(6);

        AssertNumber(TRUE, moved);
        Assert.True(call.Done);
        Assert.Equal(rig.T + 6, call.DoneTick);
        AssertNumber(FALSE, call.Result);
        AssertTrace(new[] { $"{rig.T}:began mob", $"{rig.T + 6}:ended mob" }, rig.Heard);
        Assert.Empty(rig.Room.Runtimes);
    }

    [Fact]
    public void AMoveThatChangesNothingDoesNotCancelASameLocForceMoveAndABumpIntoAWallBothLeaveTheDoAfterRunning()
    {
        // on_user_moved: `if(user.loc == old_loc) return` (timed_actions.dm:135-137); a blocked step sends no Moved at all
        var rig = new Rig();
        mob walker = null;
        DoAfterCall call = null;
        object blocked = null, same_loc = null, no_dir = null;   // asserted after the run: a phase's exception would not fail the test
        rig.At(0, () =>
        {
            walker = Dm.@new<mob>(locate(2, 3, 1));
            walker.name = "walker";
            call = DoAfterCall.Start(() => do_after(walker, 1 * SECONDS, rig.Target));
        });
        rig.At(5, () => blocked = Waiting.NoSleep(step(walker, WEST)));                    // into the wall at (1, 3): Bump, no Moved
        rig.At(8, () => same_loc = Waiting.NoSleep(walker.forceMove(walker.loc)));         // the same loc: Moved, but old_loc == loc
        rig.At(9, () => no_dir = Waiting.NoSleep(step(walker, 0)));                        // dir 0: Move is never called

        rig.RunTo(20);

        AssertNumber(FALSE, blocked);
        AssertNumber(TRUE, same_loc);
        AssertNumber(0, no_dir);

        Assert.True(call.Done);
        Assert.Equal(rig.T + 20, call.DoneTick);
        AssertNumber(TRUE, call.Result);
        rig.Room.In(() => Assert.Same(locate(2, 3, 1), walker.loc));
        Assert.Empty(rig.Room.Runtimes);
    }

    // ---- the interaction cap ----

    [Fact]
    public void TheCapReturnsNullNotFalseAndSendsNoBeganForASecondDoAfterOnTheSameTarget()
    {
        var rig = new Rig();
        DoAfterCall first = null, second = null;
        rig.At(0, () => first = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)));
        rig.At(3, () => second = DoAfterCall.Start(() => do_after(rig.M, 1 * SECONDS, rig.Target)));   // max_interact_count = 1

        rig.RunTo(3);

        Assert.True(second.Done);                      // a bare `return` before anything sleeps (timed_actions.dm:236)
        Assert.Equal(rig.T + 3, second.DoneTick);
        Assert.Null(second.Result);                    // null, not FALSE
        Assert.False(first.Done);
        AssertTrace(new[] { $"{rig.T}:began mob" }, rig.Heard);   // the capped call sent no BEGAN (and sends no ENDED)
        rig.Room.In(() =>
        {
            AssertNumber(1, rig.M.do_afters[rig.Target]);   // and did not raise the count
            Assert.Equal(1f, SStimed_actions.processing.len);
        });

        rig.RunTo(20);

        AssertNumber(TRUE, first.Result);
        AssertTrace(new[] { $"{rig.T}:began mob", $"{rig.T + 20}:ended mob" }, rig.Heard);
        rig.Room.In(() => Assert.Null(rig.M.do_afters));
        Assert.Empty(rig.Room.Runtimes);
    }
}
