// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/drug_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: M8 slice B (D182) the drug base's end of metabolism and Pump-Up; gaps P2 nicotine in a tray; gaps RG3 (D339) the
// rest (the space drugs' high, cannabis, nicotine, krokodil, meth, bath salts, aranesp, happiness, the maintenance drugs, the
// mushroom hallucinogen's and bLaSToFF's and Saturn-X's game plane filters, kronkaine and gore, syndol). The moods are MS1's
// (live since gaps train 11), the addictions are RG2's (the drugs' addiction_types are data), the kronkaine adrenal crisis
// came with the diseases (VI1, wired in gaps train 12). The var blocks are generated (D111).

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:1
public partial class datum_reagent_drug : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:7
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (Dm.truthy(trippy))
            affected_mob.clear_mood_event(Dm.text("[]_high", type));
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:12
public partial class datum_reagent_drug_space_drugs : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:22
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.set_drugginess(30 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (isturf(mob.loc) && !isspaceturf(mob.loc) && !HAS_TRAIT(mob, TRAIT_IMMOBILIZED) && SPT_PROB(5, seconds_per_tick))
            await step(mob, Dm.num(pick(GLOB.cardinals)));
        if (SPT_PROB(3.5f, seconds_per_tick))
            await mob.emote(pick("twitch", "drool", "moan", "giggle"));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:30
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_userdanger(I18n.T("You start tripping hard!")));
        await affected_mob.add_mood_event(Dm.text("[]_overdose", type), typeof(datum_mood_event_overdose), name);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:35
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float hallucination_duration_in_seconds = Dm.num(affected_mob.get_timed_status_effect_duration(typeof(datum_status_effect_hallucination))) / 10;
        if (hallucination_duration_in_seconds < Dm.num(volume) && SPT_PROB(10, seconds_per_tick))
            await affected_mob.adjust_hallucinations(10 * SECONDS);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:41
