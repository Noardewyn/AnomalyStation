using System.Diagnostics;
using Xunit.Abstractions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// A sanity check that calls by name use the cached compiled invoker (object-model.md section 12: "Invokers are
/// compiled once per (runtime type, name)"): 100,000 calls run in well under a second and allocate only the bound
/// argument array and the boxed result per call, nothing that reflection would. Generous bounds: a parallel test run
/// on a loaded machine must still pass; per-call reflection would miss them by an order of magnitude.
/// </summary>
public class CallPerformanceTests
{
    private const int Calls = 100_000;
    private readonly ITestOutputHelper _output;

    public CallPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void OneHundredThousandCachedCallsAreCheap()
    {
        var callee = new fx_callee();
        object warm = Waiting.NoSleep(Dm.Call(callee, "add_one", 1));   // resolves and compiles once
        Assert.Equal(2f, warm);

        // M3 I1d (load tolerance): the allocation bound is what proves the call is cached (a resolution or a compile per call
        // allocates kilobytes); the time is the best of three rounds, so a machine busy with other suites does not fail it
        // (I1b saw 3.2 s at 300% CPU against 72 ms alone), while a call that is not cached stays far above the bound.
        long perCall = 0;
        long bestMs = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch watch = Stopwatch.StartNew();
            float sum = 0;
            for (int i = 0; i < Calls; i++)
            {
                DmTask<object> task = Dm.Call(callee, "add_one", 1f);
                task.TryGetResult(out object result);
                sum += (float)result;
            }
            watch.Stop();
            if (round == 0)
                perCall = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / Calls;
            bestMs = Math.Min(bestMs, watch.ElapsedMilliseconds);
            Assert.Equal(2f * Calls, sum);
            if (bestMs < 500)
                break;
        }

        Assert.True(perCall <= 160, $"a cached call allocated {perCall} bytes");
        // The time is reported, and checked only in a quiet perf run (ANOMALY_PERF=1): inside the full parallel suite, beside
        // Meta worlds of several hundred MB, the lead saw 13 s per round against 39 ms alone (2026-10-09), so a wall-clock
        // bound there tests the neighbours, not the cache; the allocation bound above is what proves the call is cached.
        _output.WriteLine($"100k cached calls: {bestMs} ms (the best of three rounds), {perCall} bytes per call");
        if (Environment.GetEnvironmentVariable("ANOMALY_PERF") == "1")
            Assert.True(bestMs < 2000, $"100k cached calls took {bestMs} ms (the best of three rounds)");
    }
}
