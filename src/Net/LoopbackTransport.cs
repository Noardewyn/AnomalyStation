using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Net;

// Owner: M1 P2. Skeletons by the M1 contracts step: the public surface and the semantics below are frozen, the bodies are
// P2's.
//
// Decision (contracts step): the loopback's latency is counted in network steps, not milliseconds. The network moves only
// when its owner calls LoopbackNetwork.Advance (the tests: once per server tick), so a loopback run is deterministic,
// independent of the wall clock and of the machine's speed, and replays exactly from its seed. One step stands for one
// tick, 50 ms at 20 TPS.
//
// Additions of P2 (documented in docs/ai/specs/protocol.md section 7): LoopbackOptions.TimeoutSteps, LoopbackClientTransport.Drop
// (a client that vanishes without a goodbye), LoopbackNetwork.DroppedUnreliable, LoopbackServerTransport.MaxPendingRequests.

/// <summary>
/// The simulated network of <see cref="LoopbackNetwork"/>, seeded (scope 2e: LiteNetLib's own simulation is dead in the
/// NuGet build, so tests simulate here).
/// <list type="bullet">
/// <item><description>Latency: a message (or a connection request, accept or close) sent during step S becomes deliverable at
/// step S + L, with L drawn uniformly from [<see cref="MinLatencySteps"/>, <see cref="MaxLatencySteps"/>] per message;
/// the receiver's next <c>Poll</c> at or after that step raises it. L = 0: the receiver's next <c>Poll</c>.</description></item>
/// <item><description>Loss: on the unreliable channel a message is dropped with probability <see cref="LossRate"/>; on the
/// reliable channels nothing is lost, a "lost" message is retransmitted <see cref="RetransmitSteps"/> steps later and every
/// later message of that channel waits behind it (head-of-line blocking, as in LiteNetLib's reliable ordered
/// channel).</description></item>
/// <item><description>Reorder: on the unreliable channel a message is delivered after the next one with probability
/// <see cref="ReorderRate"/>; reliable channels keep their order.</description></item>
/// <item><description>Size: an unreliable message over <see cref="MaxUnreliableBytes"/> is refused at the send, as LiteNetLib
/// refuses one over the MTU.</description></item>
/// </list>
/// The random draws come from one generator seeded with <see cref="Seed"/>, in send order: three draws for every message
/// sent (latency, loss, reorder) and one more for each retransmission of a reliable one, so one seed and one script of sends
/// always give one schedule.
/// </summary>
public sealed class LoopbackOptions
{
    public ulong Seed { get; init; } = 1;
    public int MinLatencySteps { get; init; }
    public int MaxLatencySteps { get; init; }
    public double LossRate { get; init; }
    public int RetransmitSteps { get; init; } = 2;
    public double ReorderRate { get; init; }
    public int MaxUnreliableBytes { get; init; } = Channels.MaxUnreliableBytes;

    /// <summary>The steps without traffic after which a dropped connection times out on both ends (an addition of P2: 100 steps are 5 s at 20 TPS, LiteNetLib's default).</summary>
    public int TimeoutSteps { get; init; } = 100;
}

/// <summary>
/// An in-memory network of one server and any number of clients, for tests (scope 2f: the end-to-end test without
/// sockets, the abuse and reconnect tests). Single-threaded: the test drives it from one thread.
/// </summary>
public sealed class LoopbackNetwork
{
    /// <summary>The first port <c>Start(address, 0)</c> hands out.</summary>
    public const int FirstEphemeralPort = 40000;

    private SplitMix64 _random; // mutable struct: not readonly, or every draw would work on a copy
    private readonly Dictionary<int, LoopbackServerTransport> _servers = new();
    private readonly List<LoopbackLink> _links = new();
    private long _step;
    private long _sequence;
    private int _nextPort = FirstEphemeralPort;
    private int _nextClient = 1;
    private LoopbackServerTransport _server;

    /// <summary>
    /// Restarts the random draws from <paramref name="seed"/>: a test that measures a stretch of traffic pins its schedule after its
    /// setup, whose message count moves with everything a join sends (gaps CI1).
    /// </summary>
    public void Reseed(ulong seed) => _random = new SplitMix64(seed);

