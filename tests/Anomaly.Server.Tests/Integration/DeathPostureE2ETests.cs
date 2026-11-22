using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test (2026-10-14): a player who died stood on their feet. Through the real server and a predicting client:
/// a player on a space turf (weightless) is knocked down and dies in the vacuum, and every client sees the body lying with its
/// angle and the turned picture (the appearance's matrix), not a standing one flagged as lying.
/// </summary>
public class DeathPostureE2ETests
{
    [Fact]
    public void APlayerWhoDiesInSpaceLiesTurnedOnTheFloorForTheClient()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 400, MinLatencySteps = 2, MaxLatencySteps = 2 });
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        mob_living m = w.MobOf(alice);
        w.Run(20);
        int x, y;
        using (w.World.Enter()) { x = (int)m.x; y = (int)m.y; }
        alice.Session.DevCommand($"breach {x} {y}"); // space under the player: weightless, in a vacuum
        w.Run(2);

        for (int i = 0; i < 20 * 600 && m.stat != DEAD; i++)
            w.Run(1);
        Assert.Equal(DEAD, m.stat);
        w.Run(40);

        Assert.Equal(LYING_DOWN, m.body_position);
        Assert.Equal(Posture.Lying, alice.Session.Prediction.Predicted.Posture);
        ReplicaEntity own = alice.Session.Replica.Entities.Single(e => e.Id == m.dm_id);
        Assert.True(PostureBits.IsLying(own.State.Posture));
        Assert.NotEqual(0, PostureBits.LyingAngle(own.State.Posture));
        Assert.True(alice.Session.Replica.TryGetAppearance(own.State.Appearance, out AppearanceEntry look));
        Assert.NotNull(look.Transform); // the picture is turned: without it the body is drawn on its feet
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
