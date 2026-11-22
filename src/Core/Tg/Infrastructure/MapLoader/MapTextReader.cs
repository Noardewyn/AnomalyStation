using System.Globalization;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: M3 P2. The engine map reader behind MapText.Parse and MapValue.Parse (docs/ai/specs/map-format.md 4): it replaces
// /datum/parsed_map's DM text parsing (New's grid parsing, tgm_build_cache, readlist, parse_constant; reader.dm:178-296,
// 687-779, 1004-1102). Pure: no World, no tg code, no statics.

/// <summary>The parser of <c>.as3dmap</c> files and of their value literals.</summary>
internal static class MapTextReader
{
    private const string Magic = "anomaly-map";
    private const string OldMagic = "ss3d-map"; // D347: the header before the game was named Anomaly Station; still read
    private const string ValueSource = "<value>";

    // ---- values (map-format.md 4.4; tg's parse_constant, reader.dm:1063-1102) ----

    /// <summary>tg's <c>parse_constant</c> on a literal, in tg's order (the literal is trimmed first, as tg's callers do).</summary>
    internal static MapValue ParseValue(string literal)
    {
        string text = (literal ?? "").Trim();

        // empty text
        if (text.Length == 0)
            return new MapValue { Kind = MapValueKind.Empty, Text = "", Literal = text };

        // number
        float? num = Text2Num(text);
        if (num.HasValue)
            return new MapValue { Kind = MapValueKind.Number, Number = num.Value, Literal = text };

        // string: copytext(text, 2, -1), not unescaped
        if (text[0] == '"')
            return new MapValue { Kind = MapValueKind.Text, Text = text.Length >= 2 ? text.Substring(1, text.Length - 2) : "", Literal = text };

        // list: readlist(copytext(text, 6, -1))
        if (text.StartsWith("list(", StringComparison.Ordinal))
        {
            if (text[^1] != ')') throw new MapTextException(ValueSource, 0, "a list literal does not end with ')': " + text);
            return new MapValue { Kind = MapValueKind.List, Items = ReadList(text.Substring(5, text.Length - 6), text), Literal = text };
        }

        // typepath: text2path resolves it at load (MapValue.ToDm); here a typepath is its syntax
        if (IsTypePath(text))
            return new MapValue { Kind = MapValueKind.Path, Text = text, Literal = text };

        // file: file(copytext_char(text, 2, -1))
        if (text[0] == '\'')
            return new MapValue { Kind = MapValueKind.Resource, Text = text.Length >= 2 ? text.Substring(1, text.Length - 2) : "", Literal = text };

        // null
        if (text == "null")
            return new MapValue { Kind = MapValueKind.Null, Literal = text };

        // fallback: string
        return new MapValue { Kind = MapValueKind.Fallback, Text = text, Literal = text };
    }

    // BYOND's text2num, with the decimal exponent BYOND writes ("2e+005", Meta's SMES charge): the shim reads it (M3 I1b).
    private static float? Text2Num(string text) => Dm.text2num(text);

    // tg's readlist (reader.dm:1004-1060) with "," as the delimiter: items separated by commas outside strings, resources
    // and parentheses; an item with "=" outside them is an association; an empty item is skipped.
    // tg finds an item's "=" with a search that can run past the item's comma (a plain item followed by an association
    // reads wrongly in tg); here the "=" must be inside the item. The converter refuses such lists (map-format.md 6, rule 4).
    private static IReadOnlyList<(MapValue Key, MapValue Value)> ReadList(string body, string whole)
    {
        var items = new List<(MapValue, MapValue)>();
        foreach (string item in SplitOutside(body, ",", whole))
        {
            string trimmed = item.Trim();
            if (trimmed.Length == 0) continue;
            int equal = IndexOutside(trimmed, "=", 0, whole);
            if (equal < 0)
            {
                items.Add((null, ParseValue(trimmed)));
                continue;
            }
            items.Add((ParseValue(trimmed.Substring(0, equal)), ParseValue(trimmed.Substring(equal + 1))));
        }
        return items;
    }

