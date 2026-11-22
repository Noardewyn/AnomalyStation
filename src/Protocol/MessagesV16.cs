using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Anomaly.Protocol;

// Protocol version 16 (M5 slice 3: tg's tgui windows drawn natively, D148; docs/ai/specs/protocol.md section 25; DV44). tg's window
// is a browser control running tgui: the server writes JSON messages into it ("update" with the config, ui_data and
// ui_static_data; "suspend"; "ping") and the page answers with Topic calls ("act/<action>" with a JSON payload, "close"). The
// server keeps tg's DM side whole (SStgui, /datum/tgui, the ui states, the pooled windows "tgui-window-N"); the client draws the
// window itself from the same data (the UiWindow message: an update or a close of window N) and answers with the UiAct and
// UiClose actions (MessagesV2.cs). The data travels as a UiValue tree, JSON's value model (tg json_encodes its payload; the
// server parses that JSON into the tree).

// The last action kind the codec takes is ActionKinds.Last (MessagesV17.cs since version 17).

/// <summary>The kinds of a <see cref="UiValue"/>: JSON's value model.</summary>
public enum UiValueKind : byte
{
    Null = 0,
    False = 1,
    True = 2,
    Number = 3,
    String = 4,
    Array = 5,
    Object = 6,
}

/// <summary>
/// One JSON value of a tgui payload (tg's <c>json_encode</c> of its DM lists, or the client's act params): null, a boolean, a number,
/// a string, an array, or an object whose fields keep their order. Immutable once built; built by <see cref="FromJson(string)"/>,
/// the factories, or the codec.
/// </summary>
public sealed class UiValue
{
    public static readonly UiValue Null = new(UiValueKind.Null);
    public static readonly UiValue True = new(UiValueKind.True);
    public static readonly UiValue False = new(UiValueKind.False);

    private static readonly IReadOnlyList<UiValue> NoItems = System.Array.Empty<UiValue>();
    private static readonly IReadOnlyList<KeyValuePair<string, UiValue>> NoFields = System.Array.Empty<KeyValuePair<string, UiValue>>();

    private readonly List<UiValue> _items;
    private readonly List<KeyValuePair<string, UiValue>> _fields;

    private UiValue(UiValueKind kind) => Kind = kind;

    private UiValue(double number)
    {
        Kind = UiValueKind.Number;
        Number = number;
    }

    private UiValue(string text)
    {
        Kind = UiValueKind.String;
        Text = text;
    }

    private UiValue(List<UiValue> items)
    {
        Kind = UiValueKind.Array;
        _items = items;
    }

    private UiValue(List<KeyValuePair<string, UiValue>> fields)
    {
        Kind = UiValueKind.Object;
        _fields = fields;
    }

    public UiValueKind Kind { get; }

    /// <summary>A number's value (0 for others).</summary>
    public double Number { get; }

    /// <summary>A string's text (null for others).</summary>
    public string Text { get; }

    /// <summary>An array's items (empty for others).</summary>
    public IReadOnlyList<UiValue> Items => (IReadOnlyList<UiValue>)_items ?? NoItems;

    /// <summary>An object's fields in order (empty for others).</summary>
    public IReadOnlyList<KeyValuePair<string, UiValue>> Fields => (IReadOnlyList<KeyValuePair<string, UiValue>>)_fields ?? NoFields;

    public bool IsNull => Kind == UiValueKind.Null;

    public static UiValue Of(double number) => double.IsFinite(number) ? new UiValue(number) : throw new ArgumentOutOfRangeException(nameof(number), "a UI number must be finite");

    public static UiValue Of(string text) => text == null ? Null : new UiValue(text);

    public static UiValue Of(bool value) => value ? True : False;

    public static UiValue ListOf(IEnumerable<UiValue> items) => new(items.Select(i => i ?? Null).ToList());

    public static UiValue ObjectOf(IEnumerable<KeyValuePair<string, UiValue>> fields) => new(fields.Select(f => new KeyValuePair<string, UiValue>(f.Key ?? "", f.Value ?? Null)).ToList());

    /// <summary>An object of (name, value) pairs; a null value is <see cref="Null"/>.</summary>
    public static UiValue ObjectOf(params (string Key, UiValue Value)[] fields) => ObjectOf(fields.Select(f => new KeyValuePair<string, UiValue>(f.Key, f.Value)));

    /// <summary>An object's field (the last of that name, as JSON.parse keeps), or null when there is none or this is no object.</summary>
    public UiValue Get(string key)
    {
        if (_fields == null)
            return null;
        for (int i = _fields.Count - 1; i >= 0; i--)
            if (string.Equals(_fields[i].Key, key, StringComparison.Ordinal))
                return _fields[i].Value;
        return null;
    }

    /// <summary>JavaScript's truthiness (the views test data the way tg's TSX does): false for null, false, 0, NaN and "".</summary>
    public bool Truthy => Kind switch
    {
        UiValueKind.Null or UiValueKind.False => false,
        UiValueKind.Number => Number != 0 && !double.IsNaN(Number),
        UiValueKind.String => Text.Length > 0,
        _ => true,
    };

