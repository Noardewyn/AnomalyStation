// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/sandwichtoast.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) translated (gaps package H2, D333); the rest of the file is generated or waits for its package.

public partial class obj_item_food_sandwich_cheese : obj_item_food_sandwich
{
    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:32
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_sandwich_grilled_cheese), rand(30 * SECONDS, 60 * SECONDS), TRUE);
    }
}
