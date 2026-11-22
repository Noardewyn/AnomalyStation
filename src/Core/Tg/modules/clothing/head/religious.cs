// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/religious.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the white and red taqiyahs' small pockets. The var blocks are generated (D111).

// tg: code/modules/clothing/head/religious.dm:26
public partial class obj_item_clothing_head_chaplain_taqiyah_white : obj_item_clothing_head_chaplain_taqiyah
{
    // tg: code/modules/clothing/head/religious.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_small)));
        return dot;
    }
}

// tg: code/modules/clothing/head/religious.dm:36
public partial class obj_item_clothing_head_chaplain_taqiyah_red : obj_item_clothing_head_chaplain_taqiyah
{
    // tg: code/modules/clothing/head/religious.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_small)));
        return dot;
    }
}
