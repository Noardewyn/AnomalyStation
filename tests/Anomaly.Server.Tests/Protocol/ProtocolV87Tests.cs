using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 87 (D336, MessagesV87.cs): a map's own sky and sun at the end of MapGrid.</summary>
public class ProtocolV87Tests
{
    private static readonly byte[] Png = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70 };

    private static MapGrid Map(MapLook look)
    {
        MapGrid map = Msg.Map(2, 2, 1, new[] { "/turf/open/floor" }, new TurfRun(4, 0));
        map.Look = look;
        return map;
    }

    private static MapGrid RoundTrip(MapGrid map)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(map), out MapGrid read, out ProtocolError error), error.ToString());
        return read;
    }

    [Fact]
    public void AMapWithTheGamesOwnSkySendsOneByteMore()
    {
        Assert.Null(RoundTrip(Map(null)).Look);
        Assert.Equal(Codec.Encode(Map(null)).Length + 28, Codec.Encode(Map(new MapLook())).Length);
    }

    [Fact]
    public void TheSkyAndTheSunAreCarried()
    {
        var look = new MapLook { Sky = MapLook.SkyColours, SkyTop = 0x102030, SkyHorizon = 0xA0B0C0, Stars = 0.5f, Sun = false, SunColour = 0xFFB060, SunEnergy = 2f, SunSize = 5f, SunElevation = 18f };
        Assert.Equal(look, RoundTrip(Map(look)).Look);
        foreach (byte[] picture in new[] { Png, Jpg })
        {
            var pictured = new MapLook { Sky = MapLook.SkyImage, Image = picture };
            Assert.Equal(pictured, RoundTrip(Map(pictured)).Look);
        }
    }

    [Fact]
    public void BadSkiesAndSunsAreRefused()
    {
        foreach (MapLook bad in new[]
                 {
                     new MapLook { Sky = 4 }, new MapLook { Stars = 5f }, new MapLook { Stars = float.NaN }, new MapLook { SkyTop = 0x1000000 },
                     new MapLook { SunEnergy = 9f }, new MapLook { SunSize = 0.1f }, new MapLook { SunElevation = 0f }, new MapLook { SunElevation = 91f },
                     new MapLook { Sky = MapLook.SkyImage, Image = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 } }, // no picture
                     new MapLook { Sky = MapLook.SkyStars, Image = Png }, // a picture for a sky that has none
                     new MapLook { Sky = MapLook.SkyImage, Image = new byte[Limits.MaxSkyImageBytes + 1] },
                 })
            Assert.Throws<ProtocolException>(() => Codec.Encode(Map(bad)));

        // a decoder refuses the same: the flag, and a kind out of range
        byte[] good = Codec.Encode(Map(new MapLook()));
        int at = good.Length - 31; // (before version 91's wall height) the flag, the sky's kind, its colours, its stars, its picture's length, then the sun's flag
        foreach ((int offset, byte value) in new[] { (0, (byte)2), (1, (byte)9), (13, (byte)2) })
        {
            byte[] crafted = (byte[])good.Clone();
            crafted[at + offset] = value;
            Assert.False(Codec.TryDecode(crafted, out MapGrid _, out ProtocolError _));
        }
        Assert.False(Codec.TryDecode(good.AsSpan(0, good.Length - 4), out MapGrid _, out ProtocolError _));
    }
}
