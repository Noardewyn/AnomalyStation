// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/cherries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/cherries.dm:24
public partial class obj_item_food_grown_cherries : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cherries.dm:34
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_cherryjelly), 0));
    }
}

// tg: code/modules/hydroponics/grown/cherries.dm:50
public partial class obj_item_food_grown_bluecherries : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cherries.dm:59
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_bluecherryjelly), 0));
    }
}

// tg: code/modules/hydroponics/grown/cherries.dm:77
public partial class obj_item_food_grown_cherrybulbs : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cherries.dm:86
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_cherryjelly), 0));
    }
}
