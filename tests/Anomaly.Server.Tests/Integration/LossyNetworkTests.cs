using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10 slice B's measurement (D38: reconciliation at simulated 100 ms and 2% loss). One player walks the test room while another stands over the loopback with
/// 100 ms each way, 2% loss and a retransmission after one round trip (LiteNetLib's resend waits about one); the walker's own corrections
/// and, in the standing player's replica, the stalls of the Delta's tick (the head-of-line wait of the reliable channel after a lost
/// packet) and of the Motion's (version 42: the positions on the unreliable channel) are reported. Own walking stays exact: the predictor reconciles by input sequence, so a late delta delays the
/// check but does not move the capsule.
/// </summary>
public class LossyNetworkTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.02)]
    public void Walking_at_100_ms_and_2_percent_loss_corrects_nothing(double loss)
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 400, MinLatencySteps = 2, MaxLatencySteps = 2, LossRate = loss, RetransmitSteps = 4 });
        RealHeadlessWorld.Player walker = world.Join("Walker", inputBufferTarget: 2);
        RealHeadlessWorld.Player other = world.Join("Other", inputBufferTarget: 2);
        world.Run(40);
        MoveKeys[] plan = { MoveKeys.Forward, MoveKeys.Forward | MoveKeys.Right, MoveKeys.Right, MoveKeys.Back | MoveKeys.Right, MoveKeys.Back, MoveKeys.Left, MoveKeys.Forward | MoveKeys.Left };
        // What the standing player sees of the walker: the Delta's tick (the reliable channel) and the Motion's (the unreliable one).
        var deltas = new Gaps();
        var motion = new Gaps();
        for (int s = 0; s < 60; s++)
        {
            walker.Keys = plan[s % plan.Length];
            other.Keys = MoveKeys.None; // standing: a walker bumping into a player is corrected by design (D51), not by the network
            world.Run(20, _ =>
            {
                walker.Yaw += 0.05; // turning all the time, so a Motion is due every tick even against a wall
                if (s == 0) return; // the first second: the first Motion is still on its way
                deltas.Step(other.Session.Replica.Tick);
                motion.Step(other.Session.Replica.MotionTick);
            });
        }
        PredictionCounters c = walker.Counters;
        output.WriteLine($"loss {loss:P0}: reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, max error {c.MaxErrorTiles * 200:0.0} cm; "
            + $"Delta stalls over 2 steps {deltas.Over2}, longest {deltas.Longest * 50} ms; Motion stalls over 2 steps {motion.Over2}, longest {motion.Longest * 50} ms");
        Assert.True(c.Reconciliations > 600, "reconciliations " + c.Reconciliations);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Snaps);
        Assert.Equal(0, world.World.RuntimeErrorCount);
        // The walker's movement reaches the other player without the reliable channel's waits: a lost Motion is one tick, covered by the
        // client's two ticks of render delay.
        Assert.True(motion.Longest <= 2, "the longest Motion stall: " + motion.Longest + " steps");
        Assert.Equal(0, motion.Over2);
    }

    // Steps in which a tick did not advance: the longest run and the runs over 2 steps.
    private sealed class Gaps
    {
        private long _last = long.MinValue;
        private int _run;
        public int Longest;
        public int Over2;

        public void Step(long tick)
        {
            if (tick == _last)
            {
                _run++;
                return;
            }
            if (_run > 2) Over2++;
            Longest = Math.Max(Longest, _run);
            _run = 0;
            _last = tick;
        }
    }
}
