// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/cold9.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/cold9.dm:1
public partial class datum_disease_cold9 : datum_disease
{
    // tg: code/datums/diseases/cold9.dm:15
    public override async DmTask cure(float? add_resistance = null)
    {
        // buy one, get one free
        if (Dm.truthy(add_resistance) && affected_mob != null)
            LAZYOR(ref affected_mob.disease_resistances, Dm.text("[]", typeof(datum_disease_cold)));
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/cold9.dm:21
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            affected_mob.adjust_bodytemperature(-5 * Dm.num(seconds_per_tick));
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your throat feels sore.")));
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel stiff.")));
            if (SPT_PROB(0.05f, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                await cure();
                return FALSE;
            }
        }
        else if (stage == 3)
        {
            affected_mob.adjust_bodytemperature(-10 * Dm.num(seconds_per_tick));
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your throat feels sore.")));
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel stiff.")));
        }
        return dot;
    }
}
