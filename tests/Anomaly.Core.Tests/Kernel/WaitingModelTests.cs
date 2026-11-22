using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

public class WaitingModelTests
{
    // ---- procs under test (stand-ins for translated DM procs) ----

    private static async DmTask Logger(List<string> log, string name, float ds)
    {
        log.Add($"{name}:start@{World.Current.Time}");
        await Sleep(ds);
        log.Add($"{name}:end@{World.Current.Time}");
    }

    private static async DmTask<int> Add(int a, int b)
    {
        await Sleep(1);
        return a + b;
    }

    private static async DmTask<int> Outer(List<string> log)
    {
        int x = await Add(1, 2);
        log.Add($"x={x}");
        int y = await Add(x, 10);
        return y;
    }

    private static DmTask NoSuspension(List<string> log)
    {
        log.Add("ran");
        return DmTask.CompletedTask;
    }

    // ---- tests ----

    [Fact]
    public void SleepRoundsDownToWholeTicksWithAMinimumOfOne()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(Logger(log, "a", 1));      // 1 ds = 2 ticks
            Waiting.Detach(Logger(log, "b", 0.3f));   // floor(0.6) = 0, at least 1 tick
            Waiting.Detach(Logger(log, "c", 0.5f));   // exactly 1 tick
            Waiting.Detach(Logger(log, "d", 0.75f));  // floor(1.5) = 1 tick (BYOND/OpenDream round down)
            Waiting.Detach(Logger(log, "e", 1.25f));  // floor(2.5) = 2 ticks
        }
        w.Step(1);
        Assert.Equal(new[] { "b:end@0.5", "c:end@0.5", "d:end@0.5" }, log.Where(l => l.Contains(":end")));
        w.Step(1);
        Assert.Equal(new[] { "b:end@0.5", "c:end@0.5", "d:end@0.5", "a:end@1", "e:end@1" }, log.Where(l => l.Contains(":end")));
    }

    [Fact]
    public void SameTickWakeupsRunInInsertionOrder()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(Logger(log, "first", 2));
            Waiting.Detach(Logger(log, "second", 2));
            Waiting.Detach(Logger(log, "third", 2));
        }
        w.Step(4);
        var ends = log.Where(l => l.Contains(":end")).ToList();
        Assert.Equal(new[] { "first:end@2", "second:end@2", "third:end@2" }, ends);
    }

    [Fact]
    public void SleepZeroYieldsAndResumesInTheSameTickAfterTheQueue()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        w.Scheduler.At(1, () =>
        {
            Waiting.Detach(Task(log));
            log.Add($"caller continues@{w.Tick}");
        });
        w.Scheduler.At(1, () => log.Add($"already queued@{w.Tick}"));
        w.Step(1);
        Assert.Equal(new[] { "t:before@1", "caller continues@1", "already queued@1", "t:after@1" }, log);

        static async DmTask Task(List<string> l)
        {
            l.Add($"t:before@{World.Current.Tick}");
            await Sleep(0);
            l.Add($"t:after@{World.Current.Tick}");
        }
    }

    [Fact]
    public void AwaitedCalleeBlocksTheCallerAndReturnsItsValue()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        int result = -1;
        using (w.Enter())
        {
            Waiting.Detach(Run());
            async DmTask Run() { result = await Outer(log); log.Add("done"); }
        }
        Assert.Empty(log);
        w.Step(1);
        Assert.Empty(log);              // first Add still sleeping 1 ds = 2 ticks
        w.Step(1);
        Assert.Equal(new[] { "x=3" }, log);
        w.Step(2);
        Assert.Equal(new[] { "x=3", "done" }, log);
        Assert.Equal(13, result);
    }

    [Fact]
    public void DetachedCallLetsTheCallerContinueAtTheCalleesFirstSleep()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(Logger(log, "bg", 5));
            log.Add("caller");
        }
        Assert.Equal(new[] { "bg:start@0", "caller" }, log);
        w.Step(10);
        Assert.Equal("bg:end@5", log[^1]);
    }

    [Fact]
    public void ProcWithoutSuspensionCompletesSynchronously()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            var t = NoSuspension(log);
            Assert.True(t.IsCompleted);
            Assert.False(t.IsFaulted);
        }
        Assert.Equal(new[] { "ran" }, log);
    }

    [Fact]
    public void CallerResumesImmediatelyWhenTheCalleeReturnsBeforeOtherQueuedWork()
    {
        // DM: when B returns, its waiting caller A continues at once; a sleeper due in the same tick and
        // queued after B's wake-up runs only after A has run to its next suspension.
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(A());
            Waiting.Detach(Logger(log, "other", 0.5f));
        }
        w.Step(1);
        Assert.Equal(new[] { "other:start@0", "B done", "A resumed", "other:end@0.5" }, log);

        async DmTask A() { await B(); log.Add("A resumed"); }
        async DmTask B() { await Sleep(0.5f); log.Add("B done"); }
    }

    // Nested awaits, four deep, mixed sync and async completion, give the same values as plain calls
    // (Release builds use struct state machines; run the suite in both configurations).
    private static async DmTask<int> Level4(int v, bool sleep)
    {
        if (sleep) await Sleep(0.5f);
        return v + 1;
    }

    private static async DmTask<int> Level3(int v, bool s3, bool s4)
    {
        int local = v * 10; // hoisted local across awaits
        int a = await Level4(v, s4);
        if (s3) await Sleep(0);
        int b = await Level4(a, !s4);
        return local + a + b;
    }

    private static async DmTask<int> Level2(int v, bool s3, bool s4)
    {
        var x = await Level3(v, s3, s4);
        var y = await Level3(x, !s3, s4);
        return x + y;
    }

    private static int Ref4(int v) => v + 1;
    private static int Ref3(int v) { int a = Ref4(v); int b = Ref4(a); return v * 10 + a + b; }
    private static int Ref2(int v) { var x = Ref3(v); var y = Ref3(x); return x + y; }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NestedFourDeepMixedSyncAsyncGivesTheSameResultAsPlainCalls(bool s3, bool s4)
    {
        var w = KernelTestWorld.New(out var errors);
        var results = new List<int>();
        using (w.Enter()) Waiting.Detach(Level1());
        w.Step(50);
        Assert.Empty(errors);
        Assert.Equal(new[] { Ref2(3) }, results);
        Assert.Equal(0, w.Scheduler.Pending);

        async DmTask Level1() { results.Add(await Level2(3, s3, s4)); }
    }

    [Fact]
    public void RngIsReseededPerTickSoReplayingATickGivesTheSameNumbers()
    {
        var w1 = new World(7);
        var w2 = new World(7);
        var seq1 = new List<int>();
        var seq2 = new List<int>();
        w1.AddPhase("rand", w => seq1.Add(w.Rng.NextInt(1, 1000)));
        w2.AddPhase("rand", w => seq2.Add(w.Rng.NextInt(1, 1000)));
        w1.Step(10);
        w2.Step(10);
        Assert.Equal(seq1, seq2);
        Assert.True(seq1.Distinct().Count() > 5);
    }

    [Fact]
    public void SameScenarioTwiceGivesTheSameStateHash()
    {
        ulong Run()
        {
            var w = new World(99);
            var log = new List<string>();
            w.AddPhase("rand", x => log.Add(x.Rng.NextInt(1, 6).ToString()));
            w.AddStateHasher("log", (World _, ref StateHasher h) => { foreach (var l in log) h.Add(l); });
            using (w.Enter())
            {
                Waiting.Detach(Logger(log, "a", 3));
                Waiting.Detach(Logger(log, "b", 1));
            }
            w.Step(20);
            return w.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void DifferentSeedGivesDifferentHash()
    {
        ulong Run(ulong seed)
        {
            var w = new World(seed);
            var log = new List<string>();
            w.AddPhase("rand", x => log.Add(x.Rng.NextInt(1, 1000000).ToString()));
            w.AddStateHasher("log", (World _, ref StateHasher h) => { foreach (var l in log) h.Add(l); });
            w.Step(5);
            return w.ComputeStateHash();
        }
        Assert.NotEqual(Run(1), Run(2));
    }

    [Fact]
    public void WorldsOnDifferentThreadsDoNotShareTheAmbientContext()
    {
        var w = new World(1);
        using (w.Enter())
        {
            World seenOnOtherThread = null;
            var t = new Thread(() => seenOnOtherThread = World.CurrentOrNull);
            t.Start();
            t.Join();
            Assert.Null(seenOnOtherThread);
            Assert.Same(w, World.Current);
        }
        Assert.Null(World.CurrentOrNull);
    }

    [Fact]
    public void ContinuationResumedByAStepOnAnotherThreadSeesItsWorld()
    {
        var w = KernelTestWorld.New(out var errors);
        World seen = null;
        using (w.Enter()) Waiting.Detach(P());
        var t = new Thread(() => w.Step(2));
        t.Start();
        t.Join();
        Assert.Same(w, seen);
        Assert.Empty(errors);

        async DmTask P() { await Sleep(0.5f); seen = World.CurrentOrNull; }
    }

    [Fact]
    public void NestedWorldStepsRestoreTheAmbientWorld()
    {
        var a = KernelTestWorld.New(out _);
        var b = KernelTestWorld.New(out _);
        World seenInA = null, seenInB = null, after = null;
        using (a.Enter()) Waiting.Detach(PA());
        using (b.Enter()) Waiting.Detach(PB());
        a.AddPhase("stepB", x => { b.Step(); after = World.CurrentOrNull; });
        a.Step(2);
        Assert.Same(a, seenInA);
        Assert.Same(b, seenInB);
        Assert.Same(a, after);
        Assert.Null(World.CurrentOrNull);

        async DmTask PA() { await Sleep(0.5f); seenInA = World.Current; }
        async DmTask PB() { await Sleep(0.5f); seenInB = World.Current; }
    }
}

public class UsrTests
{
    [Fact]
    public void UsrSurvivesSleepAndIsRestoredAfterwards()
    {
        var w = new World(1);
        var log = new List<object>();
        var mob = new object();
        using (w.Enter())
        {
            w.usr = mob;
            Waiting.Detach(Proc());
            w.usr = null;

            async DmTask Proc()
            {
                log.Add(World.Current.usr);
                await Sleep(1);
                log.Add(World.Current.usr);
            }
        }
        w.Step(2);
        Assert.Same(mob, log[0]);
        Assert.Same(mob, log[1]);
        Assert.Null(w.usr);
    }

    [Fact]
    public void InterleavedProcsKeepTheirOwnUsrAcrossNestedAwaits()
    {
        var w = KernelTestWorld.New(out _);
        var log = new List<string>();
        using (w.Enter())
        {
            w.usr = "M1"; Waiting.Detach(A("A"));
            w.usr = "M2"; Waiting.Detach(A("B"));
            w.usr = null;
        }
        w.Step(10);
        Assert.Equal(new[] { "A:M1", "B:M2", "A.c:M1", "B.c:M2", "A.after:M1", "B.after:M2" }, log);
        Assert.Null(w.usr);

        async DmTask A(string n)
        {
            log.Add(n + ":" + World.Current.usr);
            await C(n);
            log.Add(n + ".after:" + World.Current.usr);
        }

        async DmTask C(string n)
        {
            await Sleep(0.5f);
            log.Add(n + ".c:" + World.Current.usr);
            await Sleep(0.5f);
        }
    }

    [Fact]
    public void SpawnedBlocksAndRawSchedulerEntriesRunWithTheUsrOfTheCodeThatScheduledThem()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            w.usr = "spawner";
            Waiting.Spawn(1, async () => { log.Add("spawn(1):" + World.Current.usr); await Sleep(1); log.Add("after sleep:" + World.Current.usr); });
            Waiting.Spawn(0, () => { log.Add("spawn(0):" + World.Current.usr); return DmTask.CompletedTask; });
            w.Scheduler.After(1, () => log.Add("At:" + World.Current.usr));
            w.usr = null;
        }
        w.Step(5);
        Assert.Empty(errors);
        Assert.Equal(new[] { "spawn(0):spawner", "At:spawner", "spawn(1):spawner", "after sleep:spawner" }, log);
        Assert.Null(w.usr);
    }

    [Fact]
    public void EachPhaseStartsWithNoUsr()
    {
        var w = KernelTestWorld.New(out _);
        var seen = new List<object>();
        w.AddPhase("commands", x => { seen.Add(x.usr); x.usr = "player"; });
        w.AddPhase("replication", x => seen.Add(x.usr));
        w.Step(1);
        Assert.Equal(new object[] { null, null }, seen);
        Assert.Null(w.usr);
    }
}
