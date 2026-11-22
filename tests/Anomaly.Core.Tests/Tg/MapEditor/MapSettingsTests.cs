using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.MapEditor;

/// <summary>A map's own settings (docs/ai/specs/map-editor.md 5.10): one key of the map json, read with its defaults and written beside tg's own keys.</summary>
public sealed class MapSettingsTests
{
    private const string Json = """
        {
        	"version": 1,
        	"map_name": "MetaStation",
        	"map_path": "map_files/MetaStation",
        	"map_file": "MetaStation.dmm",
        	"shuttles": { "cargo": "cargo_box" }
        }
        """;

    [Fact]
    public void A_json_without_the_key_has_the_games_own_and_keeps_its_text()
    {
        Assert.Equal(MapSettings.Default, MapSettings.Read(Json));
        Assert.Equal(MapSettings.Default, MapSettings.Read("not json"));
        Assert.Equal(MapSettings.Default, MapSettings.Read(null));
        // the game's own settings add no key
        string written = MapSettings.Default.Write(Json);
        Assert.DoesNotContain("anomaly", written);
        Assert.Contains("\"cargo\": \"cargo_box\"", written);
    }

    [Fact]
    public void The_settings_are_written_under_one_key_and_read_back()
    {
        var settings = new MapSettings
        {
            StationName = "Port Tarkon", Sky = MapSkyKind.Colours, SkyTop = "#102030", SkyHorizon = "#a0b0c0", Stars = 0.5f,
            Sun = true, SunColour = "#ffb060", SunEnergy = 2f, SunSize = 5f, SunElevation = 18f, SunAzimuth = 90f,
            AirInside = "o2=10;n2=10;TEMP=250", AirOutside = "co2=100;TEMP=210",
        };
        string written = settings.Write(Json);
        Assert.Equal(settings, MapSettings.Read(written));
        // tg's own keys are as they were, in tg's style of a tab a level
        Assert.Contains("\n\t\"map_name\": \"MetaStation\",", written);
        Assert.Contains("\"anomaly\": {", written);
        // written again, the text is the same; made the game's own again, the key goes
        Assert.Equal(written, settings.Write(written));
        Assert.DoesNotContain("anomaly", MapSettings.Default.Write(written));
        // only what differs from the game's own is written
        Assert.DoesNotContain("elevation", (MapSettings.Default with { StationName = "X" }).Write(Json));
    }

    [Fact]
    public void A_value_of_another_kind_or_outside_its_range_is_the_games_own()
    {
        MapSettings read = MapSettings.Read("""
            { "anomaly": { "station_name": 5, "sky": { "kind": "clouds", "top": "blue", "image": "../secret.png", "stars": 9 },
              "sun": { "on": false, "energy": -1, "size": 100, "elevation": "high", "azimuth": 400 }, "air": { "inside": "o2=many", "outside": "n2=80;TEMP=100" } } }
            """);
        MapSettings d = MapSettings.Default;
        Assert.Null(read.StationName);
        Assert.Equal(MapSkyKind.Space, read.Sky);
        Assert.Equal(d.SkyTop, read.SkyTop);
        Assert.Null(read.SkyImage);
        Assert.Equal(d.Stars, read.Stars);
        Assert.False(read.Sun);
        Assert.Equal((d.SunEnergy, d.SunSize, d.SunElevation), (read.SunEnergy, read.SunSize, read.SunElevation));
        Assert.Null(read.SunAzimuth);
        Assert.Null(read.AirInside);
        Assert.Equal("n2=80;TEMP=100", read.AirOutside);
    }

    [Theory]
    [InlineData("o2=22;n2=82;TEMP=293.15", "o2=22;n2=82;TEMP=293.15")]
    [InlineData(" o2 = 22 ; TEMP=2.7; ", "o2=22;TEMP=2.7")]
    [InlineData("TEMP=2.7", "TEMP=2.7")]
    [InlineData("o2", null)]
    [InlineData("o2=-1", null)]
    [InlineData("o2=1;;n2=2", null)]
    [InlineData("", null)]
    public void A_gas_mix_is_pairs_of_a_name_and_an_amount(string text, string mix) => Assert.Equal(mix, MapSettings.GasMix(text));

    // D347: the key's old name is read, and a write leaves only the new one
    [Fact]
    public void The_old_key_is_read_and_replaced()
    {
        const string old = """{ "map_name": "Old", "ss3d": { "station_name": "Port" } }""";
        Assert.Equal("Port", MapSettings.Read(old).StationName);
        string written = MapSettings.Read(old).Write(old);
        Assert.DoesNotContain("ss3d", written);
        Assert.Equal("Port", MapSettings.Read(written).StationName);
    }

    // tg's map jsons may hold comments and trailing commas, which the host lets through: so do the settings, and they are kept when written.
    [Fact]
    public void A_json_with_comments_and_trailing_commas_is_read()
    {
        const string commented = """
            {
            	// the station
            	"map_name": "Commented",
            	"anomaly": { "station_name": "Port", },
            }
            """;
        Assert.Equal("Port", MapSettings.Read(commented).StationName);
        string written = (MapSettings.Read(commented) with { Sun = false }).Write(commented);
        Assert.Contains("\"map_name\": \"Commented\"", written);
        Assert.False(MapSettings.Read(written).Sun);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => MapSettings.Default.Write("{ not json"));
        Assert.Null(MapSettings.ParseObject("[1, 2]"));
    }

    [Fact]
    public void A_colour_is_six_hex_digits()
    {
        Assert.Equal("#ffb060", MapSettings.Colour("#FFB060"));
        Assert.Null(MapSettings.Colour("ffb060"));
        Assert.Null(MapSettings.Colour("#ffb06"));
        Assert.Equal(0xFFB060u, MapSettings.Rgb("#ffb060"));
    }
}
