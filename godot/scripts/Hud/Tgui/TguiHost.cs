using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud.Tgui;

// M5 slice 3 (D148, protocol 16, DV44): the window host. It draws the client's open tgui windows (TguiWindows, from the replica's
// UiWindows) as tg's windows: a title bar with the title and the close button over the interface's content, in tg's theme, scaled
// with the HUD. The title bar drags the window anywhere on the screen; every new window opens in the middle (the owner, 2026-10-29). Its bottom right corner resizes it (window_size.NAME=w,h,
// the owner, 2026-10-20), its content scrolling when the window is smaller. A window whose ui status is disabled is dimmed.

public partial class TguiHost : Control
{
    private const string ChangelogInterface = "Changelog";

    // M10x slice D5 (the owner, 2026-10-28): the colour picker takes the Esc menu's look too
    private const string ColorPickerInterface = "ColorPickerModal";

    private static bool InMenuStyle(TguiWindow window) => window.Interface is ChangelogInterface or ColorPickerInterface;

    private sealed class Frame
    {
        public TguiWindow Window; // the window its handlers act on
        public float ButtonOpacity = -1f; // TguiHost.Opacity its buttons were built with
        public PanelContainer Root;
        public StyleBoxFlat Background;
        public PanelContainer Bar;
        public Button Close;
        public Label Title;
        public Label StatusIcon;
        public Control Content;
        public ColorRect Dimmer;
        public int Version = -1;
        public TguiRenderEnv Env;
        public readonly Dictionary<string, Control> MapViews = new(StringComparer.Ordinal); // M10 slice D: kept across rebuilds
    }

    private readonly Dictionary<int, Frame> _frames = new();
    private TguiWindows _windows;
    private IAppearanceTextures _textures;
    private readonly Dictionary<(string, string), Texture2D> _icons = new();
    private TguiWindow _dragging;
    private ReplicaWorld _replica;
    private Vector2 _dragLast;
    private double _scale = 1;
    private Vector2 _screen; // the screen size the windows were placed on
    private LobbyScreen _lobby;

