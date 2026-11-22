using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui;

// M5 slice 3 (D148): what a translated interface reads and calls, tgui's useBackend() and useState(). An interface is a function of
// a context to a UiNode tree; it reads the window's data (tg's static data with the data over it), calls act(action, params) (a UiAct
// to the server), and keeps local state (React's useState, kept per window while it is open). Window(...) is tgui's <Window width
// height title>: it sets the window's size and title.

/// <summary>A tgui interface translated to C#: tg's React component as a function of its context.</summary>
public delegate UiNode TguiInterface(TguiContext context);

/// <summary>What the window host keeps per open window besides the server's state: the local state of its interface.</summary>
public sealed class TguiLocalState
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    /// <summary>Counts the changes (the host redraws when it changes).</summary>
    public int Serial { get; private set; }

    public T Get<T>(string key, T initial) => _values.TryGetValue(key, out object v) && v is T t ? t : initial;

    public void Set<T>(string key, T value)
    {
        if (_values.TryGetValue(key, out object old) && Equals(old, value))
            return;
        _values[key] = value;
        Serial++;
    }

    /// <summary>Sets a value without asking for a redraw (a text field's text while it is typed: the field shows it already).</summary>
    public void SetQuiet<T>(string key, T value) => _values[key] = value;

    public void Clear()
    {
        _values.Clear();
        Serial++;
    }

    /// <summary>
    /// Syndicate X1a (D316): the clock time (<see cref="TguiClock"/>) the interface asked to be rendered again at, for what tgui does
    /// with timers (FakeTerminal's typed lines); infinity when it asked for nothing. Cleared at each render.
    /// </summary>
    public double WakeAt { get; set; } = double.PositiveInfinity;
}

/// <summary>Syndicate X1a (D316): the clock of the client's tgui timers, in seconds (engine-free; a test may set <see cref="Source"/>).</summary>
public static class TguiClock
{
    public static Func<double> Source { get; set; } = () => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

    public static double Seconds => Source();
}

/// <summary>An open window's context for its interface (tgui's <c>useBackend()</c> and <c>useState()</c>).</summary>
public sealed class TguiContext
{
    private readonly Action<string, UiValue> _act;

    /// <summary>The system clipboard's setter (tgui's <c>copyText</c>, document.execCommand('copy'): Emojipedia's emoji tag, gaps S4c); the
    /// engine sets it, the engine-free tests leave it null.</summary>
    public static Action<string> CopyText { get; set; }

    public TguiContext(int window, string @interface, string title, int status, UiValue data, Action<string, UiValue> act, TguiLocalState local)
    {
        WindowIndex = window;
        Interface = @interface;
        ConfigTitle = title ?? "";
        Status = status;
        Data = data ?? UiValue.ObjectOf();
        _act = act;
        Local = local ?? new TguiLocalState();
    }

    public int WindowIndex { get; }

    public string Interface { get; }

    /// <summary>tg's <c>config.title</c> (the window's title unless the interface's Window gives its own).</summary>
    public string ConfigTitle { get; }

    /// <summary>tg's <c>config.status</c>.</summary>
    public int Status { get; }

    /// <summary>The data the interface reads (tg's static data with the data over it).</summary>
    public UiValue Data { get; }

    public TguiLocalState Local { get; }

    // ---- what the interface's <Window> sets ----

    /// <summary>tgui's <c>Window width</c> in pixels (at 100 %).</summary>
    public double WindowWidth { get; private set; } = 400;

    public double WindowHeight { get; private set; } = 300;

    /// <summary>The title shown (the Window's own title, else the config's).</summary>
    public string WindowTitle { get; private set; }

    /// <summary>tgui's Window <c>theme</c> prop (NtosWindow: the device's theme, M9 slice E); null for tgui-core's default theme.</summary>
    public string WindowTheme { get; set; }

    /// <summary>Enter in the window (the modals' submit).</summary>
    public Action OnEnter { get; set; }

    /// <summary>Up (-1) or Down (+1) in the window (the list input's selection, tgui's onKeyDown of its section).</summary>
    public Action<int> OnArrow { get; set; }

    /// <summary>tgui's <c>&lt;Window width height title&gt;&lt;Window.Content&gt;...</c>: sets the size and title and returns the content.</summary>
    public UiNode Window(double width, double height, string title = null, params UiNode[] content)
    {
        WindowWidth = width;
        WindowHeight = height;
        WindowTitle = string.IsNullOrEmpty(title) ? ConfigTitle : title;
        return new UiNode(UiKind.Fragment).With(content);
    }

    /// <summary>M10x slice G6 (DV216): a click on a link of a browse() page, a tg chat link to the server's Topic.</summary>
    public Action<string> Topic { get; init; }

