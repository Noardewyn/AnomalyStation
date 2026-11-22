using Anomaly.Client.Shared;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// P8's finding at the M2 integration: a second client saw corrections against the body a disconnected player left behind.
/// On the current code the host keeps the body a registered, standing capsule (tg's SSD: the leave gives it an idle frame),
/// the replicator sends its capsule position to early and late joiners alike, and a predicting client that walks into it
/// from several angles is never corrected. (P8 ran it while a human moved a tile per tick, before T2's capsule step split:
/// a step as long as the touch distance of two bodies could carry a capsule through a standing one on one side only.)
/// </summary>
public class LeftBodyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void AClientWalkingIntoALeftBodyIsNeverCorrected(int latencySteps)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 400, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living body = w.MobOf(alice);
        w.Leave(alice);
        SpawnPoint bobSpawn = w.Host.Room.SpawnPoints[1];
        using (w.World.Enter())
        {
            Assert.True(LocomotionSeam.IsRegistered(body), "the left body stays a capsule (D105: solid)");
            Assert.Null(body.client);
            Waiting.NoSleep(body.forceMove(Builtins.locate(bobSpawn.X, bobSpawn.Y + 1, bobSpawn.Z))); // a tile north of Bob's spawn (open floor)
        }
        w.Run(4);

        RealHeadlessWorld.Player bob = w.Join("Bob", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        Assert.True(LocomotionSeam.TryGetState(w.World, body.dm_id, out MotionState server, out _));
        ReplicaEntity seen = bob.Session.Replica.Entities.Single(e => e.Id == body.dm_id);
        Assert.True(seen.HasPos, "a late joiner gets the body's capsule position");
        Assert.Equal((server.X, server.Y), (seen.State.PosX, seen.State.PosY));

        long start = bob.Counters.Corrections + bob.Counters.Snaps;
        foreach (double yaw in new[] { 0.0, 0.2, -0.3, 0.45, -0.1, 0.35 }) // yaw 0 walks north, into the body
        {
            bob.Keys = MoveKeys.Forward;
            bob.Yaw = yaw;
            w.Run(15);
            bob.Keys = MoveKeys.Back;
            w.Run(8);
        }
        bob.Keys = MoveKeys.None;
        w.Run(10);
        Assert.Equal(0, bob.Counters.Corrections + bob.Counters.Snaps - start);
        Assert.Equal(0, bob.Counters.Mismatches);
        MotionState bobNow = w.ServerState(bob);
        Assert.Equal((bobNow.X, bobNow.Y), (bob.Session.Prediction.Predicted.X, bob.Session.Prediction.Predicted.Y));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
