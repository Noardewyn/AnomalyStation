// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/pineapple.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/pineapple.dm:19
public partial class obj_item_food_grown_pineapple : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/pineapple.dm:39
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_pineapplejuice);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_pineapple : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/pineapple.dm:36
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_pineappleslice), 3, 15, Dm.named("screentip_verb", "Cut"));
    }
}
