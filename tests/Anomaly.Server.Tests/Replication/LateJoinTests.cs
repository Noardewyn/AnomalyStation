using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>A session that joins late gets a Snapshot equal to the world's state of its tick, then the same Deltas as everyone.</summary>
public class LateJoinTests
{
    [Fact]
    public void ALateJoinerGetsTheCurrentStateAndThenStaysInStep()
    {
        var world = ReplicationWorld.Boot(size: 20, movers: 25, seed: 21);
        var run = new ReplicationRun(world, sweepDivisor: 1, minSweep: 0);
        for (int i = 0; i < 600; i++) run.Step();

        long ownEntity = world.Movers[3].dm_id;
        var late = new ReplicaWorld();
        run.Join(late, ownEntity, lastInputSeq: 41);
        Assert.Equal(world.World.Tick, late.Tick);
        Assert.Equal(ownEntity, late.OwnEntity);
        Assert.Equal(41u, late.LastInputSeq);
        Assert.Equal(run.Client.Types, late.Types); // the whole table so far, a prefix of what comes
        List<string> differences = ReplicationRun.Differences(late, run.Truth);
        Assert.True(differences.Count == 0, ReplicationRun.Describe(world.World.Tick, differences));
        Assert.Equal(run.Client.Views(), late.Views());

        // The map of the join is the room: rim walls, floor inside.
        Assert.Equal(20, late.Map.Width);
        Assert.Equal(20, late.Map.Height);
        Assert.Equal(1, late.Map.Levels);
        Assert.Equal(new[] { "/turf/closed/indestructible", "/turf/open/indestructible" }, late.Map.TurfTypes.OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(new TurfRun(21, 0), late.Map.Runs[0]); // the bottom row and the first cell of the second row are wall

        for (int i = 0; i < 600; i++)
        {
            run.Step(late);
            differences = ReplicationRun.Differences(late, run.Truth);
            Assert.True(differences.Count == 0, ReplicationRun.Describe(world.World.Tick, differences));
        }
        Assert.Equal(run.Client.Views(), late.Views());
        Assert.Equal(run.Client.Types, late.Types);
        Assert.Empty(world.Runtimes);
    }

    [Fact]
    public void AJoinIsBuiltForTheTickOfTheLastDelta()
    {
        var world = ReplicationWorld.Boot(size: 12, movers: 4, seed: 22);
        var replicator = new Replicator();
        replicator.Attach(world.World);
        long attached = world.World.Tick;
        Assert.Equal(attached, Wire.DecodeSnapshot(replicator.BuildJoin(attached, 0, 0).Snapshot).Tick); // right after Attach
        world.Step();
        Assert.Throws<ArgumentOutOfRangeException>(() => replicator.BuildJoin(world.World.Tick, 0, 0)); // before this tick's delta
        replicator.BuildDelta(world.World.Tick);
        Snapshot snapshot = Wire.DecodeSnapshot(replicator.BuildJoin(world.World.Tick, 0, 0).Snapshot);
        Assert.Equal(world.World.Tick, snapshot.Tick);
        Assert.Equal(4 + 4, snapshot.Entities.Count); // the movers and the scenario's boxes
    }
}
