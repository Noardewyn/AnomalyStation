using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// Protocol version 22 (the ghosts, D151): the observer layer. An entity above SEE_INVISIBLE_LIVING (a ghost's
/// INVISIBILITY_OBSERVER, 60) never travels in the shared Delta body; each session gets it in its own private block while its
/// mob's see_invisible reaches it (BYOND's rule), as spawns, changes and despawns of that session's view; a late joiner who sees
/// it gets it in the Snapshot's block; an entity whose invisibility crosses 25 moves between the body and the blocks.
/// </summary>
public class ReplicatorV22Tests
{
    private static mob NewMob(V2Run run, int x, int y, float seeInvisible)
        => run.In(() =>
        {
            var m = Dm.@new<mob>(run.Turf(x, y));
            m.see_invisible = seeInvisible;
            return m;
        });

    [Fact]
    public void TheObserverLayerReachesOnlyTheSessionsWhoseMobSeesIt()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        mob seerMob = NewMob(run, 3, 3, SEE_INVISIBLE_OBSERVER);
        SessionView alive = run.Connect("Alive", NewMob(run, 2, 2, SEE_INVISIBLE_LIVING));
        SessionView seer = run.Connect("Seer", seerMob);
        obj ghost = run.In(() =>
        {
            var o = Dm.@new<obj>(run.Turf(5, 5));
            o.invisibility = INVISIBILITY_OBSERVER;
            return o;
        });
        run.Step();
        long id = ghost.dm_id;
        Assert.False(alive.Replica.TryGet(id, out _));
        Assert.True(seer.Replica.TryGet(id, out _));
        Assert.DoesNotContain(Wire.DecodeDelta(run.LastFrame).Spawned, e => e.Id == id); // never in the shared body

        // a move reaches the seer as a change of its own view
        run.In(() => Waiting.NoSleep(ghost.forceMove(run.Turf(6, 5))));
        run.Step();
        Assert.True(seer.Replica.TryGet(id, out ReplicaEntity seen) && seen.State.X == 6);
        Assert.False(alive.Replica.TryGet(id, out _));

        // the seer's sight drops to a living mob's: it leaves the seer's view; back up, it comes back
        run.In(() => seerMob.see_invisible = SEE_INVISIBLE_LIVING);
        run.Step();
        Assert.False(seer.Replica.TryGet(id, out _));
        run.In(() => seerMob.see_invisible = SEE_INVISIBLE_OBSERVER);
        run.Step();
        Assert.True(seer.Replica.TryGet(id, out _));

        // made visible to all: it leaves the layer this tick and enters the shared body the next
        run.In(() => ghost.invisibility = 0);
        run.Step();
        run.Step();
        Assert.True(alive.Replica.TryGet(id, out _));
        Assert.True(seer.Replica.TryGet(id, out _));
        run.In(() => ghost.invisibility = INVISIBILITY_OBSERVER);
        run.Step();
        run.Step();
        Assert.False(alive.Replica.TryGet(id, out _));
        Assert.True(seer.Replica.TryGet(id, out _));

        // a late joiner that sees the layer has it from its Snapshot's private block; a living one does not
        SessionView lateSeer = run.Connect("LateSeer", NewMob(run, 4, 4, SEE_INVISIBLE_OBSERVER));
        SessionView lateAlive = run.Connect("LateAlive", NewMob(run, 4, 5, SEE_INVISIBLE_LIVING));
        Assert.True(lateSeer.Replica.TryGet(id, out _));
        Assert.False(lateAlive.Replica.TryGet(id, out _));

