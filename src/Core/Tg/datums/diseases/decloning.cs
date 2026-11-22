// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/decloning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/decloning.dm:1
public partial class datum_disease_decloning : datum_disease
{
    // tg: code/datums/diseases/decloning.dm:17
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= TRUE;
        affected_mob.remove_status_effect(typeof(datum_status_effect_decloning));
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/decloning.dm:21
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (affected_mob.stat == DEAD)
        {
            await cure();
            return FALSE;
        }

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("itch");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("yawn");
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("itch");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("drool");
            if (SPT_PROB(1.5f, seconds_per_tick))
                await affected_mob.apply_status_effect(typeof(datum_status_effect_decloning));
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your skin feels strange.")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("itch");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("drool");
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                await affected_mob.apply_status_effect(typeof(datum_status_effect_decloning));
                await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1, 170);
            }
            if (SPT_PROB(7.5f, seconds_per_tick))
                await affected_mob.adjust_stutter(6 * SECONDS);
        }
        else if (stage == 5)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("itch");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("drool");
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your skin starts degrading!")));
            if (SPT_PROB(5, seconds_per_tick))
            {
                await affected_mob.apply_status_effect(typeof(datum_status_effect_decloning));
                await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 2, 170);
            }
        }
        return dot;
    }
}
