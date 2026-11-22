namespace Anomaly.Server.Tests.Server;

/// <summary>The latency histogram's math (scope 2i): fixed buckets, exact below 64 µs, 32 per power of two above, percentiles by rank.</summary>
public class MetricsTests
{
    [Fact]
    public void BucketsAreContiguousAndCoverTheRange()
    {
        long previousUpper = -1;
        int previousIndex = -1;
        for (long v = 0; v < 1 << 20; v = v < 4096 ? v + 1 : v + 37)
        {
            int index = LatencyHistogram.BucketOf(v);
            Assert.InRange(v, LatencyHistogram.LowerEdge(index), LatencyHistogram.UpperEdge(index));
            Assert.True(index >= previousIndex);
            previousIndex = index;
        }
        for (int i = 0; i < LatencyHistogram.BucketOf(LatencyHistogram.MaxValue); i++)
        {
            Assert.Equal(previousUpper + 1, LatencyHistogram.LowerEdge(i));
            previousUpper = LatencyHistogram.UpperEdge(i);
            Assert.Equal(i, LatencyHistogram.BucketOf(LatencyHistogram.LowerEdge(i)));
            Assert.Equal(i, LatencyHistogram.BucketOf(LatencyHistogram.UpperEdge(i)));
        }
        Assert.Equal(LatencyHistogram.MaxValue, LatencyHistogram.UpperEdge(LatencyHistogram.BucketOf(LatencyHistogram.MaxValue)));
    }

    [Fact]
    public void BucketsAreExactBelow64AndWithinAbout3PercentAbove()
    {
        for (long v = 0; v < 64; v++)
            Assert.Equal(v, LatencyHistogram.UpperEdge(LatencyHistogram.BucketOf(v)));
        foreach (long v in new long[] { 64, 100, 1000, 12345, 1_000_000, 50_000_000 })
        {
            int b = LatencyHistogram.BucketOf(v);
            double width = LatencyHistogram.UpperEdge(b) - LatencyHistogram.LowerEdge(b) + 1;
            Assert.True(width / LatencyHistogram.LowerEdge(b) <= 1.0 / 32 + 1e-12, $"bucket of {v} is {width} wide");
        }
    }

    [Fact]
    public void PercentilesOfAUniformRun()
    {
        var h = new LatencyHistogram();
        for (int v = 1; v <= 1000; v++) h.Record(v);
        Assert.Equal(1000, h.Count);
        Assert.Equal(1, h.Min);
        Assert.Equal(1000, h.Max);
        Assert.Equal(500.5, h.Mean);
        AssertNear(500, h.Percentile(50));
        AssertNear(950, h.Percentile(95));
        AssertNear(990, h.Percentile(99));
        AssertNear(999, h.Percentile(99.9));
        Assert.Equal(1000, h.Percentile(100));
        Assert.Equal(1, h.Percentile(0));

        static void AssertNear(long expected, long actual)
            => Assert.InRange(actual, expected, (long)Math.Ceiling(expected * (1 + 1.0 / 32)));
    }

    [Fact]
    public void ATailShowsInTheHighPercentilesOnly()
    {
        var h = new LatencyHistogram();
        for (int i = 0; i < 9990; i++) h.Record(100);
        for (int i = 0; i < 10; i++) h.Record(25_000);
        Assert.InRange(h.Percentile(50), 100, 103);
        Assert.InRange(h.Percentile(99), 100, 103);
        Assert.InRange(h.Percentile(99.9), 100, 103);  // rank 9990 is still in the body
        Assert.InRange(h.Percentile(99.95), 25_000, 25_000);
        Assert.Equal(25_000, h.Max);
    }

