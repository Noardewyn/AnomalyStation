using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 91 (MessagesV91.cs): the server's wall height at the end of MapGrid.</summary>
public class ProtocolV91WallHeightTests
{
    private static MapGrid Map(int mm)
    {
        MapGrid map = Msg.Map(2, 2, 1, new[] { "/turf/open/floor" }, new TurfRun(4, 0));
        map.WallHeightMillimetres = mm;
        return map;
    }

    [Fact]
    public void TheWallHeightIsCarriedAndAHeightOutOfRangeIsRefused()
    {
        Assert.Equal(2000, new MapGrid().WallHeightMillimetres);
        Assert.True(Codec.TryDecode(Codec.Encode(Map(3250)), out MapGrid read, out ProtocolError error), error.ToString());
        Assert.Equal(3250, read.WallHeightMillimetres);
        Assert.Throws<ProtocolException>(() => Codec.Encode(Map(1999)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Map(4001)));
        byte[] bytes = Codec.Encode(Map(2000));
        bytes[^2] = 0xA1; // 4001 = 0x0FA1, little-endian
        bytes[^1] = 0x0F;
        Assert.False(Codec.TryDecode(bytes, out MapGrid _, out _));
    }
}
