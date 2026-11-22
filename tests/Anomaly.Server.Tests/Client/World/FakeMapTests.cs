using System.Diagnostics;
using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 P6: FakeFeed version 3's map (the private reader, the type defaults, the MapGrid and the entities).</summary>
public sealed class FakeMapTests
{
    private readonly ITestOutputHelper _output;

    public FakeMapTests(ITestOutputHelper output) => _output = output;

    // A 6 x 5 room: walls round it, an airlock in the north wall, a table, a light on the north wall, a directional window and spawned windows.
    internal const string Room = @"anomaly-map 1
size 6 5 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 1 1 1]
1,5: /turf/closed/wall | /area/station/hallway
2,5: /turf/closed/wall | /area/station/hallway
3,5: /obj/machinery/door/airlock{name = ""Test Airlock""} | /turf/open/floor/plating | /area/station/hallway
4,5: /turf/closed/wall | /area/station/hallway
5,5: /turf/closed/wall | /area/station/hallway
6,5: /turf/closed/wall | /area/station/hallway
1,4: /turf/closed/wall | /area/station/hallway
2,4: /obj/machinery/light/directional/north | /turf/open/floor/iron | /area/station/hallway
3,4: /obj/effect/landmark/start | /turf/open/floor/iron | /area/station/hallway
4,4: /obj/structure/table | /turf/open/floor/iron | /area/station/hallway
5,4: /obj/structure/table | /turf/open/floor/iron | /area/station/hallway
6,4: /obj/effect/spawner/structure/window/reinforced | /turf/open/floor/plating | /area/station/hallway
1,3: /turf/closed/wall | /area/station/hallway
2,3: /obj/structure/window/spawner/directional/east | /turf/open/floor/iron | /area/station/hallway
3,3: /obj/machinery/atmospherics/pipe/smart/simple/general{dir = 4} | /turf/open/floor/iron | /area/station/hallway
";

    [Fact]
    public void TheReaderSplitsMembersAndOverrides()
    {
        RawMap raw = FakeMapReader.Parse(Room, "room");
        Assert.Equal((6, 5, 1), (raw.Width, raw.Height, raw.Levels));
        RawModel airlock = raw.Lines.First(l => l.X == 3 && l.Y == 5).Model;
        Assert.Equal(3, airlock.Members.Count);
        Assert.Equal("/obj/machinery/door/airlock", airlock.Members[0].Path);
        Assert.Equal("Test Airlock", airlock.Members[0].Text("name"));
        Assert.Equal(4f, FakeMapReader.Parse(Room, "room").Lines.Last().Model.Members[0].Number("dir"));
    }

    [Fact]
    public void TheReaderKeepsSeparatorsInsideStringsAndLists()
    {
        const string text = "anomaly-map 1\nsize 2 1 1\nchunk 32\ndefault /turf/open/space/basic | /area/space\n\n[chunk 1 1 1]\n1,1: /obj/item/paper{info = \"a | b; c = d\"; list = list(1, \"x | y\")} | /turf/open/floor/iron | /area/x\n";
        RawMap raw = FakeMapReader.Parse(text, "strings");
        RawModel model = raw.Lines[0].Model;
        Assert.Equal(3, model.Members.Count);
        Assert.Equal("a | b; c = d", model.Members[0].Text("info"));
        Assert.Equal("list(1, \"x | y\")", model.Members[0].Literal("list"));
    }

    [Fact]
    public void ABadFileIsRefusedWithItsLine()
    {
        FakeMapFormatException e = Assert.Throws<FakeMapFormatException>(() => FakeMapReader.Parse("anomaly-map 1\nsize 2 1 1\ndefault /turf/x | /area/y\n3,1: /turf/x | /area/y\n", "bad"));
        Assert.Contains("bad:4", e.Message);
    }

    [Fact]
    public void TheRoomBecomesAGrid()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        MapGrid grid = map.MapGrid;
        Assert.Equal((6, 5, 1), (grid.Width, grid.Height, grid.Levels));
        Assert.Contains("/turf/closed/wall", grid.TurfTypes);
        Assert.Equal(grid.CellCount, grid.ExpandCells().Length);
        // Walls and the table and the airlock are solid; the floor and the directional window's cell are not (a border object blocks a face).
        Assert.True(grid.IsSolid(1, 5, 1));
        Assert.True(grid.IsSolid(3, 5, 1), "a closed airlock");
        Assert.True(grid.IsSolid(4, 4, 1), "a table");
        Assert.False(grid.IsSolid(2, 4, 1));
        Assert.False(grid.IsSolid(2, 3, 1), "a border window does not block the cell");
        // Flags: the window faces east; walls and the closed airlock are opaque; space next to the room is starlit.
        byte[] flags = grid.ExpandFlags();
        Assert.Equal(CellFlags.FaceEast, flags[(2 - 1) + (3 - 1) * 6] & CellFlags.Faces);
        Assert.NotEqual(0, flags[(1 - 1) + (5 - 1) * 6] & CellFlags.Opaque);
        Assert.NotEqual(0, flags[(3 - 1) + (5 - 1) * 6] & CellFlags.Opaque);
        Assert.NotEqual(0, flags[(1 - 1) + (2 - 1) * 6] & CellFlags.Starlight);
        Assert.Equal(0, flags[(1 - 1) + (1 - 1) * 6] & CellFlags.Starlight); // two cells from the room: dark
        Assert.False(grid.Starlight.IsNone);
    }

    [Fact]
    public void WallsGetTgsSmoothingJunction()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        MapGrid grid = map.MapGrid;
        // (2,5) has walls to its west and east (1,5 and 3,5 is a door: also joins) and none to the north: junction W + E (+ S when 2,4 were a wall: it is a floor).
        var states = new List<string>();
        foreach (TurfRun run in grid.AppearanceRuns)
            states.Add(run.TypeIndex.ToString());
        TableRef wall25 = AppearanceAt(map, 2, 5);
        Assert.True(map.Tables.Appearances[(int)wall25.Index - 1].IconState.Index > 0);
        string state = map.Tables.Strings[(int)map.Tables.Appearances[(int)wall25.Index - 1].IconState.Index - 1];
        Assert.Equal("wall-12", state); // E 4 + W 8
        TableRef corner = AppearanceAt(map, 1, 4);
        Assert.Equal("wall-3", map.Tables.Strings[(int)map.Tables.Appearances[(int)corner.Index - 1].IconState.Index - 1]); // N 1 + S 2
    }

    private static TableRef AppearanceAt(FakeMap map, int x, int y)
    {
        uint[] cells = new uint[map.MapGrid.CellCount];
        int next = 0;
        foreach (TurfRun run in map.MapGrid.AppearanceRuns)
        {
            Array.Fill(cells, (uint)run.TypeIndex, next, run.Count);
            next += run.Count;
        }
        return TableRef.Public(cells[(x - 1) + (y - 1) * map.MapGrid.Width]);
    }

    [Fact]
    public void OnlyWhatD115ShowsBecomesAnEntity()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        List<string> types = map.Entities.Select(e => e.TypePath).ToList();
        Assert.Contains("/obj/machinery/door/airlock", types);
        Assert.Contains("/obj/structure/table", types);
        Assert.Contains("/obj/machinery/light/directional/north", types);
        // The landmark and the pipe are not entities; the window spawner became a window and a grille.
        Assert.DoesNotContain(types, t => t.Contains("landmark") || t.Contains("pipe"));
        Assert.DoesNotContain(types, t => t.Contains("spawner/structure"));
        Assert.Contains("/obj/structure/window/reinforced/fulltile", types);
        Assert.Contains("/obj/structure/grille", types);
        // Ids run from 1 in the file's member order.
        Assert.Equal(Enumerable.Range(1, map.Entities.Count).Select(i => (long)i), map.Entities.Select(e => e.Id));
        FakeMapEntity airlock = map.Entities.First(e => e.TypePath == "/obj/machinery/door/airlock");
        Assert.Equal("Test Airlock", airlock.Name);
        Assert.True(airlock.Density);
        FakeMapEntity light = map.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/light"));
        Assert.False(light.Light.IsNone);
        Assert.Equal(ByondDirOf.North, light.Dir);
    }

    [Fact]
    public void ASpawnCellIsAFloorWithAStartLandmark()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        Assert.Equal(new[] { (3, 4, 1) }, map.SpawnCells);
    }

    [Fact]
    public void TheEntitiesHaveAppearancesInTheTables()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        Assert.Equal(map.Entities.Count, map.EntityAppearances.Count);
        FakeMapEntity table = map.Entities.First(e => e.TypePath == "/obj/structure/table");
        int index = map.Entities.ToList().IndexOf(table);
        AppearanceEntry entry = map.Tables.Appearances[(int)map.EntityAppearances[index].Index - 1];
        // Two tables side by side: the left one joins east (4), the right one west (8).
        Assert.Equal("table-4", map.Tables.Strings[(int)entry.IconState.Index - 1]);
    }

    [Fact]
    public void BindingToAnotherSetOfTablesKeepsTheGridAndMovesTheReferences()
    {
        FakeMap map = FakeMap.Parse(Room, "room");
        var tables = new AppearanceTables();
        tables.Resource("icons/mob/human/human.dmi");
        tables.String("human");
        FakeMapBinding binding = map.Bind(tables);
        Assert.Equal(map.MapGrid.Runs.Count, binding.Grid.Runs.Count);
        Assert.All(binding.Grid.AppearanceRuns, run => Assert.True(run.TypeIndex <= tables.Appearances.Count));
        Assert.Equal("icons/mob/human/human.dmi", tables.Resources[0]);
    }

    [Fact]
    public void MetaStationReadsFast()
    {
        string path = MetaPath();
        if (path == null)
        {
            _output.WriteLine("no converted MetaStation (maps/map_files/MetaStation/MetaStation.as3dmap or ANOMALY_META_MAP): skipped");
            return;
        }
        var watch = Stopwatch.StartNew();
        FakeMap map = FakeMap.Load(path);
        long loadMs = watch.ElapsedMilliseconds;
        watch.Restart();
        MapGrid grid = map.MapGrid;
        long gridMs = watch.ElapsedMilliseconds;
        _output.WriteLine($"Meta: {map.Width}x{map.Height}x{map.Levels}, {map.Entities.Count} entities, {grid.TurfTypes.Count} turf types, {map.Tables.Appearances.Count} appearances, {map.SpawnCells.Count} spawn cells; read {loadMs} ms, grid {gridMs} ms");
        Assert.Equal((255, 255, 1), (grid.Width, grid.Height, grid.Levels));
        Assert.True(map.Entities.Count > 5000);
        Assert.True(map.SpawnCells.Count > 0);
        foreach ((int x, int y, int z) in map.SpawnCells)
            Assert.False(grid.IsSolid(x, y, z));
        Assert.Contains(map.Entities, e => e.TypePath.StartsWith("/obj/machinery/door/airlock"));
        Assert.Contains(map.Entities, e => e.TypePath.StartsWith("/obj/structure/window") && e.TypePath.Contains("fulltile"));
        Assert.True(loadMs < 15000, "reading Meta took " + loadMs + " ms");
    }

    internal static string MetaPath()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_META_MAP");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
            return env;
        string dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "maps", "map_files", "MetaStation", "MetaStation.as3dmap");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static class ByondDirOf
    {
        public const byte North = 1;
    }
}
