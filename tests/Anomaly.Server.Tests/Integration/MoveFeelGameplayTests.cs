using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV263 through the real server and a predicting client (the owner, 2026-11-03: "no mispredictions from movement"): with the server's
/// default movement feel the player gathers speed and loses it, goes slower backward, and a run of starts, stops, turns of the view,
/// reversals and walls is predicted without a single correction, with and without latency.
/// </summary>
public class MoveFeelGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static (RealHeadlessWorld W, RealHeadlessWorld.Player P, mob_living Me) Prepare(int latency, int x, int y)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000, MinLatencySteps = latency, MaxLatencySteps = latency }, strict: false);
        var p = w.Join("Bob", inputBufferTarget: latency > 0 ? 2 : 1);
        var me = w.MobOf(p);
        using (w.World.Enter())
        {
            ADD_TRAIT(me, TRAIT_GODMODE, "test");
            Waiting.NoSleep(me.forceMove(locate(x, y, 1)));
        }
        p.Yaw = 0;
        w.Run(Second);
        return (w, p, me);
    }

    [Fact]
    public void TheServersDefaultFeelRampsTheSpeedAndSlowsTheBackward()
    {
        var (w, p, me) = Prepare(0, 6, 12);
        using (w)
        {
            Assert.Equal(MoveFeel.Default, w.Config.MoveFeel);
            p.Keys = MoveKeys.Forward; // the view faces north
            for (int i = 0; i < Second && w.ServerState(p).VelY == 0; i++)
                w.Run(1); // the frames reach the server after the input clock's lead
            MotionState first = w.ServerState(p);
            int full = (int)MathF.Floor(p.Session.Prediction.Params.TilesPerTick * MotionMath.VelocityPerTile + 0.5f);
            Assert.InRange(first.VelY, 1, full / 2); // not at once
            w.Run(MoveFeel.Default.AccelTicks + 1);
            Assert.Equal(full, w.ServerState(p).VelY);
            p.Keys = MoveKeys.Back;
            w.Run(MoveFeel.Default.DecelTicks + MoveFeel.Default.AccelTicks + 2);
            Assert.InRange(-w.ServerState(p).VelY, full * 0.49 - 2, full * 0.49 + 2);
            p.Keys = MoveKeys.None;
            w.Run(MoveFeel.Default.DecelTicks + 1);
            Assert.Equal((0, 0), (w.ServerState(p).VelX, w.ServerState(p).VelY));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void RunningAroundWithTheFeelIsPredictedWithoutACorrection(int latency)
    {
        var (w, p, me) = Prepare(latency, 6, 12);
        using (w)
        {
            var before = p.Counters;
            var rng = new Random(latency + 1);
            MoveKeys[] keys =
            {
                MoveKeys.None, MoveKeys.Forward, MoveKeys.Back, MoveKeys.Left, MoveKeys.Right, MoveKeys.Forward | MoveKeys.Left,
                MoveKeys.Forward | MoveKeys.Right, MoveKeys.Back | MoveKeys.Left, MoveKeys.Back | MoveKeys.Right,
            };
            for (int segment = 0; segment < 60; segment++)
            {
                p.Keys = keys[rng.Next(keys.Length)];
                if (rng.Next(3) == 0)
                    p.Yaw = rng.NextDouble() * 2 * Math.PI; // a flick of the view
                int ticks = rng.Next(1, 12);
                for (int t = 0; t < ticks; t++)
                {
                    p.Yaw += (rng.NextDouble() - 0.5) * 0.3; // the mouse drifting while running
                    w.Run(1);
                }
            }
            p.Keys = MoveKeys.None;
            w.Run(Second);
            var c = p.Counters;
            output.WriteLine($"reconciliations {c.Reconciliations - before.Reconciliations}, corrections {c.Corrections - before.Corrections}, mismatches {c.Mismatches - before.Mismatches}, snaps {c.Snaps - before.Snaps}, max {c.MaxErrorTiles:0.###}");
            Assert.True(c.Reconciliations - before.Reconciliations > 200);
            Assert.Equal(before.Corrections, c.Corrections);
            Assert.Equal(before.Mismatches, c.Mismatches);
            Assert.Equal(before.Snaps, c.Snaps);
            Assert.Equal(w.ServerState(p).X, p.Session.Prediction.Predicted.X);
            Assert.Equal(w.ServerState(p).Y, p.Session.Prediction.Predicted.Y);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RunningIntoAWallAndAlongItIsPredicted(int latency)
    {
        var (w, p, me) = Prepare(latency, 6, 12);
        using (w)
        {
            using (w.World.Enter())
                for (int y = 9; y <= 15; y++)
                    Waiting.NoSleep(locate(10, y, 1).ChangeTurf(typeof(turf_closed_wall)));
            w.Run(2);
            var before = p.Counters;
            int startX = w.ServerState(p).TileX;
            p.Yaw = -Math.PI / 2; // Godot's yaw: facing east
            p.Keys = MoveKeys.Forward;
            w.Run(Second * 2); // into the wall, pressing it
            Assert.Equal(9, w.ServerState(p).TileX);
            Assert.Equal(0, w.ServerState(p).VelX);
            p.Keys = MoveKeys.Forward | MoveKeys.Left; // along it, diagonally into it
            w.Run(Second);
            p.Keys = MoveKeys.Back; // away, braking
            w.Run(8);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            var c = p.Counters;
            output.WriteLine($"corrections {c.Corrections - before.Corrections}, mismatches {c.Mismatches - before.Mismatches}, snaps {c.Snaps - before.Snaps}");
            Assert.Equal(before.Corrections, c.Corrections);
            Assert.Equal(before.Mismatches, c.Mismatches);
        }
    }

    [Fact]
    public void TheDevCommandChangesTheFeelAndTheClientFollows()
    {
        Assert.True(DevCommandLine.TryParse("movefeel 0 0 0 0", out DevCommand off, out string error), error);
        Assert.Equal(new[] { 0f, 0f, 0f, 0f }, off.Ranges);
        Assert.False(DevCommandLine.TryParse("movefeel 41 0 0 0", out _, out _));
        Assert.False(DevCommandLine.TryParse("movefeel 4 3 0.95 0", out _, out _));
        Assert.False(DevCommandLine.TryParse("movefeel 4 3", out _, out _));

        var (w, p, me) = Prepare(0, 6, 12);
        using (w)
        {
            using (w.World.Enter())
                LocomotionSeam.SetFeel(w.World, default);
            w.Run(2);
            Assert.Equal(default, p.Session.Prediction.Params.Feel);
            var before = p.Counters;
            p.Keys = MoveKeys.Forward;
            w.Run(1);
            Assert.Equal(0, w.ServerState(p).VelY); // tg's: no velocity kept
            w.Run(Second);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Assert.Equal(before.Mismatches, p.Counters.Mismatches);
        }
    }

    [Fact]
    public void TheCommandLogKeepsTheFeelForAReplay()
    {
        var feel = new MoveFeel(6, 2, 0.25f, 0.05f);
        CommandLog log = CommandLog.For(new ServerConfig { MoveFeel = feel });
        string text = log.ToText();
        Assert.Contains("movefeel 6 2 0.25 0.05", text);
        CommandLog read = CommandLog.Parse(text, out string error);
        Assert.True(read != null, error);
        Assert.Equal(feel, read.MoveFeel);
        Assert.Equal(feel, read.ReplayConfig().MoveFeel);
        // a log from before the feel replays without one
        CommandLog old = CommandLog.Parse(text.Replace("movefeel 6 2 0.25 0.05\n", ""), out error);
        Assert.Equal(default, old.MoveFeel);
    }
}
