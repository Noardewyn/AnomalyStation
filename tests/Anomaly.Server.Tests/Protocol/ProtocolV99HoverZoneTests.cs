using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 99 also (package T, slice T4b, Codec.V99.cs; docs/ai/specs/protocol.md section 95): a hover on an entity may end with the zone
/// looked at, one byte of 1 to 9 after its target. A hover without a zone is the bytes of version 97. A zone of 0 or above the groin, a zone after a
/// turf, a ceiling or a screen target, and any byte after the zone are refused.
/// </summary>
public class ProtocolV99HoverZoneTests
{
    private static ActionMessage Hover(TargetKind kind, long id, AimZone zone = AimZone.None, int x = 0, int y = 0, int z = 0)
        => new(5, ActionKind.Hover, id, ClickButton.None, ClickModifiers.None, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z, AimZone = zone };

    [Fact]
    public void AHoverOnAnEntityRoundTripsWithEachZoneAndWithoutOne()
    {
        byte[] none = Codec.Encode(Hover(TargetKind.Entity, 44));
        Assert.Equal(new byte[] { 10, 5, 7, 1, 44 }, none); // version 97's bytes, nothing more
        Assert.True(Codec.TryDecode(none, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(Hover(TargetKind.Entity, 44), back);
        for (int z = 1; z <= (int)AimZone.Last; z++)
        {
            ActionMessage hover = Hover(TargetKind.Entity, 44, (AimZone)z);
            byte[] bytes = Codec.Encode(hover);
            Assert.Equal(new byte[] { 10, 5, 7, 1, 44, (byte)z }, bytes);
            Assert.True(Codec.TryDecode(bytes, out back, out error), $"zone {z}: {error}");
            Assert.Equal(hover, back);
        }
    }

    [Fact]
    public void TheHoversOfVersion94DecodeAsBefore()
    {
        Assert.True(Codec.TryDecode(new byte[] { 10, 5, 7, 3, 44 }, out ActionMessage screen, out _));
        Assert.Equal((TargetKind.Screen, 44L, AimZone.None), (screen.TargetKind, screen.Target, screen.AimZone));
        Assert.True(Codec.TryDecode(new byte[] { 10, 5, 7, 2, 12, 200, 3 }, out ActionMessage turf, out _));
        Assert.Equal((TargetKind.Turf, 12, 200, 3, AimZone.None), (turf.TargetKind, turf.TurfX, turf.TurfY, turf.TurfZ, turf.AimZone));
    }

    [Fact]
    public void AZoneAboveTheGroinOrOfNoneIsRefusedBothWays()
    {
        foreach (byte z in new byte[] { 0, 10, 15, 0x78, 255 })
        {
            Assert.False(Codec.TryDecode(new byte[] { 10, 5, 7, 1, 44, z }, out ActionMessage _, out ProtocolError error), $"zone {z}");
            Assert.Equal(ProtocolError.BadValue, error);
            if (z != 0)
                Assert.Throws<ProtocolException>(() => Codec.Encode(Hover(TargetKind.Entity, 44, (AimZone)z)));
        }
        // nothing may follow the zone
        Assert.False(Codec.TryDecode(new byte[] { 10, 5, 7, 1, 44, 1, 1 }, out ActionMessage _, out ProtocolError trailing));
        Assert.Equal(ProtocolError.TrailingBytes, trailing);
    }

    [Fact]
    public void AZoneOnATurfACeilingOrAScreenHoverIsRefusedBothWays()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(Hover(TargetKind.Turf, 0, AimZone.Head, 12, 200, 3)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Hover(TargetKind.Ceiling, 0, AimZone.Head, 12, 200, 3)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Hover(TargetKind.Screen, 44, AimZone.Chest)));
        Assert.False(Codec.TryDecode(new byte[] { 10, 5, 7, 2, 12, 200, 3, 1 }, out ActionMessage _, out ProtocolError turf));
        Assert.Equal(ProtocolError.BadValue, turf);
        Assert.False(Codec.TryDecode(new byte[] { 10, 5, 7, 3, 44, 2 }, out ActionMessage _, out ProtocolError screen));
        Assert.Equal(ProtocolError.BadValue, screen);
    }

    // The host's own checks (ServerHost.ValidAction): a hover's zone only on an entity, and none above the groin.
    [Theory]
    [InlineData(TargetKind.Entity, AimZone.Head, true)]
    [InlineData(TargetKind.Entity, AimZone.Groin, true)]
    [InlineData(TargetKind.Entity, AimZone.None, true)]
    [InlineData(TargetKind.Entity, (AimZone)10, false)]
    [InlineData(TargetKind.Turf, AimZone.Head, false)]
    [InlineData(TargetKind.Ceiling, AimZone.Head, false)]
    [InlineData(TargetKind.Screen, AimZone.Head, false)]
    [InlineData(TargetKind.Screen, AimZone.None, true)]
    public void TheHostTakesAHoversZoneOnAnEntityOnly(TargetKind kind, AimZone zone, bool taken)
    {
        bool cell = kind is TargetKind.Turf or TargetKind.Ceiling;
        ActionMessage action = Hover(kind, cell ? 0 : 5, zone, cell ? 3 : 0, cell ? 3 : 0, cell ? 1 : 0);
        System.Reflection.MethodInfo valid = typeof(ServerHost).GetMethod("ValidAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(valid);
        object[] args = { action, null };
        Assert.Equal(taken, (bool)valid.Invoke(null, args));
        Assert.Equal(taken, args[1] == null);
    }
}
