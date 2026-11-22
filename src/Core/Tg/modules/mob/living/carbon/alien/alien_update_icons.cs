// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/alien_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/alien_update_icons.dm:4
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/alien_update_icons.dm:4
    public override async DmTask update_damage_overlays() //aliens don't have damage overlays.
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_update_icons.dm:7
    [DmParams("is_creating")]
    public override async DmTask update_body(params object[] __extra) // we don't use the bodyparts or body layers for aliens.
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_update_icons.dm:10
    [DmParams()]
    public override async DmTask<object> update_body_parts(object update_limb_data = null)//we don't use the bodyparts layer for aliens.
    {
        return null;
    }
}
