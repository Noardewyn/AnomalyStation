using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The client replica: applying the join sequence and the deltas, the sample ring and the interpolation (scope 2g).</summary>
public class ReplicaWorldTests
{
    private static ReplicaWorld Joined(ScriptedServer server, int history = ReplicaWorld.DefaultHistory)
    {
        var world = new ReplicaWorld(history);
        Assert.True(world.TryApply(Codec.Encode(server.Map()), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.MapGrid, id);
        Assert.True(world.TryApply(Codec.Encode(server.Snapshot()), out id, out error), error.ToString());
        Assert.Equal(MessageId.Snapshot, id);
        return world;
    }

    private static void Feed(ReplicaWorld world, Delta delta)
    {
        byte[] body = Codec.EncodeDeltaBody(delta);
        Assert.True(world.TryApply(ScriptedServer.EncodeForConnection(delta, body), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Delta, id);
    }

    // A sample of a non-capsule entity: its tile centre as the position.
    private static EntitySample SampleOf(long tick, EntityState e) =>
        new(tick, e.X, e.Y, e.Z, e.Dir, e.GlideSize, e.LastMoveTick) { PosX = e.X * 256, PosY = e.Y * 256 };

    private static void AssertSame(ScriptedServer server, ReplicaWorld world)
    {
        Assert.Equal(server.Tick, world.Tick);
        Assert.Equal(server.OwnEntity, world.OwnEntity);
        Assert.Equal(server.LastInputSeq, world.LastInputSeq);
        Assert.Equal(server.Types, world.Types);
        Assert.Equal(server.Table.Count, world.Count);
        using IEnumerator<ReplicaEntity> replica = world.Entities.GetEnumerator();
        foreach (EntityState state in server.Table.Values)
        {
            Assert.True(replica.MoveNext());
            ReplicaEntity entity = replica.Current;
            Assert.Equal(state, entity.State);
            Assert.Equal(state.Id, entity.Id);
            Assert.Equal(server.Types[state.Type], entity.TypePath);
            Assert.Equal(state.Id == server.OwnEntity, entity.IsOwn);
            Assert.True(world.TryGet(state.Id, out ReplicaEntity same));
            Assert.Same(entity, same);
        }

        Assert.False(replica.MoveNext());
    }

    // ---- the join sequence and the deltas reproduce the server ----

    [Fact]
    public void SnapshotAndDeltasReproduceAScriptedServerTableTickByTick()
    {
        var server = new ScriptedServer(1001);
        server.Populate(60);
        ReplicaWorld early = Joined(server);
        AssertSame(server, early);
        ReplicaWorld late = null;
        ReplicaWorld resynced = null;
        int spawned = 0, despawned = 0;
        early.Spawned += _ => spawned++;
        early.Despawned += _ => despawned++;
        int startCount = early.Count;
        for (int i = 0; i < 2000; i++)
        {
            Delta delta = server.Step();
            Feed(early, delta);
            if (late != null) Feed(late, delta);
            if (resynced != null) Feed(resynced, delta);
            AssertSame(server, early);
            if (late != null) AssertSame(server, late);
            if (resynced != null) AssertSame(server, resynced);
            if (i == 500) late = Joined(server);
            if (i == 1000)
            {
                // A resynchronization: a Snapshot of the current tick replaces the table of a replica that has been running.
                resynced = Joined(server);
                Assert.True(early.TryApply(Codec.Encode(server.Snapshot()), out _, out _));
                AssertSame(server, early);
            }
        }

        Assert.True(server.Table.Count > 20, "the script should keep a population");
        Assert.True(server.Types.Count > 2, "the script should add types");
        Assert.True(spawned > 100 && despawned > 100, spawned + " spawns and " + despawned + " despawns");
        Assert.Equal(early.Count - startCount, spawned - despawned);
    }

    [Fact]
    public void TheSampleRingFollowsTheSamplingRule()
    {
        // A sample at every tick in which position, direction, glide or last move changed; none for a rename alone.
        var server = new ScriptedServer(77);
        server.Populate(40);
        const int history = 6;
        ReplicaWorld world = Joined(server, history);
        var expected = new Dictionary<long, List<EntitySample>>();
        foreach (EntityState e in server.Table.Values)
            expected[e.Id] = new List<EntitySample> { SampleOf(server.Tick, e) };
        for (int i = 0; i < 400; i++)
        {
            Delta delta = server.Step();
            Feed(world, delta);
            foreach (EntityState s in delta.Spawned)
                expected[s.Id] = new List<EntitySample> { SampleOf(delta.Tick, s) };
            foreach (EntityChange c in delta.Changed)
                if ((c.Mask & ~NetField.Name) != 0)
                {
                    EntityState now = server.Table[c.Id];
                    expected[c.Id].Add(SampleOf(delta.Tick, now));
                }

            foreach (ReplicaEntity entity in world.Entities)
            {
                List<EntitySample> list = expected[entity.Id];
                Assert.Equal(Math.Min(history, list.Count), entity.SampleCount);
                for (int age = 0; age < entity.SampleCount; age++)
                    Assert.Equal(list[list.Count - 1 - age], entity.Sample(age));
            }
        }

        ReplicaEntity any = world.Entities.First();
        Assert.Throws<ArgumentOutOfRangeException>(() => any.Sample(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => any.Sample(any.SampleCount));
    }

    [Fact]
    public void ADespawnAndARespawnOfTheSameIdIsANewEntityWithAFreshHistory()
    {
        var server = new ScriptedServer(5);
        server.Populate(3);
        ReplicaWorld world = Joined(server);
        var seen = new List<string>();
        world.Spawned += e => seen.Add("spawn " + e.Id + "@" + e.SpawnTick);
        world.Despawned += e => seen.Add("despawn " + e.Id);
        world.TryGet(2, out ReplicaEntity before);
        EntityState old = server.Table[2];
        long t = server.Tick;

        Feed(world, Msg.Delta(t + 1, despawned: new long[] { 2 }));
        Assert.False(world.TryGet(2, out _));
        Assert.Equal(2, world.Count);
        Feed(world, Msg.Delta(t + 2));
        EntityState back = old with { X = 9, Y = 9, PosX = 9 * 256, PosY = 9 * 256, LastMoveTick = t + 3 };
        Feed(world, Msg.Delta(t + 3, spawned: new[] { back }));
        Assert.True(world.TryGet(2, out ReplicaEntity after));
        Assert.NotSame(before, after);
        Assert.Equal(back, after.State);
        Assert.Equal(1, after.SampleCount);
        Assert.Equal(t + 3, after.SpawnTick);
        Assert.Equal(new[] { "despawn 2", "spawn 2@" + (t + 3) }, seen);
        // The old object keeps what it knew (a drawn copy may still fade out from it).
        Assert.Equal(old, before.State);
    }

    [Fact]
    public void AnEntityOfATypeThatIsNewInADelta()
    {
        var server = new ScriptedServer(5);
        server.Populate(2);
        ReplicaWorld world = Joined(server);
        long t = server.Tick;
        Assert.Equal(2, world.Types.Count);
        Feed(world, Msg.Delta(t + 1, firstNewType: 2, newTypes: new[] { "/obj/structure/table" }, spawned: new[] { Msg.State(50, 2, 3, 3, 1, 2, "table") }));
        Assert.Equal(3, world.Types.Count);
        Assert.True(world.TryGet(50, out ReplicaEntity table));
        Assert.Equal("/obj/structure/table", table.TypePath);
        // A second entity of the same type later, without a new table entry.
        Feed(world, Msg.Delta(t + 2, spawned: new[] { Msg.State(51, 2, 4, 3, 1, 2, "table 2") }));
        Assert.Equal("/obj/structure/table", world.Entities.Last().TypePath);
        Assert.Equal(3, world.Types.Count);
    }

    [Fact]
    public void FirstNewTypeIsOnlyCheckedWhenTheDeltaAddsTypes()
    {
        var server = new ScriptedServer(5);
        server.Populate(2);
        ReplicaWorld world = Joined(server);
        world.Apply(Msg.Delta(server.Tick + 1, firstNewType: 99));
        Assert.Equal(2, world.Types.Count);
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Delta(server.Tick + 2, firstNewType: 99, newTypes: new[] { "/x" })));
    }

    [Fact]
    public void TheOwnEntityFollowsTheHeaders()
    {
        var server = new ScriptedServer(5);
        server.Populate(3);
        ReplicaWorld world = Joined(server);
        long t = server.Tick;
        Assert.Equal(1, world.OwnEntity);
        Assert.True(world.TryGet(1, out ReplicaEntity first));
        Assert.True(first.IsOwn);
        Feed(world, Msg.Delta(t + 1, own: 3, seq: 7));
        Assert.Equal(3, world.OwnEntity);
        Assert.Equal(7u, world.LastInputSeq);
        Assert.False(first.IsOwn);
        Assert.True(world.TryGet(3, out ReplicaEntity third));
        Assert.True(third.IsOwn);
        Feed(world, Msg.Delta(t + 2, own: 0, seq: 7));
        Assert.False(third.IsOwn);
    }

    // ---- a desync is reported, never papered over ----

    [Fact]
    public void ADeltaBeforeAnySnapshotIsADesync()
    {
        var world = new ReplicaWorld();
        ProtocolException e = Assert.Throws<ProtocolException>(() => world.Apply(Msg.Delta(1)));
        Assert.Equal(ProtocolError.BadValue, e.Error);
        Assert.True(world.IsDesynced);
        Assert.False(world.HasSnapshot);
        Assert.Equal(-1, world.Tick);
    }

    [Theory]
    [InlineData(2)] // a gap
    [InlineData(0)] // a repeat
    [InlineData(-3)] // out of order
    public void ADeltaThatDoesNotFollowTheLastTickIsReportedAndThenRefusedUntilASnapshot(int offset)
    {
        var server = new ScriptedServer(9);
        server.Populate(5);
        ReplicaWorld world = Joined(server);
        long t = server.Tick;
        Feed(world, Msg.Delta(t + 1));
        ProtocolException e = Assert.Throws<ProtocolException>(() => world.Apply(Msg.Delta(t + 1 + offset)));
        Assert.Equal(ProtocolError.BadValue, e.Error);
        Assert.True(world.IsDesynced);
        // Even the right next tick is refused now: the table cannot be trusted.
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Delta(t + 2)));
        // A Snapshot clears it.
        server.Step();
        Assert.True(world.TryApply(Codec.Encode(server.Snapshot()), out _, out _));
        Assert.False(world.IsDesynced);
        world.Apply(Msg.Delta(server.Tick + 1, own: server.OwnEntity));
    }

    [Fact]
    public void InconsistentDeltasAreDesyncs()
    {
        EntityState one = Msg.State(1, 0, 3, 3);
        Delta[] bad =
        {
            Msg.Delta(11, spawned: new[] { Msg.State(1) }), // spawn of an existing id
            Msg.Delta(11, changed: new[] { Msg.Move(99, 2, 2, 11) }), // change of a missing id
            Msg.Delta(11, despawned: new long[] { 99 }), // despawn of a missing id
            Msg.Delta(11, spawned: new[] { Msg.State(5, type: 1) }), // a type outside the table
            Msg.Delta(11, firstNewType: 3, newTypes: new[] { "/x" }), // new types that skip a table entry
            Msg.Delta(11, firstNewType: 0, newTypes: new[] { "/x" }), // new types that repeat an index
            Msg.Delta(11, spawned: new[] { Msg.State(5), Msg.State(5) }), // the same id twice (also refused by the codec)
            Msg.Delta(11, spawned: new[] { Msg.State(5, x: 41) }), // outside the 40 x 40 map
            Msg.Delta(11, changed: new[] { Msg.Move(1, 3, 41, 11) }),
            Msg.Delta(11, changed: new[] { new EntityChange { Id = 1, Mask = NetField.Z, Z = 3 } }),
            Msg.Delta(11, despawned: new long[] { 1, 1 }),
        };
        foreach (Delta delta in bad)
        {
            var world = new ReplicaWorld();
            world.Apply(Msg.Map(40, 40, 2, new[] { "/turf" }, new TurfRun(3200, 0)));
            world.Apply(Msg.Snapshot(10, 0, 0, new[] { "/mob" }, one));
            ProtocolException e = Assert.Throws<ProtocolException>(() => world.Apply(delta));
            Assert.Equal(ProtocolError.BadValue, e.Error);
            Assert.True(world.IsDesynced);
        }
    }

    [Fact]
    public void ASnapshotReplacesTheTableAndKeepsWhatItShares()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.Snapshot(10, 1, 0, new[] { "/mob", "/obj" }, Msg.State(1, 0, 3, 3), Msg.State(2, 1, 4, 4), Msg.State(3, 0, 5, 5)));
        world.TryGet(1, out ReplicaEntity kept);
        world.TryGet(2, out ReplicaEntity retyped);
        var log = new List<string>();
        world.Spawned += e => log.Add("+" + e.Id);
        world.Despawned += e => log.Add("-" + e.Id);
        // 1 stays (moved), 2 changes type (so it is a different object), 3 is gone, 4 is new.
        world.Apply(Msg.Snapshot(15, 4, 3, new[] { "/obj", "/mob" }, Msg.State(1, 1, 6, 6, lastMove: 14), Msg.State(2, 1, 4, 4), Msg.State(4, 1, 1, 1)));
        Assert.Equal(new[] { "-2", "-3", "+2", "+4" }.OrderBy(s => s), log.OrderBy(s => s));
        Assert.Equal(3, world.Count);
        Assert.Equal(15, world.Tick);
        Assert.Equal(4, world.OwnEntity);
        Assert.Equal(3u, world.LastInputSeq);
        Assert.Equal(new[] { "/obj", "/mob" }, world.Types);
        world.TryGet(1, out ReplicaEntity again);
        Assert.Same(kept, again);
        Assert.Equal(Msg.State(1, 1, 6, 6, 1, 2, "", 0f, 14), again.State);
        Assert.Equal("/mob", again.TypePath);
        Assert.Equal(1, again.SampleCount); // the history restarts at a resync
        world.TryGet(2, out ReplicaEntity newTwo);
        Assert.NotSame(retyped, newTwo);
        Assert.Equal(new long[] { 1, 2, 4 }, world.Entities.Select(e => e.Id).ToArray());
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Snapshot(12, 0, 0, new[] { "/mob" }))); // an older tick
        world.Apply(Msg.Snapshot(15, 0, 0, new[] { "/mob" })); // the same tick is a legal resync
    }

    [Fact]
    public void ASnapshotWithBadEntitiesIsRefusedWholeAndLeavesTheTable()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.Map(40, 40, 2, new[] { "/turf" }, new TurfRun(3200, 0)));
        world.Apply(Msg.Snapshot(10, 0, 0, new[] { "/mob" }, Msg.State(1)));
        foreach (Snapshot bad in new[]
                 {
                     Msg.Snapshot(11, 0, 0, new[] { "/mob" }, Msg.State(2), Msg.State(2)),
                     Msg.Snapshot(11, 0, 0, new[] { "/mob" }, Msg.State(3), Msg.State(2)),
                     Msg.Snapshot(11, 0, 0, new[] { "/mob" }, Msg.State(2, type: 1)),
                     Msg.Snapshot(11, 0, 0, new[] { "/mob" }, Msg.State(2, x: 41)),
                 })
        {
            Assert.Throws<ProtocolException>(() => world.Apply(bad));
            Assert.Equal(10, world.Tick);
            Assert.Equal(1, world.Count);
        }
    }

    [Fact]
    public void TryApplyRoutesByIdAndReportsWhatItCannotUse()
    {
        var server = new ScriptedServer(3);
        server.Populate(4);
        var world = new ReplicaWorld();
        Assert.False(world.TryApply(Codec.Encode(new Welcome(1, default, 1, 20, 1)), out MessageId id, out ProtocolError error));
        Assert.Equal(MessageId.Welcome, id);
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(world.TryApply(Codec.Encode(new ServerText("a", "b")), out id, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(world.TryApply(ReadOnlySpan<byte>.Empty, out id, out error));
        Assert.Equal(MessageId.None, id);
        Assert.Equal(ProtocolError.Truncated, error);
        Assert.False(world.TryApply(new byte[] { 200, 1, 2 }, out id, out error));
        Assert.Equal(ProtocolError.UnknownMessage, error);
        byte[] snapshot = Codec.Encode(server.Snapshot());
        Assert.False(world.TryApply(snapshot.AsSpan(0, snapshot.Length - 1), out id, out error));
        Assert.Equal(MessageId.Snapshot, id);
        Assert.NotEqual(ProtocolError.None, error);
        Assert.False(world.HasSnapshot);
        Assert.True(world.TryApply(Codec.Encode(server.Map()), out id, out error));
        Assert.NotNull(world.Map);
        Assert.Equal(ScriptedServer.Width, world.Map.Width);
        Assert.True(world.TryApply(snapshot, out id, out error));
        Assert.True(world.HasSnapshot);
        Assert.Equal(4, world.Count);
    }

    [Fact]
    public void AMapMustBeConsistent()
    {
        var world = new ReplicaWorld();
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Map(2, 2, 1, new[] { "/t" }, new TurfRun(3, 0))));
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Map(256, 2, 1, new[] { "/t" }, new TurfRun(512, 0))));
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Map(2, 2, 1, new[] { "/t" }, new TurfRun(4, 1))));
        Assert.Null(world.Map);
        world.Apply(Msg.FlatMap(4, 4));
        Assert.NotNull(world.Map);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplicaWorld(1));
    }

    // ---- samples and interpolation ----

    /// <summary>A world with entity 1 at (5,5), spawned at tick 100; <paramref name="steps"/> are (tick, x, y, glide, dir) deltas.</summary>
    private static ReplicaWorld Walker(int history, params (long tick, int x, int y, float glide, byte dir)[] steps)
    {
        var world = new ReplicaWorld(history);
        world.Apply(Msg.FlatMap(100, 100));
        world.Apply(Msg.Snapshot(100, 1, 0, new[] { "/mob" }, Msg.State(1, 0, 5, 5, 1, 2, "w", 10.666667f, 90)));
        long tick = 100;
        foreach (var (t, x, y, glide, dir) in steps)
        {
            while (tick + 1 < t)
                world.Apply(Msg.Delta(++tick, own: 1));
            var change = Msg.Move(1, x, y, t, dir, glide);
            world.Apply(Msg.Delta(++tick, own: 1, changed: new[] { change }));
        }

        return world;
    }

    private static EntityPose PoseAt(ReplicaWorld world, double tick)
    {
        Assert.True(world.TryGet(1, out ReplicaEntity e));
        Assert.True(e.TryInterpolate(tick, out EntityPose pose));
        return pose;
    }

    [Fact]
    public void TrySampleReturnsTheSamplesAroundAFractionalTick()
    {
        ReplicaWorld world = Walker(8, (110, 6, 5, 10.666667f, 4), (114, 7, 5, 10.666667f, 4));
        world.TryGet(1, out ReplicaEntity e);
        Assert.Equal(3, e.SampleCount);
        Assert.True(e.TrySample(112, out EntitySample from, out EntitySample to, out float t));
        Assert.Equal(110, from.Tick);
        Assert.Equal(114, to.Tick);
        Assert.Equal(0.5f, t);
        Assert.True(e.TrySample(110, out from, out to, out t));
        Assert.Equal(110, from.Tick);
        Assert.Equal(0f, t);
        Assert.True(e.TrySample(100, out from, out to, out t));
        Assert.Equal(100, from.Tick);
        Assert.Equal(110, to.Tick);
        Assert.Equal(0f, t);
        Assert.True(e.TrySample(105, out from, out to, out t));
        Assert.Equal(100, from.Tick);
        Assert.Equal(0.5f, t);
        // Before the oldest: both the oldest. After the newest: both the newest.
        Assert.True(e.TrySample(50, out from, out to, out t));
        Assert.Equal(100, from.Tick);
        Assert.Equal(100, to.Tick);
        Assert.Equal(0f, t);
        Assert.True(e.TrySample(999, out from, out to, out t));
        Assert.Equal(114, from.Tick);
        Assert.Equal(114, to.Tick);
        Assert.Equal(0f, t);
    }

    [Fact]
    public void AStationaryEntityStaysWhereItIsAtAnyRenderTick()
    {
        ReplicaWorld world = Walker(8);
        foreach (double t in new[] { -5.0, 0.0, 99.0, 100.0, 100.5, 250.0 })
            Assert.Equal(new EntityPose(5f, 5f, 1, 2), PoseAt(world, t));
    }

    [Fact]
    public void AStepGlidesOverTheGlideSizeFromTheTickOfTheStep()
    {
        // One tile at glide 32/3 = 3 ticks, east, from tick 110.
        ReplicaWorld world = Walker(8, (110, 6, 5, 32f / 3, 4));
        Assert.Equal(new EntityPose(5f, 5f, 1, 2), PoseAt(world, 109.9));
        EntityPose start = PoseAt(world, 110);
        Assert.Equal(5f, start.X);
        Assert.Equal(4, start.Dir);
        Assert.Equal(5.5f, PoseAt(world, 111.5).X, 4);
        Assert.Equal(6f, PoseAt(world, 113).X, 4);
        Assert.Equal(new EntityPose(6f, 5f, 1, 4), PoseAt(world, 500));
        // A glide of 32 is one tick.
        ReplicaWorld fast = Walker(8, (110, 6, 5, 32f, 4));
        Assert.Equal(5.25f, PoseAt(fast, 110.25).X, 4);
        Assert.Equal(6f, PoseAt(fast, 111).X, 4);
    }

    [Fact]
    public void AWalkOfOneTilePerThreeTicksIsContinuous()
    {
        ReplicaWorld world = Walker(16, (110, 6, 5, 32f / 3, 4), (113, 7, 5, 32f / 3, 4), (116, 8, 5, 32f / 3, 4), (119, 9, 5, 32f / 3, 4));
        float previous = 5f;
        for (double t = 110; t <= 122; t += 0.25)
        {
            float x = PoseAt(world, t).X;
            Assert.True(x >= previous - 1e-5f, "x went back at " + t);
            Assert.True(x - previous <= 0.1f, "x jumped at " + t + ": " + previous + " to " + x);
            previous = x;
            // The speed is one tile per 3 ticks, through the whole walk.
            if (t >= 110 && t <= 122)
                Assert.Equal(Math.Min(9.0, 5.0 + (t - 110) / 3.0), x, 3);
        }
    }

    [Fact]
    public void AStepThatArrivesMidGlideStartsFromWhereTheEntityIsDrawn()
    {
        // Glide of 3 ticks per tile: the second step arrives one tick into the first.
        ReplicaWorld world = Walker(8, (110, 6, 5, 32f / 3, 4), (111, 7, 5, 32f / 3, 4));
        // At tick 111 the entity is drawn a third of the way to x = 6; from there it glides to 7 over 3 ticks.
        Assert.Equal(5f + 1f / 3, PoseAt(world, 111).X, 4);
        double expected = (5f + 1f / 3) + (7 - (5f + 1f / 3)) * 0.5;
        Assert.Equal(expected, PoseAt(world, 112.5).X, 4);
        Assert.Equal(7f, PoseAt(world, 114).X, 4);
    }

    [Fact]
    public void ARunOfOneTilePerTickWithFullGlideIsContinuous()
    {
        ReplicaWorld world = Walker(16, (110, 6, 5, 32f, 4), (111, 7, 5, 32f, 4), (112, 8, 5, 32f, 4), (113, 9, 5, 32f, 4));
        for (double t = 110; t <= 114; t += 0.125)
            Assert.Equal(5.0 + Math.Min(t - 110, 4), PoseAt(world, t).X, 3);
    }

    [Fact]
    public void ADiagonalStepGlidesBothAxesTogether()
    {
        ReplicaWorld world = Walker(8, (110, 6, 6, 16f, 5));
        EntityPose half = PoseAt(world, 111);
        Assert.Equal(5.5f, half.X, 4);
        Assert.Equal(5.5f, half.Y, 4);
        Assert.Equal(5, half.Dir);
    }

    [Fact]
    public void AGlideSizeOfZeroGlidesOverTheIntervalSinceThePreviousMove()
    {
        // The first move has no previous one: the default of 3 ticks. The second, 4 ticks after the first, takes 4.
        ReplicaWorld world = Walker(8, (110, 6, 5, 0f, 4), (114, 7, 5, 0f, 4));
        Assert.Equal(5.5f, PoseAt(world, 111.5).X, 4);
        Assert.Equal(6f, PoseAt(world, 113).X, 4);
        Assert.Equal(6.5f, PoseAt(world, 116).X, 4);
        Assert.Equal(7f, PoseAt(world, 118).X, 4);
    }

    [Fact]
    public void AGlideIsAtLeastOneTickAndAtMostTen()
    {
        Assert.Equal(1f, EntityGlide.GlideTicks(255f, 0));
        Assert.Equal(1f, EntityGlide.GlideTicks(32f, 0));
        Assert.Equal(10f, EntityGlide.GlideTicks(1f, 0));
        Assert.Equal(10f, EntityGlide.GlideTicks(0f, 500));
        Assert.Equal(3f, EntityGlide.GlideTicks(0f, 0));
        Assert.Equal(3f, EntityGlide.GlideTicks(0f, -1));
        Assert.Equal(7f, EntityGlide.GlideTicks(0f, 7));
        Assert.Equal(4f, EntityGlide.GlideTicks(8f, 99));
        ReplicaWorld world = Walker(8, (110, 6, 5, 1f, 4));
        Assert.Equal(5.5f, PoseAt(world, 115).X, 4);
        Assert.Equal(6f, PoseAt(world, 120).X, 4);
    }

    [Fact]
    public void ATeleportAndALevelChangeDoNotGlide()
    {
        ReplicaWorld world = Walker(8, (110, 12, 5, 10.666667f, 4));
        Assert.Equal(5f, PoseAt(world, 109.99).X);
        Assert.Equal(12f, PoseAt(world, 110).X);
        Assert.Equal(12f, PoseAt(world, 110.5).X);

        var up = new ReplicaWorld(8);
        up.Apply(Msg.FlatMap(100, 100, 2));
        up.Apply(Msg.Snapshot(100, 1, 0, new[] { "/mob" }, Msg.State(1, 0, 5, 5, 1, 2)));
        up.Apply(Msg.Delta(101, own: 1, changed: new[] { new EntityChange { Id = 1, Mask = NetField.Z | NetField.LastMoveTick, Z = 2, LastMoveTick = 101 } }));
        EntityPose pose = PoseAt(up, 101);
        Assert.Equal(2, pose.Z);
        Assert.Equal(5f, pose.X);
        Assert.Equal(1, PoseAt(up, 100.9).Z);
    }

    [Fact]
    public void ADirectionChangeTakesEffectAtItsTickAndAGlideAloneMovesNothing()
    {
        var world = new ReplicaWorld(8);
        world.Apply(Msg.FlatMap(100, 100));
        world.Apply(Msg.Snapshot(100, 1, 0, new[] { "/mob" }, Msg.State(1, 0, 5, 5, 1, 2, "w", 0f, 90)));
        world.Apply(Msg.Delta(101, own: 1, changed: new[] { new EntityChange { Id = 1, Mask = NetField.Dir, Dir = 4 } }));
        world.Apply(Msg.Delta(102, own: 1, changed: new[] { new EntityChange { Id = 1, Mask = NetField.GlideSize, GlideSize = 8f } }));
        Assert.Equal(2, PoseAt(world, 100.99).Dir);
        Assert.Equal(new EntityPose(5f, 5f, 1, 4), PoseAt(world, 101));
        Assert.Equal(new EntityPose(5f, 5f, 1, 4), PoseAt(world, 150));
    }

    [Fact]
    public void InterpolationStaysRightAfterTheRingWraps()
    {
        // 40 steps with a history of 4 samples: only the newest stretch is kept, and the newest glides are still exact.
        var steps = new List<(long, int, int, float, byte)>();
        for (int i = 1; i <= 40; i++)
            steps.Add((100 + 3L * i, 5 + i, 5, 32f / 3, (byte)4));
        ReplicaWorld world = Walker(4, steps.ToArray());
        world.TryGet(1, out ReplicaEntity e);
        Assert.Equal(4, e.SampleCount);
        Assert.Equal(220, e.Sample(0).Tick);
        Assert.Equal(45f, PoseAt(world, 221.5 + 3).X, 4);
        Assert.Equal(44.5f, PoseAt(world, 218.5 + 3).X, 4);
        Assert.Equal(45f, PoseAt(world, 1000).X);
    }

    [Fact]
    public void TheInterpolationOfEveryEntityOfAScriptedServerStaysOnItsTiles()
    {
        // Whatever the script does, the drawn position is between the samples' tiles and never outside the map.
        var server = new ScriptedServer(404);
        server.Populate(50);
        ReplicaWorld world = Joined(server);
        for (int i = 0; i < 300; i++)
        {
            Feed(world, server.Step());
            foreach (ReplicaEntity e in world.Entities)
            {
                for (double t = server.Tick - 6; t <= server.Tick + 1; t += 0.5)
                {
                    Assert.True(e.TryInterpolate(t, out EntityPose pose));
                    Assert.InRange(pose.X, 1f, ScriptedServer.Width);
                    Assert.InRange(pose.Y, 1f, ScriptedServer.Height);
                    Assert.InRange(pose.Z, 1, ScriptedServer.Levels);
                }

                Assert.True(e.TryInterpolate(server.Tick + 100, out EntityPose settled));
                Assert.Equal(e.State.X, settled.X);
                Assert.Equal(e.State.Y, settled.Y);
                Assert.Equal(e.State.Z, settled.Z);
                Assert.Equal(e.State.Dir, settled.Dir);
            }
        }
    }
}
