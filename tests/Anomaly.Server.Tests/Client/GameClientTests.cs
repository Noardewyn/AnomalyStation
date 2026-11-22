using Anomaly.Client.Shared;
using System.Threading;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// M1 P6: <see cref="GameClient"/>, the real feed, with P2's codec, replica and transports. First against a scripted
/// transport that raises exactly the events a test asks for (join, deltas, rejects, timeouts, desync), then end to end
/// against P5's <c>MiniServer</c> over the loopback network and over real sockets, and a connect to a closed port that must
/// fail fast. The end-to-end run against the real server host is the lead's integration test.
/// </summary>
public class GameClientTests
{

    private sealed class ScriptedTransport : IClientTransport
    {
        public readonly List<(Channel Channel, byte[] Message)> Sent = new();
        public byte[] Hello;
        public string Host;
        public int Port;
        public bool Disconnected;
        public bool DisposedCalled;

        public ClientTransportState State { get; set; } = ClientTransportState.Idle;
        public int RoundTripMs => 12;
        public long BytesSent => 0;
        public long BytesReceived => 0;

        public event Action Connected;
        public event ClientReceivedHandler Received;
        event Action<TransportDisconnect> IClientTransport.Disconnected { add => EndedHandler += value; remove => EndedHandler -= value; }
        private Action<TransportDisconnect> EndedHandler;

        public void Connect(string host, int port, ReadOnlySpan<byte> hello)
        {
            Host = host;
            Port = port;
            Hello = hello.ToArray();
            State = ClientTransportState.Connecting;
        }

        public void Poll()
        {
        }

        public void Send(Channel channel, ReadOnlySpan<byte> message) => Sent.Add((channel, message.ToArray()));

        public void Disconnect() => Disconnected = true;

        public void Dispose() => DisposedCalled = true;

        public void RaiseConnected()
        {
            State = ClientTransportState.Connected;
            Connected?.Invoke();
        }

        public void RaiseReceived(Channel channel, byte[] message) => Received?.Invoke(channel, message);

        public void RaiseEnded(TransportDisconnect end)
        {
            State = ClientTransportState.Disconnected;
            EndedHandler?.Invoke(end);
        }
    }

    private static MapGrid SmallMap()
    {
        var map = new MapGrid { Tick = 10, Width = 4, Height = 4, Levels = 1 };
        map.TurfTypes.Add("/turf/open/indestructible");
        map.TurfTypes.Add("/turf/closed/indestructible");
        map.Runs.Add(new TurfRun(4, 1));
        map.Runs.Add(new TurfRun(8, 0));
        map.Runs.Add(new TurfRun(4, 1));
        return map;
    }

    private static Snapshot SnapshotAt(long tick, long own)
    {
        var snapshot = new Snapshot { Tick = tick, LastInputSeq = 0, OwnEntity = own };
        snapshot.Types.Add("/mob");
        snapshot.Entities.Add(new EntityState(own, 0, 2, 2, 1, 2, "Alice", 0f, tick));
        snapshot.Entities.Add(new EntityState(own + 1, 0, 3, 3, 1, 2, "Bob", 0f, tick));
        return snapshot;
    }

    private static (GameClient Client, ScriptedTransport Transport) NewClient()
    {
        var transport = new ScriptedTransport();
        var client = new GameClient(transport, "127.0.0.1", 7777, "Alice");
        return (client, transport);
    }

    private static void Join(GameClient client, ScriptedTransport transport, long tick = 10, long own = 5)
    {
        client.Connect();
        transport.RaiseConnected();
        transport.RaiseReceived(Channel.State, Codec.Encode(new Welcome(1, new SessionToken(1, 2), tick, 20, own)));
        transport.RaiseReceived(Channel.State, Codec.Encode(SmallMap()));
        transport.RaiseReceived(Channel.State, Codec.Encode(SnapshotAt(tick, own)));
    }

    [Fact]
    public void ConnectSendsAHelloAndReturnsAtOnce()
    {
        var (client, transport) = NewClient();
        Assert.Equal(FeedPhase.Idle, client.Phase);
        client.Connect();
        Assert.Equal(FeedPhase.Connecting, client.Phase);
        Assert.Equal("127.0.0.1", transport.Host);
        Assert.Equal(7777, transport.Port);
        Assert.True(Codec.TryDecode(transport.Hello, out Hello hello, out _));
        Assert.Equal(ProtocolVersion.Current, hello.Protocol);
        Assert.Equal("Alice", hello.Name);
        Assert.True(hello.ReconnectToken.IsNone);
    }

