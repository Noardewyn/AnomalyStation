// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/moth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the original mothic flightsuit's pockets (its fishing difficulty waits for fishing); the rest of the file
// is var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/suits/moth.dm:17
public partial class obj_item_clothing_suit_mothcoat_original : obj_item_clothing_suit_mothcoat
{
    // tg: code/modules/clothing/suits/moth.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/moth.dm:24", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets)));
        return dot;
    }
}
