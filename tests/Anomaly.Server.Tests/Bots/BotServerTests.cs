using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// The version-2 bots against the whole server in one process (<see cref="BotServerRig"/>: the real host, codec,
/// replicator, locomotion seam and client layer over the loopback): wandering in continuous space, prediction without
/// corrections, rest and shove actions reaching the host, every abuse mode meeting its answer (D40), and a run of ten mixed
/// bots. The tg human is still a skeleton where M2 P1 and P2 have not landed: those runs are lenient
/// (<c>NotTranslated</c> is reported, not thrown), and the tests that need the real damage and rest procs wait for them
/// (<see cref="M2Needs.Medical"/>).
/// </summary>
public class BotServerTests
{
    private readonly ITestOutputHelper _output;

    public BotServerTests(ITestOutputHelper output) => _output = output;

    // Whether the capsule at the replicated position overlaps a solid tile of the bot's own map.
    private static bool InsideAWall(Bot bot, MotionState s)
    {
        double r = MotionMath.DefaultRadius - 0.011;
        double px = s.X / 256.0, py = s.Y / 256.0;
        for (int tx = (int)Math.Floor(px - r + 0.5); tx <= (int)Math.Floor(px + r + 0.5); tx++)
        {
            for (int ty = (int)Math.Floor(py - r + 0.5); ty <= (int)Math.Floor(py + r + 0.5); ty++)
            {
                if (!bot.Replica.IsSolid(tx, ty, s.Z)) continue;
                double nx = Math.Clamp(px, tx - 0.5, tx + 0.5), ny = Math.Clamp(py, ty - 0.5, ty + 0.5);
                if ((px - nx) * (px - nx) + (py - ny) * (py - ny) < r * r) return true;
            }
        }
        return false;
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void AWanderingBotWalksTheRoomWithoutEnteringAWall()
    {
        using var rig = new BotServerRig(strict: false);
        Bot bot = rig.AddBot("Walker");
        rig.UntilJoined(bot);
        var tiles = new HashSet<(int, int)>();
        int inside = 0;
        for (int t = 0; t < 600; t++)
        {
            rig.Tick();
            MotionState s = bot.Replica.Own.Motion;
            if (InsideAWall(bot, s)) inside++;
            tiles.Add((s.TileX, s.TileY));
        }
        Assert.Equal(0, inside);
        Assert.True(tiles.Count >= 12, $"it covered {tiles.Count} tiles");
        Assert.True(bot.Stats.OwnMoves >= 100, $"{bot.Stats.OwnMoves} own moves");
        Assert.Equal(0, bot.Stats.Desyncs);
        Assert.Equal(BotState.Joined, bot.State);
        Assert.Equal(0, rig.SessionOf("Walker").Strikes);
        Assert.True(bot.Stats.FramesSent >= 550);
        Assert.True(bot.Replica.TryGet(bot.Welcome.OwnEntity, out ReplicaEntity own) && own.HasPos);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion | M2Needs.Motion)]
    public void APredictingBotAloneHasNoCorrectionsAndItsStateIsTheServers()
    {
        using var rig = new BotServerRig(strict: false);
        Bot bot = rig.AddBot("Predictor", predict: true);
        rig.UntilJoined(bot);
        rig.Tick(800);
        Assert.NotNull(bot.Predictor);
        PredictionCounters c = bot.Predictor.Counters;
        _output.WriteLine($"reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles}");
        Assert.True(c.Reconciliations >= 600, $"{c.Reconciliations} reconciliations");
        Assert.Equal(0, c.Corrections);   // scope 2l: walking alone at zero latency gives zero corrections
        Assert.Equal(0, c.Snaps);
        Assert.True(c.Mismatches <= 2, $"{c.Mismatches} bit mismatches"); // the replay is bit-equal to the server's step (one tick of the start-up may differ in a field)
        Assert.Equal(0, bot.Stats.Corrections);
        Assert.True(bot.Stats.Predicted);
        Assert.True(bot.Stats.OwnMoves >= 100);
        Assert.True(bot.Stats.FramesSent >= 700);
    }

