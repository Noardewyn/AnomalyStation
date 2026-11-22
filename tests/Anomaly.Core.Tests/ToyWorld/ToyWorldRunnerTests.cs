using System.Globalization;
using System.IO;
using Anomaly.Core.Tg;
using Anomaly.Tools.ToyWorld;

namespace Anomaly.Core.Tests.ToyWorld;

/// <summary>The toy world runner's command line (tools/Anomaly.Tools.ToyWorld), run in-process.</summary>
public class ToyWorldRunnerTests
{
    private static (int Code, string Output, string Errors) Run(params string[] args)
    {
        args = args.Where(a => a != null).ToArray();
        var output = new StringWriter();
        var errors = new StringWriter();
        int code = ToyWorldRunner.Run(args, output, errors);
        return (code, output.ToString(), errors.ToString());
    }

    private static string Hash(ulong hash) => "0x" + hash.ToString("x16", CultureInfo.InvariantCulture);

    // ---- --twice ----

    [Fact]
    public void TwiceRunsTheScenarioInTwoWorldsOfOneSeedAndReturnsZeroWhenTheHashesAreEqual()
    {
        var run = Run("--twice");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Errors);
        string[] hashes = run.Output.Split('\n').Where(l => l.Contains("state hash")).Select(l => l.Trim()).ToArray();
        Assert.Equal(2, hashes.Length);
        string expected = Hash(ToyScenario.Run(ToyScenario.DefaultSeed).StateHash);
        Assert.Equal("run 1: state hash " + expected, hashes[0]);
        Assert.Equal("run 2: state hash " + expected, hashes[1]);
        Assert.Contains("identical", run.Output);
        Assert.DoesNotContain("DIFFERENT", run.Output);
    }

    [Theory]
    [InlineData("--seed", "7")]
    [InlineData("--seed=7", null)]
    public void TwiceWithASeedUsesIt(string first, string second)
    {
        var run = Run(first, second, "--twice");

        Assert.Equal(0, run.Code);
        Assert.Contains("seed 7", run.Output);
        Assert.Contains(Hash(ToyScenario.Run(7).StateHash), run.Output);
        Assert.NotEqual(Run("--twice").Output, run.Output);
    }

    // ---- one run ----

    [Fact]
    public void ARunPrintsTheFramesTheLogTheDoAfterResultsAndTheFinalHashAndReturnsZero()
    {
        var run = Run();
        var scenario = ToyScenario.Run();

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Errors);
        // the frames: the walls, the floor, the obj, the mob, and the mob's dir as an arrow under each
        Assert.Contains("#######", run.Output);
        Assert.Contains("#@....#", run.Output);
        Assert.Contains("#..o..#", run.Output);
        Assert.Contains("@ ↓ south", run.Output);
        Assert.Contains("@ → east", run.Output);
        // the log, line by line
        foreach (ToyLogEntry entry in scenario.Log)
            Assert.Contains("  " + entry, run.Output);
        // the results and the hash
        Assert.Contains($"do_after #1 (completes): TRUE on t{scenario.CompletedDoAfter.EndTick}, 20 ticks after it started", run.Output);
        Assert.Contains($"do_after #2 (cancelled by a step): FALSE on t{scenario.CancelledDoAfter.EndTick}, 6 ticks after it started", run.Output);
        Assert.Contains($"state hash {Hash(scenario.StateHash)} at t{scenario.World.Tick}", run.Output);
        Assert.DoesNotContain("runtime", run.Output);
    }

    [Fact]
    public void TheOutputStaysShort()
    {
        string[] lines = Run().Output.Split('\n');

        Assert.True(lines.Length < 70, $"the runner printed {lines.Length} lines");
    }

    [Fact]
    public void TheSameCommandLineAlwaysPrintsTheSameText()
    {
        Assert.Equal(Run("--seed", "4").Output, Run("--seed", "4").Output);
        Assert.NotEqual(Run("--seed", "4").Output, Run("--seed", "5").Output);   // only the hash line differs
    }

    // ---- the command line ----

    [Fact]
    public void HelpPrintsTheUsageAndReturnsZero()
    {
        var run = Run("--help");

        Assert.Equal(0, run.Code);
        Assert.Contains("--twice", run.Output);
        Assert.Contains("--seed", run.Output);
    }

    [Theory]
    [InlineData("--seed", null)]
    [InlineData("--seed", "x")]
    [InlineData("--seed", "-1")]
    [InlineData("--nonsense", null)]
    public void ABadCommandLineIsRefusedWithTheUsageAndExitCodeTwo(string first, string second)
    {
        var run = Run(first, second);

        Assert.Equal(2, run.Code);
        Assert.StartsWith("error: ", run.Errors);
        Assert.Contains("Usage:", run.Errors);
        Assert.Equal("", run.Output);
    }
}
