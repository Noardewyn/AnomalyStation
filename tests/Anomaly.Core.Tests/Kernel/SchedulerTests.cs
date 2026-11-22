using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

public class SchedulerTests
{
    // ---- never drop an entry, cut runaway loops (waiting-model.md "Delays and limits") ----

    [Fact]
    public void NoEntryIsDroppedWhenTheDrainBackstopIsHit()
    {
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxRunsPerDrain = 3;
        var log = new List<string>();
        using (w.Enter())
        {
            for (int i = 0; i < 4; i++) { int k = i; Waiting.Detach(S(k)); }
            Waiting.Detach(Later());
        }
        w.Step(1);
        Assert.Equal(new[] { "s0@1", "s1@1", "s2@1" }, log);
        Assert.Single(errors);
        Assert.Contains("MaxRunsPerDrain", errors[0]);
        Assert.Equal(2, w.Scheduler.Pending);
        w.Step(1);
        Assert.Equal(new[] { "s0@1", "s1@1", "s2@1", "s3@2", "later@2" }, log); // the left-over entry runs first
        Assert.Single(errors);

        async DmTask S(int k) { await Sleep(0.5f); log.Add($"s{k}@{World.Current.Tick}"); }
        async DmTask Later() { await Sleep(1); log.Add($"later@{World.Current.Tick}"); }
    }

    [Fact]
    public void ARunawaySleepZeroLoopIsFaultedWithoutLosingOtherEntries()
    {
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxZeroDelayHops = 50;
        var log = new List<string>();
        int runawayIterations = 0;
        using (w.Enter())
        {
            Waiting.Detach(Runaway());
            Waiting.Detach(Caller());
        }
        w.Step(1);
        Assert.Equal(new[] { "caller done@1" }, log);
        Assert.Single(errors);
        Assert.Contains("Runaway zero-delay loop", errors[0]);
        Assert.Equal(52, runawayIterations); // 1 at the start, then one per hop until hop 51 is refused
        Assert.Equal(0, w.Scheduler.Pending);

        async DmTask Runaway() { while (true) { runawayIterations++; await Sleep(0); } }
        async DmTask Caller() { await Poller(); log.Add($"caller done@{World.Current.Tick}"); }
        static async DmTask Poller() { for (int i = 0; i < 20; i++) await Sleep(0); }
    }

    [Fact]
    public void ARunawayZeroDelaySpawnChainIsMovedToTheNextTick()
    {
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxZeroDelayHops = 10;
        int spawns = 0;
        var log = new List<string>();
        using (w.Enter())
        {
            Respawn();
            Waiting.Detach(Other());
        }
        w.Step(1);
        Assert.Equal(11, spawns); // hops 0..10 run; the entry of hop 11 is moved to the next tick
        Assert.Single(errors);
        Assert.Contains("spawn chain", errors[0]);
        Assert.Equal(new[] { "other@1" }, log);
        w.Step(1);
        Assert.Equal(2, errors.Count); // still reported, one tick later, never dropped

        void Respawn() => Waiting.Spawn(0, () => { spawns++; Respawn(); return DmTask.CompletedTask; });
        async DmTask Other() { await Sleep(0.5f); log.Add($"other@{World.Current.Tick}"); }
    }

    // ---- delay rounding (waiting-model.md "Delays and limits") ----

    [Theory]
    [InlineData(0f, 0L)]
    [InlineData(-1f, 0L)]
    [InlineData(float.NaN, 0L)]
    [InlineData(1e-5f, 1L)]
    [InlineData(0.3f, 1L)]
    [InlineData(0.5f, 1L)]
    [InlineData(0.55f, 1L)]   // MC with HIGH_POP_MC_TICK_RATE 1.1 (config.txt: "run every 1 byond ticks")
    [InlineData(0.75f, 1L)]
    [InlineData(1f, 2L)]
    [InlineData(1.1f, 2L)]
    [InlineData(1.25f, 2L)]
    [InlineData(1.5f, 3L)]
    [InlineData(4.3f, 8L)]
    [InlineData(10f, 20L)]
    public void SleepDelaysRoundDownToTicksWithAMinimumOfOne(float ds, long ticks)
    {
        Assert.Equal(ticks, Scheduler.TicksFromDeciseconds(ds));
    }

