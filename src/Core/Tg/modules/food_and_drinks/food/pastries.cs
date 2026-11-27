// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/pastries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cookies, the muffins (the booberry's ghosts, the moffin's look and examine), the fortune cookie's slip, the
// plump helmet biscuit's fey mood, the waffles' and the ice cream's kitchen links and holders, the sugar cookie's festive shapes.
// The var blocks are generated.

// tg: code/modules/food_and_drinks/food/pastries.dm:28
public partial class obj_item_food_muffin_booberry : obj_item_food_muffin
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ghost_edible), Dm.named("bite_consumption", bite_consumption)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:41
public partial class obj_item_food_muffin_moffin : obj_item_food_muffin
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[]_[]", base_icon_state, rand(1, 3));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pastries.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!isliving(user))
            return dot;
        mob_living moffin_observer = (mob_living)user;
        if (Dm.truthy(Dm.bitand(moffin_observer.get_liked_foodtypes(), CLOTH)))
            Dm.append(ref dot, span_nicegreen(I18n.T("Ooh! It's even got bits of clothes on it! Yummy!")));
        else
            Dm.append(ref dot, span_warning(I18n.T("You're not too sure what's on top though...")));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:133
[DmPath("/obj/item/food/cookie")]
public partial class obj_item_food_cookie : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:152
public partial class obj_item_food_fortunecookie : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:164
    public virtual async DmTask<object> get_fortune()
    {
        atom drop_location = this.drop_location() as atom;

        obj_item_paper fortune = Dm.locate<obj_item_paper>(this);
        // If a fortune exists, use that.
        if (fortune != null)
        {
            await fortune.forceMove(drop_location);
            return fortune;
        }

        // Otherwise, use a generic one
        obj_item_paper_paperslip_fortune fortune_slip = Dm.@new(trash_type, drop_location) as obj_item_paper_paperslip_fortune;
        // if someone adds lottery tickets in the future, be sure to add random numbers to this
        return fortune_slip;
    }

    // tg: code/modules/food_and_drinks/food/pastries.dm:178
    public override async DmTask make_leave_trash()
    {
        if (trash_type != null)
            await AddElement(typeof(datum_element_food_trash), trash_type, food_flags, nameof(get_fortune)); // TYPE_PROC_REF(/obj/item/food/fortunecookie, get_fortune);
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:252
public partial class obj_item_food_plumphelmetbiscuit : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:266
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        bool fey = prob(10);
        if (fey)
        {
            name = "exceptional plump helmet biscuit";
            desc = "Microwave is taken by a fey mood! It has cooked an exceptional plump helmet biscuit!";
            food_reagents = Dm.list(
                Dm.kv(typeof(datum_reagent_medicine_omnizine), 5),
                Dm.kv(typeof(datum_reagent_consumable_nutriment), 1),
                Dm.kv(typeof(datum_reagent_consumable_nutriment_vitamin), 1)
            );
        }
        object dot = base.Initialize(mapload, __extra);
        if (fey)
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_medicine_omnizine), 5));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:578
public partial class obj_item_food_cookie_macaron : obj_item_food_cookie
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:590
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[]_[]", base_icon_state, rand(1, 4));
        return dot;
    }
}

// Gaps R3 (D327): the sugar cookie's festive shapes (a holiday reader; the Halloween cookies turn them off); the file's other
// procs stay with the food.

// tg: code/modules/food_and_drinks/food/pastries.dm:194
public partial class obj_item_food_cookie_sugar : obj_item_food_cookie
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:194
    [DmParams("mapload", "seasonal_changes")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object seasonal_changes = Dm.arg(__extra, 0) ?? TRUE;
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(seasonal_changes) && Dm.truthy(check_holidays(FESTIVE_SEASON)))
        {
            object shape = pick("tree", "bear", "santa", "stocking", "present", "cane");
            desc = I18n.T("A sugar cookie in the shape of a []. I hope Santa likes it!", shape);
            icon_state = Dm.text("sugarcookie_[]", shape);
        }
        return dot;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_raw_brownie_batter : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:456
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_brownie_sheet), rand(20 * SECONDS, 30 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_brownie_sheet : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:473
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_brownie), 4, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_peanut_butter_brownie_batter : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:505
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_peanut_butter_brownie_sheet), rand(20 * SECONDS, 30 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_peanut_butter_brownie_sheet : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:523
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_peanut_butter_brownie), 4, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

// Gaps H2 (D333): the ice cream holders of the waffles and the waffle cones (the ice cream vat's readers).

// tg: code/modules/food_and_drinks/food/pastries.dm:66
public partial class obj_item_food_waffles : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:79
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("max_scoops", 1), Dm.named("x_offset", -2));
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:112
public partial class obj_item_food_rofflewaffles : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:127
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("max_scoops", 1), Dm.named("x_offset", -2));
    }
}

// tg: code/modules/food_and_drinks/food/pastries.dm:371
public partial class obj_item_food_icecream : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pastries.dm:396
    [DmParams("mapload", "prefill_flavours")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList prefill_flavours = Dm.arg(__extra, 0) as DmList;
        if (Dm.truthy(ingredients))
            ingredients_text = Dm.text("Requires: []", reagent_paths_list_to_text(ingredients));
        this.prefill_flavours = prefill_flavours;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/food_and_drinks/food/pastries.dm:402
    public override async DmTask make_edible()
    {
        await base.make_edible();
        float max_scoops = Dm.truthy(check_holidays(ICE_CREAM_DAY)) ? DEFAULT_MAX_ICE_CREAM_SCOOPS * 4 : DEFAULT_MAX_ICE_CREAM_SCOOPS;
        await AddComponent(typeof(datum_component_ice_cream_holder), max_scoops, Dm.named("filled_name", "ice cream"), Dm.named("change_desc", TRUE), Dm.named("prefill_flavours", prefill_flavours));
    }
}
