// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/revenant/revenant_blight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the revenant's blight, which the ectoplasm anomaly and the revenant's own spell give. The var
// block is generated (D111).

// tg: code/modules/antagonists/revenant/revenant_blight.dm:1
public partial class datum_disease_revblight : datum_disease
{
    // tg: code/modules/antagonists/revenant/revenant_blight.dm:20
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= FALSE;
        if (affected_mob != null)
        {
            await affected_mob.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, "#1d2953");
            if (affected_mob.dna != null && affected_mob.dna.species != null)
                affected_mob.set_haircolor(null, @override: TRUE);
            to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
        }
        await base.cure(add_resistance);
    }


    // tg: code/modules/antagonists/revenant/revenant_blight.dm:29
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(finalstage))
        {
            object need_mob_update = FALSE;
            if (affected_mob.body_position == LYING_DOWN && SPT_PROB(3 * stage, seconds_per_tick))
            {
                await cure();
                return FALSE;
            }
            if (SPT_PROB(1.5f * stage, seconds_per_tick))
            {
                to_chat(affected_mob, span_revennotice(I18n.T("You suddenly feel []...", I18n.T(Dm.str(pick(Dm.list("sick and tired", "disoriented", "tired and confused", "nauseated", "faint", "dizzy")))))));
                await affected_mob.adjust_confusion(8 * SECONDS);
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(20, updating_stamina: FALSE));
                Dm.@new<obj_effect_temp_visual_revenant>(affected_mob.loc);
            }
            if (stagedamage < stage)
            {
                stagedamage++;
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_tox_loss(1 * stage * Dm.num(seconds_per_tick), updating_health: FALSE)); //should, normally, do about 30 toxin damage.
                Dm.@new<obj_effect_temp_visual_revenant>(affected_mob.loc);
            }
            if (SPT_PROB(25, seconds_per_tick))
                need_mob_update = Dm.add(need_mob_update, await affected_mob.adjust_stamina_loss(stage, updating_stamina: FALSE));
            if (Dm.truthy(need_mob_update))
                await affected_mob.updatehealth();
        }

        if (stage == 2)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("pale");
        }
        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote(pick(Dm.list("pale", "shiver")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(7.5f, seconds_per_tick))
                await affected_mob.emote(pick(Dm.list("pale", "shiver", "cries")));
        }
        else if (stage == 5)
        {
            if (!Dm.truthy(finalstage))
            {
                finalstage = TRUE;
                to_chat(affected_mob, span_revenbignotice(I18n.T("You feel like [].", I18n.T(Dm.str(pick(Dm.list("nothing's worth it anymore", "nobody ever needed your help", "nothing you did mattered", "everything you tried to do was worthless")))))));
                await affected_mob.adjust_stamina_loss(22.5f * Dm.num(seconds_per_tick), updating_stamina: FALSE);
                Dm.@new<obj_effect_temp_visual_revenant>(affected_mob.loc);
                if (affected_mob.dna != null && affected_mob.dna.species != null)
                    affected_mob.set_haircolor("#1d2953", @override: TRUE);
                affected_mob.visible_message(span_warning(I18n.T("[] looks terrifyingly gaunt...", affected_mob)), span_revennotice(I18n.T("You suddenly feel like your skin is <i>wrong</i>...")));
                await affected_mob.add_atom_colour("#1d2953", TEMPORARY_COLOUR_PRIORITY);
                _addtimer(Dm.@new<datum_callback>(this, nameof(cure)), 10 * SECONDS, file: "code/modules/antagonists/revenant/revenant_blight.dm", line: 73);
            }
        }
        return dot;
    }
}
