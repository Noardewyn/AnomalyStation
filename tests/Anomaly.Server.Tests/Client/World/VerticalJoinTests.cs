using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>D325 (the owner, 2026-11-10): walls and full-tile windows join the ones straight above and below them, as they join along a row, unless a floor lies between.</summary>
public sealed class VerticalJoinTests
{
    private const string Floor = "/turf/open/floor/iron", Wall = "/turf/closed/wall", Open = "/turf/open/openspace";

    // A 3 x 1 map of two levels, each level's turfs given as a string (W wall, F floor, O open space); level 2 stands on level 1 when linked.
    private static MapGrid TwoLevels(string level1, string level2, bool linked = true)
    {
        var grid = new MapGrid { Width = level1.Length, Height = 1, Levels = 2 };
        grid.TurfTypes.AddRange(new[] { Floor, Wall, Open });
        foreach (char c in level1 + level2)
            grid.Runs.Add(new TurfRun(1, c switch { 'W' => 1, 'O' => 2, _ => 0 }));
        if (linked)
            grid.LinkedBelowLevels.Add(2);
        return grid;
    }

    private static TileCell WallAt(WorldPlan plan, int x, int z) => plan.ChunkOf(x, 1, z).Tiles.Single(t => t.Shape == TileShape.Wall && t.X == x && t.Z == z);

    [Theory]
    [InlineData(0, 12)] // D121's straight run
    [InlineData(Smoothing.Above, 157)] // N | NE | NW over the run
    [InlineData(Smoothing.Below, 110)]
    [InlineData(Smoothing.Above | Smoothing.Below, 255)]
    public void AWallsSideJoinsTheWallsAboveAndBelowIt(int stack, int junction) => Assert.Equal(junction, Smoothing.SideJunction((byte)stack));

    [Fact]
    public void AWallKnowsTheWallsStraightAboveAndBelowItOnALinkedLevel()
    {
        WorldPlan plan = ChunkPlanner.Plan(TwoLevels("WWF", "WFW"), null, Array.Empty<PlanEntity>());
        Assert.Equal(Smoothing.Above, WallAt(plan, 1, 1).Stack);
        Assert.Equal(Smoothing.Below, WallAt(plan, 1, 2).Stack);
        Assert.Equal(0, WallAt(plan, 2, 1).Stack); // a floor over it
        Assert.Equal(0, WallAt(plan, 3, 2).Stack); // a floor under it
        WorldPlan apart = ChunkPlanner.Plan(TwoLevels("WWF", "WFW", linked: false), null, Array.Empty<PlanEntity>());
        Assert.Equal(0, WallAt(apart, 1, 1).Stack); // levels that are not one stack do not join
    }

    [Fact]
    public void AWallsTopIsSunkOnlyUnderAFloorOrWallOfTheLevelAbove()
    {
        // the owner (2026-11-10): sunk under open space, the walls beside it stood over its top as lines between the blocks
        WorldPlan plan = ChunkPlanner.Plan(TwoLevels("WWW", "FWO"), null, Array.Empty<PlanEntity>());
        Assert.False(WallAt(plan, 1, 1).Roofless); // a floor over it
        Assert.False(WallAt(plan, 2, 1).Roofless); // a wall over it
        Assert.True(WallAt(plan, 3, 1).Roofless); // open space over it
        Assert.True(WallAt(plan, 2, 2).Roofless); // the top level
        Assert.True(WallAt(ChunkPlanner.Plan(TwoLevels("WWW", "FWO", linked: false), null, Array.Empty<PlanEntity>()), 1, 1).Roofless);
    }

    // the owner (2026-11-17): a wall placed on a floor seemed to delete it, for from below its tile showed the wall's own picture (the rule of
    // 2026-11-10). A wall stands on its plating as a floor tile lies on its: the underside is plating under both
    [Fact]
    public void TheCeilingUnderAWallOnTheLevelAboveIsPlatingAsUnderAFloor()
    {
        var wallLook = TableRef.Public(7);
        WorldPlan plan = ChunkPlanner.Plan(TwoLevels("FFF", "WFO"), (x, y, z) => z == 2 && x == 1 ? wallLook : TableRef.None, Array.Empty<PlanEntity>());
        TileCell[] ceilings = plan.ChunkOf(1, 1, 1).Tiles.Where(t => t.Shape == TileShape.Ceiling && t.Z == 1).OrderBy(t => t.X).ToArray();
        Assert.Equal(new[] { 1, 2 }, ceilings.Select(t => t.X)); // none under the open space
        Assert.All(ceilings, tile =>
        {
            Assert.True(tile.Appearance.IsNone); // no picture of its own: the plating's
            Assert.Equal(Anomaly.Core.Motion.CeilingState.Plating, tile.Ceiling.Turf);
        });
    }