    public LoopbackNetwork(LoopbackOptions options = null)
    {
        Options = options ?? new LoopbackOptions();
        if (Options.MinLatencySteps < 0 || Options.MaxLatencySteps < Options.MinLatencySteps)
            throw new ArgumentOutOfRangeException(nameof(options), "latency must satisfy 0 <= min <= max");
        if (Options.LossRate < 0 || Options.LossRate > 1 || Options.ReorderRate < 0 || Options.ReorderRate > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "loss and reorder rates are probabilities");
        if (Options.RetransmitSteps < 1 || Options.TimeoutSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "retransmit and timeout take at least one step");
        _random = new SplitMix64(Options.Seed);
    }

    public LoopbackOptions Options { get; }

    /// <summary>The number of steps so far.</summary>
    public long CurrentStep => _step;

    /// <summary>Unreliable messages the simulation dropped so far (an addition of P2, for the tests' bookkeeping).</summary>
    public long DroppedUnreliable { get; internal set; }

    /// <summary>Moves the network <paramref name="steps"/> steps forward (once per server tick in tests).</summary>
    public void Advance(int steps = 1)
    {
        if (steps < 0)
            throw new ArgumentOutOfRangeException(nameof(steps));
        // A message held back to be reordered, with no later message behind it in its step, is let go now.
        for (int i = _links.Count - 1; i >= 0; i--)
            _links[i].ReleaseHeld();
        _step += steps;
    }

    /// <summary>The server transport of this network (one per network; <c>Start</c> registers it under its address and port).</summary>
    public LoopbackServerTransport CreateServer()
    {
        if (_server != null)
            throw new InvalidOperationException("a loopback network has one server transport");
        _server = new LoopbackServerTransport(this);
        return _server;
    }

    /// <summary>A new client transport on this network.</summary>
    public LoopbackClientTransport CreateClient() => new(this, _nextClient++);

    internal int AllocatePort()
    {
        while (_servers.ContainsKey(_nextPort))
            _nextPort++;
        return _nextPort++;
    }

    internal void Register(int port, LoopbackServerTransport server)
    {
        if (!_servers.TryAdd(port, server))
            throw new InvalidOperationException("port " + port + " is in use on this loopback network");
    }

    internal void Unregister(int port) => _servers.Remove(port);

    internal bool TryGetServer(int port, out LoopbackServerTransport server) => _servers.TryGetValue(port, out server) && server.IsRunning;

    internal long NextSequence() => ++_sequence;

    internal double NextUnit() => _random.NextUnit();

    internal int DrawLatency()
    {
        int min = Options.MinLatencySteps, max = Options.MaxLatencySteps;
        ulong draw = _random.Next();
        return max == min ? min : min + (int)(draw % (ulong)(max - min + 1));
    }

    internal void Track(LoopbackLink link) => _links.Add(link);

    internal void Untrack(LoopbackLink link) => _links.Remove(link);

    /// <summary>A simulated random stream: SplitMix64, so a seed means the same numbers on every machine and runtime.</summary>
    private struct SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public double NextUnit() => (Next() >> 11) * (1.0 / (1UL << 53));
    }
}

internal enum EnvelopeKind : byte
{
    Request,
    Accepted,
    Rejected,
    Message,
    Close,
    TimedOut,
    ConnectFailed,
}

/// <summary>One thing in flight toward an endpoint: deliverable at <see cref="Deliver"/>, in (Deliver, Sequence) order.</summary>
internal sealed class Envelope
{
    public EnvelopeKind Kind;
    public long Deliver;
    public long Sequence;
    public long Sent;
    public LoopbackLink Link;
    public Channel Channel;
    public byte[] Data;
}

/// <summary>An endpoint's inbound queue, ordered by (deliver step, send sequence).</summary>
internal sealed class Inbox
{
    private readonly PriorityQueue<Envelope, (long, long)> _queue = new();

    public int Count => _queue.Count;

    public void Add(Envelope envelope) => _queue.Enqueue(envelope, (envelope.Deliver, envelope.Sequence));

