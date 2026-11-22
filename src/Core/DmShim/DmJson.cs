using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Anomaly.Core.DmShim;

/// <summary>
/// <c>json_encode</c>, <c>json_decode</c>, <c>params2list</c> and <c>list2params</c> (BYOND ref; OpenDream
/// <c>DreamProcNativeRoot.JsonEncode</c>, <c>CreateValueFromJsonElement</c>, <c>Params2List</c>, <c>List2Params</c>),
/// over System.Text.Json. Culture-invariant. Owner: values.
/// </summary>
internal static class DmJson
{
    private const int MaxDepth = 20;

    // BYOND ref /proc/json_encode, JSON_PRETTY_PRINT: "Spaces will be added after colons and commas. Non-empty arrays
    // and object literals will have a line break and tabs before each item, and a line break with one fewer tab before
    // the closing bracket or brace." The line break is "\n" on every host (the writer's default is the host's newline).
    private static readonly JsonWriterOptions CompactOptions = new JsonWriterOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // "\"" rather than """, as BYOND writes it
    };

    private static readonly JsonWriterOptions PrettyOptions = new JsonWriterOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
        IndentCharacter = '\t',
        IndentSize = 1,
        NewLine = "\n",
    };

    internal static string Encode(object value, bool pretty)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, pretty ? PrettyOptions : CompactOptions))
        {
            Write(writer, value, pretty, new HashSet<DmList>(ReferenceEqualityComparer.Instance), arrayItem: false);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // `open` holds the lists being written around this value: "Situations where a list has a reference to itself will
    // cause the nested version to print a null value instead" (BYOND ref /proc/json_encode).
    private static void Write(Utf8JsonWriter writer, object value, bool pretty, HashSet<DmList> open, bool arrayItem)
    {
        // "In parity with DM, we give up and just print a 'null' at the maximum recursion" (OpenDream).
        if (writer.CurrentDepth >= MaxDepth)
        {
            writer.WriteNullValue();
            return;
        }
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string s:
                writer.WriteStringValue(DmTextRender.StripMarkers(s));
                return;
            case Type t:
                writer.WriteStringValue(DmTextRender.TypePathText(t));
                return;
            case DmList list:
                if (!open.Add(list))
                {
                    writer.WriteNullValue();
                    return;
                }
                WriteList(writer, list, pretty, open);
                open.Remove(list);
                return;
            case matrix m:
                if (pretty)
                {
                    // "the list form for matrices, will not be given tabs and line breaks", only the spaces.
                    WriteInline(writer, "[" + NumberText(m.a) + ", " + NumberText(m.b) + ", " + NumberText(m.c) + ", "
                        + NumberText(m.d) + ", " + NumberText(m.e) + ", " + NumberText(m.f) + "]", arrayItem);
                    return;
                }
                writer.WriteStartArray();
                writer.WriteNumberValue(m.a);
                writer.WriteNumberValue(m.b);
                writer.WriteNumberValue(m.c);
                writer.WriteNumberValue(m.d);
                writer.WriteNumberValue(m.e);
                writer.WriteNumberValue(m.f);
                writer.WriteEndArray();
                return;
        }
        if (Dm.TryGetNumber(value, out float f))
        {
            if (float.IsFinite(f))
            {
                writer.WriteNumberValue(f);
                return;
            }
            // BYOND's escape for the specials JSON has no syntax for (OpenDream test JsonEncode); with
            // JSON_PRETTY_PRINT it is not given tabs and line breaks either.
            string special = float.IsNaN(f) ? "NaN" : f > 0 ? "Infinity" : "-Infinity";
            if (pretty)
            {
                WriteInline(writer, "{\"__number__\": \"" + special + "\"}", arrayItem);
                return;
            }
            writer.WriteStartObject();
            writer.WriteString("__number__", special);
            writer.WriteEndObject();
            return;
        }
        writer.WriteStringValue(DmTextRender.Plain(value));
    }

    // A value written on one line inside indented output. The writer puts a raw array item right after the comma, so
    // the line break and the tabs before it are part of the raw text (a property value already follows its "key": ).
    private static void WriteInline(Utf8JsonWriter writer, string json, bool arrayItem)
    {
        if (arrayItem) json = "\n" + new string('\t', writer.CurrentDepth) + json;
        writer.WriteRawValue(json, skipInputValidation: true);
    }

    // A finite number as the writer writes it (the shortest text that reads back as the same float).
    private static string NumberText(float f) => f.ToString("R", CultureInfo.InvariantCulture);

    private static void WriteList(Utf8JsonWriter writer, DmList list, bool pretty, HashSet<DmList> open)
    {
        object[] items = list.CopyItems();
        if (!list.HasAssociations)
        {
            writer.WriteStartArray();
            foreach (object item in items) Write(writer, item, pretty, open, arrayItem: true);
            writer.WriteEndArray();
            return;
        }
        writer.WriteStartObject();
        foreach (object item in items)
        {
            writer.WritePropertyName(DmTextRender.StripMarkers(DmTextRender.Plain(item)));
            if (list.TryGetAssociated(item, out object associated)) Write(writer, associated, pretty, open, arrayItem: false);
            else writer.WriteNullValue();
        }
        writer.WriteEndObject();
    }

    internal static object Decode(string json)
    {
        if (json == null) throw new DmRuntimeException("json_decode(): null is not JSON");
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 512 });
        }
        catch (JsonException e)
        {
            throw new DmRuntimeException("json_decode(): invalid JSON: " + e.Message);
        }
        using (document)
        {
            return Read(document.RootElement);
        }
    }

    private static object Read(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var list = new DmList();
                foreach (JsonElement child in element.EnumerateArray()) list.AppendItem(Read(child));
                return list;
            }
            case JsonValueKind.Object:
            {
                var list = new DmList();
                int count = 0;
                JsonProperty only = default;
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (count++ == 0) only = property;
                }
                if (count == 1 && only.Name == "__number__")
                {
                    string raw = only.Value.ValueKind == JsonValueKind.String ? only.Value.GetString() : null;
                    return raw switch
                    {
                        "Infinity" => float.PositiveInfinity,
                        "-Infinity" => float.NegativeInfinity,
                        _ => float.NaN,
                    };
                }
                foreach (JsonProperty property in element.EnumerateObject()) list[property.Name] = Read(property.Value);
                return list;
            }
            case JsonValueKind.String:
                return element.GetString() ?? "";
            case JsonValueKind.Number:
                return (float)element.GetDouble();
            case JsonValueKind.True:
                return 1f;
            case JsonValueKind.False:
                return 0f;
            default:
                return null;
        }
    }

    // params2list: "a=1;b=2&c" (';' and '&' both separate); keys and values URL-decoded ('+' is a space); a key without
    // '=' gets ""; a key given more than once gets a list of its values, in order.
    internal static DmList ParamsToList(string text)
    {
        var result = new DmList();
        if (string.IsNullOrEmpty(text)) return result;
        var order = new List<string>();
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string part in text.Split('&', ';'))
        {
            if (part.Length == 0) continue;
            int eq = part.IndexOf('=');
            string key = UrlDecode(eq < 0 ? part : part.Substring(0, eq));
            string value = eq < 0 ? "" : UrlDecode(part.Substring(eq + 1));
            if (!values.TryGetValue(key, out List<string> list))
            {
                values[key] = list = new List<string>();
                order.Add(key);
            }
            list.Add(value);
        }
        foreach (string key in order)
        {
            List<string> list = values[key];
            if (list.Count == 1)
            {
                result[key] = list[0];
                continue;
            }
            var many = new DmList();
            foreach (string v in list) many.AppendItem(v);
            result[key] = many;
        }
        return result;
    }

    // list2params: "key=value" for items with an associated value, the bare key otherwise, joined by '&'.
    internal static string ListToParams(DmList list)
    {
        var sb = new StringBuilder();
        foreach (object item in list.CopyItems())
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(UrlEncode(DmTextRender.Plain(item)));
            if (list.TryGetAssociated(item, out object value)) sb.Append('=').Append(UrlEncode(DmTextRender.Plain(value)));
        }
        return sb.ToString();
    }

    private static string UrlDecode(string s) => System.Net.WebUtility.UrlDecode(s) ?? "";

    // HttpUtility.UrlEncode's form (OpenDream List2Params): letters, digits and -_.!*() as they are, space as '+',
    // every other UTF-8 byte as %xx in lower-case hex.
    private static string UrlEncode(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (byte b in Encoding.UTF8.GetBytes(s))
        {
            char c = (char)b;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '!' || c == '*' || c == '(' || c == ')')
                sb.Append(c);
            else if (c == ' ')
                sb.Append('+');
            else
                sb.Append('%').Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
