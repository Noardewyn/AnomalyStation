using Anomaly.Client.Shared;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 14 (M4c slice 3, docs/ai/specs/protocol.md section 23): the Unhover action (BYOND's MouseExited when the pointer
/// leaves the hovered thing over nothing): its bytes and round trip, the refusals, the host applying it (one hover or unhover a
/// tick, the newest), a version-13 client refused, and the play session sending it on the hover interval.
/// </summary>
public class ProtocolV14Tests
{
    private static ActionMessage Unhover(uint seq = 0) => new(seq, ActionKind.Unhover, 0, ClickButton.None, ClickModifiers.None, null);

    [Fact]
    public void UnhoverIsKindNine()
    {
        Assert.True(ProtocolVersion.Current >= 14); // version 15: RadialPick (10) follows it
        Assert.Equal(9, (int)ActionKind.Unhover);
    }

    [Fact]
    public void AnUnhoverIsItsKindByteAloneAndRoundTrips()
    {
        byte[] bytes = Codec.Encode(Unhover(300));
        Assert.Equal(new byte[] { (byte)MessageId.Action, 0xAC, 0x02, 9 }, bytes); // [id] [varuint 300] [kind 9]
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal((300u, ActionKind.Unhover, TargetKind.None, 0L), (back.FrameSeq, back.Kind, back.TargetKind, back.Target));
        Assert.False(back.IsTargeted);
    }

    [Fact]
    public void AnUnhoverWithATargetOrAButtonAndAnUnknownKindAreRefused()
    {
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new ActionMessage(1, ActionKind.Unhover, 7, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity }));
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new ActionMessage(1, ActionKind.Unhover, 0, ClickButton.Left, ClickModifiers.None, null)));
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new ActionMessage(1, (ActionKind)14, 0, ClickButton.None, ClickModifiers.None, null)));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 14 }, out ActionMessage _, out _)); // kind 14 without its href (version 24's Topic)
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 9, 1 }, out ActionMessage _, out _)); // a byte after it
    }

    [Fact]
    public void TheHostAppliesAnUnhoverAndKeepsOnlyTheNewestHoverOfATick()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        alice.ActionV4(Unhover());
        server.Tick(2);
        Assert.Contains(server.Clients.Actions, a => a.Action == "unhover");
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        Assert.Contains(server.Host.Log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.Unhover);

        // a hover then an unhover in the same tick: the unhover is the newest and the hover is dropped
        server.Clients.Actions.Clear();
        int dropped = server.Host.SessionOf(1).HoversDropped;
        alice.ActionV4(new ActionMessage(0, ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 4, TurfZ = 1 });
        alice.ActionV4(Unhover());
        server.Tick(2);
        Assert.Equal(dropped + 1, server.Host.SessionOf(1).HoversDropped);
        Assert.Single(server.Clients.Actions, a => a.Action == "unhover");
    }

    [Fact]
    public void A_client_of_version_13_is_refused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 13);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }

    [Fact]
    public void ThePlaySessionSendsAnUnhoverWhenThePointerLeavesToNothingOnTheHoverInterval()
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        var slot = new ClickTarget(TargetKind.Screen, 2001, 0, 0, 0, 16, 16);
        Assert.True(rig.Session.Hover(slot, rig.Now));
        Assert.Equal(slot, rig.Session.Hovered);
        // nothing under the pointer within the interval: the hints clear at once, the unhover waits
        Assert.False(rig.Session.Hover(ClickTarget.None, rig.Now + 0.01));
        Assert.False(rig.Session.Hovered.IsSet);
        Assert.Equal(0, rig.Session.UnhoversSent);
        // back on the same thing before the interval: the server still has it, nothing is sent
        Assert.False(rig.Session.Hover(slot, rig.Now + 0.2));
        Assert.Equal(slot, rig.Session.Hovered);
        Assert.Equal((1, 0), (rig.Session.HoversSent, rig.Session.UnhoversSent));
        // off to nothing after the interval: one unhover, and nothing more while nothing is under it
        Assert.True(rig.Session.Hover(ClickTarget.None, rig.Now + 0.4));
        Assert.False(rig.Session.Hover(ClickTarget.None, rig.Now + 0.6));
        Assert.Equal((1, 1), (rig.Session.HoversSent, rig.Session.UnhoversSent));
    }
}
