using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 17 (drag and drop, docs/ai/specs/protocol.md section 26): the Drop action (kind 13), BYOND's MouseDrop of a dragged
/// thing over another. Bytes, round trips, refusals, the host validating both things as click targets and handing tg's params to the
/// client seam, the command log's round trip, a version-15 client refused. The client's drag model is Client/Interaction/DragModelTests.
/// </summary>
public class ProtocolV17Tests
{
    private static ActionMessage Drop(TargetKind kind, long id, TargetKind overKind, long overId, int x = 0, int y = 0, int z = 0, int ox = 0, int oy = 0, int oz = 0,
        ClickModifiers modifiers = ClickModifiers.None, byte iconX = 0, byte iconY = 0, byte time = 0, uint seq = 0)
        => new(seq, ActionKind.Drop, id, ClickButton.Left, modifiers, null)
        {
            TargetKind = kind,
            TurfX = x,
            TurfY = y,
            TurfZ = z,
            OverKind = overKind,
            OverTarget = overId,
            OverX = ox,
            OverY = oy,
            OverZ = oz,
            IconX = iconX,
            IconY = iconY,
            DragTime = time,
        };

    internal static ActionMessage RandomDrop(Random rng)
    {
        TargetKind Kind() => (TargetKind)rng.Next(1, 4);
        TargetKind kind = Kind(), over = Kind();
        return new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Drop, kind == TargetKind.Turf ? 0 : rng.Next(1, 1_000_000), (ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), null)
        {
            TargetKind = kind,
            TurfX = kind == TargetKind.Turf ? rng.Next(1, 256) : 0,
            TurfY = kind == TargetKind.Turf ? rng.Next(1, 256) : 0,
            TurfZ = kind == TargetKind.Turf ? rng.Next(1, Limits.MaxMapLevels + 1) : 0,
            OverKind = over,
            OverTarget = over == TargetKind.Turf ? 0 : rng.Next(1, 1_000_000),
            OverX = over == TargetKind.Turf ? rng.Next(1, 256) : 0,
            OverY = over == TargetKind.Turf ? rng.Next(1, 256) : 0,
            OverZ = over == TargetKind.Turf ? rng.Next(1, Limits.MaxMapLevels + 1) : 0,
            IconX = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            IconY = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            DragTime = (byte)rng.Next(0, 256),
        };
    }

    [Fact]
    public void TheVersionIsSeventeenAndDropIsTheLastKind()
    {
        Assert.True(ProtocolVersion.Current >= 17); // version 18: maptext in appearances
        Assert.Equal(13, (int)ActionKind.Drop);
        Assert.True(ActionKinds.Last >= ActionKind.Drop); // version 24: Topic (14) after it
        Assert.True(Limits.MaxDropActionBytes <= Limits.MaxActionMessageBytes);
        Assert.True(Limits.MaxActionMessageBytes <= Limits.MaxInboundMessageBytes);
    }

    [Fact]
    public void ADropIsItsTwoTargetsButtonModifiersPixelAndTimeAndRoundTrips()
    {
        ActionMessage drop = Drop(TargetKind.Screen, 300, TargetKind.Turf, 0, ox: 7, oy: 5, oz: 1, modifiers: ClickModifiers.Ctrl, iconX: 16, iconY: 9, time: 4, seq: 5);
        byte[] bytes = Codec.Encode(drop);
        // [id] [seq 5] [kind 13] [screen 300] [turf 7,5,1] [left] [ctrl] [icon 16,9] [4 ds]
        Assert.Equal(new byte[] { (byte)MessageId.Action, 5, 13, 3, 0xAC, 0x02, 2, 7, 5, 1, 1, 2, 16, 9, 4 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(drop, back);
        Assert.True(back.IsTargeted);
        Assert.Equal(TargetKind.Screen, back.EffectiveTargetKind);

        ActionMessage entities = Drop(TargetKind.Entity, 7, TargetKind.Entity, 8, time: 255);
        Assert.True(Codec.TryDecode(Codec.Encode(entities), out ActionMessage back2, out _));
        Assert.Equal(entities, back2);
    }

    [Fact]
    public void ABadDropIsRefused()
    {
        ActionMessage good = Drop(TargetKind.Entity, 7, TargetKind.Entity, 8);
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { OverKind = TargetKind.None, OverTarget = 0 }));  // no over thing
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { Button = ClickButton.None }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { AimZone = AimZone.Head }));                     // a click's
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { IconX = 33 }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { Modifiers = (ClickModifiers)8 }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(good with { OverX = 3 }));                                   // turf coordinates on an id
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, OverKind = TargetKind.Entity, OverTarget = 8 }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { DragTime = 3 }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, (ActionKind)14, 0, ClickButton.None, ClickModifiers.None, null))); // kind 14 (version 24's Topic) without its href

        byte[] bytes = Codec.Encode(good);
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out ActionMessage _, out _));          // no drag time
        Assert.False(Codec.TryDecode(bytes.Concat(new byte[] { 0 }).ToArray(), out ActionMessage _, out _));      // a byte after it
        byte[] badKind = (byte[])bytes.Clone();
        badKind[5] = 4; // the over thing's target kind
        Assert.False(Codec.TryDecode(badKind, out ActionMessage _, out _));
        byte[] badMods = (byte[])bytes.Clone();
        badMods[8] = 0x10; // a click's aim zone bits are not a drop's
        Assert.False(Codec.TryDecode(badMods, out ActionMessage _, out _));
        byte[] badPixel = (byte[])bytes.Clone();
        badPixel[9] = 33;
        Assert.False(Codec.TryDecode(badPixel, out ActionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 14 }, out ActionMessage _, out _));  // kind 14 (version 24's Topic) without its href
    }

    // Alice at (4, 4) and Bob east of her, stood still.
    private static (TestServer Server, FakeRemote Alice, mob AliceMob, mob BobMob) Pair()
    {
        TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        FakeRemote bob = server.Join("Bob");
        server.Host.EnqueueConsoleLine("teleport Alice 4 4");
        server.Host.EnqueueConsoleLine("teleport Bob 5 4");
        alice.Motion(0, 0);
        bob.Motion(0, 0);
        server.Tick(2);
        return (server, alice, server.MobOf("Alice"), server.MobOf("Bob"));
    }

    [Fact]
    public void TheHostHandsADropWithTgsParamsToTheSeamAndLogsIt()
    {
        var (server, alice, aliceMob, bobMob) = Pair();
        using (server)
        {
            obj slot;
            using (server.World.Enter())
            {
                slot = Dm.@new<obj>(null);
                slot.screen_loc = "CENTER-1:16,SOUTH+1:5";
                aliceMob.client.screen.Add(slot);
            }
            alice.ActionV4(Drop(TargetKind.Entity, bobMob.dm_id, TargetKind.Screen, slot.dm_id, modifiers: ClickModifiers.Alt, iconX: 3, iconY: 4, time: 6));
            alice.ActionV4(Drop(TargetKind.Screen, slot.dm_id, TargetKind.Turf, 0, ox: 6, oy: 5, oz: 1, iconX: 32, iconY: 1));
            server.Tick(2);
            Assert.Equal(2, server.Clients.Drops.Count);
            var first = server.Clients.Drops[0];
            Assert.Same(bobMob, first.Dragged);
            Assert.Same(slot, first.Over);
            Assert.Same(aliceMob, first.Usr);
            Assert.Equal("left=1;alt=1;button=left;drag=left", first.DragParams);
            Assert.Equal("icon-x=3;icon-y=4;left=1;alt=1;button=left;drag=left;screen-loc=CENTER-1:16,SOUTH+1:5", first.DropParams);
            Assert.Equal(6, first.DragTime);
            var second = server.Clients.Drops[1];
            Assert.Same(slot, second.Dragged);
            Assert.Equal((6f, 5f, 1f), (((turf)second.Over).x, ((turf)second.Over).y, ((turf)second.Over).z));
            Assert.Equal("left=1;button=left;drag=left;screen-loc=CENTER-1:16,SOUTH+1:5", second.DragParams);
            Assert.Equal("icon-x=32;icon-y=1;left=1;button=left;drag=left", second.DropParams);
            Session s = server.Host.SessionOf(1);
            Assert.Equal((2, 0, 0), (s.DropsApplied, s.DropsRejected, s.Strikes));

            QueuedCommand logged = server.Host.Log.Commands.First(c => c.Kind == CommandKind.Action && c.Action == ActionKind.Drop);
            Assert.Equal((TargetKind.Entity, bobMob.dm_id, TargetKind.Screen, slot.dm_id, (byte)6), (logged.TargetKind, logged.Target, logged.OverKind, logged.OverTarget, logged.DragTime));
            CommandLog parsed = CommandLog.Parse(server.Host.Log.ToText(), out string error);
            Assert.Null(error);
            Assert.Equal(server.Host.Log.Commands.Where(c => c.Action == ActionKind.Drop), parsed.Commands.Where(c => c.Action == ActionKind.Drop));
        }
    }

    [Fact]
    public void ADropOfOrOntoSomethingThePlayerCannotSeeIsDroppedNotAStrike()
    {
        var (server, alice, aliceMob, bobMob) = Pair();
        using (server)
        {
            obj his;
            using (server.World.Enter())
            {
                his = Dm.@new<obj>(null);
                his.screen_loc = "EAST,SOUTH";
                bobMob.client.screen.Add(his);
            }
            alice.ActionV4(Drop(TargetKind.Entity, bobMob.dm_id, TargetKind.Screen, his.dm_id));            // another player's HUD
            alice.ActionV4(Drop(TargetKind.Entity, 9_999_999, TargetKind.Entity, bobMob.dm_id));            // nothing at all
            alice.ActionV4(Drop(TargetKind.Entity, bobMob.dm_id, TargetKind.Turf, 0, ox: 12, oy: 4, oz: 1)); // 8 tiles away
            alice.ActionV4(Drop(TargetKind.Turf, 0, TargetKind.Entity, bobMob.dm_id, x: 200, y: 200, z: 1)); // outside the map
            server.Tick(2);
            Assert.Empty(server.Clients.Drops);
            Session s = server.Host.SessionOf(1);
            Assert.Equal((0, 4, 0), (s.DropsApplied, s.DropsRejected, s.Strikes));
            GC.KeepAlive(aliceMob);
        }
    }

    [Fact]
    public void AMalformedDropIsAStrike()
    {
        var (server, alice, _, bobMob) = Pair();
        using (server)
        {
            alice.ActionV4(Drop(TargetKind.Entity, bobMob.dm_id, TargetKind.Entity, bobMob.dm_id, iconX: 40)); // an icon pixel over 32 (the host's own check)
            server.Tick(2);
            Assert.Empty(server.Clients.Drops);
            Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        }
    }

    [Fact]
    public void A_client_of_version_16_is_refused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 16);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }
}
