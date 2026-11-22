// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/pumpkin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/pumpkin.dm:21
public partial class obj_item_food_grown_pumpkin : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/pumpkin.dm:31
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_pumpkinjuice);
    }

    // tg: code/modules/hydroponics/grown/pumpkin.dm:34
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()))
            return NONE;

        to_chat(user, span_notice(I18n.T("You carve a face into []!", this)));
        Dm.@new(carved_type, user.loc);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/pumpkin.dm:56
public partial class obj_item_food_grown_pumpkin_blumpkin : obj_item_food_grown_pumpkin
{
    // tg: code/modules/hydroponics/grown/pumpkin.dm:64
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_blumpkinjuice);
    }
}
