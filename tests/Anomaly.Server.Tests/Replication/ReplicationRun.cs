using System.Text;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// A replicator attached to a <see cref="ReplicationWorld"/>, as the host drives it (step, build the delta, send it), with
/// a client that joined at attach: P2's <see cref="ReplicaWorld"/> applying the bytes of the protocol's codec (MapGrid and
/// Snapshot, then every Delta as a per-session header over the shared body). The full-scan oracle scans every tick.
/// </summary>
internal sealed class ReplicationRun
{
    public ReplicationWorld World { get; }

    public Replicator Replicator { get; }

    public ReplicaWorld Client { get; } = new();

    public WorldOracle Oracle { get; } = new();

    public long DeltaBytes { get; private set; }

    public int Deltas { get; private set; }

    public ReplicationRun(ReplicationWorld world, int sweepDivisor = 64, int minSweep = 32)
    {
        World = world;
        Replicator = new Replicator(new ReplicatorOptions { SweepDivisor = sweepDivisor, MinSweepPerTick = minSweep });
        Replicator.Attach(world.World);
        Join(Client, ownEntity: 0, lastInputSeq: 0);
        Truth = Oracle.Scan(world.World); // the oracle starts observing moves at the same tick as the replicator
    }

    /// <summary>The last Delta, decoded.</summary>
    public Delta LastDelta { get; private set; }

    /// <summary>The oracle's scan of the last tick (it scans every tick, to observe every move).</summary>
    public SortedDictionary<long, EntityView> Truth { get; private set; }

    /// <summary>A client joins now (after this tick's delta): MapGrid and Snapshot from their bytes.</summary>
    public void Join(ReplicaWorld client, long ownEntity, uint lastInputSeq)
    {
        JoinMessages join = Replicator.BuildJoin(World.World.Tick, ownEntity, lastInputSeq);
        Wire.Apply(client, join.MapGrid);
        Wire.Apply(client, join.Snapshot);
    }

    /// <summary>One server tick: World.Step, then the delta, sent to the client and to every other replica given.</summary>
    public DeltaFrame Step(params ReplicaWorld[] others)
    {
        World.Step();
        DeltaFrame frame = Replicator.BuildDelta(World.World.Tick);
        Assert.Equal(World.World.Tick, frame.Tick);
        DeltaBytes += frame.Body.Length;
        Deltas++;
        byte[] message = Wire.DeltaMessage(frame);
        Assert.True(Codec.TryDecode(message, out Delta delta, out ProtocolError error), error.ToString());
        LastDelta = delta;
        Assert.Equal(frame.Spawned, delta.Spawned.Count);
        Assert.Equal(frame.Changed, delta.Changed.Count);
        Assert.Equal(frame.Despawned, delta.Despawned.Count);
        Wire.Apply(Client, message);
        foreach (ReplicaWorld other in others) Wire.Apply(other, message);
        Truth = Oracle.Scan(World.World);
        return frame;
    }

    /// <summary>The differences between a replica and the oracle's scan of this tick (empty when equal), as text.</summary>
    public static List<string> Differences(ReplicaWorld replica, SortedDictionary<long, EntityView> truth, Func<EntityView, EntityView> mask = null)
    {
        mask ??= static e => e;
        var differences = new List<string>();
        foreach (EntityView e in replica.Views())
        {
            // the truth's scan is the version 1 entity set: the visuals of version 10 (a human's eyelids since M10x) have their own tests
            if (replica.TryGet(e.Id, out ReplicaEntity seen) && seen.IsVisual) continue;
            if (!truth.TryGetValue(e.Id, out EntityView t)) differences.Add($"extra {e}");
            else if (mask(t) != mask(e)) differences.Add($"replica {e} / world {t}");
        }
        foreach (EntityView t in truth.Values)
            if (!replica.TryGet(t.Id, out _)) differences.Add($"missing {t}");
        return differences;
    }

    public static string Describe(long tick, List<string> differences)
    {
        var text = new StringBuilder($"tick {tick}: {differences.Count} differences");
        foreach (string line in differences.Take(10)) text.Append("\n  ").Append(line);
        return text.ToString();
    }
}
