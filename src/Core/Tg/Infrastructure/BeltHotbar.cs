using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV693): the belt hotbar (ui-overhaul.md section 10, package 3). Not in tg: a human's ten hotbar cells,
// 1 to 7 the worn belt's contents, 8 and 9 the left and right pocket, 10 (the key 0) the ID slot. A press of a cell takes its item into the
// active hand, puts it back, swaps it with an item taken from another cell, or stores the held item in an empty cell, every move by tg's own
// procs as tg's quick-equip keys make them (human/inventory.dm smart_equip_targeted: the belt's storage datum's attempt_insert and the stored
// item's on_found and attack_hand; the slot's equip_to_slot_if_possible), and a worn item into a hand as tg's drag of it onto the hand does
// (inventory.dm putItemFromInventoryInHandIfPossible), so tg's checks, sounds, delays and messages apply. Which belt item is in which cell is the
// port's own state on the mob (dm_hotbar), brought up to date in the command phase and before every press, and read by the replicator for the
// client's cells (protocol version 100, Hotbar).

/// <summary>What a hotbar cell shows (ui-overhaul.md section 10).</summary>
public enum HotbarState : byte
{
    /// <summary>No item in the cell and none out from it.</summary>
    Empty = 0,

    /// <summary>The cell's item is in the belt or in the cell's slot.</summary>
    Holding = 1,

    /// <summary>The cell's item was taken from the bar and is in one of the player's hands.</summary>
    Out = 2,
}

/// <summary>A human's ten hotbar cells and which item is in each (DV693).</summary>
public sealed class BeltHotbar
{
    /// <summary>The cells: 1 to 7 the belt, 8 and 9 the pockets, 10 the ID slot.</summary>
    public const int CellCount = 10;

    /// <summary>The belt's cells (tg's toolbelt and medical belt hold seven items).</summary>
    public const int BeltCells = 7;

    private readonly obj_item[] _items = new obj_item[CellCount];
    private readonly bool[] _taken = new bool[CellCount]; // the cell's item was taken from the bar (so it stays the cell's while in a hand)
    private obj_item _belt;

    /// <summary>How many presses were refused so far (the replicator sends a refusal once per count).</summary>
    public int Refusals { get; private set; }

    /// <summary>The cell (1 to 10) of the last refused press, 0 before the first.</summary>
    public int RefusedCell { get; private set; }

    /// <summary>The world tick the cells were made in (a reader that first sees them in that tick counts their refusals from 0), -1 for a copy.</summary>
    public long CreatedTick { get; private init; } = -1;

    // The mob's cells, made inside the world.
    private static BeltHotbar Of(mob_living_carbon_human user) => user.dm_hotbar ??= new BeltHotbar { CreatedTick = World.Current.Tick };

    /// <summary>The belt the seven belt cells were filled from (a worn belt-slot item with a storage), null with none.</summary>
    public obj_item Belt => _belt;

    /// <summary>The item of cell <paramref name="cell"/> (1 to 10), in its container or out, null for an empty cell.</summary>
    public obj_item ItemOf(int cell) => cell is >= 1 and <= CellCount ? _items[cell - 1] : null;

    /// <summary>What cell <paramref name="cell"/> (1 to 10) shows for <paramref name="user"/>, after <see cref="Reconcile"/>.</summary>
    public HotbarState StateOf(mob_living_carbon_human user, int cell)
    {
        obj_item item = ItemOf(cell);
        if (item == null)
            return HotbarState.Empty;
        return InContainer(user, cell - 1, item) ? HotbarState.Holding : HotbarState.Out;
    }

    /// <summary>The cell (1 to 10) <paramref name="item"/> is out from, 0 when it is not out from any.</summary>
    public int OutCellOf(mob_living_carbon_human user, obj_item item)
    {
        if (item == null)
            return 0;
        for (int c = 1; c <= CellCount; c++)
        {
            if (_items[c - 1] == item && StateOf(user, c) == HotbarState.Out)
                return c;
        }
        return 0;
    }

