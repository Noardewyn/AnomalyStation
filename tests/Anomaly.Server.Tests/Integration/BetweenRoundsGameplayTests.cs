using Anomaly.Client.Shared;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D306 through the real server (the loopback network, its own port): tg's world/Reboot (the dev <c>reboot now</c>) keeps both players'
/// connections; while the next world is built on a worker they wait between rounds;
/// then the same clients, never connecting again, are joined into the new world's lobby.
/// </summary>
public class BetweenRoundsGameplayTests
{
    private static void RunUntil(RealHeadlessWorld w, Func<bool> until, int maxTicks, string what)
    {
        for (int i = 0; i < maxTicks && !until(); i++)
            w.Run(1);
        Assert.True(until(), what + "\n" + w.Out + "\n" + w.Err);
    }

    [Fact]
    public void A_restart_keeps_the_connections_and_joins_both_into_the_new_lobby()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = true, Lobby = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        IConnection aliceConnection = w.Host.LiveSession("Alice").Connection;
        int aliceOrdinal = alice.Client.Session;
        World first = w.World;

        alice.Session.DevCommand("reboot now");
        RunUntil(w, () => w.Host.Restarts == 1, 200, "world/Reboot asked the host for a new round");
        RunUntil(w, () => alice.Client.Phase == FeedPhase.BetweenRounds && bob.Client.Phase == FeedPhase.BetweenRounds, 20, "both clients wait between rounds");
        Assert.Equal(ServerHost.RoundRestartText, alice.Client.BetweenRoundsStatus);
        Session waiting = w.Host.LiveSession("Alice");
        Assert.Same(aliceConnection, waiting.Connection);
        Assert.True(aliceConnection.IsConnected, "the connection stays");

        // the new world opens its joins: the same clients (a GameClient connects once) land in its lobby
        RunUntil(w, () => alice.Client.Phase == FeedPhase.Playing && bob.Client.Phase == FeedPhase.Playing, 2000, "both clients play the next round");
        Assert.False(w.Host.BuildingNextWorld);
        Assert.NotSame(first, w.World);
        Assert.NotEqual(aliceOrdinal, alice.Client.Session);
        Assert.True(aliceConnection.IsConnected);
        foreach (string name in new[] { "Alice", "Bob" })
        {
            Assert.Equal(SessionState.Joined, w.Host.LiveSession(name).State);
            Assert.True(w.World.Objects.TryGet(w.Host.AvatarOf(name), out object mob));
            Assert.IsType<mob_dead_new_player>(mob);
        }
        Assert.Equal(w.Host.AvatarOf("Alice"), alice.Client.OwnEntity);
    }
}
