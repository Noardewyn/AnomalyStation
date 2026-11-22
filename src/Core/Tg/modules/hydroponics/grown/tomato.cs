// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/tomato.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/tomato.dm:20
public partial class obj_item_food_grown_tomato : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/tomato.dm:28
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_ketchup), 0));
    }

    // tg: code/modules/hydroponics/grown/tomato.dm:31
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_tomatojuice);
    }
}

// tg: code/modules/hydroponics/grown/tomato.dm:47
public partial class obj_item_food_grown_tomato_blood : obj_item_food_grown_tomato
{
    // tg: code/modules/hydroponics/grown/tomato.dm:56
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_ketchup), 0), Dm.kv(typeof(datum_reagent_blood), 0));
    }
}
