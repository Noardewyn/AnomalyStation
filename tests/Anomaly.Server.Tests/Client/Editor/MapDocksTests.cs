using System.IO;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Client.World;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The docking ports of a map and the sizes of shuttles (docs/ai/specs/map-editor.md 5.11): tg's own numbers.</summary>
public sealed class MapDocksTests
{
    private const string Space = "/turf/open/space/basic | /area/space";

    [Fact]
    public void ADocksRectangleIsByItsVarsAndItsDir()
    {
        MapDocument map = MapDocument.Create(40, 40, 1, Space);
        // a dock facing north, 7 wide and 12 long, the port 3 from its left edge and at its bottom row
        map.Edit("dock", new[] { (20, 10, 1, map.Model("/obj/docking_port/stationary{dheight = 0; dir = 1; dwidth = 3; height = 12; shuttle_id = \"cargo_home\"; width = 7} | " + Space)) });
        MapDock north = Assert.Single(MapDocks.Find(map));
        Assert.Equal(("cargo_home", false, 1), (north.Id, north.Mobile, north.Dir));
        Assert.Equal((17, 10, 23, 21), (north.X0, north.Y0, north.X1, north.Y1));

        // the same dock turned: tg's return_coords
        Assert.Equal((17, -1, 23, 10), MapDocks.Coords(20, 10, 2, north.Size)); // south: the mirror
        Assert.Equal((20, 7, 31, 13), MapDocks.Coords(20, 10, 4, north.Size)); // east: its length runs east, its left is north
        Assert.Equal((9, 7, 20, 13), MapDocks.Coords(20, 10, 8, north.Size)); // west
    }

    [Fact]
    public void AShuttlesSizeIsItsMapsAndItFitsADockOrNot()
    {
        // a shuttle's map of 7 by 12 with its port on the bottom row, facing north
        MapDocument shuttle = MapDocument.Create(7, 12, 1, "/turf/template_noop | /area/template_noop");
        Assert.Null(MapDocks.ShuttleOf(shuttle));
        shuttle.Edit("port", new[] { (4, 1, 1, shuttle.Model("/obj/docking_port/mobile{dir = 1} | /turf/open/floor/mineral/titanium | /area/shuttle/supply")) });
        (DockSize size, int ports) = MapDocks.ShuttleOf(shuttle).Value;
        Assert.Equal(1, ports);
        Assert.Equal(new DockSize(7, 12, 3, 0), size);
        // its own port's rectangle is its map
        MapDock own = Assert.Single(MapDocks.Find(shuttle));
        Assert.True(own.Mobile);
        Assert.Equal((1, 1, 7, 12), (own.X0, own.Y0, own.X1, own.Y1));
        // the port facing east: tg turns the sizes
        Assert.Equal(new DockSize(12, 7, 11, 3), MapDocks.ShuttleSize(7, 12, 4, 1, 4));

        Assert.Null(MapDocks.Misfit(size, new DockSize(7, 12, 3, 0)));
        Assert.Null(MapDocks.Misfit(size, new DockSize(9, 14, 4, 1)));
        Assert.Equal("1 too wide on the port's left", MapDocks.Misfit(size, new DockSize(7, 12, 2, 0)));
        Assert.Equal("2 too wide on the port's right, 3 too long before the port", MapDocks.Misfit(size, new DockSize(5, 9, 3, 0)));

        // a second port: a shuttle has one
        shuttle.Edit("port", new[] { (2, 2, 1, shuttle.Model("/obj/docking_port/mobile | /turf/open/floor/mineral/titanium | /area/shuttle/supply")) });
        Assert.Equal(2, MapDocks.ShuttleOf(shuttle).Value.Ports);
    }

    private const string Hull = "/turf/open/floor/mineral/titanium | /area/shuttle/supply";

    // A shuttle of 3 by 4: its port in the middle of its bottom row facing north, a chair facing east before the port, a wall at its bottom left.
    private static MapDocument SmallShuttle()
    {
        MapDocument shuttle = MapDocument.Create(3, 4, 1, "/turf/template_noop | /area/template_noop");
        shuttle.Edit("hull", new[]
        {
            (2, 1, 1, shuttle.Model("/obj/docking_port/mobile{dir = 1} | " + Hull)),
            (2, 2, 1, shuttle.Model("/obj/structure/chair{dir = 4} | " + Hull)),
            (1, 1, 1, shuttle.Model("/turf/closed/wall/mineral/titanium | /area/shuttle/supply")),
        });
        return shuttle;
    }

