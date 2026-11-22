using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// The map editor's quarter turns, its copies of several rectangles and the clip's text for the system's clipboard
/// (docs/ai/specs/map-editor.md 5.14).
/// </summary>
public sealed class MapTurnsTests
{
    private const string Space = "/turf/open/space/basic | /area/space";
    private const string Hall = " | /turf/open/floor/iron | /area/station/hallway/primary/central";

    private static MapDocument Map(int width, int height, params (int X, int Y, string Members)[] tiles)
    {
        MapDocument map = MapDocument.Create(width, height, 1, Space);
        map.Edit("Start", tiles.Select(t => (t.X, t.Y, 1, map.Model(t.Members))));
        return map;
    }

    private static MapMember Member(string text) => MapDocument.Create(1, 1, 1, text).Default.Members[0];

    private static string Text(MapMember member) => MapDocument.TextOf(new[] { member });

    [Fact]
    public void ADirTurnsAQuarter()
    {
        Assert.Equal(new[] { 4, 2, 8, 1 }, new[] { 1, 4, 2, 8 }.Select(dir => MapTurns.Dir(dir, clockwise: true)));
        Assert.Equal(new[] { 8, 1, 4, 2 }, new[] { 1, 4, 2, 8 }.Select(dir => MapTurns.Dir(dir, clockwise: false)));
        Assert.Equal(6, MapTurns.Dir(5, clockwise: true)); // north-east to south-east
        Assert.Equal(8, MapTurns.Dir(0, clockwise: true)); // what is no dir counts as south
    }

    [Fact]
    public void AMemberTurnsByItsDirItsWallOrTheWayItIsSeenToFace()
    {
        // a dir of its own turns; with none, the type's (south) does and is written, so the game's own rules leave the thing as the mapper turned it
        Assert.Equal("/obj/structure/chair{dir = 2}", Text(MapTurns.Turned(Member("/obj/structure/chair{dir = 4}"), clockwise: true)));
        Assert.Equal("/obj/structure/chair{dir = 8}", Text(MapTurns.Turned(Member("/obj/structure/chair"), clockwise: true)));
        Assert.Equal("/obj/structure/chair{dir = 4}", Text(MapTurns.Turned(Member("/obj/structure/chair"), clockwise: false)));
        // the game shows a closet turned away from its wall, east: a turn goes on from there
        Assert.Equal("/obj/structure/closet{dir = 2}", Text(MapTurns.Turned(Member("/obj/structure/closet"), clockwise: true, facing: 4)));
        // (what the map says itself wins over what is seen)
        Assert.Equal("/obj/structure/closet{dir = 4}", Text(MapTurns.Turned(Member("/obj/structure/closet{dir = 1}"), clockwise: true, facing: 8)));
        // a thing hung on a wall of its tile by its type: the type of the next wall, and no dir of its own
        Assert.Equal("/obj/machinery/light/directional/east", Text(MapTurns.Turned(Member("/obj/machinery/light/directional/north"), clockwise: true)));
        Assert.Equal("/obj/machinery/light/directional/west", Text(MapTurns.Turned(Member("/obj/machinery/light/directional/north"), clockwise: false)));
        // pixel offsets turn with the member: 8 to the east is 8 to the south
        Assert.Equal("/obj/item/pen{dir = 8; pixel_y = -8}", Text(MapTurns.Turned(Member("/obj/item/pen{pixel_x = 8}"), clockwise: true)));
        // a turf turns the dir it has, an area nothing
        Assert.Equal("/turf/open/floor/iron", Text(MapTurns.Turned(Member("/turf/open/floor/iron"), clockwise: true)));
        Assert.Equal("/turf/open/floor/iron{dir = 4}", Text(MapTurns.Turned(Member("/turf/open/floor/iron{dir = 1}"), clockwise: true)));
        Assert.Equal("/area/space", Text(MapTurns.Turned(Member("/area/space"), clockwise: true)));
    }

