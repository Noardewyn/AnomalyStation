using System.Runtime.CompilerServices;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// Every call form of waiting-model.md section 5 against every way a callee can end (completes at once, fails at
/// once, sleeps and then returns, sleeps and then fails). Run the suite in Debug and Release: Release builds use
/// struct state machines and a different builder copy path.
/// </summary>
public class CallFormMatrixTests
{
    public enum Form { Detach, DetachT, NoSleep, NoSleepT, WaitforFalse, WaitforFalseT }

    public enum Callee { SyncValue, SyncError, SleepsThenValue, SleepsThenError }

    private sealed class Probe
    {
        public readonly List<string> Log = new();

        // A coloured proc: '.' = 3 before it may sleep, returns 7.
        public async DmTask<int> Run(Callee how, DotBox<int> dot)
        {
            Log.Add("callee start");
            if (dot != null) dot.Value = 3;
            if (how == Callee.SyncError) throw new InvalidOperationException("sync error");
            if (how is Callee.SleepsThenValue or Callee.SleepsThenError)
            {
                await Sleep(0.5f);
                Log.Add("callee resumed");
                if (how == Callee.SleepsThenError) throw new InvalidOperationException("late error");
            }
            Log.Add("callee end");
            return 7;
        }

        public async DmTask RunVoid(Callee how) => await Run(how, null);
    }

    [Theory]
    // form,            callee,                   value, throws, reports now, reports after the step
    [InlineData(Form.Detach,        Callee.SyncValue,       0, false, 0, 0)]
    [InlineData(Form.Detach,        Callee.SyncError,       0, false, 1, 1)]
    [InlineData(Form.Detach,        Callee.SleepsThenValue, 0, false, 0, 0)]
    [InlineData(Form.Detach,        Callee.SleepsThenError, 0, false, 0, 1)]
    [InlineData(Form.DetachT,       Callee.SyncValue,       7, false, 0, 0)]
    [InlineData(Form.DetachT,       Callee.SyncError,       0, false, 1, 1)]
    [InlineData(Form.DetachT,       Callee.SleepsThenValue, 0, false, 0, 0)]
    [InlineData(Form.DetachT,       Callee.SleepsThenError, 0, false, 0, 1)]
    [InlineData(Form.NoSleep,       Callee.SyncValue,       0, false, 0, 0)]
    [InlineData(Form.NoSleep,       Callee.SyncError,       0, true,  0, 0)]
    [InlineData(Form.NoSleep,       Callee.SleepsThenValue, 0, false, 1, 1)]
    [InlineData(Form.NoSleep,       Callee.SleepsThenError, 0, false, 1, 2)]
    [InlineData(Form.NoSleepT,      Callee.SyncValue,       7, false, 0, 0)]
    [InlineData(Form.NoSleepT,      Callee.SyncError,       0, true,  0, 0)]
    [InlineData(Form.NoSleepT,      Callee.SleepsThenValue, 0, false, 1, 1)]
    [InlineData(Form.NoSleepT,      Callee.SleepsThenError, 0, false, 1, 2)]
    [InlineData(Form.WaitforFalse,  Callee.SyncValue,       0, false, 0, 0)]
    [InlineData(Form.WaitforFalse,  Callee.SyncError,       0, true,  0, 0)]
    [InlineData(Form.WaitforFalse,  Callee.SleepsThenValue, 0, false, 0, 0)]
    [InlineData(Form.WaitforFalse,  Callee.SleepsThenError, 0, false, 0, 1)]
    [InlineData(Form.WaitforFalseT, Callee.SyncValue,       7, false, 0, 0)]
    [InlineData(Form.WaitforFalseT, Callee.SyncError,       0, true,  0, 0)]
    [InlineData(Form.WaitforFalseT, Callee.SleepsThenValue, 3, false, 0, 0)] // '.' at the sleep
    [InlineData(Form.WaitforFalseT, Callee.SleepsThenError, 3, false, 0, 1)]
    public void EachCallFormGivesTheCallerWhatSection5Promises(Form form, Callee how, int value, bool throws, int reportsNow, int reportsAfter)
    {
        var w = KernelTestWorld.New(out var errors);
        var p = new Probe();
        int got = -1;
        Exception thrown = null;
        // A synchronous caller inside a phase (a signal handler, a subsystem fire, a command).
        w.AddPhase("caller", x =>
        {
            if (x.Tick != 1) return;
            try
            {
                switch (form)
                {
                    case Form.Detach: Waiting.Detach(p.RunVoid(how)); got = 0; break;
                    case Form.DetachT: got = Waiting.Detach(p.Run(how, null)); break;
                    case Form.NoSleep: Waiting.NoSleep(p.RunVoid(how)); got = 0; break;
                    case Form.NoSleepT: got = Waiting.NoSleep(p.Run(how, null)); break;
                    case Form.WaitforFalse: Waiting.WaitforFalse(p.RunVoid(how)); got = 0; break;
                    case Form.WaitforFalseT: { var dot = new DotBox<int>(); got = Waiting.WaitforFalse(p.Run(how, dot), dot); break; }
                }
                p.Log.Add("caller continues");
            }
            catch (InvalidOperationException e)
            {
                thrown = e;
            }
        });
        w.Step(1);

        Assert.Equal(throws, thrown != null);
        if (throws) Assert.Equal("sync error", thrown.Message);
        else Assert.Equal(value, got);
        Assert.Equal(reportsNow, errors.Count);
        if (form is Form.NoSleep or Form.NoSleepT && how is Callee.SleepsThenValue or Callee.SleepsThenError)
            Assert.Contains("slept inside a no-sleep context", errors[0]);

        w.Step(2);
        Assert.Equal(reportsAfter, errors.Count);
        if (how == Callee.SleepsThenError) Assert.Equal("InvalidOperationException: late error", errors[^1]);
        if (how is Callee.SleepsThenValue or Callee.SleepsThenError) Assert.Contains("callee resumed", p.Log);
        if (how == Callee.SleepsThenValue) Assert.Equal("callee end", p.Log[^1]);
        Assert.Equal(0, w.Scheduler.Pending);
        Assert.Equal(errors.Count, w.RuntimeErrorCount);
    }

