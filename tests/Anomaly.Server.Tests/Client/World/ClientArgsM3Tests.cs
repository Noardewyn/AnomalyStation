using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 P6: the arguments of the world renderer and the bench, and the quality option.</summary>
public sealed class ClientArgsM3Tests
{
    [Fact]
    public void TheFakeMapAndTheViewParse()
    {
        ClientArgs args = ClientArgs.Parse(new[] { "--fake-server", "--fake-map=/tmp/m.as3dmap", "--view=45,121.6,1.6,90,-5", "--ceiling", "--quality=high" });
        Assert.Empty(args.Errors);
        Assert.Equal("/tmp/m.as3dmap", args.FakeMapPath);
        Assert.Equal((45.0, 121.6, 1.6, 90.0, -5.0), args.View.Value);
        Assert.Equal(CameraMode.Free, args.Camera);
        Assert.True(args.ShowCeiling);
        Assert.Equal("high", args.Quality);
    }

    [Fact]
    public void AFakeMapWithoutAValueMeansTheDefaultPath()
    {
        ClientArgs args = ClientArgs.Parse(new[] { "--fake-server", "--fake-map" });
        Assert.Equal(string.Empty, args.FakeMapPath);
        Assert.Null(ClientArgs.Parse(new[] { "--fake-server" }).FakeMapPath);
    }

    [Fact]
    public void TheBenchHasDefaultsAndOptions()
    {
        ClientArgs plain = ClientArgs.Parse(new[] { "--bench" });
        Assert.True(plain.Bench);
        Assert.True(plain.Automated);
        Assert.Equal(60.0, plain.BenchSeconds);
        Assert.Equal(new[] { "low", "medium", "high", "max" }, plain.BenchPresets);
        Assert.Null(plain.BenchOut);

        ClientArgs custom = ClientArgs.Parse(new[] { "--bench=12", "--bench-out=/tmp/b.txt", "--bench-presets=medium,low" });
        Assert.Empty(custom.Errors);
        Assert.Equal(12.0, custom.BenchSeconds);
        Assert.Equal("/tmp/b.txt", custom.BenchOut);
        Assert.Equal(new[] { "medium", "low" }, custom.BenchPresets);
    }

    [Theory]
    [InlineData("--quality=ultra")]
    [InlineData("--view=1,2,3")]
    [InlineData("--bench=0")]
    [InlineData("--bench-out=relative.txt")]
    [InlineData("--bench-presets=low,wow")]
    [InlineData("--wall-height=1.5")]
    [InlineData("--wall-height=9")]
    [InlineData("--wall-height=tall")]
    [InlineData("--wall-height")]
    [InlineData("--occlusion=maybe")]
    [InlineData("--occlusion")]
    public void BadValuesAreRecordedNotThrown(string arg) => Assert.NotEmpty(ClientArgs.Parse(new[] { arg }).Errors);

    [Fact]
    public void OcclusionCullingIsOnUnlessTurnedOff()
    {
        Assert.Null(ClientArgs.Parse(Array.Empty<string>()).Occlusion);
        Assert.True(ClientArgs.Parse(new[] { "--occlusion=on" }).Occlusion);
        Assert.False(ClientArgs.Parse(new[] { "--occlusion=off" }).Occlusion);
    }

    [Fact]
    public void TheQualityOptionRoundTripsThroughTheFile()
    {
        var options = new PlayOptions();
        Assert.Equal("medium", options.Quality);
        Assert.True(options.TrySet("quality", "low", out _));
        Assert.False(options.TrySet("quality", "ultra", out string problem));
        Assert.Contains("low, medium, high, max or custom", problem);
        PlayOptions again = PlayOptions.Parse(options.ToText());
        Assert.Equal("low", again.Quality);
    }

    [Fact]
    public void TheWallHeightIsTheServersAndAnOldOptionLineIsIgnored()
    {
        // the owner (2026-11-20, D119 revised): the server's setting (MapGrid, protocol 91), no argument nor option of the client's
        Assert.NotEmpty(ClientArgs.Parse(new[] { "--wall-height=2.4" }).Errors);
        var options = new PlayOptions();
        Assert.True(options.TrySet("wall_height", "2.5", out _));
        Assert.DoesNotContain("wall_height", options.ToText());
        Assert.Empty(ClientArgs.Parse(new[] { "--option=wall_height=3" }).Errors);
    }

    [Fact]
    public void TheFlashlightIsAnActionOnL()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.Flashlight, keys.ActionOf("L"));
        Assert.Equal("flashlight", KeyBindings.IdOf(PlayAction.Flashlight));
    }
}
