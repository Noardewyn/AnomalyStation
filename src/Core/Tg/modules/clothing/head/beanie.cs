// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/beanie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/clothing/head/beanie.dm:73  (gaps J4: the prisoner's one-in-a-hundred beanie)
public partial class obj_item_clothing_head_beanie_black_dboy : obj_item_clothing_head_beanie_black
{
    // tg: code/modules/clothing/head/beanie.dm:88
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (iscarbon(user) && beanie_owner == null)
            beanie_owner = WEAKREF(user) as datum_weakref;
    }

    // tg: code/modules/clothing/head/beanie.dm:93
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (IS_WEAKREF_OF(user, beanie_owner))
            dot.Add(span_purple(I18n.T("It's covered in otherworldly debris only your eyes have been ruined enough to see.")));
        return dot;
    }
}
