using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// P8's finding at the M2 integration: with the cap of 3 queued frames and a remote client's target of 2, a lost message
/// followed by the next one's redundant frames overflowed the server's jitter buffer (a dropped frame is a step the
/// prediction cannot know: a correction). The cap was 4, 8 since D331 (<see cref="Limits.MaxJitterBufferFrames"/>); the starve policy
/// repeats the last frame, standing in for the next seq since D331. Measured over the client's real stack (<see cref="HeadlessWorld"/>) at 100 ms one way and 2 % loss.
/// </summary>
public class InputBufferCapTests
{
    private readonly ITestOutputHelper _output;

    public InputBufferCapTests(ITestOutputHelper output) => _output = output;

    private static void WalkAbout(HeadlessWorld world, int seconds)
    {
        MoveKeys[] plan = { MoveKeys.Forward, MoveKeys.Forward | MoveKeys.Right, MoveKeys.Right, MoveKeys.Back | MoveKeys.Right, MoveKeys.Back, MoveKeys.Left, MoveKeys.Forward | MoveKeys.Left };
        double[] yaws = { 0, 0.3, 1.2, -0.8, 2.0, -1.5, 0.0 };
        for (int s = 0; s < seconds; s++)
        {
            world.Keys = plan[s % plan.Length];
            world.Yaw = yaws[s % yaws.Length];
            world.Run(20);
        }
    }

    private (long Corrections, long Starved, long Dropped) Run(int cap)
    {
        long corrections = 0, starved = 0, dropped = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            using var world = new HeadlessWorld(server: new FakeServerOptions { Bots = 0, ScriptedEvents = false, JitterCap = cap },
                network: new LoopbackOptions { Seed = seed, MinLatencySteps = 2, MaxLatencySteps = 2, LossRate = 0.02 }, inputBufferTarget: 2);
            WalkAbout(world, 60);
            corrections += world.Session.Prediction.Counters.Corrections;
            starved += world.Server.Starved;
            dropped += world.Server.Dropped;
        }
        _output.WriteLine($"cap {cap}: corrections {corrections}, starved {starved}, dropped {dropped} (6 walks of 60 s, target 2, 100 ms, 2 % loss)");
        return (corrections, starved, dropped);
    }

    [Fact]
    public void ARemoteClientsBufferNoLongerOverflowsAfterALoss()
    {
        var old = Run(3);
        var now = Run(Limits.MaxJitterBufferFrames);
        Assert.Equal(8, Limits.MaxJitterBufferFrames);
        Assert.True(old.Dropped > 0, "the old cap overflowed in these walks");
        Assert.Equal(0, now.Dropped);
        Assert.True(now.Corrections < old.Corrections, $"{now.Corrections} corrections at the new cap, {old.Corrections} at 3");
        Assert.True(now.Corrections <= now.Starved, "every correction left is a starved tick (two lost messages in a row)");
    }
}