    [Fact]
    public void ADetachedErrorNeverReachesTheCallersOwnTryCatch()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            try
            {
                Waiting.Detach(Fails());
                log.Add("continued");
            }
            catch (Exception e)
            {
                log.Add("caught " + e.Message);
            }
        }
        Assert.Equal(new[] { "continued" }, log);
        Assert.Equal(new[] { "InvalidOperationException: boom" }, errors);

        static async DmTask Fails() { throw new InvalidOperationException("boom"); }
    }

    [Fact]
    public void DetachSyncWithoutAWorldRethrowsTheOriginalError()
    {
        // Section 9.5: with no world to report to, the error is not lost (Detach rethrows it); DetachSync must
        // not replace it with "No ambient World".
        Assert.Null(World.CurrentOrNull);
        var e = Assert.Throws<InvalidOperationException>(() => Waiting.DetachSync(() => throw new InvalidOperationException("original")));
        Assert.Equal("original", e.Message);
    }

    [Fact]
    public void FromExceptionRejectsNullLikeTheNonGenericForm()
    {
        Assert.Throws<ArgumentNullException>(() => DmTask.FromException(null));
        Assert.Throws<ArgumentNullException>(() => DmTask<int>.FromException(null));
    }

    [Fact]
    public void APendingTaskIsNeitherCompletedNorFaultedAndHasNoException()
    {
        var w = KernelTestWorld.New(out var errors);
        DmTask<int> t;
        DmTask u;
        using (w.Enter())
        {
            t = Slow();
            u = SlowVoid();
            Assert.False(t.IsCompleted);
            Assert.False(t.IsFaulted);
            Assert.Null(t.Exception);
            Assert.False(u.IsCompleted);
            Assert.False(u.IsFaulted);
            Assert.Null(u.Exception);
            Assert.False(((IDmTask)t).IsCompleted);
            Assert.False(t.AsObjectTask().IsCompleted);
            Waiting.Detach(u);
        }
        w.Step(2);
        Assert.True(t.IsCompleted);
        Assert.True(t.TryGetResult(out var v));
        Assert.Equal(1, v);
        Assert.Empty(errors);

        static async DmTask<int> Slow() { await Sleep(0.5f); return 1; }
        static async DmTask SlowVoid() { await Sleep(0.5f); }
    }
}

