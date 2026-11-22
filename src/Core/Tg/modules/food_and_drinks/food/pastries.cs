// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/pastries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared.

// tg: code/modules/food_and_drinks/food/pastries.dm:133
[DmPath("/obj/item/food/cookie")]
public partial class obj_item_food_cookie : obj_item_food
{
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
