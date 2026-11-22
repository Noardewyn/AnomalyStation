// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/surgical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the sterile mask whole (a mask pulled down with the Adjust action or in hand). The var block is generated.

// tg: code/modules/clothing/masks/surgical.dm:2
public partial class obj_item_clothing_mask_surgical : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/surgical.dm:17
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }
}
