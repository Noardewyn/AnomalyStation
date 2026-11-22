// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/cult.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the ghost's cult boots, which cannot be taken off; the rest of the file is var-only. The var blocks are
// generated (D111).

// tg: code/modules/clothing/shoes/cult.dm:16
public partial class obj_item_clothing_shoes_cult_alt_ghost : obj_item_clothing_shoes_cult_alt
{
    // tg: code/modules/clothing/shoes/cult.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CULT_TRAIT);
        return dot;
    }
}
