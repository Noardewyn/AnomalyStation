using System.Diagnostics;
using System.IO;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The whole server on real UDP sockets: the host with M1 P2's LiteNetLib transport on 127.0.0.1 and an ephemeral port, two
/// of M1 P5's bots in LiteNetLib's manual mode. The world is stepped as fast as the loop turns (no pacer): the test only waits
/// for the sockets, and ends as soon as both bots joined, walked and saw each other move.
/// </summary>
public class RealSocketTests
{
    [Fact]
    public void TwoBotsJoinAndMoveOverRealSockets()
    {
        var config = new ServerConfig
        {
            Seed = 7, RoomWidth = IntegrationRig.RoomSize, RoomHeight = IntegrationRig.RoomSize, Console = false, StatusEveryTicks = 1000,
            Port = 0, BindAddress = "127.0.0.1",
        };
        var output = new StringWriter();
        var errors = new StringWriter();
        var transport = new LiteNetServerTransport();
        using var host = new ServerHost(config, transport, new Replicator(), output: output, errors: errors, strictNotTranslated: true);
        host.Start();
        int port = transport.Port;
        Assert.NotEqual(0, port);
        while (!host.JoinsOpen) host.RunTick();

        var bots = new List<Bot>();
        for (int i = 1; i <= 2; i++)
        {
            var bot = new Bot(() => new LiteNetClientTransport(manualMode: true), new BotOptions { Name = "Socket" + i, Seed = (ulong)i * 31, JoinRetryUpdates = 100 });
            bots.Add(bot);
            bot.Connect("127.0.0.1", port);
        }
        try
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(15)
                   && !bots.All(b => b.State == BotState.Joined && b.Stats.OwnMoves >= 5 && b.Stats.OtherMoves >= 5))
            {
                host.RunTick();
                foreach (Bot bot in bots) bot.Update();
                Thread.Sleep(2);
            }

            foreach (Bot bot in bots)
            {
                Assert.True(bot.State == BotState.Joined, $"{bot.Options.Name}: {bot.State} {bot.Stats.LastDisconnect}\n{output}\n{errors}");
                Assert.Equal(0, bot.Stats.Desyncs);
                Assert.True(bot.Stats.OwnMoves >= 5 && bot.Stats.OtherMoves >= 5,
                    $"{bot.Options.Name}: own moves {bot.Stats.OwnMoves}, other moves {bot.Stats.OtherMoves} after {host.Tick} ticks");
                Assert.Equal(host.AvatarOf(bot.Options.Name), bot.Welcome.OwnEntity);
                Assert.True(bot.Replica.TryGet(bots.Single(b => b != bot).Welcome.OwnEntity, out _));
            }
            Assert.Equal(0, host.World.RuntimeErrorCount);
            Assert.Contains("listening on 127.0.0.1:" + port, output.ToString());
        }
        finally
        {
            foreach (Bot bot in bots)
            {
                bot.Disconnect();
                bot.Dispose();
            }
        }
    }
}
