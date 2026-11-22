// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/jobs/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the scrubs' Initialize (the fishing difficulty element is a deferred leaf, fishing M10+). The var blocks are generated.

// tg: code/modules/clothing/under/jobs/medical.dm:90
public partial class obj_item_clothing_under_rank_medical_scrubs : obj_item_clothing_under_rank_medical
{
    // tg: code/modules/clothing/under/jobs/medical.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, -3) //FISH DOCTOR?!
        Dm.Deferred("code/modules/clothing/under/jobs/medical.dm:96", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -3): fishing (M10+)");
        return dot;
    }
}
