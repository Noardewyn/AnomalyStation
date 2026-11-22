using Godot;

namespace Anomaly.Client.Hud;

/// <summary>
/// A scroll container that the mouse wheel and the trackpad (a pan gesture on macOS) scroll wherever the pointer is over it
/// (the owner, 2026-10-13: "the panels with scroll bars scroll with the mouse wheel"). Godot's own handling stops at a child
/// that takes the mouse (a button, a check box, the console's selectable log), so the panel takes those events before the
/// controls see them.
/// </summary>
public partial class WheelScrollContainer : ScrollContainer
{
    // Godot's own step (scroll_container.cpp): an eighth of the page per wheel notch or per unit of a pan gesture.
    private const float PageShare = 1f / 8f;

    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree())
            return;
        float steps;
        Vector2 at;
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                float notches = wheel.Factor > 0 ? wheel.Factor : 1f;
                steps = (wheel.ButtonIndex == MouseButton.WheelUp ? -1f : 1f) * notches;
                at = wheel.Position;
                break;
            case InputEventPanGesture pan:
                steps = pan.Delta.Y;
                at = pan.Position;
                break;
            default:
                return;
        }
        if (!GetGlobalRect().HasPoint(at))
            return;
        VScrollBar bar = GetVScrollBar();
        if (bar == null || bar.MaxValue - bar.Page <= bar.MinValue)
            return; // nothing to scroll: the event is the game's
        float dy = steps * (float)bar.Page * PageShare;
        ScrollVertical = (int)Math.Clamp(ScrollVertical + dy, bar.MinValue, bar.MaxValue - bar.Page);
        GetViewport().SetInputAsHandled();
    }
}
