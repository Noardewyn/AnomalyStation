// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/transformation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the transformation base and its jungle flu, robotic, xenomorph, slime, corgi, morph and gondola
// forms. The var blocks are generated (D111). Two targets are not ported yet (not-translated.md section 228): the morph mob (its
// victim becomes an inert morph, D132) and the jellypeople species the slime form sets at stage 3.

// tg: code/datums/diseases/transformation.dm:1
public partial class datum_disease_transformation : datum_disease
{
    // tg: code/datums/diseases/transformation.dm:24
    public override async DmTask<object> Copy()
    {
        datum_disease_transformation D = (datum_disease_transformation)await base.Copy();
        D.stage1 = stage1.Copy();
        D.stage2 = stage2.Copy();
        D.stage3 = stage3.Copy();
        D.stage4 = stage4.Copy();
        D.stage5 = stage5.Copy();
        // D.new_form = D.new_form: tg assigns the copy's own form to itself (a no-op), so the copy keeps its type's form
        return D;
    }


    // tg: code/datums/diseases/transformation.dm:35
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (Dm.truthy(length(stage1)) && SPT_PROB(stage_prob, seconds_per_tick))
                to_chat(affected_mob, I18n.T(Dm.str(pick(stage1))));
        }
        else if (stage == 2)
        {
            if (Dm.truthy(length(stage2)) && SPT_PROB(stage_prob, seconds_per_tick))
                to_chat(affected_mob, I18n.T(Dm.str(pick(stage2))));
        }
        else if (stage == 3)
        {
            if (Dm.truthy(length(stage3)) && SPT_PROB(stage_prob * 2, seconds_per_tick))
                to_chat(affected_mob, I18n.T(Dm.str(pick(stage3))));
        }
        else if (stage == 4)
        {
            if (Dm.truthy(length(stage4)) && SPT_PROB(stage_prob * 2, seconds_per_tick))
                to_chat(affected_mob, I18n.T(Dm.str(pick(stage4))));
        }
        else if (stage == 5)
            await do_disease_transformation(affected_mob);
        return dot;
    }


    // tg: code/datums/diseases/transformation.dm:57
    public virtual async DmTask do_disease_transformation(mob_living affected_mob = null)
    {
        if (iscarbon(affected_mob) && affected_mob.stat != DEAD)
        {
            if (Dm.truthy(length(stage5)))
                to_chat(affected_mob, I18n.T(Dm.str(pick(stage5))));
            if (QDELETED(affected_mob))
                return;
            if (HAS_TRAIT_FROM(affected_mob, TRAIT_NO_TRANSFORM, REF(this)))
                return;
            ADD_TRAIT(affected_mob, TRAIT_NO_TRANSFORM, REF(this));
            foreach (obj_item W in Dm.each<obj_item>(affected_mob.get_equipped_items(INCLUDE_POCKETS)))
                await affected_mob.dropItemToGround(W);
            foreach (obj_item I in Dm.each<obj_item>(affected_mob.held_items))
                await affected_mob.dropItemToGround(I);
            mob_living new_mob = Dm.@new(new_form, affected_mob.loc) as mob_living;
            if (new_mob is mob_living)
            {
                if (Dm.truthy(bantype) && Dm.truthy(is_banned_from(affected_mob.ckey, bantype)))
                    replace_banned_player(new_mob);
                new_mob.set_combat_mode(TRUE);
                if (affected_mob.mind != null)
                    await affected_mob.mind.transfer_to(new_mob);
                else
                    new_mob.PossessByPlayer(affected_mob.ckey);
            }
            if (transformed_antag_datum != null)
                await new_mob.mind.add_antag_datum(transformed_antag_datum);
            new_mob.name = Dm.str(affected_mob.real_name);
            new_mob.real_name = new_mob.name;
            qdel(affected_mob);
        }
    }

    // tg: code/datums/diseases/transformation.dm:85
    public virtual void replace_banned_player(mob_living new_mob = null) // This can run well after the mob has been transferred, so need a handle on the new mob to kill it if needed.
    {
        Waiting.WaitforFalse(replace_banned_player__body(new_mob));
    }

    private async DmTask replace_banned_player__body(mob_living new_mob)
    {
        mob chosen_one = await SSpolling.poll_ghosts_for_target(Dm.text("Do you want to play as []?", span_notice(Dm.str(affected_mob.real_name))), check_jobban: bantype, role: bantype, poll_time: 5 * SECONDS, checked_target: affected_mob, alert_pic: affected_mob, role_name_text: "transformation victim") as mob;
        if (chosen_one != null)
        {
            to_chat(affected_mob, span_userdanger(I18n.T("Your mob has been taken over by a ghost! Appeal your job ban if you want to avoid this in the future!")));
            message_admins(Dm.text("[] has taken control of ([]) to replace a jobbanned player.", key_name_admin(chosen_one), key_name_admin(affected_mob)));
            affected_mob.ghostize(FALSE);
            affected_mob.PossessByPlayer(chosen_one.ckey);
        }
        else
        {
            to_chat(new_mob, span_userdanger(I18n.T("Your mob has been claimed by death! Appeal your job ban if you want to avoid this in the future!")));
            new_mob.investigate_log("has been killed because there was no one to replace them as a job-banned player.", INVESTIGATE_DEATHS);
            await new_mob.death();
            if (!QDELETED(new_mob))
                new_mob.ghostize(can_reenter_corpse: FALSE);
        }
    }
}

