// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/brainrot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/brainrot.dm:1
public partial class datum_disease_brainrot : datum_disease
{
    // tg: code/datums/diseases/brainrot.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null) //Removed toxloss because damaging diseases are pretty horrible. Last round it killed the entire station because the cure didn't work -- Urist -ACTUALLY Removed rather than commented out, I don't see it returning - RR
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("blink");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("yawn");
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You don't feel like yourself.")));
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1, 170);
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("stare");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("drool");
            if (SPT_PROB(5, seconds_per_tick))
            {
                await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 2, 170);
                if (prob(2))
                    to_chat(affected_mob, span_danger(I18n.T("Your try to remember something important...but can't.")));
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("stare");
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("drool");
            if (SPT_PROB(7.5f, seconds_per_tick))
            {
                await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 3, 170);
                if (prob(2))
                    to_chat(affected_mob, span_danger(I18n.T("Strange buzzing fills your head, removing all thoughts.")));
            }
            if (SPT_PROB(1.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You lose consciousness...")));
                affected_mob.visible_message(span_warning(I18n.T("[] suddenly collapses!", affected_mob)),
                                            span_userdanger(I18n.T("You suddenly collapse!")));
                await affected_mob.Unconscious(rand(100, 200));
                if (prob(1))
                    await affected_mob.emote("snore");
            }
            if (SPT_PROB(7.5f, seconds_per_tick))
                await affected_mob.adjust_stutter(6 * SECONDS);
        }
        return dot;
    }
}
