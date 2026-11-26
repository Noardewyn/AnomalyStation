using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The map editor's changes of one tile (docs/ai/specs/map-editor.md 5.2): the members stay in the format's order.</summary>
public sealed class MapEditsTests
{
    private const string Space = "/turf/open/space/basic | /area/space";

    private static MapDocument Map() => MapDocument.Create(8, 8, 1, Space);

    private static MapMember Member(string text) => MapDocument.Parse("anomaly-map 1\nsize 1 1 1\nchunk 32\ndefault " + text + " | /turf/open/space | /area/space\n", "member").Default.Members[0];

    [Fact]
    public void AThingATurfAndAnAreaArePlacedInTheFormatsOrder()
    {
        MapDocument map = Map();
        MapModel tile = map.Default;
        tile = MapEdits.Place(map, tile, new MapMember("/turf/open/floor/iron", null));
        Assert.Equal("/turf/open/floor/iron | /area/space", tile.Text);
        tile = MapEdits.Place(map, tile, new MapMember("/obj/structure/table", null));
        tile = MapEdits.Place(map, tile, Member("/obj/item/wrench{pixel_x = 4}"));
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | /turf/open/floor/iron | /area/space", tile.Text);
        tile = MapEdits.Place(map, tile, new MapMember("/area/station/hallway", null));
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | /turf/open/floor/iron | /area/station/hallway", tile.Text);
        tile = MapEdits.Place(map, tile, new MapMember("/turf/closed/wall", null));
        Assert.Equal("/obj/structure/table | /obj/item/wrench{pixel_x = 4} | /turf/closed/wall | /area/station/hallway", tile.Text);

        // the same thing is not placed twice; the same type with other vars is another thing
        Assert.Same(tile, MapEdits.Place(map, tile, new MapMember("/obj/structure/table", null)));
        Assert.Same(tile, MapEdits.Place(map, tile, Member("/obj/item/wrench{pixel_x = 4}")));
        Assert.Equal(5, MapEdits.Place(map, tile, Member("/obj/item/wrench{pixel_x = -4}")).Members.Count);
        // a turf that is the tile's already changes nothing
        Assert.Same(tile, MapEdits.Place(map, tile, new MapMember("/turf/closed/wall", null)));
    }

    // tg's open space of space is space in any area: a room's area painted over it makes it the room's open space, which holds the room's air
    [Fact]
    public void ARoomsAreaOverOpenSpaceOfSpaceMakesItTheRoomsOpenSpace()
    {
        MapDocument map = Map();
        MapModel space = map.Model("/turf/open/space/openspace | /area/space");
        MapModel room = MapEdits.Place(map, space, new MapMember("/area/station/service/bar", null));
        Assert.Equal("/turf/open/openspace | /area/station/service/bar", room.Text);
        // the area of space, near the station too, leaves it space; and a room's open space stays as it is when the area goes
        Assert.Equal(space.Text, MapEdits.Place(map, space, new MapMember("/area/space", null)).Text);
        Assert.Equal("/turf/open/space/openspace | /area/space/nearstation", MapEdits.Place(map, space, new MapMember("/area/space/nearstation", null)).Text);
        Assert.Equal("/turf/open/openspace | /area/space", MapEdits.Unareaed(map, room).Text);
    }

    [Fact]
    public void ATilesGasIsItsTurfsInitialGasMix()
    {
        MapDocument map = Map();
        MapModel floor = map.Model("/turf/open/floor/iron | /area/space");
        Assert.Equal(GasMix.NormalAir, MapEdits.GasMixOf(floor, out bool set)); // the type's own
        Assert.False(set);
        Assert.Equal("o2=100000;TEMP=293.15", MapEdits.GasMixOf(map.Model("/turf/open/floor/engine/o2 | /area/space"), out _));

        MapModel filled = MapEdits.WithGasMix(map, floor, "plasma=500;TEMP=500");
        Assert.Equal("/turf/open/floor/iron{initial_gas_mix = \"plasma=500;TEMP=500\"} | /area/space", filled.Text);
        Assert.Equal("plasma=500;TEMP=500", MapEdits.GasMixOf(filled, out set));
        Assert.True(set);
        Assert.Equal(floor.Text, MapEdits.WithGasMix(map, filled, null).Text); // the type's own again
        MapModel wall = map.Model("/turf/closed/wall | /area/space");
        Assert.Same(wall, MapEdits.WithGasMix(map, wall, GasMix.NormalAir)); // a wall holds no air
        Assert.Null(MapEdits.GasMixOf(wall, out _));
    }

