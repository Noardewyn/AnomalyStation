// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/tea_coffee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/tea_coffee.dm:20
public partial class obj_item_food_grown_tea : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/tea_coffee.dm:27
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_teapowder), 0));
    }
}

// tg: code/modules/hydroponics/grown/tea_coffee.dm:42
public partial class obj_item_food_grown_tea_astra : obj_item_food_grown_tea
{
    // tg: code/modules/hydroponics/grown/tea_coffee.dm:47
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_teapowder), 0), Dm.kv(typeof(datum_reagent_medicine_salglu_solution), 0));
    }
}

// tg: code/modules/hydroponics/grown/tea_coffee.dm:71
public partial class obj_item_food_grown_coffee : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/tea_coffee.dm:78
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_coffeepowder), 0));
    }
}

// tg: code/modules/hydroponics/grown/tea_coffee.dm:94
public partial class obj_item_food_grown_coffee_robusta : obj_item_food_grown_coffee
{
    // tg: code/modules/hydroponics/grown/tea_coffee.dm:99
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_coffeepowder), 0), Dm.kv(typeof(datum_reagent_medicine_morphine), 0));
    }
}
