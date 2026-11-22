// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/examine.dm:1
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/examine.dm:1
    public override async DmTask<object> examine(mob user = null, params object[] __extra) //Displays a silicon's laws to ghosts
    {
        object dot = await base.examine(user, __extra);
        if (laws != null && isobserver(user))
        {
            Dm.append(ref dot, I18n.T("<b>[] has the following laws:</b>", this));
            foreach (object law in Dm.each(laws.get_law_list(include_zeroth: TRUE)))
                Dm.append(ref dot, law);
        }
        return dot;
    }
}
