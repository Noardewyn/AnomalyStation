using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4B-2: what the Tab inventory sends for a drag, a click and the quick transfer. Engine-free: no Godot API here (it
// is compiled into tests/Anomaly.Server.Tests too). The client decides nothing about whether a move is allowed: it names the item and the target in
// one InventoryMove (protocol 99, DV692) and shows what the server answers.
// DEVIATION(DV690): drags between the inventory's places are moves, not tg's MouseDrop of its screen objects; the quick transfer takes Ctrl+click.

/// <summary>What a place under the pointer is (<see cref="InvPlace"/>).</summary>
public enum InvKind : byte
{
    /// <summary>Nothing of the inventory (a tg entry that is no hand or slot, an open container's window, a part of the HUD of ours that holds nothing).</summary>
    None = 0,

    /// <summary>A hand box (<see cref="InvPlace.Index"/>: tg's held index).</summary>
    Hand,

    /// <summary>A belt cell of the bar (<see cref="InvPlace.Index"/>: 1 to 7; <see cref="InvPlace.Storage"/>: the worn belt, 0 for none).</summary>
    Belt,

    /// <summary>A pocket or ID cell of the bar or a worn slot's cell of the character panel (<see cref="InvPlace.Index"/>: tg's slot id).</summary>
    Slot,

    /// <summary>A storage panel's cell (<see cref="InvPlace.Storage"/>, <see cref="InvPlace.Index"/>: the panel's cell from 0).</summary>
    Cell,

    /// <summary>A storage panel's header or backing (<see cref="InvPlace.Storage"/>).</summary>
    Panel,

    /// <summary>The own sprite of the character panel.</summary>
    Sprite,

    /// <summary>The floor in the view: a turf, or nothing at all.</summary>
    Floor,

    /// <summary>A thing in the view (an entity).</summary>
    Thing,

    /// <summary>A HUD entry of tg's that is none of the above (the health doll, an open container's window, an action button).</summary>
    Hud,
}

/// <summary>
/// A place of the inventory and the item it shows (<see cref="Item"/>, 0 for none): a hand, a bar cell, a worn slot, a panel's cell or header, the own
/// sprite, or where a drag ended in the view. <see cref="Wish"/> is the cell a move onto it wishes for in its storage (a panel's or belt cell's item's
/// place in tg's contents from 1, 0 for none).
/// </summary>
public readonly record struct InvPlace(InvKind Kind, long Item = 0, int Index = 0, long Storage = 0, int Wish = 0)
{
    /// <summary>The same place (not the same item): a drag released here is released where it started.</summary>
    public bool SamePlace(in InvPlace other) => Kind == other.Kind && Index == other.Index && Storage == other.Storage;
}

/// <summary>What the Tab inventory sends (<see cref="InventoryRules"/>).</summary>
public enum InvSendKind : byte
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>One InventoryMove: <see cref="InvSend.Item"/> to <see cref="InvSend.To"/> (<see cref="InvSend.Storage"/>, <see cref="InvSend.Index"/>).</summary>
    Move,

    /// <summary>tg's drop of the drag (its MouseDrop), as cursor mode's drags were before the Tab inventory: a held item onto a thing in the view.</summary>
    TgDrop,

    /// <summary>A click on the own sprite where the drag ended (the item in the active hand used on oneself at that part).</summary>
    SelfUse,

    /// <summary>Nothing to send it to (a quick transfer into a backpack with none worn): the item's cell blinks red as for a refused move.</summary>
    Refused,

    /// <summary>tg's Shift+click on the item (its examine): a click with Shift on the item's entity as a screen target (the server takes it for an item its own carried storages show).</summary>
    Examine,
}

/// <summary>What one drag, click or quick transfer sends.</summary>
public readonly record struct InvSend(InvSendKind Kind, long Item = 0, InventoryTarget To = InventoryTarget.None, long Storage = 0, int Index = 0)
{
    public static readonly InvSend Nothing = default;

    public static InvSend Move(long item, InventoryTarget to, long storage = 0, int index = 0) => new(InvSendKind.Move, item, to, storage, index);
}