    public bool TryTakeDue(long step, out Envelope envelope)
    {
        if (_queue.TryPeek(out envelope, out (long Deliver, long Sequence) priority) && priority.Deliver <= step)
        {
            _queue.Dequeue();
            return true;
        }

        envelope = null;
        return false;
    }

    public void Clear() => _queue.Clear();
}

/// <summary>The pipe between one client and the server: the schedule of every message in both directions.</summary>
internal sealed class LoopbackLink
{
    private readonly LoopbackNetwork _network;
    private readonly long[] _lastToServer = new long[Channels.Count];
    private readonly long[] _lastToClient = new long[Channels.Count];
    private readonly Held[] _heldToServer = new Held[Channels.Count];
    private readonly Held[] _heldToClient = new Held[Channels.Count];
    private bool _tracked;

    public LoopbackLink(LoopbackNetwork network, LoopbackClientTransport client, LoopbackServerTransport server)
    {
        _network = network;
        Client = client;
        Server = server;
    }

    public LoopbackClientTransport Client { get; }

    public LoopbackServerTransport Server { get; }

    /// <summary>The server's end, once the request was accepted.</summary>
    public LoopbackServerConnection ServerSide { get; set; }

    /// <summary>True after <see cref="LoopbackClientTransport.Drop"/>: nothing is delivered in either direction any more.</summary>
    public bool Dropped { get; set; }

    /// <summary>True once the client closed the connection (a request still in flight is then ignored).</summary>
    public bool ClientClosed { get; set; }

    private sealed class Held
    {
        public Envelope Envelope;
    }

    /// <summary>
    /// Sends one message (or one control envelope) toward the server or the client. Draws latency, loss and reorder, applies
    /// the channel's rules and puts the envelope in the receiver's inbox.
    /// </summary>
    public void Send(bool toServer, EnvelopeKind kind, Channel channel, byte[] data)
    {
        if (Dropped)
            return;
        LoopbackOptions options = _network.Options;
        long now = _network.CurrentStep;
        int latency = _network.DrawLatency();
        double lossDraw = _network.NextUnit();
        double reorderDraw = _network.NextUnit();
        long deliver = now + latency;
        long[] last = toServer ? _lastToServer : _lastToClient;
        var envelope = new Envelope { Kind = kind, Sent = now, Link = this, Channel = channel, Data = data };

        bool reliable = kind != EnvelopeKind.Message || Channels.IsReliable(channel);
        if (reliable)
        {
            for (int attempts = 0; lossDraw < options.LossRate && attempts < 8; attempts++)
            {
                deliver += options.RetransmitSteps;
                lossDraw = _network.NextUnit();
            }

            // In order: nothing of this channel overtakes what was sent before it. A close, an accept or a reject waits for
            // everything that was sent to that side before it.
            long floor = kind == EnvelopeKind.Message ? last[(int)channel] : last.Max();
            if (deliver < floor)
                deliver = floor;
            if (kind == EnvelopeKind.Message)
                last[(int)channel] = deliver;
            else
                for (int i = 0; i < last.Length; i++)
                    last[i] = deliver;
            envelope.Deliver = deliver;
            Enqueue(toServer, envelope);
            return;
        }

        if (lossDraw < options.LossRate)
        {
            _network.DroppedUnreliable++;
            return;
        }

        envelope.Deliver = deliver;
        Held[] heldSlots = toServer ? _heldToServer : _heldToClient;
        Held held = heldSlots[(int)channel];
        if (held != null)
        {
            // The message held back goes out right after this one.
            heldSlots[(int)channel] = null;
            Enqueue(toServer, envelope);
            Envelope late = held.Envelope;
            late.Deliver = Math.Max(late.Deliver, envelope.Deliver);
            Enqueue(toServer, late);
        }
        else if (reorderDraw < options.ReorderRate)
        {
            heldSlots[(int)channel] = new Held { Envelope = envelope };
            if (!_tracked)
            {
                _tracked = true;
                _network.Track(this);
            }
        }
        else
        {
            Enqueue(toServer, envelope);
        }
    }

