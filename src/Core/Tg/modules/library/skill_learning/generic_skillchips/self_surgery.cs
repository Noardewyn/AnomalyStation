// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/self_surgery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The self surgery chip (TRAIT_SELF_SURGERY), contraband. The var block is generated (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/self_surgery.dm:1
public partial class obj_item_skillchip_self_surgery : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/self_surgery.dm:12
    [DmParams("mapload", "is_removable")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}
