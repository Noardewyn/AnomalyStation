using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Types;

/// <summary>
/// Writes a folded DM value as a C# expression of a given member type (types-converter.md 4.3), or says why it cannot.
/// Constants only, plus the initializer forms of 4.4 (lists, <c>new</c>, dimensional lists, a few builtin calls).
/// </summary>
internal sealed class CSharpValues
{
    private readonly Func<string, Node> _node;

    public CSharpValues(Func<string, Node> node) => _node = node;

    /// <summary>The C# expression of <paramref name="v"/> stored in a member of type <paramref name="target"/>, or null and a reason.</summary>
    public string Emit(DmVal v, CsType target, out string why)
    {
        why = null;
        switch (v.K)
        {
            case "null":
                if (target.Kind == CsType.K.Float) { why = "null into a float var"; return null; }
                return "null";
            case "num":
            case "pinf":
            case "ninf":
                if (target.Kind is CsType.K.Float or CsType.K.NullableFloat or CsType.K.Object) return Number(v, target.Kind == CsType.K.Object);
                why = $"a number into a {target.CSharp} var";
                return null;
            case "str":
                if (target.Kind is CsType.K.String or CsType.K.Object) return Text(v.S);
                why = $"text into a {target.CSharp} var";
                return null;
            case "proc":
                // Proc references are text (rules 3.2, 7.7).
                if (target.Kind is CsType.K.String or CsType.K.Object) return Literal(v.S);
                why = $"a proc reference into a {target.CSharp} var";
                return null;
            case "res":
                if (target.Kind == CsType.K.Object) return "Dm.resource(" + Literal(v.S) + ")";
                why = $"a resource into a {target.CSharp} var";
                return null;
            case "type":
            {
                if (target.Kind is not (CsType.K.Type or CsType.K.Object)) { why = $"a typepath into a {target.CSharp} var"; return null; }
                string t = TypeOf(v.S, out why);
                return t;
            }
            case "list":
            case "alist":
            {
                if (target.Kind is not (CsType.K.List or CsType.K.Object)) { why = $"a list into a {target.CSharp} var"; return null; }
                return ListOf(v, out why);
            }
            case "dimlist":
            {
                if (target.Kind is not (CsType.K.List or CsType.K.Object)) { why = $"a list into a {target.CSharp} var"; return null; }
                var sizes = new List<string>();
                foreach (DmItem s in v.L ?? new())
                {
                    string e = Emit(s.Value, CsType.Object, out why);
                    if (e == null) return null;
                    sizes.Add(e);
                }
                return "Dm.@new<DmList>(" + string.Join(", ", sizes) + ")";
            }
            case "new":
                return New(v, target, out why);
            case "call":
                return Call(v, target, out why);
            default:
                why = $"a non-constant initializer ({v.S})";
                return null;
        }
    }

    private string New(DmVal v, CsType target, out string why)
    {
        why = null;
        if (v.S == null) { why = "new of a computed type"; return null; }
        var args = new List<string>();
        foreach (DmItem a in v.L ?? new())
        {
            if (a.Name != null) { why = "new with a named argument"; return null; }
            string e = Emit(a.Value, CsType.Object, out why);
            if (e == null) return null;
            args.Add(e);
        }
        if (v.S == "/list")
        {
            if (target.Kind is not (CsType.K.List or CsType.K.Object)) { why = $"a list into a {target.CSharp} var"; return null; }
            return args.Count switch
            {
                0 => "Dm.list()",
                1 => "Dm.list_n(" + args[0] + ")",
                _ => "Dm.@new<DmList>(" + string.Join(", ", args) + ")",
            };
        }
        Node n = _node(v.S);
        if (n == null || n.ClassName == null || (!n.HasClass && n.Existing == null)) { why = $"new {v.S}: no class"; return null; }
        if (target.Kind == CsType.K.Class && !Assignable(n, target)) { why = $"new {v.S} into a {target.CSharp} var"; return null; }
        if (target.Kind is not (CsType.K.Class or CsType.K.Object)) { why = $"new {v.S} into a {target.CSharp} var"; return null; }
        return "Dm.@new<" + n.ClassName + ">(" + string.Join(", ", args) + ")";
    }