    private static string At(DockedShuttle docked, int x, int y) => MapDocument.TextOf(docked.Tiles.Single(tile => (tile.X, tile.Y) == (x, y)).Tile.Members);

    [Fact]
    public void AShuttleStandsAtItsDockTurnedAsTheDockFaces()
    {
        MapDocument map = MapDocument.Create(40, 40, 1, Space);
        const string Dock = "/obj/docking_port/stationary{dheight = 0; dir = 1; dwidth = 1; height = 4; shuttle_id = \"cargo_home\"; width = 3}";
        map.Edit("dock", new[] { (20, 10, 1, map.Model(Dock + " | " + Space)) });
        MapDocument shuttle = SmallShuttle();

        // the dock faces as the shuttle's port does: nothing turns, the shuttle's port stands on the dock, and of the two ports the map's own stays
        DockedShuttle north = MapDocks.Docked(map, MapDocks.Find(map)[0], shuttle);
        Assert.Equal((19, 10, 21, 13), north.Box);
        Assert.Equal((0, 0), (north.OffMap, north.Crushed));
        Assert.Null(north.FirstCrushed);
        Assert.Equal(3, north.Tiles.Count); // the tiles of template_noop are no part of the shuttle
        Assert.Equal(Dock + " | " + Hull, At(north, 20, 10));
        Assert.Equal("/obj/structure/chair{dir = 4} | " + Hull, At(north, 20, 11));
        Assert.Equal("/turf/closed/wall/mineral/titanium | /area/shuttle/supply", At(north, 19, 10));

        // the dock facing east: the shuttle turns a quarter clockwise about its port, what was before the port is east of it and faces south
        map.Edit("dock", new[] { (20, 10, 1, map.Model(Dock.Replace("dir = 1", "dir = 4") + " | " + Space)) });
        DockedShuttle east = MapDocks.Docked(map, MapDocks.Find(map)[0], shuttle);
        Assert.Equal(MapDocks.Coords(20, 10, 4, new DockSize(3, 4, 1, 0)), east.Box);
        Assert.Equal("/obj/structure/chair{dir = 2} | " + Hull, At(east, 21, 10));
        Assert.Equal("/turf/closed/wall/mineral/titanium | /area/shuttle/supply", At(east, 20, 11)); // its left is north now
        Assert.All(east.Tiles, tile => Assert.True(tile.X >= east.Box.X0 && tile.X <= east.Box.X1 && tile.Y >= east.Box.Y0 && tile.Y <= east.Box.Y1));

        // the dock facing south: half a turn
        map.Edit("dock", new[] { (20, 10, 1, map.Model(Dock.Replace("dir = 1", "dir = 2") + " | " + Space)) });
        DockedShuttle south = MapDocks.Docked(map, MapDocks.Find(map)[0], shuttle);
        Assert.Equal("/obj/structure/chair{dir = 8} | " + Hull, At(south, 20, 9));
        Assert.Equal("/turf/closed/wall/mineral/titanium | /area/shuttle/supply", At(south, 21, 10));

        // a shuttle's map with no port of its own stands nowhere
        Assert.Null(MapDocks.Docked(map, MapDocks.Find(map)[0], MapDocument.Create(3, 4, 1, "/turf/template_noop | /area/template_noop")));
    }

    [Fact]
    public void AShuttleAtADockSaysWhatItStandsOnAndWhatIsPastTheMap()
    {
        // a dock on the map's west edge: the shuttle's left column is past it
        MapDocument map = MapDocument.Create(10, 10, 1, Space);
        map.Edit("dock", new[]
        {
            (1, 5, 1, map.Model("/obj/docking_port/stationary{dir = 1; dwidth = 1; height = 4; shuttle_id = \"cargo_home\"; width = 3} | " + Space)),
            (1, 6, 1, map.Model("/obj/structure/table | /turf/open/floor/iron | /area/station/cargo")), // a thing where the chair's tile comes
        });
        DockedShuttle docked = MapDocks.Docked(map, MapDocks.Find(map)[0], SmallShuttle());
        Assert.Equal(1, docked.OffMap); // the wall of the shuttle's bottom left
        Assert.Equal(1, docked.Crushed);
        Assert.Equal((1, 6), docked.FirstCrushed.Value);
        // the map's thing is not on the tile the shuttle takes: docking destroys it
        Assert.Equal("/obj/structure/chair{dir = 4} | " + Hull, At(docked, 1, 6));

        // a wall of the map under the shuttle counts too; plain floor does not, nor the lattice and the landmarks a station has about its docks
        map.Edit("wall", new[] { (1, 6, 1, map.Model("/turf/closed/wall | /area/station/cargo")) });
        Assert.Equal(1, MapDocks.Docked(map, MapDocks.Find(map)[0], SmallShuttle()).Crushed);
        map.Edit("floor", new[] { (1, 6, 1, map.Model("/turf/open/floor/iron | /area/station/cargo")) });
        Assert.Equal(0, MapDocks.Docked(map, MapDocks.Find(map)[0], SmallShuttle()).Crushed);
        map.Edit("lattice", new[] { (1, 6, 1, map.Model("/obj/structure/lattice/catwalk | /obj/effect/landmark/start | " + Space)) });
        Assert.Equal(0, MapDocks.Docked(map, MapDocks.Find(map)[0], SmallShuttle()).Crushed);
    }

