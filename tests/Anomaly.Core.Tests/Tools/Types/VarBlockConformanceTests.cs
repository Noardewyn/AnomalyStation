using System.IO;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The var-block conformance test (D111, rules 10.2, docs/ai/specs/types-converter.md 3.8): every default a hand-written
/// var block sets equals OpenDream's folded value, except the known mismatches the lead decides one by one. A fixed
/// mismatch, or a new one, fails until the types converter is rerun (it rewrites the mismatch list).
/// </summary>
public class VarBlockConformanceTests
{
    private static string Read(string rel) => File.ReadAllText(Path.Combine(TgClone.RepositoryRoot, rel));

    [Fact]
    public void EveryHandWrittenDefaultEqualsOpenDreamsValueOrIsAKnownMismatch()
    {
        List<VarBlockRow> rows = VarBlockConformance.Read(Read(VarBlockConformance.DefaultsFile));
        Dictionary<string, string> known = VarBlockConformance.ReadMismatches(Read(VarBlockConformance.MismatchesFile));
        Assert.True(rows.Count > 3000, $"only {rows.Count} hand-written defaults");
        var unexpected = new List<string>();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (VarBlockRow row in rows)
        {
            string actual = VarBlockConformance.Check(row);
            if (actual == null) continue;
            found.Add(row.Key);
            if (!known.TryGetValue(row.Key, out string listed)) unexpected.Add($"{row.Path}.{row.Var} ({row.Where}, {row.Location}): expected {row.Expected}, actual {actual}");
            else if (listed != actual) unexpected.Add($"{row.Path}.{row.Var}: the actual value changed from {listed} to {actual}");
        }
        var fixedOnes = known.Keys.Where(k => !found.Contains(k)).ToList();
        Assert.True(unexpected.Count == 0, "new var-block mismatches (fix the hand-written default or rerun the tool): " + string.Join("; ", unexpected.Take(20)));
        Assert.True(fixedOnes.Count == 0, "listed mismatches that are fixed (rerun tools/Anomaly.Tools.Types): " + string.Join("; ", fixedOnes.Take(20)));
    }

    [Fact]
    public void TheCheckComparesDmValuesNotClrValues()
    {
        // A boxed int and a float of one value are one DM number; a resource is not its path's text.
        Assert.Null(VarBlockConformance.Check(new VarBlockRow("/turf/closed", "density", "override", "-", "{\"K\":\"num\",\"N\":1}")));
        Assert.NotNull(VarBlockConformance.Check(new VarBlockRow("/turf/closed", "density", "override", "-", "{\"K\":\"num\",\"N\":0}")));
        Assert.Equal("no class", VarBlockConformance.Check(new VarBlockRow("/no/such/type", "x", "decl", "-", "{\"K\":\"null\"}")));
    }
}
