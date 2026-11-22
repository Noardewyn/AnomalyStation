using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// The map editor's shapes and helpers (docs/ai/specs/map-editor.md 5.5): the tiles of a line, a rectangle, a circle and a zone; a selection
/// copied, pasted and moved; the pieces of a run turned along it; a wall mount's type by its wall; the search.
/// </summary>
public sealed class MapShapesTests
{
    private const string Space = "/turf/open/space/basic | /area/space";
    private const string Floor = "/turf/open/floor/iron | /area/station/hallway";

    // A 8 x 6 map with a room: walls around the floor of (2..5, 2..4), a table and a wrench on (3, 3), a second floor type on (5, 4).
    private static MapDocument Room()
    {
        MapDocument map = MapDocument.Create(8, 6, 1, Space);
        var tiles = new List<(int, int, int, MapModel)>();
        for (int y = 1; y <= 5; y++)
        {
            for (int x = 1; x <= 6; x++)
                tiles.Add((x, y, 1, map.Model(x is 1 or 6 || y is 1 or 5 ? "/turf/closed/wall | /area/station/hallway" : Floor)));
        }
        tiles.Add((3, 3, 1, map.Model("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | " + Floor)));
        tiles.Add((5, 4, 1, map.Model("/turf/open/floor/wood | /area/station/hallway")));
        map.Edit("room", tiles);
        return map;
    }

    private static string Drawn(IEnumerable<(int X, int Y)> cells, int width, int height)
    {
        var set = new HashSet<(int, int)>(cells);
        var rows = new List<string>();
        for (int y = height; y >= 1; y--)
            rows.Add(string.Concat(Enumerable.Range(1, width).Select(x => set.Contains((x, y)) ? '#' : '.')));
        return string.Join("\n", rows);
    }

    [Fact]
    public void ALineGoesFromTheFirstTileToTheLast()
    {
        Assert.Equal(new[] { (2, 2), (3, 2), (4, 3), (5, 3), (6, 4) }, MapShapes.Line(2, 2, 6, 4));
        Assert.Equal(new[] { (6, 4), (5, 4), (4, 3), (3, 3), (2, 2) }, MapShapes.Line(6, 4, 2, 2));
        Assert.Equal(new[] { (3, 3) }, MapShapes.Line(3, 3, 3, 3));
        Assert.Equal(new[] { (1, 4), (1, 3), (1, 2) }, MapShapes.Line(1, 4, 1, 2));

        // a run's line: each tile shares a side with the one before it
        List<(int X, int Y)> run = MapShapes.Line(1, 1, 4, 3, orthogonal: true);
        Assert.Equal((1, 1), run[0]);
        Assert.Equal((4, 3), run[^1]);
        for (int i = 1; i < run.Count; i++)
            Assert.Equal(1, Math.Abs(run[i].X - run[i - 1].X) + Math.Abs(run[i].Y - run[i - 1].Y));
        Assert.Equal(run.Count, run.Distinct().Count());
    }

    [Fact]
    public void ARectangleAndACircleAreWholeOrTheirBorder()
    {
        Assert.Equal("####\n#..#\n####", Drawn(MapShapes.Box(4, 1, 1, 3, filled: false), 4, 3));
        Assert.Equal(12, MapShapes.Box(1, 1, 4, 3, filled: true).Count);
        Assert.Equal(new[] { (2, 2) }, MapShapes.Box(2, 2, 2, 2, filled: false));

        Assert.Equal(".###.\n#####\n#####\n#####\n.###.", Drawn(MapShapes.Circle(1, 1, 5, 5, filled: true), 5, 5));
        Assert.Equal(".###.\n#...#\n#...#\n#...#\n.###.", Drawn(MapShapes.Circle(5, 5, 1, 1, filled: false), 5, 5));
        Assert.Equal(".##.\n####\n####\n.##.", Drawn(MapShapes.Circle(1, 1, 4, 4, filled: true), 4, 4));
        // an ellipse in a rectangle that is not a square, and the shapes by their names
        Assert.Equal(".#####.\n#######\n.#####.", Drawn(MapShapes.Cells(MapShape.FilledCircle, 1, 1, 7, 3), 7, 3));
        Assert.Equal(MapShapes.Box(1, 1, 3, 2, filled: true), MapShapes.Cells(MapShape.FilledBox, 1, 1, 3, 2));
        Assert.Equal(new[] { (3, 2) }, MapShapes.Cells(MapShape.Free, 1, 1, 3, 2));
    }

