using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice D (D224, D225, D226; docs/ai/specs/interaction.md section 11.1): the cursor mode's drag gestures. Not tg code: tg's
// base_mouse_drop_handler (drag_drop.cs) asks Applies after its two signals and, for a pair tg leaves empty, runs the gesture instead of
// tg's reach checks and its two empty root handlers. Each gesture is made of tg's own actions as a player would do them; a refused
// step ends it where it stands, with tg's own line where tg's step has one, else a balloon alert naming why (DV138).

/// <summary>What a dragged item was let go over, for a gesture.</summary>
public enum DragReceiver : byte
{
    /// <summary>No gesture: tg's own handling (or nothing).</summary>
    None = 0,

    /// <summary>A hand slot of the dragger's HUD (or an item held in it): into that hand.</summary>
    Hand = 1,

    /// <summary>The dragger himself (the health doll in first person): into his hands.</summary>
    Self = 2,

    /// <summary>A storage: a container in the world or worn or carried, its open storage's cells, an item shown in them.</summary>
    Storage = 3,

    /// <summary>An equipment slot of the dragger's HUD (or the item worn in it): equipped there.</summary>
    Slot = 4,

    /// <summary>A floor: dropped there.</summary>
    Floor = 5,

    /// <summary>A thing in the world (a table, a closet, a machine, a mob): clicked with the item in hand.</summary>
    Thing = 6,
}

public static class DragGestures
{
    /// <summary>
    /// D225's rule, after tg's two signals came back without their cancel flags: whether the drop of <paramref name="dragged"/> over
    /// <paramref name="over"/> by <paramref name="user"/> is a gesture. False (tg's code runs alone) when the dragged thing is not an item
    /// in the world, carried (held or worn) or in the dragger's open storage; when the item or the receiver has its own tg handler
    /// (<c>mouse_drop_dragged</c>, <c>mouse_drop_receive</c>; <c>/atom/movable</c>'s buckling of a dropped mob aside, and an open storage's
    /// cell aside for an item from elsewhere, D226); when the item is dropped onto another of its own storage (tg's reorder,
    /// <c>datum/storage/mousedrop_receive</c>, acts without cancelling); or when the receiver is none of <see cref="DragReceiver"/>'s.
    /// </summary>
    public static bool Applies(atom dragged, atom over, mob user)
    {
        if (dragged is not obj_item item || user is not mob_living || over == null || over == dragged || Source(item, user) == 0)
            return false;
        if (TgHandles(item, "mouse_drop_dragged"))
            return false;
        datum_storage cellStorage = (over as atom_movable_screen_storage)?.master_ref?.resolve() as datum_storage;
        if (TgHandles(over, "mouse_drop_receive") && !(over is atom_movable_screen_storage_cell && cellStorage != null && item.loc != cellStorage.real_location))
            return false;
        if (over.loc == item.loc && item.loc != user && !isturf(item.loc))
            return false;
        return Classify(item, over, user) != DragReceiver.None;
    }

    // Whether tg defines the proc below its roots for the atom's type (D225): atom's empty root is none, and so is /atom/movable's
    // mouse_drop_receive, whose buckling takes a dropped mob only (buckling.dm:49).
    private static bool TgHandles(atom a, string proc)
    {
        Type defining = InertSeam.TgDefiningClass(a, proc);
        return defining != null && defining != typeof(atom) && !(proc == "mouse_drop_receive" && defining == typeof(atom_movable));
    }

    // 1: on the floor; 2: carried (held or worn); 3: in the dragger's open storage; 0: elsewhere (no gesture).
    private static int Source(obj_item item, mob user)
    {
        if (isturf(item.loc))
            return 1;
        if (item.loc == user)
            return 2;
        return user.active_storage?.real_location is atom inside && item.loc == inside ? 3 : 0;
    }

    /// <summary>What <paramref name="over"/> is for the gesture of <paramref name="item"/> (<see cref="DragReceiver"/>).</summary>
    public static DragReceiver Classify(obj_item item, atom over, mob user)
    {
        if (over is atom_movable_screen_inventory_hand hand)
            return hand.hud?.mymob == user ? DragReceiver.Hand : DragReceiver.None;
        if (over == user)
            return DragReceiver.Self;
        if (StorageOf(over, user) != null)
            return DragReceiver.Storage;
        if (over is atom_movable_screen_inventory slot)
            return slot.hud?.mymob == user && Dm.truthy(slot.slot_id) ? DragReceiver.Slot : DragReceiver.None;
        if (over is obj_item shown && shown.loc == user)
            return Dm.truthy(user.get_held_index_of_item(shown)) ? DragReceiver.Hand : DragReceiver.Slot;
        if (over is turf)
            return DragReceiver.Floor;
        if (over is atom_movable && isturf(over.loc) && over is not atom_movable_screen)
            return DragReceiver.Thing;
        return DragReceiver.None;
    }

    // The storage a drop onto `over` inserts into: an open storage's cell or box, a container's own (not a mob's), an item shown in the
    // dragger's open storage.
    private static datum_storage StorageOf(atom over, mob user)
    {
        if (over is atom_movable_screen_storage screen)
            return screen.master_ref?.resolve() as datum_storage;
        if (over.atom_storage != null && over is not mob)
            return over.atom_storage;
        if (user.active_storage is datum_storage open && open.real_location != null && over.loc == open.real_location)
            return open;
        return null;
    }

