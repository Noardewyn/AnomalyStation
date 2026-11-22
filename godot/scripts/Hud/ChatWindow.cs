using System.Linq;
using Godot;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// M9 slice C (D247, D259, chat.md 3): the big chat window, tg's chat panel (tgui-panel: its tabs with their unread counts, the log in tg's
/// dark chat colours with the "xN" badge of a repeated line, "Scroll to bottom" while scrolled up) drawn by the client over
/// <see cref="ChatLog"/>, with the action row (Say, Whisper, Shout, Radio, Me, Emotes, OOC) and the input row (<see cref="ChatInputModel"/>:
/// the channel button, the text, Send). Its title bar moves it, any edge or corner resizes it; it follows the windows' scale. The log's text
/// can be selected and copied; tg's links in it are clicked as in the feed.
/// </summary>
public sealed partial class ChatWindow : Control
{
    public static readonly Vector2 MinSize = new(360, 240);
    private const float Edge = 6f;
    private const int LogFontSize = 13; // tg's chat font size (settings/atoms.ts)

    private static readonly string[] ActionChannels = { SayChannels.Say, ChatInputModel.Whisper, ChatInputModel.Shout, SayChannels.Radio, SayChannels.Me, EmotesButton, SayChannels.Ooc, MessageButton };
    private const string EmotesButton = "Emotes";
    private const string MessageButton = "Message"; // M9 slice E (D251): the PDA's messenger

    // M9 slice E: the Radio list (chat.md 3): Common and the worn headset's departments, by their tg radio prefixes (protocol 40)
    private IReadOnlyList<string> _radioPrefixes = Array.Empty<string>();
    private PopupMenu _radioMenu;

    private ChatLog _log;
    private ChatInputModel _input;
    private Panel _frame;
    private Control _bar, _logBox;

    /// <summary>The owner (2026-10-29): how opaque the window's backgrounds are (PlayOptions.WindowOpacity); the text stays solid.</summary>
    public float Opacity { get; set; } = 1f;
    private HBoxContainer _tabs;
    private RichTextLabel _text;
    private Button _scrollButton;
    private readonly Dictionary<string, Button> _actionButtons = new(StringComparer.Ordinal);
    private Button _channel;
    private InputTextEdit _edit;
    private float _scale = 1f;
    private Vector2? _position; // the top left's share of the screen: it keeps it when the game's window is resized
    private Vector2? _size; // at scale 1; null: DefaultShare's
    private int _generation = -1;
    private readonly List<int> _paragraphs = new(); // the paragraph each drawn visible line starts at
    private int _drawnParagraphs;
    private string _tabsKey;
    private bool _syncing;
    private bool _moving;
    private int _resizing; // the edges held: 1 left, 2 right, 4 top, 8 bottom

    /// <summary>The clock (seconds).</summary>
    public Func<double> Now { get; set; } = () => Time.GetTicksMsec() / 1000.0;

    /// <summary>The window was moved or resized: its top left as its share of the screen and its size at scale 1 (the scene keeps them in the options).</summary>
    public event Action<Vector2, Vector2> GeometryChanged;

    /// <summary>The Emotes button: tg's emote panel.</summary>
    public event Action EmotesRequested;

    /// <summary>M9 slice E (D251): the Message button: the carried PDA's messenger.</summary>
    public event Action MessengerRequested;

    /// <summary>M9 slice E: the radio prefixes of the worn headset's channels (the server's RadioChannels), listed under the Radio button.</summary>
    public void SetRadioPrefixes(IReadOnlyList<string> prefixes) => _radioPrefixes = prefixes ?? Array.Empty<string>();

    /// <summary>A tg link in the log was clicked: its href.</summary>
    public event Action<string> LinkClicked;

    /// <summary>The window was closed by its close button.</summary>
    public event Action CloseRequested;

    /// <summary>D309: the microphone button went down (true) or up (false): push to talk.</summary>
    public event Action<bool> MicHeld;

    public bool IsOpen => Visible;

    /// <summary>The input row has the keyboard: the game gets no keys.</summary>
    public bool Typing => Visible && _edit.HasFocus();

