using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>Third-person play (the owner, 2026-10-16): the own mob's line of sight that shades the third-person view (Space Station 14's FOV).</summary>
public class FovMaskTests
{
    // A wall along x = 5 (y 1 to 30), the eye in the middle of tile (3, 10).
    private static FovMask Mask(HashSet<(int, int)> walls, float eyeX = 3f, float eyeY = 10f, int radius = 10)
    {
        var mask = new FovMask(radius);
        mask.Update((x, y, z) => walls.Contains((x, y)), eyeX, eyeY, 1);
        return mask;
    }

    private static HashSet<(int, int)> WallColumn()
    {
        var walls = new HashSet<(int, int)>();
        for (int y = 1; y <= 30; y++)
            walls.Add((5, y));
        return walls;
    }

    [Fact]
    public void AWallHidesWhatIsBehindItButItsOwnFaceIsSeen()
    {
        FovMask mask = Mask(WallColumn());
        Assert.True(mask.CellVisible(3, 10, 1)); // the eye's own cell
        Assert.True(mask.CellVisible(4, 10, 1));
        Assert.True(mask.CellVisible(5, 10, 1)); // the wall: its face and top towards the eye
        Assert.False(mask.CellVisible(6, 10, 1));
        Assert.False(mask.CellVisible(9, 14, 1));
        Assert.True(mask.Sees(5.4f, 10f)); // a point on the wall's top, over its far half
        Assert.False(mask.Sees(5.6f, 10f)); // just past the wall: the next cell
    }

    [Fact]
    public void AWallSeenOnlyAlongItsFaceIsLitWholeAndAWallBehindAWallIsNot()
    {
        // A wall row along y = 12 and a second row behind it at y = 13; the eye at (3, 10) sees row 12 at a slant.
        var walls = new HashSet<(int, int)>();
        for (int x = 1; x <= 30; x++)
        {
            walls.Add((x, 12));
            walls.Add((x, 13));
        }
        FovMask mask = Mask(walls);
        int At(int x, int y) => (y - mask.OriginY) * mask.Side + (x - mask.OriginX);
        Assert.True(mask.CellVisible(11, 12, 1)); // far along the row: only its face is in sight
        Assert.Equal(FovMask.OpaqueSeen, mask.Texels[At(11, 12)]);
        Assert.Equal(FovMask.OpaqueHidden, mask.Texels[At(11, 13)]);
        Assert.Equal(0, mask.Texels[At(11, 11)]);
    }

    [Fact]
    public void ADoorwayLetsTheSightThroughInAWedge()
    {
        HashSet<(int, int)> walls = WallColumn();
        walls.Remove((5, 10)); // an open airlock
        FovMask mask = Mask(walls);
        Assert.True(mask.CellVisible(8, 10, 1)); // straight through
        Assert.True(mask.CellVisible(9, 11, 1)); // within the wedge
        Assert.False(mask.CellVisible(7, 16, 1)); // far to the side of it
    }

    [Fact]
    public void OutsideTheWindowAndOnOtherLevelsNothingIsSeen()
    {
        FovMask mask = Mask(new HashSet<(int, int)>(), radius: 4);
        Assert.True(mask.CellVisible(7, 10, 1));
        Assert.False(mask.CellVisible(8, 10, 1));
        Assert.False(mask.CellVisible(3, 10, 2));
    }

    [Fact]
    public void TheMaskFollowsTheEyeAndTheDoors()
    {
        HashSet<(int, int)> walls = WallColumn();
        var mask = new FovMask(10);
        mask.Update((x, y, z) => walls.Contains((x, y)), 3f, 10f, 1);
        int version = mask.Version;
        Assert.False(mask.CellVisible(7, 10, 1));
        mask.Update((x, y, z) => walls.Contains((x, y)), 3.01f, 10f, 1);
        Assert.Equal(version, mask.Version); // a hair's move: nothing is worked out again
        walls.Remove((5, 10)); // the airlock opens
        mask.Update((x, y, z) => walls.Contains((x, y)), 3.01f, 10f, 1);
        Assert.True(mask.Version > version);
        Assert.True(mask.TextureDirty);
        Assert.True(mask.CellVisible(7, 10, 1));
        Assert.Equal(0, mask.Opaque[(10 - mask.OriginY) * mask.Side + (5 - mask.OriginX)]);
        Assert.Equal(255, mask.Opaque[(11 - mask.OriginY) * mask.Side + (5 - mask.OriginX)]);
    }
}