    private string Call(DmVal v, CsType target, out string why)
    {
        why = null;
        string name = v.S?.TrimEnd('(', ')');
        // The builtins that BYOND evaluates at compile time for an initial value (OpenDream's IsValidRightHandSide):
        // matrix() and file() have shim forms; generator(), icon(), sound(), filter() and nameof() are not generated.
        string shim = name switch
        {
            "matrix" => "Dm.matrix",
            "file" => "Dm.file",
            _ => null,
        };
        if (shim == null) { why = $"{name}(): not generated"; return null; }
        if (target.Kind is CsType.K.Float or CsType.K.NullableFloat or CsType.K.String or CsType.K.Type or CsType.K.List)
        {
            why = $"{name}() into a {target.CSharp} var";
            return null;
        }
        var args = new List<string>();
        foreach (DmItem a in v.L ?? new())
        {
            if (a.Name != null) { why = $"{name}() with a named argument"; return null; }
            string e = Emit(a.Value, CsType.Object, out why);
            if (e == null) return null;
            args.Add(e);
        }
        return shim + "(" + string.Join(", ", args) + ")";
    }

    private static bool Assignable(Node n, CsType target)
    {
        if (target.ClassNode == null) return false;
        for (Node x = n; x != null; x = x.Parent)
            if (x == target.ClassNode) return true;
        return false;
    }

    private string ListOf(DmVal v, out string why)
    {
        why = null;
        var items = new List<string>();
        foreach (DmItem it in v.L ?? new())
        {
            string value = Emit(it.Value, CsType.Object, out why);
            if (value == null) return null;
            if (it.Key != null)
            {
                string key = Emit(it.Key, CsType.Object, out why);
                if (key == null) return null;
                items.Add("Dm.kv(" + key + ", " + value + ")");
            }
            else items.Add(value);
        }
        return (v.K == "alist" ? "Dm.alist(" : "Dm.list(") + string.Join(", ", items) + ")";
    }

    public string TypeOf(string path, out string why)
    {
        why = null;
        Node n = _node(path);
        if (n != null && n.ClassName != null && (n.HasClass || n.Existing != null)) return "typeof(" + n.ClassName + ")";
        if (path == "/list") return "typeof(DmList)";
        why = $"typepath {path}: no class";
        return null;
    }

    /// <summary>A DM number: an integer literal for an integral value of int range (rules 3.1), else a float literal.</summary>
    public static string Number(DmVal v, bool boxed)
    {
        if (v.K == "pinf") return "float.PositiveInfinity";
        if (v.K == "ninf") return "float.NegativeInfinity";
        float f = v.N;
        if (float.IsNaN(f)) return "float.NaN";
        if (f == MathF.Floor(f) && Math.Abs(f) < 2147483520f)
        {
            if (f == 0 && float.IsNegative(f)) return "-0f";
            return ((long)f).ToString(CultureInfo.InvariantCulture);
        }
        string r = f.ToString("R", CultureInfo.InvariantCulture);
        if (!r.Contains('.') && !r.Contains('E')) r += ".0";
        return r + "f";
    }

    // OpenDream stores BYOND's text macros as 0xFFxx characters (DMCompiler/Bytecode/StringFormatEncoder.cs); the port
    // writes them in DM spelling through Dm.text (rules 5.1, 5.4).
    private static readonly Dictionary<int, string> Macros = new()
    {
        [4] = "The", [5] = "the", [6] = "A", [7] = "a", [8] = "He", [9] = "he", [10] = "His", [11] = "his", [12] = "him",
        [13] = "himself", [15] = "hers", [16] = "proper", [17] = "improper", [18] = "roman", [19] = "Roman", [20] = "th",
        [21] = "s", [22] = "icon",
    };

    public static bool HasMacro(string s) => s.Any(c => c >= 0xFF04 && c <= 0xFF2C);

    /// <summary>A text value: a C# string literal, or <c>Dm.text("\\improper ...")</c> when it holds text macros.</summary>
    public static string Text(string s)
    {
        if (!HasMacro(s)) return Literal(s);
        var t = new StringBuilder();
        foreach (char c in s)
        {
            if (c >= 0xFF04 && c <= 0xFF2C && Macros.TryGetValue(c - 0xFF00, out string m))
            {
                t.Append('\\').Append(m);
                // OpenDream drops the one space that follows \proper and \improper; Dm.text drops it again.
                if (m is "proper" or "improper") t.Append(' ');
                continue;
            }
            if (c is '\\' or '[' or ']') t.Append('\\');
            t.Append(c);
        }
        return "Dm.text(" + Literal(t.ToString()) + ")";
    }

    public static string Literal(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    if (c < 0x20 || c == 0x7F || (c >= 0xD800 && c <= 0xDFFF) || c == 0x2028 || c == 0x2029 || c >= 0xFFF0 || (c >= 0xE000 && c <= 0xF8FF) || (c >= 0xFF00 && c <= 0xFF5E && HasMacro(c.ToString())))
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
