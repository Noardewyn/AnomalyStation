using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 95 (DV586): the private block's flags bit 7 carries the own mob's melee click cooldown when it is not tg's default.</summary>
public class ProtocolV95CooldownTests
{
    [Fact]
    public void AMeleeCooldownOtherThanTgsDefaultIsSentInMilliseconds()
    {
        Delta d = Msg.Delta(9);
        d.Own.MeleeCooldownMs = 200;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d.Own, back.Own);
        Assert.Equal(200, back.Own.MeleeCooldownMs);
        var buffer = new byte[64];
        // flags 0, depth 0, nothing settled, the version 43 flags byte with bit 7, then 300 as a varuint
        Assert.Equal(new byte[] { 0, 0, 0, 128, 0xAC, 0x02 }, buffer.AsSpan(0, Codec.EncodeOwnBlock(new OwnBlock { MeleeCooldownMs = 300 }, buffer)).ToArray());
        // tg's CLICK_CD_MELEE costs nothing on the wire
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, buffer.AsSpan(0, Codec.EncodeOwnBlock(new OwnBlock(), buffer)).ToArray());
        Assert.True(Codec.TryDecode(Codec.Encode(Msg.Delta(9)), out back, out error), error.ToString());
        Assert.Equal(OwnBlock.DefaultMeleeCooldownMs, back.Own.MeleeCooldownMs);
    }
}
