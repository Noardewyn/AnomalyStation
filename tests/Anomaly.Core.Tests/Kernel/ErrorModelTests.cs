using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// Runtime errors (docs/ai/specs/waiting-model.md "Runtime errors"): an exception inside an
/// async proc is captured in its task, `await` rethrows it, and it unwinds to the nearest isolation boundary,
/// which reports it once. This deliberately deviates from DM, where only the failing proc ends.
/// </summary>
public class ErrorModelTests
{
    private static async DmTask ThrowsSync() { throw new InvalidOperationException("sync boom"); }

    private static async DmTask<int> ThrowsSyncT() { throw new InvalidOperationException("sync boom"); }

    private static async DmTask ThrowsAfterSleep(float ds)
    {
        await Sleep(ds);
        throw new InvalidOperationException("async boom");
    }

    private static async DmTask<int> ThrowAfterT(float ds) { await Sleep(ds); throw new InvalidOperationException("after"); }

    private static async DmTask<int> Mid(Func<DmTask<int>> f) { return 1 + await f(); }

    // ---- capture ----

    [Fact]
    public void ErrorBeforeFirstSuspensionIsCapturedInTheTaskNotThrownOutOfTheCall()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter())
        {
            var t = ThrowsSync();     // does not throw
            Assert.True(t.IsCompleted);
            Assert.True(t.IsFaulted);
            Assert.Equal("sync boom", t.Exception.Message);

            var tt = ThrowsSyncT();
            Assert.True(tt.IsFaulted);
            Assert.False(tt.TryGetResult(out _));
            Assert.Throws<InvalidOperationException>(() => tt.GetAwaiter().GetResult());
        }
        Assert.Empty(errors); // captured, not reported: the caller decides (await, Detach, NoSleep)
    }

    [Fact]
    public void AwaitedErrorBeforeFirstSuspensionIsRethrownInTheCaller()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Caller());
        Assert.Equal(new[] { "caught sync boom" }, log);
        Assert.Empty(errors);

        async DmTask Caller()
        {
            try { await ThrowsSync(); log.Add("not reached"); }
            catch (InvalidOperationException e) { log.Add("caught " + e.Message); }
        }
    }

    [Fact]
    public void DetachedErrorBeforeFirstSuspensionIsReportedAndTheCallerContinues()
    {
        // INVOKE_ASYNC is a separate thread in DM; the callee's runtime never stops the caller.
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.AddPhase("signal handler", x =>
        {
            log.Add("handler start");
            Waiting.Detach(Worker());
            log.Add("handler continues");
        });
        w.Step(1);
        Assert.Equal(new[] { "handler start", "worker start", "handler continues" }, log);
        Assert.Equal(new[] { "InvalidOperationException: worker runtime" }, errors);

        async DmTask Worker()
        {
            log.Add("worker start");
            throw new InvalidOperationException("worker runtime");
        }
    }

    [Fact]
    public void ErrorAfterSuspensionIsRethrownToTheAwaitingCaller()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Caller());
        w.Step(2);
        Assert.Equal(new[] { "caught async boom" }, log);
        Assert.Empty(errors);

        async DmTask Caller()
        {
            try { await ThrowsAfterSleep(1); log.Add("not reached"); }
            catch (InvalidOperationException e) { log.Add("caught " + e.Message); }
        }
    }

    [Fact]
    public void DetachedErrorAfterSuspensionIsReportedOnceAndTheWorldCarriesOn()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(ThrowsAfterSleep(0.5f));
            Waiting.Detach(Other());
        }
        w.Step(4);
        Assert.Equal(new[] { "InvalidOperationException: async boom" }, errors);
        Assert.Equal(new[] { "other done" }, log);
        Assert.Equal(1, w.RuntimeErrorCount);

        async DmTask Other() { await Sleep(1.5f); log.Add("other done"); }
    }

    [Fact]
    public void AwaitedCalleeRuntimeAfterSleepUnwindsTheCallerToItsBoundary()
    {
        // DEVIATION pinned (docs/ai/DEVIATIONS.md, waiting-model.md "Runtime errors"): in DM the callee's runtime ends only the
        // callee and its waiting caller continues with the callee's '.'. Here the caller unwinds too, up to
        // the boundary (Waiting.Detach), which reports the error once.
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Caller());
        w.Step(4);
        Assert.DoesNotContain("caller continues", log);
        Assert.Equal(new[] { "InvalidOperationException: callee runtime" }, errors);

        async DmTask Caller() { await Callee(); log.Add("caller continues"); }
        static async DmTask Callee() { await Sleep(0.5f); throw new InvalidOperationException("callee runtime"); }
    }

    [Fact]
    public void ErrorAfterSuspensionThreeDeepIsReportedExactlyOnce()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter()) Waiting.Detach(Mid(() => Mid(() => Mid(() => ThrowAfterT(1)))));
        w.Step(5);
        Assert.Equal(new[] { "InvalidOperationException: after" }, errors);
    }

    [Fact]
    public void ErrorBeforeSuspensionThreeDeepIsCapturedAtTheTopAndReportedOnceByDetach()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter())
        {
            var t = Mid(() => Mid(() => Mid(ThrowsSyncT)));
            Assert.True(t.IsFaulted);
            Assert.Empty(errors);
            Waiting.Detach(t);
        }
        Assert.Equal(new[] { "InvalidOperationException: sync boom" }, errors);
    }

    [Fact]
    public void SyncErrorInACalleeOfAnAlreadySuspendedCallerIsReportedOnce()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter()) Waiting.Detach(Outer());
        w.Step(5);
        Assert.Equal(new[] { "InvalidOperationException: sync boom" }, errors);

        static async DmTask Outer() { await Sleep(1); await ThrowsSyncT(); }
    }

    [Fact]
    public void SchedulerEntriesAndPhasesAreIsolationBoundaries()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        w.Scheduler.At(1, () => throw new InvalidOperationException("entry"));
        w.Scheduler.At(1, () => log.Add("next entry runs"));
        w.AddPhase("p1", _ => throw new InvalidOperationException("phase"));
        w.AddPhase("p2", _ => log.Add("next phase runs"));
        w.Step(1);
        Assert.Equal(new[] { "next entry runs", "next phase runs" }, log);
        Assert.Equal(new[] { "InvalidOperationException: entry", "InvalidOperationException: phase" }, errors);
    }

    // ---- misuse: double await, foreign awaiters, reading a pending task, no world ----

    [Fact]
    public void SleepingOutsideAWorldFaultsTheTaskAndDetachRethrowsInsteadOfLosingIt()
    {
        Assert.Null(World.CurrentOrNull);
        var t = SleepsOnce();
        Assert.True(t.IsFaulted);
        Assert.Contains("No ambient World", t.Exception.Message);
        var thrown = Assert.Throws<InvalidOperationException>(() => Waiting.Detach(t));
        Assert.Contains("No ambient World", thrown.Message);

        static async DmTask<int> SleepsOnce() { await Sleep(0.5f); return 1; }
    }

    [Fact]
    public void AwaitingAPendingTaskTwiceFaultsTheSecondAwaiterOnly()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            var shared = Slow();
            Waiting.Detach(A(shared));
            Waiting.Detach(Root(shared));
        }
        w.Step(5);
        Assert.Equal(new[] { "root caught: A DmTask can be awaited by one caller only.", "a=2" }, log);
        Assert.Empty(errors); // the misuse reached Root's try/catch once and was not reported a second time

        async DmTask A(DmTask<int> t) { log.Add("a=" + await t); }
        async DmTask Root(DmTask<int> t)
        {
            try { await B(t); }
            catch (InvalidOperationException e) { log.Add("root caught: " + e.Message); }
        }
        async DmTask B(DmTask<int> t) { log.Add("b=" + await t); }
        static async DmTask<int> Slow() { await Sleep(0.5f); return 2; }
    }

    [Fact]
    public void ATaskStoredAndAwaitedLaterIsReportedWhenItFailsAndRethrownToTheLateAwaiter()
    {
        // A rule violation (await a task right away, once): nobody awaited it when it failed, so the failure
        // was the top of a thread and was reported; the late awaiter still observes it.
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Root());
        w.Step(10);
        Assert.Equal(new[] { "caught after" }, log);
        Assert.Equal(new[] { "InvalidOperationException: after" }, errors);

        async DmTask Root()
        {
            var t = ThrowAfterT(0.5f);
            await Sleep(2);
            try { await t; }
            catch (Exception e) { log.Add("caught " + e.Message); }
        }
    }

    [Fact]
    public void AwaitingAForeignAwaiterIsARuntimeErrorAtTheAwait()
    {
        var w = KernelTestWorld.New(out var errors);
        int ranOnThread = -1;
        DmTask first, later;
        using (w.Enter())
        {
            first = DelayFirst();
            later = DelayAfterSleep();
            Waiting.Detach(later);
        }
        Assert.True(first.IsFaulted);
        Assert.Contains("not a kernel awaiter", first.Exception.Message);
        w.Step(2);
        Thread.Sleep(50);
        Assert.Equal(-1, ranOnThread); // no thread-pool continuation ever ran
        Assert.Single(errors);
        Assert.Contains("not a kernel awaiter", errors[0]);

#pragma warning disable ANOM0004 // the test pins that a foreign awaiter is refused at run time and never leaves the tick
        async DmTask DelayFirst()
        {
            await System.Threading.Tasks.Task.Delay(1);
            ranOnThread = Environment.CurrentManagedThreadId;
        }

        async DmTask DelayAfterSleep()
        {
            await Sleep(0.5f);
            await System.Threading.Tasks.Task.Yield();
            ranOnThread = Environment.CurrentManagedThreadId;
        }
#pragma warning restore ANOM0004
    }

    [Fact]
    public void ReadingAPendingTaskThrows()
    {
        var w = KernelTestWorld.New(out _);
        using (w.Enter())
        {
            var t = Slow();
            Assert.False(t.IsCompleted);
            Assert.False(t.TryGetResult(out _));
            var e = Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult());
            Assert.Contains("before completion", e.Message);

            var u = SlowVoid();
            Assert.Throws<InvalidOperationException>(() => u.GetAwaiter().GetResult());
            Waiting.Detach(t);
            Waiting.Detach(u);
        }

        static async DmTask<int> Slow() { await Sleep(1); return 42; }
        static async DmTask SlowVoid() { await Sleep(1); }
    }

    [Fact]
    public void TryGetResultReportsTheValueOnlyOnceCompletedWithoutError()
    {
        var w = KernelTestWorld.New(out _);
        DmTask<int> sync, slow;
        using (w.Enter())
        {
            sync = Value(false);
            slow = Value(true);
        }
        Assert.True(sync.TryGetResult(out var a));
        Assert.Equal(7, a);
        Assert.False(slow.TryGetResult(out _));
        w.Step(1);
        Assert.True(slow.IsCompleted);
        Assert.True(slow.TryGetResult(out var b));
        Assert.Equal(7, b);
        Assert.True(DmTask<int>.FromResult(7).TryGetResult(out var c));
        Assert.Equal(7, c);
        Assert.True(DmTask<int>.FromException(new InvalidOperationException("x")).IsFaulted);
        Assert.True(DmTask.FromException(new InvalidOperationException("x")).IsFaulted);

        static async DmTask<int> Value(bool sleep)
        {
            if (sleep) await Sleep(0.5f);
            return 7;
        }
    }
}
