// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/tuberculosis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). The fungal TB reagent's own hook (other_reagents.dm) is
// wired by the lead with gaps RG3's rewrite of that file.

// tg: code/datums/diseases/tuberculosis.dm:1
public partial class datum_disease_tuberculosis : datum_disease
{
    // tg: code/datums/diseases/tuberculosis.dm:18
    public override async DmTask<object> stage_act(object seconds_per_tick = null) //it begins
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (SPT_PROB(stage * 2, seconds_per_tick))
        {
            await affected_mob.emote("cough");
            to_chat(affected_mob, span_danger(I18n.T("Your chest hurts.")));
        }

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your stomach violently rumbles!")));
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel a cold sweat form.")));
        }
        else if (stage == 4)
        {
            object need_mob_update = FALSE;
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_userdanger(I18n.T("You see four of everything!")));
                await affected_mob.set_dizzy_if_lower(10 * SECONDS);
            }
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a sharp pain from your lower chest!")));
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_oxy_loss(5, updating_health: FALSE));
                await affected_mob.emote("gasp");
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel air escape from your lungs painfully.")));
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_oxy_loss(25, updating_health: FALSE));
                await affected_mob.emote("gasp");
            }
            if (Dm.truthy(need_mob_update))
                await affected_mob.updatehealth();
        }
        else if (stage == 5)
        {
            object need_mob_update = FALSE;
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You feel your heart slowing...", "You relax and slow your heartbeat."))))));
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(70, updating_stamina: FALSE));
            }
            if (SPT_PROB(5, seconds_per_tick))
            {
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(100, updating_stamina: FALSE));
                affected_mob.visible_message(span_warning(I18n.T("[] faints!", affected_mob)), span_userdanger(I18n.T("You surrender yourself and feel at peace...")));
                await affected_mob.AdjustSleeping(10 * SECONDS);
            }
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_userdanger(I18n.T("You feel your mind relax and your thoughts drift!")));
                await affected_mob.adjust_confusion_up_to(8 * SECONDS, 100 * SECONDS);
            }
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 20);
            if (SPT_PROB(1.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_warning(Dm.text("<i>[]</i>", I18n.T(Dm.str(pick(Dm.list("Your stomach silently rumbles...", "Your stomach seizes up and falls limp, muscles dead and lifeless.", "You could eat a crayon")))))));
                affected_mob.overeatduration = max(affected_mob.overeatduration - (200 * SECONDS), 0);
                await affected_mob.adjust_nutrition(-100);
            }
            if (SPT_PROB(7.5f, seconds_per_tick))
            {
                if (ishuman(affected_mob))
                {
                    mob_living_carbon_human human_victim = (mob_living_carbon_human)affected_mob;
                    to_chat(human_victim, span_danger(I18n.T(Dm.str(human_victim.w_uniform != null ? pick(Dm.list("You feel uncomfortably hot...", "You feel like unzipping your jumpsuit...", "You feel like taking off some clothes...")) : "You feel uncomfortably hot..."))));
                }
                else
                    to_chat(affected_mob, span_danger(I18n.T("You feel uncomfortably hot...")));
                affected_mob.adjust_bodytemperature(40);
            }
            if (Dm.truthy(need_mob_update))
                await affected_mob.updatehealth();
        }
        return dot;
    }
}