    [Fact]
    public void AGasStringIsReadAndWrittenAsTgWritesIt()
    {
        GasMix air = GasMix.Parse(GasMix.NormalAir);
        Assert.Equal(new[] { ("o2", 22.0), ("n2", 82.0) }, air.Gases);
        Assert.Equal(293.15, air.Kelvin);
        Assert.Equal(101.3, air.Kpa, 1); // a room's normal pressure
        Assert.Equal(GasMix.NormalAir, air.ToString());

        air["co2"] = 1.5;
        air["n2"] = 0; // a gas with no moles is not written
        Assert.Equal("o2=22;co2=1.5;TEMP=293.15", air.ToString());
        Assert.Empty(GasMix.Parse(GasMix.Vacuum).Gases);
        Assert.Null(GasMix.Parse("LAVALAND_ATMOS")); // a named atmosphere is no gas string
        Assert.Null(GasMix.Parse("o2=lots"));
    }

    [Fact]
    public void AnotherTurfKeepsTheCeilingTheMapSetOverTheTile()
    {
        MapDocument map = Map();
        MapModel tile = map.Model("/turf/open/floor/iron{ceiling = \"/turf/open/floor/glass\"; dir = 4} | /area/station/hallway");
        // the ceiling is the tile's, not its floor's: the other vars are the old turf's own and go with it
        Assert.Equal("/turf/open/floor/wood{ceiling = \"/turf/open/floor/glass\"} | /area/station/hallway", MapEdits.Place(map, tile, new MapMember("/turf/open/floor/wood", null)).Text);
        // a turf that says its own ceiling has it
        Assert.Equal("/turf/open/floor/wood{ceiling = \"none\"} | /area/station/hallway", MapEdits.Place(map, tile, Member("/turf/open/floor/wood{ceiling = \"none\"}")).Text);
        Assert.Equal("/turf/open/floor/wood | /area/space", MapEdits.Place(map, map.Model("/turf/open/floor/iron | /area/space"), new MapMember("/turf/open/floor/wood", null)).Text);
    }

    [Theory]
    [InlineData("/turf/closed/wall", TurfLayer.Wall)]
    [InlineData("/turf/closed/wall/r_wall", TurfLayer.Wall)]
    [InlineData("/turf/closed/indestructible/riveted", TurfLayer.Wall)]
    [InlineData("/turf/open/floor/iron", TurfLayer.Tile)]
    [InlineData("/turf/open/floor/iron/dark/airless", TurfLayer.Tile)]
    [InlineData("/turf/open/floor/glass/reinforced", TurfLayer.Tile)]
    [InlineData("/turf/open/floor/engine", TurfLayer.Tile)]
    [InlineData("/turf/open/floor/plating", TurfLayer.Base)]
    [InlineData("/turf/open/floor/plating/airless", TurfLayer.Base)]
    [InlineData("/turf/open/space/basic", TurfLayer.Base)]
    [InlineData("/turf/open/openspace", TurfLayer.Base)]
    [InlineData("/turf/open/misc/asteroid", TurfLayer.Base)]
    public void ATurfIsAWallAFloorTileOrTheBaseUnderThem(string turf, TurfLayer layer) => Assert.Equal(layer, MapEdits.LayerOf(turf));