    [Fact]
    public void ExactMultiplesComputedInFloatDoNotLoseATick()
    {
        Assert.Equal(3L, Scheduler.TicksFromDeciseconds(0.1f * 15));
        Assert.Equal(3L, Scheduler.TicksFromDeciseconds(0.3f * 5));
        Assert.Equal(3L, Scheduler.TicksFromDeciseconds(World.TickLag * 3));
        Assert.Equal(7L, Scheduler.TicksFromDeciseconds(0.7f * 5));
        Assert.Equal(1L, Scheduler.TicksFromDeciseconds(World.TickLag * 1.0000001f)); // MC sleep_delta drift
    }

    [Fact]
    public void HugeDelaysNeverWrapAround()
    {
        Assert.Equal(long.MaxValue, Scheduler.TicksFromDeciseconds(1e31f)); // tg INFINITY
        Assert.Equal(long.MaxValue, Scheduler.TicksFromDeciseconds(float.PositiveInfinity));
        Assert.Equal(long.MaxValue, Scheduler.TicksFromDeciseconds(float.MaxValue));

        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.Step(1);
        using (w.Enter())
        {
            Waiting.Detach(P(1e31f));
            Waiting.Detach(P(float.PositiveInfinity));
            Waiting.Detach(Ticks());
            w.Scheduler.After(long.MaxValue, () => log.Add("after woke"));
            w.Scheduler.At(long.MaxValue, () => log.Add("at woke"));
        }
        w.Step(3);
        Assert.Empty(log);
        Assert.Empty(errors);
        Assert.Equal(5, w.Scheduler.Pending);

        async DmTask P(float ds) { await Sleep(ds); log.Add("woke"); }
        async DmTask Ticks() { await SleepTicks(long.MaxValue); log.Add("ticks woke"); }
    }

    [Fact]
    public void StepDecisecondsStepsUntilAtLeastThatMuchTimeHasPassed()
    {
        Assert.Equal(2L, Scheduler.TicksCovering(0.75f));
        Assert.Equal(2L, Scheduler.TicksCovering(1f));
        Assert.Equal(1L, Scheduler.TicksCovering(0.3f));
        Assert.Equal(3L, Scheduler.TicksCovering(1.25f));
        Assert.Equal(3L, Scheduler.TicksCovering(0.1f * 15));
        Assert.Equal(0L, Scheduler.TicksCovering(0f));

        var w = new World(1);
        w.StepDeciseconds(0.75f);
        Assert.Equal(2, w.Tick);
        w.StepDeciseconds(10f);
        Assert.Equal(22, w.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => w.StepDeciseconds(1e10f));
        Assert.Equal(22, w.Tick);
    }

    // ---- tick order (waiting-model.md "Tick order") ----

    [Fact]
    public void ZeroDelayWorkIssuedByAPhaseResumesInTheSameTick()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        w.AddPhase("commands", x =>
        {
            if (x.Tick != 3) return;
            Waiting.Detach(P());
            Waiting.Spawn(0, async () => log.Add($"spawn(0)@{World.Current.Time}"));
            x.Scheduler.At(x.Tick, () => log.Add($"At(now)@{x.Time}"));
        });
        w.Step(5);
        Assert.Equal(new[] { "before@1.5", "after@1.5", "spawn(0)@1.5", "At(now)@1.5" }, log);

