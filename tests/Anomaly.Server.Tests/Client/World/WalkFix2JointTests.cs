using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R3 (D121, the owner's second walk: "windows and the other flat walls need corner pieces where they meet at an angle", "use ONE window picture"): a full-tile window
/// or grille is a centre post and an arm toward each neighbour it joins; directional windows, railings and border firelocks that meet at a corner get a post; every window
/// draws the straight east-west state of its type.
/// </summary>
public sealed class WalkFix2JointTests : WalkFixBase
{
    private const byte N = Joints.North, S = Joints.South, E = Joints.East, W = Joints.West;

    public WalkFix2JointTests(ITestOutputHelper output) : base(output)
    {
    }

    private static byte Arms(AsciiWorld world, int x, int y, RenderKind shape = RenderKind.FullWindow) => world.Piece(x, y, shape).Joints;

    [Fact]
    public void AStraightRunOfWindowsJoinsEastAndWestAndItsEndsStillSpanTheirTile()
    {
        var world = new AsciiWorld(new[]
        {
            ".......",
            "..WWW..",
            ".......",
        });
        foreach (int x in new[] { 3, 4, 5 })
            Assert.Equal(E | W, Arms(world, x, 2));
        // And a run along y.
        var tall = new AsciiWorld(new[]
        {
            "...",
            ".W.",
            ".W.",
            ".W.",
            "...",
        });
        foreach (int y in new[] { 2, 3, 4 })
            Assert.Equal(N | S, Arms(tall, 2, y));
    }

    [Fact]
    public void ACornerIsAnLShapeAndTJunctionsAndCrossesJoinEveryNeighbour()
    {
        // (3,2) (4,2) and (4,3): the corner at (4,2) has a neighbour west and one north: an L. The ends complete their axis.
        var corner = new AsciiWorld(new[]
        {
            "......",
            "...W..",
            "..WW..",
            "......",
        });
        Assert.Equal(W | N, Arms(corner, 4, 2)); // y counts from the bottom: rows are y = 4..1, so (4,2) is the corner below (4,3)
        Assert.Equal(S | N, Arms(corner, 4, 3));
        Assert.Equal(E | W, Arms(corner, 3, 2));
        // A T: three neighbours.
        var tee = new AsciiWorld(new[]
        {
            "......",
            "...W..",
            "..WWW.",
            "......",
        });
        Assert.Equal(W | E | N, Arms(tee, 4, 2));
        // A cross.
        var cross = new AsciiWorld(new[]
        {
            ".....",
            "..W..",
            ".WWW.",
            "..W..",
            ".....",
        });
        Assert.Equal(N | S | E | W, Arms(cross, 3, 3));
    }

    [Fact]
    public void AWindowNextToAWallOrADoorFrameClosesTheGapToIt()
    {
        var world = new AsciiWorld(new[]
        {
            ".....",
            ".#W#.",
            ".....",
            ".AW..",
            ".....",
        });
        // (3,4) has a wall west and a wall east: it spans the tile east-west and reaches both.
        Assert.Equal(E | W, Arms(world, 3, 4));
        // (3,2): an airlock west of it (a door frame joins), floor east: east and west.
        Assert.Equal(E | W, Arms(world, 3, 2));
        // A window whose only neighbour is north (a wall) joins it and, as the end of a run, spans its tile north-south.
        var end = new AsciiWorld(new[]
        {
            "..#..",
            "..W..",
            ".....",
        });
        Assert.Equal(N | S, Arms(end, 3, 2));
    }

    [Fact]
    public void AFirelockOrATrayIsNotAFrameAWindowJoins()
    {
        var world = new AsciiWorld(new[]
        {
            ".....",
            ".FWT.",
            ".....",
        });
        // A firelock stands in a hall beside the frames; a tray is furniture: neither joins, so the window is alone and takes the axis the door rule gives it (the tray and
        // the firelock block east and west of it: the way through is north-south, the plane along x, so its arms run east and west).
        Assert.Equal(E | W, Arms(world, 3, 2));
    }

    [Fact]
    public void AWindowWithNoNeighbourTakesTheAxisOfTheDoorRule()
    {
        var alone = new AsciiWorld(new[]
        {
            ".........",
            ".........",
            ".........",
            ".........",
            "....W....",
            ".........",
            ".........",
            ".........",
            ".........",
        });
        Assert.Equal(E | W, Arms(alone, 5, 5));
        // Dense things north and south of it (trays) make the way through east-west: its plane runs along y.
        var between = new AsciiWorld(new[]
        {
            ".....",
            "..T..",
            "..W..",
            "..T..",
            ".....",
        });
        Assert.Equal(N | S, Arms(between, 3, 3));
    }

    [Fact]
    public void AGrilleJoinsLikeAWindowAndALoneGrilleBesideAWindowJoinsItToo()
    {
        var world = new AsciiWorld(new[]
        {
            "......",
            "..DD..",
            "...G..",
            "......",
        });
        // (3,3) and (4,3) are window and grille each; (4,2) a grille alone below (4,3).
        Assert.Equal(E | W, Arms(world, 3, 3));
        Assert.Equal(W | S, world.Piece(4, 3, RenderKind.Grille).Joints);
        Assert.Equal(W | S, Arms(world, 4, 3));
        Assert.Equal(N | S, world.Piece(4, 2, RenderKind.Grille).Joints);
    }

    [Fact]
    public void AWindowAndItsGrilleInTheSameCellHaveTheSameArmsAndNoFace()
    {
        var world = new AsciiWorld(new[]
        {
            ".......",
            ".DDDD..",
            ".......",
        });
        for (int x = 2; x <= 5; x++)
        {
            Placed window = world.Piece(x, 2, RenderKind.FullWindow), grille = world.Piece(x, 2, RenderKind.Grille);
            Assert.Equal(window.Joints, grille.Joints);
            Assert.Equal(0, window.Face); // joined by arms in world directions, never turned
        }
    }

    // ---- border pieces ----

    private static PlanEntity Border(long id, string type, int x, int y, byte dir) => new(id, type, x, y, 1, dir, TableRef.Public((uint)id), 0, 0, LightState.None, null);

    private static Dictionary<long, Placed> PlanBorders(params PlanEntity[] entities)
    {
        MapGrid grid = Grid(8, 8);
        return ChunkPlanner.Plan(grid, null, entities).Chunks.SelectMany(c => c.Placed).ToDictionary(p => p.EntityId);
    }

    private const string DirWindow = "/obj/structure/window/spawner/directional/north", Rail = "/obj/structure/railing", FirelockEdge = "/obj/machinery/door/firedoor/border_only";

    [Fact]
    public void TwoBorderWindowsMeetingAtACornerInOneTileGetAPostAtTheCorner()
    {
        // A window on the north edge and one on the east edge of (4,4): they meet at the north-east corner. The north piece's east end is its HIGH end; the east piece's north end is its LOW end
        // (a piece is built for the north edge and turned: its +x is east for a north face, south for an east face).
        Dictionary<long, Placed> placed = PlanBorders(Border(1, DirWindow, 4, 4, 1), Border(2, DirWindow, 4, 4, 4));
        Assert.Equal(BorderCorners.HighEnd, placed[1].Joints);
        Assert.Equal(BorderCorners.LowEnd, placed[2].Joints);
    }

    [Fact]
    public void BorderPiecesInNeighbouringTilesThatMeetAtACornerGetAPostToo()
    {
        // The north edge of (4,4) and the east edge of the tile north of it, (4,5): both end at the corner (4,4)'s north-east, where the east piece's SOUTH end (its high end) is.
        Dictionary<long, Placed> placed = PlanBorders(Border(1, DirWindow, 4, 4, 1), Border(2, DirWindow, 4, 5, 4));
        Assert.Equal(BorderCorners.HighEnd, placed[1].Joints);
        Assert.Equal(BorderCorners.HighEnd, placed[2].Joints);
    }

    [Fact]
    public void TheRailingAndTheBorderFirelockGetPostsLikeTheWindow()
    {
        // A railing on the south edge of (4,4) and a border firelock on its west edge meet at the south-west corner: the railing's high end (west) and the firelock's low end (south).
        Dictionary<long, Placed> placed = PlanBorders(Border(1, Rail, 4, 4, 2), Border(2, FirelockEdge, 4, 4, 8));
        Assert.Equal(BorderCorners.HighEnd, placed[1].Joints);
        Assert.Equal(BorderCorners.LowEnd, placed[2].Joints);
        // Two pieces on tiles that only share an edge, not a corner with a piece of the other axis (the railing and a firelock of the tile west of it), do not get any.
        placed = PlanBorders(Border(1, Rail, 4, 4, 2), Border(2, FirelockEdge, 3, 4, 8));
        Assert.Equal(0, placed[1].Joints);
        Assert.Equal(0, placed[2].Joints);
    }

    [Fact]
    public void CollinearBorderPiecesAndALoneOneGetNoPosts()
    {
        Dictionary<long, Placed> placed = PlanBorders(Border(1, DirWindow, 3, 4, 1), Border(2, DirWindow, 4, 4, 1), Border(3, DirWindow, 5, 4, 1));
        Assert.All(placed.Values, p => Assert.Equal(0, p.Joints));
        placed = PlanBorders(Border(1, DirWindow, 4, 4, 1));
        Assert.Equal(0, placed[1].Joints);
        // A row along the west edges likewise.
        placed = PlanBorders(Border(1, DirWindow, 4, 3, 8), Border(2, DirWindow, 4, 4, 8));
        Assert.All(placed.Values, p => Assert.Equal(0, p.Joints));
    }

    [Fact]
    public void AWindoorIsAPartnerAtACornerButGetsNoPostOfItsOwn()
    {
        Dictionary<long, Placed> placed = PlanBorders(Border(1, DirWindow, 4, 4, 1), Border(2, "/obj/machinery/door/window/left", 4, 4, 4));
        Assert.Equal(BorderCorners.HighEnd, placed[1].Joints); // the window gets a post where the windoor meets it
        Assert.Equal(0, placed[2].Joints);
        Assert.False(BorderCorners.GetsPosts(RenderKind.Windoor));
    }

    [Fact]
    public void TheEndsOfABorderPieceAreTheCornersItsFaceGives()
    {
        // Doubled coordinates: the north-east corner of tile (4, 4) is (9, 9).
        Assert.Equal(((7, 9), (9, 9)), BorderCorners.Ends(4, 4, ByondDirConst.North));
        Assert.Equal(((9, 7), (7, 7)), BorderCorners.Ends(4, 4, ByondDirConst.South));
        Assert.Equal(((9, 9), (9, 7)), BorderCorners.Ends(4, 4, ByondDirConst.East));
        Assert.Equal(((7, 7), (7, 9)), BorderCorners.Ends(4, 4, ByondDirConst.West));
    }

    // ---- one picture per window type ----

    [Theory]
    [InlineData("window-0", "window-12")]
    [InlineData("window-13", "window-12")]
    [InlineData("window-255", "window-12")]
    [InlineData("reinforced_window-5", "reinforced_window-12")]
    [InlineData("plasma_window-1", "plasma_window-12")]
    [InlineData("tinted_window-12", "tinted_window-12")]
    public void EveryJunctionStateOfAWindowDrawsTheStraightEastWestRun(string state, string expected) => Assert.Equal(expected, Smoothing.StraightRunState(state));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("window")]
    [InlineData("window-")]
    [InlineData("window-damaged")]
    [InlineData("-12")]
    public void AStateThatIsNotABaseAndAJunctionHasNoStraightRun(string state) => Assert.Null(Smoothing.StraightRunState(state));

    [Fact]
    public void TheJunctionBitsAreTgs()
    {
        // code/__DEFINES/icon_smoothing.dm: NORTH 1, SOUTH 2, EAST 4, WEST 8; the straight east-west run is EAST | WEST.
        Assert.Equal(12, Smoothing.EastWest);
        Assert.Equal("wall-12", Smoothing.StateOf("wall", Smoothing.EastWest));
    }

    [RealIconsFact]
    public void EveryWindowTypeOfMetaHasItsStraightRunInItsIcon()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        IconLibrary library = RealIcons.NewLibrary();
        var checkedStates = new SortedSet<string>(StringComparer.Ordinal);
        foreach (FakeMapEntity window in world.Map.Entities.Where(e => PropKinds.IsFullTileWindow(e.TypePath)))
        {
            string straight = Smoothing.StraightRunState(window.IconState);
            Assert.NotNull(straight);
            Assert.True(library.TryGetIcon(window.Icon, out IIcon icon) && icon.TryGetState(straight, out _), $"{window.TypePath}: {window.Icon} has no state {straight}");
            checkedStates.Add(window.Icon + "#" + straight);
        }
        _output.WriteLine(string.Join("\n", checkedStates));
        Assert.True(checkedStates.Count >= 2, "Meta has plain and reinforced windows at least");
    }

    // ---- MetaStation ----

    [Fact]
    public void EveryWindowOfMetaJoinsEveryWindowGrilleWallAndFrameBesideItAndItsNeighboursJoinBack()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        var windowCells = world.Map.Entities.Where(e => PropKinds.IsFullTileWindow(e.TypePath)).Select(e => (e.X, e.Y)).ToHashSet();
        // M5 slice 5: a broken grille is tg's sprite standing in its tile (PropKinds.Classify with its state), not a joined piece
        bool Whole(FakeMapEntity e) => PropKinds.IsGrille(e.TypePath) && world.Placed[e.Id].Kind == PlacedKind.Piece;
        var grilleCells = world.Map.Entities.Where(Whole).Select(e => (e.X, e.Y)).ToHashSet();
        var frameCells = world.Map.Entities.Where(e => PropKinds.Classify(e.TypePath) is RenderKind.Airlock or RenderKind.PodDoor).Select(e => (e.X, e.Y)).ToHashSet();
        bool Joins(int x, int y) => world.IsWallOrWindow(x, y) || windowCells.Contains((x, y)) || grilleCells.Contains((x, y)) || frameCells.Contains((x, y));
        int windows = 0, corners = 0;
        int broken = world.Map.Entities.Count(e => PropKinds.IsGrille(e.TypePath) && !Whole(e));
        foreach (FakeMapEntity e in world.Map.Entities.Where(e => PropKinds.IsFullTileWindow(e.TypePath) || Whole(e)))
        {
            Placed p = world.Placed[e.Id];
            windows++;
            Assert.Equal(PlacedKind.Piece, p.Kind);
            // Every neighbour that joins has its arm.
            foreach ((int dx, int dy, byte arm) in new[] { (0, 1, N), (0, -1, S), (1, 0, E), (-1, 0, W) })
            {
                if (Joins(e.X + dx, e.Y + dy))
                    Assert.True((p.Joints & arm) != 0, $"{e.TypePath} at ({e.X},{e.Y}) does not reach its neighbour on arm {arm}");
            }
            // And never fewer than two arms: a run's end still spans its tile, a corner has its two.
            Assert.True(int.PopCount(p.Joints) >= 2, $"({e.X},{e.Y}) {e.TypePath}: arms {p.Joints}");
            if ((p.Joints & (E | W)) != 0 && (p.Joints & (N | S)) != 0)
                corners++;
        }
        _output.WriteLine($"{windows} windows and grilles, {corners} of them corners, tees or crosses; {broken} broken grilles drawn as tg's sprite");
        Assert.True(windows > 800, "Meta has hundreds of windows with grilles: " + windows);
        Assert.True(corners > 20, "there are window corners: " + corners);
    }
}
