using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M2 P8: the real feed's version-2 members against the scripted server over the loopback.</summary>
public class GameClientV2Tests
{
    [Fact]
    public void ThePrivateBlockAndTheTablesAreInTheReplicaAfterTheJoin()
    {
        using var world = new HeadlessWorld();
        ReplicaWorld replica = world.Client.Replica;
        Assert.Same(replica, world.Client.Replica);
        Assert.True(replica.Own.HasMotion);
        Assert.Equal(FakeServer.OwnId, world.Client.OwnEntity);
        Assert.Equal(3, replica.Screen.Count);
        Assert.NotEmpty(replica.Appearances);
        Assert.True(replica.IsSolid(1, 1, 1));
        Assert.False(replica.IsSolid(5, 5, 1));
    }

    [Fact]
    public void MotionFramesGoOnTheInputChannelAndTheirSeqIsTheLastSent()
    {
        using var world = new HeadlessWorld();
        var message = new MotionInputMessage();
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(1, 10, -10, 100, Anomaly.Core.Motion.MotionInputFlags.None));
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(2, 20, -20, 200, Anomaly.Core.Motion.MotionInputFlags.None));
        world.Client.SendMotion(message);
        Assert.Equal(2u, world.Client.LastSentSeq);
        long before = world.Client.BytesSent;
        Assert.True(before > 0);
        world.Run(3, session: false);
        // The server took the frames in seq order, one per tick, through its jitter buffer; the third tick's repeat stood in for seq 3 (D331).
        Assert.Equal(3u, world.Server.AppliedSeq);
        // An empty message sends nothing.
        world.Client.SendMotion(new MotionInputMessage());
        Assert.Equal(before, world.Client.BytesSent);
    }

    [Fact]
    public void ActionsGoOnTheControlChannelAndAreAppliedAfterTheirFrame()
    {
        using var world = new HeadlessWorld();
        world.Run(5, session: false);
        // Tagged with a frame that has not been applied yet: it waits for it.
        uint future = world.Server.AppliedSeq + 5;
        world.Client.SendAction(new ActionMessage(future, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null));
        world.Run(2, session: false);
        Assert.False(world.Server.PlayerLying);
        world.Client.SendAction(new ActionMessage(0, ActionKind.ToggleCombatMode, 0, ClickButton.None, ClickModifiers.None, null));
        var texts = new List<string>();
        world.Client.ServerTextReceived += t => texts.Add(t.Html);
        world.Run(4, session: false);
        Assert.Contains(texts, t => t.Contains("combat mode"));
    }

    [Fact]
    public void NothingIsSentBeforeTheGameIsPlaying()
    {
        var net = new LoopbackNetwork();
        var client = new GameClient(net.CreateClient(), "127.0.0.1", 1, "Tester");
        var message = new MotionInputMessage();
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(1, 0, 0, 0, Anomaly.Core.Motion.MotionInputFlags.None));
        client.SendMotion(message);
        client.SendAction(new ActionMessage(0, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null));
        Assert.Equal(0u, client.LastSentSeq);
        Assert.Equal(0, client.BytesSent);
        client.Dispose();
    }
}
