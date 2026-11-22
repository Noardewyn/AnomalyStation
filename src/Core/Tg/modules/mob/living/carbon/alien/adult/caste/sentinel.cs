// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/caste/sentinel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file. The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/adult/caste/sentinel.dm:1
public partial class mob_living_carbon_alien_adult_sentinel : mob_living_carbon_alien_adult
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/sentinel.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(Dm.@new<datum_action_cooldown_mob_cooldown_sneak_alien>(this).Grant(this)); // GRANT_ACTION
        return base.Initialize(mapload, __extra);
    }
}
