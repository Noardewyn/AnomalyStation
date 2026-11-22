// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice B, D182): the category 2 medicines of the core set (libital, probital, lenturi, aiuri, hercuri, convermol,
// tirimol, seiver, multiver, syriniver and the musiver it turns into, synthflesh, penthrite); helbital with its game of
// rock-paper-scissors with death, its necropolis curse and its omen (gaps RG2, D339; the janken achievement is a row, D332).
// Multiver's on_hydroponics_apply waits for hydroponics. The moods are live since gaps MS1. The var blocks and the penthrite alert are
// generated (D111).

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:16
public partial class datum_reagent_medicine_c2_helbital : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:29
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float death_is_coming = (Dm.num(mob.get_tox_loss()) + Dm.num(mob.get_oxy_loss()) + Dm.num(mob.get_fire_loss()) + Dm.num(mob.get_brute_loss())) * Dm.num(normalise_creation_purity());
        float thou_shall_heal = 0;
        float good_kind_of_healing = FALSE;
        float need_mob_update = FALSE;
        if (mob.stat == STABLE) //bad
        {
            thou_shall_heal = max(death_is_coming / 20, 3);
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(2 * k, TRUE, required_biotype: affected_biotype));
        }
        else if (mob.stat == SOFT_CRIT) //meh convert
        {
            thou_shall_heal = round(death_is_coming / 13, 0.1f);
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(1 * k, TRUE, required_biotype: affected_biotype));
            good_kind_of_healing = TRUE;
        }
        else //no convert
        {
            thou_shall_heal = round(death_is_coming / 10, 0.1f);
            good_kind_of_healing = TRUE;
        }
        need_mob_update += Dm.num(await mob.adjust_brute_loss(-1 * thou_shall_heal * k, FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;

        if (Dm.truthy(good_kind_of_healing) && !Dm.truthy(reaping) && SPT_PROB(0.005f, seconds_per_tick)) //janken with the grim reaper!
        {
            notify_ghosts(
                I18n.T("[] has entered a game of rock-paper-scissors with death!", mob.real_name),
                source: mob,
                header: "Who Will Win?");
            reaping = TRUE;
            if (Dm.truthy(await mob.apply_status_effect(typeof(datum_status_effect_necropolis_curse), CURSE_BLINDING)))
                helbent = TRUE;
            to_chat(mob, span_hierophant(I18n.T("Malevolent spirits appear before you, bartering your life in a 'friendly' game of rock, paper, scissors. Which do you choose?")));
            float timeisticking = world.time;
            object RPSchoice = await tgui_alert(mob, I18n.T("Janken Time! You have 60 Seconds to Choose!"), I18n.T("Rock Paper Scissors"), Dm.list("rock", "paper", "scissors"), 60);
            if (QDELETED(mob) || (timeisticking + (1.1f * MINUTES) < world.time))
            {
                reaping = FALSE;
                return dot; //good job, you ruined it
            }
            if (!Dm.truthy(RPSchoice))
            {
                to_chat(mob, span_hierophant(I18n.T("You decide to not press your luck, but the spirits remain... hopefully they'll go away soon.")));
                reaping = FALSE;
                return dot;
            }
            float sw__1 = rand(1, 3);
            if (sw__1 == 1) //You Tied!
            {
                to_chat(mob, span_hierophant(I18n.T("You tie, and the malevolent spirits disappear... for now.")));
                reaping = FALSE;
            }
            else if (sw__1 == 2) //You lost!
            {
                to_chat(mob, span_hierophant(I18n.T("You lose, and the malevolent spirits smirk eerily as they surround your body.")));
                mob.investigate_log("has lost rock paper scissors with the grim reaper and been dusted.", INVESTIGATE_DEATHS);
                await mob.dust();
                return dot;
            }
            else if (sw__1 == 3) //VICTORY ROYALE
            {
                to_chat(mob, span_hierophant(I18n.T("You win, and the malevolent spirits fade away as well as your wounds.")));
                Dm.Deferred("code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:80", "helbital on_mob_life: affected_mob.client.give_award(/datum/award/achievement/jobs/helbitaljanken, affected_mob): the achievements (D332)");
                await mob.revive(Dm.bitand(HEAL_ALL, Dm.bitnot(HEAL_REFRESH_ORGANS)));
                await holder.del_reagent(type);
                return dot;
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:85
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (!Dm.truthy(helbent))
        {
            await affected_mob.apply_necropolis_curse(Dm.bitor(CURSE_WASTING, CURSE_BLINDING));
            helbent = TRUE;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:91
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (Dm.truthy(helbent))
            affected_mob.remove_status_effect(typeof(datum_status_effect_necropolis_curse));
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:96
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (current_cycle >= 50) //greater than 10u in the system
        {
            await affected_mob.AddComponent(typeof(datum_component_omen), Dm.named("incidents_left", min(round(current_cycle / 51), 3))); //no more than 3 bad incidents for dropping more than 10u
            to_chat(affected_mob, span_hierophant_warning(I18n.T("You feel a sense of heavy dread and grave misfortune settle in as the substance leaves your body.")));
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:102
public partial class datum_reagent_medicine_c2_libital : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:113
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.3f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await mob.adjust_brute_loss(-3 * Dm.num(normalise_creation_purity()) * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:121
public partial class datum_reagent_medicine_c2_probital : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:132
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(-3 * Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
        float ooo_youaregettingsleepy = 3.5f;
        float stamina = round(Dm.num(mob.get_stamina_loss()));
        if (stamina >= 10 && stamina <= 40)
            ooo_youaregettingsleepy = 3;
        else if (stamina >= 41 && stamina <= 60)
            ooo_youaregettingsleepy = 2.5f;
        else if (stamina >= 61 && stamina <= 200) //you really can only go to 120
            ooo_youaregettingsleepy = 2;
        need_mob_update += Dm.num(await mob.adjust_stamina_loss(1 * ooo_youaregettingsleepy * k, updating_stamina: FALSE));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:148
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_stamina_loss(3 * k, updating_stamina: FALSE));
        if (Dm.num(affected_mob.get_stamina_loss()) >= 80)
            await affected_mob.adjust_drowsiness(2 * SECONDS * k);
        if (Dm.num(affected_mob.get_stamina_loss()) >= 100)
        {
            to_chat(affected_mob, span_warning(I18n.T("You feel more tired than you usually do, perhaps if you rest your eyes for a bit...")));
            need_mob_update += Dm.num(await affected_mob.adjust_stamina_loss(-100, updating_stamina: FALSE)); // Don't add the biotype parameter here as it results in infinite sleep and chat spam.
            await affected_mob.Sleeping(10 * SECONDS);
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:161
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, INGEST)) || !iscarbon(exposed_mob))
            return;

        var mob_reagents = (datum_reagents)exposed_mob.reagents;
        await mob_reagents.remove_reagent(typeof(datum_reagent_medicine_c2_probital), Dm.num(reac_volume) * 0.05f);
        await mob_reagents.add_reagent(typeof(datum_reagent_medicine_metafactor), Dm.num(reac_volume) * 0.25f);
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:172
public partial class datum_reagent_medicine_c2_lenturi : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:182
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_fire_loss(-3.75f * Dm.num(metabolization_ratio) * Dm.num(normalise_creation_purity()) * Dm.num(seconds_per_tick), required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_STOMACH, 0.4f * k, required_organ_flag: affected_organ_flags));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:190
public partial class datum_reagent_medicine_c2_aiuri : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:200
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_fire_loss(-2 * Dm.num(normalise_creation_purity()) * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_EYES, 0.25f * k, required_organ_flag: affected_organ_flags));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:208
public partial class datum_reagent_medicine_c2_hercuri : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:220
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object need_mob_update;
        if (Dm.num(mob.get_fire_loss()) > 50)
            need_mob_update = await mob.adjust_fire_loss(-3 * k * Dm.num(normalise_creation_purity()), updating_health: FALSE, required_bodytype: affected_bodytype);
        else
            need_mob_update = await mob.adjust_fire_loss(-2.25f * k * Dm.num(normalise_creation_purity()), updating_health: FALSE, required_bodytype: affected_bodytype);
        mob.adjust_bodytemperature(rand(-25, -5) * TEMPERATURE_DAMAGE_COEFFICIENT * k, 50);
        if (mob is mob_living_carbon_human humi)
            humi.adjust_coretemperature(1 * rand(-25, -5) * TEMPERATURE_DAMAGE_COEFFICIENT * k, 50);
        if (mob.reagents is datum_reagents blood)
            blood.chem_temp += (-10 * k);
        await mob.adjust_fire_stacks(-1 * k);
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:236
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= VAPOR;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, VAPOR)))
            return;

        exposed_mob.adjust_bodytemperature(-Dm.num(reac_volume) * TEMPERATURE_DAMAGE_COEFFICIENT, 50);
        await exposed_mob.adjust_fire_stacks(Dm.num(reac_volume) / -2);
        if (Dm.num(reac_volume) >= metabolization_rate)
            exposed_mob.extinguish_mob();
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:246
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        affected_mob.adjust_bodytemperature(-10 * TEMPERATURE_DAMAGE_COEFFICIENT * k, 50); //chilly chilly
        if (affected_mob is mob_living_carbon_human humi)
            humi.adjust_coretemperature(-10 * TEMPERATURE_DAMAGE_COEFFICIENT * k, 50);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:258
