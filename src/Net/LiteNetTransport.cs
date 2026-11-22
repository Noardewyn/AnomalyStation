using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Anomaly.Protocol;
using LnlDisconnectInfo = LiteNetLib.DisconnectInfo;
using LnlDisconnectReason = LiteNetLib.DisconnectReason;
using ProtocolDisconnectInfo = Anomaly.Protocol.DisconnectInfo;
using ProtocolDisconnectReason = Anomaly.Protocol.DisconnectReason;

namespace Anomaly.Net;

// Owner: M1 P2. Skeletons by the M1 contracts step: the public surface is frozen, the bodies are P2's.
//
// LiteNetLib 2.1.4 facts the implementation relies on (scope 2e, measured): events fire on the thread that calls
// PollEvents (UnsyncedEvents false, the default); Start() adds 2-4 threads, StartInManualMode() none; unreliable and
// sequenced messages over the MTU (1023 B) throw TooBigPacketException, reliable ordered ones are fragmented; the
// NuGet build ignores SimulatePacketLoss and SimulateLatency (use LoopbackNetwork for that); ChannelsCount must be set
// to Channels.Count before Start.
//
// Measured while writing this file: a connection request's Data holds exactly the bytes the client passed; a reject or
// disconnect reason travels in one packet (about 1010 bytes at most, Channels.MaxDisconnectBytes keeps clear of it);
// RejectForce refuses without creating a peer (no state kept for a request flood); a reliable message of more fragments than
// MaxFragmentsCount is dropped by the receiver without blocking the channel; a connect to a closed port ends as
// ConnectionFailed after the connect attempts (about 3 s with the defaults).

/// <summary>
/// The server transport over LiteNetLib in normal mode (its own I/O threads; events only in <see cref="Poll"/>, D31).
/// The request's Hello is read in <c>OnConnectionRequest</c>, so a refusal happens before any peer exists.
/// </summary>
public sealed class LiteNetServerTransport : IServerTransport
{
    /// <summary>
    /// The most fragments of one inbound reliable message (about 1 KB each): clients send messages of at most
    /// <c>Limits.MaxInboundMessageBytes</c> bytes, so a larger one is a protocol violation, and LiteNetLib drops what passes
    /// this before it is reassembled (so a hostile client cannot make the server hold megabytes). LiteNetLib's one knob,
    /// <c>MaxFragmentsCount</c>, limits sends too, so a connection raises it to <see cref="MaxOutboundFragments"/> for the
    /// duration of one send of a message that needs more (a join Snapshot), on the host thread, and puts it back.
    /// </summary>
    public const int MaxInboundFragments = 8;

    /// <summary>LiteNetLib's own maximum of fragments per message (65535, about 64 MB), more than the 16 MiB of <c>Limits.MaxOutboundMessageBytes</c> need.</summary>
    public const int MaxOutboundFragments = ushort.MaxValue;

    /// <summary>The largest message that needs no more than <see cref="MaxInboundFragments"/> fragments at LiteNetLib's MTU (1024 bytes, 31 of them header).</summary>
    internal const int SmallMessageBytes = MaxInboundFragments * 993;

    private readonly Listener _listener;
    private readonly Dictionary<int, LiteNetConnection> _connections = new();
    private readonly Queue<(LiteNetConnection Connection, TransportDisconnect Info)> _deferred = new();
    private readonly RequestView _requestView = new();
    private NetManager _manager;
    private int _port;
    private int _nextConnectionId = 1;
    private int _requestsThisPoll;
    private byte[] _serverFull;

    /// <param name="maxPendingRequests">Requests waiting for an answer at once; more are rejected (<see cref="Limits.MaxPendingConnectionRequests"/>).</param>
    /// <param name="disconnectTimeoutMs">LiteNetLib's <c>DisconnectTimeout</c> (5000 ms by default).</param>
    public LiteNetServerTransport(int maxPendingRequests = Limits.MaxPendingConnectionRequests, int disconnectTimeoutMs = 5000)
    {
        MaxPendingRequests = maxPendingRequests;
        DisconnectTimeoutMs = disconnectTimeoutMs;
        _listener = new Listener(this);
    }

    /// <summary>
    /// The most connection requests raised in one <see cref="Poll"/>: the rest of that poll's requests are rejected with
    /// <see cref="ProtocolDisconnectReason.ServerFull"/> without being raised, so a flood costs the host thread one reject packet
    /// each and nothing else.
    /// </summary>
    public int MaxPendingRequests { get; }

