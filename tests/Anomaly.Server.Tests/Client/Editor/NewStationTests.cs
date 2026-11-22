using System.IO;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Net;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// A new station started in the map editor (docs/ai/specs/map-editor.md 5.7, 5.10): a map saved at a station's place of a maps folder is that folder's
/// map, its json written by the save; and a small map of no maps folder is played with the game's own shuttles.
/// </summary>
public sealed class NewStationTests : IDisposable
{
    private const string Floor = "/turf/open/floor/iron | /area/station/hallway/primary/central";
    // tg's arrivals shuttle's dock, as a mapper places it: the shuttle takes 7 by 15 tiles north of the port
    private const string Dock = "/obj/docking_port/stationary{dir = 1; dwidth = 3; height = 15; width = 7; roundstart_template = /datum/map_template/shuttle/arrival/box; shuttle_id = \"arrival_stationary\"} | /turf/open/space | /area/space";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "anomaly-new-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void Build(MapDocument map)
    {
        map.Edit("floor", (from x in Enumerable.Range(4, 5) from y in Enumerable.Range(3, 5) select (x, y, 1, map.Model(Floor))).ToList());
        map.Edit("dock", new[] { (24, 20, 1, map.Model(Dock)) });
    }

    private static void AssertSeated(Integration.RealHeadlessWorld w)
    {
        var alice = w.Join("Alice");
        mob_living mob = w.MobOf(alice);
        using (w.World.Enter())
        {
            Assert.IsAssignableFrom<area_shuttle_arrival>(get_area(mob));
            Assert.IsAssignableFrom<obj_structure_chair>(mob.buckled);
            Assert.NotNull(SSshuttle.arrivals);
            Assert.NotNull(SSshuttle.getDock("emergency_away")); // CentCom's docks stand, in a world that was grown for them (DV509)
        }
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void AMapSavedAtAStationsPlaceOfAMapsFolderIsThatFoldersMap()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        // a file named straight in map_files gets tg's place, a folder of its own name
        string file = Path.Combine(_root, "map_files", "Fort Nine", "Fort Nine" + MapText.Extension);
        Assert.Equal(file, EditorSession.Placed(Path.Combine(_root, "map_files", "Fort Nine" + MapText.Extension)));
        Assert.Equal(file, EditorSession.Placed(file));
        Assert.Equal((_root, "fort_nine"), EditorFeed.PlaceOf(file));
        Assert.Null(EditorFeed.PlaceOf(Path.Combine(_root, "shuttles", "cargo", "cargo" + MapText.Extension)));
        Assert.Null(EditorFeed.PlaceOf(Path.Combine(_root, "map_files", "generic", "CentCom" + MapText.Extension)));

        string json = Path.Combine(_root, "map_jsons", "fort_nine.json");
        using (EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Fort Nine" + MapText.Extension), 48, 48, 1))
        {
            Assert.Equal(file, session.FilePath);
            Assert.Null(session.MapsFolder); // (not saved yet: nothing names it)
            Build(session.Document);
            session.Save();
            Assert.Equal(_root, session.MapsFolder);
            Assert.Equal(json, session.JsonPath);
            Assert.False(session.NeedsSave);
            string text = File.ReadAllText(json);
            Assert.Contains("\"map_path\": \"map_files/Fort Nine\"", text);
            Assert.Contains("\"map_file\": \"Fort Nine.dmm\"", text);
            Assert.False(File.Exists(Path.ChangeExtension(file, ".json")));
            // its shuttle slots are the folder's now, and saved in that json
            session.SetJsonString("shuttles", "cargo", "cargo_box");
            session.Save();
            Assert.Contains("cargo_box", File.ReadAllText(json));
            Assert.Equal((_root, "fort_nine", null), EditorFeed.MapsOf(session.Document, session.FilePath, "Fort Nine" + MapText.Extension));
        }
        using (EditorSession again = EditorSession.Open(file))
        {
            Assert.Equal(_root, again.MapsFolder);
            Assert.Equal("cargo_box", again.JsonString("shuttles", "cargo"));
        }

        // a lone map with settings of its own, saved as a station of the folder, takes them into the folder's json
        string lone = Path.Combine(_root, "elsewhere", "lone" + MapText.Extension);
        using (EditorSession session = EditorSession.New(lone, 8, 8, 1))
        {
            session.SetSettings(session.Settings with { StationName = "Moved" });
            session.Save();
            Assert.True(File.Exists(Path.ChangeExtension(lone, ".json")));
            session.SaveAs(EditorSession.Placed(Path.Combine(_root, "map_files", "Moved" + MapText.Extension)));
            Assert.Equal(_root, session.MapsFolder);
            Assert.Equal("Moved", MapSettings.Read(File.ReadAllText(Path.Combine(_root, "map_jsons", "moved.json"))).StationName);
            Assert.True(File.Exists(Path.ChangeExtension(lone, ".json"))); // (the map it was saved from keeps its own)
        }

        // a map that stood at such a place with its own json beside it (an older editor's) joins the folder at its next save, and the json goes along
        string old = Path.Combine(_root, "map_files", "Old", "Old" + MapText.Extension);
        Directory.CreateDirectory(Path.GetDirectoryName(old));
        File.Copy(lone, old);
        File.WriteAllText(Path.ChangeExtension(old, ".json"), File.ReadAllText(Path.ChangeExtension(lone, ".json")));
        using (EditorSession session = EditorSession.Open(old))
        {
            Assert.Null(session.MapsFolder);
            Assert.Equal("Moved", session.Settings.StationName);
            session.Save();
            Assert.Equal(_root, session.MapsFolder);
            Assert.Equal("Moved", MapSettings.Read(File.ReadAllText(Path.Combine(_root, "map_jsons", "old.json"))).StationName);
            Assert.False(File.Exists(Path.ChangeExtension(old, ".json")));
        }

        // a json of the map's name that is another map's is left alone: the map stays a lone one
        File.WriteAllText(Path.Combine(_root, "map_jsons", "taken.json"), "{\"version\": 1, \"map_name\": \"Other\", \"map_path\": \"map_files/Other\", \"map_file\": \"Other.dmm\"}");
        using (EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Taken", "Taken" + MapText.Extension), 8, 8, 1))
        {
            session.Save();
            Assert.Null(session.MapsFolder);
            Assert.Contains("Other.dmm", File.ReadAllText(Path.Combine(_root, "map_jsons", "taken.json")));
        }

        // the server takes the new station by its json's name, from that folder, and its arrival is seated in the game's arrivals shuttle
        if (EditorFeed.GameMaps is not { } game)
            return; // the converted maps are not here (tools/convert-maps.sh)
        foreach (string shuttle in Directory.EnumerateFiles(Path.Combine(game, "shuttles"), "*", SearchOption.AllDirectories))
        {
            string copy = Path.Combine(_root, "shuttles", Path.GetRelativePath(Path.Combine(game, "shuttles"), shuttle));
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(shuttle, copy);
        }
        var config = new ServerConfig { Seed = 1, Console = false, StatusEveryTicks = int.MaxValue, Map = "fort_nine", MapsFolder = _root, JoinAsArrival = true };
        using var w = new Integration.RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 }, config, strict: false);
        AssertSeated(w);
    }

