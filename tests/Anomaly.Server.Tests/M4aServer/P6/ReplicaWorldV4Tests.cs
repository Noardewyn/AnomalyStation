using Anomaly.Server.Tests.Protocol;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>The client replica of protocol version 4 (ReplicaWorld.V4.cs): the private images, the screen groups and the entity flags.</summary>
public class ReplicaWorldV4Tests
{
    private static readonly string[] Types = { "/mob", "/obj/item" };

    private static ReplicaWorld Joined(Action<Snapshot> shape = null)
    {
        var world = new ReplicaWorld();
        MapGrid grid = Msg.FlatMap(8, 8);
        grid.SolidBits = new byte[8];
        grid.AppearanceRuns.Add(new TurfRun(64, 1));
        world.Apply(grid);
        Snapshot s = Msg.Snapshot(10, 1, 0, Types, Msg.State(1, 0, 3, 3), Msg.State(2, 1, 4, 4));
        s.Resources.Add("icons/a.dmi");
        s.Strings.Add("one");
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), IconState = TableRef.Public(1) });
        s.Own.FirstNewPrivateAppearance = 1;
        s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
        s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), Alpha = 100 });
        shape?.Invoke(s);
        world.Apply(s);
        return world;
    }

    private static Delta Next(ReplicaWorld world) => Msg.Delta(world.Tick + 1);

    // ---- the private images ----

    [Fact]
    public void AnEmptyOverrideImageIsValidInASnapshot()
    {
        ReplicaWorld world = Joined(s => s.Own.ImagesAdded.Add(new ImageEntry(20, TableRef.None, 2) { Override = true }));
        Assert.True(world.Images[20].Override);
        Assert.True(world.Images[20].Appearance.IsNone);
    }

    [Fact]
    public void AnOverrideImageCanBecomeEmptyAndVisibleAgainDuringPlay()
    {
        ReplicaWorld world = Joined();
        Delta added = Next(world);
        added.Own.ImagesAdded.Add(new ImageEntry(20, TableRef.None, 2) { Override = true });
        world.Apply(added);
        Assert.True(world.Images[20].Appearance.IsNone);

        Delta shown = Next(world);
        shown.Own.ImagesChanged.Add(new ImageEntry(20, new TableRef(1, true), 2) { Override = true });
        world.Apply(shown);
        Assert.False(world.Images[20].Appearance.IsNone);

        Delta hidden = Next(world);
        hidden.Own.ImagesChanged.Add(new ImageEntry(20, TableRef.None, 2) { Override = true });
        world.Apply(hidden);
        Assert.True(world.Images[20].Appearance.IsNone);

        Delta removed = Next(world);
        removed.Own.ImagesRemoved.Add(20);
        world.Apply(removed);
        Assert.Empty(world.Images);
    }

    [Fact]
    public void ImagesFollowTheSnapshotAndTheDeltasAndRaiseTheirEvents()
    {
        ReplicaWorld world = Joined(s => s.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(1, true), 2)));
        Assert.Equal(new ImageEntry(1, new TableRef(1, true), 2), world.Images[1]);

        var changed = new List<ImageEntry>();
        var removed = new List<long>();
        world.ImageChanged += changed.Add;
        world.ImageRemoved += removed.Add;
        Delta d = Next(world);
        d.Own.ImagesChanged.Add(new ImageEntry(1, new TableRef(2, true), 2));
        d.Own.ImagesAdded.Add(new ImageEntry(2, TableRef.Public(1), 0));
        world.Apply(d);
        Assert.Equal(2, world.Images.Count);
        Assert.Equal(new TableRef(2, true), world.Images[1].Appearance);
        Assert.Equal(new[] { 2L, 1L }, changed.Select(i => i.Id).OrderByDescending(i => i)); // added and changed, once each
        Assert.Empty(removed);

        changed.Clear();
        Delta r = Next(world);
        r.Own.ImagesRemoved.Add(1);
        world.Apply(r);
        Assert.Equal(new long[] { 1 }, removed);
        Assert.Empty(changed);
        Assert.Equal(new long[] { 2 }, world.Images.Keys.ToArray());

        // A Snapshot replaces them: the missing one is removed, a new one is added.
        removed.Clear();
        Snapshot s = Msg.Snapshot(world.Tick + 1, 1, 0, Types, Msg.State(1, 0, 3, 3));
        s.Resources.Add("icons/a.dmi");
        s.Strings.Add("one");
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
        s.Own.ImagesAdded.Add(new ImageEntry(7, TableRef.Public(1), 0));
        world.Apply(s);
        Assert.Equal(new long[] { 7 }, world.Images.Keys.ToArray());
        Assert.Equal(new long[] { 2 }, removed);
        Assert.Equal(new long[] { 7 }, changed.Select(i => i.Id));
    }

    [Theory]
    [InlineData("image add twice")]
    [InlineData("image change missing")]
    [InlineData("image remove missing")]
    [InlineData("image appearance past the public table")]
    [InlineData("image appearance past the private table")]
    [InlineData("override image appearance past the private table")]
    [InlineData("image appearance none")]
    [InlineData("more than the cap")]
    [InlineData("snapshot changed")]
    [InlineData("snapshot removed")]
    [InlineData("snapshot past the table")]
    [InlineData("snapshot override past the table")]
    [InlineData("snapshot appearance none")]
    [InlineData("snapshot more than the cap")]
    public void ABadImageBlockIsADesync(string what)
    {
        ReplicaWorld world = Joined(s => s.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(1, true), 0)));
        if (what.StartsWith("snapshot"))
        {
            Snapshot bad = Msg.Snapshot(world.Tick + 1, 1, 0, Types);
            bad.Own.FirstNewPrivateAppearance = 1;
            bad.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
            bad.Resources.Add("icons/a.dmi");
            bad.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
            switch (what)
            {
                case "snapshot changed": bad.Own.ImagesChanged.Add(new ImageEntry(1, TableRef.Public(1), 0)); break;
                case "snapshot removed": bad.Own.ImagesRemoved.Add(1); break;
                case "snapshot past the table": bad.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(2, true), 0)); break;
                case "snapshot override past the table": bad.Own.ImagesAdded.Add(new ImageEntry(1, new TableRef(2, true), 0) { Override = true }); break;
                case "snapshot appearance none": bad.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.None, 0)); break;
                case "snapshot more than the cap":
                    for (int i = 1; i <= Limits.MaxPrivateImages + 1; i++) bad.Own.ImagesAdded.Add(new ImageEntry(i, TableRef.Public(1), 0));
                    break;
            }

            Assert.Throws<ProtocolException>(() => world.Apply(bad)); // a Snapshot is checked before it changes anything
            return;
        }

        Delta d = Next(world);
        switch (what)
        {
            case "image add twice": d.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0)); break;
            case "image change missing": d.Own.ImagesChanged.Add(new ImageEntry(5, TableRef.Public(1), 0)); break;
            case "image remove missing": d.Own.ImagesRemoved.Add(5); break;
            case "image appearance past the public table": d.Own.ImagesAdded.Add(new ImageEntry(5, TableRef.Public(2), 0)); break;
            case "image appearance past the private table": d.Own.ImagesAdded.Add(new ImageEntry(5, new TableRef(3, true), 0)); break;
            case "override image appearance past the private table": d.Own.ImagesAdded.Add(new ImageEntry(5, new TableRef(3, true), 0) { Override = true }); break;
            case "image appearance none": d.Own.ImagesAdded.Add(new ImageEntry(5, TableRef.None, 0)); break;
            case "more than the cap":
                for (int i = 2; i <= Limits.MaxPrivateImages + 1; i++) d.Own.ImagesAdded.Add(new ImageEntry(i, TableRef.Public(1), 0));
                break;
        }

        Assert.Throws<ProtocolException>(() => world.Apply(d));
        Assert.True(world.IsDesynced);
    }

    // ---- the screen groups ----

    [Fact]
    public void ScreenGroupListsTheEntriesOfOneGroupInIdOrder()
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Own.ScreenAdded.Add(new ScreenEntry(31, TableRef.Public(1), "CENTER-4,SOUTH+1") { Group = HudGroup.Storage });
            s.Own.ScreenAdded.Add(new ScreenEntry(40, TableRef.Public(1), "WEST,SOUTH") { Group = HudGroup.Static });
            s.Own.ScreenAdded.Add(new ScreenEntry(44, TableRef.Public(1), "CENTER-3,SOUTH+1") { Group = HudGroup.Storage });
            s.Own.ScreenAdded.Add(new ScreenEntry(50, TableRef.Public(1), "EAST,CENTER") { Group = HudGroup.None });
            s.Own.ScreenAdded.Add(new ScreenEntry(60, TableRef.Public(1), null) { Group = HudGroup.ToggleableInventory });
        });
        Assert.Equal(new long[] { 31, 44 }, world.ScreenGroup(HudGroup.Storage).Select(e => e.Id));
        Assert.Equal(new long[] { 40 }, world.ScreenGroup(HudGroup.Static).Select(e => e.Id));
        Assert.Equal(new long[] { 60 }, world.ScreenGroup(HudGroup.ToggleableInventory).Select(e => e.Id));
        Assert.Equal(new long[] { 50 }, world.ScreenGroup(HudGroup.None).Select(e => e.Id));
        Assert.Empty(world.ScreenGroup(HudGroup.Info));

        // A changed entry moves group; a removed one leaves.
        var seen = new List<ScreenEntry>();
        world.ScreenEntryChanged += seen.Add;
        Delta d = Next(world);
        d.Own.ScreenChanged.Add(new ScreenEntry(31, TableRef.Public(1), "CENTER-4,SOUTH+1") { Group = HudGroup.ToggleableInventory });
        d.Own.ScreenRemoved.Add(44);
        world.Apply(d);
        Assert.Equal(HudGroup.ToggleableInventory, seen.Single().Group);
        Assert.Empty(world.ScreenGroup(HudGroup.Storage));
        Assert.Equal(new long[] { 31, 60 }, world.ScreenGroup(HudGroup.ToggleableInventory).Select(e => e.Id));
    }

    [Fact]
    public void AScreenEntryInAnUnknownGroupIsADesync()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(4, 4));
        Snapshot s = Msg.Snapshot(1, 1, 0, Types);
        s.Own.ScreenAdded.Add(new ScreenEntry(1, TableRef.None, null) { Group = HudGroup.Last + 1 });
        Assert.Throws<ProtocolException>(() => world.Apply(s));
    }

    // ---- the entity flags ----

    [Fact]
    public void TheThrownFlagFollowsTheSnapshotAndTheDeltas()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(8, 8));
        world.Apply(Msg.Snapshot(5, 1, 0, Types, Msg.State(1, 0, 2, 2), Msg.State(2, 1, 4, 4, flagsOf: EntityFlags.Thrown)));
        world.TryGet(1, out ReplicaEntity mob);
        world.TryGet(2, out ReplicaEntity item);
        Assert.False(mob.IsThrown);
        Assert.True(item.IsThrown);
        Assert.Equal(EntityFlags.Thrown, item.Flags);

        Delta d = Msg.Delta(6, changed: new[]
        {
            new EntityChange { Id = 2, Mask = NetField.Flags | NetField.X | NetField.LastMoveTick, X = 5, LastMoveTick = 6, Flags = EntityFlags.None },
            new EntityChange { Id = 1, Mask = NetField.Flags, Flags = EntityFlags.Thrown },
        });
        world.Apply(d);
        Assert.False(item.IsThrown);
        Assert.Equal(5, item.State.X);
        Assert.True(mob.IsThrown);

        // A flags-only change is no movement: the interpolation history is not touched.
        Assert.Equal(1, mob.SampleCount);

        // The Snapshot sets the flags again.
        world.Apply(Msg.Snapshot(9, 1, 0, Types, Msg.State(1, 0, 2, 2), Msg.State(2, 1, 5, 4, flagsOf: EntityFlags.Thrown)));
        world.TryGet(1, out mob);
        world.TryGet(2, out item);
        Assert.False(mob.IsThrown);
        Assert.True(item.IsThrown);
    }

    [Fact]
    public void APushableEntityKeepsItsFlag()
    {
        // version 56: bit 3, the last undefined bit of the byte before it, is Pushable
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(8, 8));
        world.Apply(Msg.Snapshot(5, 1, 0, Types, Msg.State(1, 0, 2, 2, flagsOf: EntityFlags.Pushable)));
        Assert.True(world.TryGet(1, out ReplicaEntity crate));
        Assert.Equal(EntityFlags.Pushable, crate.State.Flags);
        Assert.False(crate.IsThrown);
    }
}
