// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/explorer_gear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the explorer's gas mask, folded in the miner's survival box. The explorer suit and hood with their armour
// plating (goliath plates), the goliath cloak, the bone armour and the skull helmet.

// tg: code/modules/mining/equipment/explorer_gear.dm:60
public partial class obj_item_clothing_mask_gas_explorer : obj_item_clothing_mask_gas
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:84
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        return await adjust_visor(user as mob_living);
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:87
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        // adjusted = out of the way = smaller = can fit in boxes
        update_weight_class(Dm.truthy(up) ? WEIGHT_CLASS_SMALL : WEIGHT_CLASS_NORMAL);
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:92
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        inhand_icon_state = Dm.text("[][]", Dm.initial(type, (obj_item_clothing_mask_gas_explorer m) => m.inhand_icon_state), Dm.truthy(up) ? "_up" : "");
        return dot;
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:96
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(up) || Dm.eq(w_class, WEIGHT_CLASS_SMALL))
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("You could fit this into a box if you adjusted it.")));
        return dot;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:102
public partial class obj_item_clothing_mask_gas_explorer_folded : obj_item_clothing_mask_gas_explorer
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visor_toggling();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/clothing/gloves/color.dm:46  var/static/list/slapcraft_recipe_list
    internal DmList obj_item_clothing_gloves_fingerless__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_gripperoffbrand));
}

/****************Explorer's Suit and Mask****************/
// tg: code/modules/mining/equipment/explorer_gear.dm:2
public partial class obj_item_clothing_suit_hooded_explorer : obj_item_clothing_suit_hooded
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:21
    public override object get_general_color(icon base_icon = null)
    {
        return "#796755";
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate)));
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:35
public partial class obj_item_clothing_head_hooded_explorer : obj_item_clothing_head_hooded
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate)));
        return dot;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:113
public partial class obj_item_clothing_suit_hooded_cloak_goliath : obj_item_clothing_suit_hooded_cloak
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:128
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:142
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!iscarbon(user))
            return NONE;
        mob_living_carbon @char = user as mob_living_carbon;
        if (@char.get_item_by_slot(ITEM_SLOT_NECK) == this || @char.get_item_by_slot(ITEM_SLOT_OCLOTHING) == this)
        {
            to_chat(user, span_warning(I18n.T("You can't adjust [] while wearing it!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(user.is_holding(this)))
        {
            to_chat(user, span_warning(I18n.T("You must be holding [] in order to adjust it!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_OCLOTHING)))
        {
            slot_flags = ITEM_SLOT_NECK;
            cold_protection = null;
            heat_protection = null;
            set_armor(typeof(datum_armor_none));
            user.visible_message(span_notice(I18n.T("[] adjusts their [] for ceremonial use.", user, this)), span_notice(I18n.T("You adjust your [] for ceremonial use.", this)));
        }
        else
        {
            slot_flags = Dm.initial(type, (obj_item_clothing_suit_hooded_cloak_goliath c) => c.slot_flags);
            cold_protection = Dm.initial(type, (obj_item_clothing_suit_hooded_cloak_goliath c) => c.cold_protection);
            heat_protection = Dm.initial(type, (obj_item_clothing_suit_hooded_cloak_goliath c) => c.heat_protection);
            set_armor(Dm.initial(type, (obj_item_clothing_suit_hooded_cloak_goliath c) => c.armor_type));
            user.visible_message(span_notice(I18n.T("[] adjusts their [] for defensive use.", user, this)), span_notice(I18n.T("You adjust your [] for defensive use.", this)));
        }
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:166
public partial class obj_item_clothing_head_hooded_cloakhood_goliath : obj_item_clothing_head_hooded_cloakhood
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:186
public partial class obj_item_clothing_suit_armor_bone : obj_item_clothing_suit_armor
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:200
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate), Dm.named("upgrade_item", typeof(obj_item_clothing_accessory_talisman))));
        Dm.Deferred("code/modules/mining/equipment/explorer_gear.dm:203", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, /datum/bodypart_texture/mesh/drake), the drake mesh texture on the chest (bodypart textures, M10+)");
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:206
public partial class obj_item_clothing_head_helmet_skull : obj_item_clothing_head_helmet
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:222
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate), Dm.named("upgrade_item", typeof(obj_item_clothing_accessory_talisman))));
        return dot;
    }
}
