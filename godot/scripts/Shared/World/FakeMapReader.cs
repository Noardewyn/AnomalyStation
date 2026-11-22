using System.Globalization;
using System.Text;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. A small private reader of the map format (docs/ai/specs/map-format.md 4) for FakeMap, which needs only
// the type paths and a few var overrides of each tile. The engine reader (Anomaly.Core.Tg.MapText, M3 P2) is the one the server and
// the converter's tests use; this one reads the same grammar but keeps the override values as raw literals and builds nothing
// else, so FakeMap works before P2's reader lands and without a tg world.

/// <summary>One atom of a tile: a type path and its var overrides as DM literals.</summary>
public sealed class RawMember
{
    public string Path { get; init; }

    public IReadOnlyList<(string Var, string Literal)> Overrides { get; init; }

    /// <summary>The override's literal, or null.</summary>
    public string Literal(string var)
    {
        foreach ((string name, string literal) in Overrides)
        {
            if (name == var)
                return literal;
        }
        return null;
    }

    /// <summary>The override as a number, or null.</summary>
    public float? Number(string var)
    {
        string literal = Literal(var);
        return literal != null && float.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : null;
    }

    /// <summary>The override as text (the quotes removed), or null.</summary>
    public string Text(string var)
    {
        string literal = Literal(var);
        return literal != null && literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"' ? literal.Substring(1, literal.Length - 2) : null;
    }
}

/// <summary>A tile's member list (a model: tiles with equal text share one).</summary>
public sealed class RawModel
{
    public string Text { get; init; }

    /// <summary>The members in the file's order: movables, then the turf, then the area last.</summary>
    public IReadOnlyList<RawMember> Members { get; init; }
}

/// <summary>A parsed file: the header and the tiles that have a line.</summary>
public sealed class RawMap
{
    public int Width { get; init; }

    public int Height { get; init; }

    public int Levels { get; init; }

    public RawModel Default { get; init; }

    /// <summary>The tiles that have a line, in the file's order.</summary>
    public IReadOnlyList<(int X, int Y, int Z, RawModel Model)> Lines { get; init; }

    /// <summary>Members whose override list was cut off (the scout's probe output has some): the path was kept, the overrides dropped.</summary>
    public IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>Thrown for a file this reader cannot read.</summary>
public sealed class FakeMapFormatException : Exception
{
    public FakeMapFormatException(string source, int line, string message) : base(source + ":" + line + ": " + message)
    {
    }
}

/// <summary>The reader.</summary>
public static class FakeMapReader
{
    public static RawMap Parse(string text, string sourceName)
    {
        var warnings = new List<string>();
        var models = new Dictionary<string, RawModel>(StringComparer.Ordinal);
        RawModel Model(string memberText, int line)
        {
            if (!models.TryGetValue(memberText, out RawModel model))
            {
                model = new RawModel { Text = memberText, Members = ParseMembers(memberText, sourceName, line, warnings) };
                models[memberText] = model;
            }
            return model;
        }

        int width = 0, height = 0, levels = 0;
        RawModel defaults = null;
        int z = 1;
        bool sawMagic = false;
        var lines = new List<(int X, int Y, int Z, RawModel Model)>(20000);
        int lineNo = 0;
        foreach (string raw in text.Split('\n'))
        {
            lineNo++;
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (!sawMagic)
            {
                if (!line.StartsWith("anomaly-map ", StringComparison.Ordinal) && !line.StartsWith("ss3d-map ", StringComparison.Ordinal)) // ss3d-map: before D347
                    throw new FakeMapFormatException(sourceName, lineNo, "the first line must be 'anomaly-map 1'");
                sawMagic = true;
                continue;
            }
            if (line.StartsWith("source ", StringComparison.Ordinal) || line.StartsWith("chunk ", StringComparison.Ordinal))
                continue;
            if (line.StartsWith("size ", StringComparison.Ordinal))
            {
                string[] parts = line.Split(' ');
                if (parts.Length != 4 || !int.TryParse(parts[1], out width) || !int.TryParse(parts[2], out height) || !int.TryParse(parts[3], out levels))
                    throw new FakeMapFormatException(sourceName, lineNo, "bad size line");
                continue;
            }
            if (line.StartsWith("default ", StringComparison.Ordinal))
            {
                defaults = Model(line.Substring("default ".Length), lineNo);
                continue;
            }
            if (line[0] == '[')
            {
                // [chunk X Y Z]
                string[] parts = line.TrimEnd(']').Substring(1).Split(' ');
                if (parts.Length != 4 || parts[0] != "chunk" || !int.TryParse(parts[3], out z))
                    throw new FakeMapFormatException(sourceName, lineNo, "bad chunk line");
                continue;
            }
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            int comma = line.IndexOf(',');
            if (colon < 0 || comma < 0 || comma > colon
                || !int.TryParse(line.AsSpan(0, comma), out int x) || !int.TryParse(line.AsSpan(comma + 1, colon - comma - 1), out int y))
                throw new FakeMapFormatException(sourceName, lineNo, "bad tile line");
            if (x < 1 || y < 1 || x > width || y > height || z < 1 || z > levels)
                throw new FakeMapFormatException(sourceName, lineNo, "tile " + x + "," + y + "," + z + " is outside the map");
            lines.Add((x, y, z, Model(line.Substring(colon + 2), lineNo)));
        }
        if (!sawMagic || width <= 0 || height <= 0 || levels <= 0 || defaults == null)
            throw new FakeMapFormatException(sourceName, lineNo, "the header (anomaly-map, size, default) is incomplete");
        return new RawMap { Width = width, Height = height, Levels = levels, Default = defaults, Lines = lines, Warnings = warnings };
    }

