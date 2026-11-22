// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/sugarcane.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/sugarcane.dm:50
public partial class obj_item_grown_log_bamboo : obj_item_grown_log
{
    // tg: code/modules/hydroponics/grown/sugarcane.dm:57
    public override object CheckAccepted(obj_item I = null)
    {
        return FALSE;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_sugarcane_saltcane : obj_item_food_grown_sugarcane
{
    // tg: code/modules/hydroponics/grown/sugarcane.dm:79
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_seaweedsheet_saltcane)); //soooshi
    }
}
