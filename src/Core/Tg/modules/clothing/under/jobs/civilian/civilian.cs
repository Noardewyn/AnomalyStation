// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/jobs/civilian/civilian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps H1, the washing machine): the lawyer's suits' dye_item. The rest of the file is var blocks (generated, D111).

// tg: code/modules/clothing/under/jobs/civilian/civilian.dm:101
public partial class obj_item_clothing_under_rank_civilian_lawyer : obj_item_clothing_under_rank_civilian
{
    // tg: code/modules/clothing/under/jobs/civilian/civilian.dm:108
    public override async DmTask<object> dye_item(object dye_color = null, object dye_key_override = null)
    {
        if (Dm.eq(dye_color, DYE_COSMIC) || Dm.eq(dye_color, DYE_SYNDICATE))
        {
            if (Dm.eq(dying_key, DYE_REGISTRY_JUMPSKIRT))
                return await base.dye_item(dye_color, DYE_LAWYER_SPECIAL_SKIRT);
            else
                return await base.dye_item(dye_color, DYE_LAWYER_SPECIAL);
        }
        else
            return await base.dye_item(dye_color, dye_key_override);
    }
}
