// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbreeds' clothing (the rebreather mask, the prism glasses with their light prisms and lens colour, the
// heroine bud, the adamantine armour). DV648: the prism glasses' lens action adds and removes the lens colour's element on the
// glasses; tg's calls land on the action itself, where the element refuses it with a runtime. The var blocks are generated (D111).

/*
Slimecrossing Armor
	Armor added by the slimecrossing system.
	Collected here for clarity.
*/

//Rebreather mask - Chilling Blue
// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:9
public partial class obj_item_clothing_mask_nobreath : obj_item_clothing_mask
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:24
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)))
        {
            mob_living_carbon wearer = (mob_living_carbon)user;
            wearer.failed_last_breath = FALSE;
            wearer.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            await wearer.apply_status_effect(typeof(datum_status_effect_rebreathing));
        }
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:31
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        ((mob_living)user).remove_status_effect(typeof(datum_status_effect_rebreathing));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:36
public partial class obj_item_clothing_glasses_prism_glasses : obj_item_clothing_glasses
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_glass_colour), ITEM_SLOT_EYES, GLASSES_TRAIT, glasses_color, forced_glass_color));
        return dot;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:50
public partial class obj_structure_light_prism : obj_structure
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:58
    [DmParams("mapload", "newcolor")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object newcolor = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(newcolor))
        {
            color = newcolor;
            set_light_color(newcolor);
        }
        Waiting.NoSleep(set_light(5));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:65
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        to_chat(user, span_notice(I18n.T("You dispel [].", this)));
        qdel(this);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:70
public partial class datum_action_item_action_change_prism_colour : datum_action_item_action
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:74
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_clothing_glasses_prism_glasses glasses = target as obj_item_clothing_glasses_prism_glasses;
        object new_color = await tgui_color_picker(owner, I18n.T("Choose the lens color:"), I18n.T("Color change"), glasses.glasses_color);
        if (!Dm.truthy(new_color))
            return null;
        // DV648: on the glasses, where tg calls them on the action
        glasses.RemoveElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_glass_colour), ITEM_SLOT_EYES, GLASSES_TRAIT, glasses.glasses_color, glasses.forced_glass_color);
        glasses.glasses_color = Dm.str(new_color);
        await glasses.AddElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_glass_colour), ITEM_SLOT_EYES, GLASSES_TRAIT, new_color, glasses.forced_glass_color);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:84
public partial class datum_action_item_action_place_light_prism : datum_action_item_action
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:88
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        await DmTask.CompletedTask;
        obj_item_clothing_glasses_prism_glasses glasses = target as obj_item_clothing_glasses_prism_glasses;
        if (Dm.locate<obj_structure_light_prism>(get_turf(owner)) != null)
        {
            to_chat(owner, span_warning(I18n.T("There isn't enough ambient energy to fabricate another light prism here.")));
            return null;
        }
        if (glasses is obj_item_clothing_glasses_prism_glasses)
        {
            if (!Dm.truthy(glasses.glasses_color))
            {
                to_chat(owner, span_warning(I18n.T("The lens is oddly opaque...")));
                return null;
            }
            to_chat(owner, span_notice(I18n.T("You channel nearby light into a glowing, ethereal prism.")));
            Dm.@new<obj_structure_light_prism>(get_turf(owner), glasses.glasses_color);
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:101
public partial class obj_item_clothing_head_peaceflower : obj_item_clothing_head
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:116
    public override object can_mob_unequip(mob user = null)
    {
        if (user.get_item_by_slot(slot_flags) == this)
        {
            to_chat(user, span_warning(I18n.T("You feel at peace. <b style='color:pink'>Why would you want anything else?</b>")));
            return FALSE;
        }
        return base.can_mob_unequip(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:123
public partial class obj_item_clothing_suit_armor_heavy_adamantine : obj_item_clothing_suit_armor_heavy
{
    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:136
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle), SFX_PLATE_ARMOR_RUSTLE, 8));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_clothing.dm:140
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        await DmTask.CompletedTask;
        if (Dm.@in(def_zone, Dm.list(BODY_ZONE_CHEST, BODY_ZONE_R_ARM, BODY_ZONE_L_ARM, BODY_ZONE_R_LEG, BODY_ZONE_L_LEG)) && prob(hit_reflect_chance))
            return TRUE;
        else
            return FALSE;
    }
}
