using Godot;

namespace Anomaly.Client.Hud;

/// <summary>
/// The corner a window is resized by (the owner, 2026-10-20: the game's windows can be resized): dragged with the left button, it reports how far the mouse
/// moved in screen pixels and when the drag ended; the window does the rest.
/// </summary>
public partial class ResizeGrip : Control
{
    private bool _dragging;
    private Vector2 _last;

    /// <summary>The mouse moved by this many screen pixels while the grip is held.</summary>
    public event Action<Vector2> Dragged;

    /// <summary>The grip was let go.</summary>
    public event Action Released;

    public ResizeGrip()
    {
        CustomMinimumSize = new Vector2(16, 16);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Fdiagsize;
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.ShrinkEnd;
        TooltipText = "Drag to resize";
    }

    public override void _Draw()
    {
        var color = new Color(1, 1, 1, 0.45f);
        Vector2 s = Size;
        for (int i = 1; i <= 3; i++)
            DrawLine(new Vector2(s.X - i * 4, s.Y - 1), new Vector2(s.X - 1, s.Y - i * 4), color, 1.5f);
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                _last = GetViewport().GetMousePosition();
                if (!button.Pressed)
                    Released?.Invoke();
                AcceptEvent();
                break;
            case InputEventMouseMotion when _dragging:
                Vector2 now = GetViewport().GetMousePosition();
                Dragged?.Invoke(now - _last);
                _last = now;
                AcceptEvent();
                break;
        }
    }
}
