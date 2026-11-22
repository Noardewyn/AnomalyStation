// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/korta_nut.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/korta_nut.dm:20
public partial class obj_item_food_grown_korta_nut : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/korta_nut.dm:28
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_korta_flour), 0));
    }

    // tg: code/modules/hydroponics/grown/korta_nut.dm:31
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_korta_milk);
    }
}

// tg: code/modules/hydroponics/grown/korta_nut.dm:49
public partial class obj_item_food_grown_korta_nut_sweet : obj_item_food_grown_korta_nut
{
    // tg: code/modules/hydroponics/grown/korta_nut.dm:56
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_korta_flour), 0));
    }

    // tg: code/modules/hydroponics/grown/korta_nut.dm:59
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_korta_nectar);
    }
}
