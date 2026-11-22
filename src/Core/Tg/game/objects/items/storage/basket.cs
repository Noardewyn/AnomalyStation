// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/basket.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SR1 (D333): the file whole (the handwoven basket). The var block is generated (D111).

// tg: code/game/objects/items/storage/basket.dm:1
public partial class obj_item_storage_basket : obj_item_storage
{
    // tg: code/game/objects/items/storage/basket.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/storage/basket.dm:12", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: the cuffed_item status effect, not-translated.md section 46)");
        return dot;
    }
}
