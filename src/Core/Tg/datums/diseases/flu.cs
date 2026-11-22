// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/flu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/flu.dm:1
public partial class datum_disease_flu : datum_disease
{
    // tg: code/datums/diseases/flu.dm:15
    public override async DmTask cure(float? add_resistance = null)
    {
        // buy one, get one free
        if (Dm.truthy(add_resistance) && affected_mob != null)
            LAZYOR(ref affected_mob.disease_resistances, Dm.text("[]", typeof(datum_disease_fluspanish)));
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/flu.dm:21
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your muscles ache.")));
                if (prob(20))
                    await affected_mob.take_bodypart_damage(1, updating_health: FALSE);
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your stomach hurts.")));
                if (prob(20))
                    await affected_mob.adjust_tox_loss(1, FALSE);
            }
            if (affected_mob.body_position == LYING_DOWN && SPT_PROB(10, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                stage--;
                return dot;
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your muscles ache.")));
                if (prob(20))
                    await affected_mob.take_bodypart_damage(1, updating_health: FALSE);
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your stomach hurts.")));
                if (prob(20))
                    await affected_mob.adjust_tox_loss(1, FALSE);
            }
            if (affected_mob.body_position == LYING_DOWN && SPT_PROB(7.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                stage--;
                return dot;
            }
        }
        return dot;
    }
}
