// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/berries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/berries.dm:22
public partial class obj_item_food_grown_berries : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/berries.dm:31
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_berryjuice);
    }
}

// tg: code/modules/hydroponics/grown/berries.dm:47
public partial class obj_item_food_grown_berries_poison : obj_item_food_grown_berries
{
    // tg: code/modules/hydroponics/grown/berries.dm:57
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_poisonberryjuice);
    }
}

// tg: code/modules/hydroponics/grown/berries.dm:77
public partial class obj_item_food_grown_berries_death : obj_item_food_grown_berries
{
    // tg: code/modules/hydroponics/grown/berries.dm:87
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_poisonberryjuice);
    }
}

// tg: code/modules/hydroponics/grown/berries.dm:139
public partial class obj_item_food_grown_grapes : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/berries.dm:148
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_grapejuice);
    }
}

// tg: code/modules/hydroponics/grown/berries.dm:193
public partial class obj_item_food_grown_toechtauese : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/berries.dm:201
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_toechtauese_juice);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_grapes : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/berries.dm:151
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_no_raisin_healthy));
    }
}
