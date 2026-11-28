using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// M9 slice A (D250): tg's say window (tgui/packages/tgui-say) drawn by the client: the channel button on the left, the text on the right, the
/// window coloured by the channel (styles/colors.scss), 231 tgui pixels wide and 30, 50 or 70 high as the text grows (<see cref="SayModel"/>
/// holds the state and what goes to the server). The keys are tg's: Enter sends, Esc closes, Tab cycles the channel, Up and Down walk the
/// history. A click on the channel button cycles it; dragging the button or the frame moves the window (cursor mode frees the mouse).
/// </summary>
public sealed partial class SayWindow : Control
{
    private SayModel _model;
    private Panel _frame;
    private Panel _content;
    private Button _button;
    private SayTextEdit _text;
    private float _scale = 1f;
    private Vector2? _position; // the top left's share of the screen: it keeps it when the game's window is resized
    private bool _syncing;
    private bool _pressed;
    private bool _dragging;
    private Vector2 _pressAt;

    /// <summary>The clock the model's timers run on (seconds).</summary>
    public Func<double> Now { get; set; } = () => Time.GetTicksMsec() / 1000.0;

    /// <summary>The window's top left was dragged, as its share of the screen (the scene keeps it in the options).</summary>
    public event Action<Vector2> Moved;

    public bool IsOpen => _model?.IsOpen == true;

