using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the sample interpolation of other capsule mobs (scope 2d, 1.6), the visible-error limit and the speed statistics.</summary>
public class InterpolationTests
{
    private const int Tile = 256;

    // Newest first, like ReplicaEntity.Sample(age): a mob walking east at a third of a tile per tick from tile 10.
    private static PoseSample[] Walk(long fromTick, long toTick, int startTile = 10, int speed = Tile / 3)
    {
        var list = new List<PoseSample>();
        for (long t = toTick; t >= fromTick; t--)
            list.Add(new PoseSample(t, startTile * Tile + (int)(t - fromTick) * speed, 5 * Tile, 1, 0, 0));
        return list.ToArray();
    }

    private static SampledPose Eval(PoseSample[] samples, double render, long latest)
    {
        Assert.True(SampleInterpolator.TryEvaluate(samples, render, latest, null, out SampledPose pose));
        return pose;
    }

    [Fact]
    public void BetweenConsecutiveSamplesTheEntityMovesInAStraightLine()
    {
        PoseSample[] s = Walk(100, 110);
        SampledPose at = Eval(s, 105.5, 110);
        // The sample of tick 105 is at 10 + 5/3 tiles, the one of 106 at 10 + 6/3: halfway between.
        Assert.Equal(10 + 5.5f / 3f, at.X, 2);
        Assert.Equal(5f, at.Y, 3);
        Assert.True(at.Moving);
    }

    [Fact]
    public void ASampleIsHitExactlyAtItsOwnTick()
    {
        PoseSample[] s = Walk(100, 110);
        Assert.Equal(10 + 4 * (Tile / 3) / 256f, Eval(s, 104.0, 110).X, 3);
    }

    [Fact]
    public void TheNewestSampleIsHeldWhenTheDataIsNotLate()
    {
        PoseSample[] s = Walk(100, 110);
        // Render time 112 with the latest tick 112: the entity has no sample for 111 and 112, so it stood still.
        SampledPose held = Eval(s, 111.5, 112);
        Assert.Equal(10 + 10 * (Tile / 3) / 256f, held.X, 3);
        Assert.False(held.Moving);
    }

    [Fact]
    public void ALateDeltaIsBridgedByExtrapolatingAtMostOneTick()
    {
        PoseSample[] s = Walk(100, 110);
        float newest = 10 + 10 * (Tile / 3) / 256f;
        float perTick = (Tile / 3) / 256f;
        SampledPose half = Eval(s, 110.5, 110);
        Assert.Equal(newest + 0.5f * perTick, half.X, 3);
        Assert.True(half.Moving);
        SampledPose one = Eval(s, 111.0, 110);
        Assert.Equal(newest + perTick, one.X, 3);
        SampledPose far = Eval(s, 115.0, 110);
        Assert.Equal(newest + perTick, far.X, 3); // never more than one tick
    }

    [Fact]
    public void AnEntityThatStoppedIsNotExtrapolatedPastItsLastSample()
    {
        // It moved up to tick 108 and then stood still while ticks 109 and 110 arrived without a sample for it.
        PoseSample[] s = Walk(100, 108);
        SampledPose p = Eval(s, 110.5, 110);
        Assert.Equal(10 + 8 * (Tile / 3) / 256f, p.X, 3);
    }

    [Fact]
    public void AfterAGapTheEntityStandsStillAndThenMovesOverTheLastTick()
    {
        // Stopped at tick 100, started again: the next sample is of tick 110, one step away.
        var s = new[]
        {
            new PoseSample(110, 11 * Tile, 5 * Tile, 1, 0, 0),
            new PoseSample(100, 10 * Tile, 5 * Tile, 1, 0, 0),
        };
        Assert.Equal(10f, Eval(s, 105.0, 120).X, 3);
        Assert.Equal(10f, Eval(s, 108.99, 120).X, 2);
        Assert.Equal(10.5f, Eval(s, 109.5, 120).X, 3);
        Assert.Equal(11f, Eval(s, 110.0, 120).X, 3);
    }

    [Fact]
    public void ATeleportOrALevelChangeJumpsAtTheSampleInsteadOfSliding()
    {
        var teleport = new[]
        {
            new PoseSample(10, 30 * Tile, 5 * Tile, 1, 0, 0),
            new PoseSample(9, 10 * Tile, 5 * Tile, 1, 0, 0),
        };
        Assert.Equal(10f, Eval(teleport, 9.5, 10).X, 3);
        Assert.Equal(30f, Eval(teleport, 10.0, 10).X, 3);
        var level = new[]
        {
            new PoseSample(10, 10 * Tile, 5 * Tile, 2, 0, 0),
            new PoseSample(9, 10 * Tile, 5 * Tile, 1, 0, 0),
        };
        Assert.Equal(1, Eval(level, 9.9, 10).Z);
        Assert.Equal(2, Eval(level, 10.0, 10).Z);
    }

    [Fact]
    public void BeforeTheOldestSampleTheEntityIsAtTheOldest()
    {
        PoseSample[] s = Walk(100, 110);
        Assert.Equal(10f, Eval(s, 50, 110).X, 3);
        Assert.False(SampleInterpolator.TryEvaluate(ReadOnlySpan<PoseSample>.Empty, 1, 1, null, out _));
    }

