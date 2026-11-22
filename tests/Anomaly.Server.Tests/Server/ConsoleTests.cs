using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>The server console (D73 core subset, scope 2h): parsing, and the commands applied in the world.</summary>
public class ConsoleTests
{
    // ---- parsing ----

    [Theory]
    [InlineData("spawn /obj/item 5 6", ConsoleVerb.Spawn, 3)]
    [InlineData("spawn /obj", ConsoleVerb.Spawn, 1)]
    [InlineData("SPAWN /mob 5 6 1", ConsoleVerb.Spawn, 4)]
    [InlineData("teleport Alice 10 12", ConsoleVerb.Teleport, 3)]
    [InlineData("tp #42 10 12 1", ConsoleVerb.Teleport, 4)]
    [InlineData("tp me #42", ConsoleVerb.Teleport, 2)]
    [InlineData("  list  ", ConsoleVerb.List, 0)]
    [InlineData("who", ConsoleVerb.List, 0)]
    [InlineData("kick Alice", ConsoleVerb.Kick, 1)]
    [InlineData("kick Alice spamming the chat", ConsoleVerb.Kick, 4)]
    [InlineData("status", ConsoleVerb.Status, 0)]
    [InlineData("hash", ConsoleVerb.Hash, 0)]
    [InlineData("quit", ConsoleVerb.Quit, 0)]
    [InlineData("help", ConsoleVerb.Help, 0)]
    [InlineData("skill-panel Bob", ConsoleVerb.SkillPanel, 1)] // gaps SL1
    public void ParsesTheVerbsAndTheirArguments(string line, ConsoleVerb verb, int args)
    {
        Assert.True(ConsoleCommand.TryParse(line, out ConsoleCommand command, out string error), error);
        Assert.Equal(verb, command.Verb);
        Assert.Equal(args, command.Args.Count);
        Assert.Equal(line, command.Line);
    }

    [Theory]
    [InlineData("", "empty line")]
    [InlineData("levitate", "unknown command 'levitate'")]
    [InlineData("spawn", "usage: spawn")]
    [InlineData("spawn obj 1 1", "not a type path")]
    [InlineData("spawn /obj 1", "usage: spawn")]
    [InlineData("spawn /obj 0 5", "not a map coordinate")]
    [InlineData("spawn /obj 5 999", "not a map coordinate")]
    [InlineData("teleport Alice 5", "usage: teleport")]
    [InlineData("teleport Alice x 5", "not a map coordinate")]
    [InlineData("kick", "usage: kick")]
    [InlineData("skill-panel", "usage: skill-panel")] // gaps SL1
    [InlineData("list now", "takes no arguments")]
    [InlineData("quit\u0000", "control characters")]
    public void RefusesBadLines(string line, string error)
    {
        Assert.False(ConsoleCommand.TryParse(line, out _, out string why));
        Assert.Contains(error, why);
    }

    [Fact]
    public void ArgumentHelpers()
    {
        Assert.True(ConsoleCommand.TryParse("teleport Bob 3 4", out var c, out _));
        Assert.Equal((3, 4, 1), c.CoordinatesAt(1));
        Assert.True(ConsoleCommand.TryParse("kick Bob too  many words", out c, out _));
        Assert.Equal("too many words", c.KickReason);
        Assert.True(c.IsWorldCommand);
        Assert.True(ConsoleCommand.TryParse("status", out c, out _));
        Assert.False(c.IsWorldCommand);
    }

    // ---- in the world ----

