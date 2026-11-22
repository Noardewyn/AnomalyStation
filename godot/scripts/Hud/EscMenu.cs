using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// D282: the menu Esc opens and closes in game (not in the lobby, D283): tg's escape menu (<c>modules/escape_menu/escape_menu.dm</c>, the
/// tgui-escape-menu page) in the port's own look with tg's fonts (Pixellari, Grand9K Pixel) and sounds (<c>sound/misc/escape_menu</c>,
/// audio.ts). Its data is the server's escape menu window (<see cref="Limits.EscapeMenuUiWindow"/>: tg's "init" and "state"); its
/// buttons send that page's actions. It dims the screen and holds the pointer while open; the game behind it gets no keys or clicks.
/// Settings is the client's own window (D282); Quit asks first, as tg's Quit page does, and quits the client. Bug Report (D282,
/// DV206, protocol 53) is the port's own: a title, what happened and the steps, with the frame captured when the menu opened or an
/// image from disk, sent as WebP; the server keeps one report per key every 2 minutes.
/// </summary>
public partial class EscMenu : Control
{
    /// <summary>An action of tg's page (<c>sendAction</c>) to the server's escape menu window.</summary>
    public Action<string, UiValue> Act;
    public Action Settings;
    public Action Quit;
    /// <summary>What the Quit row and its page say in place of "Quit" (the map editor's Play: the row goes back to the editor); null: "Quit".</summary>
    public string QuitText;
    /// <summary>DV364: tg's Vote verb (the OOC verb tab the port has no panel for): opens the vote window.</summary>
    public Action Vote;
    /// <summary>The windows' scale (the Settings' windows scale and the screen's height), read while open so a change shows at once.</summary>
    public Func<float> WindowScale;
    /// <summary>Sends a bug report (<c>PlaySession.BugReport</c>); false when nothing was sent.</summary>
    public Func<BugReportMessage, bool> SendBugReport;
    /// <summary>The client's lines of a bug report: its version, OS, GPU, preset and window.</summary>
    public Func<string> ClientInfo;

    private Func<string, bool, AudioStream> _streams;
    private AudioStreamPlayer _oneShot;
    private Font _titleFont;
    private Font _textFont;
    private PanelContainer _panel;
    private Label _station;
    private VBoxContainer _page;
    private Label _details;
    private UiValue _data = UiValue.ObjectOf();
    private string _pageName = "home";
    private VBoxContainer _bugForm;
    private LineEdit _bugTitle;
    private TextEdit _bugWhat;
    private TextEdit _bugSteps;
    private CheckBox _bugAttach;
    private TextureRect _bugThumb;
    private Label _bugStatus;
    private Image _screenshot;
    private ulong _nextReportMsec;
    private bool _formOnly;

    public bool IsOpen => Visible;