    [Fact]
    public void PostureChangesAtItsSampleAndYawTakesTheShortWay()
    {
        var s = new[]
        {
            new PoseSample(11, 10 * Tile, 5 * Tile, 1, 4, 1),
            new PoseSample(10, 10 * Tile, 5 * Tile, 1, 252, 0),
        };
        Assert.Equal((byte)0, Eval(s, 10.5, 11).Posture);
        Assert.Equal((byte)1, Eval(s, 11.0, 11).Posture);
        // 252 to 4 is 8 steps through 0, not 248 the other way.
        float yaw = Eval(s, 10.5, 11).Yaw;
        Assert.True(yaw < 0.01f || yaw > 255.99f, "yaw " + yaw);
        Assert.Equal(0f, SampleInterpolator.LerpYaw(252, 4, 0.5f), 3);
        Assert.Equal(2f, SampleInterpolator.LerpYaw(4, 252, 0.25f) , 3);
    }

    [Fact]
    public void SteadyMotionSampledAtAnyFrameRateNeverExceedsTheTrueSpeed()
    {
        PoseSample[] all = Walk(0, 400);
        foreach (double fps in new[] { 15.0, 30.0, 144.0, 240.0 })
        {
            double previous = double.NaN;
            double worst = 0;
            for (double now = 100; now < 300; now += 1.0 / fps * 20) // render time advances 20 ticks per second
            {
                int newest = (int)Math.Floor(now) + 2; // two ticks of delay
                var visible = new List<PoseSample>();
                for (int t = newest; t >= 0 && visible.Count < 16; t--)
                    visible.Add(all[400 - t]);
                SampledPose p = Eval(visible.ToArray(), now, newest);
                if (!double.IsNaN(previous))
                    worst = Math.Max(worst, (p.X - previous) / (1.0 / fps) * 2.0);
                previous = p.X;
            }
            Assert.InRange(worst, 13.0, 13.7); // 13.3 m/s at 2 m tiles
        }
    }

    [Fact]
    public void TheSmootherBoundsACatchUpAndSnapsAtATeleport()
    {
        var smoother = new PoseSmoother();
        smoother.Snap(10, 5, 1);
        // The target keeps pace at 6.67 tiles/s for a second, then jumps a third of a tile in one frame.
        double dt = 1.0 / 120;
        float x = 10;
        for (int i = 0; i < 120; i++)
        {
            x += (float)(6.67 * dt);
            smoother.Follow(x, 5, 1, dt);
        }
        float before = smoother.X;
        x += 1f / 3f;
        (float dx, _) = smoother.Follow(x, 5, 1, dt);
        float step = dx - before;
        Assert.True(step < 0.5f * (1f / 3f) + 6.67f * (float)dt, "the jump is spread over several frames: " + step);
        // It catches up within a few frames.
        for (int i = 0; i < 40; i++)
            smoother.Follow(x, 5, 1, dt);
        Assert.Equal(x, smoother.X, 3);
        // A teleport snaps.
        smoother.Follow(40, 5, 1, dt);
        Assert.Equal(40f, smoother.X, 3);
        // A level change snaps too.
        smoother.Follow(41, 5, 2, dt);
        Assert.Equal(41f, smoother.X, 3);
    }

    [Fact]
    public void TheSpeedStatisticsSeparateAHitchFromAHabit()
    {
        var stats = new FrameSpeedStats();
        double dt = 1.0 / 120;
        float x = 10;
        for (int i = 0; i < 20000; i++)
        {
            x += (float)(6.67 * dt);
            stats.Observe(7, x, 5, 1, i * dt);
            if (i == 10000)
                x += 0.3f; // one hitch
        }
        Assert.True(stats.Max > 30, "the maximum sees the hitch: " + stats.Max);
        Assert.InRange(stats.P99, 13.0, 14.0);
        Assert.Equal(19999, stats.Count);
    }

    [Fact]
    public void TheSpeedStatisticsIgnoreTeleportsAndOtherLevelsAndForgottenEntities()
    {
        var stats = new FrameSpeedStats();
        stats.Observe(1, 10, 10, 1, 0.0);
        stats.Observe(1, 40, 10, 1, 0.01);   // teleport
        stats.Observe(1, 40, 10, 2, 0.02);   // another level
        Assert.Equal(0, stats.Count);
        stats.Forget(1);
        stats.Observe(1, 10, 10, 1, 1.0);
        Assert.Equal(0, stats.Count);
        Assert.Equal(0.0, stats.P99);
    }

    [Fact]
    public void TheRenderClockSlowsDownAheadOfTheDataAndNeverPassesIt()
    {
        var clock = new RenderClock();
        double now = 0;
        long newest = 100;
        // A steady stream for two seconds, then the data stops (a 300 ms stall).
        for (int frame = 0; frame < 120; frame++)
        {
            now = frame / 60.0;
            long arrived = (long)Math.Floor(now * 20) + 100;
            while (newest < arrived)
            {
                newest++;
                clock.OnServerTick(newest, newest / 20.0 - 5);
            }
            clock.Update(now, newest);
        }
        double settled = clock.RenderTick;
        Assert.InRange(newest - settled, 1.0, 3.0);
        double maxRender = settled;
        for (int frame = 0; frame < 30; frame++)
        {
            now += 1 / 60.0;
            clock.Update(now, newest); // no new ticks for half a second
            maxRender = Math.Max(maxRender, clock.RenderTick);
        }
        Assert.True(clock.RenderTick <= newest + 1.0 + 1e-9, "render " + clock.RenderTick + " newest " + newest);
        Assert.True(clock.RenderTick >= settled);
    }

    [Fact]
    public void WithoutADataTickTheClockBehavesAsInM1()
    {
        var withLimit = new RenderClock();
        var plain = new RenderClock();
        for (int i = 0; i < 100; i++)
        {
            double t = i * 0.05;
            withLimit.OnServerTick(i, t);
            plain.OnServerTick(i, t);
            withLimit.Update(t);
            plain.Update(t, double.NaN);
        }
        Assert.Equal(plain.RenderTick, withLimit.RenderTick, 9);
    }
}
