// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/potato.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/potato.dm:23
public partial class obj_item_food_grown_potato : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/potato.dm:30
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_potato_juice);
    }

    // tg: code/modules/hydroponics/grown/potato.dm:42
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()))
            return NONE;
        to_chat(user, span_notice(I18n.T("You cut the potato into wedges with [].", tool)));
        obj_item_food_grown_potato_wedges slices = Dm.@new<obj_item_food_grown_potato_wedges>();
        await remove_item_from_storage(user);
        qdel(this);
        await user.put_in_hands(slices);
        return ITEM_INTERACT_SUCCESS;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_potato : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/potato.dm:33
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_baked_potato), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_grown_potato_sweet : obj_item_food_grown_potato
{
    // tg: code/modules/hydroponics/grown/potato.dm:71
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_yakiimo), rand(15 * SECONDS, 35 * SECONDS), TRUE, TRUE);
    }
}