    [Fact]
    public void TheEraseToolTakesOneLayerOfATileAway()
    {
        MapDocument map = Map();
        // a wall leaves its plating; what stands there and the area stay
        MapModel wall = map.Model("/obj/structure/sign/warning | /turf/closed/wall/r_wall | /area/station/hallway");
        MapModel bared = MapEdits.Peel(map, wall);
        Assert.Equal("/obj/structure/sign/warning | /turf/open/floor/plating | /area/station/hallway", bared.Text);
        // a floor tile leaves its plating, airless where it was, and the ceiling the map set stays over the tile
        Assert.Equal("/obj/structure/table | /turf/open/floor/plating/airless{ceiling = \"none\"} | /area/station/hallway",
            MapEdits.Peel(map, map.Model("/obj/structure/table | /turf/open/floor/iron/airless{ceiling = \"none\"; dir = 8} | /area/station/hallway")).Text);
        // plating leaves the map's default turf and area: the things stay
        MapModel hole = MapEdits.Peel(map, bared);
        Assert.Equal("/obj/structure/sign/warning | /turf/open/space/basic | /area/space", hole.Text);
        Assert.Equal("/obj/structure/lattice | /turf/open/space/basic | /area/space", MapEdits.Peel(map, map.Model("/obj/structure/lattice | /turf/open/misc/asteroid | /area/station/hallway")).Text);
        // nothing is under the default turf
        Assert.Same(hole, MapEdits.Peel(map, hole));
        Assert.Same(map.Default, MapEdits.Peel(map, map.Default));

        // a stroke takes one kind: a tile whose turf is another stays as it is
        MapModel floor = map.Model("/turf/open/floor/iron | /area/station/hallway");
        Assert.Same(wall, MapEdits.Peel(map, wall, TurfLayer.Tile));
        Assert.Same(wall, MapEdits.Peel(map, wall, TurfLayer.Base));
        Assert.Same(floor, MapEdits.Peel(map, floor, TurfLayer.Wall));
        Assert.Same(bared, MapEdits.Peel(map, bared, TurfLayer.Tile));
        Assert.Equal("/turf/open/floor/plating | /area/station/hallway", MapEdits.Peel(map, floor, TurfLayer.Tile).Text);
        Assert.Equal(bared.Text, MapEdits.Peel(map, wall, TurfLayer.Wall).Text);
    }

    [Fact]
    public void ACeilingIsMadeOfAFloorALatticeOrACatwalkAndLosesALayerAtATime()
    {
        Assert.Equal("/turf/open/floor/iron/dark", MapEdits.CeilingFrom("/turf/open/floor/iron/dark"));
        Assert.Equal("/turf/open/floor/plating", MapEdits.CeilingFrom("/turf/open/floor/plating"));
        Assert.Equal("lattice", MapEdits.CeilingFrom("/obj/structure/lattice"));
        Assert.Equal("catwalk", MapEdits.CeilingFrom("/obj/structure/lattice/catwalk"));
        Assert.Null(MapEdits.CeilingFrom("/turf/closed/wall"));
        Assert.Null(MapEdits.CeilingFrom("/turf/open/space/basic"));
        Assert.Null(MapEdits.CeilingFrom("/obj/structure/table"));

        // a tile's leaves plating; plating, a lattice or a catwalk leaves nothing; nothing leaves nothing to take
        Assert.Equal("/turf/open/floor/plating", MapEdits.CeilingPeeled("/turf/open/floor/glass"));
        Assert.Equal("none", MapEdits.CeilingPeeled("/turf/open/floor/plating"));
        Assert.Equal("none", MapEdits.CeilingPeeled("lattice"));
        Assert.Equal("none", MapEdits.CeilingPeeled("catwalk"));
        Assert.Null(MapEdits.CeilingPeeled("none"));
        Assert.Null(MapEdits.CeilingPeeled(null));

        // placed on a tile it is the ceiling var of its turf, and a floor placed under it later keeps it
        MapDocument map = Map();
        MapModel tile = MapEdits.WithCeiling(map, map.Model("/turf/open/floor/iron | /area/station/hallway"), MapEdits.CeilingFrom("/turf/open/floor/wood"));
        Assert.Equal("/turf/open/floor/wood", MapEdits.CeilingOf(tile));
        Assert.Equal("/turf/open/floor/plating", MapEdits.CeilingOf(MapEdits.WithCeiling(map, tile, MapEdits.CeilingPeeled(MapEdits.CeilingOf(tile)))));
        Assert.Equal("/turf/open/floor/wood", MapEdits.CeilingOf(MapEdits.Peel(map, tile)));
    }

