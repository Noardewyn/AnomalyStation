// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/rhumba_beat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/rhumba_beat.dm:1
public partial class datum_disease_rhumba_beat : datum_disease
{
    // tg: code/datums/diseases/rhumba_beat.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(26, seconds_per_tick))
                await affected_mob.adjust_fire_loss(5);
            if (SPT_PROB(0.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel strange...")));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel the urge to dance...")));
            else if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("gasp");
            else if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel the need to chick chicky boom...")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(10, seconds_per_tick))
            {
                if (prob(50))
                {
                    await affected_mob.adjust_fire_stacks(2);
                    affected_mob.ignite_mob();
                }
                else
                {
                    await affected_mob.emote("gasp");
                    to_chat(affected_mob, span_danger(I18n.T("You feel a burning beat inside...")));
                }
            }
        }
        else if (stage == 5)
        {
            to_chat(affected_mob, span_danger(I18n.T("Your body is unable to contain the Rhumba Beat...")));
            if (SPT_PROB(29, seconds_per_tick))
                await explosion(affected_mob, devastation_range: -1, light_impact_range: 2, flame_range: 2, flash_range: 3, adminlog: FALSE, explosion_cause: this); // This is equivalent to a lvl 1 fireball
        }
        return dot;
    }
}
