// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/floor_diseases/nebula_nausea.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var blocks are generated (D111). tg defines the stage_act on /datum/disease/advance/nebula_nausea
// (a type only that proc makes), not on the /datum/disease/nebula_nausea that dirty food gives, so the latter has no stage effects; kept.

/// Caused by dirty food. Makes you vomit stars.
// tg: code/datums/diseases/floor_diseases/nebula_nausea.dm:16
public partial class datum_disease_advance_nebula_nausea : datum_disease_advance
{
    // tg: code/datums/diseases/floor_diseases/nebula_nausea.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("The colorful beauty of the cosmos seems to have taken a toll on your equilibrium.")));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("Your stomach swirls with colors unseen by human eyes.")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("It feels like you're floating through a maelstrom of celestial colors.")));
        }
        else if (stage == 5)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("Your stomach has become a turbulent nebula, swirling with kaleidoscopic patterns.")));
            else
                await affected_mob.vomit(vomit_flags: (MOB_VOMIT_MESSAGE | MOB_VOMIT_HARM), vomit_type: typeof(obj_effect_decal_cleanable_vomit_nebula), lost_nutrition: 10, distance: 2);
        }
        return dot;
    }
}
