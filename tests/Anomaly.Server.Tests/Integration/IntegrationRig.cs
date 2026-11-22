using System.IO;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Replication;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The whole M1 server in one process, end to end (the lead's integration, apps.md section 3): M1 P3's host
/// (<see cref="ServerHost"/>) over M1 P2's loopback transport and codec, M1 P4's <see cref="Replicator"/>, the booted tg
/// world with M1 P1's client layer behind the real <see cref="ClientSeam"/> (no stand-ins), and M1 P5's <see cref="Bot"/>s as
/// the players. One <see cref="Tick"/> is one server tick: <see cref="ServerHost.RunTick"/>, one network step, then every
/// bot's <see cref="Bot.Update"/> (the network has no latency unless the options give it some, so a bot sees the tick's
/// Delta in the same <see cref="Tick"/> and its input reaches the next server tick). <see cref="DmWorldState.NotTranslatedThrows"/>
/// is on by default: a leaf the M1 path must not reach is a runtime error, which the tests count.
/// </summary>
internal sealed class IntegrationRig : IDisposable
{
    /// <summary>The room of the tests: smaller than the server's 48 x 48 default, so the boot is quick.</summary>
    public const int RoomSize = 24;

    public IntegrationRig(ServerConfig config = null, LoopbackOptions network = null, bool strict = true, TextWriter record = null,
        bool oracle = false, Func<SessionToken> tokens = null)
    {
        Config = config ?? DefaultConfig();
        Network = new LoopbackNetwork(network ?? new LoopbackOptions { Seed = 3, TimeoutSteps = 40 });
        Server = Network.CreateServer();
        Replicator = new Replicator();
        Host = new ServerHost(Config, Server, Replicator, output: Out, errors: Err, recordTo: record, strictNotTranslated: strict,
            newToken: tokens);
        // M4b audio slice 5: the sound length fixture (the sound tokens of the fire alarms, firelocks, the gravity generator and
        // the overclocked vents read their files' lengths, SSsounds.get_sound_length), so a rig does not depend on the sound
        // tool's index being built in the checkout; installed before the Master initializes the atoms.
        SoundLengths.Install(Host.World, RigSoundLengths.Lengths);
        if (oracle)
        {
            // The oracle starts observing moves at the replicator's attach (the host's constructor, after the boot).
            Oracle = new WorldOracle();
            Truth = Oracle.Scan(Host.World);
        }
        Host.Start();
        while (!Host.JoinsOpen && Host.Tick < 200) Tick();
        Assert.True(Host.JoinsOpen, "joins did not open within 200 ticks");
    }

    public ServerConfig Config { get; }
    public LoopbackNetwork Network { get; }
    public LoopbackServerTransport Server { get; }
    public Replicator Replicator { get; }
    public ServerHost Host { get; }
    public World World => Host.World;
    public StringWriter Out { get; } = new();
    public StringWriter Err { get; } = new();
    public List<Bot> Bots { get; } = new();

    /// <summary>The hand-driven clients (<see cref="RawClient"/>), polled after the bots.</summary>
    public List<RawClient> Clients { get; } = new();

    /// <summary>The full-scan oracle (P4's), when the rig was made with one: it scans the world after every server tick.</summary>
    public WorldOracle Oracle { get; }

    /// <summary>The oracle's scan of the last server tick.</summary>
    public SortedDictionary<long, EntityView> Truth { get; private set; }

    /// <summary>Called after every <see cref="Tick"/> (the bots updated): the tests' per-tick checks.</summary>
    public Action AfterTick { get; set; }

    public static ServerConfig DefaultConfig(ulong seed = 7)
        => new() { Seed = seed, RoomWidth = RoomSize, RoomHeight = RoomSize, Console = false, StatusEveryTicks = 1000, MoveSpeedScale = 1f }; // tg's speed, not DV70's

    /// <summary>A bot that connects now (its Hello reaches the server in the next tick).</summary>
    public Bot AddBot(string name, BotBehaviour behaviour = BotBehaviour.Wander, ulong seed = 0, Func<BotOptions, BotOptions> options = null)
    {
        var o = new BotOptions { Name = name, Seed = seed != 0 ? seed : (ulong)(Bots.Count + 1) * 7919, Behaviour = behaviour };
        if (options != null) o = options(o);
        var bot = new Bot(() => Network.CreateClient(), o);
        Bots.Add(bot);
        bot.Connect("127.0.0.1", Server.Port);
        return bot;
    }

    /// <summary>Ticks until <paramref name="bot"/> joined (Welcome, MapGrid and Snapshot applied).</summary>
    public void UntilJoined(Bot bot, int maxTicks = 20)
    {
        for (int i = 0; i < maxTicks && bot.State != BotState.Joined && bot.State != BotState.Disconnected; i++) Tick();
        Assert.True(bot.State == BotState.Joined, $"{bot.Options.Name} did not join: {bot.State} {bot.Stats.LastDisconnect}\n{Err}");
    }

    public void Tick(int n = 1)
    {
        for (int i = 0; i < n && !Host.Finished; i++)
        {
            Host.RunTick();
            if (Oracle != null) Truth = Oracle.Scan(World);
            Network.Advance();
            foreach (Bot bot in Bots) bot.Update();
            foreach (RawClient client in Clients) client.Transport.Poll();
            AfterTick?.Invoke();
        }
    }

    /// <summary>The live avatar of a key (the mob the host keyed for it), or null.</summary>
    public mob MobOf(string key)
        => World.Objects.TryGet(Host.AvatarOf(key), out object o) ? o as mob : null;

    /// <summary>The differences between a joined bot's replica and the oracle's scan of the same tick (empty when equal).</summary>
    public List<string> ReplicaDifferences(Bot bot)
    {
        Assert.NotNull(Oracle);
        Assert.Equal(Host.Tick, bot.Replica.Tick);
        return ReplicationRun.Differences(bot.Replica, Truth);
    }

    /// <summary>The runtime errors of the run so far (stderr of the host), for an assertion message.</summary>
    public string Runtimes => Err.ToString();

    /// <summary>Stops the server cleanly (a stop request: the shutdown tick) and ticks until it finished.</summary>
    public void Stop()
    {
        Host.RequestStop();
        for (int i = 0; i < 5 && !Host.Finished; i++) Tick();
        Assert.True(Host.Finished);
    }

    public void Dispose()
    {
        foreach (Bot bot in Bots) bot.Dispose();
        Host.Dispose();
    }
}

/// <summary>
/// A hand-driven protocol client over the loopback, for what a <see cref="Bot"/> does not do on purpose (a Hello with a
/// chosen token): it records the Welcome, the server texts and the end of its connection, and applies the State channel
/// to its own replica.
/// </summary>
internal sealed class RawClient
{
    private uint _seq;

    public RawClient(IntegrationRig rig, string name, SessionToken token = default)
    {
        Transport = rig.Network.CreateClient();
        Transport.Received += (channel, message) =>
        {
            Received.Add((channel, message.ToArray()));
            Assert.True(Codec.TryPeekId(message, out MessageId id));
            switch (id)
            {
                case MessageId.Welcome:
                    Assert.True(Codec.TryDecode(message, out Welcome w, out _));
                    Welcome = w;
                    break;
                case MessageId.ServerText:
                    Assert.True(Codec.TryDecode(message, out ServerText t, out _));
                    Texts.Add(t.Html);
                    break;
                case MessageId.SayModal: // version 38: the say window's props, 3 s after the login
                    Assert.True(Codec.TryDecode(message, out SayModalMessage _, out _));
                    break;
                case MessageId.Runechat:
                    Assert.True(Codec.TryDecode(message, out RunechatMessage _, out _));
                    break;
                default:
                    Assert.True(Replica.TryApply(message, out _, out ProtocolError error), error.ToString());
                    break;
            }
        };
        Transport.Disconnected += info =>
        {
            GoneKind = info.Kind;
            if (info.Data is { Length: > 0 } && Codec.TryDecode(info.Data, out DisconnectInfo d, out _)) Gone = d;
        };
        rig.Clients.Add(this);
        Transport.Connect("127.0.0.1", rig.Server.Port, Codec.Encode(new Hello(ProtocolVersion.Current, name, token)));
    }

    public LoopbackClientTransport Transport { get; }
    public ReplicaWorld Replica { get; } = new();
    public List<string> Texts { get; } = new();

    /// <summary>Every message the client received, in order, with its channel (the raw bytes).</summary>
    public List<(Channel Channel, byte[] Message)> Received { get; } = new();
    public Welcome? Welcome { get; private set; }
    public DisconnectInfo? Gone { get; private set; }
    public TransportDisconnectKind? GoneKind { get; private set; }

    // Version 2: a MotionInput frame on the Input channel (a wish east for East, none for the rest).
    public void Input(HeldDirs dirs)
    {
        var message = new MotionInputMessage();
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(++_seq, (sbyte)((dirs & HeldDirs.East) != 0 ? 127 : 0), (sbyte)((dirs & HeldDirs.North) != 0 ? 127 : 0), 0, Anomaly.Core.Motion.MotionInputFlags.None));
        Transport.Send(Channel.Input, Codec.Encode(message));
    }
}

/// <summary>A recorder of tg's login and logout signals on a mob (COMSIG_MOB_LOGIN, COMSIG_MOB_LOGOUT), as a plain datum.</summary>
[DmPath("/datum/test_m1_integration_listener")]
public class datum_test_m1_integration_listener : datum
{
    public readonly List<string> Events = new();

    [SignalHandler]
    public object on_login(object source = null)
    {
        Events.Add("Login");
        return null;
    }

    [SignalHandler]
    public object on_logout(object source = null)
    {
        Events.Add("Logout");
        return null;
    }

    public string Joined => string.Join(",", Events);
}

/// <summary>
/// A plain mob whose Login sleeps 10 ticks before tg's (the case tg warns about, login.dm:2-5), so that a session stays
/// logging in long enough for a client to command before its Welcome. A fixture type of the port's own (rules 15).
/// </summary>
[DmPath("/mob/test_m1_integration_slow_login")]
public class mob_test_m1_integration_slow_login : mob
{
    public override async DmTask<object> Login()
    {
        await Dm.sleep(10 * Dm.world.tick_lag);
        return await base.Login();
    }
}
