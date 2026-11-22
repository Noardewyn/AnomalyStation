using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    public const string ConstantsPath = TypesDir + "/TypeConstants.g.cs";

    // (node, var) -> what the generated part gives the var at that type: a field's initializer (null: the C# default) or
    // a constructor assignment (types-converter.md 9).
    private readonly Dictionary<(Node Node, string Var), (DmVal Value, CsType Type)> _emitted = new();

    private void Record(Node n, string var, DmVal value, CsType type) => _emitted[(n, var)] = (value, type);

    /// <summary>A node whose class has no hand-written var block: its compile-time values are exactly what the generator emitted.</summary>
    private static bool Eligible(Node n) => n.Mode is Mode.Generated or Mode.HandProcs;

    /// <summary>
    /// TypeConstants.g.cs (types-converter.md 9): tg's compile-time values of the vars that translated code reads with
    /// <c>T::var</c> / <c>initial(T.var)</c>, per type, for the classes whose var block is generated; the shim's
    /// <c>Dm.initial</c> reads them for a <c>Type</c> instead of building the prototype.
    /// </summary>
    private void EmitConstants(Outputs outputs)
    {
        var index = new Dictionary<Node, int>();
        for (int i = 0; i < _preorder.Count; i++) index[_preorder[i]] = i;
        var vars = _hand.InitialReads.ToList();
        var body = new StringBuilder();
        int entries = 0;
        foreach (string var in vars)
        {
            var rows = new List<string>();
            foreach (Node n in _preorder.Where(Eligible))
            {
                if (!_emitted.TryGetValue((n, var), out var e)) continue;
                string encoded = EncodeConstant(e.Value, e.Type);
                if (encoded == null) continue;
                rows.Add(index[n].ToString(CultureInfo.InvariantCulture) + "\t" + encoded);
            }
            if (rows.Count == 0) continue;
            body.Append('#').Append(var).Append('\n');
            foreach (string r in rows) body.Append(r).Append('\n');
            entries += rows.Count;
        }
        var stops = _preorder.Where(n => !Eligible(n)).Select(n => index[n].ToString(CultureInfo.InvariantCulture));
        body.Append("!").Append(string.Join(",", stops)).Append('\n');
        Report.Add($"constants: {entries} compile-time values of {vars.Count} vars read with initial() ({body.Length / 1024} KB)");

        var sb = new StringBuilder();
        sb.Append(Header("tg's compile-time var values for T::var reads (types-converter.md 9)"));
        sb.Append(@"using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

public static partial class TypeOrder
{
    // Types-converter.md 9: BYOND's T::var is a compile-time read. For the vars that translated code reads with
    // Dm.initial(T, (C p) => p.var), this table holds what the generated part of each class gives the var (a field's
    // initializer or a constructor assignment), for the classes whose var block is all generated; a class with a
    // hand-written var block, and the shim's types, are stops: there the prototype answers. Decoded once, immutable.
    private static readonly Lazy<ConstantTable> s_constants = new(() => new ConstantTable(EncodedConstants()));

    /// <summary>
    /// The compile-time value of <paramref name=""var""/> for the type <paramref name=""path""/>: 1 and the value when the table
    /// has it; 0 and the path of the nearest class with a hand-written var block (or a shim type), whose prototype
    /// answers; -1 when the table does not cover the var or the path. The shim's <c>Dm.initial</c> calls it.
    /// </summary>
    internal static int Constant(string path, string var, out object value)
    {
        ConstantTable table = s_constants.Value;
        value = null;
        if (var == null || path == null || !table.Vars.TryGetValue(var, out FrozenDictionary<int, string> values)) return -1;
        if (!table.Index.TryGetValue(path, out int i)) return -1;
        for (int depth = 0; i >= 0 && depth < 64; i = Parents[i], depth++)
        {
            if (table.Stops.Contains(i))
            {
                value = Paths[i];
                return 0;
            }
            if (values.TryGetValue(i, out string encoded))
            {
                value = DecodeConstant(encoded);
                return 1;
            }
        }
        return -1;
    }

    /// <summary>
    /// The types that may hold their own path in <paramref name=""var""/> (tg's abstract_type test), in tree order: every
    /// type whose generated part sets the var to its own path or to a subtype's, and every stop class. The caller makes
    /// tg's own test on each (types-converter.md 9).
    /// </summary>
    internal static IEnumerable<string> SelfReferenceCandidates(string var)
    {
        ConstantTable table = s_constants.Value;
        var found = new SortedSet<int>(table.Stops);
        if (table.Vars.TryGetValue(var, out FrozenDictionary<int, string> values))
        {
            foreach (KeyValuePair<int, string> pair in values)
            {
                if (pair.Value.Length < 2 || pair.Value[0] != 'y') continue;
                if (table.Index.TryGetValue(pair.Value.Substring(1), out int target)) found.Add(target);
            }
        }
        foreach (int i in found) yield return Paths[i];
    }

    private static object DecodeConstant(string encoded)
    {
        string rest = encoded.Substring(1);
        switch (encoded[0])
        {
            case 'f': return float.Parse(rest, CultureInfo.InvariantCulture);
            case 'i': return int.Parse(rest, CultureInfo.InvariantCulture);
            case 'p': return float.PositiveInfinity;
            case 'm': return float.NegativeInfinity;
            case 's': return Unescape(rest);
            case 't': return Dm.text(Unescape(rest));
            case 'r': return Dm.resource(Unescape(rest));
            case 'y': return DmTypes.FromPath(rest);
            default: return null;
        }
    }

    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
            char e = s[++i];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'u': sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        return sb.ToString();
    }

    private sealed class ConstantTable
    {
        internal readonly FrozenDictionary<string, int> Index;
        internal readonly FrozenSet<int> Stops;
        internal readonly FrozenDictionary<string, FrozenDictionary<int, string>> Vars;

        internal ConstantTable(string encoded)
        {
            var index = new Dictionary<string, int>(Paths.Length, StringComparer.Ordinal);
            for (int i = 0; i < Paths.Length; i++) index[Paths[i]] = i;
            Index = index.ToFrozenDictionary(StringComparer.Ordinal);
            var vars = new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.Ordinal);
            var stops = new HashSet<int>();
            string current = null;
            var rows = new Dictionary<int, string>();
            foreach (string line in encoded.Split('\n'))
            {
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == '!')
                {
                    if (current != null) vars[current] = rows.ToFrozenDictionary();
                    rows = new Dictionary<int, string>();
                    current = line[0] == '#' ? line.Substring(1) : null;
                    if (line[0] == '!')
                        foreach (string s in line.Substring(1).Split(',', StringSplitOptions.RemoveEmptyEntries)) stops.Add(int.Parse(s, CultureInfo.InvariantCulture));
                    continue;
                }
                int tab = line.IndexOf('\t');
                rows[int.Parse(line.AsSpan(0, tab), CultureInfo.InvariantCulture)] = line.Substring(tab + 1);
            }
            if (current != null) vars[current] = rows.ToFrozenDictionary();
            Vars = vars.ToFrozenDictionary(StringComparer.Ordinal);
            Stops = stops.ToFrozenSet();
        }
    }

    // A method, not a const: a const string would be stored twice in the assembly.
    private static string EncodedConstants() => """"""
