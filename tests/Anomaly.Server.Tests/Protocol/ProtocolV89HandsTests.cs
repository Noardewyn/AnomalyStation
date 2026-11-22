using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 89 (DV179): the private block's flags bit 6 says the own mob's hands are blocked, with no bytes of its own.</summary>
public class ProtocolV89HandsTests
{
    [Fact]
    public void BlockedHandsAreAFlagOfTheOwnBlock()
    {
        Delta d = Msg.Delta(9);
        d.Own.HandsBlocked = true;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d.Own, back.Own);
        Assert.True(back.Own.HandsBlocked);
        var buffer = new byte[64];
        // flags 0, depth 0, nothing settled, the version 43 flags byte with bit 6
        Assert.Equal(new byte[] { 0, 0, 0, 64 }, buffer.AsSpan(0, Codec.EncodeOwnBlock(new OwnBlock { HandsBlocked = true }, buffer)).ToArray());
        Assert.True(ProtocolVersion.Current >= 89); // the flag came in with 89 and stays
    }
}
