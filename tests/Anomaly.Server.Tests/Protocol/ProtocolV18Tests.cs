using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 18 (HUD maptext, docs/ai/specs/protocol.md section 27): BYOND's maptext and its box travel in the appearance entries of a
/// session's private table: the bytes, the round trip, the refusals (a public entry with maptext, a maptext naming nothing, a box out of range),
/// the replica refusing a string the table does not have, and the string cap.
/// </summary>
public class ProtocolV18Tests
{
    private static OwnBlock OwnWith(AppearanceEntry entry)
    {
        var own = new OwnBlock { FirstNewPrivateString = 1, FirstNewPrivateAppearance = 1 };
        own.NewPrivateStrings.Add("<span class='maptext'><div align='right'>85%</div></span>");
        own.NewPrivateAppearances.Add(entry);
        return own;
    }

    private static AppearanceEntry WithText(short width = 32, short height = 32, short x = 0, short y = 0)
        => new() { Maptext = new TableRef(1, true), MaptextWidth = width, MaptextHeight = height, MaptextX = x, MaptextY = y };

    [Fact]
    public void TheVersionIsEighteen() => Assert.True(ProtocolVersion.Current >= 18); // version 19: the Tooltip message

    [Fact]
    public void AMaptextEntryIsItsPresenceBitAndTheBoxAndRoundTrips()
    {
        OwnBlock own = OwnWith(WithText(480, 16, -4, 2));
        var buffer = new byte[Codec.MaxOwnBlockSize(own)];
        int length = Codec.EncodeOwnBlock(own, buffer);
        Delta d = Msg.Delta(10, 4, 5);
        d.Own = own;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d.Own, back.Own);
        AppearanceEntry e = back.Own.NewPrivateAppearances[0];
        Assert.Equal(new TableRef(1, true), e.Maptext);
        Assert.Equal((480, 16, -4, 2), ((int)e.MaptextWidth, (int)e.MaptextHeight, (int)e.MaptextX, (int)e.MaptextY));
        Assert.True(length > 0);
        // presence bit 15 = 0x8000, a varuint of 3 bytes (80 80 02), then the string reference (1, private: 3), 480, 16 (varuints), -4, 2 (zigzag varints)
        byte[] bytes = Codec.Encode(d);
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x80, 0x80, 0x02, 0x03, 0xE0, 0x03, 0x10, 0x07, 0x04 }) >= 0, BitConverter.ToString(bytes));
    }

    [Fact]
    public void AnEntryWithoutMaptextCostsNothingMore()
    {
        var plain = new AppearanceEntry { Icon = new TableRef(1, true) };
        var own = new OwnBlock { FirstNewPrivateResource = 1, FirstNewPrivateAppearance = 1 };
        own.NewPrivateResources.Add("icons/a.dmi");
        own.NewPrivateAppearances.Add(plain);
        var buffer = new byte[Codec.MaxOwnBlockSize(own)];
        int withNothing = Codec.EncodeOwnBlock(own, buffer);
        Assert.True(Codec.TryDecode(Codec.Encode(MsgDelta(own)), out Delta back, out _));
        Assert.True(back.Own.NewPrivateAppearances[0].Maptext.IsNone);
        Assert.True(withNothing < 40);
    }

    private static Delta MsgDelta(OwnBlock own)
    {
        Delta d = Msg.Delta(10, 4, 5);
        d.Own = own;
        return d;
    }

    [Fact]
    public void APublicEntryWithMaptextRoundTripsSinceVersion74()
    {
        // version 74 (gaps W1): the world's atoms carry their maptext in the public tables (tg's status displays)
        var delta = Msg.Delta(10, 4, 5);
        delta.FirstNewAppearance = 1;
        delta.NewStrings.Add("<b>ETA</b>");
        delta.FirstNewString = 1;
        delta.NewAppearances.Add(new AppearanceEntry { Maptext = new TableRef(1, false), MaptextWidth = 32, MaptextHeight = 32, MaptextY = -4 });
        Assert.True(Codec.TryDecode(Codec.Encode(delta), out Delta decodedDelta, out ProtocolError delta74), delta74.ToString());
        Assert.Equal(new TableRef(1, false), decodedDelta.NewAppearances[0].Maptext);
        Assert.Equal(-4, decodedDelta.NewAppearances[0].MaptextY);

        // forged bytes: the same entry with the bit set in the public table
        var good = MsgDelta(OwnWith(WithText()));
        byte[] bytes = Codec.Encode(good);
        Assert.True(Codec.TryDecode(bytes, out Delta _, out _));
    }

    [Fact]
    public void AMaptextThatNamesNothingOrAnUnsetBoxIsRefused()
    {
        AppearanceEntry noText = new() { MaptextWidth = 32 };
        Assert.True(noText.Maptext.IsNone); // not present at all: nothing is written
        OwnBlock negative = OwnWith(WithText(-1));
        Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(negative, new byte[4096]));
        OwnBlock outside = OwnWith(new AppearanceEntry { Maptext = new TableRef(int.MaxValue, true) });
        Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(outside, new byte[4096]));
    }

    [Fact]
    public void TheReplicaRefusesAMaptextStringTheTableDoesNotHave()
    {
        var own = new OwnBlock { FirstNewPrivateAppearance = 1 };
        own.NewPrivateAppearances.Add(WithText()); // string 1 of the private table was never sent
        Snapshot snapshot = Msg.Snapshot(9, 4, 5, new string[0]);
        snapshot.Own = own;
        var replica = new ReplicaWorld();
        Assert.ThrowsAny<Exception>(() => replica.Apply(snapshot));
    }

    [Fact]
    public void ATableStringMayBeAKilobyteLong()
    {
        Assert.Equal(1024, Limits.MaxTableStringBytes);
        string text = "<span class='maptext'>" + new string('x', 900) + "</span>";
        var own = new OwnBlock { FirstNewPrivateString = 1, FirstNewPrivateAppearance = 1 };
        own.NewPrivateStrings.Add(text);
        own.NewPrivateAppearances.Add(WithText());
        Assert.True(Codec.TryDecode(Codec.Encode(MsgDelta(own)), out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(text, back.Own.NewPrivateStrings[0]);
    }
}
