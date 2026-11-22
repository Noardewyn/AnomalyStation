using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: tile coordinates, directions and the look of the turfs.</summary>
public class TileSpaceTests
{
    [Fact]
    public void ATileIsTwoMetresAndNorthIsMinusZ()
    {
        Assert.Equal(1f, TileSpace.WorldX(1f), 5);
        Assert.Equal(3f, TileSpace.WorldX(2f), 5);
        Assert.Equal(-1f, TileSpace.WorldZ(1f), 5);
        Assert.Equal(-3f, TileSpace.WorldZ(2f), 5);
        // A whole tile east is 2 m, a whole tile north is 2 m towards -Z.
        Assert.Equal(2f, TileSpace.WorldX(11f) - TileSpace.WorldX(10f), 5);
        Assert.Equal(-2f, TileSpace.WorldZ(11f) - TileSpace.WorldZ(10f), 5);
        Assert.Equal(0f, TileSpace.WorldY(1));
        Assert.Equal(WorldScale.WallHeight, TileSpace.WorldY(2)); // a level is one wall high (D119)
        Assert.Equal(2f, TileSpace.WorldY(2));
        Assert.Equal(2f, TileSpace.LevelHeight);
    }

    [Theory]
    [InlineData((byte)1, 0.0, -1.0)]   // north faces -Z
    [InlineData((byte)2, 0.0, 1.0)]    // south
    [InlineData((byte)4, 1.0, 0.0)]    // east faces +X
    [InlineData((byte)8, -1.0, 0.0)]   // west
    [InlineData((byte)5, 0.7071, -0.7071)]
    [InlineData((byte)9, -0.7071, -0.7071)]
    [InlineData((byte)6, 0.7071, 0.7071)]
    [InlineData((byte)10, -0.7071, 0.7071)]
    public void DirectionsFaceTheirWay(byte dir, double expectedX, double expectedZ)
    {
        double yaw = TileSpace.DirYaw(dir);
        // The forward vector of a Godot yaw about +Y is (-sin, -cos) in (X, Z).
        Assert.Equal(expectedX, -Math.Sin(yaw), 3);
        Assert.Equal(expectedZ, -Math.Cos(yaw), 3);
    }

    [Fact]
    public void ANoDirectionOrAnImpossibleOneFacesNorth()
    {
        Assert.Equal(0f, TileSpace.DirYaw(0));
        Assert.Equal(0f, TileSpace.DirYaw(3)); // north and south
        Assert.Equal(0f, TileSpace.DirYaw(12)); // east and west
    }

    [Fact]
    public void HeadingNamesMatchTheQuarterTurns()
    {
        Assert.Equal("NORTH", TileSpace.HeadingName(0));
        Assert.Equal("WEST", TileSpace.HeadingName(1));
        Assert.Equal("SOUTH", TileSpace.HeadingName(2));
        Assert.Equal("EAST", TileSpace.HeadingName(3));
        Assert.Equal("EAST", TileSpace.HeadingName(-1));
        Assert.Equal("WEST", TileSpace.HeadingName(5));
    }

    [Theory]
    [InlineData("/turf/closed/indestructible", TurfKind.Closed)]
    [InlineData("/turf/closed/wall/r_wall", TurfKind.Closed)]
    [InlineData("/turf/open/indestructible", TurfKind.Open)]
    [InlineData("/turf/open/floor/plating", TurfKind.Open)]
    [InlineData("/turf/open/space", TurfKind.Void)]
    [InlineData("/turf/open/space/basic", TurfKind.Void)]
    [InlineData("/turf", TurfKind.Open)]
    [InlineData("", TurfKind.Open)]
    [InlineData(null, TurfKind.Open)]
    public void TurfTypesAreClassified(string path, TurfKind expected)
    {
        Assert.Equal(expected, TurfStyle.Classify(path));
    }

    [Theory]
    [InlineData("/turf/open/floor/iron/white", true)]
    [InlineData("/turf/open/floor/iron/dark/corner", true)]
    [InlineData("/turf/open/floor/mineral/titanium", true)]
    [InlineData("/turf/open/floor/iron", false)]
    [InlineData("/turf/open/floor/iron/whitewash_never", false)]
    [InlineData("/turf/open/floor/carpet", false)]
    [InlineData("/turf/closed/wall", false)]
    [InlineData(null, false)]
    public void OnlyThePolishedFloorsReflect(string path, bool expected)
    {
        Assert.Equal(expected, TurfStyle.Polished(path));
    }

    [Fact]
    public void ATurfTypeAlwaysLooksTheSameAndTypesDiffer()
    {
        Assert.Equal(TurfStyle.Tint("/turf/closed/indestructible"), TurfStyle.Tint("/turf/closed/indestructible"));
        Assert.NotEqual(TurfStyle.Tint("/turf/closed/indestructible"), TurfStyle.Tint("/turf/closed/wall"));
        Rgb c = TurfStyle.Tint("/turf/open/floor/plating");
        Assert.InRange(c.R, 0f, 1f);
        Assert.InRange(c.G, 0f, 1f);
        Assert.InRange(c.B, 0f, 1f);
    }
}
