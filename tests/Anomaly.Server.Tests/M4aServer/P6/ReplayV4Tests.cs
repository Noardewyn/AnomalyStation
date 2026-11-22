using System.IO;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>The version-4 commands in the replay log (CommandLog.cs, D31): recorded with their target, icon pixel and keybinding, replayed to the same state.</summary>
public class ReplayV4Tests
{
    private static ServerHost Script(TestServer server)
    {
        FakeRemote a = server.Join("Alice");
        FakeRemote b = server.Join("Bob");
        server.Host.EnqueueConsoleLine("teleport Alice 4 4");
        server.Host.EnqueueConsoleLine("teleport Bob 5 4");
        a.Motion(0, 0);
        b.Motion(0, 0);
        server.Tick(2);
        long bob = server.MobOf("Bob").dm_id;
        long alice = server.MobOf("Alice").dm_id;
        // Everything that enters the world is a command (D31): the script makes no object outside one, so a screen click names an id
        // that is not on the screen (the world refuses it, in the live run and in the replay alike).
        const long NotOnTheScreen = 9_999_998;
        for (int t = 0; t < 30; t++)
        {
            switch (t % 6)
            {
                case 0:
                    a.ActionV4(new ActionMessage(0, ActionKind.Click, bob, ClickButton.Left, ClickModifiers.Shift, null) { TargetKind = TargetKind.Entity, IconX = (byte)(t + 1), IconY = 7 });
                    break;
                case 1:
                    a.ActionV4(new ActionMessage(0, ActionKind.Click, 0, ClickButton.Right, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4 + t % 4, TurfY = 4, TurfZ = 1 });
                    break;
                case 2:
                    a.ActionV4(new ActionMessage(0, ActionKind.Click, NotOnTheScreen, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Screen, IconX = 3, IconY = 3 });
                    b.ActionV4(new ActionMessage(0, ActionKind.Hover, alice, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity });
                    break;
                case 3:
                    a.ActionV4(new ActionMessage(0, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = (KeybindingCode)(1 + t % 8), KeyUp = t % 2 == 1 });
                    a.ActionV4(new ActionMessage(0, ActionKind.Click, 9_999_999, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity }); // forged: dropped, never logged
                    break;
                case 4:
                    a.ActionV4(new ActionMessage(0, ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 6, TurfY = 5, TurfZ = 1 });
                    break;
            }

            if (t % 7 == 0) a.Motion((sbyte)(t % 14 == 0 ? 100 : -100), 0, (ushort)(t * 977));
            server.Tick();
        }

        server.Host.RequestStop();
        while (!server.Host.Finished) server.Tick();
        return server.Host;
    }

    [Fact]
    public void TheCommandsKeepTheirVersionFourFieldsAndTheForgedOnesAreNotLogged()
    {
        using var server = TestServer.Start(strict: false);
        ServerHost host = Script(server);
        var actions = host.Log.Commands.Where(c => c.Kind == CommandKind.Action).ToArray();
        Assert.Contains(actions, c => c.Action == ActionKind.Click && c.TargetKind == TargetKind.Entity && c.IconX > 0 && c.Modifiers == ClickModifiers.Shift);
        Assert.Contains(actions, c => c.Action == ActionKind.Click && c.TargetKind == TargetKind.Turf && c.TurfX >= 4 && c.TurfZ == 1);
        Assert.Contains(actions, c => c.Action == ActionKind.Click && c.TargetKind == TargetKind.Screen);
        Assert.Contains(actions, c => c.Action == ActionKind.Keybinding && c.Keybinding != KeybindingCode.None);
        Assert.Contains(actions, c => c.Action == ActionKind.Hover);
        // A forged id is logged as the command the host queued (the world refuses it on replay too), never applied.
        Assert.True(host.SessionOf(1).ClicksRejected > 0);
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
        Assert.NotEmpty(first.Clients.Clicks);
        Assert.NotEmpty(first.Clients.Keybindings);
        Assert.NotEmpty(first.Clients.Hovers);
    }