");
        sb.Append(body);
        sb.Append("\"\"\";\n}\n");
        outputs.Files[ConstantsPath] = sb.ToString();
    }

    /// <summary>One table value in the C# type of its member, as the generated part writes it; null when it is not a constant.</summary>
    private static string EncodeConstant(DmVal v, CsType t)
    {
        if (v == null) return t.Kind == CsType.K.Float ? "f0" : "n"; // the C# default of a field without initializer
        switch (v.K)
        {
            case "null": return "n";
            case "pinf": return "p";
            case "ninf": return "m";
            case "num":
                bool integral = v.N == MathF.Floor(v.N) && Math.Abs(v.N) < 2147483520f;
                if (t.Kind == CsType.K.Object && integral) return "i" + ((long)v.N).ToString(CultureInfo.InvariantCulture);
                return "f" + v.N.ToString("R", CultureInfo.InvariantCulture);
            case "str":
                return (CSharpValues.HasMacro(v.S) ? "t" + Escape(TemplateOf(v.S)) : "s" + Escape(v.S));
            case "proc": return "s" + Escape(v.S);
            case "res": return "r" + Escape(v.S);
            case "type": return "y" + v.S;
            default: return null; // lists and objects are runtime-initialized: never in a constructor
        }
    }

    // The Dm.text template of a text with OpenDream's macro characters (CSharpValues.Text without the C# literal).
    private static string TemplateOf(string s)
    {
        string literal = CSharpValues.Text(s); // Dm.text("...")
        string inner = literal.Substring("Dm.text(".Length, literal.Length - "Dm.text(".Length - 1);
        return JsonSerializer.Deserialize<string>(inner);
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                case '"': sb.Append("\\u0022"); break;
                default:
                    if (c < 0x20 || (c >= 0xD800 && c <= 0xDFFF)) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