    /// <summary>
    /// Brings the cells up to date with the worn belt, the pockets, the ID slot and the hands (ui-overhaul.md section 10, "Which belt item is in
    /// which cell"): a new belt (or none) empties the seven belt cells and its contents take the free ones in tg's storage order; an item keeps its
    /// cell while it is in the belt (or the slot) or, taken from the bar, in a hand; anything else frees it; a slot's item is its cell's. Reads
    /// tg's state only.
    /// </summary>
    public void Reconcile(mob_living_carbon_human user)
    {
        obj_item belt = WornBelt(user);
        if (belt != _belt)
        {
            for (int c = 0; c < BeltCells; c++)
                Clear(c);
            _belt = belt;
        }
        for (int c = 0; c < BeltCells; c++)
        {
            obj_item item = _items[c];
            if (item == null)
                continue;
            if (InContainer(user, c, item))
                _taken[c] = false;
            else if (!_taken[c] || !Held(user, item))
                Clear(c);
        }
        if (belt != null)
        {
            foreach (obj_item item in Dm.each<obj_item>(belt.atom_storage.real_location.contents))
            {
                if (Array.IndexOf(_items, item, 0, BeltCells) >= 0)
                    continue;
                int free = Array.IndexOf(_items, null, 0, BeltCells);
                if (free < 0)
                    break; // beyond seven: reached under Tab
                _items[free] = item;
                _taken[free] = false;
            }
        }
        for (int c = BeltCells; c < CellCount; c++)
        {
            obj_item worn = user.get_item_by_slot(SlotOf(c)) as obj_item;
            if (worn != null)
            {
                if (_items[c] != worn)
                    _items[c] = worn;
                _taken[c] = false; // shortcut: the slot's own item wins over an item out from it, the cell shows one item
            }
            else if (_items[c] != null && (!_taken[c] || !Held(user, _items[c])))
                Clear(c);
        }
    }

    /// <summary>
    /// DV693, DV696 (ui-overhaul.md section 12, slice 4B-2): an item moved into the worn belt with the wish for bar cell <paramref name="cell"/> (1 to
    /// 7) takes that cell once it is in the belt: an empty cell simply; a cell holding another belt item swaps with the moved item's own cell when it had
    /// one (an item out from a cell in a hand keeps that cell until it is back); an item new to the bar takes the cell and the item that was there
    /// the lowest free one, or, with none free (a belt of more than seven), goes off the bar. A wish for the cell it already has, an item not in the
    /// worn belt, a cell out of range, or a cell whose item another press or move is moving (<see cref="CarriedStorages.IsClaimed"/>) or which
    /// is out in a hand changes nothing: the moved item stays in the belt where tg's insert and the bar put it, as with no wish.
    /// </summary>
    public static void PlaceInCell(mob_living_carbon_human user, obj_item item, int cell)
    {
        BeltHotbar bar = Of(user);
        bar.Reconcile(user);
        if (cell is < 1 or > BeltCells || item == null || bar._belt?.atom_storage == null || item.loc != bar._belt.atom_storage.real_location)
            return;
        int to = cell - 1, from = Array.IndexOf(bar._items, item, 0, BeltCells);
        if (from == to)
            return;
        (obj_item other, bool otherTaken) = (bar._items[to], bar._taken[to]);
        // an item another press or move is moving, or one out of the cell in a hand (section 10: it keeps its cell while held), keeps its cell: the bar
        // is not rearranged for this wish, and the moved item keeps the cell it got as with no wish (the lowest free one, or none)
        if (other != null && (otherTaken || CarriedStorages.IsClaimed(user, other)))
            return;
        if (from >= 0)
            (bar._items[from], bar._taken[from]) = (other, other != null && otherTaken);
        else if (other != null && Array.IndexOf(bar._items, null, 0, BeltCells) is int free and >= 0)
            (bar._items[free], bar._taken[free]) = (other, otherTaken);
        (bar._items[to], bar._taken[to]) = (item, false);
    }

    /// <summary>A copy brought up to date (<see cref="Reconcile"/>), for a reader outside the world: the mob's own cells are not changed.</summary>
    public static BeltHotbar Peek(mob_living_carbon_human user)
    {
        var copy = new BeltHotbar();
        if (user.dm_hotbar is BeltHotbar kept)
        {
            Array.Copy(kept._items, copy._items, CellCount);
            Array.Copy(kept._taken, copy._taken, CellCount);
            copy._belt = kept._belt;
            copy.Refusals = kept.Refusals;
            copy.RefusedCell = kept.RefusedCell;
        }
        copy.Reconcile(user);
        return copy;
    }

    /// <summary>The command phase's update of a human's cells (inside the world), so items enter the belt's cells in the order they came.</summary>
    public static void Track(mob_living_carbon_human user)
    {
        if (user != null && !QDELETED(user))
            Of(user).Reconcile(user);
    }

