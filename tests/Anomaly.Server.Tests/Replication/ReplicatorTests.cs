using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Replication;

/// <summary>The replicator's contract (<see cref="IReplicator"/>) and its pieces: call order, the outside-the-world rule, stamps, the type table, the sweep, the wire forms of the values.</summary>
public class ReplicatorTests
{
    [Fact]
    public void ItReadsOnlyFromOutsideTheWorldAndInTickOrder()
    {
        var world = ReplicationWorld.Boot(size: 10, movers: 2, seed: 51, mix: ScenarioMix.MoversOnly);
        var replicator = new Replicator();
        using (world.World.Enter())
            Assert.Throws<InvalidOperationException>(() => replicator.Attach(world.World));
        Assert.Throws<InvalidOperationException>(() => replicator.BuildDelta(world.World.Tick + 1)); // not attached
        replicator.Attach(world.World);
        Assert.Throws<InvalidOperationException>(() => replicator.Attach(world.World));
        world.Step();
        using (world.World.Enter())
            Assert.Throws<InvalidOperationException>(() => replicator.BuildDelta(world.World.Tick));
        Assert.Throws<ArgumentOutOfRangeException>(() => replicator.BuildDelta(world.World.Tick + 1));
        replicator.BuildDelta(world.World.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => replicator.BuildDelta(world.World.Tick)); // once per tick
        Assert.Equal(world.World.Tick, replicator.StateTick);
        replicator.Forget(1); // nothing kept per session in M1
    }

