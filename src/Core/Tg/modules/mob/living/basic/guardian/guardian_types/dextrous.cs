// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// Dextrous guardians have some of the most powerful abilities of all: hands and pockets
// tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:2
public partial class mob_living_basic_guardian_dextrous : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_inhands)));
        add_traits(Dm.list(TRAIT_ADVANCEDTOOLUSER, TRAIT_CAN_STRIP), ROUNDSTART_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_dextrous), Dm.named("hud_type", hud_type), Dm.named("can_throw", TRUE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_inhands)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:24
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        await dropItemToGround(internal_storage);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:28
    public override async DmTask create_actions()
    {
        foreach (object action_type in Dm.each(self_actions))
        {
            if (action_type == null) //no toggle button type
                continue;
            if (Dm.locate_type((Type)action_type, actions) != null)
                continue;
            datum_action new_action = Dm.@new((Type)action_type, this) as datum_action;
            //Show up at the top left like usual.
            new_action.default_button_position = Dm.initial(typeof(datum_action), (datum_action p) => p.default_button_position);
            await new_action.Grant(this);
        }
        await update_action_buttons();
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (internal_storage == null || Dm.truthy(Dm.bitand(internal_storage.item_flags, ABSTRACT)))
            return dot;
        Dm.append(dot, span_info(Dm.text("It is holding [] in its internal storage.", internal_storage.examine_title(user))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:46
    public override async DmTask manifest_effects()
    {
        await base.manifest_effects();
        REMOVE_TRAIT(this, TRAIT_HANDS_BLOCKED, GUARDIAN_RECALLED);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:50
    public override async DmTask recall_effects()
    {
        await base.recall_effects();
        await drop_all_held_items();
        ADD_TRAIT(this, TRAIT_HANDS_BLOCKED, GUARDIAN_RECALLED);
    }

    // Bullshit related to having a fake pocket begins here

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:57
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        object dot = await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop ?? TRUE, silent ?? FALSE);
        if (!Dm.truthy(dot))
            return FALSE;
        await update_held_items();
        if (item_dropping == internal_storage)
        {
            internal_storage = null;
            update_inv_internal_storage();
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:67
    public override async DmTask<object> can_equip(obj_item I = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.eq(slot, ITEM_SLOT_DEX_STORAGE))
            return FALSE;
        return Dm.num(internal_storage == null);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:72
    public override object get_item_by_slot(object slot_id = null)
    {
        if (Dm.eq(slot_id, ITEM_SLOT_DEX_STORAGE))
            return internal_storage;
        return base.get_item_by_slot(slot_id);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:77
    public override object get_slot_by_item(obj_item looking_for = null)
    {
        if (internal_storage == looking_for)
            return ITEM_SLOT_DEX_STORAGE;
        return base.get_slot_by_item(looking_for);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:82
    public override async DmTask<object> equip_to_slot(obj_item equipping = null, object slot = null, float? initial = null, float? redraw_mob = null, float? indirect_action = null)
    {
        if (!Dm.eq(slot, ITEM_SLOT_DEX_STORAGE))
        {
            to_chat(this, span_danger("You are trying to equip this item to an unsupported inventory slot. Report this to a coder!"));
            return FALSE;
        }

        object index = get_held_index_of_item(equipping);
        if (Dm.truthy(index))
            held_items[index] = null;
        await update_held_items();

        if (equipping.pulledby != null)
            await equipping.pulledby.stop_pulling();

        hud_used?.update_inventory_slot(slot);
        await equipping.forceMove(this);
        SET_PLANE_EXPLICIT(equipping, ABOVE_HUD_PLANE, this);

        internal_storage = equipping;
        update_inv_internal_storage();

        await has_equipped(equipping, slot);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:105
    public override object getBackSlot()
    {
        return ITEM_SLOT_DEX_STORAGE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:108
    public virtual void update_inv_internal_storage()
    {
        hud_used?.update_inventory_slot(ITEM_SLOT_DEX_STORAGE);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/dextrous.dm:111
    public override async DmTask<object> regenerate_icons()
    {
        update_inv_internal_storage();
        await DmTask.CompletedTask;
        return null;
    }
}