/// <summary>The Tab inventory's places and the rules that map a drag, a click and the quick transfer to what is sent (ui-overhaul.md section 12, slice 4B-2).</summary>
public static class InventoryRules
{
    /// <summary>
    /// The inventory place a HUD pick's <paramref name="target"/> names: the own sprite, a belt cell (its item: <paramref name="beltItems"/>, the worn
    /// belt <paramref name="belt"/>), a panel's cell or header (<paramref name="panels"/>), tg's hand box or worn, pocket or ID slot (the entry, or
    /// the item drawn in it, whose holder it is; <paramref name="screen"/>); <see cref="InvKind.Hud"/> for any other screen entry; a world thing is
    /// <see cref="InvKind.Thing"/>, a turf or nothing <see cref="InvKind.Floor"/>.
    /// </summary>
    public static InvPlace Resolve(in ClickTarget target, IReadOnlyDictionary<long, ScreenEntry> screen, IReadOnlyList<StoragePanel> panels, long belt, IReadOnlyList<long> beltItems,
        bool humanHud = true)
    {
        // the Tab inventory is the human HUD's (the server takes a move from a human only): on any other HUD nothing is an inventory place, so its
        // drags, clicks and Ctrl+clicks keep exactly the paths they had (tg's drop, tg's clicks)
        if (!humanHud)
            return new InvPlace(InvKind.None);
        switch (target.Kind)
        {
            case TargetKind.Entity:
                return new InvPlace(InvKind.Thing, Index: 0, Storage: 0, Item: target.Id);
            case TargetKind.Screen:
                break;
            default:
                return new InvPlace(InvKind.Floor);
        }
        long id = target.Id;
        if (id == TabInventory.SpriteId)
            return new InvPlace(InvKind.Sprite);
        if (LiveHud.BeltCellOf(target) is int cell and > 0)
        {
            long item = beltItems != null && cell <= beltItems.Count ? beltItems[cell - 1] : 0;
            // DV689, DV692: into the worn belt the wish is the bar's cell (the server gives the item that cell)
            return new InvPlace(InvKind.Belt, item, cell, belt, belt == 0 ? 0 : cell);
        }
        if (StorageColumn.Decode(id, out int panel, out int at))
        {
            if (panels == null || panel >= panels.Count)
                return new InvPlace(InvKind.None);
            StoragePanel p = panels[panel];
            if (at < 0)
                return new InvPlace(InvKind.Panel, Storage: p.Storage);
            if (at >= p.Cells.Length)
                return new InvPlace(InvKind.None);
            return new InvPlace(InvKind.Cell, p.Cells[at].Entity, at, p.Storage, p.Wishes[at]);
        }
        if (id < 0 || screen == null)
            return new InvPlace(InvKind.None);
        if (screen.TryGetValue(id, out ScreenEntry entry))
            return OfEntry(entry, ItemIn(entry));
        foreach (ScreenEntry holder in screen.Values)
        {
            if (holder.VisChildren != null && holder.VisChildren.Contains(id))
                return OfEntry(holder, id);
        }
        return new InvPlace(InvKind.None);
    }

    // a hand box or a worn slot of the inventory (the pockets and the ID slot are bar cells, the other worn slots the character panel's), any other
    // entry of tg's not one of them
    private static InvPlace OfEntry(in ScreenEntry entry, long item)
    {
        if (entry.Group == HudGroup.Storage)
            return new InvPlace(InvKind.Hud);
        if (entry.HandIndex > 0)
            return new InvPlace(InvKind.Hand, item, entry.HandIndex);
        if (entry.SlotId != 0 && (TabInventory.CellOf(entry.SlotId) >= 0 || LiveHud.BarCellOfSlot(entry.SlotId) > 0))
            return new InvPlace(InvKind.Slot, item, (int)entry.SlotId);
        return new InvPlace(InvKind.Hud);
    }

    /// <summary>The item a hand box or slot holds: the first item drawn in it (its vis_contents), 0 for none.</summary>
    public static long ItemIn(in ScreenEntry entry)
    {
        if (entry.VisChildren == null)
            return 0;
        foreach (ScreenChild c in entry.VisChildren.Items)
        {
            if (c.IsItem)
                return c.Id;
        }
        return 0;
    }