    [Fact]
    public void TheJoinSequenceMakesTheFeedPlay()
    {
        var (client, transport) = NewClient();
        var spawned = new List<long>();
        client.Spawned += e => spawned.Add(e.Id);
        client.Connect();
        transport.RaiseConnected();
        Assert.Equal(FeedPhase.Joining, client.Phase);
        transport.RaiseReceived(Channel.State, Codec.Encode(new Welcome(1, new SessionToken(1, 2), 10, 20, 5)));
        Assert.Equal(FeedPhase.Joining, client.Phase);
        transport.RaiseReceived(Channel.State, Codec.Encode(SmallMap()));
        Assert.Equal(FeedPhase.Joining, client.Phase);
        transport.RaiseReceived(Channel.State, Codec.Encode(SnapshotAt(10, 5)));
        Assert.Equal(FeedPhase.Playing, client.Phase);
        Assert.Equal(5, client.OwnEntity);
        Assert.Equal(10, client.Tick);
        Assert.Equal(2, client.Count);
        Assert.Equal(new long[] { 5, 6 }, spawned.OrderBy(i => i));
        Assert.Equal(4, client.Map.Width);
        Assert.Equal(new SessionToken(1, 2), client.Token);
        Assert.Equal(1, client.Session);
        ReplicaEntity own = client.Entities.Single(e => e.Id == 5);
        Assert.Equal("Alice", own.State.Name);
        Assert.Equal("/mob", own.TypePath);
        Assert.True(own.IsMob());
    }

    [Fact]
    public void ADeltaMovesEntitiesAndReportsTheAppliedInput()
    {
        var (client, transport) = NewClient();
        var despawned = new List<long>();
        client.Despawned += e => despawned.Add(e.Id);
        Join(client, transport);
        var delta = new Delta { Tick = 11, LastInputSeq = 3, OwnEntity = 5 };
        delta.FirstNewType = 1;
        var change = new EntityChange { Id = 5, Mask = NetField.X | NetField.LastMoveTick, X = 3, LastMoveTick = 11 };
        delta.Changed.Add(change);
        delta.Despawned.Add(6);
        transport.RaiseReceived(Channel.State, Codec.Encode(delta));
        Assert.Equal(11, client.Tick);
        Assert.Equal(3u, client.LastAppliedSeq);
        Assert.Equal(1, client.DeltaCount);
        Assert.Equal(new long[] { 6 }, despawned);
        Assert.Equal(1, client.Count);
        ReplicaEntity own = client.Entities.Single();
        Assert.Equal(2, own.SampleCount);
        Assert.Equal(3, own.Sample(0).X);
        Assert.Equal(2, own.Sample(1).X);
    }

