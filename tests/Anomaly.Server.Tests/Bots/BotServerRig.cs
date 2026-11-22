using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// The whole M2 server in one process for the bots' tests: <see cref="ServerHost"/> over the loopback transport with the real
/// codec, replicator, locomotion seam and client layer, and <see cref="Bot"/>s as the players. One <see cref="Tick"/> is one
/// server tick, then one network step, then every bot's <see cref="Bot.Update"/> (the cadence of the integration rig).
/// </summary>
internal sealed class BotServerRig : IDisposable
{
    public BotServerRig(ServerConfig config = null, bool strict = true, LoopbackOptions network = null)
    {
        Config = config ?? DefaultConfig();
        Network = new LoopbackNetwork(network ?? new LoopbackOptions { Seed = 3, TimeoutSteps = 40 });
        Server = Network.CreateServer();
        Host = new ServerHost(Config, Server, new Replicator(), output: Out, errors: Err, strictNotTranslated: strict);
        Host.Start();
        while (!Host.JoinsOpen && Host.Tick < 200) Tick();
        Assert.True(Host.JoinsOpen, "joins did not open within 200 ticks");
    }

    public static ServerConfig DefaultConfig(ulong seed = 7, int size = 24, bool? devCommands = null)
        => new() { Seed = seed, RoomWidth = size, RoomHeight = size, Console = false, StatusEveryTicks = 1000, DevCommands = devCommands };

    public ServerConfig Config { get; }
    public LoopbackNetwork Network { get; }
    public LoopbackServerTransport Server { get; }
    public ServerHost Host { get; }
    public World World => Host.World;
    public StringWriter Out { get; } = new();
    public StringWriter Err { get; } = new();
    public List<Bot> Bots { get; } = new();

    public Bot AddBot(string name, BotBehaviour behaviour = BotBehaviour.Wander, bool predict = false, int index = 0, ulong seed = 0, Func<BotOptions, BotOptions> options = null)
    {
        var o = new BotOptions { Name = name, Seed = seed != 0 ? seed : (ulong)(Bots.Count + 1) * 7919, Behaviour = behaviour, Predict = predict, Index = index };
        if (options != null) o = options(o);
        var bot = new Bot(() => Network.CreateClient(), o);
        Bots.Add(bot);
        bot.Connect("127.0.0.1", Server.Port);
        return bot;
    }

    public void Tick(int n = 1)
    {
        for (int i = 0; i < n && !Host.Finished; i++)
        {
            Host.RunTick();
            Network.Advance();
            foreach (Bot bot in Bots) bot.Update();
        }
    }

    public void UntilJoined(Bot bot, int maxTicks = 30)
    {
        for (int i = 0; i < maxTicks && bot.State != BotState.Joined && bot.State != BotState.Disconnected; i++) Tick();
        Assert.True(bot.State == BotState.Joined, $"{bot.Options.Name} did not join: {bot.State} {bot.Stats.LastDisconnect}\n{Err}");
    }

    public mob MobOf(string key) => World.Objects.TryGet(Host.AvatarOf(key), out object o) ? o as mob : null;

    public Session SessionOf(string key) => Host.Sessions.Last(s => s.Key == key);

    public void Dispose()
    {
        foreach (Bot bot in Bots) bot.Dispose();
        Host.Dispose();
    }
}