    public int DisconnectTimeoutMs { get; }

    public int Port => _port;

    /// <summary>The connections that are up.</summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>Messages dropped for arriving on a channel with the wrong delivery method, or on a channel that does not exist (a misbehaving client).</summary>
    public long DroppedMessages { get; private set; }

    public event Action<IConnectionRequest> ConnectionRequested;
    public event Action<IConnection> Connected;
    public event ServerReceivedHandler Received;
    public event Action<IConnection, TransportDisconnect> Disconnected;

    public void Start(string bindAddress, int port)
    {
        if (_manager != null)
            throw new InvalidOperationException("the transport is already running");
        if (port < 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        IPAddress v4 = IPAddress.Any;
        IPAddress v6 = IPAddress.IPv6None;
        if (!string.IsNullOrEmpty(bindAddress))
        {
            if (!IPAddress.TryParse(bindAddress, out IPAddress address))
                throw new ArgumentException("not an IP address: " + bindAddress, nameof(bindAddress));
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (!address.Equals(IPAddress.IPv6Any))
                    throw new NotSupportedException("an IPv6 bind address other than :: is not supported in M1");
                v6 = IPAddress.IPv6Any;
            }
            else
            {
                v4 = address;
            }
        }

        var manager = new NetManager(_listener)
        {
            AutoRecycle = true,
            ChannelsCount = (byte)Channels.Count,
            DisconnectTimeout = DisconnectTimeoutMs,
            MaxFragmentsCount = (ushort)MaxInboundFragments,
            EnableStatistics = true,
            DisconnectOnUnreachable = true,
        };
        if (!manager.Start(v4, v6, port))
            throw new InvalidOperationException("cannot listen on " + (bindAddress ?? "any address") + ":" + port + " (in use?)");
        _manager = manager;
        _port = manager.LocalPort;
        _serverFull ??= Codec.Encode(new ProtocolDisconnectInfo(ProtocolDisconnectReason.ServerFull, "too many connection requests"));
    }

    public void Poll()
    {
        _requestsThisPoll = 0;
        _manager?.PollEvents();
        while (_deferred.Count > 0)
        {
            (LiteNetConnection connection, TransportDisconnect info) = _deferred.Dequeue();
            Disconnected?.Invoke(connection, info);
        }
    }

    public void Flush() => _manager?.TriggerUpdate();

    public void Stop()
    {
        NetManager manager = _manager;
        if (manager == null)
            return;
        _manager = null;
        foreach (LiteNetConnection connection in _connections.Values.ToArray())
        {
            connection.MarkClosed();
            _deferred.Enqueue((connection, new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>())));
        }