    /// <summary>
    /// What a drag of the item at <paramref name="from"/> released over <paramref name="over"/> sends, with the active hand <paramref name="activeHand"/>:
    /// <list type="bullet">
    /// <item><description>onto a hand box: to that hand; onto a belt cell: into the worn belt, wishing for that bar cell (the server gives the item that cell; no belt: nothing); onto a pocket, ID or
    /// worn slot's cell: onto that slot; onto a panel's cell: into that storage at that cell's item (an empty cell: simply in); onto a panel's header
    /// or backing: into that storage; each one move;</description></item>
    /// <item><description>onto the own sprite: an item from the active hand is used on oneself there (a click on the sprite); any other: nothing;</description></item>
    /// <item><description>onto the floor in the view: the move to the floor; onto a thing in the view, or onto a HUD entry of tg's that is no inventory
    /// place (the health doll, an open container's window): from one of tg's own entries (a hand box, a worn slot's, a pocket's or the ID slot's cell)
    /// tg's drop, as before this slice (D224, D225: a worn backpack dragged onto a table empties onto it); from one of our cells (a storage panel's, a
    /// belt cell of the bar), which never had a tg drag, nothing;</description></item>
    /// <item><description>released where it was pressed, from a place with no item, or anywhere else: nothing.</description></item>
    /// </list>
    /// </summary>
    public static InvSend Drop(in InvPlace from, in InvPlace over, int activeHand)
    {
        if (from.Item == 0 || from.Kind is not (InvKind.Hand or InvKind.Belt or InvKind.Slot or InvKind.Cell) || from.SamePlace(over))
            return InvSend.Nothing;
        switch (over.Kind)
        {
            case InvKind.Hand:
                return InvSend.Move(from.Item, InventoryTarget.Hand, 0, over.Index);
            case InvKind.Belt:
                return over.Storage == 0 ? InvSend.Nothing : InvSend.Move(from.Item, InventoryTarget.Storage, over.Storage, over.Wish);
            case InvKind.Slot:
                return InvSend.Move(from.Item, InventoryTarget.Slot, 0, over.Index);
            case InvKind.Cell:
                return InvSend.Move(from.Item, InventoryTarget.Storage, over.Storage, over.Wish);
            case InvKind.Panel:
                return InvSend.Move(from.Item, InventoryTarget.Storage, over.Storage, 0);
            case InvKind.Sprite:
                return from.Kind == InvKind.Hand && from.Index == activeHand ? new InvSend(InvSendKind.SelfUse, from.Item) : InvSend.Nothing;
            case InvKind.Floor:
                return InvSend.Move(from.Item, InventoryTarget.Floor);
            case InvKind.Thing:
            case InvKind.Hud:
                return from.Kind is InvKind.Hand or InvKind.Slot ? new InvSend(InvSendKind.TgDrop, from.Item) : InvSend.Nothing;
            default:
                return InvSend.Nothing;
        }
    }

    /// <summary>
    /// What a click (no drag) at <paramref name="at"/> sends, null where the click stays what it was before this slice (tg's click on a hand box or a
    /// slot, a belt cell's press): on a panel's item, the move to the active hand (as tg's click on it in tg's storage window); on an empty cell, or
    /// a header or backing, with an item <paramref name="held"/> in the active hand, that item into the storage there (an empty cell: simply in);
    /// Shift alone on a panel's item tg's Shift+click on it (its examine); any other click on a panel (another modifier held, an empty hand on an
    /// empty cell) nothing.
    /// </summary>
    public static InvSend? Click(in InvPlace at, ClickModifiers modifiers, int activeHand, long held)
    {
        if (at.Kind is not (InvKind.Cell or InvKind.Panel))
            return null;
        if (modifiers == ClickModifiers.Shift && at.Kind == InvKind.Cell && at.Item != 0)
            return new InvSend(InvSendKind.Examine, at.Item);
        if (modifiers != ClickModifiers.None)
            return InvSend.Nothing;
        if (at.Kind == InvKind.Cell && at.Item != 0)
            return activeHand > 0 ? InvSend.Move(at.Item, InventoryTarget.Hand, 0, activeHand) : InvSend.Nothing;
        return held != 0 ? InvSend.Move(held, InventoryTarget.Storage, at.Storage, 0) : InvSend.Nothing;
    }

    /// <summary>What the client's balloon says when a quick transfer from a hand has no backpack to go into (nothing is sent).</summary>
    public const string NoBackpack = "no backpack!";

    /// <summary>
    /// Whether a left press counts as the quick transfer: its binding (<paramref name="pressName"/>, the press with the modifiers held, as
    /// <see cref="KeyBindings.NameOf"/> writes it) is one of the action's keys and no other modifier is held (<paramref name="alt"/>: Alt, which a
    /// binding name cannot carry); Ctrl+Alt+click and any other press go their old way.
    /// </summary>
    public static bool IsQuickTransfer(KeyBindings bindings, string pressName, bool alt) => !alt && bindings != null && bindings.Has(PlayAction.QuickTransfer, pressName);

    /// <summary>
    /// The quick transfer on <paramref name="at"/> (ui-overhaul.md section 12: its own action, Ctrl+left click by default), null where it is no item of
    /// the inventory (the click stays tg's Ctrl-click): an item in a panel, a bar cell or a worn slot goes to the active hand; an item in a hand box
    /// into the backpack (<paramref name="back"/>: the storage worn on the back), or, with none, is refused; on a panel's header or empty cell nothing.
    /// </summary>
    public static InvSend? QuickTransfer(in InvPlace at, int activeHand, long back)
    {
        switch (at.Kind)
        {
            case InvKind.Cell or InvKind.Belt or InvKind.Slot when at.Item != 0:
                return activeHand > 0 ? InvSend.Move(at.Item, InventoryTarget.Hand, 0, activeHand) : InvSend.Nothing;
            case InvKind.Hand when at.Item != 0:
                return back != 0 ? InvSend.Move(at.Item, InventoryTarget.Storage, back, 0) : new InvSend(InvSendKind.Refused, at.Item);
            case InvKind.Cell or InvKind.Panel:
                return InvSend.Nothing;
            default:
                return null;
        }
    }

