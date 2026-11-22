using System.Diagnostics;
using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.World;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// The map editor's view (docs/ai/specs/map-editor.md 3): the feed that shows a document through the game's own world, with no server to run and no
/// player, an edit of the document shown by tg's template load, the session's file and the arguments.
/// </summary>
public sealed class EditorFeedTests
{
    private const string Hall = " | /area/station/hallway/primary/central";
    private const string Floor = "/turf/open/floor/iron" + Hall;

    // A room with what joins: walls, two tables, a window spawner, pipes, cables, a door, a landmark.
    private const string Room = @"anomaly-map 1
size 7 6 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 1 1 1]
1,5: /turf/closed/wall | /area/station/hallway/primary/central
2,5: /turf/closed/wall | /area/station/hallway/primary/central
3,5: /obj/machinery/door/airlock{name = ""Test Airlock""} | /turf/open/floor/plating | /area/station/hallway/primary/central
4,5: /turf/closed/wall | /area/station/hallway/primary/central
5,5: /turf/closed/wall | /area/station/hallway/primary/central
6,5: /turf/closed/wall | /area/station/hallway/primary/central
1,4: /turf/closed/wall | /area/station/hallway/primary/central
2,4: /obj/structure/cable | /turf/open/floor/plating | /area/station/hallway/primary/central
3,4: /obj/effect/landmark/start/assistant | /turf/open/floor/iron | /area/station/hallway/primary/central
4,4: /obj/structure/table | /turf/open/floor/iron | /area/station/hallway/primary/central
5,4: /obj/structure/table | /turf/open/floor/iron | /area/station/hallway/primary/central
6,4: /obj/effect/spawner/structure/window/reinforced | /turf/open/floor/plating | /area/station/hallway/primary/central
1,3: /turf/closed/wall | /area/station/hallway/primary/central
2,3: /turf/open/floor/plating | /area/station/hallway/primary/central
3,3: /obj/machinery/atmospherics/pipe/smart/simple/general/visible{dir = 4} | /turf/open/floor/plating | /area/station/hallway/primary/central
4,3: /turf/open/floor/plating | /area/station/hallway/primary/central
5,3: /obj/machinery/atmospherics/pipe/smart/simple/general/visible{dir = 4} | /turf/open/floor/plating | /area/station/hallway/primary/central
6,3: /turf/closed/wall | /area/station/hallway/primary/central
";

    private readonly ITestOutputHelper _output;

    public EditorFeedTests(ITestOutputHelper output) => _output = output;

    // Polls as the scene does, a frame at a time, until the feed plays and has no edit left to show.
    private static void Settle(EditorFeed feed, int seconds = 300)
    {
        var watch = Stopwatch.StartNew();
        float progress = 0;
        for (int frame = 0; feed.Phase != FeedPhase.Closed && (feed.Phase != FeedPhase.Playing || feed.Busy); frame++)
        {
            Assert.True(watch.Elapsed.TotalSeconds < seconds, "the editor's world did not settle in " + seconds + " s: " + feed.Phase + "\n" + string.Join("\n", feed.Log.TakeLast(20)));
            feed.Poll(frame * 0.016);
            (float now, string doing) = feed.StartProgress; // the loading screen's bar never goes back
            Assert.True(now >= progress && now <= 1f, $"the start's progress went from {progress} to {now} ({doing})");
            progress = now;
            if (feed.Phase != FeedPhase.Playing)
                Thread.Sleep(5); // the world starts on its own thread
        }
        Assert.True(feed.Phase == FeedPhase.Playing, feed.CloseReason + "\n" + string.Join("\n", feed.Log.TakeLast(20)));
        Assert.Equal(1f, feed.StartProgress.Fraction);
    }

    private static string State(EditorFeed feed, ReplicaEntity thing)
        => feed.Replica.TryGetAppearance(thing.State.Appearance, out AppearanceEntry look) ? feed.Replica.ResolveString(look.IconState) : null;

    private static List<ReplicaEntity> At(EditorFeed feed, int x, int y, int z = 1)
        => feed.Entities.Where(e => e.State.X == x && e.State.Y == y && e.State.Z == z).OrderBy(e => e.Id).ToList();

    // What the feed shows of the map's tiles, to compare a view with another: the turfs, and the things without their ids.
    private static List<string> Shown(EditorFeed feed)
    {
        MapDocument map = feed.Document;
        var shown = new List<string>();
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++)
                {
                    feed.Replica.TryGetAppearance(feed.Replica.TurfAppearance(x, y, z), out AppearanceEntry turf);
                    // tg loads the map's space as /turf/open/space/basic and makes /turf/open/space of it in any later ChangeTurf (turf.dm): the same turf to see
                    string type = feed.Replica.TurfTypeAt(x, y, z) == "/turf/open/space/basic" ? "/turf/open/space" : feed.Replica.TurfTypeAt(x, y, z);
                    shown.Add($"{x},{y},{z} {type} '{(turf == null ? null : feed.Replica.ResolveString(turf.IconState))}' solid {feed.Replica.IsSolid(x, y, z)}");
                }
            }
        }
        shown.AddRange(feed.Entities.Where(e => map.Contains(e.State.X, e.State.Y, e.State.Z))
            // tg's smart pipe takes its dir from its links, and one that lost a link keeps the last (update_pipe_icon): the links are in its state
            .Select(e => $"{e.TypePath} {e.State.X},{e.State.Y},{e.State.Z} dir {(e.TypePath.Contains("/pipe/smart", StringComparison.Ordinal) ? 0 : e.State.Dir)} '{e.State.Name}' '{State(feed, e)}'")
            .OrderBy(t => t, StringComparer.Ordinal));
        return shown;
    }

    private static void Edit(EditorFeed feed, int x, int y, string members)
    {
        feed.Document.Edit("Edit", new[] { (x, y, 1, feed.Document.Model(members)) });
        Settle(feed);
    }

    [Fact]
    public void AMapIsShownByTheGamesInitAndAnEditByItsTemplateLoad()
    {
        var watch = Stopwatch.StartNew();
        using var feed = new EditorFeed(MapDocument.Parse(Room, "room"), null, "room.as3dmap");
        Assert.Equal(FeedPhase.Idle, feed.Phase);
        feed.Poll(0);
        Assert.Equal(FeedPhase.Connecting, feed.Phase); // the world starts on another thread
        Settle(feed);
        _output.WriteLine($"the room is shown after {watch.ElapsedMilliseconds} ms: {feed.Count} things, map {feed.Map.Width}x{feed.Map.Height}x{feed.Map.Levels}");
        Assert.Equal("Anomaly Editor: room.as3dmap", feed.Description);
        Assert.Equal(0, feed.OwnEntity);
        Assert.True(feed.Map.Width >= 7 && feed.Map.Height >= 6);
        long tick = feed.Tick;
        feed.Poll(1000);
        feed.Poll(1001);
        Assert.Equal(tick, feed.Tick); // a world with nothing to show is not ticked: nothing of the map is played

        // the game's init: the turfs, the door by its name, the spawner's window and grille, the pipes, the landmark that stays to be seen (DV504)
        Assert.Equal("/turf/closed/wall", feed.Replica.TurfTypeAt(1, 5, 1));
        Assert.True(feed.Replica.IsSolid(1, 5, 1));
        Assert.Equal("/turf/open/space/basic", feed.Replica.TurfTypeAt(7, 6, 1));
        ReplicaEntity airlock = Assert.Single(At(feed, 3, 5));
        Assert.Equal(("/obj/machinery/door/airlock", "Test Airlock"), (airlock.TypePath, airlock.State.Name));
        Assert.Equal(new[] { "/obj/structure/grille", "/obj/structure/window/reinforced/fulltile" }, At(feed, 6, 4).Select(e => e.TypePath).OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal("/obj/effect/landmark/start/assistant", Assert.Single(At(feed, 3, 4)).TypePath);
        Assert.DoesNotContain(feed.Entities, e => e.TypePath.StartsWith("/mob", StringComparison.Ordinal)); // none of tg's roundstart mice on the bare cable
        string pipeAlone = State(feed, Assert.Single(At(feed, 3, 3)));
        string tableOfTwo = State(feed, Assert.Single(At(feed, 4, 4)));
        List<string> start = Shown(feed);

        // a thing knows its tile and its member; a spawner's things are the spawner's; a turf shows none
        Assert.True(feed.TryLocate(airlock.Id, out int x, out int y, out int z, out int member));
        Assert.Equal((3, 5, 1, 0), (x, y, z, member));
        Assert.Equal(airlock.Id, feed.ThingOf(3, 5, 1, 0));
        Assert.Equal(0, feed.ThingOf(3, 5, 1, 1));
        Assert.False(feed.TryLocate(987654321, out _, out _, out _, out _));
        foreach (ReplicaEntity made in At(feed, 6, 4))
        {
            Assert.True(feed.TryLocate(made.Id, out _, out _, out _, out member));
            Assert.Equal(0, member);
        }

        // a pipe between the two joins them: tg's own links, in the state tg gives a pipe by its links
        Edit(feed, 4, 3, "/obj/machinery/atmospherics/pipe/smart/simple/general/visible{dir = 4} | /turf/open/floor/plating" + Hall);
        Assert.Equal(tick + 20, feed.Tick);
        Assert.NotEqual(pipeAlone, State(feed, Assert.Single(At(feed, 3, 3))));
        Assert.NotEqual(pipeAlone, State(feed, Assert.Single(At(feed, 5, 3))));

        // a third table joins the two; the landmark stays before it, so the table is member 1
        Edit(feed, 3, 4, "/obj/effect/landmark/start/assistant | /obj/structure/table | /turf/open/floor/iron" + Hall);
        Assert.NotEqual(tableOfTwo, State(feed, Assert.Single(At(feed, 4, 4))));
        ReplicaEntity table = At(feed, 3, 4).Single(e => e.TypePath == "/obj/structure/table");
        Assert.True(feed.TryLocate(table.Id, out _, out _, out _, out member));
        Assert.Equal(1, member);
        Assert.Equal(table.Id, feed.ThingOf(3, 4, 1, 1));

        // a wall less: the turf, its solidity and the joints of the walls beside it
        Edit(feed, 1, 4, Floor);
        Assert.Equal("/turf/open/floor/iron", feed.Replica.TurfTypeAt(1, 4, 1));
        Assert.False(feed.Replica.IsSolid(1, 4, 1));

        // a random spawner spawns nothing and stays as the mapper placed it (DV504); a cable links to the one beside it
        Edit(feed, 2, 3, "/obj/effect/spawner/random/maintenance | /obj/structure/cable | /turf/open/floor/plating" + Hall);
        Assert.Equal(new[] { "/obj/effect/spawner/random/maintenance", "/obj/structure/cable" }, At(feed, 2, 3).Select(e => e.TypePath));
        Assert.NotEqual("l2-noconnection", State(feed, At(feed, 2, 3)[1]));

        // a tile outside the station: space becomes a floor
        Edit(feed, 7, 6, Floor);
        Assert.Equal("/turf/open/floor/iron", feed.Replica.TurfTypeAt(7, 6, 1));

        // a floor laid on bare space keeps the map's area (tg's ChangeTurf would move it to the nearstation area, which this map has not got)
        Edit(feed, 7, 1, "/turf/open/floor/iron | /area/space");
        Assert.Equal("/turf/open/floor/iron", feed.Replica.TurfTypeAt(7, 1, 1));
        Edit(feed, 6, 1, "/obj/structure/table | /turf/open/floor/iron | /area/space");
        Assert.Equal("/obj/structure/table", Assert.Single(At(feed, 6, 1)).TypePath);
        Assert.True(feed.Shown);

        // walls built by edits join each other and the walls that stood: the edited view is the view of the same map opened anew
        Edit(feed, 2, 1, "/turf/closed/wall | /area/space");
        Edit(feed, 3, 1, "/turf/closed/wall | /area/space");
        Edit(feed, 3, 2, "/turf/closed/wall | /area/space");
        Edit(feed, 2, 3, "/turf/closed/wall" + Hall);
        using (var anew = new EditorFeed(MapDocument.Parse(feed.Document.ToText(), "anew"), null, "room.as3dmap"))
        {
            Settle(anew);
            List<string> edited = Shown(feed), opened = Shown(anew);
            Assert.True(opened.SequenceEqual(edited), "shown after the edits: " + string.Join(" | ", edited.Except(opened)) + "; shown when opened: " + string.Join(" | ", opened.Except(edited)));
        }

        // taken back step by step, the view is the one it started as
        while (feed.Document.CanUndo)
            feed.Document.Undo();
        Settle(feed);
        Assert.Equal(Room, feed.Document.ToText());
        List<string> end = Shown(feed);
        Assert.True(start.SequenceEqual(end), "shown now: " + string.Join(" | ", end.Except(start)) + "; shown at the start: " + string.Join(" | ", start.Except(end)));

        // the layers (5.5). What a floor tile covers is hidden as in the game, and shown, half clear, with the under-floor layer
        Edit(feed, 4, 3, "/obj/structure/cable | /obj/structure/disposalpipe/segment{dir = 4} | /turf/open/floor/iron" + Hall);
        Assert.Empty(At(feed, 4, 3));
        feed.UnderFloor = true;
        Assert.True(feed.Busy);
        Settle(feed);
        Assert.Equal(new[] { "/obj/structure/cable", "/obj/structure/disposalpipe/segment" }, At(feed, 4, 3).Select(e => e.TypePath));
        Assert.True(feed.TryLocate(At(feed, 4, 3)[1].Id, out _, out _, out _, out member));
        Assert.Equal(1, member);
        // a cable laid under a tile while the layer is shown is seen at once, joined to the one beside it
        Edit(feed, 5, 3, "/obj/structure/cable | /turf/open/floor/iron" + Hall);
        Assert.NotEqual("l2-noconnection", State(feed, Assert.Single(At(feed, 5, 3))));
        feed.UnderFloor = false;
        Settle(feed);
        Assert.Empty(At(feed, 4, 3));
        Assert.Empty(At(feed, 5, 3));
        Assert.Single(At(feed, 2, 4)); // the cable on the plating is seen with either
        // the markers a mapper places are a layer too
        feed.Markers = false;
        Settle(feed);
        Assert.Empty(At(feed, 3, 4));
        Edit(feed, 4, 4, "/obj/effect/landmark/start/assistant | /obj/structure/table | /turf/open/floor/iron" + Hall);
        Assert.Equal("/obj/structure/table", Assert.Single(At(feed, 4, 4)).TypePath); // one placed while they are hidden is hidden
        feed.Markers = true;
        Settle(feed);
        Assert.Equal("/obj/effect/landmark/start/assistant", Assert.Single(At(feed, 3, 4)).TypePath);
        Assert.Equal(2, At(feed, 4, 4).Count);
        Assert.True(!feed.Log.Any(line => line.StartsWith("runtime at", StringComparison.Ordinal)), string.Join("\n", feed.Log.Where(line => line.StartsWith("runtime at", StringComparison.Ordinal)).Take(6)));
    }

    // DV508: the game turns a modelled thing away from the wall behind it, and leaves one whose map says the way it faces as the mapper turned it.
    [Fact]
    public void AThingFacesAsItsMapSaysAndElseAwayFromItsWall()
    {
        MapDocument map = MapDocument.Parse(Room, "room");
        using var feed = new EditorFeed(map, null, "room");
        Settle(feed);
        byte Facing(Predicate<ReplicaEntity> which) => Assert.Single(At(feed, 2, 3), which).State.Dir;

        // (2, 3) has a wall to its west and none to its north: the rule turns a closet east
        Edit(feed, 2, 3, "/obj/structure/closet | /turf/open/floor/plating" + Hall);
        Assert.Equal(4, Facing(e => e.TypePath == "/obj/structure/closet"));
        // the map turns it north, and south (tg's own way, which the rule would not leave): it faces so
        Edit(feed, 2, 3, "/obj/structure/closet{dir = 1} | /turf/open/floor/plating" + Hall);
        Assert.Equal(1, Facing(e => e.TypePath == "/obj/structure/closet"));
        Edit(feed, 2, 3, "/obj/structure/closet{dir = 2} | /turf/open/floor/plating" + Hall);
        Assert.Equal(2, Facing(e => e.TypePath == "/obj/structure/closet"));
    }

    // The preview (5.9): what a tool is about to place is made by the world where it would go, and is none of the document's.
    [Fact]
    public void APreviewIsShownByTheWorldAndIsNoneOfTheDocuments()
    {
        MapDocument map = MapDocument.Parse(Room, "room");
        using var feed = new EditorFeed(map, null, "room");
        Settle(feed);
        long revision = map.Revision;
        List<string> before = Shown(feed);

        // a third table beside the two, as a click would place it: on the tile of a pipe, whose member keeps its place
        var ghost = new MapMember("/obj/structure/table", null);
        feed.SetPreview(new[] { (3, 3, 1, MapEdits.Place(map, map[3, 3, 1], ghost)) });
        Settle(feed);
        Assert.True(feed.Previewed(3, 3, 1));
        Assert.Equal((3, 3, 1, (string)null), Assert.Single(feed.PreviewCells)); // (the tile keeps its turf: the view tints the table alone)
        ReplicaEntity table = Assert.Single(At(feed, 3, 3), e => e.TypePath == "/obj/structure/table");
        Assert.True(feed.IsPreview(table.Id));
        Assert.True(feed.TryLocate(table.Id, out _, out _, out _, out int member));
        Assert.Equal(EditorFeed.PreviewMember, member);
        ReplicaEntity pipe = Assert.Single(At(feed, 3, 3), e => e.TypePath.StartsWith("/obj/machinery/atmospherics", StringComparison.Ordinal));
        Assert.False(feed.IsPreview(pipe.Id));
        Assert.True(feed.TryLocate(pipe.Id, out _, out _, out _, out member));
        Assert.Equal(0, member);
        // the document is as it was, and the feed shows it
        Assert.Equal(revision, map.Revision);
        Assert.False(map.IsDirty);
        Assert.True(feed.Shown);

        // the preview moves: its first tile is the document's again
        feed.SetPreview(new[] { (4, 3, 1, MapEdits.Place(map, map[4, 3, 1], ghost)) });
        Settle(feed);
        Assert.DoesNotContain(At(feed, 3, 3), e => e.TypePath == "/obj/structure/table");
        Assert.Single(At(feed, 4, 3), e => e.TypePath == "/obj/structure/table");

        // an edit of a previewed tile takes its preview away: the tile is the document's
        Edit(feed, 4, 3, "/obj/structure/chair | /turf/open/floor/plating" + Hall);
        Settle(feed);
        Assert.False(feed.Previewed(4, 3, 1));
        Assert.DoesNotContain(At(feed, 4, 3), e => e.TypePath == "/obj/structure/table");
        Assert.Single(At(feed, 4, 3), e => e.TypePath == "/obj/structure/chair");
        Assert.True(map.Undo());
        Settle(feed);

        // and with no preview the view is the one before it
        feed.SetPreview(null);
        Settle(feed);
        Assert.Equal(before, Shown(feed));

        // a tile previewed over and over stays loadable: a turf is not laid on top of the ones before it (tg allows ten base turfs, and a load that
        // stacked them stopped at the eleventh)
        int runtimes = feed.Log.Count(line => line.Contains("runtime", StringComparison.Ordinal));
        for (int i = 0; i < 12; i++)
        {
            feed.SetPreview(new[] { (2, 3, 1, map.Model((i % 2 == 0 ? "/turf/closed/wall" : "/turf/open/floor/iron") + Hall)) });
            Settle(feed);
        }
        Assert.Equal((2, 3, 1, "/turf/open/floor/iron"), Assert.Single(feed.PreviewCells)); // (the turf is the preview's own: the view tints the floor)
        Assert.Equal("/turf/open/floor/iron", feed.Replica.TurfTypeAt(2, 3, 1));
        Assert.Equal(runtimes, feed.Log.Count(line => line.Contains("runtime", StringComparison.Ordinal)));
        feed.SetPreview(null);
        Settle(feed);
        Assert.Equal(before, Shown(feed));
    }

    [Fact]
    [Trait("Category", "Meta")]
    public void MetaStationIsShownFromItsMapsFolderAndAnEditAtOnce()
    {
        string path = FakeMapTests.MetaPath();
        if (path == null || !MetaWorldGate.Enter())
        {
            _output.WriteLine("no converted MetaStation (maps/map_files/MetaStation/MetaStation.as3dmap or ANOMALY_META_MAP): skipped");
            return;
        }
        try
        {
            var watch = Stopwatch.StartNew();
            MapDocument document = MapDocument.Parse(File.ReadAllText(path), path);
            using var feed = new EditorFeed(document, path);
            Settle(feed, 900);
            _output.WriteLine($"MetaStation shown in {watch.ElapsedMilliseconds} ms: {feed.Count} things");
            Assert.True(feed.Count > 15000);
            Assert.Contains(feed.Entities, e => e.TypePath.StartsWith("/obj/machinery/atmospherics/pipe/smart", StringComparison.Ordinal));
            Assert.Contains(feed.Entities, e => e.TypePath.StartsWith("/obj/effect/landmark/start", StringComparison.Ordinal));
            Assert.Contains(feed.Entities, e => e.TypePath.StartsWith("/obj/effect/spawner/random", StringComparison.Ordinal));

            (int X, int Y, string Members)[] edits =
            {
                (120, 130, Floor),
                (131, 127, "/obj/structure/table | /turf/open/floor/iron" + Hall),
                (100, 100, EditorSession.NewMapMembers),
            };
            foreach ((int x, int y, string members) in edits)
            {
                document.Edit("Edit", new[] { (x, y, 1, document.Model(members)) });
                watch.Restart();
                feed.Poll(1);
                double first = watch.Elapsed.TotalMilliseconds;
                Settle(feed);
                _output.WriteLine($"an edit at {x},{y}: loaded and first shown in {first:0.0} ms, settled in {watch.Elapsed.TotalMilliseconds:0.0} ms");
            }
            Assert.Equal("/turf/open/floor/iron", feed.Replica.TurfTypeAt(120, 130, 1));
            Assert.Contains(At(feed, 131, 127), e => e.TypePath == "/obj/structure/table");
            Assert.Empty(At(feed, 100, 100));
        }
        finally
        {
            MetaWorldGate.Exit();
        }
    }

    [Fact]
    public void ASessionSavesANewMapAndOpensItAgain()
    {
        string folder = Path.Combine(Path.GetTempPath(), "anomaly-editor-" + Guid.NewGuid().ToString("N"));
        try
        {
            string file = Path.Combine(folder, "maps", "new.as3dmap");
            using EditorSession session = EditorSession.OpenOrNew(file, 12, 9, 2);
            Assert.False(session.OnDisk);
            Assert.True(session.NeedsSave);
            Assert.Equal("new.as3dmap *", session.Title);
            session.Document.Edit("Floor", new[] { (3, 3, 2, session.Document.Model(Floor)) });
            session.Save();
            Assert.True(session.OnDisk);
            Assert.False(session.NeedsSave);
            Assert.Equal("new.as3dmap", session.Title);
            Assert.False(File.Exists(file + ".saving"));
            Assert.Equal((byte)'a', File.ReadAllBytes(file)[0]); // "anomaly-map": no byte order mark

            using EditorSession again = EditorSession.OpenOrNew(file, 1, 1, 1);
            Assert.True(again.OnDisk);
            Assert.False(again.NeedsSave);
            Assert.Equal((12, 9, 2), (again.Document.Width, again.Document.Height, again.Document.Levels));
            Assert.Equal("/turf/open/floor/iron", again.Document[3, 3, 2].Members[0].Path);
            Assert.Equal("Anomaly Editor: new.as3dmap", again.Feed.Description);
            Assert.Equal(FeedPhase.Idle, again.Feed.Phase); // the world starts with the first poll

            again.Document.Edit("Space", new[] { (3, 3, 2, again.Document.Default) });
            Assert.Equal("new.as3dmap *", again.Title);
            again.Document.Undo();
            Assert.Equal("new.as3dmap", again.Title); // back at what the file holds
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheRecentMapsAreTheNewestFirstEachOnceAndTen()
    {
        string[] recent = Array.Empty<string>();
        for (int i = 0; i < 12; i++)
            recent = RecentMaps.With(recent, "/maps/m" + i + ".as3dmap");
        Assert.Equal(RecentMaps.Max, recent.Length);
        Assert.EndsWith("m11.as3dmap", recent[0]);
        Assert.EndsWith("m2.as3dmap", recent[^1]);
        recent = RecentMaps.With(recent, "/maps/m5.as3dmap"); // opened again: at the head, and not listed twice
        Assert.EndsWith("m5.as3dmap", recent[0]);
        Assert.Equal(RecentMaps.Max, recent.Length);
        Assert.Single(recent, path => path.EndsWith("m5.as3dmap", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEditorArgumentsAreRead()
    {
        // the program under the map editor's name is the map editor, and a map of 64 levels is asked for by its size
        Assert.True(ClientArgs.EditorByName("/games/AnomalyStation/MapEditor.exe"));
        Assert.True(ClientArgs.EditorByName("C:\\games\\Anomaly Station Map-Editor.exe"));
        Assert.True(ClientArgs.EditorByName("/opt/anomaly/anomaly_map_editor"));
        Assert.True(ClientArgs.EditorByName("/Applications/Anomaly Editor.app/Contents/MacOS/Anomaly Editor"));
        Assert.True(ClientArgs.EditorByName("C:\\games\\AnomalyEditor.exe"));
        Assert.True(ClientArgs.EditorByName("/opt/anomaly/anomaly-editor"));
        Assert.False(ClientArgs.EditorByName("/games/AnomalyStation/AnomalyStation.exe"));
        Assert.False(ClientArgs.EditorByName(null));
        Assert.Equal((8, 8, 64), ClientArgs.Parse(new[] { "--editor", "--editor-size=8,8,64" }).EditorSize);
        Assert.NotEmpty(ClientArgs.Parse(new[] { "--editor", "--editor-size=8,8,65" }).Errors);
        ClientArgs args = ClientArgs.Parse(new[] { "--editor=/maps/a.as3dmap", "--editor-size=10,20,3" });
        Assert.Empty(args.Errors);
        Assert.True(args.Editor);
        Assert.True(args.SkipConnectScreen);
        Assert.Equal("/maps/a.as3dmap", args.EditorMapPath);
        Assert.Equal((10, 20, 3), args.EditorSize);

        ClientArgs bare = ClientArgs.Parse(new[] { "--editor" });
        Assert.True(bare.Editor);
        Assert.Equal(string.Empty, bare.EditorMapPath);
        Assert.Equal((64, 64, 1), bare.EditorSize);

        Assert.Null(bare.EditorSettingsPath); // the user's own settings file
        Assert.Equal("/tmp/check.cfg", ClientArgs.Parse(new[] { "--editor", "--editor-settings=/tmp/check.cfg" }).EditorSettingsPath);
        Assert.Single(ClientArgs.Parse(new[] { "--editor", "--editor-settings" }).Errors);

        Assert.False(ClientArgs.Parse(Array.Empty<string>()).Editor);
        Assert.Single(ClientArgs.Parse(new[] { "--editor", "--editor-size=0,5,1" }).Errors);
        Assert.Single(ClientArgs.Parse(new[] { "--editor", "--editor-size=5,5" }).Errors);
    }
}
