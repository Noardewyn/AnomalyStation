using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using System.Numerics;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R3 (D121, the owner's addition during the second walk: "a wall seen from the side may have a different texture; the sides need one, the horizontal one"): every wall draws its
/// type's straight east-west run (<c>&lt;base&gt;-12</c>) on its side faces, whatever junction its appearance carries, by the same rule (<see cref="StraightRun"/>) that gives the
/// windows their one picture; the top keeps the turf's own picture.
/// </summary>
public sealed class WalkFix2WallPictureTests : WalkFixBase
{
    private const uint Red = 0xFF0000FF, Green = 0x00FF00FF, Blue = 0x0000FFFF, Yellow = 0xFFFF00FF;

    public WalkFix2WallPictureTests(ITestOutputHelper output) : base(output)
    {
    }

    private static (IconLibrary Library, TestTables Tables) Walls()
    {
        var library = new IconLibrary(new MemoryIconFiles());
        // Every junction picture differs; only wall-12 (EAST | WEST) is the straight run.
        var wall = new FakeIcon().Solid("wall-0", Red).Solid("wall-5", Green).Solid("wall-255", Yellow).Solid("wall-12", Blue).Solid("wall", Red);
        var reinforced = new FakeIcon().Solid("reinforced_wall-3", Green).Solid("reinforced_wall-12", Yellow);
        var bare = new FakeIcon().Solid("rock-4", Green); // no straight run in this icon
        library.Register("icons/turf/walls/wall.dmi", wall);
        library.Register("icons/turf/walls/reinforced_wall.dmi", reinforced);
        library.Register("icons/turf/walls/bare.dmi", bare);
        return (library, new TestTables());
    }

    private static uint Pixel(Raster raster) => raster.Get(0, 0);

    [Theory]
    [InlineData("wall-0")]
    [InlineData("wall-5")]
    [InlineData("wall-255")]
    [InlineData("wall-12")]
    public void WallsWithDifferentJunctionsShowTheSameSidePicture(string state)
    {
        (IconLibrary library, TestTables tables) = Walls();
        TableRef appearance = tables.Add("icons/turf/walls/wall.dmi", state);
        Assert.True(StraightRun.TryGetPicture(tables, appearance, library, out Raster side));
        Assert.Equal(Blue, Pixel(side)); // wall-12, whichever state the turf's appearance has
        Assert.True(StraightRun.TryFind(tables, appearance, library, out string icon, out string straight));
        Assert.Equal(("icons/turf/walls/wall.dmi", "wall-12"), (icon, straight));
    }

    [Fact]
    public void EveryWallTypeKeepsItsOwnPictureAndAWindowTakesTheSameRule()
    {
        (IconLibrary library, TestTables tables) = Walls();
        TableRef plain = tables.Add("icons/turf/walls/wall.dmi", "wall-5"), reinforced = tables.Add("icons/turf/walls/reinforced_wall.dmi", "reinforced_wall-3");
        Assert.True(StraightRun.TryGetPicture(tables, plain, library, out Raster plainSide));
        Assert.True(StraightRun.TryGetPicture(tables, reinforced, library, out Raster reinforcedSide));
        Assert.Equal((Blue, Yellow), (Pixel(plainSide), Pixel(reinforcedSide)));
        // The windows use the very same function (MapRenderer.TryWindowPicture calls StraightRun.TryFind): a window's states are base-junction too.
        library.Register("icons/obj/smooth_structures/window.dmi", new FakeIcon().Solid("window-0", Red).Solid("window-12", Green));
        TableRef window = tables.Add("icons/obj/smooth_structures/window.dmi", "window-9");
        Assert.True(StraightRun.TryFind(tables, window, library, out string icon, out string state));
        Assert.Equal(("icons/obj/smooth_structures/window.dmi", "window-12"), (icon, state));
    }

    [Fact]
    public void ATurfWithoutAStraightRunKeepsItsOwnPicture()
    {
        (IconLibrary library, TestTables tables) = Walls();
        Assert.False(StraightRun.TryFind(tables, tables.Add("icons/turf/walls/bare.dmi", "rock-4"), library, out _, out _)); // the icon has no rock-12
        Assert.False(StraightRun.TryFind(tables, tables.Add("icons/turf/walls/wall.dmi", "wall"), library, out _, out _)); // not a base and a junction
        Assert.False(StraightRun.TryFind(tables, tables.Add("icons/turf/walls/missing.dmi", "wall-5"), library, out _, out _)); // no such icon
        Assert.False(StraightRun.TryFind(tables, TableRef.None, library, out _, out _));
        Assert.False(StraightRun.TryGetPicture(tables, tables.Add(null, "wall-5"), library, out _));
    }

    [Fact]
    public void TheSideFacesOfTheWallBoxAreMarkedAndTheTopIsNot()
    {
        List<GeoQuad> walls = WorldGeometry.TileKit(2f).Where(q => q.Part == TileKitParts.Wall).ToList();
        Assert.Equal(5, walls.Count);
        Assert.Equal(4, walls.Count(q => q.Arm == TileKitParts.SideFace && MathF.Abs(q.Normal.Y) < 0.5f));
        GeoQuad top = walls.Single(q => q.Normal == Vector3.UnitY);
        Assert.Equal(0, top.Arm); // the plan view keeps the turf's own junction picture
        Assert.All(WorldGeometry.TileKit(2f).Where(q => q.Part is not (TileKitParts.Wall or TileKitParts.DiagonalWall)), q => Assert.Equal(0, q.Arm));
    }

    [Theory]
    [InlineData("shuttle_wall-5-diagonal", "shuttle_wall-12")]
    [InlineData("shuttle_wall-137-diagonal", "shuttle_wall-12")]
    [InlineData("shuttle_wall-5", "shuttle_wall-12")]
    public void ADiagonalCornerWallShowsTheStraightRunOnItsSides(string state, string straight) => Assert.Equal(straight, Smoothing.StraightRunState(state));

    [Theory]
    [InlineData(5, 0)]
    [InlineData(21, 0)]
    [InlineData(6, 1)]
    [InlineData(10, 2)]
    [InlineData(9, 3)]
    [InlineData(12, -1)]
    public void ADiagonalCornerWallTurnsAQuarterPerJunction(int junction, int turns) => Assert.Equal(turns, TileKitParts.TurnsOf(junction));

    [RealIconsFact]
    public void EveryWallOfMetaHasOneSidePicturePerTypeWhateverItsJunction()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        IconLibrary library = RealIcons.NewLibrary();
        var tables = new TableSource(world.Map.Tables);
        MapGrid grid = world.Map.MapGrid;
        ushort[] cells = grid.ExpandCells();
        // The distinct turf appearances of Meta's closed turfs, by (icon, state): many junctions of each wall type.
        var kinds = grid.TurfTypes.Select(TurfStyle.Classify).ToArray();
        var appearancesByType = new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);
        var runs = grid.AppearanceRuns;
        int cell = 0;
        foreach (TurfRun run in runs)
        {
            for (int i = 0; i < run.Count; i++, cell++)
            {
                if (kinds[cells[cell]] != TurfKind.Closed || run.TypeIndex == 0)
                    continue;
                string type = grid.TurfTypes[cells[cell]];
                if (!appearancesByType.TryGetValue(type, out HashSet<uint> set))
                    appearancesByType[type] = set = new HashSet<uint>();
                set.Add((uint)run.TypeIndex);
            }
        }
        int junctions = 0;
        foreach ((string type, HashSet<uint> appearances) in appearancesByType)
        {
            var sidePictures = new HashSet<string>();
            foreach (uint index in appearances)
            {
                TableRef appearance = TableRef.Public(index);
                Assert.True(StraightRun.TryGetPicture(tables, appearance, library, out Raster side), $"{type}: appearance {index} has no straight run");
                sidePictures.Add(Convert.ToBase64String(side.Pixels));
                junctions++;
            }
            _output.WriteLine($"{type}: {appearances.Count} junction pictures, {sidePictures.Count} side picture(s)");
            Assert.Single(sidePictures);
        }
        Assert.True(junctions > 20, "Meta's walls have many junctions: " + junctions);
        Assert.Contains(appearancesByType.Keys, t => t.StartsWith("/turf/closed/wall/r_wall", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(40, 4)] // tg's basalt rock: smoothrocks.dmi, TRANSLATE_MATRIX(-4, -4)
    [InlineData(48, 8)] // its siderite and shale rock: TRANSLATE_MATRIX(-8, -8)
    public void ARockLargerThanATileShowsItsTileSquareOnItsSidesWithNoTransparentMargin(int size, int shift)
    {
        // The owner's report (2026-11-02): the rocks' sides had see-through strips along their edges, the margin around the tile the bottom-left cut took in.
        var picture = new Raster(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (x >= shift && x < shift + 32 && y >= shift && y < shift + 32)
                    picture.Set(x, y, x == shift && y == shift ? Red : Blue);
        var tables = new TestTables();
        TableRef rock = tables.Add("icons/turf/smoothrocks.dmi", "smoothrocks-12", e => e.Transform = new float[] { 1, 0, -shift, 0, 1, -shift });
        Raster square = StraightRun.TileSquare(tables, rock, picture, 32);
        Assert.Equal((32, 32), (square.Width, square.Height));
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                Assert.Equal(255u, square.Get(x, y) & 0xFF);
        Assert.Equal(Red, square.Get(0, 0));
        // a picture with no move is cut from its bottom-left corner, as before (here that corner is the empty margin)
        TableRef plain = tables.Add("icons/turf/walls/wall.dmi", "wall-12");
        Assert.Equal(0u, StraightRun.TileSquare(tables, plain, picture, 32).Get(0, 0) & 0xFF);
    }

    private sealed class TableSource : IAppearanceSource
    {
        private readonly AppearanceTables _tables;

        public TableSource(AppearanceTables tables) => _tables = tables;

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        {
            entry = reference.IsNone || reference.Index > _tables.Appearances.Count ? null : _tables.Appearances[(int)reference.Index - 1];
            return entry != null;
        }

        public string ResolveString(TableRef reference) => reference.IsNone || reference.Index > _tables.Strings.Count ? null : _tables.Strings[(int)reference.Index - 1];

        public string ResolveResource(TableRef reference) => reference.IsNone || reference.Index > _tables.Resources.Count ? null : _tables.Resources[(int)reference.Index - 1];
    }
}