    [Fact]
    public void RecordThenReplayGivesTheSameHashAndTheSameCalls()
    {
        var file = new StringWriter();
        using var live = TestServer.Start(record: file, strict: false);
        ServerHost host = Script(live);

        CommandLog recorded = CommandLog.Parse(file.ToString(), out string error);
        Assert.Null(error);
        Assert.Equal(host.Log.Commands, recorded.Commands);
        Assert.Contains("c ", file.ToString());

        using var replay = TestServer.Replay(recorded, strict: false);
        replay.Tick(100000);
        Assert.True(replay.Host.Finished);
        Assert.Equal(host.Tick, replay.Host.Tick);
        Assert.Equal(host.FinalHash, replay.Host.FinalHash);
        Assert.Equal(host.Log.Commands, replay.Host.Log.Commands);
        // The calls into tg are the same, tick by tick (the replay has no network and no screen object of its own: the world's
        // objects are the same ids because the same commands ran).
        Assert.Equal(live.Clients.Keybindings.Select(k => (k.Tick, k.Key, k.Code, k.Down)), replay.Clients.Keybindings.Select(k => (k.Tick, k.Key, k.Code, k.Down)));
        Assert.Equal(live.Clients.Hovers.Select(h => (h.Tick, h.Key, h.Target.dm_id)), replay.Clients.Hovers.Select(h => (h.Tick, h.Key, h.Target.dm_id)));
        Assert.NotEmpty(live.Clients.Keybindings);
    }

    [Fact]
    public void TheLogTextRoundTripsTheVersionFourFieldsAndStillReadsM3sLines()
    {
        var log = CommandLog.For(new ServerConfig { Seed = 3 });
        log.Add(new QueuedCommand(5, 1, 3, CommandKind.Action, null, Action: ActionKind.Click, Target: 0, Button: ClickButton.Right, Modifiers: ClickModifiers.Alt,
            TargetKind: TargetKind.Turf, TurfX: 255, TurfY: 7, TurfZ: 16, IconX: 32, IconY: 1));
        log.Add(new QueuedCommand(5, 1, 3, CommandKind.Action, null, Action: ActionKind.Click, Target: 44, Button: ClickButton.Left, TargetKind: TargetKind.Screen, IconX: 9, IconY: 9));
        log.Add(new QueuedCommand(6, 1, 4, CommandKind.Action, null, Action: ActionKind.Keybinding, Keybinding: KeybindingCode.HoldThrowMode, KeyUp: true));
        log.Add(new QueuedCommand(6, 1, 4, CommandKind.Action, null, Action: ActionKind.Hover, Target: 12, TargetKind: TargetKind.Entity));
        log.Finish(6, 1);
        string text = log.ToText();
        Assert.Contains("c 5 1 3 Action a4,0,2,4,2,255,7,16,32,1,0,0 -", text);
        Assert.Contains("c 6 1 4 Action a6,0,0,0,0,0,0,0,0,0,8,1 -", text);
        Assert.Contains("c 6 1 4 Action a7,12,0,0,1,0,0,0,0,0,0,0 -", text);
        CommandLog back = CommandLog.Parse(text, out string error);
        Assert.Null(error);
        Assert.Equal(log.Commands, back.Commands);

        // M3's four-value line (a click on an entity) reads as before: an entity click without the version-4 fields.
        CommandLog old = CommandLog.Parse("format 2\nc 3 1 2 Action a4,77,1,0 -\n", out error);
        Assert.Null(error);
        QueuedCommand c = Assert.Single(old.Commands);
        Assert.Equal((ActionKind.Click, 77L, ClickButton.Left, TargetKind.None), (c.Action, c.Target, c.Button, c.TargetKind));
        // Too few or non-numeric values are refused.
        Assert.Null(CommandLog.Parse("format 2\nc 3 1 2 Action a4,77,1,0,9,x -\n", out error));
        Assert.Contains("line 2", error);
    }
}