    public override void _Ready()
    {
        Name = "ChatWindow";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _frame = new Panel { MouseFilter = MouseFilterEnum.Stop };
        StyleBoxFlat bg = TguiRender.Flat(TguiTheme.WindowBg, 0);
        bg.SetBorderWidthAll(1);
        bg.BorderColor = new Color(0, 0, 0, 0.6f);
        bg.ShadowColor = new Color(0, 0, 0, 0.45f);
        bg.ShadowSize = 6;
        _frame.AddThemeStyleboxOverride("panel", bg);
        _frame.GuiInput += OnFrameInput;
        AddChild(_frame);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        column.OffsetLeft = column.OffsetTop = Edge - 2;
        column.OffsetRight = column.OffsetBottom = -(Edge - 2);
        _frame.AddChild(column);

        // tgui's title bar: the title and the close button; dragging it moves the window.
        var bar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move, CustomMinimumSize = new Vector2(0, (float)TguiWindows.TitleBarHeight) };
        bar.AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.TitleBarBg, 0));
        bar.GuiInput += OnTitleInput;
        _bar = bar;
        var titleRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        Label title = TguiRender.Text("  Chat", null, false, 14, null);
        title.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = new Button { Text = "✕", FocusMode = FocusModeEnum.None, Flat = true, CustomMinimumSize = new Vector2(32, 0), TooltipText = "Close" };
        close.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        close.AddThemeColorOverride("font_hover_color", TguiTheme.Hex("#ff6060"));
        TguiRender.BareCloseButton(close);
        close.Pressed += () => CloseRequested?.Invoke();
        titleRow.AddChild(close);
        bar.AddChild(titleRow);
        column.AddChild(bar);

        // tg's ChatTabs
        _tabs = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 2);
        column.AddChild(_tabs);

        // tg's ChatPanel: the log, and "Scroll to bottom" while it is scrolled up
        var logBox = new Panel { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        logBox.AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.SectionBg, 0));
        column.AddChild(logBox);
        _logBox = logBox;
        _text = new RichTextLabel
        {
            ScrollFollowing = true,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            FocusMode = FocusModeEnum.Click,
            BbcodeEnabled = false,
        };
        _text.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _text.OffsetLeft = _text.OffsetTop = 4;
        _text.OffsetRight = _text.OffsetBottom = -2;
        ChatText.Style(_text, LogFontSize);
        _text.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _text.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _text.MetaClicked += meta => LinkClicked?.Invoke(meta.AsString());
        logBox.AddChild(_text);
        _scrollButton = MakeButton("↓ Scroll to bottom", false);
        _scrollButton.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _scrollButton.GrowHorizontal = GrowDirection.Begin;
        _scrollButton.GrowVertical = GrowDirection.Begin;
        _scrollButton.OffsetRight = -24;
        _scrollButton.OffsetBottom = -12;
        _scrollButton.Pressed += ScrollToBottom;
        _scrollButton.Visible = false;
        logBox.AddChild(_scrollButton);

        // The action row: a click picks the channel and puts the caret in the input (chat.md 3).
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", 2);
        actions.AddThemeConstantOverride("v_separation", 2);
        foreach (string channel in ActionChannels)
        {
            Button button = MakeButton(channel == SayChannels.Radio ? channel + " ▾" : channel, false);
            string picked = channel;
            if (channel == SayChannels.Radio)
                button.Pressed += () => OpenRadioMenu(button);
            else
                button.Pressed += () => OnAction(picked);
            actions.AddChild(button);
            _actionButtons[channel] = button;
        }
        _radioMenu = new PopupMenu();
        _radioMenu.IdPressed += id =>
        {
            if (id == 0)
                OnAction(SayChannels.Radio); // Common: tg's ";" radio channel
            else if (id - 1 < _radioPrefixes.Count)
            {
                _input?.SetRadioPrefix(_radioPrefixes[(int)id - 1]);
                _edit.GrabFocus();
                SyncInput();
            }
        };
        AddChild(_radioMenu);
        column.AddChild(actions);

        // The input row: the channel button, the text, Send.
        var inputRow = new HBoxContainer();
        inputRow.AddThemeConstantOverride("separation", 4);
        _channel = MakeButton(SayChannels.Say, false);
        _channel.CustomMinimumSize = new Vector2(64, 0);
        _channel.ClipText = true;
        _channel.Pressed += () =>
        {
            _input?.IncrementChannel();
            _edit.GrabFocus();
            SyncInput();
        };
        inputRow.AddChild(_channel);
        _edit = new InputTextEdit
        {
            Owner_ = this,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            ScrollFitContentHeight = true,
            ContextMenuEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 24),
            PlaceholderText = "Type here; Enter sends, Tab or Esc returns to the game, Shift+Tab: channel",
        };
        StyleBoxFlat field = TguiRender.Flat(TguiTheme.InputBg, 2);
        field.SetBorderWidthAll(1);
        field.BorderColor = TguiTheme.InputBorder.Darkened(0.35f);
        field.ContentMarginLeft = field.ContentMarginRight = 4;
        StyleBoxFlat focus = (StyleBoxFlat)field.Duplicate();
        focus.BorderColor = TguiTheme.InputBorder;
        _edit.AddThemeStyleboxOverride("normal", field);
        _edit.AddThemeStyleboxOverride("focus", focus);
        _edit.AddThemeColorOverride("font_color", TguiTheme.Text);
        _edit.AddThemeFontSizeOverride("font_size", LogFontSize);
        _edit.TextChanged += OnTextChanged;
        _edit.FocusEntered += () => _input?.Focus();
        _edit.FocusExited += () => _input?.Blur();
        inputRow.AddChild(_edit);
        Button mic = MakeButton("Mic", false);
        mic.TooltipText = "Hold and speak: your words go into the input (speech to text; Alt does it too while typing)";
        mic.ButtonDown += () => MicHeld?.Invoke(true);
        mic.ButtonUp += () => MicHeld?.Invoke(false);
        inputRow.AddChild(mic);
        Button send = MakeButton("Send", false);
        send.Pressed += () =>
        {
            _input?.Enter();
            SyncInput();
        };
        inputRow.AddChild(send);
        column.AddChild(inputRow);

        Visible = false;
    }

    /// <summary>The place and size, as shares of the screen, of a window with no saved place or size.</summary>
    public Rect2 DefaultShare { get; set; } = new(0.01f, 0.4f, 0.3f, 0.35f);

    /// <summary>Binds the log and the input.</summary>
    public void Bind(ChatLog log, ChatInputModel input)
    {
        _log = log;
        _input = input;
    }

    /// <summary>Puts the window at its saved place (the top left's share of the screen) and size (at scale 1); null: <see cref="DefaultShare"/>'s.</summary>
    public void SetGeometry(Vector2? position, Vector2? size)
    {
        _position = position;
        _size = size is Vector2 s ? new Vector2(Math.Max(MinSize.X, s.X), Math.Max(MinSize.Y, s.Y)) : null;
    }

    public void Open()
    {
        Visible = true;
        _generation = -1; // drawn afresh
    }

    public void Close()
    {
        if (_edit.HasFocus())
            _edit.ReleaseFocus();
        Visible = false;
    }

    /// <summary>Puts the caret in the input.</summary>
    public void FocusInput() => _edit.GrabFocus();

    /// <summary>D307: speech to text's words after what is typed, for the player to send or fix.</summary>
    public void AppendInput(string text)
    {
        if (_input == null || string.IsNullOrWhiteSpace(text))
            return;
        _input.Input(_input.Value.Length == 0 ? text : _input.Value.TrimEnd() + " " + text, Now());
        SyncInput();
        _edit.GrabFocus();
        _edit.SetCaretLine(_edit.GetLineCount() - 1);
        _edit.SetCaretColumn(_edit.GetLine(_edit.GetLineCount() - 1).Length);
    }

    /// <summary>Esc: the input lets the keyboard go (true), or nothing had it (false: the scene closes the window).</summary>
    public bool LeaveInput()
    {
        if (!_edit.HasFocus())
            return false;
        _edit.ReleaseFocus();
        return true;
    }

    /// <summary>Per frame: the place and scale, the tabs, the log's new lines and its scroll state.</summary>
    public void Update(float scale)
    {
        if (_log == null)
            return;
        _input?.Advance(Now());
        _log.Advance(Now());
        if (!Visible)
            return;
        _scale = Math.Max(0.5f, scale);
        _frame.SelfModulate = _bar.SelfModulate = _logBox.SelfModulate = new Color(1, 1, 1, Opacity);
        if (s_buttonOpacity != Opacity)
        {
            FadeButtons(_frame, Opacity / s_buttonOpacity);
            s_buttonOpacity = Opacity;
        }
        Place();
        SyncTabs();
        SyncLog();
        bool tracking = IsAtBottom();
        _log.SetScrollTracking(tracking);
        _scrollButton.Visible = !tracking;
        SyncInput();
    }

    private Vector2 Size1 => _size ?? new Vector2(Math.Max(MinSize.X, DefaultShare.Size.X * GetViewportRect().Size.X / _scale), Math.Max(MinSize.Y, DefaultShare.Size.Y * GetViewportRect().Size.Y / _scale));

    private void Place()
    {
        Vector2 screen = GetViewportRect().Size;
        Vector2 size = Size1;
        Vector2 shown = size * _scale;
        Vector2 at = (_position ?? DefaultShare.Position) * screen;
        at = new Vector2(Math.Clamp(at.X, 0, Math.Max(0, screen.X - shown.X)), Math.Clamp(at.Y, 0, Math.Max(0, screen.Y - shown.Y)));
        _frame.Scale = new Vector2(_scale, _scale);
        _frame.Position = at;
        _frame.Size = size;
    }

    // tg's ChatTabs: a tab per page, its unread count (up to 99) unless it is muted.
    private void SyncTabs()
    {
        string key = string.Join("|", _log.Pages.Select(p => p.Name + ":" + (p == _log.Current) + ":" + (p.HideUnreadCount ? 0 : p.UnreadCount)));
        if (key == _tabsKey)
            return;
        _tabsKey = key;
        foreach (Node child in _tabs.GetChildren())
            child.QueueFree();
        foreach (ChatPage page in _log.Pages)
        {
            string text = page.Name + (!page.HideUnreadCount && page.UnreadCount > 0 ? "  " + Math.Min(page.UnreadCount, 99) : "");
            Button tab = MakeButton(text, page == _log.Current, transparent: true);
            ChatPage picked = page;
            tab.Pressed += () =>
            {
                _log.ChangePage(picked);
                _tabsKey = null;
            };
            _tabs.AddChild(tab);
        }
    }

    // The log's new and re-counted lines (tg's processBatch), or all of it after a page change, a prune or a clear.
    private void SyncLog()
    {
        int from = _log.TakeChanges();
        if (_generation != _log.Generation)
        {
            _generation = _log.Generation;
            from = 0;
        }
        IReadOnlyList<ChatEntry> visible = _log.Visible;
        from = Math.Min(from, _paragraphs.Count);
        if (from >= visible.Count && from >= _paragraphs.Count)
            return;
        if (from == 0)
        {
            _text.Clear();
            _drawnParagraphs = 0;
        }
        else if (from < _paragraphs.Count)
        {
            // a re-counted line (tg's badge): it and the lines after it are drawn again
            for (int p = _text.GetParagraphCount() - 1; p >= _paragraphs[from]; p--)
                _text.RemoveParagraph(p);
            _drawnParagraphs = _paragraphs[from];
        }
        _paragraphs.RemoveRange(from, _paragraphs.Count - from);
        for (int i = from; i < visible.Count; i++)
            DrawEntry(visible[i]);
    }

    // One line: its text in its styles and links, then tg's badge when it was repeated.
    private void DrawEntry(ChatEntry entry)
    {
        if (_drawnParagraphs > 0)
            _text.Newline();
        _paragraphs.Add(_drawnParagraphs);
        string text = entry.Plain;
        ChatText.Append(_text, text, entry.Spans, entry.Links, LogFontSize);
        if (entry.Times > 1)
        {
            // tg's Chat__badge: "x" and the count, white on red
            _text.AddText(" ");
            _text.PushBgcolor(ToColor(ChatStyles.Hsl(348, 83, 47)));
            _text.PushColor(Colors.White);
            _text.PushFontSize((int)MathF.Round(LogFontSize * 0.7f));
            _text.AddText(" x" + entry.Times + " ");
            _text.Pop();
            _text.Pop();
            _text.Pop();
        }
        _drawnParagraphs += text.Count(c => c == '\n') + 1;
    }

    // tg's SCROLL_TRACKING_TOLERANCE: within 24 px of the end counts as at the end.
    private bool IsAtBottom()
    {
        VScrollBar bar = _text.GetVScrollBar();
        return bar.MaxValue - bar.Page - bar.Value < 24;
    }

    private void ScrollToBottom()
    {
        VScrollBar bar = _text.GetVScrollBar();
        bar.Value = bar.MaxValue;
        _text.ScrollFollowing = true;
    }

    // The Radio list under its button: Common, then the headset's departments by tg's say window labels (SayModel.RadioPrefixes).
    private void OpenRadioMenu(Control under)
    {
        _radioMenu.Clear();
        _radioMenu.AddItem("Common", 0);
        for (int i = 0; i < _radioPrefixes.Count; i++)
        {
            string prefix = _radioPrefixes[i];
            _radioMenu.AddItem(SayModel.RadioPrefixes.TryGetValue(prefix, out string label) ? label + " (" + prefix.Trim() + ")" : prefix.Trim(), i + 1);
        }
        Rect2 rect = under.GetGlobalRect();
        _radioMenu.Popup(new Rect2I((int)rect.Position.X, (int)rect.End.Y, 0, 0));
    }

    private void OnAction(string channel)
    {
        if (channel == EmotesButton)
        {
            EmotesRequested?.Invoke();
            return;
        }
        if (channel == MessageButton)
        {
            MessengerRequested?.Invoke();
            return;
        }
        _input?.SetChannel(channel);
        _edit.GrabFocus();
        SyncInput();
    }

    // The input model's state into the controls.
    private void SyncInput()
    {
        if (_input == null)
            return;
        foreach ((string channel, Button button) in _actionButtons)
            button.Disabled = !_input.Allowed(channel); // M10x slice A: in the lobby only OOC and Pray reach anyone
        if (_edit.Text != _input.Value)
        {
            _syncing = true;
            _edit.Text = _input.Value;
            _edit.SetCaretLine(_edit.GetLineCount() - 1);
            _edit.SetCaretColumn(_edit.GetLine(_edit.GetLineCount() - 1).Length);
            _syncing = false;
        }
        if (_channel.Text != _input.ButtonContent)
        {
            _channel.Text = _input.ButtonContent;
            (float r, float g, float b) = SayColours.Of(_input.Theme);
            var colour = new Color(r, g, b);
            foreach (string name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                _channel.AddThemeColorOverride(name, colour);
        }
    }

    private void OnTextChanged()
    {
        if (_syncing || _input == null)
            return;
        // Enter sends: a pasted line break is a space, as in tg's say window
        _input.Input(_edit.Text.Replace("\r", "").Replace('\n', ' '), Now());
        SyncInput();
    }

    // tg's say window keys in the input, before the text field's own handling.
    private bool Key(InputEventKey key)
    {
        if (_input == null)
            return false;
        switch (key.Keycode)
        {
            case Godot.Key.Up:
            case Godot.Key.Down:
                _input.Arrow(key.Keycode == Godot.Key.Up);
                SyncInput();
                return true;
            case Godot.Key.Delete:
            case Godot.Key.Backspace:
                _input.BackspaceDelete();
                return false;
            case Godot.Key.Enter:
            case Godot.Key.KpEnter:
                _input.Enter();
                SyncInput();
                // the owner (2026-10-29, revising 2026-10-26): sent, the input keeps the keys for the next message; Tab, Esc or a click outside
                // goes back to the game (D292)
                return true;
            case Godot.Key.Tab: // Shift+Tab (D292, DV224): the game takes a plain Tab first (back to the game) unless cursor mode is bound elsewhere
                _input.IncrementChannel();
                SyncInput();
                return true;
            case Godot.Key.Escape:
                _edit.ReleaseFocus();
                return true;
        }
        return false;
    }

    private void OnTitleInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } press:
                _moving = press.Pressed;
                if (!press.Pressed)
                    GeometryChanged?.Invoke(_frame.Position / GetViewportRect().Size, Size1);
                break;
            case InputEventMouseMotion motion when _moving:
                _position = (_frame.Position + motion.Relative * _scale) / GetViewportRect().Size;
                Place();
                break;
        }
    }

    // Any edge or corner resizes the window (the owner, 2026-10-20: the game's windows can be resized).
    private void OnFrameInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } press:
                _resizing = press.Pressed ? EdgesAt(press.Position) : 0;
                if (!press.Pressed)
                    GeometryChanged?.Invoke(_frame.Position / GetViewportRect().Size, Size1);
                break;
            case InputEventMouseMotion motion when _resizing != 0:
                Vector2 d = motion.Relative;
                Vector2 at = _frame.Position;
                Vector2 size = Size1;
                if ((_resizing & 2) != 0) size.X += d.X;
                if ((_resizing & 8) != 0) size.Y += d.Y;
                if ((_resizing & 1) != 0)
                {
                    float w = Math.Max(MinSize.X, size.X - d.X);
                    at.X += (size.X - w) * _scale;
                    size.X = w;
                }
                if ((_resizing & 4) != 0)
                {
                    float h = Math.Max(MinSize.Y, size.Y - d.Y);
                    at.Y += (size.Y - h) * _scale;
                    size.Y = h;
                }
                _size = new Vector2(Math.Max(MinSize.X, size.X), Math.Max(MinSize.Y, size.Y));
                _position = at / GetViewportRect().Size;
                Place();
                break;
            case InputEventMouseMotion hover:
                _frame.MouseDefaultCursorShape = EdgesAt(hover.Position) switch
                {
                    1 or 2 => CursorShape.Hsize,
                    4 or 8 => CursorShape.Vsize,
                    5 or 10 => CursorShape.Fdiagsize,
                    6 or 9 => CursorShape.Bdiagsize,
                    _ => CursorShape.Arrow,
                };
                break;
        }
    }

    private int EdgesAt(Vector2 local)
    {
        int edges = 0;
        if (local.X < Edge) edges |= 1;
        else if (local.X > Size1.X - Edge) edges |= 2;
        if (local.Y < Edge) edges |= 4;
        else if (local.Y > Size1.Y - Edge) edges |= 8;
        return edges;
    }

    private static float s_buttonOpacity = 1f; // what the buttons' backgrounds are faded to (Opacity), for the ones built later too

    private static void FadeButtons(Node node, float by)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Button button)
            {
                foreach (string state in new[] { "normal", "hover", "pressed" })
                {
                    if (button.GetThemeStylebox(state) is StyleBoxFlat flat && button.HasThemeStyleboxOverride(state))
                        flat.BgColor = flat.BgColor with { A = flat.BgColor.A * by };
                }
            }
            FadeButtons(child, by);
        }
    }

    // tgui's Button: blue, green when selected; a tab is transparent until selected.
    private static Button MakeButton(string text, bool selected, bool transparent = false)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        Color bg = transparent && !selected ? new Color(0, 0, 0, 0) : transparent ? new Color(1, 1, 1, 0.12f) : TguiTheme.ButtonColor(null, selected);
        StyleBoxFlat normal = TguiRender.Flat(bg, 2);
        normal.ContentMarginLeft = normal.ContentMarginRight = 6;
        normal.ContentMarginTop = normal.ContentMarginBottom = 1;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = transparent ? new Color(1, 1, 1, 0.18f) : bg.Lightened(0.15f);
        StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = transparent ? new Color(1, 1, 1, 0.12f) : bg.Darkened(0.1f);
        foreach (StyleBoxFlat style in new[] { normal, hover, pressed })
            style.BgColor = style.BgColor with { A = style.BgColor.A * s_buttonOpacity };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        Color colour = transparent && !selected ? TguiTheme.TransparentText : TguiTheme.Text;
        button.AddThemeColorOverride("font_color", colour);
        button.AddThemeColorOverride("font_hover_color", TguiTheme.Text);
        button.AddThemeColorOverride("font_pressed_color", colour);
        button.AddThemeFontSizeOverride("font_size", TguiRender.FontSize);
        button.CustomMinimumSize = new Vector2(0, 20);
        return button;
    }

    private static Color ToColor(uint rgba) => ChatText.ToColor(rgba);

    /// <summary>The input: tg's say keys go to the window before the field types them.</summary>
    private sealed partial class InputTextEdit : TextEdit
    {
        public ChatWindow Owner_;

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true } key && Owner_.Key(key))
                AcceptEvent();
        }
    }
}
