using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anomaly.Core.Tg;
using static System.FormattableString;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>One map open in the map editor (docs/ai/specs/map-editor.md 3): its document, its file and the feed that shows it.</summary>
public sealed class EditorSession : IDisposable
{
    /// <summary>The members of every tile of a new map's first level: space.</summary>
    public const string NewMapMembers = "/turf/open/space/basic | /area/space";

    /// <summary>tg's space that is open to the level below: what falls there falls through, and the level below is seen through it.</summary>
    public const string OpenSpace = "/turf/open/space/openspace";

    /// <summary>
    /// The members of every tile of a new map's levels above the first: open space, with which those levels get tg's <c>Baseturf</c> trait
    /// (<see cref="New"/>), so that what is destroyed there is open space again.
    /// </summary>
    public const string NewMapUpperMembers = OpenSpace + " | /area/space";

    /// <summary>
    /// The members of every tile of a new shuttle's map: tg's tile that leaves the map under it as it is, so that a shuttle of any outline loads over
    /// its dock without the tiles around it.
    /// </summary>
    public const string NewShuttleMembers = "/turf/template_noop | /area/template_noop";

    private string _jsonText; // the map json as it was read or last written; null: the map has none yet
    private (MapSettings Settings, string Json) _savedSettings; // the settings and the json as the file has them: what differs is to be saved
    private bool _restored; // the document is a kept copy's (MapAutosave): its file holds an older map until a save

    private EditorSession(MapDocument document, string filePath, bool onDisk, bool restored = false)
    {
        _restored = restored;
        Document = document;
        FilePath = filePath;
        OnDisk = onDisk;
        JsonPath = JsonOf(filePath);
        _jsonText = File.Exists(JsonPath) ? File.ReadAllText(JsonPath) : null;
        try
        {
            JsonUnreadable = _jsonText != null && MapSettings.ParseObject(_jsonText) == null;
        }
        catch (JsonException)
        {
            JsonUnreadable = true;
        }
        if (JsonUnreadable)
            _jsonText = null; // (its file is left as it is: SaveAs writes no json over one that could not be read)
        Settings = MapSettings.Read(_jsonText);
        _savedSettings = (Settings, _jsonText);
        document.EmptyTile = EmptyOf;
        Feed = new EditorFeed(document, onDisk ? filePath : null, Path.GetFileName(filePath), () => (JsonText, SkyImagePath), fileIsOlder: restored);
    }

    private (string Json, MapModel[] Tiles) _empty; // the levels' empty tiles as the json has them

    // A level's empty tile (docs/ai/specs/map-editor.md 5.17), which the tools leave where they take a tile away: the map's default tile with the level's
    // base turf (tg's Baseturf trait) for its turf, so that a tile emptied on a level over another is open space again and not plain space. Null (the
    // default tile) for a level with no base turf, and for a shuttle's map, whose every empty tile is tg's noop tile.
    private MapModel EmptyOf(int level)
    {
        if (level < 1 || level > Document.Levels || MapEdits.TurfOf(Document.Default) == "/turf/template_noop")
            return null;
        if (_empty.Tiles == null || !ReferenceEquals(_empty.Json, _jsonText))
            _empty = (_jsonText, new MapModel[Document.Levels + 1]);
        return _empty.Tiles[level] ??= LevelTrait(level, "Baseturf")?.Trim('"') is { } turf && MapEdits.IsTurf(turf)
            ? Document.Model(Document.Default.Members.Select(m => MapEdits.IsTurf(m.Path) ? new MapMember(turf, null) : m).ToList()) : Document.Default;
    }

    /// <summary>
    /// The map's own settings (docs/ai/specs/map-editor.md 5.10): the station's name, the sky, the sun, the air. They are a key of the map json
    /// (<see cref="JsonPath"/>) and are written with the map.
    /// </summary>
    public MapSettings Settings { get; private set; }

    /// <summary>
    /// The map json: the one of a maps folder that names the map's file (tg's layout, <c>map_jsons/NAME.json</c>), else the file's own, beside it with
    /// its name. It need not exist: a map with the game's own settings has none until something is set.
    /// </summary>
    public string JsonPath { get; private set; }

    /// <summary>
    /// True when the map has a json that could not be read (no json, or no object): the settings are the game's own, and a save leaves that file as it
    /// is, so that what a hand wrote there is not lost.
    /// </summary>
    public bool JsonUnreadable { get; private set; }

    /// <summary>The interface's clock (seconds): changes of one setting that follow each other within a second are one undo step (a slider dragged).</summary>
    public Func<double> Clock { get; set; }

