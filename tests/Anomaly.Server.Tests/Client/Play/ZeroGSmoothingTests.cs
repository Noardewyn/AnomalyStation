using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// The space play test (2026-10-15): "flying fast on the jetpack, or just after speeding up, the picture jerks strongly, especially
/// diagonally across tiles". The own body's glide in zero gravity (<see cref="OwnGlide"/>) measured per rendered frame without an engine,
/// over tg's step patterns as the server sends them: a stabilized CO2 jetpack flight east (a tile per 2.4 ticks: steps 3 and 2 ticks
/// apart), a diagonal flight (tg's diagonal move_delay times sqrt 2, one two-tile move a step), a fast drift at a shallow angle (smooth_move's
/// steps alternate east and north-east), a drift speeding up (tg's newtonian impulses shorten the loop's delay) and the flight with the
/// network's jitter. The drawn speed must change little from frame to frame (no stop-and-go at each tile), never jump, and keep up.
/// </summary>
public class ZeroGSmoothingTests
{
    private readonly ITestOutputHelper _output;

    public ZeroGSmoothingTests(ITestOutputHelper output) => _output = output;

    private const double TickSeconds = 1.0 / MotionMath.TicksPerSecond;

    /// <summary>One server step: the tick it happens in, the tile it ends on, tg's glide size then (pixels a tick).</summary>
    private readonly record struct Step(int Tick, int X, int Y, float Glide);

    private static MotionState At(int x, int y, MotionState from, ushort ticks)
    {
        MotionState s = from;
        MotionStep.Displace(ref s, x, y, 1, ticks, 2);
        return s;
    }

    /// <summary>The frames' drawn positions of a flight: the server's states arrive at each tick's end (plus a jitter), the frames come at <paramref name="fps"/>.</summary>
    private static List<(double T, float X, float Y, int SX, int SY)> Fly(IReadOnlyList<Step> steps, int fps, double jitterSeconds = 0, int seed = 1, int endTick = -1)
    {
        var glide = new OwnGlide();
        glide.Start(steps[0].X, steps[0].Y, 1);
        var rng = new Random(seed);
        int last = endTick > 0 ? endTick : steps[^1].Tick + 20;
        // The state of each tick: the newest step's tile (as a slide's target), arriving at the tick's end plus the jitter (in order).
        var arrivals = new List<(double At, int X, int Y, float Glide)>();
        int k = 0;
        (int X, int Y, float G) now = (steps[0].X, steps[0].Y, steps[0].Glide);
        double previous = 0;
        for (int tick = 1; tick <= last; tick++)
        {
            while (k < steps.Count && steps[k].Tick <= tick)
            {
                now = (steps[k].X, steps[k].Y, steps[k].Glide);
                k++;
            }
            double at = Math.Max(previous, tick * TickSeconds + rng.NextDouble() * jitterSeconds);
            previous = at;
            arrivals.Add((at, now.X, now.Y, now.G));
        }
        var drawn = new List<(double, float, float, int, int)>();
        (int X, int Y) server = (steps[0].X, steps[0].Y);
        double frame = 1.0 / fps;
        int a = 0;
        float glideSize = steps[0].Glide;
        MotionState state = MotionState.AtTile(steps[0].X, steps[0].Y, 1);
        state.Driver = MotionDriver.ZeroG;
        for (double t = frame; t <= (last + 1) * TickSeconds; t += frame)
        {
            while (a < arrivals.Count && arrivals[a].At <= t)
            {
                (_, int x, int y, float g) = arrivals[a++];
                server = (x, y);
                if (MotionMath.ToTiles(state.Slide.Active ? state.Slide.TargetX : state.X) != x || MotionMath.ToTiles(state.Slide.Active ? state.Slide.TargetY : state.Y) != y)
                {
                    state = At(x, y, state, 3);
                }
                glideSize = g;
                glide.Follow(state);
            }
            glide.Advance(frame, glideSize);
            drawn.Add((t, glide.X, glide.Y, server.X, server.Y));
        }
        return drawn;
    }

    /// <summary>The drawn speed per frame (tiles a second), its largest change between two frames while moving, the share of moving frames that stall.</summary>
    private static (double Mean, double MaxChange, double Stalls, double MaxSpeed, double Behind) Measure(List<(double T, float X, float Y, int SX, int SY)> drawn, double from, double to)
    {
        var speeds = new List<double>();
        double behind = 0;
        for (int i = 1; i < drawn.Count; i++)
        {
            if (drawn[i].T < from || drawn[i].T > to)
                continue;
            behind += Math.Sqrt(Math.Pow(drawn[i].SX - drawn[i].X, 2) + Math.Pow(drawn[i].SY - drawn[i].Y, 2));
            double dt = drawn[i].T - drawn[i - 1].T;
            double dx = drawn[i].X - drawn[i - 1].X, dy = drawn[i].Y - drawn[i - 1].Y;
            speeds.Add(Math.Sqrt(dx * dx + dy * dy) / dt);
        }
        double mean = speeds.Average();
        double change = 0;
        for (int i = 1; i < speeds.Count; i++)
            change = Math.Max(change, Math.Abs(speeds[i] - speeds[i - 1]));
        double stalls = speeds.Count(v => v < 0.2 * mean) / (double)speeds.Count;
        return (mean, change, stalls, speeds.Max(), behind / speeds.Count);
    }

