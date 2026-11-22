using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Version 47: the Delta carries tg's SSsun.azimuth for the client's sky.</summary>
public sealed class ProtocolV47Tests
{
    [Fact]
    public void TheSunsAzimuthRoundTripsInHundredthsAndNoSunStaysNaN()
    {
        foreach ((float sent, float read) in new[] { (0f, 0f), (123.456f, 123.46f), (359.99f, 359.99f) })
        {
            Delta d = Msg.Delta(3);
            d.SunAzimuth = sent;
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(read, decoded.SunAzimuth, 3);
        }
        Assert.True(Codec.TryDecode(Codec.Encode(Msg.Delta(3)), out Delta none, out _));
        Assert.True(float.IsNaN(none.SunAzimuth));
    }

    [Fact]
    public void AnAzimuthOf360OrMoreIsRefused()
    {
        // crafted: no cells, no ceilings, a sun of 1 + 36000, the empty private block
        byte[] bytes = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0)
            .VarUInt(36001).Byte(0).Byte(0).VarUInt(0).Byte(0));
        Assert.False(Codec.TryDecode(bytes, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
    }
}
