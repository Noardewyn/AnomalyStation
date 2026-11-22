using System.Linq;
using System.Threading.Tasks;
using Godot;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

// M10 slice F (DV44): tg's lobby menu, tgui-lobby's LobbyMenu.tsx and styles/main.scss translated: the title screen behind, the menu's
// sprites anchored at the top middle and scaled with the window (scaling.ts: the smaller of width / 608 and height / 480), the sprite
// buttons with their hover, disabled, pressed and enabled pictures (the pressed and enabled ones are tg's APNG flicks), the collapse
// with its shutter, and the info TV on the right with its lines. Its data is the lobby window's (the server's "init" and "state"
// messages), its clicks tg's sendAction. The polls and the admin's lines are drawn as tg draws them when the server sends none (no
// database, no admins). Gaps R2: the station traits' sign-up buttons (their icon state, overlays and tooltip from the server's
// stationTraits) and the feedback of a click (traitFeedback).

public partial class LobbyScreen : Control
{
    // tgui-lobby scaling.ts
    private const float ReferenceWidth = 608, ReferenceHeight = 480;

    // LobbyMenu.tsx's timings and distances (px at scale 1)
    // DEVIATION: tg's shutter (175 x 130) comes down 143 px and covers only the panel's upper part (the row of buttons below it rolls up uncovered); here it is stretched by
    // ShutterExtra (its height scaled up) and comes down as much further, so it covers the whole menu down to the start button.
    private const float ShutterExtra = 70, ShutterTravel = 143 + ShutterExtra, CollapseUp = 146, CollapseSlide = 134, TvSlide = 146;
    private const double MoveSeconds = 0.4, ShutterWaitSeconds = 0.2;

    private const string Png = "icons/hud/lobby/png/";
    private const string TitlePicture = "res://assets/ui/lobby_title.webp";

    /// <summary>The z index the other windows draw at: above the lobby's own layers (its buttons' z up to 6, their flicks 2 more),
    /// since a z index outranks the order of the siblings.</summary>
    public const int WindowsZ = 16;

    private readonly Func<string, byte[]> _read;
    private readonly Func<string, Texture2D> _icon;
    private readonly Action<string> _sound;
    private readonly Font _tvFont;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApngFrames> _flicks = new(StringComparer.Ordinal);

    private TextureRect _title;
    private Control _anchor;
    private Control _tv;
    private Label _tvText;
    private TextureRect _tvStatic, _tvScanlines;
    private TextureRect _shutter;
    private readonly List<(Control Node, float Top, float Left, bool Slide)> _placed = new();
    private readonly Dictionary<string, SpriteButton> _buttons = new(StringComparer.Ordinal);
    private TextureRect _blip;

    // gaps R2: the station traits' buttons of the last state (rebuilt when it changes) and the feedback line over them
    private readonly List<Control> _traitButtons = new();
    private string _traitsShown;
    private Label _traitFeedback;
    private string _feedbackShown;
    private Godot.Tween _feedbackTween;

    private UiValue _data;
    private Action<string, (string, object)[]> _act;
    private bool _collapsed, _animating, _tvActive = true;
    private float _collapse; // 0 shown .. 1 collapsed (the elements' transition)
    private float _shutterDown; // 0 .. ShutterTravel
    private float _scale = 1;

    /// <summary>The Settings button was pressed (the scene opens its Settings window).</summary>
    public Action OpenSettings { get; set; }

    public LobbyScreen(Func<string, byte[]> read, Func<string, Texture2D> icon, Action<string> sound, Font tvFont)
    {
        _read = read;
        _icon = icon;
        _sound = sound;
        _tvFont = tvFont;
        Name = "LobbyScreen";
        MouseFilter = MouseFilterEnum.Stop; // the lobby covers the map: clicks stay in it
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Build();
    }

    /// <summary>The lobby's state (tg's ServerState) and the act of its window.</summary>
    public void Show(UiValue data, Action<string, (string, object)[]> act)
    {
        _data = data;
        _act = act;
        Refresh();
    }

