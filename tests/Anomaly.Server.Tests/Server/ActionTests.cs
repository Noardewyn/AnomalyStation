using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// Actions (scope 2b, 2f, 2k; ServerHost.ReceiveAction, FormInputs, ApplyAction): validated and rate-limited when they arrive,
/// held until the tick that takes their frame, then tg's own entry points with usr the player's mob. Clicks need a target
/// the replicator shows; dev commands need DEV_COMMANDS and answer the requester only.
/// </summary>
public class ActionTests
{
    [Fact]
    public void RestWalkRunAndCombatModeReachTgsKeybindingsWithTheMobAsUsr()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        mob avatar = server.MobOf("Alice");
        server.Clients.Actions.Clear();
        alice.Motion(0, 0);
        alice.Action(ActionKind.Rest);
        alice.Action(ActionKind.ToggleMoveIntent);
        alice.Action(ActionKind.ToggleCombatMode);
        server.Tick();
        Assert.Equal(new[] { "rest", "intent", "combat" }, server.Clients.Actions.Select(a => a.Action));
        Assert.All(server.Clients.Actions, a => Assert.Same(avatar, a.Usr));
        Assert.Equal(3, server.Host.SessionOf(1).ActionsApplied);
        var commands = server.Host.Log.Commands.Where(c => c.Kind == CommandKind.Action).ToArray();
        Assert.Equal(new[] { ActionKind.Rest, ActionKind.ToggleMoveIntent, ActionKind.ToggleCombatMode }, commands.Select(c => c.Action));
        Assert.All(commands, c => Assert.Equal(1u, c.Seq)); // the seq of the frame they belong to
    }

    [Fact]
    public void AnActionWaitsForTheTickOfItsFrame()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Clients.Actions.Clear();
        // Frames 1-3 arrive together; the action is tagged with frame 3, so it applies in the tick that takes frame 3.
        alice.Motion(10, 0);
        alice.Motion(20, 0);
        alice.Motion(30, 0);
        alice.Action(ActionKind.Rest); // tagged 3
        long start = server.Host.Tick;
        server.Tick();
        Assert.Empty(server.Clients.Actions);
        Assert.Equal(1, server.Host.SessionOf(1).PendingActionCount);
        server.Tick();
        Assert.Empty(server.Clients.Actions);
        server.Tick();
        var applied = Assert.Single(server.Clients.Actions);
        Assert.Equal(start + 3, applied.Tick);
        Assert.Equal(0, server.Host.SessionOf(1).PendingActionCount);
        // The Motion command of frame 3 comes before the action in that tick's log.
        var log = server.Host.Log.Commands.Where(c => c.ApplyTick == start + 3 && c.Session == 1).Select(c => c.Kind).ToArray();
        Assert.Equal(new[] { CommandKind.Motion, CommandKind.Action }, log);
    }

    [Fact]
    public void AnActionOfAnAlreadyAppliedFrameAppliesAtOnce()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(10, 0);
        server.Tick(3);
        server.Clients.Actions.Clear();
        long now = server.Host.Tick;
        alice.Action(ActionKind.ToggleCombatMode, frameSeq: 1);
        server.Tick();
        Assert.Equal(now + 1, Assert.Single(server.Clients.Actions).Tick);
    }

    [Fact]
    public void AClickNeedsATargetTheReplicatorShowsAndBuildsTgsParams()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        long bobId = server.MobOf("Bob").dm_id;
        server.Host.EnqueueConsoleLine(FormattableString.Invariant($"teleport Bob {server.PositionOf("Alice").X + 1} {server.PositionOf("Alice").Y}")); // a click target is within tg's view range
        server.Tick(2);
        server.Clients.Clicks.Clear();
        alice.Motion(0, 0);
        alice.Action(ActionKind.Click, bobId, ClickButton.Right);
        alice.Action(ActionKind.Click, bobId, ClickButton.Left, ClickModifiers.Shift | ClickModifiers.Alt);
        alice.Action(ActionKind.Click, 9_999_999, ClickButton.Left);  // no such entity
        alice.Action(ActionKind.Click, 1, ClickButton.Left);          // an id that is no entity (the world's first objects are not movables on a turf)
        server.Tick();
        Assert.Equal(new[] { "right=1;button=right", "left=1;shift=1;alt=1;button=left" }, server.Clients.Clicks.Select(c => c.Params));
        Assert.All(server.Clients.Clicks, c => Assert.Same(server.MobOf("Bob"), c.Target));
        Assert.All(server.Clients.Clicks, c => Assert.Same(server.MobOf("Alice"), c.Usr));
        Session s = server.Host.SessionOf(1);
        Assert.Equal(2, s.ClicksRejected);
        Assert.Equal(0, s.Strikes);
    }

    [Fact]
    public void AClickOnSomethingInvisibleOrDeletedIsDropped()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Join("Bob");
        mob bob = server.MobOf("Bob");
        server.Host.EnqueueConsoleLine(FormattableString.Invariant($"teleport Bob {server.PositionOf("Alice").X + 1} {server.PositionOf("Alice").Y}")); // in view: only the invisibility rejects it
        alice.Motion(0, 0);
        server.Tick(2);
        bob.invisibility = Defines.INVISIBILITY_ABSTRACT; // the replicator never shows it
        server.Clients.Clicks.Clear();
        alice.Action(ActionKind.Click, bob.dm_id, ClickButton.Right);
        server.Tick();
        Assert.Empty(server.Clients.Clicks);
        Assert.Equal(1, server.Host.SessionOf(1).ClicksRejected);
    }

    [Theory]
    [InlineData(ActionKind.Click, 0L, ClickButton.Right, ClickModifiers.None, null)]            // a click without a target
    [InlineData(ActionKind.Click, 5L, ClickButton.None, ClickModifiers.None, null)]             // without a button
    [InlineData(ActionKind.Click, 5L, ClickButton.Right, (ClickModifiers)64, null)]             // unknown modifiers
    [InlineData(ActionKind.Click, 5L, ClickButton.Right, ClickModifiers.None, "text")]          // text on a click
    [InlineData(ActionKind.Rest, 5L, ClickButton.None, ClickModifiers.None, null)]              // a target on a toggle
    [InlineData(ActionKind.ToggleCombatMode, 0L, ClickButton.Left, ClickModifiers.None, null)]  // a button on a toggle
    [InlineData(ActionKind.DevCommand, 0L, ClickButton.None, ClickModifiers.None, null)]        // a dev command without a line
    [InlineData(ActionKind.DevCommand, 3L, ClickButton.None, ClickModifiers.None, "kill me")]   // a target on a dev command
    [InlineData(ActionKind.None, 0L, ClickButton.None, ClickModifiers.None, null)]              // no kind
    [InlineData((ActionKind)99, 0L, ClickButton.None, ClickModifiers.None, null)]               // an unknown kind
    public void AnInvalidActionIsAStrikeAndNeverQueued(ActionKind kind, long target, ClickButton button, ClickModifiers modifiers, string text)
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.Action(kind, target, button, modifiers, text);
        server.Tick(2);
        Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
        Assert.Equal(0, server.Host.SessionOf(1).PendingActionCount);
    }

    [Fact]
    public void ThePendingActionsAreCappedAndTheRestIsRateLimitedAsADrop()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        // MaxPendingActions actions of a frame the buffer has not reached wait; more are dropped (and the flood rule counts them).
        uint ahead = 50;
        for (int i = 0; i < 4; i++) alice.Action(ActionKind.ToggleCombatMode, frameSeq: ahead);
        server.Tick();
        for (int i = 0; i < ActionLimits.Click.Burst; i++) alice.Action(ActionKind.Click, 5, ClickButton.Left, frameSeq: ahead);
        server.Tick();
        for (int i = 0; i < ActionLimits.MaxPendingActions - 4 - ActionLimits.Click.Burst; i++)
        {
            alice.Action(ActionKind.Click, 5, ClickButton.Left, frameSeq: ahead);
            server.Tick();
        }
        Session s = server.Host.SessionOf(1);
        Assert.Equal(ActionLimits.MaxPendingActions, s.PendingActionCount);
        alice.Action(ActionKind.Rest, frameSeq: ahead);
        server.Tick();
        Assert.Equal(ActionLimits.MaxPendingActions, s.PendingActionCount);
        Assert.True(s.RateLimited >= 1);
        Assert.Equal(0, s.Strikes);
        // A leaving session's waiting actions go with it.
        alice.Drop();
        server.Tick(2);
        Assert.Equal(0, s.PendingActionCount);
    }

    // ---- dev commands from a client ----

    [Fact]
    public void ADevCommandIsRefusedWithAReplyWhenDevCommandsAreOff()
    {
        using var server = TestServer.Start(TestServer.DefaultConfig(devCommands: false));
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "godmode me");
        server.Tick(2);
        Assert.Contains("Dev commands are off", Texts(alice).Single());
        Assert.Empty(Texts(bob));
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        Assert.False(Dm_HasTrait(server.MobOf("Alice")));
    }

    [Fact]
    public void ADevCommandThatDoesNotParseIsAnsweredAndNeverQueued()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "damage me brute lots");
        alice.Action(ActionKind.DevCommand, text: "levitate me");
        server.Tick(2);
        string[] texts = Texts(alice).ToArray();
        Assert.Equal(2, texts.Length);
        Assert.Contains("damage must be above 0", texts[0]);
        Assert.Contains("unknown dev command", texts[1]);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
    }

    [Fact]
    public void ADevCommandRunsInTheCommandPhaseAndTheReplyGoesToTheRequesterOnly()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "godmode me");
        server.Tick(2);
        Assert.True(Dm_HasTrait(server.MobOf("Alice")));
        Assert.False(Dm_HasTrait(server.MobOf("Bob")));
        Assert.Equal(new[] { "Alice: godmode on" }, Texts(alice).Select(t => System.Net.WebUtility.HtmlDecode(t).Replace(server.MobOf("Alice").name.ToString(), "Alice")));
        Assert.Empty(Texts(bob));
        var command = Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
        Assert.Equal((ActionKind.DevCommand, "godmode me"), (command.Action, command.Text));
        // The speed scale goes to the seam.
        alice.Action(ActionKind.DevCommand, text: "speedscale 0.5");
        server.Tick(2);
        Assert.Equal(0.5f, server.Locomotion.SpeedScale);
    }

    // ---- the console's commands from a client (CLIENT_CONSOLE, apps.md 1.6b) ----

    [Fact]
    public void TheConsolesCommandsAreRefusedFromAClientByDefaultButTeleport()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        var before = server.PositionOf("Alice");
        alice.Action(ActionKind.DevCommand, text: "teleport me 10 10"); // (spawn is a dev command too: the dev menu's spawn panel)
        alice.Action(ActionKind.DevCommand, text: "list");
        server.Tick(2);
        Assert.Equal(2, Texts(alice).Count());
        Assert.Contains(Texts(alice), t => t.Contains("unknown dev command &#39;list&#39;")); // HTML-encoded ServerText
        // the owner (2026-10-26): with the dev commands on, teleport is the players' too
        Assert.Contains(Texts(alice), t => t.Contains("teleported Alice to (10,10,1)"));
        Assert.Equal((10, 10), server.PositionOf("Alice"));
    }

    [Fact]
    public void WithClientConsoleAPlayerSpawnsAnyTypeAtTheirFeetTeleportsListsAndReadsTheStatus()
    {
        using var server = TestServer.Start(TestServer.DefaultConfig(clientConsole: true), strict: false); // the list's key_name of a mob with a client
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "spawn /obj/item/crowbar");
        server.Tick(2);
        var (ax, ay) = server.PositionOf("Alice");
        Assert.Contains(Texts(alice), t => t.Contains($"spawned /obj/item/crowbar") && t.Contains($"at ({ax},{ay},1)"));
        using (server.World.Enter())
            Assert.Single(Dm.each<obj_item_crowbar>(Anomaly.Core.Tg.Builtins.locate(ax, ay, 1).contents));
        var command = Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
        Assert.Equal((ActionKind.DevCommand, "spawn /obj/item/crowbar"), (command.Action, command.Text)); // recorded as the player's action

        alice.Action(ActionKind.DevCommand, text: "teleport me 10 10");
        alice.Action(ActionKind.DevCommand, text: "list");
        alice.Action(ActionKind.DevCommand, text: "status");
        server.Tick(20); // the dev command bucket: bursts of 8
        alice.Action(ActionKind.DevCommand, text: "teleport me");
        server.Tick(2);
        Assert.Equal((10, 10), server.PositionOf("Alice"));
        string[] texts = Texts(alice).ToArray();
        Assert.Contains(texts, t => t.Contains("teleported Alice to (10,10,1)"));
        Assert.Contains(texts, t => t.Contains("list: 2 session(s)"));
        Assert.Contains(texts, t => t.StartsWith("status tick="));
        Assert.Contains(texts, t => t.Contains("usage: teleport"));
        Assert.Empty(Texts(bob)); // the answers go to the typing player only
        Assert.Contains("console (Alice)> teleport me 10 10", server.Out.ToString()); // and to the server's output
    }

    [Fact]
    public void WithClientConsoleAPlayerKicksAnother()
    {
        using var server = TestServer.Start(TestServer.DefaultConfig(clientConsole: true), strict: false);
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "kick Bob go away");
        server.Tick(2);
        Assert.Equal(DisconnectReason.Kicked, bob.Closed?.Reason);
        Assert.Contains("go away", bob.Closed?.Text);
        Assert.Null(alice.Closed);
    }

    [Fact]
    public void ClientConsoleAloneKeepsTheDevCommandsOff()
    {
        using var server = TestServer.Start(TestServer.DefaultConfig(devCommands: false, clientConsole: true), strict: false);
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "godmode me");
        alice.Action(ActionKind.DevCommand, text: "list");
        server.Tick(2);
        string[] texts = Texts(alice).ToArray();
        Assert.Contains("Dev commands are off", texts[0]);
        Assert.Contains(texts, t => t.Contains("list: 1 session(s)"));
        Assert.False(Dm_HasTrait(server.MobOf("Alice")));
    }

    [Fact]
    public void TheDevCommandBucketIsItsOwn()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        for (int i = 0; i < 12; i++) alice.Action(ActionKind.DevCommand, text: "godmode me");
        server.Tick(2);
        Assert.Equal(ActionLimits.Dev.Burst, server.Host.Log.Commands.Count(c => c.Kind == CommandKind.Action));
        Assert.Equal(12 - ActionLimits.Dev.Burst, server.Host.SessionOf(1).RateLimited);
    }

    [Fact]
    public void APlayersDevSpawnsAreCappedAndTheConsolesAreNot()
    {
        using var server = TestServer.Start(TestServer.DefaultConfig());
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        for (int i = 0; i < ServerHost.MaxPlayerDevSpawns + 5; i++)
        {
            alice.Action(ActionKind.DevCommand, text: "spawn /mob/living/carbon/human 9 9");
            server.Tick(6); // under the dev bucket's rate (4 a second)
        }
        server.Tick(3);
        int spawned;
        using (server.World.Enter())
            spawned = Anomaly.Core.DmShim.Dm.each<mob_living_carbon_human>(Anomaly.Core.Tg.Builtins.locate(9, 9, 1).contents).Count();
        Assert.Equal(ServerHost.MaxPlayerDevSpawns, spawned);
        Assert.Contains("the limit of", Texts(alice).Last());
        server.Host.EnqueueConsoleLine("spawn /mob/living/carbon/human 10 10");
        server.Tick(2);
        using (server.World.Enter())
            Assert.Single(Anomaly.Core.DmShim.Dm.each<mob_living_carbon_human>(Anomaly.Core.Tg.Builtins.locate(10, 10, 1).contents));
    }

    private static IEnumerable<string> Texts(FakeRemote remote)
        => remote.Messages(MessageId.ServerText).Select(FakeCodec.DecodeServerText).Where(t => t.Type != DevAccess.Type).Select(t => t.Html); // the lines a player reads

    private static bool Dm_HasTrait(mob m) => Dm.truthy(Builtins_HAS_TRAIT(m));

    private static bool Builtins_HAS_TRAIT(mob m) => TraitHelpers.Has(m);
}

internal static class TraitHelpers
{
    public static bool Has(mob m) => Anomaly.Core.Tg.GlobalProcs.HAS_TRAIT(m, Defines.TRAIT_GODMODE);
}