public partial class datum_reagent_drug_cannabis : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:53
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.apply_status_effect(typeof(datum_status_effect_stoned));
        if (SPT_PROB(1, seconds_per_tick))
        {
            object smoke_message = pick(I18n.T("You feel relaxed."), I18n.T("You feel calmed."), I18n.T("Your mouth feels dry."), I18n.T("You could use some water."), I18n.T("Your heart beats quickly."), I18n.T("You feel clumsy."), I18n.T("You crave junk food."), I18n.T("You notice you've been moving more slowly."));
            to_chat(mob, span_notice(Dm.text("[]", smoke_message)));
        }
        if (SPT_PROB(2, seconds_per_tick))
            await mob.emote(pick("smile", "laugh", "giggle"));
        await mob.adjust_nutrition(-0.6f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)); //munchies
        if (SPT_PROB(4, seconds_per_tick) && mob.body_position == LYING_DOWN && !Dm.truthy(mob.IsSleeping())) //chance to fall asleep if lying down
        {
            to_chat(mob, span_warning(I18n.T("You doze off...")));
            await mob.Sleeping(10 * SECONDS);
        }
        if (SPT_PROB(4, seconds_per_tick) && mob.buckled != null && mob.body_position != LYING_DOWN && !Dm.truthy(mob.IsParalyzed())) //chance to be couchlocked if sitting
        {
            to_chat(mob, span_warning(I18n.T("It's too comfy to move...")));
            await mob.Paralyze(10 * SECONDS);
        }

        datum_ai_controller controller = mob.ai_controller as datum_ai_controller;
        DmList enemies = controller?.blackboard?[BB_MONKEY_ENEMIES] as DmList;
        foreach (object enemy in Dm.each(enemies))
        {
            controller.add_blackboard_key_assoc(BB_MONKEY_ENEMIES, enemy, MONKEY_CALMED_HATRED_AMOUNT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            if (Dm.truthy(controller.blackboard[BB_MONKEY_AGGRESSIVE]) && SPT_PROB(10 - Dm.num(Dm.values_sum(enemies)), seconds_per_tick))
            {
                controller.set_blackboard_key(BB_MONKEY_AGGRESSIVE, FALSE);
                cleared_aggressive = TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:76
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (Dm.truthy(cleared_aggressive))
            (affected_mob.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_MONKEY_AGGRESSIVE, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:81
public partial class datum_reagent_drug_nicotine : datum_reagent_drug
{
    //Nicotine is used as a pesticide IRL.
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:95  (gaps P2, D298)
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(Dm.round(Dm.num(volume)));
        mytray.adjust_pestlevel(-rand(1, 2));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:99
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(0.5f, seconds_per_tick))
        {
            object smoke_message = pick(I18n.T("You feel relaxed."), I18n.T("You feel calmed."), I18n.T("You feel alert."), I18n.T("You feel rugged."));
            to_chat(mob, span_notice(Dm.text("[]", smoke_message)));
        }
        await mob.add_mood_event("smoked", typeof(datum_mood_event_smoked));
        mob.remove_status_effect(typeof(datum_status_effect_jitter));
        await mob.AdjustAllImmobility(-20 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

        datum_ai_controller controller = mob.ai_controller as datum_ai_controller;
        foreach (object enemy in Dm.each(controller?.blackboard?[BB_MONKEY_ENEMIES]))
            controller.add_blackboard_key_assoc(BB_MONKEY_ENEMIES, enemy, MONKEY_CALMED_HATRED_AMOUNT * 0.1f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:113
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_tox_loss(0.4f * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_oxy_loss(4.4f * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:121
public partial class datum_reagent_drug_krokodil : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:133
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        object high_message = pick(I18n.T("You feel calm."), I18n.T("You feel collected."), I18n.T("You feel like you need to relax."));
        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", high_message)));
        await mob.add_mood_event("smacked out", typeof(datum_mood_event_narcotic_heavy));

        if (current_cycle != 36 || creation_purity > 0.6f)
            return dot;

        if (HAS_TRAIT(mob, TRAIT_HUSK))
            return dot;

        to_chat(mob, span_userdanger(I18n.T("Your skin falls off easily!")));
        var affected_human = mob as mob_living_carbon_human;
        affected_human.set_facial_hairstyle("Shaved", update: FALSE);
        affected_human.set_hairstyle("Bald", update: FALSE);
        mob.become_husk(Dm.initial(typeof(datum_status_effect_zombie), (datum_status_effect_zombie z) => z.id));

        if (Dm.truthy(await mob.adjust_brute_loss(25, updating_health: FALSE, required_bodytype: affected_bodytype))) // holy shit your skin just FELL THE FUCK OFF
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:155
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object need_mob_update = await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.25f * k, required_organ_flag: affected_organ_flags);
        need_mob_update = Dm.bitor(need_mob_update, await affected_mob.adjust_tox_loss(0.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:162
public partial class datum_reagent_drug_methamphetamine : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:175
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        //the more pure, the less non-blue colors get involved - best case scenario is rgb(135, 200, 250) AKA #78C8FA
        //worst case scenario is rgb(250, 250, 250) AKA #FAFAFA
        //minimum purity of meth is 50%, therefore we base values on that
        float effective_impurity = min(1, (1 - creation_purity) / 0.5f);
        //yes i know that purity doesn't actually affect how meth works at all but this is so funny
        color = Dm.str(BlendRGB(Dm.initial(this, (datum_reagent_drug_methamphetamine r) => r.color), "#FAFAFA", effective_impurity));
    }

    //we need to update the color whenever purity gets changed
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:185
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        float effective_impurity = min(1, (1 - creation_purity) / 0.5f);
        color = Dm.str(BlendRGB(Dm.initial(this, (datum_reagent_drug_methamphetamine r) => r.color), "#FAFAFA", effective_impurity));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:190
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:194
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:198
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object high_message = pick(I18n.T("You feel hyper."), I18n.T("You feel like you need to go faster."), I18n.T("You feel like you can run the world."), I18n.T("You understand now."));
        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", high_message)));
        await mob.add_mood_event("tweaking", typeof(datum_mood_event_stimulant_medium));
        await mob.AdjustAllImmobility(-26.67f * k);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_stamina_loss(-3.34f * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        await mob.set_jitter_if_lower(2.67f * SECONDS * k);
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.67f * rand(1, 4) * k, required_organ_flag: affected_organ_flags));
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        if (SPT_PROB(2.5f, seconds_per_tick))
            await mob.emote(pick("twitch", "shiver"));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:214
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (!HAS_TRAIT(affected_mob, TRAIT_IMMOBILIZED) && !ismovable(affected_mob.loc))
        {
            for (float i = 1; i <= round(2.67f * k, 1); i++)
                await step(affected_mob, Dm.num(pick(GLOB.cardinals)));
        }
        if (SPT_PROB(10, seconds_per_tick))
            await affected_mob.emote("laugh");
        if (SPT_PROB(18, seconds_per_tick))
        {
            affected_mob.visible_message(span_danger(I18n.T("[]'s hands flip out and flail everywhere!", affected_mob)));
            await affected_mob.drop_all_held_items();
        }
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_tox_loss(0.67f * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.67f * (rand(5, 10) / 10f) * k, required_organ_flag: affected_organ_flags));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:230
public partial class datum_reagent_drug_bath_salts : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:245
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (iscarbon(affected_mob))
        {
            var carbon_mob = (mob_living_carbon)affected_mob;
            rage = Dm.@new<datum_brain_trauma_special_psychotic_brawling_bath_salts>();
            await carbon_mob.gain_trauma(rage, TRAUMA_RESILIENCE_ABSOLUTE);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:252
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (rage != null)
            QDEL_NULL(ref rage);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:257
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object high_message = pick(I18n.T("You feel amped up."), I18n.T("You feel ready."), I18n.T("You feel like you can push it to the limit."));
        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", high_message)));
        await mob.add_mood_event("salted", typeof(datum_mood_event_stimulant_heavy));
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_stamina_loss(-6 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 4 * k, required_organ_flag: affected_organ_flags));
        await mob.adjust_hallucinations(10 * SECONDS * k);
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        if (!HAS_TRAIT(mob, TRAIT_IMMOBILIZED) && !ismovable(mob.loc))
        {
            await step(mob, Dm.num(pick(GLOB.cardinals)));
            await step(mob, Dm.num(pick(GLOB.cardinals)));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:273
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await affected_mob.adjust_hallucinations(10 * SECONDS * k);
        if (!HAS_TRAIT(affected_mob, TRAIT_IMMOBILIZED) && !ismovable(affected_mob.loc))
        {
            for (float i = 1; i <= round(8 * k, 1); i++)
                await step(affected_mob, Dm.num(pick(GLOB.cardinals)));
        }
        if (SPT_PROB(10, seconds_per_tick))
            await affected_mob.emote(pick("twitch", "drool", "moan"));
        if (SPT_PROB(28, seconds_per_tick))
            await affected_mob.drop_all_held_items();
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:284
public partial class datum_reagent_drug_aranesp : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:295
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object high_message = pick(I18n.T("You feel amped up."), I18n.T("You feel ready."), I18n.T("You feel like you can push it to the limit."));
        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", high_message)));
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_stamina_loss(-18 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(0.5f * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (SPT_PROB(30, seconds_per_tick))
        {
            mob.losebreath++;
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(1, FALSE, required_biotype: affected_biotype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:309
public partial class datum_reagent_drug_happiness : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:322
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_mood_event("happiness_drug", typeof(datum_mood_event_happiness_drug));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:326
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        affected_mob.clear_mood_event("happiness_drug");
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:330
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        mob.remove_status_effect(typeof(datum_status_effect_jitter));
        mob.remove_status_effect(typeof(datum_status_effect_confusion));
        mob.disgust = 0;
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.2f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:338
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(16, seconds_per_tick))
        {
            float reaction = rand(1, 3);
            if (reaction == 1)
            {
                await affected_mob.emote("laugh");
                await affected_mob.add_mood_event("happiness_drug", typeof(datum_mood_event_happiness_drug_good_od));
            }
            else if (reaction == 2)
            {
                await affected_mob.emote("sway");
                await affected_mob.set_dizzy_if_lower(50 * SECONDS);
            }
            else if (reaction == 3)
            {
                await affected_mob.emote("frown");
                await affected_mob.add_mood_event("happiness_drug", typeof(datum_mood_event_happiness_drug_bad_od));
            }
        }
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:355
public partial class datum_reagent_drug_pumpup : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:366
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        var liver = affected_mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM))
        {
            await affected_mob.add_mood_event("maintenance_fun", typeof(datum_mood_event_maintenance_high));
            metabolization_rate *= 0.8f;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:373
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.set_jitter_if_lower(2.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", pick(I18n.T("Go! Go! GO!"), I18n.T("You feel ready..."), I18n.T("You feel invincible...")))));
        if (SPT_PROB(7.5f, seconds_per_tick))
        {
            mob.losebreath++;
            await mob.adjust_tox_loss(2, updating_health: FALSE, required_biotype: affected_biotype);
            return UPDATE_MOB_HEALTH;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:384
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_userdanger(I18n.T("You can't stop shaking, your heart beats faster and faster...")));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:388
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(2.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        float need_mob_update = 0;
        if (SPT_PROB(2.5f, seconds_per_tick))
            await affected_mob.drop_all_held_items();
        if (SPT_PROB(7.5f, seconds_per_tick))
        {
            await affected_mob.emote(pick("twitch", "drool"));
        }
        if (SPT_PROB(10, seconds_per_tick))
        {
            affected_mob.losebreath++;
            await affected_mob.adjust_stamina_loss(4, updating_stamina: FALSE, required_biotype: affected_biotype);
            need_mob_update = TRUE;
        }
        if (SPT_PROB(7.5f, seconds_per_tick))
            need_mob_update += Dm.num(await affected_mob.adjust_tox_loss(2, updating_health: FALSE, required_biotype: affected_biotype));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:405
public partial class datum_reagent_drug_maint : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:409
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (!iscarbon(affected_mob))
            return;

        var carbon_mob = (mob_living_carbon)affected_mob;
        var liver = carbon_mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (HAS_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM))
        {
            await carbon_mob.add_mood_event("maintenance_fun", typeof(datum_mood_event_maintenance_high));
            metabolization_rate *= 0.8f;
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:420
public partial class datum_reagent_drug_maint_powder : datum_reagent_drug_maint
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:430
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.1f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags);
        // 5x if you want to OD, you can potentially go higher, but good luck managing the brain damage.
        float amt = max(round(Dm.num(volume) / 3, 0.1f), 1);
        datum_mind mind = mob?.mind as datum_mind;
        if (mind != null)
        {
            Dm.combine(ref mind.experience_multiplier_reasons, type);
            mind.experience_multiplier_reasons[type] = 1 * amt * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:438
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        datum_mind mind = affected_mob?.mind as datum_mind;
        if (mind != null)
        {
            mind.experience_multiplier_reasons[type] = null;
            Dm.remove(ref mind.experience_multiplier_reasons, type);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:443
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 6 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:448
public partial class datum_reagent_drug_maint_sludge : datum_reagent_drug_maint
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:459
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_tox_loss(0.125f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:464
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (!iscarbon(affected_mob))
            return dot;
        var carbie = (mob_living_carbon)affected_mob;
        //You will be vomiting so the damage is really for a few ticks before you flush it out of your system
        float need_mob_update;
        need_mob_update = Dm.num(await carbie.adjust_tox_loss(0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
        if (SPT_PROB(5, seconds_per_tick))
        {
            need_mob_update += Dm.num(await carbie.adjust_tox_loss(2.5f * Dm.num(metabolization_ratio), required_biotype: affected_biotype, updating_health: FALSE));
            await carbie.vomit(VOMIT_CATEGORY_DEFAULT);
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:478
public partial class datum_reagent_drug_maint_tar : datum_reagent_drug_maint
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:487
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.AdjustAllImmobility(-10 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 1.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags);
        return UPDATE_MOB_HEALTH;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:493
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float need_update;
        need_update = Dm.num(await affected_mob.adjust_tox_loss(5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
        need_update += Dm.num(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 3 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags));
        if (Dm.truthy(need_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:501
public partial class datum_reagent_drug_mushroomhallucinogen : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:513
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var psychonaut = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await psychonaut.set_slurring_if_lower(2.5f * SECONDS * k);

        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 2 && current_cycle <= 6)
        {
            if (SPT_PROB(5, seconds_per_tick))
                await psychonaut.emote(pick("twitch", "giggle"));
        }
        else if (current_cycle >= 6 && current_cycle <= 11)
        {
            await psychonaut.set_jitter_if_lower(50 * SECONDS * k);
            if (SPT_PROB(10, seconds_per_tick))
                await psychonaut.emote(pick("twitch", "giggle"));
        }
        else if (current_cycle >= 11)
        {
            await psychonaut.set_jitter_if_lower(100 * SECONDS * k);
            if (SPT_PROB(16, seconds_per_tick))
                await psychonaut.emote(pick("twitch", "giggle"));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:530
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        RegisterSignal(affected_mob, COMSIG_MOB_LOGIN, nameof(trip));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:534
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        UnregisterSignal(affected_mob, COMSIG_MOB_LOGIN);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:538
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living psychonaut = affected_mob;
        await psychonaut.add_mood_event("tripping", typeof(datum_mood_event_high));
        if (psychonaut.client == null)
            return; // No need to mess with a mob's plane masters if they physically can't see it.
        trip(psychonaut);
    }

    /// Proc that handles the changes
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:546
    [SignalHandler]
    public virtual void trip(mob_living psychonaut = null)
    {
        if (psychonaut.hud_used == null)
            return;

        var game_plane_master_controller = psychonaut.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

        // Info for non-matrix plebs like me!

        // This doesn't change the RGB matrixes directly at all. Instead, it shifts all the colors' Hue by 33%,
        // Shifting them up the color wheel, turning R to G, G to B, B to R, making a psychedelic effect.
        // The second moves them two colors up instead, turning R to B, G to R, B to G.
        // The third does a full spin, or resets it back to normal.
        // Imagine a triangle on the color wheel with the points located at the color peaks, rotating by 90 degrees each time.
        // The value with decimals is the Hue. The rest are Saturation, Luminosity, and Alpha, though they're unused here.

        // The filters were initially named _green, _blue, _red, despite every filter changing all the colors. It caused me a 2-years-long headache.

        DmList col_filter_identity = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.000f, 0, 0, 0);
        DmList col_filter_shift_once = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.333f, 0, 0, 0);
        DmList col_filter_shift_twice = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.666f, 0, 0, 0);
        DmList col_filter_reset = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1.000f, 0, 0, 0); //visually this is identical to the identity

        game_plane_master_controller.add_filter("rainbow", 10, color_matrix_filter(col_filter_reset, FILTER_COLOR_HSL) as DmList);

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("rainbow")))
        {
            animate(filter, Dm.named("color", col_filter_identity), Dm.named("time", 0 * SECONDS), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("color", col_filter_shift_once), Dm.named("time", 4 * SECONDS));
            animate(Dm.named("color", col_filter_shift_twice), Dm.named("time", 4 * SECONDS));
            animate(Dm.named("color", col_filter_reset), Dm.named("time", 4 * SECONDS));
        }

        game_plane_master_controller.add_filter("psilocybin_wave", 1, Dm.list(Dm.kv("type", "wave"), Dm.kv("size", 2), Dm.kv("x", 32), Dm.kv("y", 32)));

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("psilocybin_wave")))
            animate(filter, Dm.named("time", 64 * SECONDS), Dm.named("loop", -1), Dm.named("easing", LINEAR_EASING), Dm.named("offset", 32), Dm.named("flags", ANIMATION_PARALLEL));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:581
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living psychonaut = affected_mob;
        psychonaut.clear_mood_event("tripping");
        if (psychonaut.hud_used == null)
            return;
        var game_plane_master_controller = psychonaut.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.remove_filter("rainbow");
        game_plane_master_controller.remove_filter("psilocybin_wave");
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:590
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living psychonaut = affected_mob;
        if (SPT_PROB(10, seconds_per_tick))
            await psychonaut.emote(pick("twitch", "drool", "moan"));

        if (SPT_PROB(10, seconds_per_tick))
            await psychonaut.apply_status_effect(typeof(datum_status_effect_tower_of_babel));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:598
public partial class datum_reagent_drug_blastoff : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:616
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living dancer = affected_mob;

        await dancer.add_mood_event("vibing", typeof(datum_mood_event_high));
        RegisterSignal(dancer, COMSIG_MOB_EMOTED("flip"), nameof(on_flip));
        RegisterSignal(dancer, COMSIG_MOB_EMOTED("spin"), nameof(on_spin));

        if (dancer.hud_used == null)
            return;

        var game_plane_master_controller = dancer.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

        DmList col_filter_shift_twice = Dm.list(0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.764f, 0, 0, 0); //most blue color
        DmList col_filter_mid = Dm.list(0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.832f, 0, 0, 0); //red/blue mix midpoint
        DmList col_filter_reset = Dm.list(0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0.900f, 0, 0, 0); //most red color

        game_plane_master_controller.add_filter("blastoff_filter", 10, color_matrix_filter(col_filter_mid, FILTER_COLOR_HCY) as DmList);
        game_plane_master_controller.add_filter("blastoff_wave", 1, Dm.list(Dm.kv("type", "wave"), Dm.kv("x", 32), Dm.kv("y", 32)));

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("blastoff_filter")))
        {
            animate(filter, Dm.named("color", col_filter_shift_twice), Dm.named("time", 3 * SECONDS), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("color", col_filter_mid), Dm.named("time", 3 * SECONDS));
            animate(Dm.named("color", col_filter_reset), Dm.named("time", 3 * SECONDS));
            animate(Dm.named("color", col_filter_mid), Dm.named("time", 3 * SECONDS));
        }

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("blastoff_wave")))
            animate(filter, Dm.named("time", 32 * SECONDS), Dm.named("loop", -1), Dm.named("easing", LINEAR_EASING), Dm.named("offset", 32), Dm.named("flags", ANIMATION_PARALLEL));

        dancer.sound_environment_override = SOUND_ENVIRONMENT_PSYCHOTIC;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:647
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living dancer = affected_mob;

        dancer.clear_mood_event("vibing");
        UnregisterSignal(dancer, COMSIG_MOB_EMOTED("flip"));
        UnregisterSignal(dancer, COMSIG_MOB_EMOTED("spin"));

        if (dancer.hud_used == null)
            return;

        var game_plane_master_controller = dancer.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

        game_plane_master_controller.remove_filter("blastoff_filter");
        game_plane_master_controller.remove_filter("blastoff_wave");
        dancer.sound_environment_override = NONE;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:663
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var dancer = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await dancer.adjust_organ_loss(ORGAN_SLOT_LUNGS, 0.3f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;
        await dancer.AdjustKnockdown(-20);

        if (SPT_PROB(BLASTOFF_DANCE_MOVE_CHANCE_PER_UNIT * Dm.num(volume), seconds_per_tick))
            await dancer.emote("flip");
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:672
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living dancer = affected_mob;
        if (Dm.truthy(await dancer.adjust_organ_loss(ORGAN_SLOT_LUNGS, 0.3f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;

        if (SPT_PROB(BLASTOFF_DANCE_MOVE_CHANCE_PER_UNIT * Dm.num(volume), seconds_per_tick))
            await dancer.emote("spin");
        return dot;
    }

    ///This proc listens to the flip signal and throws the mob every third flip
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:681
    [SignalHandler]
    public virtual void on_flip(datum source = null, params object[] __extra)
    {
        if (!iscarbon(holder.my_atom))
            return;
        var dancer = (mob_living_carbon)holder.my_atom;

        flip_count++;
        if (flip_count < BLASTOFF_DANCE_MOVES_PER_SUPER_MOVE)
            return;
        flip_count = 0;
        atom throw_target = get_edge_target_turf(dancer, dancer.dir) as atom; //Do a super flip
        dancer.SpinAnimation(speed: 3, loops: 3);
        dancer.visible_message(span_notice(I18n.T("[] does an extravagant flip!", dancer)), span_nicegreen(I18n.T("You do an extravagant flip!")));
        Waiting.NoSleep(dancer.throw_at(throw_target, range: 6, speed: Dm.truthy(overdosed) ? 4 : 1));
    }

    ///This proc listens to the spin signal and throws the mob every third spin
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:698
    [SignalHandler]
    public virtual void on_spin(datum source = null, params object[] __extra)
    {
        if (!iscarbon(holder.my_atom))
            return;
        var dancer = (mob_living_carbon)holder.my_atom;

        spin_count++;
        if (spin_count < BLASTOFF_DANCE_MOVES_PER_SUPER_MOVE)
            return;
        spin_count = 0; //Do a super spin.
        dancer.visible_message(span_danger(I18n.T("[] spins around violently!", dancer)), span_danger(I18n.T("You spin around violently!")));
        dancer.spin(30, 2);
        if (dancer.disgust < 40)
            dancer.adjust_disgust(10);
        if (dancer.pulledby == null)
            return;
        turf dancer_turf = get_turf(dancer);
        atom_movable dance_partner = dancer.pulledby;
        dance_partner.visible_message(span_danger(I18n.T("[] tries to hold onto [], but is thrown back!", dance_partner, dancer)), span_danger(I18n.T("You try to hold onto [], but you are thrown back!", dancer)), null, COMBAT_MESSAGE_RANGE);
        atom throwtarget = get_edge_target_turf(dancer_turf, get_dir(dancer_turf, get_step_away(dance_partner, dancer_turf))) as atom;
        if (Dm.truthy(overdosed))
            Waiting.NoSleep(dance_partner.throw_at(target: throwtarget, range: 7, speed: 4));
        else
            Waiting.NoSleep(dance_partner.throw_at(target: throwtarget, range: 4, speed: 1)); //superspeed
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:724
public partial class datum_reagent_drug_saturnx : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:735
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var invisible_man = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await invisible_man.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.3f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:740
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living invisible_man = affected_mob;
        playsound(invisible_man, Dm.resource("sound/effects/chemistry/saturnx_fade.ogg"), 40);
        to_chat(invisible_man, span_nicegreen(I18n.T("You feel pins and needles all over your skin as your body suddenly becomes transparent!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(turn_man_invisible), invisible_man), 1 * SECONDS, file: "code/modules/reagents/chemistry/reagents/drug_reagents.dm", line: 742); //just a quick delay to synch up the sound.
        if (invisible_man.hud_used == null)
            return;

        var game_plane_master_controller = invisible_man.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

        DmList col_filter_full = Dm.list(1, 0, 0, 0, 0, 1.00f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        DmList col_filter_twothird = Dm.list(1, 0, 0, 0, 0, 0.68f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        DmList col_filter_half = Dm.list(1, 0, 0, 0, 0, 0.42f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        DmList col_filter_empty = Dm.list(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);

        game_plane_master_controller.add_filter("saturnx_filter", 10, color_matrix_filter(col_filter_twothird, FILTER_COLOR_HCY) as DmList);
        game_plane_master_controller.add_filter("saturnx_blur", 1, Dm.list(Dm.kv("type", "radial_blur"), Dm.kv("size", 0)));

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("saturnx_filter")))
        {
            animate(filter, Dm.named("loop", -1), Dm.named("color", col_filter_full), Dm.named("time", 4 * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
            //uneven so we spend slightly less time with bright colors
            animate(Dm.named("color", col_filter_twothird), Dm.named("time", 6 * SECONDS), Dm.named("easing", LINEAR_EASING));
            animate(Dm.named("color", col_filter_half), Dm.named("time", 3 * SECONDS), Dm.named("easing", LINEAR_EASING));
            animate(Dm.named("color", col_filter_empty), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
            animate(Dm.named("color", col_filter_half), Dm.named("time", 24 * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
            animate(Dm.named("color", col_filter_twothird), Dm.named("time", 12 * SECONDS), Dm.named("easing", LINEAR_EASING));
        }

        foreach (object filter in Dm.each(game_plane_master_controller.get_filters("saturnx_blur")))
        {
            animate(filter, Dm.named("loop", -1), Dm.named("size", 0.02f), Dm.named("time", 2 * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("size", 0), Dm.named("time", 6 * SECONDS), Dm.named("easing", SINE_EASING));
        }
    }

    ///This proc turns the living mob passed as the arg "invisible_man"s invisible by giving him the invisible man trait and updating his body, this changes the sprite of all his organic limbs to a 1 alpha version.
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:772
    public virtual void turn_man_invisible(mob_living_carbon invisible_man = null, float? requires_liver = null)
    {
        requires_liver ??= TRUE;
        if (Dm.truthy(requires_liver))
        {
            if (invisible_man.get_organ_slot(ORGAN_SLOT_LIVER) == null)
                return;
            if (Dm.truthy(invisible_man.undergoing_liver_failure()))
                return;
            if (HAS_TRAIT(invisible_man, TRAIT_LIVERLESS_METABOLISM))
                return;
        }
        if (Dm.truthy(invisible_man.has_status_effect(typeof(datum_status_effect_grouped_stasis))))
            return;

        invisible_man.add_traits(Dm.list(TRAIT_INVISIBLE_MAN, TRAIT_HIDE_EXTERNAL_ORGANS, TRAIT_NO_BLOOD_OVERLAY), type);

        invisible_man.remove_from_all_data_huds();
        // invisible_man.sound_environment_override = SOUND_ENVIROMENT_PHASED: DEVIATION(DV552): tg's custom reverb is a list of 23
        // EAX numbers (sound.dm:103); the port's sound_environment_override and the client's reverb take tg's numbered presets only.
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:788
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living invisible_man = affected_mob;
        if (HAS_TRAIT_FROM(invisible_man, TRAIT_INVISIBLE_MAN, type))
        {
            invisible_man.add_to_all_human_data_huds(); //Is this safe, what do you think, Floyd?
            invisible_man.remove_traits(Dm.list(TRAIT_INVISIBLE_MAN, TRAIT_HIDE_EXTERNAL_ORGANS, TRAIT_NO_BLOOD_OVERLAY), type);

            to_chat(invisible_man, span_notice(I18n.T("As you sober up, opacity once again returns to your body meats.")));
        }

        invisible_man.sound_environment_override = NONE;

        if (invisible_man.hud_used == null)
            return;

        var game_plane_master_controller = invisible_man.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.remove_filter("saturnx_filter");
        game_plane_master_controller.remove_filter("saturnx_blur");
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:805
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living invisible_man = affected_mob;
        if (SPT_PROB(7.5f, seconds_per_tick))
            await invisible_man.emote("giggle");
        if (SPT_PROB(5, seconds_per_tick))
            await invisible_man.emote("laugh");
        if (Dm.truthy(await invisible_man.adjust_organ_loss(ORGAN_SLOT_LIVER, METABOLIZE_FREE_CONSTANT(0.2f) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:830
public partial class datum_reagent_drug_kronkaine : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:843
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        // Kronkaine also makes for a great fishing bait (found in "natural" baits)
        if (!istype(holder?.my_atom, typeof(obj_item_food)))
            return;
        ADD_TRAIT(holder.my_atom, TRAIT_GREAT_QUALITY_BAIT, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:850
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(holder?.my_atom, TRAIT_GREAT_QUALITY_BAIT, type);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:854
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living kronkaine_fiend = affected_mob;
        await kronkaine_fiend.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_kronkaine));
        kronkaine_fiend.sound_environment_override = SOUND_ENVIRONMENT_HANGAR;
        Dm.output_to(kronkaine_fiend, sound(Dm.resource("sound/effects/health/fastbeat.ogg"), repeat: TRUE, channel: CHANNEL_HEARTBEAT, volume: 30)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:860
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living kronkaine_fiend = affected_mob;
        kronkaine_fiend.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_kronkaine));
        kronkaine_fiend.sound_environment_override = NONE;
        //Stop the rapid heartneats, we make sure we are not in crit as to not mess with the heartbeats from organ/heart.
        if (!IS_UNCONSCIOUS_OR_CRIT(kronkaine_fiend))
            kronkaine_fiend.stop_sound_channel(CHANNEL_HEARTBEAT);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:868
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        var druggo = (mob_living_carbon)exposed_mob;
        object trans_volume = reac_volume;

        //The drug is more effective if smoked or injected, restoring more stamina per unit.
        float stamina_heal_per_unit;
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(INJECT, INHALE))))
        {
            stamina_heal_per_unit = 12;
            if (Dm.num(trans_volume) >= 3)
            {
                Dm.output_to(druggo, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"))); //The efffect is often refered to as the "kronkaine bells". (SEND_SOUND)
                to_chat(druggo, span_danger(I18n.T("Your ears ring as your blood pressure suddenly spikes!")));
                to_chat(druggo, span_nicegreen(I18n.T("You feel an amazing rush!")));
            }
            else if (prob(15))
                to_chat(druggo, span_nicegreen(Dm.str(pick(I18n.T("You feel the cowardice melt away..."), I18n.T("You feel unbothered by the judgements of others."), I18n.T("My life feels lovely!"), I18n.T("You lower your snout... and suddenly feel more charitable!")))));
        }
        else
            stamina_heal_per_unit = 6;
        await druggo.adjust_stamina_loss(-stamina_heal_per_unit * Dm.num(trans_volume));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:887
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var kronkaine_fiend = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object need_mob_update = null;
        await kronkaine_fiend.add_mood_event("tweaking", typeof(datum_mood_event_stimulant_medium));
        if (Dm.truthy(await kronkaine_fiend.adjust_organ_loss(ORGAN_SLOT_HEART, 0.67f * (0.1f + 0.04f * Dm.num(volume)) * k, required_organ_flag: affected_organ_flags)))
        {
            need_mob_update = UPDATE_MOB_HEALTH;
            if (Dm.num(kronkaine_fiend.get_organ_loss(ORGAN_SLOT_HEART)) >= 75 && prob(15))
            {
                to_chat(kronkaine_fiend, span_userdanger(I18n.T("You feel like your heart is about to explode!")));
                playsound(kronkaine_fiend, Dm.resource("sound/effects/singlebeat.ogg"), 200, TRUE);
            }
        }
        await kronkaine_fiend.set_jitter_if_lower(13.34f * SECONDS * k);
        await kronkaine_fiend.AdjustSleeping(-1.34f * SECONDS * k);
        await kronkaine_fiend.adjust_drowsiness(-6.67f * SECONDS * k);
        /* Do not try to cheese the overdose threshhold with purging chems to become stamina immune, if you purge and take stamina damage you will be punished!

        The reason why I choose to add the adrenal crisis anti-cheese mechanic is because the main combat benefit is so front loaded, you could easily negate all the risk and downsides by mixing it with a small amount of a purger like haloperidol.
        I think that level of safety goes against the design we would like achieve with drugs; great rewards but at the cost of great risk.*/
        if (Dm.num(kronkaine_fiend.get_stamina_loss()) > 30)
        {
            foreach (object possible_purger in Dm.each(((datum_reagents)kronkaine_fiend.reagents).reagent_list))
            {
                if (istype(possible_purger, typeof(datum_reagent_medicine_c2_multiver)) || istype(possible_purger, typeof(datum_reagent_medicine_haloperidol)))
                {
                    // tg :906 hands HasDisease(datum/disease/D) a typepath, whose D.IsSame(DD) then runtimes for a host with any
                    // disease (a path has no procs); the port asks what it means: is an adrenal crisis caught already (gaps train 12)
                    bool in_crisis = false;
                    foreach (datum_disease caught in Dm.each_cast<datum_disease>(kronkaine_fiend.diseases))
                        if (istype(caught, typeof(datum_disease_adrenal_crisis)))
                            in_crisis = true;
                    if (in_crisis)
                        break;
                    kronkaine_fiend.visible_message(span_bolddanger(I18n.T("[] suddenly tenses up, it looks like the shock is causing their body to shut down!", kronkaine_fiend.name)), span_userdanger(I18n.T("The sudden shock in combination with the cocktail of drugs and purgatives in your body makes your adrenal system go haywire. Uh oh!")));
                    await kronkaine_fiend.ForceContractDisease(Dm.@new<datum_disease_adrenal_crisis>(), FALSE, TRUE); //We punish players for purging, since unchecked purging would allow players to reap the stamina healing benefits without any drawbacks. This also has the benefit of making haloperidol a counter, like it is supposed to be.
                    break;
                }
            }
        }
        need_mob_update = await kronkaine_fiend.adjust_stamina_loss(-0.54f * Dm.num(volume) * k, updating_stamina: FALSE, required_biotype: affected_biotype);
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:915
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living kronkaine_fiend = affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await kronkaine_fiend.adjust_organ_loss(ORGAN_SLOT_HEART, 0.34f * k, required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;
        await kronkaine_fiend.set_jitter_if_lower(13.34f * SECONDS * k);
        if (SPT_PROB(10, seconds_per_tick))
            to_chat(kronkaine_fiend, span_danger(Dm.str(pick(I18n.T("Your heart is racing!"), I18n.T("Your ears are ringing!"), I18n.T("You sweat like a pig!"), I18n.T("You clench your jaw and grind your teeth."), I18n.T("You feel prickles of pain in your chest.")))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:923
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        Dm.output_to(affected_mob, sound(Dm.resource("sound/effects/health/fastbeat.ogg"), repeat: TRUE, channel: CHANNEL_HEARTBEAT, volume: 90)); // SEND_SOUND
    }
}

///dirty kronkaine, aka gore. far worse overdose effects.
// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:928
public partial class datum_reagent_drug_kronkaine_gore : datum_reagent_drug_kronkaine
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:935
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_ECHOLOCATOR))
            to_chat(affected_mob, span_nicegreen(I18n.T("OH YEAH! THAT'S THE STUFF! THAT'S GORE!")));
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:940
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var gored = (mob_living)affected_mob;
        if (HAS_TRAIT(gored, TRAIT_ECHOLOCATOR))
        {
            await gored.adjust_brute_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            await gored.adjust_fire_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:946
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        mob_living gored = affected_mob;
        gored.visible_message(
            span_danger(I18n.T("[] explodes in a shower of gore!", gored)),
            span_userdanger(I18n.T("GORE! GORE! GORE! YOU'RE GORE! TOO MUCH GORE! YOU'RE GORE! GORE! IT'S OVER! GORE! GORE! YOU'RE GORE! TOO MUCH G-")));
        Dm.@new<obj_structure_bouncy_castle>(gored.loc, gored);
        await gored.gib();
    }
}

// tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:955
public partial class datum_reagent_drug_syndol : datum_reagent_drug
{
    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:970
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        var liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ;
        if (liver == null || !Dm.truthy(Dm.bitand(liver.organ_flags, affected_organ_flags)))
            return dot;
        // non-trivial but not immediately dangerous liver damage
        await liver.apply_organ_damage(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        // anti-hallucinogens can counteract the effects
        if (HAS_TRAIT(mob, TRAIT_HALLUCINATION_IMMUNE) || Dm.truthy(((datum_reagents)mob.reagents).has_reagent(typeof(datum_reagent_medicine_haloperidol), amount: 3, needs_metabolizing: TRUE)))
        {
            QDEL_NULL(ref active_hallucination_weakref);
            return dot;
        }

        // and the main event, funny hallucinations
        if (active_hallucination_weakref?.resolve() != null)
            return dot;
        Type greatest_fear = null;
        if (HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
            greatest_fear = typeof(datum_hallucination_delusion_preset_syndies);
        else if (HAS_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM) || HAS_TRAIT(liver, TRAIT_COMEDY_METABOLISM))
            greatest_fear = typeof(datum_hallucination_delusion_preset_seccies);

        if (greatest_fear != null)
        {
            // 5 minutes = 15 units, roughly. we cancel the hallucination early when we exit the mob, anyway
            active_hallucination_weakref = WEAKREF(await mob.cause_hallucination(greatest_fear, name, Dm.named("duration", 5 * MINUTES), Dm.named("skip_nearby", !Dm.truthy(overdosed))) as datum) as datum_weakref;
        }
        else
        {
            // if they're just some random schmuck, give them random hallucinations
            await mob.adjust_hallucinations_up_to(4 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 30 * SECONDS);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:998
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.adjust_hallucinations(-16 * SECONDS);
        QDEL_NULL(ref active_hallucination_weakref);
    }

    // tg: code/modules/reagents/chemistry/reagents/drug_reagents.dm:1003
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        // no message, just refresh the hallucination
        QDEL_NULL(ref active_hallucination_weakref);
    }
}
