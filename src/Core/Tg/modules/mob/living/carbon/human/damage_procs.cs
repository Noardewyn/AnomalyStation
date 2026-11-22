// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "human": a human with high toxins starts vomiting): the human's toxin loss
// overrides.

// tg: code/modules/mob/living/carbon/human/damage_procs.dm:1
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/damage_procs.dm:1
    public override async DmTask<object> adjust_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        object dot = null;
        dot = await base.adjust_tox_loss(amount, updating_health, forced, required_biotype);
        if (Dm.ge(dot, 0)) // 0 = no damage, + values = healed damage
            return dot;

        if (AT_TOXIN_VOMIT_THRESHOLD(this))
            await apply_status_effect(typeof(datum_status_effect_tox_vomit));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/damage_procs.dm:9
    public override async DmTask<object> set_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        object dot = null;
        dot = await base.set_tox_loss(amount, updating_health, forced, required_biotype);
        if (Dm.ge(dot, 0))
            return dot;

        if (AT_TOXIN_VOMIT_THRESHOLD(this))
            await apply_status_effect(typeof(datum_status_effect_tox_vomit));
        return dot;
    }
}
