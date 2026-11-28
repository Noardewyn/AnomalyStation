using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 15 (DV698): the actions menu's nine slots and its layout. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). The slots are the client's own: they name tg's action buttons (the server's Actions message, protocol version 100),
// and using a slot is tg's click on its button. Nothing is kept across a reconnect or a round: a slot lasts while the player has its action.

/// <summary>
/// The nine slots of the actions menu, Shift+1 to Shift+9: which of tg's action buttons each holds (0: empty). A button the player gets that was never
/// seen before takes the first empty slot by itself; a button that goes away leaves its slot empty; a drop onto a slot swaps (the action that was there
/// goes to the dropped one's old slot, or out of the row when the dropped one came from the list).
/// </summary>
public sealed class ActionSlots
{
    /// <summary>How many slots there are.</summary>
    public const int Count = 9;

    private readonly long[] _slots = new long[Count];
    private readonly HashSet<long> _known = new();

    /// <summary>The button in slot <paramref name="slot"/> (1 to 9), 0 for an empty slot or one out of range.</summary>
    public long ButtonIn(int slot) => slot is >= 1 and <= Count ? _slots[slot - 1] : 0;

    /// <summary>The slot (1 to 9) holding <paramref name="button"/>, 0 for none.</summary>
    public int SlotOf(long button) => button == 0 ? 0 : Array.IndexOf(_slots, button) + 1;

    /// <summary>
    /// The buttons the player has now (<paramref name="buttons"/>, in tg's order): a slot whose button is gone is emptied, and a button not seen before
    /// takes the first empty slot (none when the nine are full; it is still in the menu's list).
    /// </summary>
    public void Sync(IEnumerable<long> buttons)
    {
        var now = new List<long>(buttons ?? Array.Empty<long>());
        var present = new HashSet<long>(now);
        for (int i = 0; i < Count; i++)
        {
            if (_slots[i] != 0 && !present.Contains(_slots[i]))
                _slots[i] = 0;
        }
        _known.IntersectWith(present);
        foreach (long button in now)
        {
            if (button == 0 || !_known.Add(button) || SlotOf(button) != 0)
                continue;
            int free = Array.IndexOf(_slots, 0L);
            if (free >= 0)
                _slots[free] = button;
        }
    }

    /// <summary>
    /// <paramref name="button"/> dropped onto slot <paramref name="slot"/>: it goes there; the button that was there goes to its old slot (a swap), or
    /// out of the row when it came from the list. Nothing for a button the player does not have or a slot out of range.
    /// </summary>
    public void Assign(long button, int slot)
    {
        if (slot is < 1 or > Count || !_known.Contains(button))
            return;
        int from = SlotOf(button);
        long there = _slots[slot - 1];
        _slots[slot - 1] = button;
        if (from != 0 && from != slot)
            _slots[from - 1] = there;
    }

    /// <summary>Empties slot <paramref name="slot"/> (a right click on it); its action stays in the list.</summary>
    public void Clear(int slot)
    {
        if (slot is >= 1 and <= Count)
            _slots[slot - 1] = 0;
    }

    /// <summary>Whether slot <paramref name="slot"/> is drawn: a slot with an action always, an empty one only while an action is being dragged.</summary>
    public bool Drawn(int slot, bool dragging) => ButtonIn(slot) != 0 || dragging && slot is >= 1 and <= Count;

    /// <summary>The buttons of <paramref name="message"/> the client can draw (it has their screen entries), in the message's order.</summary>
    public static List<long> Drawable(ActionsMessage message, IReadOnlyDictionary<long, ScreenEntry> screen)
    {
        var buttons = new List<long>();
        foreach (ActionButtonState b in message?.Buttons ?? Array.Empty<ActionButtonState>())
        {
            if (screen == null || screen.ContainsKey(b.Button))
                buttons.Add(b.Button);
        }
        return buttons;
    }
}

/// <summary>What a press and release in the actions menu did (<see cref="ActionsMenuInput"/>).</summary>
public enum ActionsMenuOutcome : byte
{
    None = 0,

    /// <summary>A click on an action (in the list or a slot): tg's click on its button.</summary>
    Use,

    /// <summary>An action dragged onto a slot.</summary>
    Assigned,

    /// <summary>A right click emptied a slot.</summary>
    Cleared,
}

