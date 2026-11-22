using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// The server's own path: the replicator with the protocol's codec (its default encoder), every session's Delta written as
/// header + shared body with <c>Codec.EncodeDelta</c>, and P2's <see cref="ReplicaWorld"/> applying the bytes; the replica
/// equals the full-scan oracle every tick, for an early and a late joiner with their own headers (own entity and last input
/// seq differ per session, the body is the same bytes).
/// </summary>
public class CodecRoundTripTests
{
    [Fact]
    public void TheCodecAndReplicaWorldReproduceTheWorldEveryTick()
    {
        var world = ReplicationWorld.Boot(size: 20, movers: 30, seed: 61);
        var replicator = new Replicator(new ReplicatorOptions { SweepDivisor = 1, MinSweepPerTick = 0 });
        replicator.Attach(world.World);
        var oracle = new WorldOracle();
        SortedDictionary<long, EntityView> truth = oracle.Scan(world.World);

        long own = world.Movers[0].dm_id;
        var early = new ReplicaWorld();
        Join(replicator, early, world.World.Tick, own, lastInputSeq: 0);
        AssertEqual(early, truth, world.World.Tick);
        Assert.Equal(own, early.OwnEntity);

        ReplicaWorld late = null;
        long lateOwn = world.Movers[1].dm_id;
        var buffer = new byte[1024];
        for (int i = 1; i <= 1500; i++)
        {
            world.Step();
            long tick = world.World.Tick;
            DeltaFrame frame = replicator.BuildDelta(tick);
            Send(early, frame, own, lastInputSeq: (uint)i, ref buffer);
            if (late != null) Send(late, frame, lateOwn, lastInputSeq: 7, ref buffer);
            truth = oracle.Scan(world.World);
            AssertEqual(early, truth, tick);
            Assert.Equal((uint)i, early.LastInputSeq);
            if (late != null) AssertEqual(late, truth, tick);
            if (i == 700)
            {
                late = new ReplicaWorld();
                Join(replicator, late, tick, lateOwn, lastInputSeq: 7);
                AssertEqual(late, truth, tick);
                Assert.Equal(lateOwn, late.OwnEntity);
            }
        }
        Assert.Equal(early.Types, late.Types);
        Assert.Empty(world.Runtimes);
    }

    private static void Join(Replicator replicator, ReplicaWorld replica, long tick, long own, uint lastInputSeq)
    {
        JoinMessages join = replicator.BuildJoin(tick, own, lastInputSeq);
        Assert.True(replica.TryApply(join.MapGrid, out MessageId gridId, out ProtocolError gridError), gridError.ToString());
        Assert.Equal(MessageId.MapGrid, gridId);
        Assert.True(replica.TryApply(join.Snapshot, out MessageId snapshotId, out ProtocolError snapshotError), snapshotError.ToString());
        Assert.Equal(MessageId.Snapshot, snapshotId);
    }

    private static void Send(ReplicaWorld replica, DeltaFrame frame, long own, uint lastInputSeq, ref byte[] buffer)
    {
        int size = Codec.MaxDeltaHeaderBytes + frame.Body.Length;
        if (buffer.Length < size) buffer = new byte[size * 2];
        int written = Codec.EncodeDelta(frame.Tick, lastInputSeq, own, frame.Body.Span, buffer);
        Assert.True(replica.TryApply(buffer.AsSpan(0, written), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Delta, id);
    }

    private static void AssertEqual(ReplicaWorld replica, SortedDictionary<long, EntityView> truth, long tick)
    {
        Assert.Equal(tick, replica.Tick);
        var have = replica.Entities
            .Select(e => new EntityView(e.Id, e.TypePath, e.State.X, e.State.Y, e.State.Z, e.State.Dir, e.State.Name, e.State.GlideSize, e.State.LastMoveTick))
            .ToList();
        Assert.Equal(truth.Values.ToList(), have);
    }
}
