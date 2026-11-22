// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/bone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the bone bracers; their fishing difficulty element waits for fishing). The var block is
// generated (D111).

// tg: code/modules/clothing/gloves/bone.dm:1
public partial class obj_item_clothing_gloves_bracer : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/bone.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/bone.dm:18", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 2): fishing (M10+)");
        return dot;
    }
}