/// <summary>
/// The mouse in the actions menu: a left press on an action (in the list or in a slot) and its release without moving is a click that uses it; moved
/// past <see cref="DragStart"/> pixels it is a drag, and released over a slot it assigns the action there (<see cref="ActionSlots.Assign"/>), anywhere
/// else nothing. A right click on a slot empties it.
/// </summary>
public sealed class ActionsMenuInput
{
    /// <summary>How far the pointer moves with the button down before the press is a drag, screen pixels.</summary>
    public const float DragStart = 6f;

    private long _pressed;
    private float _pressX, _pressY;

    /// <summary>The action being dragged (0: none).</summary>
    public long Dragged { get; private set; }

    /// <summary>Whether an action is being dragged (the empty slots are drawn then, as places to drop it).</summary>
    public bool Dragging => Dragged != 0;

    /// <summary>The pointer's last place.</summary>
    public float X { get; private set; }

    /// <inheritdoc cref="X"/>
    public float Y { get; private set; }

    /// <summary>The left button went down on <paramref name="button"/> (0: on nothing that is an action).</summary>
    public void Press(long button, float x, float y)
    {
        (_pressed, _pressX, _pressY, X, Y, Dragged) = (button, x, y, x, y, 0);
    }

    /// <summary>The pointer moved.</summary>
    public void Move(float x, float y)
    {
        (X, Y) = (x, y);
        if (_pressed != 0 && Dragged == 0 && MathF.Abs(x - _pressX) + MathF.Abs(y - _pressY) > DragStart)
            Dragged = _pressed;
    }

    /// <summary>The left button came up over slot <paramref name="slotUnder"/> (1 to 9, 0 for none): what that did, and the action it did it with.</summary>
    public (ActionsMenuOutcome Outcome, long Button) Release(int slotUnder, ActionSlots slots)
    {
        long pressed = _pressed, dragged = Dragged;
        (_pressed, Dragged) = (0, 0);
        if (dragged != 0)
        {
            if (slotUnder == 0)
                return (ActionsMenuOutcome.None, 0);
            slots.Assign(dragged, slotUnder);
            return (ActionsMenuOutcome.Assigned, dragged);
        }
        return pressed != 0 ? (ActionsMenuOutcome.Use, pressed) : (ActionsMenuOutcome.None, 0);
    }

    /// <summary>A right click over slot <paramref name="slotUnder"/>: an assigned slot is emptied.</summary>
    public static ActionsMenuOutcome RightClick(int slotUnder, ActionSlots slots)
    {
        if (slots.ButtonIn(slotUnder) == 0)
            return ActionsMenuOutcome.None;
        slots.Clear(slotUnder);
        return ActionsMenuOutcome.Cleared;
    }

    /// <summary>The menu closed or the action went away: no press, no drag.</summary>
    public void Cancel() => (_pressed, Dragged) = (0, 0);
}

/// <summary>Where the actions menu's parts are this frame, screen pixels (<see cref="ActionsMenu.Layout"/>).</summary>
/// <param name="Window">The window's backing.</param>
/// <param name="Title">Its title line.</param>
/// <param name="Slots">The nine slots under the title, slot 1 first.</param>
/// <param name="List">Where the list of actions is seen (its cells are cut at it).</param>
/// <param name="Items">The list's cells (each with its name under it), scrolled.</param>
/// <param name="NameHeight">The height of the name line under each cell.</param>
/// <param name="Scroll">How far the list is scrolled (0 to <paramref name="MaxScroll"/>).</param>
/// <param name="MaxScroll">How far it can be scrolled.</param>
public sealed record ActionsMenuLayout(HudRect Window, HudRect Title, HudRect[] Slots, HudRect List, HudRect[] Items, float NameHeight, float Scroll, float MaxScroll);

/// <summary>The actions menu's layout and the top-left row of slots (ui-overhaul.md section 15).</summary>
public static class ActionsMenu
{
    /// <summary>The list's cells in one row, as the slots.</summary>
    public const int Columns = ActionSlots.Count;

    /// <summary>The share of the screen's height the window may take at most; a longer list scrolls.</summary>
    public const float MaxHeightShare = 0.7f;

    /// <summary>
    /// The nine slots of the top-left row in first person: cells of <paramref name="cell"/> pixels, <see cref="UiTokens.StorageCellGap"/> apart, from
    /// <see cref="UiTokens.ScreenMargin"/> in from the screen's top left corner, at <paramref name="unit"/> screen pixels per reference pixel.
    /// </summary>
    public static HudRect[] Row(float cell, float unit)
    {
        float margin = MathF.Round(UiTokens.ScreenMargin * unit), gap = MathF.Round(UiTokens.StorageCellGap * unit);
        var row = new HudRect[ActionSlots.Count];
        for (int i = 0; i < row.Length; i++)
            row[i] = new HudRect(margin + i * (cell + gap), margin, cell, cell);
        return row;
    }

