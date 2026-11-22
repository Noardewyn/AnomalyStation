using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The session state machine (scope 2b, 2e; Session.cs): a Hello accepted, the Join applied in the next tick (the avatar
/// made and keyed, client/New started), the Welcome once client/New returned, the Leave on a disconnect, the reconnect by
/// name and token, the timeout, max_players, and a disconnect while a sleeping Login has not finished.
/// </summary>
public class SessionTests
{
    [Fact]
    public void JoinsOpenOnlyOnceTheMasterInitializedAndTheRunLevelIsGame()
    {
        var config = TestServer.DefaultConfig();
        using var server = TestServer.Start(config);
        Assert.InRange(server.Host.JoinsOpenedAt, 20, 45); // Master.Initialize sleeps 10 ds, then initializes at tick 20; the first fires' random offsets (one per subsystem) move the rest
        Assert.Contains("joins open at tick", server.Out.ToString());
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void AHelloBeforeJoinsOpenIsRefusedAsNotReady()
    {
        using var server = TestServer.StartWithoutBoot(TestServer.DefaultConfig());
        FakeRemote early = server.Connect("Early");
        server.Tick();
        Assert.Equal(DisconnectReason.NotReady, early.Rejected?.Reason);
        Assert.Null(early.Connection);
        server.UntilJoinsOpen();
        FakeRemote later = server.Connect("Later");
        server.Tick();
        Assert.NotNull(later.Connection);
    }

    [Fact]
    public void AJoinMakesAKeyedAvatarAtASpawnPointAndTheWelcomeFollowsTheLogin()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Connect("Alice");
        long before = server.Host.Tick;

        // One tick: the poll accepts the Hello and queues the Join for the next tick (the one this RunTick steps); the
        // Join applies in its command phase (avatar, KeyMob, Connect); the Login is instant here, so the Welcome goes out
        // after the Step.
        server.Tick();
        Session s = server.Host.SessionOf(1);
        Assert.Equal((before + 1, 1, QueuedCommand.JoinSeq, "Alice"),
            server.Host.Log.Commands.Where(c => c.Kind == CommandKind.Join).Select(c => (c.ApplyTick, c.Session, c.Seq, c.Text)).Single());
        Assert.Equal(new[] { before + 1 }, server.Clients.Connects.Select(c => c.Tick));
        Assert.Equal(SessionState.Joined, s.State);
        Assert.Single(server.Clients.Keyed);
        Assert.Equal("Alice", server.Clients.Keyed[0].Key);
        mob avatar = server.MobOf("Alice");
        Assert.NotNull(avatar);
        Assert.Equal("alice", avatar.ckey);
        SpawnPoint first = server.Host.Room.SpawnPoints[0];
        Assert.Equal((first.X, first.Y), server.PositionOf("Alice"));

        Welcome welcome = alice.Welcome.Value;
        Assert.Equal(1, welcome.Session);
        Assert.Equal(avatar.dm_id, welcome.OwnEntity);
        Assert.Equal(server.Host.Tick, welcome.Tick);
        Assert.Equal((byte)20, welcome.TickRate);
        Assert.False(welcome.Token.IsNone);

        // The join sequence on the State channel: Welcome, MapGrid, Snapshot of the same tick, then a Delta per tick.
        var state = alice.SentOn(Channel.State).Select(m => (MessageId)m[0]).ToArray();
        Assert.Equal(new[] { MessageId.Welcome, MessageId.MapGrid, MessageId.Snapshot }, state);
        // M2: the avatar is tg's human, registered as a capsule mob once its client is attached, and the join messages come
        // from the version-2 call with the session's private inputs.
        Assert.IsType<mob_living_carbon_human>(avatar);
        Assert.Equal(new[] { (before + 1, avatar.dm_id) }, server.Locomotion.Registrations);
        var (joinTick, joinInputs) = server.Replicator.JoinsV2.Single();
        Assert.Equal(server.Host.Tick, joinTick);
        Assert.Equal((1, avatar.dm_id, 0u), (joinInputs.Session, joinInputs.OwnEntity, joinInputs.LastInputSeq));
        Assert.Equal(server.Clients.Connects.Single().Key == "Alice" ? alice.Welcome.Value.OwnEntity : 0, joinInputs.OwnEntity);
        Assert.True(joinInputs.ClientId != 0);
        Assert.Empty(server.Replicator.Joins); // the version-1 call is not used

        server.Tick(3);
        var deltas = alice.Messages(MessageId.Delta).Select(FakeCodec.DecodeDelta).ToArray();
        Assert.Equal(new[] { welcome.Tick + 1, welcome.Tick + 2, welcome.Tick + 3 }, deltas.Select(d => d.Tick));
        Assert.All(deltas, d => Assert.Equal(avatar.dm_id, d.OwnEntity));
        Assert.All(deltas, d => Assert.Equal(BitConverter.GetBytes(d.Tick), d.Body));
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void ALeaveRunsTheDisconnectAndTheMobKeepsItsKey()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        Session s = server.Host.SessionOf(1);
        mob avatar = server.MobOf("Alice");

        alice.Drop();
        server.Tick(); // the poll sees the transport's disconnect and queues the Leave, which this tick's Step applies
        Assert.Equal(SessionState.Closed, s.State);
        Assert.Equal((CommandKind.Leave, 1, QueuedCommand.LeaveSeq), server.Host.Log.Commands.Select(c => (c.Kind, c.Session, c.Seq)).Last());
        Assert.Null(s.Client);
        Assert.Single(server.Clients.Disconnects);
        Assert.Same(avatar, server.MobOf("Alice"));
        Assert.Equal("alice", avatar.ckey); // tg's SSD: the mob keeps its key
        Assert.Null(avatar.client);
        Assert.Contains(1, server.Replicator.Forgotten);
    }

    [Fact]
    public void AReconnectWithTheTokenTakesTheSameMobBack()
    {
        using var server = TestServer.Start();
        FakeRemote first = server.Join("Alice");
        SessionToken token = first.Welcome.Value.Token;
        long mobId = first.Welcome.Value.OwnEntity;
        first.Drop();
        server.Tick(2);

        FakeRemote again = server.Join("Alice", token);
        Welcome welcome = again.Welcome.Value;
        Assert.Equal(2, welcome.Session);       // a new session ...
        Assert.Equal(token, welcome.Token);     // ... of the same player
        Assert.Equal(mobId, welcome.OwnEntity); // the same dm_id
        Assert.Single(server.Clients.Keyed);    // no second avatar
        Assert.Equal(2, server.Clients.Connects.Count);
    }

    [Fact]
    public void ReconnectingWithoutTheTokenOrWithAWrongOneIsRefused()
    {
        using var server = TestServer.Start();
        FakeRemote owner = server.Join("Alice");
        SessionToken token = owner.Welcome.Value.Token;

        FakeRemote noToken = server.Connect("alice"); // the same ckey
        FakeRemote wrong = server.Connect("Alice", new SessionToken(token.Low + 1, token.High));
        FakeRemote stranger = server.Connect("Bob", new SessionToken(99, 99));
        server.Tick();
        Assert.Equal(DisconnectReason.NameInUse, noToken.Rejected?.Reason);
        Assert.Equal(DisconnectReason.BadToken, wrong.Rejected?.Reason); // while the owner is connected
        Assert.Equal(DisconnectReason.BadToken, stranger.Rejected?.Reason);
        Assert.True(owner.IsConnected);
        Assert.Equal(SessionState.Joined, server.Host.SessionOf(1).State);

        // The owner gone (SSD), a wrong token still does not take the mob.
        owner.Drop();
        server.Tick(2);
        FakeRemote thief = server.Connect("Alice", new SessionToken(1234, 5678));
        server.Tick();
        Assert.Equal(DisconnectReason.BadToken, thief.Rejected?.Reason);
    }

    [Fact]
    public void TheRightTokenReplacesALiveSession()
    {
        using var server = TestServer.Start();
        FakeRemote owner = server.Join("Alice");
        FakeRemote again = server.Connect("Alice", owner.Welcome.Value.Token);
        server.Tick(); // the old session is closed with Replaced; its Leave and the new Join apply in the same tick, in ordinal order
        Assert.Equal(DisconnectReason.Replaced, owner.Closed?.Reason);
        server.Tick(2);
        Assert.NotNull(again.Welcome);
        Assert.Equal(owner.Welcome.Value.OwnEntity, again.Welcome.Value.OwnEntity);
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
        Assert.Equal(SessionState.Joined, server.Host.SessionOf(2).State);
        var applied = server.Host.Log.Commands.Where(c => c.Kind is CommandKind.Leave or CommandKind.Join).Select(c => (c.Kind, c.Session)).ToArray();
        Assert.Equal(new[] { (CommandKind.Join, 1), (CommandKind.Leave, 1), (CommandKind.Join, 2) }, applied);
    }

    [Fact]
    public void ATransportTimeoutFollowsTheLeavePath()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Drop(TransportDisconnectKind.Timeout);
        server.Tick(2);
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
        Assert.Single(server.Clients.Disconnects);
        Assert.Contains("(Timeout)", server.Out.ToString());
    }

    [Fact]
    public void ADisconnectBeforeTheJoinAppliedDropsTheJoin()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Connect("Alice");
        alice.DropOnAccept = true; // accepted and gone in the same poll, before the Step applies the Join
        server.Tick();
        server.Tick();
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
        Assert.Empty(server.Clients.Connects);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Session == 1);
    }

    [Fact]
    public void TheWelcomeWaitsForASleepingLoginAndADisconnectDuringItIsHandled()
    {
        using var server = TestServer.Start();
        server.Clients.LoginSleepTicks = 5;
        FakeRemote slow = server.Connect("Slow");
        server.Tick(2); // Join applied, client/New sleeping
        Session s = server.Host.SessionOf(1);
        Assert.Equal(SessionState.LoggingIn, s.State);
        Assert.Null(slow.Welcome);
        server.Tick(3);
        Assert.Null(slow.Welcome);
        server.Tick(2);
        Assert.NotNull(slow.Welcome);
        Assert.Equal(SessionState.Joined, s.State);

        FakeRemote gone = server.Connect("Gone");
        server.Tick(2);
        Session g = server.Host.SessionOf(2);
        Assert.Equal(SessionState.LoggingIn, g.State);
        gone.Drop(); // while client/New sleeps
        server.Tick(2);
        Assert.Equal(SessionState.Closed, g.State);
        Assert.Contains(server.Clients.Disconnects, d => d.Key == "Gone");
        server.Tick(10); // the Login finishes later: nothing is sent, nothing breaks
        Assert.Null(gone.Welcome);
        Assert.Null(g.Client);
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void ARefusedLoginClosesTheSession()
    {
        using var server = TestServer.Start();
        server.Clients.Refuse = true;
        FakeRemote alice = server.Connect("Alice");
        server.Tick(3);
        Assert.Equal(DisconnectReason.Refused, alice.Closed?.Reason);
        Assert.Null(alice.Welcome);
        server.Tick();
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
    }

    [Fact]
    public void MaxPlayersRefusesTheNextNewPlayer()
    {
        var config = TestServer.DefaultConfig();
        using var server = TestServer.Start(new ServerConfig
        {
            Seed = config.Seed, RoomWidth = config.RoomWidth, RoomHeight = config.RoomHeight, Console = false, MaxPlayers = 2,
            StatusEveryTicks = 1000,
        });
        FakeRemote a = server.Join("A1");
        server.Join("B2");
        FakeRemote c = server.Connect("C3");
        server.Tick();
        Assert.Equal(DisconnectReason.ServerFull, c.Rejected?.Reason);
        // A reconnect of a known player replacing its own session is not a new player.
        FakeRemote again = server.Connect("A1", a.Welcome.Value.Token);
        server.Tick(3);
        Assert.NotNull(again.Welcome);
    }

    [Fact]
    public void ChatToAPlayerArrivesAsServerTextAfterTheWelcome()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        using (server.World.Enter())
            Anomaly.Core.DmShim.PresentationSinks.Of(server.World).Messages.Chat(server.MobOf("Alice"),
                Anomaly.Core.DmShim.Dm.list(Anomaly.Core.DmShim.Dm.kv("type", "notice"), Anomaly.Core.DmShim.Dm.kv("html", "<b>hi</b>")));
        server.Tick();
        ServerText text = alice.Messages(MessageId.ServerText).Select(FakeCodec.DecodeServerText).Single(t => t.Type != DevAccess.Type);
        Assert.Equal("notice", text.Type);
        Assert.Equal("<b>hi</b>", text.Html);
        Assert.Contains(alice.Connection.Sent, m => m.Channel == Channel.Control && m.Message[0] == (byte)MessageId.ServerText);
    }
}
