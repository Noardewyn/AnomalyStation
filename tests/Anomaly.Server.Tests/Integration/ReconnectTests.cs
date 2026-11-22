using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Replication;
using Anomaly.Tools.Bots;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Reconnect through the whole server (ROADMAP M1 "reconnect works", scope Q3): a player who comes back with the server's
/// token gets the mob that kept its key (tg's SSD) after one <c>Logout</c> and one <c>Login</c>; the right token replaces a
/// live session the same way; a wrong token is refused while the owner plays; a vanished client times out and leaves its
/// mob keyed and without a client.
/// </summary>
public class ReconnectTests
{
    // Registers a recorder of tg's login and logout signals on the avatar of `key`.
    private static datum_test_m1_integration_listener Listen(IntegrationRig rig, string key)
    {
        mob avatar = rig.MobOf(key);
        using (rig.World.Enter())
        {
            var listener = Dm.@new<datum_test_m1_integration_listener>();
            listener.RegisterSignal(avatar, COMSIG_MOB_LOGIN, nameof(listener.on_login));
            listener.RegisterSignal(avatar, COMSIG_MOB_LOGOUT, nameof(listener.on_logout));
            return listener;
        }
    }

    private static int ClientsOf(IntegrationRig rig, string ckey)
    {
        using (rig.World.Enter())
            return Dm.each<client>(GLOB.clients).Count(player => player.ckey == ckey);
    }

    private static void AssertReplicaEqualsTheWorld(IntegrationRig rig, Bot bot)
    {
        List<string> differences = rig.ReplicaDifferences(bot);
        Assert.True(differences.Count == 0, ReplicationRun.Describe(rig.Host.Tick, differences));
    }

