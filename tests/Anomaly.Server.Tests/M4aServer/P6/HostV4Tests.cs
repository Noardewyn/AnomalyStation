using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>
/// The host's version-4 actions (ServerHost.Network.cs, ServerHost.World.cs; interaction.md section 2, D40): the target of a
/// click or hover is checked in the command phase, a forged or stale one is dropped and counted (never run, never a strike),
/// a malformed one is a strike; the keybinding and hover buckets; one hover per tick; the screentip sink; click_ms.
/// </summary>
public class HostV4Tests
{
    private static ActionMessage Click(TargetKind kind, long target = 0, int x = 0, int y = 0, int z = 0, byte iconX = 0, byte iconY = 0,
        ClickButton button = ClickButton.Left, ClickModifiers modifiers = ClickModifiers.None)
        => new(0, ActionKind.Click, target, button, modifiers, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z, IconX = iconX, IconY = iconY };

    private static ActionMessage Hover(TargetKind kind, long target = 0, int x = 0, int y = 0, int z = 0)
        => new(0, ActionKind.Hover, target, ClickButton.None, ClickModifiers.None, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z };

    private static ActionMessage Key(KeybindingCode code, bool up = false)
        => new(0, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = code, KeyUp = up };

    // Alice at (4, 4) and Bob east of her by `gap` tiles, both stood still.
    private static (TestServer Server, FakeRemote Alice, FakeRemote Bob, mob AliceMob, mob BobMob) Pair(int gap = 1)
    {
        TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        server.Host.EnqueueConsoleLine("teleport Alice 4 4");
        server.Host.EnqueueConsoleLine(FormattableString.Invariant($"teleport Bob {4 + gap} 4"));
        alice.Motion(0, 0);
        bob.Motion(0, 0);
        server.Tick(2);
        server.Clients.Clicks.Clear();
        return (server, alice, bob, server.MobOf("Alice"), server.MobOf("Bob"));
    }

    // ---- entity targets ----

    [Fact]
    public void AnEntityClickInViewRunsWithTgsIconPixelsInItsParams()
    {
        var (server, alice, _, aliceMob, bobMob) = Pair();
        using (server)
        {
            alice.ActionV4(Click(TargetKind.Entity, bobMob.dm_id, iconX: 16, iconY: 9, button: ClickButton.Right, modifiers: ClickModifiers.Ctrl));
            server.Tick();
            var click = Assert.Single(server.Clients.Clicks);
            Assert.Same(bobMob, click.Target);
            Assert.Same(aliceMob, click.Usr);
            Assert.Equal("icon-x=16;icon-y=9;right=1;ctrl=1;button=right", click.Params);
            Assert.Equal(0, server.Host.SessionOf(1).ClicksRejected);
            var command = server.Host.Log.Commands.Last(c => c.Kind == CommandKind.Action);
            Assert.Equal((TargetKind.Entity, (byte)16, (byte)9), (command.TargetKind, command.IconX, command.IconY));
        }
    }

    [Fact]
    public void AnEntityBeyondTgsViewRangeIsDroppedNotAStrike()
    {
        // get_dist 7 is in view (a 15 x 15 view); 8 is not.
        var (server, alice, _, _, bobMob) = Pair(gap: 7);
        using (server)
        {
            alice.ActionV4(Click(TargetKind.Entity, bobMob.dm_id));
            server.Tick();
            Assert.Single(server.Clients.Clicks);
            server.Host.EnqueueConsoleLine("teleport Bob 12 4");
            server.Tick(2);
            server.Clients.Clicks.Clear();
            alice.ActionV4(Click(TargetKind.Entity, bobMob.dm_id));
            alice.ActionV4(Hover(TargetKind.Entity, bobMob.dm_id));
            server.Tick();
            Assert.Empty(server.Clients.Clicks);
            Assert.Empty(server.Clients.Hovers);
            Session s = server.Host.SessionOf(1);
            Assert.Equal(2, s.ClicksRejected);
            Assert.Equal(0, s.Strikes);
        }
    }

