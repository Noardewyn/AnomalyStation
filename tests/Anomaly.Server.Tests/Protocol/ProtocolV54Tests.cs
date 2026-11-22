using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 54 (D285, docs/ai/specs/protocol.md section 59): the own gun's held motion.</summary>
public class ProtocolV54Tests
{
    [Theory]
    [InlineData(HeldMotionKind.Load)]
    [InlineData(HeldMotionKind.Unload)]
    [InlineData(HeldMotionKind.Rack)]
    public void AHeldMotionRoundTrips(HeldMotionKind kind)
    {
        byte[] bytes = Codec.Encode(new HeldMotionMessage(kind));
        Assert.True(Codec.TryDecode(bytes, out HeldMotionMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(kind, decoded.Kind);
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    [Fact]
    public void AnUnknownKindOrATrailingByteIsRefused()
    {
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.HeldMotion, 3 }, out HeldMotionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.HeldMotion, 0, 0 }, out HeldMotionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.HeldMotion }, out HeldMotionMessage _, out _));
    }
}