    // A map of no maps folder is played from a scratch folder (EditorFeed.MapsOf) that has the game's own shuttles, in a world large enough for the
    // stand-ins of CentCom's docks however small the map is: the stock arrivals shuttle docks, and an arrival sits in it.
    [Fact]
    public void ASmallLoneMapIsPlayedWithTheGamesShuttles()
    {
        if (EditorFeed.GameMaps == null)
            return; // the converted maps are not here (tools/convert-maps.sh)
        MapDocument map = MapDocument.Create(48, 48, 1, EditorSession.NewMapMembers);
        Build(map);
        Assert.True(MapChecks.HasArrival(map));
        (string folder, string name, string scratch) = EditorFeed.MapsOf(map, null, "untitled" + MapText.Extension);
        try
        {
            Assert.Equal(folder, scratch);
            Assert.True(File.Exists(Path.Combine(scratch, "shuttles", "arrival", "arrival_box" + MapText.Extension)));
            var config = new ServerConfig { Seed = 1, Console = false, StatusEveryTicks = int.MaxValue, Map = name, MapsFolder = folder, JoinAsArrival = true };
            using var w = new Integration.RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 }, config, strict: false);
            AssertSeated(w);
            using (w.World.Enter())
                Assert.IsType<turf_open_floor_iron>(Anomaly.Core.Tg.Builtins.locate(6, 5, 1)); // (the map is where it was built: the world grew beside it)
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }
}
