// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/chili.dm
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

public partial class obj_item_food_grown_bell_pepper : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/chili.dm:108
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_roasted_bell_pepper), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}