    // the owner (2026-11-17): on a map of several levels no ceiling could be made or changed; and a tile placed from under a level went on the floor's
    // top, not on the side looked at. Under a level its floor is the ceiling: glass takes its place, any other tile lies under its plating
    [Fact]
    public void FromUnderALevelATileIsLaidUnderItsPlatingAndGlassTakesItsPlace()
    {
        MapDocument map = Map();
        MapMember Brush(string path) => new(path, null);
        MapModel room = map.Model("/turf/open/floor/iron | /area/station/hallway"), iron = map.Model("/obj/structure/table | /turf/open/floor/iron | /area/station/hallway");

        // a tile under a floor: the floor stays as it is, with what stands on it; the room's turf says what lies under its plating
        (MapModel under, MapModel over) = MapEdits.Ceiled(map, room, iron, Brush("/turf/open/floor/carpet"));
        Assert.Same(iron, over);
        Assert.Equal("/turf/open/floor/iron{ceiling = \"/turf/open/floor/carpet\"} | /area/station/hallway", under.Text);
        Assert.Equal("/turf/open/floor/carpet", MapEdits.LaidCeiling(under));
        Assert.Equal("/turf/open/floor/carpet", MapEdits.UndersideOf(under, over));
        // plating takes the laid tile off again
        Assert.Equal((room.Text, iron.Text), Texts(MapEdits.Ceiled(map, under, over, Brush("/turf/open/floor/plating"))));
        Assert.Equal("/turf/open/floor/plating", MapEdits.UndersideOf(room, iron));
        // under a wall of that level too: it stands on plating
        MapModel wall = map.Model("/turf/closed/wall | /area/station/hallway");
        Assert.Equal((under.Text, wall.Text), Texts(MapEdits.Ceiled(map, room, wall, Brush("/turf/open/floor/carpet"))));

        // where there is nothing to lie under, open space, the slab is made of plating, in the room's area
        Assert.Equal((under.Text, "/turf/open/floor/plating | /area/station/hallway"), Texts(MapEdits.Ceiled(map, room, map.Default, Brush("/turf/open/floor/carpet"))));
        Assert.Equal((room.Text, "/turf/open/floor/plating | /area/station/hallway"), Texts(MapEdits.Ceiled(map, room, map.Default, Brush("/turf/open/floor/plating"))));
        Assert.Null(MapEdits.UndersideOf(room, map.Default));

        // glass takes the slab's place whole, and the tile that lay under its plating goes with it; a wall stays
        (MapModel bare, MapModel pane) = MapEdits.Ceiled(map, under, iron, Brush("/turf/open/floor/glass"));
        Assert.Equal((room.Text, "/obj/structure/table | /turf/open/floor/glass | /area/station/hallway"), (bare.Text, pane.Text));
        Assert.Equal("/turf/open/floor/glass", MapEdits.UndersideOf(bare, pane));
        Assert.Equal((room.Text, wall.Text), Texts(MapEdits.Ceiled(map, room, wall, Brush("/turf/open/floor/glass"))));
        // and under glass another tile has no plating to lie under: the pane becomes plating
        Assert.Equal((under.Text, "/obj/structure/table | /turf/open/floor/plating | /area/station/hallway"), Texts(MapEdits.Ceiled(map, bare, pane, Brush("/turf/open/floor/carpet"))));

        // a lattice goes where the tile above has no floor; what makes no ceiling changes nothing
        Assert.Equal((room.Text, "/obj/structure/lattice | /turf/open/space/basic | /area/space"), Texts(MapEdits.Ceiled(map, room, map.Default, Brush("/obj/structure/lattice"))));
        Assert.Equal((room.Text, iron.Text), Texts(MapEdits.Ceiled(map, room, iron, Brush("/obj/structure/lattice"))));
        Assert.Equal((room.Text, iron.Text), Texts(MapEdits.Ceiled(map, room, iron, Brush("/turf/closed/wall"))));

        // the var's other words lay no tile
        Assert.Null(MapEdits.LaidCeiling(MapEdits.WithCeiling(map, room, "none")));
        Assert.Null(MapEdits.LaidCeiling(MapEdits.WithCeiling(map, room, "/turf/open/floor/glass")));
        Assert.Null(MapEdits.LaidCeiling(MapEdits.WithCeiling(map, room, "/turf/open/floor/plating")));

        // the erase tool from under a level, with no tile laid there, takes the floor or the wall whole: what stands there stays
        Assert.Equal("/obj/structure/table | /turf/open/space/basic | /area/space", MapEdits.Unturfed(map, iron).Text);
        Assert.Equal(map.Default.Text, MapEdits.Unturfed(map, wall).Text);
        Assert.Same(map.Default, MapEdits.Unturfed(map, map.Default));

        // the erase tool while the palette shows the areas takes the area alone: the map's default area is left, the turf and what stands there stay
        Assert.Equal("/obj/structure/table | /turf/open/floor/iron | /area/space", MapEdits.Unareaed(map, iron).Text);
        Assert.Equal("/turf/closed/wall | /area/space", MapEdits.Unareaed(map, wall).Text);
        Assert.Same(map.Default, MapEdits.Unareaed(map, map.Default));
        MapModel unnamed = map.Model("/obj/structure/table | /turf/open/floor/iron | /area/space");
        Assert.Same(unnamed, MapEdits.Unareaed(map, unnamed));
    }

