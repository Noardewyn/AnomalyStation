using System.Diagnostics;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Net;

/// <summary>
/// The LiteNetLib transports over real UDP sockets on 127.0.0.1 with an ephemeral port: a server in normal mode and clients
/// in manual mode (and one in normal mode). Each test pumps until its condition holds, within a deadline, so a slow machine
/// slows a test down and never fails it for being a few milliseconds late.
/// </summary>
public class LiteNetSocketTests
{
    private const int DeadlineMs = 8000;

    private sealed class Host : IDisposable
    {
        public readonly LiteNetServerTransport Server;
        public readonly List<IConnection> Connections = new();
        public readonly List<(IConnection Connection, Channel Channel, byte[] Data)> Received = new();
        public readonly List<(IConnection Connection, TransportDisconnect Info)> Ended = new();
        public readonly HashSet<int> EventThreads = new();
        public int Requests;

        public Host(int maxPendingRequests = Limits.MaxPendingConnectionRequests, int disconnectTimeoutMs = 5000)
        {
            Server = new LiteNetServerTransport(maxPendingRequests, disconnectTimeoutMs);
            Server.ConnectionRequested += OnRequest;
            Server.Connected += c =>
            {
                EventThreads.Add(Environment.CurrentManagedThreadId);
                Connections.Add(c);
            };
            Server.Received += (c, ch, m) =>
            {
                EventThreads.Add(Environment.CurrentManagedThreadId);
                Received.Add((c, ch, m.ToArray()));
            };
            Server.Disconnected += (c, info) =>
            {
                EventThreads.Add(Environment.CurrentManagedThreadId);
                Ended.Add((c, info));
            };
            Server.Start("127.0.0.1", 0);
        }

        // What the server host does with a Hello (P3's job; the transport only hands the bytes over).
        private void OnRequest(IConnectionRequest request)
        {
            EventThreads.Add(Environment.CurrentManagedThreadId);
            Requests++;
            if (!Codec.TryDecode(request.Data, out Hello hello, out _))
                request.Reject(Codec.Encode(new DisconnectInfo(DisconnectReason.BadHello, "no Hello")));
            else if (hello.Protocol != ProtocolVersion.Current)
                request.Reject(Codec.Encode(new DisconnectInfo(DisconnectReason.WrongVersion, "server speaks " + ProtocolVersion.Current)));
            else if (!NameRules.IsValid(hello.Name))
                request.Reject(Codec.Encode(new DisconnectInfo(DisconnectReason.InvalidName, "bad name")));
            else
                request.Accept();
        }

        public void Dispose() => Server.Dispose();
    }

    private sealed class Peer : IDisposable
    {
        public readonly LiteNetClientTransport Transport;
        public readonly List<(Channel Channel, byte[] Data)> Received = new();
        public readonly List<TransportDisconnect> Ended = new();
        public int ConnectedEvents;

        public Peer(bool manual = true, int disconnectTimeoutMs = 5000)
        {
            Transport = new LiteNetClientTransport(manual, disconnectTimeoutMs);
            Transport.Connected += () => ConnectedEvents++;
            Transport.Received += (ch, m) => Received.Add((ch, m.ToArray()));
            Transport.Disconnected += d => Ended.Add(d);
        }

        public void Connect(Host host, string name = "Alice", ushort version = ProtocolVersion.Current, string address = "127.0.0.1") =>
            Transport.Connect(address, host.Server.Port, Codec.Encode(new Hello(version, name, SessionToken.None)));

        public void Dispose() => Transport.Dispose();
    }