    /// <summary>
    /// The window in the middle of a screen of <paramref name="metrics"/>: a backing (<see cref="UiTokens.StoragePanelPad"/> round its content), a title
    /// line, the nine slots in a row (cells of <paramref name="cell"/>, <see cref="UiTokens.StorageCellGap"/> apart), and under them the list of
    /// <paramref name="actions"/> actions in rows of nine, each a cell with its name under it (one cell's room when there are none). The window is as
    /// tall as its content up to <see cref="MaxHeightShare"/> of the screen; past that the list is seen in what is left and scrolls by
    /// <paramref name="scroll"/>, kept from 0 to the overflow. Everything at whole pixels.
    /// </summary>
    public static ActionsMenuLayout Layout(in HudMetrics metrics, float cell, float unit, int actions, float scroll)
    {
        float pad = MathF.Round(UiTokens.StoragePanelPad * unit * 2f), gap = MathF.Round(UiTokens.StorageCellGap * unit);
        float title = MathF.Round(UiTokens.StorageHeaderFont * unit * 1.6f), name = MathF.Round(UiTokens.SwapCapFont * unit * 1.5f);
        float width = 2 * pad + Columns * cell + (Columns - 1) * gap;
        int rows = Math.Max(1, (actions + Columns - 1) / Columns);
        float pitch = cell + name + gap;
        float content = rows * pitch;
        float fixedPart = pad + title + cell + 3 * gap + pad;
        float height = Math.Min(fixedPart + content, Math.Max(fixedPart + pitch, MathF.Floor(metrics.Height * MaxHeightShare)));
        float left = MathF.Round((metrics.Width - width) / 2f), top = MathF.Round((metrics.Height - height) / 2f);
        var window = new HudRect(left, top, width, height);
        var titleRect = new HudRect(left + pad, top + pad, width - 2 * pad, title);
        var slots = new HudRect[ActionSlots.Count];
        float slotTop = titleRect.Bottom;
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new HudRect(left + pad + i * (cell + gap), slotTop, cell, cell);
        float listTop = slotTop + cell + 3 * gap;
        var list = new HudRect(left + pad, listTop, width - 2 * pad, Math.Max(0f, window.Bottom - pad - listTop));
        float max = Math.Max(0f, content - list.Height);
        scroll = Math.Clamp(scroll, 0f, max);
        var items = new HudRect[Math.Max(0, actions)];
        for (int i = 0; i < items.Length; i++)
            items[i] = new HudRect(list.Left + i % Columns * (cell + gap), listTop + i / Columns * pitch - scroll, cell, cell);
        return new ActionsMenuLayout(window, titleRect, slots, list, items, name, scroll, max);
    }

    /// <summary>The slot (1 to 9) of <paramref name="slots"/> under the point, 0 for none.</summary>
    public static int SlotAt(IReadOnlyList<HudRect> slots, float x, float y)
    {
        for (int i = 0; i < (slots?.Count ?? 0); i++)
        {
            if (Inside(slots[i], x, y))
                return i + 1;
        }
        return 0;
    }

    /// <summary>The list's cell (from 0) under the point, within the part of the list that is seen; -1 for none.</summary>
    public static int ItemAt(ActionsMenuLayout layout, float x, float y)
    {
        if (layout == null || !Inside(layout.List, x, y))
            return -1;
        for (int i = 0; i < layout.Items.Length; i++)
        {
            HudRect r = layout.Items[i];
            if (x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom + layout.NameHeight)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// tg's count on an action button: the plain text of the button's own maptext (tg writes a charge, a growth or a cooldown's seconds there,
    /// <c>button.maptext</c>), drawn in the square's bottom corner; null for none.
    /// </summary>
    public static string CountOf(ReplicaWorld replica, TableRef appearance)
    {
        if (replica == null || !replica.TryGetAppearance(appearance, out AppearanceEntry look) || look.Maptext.IsNone)
            return null;
        string text = HtmlText.ToPlain(replica.ResolveString(look.Maptext))?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Whether the point is in <paramref name="r"/> (its left and top edges in, its right and bottom out).</summary>
    public static bool Inside(in HudRect r, float x, float y) => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
}