    [Fact]
    public void EmptyClampedAndReset()
    {
        var h = new LatencyHistogram();
        Assert.Equal(0, h.Percentile(99));
        Assert.Equal(0, h.Mean);
        h.Record(-5);
        h.Record(long.MaxValue);
        Assert.Equal(0, h.Min);
        Assert.Equal(LatencyHistogram.MaxValue, h.Max);
        h.Reset();
        Assert.Equal(0, h.Count);
        Assert.Equal(0, h.Max);
        Assert.Equal("p50 0.00 p95 0.00 p99 0.00 p99.9 0.00 max 0.00", h.Summary());
    }

    [Fact]
    public void RecordingDoesNotAllocate()
    {
        var h = new LatencyHistogram();
        h.Record(1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++) h.Record(i * 7 % 1_000_000);
        _ = h.Percentile(99);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void AWindowLineAndTheSummary()
    {
        using var metrics = new ServerMetrics();
        for (int t = 1; t <= 40; t++)
            metrics.EndTick(new TickSample { Tick = t, StepMicros = 100 + t, TotalMicros = 200 + t, Sessions = 2, BytesOut = 1000, BytesIn = 10, Overrun = t == 7 });
        metrics.AddTransportBytes(100_000, 1000);
        string line = metrics.EndWindow(40);
        Assert.StartsWith("status tick=40 ticks=40 sessions=2 | total p50 ", line);
        Assert.Contains("overruns=1", line);
        Assert.Contains("B/s per session out 10000 in 100 (transport out 25000 in 250)", line); // 40 ticks = 2 s, 2 sessions
        Assert.Equal(0, metrics.WindowTotal.Count);
        Assert.Equal(40, metrics.RunTotal.Count);
        Assert.Equal(line, metrics.LastWindow);
        string summary = metrics.Summary(40_000, 400, 3, 0);
        Assert.Contains("summary ticks=40 overruns=1 sessions_served=3 runtimes=0", summary);
        Assert.Contains("summary total p50 ", summary);
    }

    [Fact]
    public void TheTicksSSmobsFiredInGetTheirOwnPeakFigure()
    {
        // M2 (scope 2l): SSmobs runs every living mob's Life in one tick every 40 (DV3: it never yields), the heaviest tick.
        using var metrics = new ServerMetrics();
        for (int t = 1; t <= 80; t++)
            metrics.EndTick(new TickSample { Tick = t, StepMicros = t % 40 == 0 ? 9_000 : 200, TotalMicros = 300, MobsFired = t % 40 == 0 });
        Assert.Equal(2, metrics.WindowMobsStep.Count);
        Assert.Equal(9_000, metrics.WindowMobsStep.Max);
        string line = metrics.EndWindow(80);
        Assert.Contains("mobs-tick p50 ", line);
        Assert.Contains("max 9.00", line);
        Assert.Equal(0, metrics.WindowMobsStep.Count);
        Assert.Equal(2, metrics.RunMobsStep.Count);
        string summary = metrics.Summary(0, 0, 0, 0);
        Assert.Contains("summary mobs-tick (world step of the 2 ticks SSmobs fired in) p50 ", summary);
        // No tick of it: a dash, not a row of zeros.
        using var quiet = new ServerMetrics();
        quiet.EndTick(new TickSample { Tick = 1, StepMicros = 100, TotalMicros = 100 });
        Assert.Contains("mobs-tick - |", quiet.EndWindow(1));
    }

    [Fact]
    public void TheCsvHasAMobsFiredColumnAtTheEnd()
    {
        var csv = new System.IO.StringWriter();
        using (var metrics = new ServerMetrics(csv))
        {
            metrics.EndTick(new TickSample { Tick = 1, StepMicros = 100, TotalMicros = 100, MobsFired = true });
            metrics.EndTick(new TickSample { Tick = 2, StepMicros = 100, TotalMicros = 100 });
        }
        string[] lines = csv.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith(",heap_bytes,mobs_fired", lines[0]);
        Assert.Equal(lines[0].Split(',').Length, lines[1].Split(',').Length);
        Assert.EndsWith(",1", lines[1]);
        Assert.EndsWith(",0", lines[2]);
    }
}
