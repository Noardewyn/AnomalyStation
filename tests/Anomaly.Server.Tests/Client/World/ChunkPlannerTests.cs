using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 P6: the chunk planner, wall-mount placement and the classification of types (engine-free).</summary>
public sealed class ChunkPlannerTests
{
    private static PlanEntity Entity(long id, string type, int x, int y, byte dir = 2, short px = 0, short py = 0, bool appearance = true, string state = null)
        => new(id, type, x, y, 1, dir, appearance ? TableRef.Public((uint)id) : TableRef.None, px, py, LightState.None, state);

    private static MapGrid Grid(int width, int height, Func<int, int, string> turf)
    {
        var grid = new MapGrid { Width = width, Height = height, Levels = 1 };
        var types = new Dictionary<string, int>();
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
            {
                string path = turf(x, y);
                if (!types.TryGetValue(path, out int index))
                {
                    types[path] = index = types.Count;
                    grid.TurfTypes.Add(path);
                }
                grid.Runs.Add(new TurfRun(1, index));
                if (path == Floor) grid.Ceilings.Add(new CeilingChange(x, y, 1, Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating)));
            }
        }
        return grid;
    }

    private const string Floor = "/turf/open/floor/iron", Wall = "/turf/closed/wall", Space = "/turf/open/space/basic";

    [Theory]
    [InlineData("/obj/machinery/door/airlock/external", RenderKind.Airlock)]
    [InlineData("/obj/machinery/door/window/left/directional/east", RenderKind.Windoor)]
    [InlineData("/obj/machinery/door/poddoor/shutters/preopen", RenderKind.PodDoor)]
    [InlineData("/obj/machinery/door/poddoor", RenderKind.PodDoor)]
    [InlineData("/obj/machinery/door/firedoor", RenderKind.Firelock)]
    [InlineData("/obj/machinery/door/firedoor/heavy", RenderKind.Firelock)]
    [InlineData("/obj/machinery/door/firedoor/border_only/closed", RenderKind.FirelockBorder)]
    [InlineData("/obj/machinery/door/morgue", RenderKind.Airlock)]
    [InlineData("/obj/structure/window/reinforced/fulltile", RenderKind.FullWindow)]
    [InlineData("/obj/structure/window/reinforced/spawner/directional/north", RenderKind.DirectionalWindow)]
    [InlineData("/obj/structure/grille", RenderKind.Grille)]
    [InlineData("/obj/structure/table/wood", RenderKind.Table)]
    [InlineData("/obj/structure/table_frame", RenderKind.Billboard)]
    [InlineData("/obj/structure/railing/corner", RenderKind.Railing)]
    [InlineData("/obj/structure/plasticflaps/opaque", RenderKind.Flap)]
    [InlineData("/obj/machinery/light/small/directional/north", RenderKind.LightFixture)]
    [InlineData("/obj/machinery/light_switch/directional/east", RenderKind.Billboard)]
    [InlineData("/obj/structure/chair/stool", RenderKind.Billboard)]
    public void TypesGetTheirShape(string path, RenderKind expected) => Assert.Equal(expected, PropKinds.Classify(path));

    [Fact]
    public void TheMapIsCutIntoSixteenTileChunks()
    {
        MapGrid grid = Grid(40, 20, (x, y) => x <= 2 ? Wall : Floor);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, Array.Empty<PlanEntity>());
        // 40 x 20 tiles: 3 columns x 2 rows of chunks, all non-empty.
        Assert.Equal(6, plan.Chunks.Count);
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1) }, plan.Chunks.Select(c => (c.ChunkX, c.ChunkY)));
        // A wall is one tile, a floor is a floor and a ceiling.
        ChunkPlan first = plan.ChunkOf(1, 1, 1);
        Assert.Equal(16 * 2 + 14 * 16 * 2, first.Tiles.Count);
        Assert.Equal(2 * 16, first.Tiles.Count(t => t.Shape == TileShape.Wall));
        Assert.Equal(14 * 16, first.Tiles.Count(t => t.Shape == TileShape.Floor));
        Assert.Equal(14 * 16, first.Tiles.Count(t => t.Shape == TileShape.Ceiling));
    }

    [Fact]
    public void SpaceDrawsNothingAndGetsNoCeiling()
    {
        MapGrid grid = Grid(16, 16, (x, y) => x < 8 ? Space : Floor);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, Array.Empty<PlanEntity>());
        Assert.All(plan.Chunks.SelectMany(c => c.Tiles), t => Assert.True(t.X >= 8));
        Assert.Equal(9 * 16, plan.Chunks[0].Tiles.Count(t => t.Shape == TileShape.Ceiling));
    }

    [Fact]
    public void TheCeilingCanBeLeftOut()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, Array.Empty<PlanEntity>(), new PlanOptions { Ceilings = false });
        Assert.DoesNotContain(plan.Chunks[0].Tiles, t => t.Shape == TileShape.Ceiling);
    }

    [Fact]
    public void EntitiesGoIntoTheirChunk()
    {
        MapGrid grid = Grid(40, 20, (x, y) => Floor);
        var entities = new[]
        {
            Entity(1, "/obj/structure/chair", 3, 3),
            Entity(2, "/obj/structure/chair", 17, 3),
            Entity(3, "/obj/structure/chair", 35, 18),
        };
        WorldPlan plan = ChunkPlanner.Plan(grid, null, entities);
        Assert.Equal(new long[] { 1 }, plan.ChunkOf(3, 3, 1).Placed.Select(p => p.EntityId));
        Assert.Equal(new long[] { 2 }, plan.ChunkOf(17, 3, 1).Placed.Select(p => p.EntityId));
        Assert.Equal(new long[] { 3 }, plan.ChunkOf(35, 18, 1).Placed.Select(p => p.EntityId));
    }

    [Fact]
    public void AnEntityWithoutAPictureDrawsNothingButAShapedOneDoes()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, new[]
        {
            Entity(1, "/obj/item/pen", 3, 3, appearance: false),
            Entity(2, "/obj/structure/grille", 4, 3, appearance: false),
        });
        Assert.Equal(new long[] { 2 }, plan.Chunks[0].Placed.Select(p => p.EntityId));
    }

    [Fact]
    public void ANorthSignOnAWallIsADecalAtSignHeight()
    {
        MapGrid grid = Grid(16, 16, (x, y) => y == 10 ? Wall : Floor);
        PlanEntity sign = Entity(1, "/obj/structure/sign/departments/medbay/directional/north", 5, 9, 1, 0, 32);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, new[] { sign });
        Placed placed = plan.Chunks[0].Placed.Single();
        Assert.Equal(PlacedKind.WallDecal, placed.Kind);
        Assert.Equal(ByondDirConst.North, placed.Face);
        Assert.Equal(1.5f, placed.Height, 4); // a sign at 1.5 m on a 2 m wall (D119)
    }

    [Fact]
    public void ApcsAndButtonsSitLowerThanLights()
    {
        Assert.Equal(1.1f, PropKinds.MountHeight("/obj/machinery/power/apc/auto_name/directional/east"), 4);
        Assert.Equal(1.1f, PropKinds.MountHeight("/obj/machinery/button/door/directional/west"), 4);
        Assert.Equal(1.85f, PropKinds.MountHeight("/obj/machinery/light/directional/north"), 4);
        Assert.Equal(1.7f, PropKinds.MountHeight("/obj/machinery/camera"), 4);
        Assert.Equal(1.2f, PropKinds.MountHeight("/obj/machinery/something_else"), 4);
    }

    [Fact]
    public void AMountBesideFloorIsAPlainPicture()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        PlanEntity apc = Entity(1, "/obj/machinery/power/apc/directional/north", 5, 9, 1, 0, 32);
        Placed placed = ChunkPlanner.Plan(grid, null, new[] { apc }).Chunks[0].Placed.Single();
        Assert.Equal(PlacedKind.Billboard, placed.Kind);
    }

    [Fact]
    public void AFullWindowCountsAsAWallForAMount()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        var entities = new[]
        {
            Entity(1, "/obj/structure/window/reinforced/fulltile", 6, 5),
            Entity(2, "/obj/structure/sign/poster/random/directional/east", 5, 5, 4, 32, 0),
        };
        Placed poster = ChunkPlanner.Plan(grid, null, entities).Chunks[0].Placed.Single(p => p.EntityId == 2);
        Assert.Equal(PlacedKind.WallDecal, poster.Kind);
        Assert.Equal(ByondDirConst.East, poster.Face);
    }

    [Theory]
    [InlineData(0, 32, ByondDirConst.North)]
    [InlineData(0, -32, ByondDirConst.South)]
    [InlineData(32, 0, ByondDirConst.East)]
    [InlineData(-29, 0, ByondDirConst.West)]
    [InlineData(-32, 10, ByondDirConst.West)]
    public void ThePixelOffsetPicksTheFace(int px, int py, byte face)
    {
        bool placed = WallMounts.TryPlace("/obj/structure/extinguisher_cabinet", 5, 5, 1, 2, px, py, (x, y, z) => true, out byte got, out _, out _);
        Assert.True(placed);
        Assert.Equal(face, got);
    }

    [Fact]
    public void ASmallOffsetIsNotAMountButALightUsesItsDir()
    {
        Assert.False(WallMounts.TryPlace("/obj/structure/chair", 5, 5, 1, 2, 8, 0, (x, y, z) => true, out _, out _, out _));
        Assert.True(WallMounts.TryPlace("/obj/machinery/light/directional/west", 5, 5, 1, 8, 0, 0, (x, y, z) => true, out byte face, out float height, out _));
        Assert.Equal(ByondDirConst.West, face);
        Assert.Equal(1.85f, height, 4);
    }

    [Fact]
    public void ALightFixtureKeepsItsFaceWithoutAWall()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        PlanEntity light = Entity(1, "/obj/machinery/light/directional/north", 5, 5, 1);
        Placed placed = ChunkPlanner.Plan(grid, null, new[] { light }).Chunks[0].Placed.Single();
        Assert.Equal(PlacedKind.Piece, placed.Kind);
        Assert.Equal(RenderKind.LightFixture, placed.Shape);
        Assert.Equal(ByondDirConst.North, placed.Face);
    }

    [Fact]
    public void ABorderWindowSitsOnItsDirsFace()
    {
        MapGrid grid = Grid(16, 16, (x, y) => Floor);
        PlanEntity window = Entity(1, "/obj/structure/window/spawner/directional/east", 5, 5, 4);
        Placed placed = ChunkPlanner.Plan(grid, null, new[] { window }).Chunks[0].Placed.Single();
        Assert.Equal(RenderKind.DirectionalWindow, placed.Shape);
        Assert.Equal(ByondDirConst.East, placed.Face);
    }

    [Theory]
    [InlineData("/mob/living/carbon/human", false, false)]
    [InlineData("/mob/living/basic/mouse", false, false)]
    [InlineData("/obj/item/toolbox", true, false)]
    [InlineData("/obj/item/toolbox", false, true)]
    [InlineData("/obj/structure/chair", false, true)]
    [InlineData("/obj/machinery/door/airlock", false, true)]
    [InlineData("/obj/effect/hotspot", false, false)] // M4b slice 3: flames, drawn by the entity layer
    public void OnlyANonMobWithoutACapsuleIsAStaticProp(string type, bool hasPos, bool expected) => Assert.Equal(expected, StaticPropRule.IsCandidate(type, hasPos));

    [Fact]
    public void TheFirstPositionChangeEndsTheStaticState()
    {
        Assert.False(StaticPropRule.HasMoved((5, 5, 1), 5, 5, 1));
        Assert.True(StaticPropRule.HasMoved((5, 5, 1), 6, 5, 1));
        Assert.True(StaticPropRule.HasMoved((5, 5, 1), 5, 5, 2));
    }

    [Fact]
    public void ADoorStandsAcrossTheCorridorBetweenItsWalls()
    {
        // Walls east and west of the door: its plane runs along x (NORTH); walls only north and south: along y (EAST).
        bool WestEast(int x, int y, int z) => (x, y) is (4, 5) or (6, 5);
        bool NorthSouth(int x, int y, int z) => (x, y) is (5, 4) or (5, 6);
        bool Both(int x, int y, int z) => WestEast(x, y, z) || NorthSouth(x, y, z);
        Assert.Equal(ByondDirConst.North, WallMounts.AxisFace(5, 5, 1, WestEast));
        Assert.Equal(ByondDirConst.East, WallMounts.AxisFace(5, 5, 1, NorthSouth));
        Assert.Equal(ByondDirConst.North, WallMounts.AxisFace(5, 5, 1, Both));
        Assert.Equal(ByondDirConst.North, WallMounts.AxisFace(5, 5, 1, (x, y, z) => false));
    }

    [Fact]
    public void AnAirlockInANorthSouthWallRunsAlongTheWall()
    {
        MapGrid grid = Grid(16, 16, (x, y) => x == 5 ? Wall : Floor);
        // The airlock at (5, 8): walls at (5, 9) and (5, 7).
        Placed placed = ChunkPlanner.Plan(grid, null, new[] { Entity(1, "/obj/machinery/door/airlock", 5, 8) }).Chunks[0].Placed.Single();
        Assert.Equal(PlacedKind.Piece, placed.Kind);
        Assert.Equal(RenderKind.Airlock, placed.Shape);
        Assert.Equal(ByondDirConst.East, placed.Face);
        Assert.Equal(new[] { 7, 8 }, PieceParts.PartsOf(RenderKind.Airlock));
    }

    [Fact]
    public void AChunkHasWorldBounds()
    {
        MapGrid grid = Grid(20, 20, (x, y) => Floor);
        WorldPlan plan = ChunkPlanner.Plan(grid, null, Array.Empty<PlanEntity>());
        (float minX, float minY, float minZ, float sx, float sy, float sz) = plan.ChunkOf(1, 1, 1).Bounds(20, 20);
        Assert.Equal((0f, 0f, -32f, 32f, WorldScale.WallHeight, 32f), (minX, minY, minZ, sx, sy, sz)); // one wall high: 2 m (D119)
        Assert.Equal(2f, sy);
        // The last chunk holds 4 x 4 tiles: 8 m, from x 32 and z -40.
        (minX, _, minZ, sx, _, sz) = plan.ChunkOf(17, 17, 1).Bounds(20, 20);
        Assert.Equal((32f, -40f, 8f, 8f), (minX, minZ, sx, sz));
    }

    // the owner (2026-11-15): a glass floor's underside was plating, so from below a glass floor looked like the plating it replaced; (2026-11-17) and
    // drawn as glass with the plating's picture it still came with its plating. A glass floor has no underside tile: its pane is seen from both sides
    [Fact]
    public void AGlassFloorHasNoUndersideTileAnyOtherFloorPlating()
    {
        Assert.False(TurfStyle.PlatingUnder("/turf/open/floor/glass"));
        Assert.False(TurfStyle.PlatingUnder("/turf/open/floor/glass/reinforced"));
        Assert.False(TurfStyle.PlatingUnder("/turf/open/openspace"));
        Assert.True(TurfStyle.PlatingUnder("/turf/open/floor/iron"));
        Assert.True(TurfStyle.PlatingUnder("/turf/closed/wall"));

        var grid = new MapGrid { Width = 2, Height = 1, Levels = 2 };
        grid.TurfTypes.AddRange(new[] { Space, "/turf/open/floor/glass", Floor });
        grid.Runs.Add(new TurfRun(2, 0)); // level 1: space
        grid.Runs.Add(new TurfRun(1, 1)); // level 2: a glass floor, an iron floor
        grid.Runs.Add(new TurfRun(1, 2));
        grid.LinkedBelowLevels.Add(2);
        TileCell under = Assert.Single(ChunkPlanner.Plan(grid, null, Array.Empty<PlanEntity>()).ChunkOf(1, 1, 1).Tiles, t => t.Shape == TileShape.Ceiling);
        Assert.Equal((2, Anomaly.Core.Motion.CeilingState.Plating), (under.X, under.Ceiling.Turf));
    }
}
