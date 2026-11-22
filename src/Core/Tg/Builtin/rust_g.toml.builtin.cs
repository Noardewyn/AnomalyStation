// Engine part (syndicate package A3, D316): rust-g's toml_file_to_json (src/toml.rs), which Dynamic's load_config reads its config with
// (rustg_raw_read_toml_file, code/__DEFINES/rust_g.dm:553), written natively as the other rust-g functions (rust_g.builtin.cs): the file
// through the world's file seam, parsed as TOML into the JSON text rust-g gives (its tables in file order).
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/rust_g.dm:553  #define rustg_raw_read_toml_file(path) json_decode(RUSTG_CALL(RUST_G, "toml_file_to_json")(path) || "null")
    public static DmList rustg_raw_read_toml_file(object path) => Dm.json_decode(rustg_toml_file_to_json(path) ?? "null") as DmList;

    // tg: code/__DEFINES/rust_g.dm:555 (gaps BR1: the modular map roots read their .toml with it, modular_map_loader.dm:30)
    public static object rustg_read_toml_file(object path = null)
    {
        DmList output = rustg_raw_read_toml_file(path);
        if (Dm.truthy(output["success"]))
            return Dm.json_decode(output["content"]);
        else
            throw Dm.CRASH(output["content"], "code/__DEFINES/rust_g.dm", 560);
    }

    /// <summary>
    /// rust-g's <c>toml_file_to_json</c>: <c>{"success": true, "content": JSON}</c> with the file's TOML as a JSON text, or
    /// <c>{"success": false, "content": ERROR}</c> for a missing file or a TOML error.
    /// </summary>
    public static string rustg_toml_file_to_json(object path)
    {
        string text = DmWorldState.Current.Files.ReadText(Dm.text("[]", path));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            try
            {
                if (text == null)
                    throw new FormatException("No such file or directory (os error 2)");
                string content = TomlToJson(text);
                writer.WriteBoolean("success", true);
                writer.WriteString("content", content);
            }
            catch (FormatException e)
            {
                writer.WriteBoolean("success", false);
                writer.WriteString("content", e.Message);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // A TOML document (tables, dotted and quoted keys, strings, integers, floats, booleans, arrays, inline tables; no dates) as a JSON text.
    internal static string TomlToJson(string text)
    {
        var root = new TomlTable();
        TomlTable current = root;
        var p = new TomlParser(text);
        while (p.SkipBlank())
        {
            if (p.Peek == '[')
            {
                bool arrayTable = p.Peek2 == '[';
                p.Pos += arrayTable ? 2 : 1;
                List<string> keys = p.Key();
                p.Expect(']');
                if (arrayTable)
                    p.Expect(']');
                TomlTable parent = root.Walk(keys, keys.Count - 1);
                if (arrayTable)
                {
                    if (parent.Get(keys[^1]) is not List<object> array)
                        parent.Set(keys[^1], array = new List<object>());
                    array.Add(current = new TomlTable());
                }
                else
                {
                    current = parent.Get(keys[^1]) as TomlTable ?? new TomlTable();
                    parent.Set(keys[^1], current);
                }
            }
            else
            {
                List<string> keys = p.Key();
                p.Expect('=');
                current.Walk(keys, keys.Count - 1).Set(keys[^1], p.Value());
            }
            p.EndOfLine();
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteJson(writer, root);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJson(Utf8JsonWriter w, object value)
    {
        switch (value)
        {
            case TomlTable table:
                w.WriteStartObject();
                foreach ((string key, object item) in table.Items)
                {
                    w.WritePropertyName(key);
                    WriteJson(w, item);
                }
                w.WriteEndObject();
                break;
            case List<object> array:
                w.WriteStartArray();
                foreach (object item in array)
                    WriteJson(w, item);
                w.WriteEndArray();
                break;
            case string s: w.WriteStringValue(s); break;
            case bool b: w.WriteBooleanValue(b); break;
            case long l: w.WriteNumberValue(l); break;
            case double d: w.WriteNumberValue(d); break;
        }
    }

    private sealed class TomlTable
    {
        public readonly List<(string Key, object Value)> Items = new();

        public object Get(string key) => Items.Find(i => i.Key == key).Value;

        public void Set(string key, object value)
        {
            int at = Items.FindIndex(i => i.Key == key);
            if (at >= 0)
                Items[at] = (key, value);
            else
                Items.Add((key, value));
        }

        // The table a dotted key's first parts name, made as needed.
        public TomlTable Walk(List<string> keys, int count)
        {
            TomlTable table = this;
            for (int i = 0; i < count; i++)
            {
                object next = table.Get(keys[i]);
                if (next is List<object> array && array.Count > 0 && array[^1] is TomlTable last)
                    next = last;
                if (next is not TomlTable child)
                    table.Set(keys[i], child = new TomlTable());
                table = child;
            }
            return table;
        }
    }

    private sealed class TomlParser(string text)
    {
        public int Pos;

        public char Peek => Pos < text.Length ? text[Pos] : '\0';

        public char Peek2 => Pos + 1 < text.Length ? text[Pos + 1] : '\0';

        // Skips spaces, newlines and comments; false at the end.
        public bool SkipBlank()
        {
            while (Pos < text.Length)
            {
                char c = text[Pos];
                if (c == '#')
                    while (Pos < text.Length && text[Pos] != '\n') Pos++;
                else if (char.IsWhiteSpace(c))
                    Pos++;
                else
                    return true;
            }
            return false;
        }

        private void SkipSpaces()
        {
            while (Peek is ' ' or '\t') Pos++;
        }

        public void Expect(char c)
        {
            SkipSpaces();
            if (Peek != c)
                throw Error("expected '" + c + "'");
            Pos++;
        }

        public void EndOfLine()
        {
            SkipSpaces();
            if (Peek == '#')
                while (Pos < text.Length && text[Pos] != '\n') Pos++;
            if (Peek is not ('\n' or '\r' or '\0'))
                throw Error("expected a newline");
        }

        public List<string> Key()
        {
            var keys = new List<string>();
            while (true)
            {
                SkipSpaces();
                if (Peek is '"' or '\'')
                    keys.Add(String());
                else
                {
                    int start = Pos;
                    while (char.IsLetterOrDigit(Peek) || Peek is '_' or '-') Pos++;
                    if (Pos == start)
                        throw Error("expected a key");
                    keys.Add(text[start..Pos]);
                }
                SkipSpaces();
                if (Peek != '.')
                    return keys;
                Pos++;
            }
        }

        public object Value()
        {
            SkipSpaces();
            char c = Peek;
            if (c is '"' or '\'')
                return String();
            if (c == '[')
            {
                Pos++;
                var array = new List<object>();
                while (true)
                {
                    SkipBlank();
                    if (Peek == ']')
                    {
                        Pos++;
                        return array;
                    }
                    array.Add(Value());
                    SkipBlank();
                    if (Peek == ',')
                        Pos++;
                    else if (Peek != ']')
                        throw Error("expected ',' or ']'");
                }
            }
            if (c == '{')
            {
                Pos++;
                var table = new TomlTable();
                SkipSpaces();
                if (Peek == '}')
                {
                    Pos++;
                    return table;
                }
                while (true)
                {
                    List<string> keys = Key();
                    Expect('=');
                    table.Walk(keys, keys.Count - 1).Set(keys[^1], Value());
                    SkipSpaces();
                    if (Peek == '}')
                    {
                        Pos++;
                        return table;
                    }
                    Expect(',');
                }
            }
            int start = Pos;
            while (Pos < text.Length && text[Pos] is not (',' or ']' or '}' or '#' or '\n' or '\r' or ' ' or '\t')) Pos++;
            string word = text[start..Pos].Replace("_", "", StringComparison.Ordinal);
            if (word == "true") return true;
            if (word == "false") return false;
            if (word is "inf" or "+inf") return double.PositiveInfinity;
            if (word == "-inf") return double.NegativeInfinity;
            if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                return integer;
            if (word.StartsWith("0x", StringComparison.Ordinal) && long.TryParse(word[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
                return hex;
            if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
                return real;
            throw Error("invalid value '" + word + "'");
        }

        // A basic ("...", with escapes) or literal ('...') string, single-line or multi-line.
        private string String()
        {
            char quote = text[Pos];
            bool multi = Pos + 2 < text.Length && text[Pos + 1] == quote && text[Pos + 2] == quote;
            Pos += multi ? 3 : 1;
            if (multi && Peek == '\n')
                Pos++;
            var sb = new StringBuilder();
            while (true)
            {
                if (Pos >= text.Length)
                    throw Error("unterminated string");
                char c = text[Pos];
                if (c == quote && (!multi || (Peek2 == quote && Pos + 2 < text.Length && text[Pos + 2] == quote)))
                {
                    Pos += multi ? 3 : 1;
                    return sb.ToString();
                }
                if (c == '\n' && !multi)
                    throw Error("newline in a string");
                Pos++;
                if (c != '\\' || quote == '\'')
                {
                    sb.Append(c);
                    continue;
                }
                char e = text[Pos++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'u': sb.Append((char)int.Parse(text.AsSpan(Pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); Pos += 4; break;
                    case 'U': sb.Append(char.ConvertFromUtf32(int.Parse(text.AsSpan(Pos, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture))); Pos += 8; break;
                    case '\n' or '\r' or ' ' or '\t' when multi:
                        while (Pos < text.Length && char.IsWhiteSpace(text[Pos])) Pos++;
                        break;
                    default: throw Error("invalid escape '\\" + e + "'");
                }
            }
        }

        private FormatException Error(string what)
        {
            int line = 1;
            for (int i = 0; i < Pos && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return new FormatException("TOML parse error at line " + line + ": " + what);
        }
    }
}
