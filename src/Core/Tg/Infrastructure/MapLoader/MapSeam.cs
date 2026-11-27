using System.IO;
using System.Linq;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Owner: the contract is the M3 contracts step's and frozen (docs/ai/specs/m3-ownership.md); M3 P2 implements it. A host seam:
// which converted map the world's SSmapping loads (D108, D109), and where the players join (tg's late-join landmarks).

/// <summary>The map a world loads: the folder of converted maps and the map json's name (docs/ai/specs/map-format.md 2).</summary>
/// <param name="MapsFolder">The folder of converted maps (<c>maps</c> at the repository root by default, D109).</param>
/// <param name="MapName">The map json's name without the extension (<c>metastation</c>), tg's <c>/datum/map_config</c> name.</param>
/// <param name="Texts">
/// The text of map files that is loaded in place of what the files hold, by a file's full path, or null (docs/ai/specs/map-editor.md 5.7: the map
/// editor plays a map with the changes that are not saved yet).
/// </param>
public sealed record MapSelection(string MapsFolder, string MapName, IReadOnlyDictionary<string, string> Texts = null);

/// <summary>The world's map selection and its spawn points (per world, no statics).</summary>
public static class MapSeam
{
    /// <summary>
    /// Selects the map <paramref name="world"/>'s SSmapping loads (its translated <c>loadWorld</c> reads the selection's json
    /// and map files through the file seam), before <c>TgWorld.Boot</c>. Without a selection the world has no station level
    /// (the test rooms, M2's server).
    /// </summary>
    public static void Select(World world, MapSelection selection)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(selection);
        if (string.IsNullOrEmpty(selection.MapsFolder) || string.IsNullOrEmpty(selection.MapName))
            throw new ArgumentException("a map selection names a maps folder and a map json name", nameof(selection));
        Mount(world, selection);
        State(world).Selection = selection;
    }

    // M3 P2: the converted maps go into the world's file seam at tg's paths (docs/ai/specs/map-format.md 5), so tg's map
    // config and SSmapping read them unchanged: <MapsFolder>/map_jsons/<name>.json at _maps/map_jsons/<name>.json (tg's
    // MAP_DIRECTORY_MAPS), and each map file the json names, <MapsFolder>/<map_path>/<stem>.as3dmap, at
    // _maps/<map_path>/<map_file> (tg's own file name, holding our format: the engine part of /datum/parsed_map reads it).
    // The compiled-in map size (CompiledMapSize) is the largest of the files' sizes.
    private static void Mount(World world, MapSelection selection)
    {
        string jsonPath = Path.Combine(selection.MapsFolder, "map_jsons", selection.MapName + ".json");
        // (the map editor's Play gives the json's text too, with the settings that are not saved)
        string json = selection.Texts != null && selection.Texts.TryGetValue(Path.GetFullPath(jsonPath), out string givenJson) ? givenJson : null;
        if (json == null && !File.Exists(jsonPath))
            throw new FileNotFoundException("no converted map json (tools/convert-maps.sh writes it): " + jsonPath, jsonPath);
        json ??= File.ReadAllText(jsonPath);
        string mapPath;
        var mapFiles = new List<string>();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            mapPath = doc.RootElement.GetProperty("map_path").GetString();
            JsonElement files = doc.RootElement.GetProperty("map_file");
            if (files.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement f in files.EnumerateArray()) mapFiles.Add(f.GetString());
            }
            else
            {
                mapFiles.Add(files.GetString());
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("not a tg map json: " + jsonPath + " (" + e.Message + ")", e);
        }
        // the map's own settings (docs/ai/specs/map-editor.md 5.10): the json's key tg does not read, and the sky's picture beside the map's files
        MapSettings settings = MapSettings.Read(json);
        byte[] skyImage = null;
        if (settings.SkyImage != null)
        {
            string picture = Path.Combine(selection.MapsFolder, mapPath, settings.SkyImage);
            if (File.Exists(picture) && new FileInfo(picture).Length <= MapSettings.MaxSkyImageBytes)
                skyImage = File.ReadAllBytes(picture);
        }
        MapSettingsSeam.Set(world, settings, skyImage);
        IDmFileSystem seam = DmWorldState.Of(world).Files;
        seam.WriteText("_maps/map_jsons/" + selection.MapName + ".json", json);
        SeamState state = State(world);
        foreach (string mapFile in mapFiles)
        {
            string converted = Path.Combine(selection.MapsFolder, mapPath, Path.GetFileNameWithoutExtension(mapFile) + MapText.Extension);
            string text = selection.Texts != null && selection.Texts.TryGetValue(Path.GetFullPath(converted), out string given) ? given : null;
            if (text == null && !File.Exists(converted))
                throw new FileNotFoundException("no converted map (tools/convert-maps.sh writes it): " + converted, converted);
            text ??= File.ReadAllText(converted);
            seam.WriteText("_maps/" + mapPath + "/" + mapFile, text);
            (int w, int h) = HeaderSize(text, converted);
            state.CompiledWidth = Math.Max(state.CompiledWidth, w);
            state.CompiledHeight = Math.Max(state.CompiledHeight, h);
        }
        // M10 slice G2 (D272): the converted shuttle templates, <MapsFolder>/shuttles/<folder>/<name>.as3dmap, at tg's
        // _maps/shuttles/<folder>/<name>.dmm (the templates' mappath), where SSmapping's preloadShuttleTemplates finds them
        // D299: CentCom's level, at tg's path; a maps folder without it keeps CentCom's stand-in docks
        // A map editor's world (MapEditSeam) shows its map alone: CentCom and Lavaland keep their stand-in docks there too.
        bool alone = MapEditSeam.Selected(world);
        string centcom = Path.Combine(selection.MapsFolder, "map_files", "generic", "CentCom" + MapText.Extension);
        if (File.Exists(centcom) && !alone)
            seam.WriteText("_maps/map_files/generic/CentCom.dmm", File.ReadAllText(centcom));
        // D304: Lavaland's level; a maps folder without it keeps Lavaland's stand-in docks (DV236)
        string lavaland = Path.Combine(selection.MapsFolder, "map_files", "Mining", "Lavaland" + MapText.Extension);
        if (File.Exists(lavaland) && !alone)
            seam.WriteText("_maps/map_files/Mining/Lavaland.dmm", File.ReadAllText(lavaland));

        // Syndicate X1a (D316): the converted lazy templates, <MapsFolder>/templates/lazy_templates/<name>.as3dmap, at tg's
        // _maps/templates/lazy_templates/<name>.dmm, where /datum/lazy_template's lazy_load finds them
        string lazyTemplates = Path.Combine(selection.MapsFolder, "templates", "lazy_templates");
        if (Directory.Exists(lazyTemplates))
        {
            foreach (string converted in Directory.GetFiles(lazyTemplates, "*" + MapText.Extension).OrderBy(f => f, StringComparer.Ordinal))
                seam.WriteText("_maps/templates/lazy_templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        }

        // The survival capsules' shelters, <MapsFolder>/templates/shelter_*.as3dmap, at tg's _maps/templates/shelter_*.dmm, where SSmapping's
        // preloadShelterTemplates finds them (and preloadTemplates, as tg's every file of _maps/templates)
        string templates = Path.Combine(selection.MapsFolder, "templates");
        if (Directory.Exists(templates))
        {
            foreach (string converted in Directory.GetFiles(templates, "*" + MapText.Extension).OrderBy(f => f, StringComparer.Ordinal))
                seam.WriteText("_maps/templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        }

        // Gaps S4d (D327): the converted Mafia maps, <MapsFolder>/minigame/mafia/<name>.as3dmap, at tg's _maps/minigame/mafia/<name>.dmm,
        // where /datum/map_template/mafia's New points its mappath
        string mafiaMaps = Path.Combine(selection.MapsFolder, "minigame", "mafia");
        if (Directory.Exists(mafiaMaps))
        {
            foreach (string converted in Directory.GetFiles(mafiaMaps, "*" + MapText.Extension).OrderBy(f => f, StringComparer.Ordinal))
                seam.WriteText("_maps/minigame/mafia/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        }

        // Gaps BR1 (D339): the converted virtual domains, <MapsFolder>/virtual_domains/<name>.as3dmap, at tg's _maps/virtual_domains/<name>.dmm,
        // where the quantum server's lazy_load finds them, and the safehouses their modular map roots load, at _maps/safehouses/<name>.dmm
        foreach (string folder in new[] { "virtual_domains", "safehouses" })
        {
            string converted_folder = Path.Combine(selection.MapsFolder, folder);
            if (!Directory.Exists(converted_folder))
                continue;
            foreach (string converted in Directory.GetFiles(converted_folder, "*" + MapText.Extension).OrderBy(f => f, StringComparer.Ordinal))
                seam.WriteText("_maps/" + folder + "/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        }

        string shuttles = Path.Combine(selection.MapsFolder, "shuttles");
        if (Directory.Exists(shuttles))
        {
            foreach (string converted in Directory.GetFiles(shuttles, "*" + MapText.Extension, SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(shuttles, converted).Replace('\\', '/');
                seam.WriteText("_maps/shuttles/" + relative[..^MapText.Extension.Length] + ".dmm", File.ReadAllText(converted));
                // (a shuttle with no template type of its own gets one from its file, with what the json beside it says: DV506)
                string[] parts = relative[..^MapText.Extension.Length].Split('/');
                string beside = Path.ChangeExtension(converted, ".json");
                if (parts.Length == 2)
                    state.Shuttles.Add((parts[0], parts[1], File.Exists(beside) ? MapSettings.Read(File.ReadAllText(beside)) : MapSettings.Default));
            }
        }
    }

    // The size line of a converted map's header (map-format.md 4.1), without parsing the rest.
    private static (int Width, int Height) HeaderSize(string text, string name)
    {
        int lineStart = 0;
        for (int line = 0; line < 8 && lineStart < text.Length; line++)
        {
            int end = text.IndexOf('\n', lineStart);
            if (end < 0) end = text.Length;
            string header = text.Substring(lineStart, end - lineStart);
            lineStart = end + 1;
            if (!header.StartsWith("size ", StringComparison.Ordinal)) continue;
            string[] parts = header.Split(' ');
            if (parts.Length == 4 && int.TryParse(parts[1], out int w) && int.TryParse(parts[2], out int h)) return (w, h);
        }
        throw new InvalidDataException("a converted map without a size line: " + name);
    }

    /// <summary>
    /// The size BYOND gives the world from its compiled-in maps (<c>world.maxx</c>, <c>maxy</c>) when <paramref name="world"/>
    /// has a selected map: the largest of its map files (tg's compiled-in CentCom level is 255 x 255; D108 loads no level
    /// of it, so the size is the station's), or (0, 0) without a selection. <see cref="TgWorld.CompiledMap"/> sets it.
    /// </summary>
    public static (int Width, int Height) CompiledMapSize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        SeamState state = State(world);
        return state.Selection == null ? (0, 0) : (state.CompiledWidth, state.CompiledHeight);
    }

    /// <summary>The selection of <paramref name="world"/>, or null.</summary>
    public static MapSelection Selected(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return State(world).Selection;
    }

    /// <summary>
    /// Where players join a loaded map: the turfs of tg's late-join landmarks (<c>/obj/effect/landmark/latejoin</c>,
    /// <c>SSjob.latejoin_trackers</c>, the arrivals shuttle), in tg's list order, as the host's spawn points (the host
    /// cycles through them as it does through <c>TestRoom.SpawnPoints</c>). Empty before the map loaded. After
    /// <c>World.Step</c>, read-only.
    /// </summary>
    public static IReadOnlyList<SpawnPoint> LatejoinSpawns(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (State(world).Selection == null) return Array.Empty<SpawnPoint>();
        // tg's /obj/effect/landmark/latejoin/Initialize: SSjob.latejoin_trackers += loc (landmarks.dm:347)
        TgState tg = TgState.Of(world);
        var spawns = new List<SpawnPoint>();
        DmList trackers = tg.SSjob?.latejoin_trackers;
        for (int i = 1; trackers != null && i <= trackers.Count; i++)
        {
            // M10 slice G3: tg's trackers are the landmarks' turfs, or the arrivals shuttle's chairs (arrivals.dm LateInitialize)
            turf t = trackers[i] as turf ?? (trackers[i] as atom)?.dm_loc_raw as turf;
            if (t != null && t.dm_on_grid)
                spawns.Add(new SpawnPoint(t.dm_grid_x, t.dm_grid_y, t.dm_grid_z));
        }
        if (spawns.Count > 0) return spawns;
        // DEVIATION(DV26): no late-join landmark and no arrivals shuttle (D108): the Assistant start landmarks' turfs.
        // M3 P2 (D108): tg's station maps have no late-join landmark; a late joiner takes a chair of the arrivals shuttle
        // (arrivals.dm:31-58, SSjob.get_last_resort_spawn_points, job.dm:767), a shuttle template SSshuttle loads, which
        // D108 does not, and tg's last resort (the arrivals area, then the CentCom error room) is not loaded either. The
        // host then spawns players where tg puts a round-start Assistant: the turfs of the job start landmarks named
        // JOB_ASSISTANT, in the order they initialized (GLOB.start_landmarks_list's order before M10 slice F's round start
        // shuffles it and deletes them: NoteStartLandmark keeps them).
        return State(world).AssistantStarts;
    }

    // M3 P2: the per-world state of the seam (a World extension, no statics).
    /// <summary>
    /// DV26: a start landmark that initialized (<c>/obj/effect/landmark/start/Initialize</c>); an Assistant's turf becomes one of
    /// the host's spawn points, kept after the round start shuffles and deletes the landmarks.
    /// </summary>
    public static void NoteStartLandmark(obj_effect_landmark_start start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (start.name == Defines.JOB_ASSISTANT && start.dm_loc_raw is turf t && t.dm_on_grid)
            State(World.Current).AssistantStarts.Add(new SpawnPoint(t.dm_grid_x, t.dm_grid_y, t.dm_grid_z));
    }


    internal sealed class SeamState
    {
        internal MapSelection Selection;

        // DV26: the Assistant start landmarks' turfs, in the order they initialized (NoteStartLandmark).
        internal readonly List<SpawnPoint> AssistantStarts = new();

        // The largest map file size of the selection (CompiledMapSize).
        // the shuttles of the maps folder, shuttles/<folder>/<name>, each with what the json beside its file says of it (DV506)
        internal readonly List<(string Folder, string Name, MapSettings Data)> Shuttles = new();
        // the shuttle whose template is being made from its file, while it is (the world's own: no static state)
        internal (string PortId, string Suffix, MapSettings Data)? MakingShuttle;
        // the things whose dir their map set (DV508); held weakly, so a deleted thing is let go
        internal readonly System.Runtime.CompilerServices.ConditionalWeakTable<atom, object> MapDirs = new();
        internal int CompiledWidth;
        internal int CompiledHeight;
    }

    internal static SeamState State(World world) => world.GetExtension(static _ => new SeamState());

    /// <summary>
    /// The shuttle maps the maps folder holds (<c>shuttles/FOLDER/NAME</c>), each with the data of the json beside its file: SSmapping makes a template
    /// for one that has no template type of its own (DV506).
    /// </summary>
    public static IReadOnlyList<(string Folder, string Name, MapSettings Data)> ShuttleFiles(World world) => State(world).Shuttles;

    /// <summary>The shuttle a template is being made for from its file (DV506), which the template's New reads; null: none.</summary>
    public static (string PortId, string Suffix, MapSettings Data)? ShuttleBeingMade(World world) => State(world).MakingShuttle;

    /// <summary>Says which shuttle the next template made is for (SSmapping.preloadShuttleTemplates), and null once it is made.</summary>
    public static void MakeShuttle(World world, (string PortId, string Suffix, MapSettings Data)? shuttle) => State(world).MakingShuttle = shuttle;

    /// <summary>
    /// DV508: notes that a thing faces the way its map says (its <c>dir</c> is a var the map set on it, or on the random spawner that made it): the
    /// 3D models' own facing rule (the server's ModelDirRules: away from the wall behind it) then leaves it as the mapper turned it. Inside a world.
    /// </summary>
    public static void NoteMapDir(atom thing)
    {
        if (thing != null)
            State(World.Current).MapDirs.AddOrUpdate(thing, null);
    }

    /// <summary>True for a thing whose map set its dir (<see cref="NoteMapDir"/>). Inside a world.</summary>
    public static bool HasMapDir(atom thing) => thing != null && State(World.Current).MapDirs.TryGetValue(thing, out _);
}