    /// <summary>Lets go of a message that was held back for a reorder and found no later message in its step.</summary>
    public void ReleaseHeld()
    {
        if (!_tracked)
            return;
        _tracked = false;
        _network.Untrack(this);
        for (int i = 0; i < Channels.Count; i++)
        {
            Release(_heldToServer, i, true);
            Release(_heldToClient, i, false);
        }
    }

    private void Release(Held[] slots, int index, bool toServer)
    {
        Held held = slots[index];
        if (held == null)
            return;
        slots[index] = null;
        Enqueue(toServer, held.Envelope);
    }

    /// <summary>Puts an envelope in an endpoint's inbox at an exact step (the timeouts and a failed connect).</summary>
    public void Schedule(bool toServer, EnvelopeKind kind, long deliver)
    {
        var envelope = new Envelope { Kind = kind, Deliver = deliver, Sent = _network.CurrentStep, Link = this, Data = Array.Empty<byte>() };
        Enqueue(toServer, envelope);
    }

    private void Enqueue(bool toServer, Envelope envelope)
    {
        envelope.Sequence = _network.NextSequence();
        if (toServer)
            Server?.Inbox.Add(envelope);
        else
            Client.Inbox.Add(envelope);
    }
}

/// <summary>The server end of a <see cref="LoopbackNetwork"/>.</summary>
public sealed class LoopbackServerTransport : IServerTransport
{
    private readonly LoopbackNetwork _network;
    private readonly Dictionary<int, LoopbackServerConnection> _connections = new();
    private readonly Queue<(LoopbackServerConnection Connection, TransportDisconnect Info)> _local = new();
    private int _port;
    private int _nextConnectionId = 1;
    private int _requestsThisPoll;

    internal LoopbackServerTransport(LoopbackNetwork network)
    {
        _network = network;
    }

    internal Inbox Inbox { get; } = new();

    internal bool IsRunning { get; private set; }

    /// <summary>Requests answered in one <see cref="Poll"/> before the rest are refused with <see cref="DisconnectReason.ServerFull"/> (an addition of P2, like the LiteNetLib transport's).</summary>
    public int MaxPendingRequests { get; set; } = Limits.MaxPendingConnectionRequests;

    /// <summary>The connections that are up.</summary>
    public int ConnectionCount => _connections.Count;

    public int Port => _port;

    public event Action<IConnectionRequest> ConnectionRequested;
    public event Action<IConnection> Connected;
    public event ServerReceivedHandler Received;
    public event Action<IConnection, TransportDisconnect> Disconnected;

