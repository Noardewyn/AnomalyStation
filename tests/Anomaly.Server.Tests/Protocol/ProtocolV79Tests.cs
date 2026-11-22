using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 79 (silicons SI4d, DV388): a runtime resource, a PNG the world made, under its rsc/MD5.png path.</summary>
public class ProtocolV79Tests
{
    private static readonly byte[] Png = DmPng.Encode(2, 1, new uint[] { 0xFF0000FF, 0x00FF00FF });

    [Fact]
    public void ARuntimeResourceRoundTripsAndIsChecked()
    {
        Assert.True(ProtocolVersion.Current >= 79);
        string path = "rsc/" + new string('a', 32) + ".png";
        var message = new RuntimeResourceMessage(path, Png);
        Assert.True(Codec.TryDecode(Codec.Encode(message), out RuntimeResourceMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(path, back.Path);
        Assert.Equal(Png, back.Png);
        (int width, int height, uint[] rgba) = DmPng.Decode(back.Png);
        Assert.Equal((2, 1), (width, height));
        Assert.Equal(new uint[] { 0xFF0000FF, 0x00FF00FF }, rgba);

        foreach (string bad in new[] { "icons/obj/x.png", "rsc/" + new string('A', 32) + ".png", "rsc/abc.png", "rsc/" + new string('a', 32) + ".dmi" })
            Assert.Throws<ProtocolException>(() => Codec.Encode(new RuntimeResourceMessage(bad, Png)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new RuntimeResourceMessage(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new RuntimeResourceMessage(path, new byte[Limits.MaxRuntimeResourceBytes + 1])));
        // a decoded message whose bytes are not a PNG is refused
        byte[] encoded = Codec.Encode(message);
        encoded[^Png.Length] ^= 0xFF;
        Assert.False(Codec.TryDecode(encoded, out RuntimeResourceMessage _, out _));
    }
}
