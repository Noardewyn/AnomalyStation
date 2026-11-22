using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anomaly.Tools.Types;

// The tg type tree as the converter sees it (docs/ai/specs/types-converter.md, section 2): what OpenDream's front end
// gives, in plain classes that do not depend on OpenDream, so that the generator and the tests can run from the cached
// model (TgModel.Load) without recompiling tg.

/// <summary>A DM value as OpenDream folds it (types-converter.md 2.2).</summary>
public sealed class DmVal
{
    /// <summary>
    /// The kind: <c>null</c>, <c>num</c>, <c>str</c>, <c>res</c> (resource literal), <c>type</c> (typepath),
    /// <c>proc</c> (proc path), <c>list</c>, <c>alist</c>, <c>pinf</c>, <c>ninf</c> (the infinities), and the non-constant
    /// kinds <c>new</c> (<c>new /x(args)</c>), <c>dimlist</c> (<c>var/L[a][b]</c>), <c>call</c> (a builtin call:
    /// <c>matrix()</c>, <c>icon(...)</c>, ...) and <c>code</c> (anything else).
    /// </summary>
    public string K { get; set; }

    /// <summary>The number of a <c>num</c>.</summary>
    public float N { get; set; }

    /// <summary>The text of a <c>str</c>; the path of a <c>res</c>, <c>type</c>, <c>proc</c> or <c>new</c>; the callee of a <c>call</c>; the expression class of a <c>code</c>.</summary>
    public string S { get; set; }

    /// <summary>The items of a list (with keys for associations), the arguments of a <c>new</c> or <c>call</c>, the sizes of a <c>dimlist</c>.</summary>
    public List<DmItem> L { get; set; }

    [JsonIgnore] public bool IsConstant => K switch
    {
        "new" or "dimlist" or "call" or "code" => false,
        "list" or "alist" => L == null || L.All(i => (i.Key == null || i.Key.IsConstant) && i.Value.IsConstant),
        _ => true,
    };

    public static DmVal Null() => new() { K = "null" };

    public override string ToString() => K switch
    {
        "null" => "null",
        "num" => N.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        "str" => JsonSerializer.Serialize(S),
        "res" => "'" + S + "'",
        "type" or "proc" => S,
        "pinf" => "INFINITY",
        "ninf" => "-INFINITY",
        "list" or "alist" => K + "(" + string.Join(", ", (L ?? new()).Select(i => i.ToString())) + ")",
        "new" => "new " + S + "(" + string.Join(", ", (L ?? new()).Select(i => i.ToString())) + ")",
        "dimlist" => "[" + string.Join("][", (L ?? new()).Select(i => i.ToString())) + "]",
        "call" => S + "(" + string.Join(", ", (L ?? new()).Select(i => i.ToString())) + ")",
        _ => "<" + S + ">",
    };
}

/// <summary>A list item (an association has a key), or an argument (a named one has a name).</summary>
public sealed class DmItem
{
    public DmVal Key { get; set; }
    public DmVal Value { get; set; }
    public string Name { get; set; }

    public override string ToString()
        => Name != null ? Name + " = " + Value : Key != null ? Key + " = " + Value : Value?.ToString();
}

/// <summary>A var declared at a type (<c>var/x = 1</c>), not counting BYOND's own declarations (DMStandard).</summary>
public sealed class VarDecl
{
    public string Name { get; set; }
    /// <summary>The declared type path (<c>var/obj/item/x</c>: <c>/obj/item</c>; <c>var/list/x</c>: <c>/list</c>), or null.</summary>
    public string DeclType { get; set; }
    /// <summary>The folded default (null for a runtime-initialized var: its value is in <see cref="TypeInfo.Inits"/>).</summary>
    public DmVal Value { get; set; }
    public bool Tmp { get; set; }
    public bool Const { get; set; }
    public bool Final { get; set; }
    public string File { get; set; }
    public int Line { get; set; }
}

