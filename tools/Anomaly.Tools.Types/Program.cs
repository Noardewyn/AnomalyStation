using System.Diagnostics;

namespace Anomaly.Tools.Types;

// The command line (docs/ai/specs/types-converter.md, section 1):
//   --tg <tg> --opendream <od> --out <repo> [--scratch <dir>] [--no-cache]   write every output
//   ... --check                                                              compare, write nothing (exit 1 on a difference)
//   ... --model-only                                                         compile tg and cache the model only
//   --out <repo> --summary                                                   the tracker's totals from types-index.tsv (no clone)
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("usage: Anomaly.Tools.Types --tg <tg> --opendream <od> --out <repo> [--check | --summary | --model-only] [--scratch <dir>] [--no-cache]");
            return args.Length == 0 ? 2 : 0;
        }
        // Two spellings: "generate|check|model|summary --tg ..." and the contract's "--tg ... --out ... [--check] [--summary]"
        // (types-converter.md 1).
        string command = args[0].StartsWith("--", StringComparison.Ordinal) ? "generate" : args[0];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = command == args[0] ? 1 : 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--no-cache") { options["no-cache"] = "1"; continue; }
            if (a == "--check") { command = "check"; continue; }
            if (a == "--summary") { command = "summary"; continue; }
            if (a == "--model-only") { command = "model"; continue; }
            if (!a.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"bad argument: {a}");
                return 2;
            }
            options[a[2..]] = args[++i];
        }
        string repo = Path.GetFullPath(options.GetValueOrDefault("out") ?? ".");
        try
        {
            switch (command)
            {
                case "summary":
                    return Summary.Run(repo, Console.Out);
                case "model":
                case "generate":
                case "check":
                {
                    var sw = Stopwatch.StartNew();
                    TgModel model = LoadOrCompile(options);
                    string tgDir = Path.GetFullPath(options["tg"]);
                    Greps greps = Greps.Run(tgDir);
                    model.NullUsed = greps.NullUsed.OrderBy(x => x, StringComparer.Ordinal).ToList();
                    model.TypepathAssigned = greps.TypepathAssigned.OrderBy(x => x, StringComparer.Ordinal).ToList();
                    model.MetaTypes = MapTypes.Read(Path.Combine(tgDir, MapTypes.MetaStation));
                    model.MapEdits = MapEdits.Read(tgDir);
                    model.IconSizes = IconSizes.Read(tgDir);
                    model.KindTests = greps.KindTests;
                    Console.WriteLine($"greps: {model.NullUsed.Count} null-used names, {model.TypepathAssigned.Count} typepath-assigned names; MetaStation: {model.MetaTypes.Count} types; station map edits: {model.MapEdits.Count} (path, var, kind); kind tests: {model.KindTests.Count} (file, name) ({sw.Elapsed.TotalSeconds:F1} s)");
                    Console.WriteLine($"model: {model.Types.Count} types, {model.Types.Sum(t => t.Procs.Count)} procs, {model.GlobalProcs.Count} global procs ({sw.Elapsed.TotalSeconds:F1} s)");
                    if (command == "model") return 0;
                    var gen = new Generator(model, repo, Console.Out);
                    Outputs outputs = gen.Run();
                    Console.WriteLine($"generated {outputs.Files.Count} files ({sw.Elapsed.TotalSeconds:F1} s)");
                    string report = Path.Combine(Scratch(options), "types-report.txt");
                    File.WriteAllText(report, string.Join("\n", outputs.Report) + "\n");
                    Console.WriteLine($"report: {outputs.Report.Count} lines in {report}");
                    return command == "check" ? outputs.Check(repo, Console.Out) : outputs.Write(repo, Console.Out);
                }
                default:
                    Console.Error.WriteLine($"unknown command: {command}");
                    return 2;
            }
        }
        catch (ToolException e)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static string Scratch(Dictionary<string, string> options)
        => Path.GetFullPath(options.GetValueOrDefault("scratch") ?? Path.Combine(Path.GetTempPath(), "anomaly-types"));

    private static TgModel LoadOrCompile(Dictionary<string, string> options)
    {
        string tg = Path.GetFullPath(options.GetValueOrDefault("tg") ?? throw new ToolException("--tg is required"));
        string od = Path.GetFullPath(options.GetValueOrDefault("opendream") ?? throw new ToolException("--opendream is required"));
        string scratch = Scratch(options);
        if (!File.Exists(Path.Combine(tg, "tgstation.dme"))) throw new ToolException($"no tgstation.dme in {tg}");
        if (!Directory.Exists(Path.Combine(od, "DMCompiler"))) throw new ToolException($"no DMCompiler in {od}");

        string tgCommit = Clones.GitHead(tg) ?? throw new ToolException($"{tg} is not a git checkout");
        string odCommit = Clones.GitHead(od) ?? throw new ToolException($"{od} is not a git checkout");
        string rtCommit = Clones.GitHead(Path.Combine(od, "RobustToolbox")) ?? "unknown";
        if (!odCommit.StartsWith(Clones.OpenDreamPin, StringComparison.Ordinal))
            throw new ToolException($"OpenDream is at {odCommit[..10]}, the pin is {Clones.OpenDreamPin} (D110)");
        if (!rtCommit.StartsWith(Clones.RobustToolboxPin, StringComparison.Ordinal))
            throw new ToolException($"RobustToolbox is at {rtCommit}, the pin is {Clones.RobustToolboxPin} (D110)");
        if (!tgCommit.StartsWith(Clones.TgPin, StringComparison.Ordinal))
            throw new ToolException($"tg is at {tgCommit[..10]}, the snapshot is {Clones.TgPin}");
        string compiled = Clones.CompiledOpenDream;
        if (compiled == null)
            throw new ToolException("this build has no OpenDream front end: build it with the clone (set ANOMALY_OPENDREAM)");
        if (!string.Equals(Path.GetFullPath(compiled).TrimEnd('/'), od.TrimEnd('/'), StringComparison.Ordinal))
            Console.WriteLine($"note: the front end was compiled from {compiled}; --opendream is {od}");

        string cache = Path.Combine(scratch, "model.json");
        if (!options.ContainsKey("no-cache") && File.Exists(cache))
        {
            try
            {
                TgModel cached = TgModel.Load(cache);
                if (cached.Format == TgModel.FormatVersion && cached.TgCommit == tgCommit && cached.OpenDreamCommit == odCommit
                    && cached.Defines == Clones.DefinesText)
                {
                    Console.WriteLine($"model: cached {cache}");
                    return cached;
                }
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
            {
                // recompile
            }
        }
#if ANOMALY_OPENDREAM
        string dme = Clones.MakeFarm(tg, scratch);
        string cwd = Directory.GetCurrentDirectory();
        TgModel model;
        try
        {
            model = OdFrontEnd.Compile(dme, Clones.Defines, Clones.DmVersion, Clones.DmBuild, Console.Out);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            string json = Path.ChangeExtension(dme, "json");
            if (File.Exists(json)) File.Delete(json); // OpenDream's own output (59 MB), not used
        }
        model.TgCommit = tgCommit;
        model.OpenDreamCommit = odCommit;
        model.RobustToolboxCommit = rtCommit;
        model.Defines = Clones.DefinesText;
        model.Save(cache);
        return model;
#else
        throw new ToolException("this build has no OpenDream front end: build it with the clone (set ANOMALY_OPENDREAM)");
#endif
    }
}

/// <summary>An error the tool reports without a stack trace.</summary>
internal sealed class ToolException(string message) : Exception(message);
