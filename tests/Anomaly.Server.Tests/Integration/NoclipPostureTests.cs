using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test (2026-11-02): a noclip player flying over tables was seen by the others standing on each one. The noclip
/// mob's posture byte says incorporeal, so no client stands it on a table (EntityLayer's StandOn).
/// </summary>
public class NoclipPostureTests
{
    [Fact]
    public void ANoclipPlayersPostureSaysIncorporealToTheOthers()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 400, MinLatencySteps = 2, MaxLatencySteps = 2 });
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        RealHeadlessWorld.Player bob = w.Join("Bob", inputBufferTarget: 2);
        mob_living m = w.MobOf(alice);
        w.Run(20);
        byte Seen() => bob.Session.Replica.Entities.Single(e => e.Id == m.dm_id).State.Posture;
        Assert.False(PostureBits.IsIncorporeal(Seen()));

        alice.Session.DevCommand("noclip me");
        w.Run(10);
        Assert.True(PostureBits.IsIncorporeal(Seen()));

        alice.Session.DevCommand("noclip me");
        w.Run(10);
        Assert.False(PostureBits.IsIncorporeal(Seen()));
    }
}
