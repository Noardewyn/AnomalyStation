// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/beesease.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/beesease.dm:1
public partial class datum_disease_beesease : datum_disease
{

    // tg: code/datums/diseases/beesease.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2) //also changes say, see say.dm
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("You taste honey in your mouth.")));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("Your stomach rumbles.")));
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your stomach stings painfully.")));
                if (prob(20))
                    await affected_mob.adjust_tox_loss(2);
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(5, seconds_per_tick))
                affected_mob.visible_message(span_danger(I18n.T("[] buzzes.", affected_mob)),
                                                span_userdanger(I18n.T("Your stomach buzzes violently!")));
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel something moving in your throat.")));
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                affected_mob.visible_message(span_danger(I18n.T("[] coughs up a swarm of bees!", affected_mob)),
                                                    span_userdanger(I18n.T("You cough up a swarm of bees!")));
                Dm.@new<mob_living_basic_bee>(affected_mob.loc);
            }
        }
        return dot;
    }
}
