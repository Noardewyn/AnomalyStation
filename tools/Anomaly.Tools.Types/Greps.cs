using System.Text.RegularExpressions;

namespace Anomaly.Tools.Types;

/// <summary>
/// The two name greps of translation-rules.md 3.2, run once over <c>../tgstation/code/**/*.dm</c> and kept as name sets
/// (types-converter.md 4.2): the null-use grep (V4: a numeric var whose name tg tests against or assigns null is
/// <c>float?</c>) and the typepath-assignment grep (V3: a var whose name tg assigns a typepath literal is <c>Type</c>).
/// Each capture form below matches exactly the lines the rules' grep matches for that name.
/// </summary>
internal sealed class Greps
{
    public readonly HashSet<string> NullUsed = new(StringComparer.Ordinal);
    public readonly HashSet<string> TypepathAssigned = new(StringComparer.Ordinal);

    /// <summary>
    /// The kind tests of each file (types-converter.md 3.5, "widening"): "file|name" -> the value kinds tg's code in that
    /// file tests a bare name (or <c>src.name</c>) for, with <c>istext</c> (<c>str</c>), <c>isnum</c> (<c>num</c>),
    /// <c>ispath</c> (<c>type</c>) and <c>islist</c> (<c>list</c>).
    /// </summary>
    public readonly SortedDictionary<string, SortedSet<string>> KindTests = new(StringComparer.Ordinal);

    // (istext|isnum|ispath|islist)\(\s*(src\.)?NAME\s*\)
    private static readonly Regex KindTest = new(@"\b(istext|isnum|ispath|islist)\(\s*(?:src\.)?([A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.Compiled);

    // isnull(([A-Za-z_][A-Za-z0-9_.?:\[\]]*\.)?NAME)
    private static readonly Regex IsNull = new(@"isnull\((?:[A-Za-z_][A-Za-z0-9_.?:\[\]]*\.)?([A-Za-z_][A-Za-z0-9_]*)\)", RegexOptions.Compiled);
    // (^|[^A-Za-z0-9_])NAME\s*(==|!=)\s*null\b
    private static readonly Regex EqNull = new(@"(?:^|[^A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*)\s*(?:==|!=)\s*null\b", RegexOptions.Compiled);
    // \bnull\s*(==|!=)\s*([A-Za-z0-9_?.]*\.)?NAME\b: NAME is any segment of the dotted chain
    private static readonly Regex NullEq = new(@"\bnull\s*(?:==|!=)\s*([A-Za-z0-9_?.]+)", RegexOptions.Compiled);
    // (^|[^A-Za-z0-9_])NAME\s*=\s*null\b
    private static readonly Regex SetNull = new(@"(?:^|[^A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*)\s*=\s*null\b", RegexOptions.Compiled);
    // \bNAME\s*=\s*/[a-z]
    private static readonly Regex SetPath = new(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*=\s*/[a-z]", RegexOptions.Compiled);

    public static Greps Run(string tg)
    {
        var g = new Greps();
        string code = Path.Combine(tg, "code");
        foreach (string file in Directory.EnumerateFiles(code, "*.dm", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string rel = Path.GetRelativePath(tg, file).Replace('\\', '/');
            foreach (string line in File.ReadLines(file))
            {
                if (line.Contains("is", StringComparison.Ordinal))
                    foreach (Match m in KindTest.Matches(line))
                    {
                        string key = rel + "|" + m.Groups[2].Value;
                        if (!g.KindTests.TryGetValue(key, out var kinds)) g.KindTests[key] = kinds = new SortedSet<string>(StringComparer.Ordinal);
                        kinds.Add(m.Groups[1].Value switch { "istext" => "str", "isnum" => "num", "ispath" => "type", _ => "list" });
                    }
                if (line.Contains("null", StringComparison.Ordinal))
                {
                    foreach (Match m in IsNull.Matches(line)) g.NullUsed.Add(m.Groups[1].Value);
                    foreach (Match m in EqNull.Matches(line)) g.NullUsed.Add(m.Groups[1].Value);
                    foreach (Match m in SetNull.Matches(line)) g.NullUsed.Add(m.Groups[1].Value);
                    foreach (Match m in NullEq.Matches(line))
                    {
                        foreach (string seg in m.Groups[1].Value.Split('.'))
                        {
                            string s = seg.TrimEnd('?');
                            if (s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_'))
                                g.NullUsed.Add(s);
                        }
                    }
                }
                if (line.Contains('/'))
                {
                    foreach (Match m in SetPath.Matches(line))
                    {
                        string name = m.Groups[1].Value;
                        // grep -vE "\bNAME\s*=\s*(/[A-Za-z_0-9]+)*/(proc|verb)/": a proc or verb path is not a typepath
                        var exclude = new Regex(@"\b" + Regex.Escape(name) + @"\s*=\s*(/[A-Za-z_0-9]+)*/(proc|verb)/");
                        // grep -vE "\bNAME\s*=\s*(/[A-Za-z_0-9]+)+::": a compile-time var read (/x::y, rules 10.3) is
                        // the var's value, not a typepath (types-converter.md 3.5)
                        var read = new Regex(@"\b" + Regex.Escape(name) + @"\s*=\s*(/[A-Za-z_0-9]+)+::");
                        if (!exclude.IsMatch(line) && !read.IsMatch(line)) g.TypepathAssigned.Add(name);
                    }
                }
            }
        }
        return g;
    }
}
