// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/chronic_illness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/chronic_illness.dm:1
public partial class datum_disease_chronic_illness : datum_disease
{
    // tg: code/datums/diseases/chronic_illness.dm:22
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
            carrier = FALSE; // Go fuck yourself
        else if (stage == 2)
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(I18n.T("You feel dizzy.")));
                await affected_mob.adjust_confusion(6 * SECONDS);
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You look at your hand. Your vision blurs.")));
                await affected_mob.set_eye_blur_if_lower(10 * SECONDS);
            }
        }
        else if (stage == 3)
        {
            object need_mob_update = FALSE;
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a very sharp pain in your chest!")));
                if (prob(45))
                    await affected_mob.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 20);
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You feel your heart slowing...", "You relax and slow your heartbeat."))))));
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(70, updating_stamina: FALSE));
            }
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a buzzing in your brain.")));
                Dm.output_to(affected_mob, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_brute_loss(1, updating_health: FALSE));
            if (Dm.truthy(need_mob_update))
                await affected_mob.updatehealth();
        }
        else if (stage == 4)
        {
            object need_mob_update = FALSE;
            if (prob(30))
                affected_mob.playsound_local(affected_mob, Dm.resource("sound/effects/singlebeat.ogg"), 100, FALSE, use_reverb: FALSE);
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a gruesome pain in your chest!")));
                if (prob(75))
                    await affected_mob.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 45);
            }
            if (SPT_PROB(1, seconds_per_tick))
            {
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(100, updating_stamina: FALSE));
                affected_mob.visible_message(span_warning(I18n.T("[] collapses!", affected_mob)));
                if (prob(30))
                {
                    to_chat(affected_mob, span_danger(I18n.T("Your vision blurs as you faint!")));
                    await affected_mob.AdjustSleeping(1 * SECONDS);
                }
            }
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T(Dm.str(pick(Dm.list("You feel as though your atoms are accelerating in place.", "You feel like you're being torn apart!"))))));
                await affected_mob.emote("scream");
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_brute_loss(10, updating_health: FALSE));
            }
            if (Dm.truthy(need_mob_update))
                await affected_mob.updatehealth();
        }
        else if (stage == 5)
        {
            float sw__1 = rand(1, 2);
            if (sw__1 == 1)
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel your atoms begin to realign. You're safe. For now.")));
                await update_stage(1);
            }
            else if (sw__1 == 2)
            {
                to_chat(affected_mob, span_boldwarning(I18n.T("There is no place for you in this timeline.")));
                await affected_mob.adjust_stamina_loss(100, forced: TRUE);
                playsound(affected_mob.loc as atom, Dm.resource("sound/effects/magic/repulse.ogg"), 100, FALSE);
                await affected_mob.emote("scream");
                foreach (mob_living viewers in Dm.each<mob_living>(Builtins.viewers(3, affected_mob.loc)))
                    await viewers.flash_act();
                Dm.@new<obj_effect_decal_cleanable_plasma>(affected_mob.loc);
                Dm.@new<obj_effect_decal_cleanable_ash>(affected_mob.loc);
                affected_mob.visible_message(span_warning(I18n.T("[] is erased from the timeline!", affected_mob)), span_userdanger(I18n.T("You are ripped from the timeline!")));
                affected_mob.investigate_log(Dm.text("has been dusted / deleted by [].", name), INVESTIGATE_DEATHS);
                affected_mob.ghostize(can_reenter_corpse: FALSE);
                qdel(affected_mob);
            }
        }
        return dot;
    }
}