/// <summary>Exception capture and rethrow at await (waiting-model.md section 9), in Debug and Release.</summary>
public class ErrorCaptureAttackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinallyBlocksRunInnermostFirstBeforeTheCallerCatches(bool sleepFirst)
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Top());
        w.Step(3);
        Assert.Equal(new[] { "inner finally", "middle finally", "top caught boom" }, log);
        Assert.Empty(errors);

        async DmTask Top()
        {
            try { await Middle(); log.Add("not reached"); }
            catch (InvalidOperationException e) when (e.Message == "boom") { log.Add("top caught " + e.Message); }
        }

        async DmTask<int> Middle()
        {
            try { return 1 + await Inner(); }
            finally { log.Add("middle finally"); }
        }

        async DmTask<int> Inner()
        {
            try
            {
                if (sleepFirst) await Sleep(0.5f);
                throw new InvalidOperationException("boom");
            }
            finally { log.Add("inner finally"); }
        }
    }

    [Fact]
    public void TheRethrownErrorIsTheSameObjectAndKeepsTheThrowersStackTrace()
    {
        var w = KernelTestWorld.New(out _);
        var original = new InvalidOperationException("same");
        Exception seen = null;
        using (w.Enter()) Waiting.Detach(Catcher());
        w.Step(2);
        Assert.Same(original, seen);
        Assert.Contains(nameof(ThrowerAfterSleep), seen.StackTrace);

        async DmTask Catcher()
        {
            try { await ThrowerAfterSleep(original); }
            catch (Exception e) { seen = e; }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async DmTask ThrowerAfterSleep(Exception e)
    {
        await Sleep(0.5f);
        throw e;
    }

    [Fact]
    public void AnErrorUnwindingADeepChainBeyondTheInlineDepthIsCaughtOnceInOrder()
    {
        // 300 levels: the resumptions beyond depth 64 go through the trampoline queue.
        var w = KernelTestWorld.New(out var errors);
        var finallies = new List<int>();
        string caught = null;
        using (w.Enter()) Waiting.Detach(Top());
        w.Step(2);
        Assert.Equal("deep", caught);
        Assert.Equal(Enumerable.Range(0, 301), finallies);
        Assert.Empty(errors);

        async DmTask Top()
        {
            try { await Level(300); }
            catch (Exception e) { caught = e.Message; }
        }

        async DmTask Level(int n)
        {
            try
            {
                if (n == 0) { await Sleep(0.5f); throw new InvalidOperationException("deep"); }
                await Level(n - 1);
            }
            finally { finallies.Add(n); }
        }
    }

    [Fact]
    public void AnErrorRaisedAfterTheCallerHasCaughtAnotherIsStillDeliveredToIt()
    {
        // The caller recovers from one callee's runtime and awaits another that fails later: both reach it.
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(Caller());
        w.Step(5);
        Assert.Equal(new[] { "first: one", "second: two" }, log);
        Assert.Empty(errors);

        async DmTask Caller()
        {
            try { await Fail("one", 0.5f); } catch (Exception e) { log.Add("first: " + e.Message); }
            try { await Fail("two", 1f); } catch (Exception e) { log.Add("second: " + e.Message); }
        }

        static async DmTask Fail(string m, float ds) { await Sleep(ds); throw new InvalidOperationException(m); }
    }
}

/// <summary>Only kernel awaiters may be awaited inside a DmTask proc (frozen decision 2(g)).</summary>
public class AwaiterCheckAttackTests
{
    // A foreign awaiter that is never complete and must never be handed a continuation.
    private readonly struct ForeignCritical
    {
        public Awaiter GetAwaiter() => default;

        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            public bool IsCompleted => false;
            public void OnCompleted(Action c) => throw new Xunit.Sdk.XunitException("continuation handed to a foreign awaiter");
            public void UnsafeOnCompleted(Action c) => throw new Xunit.Sdk.XunitException("continuation handed to a foreign awaiter");
            public void GetResult() { }
        }
    }

    // A foreign awaiter that implements only INotifyCompletion: the compiler goes through AwaitOnCompleted.
    private readonly struct ForeignPlain
    {
        public Awaiter GetAwaiter() => default;

        public readonly struct Awaiter : INotifyCompletion
        {
            public bool IsCompleted => false;
            public void OnCompleted(Action c) => throw new Xunit.Sdk.XunitException("continuation handed to a foreign awaiter");
            public int GetResult() => 0;
        }
    }

    // A kernel-marked awaiter defined outside the kernel: the marker, not the assembly, is what is checked.
    private sealed class Gate : INotifyCompletion, IDmAwaiter
    {
        private Action _continuation;
        public bool Opened;
        public Gate GetAwaiter() => this;
        public bool IsCompleted => Opened;
        public void OnCompleted(Action c) => _continuation = c;
        public void GetResult() { }
        public void Open() { Opened = true; var c = _continuation; _continuation = null; c?.Invoke(); }
    }

    [Fact]
    public void AForeignCriticalAwaiterFaultsBothBuildersAtTheAwait()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter())
        {
            var a = Void();
            var b = Value();
            Assert.True(a.IsFaulted);
            Assert.True(b.IsFaulted);
            Assert.Contains("not a kernel awaiter", a.Exception.Message);
            Assert.Contains("not a kernel awaiter", b.Exception.Message);
        }
        Assert.Empty(errors); // captured in the tasks, nobody detached them

