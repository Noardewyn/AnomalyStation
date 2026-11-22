// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/root.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/root.dm:2
public partial class obj_item_food_grown_carrotlike : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/root.dm:15
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()))
            return NONE;

        /// The blade our "carrot" will turn into once sharpened
        obj_item blade;
        /// Chance for it to become better than a shiv
        float sword_chance = (Dm.max(0, Dm.num(((obj_item_seeds)seed).potency) - potency_minimum) / 50);
        if (prob(sword_chance))
        {
            blade = Dm.@new(blade_type) as obj_item;
            to_chat(user, span_notice(I18n.T("You sharpen \\the [] into a [] with [].", this, blade_string, tool)));
        }
        else
        {
            blade = Dm.@new(shiv_type) as obj_item;
            to_chat(user, span_notice(I18n.T("You sharpen \\the [] into a shiv with [].", this, tool)));
        }
        await remove_item_from_storage(user);
        qdel(this);
        await user.put_in_hands(blade);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/root.dm:53
public partial class obj_item_food_grown_carrotlike_carrot : obj_item_food_grown_carrotlike
{
    // tg: code/modules/hydroponics/grown/root.dm:65
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_carrotjuice);
    }
}

// tg: code/modules/hydroponics/grown/root.dm:81
public partial class obj_item_food_grown_carrotlike_parsnip : obj_item_food_grown_carrotlike
{
    // tg: code/modules/hydroponics/grown/root.dm:92
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_parsnipjuice);
    }
}

// tg: code/modules/hydroponics/grown/root.dm:113
public partial class obj_item_food_grown_carrotlike_cahnroot : obj_item_food_grown_carrotlike
{
    // tg: code/modules/hydroponics/grown/root.dm:126
    public override object juice_typepath()
    {
        return null;
    }
}
