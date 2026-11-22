using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 67 (D315, docs/ai/specs/protocol.md section 68): a z-level's minimap.</summary>
public class ProtocolV67Tests
{
    private static MinimapMessage Sample()
    {
        var m = new MinimapMessage { Z = 2, MinX = 10, MinY = 20, Width = 3, Height = 2 };
        m.Colours.Add(0x111111D0);
        m.Colours.Add(0x8BA4C5D5);
        m.PixelRuns.Add(new TurfRun(1, 1));
        m.PixelRuns.Add(new TurfRun(4, 2));
        m.PixelRuns.Add(new TurfRun(1, 0));
        m.AreaNames.Add("Medbay Central");
        m.AreaCodes.Add("MED");
        m.AreaRuns.Add(new TurfRun(5, 1));
        m.AreaRuns.Add(new TurfRun(1, 0));
        return m;
    }

    [Fact]
    public void AMinimapRoundTripsAndExpandsPerCell()
    {
        byte[] bytes = Codec.Encode(Sample());
        Assert.True(Codec.TryDecode(bytes, out MinimapMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal((2, 10, 20, 3, 2), (decoded.Z, decoded.MinX, decoded.MinY, decoded.Width, decoded.Height));
        Assert.Equal(new uint[] { 0x111111D0, 0x8BA4C5D5 }, decoded.Colours);
        Assert.Equal(new[] { 1, 2, 2, 2, 2, 0 }, decoded.ExpandPixels());
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 0 }, decoded.ExpandAreas());
        Assert.Equal("Medbay Central", Assert.Single(decoded.AreaNames));
        Assert.Equal("MED", Assert.Single(decoded.AreaCodes));
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    [Fact]
    public void APictureOffTheMapRunsPastItsCellsOrAnIndexPastItsTableIsRefused()
    {
        MinimapMessage wide = Sample();
        wide.MinX = Limits.MaxMapSize - 1; // 3 columns from 254 end past 255
        Assert.Throws<ProtocolException>(() => Codec.Encode(wide));

        MinimapMessage longRuns = Sample();
        longRuns.PixelRuns.Add(new TurfRun(1, 1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(longRuns));

        MinimapMessage badColour = Sample();
        badColour.PixelRuns[0] = new TurfRun(1, 3);
        Assert.Throws<ProtocolException>(() => Codec.Encode(badColour));

        byte[] bytes = Codec.Encode(Sample());
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out MinimapMessage _, out _));
        byte[] trailing = [.. bytes, 0];
        Assert.False(Codec.TryDecode(trailing, out MinimapMessage _, out _));
        bytes[4] = 0xFF; // the width's varuint, now past the map's edge
        Assert.False(Codec.TryDecode(bytes, out MinimapMessage _, out _));
    }
}
