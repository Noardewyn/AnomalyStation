using System.Diagnostics;
using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// The map editor's Play (docs/ai/specs/map-editor.md 5.7): a round of the game's own server on the map as it is, the changes that are not saved included,
/// which the mapper joins at a tile of the map as an admin; and the round inside the program, on its own thread, joined over the real network.
/// </summary>
public sealed class PlayWorldTests : IDisposable
{
    private const string Floor = "/turf/open/floor/iron | /area/station/hallway/primary/central";
    private static readonly (int X, int Y, int Z) At = (6, 5, 1);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "anomaly-play-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // A floor of five by five tiles in the middle of a map of space: a map with no landmark to join at.
    private static void LayFloor(MapDocument map)
        => map.Edit("floor", (from x in Enumerable.Range(4, 5) from y in Enumerable.Range(3, 5) select (x, y, 1, map.Model(Floor))).ToList());

    private static void Wait(Func<bool> done, int seconds, Func<string> why)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(watch.Elapsed.TotalSeconds < seconds, "not after " + seconds + " s: " + why());
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void APlayerStartsOnAFloorAndNotOnSpaceAWallOrAHole()
    {
        MapDocument map = MapDocument.Create(2, 2, 1, EditorSession.NewMapMembers);
        Assert.False(MapEdits.Standable(map[1, 1, 1])); // space
        Assert.True(MapEdits.Standable(map.Model("/obj/structure/table | /turf/open/floor/iron | /area/space")));
        Assert.True(MapEdits.Standable(map.Model("/turf/open/misc/asteroid | /area/space")));
        Assert.False(MapEdits.Standable(map.Model("/turf/closed/wall | /area/space")));
        Assert.False(MapEdits.Standable(map.Model("/turf/open/openspace | /area/space")));
        Assert.False(MapEdits.Standable(map.Model("/turf/open/lava/smooth | /area/space")));
        Assert.False(MapEdits.Standable(map.Model("/turf/open/space/basic | /area/space")));
    }

    [Fact]
    public void ARoundTakesTheMapsUnsavedTextItsJoinTileAndTheLocalAdmin()
    {
        // a maps folder of tg's layout with a map of space, saved, and a floor laid in the open map that is not saved
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 1);
        session.Save();
        string saved = File.ReadAllText(session.FilePath);
        LayFloor(session.Document);
        Assert.Equal((_root, "play", null), EditorFeed.MapsOf(session.Document, session.FilePath, "Play" + MapText.Extension));

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig
        {
            Seed = 1,
            Port = 7777,
            Console = false,
            StatusEveryTicks = int.MaxValue,
            Map = "play",
            MapsFolder = _root,
            MapTexts = new Dictionary<string, string> { [session.FilePath] = session.Document.ToText() },
            JoinAt = At,
            LocalAdmin = true,
        };
        var output = new StringWriter();
        using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
        using var mapper = new GameClient(net.CreateClient(), "127.0.0.1", 7777, "Mapper");
        host.Start();
        for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.True(host.JoinsOpen, output.ToString());
        mapper.Connect();
        for (int tick = 0; mapper.OwnEntity == 0 && tick < 400; tick++)
        {
            host.RunTick();
            net.Advance();
            mapper.Poll(tick * 0.05);
        }
        Assert.True(mapper.OwnEntity != 0, mapper.Phase + " " + mapper.CloseReason + "\n" + output);

        // the mapper stands on the tile asked for, on the floor the file has not got
        Assert.True(mapper.Replica.TryGet(mapper.OwnEntity, out ReplicaEntity own));
        Assert.Equal(At, (own.State.X, own.State.Y, own.State.Z));
        Assert.Equal("/turf/open/floor/iron", mapper.Replica.TurfTypeAt(At.X, At.Y, At.Z));
        Assert.Equal(saved, File.ReadAllText(session.FilePath));

