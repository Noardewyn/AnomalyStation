// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/sombrero.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the shamebrero, which cannot be taken off. The var blocks are generated (D111).

// tg: code/modules/clothing/head/sombrero.dm:26
public partial class obj_item_clothing_head_costume_sombrero_shamebrero : obj_item_clothing_head_costume_sombrero
{
    // tg: code/modules/clothing/head/sombrero.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, SHAMEBRERO_TRAIT);
        return dot;
    }
}
