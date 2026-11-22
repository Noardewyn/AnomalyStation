using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The client replica of protocol version 3 (docs/ai/specs/protocol.md section 13): the cell flags, base lights, turf types, starlight and entity lights.</summary>
public class ReplicaWorldV3Tests
{
    private static readonly string[] Types = { "/mob", "/obj" };
    private static LightState Lamp => new(6f, 1f, 0xFFFFFFFFu, true, 360f, 0, 1f, 0f, 0f, 1);

    private static ReplicaWorld Joined(Action<MapGrid> map = null, params EntityState[] entities)
    {
        var world = new ReplicaWorld();
        MapGrid grid = Msg.FlatMap(8, 8);
        grid.TurfTypes.Add("/turf/closed/wall");
        grid.Runs.Clear();
        grid.Runs.Add(new TurfRun(60, 0));
        grid.Runs.Add(new TurfRun(4, 1));
        grid.FlagRuns.Add(new TurfRun(9, 0));
        grid.FlagRuns.Add(new TurfRun(1, CellFlags.FaceNorth | CellFlags.FaceEast)); // cell 10: x = 2, y = 2
        grid.FlagRuns.Add(new TurfRun(50, 0));
        grid.FlagRuns.Add(new TurfRun(4, CellFlags.Opaque));
        grid.BaseLights.Add(new BaseLight(0xFF800080u));
        grid.BaseLightRuns.Add(new TurfRun(8, 1));
        grid.BaseLightRuns.Add(new TurfRun(56, 0));
        grid.Starlight = new LightState(2f, 1f, 0x8589FAFFu, true, 360f, 0, -0.5f, 0f, 0f, 1);
        map?.Invoke(grid);
        world.Apply(grid);
        world.Apply(Msg.Snapshot(10, 1, 0, Types, entities.Length == 0 ? new[] { Msg.State(1, 0, 3, 3), Msg.State(2, 1, 4, 4) } : entities));
        return world;
    }

    [Fact]
    public void TheMapGridsCellsAreReadBackPerCell()
    {
        ReplicaWorld w = Joined();
        Assert.Equal(CellFlags.FaceNorth | CellFlags.FaceEast, w.CellFlagsAt(2, 2, 1));
        Assert.Equal(TileEdges.North | TileEdges.East, w.FacesOf(2, 2, 1));
        Assert.Equal(TileEdges.None, w.FacesOf(3, 2, 1));
        Assert.False(w.IsOpaque(2, 2, 1));
        Assert.True(w.IsOpaque(5, 8, 1)); // the last four cells are opaque
        Assert.True(w.IsOpaque(0, 0, 1)); // outside the map
        Assert.Equal(0, w.CellFlagsAt(99, 1, 1));
        Assert.Equal(1, w.BaseLightIndexAt(8, 1, 1));
        Assert.Equal(0, w.BaseLightIndexAt(1, 2, 1));
        Assert.Equal(0xFF800080u, w.BaseLights[0].Rgba);
        Assert.Equal("/turf/open/floor", w.TurfTypeAt(1, 1, 1));
        Assert.Equal("/turf/closed/wall", w.TurfTypeAt(8, 8, 1));
        Assert.Null(w.TurfTypeAt(0, 1, 1));
        Assert.Equal(2f, w.Starlight.Range);
        Assert.False(w.IsStarlit(1, 1, 1));
        // The collision view returns the faces.
        Assert.Equal(TileEdges.North | TileEdges.East, w.CollisionView.EdgesOf(2, 2, 1));
        Assert.True(TileFaces.IsFaceBlocked(w.CollisionView, 3, 2, 1, MotionDir.West));
    }

    [Fact]
    public void AMapGridWithoutVersion3PartsIsAllZero()
    {
        var w = new ReplicaWorld();
        w.Apply(Msg.FlatMap(4, 4));
        Assert.Equal(0, w.CellFlagsAt(2, 2, 1));
        Assert.Empty(w.BaseLights);
        Assert.True(w.Starlight.IsNone);
        Assert.Equal("/turf/open/floor", w.TurfTypeAt(1, 1, 1));
    }