    [Fact]
    public void ABoxTurnsAboutItsMiddleAndComesHome()
    {
        // three tiles in a row become three in a column on the middle one; clockwise the west end is the north end
        TurnBox row = TurnBox.Of(new[] { (4, 5), (5, 5), (6, 5) }, clockwise: true);
        Assert.Equal((5, 6), row.Spot(4, 5));
        Assert.Equal((5, 5), row.Spot(5, 5));
        Assert.Equal((5, 4), row.Spot(6, 5));
        Assert.Equal((5, 4), TurnBox.Of(new[] { (4, 5), (5, 5), (6, 5) }, clockwise: false).Spot(4, 5));

        // a box with no tile in its middle (four by one) steps half a tile and back: four turns bring every tile home
        var cells = new List<(int X, int Y)> { (3, 5), (4, 5), (5, 5), (6, 5) };
        var at = cells.ToList();
        for (int turn = 0; turn < 4; turn++)
        {
            TurnBox box = TurnBox.Of(at, clockwise: true);
            at = at.Select(c => box.Spot(c.X, c.Y)).ToList();
        }
        Assert.Equal(cells, at);
    }

    [Fact]
    public void SelectedTilesTurnWithWhatStandsOnThem()
    {
        // a wall with a lamp on its south side, a chair that faces it, and a floor beside them
        MapDocument map = Map(7, 7,
            (3, 4, "/turf/closed/wall | /area/station/hallway/primary/central"),
            (3, 3, "/obj/machinery/light/directional/north | /obj/structure/chair{dir = 1}" + Hall),
            (4, 3, "/obj/structure/closet" + Hall));
        var cells = new[] { (3, 3), (4, 3), (3, 4), (4, 4) };
        // (the game shows the closet facing west)
        List<(int X, int Y, int Z, MapModel Model)> tiles = MapTurns.Turn(map, cells, 1, clockwise: true, (x, y, member) => (x, y, member) == (4, 3, 0) ? 8 : 0);
        Assert.True(map.Edit("Turn", tiles));

        // clockwise the north side is the east side: the wall is east of the chair's tile, the lamp hangs on it, the chair faces it
        Assert.Equal("/turf/closed/wall | /area/station/hallway/primary/central", map[4, 4, 1].Text);
        Assert.Equal("/obj/machinery/light/directional/east | /obj/structure/chair{dir = 4}" + Hall, map[3, 4, 1].Text);
        Assert.Equal("/obj/structure/closet{dir = 1}" + Hall, map[3, 3, 1].Text); // west turned is north
        Assert.Equal(Space, map[4, 3, 1].Text); // (the tile of the box that held nothing)

        // three more turns: every tile is where it was, and every thing faces as it did
        for (int turn = 0; turn < 3; turn++)
            Assert.True(map.Edit("Turn", MapTurns.Turn(map, cells, 1, clockwise: true)));
        Assert.Equal("/obj/machinery/light/directional/north | /obj/structure/chair{dir = 1}" + Hall, map[3, 3, 1].Text);
        Assert.Equal("/obj/structure/closet{dir = 8}" + Hall, map[4, 3, 1].Text);
        Assert.Equal("/turf/closed/wall | /area/station/hallway/primary/central", map[3, 4, 1].Text);

        // a turn that would reach past the map is not made
        Assert.Null(MapTurns.Turn(map, new[] { (1, 1), (2, 1), (3, 1), (4, 1), (5, 1) }, 1, clockwise: true));
    }

