namespace Anomaly.Tools.Maps;

/// <summary>One atom of a .dmm model: its typepath and its var edits as literal text, in the file's order.</summary>
public sealed record DmmMember(string Path, IReadOnlyList<(string Var, string Literal)> Overrides);

/// <summary>One model of a .dmm file: the key, its members in tg's order and the line it starts on.</summary>
public sealed record DmmModel(string Key, IReadOnlyList<DmmMember> Members, int Line);

/// <summary>
/// A .dmm file in TGM or DMM syntax, read the way tg's <c>/datum/parsed_map</c> reads it (reader.dm:178-296, 687-890):
/// the models ("key" = (members)), the grid sets ((x,y,z) = {"keys"}) with the top row first, the key length promised by
/// the format. Duplicate model keys are ignored as tg ignores them (the first wins).
/// </summary>
public sealed class DmmFile
{
    /// <summary>The models by key.</summary>
    public Dictionary<string, DmmModel> Models { get; } = new(StringComparer.Ordinal);

    /// <summary>The key of every tile (x, y, z from 1) the grid sets cover; a tile covered twice is in <see cref="Duplicates"/>.</summary>
    public Dictionary<(int X, int Y, int Z), string> Tiles { get; } = new();

    /// <summary>Tiles the grid sets cover more than once, with the line of the later grid set.</summary>
    public List<((int X, int Y, int Z) Tile, int Line)> Duplicates { get; } = new();

    /// <summary>The highest x, y and z the grid covers.</summary>
    public int MaxX { get; private set; }

    public int MaxY { get; private set; }

    public int MaxZ { get; private set; }

    /// <summary>Whether the file is TGM (tg's matches_tgm regex: a key followed by "(" and a line end).</summary>
    public bool IsTgm { get; private set; }

