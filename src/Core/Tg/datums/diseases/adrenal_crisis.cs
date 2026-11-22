// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/adrenal_crisis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). Kronkaine's overdose gives it (drug_reagents.dm, a hook the
// lead wires with gaps RG3's rewrite of that file).

// tg: code/datums/diseases/adrenal_crisis.dm:1
public partial class datum_disease_adrenal_crisis : datum_disease
{
    // tg: code/datums/diseases/adrenal_crisis.dm:19
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel lightheaded.", "You feel lethargic."))))));
        }
        else if (stage == 2)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.Unconscious(40);

            if (SPT_PROB(10, seconds_per_tick))
                await affected_mob.adjust_slurring(14 * SECONDS);

            if (SPT_PROB(7, seconds_per_tick))
                await affected_mob.set_dizzy_if_lower(20 * SECONDS);

            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel pain shoot down your legs!", "You feel like you are going to pass out at any moment.", "You feel really dizzy."))))));
        }
        return dot;
    }
}
