using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Protocol;
using Anomaly.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 51 (M10x slice C, docs/ai/specs/protocol.md section 56): the private block's eye (tg's client.eye when it is another mob,
/// a ghost observing a player) and the DblClick action (BYOND's double click); and tg's unquoted chat links (FOLLOW_LINK, TURF_LINK), which
/// the host keeps and the client offers; the motion parameters' push speed (DV177, the drift a push from rest starts).
/// </summary>
public class ProtocolV51Tests
{
    [Fact]
    public void ThePushSpeedRoundTripsAndOneOutOfRangeIsRefused()
    {
        foreach (short impulse in new short[] { 0, 38, (short)(Limits.MaxTilesPerTick * 256) })
        {
            MotionParams p = MotionParams.DefaultRun with { ImpulseSpeed = impulse };
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(39, 118, 1), Params = p };
            Assert.True(Codec.TryDecode(Codec.Encode(new Delta { Tick = 4, Own = own }), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(p, decoded.Own.Params);
        }
        foreach (short impulse in new short[] { -1, (short)(Limits.MaxTilesPerTick * 256 + 1) })
        {
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { ImpulseSpeed = impulse } };
            Assert.Throws<ProtocolException>(() => Codec.Encode(new Delta { Tick = 4, Own = own }));
        }
    }

    [Fact]
    public void TheEyeRoundTripsAsBitOneOfTheVersion43FlagsAndNoChangeIsNull()
    {
        Assert.True(ProtocolVersion.Current >= 51);
        var buffer = new byte[64];
        // flags 0, depth 0, 0 settled, v43 flags 2 (the eye), eye 5
        Assert.Equal(new byte[] { 0, 0, 0, 2, 5 }, buffer.AsSpan(0, Codec.EncodeOwnBlock(new OwnBlock { Eye = 5 }, buffer)).ToArray());
        Assert.Equal(Codec.EmptyOwnBlockBytes, Codec.EncodeOwnBlock(new OwnBlock(), buffer));
        foreach (long? eye in new long?[] { null, 0, 300, 1L << 40 })
        {
            Delta d = Msg.Delta(9);
            d.Own.Eye = eye;
            d.Own.Access = new List<string> { "maint_tunnels" };
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
            Msg.AssertEqual(d.Own, back.Own);
        }
        Delta negative = Msg.Delta(9);
        negative.Own.Eye = -1;
        Assert.Throws<ProtocolException>(() => Codec.Encode(negative));
        // an undefined bit of the version 43 flags is still refused
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0)
            .VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(4)), out Delta _, out _));
    }

    [Fact]
    public void TheReplicaKeepsTheEyeUntilABlockNamesAnother()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(8, 8));
        world.Apply(Msg.Snapshot(10, 1, 0, new[] { "/mob" }, Msg.State(1), Msg.State(2, 0, 3, 3)));
        Assert.Equal(0, world.EyeEntity);
        Delta d = Msg.Delta(11, own: 1);
        d.Own.Eye = 2;
        world.Apply(d);
        Assert.Equal(2, world.EyeEntity);
        world.Apply(Msg.Delta(12, own: 1)); // no change
        Assert.Equal(2, world.EyeEntity);
        Delta back = Msg.Delta(13, own: 1);
        back.Own.Eye = 0;
        world.Apply(back);
        Assert.Equal(0, world.EyeEntity);
    }

    private static ActionMessage DblClick(TargetKind kind, long id, int x = 0, int y = 0, int z = 0)
        => new(4, ActionKind.DblClick, id, ClickButton.Left, ClickModifiers.Ctrl, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z, IconX = 16, IconY = 9 };

    [Fact]
    public void ADoubleClickIsAMouseActionOnATargetAndRoundTrips()
    {
        Assert.True(ActionKind.DblClick <= ActionKinds.Last); // version 70 adds MouseWheel after it
        foreach (ActionMessage sent in new[] { DblClick(TargetKind.Entity, 77), DblClick(TargetKind.Turf, 0, 10, 20, 1), DblClick(TargetKind.Screen, 5) })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(sent), out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(sent, back);
            Assert.True(back.IsTargeted);
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(DblClick(TargetKind.None, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(DblClick(TargetKind.Entity, 77) with { Aim = new ShotAim(256, 256, 1000, 0, 0) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(DblClick(TargetKind.Entity, 77) with { AimZone = AimZone.Head }));
        // over nothing: the mouse action's empty target byte
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 4, (byte)ActionKind.DblClick, 0, 1, 0, 16, 9 }, out ActionMessage _, out _));
    }

    [Fact]
    public void TgsUnquotedChatLinksAreKeptByTheHostAndOfferedByTheClient()
    {
        const string follow = "byond://?src=[mob_1];follow=[mob_2];x=3;y=4;z=1";
        string html = "<a href=" + follow + ">(F)</a> <span class='deadsay'><b>Bob</b> has died at <b>Bar</b>.</span> <a href=byond://?src=[mob_1];x=5;y=6;z=1>(T)</a>";
        Assert.Equal(new[] { follow, "byond://?src=[mob_1];x=5;y=6;z=1" }, ChatLinks.Hrefs(html).ToArray());
        var links = new List<ChatLink>();
        string plain = HtmlText.ToPlain(html, links);
        Assert.Equal(2, links.Count);
        Assert.Equal("(F)", plain.Substring(links[0].Start, links[0].Length));
        Assert.Equal(follow, links[0].Href);
        Assert.Equal("(T)", plain.Substring(links[1].Start, links[1].Length));
    }
}
