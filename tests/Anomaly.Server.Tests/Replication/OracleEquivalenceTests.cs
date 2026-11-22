using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// The replica a client builds from MapGrid + Snapshot + every Delta equals a full scan of the world (the oracle) every
/// tick, over a booted tg world with random steps, spawns, qdels, teleports (<c>abstract_move</c>, <c>forceMove</c>),
/// turns, glide changes, moves into and out of containers and nullspace, abstract invisibility and renames.
/// </summary>
public class OracleEquivalenceTests
{
    [Fact]
    public void WithoutRenamesTheReplicaEqualsTheWorldEveryTick()
    {
        // Every change but a rename comes with an engine hint: no sweep is needed for exactness.
        var world = ReplicationWorld.Boot(size: 20, movers: 30, seed: 11, mix: new ScenarioMix().WithoutRenames());
        var run = new ReplicationRun(world, sweepDivisor: 64, minSweep: 0);
        int sweepCatches = AssertEqualEveryTick(run, ticks: 2500);
        Assert.Equal(0, sweepCatches); // the sweep found nothing the hints had missed
        Assert.Empty(world.Runtimes);
    }

    [Fact]
    public void WithAFullSweepEveryChangeIncludingRenamesIsExactEveryTick()
    {
        var world = ReplicationWorld.Boot(size: 20, movers: 30, seed: 12);
        var run = new ReplicationRun(world, sweepDivisor: 1, minSweep: 0);
        AssertEqualEveryTick(run, ticks: 2500);
        Assert.Empty(world.Runtimes);
        Assert.True(world.RenamedAt.Count > 50, "the scenario renamed things");
    }

    [Fact]
    public void WithTheDefaultSweepEveryFieldIncludingRenamesIsExactEveryTick()
    {
        // name is an engine property since M2 (replication.md 5.2): a rename comes with a hint and reaches the replica in its
        // own tick, so the default sweep (1/64 of the slots, at least 32) has nothing left to catch.
        var world = ReplicationWorld.Boot(size: 20, movers: 30, seed: 13);
        var run = new ReplicationRun(world);
        int sweepCatches = AssertEqualEveryTick(run, ticks: 3000);
        Assert.Equal(0, sweepCatches);
        Assert.True(world.RenamedAt.Count > 50, "the scenario renamed things");
        Assert.Empty(world.Runtimes);
    }

    // Returns the changes only the sweep found.
    private static int AssertEqualEveryTick(ReplicationRun run, int ticks)
    {
        List<string> start = ReplicationRun.Differences(run.Client, run.Truth);
        Assert.True(start.Count == 0, ReplicationRun.Describe(run.World.World.Tick, start));
        int spawned = 0, changed = 0, despawned = 0, sweepCatches = 0;
        for (int i = 0; i < ticks; i++)
        {
            DeltaFrame frame = run.Step();
            spawned += frame.Spawned;
            changed += frame.Changed;
            despawned += frame.Despawned;
            sweepCatches += run.Replicator.LastDetail.SweepCatches;
            List<string> differences = ReplicationRun.Differences(run.Client, run.Truth);
            Assert.True(differences.Count == 0, ReplicationRun.Describe(run.World.World.Tick, differences));
        }
        Assert.True(spawned > 100 && changed > 1000 && despawned > 50, $"the scenario exercised the replica: spawned {spawned}, changed {changed}, despawned {despawned}");
        return sweepCatches;
    }
}
