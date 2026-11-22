using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Tools.Types;

// The var-block conformance check (docs/ai/specs/types-converter.md 3.4): every default a hand-written class sets (its
// field initializers, constructor, dm_tg_vars and dm_init_vars) against OpenDream's folded value. Shared source: the
// types converter (tools/Anomaly.Tools.Types) compiles this file too, to write the list of known mismatches the test
// compares with, so the tool and the test judge a value the same way.

/// <summary>One expected default: a var of a hand-written class and OpenDream's value for it.</summary>
public sealed record VarBlockRow(string Path, string Var, string Where, string Location, string Expected)
{
    /// <summary>The key of a row in the mismatch list.</summary>
    public string Key => Path + "\t" + Var + "\t" + Where;
}

/// <summary>Reads the expectations and checks them against the Tg assembly's classes.</summary>
public static class VarBlockConformance
{
    public const string DefaultsFile = "tests/Anomaly.Core.Tests/Tools/Types/handwritten-defaults.tsv";
    public const string MismatchesFile = "tests/Anomaly.Core.Tests/Tools/Types/handwritten-mismatches.tsv";

    public static List<VarBlockRow> Read(string tsv)
    {
        var rows = new List<VarBlockRow>();
        foreach (string line in tsv.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split('\t');
            rows.Add(new VarBlockRow(f[0], f[1], f[2], f[3], f[4]));
        }
        return rows;
    }

    /// <summary>The mismatch list: key to the actual value's description.</summary>
    public static Dictionary<string, string> ReadMismatches(string tsv)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in tsv.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split('\t');
            map[f[0] + "\t" + f[1] + "\t" + f[2]] = f.Length > 5 ? f[5] : "";
        }
        return map;
    }

    /// <summary>Null when the class's value equals the expectation, else a description of the actual value.</summary>
    public static string Check(VarBlockRow row)
    {
        Type cls = DmTypes.FromPath(row.Path);
        if (cls == null) return "no class";
        using JsonDocument doc = JsonDocument.Parse(row.Expected);
        JsonElement expected = doc.RootElement;
        string kind = expected.GetProperty("K").GetString();
        if (kind is "new" or "call" or "code" or "dimlist") return null; // not a value: not compared (types-converter.md 3.4)
        object instance;
        try
        {
            var world = new World();
            using (world.Enter())
            {
                instance = Activator.CreateInstance(cls);
                if (row.Where == "init")
                {
                    MethodInfo init = cls.GetMethod("dm_init_vars", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    init.Invoke(instance, null);
                }
            }
        }
        catch (Exception e)
        {
            Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
            return "error: " + inner.GetType().Name + ": " + OneLine(inner.Message);
        }
        if (!TryRead(instance, row.Var, out object actual)) return "no member";
        return Same(actual, expected) ? null : Describe(actual);
    }

    private static bool TryRead(object instance, string name, out object value)
    {
        for (Type t = instance.GetType(); t != null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo f = t.GetField(name, flags);
            if (f != null) { value = f.GetValue(instance); return true; }
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null && p.GetIndexParameters().Length == 0) { value = p.GetValue(instance); return true; }
        }
        value = null;
        return false;
    }

    private static bool Same(object actual, JsonElement e)
    {
        string k = e.GetProperty("K").GetString();
        switch (k)
        {
            case "null":
                return actual == null;
            case "num":
                return actual != null && Dm.isnum(actual) && Dm.eq(actual, (float)e.GetProperty("N").GetDouble());
            case "pinf":
                return actual != null && Dm.isnum(actual) && float.IsPositiveInfinity(Dm.num(actual));
            case "ninf":
                return actual != null && Dm.isnum(actual) && float.IsNegativeInfinity(Dm.num(actual));
            case "str":
                return actual is string s && string.Equals(s, Text(e.GetProperty("S").GetString()), StringComparison.Ordinal);
            case "proc":
                return actual is string ps && string.Equals(ps, e.GetProperty("S").GetString(), StringComparison.Ordinal);
            case "res":
                return actual is DmResource r && string.Equals(r.Path, e.GetProperty("S").GetString(), StringComparison.Ordinal);
            case "type":
            {
                Type want = DmTypes.FromPath(e.GetProperty("S").GetString());
                return want != null && actual is Type at && at == want;
            }
            case "list":
            case "alist":
            {
                if (actual is not DmList list) return false;
                JsonElement items = e.TryGetProperty("L", out JsonElement l) ? l : default;
                int n = items.ValueKind == JsonValueKind.Array ? items.GetArrayLength() : 0;
                if (list.Count != n) return false;
                int i = 1;
                foreach (JsonElement item in n == 0 ? Enumerable.Empty<JsonElement>() : items.EnumerateArray())
                {
                    object key = list[(float)i];
                    if (item.TryGetProperty("Key", out JsonElement ke))
                    {
                        if (!Same(key, ke)) return false;
                        if (!Same(list[key], item.GetProperty("Value"))) return false;
                    }
                    else if (!Same(key, item.GetProperty("Value"))) return false;
                    i++;
                }
                return true;
            }
            default:
                return true;
        }
    }

    // OpenDream's 0xFFxx text-macro characters in DM spelling, through Dm.text (as the generator writes them).
    private static readonly Dictionary<int, string> Macros = new()
    {
        [4] = "The", [5] = "the", [6] = "A", [7] = "a", [8] = "He", [9] = "he", [10] = "His", [11] = "his", [12] = "him",
        [13] = "himself", [15] = "hers", [16] = "proper", [17] = "improper", [18] = "roman", [19] = "Roman", [20] = "th",
        [21] = "s", [22] = "icon",
    };

    public static string Text(string s)
    {
        if (!s.Any(c => c >= 0xFF04 && c <= 0xFF2C)) return s;
        var t = new StringBuilder();
        foreach (char c in s)
        {
            if (c >= 0xFF04 && c <= 0xFF2C && Macros.TryGetValue(c - 0xFF00, out string m))
            {
                t.Append('\\').Append(m);
                if (m is "proper" or "improper") t.Append(' ');
                continue;
            }
            if (c is '\\' or '[' or ']') t.Append('\\');
            t.Append(c);
        }
        return Dm.text(t.ToString());
    }

    public static string Describe(object v)
    {
        string s = v switch
        {
            null => "null",
            string str => JsonSerializer.Serialize(str),
            Type t => DmTypes.PathOf(t) ?? t.Name,
            DmResource r => "'" + r.Path + "'",
            DmList l => "list(" + l.Count + " items)",
            _ when Dm.isnum(v) => Dm.num(v).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            _ => v.GetType().Name,
        };
        return OneLine(s);
    }

    private static string OneLine(string s)
    {
        s = s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return s.Length > 120 ? s.Substring(0, 117) + "..." : s;
    }
}
