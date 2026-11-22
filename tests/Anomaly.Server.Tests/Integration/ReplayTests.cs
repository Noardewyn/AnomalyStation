using System.IO;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Determinism of the whole server (D31, scope 2j): a session of bots over the loopback (wandering, churning with their
/// tokens, kicked, spawning through the console), recorded as the host applies its commands, replayed into a fresh world
/// without network, ends with the same state hash; and the same live run twice gives the same hash.
/// </summary>
public class ReplayTests
{
    private static (ulong Hash, long End, string Log) Live(ulong networkSeed)
    {
        var record = new StringWriter();
        // Some latency, loss and reorder: the network may change when inputs arrive, never what the world does with them.
        var network = new LoopbackOptions { Seed = networkSeed, TimeoutSteps = 40, MinLatencySteps = 0, MaxLatencySteps = 2, LossRate = 0.05, ReorderRate = 0.05 };
        using var rig = new IntegrationRig(network: network, record: record, tokens: TestTokens());
        for (int i = 1; i <= 4; i++) rig.AddBot("Walker" + i);
        rig.AddBot("Churner", BotBehaviour.Churn, options: o => new BotOptions
        {
            Name = o.Name, Seed = o.Seed, Behaviour = o.Behaviour, ChurnMinUpdates = 40, ChurnMaxUpdates = 60, ChurnReconnectMaxUpdates = 10,
        });
        rig.Tick(60);
        rig.Host.EnqueueConsoleLine("spawn /obj");
        rig.Tick(60);
        rig.Host.EnqueueConsoleLine("kick Walker2 testing");
        rig.Tick(80);
        Assert.True(rig.Bots.Single(b => b.Options.Name == "Churner").Stats.Joins >= 2, "the churner came back with its token");
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
        rig.Stop();
        return (rig.Host.FinalHash, rig.Host.Tick, record.ToString());
    }

    private static Func<Anomaly.Protocol.SessionToken> TestTokens()
    {
        ulong next = 0;
        return () => new Anomaly.Protocol.SessionToken(++next, 0x5A5A);
    }

    [Fact]
    public void ARecordedBotSessionReplayedIntoAFreshWorldEndsWithTheSameHash()
    {
        var (live, end, text) = Live(networkSeed: 21);
        CommandLog log = CommandLog.Parse(text, out string error);
        Assert.Null(error);
        Assert.Equal(live, log.FinalHash);
        Assert.Contains(log.Commands, c => c.Kind == CommandKind.Motion);
        Assert.True(log.Commands.Count(c => c.Kind == CommandKind.Join) >= 6, "joins and rejoins recorded");

        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths); // the live rig's table (CommandLog.SoundsDigest)
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(live, result.FinalHash);
        Assert.Equal(0, replay.World.RuntimeErrorCount);
    }

    [Fact]
    public void TheSameLiveRunTwiceEndsWithTheSameHash()
    {
        Assert.Equal(Live(networkSeed: 22).Hash, Live(networkSeed: 22).Hash);
    }
}
