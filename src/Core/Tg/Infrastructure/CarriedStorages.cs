using Anomaly.Core.DmShim;
using System.Linq;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV692): the Tab inventory's server side (ui-overhaul.md section 12, slice 4B-1). tg shows one storage at a time (active_storage); the
// port lists every storage a human carries (the back's, the belt's, one in the suit-storage slot, the pockets and the ID slot, then one held in a
// hand) for the owner's client (protocol version 99's Storages, read by the replicator), and moves one item of the own inventory at a time between a
// hand, a worn slot, those storages and the floor through the steps a player's hands take in tg, so that tg's special cases apply: a worn item comes
// off by tg's own click with the empty active hand (or tg's drag of a worn storage onto it) and then goes on from that hand; a held item goes where
// tg's click on a storage, a slot or a hand puts it; a stored item is reached (on_found), picked as tg picks (attempt_pickup's checks) and taken out
// by its storage's own removal, then goes on. What tg needs a free hand for, the port does without one only for an item taken out of a storage (it
// goes straight on to its target) and between the hands: tg carries the item in a hand from one click to the next. A target that refuses an item
// already taken out sends it back where it was. The Tab notice opens the carried storages as tg's open_storage does, without tg's screen.

/// <summary>Where a carried storage is (the Tab inventory's order; protocol version 99's <c>StoragePlace</c> has the same numbers).</summary>
public enum CarriedPlace : byte
{
    Back = 1,
    Belt = 2,
    SuitStorage = 3,
    LeftPocket = 4,
    RightPocket = 5,
    Id = 6,
    Hand = 7,
}

/// <summary>What an inventory move puts its item into (protocol version 99's <c>InventoryTarget</c> has the same numbers).</summary>
public enum MoveTarget : byte
{
    None = 0,
    Storage = 1,
    Slot = 2,
    Hand = 3,
    Floor = 4,
}

/// <summary>A human's carried storages and the inventory moves of its client (DV692).</summary>
public sealed class CarriedStorages
{
    /// <summary>The most storages listed (protocol version 99's <c>Limits.MaxStorages</c>).</summary>
    public const int MaxStorages = 8;

    /// <summary>The most items listed of one storage, the first ones in tg's order (<c>Limits.MaxStorageItems</c>); also the highest cell wished for.</summary>
    public const int MaxItems = 64;

    /// <summary>The highest held index listed or moved to (<c>Limits.MaxStorageHands</c>).</summary>
    public const int MaxHands = 8;

    /// <summary>The shortest time between two openings by the Tab notice, in deciseconds (once a second).</summary>
    public const float OpenInterval = 10;

    /// <summary>tg's worn slots a move may name, in the order a source is looked for (the suit to the back, the neck, suit storage, the pockets).</summary>
    public static readonly int[] WornSlots =
    {
        ITEM_SLOT_OCLOTHING, ITEM_SLOT_ICLOTHING, ITEM_SLOT_GLOVES, ITEM_SLOT_EYES, ITEM_SLOT_EARS, ITEM_SLOT_MASK, ITEM_SLOT_HEAD, ITEM_SLOT_FEET,
        ITEM_SLOT_ID, ITEM_SLOT_BELT, ITEM_SLOT_BACK, ITEM_SLOT_NECK, ITEM_SLOT_SUITSTORE, ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET,
    };

    // The incapacitated flags tg's inventory clicks ignore (INCAPACITATED_IGNORING): a click on a worn slot ignores stasis
    // (screen_objects.dm:206); a click on a storage cell (:661) or a hand (:299) ignores nothing; a click on an item (click.dm ClickOn) ignores
    // restraints and stasis.
    private const float SlotClick = INCAPABLE_STASIS;
    private const float StorageClick = 0;
    private const float HandClick = 0;
    private const float ItemClick = INCAPABLE_RESTRAINTS | INCAPABLE_STASIS;

    /// <summary>One carried storage: its item, its storage datum, where it is, and the hand (tg's held index) when it is held.</summary>
    public readonly record struct Carried(obj_item Item, datum_storage Storage, CarriedPlace Place, int Hand);

    private enum Source { Hand, Slot, Storage }

    private readonly HashSet<obj_item> _moving = new(); // the items a move or a hotbar press of this human is moving (one at a time each)
    private float _openedAt = float.NegativeInfinity;

    /// <summary>How many moves were refused so far (the replicator sends a refusal once per count).</summary>
    public int Refusals { get; private set; }

    /// <summary>The item (its world id) of the last refused move, 0 before the first.</summary>
    public long RefusedItem { get; private set; }

    /// <summary>The world tick this state was made in (a reader that first sees it in that tick counts its refusals from 0).</summary>
    public long CreatedTick { get; private init; } = -1;

    private static CarriedStorages Of(mob_living_carbon_human user) => user.dm_carried ??= new CarriedStorages { CreatedTick = World.Current.Tick };

