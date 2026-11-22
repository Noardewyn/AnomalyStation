using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 92 (MessagesV92.cs): the own gun's sight, its cone and the own shots' hits.</summary>
public class ProtocolV92GunSightTests
{
    [Fact]
    public void TheSightIsCarriedAndABadOneIsRefused()
    {
        var sight = new GunSightMessage(true, 3.5f, 255);
        byte[] bytes = Codec.Encode(sight);
        Assert.Equal(Limits.MaxGunSightBytes, bytes.Length);
        Assert.True(Codec.TryDecode(bytes, out GunSightMessage read, out ProtocolError error), error.ToString());
        Assert.Equal(sight, read);
        Assert.Equal(bytes, Codec.Encode(read));
        Assert.True(Codec.TryDecode(Codec.Encode(GunSightMessage.None), out read, out _));
        Assert.Equal(GunSightMessage.None, read);

        Assert.Throws<ProtocolException>(() => Codec.Encode(new GunSightMessage(true, 91f, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new GunSightMessage(true, float.NaN, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new GunSightMessage(false, 2f, 0))); // a cone without a gun
        byte[] bad = (byte[])bytes.Clone();
        bad[1] = 2; // the gun flag
        Assert.False(Codec.TryDecode(bad, out GunSightMessage _, out _));
        bad = (byte[])bytes.Clone();
        BitConverter.GetBytes(-1f).CopyTo(bad, 2);
        Assert.False(Codec.TryDecode(bad, out GunSightMessage _, out _));
        Assert.False(Codec.TryDecode(bytes.Append((byte)0).ToArray(), out GunSightMessage _, out error));
        Assert.Equal(ProtocolError.TooLong, error);
        Assert.False(Codec.TryDecode(bytes[..^1], out GunSightMessage _, out _));
    }
}
