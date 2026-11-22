using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>Tick order, re-drains after phases, runaway limits and delay rounding (waiting-model.md 7-8).</summary>
public class SchedulerAttackTests
{
    // ---- ordering ----

    [Fact]
    public void ZeroDelayChainsOfEachPhaseFinishBeforeTheNextPhaseAndLaterWakeupsKeepFifo()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Sleeper("drain", 0.5f)); // wakes at tick 1 in the first drain
        w.AddPhase("p1", x => { if (x.Tick == 1) { Waiting.Detach(Hops("p1")); Waiting.Spawn(0, async () => log.Add("p1 spawn(0)")); } });
        w.AddPhase("p2", x => { if (x.Tick == 1) Waiting.Detach(Hops("p2")); });
        w.Step(2);
        Assert.Equal(new[]
        {
            "drain woke@1",
            "p1 hop0@1", "p1 hop1@1", "p1 spawn(0)", "p1 hop2@1", "p1 hop3@1",
            "p2 hop0@1", "p2 hop1@1", "p2 hop2@1", "p2 hop3@1",
            // the next tick: the first drain's sleeper went to sleep first, then p1's, then p2's
            "drain again@2", "p1 again@2", "p2 again@2",
        }, log);
        Assert.Empty(errors);

        async DmTask Sleeper(string n, float ds)
        {
            await Sleep(ds);
            log.Add($"{n} woke@{World.Current.Tick}");
            await Sleep(0.5f);
            log.Add($"{n} again@{World.Current.Tick}");
        }

        async DmTask Hops(string n)
        {
            for (int i = 0; i < 4; i++)
            {
                log.Add($"{n} hop{i}@{World.Current.Tick}");
                if (i < 3) await Sleep(0);
            }
            await Sleep(0.5f);
            log.Add($"{n} again@{World.Current.Tick}");
        }
    }

    [Fact]
    public void AnEntryForAPastTickAddedByAPhaseRunsInThatPhasesReDrain()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        w.AddPhase("p1", x => { if (x.Tick == 3) x.Scheduler.At(0, () => log.Add($"past@{x.Tick}")); });
        w.AddPhase("p2", x => { if (x.Tick == 3) log.Add("p2"); });
        w.Step(4);
        Assert.Equal(new[] { "past@3", "p2" }, log);
    }

    [Fact]
    public void WorkAPhaseScheduledBeforeItFailedStillRunsInThatTick()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.AddPhase("commands", x =>
        {
            if (x.Tick != 2) return;
            x.usr = "player";
            Waiting.Detach(P());
            throw new InvalidOperationException("verb runtime");
        });
        w.AddPhase("replication", x => { if (x.Tick == 2) log.Add($"replication usr={x.usr ?? "null"}"); });
        w.Step(3);
        Assert.Equal(new[] { "resumed@2 usr=player", "replication usr=null" }, log);
        Assert.Equal(new[] { "InvalidOperationException: verb runtime" }, errors);

        async DmTask P()
        {
            await Sleep(0);
            log.Add($"resumed@{World.Current.Tick} usr={World.Current.usr}");
        }
    }

    [Fact]
    public void ADrainStoppedByTheBackstopResumesInTheDrainAfterTheNextPhase()
    {
        // Pinned: leftovers keep their keys and run first in the next drain, which is the re-drain after the
        // next phase when there is one (waiting-model.md 8); nothing is dropped or reordered among themselves.
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxRunsPerDrain = 2;
        var log = new List<string>();
        using (w.Enter()) for (int i = 0; i < 3; i++) { int k = i; Waiting.Detach(S(k)); }
        w.AddPhase("commands", x => log.Add($"commands@{x.Tick}"));
        w.Step(1);
        Assert.Equal(new[] { "s0@1", "s1@1", "commands@1", "s2@1" }, log);
        Assert.Single(errors);

        async DmTask S(int k) { await Sleep(0.5f); log.Add($"s{k}@{World.Current.Tick}"); }
    }

    // ---- runaway zero-delay loops ----

    [Fact]
    public void ManyShortZeroDelayChainsInOneTickAreNotARunaway()
    {
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxZeroDelayHops = 5;
        int done = 0;
        using (w.Enter()) for (int i = 0; i < 1000; i++) Waiting.Detach(P());
        w.Step(1);
        Assert.Equal(1000, done);
        Assert.Empty(errors);

        async DmTask P() { for (int i = 0; i < 5; i++) await Sleep(0); done++; }
    }

    [Fact]
    public void ARunawayThroughAnAwaitedCalleeFaultsTheCalleeAndUnwindsTheCallerOnce()
    {
        // The hop count follows the chain through inline completions: A awaits B, B sleeps(0) and returns, A calls
        // B again. B gets the runtime at its sleep, A unwinds to its boundary, one report.
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxZeroDelayHops = 20;
        int calls = 0;
        using (w.Enter()) Waiting.Detach(A());
        w.Step(1);
        Assert.Equal(22, calls); // call k sleeps with hop k-1; hop 21 (call 22's sleep) is the first one refused
        Assert.Single(errors);
        Assert.Contains("Runaway zero-delay loop", errors[0]);
        Assert.Equal(0, w.Scheduler.Pending);

        async DmTask A() { while (true) await B(); }
        async DmTask B() { calls++; await Sleep(0); }
    }

    [Fact]
    public void ARunawayThatCatchesItsOwnErrorNeverStarvesOtherProcs()
    {
        var w = KernelTestWorld.New(out var errors);
        w.Scheduler.MaxZeroDelayHops = 10;
        w.Scheduler.MaxRunsPerDrain = 100;
        var ticks = new List<long>();
        using (w.Enter())
        {
            Waiting.Detach(Stubborn());
            Waiting.Detach(Periodic());
        }
        w.Step(5);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, ticks); // never lost, never delayed
        Assert.Equal(5, errors.Count(e => e.Contains("MaxRunsPerDrain")));

        static async DmTask Stubborn()
        {
            while (true)
            {
                try { await Sleep(0); }
                catch (WaitingException) { }
            }
        }

        async DmTask Periodic()
        {
            while (true) { await Sleep(0.5f); ticks.Add(World.Current.Tick); }
        }
    }

    // ---- delay rounding, end to end through sleep and spawn ----

    // ds, ticks after the current one (0: later in this tick, after the queue; -1: never)
    public static IEnumerable<object[]> Delays => new[]
    {
        new object[] { 0.3f, 1L }, new object[] { 0.5f, 1L }, new object[] { 0.55f, 1L }, new object[] { 0.75f, 1L },
        new object[] { 1f, 2L }, new object[] { 1.1f, 2L }, new object[] { 1.25f, 2L }, new object[] { 1.5f, 3L },
        new object[] { 4.3f, 8L }, new object[] { 0f, 0L }, new object[] { float.NaN, 0L },
        new object[] { 1e31f, -1L }, new object[] { float.PositiveInfinity, -1L }, new object[] { float.MaxValue, -1L },
    };

    [Theory]
    [MemberData(nameof(Delays))]
    [InlineData(-1f, 0L)]
    [InlineData(-0.5f, 0L)]
    [InlineData(float.NegativeInfinity, 0L)]
    public void SleepWakesAtTheRoundedTick(float ds, long ticks)
    {
        var (woke, order) = Run(ds, spawn: false);
        if (ticks < 0) { Assert.Equal(-1, woke); return; }
        Assert.Equal(5 + ticks, woke);
        if (ticks == 0) Assert.Equal(new[] { "queued", "woke" }, order); // after what was already queued
    }

    [Theory]
    [MemberData(nameof(Delays))]
    public void SpawnRunsAtTheSameRoundedTickAsSleep(float ds, long ticks)
    {
        var (woke, order) = Run(ds, spawn: true);
        if (ticks < 0) { Assert.Equal(-1, woke); return; }
        Assert.Equal(5 + ticks, woke);
        if (ticks == 0) Assert.Equal(new[] { "queued", "woke" }, order);
    }

    private static (long woke, List<string> order) Run(float ds, bool spawn)
    {
        var w = KernelTestWorld.New(out var errors);
        long woke = -1;
        var order = new List<string>();
        w.Scheduler.At(5, () =>
        {
            if (spawn) Waiting.Spawn(ds, () => { Mark(); return DmTask.CompletedTask; });
            else Waiting.Detach(P());
        });
        w.Scheduler.At(5, () => order.Add("queued")); // already queued when the delay starts
        w.Step(30);
        Assert.Empty(errors);
        return (woke, order);

        void Mark() { woke = World.Current.Tick; order.Add("woke"); }
        async DmTask P() { await Sleep(ds); Mark(); }
    }
}