    private static List<RawMember> ParseMembers(string text, string source, int line, List<string> warnings)
    {
        var members = new List<RawMember>();
        foreach (string part in SplitOutside(text, " | "))
        {
            string member = part.Trim();
            if (member.Length == 0 || member[0] != '/')
                throw new FakeMapFormatException(source, line, "bad member '" + member + "'");
            int brace = member.IndexOf('{');
            if (brace < 0)
            {
                members.Add(new RawMember { Path = member, Overrides = Array.Empty<(string, string)>() });
                continue;
            }
            if (member[^1] != '}')
            {
                // Not a valid file (map-format.md 4.3); read it anyway as the bare type.
                warnings.Add(source + ":" + line + ": unclosed override list in '" + member + "'");
                members.Add(new RawMember { Path = member.Substring(0, brace), Overrides = Array.Empty<(string, string)>() });
                continue;
            }
            var overrides = new List<(string, string)>();
            foreach (string item in SplitOutside(member.Substring(brace + 1, member.Length - brace - 2), "; "))
            {
                int equals = item.IndexOf(" = ", StringComparison.Ordinal);
                if (equals <= 0)
                    throw new FakeMapFormatException(source, line, "bad override '" + item + "'");
                overrides.Add((item.Substring(0, equals).Trim(), item.Substring(equals + 3).Trim()));
            }
            members.Add(new RawMember { Path = member.Substring(0, brace), Overrides = overrides });
        }
        return members;
    }

    /// <summary>Splits at the separator outside strings, resources, braces and parentheses (map-format.md 4.3).</summary>
    private static List<string> SplitOutside(string text, string separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        bool inString = false, inResource = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < text.Length)
                    current.Append(text[++i]);
                else if (c == '"')
                    inString = false;
                continue;
            }
            if (inResource)
            {
                current.Append(c);
                if (c == '\'')
                    inResource = false;
                continue;
            }
            if (c == '"') { inString = true; current.Append(c); continue; }
            if (c == '\'') { inResource = true; current.Append(c); continue; }
            if (c == '{' || c == '(') depth++;
            else if (c == '}' || c == ')') depth--;
            if (depth == 0 && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
            {
                parts.Add(current.ToString());
                current.Clear();
                i += separator.Length - 1;
                continue;
            }
            current.Append(c);
        }
        parts.Add(current.ToString());
        return parts;
    }
}
