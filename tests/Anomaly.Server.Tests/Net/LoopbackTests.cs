using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Net;

/// <summary>The in-memory transport: latency in steps, loss and reorder on the unreliable channel only, determinism for a seed.</summary>
public class LoopbackTests
{
    /// <summary>A loopback server with a host stand-in that accepts every Hello, plus the clients a test creates.</summary>
    private sealed class Harness
    {
        public readonly LoopbackNetwork Network;
        public readonly LoopbackServerTransport Server;
        public readonly List<string> Log = new();
        public readonly List<IConnection> Connections = new();
        public readonly List<(int Connection, Channel Channel, byte[] Data)> ServerReceived = new();
        public readonly List<(Channel Channel, byte[] Data)> ClientReceived = new();
        public readonly List<TransportDisconnect> ClientDisconnects = new();
        public readonly List<(int Connection, TransportDisconnect Info)> ServerDisconnects = new();
        public int Requests;
        public Func<byte[], byte[]> Judge = hello => null; // null: accept; else the reject reason

        public Harness(LoopbackOptions options = null, bool start = true)
        {
            Network = new LoopbackNetwork(options);
            Server = Network.CreateServer();
            Server.ConnectionRequested += OnRequest;
            Server.Connected += c =>
            {
                Connections.Add(c);
                Log.Add(Network.CurrentStep + " server connected " + c.Id);
            };
            Server.Received += (c, ch, m) =>
            {
                ServerReceived.Add((c.Id, ch, m.ToArray()));
                Log.Add(Network.CurrentStep + " server received " + c.Id + " " + ch + " " + m.Length);
            };
            Server.Disconnected += (c, info) =>
            {
                ServerDisconnects.Add((c.Id, info));
                Log.Add(Network.CurrentStep + " server disconnected " + c.Id + " " + info.Kind + " " + info.Data.Length);
            };
            if (start)
                Server.Start("127.0.0.1", 0);
        }

        private void OnRequest(IConnectionRequest request)
        {
            Requests++;
            byte[] reason = Judge(request.Data.ToArray());
            if (reason == null)
                request.Accept();
            else
                request.Reject(reason);
        }

        public LoopbackClientTransport Client(string name = null)
        {
            LoopbackClientTransport client = Network.CreateClient();
            string who = name ?? "client";
            client.Connected += () => Log.Add(Network.CurrentStep + " " + who + " connected");
            client.Received += (ch, m) =>
            {
                ClientReceived.Add((ch, m.ToArray()));
                Log.Add(Network.CurrentStep + " " + who + " received " + ch + " " + m.Length);
            };
            client.Disconnected += d =>
            {
                ClientDisconnects.Add(d);
                Log.Add(Network.CurrentStep + " " + who + " disconnected " + d.Kind + " " + d.Data.Length);
            };
            return client;
        }

        /// <summary>One network step: poll both ends, then advance.</summary>
        public void Step(params LoopbackClientTransport[] clients)
        {
            Server.Poll();
            foreach (LoopbackClientTransport c in clients)
                c.Poll();
            Network.Advance();
        }

        public void Run(int steps, params LoopbackClientTransport[] clients)
        {
            for (int i = 0; i < steps; i++)
                Step(clients);
        }
    }

    private static byte[] Hello(string name = "Alice") => Codec.Encode(new Hello(ProtocolVersion.Current, name, SessionToken.None));

    private static byte[] Numbered(int n) => BitConverter.GetBytes(n);

    private static int Number(byte[] message) => BitConverter.ToInt32(message);

    // ---- connect, accept, reject ----

    [Fact]
    public void AConnectionIsMadeInTheStepsTheLatencyGives()
    {
        var h = new Harness(new LoopbackOptions { MinLatencySteps = 2, MaxLatencySteps = 2 });
        LoopbackClientTransport client = h.Client();
        Assert.Equal(ClientTransportState.Idle, client.State);
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        Assert.Equal(ClientTransportState.Connecting, client.State);
        for (int i = 0; i < 6; i++)
            h.Step(client);
        // Sent at step 0: the server's poll at step 2 raises the request and accepts; the accept reaches the client at step 4.
        Assert.Equal(new[] { "2 server connected 1", "4 client connected" }, h.Log);
        Assert.Equal(ClientTransportState.Connected, client.State);
        Assert.Equal(1, h.Requests);
    }

