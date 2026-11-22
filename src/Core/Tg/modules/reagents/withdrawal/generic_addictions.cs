// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/withdrawal/generic_addictions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the seven addictions' withdrawals. The moodlets (too_bright, the withdrawal moods) are
// live since gaps MS1. The hallucinogens' angular blur and wave filters are
// added to the hud's game plane as tg does; the client draws only the game plane's blur and colour filters (not-translated.md
// section 225). The var blocks are generated (D111).

///Opioids
// tg: code/modules/reagents/withdrawal/generic_addictions.dm:3
public partial class datum_addiction_opioids : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:8
    public override async DmTask withdrawal_stage_1_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_1_process(affected_carbon, seconds_per_tick);
        if (SPT_PROB(10, seconds_per_tick))
            await affected_carbon.emote("yawn");
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:13
    public override async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_2(affected_carbon);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_high_blood_pressure));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:17
    public override async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_3_process(affected_carbon, seconds_per_tick);
        if (affected_carbon.disgust < DISGUST_LEVEL_DISGUSTED && SPT_PROB(7.5f, seconds_per_tick))
            affected_carbon.adjust_disgust(12.5f * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:22
    public override void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        base.end_withdrawal(affected_carbon);
        affected_carbon.remove_status_effect(typeof(datum_status_effect_high_blood_pressure));
        affected_carbon.set_disgust(affected_carbon.disgust * 0.5f); //half their disgust to help
    }
}

///Stimulants

// tg: code/modules/reagents/withdrawal/generic_addictions.dm:30
public partial class datum_addiction_stimulants : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:35
    public override async DmTask withdrawal_enters_stage_1(mob_living_carbon affected_carbon = null, params object[] __extra)
    {
        await base.withdrawal_enters_stage_1(affected_carbon, __extra);
        await affected_carbon.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_stimulants));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:39
    public override async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_2(affected_carbon);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_woozy));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:43
    public override async DmTask withdrawal_enters_stage_3(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_3(affected_carbon);
        await affected_carbon.add_movespeed_modifier(typeof(datum_movespeed_modifier_stimulants));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:47
    public override void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        base.end_withdrawal(affected_carbon);
        affected_carbon.remove_actionspeed_modifier(ACTIONSPEED_ID_STIMULANTS);
        affected_carbon.remove_status_effect(typeof(datum_status_effect_woozy));
        affected_carbon.remove_movespeed_modifier(MOVESPEED_ID_STIMULANTS);
    }
}

///Alcohol
// tg: code/modules/reagents/withdrawal/generic_addictions.dm:55
public partial class datum_addiction_alcohol : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:60
    public override async DmTask withdrawal_stage_1_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_1_process(affected_carbon, seconds_per_tick);
        await affected_carbon.set_jitter_if_lower(10 * SECONDS * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:64
    public override async DmTask withdrawal_stage_2_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_2_process(affected_carbon, seconds_per_tick);
        await affected_carbon.set_jitter_if_lower(20 * SECONDS * Dm.num(seconds_per_tick));
        await affected_carbon.set_hallucinations_if_lower(10 * SECONDS);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:69
    public override async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_3_process(affected_carbon, seconds_per_tick);
        await affected_carbon.set_jitter_if_lower(30 * SECONDS * Dm.num(seconds_per_tick));
        await affected_carbon.set_hallucinations_if_lower(10 * SECONDS);
        if (SPT_PROB(4, seconds_per_tick) && !HAS_TRAIT(affected_carbon, TRAIT_ANTICONVULSANT))
            await affected_carbon.apply_status_effect(typeof(datum_status_effect_seizure));
    }
}

// tg: code/modules/reagents/withdrawal/generic_addictions.dm:77
public partial class datum_addiction_hallucinogens : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:82
    public override async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_2(affected_carbon);
        atom_movable_plane_master_controller game_plane_master_controller = affected_carbon.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.add_filter("hallucinogen_blur", 10, angular_blur_filter(0, 0, 3) as DmList);
        game_plane_master_controller.add_filter("hallucinogen_wave", 10, wave_filter(300, 300, 3, 0, WAVE_SIDEWAYS) as DmList);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:89
    public override async DmTask withdrawal_enters_stage_3(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_3(affected_carbon);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_trance), 40 * SECONDS, TRUE);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:93
    public override void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        base.end_withdrawal(affected_carbon);
        atom_movable_plane_master_controller game_plane_master_controller = affected_carbon.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.remove_filter("hallucinogen_blur");
        game_plane_master_controller.remove_filter("hallucinogen_wave");
        affected_carbon.remove_status_effect(typeof(datum_status_effect_trance), 40 * SECONDS, TRUE);
    }
}

