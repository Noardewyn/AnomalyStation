using System.IO;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// Determinism with network input (scope 2j, D31): commands are stamped with the next tick and applied in (session ordinal,
/// seq) order whatever their arrival order; two runs of the same script give the same command stream and hash; the
/// recorded stream replayed into a fresh world without network gives the same final hash.
/// </summary>
public class ReplayTests
{
    // A scripted session: joins, inputs that arrive in mixed order, a reconnect, a kick, console commands, a leave.
    private static ServerHost Script(TestServer server)
    {
        FakeRemote a = server.Join("Alice");
        FakeRemote b = server.Join("Bob");
        FakeRemote c = server.Join("Carol");
        for (int t = 0; t < 40; t++)
        {
            // Bob's and Carol's frames arrive before Alice's in the same poll.
            if (t % 7 == 0) c.Motion(t % 14 == 0 ? (sbyte)-100 : (sbyte)0, 0);
            if (t % 5 == 0) b.Motion(0, t % 10 == 0 ? (sbyte)100 : (sbyte)-100);
            if (t % 3 == 0) a.Motion((sbyte)((t / 3 % 4) switch { 0 => 100, 2 => -100, _ => 0 }), (sbyte)((t / 3 % 4) switch { 1 => 100, 3 => -100, _ => 0 }), (ushort)(t * 311));
            if (t == 18) a.Action(ActionKind.ToggleMoveIntent);
            if (t == 12) server.Host.EnqueueConsoleLine("spawn /obj 6 6");
            if (t == 15) server.Host.EnqueueConsoleLine("teleport Bob 10 10");
            if (t == 20) b.Drop();
            if (t == 25) server.Join("Bob", b.Welcome.Value.Token);
            if (t == 30) server.Host.EnqueueConsoleLine("kick Carol bye");
            server.Tick();
        }
        server.Host.RequestStop();
        while (!server.Host.Finished) server.Tick();
        return server.Host;
    }

    [Fact]
    public void CommandsOfATickApplyInSessionThenSeqOrderWhateverTheArrivalOrder()
    {
        using var server = TestServer.Start();
        FakeRemote a = server.Join("Alice");
        FakeRemote b = server.Join("Bob");
        server.Locomotion.Frames.Clear();
        // Bob's frame and action arrive before Alice's in the same poll; the tick applies Alice's first (session 1), each
        // session's Motion before its action.
        b.Motion(0, 100);
        b.Action(ActionKind.ToggleCombatMode);
        a.Motion(100, 0);
        a.Action(ActionKind.Rest);
        server.Tick();
        Assert.Equal(new[] { "Alice", "Bob" }, server.Locomotion.Frames.Select(f => ((client_test_m1_server_client)server.MobOf(f.Mob == server.MobOf("Alice").dm_id ? "Alice" : "Bob").client).key));
        Assert.Equal(new[] { ("Alice", "rest"), ("Bob", "combat") }, server.Clients.Actions.Select(x => (x.Key, x.Action)));
        var order = server.Host.Log.Commands.Where(c => c.Kind is CommandKind.Motion or CommandKind.Action).Select(c => (c.Session, c.Kind)).ToArray();
        Assert.Equal(new[] { (1, CommandKind.Motion), (1, CommandKind.Action), (2, CommandKind.Motion), (2, CommandKind.Action) }, order);
    }

    [Fact]
    public void TwoRunsOfOneScriptGiveTheSameCommandsAndHash()
    {
        using var first = TestServer.Start(strict: false);
        using var second = TestServer.Start(strict: false);
        ServerHost a = Script(first);
        ServerHost b = Script(second);
        Assert.Equal(a.Log.Commands, b.Log.Commands);
        Assert.Equal(a.FinalHash, b.FinalHash);
        Assert.Equal(a.Tick, b.Tick);
    }

    [Fact]
    public void RecordThenReplayGivesTheSameFinalHash()
    {
        var file = new StringWriter();
        using var live = TestServer.Start(record: file, strict: false);
        ServerHost host = Script(live);
        Assert.Contains(host.Log.Commands, c => c.Kind == CommandKind.Console && c.Text == "kick Carol bye");
        Assert.Contains(host.Log.Commands, c => c.Kind == CommandKind.Console && c.Text == "quit");
        Assert.Contains(host.Log.Commands, c => c.Kind == CommandKind.Leave);
        Assert.Equal(DisconnectReason.Kicked, host.Sessions.Single(s => s.Key == "Carol").CloseReason);

        CommandLog recorded = CommandLog.Parse(file.ToString(), out string error);
        Assert.Null(error);
        Assert.Equal(host.Log.Commands, recorded.Commands);
        Assert.Equal(host.Tick, recorded.EndTick);
        Assert.Equal(host.FinalHash, recorded.FinalHash);

        using var replay = TestServer.Replay(recorded, strict: false);
        replay.Tick(100000);
        Assert.True(replay.Host.Finished);
        Assert.Equal(host.Tick, replay.Host.Tick);
        Assert.Equal(host.FinalHash, replay.Host.FinalHash);
        Assert.Equal(host.Log.Commands, replay.Host.Log.Commands);
        Assert.Equal(live.Clients.Actions.Select(h => (h.Tick, h.Key, h.Action)), replay.Clients.Actions.Select(h => (h.Tick, h.Key, h.Action)));
        Assert.Equal(live.Locomotion.Frames.Select(f => (f.Tick, f.Mob, f.Frame)), replay.Locomotion.Frames.Select(f => (f.Tick, f.Mob, f.Frame)));
        Assert.NotEmpty(live.Locomotion.Frames);
    }

    [Fact]
    public void AReplayOfAnEditedLogDiffers()
    {
        var file = new StringWriter();
        using var live = TestServer.Start(record: file, strict: false);
        ServerHost host = Script(live);
        string text = file.ToString().Replace("teleport Bob 10 10", "teleport Bob 11 10");
        using var replay = TestServer.Replay(CommandLog.Parse(text, out _), strict: false);
        replay.Tick(100000);
        Assert.NotEqual(host.FinalHash, replay.Host.FinalHash);
    }
}