    [Fact]
    public void AZoneIsTheJoinedTilesOfOneTurfAndArea()
    {
        MapDocument map = Room();
        // the room's iron floor, whatever stands on it; the wood tile and the walls are not of it
        Assert.Equal("........\n........\n.###....\n.####...\n.####...\n........", Drawn(MapShapes.Zone(map, 2, 2, 1), 8, 6));
        Assert.Equal(11, MapShapes.Zone(map, 3, 3, 1).Count);
        Assert.DoesNotContain((5, 4), MapShapes.Zone(map, 2, 2, 1));
        Assert.Equal(new[] { (5, 4) }, MapShapes.Zone(map, 5, 4, 1));
        Assert.Equal(18, MapShapes.Zone(map, 1, 1, 1).Count); // the ring of walls
        // the space around: 48 tiles less the room's 30; too many for a small limit
        Assert.Equal(18, MapShapes.Zone(map, 8, 6, 1).Count);
        Assert.Null(MapShapes.Zone(map, 8, 6, 1, limit: 10));
        Assert.Empty(MapShapes.Zone(map, 9, 1, 1));
    }

    [Fact]
    public void ARectangleIsCopiedPastedAndMoved()
    {
        MapDocument map = Room();
        MapClip clip = MapEdits.Copy(map, 4, 4, 3, 3, 1);
        Assert.Equal((2, 2), (clip.Width, clip.Height));
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | " + Floor, clip.Tiles[0]); // (3, 3): the south-west tile first
        Assert.Equal(Floor, clip.Tiles[3]);

        // pasted with its south-west tile on (7, 5): the part outside the map is left out; into another map too
        Assert.Equal(new[] { (7, 5), (8, 5), (7, 6), (8, 6) }, MapEdits.Paste(map, clip, 7, 5, 1).Select(t => (t.X, t.Y)));
        Assert.Equal(new[] { (8, 6) }, MapEdits.Paste(map, clip, 8, 6, 1).Select(t => (t.X, t.Y)));
        MapDocument other = MapDocument.Create(4, 4, 1, Space);
        Assert.True(other.Edit("paste", MapEdits.Paste(other, clip, 1, 1, 1)));
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | " + Floor, other[1, 1, 1].Text);
        Assert.Equal(Space, other[3, 3, 1].Text);

        // moved one tile east: the tiles it left are the map's default, the ones it took are its own, where the two overlap too
        Assert.True(map.Edit("move", MapEdits.Move(map, 3, 3, 4, 4, 1, 1, 0)));
        Assert.Same(map.Default, map[3, 3, 1]);
        Assert.Same(map.Default, map[3, 4, 1]);
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | " + Floor, map[4, 3, 1].Text);
        Assert.Equal(Floor, map[5, 4, 1].Text); // the wood tile gave its place
        Assert.True(map.Undo());
        Assert.Equal("/turf/open/floor/wood | /area/station/hallway", map[5, 4, 1].Text);
        Assert.Empty(MapEdits.Move(map, 3, 3, 4, 4, 1, 0, 0));

        // one thing: copied, it is one more thing of the tile it is pasted on; moved, it leaves its tile
        MapClip wrench = MapClip.Of(map[3, 3, 1].Members[1]);
        Assert.Equal("/obj/item/wrench{pixel_x = 4} | " + Floor, MapEdits.Paste(map, wrench, 2, 2, 1).Single().Model.Text);
        Assert.True(map.Edit("move thing", MapEdits.MoveThing(map, (3, 3, 1), 0, (2, 4, 1))));
        Assert.Equal("/obj/item/wrench{pixel_x = 4} | " + Floor, map[3, 3, 1].Text);
        Assert.Equal("/obj/structure/table | " + Floor, map[2, 4, 1].Text);
        Assert.Empty(MapEdits.MoveThing(map, (2, 4, 1), 1, (3, 3, 1))); // a turf does not move
        Assert.Empty(MapEdits.MoveThing(map, (2, 4, 1), 0, (2, 4, 1)));

        Assert.Equal(Floor, MapEdits.ClearThings(map, map[2, 4, 1]).Text);
        Assert.Same(map[2, 2, 1], MapEdits.ClearThings(map, map[2, 2, 1]));
    }