// tg: code/modules/reagents/withdrawal/generic_addictions.dm:101
public partial class datum_addiction_maintenance_drugs : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:107
    public override async DmTask withdrawal_enters_stage_1(mob_living_carbon affected_carbon = null, params object[] __extra)
    {
        await base.withdrawal_enters_stage_1(affected_carbon, __extra);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:111
    public override async DmTask withdrawal_stage_1_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_1_process(affected_carbon, seconds_per_tick);
        if (SPT_PROB(7.5f, seconds_per_tick))
            await affected_carbon.emote("growls");
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:116
    public override async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_2(affected_carbon);
        if (!ishuman(affected_carbon))
            return;
        mob_living_carbon_human affected_human = (mob_living_carbon_human)affected_carbon;
        if (affected_human.gender == MALE)
        {
            to_chat(affected_human, span_warning(I18n.T("Your chin itches.")));
            affected_human.set_facial_hairstyle("Beard (Full)", update: TRUE);
        }
        //Only like gross food
        obj_item_organ_tongue tongue = affected_carbon.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue == null)
            return;
        tongue.liked_foodtypes = GROSS;
        tongue.disliked_foodtypes = NONE;
        tongue.toxic_foodtypes = Dm.bitnot(GROSS);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:132
    public override async DmTask withdrawal_enters_stage_3(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_3(affected_carbon);
        if (!ishuman(affected_carbon))
            return;
        to_chat(affected_carbon, span_warning(I18n.T("You feel yourself adapt to the darkness.")));
        ADD_TRAIT(affected_carbon, TRAIT_NIGHT_VISION, type);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:139  (tg's override does not call its parent)
    public override async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        if (!ishuman(affected_carbon))
            return;
        mob_living_carbon_human affected_human = (mob_living_carbon_human)affected_carbon;
        turf T = get_turf(affected_human) as turf;
        if (Dm.truthy(T.check_lumcount_above(0.5f)))
        {
            await affected_human.add_mood_event("too_bright", typeof(datum_mood_event_bright_light));
            await affected_human.adjust_dizzy_up_to(6 * SECONDS, 80 * SECONDS);
            await affected_human.adjust_confusion_up_to(0.5f * SECONDS * Dm.num(seconds_per_tick), 20 * SECONDS);
        }
        else
        {
            affected_carbon.clear_mood_event("too_bright");
        }
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:151
    public override void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        base.end_withdrawal(affected_carbon);
        affected_carbon.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
        //restore tongue's tastes
        obj_item_organ_tongue tongue = affected_carbon.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue != null)
        {
            tongue.liked_foodtypes = Dm.initial(tongue, (obj_item_organ_tongue p) => p.liked_foodtypes);
            tongue.disliked_foodtypes = Dm.initial(tongue, (obj_item_organ_tongue p) => p.disliked_foodtypes);
            tongue.toxic_foodtypes = Dm.initial(tongue, (obj_item_organ_tongue p) => p.toxic_foodtypes);
        }
        REMOVE_TRAIT(affected_carbon, TRAIT_NIGHT_VISION, type);
    }
}