    private static (string, string) Texts((MapModel Room, MapModel Slab) pair) => (pair.Room.Text, pair.Slab.Text);

    [Fact]
    public void ATilesPiledTurfsGiveTheirPlaceToTheOnePlaced()
    {
        MapDocument map = MapDocument.Parse("anomaly-map 1\nsize 2 2 1\nchunk 32\ndefault " + Space + "\n\n[chunk 1 1 1]\n1,1: /obj/structure/lattice | /turf/open/floor/iron | /turf/open/floor/plating | /area/station/hallway\n", "piled");
        Assert.Equal("/obj/structure/lattice | /turf/closed/wall | /area/station/hallway", MapEdits.Place(map, map[1, 1, 1], new MapMember("/turf/closed/wall", null)).Text);
    }

    // The place tool's replace mode (5.8): what was clicked gives its place, and a tile that has not got it is left as it is.
    [Fact]
    public void WhatWasClickedGivesItsPlaceToTheBrush()
    {
        MapDocument map = Map();
        MapModel room = map.Model("/obj/structure/table | /obj/item/wrench | /obj/structure/table{name = \"desk\"} | /turf/open/floor/iron | /area/space");
        var chair = new MapMember("/obj/structure/chair", null);

        // a thing in a thing's place: where the first of the type stood, the others of the type gone, the rest in their order
        Assert.Equal("/obj/structure/chair | /obj/item/wrench | /turf/open/floor/iron | /area/space", MapEdits.ReplaceOn(map, room, "/obj/structure/table", chair).Text);
        // a tile with none of the clicked type is the same tile
        Assert.Same(room, MapEdits.ReplaceOn(map, room, "/obj/structure/rack", chair));
        // the brush the tile has already is not there twice
        MapModel both = map.Model("/obj/structure/chair | /obj/structure/table | /turf/open/floor/iron | /area/space");
        Assert.Equal("/obj/structure/chair | /turf/open/floor/iron | /area/space", MapEdits.ReplaceOn(map, both, "/obj/structure/table", chair).Text);

        // a turf in a thing's place: the thing is taken away and the turf is the tile's
        MapModel window = map.Model("/obj/structure/window/reinforced/fulltile | /obj/structure/grille | /turf/open/floor/plating | /area/space");
        Assert.Equal("/obj/structure/grille | /turf/closed/wall | /area/space", MapEdits.ReplaceOn(map, window, "/obj/structure/window/reinforced/fulltile", new MapMember("/turf/closed/wall", null)).Text);

        // a turf in a turf's place, only where the tile's turf is the clicked one
        var wood = new MapMember("/turf/open/floor/wood", null);
        Assert.Equal("/obj/structure/table | /obj/item/wrench | /obj/structure/table{name = \"desk\"} | /turf/open/floor/wood | /area/space", MapEdits.ReplaceOn(map, room, "/turf/open/floor/iron", wood).Text);
        MapModel wall = map.Model("/turf/closed/wall | /area/space");
        Assert.Same(wall, MapEdits.ReplaceOn(map, wall, "/turf/open/floor/iron", wood));

        // a thing in a wall's place stands on plating; on a floor it is one more thing
        Assert.Equal("/obj/structure/chair | /turf/open/floor/plating | /area/space", MapEdits.ReplaceOn(map, wall, "/turf/closed/wall", chair).Text);
        Assert.Equal("/obj/structure/chair | /turf/open/floor/iron | /area/space", MapEdits.ReplaceOn(map, map.Model("/turf/open/floor/iron | /area/space"), "/turf/open/floor/iron", chair).Text);
        Assert.Equal("/turf/open/floor/iron", MapEdits.TurfOf(room));
    }

    [Fact]
    public void AMemberIsTakenAway()
    {
        MapDocument map = Map();
        MapModel tile = map.Model("/obj/structure/table | /obj/item/wrench | /turf/open/floor/iron | /area/station/hallway");
        Assert.Equal("/obj/item/wrench | /turf/open/floor/iron | /area/station/hallway", MapEdits.Remove(map, tile, 0).Text);
        Assert.Equal("/obj/structure/table | /turf/open/floor/iron | /area/station/hallway", MapEdits.Remove(map, tile, 1).Text);
        // a tile is never without a turf or an area: the map's default takes the place
        Assert.Equal("/obj/structure/table | /obj/item/wrench | /turf/open/space/basic | /area/station/hallway", MapEdits.Remove(map, tile, 2).Text);
        Assert.Equal("/obj/structure/table | /obj/item/wrench | /turf/open/floor/iron | /area/space", MapEdits.Remove(map, tile, 3).Text);
        Assert.Same(tile, MapEdits.Remove(map, tile, 4));
        Assert.Same(tile, MapEdits.Remove(map, tile, -1));
        Assert.Same(map.Default, MapEdits.Remove(map, map.Default, 0)); // the default's own turf stays
    }