    /// <summary>
    /// A press of cell <paramref name="cell"/> from <paramref name="player"/>'s client (protocol version 100's HotbarPress): only a connected client's
    /// own human mob, a cell of 1 to 10; anything else does nothing. Runs detached with usr the mob, as BYOND runs a verb.
    /// </summary>
    public static void Press(client player, int cell)
    {
        if (player == null || !player.dm_connected || player.gc_destroyed != null || player.mob is not mob_living_carbon_human user || QDELETED(user)
            || cell is < 1 or > CellCount)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = user;
            Waiting.Detach(PressAsync(user, cell));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// The five rules of a press (ui-overhaul.md section 10, "What a press of cell N does"), refused (nothing changes, the client is told) when the
    /// mob is dead or incapacitated (tg's smart_equip_targeted: <c>if(incapacitated) return</c>), one of its items is being moved by an inventory
    /// move, no rule applies, or tg's procs refuse the move.
    /// </summary>
    public static async DmTask PressAsync(mob_living_carbon_human user, int cell)
    {
        BeltHotbar bar = Of(user);
        bar.Reconcile(user);
        // DV696: a press and an inventory move (CarriedStorages) never move the same item at once
        obj_item cellItem = bar.ItemOf(cell), held = user.get_active_held_item() as obj_item;
        if (CarriedStorages.Claim(user, cellItem, held))
        {
            try
            {
                if (await bar.Apply(user, cell))
                    return;
            }
            finally
            {
                CarriedStorages.Release(user, cellItem, held);
            }
        }
        bar.Refusals++;
        bar.RefusedCell = cell;
    }

    private async DmTask<bool> Apply(mob_living_carbon_human user, int cell)
    {
        // tg: code/modules/mob/living/carbon/human/inventory.dm:386 (smart_equip_targeted: incapacitated; blocked hands and a mech are left to
        // what tg's procs it calls refuse by themselves)
        if (user.stat == DEAD || Dm.truthy(user.incapacitated))
            return false;
        obj_item held = user.get_active_held_item() as obj_item;
        HotbarState state = StateOf(user, cell);
        // 1. the cell's item is out, in either hand: it goes back into the cell
        if (state == HotbarState.Out)
            return await PutBack(user, cell);
        // 2. an empty active hand and an item in the cell: it comes into the active hand
        if (state == HotbarState.Holding && held == null)
            return await TakeOut(user, cell);
        // 3. the held item is out from another cell: it goes back there, then this cell's item comes into the hand; nothing if it cannot go back
        if (state == HotbarState.Holding && OutCellOf(user, held) is int from and > 0)
        {
            if (!await PutBack(user, from))
                return false;
            await TakeOut(user, cell);
            return true;
        }
        // 4. an item in the active hand and an empty cell: stored there if tg lets it in
        if (state == HotbarState.Empty && held != null)
            return await Store(user, cell, held);
        // 5. anything else is refused
        return false;
    }

    // The cell's out item back into its belt or slot.
    private async DmTask<bool> PutBack(mob_living_carbon_human user, int cell)
    {
        obj_item item = _items[cell - 1];
        if (item == null || QDELETED(item))
            return false;
        if (!await Insert(user, cell, item))
            return false;
        Assign(cell, item);
        return true;
    }

    // The held item into an empty cell's belt or slot.
    private async DmTask<bool> Store(mob_living_carbon_human user, int cell, obj_item held)
    {
        if (!await Insert(user, cell, held))
            return false;
        Assign(cell, held);
        return true;
    }

    // tg's smart_equip_targeted (human/inventory.dm:385) putting the item in: the belt's storage datum's attempt_insert with its messages, or
    // equip_to_slot_if_possible into the pocket or the ID slot.
    private async DmTask<bool> Insert(mob_living_carbon_human user, int cell, obj_item item)
    {
        if (cell <= BeltCells)
        {
            if (!BeltUsable(user, out obj_item belt))
                return false;
            // tg: code/modules/mob/living/carbon/human/inventory.dm:415
            if (!Dm.truthy(belt.atom_storage.attempt_insert(item, user)))
            {
                to_chat(user, span_warning(I18n.T("You can't fit [] into your []!", item, belt.name)));
                return false;
            }
            return true;
        }
        object slot = SlotOf(cell - 1);
        // tg: code/modules/mob/living/carbon/human/inventory.dm:397
        if (!Dm.truthy(await user.equip_to_slot_if_possible(item, slot)))
            return false;
        await user.update_held_items();
        return user.get_item_by_slot(slot) == item; // an equip delay may have ended otherwise
    }

    // The cell's item into the empty active hand: a stored one by tg's on_found and attack_hand (smart_equip_targeted), a worn one by
    // putItemFromInventoryInHandIfPossible (tg's drag of a worn item onto a hand; attack_hand on a worn wallet would open it instead).
    private async DmTask<bool> TakeOut(mob_living_carbon_human user, int cell)
    {
        obj_item item = _items[cell - 1];
        if (item == null || QDELETED(item) || user.get_active_held_item() != null)
            return false;
        if (cell <= BeltCells)
        {
            if (!BeltUsable(user, out _))
                return false;
            // tg: code/modules/mob/living/carbon/human/inventory.dm:422-424
            if (Dm.truthy(await item.on_found(user)))
                return false;
            await item.attack_hand(user);
        }
        else
        {
            if (!Dm.truthy(await user.putItemFromInventoryInHandIfPossible(item, user.active_hand_index)))
                return false;
            item.add_fingerprint(user);
        }
        if (!Dm.truthy(user.is_holding(item)))
            return false;
        // the item's cell after the waits (a move may have rearranged the bar meanwhile), not the cell index read before them
        int index = Array.IndexOf(_items, item);
        if (index >= 0)
            _taken[index] = true;
        return true;
    }

    // tg's smart_equip_targeted's checks of the worn storage before a move (human/inventory.dm:409-413).
    private bool BeltUsable(mob_living_carbon_human user, out obj_item belt)
    {
        belt = _belt;
        if (belt == null || WornBelt(user) != belt)
            return false;
        datum_storage storage = belt.atom_storage;
        if (!Dm.truthy(storage.supports_smart_equip))
            return false;
        if (Dm.truthy(storage.locked))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is locked!", belt)));
            return false;
        }
        return true;
    }

