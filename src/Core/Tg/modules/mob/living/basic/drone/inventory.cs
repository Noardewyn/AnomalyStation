// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file. Drone inventory procs.

public partial class mob_living_basic_drone : mob_living_basic
{
    // tg: code/modules/mob/living/basic/drone/inventory.dm:3
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        invdrop ??= TRUE;
        silent ??= FALSE;
        if (Dm.truthy(await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop, silent)))
        {
            await update_held_items();
            if (item_dropping == head)
            {
                head = null;
                await update_worn_head();
            }
            if (item_dropping == internal_storage)
            {
                internal_storage = null;
                update_inv_internal_storage();
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/drone/inventory.dm:16
    public override async DmTask<object> can_equip(obj_item I = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        switch ((int)Dm.num(slot))
        {
            case (int)ITEM_SLOT_HEAD:
                if (head != null)
                    return FALSE;
                if (!(Dm.truthy(Dm.bitand(I.slot_flags, ITEM_SLOT_HEAD)) || Dm.truthy(Dm.bitand(I.slot_flags, ITEM_SLOT_MASK))))
                    return FALSE;
                return TRUE;
            case (int)ITEM_SLOT_DEX_STORAGE:
                if (internal_storage != null)
                    return FALSE;
                return TRUE;
        }
        await base.can_equip(I, slot, disable_warning, bypass_equip_delay_self, ignore_equipped, indirect_action);
        return null; // tg's ..() without a return: null
    }

    // tg: code/modules/mob/living/basic/drone/inventory.dm:31
    public override object get_item_by_slot(object slot_id = null)
    {
        switch ((int)Dm.num(slot_id))
        {
            case (int)ITEM_SLOT_HEAD:
                return head;
            case (int)ITEM_SLOT_DEX_STORAGE:
                return internal_storage;
        }
        return base.get_item_by_slot(slot_id);
    }

    // tg: code/modules/mob/living/basic/drone/inventory.dm:40
    public override object get_slot_by_item(obj_item looking_for = null)
    {
        if (internal_storage == looking_for)
            return ITEM_SLOT_DEX_STORAGE;
        if (head == looking_for)
            return ITEM_SLOT_HEAD;
        return base.get_slot_by_item(looking_for);
    }

    // tg: code/modules/mob/living/basic/drone/inventory.dm:47
    public override async DmTask<object> equip_to_slot(obj_item equipping = null, object slot = null, float? initial = null, float? redraw_mob = null, float? indirect_action = null)
    {
        if (!Dm.truthy(slot))
            return null;
        if (equipping == null)
            return null;

        object index = get_held_index_of_item(equipping);
        if (Dm.truthy(index))
            held_items[index] = null;
        await update_held_items();

        if (equipping.pulledby != null)
            await equipping.pulledby.stop_pulling();

        hud_used?.update_inventory_slot(slot);
        await equipping.forceMove(this); //This has to come before has_equipped is called.
        SET_PLANE_EXPLICIT(equipping, ABOVE_HUD_PLANE, this);

        switch ((int)Dm.num(slot))
        {
            case (int)ITEM_SLOT_HEAD:
                head = equipping;
                await update_worn_head();
                break;
            case (int)ITEM_SLOT_DEX_STORAGE:
                internal_storage = equipping;
                update_inv_internal_storage();
                break;
            default:
                to_chat(this, span_danger(I18n.T("You are trying to equip this item to an unsupported inventory slot. Report this to a coder!")));
                return null;
        }

        //Call back for item being equipped to drone
        await has_equipped(equipping, slot);
        return null;
    }

    // tg: code/modules/mob/living/basic/drone/inventory.dm:79
    public override object getBackSlot() => ITEM_SLOT_DEX_STORAGE;
}