        // and holds tg's !localhost! rank
        using (host.World.Enter())
            Assert.NotNull(((client)GLOB.directory["mapper"]).holder);
        Assert.Contains("ENABLE_LOCALHOST_RANK", ServerHost.TgConfigText(config));
        Assert.DoesNotContain("ENABLE_LOCALHOST_RANK", ServerHost.TgConfigText(new ServerConfig()));
    }

    // The map's own settings (5.10): a round has the station's name, the air a room starts with, the sun where the map fixes it, and its client
    // the sky and the sun; and the settings that are not saved reach the round with the map json's text.
    [Fact]
    public void ARoundHasTheMapsOwnSettings()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        string jsonPath = Path.Combine(_root, "map_jsons", "play.json");
        File.WriteAllText(jsonPath, "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 1);
        LayFloor(session.Document);
        session.Save();
        Assert.Equal(jsonPath, session.JsonPath);
        Assert.Equal(MapSettings.Default, session.Settings);
        Assert.False(session.NeedsSave);

        // set and not saved: the json's file is as it was
        session.SetSettings(session.Settings with
        {
            StationName = "Port Test", Sky = MapSkyKind.Colours, SkyTop = "#102030", SunEnergy = 2f, SunAzimuth = 90f, AirInside = "o2=10;n2=10;TEMP=250",
        });
        session.SetLevelTrait(1, "Weather_Snowstorm", System.Text.Json.Nodes.JsonValue.Create(true));
        Assert.True(session.NeedsSave);
        Assert.Equal("true", session.LevelTrait(1, "Weather_Snowstorm"));
        Assert.DoesNotContain("anomaly", File.ReadAllText(jsonPath));

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig
        {
            Seed = 1, Port = 7777, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = At, LocalAdmin = true,
            MapTexts = new Dictionary<string, string> { [session.FilePath] = session.Document.ToText(), [jsonPath] = session.JsonText },
        };
        var output = new StringWriter();
        using (var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output))
        using (var mapper = new GameClient(net.CreateClient(), "127.0.0.1", 7777, "Mapper"))
        {
            host.Start();
            for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
            {
                host.RunTick();
                net.Advance();
            }
            Assert.True(host.JoinsOpen, output.ToString());
            mapper.Connect();
            for (int tick = 0; mapper.OwnEntity == 0 && tick < 400; tick++)
            {
                host.RunTick();
                net.Advance();
                mapper.Poll(tick * 0.05);
            }
            Assert.True(mapper.OwnEntity != 0, mapper.Phase + " " + mapper.CloseReason + "\n" + output);

            using (host.World.Enter())
            {
                Assert.Equal("Port Test", station_name());
                var floor = (turf_open)Builtins.locate(At.X, At.Y, At.Z);
                Assert.Equal("o2=10;n2=10;TEMP=250", floor.initial_gas_mix); // (its air is gone by now: the floor lies open to space)
                Assert.Equal(90f, SSsun.azimuth, 3);
                Assert.True(Anomaly.Core.DmShim.Dm.truthy(SSmapping.level_trait(1, "Weather_Snowstorm")));
            }
            MapLook look = mapper.Map.Look;
            Assert.NotNull(look);
            Assert.Equal((MapLook.SkyColours, 0x102030u, 2f), (look.Sky, look.SkyTop, look.SunEnergy));
        }

        // saved: the json holds the settings and the trait, tg's keys as they were, and the session opened again reads them
        session.Save();
        Assert.False(session.NeedsSave);
        string saved = File.ReadAllText(jsonPath);
        Assert.Contains("\"station_name\": \"Port Test\"", saved);
        Assert.Contains("\"Weather_Snowstorm\": true", saved);
        Assert.Contains("\"map_path\": \"map_files/Play\"", saved);
        using EditorSession again = EditorSession.Open(session.FilePath);
        Assert.Equal(session.Settings, again.Settings);
        Assert.Equal("true", again.LevelTrait(1, "Weather_Snowstorm"));
        // the weather taken away again leaves tg's default traits: the key goes
        again.SetLevelTrait(1, "Weather_Snowstorm", null);
        again.Save();
        Assert.DoesNotContain("traits", File.ReadAllText(jsonPath));
    }

    // The gas the editor sets on one tile of a closed room (5.23) spreads over the room in a round, as tg's air does.
    [Fact]
    public void TheGasOfOneTileSpreadsOverItsRoomInARound()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 1);
        MapDocument map = session.Document;
        // a room of three by three plating tiles in walls, in a station area (one in the area of space has no ceiling by the game's rule, DV183: its air
        // goes out to space), the gas on one corner
        const string Area = " | /area/station/engineering/atmos";
        map.Edit("room", (from x in Enumerable.Range(3, 5) from y in Enumerable.Range(3, 5)
                          select (x, y, 1, map.Model((x is 3 or 7 || y is 3 or 7 ? "/turf/closed/wall" : "/turf/open/floor/plating") + Area))).ToList());
        map.Edit("gas", new[] { (6, 4, 1, MapEdits.WithGasMix(map, map[6, 4, 1], "o2=10000;n2=82;TEMP=293.15")) });
        session.Save();

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig { Seed = 1, Port = 7777, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = (5, 5, 1) };
        var output = new StringWriter();
        using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
        host.Start();
        for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.True(host.JoinsOpen, output.ToString());
        double Moles(int x, int y)
        {
            using (host.World.Enter())
                return Anomaly.Core.DmShim.Dm.num(((turf_open)Builtins.locate(x, y, 1)).air.total_moles());
        }
        // (tg's air setup at the round's start shares a closed room's gas out already: 10 000 mol of oxygen and the plating's own air, over nine tiles)
        for (int tick = 0; tick < 400; tick++)
            host.RunTick();
        double gassed = Moles(6, 4), far = Moles(4, 6);
        Assert.True(far > 1000 && Math.Abs(gassed - far) < 1, Inv($"the far corner {far:0} mol, the gassed tile {gassed:0} mol"));
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    // A map of no maps folder keeps its settings in a json of its own, beside its file.
    [Fact]
    public void ALoneMapKeepsItsSettingsBesideItsFile()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "lone" + MapText.Extension);
        using (EditorSession session = EditorSession.New(file, 8, 8, 2))
        {
            session.Save();
            session.SetSettings(session.Settings with { StationName = "Lone", Sun = false });
            session.SetLevelTrait(2, "Gravity", System.Text.Json.Nodes.JsonValue.Create(1));
            session.Save();
        }
        string json = File.ReadAllText(Path.ChangeExtension(file, ".json"));
        Assert.Contains("\"map_file\": \"lone.dmm\"", json);
        using EditorSession again = EditorSession.Open(file);
        Assert.Equal("Lone", again.Settings.StationName);
        Assert.False(again.Settings.Sun);
        Assert.Equal("1", again.LevelTrait(2, "Gravity"));
        Assert.Null(again.LevelTrait(1, "Gravity"));
        // a json that cannot be read is told, its settings are the game's own, and a save leaves the file as a hand wrote it
        string broken = Path.Combine(_root, "broken" + MapText.Extension);
        File.Copy(file, broken);
        File.WriteAllText(Path.ChangeExtension(broken, ".json"), "{ \"map_name\": oops");
        using (EditorSession unread = EditorSession.Open(broken))
        {
            Assert.True(unread.JsonUnreadable);
            Assert.Equal(MapSettings.Default, unread.Settings);
            unread.SetSettings(unread.Settings with { StationName = "Lost" });
            unread.Save();
            Assert.Equal("{ \"map_name\": oops", File.ReadAllText(Path.ChangeExtension(broken, ".json")));
        }

        // saved under another name, the settings go along
        string other = Path.Combine(_root, "other", "copy" + MapText.Extension);
        again.SaveAs(other);
        Assert.Contains("\"map_file\": \"copy.dmm\"", File.ReadAllText(Path.ChangeExtension(other, ".json")));
        Assert.Equal("Lone", EditorSession.Open(other).Settings.StationName);
    }

    // A new map of several levels (5.10; the owner's test station, 2026-11-17): its levels above the first are open space and have tg's Baseturf
    // trait of it, so nothing there is plain space that stops a fall and a look. A level's base turf and the map's mine (tg's minetype) are written
    // to the json and read back, Play's text has them before a save, and undo takes them back.
    [Fact]
    public void ANewMapsUpperLevelsAreOpenSpaceAndItsBaseTurfAndMineAreKeptInTheJson()
    {
        Directory.CreateDirectory(_root);
        // a map of one level is all space with the game's own of everything: it needs no json
        string flat = Path.Combine(_root, "flat" + MapText.Extension);
        using (EditorSession one = EditorSession.New(flat, 4, 4, 1))
        {
            one.Save();
            Assert.False(File.Exists(Path.ChangeExtension(flat, ".json")));
        }

        const string OpenSpace = "\"/turf/open/space/openspace\"", OpenAir = "/turf/open/openspace";
        string file = Path.Combine(_root, "decks" + MapText.Extension), json = Path.ChangeExtension(file, ".json");
        using (EditorSession session = EditorSession.New(file, 6, 5, 3))
        {
            MapDocument map = session.Document;
            Assert.Same(map.Model(EditorSession.NewMapMembers), map[6, 5, 1]);
            Assert.Same(map.Model(EditorSession.NewMapUpperMembers), map[1, 1, 2]);
            Assert.Same(map.Model(EditorSession.NewMapUpperMembers), map[6, 5, 3]);
            Assert.Equal((null, OpenSpace, OpenSpace), (session.LevelTrait(1, "Baseturf"), session.LevelTrait(2, "Baseturf"), session.LevelTrait(3, "Baseturf")));
            Assert.False(map.CanUndo); // (the map is new with them: no step to take back)
            session.Save();
            Assert.Contains("\"Baseturf\": \"/turf/open/space/openspace\"", File.ReadAllText(json));

            // set and not saved: Play's text has them, the file has not
            session.SetLevelTrait(3, "Baseturf", System.Text.Json.Nodes.JsonValue.Create(OpenAir));
            session.SetJsonString("minetype", "none");
            Assert.Equal("none", session.JsonString("minetype"));
            Assert.Contains("\"minetype\": \"none\"", session.JsonText);
            Assert.Contains("\"Baseturf\": \"/turf/open/openspace\"", session.JsonText);
            Assert.DoesNotContain("minetype", File.ReadAllText(json));

            Assert.True(map.Undo()); // the mine
            Assert.Null(session.JsonString("minetype"));
            Assert.True(map.Undo()); // the base turf
            Assert.Equal(OpenSpace, session.LevelTrait(3, "Baseturf"));
            Assert.False(session.NeedsSave);
            Assert.True(map.Redo() && map.Redo());
            session.Save();
        }
        string saved = File.ReadAllText(json);
        Assert.Contains("\"minetype\": \"none\"", saved);
        Assert.Contains("\"Baseturf\": \"/turf/open/openspace\"", saved);

        using EditorSession again = EditorSession.Open(file);
        Assert.Equal("none", again.JsonString("minetype"));
        Assert.Equal((OpenSpace, "\"" + OpenAir + "\""), (again.LevelTrait(2, "Baseturf"), again.LevelTrait(3, "Baseturf")));
        Assert.Same(again.Document.Model(EditorSession.NewMapUpperMembers), again.Document[3, 3, 2]);
        // tg's own of both again: the keys go
        again.SetJsonString("minetype", null);
        again.SetLevelTrait(2, "Baseturf", null);
        again.SetLevelTrait(3, "Baseturf", null);
        Assert.DoesNotContain("minetype", again.JsonText);
        Assert.DoesNotContain("traits", again.JsonText);

        // a shuttle's map is tg's noop tile on every level, and has no trait
        using EditorSession shuttle = EditorSession.New(Path.Combine(_root, "shuttle" + MapText.Extension), 3, 4, 2, EditorSession.NewShuttleMembers);
        Assert.Same(shuttle.Document.Default, shuttle.Document[1, 1, 2]);
        Assert.Null(shuttle.LevelTrait(2, "Baseturf"));
    }

    // A level's empty tile (5.17; the owner's test station, 2026-11-17): where a tool takes a tile or its turf away it leaves the map's default tile
    // with the level's base turf, so a tile emptied on a level over another is open space again and not plain space.
    [Fact]
    public void AToolLeavesTheLevelsEmptyTileWhereItTakesATileAway()
    {
        Directory.CreateDirectory(_root);
        const string Hall = " | /area/station/hallway/primary/central";
        using EditorSession session = EditorSession.New(Path.Combine(_root, "decks" + MapText.Extension), 8, 8, 2);
        MapDocument map = session.Document;
        MapModel space = map.Default, open = map.Model(EditorSession.NewMapUpperMembers);
        Assert.Same(space, map.Empty(1));
        Assert.Same(open, map.Empty(2));
        Assert.Same(space, map.Empty(3)); // (no such level)

        // the erase tool's layers: a floor tile leaves its plating, the plating the level's empty tile, and there is nothing to take from that
        MapModel floor = map.Model(Floor), plating = MapEdits.Peel(map, floor);
        Assert.Same(space, MapEdits.Peel(map, plating, null, map.Empty(1)));
        Assert.Same(open, MapEdits.Peel(map, plating, null, map.Empty(2)));
        Assert.Same(open, MapEdits.Peel(map, open, null, map.Empty(2)));
        // a floor taken whole from under it; what stands there stays
        Assert.Same(open, MapEdits.Unturfed(map, floor, map.Empty(2)));
        Assert.Equal("/obj/structure/table | " + EditorSession.NewMapUpperMembers, MapEdits.Unturfed(map, map.Model("/obj/structure/table | " + Floor), map.Empty(2)).Text);
        // the Tile panel's Delete on the turf alone: the area stays
        Assert.Equal(EditorSession.OpenSpace + Hall, MapEdits.Remove(map, floor, 0, map.Empty(2)).Text);
        Assert.Equal("/turf/open/space/basic" + Hall, MapEdits.Remove(map, floor, 0).Text);

        // the place a moved or a turned selection leaves
        map.Edit("floors", new[] { (2, 2, 1, floor), (2, 2, 2, floor), (3, 2, 2, map.Model("/turf/closed/wall" + Hall)) });
        Assert.Contains((2, 2, 1, space), MapEdits.Move(map, 2, 2, 2, 2, 1, 3, 0));
        Assert.Contains((2, 2, 2, open), MapEdits.Move(map, 2, 2, 2, 2, 2, 3, 0));
        Assert.Contains((2, 2, 2, open), MapTurns.Turn(map, new[] { (2, 2), (3, 2) }, 2, clockwise: true));

        // another base turf holds for the next erase, undo takes it back, and a level that names none leaves the default tile
        session.SetLevelTrait(2, "Baseturf", System.Text.Json.Nodes.JsonValue.Create("/turf/open/openspace"));
        Assert.Equal("/turf/open/openspace | /area/space", map.Empty(2).Text);
        Assert.True(map.Undo());
        Assert.Same(open, map.Empty(2));
        session.SetLevelTrait(2, "Baseturf", null);
        Assert.Same(space, map.Empty(2));

        // a shuttle's map leaves tg's noop tile on every level, whatever its json says; and a document that nobody holds its default tile
        using EditorSession shuttle = EditorSession.New(Path.Combine(_root, "shuttle" + MapText.Extension), 3, 4, 2, EditorSession.NewShuttleMembers);
        shuttle.SetLevelTrait(2, "Baseturf", System.Text.Json.Nodes.JsonValue.Create(EditorSession.OpenSpace));
        Assert.Same(shuttle.Document.Default, shuttle.Document.Empty(2));
        MapDocument alone = MapDocument.Create(2, 2, 2, EditorSession.NewMapMembers);
        Assert.Same(alone.Default, alone.Empty(2));
    }

    // The round Play starts (PlayWorld's own server config) has what is set and not saved: the upper level's base turf is what a turf scraped away
    // there becomes, and a map whose mine is none loads no mining level, where tg's own loads the maps folder's Lavaland.
    [Fact]
    public void ARoundPlayStartsHasTheLevelsBaseTurfAndTheMapsMine()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        // a stand-in for tg's Lavaland in the maps folder: the mining level of a map that names no mine
        using (EditorSession lavaland = EditorSession.New(Path.Combine(_root, "map_files", "Mining", "Lavaland" + MapText.Extension), 12, 10, 1))
            lavaland.Save();
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 2);
        LayFloor(session.Document);
        session.Document.Edit("deck", new[] { (At.X, At.Y, 2, session.Document.Model(Floor)) });
        session.Save();

        (string BaseTurf, Type Scraped, string Mine, bool MiningLevel) Round()
        {
            (ServerConfig config, string scratch) = PlayWorld.ConfigOf(session.Document, session.FilePath, "Play" + MapText.Extension, At, session.JsonText);
            Assert.Null(scratch); // (a map of a maps folder is loaded from it)
            var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
            var output = new StringWriter();
            using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
            host.Start();
            for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
            {
                host.RunTick();
                net.Advance();
            }
            Assert.True(host.JoinsOpen, output.ToString());
            using (host.World.Enter())
            {
                Anomaly.Core.Kernel.Waiting.NoSleep(Builtins.locate(At.X, At.Y, 2).ChangeTurf(typeof(turf_baseturf_bottom)));
                return (SSmapping.level_trait(2, "Baseturf")?.ToString(), Builtins.locate(At.X, At.Y, 2).GetType(), SSmapping.current_map.minetype,
                    Anomaly.Core.DmShim.Dm.each<object>(SSmapping.levels_by_trait("Mining")).Any());
            }
        }

        // the new map as it is: open space under what is scraped away on the upper level, and tg's own mine
        Assert.Equal(("/turf/open/space/openspace", typeof(turf_open_space_openspace), "lavaland", true), Round());

        // set and not saved
        session.SetLevelTrait(2, "Baseturf", System.Text.Json.Nodes.JsonValue.Create("/turf/open/openspace"));
        session.SetJsonString("minetype", "none");
        Assert.True(session.NeedsSave);
        Assert.Equal(("/turf/open/openspace", typeof(turf_open_openspace), "none", false), Round());
    }

    // The settings' changes are steps of the map's history (5.10): undo takes them back among the tiles' steps, and a save writes what is there then.
    [Fact]
    public void ASettingIsTakenBackAndDoneAgainWithTheTiles()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "undo" + MapText.Extension);
        using EditorSession session = EditorSession.New(file, 8, 8, 2);
        double now = 0;
        session.Clock = () => now;
        session.Save();
        MapDocument map = session.Document;

        session.SetSettings(session.Settings with { StationName = "First" });
        now = 5;
        map.Edit("Floor", new[] { (1, 1, 1, map.Model("/turf/open/floor/iron | /area/space")) });
        session.SetLevelTrait(2, "Gravity", System.Text.Json.Nodes.JsonValue.Create(1));
        // a slider dragged: three values within a second are one step
        now = 10;
        session.SetSettings(session.Settings with { SunEnergy = 2f });
        now = 10.4;
        session.SetSettings(session.Settings with { SunEnergy = 3f });
        now = 10.8;
        session.SetSettings(session.Settings with { SunEnergy = 4f });
        Assert.Equal("Map setting: SunEnergy", map.UndoLabel);

        Assert.True(map.Undo());
        Assert.Equal(MapSettings.Default.SunEnergy, session.Settings.SunEnergy);
        Assert.Equal("1", session.LevelTrait(2, "Gravity"));
        Assert.True(map.Undo()); // the level's gravity
        Assert.Null(session.LevelTrait(2, "Gravity"));
        Assert.True(map.Undo()); // the floor
        Assert.Equal("First", session.Settings.StationName);
        Assert.True(map.Undo()); // the station's name
        Assert.Null(session.Settings.StationName);
        Assert.False(session.NeedsSave);

        Assert.True(map.Redo() && map.Redo() && map.Redo() && map.Redo());
        Assert.Equal(4f, session.Settings.SunEnergy);
        Assert.Equal("1", session.LevelTrait(2, "Gravity"));
        Assert.True(session.NeedsSave);
        Assert.True(map.Undo()); // the sun's energy again: the save writes the rest
        session.Save();
        using EditorSession again = EditorSession.Open(file);
        Assert.Equal("First", again.Settings.StationName);
        Assert.Equal(MapSettings.Default.SunEnergy, again.Settings.SunEnergy);
        Assert.Equal("1", again.LevelTrait(2, "Gravity"));
    }

    // A shuttle made in the editor (5.11): a map of the maps folder's shuttles with no template type of its own has a template made from its file, with the
    // data of the json beside it, so a map's shuttle slot can name it.
    [Fact]
    public void AShuttleOfTheMapsFolderHasATemplateWithNoTypeOfItsOwn()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"),
            "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\", \"shuttles\": {\"cargo\": \"cargo_mine\"}}");
        using (EditorSession station = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 1))
        {
            LayFloor(station.Document);
            station.Save();
            Assert.Equal("cargo_mine", station.JsonString("shuttles", "cargo"));
            Assert.Equal(_root, station.MapsFolder);
        }
        // the shuttle: three by four tiles of its own floor with its one port, and its data
        string file = Path.Combine(_root, "shuttles", "cargo", "cargo_mine" + MapText.Extension);
        using (EditorSession shuttle = EditorSession.New(file, 3, 4, 1, EditorSession.NewShuttleMembers))
        {
            MapDocument map = shuttle.Document;
            map.Edit("hull", (from x in Enumerable.Range(1, 3) from y in Enumerable.Range(1, 4) select (x, y, 1, map.Model("/turf/open/floor/mineral/titanium/blue | /area/shuttle/supply"))).ToList());
            map.Edit("port", new[] { (2, 1, 1, map.Model("/obj/docking_port/mobile/supply{dir = 1} | /turf/open/floor/mineral/titanium/blue | /area/shuttle/supply")) });
            Assert.Equal((new DockSize(3, 4, 1, 0), 1), MapDocks.ShuttleOf(map).Value);
            shuttle.SetSettings(shuttle.Settings with { ShuttleName = "Mine Hauler", ShuttleDescription = "It hauls.", ShuttleCost = 1500f });
            shuttle.Save();
        }
        Assert.Contains("\"name\": \"Mine Hauler\"", File.ReadAllText(Path.ChangeExtension(file, ".json")));

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig { Seed = 1, Port = 7777, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = At };
        var output = new StringWriter();
        using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
        host.Start();
        for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.True(host.JoinsOpen, output.ToString());
        using (host.World.Enter())
        {
            var template = Assert.IsType<datum_map_template_shuttle>(SSmapping.shuttle_templates["cargo_mine"]);
            Assert.Equal(("cargo", "mine", "Mine Hauler", "It hauls.", 1500f), ((string)template.port_id, (string)template.suffix, (string)template.name, (string)template.description, template.credit_cost));
            Assert.Equal("_maps/shuttles/cargo/cargo_mine.dmm", (string)template.mappath);
            Assert.Equal((3f, 4f), (template.width, template.height));
        }
    }

    // The ceilings a map sets (5.12): a turf's ceiling var is read when the map is loaded, in place of the game's rule.
    [Fact]
    public void AMapSetsTheCeilingOverATurf()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 1);
        MapDocument map = session.Document;
        LayFloor(map);
        // a tile open to the sky, one under glass, one under a catwalk, one with a ceiling that is none (the rule holds), and the rule's plating
        map.Edit("ceilings", new[]
        {
            (4, 3, 1, MapEdits.WithCeiling(map, map[4, 3, 1], "none")),
            (5, 3, 1, MapEdits.WithCeiling(map, map[5, 3, 1], "/turf/open/floor/glass")),
            (6, 3, 1, MapEdits.WithCeiling(map, map[6, 3, 1], "catwalk")),
            (7, 3, 1, MapEdits.WithCeiling(map, map[7, 3, 1], "/obj/structure/table")),
        });
        Assert.Equal("none", MapEdits.CeilingOf(map[4, 3, 1]));
        Assert.Null(MapEdits.CeilingOf(map[8, 3, 1]));
        Assert.Contains("/turf/open/floor/iron{ceiling = \"none\"}", map[4, 3, 1].Text);
        Assert.Same(map[8, 3, 1], MapEdits.WithCeiling(map, map[8, 3, 1], null));
        session.Save();

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig { Seed = 1, Port = 7777, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = At };
        var output = new StringWriter();
        using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
        host.Start();
        for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.True(host.JoinsOpen, output.ToString());
        CeilingSystem ceilings = CeilingSystem.Of(host.World);
        Assert.False(ceilings.At(4, 3, 1).Exists);
        Assert.Equal("/turf/open/floor/glass", ceilings.At(5, 3, 1).Turf);
        Assert.Equal(Anomaly.Core.Motion.CeilingLattice.Catwalk, ceilings.At(6, 3, 1).Lattice);
        Assert.Equal(Anomaly.Core.Motion.CeilingState.Plating, ceilings.At(7, 3, 1).Turf);
        Assert.Equal(Anomaly.Core.Motion.CeilingState.Plating, ceilings.At(8, 3, 1).Turf);
        Assert.False(ceilings.At(1, 1, 1).Exists); // space
    }

    // The owner (2026-11-17): from under a level a floor tile is to be placed on the side looked at, under the plating. A tile under a level above has
    // no ceiling of its own (D325): its map's word lays a floor tile under the plating of the turf over it (DV507), shown while that turf has plating.
    [Fact]
    public void UnderALevelAboveAMapLaysATileUnderItsPlating()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        using EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 2);
        MapDocument map = session.Document;
        LayFloor(map);
        const string Carpet = "/turf/open/floor/carpet", Hall = " | /area/station/hallway/primary/central";
        // over the lower floor: an iron floor, a glass floor, open space, a wall; and under each a carpet tile is asked for
        map.Edit("decks", new[]
        {
            (4, 3, 2, map.Model(Floor)),
            (5, 3, 2, map.Model("/turf/open/floor/glass" + Hall)),
            (7, 3, 2, map.Model("/turf/closed/wall" + Hall)),
            (8, 3, 2, map.Model(Floor)),
            (4, 3, 1, MapEdits.WithCeiling(map, map[4, 3, 1], Carpet)),
            (5, 3, 1, MapEdits.WithCeiling(map, map[5, 3, 1], Carpet)),
            (6, 3, 1, MapEdits.WithCeiling(map, map[6, 3, 1], Carpet)),
            (7, 3, 1, MapEdits.WithCeiling(map, map[7, 3, 1], Carpet)),
            (8, 3, 1, MapEdits.WithCeiling(map, map[8, 3, 1], "none")), // (a word that lays no tile says nothing there)
        });
        session.Save();

        var net = new LoopbackNetwork(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
        var config = new ServerConfig { Seed = 1, Port = 7777, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = At };
        var output = new StringWriter();
        using var host = new ServerHost(config, net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: output, errors: output);
        host.Start();
        for (int tick = 0; !host.JoinsOpen && tick < 2000; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.True(host.JoinsOpen, output.ToString());
        CeilingSystem ceilings = CeilingSystem.Of(host.World);
        for (int x = 4; x <= 8; x++)
            Assert.False(ceilings.At(x, 3, 1).Exists); // no ceiling of its own under a level above: the rules of ceilings do not see the laid tile
        Assert.Equal(Carpet, ceilings.ShownAt(4, 3, 1).Turf); // under the iron floor's plating
        Assert.False(ceilings.ShownAt(5, 3, 1).Exists); // glass has no plating: it is seen through from both sides
        Assert.False(ceilings.ShownAt(6, 3, 1).Exists); // nothing over it to lie under
        Assert.Equal(Carpet, ceilings.ShownAt(7, 3, 1).Turf); // under the plating a wall stands on
        Assert.False(ceilings.ShownAt(8, 3, 1).Exists);
        Assert.True(ceilings.At(4, 3, 2).Exists); // the top level keeps its own ceilings

        // in a round: the floor above made glass shows the tile no more
        using (host.World.Enter())
            Anomaly.Core.Kernel.Waiting.NoSleep(Anomaly.Core.Tg.Builtins.locate(4, 3, 2).ChangeTurf(typeof(turf_open_floor_glass)));
        Assert.False(ceilings.ShownAt(4, 3, 1).Exists);
        // an edit of the map editor's world: a floor laid over the open tile shows the tile laid under it, and the wall taken away shows its no more
        Anomaly.Core.Kernel.DmTask edit = host.SetMapTiles(new[] { (6, 3, 2, map.Model("/turf/open/floor/plating" + Hall)), (7, 3, 2, map.Default) });
        for (int tick = 0; !edit.IsCompleted && tick < 400; tick++)
        {
            host.RunTick(); // (the replication reads the shown ceilings between the world's steps)
            net.Advance();
        }
        Assert.True(edit.IsCompleted, output.ToString());
        Assert.Equal(Carpet, ceilings.ShownAt(6, 3, 1).Turf);
        Assert.False(ceilings.ShownAt(7, 3, 1).Exists);
        // and an edit of the lower tile takes the laid tile off
        edit = host.SetMapTiles(new[] { (6, 3, 1, MapEdits.WithCeiling(map, map[6, 3, 1], null)) });
        for (int tick = 0; !edit.IsCompleted && tick < 400; tick++)
        {
            host.RunTick();
            net.Advance();
        }
        Assert.False(ceilings.ShownAt(6, 3, 1).Exists);
    }

    // The owner (2026-11-17): in a round too. A player under a level above lays a floor tile under the plating of the turf over a cell, from
    // below, as tg's tile is laid on plating, and pries it off with a crowbar; the floor on top of that plating stays as it is.
    [Fact]
    public void InARoundAFloorTileIsLaidUnderThePlatingOfTheLevelAboveAndPriedOff()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        const string Carpet = "/turf/open/floor/carpet", Hall = " | /area/station/hallway/primary/central";
        using (EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 12, 10, 2))
        {
            MapDocument map = session.Document;
            LayFloor(map);
            // over the player's tile and the one east of it an iron floor, west of it a glass floor, and open space over the rest
            map.Edit("deck", new[] { (6, 5, 2, map.Model(Floor)), (7, 5, 2, map.Model(Floor)), (5, 5, 2, map.Model("/turf/open/floor/glass" + Hall)) });
            session.Save();
        }
        var config = new ServerConfig { Seed = 1, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAt = At, MoveSpeedScale = 1f };
        using var w = new Integration.RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 }, config, strict: false);
        var alice = w.Join("Alice");
        mob_living mob = w.MobOf(alice);
        CeilingSystem ceilings = CeilingSystem.Of(w.World);
        obj_item_stack_tile_carpet tiles;
        using (w.World.Enter())
        {
            Assert.Equal(At, ((int)mob.x, (int)mob.y, (int)mob.z));
            tiles = Anomaly.Core.DmShim.Dm.@new<obj_item_stack_tile_carpet>(Anomaly.Core.Tg.Builtins.locate(At.X, At.Y, At.Z), 5);
            Anomaly.Core.Kernel.Waiting.NoSleep(mob.put_in_hands(tiles));
        }
        w.Run(5);
        Assert.False(alice.Session.Replica.CeilingAt(6, 5, 1).Exists);

        using (w.World.Enter())
            Assert.Equal(("plating", "glass floor", "none"), (ceilings.NameAt(6, 5, 1), ceilings.NameAt(5, 5, 1), ceilings.NameAt(6, 4, 1)));

        // with something in the hand a look up picks the cell's ceiling (GameScene.HoldsACeilingTool): a tile used on it is laid under the plating
        alice.Session.Click(new ClickTarget(TargetKind.Ceiling, 0, 6, 5, 1), ClickButton.Left, ClickModifiers.None);
        w.Run(10);
        Assert.Equal(Carpet, ceilings.ShownAt(6, 5, 1).Turf);
        Assert.False(ceilings.At(6, 5, 1).Exists); // still no ceiling of its own (D325)
        Assert.Equal(4, tiles.amount);
        Assert.Equal(Carpet, alice.Session.Replica.CeilingAt(6, 5, 1).Turf);
        Assert.False(alice.Session.Replica.CeilingLookAt(6, 5, 1).IsNone);
        Assert.Equal("/turf/open/floor/iron", alice.Session.Replica.TurfTypeAt(6, 5, 2)); // the floor on top stays
        // over the next tile too, within reach
        alice.Session.Click(new ClickTarget(TargetKind.Ceiling, 0, 7, 5, 1), ClickButton.Left, ClickModifiers.None);
        w.Run(15);
        Assert.Equal(Carpet, ceilings.ShownAt(7, 5, 1).Turf);
        Assert.Equal(3, tiles.amount);
        using (w.World.Enter())
            Assert.Equal("carpet", ceilings.NameAt(7, 5, 1));
        // glass and open space have no plating to lay a tile under, and a laid tile takes no second one
        foreach ((int x, int y) in new[] { (5, 5), (6, 4), (6, 5) })
        {
            alice.Session.Click(new ClickTarget(TargetKind.Ceiling, 0, x, y, 1), ClickButton.Left, ClickModifiers.None);
            w.Run(15);
        }
        Assert.False(ceilings.ShownAt(5, 5, 1).Exists);
        Assert.False(ceilings.ShownAt(6, 4, 1).Exists);
        Assert.Equal(3, tiles.amount);

        // a crowbar pries the laid tile off: it falls into the room, and the plating is bare again
        using (w.World.Enter())
        {
            qdel(tiles);
            Anomaly.Core.Kernel.Waiting.NoSleep(mob.put_in_hands(Anomaly.Core.DmShim.Dm.@new<obj_item_crowbar>(Anomaly.Core.Tg.Builtins.locate(At.X, At.Y, At.Z))));
        }
        w.Run(5);
        alice.Session.Click(new ClickTarget(TargetKind.Ceiling, 0, 6, 5, 1), ClickButton.Left, ClickModifiers.None);
        w.Run(15);
        // (and on the bare plating the crowbar does nothing: that plating is the floor of the level above)
        alice.Session.Click(new ClickTarget(TargetKind.Ceiling, 0, 6, 5, 1), ClickButton.Left, ClickModifiers.None);
        w.Run(15);
        Assert.False(ceilings.ShownAt(6, 5, 1).Exists);
        Assert.False(alice.Session.Replica.CeilingAt(6, 5, 1).Exists);
        Assert.Equal(Carpet, ceilings.ShownAt(7, 5, 1).Turf);
        using (w.World.Enter())
        {
            Assert.Equal("plating", ceilings.NameAt(6, 5, 1));
            var fallen = Anomaly.Core.DmShim.Dm.each<obj_item_stack_tile_carpet>(Anomaly.Core.Tg.Builtins.locate(6, 5, 1).contents).Single();
            Assert.Equal(1, fallen.amount);
            Assert.IsType<turf_open_floor_iron>(Anomaly.Core.Tg.Builtins.locate(6, 5, 2));
        }
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    // Play as a new arrival (5.7; the owner, 2026-11-17): with no join tile the round's player is tg's late joiner, buckled in a seat of the arrivals
    // shuttle that the map's dock loads. A map with no dock and no late-join landmark has no arrival point, and the editor says so.
    [Fact]
    public void ARoundJoinedAsANewArrivalStartsInASeatOfTheArrivalsShuttle()
    {
        Directory.CreateDirectory(Path.Combine(_root, "map_jsons"));
        File.WriteAllText(Path.Combine(_root, "map_jsons", "play.json"), "{\"version\": 1, \"map_name\": \"Play\", \"map_path\": \"map_files/Play\", \"map_file\": \"Play.dmm\"}");
        // the game's own shuttles: tg's arrivals shuttle, and those the stand-ins of CentCom's docks load
        string stock = Path.Combine(Integration.MultizDebugBreachTests.RepoMaps(), "shuttles");
        foreach (string file in Directory.EnumerateFiles(stock, "*", SearchOption.AllDirectories))
        {
            string copy = Path.Combine(_root, "shuttles", Path.GetRelativePath(stock, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(file, copy);
        }
        using (EditorSession session = EditorSession.New(Path.Combine(_root, "map_files", "Play", "Play" + MapText.Extension), 48, 48, 1))
        {
            MapDocument map = session.Document;
            LayFloor(map);
            Assert.False(MapChecks.HasArrival(map));
            // MetaStation's own dock for tg's arrivals shuttle
            map.Edit("dock", new[] { (24, 24, 1, map.Model("/obj/docking_port/stationary{dir = 8; dwidth = 3; height = 15; name = \"arrivals\"; roundstart_template = /datum/map_template/shuttle/arrival/box; shuttle_id = \"arrival_stationary\"; width = 7} | /turf/open/space | /area/space")) });
            Assert.True(MapChecks.HasArrival(map));
            session.Save();
        }
        var config = new ServerConfig { Seed = 1, Console = false, StatusEveryTicks = int.MaxValue, Map = "play", MapsFolder = _root, JoinAsArrival = true };
        using var w = new Integration.RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 }, config, strict: false);
        var alice = w.Join("Alice");
        mob_living mob = w.MobOf(alice);
        using (w.World.Enter())
        {
            Assert.IsAssignableFrom<area_shuttle_arrival>(get_area(mob));
            Assert.IsAssignableFrom<obj_structure_chair>(mob.buckled);
            Assert.NotEqual(At, ((int)mob.x, (int)mob.y, (int)mob.z));
        }
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void PlayRunsANewMapInItsOwnWorldAndStops()
    {
        MapDocument map = MapDocument.Create(12, 10, 1, EditorSession.NewMapMembers);
        LayFloor(map);
        using var play = new PlayWorld(map, null, "untitled" + MapText.Extension, At);
        string Log() => play.Failure + "\n" + string.Join("\n", play.Log.TakeLast(30));
        Wait(() => play.Ready || play.Over, 180, Log);
        Assert.True(play.Ready, Log());

        // the real client's connection, over the real network of this machine
        using var mapper = new GameClient(GameClient.CreateTransport(), "127.0.0.1", play.Port, "Mapper");
        var texts = new List<string>();
        mapper.ServerTextReceived += text => texts.Add(text.Html);
        var clock = Stopwatch.StartNew();
        mapper.Connect();
        bool Polled(Func<bool> done)
        {
            mapper.Poll(clock.Elapsed.TotalSeconds);
            return done() || mapper.Phase == FeedPhase.Closed;
        }
        Wait(() => Polled(() => mapper.OwnEntity != 0), 60, () => mapper.Phase + " " + mapper.CloseReason + "\n" + Log());
        Assert.True(mapper.OwnEntity != 0, mapper.CloseReason + "\n" + Log());
        Assert.True(mapper.Replica.TryGet(mapper.OwnEntity, out ReplicaEntity own));
        Assert.Equal(At, (own.State.X, own.State.Y, own.State.Z));
        Assert.Equal("/turf/open/floor/iron", mapper.Replica.TurfTypeAt(At.X, At.Y, At.Z));

        // the dev menu is the mapper's, and so are the server console's commands
        Wait(() => Polled(() => mapper.DevAccessAllowed == true), 20, Log);
        Assert.True(mapper.DevAccessAllowed);
        mapper.SendAction(new ActionMessage(0, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, "status"));
        Wait(() => Polled(() => texts.Any(t => t.Contains("status tick=", StringComparison.Ordinal))), 20, () => string.Join("\n", texts) + "\n" + Log());
        Assert.Contains(texts, t => t.Contains("status tick=", StringComparison.Ordinal));

        // Stop: the round ends by itself, with nothing to say
        play.Dispose();
        Wait(() => play.Over, 60, Log);
        Assert.Null(play.Failure);
        Assert.False(play.Ready);
    }
}
