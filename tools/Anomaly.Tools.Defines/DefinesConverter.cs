using System.Text;

namespace Anomaly.Tools.Defines;

public sealed class ConverterOutput
{
    public Preprocessor Preprocessor { get; init; }

    public PreprocessorConfig Config { get; init; }

    public List<DefineResult> Results { get; init; }

    public string Snapshot { get; init; }

    /// <summary>Generated files by path relative to the output directory (forward slashes), ordinal order.</summary>
    public SortedDictionary<string, string> Files { get; init; }

    /// <summary>Problems the tool refuses to emit (a shift by 24 or more bits, which loses every bit in DM). The tool writes nothing then.</summary>
    public List<string> Errors { get; init; }

    public List<string> Warnings { get; init; }

    public DefineResult Find(string name) => Results.FirstOrDefault(r => r.Emitted && r.Name == name) ?? Results.LastOrDefault(r => r.Name == name);
}

/// <summary>The whole conversion: preprocess, classify, emit C# and the manifests.</summary>
public static class DefinesConverter
{
    public static ConverterOutput Generate(string tgRoot, PreprocessorConfig config = null)
    {
        config ??= PreprocessorConfig.Production;
        var preprocessor = new Preprocessor(tgRoot, config);
        preprocessor.Run();
        return Finish(preprocessor, config, ReadSnapshot(tgRoot));
    }

    /// <summary>Converts DM text as if it were the file <paramref name="file"/> (tests and snippets).</summary>
    public static ConverterOutput ConvertText(string dm, string file = "code/__DEFINES/snippet.dm", PreprocessorConfig config = null)
    {
        config ??= PreprocessorConfig.Production;
        var preprocessor = new Preprocessor(null, config);
        preprocessor.RunText(file, dm);
        return Finish(preprocessor, config, "snippet");
    }

    private static ConverterOutput Finish(Preprocessor preprocessor, PreprocessorConfig config, string snapshot)
    {
        var classifier = new Classifier(preprocessor.Table);
        List<DefineResult> results = classifier.ClassifyAll(preprocessor.Definitions);
        var output = new ConverterOutput
        {
            Preprocessor = preprocessor,
            Config = config,
            Results = results,
            Snapshot = snapshot,
            Files = CSharpEmitter.EmitAll(results, snapshot),
            Errors = classifier.Errors,
            Warnings = classifier.Warnings.Concat(preprocessor.Diagnostics).ToList(),
        };
        output.Files[Manifests.DefinesFile] = Manifests.Defines(output);
        output.Files[Manifests.SignalsFile] = Manifests.Signals(output);
        return output;
    }

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the files that changed and deletes generated files (<c>*.g.cs</c> and the manifests) that are no
    /// longer produced, so the directory holds exactly the output. Returns the number of files written or deleted.
    /// </summary>
    public static int Write(ConverterOutput output, string outDir)
    {
        int changed = 0;
        Directory.CreateDirectory(outDir);
        foreach ((string relative, string content) in output.Files)
        {
            string path = Path.Combine(outDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && File.ReadAllText(path, Utf8) == content)
                continue;
            File.WriteAllText(path, content, Utf8);
            changed++;
        }
        foreach (string stale in StaleFiles(output, outDir))
        {
            File.Delete(Path.Combine(outDir, stale));
            changed++;
        }
        foreach (string dir in Directory.EnumerateDirectories(outDir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        return changed;
    }

    /// <summary>Paths (relative, forward slashes) whose content on disk differs from the output, missing or stale.</summary>
    public static List<string> Diff(ConverterOutput output, string outDir)
    {
        var differences = new List<string>();
        foreach ((string relative, string content) in output.Files)
        {
            string path = Path.Combine(outDir, relative);
            if (!File.Exists(path) || File.ReadAllText(path, Utf8) != content)
                differences.Add(relative);
        }
        differences.AddRange(StaleFiles(output, outDir));
        return differences;
    }

    private static List<string> StaleFiles(ConverterOutput output, string outDir)
    {
        var stale = new List<string>();
        if (!Directory.Exists(outDir))
            return stale;
        foreach (string file in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            bool generated = name.EndsWith(".g.cs", StringComparison.Ordinal) || name is Manifests.DefinesFile or Manifests.SignalsFile;
            string relative = Path.GetRelativePath(outDir, file).Replace(Path.DirectorySeparatorChar, '/');
            if (generated && !output.Files.ContainsKey(relative))
                stale.Add(relative);
        }
        stale.Sort(StringComparer.Ordinal);
        return stale;
    }

    /// <summary>The short commit id of the tg clone's HEAD (read from <c>.git</c>, no git process), or "unknown".</summary>
    public static string ReadSnapshot(string tgRoot)
    {
        try
        {
            string gitDir = Path.Combine(tgRoot, ".git");
            if (File.Exists(gitDir))
            {
                string pointer = File.ReadAllText(gitDir).Trim();
                if (pointer.StartsWith("gitdir:", StringComparison.Ordinal))
                    gitDir = Path.GetFullPath(Path.Combine(tgRoot, pointer.Substring(7).Trim()));
            }
            string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
            string sha = head;
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                string reference = head.Substring(4).Trim();
                string refFile = Path.Combine(gitDir, reference.Replace('/', Path.DirectorySeparatorChar));
                sha = null;
                if (File.Exists(refFile))
                {
                    sha = File.ReadAllText(refFile).Trim();
                }
                else
                {
                    string packed = Path.Combine(gitDir, "packed-refs");
                    if (File.Exists(packed))
                    {
                        sha = File.ReadLines(packed)
                            .Select(l => l.Split(' '))
                            .Where(p => p.Length == 2 && p[1] == reference)
                            .Select(p => p[0])
                            .FirstOrDefault();
                    }
                }
            }
            return sha != null && sha.Length >= 8 ? sha.Substring(0, 8) : "unknown";
        }
        catch (IOException)
        {
            return "unknown";
        }
        catch (UnauthorizedAccessException)
        {
            return "unknown";
        }
    }
}
