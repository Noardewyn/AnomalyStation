using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>
/// D325 (the owner, 2026-11-10: the hover name showed the own turf when looking at a wall on another level, and the dev tools and pointing could not reach there):
/// the world pick goes on through open space into the level under it, and into the level over a cell whose ceiling is that level's turf.
/// </summary>
public sealed class MultiLevelPickTests
{
    // Two levels; the eye at the middle of tile (3, 5) of level z, 1.7 m over its floor, looking east and down (or up) one metre per two.
    private static PickRay Ray(int z, bool up) => new(PickFixture.CentreOf(3), PickFixture.CentreOf(5), 1.7, z, 2 / Math.Sqrt(5), 0, (up ? 1 : -1) / Math.Sqrt(5), 30);

    [Fact]
    public void LookingDownThroughOpenSpacePicksTheMobOnTheLevelBelow()
    {
        var world = new PickFixture { Levels = 2 };
        world.Open.Add((5, 5, 2)); // the ray reaches level 2's floor at 8.4 m east, in tile 5
        world.Moving.Add(PickShape.StandingPicture(7, PickSurface.Billboard, 5, 5, 1, PickFixture.CentreOf(5), PickFixture.CentreOf(5), 0,
            SpriteRect.Of(32, 32, 0, 0), Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0));
        PickHit hit = new WorldPicker(world).Pick(Ray(2, up: false), exclude: 0);
        Assert.Equal(TargetKind.Entity, hit.Kind);
        Assert.Equal(7, hit.Id);
        Assert.Equal(1, hit.Z);
    }

    [Fact]
    public void LookingDownThroughOpenSpacePicksTheFloorOfTheLevelBelowAndWithoutItTheOwnFloor()
    {
        var world = new PickFixture { Levels = 2 };
        world.Open.Add((5, 5, 2));
        PickHit below = new WorldPicker(world).Pick(Ray(2, up: false), exclude: 0);
        Assert.Equal((PickSurface.Floor, 7, 5, 1), (below.Surface, below.X, below.Y, below.Z)); // 3.7 m over level 1's floor: 12.4 m east
        world.Open.Clear();
        PickHit own = new WorldPicker(world).Pick(Ray(2, up: false), exclude: 0);
        Assert.Equal((PickSurface.Floor, 5, 5, 2), (own.Surface, own.X, own.Y, own.Z));
    }

    [Fact]
    public void LookingUpUnderALevelPicksItsTurfAndThroughItsOpenSpaceItsWall()
    {
        var world = new PickFixture { Levels = 2 };
        world.NoCeiling.Add((3, 5, 1)); // the floor of level 2 is the ceiling (the ray reaches 2 m at 5.6 m east, in tile 3)
        PickHit floor = new WorldPicker(world).Pick(Ray(1, up: true), exclude: 0);
        Assert.Equal((TargetKind.Turf, PickSurface.Floor, 3, 5, 2), (floor.Kind, floor.Surface, floor.X, floor.Y, floor.Z));
        world.Open.Add((3, 5, 2));
        world.Walls.Add((4, 5, 2)); // entered at 6 m east, 0.2 m over level 2's floor
        PickHit wall = new WorldPicker(world).Pick(Ray(1, up: true), exclude: 0);
        Assert.Equal((PickSurface.Wall, 4, 5, 2), (wall.Surface, wall.X, wall.Y, wall.Z));
    }

    [Fact]
    public void ARayComeUpFromBelowPassesUnderTheWallsBeforeTheOpenSpace()
    {
        var world = new PickFixture { Levels = 2 };
        // from 0.2 m over the floor of tile 1 the ray reaches 2 m at 4.6 m east (tile 3) and rises through the open space there; it passed 1.3 m under level 2's
        // wall in tile 2, and enters the one in tile 4 0.7 m up
        var ray = new PickRay(1.0, PickFixture.CentreOf(5), 0.2, 1, 2 / Math.Sqrt(5), 0, 1 / Math.Sqrt(5), 30);
        world.NoCeiling.Add((3, 5, 1));
        world.Open.Add((3, 5, 2));
        world.Walls.Add((2, 5, 2));
        world.Walls.Add((4, 5, 2));
        PickHit hit = new WorldPicker(world).Pick(ray, exclude: 0);
        Assert.Equal((PickSurface.Wall, 4, 5, 2), (hit.Surface, hit.X, hit.Y, hit.Z));
    }

    [Fact]
    public void AnEyeOverTheOwnLevelsCeilingPicksFromTheLevelItIsIn()
    {
        // the third-person camera 5 m over the floor of the mob's level 1 (1 m into level 3) looks east and down at a stack of two walls in tile 4: the ray
        // goes down through level 3's open space and hits the top of the upper wall (from level 1 it passed under that wall and over the lower one)
        var world = new PickFixture { Levels = 3 };
        world.Walls.Add((4, 5, 1));
        world.Walls.Add((4, 5, 2));
        world.Open.Add((4, 5, 3));
        var ray = new PickRay(PickFixture.CentreOf(3), PickFixture.CentreOf(5), 5.0, 1, 2 / Math.Sqrt(5), 0, -1 / Math.Sqrt(5), 30);
        PickHit hit = new WorldPicker(world).Pick(ray, exclude: 0);
        Assert.Equal((PickSurface.Wall, 4, 5, 2), (hit.Surface, hit.X, hit.Y, hit.Z));
        Assert.Equal(2.0, hit.HitUp, 6); // its top
    }
}
