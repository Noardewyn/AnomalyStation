using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 31 (M7 slice T3, DV81, docs/ai/specs/protocol.md section 40): the private block's vision (tg's sight flags and lighting
/// cutoffs). Its bytes, the round trip, the refusal of a value out of range, the replica's state, the lighting floor tg's set_light_cutoff
/// makes of the cutoffs, and the client's colour matrix of the game plane's filters (the mesons' tint).
/// </summary>
public class ProtocolV31Tests
{
    private static OwnBlock Decoded(Delta d)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        return back.Own;
    }

    [Fact]
    public void TheVersionIsThirtyOne() => Assert.True(ProtocolVersion.Current >= 31);

    [Fact]
    public void TheVisionRoundTripsAndItsBytesAreTheSightThenTheFourCutoffs()
    {
        Delta d = Msg.Delta(9);
        d.Own.VisionChanged = true;
        d.Own.Vision = new VisionState(SightFlags.SeeTurfs, 4.5f, 5f, 15f, 5f);
        OwnBlock own = Decoded(d);
        Assert.True(own.VisionChanged);
        Assert.Equal(d.Own.Vision, own.Vision);
        var buffer = new byte[256];
        int n = Codec.EncodeOwnBlock(d.Own, buffer);
        // flags (vision, bit 7), depth 0, version 43's 0 actions done and flags, sight 16, then 4.5, 5, 15 and 5 as little endian singles
        byte[] expected = { 128, 0, 0, 0, 16, 0, 0, 0x90, 0x40, 0, 0, 0xA0, 0x40, 0, 0, 0x70, 0x41, 0, 0, 0xA0, 0x40 };
        Assert.Equal(expected, buffer.AsSpan(0, n).ToArray());
    }

    [Fact]
    public void ABlockWithoutVisionLeavesItUnchangedOnTheWire()
    {
        Delta d = Msg.Delta(9);
        OwnBlock own = Decoded(d);
        Assert.False(own.VisionChanged);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(100.5f, 0f)]
    [InlineData(float.NaN, 0f)]
    [InlineData(0f, 101f)]
    public void ACutoffOutOfRangeIsRefusedOnTheWire(float lighting, float red)
    {
        Delta d = Msg.Delta(9);
        d.Own.VisionChanged = true;
        d.Own.Vision = new VisionState(0, lighting, red, 0f, 0f);
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }

    [Fact]
    public void ASightAbove24BitsIsRefusedWhenSentAndWhenRead()
    {
        Delta d = Msg.Delta(9);
        d.Own.VisionChanged = true;
        d.Own.Vision = new VisionState(1 << 24, 0f, 0f, 0f, 0f);
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        // crafted: no ceilings (version 44), no sun (version 47), no turf lights (version 96), flags 128, depth 0, version 43's 0 and 0, a sight of 2^24 (varuint 0x80 0x80 0x80 0x08) and four zero singles
        byte[] bytes = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0)
            .Bytes(new byte[] { 128, 0, 0, 0, 0x80, 0x80, 0x80, 0x08, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        Assert.False(Codec.TryDecode(bytes, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
    }

    [Fact]
    public void TheReplicaKeepsTheVisionUntilTheServerChangesItAndAForeignSnapshotResetsIt()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(8, 8));
        world.Apply(Msg.Snapshot(10, 1, 0, new[] { "/mob" }, Msg.State(1, 0, 3, 3)));
        Assert.Equal(VisionState.Normal, world.OwnVision);
        Delta d = Msg.Delta(world.Tick + 1);
        d.Own.VisionChanged = true;
        d.Own.Vision = new VisionState(SightFlags.SeeTurfs, 0f, 5f, 15f, 5f);
        world.Apply(d);
        Assert.True(world.OwnVision.SeesTurfs);
        world.Apply(Msg.Delta(world.Tick + 1)); // nothing named: unchanged
        Assert.True(world.OwnVision.SeesTurfs);
        Delta back = Msg.Delta(world.Tick + 1);
        back.Own.VisionChanged = true;
        back.Own.Vision = VisionState.Normal;
        world.Apply(back);
        Assert.False(world.OwnVision.SeesTurfs);
    }

    [Fact]
    public void TheLightingFloorIsTgsCutoffPlusTheChannelsCutoffOverAHundred()
    {
        // tg's set_light_cutoff (render_plate.dm:337): the colour matrix filters subtract and add back (cutoff + channel cutoff) / 100,
        // so a channel never shows less than that; the optical mesons: (0, 5, 15, 5), the night vision ones (0, 10, 35, 10)
        (float r, float g, float b) = new VisionState(SightFlags.SeeTurfs, 0f, 5f, 15f, 5f).LightFloor;
        Assert.Equal((0.05f, 0.15f, 0.05f), (r, g, b));
        (r, g, b) = new VisionState(0, 30f, 0f, 0f, 0f).LightFloor; // LIGHTING_CUTOFF_HIGH, a night vision eye
        Assert.Equal((0.3f, 0.3f, 0.3f), (r, g, b));
        (r, g, b) = new VisionState(0, 100f, 50f, 0f, 0f).LightFloor;
        Assert.Equal((1f, 1f, 1f), (r, g, b)); // clamped
        Assert.Equal((0f, 0f, 0f), VisionState.Normal.LightFloor);
    }

    [Fact]
    public void TheGamePlanesColourFiltersMakeOneMatrixThatTintsWhiteLightGreen()
    {
        // the mesons' client colour: color_matrix_filter("#ccffcc") reaches the wire as the diagonal (0.8, 1, 0.8)
        var green = new FilterEntry { Kind = FilterKind.Color, ColorMatrix = new float[] { 0.8f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0.8f, 0, 0, 0, 0, 1, 0, 0, 0, 0 } };
        var blur = new FilterEntry { Kind = FilterKind.Blur, Size = 2f };
        Assert.Null(ViewBlur.ColorMatrix(null));
        Assert.Null(ViewBlur.ColorMatrix(new[] { blur }));
        float[] matrix = ViewBlur.ColorMatrix(new[] { blur, green });
        (float r, float g, float b) = ViewBlur.Apply(matrix, 1f, 1f, 1f);
        Assert.Equal((0.8f, 1f, 0.8f), (r, g, b));
        // two filters apply in order: the green, then a half-bright red-only one
        var half = new FilterEntry { Kind = FilterKind.Color, ColorMatrix = new float[] { 0.5f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0 } };
        (r, g, b) = ViewBlur.Apply(ViewBlur.ColorMatrix(new[] { green, half }), 1f, 1f, 1f);
        Assert.Equal(0.4f, r, 4);
        Assert.Equal(1f, g, 4);
        Assert.Equal(0.8f, b, 4);
        // a constant row adds and the result is clamped
        var lift = new FilterEntry { Kind = FilterKind.Color, ColorMatrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.5f, 0.5f, 0.5f, 0 } };
        (r, g, b) = ViewBlur.Apply(ViewBlur.ColorMatrix(new[] { lift }), 0.8f, 0.2f, 0f);
        Assert.Equal((1f, 0.7f, 0.5f), (r, g, b));
    }
}