    [Fact]
    public void SpawnMakesTheTypeAtTheTurfFlaggedAdminSpawnedAndIsRecorded()
    {
        using var server = TestServer.Start();
        server.Host.EnqueueConsoleLine("spawn /obj 5 6");
        server.Tick();
        Assert.Contains(server.Host.Log.Commands, c => c.Kind == CommandKind.Console && c.Session == 0 && c.Text == "spawn /obj 5 6");
        string line = server.Out.ToString().Split('\n').Single(l => l.StartsWith("spawned /obj #"));
        long id = long.Parse(line.Split('#')[1].Split(' ')[0]);
        Assert.True(server.World.Objects.TryGet(id, out object o));
        var spawned = (obj)o;
        Assert.Equal(5f, spawned.x);
        Assert.Equal(6f, spawned.y);
        Assert.True(Dm.truthy(Dm.bitand(spawned.flags_1, Defines.ADMIN_SPAWNED_1)));
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void SpawnRefusesTurfsAndUnknownTypes()
    {
        using var server = TestServer.Start();
        server.Host.EnqueueConsoleLine("spawn /turf/closed/indestructible 5 5");
        server.Host.EnqueueConsoleLine("spawn /obj/no_such_thing 5 5");
        server.Tick();
        Assert.Equal(2, server.Out.ToString().Split('\n').Count(l => l.StartsWith("spawn: '")));
    }

    [Fact]
    public void TeleportMovesAPlayersMob()
    {
        // Lenient: a mob's abstract_move asks tg's is_secret_level (z-level traits, M3) and the log line tg's key_name of a
        // mob with a client (M1 P1); a running server reports each once and carries on.
        using var server = TestServer.Start(strict: false);
        server.Join("Alice");
        server.Host.EnqueueConsoleLine("teleport alice 10 11");
        server.Tick();
        Assert.Equal((10, 11), server.PositionOf("Alice"));
        server.Host.EnqueueConsoleLine("teleport #" + server.MobOf("Alice").dm_id + " 4 4");
        server.Host.EnqueueConsoleLine("teleport Nobody 4 4");
        server.Tick();
        Assert.Equal((4, 4), server.PositionOf("Alice"));
        Assert.Contains("teleport: no player or movable 'Nobody'", server.Out.ToString());
    }

    [Fact]
    public void TeleportOntoAPlayerMovesToTheirTurfEitherWay()
    {
        using var server = TestServer.Start(strict: false); // lenient as above
        server.Join("Alice");
        server.Join("Bob");
        server.Host.EnqueueConsoleLine("teleport alice 10 11");
        server.Host.EnqueueConsoleLine("teleport bob 4 4");
        server.Tick();
        server.Host.EnqueueConsoleLine("teleport alice #" + server.MobOf("Bob").dm_id);
        server.Tick();
        Assert.Equal((4, 4), server.PositionOf("Alice"));
        server.Host.EnqueueConsoleLine("teleport alice 10 11");
        server.Tick();
        server.Host.EnqueueConsoleLine("teleport bob alice");
        server.Host.EnqueueConsoleLine("teleport bob Nobody");
        server.Tick();
        Assert.Equal((10, 11), server.PositionOf("Bob"));
        Assert.Contains("teleport: no player or movable 'Nobody'", server.Out.ToString());
    }

    [Fact]
    public void KickDeletesTheClientAndClosesTheSessionWithTheReason()
    {
        using var server = TestServer.Start(strict: false); // tg's key_name of a mob with a client is M1 P1's
        FakeRemote alice = server.Join("Alice");
        server.Host.EnqueueConsoleLine("kick Alice go away");
        server.Tick();
        Assert.Equal(DisconnectReason.Kicked, alice.Closed?.Reason);
        Assert.Contains("go away", alice.Closed?.Text);
        server.Tick();
        Session s = server.Host.SessionOf(1);
        Assert.Equal(SessionState.Closed, s.State);
        Assert.Null(s.Client);
        Assert.Null(server.MobOf("Alice").client);
    }

    [Fact]
    public void ListShowsTheSessions()
    {
        using var server = TestServer.Start(strict: false);
        server.Join("Alice");
        server.Host.EnqueueConsoleLine("list");
        server.Tick();
        string output = server.Out.ToString();
        Assert.Contains("#1 Alice Joined mob #" + server.MobOf("Alice").dm_id, output);
        Assert.Contains("list: 1 session(s)", output);
    }

    [Fact]
    public void StatusHashAndHelpRunOnTheHostAndAreNotRecorded()
    {
        using var server = TestServer.Start();
        int before = server.Host.Log.Commands.Count;
        server.Host.EnqueueConsoleLine("status");
        server.Host.EnqueueConsoleLine("hash");
        server.Host.EnqueueConsoleLine("help");
        server.Host.EnqueueConsoleLine("nonsense");
        server.Tick();
        string output = server.Out.ToString();
        Assert.Contains("status tick=", output);
        Assert.Contains("joins=open", output);
        Assert.Contains("hash tick=", output);
        Assert.Contains("Console commands", output);
        Assert.Contains("console: unknown command 'nonsense'", output);
        Assert.Equal(before, server.Host.Log.Commands.Count);
    }

    [Fact]
    public void QuitKicksEveryoneRunsWorldDelAndEndsTheRun()
    {
        using var server = TestServer.Start(strict: false);
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        server.Host.EnqueueConsoleLine("quit");
        server.Tick();
        Assert.True(server.Host.Finished);
        Assert.True(server.Host.Stopped);
        Assert.Equal(DisconnectReason.ServerShutdown, alice.Closed?.Reason);
        Assert.Equal(DisconnectReason.ServerShutdown, bob.Closed?.Reason);
        Assert.Equal(new[] { "Alice", "Bob" }, server.Clients.Disconnects.Select(d => d.Key)); // in ordinal order
        Assert.True(server.Transport.Stopped);
        Assert.Contains("shutdown at tick " + server.Host.Tick, server.Out.ToString());
        Assert.Contains("final tick=" + server.Host.Tick, server.Out.ToString());
        Assert.Equal("quit", server.Host.Log.Commands.Last().Text);
        Assert.Throws<InvalidOperationException>(() => server.Host.RunTick());
    }

    [Fact]
    public void RequestsAfterTheShutdownStartedAreRefused()
    {
        using var server = TestServer.Start(strict: false);
        server.Host.RequestStop();
        FakeRemote late = server.Connect("Late");
        server.Tick();
        Assert.Equal(DisconnectReason.ServerShutdown, late.Rejected?.Reason);
    }
}
