using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.M3Contracts;

/// <summary>The M3 contracts step's protocol version 3 types (src/Protocol/MessagesV3.cs, NetField, Limits).</summary>
public class ProtocolV3ContractTests
{
    [Fact]
    public void TheVersionIsAtLeastThree() => Assert.True(ProtocolVersion.Current >= 3); // M4a contracts: version 4

    [Fact]
    public void TheFaceBitsAreTileEdges()
    {
        Assert.Equal((byte)TileEdges.North, CellFlags.FaceNorth);
        Assert.Equal((byte)TileEdges.South, CellFlags.FaceSouth);
        Assert.Equal((byte)TileEdges.East, CellFlags.FaceEast);
        Assert.Equal((byte)TileEdges.West, CellFlags.FaceWest);
        Assert.Equal(TileEdges.North | TileEdges.West, CellFlags.FacesOf((byte)(CellFlags.FaceNorth | CellFlags.FaceWest | CellFlags.Opaque | CellFlags.Starlight)));
        Assert.Equal(0, CellFlags.All & ~(CellFlags.Faces | CellFlags.Opaque | CellFlags.Starlight | CellFlags.ClimbWalkable | CellFlags.Flaps)); // version 37 adds ClimbWalkable, 73 Flaps
        Assert.Equal(0, CellFlags.Faces & (CellFlags.Opaque | CellFlags.Starlight));
    }

    [Fact]
    public void LightIsAFieldOfVersionThreeOnly()
    {
        Assert.Equal(NetField.Light, NetField.Changeable & NetField.Light);
        Assert.Equal(NetField.Id | NetField.Type | NetField.Changeable | NetField.OwnMob, NetField.All);
                Assert.Equal(1u << 14, (uint)NetField.Light);
        Assert.True(LightState.None.IsNone);
        Assert.False(new LightState(8, 1, 0xFFFFFFFF, true, 170, 1, 1, 0, 0.5f, 1).IsNone);
        Assert.True(new EntityState(1, 1, 1, 1, 1, 2, "x", 0, 0).Light.IsNone);
    }

    [Fact]
    public void TheCellRunsExpandOnePerCell()
    {
        var grid = new MapGrid { Width = 3, Height = 2, Levels = 1 };
        Assert.Equal(new byte[6], grid.ExpandFlags()); // no runs: all zero
        Assert.Equal(new ushort[6], grid.ExpandBaseLights());
        grid.FlagRuns.Add(new TurfRun(2, CellFlags.Opaque));
        grid.FlagRuns.Add(new TurfRun(4, CellFlags.FaceEast));
        Assert.Equal(new byte[] { 16, 16, 4, 4, 4, 4 }, grid.ExpandFlags());
        grid.BaseLights.Add(new BaseLight(0xFFFFFF80));
        grid.BaseLightRuns.Add(new TurfRun(6, 1));
        Assert.Equal(new ushort[] { 1, 1, 1, 1, 1, 1 }, grid.ExpandBaseLights());
        grid.FlagRuns.Add(new TurfRun(1, 0));
        Assert.Throws<InvalidOperationException>(() => grid.ExpandFlags()); // seven cells of six
        grid.BaseLightRuns[0] = new TurfRun(5, 1);
        Assert.Throws<InvalidOperationException>(() => grid.ExpandBaseLights()); // five of six
    }

    [Fact]
    public void TheNewCapsAreSane()
    {
        Assert.True(Limits.MaxLightRange >= 8f); // tg's fixtures
        Assert.True(Limits.MaxBaseLightEntries <= ushort.MaxValue);
        Assert.True(Limits.MaxNewTurfTypesPerDelta <= Limits.MaxTurfTypeEntries);
    }
}
