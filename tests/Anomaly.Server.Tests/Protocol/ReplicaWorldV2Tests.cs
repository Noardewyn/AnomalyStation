using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The client replica of protocol version 2 (docs/ai/specs/protocol.md section 6): the tables, the cells, the capsules, the private block.</summary>
public class ReplicaWorldV2Tests
{
    private static readonly string[] Types = { "/mob", "/obj" };

    private static ReplicaWorld Joined(Action<Snapshot> shape = null, Action<MapGrid> map = null, long tick = 10)
    {
        var world = new ReplicaWorld();
        MapGrid grid = Msg.FlatMap(8, 8);
        grid.SolidBits = new byte[8];
        grid.AppearanceRuns.Add(new TurfRun(64, 1));
        map?.Invoke(grid);
        world.Apply(grid);
        Snapshot s = Msg.Snapshot(tick, 1, 0, Types, Msg.State(1, 0, 3, 3), Msg.State(2, 1, 4, 4));
        s.Resources.AddRange(new[] { "icons/a.dmi", "icons/b.dmi" });
        s.Strings.AddRange(new[] { "one", "two", "three" });
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), IconState = TableRef.Public(1) });
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(2), Overlays = { TableRef.Public(1) } });
        shape?.Invoke(s);
        world.Apply(s);
        return world;
    }

    private static Delta Next(ReplicaWorld world) => Msg.Delta(world.Tick + 1);

    [Fact]
    public void ASnapshotFillsTheTablesAndTheyResolve()
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Own.FirstNewPrivateString = 1;
            s.Own.NewPrivateStrings.Add("head3");
        });
        Assert.Equal(new[] { "icons/a.dmi", "icons/b.dmi" }, world.Resources);
        Assert.Equal(3, world.Strings.Count);
        Assert.Equal(2, world.Appearances.Count);
        Assert.Equal("icons/b.dmi", world.ResolveResource(TableRef.Public(2)));
        Assert.Equal("two", world.ResolveString(TableRef.Public(2)));
        Assert.Null(world.ResolveString(TableRef.None));
        Assert.Null(world.ResolveString(TableRef.Public(4)));
        Assert.Null(world.ResolveResource(new TableRef(1, true)));
        Assert.True(world.TryGetAppearance(TableRef.Public(2), out AppearanceEntry e));
        Assert.Equal(TableRef.Public(2), e.Icon);
        Assert.False(world.TryGetAppearance(TableRef.Public(3), out _));
        Assert.False(world.TryGetAppearance(TableRef.None, out _));
        Assert.False(world.TryGetAppearance(new TableRef(1, true), out _));
    }

    [Fact]
    public void ADeltaAppendsNewEntriesAtTheNextIndexAndEntitiesMayUseThem()
    {
        ReplicaWorld world = Joined();
        Delta d = Next(world);
        d.FirstNewResource = 3;
        d.NewResources.Add("icons/c.dmi");
        d.FirstNewString = 4;
        d.NewStrings.Add("four");
        d.FirstNewAppearance = 3;
        d.NewAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(3), IconState = TableRef.Public(4), Overlays = { TableRef.Public(2) } });
        d.Spawned.Add(Msg.State(5, 1, 5, 5, 1, 2, "new", 0f, world.Tick + 1) with { Appearance = TableRef.Public(3) });
        d.Changed.Add(new EntityChange { Id = 2, Mask = NetField.Appearance, Appearance = TableRef.Public(3) });
        world.Apply(d);
        Assert.Equal("icons/c.dmi", world.ResolveResource(TableRef.Public(3)));
        Assert.Equal("four", world.ResolveString(TableRef.Public(4)));
        Assert.True(world.TryGet(5, out ReplicaEntity spawned));
        Assert.Equal(TableRef.Public(3), spawned.State.Appearance);
        world.TryGet(2, out ReplicaEntity changed);
        Assert.Equal(TableRef.Public(3), changed.State.Appearance);
        Assert.Equal(TableRef.Public(3), changed.Sample(0).Appearance);
        Assert.Equal(TableRef.None, changed.Sample(1).Appearance);
    }

    [Theory]
    [InlineData("resource first")]
    [InlineData("string first")]
    [InlineData("appearance first")]
    [InlineData("entity appearance")]
    [InlineData("change appearance")]
    [InlineData("overlay forward")]
    [InlineData("icon past table")]
    [InlineData("turf appearance")]
    public void ABadReferenceOrIndexIsADesyncAndOnlyASnapshotClearsIt(string what)
    {
        ReplicaWorld world = Joined();
        Delta d = Next(world);
        switch (what)
        {
            case "resource first":
                d.FirstNewResource = 4;
                d.NewResources.Add("x");
                break;
            case "string first":
                d.FirstNewString = 2;
                d.NewStrings.Add("x");
                break;
            case "appearance first":
                d.FirstNewAppearance = 5;
                d.NewAppearances.Add(new AppearanceEntry());
                break;
            case "entity appearance":
                d.Spawned.Add(Msg.State(9, 0, 1, 1, 1, 2, "x", 0f, world.Tick + 1) with { Appearance = TableRef.Public(3) });
                break;
            case "change appearance":
                d.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Appearance, Appearance = TableRef.Public(9) });
                break;
            case "overlay forward":
                d.FirstNewAppearance = 3;
                d.NewAppearances.Add(new AppearanceEntry { Overlays = { TableRef.Public(4) } });
                d.NewAppearances.Add(new AppearanceEntry());
                break;
            case "icon past table":
                d.FirstNewAppearance = 3;
                d.NewAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(3) });
                break;
            case "turf appearance":
                d.TurfAppearanceChanged.Add(new CellAppearanceChange(1, 1, 1, TableRef.Public(5)));
                break;
        }

        ProtocolException e = Assert.Throws<ProtocolException>(() => world.Apply(d));
        Assert.Equal(ProtocolError.BadValue, e.Error);
        Assert.True(world.IsDesynced);
        Assert.Throws<ProtocolException>(() => world.Apply(Msg.Delta(world.Tick + 1)));
        // A Snapshot is the way back (the map's turf appearances need the table the Snapshot brings: a plain map first).
        world.Apply(Msg.FlatMap(8, 8));
        Snapshot fresh = Msg.Snapshot(world.Tick + 1, 1, 0, Types, Msg.State(1));
        world.Apply(fresh);
        Assert.False(world.IsDesynced);
        Assert.Empty(world.Appearances);
    }

    [Fact]
    public void ASnapshotWithAPublicEntryNamingAPrivateOneOrABadTableIsRefusedBeforeItChangesAnything()
    {
        ReplicaWorld world = Joined();
        Snapshot bad = Msg.Snapshot(20, 1, 0, Types, Msg.State(1) with { Appearance = TableRef.Public(2) });
        bad.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) }); // no resources in this Snapshot
        Assert.Throws<ProtocolException>(() => world.Apply(bad));
        Assert.Equal(10, world.Tick);
        Assert.Equal(2, world.Count);
        Assert.Equal(2, world.Resources.Count);
        Snapshot withOwn = Msg.Snapshot(20, 1, 0, Types);
        withOwn.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = new TableRef(3, true) });
        withOwn.Own.FirstNewPrivateAppearance = 1;
        Assert.Throws<ProtocolException>(() => world.Apply(withOwn));
        Assert.Equal(10, world.Tick);
    }

    // ---- the cells ----

    [Fact]
    public void TheMapCellsComeFromTheMapGridThenFromTheDeltasAndAreReportedOnce()
    {
        ReplicaWorld world = Joined(map: g => g.SolidBits[0] = 0b0000_0110);
        Assert.False(world.IsSolid(1, 1, 1));
        Assert.True(world.IsSolid(2, 1, 1));
        Assert.True(world.IsSolid(3, 1, 1));
        Assert.False(world.IsSolid(4, 1, 1));
        Assert.True(world.IsSolid(0, 1, 1) && world.IsSolid(9, 1, 1) && world.IsSolid(1, 9, 1) && world.IsSolid(1, 1, 2));
        Assert.Equal(TableRef.Public(1), world.TurfAppearance(4, 4, 1));
        Assert.Equal(TableRef.None, world.TurfAppearance(9, 4, 1));

        var reported = new List<(int X, int Y, int Z)>();
        int events = 0;
        world.CellsChanged += cells =>
        {
            events++;
            reported.AddRange(cells);
        };
        Delta d = Next(world);
        d.SolidChanged.Add(new CellSolidChange(2, 1, 1, false));
        d.SolidChanged.Add(new CellSolidChange(8, 8, 1, true));
        d.TurfAppearanceChanged.Add(new CellAppearanceChange(4, 4, 1, TableRef.Public(2)));
        world.Apply(d);
        Assert.False(world.IsSolid(2, 1, 1));
        Assert.True(world.IsSolid(8, 8, 1));
        Assert.Equal(TableRef.Public(2), world.TurfAppearance(4, 4, 1));
        Assert.Equal(1, events);
        Assert.Equal(new[] { (2, 1, 1), (8, 8, 1), (4, 4, 1) }, reported);
        world.Apply(Next(world));
        Assert.Equal(1, events); // a Delta that changed no cell raises nothing

        // The MapGrid object itself is not touched; a Delta outside the map is a desync.
        Delta outside = Next(world);
        outside.SolidChanged.Add(new CellSolidChange(9, 1, 1, true));
        Assert.Throws<ProtocolException>(() => world.Apply(outside));
    }

    [Fact]
    public void AMapWithoutV2PartsHasNothingSolidAndNoAppearances()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(4, 4));
        world.Apply(Msg.Snapshot(1, 0, 0, Types));
        Assert.False(world.IsSolid(2, 2, 1));
        Assert.True(world.IsSolid(5, 2, 1));
        Assert.Equal(TableRef.None, world.TurfAppearance(2, 2, 1));
        world.Apply(Msg.FlatMap(4, 4)); // a second map replaces the cells
        Assert.False(world.IsSolid(2, 2, 1));
    }

    // ---- capsules ----

    private static EntityState Capsule(long id, int x, int y, int posX, int posY, byte posture = 0, int z = 1) =>
        Msg.State(id, 0, x, y, z) with { HasPos = true, PosX = posX, PosY = posY, Yaw = 10, Posture = posture };

    [Fact]
    public void ACapsuleKeepsItsContinuousPositionAndANonCapsuleFollowsItsTile()
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Entities.Clear();
            s.Entities.Add(Capsule(1, 3, 3, 3 * 256 + 20, 3 * 256 - 30));
            s.Entities.Add(Msg.State(2, 1, 4, 4));
        });
        world.TryGet(1, out ReplicaEntity capsule);
        world.TryGet(2, out ReplicaEntity prop);
        Assert.True(capsule.HasPos);
        Assert.False(prop.HasPos);
        Assert.Equal(4 * 256, prop.State.PosX);

        Delta d = Next(world);
        d.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Pos | NetField.Yaw, PosX = 3 * 256 + 60, PosY = 3 * 256 - 30, Yaw = 99 });
        d.Changed.Add(new EntityChange { Id = 2, Mask = NetField.X | NetField.Y | NetField.LastMoveTick, X = 5, Y = 6, LastMoveTick = world.Tick + 1 });
        world.Apply(d);
        Assert.Equal(3 * 256 + 60, capsule.State.PosX);
        Assert.Equal(99, capsule.State.Yaw);
        Assert.Equal(3, capsule.State.X);
        Assert.Equal(5 * 256, prop.State.PosX);
        Assert.Equal(6 * 256, prop.State.PosY);
        Assert.False(prop.HasPos);

        // A prop that becomes a capsule (the bridge registered it) starts sending Pos.
        Delta becomes = Next(world);
        becomes.Changed.Add(new EntityChange { Id = 2, Mask = NetField.Pos | NetField.Yaw, PosX = 5 * 256 + 5, PosY = 6 * 256, Yaw = 1 });
        world.Apply(becomes);
        Assert.True(prop.HasPos);
        Assert.Equal(5 * 256 + 5, prop.Sample(0).PosX);

        // A position outside the map is a desync.
        Delta outside = Next(world);
        outside.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Pos, PosX = 20 * 256, PosY = 3 * 256 });
        Assert.Throws<ProtocolException>(() => world.Apply(outside));
    }

    [Fact]
    public void TheCollisionViewSeesStandingCapsulesAtTheirNewestPositionInIdOrder()
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Entities.Clear();
            s.Entities.Add(Capsule(1, 3, 3, 3 * 256, 3 * 256)); // the own mob, excluded by the caller
            s.Entities.Add(Capsule(2, 4, 3, 4 * 256 + 40, 3 * 256));
            s.Entities.Add(Capsule(3, 4, 4, 4 * 256, 4 * 256, posture: PostureBits.Encode(true, 90))); // lying: no collision (D51)
            s.Entities.Add(Capsule(4, 3, 4, 3 * 256 - 100, 4 * 256 + 20));
            s.Entities.Add(Capsule(5, 6, 6, 6 * 256, 6 * 256)); // far away
            s.Entities.Add(Msg.State(6, 1, 4, 3)); // not a capsule
        });
        ICollisionView view = world.CollisionView;
        Assert.Same(view, world.CollisionView);
        var found = new List<CapsuleObstacle>();
        view.GatherCapsules(3 * 256, 3 * 256, 1, 2 * 256, exclude: 1, found);
        Assert.Equal(new long[] { 2, 4 }, found.Select(c => c.Id).ToArray());
        Assert.Equal(4 * 256 + 40, found[0].X);
        Assert.Equal(MotionMath.DefaultRadius, found[0].Radius);

        // The newest received position, not an older sample.
        Delta d = Next(world);
        d.Changed.Add(new EntityChange { Id = 2, Mask = NetField.Pos, PosX = 4 * 256 + 90, PosY = 3 * 256 });
        d.Changed.Add(new EntityChange { Id = 3, Mask = NetField.Posture, Posture = 0 });
        world.Apply(d);
        found.Clear();
        view.GatherCapsules(3 * 256, 3 * 256, 1, 2 * 256, exclude: 1, found);
        Assert.Equal(new long[] { 2, 3, 4 }, found.Select(c => c.Id).ToArray());
        Assert.Equal(4 * 256 + 90, found[0].X);
        found.Clear();
        view.GatherCapsules(3 * 256, 3 * 256, 2, 2 * 256, exclude: 0, found); // another level
        Assert.Empty(found);

        Assert.Equal(TileEdges.None, view.EdgesOf(1, 1, 1));
        Assert.Equal(world.IsSolid(2, 2, 1), view.IsSolid(2, 2, 1));
        Assert.True(view.IsSolid(0, 0, 1));
    }

    // ---- the private block ----

    private static MotionState State(int x, int y) => MotionState.AtTile(x, y, 1);

    [Fact]
    public void TheOwnStateIsKeptAndRaisedAfterTheMessageIsApplied()
    {
        ReplicaWorld world = Joined();
        Assert.Equal(10, world.Own.Tick);
        Assert.False(world.Own.HasMotion);
        var seen = new List<string>();
        world.Despawned += e => seen.Add("despawn " + e.Id);
        world.CellsChanged += _ => seen.Add("cells");
        world.ScreenEntryRemoved += id => seen.Add("screen removed " + id);
        world.ScreenEntryChanged += e => seen.Add("screen " + e.Id);
        world.OwnStateReceived += own =>
        {
            seen.Add("own " + own.Tick);
            Assert.Equal(11, world.Tick); // the whole message was applied first
        };

        Delta d = Msg.Delta(11, own: 1, seq: 7);
        d.Despawned.Add(2);
        d.SolidChanged.Add(new CellSolidChange(1, 1, 1, true));
        d.Own = new OwnBlock
        {
            HasMotion = true, Motion = State(3, 3), Params = new MotionParams(0.25f, 0.175f, false, 4), InputBufferDepth = 2,
        };
        d.Own.ScreenAdded.Add(new ScreenEntry(30, TableRef.None, "WEST,SOUTH"));
        world.Apply(d);
        Assert.Equal(new[] { "despawn 2", "cells", "screen 30", "own 11" }, seen);
        Assert.Equal(new OwnState(11, 7, true, State(3, 3), new MotionParams(0.25f, 0.175f, false, 4), 2), world.Own);
        Assert.Equal(7u, world.LastInputSeq);
    }

    [Fact]
    public void ThePrivateTablesAndTheScreenFollowTheBlocksAndASnapshotReplacesThem()
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Own.FirstNewPrivateString = 1;
            s.Own.NewPrivateStrings.AddRange(new[] { "head0", "chest0" });
            s.Own.FirstNewPrivateResource = 1;
            s.Own.NewPrivateResources.Add("icons/hud/screen_gen.dmi");
            s.Own.FirstNewPrivateAppearance = 1;
            s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = new TableRef(1, true), IconState = new TableRef(1, true) });
            s.Own.ScreenAdded.Add(new ScreenEntry(30, new TableRef(1, true), "WEST,CENTER-2"));
            s.Own.ScreenAdded.Add(new ScreenEntry(31, TableRef.Public(1), null));
        });
        Assert.Equal(2, world.Screen.Count);
        Assert.Equal("head0", world.ResolveString(new TableRef(1, true)));
        Assert.Equal("one", world.ResolveString(TableRef.Public(1))); // the public tables are another namespace
        Assert.True(world.TryGetAppearance(new TableRef(1, true), out AppearanceEntry doll));
        Assert.Equal("icons/hud/screen_gen.dmi", world.ResolveResource(doll.Icon));
        Assert.Equal(new[] { "head0", "chest0" }, world.PrivateStrings);

        var changed = new List<ScreenEntry>();
        var removed = new List<long>();
        world.ScreenEntryChanged += changed.Add;
        world.ScreenEntryRemoved += removed.Add;
        Delta d = Next(world);
        d.Own.FirstNewPrivateString = 3;
        d.Own.NewPrivateStrings.Add("head3");
        d.Own.FirstNewPrivateAppearance = 2;
        d.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = new TableRef(1, true), IconState = new TableRef(3, true) });
        d.Own.ScreenChanged.Add(new ScreenEntry(30, new TableRef(2, true), "WEST,CENTER-2"));
        d.Own.ScreenRemoved.Add(31);
        d.Own.ScreenAdded.Add(new ScreenEntry(32, TableRef.None, "EAST,NORTH"));
        world.Apply(d);
        Assert.Equal(new long[] { 31 }, removed);
        Assert.Equal(new long[] { 32, 30 }.OrderBy(i => i), changed.Select(e => e.Id).OrderBy(i => i));
        Assert.Equal(new TableRef(2, true), world.Screen[30].Appearance);
        Assert.False(world.Screen.ContainsKey(31));
        Assert.Equal("head3", world.ResolveString(new TableRef(3, true)));

        // A Snapshot replaces the private tables and the screen; the entries it lacks are reported removed.
        removed.Clear();
        changed.Clear();
        world.Apply(Msg.FlatMap(8, 8));
        Snapshot s = Msg.Snapshot(world.Tick, 1, 0, Types);
        s.Own.FirstNewPrivateString = 1;
        s.Own.NewPrivateStrings.Add("only");
        s.Own.ScreenAdded.Add(new ScreenEntry(40, TableRef.None, "a"));
        world.Apply(s);
        Assert.Equal(new[] { "only" }, world.PrivateStrings);
        Assert.Empty(world.PrivateAppearances);
        Assert.Equal(new long[] { 40 }, world.Screen.Keys.ToArray());
        Assert.Equal(new long[] { 30, 32 }, removed.OrderBy(i => i).ToArray());
        Assert.Equal(new long[] { 40 }, changed.Select(e => e.Id).ToArray());
    }

    [Theory]
    [InlineData("private string first")]
    [InlineData("private appearance first")]
    [InlineData("screen add twice")]
    [InlineData("screen change missing")]
    [InlineData("screen remove missing")]
    [InlineData("screen appearance past table")]
    [InlineData("private icon past table")]
    [InlineData("snapshot removed")]
    public void ABadPrivateBlockIsADesync(string what)
    {
        ReplicaWorld world = Joined(s =>
        {
            s.Own.FirstNewPrivateString = 1;
            s.Own.NewPrivateStrings.Add("a");
            s.Own.ScreenAdded.Add(new ScreenEntry(30, TableRef.None, "x"));
        });
        if (what == "snapshot removed")
        {
            Snapshot bad = Msg.Snapshot(world.Tick + 1, 1, 0, Types);
            bad.Own.ScreenRemoved.Add(30);
            Assert.Throws<ProtocolException>(() => world.Apply(bad));
            return;
        }

        Delta d = Next(world);
        switch (what)
        {
            case "private string first":
                d.Own.FirstNewPrivateString = 3;
                d.Own.NewPrivateStrings.Add("b");
                break;
            case "private appearance first":
                d.Own.FirstNewPrivateAppearance = 2;
                d.Own.NewPrivateAppearances.Add(new AppearanceEntry());
                break;
            case "screen add twice":
                d.Own.ScreenAdded.Add(new ScreenEntry(30, TableRef.None, "y"));
                break;
            case "screen change missing":
                d.Own.ScreenChanged.Add(new ScreenEntry(31, TableRef.None, "y"));
                break;
            case "screen remove missing":
                d.Own.ScreenRemoved.Add(31);
                break;
            case "screen appearance past table":
                d.Own.ScreenAdded.Add(new ScreenEntry(31, new TableRef(1, true), "y"));
                break;
            case "private icon past table":
                d.Own.FirstNewPrivateAppearance = 1;
                d.Own.NewPrivateAppearances.Add(new AppearanceEntry { IconState = new TableRef(2, true) });
                break;
        }

        Assert.Throws<ProtocolException>(() => world.Apply(d));
        Assert.True(world.IsDesynced);
    }

    [Fact]
    public void APrivateAppearanceMayNameAPublicEntryButNotTheOtherWayAround()
    {
        ReplicaWorld world = Joined();
        Delta d = Next(world);
        d.Own.FirstNewPrivateAppearance = 1;
        d.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(2), Overlays = { TableRef.Public(1) } }); // public icon and overlay
        d.Own.NewPrivateAppearances.Add(new AppearanceEntry { Overlays = { new TableRef(1, true) } });
        world.Apply(d);
        Assert.Equal(2, world.PrivateAppearances.Count);
    }

    [Fact]
    public void DecodedMessagesApplyThroughTryApplyAndTheSnapshotRoundTripsThePrivateBlock()
    {
        var client = new ReplicaWorld();
        client.Apply(Msg.FlatMap(8, 8));
        Snapshot s = Msg.Snapshot(5, 1, 3, Types, Capsule(1, 3, 3, 800, 780));
        s.Own = CodecV2Tests.RichOwn();
        s.Own.ScreenChanged.Clear();
        s.Own.ScreenRemoved.Clear();
        s.Own.FirstNewPrivateString = 1;
        s.Own.FirstNewPrivateResource = 1;
        s.Own.FirstNewPrivateAppearance = 1;
        s.Own.NewPrivateAppearances.Clear();
        s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = new TableRef(1, true), IconState = new TableRef(2, true) });
        s.Own.ScreenAdded.Clear();
        s.Own.ScreenAdded.Add(new ScreenEntry(30, new TableRef(1, true), "x"));
        Assert.True(client.TryApply(Codec.Encode(s), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Snapshot, id);
        Assert.Equal(s.Own.Motion, client.Own.Motion);
        Assert.Equal(3u, client.Own.AckedSeq);
        Assert.Equal(5, client.Own.Tick);
        Assert.Single(client.Screen);
    }
}