        _connections.Clear();
        manager.Stop(true);
    }

    public void Dispose() => Stop();

    /// <summary>The LiteNetLib delivery method of a channel: Control and State reliable ordered, Input unreliable.</summary>
    internal static DeliveryMethod DeliveryOf(Channel channel) => Channels.IsReliable(channel) ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

    /// <summary>
    /// The channel of a received message. LiteNetLib carries a channel number on reliable packets only: an unreliable
    /// message always reports channel 0, so by this protocol it is the Input channel's, the only one that is unreliable.
    /// </summary>
    internal static bool TryChannelOf(byte channelNumber, DeliveryMethod method, out Channel channel)
    {
        if (method == DeliveryMethod.Unreliable)
        {
            channel = Channel.Input;
            return true;
        }

        if (method == DeliveryMethod.ReliableOrdered && channelNumber < (byte)Channel.Input)
        {
            channel = (Channel)channelNumber;
            return true;
        }

        channel = default;
        return false;
    }

    internal static TransportDisconnectKind KindOf(LnlDisconnectReason reason) => reason switch
    {
        LnlDisconnectReason.DisconnectPeerCalled => TransportDisconnectKind.LocalClose,
        LnlDisconnectReason.Timeout => TransportDisconnectKind.Timeout,
        LnlDisconnectReason.HostUnreachable => TransportDisconnectKind.Timeout,
        LnlDisconnectReason.NetworkUnreachable => TransportDisconnectKind.Timeout,
        LnlDisconnectReason.ConnectionRejected => TransportDisconnectKind.Rejected,
        _ => TransportDisconnectKind.RemoteClose,
    };

    private void HandleRequest(ConnectionRequest request)
    {
        // Nothing here creates a peer or keeps a reference: the request is answered inside the event.
        if (++_requestsThisPoll > MaxPendingRequests)
        {
            request.RejectForce(_serverFull);
            return;
        }

        Action<IConnectionRequest> handler = ConnectionRequested;
        if (handler == null)
        {
            request.RejectForce();
            return;
        }

        _requestView.Begin(this, request);
        try
        {
            handler(_requestView);
        }
        finally
        {
            if (!_requestView.Answered)
                request.RejectForce();
            _requestView.End();
        }
    }

    private void HandleConnected(NetPeer peer)
    {
        if (peer.Tag is LiteNetConnection connection)
            Connected?.Invoke(connection);
    }

    private void HandleDisconnected(NetPeer peer, LnlDisconnectInfo info)
    {
        if (peer.Tag is not LiteNetConnection connection)
            return;
        peer.Tag = null;
        connection.MarkClosed();
        _connections.Remove(connection.Id);
        if (info.Reason == LnlDisconnectReason.DisconnectPeerCalled)
        {
            // LiteNetLib raises a local close synchronously inside the Disconnect call; events belong to Poll (D31).
            _deferred.Enqueue((connection, new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>())));
            return;
        }

        byte[] data = info.AdditionalData == null || info.AdditionalData.AvailableBytes == 0 ? Array.Empty<byte>() : info.AdditionalData.GetRemainingBytes();
        Disconnected?.Invoke(connection, new TransportDisconnect(KindOf(info.Reason), data));
    }

    private void HandleReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod method)
    {
        if (peer.Tag is not LiteNetConnection connection)
            return;
        if (!TryChannelOf(channelNumber, method, out Channel channel))
        {
            DroppedMessages++;
            return;
        }

        ReadOnlySpan<byte> message = reader.GetRemainingBytesSpan();
        connection.Tally.Received(channel, message.Length);
        Received?.Invoke(connection, channel, message);
    }

    private LiteNetConnection Accept(ConnectionRequest request)
    {
        NetPeer peer = request.Accept();
        if (peer == null)
            throw new InvalidOperationException("LiteNetLib did not create a peer for the request");
        var connection = new LiteNetConnection(peer, _nextConnectionId++);
        peer.Tag = connection;
        _connections.Add(connection.Id, connection);
        return connection;
    }

    private sealed class Listener : INetEventListener
    {
        private readonly LiteNetServerTransport _owner;

        public Listener(LiteNetServerTransport owner)
        {
            _owner = owner;
        }

        public void OnConnectionRequest(ConnectionRequest request) => _owner.HandleRequest(request);

        public void OnPeerConnected(NetPeer peer) => _owner.HandleConnected(peer);

        public void OnPeerDisconnected(NetPeer peer, LnlDisconnectInfo disconnectInfo) => _owner.HandleDisconnected(peer, disconnectInfo);

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) =>
            _owner.HandleReceive(peer, reader, channelNumber, deliveryMethod);

        public void OnMessageDelivered(NetPeer peer, object userData)
        {
        }

        public void OnNetworkError(IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNtpResponse(NtpPacket packet)
        {
        }

        public void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress)
        {
        }
    }

    /// <summary>The one request object the transport hands to the event handler, reused for every request (alive during the event only).</summary>
    private sealed class RequestView : IConnectionRequest
    {
        private LiteNetServerTransport _transport;
        private ConnectionRequest _request;

        public bool Answered { get; private set; }

        public ReadOnlySpan<byte> Data
        {
            get
            {
                ThrowIfDead();
                return _request.Data.GetRemainingBytesSpan();
            }
        }

        public string RemoteEndPoint
        {
            get
            {
                ThrowIfDead();
                return _request.RemoteEndPoint.ToString();
            }
        }

        public void Begin(LiteNetServerTransport transport, ConnectionRequest request)
        {
            _transport = transport;
            _request = request;
            Answered = false;
        }

        public void End()
        {
            _transport = null;
            _request = null;
        }

        public IConnection Accept()
        {
            ThrowIfDead();
            ThrowIfAnswered();
            LiteNetConnection connection = _transport.Accept(_request);
            Answered = true;
            return connection;
        }

        public void Reject(ReadOnlySpan<byte> reason)
        {
            ThrowIfDead();
            ThrowIfAnswered();
            Answered = true;
            if (reason.Length == 0 || reason.Length > Channels.MaxDisconnectBytes)
                _request.RejectForce();
            else
                _request.RejectForce(reason.ToArray());
        }

        private void ThrowIfDead()
        {
            if (_request == null)
                throw new InvalidOperationException("the request is alive during the ConnectionRequested event only");
        }

        private void ThrowIfAnswered()
        {
            if (Answered)
                throw new InvalidOperationException("the request was answered already");
        }
    }
}