    public void Start(string bindAddress, int port)
    {
        if (IsRunning)
            throw new InvalidOperationException("the transport is already running");
        if (port < 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        _port = port == 0 ? _network.AllocatePort() : port;
        _network.Register(_port, this);
        IsRunning = true;
    }

    public void Poll()
    {
        _requestsThisPoll = 0;
        while (_local.Count > 0)
        {
            (LoopbackServerConnection connection, TransportDisconnect info) = _local.Dequeue();
            Disconnected?.Invoke(connection, info);
        }

        while (Inbox.TryTakeDue(_network.CurrentStep, out Envelope envelope))
        {
            switch (envelope.Kind)
            {
                case EnvelopeKind.Request:
                    HandleRequest(envelope);
                    break;
                case EnvelopeKind.Message:
                    LoopbackServerConnection live = envelope.Link.ServerSide;
                    if (live is { IsConnected: true } && !envelope.Link.Dropped)
                    {
                        live.Tally.Received(envelope.Channel, envelope.Data.Length);
                        live.LatencySteps = _network.CurrentStep - envelope.Sent;
                        Received?.Invoke(live, envelope.Channel, envelope.Data);
                    }

                    break;
                case EnvelopeKind.Close:
                    EndRemotely(envelope, TransportDisconnectKind.RemoteClose);
                    break;
                case EnvelopeKind.TimedOut:
                    EndRemotely(envelope, TransportDisconnectKind.Timeout);
                    break;
            }
        }
    }

    public void Flush()
    {
        // Nothing is buffered: a send is already scheduled.
    }

    public void Stop()
    {
        if (!IsRunning)
            return;
        IsRunning = false;
        _network.Unregister(_port);
        foreach (LoopbackServerConnection connection in _connections.Values.ToArray())
            connection.Disconnect(ReadOnlySpan<byte>.Empty);
        Inbox.Clear();
    }

    public void Dispose() => Stop();

    private void HandleRequest(Envelope envelope)
    {
        LoopbackLink link = envelope.Link;
        if (link.Client.IsClosedBeforeAccept(link))
            return;
        var request = new LoopbackRequest(this, link, envelope.Data);
        if (++_requestsThisPoll > MaxPendingRequests)
        {
            request.Reject(Codec.Encode(new DisconnectInfo(DisconnectReason.ServerFull, "too many connection requests")));
            return;
        }

        try
        {
            ConnectionRequested?.Invoke(request);
        }
        finally
        {
            if (!request.Answered)
                request.Reject(ReadOnlySpan<byte>.Empty);
            request.Expire();
        }

        if (request.Connection != null)
            Connected?.Invoke(request.Connection);
    }

    private void EndRemotely(Envelope envelope, TransportDisconnectKind kind)
    {
        LoopbackServerConnection connection = envelope.Link.ServerSide;
        if (connection == null || !connection.IsConnected)
            return;
        connection.MarkClosed();
        _connections.Remove(connection.Id);
        Disconnected?.Invoke(connection, new TransportDisconnect(kind, envelope.Data));
    }

    internal LoopbackServerConnection Accept(LoopbackLink link)
    {
        var connection = new LoopbackServerConnection(this, link, _nextConnectionId++);
        link.ServerSide = connection;
        _connections.Add(connection.Id, connection);
        link.Send(false, EnvelopeKind.Accepted, Channel.Control, Array.Empty<byte>());
        return connection;
    }

    internal void Forget(LoopbackServerConnection connection, TransportDisconnect local)
    {
        _connections.Remove(connection.Id);
        _local.Enqueue((connection, local));
    }
}

/// <summary>A connection of a <see cref="LoopbackServerTransport"/>.</summary>
internal sealed class LoopbackServerConnection : IConnection, IChannelCounters
{
    private readonly LoopbackServerTransport _transport;
    private readonly LoopbackLink _link;

    public LoopbackServerConnection(LoopbackServerTransport transport, LoopbackLink link, int id)
    {
        _transport = transport;
        _link = link;
        Id = id;
        IsConnected = true;
    }

    public int Id { get; }

    public string RemoteEndPoint => _link.Client.EndPoint;

    public bool IsConnected { get; private set; }

    internal ChannelTally Tally { get; } = new();

    internal long LatencySteps { get; set; }

    public long BytesSent => Tally.BytesSent;

    public long BytesReceived => Tally.BytesReceived;

    public long MessagesSent => Tally.MessagesSent;

    public long MessagesReceived => Tally.MessagesReceived;

    public int RoundTripMs => (int)(LatencySteps * 2 * 50);

    public long WireBytesSent => Tally.BytesSent;

    public long WireBytesReceived => Tally.BytesReceived;

    public long BytesSentOn(Channel channel) => Tally.BytesSentOn(channel);

    public long BytesReceivedOn(Channel channel) => Tally.BytesReceivedOn(channel);

    public long MessagesSentOn(Channel channel) => Tally.MessagesSentOn(channel);

    public long MessagesReceivedOn(Channel channel) => Tally.MessagesReceivedOn(channel);

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (!Channels.IsValid(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        CheckUnreliableSize(channel, message.Length, _link);
        if (!IsConnected)
            return;
        Tally.Sent(channel, message.Length);
        _link.Send(false, EnvelopeKind.Message, channel, message.ToArray());
    }

    public void Disconnect(ReadOnlySpan<byte> reason)
    {
        if (!IsConnected)
            return;
        IsConnected = false;
        _link.Send(false, EnvelopeKind.Close, Channel.Control, reason.ToArray());
        _transport.Forget(this, new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>()));
    }

    internal void MarkClosed() => IsConnected = false;