    // tg's CO2 jetpack with the stabilizer: 1.2 ds a tile, client/Move's move_delay carry gives steps 3 and 2 ticks apart (2.4 on average).
    private static List<Step> Straight(int tiles, int startTick = 2)
    {
        var steps = new List<Step> { new(0, 10, 10, 32f / 2.4f) };
        double t = startTick;
        for (int i = 1; i <= tiles; i++)
        {
            steps.Add(new Step((int)Math.Ceiling(t - 1e-9), 10 + i, 10, 32f / 2.4f));
            t += 2.4;
        }
        return steps;
    }

    // NE held: tg's diagonal move_delay is the delay times sqrt 2 (mob_movement.dm:112), one move of two tiles a step.
    private static List<Step> Diagonal(int steps0)
    {
        double delay = 2.4 * Math.Sqrt(2);
        var steps = new List<Step> { new(0, 10, 10, (float)(32 / delay)) };
        double t = 2;
        for (int i = 1; i <= steps0; i++)
        {
            steps.Add(new Step((int)Math.Ceiling(t - 1e-9), 10 + i, 10 + i, (float)(32 / delay)));
            t += delay;
        }
        return steps;
    }

    // A fast drift at atan(1/2) (about 27 degrees): smooth_move steps east and north-east by turns, a step per 1.5 ticks.
    private static List<Step> Zigzag(int count)
    {
        var steps = new List<Step> { new(0, 10, 10, 32f / 1.5f) };
        double t = 2;
        int x = 10, y = 10;
        for (int i = 1; i <= count; i++)
        {
            x++;
            if (i % 2 == 0)
                y++;
            steps.Add(new Step((int)Math.Ceiling(t - 1e-9), x, y, 32f / 1.5f));
            t += 1.5;
        }
        return steps;
    }

    // A drift speeding up: the loop's delay from 5 ticks a tile down to 1.5 over the first tiles (tg's impulses), then steady.
    private static List<Step> SpeedingUp(int count)
    {
        var steps = new List<Step> { new(0, 10, 10, 32f / 5f) };
        double t = 2, delay = 5;
        for (int i = 1; i <= count; i++)
        {
            steps.Add(new Step((int)Math.Ceiling(t - 1e-9), 10 + i, 10, (float)(32 / delay)));
            t += delay;
            delay = Math.Max(1.5, delay * 0.8);
        }
        return steps;
    }

    // Steady flight: the largest change of the drawn speed between two frames as a share of its mean, no stalled frame, at most a quarter
    // above tg's fastest rate of the flight (no rush to catch up), and the body ends on the server's last tile.
    private void Check(string name, List<Step> steps, int fps, double from, double to, double maxShare, double top, double jitter = 0)
    {
        List<(double T, float X, float Y, int SX, int SY)> drawn = Fly(steps, fps, jitter);
        (double mean, double change, double stalls, double max, double behind) = Measure(drawn, from, to);
        _output.WriteLine($"{fps} fps {name}: mean {mean:0.00} tiles/s, max {max:0.00}, largest change a frame {change:0.00} ({change / mean:P0} of the mean), "
            + $"stalled frames {stalls:P1}, drawn {behind:0.00} tiles behind the server's tile ({behind / mean * 1000:0} ms)");
        Assert.True(change <= maxShare * mean, $"{name} at {fps} fps: the speed changes by {change:0.00} tiles/s in a frame (mean {mean:0.00})");
        Assert.Equal(0.0, stalls);
        Assert.True(max <= 1.25 * top, $"{name} at {fps} fps: {max:0.00} tiles/s at most, tg's rate {top:0.00}");
        Assert.Equal(steps[^1].X, drawn[^1].X, 3);
        Assert.Equal(steps[^1].Y, drawn[^1].Y, 3);
    }

    [Fact]
    public void A_fast_flight_straight_diagonal_zigzag_or_speeding_up_is_drawn_without_jerks()
    {
        // Measured before (M4c slice 7's glide, the same flights): the speed fell to nothing and back in one frame at 120 fps (101 % of the
        // mean) straight and diagonally, 33 % on the zigzag, 71 % to 110 % speeding up; now a few per cent, bounded by OwnGlide.Accel.
        foreach (int fps in new[] { 60, 120 })
        {
            double share = fps == 60 ? 0.13 : 0.07;
            double jetpack = 20 / 2.4, drift = 20 / 1.5;
            Check("straight", Straight(20), fps, 0.3, 2.2, share, jetpack);
            Check("diagonal", Diagonal(12), fps, 0.4, 2.0, share, jetpack);
            Check("zigzag", Zigzag(30), fps, 0.6, 2.2, fps == 60 ? 0.2 : 0.1, drift * (1 + Math.Sqrt(2)) / 2);
            Check("speeding up", SpeedingUp(25), fps, 0.3, 2.4, share, drift);
            Check("straight, jitter 20 ms", Straight(20), fps, 0.3, 2.2, share, jetpack, 0.02);
        }
    }
}