/// <summary>A var override at a type (<c>x = 2</c>), or an assignment of OpenDream's per-type init proc.</summary>
public sealed class VarSet
{
    public string Name { get; set; }
    public DmVal Value { get; set; }
    public string File { get; set; }
    public int Line { get; set; }
}

/// <summary>A proc or verb definition at a type (or a global proc at <c>/</c>), as tg's code defines it.</summary>
public sealed class ProcDef
{
    public string Name { get; set; }
    public bool Verb { get; set; }
    /// <summary>Whether an ancestor already has the proc (an override); false for the root definition of the slot.</summary>
    public bool Override { get; set; }
    public string File { get; set; }
    public int Line { get; set; }
    public List<string> Args { get; set; } = new();
}

/// <summary>One DM type.</summary>
public sealed class TypeInfo
{
    public string Path { get; set; }
    public string Parent { get; set; }
    /// <summary>OpenDream's type id (compile order); the tree order sorts children by it.</summary>
    public int Id { get; set; }
    /// <summary>The first place in tg's code (include order) that declares the type or something on it; null when only a subtype's path implies it.</summary>
    public string File { get; set; }
    public int Line { get; set; }
    /// <summary>BYOND declares the type (DMStandard).</summary>
    public bool Builtin { get; set; }
    /// <summary>The vars BYOND declares at this type (DMStandard): the engine parts declare them (rules 3.2).</summary>
    public List<string> BuiltinVars { get; set; } = new();
    public List<VarDecl> Vars { get; set; } = new();
    /// <summary>Overrides at this type in tg's declaration order, one per var (the last value wins, as in DM).</summary>
    public List<VarSet> Overrides { get; set; } = new();
    /// <summary>OpenDream's init-proc assignments (rules 10.2: object-creating and non-constant defaults), in declaration order.</summary>
    public List<VarSet> Inits { get; set; } = new();
    public List<ProcDef> Procs { get; set; } = new();
}

/// <summary>The whole tree, in OpenDream's id order, plus where it came from.</summary>
public sealed class TgModel
{
    public const int FormatVersion = 3;

    public int Format { get; set; } = FormatVersion;
    public string TgCommit { get; set; }
    public string OpenDreamCommit { get; set; }
    public string RobustToolboxCommit { get; set; }
    public string Defines { get; set; }
    public double CompileSeconds { get; set; }
    public List<TypeInfo> Types { get; set; } = new();
    /// <summary>Global procs (<c>/proc/x</c>) defined in tg.</summary>
    public List<ProcDef> GlobalProcs { get; set; } = new();
    /// <summary>OpenDream's warnings and errors for the compile (a count per code).</summary>
    public Dictionary<string, int> Diagnostics { get; set; } = new();

    // Read from the tg clone on every run (not cached): the name greps of rules 3.2, MetaStation's types and the station maps' var edits.
    [JsonIgnore] public List<string> NullUsed { get; set; }
    [JsonIgnore] public List<string> TypepathAssigned { get; set; }
    [JsonIgnore] public List<string> MetaTypes { get; set; }
    /// <summary>The station maps' var edits (<see cref="MapEdits"/>), for the var typing.</summary>
    [JsonIgnore] public List<MapEdit> MapEdits { get; set; }

    /// <summary>The icon files that are not one tile, by resource path (<see cref="Types.IconSizes"/>).</summary>
    [JsonIgnore] public SortedDictionary<string, (int Width, int Height)> IconSizes { get; set; }
    /// <summary>The kind tests of tg's files (<see cref="Greps.KindTests"/>), for the var typing.</summary>
    [JsonIgnore] public SortedDictionary<string, SortedSet<string>> KindTests { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        using FileStream s = File.Create(path);
        JsonSerializer.Serialize(s, this, Options);
    }

    public static TgModel Load(string path)
    {
        using FileStream s = File.OpenRead(path);
        return JsonSerializer.Deserialize<TgModel>(s, Options);
    }
}
