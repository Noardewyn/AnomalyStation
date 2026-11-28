using System.Linq;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4D-1: the Tab inventory's strip view on the human HUD. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). While tg's StripMenu window is open (strippable.dm's ui_data, as the client already receives it), the native window
// is not drawn and the Tab inventory shows the storages column in the middle and the strip panel at the right: the target's sprite with the worn
// slots round it in the own panel's arrangement, a row of the hands (and the cuffs when tg shows them), a row of the ID and the pockets. Every click
// and drag on it is tg's own act on that window (`use`, `alt`) or tg's close; Shift+click on the sprite is the world's examine of the target.
// DEVIATION(DV694): the strip panel is not tg's window; the in-slot bar shows that tg marks the slot `interacting`, not how far the action is.

/// <summary>What a strip slot shows (tg's <c>ObscuringLevel</c> and whether the window names an item there).</summary>
public enum StripLook : byte
{
    /// <summary>Nothing there (tg's payload has no item).</summary>
    Empty = 0,

    /// <summary>An item tg names (its picture and name).</summary>
    Item,

    /// <summary>tg's <c>STRIPPABLE_OBSCURING_COMPLETELY</c>: the slot is hidden and cannot be used (tg's "ban" mark).</summary>
    Blocked,

    /// <summary>tg's <c>STRIPPABLE_OBSCURING_HIDDEN</c>: something is there, unseen (a full pocket: a "?"); it can be used.</summary>
    Hidden,
}

/// <summary>
/// One slot of tg's strip window as the client has it: its key (strippable.dm's <c>STRIPPABLE_ITEM_*</c>), what it shows, whether tg shows it but
/// lets nobody touch it (<c>STRIPPABLE_OBSCURING_INACCESSIBLE</c>), the item's name and icon (DV150: the icon file and state), tg's alternate
/// actions offered on it and whether tg marks it as being worked on by the player (<c>interacting</c>).
/// </summary>
public sealed record StripSlot(string Key, StripLook Look, bool Inaccessible, string Name, string Icon, string IconState, IReadOnlyList<string> Alternates, bool Interacting)
{
    /// <summary>Something is in it (an item tg names, or a hidden pocket's).</summary>
    public bool Full => Look is StripLook.Item or StripLook.Hidden;

    /// <summary>tg takes a click on it (its ui_act refuses a completely obscured or inaccessible slot).</summary>
    public bool Usable => Look != StripLook.Blocked && !Inaccessible;

    /// <summary>The tooltip as tg's window words it: the item's name; "obscured left pocket" for a hidden or blocked slot; the slot's name when empty.</summary>
    public string Tooltip => Look switch
    {
        StripLook.Item => Name,
        StripLook.Blocked or StripLook.Hidden => "obscured " + (StripMenu.DisplayName(Key) ?? Key),
        _ => StripMenu.DisplayName(Key) ?? Key,
    };
}

/// <summary>tg's strip window as the strip view shows it: the window, the target's name, whether tg's status is interactive, and the slots by <see cref="StripView.Keys"/>.</summary>
public sealed class StripModel
{
    private readonly StripSlot[] _slots;

    private StripModel(int window, string name, bool interactive, StripSlot[] slots)
    {
        (Window, Name, Interactive, _slots) = (window, name, interactive, slots);
    }

    /// <summary>The tgui window's pool index (the acts and the close go to it).</summary>
    public int Window { get; }

    /// <summary>tg's <c>name</c> of the window's data (<c>"\the [owner]"</c>).</summary>
    public string Name { get; }

    /// <summary>tg's ui status is interactive (UI_INTERACTIVE); otherwise the slots take no clicks (too far, hands blocked).</summary>
    public bool Interactive { get; }

    /// <summary>The panel's title.</summary>
    public string Title => "Stripping: " + Name;

    /// <summary>The slot of <see cref="StripView.Keys"/>' index <paramref name="index"/>; null where tg's window shows none.</summary>
    public StripSlot At(int index) => index >= 0 && index < _slots.Length ? _slots[index] : null;

    /// <summary>The most alternate actions kept for a slot (tg's window draws three).</summary>
    public const int MaxAlternates = 8;

    /// <summary>
    /// The model of tg's StripMenu window <paramref name="window"/> from its merged data (strippable.dm:349 ui_data: <c>items</c> keyed by slot,
    /// each <c>{obscured, interacting?, icon?, icon_state?, name?, alternate?}</c>, and <c>name</c>) and its ui status (2: interactive). A key tg's
    /// window does not place is left out.
    /// </summary>
    public static StripModel From(int window, int status, UiValue data)
    {
        var slots = new StripSlot[StripView.Keys.Length];
        foreach ((string key, UiValue value) in data.Obj("items"))
        {
            int index = StripView.IndexOf(key);
            if (index < 0)
                continue;
            bool none = value == null || value.IsNull;
            int obscured = none ? 0 : (int)value.Get("obscured").AsNum(0);
            bool named = !none && value.Get("name") != null; // tg's StripMenu.tsx: 'name' in item
            StripLook look = named ? StripLook.Item : obscured == 1 ? StripLook.Blocked : obscured == 2 ? StripLook.Hidden : StripLook.Empty;
            string[] alternates = named
                ? value.Arr("alternate").Select(a => a.AsStr(null)).Where(a => !string.IsNullOrEmpty(a)).Take(MaxAlternates).ToArray()
                : Array.Empty<string>();
            slots[index] = new StripSlot(key, look, obscured == 3, named ? value.Str("name") : null, named ? value.Str("icon", null) : null,
                named ? value.Str("icon_state", null) : null, alternates, !none && value.Truthy("interacting"));
        }
        return new StripModel(window, data.Str("name"), status >= 2, slots);
    }
}

/// <summary>Where the strip view's parts go this frame (<see cref="StripView.Layout"/>), screen pixels.</summary>
/// <param name="Storages">Where the storages column stands (the middle column); empty where it gives way (a screen too narrow for both).</param>
/// <param name="Panel">The strip panel's backing, which cuts everything drawn in it.</param>
/// <param name="Title">Its title row ("Stripping: name").</param>
/// <param name="Close">The X at the title row's right; empty where the row has no room for it.</param>
/// <param name="Sprite">The square the target's 32 x 32 icon square fills.</param>
/// <param name="SpriteScale">Screen pixels per tg pixel of the sprite: a whole number.</param>
/// <param name="Cell">The cells' side.</param>
/// <param name="Cells">Each slot's cell by <see cref="StripView.Keys"/>' index; empty for a slot tg's window does not show.</param>
public sealed record StripLayout(HudRect Storages, HudRect Panel, HudRect Title, HudRect Close, HudRect Sprite, int SpriteScale, float Cell, HudRect[] Cells)
{
    /// <summary>No strip panel.</summary>
    public static readonly StripLayout None = new(default, default, default, default, default, 0, 0f, new HudRect[StripView.Keys.Length]);

    /// <summary>Whether the panel is placed.</summary>
    public bool Shown => Panel.Width > 0f && Panel.Height > 0f;
}

/// <summary>Which part of the strip panel a pick id names (<see cref="StripView.Decode"/>).</summary>
public enum StripPart : byte
{
    None = 0,
    Panel,
    Close,
    Sprite,
    Slot,
}

/// <summary>The strip view's keys, layout, pick ids and the target it shows (ui-overhaul.md section 12, slice 4D-1).</summary>
public static class StripView
{
    /// <summary>tg's interface of the strip window.</summary>
    public const string Interface = "StripMenu";

    /// <summary>
    /// tg's strip keys (__DEFINES/strippable.dm) in the panel's order: the twelve worn slots in the own panel's (<see cref="TabInventory.CellOf"/>):
    /// ears, head, eyes, mask, neck, suit, jumpsuit, gloves, back, suit storage, belt, shoes; then the hands' row (the left and the right hand, the
    /// handcuffs and the legcuffs), the ID and the pockets; last an animal's headset and collar, which stand in the ears' and the neck's cells.
    /// </summary>
    public static readonly string[] Keys =
    {
        "ears", "head", "eyes", "mask", "neck", "suit", "jumpsuit", "gloves", "back", "suit_storage", "belt", "shoes",
        "left_hand", "right_hand", "handcuffs", "legcuffs", "id", "left_pocket", "right_pocket", "parrot_headset", "pet_collar",
    };

    private const int LeftHand = 12, RightHand = 13, Handcuffs = 14, Legcuffs = 15, Id = 16, LeftPocket = 17, RightPocket = 18;

    // tg's slot pictures (_onclick/hud/human.dm: icon_state empty, icon_full full) by key; the hands keep theirs, the cuffs have none of their own
    private static readonly string[] EmptyArt =
    {
        "ears", "head", "glasses", "mask", "neck", "suit", "uniform", "gloves", "back", "suit_storage", "belt", "shoes",
        "hand_l", "hand_r", LiveHud.BlankSlotState, LiveHud.BlankSlotState, "id", "pocket", "pocket", "ears", "neck",
    };

    private static readonly string[] FullArt =
    {
        "template", "template", "template", "template", "template", "template", "template", "template", "template_small", "template", "template_small", "template",
        "hand_l", "hand_r", LiveHud.BlankSlotState, LiveHud.BlankSlotState, "template_small", "template_small", "template_small", "template", "template",
    };

    /// <summary>The index of tg's strip key <paramref name="key"/> in <see cref="Keys"/>; -1 for one the panel does not place.</summary>
    public static int IndexOf(string key) => key == null ? -1 : Array.IndexOf(Keys, key);

    /// <summary>The own panel's worn cell (0 to 11) the key of <paramref name="index"/> stands in; -1 for a key of the rows.</summary>
    public static int WornCell(int index) => index switch
    {
        < 12 and >= 0 => index,
        19 => 0, // parrot_headset: the ears
        20 => 4, // pet_collar: the neck
        _ => -1,
    };

    /// <summary>tg's slot picture's icon state for the key of <paramref name="index"/>, empty or holding something (<paramref name="full"/>).</summary>
    public static string ArtState(int index, bool full) => index >= 0 && index < Keys.Length ? (full ? FullArt : EmptyArt)[index] : null;

    /// <summary>The strip window the strip view shows: the last opened of tg's StripMenu windows (<paramref name="open"/>: bottom first, as opened), null for none.</summary>
    public static TguiWindow Current(IEnumerable<TguiWindow> open)
    {
        TguiWindow last = null;
        foreach (TguiWindow w in open ?? Array.Empty<TguiWindow>())
        {
            if (w.Interface == Interface && w.Context != null)
                last = w;
        }
        return last;
    }

    /// <summary>The model of <paramref name="window"/> (<see cref="StripModel.From"/>), null for none.</summary>
    public static StripModel ModelOf(TguiWindow window) => window?.Context == null ? null : StripModel.From(window.Index, window.Status, window.Context.Data);

    // ---- the layout ----

    /// <summary>
    /// The strip view's places in the Tab inventory's middle column <paramref name="middle"/> (the storages) and right column <paramref name="right"/>
    /// (the strip panel), cut clear of what stands there (<see cref="TabInventory.Layout"/>), with the own panel's cell <paramref name="cell"/>, its first
    /// sprite scale <paramref name="firstScale"/> and <paramref name="unit"/> screen pixels per reference pixel. The panel, top to bottom: a backing
    /// (<see cref="UiTokens.StoragePanelPad"/> round its content) with the title row and the X, the target's sprite with the worn cells in the own
    /// panel's arrangement (<see cref="TabInventory.Arrangement"/>), the hands' row (the cuffs at its end when tg shows them) and the row of the ID
    /// and the pockets, the rows centred. Where it is taller than the column the sprite steps down a whole number at a time; at a scale of 1 the
    /// cells stay their size and the bottom rows are cut by the panel. Where it is wider than the right column even at a scale of 1, it takes both
    /// columns (<paramref name="both"/>: the two as one, cut clear of what stands there, <see cref="TabInventoryLayout.Both"/>) and the storages are
    /// not placed; narrower still, the cells are made smaller (down to <see cref="UiTokens.TabCellMinPixels"/>), then what does not fit is cut.
    /// Everything at whole pixels.
    /// </summary>
    public static StripLayout Layout(HudRect middle, HudRect right, HudRect both, float cell, int firstScale, float unit, StripModel model)
    {
        if (model == null)
            return StripLayout.None;
        cell = MathF.Floor(cell);
        firstScale = Math.Max(1, firstScale);
        if (Place(right, cell, firstScale, unit, model, force: false) is StripLayout r)
            return r with { Storages = middle };
        for (float side = cell; side >= UiTokens.TabCellMinPixels; side--)
        {
            if (Place(both, side, side == cell ? firstScale : 1, unit, model, force: false) is StripLayout s)
                return s;
        }
        return Place(both, Math.Max(1f, Math.Min(cell, UiTokens.TabCellMinPixels)), 1, unit, model, force: true) ?? StripLayout.None;
    }

    // The panel in `area` with cells of `side`, the sprite as large as fits from `firstScale` down; null where it is wider than the area even at a
    // scale of 1 (unless `force`: then placed at a scale of 1 and cut).
    private static StripLayout Place(HudRect area, float side, int firstScale, float unit, StripModel model, bool force)
    {
        if (area.Width <= 0f || area.Height <= 0f || side <= 0f)
            return null;
        float pad = Math.Max(1f, MathF.Round(UiTokens.StoragePanelPad * unit)), title = StorageColumn.HeaderHeight(side, unit);
        var hands = new List<int> { LeftHand, RightHand };
        if (model.At(Handcuffs) != null)
            hands.Add(Handcuffs);
        if (model.At(Legcuffs) != null)
            hands.Add(Legcuffs);
        int[] ids = { Id, LeftPocket, RightPocket };
        for (int s = firstScale; s >= 1; s--)
        {
            if (!TabInventory.Arrangement(s, side, out HudRect[] at, out HudRect bounds, out float gap, out float spriteGap))
                continue;
            float row = Math.Max(hands.Count, ids.Length) * side + (Math.Max(hands.Count, ids.Length) - 1) * gap;
            float width = Math.Max(bounds.Width, row), height = title + pad + bounds.Height + spriteGap + side + gap + side;
            if (width > area.Width - 2 * pad && !(force && s == 1))
                continue;
            if (height > area.Height - 2 * pad && s > 1)
                continue;
            float panelHeight = Math.Min(area.Height, pad + height + pad);
            var panel = new HudRect(area.Left, area.Top, area.Width, panelHeight);
            var titleRow = new HudRect(area.Left + pad, area.Top + pad, Math.Max(0f, area.Width - 2 * pad), title);
            HudRect close = titleRow.Width >= 2 * title ? new HudRect(titleRow.Right - title, titleRow.Top, title, title) : default;
            float top = titleRow.Bottom + pad;
            float x = MathF.Floor(area.Left + (area.Width - bounds.Width) / 2f) - bounds.Left, y = top - bounds.Top;
            var cells = new HudRect[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                int worn = WornCell(i);
                if (worn >= 0 && model.At(i) != null)
                    cells[i] = new HudRect(x + at[worn].Left, y + at[worn].Top, side, side);
            }
            float handsTop = top + bounds.Height + spriteGap;
            Row(hands, handsTop);
            Row(ids, handsTop + side + gap);
            float p = HudMetrics.IconPixels * s;
            return new StripLayout(default, panel, titleRow, close, new HudRect(x, y, p, p), s, side, cells);

            void Row(IReadOnlyList<int> keys, float rowTop)
            {
                float left = MathF.Floor(area.Left + (area.Width - (keys.Count * side + (keys.Count - 1) * gap)) / 2f);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (model.At(keys[i]) != null)
                        cells[keys[i]] = new HudRect(left + i * (side + gap), rowTop, side, side);
                }
            }
        }
        return null;
    }

    // ---- the pick ids ----

    /// <summary>The HUD pick's id of the strip panel's backing and title: ours, below zero (<see cref="LiveHud.IsOurs"/>) and clear of the other Tab inventory ids.</summary>
    public const long PanelId = -5000;

    /// <summary>The HUD pick's id of the panel's X.</summary>
    public const long CloseId = -5001;

    /// <summary>The HUD pick's id of the target's sprite.</summary>
    public const long SpriteId = -5002;

    private const long FirstSlotId = -5010;

    /// <summary>The HUD pick's id of the slot of <see cref="Keys"/>' index <paramref name="index"/>.</summary>
    public static long SlotId(int index) => FirstSlotId - index;

    /// <summary>The part of the panel (and for a slot its index) a pick id names; false for any other id.</summary>
    public static bool Decode(long id, out StripPart part, out int index)
    {
        index = -1;
        part = id switch
        {
            PanelId => StripPart.Panel,
            CloseId => StripPart.Close,
            SpriteId => StripPart.Sprite,
            <= FirstSlotId when FirstSlotId - id < Keys.Length => StripPart.Slot,
            _ => StripPart.None,
        };
        if (part == StripPart.Slot)
            index = (int)(FirstSlotId - id);
        return part != StripPart.None;
    }

    /// <summary>
    /// What the HUD pick gets for the strip panel: its backing under <see cref="PanelId"/>, the sprite under <see cref="SpriteId"/>, each shown slot's
    /// cell under <see cref="SlotId"/> and the X under <see cref="CloseId"/>, every one cut at the panel (a cell cut off by it is picked only on its part
    /// inside).
    /// </summary>
    public static void Drawn(StripLayout layout, List<HudDrawn> into)
    {
        if (layout == null || !layout.Shown)
            return;
        into.Add(new HudDrawn(PanelId, layout.Panel, null, 0, 0));
        if (StorageColumn.Cut(layout.Sprite, layout.Panel) is HudRect sprite)
            into.Add(new HudDrawn(SpriteId, sprite, null, 0, 0));
        for (int i = 0; i < layout.Cells.Length; i++)
        {
            if (layout.Cells[i].Width > 0f && StorageColumn.Cut(layout.Cells[i], layout.Panel) is HudRect cell)
                into.Add(new HudDrawn(SlotId(i), cell, null, 0, 0));
        }
        if (layout.Close.Width > 0f && StorageColumn.Cut(layout.Close, layout.Panel) is HudRect close)
            into.Add(new HudDrawn(CloseId, close, null, 0, 0));
    }

    /// <summary>The <see cref="InvPlace.Item"/> a full strip slot's place carries (tg sends no entity for the target's items): ours, never sent.</summary>
    public const long FullMark = long.MinValue;

    /// <summary>
    /// Whether a drop the client sent is tg's way of opening a strip window (<paramref name="dragged"/>, a mob (<paramref name="draggedIsMob"/>) other
    /// than oneself, dropped onto the own mob <paramref name="own"/>: the wheel's Strip, a body dragged onto the doll) while a strip window is already
    /// open (<paramref name="stripOpen"/>) and the Tab inventory is off (<paramref name="tabOn"/>) on the human HUD (<paramref name="humanHud"/>): tg
    /// brings its window forward again and opens no new one, so the Tab inventory turns on at once (as slice 4C-2's reopened container does).
    /// </summary>
    public static bool ReopensStrip(long dragged, long over, long own, bool draggedIsMob, bool stripOpen, bool tabOn, bool humanHud)
        => humanHud && !tabOn && stripOpen && own != 0 && over == own && dragged != 0 && dragged != own && draggedIsMob;

    // ---- the target ----

    /// <summary>
    /// Whether a mob's name <paramref name="mobName"/> is the one tg's window names (<paramref name="windowName"/>, tg's <c>"\the [owner]"</c>: a
    /// proper name as it is, any other with "the " before it).
    /// </summary>
    public static bool Names(string windowName, string mobName)
    {
        if (string.IsNullOrEmpty(windowName) || string.IsNullOrEmpty(mobName))
            return false;
        return windowName == mobName || windowName.Length == mobName.Length + 4 && windowName.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            && windowName.EndsWith(mobName, StringComparison.Ordinal);
    }

    /// <summary>
    /// The mob tg's window strips, as far as the client can tell (the window's data names no entity): the mob the player last dragged onto
    /// themselves (<paramref name="dragged"/>: tg's way of opening it, the radial's Strip and a body dragged onto the doll) when its name is the
    /// window's; else the one mob next to the player (<paramref name="mobs"/>: id, name and distance in tiles) of that name; 0 where none or several
    /// are.
    /// </summary>
    public static long TargetOf(string windowName, long dragged, IEnumerable<(long Id, string Name, int Distance)> mobs)
    {
        long near = 0;
        int matches = 0;
        foreach ((long id, string name, int distance) in mobs ?? Array.Empty<(long, string, int)>())
        {
            if (!Names(windowName, name))
                continue;
            if (id == dragged && dragged != 0)
                return id;
            if (distance <= 1)
            {
                near = id;
                matches++;
            }
        }
        return matches == 1 ? near : 0;
    }
}

/// <summary>
/// Slice 4D-1: turns the Tab inventory on once for each strip window that opens: only on the human HUD, where the window is taken only then (so a strip
/// window still open at join turns it on once that HUD is up); a window that closed is forgotten, so its index opening again counts again.
/// </summary>
public sealed class StripOpenings
{
    private readonly HashSet<int> _seen = new();

    /// <summary>Given the strip windows open now (<paramref name="open"/>), whether one is new to the human HUD (<paramref name="humanHud"/>).</summary>
    public bool TakeNew(IEnumerable<int> open, bool humanHud)
    {
        var now = new HashSet<int>(open ?? Array.Empty<int>());
        _seen.IntersectWith(now);
        if (!humanHud)
            return false;
        bool fresh = false;
        foreach (int window in now)
            fresh |= _seen.Add(window);
        return fresh;
    }
}
