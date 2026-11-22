// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/corn.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/corn.dm:20
public partial class obj_item_food_grown_corn : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/corn.dm:30
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_cornmeal), 0), Dm.kv(typeof(datum_reagent_consumable_nutriment_fat_oil_corn), 0));
    }

    // tg: code/modules/hydroponics/grown/corn.dm:33
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_corn_starch);
    }
}

// tg: code/modules/hydroponics/grown/corn.dm:43
public partial class obj_item_grown_corncob : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/corn.dm:53
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 10));
    }

    // tg: code/modules/hydroponics/grown/corn.dm:56
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()))
            return NONE;

        to_chat(user, span_notice(I18n.T("You use [] to fashion a pipe out of the corn cob!", tool)));
        Dm.@new<obj_item_cigarette_pipe_cobpipe>(user.loc);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/corn.dm:77
public partial class obj_item_grown_snapcorn : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/corn.dm:88
    public override object add_juice()
    {
        object dot = base.add_juice();
        snap_pops = Dm.max(Dm.round(Dm.num(((obj_item_seeds)seed).potency) / 8), 1);
        return dot;
    }

    // tg: code/modules/hydroponics/grown/corn.dm:92
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        to_chat(user, span_notice(I18n.T("You pick a snap pop from the cob.")));
        obj_item_toy_snappop S = Dm.@new<obj_item_toy_snappop>(user.loc);
        if (ishuman(user))
            await user.put_in_hands(S);
        snap_pops -= 1;
        if (!Dm.truthy(snap_pops))
        {
            Dm.@new<obj_item_grown_corncob_snap>(user.loc);
            qdel(this);
        }
        return null;
    }
}

// tg: code/modules/hydroponics/grown/corn.dm:120
public partial class obj_item_food_grown_peppercorn : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/corn.dm:128
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_blackpepper), 0));
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_corn : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/corn.dm:36
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_oven_baked_corn), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/hydroponics/grown/corn.dm:39
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_popcorn));
    }
}
