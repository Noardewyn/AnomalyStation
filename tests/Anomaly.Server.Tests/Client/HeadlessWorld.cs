using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// The client's real stack against the scripted server over the loopback network (M2 P8 scope: "a headless prediction test
/// over the loopback"): <see cref="GameClient"/> over a <see cref="LoopbackClientTransport"/>, the codec both ways, the
/// unreliable Input channel with the network's latency and loss, <see cref="PlaySession"/> with the predictor on top. One
/// <see cref="Tick"/> is one server tick and one network step (50 ms); the client draws <c>FramesPerTick</c> frames in it.
/// </summary>
internal sealed class HeadlessWorld : IDisposable
{
    public const double TickSeconds = 0.05;

    private readonly LoopbackServerTransport _serverTransport;
    private IConnection _connection;

    public HeadlessWorld(FakeServerOptions server = null, LoopbackOptions network = null, int framesPerTick = 3, string name = "Tester", int inputBufferTarget = 1)
    {
        Server = new FakeServer(name, server ?? new FakeServerOptions { Bots = 0, ScriptedEvents = false });
        Network = new LoopbackNetwork(network ?? new LoopbackOptions { Seed = 5 });
        FramesPerTick = framesPerTick;
        _serverTransport = Network.CreateServer();
        _serverTransport.ConnectionRequested += request => _connection = request.Accept();
        _serverTransport.Connected += connection =>
        {
            foreach (byte[] message in Server.JoinMessages())
                connection.Send(Channel.State, message);
        };
        _serverTransport.Received += (connection, channel, message) =>
        {
            if (channel == Channel.Input && Codec.TryDecode(message, out MotionInputMessage motion, out _))
                Server.OnMotion(motion);
            else if (channel == Channel.Control && Codec.TryDecode(message, out ActionMessage action, out _))
                Server.OnAction(action);
        };
        _serverTransport.Start("127.0.0.1", 0);
        Client = new GameClient(Network.CreateClient(), "127.0.0.1", _serverTransport.Port, name);
        Client.Connect();
        // The join: a few network steps until the snapshot is in (the server's tick does not run meanwhile).
        for (int i = 0; i < 40 && Client.Phase != FeedPhase.Playing; i++)
        {
            _serverTransport.Poll();
            Network.Advance();
            Client.Poll(Now);
        }
        Assert.Equal(FeedPhase.Playing, Client.Phase);
        Session = new PlaySession(Client, new PlayOptions(), inputBufferTarget);
    }

    public FakeServer Server { get; }
    public LoopbackNetwork Network { get; }
    public GameClient Client { get; }
    public PlaySession Session { get; }
    public int FramesPerTick { get; }
    public MoveKeys Keys { get; set; }
    public double Yaw { get; set; }
    public long Ticks { get; private set; }
    public double Now => Ticks * TickSeconds;

    /// <summary>Runs <paramref name="ticks"/> ticks; <paramref name="afterTick"/> is called after each (the tick number from 1).</summary>
    public void Run(int ticks, Action<long> afterTick = null, bool session = true)
    {
        for (int i = 0; i < ticks; i++)
        {
            double start = Now;
            for (int f = 0; f < FramesPerTick; f++)
            {
                double at = start + (f + 1) * TickSeconds / FramesPerTick;
                if (session)
                    Session.Frame(at, Keys, Yaw, true);
                else
                    Client.Poll(at);
            }
            _serverTransport.Poll();
            Server.Step((channel, bytes) => _connection?.Send(channel, bytes));
            _serverTransport.Flush();
            Network.Advance();
            Ticks++;
            afterTick?.Invoke(Ticks);
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        _serverTransport.Dispose();
    }
}