    /// <summary>
    /// The gesture (<see cref="Applies"/> held): tg's reach and usability of the item first (<c>can_perform_action</c>, drag_pickup's check,
    /// with tg's lines), then the receiver's actions.
    /// </summary>
    public static async DmTask Run(obj_item item, atom over, mob user, object @params)
    {
        if (!Dm.truthy(await user.can_perform_action(item, FORBID_TELEKINESIS_REACH)))
            return;
        switch (Classify(item, over, user))
        {
            case DragReceiver.Hand:
            {
                // drag_pickup's pick_up (D225: for every item): putItemFromInventoryInHandIfPossible into that hand
                object index = over is atom_movable_screen_inventory_hand hand ? hand.held_index : user.get_held_index_of_item((obj_item)over);
                if (Dm.eq(user.get_held_index_of_item(item), index))
                    return;
                if (Dm.truthy(user.get_item_for_held_index(index)))
                {
                    Refuse(item, user, "hand full");
                    return;
                }
                await PickUp(item, user, index);
                return;
            }
            case DragReceiver.Self:
                // drag_pickup's put_in_hands: a held item stays where it is
                if (!Dm.truthy(user.get_held_index_of_item(item)))
                    await IntoHand(item, user, makeActive: false);
                return;
            case DragReceiver.Storage:
            {
                datum_storage storage = StorageOf(over, user);
                if (storage.parent is atom holder && !Dm.truthy(await user.can_perform_action(holder, FORBID_TELEKINESIS_REACH)))
                    return;
                storage.attempt_insert(item, user); // tg's own lines: too big, no room, and the rest of can_insert
                return;
            }
            case DragReceiver.Slot:
            {
                atom_movable_screen_inventory slot = over as atom_movable_screen_inventory;
                // DEVIATION(DV141): an occupied slot takes the item into a worn container, else refuses (tg's click would click the worn item)
                if ((slot != null ? user.get_item_by_slot(slot.slot_id) : over) is obj_item worn)
                {
                    if (worn.atom_storage != null)
                    {
                        worn.atom_storage.attempt_insert(item, user);
                        return;
                    }
                    Refuse(item, user, "slot taken");
                    return;
                }
                if (!await IntoHand(item, user, makeActive: true))
                    return;
                await slot.Click(null, SKIN_MAPWINDOW_MAP, @params); // the slot's click: attack_ui, equip_to_slot_if_possible and its line
                return;
            }
            case DragReceiver.Floor:
            {
                if (!isopenturf(over))
                {
                    Refuse(item, user, "no room");
                    return;
                }
                if (!Dm.truthy(await user.can_perform_action(over, FORBID_TELEKINESIS_REACH)))
                    return;
                if (item.loc != user && !await IntoHand(item, user, makeActive: true))
                    return;
                if (!Dm.truthy(await user.dropItemToGround(item)))
                {
                    Refuse(item, user, "can't let go");
                    return;
                }
                if (item.loc != over && !QDELETED(item))
                    await item.forceMove(over);
                return;
            }
            case DragReceiver.Thing:
                if (!Dm.truthy(await user.can_perform_action(over, FORBID_TELEKINESIS_REACH)))
                    return;
                if (!await IntoHand(item, user, makeActive: true))
                    return;
                await user.ClickOn(over, @params); // the thing clicked with the item in the active hand
                return;
        }
    }

    // The item into a hand (D225): a held one's hand made active; a world one into the empty active hand (any empty hand when dropped on
    // oneself: put_in_hands); a worn or stored one into the active hand when it is empty, else into another empty hand; then that hand
    // made active (when asked).
    private static async DmTask<bool> IntoHand(obj_item item, mob user, bool makeActive)
    {
        object index = user.get_held_index_of_item(item);
        if (!Dm.truthy(index))
        {
            index = user.active_hand_index;
            if (Dm.truthy(user.get_item_for_held_index(index)))
            {
                index = isturf(item.loc) && makeActive ? null : (user.get_empty_held_indexes() as DmList)?[1];
                if (!Dm.truthy(index))
                {
                    Refuse(item, user, "hands full");
                    return false;
                }
            }
            if (!await PickUp(item, user, index))
                return false;
        }
        if (makeActive && !Dm.eq(index, user.active_hand_index))
            user.swap_hand(index);
        return Dm.eq(user.get_held_index_of_item(item), user.active_hand_index) || !makeActive;
    }

    // DEVIATION(DV140): drag_pickup's steps into one hand, for every item (D225): an anchored thing stays, a worn one that will not come off
    // stays; tg's pickup by the empty hand's click is not run.
    private static async DmTask<bool> PickUp(obj_item item, mob user, object index)
    {
        if (Dm.truthy(item.anchored))
        {
            Refuse(item, user, "anchored");
            return false;
        }
        if (item.loc == user && !Dm.truthy(item.can_mob_unequip(user)))
        {
            Refuse(item, user, "can't take it off");
            return false;
        }
        if (!Dm.truthy(await user.putItemFromInventoryInHandIfPossible(item, index)))
        {
            Refuse(item, user, "can't hold it");
            return false;
        }
        item.add_fingerprint(user);
        return true;
    }

    // DEVIATION(DV138): a step tg has no line for is refused with a balloon alert naming why (D226).
    private static void Refuse(obj_item item, mob user, string why) => item.balloon_alert(user, I18n.T(why));
}
