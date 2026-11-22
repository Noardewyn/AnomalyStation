using System.IO;
using System.Text.Json;

namespace Anomaly.Tools.Maps;

/// <summary>
/// The DM types and vars a map is validated against (docs/ai/specs/map-format.md 6): which typepaths exist, their parents, and
/// the vars each type declares. Loaded from P1's <c>types-index.tsv</c> (its <c>type</c> rows; <c>var</c> rows when the index
/// has them) and, for the vars, from OpenDream's compiled JSON of tg (D110, a local tool output). An index without vars
/// skips the var check (<see cref="HasVarData"/>); <see cref="Permissive"/> accepts everything (measurements and tests).
/// </summary>
public sealed class TypeIndex
{
    private readonly Dictionary<string, string> _parent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _vars = new(StringComparer.Ordinal);
    private bool _permissive;

    /// <summary>An index that accepts every type and var.</summary>
    public static TypeIndex Permissive() => new TypeIndex { _permissive = true };

    /// <summary>Whether the index knows any var (otherwise the var check is skipped).</summary>
    public bool HasVarData => _vars.Count > 0;

    /// <summary>Whether everything is accepted.</summary>
    public bool IsPermissive => _permissive;

    /// <summary>The number of types.</summary>
    public int TypeCount => _parent.Count;

    /// <summary>Adds a type (its parent path or null) and the vars it declares.</summary>
    public void Add(string path, string parent, IEnumerable<string> vars = null)
    {
        _parent[path] = parent;
        if (vars == null) return;
        if (!_vars.TryGetValue(path, out HashSet<string> set)) _vars[path] = set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string v in vars) set.Add(v);
    }

    /// <summary>Whether <paramref name="path"/> is a type.</summary>
    public bool HasType(string path) => _permissive || _parent.ContainsKey(path);

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or one of its subtypes.</summary>
    public bool IsA(string path, string root)
        => path == root || path.StartsWith(root + "/", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="var"/> is declared at <paramref name="path"/> or an ancestor (true without var data).</summary>
    public bool HasVar(string path, string var)
    {
        if (_permissive || !HasVarData) return true;
        for (string at = path; at != null; at = _parent.TryGetValue(at, out string up) ? up : null)
        {
            if (_vars.TryGetValue(at, out HashSet<string> set) && set.Contains(var)) return true;
        }
        return false;
    }

    /// <summary>
    /// Reads P1's <c>types-index.tsv</c> (types-converter.md 3.9): the <c>type</c> rows (path, parent, ...) and, when present,
    /// <c>var</c> rows (owner, name).
    /// </summary>
    public static TypeIndex LoadTsv(string path)
    {
        var index = new TypeIndex();
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] cols = line.Split('\t');
            if (cols[0] == "type" && cols.Length >= 3)
                index.Add(cols[1], cols[2] is "" or "-" ? null : cols[2]);
            else if (cols[0] == "var" && cols.Length >= 3)
                index.Add(cols[1], index._parent.TryGetValue(cols[1], out string parent) ? parent : null, new[] { cols[2] });
        }
        return index;
    }

    /// <summary>Adds the types and vars of OpenDream's compiled JSON of tg (<c>Types[]</c>: <c>Path</c>, <c>Parent</c>, <c>Variables</c>, <c>TmpVariables</c>, <c>ConstVariables</c>).</summary>
    public void AddOpenDreamJson(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 256 });
        JsonElement types = doc.RootElement.GetProperty("Types");
        var paths = new List<string>();
        foreach (JsonElement type in types.EnumerateArray()) paths.Add(type.GetProperty("Path").GetString());
        int i = 0;
        foreach (JsonElement type in types.EnumerateArray())
        {
            string typePath = paths[i++];
            string parent = type.TryGetProperty("Parent", out JsonElement p) && p.ValueKind == JsonValueKind.Number ? paths[p.GetInt32()] : null;
            var vars = new List<string>();
            if (type.TryGetProperty("Variables", out JsonElement v) && v.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in v.EnumerateObject()) vars.Add(prop.Name);
            }
            foreach (string listName in new[] { "TmpVariables", "ConstVariables" })
            {
                if (!type.TryGetProperty(listName, out JsonElement list)) continue;
                if (list.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement name in list.EnumerateArray())
                        if (name.ValueKind == JsonValueKind.String) vars.Add(name.GetString());
                }
                else if (list.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty prop in list.EnumerateObject()) vars.Add(prop.Name);
                }
            }
            if (typePath == "/") continue;
            Add(typePath, parent == "/" ? null : parent, vars);
        }
    }
}
