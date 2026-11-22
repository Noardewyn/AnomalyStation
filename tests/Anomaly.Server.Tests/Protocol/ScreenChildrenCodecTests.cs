using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 12 (docs/ai/specs/protocol.md section 21): a screen entry's clickable vis_contents children in the private block, their
/// round trips beside a flick and an animation, the bytes of an entry with children only, the refusals, the replica's table check and the
/// version gate.
/// </summary>
public class ScreenChildrenCodecTests
{
    private static ScreenChildren Of(params ScreenChild[] items) => new() { Items = items };

    [Fact]
    public void A_screen_entrys_children_round_trip_in_a_delta_and_a_snapshot()
    {
        Delta d = Msg.Delta(300);
        d.Own.ScreenChanged.Add(new ScreenEntry(12, new TableRef(1, true), "CENTER+0:16,SOUTH+0:5") { VisChildren = Of(new ScreenChild(400123, new TableRef(2, true))) });
        d.Own.ScreenChanged.Add(new ScreenEntry(13, TableRef.Public(1), null)
        {
            FlickAppearance = TableRef.Public(2), FlickTick = 297, Animation = AnimationCodecTests.Sample(299),
            VisChildren = Of(new ScreenChild(7, TableRef.Public(3), MouseOpaque: true), new ScreenChild(5, new TableRef(9, true))),
        });
        d.Own.ScreenChanged.Add(new ScreenEntry(14, TableRef.Public(1), null)); // none: null
        d.Own.ScreenAdded.Add(new ScreenEntry(9, TableRef.Public(1), "EAST,1") { VisChildren = Of(new ScreenChild(long.MaxValue, TableRef.Public(1))) });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(new ScreenChild(400123, new TableRef(2, true)), Assert.Single(decoded.Own.ScreenChanged[0].VisChildren.Items));
        Assert.Equal(new long[] { 7, 5 }, decoded.Own.ScreenChanged[1].VisChildren.Items.Select(c => c.Id)); // bottom to top, as sent
        Assert.True(decoded.Own.ScreenChanged[1].VisChildren.Items[0].MouseOpaque);
        Assert.Equal(297, decoded.Own.ScreenChanged[1].FlickTick);
        Assert.Null(decoded.Own.ScreenChanged[2].VisChildren);

        Snapshot s = Msg.Snapshot(100, 7, 0, new[] { "/obj" });
        s.Own.ScreenAdded.Add(new ScreenEntry(5, TableRef.Public(1), "WEST,2") { VisChildren = Of(new ScreenChild(6, TableRef.Public(1))) });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot snapshot, out error), error.ToString());
        Assert.Equal(s.Own.ScreenAdded[0].VisChildren, snapshot.Own.ScreenAdded[0].VisChildren);
    }

    [Fact]
    public void Children_alone_are_written_after_the_look_byte_without_a_reference_tick()
    {
        var own = new OwnBlock();
        own.ScreenChanged.Add(new ScreenEntry(40, TableRef.Public(1), null) { VisChildren = Of(new ScreenChild(300, new TableRef(3, true), true)) });
        byte[] block = new byte[64];
        int n = Codec.EncodeOwnBlock(own, block);
        // flags (screen), depth, version 43's 0 actions done and flags, 0 added, 1 changed: id 40 (zigzag 80), appearance (1 << 1), a null screenLoc, group 0, look 4 (children),
        // 1 child: id 300 (varuint 0xAC 0x02), its appearance (3 << 2 | opaque 2 | private 1 = 15), 0 removed
        Assert.Equal(new byte[] { 4, 0, 0, 0, 0, 1, 80, 2, 0, 0, 4, 1, 0xAC, 0x02, 15, 0 }, block.AsSpan(0, n).ToArray());
    }

    [Fact]
    public void The_encoder_and_the_decoder_refuse_bad_children()
    {
        static Delta With(ScreenChildren children)
        {
            Delta d = Msg.Delta(300);
            d.Own.ScreenChanged.Add(new ScreenEntry(1, TableRef.Public(1), null) { VisChildren = children });
            return d;
        }

        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Of())));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Of(new ScreenChild(0, TableRef.Public(1))))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Of(new ScreenChild(5, TableRef.None)))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Of(Enumerable.Range(1, Limits.MaxScreenChildren + 1).Select(i => new ScreenChild(i, TableRef.Public(1))).ToArray()))));

        byte[] good = Codec.Encode(With(Of(new ScreenChild(5, TableRef.Public(1)))));
        // the private block ends: ... group 0, look 4, 1 child: id 5, appearance (1 << 2), 0 removed
        int look = good.Length - 5;
        Assert.Equal(new byte[] { 4, 1, 5, 4, 0 }, good.AsSpan(look).ToArray());
        Assert.True(Codec.TryDecode(good, out Delta _, out _));

        foreach ((int at, byte value) in new[] { (1, (byte)0), (2, (byte)0), (3, (byte)0), (3, (byte)2), (1, (byte)(Limits.MaxScreenChildren + 1)) })
        {
            byte[] bad = (byte[])good.Clone();
            bad[look + at] = value; // 0 children; id 0; no appearance; no appearance (the opaque bit alone); over the cap
            Assert.False(Codec.TryDecode(bad, out Delta _, out ProtocolError error), $"byte {at} = {value}");
            Assert.True(error is ProtocolError.BadValue or ProtocolError.CountTooLarge, error.ToString());
        }
    }

    [Fact]
    public void The_replica_refuses_a_child_appearance_its_tables_do_not_have()
    {
        var replica = new ReplicaWorld();
        Snapshot s = Msg.Snapshot(10, 0, 0, new[] { "/obj" });
        s.Own.ScreenAdded.Add(new ScreenEntry(5, TableRef.None, "WEST,2") { VisChildren = Of(new ScreenChild(6, new TableRef(4, true))) });
        Assert.ThrowsAny<ProtocolException>(() => replica.Apply(s));
    }

    [Fact]
    public void A_client_of_version_11_is_refused()
    {
        Assert.True(ProtocolVersion.Current >= 12); // version 13: the Sounds message
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 11);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }
}
