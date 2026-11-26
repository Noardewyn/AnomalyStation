using System.Linq;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// The dev console (D73, plan Q9): a text line at the bottom of the screen that sends dev commands to the server and a log
/// of the replies. The server takes the commands only when its <c>DEV_COMMANDS</c> is on (it says so in a reply when it is
/// not). The model (history, line checks, bounded logs) is <see cref="DevConsoleModel"/>; this is the widgets. While it is open
/// the keyboard is the line's: the avatar does not move. The log scrolls (the owner's play test, 2026-10-10): a scroll bar, the
/// mouse wheel, PageUp and PageDown over the whole bounded log; a new line keeps the view at the end unless it was scrolled up.
/// While a line is typed, the matching commands show under the log and the rest of the word greyed after the caret; Tab (or
/// Right at the end) takes it (<see cref="ConsoleCatalog"/>). The log and the line are in a monospace font, so the help's
/// columns line up.
/// </summary>
public partial class DevConsolePanel : PanelContainer
{
    private const int VisibleLines = 12;

    private DevConsoleModel _model;
    private KeyBindings _bindings;
    private ConsoleLineEdit _line;
    private WheelScrollContainer _scroll;
    private RichTextLabel _log;
    private RichTextLabel _suggestions;
    private ScrollContainer _suggestionScroll;
    private int _lineHeight;
    private int _shownVersion = -1;
    private int _shownHelp = -1;
    private bool _stickToEnd = true;
    private bool _forceEnd;
    private int _scrollToParagraph = -1;
    private WindowChrome _chrome;

    private static readonly string[] MonoNames = { "Menlo", "Monaco", "Consolas", "DejaVu Sans Mono", "Liberation Mono", "monospace" };
    private static readonly SystemFont Mono = new() { FontNames = MonoNames };
    private static readonly SystemFont MonoBold = new() { FontNames = MonoNames, FontWeight = 700 };

    /// <summary>A command line the player submitted (already trimmed and checked).</summary>
    public event Action<string> Submitted;

    /// <summary>The console is open and has the keyboard.</summary>
    public bool IsOpen => Visible;

    public void Bind(DevConsoleModel model, KeyBindings bindings, WindowChrome chrome)
    {
        _model = model;
        _bindings = bindings;
        _chrome = chrome;
        AddThemeStyleboxOverride("panel", PanelStyle.Dark());
        Name = "DevConsole";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        GrowVertical = GrowDirection.Begin; // the suggestions make it taller upwards
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(chrome.TitleBar("Dev console", Close)); // D282: closed, moved and resized as every window
        _scroll = new WheelScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
        };
        box.AddChild(_scroll);
        _scroll.GetVScrollBar().Changed += OnLogResized;
        _scroll.Resized += () => _log.CustomMinimumSize = new Vector2(0, _scroll.Size.Y); // a short log sits at the bottom
        _log = NewText();
        _log.VerticalAlignment = VerticalAlignment.Bottom;
        _log.SelectionEnabled = true;
        _scroll.AddChild(_log);
        _suggestions = NewText();
        _suggestions.MouseFilter = MouseFilterEnum.Ignore;
        _suggestionScroll = new WheelScrollContainer { Visible = false, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _suggestionScroll.AddChild(_suggestions);
        box.AddChild(_suggestionScroll);
        _line = new ConsoleLineEdit { PlaceholderText = "command: try help (Tab completes)", MaxLength = 190 };
        _line.TextSubmitted += OnSubmitted;
        _line.TextChanged += _ => UpdateSuggestions();
        _line.GuiInput += OnLineInput;
        box.AddChild(_line);
        box.AddChild(chrome.Grip());
        ApplyScale(DevUiScale.Default);
    }

    private static RichTextLabel NewText()
    {
        var text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        text.AddThemeColorOverride("default_color", new Color(0.85f, 0.95f, 0.85f));
        text.AddThemeFontOverride("normal_font", Mono);
        text.AddThemeFontOverride("bold_font", MonoBold);
        return text;
    }