    /// <summary>
    /// The storages <paramref name="user"/> carries, in the Tab inventory's order: the back slot's, the belt's, the suit-storage slot's, the left and the
    /// right pocket's, the ID slot's, then those held, by held index; each a live item with a storage datum (tg's <c>atom_storage</c>), at most
    /// <see cref="MaxStorages"/>. Reads tg's state only.
    /// </summary>
    public static List<Carried> List(mob_living_carbon_human user)
    {
        var list = new List<Carried>();
        Add(list, user.back, CarriedPlace.Back, 0);
        Add(list, user.belt, CarriedPlace.Belt, 0);
        Add(list, user.s_store, CarriedPlace.SuitStorage, 0);
        Add(list, user.l_store, CarriedPlace.LeftPocket, 0);
        Add(list, user.r_store, CarriedPlace.RightPocket, 0);
        Add(list, user.wear_id, CarriedPlace.Id, 0);
        int hands = Math.Min((int)Dm.length(user.held_items), MaxHands);
        for (int i = 1; i <= hands; i++)
            Add(list, user.get_item_for_held_index(i) as obj_item, CarriedPlace.Hand, i);
        return list;
    }

    private static void Add(List<Carried> list, obj_item item, CarriedPlace place, int hand)
    {
        if (list.Count < MaxStorages && item != null && !QDELETED(item) && item.atom_storage is datum_storage storage && storage.real_location != null)
            list.Add(new Carried(item, storage, place, hand));
    }

    /// <summary>Whether tg lets the living see the storage's contents: not locked (tg's <c>locked</c>) and shown (<c>display_contents</c>).</summary>
    public static bool ShowsItems(datum_storage storage) => !Dm.truthy(storage.locked) && Dm.truthy(storage.display_contents);

    /// <summary>The items of a storage the client is told of: the first <see cref="MaxItems"/> in tg's order, none when it does not show them.</summary>
    public static List<obj_item> ShownItems(datum_storage storage)
    {
        var items = new List<obj_item>();
        if (!ShowsItems(storage))
            return items;
        foreach (obj_item item in Dm.each<obj_item>(storage.real_location.contents))
        {
            if (items.Count >= MaxItems)
                break;
            items.Add(item);
        }
        return items;
    }

    /// <summary>
    /// Whether <paramref name="item"/> is one of the items <paramref name="user"/>'s listed storages show now (what its own Storages message lists): in
    /// one of them directly, among its first <see cref="MaxItems"/>, the storage neither locked nor hidden. Slice 4B-2: such an item takes tg's
    /// Shift+click (its examine) as one shown in tg's own storage screen would.
    /// </summary>
    public static bool ShowsItem(mob_living_carbon_human user, obj_item item)
    {
        if (user == null || item == null || QDELETED(item))
            return false;
        foreach (Carried carried in List(user))
        {
            if (item.loc == carried.Storage.real_location && ShownItems(carried.Storage).Contains(item))
                return true;
        }
        return false;
    }

    /// <summary>Whether a storage holds more items than <see cref="ShownItems"/> lists.</summary>
    public static bool IsCut(datum_storage storage) => ShowsItems(storage) && Dm.each<obj_item>(storage.real_location.contents).Skip(MaxItems).Any();

    // ---- tg's checks of each click a move stands for ----

    // tg's checks of a click on a HUD inventory entry: a worn slot (screen_objects.dm:203-209: the click cooldown, incapacitated ignoring stasis, a
    // mech), a storage cell (:657-663: the cooldown, incapacitated, a mech) or a hand (:297-302: the same); none of them looks at blocked hands. A
    // dead body acts on nothing.
    private static bool CanClick(mob_living_carbon_human user, float ignoring)
        => user.stat != DEAD && world.time > Dm.num(user.next_move) && !Dm.truthy(INCAPACITATED_IGNORING(user, ignoring))
            && !istype(user.loc, typeof(obj_vehicle_sealed_mecha));

    // tg's checks of a click on an item inside a carried storage (click.dm ClickOn: incapacitated ignoring restraints and stasis :115, the cooldown
    // :120, blocked hands :126, whose UnarmedAttack refuses (other_mobs.dm:17-18), and a loc that allows the click :161, which a mech or a closet
    // does not); a dead body acts on nothing.
    private static bool CanClickStored(mob_living_carbon_human user)
        => user.stat != DEAD && world.time > Dm.num(user.next_move) && !Dm.truthy(INCAPACITATED_IGNORING(user, ItemClick))
            && !HAS_TRAIT(user, TRAIT_HANDS_BLOCKED) && user.loc is atom loc && Dm.truthy(loc.AllowClick());

    /// <summary>
    /// Marks <paramref name="items"/> (nulls skipped) as being moved for <paramref name="user"/>, false (nothing marked) when one already is: a move and
    /// a hotbar press never move the same item at once. Every true is followed by <see cref="Release"/>.
    /// </summary>
    public static bool Claim(mob_living_carbon_human user, obj_item a, obj_item b = null)
    {
        CarriedStorages state = Of(user);
        if ((a != null && state._moving.Contains(a)) || (b != null && state._moving.Contains(b)))
            return false;
        if (a != null)
            state._moving.Add(a);
        if (b != null)
            state._moving.Add(b);
        return true;
    }

