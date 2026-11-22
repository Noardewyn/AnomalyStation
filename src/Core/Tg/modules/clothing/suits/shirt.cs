// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/shirt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the well-worn shirt shrinks in the washing machine and implodes after six washes). The var
// blocks are generated (D111).

// tg: code/modules/clothing/suits/shirt.dm:1
public partial class obj_item_clothing_suit_costume_wellworn_shirt : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/shirt.dm:18
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        await base.machine_wash(washer, __extra);
        if (wash_count <= 5)
        {
            transform = Dm.mul(transform, Dm.num(Dm.pow(Dm.sin((90 * 0.8f) / 1), 2))); // TRANSFORM_USING_VARIABLE(0.8, 1)
            washer.visible_message(I18n.T("[] appears to have shrunken after being washed.", this));
            wash_count += 1;
        }
        else
        {
            washer.visible_message(I18n.T("[] implodes due to repeated washing.", this));
            qdel(this);
        }
    }
}
