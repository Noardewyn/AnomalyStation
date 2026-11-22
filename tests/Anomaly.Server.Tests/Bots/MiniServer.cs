using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// A small stand-in for the real server, speaking the protocol (version 2) over any <see cref="IServerTransport"/>: the join
/// sequence, one Delta per <see cref="Step"/>, movement from MotionInput frames (one tile every <see cref="StepEvery"/> ticks
/// along the wish vector's dominant axis, never into a wall), reconnects by token, three strikes for a protocol violation. The
/// entities have tile positions only (no capsule <c>Pos</c>, no private block). It exists to run clients over the loopback and
/// over real sockets without the whole world (the Godot client's tests use it); it is test code, not the server.
/// </summary>
internal sealed class MiniServer
{
    private sealed class Entity
    {
        public long Id;
        public int X;
        public int Y;
        public long LastMoveTick;
    }

    private sealed class Peer
    {
        public IConnection Connection;
        public string Name;
        public int Session;
        public Entity Entity;
        public sbyte WishX;
        public sbyte WishY;
        public uint LastSeq;
        public int Strikes;
        public bool Joined;
        public Dictionary<long, (int X, int Y)> Known = new();
    }

    private readonly IServerTransport _transport;
    private readonly MapGrid _map;
    private readonly Walls _passability;
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly Dictionary<string, (SessionToken Token, Entity Entity)> _sessions = new();
    private readonly List<Entity> _entities = new();
    private readonly BotRandom _random = new(99);
    private int _nextSession = 1;
    private long _nextEntity = 1000;

    public MiniServer(IServerTransport transport, MapGrid map)
    {
        _transport = transport;
        _map = map;
        _passability = new Walls(map);
        map.SolidBits = _passability.SolidBits();
        transport.ConnectionRequested += OnRequest;
        transport.Connected += OnConnected;
        transport.Received += OnReceived;
        transport.Disconnected += OnDisconnected;
    }

    public int StepEvery { get; init; } = 3;

    public long Tick { get; private set; } = 100;

    public int MaxPlayers { get; init; } = 1000;

    public int Joined => _peers.Values.Count(p => p.Joined);

    public int Violations { get; private set; }

    public int Rejected { get; private set; }

    public int Accepted { get; private set; }

    public int Leaves { get; private set; }

    public int Moves { get; private set; }

    public Dictionary<DisconnectReason, int> RejectReasons { get; } = new();

    public IReadOnlyList<(int X, int Y, long Id)> EntityPositions => _entities.Select(e => (e.X, e.Y, e.Id)).ToList();

    public void Step()
    {
        _transport.Poll();
        Tick++;

        foreach (Peer peer in _peers.Values)
        {
            if (!peer.Joined || (peer.WishX == 0 && peer.WishY == 0) || Tick % StepEvery != 0) continue;
            int dx = 0, dy = 0;
            if (Math.Abs(peer.WishX) >= Math.Abs(peer.WishY)) dx = peer.WishX > 0 ? 1 : -1;
            else dy = peer.WishY > 0 ? 1 : -1;
            if (!_passability.IsWalkable(peer.Entity.X + dx, peer.Entity.Y + dy, 1)) continue;
            peer.Entity.X += dx;
            peer.Entity.Y += dy;
            peer.Entity.LastMoveTick = Tick;
            Moves++;
        }

        foreach (Peer peer in _peers.Values)
        {
            if (!peer.Joined || !peer.Connection.IsConnected) continue;
            var delta = new Delta { Tick = Tick, LastInputSeq = peer.LastSeq, OwnEntity = peer.Entity.Id, FirstNewType = 1 };
            foreach (Entity e in _entities)
            {
                if (!peer.Known.TryGetValue(e.Id, out (int X, int Y) known))
                {
                    delta.Spawned.Add(StateOf(e));
                    peer.Known[e.Id] = (e.X, e.Y);
                }
                else if (known.X != e.X || known.Y != e.Y)
                {
                    NetField mask = NetField.LastMoveTick;
                    if (known.X != e.X) mask |= NetField.X;
                    if (known.Y != e.Y) mask |= NetField.Y;
                    delta.Changed.Add(new EntityChange { Id = e.Id, Mask = mask, X = e.X, Y = e.Y, LastMoveTick = e.LastMoveTick });
                    peer.Known[e.Id] = (e.X, e.Y);
                }
            }
            List<long> gone = peer.Known.Keys.Where(id => !_entities.Exists(e => e.Id == id)).ToList();
            foreach (long id in gone)
            {
                delta.Despawned.Add(id);
                peer.Known.Remove(id);
            }
            peer.Connection.Send(Channel.State, Codec.Encode(delta));
        }

        _transport.Flush();
    }

    private EntityState StateOf(Entity e) => new(e.Id, 0, e.X, e.Y, 1, 2, "Mob" + e.Id, 0f, e.LastMoveTick);

