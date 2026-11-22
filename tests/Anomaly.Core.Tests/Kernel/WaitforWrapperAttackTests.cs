using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// The callee-side wrapper of <c>set waitfor = FALSE</c> (waiting-model.md 5.4): '.' at the first suspension,
/// inheritance through overrides that call ..(), and the value callers act on (PROCESS_KILL).
/// </summary>
public class WaitforWrapperAttackTests
{
    private const int PROCESS_KILL = 26;

    // ---- '.' at the first suspension, whatever suspends ----

    private sealed class Owner
    {
        public readonly List<string> Log = new();

        // set waitfor = FALSE; '.' = 1, then awaits a waitfor = TRUE callee that sleeps, then '.' = 2.
        public int Act()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(Act__body(dot), dot);
        }

        private async DmTask<int> Act__body(DotBox<int> dot)
        {
            dot.Value = 1;
            int v = await SlowCallee();
            dot.Value = 2 + v;
            Log.Add($"body finished with {dot.Value}");
            return dot.Value;
        }

        private static async DmTask<int> SlowCallee() { await Sleep(0.5f); return 40; }

        // set waitfor = FALSE, calls another waitfor = FALSE proc that sleeps: B's wrapper returns at B's sleep,
        // so this body never suspends and its caller gets its return value.
        public int Outer()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(Outer__body(dot), dot);
        }

        private async DmTask<int> Outer__body(DotBox<int> dot)
        {
            dot.Value = Act();
            Log.Add($"outer got {dot.Value}");
            return dot.Value + 10;
        }
    }

    [Fact]
    public void TheCallerGetsDotAtTheFirstSuspensionEvenWhenItIsAnAwaitedCallee()
    {
        var w = KernelTestWorld.New(out var errors);
        var o = new Owner();
        int got;
        using (w.Enter()) got = o.Act();
        Assert.Equal(1, got);
        w.Step(2);
        Assert.Equal(new[] { "body finished with 42" }, o.Log); // later writes to '.' never reach the caller
        Assert.Empty(errors);
    }

    [Fact]
    public void AWaitforFalseCalleeThatSleepsLetsAWaitforFalseCallerFinishAtOnce()
    {
        var w = KernelTestWorld.New(out var errors);
        var o = new Owner();
        int got;
        using (w.Enter()) got = o.Outer();
        Assert.Equal(11, got);
        Assert.Equal(new[] { "outer got 1" }, o.Log);
        w.Step(2);
        Assert.Equal(new[] { "outer got 1", "body finished with 42" }, o.Log);
        Assert.Empty(errors);
    }

    // ---- inheritance through two override levels ----

    private class parent
    {
        public List<string> log;

        // set waitfor = FALSE
        public virtual int act()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(act__body(dot), dot);
        }

        private async DmTask<int> act__body(DotBox<int> dot)
        {
            dot.Value = 5;
            await Sleep(1);
            log.Add("parent resumed");
            return 6;
        }
    }

    private class child : parent
    {
        // Inherits waitfor = FALSE; sleeps only through ..(): a plain method.
        public override int act() => base.act() * 2;
    }

    private sealed class grandchild : child
    {
        // Inherits waitfor = FALSE and sleeps itself: its own wrapper and body.
        public override int act()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(act__body(dot), dot);
        }

        private async DmTask<int> act__body(DotBox<int> dot)
        {
            dot.Value = base.act(); // ..() -> child -> parent's wrapper: 5 * 2
            log.Add($"grandchild got {dot.Value} from ..()");
            await Sleep(0.5f);
            dot.Value += 100;
            log.Add($"grandchild resumed with {dot.Value}");
            return dot.Value;
        }
    }

    [Fact]
    public void WaitforFalseIsInheritedThroughOverridesThatCallTheParent()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        int fromChild, fromGrandchild;
        using (w.Enter())
        {
            fromChild = new child { log = log }.act();
            fromGrandchild = new grandchild { log = log }.act();
        }
        Assert.Equal(10, fromChild);
        Assert.Equal(10, fromGrandchild); // '.' at the grandchild's own sleep
        Assert.Equal(new[] { "grandchild got 10 from ..()" }, log);
        w.Step(2);
        Assert.Equal(new[] { "grandchild got 10 from ..()", "grandchild resumed with 110", "parent resumed", "parent resumed" }, log);
        Assert.Empty(errors);
    }

    // ---- process() and PROCESS_KILL ----

    private class datum
    {
        public List<string> log;
        public virtual int process(float seconds_per_tick) => PROCESS_KILL; // processing.dm:48, never sleeps
    }

    // Writes '. = PROCESS_KILL' before it sleeps: the caller sees PROCESS_KILL at the sleep and stops processing it.
    private sealed class dot_before_sleep : datum
    {
        public override int process(float seconds_per_tick)
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
        }

        private async DmTask<int> process__body(float seconds_per_tick, DotBox<int> dot)
        {
            dot.Value = PROCESS_KILL;
            await Sleep(1);
            log.Add("finished after being removed");
            return 0; // discarded
        }
    }

    // Calls ..() (the /datum default) and sleeps afterwards: '.' is ..()'s PROCESS_KILL.
    private sealed class dot_from_parent : datum
    {
        public override int process(float seconds_per_tick)
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
        }

        private async DmTask<int> process__body(float seconds_per_tick, DotBox<int> dot)
        {
            dot.Value = base.process(seconds_per_tick);
            await Sleep(1);
            dot.Value = 0;
            return dot.Value;
        }
    }

    // Fails before it sleeps: a plain runtime of a plain call, caught by the per-item boundary.
    private sealed class fails_at_once : datum
    {
        public override int process(float seconds_per_tick)
        {
            return Waiting.WaitforFalse(process__body(), null);
        }

        private async DmTask<int> process__body()
        {
            if (log != null) throw new InvalidOperationException("process runtime");
            await Sleep(1);
            return 0;
        }
    }

    [Fact]
    public void ProcessSeesDotAtTheSleepAndPerItemBoundariesKeepTheLoopGoing()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        var a = new dot_before_sleep { log = log };
        var b = new dot_from_parent { log = log };
        var c = new fails_at_once { log = log };
        var processing = new List<datum> { a, b, c };

        using (w.Enter())
        {
            // SSprocessing.fire with the per-item boundary of waiting-model.md 9.2.
            foreach (var thing in processing.ToList())
            {
                int r;
                try { r = thing.process(1f); }
                catch (Exception e) { World.Current.ReportRuntime(e); continue; }
                if (r == PROCESS_KILL) processing.Remove(thing);
            }
        }
        Assert.Equal(new datum[] { c }, processing);
        Assert.Equal(new[] { "InvalidOperationException: process runtime" }, errors);
        w.Step(3);
        Assert.Equal(new[] { "finished after being removed" }, log);
        Assert.Single(errors);
    }

    // ---- a wrapper with a value in a coloured slot ----

    private class atom
    {
        public virtual async DmTask<int> Enter(int x) { await Sleep(1); return x; } // a waitfor = TRUE sleeper
    }

    private sealed class area : atom
    {
        public readonly List<string> log = new();

        // set waitfor = FALSE in a coloured slot: an async wrapper that always completes at once.
        public override async DmTask<int> Enter(int x)
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(Enter__body(x, dot), dot);
        }

        private async DmTask<int> Enter__body(int x, DotBox<int> dot)
        {
            dot.Value = x + 1;
            dot.Value = await base.Enter(x); // ..() into the waitfor = TRUE parent, which sleeps
            log.Add("area body resumed");
            return dot.Value;
        }
    }

    [Fact]
    public void AValuedWrapperInAColouredSlotCompletesAtOnceWithDot()
    {
        var w = KernelTestWorld.New(out var errors);
        var a = new area();
        int got = -1;
        bool completedAtOnce = false;
        using (w.Enter()) Waiting.Detach(Caller());
        Assert.True(completedAtOnce);
        Assert.Equal(8, got);
        Assert.Empty(a.log);
        w.Step(2);
        Assert.Equal(new[] { "area body resumed" }, a.log);
        Assert.Empty(errors);

        async DmTask Caller()
        {
            atom target = a;
            var t = target.Enter(7);
            completedAtOnce = t.IsCompleted;
            got = await t;
        }
    }
}