    internal static void CheckUnreliableSize(Channel channel, int length, LoopbackLink link)
    {
        int max = link.Client.Network.Options.MaxUnreliableBytes;
        if (!Channels.IsReliable(channel) && length > max)
            throw new ArgumentException("an unreliable message of " + length + " bytes is over the limit of " + max, nameof(length));
    }
}

/// <summary>A connection request of the loopback, alive during the <c>ConnectionRequested</c> event.</summary>
internal sealed class LoopbackRequest : IConnectionRequest
{
    private readonly LoopbackServerTransport _transport;
    private readonly LoopbackLink _link;
    private readonly byte[] _data;
    private bool _expired;

    public LoopbackRequest(LoopbackServerTransport transport, LoopbackLink link, byte[] data)
    {
        _transport = transport;
        _link = link;
        _data = data;
    }

    public bool Answered { get; private set; }

    public LoopbackServerConnection Connection { get; private set; }

    public ReadOnlySpan<byte> Data
    {
        get
        {
            ThrowIfExpired();
            return _data;
        }
    }

    public string RemoteEndPoint => _link.Client.EndPoint;

    public IConnection Accept()
    {
        ThrowIfExpired();
        if (Answered)
            throw new InvalidOperationException("the request was answered already");
        Answered = true;
        Connection = _transport.Accept(_link);
        return Connection;
    }

    public void Reject(ReadOnlySpan<byte> reason)
    {
        ThrowIfExpired();
        if (Answered)
            throw new InvalidOperationException("the request was answered already");
        Answered = true;
        _link.Send(false, EnvelopeKind.Rejected, Channel.Control, reason.ToArray());
    }

    internal void Expire() => _expired = true;

    private void ThrowIfExpired()
    {
        if (_expired)
            throw new InvalidOperationException("the request is alive during the ConnectionRequested event only");
    }
}

/// <summary>A client end of a <see cref="LoopbackNetwork"/>.</summary>
public sealed class LoopbackClientTransport : IClientTransport, IChannelCounters
{
    private readonly ChannelTally _tally = new();
    private readonly Queue<TransportDisconnect> _local = new();
    private LoopbackLink _link;
    private long _latencySteps;

    internal LoopbackClientTransport(LoopbackNetwork network, int number)
    {
        Network = network;
        Name = "client" + number;
    }

    internal LoopbackNetwork Network { get; }

    internal string Name { get; }

