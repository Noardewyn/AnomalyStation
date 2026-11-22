// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/whoopee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the whoopee cushion's squeaks, every step on April Fools). The var block is generated (D111).

/// Whoopee cushion for the april fools mail pool
/// I fear what would happen if this were to be released outside of april fools but there's a `check_holidays()` in there just in case
// tg: code/modules/clothing/head/whoopee.dm:3
public partial class obj_item_clothing_head_costume_whoopee : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/whoopee.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(check_holidays(APRIL_FOOLS)))
            step_delay = 1;
        Waiting.NoSleep(LoadComponent(typeof(datum_component_squeak), fecal_funnies, 50, Dm.named("falloff_exponent", 20), Dm.named("step_delay_override", step_delay)));
        return dot;
    }
}