    [Theory]
    [InlineData(1L)]            // the world's first object: no movable on a turf
    [InlineData(9_999_999L)]    // no such object
    public void AForgedEntityIdIsDroppedNotAStrike(long id)
    {
        var (server, alice, _, _, _) = Pair();
        using (server)
        {
            alice.ActionV4(Click(TargetKind.Entity, id));
            alice.ActionV4(Hover(TargetKind.Entity, id));
            server.Tick();
            Assert.Empty(server.Clients.Clicks);
            Assert.Empty(server.Clients.Hovers);
            Assert.Equal(2, server.Host.SessionOf(1).ClicksRejected);
            Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        }
    }

    // ---- turf targets ----

    [Fact]
    public void ATurfClickInViewIsTgsLocateAndCarriesTheIconPixel()
    {
        var (server, alice, _, _, _) = Pair();
        using (server)
        {
            alice.ActionV4(Click(TargetKind.Turf, x: 7, y: 5, z: 1, iconX: 32, iconY: 1));
            server.Tick();
            var click = Assert.Single(server.Clients.Clicks);
            var turf = Assert.IsAssignableFrom<turf>(click.Target);
            Assert.Equal((7f, 5f, 1f), (turf.x, turf.y, turf.z));
            Assert.Equal("icon-x=32;icon-y=1;left=1;button=left", click.Params);
            alice.ActionV4(Hover(TargetKind.Turf, x: 5, y: 6, z: 1));
            server.Tick();
            Assert.Equal((5f, 6f), (Assert.IsAssignableFrom<turf>(Assert.Single(server.Clients.Hovers).Target).x, ((turf)server.Clients.Hovers[0].Target).y));
        }
    }

    [Theory]
    [InlineData(200, 200, 1)]   // inside the protocol's range, outside the room
    [InlineData(4, 4, 2)]       // another level (the room has one)
    [InlineData(12, 4, 1)]      // 8 tiles away
    [InlineData(4, 13, 1)]      // 9 tiles away
    [InlineData(4, 4, 16)]      // the highest level the protocol knows
    public void ATurfOutsideTheMapTheLevelOrTheViewIsDroppedNotAStrike(int x, int y, int z)
    {
        var (server, alice, _, _, _) = Pair();
        using (server)
        {
            alice.ActionV4(Click(TargetKind.Turf, x: x, y: y, z: z));
            alice.ActionV4(Hover(TargetKind.Turf, x: x, y: y, z: z));
            server.Tick();
            Assert.Empty(server.Clients.Clicks);
            Assert.Empty(server.Clients.Hovers);
            Assert.Equal(2, server.Host.SessionOf(1).ClicksRejected);
            Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        }
    }

    // ---- screen targets ----

    private static obj ScreenObject(TestServer server, mob owner, string loc)
    {
        obj o = null;
        using (server.World.Enter())
        {
            o = Dm.@new<obj>(null);
            o.screen_loc = loc;
            owner.client.screen.Add(o);
        }
        return o;
    }

    [Fact]
    public void AScreenClickNeedsAnObjectOfThePlayersOwnScreenAndCarriesItsScreenLoc()
    {
        var (server, alice, bob, aliceMob, bobMob) = Pair();
        using (server)
        {
            obj mine = ScreenObject(server, aliceMob, "WEST+1,SOUTH");
            obj his = ScreenObject(server, bobMob, "EAST,SOUTH");
            alice.ActionV4(Click(TargetKind.Screen, mine.dm_id, iconX: 3, iconY: 4));
            alice.ActionV4(Click(TargetKind.Screen, his.dm_id));         // another player's screen object
            alice.ActionV4(Click(TargetKind.Screen, 9_999_999));         // none at all
            alice.ActionV4(Hover(TargetKind.Screen, his.dm_id));
            alice.ActionV4(Hover(TargetKind.Screen, mine.dm_id));
            server.Tick();
            var click = Assert.Single(server.Clients.Clicks);
            Assert.Same(mine, click.Target);
            Assert.Equal("icon-x=3;icon-y=4;left=1;button=left;screen-loc=WEST+1,SOUTH", click.Params);
            Assert.Equal(2, server.Host.SessionOf(1).ClicksRejected); // the other's and the missing one; the hover on his was replaced by mine
            Assert.Same(mine, Assert.Single(server.Clients.Hovers).Target);
            Assert.Equal(0, server.Host.SessionOf(1).Strikes);

            // Taken off the screen, it is stale: dropped.
            using (server.World.Enter()) aliceMob.client.screen.Remove(mine);
            server.Clients.Clicks.Clear();
            alice.ActionV4(Click(TargetKind.Screen, mine.dm_id));
            server.Tick();
            Assert.Empty(server.Clients.Clicks);
            Assert.Equal(3, server.Host.SessionOf(1).ClicksRejected);
            GC.KeepAlive(bob);
        }
    }

