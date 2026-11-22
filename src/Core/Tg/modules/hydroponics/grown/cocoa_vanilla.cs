// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/cocoa_vanilla.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/cocoa_vanilla.dm:76
public partial class obj_item_food_grown_bungofruit : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cocoa_vanilla.dm:86
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_bungojuice);
    }
}

// tg: code/modules/hydroponics/grown/cocoa_vanilla.dm:90
public partial class obj_item_food_grown_bungopit : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cocoa_vanilla.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_reagents holder = reagents as datum_reagents;
        Waiting.NoSleep(holder.clear_reagents());
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_toxin_bungotoxin), Dm.num(((obj_item_seeds)seed).potency) * 0.10f)); //More than this will kill at too low potency
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_consumable_nutriment), Dm.num(((obj_item_seeds)seed).potency) * 0.04f));
        return dot;
    }
}
