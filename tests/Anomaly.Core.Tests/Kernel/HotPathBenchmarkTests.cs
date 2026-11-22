using System.Diagnostics;
using System.Runtime.CompilerServices;
using Anomaly.Core.Kernel;
using Xunit.Abstractions;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// Hot-path costs of the waiting model, reported for docs/ai/specs/waiting-model.md ("Hot-path costs"). No hard
/// gate: the numbers depend on the machine and the configuration (run with -c Release for real numbers;
/// Debug builds use class state machines). Only correctness is asserted.
/// </summary>
public class HotPathBenchmarkTests
{
    private readonly ITestOutputHelper _out;
    public HotPathBenchmarkTests(ITestOutputHelper output) => _out = output;

    private class Proc
    {
        public int X;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual int SyncLevel(int depth) => depth == 0 ? ++X : SyncLevel(depth - 1) + 1;

        public virtual async DmTask<int> AsyncLevel(int depth) => depth == 0 ? ++X : await AsyncLevel(depth - 1) + 1;

        public virtual int Wrapped(int v)
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(Wrapped__body(v, dot), dot);
        }

        private async DmTask<int> Wrapped__body(int v, DotBox<int> dot)
        {
            dot.Value = v;
            return v + 1;
        }
    }

    private static (double nsPerCall, double bytesPerCall) Measure(int n, Func<int, long> body)
    {
        // Warm up long enough for tiered compilation to promote the state machines (tier 0 is several times
        // slower, and call counting starts only after a ~100 ms quiet period).
        var warm = Stopwatch.StartNew();
        while (warm.ElapsedMilliseconds < 250) body(1000);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        long check = body(n);
        sw.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(check > 0);
        return (sw.Elapsed.TotalMilliseconds * 1e6 / n, (double)allocated / n);
    }

    [Fact]
    public void ReportHotPathCosts()
    {
#if DEBUG
        _out.WriteLine("configuration: Debug (class state machines; use -c Release for the documented numbers)");
#else
        _out.WriteLine("configuration: Release (struct state machines)");
#endif
        var w = new World(1);
        using (w.Enter())
        {
            var p = new Proc();
            const int n = 200_000;

            var sync = Measure(n, k => { long s = 0; for (int i = 0; i < k; i++) s += p.SyncLevel(3); return s; });
            var colored = Measure(n, k =>
            {
                long s = 0;
                for (int i = 0; i < k; i++) s += p.AsyncLevel(3).GetAwaiter().GetResult();
                return s;
            });
            var wrapped = Measure(n, k => { long s = 0; for (int i = 0; i < k; i++) s += p.Wrapped(i); return s; });
            _out.WriteLine($"4-deep virtual call chain, plain int:          {sync.nsPerCall,6:F1} ns/call {sync.bytesPerCall,6:F1} B/call");
            _out.WriteLine($"4-deep virtual call chain, DmTask<int> (sync): {colored.nsPerCall,6:F1} ns/call {colored.bytesPerCall,6:F1} B/call");
            _out.WriteLine($"waitfor=FALSE wrapper + DotBox (sync body):    {wrapped.nsPerCall,6:F1} ns/call {wrapped.bytesPerCall,6:F1} B/call");
        }

        // A proc that suspends once: what one sleep costs (state, boxed state machine, delegate, queue entry).
        {
            var sw = new World(1);
            const int procs = 20_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            using (sw.Enter()) for (int i = 0; i < procs; i++) Waiting.Detach(SleepOnce());
            long suspendBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            sw.Step(2);
            long wakeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, sw.Scheduler.Pending);
            _out.WriteLine($"first suspension of a proc: {(double)suspendBytes / procs,6:F1} B; its wake-up and completion: {(double)wakeBytes / procs,6:F1} B");
        }

        // 10k procs that wake every tick, each awaiting a callee that sleeps every other tick.
        {
            var lw = new World(1);
            const int procs = 10_000;
            const int ticks = 50;
            long wakeups = 0;
            using (lw.Enter()) for (int i = 0; i < procs; i++) Waiting.Detach(Loop());
            lw.Step(10);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int gen0 = GC.CollectionCount(0);
            long start = wakeups;
            var sw = Stopwatch.StartNew();
            lw.Step(ticks);
            sw.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            long done = wakeups - start;
            Assert.True(done >= procs * (long)ticks);
            _out.WriteLine($"{procs} procs waking every tick: {sw.Elapsed.TotalMilliseconds / ticks,6:F2} ms/tick, "
                + $"{bytes / ticks / 1024.0,8:F1} KB/tick, {(double)bytes / done,6:F1} B/wake-up, gen0 GCs: {GC.CollectionCount(0) - gen0}");

            async DmTask Loop()
            {
                while (true)
                {
                    await Sleep(World.TickLag);
                    wakeups++;
                    await Inner();
                }
            }

            async DmTask Inner()
            {
                if ((wakeups & 1) == 0) await Sleep(0);
            }
        }

        static async DmTask SleepOnce() { await Sleep(0.5f); }
    }
}