    /// <summary>The console's size for a dev UI scale (<see cref="DevUiScale"/>): the M1 layout times the scale.</summary>
    public void ApplyScale(float scale)
    {
        int line = DevUiScale.Px(20, scale), margin = DevUiScale.Px(12, scale);
        _lineHeight = line;
        Vector2 screen = GetViewportRect().Size;
        float height = VisibleLines * line + DevUiScale.Px(62, scale) + 44; // the M1 layout, with the title bar and the grip
        _chrome.Place(new Vector2(margin, screen.Y - height - margin), new Vector2(screen.X - 2 * margin, height));
        _scroll.CustomMinimumSize = new Vector2(0, 3 * line);
        _scroll.GetVScrollBar().CustomMinimumSize = new Vector2(DevUiScale.Px(10, scale), 0); // wide enough to grab
        foreach (RichTextLabel text in new[] { _log, _suggestions })
        {
            text.AddThemeFontSizeOverride("normal_font_size", DevUiScale.Px(13, scale));
            text.AddThemeFontSizeOverride("bold_font_size", DevUiScale.Px(13, scale));
        }
        _line.AddThemeFontOverride("font", Mono);
        _line.AddThemeFontSizeOverride("font_size", DevUiScale.Px(14, scale));
        AddThemeStyleboxOverride("panel", PanelStyle.Dark(scale));
    }

    public void Open()
    {
        Visible = true;
        _shownVersion = -1;
        _forceEnd = true;
        Refresh();
        UpdateSuggestions();
        _line.GrabFocus();
    }

    public void Close()
    {
        Visible = false;
        _line.ReleaseFocus();
        _line.Clear();
        UpdateSuggestions();
    }

    public void Refresh()
    {
        if (_model == null || _model.Version == _shownVersion)
            return;
        _shownVersion = _model.Version;
        _stickToEnd = _forceEnd || IsScrolledToEnd();
        _forceEnd = false;
        if (_model.HelpStart != _shownHelp && _model.HelpStart >= 0 && _model.Log.Count - _model.HelpStart > VisibleLines)
            _scrollToParagraph = _model.HelpStart; // a new help longer than the view: its first line at the top
        _shownHelp = _model.HelpStart;
        _log.Text = string.Join('\n', _model.LogMarkup);
        Callable.From(OnLogResized).CallDeferred(); // also when the label keeps its size
    }

    /// <summary>The log's new size is laid out (the scroll bar's range changed): show a new help from its top, else keep the end in view unless scrolled up.</summary>
    private void OnLogResized()
    {
        if (_scrollToParagraph >= 0 && _scrollToParagraph < _log.GetParagraphCount())
        {
            _scroll.ScrollVertical = (int)(_log.Position.Y + _log.GetParagraphOffset(_scrollToParagraph));
            if (_log.Size.Y >= _log.GetContentHeight() - 1 && _scroll.GetVScrollBar().MaxValue >= _log.Size.Y - 1)
                _scrollToParagraph = -1; // laid out: done
            _stickToEnd = false;
        }
        else if (_stickToEnd)
            ScrollToEnd();
    }

    private bool IsScrolledToEnd()
    {
        VScrollBar bar = _scroll.GetVScrollBar();
        return _scroll.ScrollVertical >= bar.MaxValue - bar.Page - 2;
    }

    private void ScrollToEnd() => _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;

    private void ScrollBy(double pages) => _scroll.ScrollVertical += (int)(pages * _scroll.GetVScrollBar().Page);

    /// <summary>The suggestions under the log and the greyed completion after the caret, for the line as it is now.</summary>
    private void UpdateSuggestions()
    {
        string text = _line.Text;
        var lines = ConsoleCatalog.Suggestions(text);
        _suggestions.Text = string.Join('\n', lines.Select(l => l.Markup));
        _suggestionScroll.Visible = lines.Count > 0;
        // every line is there; past ConsoleCatalog.MaxSuggestions they scroll (the wheel over them)
        _suggestionScroll.CustomMinimumSize = new Vector2(0, Math.Min(lines.Count, ConsoleCatalog.MaxSuggestions) * _lineHeight);
        _suggestionScroll.ScrollVertical = 0;
        _line.Ghost = _line.CaretColumn == text.Length ? ConsoleCatalog.Completion(text) : string.Empty;
    }

