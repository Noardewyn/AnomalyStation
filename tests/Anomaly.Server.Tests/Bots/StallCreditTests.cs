using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// M3 I1b (the owner's second walk: a server stall kicked the client for flooding): game time stops while a tick overruns,
/// but a client keeps sending in real time. After the stall the host handles that backlog at once; the stall credit
/// (TokenBucket.CreditStall) lets it through, while a real flood (D40) is still closed.
/// </summary>
public class StallCreditTests
{
    private const int StallTicks = 80; // 4 s at 20 ticks per second

    // The clients run StallTicks network steps while the server does not tick, then the server's next tick starts late.
    private static void Stall(BotServerRig rig, double latenessMs)
    {
        for (int i = 0; i < StallTicks; i++)
        {
            rig.Network.Advance();
            foreach (Bot bot in rig.Bots) bot.Update();
        }
        rig.Host.RunTick(latenessMs, overrun: latenessMs > 0);
        rig.Network.Advance();
        foreach (Bot bot in rig.Bots) bot.Update();
    }

    private static BotServerRig Rig() => new(strict: false, network: new LoopbackOptions { Seed = 3, TimeoutSteps = 4 * StallTicks });

    [Fact]
    public void AFourSecondStallKeepsAnHonestClient()
    {
        using BotServerRig rig = Rig();
        Bot honest = rig.AddBot("Honest", BotBehaviour.Wander);
        rig.UntilJoined(honest);
        rig.Tick(20);
        Stall(rig, StallTicks * 50.0);
        rig.Tick(40);
        Assert.Equal(BotState.Joined, honest.State);
        Session s = rig.SessionOf("Honest");
        Assert.Equal(0, s.RateLimited);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(StallTicks, rig.Host.StallCreditedTicks);
    }

    [Fact]
    public void WithoutTheCreditTheSameBacklogOverflowsTheBucketButDoesNotCloseIt()
    {
        // The control: the same backlog handled on a tick that was not late (no credit) overflows the bucket: frames are dropped, far short of the flood limit.
        using BotServerRig rig = Rig();
        Bot honest = rig.AddBot("Honest", BotBehaviour.Wander);
        rig.UntilJoined(honest);
        rig.Tick(20);
        Stall(rig, 0);
        rig.Tick(5);
        Assert.Equal(BotState.Joined, honest.State);
        Assert.True(rig.SessionOf("Honest").RateLimited > 0);
    }

    [Fact]
    public void ARealFloodDuringAStallIsStillClosed()
    {
        using BotServerRig rig = Rig();
        Bot honest = rig.AddBot("Honest", BotBehaviour.Wander);
        rig.UntilJoined(honest);
        Bot flood = rig.AddBot("Flood", BotBehaviour.Flood);
        rig.UntilJoined(flood, maxTicks: 30);
        Stall(rig, StallTicks * 50.0);
        for (int i = 0; i < 100 && flood.State != BotState.Disconnected; i++) rig.Tick();
        Assert.Equal(BotState.Disconnected, flood.State);
        Assert.Equal(DisconnectReason.Flooding, flood.LastDisconnect.Reason);
        Assert.Equal(BotState.Joined, honest.State);
        Assert.Equal(0, rig.SessionOf("Honest").RateLimited);
    }
}
