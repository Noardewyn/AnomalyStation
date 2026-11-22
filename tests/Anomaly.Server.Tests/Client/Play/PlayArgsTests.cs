using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the new automation arguments and the extended input script.</summary>
public class PlayArgsTests
{
    [Fact]
    public void TheSimulatedNetworkTakesTheValueAfterAnEqualsSignOrAsTheNextWord()
    {
        var args = ClientArgs.Parse(new[] { "--sim-latency=100", "--sim-loss", "2" });
        Assert.Empty(args.Errors);
        Assert.Equal(100.0, args.SimLatencyMs);
        Assert.Equal(0.02, args.SimLoss, 6);
        Assert.True(args.SimulatesNetwork);
        args = ClientArgs.Parse(new[] { "--sim-latency", "250.5" });
        Assert.Equal(250.5, args.SimLatencyMs);
        Assert.False(ClientArgs.Parse(Array.Empty<string>()).SimulatesNetwork);
    }

    [Theory]
    [InlineData("--sim-latency=-1")]
    [InlineData("--sim-latency=5000")]
    [InlineData("--sim-latency=fast")]
    [InlineData("--sim-loss=60")]
    [InlineData("--sim-loss=-2")]
    [InlineData("--sim-latency")]
    public void ABadSimulationIsReported(string arg)
    {
        var args = ClientArgs.Parse(new[] { arg });
        Assert.Single(args.Errors);
        Assert.Equal(0, args.SimLatencyMs);
        Assert.Equal(0, args.SimLoss);
    }

    [Fact]
    public void BindingsAndOptionsAreCollectedAndChecked()
    {
        var args = ClientArgs.Parse(new[] { "--bind=rest=Z", "--bind", "shove=MouseMiddle", "--option=head_bob=off", "--option=colorblind=mono" });
        Assert.Empty(args.Errors);
        Assert.Equal(new[] { ("rest", "Z"), ("shove", "MouseMiddle") }, args.Binds);
        Assert.Equal(new[] { ("head_bob", "off"), ("colorblind", "mono") }, args.Options);

        args = ClientArgs.Parse(new[] { "--bind=fly=Z", "--bind=rest", "--bind=rest=a b", "--option=fov=999", "--option=nothing=1", "--option" });
        Assert.Equal(6, args.Errors.Count);
        Assert.Empty(args.Binds);
        Assert.Empty(args.Options);
    }

    [Fact]
    public void DevCommandsAreScheduledAndMakeTheRunAutomated()
    {
        var args = ClientArgs.Parse(new[] { "--dev-after=2,damage brute 40 head", "--dev-after", "5,analyze" });
        Assert.Empty(args.Errors);
        Assert.Equal(new[] { new DevCommandRequest(2, "damage brute 40 head"), new DevCommandRequest(5, "analyze") }, args.DevCommands);
        Assert.True(args.Automated);
        Assert.Single(ClientArgs.Parse(new[] { "--dev-after=heal" }).Errors);
        Assert.Single(ClientArgs.Parse(new[] { "--dev-after=1," }).Errors);
    }

    [Fact]
    public void TheScriptPressesTheActionKeysAndClicks()
    {
        Assert.True(InputScript.TryParse("1:U+,1.1:U-,2:F+,3:c+,4:click=right,5:click=LEFT", out List<ScriptedInput> steps, out string error), error);
        Assert.Equal(6, steps.Count);
        Assert.Contains(new ScriptedInput(1, ScriptAction.Press, "U", 0), steps);
        Assert.Contains(new ScriptedInput(3, ScriptAction.Press, "C", 0), steps);
        Assert.Contains(new ScriptedInput(4, ScriptAction.Click, "RIGHT", 0), steps);
        Assert.Contains(new ScriptedInput(5, ScriptAction.Click, "LEFT", 0), steps);
        Assert.False(InputScript.TryParse("1:click=up", out _, out _));
        Assert.False(InputScript.TryParse("1:click", out _, out _));
    }
}
