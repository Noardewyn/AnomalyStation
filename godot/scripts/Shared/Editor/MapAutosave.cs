using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// A copy the map editor kept of a map's changes that were not saved (<see cref="MapAutosave"/>): the map's file, when the copy was written, and
/// the copy's own files.
/// </summary>
/// <param name="Record">The copy's record file (the others are beside it, by its name).</param>
/// <param name="MapPath">The file of the map the copy is of.</param>
/// <param name="At">When the copy was written (universal time).</param>
/// <param name="Process">The editor's process that wrote it.</param>
public sealed record KeptMap(string Record, string MapPath, DateTime At, int Process)
{
    /// <summary>The copy of the map's text.</summary>
    public string MapCopy => Path.ChangeExtension(Record, ".map");

    /// <summary>The copy of the map json's text; it need not be there (a map with no json).</summary>
    public string JsonCopy => Path.ChangeExtension(Record, ".json");

    /// <summary>True when the map's own file was written after the copy: restoring the copy would take that later save back.</summary>
    public bool Older => File.Exists(MapPath) && File.GetLastWriteTimeUtc(MapPath) > At;
}

/// <summary>
/// The map editor's autosave (docs/ai/specs/map-editor.md 5.21): while a map has changes that are not saved, a copy of it is written to a folder
/// of the user's own every minute, and taken away when the map is saved or left on purpose. A copy that is still there when the editor starts
/// was left by an editor that crashed or was killed: the editor offers to restore it. An editor's copies are its own (several editors may have
/// one map open), told apart by its process.
/// </summary>
public static class MapAutosave
{
    private const string RecordExtension = ".kept";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // The record file of a map's copy in a folder: the map's name, then its path's and the process's numbers (two maps of one name do not share it).
    private static string RecordOf(string folder, string mapPath, int process)
    {
        uint hash = 2166136261;
        foreach (char c in Path.GetFullPath(mapPath))
            hash = (hash ^ c) * 16777619;
        string stem = new string(Path.GetFileNameWithoutExtension(mapPath).Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').Take(40).ToArray());
        return Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem}-{hash:x8}-{process}{RecordExtension}"));
    }

    /// <summary>
    /// Writes a session's copy: the map's text and the map json as a save would write them now. The record is written last, so a record always has
    /// whole files. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when the folder cannot be written.
    /// </summary>
    public static void Write(string folder, EditorSession session, int process, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(session);
        Directory.CreateDirectory(folder);
        var kept = new KeptMap(RecordOf(folder, session.FilePath, process), session.FilePath, nowUtc, process);
        Replace(kept.MapCopy, session.Document.ToText());
        Replace(kept.JsonCopy, session.JsonText);
        Replace(kept.Record, string.Create(CultureInfo.InvariantCulture, $"path={session.FilePath}\ntime={nowUtc.Ticks}\nprocess={process}\n"));
    }

    private static void Replace(string path, string text)
    {
        File.WriteAllText(path + ".writing", text, Utf8);
        File.Move(path + ".writing", path, overwrite: true);
    }

    /// <summary>Takes this process's copy of a map away (the map was saved, or left on purpose). A copy that cannot be deleted is left.</summary>
    public static void Drop(string folder, string mapPath, int process)
    {
        if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(mapPath))
            Drop(new KeptMap(RecordOf(folder, mapPath, process), mapPath, default, process));
    }

    /// <summary>Takes a copy away.</summary>
    public static void Drop(KeptMap kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        try
        {
            // (the record first: what is left without one is no copy)
            foreach (string file in new[] { kept.Record, kept.MapCopy, kept.JsonCopy })
                File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // left: it is offered again
        }
    }

    /// <summary>
    /// The copies an editor left behind, the latest first: every record of the folder with its map's copy, but those of a process that still runs
    /// (<paramref name="runs"/>: another editor at work; a record of this very process's number is a dead one's, the number given again).
    /// </summary>
    public static List<KeptMap> Left(string folder, int process, Func<int, bool> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var left = new List<KeptMap>();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return left;
        foreach (string record in Directory.EnumerateFiles(folder, "*" + RecordExtension))
        {
            try
            {
                var lines = File.ReadAllLines(record).Select(line => line.Split('=', 2)).Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
                if (!lines.TryGetValue("path", out string path) || !lines.TryGetValue("time", out string time) || !lines.TryGetValue("process", out string writer)
                    || !long.TryParse(time, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) || !int.TryParse(writer, NumberStyles.None, CultureInfo.InvariantCulture, out int by))
                    continue;
                var kept = new KeptMap(record, path, new DateTime(ticks, DateTimeKind.Utc), by);
                if (File.Exists(kept.MapCopy) && (by == process || !runs(by)))
                    left.Add(kept);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // a record that cannot be read is no copy
            }
        }
        left.Sort((a, b) => b.At.CompareTo(a.At));
        return left;
    }
}
