using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// ui-overhaul.md section 15 (DV698): the actions menu and what the top-left row of slots shows over tg's button pictures. The row's pictures are tg's
/// action buttons themselves, re-placed by <see cref="HudLayer"/> into the slots (so a click on one in cursor mode is tg's click); this view draws over
/// them each slot's key cap, a toggle's highlight and a cooldown's wheel, and while an action is dragged the empty slots as places to drop it. The
/// menu (K) is a window in the middle of the screen in the storage panels' style: the nine slots at its top and every action the player has under
/// them in rows of nine, each its button's picture and its name. A click on an action uses it, a drag onto a slot assigns it, a right click on a
/// slot empties it (<see cref="ActionsMenuInput"/>).
/// </summary>
public partial class ActionsView : Control
{
    private readonly StyleBoxFlat _back = new(), _frame = new() { DrawCenter = false };
    private ReplicaWorld _replica;
    private IAppearanceTextures _textures;
    private ActionSlots _slots;
    private ActionsMessage _message = ActionsMessage.None;
    private readonly List<long> _buttons = new();
    private IReadOnlyList<HudRect> _row = Array.Empty<HudRect>();
    private float _unit = 1f, _cell = 48f, _scroll;
    private HudMetrics _metrics;
    private ActionsMenuLayout _layout;
    private double _tick, _seconds;
    private PlayOptions _options;
    private long _hovered;

    /// <summary>The mouse in the menu (its drag shows the empty slots).</summary>
    public ActionsMenuInput Mouse { get; } = new();

    /// <summary>Whether the menu is open.</summary>
    public bool Open { get; private set; }

    /// <summary>A click on an action: tg's click on its button (the screen object's id).</summary>
    public event Action<long> Use;

    /// <summary>The menu as laid out this frame (null while it is closed).</summary>
    public ActionsMenuLayout Layout => Open ? _layout : null;

    public ActionsView()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Opens or closes the menu (a press or a drag in it ends when it closes).</summary>
    public void SetOpen(bool open)
    {
        if (open == Open)
            return;
        Open = open;
        _scroll = 0f;
        _hovered = 0;
        Mouse.Cancel();
        QueueRedraw();
    }

    /// <summary>
    /// This frame's state: the replica and its pictures, the slots, the server's buttons (only those with a screen entry are listed), the row's
    /// slots and the bar's cell side and unit (from the HUD), the screen, the server tick now and the options (the slots' keys).
    /// </summary>
    public void Frame(ReplicaWorld replica, IAppearanceTextures textures, ActionSlots slots, IReadOnlyList<HudRect> row, float cell, float unit,
        in HudMetrics metrics, double tick, double seconds, PlayOptions options)
    {
        (_replica, _textures, _slots, _row, _cell, _unit, _metrics, _tick, _seconds, _options) = (replica, textures, slots, row ?? Array.Empty<HudRect>(), cell, unit, metrics, tick, seconds, options);
        _message = replica?.Actions ?? ActionsMessage.None;
        _buttons.Clear();
        _buttons.AddRange(ActionSlots.Drawable(_message, replica?.Screen));
        if (Mouse.Dragging && !_buttons.Contains(Mouse.Dragged))
            Mouse.Cancel(); // the dragged action went away
        _layout = Open ? ActionsMenu.Layout(metrics, cell, unit, _buttons.Count, _scroll) : null;
        _scroll = _layout?.Scroll ?? 0f;
        QueueRedraw();
    }

