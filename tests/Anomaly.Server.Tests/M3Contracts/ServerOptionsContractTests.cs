using Anomaly.Server;

namespace Anomaly.Server.Tests.M3Contracts;

/// <summary>The M3 contracts step's server options: MAP, MAPS_FOLDER and the access dev command's syntax.</summary>
public class ServerOptionsContractTests
{
    private static ServerConfig Parse(string[] args, string file, out string error)
        => ServerConfig.Parse(args, path => path == "config/server.txt" ? file : null, out error);

    // DV96 (the owner, 2026-11-20): aimed shots keep half of tg's spread by default; the command log replays a round with its own share
    [Fact]
    public void TheSpreadScaleIsHalfTgsAndAReplayKeepsIt()
    {
        Assert.Equal(0.5f, Parse(new[] { "--ticks", "1" }, null, out _).SpreadScale);
        Assert.Equal(1f, Parse(new[] { "--ticks", "1", "--spread-scale", "1" }, null, out _).SpreadScale);
        Assert.Equal(0.25f, Parse(new[] { "--ticks", "1" }, "SPREAD_SCALE 0.25\n", out _).SpreadScale);
        Assert.Null(Parse(new[] { "--ticks", "1", "--spread-scale", "3" }, null, out string error));
        Assert.Contains("--spread-scale", error);

        string text = CommandLog.For(new ServerConfig { SpreadScale = 0.5f }).ToText();
        Assert.Contains("spreadscale 0.5\n", text);
        Assert.Equal(0.5f, CommandLog.Parse(text, out error).ReplayConfig().SpreadScale);
        // a log from before the setting was played with tg's spread
        Assert.Equal(1f, CommandLog.Parse(text.Replace("spreadscale 0.5\n", ""), out error).ReplayConfig().SpreadScale);
    }

    [Fact]
    public void TheMapOptionNamesAMapJson()
    {
        ServerConfig c = Parse(new[] { "--ticks", "1" }, null, out _);
        Assert.Null(c.Map); // the test room
        Assert.Equal("maps", c.MapsFolder);
        c = Parse(new[] { "--ticks", "1", "--map", "metastation", "--maps-folder", "/tmp/maps" }, null, out _);
        Assert.Equal("metastation", c.Map);
        Assert.Equal("/tmp/maps", c.MapsFolder);
        c = Parse(new[] { "--ticks", "1" }, "MAP metastation\nMAPS_FOLDER converted\n", out _);
        Assert.Equal("metastation", c.Map);
        Assert.Equal("converted", c.MapsFolder);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("maps/metastation")]
    [InlineData("meta station")]
    public void AMapIsAPlainName(string name)
    {
        Assert.Null(Parse(new[] { "--ticks", "1", "--map", name }, null, out string error));
        Assert.Contains("--map", error);
    }

    [Fact]
    public void AnEmptyWorldHasNoMap()
    {
        Assert.Null(Parse(new[] { "--ticks", "1", "--empty", "--map", "metastation" }, null, out string error));
        Assert.Contains("--map", error);
    }

    [Fact]
    public void AccessIsRetiredAndGiveTakesAnItemPath()
    {
        // M4a P5 (D130): the ID card gives access; give spawns tg's items (the debug card has all access).
        Assert.False(DevCommandLine.IsDevVerb("access"));
        Assert.True(DevCommandLine.TryParse("give me /obj/item/card/id/advanced/debug", out DevCommand give, out string error), error);
        Assert.Equal((DevVerb.Give, "me", "/obj/item/card/id/advanced/debug"), (give.Verb, give.Who, give.TypePath));
        Assert.False(DevCommandLine.TryParse("give me /mob/living/carbon/human", out _, out _));
    }
}
