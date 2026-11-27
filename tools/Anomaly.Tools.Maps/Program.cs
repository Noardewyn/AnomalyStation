using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Anomaly.Tools.Maps;

/// <summary>The command line of docs/ai/specs/map-format.md 3.</summary>
public static class Program
{
    private const string Usage =
        "usage: dotnet run --project tools/Anomaly.Tools.Maps -- [--tg <tg clone>] [--out <folder>] [--map <json name>]... [--all-station-maps]\n"
        + "                                                [--check] [--allow-bad-paths] [--types <types-index.tsv>] [--od-json <tgstation.json>]\n"
        + "  --tg               the tg clone (default ANOMALY_TG, or ../tgstation beside the repository)\n"
        + "  --out              the output folder (default maps at the repository root)\n"
        + "  --map              a map json of _maps/map_jsons (repeatable; default metastation and multiz_debug, D108, D325)\n"
        + "  --all-station-maps every map json of _maps/map_jsons\n"
        + "  --shuttle          a shuttle template of _maps/shuttles, as <folder>/<name>.dmm (repeatable; default the round-start\n"
        + "                     templates MetaStation's docks load, M10 slice G, D272)\n"
        + "  --check            convert into memory and compare with the files on disk (exit 1 on a difference)\n"
        + "  --allow-bad-paths  leave unknown paths and vars out (listed) instead of refusing the map\n"
        + "  --types            the type index (default docs/ai/specs/generated/types-index.tsv; without it only the syntax is checked)\n"
        + "  --od-json          OpenDream's compiled JSON of tg, for the var check (types-index.tsv has no vars)\n"
        + "exit codes: 0 done, 1 --check found a difference, 2 a validation error, 3 a usage or I/O error";