    [Fact]
    public void TheRequestCarriesTheHelloBytes()
    {
        var h = new Harness();
        byte[] hello = Hello("Bob");
        byte[] seen = null;
        h.Judge = data =>
        {
            seen = data;
            return null;
        };
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, hello);
        h.Run(3, client);
        Assert.Equal(hello, seen);
        Assert.True(Codec.TryDecode(seen, out Hello decoded, out _));
        Assert.Equal("Bob", decoded.Name);
    }

    [Fact]
    public void ARejectReachesTheClientWithItsReasonAndNoConnectionExists()
    {
        var h = new Harness();
        byte[] reason = Codec.Encode(new DisconnectInfo(DisconnectReason.WrongVersion, "protocol 1 only"));
        h.Judge = _ => reason;
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        Assert.Single(h.ClientDisconnects);
        Assert.Equal(TransportDisconnectKind.Rejected, h.ClientDisconnects[0].Kind);
        Assert.True(Codec.TryDecode(h.ClientDisconnects[0].Data, out DisconnectInfo info, out _));
        Assert.Equal(new DisconnectInfo(DisconnectReason.WrongVersion, "protocol 1 only"), info);
        Assert.Empty(h.Connections);
        Assert.Equal(0, h.Server.ConnectionCount);
        Assert.Equal(ClientTransportState.Disconnected, client.State);
        Assert.Empty(h.ServerDisconnects);
    }

    [Fact]
    public void ARequestNobodyAnsweredIsRejectedWithoutData()
    {
        var network = new LoopbackNetwork();
        LoopbackServerTransport server = network.CreateServer();
        server.ConnectionRequested += _ => { };
        server.Start("127.0.0.1", 0);
        LoopbackClientTransport client = network.CreateClient();
        TransportDisconnect? result = null;
        client.Disconnected += d => result = d;
        client.Connect("127.0.0.1", server.Port, Hello());
        server.Poll();
        client.Poll();
        Assert.NotNull(result);
        Assert.Equal(TransportDisconnectKind.Rejected, result.Value.Kind);
        Assert.Empty(result.Value.Data);
    }

    [Fact]
    public void ARequestHandlerThatThrowsStillAnswersTheRequest()
    {
        var network = new LoopbackNetwork();
        LoopbackServerTransport server = network.CreateServer();
        server.ConnectionRequested += _ => throw new InvalidOperationException("host bug");
        server.Start("127.0.0.1", 0);
        LoopbackClientTransport client = network.CreateClient();
        TransportDisconnect? result = null;
        client.Disconnected += d => result = d;
        client.Connect("127.0.0.1", server.Port, Hello());
        Assert.Throws<InvalidOperationException>(() => server.Poll());
        client.Poll();
        Assert.Equal(TransportDisconnectKind.Rejected, result?.Kind);
    }

    [Fact]
    public void ARequestIsAliveDuringItsEventOnly()
    {
        var network = new LoopbackNetwork();
        LoopbackServerTransport server = network.CreateServer();
        IConnectionRequest kept = null;
        server.ConnectionRequested += r =>
        {
            kept = r;
            r.Accept();
            Assert.Throws<InvalidOperationException>(() => r.Accept());
            Assert.Throws<InvalidOperationException>(() => r.Reject(ReadOnlySpan<byte>.Empty));
        };
        server.Start("127.0.0.1", 0);
        network.CreateClient().Connect("127.0.0.1", server.Port, Hello());
        server.Poll();
        Assert.Throws<InvalidOperationException>(() => kept.Accept());
        Assert.Throws<InvalidOperationException>(() => kept.Data.ToArray());
    }

    [Fact]
    public void ConnectingToNobodyFailsAtTheNextPoll()
    {
        var network = new LoopbackNetwork();
        LoopbackClientTransport client = network.CreateClient();
        var ends = new List<TransportDisconnect>();
        client.Disconnected += ends.Add;
        client.Connect("127.0.0.1", 12345, Hello());
        Assert.Empty(ends);
        client.Poll();
        Assert.Equal(TransportDisconnectKind.ConnectFailed, Assert.Single(ends).Kind);
        Assert.Equal(ClientTransportState.Disconnected, client.State);
    }

    [Fact]
    public void ThePendingRequestCapRefusesTheRestOfAPollWithServerFull()
    {
        var h = new Harness();
        h.Server.MaxPendingRequests = 3;
        var clients = Enumerable.Range(0, 8).Select(i => h.Client("c" + i)).ToArray();
        foreach (LoopbackClientTransport c in clients)
            c.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, clients);
        Assert.Equal(3, h.Requests);
        Assert.Equal(3, h.Connections.Count);
        var full = h.ClientDisconnects.Where(d => d.Kind == TransportDisconnectKind.Rejected).ToList();
        Assert.Equal(5, full.Count);
        foreach (TransportDisconnect d in full)
        {
            Assert.True(Codec.TryDecode(d.Data, out DisconnectInfo info, out _));
            Assert.Equal(DisconnectReason.ServerFull, info.Reason);
        }
    }

    [Fact]
    public void TwoClientsGetTheirOwnConnectionsAndIds()
    {
        var h = new Harness();
        LoopbackClientTransport a = h.Client("a"), b = h.Client("b");
        a.Connect("127.0.0.1", h.Server.Port, Hello("A"));
        b.Connect("127.0.0.1", h.Server.Port, Hello("B"));
        h.Run(3, a, b);
        Assert.Equal(2, h.Connections.Count);
        Assert.NotEqual(h.Connections[0].Id, h.Connections[1].Id);
        Assert.NotEqual(h.Connections[0].RemoteEndPoint, h.Connections[1].RemoteEndPoint);
        h.Connections[0].Send(Channel.State, new byte[] { 1 });
        h.Connections[1].Send(Channel.State, new byte[] { 2 });
        a.Send(Channel.Control, new byte[] { 9 });
        h.Run(2, a, b);
        Assert.Equal(new[] { new byte[] { 1 }, new byte[] { 2 } }, h.ClientReceived.Select(r => r.Data).ToArray());
        Assert.Single(h.ServerReceived);
        Assert.Equal(h.Connections[0].Id, h.ServerReceived[0].Connection);
    }

    // ---- latency, ordering, loss, reorder ----

    [Fact]
    public void AMessageIsRaisedByThePollAtItsDeliveryStepAndNeverBefore()
    {
        var h = new Harness(new LoopbackOptions { MinLatencySteps = 3, MaxLatencySteps = 3 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(8, client);
        Assert.Equal(ClientTransportState.Connected, client.State);
        h.Log.Clear();
        client.Send(Channel.Control, new byte[] { 7 });
        long sentAt = h.Network.CurrentStep;
        for (int i = 0; i < 6; i++)
            h.Step(client);
        Assert.Equal(new[] { (sentAt + 3) + " server received 1 Control 1" }, h.Log);
        // Zero latency: the receiver's next poll.
        var z = new Harness();
        LoopbackClientTransport c0 = z.Client();
        c0.Connect("127.0.0.1", z.Server.Port, Hello());
        z.Server.Poll();
        c0.Poll();
        c0.Send(Channel.State, new byte[] { 1, 2 });
        z.Server.Poll();
        Assert.Single(z.ServerReceived);
    }

    [Fact]
    public void ReliableChannelsNeverLoseAndKeepTheirOrderUnderLossAndJitter()
    {
        var h = new Harness(new LoopbackOptions { Seed = 42, MinLatencySteps = 0, MaxLatencySteps = 5, LossRate = 0.35, RetransmitSteps = 3, ReorderRate = 0.5 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(20, client);
        Assert.Equal(ClientTransportState.Connected, client.State);
        h.ServerReceived.Clear();
        h.ClientReceived.Clear();
        for (int n = 0; n < 600; n++)
        {
            Channel channel = n % 3 == 0 ? Channel.Control : Channel.State;
            client.Send(channel, Numbered(n));
            h.Connections[0].Send(channel, Numbered(1000 + n));
            h.Step(client);
        }

        h.Run(200, client);
        foreach (Channel channel in new[] { Channel.Control, Channel.State })
        {
            int[] up = h.ServerReceived.Where(r => r.Channel == channel).Select(r => Number(r.Data)).ToArray();
            int[] down = h.ClientReceived.Where(r => r.Channel == channel).Select(r => Number(r.Data)).ToArray();
            Assert.Equal(Enumerable.Range(0, 600).Where(n => (n % 3 == 0) == (channel == Channel.Control)).ToArray(), up);
            Assert.Equal(up.Select(n => n + 1000).ToArray(), down);
        }
    }

    [Fact]
    public void TheUnreliableChannelLosesAndReordersAndNeverDuplicates()
    {
        var h = new Harness(new LoopbackOptions { Seed = 7, MinLatencySteps = 1, MaxLatencySteps = 3, LossRate = 0.3, ReorderRate = 0.3 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(20, client);
        h.ServerReceived.Clear();
        long dropped0 = h.Network.DroppedUnreliable;
        const int total = 2000;
        for (int n = 0; n < total; n++)
        {
            client.Send(Channel.Input, Numbered(n));
            h.Step(client);
        }

        h.Run(20, client);
        int[] got = h.ServerReceived.Select(r => Number(r.Data)).ToArray();
        long dropped = h.Network.DroppedUnreliable - dropped0;
        Assert.Equal(total, got.Length + dropped);
        Assert.InRange(dropped, total * 0.2, total * 0.4);
        Assert.Equal(got.Length, got.Distinct().Count());
        int inversions = 0;
        for (int i = 1; i < got.Length; i++)
            if (got[i] < got[i - 1])
                inversions++;
        Assert.True(inversions > 50, "reorder happened " + inversions + " times");
        Assert.True(inversions < got.Length / 2, "most messages still arrive in order");
    }

    [Fact]
    public void AHeldBackMessageIsReleasedWhenNothingFollowsIt()
    {
        var h = new Harness(new LoopbackOptions { ReorderRate = 1.0 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        client.Send(Channel.Input, new byte[] { 1 });
        h.Server.Poll();
        Assert.Empty(h.ServerReceived); // held back, waiting for a later message
        h.Network.Advance();
        h.Server.Poll();
        Assert.Single(h.ServerReceived);
    }

    [Fact]
    public void ReorderPutsAMessageRightAfterTheNextOne()
    {
        var h = new Harness(new LoopbackOptions { ReorderRate = 1.0 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        h.ServerReceived.Clear();
        // Rate 1: the first of a pair is held, the second goes out and the first after it.
        client.Send(Channel.Input, new byte[] { 1 });
        client.Send(Channel.Input, new byte[] { 2 });
        h.Server.Poll();
        Assert.Equal(new byte[] { 2, 1 }, h.ServerReceived.Select(r => r.Data[0]).ToArray());
    }

    [Fact]
    public void ASeedGivesTheSameScheduleAndAnotherSeedAnother()
    {
        List<string> Run(ulong seed)
        {
            var h = new Harness(new LoopbackOptions { Seed = seed, MinLatencySteps = 0, MaxLatencySteps = 4, LossRate = 0.3, ReorderRate = 0.3 });
            LoopbackClientTransport a = h.Client("a"), b = h.Client("b");
            a.Connect("127.0.0.1", h.Server.Port, Hello("A"));
            b.Connect("127.0.0.1", h.Server.Port, Hello("B"));
            h.Run(15, a, b);
            for (int n = 0; n < 300 && h.Connections.Count == 2; n++)
            {
                a.Send(n % 2 == 0 ? Channel.Control : Channel.Input, new byte[1 + n % 40]);
                b.Send(n % 3 == 0 ? Channel.State : Channel.Input, new byte[1 + n % 17]);
                h.Connections[0].Send(n % 2 == 0 ? Channel.State : Channel.Input, new byte[1 + n % 23]);
                h.Connections[1].Send(Channel.Control, new byte[1 + n % 5]);
                h.Step(a, b);
            }

            h.Run(30, a, b);
            return h.Log;
        }

        List<string> first = Run(11), again = Run(11), other = Run(12);
        Assert.True(first.Count > 300);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    // ---- closing ----

    [Fact]
    public void AServerDisconnectArrivesAfterTheMessagesSentBeforeItWithItsReason()
    {
        var h = new Harness(new LoopbackOptions { MinLatencySteps = 0, MaxLatencySteps = 3, Seed = 5 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(10, client);
        byte[] reason = Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, "bye"));
        IConnection connection = h.Connections[0];
        for (int i = 0; i < 20; i++)
            connection.Send(i % 2 == 0 ? Channel.State : Channel.Control, Numbered(i));
        connection.Disconnect(reason);
        Assert.False(connection.IsConnected);
        connection.Send(Channel.State, Numbered(99)); // dropped
        h.Run(10, client);
        Assert.Equal(20, h.ClientReceived.Count);
        Assert.Equal(Enumerable.Range(0, 20).Where(i => i % 2 == 0), h.ClientReceived.Where(r => r.Channel == Channel.State).Select(r => Number(r.Data)));
        TransportDisconnect end = Assert.Single(h.ClientDisconnects);
        Assert.Equal(TransportDisconnectKind.RemoteClose, end.Kind);
        Assert.Equal(reason, end.Data);
        Assert.Equal(ClientTransportState.Disconnected, client.State);
        // The server's own end sees a local close at its next poll.
        TransportDisconnect local = Assert.Single(h.ServerDisconnects).Info;
        Assert.Equal(TransportDisconnectKind.LocalClose, local.Kind);
        client.Send(Channel.Control, new byte[] { 1 }); // dropped
        h.Run(3, client);
        Assert.Empty(h.ServerReceived);
    }

    [Fact]
    public void AClientDisconnectGivesALocalCloseHereAndARemoteCloseThere()
    {
        var h = new Harness();
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        client.Send(Channel.Control, new byte[] { 1 });
        client.Disconnect();
        Assert.Equal(ClientTransportState.Disconnected, client.State);
        Assert.Empty(h.ClientDisconnects); // events only in Poll
        h.Run(3, client);
        Assert.Equal(TransportDisconnectKind.LocalClose, Assert.Single(h.ClientDisconnects).Kind);
        Assert.Single(h.ServerReceived);
        (int id, TransportDisconnect info) = Assert.Single(h.ServerDisconnects);
        Assert.Equal(h.Connections[0].Id, id);
        Assert.Equal(TransportDisconnectKind.RemoteClose, info.Kind);
        Assert.False(h.Connections[0].IsConnected);
        Assert.Equal(0, h.Server.ConnectionCount);
    }

    [Fact]
    public void ADisconnectBeforeTheAcceptStillClosesBothEnds()
    {
        var h = new Harness(new LoopbackOptions { MinLatencySteps = 2, MaxLatencySteps = 2 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Step(client);
        client.Disconnect();
        h.Run(8, client);
        Assert.Equal(TransportDisconnectKind.LocalClose, Assert.Single(h.ClientDisconnects).Kind);
        Assert.Empty(h.Connections); // the request was ignored: the client had left
        Assert.Empty(h.ServerDisconnects);
    }

    [Fact]
    public void ADroppedClientTimesOutOnBothEndsAfterTheTimeoutAndNothingMoreIsDelivered()
    {
        var h = new Harness(new LoopbackOptions { TimeoutSteps = 40 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        long dropAt = h.Network.CurrentStep;
        client.Drop();
        client.Send(Channel.Control, new byte[] { 1 });
        h.Connections[0].Send(Channel.State, new byte[] { 2 });
        while (h.Network.CurrentStep < dropAt + 40)
            h.Step(client);
        Assert.Empty(h.ClientDisconnects);
        Assert.Empty(h.ServerDisconnects);
        Assert.Empty(h.ServerReceived);
        Assert.Empty(h.ClientReceived);
        h.Step(client); // the poll at dropAt + 40
        Assert.Equal(TransportDisconnectKind.Timeout, Assert.Single(h.ClientDisconnects).Kind);
        Assert.Equal(TransportDisconnectKind.Timeout, Assert.Single(h.ServerDisconnects).Info.Kind);
        Assert.False(h.Connections[0].IsConnected);
    }

    [Fact]
    public void ServerStopDisconnectsEveryoneFreesThePortAndAllowsARestart()
    {
        var h = new Harness();
        LoopbackClientTransport a = h.Client("a"), b = h.Client("b");
        a.Connect("127.0.0.1", h.Server.Port, Hello("A"));
        b.Connect("127.0.0.1", h.Server.Port, Hello("B"));
        h.Run(3, a, b);
        int port = h.Server.Port;
        h.Server.Stop();
        h.Run(3, a, b);
        Assert.Equal(2, h.ClientDisconnects.Count);
        Assert.All(h.ClientDisconnects, d => Assert.Equal(TransportDisconnectKind.RemoteClose, d.Kind));
        Assert.Equal(2, h.ServerDisconnects.Count); // raised by the poll after the stop
        Assert.All(h.ServerDisconnects, d => Assert.Equal(TransportDisconnectKind.LocalClose, d.Info.Kind));
        // The port is free and the same transport starts again; a client can connect again too.
        h.Server.Start("127.0.0.1", port);
        a.Connect("127.0.0.1", port, Hello("A"));
        h.Run(3, a);
        Assert.Equal(ClientTransportState.Connected, a.State);
        Assert.Throws<InvalidOperationException>(() => h.Server.Start("127.0.0.1", port));
    }

    [Fact]
    public void ATransportConnectsAgainAfterItEnded()
    {
        var h = new Harness();
        LoopbackClientTransport client = h.Client();
        for (int round = 0; round < 3; round++)
        {
            client.Connect("127.0.0.1", h.Server.Port, Hello());
            Assert.Throws<InvalidOperationException>(() => client.Connect("127.0.0.1", h.Server.Port, Hello()));
            h.Run(3, client);
            Assert.Equal(ClientTransportState.Connected, client.State);
            client.Disconnect();
            h.Run(3, client);
            Assert.Equal(ClientTransportState.Disconnected, client.State);
        }

        Assert.Equal(3, h.Connections.Count);
        Assert.Equal(3, h.Connections.Select(c => c.Id).Distinct().Count());
    }

    // ---- limits, counters, threading ----

    [Fact]
    public void AnUnreliableMessageOverTheLimitIsRefusedAndAReliableOneOfAnySizePasses()
    {
        var h = new Harness();
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        client.Send(Channel.Input, new byte[Channels.MaxUnreliableBytes]);
        Assert.Throws<ArgumentException>(() => client.Send(Channel.Input, new byte[Channels.MaxUnreliableBytes + 1]));
        Assert.Throws<ArgumentException>(() => h.Connections[0].Send(Channel.Input, new byte[Channels.MaxUnreliableBytes + 1]));
        client.Send(Channel.State, new byte[300_000]);
        h.Connections[0].Send(Channel.State, new byte[2_000_000]);
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Send((Channel)3, new byte[1]));
        h.Run(3, client);
        Assert.Equal(300_000, h.ServerReceived.Single(r => r.Channel == Channel.State).Data.Length);
        Assert.Equal(2_000_000, h.ClientReceived.Single().Data.Length);
        var small = new Harness(new LoopbackOptions { MaxUnreliableBytes = 100 });
        LoopbackClientTransport c2 = small.Client();
        c2.Connect("127.0.0.1", small.Server.Port, Hello());
        small.Run(3, c2);
        Assert.Throws<ArgumentException>(() => c2.Send(Channel.Input, new byte[101]));
    }

    [Fact]
    public void CountersCountPayloadBytesAndMessagesPerChannelOnBothEnds()
    {
        var h = new Harness();
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(3, client);
        client.Send(Channel.Control, new byte[10]);
        client.Send(Channel.Control, new byte[5]);
        client.Send(Channel.Input, new byte[7]);
        h.Connections[0].Send(Channel.State, new byte[100]);
        h.Connections[0].Send(Channel.Control, new byte[3]);
        h.Run(3, client);
        IConnection server = h.Connections[0];
        var sc = (IChannelCounters)server;
        Assert.Equal(15, sc.BytesReceivedOn(Channel.Control));
        Assert.Equal(2, sc.MessagesReceivedOn(Channel.Control));
        Assert.Equal(7, sc.BytesReceivedOn(Channel.Input));
        Assert.Equal(0, sc.BytesReceivedOn(Channel.State));
        Assert.Equal(22, server.BytesReceived);
        Assert.Equal(3, server.MessagesReceived);
        Assert.Equal(103, server.BytesSent);
        Assert.Equal(100, sc.BytesSentOn(Channel.State));
        Assert.Equal(2, server.MessagesSent);
        var cc = (IChannelCounters)client;
        Assert.Equal(22, client.BytesSent);
        Assert.Equal(103, client.BytesReceived);
        Assert.Equal(100, cc.BytesReceivedOn(Channel.State));
        Assert.Equal(7, cc.BytesSentOn(Channel.Input));
        Assert.Equal(1, cc.MessagesSentOn(Channel.Input));
        Assert.Equal(22, cc.WireBytesSent);
    }

    [Fact]
    public void EventsComeOnlyFromPoll()
    {
        var h = new Harness();
        var raised = new List<string>();
        h.Server.Connected += c => raised.Add("connected");
        LoopbackClientTransport client = h.Client();
        client.Connected += () => raised.Add("client connected");
        client.Received += (_, _) => raised.Add("client received");
        client.Disconnected += _ => raised.Add("client disconnected");
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Network.Advance(5);
        Assert.Empty(raised);
        h.Server.Poll();
        Assert.Equal(new[] { "connected" }, raised);
        raised.Clear();
        h.Connections[0].Send(Channel.State, new byte[1]);
        h.Connections[0].Disconnect(ReadOnlySpan<byte>.Empty);
        h.Network.Advance(5);
        Assert.Empty(raised);
        client.Poll();
        Assert.Equal(new[] { "client connected", "client received", "client disconnected" }, raised);
    }

    [Fact]
    public void ARoundTripIsReportedFromTheLatencyInMilliseconds()
    {
        var h = new Harness(new LoopbackOptions { MinLatencySteps = 2, MaxLatencySteps = 2 });
        LoopbackClientTransport client = h.Client();
        client.Connect("127.0.0.1", h.Server.Port, Hello());
        h.Run(6, client);
        client.Send(Channel.Control, new byte[1]);
        h.Connections[0].Send(Channel.Control, new byte[1]);
        h.Run(4, client);
        Assert.Equal(200, h.Connections[0].RoundTripMs);
        Assert.Equal(200, client.RoundTripMs);
    }

    [Fact]
    public void OptionsAreValidatedAndOneServerPerNetwork()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackNetwork(new LoopbackOptions { MinLatencySteps = 3, MaxLatencySteps = 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackNetwork(new LoopbackOptions { LossRate = 1.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackNetwork(new LoopbackOptions { ReorderRate = -0.1 }));
        var network = new LoopbackNetwork();
        network.CreateServer();
        Assert.Throws<InvalidOperationException>(() => network.CreateServer());
        Assert.Throws<ArgumentOutOfRangeException>(() => network.Advance(-1));
        Assert.Equal(0, network.CurrentStep);
        network.Advance(4);
        Assert.Equal(4, network.CurrentStep);
    }
}
