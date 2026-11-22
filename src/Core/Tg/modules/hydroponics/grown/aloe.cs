// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/aloe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/aloe.dm:20
public partial class obj_item_food_grown_aloe : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/aloe.dm:28
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_aloejuice);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_aloe : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/aloe.dm:31
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_stack_medical_aloe_fresh), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/hydroponics/grown/aloe.dm:34
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_stack_medical_aloe_fresh));
    }
}
