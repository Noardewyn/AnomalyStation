using System.Diagnostics;

namespace Anomaly.Tools.Defines;

/// <summary>
/// <c>dotnet run --project tools/Anomaly.Tools.Defines -- --out src/Core/Tg/Generated/Defines [--tg path] [--check]</c>.
/// Converts tg's <c>#define</c>s to C# constants (docs/ai/specs/defines-converter.md). <c>--check</c> writes nothing
/// and exits with 1 when the output directory differs from what would be generated.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string tg = null;
        string outDir = null;
        bool check = false;
        bool verbose = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tg" when i + 1 < args.Length:
                    tg = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--check":
                    check = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    Console.Error.WriteLine($"unknown argument {args[i]}");
                    Console.Error.WriteLine("usage: Anomaly.Tools.Defines --out <dir> [--tg <tgstation clone>] [--check] [--verbose]");
                    return 2;
            }
        }
        if (outDir == null)
        {
            Console.Error.WriteLine("usage: Anomaly.Tools.Defines --out <dir> [--tg <tgstation clone>] [--check] [--verbose]");
            return 2;
        }

        string root;
        try
        {
            root = TgLocator.Locate(tg, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
        }
        catch (DirectoryNotFoundException e)
        {
            // --tg or $ANOMALY_TGSTATION names a directory without tgstation.dme.
            Console.Error.WriteLine(e.Message);
            return 2;
        }
        if (root == null)
        {
            Console.Error.WriteLine($"tgstation clone not found: pass --tg, set ${TgLocator.EnvironmentVariable}, or put it at ../tgstation next to the repository");
            return 2;
        }

        var watch = Stopwatch.StartNew();
        ConverterOutput output = DefinesConverter.Generate(root);
        Console.WriteLine($"tg: {root} (snapshot {output.Snapshot}), {output.Preprocessor.Files.Count} files");
        Console.WriteLine($"defines seen: {output.Results.Count}, emitted: {output.Results.Count(r => r.Emitted)}, not emitted: {output.Results.Count(r => !r.Emitted)}");
        foreach (IGrouping<string, DefineResult> g in output.Results.GroupBy(r => r.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {g.Key,-14} {g.Count(),6} ({g.Count(r => r.Emitted)} emitted)");
        foreach (string e in output.Preprocessor.ActiveErrorsAndWarnings)
            Console.WriteLine($"active #error/#warn: {e}");
        if (verbose)
        {
            foreach (string w in output.Warnings)
                Console.WriteLine($"warning: {w}");
        }
        else if (output.Warnings.Count > 0)
        {
            Console.WriteLine($"{output.Warnings.Count} warning(s); --verbose lists them");
        }
        if (output.Errors.Count > 0)
        {
            foreach (string e in output.Errors)
                Console.Error.WriteLine($"error: {e}");
            Console.Error.WriteLine("nothing written");
            return 1;
        }

        if (check)
        {
            List<string> differences = DefinesConverter.Diff(output, outDir);
            foreach (string d in differences)
                Console.WriteLine($"differs: {d}");
            Console.WriteLine(differences.Count == 0 ? "up to date" : $"{differences.Count} file(s) differ from the generated output");
            return differences.Count == 0 ? 0 : 1;
        }

        int changed = DefinesConverter.Write(output, outDir);
        Console.WriteLine($"{output.Files.Count} files in {Path.GetFullPath(outDir)}, {changed} written or deleted, {watch.ElapsedMilliseconds} ms");
        return 0;
    }
}
