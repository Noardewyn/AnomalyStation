using System.Text;

namespace Anomaly.Tools.Types;

/// <summary>Every file the tool owns, by repository-relative path, and the review report (types-converter.md 1).</summary>
internal sealed class Outputs
{
    public readonly SortedDictionary<string, string> Files = new(StringComparer.Ordinal);

    public List<string> Report = new();

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Directories whose every file the tool owns: a file there that it did not produce is deleted.</summary>
    private static readonly string[] OwnedDirectories = { Generator.TypesDir, Generator.LivePartialDir };

    private IEnumerable<string> Stale(string repo)
    {
        foreach (string dir in OwnedDirectories)
        {
            string full = Path.Combine(repo, dir);
            if (!Directory.Exists(full)) continue;
            foreach (string f in Directory.EnumerateFiles(full, "*.g.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(repo, f).Replace('\\', '/');
                if (!Files.ContainsKey(rel)) yield return rel;
            }
        }
    }

    public int Write(string repo, TextWriter log)
    {
        int changed = 0;
        foreach (var (rel, text) in Files)
        {
            string full = Path.Combine(repo, rel);
            byte[] bytes = Utf8.GetBytes(text);
            if (File.Exists(full) && File.ReadAllBytes(full).AsSpan().SequenceEqual(bytes)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
            changed++;
        }
        int deleted = 0;
        foreach (string rel in Stale(repo).ToList())
        {
            File.Delete(Path.Combine(repo, rel));
            deleted++;
        }
        log.WriteLine($"wrote {changed} changed files, deleted {deleted} stale files, {Files.Count - changed} unchanged");
        return 0;
    }

    public int Check(string repo, TextWriter log)
    {
        var diffs = new List<string>();
        foreach (var (rel, text) in Files)
        {
            string full = Path.Combine(repo, rel);
            if (!File.Exists(full)) { diffs.Add("missing: " + rel); continue; }
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(Utf8.GetBytes(text))) diffs.Add("differs: " + rel);
        }
        foreach (string rel in Stale(repo)) diffs.Add("stale: " + rel);
        foreach (string d in diffs.Take(50)) log.WriteLine(d);
        log.WriteLine(diffs.Count == 0 ? $"check: all {Files.Count} files equal" : $"check: {diffs.Count} differences");
        return diffs.Count == 0 ? 0 : 1;
    }
}
