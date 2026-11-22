// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/bandana.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the facescarf (pulled up over the face or down with its action, a use in hand or an alt-click). The
// bandanas' procs are in modules/clothing/job_gear_j5.cs (gaps J5). The var blocks are generated (D111).

// tg: code/modules/clothing/masks/bandana.dm:229
public partial class obj_item_clothing_mask_facescarf : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/bandana.dm:254
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/masks/bandana.dm:257
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/masks/bandana.dm:262
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to adjust it.", this)));
        return dot;
    }
}