    [Theory]
    // the middle of a row, stacked under a window with its row stacked too: the frame leaves its top and both upper corners
    [InlineData("window-12", Smoothing.Above | BlockFaces.UpLeft | BlockFaces.UpRight, "window-157")]
    // the row's left neighbour is not stacked: the upper-left corner keeps its notch
    [InlineData("window-12", Smoothing.Above | BlockFaces.UpRight, "window-29")]
    // a lone pane between two: frame only at its sides
    [InlineData("window-0", Smoothing.Above | Smoothing.Below, "window-3")]
    [InlineData("window-12", 0, "window-12")]
    public void AWindowsSideFaceJoinsTheWindowsAboveAndBelowIt(string state, int stack, string expected)
        => Assert.Equal(expected, BlockFaces.FaceState(state, ByondDirConst.South, stack: stack));

    [Fact]
    public void ATopFaceIsNotJoinedUpOrDown() => Assert.Equal("window-12", BlockFaces.FaceState("window-12", 0, stack: Smoothing.Above | Smoothing.Below));

    [Fact]
    public void AFacesLeftNeighbourIsTheViewersLeft()
    {
        Assert.Equal((-1, 0), BlockFaces.LeftOf(ByondDirConst.South));
        Assert.Equal((1, 0), BlockFaces.LeftOf(ByondDirConst.North));
        Assert.Equal((0, -1), BlockFaces.LeftOf(ByondDirConst.East));
        Assert.Equal((0, 1), BlockFaces.LeftOf(ByondDirConst.West));
    }

    [Theory]
    // the owner (2026-11-10): a built wall is framed where it meets what it does not join; seen from the south its left is west, its right east
    [InlineData(0, 0, 0, 0)] // a lone wall: the frame all round
    [InlineData(12, 0, 0, 12)] // the middle of a row: top and bottom only
    [InlineData(4, 0, 0, 4)] // the row's west end: the frame on the left too
    [InlineData(5, 0, 0, 4)] // an L's corner (walls north and east): its outer south face keeps the frame on the left (the owner: no bent pane as a window's)
    [InlineData(12, Smoothing.Above, ByondDirConst.West | ByondDirConst.East, 157)] // under a stacked row: no top
    [InlineData(12, Smoothing.Above, ByondDirConst.East, 29)] // the west neighbour has nothing over it: the upper-left corner stays
    [InlineData(12, Smoothing.Below, (ByondDirConst.West | ByondDirConst.East) << 4, 110)]
    public void AWallsSouthFaceIsFramedWhereItJoinsNothing(int junction, int stack, int near, int expected)
        => Assert.Equal(expected, BlockFaces.WallFace(junction, ByondDirConst.South, stack, near));

    [Fact]
    public void AWallKnowsWhichOfItsNeighboursHaveWallsAboveAndBelowThem()
    {
        WorldPlan plan = ChunkPlanner.Plan(TwoLevels("WWF", "WWF"), null, Array.Empty<PlanEntity>());
        Assert.Equal(ByondDirConst.East, WallAt(plan, 1, 1).NearStack);
        Assert.Equal(ByondDirConst.East << 4, WallAt(plan, 1, 2).NearStack);
        Assert.Equal(ByondDirConst.West, WallAt(plan, 2, 1).NearStack);
    }

    [Fact]
    public void AWallThatDoesNotSmoothJoinsNothingAboveOrBelow()
    {
        // tg's indestructible walls have no junctions: a smoothing wall under one keeps its frame
        var options = new PlanOptions { Smooths = (x, y, z) => !(x == 1 && z == 2) };
        WorldPlan plan = ChunkPlanner.Plan(TwoLevels("WWF", "WWF"), null, Array.Empty<PlanEntity>(), options);
        Assert.Equal(0, WallAt(plan, 1, 1).Stack);
        Assert.Equal(Smoothing.Above, WallAt(plan, 2, 1).Stack);
        Assert.Equal(0, WallAt(plan, 2, 1).NearStack); // its west neighbour joins nothing above
    }
}
