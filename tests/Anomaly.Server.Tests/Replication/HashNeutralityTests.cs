using Anomaly.Core.Tg;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// Replication only reads, outside the world, and keeps its state outside it (<see cref="IReplicator"/>): a world's state
/// hash is the same with and without a replicator attached, building a Delta every tick and joins along the way.
/// </summary>
public class HashNeutralityTests
{
    [Fact]
    public void TheStateHashIsTheSameWithAndWithoutTheReplicator()
    {
        ulong[] plain = Run(replicate: false);
        ulong[] replicated = Run(replicate: true);
        Assert.Equal(plain, replicated);
    }

    [Fact]
    public void TheToyScenarioHashIsTheSameWithAReplicatorAttachedBeforeTheBoot()
    {
        // The M0 toy scenario steps the world without ever building a delta: the hints of a whole run pile up as ids.
        ToyScenario plain = ToyScenario.Run();
        ToyScenario attached = ToyScenario.Run(prepare: world => new Replicator().Attach(world));
        Assert.Equal(plain.StateHash, attached.StateHash);
        Assert.Equal(plain.Log.Select(e => e.ToString()), attached.Log.Select(e => e.ToString()));
    }

    private static ulong[] Run(bool replicate)
    {
        var world = ReplicationWorld.Boot(size: 16, movers: 20, seed: 41);
        Replicator replicator = null;
        if (replicate)
        {
            replicator = new Replicator();
            replicator.Attach(world.World);
        }
        var hashes = new List<ulong>();
        for (int i = 1; i <= 600; i++)
        {
            world.Step();
            if (replicator != null)
            {
                replicator.BuildDelta(world.World.Tick);
                if (i % 100 == 0) replicator.BuildJoin(world.World.Tick, world.Movers[0].dm_id, (uint)i);
            }
            if (i % 200 == 0) hashes.Add(world.World.ComputeStateHash());
        }
        Assert.Empty(world.Runtimes);
        return hashes.ToArray();
    }
}