    public override void _Ready()
    {
        Name = "SayWindow";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _frame = new Panel { MouseFilter = MouseFilterEnum.Stop };
        _frame.GuiInput += OnDragInput;
        AddChild(_frame);
        _content = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _frame.AddChild(_content);
        _button = new Button { FocusMode = FocusModeEnum.None, ClipText = true, MouseFilter = MouseFilterEnum.Stop };
        _button.GuiInput += OnButtonInput;
        _content.AddChild(_button);
        _text = new SayTextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            ScrollFitContentHeight = false,
            ContextMenuEnabled = false,
            ShortcutKeysEnabled = true,
            CaretBlink = true,
        };
        _text.Owner_ = this;
        _text.TextChanged += OnTextChanged;
        _content.AddChild(_text);
        Visible = false;
    }

    /// <summary>Binds the model and where the window stood last as its share of the screen (null: tg's place near the middle of the screen).</summary>
    public void Bind(SayModel model, Vector2? position)
    {
        _model = model;
        _position = position;
    }

    /// <summary>Opens the window on <paramref name="channel"/> with the text field focused (tg's key: the window's open).</summary>
    public void Open(string channel)
    {
        if (_model == null)
            return;
        _model.Open(channel);
        Sync();
        _text.GrabFocus();
    }

    /// <summary>Slice 4E: opens the window on <paramref name="channel"/> with <paramref name="typed"/> already in it (the whisper key: tg's <c>#</c>), the caret after it.</summary>
    public void Open(string channel, string typed)
    {
        if (_model == null)
            return;
        _model.Open(channel);
        _model.Input(typed, Now());
        Sync(); // the caret goes after the text
        _text.GrabFocus();
    }

    /// <summary>D307: speech to text's words after what is typed, for the player to send or fix.</summary>
    public void AppendText(string text)
    {
        if (_model == null || !_model.IsOpen || string.IsNullOrWhiteSpace(text))
            return;
        _model.Input(_model.Value.Length == 0 ? text : _model.Value.TrimEnd() + " " + text, Now());
        Sync();
        _text.GrabFocus();
    }

    /// <summary>Per frame: the model's timers, the window's size at the windows' scale, and the window shown while open.</summary>
    public void Update(float scale)
    {
        if (_model == null)
            return;
        _model.Advance(Now());
        _scale = Math.Max(0.5f, scale);
        if (!_model.IsOpen)
        {
            if (Visible)
            {
                Visible = false;
                _text.ReleaseFocus();
            }
            return;
        }
        Sync();
    }

    // The model's state into the controls: text, button, theme and size.
    private void Sync()
    {
        Visible = true;
        if (_text.Text != _model.Value)
        {
            _syncing = true;
            _text.Text = _model.Value;
            _text.SetCaretLine(_text.GetLineCount() - 1);
            _text.SetCaretColumn(_text.GetLine(_text.GetLineCount() - 1).Length);
            _syncing = false;
        }
        _button.Text = _model.ButtonContent;
        Layout();
        ApplyTheme(_model.Theme, _model.LightMode);
    }

    private void Layout()
    {
        float s = _scale;
        var size = new Vector2(SayModel.Width * s, _model.Size * s);
        Vector2 screen = GetViewportRect().Size;
        Vector2 at = _position * screen ?? new Vector2((screen.X - size.X) / 2f, screen.Y * 0.62f);
        at = new Vector2(Math.Clamp(at.X, 0, Math.Max(0, screen.X - size.X)), Math.Clamp(at.Y, 0, Math.Max(0, screen.Y - size.Y)));
        _frame.Position = at;
        _frame.Size = size;
        // .content: inset 2px, padding 2px, a grid of 4rem (tgui's 12px root: 48px) and the rest
        _content.Position = new Vector2(2 * s, 2 * s);
        _content.Size = size - new Vector2(4 * s, 4 * s);
        float inner = _content.Size.Y - 4 * s;
        _button.Position = new Vector2(2 * s, 2 * s);
        _button.Size = new Vector2(48 * s, Math.Min(inner, 22 * s));
        _text.Position = new Vector2(2 * s + 48 * s + 4.8f * s, 2 * s + 1.2f * s);
        _text.Size = new Vector2(_content.Size.X - _text.Position.X - 2 * s, inner - 1.2f * s);
        _button.AddThemeFontSizeOverride("font_size", Math.Max(6, (int)MathF.Round(11 * s)));
        _text.AddThemeFontSizeOverride("font_size", Math.Max(6, (int)MathF.Round(13.2f * s)));
        _text.ScrollFitContentHeight = false;
    }

    private string _theme;
    private bool _light;

    private void ApplyTheme(string theme, bool light)
    {
        if (theme == _theme && light == _light)
            return;
        _theme = theme;
        _light = light;
        Color colour = ToColor(SayColours.Of(theme));
        _frame.AddThemeStyleboxOverride("panel", Box(light ? new Color(1, 1, 1) : colour, light ? new Color(0.733f, 0.733f, 0.733f) : (Color?)null, 2, 0));
        _content.AddThemeStyleboxOverride("panel", Box(light ? Colors.White : Colors.Black, null, 0, 0));
        Color buttonBg = light ? new Color(0.733f, 0.733f, 0.733f) : new Color(0.122f, 0.122f, 0.122f);
        Color buttonHover = light ? new Color(0.918f, 0.918f, 0.918f) : new Color(0.222f, 0.222f, 0.222f);
        _button.AddThemeStyleboxOverride("normal", Box(buttonBg, null, 0, 4));
        _button.AddThemeStyleboxOverride("hover", Box(buttonHover, null, 0, 4));
        _button.AddThemeStyleboxOverride("pressed", Box(buttonHover, null, 0, 4));
        Color text = light ? Colors.Black : colour;
        foreach (string name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            _button.AddThemeColorOverride(name, light ? Colors.Black : name == "font_hover_color" ? ToColor(SayColours.Of(theme, 5)) : colour);
        var clear = new StyleBoxEmpty();
        _text.AddThemeStyleboxOverride("normal", clear);
        _text.AddThemeStyleboxOverride("focus", clear);
        _text.AddThemeColorOverride("font_color", text);
        _text.AddThemeColorOverride("caret_color", text);
        _text.AddThemeColorOverride("background_color", Colors.Transparent);
    }

    private static StyleBoxFlat Box(Color bg, Color? border, int borderWidth, int radius)
    {
        var box = new StyleBoxFlat { BgColor = bg };
        if (border is Color b)
        {
            box.BorderColor = b;
            box.SetBorderWidthAll(borderWidth);
        }
        box.SetCornerRadiusAll(radius);
        return box;
    }

    private static Color ToColor((float R, float G, float B) c) => new(c.R, c.G, c.B);

    private void OnTextChanged()
    {
        if (_syncing || _model == null || !_model.IsOpen)
            return;
        // the textarea has no line breaks of its own: Enter sends (a pasted one is a space)
        string typed = _text.Text.Replace("\r", "").Replace('\n', ' ');
        _model.Input(typed, Now());
        Sync();
    }

    // tg's handleKeyDown, before the text field's own handling.
    internal bool Key(InputEventKey key)
    {
        if (_model == null || !_model.IsOpen)
            return false;
        switch (key.Keycode)
        {
            case Godot.Key.Up:
            case Godot.Key.Down:
                _model.Arrow(key.Keycode == Godot.Key.Up);
                Sync();
                return true;
            case Godot.Key.Delete:
            case Godot.Key.Backspace:
                _model.BackspaceDelete();
                _button.Text = _model.ButtonContent;
                ApplyTheme(_model.Theme, _model.LightMode);
                return false; // the text field deletes as usual
            case Godot.Key.Enter:
            case Godot.Key.KpEnter:
                _model.Enter();
                Update(_scale);
                return true;
            case Godot.Key.Tab:
                _model.IncrementChannel(Now());
                Sync();
                return true;
            case Godot.Key.Escape:
                _model.Close();
                Update(_scale);
                return true;
        }
        return false;
    }

    // The channel button: a click cycles the channel (handleButtonRelease), a drag moves the window (dragStartHandler).
    private void OnButtonInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _pressed = true;
                _dragging = false;
                _pressAt = press.GlobalPosition;
                _button.AcceptEvent();
                break;
            case InputEventMouseMotion motion when _pressed:
                Drag(motion);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                _pressed = false;
                if (!_dragging && _model?.IsOpen == true)
                {
                    _model.IncrementChannel(Now());
                    Sync();
                }
                _dragging = false;
                _text.GrabFocus();
                break;
        }
    }

    // The window's frame: a drag moves it (tgui-say's window onMouseDown).
    private void OnDragInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } press:
                _pressed = press.Pressed;
                _pressAt = press.GlobalPosition;
                _dragging = false;
                if (!press.Pressed)
                    _text.GrabFocus();
                break;
            case InputEventMouseMotion motion when _pressed:
                Drag(motion);
                break;
        }
    }

    private void Drag(InputEventMouseMotion motion)
    {
        if (!_dragging && motion.GlobalPosition.DistanceTo(_pressAt) < 3f)
            return;
        _dragging = true;
        _frame.Position += motion.Relative;
        _position = _frame.Position / GetViewportRect().Size;
        Moved?.Invoke(_position.Value);
    }

    /// <summary>The text field: tg's keys go to the window before the field types them.</summary>
    private sealed partial class SayTextEdit : TextEdit
    {
        public SayWindow Owner_;

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true } key && Owner_.Key(key))
                AcceptEvent();
        }
    }
}