    private static void Pump(Host host, Peer[] peers, Func<bool> done, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(clock.ElapsedMilliseconds < DeadlineMs, "timed out waiting for " + what + " (" + string.Join(", ", peers.Select(p => p.Transport.State + " ended " + p.Ended.Count)) + (host == null ? "" : "; host ended " + host.Ended.Count + " connections " + host.Connections.Count) + ")");
            host?.Server.Poll();
            foreach (Peer p in peers)
                p.Transport.Poll();
            Thread.Sleep(1);
        }
    }

    private static void Pump(Host host, Peer peer, Func<bool> done, string what) => Pump(host, new[] { peer }, done, what);

    private static DisconnectInfo Info(TransportDisconnect end)
    {
        Assert.True(Codec.TryDecode(end.Data, out DisconnectInfo info, out ProtocolError error), "the reject data is a Disconnect message: " + error);
        return info;
    }

    [Fact]
    public void TwoManualClientsConnectExchangeMessagesInBothDirectionsAndDisconnect()
    {
        using var host = new Host();
        using var a = new Peer();
        using var b = new Peer();
        a.Connect(host, "Alice");
        b.Connect(host, "Bob");
        Assert.Equal(ClientTransportState.Connecting, a.Transport.State);
        Pump(host, new[] { a, b }, () => host.Connections.Count == 2 && a.ConnectedEvents == 1 && b.ConnectedEvents == 1, "two connections");
        Assert.Equal(ClientTransportState.Connected, a.Transport.State);
        Assert.Equal(2, host.Server.ConnectionCount);
        Assert.NotEqual(host.Connections[0].Id, host.Connections[1].Id);
        Assert.All(host.Connections, c => Assert.True(c.IsConnected));

        // Client to server on the Control channel: an InputFrame, byte for byte.
        byte[] frame = Codec.Encode(new InputFrame(1, HeldDirs.North | HeldDirs.East));
        a.Transport.Send(Channel.Control, frame);
        Pump(host, new[] { a, b }, () => host.Received.Count == 1, "an InputFrame");
        Assert.Equal(Channel.Control, host.Received[0].Channel);
        Assert.Equal(frame, host.Received[0].Data);
        IConnection fromA = host.Received[0].Connection;

        // Server to client on both reliable channels, in order, and a large message that LiteNetLib fragments.
        byte[] welcome = Codec.Encode(new Welcome(1, new SessionToken(3, 4), 12, 20, 77));
        byte[] text = Codec.Encode(new ServerText("chat", "hello"));
        var big = new byte[300_000];
        new Random(5).NextBytes(big);
        fromA.Send(Channel.State, welcome);
        fromA.Send(Channel.Control, text);
        fromA.Send(Channel.State, big);
        fromA.Send(Channel.State, welcome);
        Pump(host, new[] { a, b }, () => a.Received.Count == 4, "four server messages");
        Assert.Equal(new[] { Channel.State, Channel.State, Channel.State, Channel.Control }, a.Received.Select(r => r.Channel).OrderBy(c => c == Channel.Control).ToArray());
        Assert.Equal(new[] { welcome, big, welcome }, a.Received.Where(r => r.Channel == Channel.State).Select(r => r.Data).ToArray());
        Assert.Equal(text, a.Received.Single(r => r.Channel == Channel.Control).Data);
        Assert.Empty(b.Received);

        // The unreliable Input channel in both directions (localhost: most of 30 get through).
        for (int i = 0; i < 30; i++)
        {
            a.Transport.Send(Channel.Input, new byte[] { (byte)i, 1, 2, 3 });
            fromA.Send(Channel.Input, new byte[] { (byte)i, 9 });
        }

        Pump(host, new[] { a, b }, () => host.Received.Count(r => r.Channel == Channel.Input) >= 20 && a.Received.Count(r => r.Channel == Channel.Input) >= 20, "unreliable messages");
        Assert.Throws<ArgumentException>(() => a.Transport.Send(Channel.Input, new byte[Channels.MaxUnreliableBytes + 1]));
        Assert.Throws<ArgumentException>(() => fromA.Send(Channel.Input, new byte[Channels.MaxUnreliableBytes + 1]));

        // Counters: the payload per channel, the transport's own bytes on top, and a round-trip time once measured.
        var counters = (IChannelCounters)fromA;
        Assert.Equal(frame.Length, counters.BytesReceivedOn(Channel.Control));
        Assert.Equal(welcome.Length * 2 + big.Length, counters.BytesSentOn(Channel.State));
        Assert.Equal(text.Length, counters.BytesSentOn(Channel.Control));
        Assert.True(counters.WireBytesSent > counters.BytesSentOn(Channel.State), "wire bytes include the headers");
        Assert.True(counters.WireBytesReceived > 0);
        Assert.Equal(fromA.BytesSent, new[] { Channel.Control, Channel.State, Channel.Input }.Sum(c => counters.BytesSentOn(c)));
        Assert.True(a.Transport.BytesSent >= frame.Length);
        Assert.True(a.Transport.BytesReceived >= big.Length);
        Assert.True(((IChannelCounters)a.Transport).WireBytesReceived > big.Length);
        Assert.True(a.Transport.RoundTripMs >= 0);

        // A client leaves: a local close here, a remote close there.
        a.Transport.Disconnect();
        Assert.Equal(ClientTransportState.Disconnected, a.Transport.State);
        a.Transport.Send(Channel.Control, frame); // dropped
        Pump(host, new[] { a, b }, () => a.Ended.Count == 1 && host.Ended.Count == 1, "a client's disconnect");
        Assert.Equal(TransportDisconnectKind.LocalClose, a.Ended[0].Kind);
        Assert.Equal(TransportDisconnectKind.RemoteClose, host.Ended[0].Info.Kind);
        Assert.Same(fromA, host.Ended[0].Connection);
        Assert.False(fromA.IsConnected);
        Assert.Equal(1, host.Server.ConnectionCount);
        fromA.Send(Channel.State, welcome); // a dead connection drops sends

        // Every event of the transports came on this thread, from Poll.
        Assert.Equal(new[] { Environment.CurrentManagedThreadId }, host.EventThreads.ToArray());
    }

    [Fact]
    public void ANormalModeClientConnectsLikeTheGodotClientDoes()
    {
        using var host = new Host();
        using var godot = new Peer(manual: false);
        var threads = new HashSet<int>();
        godot.Transport.Connected += () => threads.Add(Environment.CurrentManagedThreadId);
        godot.Transport.Received += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        godot.Connect(host, "Godot", address: "localhost");
        Pump(host, godot, () => godot.ConnectedEvents == 1 && host.Connections.Count == 1, "a normal-mode connection");
        host.Connections[0].Send(Channel.State, new byte[] { 1, 2, 3 });
        host.Server.Flush();
        Pump(host, godot, () => godot.Received.Count == 1, "a message");
        godot.Transport.Send(Channel.Control, new byte[] { 4 });
        Pump(host, godot, () => host.Received.Count == 1, "a message to the server");
        Assert.Equal(new[] { Environment.CurrentManagedThreadId }, threads.ToArray());
    }

    [Fact]
    public void AHelloOfAnotherVersionIsRejectedWithItsReason()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host, version: ProtocolVersion.Current + 1);
        Pump(host, peer, () => peer.Ended.Count == 1, "the rejection");
        Assert.Equal(TransportDisconnectKind.Rejected, peer.Ended[0].Kind);
        Assert.Equal(DisconnectReason.WrongVersion, Info(peer.Ended[0]).Reason);
        Assert.Equal(ClientTransportState.Disconnected, peer.Transport.State);
        Assert.Equal(0, peer.ConnectedEvents);
        Assert.Empty(host.Connections);
        Assert.Equal(0, host.Server.ConnectionCount);
        Assert.Empty(host.Ended);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tab\there")]
    [InlineData("0123456789012345678901234567890123456789012")] // 43 characters
    [InlineData("nul\0name")]
    public void ABadNameIsRejectedWithInvalidName(string name)
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host, name);
        Pump(host, peer, () => peer.Ended.Count == 1, "the rejection");
        Assert.Equal(TransportDisconnectKind.Rejected, peer.Ended[0].Kind);
        Assert.Equal(DisconnectReason.InvalidName, Info(peer.Ended[0]).Reason);
        Assert.Empty(host.Connections);
    }

    [Fact]
    public void ARequestWithoutAHelloIsRejectedWithBadHello()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Transport.Connect("127.0.0.1", host.Server.Port, new byte[] { 1, 2, 3, 4, 5 });
        Pump(host, peer, () => peer.Ended.Count == 1, "the rejection");
        Assert.Equal(DisconnectReason.BadHello, Info(peer.Ended[0]).Reason);
        using var empty = new Peer();
        empty.Transport.Connect("127.0.0.1", host.Server.Port, ReadOnlySpan<byte>.Empty);
        Pump(host, empty, () => empty.Ended.Count == 1, "the rejection of an empty request");
        Assert.Equal(DisconnectReason.BadHello, Info(empty.Ended[0]).Reason);
        Assert.Empty(host.Connections);
    }

    [Fact]
    public void TheLongestHelloCrossesRealSockets()
    {
        // version 85 (D335): a Hello with the longest name, a reconnect token and the longest login token, 964 bytes in the connection request
        using var host = new Host();
        using var peer = new Peer();
        string name = string.Concat(Enumerable.Repeat("\U0001F600", Limits.MaxNameChars));
        byte[] hello = Codec.Encode(new Hello(ProtocolVersion.Current, name, new SessionToken(5, 6), new string('t', Limits.MaxLoginTokenBytes)));
        Assert.Equal(Codec.MaxHelloBytes, hello.Length);
        peer.Transport.Connect("127.0.0.1", host.Server.Port, hello);
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "the longest Hello's accept");
        Assert.Empty(peer.Ended); // the host refuses a Hello that does not decode whole
    }

    [Fact]
    public void ARejectedClientCanTryAgainWithAGoodHello()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host, "");
        Pump(host, peer, () => peer.Ended.Count == 1, "the rejection");
        peer.Connect(host, "Alice");
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "the second try");
        Assert.Equal(ClientTransportState.Connected, peer.Transport.State);
        Assert.Single(host.Connections);
    }

    [Fact]
    public void AServerDisconnectCarriesItsReasonToTheClient()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host);
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "a connection");
        IConnection connection = host.Connections[0];
        connection.Send(Channel.Control, Codec.Encode(new ServerText("alert", "kicked for flooding")));
        connection.Disconnect(Codec.Encode(new DisconnectInfo(DisconnectReason.Flooding, "too many inputs")));
        Assert.False(connection.IsConnected);
        Pump(host, peer, () => peer.Ended.Count == 1 && host.Ended.Count == 1, "the disconnect");
        // The text sent before the disconnect arrives first when the connection still carries it.
        Assert.Equal(TransportDisconnectKind.RemoteClose, peer.Ended[0].Kind);
        Assert.Equal(new DisconnectInfo(DisconnectReason.Flooding, "too many inputs"), Info(peer.Ended[0]));
        Assert.Equal(TransportDisconnectKind.LocalClose, host.Ended[0].Info.Kind);
        Assert.Equal(ClientTransportState.Disconnected, peer.Transport.State);
        Assert.Equal(0, host.Server.ConnectionCount);
    }

    [Fact]
    public void ADisconnectReasonTooLargeForOnePacketStillClosesTheConnection()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host);
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "a connection");
        host.Connections[0].Disconnect(new byte[Channels.MaxDisconnectBytes + 1]);
        Pump(host, peer, () => peer.Ended.Count == 1, "the disconnect");
        Assert.Equal(TransportDisconnectKind.RemoteClose, peer.Ended[0].Kind);
        Assert.Empty(peer.Ended[0].Data);
    }

    [Fact]
    public void AClientThatStopsTalkingTimesOutOnTheServer()
    {
        using var host = new Host(disconnectTimeoutMs: 600);
        using var peer = new Peer();
        peer.Connect(host);
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "a connection");
        // The manual-mode client is simply not polled any more: no pings, no acks.
        var clock = Stopwatch.StartNew();
        Pump(host, Array.Empty<Peer>(), () => host.Ended.Count == 1, "a timeout");
        Assert.Equal(TransportDisconnectKind.Timeout, host.Ended[0].Info.Kind);
        Assert.InRange(clock.ElapsedMilliseconds, 300, 6000);
        Assert.False(host.Connections[0].IsConnected);
        Assert.Equal(0, host.Server.ConnectionCount);
    }

    [Fact]
    public void ConnectingToAClosedPortFails()
    {
        using var peer = new Peer();
        peer.Transport.MaxConnectAttempts = 2;
        peer.Transport.ReconnectDelayMs = 100;
        // A port nobody listens on: bind and free one.
        int port;
        using (var probe = new Host())
            port = probe.Server.Port;
        peer.Transport.Connect("127.0.0.1", port, Codec.Encode(new Hello(ProtocolVersion.Current, "Alice", SessionToken.None)));
        Pump(null, peer, () => peer.Ended.Count == 1, "a failed connect");
        Assert.Equal(TransportDisconnectKind.ConnectFailed, peer.Ended[0].Kind);
        Assert.Equal(ClientTransportState.Disconnected, peer.Transport.State);
        Assert.Equal(0, peer.ConnectedEvents);
    }

    [Fact]
    public void AHostNameIsResolvedOffTheCallingThread()
    {
        using var peer = new Peer();
        var clock = Stopwatch.StartNew();
        peer.Transport.Connect("host-that-does-not-exist.invalid", 12345, new byte[] { 1 });
        Assert.True(clock.ElapsedMilliseconds < 500, "Connect blocked for " + clock.ElapsedMilliseconds + " ms");
        Assert.Equal(ClientTransportState.Connecting, peer.Transport.State);
        peer.Transport.Disconnect();
        peer.Transport.Poll();
        Assert.Equal(TransportDisconnectKind.LocalClose, Assert.Single(peer.Ended).Kind);
        Assert.Throws<ArgumentException>(() => peer.Transport.Connect("", 1, new byte[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => peer.Transport.Connect("127.0.0.1", 0, new byte[] { 1 }));
    }

    [Fact]
    public void ARequestFloodIsCappedPerPollAndTheRestAreToldTheServerIsFull()
    {
        using var host = new Host(maxPendingRequests: 3);
        var peers = Enumerable.Range(0, 10).Select(i => new Peer()).ToArray();
        try
        {
            foreach (Peer p in peers)
            {
                p.Transport.ReconnectDelayMs = 60_000; // no second wave of requests while the test runs
                p.Connect(host, "Flood");
            }

            // Only the clients run for a moment, so every request is queued in LiteNetLib before the host polls once.
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 300)
            {
                foreach (Peer p in peers)
                    p.Transport.Poll();
                Thread.Sleep(2);
            }

            host.Server.Poll();
            Assert.Equal(3, host.Requests);
            Pump(host, peers, () => peers.Count(p => p.Ended.Count > 0) == 7 && peers.Count(p => p.ConnectedEvents == 1) == 3, "the flood to settle");
            foreach (Peer p in peers.Where(p => p.Ended.Count > 0))
            {
                Assert.Equal(TransportDisconnectKind.Rejected, p.Ended[0].Kind);
                Assert.Equal(DisconnectReason.ServerFull, Info(p.Ended[0]).Reason);
            }
        }
        finally
        {
            foreach (Peer p in peers)
                p.Dispose();
        }
    }

    [Fact]
    public void AnOversizedMessageIsDroppedByTheTransportOrReachesTheHostForAStrike()
    {
        using var host = new Host();
        using var peer = new Peer();
        peer.Connect(host);
        Pump(host, peer, () => peer.ConnectedEvents == 1 && host.Connections.Count == 1, "a connection");
        // 2000 bytes: more than a client may send (256) but within the fragment cap, so the host sees its length.
        peer.Transport.Send(Channel.Control, new byte[2000]);
        // 40 KB: past the fragment cap, dropped before it is reassembled; the channel carries on.
        peer.Transport.Send(Channel.Control, new byte[40_000]);
        peer.Transport.Send(Channel.Control, new byte[] { 9, 9 });
        Pump(host, peer, () => host.Received.Count >= 2, "the messages after the oversized ones");
        var settle = Stopwatch.StartNew();
        Pump(host, peer, () => settle.ElapsedMilliseconds > 150, "a settle");
        Assert.Equal(new[] { 2000, 2 }, host.Received.Select(r => r.Data.Length).ToArray());
        Assert.True(host.Connections[0].IsConnected);
    }

    [Fact]
    public void AClientTransportConnectsAgainAfterItEnded()
    {
        using var host = new Host();
        using var peer = new Peer();
        for (int round = 0; round < 3; round++)
        {
            peer.Connect(host, "Alice");
            Assert.Throws<InvalidOperationException>(() => peer.Connect(host, "Alice"));
            int connected = peer.ConnectedEvents;
            Pump(host, peer, () => peer.ConnectedEvents == connected + 1 && host.Connections.Count == round + 1, "a connection, round " + round);
            peer.Transport.Disconnect();
            int ended = peer.Ended.Count;
            Pump(host, peer, () => peer.Ended.Count == ended + 1 && host.Ended.Count == round + 1, "a disconnect, round " + round);
        }

        Assert.Equal(3, host.Connections.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void StoppingTheServerClosesEveryClientAndRaisesTheLocalClosesAtTheNextPoll()
    {
        var host = new Host();
        using var a = new Peer();
        using var b = new Peer();
        a.Connect(host, "Alice");
        b.Connect(host, "Bob");
        Pump(host, new[] { a, b }, () => host.Connections.Count == 2 && a.ConnectedEvents == 1 && b.ConnectedEvents == 1, "two connections");
        host.Server.Stop();
        Assert.Empty(host.Ended); // events only in Poll
        host.Server.Poll();
        Assert.Equal(2, host.Ended.Count);
        Assert.All(host.Ended, e => Assert.Equal(TransportDisconnectKind.LocalClose, e.Info.Kind));
        Pump(null, new[] { a, b }, () => a.Ended.Count == 1 && b.Ended.Count == 1, "the clients to notice");
        Assert.All(new[] { a, b }, p => Assert.Equal(TransportDisconnectKind.RemoteClose, p.Ended[0].Kind));
        host.Dispose();
        host.Server.Poll();
    }

    [Fact]
    public void AServerStartsOnAFixedPortRefusesABusyOneAndRejectsBadAddresses()
    {
        using var first = new Host();
        var second = new LiteNetServerTransport();
        Assert.Throws<InvalidOperationException>(() => second.Start("127.0.0.1", first.Server.Port));
        Assert.Throws<ArgumentException>(() => second.Start("not an address", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => second.Start("127.0.0.1", 70000));
        Assert.Throws<InvalidOperationException>(() => first.Server.Start("127.0.0.1", 0));
        second.Start(null, 0);
        Assert.NotEqual(first.Server.Port, second.Port);
        second.Dispose();
    }
}