    [M2Theory(M2Needs.Protocol | M2Needs.Locomotion | M2Needs.Motion)]
    [InlineData(2)]
    [InlineData(5)]
    public void APredictingBotKeepsUpOverANetworkWithADelay(int latencySteps)
    {
        var network = new LoopbackOptions { Seed = 8, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps, TimeoutSteps = 60 };
        using var rig = new BotServerRig(strict: false, network: network);
        Bot bot = rig.AddBot("Predictor", predict: true);
        rig.UntilJoined(bot, 60);
        rig.Tick(800);
        PredictionCounters c = bot.Predictor.Counters;
        _output.WriteLine($"latency {latencySteps} steps: reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles}");
        Assert.True(c.Reconciliations >= 600);
        // Alone, the replay of the unacknowledged frames from the server's state is the server's own step: only the clock's
        // settling at the start (frames sent twice or not at all while it finds its depth) may differ.
        Assert.True(c.Corrections <= 6, $"{c.Corrections} corrections");
        Assert.True(c.Snaps <= 3, $"{c.Snaps} snaps"); // at the speed of the skeleton human (a tile a tick) a skipped frame is a tile
        Assert.Equal(BotState.Joined, bot.State);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void ARestBotSendsRestAndWalkRunTogglesThatTheHostApplies()
    {
        using var rig = new BotServerRig(strict: false);
        Bot bot = rig.AddBot("Sleeper", BotBehaviour.Rest, options: o => new BotOptions
        {
            Name = o.Name, Seed = o.Seed, Behaviour = o.Behaviour,
            Rest = new RestSettings { MinUpSeconds = 2, MaxUpSeconds = 3, MinDownSeconds = 1, MaxDownSeconds = 2 },
        });
        rig.UntilJoined(bot);
        rig.Tick(400);
        Session s = rig.SessionOf("Sleeper");
        Assert.True(bot.Stats.ActionsByKind[(int)ActionKind.Rest] >= 3, $"{bot.Stats.ActionsByKind[(int)ActionKind.Rest]} Rest actions");
        Assert.True(s.ActionsApplied >= 3);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(0, s.RateLimited);
        Assert.Equal(BotState.Joined, bot.State);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion | M2Needs.Click)]
    public void AShoveBotWalksToAnotherPlayerAndRightClicksIt()
    {
        using var rig = new BotServerRig(strict: false);
        Bot target = rig.AddBot("Target", BotBehaviour.Idle);
        Bot shover = rig.AddBot("Shover", BotBehaviour.Shove);
        rig.UntilJoined(target);
        rig.UntilJoined(shover);
        // The two spawn points are in different rooms of the default layout and the shover has no path finding: it wanders
        // until it sees the target (at tg's run speed, RUN_DELAY 1.5, that takes about a minute of game time).
        const int ticks = 2400;
        rig.Tick(ticks);
        Session s = rig.SessionOf("Shover");
        _output.WriteLine($"shoves {shover.Stats.ShovesSent}, applied {s.ActionsApplied}, rejected clicks {s.ClicksRejected}");
        Assert.True(shover.Stats.ShovesSent >= 1, "it never got within reach");
        Assert.True(s.ActionsApplied >= 1);
        Assert.Equal(0, s.ClicksRejected);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(0, s.RateLimited);
        Assert.Equal(BotState.Joined, shover.State);
        // The cooldown keeps the clicks under tg's melee delay: at most one per 20 ticks.
        Assert.True(shover.Stats.ShovesSent <= ticks / ShovePlanner.CooldownTicks + 1);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion | M2Needs.Click | M2Needs.Medical)]
    public void AShoveBotKnocksTheTargetDownOnceTheLivingCoreHasLanded()
    {
        using var rig = new BotServerRig(strict: true);
        Bot target = rig.AddBot("Target", BotBehaviour.Idle);
        Bot shover = rig.AddBot("Shover", BotBehaviour.Shove);
        rig.UntilJoined(target);
        rig.UntilJoined(shover);
        bool floored = false;
        // As above, the shover first has to find the target (about a minute of game time), then push it into a wall.
        for (int t = 0; t < 3600 && !floored; t++)
        {
            rig.Tick();
            if (target.Replica.TryGet(target.Welcome.OwnEntity, out ReplicaEntity e) && PostureBits.IsLying(e.State.Posture)) floored = true;
        }
        Assert.True(floored, "the shoved player never lay down");
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }

    // ---- the abuse modes meet their answers (D40) ----

    private static Bot Abuser(BotServerRig rig, string name, BotBehaviour behaviour, Func<BotOptions, BotOptions> options = null)
    {
        Bot bot = rig.AddBot(name, behaviour, options: options);
        for (int i = 0; i < 100 && bot.State != BotState.Disconnected; i++) rig.Tick();
        return bot;
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void AMotionFloodIsDroppedAndClosedAsFlooding()
    {
        using var rig = new BotServerRig(strict: false);
        Bot honest = rig.AddBot("Honest");
        rig.UntilJoined(honest);
        Bot flood = Abuser(rig, "Flood", BotBehaviour.Flood);
        Assert.Equal(BotState.Disconnected, flood.State);
        Assert.Equal(DisconnectReason.Flooding, flood.LastDisconnect.Reason);
        Session s = rig.SessionOf("Flood");
        Assert.True(s.RateLimited >= ServerHost.FloodDropLimit);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(BotState.Joined, honest.State);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void AnActionFloodIsDroppedPerKindAndClosedAsFlooding()
    {
        using var rig = new BotServerRig(strict: false);
        Bot flood = Abuser(rig, "ActionFlood", BotBehaviour.ActionFlood);
        Assert.Equal(DisconnectReason.Flooding, flood.LastDisconnect.Reason);
        Session s = rig.SessionOf("ActionFlood");
        Assert.True(s.RateLimited >= ServerHost.FloodDropLimit);
        Assert.Equal(0, s.Strikes);
        // The bucket let a few through (toggle bucket 4, overall 10): never the whole flood.
        Assert.InRange(s.ActionsApplied, 0, 20);
        Assert.True(flood.Stats.ActionsSent >= ServerHost.FloodDropLimit);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void AForgedSeqIsAStrikeAndThreeClose()
    {
        using var rig = new BotServerRig(strict: false);
        Bot forger = Abuser(rig, "Forger", BotBehaviour.ForgedSeq);
        Assert.Equal(DisconnectReason.ProtocolViolation, forger.LastDisconnect.Reason);
        Assert.Equal(Limits.MaxStrikes, rig.SessionOf("Forger").Strikes);
        Assert.Contains("seq is out of range", rig.Out.ToString());
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void MalformedAndOversizedMessagesAreStrikes()
    {
        using var rig = new BotServerRig(strict: false);
        Bot garbage = Abuser(rig, "Garbage", BotBehaviour.Malformed);
        Assert.Equal(DisconnectReason.ProtocolViolation, garbage.LastDisconnect.Reason);
        Assert.Equal(Limits.MaxStrikes, rig.SessionOf("Garbage").Strikes);
        Bot big = Abuser(rig, "Big", BotBehaviour.Oversized);
        Assert.Equal(DisconnectReason.ProtocolViolation, big.LastDisconnect.Reason);
        Assert.Contains("an oversized message", rig.Out.ToString());
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void BadActionsAreStrikesAndAClickOnAMissingEntityIsDroppedSilently()
    {
        using var rig = new BotServerRig(strict: false);
        Bot bad = Abuser(rig, "BadAction", BotBehaviour.BadAction);
        Assert.Equal(DisconnectReason.ProtocolViolation, bad.LastDisconnect.Reason);
        Session s = rig.SessionOf("BadAction");
        Assert.Equal(Limits.MaxStrikes, s.Strikes);
        // The legal clicks on entities no world has did not strike: they were dropped in the world.
        Assert.True(s.ClicksRejected >= 1, $"{s.ClicksRejected} rejected clicks");
    }

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion)]
    public void DevCommandAbuseWithDevCommandsOffIsAnsweredAndOnlyTheOverlongLineStrikes()
    {
        using var rig = new BotServerRig(BotServerRig.DefaultConfig(devCommands: false), strict: false);
        Bot dev = rig.AddBot("DevAbuse", BotBehaviour.DevAbuse);
        rig.UntilJoined(dev);
        rig.Tick(3);                         // the at-cap line goes first, the over-cap ones from the fifth tick on
        Session s = rig.SessionOf("DevAbuse");
        Assert.Equal(0, s.Strikes);          // a legal dev command with dev off is an answer, not a violation
        Assert.True(dev.Stats.ServerTexts >= 1, "no reply came back");
        Assert.Equal(0, s.ActionsApplied);
        for (int i = 0; i < 100 && dev.State == BotState.Joined; i++) rig.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, dev.LastDisconnect.Reason); // the over-cap lines are strikes
        Assert.DoesNotContain(rig.Host.Log.Commands, c => c.Kind == CommandKind.Action);
    }

    // ---- a run of mixed bots ----

    [M2Fact(M2Needs.Protocol | M2Needs.Locomotion | M2Needs.Click)]
    public void TenMixedBotsStayJoinedAndActForThirtySeconds()
    {
        using var rig = new BotServerRig(strict: false);
        var bots = new List<Bot>();
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            bots.Add(rig.AddBot("Mixed" + (i + 1), BotBehaviour.Mixed, predict: i % 5 == 0, index: index, options: o => new BotOptions
            {
                Name = o.Name, Seed = o.Seed, Behaviour = o.Behaviour, Predict = o.Predict, Index = o.Index,
                Rest = new RestSettings { MinUpSeconds = 3, MaxUpSeconds = 6, MinDownSeconds = 1, MaxDownSeconds = 2 },
            }));
        }
        rig.Tick(600);
        foreach (Bot bot in bots)
        {
            Assert.True(bot.State == BotState.Joined, $"{bot.Options.Name}: {bot.State} {bot.Stats.LastDisconnect} {bot.Stats.LastDesync}");
            Assert.Equal(0, bot.Stats.Desyncs);
            Assert.True(bot.Stats.OwnMoves >= 20 || bot.Behaviour == BotBehaviour.Rest, $"{bot.Options.Name} ({bot.Behaviour}) moved {bot.Stats.OwnMoves} times");
        }
        Assert.Equal(0, rig.Host.Sessions.Sum(s => s.Strikes));
        Assert.True(bots.Sum(b => b.Stats.RestsStarted) >= 1, "no one lay down");
        Assert.True(bots.Sum(b => b.Stats.ShovesSent) >= 1, "no one shoved");
        foreach (Bot bot in bots.Where(b => b.Options.Predict))
        {
            PredictionCounters c = bot.Predictor.Counters;
            _output.WriteLine($"{bot.Options.Name} ({bot.Behaviour}): reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, max error {c.MaxErrorTiles:0.00}");
        }
        Assert.True(bots.Where(b => b.Options.Predict).All(b => b.Predictor.Counters.Snaps <= 3), "a predicting bot in a crowd snaps now and then at contacts, not all the time");
    }

    [M2Fact(M2Needs.All)]
    public void TenMixedBotsRunTwoMinutesWithoutRuntimes()
    {
        // scope 2l / p6.md: wander, rest and shove for 2 minutes of game time on the whole server, tg's NotTranslated on.
        using var rig = new BotServerRig(BotServerRig.DefaultConfig(size: 48), strict: true);
        var bots = new List<Bot>();
        for (int i = 0; i < 10; i++)
            bots.Add(rig.AddBot("Mixed" + (i + 1), BotBehaviour.Mixed, predict: i % 2 == 0, index: i));
        rig.Tick(2 * 60 * 20);
        foreach (Bot bot in bots)
        {
            Assert.True(bot.State == BotState.Joined, $"{bot.Options.Name}: {bot.State} {bot.Stats.LastDisconnect} {bot.Stats.LastDesync}");
            Assert.Equal(0, bot.Stats.Desyncs);
            Assert.Equal(0, bot.Stats.Exceptions);
        }
        _output.WriteLine($"rests {bots.Sum(b => b.Stats.RestsStarted)}, shoves {bots.Sum(b => b.Stats.ShovesSent)}, SSmobs ticks {rig.Host.Metrics.RunMobsStep.Count}, peak {rig.Host.Metrics.RunMobsStep.Max} us");
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Err.ToString());
        Assert.True(bots.Sum(b => b.Stats.RestsStarted) >= 1);
        Assert.True(bots.Sum(b => b.Stats.ShovesSent) >= 1);
        Assert.True(rig.Host.Metrics.RunMobsStep.Count >= 2, "SSmobs never fired (the living core's stub)");
        Assert.Equal(0, rig.Host.Sessions.Sum(s => s.Strikes));
    }
}