    [Fact]
    public void AVarIsSetChangedAndGivenBack()
    {
        MapDocument map = Map();
        MapModel tile = map.Model("/obj/machinery/power/smes | /turf/open/floor/iron | /area/station/hallway");
        tile = MapEdits.SetVar(map, tile, 0, "charge", "5e+006");
        Assert.Equal("/obj/machinery/power/smes{charge = 5e+006} | /turf/open/floor/iron | /area/station/hallway", tile.Text);
        Assert.Equal("5e+006", MapEdits.VarOf(tile.Members[0], "charge"));
        Assert.Null(MapEdits.VarOf(tile.Members[0], "name"));

        // vars keep the order of their names, as tg's map files do
        tile = MapEdits.SetVar(map, tile, 0, "name", "\"Engine SMES\"");
        tile = MapEdits.SetVar(map, tile, 0, "dir", "4");
        Assert.Equal("/obj/machinery/power/smes{charge = 5e+006; dir = 4; name = \"Engine SMES\"} | /turf/open/floor/iron | /area/station/hallway", tile.Text);
        tile = MapEdits.SetVar(map, tile, 0, "dir", " 8 ");
        Assert.Equal("8", MapEdits.VarOf(tile.Members[0], "dir"));

        // an empty literal gives the type's value back; a var that was not set stays unset
        tile = MapEdits.SetVar(map, tile, 0, "charge", "");
        tile = MapEdits.SetVar(map, tile, 0, "dir", null);
        Assert.Equal("/obj/machinery/power/smes{name = \"Engine SMES\"} | /turf/open/floor/iron | /area/station/hallway", tile.Text);
        Assert.Same(tile, MapEdits.SetVar(map, tile, 0, "charge", null));

        // the turf's and the area's vars are set the same way
        Assert.Equal("/obj/machinery/power/smes{name = \"Engine SMES\"} | /turf/open/floor/iron{dir = 4} | /area/station/hallway",
            MapEdits.SetVar(map, tile, 1, "dir", "4").Text);

        // what the format does not allow is refused
        Assert.Throws<MapTextException>(() => MapEdits.SetVar(map, tile, 0, "name", "\"unclosed"));
        Assert.Throws<MapTextException>(() => MapEdits.SetVar(map, tile, 0, "bad name", "1"));
    }

    [Fact]
    public void ATypesVarsAreReadFromItsClass()
    {
        IReadOnlyList<TypeVar> smes = TypeVars.Of("/obj/machinery/power/smes");
        Assert.Contains(smes, v => v.Name == "charge");
        Assert.Equal("\"power storage unit\"", smes.Single(v => v.Name == "name").Default);
        Assert.Equal("1", smes.Single(v => v.Name == "density").Default);
        Assert.Contains(smes, v => v.Name == "icon_state" && v.Default != null);
        Assert.Contains(smes, v => v.Name == "dir");
        Assert.DoesNotContain(smes, v => v.Name is "loc" or "contents" or "type");
        Assert.Equal(smes.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal), smes.Select(v => v.Name));
        Assert.Same(smes, TypeVars.Of("/obj/machinery/power/smes")); // read once

        Assert.Contains(TypeVars.Of("/turf/closed/wall"), v => v.Name == "hardness");
        Assert.Contains(TypeVars.Of("/area/station/hallway"), v => v.Name == "name");
        Assert.Empty(TypeVars.Of("/obj/no/such/type"));
        Assert.Empty(TypeVars.Of(null));

        Assert.Equal("null", TypeVars.Literal(null));
        Assert.Equal("\"a \\\"b\\\"\"", TypeVars.Literal("a \"b\""));
        Assert.Equal("0.5", TypeVars.Literal(0.5f));
        Assert.Equal("/obj/item/wrench", TypeVars.Literal(Anomaly.Core.DmShim.DmTypes.FromPath("/obj/item/wrench")));
        Assert.Null(TypeVars.Literal(new object()));
    }
}