#pragma warning disable ANOM0004 // the test pins the builders' run-time refusal of a foreign awaiter
        static async DmTask Void() { await new ForeignCritical(); }
        static async DmTask<int> Value() { await new ForeignCritical(); return 1; }
#pragma warning restore ANOM0004
    }

    [Fact]
    public void AForeignNonCriticalAwaiterFaultsBothBuildersAtTheAwait()
    {
        var w = KernelTestWorld.New(out _);
        using (w.Enter())
        {
            var a = Void();
            var b = Value();
            Assert.Contains("not a kernel awaiter", a.Exception.Message);
            Assert.Contains("not a kernel awaiter", b.Exception.Message);
        }

#pragma warning disable ANOM0004 // the test pins the builders' run-time refusal of a foreign awaiter
        static async DmTask Void() { await new ForeignPlain(); }
        static async DmTask<int> Value() => await new ForeignPlain();
#pragma warning restore ANOM0004
    }

    [Fact]
    public void AForeignAwaiterAfterASleepIsReportedOnceThroughTheBoundary()
    {
        var w = KernelTestWorld.New(out var errors);
        using (w.Enter()) Waiting.Detach(P());
        w.Step(2);
        Assert.Single(errors);
        Assert.Contains("not a kernel awaiter", errors[0]);

#pragma warning disable ANOM0004 // the test pins the builders' run-time refusal of a foreign awaiter
        static async DmTask<int> P() { await Sleep(0.5f); return await new ForeignPlain(); }
#pragma warning restore ANOM0004
    }

    [Fact]
    public void AnyAwaiterCarryingTheMarkerIsAccepted()
    {
        var w = KernelTestWorld.New(out var errors);
        var gate = new Gate();
        var log = new List<string>();
        using (w.Enter()) Waiting.Detach(P());
        Assert.Equal(new[] { "waiting" }, log);
        w.Scheduler.At(2, gate.Open);
        w.Step(2);
        Assert.Equal(new[] { "waiting", "opened@2" }, log);
        Assert.Empty(errors);

        async DmTask P() { log.Add("waiting"); await gate; log.Add($"opened@{World.Current.Tick}"); }
    }

    [Fact]
    public void TheForeignAwaiterErrorIsThrownAtTheAwaitAndTheProcCanCatchItAndGoOn()
    {
        // The check throws inside the proc, at the await, before any suspension: a DM try/catch around it sees it
        // and the proc keeps working (its state machine was not boxed or left half-suspended).
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        DmTask<int> t;
        using (w.Enter()) t = P();
        Assert.False(t.IsCompleted);
        w.Step(2);
        Assert.True(t.TryGetResult(out var v));
        Assert.Equal(5, v);
        Assert.Equal(new[] { "caught at the await", "slept", "slept again" }, log);
        Assert.Empty(errors);

        async DmTask<int> P()
        {
            int x = 2;
#pragma warning disable ANOM0004 // the test pins the builders' run-time refusal of a foreign awaiter
            try { await new ForeignCritical(); x = 100; }
#pragma warning restore ANOM0004
            catch (InvalidOperationException e) when (e.Message.Contains("not a kernel awaiter")) { log.Add("caught at the await"); }
            await Sleep(0.5f);
            log.Add("slept");
            x += 3;
            await Sleep(0);
            log.Add("slept again");
            return x;
        }
    }

    [Fact]
    public void AForeignAwaitableThatIsAlreadyCompleteIsNotDetected()
    {
        // Pinned limitation (waiting-model.md 4.6): the builder sees an awaiter only when the proc suspends, so an
        // already completed Task or ValueTask passes without an error. Nothing leaves the tick, so determinism
        // holds; the analyzer flags the lines at build time (ANOM0004, waiting-model.md 5.6), suppressed here.
        var w = KernelTestWorld.New(out var errors);
        DmTask<int> t;
        using (w.Enter()) t = P();
        Assert.True(t.TryGetResult(out var v));
        Assert.Equal(3, v);
        Assert.Empty(errors);

        static async DmTask<int> P()
        {
#pragma warning disable ANOM0004 // the test pins that the run-time check misses an already completed foreign awaitable
            await System.Threading.Tasks.Task.CompletedTask;
            return await new System.Threading.Tasks.ValueTask<int>(3);
#pragma warning restore ANOM0004
        }
    }
}
