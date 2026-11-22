// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/simple_animal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (X3): only examine; the simple animal's other procs come with the creatures (PORT-CHECKLIST section 7).

// tg: code/modules/mob/living/simple_animal/simple_animal.dm:222
public partial class mob_living_simple_animal : mob_living
{
    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (stat == DEAD)
        {
            if (HAS_MIND_TRAIT(user, TRAIT_NAIVE))
                Dm.append(ref dot, span_deadsay(I18n.T("Upon closer examination, [] appear[] to be asleep.", p_they(), p_s())));
            else
                Dm.append(ref dot, span_deadsay(I18n.T("Upon closer examination, [] appear[] to be dead.", p_they(), p_s())));
        }
        return dot;
    }
}