    // ---- keybindings ----

    [Fact]
    public void AKeybindingRunsTgsBindingWithTheMobAsUsr()
    {
        var (server, alice, _, aliceMob, _) = Pair();
        using (server)
        {
            alice.ActionV4(Key(KeybindingCode.DropItem));
            alice.ActionV4(Key(KeybindingCode.HoldThrowMode));
            alice.ActionV4(Key(KeybindingCode.HoldThrowMode, up: true));
            server.Tick();
            Assert.Equal(new[] { ((byte)5, true), ((byte)8, true), ((byte)8, false) }, server.Clients.Keybindings.Select(k => (k.Code, k.Down)));
            Assert.All(server.Clients.Keybindings, k => Assert.Same(aliceMob, k.Usr));
            Assert.Equal(3, server.Host.SessionOf(1).KeybindingsApplied);
            Assert.Equal(0, server.Host.SessionOf(1).ClicksRejected);
        }
    }

    [Fact]
    public void KeybindingsOverTheirRateAreDroppedAndAFloodClosesTheSession()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        for (int i = 0; i < 10; i++) alice.ActionV4(Key(KeybindingCode.SwapHands));
        server.Tick();
        // The burst of 5 passes (the overall bucket's burst is larger); the other 5 are drops, not strikes.
        Assert.Equal(5, server.Clients.Keybindings.Count);
        Session s = server.Host.SessionOf(1);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(5, s.RateLimited);
        Assert.Equal(SessionState.Joined, s.State);
        for (int round = 0; round < 100 && s.State == SessionState.Joined; round++)
        {
            for (int i = 0; i < 10; i++) alice.ActionV4(Key(KeybindingCode.SwapHands));
            server.Tick();
        }
        Assert.NotEqual(SessionState.Joined, s.State);
        Assert.Equal(DisconnectReason.Flooding, s.CloseReason);
    }

    // ---- hovers ----

    [Fact]
    public void OnlyTheLastHoverOfATickRunsAndTheRestAreCounted()
    {
        var (server, alice, _, _, bobMob) = Pair();
        using (server)
        {
            alice.ActionV4(Hover(TargetKind.Turf, x: 5, y: 4, z: 1));
            alice.ActionV4(Hover(TargetKind.Entity, bobMob.dm_id));
            alice.ActionV4(Hover(TargetKind.Turf, x: 6, y: 4, z: 1));
            server.Tick();
            var hover = Assert.Single(server.Clients.Hovers);
            Assert.Equal((6f, 4f), (((turf)hover.Target).x, ((turf)hover.Target).y));
            Session s = server.Host.SessionOf(1);
            Assert.Equal(2, s.HoversDropped);
            Assert.Equal(1, s.HoversApplied);
            Assert.Equal(0, s.Strikes);
            Assert.Equal(0, s.RateLimited);
        }
    }

    [Fact]
    public void HoversOverTheirRateAreDroppedSilentlyAndNeverCloseTheSession()
    {
        var (server, alice, _, _, bobMob) = Pair();
        using (server)
        {
            // One hover a tick is 20 a second: the overall rate allows it, the hover rate (10 a second) does not.
            for (int t = 0; t < 100; t++)
            {
                alice.ActionV4(Hover(TargetKind.Entity, bobMob.dm_id));
                server.Tick();
            }
            Session s = server.Host.SessionOf(1);
            Assert.Equal(SessionState.Joined, s.State);
            Assert.Equal(0, s.Strikes);
            Assert.Equal(0, s.RateLimited);
            Assert.InRange(server.Clients.Hovers.Count, 40, 70);       // about half: 10 a second plus the burst
            Assert.Equal(100, s.HoversApplied + s.HoversDropped);
        }
    }

    // ---- the malformed ones are strikes ----

    [Theory]
    [InlineData(ActionKind.Click, TargetKind.None, 0L, 0, 0, 0, ClickButton.Left, (byte)0, (byte)0)]       // a click without a target kind or id
    [InlineData(ActionKind.Click, TargetKind.Entity, 0L, 0, 0, 0, ClickButton.Left, (byte)0, (byte)0)]     // an entity without an id
    [InlineData(ActionKind.Click, TargetKind.Entity, 5L, 3, 0, 0, ClickButton.Left, (byte)0, (byte)0)]     // an id and a turf
    [InlineData(ActionKind.Click, TargetKind.Turf, 5L, 3, 3, 1, ClickButton.Left, (byte)0, (byte)0)]       // a turf and an id
    [InlineData(ActionKind.Click, TargetKind.Turf, 0L, 0, 3, 1, ClickButton.Left, (byte)0, (byte)0)]       // x 0
    [InlineData(ActionKind.Click, TargetKind.Turf, 0L, 3, 256, 1, ClickButton.Left, (byte)0, (byte)0)]     // y over the map
    [InlineData(ActionKind.Click, TargetKind.Turf, 0L, 3, 3, 0, ClickButton.Left, (byte)0, (byte)0)]       // z 0
    [InlineData(ActionKind.Click, TargetKind.Turf, 0L, 3, 3, Limits.MaxMapLevels + 1, ClickButton.Left, (byte)0, (byte)0)]      // z over the levels
    [InlineData(ActionKind.Click, TargetKind.Entity, 5L, 0, 0, 0, ClickButton.Left, (byte)33, (byte)1)]    // icon-x over 32
    [InlineData(ActionKind.Click, TargetKind.Entity, 5L, 0, 0, 0, ClickButton.Left, (byte)1, (byte)200)]   // icon-y over 32
    [InlineData(ActionKind.Click, TargetKind.Screen, 5L, 0, 0, 0, ClickButton.None, (byte)0, (byte)0)]     // no button
    [InlineData(ActionKind.Hover, TargetKind.None, 0L, 0, 0, 0, ClickButton.None, (byte)0, (byte)0)]       // a hover without a target
    [InlineData(ActionKind.Hover, TargetKind.Entity, 5L, 0, 0, 0, ClickButton.Left, (byte)0, (byte)0)]     // a hover with a button
    [InlineData(ActionKind.Hover, TargetKind.Entity, 5L, 0, 0, 0, ClickButton.None, (byte)4, (byte)0)]     // a hover with an icon pixel
    [InlineData(ActionKind.Rest, TargetKind.Entity, 0L, 0, 0, 0, ClickButton.None, (byte)0, (byte)0)]      // a target on a toggle
    [InlineData(ActionKind.Rest, TargetKind.None, 0L, 4, 0, 0, ClickButton.None, (byte)0, (byte)0)]        // a turf on a toggle
    public void AMalformedTargetIsAStrikeAndNeverQueued(ActionKind kind, TargetKind targetKind, long target, int x, int y, int z, ClickButton button, byte iconX, byte iconY)
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.ActionV4(new ActionMessage(0, kind, target, button, ClickModifiers.None, null) { TargetKind = targetKind, TurfX = x, TurfY = y, TurfZ = z, IconX = iconX, IconY = iconY });
        server.Tick(2);
        Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Action);
    }

    [Theory]
    [InlineData((KeybindingCode)0, false, 0L)]    // no code
    [InlineData((KeybindingCode)20, false, 0L)]   // past the last (version 93: 19 is the scoreboard)
    [InlineData((KeybindingCode)255, true, 0L)]
    [InlineData(KeybindingCode.DropItem, false, 5L)]  // a target on a keybinding
    public void AMalformedKeybindingIsAStrikeAndNeverQueued(KeybindingCode code, bool up, long target)
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.ActionV4(new ActionMessage(0, ActionKind.Keybinding, target, ClickButton.None, ClickModifiers.None, null) { Keybinding = code, KeyUp = up });
        server.Tick(2);
        Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        Assert.Empty(server.Clients.Keybindings);
    }

    [Fact]
    public void AKeybindingFieldOnAnotherKindIsAStrike()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.ActionV4(new ActionMessage(0, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.DropItem });
        alice.ActionV4(new ActionMessage(0, ActionKind.Click, 5, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, KeyUp = true });
        server.Tick(2);
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
    }

    // ---- the screentip sink ----

    [Fact]
    public void ATgScreentipReachesTheHoveringSessionAsContextHints()
    {
        var (server, alice, bob, aliceMob, bobMob) = Pair();
        using (server)
        {
            obj door = null;
            using (server.World.Enter())
            {
                door = Dm.@new<obj>(locate(6, 4, 1));
                DmList context = Dm.list();
                context[SCREENTIP_CONTEXT_LMB] = "Open";
                context[SCREENTIP_CONTEXT_ALT_RMB] = "<b>Weld</b> shut";
                context[SCREENTIP_CONTEXT_SHIFT_LMB] = "Examine";
                ScreentipSeam.Publish(aliceMob.client, door, "airlock", context);
                ScreentipSeam.Publish(aliceMob.client, locate(6, 5, 1), "floor", null);
                ScreentipSeam.Publish(aliceMob.client, door, null, null);
            }
            server.Tick();
            ContextHintsMessage[] sent = alice.Messages(MessageId.ContextHints).Select(m =>
            {
                Assert.True(Codec.TryDecode(m, out ContextHintsMessage value, out ProtocolError error), error.ToString());
                return value;
            }).ToArray();
            Assert.Equal(3, sent.Length);
            Assert.Equal((TargetKind.Entity, door.dm_id, "airlock"), (sent[0].TargetKind, sent[0].Target, sent[0].Name));
            Assert.Equal(new[]
            {
                new ContextHint(ClickButton.Left, ClickModifiers.None, "Open"),
                new ContextHint(ClickButton.Right, ClickModifiers.Alt, "Weld shut"),
                new ContextHint(ClickButton.Left, ClickModifiers.Shift, "Examine"),
            }, sent[0].Hints);
            Assert.Equal((TargetKind.Turf, 6, 5, 1, "floor"), (sent[1].TargetKind, sent[1].TurfX, sent[1].TurfY, sent[1].TurfZ, sent[1].Name));
            Assert.Empty(sent[1].Hints);
            Assert.Null(sent[2].Name);
            Assert.Empty(bob.Messages(MessageId.ContextHints)); // another session's screentip never reaches Bob
            GC.KeepAlive(bobMob);
        }
    }

    [Fact]
    public void AScreentipOfAScreenObjectNamesTheScreenId()
    {
        var (server, alice, _, aliceMob, _) = Pair();
        using (server)
        {
            obj hand = ScreenObject(server, aliceMob, "CENTER,SOUTH");
            using (server.World.Enter()) ScreentipSeam.Publish(aliceMob.client, hand, "Left hand", null);
            server.Tick();
            Assert.True(Codec.TryDecode(alice.Messages(MessageId.ContextHints).Single(), out ContextHintsMessage hints, out _));
            Assert.Equal((TargetKind.Screen, hand.dm_id, "Left hand"), (hints.TargetKind, hints.Target, hints.Name));
        }
    }

    // ---- click_ms ----

    [Fact]
    public void TheServersPartOfClickMsCountsEachClickThatTheWorldApplied()
    {
        var (server, alice, _, _, bobMob) = Pair();
        using (server)
        {
            Assert.Equal(0, server.Host.ClicksTimed);
            alice.ActionV4(Click(TargetKind.Entity, bobMob.dm_id));
            alice.ActionV4(Hover(TargetKind.Entity, bobMob.dm_id));
            alice.ActionV4(Key(KeybindingCode.SwapHands));
            server.Tick();
            Assert.Equal(1, server.Host.ClicksTimed); // clicks only; a forged one is still a click the host handled
            alice.ActionV4(Click(TargetKind.Entity, 9_999_999));
            server.Tick();
            Assert.Equal(2, server.Host.ClicksTimed);
            Assert.Equal(2, server.Host.ClickLatency.Count);
            Assert.True(server.Host.ClickLatency.Max >= 0);
            server.Tick(3);
            Assert.Equal(2, server.Host.ClicksTimed);
        }
    }
}