    /// <summary>Whether a move or a hotbar press of <paramref name="user"/> is moving <paramref name="item"/> now (<see cref="Claim"/>).</summary>
    public static bool IsClaimed(mob_living_carbon_human user, obj_item item) => item != null && user?.dm_carried is CarriedStorages state && state._moving.Contains(item);

    /// <summary>Ends a <see cref="Claim"/>.</summary>
    public static void Release(mob_living_carbon_human user, obj_item a, obj_item b = null)
    {
        CarriedStorages state = Of(user);
        if (a != null)
            state._moving.Remove(a);
        if (b != null)
            state._moving.Remove(b);
    }

    // ---- the moves ----

    /// <summary>
    /// A move from <paramref name="player"/>'s client (protocol version 99's InventoryMove): the item of world id <paramref name="itemId"/> to
    /// <paramref name="to"/> (the storage of world id <paramref name="storageId"/> at cell <paramref name="index"/>, 0 for none; tg's slot id; a held
    /// index; the floor). Only a connected client's own human; an item it holds, wears or sees in a listed storage; a target of its own and in range.
    /// Anything else does nothing and is not reported. Runs detached with usr the mob, as BYOND runs a verb.
    /// </summary>
    public static void Move(client player, long itemId, MoveTarget to, long storageId, int index)
    {
        if (player == null || !player.dm_connected || player.gc_destroyed != null || player.mob is not mob_living_carbon_human user || QDELETED(user))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = user;
            Waiting.Detach(MoveAsync(user, itemId, to, storageId, index));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// The move itself (<see cref="Move"/>): the trust checks (nothing changes and nothing is reported when one fails), then the move through tg's steps;
    /// a move that does not reach its target counts a refusal of the item: one tg refuses, one made while the body cannot act, one of an item already
    /// being moved, and one a runtime error ended (reported as the world reports runtimes; an item it left taken out is put in a hand or at the feet).
    /// </summary>
    public static async DmTask MoveAsync(mob_living_carbon_human user, long itemId, MoveTarget to, long storageId, int index)
    {
        if (!World.Current.Objects.TryGet(itemId, out object found) || found is not obj_item item || QDELETED(item)
            || !FindSource(user, item, out _, out _, out _) || !ValidTarget(user, to, storageId, index, out _))
            return;
        CarriedStorages state = Of(user);
        bool done = false;
        // DV689, DV692: into the worn belt the wish is the bar's cell: tg's insert takes the item as without one (tg's order in the belt is left to
        // tg), then the bar gives it that cell (BeltHotbar.PlaceInCell); an item already in the belt only changes cell
        int barCell = 0;
        if (to == MoveTarget.Storage && index > 0 && user.belt is obj_item belt && belt.dm_id == storageId)
            (barCell, index) = (index, 0);
        if (Claim(user, item))
        {
            try
            {
                done = barCell > 0 && item.loc == user.belt.atom_storage?.real_location
                    ? CanClickStored(user) && Dm.truthy(await user.can_perform_action(user.belt, FORBID_TELEKINESIS_REACH))
                    : await Apply(user, item, to, storageId, index);
                if (done && barCell > 0)
                    BeltHotbar.PlaceInCell(user, item, barCell);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                World.Current.ReportRuntime(e);
                await Recover(user, item);
            }
            finally
            {
                Release(user, item);
            }
        }
        if (done)
            return;
        state.Refusals++;
        state.RefusedItem = item.dm_id;
    }

    // After a runtime error inside a move: an item it left inside the mob in no place goes to tg's put_in_hands (a free hand, else the feet); if that
    // throws too, it is reported, and an item still in no place is put on the mob's turf by a plain forceMove, reported as a runtime.
    private static async DmTask Recover(mob_living_carbon_human user, obj_item item)
    {
        if (QDELETED(item) || QDELETED(user) || item.loc != user || FindSource(user, item, out _, out _, out _))
            return;
        try
        {
            await user.put_in_hands(item);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            World.Current.ReportRuntime(e);
        }
        if (QDELETED(item) || QDELETED(user) || item.loc != user || FindSource(user, item, out _, out _, out _))
            return;
        World.Current.ReportRuntime(new InvalidOperationException("an inventory move left " + item.GetType().Name + " inside its mob in no place: put on the mob's turf"));
        try
        {
            await item.forceMove(get_turf(user));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            World.Current.ReportRuntime(e);
        }
    }

    // Where the item is now: in a hand, in one of the worn slots, or among the items a listed storage shows (no deeper).
    private static bool FindSource(mob_living_carbon_human user, obj_item item, out Source source, out int at, out datum_storage storage)
    {
        storage = null;
        at = (int)Dm.num(user.get_held_index_of_item(item));
        source = Source.Hand;
        if (at > 0)
            return true;
        source = Source.Slot;
        foreach (int slot in WornSlots)
        {
            if (user.get_item_by_slot(slot) == item)
            {
                at = slot;
                return true;
            }
        }
        source = Source.Storage;
        at = 0;
        foreach (Carried carried in List(user))
        {
            if (item.loc == carried.Storage.real_location && ShownItems(carried.Storage).Contains(item))
            {
                storage = carried.Storage;
                return true;
            }
        }
        return false;
    }

    // The target is the user's own and in range: a listed storage (cell 0 to MaxItems), a worn slot, a hand the mob has, the floor.
    private static bool ValidTarget(mob_living_carbon_human user, MoveTarget to, long storageId, int index, out datum_storage storage)
    {
        storage = null;
        switch (to)
        {
            case MoveTarget.Storage:
                if (index is < 0 or > MaxItems)
                    return false;
                foreach (Carried carried in List(user))
                {
                    if (carried.Item.dm_id == storageId)
                    {
                        storage = carried.Storage;
                        // DV689, DV692: the worn belt's wish is a bar cell (1 to 7), not a place in tg's contents
                        return carried.Place != CarriedPlace.Belt || index <= BeltHotbar.BeltCells;
                    }
                }
                return false;
            case MoveTarget.Slot:
                return storageId == 0 && Array.IndexOf(WornSlots, index) >= 0;
            case MoveTarget.Hand:
                return storageId == 0 && index >= 1 && index <= Math.Min((int)Dm.length(user.held_items), MaxHands);
            case MoveTarget.Floor:
                return storageId == 0 && index == 0;
            default:
                return false;
        }
    }

    // The target storage is still one the user carries (it may have been dropped or deleted during a wait).
    private static bool StillCarried(mob_living_carbon_human user, long storageId, datum_storage into)
        => ValidTarget(user, MoveTarget.Storage, storageId, 0, out datum_storage now) && now == into && !QDELETED(into.parent);

    // After a wait: the target is still the user's own and free: a carried storage (StillCarried), an empty worn slot, a hand the mob has and that is
    // empty, or the floor. Every path asks it after each of its waits.
    private static bool TargetStill(mob_living_carbon_human user, MoveTarget to, long storageId, datum_storage into, int index) => to switch
    {
        MoveTarget.Storage => StillCarried(user, storageId, into),
        MoveTarget.Slot => user.get_item_by_slot(index) == null,
        MoveTarget.Hand => ValidTarget(user, MoveTarget.Hand, 0, index, out _) && user.get_item_for_held_index(index) == null,
        _ => true,
    };

    private static async DmTask<bool> Apply(mob_living_carbon_human user, obj_item item, MoveTarget to, long storageId, int index)
    {
        if (!FindSource(user, item, out Source source, out int at, out datum_storage from) || !ValidTarget(user, to, storageId, index, out datum_storage into))
            return false;
        // tg gives a player no way to move an abstract item or a hand item (the slapper, the kiss: hand_items.dm:7) but to drop it
        if (Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT | HAND_ITEM)) && to != MoveTarget.Floor)
            return false;
        // DEVIATION(DV27): the interaction guard, before anything moves (attempt_pickup's own guard and equip_to_slot's refuse such an item
        // only after it left its place)
        if (InertSeam.MissingOverride(item, "pickup", user) || InertSeam.MissingOverride(item, "equipped", user) || InertSeam.MissingOverride(item, "dropped", user))
            return false;
        if (to == MoveTarget.Storage && Inside(into, item, user))
            return false; // nothing comes off or out for a move tg's can_insert refuses anyway
        switch (source)
        {
            case Source.Slot:
                if (to == MoveTarget.Slot && at == index)
                    return false;
                if (!await TakeOff(user, item, at))
                    return false;
                int active = (int)Dm.num(user.active_hand_index);
                if (to == MoveTarget.Hand && index == active)
                    return true;
                // it is in the hand now (after the click's own waits: attempt_pickup's heavy gravity): on from there if the target is still free, and if
                // the target refuses it stays in that hand, as in tg after taking it off
                if (!TargetStill(user, to, storageId, into, index))
                    return false;
                return await FromHand(user, item, active, to, storageId, into, index);
            case Source.Hand:
                return await FromHand(user, item, at, to, storageId, into, index);
            default:
                return await FromStorage(user, item, from, to, storageId, into, index);
        }
    }

    // A worn item comes off as tg takes it off, into the empty active hand. An item tg takes off by a drag (one with tg's drag_pickup element: the
    // backpack watertank, the defibrillator, whose click toggles the mister or takes the paddles; every worn storage not in a pocket, whether its
    // click opens it or not: boots with a shoe pocket, whose click would tie them, come off by tg's own drag too) by tg's drag of it onto that hand: COMSIG_MOUSEDROP_ONTO, the first step of tg's base_mouse_drop_handler (drag_drop.dm:31), which
    // drag_pickup (drag_pickup.dm:19-35) and the storage (storage.dm:801-822) answer with putItemFromInventoryInHandIfPossible. Anything else by tg's
    // click on its slot with that hand empty: the slot's Click passes the click to the item (screen_objects.dm:200-214), and ClickOn gives an empty
    // hand's click on an item in the own inventory to UnarmedAttack and so to the item's attack_hand, which an item may override (a tinfoil hat stays
    // on, an untied shoe is tied). A click that does something else is done as in tg, and the move is refused.
    private static async DmTask<bool> TakeOff(mob_living_carbon_human user, obj_item item, int slot)
    {
        // tg: code/_onclick/hud/screen_objects/screen_objects.dm:203-208
        if (!CanClick(user, SlotClick))
            return false;
        // with an item in the active hand tg's click uses that item on the worn one (mob.dm:487-493): nothing comes off
        if (user.get_active_held_item() != null)
            return false;
        int active = (int)Dm.num(user.active_hand_index);
        if (HasDragPickup(item) || (item.atom_storage != null && slot != ITEM_SLOT_LPOCKET && slot != ITEM_SLOT_RPOCKET))
        {
            if (HandBox(user, active) is not atom_movable_screen_inventory_hand box)
                return false;
            // tg: code/_onclick/drag_drop.dm:31
            if (Dm.truthy(item._listen_lookup?[COMSIG_MOUSEDROP_ONTO]))
                item._SendSignal(COMSIG_MOUSEDROP_ONTO, Dm.list(item, box, user));
        }
        else
        {
            // tg: code/_onclick/click.dm:58 (ClickOn: incapacitated ignoring restraints and stasis; the item in DirectAccess, the hand empty)
            if (Dm.truthy(INCAPACITATED_IGNORING(user, ItemClick)))
                return false;
            await user.UnarmedAttack(item, TRUE, Dm.list());
        }
        return !QDELETED(item) && Dm.eq(user.get_held_index_of_item(item), active);
    }

    // The item carries tg's drag_pickup element (its COMSIG_MOUSEDROP_ONTO listener), the mark of an item tg takes off by a drag.
    private static bool HasDragPickup(obj_item item)
    {
        object listeners = item._listen_lookup?[COMSIG_MOUSEDROP_ONTO];
        return listeners is datum_element_drag_pickup || (listeners is DmList list && Dm.each<datum_element_drag_pickup>(list).Any());
    }

    // The HUD's box of hand <paramref name="index"/> (tg's /atom/movable/screen/inventory/hand), what tg's drag of a worn storage is dropped on.
    private static atom_movable_screen_inventory_hand HandBox(mob_living_carbon_human user, int index)
    {
        if (user.hud_used?.screen_groups?[HUD_GROUP_STATIC] is not DmList boxes)
            return null;
        foreach (atom_movable_screen_inventory_hand box in Dm.each<atom_movable_screen_inventory_hand>(boxes))
        {
            if (Dm.eq(box.held_index, index))
                return box;
        }
        return null;
    }

    // A held item to its target, as tg's click with it in hand: on a storage cell (attempt_insert), on a slot (attack_ui: equip_to_slot_if_possible),
    // on a hand (putItemFromInventoryInHandIfPossible), or tg's drop (dropItemToGround). If the target refuses, the item stays in its hand.
    private static async DmTask<bool> FromHand(mob_living_carbon_human user, obj_item item, int hand, MoveTarget to, long storageId, datum_storage into, int index)
    {
        switch (to)
        {
            case MoveTarget.Storage:
            {
                // tg: code/_onclick/hud/screen_objects/screen_objects.dm:653-667
                if (!CanClick(user, StorageClick) || Inside(into, item, user))
                    return false;
                if (!Dm.truthy(into.attempt_insert(item, user)))
                    return false;
                return Inserted(into, item, index);
            }
            case MoveTarget.Slot:
            {
                // tg: code/_onclick/hud/screen_objects/screen_objects.dm:200-217 and code/modules/mob/mob.dm:485 (attack_ui) and
                // code/modules/mob/inventory.dm:516 (equip_to_slot_if_possible)
                if (!CanClick(user, SlotClick))
                    return false;
                if (!Dm.truthy(await item.mob_can_equip(user, index)))
                {
                    to_chat(user, span_warning(I18n.T("You are unable to equip that!")));
                    return false;
                }
                // the equip delay may have let the item move, the slot fill or the body stop acting: then nothing is put on (tg's equip_to_slot
                // would put the item on from wherever it went)
                if (QDELETED(item) || !Dm.eq(user.get_held_index_of_item(item), hand) || user.get_item_by_slot(index) != null || !CanClick(user, SlotClick))
                    return false;
                await user.equip_to_slot(item, index);
                if (user.get_item_by_slot(index) == item)
                    return true;
                // tg's equip_to_slot "should not ever fail"; if it does, the item it took from the hand goes back into it
                if (!QDELETED(item) && item.loc == user && !Dm.truthy(user.is_holding(item)) && !Dm.truthy(await user.put_in_hand(item, hand)))
                    await user.put_in_hands(item);
                return false;
            }
            case MoveTarget.Hand:
            {
                // tg: code/modules/mob/inventory.dm:330 (putItemFromInventoryInHandIfPossible)
                if (hand == index || !CanClick(user, HandClick))
                    return false;
                if (!Dm.truthy(await user.putItemFromInventoryInHandIfPossible(item, index)))
                    return false;
                item.add_fingerprint(user);
                return Dm.eq(user.get_held_index_of_item(item), index);
            }
            default:
                // tg: code/datums/keybinding/mob.dm:90-102 (the drop key: no check of its own; a cuffed or stunned player drops) and
                // code/modules/mob/inventory.dm:349 (dropItemToGround, which refuses TRAIT_NODROP); a dead body acts on nothing
                if (user.stat == DEAD)
                    return false;
                return await user.dropItemToGround(item) != null || QDELETED(item); // deleted: a DROPDEL item, or a stack merged where it fell
        }
    }

    // A stored item to its target. tg's way: the storage is open (Reach: on_found), a click on the item picks it (attempt_pickup: its checks, the
    // storage's own remove_single, pickup, put_in_active_hand), and a click with it in hand puts it on. Into the active hand that click is tg's own
    // (ClickOn's UnarmedAttack, as TakeOff's); into the other hand attempt_pickup's steps with that hand; onto a storage, a slot or the floor the
    // item goes straight on after remove_single, with no hand: pickup() and the hand's equipped() are skipped, tg's dropped() runs again where tg
    // would run it when the hand lets go. Every target is asked before anything moves, as tg asks it for its slot highlight; the item stays in its
    // storage through every wait but its storage's own removal, and a target that no longer takes it once it is out sends it back where it was.
    private static async DmTask<bool> FromStorage(mob_living_carbon_human user, obj_item item, datum_storage from, MoveTarget to, long storageId,
        datum_storage into, int index)
    {
        // tg: code/_onclick/click.dm:115-161 (ClickOn on the stored item; the take-out's checks, the floor's too: tg's player takes it out first)
        if (!CanClickStored(user))
            return false;
        if (to == MoveTarget.Storage && into == from)
            return await Reorder(user, into, item, index);
        int active = (int)Dm.num(user.active_hand_index);
        // what the target takes, asked before anything moves, with tg's own message for each case
        switch (to)
        {
            case MoveTarget.Storage:
                if (!CanClick(user, StorageClick) || Inside(into, item, user) || !Dm.truthy(into.can_insert(item, user)))
                    return false;
                break;
            case MoveTarget.Slot:
                // tg: code/_onclick/hud/screen_objects/screen_objects.dm:240-258 (add_overlays: mob_can_equip without its warnings and delay)
                if (!CanClick(user, SlotClick))
                    return false;
                if (!Dm.truthy(await item.mob_can_equip(user, index, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))
                {
                    to_chat(user, span_warning(I18n.T("You are unable to equip that!")));
                    return false;
                }
                break;
            case MoveTarget.Hand:
                if (!CanClick(user, HandClick) || !Dm.truthy(user.can_put_in_hand(item, index)))
                    return false;
                break;
        }
        if (await Reach(user, item, from) || !TargetStill(user, to, storageId, into, index))
            return false;
        if (to == MoveTarget.Hand && index == active)
        {
            // tg: code/_onclick/click.dm:150-159 (an empty hand's click on a reachable item: UnarmedAttack -> attack_hand -> attempt_pickup into the
            // active hand, its own fallback included)
            await user.UnarmedAttack(item, TRUE, Dm.list());
            if (!QDELETED(item) && Dm.truthy(user.is_holding(item)))
            {
                BeltHotbar.TakenFromBar(user, item, from);
                return true;
            }
            return false;
        }
        if (to == MoveTarget.Slot)
        {
            // tg: code/modules/mob/inventory.dm:519 (equip_to_slot_if_possible: mob_can_equip with its warnings and its delay), the item still in its
            // storage meanwhile
            if (!Dm.truthy(await item.mob_can_equip(user, index)))
            {
                to_chat(user, span_warning(I18n.T("You are unable to equip that!")));
                return false;
            }
            if (!Still(user, item, from) || !CanClick(user, SlotClick) || !TargetStill(user, to, storageId, into, index))
                return false;
        }
        bool Free() => TargetStill(user, to, storageId, into, index);
        if (!await PickUp(user, item, from, Free))
            return false;
        // the item is out, inside the mob, after its storage's own removal (which may have waited): every target is asked again, and one that no
        // longer takes it sends it back
        switch (to)
        {
            case MoveTarget.Storage:
                if (!CanClick(user, StorageClick) || !Free() || Inside(into, item, user) || !Dm.truthy(into.attempt_insert(item, user)))
                {
                    await PutBack(user, item, from);
                    return false;
                }
                return Inserted(into, item, index);
            case MoveTarget.Slot:
                if (QDELETED(item) || item.loc != user || !CanClick(user, SlotClick) || !Free()
                    || !Dm.truthy(await item.mob_can_equip(user, index, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))
                {
                    await PutBack(user, item, from);
                    return false;
                }
                await user.equip_to_slot(item, index);
                if (user.get_item_by_slot(index) == item)
                    return true;
                await PutBack(user, item, from);
                return false;
            case MoveTarget.Hand:
                if (!Free())
                {
                    await PutBack(user, item, from);
                    return false;
                }
                // tg: code/game/objects/items.dm:586-588 (pickup, add_fingerprint, put_in_active_hand), into the hand named
                await item.pickup(user);
                item.add_fingerprint(user);
                if (!QDELETED(item) && item.loc == user && Dm.truthy(await user.put_in_hand(item, index)))
                {
                    BeltHotbar.TakenFromBar(user, item, from);
                    return true;
                }
                if (QDELETED(item))
                    return true; // a stack that merged into one of its kind on the mob (tg's stack Moved, merge_with_loc)
                await PutBack(user, item, from);
                return false;
            default:
                // tg: code/modules/mob/inventory.dm:349 (dropItemToGround: the drop sound, the pixel offset, the drop animation, COMSIG_MOB_DROPPING_ITEM)
                if (await user.dropItemToGround(item) != null || QDELETED(item))
                    return true;
                await PutBack(user, item, from);
                return false;
        }
    }

    // tg's attempt_pickup up to the hand (items.dm:546-582): the auto-pickup flag, MOBILITY_PICKUP, the heavy-gravity wait, the storage's own
    // remove_single into the mob (a trash bag's search, an evidence bag's reset), the pickup animation from the storage's turf and a throw ended.
    // False when it stopped; the item is then where it was (or gone, when its removal destroyed it).
    private static async DmTask<bool> PickUp(mob_living_carbon_human user, obj_item item, datum_storage from, Func<bool> targetStill)
    {
        // tg: code/game/objects/items.dm:549-562
        if (!Dm.truthy(Dm.bitand(item.interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP)))
            return false;
        if (!Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_PICKUP)))
            return false;
        object grav = user.has_gravity();
        if (Dm.gt(grav, STANDARD_GRAVITY))
        {
            float grav_power = min(3, Dm.num(grav) - STANDARD_GRAVITY);
            to_chat(user, span_notice(I18n.T("You start picking up []...", item)));
            if (!Dm.truthy(await do_after(user, 30 * grav_power, item)))
                return false;
            if (!Still(user, item, from) || !targetStill())
                return false;
        }
        // tg: code/game/objects/items.dm:565-575
        turf storage_turf = isturf(user.loc) ? get_turf(item.loc) : null;
        if (!Dm.truthy(await from.remove_single(user, item, user, silent: TRUE)))
            return false;
        if (QDELETED(item) || item.loc != user)
            return false;
        if (storage_turf != null)
            item.do_pickup_animation(user, storage_turf);
        if (Dm.truthy(item.throwing))
            item.throwing.finalize(FALSE);
        return true;
    }

    // An item taken out that its target no longer takes goes back where it was: into its storage by tg's insert; if that refuses too, tg's
    // put_in_hands (a free hand, else the feet).
    private static async DmTask PutBack(mob_living_carbon_human user, obj_item item, datum_storage from)
    {
        if (QDELETED(item) || QDELETED(user) || item.loc != user || Dm.truthy(user.is_holding(item)))
            return;
        if (!QDELETED(from.parent) && from.real_location != null && Dm.truthy(from.attempt_insert(item, user, messages: FALSE)))
            return;
        await user.put_in_hands(item);
    }

    // A stored item is reached into: tg's on_found (a mousetrap snaps) runs as when tg's storage is opened over it or the hotbar takes it; true when
    // the item stopped the reach, or the item or the user changed meanwhile.
    // tg: code/datums/storage/storage.dm:1080-1084 (show_contents' on_found pass), code/game/objects/items.dm on_found
    private static async DmTask<bool> Reach(mob_living_carbon_human user, obj_item item, datum_storage from)
    {
        if (Dm.truthy(await item.on_found(user)))
            return true;
        return !Still(user, item, from) || !CanClickStored(user);
    }

    // After a wait: the stored item is still among the items of the storage the move found it in.
    private static bool Still(mob_living_carbon_human user, obj_item item, datum_storage from)
        => !QDELETED(item) && !QDELETED(user) && FindSource(user, item, out Source now, out _, out datum_storage nowFrom) && now == Source.Storage && nowFrom == from;

    // The target storage is the item or inside it (a storage into itself, or into one it holds): tg's can_insert refuses the first; the second is
    // never listed, so this only guards.
    private static bool Inside(datum_storage into, obj_item item, mob_living_carbon_human user)
    {
        for (atom at = into.parent; at != null && at != user; at = at.loc)
        {
            if (at == item)
                return true;
        }
        return false;
    }

    // After tg's attempt_insert said yes: the item is in the storage (or merged into a stack of its kind there), at the wished cell.
    private static bool Inserted(datum_storage into, obj_item item, int cell)
    {
        if (QDELETED(item))
            return true; // a stack that merged into one of its kind in the storage (tg's stack Moved, merge_with_loc)
        if (item.loc != into.real_location)
            return false;
        if (cell > 0)
            Order(into, item, cell);
        return true;
    }

    // A move within one storage: tg's reorder by a drag onto an item or a cell of the open storage, with its reach check (can_perform_action); the
    // storage need not be the user's active_storage, since the Tab inventory shows it open.
    // tg: code/datums/storage/storage.dm:524-546 (mousedrop_receive), code/_onclick/hud/screen_objects/screen_objects.dm:673-687 (an empty cell)
    private static async DmTask<bool> Reorder(mob_living_carbon_human user, datum_storage storage, obj_item item, int cell)
    {
        if (!Dm.truthy(await user.can_perform_action(storage.parent, FORBID_TELEKINESIS_REACH)))
            return false;
        if (item.loc != storage.real_location || Dm.truthy(storage.numerical_stacking))
            return false;
        Order(storage, item, cell);
        return true;
    }

    // tg's reorder of a storage's contents: the item dropped onto the item in the wished cell goes before it (an item moved onto a later one lands
    // after it, as tg reads the index before taking the item out); onto an empty cell or no cell, last. Nothing for tg's numbered displays.
    // tg: code/datums/storage/storage.dm:539-546, code/_onclick/hud/screen_objects/screen_objects.dm:684-686
    private static void Order(datum_storage storage, obj_item item, int cell)
    {
        if (Dm.truthy(storage.numerical_stacking))
            return;
        DmList contents = storage.real_location.contents;
        if (cell > 0 && cell <= Dm.length(contents))
        {
            object dropped_onto = contents[cell];
            if (dropped_onto == item)
                return;
            float drop_index = contents.Find(dropped_onto);
            Dm.remove(contents, item);
            DmList to_move = Dm.length(contents) >= drop_index ? contents.Copy(drop_index) : Dm.list();
            Dm.remove(contents, to_move);
            Dm.append(contents, item);
            Dm.append(contents, to_move);
        }
        else
        {
            Dm.remove(contents, item);
            Dm.append(contents, item);
        }
        Waiting.NoSleep(storage.refresh_views());
    }

    // ---- the Tab notice ----

    /// <summary>
    /// The Tab notice from <paramref name="player"/>'s client (protocol version 99's InventoryOpen): the carried storages are opened as tg opens a
    /// storage, without its screen, at most once every <see cref="OpenInterval"/>. Only a connected client's own human.
    /// </summary>
    public static void Opened(client player)
    {
        if (player == null || !player.dm_connected || player.gc_destroyed != null || player.mob is not mob_living_carbon_human user || QDELETED(user))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = user;
            Waiting.Detach(OpenedAsync(user));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// tg's open_storage and show_contents on each listed storage, in tg's order, but for tg's screen and active_storage: for each, on its own,
    /// can_perform_action with ALLOW_RESTING and FORBID_TELEKINESIS_REACH, MOBILITY_STORAGE, locked (with "closed!" for the back storage), a client,
    /// display_contents, then the on_found pass over its contents (an armed mousetrap snaps and that storage's opening ends there); a storage that
    /// cannot be opened is skipped and the others still are. The back storage, opened, then plays the animation and the rustle or its open sound
    /// that those near hear; the others make no sound. A quickdraw storage does not draw; the other container the user has open (active_storage) is
    /// neither closed nor replaced, also when a find ends the search (tg would close it).
    /// </summary>
    public static async DmTask OpenedAsync(mob_living_carbon_human user)
    {
        CarriedStorages state = Of(user);
        if (world.time < state._openedAt + OpenInterval)
            return;
        state._openedAt = world.time;
        foreach (Carried c in List(user))
        {
            if (QDELETED(c.Item) || QDELETED(user))
                continue;
            datum_storage storage = c.Storage;
            bool back = c.Place == CarriedPlace.Back;
            // tg: code/datums/storage/storage.dm:971-981
            if (!Dm.truthy(await user.can_perform_action(storage.parent, Dm.bitor(ALLOW_RESTING, FORBID_TELEKINESIS_REACH))))
                continue;
            if (!Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_STORAGE)))
                continue;
            if (Dm.truthy(storage.locked))
            {
                if (back && !Dm.truthy(storage.silent))
                    storage.parent.balloon_alert(user, I18n.T("closed!"));
                continue;
            }
            // tg: code/datums/storage/storage.dm:1073-1084 (show_contents: a client, display_contents, the on_found pass)
            if (user.client == null || !Dm.truthy(storage.display_contents) || await Search(user, storage) || !back)
                continue;
            // tg: code/datums/storage/storage.dm:1002-1006
            if (Dm.truthy(storage.animated))
                storage.animate_parent();
            if (Dm.truthy(storage.do_rustle) && !Dm.truthy(storage.silent))
                playsound(storage.parent, Dm.truthy(storage.open_sound) ? storage.open_sound : SFX_RUSTLE, 50, storage.open_sound_vary, -5);
        }
    }

    // show_contents' on_found pass: unless the storage is the user's open one already or the user is unconscious, each item may end the search
    // (true: a mousetrap snapped on the searching hand).
    // tg: code/datums/storage/storage.dm:1080-1084
    private static async DmTask<bool> Search(mob_living_carbon_human user, datum_storage storage)
    {
        if (user.active_storage == storage || IS_UNCONSCIOUS_OR_CRIT(user))
            return false;
        foreach (obj_item thing in Dm.each<obj_item>(storage.real_location.contents).ToList())
        {
            if (Dm.truthy(await thing.on_found(user)))
                return true;
        }
        return false;
    }
}

public partial class mob_living_carbon_human
{
    /// <summary>DV692: the Tab inventory's moves and refusals (<see cref="CarriedStorages"/>), made at the mob's first move, press or notice.</summary>
    public CarriedStorages dm_carried;
}
