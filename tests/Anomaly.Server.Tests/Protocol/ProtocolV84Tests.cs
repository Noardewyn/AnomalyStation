using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 84 (gaps E2a): the weather of a level.</summary>
public class ProtocolV84Tests
{
    private static WeatherMessage Sample()
    {
        var m = new WeatherMessage { Z = 2 };
        var storm = new WeatherLayer
        {
            Id = 7, Stage = 2, State = "ash_storm", Colour = 0x008000FF, Alpha = 255, Glow = true,
            MinX = 10, MinY = 20, Width = 3, Height = 2,
        };
        storm.Cells.Add(new TurfRun(2, 1));
        storm.Cells.Add(new TurfRun(3, 0));
        storm.Cells.Add(new TurfRun(1, 1));
        m.Layers.Add(storm);
        var rain = new WeatherLayer { Id = 8, Stage = 1, State = "rain_low", Particles = "rain_storm", Severity = 30, Wind = -1 };
        m.Layers.Add(rain);
        return m;
    }

    [Fact]
    public void AWeatherRoundTripsAndExpandsItsCells()
    {
        byte[] bytes = Codec.Encode(Sample());
        Assert.True(Codec.TryDecode(bytes, out WeatherMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(2, decoded.Z);
        Assert.Equal(2, decoded.Layers.Count);
        WeatherLayer storm = decoded.Layers[0];
        Assert.Equal((7, 2, "ash_storm", 0x008000FFu, 255, true), (storm.Id, storm.Stage, storm.State, storm.Colour, storm.Alpha, storm.Glow));
        Assert.Equal((10, 20, 3, 2), (storm.MinX, storm.MinY, storm.Width, storm.Height));
        Assert.Equal(new[] { true, true, false, false, false, true }, storm.ExpandCells());
        WeatherLayer rain = decoded.Layers[1];
        Assert.Equal(("rain_storm", 30, -1), (rain.Particles, rain.Severity, rain.Wind));
        Assert.Empty(rain.Cells);
        Assert.Equal(bytes, Codec.Encode(decoded));
        Assert.True(Codec.TryDecode(Codec.Encode(new WeatherMessage { Z = 1 }), out WeatherMessage clear, out _));
        Assert.Empty(clear.Layers);
    }

    [Fact]
    public void AStageASeverityOrABoxOutOfRangeIsRefused()
    {
        WeatherMessage stage = Sample();
        stage.Layers[0].Stage = 4;
        Assert.Throws<ProtocolException>(() => Codec.Encode(stage));

        WeatherMessage severity = Sample();
        severity.Layers[1].Severity = 101;
        Assert.Throws<ProtocolException>(() => Codec.Encode(severity));

        WeatherMessage wide = Sample();
        wide.Layers[0].MinX = Limits.MaxMapSize - 1;
        Assert.Throws<ProtocolException>(() => Codec.Encode(wide));

        WeatherMessage longRuns = Sample();
        longRuns.Layers[0].Cells.Add(new TurfRun(1, 1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(longRuns));

        byte[] bytes = Codec.Encode(Sample());
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out WeatherMessage _, out _));
        byte[] trailing = [.. bytes, 0];
        Assert.False(Codec.TryDecode(trailing, out WeatherMessage _, out _));
    }
}
