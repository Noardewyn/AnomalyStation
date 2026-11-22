// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/fluspanish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/fluspanish.dm:1
public partial class datum_disease_fluspanish : datum_disease
{
    // tg: code/datums/diseases/fluspanish.dm:16
    public override async DmTask cure(float? add_resistance = null)
    {
        // buy one, get one free
        if (Dm.truthy(add_resistance) && affected_mob != null)
            LAZYOR(ref affected_mob.disease_resistances, Dm.text("[]", typeof(datum_disease_flu)));
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/fluspanish.dm:22
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            affected_mob.adjust_bodytemperature(5 * Dm.num(seconds_per_tick));
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You're burning in your own skin!")));
                await affected_mob.take_bodypart_damage(0, 5, updating_health: FALSE);
            }
        }
        else if (stage == 3)
        {
            affected_mob.adjust_bodytemperature(10 * Dm.num(seconds_per_tick));
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("sneeze");
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You're burning in your own skin!")));
                await affected_mob.take_bodypart_damage(0, 5, updating_health: FALSE);
            }
        }
        return dot;
    }
}
