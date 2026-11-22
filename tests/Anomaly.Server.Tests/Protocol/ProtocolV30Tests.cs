using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 30 (DV75, docs/ai/specs/protocol.md section 39): a private image on a tile (tg's T-ray pulse). Its bytes, the round trip,
/// the refusal of a partial or out-of-range tile, the cap of 512 images, and the replica's visual of an image on a tile (and none of a bar).
/// </summary>
public class ProtocolV30Tests
{
    private static readonly string[] Types = { "/mob", "/obj/item" };

    private static OwnBlock Decoded(Delta d)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        return back.Own;
    }

    [Fact]
    public void TheVersionIsThirty() => Assert.True(ProtocolVersion.Current >= 30);

    [Fact]
    public void AnImageOnATileRoundTripsWithItsTileAndAnImageOnNothingKeepsZero()
    {
        Delta d = Msg.Delta(9);
        d.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { TurfX = 12, TurfY = 200, TurfZ = 2 });
        d.Own.ImagesAdded.Add(new ImageEntry(2, new TableRef(3, true), 77) { Holder = 77, HolderName = "tank" });
        OwnBlock own = Decoded(d);
        Assert.Equal(12, own.ImagesAdded[0].TurfX);
        Assert.Equal(200, own.ImagesAdded[0].TurfY);
        Assert.Equal(2, own.ImagesAdded[0].TurfZ);
        Assert.Equal(0, own.ImagesAdded[1].TurfX);
        Assert.Equal("tank", own.ImagesAdded[1].HolderName);
        Assert.Equal(d.Own.ImagesAdded, own.ImagesAdded);
    }

    [Theory]
    [InlineData(5, 0, 1)]
    [InlineData(0, 5, 1)]
    [InlineData(5, 5, 0)]
    [InlineData(-1, 5, 1)]
    public void APartialOrNegativeTileIsRefusedOnTheWire(int x, int y, int z)
    {
        Delta d = Msg.Delta(9);
        d.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { TurfX = x, TurfY = y, TurfZ = z });
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }

    [Fact]
    public void ATileOutsideTheLimitsIsRefusedWhenItIsRead()
    {
        Delta d = Msg.Delta(9);
        d.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { TurfX = 1, TurfY = 1, TurfZ = 1 });
        byte[] ok = Codec.Encode(d);
        d.Own.ImagesAdded.Clear();
        d.Own.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), 0) { TurfX = Limits.MaxMapSize + 1, TurfY = 1, TurfZ = 1 });
        byte[] tooFar = Codec.Encode(d); // the sender does not check the map's limits: the reader does
        Assert.True(Codec.TryDecode(ok, out Delta _, out ProtocolError _));
        Assert.False(Codec.TryDecode(tooFar, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
    }

    [Fact]
    public void AHundredsOfImagesFitTheCap()
    {
        Delta d = Msg.Delta(9);
        for (int i = 1; i <= Limits.MaxPrivateImages; i++)
            d.Own.ImagesAdded.Add(new ImageEntry(i, TableRef.Public(1), 0) { TurfX = 1 + i % 200, TurfY = 3, TurfZ = 1 });
        Assert.Equal(512, Limits.MaxPrivateImages);
        Assert.Equal(d.Own.ImagesAdded, Decoded(d).ImagesAdded);
    }

    private static ReplicaWorld Joined()
    {
        var world = new ReplicaWorld();
        MapGrid grid = Msg.FlatMap(8, 8);
        grid.SolidBits = new byte[8];
        grid.AppearanceRuns.Add(new TurfRun(64, 1));
        world.Apply(grid);
        Snapshot s = Msg.Snapshot(10, 1, 0, Types, Msg.State(1, 0, 3, 3));
        s.Resources.Add("icons/a.dmi");
        s.Strings.Add("0-1");
        s.Strings.Add("prog_bar_45");
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), IconState = TableRef.Public(1), Alpha = 128, Dir = 4 });
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), IconState = TableRef.Public(2) });
        world.Apply(s);
        return world;
    }

    [Fact]
    public void TheReplicaMakesAVisualOfAnImageOnATileAndRemovesItWithTheImage()
    {
        ReplicaWorld world = Joined();
        var spawned = new List<ReplicaEntity>();
        var despawned = new List<long>();
        world.Spawned += spawned.Add;
        world.Despawned += e => despawned.Add(e.Id);
        int entities = world.Entities.Count();

        Delta d = Msg.Delta(world.Tick + 1);
        d.Own.ImagesAdded.Add(new ImageEntry(7, TableRef.Public(1), 0) { TurfX = 5, TurfY = 4, TurfZ = 1 });
        d.Own.ImagesAdded.Add(new ImageEntry(8, TableRef.Public(2), 0) { TurfX = 6, TurfY = 4, TurfZ = 1 }); // a progress bar on a tile: the HUD's
        d.Own.ImagesAdded.Add(new ImageEntry(9, TableRef.Public(1), 1)); // on an entity: a visual drawn on it (M8 slice F: a data HUD's image on a mob)
        world.Apply(d);
        Assert.Equal(2, spawned.Count);
        ReplicaEntity onEntity = spawned.Single(e => e.Id == -9);
        Assert.True(onEntity.State.IsVisual);
        Assert.Equal(1, onEntity.State.VisHost);
        ReplicaEntity visual = spawned.Single(e => e.Id == -7);
        Assert.Equal(-7, visual.Id);
        Assert.True(visual.State.IsVisual);
        Assert.Equal(0, visual.State.VisHost);
        Assert.Equal((5, 4, 1), (visual.State.X, visual.State.Y, visual.State.Z));
        Assert.Equal(4, visual.State.Dir);
        Assert.Equal(TableRef.Public(1), visual.State.Appearance);
        Assert.Equal(entities, world.Entities.Count()); // it is in no table of entities
        Assert.Empty(despawned);

        // a change of the image replaces its visual
        spawned.Clear();
        Delta c = Msg.Delta(world.Tick + 1);
        c.Own.ImagesChanged.Add(new ImageEntry(7, TableRef.Public(1), 0) { TurfX = 5, TurfY = 5, TurfZ = 1 });
        world.Apply(c);
        Assert.Equal(new long[] { -7 }, despawned);
        Assert.Equal(5, Assert.Single(spawned).State.Y);

        despawned.Clear();
        Delta r = Msg.Delta(world.Tick + 1);
        r.Own.ImagesRemoved.Add(7);
        world.Apply(r);
        Assert.Equal(new long[] { -7 }, despawned);
    }
}
