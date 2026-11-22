using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Replication;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Ten bots on the whole server over the loopback (ROADMAP M1 "clients see each other move"): every bot joins, walks and
/// sees the others move; and every bot's replica equals the server's world, scanned in full by P4's oracle, at every tick
/// (M1's D98 stand-in is gone since M2: the avatars are capsule mobs, D105).
/// </summary>
public class LoopbackFleetTests
{
    private readonly ITestOutputHelper _output;

    public LoopbackFleetTests(ITestOutputHelper output) => _output = output;

    private const int BotCount = 10;
    private const int Ticks = 400;

    [Fact]
    public void TenBotsJoinWalkSeeEachOtherAndTheirReplicasEqualTheWorldEveryTick()
    {
        using var rig = new IntegrationRig(oracle: true);
        var bots = new List<Bot>();
        for (int i = 0; i < BotCount; i++) bots.Add(rig.AddBot("Bot" + (i + 1)));

        var lastStep = new Dictionary<string, long>();       // key -> tick of its avatar's last step
        var lastTile = new Dictionary<string, (int X, int Y)>();
        var steps = new Dictionary<string, int>();
        int shortestGap = int.MaxValue;
        int compared = 0;
        rig.AfterTick = () =>
        {
            foreach (Bot bot in bots)
            {
                if (bot.State != BotState.Joined) continue;
                // The replica of every joined bot is the world, field by field, at this very tick.
                List<string> differences = rig.ReplicaDifferences(bot);
                Assert.True(differences.Count == 0, ReplicationRun.Describe(rig.Host.Tick, differences) + " (" + bot.Options.Name + ")");
                compared++;

                string key = bot.Options.Name;
                mob avatar = rig.MobOf(key);
                var tile = ((int)avatar.x, (int)avatar.y);
                if (lastTile.TryGetValue(key, out var before) && before != tile)
                {
                    if (lastStep.TryGetValue(key, out long previous))
                        shortestGap = Math.Min(shortestGap, (int)(rig.Host.Tick - previous));
                    lastStep[key] = rig.Host.Tick;
                    steps[key] = steps.GetValueOrDefault(key) + 1;
                }
                lastTile[key] = tile;
            }
        };

        rig.Tick(Ticks);

        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
        _output.WriteLine($"tick {rig.Host.Tick}, joins open at {rig.Host.JoinsOpenedAt}, replicas compared {compared} times, shortest step gap {shortestGap} ticks");
        foreach (Bot bot in bots)
            _output.WriteLine($"{bot.Options.Name}: steps {steps.GetValueOrDefault(bot.Options.Name)}, own moves {bot.Stats.OwnMoves}, other moves {bot.Stats.OtherMoves}, inputs {bot.Stats.InputsSent}, deltas {bot.Stats.DeltasApplied}");
        foreach (Bot bot in bots)
        {
            string key = bot.Options.Name;
            Assert.True(bot.State == BotState.Joined, $"{key}: {bot.State} {bot.Stats.LastDisconnect} {bot.Stats.LastDesync}");
            Assert.Equal(1, bot.Stats.Joins);
            Assert.Equal(0, bot.Stats.Desyncs);
            Assert.NotEqual(0, bot.Welcome.OwnEntity);
            Assert.Equal(rig.Host.AvatarOf(key), bot.Welcome.OwnEntity);
            Assert.Equal(bot.Welcome.OwnEntity, bot.Replica.OwnEntity);
            Assert.Equal(IntegrationRig.RoomSize + 2 * TestRoom.ServerSpace, bot.Replica.Map.Width); // the room and its ring of space
            Assert.True(steps.GetValueOrDefault(key) >= 20, $"{key} walked {steps.GetValueOrDefault(key)} tiles");
            Assert.True(bot.Stats.OwnMoves >= 20, $"{key} saw itself move {bot.Stats.OwnMoves} times");
            Assert.True(bot.Stats.OtherMoves >= 100, $"{key} saw others move {bot.Stats.OtherMoves} times");
            // Every other bot's avatar is in this bot's replica, and moved there.
            foreach (Bot other in bots.Where(b => b != bot))
                Assert.True(bot.Replica.TryGet(other.Welcome.OwnEntity, out _), $"{key} does not see {other.Options.Name}");
        }
        Assert.True(compared > BotCount * (Ticks - 20), $"replicas compared {compared} times");
    }
}