    private void Build()
    {
        AddChild(new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        // the title screen: backgroundSize cover, backgroundPosition center
        _title = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, TextureFilter = TextureFilterEnum.Nearest, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_title);

        _anchor = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_anchor);
        // LobbyMenu.tsx's elements in its order; the z-index of each (CSS: z-index auto paints under 1)
        var collapse = new SpriteButton(this, "collapse", noPress: true) { ZIndex = 0 };
        collapse.Clicked += _ => ToggleCollapse();
        _blip = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest };
        collapse.AddChild(_blip);
        Place(collapse, 82, -54, slide: true);
        _buttons["collapse"] = collapse;
        Place(Sprite("background", 1), 0, -61);
        AddButton("ready", 8, -65, 3, _ => Act("ready_toggle"));
        AddButton("join_game", 13, -58, 3, ctrl => Act("join", ("ctrlClick", ctrl)));
        AddButton("observe", 40, -54, 3, _ => Act("observe"));
        AddButton("character_setup", 70, -54, 3, _ => Act("character_setup"));
        _shutter = Sprite("shutter", 9); // over every button (their z is up to 6, their flicks 8), under the other windows (WindowsZ)
        _anchor.AddChild(_shutter);
        AddButton("poll", 122, -26, 6, _ => Act("poll"));
        AddButton("crew_manifest", 122, 2, 6, _ => Act("crew_manifest"));
        AddButton("settings", 122, 29, 6, _ => OpenSettings?.Invoke()); // D282 (DV204): the client's Settings window, not tg's game preferences
        AddButton("changelog", 122, 57, 6, _ => Act("changelog"));
        AddButton("start_round", 146, -54, 3, _ => Act("start_now")); // the owner's plain plate (assets/ui) instead of tg's START NOW sign

        // LobbyMenu.tsx's TraitFeedback: .lobby__trait-feedback (top 24, left -112, z-index 10, Pixellari 12px, white with a black stroke)
        _traitFeedback = new Label { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10, Visible = false, AutowrapMode = TextServer.AutowrapMode.Off };
        if (_tvFont != null)
            _traitFeedback.AddThemeFontOverride("font", _tvFont);
        _traitFeedback.AddThemeColorOverride("font_color", Colors.White);
        _traitFeedback.AddThemeColorOverride("font_outline_color", Colors.Black);
        _traitFeedback.AddThemeConstantOverride("outline_size", 2);
        Place(_traitFeedback, 24, -112);

        // the info TV: .lobby__tv on the right edge, its layers, its text, its static and scanlines
        _tv = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        AddChild(_tv);
        _tv.AddChild(Sprite("newplayer", 0));
        _tv.AddChild(Sprite("newplayer_overlay", 1));
        _tvText = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ZIndex = 2, AutowrapMode = TextServer.AutowrapMode.Off };
        if (_tvFont != null)
            _tvText.AddThemeFontOverride("font", _tvFont);
        _tvText.AddThemeColorOverride("font_color", Colors.White);
        _tv.AddChild(_tvText);
        _tvStatic = Sprite("static_base", 3);
        _tvStatic.Modulate = new Color(1, 1, 1, 0.15f);
        _tv.AddChild(_tvStatic);
        _tvScanlines = Sprite("scanline", 4);
        _tvScanlines.Modulate = new Color(1, 1, 1, 0.08f);
        _tv.AddChild(_tvScanlines);
    }

    private void AddButton(string state, float top, float left, int z, Action<bool> click)
    {
        var button = new SpriteButton(this, state) { ZIndex = z };
        button.Clicked += click;
        Place(button, top, left);
        _buttons[state] = button;
    }

    private void Place(Control node, float top, float left, bool slide = false)
    {
        _anchor.AddChild(node);
        _placed.Add((node, top, left, slide));
    }

    private TextureRect Sprite(string name, int z) => new()
    {
        Texture = Texture(name), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, ZIndex = z,
    };

    private void Act(string action, params (string, object)[] parameters) => _act?.Invoke(action, parameters);

    internal Texture2D Texture(string name)
    {
        if (_textures.TryGetValue(name, out Texture2D known))
            return known;
        Texture2D texture = null;
        if (name.StartsWith("start_round", StringComparison.Ordinal))
            return _textures[name] = GD.Load<Texture2D>("res://assets/ui/lobby_" + name + ".png");
        byte[] bytes = _read(Png + name + ".png");
        if (bytes != null)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) == Error.Ok)
                texture = ImageTexture.CreateFromImage(image);
        }
        _textures[name] = texture;
        return texture;
    }

    internal ApngFrames Flick(string name)
    {
        if (!_flicks.TryGetValue(name, out ApngFrames frames))
            _flicks[name] = frames = ApngFrames.Read(_read(Png + name + ".png"));
        return frames;
    }

    internal void Sound(string file) => _sound?.Invoke(file);

    // LobbyMenu.tsx's render from the server state
    private void Refresh()
    {
        if (_data == null)
            return;
        if (_title.Texture == null)
        {
            // this port's own title picture (a painted image, so smooth, not the pixel filter); tg's default title icon when the file is not imported
            Texture2D own = ResourceLoader.Exists(TitlePicture) ? GD.Load<Texture2D>(TitlePicture) : null;
            if (own != null)
                _title.TextureFilter = TextureFilterEnum.Linear;
            _title.Texture = own ?? _icon(_data.Get("titleImageUrl")?.Text);
        }
        bool transparent = _data.Truthy("transparent");
        _title.Visible = !transparent;
        string phase = _data.Get("gamePhase")?.Text ?? "startup";
        bool isReady = _data.Truthy("isReady");

        SpriteButton ready = _buttons["ready"];
        ready.Visible = _data.Truthy("canReady");
        ready.SetState(isReady ? "ready" : "not_ready", true);
        _buttons["join_game"].Visible = _data.Truthy("canJoin");
        _buttons["observe"].SetState("observe", _data.Truthy("canObserve"));
        _buttons["character_setup"].SetState("character_setup", _data.Truthy("assetsReady"));
        _buttons["poll"].SetState("poll", _data.Truthy("canPoll"));
        _buttons["crew_manifest"].SetState("crew_manifest", true);
        _buttons["settings"].SetState("settings", _data.Truthy("assetsReady"));
        _buttons["changelog"].SetState("changelog", true);
        _buttons["collapse"].SetState(_collapsed ? "expand" : "collapse", true);
        bool blipEnabled = phase is "pregame" or "startup";
        _blip.Texture = Texture(!blipEnabled ? "ready_blip_disabled" : isReady ? "ready_blip_ready" : "ready_blip_not_ready");
        _tvText.Text = string.Join("\n", TvLines(phase));
        RefreshTraits();
        Layout();
    }

    // LobbyMenu.tsx: {ss.stationTraits.map((trait, i) => <LobbyElement top={40 + (i % MAX_TRAITS_PER_COLUMN) * 27}
    // left={-85 - Math.floor(i / MAX_TRAITS_PER_COLUMN) * 27} zIndex={3}> <Tooltip content={trait.description}> <SpriteButton
    // iconState={trait.iconState} onClick={() => sendAction('sign_up', { ref: trait.ref })}> {trait.overlays.map(<img className=
    // "lobby__trait-overlay" />)} ...}, then {!!ss.traitFeedback && <TraitFeedback text={ss.traitFeedback} />}
    private const int MaxTraitsPerColumn = 3;
    private const double TraitFeedbackSeconds = 1.5;

    private void RefreshTraits()
    {
        UiValue traits = _data.Get("stationTraits");
        string shown = traits?.ToString() ?? "";
        if (shown != _traitsShown)
        {
            _traitsShown = shown;
            foreach (Control old in _traitButtons)
            {
                _placed.RemoveAll(p => p.Node == old);
                old.QueueFree();
            }
            _traitButtons.Clear();
            IReadOnlyList<UiValue> items = traits is { Kind: UiValueKind.Array } ? traits.Items : Array.Empty<UiValue>();
            for (int i = 0; i < items.Count; i++)
            {
                UiValue trait = items[i];
                string traitRef = trait.Get("ref")?.Text;
                var button = new SpriteButton(this, trait.Get("iconState")?.Text ?? "signup") { ZIndex = 3, TooltipText = trait.Get("description")?.Text ?? "" };
                button.Clicked += _ => Act("sign_up", ("ref", traitRef));
                // .lobby__trait-overlay: over the button at the lobby's scale, z-index 3, no pointer events
                if (trait.Get("overlays") is { Kind: UiValueKind.Array } overlays)
                {
                    foreach (UiValue overlay in overlays.Items)
                    {
                        TextureRect picture = Sprite(overlay.Text ?? "", 3);
                        picture.AnchorRight = 1;
                        picture.AnchorBottom = 1;
                        button.AddChild(picture);
                    }
                }
                Place(button, 40 + (i % MaxTraitsPerColumn) * 27, -85 - (i / MaxTraitsPerColumn) * 27);
                _traitButtons.Add(button);
            }
        }

        // TraitFeedback: shown when its text changes, faded out over 1.5 s while it rises 8 px after 70 % of it
        string feedback = _data.Get("traitFeedback")?.Text;
        if (feedback != _feedbackShown)
        {
            _feedbackShown = feedback;
            _feedbackTween?.Kill();
            _traitFeedback.Visible = !string.IsNullOrEmpty(feedback);
            _traitFeedback.Text = feedback ?? "";
            _traitFeedback.Modulate = Colors.White;
            if (_traitFeedback.Visible && IsInsideTree())
            {
                _feedbackTween = CreateTween();
                _feedbackTween.TweenInterval(TraitFeedbackSeconds * 0.7);
                _feedbackTween.TweenProperty(_traitFeedback, "modulate:a", 0f, TraitFeedbackSeconds * 0.3);
                _feedbackTween.TweenCallback(Callable.From(() => _traitFeedback.Visible = false));
            }
        }
    }

    private IEnumerable<string> TvLines(string phase)
    {
        int players = (int)(_data.Get("playerCount")?.Number ?? 0);
        string plural = players != 1 ? "s" : "";
        if (phase == "postgame")
            return new[] { "Game ended,", "restart soon" };
        if (phase == "playing")
            return new[] { _data.Get("mapName")?.Text ?? "", $"{players} player{plural} online", $"{_data.Get("shiftTime")?.Text} in" };
        if (_data.Truthy("isAdmin"))
        {
            return new[]
            {
                $"Starting in {_data.Get("countdown")?.Text}", $"{players} player{plural}", $"{(int)(_data.Get("readyCount")?.Number ?? 0)} players ready",
                $"{(int)(_data.Get("adminReadyCount")?.Number ?? 0)} / {(int)(_data.Get("adminCount")?.Number ?? 0)} admins ready",
            };
        }
        return new[] { _data.Get("countdown")?.Text ?? "", $"{players} player{plural}" };
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            Layout();
    }

    private void Layout()
    {
        Vector2 size = Size;
        _scale = Math.Min(size.X / ReferenceWidth, size.Y / ReferenceHeight);
        if (_scale <= 0)
            return;
        _anchor.Position = new Vector2(size.X / 2, 0);
        foreach ((Control node, float top, float left, bool slide) in _placed)
        {
            float up = (slide ? -CollapseSlide : -CollapseUp) * _collapse;
            node.Position = new Vector2(left, top + up) * _scale;
            Fit(node);
        }
        // The sign rolls up to the top edge, onto the expand button's place (both at left -54): it is gone once collapsed, or it would cover the button and take its clicks.
        if (_buttons.TryGetValue("start_round", out SpriteButton startNow))
        {
            startNow.Visible = _data != null && _data.Truthy("isLocalhost") && _collapse < 0.999f;
            startNow.MouseFilter = _collapse > 0.001f ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        }
        // the sprite stretched down by ShutterExtra (its bottom edge ends where it did, just lower), above the screen at rest as tg's is
        Fit(_shutter);
        _shutter.Position = new Vector2(-73, -143 - ShutterExtra + _shutterDown) * _scale;
        _shutter.Size = new Vector2(_shutter.Size.X, _shutter.Size.Y + ShutterExtra * _scale);
        _blip.Size = (_blip.Texture?.GetSize() ?? Vector2.Zero) * _scale;
        _traitFeedback.AddThemeFontSizeOverride("font_size", Math.Max(1, (int)Math.Round(12 * _scale)));

        // .lobby__tv: right 0, top 50% - 208px, 128 x 96; collapsed it slides right 146px
        var tv = new Vector2(128, 96) * _scale;
        _tv.Size = tv;
        _tv.Position = new Vector2(size.X - tv.X + TvSlide * _scale * _collapse, size.Y / 2 - 208 * _scale);
        foreach (Node child in _tv.GetChildren())
        {
            if (child is TextureRect layer)
                Fit(layer);
        }
        _tvStatic.Size = tv;
        _tvScanlines.Position = new Vector2(5, 29) * _scale;
        if (_tvScanlines.Texture != null)
            _tvScanlines.Size = new Vector2(94, 94 * _tvScanlines.Texture.GetHeight() / (float)_tvScanlines.Texture.GetWidth()) * _scale;
        _tvText.Position = new Vector2(14, 28) * _scale;
        _tvText.Size = new Vector2(80, 50) * _scale;
        _tvText.AddThemeFontSizeOverride("font_size", Math.Max(1, (int)Math.Round(7 * _scale)));
        _tvText.AddThemeConstantOverride("line_spacing", (int)Math.Round(7 * 0.5 * _scale));
        bool showStatic = _tvActive && !_collapsed;
        _tvText.Visible = _tvActive;
        _tvStatic.Visible = showStatic;
        _tvScanlines.Visible = showStatic;
    }

    private void Fit(Control node)
    {
        Texture2D texture = node is TextureRect rect ? rect.Texture : node is SpriteButton button ? button.Normal : null;
        if (texture != null)
            node.Size = texture.GetSize() * _scale;
    }

    internal float LobbyScale => _scale;

    // LobbyMenu.tsx handleToggleCollapse: the shutter comes down and up again while the menu rolls up into it (or out of it)
    private async void ToggleCollapse()
    {
        if (_animating)
            return;
        _animating = true;
        if (!_collapsed)
        {
            Sound("sound/misc/menu/menu_rollup1.ogg");
            _tvActive = false;
            await Tween(v => _shutterDown = v, 0, ShutterTravel, MoveSeconds, Godot.Tween.EaseType.Out);
            await ToSignal(GetTree().CreateTimer(ShutterWaitSeconds), SceneTreeTimer.SignalName.Timeout);
            _collapsed = true;
            Refresh();
            // The shutter, longer than tg's, must not outrun the menu rolling up under it (its lower edge stays below the lowest button's): it goes up as far as
            // that allows with the menu, and the rest once the menu is gone.
            await Task.WhenAll(
                Tween(v => _shutterDown = v, ShutterTravel, ShutterTravel * 0.3f, MoveSeconds, Godot.Tween.EaseType.In),
                Tween(v => _collapse = v, 0, 1, MoveSeconds, Godot.Tween.EaseType.In));
            await Tween(v => _shutterDown = v, ShutterTravel * 0.3f, 0, MoveSeconds * 0.5, Godot.Tween.EaseType.Out);
        }
        else
        {
            Sound("sound/misc/menu/menu_rolldown1.ogg");
            await Tween(v => _shutterDown = v, 0, ShutterTravel, MoveSeconds, Godot.Tween.EaseType.Out);
            _collapsed = false;
            Refresh();
            await Tween(v => _collapse = v, 1, 0, MoveSeconds, Godot.Tween.EaseType.Out);
            await ToSignal(GetTree().CreateTimer(ShutterWaitSeconds), SceneTreeTimer.SignalName.Timeout);
            await Tween(v => _shutterDown = v, ShutterTravel, 0, MoveSeconds, Godot.Tween.EaseType.In);
            _tvActive = true;
        }
        _animating = false;
        Refresh();
    }

    // CSS cubic-bezier(0.33, 1, 0.68, 1) and (0.32, 0, 0.67, 0) are the cubic ease-out and ease-in
    private Task Tween(Action<float> set, float from, float to, double seconds, Godot.Tween.EaseType ease)
    {
        var done = new TaskCompletionSource();
        Godot.Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v => { set(v); Layout(); }), from, to, seconds).SetTrans(Godot.Tween.TransitionType.Cubic).SetEase(ease);
        tween.Finished += () => done.TrySetResult();
        return done.Task;
    }
}

