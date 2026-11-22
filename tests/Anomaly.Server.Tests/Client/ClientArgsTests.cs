using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the client's automation arguments.</summary>
public class ClientArgsTests
{
    [Fact]
    public void NoArgumentsMeansTheConnectScreen()
    {
        var args = ClientArgs.Parse(Array.Empty<string>());
        Assert.Empty(args.Errors);
        Assert.False(args.HasConnect);
        Assert.False(args.FakeServer);
        Assert.False(args.SkipConnectScreen);
        Assert.False(args.Automated);
        Assert.Equal(ClientArgs.DefaultName, args.Name);
    }

    [Fact]
    public void EveryDocumentedOptionParses()
    {
        var args = ClientArgs.Parse(new[]
        {
            "--connect=10.0.0.5:7001", "--name=Alice", "--autowalk", "--quit-after=12.5",
            "--screenshot-after=3,/tmp/a.png", "--screenshot-after=6.5,/tmp/b.png", "--fake-server",
            "--camera=orbit", "--yaw=-30", "--pitch=15.5", "--names", "--seed=42", "--fake-bots=150",
        });
        Assert.Empty(args.Errors);
        Assert.Equal("10.0.0.5", args.Host);
        Assert.Equal(7001, args.Port);
        Assert.Equal("Alice", args.Name);
        Assert.True(args.AutoWalk);
        Assert.Equal(12.5, args.QuitAfter);
        Assert.Equal(new[] { new ScreenshotRequest(3, "/tmp/a.png"), new ScreenshotRequest(6.5, "/tmp/b.png") }, args.Screenshots);
        Assert.True(args.FakeServer);
        Assert.Equal(CameraMode.Orbit, args.Camera);
        Assert.Equal(-30.0, args.YawDegrees);
        Assert.Equal(15.5, args.PitchDegrees);
        Assert.True(args.Names);
        Assert.Equal(42, args.Seed);
        Assert.Equal(150, args.FakeBots);
        Assert.True(args.Automated);
        Assert.True(args.SkipConnectScreen);
    }

    [Theory]
    [InlineData("example.org", "example.org", 7777)]
    [InlineData("127.0.0.1:8000", "127.0.0.1", 8000)]
    [InlineData("localhost:1", "localhost", 1)]
    [InlineData("[::1]:9000", "::1", 9000)]
    [InlineData("[::1]", "::1", 7777)]
    [InlineData("::1", "::1", 7777)]
    [InlineData(" host:65535 ", "host", 65535)]
    public void EndPointsParse(string text, string host, int port)
    {
        Assert.True(ClientArgs.TryParseEndPoint(text, out string h, out int p));
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":7777")]
    [InlineData("host:0")]
    [InlineData("host:65536")]
    [InlineData("host:abc")]
    [InlineData("host:-1")]
    [InlineData("[::1")]
    [InlineData("[::1]x")]
    [InlineData("[]:5")]
    public void BadEndPointsAreRefused(string text)
    {
        Assert.False(ClientArgs.TryParseEndPoint(text, out _, out _));
    }

    [Fact]
    public void BadValuesAreRecordedNotThrown()
    {
        var args = ClientArgs.Parse(new[]
        {
            "--connect=host:99999", "--quit-after=soon", "--quit-after=-1", "--screenshot-after=3", "--screenshot-after=x,/a.png",
            "--screenshot-after=2,relative.png", "--screenshot=rel.png", "--camera=top", "--yaw=north", "--seed=1.5",
            "--bogus", "positional", "--name=", "--name=" + new string('x', 43), "--fake-bots=1001", "--fake-bots=-3",
        });
        Assert.Equal(16, args.Errors.Count);
        Assert.False(args.HasConnect);
        Assert.Null(args.QuitAfter);
        Assert.Empty(args.Screenshots);
        Assert.Null(args.LegacyScreenshot);
        Assert.Null(args.Camera);
        Assert.Equal(ClientArgs.DefaultName, args.Name);
    }

    [Fact]
    public void TheEngineSchemesAreAcceptedAsPaths()
    {
        var args = ClientArgs.Parse(new[] { "--screenshot=user://x.png", "--screenshot-after=1,res://y.png" });
        Assert.Empty(args.Errors);
        Assert.Equal("user://x.png", args.LegacyScreenshot);
        Assert.Equal("res://y.png", args.Screenshots[0].Path);
    }

    [Fact]
    public void ThePathMayHoldCommasAndEquals()
    {
        var args = ClientArgs.Parse(new[] { "--screenshot-after=2,/tmp/a,b=c.png" });
        Assert.Empty(args.Errors);
        Assert.Equal("/tmp/a,b=c.png", args.Screenshots[0].Path);
    }

    [Theory]
    [InlineData("Alice", true)]
    [InlineData("Дмитрий", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\tb", false)]
    public void NamesAreCheckedLikeTheServerDoes(string name, bool valid)
    {
        Assert.Equal(valid, ClientArgs.IsValidName(name, out string problem));
        Assert.Equal(valid, problem == null);
    }

    [Fact]
    public void ANameOfTheLimitIsValidAndOneMoreIsNot()
    {
        Assert.True(ClientArgs.IsValidName(new string('n', Anomaly.Protocol.Limits.MaxNameChars), out _));
        Assert.False(ClientArgs.IsValidName(new string('n', Anomaly.Protocol.Limits.MaxNameChars + 1), out _));
    }
}
