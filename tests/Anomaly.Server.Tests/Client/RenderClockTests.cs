using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the render time (about 2 ticks behind the latest, smoothed against jitter).</summary>
public class RenderClockTests
{
    [Fact]
    public void NothingHappensBeforeTheFirstTick()
    {
        var clock = new RenderClock();
        Assert.False(clock.Started);
        clock.Update(1.0);
        Assert.False(clock.Started);
    }

    [Fact]
    public void TheFirstTickStartsTwoTicksBehind()
    {
        var clock = new RenderClock();
        clock.OnServerTick(100, 5.0);
        Assert.True(clock.Started);
        Assert.Equal(98.0, clock.RenderTick, 6);
    }

    [Fact]
    public void ASteadyStreamSettlesTwoTicksBehindAndRunsAtTheTickRate()
    {
        var clock = new RenderClock();
        double now = 0;
        long arrived = -1;
        double previousRender = double.NaN;
        // Ticks arrive every 50 ms with a constant 30 ms delay; frames at 144 Hz.
        for (int frame = 0; frame < 144 * 10; frame++)
        {
            now = frame / 144.0;
            while ((arrived + 1) * 0.05 + 0.03 <= now)
            {
                arrived++;
                clock.OnServerTick(arrived, (arrived) * 0.05 + 0.03);
            }
            clock.Update(now);
            if (clock.Started && !double.IsNaN(previousRender))
            {
                double speed = (clock.RenderTick - previousRender) * 144.0;
                Assert.InRange(speed, 18.0, 22.0);
            }
            if (clock.Started)
                previousRender = clock.RenderTick;
        }
        Assert.InRange(arrived - clock.RenderTick, 0.9, 2.1); // two ticks behind the estimate, which is up to a tick ahead of the last arrival
        Assert.InRange(clock.LagTicks, 1.9, 2.1);
    }

    [Fact]
    public void JitterNeverMakesTimeRunBackwardsOrPastTheNewestTick()
    {
        var clock = new RenderClock();
        var random = new Random(7);
        var arrivals = new List<(double At, long Tick)>();
        double lastAt = 0;
        for (long tick = 0; tick < 400; tick++)
        {
            // 25 ms delay plus up to 40 ms of jitter, in order (a reliable channel).
            double at = Math.Max(lastAt, tick * 0.05 + 0.025 + random.NextDouble() * 0.04);
            arrivals.Add((at, tick));
            lastAt = at;
        }
        int next = 0;
        long newest = -1;
        double previous = double.NaN;
        for (double now = 0; now < 20; now += 1 / 90.0)
        {
            while (next < arrivals.Count && arrivals[next].At <= now)
            {
                newest = arrivals[next].Tick;
                clock.OnServerTick(newest, arrivals[next].At);
                next++;
            }
            clock.Update(now);
            if (!clock.Started)
                continue;
            if (!double.IsNaN(previous))
                Assert.True(clock.RenderTick > previous, $"time did not advance at {now}");
            previous = clock.RenderTick;
            if (now > 2)
                Assert.True(clock.RenderTick <= newest, $"drawing past the newest tick at {now}: {clock.RenderTick} > {newest}");
        }
    }

    [Fact]
    public void AStallPausesTheStreamButNotTheClock()
    {
        var clock = new RenderClock();
        for (long tick = 0; tick < 40; tick++)
            clock.OnServerTick(tick, tick * 0.05);
        double now = 40 * 0.05;
        clock.Update(now);
        double before = clock.RenderTick;
        // One second with no tick.
        for (int i = 0; i < 60; i++)
        {
            now += 1.0 / 60;
            clock.Update(now);
        }
        Assert.True(clock.RenderTick > before + 15);
        // The stream resumes where the server is now: the clock converges without running backwards.
        double previous = clock.RenderTick;
        for (long tick = 60; tick < 160; tick++)
        {
            double at = 40 * 0.05 + 1.0 + (tick - 60) * 0.05;
            while (now < at)
            {
                now += 1.0 / 60;
                clock.Update(now);
                Assert.True(clock.RenderTick > previous);
                previous = clock.RenderTick;
            }
            clock.OnServerTick(tick, at);
        }
    }

    [Fact]
    public void AServerThatCatchesUpIsFollowedAtMostTenPercentFaster()
    {
        var clock = new RenderClock();
        for (long tick = 0; tick < 100; tick++)
            clock.OnServerTick(tick, tick * 0.05);
        double now = 100 * 0.05;
        clock.Update(now);
        // The server's time jumps 5 ticks ahead (a burst).
        clock.OnServerTick(105, now);
        double previous = clock.RenderTick;
        for (int i = 0; i < 30; i++)
        {
            now += 1.0 / 60;
            clock.Update(now);
            double speed = (clock.RenderTick - previous) * 60;
            Assert.InRange(speed, 18.0, 22.0 + 1e-6);
            previous = clock.RenderTick;
        }
    }

    [Fact]
    public void AHugeErrorSnaps()
    {
        var clock = new RenderClock();
        clock.OnServerTick(10, 0.0);
        clock.Update(0.016);
        clock.OnServerTick(400, 0.05);
        clock.Update(0.066);
        Assert.InRange(clock.RenderTick, 397.0, 399.0);
    }

    [Fact]
    public void ResetForgetsTheStream()
    {
        var clock = new RenderClock();
        clock.OnServerTick(500, 0.0);
        clock.Reset();
        Assert.False(clock.Started);
        clock.OnServerTick(3, 100.0);
        Assert.Equal(1.0, clock.RenderTick, 6);
    }
}