    /// <summary>
    /// The client's address as the server sees it (M10 slice I, the IP bans): <c>ADDRESS:PORT</c> when a test set
    /// <see cref="Address"/>, otherwise <c>loopback:NAME</c>, which names no IP address.
    /// </summary>
    internal string EndPoint => Address != null ? Address + ":" + (40000 + int.Parse(Name.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture) : "loopback:" + Name;

    /// <summary>The IP address the server sees for this client (null: none); set before connecting.</summary>
    public string Address { get; set; }

    internal Inbox Inbox { get; } = new();

    public ClientTransportState State { get; private set; }

    public int RoundTripMs => (int)(_latencySteps * 2 * 50);

    public long BytesSent => _tally.BytesSent;

    public long BytesReceived => _tally.BytesReceived;

    public long WireBytesSent => _tally.BytesSent;

    public long WireBytesReceived => _tally.BytesReceived;

    public long BytesSentOn(Channel channel) => _tally.BytesSentOn(channel);

    public long BytesReceivedOn(Channel channel) => _tally.BytesReceivedOn(channel);

    public long MessagesSentOn(Channel channel) => _tally.MessagesSentOn(channel);

    public long MessagesReceivedOn(Channel channel) => _tally.MessagesReceivedOn(channel);

    public event Action Connected;
    public event ClientReceivedHandler Received;
    public event Action<TransportDisconnect> Disconnected;

    public void Connect(string host, int port, ReadOnlySpan<byte> hello)
    {
        if (State == ClientTransportState.Connecting || State == ClientTransportState.Connected)
            throw new InvalidOperationException("the transport is connecting or connected already");
        Inbox.Clear();
        _local.Clear();
        State = ClientTransportState.Connecting;
        if (!Network.TryGetServer(port, out LoopbackServerTransport server))
        {
            _link = new LoopbackLink(Network, this, null);
            _link.Schedule(false, EnvelopeKind.ConnectFailed, Network.CurrentStep);
            return;
        }

        _link = new LoopbackLink(Network, this, server);
        _link.Send(true, EnvelopeKind.Request, Channel.Control, hello.ToArray());
    }

    public void Poll()
    {
        while (_local.Count > 0)
            Disconnected?.Invoke(_local.Dequeue());
        while (Inbox.TryTakeDue(Network.CurrentStep, out Envelope envelope))
        {
            if (envelope.Link != _link)
                continue; // left over from an earlier connection of this transport
            switch (envelope.Kind)
            {
                case EnvelopeKind.Accepted:
                    if (State == ClientTransportState.Connecting)
                    {
                        State = ClientTransportState.Connected;
                        Connected?.Invoke();
                    }

                    break;
                case EnvelopeKind.Rejected:
                    if (State == ClientTransportState.Connecting)
                        End(TransportDisconnectKind.Rejected, envelope.Data);
                    break;
                case EnvelopeKind.ConnectFailed:
                    if (State == ClientTransportState.Connecting)
                        End(TransportDisconnectKind.ConnectFailed, envelope.Data);
                    break;
                case EnvelopeKind.Message:
                    if (State == ClientTransportState.Connected && !_link.Dropped)
                    {
                        _tally.Received(envelope.Channel, envelope.Data.Length);
                        _latencySteps = Network.CurrentStep - envelope.Sent;
                        Received?.Invoke(envelope.Channel, envelope.Data);
                    }

                    break;
                case EnvelopeKind.Close:
                    if (State == ClientTransportState.Connected)
                        End(TransportDisconnectKind.RemoteClose, envelope.Data);
                    break;
                case EnvelopeKind.TimedOut:
                    if (State == ClientTransportState.Connected || State == ClientTransportState.Connecting)
                        End(TransportDisconnectKind.Timeout, envelope.Data);
                    break;
            }
        }
    }

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (!Channels.IsValid(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        if (!Channels.IsReliable(channel) && message.Length > Network.Options.MaxUnreliableBytes)
            throw new ArgumentException("an unreliable message of " + message.Length + " bytes is over the limit of " + Network.Options.MaxUnreliableBytes, nameof(message));
        if (State != ClientTransportState.Connected)
            return;
        _tally.Sent(channel, message.Length);
        _link.Send(true, EnvelopeKind.Message, channel, message.ToArray());
    }

    public void Disconnect()
    {
        if (State != ClientTransportState.Connected && State != ClientTransportState.Connecting)
            return;
        State = ClientTransportState.Disconnected;
        _link.ClientClosed = true;
        _link.Send(true, EnvelopeKind.Close, Channel.Control, Array.Empty<byte>());
        _local.Enqueue(new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>()));
    }

    /// <summary>
    /// The client vanishes without a goodbye (a pulled cable, a killed process): nothing is delivered in either direction
    /// from now on, and both ends time out <see cref="LoopbackOptions.TimeoutSteps"/> steps later with
    /// <see cref="TransportDisconnectKind.Timeout"/> (an addition of P2).
    /// </summary>
    public void Drop()
    {
        if (State != ClientTransportState.Connected && State != ClientTransportState.Connecting)
            return;
        long at = Network.CurrentStep + Network.Options.TimeoutSteps;
        LoopbackLink link = _link;
        link.Dropped = true;
        link.Schedule(true, EnvelopeKind.TimedOut, at);
        link.Schedule(false, EnvelopeKind.TimedOut, at);
    }

    public void Dispose()
    {
        if (State == ClientTransportState.Connected || State == ClientTransportState.Connecting)
            Disconnect();
    }

    internal bool IsClosedBeforeAccept(LoopbackLink link) => !ReferenceEquals(link, _link) || link.ClientClosed;

    private void End(TransportDisconnectKind kind, byte[] data)
    {
        State = ClientTransportState.Disconnected;
        Disconnected?.Invoke(new TransportDisconnect(kind, data));
    }
}