    // Only the window and the row's drawn slots take the mouse; elsewhere the pointer works on the HUD and the world as in cursor mode.
    public override bool _HasPoint(Vector2 point)
    {
        if (!Open || _layout == null)
            return false;
        if (ActionsMenu.Inside(_layout.Window, point.X, point.Y))
            return true;
        int slot = ActionsMenu.SlotAt(_row, point.X, point.Y);
        return slot != 0 && _slots.Drawn(slot, Mouse.Dragging);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Open || _layout == null || _slots == null)
            return;
        switch (@event)
        {
            case InputEventMouseMotion motion:
                Mouse.Move(motion.Position.X, motion.Position.Y);
                _hovered = ButtonAt(motion.Position.X, motion.Position.Y);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel:
                _scroll += (wheel.ButtonIndex == MouseButton.WheelDown ? 1f : -1f) * (_cell + _layout.NameHeight + MathF.Round(UiTokens.StorageCellGap * _unit));
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (left.Pressed)
                    Mouse.Press(ButtonAt(left.Position.X, left.Position.Y), left.Position.X, left.Position.Y);
                else
                {
                    (ActionsMenuOutcome outcome, long button) = Mouse.Release(SlotUnder(left.Position.X, left.Position.Y), _slots);
                    if (outcome == ActionsMenuOutcome.Use)
                        Use?.Invoke(button);
                }
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right:
                ActionsMenuInput.RightClick(SlotUnder(right.Position.X, right.Position.Y), _slots);
                AcceptEvent();
                break;
        }
    }

    // The slot under the point: the menu's or the row's.
    private int SlotUnder(float x, float y)
    {
        int slot = ActionsMenu.SlotAt(_layout.Slots, x, y);
        return slot != 0 ? slot : ActionsMenu.SlotAt(_row, x, y);
    }

    // The action under the point: a slot's, or a cell's of the list.
    private long ButtonAt(float x, float y)
    {
        int slot = SlotUnder(x, y);
        if (slot != 0)
            return _slots.ButtonIn(slot);
        int item = ActionsMenu.ItemAt(_layout, x, y);
        return item >= 0 && item < _buttons.Count ? _buttons[item] : 0;
    }

    private static Rect2 R(HudRect r) => new(r.Left, r.Top, r.Width, r.Height);

    private static int FontPx(float referencePixels, float u) => Math.Max(1, (int)Math.Round(referencePixels * u));

    public override void _Draw()
    {
        if (_slots == null)
            return;
        Font font = ThemeDB.FallbackFont;
        bool dragging = Mouse.Dragging;
        // the row: over tg's pictures each slot's states and key cap; while dragging the empty slots too
        for (int slot = 1; slot <= ActionSlots.Count && slot <= _row.Count; slot++)
        {
            if (!_slots.Drawn(slot, dragging))
                continue;
            HudRect cell = _row[slot - 1];
            long button = _slots.ButtonIn(slot);
            if (button == 0)
                EmptyCell(cell);
            else
                States(cell, button, font, picture: false);
            Cap(font, cell, slot, button == 0 ? UiTokens.HotbarEmptyCapAlpha : 1f);
        }
        if (Open && _layout != null)
            DrawMenu(font, dragging);
        if (dragging)
            Picture(new HudRect(Mouse.X - _cell / 2f, Mouse.Y - _cell / 2f, _cell, _cell), Mouse.Dragged, 0.8f);
    }

    private void DrawMenu(Font font, bool dragging)
    {
        ActionsMenuLayout l = _layout;
        _back.BgColor = ChatLines.Rgba(UiTokens.StoragePanelBack);
        _back.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.DollPanelRadius * _unit)));
        DrawStyleBox(_back, R(l.Window));
        int titleSize = FontPx(UiTokens.StorageHeaderFont, _unit);
        string title = _hovered != 0 ? _message.Find(_hovered)?.Name ?? ActionsTitle : ActionsTitle;
        title = LiveHud.HintName(title, l.Title.Width, t => font.GetStringSize(t, HorizontalAlignment.Left, -1, titleSize).X);
        float baseline = l.Title.Top + (l.Title.Height + font.GetAscent(titleSize) - font.GetDescent(titleSize)) / 2f;
        if (!string.IsNullOrEmpty(title))
            DrawString(font, new Vector2(l.Title.Left, baseline), title, HorizontalAlignment.Left, -1, titleSize, ChatLines.Rgba(UiTokens.LocalText));
        // the nine slots: an empty one only while dragging, as in the row
        for (int slot = 1; slot <= ActionSlots.Count; slot++)
        {
            HudRect cell = l.Slots[slot - 1];
            long button = _slots.ButtonIn(slot);
            if (button != 0)
                States(cell, button, font, picture: true);
            else if (dragging)
                EmptyCell(cell);
            if (button != 0 || dragging)
                Cap(font, cell, slot, button == 0 ? UiTokens.HotbarEmptyCapAlpha : 1f);
        }
        // the list, cut at its rectangle, each action's picture and its name under it
        int nameSize = FontPx(UiTokens.SwapCapFont, _unit);
        if (_buttons.Count == 0)
        {
            float y = l.List.Top + (l.List.Height + font.GetAscent(nameSize)) / 2f;
            DrawString(font, new Vector2(l.List.Left, y), NoActions, HorizontalAlignment.Center, l.List.Width, nameSize, ChatLines.Rgba(UiTokens.LocalText, 0.6f));
            return;
        }
        for (int i = 0; i < _buttons.Count && i < l.Items.Length; i++)
        {
            HudRect cell = l.Items[i];
            if (cell.Bottom + l.NameHeight <= l.List.Top || cell.Top >= l.List.Bottom)
                continue; // scrolled out
            if (cell.Top < l.List.Top || cell.Bottom + l.NameHeight > l.List.Bottom)
                continue; // a row cut by the edge is not drawn half: the list scrolls by whole rows
            States(cell, _buttons[i], font, picture: true);
            string name = _message.Find(_buttons[i])?.Name;
            float pitch = cell.Width + MathF.Round(UiTokens.StorageCellGap * _unit);
            string cut = LiveHud.HintName(name, pitch, t => font.GetStringSize(t, HorizontalAlignment.Left, -1, nameSize).X);
            if (!string.IsNullOrEmpty(cut))
                DrawString(font, new Vector2(cell.Left - (pitch - cell.Width) / 2f, cell.Bottom + font.GetAscent(nameSize) + MathF.Round(_unit)), cut,
                    HorizontalAlignment.Center, pitch, nameSize, ChatLines.Rgba(UiTokens.LocalText));
        }
        if (l.MaxScroll > 0f)
        {
            // the scroll bar's thumb at the list's right edge
            float bar = MathF.Max(1f, MathF.Round(UiTokens.StorageScrollBar * _unit));
            float content = l.List.Height + l.MaxScroll, thumb = MathF.Max(bar * 4, l.List.Height * l.List.Height / content);
            float at = l.List.Top + (l.List.Height - thumb) * (l.Scroll / l.MaxScroll);
            DrawRect(new Rect2(l.Window.Right - bar - MathF.Round(_unit * 2), at, bar, thumb), ChatLines.Rgba(UiTokens.StorageScrollThumb));
        }
    }

    /// <summary>The menu's title while no action is hovered.</summary>
    public const string ActionsTitle = "Actions";

    /// <summary>What the list says with no actions.</summary>
    public const string NoActions = "No actions";

    // An empty slot while dragging: the panels' dark backing and a light rim, under the drop
    private void EmptyCell(HudRect cell)
    {
        _back.BgColor = ChatLines.Rgba(UiTokens.StoragePanelBack);
        _back.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * _unit)));
        DrawStyleBox(_back, R(cell));
        Rim(cell, UiTokens.InventoryDragRim, Math.Max(1, (int)Math.Round(_unit)));
    }

    // An action's square: its picture (in the menu; the row's is tg's own entry), a toggle's highlight, a cooldown's wheel and tg's count
    private void States(HudRect cell, long button, Font font, bool picture)
    {
        ActionButtonState? state = _message.Find(button);
        if (picture)
        {
            _back.BgColor = ChatLines.Rgba(UiTokens.StoragePanelBack);
            _back.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * _unit)));
            DrawStyleBox(_back, R(cell));
            Picture(cell, button, 1f);
        }
        if (state?.Active == true)
        {
            DrawRect(R(cell), ChatLines.Rgba(UiTokens.ActionActiveFill));
            Rim(cell, UiTokens.ActionActiveRim, Math.Max(1, (int)Math.Round(UiTokens.ActionActiveRimWidth * _unit)));
        }
        float left = state?.CooldownLeft(_tick) ?? 0f;
        if (left > 0f)
            Wheel(cell, left);
        if (picture && _replica != null && _replica.Screen.TryGetValue(button, out ScreenEntry entry) && ActionsMenu.CountOf(_replica, entry.Appearance) is string count)
        {
            int size = FontPx(UiTokens.SwapCapFont, _unit);
            float inset = MathF.Round(UiTokens.SwapCapInset * _unit);
            DrawStringOutline(font, new Vector2(cell.Left + inset, cell.Bottom - inset), count, HorizontalAlignment.Left, -1, size, Math.Max(1, (int)Math.Round(3 * _unit)), Colors.Black);
            DrawString(font, new Vector2(cell.Left + inset, cell.Bottom - inset), count, HorizontalAlignment.Left, -1, size, ChatLines.Rgba(UiTokens.LocalText));
        }
    }

    // A cooldown: a dark wheel over the square from twelve o'clock, clockwise over the share still to run, so it shrinks as the cooldown runs down
    private void Wheel(HudRect cell, float left)
    {
        var center = new Vector2(cell.Left + cell.Width / 2f, cell.Top + cell.Height / 2f);
        float hw = cell.Width / 2f, hh = cell.Height / 2f;
        int steps = Math.Max(2, (int)MathF.Ceiling(360f * left));
        var points = new Vector2[steps + 2];
        points[0] = center;
        for (int i = 0; i <= steps; i++)
        {
            float angle = -MathF.PI / 2f + 2f * MathF.PI * left * i / steps;
            float dx = MathF.Cos(angle), dy = MathF.Sin(angle);
            float t = MathF.Min(MathF.Abs(dx) > 1e-4f ? hw / MathF.Abs(dx) : float.MaxValue, MathF.Abs(dy) > 1e-4f ? hh / MathF.Abs(dy) : float.MaxValue);
            points[i + 1] = center + new Vector2(dx, dy) * t; // on the square's edge
        }
        DrawColoredPolygon(points, ChatLines.Rgba(UiTokens.ActionCooldownShade));
    }

    private void Rim(HudRect cell, uint rgba, int width)
    {
        _frame.BorderColor = ChatLines.Rgba(rgba);
        _frame.SetBorderWidthAll(width);
        _frame.SetCornerRadiusAll(Math.Max(1, (int)Math.Round(UiTokens.ChipRadius * _unit)));
        DrawStyleBox(_frame, R(cell));
    }

    // Slot N's key (Shift+N by default) as the bar's key cap in the cell's top left corner
    private void Cap(Font font, HudRect cell, int slot, float alpha)
    {
        IReadOnlyList<string> keys = _options?.Bindings.KeysOf(PlayAction.ActionButton1 + (byte)(slot - 1));
        if (keys == null || keys.Count == 0)
            return;
        string label = KeyChips.Label(keys[0]);
        int size = FontPx(UiTokens.SwapCapFont, _unit);
        HudRect cap = LiveHud.CellCap(cell, _unit, font.GetStringSize(label, HorizontalAlignment.Left, -1, size).X + 2 * UiTokens.SwapCapInset * _unit);
        if (cap.Width > 0f)
            LiveHudView.DrawCap(this, font, cap, label, size, _unit, alpha);
    }

    // tg's button picture (its background, icon and overlays, tinted red while unavailable) fitted to the square as the HUD fits an icon
    private void Picture(HudRect cell, long button, float alpha)
    {
        if (_replica == null || _textures == null || !_replica.Screen.TryGetValue(button, out ScreenEntry entry)
            || !_textures.TryCompose(_replica, entry.Appearance, 2, _seconds, out ComposedAppearance composed))
            return;
        HudRect at = ScreenLoc.FitIcon(cell, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY);
        DrawTextureRect(composed.Texture, R(at), false, new Color(1f, 1f, 1f, alpha));
    }
}