// tg: code/datums/diseases/transformation.dm:101
public partial class datum_disease_transformation_jungle_flu : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:130
    public override async DmTask do_disease_transformation(mob_living affected_mob = null)
    {
        ((mob_living_carbon)affected_mob).monkeyize();
        await DmTask.CompletedTask;
    }

    // tg: code/datums/diseases/transformation.dm:133
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_notice(I18n.T("Your [] itches.", I18n.T(Dm.str(pick(Dm.list("arm", "back", "elbow", "head", "leg")))))));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a stabbing pain in your head.")));
                await affected_mob.adjust_confusion(10 * SECONDS);
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1.5f, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("Eeee!", "Eeek, ook ook!", "Eee-eeek!", "Ungh, ungh.")), forced: "jungle fever");
        }
        return dot;
    }
}

// tg: code/datums/diseases/transformation.dm:150
public partial class datum_disease_transformation_robot : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:172
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 3)
        {
            if (SPT_PROB(4, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("beep, beep!", "Beep, boop", "Boop...bop")), forced: "robotic transformation");
            if (SPT_PROB(2, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a stabbing pain in your head.")));
                await affected_mob.Unconscious(40);
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(10, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("beep, beep!", "Boop bop boop beep.", "I wwwaaannntt tttoo dddiiieeee...", "kkkiiiill mmme")), forced: "robotic transformation");
        }
        return dot;
    }
}

// tg: code/datums/diseases/transformation.dm:189
public partial class datum_disease_transformation_xeno : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:216
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 3)
        {
            if (SPT_PROB(2, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a stabbing pain in your head.")));
                await affected_mob.Unconscious(40);
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(10, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("Going to... devour you...", "Hsssshhhhh!", "You look delicious.")), forced: "xenomorph transformation");
        }
        return dot;
    }
}

// tg: code/datums/diseases/transformation.dm:231
public partial class datum_disease_transformation_slime : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:248
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (ishuman(affected_mob))
            {
                mob_living_carbon_human human = (mob_living_carbon_human)affected_mob;
                if (Dm.truthy(isjellyperson(human)))
                    await update_stage(5);
            }
        }
        else if (stage == 3)
        {
            if (ishuman(affected_mob))
            {
                mob_living_carbon_human human = (mob_living_carbon_human)affected_mob;
                if (!HAS_TRAIT(human, TRAIT_LESSER_HUMANOID) && !Dm.truthy(isjellyperson(human)))
                    human.set_species(typeof(datum_species_jelly_slime));
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/transformation.dm:265
    public override async DmTask do_disease_transformation(mob_living affected_mob = null)
    {
        if (affected_mob.client != null && !HAS_TRAIT(affected_mob, TRAIT_LESSER_HUMANOID)) // if they are a human who's not a monkey and are sentient, then let them have the old fun
            new_form = typeof(mob_living_basic_slime_random);
        await base.do_disease_transformation(affected_mob);
    }
}

// tg: code/datums/diseases/transformation.dm:270
public partial class datum_disease_transformation_corgi : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:286
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;
        if (stage == 3)
        {
            if (SPT_PROB(4, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("Woof!", "YAP")), forced: "corgi transformation");
        }
        else if (stage == 4)
        {
            if (SPT_PROB(10, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("AUUUUUU", "Bark!")), forced: "corgi transformation");
        }
        return dot;
    }
}

// tg: code/datums/diseases/transformation.dm:317
public partial class datum_disease_transformation_gondola : datum_disease_transformation
{
    // tg: code/datums/diseases/transformation.dm:339
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("smile");
            if (SPT_PROB(10, seconds_per_tick))
                await ((datum_reagents)affected_mob.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_pax), 5)));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("smile");
            if (SPT_PROB(10, seconds_per_tick))
                await ((datum_reagents)affected_mob.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_pax), 5)));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("smile");
            if (SPT_PROB(10, seconds_per_tick))
                await ((datum_reagents)affected_mob.reagents).add_reagent_list(Dm.list(Dm.kv(typeof(datum_reagent_pax), 5)));
            if (SPT_PROB(1, seconds_per_tick))
            {
                obj_item held_item = affected_mob.get_active_held_item() as obj_item;
                if (held_item != null)
                {
                    to_chat(affected_mob, span_danger(I18n.T("You let go of what you were holding.")));
                    await affected_mob.dropItemToGround(held_item);
                }
            }
        }
        return dot;
    }
}
