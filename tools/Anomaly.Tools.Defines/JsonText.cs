using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>A small deterministic JSON writer: ordered keys, ASCII output, one object per call.</summary>
public sealed class JsonObject
{
    private readonly List<(string Key, string Json)> _fields = new();

    public JsonObject Add(string key, string value)
    {
        if (value != null)
            _fields.Add((key, JsonText.Quote(value)));
        return this;
    }

    public JsonObject Add(string key, int value)
    {
        _fields.Add((key, value.ToString(CultureInfo.InvariantCulture)));
        return this;
    }

    public JsonObject Add(string key, bool value)
    {
        _fields.Add((key, value ? "true" : "false"));
        return this;
    }

    public JsonObject Add(string key, IReadOnlyCollection<string> values)
    {
        if (values != null && values.Count > 0)
            _fields.Add((key, "[" + string.Join(",", values.Select(JsonText.Quote)) + "]"));
        return this;
    }

    public JsonObject AddValue(string key, object value)
    {
        switch (value)
        {
            case null:
                return this;
            case int i:
                return Add(key, i);
            case float f when float.IsFinite(f):
                _fields.Add((key, f.ToString("R", CultureInfo.InvariantCulture)));
                return this;
            case float f:
                return Add(key, f.ToString(CultureInfo.InvariantCulture));
            case string s:
                return Add(key, s);
        }
        throw new ArgumentException($"unsupported JSON value {value.GetType()}");
    }

    public JsonObject AddRaw(string key, string json)
    {
        _fields.Add((key, json));
        return this;
    }

    public override string ToString() => "{" + string.Join(",", _fields.Select(f => JsonText.Quote(f.Key) + ":" + f.Json)) + "}";
}

public static class JsonText
{
    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c > 0x7E)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>A JSON document with a header object and an array of entries, one entry per line.</summary>
    public static string Document(JsonObject header, string arrayKey, IEnumerable<JsonObject> entries)
    {
        var sb = new StringBuilder();
        string head = header.ToString();
        sb.Append(head, 0, head.Length - 1);
        if (head.Length > 2)
            sb.Append(',');
        sb.Append('\n');
        sb.Append(Quote(arrayKey)).Append(":[\n");
        bool first = true;
        foreach (JsonObject e in entries)
        {
            if (!first)
                sb.Append(",\n");
            first = false;
            sb.Append(e);
        }
        sb.Append("\n]}\n");
        return sb.ToString();
    }
}