/// <summary>A connection of a <see cref="LiteNetServerTransport"/>.</summary>
internal sealed class LiteNetConnection : IConnection, IChannelCounters
{
    private readonly NetPeer _peer;

    public LiteNetConnection(NetPeer peer, int id)
    {
        _peer = peer;
        Id = id;
        IsConnected = true;
    }

    public int Id { get; }

    public string RemoteEndPoint => _peer.ToString();

    public bool IsConnected { get; private set; }

    internal ChannelTally Tally { get; } = new();

    public long BytesSent => Tally.BytesSent;

    public long BytesReceived => Tally.BytesReceived;

    public long MessagesSent => Tally.MessagesSent;

    public long MessagesReceived => Tally.MessagesReceived;

    public int RoundTripMs => _peer.RoundTripTime;

    public long WireBytesSent => _peer.Statistics.BytesSent;

    public long WireBytesReceived => _peer.Statistics.BytesReceived;

    public long BytesSentOn(Channel channel) => Tally.BytesSentOn(channel);

    public long BytesReceivedOn(Channel channel) => Tally.BytesReceivedOn(channel);

    public long MessagesSentOn(Channel channel) => Tally.MessagesSentOn(channel);

    public long MessagesReceivedOn(Channel channel) => Tally.MessagesReceivedOn(channel);

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (!Channels.IsValid(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        if (!Channels.IsReliable(channel) && message.Length > Channels.MaxUnreliableBytes)
            throw new ArgumentException("an unreliable message of " + message.Length + " bytes is over the limit of " + Channels.MaxUnreliableBytes, nameof(message));
        if (!IsConnected)
            return;
        if (message.Length > LiteNetServerTransport.SmallMessageBytes)
        {
            // One knob limits both directions (see MaxInboundFragments): widen it for this send only.
            NetManager manager = _peer.NetManager as NetManager;
            ushort before = manager.MaxFragmentsCount;
            manager.MaxFragmentsCount = (ushort)LiteNetServerTransport.MaxOutboundFragments;
            try
            {
                _peer.Send(message, (byte)channel, LiteNetServerTransport.DeliveryOf(channel));
            }
            finally
            {
                manager.MaxFragmentsCount = before;
            }
        }
        else
        {
            _peer.Send(message, (byte)channel, LiteNetServerTransport.DeliveryOf(channel));
        }

        Tally.Sent(channel, message.Length);
    }

    public void Disconnect(ReadOnlySpan<byte> reason)
    {
        if (!IsConnected)
            return;
        IsConnected = false;
        if (reason.Length == 0 || reason.Length > Channels.MaxDisconnectBytes)
            _peer.Disconnect();
        else
            _peer.Disconnect(reason.ToArray());
    }

    internal void MarkClosed() => IsConnected = false;
}

/// <summary>
/// The client transport over LiteNetLib. Manual mode (no threads; <see cref="Poll"/> runs LiteNetLib's update with the
/// time elapsed since the last call) for the bots, 150 in one process (scope risk 5); normal mode for the Godot client.
/// <para>
/// A host name is resolved on a pool thread and never on the caller's, so <see cref="Connect"/> returns at once and a slow
/// or failing lookup never blocks the Godot main thread; an IP literal and <c>localhost</c> need no lookup. The transport
/// can connect again after a <see cref="Disconnected"/> (the bots' reconnect with a token).
/// </para>
/// </summary>
public sealed class LiteNetClientTransport : IClientTransport, IChannelCounters
{
    private readonly Listener _listener;
    private readonly ChannelTally _tally = new();
    private NetManager _manager;
    private NetPeer _peer;
    private Task<IPAddress[]> _lookup;
    private byte[] _hello;
    private int _port;
    private ClientTransportState _state;
    private bool _closing;
    private bool _ended;
    private bool _stopAfterPoll;
    private long _lastPollTimestamp;
    private long _wireSentBefore;
    private long _wireReceivedBefore;

