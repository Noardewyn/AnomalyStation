// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/botany.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the botanist's leather gloves' Initialize (the fishing difficulty element is a deferred leaf, fishing M10+). The var block is generated.

// tg: code/modules/clothing/gloves/botany.dm:2
public partial class obj_item_clothing_gloves_botanic_leather : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/botany.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, -4)
        Dm.Deferred("code/modules/clothing/gloves/botany.dm:17", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}
