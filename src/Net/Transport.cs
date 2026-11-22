namespace Anomaly.Net;

// Owner: M1 P2 (protocol and transport). Contract by the M1 contracts step (docs/ai/specs/m1-ownership.md); the
// implementations are LiteNetTransport.cs and LoopbackTransport.cs.
//
// Threading (D31): a transport may move bytes on its own threads (LiteNetLib's normal mode starts 2-4), but its events are
// raised only inside Poll(), synchronously, on the thread that calls Poll(). The server host calls Poll() only on its host
// thread, between ticks, so nothing reaches the world from another thread; the host decodes, validates and queues there
// (scope 2e "Where the network meets the tick").

/// <summary>
/// The logical channels of M1 (scope 2e "Channels"). One transport message carries one protocol message.
/// </summary>
public enum Channel : byte
{
    /// <summary>Reliable ordered: InputFrame (client to server), ServerText (server to client); later commands and their replies.</summary>
    Control = 0,

    /// <summary>Reliable ordered, apart from Control so that a large Snapshot never holds commands back: Welcome, MapGrid, Snapshot and the per-tick Deltas.</summary>
    State = 1,

    /// <summary>Unreliable: the client's motion input frames with redundancy (M2) and the server's Motion messages (protocol version 42); a message over the MTU (about 1 KB) is refused here.</summary>
    Input = 2,
}

/// <summary>Constants of <see cref="Channel"/>.</summary>
public static class Channels
{
    /// <summary>The number of channels a transport opens.</summary>
    public const int Count = 3;

    /// <summary>True for the reliable ordered channels (<see cref="Channel.Control"/>, <see cref="Channel.State"/>).</summary>
    public static bool IsReliable(Channel channel) => channel != Channel.Input;

    /// <summary>True for the three channels that exist.</summary>
    public static bool IsValid(Channel channel) => (byte)channel < Count;

    /// <summary>
    /// The largest message of the unreliable <see cref="Channel.Input"/> (LiteNetLib's single-packet size at its starting MTU
    /// of 1024; a bigger unreliable message throws there). <c>Send</c> on <see cref="Channel.Input"/> throws
    /// <see cref="ArgumentException"/> past it, in every transport, so tests and the real network agree.
    /// </summary>
    public const int MaxUnreliableBytes = 1023;

    /// <summary>
    /// The most bytes a reject or disconnect reason may carry: LiteNetLib puts it in one packet (MTU 1024, minus its header).
    /// A longer reason is sent as no reason at all (the connection still closes), so a host's text never throws in the
    /// transport; keep <c>DisconnectInfo</c> texts short (the codec's own cap, <c>Limits.MaxDisconnectTextBytes</c>, is for
    /// decoders).
    /// </summary>
    public const int MaxDisconnectBytes = 1000;
}

/// <summary>Why a connection ended, as the transport saw it (<see cref="TransportDisconnect.Kind"/>).</summary>
public enum TransportDisconnectKind : byte
{
    /// <summary>The remote side closed it (its data, if any, is in <see cref="TransportDisconnect.Data"/>).</summary>
    RemoteClose,

    /// <summary>This side closed it (<see cref="IConnection.Disconnect"/>, <see cref="IClientTransport.Disconnect"/>, a stop).</summary>
    LocalClose,

    /// <summary>No traffic for the transport's timeout (LiteNetLib: 5 s by default).</summary>
    Timeout,

    /// <summary>The server refused the connection request (its reject data is in <see cref="TransportDisconnect.Data"/>).</summary>
    Rejected,

    /// <summary>The connection could not be made (no server, unreachable host).</summary>
    ConnectFailed,
}

/// <summary>
/// How a connection ended: the kind and the bytes the other side attached (a protocol <c>DisconnectInfo</c> message, or
/// empty).
/// </summary>
public readonly record struct TransportDisconnect(TransportDisconnectKind Kind, byte[] Data);

/// <summary>An established connection of a server transport (one per client).</summary>
public interface IConnection
{
    /// <summary>The transport's id of this connection: unique within one transport's run, never reused.</summary>
    int Id { get; }

    /// <summary>The remote end point as text (for logs; D67: not stored).</summary>
    string RemoteEndPoint { get; }

    /// <summary>False once the connection has ended (sends are then dropped).</summary>
    bool IsConnected { get; }

    /// <summary>Queues <paramref name="message"/> on <paramref name="channel"/> (copied before the call returns).</summary>
    void Send(Channel channel, ReadOnlySpan<byte> message);

    /// <summary>Closes the connection, sending <paramref name="reason"/> (a <c>DisconnectInfo</c> message) to the client. <c>Disconnected</c> follows in a later <c>Poll</c>.</summary>
    void Disconnect(ReadOnlySpan<byte> reason);

    /// <summary>Payload bytes sent on this connection (the transport's own counters; protocol overhead excluded).</summary>
    long BytesSent { get; }

    /// <summary>Payload bytes received on this connection.</summary>
    long BytesReceived { get; }

