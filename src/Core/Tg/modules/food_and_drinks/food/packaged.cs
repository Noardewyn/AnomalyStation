// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/packaged.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable), the cans' base (:4-38: opening a can) and the Ready-Donk's box (:241) translated (gaps package H2, D333);
// the ration pack (:359, the chilling silver slime extract's emergency food); the rest of the file is generated or waits for
// its package.

// Cans
// tg: code/modules/food_and_drinks/food/packaged.dm:4
public partial class obj_item_food_canned : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:19
    public override async DmTask make_germ_sensitive(object mapload = null)
    {
        return; // It's in a can
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:22
    public virtual async DmTask open_can(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You pull back the tab of \\the [].", this)));
        playsound(user.loc, Dm.resource("sound/items/foodcanopen.ogg"), 50);
        Dm.combine(ref ((datum_reagents)reagents).flags, OPENCONTAINER);
        preserved_food = FALSE;
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:28
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_drainable()))
        {
            await open_can(user);
            icon_state = Dm.text("[]_open", icon_state);
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:34  (tg names the third argument def_zone)
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_drainable()))
        {
            to_chat(user, span_warning(I18n.T("[]'s lid hasn't been opened!", this)));
            return FALSE;
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/packaged.dm:178
public partial class obj_item_food_canned_chap : obj_item_food_canned
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:187
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_chapslice), 5, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_chapslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:202
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_grilled_chapslice), rand(20 * SECONDS, 40 * SECONDS), TRUE, TRUE);
    }
}

// DONK DINNER: THE INNOVATIVE WAY TO GET YOUR DAILY RECOMMENDED ALLOWANCE OF SALT... AND THEN SOME!
// tg: code/modules/food_and_drinks/food/packaged.dm:219
public partial class obj_item_food_ready_donk : obj_item_food
{
    /// What reagents should be added when this item is warmed?
    // tg: code/modules/food_and_drinks/food/packaged.dm:233  var/static/list/added_reagents (rules 10.6)
    public static DmList added_reagents => TgState.Current.obj_item_food_ready_donk__added_reagents ??= Dm.list(Dm.kv(typeof(datum_reagent_medicine_omnizine), 3));

    // tg: code/modules/food_and_drinks/food/packaged.dm:235
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE, added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:238
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:241
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        dot.Add(span_notice(I18n.T("<i>You browse the back of the box...</i>")));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Ready-Donk: a product of Donk Co."))));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Heating instructions: open box and pierce film, heat in microwave on high for 2 minutes. Allow to stand for 60 seconds prior to eating. Product will be hot."))));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Per 200g serving contains: 8g Sodium; 25g Fat, of which 22g are saturated; 2g Sugar."))));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:233  /obj/item/food/ready_donk's var/static/list/added_reagents
    internal DmList obj_item_food_ready_donk__added_reagents;
}

// Rations
// tg: code/modules/food_and_drinks/food/packaged.dm:359
public partial class obj_item_food_rationpack : obj_item_food
{
    ///Override for checkliked callback
    // tg: code/modules/food_and_drinks/food/packaged.dm:373
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:377
    public virtual object check_liked(mob mob = null) //Nobody likes rationpacks. Nobody.
    {
        return FOOD_DISLIKED;
    }
}
