using System.Diagnostics;
using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>
/// The map editor's autosave (docs/ai/specs/map-editor.md 5.21): a copy of a map's unsaved changes is kept in a folder, taken away when the map is
/// saved, and restored as changes that are not saved when the editor that wrote it is gone.
/// </summary>
public sealed class MapAutosaveTests : IDisposable
{
    private const string Floor = "/turf/open/floor/iron | /area/station/hallway/primary/central";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-autosave-test-" + Guid.NewGuid().ToString("N"));

    public MapAutosaveTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Kept => Path.Combine(_folder, "kept");

    private static void Settle(EditorFeed feed)
    {
        var watch = Stopwatch.StartNew();
        for (int frame = 0; feed.Phase != FeedPhase.Closed && (feed.Phase != FeedPhase.Playing || feed.Busy); frame++)
        {
            Assert.True(watch.Elapsed.TotalSeconds < 300, "the editor's world did not settle: " + feed.Phase + "\n" + string.Join("\n", feed.Log.TakeLast(20)));
            feed.Poll(frame * 0.016);
            if (feed.Phase != FeedPhase.Playing)
                Thread.Sleep(5);
        }
        Assert.True(feed.Phase == FeedPhase.Playing, feed.CloseReason + "\n" + string.Join("\n", feed.Log.TakeLast(20)));
    }

    [Fact]
    public void UnsavedChangesAreKeptAndRestoredAsUnsaved()
    {
        string file = Path.Combine(_folder, "deck.as3dmap");
        var at = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc); // (before the file below is written)
        using (EditorSession session = EditorSession.New(file, 6, 5, 1))
        {
            session.Save();
            session.Document.Edit("Floor", new[] { (2, 3, 1, session.Document.Model(Floor)) });
            session.SetSettings(session.Settings with { StationName = "Kept Station" });
            Assert.True(session.NeedsSave);
            MapAutosave.Write(Kept, session, process: 41, at);
        }
        // (the editor is gone without a save: its file has the map as it was saved)
        Assert.DoesNotContain("iron", File.ReadAllText(file), StringComparison.Ordinal);

        // another editor that still runs is at work on its copy: nothing is offered; one that is gone left it
        Assert.Empty(MapAutosave.Left(Kept, process: 7, runs: process => process == 41));
        KeptMap kept = Assert.Single(MapAutosave.Left(Kept, process: 7, runs: _ => false));
        Assert.Equal((Path.GetFullPath(file), at, 41), (kept.MapPath, kept.At, kept.Process));
        // (a copy under this very process's number is a dead one's: the number was given again)
        Assert.Single(MapAutosave.Left(Kept, process: 41, runs: _ => true));
        Assert.True(kept.Older); // (the file was written after the copy's time)

        using (EditorSession restored = EditorSession.Restore(kept))
        {
            Assert.Equal(Path.GetFullPath(file), restored.FilePath);
            Assert.True(restored.NeedsSave && restored.Restored && restored.OnDisk);
            Assert.False(restored.Document.IsDirty); // (nothing was edited since: it is the copy itself that is to be saved)
            Assert.Equal(Floor, restored.Document[2, 3, 1].Text);
            Assert.Equal("Kept Station", restored.Settings.StationName);
            Assert.EndsWith(" *", restored.Title, StringComparison.Ordinal);

            // the view shows the copy, not the older file
            Settle(restored.Feed);
            Assert.Equal("/turf/open/floor/iron", restored.Feed.Replica.TurfTypeAt(2, 3, 1));

            restored.Save();
            Assert.False(restored.NeedsSave || restored.Restored);
        }
        Assert.Contains("iron", File.ReadAllText(file), StringComparison.Ordinal);
        using (EditorSession again = EditorSession.Open(file))
            Assert.Equal("Kept Station", again.Settings.StationName);