    private void OnRequest(IConnectionRequest request)
    {
        if (!Codec.TryDecode(request.Data, out Hello hello, out _))
        {
            Reject(request, DisconnectReason.BadHello);
            return;
        }
        if (hello.Protocol != ProtocolVersion.Current)
        {
            Reject(request, DisconnectReason.WrongVersion);
            return;
        }
        if (_peers.Count >= MaxPlayers)
        {
            Reject(request, DisconnectReason.ServerFull);
            return;
        }
        if (!hello.ReconnectToken.IsNone)
        {
            if (!_sessions.TryGetValue(hello.Name, out var known) || known.Token != hello.ReconnectToken)
            {
                Reject(request, DisconnectReason.BadToken);
                return;
            }
        }
        else if (_sessions.ContainsKey(hello.Name))
        {
            Reject(request, DisconnectReason.NameInUse);
            return;
        }

        IConnection connection = request.Accept();
        Accepted++;
        var peer = new Peer { Connection = connection, Name = hello.Name, Session = _nextSession++ };
        _peers[connection.Id] = peer;
        if (_sessions.TryGetValue(hello.Name, out var existing))
        {
            peer.Entity = existing.Entity;
        }
        else
        {
            peer.Entity = Spawn();
            _sessions[hello.Name] = (new SessionToken((ulong)peer.Session * 7919UL + 1, 0xABCDEF0123456789UL), peer.Entity);
        }
        if (!_entities.Contains(peer.Entity)) _entities.Add(peer.Entity);
    }

    private void Reject(IConnectionRequest request, DisconnectReason reason)
    {
        Rejected++;
        RejectReasons.TryGetValue(reason, out int n);
        RejectReasons[reason] = n + 1;
        request.Reject(Codec.Encode(new DisconnectInfo(reason, reason.ToString())));
    }

    private Entity Spawn()
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            int x = _random.Next(2, _map.Width - 1);
            int y = _random.Next(2, _map.Height - 1);
            if (_passability.IsWalkable(x, y, 1)) return new Entity { Id = _nextEntity++, X = x, Y = y };
        }
        throw new InvalidOperationException("no free cell");
    }

    private void OnConnected(IConnection connection)
    {
        if (!_peers.TryGetValue(connection.Id, out Peer peer)) return;
        SessionToken token = _sessions[peer.Name].Token;
        connection.Send(Channel.State, Codec.Encode(new Welcome(peer.Session, token, Tick, 20, peer.Entity.Id)));
        connection.Send(Channel.State, Codec.Encode(_map));
        var snapshot = new Snapshot { Tick = Tick, LastInputSeq = 0, OwnEntity = peer.Entity.Id };
        snapshot.Types.Add("/mob");
        foreach (Entity e in _entities.OrderBy(x => x.Id))
        {
            snapshot.Entities.Add(StateOf(e));
            peer.Known[e.Id] = (e.X, e.Y);
        }
        connection.Send(Channel.State, Codec.Encode(snapshot));
        peer.Joined = true;
    }

    private void OnReceived(IConnection connection, Channel channel, ReadOnlySpan<byte> message)
    {
        if (!_peers.TryGetValue(connection.Id, out Peer peer)) return;
        if (message.Length > Limits.MaxInboundMessageBytes || !peer.Joined)
        {
            Strike(peer);
            return;
        }
        if (channel == Channel.Control && Codec.TryDecode(message, out ActionMessage _, out _)) return; // actions are ignored
        if (channel != Channel.Input || !Codec.TryDecode(message, out MotionInputMessage input, out _) || input.Frames.Count == 0)
        {
            Strike(peer);
            return;
        }
        foreach (Anomaly.Core.Motion.MotionInput frame in input.Frames)
        {
            if (frame.Seq <= peer.LastSeq) continue; // the repeats of the redundancy
            peer.LastSeq = frame.Seq;
            peer.WishX = frame.MoveX;
            peer.WishY = frame.MoveY;
        }
    }

    private void Strike(Peer peer)
    {
        Violations++;
        if (++peer.Strikes >= Limits.MaxStrikes && peer.Connection.IsConnected)
        {
            peer.Connection.Disconnect(Codec.Encode(new DisconnectInfo(DisconnectReason.ProtocolViolation, "three strikes")));
        }
    }

    private void OnDisconnected(IConnection connection, TransportDisconnect disconnect)
    {
        if (!_peers.Remove(connection.Id, out Peer peer)) return;
        Leaves++;
        // The mob leaves the world with its player here; a reconnect with the token puts it back where it was.
        if (!_peers.Values.Any(p => p.Entity == peer.Entity)) _entities.Remove(peer.Entity);
    }

    // The walls of a test map by its turf type paths (TestMaps: the rim and pillars are /turf/closed).
    private sealed class Walls
    {
        private readonly bool[] _walkable;
        private readonly int _width, _height;

        public Walls(MapGrid grid)
        {
            _width = grid.Width;
            _height = grid.Height;
            _walkable = new bool[_width * _height];
            int cell = 0;
            foreach (TurfRun run in grid.Runs)
            {
                bool open = grid.TurfTypes[run.TypeIndex].StartsWith("/turf/open", StringComparison.Ordinal);
                for (int i = 0; i < run.Count && cell < _walkable.Length; i++) _walkable[cell++] = open;
            }
        }

        // MapGrid version 2's solidity bits: a bit per cell (x fastest, then y, then z), set where the cell is not walkable.
        public byte[] SolidBits()
        {
            var bits = new byte[(_walkable.Length + 7) / 8];
            for (int i = 0; i < _walkable.Length; i++)
            {
                if (!_walkable[i]) bits[i / 8] |= (byte)(1 << (i % 8));
            }
            return bits;
        }

        public bool IsWalkable(int x, int y, int z) => z == 1 && x >= 1 && y >= 1 && x <= _width && y <= _height && _walkable[(y - 1) * _width + (x - 1)];
    }
}