    /// <summary>
    /// The value merged with <paramref name="newer"/> the way tgui's update handler merges payloads (<c>{...prev, ...payload}</c>):
    /// for two objects, this one's fields with the newer's fields set over them (a field keeps its first position); otherwise the newer.
    /// </summary>
    public UiValue MergedWith(UiValue newer)
    {
        if (newer == null)
            return this;
        if (Kind != UiValueKind.Object || newer.Kind != UiValueKind.Object)
            return newer;
        var fields = new List<KeyValuePair<string, UiValue>>(_fields);
        foreach (KeyValuePair<string, UiValue> f in newer._fields)
        {
            int at = fields.FindIndex(e => string.Equals(e.Key, f.Key, StringComparison.Ordinal));
            if (at >= 0) fields[at] = f;
            else fields.Add(f);
        }
        return new UiValue(fields);
    }

    /// <summary>Parses JSON text (tg's <c>json_encode</c> output). Throws <see cref="JsonException"/> on bad JSON.</summary>
    public static UiValue FromJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = Limits.MaxUiDepth + 2 });
        return FromJson(document.RootElement);
    }

    /// <summary>The tree of a parsed JSON element.</summary>
    public static UiValue FromJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => Null,
        JsonValueKind.True => True,
        JsonValueKind.False => False,
        JsonValueKind.Number => new UiValue(element.GetDouble()),
        JsonValueKind.String => new UiValue(element.GetString()),
        JsonValueKind.Array => new UiValue(element.EnumerateArray().Select(FromJson).ToList()),
        JsonValueKind.Object => new UiValue(element.EnumerateObject().Select(p => new KeyValuePair<string, UiValue>(p.Name, FromJson(p.Value))).ToList()),
        _ => Null,
    };

    /// <summary>The value as JSON text (numbers in invariant culture, the shortest round-trip form).</summary>
    public string ToJson()
    {
        var sb = new StringBuilder();
        AppendJson(sb);
        return sb.ToString();
    }

    private void AppendJson(StringBuilder sb)
    {
        switch (Kind)
        {
            case UiValueKind.Null: sb.Append("null"); break;
            case UiValueKind.True: sb.Append("true"); break;
            case UiValueKind.False: sb.Append("false"); break;
            case UiValueKind.Number: sb.Append(Number.ToString("R", CultureInfo.InvariantCulture)); break;
            case UiValueKind.String: sb.Append(JsonSerializer.Serialize(Text)); break;
            case UiValueKind.Array:
                sb.Append('[');
                for (int i = 0; i < _items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    _items[i].AppendJson(sb);
                }
                sb.Append(']');
                break;
            case UiValueKind.Object:
                sb.Append('{');
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(JsonSerializer.Serialize(_fields[i].Key)).Append(':');
                    _fields[i].Value.AppendJson(sb);
                }
                sb.Append('}');
                break;
        }
    }

    public override string ToString() => ToJson();

    // The codec builds values through these (MessagesV16 is the only place the lists are made).
    internal static UiValue WrapNumber(double number) => new(number);

    internal static UiValue WrapString(string text) => new(text);

    internal static UiValue WrapArray(List<UiValue> items) => new(items);

    internal static UiValue WrapObject(List<KeyValuePair<string, UiValue>> fields) => new(fields);
}

/// <summary>What a <see cref="UiWindowMessage"/> does.</summary>
public enum UiWindowKind : byte
{
    /// <summary>tg's <c>update</c> message: the window shows (or keeps showing) this interface with this config and data.</summary>
    Update = 0,

    /// <summary>The window is gone: tg's <c>suspend</c> message or <c>browse(null)</c> of it.</summary>
    Close = 1,
}

/// <summary>
/// Version 16, server to client on the State channel after the tick's Delta: one of the session's tgui windows (tg's pooled
/// <c>"tgui-window-N"</c>, <see cref="Window"/> = N) updated or closed. An update carries tg's payload: the config (the interface, the
/// title, the ui status, refreshing) and, when present, the data and the static data, which the client merges into what it has as tgui
/// does (shallow, newer fields over older); the first update of a window after a close starts it empty.
/// </summary>
public sealed class UiWindowMessage
{
    public UiWindowKind Kind { get; set; }

    /// <summary>The window's pool index (tg's <c>TGUI_WINDOW_INDEX</c>), 1 to <see cref="Limits.MaxUiWindows"/>.</summary>
    public int Window { get; set; }

    /// <summary>The interface's name (tg's <c>config.interface.name</c>: <c>"Canister"</c>, <c>"NumberInputModal"</c>, ...); non-empty for an update.</summary>
    public string Interface { get; set; }

    /// <summary>The window's title (tg's <c>config.title</c>); may be empty.</summary>
    public string Title { get; set; }

    /// <summary>tg's <c>config.status</c>: <c>UI_CLOSE</c> -1, <c>UI_DISABLED</c> 0, <c>UI_UPDATE</c> 1, <c>UI_INTERACTIVE</c> 2.</summary>
    public int Status { get; set; }

    /// <summary>tg's <c>config.refreshing</c> (a full update waits for its cooldown).</summary>
    public bool Refreshing { get; set; }

    /// <summary>tg's <c>data</c> (ui_data), an object, or null when the update has none.</summary>
    public UiValue Data { get; set; }

    /// <summary>tg's <c>static_data</c> (ui_static_data), an object, or null when the update has none.</summary>
    public UiValue StaticData { get; set; }
}
