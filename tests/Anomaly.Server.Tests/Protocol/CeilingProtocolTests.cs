using Anomaly.Core.Motion;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Protocol;

public class CeilingProtocolTests
{
    private const string Glass = "/turf/open/floor/glass";

    private static Anomaly.Protocol.MapGrid Map()
    {
        var map = new Anomaly.Protocol.MapGrid { Width = 3, Height = 3, Levels = 1 };
        map.TurfTypes.Add("/turf/open/floor/plating");
        map.Runs.Add(new Anomaly.Protocol.TurfRun(9, 0));
        return map;
    }

    [Fact]
    public void JoinAndDeltaRoundtripPreserveTurfBrokenSupportAndPicture()
    {
        var map = Map();
        var ceiling = new CeilingState("/turf/open/floor/iron/dark", CeilingLattice.None, 3, false);
        map.Ceilings.Add(new(2, 2, 1, ceiling, Anomaly.Protocol.TableRef.Public(7)));
        Assert.True(Anomaly.Protocol.Codec.TryDecode(Anomaly.Protocol.Codec.Encode(map), out Anomaly.Protocol.MapGrid decoded, out _));
        var replica = new Anomaly.Protocol.ReplicaWorld(); replica.Apply(decoded); replica.Apply(new Anomaly.Protocol.Snapshot());
        Assert.Equal(ceiling, replica.CeilingAt(2, 2, 1));
        Assert.Equal(Anomaly.Protocol.TableRef.Public(7), replica.CeilingLookAt(2, 2, 1));
        Assert.False(replica.CeilingAt(1, 1, 1).Exists);
        var delta = new Anomaly.Protocol.Delta { Tick = 1 };
        var damaged = new CeilingState("", CeilingLattice.Catwalk, 0, true, 123);
        delta.Ceilings.Add(new(2, 2, 1, damaged));
        delta.Ceilings.Add(new(1, 1, 1, default));
        Assert.True(Anomaly.Protocol.Codec.TryDecode(Anomaly.Protocol.Codec.Encode(delta), out Anomaly.Protocol.Delta first, out _));
        replica.Apply(first);
        Assert.Equal(damaged, replica.CeilingAt(2, 2, 1));
        delta = new Anomaly.Protocol.Delta { Tick = 2 };
        delta.Ceilings.Add(new(2, 2, 1, default));
        Assert.True(Anomaly.Protocol.Codec.TryDecode(Anomaly.Protocol.Codec.Encode(delta), out Anomaly.Protocol.Delta d, out _));
        replica.Apply(d);
        Assert.False(replica.CeilingAt(2, 2, 1).Exists);
    }

    [Fact]
    public void AMapWithoutCeilingsDoesNotInventThemAndSpaceCanHaveARoof()
    {
        var map = Map();
        Assert.DoesNotContain(ChunkPlanner.Plan(map, null, Array.Empty<PlanEntity>()).Chunks.SelectMany(c => c.Tiles), t => t.Shape == TileShape.Ceiling);
        map.TurfTypes[0] = "/turf/open/space";
        map.Ceilings.Add(new(2, 2, 1, CeilingState.Of(Glass)));
        var tile = Assert.Single(ChunkPlanner.Plan(map, null, Array.Empty<PlanEntity>()).Chunks.SelectMany(c => c.Tiles));
        Assert.Equal(TileShape.Ceiling, tile.Shape);
        Assert.True(tile.Ceiling.IsGlass);
    }

    [Fact]
    public void DecoderRejectsTruncationAndEncoderRejectsInvalidState()
    {
        var map = Map(); map.Ceilings.Add(new(2, 2, 1, CeilingState.Of(Glass)));
        var bytes = Anomaly.Protocol.Codec.Encode(map);
        for (int i = 0; i < bytes.Length; i++)
            Assert.False(Anomaly.Protocol.Codec.TryDecode(bytes.AsSpan(0, i), out Anomaly.Protocol.MapGrid _, out _));
        // a lattice under a floor block, a broken lattice and a turf that is not a floor are refused
        foreach (var bad in new[] { new CeilingState(Glass, CeilingLattice.Lattice), new CeilingState("", CeilingLattice.Lattice, 1), CeilingState.Of("/turf/closed/wall"),
            new CeilingState(Glass, CeilingLattice.None, 0, true, 10), new CeilingState("", CeilingLattice.Lattice, 0, true, 500) })
        {
            map.Ceilings.Clear(); map.Ceilings.Add(new(2, 2, 1, bad));
            Assert.Throws<Anomaly.Protocol.ProtocolException>(() => Anomaly.Protocol.Codec.Encode(map));
        }
    }

    [Fact]
    public void TheMapSaysWhichLevelsHaveNoSpaceBehindThem()
    {
        var map = new Anomaly.Protocol.MapGrid { Width = 2, Height = 2, Levels = 3 };
        map.TurfTypes.Add("/turf/open/floor/plating");
        map.Runs.Add(new Anomaly.Protocol.TurfRun(12, 0));
        map.NoParallaxLevels.Add(3);
        Assert.True(Anomaly.Protocol.Codec.TryDecode(Anomaly.Protocol.Codec.Encode(map), out Anomaly.Protocol.MapGrid decoded, out _));
        var replica = new Anomaly.Protocol.ReplicaWorld(); replica.Apply(decoded);
        Assert.True(replica.IsNoParallax(3));
        Assert.False(replica.IsNoParallax(1));
        // a level past the map, or out of order, is refused
        map.NoParallaxLevels.Clear(); map.NoParallaxLevels.Add(4);
        Assert.Throws<Anomaly.Protocol.ProtocolException>(() => Anomaly.Protocol.Codec.Encode(map));
        map.NoParallaxLevels.Clear(); map.NoParallaxLevels.AddRange(new[] { 2, 1 });
        Assert.Throws<Anomaly.Protocol.ProtocolException>(() => Anomaly.Protocol.Codec.Encode(map));
    }

    [Fact]
    public void GlassPassesExteriorLightWithoutMakingTheCellOpaque()
    {
        var map = Map(); map.Ceilings.Add(new(2, 2, 1, CeilingState.Of(Glass)));
        var replica = new Anomaly.Protocol.ReplicaWorld(); replica.Apply(map);
        Assert.True(replica.ReceivesExteriorLight(2, 2, 1));
        Assert.False(replica.IsOpaque(2, 2, 1));
    }
}