    [Fact]
    public void AMoveIsReplicatedInItsTickWithStampsAndStats()
    {
        var world = ReplicationWorld.Boot(size: 10, movers: 1, seed: 52, mix: ScenarioMix.MoversOnly);
        world.ScenarioOn = false;
        var run = new ReplicationRun(world);
        mob m = world.Movers[0];
        long attached = world.World.Tick;
        Assert.Equal(attached, run.Replicator.ChangedAt(m.dm_id, NetField.X));

        world.World.AddPhase("teleport", w =>
        {
            if (w.Tick == attached + 5) Waiting.NoSleep(m.forceMove(locate(5, 6, 1)));
        });
        for (int i = 0; i < 5; i++) run.Step();
        Assert.True(run.Replicator.TryGetEntity(m.dm_id, out EntityState state));
        Assert.Equal((5, 6, 1), (state.X, state.Y, state.Z));
        Assert.Equal(attached + 5, state.LastMoveTick);
        Assert.Equal(attached + 5, run.Replicator.ChangedAt(m.dm_id, NetField.LastMoveTick));
        Assert.True(run.Replicator.LastDetail.Hinted >= 1);
        Assert.True(run.Replicator.LastStats.Candidates >= 1);
        Assert.Equal(run.Replicator.EntityCount, run.Replicator.LastStats.Entities);
        Assert.True(run.Client.TryGetView(m.dm_id, out EntityView view));
        Assert.Equal((5, 6, 1, attached + 5), (view.X, view.Y, view.Z, view.LastMoveTick));
        Assert.Equal(-1, run.Replicator.ChangedAt(-5, NetField.X));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.ChangedAt(m.dm_id, NetField.X | NetField.Y));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.ChangedAt(m.dm_id, NetField.Id));
    }

    [Fact]
    public void TheSweepFindsAnUnhintedChangeWithinItsPeriod()
    {
        var world = ReplicationWorld.Boot(size: 12, movers: 6, seed: 53, mix: ScenarioMix.MoversOnly);
        world.ScenarioOn = false;
        world.AddClutter(200);
        var run = new ReplicationRun(world, sweepDivisor: 64, minSweep: 0);
        Assert.InRange(run.Replicator.SweepPeriodTicks, 50, 64); // 210 entities, 4 per tick
        obj thing = null;
        world.World.Objects.ForEachLiveInIdOrder((id, o) => thing ??= o is obj ob && ob.name == "clutter 100" ? ob : null);
        thing.name = "renamed"; // a plain field: no hint
        int ticks = 0;
        while (!run.Client.Views().Any(e => e.Name == "renamed"))
        {
            run.Step();
            ticks++;
            Assert.True(ticks <= 64, "the sweep found the rename within its period");
        }
        Assert.Contains(run.Replicator.LastSweepCatches, c => c.Id == thing.dm_id && c.Fields == NetField.Name);
        Assert.Equal(1, run.Replicator.LastDetail.SweepCatches);
        Assert.Equal(NetField.Name, run.Replicator.LastDetail.SweepCaughtFields);
    }

    [Fact]
    public void ALightTurnedOnIsHintedAndGoesOutTheNextTickNotWhenTheSweepComesRound()
    {
        // the owner (2026-10-26): an APC's lights came on about two seconds late (the sweep's period)
        var world = ReplicationWorld.Boot(size: 12, movers: 6, seed: 57, mix: ScenarioMix.MoversOnly);
        world.ScenarioOn = false;
        world.AddClutter(200);
        var run = new ReplicationRun(world, sweepDivisor: 64, minSweep: 0);
        obj thing = null;
        world.World.Objects.ForEachLiveInIdOrder((id, o) => thing ??= o is obj ob && ob.name == "clutter 100" ? ob : null);
        run.Step();
        long before = run.Replicator.ChangedAt(thing.dm_id, NetField.Light);
        using (world.World.Enter())
            Anomaly.Core.Kernel.Waiting.NoSleep(thing.set_light(4, 1, "#ffffff", l_on: Dm.TRUE));
        run.Step();
        Assert.NotEqual(before, run.Replicator.ChangedAt(thing.dm_id, NetField.Light));
        Assert.DoesNotContain(run.Replicator.LastSweepCatches, c => c.Id == thing.dm_id);
    }

    [Fact]
    public void ALookChangedDeepInANullspaceVisContentsGoesOutWithItsHolderTheNextTick()
    {
        // BYOND redraws every vis_locs holder of a changed atom; the holder's picture flattens its vis_contents (tg's greenscreen feed:
        // a copy of a mob in a nullspace display, shown by every status display, status_display.dm:968-1006)
        var world = ReplicationWorld.Boot(size: 12, movers: 6, seed: 58, mix: ScenarioMix.MoversOnly);
        world.ScenarioOn = false;
        world.AddClutter(200);
        var run = new ReplicationRun(world, sweepDivisor: 64, minSweep: 0);
        obj thing = null;
        world.World.Objects.ForEachLiveInIdOrder((id, o) => thing ??= o is obj ob && ob.name == "clutter 100" ? ob : null);
        obj copy;
        using (world.World.Enter())
        {
            obj feed = Dm.@new<obj>();
            copy = Dm.@new<obj>();
            copy.icon = Dm.resource("icons/obj/machines/status_display.dmi");
            copy.icon_state = "before";
            Dm.append(feed.vis_contents, copy);
            Dm.append(thing.vis_contents, feed);
        }
        run.Step();
        Assert.Contains("before", States(run.Client, thing.dm_id));

        using (world.World.Enter())
            copy.icon_state = "after";
        run.Step();
        Assert.Contains("after", States(run.Client, thing.dm_id));
        Assert.DoesNotContain(run.Replicator.LastSweepCatches, c => c.Id == thing.dm_id);
    }

    // Every icon state in an entity's picture, its overlays and theirs.
    private static List<string> States(ReplicaWorld client, long id)
    {
        var states = new List<string>();
        void Walk(TableRef reference, int depth)
        {
            if (depth > 8 || !client.TryGetAppearance(reference, out AppearanceEntry e)) return;
            if (client.ResolveString(e.IconState) is string s) states.Add(s);
            foreach (TableRef o in e.Overlays) Walk(o, depth + 1);
        }
        Walk(client.Entities.Single(e => e.Id == id).State.Appearance, 0);
        return states;
    }

    [Fact]
    public void NewTypesAreAppendedWhenFirstReplicated()
    {
        var world = ReplicationWorld.Boot(size: 10, movers: 1, seed: 54, mix: ScenarioMix.MoversOnly);
        world.ScenarioOn = false;
        var run = new ReplicationRun(world);
        int before = run.Replicator.Types.Count;
        Assert.Contains("/mob", run.Replicator.Types);
        Assert.DoesNotContain("/obj/item", run.Replicator.Types);
        world.World.AddPhase("spawn", w =>
        {
            if (w.Tick == run.Replicator.StateTick + 1) Dm.@new<obj_item>(locate(3, 3, 1));
        });
        DeltaFrame frame = run.Step();
        Assert.Equal(1, frame.Spawned);
        Delta delta = Wire.DecodeDelta(frame);
        Assert.Equal(before, delta.FirstNewType);
        Assert.Equal(new[] { "/obj/item" }, delta.NewTypes);
        Assert.Equal(before, delta.Spawned[0].Type);
        Assert.Equal(new[] { "/obj/item" }, run.Replicator.Types.Skip(before));
    }

    [Fact]
    public void WireFormsOfTheValues()
    {
        foreach (float dir in new float[] { 1, 2, 4, 5, 6, 8, 9, 10 })
            Assert.Equal((byte)dir, ReplicatedFields.WireDir(dir));
        foreach (float dir in new float[] { 0, 3, 7, 12, 16, 32, 2.5f, -1, float.NaN })
            Assert.Equal(0, ReplicatedFields.WireDir(dir));
        Assert.Equal(0f, ReplicatedFields.WireGlide(float.NaN));
        Assert.Equal(Limits.MaxGlideSize, ReplicatedFields.WireGlide(float.PositiveInfinity));
        Assert.Equal(0f, ReplicatedFields.WireGlide(float.NegativeInfinity));
        Assert.Equal(0f, ReplicatedFields.WireGlide(-3f));
        Assert.True(float.IsPositive(ReplicatedFields.WireGlide(-0f)));
        Assert.Equal(8f, ReplicatedFields.WireGlide(8f));
        Assert.Equal(Limits.MaxGlideSize, ReplicatedFields.WireGlide(1000f));
        Assert.Null(ReplicatedFields.WireName(null));
        string shortName = "Urist McTest";
        Assert.Same(shortName, ReplicatedFields.WireName(shortName));
        string longName = new string('a', 600);
        Assert.Equal(Limits.MaxEntityNameBytes, ReplicatedFields.WireName(longName).Length);
        string emoji = string.Concat(Enumerable.Repeat("\U0001F600", 200)); // 4 bytes each: 800 bytes
        string cut = ReplicatedFields.WireName(emoji);
        Assert.Equal(128 * 2, cut.Length); // 128 whole pairs, never half of one
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(cut) <= Limits.MaxEntityNameBytes);
    }

    [Fact]
    public void OptionsAreChecked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Replicator(new ReplicatorOptions { SweepDivisor = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Replicator(new ReplicatorOptions { MinSweepPerTick = -1 }));
        Assert.Throws<ArgumentException>(() => new Replicator(new ReplicatorOptions { Encoder = null }));
        Assert.Same(CodecReplicationEncoder.Instance, new ReplicatorOptions().Encoder);
    }
}