    [Fact]
    public void ABotThatLeavesAndComesBackWithItsTokenGetsTheSameMobAfterOneLogoutAndOneLogin()
    {
        using var rig = new IntegrationRig(oracle: true);
        Bot alice = rig.AddBot("Alice");
        rig.UntilJoined(alice);
        rig.Tick(30);
        long avatarId = alice.Welcome.OwnEntity;
        SessionToken token = alice.Token;
        Assert.False(token.IsNone);
        datum_test_m1_integration_listener listener = Listen(rig, "Alice");

        alice.Disconnect();
        rig.Tick(5);
        Assert.Equal(SessionState.Closed, rig.Host.SessionOf(1).State);
        Assert.Equal("Logout", listener.Joined);
        mob avatar = rig.MobOf("Alice");
        Assert.Equal("Alice", avatar.key); // tg's SSD: the mob keeps its key
        Assert.Null(avatar.client);
        Assert.Equal(0, ClientsOf(rig, "alice"));

        long ownMovesBefore = alice.Stats.OwnMoves;
        alice.Connect("127.0.0.1", rig.Server.Port);
        rig.UntilJoined(alice);
        Assert.Equal(avatarId, alice.Welcome.OwnEntity);
        Assert.Equal(token, alice.Welcome.Token);
        Assert.Equal(2, alice.Welcome.Session);
        Assert.Equal("Logout,Login", listener.Joined);
        Assert.Same(avatar, rig.MobOf("Alice"));
        Assert.NotNull(avatar.client);
        Assert.Equal(1, ClientsOf(rig, "alice"));

        // The new replica continues: it equals the world every tick, and the avatar walks again.
        rig.AfterTick = () => AssertReplicaEqualsTheWorld(rig, alice);
        rig.Tick(60);
        Assert.True(alice.Stats.OwnMoves > ownMovesBefore + 5, $"{alice.Stats.OwnMoves - ownMovesBefore} own moves after the reconnect");
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void TheRightTokenReplacesALiveSessionAndTgSeesTheOldClientLogOutBeforeTheNewOneLogsIn()
    {
        using var rig = new IntegrationRig(oracle: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        datum_test_m1_integration_listener listener = Listen(rig, "Alice");

        var again = new RawClient(rig, "Alice", alice.Token);
        for (int i = 0; i < 10 && again.Welcome == null; i++) rig.Tick();

        Assert.NotNull(again.Welcome);
        Assert.Equal(alice.Welcome.OwnEntity, again.Welcome.Value.OwnEntity);
        Assert.Equal(BotState.Disconnected, alice.State);
        Assert.Equal(DisconnectReason.Replaced, alice.LastDisconnect.Reason);
        Assert.Equal(SessionState.Closed, rig.Host.SessionOf(1).State);
        Assert.Equal(SessionState.Joined, rig.Host.SessionOf(2).State);
        Assert.Equal("Logout,Login", listener.Joined); // one each, the old client's first
        Assert.Equal(1, ClientsOf(rig, "alice"));
        Assert.Same(rig.MobOf("Alice").client, rig.Host.SessionOf(2).Client);
        // The old session's Leave and the new one's Join applied in one tick, the Leave first.
        QueuedCommand leave = rig.Host.Log.Commands.Single(c => c.Kind == CommandKind.Leave);
        QueuedCommand join = rig.Host.Log.Commands.Single(c => c.Kind == CommandKind.Join && c.Session == 2);
        Assert.Equal(leave.ApplyTick, join.ApplyTick);
        Assert.Equal(1, leave.Session);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void AWrongTokenOrNoneIsRefusedWhileTheOwnerIsConnected()
    {
        using var rig = new IntegrationRig();
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        datum_test_m1_integration_listener listener = Listen(rig, "Alice");

        var wrong = new RawClient(rig, "Alice", new SessionToken(alice.Token.High ^ 1, alice.Token.Low));
        var none = new RawClient(rig, "ALICE");
        rig.Tick(5);

        Assert.Equal(DisconnectReason.BadToken, wrong.Gone?.Reason);
        Assert.Equal(DisconnectReason.NameInUse, none.Gone?.Reason);
        Assert.Equal(TransportDisconnectKind.Rejected, wrong.GoneKind);
        Assert.Equal(TransportDisconnectKind.Rejected, none.GoneKind);
        Assert.Equal(BotState.Joined, alice.State);
        Assert.Equal("", listener.Joined);
        Assert.Single(rig.Host.Sessions);
        Assert.Same(rig.MobOf("Alice").client, rig.Host.SessionOf(1).Client);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void AVanishedClientTimesOutLeavingItsMobKeyedWithoutAClientAndComesBack()
    {
        using var rig = new IntegrationRig();
        Bot alice = rig.AddBot("Alice");
        rig.UntilJoined(alice);
        rig.Tick(10);
        datum_test_m1_integration_listener listener = Listen(rig, "Alice");

        ((LoopbackClientTransport)alice.Transport).Drop(); // no goodbye: the server learns it from its timeout
        rig.Tick(rig.Network.Options.TimeoutSteps + 5);

        Assert.Equal(SessionState.Closed, rig.Host.SessionOf(1).State);
        Assert.Contains("(Timeout)", rig.Out.ToString());
        Assert.Equal("Logout", listener.Joined);
        mob avatar = rig.MobOf("Alice");
        Assert.Equal("Alice", avatar.key);
        Assert.Equal("alice", avatar.ckey);
        Assert.Null(avatar.client);
        Assert.Equal(0, ClientsOf(rig, "alice"));
        using (rig.World.Enter())
        {
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.Null(GLOB.directory["alice"]);
        }

        // The bot has not noticed (its transport was dropped); it comes back with its token like a player would.
        var back = new RawClient(rig, "Alice", alice.Token);
        for (int i = 0; i < 10 && back.Welcome == null; i++) rig.Tick();
        Assert.Equal(alice.Welcome.OwnEntity, back.Welcome?.OwnEntity);
        Assert.Equal("Logout,Login", listener.Joined);
        using (rig.World.Enter())
            Assert.True(Dm.@in(avatar, GLOB.player_list));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }
}
