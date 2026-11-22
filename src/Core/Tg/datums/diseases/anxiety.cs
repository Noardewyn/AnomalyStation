// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/anxiety.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/anxiety.dm:1
public partial class datum_disease_anxiety : datum_disease
{

    // tg: code/datums/diseases/anxiety.dm:15
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2) //also changes say, see say.dm
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("You feel anxious.")));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("Your stomach flutters.")));
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("You feel panicky.")));
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You're overtaken with panic!")));
                await affected_mob.adjust_confusion(rand(2 * SECONDS, 3 * SECONDS));
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel butterflies in your stomach.")));
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                affected_mob.visible_message(span_danger(I18n.T("[] stumbles around in a panic.", affected_mob)),
                                                span_userdanger(I18n.T("You have a panic attack!")));
                await affected_mob.adjust_confusion(rand(6 * SECONDS, 8 * SECONDS));
                await affected_mob.adjust_jitter(rand(12 * SECONDS, 16 * SECONDS));
            }
            if (SPT_PROB(1, seconds_per_tick))
            {
                affected_mob.visible_message(span_danger(I18n.T("[] coughs up butterflies!", affected_mob)),
                                                    span_userdanger(I18n.T("You cough up butterflies!")));
                Dm.@new<mob_living_basic_butterfly>(affected_mob.loc);
                Dm.@new<mob_living_basic_butterfly>(affected_mob.loc);
            }
        }
        return dot;
    }
}
