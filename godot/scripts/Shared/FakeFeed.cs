using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// A scripted world in the client (<c>--fake-server</c>, development, version 2): the scenes, the camera, the prediction,
/// the HUD and the console can be seen, screenshotted and tested without a server. The world is a <see cref="FakeServer"/>
/// (a 20 TPS room of capsule mobs that speaks protocol version 2 and runs the real controller); this feed carries its messages
/// to a real <see cref="ReplicaWorld"/> through the real <see cref="Codec"/> after a simulated one-way delay with jitter, in order
/// on the reliable channels, and carries the client's frames and actions back the same way (the Input channel unreliable, with
/// an optional loss). So the renderer, the predictor, the HUD and even the byte counts are the real ones; only the server's
/// world is scripted. Wall clock time comes in through <see cref="Poll"/>, so a test drives it with a fake clock.
/// </summary>
public sealed class FakeFeed : IWorldFeed
{
    private const int MaxCatchUpTicks = 40;

    private readonly string _name;
    private readonly FakeServer _server;
    private readonly double _oneWay;
    private readonly double _jitter;
    private readonly double _inputLoss;
    private readonly Random _random;
    private readonly ReplicaWorld _replica = new();
    private readonly Queue<(double ArriveAt, Channel Channel, byte[] Bytes)> _toClient = new();
    private readonly List<(double ArriveAt, Channel Channel, byte[] Bytes)> _toServer = new();
    private double _lastToClient;
    private double _lastControlToServer;
    private double _nextTickAt;
    private bool _started;
    private double _lastPoll;
    private uint _seq;
    private long _deltas;
    private long _bytesSent;
    private long _bytesReceived;
    private FeedPhase _phase = FeedPhase.Idle;

    /// <param name="name">The player's name (the own mob's name).</param>
    /// <param name="seed">Seeds the bots and the network.</param>
    /// <param name="size">The room's width and height in tiles, rim included (at least 12).</param>
    /// <param name="bots">How many wandering bots.</param>
    /// <param name="oneWayLatencyMs">The simulated one-way network delay (a ping is twice that).</param>
    /// <param name="jitterMs">The most extra delay a message gets (uniformly random).</param>
    /// <param name="inputLoss">The share of the client's Input messages that are lost on the way.</param>
    /// <param name="devCommands">Whether the server accepts dev commands.</param>
    /// <param name="map">M3: serve this converted map instead of the generated room (<paramref name="size"/> is then ignored).</param>
    /// <param name="doorSounds">M4b audio: the airlock sound every 2 s near the player (<see cref="FakeServerOptions.DoorSounds"/>).</param>
    /// <param name="inventory">M4a: the version-4 inventory script of the fake server (<see cref="FakeServerOptions.Inventory"/>); also on when the environment variable <c>ANOMALY_FAKE_INVENTORY</c> is <c>1</c> (GAP(lead): a <c>--fake-inventory</c> argument, ClientArgs and ConnectScreen are not P7's).</param>
    public FakeFeed(string name, int seed = 1, int size = 32, int bots = 8, int oneWayLatencyMs = 25, int jitterMs = 10, double inputLoss = 0, bool devCommands = true, FakeMap map = null, bool inventory = false, bool doorSounds = false)
    {
        _name = name;
        _oneWay = oneWayLatencyMs / 1000.0;
        _jitter = jitterMs / 1000.0;
        _inputLoss = inputLoss;
        _random = new Random(seed);
        _server = new FakeServer(name, new FakeServerOptions { Size = size, Bots = bots, Seed = seed, DevCommands = devCommands, Map = map, Inventory = inventory || System.Environment.GetEnvironmentVariable("ANOMALY_FAKE_INVENTORY") == "1", ThrowTicksPerTile = int.TryParse(System.Environment.GetEnvironmentVariable("ANOMALY_FAKE_THROW_TICKS"), out int throwTicks) ? Math.Clamp(throwTicks, 1, 40) : 1, DoorSounds = doorSounds });
        _replica.Spawned += e => Spawned?.Invoke(e);
        _replica.Despawned += e => Despawned?.Invoke(e);
    }

    /// <summary>A room side that leaves room for <paramref name="bots"/> wandering bots (at least 32 tiles).</summary>
    public static int SizeFor(int bots) => Math.Max(32, (int)Math.Ceiling(Math.Sqrt(bots * 10.0)));

    /// <summary>The scripted server behind the feed (the tests ask it what is true).</summary>
    public FakeServer Server => _server;

    public FeedPhase Phase => _phase;

    public string CloseReason => null;

    public bool Rejected => false;

    public string Description => "fake server";

    public uint LastSentSeq => _seq;

    public uint LastAppliedSeq => _replica.HasSnapshot ? _replica.LastInputSeq : 0;

    public MapGrid Map => _phase == FeedPhase.Idle ? null : _replica.Map;

    public long Tick => _replica.Tick;

    public long DeltaCount => _deltas;

    public long OwnEntity => _phase == FeedPhase.Idle ? 0 : _replica.OwnEntity;

    public int Count => _replica.Count;

    public IEnumerable<ReplicaEntity> Entities => _replica.Entities;

    public int RoundTripMs => (int)Math.Round(2 * (_oneWay + _jitter / 2) * 1000);

    public long BytesSent => _bytesSent;

    public long BytesReceived => _bytesReceived;

    public ReplicaWorld Replica => _replica;

    public event Action<ReplicaEntity> Spawned;

    public event Action<ReplicaEntity> Despawned;

    public event Action<ServerText> ServerTextReceived;

    /// <summary>M4a: tg's screentip for the hovered target (the answer to a Hover).</summary>
    public event Action<ContextHintsMessage> ContextHintsReceived;