    /// <summary>Parses a .dmm text. Throws <see cref="FormatException"/> with the line for what tg's reader would CRASH on or cannot read.</summary>
    public static DmmFile Parse(string text, string sourceName)
    {
        var file = new DmmFile();
        text = text.Replace("\r\n", "\n");
        int keyLen = 0;
        int i = 0;
        int line = 1;
        int n = text.Length;
        while (i < n)
        {
            char c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }
            if (c == ' ' || c == '\t')
            {
                i++;
                continue;
            }
            if (c == '/' && i + 1 < n && text[i + 1] == '/')
            {
                while (i < n && text[i] != '\n') i++;
                continue;
            }
            if (c == '"')
            {
                // "key" = (members)
                int keyEnd = text.IndexOf('"', i + 1);
                if (keyEnd < 0) throw Error(sourceName, line, "an unterminated model key");
                string key = text.Substring(i + 1, keyEnd - i - 1);
                foreach (char k in key)
                {
                    if (!char.IsAsciiLetter(k)) throw Error(sourceName, line, "a model key of letters expected: \"" + key + "\"");
                }
                if (keyLen == 0) keyLen = key.Length;
                else if (keyLen != key.Length) throw Error(sourceName, line, "Inconsistent key length in DMM");
                int open = SkipSpaces(text, keyEnd + 1);
                if (open >= n || text[open] != '=') throw Error(sourceName, line, "expected '=' after the model key");
                open = SkipSpaces(text, open + 1);
                if (open >= n || text[open] != '(') throw Error(sourceName, line, "expected '(' after the model key");
                if (file.Models.Count == 0) file.IsTgm = open + 1 < n && text[open + 1] == '\n';
                int close = MatchClose(text, open, sourceName, line);
                string body = text.Substring(open + 1, close - open - 1);
                int startLine = line;
                line += Count(text, '\n', i, close);
                i = close + 1;
                if (file.Models.ContainsKey(key)) continue; // duplicate model keys are ignored in DMMs (reader.dm:213)
                file.Models.Add(key, new DmmModel(key, ParseMembers(body, sourceName, startLine), startLine));
                continue;
            }
            if (c == '(')
            {
                // (x,y,z) = {"keys"}
                int close = text.IndexOf(')', i);
                if (close < 0) throw Error(sourceName, line, "an unterminated grid coordinate");
                string[] coords = text.Substring(i + 1, close - i - 1).Split(',');
                if (coords.Length != 3 || !int.TryParse(coords[0], out int gx) || !int.TryParse(coords[1], out int gy) || !int.TryParse(coords[2], out int gz)
                    || gx < 1 || gy < 1 || gz < 1)
                    throw Error(sourceName, line, "a grid coordinate (x,y,z) expected");
                if (keyLen == 0) throw Error(sourceName, line, "Coords before model definition in DMM");
                int brace = text.IndexOf("{\"", close, StringComparison.Ordinal);
                int end = brace < 0 ? -1 : text.IndexOf("\"}", brace + 2, StringComparison.Ordinal);
                if (brace < 0 || end < 0) throw Error(sourceName, line, "a grid set {\"...\"} expected");
                int gridLine = line;
                var gridLines = new List<string>(text.Substring(brace + 2, end - brace - 2).Split('\n'));
                line += Count(text, '\n', i, end);
                i = end + 2;
                // reader.dm:252-262: drop the leading blank lines and one blank line at the end
                int leading = 0;
                while (leading < gridLines.Count && gridLines[leading].Length == 0) leading++;
                gridLines.RemoveRange(0, leading);
                if (gridLines.Count == 0) continue;
                if (gridLines[^1].Length == 0) gridLines.RemoveAt(gridLines.Count - 1);
                int top = gy + gridLines.Count - 1; // start at the top and work down
                for (int row = 0; row < gridLines.Count; row++)
                {
                    string keys = gridLines[row];
                    if (keys.Length % keyLen != 0) throw Error(sourceName, gridLine + row + 1, "a grid line is not a whole number of keys");
                    int y = top - row;
                    for (int k = 0; k * keyLen < keys.Length; k++)
                    {
                        int x = gx + k;
                        var tile = (x, y, gz);
                        string key = keys.Substring(k * keyLen, keyLen);
                        if (file.Tiles.ContainsKey(tile)) file.Duplicates.Add((tile, gridLine + row + 1));
                        file.Tiles[tile] = key;
                        file.MaxX = Math.Max(file.MaxX, x);
                        file.MaxY = Math.Max(file.MaxY, y);
                        file.MaxZ = Math.Max(file.MaxZ, gz);
                    }
                }
                continue;
            }
            throw Error(sourceName, line, "unexpected '" + c + "'");
        }
        return file;
    }

    // The members of a model body: atoms separated by commas outside strings, resources, parentheses and braces; each a
    // path with optional {var = value; ...} edits (tgm_build_cache and dmm_build_cache, reader.dm:687-890).
    private static List<DmmMember> ParseMembers(string body, string sourceName, int line)
    {
        var members = new List<DmmMember>();
        foreach (string raw in Split(body, ',', sourceName, line))
        {
            string member = raw.Trim();
            if (member.Length == 0) continue;
            int brace = member.IndexOf('{');
            string path = (brace < 0 ? member : member.Substring(0, brace)).Trim();
            var overrides = new List<(string, string)>();
            if (brace >= 0)
            {
                if (member[^1] != '}') throw Error(sourceName, line, "the var edits of " + path + " do not end with '}'");
                foreach (string rawEdit in Split(member.Substring(brace + 1, member.Length - brace - 2), ';', sourceName, line))
                {
                    string edit = rawEdit.Trim();
                    if (edit.Length == 0) continue;
                    int eq = edit.IndexOf('=');
                    if (eq < 0) throw Error(sourceName, line, "a var edit without a value: '" + edit + "' of " + path);
                    overrides.Add((edit.Substring(0, eq).Trim(), edit.Substring(eq + 1).Trim()));
                }
            }
            members.Add(new DmmMember(path, overrides));
        }
        return members;
    }

    // Splits at `separator` outside strings ("..." with escapes), resources ('...'), parentheses and braces.
    private static List<string> Split(string text, char separator, string sourceName, int line)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' || c == '\'')
            {
                i = SkipQuoted(text, i);
                if (i < 0) throw Error(sourceName, line, "an unterminated " + (c == '"' ? "string" : "resource"));
                continue;
            }
            if (c == '(' || c == '{') depth++;
            else if (c == ')' || c == '}') depth--;
            else if (c == separator && depth == 0)
            {
                parts.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (depth != 0) throw Error(sourceName, line, "unbalanced brackets in a model");
        parts.Add(text.Substring(start));
        return parts;
    }

    // The position of the parenthesis that closes the one at `open` (outside strings and resources).
    private static int MatchClose(string text, int open, string sourceName, int line)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' || c == '\'')
            {
                i = SkipQuoted(text, i);
                if (i < 0) throw Error(sourceName, line, "an unterminated " + (c == '"' ? "string" : "resource") + " in a model");
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        throw Error(sourceName, line, "an unterminated model");
    }

    private static int SkipQuoted(string text, int open)
    {
        char quote = text[open];
        for (int i = open + 1; i < text.Length; i++)
        {
            char c = text[i];
            if (quote == '"' && c == '\\')
            {
                i++;
                continue;
            }
            if (c == quote) return i;
            if (c == '\n') return -1;
        }
        return -1;
    }

    private static int SkipSpaces(string text, int i)
    {
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return i;
    }

    private static int Count(string text, char c, int from, int to)
    {
        int count = 0;
        for (int i = from; i < to && i < text.Length; i++)
        {
            if (text[i] == c) count++;
        }
        return count;
    }

    private static FormatException Error(string sourceName, int line, string message) => new FormatException(sourceName + ":" + line + ": " + message);
}