    /// <summary>Whether <paramref name="text"/> is a typepath (<c>/seg/seg</c>, segments of letters, digits and underscores).</summary>
    internal static bool IsTypePath(string text)
    {
        if (text.Length < 2 || text[0] != '/') return false;
        bool segment = false;
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '/')
            {
                if (!segment) return false;
                segment = false;
                continue;
            }
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
            segment = true;
        }
        return segment;
    }

    /// <summary>Whether <paramref name="text"/> is a var name (<c>[A-Za-z_][A-Za-z0-9_]*</c>).</summary>
    internal static bool IsVarName(string text)
    {
        if (text.Length == 0 || !(char.IsAsciiLetter(text[0]) || text[0] == '_')) return false;
        for (int i = 1; i < text.Length; i++)
        {
            if (!(char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) return false;
        }
        return true;
    }

    // The parts of `text` between occurrences of `separator` outside strings ("..." with backslash escapes), resources
    // ('...'), parentheses and braces.
    internal static List<string> SplitOutside(string text, string separator, string context)
    {
        var parts = new List<string>();
        int start = 0;
        while (true)
        {
            int at = IndexOutside(text, separator, start, context);
            if (at < 0)
            {
                parts.Add(text.Substring(start));
                return parts;
            }
            parts.Add(text.Substring(start, at - start));
            start = at + separator.Length;
        }
    }

    // The first position at or after `from` where `needle` starts outside strings, resources, parentheses and braces; -1
    // when none. An unterminated string or resource, or unbalanced brackets, is a malformed literal.
    internal static int IndexOutside(string text, string needle, int from, string context)
    {
        int depth = 0;
        int found = -1;
        for (int i = from; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' || c == '\'')
            {
                int end = SkipQuoted(text, i);
                if (end < 0) throw new MapTextException(ValueSource, 0, "an unterminated " + (c == '"' ? "string" : "resource") + " in " + context);
                i = end;
                continue;
            }
            if (c == '(' || c == '{')
            {
                depth++;
                continue;
            }
            if (c == ')' || c == '}')
            {
                depth--;
                if (depth < 0) throw new MapTextException(ValueSource, 0, "an unbalanced '" + c + "' in " + context);
                continue;
            }
            if (depth == 0 && found < 0 && string.CompareOrdinal(text, i, needle, 0, needle.Length) == 0)
            {
                found = i;
                break;
            }
        }
        if (found < 0 && depth != 0)
            throw new MapTextException(ValueSource, 0, "an unbalanced bracket in " + context);
        return found;
    }

    // The position of the quote that closes the string or resource opening at `open`, or -1.
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
        }
        return -1;
    }

    // ---- members (map-format.md 4.3) ----

    /// <summary>The members of a model text: atoms separated by <c> | </c>, each a typepath with optional overrides.</summary>
    internal static List<MapMember> ParseMembers(string text, string sourceName, int line)
    {
        var members = new List<MapMember>();
        List<string> parts;
        try
        {
            parts = SplitOutside(text, " | ", text);
        }
        catch (MapTextException e)
        {
            throw new MapTextException(sourceName, line, e.Message.Substring(e.Message.IndexOf(": ", StringComparison.Ordinal) + 2));
        }
        foreach (string part in parts)
            members.Add(ParseMember(part, sourceName, line));
        return members;
    }

    private static MapMember ParseMember(string text, string sourceName, int line)
    {
        int brace = text.IndexOf('{');
        string path = brace < 0 ? text : text.Substring(0, brace);
        if (!IsTypePath(path)) throw new MapTextException(sourceName, line, "not a typepath: '" + path + "'");
        if (brace < 0) return new MapMember(path, Array.Empty<MapOverride>());
        if (text[^1] != '}' || text.Length == brace + 2)
            throw new MapTextException(sourceName, line, "malformed overrides of " + path);
        string body = text.Substring(brace + 1, text.Length - brace - 2);
        var overrides = new List<MapOverride>();
        try
        {
            foreach (string item in SplitOutside(body, "; ", text))
            {
                int eq = item.IndexOf(" = ", StringComparison.Ordinal);
                if (eq <= 0) throw new MapTextException(sourceName, line, "an override is not 'var = value': '" + item + "'");
                string name = item.Substring(0, eq);
                if (!IsVarName(name)) throw new MapTextException(sourceName, line, "not a var name: '" + name + "'");
                string literal = item.Substring(eq + 3);
                if (literal.Length == 0 || literal.Trim().Length != literal.Length)
                    throw new MapTextException(sourceName, line, "the value of " + name + " is empty or padded");
                overrides.Add(new MapOverride(name, ParseValue(literal)));
            }
        }
        catch (MapTextException e) when (e.SourceName == ValueSource)
        {
            throw new MapTextException(sourceName, line, e.Message.Substring(e.Message.IndexOf(": ", StringComparison.Ordinal) + 2));
        }
        return new MapMember(path, overrides);
    }

    // ---- the file (map-format.md 4) ----

    internal static ParsedMapText Parse(string text, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        sourceName ??= "<map>";
        if (text.Length > 0 && text[0] == '﻿') throw new MapTextException(sourceName, 1, "a byte order mark");
        string[] lines = text.Split('\n');
        int count = lines.Length;
        if (count > 0 && lines[^1].Length == 0) count--; // the final line end

        string source = null;
        int width = 0, height = 0, levels = 0;
        MapModel defaultModel = null;
        var models = new List<MapModel>();
        var byText = new Dictionary<string, int>(StringComparer.Ordinal);
        int[] tiles = null;
        var tileLines = new List<(int X, int Y, int Z, MapModel Model)>();

        // header state: 0 magic, 1 source or size, 2 chunk, 3 default, 4 sections
        int header = 0;
        bool inChunk = false;
        int chunkX = 0, chunkY = 0, chunkZ = 0, chunkLines = 0;
        int lastX = 0, lastY = 0;
        long lastChunkOrder = -1;

        for (int i = 0; i < count; i++)
        {
            int number = i + 1;
            string line = lines[i];
            if (line.Length == 0) continue;
            if (line.Contains('\r')) throw new MapTextException(sourceName, number, "a carriage return (line ends are \\n)");
            if (line.Contains('\t')) throw new MapTextException(sourceName, number, "a tab");
            if (line[^1] == ' ') throw new MapTextException(sourceName, number, "a trailing space");
            if (line[0] == '#') continue;

            switch (header)
            {
                case 0:
                    string version = " " + MapText.FormatVersion.ToString(CultureInfo.InvariantCulture);
                    if (line != Magic + version && line != OldMagic + version)
                        throw new MapTextException(sourceName, number, "not a version " + MapText.FormatVersion + " map: the first line must be '" + Magic + " " + MapText.FormatVersion + "'");
                    header = 1;
                    continue;
                case 1:
                    if (source == null && line.StartsWith("source ", StringComparison.Ordinal))
                    {
                        source = line.Substring(7);
                        if (source.Length == 0) throw new MapTextException(sourceName, number, "an empty source line");
                        continue;
                    }
                    string[] size = line.Split(' ');
                    if (size.Length != 4 || size[0] != "size"
                        || !TryInt(size[1], 1, 255, out width) || !TryInt(size[2], 1, 255, out height) || !TryInt(size[3], 1, 16, out levels))
                        throw new MapTextException(sourceName, number, "expected 'size <W> <H> <Z>' (1 to 255, 1 to 255, 1 to 16)");
                    tiles = new int[width * height * levels];
                    header = 2;
                    continue;
                case 2:
                    if (line != "chunk " + MapText.ChunkSize.ToString(CultureInfo.InvariantCulture))
                        throw new MapTextException(sourceName, number, "expected 'chunk " + MapText.ChunkSize + "'");
                    header = 3;
                    continue;
                case 3:
                    if (!line.StartsWith("default ", StringComparison.Ordinal))
                        throw new MapTextException(sourceName, number, "expected 'default <members>'");
                    string defaultText = line.Substring(8);
                    defaultModel = new MapModel { Members = ParseMembers(defaultText, sourceName, number), Text = defaultText };
                    models.Add(defaultModel);
                    byText.Add(defaultText, 0);
                    header = 4;
                    continue;
            }

            if (line[0] == '[')
            {
                if (inChunk && chunkLines == 0) throw new MapTextException(sourceName, number - 1, "an empty chunk (a chunk with no line is not written)");
                if (!TryChunkHeader(line, out chunkX, out chunkY, out chunkZ))
                    throw new MapTextException(sourceName, number, "expected '[chunk <X> <Y> <Z>]'");
                if ((chunkX - 1) % MapText.ChunkSize != 0 || (chunkY - 1) % MapText.ChunkSize != 0
                    || chunkX > width || chunkY > height || chunkZ > levels)
                    throw new MapTextException(sourceName, number, "the chunk's corner is not 1 + 32 * i inside the map's size");
                long order = ((long)chunkZ * 1024 + chunkY) * 1024 + chunkX;
                if (order <= lastChunkOrder) throw new MapTextException(sourceName, number, "chunks are not in z, y, x order");
                lastChunkOrder = order;
                inChunk = true;
                chunkLines = 0;
                continue;
            }

            if (!inChunk) throw new MapTextException(sourceName, number, "a tile line outside a chunk");
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            int comma = colon < 0 ? -1 : line.IndexOf(',', 0, colon);
            if (colon < 0 || comma < 0
                || !TryInt(line.Substring(0, comma), 1, 255, out int x) || !TryInt(line.Substring(comma + 1, colon - comma - 1), 1, 255, out int y))
                throw new MapTextException(sourceName, number, "expected '<x>,<y>: <members>'");
            if (x < chunkX || x >= chunkX + MapText.ChunkSize || y < chunkY || y >= chunkY + MapText.ChunkSize || x > width || y > height)
                throw new MapTextException(sourceName, number, $"the tile {x},{y} is outside its chunk or the map");
            if (chunkLines > 0 && (y > lastY || (y == lastY && x <= lastX)))
                throw new MapTextException(sourceName, number, "tile lines are not in y decreasing, x increasing order");
            string modelText = line.Substring(colon + 2);
            if (modelText == defaultModel.Text) throw new MapTextException(sourceName, number, "a tile equal to the default is not written");
            if (!byText.TryGetValue(modelText, out int index))
            {
                index = models.Count;
                models.Add(new MapModel { Members = ParseMembers(modelText, sourceName, number), Text = modelText });
                byText.Add(modelText, index);
            }
            tiles[((chunkZ - 1) * height + (y - 1)) * width + (x - 1)] = index;
            tileLines.Add((x, y, chunkZ, models[index]));
            chunkLines++;
            lastX = x;
            lastY = y;
        }

        if (header < 4) throw new MapTextException(sourceName, count, "the header is incomplete");
        if (inChunk && chunkLines == 0) throw new MapTextException(sourceName, count, "an empty chunk (a chunk with no line is not written)");
        var parsed = new ParsedMapText
        {
            SourceName = sourceName,
            Source = source,
            Width = width,
            Height = height,
            Levels = levels,
            Default = defaultModel,
            Models = models,
        };
        parsed.dm_tiles = tiles;
        parsed.dm_lines = tileLines;
        return parsed;
    }

    private static bool TryChunkHeader(string line, out int x, out int y, out int z)
    {
        x = y = z = 0;
        if (!line.StartsWith("[chunk ", StringComparison.Ordinal) || line[^1] != ']') return false;
        string[] parts = line.Substring(7, line.Length - 8).Split(' ');
        return parts.Length == 3 && TryInt(parts[0], 1, 255, out x) && TryInt(parts[1], 1, 255, out y) && TryInt(parts[2], 1, 16, out z);
    }

    // A decimal integer in [min, max] without sign, padding or leading zeros.
    private static bool TryInt(string text, int min, int max, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 4 || (text.Length > 1 && text[0] == '0')) return false;
        foreach (char c in text)
        {
            if (c < '0' || c > '9') return false;
            value = value * 10 + (c - '0');
        }
        return value >= min && value <= max;
    }
}
