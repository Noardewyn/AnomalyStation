using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Replication;
using Anomaly.Tools.Bots;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The console's world commands through the real tg world (ROADMAP M1 "server console commands", D73), strict: spawn, teleport,
/// list and kick on tg's own primitives (<c>new</c> with <c>ADMIN_SPAWNED_1</c>, <c>abstract_move</c>, <c>key_name</c>,
/// <c>to_chat</c> and <c>qdel(client)</c>, <c>log_admin</c>), seen by the bots as the replication shows them.
/// </summary>
public class ConsoleTests
{
    private static int AdminLogLines(IntegrationRig rig)
    {
        using (rig.World.Enter())
            return (int)GLOB.admin_activities.len;
    }

    [Fact]
    public void SpawnPutsAnObjectInTheWorldThatEveryBotReplicates()
    {
        using var rig = new IntegrationRig(oracle: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        SpawnPoint at = rig.Host.Room.SpawnPoints[5];

        rig.Host.EnqueueConsoleLine($"spawn /obj {at.X} {at.Y}");
        rig.Tick(2);

        Assert.Contains("spawned /obj #", rig.Out.ToString());
        EntityView spawned = rig.Truth.Values.Single(e => e.Type == "/obj");
        Assert.Equal((at.X, at.Y, at.Z), (spawned.X, spawned.Y, spawned.Z));
        Assert.True(rig.World.Objects.TryGet(spawned.Id, out object o));
        Assert.True(Dm.truthy(Dm.bitand(((atom)o).flags_1, Defines.ADMIN_SPAWNED_1)));
        foreach (Bot bot in new[] { alice, bob })
        {
            Assert.True(bot.Replica.TryGet(spawned.Id, out ReplicaEntity e), bot.Options.Name + " does not see the spawned /obj");
            Assert.Equal("/obj", e.TypePath);
            Assert.Empty(rig.ReplicaDifferences(bot));
        }
        Assert.Equal(1, AdminLogLines(rig));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void TeleportMovesAPlayersMobAndItsBotSeesItArrive()
    {
        using var rig = new IntegrationRig(oracle: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        SpawnPoint to = rig.Host.Room.SpawnPoints[7];
        mob avatar = rig.MobOf("Alice");
        Assert.NotEqual((to.X, to.Y), ((int)avatar.x, (int)avatar.y));

        rig.Host.EnqueueConsoleLine($"teleport Alice {to.X} {to.Y}");
        rig.Tick(2);

        Assert.Equal((to.X, to.Y, to.Z), ((int)avatar.x, (int)avatar.y, (int)avatar.z));
        Assert.True(alice.Replica.TryGet(alice.Welcome.OwnEntity, out ReplicaEntity own));
        Assert.Equal((to.X, to.Y), (own.State.X, own.State.Y));
        Assert.Empty(rig.ReplicaDifferences(alice));
        Assert.Contains("teleported Alice to", rig.Out.ToString());
        Assert.Equal(1, AdminLogLines(rig));
        // A movable by its world id, too.
        rig.Host.EnqueueConsoleLine($"teleport #{avatar.dm_id} {to.X + 1} {to.Y}");
        rig.Tick(2);
        Assert.Equal(to.X + 1, (int)avatar.x);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void ListShowsTheSessionsWithTgsKeyName()
    {
        using var rig = new IntegrationRig();
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);

        rig.Host.EnqueueConsoleLine("list");
        rig.Tick(2);

        string output = rig.Out.ToString();
        // tg's key_name of a mob with a client: its key, then "/(name)" (code/__HELPERS/logging/_logging.dm).
        Assert.Contains($"#1 Alice Joined mob #{alice.Welcome.OwnEntity} Alice/(", output); // the human's name is its own (M2: tg's)
        Assert.Contains($"#2 Bob Joined mob #{bob.Welcome.OwnEntity} Bob/(", output);
        Assert.Contains("list: 2 session(s)", output);
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }

    [Fact]
    public void KickDeletesTheClientAndTheKickedBotGetsItsReason()
    {
        using var rig = new IntegrationRig();
        Bot alice = rig.AddBot("Alice");
        Bot bob = rig.AddBot("Bob");
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        rig.Tick(10);
        mob avatar = rig.MobOf("Alice");
        long textsBefore = alice.Stats.ServerTexts;

        rig.Host.EnqueueConsoleLine("kick Alice spamming the corridor");
        rig.Tick();

        // tg's qdel(client) runs client/Del inside qdel (BYOND's del), so the host learns it in the kick's own tick and
        // closes the session after that tick: the bot is gone in the same tick.
        long kickTick = rig.Host.Tick;
        Assert.Contains($"closed #1 Alice at tick {kickTick}: Kicked", rig.Out.ToString());
        Assert.Equal(BotState.Disconnected, alice.State);
        rig.Tick(2);
        Assert.Equal(BotState.Disconnected, alice.State);
        Assert.Equal(DisconnectReason.Kicked, alice.LastDisconnect.Reason);
        Assert.Contains("spamming the corridor", alice.LastDisconnect.Text);
        Assert.True(alice.Stats.ServerTexts > textsBefore, "the kicked player got no message from tg's to_chat");
        // tg's qdel(client): the client is gone, the mob keeps its key (BYOND's client/Del), the session closed.
        Assert.Null(avatar.client);
        Assert.Equal("Alice", avatar.key);
        Assert.Equal(SessionState.Closed, rig.Host.SessionOf(1).State);
        Assert.Equal(BotState.Joined, bob.State);
        Assert.Equal(1, AdminLogLines(rig));
        Assert.True(rig.World.RuntimeErrorCount == 0, rig.Runtimes);
    }
}
