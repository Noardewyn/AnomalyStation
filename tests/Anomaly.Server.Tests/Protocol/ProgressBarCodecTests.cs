using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 27 (docs/ai/specs/protocol.md section 36): a private image's holder and the holder's name, and an action button's target
/// item, their round trips, their bytes and the refusals.
/// </summary>
public class ProgressBarCodecTests
{
    [Fact]
    public void An_images_holder_and_an_action_buttons_target_round_trip()
    {
        Delta d = Msg.Delta(300);
        d.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(1, true), 0) { Holder = 400123, HolderName = "oxygen tank" });
        d.Own.ImagesAdded.Add(new ImageEntry(2, new TableRef(1, true), 77) { Holder = 77, HolderName = "airlock" });
        d.Own.ImagesChanged.Add(new ImageEntry(3, TableRef.Public(1), 0)); // none: 0 and null
        d.Own.ScreenChanged.Add(new ScreenEntry(12, new TableRef(1, true), "WEST+0:6,NORTH+0:-6") { ActionTarget = 400123 });
        d.Own.ScreenChanged.Add(new ScreenEntry(13, TableRef.Public(1), null)
        {
            FlickAppearance = TableRef.Public(2), FlickTick = 297, Animation = AnimationCodecTests.Sample(299), ActionTarget = long.MaxValue,
            VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(7, TableRef.Public(3)) } },
        });
        d.Own.ScreenChanged.Add(new ScreenEntry(14, TableRef.Public(1), null));
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(d.Own.ImagesAdded, decoded.Own.ImagesAdded);
        Assert.Equal("oxygen tank", decoded.Own.ImagesAdded[0].HolderName);
        Assert.Equal(400123, decoded.Own.ImagesAdded[0].Holder);
        Assert.Null(decoded.Own.ImagesChanged[0].HolderName);
        Assert.Equal(new long[] { 400123, long.MaxValue, 0 }, decoded.Own.ScreenChanged.Select(e => e.ActionTarget));
        Assert.Equal(7, Assert.Single(decoded.Own.ScreenChanged[1].VisChildren.Items).Id);
    }

    [Fact]
    public void An_action_target_alone_is_bit_3_of_the_look_and_comes_last()
    {
        var own = new OwnBlock();
        own.ScreenChanged.Add(new ScreenEntry(40, TableRef.Public(1), null) { ActionTarget = 300 });
        byte[] block = new byte[64];
        int n = Codec.EncodeOwnBlock(own, block);
        // flags (screen), depth, version 43's 0 actions done and flags, 0 added, 1 changed: id 40 (zigzag 80), appearance (1 << 1), a null screenLoc, group 0, look 8 (action
        // target), the target 300 (varuint 0xAC 0x02), 0 removed
        Assert.Equal(new byte[] { 4, 0, 0, 0, 0, 1, 80, 2, 0, 0, 8, 0xAC, 0x02, 0 }, block.AsSpan(0, n).ToArray());
    }

    [Fact]
    public void The_encoder_and_the_decoder_refuse_bad_holders_and_targets()
    {
        Delta negativeTarget = Msg.Delta(300);
        negativeTarget.Own.ScreenChanged.Add(new ScreenEntry(1, TableRef.Public(1), null) { ActionTarget = -1 });
        Assert.Throws<ProtocolException>(() => Codec.Encode(negativeTarget));

        Delta negativeHolder = Msg.Delta(300);
        negativeHolder.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { Holder = -5 });
        Assert.Throws<ProtocolException>(() => Codec.Encode(negativeHolder));

        Delta longName = Msg.Delta(300);
        longName.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { HolderName = new string('a', Limits.MaxImageHolderNameBytes + 1) });
        Assert.Throws<ProtocolException>(() => Codec.Encode(longName));

        // the private block ends: ... group 0, look 8, the target 300 (0xAC 0x02), 0 removed
        Delta target = Msg.Delta(300);
        target.Own.ScreenChanged.Add(new ScreenEntry(1, TableRef.Public(1), null) { ActionTarget = 300 });
        byte[] good = Codec.Encode(target);
        int look = good.Length - 4;
        Assert.Equal(new byte[] { 8, 0xAC, 0x02, 0 }, good.AsSpan(look).ToArray());
        Assert.True(Codec.TryDecode(good, out Delta _, out _));
        foreach (byte value in new byte[] { 64, 8 | 64 }) // version 43 defines bits 4 and 5
        {
            byte[] bad = (byte[])good.Clone();
            bad[look] = value; // a look bit that is not defined
            Assert.False(Codec.TryDecode(bad, out Delta _, out ProtocolError error), $"look {value}");
            Assert.Equal(ProtocolError.BadValue, error);
        }
        byte[] zero = good.Take(look + 1).Concat(new byte[] { 0, 0 }).ToArray(); // the target 0, then 0 removed
        Assert.False(Codec.TryDecode(zero, out Delta _, out ProtocolError zeroError));
        Assert.Equal(ProtocolError.BadValue, zeroError);
    }
}
