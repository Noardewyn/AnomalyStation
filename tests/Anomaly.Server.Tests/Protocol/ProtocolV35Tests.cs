using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 35 (M8 slice G, D187, docs/ai/specs/protocol.md section 44): a private image carries BYOND's override flag; an override image
/// may draw nothing. Bytes, round trips, the refusals.
/// </summary>
public class ProtocolV35Tests
{
    [Fact]
    public void TheVersionIsAtLeast35()
    {
        Assert.True(ProtocolVersion.Current >= 35); // version 36 (BuildInfoTests pins the current one)
    }

    [Fact]
    public void AnOverrideImageRoundTripsWithItsFlagAndAnEmptyOneNamesNoAppearance()
    {
        Delta d = Msg.Delta(300);
        d.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(3, true), 7) { Override = true });
        d.Own.ImagesAdded.Add(new ImageEntry(2, default, 9) { Override = true });
        d.Own.ImagesAdded.Add(new ImageEntry(3, new TableRef(4, true), 7));
        byte[] bytes = Codec.Encode(d);
        Assert.True(Codec.TryDecode(bytes, out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(d.Own.ImagesAdded, back.Own.ImagesAdded);
        Assert.True(back.Own.ImagesAdded[0].Override);
        Assert.True(back.Own.ImagesAdded[1].Override);
        Assert.True(back.Own.ImagesAdded[1].Appearance.IsNone);
        Assert.False(back.Own.ImagesAdded[2].Override);
    }

    [Fact]
    public void ThePlainImageMustNameAnAppearanceAndTheFlagIsZeroOrOne()
    {
        Delta none = Msg.Delta(300);
        none.Own.ImagesAdded.Add(new ImageEntry(1, default, 9));
        Assert.Throws<ProtocolException>(() => Codec.Encode(none));

        Delta one = Msg.Delta(300);
        one.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(2, true), 9) { Override = true });
        byte[] good = Codec.Encode(one);
        // the image list is the block's tail: the flag is the last byte of the one image, followed by the 0 changed and the 0 removed counts
        int flag = Array.LastIndexOf(good, (byte)1, good.Length - 3);
        byte[] bad = (byte[])good.Clone();
        bad[flag] = 2;
        Assert.False(Codec.TryDecode(bad, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
    }
}