    // Godot's tooltip dies with its control and a window rebuilds at every server update: the windows' tooltips are drawn here (Rebuild moves them to TipMeta)
    public const string TipMeta = "tgui_tip";
    private const double TipDelay = 0.5; // Godot's gui/timers/tooltip_delay_sec
    private const float TipMaxWidth = 380;
    private readonly PanelContainer _tip = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = (int)RenderingServer.CanvasItemZMax };
    private readonly Label _tipText = TguiRender.Text("", null, false, 14, null);
    private double _tipWait;
    private readonly List<Action> _afterLayout = new(); // a rebuild's, for the next frame

    /// <summary>D282: the lobby's Settings button opens the client's Settings window (the client's own options; tg's game preferences are not ported).</summary>
    public Action LobbySettings { get; set; }
    private readonly List<Control> _overlays = new();
    private Action<string> _lobbySound;
    private Font _lobbyFont;

    /// <summary>M10 slice F: how the lobby screen plays tg's menu sounds and its info TV's font (tgui-lobby's Grand9K Pixel).</summary>
    public void BindLobby(Action<string> sound, Font tvFont)
    {
        _lobbySound = sound;
        _lobbyFont = tvFont;
    }

    /// <summary>
    /// M10x slice A (D281): <paramref name="node"/> is drawn over the lobby screen and under the windows until <see cref="Release"/> (the
    /// chat: tg's chat panel is on screen in the lobby too).
    /// </summary>
    public void Adopt(Control node)
    {
        node.Reparent(this, false);
        node.ZIndex = LobbyScreen.WindowsZ - 1;
        _overlays.Add(node);
    }

    /// <summary>Gives back a node of <see cref="Adopt"/> to <paramref name="parent"/> at <paramref name="index"/>.</summary>
    public void Release(Control node, Node parent, int index)
    {
        _overlays.Remove(node);
        node.ZIndex = 0;
        node.Reparent(parent, false);
        parent.MoveChild(node, Math.Min(index, parent.GetChildCount() - 1));
    }

    /// <summary>The owner (2026-10-29): how opaque the windows' backgrounds are (PlayOptions.WindowOpacity); their content stays solid.</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>The windows (the engine-free half).</summary>
    public TguiWindows Windows => _windows;

    // Godot sends every control the drag's end: the window node that began it hears it here (tgui's onDragEnd)
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && TguiRender.ActiveDragEnd is Action end)
        {
            TguiRender.ActiveDragEnd = null;
            end();
        }
    }

    /// <summary>A text field of a window has the keyboard (the game's keys stay off while it is typed in).</summary>
    public bool Typing => GetViewport()?.GuiGetFocusOwner() is (LineEdit or TextEdit) and Control edit && IsAncestorOf(edit);

    public void Bind(TguiWindows windows, IAppearanceTextures textures)
    {
        _windows = windows;
        _textures = textures;
        Name = "TguiWindows";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        StyleBoxFlat tipBox = TguiRender.Flat(new Color(0, 0, 0, 0.9f), 2);
        tipBox.SetContentMarginAll(6);
        _tip.AddThemeStyleboxOverride("panel", tipBox);
        _tipText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tip.AddChild(_tipText);
        AddChild(_tip);
    }

    /// <summary>Each frame: syncs the windows with the replica, rebuilds the ones that changed, places and stacks them.</summary>
    public void Update(ReplicaWorld replica, double scale)
    {
        if (_windows == null)
            return;
        _scale = scale;
        _replica = replica;
        Vector2 screen = GetViewportRect().Size;
        UpdateTip(screen);
        if (screen != _screen)
        {
            _windows.Rescreen(_screen.X, _screen.Y, screen.X, screen.Y, scale);
            _screen = screen;
        }
        IReadOnlyDictionary<int, UiWindowState> states = replica?.UiWindows ?? new Dictionary<int, UiWindowState>();
        _windows.Sync(states, screen.X, screen.Y, scale);

        // NtOS swaps a program's interface on the same index (a new TguiWindow): the frame is made again, else its title bar, grip and close
        // would still act on the old window
        foreach (int gone in _frames.Keys.Where(k => _windows.Get(k) != _frames[k].Window).ToList())
        {
            _frames[gone].Root.QueueFree();
            _frames.Remove(gone);
        }
        UpdateLobby();
        int z = _lobby != null ? 1 : 0;
        foreach (Control overlay in _overlays)
            MoveChild(overlay, z++);
        foreach (TguiWindow window in _windows.Open)
        {
            if (window.Interface == Limits.LobbyUiInterface)
                continue;
            if (!_frames.TryGetValue(window.Index, out Frame frame))
            {
                frame = MakeFrame(window);
                _frames[window.Index] = frame;
            }
            if (frame.Version != window.Version || frame.ButtonOpacity != Opacity)
            {
                frame.ButtonOpacity = TguiRender.ButtonOpacity = Opacity;
                Rebuild(window, frame);
                TguiRender.ButtonOpacity = 1f;
            }
            frame.Root.Scale = new Vector2((float)scale, (float)scale);
            // self_modulate tints only the panel's own box, not the children (the text, the controls)
            frame.Root.SelfModulate = frame.Bar.SelfModulate = new Color(1, 1, 1, Opacity);
            frame.Root.Position = new Vector2((float)window.X, (float)window.Y);
            frame.Root.Visible = !_windows.IsWithheld(window); // slice 4D-1: the strip window on the human HUD is shown by the Tab inventory
            MoveChild(frame.Root, z++);
        }
    }

    private void UpdateTip(Vector2 screen)
    {
        if (_afterLayout.Count > 0) // a frame after a rebuild: the control under the pointer is a new one
        {
            _afterLayout.ForEach(run => run());
            _afterLayout.Clear();
            if (Input.MouseMode == Input.MouseModeEnum.Visible)
                GetViewport().UpdateMouseCursorState();
        }
        string text = Input.MouseMode == Input.MouseModeEnum.Visible ? TipOf(GetViewport().GuiGetHoveredControl()) : null;
        if (text == null || (_tipWait += GetProcessDeltaTime()) < TipDelay)
        {
            _tip.Visible = false;
            _tipWait = text == null ? 0 : _tipWait;
            return;
        }
        bool fresh = _tipText.Text != text || !_tip.Visible;
        if (fresh)
        {
            _tipText.Text = text;
            float width = _tipText.GetThemeFont("font").GetMultilineStringSize(text, fontSize: _tipText.GetThemeFontSize("font_size")).X;
            _tipText.CustomMinimumSize = new Vector2(Math.Min(width + 1, TipMaxWidth), 0);
            _tip.Visible = true;
        }
        _tip.Modulate = fresh ? Colors.Transparent : Colors.White; // a wrapped label knows its height only after a layout at its width
        _tip.ResetSize();
        _tip.Scale = new Vector2((float)_scale, (float)_scale);
        Vector2 size = _tip.Size * _tip.Scale;
        _tip.Position = (GetLocalMousePosition() + new Vector2(10, 18)).Clamp(Vector2.Zero, (screen - size).Max(Vector2.Zero));
    }

    // Godot's walk (Viewport::_gui_get_tooltip): up through the controls that pass the mouse on
    private static string TipOf(Control control)
    {
        for (; control != null; control = control.GetParentControl())
        {
            if (control.HasMeta(TipMeta))
                return control.GetMeta(TipMeta).AsString();
            if (control.MouseFilter == MouseFilterEnum.Stop)
                return null;
        }
        return null;
    }

    private static void TakeTips(Control control)
    {
        if (control.TooltipText != "")
        {
            control.SetMeta(TipMeta, control.TooltipText);
            control.TooltipText = "";
        }
        foreach (Node child in control.GetChildren())
            if (child is Control inner)
                TakeTips(inner);
    }

    // M10 slice F (DV44): tg's lobby window is the lobby screen, under the other windows and over everything else
    private void UpdateLobby()
    {
        TguiWindow lobby = _windows.Get(Limits.LobbyUiWindow);
        if (lobby?.Context == null || lobby.Interface != Limits.LobbyUiInterface)
        {
            if (_lobby != null)
            {
                _lobby.QueueFree();
                _lobby = null;
            }
            return;
        }
        if (_lobby == null)
        {
            IIconFiles files = AssetPreload.IconFiles(IconPaths.Root);
            _lobby = new LobbyScreen(path => files.TryRead(path, out byte[] bytes) ? bytes : null, icon => IconTexture(icon, ""), _lobbySound, _lobbyFont);
            _lobby.OpenSettings = () => LobbySettings?.Invoke();
            AddChild(_lobby);
            MoveChild(_lobby, 0);
        }
        if (_lobbyVersion != lobby.Version)
        {
            _lobbyVersion = lobby.Version;
            TguiContext context = lobby.Context;
            _lobby.Show(context.Data, (action, parameters) => context.Act(action, parameters));
        }
    }

    private int _lobbyVersion = -1;

    private Frame MakeFrame(TguiWindow window)
    {
        var frame = new Frame { Window = window };
        frame.Root = new PanelContainer { Name = "Tgui" + window.Index, MouseFilter = MouseFilterEnum.Stop, ZIndex = LobbyScreen.WindowsZ };
        StyleBoxFlat bg = TguiRender.Flat(TguiTheme.WindowBg, 0);
        bg.SetBorderWidthAll(1);
        bg.BorderColor = new Color(0, 0, 0, 0.6f);
        bg.ShadowColor = new Color(0, 0, 0, 0.45f);
        bg.ShadowSize = 6;
        frame.Root.AddThemeStyleboxOverride("panel", bg);
        frame.Background = bg;
        frame.Root.GuiInput += e => OnWindowInput(window, e);
        if (InMenuStyle(window))
            frame.Root.Theme = MenuStyle.Theme;

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        frame.Root.AddChild(column);

        // tgui's TitleBar: the title, the close button; dragging it moves the window.
        var bar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move, CustomMinimumSize = new Vector2(0, (float)TguiWindows.TitleBarHeight) };
        bar.AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.TitleBarBg, 0));
        frame.Bar = bar;
        bar.GuiInput += e => OnTitleBarInput(window, e);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var pad = new Control { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(pad);
        // tgui's TitleBar__statusIcon: the eye, coloured by the window's status (TitleBar.tsx statusToColor)
        frame.StatusIcon = TguiRender.Text("", null, false, 14, null);
        frame.StatusIcon.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(frame.StatusIcon);
        frame.Title = TguiRender.Text("", null, false, 14, null);
        frame.Title.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        frame.Title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        frame.Title.ClipText = true;
        if (InMenuStyle(window))
        {
            frame.Title.AddThemeFontOverride("font", MenuStyle.TitleFont);
            frame.Title.AddThemeFontSizeOverride("font_size", 18);
        }
        row.AddChild(frame.Title);
        var close = new Button { Text = "✕", FocusMode = FocusModeEnum.None, Flat = true, CustomMinimumSize = new Vector2(32, 0), TooltipText = "Close" };
        close.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        close.AddThemeColorOverride("font_hover_color", TguiTheme.Hex("#ff6060"));
        TguiRender.BareCloseButton(close);
        close.Pressed += () => _windows.Close(window);
        frame.Close = close;
        row.AddChild(close);
        bar.AddChild(row);
        column.AddChild(bar);

        var content = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            content.AddThemeConstantOverride(side, 6);
        column.AddChild(content);
        frame.Content = content;

        // The corner that resizes the window (the owner, 2026-10-20); the window's size is remembered per interface.
        var grip = new ResizeGrip();
        grip.Dragged += delta =>
        {
            Vector2 screen = GetViewportRect().Size;
            _windows.Resize(window, delta.X, delta.Y, screen.X, screen.Y, _scale);
            (double w, double h) = window.Size(1);
            frame.Root.CustomMinimumSize = new Vector2((float)w, (float)h);
            frame.Root.Size = frame.Root.CustomMinimumSize;
        };
        grip.Released += () => _windows.ResizeEnded(window);
        var foot = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        foot.AddChild(grip);
        column.AddChild(foot);

        frame.Dimmer = new ColorRect { Color = TguiTheme.Dimmer, MouseFilter = MouseFilterEnum.Stop, Visible = false };
        frame.Root.AddChild(frame.Dimmer);
        AddChild(frame.Root);
        return frame;
    }

    private void Rebuild(TguiWindow window, Frame frame)
    {
        string focusedKey = frame.Env?.Fields.FirstOrDefault(f => IsInstanceValid(f.Value) && f.Value.HasFocus()).Key;
        int caret = focusedKey != null ? frame.Env.Fields[focusedKey].CaretColumn : 0;
        // M9 slice E: a text area being typed in keeps its focus and caret across the rebuild too
        string focusedArea = frame.Env?.Areas.FirstOrDefault(f => IsInstanceValid(f.Value) && f.Value.HasFocus()).Key;
        (int Line, int Column) areaCaret = focusedArea != null ? (frame.Env.Areas[focusedArea].GetCaretLine(), frame.Env.Areas[focusedArea].GetCaretColumn()) : (0, 0);
        foreach (Control view in frame.MapViews.Values)
            view.GetParent()?.RemoveChild(view); // M10 slice D: the 3D views (and M10x's 2D pictures) outlive the rebuild
        foreach (Node child in frame.Content.GetChildren())
        {
            frame.Content.RemoveChild(child);
            child.QueueFree();
        }
        var usedViews = new HashSet<string>(StringComparer.Ordinal);
        frame.Env = new TguiRenderEnv { Icon = IconTexture, Local = window.Local, MapView = node => MapViewFor(frame, node, usedViews), AfterLayout = _afterLayout };
        // the window's theme (tgui's Window theme prop, M9 slice E: an NtOS device's theme): its frame and its controls' colours
        // the changelog is in the Esc menu's look (the owner, 2026-10-28; MakeFrame sets its fonts)
        TguiTheme.Palette palette = InMenuStyle(window) ? MenuStyle.Palette : TguiTheme.Of(window.Context?.WindowTheme);
        using var themed = TguiTheme.Use(palette);
        frame.Background.BgColor = palette.WindowBg;
        frame.Bar.AddThemeStyleboxOverride("panel", TguiRender.Flat(palette.TitleBarBg, 0));
        frame.Title.AddThemeColorOverride("font_color", palette.TitleText);
        frame.Close.AddThemeColorOverride("font_color", palette.TitleText);
        Control content = TguiRender.Build(window.Content, frame.Env);
        foreach (string unused in frame.MapViews.Keys.Where(k => !usedViews.Contains(k)).ToList())
        {
            frame.MapViews[unused].QueueFree();
            frame.MapViews.Remove(unused);
        }
        content.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // a window resized smaller than its content scrolls it (tgui's Window.Content scrolls the same way)
        var scroll = new WheelScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        TguiRender.KeepScroll(scroll, "window", frame.Env);
        scroll.AddChild(content);
        frame.Content.AddChild(scroll);
        if (frame.Env.Modal != null) // a tgui-core Modal: over the content (a MarginContainer stacks its children)
            frame.Content.AddChild(frame.Env.Modal);
        TakeTips(frame.Content);
        _afterLayout.Add(() => { }); // the hover is refreshed even with no scrolled box
        frame.Title.Text = window.Title ?? window.Interface;
        frame.StatusIcon.Text = TguiTheme.Glyph(window.Status == 0 ? "eye-slash" : "eye");
        frame.StatusIcon.AddThemeColorOverride("font_color", window.Status switch { 2 => TguiTheme.Named("good").Value, 1 => TguiTheme.Named("average").Value, _ => TguiTheme.Named("bad").Value });
        (double w, double h) = window.Size(1);
        frame.Root.CustomMinimumSize = new Vector2((float)w, (float)h);
        frame.Root.Size = frame.Root.CustomMinimumSize;
        frame.Dimmer.Visible = window.Status <= 0;
        frame.Dimmer.MoveToFront();
        frame.Version = window.Version;
        if (focusedKey != null && frame.Env.Fields.TryGetValue(focusedKey, out LineEdit again))
        {
            again.CallDeferred(Control.MethodName.GrabFocus);
            again.CaretColumn = Math.Min(caret, again.Text.Length);
        }
        if (focusedArea != null && frame.Env.Areas.TryGetValue(focusedArea, out TextEdit area))
        {
            area.CallDeferred(Control.MethodName.GrabFocus);
            area.SetCaretLine(Math.Min(areaCaret.Line, area.GetLineCount() - 1));
            area.SetCaretColumn(areaCaret.Column);
        }
    }

    private Texture2D IconTexture(string icon, string state)
    {
        if (string.IsNullOrEmpty(icon) || _textures is not AppearanceTextures textures)
            return null;
        if (_icons.TryGetValue((icon, state), out Texture2D cached))
            return cached;
        Texture2D texture = null;
        if (icon == TguiSpritesheets.Decals32)
        {
            // engineering E14: the decal painter's previews, a floor tile with the coloured decal (tg's paintable_decals sheet)
            if (TguiSpritesheets.TryDrawDecal(textures.Library, state, out Raster decal))
                texture = ImageTexture.CreateFromImage(AppearanceTextures.ToImage(decal));
        }
        else if (icon == TguiSpritesheets.Fish32)
        {
            // the fish encyclopedia's illustrations (tg's fish sheet, its classes the sanitized icon file and state)
            if (TguiSpritesheets.TryDrawFish(textures.Library, state, out Raster fish))
                texture = ImageTexture.CreateFromImage(AppearanceTextures.ToImage(fish));
        }
        else if (TguiSpritesheets.IsSheet(icon))
        {
            // M7 slice D172: a class of one of tg's sprite sheets (the RPD's "pipes32x32 north-pipe"): that state's frame of that dir in
            // the first of the sheet's files that has the state
            if (TguiSpritesheets.TryResolve(icon, state, out SpritesheetIcon cls))
            {
                foreach (string file in cls.Files)
                {
                    if (textures.Library.TryGetIcon(file, out IIcon source) && source.TryGetState(cls.State, out IIconState exact))
                    {
                        Image picture = AppearanceTextures.ToImage(exact.Get(ByondDir.Index(cls.Dir, exact.Dirs), 0));
                        // E13: the class's blend_icon underlay and overlay (the RCD's grilled windows, its solid airlocks' fill)
                        if (SheetLayer(textures, cls.Underlay) is Image under)
                        {
                            picture.Convert(Image.Format.Rgba8);
                            under.BlendRect(picture, new Rect2I(Vector2I.Zero, picture.GetSize()), Vector2I.Zero);
                            picture = under;
                        }
                        if (SheetLayer(textures, cls.Overlay) is Image over)
                        {
                            picture.Convert(Image.Format.Rgba8);
                            picture.BlendRect(over, new Rect2I(Vector2I.Zero, over.GetSize()), Vector2I.Zero);
                        }
                        texture = ImageTexture.CreateFromImage(picture);
                        break;
                    }
                }
            }
        }
        else if (icon.EndsWith(".png", StringComparison.Ordinal) && state == null)
        {
            // M8z slice B (D236): a plain PNG of the converted tree (the strip menu's slot pictures), copied by tools/Anomaly.Tools.Icons
            if (AssetPreload.IconFiles(IconPaths.Root).TryRead(icon, out byte[] bytes))
            {
                var picture = new Image();
                if (picture.LoadPngFromBuffer(bytes) == Error.Ok)
                    texture = ImageTexture.CreateFromImage(picture);
            }
        }
        else if (textures.Library.TryGetIcon(icon, out IIcon sheet) && sheet.TryGetShown(state ?? "", out IIconState found))
            texture = ImageTexture.CreateFromImage(AppearanceTextures.ToImage(found.Get(0, 0)));
        _icons[(icon, state)] = texture;
        return texture;
    }

    // E13: a sprite sheet class's blended layer, its state's first frame facing south
    private static Image SheetLayer(AppearanceTextures textures, SpritesheetLayer? layer)
    {
        if (layer is not SpritesheetLayer l || !textures.Library.TryGetIcon(l.File, out IIcon icon) || !icon.TryGetState(l.State, out IIconState state))
            return null;
        Image picture = AppearanceTextures.ToImage(state.Get(0, 0));
        picture.Convert(Image.Format.Rgba8);
        return picture;
    }

    private void OnWindowInput(TguiWindow window, InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true })
            _windows.Raise(window);
    }

    private void OnTitleBarInput(TguiWindow window, InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                {
                    _dragging = window;
                    _dragLast = GetViewport().GetMousePosition();
                    _windows.Raise(window);
                }
                else if (_dragging == window)
                    _dragging = null;
                AcceptEvent();
                break;
            case InputEventMouseMotion when _dragging == window:
                Vector2 now = GetViewport().GetMousePosition();
                Vector2 screen = GetViewportRect().Size;
                _windows.Drag(window, now.X - _dragLast.X, now.Y - _dragLast.Y, screen.X, screen.Y, _scale);
                _dragLast = now;
                if (_frames.TryGetValue(window.Index, out Frame frame))
                    frame.Root.Position = new Vector2((float)window.X, (float)window.Y);
                AcceptEvent();
                break;
        }
    }

    /// <summary>The frame of a window (for the scripted clicks and the tests in the scene).</summary>
    public Rect2? FrameRect(int index) => _frames.TryGetValue(index, out Frame f) ? f.Root.GetGlobalRect() : null;

    // M10 slice D: the 3D view of a ByondUi map, one per map id and window, made once and reused by every rebuild; M10x slice E2
    // (D284): a map with no turfs (UiNode.MapPicture) is a 2D picture of its screen objects.
    private Control MapViewFor(Frame frame, UiNode node, HashSet<string> used)
    {
        string key = node.MapRef ?? "";
        if (!frame.MapViews.TryGetValue(key, out Control view))
        {
            view = node.MapPicture ? new TguiMapPicture(() => _replica, _textures) { MapRef = key } : new TguiMapView(() => _replica);
            frame.MapViews[key] = view;
        }
        used.Add(key);
        if (view is TguiMapView map)
            map.From = node.MapFrom;
        return view;
    }
}