    public static int Main(string[] args)
    {
        try
        {
            return Run(args, Console.Out, Console.Error);
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 3;
        }
        catch (UnauthorizedAccessException e)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 3;
        }
    }

    /// <summary>Runs the tool (the entry point's body, for tests).</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        string tg = null, outDir = null, typesPath = null, odJson = null;
        var mapNames = new List<string>();
        var shuttles = new List<string>();
        bool all = false, check = false, allowBad = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next()
            {
                if (i + 1 >= args.Length) throw new ArgumentException(a + " needs a value");
                return args[++i];
            }
            try
            {
                switch (a)
                {
                    case "--tg": tg = Next(); break;
                    case "--out": outDir = Next(); break;
                    case "--map": mapNames.Add(Next()); break;
                    case "--all-station-maps": all = true; break;
                    case "--shuttle": shuttles.Add(Next()); break;
                    case "--check": check = true; break;
                    case "--allow-bad-paths": allowBad = true; break;
                    case "--types": typesPath = Next(); break;
                    case "--od-json": odJson = Next(); break;
                    case "-h":
                    case "--help":
                        output.WriteLine(Usage);
                        return 0;
                    default:
                        error.WriteLine("unknown argument: " + a);
                        error.WriteLine(Usage);
                        return 3;
                }
            }
            catch (ArgumentException e)
            {
                error.WriteLine(e.Message);
                return 3;
            }
        }

        string repo = FindRepoRoot(Directory.GetCurrentDirectory());
        tg ??= Environment.GetEnvironmentVariable("ANOMALY_TG");
        if (string.IsNullOrEmpty(tg)) tg = FindTgClone(repo ?? Directory.GetCurrentDirectory());
        if (tg == null || !Directory.Exists(Path.Combine(tg, "_maps")))
        {
            error.WriteLine("error: no tg clone (--tg, ANOMALY_TG or ../tgstation)");
            return 3;
        }
        outDir ??= Path.Combine(repo ?? Directory.GetCurrentDirectory(), "maps");

        TypeIndex types;
        if (typesPath != null && !File.Exists(typesPath))
        {
            error.WriteLine("error: no type index at " + typesPath);
            return 3;
        }
        typesPath ??= repo == null ? null : Path.Combine(repo, "docs", "ai", "specs", "generated", "types-index.tsv");
        if (typesPath != null && File.Exists(typesPath))
        {
            types = TypeIndex.LoadTsv(typesPath);
        }
        else if (odJson != null)
        {
            types = new TypeIndex();
        }
        else
        {
            output.WriteLine("note: no type index (docs/ai/specs/generated/types-index.tsv): only the syntax and the member order are checked");
            types = TypeIndex.Permissive();
        }
        if (odJson != null)
        {
            if (!File.Exists(odJson))
            {
                error.WriteLine("error: no OpenDream JSON at " + odJson);
                return 3;
            }
            types.AddOpenDreamJson(odJson);
        }
        if (!types.IsPermissive && !types.HasVarData)
            output.WriteLine("note: the type index has no vars (--od-json adds them): the var check is skipped");

        string jsonDir = Path.Combine(tg, "_maps", "map_jsons");
        if (all)
            mapNames.AddRange(Directory.GetFiles(jsonDir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal));
        if (mapNames.Count == 0) mapNames.AddRange(["metastation", "multiz_debug"]); // D325: the multi-z debug map
        string commit = TgCommit(tg);

        int exit = 0;
        var written = new List<string>();
        foreach (string name in mapNames.Distinct(StringComparer.Ordinal))
        {
            string jsonPath = Path.Combine(jsonDir, name + ".json");
            if (!File.Exists(jsonPath))
            {
                error.WriteLine("error: no map json " + jsonPath);
                return 3;
            }
            string jsonText = File.ReadAllText(jsonPath);
            string mapPath;
            List<string> mapFiles = new List<string>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonText, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                mapPath = doc.RootElement.GetProperty("map_path").GetString();
                JsonElement files = doc.RootElement.GetProperty("map_file");
                if (files.ValueKind == JsonValueKind.Array) mapFiles.AddRange(files.EnumerateArray().Select(f => f.GetString()));
                else mapFiles.Add(files.GetString());
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                error.WriteLine($"error: {jsonPath}: not a tg map json ({e.Message})");
                return 3;
            }

            bool padded = name == MultizPadding.Map; // DV436: space levels under and over the debug map's decks
            var outputs = new List<(string Path, string Text)> { (Path.Combine(outDir, "map_jsons", name + ".json"), padded ? MultizPadding.PadJson(jsonText) : jsonText) };
            foreach (string mapFile in mapFiles)
            {
                string relative = "_maps/" + mapPath + "/" + mapFile;
                string dmmPath = Path.Combine(tg, "_maps", mapPath, mapFile);
                if (!File.Exists(dmmPath))
                {
                    error.WriteLine("error: no map file " + dmmPath);
                    return 3;
                }
                string dmmText = File.ReadAllText(dmmPath);
                if (padded) dmmText = MultizPadding.PadDmm(dmmText);
                var sw = Stopwatch.StartNew();
                ConversionResult result;
                try
                {
                    result = MapConverter.Convert(dmmText, relative, types, allowBad, commit);
                }
                catch (FormatException e)
                {
                    error.WriteLine("error: " + e.Message);
                    return 2;
                }
                foreach ((string bad, SortedSet<string> keys) in result.BadPaths)
                    output.WriteLine($"  bad path {bad} (keys {string.Join(" ", keys.Take(12))}{(keys.Count > 12 ? " ..." : "")})");
                if (result.Text == null)
                {
                    foreach (MapFinding finding in result.Findings.Take(50)) error.WriteLine($"  {relative}: {finding}");
                    if (result.Findings.Count > 50) error.WriteLine($"  ... {result.Findings.Count - 50} more");
                    error.WriteLine($"{relative}: refused ({result.Findings.Count} findings{(allowBad ? "" : "; --allow-bad-paths leaves unknown paths and vars out")})");
                    return 2;
                }
                double seconds = sw.Elapsed.TotalSeconds;
                if (check)
                {
                    List<string> differences = MapConverter.RoundTrip(dmmText, result.Text, types);
                    foreach (string d in differences) error.WriteLine($"  round trip: {relative}: {d}");
                    if (differences.Count > 0) exit = Math.Max(exit, 2);
                }
                string target = Path.Combine(outDir, mapPath, Path.GetFileNameWithoutExtension(mapFile) + Anomaly.Core.Tg.MapText.Extension);
                outputs.Add((target, result.Text));
                output.WriteLine($"{relative}: {result.Width}x{result.Height}x{result.Levels}, tiles {result.Tiles}, lines {result.Lines}, atoms {result.Atoms}, "
                    + $"types {result.DistinctTypes}, areas {result.Areas}, movables {result.Movables}, bytes {Encoding.UTF8.GetByteCount(result.Text)}, {seconds:F2} s");
            }
            foreach ((string path, string text) in outputs)
            {
                if (check)
                {
                    if (!File.Exists(path) || File.ReadAllText(path) != text)
                    {
                        output.WriteLine("differs: " + path);
                        exit = Math.Max(exit, 1);
                    }
                    continue;
                }
                if (WriteIfChanged(path, text)) written.Add(path);
            }
        }

        // M10 slice G (D272): the shuttle templates tg's SSmapping preloads and the docks load, at _maps/shuttles/<folder>/<name>.dmm,
        // converted to maps/shuttles/<folder>/<name>.as3dmap (the host mounts each at tg's path); Meta's by default when Meta is converted
        if (shuttles.Count == 0 && mapNames.Contains("metastation", StringComparer.Ordinal)) shuttles.AddRange(MetaShuttles);
        // D299: CentCom's level goes with Meta, at maps/map_files/generic/CentCom.as3dmap; D304: so does Lavaland (Meta's minetype)
        var templates = shuttles.Distinct(StringComparer.Ordinal).Select(shuttle => "shuttles/" + shuttle).ToList();
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(["map_files/generic/CentCom.dmm", "map_files/Mining/Lavaland.dmm"]);
        // Syndicate X1a (D316): the lazy templates SSmapping loads on demand, at maps/templates/lazy_templates/<name>.as3dmap; the ninja
        // holding facility (the contractor's victims are held there)
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(LazyTemplates);
        // Gaps S4d (D327): the Mafia maps the game board loads on CentCom's game area, at maps/minigame/mafia/<name>.as3dmap
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(MafiaMaps);
        // Gaps BR1 (D339): the virtual domains the quantum server loads (lazy templates at maps/virtual_domains/<name>.as3dmap) and the
        // safehouses their modular map roots load (maps/safehouses/<name>.as3dmap)
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(VirtualDomains);
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(Safehouses);
        // The survival capsules' shelters (tg's /datum/map_template/shelter mappaths) the miners unfold on Lavaland, at maps/templates/<name>.as3dmap
        if (mapNames.Contains("metastation", StringComparer.Ordinal)) templates.AddRange(Shelters);
        foreach (string template in templates)
        {
            string relative = "_maps/" + template;
            string dmmPath = Path.Combine(tg, "_maps", template);
            if (!File.Exists(dmmPath))
            {
                error.WriteLine("error: no template " + dmmPath);
                return 3;
            }
            string dmmText = File.ReadAllText(dmmPath);
            ConversionResult result;
            try
            {
                result = MapConverter.Convert(dmmText, relative, types, allowBad, commit);
            }
            catch (FormatException e)
            {
                error.WriteLine("error: " + e.Message);
                return 2;
            }
            foreach ((string bad, SortedSet<string> keys) in result.BadPaths)
                output.WriteLine($"  bad path {bad} (keys {string.Join(" ", keys.Take(12))}{(keys.Count > 12 ? " ..." : "")})");
            if (result.Text == null)
            {
                foreach (MapFinding finding in result.Findings.Take(50)) error.WriteLine($"  {relative}: {finding}");
                error.WriteLine($"{relative}: refused ({result.Findings.Count} findings{(allowBad ? "" : "; --allow-bad-paths leaves unknown paths and vars out")})");
                return 2;
            }
            string target = Path.Combine(outDir, Path.ChangeExtension(template, null) + Anomaly.Core.Tg.MapText.Extension);
            output.WriteLine($"{relative}: {result.Width}x{result.Height}, atoms {result.Atoms}");
            if (check)
            {
                if (!File.Exists(target) || File.ReadAllText(target) != result.Text)
                {
                    output.WriteLine("differs: " + target);
                    exit = Math.Max(exit, 1);
                }
            }
            else if (WriteIfChanged(target, result.Text))
            {
                written.Add(target);
            }
        }

        string stamp = Path.Combine(outDir, ".as3dmaps-stamp");
        string stampText = $"tg {commit ?? "unknown"}\ntool {MapConverter.ToolVersion}\nformat {Anomaly.Core.Tg.MapText.FormatVersion}\n";
        if (check)
        {
            if (!File.Exists(stamp) || File.ReadAllText(stamp) != stampText)
            {
                output.WriteLine("differs: " + stamp);
                exit = Math.Max(exit, 1);
            }
        }
        else
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(stamp, stampText);
            output.WriteLine(written.Count == 0 ? "up to date: " + outDir : $"wrote {written.Count} file(s) into {outDir}");
        }
        return exit;
    }

    /// <summary>
    /// Gaps S4d (D327): the Mafia maps (tg's code/modules/mafia/map_pieces.dm map_suffix values; the unit test map is UNIT_TESTS only).
    /// </summary>
    internal static readonly string[] MafiaMaps = ["minigame/mafia/mafia_ball.dmm", "minigame/mafia/mafia_ayylmao.dmm",
        "minigame/mafia/mafia_spiderclan.dmm", "minigame/mafia/mafia_gothic.dmm", "minigame/mafia/mafia_syndie.dmm",
        "minigame/mafia/mafia_snow.dmm", "minigame/mafia/mafia_lavaland.dmm"];

    /// <summary>
    /// Gaps BR1 (D339): every virtual domain of tg's _maps/virtual_domains (the subtypes of /datum/lazy_template/virtual_domain, their
    /// map_name values: SSbitrunning offers them all) and every safehouse of strings/modular_maps/safehouse.toml.
    /// </summary>
    internal static readonly string[] VirtualDomains = ["virtual_domains/abductor_ship.dmm", "virtual_domains/ash_drake.dmm",
        "virtual_domains/beach_bar.dmm", "virtual_domains/blood_drunk_miner.dmm", "virtual_domains/breeze_bay.dmm", "virtual_domains/bubblegum.dmm",
        "virtual_domains/clown_planet.dmm", "virtual_domains/colossus.dmm", "virtual_domains/fredingtonfastingbear.dmm",
        "virtual_domains/gondola_asteroid.dmm", "virtual_domains/grasslands_hunt.dmm", "virtual_domains/heretic_hunt.dmm",
        "virtual_domains/hierophant.dmm", "virtual_domains/island_brawl.dmm", "virtual_domains/meta_central.dmm", "virtual_domains/pipedream.dmm",
        "virtual_domains/pirates.dmm", "virtual_domains/psyker_shuffle.dmm", "virtual_domains/psyker_zombies.dmm",
        "virtual_domains/stairs_and_cliffs.dmm", "virtual_domains/syndicate_assault.dmm", "virtual_domains/test_only.dmm",
        "virtual_domains/vaporwave.dmm", "virtual_domains/wendigo.dmm", "virtual_domains/xeno_nest.dmm"];

    /// <summary>
    /// The survival capsules' shelters: the mining shelters of code/modules/mining/shelters.dm (the fishing spots come with fishing).
    /// </summary>
    internal static readonly string[] Shelters = ["templates/shelter_1.dmm", "templates/shelter_2.dmm", "templates/shelter_3.dmm",
        "templates/shelter_t.dmm"];

    internal static readonly string[] Safehouses = ["safehouses/abductor.dmm", "safehouses/bathroom.dmm", "safehouses/den.dmm",
        "safehouses/dig.dmm", "safehouses/ice.dmm", "safehouses/lavaland_boss.dmm", "safehouses/maintsafe.dmm", "safehouses/mine.dmm",
        "safehouses/shuttle.dmm", "safehouses/shuttle_space.dmm", "safehouses/test_only_safehouse.dmm", "safehouses/wood.dmm"];

    /// <summary>
    /// Syndicate X1a (D316): the lazy templates (tg's code/datums/lazy_template.dm map names) the port loads: the ninja holding facility.
    internal static readonly string[] LazyTemplates = ["templates/lazy_templates/ninja_den.dmm",
        // Syndicate N1b (D316): the nuclear operatives' base and the elevator they arrive in
        "templates/lazy_templates/nukie_base.dmm", "templates/lazy_templates/nukie_elevator.dmm"];

    /// The shuttle templates of MetaStation's round-start docks (the stationary ports' roundstart_template, the map json's shuttles and
    /// the CentCom docks' json keys, which slice G's stand-in loads): arrivals, cargo, emergency, ferry, the mining, common mining and
    /// labour shuttles, the escape pods, the auxiliary base, and the backup emergency shuttle of CentCom's backup dock; the antagonists'
    /// ships; and (gaps CM1) the emergency shuttles the communications console sells.
    /// </summary>
    internal static readonly string[] MetaShuttles =
    {
        "arrival/arrival_box.dmm", "cargo/cargo_box.dmm", "emergency/emergency_meta.dmm", "ferry/ferry_fancy.dmm",
        "emergency/emergency_backup.dmm", "mining/mining_delta.dmm", "mining/mining_common_meta.dmm", "labour/labour_delta.dmm", "escape_pod/escape_pod_default.dmm",
        "aux_base/aux_base_default.dmm",
        // Syndicate N1b (D316): the nuclear operatives' infiltrator (its leader loads it at the round's start) and the syndicate base's
        // assault pod (its dock's roundstart_template)
        "infiltrator/infiltrator_basic.dmm", "assault_pod_default.dmm",
        // Syndicate N3 (D316): the clown operatives' infiltrator (their leader loads it)
        "infiltrator/infiltrator_clown.dmm",
        // Gaps CM1 (D333): the emergency shuttle catalogue the communications console sells (code/datums/shuttles/emergency.dm's
        // templates with a price and a purchase access), which the shuttle catastrophe also picks from; not the Scrapheap Challenge
        // (its modular map roots, /obj/modular_map_root, are not ported) nor the Arena (its arena level, _maps/templates/the_arena.dmm,
        // is not converted): not-translated.md 200
        "emergency/emergency_construction.dmm", "emergency/emergency_constructionbig.dmm", "emergency/emergency_asteroid.dmm",
        "emergency/emergency_venture.dmm", "emergency/emergency_humpback.dmm", "emergency/emergency_bar.dmm",
        "emergency/emergency_russiafightpit.dmm", "emergency/emergency_meteor.dmm", "emergency/emergency_monastery.dmm",
        "emergency/emergency_luxury.dmm", "emergency/emergency_medisim.dmm", "emergency/emergency_discoinferno.dmm",
        "emergency/emergency_birdboat.dmm", "emergency/emergency_box.dmm", "emergency/emergency_donut.dmm",
        "emergency/emergency_clown.dmm", "emergency/emergency_kilo.dmm", "emergency/emergency_mini.dmm", "emergency/emergency_tram.dmm",
        "emergency/emergency_birdshot.dmm", "emergency/emergency_catwalk.dmm", "emergency/emergency_wawa.dmm",
        "emergency/emergency_narnar.dmm", "emergency/emergency_pubby.dmm", "emergency/emergency_cere.dmm",
        "emergency/emergency_supermatter.dmm", "emergency/emergency_imfedupwiththisworld.dmm", "emergency/emergency_goon.dmm",
        "emergency/emergency_rollerdome.dmm", "emergency/emergency_bballhooper.dmm", "emergency/emergency_wabbajack.dmm",
        "emergency/emergency_omega.dmm", "emergency/emergency_cruise.dmm", "emergency/emergency_nature.dmm",
        "emergency/emergency_casino.dmm", "emergency/emergency_shadow.dmm", "emergency/emergency_fish.dmm",
        "emergency/emergency_lance.dmm", "emergency/emergency_tranquility.dmm", "emergency/emergency_hugcage.dmm",
        "emergency/emergency_fame.dmm", "emergency/emergency_delta.dmm", "emergency/emergency_northstar.dmm",
        "emergency/emergency_nebula.dmm", "emergency/emergency_raven.dmm", "emergency/emergency_zeta.dmm",
        "emergency/emergency_tombstone.dmm", "emergency/emergency_ark.dmm", "emergency/emergency_dep_cap.dmm",
        "emergency/emergency_dep_civ.dmm", "emergency/emergency_dep_eng.dmm", "emergency/emergency_dep_med.dmm",
        "emergency/emergency_dep_sci.dmm", "emergency/emergency_dep_sec.dmm", "emergency/emergency_dep_srv.dmm",
        "emergency/emergency_dep_sup.dmm",
    };

    // Writes UTF-8 without a byte order mark when the content differs (an unchanged file keeps its time).
    private static bool WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return true;
    }

    /// <summary>The folder with AnomalyStation.sln, from <paramref name="start"/> upward, or null.</summary>
    internal static string FindRepoRoot(string start)
    {
        for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "AnomalyStation.sln"))) return dir;
        }
        return null;
    }

    // ../tgstation beside the repository or one of its parents (a worktree sits inside the main checkout).
    private static string FindTgClone(string start)
    {
        for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tgstation");
            if (Directory.Exists(Path.Combine(candidate, "_maps"))) return candidate;
        }
        return null;
    }

    /// <summary>The tg clone's commit (the first 8 hex digits of HEAD), read from its .git folder, or null.</summary>
    internal static string TgCommit(string tg)
    {
        try
        {
            string git = Path.Combine(tg, ".git");
            if (File.Exists(git))
            {
                string pointer = File.ReadAllText(git).Trim();
                if (!pointer.StartsWith("gitdir: ", StringComparison.Ordinal)) return null;
                git = Path.GetFullPath(Path.Combine(tg, pointer.Substring(8)));
            }
            string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
            string sha = head;
            if (head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                string reference = head.Substring(5);
                string loose = Path.Combine(git, reference);
                if (File.Exists(loose))
                {
                    sha = File.ReadAllText(loose).Trim();
                }
                else
                {
                    string packed = Path.Combine(git, "packed-refs");
                    sha = File.Exists(packed)
                        ? File.ReadLines(packed).Select(l => l.Split(' ')).FirstOrDefault(p => p.Length == 2 && p[1] == reference)?[0]
                        : null;
                }
            }
            return sha != null && sha.Length >= 8 ? sha.Substring(0, 8) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