    /// <param name="manualMode">True: LiteNetLib's manual mode, driven by <see cref="Poll"/>; false: its own threads.</param>
    /// <param name="disconnectTimeoutMs">LiteNetLib's <c>DisconnectTimeout</c>.</param>
    public LiteNetClientTransport(bool manualMode, int disconnectTimeoutMs = 5000)
    {
        ManualMode = manualMode;
        DisconnectTimeoutMs = disconnectTimeoutMs;
        _listener = new Listener(this);
    }

    public bool ManualMode { get; }

    public int DisconnectTimeoutMs { get; }

    /// <summary>LiteNetLib's <c>MaxConnectAttempts</c> for the next <see cref="Connect"/> (10 by default; a connect that finds nobody fails after this many tries, <see cref="ReconnectDelayMs"/> apart).</summary>
    public int MaxConnectAttempts { get; set; } = 10;

    /// <summary>LiteNetLib's <c>ReconnectDelay</c> in milliseconds for the next <see cref="Connect"/> (500 by default).</summary>
    public int ReconnectDelayMs { get; set; } = 500;

    public ClientTransportState State => _state;

    public int RoundTripMs => _peer?.RoundTripTime ?? 0;

    public long BytesSent => _tally.BytesSent;

    public long BytesReceived => _tally.BytesReceived;

    public long WireBytesSent => _wireSentBefore + (_manager?.Statistics.BytesSent ?? 0);

    public long WireBytesReceived => _wireReceivedBefore + (_manager?.Statistics.BytesReceived ?? 0);

    public long BytesSentOn(Channel channel) => _tally.BytesSentOn(channel);

    public long BytesReceivedOn(Channel channel) => _tally.BytesReceivedOn(channel);

    public long MessagesSentOn(Channel channel) => _tally.MessagesSentOn(channel);

    public long MessagesReceivedOn(Channel channel) => _tally.MessagesReceivedOn(channel);

    public event Action Connected;
    public event ClientReceivedHandler Received;
    public event Action<TransportDisconnect> Disconnected;

    public void Connect(string host, int port, ReadOnlySpan<byte> hello)
    {
        if (_state == ClientTransportState.Connecting || _state == ClientTransportState.Connected)
            throw new InvalidOperationException("the transport is connecting or connected already");
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("no host", nameof(host));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        StopManager();
        _hello = hello.ToArray();
        _port = port;
        _closing = false;
        _ended = false;
        _stopAfterPoll = false;
        _state = ClientTransportState.Connecting;
        if (IPAddress.TryParse(host, out IPAddress address))
        {
            StartConnect(address);
        }
        else if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            StartConnect(IPAddress.Loopback);
        }
        else
        {
            _lookup = Task.Run(() => Dns.GetHostAddresses(host));
        }
    }

