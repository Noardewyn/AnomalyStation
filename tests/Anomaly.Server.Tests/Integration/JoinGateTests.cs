using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV597: without the lobby, a client whose Hello says it loads the world first stays tg's new player (no body) until it sends Loaded, and is
/// late-joined then; a client without the flag (a bot, a test rig) joins at once as before. The replay log keeps the flag on the Join.
/// </summary>
public class JoinGateTests
{
    [Fact]
    public void AClientThatLoadsFirstIsLateJoinedOnlyOnceItSaysItHasLoaded()
    {
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null || !File.Exists(Path.Combine(maps, "map_jsons", "deathmatch_arena.json")))
            return;
        using RealHeadlessWorld w = DeathmatchArenaGameplayTests.Boot(maps);
        var client = new GameClient(w.Network.CreateClient(), "127.0.0.1", w.Server.Port, "Alice", waitsForLoad: true);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        RealHeadlessWorld.Player alice = w.Adopt("Alice", client);
        w.Run(60);
        Assert.Null(w.MobOf(alice)); // still in tg's lobby while its client loads
        Assert.True(w.Host.Sessions.Last(s => s.Key == "Alice").AwaitingLoad);

        RealHeadlessWorld.Player bob = w.Join("Bob"); // no flag: at once
        Assert.NotNull(w.MobOf(bob));

        alice.Session.Loaded();
        w.Run(20);
        Assert.NotNull(w.MobOf(alice));
        Assert.False(w.Host.Sessions.Last(s => s.Key == "Alice").AwaitingLoad);

        CommandLog log = CommandLog.Parse(w.Host.Log.ToText(), out string error);
        Assert.Null(error);
        Assert.Contains(log.Commands, c => c.Kind == CommandKind.Join && c.Text == "Alice" && c.WaitsForLoad);
        Assert.Contains(log.Commands, c => c.Kind == CommandKind.Join && c.Text == "Bob" && !c.WaitsForLoad);
        Assert.Contains(log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.Loaded);
    }
}