///Makes you a hypochondriac - I'd like to call it hypochondria, but "I could use some hypochondria" doesn't work
// tg: code/modules/reagents/withdrawal/generic_addictions.dm:164
public partial class datum_addiction_medicine : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:174
    public override async DmTask withdrawal_enters_stage_1(mob_living_carbon affected_carbon = null, params object[] __extra)
    {
        await base.withdrawal_enters_stage_1(affected_carbon, __extra);
        if (!ishuman(affected_carbon))
            return;
        datum_hallucination health_doll = await affected_carbon.cause_hallucination(
            typeof(datum_hallucination_fake_health_doll),
            "medicine addiction",
            Dm.named("severity", 1),
            Dm.named("duration", 120 * MINUTES)) as datum_hallucination;
        if (health_doll == null)
            return;
        health_doll_ref = WEAKREF(health_doll) as datum_weakref;
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:188
    public override async DmTask withdrawal_stage_1_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_1_process(affected_carbon, seconds_per_tick);
        if (SPT_PROB(1, seconds_per_tick))
            await affected_carbon.emote("cough");
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:193
    public override async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_2(affected_carbon);
        DmList possibilities = Dm.list();

        if (!HAS_TRAIT(affected_carbon, TRAIT_RESISTHEAT))
            Dm.append(ref possibilities, typeof(datum_hallucination_fake_alert_hot));
        if (!HAS_TRAIT(affected_carbon, TRAIT_RESISTCOLD))
            Dm.append(ref possibilities, typeof(datum_hallucination_fake_alert_cold));

        obj_item_organ_lungs lungs = affected_carbon.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs != null)
        {
            if (Dm.truthy(lungs.safe_oxygen_min))
                Dm.append(ref possibilities, typeof(datum_hallucination_fake_alert_need_oxygen));
            if (Dm.truthy(lungs.safe_oxygen_max))
                Dm.append(ref possibilities, typeof(datum_hallucination_fake_alert_bad_oxygen));
        }

        if (!Dm.truthy(length(possibilities)))
            return;

        datum_hallucination fake_alert = await affected_carbon.cause_hallucination(
            pick(possibilities),
            "medicine addiction",
            Dm.named("duration", 120 * MINUTES)) as datum_hallucination;
        if (fake_alert == null)
            return;
        fake_alert_ref = WEAKREF(fake_alert) as datum_weakref;
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:221
    public override async DmTask withdrawal_stage_2_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_2_process(affected_carbon, seconds_per_tick);
        if (SPT_PROB(2, seconds_per_tick))
            await affected_carbon.emote("cough");

        datum_hallucination_fake_health_doll hallucination = health_doll_ref?.resolve() as datum_hallucination_fake_health_doll;
        if (QDELETED(hallucination))
        {
            health_doll_ref = null;
            return;
        }

        if (SPT_PROB(10, seconds_per_tick))
        {
            hallucination.add_fake_limb(severity: 1);
            return;
        }

        if (SPT_PROB(5, seconds_per_tick))
        {
            hallucination.increment_fake_damage();
            return;
        }
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:239
    public override async DmTask withdrawal_enters_stage_3(mob_living_carbon affected_carbon = null)
    {
        await base.withdrawal_enters_stage_3(affected_carbon);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_crit), type);
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:243
    public override async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_3_process(affected_carbon, seconds_per_tick);
        if (SPT_PROB(5, seconds_per_tick))
            await affected_carbon.emote("cough");

        datum_hallucination_fake_health_doll hallucination = health_doll_ref?.resolve() as datum_hallucination_fake_health_doll;
        if (!QDELETED(hallucination) && SPT_PROB(5, seconds_per_tick))
        {
            hallucination.increment_fake_damage();
            return;
        }

        if (SPT_PROB(65, seconds_per_tick))
            return;

        if (IS_UNCONSCIOUS_OR_CRIT(affected_carbon))
            return;

        obj_item_organ organ = pick(affected_carbon.organs) as obj_item_organ;
        if (Dm.truthy(organ.low_threshold))
        {
            to_chat(affected_carbon, organ.low_threshold_passed);
            return;
        }

        else if (Dm.truthy(organ.high_threshold_passed))
        {
            to_chat(affected_carbon, organ.high_threshold_passed);
            return;
        }

        to_chat(affected_carbon, span_warning(I18n.T("You feel a dull pain in your [].", organ.name)));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:270
    public override void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        base.end_withdrawal(affected_carbon);
        affected_carbon.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_crit), type);
        QDEL_NULL(ref fake_alert_ref);
        QDEL_NULL(ref health_doll_ref);
    }
}

///Nicotine
// tg: code/modules/reagents/withdrawal/generic_addictions.dm:278
public partial class datum_addiction_nicotine : datum_addiction
{
    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:287
    [DmParams("affected_carbon", "seconds_per_tick")]
    public override async DmTask withdrawal_enters_stage_1(mob_living_carbon affected_carbon = null, params object[] __extra)
    {
        object seconds_per_tick = Dm.arg(__extra, 0);
        await base.withdrawal_enters_stage_1(affected_carbon, __extra);
        await affected_carbon.set_jitter_if_lower(10 * SECONDS * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:291
    public override async DmTask withdrawal_stage_2_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_2_process(affected_carbon, seconds_per_tick);
        await affected_carbon.set_jitter_if_lower(20 * SECONDS * Dm.num(seconds_per_tick));
        if (SPT_PROB(2, seconds_per_tick))
            await affected_carbon.emote("cough");
    }

    // tg: code/modules/reagents/withdrawal/generic_addictions.dm:297
    public override async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        await base.withdrawal_stage_3_process(affected_carbon, seconds_per_tick);
        await affected_carbon.set_jitter_if_lower(30 * SECONDS * Dm.num(seconds_per_tick));
        if (SPT_PROB(5, seconds_per_tick))
            await affected_carbon.emote("cough");
    }
}
