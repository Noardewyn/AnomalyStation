// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/hat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N2b, D316: the outlaw's cowboy hat that catches a bullet now and then); gaps CL1 (D333): the bear pelt (bears are
// friendly to its wearer with the bear suit, suits/costume.cs), the rice hat turned low, the lizard skin hat in the hide's colour and
// the ushanka's ear flaps. The var blocks are generated (D111).

// tg: code/modules/clothing/head/hat.dm:85
public partial class obj_item_clothing_head_costume_bearpelt : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/hat.dm:91
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!ishuman(user) || !Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
            return;

        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        obj_item_clothing_suit_costume_bear_suit our_suit = human_user.wear_suit as obj_item_clothing_suit_costume_bear_suit;
        if (our_suit != null)
            our_suit.make_friendly(human_user, this);
    }
}

// tg: code/modules/clothing/head/hat.dm:112
public partial class obj_item_clothing_head_cowboy : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/hat.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_bullet_intercepting),
            Dm.named("block_chance", deflect_chance),
            Dm.named("active_slots", ITEM_SLOT_HEAD),
            Dm.named("on_intercepted", Dm.@new<datum_callback>(this, nameof(on_intercepted_bullet)))));
        return dot;
    }

    /// When we catch a bullet, fling away
    // tg: code/modules/clothing/head/hat.dm:135
    public virtual async DmTask on_intercepted_bullet(mob_living victim = null, obj_projectile bullet = null)
    {
        victim.visible_message(span_warning(I18n.T("\\The [] sends []'s hat flying!", bullet, victim)));
        await victim.dropItemToGround(this, force: TRUE, silent: TRUE);
        await throw_at(get_edge_target_turf(loc as atom, pick(GLOB.alldirs)) as atom, 3, 3);
        playsound(victim, SFX_RICOCHET, 100, TRUE);
    }
}

// tg: code/modules/clothing/head/hat.dm:236
public partial class obj_item_clothing_head_costume_rice_hat : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/hat.dm:243
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        reversed = Dm.num(!Dm.truthy(reversed));
        worn_icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(reversed) ? "_kim" : "");
        to_chat(user, span_notice(I18n.T("You [] the hat.", Dm.truthy(reversed) ? I18n.T("lower") : I18n.T("raise"))));
        update_appearance();
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/clothing/head/hat.dm:249
public partial class obj_item_clothing_head_costume_lizard : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/hat.dm:259
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_item_stack_sheet_animalhide_carbon_lizard skin = Dm.locate_type(typeof(obj_item_stack_sheet_animalhide_carbon_lizard), components) as obj_item_stack_sheet_animalhide_carbon_lizard;
        if (skin == null || length(skin.skin_color) == 0) // what
        {
            await base.on_craft_completion(components, current_recipe, crafter);
            return;
        }
        await set_greyscale(skin.skin_color);
        await base.on_craft_completion(components, current_recipe, crafter);
    }
}

// tg: code/modules/clothing/head/hat.dm:346
public partial class obj_item_clothing_head_costume_ushanka : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/hat.dm:367
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(earflaps))
        {
            icon_state = upsprite;
            inhand_icon_state = upsprite;
            to_chat(user, span_notice(I18n.T("You raise the ear flaps on the ushanka.")));
        }
        else
        {
            icon_state = downsprite;
            inhand_icon_state = downsprite;
            to_chat(user, span_notice(I18n.T("You lower the ear flaps on the ushanka.")));
        }
        earflaps = Dm.num(!Dm.truthy(earflaps));
        await DmTask.CompletedTask;
        return null;
    }
}