    /// <summary>
    /// DV696: an item an inventory move took from the worn belt's storage <paramref name="from"/> into a hand keeps its cell, out, as a press of the
    /// cell would have left it (the same key puts it back); a move anywhere else frees the cell at the next <see cref="Reconcile"/>.
    /// </summary>
    public static void TakenFromBar(mob_living_carbon_human user, obj_item item, datum_storage from)
    {
        if (user.dm_hotbar is not BeltHotbar bar || bar._belt == null || bar._belt.atom_storage != from || !Held(user, item))
            return;
        int index = Array.IndexOf(bar._items, item, 0, BeltCells);
        if (index >= 0)
            bar._taken[index] = true;
    }

    // The item is the cell's alone (an item has one cell).
    private void Assign(int cell, obj_item item)
    {
        for (int c = 0; c < CellCount; c++)
        {
            if (_items[c] == item)
                Clear(c);
        }
        _items[cell - 1] = item;
        _taken[cell - 1] = false;
    }

    private void Clear(int index)
    {
        _items[index] = null;
        _taken[index] = false;
    }

    // The item is where its cell keeps it: inside the belt's storage, or worn in the cell's slot.
    private bool InContainer(mob_living_carbon_human user, int index, obj_item item)
    {
        if (QDELETED(item))
            return false;
        if (index < BeltCells)
            return _belt != null && item.loc == _belt.atom_storage?.real_location;
        return user.get_item_by_slot(SlotOf(index)) == item;
    }

    private static bool Held(mob_living_carbon_human user, obj_item item) => !QDELETED(item) && Dm.truthy(user.is_holding(item));

    // The worn belt-slot item when it has a storage (tg's toolbelt, medical belt, bandolier); a belt-slot item without one has no contents.
    private static obj_item WornBelt(mob_living_carbon_human user)
        => user.belt is obj_item belt && !QDELETED(belt) && belt.atom_storage?.real_location != null ? belt : null;

    private static int SlotOf(int index) => index switch
    {
        7 => ITEM_SLOT_LPOCKET,
        8 => ITEM_SLOT_RPOCKET,
        _ => ITEM_SLOT_ID,
    };
}

public partial class mob_living_carbon_human
{
    /// <summary>DV693: the belt hotbar's cells (<see cref="BeltHotbar"/>), made at the mob's first command phase or press.</summary>
    public BeltHotbar dm_hotbar;
}
