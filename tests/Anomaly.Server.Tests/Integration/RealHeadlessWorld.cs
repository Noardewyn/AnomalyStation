using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.M3Maps.R4;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The M2 integration's headless rig: the Godot client's real engine-free stack (<see cref="GameClient"/> and
/// <see cref="PlaySession"/> with the own-capsule prediction) against the <b>real</b> server (<see cref="ServerHost"/> with
/// the booted tg world, the real replicator and the locomotion bridge) over the loopback network with latency and loss. One
/// <see cref="Run"/> tick is the clients' frames (<see cref="FramesPerTick"/> per tick, each sending its motion), one
/// server tick and one network step (50 ms): the cadence of P8's <c>HeadlessWorld</c> against its scripted server.
/// </summary>
internal sealed class RealHeadlessWorld : IDisposable
{
    public const double TickSeconds = 0.05;

    public sealed class Player
    {
        internal Player(string name, GameClient client, PlaySession session, int inputBufferTarget)
        {
            Name = name;
            Client = client;
            Session = session;
            InputBufferTarget = inputBufferTarget;
        }

        public string Name { get; }
        public GameClient Client { get; }
        /// <summary>The game scene's play session; D306: a new one for each round the client joins (<see cref="BetweenRounds"/>).</summary>
        public PlaySession Session { get; private set; }
        internal int InputBufferTarget { get; }
        /// <summary>D306: the game scene left for the waiting screen (GameScene on <see cref="FeedPhase.BetweenRounds"/>) until the next round plays.</summary>
        public bool BetweenRounds { get; private set; }

        // One frame as the Godot client draws it: the game scene's, or the waiting screen's, which polls the feed and starts a new game
        // scene (a new play session: its prediction's seqs start over, as the next round's session expects) once the next round plays.
        internal void Frame(double at, bool session)
        {
            if (BetweenRounds)
            {
                Client.Poll(at);
                if (Client.Phase == FeedPhase.Playing)
                {
                    Session = new PlaySession(Client, new PlayOptions(), InputBufferTarget);
                    BetweenRounds = false;
                }
            }
            else if (session)
                Session.Frame(at, Keys, Yaw, true);
            else
                Client.Poll(at);
            if (Client.Phase == FeedPhase.BetweenRounds)
                BetweenRounds = true;
        }
        public MoveKeys Keys { get; set; }
        public double Yaw { get; set; }
        public bool Gone { get; internal set; }
        public PredictionCounters Counters => Session.Prediction.Counters;
    }

    public RealHeadlessWorld(LoopbackOptions network = null, ServerConfig config = null, int framesPerTick = 3, bool strict = true, InterestOptions interest = null)
    {
        // a Meta world waits for a slot of the run's Meta worlds (MetaWorldGate: the memory holds a dozen, not 32)
        _gateTaken = config?.Map != null && MetaWorldGate.Enter();
        try
        {
            Network = new LoopbackNetwork(network ?? new LoopbackOptions { Seed = 5, TimeoutSteps = 200 });
            Server = Network.CreateServer();
            // the tests run at tg's tiles per second (MoveSpeedScale 1; the server's default is DV70's 0.5)
            Config = config ?? new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f };
            // M10 slice C: interest management as the server runs it (Program.cs), unless the config turns it off; a join's Snapshot carries
            // all it wants (P3.7's budget would stream it over about 25 ticks of game time, and the tests count their time from the joins)
            var replicator = new Replicator(new ReplicatorOptions { Interest = Config.NoInterest ? null : interest ?? new InterestOptions { JoinSpawnBudget = int.MaxValue } });
            Host = new ServerHost(Config, Server, replicator, output: Out, errors: Err, strictNotTranslated: strict);
            SoundLengths.Install(Host.World, RigSoundLengths.Lengths); // M4b audio slice 5: the sound tokens' lengths (IntegrationRig)
            FramesPerTick = framesPerTick;
            Host.Start();
            while (!Host.JoinsOpen && Host.Tick < 200)
            {
                Host.RunTick();
                Network.Advance();
            }
            Assert.True(Host.JoinsOpen, "joins did not open");
        }
        catch
        {
            if (_gateTaken)
                MetaWorldGate.Exit();
            throw;
        }
    }

    public LoopbackNetwork Network { get; }
    public LoopbackServerTransport Server { get; }
    public ServerConfig Config { get; }
    public ServerHost Host { get; }
    /// <summary>The current world's replicator (a round's restart replaces it).</summary>
    public Replicator Replicator => (Replicator)Host.Replicator;
    public World World => Host.World;
    public StringWriter Out { get; } = new();
    public StringWriter Err { get; } = new();
    public List<Player> Players { get; } = new();
    public List<Bot> Bots { get; } = new();

    /// <summary>Connects a bot (the M2 bots' version-2 client, predicting or not) and runs until it joined.</summary>
    public Bot AddBot(string name, BotBehaviour behaviour, bool predict)
    {
        var bot = new Bot(() => Network.CreateClient(), new BotOptions { Name = name, Seed = (ulong)(Bots.Count + 1) * 7919, Behaviour = behaviour, Predict = predict });
        Bots.Add(bot);
        bot.Connect("127.0.0.1", Server.Port);
        for (int i = 0; i < 200 && bot.State != BotState.Joined && bot.State != BotState.Disconnected; i++)
            Run(1);
        Assert.Equal(BotState.Joined, bot.State);
        return bot;
    }
    public int FramesPerTick { get; }
    public long Ticks { get; private set; }
    public double Now => Ticks * TickSeconds;

    /// <summary>Connects a player and runs until its client plays (the join ticks run the world).</summary>
    public Player Join(string name, int inputBufferTarget = 1)
    {
        var client = new GameClient(Network.CreateClient(), "127.0.0.1", Server.Port, name);
        client.Connect();
        for (int i = 0; i < 200 && client.Phase != FeedPhase.Playing; i++)
            Step(session: false, extra: client);
        Assert.Equal(FeedPhase.Playing, client.Phase);
        // P3.7: what the Snapshot's budget left out streams in over the next ticks (a test that sets its own budget); a test starts with all of it
        int ordinal = Host.Sessions.Last(s => s.Key == name).Ordinal;
        for (int i = 0; i < 200 && Replicator.QueuedCount(ordinal) > 0; i++)
            Step(session: true, extra: client); // the players already in play go on playing
        var player = new Player(name, client, new PlaySession(client, new PlayOptions(), inputBufferTarget), inputBufferTarget);
        Players.Add(player);
        Run(5);
        return player;
    }

    /// <summary>
    /// M3 R4: runs the ticks of a connect screen's join (<see cref="JoinAttempt"/>, its retry included) until it plays or
    /// is closed; returns the phase.
    /// </summary>
    public FeedPhase Join(JoinAttempt join, int maxTicks = 200)
    {
        FeedPhase phase = join.Client.Phase;
        for (int i = 0; i < maxTicks && phase != FeedPhase.Playing && phase != FeedPhase.Closed; i++)
            Step(session: true, extra: null, poll: at => phase = join.Poll(at));
        return phase;
    }

    /// <summary>
    /// M10 slice I: connects a client the test made (with its own address or token) and runs until it plays or is refused; returns
    /// the phase (<see cref="Adopt"/> makes a playing one a player).
    /// </summary>
    public FeedPhase Connect(GameClient client, int maxTicks = 200)
    {
        client.Connect();
        for (int i = 0; i < maxTicks && client.Phase != FeedPhase.Playing && client.Phase != FeedPhase.Closed; i++)
            Step(session: false, extra: client);
        return client.Phase;
    }

    /// <summary>M3 R4: a playing client the rig did not connect becomes one of its players (the game scene takes it over).</summary>
    public Player Adopt(string name, GameClient client, int inputBufferTarget = 1)
    {
        Assert.Equal(FeedPhase.Playing, client.Phase);
        var player = new Player(name, client, new PlaySession(client, new PlayOptions(), inputBufferTarget), inputBufferTarget);
        Players.Add(player);
        Run(5);
        return player;
    }

    /// <summary>M3 R4: the player's client crashes: it stops without a goodbye (the server hears nothing more).</summary>
    public void Crash(Player player) => player.Gone = true;

    /// <summary>The player leaves (its body stays in the world, standing: tg's SSD).</summary>
    public void Leave(Player player)
    {
        player.Client.Disconnect();
        player.Gone = true;
        Run(10);
    }

    /// <summary>DV403: called after every rendered frame of the players (a test that samples what they draw frame by frame).</summary>
    public Action AfterFrame { get; set; }

    /// <summary>Runs <paramref name="ticks"/> ticks; <paramref name="afterTick"/> after each.</summary>
    public void Run(int ticks, Action<long> afterTick = null)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step(session: true, extra: null);
            afterTick?.Invoke(Ticks);
        }
    }

    private void Step(bool session, GameClient extra, Action<double> poll = null)
    {
        double start = Now;
        for (int f = 0; f < FramesPerTick; f++)
        {
            double at = start + (f + 1) * TickSeconds / FramesPerTick;
            foreach (Player p in Players)
            {
                if (p.Gone) continue;
                p.Frame(at, session);
            }
            extra?.Poll(at);
            poll?.Invoke(at);
            if (session)
                AfterFrame?.Invoke();
        }
        Host.WaitForNextWorld(); // D306: a fast-forward run waits for the worker's build, so the tests' tick budgets hold
        Host.RunTick();
        Network.Advance();
        foreach (Bot bot in Bots)
            bot.Update();
        Ticks++;
    }

    /// <summary>The player's mob in the world.</summary>
    public mob_living MobOf(Player player) => World.Objects.TryGet(Host.AvatarOf(player.Name), out object o) ? o as mob_living : null;

    /// <summary>The server's capsule state of the player's mob.</summary>
    public MotionState ServerState(Player player)
    {
        Assert.True(LocomotionSeam.TryGetState(World, MobOf(player).dm_id, out MotionState state, out _));
        return state;
    }

    private readonly bool _gateTaken;

    public void Dispose()
    {
        foreach (Player p in Players)
            p.Client.Dispose();
        foreach (Bot bot in Bots)
            bot.Dispose();
        Host.Dispose();
        if (_gateTaken)
            MetaWorldGate.Exit();
    }
}
