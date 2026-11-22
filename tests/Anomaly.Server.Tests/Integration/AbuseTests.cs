using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Abuse through the whole server (ROADMAP M1 "malformed and flooded commands are rejected", D40): P5's abusive bots against
/// the real host, the real codec and the booted tg world. A MotionInput flood and three malformed messages disconnect, an
/// oversized message is refused, a command before the Welcome is a strike; and what the world did equals a run of only
/// what the host accepted: the recorded command log, replayed into a fresh world, gives the same state hash.
/// </summary>
public class AbuseTests
{
    private static Bot Abuser(IntegrationRig rig, string name, BotBehaviour behaviour, Func<BotOptions, BotOptions> options = null)
    {
        Bot bot = rig.AddBot(name, behaviour, options: options);
        for (int i = 0; i < 60 && bot.State != BotState.Disconnected; i++) rig.Tick();
        Assert.Equal(BotState.Disconnected, bot.State);
        return bot;
    }

    [Fact]
    public void AMotionInputFloodOverTheRateLimitDisconnectsWithFlooding()
    {
        using var rig = new IntegrationRig();
        Bot honest = rig.AddBot("Honest");
        rig.UntilJoined(honest);
        // Forty messages per tick (800 per second): over the bucket (30 per second, bursts of 15) by enough to reach the flood limit.
        Bot flood = Abuser(rig, "Flood", BotBehaviour.Flood, o => new BotOptions { Name = o.Name, Seed = o.Seed, Behaviour = o.Behaviour, FloodFramesPerUpdate = 40 });
        Assert.Equal(DisconnectReason.Flooding, flood.LastDisconnect.Reason);
        Session s = rig.Host.Sessions.Single(x => x.Key == "Flood");
        Assert.True(s.RateLimited >= ServerHost.FloodDropLimit);
        Assert.True(flood.Stats.InputsSent > s.RateLimited);
        // What the bucket let through reached the world as commands; nothing it dropped did.
        // The jitter buffer keeps three frames and the host takes one a tick, so only a few reached the world as commands.
        int applied = rig.Host.Log.Commands.Count(c => c.Kind == CommandKind.Motion && c.Session == s.Ordinal);
        Assert.InRange(applied, 0, flood.Stats.InputsSent - s.RateLimited);
        Assert.Equal(BotState.Joined, honest.State);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void AHugeFloodIsClosedWithinASecondAndOnlyTheBucketsFramesReachTheWorld()
    {
        using var rig = new IntegrationRig();
        Bot flood = Abuser(rig, "Flood", BotBehaviour.Flood); // 50 messages per tick: about 48 of them dropped per tick
        Assert.Equal(DisconnectReason.Flooding, flood.LastDisconnect.Reason);
        Session s = rig.Host.Sessions.Single(x => x.Key == "Flood");
        Assert.InRange(rig.Host.Log.Commands.Count(c => c.Kind is CommandKind.Input or CommandKind.Motion && c.Session == s.Ordinal), 0, 20);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void ThreeMalformedMessagesDisconnect()
    {
        using var rig = new IntegrationRig();
        Bot malformed = Abuser(rig, "Garbage", BotBehaviour.Malformed);
        Assert.Equal(DisconnectReason.ProtocolViolation, malformed.LastDisconnect.Reason);
        Session s = rig.Host.Sessions.Single(x => x.Key == "Garbage");
        Assert.Equal(Limits.MaxStrikes, s.Strikes);
        Assert.Equal(Limits.MaxStrikes, malformed.Stats.AbuseSent); // the third one closed it
        Assert.Equal(0, rig.Host.Log.Commands.Count(c => c.Kind is CommandKind.Input or CommandKind.Motion));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void AnOversizedMessageIsRefusedUnreadAsAStrike()
    {
        using var rig = new IntegrationRig();
        Bot oversized = rig.AddBot("Big", BotBehaviour.Oversized);
        rig.UntilJoined(oversized);
        rig.Tick(); // the first oversized message reaches the server
        Session s = rig.Host.Sessions.Single(x => x.Key == "Big");
        Assert.InRange(s.Strikes, 1, Limits.MaxStrikes);
        for (int i = 0; i < 10 && oversized.State != BotState.Disconnected; i++) rig.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, oversized.LastDisconnect.Reason);
        Assert.Contains("an oversized message", rig.Out.ToString());
        Assert.Equal(0, rig.Host.Log.Commands.Count(c => c.Kind is CommandKind.Input or CommandKind.Motion));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void ACommandBeforeTheWelcomeIsAStrikeAndTheWelcomeWaitsForASleepingLogin()
    {
        // Against the real host a client can only command before its Welcome while tg's Login sleeps: the host applies a
        // Join in the tick that accepted the connection, and a plain mob's Login does not sleep (P5's PreWelcomeCommand bot
        // therefore sees its Welcome together with the accept). So the player's mob here is one whose Login sleeps 10 ticks:
        // the player joins, the world moves the client to that mob (BYOND's client.mob assignment), the player leaves and
        // comes back with the token to the mob that kept its key.
        using var rig = new IntegrationRig();
        var first = new RawClient(rig, "Early");
        for (int i = 0; i < 5 && first.Welcome == null; i++) rig.Tick();
        Assert.NotNull(first.Welcome);
        mob_test_m1_integration_slow_login slow;
        using (rig.World.Enter())
        {
            mob avatar = rig.MobOf("Early");
            slow = Dm.@new<mob_test_m1_integration_slow_login>(avatar.loc);
            avatar.client.mob = slow;
        }
        rig.Tick(15);
        Assert.Equal(slow.dm_id, rig.Host.AvatarOf("Early"));
        first.Transport.Disconnect();
        rig.Tick(3);
        Assert.Equal("Early", slow.key);

        var again = new RawClient(rig, "Early", first.Welcome.Value.Token);
        rig.Tick(2); // accepted, the Join applied: client/New waits for the sleeping Login
        Session s = rig.Host.Sessions.Single(x => x.Ordinal == 2);
        Assert.Equal(SessionState.LoggingIn, s.State);
        Assert.Null(again.Welcome);
        again.Input(HeldDirs.North); // the client knows it is connected, not that it is in the world
        rig.Tick(2);
        Assert.Equal(1, s.Strikes);
        Assert.Null(again.Gone);
        for (int i = 0; i < 15 && again.Welcome == null; i++) rig.Tick();
        Assert.NotNull(again.Welcome);
        Assert.Equal(slow.dm_id, again.Welcome.Value.OwnEntity);
        QueuedCommand join = rig.Host.Log.Commands.Single(c => c.Kind == CommandKind.Join && c.Session == 2);
        Assert.True(again.Welcome.Value.Tick >= join.ApplyTick + 10, $"the Welcome of tick {again.Welcome.Value.Tick} came before the Login of the Join at {join.ApplyTick} woke");
        Assert.Equal(0, rig.Host.Log.Commands.Count(c => c.Kind is CommandKind.Input or CommandKind.Motion));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void TheWorldOfARunWithAbusersEqualsAReplayOfWhatTheHostAccepted()
    {
        // The live run: three honest walkers and every kind of abuser, the commands recorded as the host applies them.
        var record = new StringWriter();
        ulong live;
        long end;
        int rejected;
        using (var rig = new IntegrationRig(record: record))
        {
            for (int i = 1; i <= 3; i++) rig.AddBot("Walker" + i);
            rig.Tick(10);
            rig.AddBot("Flood", BotBehaviour.Flood, options: o => new BotOptions { Name = o.Name, Seed = o.Seed, Behaviour = o.Behaviour, FloodFramesPerUpdate = 40 });
            rig.AddBot("Burst", BotBehaviour.Flood);
            rig.AddBot("Garbage", BotBehaviour.Malformed);
            rig.AddBot("Big", BotBehaviour.Oversized);
            rig.AddBot("Old", BotBehaviour.WrongVersion);
            rig.Tick(150);
            Assert.All(rig.Bots.Where(b => b.Options.Behaviour != BotBehaviour.Wander), b => Assert.Equal(BotState.Disconnected, b.State));
            Assert.All(rig.Bots.Where(b => b.Options.Behaviour == BotBehaviour.Wander), b => Assert.Equal(BotState.Joined, b.State));
            Assert.Equal(DisconnectReason.WrongVersion, rig.Bots.Single(b => b.Options.Name == "Old").LastDisconnect.Reason);
            rejected = rig.Host.Sessions.Sum(s => s.Strikes + s.RateLimited);
            Assert.True(rejected > 50, $"{rejected} rejected messages");
            Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
            rig.Stop();
            live = rig.Host.FinalHash;
            end = rig.Host.Tick;
        }

        // The replay: only the accepted commands (the log), no network, a fresh world.
        CommandLog log = CommandLog.Parse(record.ToString(), out string error);
        Assert.Null(error);
        Assert.Equal(live, log.FinalHash);
        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths); // the live rig's table (CommandLog.SoundsDigest)
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(live, result.FinalHash);
        Assert.Equal(0, replay.World.RuntimeErrorCount);
    }
}
