// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/death_sandwich_poisoning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/death_sandwich_poisoning.dm:1
public partial class datum_disease_death_sandwich_poisoning : datum_disease
{
    // tg: code/datums/diseases/death_sandwich_poisoning.dm:19
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(1.5f, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(0.5f, seconds_per_tick))
                await affected_mob.adjust_tox_loss(5);
        }
        else if (stage == 2)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your body feels hot!")));
                if (prob(20))
                    await affected_mob.take_bodypart_damage(burn: 1);
            }
            if (SPT_PROB(3, seconds_per_tick))
                await affected_mob.adjust_tox_loss(10);
        }

        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(10, seconds_per_tick))
                await affected_mob.emote("gasp");
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 20);
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("Your body feels hot!")));
                if (prob(60))
                    await affected_mob.take_bodypart_damage(burn: 2);
            }
            if (SPT_PROB(6, seconds_per_tick))
                await affected_mob.adjust_tox_loss(15);
            if (SPT_PROB(1.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You try to scream, but nothing comes out!")));
                await affected_mob.set_silence_if_lower(5 * SECONDS);
            }
        }
        return dot;
    }
}
