using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 11 (docs/ai/specs/protocol.md section 20): a screen entry's flick and animate() chains in the private block, their
/// round trips, the bytes of an entry without them and the refusals.
/// </summary>
public class ScreenLookCodecTests
{
    private static Delta WithScreen(params ScreenEntry[] changed)
    {
        Delta d = Msg.Delta(300);
        d.Own.ScreenChanged.AddRange(changed);
        return d;
    }

    [Fact]
    public void A_screen_entrys_flick_and_animation_round_trip_in_a_delta_and_a_snapshot()
    {
        Delta d = WithScreen(
            new ScreenEntry(12, TableRef.Public(1), "WEST,1") { FlickAppearance = new TableRef(3, true), FlickTick = 297, Animation = AnimationCodecTests.Sample(299) },
            new ScreenEntry(13, TableRef.Public(1), null) { Animation = AnimationCodecTests.Sample(250) },
            new ScreenEntry(14, TableRef.Public(2), null) { FlickAppearance = TableRef.Public(2), FlickTick = 0 });
        d.Own.ScreenAdded.Add(new ScreenEntry(9, TableRef.Public(1), "EAST,1") { FlickAppearance = TableRef.Public(1), FlickTick = 300 });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(new TableRef(3, true), decoded.Own.ScreenChanged[0].FlickAppearance);
        Assert.Equal(297, decoded.Own.ScreenChanged[0].FlickTick);
        Assert.Equal(AnimationCodecTests.Sample(299), decoded.Own.ScreenChanged[0].Animation);
        Assert.True(decoded.Own.ScreenChanged[1].FlickAppearance.IsNone);
        Assert.Equal(0, decoded.Own.ScreenChanged[2].FlickTick);
        Assert.Null(decoded.Own.ScreenChanged[2].Animation);
        Assert.Equal(300, decoded.Own.ScreenAdded[0].FlickTick);

        Snapshot s = Msg.Snapshot(100, 7, 0, new[] { "/obj" });
        s.Own.ScreenAdded.Add(new ScreenEntry(5, TableRef.Public(1), "WEST,2") { Animation = AnimationCodecTests.Sample(90) });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot snapshot, out error), error.ToString());
        Assert.Equal(AnimationCodecTests.Sample(90), snapshot.Own.ScreenAdded[0].Animation);
    }

    [Fact]
    public void A_flick_is_written_relative_to_the_entrys_reference_tick()
    {
        var own = new OwnBlock();
        own.ScreenChanged.Add(new ScreenEntry(40, TableRef.Public(1), null) { FlickAppearance = TableRef.Public(2), FlickTick = 130 });
        byte[] block = new byte[64];
        int n = Codec.EncodeOwnBlock(own, block);
        // flags (screen), depth, version 43's 0 actions done and flags, 0 added, 1 changed: id 40 (zigzag 80), appearance (1 << 1), a null screenLoc, group 0, look 1 (a flick),
        // the reference tick 130 (varuint 0x82 0x01), the flick's appearance (2 << 1) and 0 ticks before the reference, 0 removed
        Assert.Equal(new byte[] { 4, 0, 0, 0, 0, 1, 80, 2, 0, 0, 1, 0x82, 0x01, 4, 0, 0 }, block.AsSpan(0, n).ToArray());
    }

    [Fact]
    public void The_encoder_and_the_decoder_refuse_a_bad_look()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithScreen(new ScreenEntry(1, TableRef.Public(1), null) { FlickAppearance = TableRef.Public(1), FlickTick = -1 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithScreen(new ScreenEntry(1, TableRef.Public(1), null) { Animation = new EntityAnimation() })));

        byte[] good = Codec.Encode(WithScreen(new ScreenEntry(1, TableRef.Public(1), null) { FlickAppearance = TableRef.Public(1), FlickTick = 300 }));
        // the private block ends: ... group 0, look 1, the reference tick 300 (0xAC 0x02), the flick's appearance (1 << 1), 0 ticks before it, 0 removed
        int look = good.Length - 6;
        Assert.Equal(new byte[] { 1, 0xAC, 0x02, 2, 0, 0 }, good.AsSpan(look).ToArray());
        Assert.True(Codec.TryDecode(good, out Delta _, out _));

        byte[] badBits = (byte[])good.Clone();
        badBits[look] = 8; // a look bit that is not defined (bit 2 is the vis children of version 12)
        Assert.False(Codec.TryDecode(badBits, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);

        byte[] noAppearance = (byte[])good.Clone();
        noAppearance[look + 3] = 0; // the flick's appearance: none
        Assert.False(Codec.TryDecode(noAppearance, out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
    }
}