    // One wish vector as the next motion frame (the version-2 input; the play loop's predictor sends them in M2).
    private static void Walk(GameClient client, sbyte moveX, sbyte moveY)
    {
        var message = new MotionInputMessage();
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(client.LastSentSeq + 1, moveX, moveY, 0, Anomaly.Core.Motion.MotionInputFlags.None));
        client.SendMotion(message);
    }

    [Fact]
    public void MotionFramesGoOutOnTheInputChannelWithTheirSeqs()
    {
        var (client, transport) = NewClient();
        Walk(client, 0, 127); // not playing yet: dropped
        Assert.Empty(transport.Sent);
        Join(client, transport);
        Walk(client, 0, 127);
        Walk(client, 0, 0);
        Assert.Equal(2, transport.Sent.Count);
        Assert.All(transport.Sent, s => Assert.Equal(Channel.Input, s.Channel));
        Assert.True(Codec.TryDecode(transport.Sent[0].Message, out MotionInputMessage first, out _));
        Assert.True(Codec.TryDecode(transport.Sent[1].Message, out MotionInputMessage second, out _));
        Assert.Equal((1u, (sbyte)127), (first.Frames.Single().Seq, first.Frames.Single().MoveY));
        Assert.Equal((2u, (sbyte)0), (second.Frames.Single().Seq, second.Frames.Single().MoveY));
        Assert.Equal(2u, client.LastSentSeq);
    }

    [Fact]
    public void ARejectShowsTheServersReason()
    {
        var (client, transport) = NewClient();
        client.Connect();
        byte[] data = Codec.Encode(new DisconnectInfo(DisconnectReason.NameInUse, "Alice"));
        transport.RaiseEnded(new TransportDisconnect(TransportDisconnectKind.Rejected, data));
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.True(client.Rejected);
        Assert.Contains("rejected", client.CloseReason);
        Assert.Contains("in use", client.CloseReason);
        Assert.Contains("Alice", client.CloseReason);
    }

    [Fact]
    public void ATimeoutAndAFailedConnectSayWhatHappened()
    {
        var (client, transport) = NewClient();
        client.Connect();
        transport.RaiseEnded(new TransportDisconnect(TransportDisconnectKind.Timeout, null));
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.False(client.Rejected);
        Assert.Contains("127.0.0.1:7777", client.CloseReason);

        var (second, secondTransport) = NewClient();
        second.Connect();
        secondTransport.RaiseEnded(new TransportDisconnect(TransportDisconnectKind.ConnectFailed, null));
        Assert.Contains("could not connect", second.CloseReason);
    }

    [Fact]
    public void AKickAfterJoiningIsADisconnectNotAReject()
    {
        var (client, transport) = NewClient();
        Join(client, transport);
        byte[] data = Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, null));
        transport.RaiseEnded(new TransportDisconnect(TransportDisconnectKind.RemoteClose, data));
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.False(client.Rejected);
        Assert.Contains("kicked", client.CloseReason);
    }

    [Fact]
    public void ADesyncLeavesWithAReason()
    {
        var (client, transport) = NewClient();
        Join(client, transport);
        var skipped = new Delta { Tick = 99, OwnEntity = 5 }; // not the tick after the snapshot
        transport.RaiseReceived(Channel.State, Codec.Encode(skipped));
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.Contains("out of sync", client.CloseReason);
        Assert.True(transport.Disconnected);
    }

    [Fact]
    public void AMessageNobodyShouldSendLeaves()
    {
        var (client, transport) = NewClient();
        Join(client, transport);
        var input = new MotionInputMessage();
        input.Frames.Add(Anomaly.Core.Motion.MotionInput.Idle(1, 0));
        transport.RaiseReceived(Channel.State, Codec.Encode(input)); // a client's message: no server sends it
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.Contains("unexpected", client.CloseReason);
    }

    [Fact]
    public void ServerTextReachesTheEvent()
    {
        var (client, transport) = NewClient();
        var texts = new List<ServerText>();
        client.ServerTextReceived += texts.Add;
        Join(client, transport);
        transport.RaiseReceived(Channel.Control, Codec.Encode(new ServerText("info", "<b>Hello</b>")));
        Assert.Equal(new[] { new ServerText("info", "<b>Hello</b>") }, texts);
    }

    [Fact]
    public void DisconnectAndDisposeAreSafeAtAnyTime()
    {
        var (client, transport) = NewClient();
        client.Poll(0); // idle: nothing happens
        client.Disconnect();
        Assert.Equal(FeedPhase.Idle, client.Phase);
        client.Connect();
        Assert.Equal(FeedPhase.Connecting, client.Phase);
        client.Disconnect();
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.Equal("you disconnected", client.CloseReason);
        Assert.True(transport.Disconnected);
        client.Poll(1); // closed: nothing happens
        client.Dispose();
        client.Dispose();
        Assert.True(transport.DisposedCalled);
    }

    [Fact]
    public void ATransportThatFailsInPollClosesTheFeedInsteadOfCrashing()
    {
        var transport = new FailingTransport();
        var client = new GameClient(transport, "127.0.0.1", 7777, "Alice");
        client.Connect();
        client.Poll(0);
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.Contains("network error", client.CloseReason);
        Assert.Contains("boom", client.CloseReason);
    }

    private sealed class FailingTransport : IClientTransport
    {
        public ClientTransportState State => ClientTransportState.Connecting;
        public int RoundTripMs => 0;
        public long BytesSent => 0;
        public long BytesReceived => 0;
        public event Action Connected { add { } remove { } }
        public event ClientReceivedHandler Received { add { } remove { } }
        public event Action<TransportDisconnect> Disconnected { add { } remove { } }
        public void Connect(string host, int port, ReadOnlySpan<byte> hello) { }
        public void Poll() => throw new InvalidOperationException("boom");
        public void Send(Channel channel, ReadOnlySpan<byte> message) { }
        public void Disconnect() { }
        public void Dispose() { }
    }

    // ---- end to end: the real transports and P5's MiniServer (the join, the deltas, the input frames) ----

    private static MapGrid Room() => TestMaps.Room(30, 30, (10, 10), (11, 10), (20, 15));

    [Fact]
    public void TheClientJoinsWalksAndSeesItselfMoveOverTheLoopback()
    {
        var network = new LoopbackNetwork(new LoopbackOptions { Seed = 2, MinLatencySteps = 0, MaxLatencySteps = 1 });
        LoopbackServerTransport serverTransport = network.CreateServer();
        serverTransport.Start("127.0.0.1", 7777);
        var server = new MiniServer(serverTransport, Room());
        var client = new GameClient(network.CreateClient(), "127.0.0.1", 7777, "Alice");
        var other = new GameClient(network.CreateClient(), "127.0.0.1", 7777, "Bob");
        var spawned = new List<long>();
        client.Spawned += e => spawned.Add(e.Id);
        client.Connect();
        other.Connect();

        void Tick()
        {
            server.Step();
            network.Advance();
            client.Poll(0);
            other.Poll(0);
        }

        for (int i = 0; i < 30 && (client.Phase != FeedPhase.Playing || other.Phase != FeedPhase.Playing); i++)
            Tick();
        Assert.Equal(FeedPhase.Playing, client.Phase);
        Assert.Equal(FeedPhase.Playing, other.Phase);
        Assert.NotNull(client.Map);
        Assert.Equal(30, client.Map.Width);
        long own = client.OwnEntity;
        Assert.NotEqual(0, own);
        Assert.NotEqual(other.OwnEntity, own);
        for (int i = 0; i < 5; i++)
            Tick();
        Assert.Equal(2, client.Count); // itself and Bob
        Assert.Contains(own, spawned);

        ReplicaEntity me = client.Entities.Single(e => e.Id == own);
        (int x, int y) start = (me.State.X, me.State.Y);
        Walk(client, 127, 127);
        Assert.Equal(1u, client.LastSentSeq);
        for (int i = 0; i < 40; i++)
            Tick();
        Assert.Equal(1u, client.LastAppliedSeq);
        Assert.True(me.State.X != start.x || me.State.Y != start.y, "the avatar never moved");
        Assert.True(me.SampleCount >= 2);
        Assert.True(me.TryInterpolate(client.Tick + 5, out EntityPose pose));
        Assert.Equal(me.State.X, pose.X, 3);
        Assert.Equal(me.State.Y, pose.Y, 3);
        Assert.True(client.DeltaCount > 40);
        Assert.True(client.BytesReceived > client.DeltaCount * 5, $"{client.BytesReceived} bytes for {client.DeltaCount} deltas");
        Assert.True(client.BytesSent > 0);
        Assert.Equal(0, server.Violations);

        // Bob walks too; Alice sees him move.
        ReplicaEntity bob = client.Entities.Single(e => e.Id == other.OwnEntity);
        (int x, int y) bobStart = (bob.State.X, bob.State.Y);
        Walk(other, -127, 0);
        for (int i = 0; i < 40; i++)
            Tick();
        Assert.True(bob.State.X != bobStart.x || bob.State.Y != bobStart.y, "Alice never saw Bob move");

        // Leaving: Bob's mob despawns from Alice's view.
        var despawned = new List<long>();
        client.Despawned += e => despawned.Add(e.Id);
        long bobId = other.OwnEntity;
        other.Disconnect();
        for (int i = 0; i < 20; i++)
            Tick();
        Assert.Equal(new[] { bobId }, despawned);
        Assert.Equal(1, client.Count);
        client.Dispose();
        other.Dispose();
    }

    [Fact]
    public void ASecondClientWithTheSameNameIsRefusedWithTheReason()
    {
        var network = new LoopbackNetwork();
        LoopbackServerTransport serverTransport = network.CreateServer();
        serverTransport.Start("127.0.0.1", 7777);
        var server = new MiniServer(serverTransport, Room());
        var first = new GameClient(network.CreateClient(), "127.0.0.1", 7777, "Alice");
        var second = new GameClient(network.CreateClient(), "127.0.0.1", 7777, "Alice");
        first.Connect();
        for (int i = 0; i < 10; i++)
        {
            server.Step();
            network.Advance();
            first.Poll(0);
        }
        second.Connect();
        for (int i = 0; i < 10 && second.Phase != FeedPhase.Closed; i++)
        {
            server.Step();
            network.Advance();
            first.Poll(0);
            second.Poll(0);
        }
        Assert.Equal(FeedPhase.Playing, first.Phase);
        Assert.Equal(FeedPhase.Closed, second.Phase);
        Assert.True(second.Rejected);
        Assert.Contains("that name is in use", second.CloseReason);
    }

    [Fact]
    public void AConnectToAPortWithNoServerOnTheLoopbackFails()
    {
        var network = new LoopbackNetwork();
        var client = new GameClient(network.CreateClient(), "127.0.0.1", 9, "Alice");
        client.Connect();
        for (int i = 0; i < 5 && client.Phase != FeedPhase.Closed; i++)
        {
            network.Advance();
            client.Poll(0);
        }
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.False(client.Rejected);
        Assert.Contains("could not connect", client.CloseReason);
    }

    [Fact]
    public void TheClientJoinsAndSeesDeltasOverRealSocketsWithTheGodotTransportSettings()
    {
        using var serverTransport = new LiteNetServerTransport();
        serverTransport.Start("127.0.0.1", 0);
        var server = new MiniServer(serverTransport, Room());
        using var client = new GameClient(GameClient.CreateTransport(), "127.0.0.1", serverTransport.Port, "Alice");
        client.Connect();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int steps = 0;
        // The server ticks at 20 Hz, the client polls at about 60 Hz (a game's frames).
        while (clock.Elapsed.TotalSeconds < 10 && (client.Phase != FeedPhase.Playing || client.DeltaCount < 20))
        {
            if (clock.Elapsed.TotalMilliseconds >= steps * 50)
            {
                server.Step();
                steps++;
            }
            client.Poll(clock.Elapsed.TotalSeconds);
            Thread.Sleep(2);
            Assert.NotEqual(FeedPhase.Closed, client.Phase);
        }
        Assert.Equal(FeedPhase.Playing, client.Phase);
        Assert.True(client.DeltaCount >= 20, $"only {client.DeltaCount} deltas");
        Assert.NotEqual(0, client.OwnEntity);

        Walk(client, 0, 127);
        ReplicaEntity me = client.Entities.Single(e => e.Id == client.OwnEntity);
        int y0 = me.State.Y;
        long until = clock.ElapsedMilliseconds + 1500;
        while (clock.ElapsedMilliseconds < until)
        {
            if (clock.Elapsed.TotalMilliseconds >= steps * 50)
            {
                server.Step();
                steps++;
            }
            client.Poll(clock.Elapsed.TotalSeconds);
            Thread.Sleep(2);
        }
        Assert.True(client.LastAppliedSeq >= 1);
        Assert.True(me.State.Y != y0, "the avatar never moved over the socket");
        Assert.True(client.RoundTripMs >= 0);

        client.Disconnect();
        Assert.Equal(FeedPhase.Closed, client.Phase);
    }

    [Fact]
    public void AConnectToAClosedPortFailsFast()
    {
        // A UDP port that is free now (nothing listens on it once the probe socket is closed).
        int port;
        using (var probe = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
            port = ((System.Net.IPEndPoint)probe.Client.LocalEndPoint).Port;

        using var client = new GameClient(GameClient.CreateTransport(), "127.0.0.1", port, "Alice");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        client.Connect();
        // M3 I1d (load tolerance): the bounds are below what they guard against, with room for a busy machine (I1c saw 2.5 s at
        // a load average of 17-25): Connect does not wait for the handshake (2 s), and the give-up comes before LiteNetLib's 5 s.
        Assert.True(clock.ElapsedMilliseconds < 1000, $"Connect must return at once, took {clock.ElapsedMilliseconds} ms");
        while (client.Phase != FeedPhase.Closed && clock.Elapsed.TotalSeconds < 8)
        {
            client.Poll(clock.Elapsed.TotalSeconds);
            Thread.Sleep(5);
        }
        double seconds = clock.Elapsed.TotalSeconds;
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.False(client.Rejected);
        Assert.True(client.CloseReason.Contains("could not connect") || client.CloseReason.Contains("timed out"), client.CloseReason);
        // LiteNetLib's defaults take 5 s (10 attempts, 500 ms apart); the client's settings give up within two seconds on an idle
        // machine (TheClientTransportFailsFasterThanLiteNetLibsDefaults pins the settings themselves).
        Assert.True(seconds < 4.5, $"a connect to a closed port took {seconds:0.0} s");
    }

    [Fact]
    public void TheClientTransportFailsFasterThanLiteNetLibsDefaults()
    {
        var transport = GameClient.CreateTransport();
        Assert.False(transport.ManualMode);
        Assert.Equal(GameClient.ConnectAttempts, transport.MaxConnectAttempts);
        Assert.Equal(GameClient.ConnectRetryMs, transport.ReconnectDelayMs);
        Assert.True(transport.MaxConnectAttempts * transport.ReconnectDelayMs <= 2000);
        transport.Dispose();
    }
}
