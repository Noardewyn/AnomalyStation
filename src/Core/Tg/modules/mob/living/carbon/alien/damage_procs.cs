// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/damage_procs.dm:2
public partial class mob_living_carbon_alien : mob_living_carbon
{
    ///alien immune to tox damage
    // tg: code/modules/mob/living/carbon/alien/damage_procs.dm:2
    public override object get_tox_loss()
    {
        return FALSE;
    }

    ///alien immune to tox damage
    // tg: code/modules/mob/living/carbon/alien/damage_procs.dm:6
    public override async DmTask<object> adjust_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        return FALSE;
    }

    ///aliens are immune to stamina damage.
    // tg: code/modules/mob/living/carbon/alien/damage_procs.dm:10
    public override async DmTask<object> adjust_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null)
    {
        return FALSE;
    }

    ///aliens are immune to stamina damage.
    // tg: code/modules/mob/living/carbon/alien/damage_procs.dm:14
    public override async DmTask<object> set_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null)
    {
        return FALSE;
    }
}