    /// <summary>tgui's <c>act(action, params)</c>: sent to the server's ui_act (a UiAct action).</summary>
    public void Act(string action, params (string Key, object Value)[] parameters)
    {
        UiValue p = parameters.Length == 0 ? null : UiValue.ObjectOf(parameters.Select(kv => new KeyValuePair<string, UiValue>(kv.Key, ToValue(kv.Value))));
        _act?.Invoke(action, p);
    }

    /// <summary>React's <c>useState(initial)</c>: the value kept under <paramref name="key"/> for this window.</summary>
    public T UseState<T>(string key, T initial) => Local.Get(key, initial);

    /// <summary>Syndicate X1a (D316): the clock's time now (tgui's timers, <see cref="TguiClock"/>).</summary>
    public double Now => TguiClock.Seconds;

    /// <summary>Syndicate X1a (D316): render the window again at <paramref name="seconds"/> (the clock's time), as a tgui timer would.</summary>
    public void RenderAgainAt(double seconds)
    {
        if (seconds < Local.WakeAt)
            Local.WakeAt = seconds;
    }

    public void SetState<T>(string key, T value) => Local.Set(key, value);

    /// <summary>
    /// tgui-core's <c>Button.Confirm</c> (the air alarm's Disconnect Sensor): the first click turns the button into its confirm look
    /// (<c>confirmContent</c> "Confirm?", <c>confirmColor</c> "bad", no icon), the second one runs its onClick. tgui-core forgets the
    /// first click when the button loses the focus; the kit's buttons take no focus, so it is forgotten by the second click.
    /// </summary>
    public UiNode ButtonConfirm(string key, UiNode button)
    {
        string stateKey = "confirm:" + key;
        bool clickedOnce = UseState(stateKey, false);
        Action click = button.OnClick;
        button.Key ??= key;
        if (clickedOnce)
        {
            button.Text = "Confirm?";
            button.Color = "bad";
            button.Icon = null;
            button.OnClick = () =>
            {
                SetState(stateKey, false);
                click?.Invoke();
            };
        }
        else
            button.OnClick = () => SetState(stateKey, true);
        return button;
    }

    /// <summary>A JavaScript value as a UiValue (null, bool, a number, a string, a UiValue).</summary>
    public static UiValue ToValue(object value) => value switch
    {
        null => UiValue.Null,
        UiValue v => v,
        bool b => UiValue.Of(b),
        string s => UiValue.Of(s),
        double d => UiValue.Of(d),
        float f => UiValue.Of(f),
        int i => UiValue.Of(i),
        long l => UiValue.Of(l),
        _ => UiValue.Of(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };
}

/// <summary>Reading a tgui payload the way the TSX does (<c>data.x</c> with its default, <c>!!x</c>, <c>x || []</c>).</summary>
public static class UiData
{
    /// <summary>A field, or null when absent (JavaScript's undefined).</summary>
    public static UiValue Field(this UiValue data, string key) => data?.Get(key);

    /// <summary>A number field (a bool is 0 or 1, text is parsed), or <paramref name="fallback"/> when absent or null.</summary>
    public static double Num(this UiValue data, string key, double fallback = 0) => AsNum(data?.Get(key), fallback);

    public static double AsNum(this UiValue v, double fallback = 0) => v?.Kind switch
    {
        UiValueKind.Number => v.Number,
        UiValueKind.True => 1,
        UiValueKind.False => 0,
        UiValueKind.String => double.TryParse(v.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback,
        _ => fallback,
    };

    /// <summary>A text field (a number in its JavaScript form), or <paramref name="fallback"/> when absent or null.</summary>
    public static string Str(this UiValue data, string key, string fallback = "") => AsStr(data?.Get(key), fallback);

    public static string AsStr(this UiValue v, string fallback = "") => v?.Kind switch
    {
        UiValueKind.String => v.Text,
        UiValueKind.Number => TguiFormat.JsNumber(v.Number),
        UiValueKind.True => "true",
        UiValueKind.False => "false",
        _ => fallback,
    };

    /// <summary>JavaScript's <c>!!data.key</c>.</summary>
    public static bool Truthy(this UiValue data, string key) => data?.Get(key)?.Truthy ?? false;

    /// <summary>An array field's items (empty when absent or no array: the TSX's <c>= []</c> defaults).</summary>
    public static IReadOnlyList<UiValue> Arr(this UiValue data, string key) => data?.Get(key) is { Kind: UiValueKind.Array } a ? a.Items : Array.Empty<UiValue>();

    /// <summary>An object field's fields (empty when absent or no object: the TSX's <c>= {}</c> defaults).</summary>
    public static IReadOnlyList<KeyValuePair<string, UiValue>> Obj(this UiValue data, string key) => data?.Get(key) is { Kind: UiValueKind.Object } o ? o.Fields : Array.Empty<KeyValuePair<string, UiValue>>();
}
