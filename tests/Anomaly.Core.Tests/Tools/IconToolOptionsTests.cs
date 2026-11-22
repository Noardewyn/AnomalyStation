using Anomaly.Tools.Icons;

namespace Anomaly.Core.Tests.Tools;

/// <summary>The command line of tools/Anomaly.Tools.Icons (M2 contracts; the conversion is M2 P7's): <c>--tg PATH --out DIR</c>.</summary>
public class IconToolOptionsTests
{
    [Fact]
    public void BothFormsOfTheTwoOptionsParse()
    {
        Assert.True(IconToolOptions.TryParse(new[] { "--tg", "../tgstation", "--out", "godot/assets/tg" }, out IconToolOptions a, out _));
        Assert.Equal(new IconToolOptions("../tgstation", "godot/assets/tg"), a);
        Assert.True(IconToolOptions.TryParse(new[] { "--out=o", "--tg=t" }, out IconToolOptions b, out _));
        Assert.Equal(new IconToolOptions("t", "o"), b);
    }

    [Theory]
    [InlineData("--tg", "t")]
    [InlineData("--out", "o")]
    [InlineData("--tg", "t", "--out")]
    [InlineData("--tg", "t", "--out", "o", "--extra", "x")]
    public void AMissingOrUnknownOptionIsAnError(params string[] args)
    {
        Assert.False(IconToolOptions.TryParse(args, out _, out string error));
        Assert.NotNull(error);
    }

    [Fact]
    public void HelpIsNoError()
    {
        Assert.False(IconToolOptions.TryParse(new[] { "--help" }, out _, out string error));
        Assert.Null(error);
    }
}