    [Fact]
    public void FurnitureOnOneFloorTurnsAndTheFloorStays()
    {
        // a row of three tiles of one floor, with a wall north of its middle and plating south of it
        MapDocument map = Map(7, 7,
            (3, 4, "/obj/structure/chair{dir = 4}" + Hall), (4, 4, "/obj/structure/table" + Hall), (5, 4, Hall.Substring(3)),
            (4, 5, "/obj/item/pen | /turf/closed/wall | /area/station/hallway/primary/central"), (4, 3, "/turf/open/floor/plating | /area/station/maintenance/central"));
        Assert.True(map.Edit("Turn", MapTurns.Turn(map, new[] { (3, 4), (4, 4), (5, 4) }, 1, clockwise: true)));

        // the tiles the things left keep their floor; the tiles they went to keep their own turf and area, and lose what stood on them
        Assert.Equal(Hall.Substring(3), map[3, 4, 1].Text);
        Assert.Equal(Hall.Substring(3), map[5, 4, 1].Text);
        Assert.Equal("/obj/structure/chair{dir = 2} | /turf/closed/wall | /area/station/hallway/primary/central", map[4, 5, 1].Text);
        Assert.Equal("/obj/structure/table{dir = 8}" + Hall, map[4, 4, 1].Text);
        Assert.Equal("/turf/open/floor/plating | /area/station/maintenance/central", map[4, 3, 1].Text);
    }

    [Fact]
    public void SeveralRectanglesAreCopiedMovedAndPastedAsTheyLie()
    {
        MapDocument map = Map(8, 4, (1, 1, "/obj/structure/chair" + Hall), (3, 2, "/obj/structure/table" + Hall), (2, 1, "/obj/item/pen" + Hall));
        // two tiles with one between them that is not selected
        var cells = new[] { (1, 1), (3, 2) };
        MapClip clip = MapEdits.Copy(map, cells, 1);
        Assert.Equal((3, 2, 2), (clip.Width, clip.Height, clip.Count));
        Assert.Equal(new[] { "/obj/structure/chair" + Hall, null, null, null, null, "/obj/structure/table" + Hall }, clip.Tiles);

        // pasted, the tiles between stay as they are
        Assert.True(map.Edit("Paste", MapEdits.Paste(map, clip, 5, 3, 1)));
        Assert.Equal("/obj/structure/chair" + Hall, map[5, 3, 1].Text);
        Assert.Equal("/obj/structure/table" + Hall, map[7, 4, 1].Text);
        Assert.Equal(Space, map[6, 3, 1].Text);

        // moved, the pen between them stays
        Assert.True(map.Edit("Move", MapEdits.Move(map, cells, 1, 0, 1)));
        Assert.Equal(Space, map[1, 1, 1].Text);
        Assert.Equal("/obj/structure/chair" + Hall, map[1, 2, 1].Text);
        Assert.Equal("/obj/structure/table" + Hall, map[3, 3, 1].Text);
        Assert.Equal("/obj/item/pen" + Hall, map[2, 1, 1].Text);
    }

    [Fact]
    public void AClipGoesThroughTheClipboardAsText()
    {
        MapDocument map = Map(4, 4, (1, 1, "/obj/structure/chair{dir = 4; name = \"a: b\"}" + Hall), (3, 2, "/obj/structure/table" + Hall));
        MapClip clip = MapEdits.Copy(map, new[] { (1, 1), (3, 2) }, 1);
        MapClip read = MapClip.Parse(clip.ToText());
        Assert.NotNull(read);
        Assert.Equal((clip.Width, clip.Height), (read.Width, read.Height));
        Assert.Equal(clip.Tiles, read.Tiles);

        MapClip thing = MapClip.Parse(MapClip.Of(Member("/obj/item/pen{pixel_x = 8}")).ToText());
        Assert.Equal("/obj/item/pen{pixel_x = 8}", Text(thing.Thing.Value));

        // what else a clipboard may hold is no clip
        Assert.Null(MapClip.Parse(null));
        Assert.Null(MapClip.Parse("the quick brown fox"));
        Assert.Null(MapClip.Parse("anomaly-clip 1\nsize 2 2\n5,5: /turf/open/space | /area/space\n"));
        Assert.Null(MapClip.Parse("anomaly-clip 1\nsize 2 2\n"));
    }
}