    /// <summary>Gives the map other settings; a save writes them. The change is a step of the document's history.</summary>
    public void SetSettings(MapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings == Settings)
            return;
        (MapSettings, string) before = (Settings, _jsonText);
        string changed = typeof(MapSettings).GetProperties().FirstOrDefault(p => !Equals(p.GetValue(Settings), p.GetValue(settings)))?.Name ?? "settings";
        Settings = settings;
        Recorded("Map setting: " + changed, changed, before);
    }

    // A change of the map's settings or of its json is a step of the document's history (docs/ai/specs/map-editor.md 5.10): undo and redo give both
    // back as they were then, among the tiles' steps in the order they were made.
    private void Recorded(string label, string key, (MapSettings Settings, string Json) before)
    {
        if (Document.InStep)
            return; // (a stroke is being drawn: its step is open)
        (MapSettings Settings, string Json) after = (Settings, _jsonText);
        Document.Record(label, () => Restore(before), () => Restore(after), key, Clock?.Invoke() ?? 0);
    }

    private void Restore((MapSettings Settings, string Json) state) => (Settings, _jsonText) = state;

    private bool SettingsDirty => Settings != _savedSettings.Settings || _jsonText != _savedSettings.Json;

    /// <summary>The map json's text as a save would write it now, the settings that are not saved in it: Play gives it to its round.</summary>
    public string JsonText => Settings.Write(_jsonText ?? LeastJson(FilePath));

    /// <summary>The file of the sky's picture, beside the map's file; null when the sky is no picture's.</summary>
    public string SkyImagePath => Settings.SkyImage == null ? null : Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, Settings.SkyImage);

    /// <summary>
    /// A value of the map json's level traits (tg's <c>traits</c>: an object a level, from the lowest), as json text; null when the json, the level
    /// or the trait is not there.
    /// </summary>
    public string LevelTrait(int level, string trait)
    {
        try
        {
            return MapSettings.ParseObject(_jsonText) is { } root && root["traits"] is JsonArray traits
                && level >= 1 && level <= traits.Count && traits[level - 1] is JsonObject held && held[trait] is { } value ? value.ToJsonString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sets a level trait of the map json (null: takes it away). The json gets tg's <c>traits</c> when it has none: an object a level with tg's default
    /// linkage, which tg gives a level only while a map names no traits. A json whose levels all have that default again loses the key.
    /// </summary>
    public void SetLevelTrait(int level, string trait, JsonNode value)
    {
        if (level < 1 || level > Document.Levels)
            return;
        (MapSettings, string) before = (Settings, _jsonText);
        string text = WithLevelTrait(_jsonText ?? LeastJson(FilePath), Document.Levels, level, trait, value);
        if (text == MapSettings.Default.Write(_jsonText ?? LeastJson(FilePath)))
            return;
        _jsonText = text;
        Recorded(Invariant($"Level {level}: {trait}"), Invariant($"trait {level} {trait}"), before);
    }

    // A map json's text with a level trait set (null: taken away), as SetLevelTrait says.
    private static string WithLevelTrait(string json, int levels, int level, string trait, JsonNode value)
    {
        var root = MapSettings.ParseObject(json);
        if (root["traits"] is not JsonArray traits)
            root["traits"] = traits = new JsonArray();
        while (traits.Count < levels)
            traits.Add(new JsonObject { ["Linkage"] = "Cross" });
        if (traits[level - 1] is not JsonObject held)
            traits[level - 1] = held = new JsonObject { ["Linkage"] = "Cross" };
        held.Remove(trait);
        if (value != null)
            held[trait] = value;
        if (traits.All(entry => entry is JsonObject { Count: 1 } only && only["Linkage"]?.ToJsonString() == "\"Cross\""))
            root.Remove("traits");
        return MapSettings.Default.Write(root.ToJsonString());
    }

    /// <summary>A text of the map json's own keys (tg's <c>map_name</c>); null when the json or the key is not there.</summary>
    public string JsonString(string key)
    {
        try
        {
            return MapSettings.ParseObject(_jsonText ?? LeastJson(FilePath)) is { } root && root[key] is JsonValue value && value.TryGetValue(out string text) ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A text of an object of the map json (tg's <c>shuttles</c>: a slot's shuttle); null when it is not there.</summary>
    public string JsonString(string holder, string key)
    {
        try
        {
            return MapSettings.ParseObject(_jsonText) is { } root && root[holder] is JsonObject held && held[key] is JsonValue value && value.TryGetValue(out string text) ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Sets a text of an object of the map json (null: takes it away, and the object with its last key); a save writes it.</summary>
    public void SetJsonString(string holder, string key, string text)
    {
        if (text == JsonString(holder, key))
            return;
        (MapSettings, string) before = (Settings, _jsonText);
        var root = MapSettings.ParseObject(_jsonText ?? LeastJson(FilePath));
        if (root[holder] is not JsonObject held)
            root[holder] = held = new JsonObject();
        held.Remove(key);
        if (text != null)
            held[key] = text;
        if (held.Count == 0)
            root.Remove(holder);
        _jsonText = MapSettings.Default.Write(root.ToJsonString());
        Recorded("Map setting: " + holder + " " + key, holder + "." + key, before);
    }

    /// <summary>The maps folder (tg's layout) whose json names the map's file; null for a map of none.</summary>
    public string MapsFolder => EditorFeed.FindJson(FilePath)?.Folder;

    /// <summary>Sets a text of the map json's own keys (null: takes the key away, tg's <c>minetype</c> back to tg's own); a save writes it.</summary>
    public void SetJsonString(string key, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (text == JsonString(key))
            return;
        (MapSettings, string) before = (Settings, _jsonText);
        var root = MapSettings.ParseObject(_jsonText ?? LeastJson(FilePath));
        root.Remove(key);
        if (text != null)
            root[key] = text;
        _jsonText = MapSettings.Default.Write(root.ToJsonString());
        Recorded("Map setting: " + key, key, before);
    }

    // The map json a file's map is read with: a maps folder's that names it, else the file's own beside it when it has one, else the one a save writes.
    private static string JsonOf(string filePath)
        => EditorFeed.FindJson(filePath) == null && File.Exists(Path.ChangeExtension(filePath, ".json")) ? Path.ChangeExtension(filePath, ".json") : JsonToWrite(filePath);

    // The map json a save of a file writes: a maps folder's that names it; for a file at a station's place of a maps folder (EditorFeed.PlaceOf) that
    // no json names, the json of its name in that folder's map_jsons, which makes the map that folder's (unless a json of that name is another
    // map's); else the file's own, beside it.
    private static string JsonToWrite(string filePath)
    {
        if (EditorFeed.FindJson(filePath) is { } found)
            return Path.Combine(found.Folder, "map_jsons", found.Name + ".json");
        if (EditorFeed.PlaceOf(filePath) is { } place && Path.Combine(place.Folder, "map_jsons", place.Name + ".json") is { } json && !File.Exists(json))
            return json;
        return Path.ChangeExtension(filePath, ".json");
    }

    /// <summary>
    /// A map's file named straight in the <c>map_files</c> of a maps folder gets tg's place there, a folder of its own name:
    /// <c>map_files/NAME.as3dmap</c> is <c>map_files/NAME/NAME.as3dmap</c>. Any other path is itself, in full.
    /// </summary>
    public static string Placed(string filePath)
    {
        string full = Path.GetFullPath(filePath), files = Path.GetDirectoryName(full), folder = Path.GetDirectoryName(files);
        return folder != null && string.Equals(Path.GetFileName(files), "map_files", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(folder, "map_jsons"))
            ? Path.Combine(files, Path.GetFileNameWithoutExtension(full), Path.GetFileName(full)) : full;
    }

    // The least a map json holds, for a map that has none: tg's four keys, the map's files beside the json.
    internal static string LeastJson(string filePath)
    {
        string stem = Path.GetFileNameWithoutExtension(filePath);
        return JsonSerializer.Serialize(new Dictionary<string, object> { ["version"] = 1, ["map_name"] = stem, ["map_path"] = ".", ["map_file"] = stem + ".dmm" });
    }

    /// <summary>Stops the world that shows the map.</summary>
    public void Dispose() => Feed.Dispose();

    /// <summary>The map.</summary>
    public MapDocument Document { get; }

    /// <summary>The map's file.</summary>
    public string FilePath { get; private set; }

    /// <summary>False for a new map that was never written to its file.</summary>
    public bool OnDisk { get; private set; }

    /// <summary>The feed the scene draws.</summary>
    public EditorFeed Feed { get; }

    /// <summary>True for a kept copy's map that was not saved yet.</summary>
    public bool Restored => _restored;

    /// <summary>True when <see cref="Save"/> has something to write.</summary>
    public bool NeedsSave => !OnDisk || _restored || Document.IsDirty || SettingsDirty;

    /// <summary>The file's name, with a star while there is something to save.</summary>
    public string Title => Path.GetFileName(FilePath) + (NeedsSave ? " *" : string.Empty);

    /// <summary>Opens the map of a file. Throws <see cref="IOException"/> or <see cref="MapTextException"/>.</summary>
    public static EditorSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        string full = Path.GetFullPath(filePath);
        return new EditorSession(MapDocument.Parse(File.ReadAllText(full), full), full, onDisk: true);
    }

    /// <summary>
    /// A new map of the given size, all space, that <see cref="Save"/> writes to <paramref name="filePath"/>. A station's levels above the first
    /// are open space (<see cref="NewMapUpperMembers"/>) and get tg's <c>Baseturf</c> trait of it, unless the file's json names traits already.
    /// </summary>
    public static EditorSession New(string filePath, int width, int height, int levels, string members = NewMapMembers)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        bool station = members == NewMapMembers;
        var session = new EditorSession(MapDocument.Create(width, height, levels, members, station ? NewMapUpperMembers : null), Placed(filePath), onDisk: false);
        if (station && !session.JsonUnreadable && MapSettings.ParseObject(session._jsonText ?? LeastJson(session.FilePath))["traits"] == null)
        {
            // (no step of the history: the map is new with them)
            for (int z = 2; z <= levels; z++)
                session._jsonText = WithLevelTrait(session._jsonText ?? LeastJson(session.FilePath), levels, z, "Baseturf", JsonValue.Create(OpenSpace));
        }
        return session;
    }

    /// <summary>
    /// The map of a copy the editor kept (<see cref="MapAutosave"/>): the copy's text is the document and its json the settings, the map's own
    /// file is the session's file, and all of it is to be saved (the file holds the map as it was last saved, if it is there at all). Throws
    /// <see cref="IOException"/> or <see cref="MapTextException"/>.
    /// </summary>
    public static EditorSession Restore(KeptMap kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        string full = Path.GetFullPath(kept.MapPath);
        var session = new EditorSession(MapDocument.Parse(File.ReadAllText(kept.MapCopy), full), full, onDisk: File.Exists(full), restored: true);
        if (File.Exists(kept.JsonCopy) && !session.JsonUnreadable)
        {
            // (the file's own json stays what a save compares with)
            session._jsonText = File.ReadAllText(kept.JsonCopy);
            session.Settings = MapSettings.Read(session._jsonText);
        }
        return session;
    }

    /// <summary>The map of the file when it exists, else a new map of the given size for it.</summary>
    public static EditorSession OpenOrNew(string filePath, int width, int height, int levels)
        => File.Exists(filePath) ? Open(filePath) : New(filePath, width, height, levels);

    /// <summary>
    /// Writes the map to its file (UTF-8, no byte order mark, map-format.md 4). The text goes to a file beside it first and takes
    /// the map's place only when it is whole, so a failed write leaves the old file as it was.
    /// </summary>
    public void Save() => SaveAs(FilePath);

    /// <summary>The same into another file, which is the map's file from then on.</summary>
    public void SaveAs(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        string picture = SkyImagePath, jsonWas = JsonPath;
        FilePath = Path.GetFullPath(filePath);
        string text = Document.ToText();
        string folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        string scratch = FilePath + ".saving";
        File.WriteAllText(scratch, text, utf8);
        File.Move(scratch, FilePath, overwrite: true);
        Document.MarkSaved();
        (OnDisk, _restored) = (true, false);

        // The map's json, when it has something to hold: the settings and the level traits. A map saved under another name takes them along, with the
        // sky's picture, into the json of its new file.
        // A map saved at a station's place of a maps folder that no json names yet gets its json there, settings or none (the owner, 2026-11-17): the
        // map is that folder's from then on, with its shuttles and its CentCom.
        bool ofFolder = EditorFeed.FindJson(FilePath) != null, unreadable = JsonUnreadable;
        JsonPath = JsonToWrite(FilePath);
        string beside = Path.ChangeExtension(FilePath, ".json");
        bool joins = !ofFolder && !string.Equals(JsonPath, beside, StringComparison.OrdinalIgnoreCase);
        bool moved = !string.Equals(JsonPath, jsonWas, StringComparison.OrdinalIgnoreCase);
        if (picture != null && File.Exists(picture) && !File.Exists(SkyImagePath))
            File.Copy(picture, SkyImagePath);
        if (JsonUnreadable && !moved)
            return;
        JsonUnreadable = false;
        if (!joins && !SettingsDirty && !(moved && _jsonText != null && !File.Exists(JsonPath)))
            return;
        string json = _jsonText ?? LeastJson(FilePath);
        if (!ofFolder)
        {
            // (the folder's json names the file by its place in the folder, as the map's package does; the file's own json names the file beside it)
            string stem = Path.GetFileNameWithoutExtension(FilePath);
            var root = MapSettings.ParseObject(json);
            (root["map_path"], root["map_file"]) = (joins ? "map_files/" + stem : ".", stem + ".dmm");
            json = root.ToJsonString();
        }
        _jsonText = Settings.Write(json);
        File.WriteAllText(JsonPath + ".saving", _jsonText, utf8);
        File.Move(JsonPath + ".saving", JsonPath, overwrite: true);
        _savedSettings = (Settings, _jsonText);
        // (the map's own json, which it had beside its file until now, went into the folder's; one that could not be read is left as it is)
        if (joins && !unreadable && string.Equals(jsonWas, beside, StringComparison.OrdinalIgnoreCase) && File.Exists(beside))
            File.Delete(beside);
    }
}