    /// <summary>
    /// Whether a drag pressed on the item at <paramref name="pressed"/> goes on, its place now being <paramref name="now"/> (the same pick resolved
    /// against the newest message): only while that place still shows that item; a message that took it away, moved it, or removed its storage
    /// ends the drag with nothing sent.
    /// </summary>
    public static bool DragHolds(in InvPlace pressed, in InvPlace now) => pressed.Item != 0 && now.Item == pressed.Item && pressed.SamePlace(now);

    /// <summary>
    /// The item whose cell blinks red now (0: none): the last refused move's item (<see cref="StoragesModel.RefusedItem"/>) for
    /// <see cref="UiTokens.HotbarRefusedSeconds"/> from when it came, wherever it is drawn.
    /// </summary>
    public static long Blinking(StoragesModel model, double now)
        => model != null && model.RefusedItem != 0 && now >= model.RefusedAt && now - model.RefusedAt < UiTokens.HotbarRefusedSeconds ? model.RefusedItem : 0;

    /// <summary>
    /// The rim of the inventory place <paramref name="place"/> showing <paramref name="item"/> (a hand box, a bar cell, a worn slot's cell, a panel's
    /// cell): red while that item's refused move blinks (<paramref name="blinking"/>, <see cref="Blinking"/>), else the light rim while a drag is over
    /// that place (<paramref name="over"/>), else none.
    /// </summary>
    public static uint Rim(long item, in InvPlace place, long blinking, in InvPlace over)
        => item != 0 && item == blinking ? UiTokens.HotbarRefusedRim
            : over.Kind is InvKind.Hand or InvKind.Belt or InvKind.Slot or InvKind.Cell && over.SamePlace(place) ? UiTokens.InventoryDragRim : 0u;

    /// <summary>
    /// How long after the player clicked a thing or used the held one, seconds, an empty storage window that opens counts as opened by it
    /// (<see cref="ListedWindow"/>'s <c>actedOn</c>): long enough for the server's answer to arrive, short enough that a later, unrelated window (a
    /// crate's) is not taken for it.
    /// </summary>
    public const double ActedOnSeconds = 2.0;

    /// <summary>
    /// Whether tg's storage window now open shows a storage the column lists (each thing is shown once, so it is not drawn and the Tab inventory comes
    /// on instead): one of its items (<paramref name="windowItems"/>) is in a listed storage, or, holding none, the thing the player last acted on
    /// (<paramref name="actedOn"/>: clicked, or used in hand) is a listed storage holding none. Any other container keeps tg's window.
    /// </summary>
    public static bool ListedWindow(IEnumerable<long> windowItems, IReadOnlyList<CarriedStorage> storages, long actedOn)
    {
        if (storages == null || storages.Count == 0)
            return false;
        bool any = false;
        foreach (long item in windowItems ?? Array.Empty<long>())
        {
            any = true;
            foreach (CarriedStorage s in storages)
            {
                foreach (StorageItem i in s.Items)
                {
                    if (i.Entity == item)
                        return true;
                }
            }
        }
        if (any || actedOn == 0)
            return false;
        foreach (CarriedStorage s in storages)
        {
            if (s.Entity == actedOn && s.Items.Count == 0)
                return true;
        }
        return false;
    }
}

/// <summary>
/// The "inventory opened" notice (protocol 99's InventoryOpen): sent once per turning the Tab inventory on with Tab or the inventory key, and no more
/// often than the server takes it (<see cref="Seconds"/>: the server's own limit, once a second, DV692).
/// </summary>
public sealed class InventoryNotice
{
    /// <summary>The least time between two notices, seconds: the server opens the storages at most once a second.</summary>
    public const double Seconds = 1.0;

    private double _last = double.NegativeInfinity;

    /// <summary>
    /// Tab or the inventory key (both are cursor mode's key, <see cref="TabInventory.KeyAction"/>) was pressed at <paramref name="now"/>, turning cursor
    /// mode on (<paramref name="turnedOn"/>) or off: whether the notice goes out: only when it turned on, on the human HUD (<paramref name="humanHud"/>:
    /// the Tab inventory), and a second after the last one at the soonest. Cursor mode that comes on by itself (the chat typed in, a window) is no
    /// press of the key and never asks.
    /// </summary>
    public bool Opened(bool turnedOn, bool humanHud, double now)
    {
        if (!turnedOn || !humanHud || now - _last < Seconds)
            return false;
        _last = now;
        return true;
    }
}