/// <summary>tgui-lobby's SpriteButton: the normal picture, the highlighted one under the pointer, the disabled one, and the pressed flick.</summary>
public partial class SpriteButton : Control
{
    private readonly LobbyScreen _lobby;
    private readonly bool _noPress;
    private readonly TextureRect _picture;
    private readonly TextureRect _flick;
    private string _state;
    private bool _enabled = true, _hover;
    private Godot.Tween _flickTween;

    public event Action<bool> Clicked;

    public Texture2D Normal => _picture.Texture;

    public SpriteButton(LobbyScreen lobby, string state, bool noPress = false)
    {
        _lobby = lobby;
        _noPress = noPress;
        _state = state;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _picture = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_picture);
        _flick = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Visible = false, ZIndex = 2 };
        AddChild(_flick);
        MouseEntered += () => { _hover = true; Redraw(); };
        MouseExited += () => { _hover = false; Redraw(); };
        Redraw();
    }

    /// <summary>The button's icon state and whether it is enabled; becoming enabled plays its "_enabled" flick.</summary>
    public void SetState(string state, bool enabled)
    {
        bool wasEnabled = _enabled;
        _state = state;
        _enabled = enabled;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore; // .sprite-btn--disabled: pointer-events none
        if (enabled && !wasEnabled)
            Play(state + "_enabled");
        Redraw();
    }

    private void Redraw()
    {
        string shown = !_enabled ? _state + "_disabled" : _hover ? _state + "_highlighted" : _state;
        _picture.Texture = _lobby.Texture(shown) ?? _lobby.Texture(_state);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click && _enabled)
        {
            AcceptEvent();
            if (!_noPress)
            {
                _lobby.Sound("sound/misc/menu/ui_select1.ogg");
                Play(_state + "_pressed");
            }
            Clicked?.Invoke(click.CtrlPressed);
        }
    }

    // useFlick: the APNG over the button for its duration
    private void Play(string name)
    {
        ApngFrames frames = _lobby.Flick(name);
        if (frames == null || frames.Frames.Count == 0)
            return;
        _flickTween?.Kill();
        var textures = frames.Frames.Select(f => (Texture: (Texture2D)ImageTexture.CreateFromImage(f.Frame), f.Seconds)).ToList();
        _flick.Visible = true;
        _flick.Size = textures[0].Texture.GetSize() * _lobby.LobbyScale;
        _flickTween = CreateTween();
        foreach ((Texture2D texture, double seconds) in textures)
        {
            _flickTween.TweenCallback(Callable.From(() => _flick.Texture = texture));
            _flickTween.TweenInterval(Math.Max(seconds, 0.01));
        }
        _flickTween.TweenCallback(Callable.From(() => _flick.Visible = false));
    }
}
