using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>
/// The dev menu's spawn tool (the owner, 2026-11-10): the cell in front of the face a click hit, a wall on a wall's top, a thing stuck to a side,
/// a wall under a ceiling.
/// </summary>
public sealed class FaceCellTests
{
    private const double Level = 2.0;

    // a hit at (east, north, up) metres of level z, tile (x, y)
    private static PickHit Hit(PickSurface surface, int x, int y, int z, double east, double north, double up)
        => new(surface, TargetKind.Turf, 0, x, y, z, 1, east, north, up, 0, 0);

    [Fact]
    public void AWallsTopIsTheCellAboveIt()
        => Assert.Equal((3, 4, 2), PickGrid.InFront(Hit(PickSurface.Wall, 3, 4, 1, 5, 7, 2.0), 0.3, 0.2, -0.9, Level));

    [Fact]
    public void AWallsSideIsTheCellBesideIt()
        // wall (3, 4) spans east 4..6: a ray going east hits its west face at 4, the cell in front is (2, 4)
        => Assert.Equal((2, 4, 1), PickGrid.InFront(Hit(PickSurface.Wall, 3, 4, 1, 4.0, 7, 1.2), 1, 0, 0, Level));

    [Fact]
    public void ACeilingIsTheCellUnderIt()
        => Assert.Equal((3, 4, 1), PickGrid.InFront(Hit(PickSurface.Ceiling, 3, 4, 1, 5, 7, 2.0), 0, 0.3, 0.9, Level));

    [Fact]
    public void TheUndersideOfTheLevelAboveIsTheCellUnderIt()
        // seen from below through a cell with no ceiling: the hit is the turf of level 2 at its floor
        => Assert.Equal((3, 4, 1), PickGrid.InFront(Hit(PickSurface.Floor, 3, 4, 2, 5, 7, 0), 0, 0.3, 0.9, Level));

    [Fact]
    public void AFloorIsItsOwnCell()
        => Assert.Equal((3, 4, 1), PickGrid.InFront(Hit(PickSurface.Floor, 3, 4, 1, 5, 7, 0), 0, 0.3, -0.9, Level));

    [Fact]
    public void ACeilingToolAimedUpThroughAHoleOnALowerLevelHitsThatEmptyCeiling()
    {
        var world = new PickFixture { Levels = 2 };
        world.NoCeiling.Add((3, 3, 1));
        var ray = new PickRay(PickFixture.CentreOf(3), PickFixture.CentreOf(3), 1.2, 1, 0, 0, 1, 10);
        Assert.Equal(TargetKind.Turf, new WorldPicker(world).Pick(ray, 0, entitiesOnly: false).Kind); // the ordinary pick goes on to the level above
        PickHit hit = new WorldPicker(world) { PickEmptyCeilings = true }.Pick(ray, 0, entitiesOnly: false);
        Assert.Equal((TargetKind.Ceiling, 3, 3, 1), (hit.Kind, hit.X, hit.Y, hit.Z));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    public void AMountFacesTheWallItWasHungOnBackAlongTheStepToIt(int dx, int dy)
    {
        byte dir = Anomaly.Client.Shared.World.WallMounts.DirOfStep(dx, dy);
        Assert.Equal((dx, dy), Anomaly.Client.Shared.World.WallMounts.Step(dir));
    }

    [Fact]
    public void TheTopOfTheTopLevelIsPastTheMap()
        => Assert.Equal((3, 4, 4), PickGrid.InFront(Hit(PickSurface.Wall, 3, 4, 3, 5, 7, 2.0), 0, 0, -1, Level));
}