        async DmTask P()
        {
            log.Add($"before@{World.Current.Time}");
            await Sleep(0);
            log.Add($"after@{World.Current.Time}");
        }
    }

    [Fact]
    public void ZeroDelayWorkIssuedOutsideAStepRunsAtTheStartOfTheNextStep()
    {
        var w = KernelTestWorld.New(out _);
        long resumedAt = -1;
        w.Step(2);
        using (w.Enter()) Waiting.Detach(T());
        Assert.Equal(-1, resumedAt);
        w.Step(1);
        Assert.Equal(3, resumedAt);

        async DmTask T() { await Sleep(0); resumedAt = World.Current.Tick; }
    }

    [Fact]
    public void AStepDrainsSleepersThenRunsEachPhaseFollowedByAnotherDrain()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Sleeper());
        w.AddPhase("commands", x =>
        {
            log.Add("commands");
            Waiting.Spawn(0, async () => log.Add("spawned by commands"));
        });
        w.AddPhase("replication", x => log.Add("replication"));
        w.Step(1);
        Assert.Equal(new[] { "sleeper woke", "commands", "spawned by commands", "replication" }, log);

        async DmTask Sleeper() { await Sleep(0.5f); log.Add("sleeper woke"); }
    }

    [Fact]
    public void EntriesAddedForTheCurrentTickDuringADrainRunInThatDrainAndPastTicksAreClamped()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        w.Scheduler.At(1, () =>
        {
            log.Add("a@" + w.Tick);
            w.Scheduler.At(w.Tick, () => log.Add("c@" + w.Tick));
            w.Scheduler.At(-5, () => log.Add("d@" + w.Tick));
        });
        w.Scheduler.At(1, () => log.Add("b@" + w.Tick));
        w.Step(1);
        Assert.Equal(new[] { "a@1", "b@1", "c@1", "d@1" }, log);
    }

    // ---- the Master Controller as a scheduler coroutine and the do_after poll order (waiting-model.md "Tick order") ----

    private const int ACTION_WORKING = 1, ACTION_SUCCEEDED = 2;

    // Stand-in for /datum/timed_action (code/datums/timed_actions.dm:103-128) and SStimed_actions.
    private sealed class timed_action
    {
        public int status;
        public float end_time;
        public List<string> trace;
        public List<timed_action> processing;
        public int polls;

        // tg: code/datums/timed_actions.dm:103
        public async DmTask<bool> await_()
        {
            processing.Add(this); // START_PROCESSING(SStimed_actions, src)
            status = ACTION_WORKING;
            while (status == ACTION_WORKING && World.Current.Time < end_time)
            {
                await Sleep(World.TickLag);
                trace.Add($"{World.Current.Tick}:await-wake");
            }
            // "Due to how MC handles sleeping, await will tick first before the subsystem itself, so we need to
            // tick ourselves one last time if we haven't been aborted"
            if (status == ACTION_WORKING) process("await");
            processing.Remove(this);
            return status == ACTION_SUCCEEDED;
        }

        // tg: code/datums/timed_actions.dm:122
        public void process(string by)
        {
            polls++;
            trace.Add($"{World.Current.Tick}:{by}-poll#{polls}");
            if (World.Current.Time >= end_time)
            {
                status = ACTION_SUCCEEDED;
                processing.Remove(this); // return PROCESS_KILL -> STOP_PROCESSING
            }
        }
    }

    private static (List<string> trace, bool? result, long doneTick) RunDoAfter(bool startedInsideTheMc)
    {
        var w = KernelTestWorld.New(out var errors);
        var trace = new List<string>();
        var processing = new List<timed_action>();
        bool? result = null;
        long doneTick = -1;
        const int startTick = 10;

        async DmTask DoAfter()
        {
            var action = new timed_action { end_time = World.Current.Time + 10f, trace = trace, processing = processing };
            result = await action.await_();
            doneTick = World.Current.Tick;
        }

        // The MC loop: a coroutine started at world start; every tick it fires SStimed_actions (a ticker).
        async DmTask Master()
        {
            while (true)
            {
                if (startedInsideTheMc && World.Current.Tick == startTick) Waiting.Detach(DoAfter()); // e.g. from an SS fire
                var current_run = processing.ToList();
                for (int i = current_run.Count - 1; i >= 0; i--) current_run[i].process("MC");
                await Sleep(World.TickLag);
            }
        }

        using (w.Enter()) Waiting.Detach(Master());
        if (!startedInsideTheMc)
            w.AddPhase("commands", x => { if (x.Tick == startTick) Waiting.Detach(DoAfter()); }); // a player verb
        w.Step(35);
        Assert.Empty(errors);
        return (trace, result, doneTick);
    }

    [Fact]
    public void ADoAfterStartedByAPlayerCommandIsPolledByTheMcBeforeItsAwaitWakes()
    {
        var (trace, result, doneTick) = RunDoAfter(startedInsideTheMc: false);
        Assert.Equal(new[] { "11:MC-poll#1", "11:await-wake" }, trace.Where(s => s.StartsWith("11:")));
        Assert.Equal(20, trace.Count(s => s.Contains("poll")));
        Assert.Equal("30:MC-poll#20", trace.Last(s => s.Contains("poll")));
        Assert.True(result);
        Assert.Equal(30, doneTick);
    }

    [Fact]
    public void ADoAfterStartedInsideTheMcWakesBeforeTheMcPollsIt()
    {
        var (trace, result, doneTick) = RunDoAfter(startedInsideTheMc: true);
        Assert.Equal(new[] { "11:await-wake", "11:MC-poll#2" }, trace.Where(s => s.StartsWith("11:")));
        Assert.Equal(21, trace.Count(s => s.Contains("poll")));
        Assert.Equal("30:await-poll#21", trace.Last(s => s.Contains("poll")));
        Assert.True(result);
        Assert.Equal(30, doneTick);
    }
}
