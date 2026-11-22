using Godot;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud;

/// <summary>
/// D282 (the owner, 2026-10-27): the frame of the game's own windows, as the tgui windows' (TguiHost) and the chat window's: a title bar
/// with the title and a close button that drags the window anywhere on the screen, a corner grip that resizes it, and the place and size
/// kept in the options file (<c>window.NAME</c>, <c>window_size.NAME</c>, pixels on <see cref="PlayOptions.WindowScreen"/>) for the next start.
/// The owner (2026-10-28): the window keeps its share of the screen, place and size, when the game's window is resized.
/// </summary>
public sealed class WindowChrome
{
    private readonly Control _window;
    private readonly string _name;
    private readonly PlayOptions _options;
    private readonly Action _saved;
    private bool _dragging;
    private Vector2 _dragLast;
    private Vector2 _at, _size; // saved pixels (PlayOptions.WindowScreen)
    private Viewport _following;

    /// <summary>The smallest the grip makes the window.</summary>
    public Vector2 MinSize { get; set; } = new(200, 120);

    /// <param name="window">The window: placed from the top left of the screen by this frame.</param>
    /// <param name="name">Its key in the options file.</param>
    /// <param name="saved">Called when a drag or a resize ended, to write the options file.</param>
    public WindowChrome(Control window, string name, PlayOptions options, Action saved)
    {
        _window = window;
        _name = name;
        _options = options;
        _saved = saved;
    }

    /// <summary>The title bar to put at the top of the window's content: dragging it moves the window, its button calls <paramref name="close"/>.</summary>
    public Control TitleBar(string title, Action close)
    {
        var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.Move, CustomMinimumSize = new Vector2(0, (float)TguiWindows.TitleBarHeight) };
        bar.AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.TitleBarBg, 0));
        DragBy(bar);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        Label label = TguiRender.Text("  " + title, null, false, 14, null);
        label.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(label);
        var button = new Button { Text = "✕", FocusMode = Control.FocusModeEnum.None, Flat = true, CustomMinimumSize = new Vector2(32, 0), TooltipText = "Close" };
        button.AddThemeColorOverride("font_color", TguiTheme.TitleText);
        button.AddThemeColorOverride("font_hover_color", TguiTheme.Hex("#ff6060"));
        TguiRender.BareCloseButton(button);
        button.Pressed += close;
        row.AddChild(button);
        bar.AddChild(row);
        return bar;
    }

    /// <summary>
    /// Dragging <paramref name="handle"/> with the left button moves the window (the title bar's drag; D315: the minimap, which has no bar), a
    /// press only where <paramref name="grabs"/> says so when given (the handle's own pixels).
    /// </summary>
    public void DragBy(Control handle, Func<Vector2, bool> grabs = null) => handle.GuiInput += e =>
    {
        if (grabs == null || _dragging || e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press && grabs(press.Position))
            OnBarInput(handle, e);
    };

    /// <summary>The corner grip to put at the bottom right of the window's content.</summary>
    public ResizeGrip Grip()
    {
        var grip = new ResizeGrip();
        grip.Dragged += delta =>
        {
            Vector2 screen = _window.GetViewportRect().Size;
            _window.Size = new Vector2(Mathf.Clamp(_window.Size.X + delta.X, MinSize.X, Math.Max(MinSize.X, screen.X)),
                Mathf.Clamp(_window.Size.Y + delta.Y, MinSize.Y, Math.Max(MinSize.Y, screen.Y)));
        };
        grip.Released += () =>
        {
            _size = Saved(_window.Size);
            _options.WindowSizes[_name] = ((int)MathF.Round(_size.X), (int)MathF.Round(_size.Y));
            _saved();
        };
        return grip;
    }

    /// <summary>Puts the window where it was left (else at <paramref name="defaultPosition"/>) at the size it was left (else <paramref name="defaultSize"/>).</summary>
    public void Place(Vector2 defaultPosition, Vector2 defaultSize)
    {
        _window.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _size = _options.WindowSizes.TryGetValue(_name, out (int Width, int Height) s) ? new Vector2(s.Width, s.Height) : Saved(defaultSize);
        _at = _options.WindowPositions.TryGetValue(_name, out (int X, int Y) p) ? new Vector2(p.X, p.Y) : Saved(defaultPosition);
        if (_following == null)
        {
            _following = _window.GetViewport();
            _following.SizeChanged += Show;
        }
        Show();
    }

    private void Show()
    {
        if (!GodotObject.IsInstanceValid(_window))
        {
            _following.SizeChanged -= Show; // the scene is gone (a reparent, as the lobby's, keeps it)
            return;
        }
        if (!_window.IsInsideTree())
            return;
        _window.Size = OnScreen(_size);
        _window.Position = OnScreen(_at);
        Keep();
    }

    private Vector2 OnScreen(Vector2 saved)
    {
        Vector2 screen = _window.GetViewportRect().Size;
        (double x, double y) = _options.WindowOnScreen(saved.X, saved.Y, screen.X, screen.Y);
        return new Vector2((float)x, (float)y);
    }

    private Vector2 Saved(Vector2 onScreen)
    {
        Vector2 screen = _window.GetViewportRect().Size;
        (int w, int h) = _options.WindowScreenFor(screen.X, screen.Y);
        return onScreen * new Vector2(w / screen.X, h / screen.Y);
    }

    private void Keep()
    {
        Vector2 screen = _window.GetViewportRect().Size;
        (double x, double y) = TguiWindows.KeepOnScreen(_window.Position.X, _window.Position.Y, _window.Size.X, screen.X, screen.Y, 28, 1);
        _window.Position = new Vector2((float)x, (float)y);
    }

    private void OnBarInput(Control bar, InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                _dragLast = bar.GetViewport().GetMousePosition();
                if (!button.Pressed)
                {
                    _at = Saved(_window.Position);
                    _options.WindowPositions[_name] = ((int)MathF.Round(_at.X), (int)MathF.Round(_at.Y));
                    _saved();
                }
                bar.AcceptEvent();
                break;
            case InputEventMouseMotion when _dragging:
                Vector2 now = bar.GetViewport().GetMousePosition();
                _window.Position += now - _dragLast;
                _dragLast = now;
                Keep();
                bar.AcceptEvent();
                break;
        }
    }
}
