using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.ToyWorld;

/// <summary>
/// The M0 acceptance line "the same scenario run twice gives an identical state hash" (docs/ROADMAP.md, M0, Done when), on the
/// scripted scenario in worlds of its own: same seed, same log, same frames, same hash; another seed still runs the same script
/// to the same ends, with another hash (the seed is part of the world state, and mobs draw their nutrition from the world RNG).
/// </summary>
public class ToyScenarioDeterminismTests
{
    private static string[] Text(ToyScenario s) => s.Log.Select(e => e.ToString()).ToArray();

    private static string[] Pictures(ToyScenario s) => s.Frames.Select(f => $"{f.Tick} {f.Caption} {f.MobDir}\n{string.Join("\n", f.Rows)}").ToArray();

    [Fact]
    public void TwoRunsOfOneSeedGiveTheSameStateHashTheSameLogAndTheSameFrames()
    {
        var a = ToyScenario.Run(3);
        var b = ToyScenario.Run(3);

        Assert.NotSame(a.World, b.World);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(Text(a), Text(b));
        Assert.Equal(Pictures(a), Pictures(b));
        Assert.Equal(a.StartTick, b.StartTick);
        Assert.Empty(a.Runtimes);
        Assert.Empty(b.Runtimes);
    }

    [Fact]
    public void TheHashOfTheFinishedRunIsTheWorldsHashAtItsLastTick()
    {
        var s = ToyScenario.Run(3);

        Assert.Equal(s.World.ComputeStateHash(), s.StateHash);
        Assert.Equal(s.StartTick + ToyScenario.EndAt, s.World.Tick);
    }

    [Fact]
    public void TheDefaultSeedIsTheOneTheRunnerUses()
    {
        Assert.Equal(ToyScenario.DefaultSeed, ToyScenario.Run().Seed);
        Assert.Equal(ToyScenario.Run(ToyScenario.DefaultSeed).StateHash, ToyScenario.Run().StateHash);
    }

    [Fact]
    public void AnotherSeedStillRunsTheScriptToTheSameEndsWithAnotherHash()
    {
        var a = ToyScenario.Run(1);
        var b = ToyScenario.Run(2);

        Assert.Equal(2UL, b.Seed);
        Assert.Empty(b.Runtimes);
        // the script does not depend on the seed: the same lines on the same ticks, the same results
        Assert.Equal(Text(a), Text(b));
        Assert.Equal(a.CompletedDoAfter.EndTick, b.CompletedDoAfter.EndTick);
        Assert.True(b.CompletedDoAfter.Succeeded);
        Assert.False(b.CancelledDoAfter.Succeeded);
        Assert.Equal(a.CancelledDoAfter.EndTick, b.CancelledDoAfter.EndTick);
        // but the world is another one
        Assert.NotEqual(a.StateHash, b.StateHash);
    }

    [Fact]
    public void ARunDoesNotDisturbTheNextOneInTheSameProcess()
    {
        // no static state: a run between two runs of one seed changes nothing
        ulong first = ToyScenario.Run(5).StateHash;
        ToyScenario.Run(6);
        ulong again = ToyScenario.Run(5).StateHash;

        Assert.Equal(first, again);
    }
}
