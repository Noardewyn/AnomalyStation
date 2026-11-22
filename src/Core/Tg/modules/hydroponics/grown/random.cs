// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/random.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/random.dm:4
public partial class obj_item_seeds_random : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/random.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        randomize_stats();
        if (prob(60))
            add_random_reagents(1, 3);
        if (prob(50))
            add_random_traits(1, 2);
        add_random_plant_type(35);
        return dot;
    }
}

// tg: code/modules/hydroponics/grown/random.dm:26
public partial class obj_item_food_grown_random : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/random.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        wine_power = rand(10, 150);
        if (prob(1))
            wine_power = 200;
        return dot;
    }
}
