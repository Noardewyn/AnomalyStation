// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/punch_mitts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the punching mitts (hunter boxing), whole but the fishing difficulty (fishing is not ported).
// The var blocks are generated (D111).

// tg: code/modules/clothing/gloves/punch_mitts.dm:1
public partial class obj_item_clothing_gloves_fingerless_punch_mitts : obj_item_clothing_gloves_fingerless
{
    // tg: code/modules/clothing/gloves/punch_mitts.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/punch_mitts.dm:12", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -6): fishing (M10+)");
        Waiting.NoSleep(AddComponent(typeof(datum_component_martial_art_giver), typeof(datum_martial_art_boxing_hunter)));
        return dot;
    }
}