    public void Poll()
    {
        if (_lookup != null && _lookup.IsCompleted)
        {
            Task<IPAddress[]> lookup = _lookup;
            _lookup = null;
            IPAddress[] addresses = lookup.IsCompletedSuccessfully ? lookup.Result : null;
            if (!_closing && !_ended)
            {
                IPAddress address = addresses?.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? addresses?.FirstOrDefault();
                if (address == null)
                    End(TransportDisconnectKind.ConnectFailed, Array.Empty<byte>());
                else
                    StartConnect(address);
            }
        }

        NetManager manager = _manager;
        if (manager != null)
        {
            if (ManualMode)
            {
                long now = Stopwatch.GetTimestamp();
                float elapsedMs = (float)((now - _lastPollTimestamp) * 1000.0 / Stopwatch.Frequency);
                _lastPollTimestamp = now;
                manager.ManualUpdate(elapsedMs);
            }

            manager.PollEvents();
        }

        if (_closing && !_ended)
            End(TransportDisconnectKind.LocalClose, Array.Empty<byte>());
        if (_stopAfterPoll)
        {
            _stopAfterPoll = false;
            StopManager();
        }
    }

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (!Channels.IsValid(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        if (!Channels.IsReliable(channel) && message.Length > Channels.MaxUnreliableBytes)
            throw new ArgumentException("an unreliable message of " + message.Length + " bytes is over the limit of " + Channels.MaxUnreliableBytes, nameof(message));
        if (_state != ClientTransportState.Connected || _peer == null)
            return;
        _peer.Send(message, (byte)channel, LiteNetServerTransport.DeliveryOf(channel));
        _tally.Sent(channel, message.Length);
    }

    public void Disconnect()
    {
        if (_state != ClientTransportState.Connecting && _state != ClientTransportState.Connected)
            return;
        _state = ClientTransportState.Disconnected;
        _closing = true;
        _peer?.Disconnect();
    }

    public void Dispose()
    {
        _closing = true;
        _ended = true;
        _state = ClientTransportState.Disconnected;
        StopManager();
    }

    private void StartConnect(IPAddress address)
    {
        var manager = new NetManager(_listener)
        {
            AutoRecycle = true,
            ChannelsCount = (byte)Channels.Count,
            DisconnectTimeout = DisconnectTimeoutMs,
            MaxConnectAttempts = MaxConnectAttempts,
            ReconnectDelay = ReconnectDelayMs,
            DisconnectOnUnreachable = true,
            EnableStatistics = true,
        };
        bool v6 = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        IPAddress bind6 = v6 ? IPAddress.IPv6Any : IPAddress.IPv6None;
        bool started = ManualMode ? manager.StartInManualMode(IPAddress.Any, bind6, 0) : manager.Start(IPAddress.Any, bind6, 0);
        if (!started)
        {
            End(TransportDisconnectKind.ConnectFailed, Array.Empty<byte>());
            return;
        }

        _manager = manager;
        _lastPollTimestamp = Stopwatch.GetTimestamp();
        _peer = manager.Connect(new IPEndPoint(address, _port), _hello);
    }

    private void StopManager()
    {
        NetManager manager = _manager;
        _manager = null;
        _peer = null;
        _lookup = null;
        if (manager == null)
            return;
        _wireSentBefore += manager.Statistics.BytesSent;
        _wireReceivedBefore += manager.Statistics.BytesReceived;
        manager.Stop(true);
    }

    private void End(TransportDisconnectKind kind, byte[] data)
    {
        if (_ended)
            return;
        _ended = true;
        _state = ClientTransportState.Disconnected;
        _stopAfterPoll = true;
        Disconnected?.Invoke(new TransportDisconnect(kind, data));
    }

    private void HandleConnected(NetPeer peer)
    {
        if (peer != _peer || _closing || _ended)
            return;
        _state = ClientTransportState.Connected;
        Connected?.Invoke();
    }

    private void HandleDisconnected(NetPeer peer, LnlDisconnectInfo info)
    {
        if (peer != _peer || _ended)
            return;
        if (info.Reason == LnlDisconnectReason.DisconnectPeerCalled)
            return; // raised synchronously inside Disconnect(); Poll reports the local close (events belong to Poll, D31)
        byte[] data = info.AdditionalData == null || info.AdditionalData.AvailableBytes == 0 ? Array.Empty<byte>() : info.AdditionalData.GetRemainingBytes();
        TransportDisconnectKind kind = LiteNetServerTransport.KindOf(info.Reason);
        if (_state == ClientTransportState.Connecting && kind != TransportDisconnectKind.Rejected && kind != TransportDisconnectKind.LocalClose)
            kind = TransportDisconnectKind.ConnectFailed;
        if (_closing)
            kind = TransportDisconnectKind.LocalClose;
        End(kind, data);
    }

    private void HandleReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod method)
    {
        if (peer != _peer || _state != ClientTransportState.Connected)
            return;
        if (!LiteNetServerTransport.TryChannelOf(channelNumber, method, out Channel channel))
            return;
        ReadOnlySpan<byte> message = reader.GetRemainingBytesSpan();
        _tally.Received(channel, message.Length);
        Received?.Invoke(channel, message);
    }

    private sealed class Listener : INetEventListener
    {
        private readonly LiteNetClientTransport _owner;

        public Listener(LiteNetClientTransport owner)
        {
            _owner = owner;
        }

        public void OnPeerConnected(NetPeer peer) => _owner.HandleConnected(peer);

        public void OnPeerDisconnected(NetPeer peer, LnlDisconnectInfo disconnectInfo) => _owner.HandleDisconnected(peer, disconnectInfo);

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) =>
            _owner.HandleReceive(peer, reader, channelNumber, deliveryMethod);

        public void OnConnectionRequest(ConnectionRequest request) => request.RejectForce();

        public void OnMessageDelivered(NetPeer peer, object userData)
        {
        }

        public void OnNetworkError(IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNtpResponse(NtpPacket packet)
        {
        }

        public void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress)
        {
        }
    }
}