    /// <param name="streams">tg's sound path to its stream (a looping copy when asked), or null.</param>
    public void Build(Func<string, bool, AudioStream> streams)
    {
        Name = "EscMenu";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop; // the clicks around the panel are the menu's, not the world's
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _streams = streams;
        _titleFont = MenuStyle.TitleFont;
        _textFont = MenuStyle.TextFont;
        // audio.ts: the one-shots at 0.6 (DEVIATION(DV208): tg's esc_middle ambient loop is not played, the dim is darker than tg's)
        _oneShot = new AudioStreamPlayer { Bus = Audio.SoundPlayer.UiBus, VolumeDb = Mathf.LinearToDb(0.6f) };
        AddChild(_oneShot);

        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.85f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        // not in a CenterContainer: a container resets its children's scale (_Process centres it)
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle.Dark(2f));
        AddChild(_panel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(stack);
        stack.AddChild(Text("Another day on...", _textFont, 17, new Color(0.7f, 0.7f, 0.7f)));
        _station = Text("", _titleFont, 38, Colors.White);
        _station.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_station);
        _page = new VBoxContainer { CustomMinimumSize = new Vector2(0, 380) }; // the home page's height: the panel keeps its place between pages
        _page.AddThemeConstantOverride("separation", 4);
        stack.AddChild(_page);
        _details = Text("", _textFont, 15, new Color(0.65f, 0.65f, 0.65f));
        stack.AddChild(_details);
        BuildBugForm();
        ShowPage("home");
    }

    /// <summary>The server's escape menu data changed (the window's merged state).</summary>
    public void Show(UiValue data)
    {
        _data = data ?? UiValue.ObjectOf();
        if (Visible && _pageName != "bug_report") // the form keeps its focus and caret
            ShowPage(_pageName);
    }

    public override void _Notification(int what)
    {
        // the bug form waits off the tree between visits (ShowPage keeps its draft): nothing else frees it with the menu
        if (what == NotificationPredelete && _bugForm != null && _bugForm.GetParent() == null)
            _bugForm.Free();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        _panel.Size = _panel.GetCombinedMinimumSize(); // hug the page (a page that was taller leaves its size behind)
        // the windows scale, but never past the screen (a long station name, the bug report form)
        float fit = Mathf.Min(Size.X / Mathf.Max(_panel.Size.X, 1f), Size.Y / Mathf.Max(_panel.Size.Y, 1f)) * 0.96f;
        _panel.Scale = Vector2.One * Mathf.Min(WindowScale?.Invoke() ?? 1f, fit);
        _panel.Position = ((Size - _panel.Size * _panel.Scale) / 2f).Floor();
    }

    /// <summary>Opens the menu; with <paramref name="bugReportOnly"/> only its bug report form (the lobby's corner button, D283), which closes the menu when done.</summary>
    public void Open(bool bugReportOnly = false)
    {
        _screenshot = GetViewport().GetTexture().GetImage(); // the frame before the menu, for a bug report
        _formOnly = bugReportOnly;
        ShowPage(bugReportOnly ? "bug_report" : "home");
        Visible = true;
        Play(_oneShot, "sound/misc/escape_menu/esc_open.ogg", false);
        Act?.Invoke("opened", null);
    }

    public void Close()
    {
        if (!Visible)
            return;
        Visible = false;
        Play(_oneShot, "sound/misc/escape_menu/esc_close.ogg", false);
        Act?.Invoke("closed", null);
    }

    private void Play(AudioStreamPlayer player, string path, bool loop)
    {
        AudioStream stream = _streams?.Invoke(path, loop);
        if (stream == null)
            return;
        player.Stream = stream;
        player.Play();
    }

    private string Field(string key) => _data.Get(key) is { Kind: UiValueKind.String or UiValueKind.Number } v ? Value(v) : null;

    private static string Value(UiValue v) => v.Kind == UiValueKind.Number ? v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) : v.Text;

    private void ShowPage(string name)
    {
        _pageName = name;
        foreach (Node child in _page.GetChildren())
        {
            _page.RemoveChild(child);
            if (child != _bugForm) // a draft survives going back
                child.QueueFree();
        }
        bool server = Field("stationName") != null; // the server's init has come (tg: 3 s after the connection)
        _station.Text = Field("stationName") ?? "";
        _details.Text = server
            ? $"Round ID: {Field("roundId") ?? "Unset"}\nServer Time: {Field("serverTime")}\nShift Time: {Field("shiftTime")}\nMap: {Field("mapName")}"
            : "";
        switch (name)
        {
            case "players":
                PlayersPage();
                break;
            case "leave_body":
                _page.AddChild(MenuButton("< Back", () => ShowPage("home")));
                _page.AddChild(MenuButton("Ghost", () => ActAndClose("ghost")));
                break;
            case "bug_report":
                _page.AddChild(_bugForm);
                _bugStatus.Text = "";
                ShowThumb();
                break;
            case "admin":
                // tg's AdminPage (M10x slice G6): See Admin Notices, Pray and See Notes are not ported (their acts are not translated)
                _page.AddChild(MenuButton("< Back", () => ShowPage("home")));
                _page.AddChild(MenuButton("Create Admin Ticket", () => ActAndClose("create_ticket"), !(_data.Get("canAdminHelp")?.Truthy ?? false)));
                _page.AddChild(Blinking(MenuButton("View Latest Ticket", () => Act?.Invoke("view_ticket", null))));
                break;
            case "quit":
                _page.AddChild(Text(QuitText == null ? "Quit the game?" : QuitText + "?", _titleFont, 26, Colors.White));
                _page.AddChild(MenuButton(QuitText ?? "Quit", () => Quit?.Invoke()));
                _page.AddChild(MenuButton("< Back", () => ShowPage("home")));
                break;
            default:
                _page.AddChild(MenuButton("Resume", Close));
                _page.AddChild(MenuButton("Character", () => ActAndClose("character"), !server));
                _page.AddChild(MenuButton("Settings", () =>
                {
                    Close();
                    Settings?.Invoke();
                }));
                _page.AddChild(MenuButton("Players", () => ShowPage("players"), !server));
                _page.AddChild(MenuButton("Vote", () =>
                {
                    Close();
                    Vote?.Invoke();
                }, !server));
                _page.AddChild(Blinking(MenuButton("Admin Help", () => ShowPage("admin"), !server)));
                _page.AddChild(MenuButton("Bug Report", () => ShowPage("bug_report")));
                _page.AddChild(MenuButton("Leave Body", () => ShowPage("leave_body"), !(_data.Get("canLeaveBody")?.Truthy ?? false)));
                _page.AddChild(MenuButton(QuitText ?? "Quit", () => ShowPage("quit")));
                var resources = new HBoxContainer();
                foreach (UiValue resource in _data.Get("resources")?.Items ?? Array.Empty<UiValue>())
                {
                    string id = resource.Get("id")?.Text;
                    Button button = SmallButton(resource.Get("label")?.Text ?? id, () => Act?.Invoke("resource_" + id, null));
                    button.TooltipText = resource.Get("tooltip")?.Text ?? "";
                    resources.AddChild(button);
                }
                _page.AddChild(resources);
                break;
        }
        _panel.ResetSize();
    }

    private void ActAndClose(string action)
    {
        Act?.Invoke(action, null);
        Close();
    }

    // tg's PlayersPage without the ignore toggle (the port's OOC ignores nobody, DV161)
    private void PlayersPage()
    {
        _page.AddChild(MenuButton("< Back", () => ShowPage("home")));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 320), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer();
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        _page.AddChild(scroll);
        IReadOnlyList<UiValue> admins = _data.Get("admins")?.Items ?? Array.Empty<UiValue>();
        list.AddChild(Text(admins.Count > 0 ? "Admins" : "No Admins Online!", _titleFont, 25, new Color(1f, 0.85f, 0.4f)));
        foreach (UiValue admin in admins)
            list.AddChild(PlayerLine(admin));
        list.AddChild(Text("Players", _titleFont, 25, new Color(1f, 0.85f, 0.4f)));
        foreach (UiValue player in _data.Get("players")?.Items ?? Array.Empty<UiValue>())
            list.AddChild(PlayerLine(player));
    }

    private Label PlayerLine(UiValue player)
    {
        string text = player.Get("displayName")?.Text ?? "";
        if (player.Get("ping") is { Kind: UiValueKind.Number } ping)
            text += $" ({Value(ping)}ms)";
        if (player.Get("rank")?.Text is { Length: > 0 } rank)
            text += " - " + rank;
        return Text(text, _textFont, 17, player.Get("isSelf")?.Truthy == true ? new Color(0.6f, 0.85f, 1f) : Colors.White);
    }

    private void BuildBugForm()
    {
        _bugForm = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        _bugForm.AddThemeConstantOverride("separation", 4);
        _bugForm.AddChild(MenuButton("< Back", BackFromForm));
        _bugForm.AddChild(Text("Title", _textFont, 17, Colors.White));
        _bugTitle = new LineEdit { MaxLength = 100, PlaceholderText = "A short summary" };
        _bugForm.AddChild(Field(_bugTitle));
        _bugForm.AddChild(Text("What happened", _textFont, 17, Colors.White));
        _bugWhat = new TextEdit { CustomMinimumSize = new Vector2(0, 100), WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "What you saw, and what you expected" };
        _bugForm.AddChild(Field(_bugWhat));
        _bugForm.AddChild(Text("Steps to reproduce (optional)", _textFont, 17, Colors.White));
        _bugSteps = new TextEdit { CustomMinimumSize = new Vector2(0, 80), WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "1. ...\n2. ..." };
        _bugForm.AddChild(Field(_bugSteps));
        var image = new HBoxContainer();
        image.AddThemeConstantOverride("separation", 10);
        _bugAttach = new CheckBox { Text = "Attach the screenshot", ButtonPressed = true, FocusMode = FocusModeEnum.None };
        _bugAttach.AddThemeFontOverride("font", _textFont);
        _bugAttach.AddThemeFontSizeOverride("font_size", 15);
        _bugAttach.Toggled += _ => ShowThumb();
        image.AddChild(_bugAttach);
        _bugThumb = new TextureRect { CustomMinimumSize = new Vector2(176, 99), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        image.AddChild(_bugThumb);
        _bugForm.AddChild(image);
        _bugStatus = Text("", _textFont, 15, new Color(1f, 0.85f, 0.4f));
        _bugStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bugForm.AddChild(_bugStatus);
        _bugForm.AddChild(MenuButton("Send", SendReport));
    }

    /// <summary>D283: the lobby's Vote (DV364), Change Log, Bug Report and Exit buttons, for its bottom right corner (the lobby has no Esc menu).</summary>
    public Control LobbyCorner()
    {
        var corner = new HBoxContainer { GrowHorizontal = GrowDirection.Begin, GrowVertical = GrowDirection.Begin };
        corner.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 16);
        corner.AddThemeConstantOverride("separation", 8);
        foreach ((string text, Action pressed) in new (string, Action)[] { ("Vote", () => Vote?.Invoke()), ("Change Log", () => Act?.Invoke("resource_changelog", null)), ("Bug Report", () => Open(true)), ("Exit", () => Quit?.Invoke()) })
        {
            Button button = SmallButton(text, pressed);
            button.AddThemeFontSizeOverride("font_size", 19);
            button.CustomMinimumSize = new Vector2(0, 34);
            corner.AddChild(button);
        }
        return corner;
    }

    private void BackFromForm()
    {
        if (_formOnly)
            Close();
        else
            ShowPage("home");
    }

    private Control Field(Control input)
    {
        input.AddThemeFontOverride("font", _textFont);
        input.AddThemeFontSizeOverride("font_size", 16);
        return input;
    }

    private void ShowThumb()
    {
        Image image = _screenshot;
        _bugThumb.Texture = _bugAttach.ButtonPressed && image != null ? ImageTexture.CreateFromImage(image) : null;
    }

    private void SendReport()
    {
        string title = _bugTitle.Text.Trim(), what = _bugWhat.Text.Trim(), steps = _bugSteps.Text.Trim();
        if (title.Length == 0 || what.Length == 0)
        {
            _bugStatus.Text = "Write a title and what happened.";
            return;
        }
        ulong now = Time.GetTicksMsec();
        if (now < _nextReportMsec)
        {
            _bugStatus.Text = $"You can send another report in {(_nextReportMsec - now + 999) / 1000} seconds.";
            return;
        }
        Image image = _bugAttach.ButtonPressed ? _screenshot : null;
        var report = new BugReportMessage(title, what, steps.Length == 0 ? null : steps, ClientInfo?.Invoke(), image == null ? null : WebP(image));
        if (SendBugReport?.Invoke(report) != true)
        {
            _bugStatus.Text = "The report could not be sent: a field is too long, or the game is not connected.";
            return;
        }
        _nextReportMsec = now + Limits.BugReportCooldownTicks * 50UL; // the server's limit, so a report in the wait is not typed for nothing
        _bugTitle.Text = _bugWhat.Text = _bugSteps.Text = "";
        BackFromForm();
    }

    // Lossy WebP at 80%, halved until it fits the protocol's cap.
    private static byte[] WebP(Image source)
    {
        var image = (Image)source.Duplicate();
        if (image.IsCompressed())
            image.Decompress();
        for (int i = 0; i < 4; i++)
        {
            byte[] bytes = image.SaveWebpToBuffer(true, 0.8f);
            if (bytes.Length > 0 && bytes.Length <= Limits.MaxBugReportImageBytes)
                return bytes;
            image.Resize(Math.Max(1, image.GetWidth() / 2), Math.Max(1, image.GetHeight() / 2));
        }
        return null;
    }

    private static Label Text(string text, Font font, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private Button MenuButton(string text, Action pressed, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 46), FocusMode = FocusModeEnum.None };
        MenuStyle.StyleButton(button);
        button.AddThemeFontOverride("font", _titleFont);
        button.AddThemeFontSizeOverride("font_size", 32);
        button.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.85f, 0.4f));
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.4f, 0.4f));
        button.Pressed += pressed;
        return button;
    }

    // tg's escape-menu__menu-button--blinking while an admin's reply waits (hasTicketNotification): its text white to red and back in
    // 0.8 s (blink-red), with tg's tooltip
    private Button Blinking(Button button)
    {
        if (!(_data.Get("hasTicketNotification")?.Truthy ?? false))
            return button;
        button.TooltipText = "An admin is trying to talk to you!";
        Tween tween = button.CreateTween().SetLoops();
        tween.TweenMethod(Callable.From<Color>(c => button.AddThemeColorOverride("font_color", c)), Colors.White, Colors.Red, 0.4f);
        tween.TweenMethod(Callable.From<Color>(c => button.AddThemeColorOverride("font_color", c)), Colors.Red, Colors.White, 0.4f);
        return button;
    }

    private Button SmallButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        MenuStyle.StyleButton(button);
        button.AddThemeFontOverride("font", _textFont);
        button.AddThemeFontSizeOverride("font_size", 15);
        button.Pressed += pressed;
        return button;
    }
}