    /// <summary>Messages sent on this connection.</summary>
    long MessagesSent { get; }

    /// <summary>Messages received on this connection.</summary>
    long MessagesReceived { get; }

    /// <summary>The current round-trip time in milliseconds (0 before it is measured).</summary>
    int RoundTripMs { get; }
}

/// <summary>
/// A connection request, alive only during the <see cref="IServerTransport.ConnectionRequested"/> event: the handler
/// answers it with <see cref="Accept"/> or <see cref="Reject"/> before it returns (a request left unanswered is rejected
/// without data). The request carries the client's Hello.
/// </summary>
public interface IConnectionRequest
{
    /// <summary>The request data: an encoded <c>Hello</c>. Valid during the event only.</summary>
    ReadOnlySpan<byte> Data { get; }

    /// <summary>The remote end point as text.</summary>
    string RemoteEndPoint { get; }

    /// <summary>Accepts: the connection exists from now on; <see cref="IServerTransport.Connected"/> follows.</summary>
    IConnection Accept();

    /// <summary>Refuses, sending <paramref name="reason"/> (a <c>DisconnectInfo</c> message) to the client; no connection is made.</summary>
    void Reject(ReadOnlySpan<byte> reason);
}

/// <summary>A received message: valid during the call only (the span is the transport's buffer).</summary>
public delegate void ServerReceivedHandler(IConnection connection, Channel channel, ReadOnlySpan<byte> message);

/// <summary>A received message on a client: valid during the call only.</summary>
public delegate void ClientReceivedHandler(Channel channel, ReadOnlySpan<byte> message);

/// <summary>
/// The server side of a transport. The host starts it, then calls <see cref="Poll"/> once per tick on the host thread
/// (step 2 of the tick, scope 2e) and <see cref="Flush"/> after sending (step 5). Events are raised only inside
/// <see cref="Poll"/>, on the calling thread, in arrival order.
/// </summary>
public interface IServerTransport : IDisposable
{
    /// <summary>Starts listening on <paramref name="bindAddress"/> (for example <c>127.0.0.1</c>) and <paramref name="port"/> (0: an ephemeral port).</summary>
    void Start(string bindAddress, int port);

    /// <summary>The port it listens on (the chosen one after <c>Start(..., 0)</c>).</summary>
    int Port { get; }

    /// <summary>Raises the events of everything that arrived since the last call. Host thread only.</summary>
    void Poll();

    /// <summary>Sends what was queued now instead of at the transport's next update (LiteNetLib <c>TriggerUpdate</c>).</summary>
    void Flush();

    /// <summary>Disconnects every connection (no data) and stops listening.</summary>
    void Stop();

    /// <summary>A client asks to connect; answer it during the event (<see cref="IConnectionRequest"/>).</summary>
    event Action<IConnectionRequest> ConnectionRequested;

    /// <summary>An accepted connection is established.</summary>
    event Action<IConnection> Connected;

    /// <summary>A message arrived on a connection.</summary>
    event ServerReceivedHandler Received;

    /// <summary>A connection ended (any side, any cause); its <see cref="IConnection"/> is dead after this.</summary>
    event Action<IConnection, TransportDisconnect> Disconnected;
}

/// <summary>The state of a <see cref="IClientTransport"/>.</summary>
public enum ClientTransportState : byte
{
    Idle,
    Connecting,
    Connected,
    Disconnected,
}

/// <summary>
/// The client side of a transport (the Godot client, each bot). Events are raised only inside <see cref="Poll"/>, on the
/// calling thread; the Godot client polls in <c>_Process</c> and never blocks on a connect (scope risk 6).
/// </summary>
public interface IClientTransport : IDisposable
{
    /// <summary>Starts connecting to <paramref name="host"/>:<paramref name="port"/> with <paramref name="hello"/> (an encoded <c>Hello</c>) as the request data. Returns at once.</summary>
    void Connect(string host, int port, ReadOnlySpan<byte> hello);

    /// <summary>Does the transport's work and raises the events of what happened since the last call.</summary>
    void Poll();

    /// <summary>Queues <paramref name="message"/> on <paramref name="channel"/>; dropped when not connected.</summary>
    void Send(Channel channel, ReadOnlySpan<byte> message);

    /// <summary>Closes the connection (no data); <see cref="Disconnected"/> follows in a later <see cref="Poll"/>.</summary>
    void Disconnect();

    /// <summary>The current state.</summary>
    ClientTransportState State { get; }

    /// <summary>The current round-trip time in milliseconds.</summary>
    int RoundTripMs { get; }

    /// <summary>Payload bytes sent.</summary>
    long BytesSent { get; }

    /// <summary>Payload bytes received.</summary>
    long BytesReceived { get; }

    /// <summary>The server accepted the connection.</summary>
    event Action Connected;

    /// <summary>A message arrived.</summary>
    event ClientReceivedHandler Received;

    /// <summary>The connection ended or could not be made (<see cref="TransportDisconnectKind.Rejected"/> carries the reject data).</summary>
    event Action<TransportDisconnect> Disconnected;
}