public partial class datum_reagent_medicine_c2_convermol : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:256  #define CONVERMOL_RATIO 5 (file-local, #undef at :287)
    private const float CONVERMOL_RATIO = 5; //# Oxygen damage to result in 1 tox

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:269
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float ratio = Dm.num(metabolization_ratio), spt = Dm.num(seconds_per_tick);
        float oxycalc = 2.5f * ratio * (current_cycle - 1);
        if (!Dm.truthy(overdosed))
            oxycalc = min(oxycalc, Dm.num(mob.get_oxy_loss()) + 0.5f); //if NOT overdosing, we lower our toxdamage to only the damage we actually healed with a minimum of 0.1*current_cycle. IE if we only heal 10 oxygen damage but we COULD have healed 20, we will only take toxdamage for the 10. We would take the toxdamage for the extra 10 if we were overdosing.
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_oxy_loss(-2 * oxycalc * Dm.num(normalise_creation_purity()) * ratio * spt, FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(2 * oxycalc * ratio * spt / CONVERMOL_RATIO, updating_health: FALSE, required_biotype: affected_biotype));
        if (SPT_PROB((current_cycle - 1) / 2, seconds_per_tick) && Dm.truthy(mob.losebreath))
        {
            mob.losebreath--;
            need_mob_update = TRUE;
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:283
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        metabolization_rate += 2.5f * REAGENTS_METABOLISM;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:289
public partial class datum_reagent_medicine_c2_tirimol : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:302
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_oxy_loss(-4.5f * k * Dm.num(normalise_creation_purity()), updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_stamina_loss(2 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        // COOLDOWN_FINISHED and COOLDOWN_START (__DEFINES/cooldowns.dm:111, :113), inline (rules 11.1 (a))
        if (Dm.truthy(drowsycd) && drowsycd <= Dm.num(world.time))
        {
            await mob.adjust_drowsiness(20 * SECONDS);
            drowsycd = Dm.num(world.time) + 45 * SECONDS;
        }
        else if (!Dm.truthy(drowsycd))
        {
            drowsycd = Dm.num(world.time) + 15 * SECONDS;
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:315
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (current_cycle > 21)
            await affected_mob.Sleeping(10 * SECONDS);
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:323
public partial class datum_reagent_medicine_c2_seiver : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:335
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        rads_heal_threshold = rand(rads_heal_threshold - 50, rads_heal_threshold + 50); // Basically this means 50K and below will always give the radiation heal, and upto 150K could. Calculated once.
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:339
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float chemtemp = min(holder.chem_temp, 1000);
        chemtemp = chemtemp != 0 ? chemtemp : T0C; //why do you have null sweaty
        float healypoints = 0; //5 healypoints = 1 heart damage; 5 rads = 1 tox damage healed for the purpose of healypoints

        //you're hot
        float toxcalc = min(1 * round(5 + ((chemtemp - 1000) / 175), 0.1f), 5) * k * Dm.num(normalise_creation_purity()); //max 2.5 tox healing per second
        float need_mob_update = 0;
        if (toxcalc > 0)
        {
            need_mob_update = Dm.num(await mob.adjust_tox_loss(-toxcalc, updating_health: FALSE, required_biotype: affected_biotype));
            healypoints += toxcalc;
        }

        //and you're cold
        float radcalc = 1 * round((T0C - chemtemp) / 6, 0.1f) * k; //max ~45 rad loss unless you've hit below 0K. if so, wow.
        if (radcalc > 0 && HAS_TRAIT(mob, TRAIT_IRRADIATED))
        {
            radcalc *= Dm.num(normalise_creation_purity());
            // extra rad healing if you are SUPER cold
            if (chemtemp < rads_heal_threshold * 0.1f)
                need_mob_update += Dm.num(await mob.adjust_tox_loss(-radcalc * 0.9f, updating_health: FALSE, required_biotype: affected_biotype));
            else if (chemtemp < rads_heal_threshold)
                need_mob_update += Dm.num(await mob.adjust_tox_loss(-radcalc * 0.75f, updating_health: FALSE, required_biotype: affected_biotype));
            healypoints += (radcalc / 5);
        }

        //you're yes and... oh no!
        healypoints = round(healypoints, 0.1f);
        await mob.adjust_organ_loss(ORGAN_SLOT_HEART, healypoints / 5, required_organ_flag: affected_organ_flags);
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:369
public partial class datum_reagent_medicine_c2_multiver : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:378
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        var blood = (datum_reagents)mob.reagents;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float medibonus = 0; //it will always have itself which makes it REALLY start @ 1
        foreach (datum_reagent the_reagent in Dm.each_cast<datum_reagent>(blood.reagent_list))
        {
            if (the_reagent is datum_reagent_medicine)
                medibonus += 1;
        }
        if (creation_purity >= 1) //Perfectly pure multivers gives a bonus of 2!
            medibonus += 1;
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_tox_loss(-0.5f * min(medibonus, 6 * Dm.num(normalise_creation_purity())) * k, updating_health: FALSE, required_biotype: affected_biotype)); //not great at healing but if you have nothing else it will work
        need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LUNGS, 0.5f * k, required_organ_flag: affected_organ_flags)); //kills at 40u
        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_anacea))))
        {
            foreach (datum_reagent second_reagent in Dm.each_cast<datum_reagent>(blood.reagent_list?.Copy()))
            {
                if (second_reagent == this)
                    continue;
                if (medibonus >= 3 && second_reagent is datum_reagent_medicine) //3 unique meds (2+multiver) | (1 + pure multiver) will make it not purge medicines
                    continue;
                await blood.remove_reagent(second_reagent.type, 3 * second_reagent.purge_multiplier * k);
            }
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:406
public partial class datum_reagent_medicine_c2_syriniver : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:404  #define issyrinormusc(A) (file-local, #undef at :493)
    internal static bool issyrinormusc(object A) => A is datum_reagent_medicine_c2_syriniver || A is datum_reagent_medicine_c2_musiver; //musc is metab of syrin so let's make sure we're not purging either

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:417
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        float trans_volume = Dm.num(reac_volume);
        if (!Dm.truthy(Dm.bitand(methods, INJECT)) || exposed_mob is not mob_living_carbon carbon)
            return;

        if (trans_volume >= 0.4f) //prevents cheesing with ultralow doses.
            await carbon.adjust_tox_loss((-1.5f * min(2, trans_volume)) * Dm.num(normalise_creation_purity()), required_biotype: affected_biotype); //This is to promote iv pole use for that chemotherapy feel.
        var L = carbon.organs_slot?[ORGAN_SLOT_LIVER] as obj_item_organ_liver;
        if (L == null || Dm.truthy(Dm.bitand(L.organ_flags, ORGAN_FAILING)))
            return;
        conversion_amount = (trans_volume * (min(100 - Dm.num(carbon.get_organ_loss(ORGAN_SLOT_LIVER)), 80) / 100) * Dm.num(normalise_creation_purity())); //the more damaged the liver the worse we metabolize.

        var mob_reagents = (datum_reagents)carbon.reagents;
        await mob_reagents.remove_reagent(typeof(datum_reagent_medicine_c2_syriniver), conversion_amount);
        await mob_reagents.add_reagent(typeof(datum_reagent_medicine_c2_musiver), conversion_amount);
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:433
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        var blood = (datum_reagents)mob.reagents;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.54f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-1.34f * k, updating_health: FALSE, required_biotype: affected_biotype));
        foreach (datum_reagent R in Dm.each_cast<datum_reagent>(blood.reagent_list))
        {
            if (issyrinormusc(R))
                continue;
            R.volume = Dm.num(R.volume) - 0.27f * k;
        }
        await blood.update_total();

        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:447
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 1 * k, required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;
        affected_mob.adjust_disgust(2 * k);
        await ((datum_reagents)affected_mob.reagents).add_reagent(typeof(datum_reagent_medicine_c2_musiver), 0.15f * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:454  (syriniver's metabolite)
public partial class datum_reagent_medicine_c2_musiver : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:465
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        var blood = (datum_reagents)mob.reagents;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 0.2f * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-3 * Dm.num(normalise_creation_purity()) * k, updating_health: FALSE, required_biotype: affected_biotype));
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(blood.reagent_list?.Copy()))
        {
            if (datum_reagent_medicine_c2_syriniver.issyrinormusc(reagent))
                continue;
            await blood.remove_reagent(reagent.type, 0.4f * reagent.purge_multiplier * k);
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:477
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        trauma = Dm.@new<datum_brain_trauma_mild_muscle_weakness>();
        await ((mob_living_carbon)affected_mob).gain_trauma(trauma, TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:482
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (trauma != null)
            QDEL_NULL(ref trauma);
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:487
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(await affected_mob.adjust_organ_loss(ORGAN_SLOT_LIVER, 3 * k, required_organ_flag: affected_organ_flags)))
            dot = UPDATE_MOB_HEALTH;
        affected_mob.adjust_disgust(6 * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:496
public partial class datum_reagent_medicine_c2_synthflesh : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:504
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (exposed_mob is not mob_living_carbon carbies)
            return;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;

        float volume_in = Dm.num(reac_volume);
        float current_bruteloss = Dm.num(carbies.get_brute_loss()); // because this will be changed after calling adjust_brute_loss()
        float current_fireloss = Dm.num(carbies.get_fire_loss()); // because this will be changed after calling adjust_fire_loss()
        float touch_protection_interference = (1 - touch_protection.Value);
        if (touch_protection_interference == 0)
            return;
        float harmies = clamp(Dm.num(await carbies.adjust_brute_loss(-1.25f * volume_in * touch_protection_interference, updating_health: FALSE, required_bodytype: affected_bodytype)), 0, current_bruteloss);
        float burnies = clamp(Dm.num(await carbies.adjust_fire_loss(-1.25f * volume_in * touch_protection_interference, updating_health: FALSE, required_bodytype: affected_bodytype)), 0, current_fireloss);
        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(carbies.all_wounds?.Copy()))
            await iter_wound.on_synthflesh(reac_volume);
        float need_mob_update = harmies + burnies;
        object tox = await carbies.adjust_tox_loss((harmies + burnies) * (0.5f + (0.25f * (1 - creation_purity))), updating_health: FALSE, required_biotype: affected_biotype); //0.5 - 0.75
        need_mob_update = Dm.truthy(tox) ? Dm.num(tox) : need_mob_update;

        if (Dm.truthy(need_mob_update))
        {
            await carbies.updatehealth();
            if (Dm.truthy(show_message))
            {
                carbies.visible_message(span_nicegreen(I18n.T("A rubbery liquid partially coats []'s burns.", carbies)));
                if (carbies.stat != DEAD)
                {
                    to_chat(carbies, span_danger(I18n.T("You feel your burns and bruises healing! It stings like hell!")));
                    await carbies.add_mood_event("painful_medicine", typeof(datum_mood_event_painful_medicine));
                }
            }
        }

        //don't unhusked non husked mobs
        if (!HAS_TRAIT_FROM(exposed_mob, TRAIT_HUSK, BURN))
            return;

        //don't try to unhusk mobs above burn damage threshold
        if (Dm.num(carbies.get_fire_loss()) > UNHUSK_DAMAGE_THRESHOLD)
        {
            if (Dm.truthy(show_message) && !Dm.truthy(need_mob_update))
                carbies.visible_message(span_minoralert(I18n.T("The liquid fails to properly stick on []. []'s burns need to be repaired first!", carbies, carbies)));
            return;
        }

        var synthflesh = ((datum_reagents)carbies.reagents).has_reagent(typeof(datum_reagent_medicine_c2_synthflesh)) as datum_reagent;
        float current_volume = synthflesh != null ? Dm.num(synthflesh.volume) : 0;
        float current_purity = synthflesh != null ? synthflesh.purity : 0;

        if (Dm.truthy(Dm.bitand(methods, TOUCH))) //touch does not apply chems to blood, we want to combine the two volumes before attempting to unhusk
        {
            current_purity = current_volume > 0 ? (current_volume * current_purity + volume_in * creation_purity) / (current_volume + volume_in) : creation_purity;
            current_volume += volume_in;
        }

        //when purity = 100%, 60u to unhusk, when purity = 60%, 100u to unhusk.
        if (current_volume >= SYNTHFLESH_UNHUSK_MAX || current_volume * current_purity >= SYNTHFLESH_UNHUSK_AMOUNT)
        {
            await carbies.cure_husk(BURN);
            await ((datum_reagents)carbies.reagents).remove_reagent(typeof(datum_reagent_medicine_c2_synthflesh), current_volume); // consume the synthflesh, it won't do anything in their blood
            //we're avoiding using the phrases "burnt flesh" and "burnt skin" here because carbies could be a skeleton or a golem or something
            carbies.visible_message(span_nicegreen(I18n.T("A rubbery liquid coats []'s burns. [] looks a lot healthier!", carbies, carbies)));
        }
        else if (Dm.truthy(show_message) && !Dm.truthy(need_mob_update))
        {
            // if they are laying in a pool of synthflesh, we don't want it sending a message every tick
            if (Dm.truthy(Dm.bitand(methods, TOUCH)))
            {
                if (Dm.truthy(TIMER_COOLDOWN_RUNNING(carbies, REF(carbies))))
                    return;
                // TIMER_COOLDOWN_START(carbies, REF(carbies), 16 SECONDS) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
                LAZYSET(ref carbies.cooldowns, REF(carbies), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), carbies, REF(carbies)), 16 * SECONDS, file: "code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm", line: 562));
                carbies.visible_message(span_boldnotice(I18n.T("The liquid fails to properly stick on []. There isn't enough to unhusk!", carbies)));
            }
            else
            {
                carbies.visible_message(span_nicegreen(I18n.T("A rubbery liquid partially mends []... It seems more is required to fully unhusk!", carbies)));
            }
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:579
public partial class datum_reagent_medicine_c2_penthrite : datum_reagent_medicine_c2
{
    /// List of traits to add/remove from our subject when we are in their system
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:590  var/static/list/subject_traits = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList subject_traits => ref TgState.Current.datum_reagent_medicine_c2_penthrite__subject_traits;

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:604
    public override async DmTask on_mob_metabolize(mob_living user = null, params object[] __extra)
    {
        await base.on_mob_metabolize(user, __extra);
        send_alert(user);
        user.add_traits(subject_traits, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:609
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float lowest = mob.crit_threshold + HEALTH_THRESHOLD_FULLCRIT * (2 * Dm.num(normalise_creation_purity()));
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_STOMACH, 0.25f * k, required_organ_flag: affected_organ_flags));
        if (mob.health <= HEALTH_THRESHOLD_CRIT && mob.health > lowest) //we cannot save someone below our lowered crit threshold.
        {
            need_mob_update += Dm.num(await mob.adjust_tox_loss(-2 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(-2 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-2 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(-6 * k, updating_health: FALSE, required_biotype: affected_biotype));

            mob.losebreath = 0;

            need_mob_update += Dm.num(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, 1 * max(Dm.num(volume) / 10, 1) * k, required_organ_flag: affected_organ_flags)); // your heart is barely keeping up!

            await mob.set_jitter_if_lower(1 * rand(0 * SECONDS, 4 * SECONDS) * k);
            await mob.set_dizzy_if_lower(1 * rand(0 * SECONDS, 4 * SECONDS) * k);

            if (SPT_PROB(18, seconds_per_tick))
                to_chat(mob, span_danger(I18n.T("Your body is trying to give up, but your heart is still beating!")));
        }

        if (mob.health <= (mob.crit_threshold + HEALTH_THRESHOLD_FULLCRIT * (2 * Dm.num(normalise_creation_purity())))) //certain death below this threshold
        {
            REMOVE_TRAIT(mob, TRAIT_STABLEHEART, type); //we have to remove the stable heart trait before we give them a heart attack
            mob.remove_traits(subject_traits, type);
            to_chat(mob, span_danger(I18n.T("You feel something rupturing inside your chest!")));
            if (!HAS_TRAIT(mob, TRAIT_ANALGESIA))
                await mob.emote("scream");
            mob.set_heartattack(TRUE);
            volume = 0;
        }
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:641
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        remove_alert(affected_mob);
        affected_mob.remove_traits(subject_traits, type);
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:646
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        REMOVE_TRAIT(affected_mob, TRAIT_STABLEHEART, type);
        float need_mob_update;
        need_mob_update = Dm.num(await affected_mob.adjust_stamina_loss(10 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await affected_mob.adjust_organ_loss(ORGAN_SLOT_HEART, 10 * k, required_organ_flag: affected_organ_flags));
        need_mob_update += Dm.num(((mob_living_carbon)affected_mob).set_heartattack(TRUE));
        return Dm.truthy(need_mob_update) ? UPDATE_MOB_HEALTH : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:656
    public virtual void send_alert(mob_living affected_mob = null)
    {
        affected_mob.throw_alert("penthrite", typeof(atom_movable_screen_alert_penthrite));
    }

    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:659
    public virtual void remove_alert(mob_living affected_mob = null)
    {
        affected_mob.clear_alert("penthrite");
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:590-595  /datum/reagent/medicine/c2/penthrite/var/static/list/subject_traits
    internal DmList datum_reagent_medicine_c2_penthrite__subject_traits = Dm.list(
        TRAIT_STABLEHEART,
        TRAIT_NOHARDCRIT,
        TRAIT_NOSOFTCRIT,
        TRAIT_NOCRITDAMAGE);
}

// Gaps P2 (D298): the reagents' effects in a hydroponics tray (on_hydroponics_apply).

// tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:401
public partial class datum_reagent_medicine_c2_multiver : datum_reagent_medicine_c2
{
    // tg: code/modules/reagents/chemistry/reagents/cat2_medicine_reagents.dm:401
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_toxic(-(Dm.round(Dm.num(volume) * 2) * Dm.num(normalise_creation_purity()))); //0-2.66, 2 by default (0.75 purity).
    }
}
