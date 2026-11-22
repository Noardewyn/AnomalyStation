using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 68 (D315, docs/ai/specs/protocol.md section 69): the minimap's dev trackers.</summary>
public class ProtocolV68Tests
{
    [Fact]
    public void TheLivingMobsRoundTrip()
    {
        var m = new MinimapMobsMessage { Z = 3 };
        m.Mobs.Add(new MinimapMob(1, 255, true, "Alice"));
        m.Mobs.Add(new MinimapMob(40, 7, false, "the mouse"));
        m.Mobs.Add(new MinimapMob(2, 2, false, null));
        byte[] bytes = Codec.Encode(m);
        Assert.True(Codec.TryDecode(bytes, out MinimapMobsMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(3, decoded.Z);
        Assert.Equal(m.Mobs, decoded.Mobs);
        Assert.Equal(bytes, Codec.Encode(decoded));
        Assert.True(Codec.TryDecode(Codec.Encode(new MinimapMobsMessage { Z = 1 }), out MinimapMobsMessage empty, out _));
        Assert.Empty(empty.Mobs);
    }

    [Fact]
    public void AMobOffTheMapABadFlagOrATrailingByteIsRefused()
    {
        var off = new MinimapMobsMessage { Z = 1 };
        off.Mobs.Add(new MinimapMob(0, 5, false, "x"));
        Assert.Throws<ProtocolException>(() => Codec.Encode(off));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new MinimapMobsMessage { Z = Limits.MaxMapLevels + 1 }));

        var one = new MinimapMobsMessage { Z = 1 };
        one.Mobs.Add(new MinimapMob(5, 5, true, "a"));
        byte[] bytes = Codec.Encode(one);
        byte[] flag = (byte[])bytes.Clone();
        flag[5] = 2; // [id][z][count][x][y][player]
        Assert.False(Codec.TryDecode(flag, out MinimapMobsMessage _, out _));
        Assert.False(Codec.TryDecode([.. bytes, 0], out MinimapMobsMessage _, out _));
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out MinimapMobsMessage _, out _));
    }
}