    [Fact]
    public void ARunsPiecesTurnAlongIt()
    {
        var segment = new MapMember("/obj/structure/disposalpipe/segment", null);
        Assert.True(MapEdits.IsRunPiece(segment.Path));
        Assert.True(MapEdits.IsRunPiece("/obj/machinery/atmospherics/pipe/heat_exchanging/simple/layer2"));
        Assert.False(MapEdits.IsRunPiece("/obj/structure/disposalpipe/junction"));
        Assert.False(MapEdits.IsRunPiece("/obj/structure/cable"));

        // east, east, then north: straight pieces along the way, and tg's diagonal dir at the bend (the west and the north sides: 9)
        string[] dirs = MapEdits.RunPieces(segment, new[] { (1, 1), (2, 1), (3, 1), (3, 2), (3, 3) }).Select(p => MapEdits.VarOf(p, "dir")).ToArray();
        Assert.Equal(new[] { "4", "4", "9", "1", "2" }, dirs);
        // the four bends by the sides they join
        Assert.Equal("5", MapEdits.VarOf(MapEdits.RunPieces(segment, new[] { (2, 2), (1, 2), (1, 3) })[1], "dir")); // east and north
        Assert.Equal("6", MapEdits.VarOf(MapEdits.RunPieces(segment, new[] { (2, 2), (1, 2), (1, 1) })[1], "dir")); // east and south
        Assert.Equal("10", MapEdits.VarOf(MapEdits.RunPieces(segment, new[] { (1, 2), (2, 2), (2, 1) })[1], "dir")); // west and south
        // a lone piece is the brush as it is, its own vars kept on a turned one
        Assert.Null(MapEdits.VarOf(MapEdits.RunPieces(segment, new[] { (4, 4) })[0], "dir"));
        MapMember named = MapEdits.WithVar(segment, "name", "\"loop\"");
        Assert.Equal("/obj/structure/disposalpipe/segment{dir = 8; name = \"loop\"}", MapDocument.TextOf(new[] { MapEdits.RunPieces(named, new[] { (2, 1), (1, 1) })[0] }));

        // a turned piece takes the place of the one the stroke placed before
        MapDocument map = Room();
        MapModel tile = MapEdits.Place(map, map[2, 2, 1], MapEdits.WithVar(segment, "dir", "4"));
        Assert.Equal("/obj/structure/disposalpipe/segment{dir = 9} | " + Floor, MapEdits.Replace(map, tile, MapEdits.WithVar(segment, "dir", "4"), MapEdits.WithVar(segment, "dir", "9")).Text);
        Assert.Same(tile, MapEdits.Replace(map, tile, segment, MapEdits.WithVar(segment, "dir", "9")));
    }

    [Fact]
    public void AWallMountTakesTheTypeOfItsWall()
    {
        Assert.Equal("/obj/machinery/light/directional/north", MapEdits.Directional("/obj/machinery/light", 1));
        Assert.Equal("/obj/machinery/light/directional/west", MapEdits.Directional("/obj/machinery/light/directional/north", 8));
        Assert.Equal("/obj/machinery/firealarm/directional/south", MapEdits.Directional("/obj/machinery/firealarm/directional/east", 2));
        Assert.Null(MapEdits.Directional("/obj/structure/table", 1)); // a family with no such types
        Assert.Null(MapEdits.Directional("/obj/machinery/light", 0));
        Assert.Null(MapEdits.Directional("/obj/machinery/light", 5));

        // the mount keeps its own vars but the ones the directional type sets
        MapMember alarm = MapDocument.Parse("anomaly-map 1\nsize 1 1 1\nchunk 32\ndefault /obj/machinery/firealarm/directional/east{dir = 4; name = \"lobby alarm\"; pixel_x = 26} | /turf/open/space | /area/space\n", "member").Default.Members[0];
        Assert.Equal("/obj/machinery/firealarm/directional/north{name = \"lobby alarm\"}", MapDocument.TextOf(new[] { MapEdits.Mounted(alarm, 1) }));
        // not against a wall, and a thing that is no wall furniture: as it is
        Assert.Equal(alarm, MapEdits.Mounted(alarm, 0));
        var table = new MapMember("/obj/structure/table", null);
        Assert.Equal(table, MapEdits.Mounted(table, 4));
    }

    [Fact]
    public void ASearchFindsTheMembersThatHoldEveryWord()
    {
        MapDocument map = Room();
        List<MapFound> found = MapSearch.Find(map, "wrench", null, 10, out int total);
        Assert.Equal(new[] { new MapFound(3, 3, 1, 1) }, found);
        Assert.Equal(1, total);

        // by a var the map sets, by more than one word, whatever the case; by the type's name
        Assert.Single(MapSearch.Find(map, "pixel_x = 4", null, 10, out _));
        Assert.Single(MapSearch.Find(map, "ITEM pixel_x", null, 10, out _));
        Assert.Empty(MapSearch.Find(map, "wrench table", null, 10, out _));
        Assert.Equal(new[] { new MapFound(3, 3, 1, 0) }, MapSearch.Find(map, "dining", path => path == "/obj/structure/table" ? "Dining table" : null, 10, out _));

        // more than the list takes: the first of them in the map's order, and the count of all (the 18 walls)
        found = MapSearch.Find(map, "/turf/closed/wall", null, 3, out total);
        Assert.Equal(new[] { new MapFound(1, 1, 1, 0), new MapFound(2, 1, 1, 0), new MapFound(3, 1, 1, 0) }, found);
        Assert.Equal(18, total);
        Assert.Empty(MapSearch.Find(map, "  ", null, 10, out total));
        Assert.Equal(0, total);
    }
}
