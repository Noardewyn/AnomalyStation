using System.IO;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>The checked-in types-index.tsv (docs/ai/specs/types-converter.md 3.9), parsed once per test run.</summary>
internal sealed class TypesIndex
{
    public sealed record TypeRow(string Path, string Parent, string Location, string State, string Class, bool Hand, bool Meta);

    public sealed record ProcRow(string Owner, string Name, string Location, string Args, string Translated, string Body, string Kind);

    public readonly List<TypeRow> Types = new();
    public readonly Dictionary<string, TypeRow> ByPath = new(StringComparer.Ordinal);
    public readonly List<ProcRow> Procs = new();
    public readonly Dictionary<string, List<ProcRow>> ProcsOf = new(StringComparer.Ordinal);

    private static readonly Lazy<TypesIndex> LazyIndex = new(() => Read(Path.Combine(TgClone.RepositoryRoot, "docs", "ai", "specs", "generated", "types-index.tsv")));

    public static TypesIndex Index => LazyIndex.Value;

    public static TypesIndex Read(string file)
    {
        var index = new TypesIndex();
        foreach (string line in File.ReadLines(file))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split('\t');
            if (f[0] == "type")
            {
                var row = new TypeRow(f[1], f[2], f[3], f[4], f[5], f[6] == "1", f[7] == "1");
                index.Types.Add(row);
                index.ByPath[row.Path] = row;
            }
            else
            {
                var row = new ProcRow(f[1], f[2], f[3], f[4], f[5], f[6], f[7]);
                index.Procs.Add(row);
                if (!index.ProcsOf.TryGetValue(row.Owner, out var list)) index.ProcsOf[row.Owner] = list = new();
                list.Add(row);
            }
        }
        return index;
    }

    public IReadOnlyList<ProcRow> ProcsAt(string path) => ProcsOf.TryGetValue(path, out var list) ? list : Array.Empty<ProcRow>();
}

/// <summary>The OpenDream clone the types converter compiles (types-converter.md 1): $ANOMALY_OPENDREAM, or OpenDream/ next to the repository.</summary>
internal static class OpenDreamClone
{
    public static readonly string Root = Locate();

    private static string Locate()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_OPENDREAM");
        if (!string.IsNullOrEmpty(env))
            return File.Exists(Path.Combine(env, "DMCompiler", "DMCompiler.csproj")) ? Path.GetFullPath(env) : null;
        for (DirectoryInfo d = new DirectoryInfo(TgClone.RepositoryRoot); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "OpenDream");
            if (File.Exists(Path.Combine(candidate, "DMCompiler", "DMCompiler.csproj"))) return candidate;
        }
        return null;
    }
}

/// <summary>A fact that needs both clones (tg and OpenDream); skipped without them.</summary>
public sealed class TypesToolFactAttribute : FactAttribute
{
    public TypesToolFactAttribute()
    {
        if (TgClone.Root == null || OpenDreamClone.Root == null)
            Skip = "the tgstation or OpenDream clone was not found (ANOMALY_TGSTATION, ANOMALY_OPENDREAM, or next to the repository)";
    }
}
