// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 3: wet floors and slipping): only slip translated; the carbon's Move (hunger), set_usable_legs and the
// rest come with their mechanics.

// tg: code/modules/mob/living/carbon/carbon_movement.dm:1
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_movement.dm:1
    public override async DmTask<object> slip(object knockdown_amount = null, atom slipped_on = null, object lube_flags = null, object paralyze = null, object daze = null, float? force_drop = null)
    {
        force_drop ??= FALSE;
        if (!Dm.truthy(await loc.handle_slip(this, knockdown_amount, slipped_on, lube_flags, paralyze, daze, force_drop)))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(lube_flags, SLIDE_ICE)))
            log_combat(this, (slipped_on as atom) ?? get_turf(this) as atom, "slipped on the", null, (Dm.truthy(Dm.bitand(lube_flags, SLIDE)) ? "(SLIDING)" : null));

        return await base.slip(knockdown_amount, slipped_on, lube_flags, paralyze, daze, force_drop);
    }
}