        // saved: the copy is taken away, by the map and the process it was written for
        MapAutosave.Drop(Kept, file, process: 9);
        Assert.Single(MapAutosave.Left(Kept, 7, _ => false));
        MapAutosave.Drop(Kept, file, process: 41);
        Assert.Empty(MapAutosave.Left(Kept, 7, _ => false));
        Assert.Empty(Directory.GetFiles(Kept));
    }

    [Fact]
    public void AMapOfAMapsFolderIsShownFromItsCopyNotFromItsOlderFile()
    {
        // tg's layout: the json names the map's file, and the world loads the map from the folder (for its shuttles and its other files)
        string maps = Path.Combine(_folder, "maps"), file = Path.Combine(maps, "map_files", "Deck", "Deck.as3dmap");
        Directory.CreateDirectory(Path.Combine(maps, "map_jsons"));
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(Path.Combine(maps, "map_jsons", "deck.json"), "{\"version\": 1, \"map_name\": \"Deck\", \"map_path\": \"map_files/Deck\", \"map_file\": \"Deck.dmm\"}");
        File.WriteAllText(file, MapDocument.Create(6, 5, 1, EditorSession.NewMapMembers).ToText());
        using (EditorSession session = EditorSession.Open(file))
        {
            Assert.Equal(maps, session.MapsFolder);
            session.Document.Edit("Floor", new[] { (4, 2, 1, session.Document.Model(Floor)) });
            MapAutosave.Write(Kept, session, 3, DateTime.UtcNow);
        }
        using EditorSession restored = EditorSession.Restore(Assert.Single(MapAutosave.Left(Kept, 1, _ => false)));
        Assert.Equal(maps, restored.MapsFolder);
        Settle(restored.Feed);
        Assert.Equal("/turf/open/floor/iron", restored.Feed.Replica.TurfTypeAt(4, 2, 1));
        Assert.DoesNotContain("iron", File.ReadAllText(file), StringComparison.Ordinal); // (the file is as it was until a save)
    }

    [Fact]
    public void ANewMapThatWasNeverSavedIsRestoredToo()
    {
        string file = Path.Combine(_folder, "never saved.as3dmap");
        using (EditorSession session = EditorSession.New(file, 4, 4, 2))
        {
            session.Document.Edit("Floor", new[] { (1, 1, 2, session.Document.Model(Floor)) });
            MapAutosave.Write(Kept, session, 5, DateTime.UtcNow);
            // the next minute's copy takes the place of this one
            session.Document.Edit("Floor", new[] { (2, 1, 2, session.Document.Model(Floor)) });
            MapAutosave.Write(Kept, session, 5, DateTime.UtcNow);
        }
        Assert.False(File.Exists(file));
        KeptMap kept = Assert.Single(MapAutosave.Left(Kept, 6, _ => false));
        Assert.False(kept.Older);
        using EditorSession restored = EditorSession.Restore(kept);
        Assert.True(restored.NeedsSave);
        Assert.False(restored.OnDisk);
        Assert.Equal((4, 4, 2), (restored.Document.Width, restored.Document.Height, restored.Document.Levels));
        Assert.Equal((Floor, Floor), (restored.Document[1, 1, 2].Text, restored.Document[2, 1, 2].Text));
        MapAutosave.Drop(kept);
        Assert.Empty(MapAutosave.Left(Kept, 6, _ => false));
    }

    [Fact]
    public void TwoMapsOfOneNameAndTwoEditorsOfOneMapKeepTheirOwnCopies()
    {
        string first = Path.Combine(_folder, "a", "deck.as3dmap"), second = Path.Combine(_folder, "b", "deck.as3dmap");
        using EditorSession one = EditorSession.New(first, 3, 3, 1), two = EditorSession.New(second, 5, 5, 1);
        MapAutosave.Write(Kept, one, 11, DateTime.UtcNow.AddMinutes(-2));
        MapAutosave.Write(Kept, two, 11, DateTime.UtcNow.AddMinutes(-1));
        MapAutosave.Write(Kept, one, 12, DateTime.UtcNow);
        List<KeptMap> left = MapAutosave.Left(Kept, 1, _ => false);
        // the latest first
        Assert.Equal(new[] { (Path.GetFullPath(first), 12), (Path.GetFullPath(second), 11), (Path.GetFullPath(first), 11) }, left.Select(k => (k.MapPath, k.Process)));
        // a folder that is not there, and a record that is no record, give nothing
        File.WriteAllText(Path.Combine(Kept, "junk.kept"), "what\n");
        Assert.Equal(3, MapAutosave.Left(Kept, 1, _ => false).Count);
        Assert.Empty(MapAutosave.Left(Path.Combine(_folder, "none"), 1, _ => false));
    }
}