    [Fact]
    public void ADocksOwnShuttleIsItsRoundstartTemplates()
    {
        MapDocument map = MapDocument.Create(8, 8, 1, Space);
        MapMember Port(string text) => map.Model(text + " | " + Space).Members[0];
        Assert.Equal(("arrival", "arrival", "arrival_box"), MapDocks.TemplateOf(Port("/obj/docking_port/stationary{roundstart_template = /datum/map_template/shuttle/arrival/box}")));
        Assert.Null(MapDocks.TemplateOf(Port("/obj/docking_port/stationary{shuttle_id = \"cargo_home\"}")));
        Assert.Null(MapDocks.TemplateOf(Port("/obj/docking_port/stationary{roundstart_template = /datum/map_template/shuttle/no_such/thing}")));
        // a dock whose type names its shuttle
        Assert.Equal(("mining", "mining", "mining_delta"), MapDocks.TemplateOf(Port("/obj/docking_port/stationary/mining_home")));
        // the folder is the template's own (tg's prefix), not its port's name
        Assert.Equal(("mining", "mining_common", "mining_common_meta"), MapDocks.TemplateOf(Port("/obj/docking_port/stationary/mining_home/common")));
    }

    // The game's own: the Outpost's departure shuttle stands in its dock's rectangle, on nothing of the map.
    [Fact]
    public void TheOutpostsShuttleStandsInItsDock()
    {
        string maps = Integration.MultizDebugBreachTests.RepoMaps();
        string station = maps == null ? null : Path.Combine(maps, "map_files", "Outpost", "Outpost" + MapText.Extension);
        string file = maps == null ? null : Path.Combine(maps, "shuttles", "emergency", "emergency_outpost" + MapText.Extension);
        if (station == null || !File.Exists(station) || !File.Exists(file))
            return; // no maps folder: skipped
        MapDocument map = MapDocument.Parse(File.ReadAllText(station), "Outpost");
        MapDock dock = Assert.Single(MapDocks.Find(map), d => d.Id == "emergency_home");
        DockedShuttle docked = MapDocks.Docked(map, dock, MapDocument.Parse(File.ReadAllText(file), "emergency_outpost"));
        Assert.Equal((dock.X0, dock.Y0, dock.X1, dock.Y1), docked.Box);
        Assert.Equal((0, 0), (docked.OffMap, docked.Crushed));
        Assert.All(docked.Tiles, tile => Assert.True(tile.X >= dock.X0 && tile.X <= dock.X1 && tile.Y >= dock.Y0 && tile.Y <= dock.Y1 && tile.Z == dock.Z));
    }

    // tg's own: the cargo shuttle of the maps folder fits MetaStation's cargo dock.
    [Fact]
    [Trait("Category", "Meta")]
    public void TheCargoShuttleFitsMetaStationsDock()
    {
        string meta = FakeMapTests.MetaPath();
        string box = meta == null ? null : Path.Combine(Path.GetDirectoryName(meta), "..", "..", "shuttles", "cargo", "cargo_box" + MapText.Extension);
        if (meta == null || !File.Exists(box))
            return; // no converted MetaStation with its maps folder: skipped
        MapDocument station = MapDocument.Parse(File.ReadAllText(meta), "MetaStation");
        MapDocument cargo = MapDocument.Parse(File.ReadAllText(box), "cargo_box");
        MapDock dock = Assert.Single(MapDocks.Find(station), d => d.Id == "cargo_home");
        (DockSize size, int ports) = MapDocks.ShuttleOf(cargo).Value;
        Assert.Equal(1, ports);
        Assert.Null(MapDocks.Misfit(size, dock.Size));
    }
}