    [Fact]
    public void ADeltaChangesTheCellsExtendsTheTablesAndRaisesTheEventsInOrder()
    {
        ReplicaWorld w = Joined();
        var cells = new List<(int X, int Y, int Z)>();
        w.CellsChanged += c => cells.AddRange(c);
        LightState? star = null;
        w.StarlightChanged += l => star = l;
        Delta d = Msg.Delta(w.Tick + 1);
        d.CellFlagsChanged.Add(new CellFlagsChange(2, 2, 1, CellFlags.Opaque | CellFlags.FaceSouth));
        d.FirstNewBaseLight = 2;
        d.NewBaseLights.Add(new BaseLight(0x11223344));
        d.BaseLightChanged.Add(new CellBaseLightChange(3, 3, 1, 2));
        d.FirstNewTurfType = 2;
        d.NewTurfTypes.Add("/turf/open/floor/plating");
        d.TurfTypeChanged.Add(new CellTurfTypeChange(4, 4, 1, 2));
        d.StarlightChanged = new LightState(3f, 2f, 0xFFFFFFFFu, true, 360f, 0, -0.5f, 0f, 0f, 1);
        w.Apply(d);
        Assert.Equal(TileEdges.South, w.FacesOf(2, 2, 1));
        Assert.True(w.IsOpaque(2, 2, 1));
        Assert.Equal(0x11223344u, w.BaseLights[1].Rgba);
        Assert.Equal(2, w.BaseLightIndexAt(3, 3, 1));
        Assert.Equal("/turf/open/floor/plating", w.TurfTypeAt(4, 4, 1));
        Assert.Equal(3, w.TurfTypes.Count);
        Assert.Equal(3f, w.Starlight.Range);
        Assert.Equal(3f, star?.Range);
        Assert.Equal(new[] { (2, 2, 1), (3, 3, 1), (4, 4, 1) }, cells.ToArray());
        // The MapGrid object as received is unchanged.
        Assert.Equal(2, w.Map.TurfTypes.Count);
    }

    [Fact]
    public void ADeltaThatBreaksATableOrAnIndexIsADesync()
    {
        foreach (Action<Delta> bad in new Action<Delta>[]
                 {
                     d => { d.FirstNewBaseLight = 5; d.NewBaseLights.Add(new BaseLight(1)); },
                     d => d.BaseLightChanged.Add(new CellBaseLightChange(1, 1, 1, 9)),
                     d => { d.FirstNewTurfType = 7; d.NewTurfTypes.Add("/turf"); },
                     d => d.TurfTypeChanged.Add(new CellTurfTypeChange(1, 1, 1, 5)),
                     d => d.CellFlagsChanged.Add(new CellFlagsChange(9, 1, 1, 0)),
                 })
        {
            ReplicaWorld w = Joined();
            Delta d = Msg.Delta(w.Tick + 1);
            bad(d);
            Assert.Throws<ProtocolException>(() => w.Apply(d));
            Assert.True(w.IsDesynced);
        }

        // A MapGrid whose base-light run names an entry past its table is refused at Apply.
        Assert.Throws<ProtocolException>(() => Joined(g => { g.BaseLightRuns.Clear(); g.BaseLightRuns.Add(new TurfRun(64, 3)); }));
        Assert.Throws<ProtocolException>(() => Joined(g => { g.FlagRuns.Clear(); g.FlagRuns.Add(new TurfRun(60, 0)); }));
    }

    [Fact]
    public void EntityLightsAreFieldsWithAnEventPerEntityInIdOrder()
    {
        ReplicaWorld w = Joined(null, Msg.State(1, 0, 3, 3), Msg.State(2, 1, 4, 4) with { Light = Lamp });
        var seen = new List<(long Id, bool Has)>();
        w.LightChanged += e => seen.Add((e.Id, e.HasLight));
        Assert.True(w.Entities.Single(e => e.Id == 2).HasLight);
        Assert.Equal(Lamp, w.Entities.Single(e => e.Id == 2).Light);

        Delta d = Msg.Delta(w.Tick + 1);
        d.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Light, Light = Lamp with { Range = 3f } });
        d.Changed.Add(new EntityChange { Id = 2, Mask = NetField.Light, Light = LightState.None });
        d.Spawned.Add(Msg.State(9, 1, 5, 5) with { Light = Lamp });
        w.Apply(d);
        Assert.Equal(new[] { (1L, true), (2L, false), (9L, true) }, seen.ToArray());
        Assert.False(w.Entities.Single(e => e.Id == 2).HasLight);

        // A change that keeps the light the same raises nothing; a despawn with a light raises it with the light gone.
        seen.Clear();
        Delta same = Msg.Delta(w.Tick + 1);
        same.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Light, Light = Lamp with { Range = 3f } });
        same.Despawned.Add(9);
        w.Apply(same);
        Assert.Equal(new[] { (9L, false) }, seen.ToArray());

        // A Snapshot that drops a lit entity and changes another's light raises both.
        seen.Clear();
        w.Apply(Msg.Snapshot(w.Tick + 5, 1, 0, Types, Msg.State(1, 0, 3, 3) with { Light = Lamp with { Power = 2f } }));
        Assert.Contains((1L, true), seen);
    }
}
