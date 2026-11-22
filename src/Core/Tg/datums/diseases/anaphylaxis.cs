// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/anaphylaxis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/anaphylaxis.dm:1
public partial class datum_disease_anaphylaxis : datum_disease
{
    // tg: code/datums/diseases/anaphylaxis.dm:19
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (HAS_TRAIT(affected_mob, TRAIT_TOXINLOVER)) // You are no fun
        {
            await cure();
            return dot;
        }

        float spt = Dm.num(seconds_per_tick);
        // Cool them enough to feel cold to the touch, and then some, because temperature mechanics are dumb
        affected_mob.adjust_bodytemperature(-10 * spt * stage, min_temp: BODYTEMP_COLD_DAMAGE_LIMIT - 70);

        // early symptoms: mild shakes and dizziness
        if (stage == 1)
        {
            if (affected_mob.num_hands >= 1 && SPT_PROB(5, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel your hand[] start to shake.", affected_mob.num_hands == 1 ? "" : "s")));
                await affected_mob.adjust_jitter_up_to(4 * SECONDS * spt, 1 * MINUTES);
            }
            if (affected_mob.num_legs >= 1 && SPT_PROB(5, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel your leg[] start to shake.", affected_mob.num_hands == 1 ? "" : "s")));
                await affected_mob.adjust_jitter_up_to(4 * SECONDS * spt, 1 * MINUTES);
            }
            if (SPT_PROB(2, seconds_per_tick))
                await affected_mob.adjust_dizzy_up_to(5 * SECONDS * spt, 1 * MINUTES);
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your throat itches.")));
        }

        // warning symptoms: violent shakes, dizziness, blurred vision, difficulty breathing
        else if (stage == 2)
        {
            await affected_mob.apply_damage(0.33f * spt, TOX, spread_damage: TRUE);

            if (affected_mob.num_hands >= 1 && SPT_PROB(5, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel your hand[] shake violently.", affected_mob.num_hands == 1 ? "" : "s")));
                await affected_mob.adjust_jitter_up_to(8 * SECONDS * spt, 1 * MINUTES);
                if (prob(20))
                    await affected_mob.drop_all_held_items();
            }
            if (affected_mob.num_legs >= 1 && SPT_PROB(5, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel your leg[] shake violently.", affected_mob.num_hands == 1 ? "" : "s")));
                await affected_mob.adjust_jitter_up_to(8 * SECONDS * spt, 1 * MINUTES);
                if (prob(40) && Dm.num(affected_mob.get_stamina_loss()) < 75)
                    await affected_mob.adjust_stamina_loss(15);
            }
            if (Dm.truthy(affected_mob.get_organ_slot(ORGAN_SLOT_EYES)) && SPT_PROB(4, seconds_per_tick))
            {
                await affected_mob.adjust_eye_blur(4 * SECONDS * spt);
                to_chat(affected_mob, span_warning(I18n.T("It's getting harder to see clearly.")));
            }
            if (!HAS_TRAIT(affected_mob, TRAIT_NOBREATH) && SPT_PROB(4, seconds_per_tick))
            {
                await affected_mob.apply_damage(2 * spt, OXY);
                affected_mob.losebreath += (2 * spt);
                to_chat(affected_mob, span_warning(I18n.T("It's getting harder to breathe.")));
            }
            if (SPT_PROB(2, seconds_per_tick))
                await affected_mob.adjust_drowsiness_up_to(3 * SECONDS * spt, 30 * SECONDS);
            if (SPT_PROB(2, seconds_per_tick))
            {
                await affected_mob.adjust_dizzy_up_to(5 * SECONDS * spt, 1 * MINUTES);
                await affected_mob.adjust_confusion_up_to(1 * SECONDS * spt, 10 * SECONDS);
            }
            if (SPT_PROB(2, seconds_per_tick))
            {
                await affected_mob.vomit(MOB_VOMIT_MESSAGE | MOB_VOMIT_HARM);
                await affected_mob.Stun(2 * SECONDS); // The full 20 second vomit stun would be lethal
            }
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("cough");
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your throat feels sore.")));
        }

        // "you are too late" symptoms: death.
        else if (stage == 3)
        {
            await affected_mob.apply_damage(3 * spt, TOX, spread_damage: TRUE);
            await affected_mob.apply_damage(1 * spt, OXY);
            await affected_mob.Unconscious(3 * SECONDS * spt);
        }
        return dot;
    }
}
