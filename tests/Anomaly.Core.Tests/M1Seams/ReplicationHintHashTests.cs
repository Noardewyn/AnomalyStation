using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// Hints only select candidates (presentation-seam.md rule 5): a world with a replication hint sink computes exactly what
/// the same world computes without one. The M0 toy scenario (ROADMAP M0 "Done when"; <c>tools/Anomaly.Tools.ToyWorld</c>) runs
/// twice for the same seed, once with a recording sink installed before the boot: the state hash, the log and the frames
/// are equal, while the sink heard the moves, appearance changes and deletions of the run.
/// </summary>
public class ReplicationHintHashTests
{
    [Fact]
    public void HintsChangeNeitherTheToyScenarioNorItsStateHash()
    {
        ToyScenario plain = ToyScenario.Run();
        var hints = new HintRecorder();
        ToyScenario hinted = ToyScenario.Run(prepare: world => PresentationSinks.Of(world).Replication = hints);

        Assert.Equal(plain.StateHash, hinted.StateHash);
        Assert.Equal(plain.World.Tick, hinted.World.Tick);
        Assert.Equal(plain.Log.Select(e => e.ToString()), hinted.Log.Select(e => e.ToString()));
        Assert.Equal(plain.Frames.SelectMany(f => f.Rows), hinted.Frames.SelectMany(f => f.Rows));
        Assert.Empty(plain.Runtimes);
        Assert.Empty(hinted.Runtimes);

        Assert.Contains(hints.Lines, line => line.StartsWith("moved mob#", StringComparison.Ordinal));
        Assert.Contains(hints.Lines, line => line.StartsWith("appearance mob#", StringComparison.Ordinal) && line.EndsWith(" Dir", StringComparison.Ordinal));
        Assert.Contains(hints.Lines, line => line.StartsWith("deleted ", StringComparison.Ordinal));
    }
}