        // deleted: despawned from every view that had it
        run.In(() => qdel(ghost));
        run.Step();
        Assert.False(seer.Replica.TryGet(id, out _));
        Assert.False(lateSeer.Replica.TryGet(id, out _));
        Assert.Equal(0, run.Replicator.LayerEntityCount);
    }

    [Fact]
    public void AnInvisiminMobStaysInItsOwnClientButLeavesTheLivingOnes()
    {
        // DV576: the dev invisimin lifts a living mob to INVISIBILITY_OBSERVER above its own see_invisible; its client keeps it
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        mob invisible = NewMob(run, 3, 3, SEE_INVISIBLE_LIVING);
        SessionView own = run.Connect("Own", invisible);
        SessionView alive = run.Connect("Alive", NewMob(run, 2, 2, SEE_INVISIBLE_LIVING));
        SessionView seer = run.Connect("Seer", NewMob(run, 4, 4, SEE_INVISIBLE_OBSERVER));
        run.In(() => invisible.invisibility = INVISIBILITY_OBSERVER);
        run.Step();
        run.Step();
        long id = invisible.dm_id;
        Assert.True(own.Replica.TryGet(id, out _));
        Assert.True(seer.Replica.TryGet(id, out _));
        Assert.False(alive.Replica.TryGet(id, out _));
    }

    [Fact]
    public void AGhostsMotionReachesOnlyTheSessionsThatSeeIt()
    {
        // Version 42 (D40): a capsule mob of the observer layer moves; its Motion entries go to the seer, never to the living.
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView alive = run.Connect("Alive", NewMob(run, 2, 2, SEE_INVISIBLE_LIVING));
        SessionView seer = run.Connect("Seer", NewMob(run, 3, 3, SEE_INVISIBLE_OBSERVER));
        mob ghost = NewMob(run, 5, 5, SEE_INVISIBLE_OBSERVER);
        run.In(() => ghost.invisibility = INVISIBILITY_OBSERVER);
        run.Locomotion.Capsules[ghost.dm_id] = MotionState.AtTile(5, 5, 1, 0);
        run.Step();
        int seen = 0;
        for (int i = 1; i <= 10; i++)
        {
            run.Locomotion.Capsules[ghost.dm_id] = MotionState.AtTile(5, 5, 1, 0) with { X = 5 * 256 + i * 10 };
            run.Step();
            Assert.False(alive.Replica.TryGet(ghost.dm_id, out _));
            Assert.True(seer.Replica.TryGet(ghost.dm_id, out ReplicaEntity e));
            Assert.Equal(5 * 256 + i * 10, e.State.PosX);
            var motion = new List<MotionMessage>();
            run.Replicator.BuildMotion(alive.Ordinal, motion);
            Assert.DoesNotContain(motion.SelectMany(m => m.Entries), entry => entry.Id == ghost.dm_id);
            run.Replicator.BuildMotion(seer.Ordinal, motion);
            seen += motion.SelectMany(m => m.Entries).Count(entry => entry.Id == ghost.dm_id);
        }
        Assert.Equal(10, seen);
    }

    [Fact]
    public void AnOwnBlockWithLayerEntitiesRoundTripsThroughTheCodec()
    {
        var own = new OwnBlock { LayerTick = 40 };
        own.LayerSpawned.Add(new EntityState(7, 0, 3, 4, 1, 2, "ghost", 32f, 39));
        own.LayerChanged.Add(new EntityChange { Id = 9, Mask = NetField.X | NetField.LastMoveTick, X = 5, LastMoveTick = 40 });
        own.LayerDespawned.Add(11);
        var buffer = new byte[Codec.MaxOwnBlockSize(own)];
        int length = Codec.EncodeOwnBlock(own, buffer);
        var delta = new Delta { Tick = 40 };
        byte[] body = new byte[Codec.MaxDeltaBodySize(delta)];
        int bodyLength = Codec.EncodeDeltaBody(delta, body);
        var message = new byte[Codec.MaxDeltaHeaderBytes + bodyLength + length];
        int total = Codec.EncodeDelta(40, 0, 0, body.AsSpan(0, bodyLength), buffer.AsSpan(0, length), message);
        Assert.True(Codec.TryDecode(message.AsSpan(0, total), out Delta decoded, out ProtocolError error), error.ToString());
        Assert.Equal(40, decoded.Own.LayerTick);
        EntityState spawned = Assert.Single(decoded.Own.LayerSpawned);
        Assert.Equal((7L, 3, 4, "ghost", 39L), (spawned.Id, spawned.X, spawned.Y, spawned.Name, spawned.LastMoveTick));
        EntityChange changed = Assert.Single(decoded.Own.LayerChanged);
        Assert.Equal((9L, 5), (changed.Id, changed.X));
        Assert.Equal(new[] { 11L }, decoded.Own.LayerDespawned);
    }
}