    private void SetLine(string text)
    {
        _line.Text = text;
        _line.CaretColumn = text.Length;
        UpdateSuggestions();
    }

    private void OnSubmitted(string text)
    {
        _line.Clear();
        UpdateSuggestions();
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (!DevConsoleModel.TryPrepare(text, out string command, out string problem))
        {
            _model.AddLog("! " + problem);
            Refresh();
            return;
        }
        _forceEnd = true; // a sent command shows its answer
        Submitted?.Invoke(command);
        Refresh();
    }

    private void OnLineInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key)
            return;
        if (key.PhysicalKeycode == Key.Escape || _bindings.Has(PlayAction.Console, key.PhysicalKeycode.ToString()))
        {
            Close();
            _line.AcceptEvent();
        }
        else if (key.Keycode == Key.Tab || (key.Keycode == Key.Right && _line.CaretColumn == _line.Text.Length && _line.Ghost.Length > 0))
        {
            if (_line.CaretColumn == _line.Text.Length)
                SetLine(ConsoleCatalog.Complete(_line.Text));
            _line.AcceptEvent(); // Tab never moves the focus away
        }
        else if (key.Keycode == Key.Pageup || key.Keycode == Key.Pagedown)
        {
            ScrollBy(key.Keycode == Key.Pageup ? -0.8 : 0.8);
            _line.AcceptEvent();
        }
        else if (key.Keycode == Key.Up)
        {
            string text = _model.HistoryPrevious();
            if (text != null)
                SetLine(text);
            _line.AcceptEvent();
        }
        else if (key.Keycode == Key.Down)
        {
            string text = _model.HistoryNext();
            if (text != null)
                SetLine(text);
            _line.AcceptEvent();
        }
    }
}

/// <summary>
/// The console's line: a LineEdit that draws <see cref="Ghost"/>, the completion Tab would add, greyed after the text.
/// </summary>
public partial class ConsoleLineEdit : LineEdit
{
    private string _ghost = string.Empty;

    /// <summary>The greyed rest of the word at the caret (empty: none).</summary>
    public string Ghost
    {
        get => _ghost;
        set
        {
            value ??= string.Empty;
            if (value == _ghost)
                return;
            _ghost = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_ghost.Length == 0 || !HasFocus() || CaretColumn != Text.Length)
            return;
        Font font = GetThemeFont("font");
        int size = GetThemeFontSize("font_size");
        StyleBox style = GetThemeStylebox("normal");
        float x = style.GetMargin(Side.Left) + font.GetStringSize(Text, HorizontalAlignment.Left, -1, size).X;
        float room = Size.X - style.GetMargin(Side.Right) - x;
        if (room <= 0)
            return; // the text has scrolled: no place to show it
        float y = style.GetOffset().Y + (Size.Y - style.GetMinimumSize().Y - font.GetHeight(size)) / 2 + font.GetAscent(size);
        DrawString(font, new Vector2(x, y), _ghost, HorizontalAlignment.Left, room, size, new Color(1, 1, 1, 0.35f));
    }
}

/// <summary>The dark, nearly opaque background of the HUD's panels.</summary>
internal static class PanelStyle
{
    public static StyleBoxFlat Dark(float scale = 1f) => new()
    {
        BgColor = new Color(0.07f, 0.08f, 0.11f, 0.94f),
        BorderColor = new Color(0.35f, 0.4f, 0.5f, 0.9f),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = 8 * scale,
        ContentMarginRight = 8 * scale,
        ContentMarginTop = 6 * scale,
        ContentMarginBottom = 6 * scale,
    };
}

/// <summary>
/// The size of the dev UI (the console, the analyzer panel and the debug HUD's text): its own option, "Dev windows scale"
/// (<see cref="PlayOptions.DevUiScale"/>, 200 % of the M1 layout by default; the owner, 2026-10-09).
/// </summary>
internal static class DevUiScale
{
    /// <summary>The scale before the options are read (the option's default).</summary>
    public const float Default = 2f;

    /// <summary>An M1 pixel size at <paramref name="scale"/>, rounded, at least 1.</summary>
    public static int Px(float px, float scale) => Math.Max(1, (int)Math.Round(px * scale));
}
