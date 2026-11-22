// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file.

// tg: code/modules/mob/living/basic/guardian/guardian_helpers.dm:2
public partial class mob_living : mob
{
    /// Returns a list of all holoparasites that has this mob as a summoner.
    // tg: code/modules/mob/living/basic/guardian/guardian_helpers.dm:2
    public virtual DmList get_all_linked_holoparasites()
    {
        DmList all_parasites = Dm.list();
        foreach (mob_living_basic_guardian stand in Dm.each_cast<mob_living_basic_guardian>(GLOB.parasites))
        {
            if (stand.summoner != this)
                continue;
            Dm.append(all_parasites, stand);
        }
        return all_parasites;
    }
}

// tg: code/modules/mob/living/basic/guardian/guardian_helpers.dm:12
public partial class mob_living_basic_guardian : mob_living_basic
{
    /// Returns true if this holoparasite has the same summoner as the passed holoparasite.
    // tg: code/modules/mob/living/basic/guardian/guardian_helpers.dm:12
    public virtual object shares_summoner(mob_living_basic_guardian other_guardian = null)
    {
        return Dm.num(other_guardian != null && other_guardian.summoner == summoner);
    }
}