    /// <summary>True when (<paramref name="x"/>, <paramref name="y"/>) is a wall (for the tests).</summary>
    public bool IsWall(int x, int y) => _server.IsWall(x, y);

    /// <summary>The room's size in tiles.</summary>
    public int Size => _server.Size;

    public void Poll(double nowSeconds)
    {
        _lastPoll = nowSeconds;
        if (!_started)
            Start(nowSeconds);

        // The server's ticks, in real time (a long stall catches up by at most MaxCatchUpTicks).
        if (nowSeconds - _nextTickAt > MaxCatchUpTicks / TileSpace.TickRate)
            _nextTickAt = nowSeconds - MaxCatchUpTicks / TileSpace.TickRate;
        while (_nextTickAt <= nowSeconds)
        {
            RunServerTick(_nextTickAt);
            _nextTickAt += 1.0 / TileSpace.TickRate;
        }

        // What reached the client.
        while (_toClient.Count > 0 && _toClient.Peek().ArriveAt <= nowSeconds)
        {
            var item = _toClient.Dequeue();
            Deliver(item.Channel, item.Bytes);
        }
    }

    public void SendMotion(MotionInputMessage frames)
    {
        if (_phase != FeedPhase.Playing || frames == null || frames.Frames.Count == 0)
            return;
        byte[] bytes = Codec.Encode(frames);
        _bytesSent += bytes.Length;
        _seq = frames.Frames[frames.Frames.Count - 1].Seq;
        if (_inputLoss > 0 && _random.NextDouble() < _inputLoss)
            return;
        // Unreliable: each message has its own delay and may overtake another.
        _toServer.Add((_lastPoll + _oneWay + _random.NextDouble() * _jitter, Channel.Input, bytes));
    }

    public void SendAction(in ActionMessage action)
    {
        if (_phase != FeedPhase.Playing)
            return;
        byte[] bytes = Codec.Encode(action);
        _bytesSent += bytes.Length;
        double arrive = Math.Max(_lastControlToServer, _lastPoll + _oneWay + _random.NextDouble() * _jitter);
        _lastControlToServer = arrive;
        _toServer.Add((arrive, Channel.Control, bytes));
    }

    /// <summary>
    /// A hiccup on the way to the client (the M1 soak's 135 ms gen2 tick, a congested link): nothing the server sent reaches the
    /// client for <paramref name="seconds"/> from the last poll; what comes after arrives right behind, all at once.
    /// </summary>
    public void Hiccup(double seconds)
    {
        double until = _lastPoll + seconds;
        var held = new Queue<(double ArriveAt, Channel Channel, byte[] Bytes)>();
        foreach (var item in _toClient)
            held.Enqueue((Math.Max(item.ArriveAt, until), item.Channel, item.Bytes));
        _toClient.Clear();
        foreach (var item in held)
            _toClient.Enqueue(item);
        _lastToClient = Math.Max(_lastToClient, until);
    }

    public void Disconnect()
    {
        // The fake world has no connection to close; the scene just leaves.
    }

    private void Start(double now)
    {
        _started = true;
        _nextTickAt = now;
        _phase = FeedPhase.Joining;
        // The join: Welcome, the map and a snapshot of tick 0 through the codec, nothing in flight.
        foreach (byte[] message in _server.JoinMessages())
            Deliver(Channel.State, message);
        _phase = FeedPhase.Playing;
        ServerTextReceived?.Invoke(new ServerText(null, "Welcome to the fake server, " + _name + "."));
    }

    private void RunServerTick(double tickTime)
    {
        // The command phase: what has arrived by now (the Input channel in arrival order, then the reliable actions).
        _toServer.Sort((a, b) => a.ArriveAt.CompareTo(b.ArriveAt));
        int taken = 0;
        while (taken < _toServer.Count && _toServer[taken].ArriveAt <= tickTime)
        {
            var item = _toServer[taken++];
            if (item.Channel == Channel.Input)
            {
                if (Codec.TryDecode(item.Bytes, out MotionInputMessage motion, out _))
                    _server.OnMotion(motion);
            }
            else if (Codec.TryDecode(item.Bytes, out ActionMessage action, out _))
            {
                _server.OnAction(action);
            }
        }
        _toServer.RemoveRange(0, taken);

        _server.Step((channel, bytes) =>
        {
            double arrive = Math.Max(_lastToClient, tickTime + _oneWay + _random.NextDouble() * _jitter);
            _lastToClient = arrive; // reliable and ordered
            _toClient.Enqueue((arrive, channel, bytes));
        });
    }

    /// <summary>The client side of the wire: the bytes go through the real codec into the real replica.</summary>
    private void Deliver(Channel channel, byte[] bytes)
    {
        _bytesReceived += bytes.Length;
        if (!Codec.TryPeekId(bytes, out MessageId id))
            throw new InvalidOperationException("the fake server sent a message with no id");
        switch (id)
        {
            case MessageId.Welcome:
                return;
            case MessageId.ServerText:
                if (!Codec.TryDecode(bytes, out ServerText text, out ProtocolError textError))
                    throw new InvalidOperationException("the fake server sent a text the client cannot read: " + textError);
                ServerTextReceived?.Invoke(text);
                return;
            case MessageId.ContextHints:
                if (!Codec.TryDecode(bytes, out ContextHintsMessage hints, out ProtocolError hintsError))
                    throw new InvalidOperationException("the fake server sent hints the client cannot read: " + hintsError);
                ContextHintsReceived?.Invoke(hints);
                return;
        }
        if (!_replica.TryApply(bytes, out id, out ProtocolError error))
            throw new InvalidOperationException("the fake server sent a message the client cannot read: " + error);
        if (id == MessageId.Delta)
            _deltas++;
    }
}
