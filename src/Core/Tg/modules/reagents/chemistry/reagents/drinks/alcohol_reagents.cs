// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG1 (D333, D339): the whole file. The ethanol base (the drunkenness it adds by its boozepwr, the drinker traits, water's
// dilution, the liver's damage; the ink it washes off paper and books; the fire stacks and the surgery speed of a splash) and every
// alcohol's own effects. The beer's on_hydroponics_apply is gaps P2's (D298). The addiction points (/datum/addiction/alcohol) are
// RG2's; the moodlets are live since gaps MS1.

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:4
public partial class datum_reagent_consumable_ethanol : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1  #define ALCOHOL_THRESHOLD_MODIFIER 1 (file-local, #undef at :3628)
    //Greater numbers mean that less alcohol has greater intoxication potential
    internal const float ALCOHOL_THRESHOLD_MODIFIER = 1;
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2  #define ALCOHOL_EXPONENT 1.6 (file-local, #undef at :3627)
    //The exponent applied to boozepwr to make higher volume alcohol at least a little bit damaging to the liver
    internal const float ALCOHOL_EXPONENT = 1.6f;

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:43
    [DmParams("data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList data = Dm.arg(__extra, 0) as DmList;
        if (Dm.truthy(LAZYLEN(data)))
        {
            if (data["quality"] != null)
            {
                quality = Dm.num_or_null(data["quality"]);
                name = "Natural " + name;
            }
            if (Dm.truthy(data["boozepwr"]))
                boozepwr = Dm.num(data["boozepwr"]);
        }
        if (boozepwr > 0)
        {
            // the stronger the drink, the less total of the drink is needed to reach addiction
            LAZYSET(ref addiction_types, typeof(datum_addiction_alcohol), max(50, round(150 - boozepwr, 5)));
        }
        await base.New(__extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:55
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.num(drinker.get_drunk_amount()) < Dm.num(volume) * boozepwr * ALCOHOL_THRESHOLD_MODIFIER || boozepwr < 0)
        {
            float booze_power = boozepwr;
            if (HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE)) // we're an accomplished drinker
                booze_power *= 0.7f;
            if (HAS_TRAIT(drinker, TRAIT_LIGHT_DRINKER))
                booze_power *= 2;

            // water will dilute alcohol effects
            float total_water_volume = 0;
            float total_alcohol_volume = 0;
            foreach (datum_reagent_water sobriety in Dm.each<datum_reagent_water>(((datum_reagents)drinker.reagents).reagent_list))
                total_water_volume += Dm.num(sobriety.volume);

            foreach (datum_reagent_consumable_ethanol alcohol in Dm.each<datum_reagent_consumable_ethanol>(((datum_reagents)drinker.reagents).reagent_list))
                total_alcohol_volume += Dm.num(alcohol.volume);

            float combined_dilute_volume = total_alcohol_volume + total_water_volume;
            if (Dm.truthy(combined_dilute_volume)) // safety check to prevent division by zero
                booze_power *= (total_alcohol_volume / combined_dilute_volume);

            datum_ai_controller controller = drinker.ai_controller as datum_ai_controller;
            foreach (object enemy in Dm.each(controller?.blackboard[BB_MONKEY_ENEMIES]))
                controller.add_blackboard_key_assoc(BB_MONKEY_ENEMIES, enemy, MONKEY_ANGERED_HATRED_AMOUNT * (boozepwr / 100) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));

            // Volume, power, and server alcohol rate effect how quickly one gets drunk
            await drinker.adjust_drunk_effect(1 * sqrt(Dm.num(volume)) * booze_power * ALCOHOL_RATE * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            if (boozepwr > 0)
            {
                obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
                float heavy_drinker_multiplier = (HAS_TRAIT(drinker, TRAIT_HEAVY_DRINKER) ? 0.5f : 1);
                if (liver != null)
                {
                    if (Dm.truthy(await liver.apply_organ_damage(((max(sqrt(Dm.num(volume)) * Dm.pow(boozepwr, ALCOHOL_EXPONENT) * liver.alcohol_tolerance * heavy_drinker_multiplier * Dm.num(seconds_per_tick), 0)) / 150))))
                        return UPDATE_MOB_HEALTH;
                }
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:89
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        if (istype(exposed_obj, typeof(obj_item_paper)))
        {
            obj_item_paper paperaffected = (obj_item_paper)exposed_obj;
            paperaffected.clear_paper();
            to_chat(usr, span_notice(I18n.T("[]'s ink washes away.", paperaffected)));
        }
        if (istype(exposed_obj, typeof(obj_item_book)))
        {
            if (Dm.num(reac_volume) >= 5)
            {
                obj_item_book affectedbook = (obj_item_book)exposed_obj;
                affectedbook.book_data.set_content("");
                exposed_obj.visible_message(span_notice(I18n.T("[]'s writing is washed away by []!", exposed_obj, name)));
            }
            else
            {
                exposed_obj.visible_message(span_warning(I18n.T("[]'s ink is smeared by [], but doesn't wash away!", exposed_obj, name)));
            }
        }
        return await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
    }

    //Splashing people with ethanol isn't quite as good as fuel.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:103
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, INGEST)))
            await exposed_mob.check_allergic_reaction(ALCOHOL, chance: Dm.num(reac_volume) * 5, histamine_add: min(10, Dm.num(reac_volume)));

        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
        {
            await exposed_mob.adjust_fire_stacks(Dm.num(reac_volume) / 15);
            exposed_mob.add_surgery_speed_mod("alcohol", round(1 - (boozepwr / 650), 0.05f), min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES)); // Weak alcohol has less sterilizing power
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:113
public partial class datum_reagent_consumable_ethanol_beer : datum_reagent_consumable_ethanol
{
    // Beer is a chemical composition of alcohol and various other things. It's a garbage nutrient but hey, it's still one. Also alcohol is bad, mmmkay?
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:125
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 0.05f));
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume) * 0.7f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:148
public partial class datum_reagent_consumable_ethanol_beer_green : datum_reagent_consumable_ethanol_beer
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:157
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (!Dm.eq(drinker.color, color))
            await drinker.add_atom_colour(color, TEMPORARY_COLOUR_PRIORITY);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:162
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, color);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:166
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        metabolization_rate = REAGENTS_METABOLISM;

        if (!ishuman(affected_mob))
            return dot;

        mob_living_carbon_human affected_human = (mob_living_carbon_human)affected_mob;
        if (HAS_TRAIT(affected_human, TRAIT_USES_SKINTONES))
            affected_human.skin_tone = "green";
        else if (HAS_TRAIT(affected_human, TRAIT_MUTANT_COLORS) && !HAS_TRAIT(affected_human, TRAIT_FIXED_MUTANT_COLORS)) //Code stolen from spraytan overdose
            affected_human.dna.features[FEATURE_MUTANT_COLOR] = "#a8e61d";
        await affected_human.update_body(TRUE); // update_body(is_creating = TRUE)
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:181
public partial class datum_reagent_consumable_ethanol_kahlua : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:190
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_dizzy_if_lower(10 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.adjust_drowsiness(-6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.AdjustSleeping(-4 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (!HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.set_jitter_if_lower(10 * SECONDS);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:218
public partial class datum_reagent_consumable_ethanol_whiskey_candycorn : datum_reagent_consumable_ethanol_whiskey
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:225
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(5, seconds_per_tick))
            await drinker.adjust_hallucinations(4 * SECONDS * Dm.num(metabolization_ratio));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:231
public partial class datum_reagent_consumable_ethanol_thirteenloko : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:243
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.adjust_drowsiness(-14 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.AdjustSleeping(-4 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        drinker.adjust_bodytemperature(-5 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(drinker.get_body_temp_normal()));
        if (!HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.set_jitter_if_lower(10 * SECONDS);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:251
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        to_chat(drinker, span_userdanger(I18n.T("Your entire body violently jitters as you start to feel queasy. You really shouldn't have drank all of that []!", name)));
        await drinker.set_jitter_if_lower(40 * SECONDS);
        await drinker.Stun(1.5f * SECONDS);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:257
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living drinker = affected_mob;
        if (SPT_PROB(3.5f, seconds_per_tick) && iscarbon(drinker))
        {
            obj_item held_item = drinker.get_active_held_item() as obj_item;
            if (held_item != null)
            {
                await drinker.dropItemToGround(held_item);
                to_chat(drinker, span_notice(I18n.T("Your hands jitter and you drop what you were holding!")));
                await drinker.set_jitter_if_lower(20 * SECONDS);
            }
        }

        if (SPT_PROB(3.5f, seconds_per_tick))
            to_chat(drinker, span_notice(Dm.text("[]", pick(I18n.T("You have a really bad headache."), I18n.T("Your eyes hurt."), I18n.T("You find it hard to stay still."), I18n.T("You feel your heart practically beating out of your chest.")))));

        if (SPT_PROB(2.5f, seconds_per_tick) && iscarbon(drinker))
        {
            mob_living_carbon carbon_drinker = (mob_living_carbon)drinker;
            obj_item_organ_eyes eyes = carbon_drinker.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (eyes != null && Dm.truthy(IS_ORGANIC_ORGAN(eyes))) // doesn't affect robotic eyes
            {
                if (Dm.truthy(drinker.is_blind()))
                {
                    await eyes.Remove(carbon_drinker);
                    await eyes.forceMove(get_turf(drinker));
                    to_chat(drinker, span_userdanger(I18n.T("You double over in pain as you feel your eyeballs liquify in your head!")));
                    await drinker.emote("scream");
                    if (Dm.truthy(await drinker.adjust_brute_loss(15 * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_bodytype)))
                        dot = UPDATE_MOB_HEALTH;
                }
                else
                {
                    to_chat(drinker, span_userdanger(I18n.T("You scream in terror as you go blind!")));
                    if (Dm.truthy(await eyes.apply_organ_damage(eyes.maxHealth)))
                        dot = UPDATE_MOB_HEALTH;
                    await drinker.emote("scream");
                }
            }
        }

        if (SPT_PROB(1.5f, seconds_per_tick) && iscarbon(drinker))
        {
            drinker.visible_message(span_danger(I18n.T("[] starts having a seizure!", drinker)), span_userdanger(I18n.T("You have a seizure!")));
            if (Dm.truthy(await drinker.Unconscious(10 * SECONDS)))
                dot = UPDATE_MOB_HEALTH;
            await drinker.set_jitter_if_lower(700 * SECONDS);
        }

        if (SPT_PROB(0.5f, seconds_per_tick) && iscarbon(drinker))
        {
            await drinker.apply_status_effect(typeof(datum_status_effect_heart_attack));
            to_chat(drinker, span_userdanger(I18n.T("You're pretty sure you just felt your heart stop for a second there..")));
            drinker.playsound_local(drinker, Dm.resource("sound/effects/singlebeat.ogg"), 100, 0);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:308
public partial class datum_reagent_consumable_ethanol_bilk : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:317
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.truthy(drinker.get_brute_loss()) && SPT_PROB(5, seconds_per_tick))
        {
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: 1 * Dm.num(metabolization_ratio), updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:324
public partial class datum_reagent_consumable_ethanol_threemileisland : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:334
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_drugginess(100 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:361
public partial class datum_reagent_consumable_ethanol_rum_aged : datum_reagent_consumable_ethanol_rum
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:370
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        drinker.add_blocked_language(Dm.sub(subtypesof(typeof(datum_language)), typeof(datum_language_piratespeak)), source: LANGUAGE_DRINK);
        drinker.grant_language(typeof(datum_language_piratespeak), source: LANGUAGE_DRINK);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:375
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        mob_living drinker = affected_mob;
        if (!QDELING(drinker))
        {
            drinker.remove_blocked_language(subtypesof(typeof(datum_language)), source: LANGUAGE_DRINK);
            drinker.remove_language(typeof(datum_language_piratespeak), source: LANGUAGE_DRINK);
        }
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:403
public partial class datum_reagent_consumable_ethanol_wine : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:414
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        if (Dm.truthy(data) && Dm.truthy(mix_data) && !Dm.eq(((DmList)data)["vintage"], mix_data["vintage"]))
            ((DmList)data)["vintage"] = "mixed wine";
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:419
    public override object get_taste_description(mob_living taster = null)
    {
        if (HAS_TRAIT(taster, TRAIT_WINE_TASTER))
        {
            if (Dm.truthy(data) && Dm.truthy(((DmList)data)["vintage"]))
                return Dm.list(Dm.kv(Dm.text("[]", ((DmList)data)["vintage"]), 1));
            else
                return Dm.list(Dm.kv("synthetic wine", 1));
        }
        return base.get_taste_description(taster);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:472
public partial class datum_reagent_consumable_ethanol_absinthe : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:480
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(5, seconds_per_tick) && !HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.adjust_hallucinations(8 * SECONDS * Dm.num(metabolization_ratio));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:507
public partial class datum_reagent_consumable_ethanol_goldschlager : datum_reagent_consumable_ethanol
{
    // tg defines expose_mob twice at this type (:519 and :531): the earlier is expose_mob__d0, which the later's ..() calls
    // (rules 2.5).
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:519
    public async DmTask expose_mob__d0(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        // Reset quality each time, since the bottle can be shared

        if (ishuman(exposed_mob))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)exposed_mob;
            // tgstation13 does not endorse underage drinking. laws may vary by your jurisdiction.
            if (Dm.num(human.age) >= 13 && Dm.num(human.age) <= 19 && human.gender == FEMALE)
                quality = teenage_girl_quality;
        }

        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:531
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await expose_mob__d0(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, INGEST)))
            return;

        float convert_amount = Dm.num(reac_volume) * min(GOLDSCHLAGER_GOLD_RATIO, 1);
        datum_reagents mob_reagents = exposed_mob.reagents as datum_reagents;

        await mob_reagents.remove_reagent(typeof(datum_reagent_consumable_ethanol_goldschlager), convert_amount);
        await mob_reagents.add_reagent(typeof(datum_reagent_gold), convert_amount);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:578
public partial class datum_reagent_consumable_ethanol_cuba_libre : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:587
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon cubano = (mob_living_carbon)affected_mob;
        float need_mob_update = 0;
        if (cubano.mind != null && Dm.truthy(cubano.mind.has_antag_datum(typeof(datum_antagonist_rev)))) //Cuba Libre, the traditional drink of revolutions! Heals revolutionaries.
        {
            need_mob_update = Dm.num(await cubano.adjust_brute_loss(-1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await cubano.adjust_fire_loss(-1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await cubano.adjust_tox_loss(-1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await cubano.adjust_oxy_loss(-5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:641
public partial class datum_reagent_consumable_ethanol_screwdrivercocktail : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:650
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        // We want to turn only base drinking glasses with screwdriver(cocktail) into screwdrivers(tool),
        // but we can't check style so we have to check type, and we don't want it match subtypes like istype does
        if (holder?.my_atom != null && holder.my_atom.type == typeof(obj_item_reagent_containers_cup_glass_drinkingglass))
        {
            RegisterSignal(holder, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
            if (ReferenceEquals(this, holder.get_master_reagent()))
            {
                obj_item_reagent_containers_cup_glass_drinkingglass drink = (obj_item_reagent_containers_cup_glass_drinkingglass)holder.my_atom;
                drink.tool_behaviour = TOOL_SCREWDRIVER;
                drink.usesound = Dm.list(Dm.resource("sound/items/tools/screwdriver.ogg"), Dm.resource("sound/items/tools/screwdriver2.ogg"));
            }
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:661
    public override object Destroy(float? force = null)
    {
        // holder?.: a screwdriver destroyed without a holder (none is in the port's paths; tg's holder deletes its reagents) reads
        // no glass where tg would runtime before its ..()
        obj_item_reagent_containers_cup_glass_drinkingglass drink = holder?.my_atom as obj_item_reagent_containers_cup_glass_drinkingglass;
        if (drink != null)
        {
            if (Dm.eq(drink.tool_behaviour, TOOL_SCREWDRIVER))
            {
                drink.tool_behaviour = Dm.initial(drink, (obj_item p) => p.tool_behaviour);
                drink.usesound = Dm.initial(drink, (obj_item p) => p.usesound);
            }
            UnregisterSignal(holder, COMSIG_REAGENTS_HOLDER_UPDATED);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:670
    [SignalHandler]
    public virtual void on_reagent_change(datum_reagents reagents = null)
    {
        obj_item_reagent_containers_cup_glass_drinkingglass drink = reagents.my_atom as obj_item_reagent_containers_cup_glass_drinkingglass;
        if (ReferenceEquals(reagents.get_master_reagent(), this))
        {
            drink.tool_behaviour = TOOL_SCREWDRIVER;
            drink.usesound = Dm.list(Dm.resource("sound/items/tools/screwdriver.ogg"), Dm.resource("sound/items/tools/screwdriver2.ogg"));
        }
        else
        {
            drink.tool_behaviour = Dm.initial(drink, (obj_item p) => p.tool_behaviour);
            drink.usesound = Dm.initial(drink, (obj_item p) => p.usesound);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:680
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (HAS_TRAIT(liver, TRAIT_ENGINEER_METABOLISM))
        {
            ADD_TRAIT(drinker, TRAIT_HALT_RADIATION_EFFECTS, Dm.text("[]", type));
            if (HAS_TRAIT(drinker, TRAIT_IRRADIATED))
            {
                if (Dm.truthy(await drinker.adjust_tox_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                    return UPDATE_MOB_HEALTH;
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:689
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        REMOVE_TRAIT(affected_mob, TRAIT_HALT_RADIATION_EFFECTS, Dm.text("[]", type));
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:704
public partial class datum_reagent_consumable_ethanol_bloody_mary : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:713
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_blood_volume((0.25f + round(2 * Dm.num(drinker.get_drunk_amount()) / 40, 0.1f)) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), maximum: BLOOD_VOLUME_NORMAL); // Bloody Mary restores blood loss based on how drunk you are
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:718
public partial class datum_reagent_consumable_ethanol_brave_bull : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:730
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        tough_text = pick(I18n.T("brawny"), I18n.T("tenacious"), I18n.T("tough"), I18n.T("hardy"), I18n.T("sturdy")); //Tuff stuff
        to_chat(drinker, span_notice(I18n.T("You feel []!", tough_text)));
        drinker.maxHealth += 10; //Brave Bull makes you sturdier, and thus capable of withstanding a tiny bit more punishment.
        drinker.health += 10;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:737
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        to_chat(drinker, span_notice(I18n.T("You no longer feel [].", tough_text)));
        drinker.maxHealth -= 10;
        drinker.health = min(drinker.health - 10, drinker.maxHealth); //This can indeed crit you if you're alive solely based on alchol ingestion
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:744
public partial class datum_reagent_consumable_ethanol_tequila_sunrise : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:755
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        to_chat(drinker, span_notice(I18n.T("You feel gentle warmth spread through your body!")));
        light_holder = Dm.@new<obj_effect>(drinker);
        await light_holder.set_light(3, 0.7f, COLOR_TANGERINE_YELLOW); //Tequila Sunrise makes you radiate dim light, like a sunrise!
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:761
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (QDELETED(light_holder))
            await holder.del_reagent(type); //If we lost our light object somehow, remove the reagent
        else if (light_holder.loc != drinker)
            await light_holder.forceMove(drinker);
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:768
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_notice(I18n.T("The warmth in your body fades.")));
        QDEL_NULL(ref light_holder);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:774
public partial class datum_reagent_consumable_ethanol_toxins_special : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:783
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living drinker = (mob_living)affected_mob;
        drinker.adjust_bodytemperature(15 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(drinker.get_body_temp_normal()) + 20); //310.15 is the normal bodytemp.
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:788
public partial class datum_reagent_consumable_ethanol_beepsky_smash : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:801
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            metabolization_rate = 4 * REAGENTS_METABOLISM;
        // if you don't have a liver, or your liver isn't an officer's liver
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver == null || !HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
        {
            beepsky_hallucination = Dm.@new<datum_brain_trauma_special_beepsky>();
            await drinker.gain_trauma(beepsky_hallucination, TRAUMA_RESILIENCE_ABSOLUTE);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:811
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_jitter_if_lower(4 * SECONDS);
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        // if you have a liver and that liver is an officer's liver
        if (liver != null && HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
        {
            if (Dm.truthy(await drinker.adjust_stamina_loss(-4 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
            if (SPT_PROB(10, seconds_per_tick))
                await drinker.cause_hallucination(get_random_valid_hallucination_subtype(typeof(datum_hallucination_nearby_fake_item)), name);
            if (SPT_PROB(5, seconds_per_tick))
                await drinker.cause_hallucination(typeof(datum_hallucination_stray_bullet), name);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:824
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (beepsky_hallucination != null)
            QDEL_NULL(ref beepsky_hallucination);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:829
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        // if you don't have a liver, or your liver isn't an officer's liver
        if (liver == null || !HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
            await drinker.gain_trauma(typeof(datum_brain_trauma_mild_phobia_security), TRAUMA_RESILIENCE_BASIC);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:847
public partial class datum_reagent_consumable_ethanol_manly_dorf : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:857
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (ishuman(affected_mob))
        {
            mob_living_carbon_human potential_dwarf = (mob_living_carbon_human)affected_mob;
            if (HAS_TRAIT(potential_dwarf, TRAIT_DWARF))
            {
                to_chat(potential_dwarf, span_notice(I18n.T("Now THAT is MANLY!")));
                boozepwr = 50; // will still smash but not as much.
                dorf_mode = TRUE;
            }
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:866
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon dwarf = (mob_living_carbon)affected_mob;
        if (Dm.truthy(dorf_mode))
        {
            float need_mob_update;
            need_mob_update = Dm.num(await dwarf.adjust_brute_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await dwarf.adjust_fire_loss(-2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:895
public partial class datum_reagent_consumable_ethanol_b52 : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:905
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        playsound(affected_mob, Dm.resource("sound/effects/explosion/explosion_distant.ogg"), 100, FALSE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:953
public partial class datum_reagent_consumable_ethanol_manhattan_proj : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:962
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_drugginess(1 * MINUTES * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:977
public partial class datum_reagent_consumable_ethanol_antifreeze : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:986
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_bodytemperature(20 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(drinker.get_body_temp_normal()) + 20); //310.15 is the normal bodytemp.
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:991
public partial class datum_reagent_consumable_ethanol_barefoot : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1000
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (ishuman(drinker)) //Barefoot causes the imbiber to quickly regenerate brute trauma if they're not wearing shoes.
        {
            mob_living_carbon_human unshoed = (mob_living_carbon_human)drinker;
            if (unshoed.shoes == null)
            {
                if (Dm.truthy(await unshoed.adjust_brute_loss(-3 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype)))
                    return UPDATE_MOB_HEALTH;
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1019
public partial class datum_reagent_consumable_ethanol_demonsblood : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1029
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        RegisterSignal(affected_mob, COMSIG_LIVING_BLOOD_CRAWL_PRE_CONSUMED, nameof(pre_bloodcrawl_consumed));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1033
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_LIVING_BLOOD_CRAWL_PRE_CONSUMED);
    }

    /// Prevents the imbiber from being dragged into a pool of blood by a slaughter demon.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1038
    [SignalHandler]
    public virtual object pre_bloodcrawl_consumed(mob_living source = null, datum_action_cooldown_spell_jaunt_bloodcrawl crawl = null, mob_living jaunter = null, obj_effect_decal_cleanable blood = null)
    {
        turf jaunt_turf = get_turf(jaunter);
        jaunt_turf.visible_message(
            span_warning(I18n.T("Something prevents [] from entering []!", source, blood)),
            blind_message: span_notice(I18n.T("You hear a splash and a thud.")));
        to_chat(jaunter, span_warning(I18n.T("A strange force is blocking [] from entering!", source)));
        return COMPONENT_STOP_CONSUMPTION;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1057
public partial class datum_reagent_consumable_ethanol_devilskiss : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1066
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        RegisterSignal(affected_mob, COMSIG_LIVING_BLOOD_CRAWL_CONSUMED, nameof(on_bloodcrawl_consumed));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1070
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        UnregisterSignal(affected_mob, COMSIG_LIVING_BLOOD_CRAWL_CONSUMED);
    }

    /// If eaten by a slaughter demon, the demon will regret it.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1075
    [SignalHandler]
    public virtual object on_bloodcrawl_consumed(mob_living source = null, datum_action_cooldown_spell_jaunt_bloodcrawl crawl = null, mob_living jaunter = null)
    {
        object dot = COMPONENT_STOP_CONSUMPTION;
        to_chat(jaunter, span_boldwarning(I18n.T("AAH! THEIR FLESH! IT BURNS!")));
        Waiting.NoSleep(jaunter.apply_damage(25, BRUTE, wound_bonus: CANT_WOUND));
        foreach (obj_effect_decal_cleanable nearby_blood in Dm.each<obj_effect_decal_cleanable>(range(1, get_turf(source))))
        {
            if (!Dm.truthy(nearby_blood.can_bloodcrawl_in()))
                continue;
            Waiting.NoSleep(source.forceMove(get_turf(nearby_blood)));
            source.visible_message(span_warning(I18n.T("[] violently expels []!", nearby_blood, source)));
            // crawl.exit_blood_effect(source): the blood crawl spell is inert (the slaughter demon, an antagonist, is not ported)
            Dm.Deferred("code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1091", "on_bloodcrawl_consumed: crawl.exit_blood_effect(source) (the blood crawl spell and the slaughter demon are not ported)");
            return dot;
        }
        // Fuck it, just eject them, thanks to some split second cleaning
        Waiting.NoSleep(source.forceMove(get_turf(source)));
        source.visible_message(span_warning(I18n.T("[] appears from nowhere, covered in blood!", source)));
        // crawl.exit_blood_effect(source): as above
        Dm.Deferred("code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1096", "on_bloodcrawl_consumed: crawl.exit_blood_effect(source) (the blood crawl spell and the slaughter demon are not ported)");
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1132
public partial class datum_reagent_consumable_ethanol_singulo : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1140  var/static/list/ray_filter (rules 10.6: a type-level
    // static lives in TgState, built at first use)
    public static DmList ray_filter => TgState.Current.datum_reagent_consumable_ethanol_singulo__ray_filter ??= Dm.list(Dm.kv("type", "rays"), Dm.kv("size", 40), Dm.kv("density", 15), Dm.kv("color", SUPERMATTER_SINGULARITY_RAYS_COLOUR), Dm.kv("factor", 15));

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1143
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_filter("singulo_rays");
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1147
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(2.5f, seconds_per_tick))
        {
            // 20u = 1x1, 45u = 2x2, 80u = 3x3
            float volume_to_radius = FLOOR(sqrt(Dm.num(volume) / 5), 1) - 1;
            float suck_range = clamp(volume_to_radius, 0, 3);
            if (!Dm.truthy(suck_range))
                return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio); // tg's second ..(): the parents run twice this tick (kept)
            turf gravity_well_turf = get_turf(drinker);
            await goonchem_vortex(gravity_well_turf, 0, suck_range);
            playsound(get_turf(drinker), Dm.resource("sound/effects/supermatter.ogg"), 150, TRUE);
            drinker.add_filter("singulo_rays", 1, ray_filter);
            Dm.animate(drinker.get_filter("singulo_rays"), Dm.named("offset", 10), Dm.named("time", 1.5f * SECONDS), Dm.named("loop", -1));
            _addtimer(Dm.@new<datum_callback>(drinker, nameof(datum.remove_filter), "singulo_rays"), 1.5f * SECONDS, file: "code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm", line: 1161);
            await drinker.emote("burp");
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1140  the singulo's ray filter
    internal DmList datum_reagent_consumable_ethanol_singulo__ray_filter;
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1166
public partial class datum_reagent_consumable_ethanol_sbiten : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1175
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_bodytemperature(50 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, BODYTEMP_HEAT_DAMAGE_LIMIT); //310.15 is the normal bodytemp.
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1201
public partial class datum_reagent_consumable_ethanol_iced_beer : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1209
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_bodytemperature(-20 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), T0C); //310.15 is the normal bodytemp.
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1276
public partial class datum_reagent_consumable_ethanol_changelingsting : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1285
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon target = (mob_living_carbon)affected_mob;
        datum_antagonist_changeling changeling = IS_CHANGELING(target) as datum_antagonist_changeling;
        changeling?.adjust_chemicals(metabolization_rate * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1301
public partial class datum_reagent_consumable_ethanol_syndicatebomb : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1310
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(2.5f, seconds_per_tick))
            playsound(get_turf(drinker), Dm.resource("sound/effects/explosion/explosionfar.ogg"), 100, TRUE);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1347
public partial class datum_reagent_consumable_ethanol_bananahonk : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1357
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if ((liver != null && HAS_TRAIT(liver, TRAIT_COMEDY_METABOLISM)) || HAS_TRAIT(drinker, TRAIT_SIMIAN))
        {
            float heal = 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1366
public partial class datum_reagent_consumable_ethanol_silencer : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1376
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (ishuman(drinker) && HAS_MIND_TRAIT(drinker, TRAIT_MIMING))
        {
            await drinker.set_silence_if_lower(MIMEDRINK_SILENCE_DURATION);
            float heal = 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1414
public partial class datum_reagent_consumable_ethanol_fetching_fizz : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1424
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        turf drinker_turf = get_turf(drinker);
        foreach (obj_item_stack_ore ore in Dm.each<obj_item_stack_ore>(orange(3, drinker)))
            await step_towards(ore, drinker_turf);
        return dot;
    }
}

//Another reference. Heals those in critical condition extremely quickly.
// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1432
public partial class datum_reagent_consumable_ethanol_hearty_punch : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1442
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (drinker.health <= 0)
        {
            float need_mob_update;
            need_mob_update = Dm.num(await drinker.adjust_brute_loss(-3.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await drinker.adjust_fire_loss(-3.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await drinker.adjust_oxy_loss(-5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await drinker.adjust_tox_loss(-3.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1463
public partial class datum_reagent_consumable_ethanol_atomicbomb : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1473
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_drugginess(100 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (!HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.adjust_confusion(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.set_dizzy_if_lower(20 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.adjust_slurring(6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        // switch(current_cycle)
        if (current_cycle >= 52 && current_cycle <= 201)
        {
            await drinker.Sleeping(100 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        else if (current_cycle >= 202)
        {
            await drinker.AdjustSleeping(4 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            if (Dm.truthy(await drinker.adjust_tox_loss(2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1489
public partial class datum_reagent_consumable_ethanol_gargle_blaster : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1498
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.adjust_dizzy(3 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 16 && current_cycle <= 46)
        {
            await drinker.adjust_slurring(3 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        else if (current_cycle >= 46 && current_cycle <= 56)
        {
            if (SPT_PROB(30, seconds_per_tick))
                await drinker.adjust_confusion(3 * SECONDS * Dm.num(metabolization_ratio));
        }
        else if (current_cycle >= 56 && current_cycle <= 201)
        {
            await drinker.set_drugginess(110 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        else if (current_cycle >= 201)
        {
            if (Dm.truthy(await drinker.adjust_tox_loss(2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1514
public partial class datum_reagent_consumable_ethanol_neurotoxin : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1524
    public virtual object pick_paralyzed_limb()
    {
        return (pick(TRAIT_PARALYSIS_L_ARM, TRAIT_PARALYSIS_R_ARM, TRAIT_PARALYSIS_R_LEG, TRAIT_PARALYSIS_L_LEG));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1527
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.set_drugginess(50 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await drinker.adjust_dizzy(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        float need_mob_update;
        need_mob_update = Dm.num(await drinker.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 150, required_organ_flag: affected_organ_flags));
        if (SPT_PROB(10, seconds_per_tick))
        {
            need_mob_update += Dm.num(await drinker.adjust_stamina_loss(5 * Dm.num(metabolization_ratio), updating_stamina: FALSE, required_biotype: affected_biotype));
            await drinker.drop_all_held_items();
            to_chat(drinker, span_notice(I18n.T("You cant feel your hands!")));
        }
        if (current_cycle > 6)
        {
            if (SPT_PROB(10, seconds_per_tick))
            {
                object paralyzed_limb = pick_paralyzed_limb();
                ADD_TRAIT(drinker, paralyzed_limb, type);
                need_mob_update += Dm.num(await drinker.adjust_stamina_loss(5 * Dm.num(metabolization_ratio), updating_stamina: FALSE, required_biotype: affected_biotype));
            }
            if (current_cycle > 31)
            {
                need_mob_update += Dm.num(await drinker.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: affected_organ_flags));
                if (current_cycle > 51 && SPT_PROB(7.5f, seconds_per_tick))
                {
                    if (!Dm.truthy(drinker.undergoing_cardiac_arrest()) && Dm.truthy(drinker.can_heartattack()))
                    {
                        drinker.set_heartattack(TRUE);
                        if (!IS_UNCONSCIOUS_OR_CRIT(drinker))
                            drinker.visible_message(span_userdanger(I18n.T("[] clutches at [] chest as if [] heart stopped!", drinker, drinker.p_their(), drinker.p_their())));
                    }
                }
            }
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1552
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        REMOVE_TRAIT(drinker, TRAIT_PARALYSIS_L_ARM, type);
        REMOVE_TRAIT(drinker, TRAIT_PARALYSIS_R_ARM, type);
        REMOVE_TRAIT(drinker, TRAIT_PARALYSIS_R_LEG, type);
        REMOVE_TRAIT(drinker, TRAIT_PARALYSIS_L_LEG, type);
        await drinker.adjust_stamina_loss(10, required_biotype: affected_biotype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1561
public partial class datum_reagent_consumable_ethanol_hippies_delight : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1572
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await drinker.set_slurring_if_lower(2.5f * SECONDS * k);
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 2 && current_cycle <= 6)
        {
            await drinker.set_dizzy_if_lower(50 * SECONDS * k);
            await drinker.set_drugginess(150 * SECONDS * k);
            if (SPT_PROB(5, seconds_per_tick))
                await drinker.emote(pick("twitch", "giggle"));
        }
        else if (current_cycle >= 6 && current_cycle <= 11)
        {
            await drinker.set_jitter_if_lower(100 * SECONDS * k);
            await drinker.set_dizzy_if_lower(100 * SECONDS * k);
            await drinker.set_drugginess(225 * SECONDS * k);
            if (SPT_PROB(10, seconds_per_tick))
                await drinker.emote(pick("twitch", "giggle"));
        }
        else if (current_cycle >= 11 && current_cycle <= 201)
        {
            await drinker.set_jitter_if_lower(200 * SECONDS * k);
            await drinker.set_dizzy_if_lower(200 * SECONDS * k);
            await drinker.set_drugginess(5 * MINUTES * k);
            if (SPT_PROB(16, seconds_per_tick))
                await drinker.emote(pick("twitch", "giggle"));
        }
        else if (current_cycle >= 201)
        {
            await drinker.set_jitter_if_lower(300 * SECONDS * k);
            await drinker.set_dizzy_if_lower(300 * SECONDS * k);
            await drinker.set_drugginess(6.25f * MINUTES * k);
            if (SPT_PROB(23, seconds_per_tick))
                await drinker.emote(pick("twitch", "giggle"));
            if (SPT_PROB(16, seconds_per_tick))
            {
                if (Dm.truthy(await drinker.adjust_tox_loss(5 * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                    return UPDATE_MOB_HEALTH;
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1627
public partial class datum_reagent_consumable_ethanol_narsour : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1636
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.adjust_timed_status_effect(6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), typeof(datum_status_effect_speech_slurring_cult), max_duration: 6 * SECONDS);
        await drinker.adjust_stutter_up_to(6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 6 * SECONDS);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1678
public partial class datum_reagent_consumable_ethanol_quadruple_sec : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1687
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        //Securidrink in line with the Screwdriver for engineers or Nothing for mimes
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
        {
            float heal = 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1697
public partial class datum_reagent_consumable_ethanol_quintuple_sec : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1706
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        //Securidrink in line with the Screwdriver for engineers or Nothing for mimes but STRONG..
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
        {
            float need_mob_update;
            need_mob_update = Dm.num(await drinker.heal_bodypart_damage(2 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await drinker.adjust_stamina_loss(-5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE, required_biotype: affected_biotype));
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1738
public partial class datum_reagent_consumable_ethanol_bastion_bourbon : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1750
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        float heal_points = 10;
        if (drinker.health <= 0)
            heal_points = 20; //heal more if we're in softcrit
        float need_mob_update;
        float heal_amt = min(Dm.num(volume), heal_points); //only heals 1 point of damage per unit on add, for balance reasons
        need_mob_update = Dm.num(await drinker.adjust_brute_loss(-heal_amt, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await drinker.adjust_fire_loss(-heal_amt, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await drinker.adjust_tox_loss(-heal_amt, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await drinker.adjust_oxy_loss(-heal_amt, updating_health: FALSE, required_biotype: affected_biotype));
        // heal stamina loss on first metabolization, but only to a max of 20
        need_mob_update += Dm.num(await drinker.adjust_stamina_loss(max(-heal_amt * 5, -20), updating_stamina: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            await drinker.updatehealth();
        drinker.visible_message(span_warning(I18n.T("[] shivers with renewed vigor!", drinker)), span_notice(I18n.T("One taste of [] fills you with energy!", LOWER_TEXT(name))));
        if (!IS_UNCONSCIOUS_OR_CRIT(drinker) && heal_points == 20) //brought us out of softcrit
            drinker.visible_message(span_danger(I18n.T("[] lurches to [] feet!", drinker, drinker.p_their())), span_boldnotice(I18n.T("Up and at 'em, kid.")));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1769
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living drinker = (mob_living)affected_mob;
        if (drinker.health > 0)
        {
            float need_mob_update;
            need_mob_update = Dm.num(await drinker.adjust_brute_loss(-0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await drinker.adjust_fire_loss(-0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await drinker.adjust_tox_loss(-0.125f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await drinker.adjust_oxy_loss(-0.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await drinker.adjust_stamina_loss(-1.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE, required_biotype: affected_biotype));
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1782
public partial class datum_reagent_consumable_ethanol_squirt_cider : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1791
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.satiety += 5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick); //for context, vitamins give 15 satiety per second
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1806
public partial class datum_reagent_consumable_ethanol_sugar_rush : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1816
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.satiety -= 10 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick); //junky as hell! a whole glass will keep you from being able to eat junk food
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1821
public partial class datum_reagent_consumable_ethanol_crevice_spike : datum_reagent_consumable_ethanol
{
    //damage only applies when drink first enters system and won't again until drink metabolizes out
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1830
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.adjust_brute_loss(3 * min(5, Dm.num(volume)), required_bodytype: affected_bodytype); //minimum 3 brute damage on ingestion to limit non-drink means of injury - a full 5 unit gulp of the drink trucks you for the full 15
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1844
public partial class datum_reagent_consumable_ethanol_peppermint_patty : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1854
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.apply_status_effect(typeof(datum_status_effect_throat_soothed));
        drinker.adjust_bodytemperature(5 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(drinker.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1859
public partial class datum_reagent_consumable_ethanol_alexander : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1870
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        if (ishuman(drinker))
        {
            mob_living_carbon_human the_human = (mob_living_carbon_human)drinker;
            foreach (obj_item_shield the_shield in Dm.each<obj_item_shield>(the_human.contents))
            {
                mighty_shield = WEAKREF(the_shield) as datum_weakref;
                the_shield.block_chance += 10;
                to_chat(the_human, span_notice(I18n.T("[] appears polished, although you don't recall polishing it.", the_shield)));
                break;
            }
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1880
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        mob_living drinker = (mob_living)affected_mob;
        obj_item_shield the_shield = mighty_shield?.resolve() as obj_item_shield;
        if (the_shield != null && !Dm.@in(the_shield, drinker.contents)) //If you had a shield and lose it, you lose the reagent as well. Otherwise this is just a normal drink.
        {
            await holder.remove_reagent(type, volume);
            return null;
        }
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1887
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        obj_item_shield the_shield = mighty_shield?.resolve() as obj_item_shield;
        if (the_shield != null)
        {
            the_shield.block_chance -= 10;
            to_chat(drinker, span_notice(I18n.T("You notice [] looks worn again. Weird.", the_shield)));
            mighty_shield = null;
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1916
public partial class datum_reagent_consumable_ethanol_between_the_sheets : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1927
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living drinker = (mob_living)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float is_between_the_sheets = FALSE;
        foreach (obj_item_bedsheet bedsheet in Dm.each<obj_item_bedsheet>(range(drinker.loc, 0)))
        {
            if (bedsheet.loc != drinker.loc) // bedsheets in your backpack/neck don't count
                continue;
            is_between_the_sheets = TRUE;
            break;
        }

        if (!Dm.truthy(drinker.IsSleeping()) || !Dm.truthy(is_between_the_sheets))
            return dot;

        object need_mob_update = null;
        if (Dm.truthy(drinker.get_brute_loss()) && Dm.truthy(drinker.get_fire_loss())) //If you are damaged by both types, slightly increased healing but it only heals one. The more the merrier wink wink.
        {
            if (prob(50))
                need_mob_update = await drinker.adjust_brute_loss(-0.25f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
            else
                need_mob_update = await drinker.adjust_fire_loss(-0.25f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        }
        else if (Dm.truthy(drinker.get_brute_loss())) //If you have only one, it still heals but not as well.
        {
            need_mob_update = await drinker.adjust_brute_loss(-0.2f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        }
        else if (Dm.truthy(drinker.get_fire_loss()))
        {
            need_mob_update = await drinker.adjust_fire_loss(-0.2f * k, updating_health: FALSE, required_bodytype: affected_bodytype);
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1983
public partial class datum_reagent_consumable_ethanol_fernet : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:1992
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (drinker.nutrition <= NUTRITION_LEVEL_STARVING)
        {
            if (Dm.truthy(await drinker.adjust_tox_loss(1 * k, updating_health: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
        }
        await drinker.adjust_nutrition(-5 * k);
        drinker.overeatduration = 0;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2000
public partial class datum_reagent_consumable_ethanol_fernet_cola : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2010
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (drinker.nutrition <= NUTRITION_LEVEL_STARVING)
        {
            if (Dm.truthy(await drinker.adjust_tox_loss(0.5f * k, updating_health: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
        }
        await drinker.adjust_nutrition(-3 * k);
        drinker.overeatduration = 0;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2018
public partial class datum_reagent_consumable_ethanol_fanciulli : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2029
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.adjust_nutrition(-5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        drinker.overeatduration = 0;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2034
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        if (drinker.health > 0)
            await drinker.adjust_stamina_loss(20, required_biotype: affected_biotype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2039
public partial class datum_reagent_consumable_ethanol_branca_menta : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2050
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_bodytemperature(-20 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), T0C);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2054
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        if (drinker.health > 0)
            await drinker.adjust_stamina_loss(35, required_biotype: affected_biotype);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2059
public partial class datum_reagent_consumable_ethanol_blank_paper : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2070
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (ishuman(drinker) && HAS_MIND_TRAIT(drinker, TRAIT_MIMING))
        {
            await drinker.set_silence_if_lower(MIMEDRINK_SILENCE_DURATION);
            float heal = 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2078
public partial class datum_reagent_consumable_ethanol_fruit_wine : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2089
    public override async DmTask on_new(object data = null)
    {
        if (!Dm.truthy(data))
            return;

        DmList data_list = data as DmList;
        this.data = data;
        names = data_list["names"] as DmList;
        tastes = data_list["tastes"] as DmList;
        boozepwr = Dm.num(data_list["boozepwr"]);
        color = data_list["color"] as string;
        generate_data_info(data_list);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2100
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        float diff = (Dm.num(amount) / Dm.num(volume));
        if (diff < 1)
            color = BlendRGB(color, mix_data["color"], diff / 2) as string; //The percentage difference over two, so that they take average if equal.
        else
            color = BlendRGB(color, mix_data["color"], (1 / diff) / 2) as string; //Adjust so it's always blending properly.
        float oldvolume = Dm.num(volume) - Dm.num(amount);

        DmList cachednames = mix_data["names"] as DmList;
        foreach (object name in Dm.each(Dm.bitor(names, cachednames)))
            names[name] = ((Dm.num(names[name]) * oldvolume) + (Dm.num(cachednames?[name]) * Dm.num(amount))) / Dm.num(volume);

        DmList cachedtastes = mix_data["tastes"] as DmList;
        foreach (object taste in Dm.each(Dm.bitor(tastes, cachedtastes)))
            tastes[taste] = ((Dm.num(tastes[taste]) * oldvolume) + (Dm.num(cachedtastes?[taste]) * Dm.num(amount))) / Dm.num(volume);

        boozepwr *= oldvolume;
        float newzepwr = Dm.num(mix_data["boozepwr"]) * Dm.num(amount);
        boozepwr += newzepwr;
        boozepwr /= Dm.num(volume); //Blending boozepwr to volume.
        generate_data_info(data as DmList);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2123
    public virtual void generate_data_info(DmList data = null)
    {
        // BYOND's compiler fails to catch non-consts in a ranged switch case, and it causes incorrect behavior. So this needs to explicitly be a constant.
        const float minimum_percent = 0.15f; //Percentages measured between 0 and 1.
        DmList primary_tastes = Dm.list();
        DmList secondary_tastes = Dm.list();
        foreach (object taste in Dm.each(tastes))
        {
            float sw__1 = Dm.num(tastes[taste]);
            if (sw__1 >= minimum_percent * 2)
                Dm.append(ref primary_tastes, taste);
            else if (sw__1 >= minimum_percent && sw__1 <= minimum_percent * 2)
                Dm.append(ref secondary_tastes, taste);
        }

        float minimum_name_percent = 0.35f;
        name = "";
        DmList names_in_order = sortTim(names, "/proc/" + nameof(GlobalProcs.cmp_numeric_dsc), TRUE) as DmList;
        float named = FALSE;
        foreach (object fruit_name in Dm.each(names))
        {
            if (Dm.num(names[fruit_name]) >= minimum_name_percent)
            {
                name += Dm.text("[] ", fruit_name);
                named = TRUE;
            }
        }
        if (Dm.truthy(named))
            name += "Wine";
        else
            name = Dm.text("Mixed [] Wine", names_in_order[1]);

        string alcohol_description;
        float sw__2 = boozepwr;
        if (sw__2 >= 120)
            alcohol_description = "suicidally strong";
        else if (sw__2 >= 90 && sw__2 <= 120)
            alcohol_description = "rather strong";
        else if (sw__2 >= 70 && sw__2 <= 90)
            alcohol_description = "strong";
        else if (sw__2 >= 40 && sw__2 <= 70)
            alcohol_description = "rich";
        else if (sw__2 >= 20 && sw__2 <= 40)
            alcohol_description = "mild";
        else if (sw__2 >= 0 && sw__2 <= 20)
            alcohol_description = "sweet";
        else
            alcohol_description = "watery"; //How the hell did you get negative boozepwr?

        DmList fruits = Dm.list();
        if (names_in_order.len <= 3)
        {
            fruits = names_in_order;
        }
        else
        {
            for (float i = 1; i <= 3; i++)
                Dm.append(ref fruits, names_in_order[i]);
            Dm.append(ref fruits, "other plants");
        }
        object fruit_list = english_list(fruits);
        description = Dm.text("A [] wine brewed from [].", alcohol_description, fruit_list);

        string flavor = "";
        if (!Dm.truthy(primary_tastes.len))
            primary_tastes = Dm.list(Dm.text("[] alcohol", alcohol_description));
        flavor += Dm.str(english_list(primary_tastes));
        if (Dm.truthy(secondary_tastes.len))
        {
            flavor += ", with a hint of ";
            flavor += Dm.str(english_list(secondary_tastes));
        }
        taste_description = flavor;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2194
public partial class datum_reagent_consumable_ethanol_wizz_fizz : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2204
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        //A healing drink similar to Quadruple Sec, Ling Stings, and Screwdrivers for the Wizznerds; the check is consistent with the changeling sting
        if (Dm.truthy(drinker?.mind?.has_antag_datum(typeof(datum_antagonist_wizard))))
        {
            float need_mob_update;
            need_mob_update = Dm.num(await drinker.heal_bodypart_damage(1 * k, 1 * k, updating_health: FALSE));
            need_mob_update += Dm.num(await drinker.adjust_oxy_loss(-1 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await drinker.adjust_tox_loss(-1 * k, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await drinker.adjust_stamina_loss(-5 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
            if (Dm.truthy(need_mob_update))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2216
public partial class datum_reagent_consumable_ethanol_bug_spray : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2227
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        await AddElement(typeof(datum_element_bugkiller_reagent)); // inert until its package: the reagent's bug-killing exposure
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2231
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        // Does some damage to bug biotypes
        if (Dm.truthy(await drinker.adjust_tox_loss(1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
        {
            dot = UPDATE_MOB_HEALTH;
            // Random chance of causing a screm if we did some damage
            if (SPT_PROB(2, seconds_per_tick))
                await drinker.emote("scream");
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2259
public partial class datum_reagent_consumable_ethanol_turbo : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2269
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(2, seconds_per_tick))
            to_chat(drinker, span_notice(Dm.text("[]", pick(I18n.T("You feel disregard for the rule of law."), I18n.T("You feel pumped!"), I18n.T("Your head is pounding."), I18n.T("Your thoughts are racing..")))));
        if (Dm.truthy(await drinker.adjust_stamina_loss(-0.5f * Dm.num(drinker.get_drunk_amount()) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2276
public partial class datum_reagent_consumable_ethanol_old_timer : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2286
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(10, seconds_per_tick) && ishuman(drinker))
        {
            mob_living_carbon_human metabolizer = (mob_living_carbon_human)drinker;
            metabolizer.age = Dm.num(metabolizer.age) + 1;
            if (metabolizer.age > 70)
            {
                metabolizer.set_facial_haircolor("#cccccc", update: FALSE);
                metabolizer.set_haircolor("#cccccc", update: TRUE);
                if (metabolizer.age > 100)
                {
                    await metabolizer.become_nearsighted(type);
                    if (Dm.eq(metabolizer.gender, MALE))
                        metabolizer.set_facial_hairstyle("Beard (Very Long)", update: TRUE);

                    if (metabolizer.age > 969) //Best not let people get older than this or i might incur G-ds wrath
                    {
                        metabolizer.visible_message(span_notice(I18n.T("[] becomes older than any man should be.. and crumbles into dust!", metabolizer)));
                        await metabolizer.dust(just_ash: FALSE, drop_items: TRUE, force: FALSE);
                    }
                }
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2324
public partial class datum_reagent_consumable_ethanol_trappist : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2334
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(drinker.mind?.holy_role))
        {
            if (Dm.truthy(await drinker.adjust_fire_loss(-2.5f * k, updating_health: FALSE, required_bodytype: affected_bodytype)))
                dot = UPDATE_MOB_HEALTH;
            await drinker.adjust_jitter(-2 * SECONDS * k);
            await drinker.adjust_stutter(-2 * SECONDS * k);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2342
public partial class datum_reagent_consumable_ethanol_blazaam : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2350
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.num(drinker.get_drunk_amount()) > 40)
        {
            if (Dm.truthy(stored_teleports))
            {
                await do_teleport(drinker, get_turf(drinker), rand(1, 3), channel: TELEPORT_CHANNEL_WORMHOLE);
                stored_teleports--;
            }

            if (SPT_PROB(5, seconds_per_tick))
            {
                stored_teleports += rand(2, 6);
                if (prob(70))
                    await drinker.vomit(vomit_flags: VOMIT_CATEGORY_DEFAULT, vomit_type: typeof(obj_effect_decal_cleanable_vomit_purple));
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2369
public partial class datum_reagent_consumable_ethanol_mauna_loa : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2379
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        // Heats the user up while the reagent is in the body. Occasionally makes you burst into flames.
        drinker.adjust_bodytemperature(25 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick));
        if (SPT_PROB(2.5f, seconds_per_tick))
        {
            await drinker.adjust_fire_stacks(1 * Dm.num(metabolization_ratio));
            drinker.ignite_mob();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2406
public partial class datum_reagent_consumable_ethanol_pina_olivada : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2414
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(8, seconds_per_tick))
            drinker.manual_emote(pick(I18n.T("coughs up some oil"), I18n.T("swallows the lump in [] throat", drinker.p_their()), I18n.T("gags"), I18n.T("chokes up a bit")));
        if (SPT_PROB(3, seconds_per_tick))
        {
            // tg: var/static/list/messages (a cache of constant texts): picked from directly
            to_chat(drinker, span_notice((string)pick(
                I18n.T("A horrible aftertaste coats your mouth."),
                I18n.T("You feel like you're going to choke on the oil in your throat."),
                I18n.T("You start to feel some heartburn coming on."),
                I18n.T("You want to throw up, but you know that nothing can come out due to the clog in your esophagus."),
                I18n.T("Your throat feels horrible."))));
        }
        return dot;
    }
}

// pruno mix is in drink_reagents
// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2428
public partial class datum_reagent_consumable_ethanol_pruno : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2437
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.adjust_disgust(5 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2472
public partial class datum_reagent_consumable_ethanol_kortara : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2482
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.truthy(drinker.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: 1 * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2488
public partial class datum_reagent_consumable_ethanol_sea_breeze : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2498
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.apply_status_effect(typeof(datum_status_effect_throat_soothed));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2512
public partial class datum_reagent_consumable_ethanol_drunken_espatier : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2522
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.add_mood_event("numb", typeof(datum_mood_event_narcotic_medium), name); //comfortably numb
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2526
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2530
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy), type);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2534
public partial class datum_reagent_consumable_ethanol_protein_blend : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2545
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await drinker.adjust_nutrition(2 * k);
        if (!HAS_TRAIT(drinker, TRAIT_LIZARD_METABOLISM))
            drinker.adjust_disgust(5 * k);
        else
            drinker.adjust_disgust(2 * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2563
public partial class datum_reagent_consumable_ethanol_triumphal_arch : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2573
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(drinker, TRAIT_LIZARD_METABOLISM))
            await drinker.add_mood_event("triumph", typeof(datum_mood_event_memories_of_home), name);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2578
public partial class datum_reagent_consumable_ethanol_the_juice : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2589
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        prophet_trauma = Dm.@new<datum_brain_trauma_special_bluespace_prophet>();
        await drinker.gain_trauma(prophet_trauma, TRAUMA_RESILIENCE_ABSOLUTE);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2594
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        if (prophet_trauma != null)
            QDEL_NULL(ref prophet_trauma);
    }
}

//a jacked up absinthe that causes hallucinations to the game master controller basically, used in smuggling objectives
// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2600
public partial class datum_reagent_consumable_ethanol_ritual_wine : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2608
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living psychonaut = affected_mob;
        if (psychonaut.hud_used == null)
            return;
        atom_movable_plane_master_controller game_plane_master_controller = psychonaut.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.add_filter("ritual_wine", 1, Dm.list(Dm.kv("type", "wave"), Dm.kv("size", 1), Dm.kv("x", 5), Dm.kv("y", 0), Dm.kv("flags", WAVE_SIDEWAYS)));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2615
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living psychonaut = affected_mob;
        if (psychonaut.hud_used == null)
            return;
        atom_movable_plane_master_controller game_plane_master_controller = psychonaut.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.remove_filter("ritual_wine");
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2733
public partial class datum_reagent_consumable_ethanol_helianthus : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2745
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (SPT_PROB(5, seconds_per_tick))
            await drinker.adjust_hallucinations_up_to(4 * SECONDS * Dm.num(metabolization_ratio), 48 * SECONDS);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2772
public partial class datum_reagent_consumable_ethanol_gin_garden : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2783
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon doll = (mob_living_carbon)affected_mob;
        doll.adjust_bodytemperature(-5 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(doll.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2787
public partial class datum_reagent_consumable_ethanol_wine_voltaic : datum_reagent_consumable_ethanol
{
    //can't be on life because of the way blood works.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2796
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(INGEST, INJECT), PATCH))) || !iscarbon(exposed_mob))
            return;

        mob_living_carbon exposed_carbon = (mob_living_carbon)exposed_mob;
        obj_item_organ_stomach_ethereal stomach = exposed_carbon.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (stomach != null)
            await stomach.adjust_charge(Dm.num(reac_volume) * 10 * ETHEREAL_DISCHARGE_RATE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2806
public partial class datum_reagent_consumable_ethanol_telepole : datum_reagent_consumable_ethanol
{
    //can't be on life because of the way blood works.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2817
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(INGEST, INJECT), PATCH))) || !iscarbon(exposed_mob))
            return;

        mob_living_carbon exposed_carbon = (mob_living_carbon)exposed_mob;
        obj_item_organ_stomach_ethereal stomach = exposed_carbon.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (stomach != null)
            await stomach.adjust_charge(Dm.num(reac_volume) * 20 * ETHEREAL_DISCHARGE_RATE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2827
public partial class datum_reagent_consumable_ethanol_pod_tesla : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2837
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        affected_mob.add_traits(Dm.list(TRAIT_SHOCKIMMUNE, TRAIT_TESLA_SHOCKIMMUNE, TRAIT_FEARLESS), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2841
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_traits(Dm.list(TRAIT_SHOCKIMMUNE, TRAIT_TESLA_SHOCKIMMUNE, TRAIT_FEARLESS), type);
    }

    //can't be on life because of the way blood works.
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:2845
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(INGEST, INJECT), PATCH))) || !iscarbon(exposed_mob))
            return;

        mob_living_carbon exposed_carbon = (mob_living_carbon)exposed_mob;
        obj_item_organ_stomach_ethereal stomach = exposed_carbon.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (stomach != null)
            await stomach.adjust_charge(Dm.num(reac_volume) * 30 * ETHEREAL_DISCHARGE_RATE);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3172
public partial class datum_reagent_consumable_ethanol_suffering_bastard : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3182
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_headache_soothed)); //prevents headaches
        affected_carbon.adjust_disgust(-5 * k); //removes disgust, same with sol dry
        if (Dm.truthy(await affected_carbon.adjust_organ_loss(ORGAN_SLOT_BRAIN, -0.5f * k * Dm.num(normalise_creation_purity()), required_organ_flag: affected_organ_flags))) //heals brain damage very slowly, about 12 damage per 5u
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3189
public partial class datum_reagent_consumable_ethanol_blue_blazer : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3199
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        affected_carbon.adjust_bodytemperature(25 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(affected_carbon.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3203
public partial class datum_reagent_consumable_ethanol_hot_toddy : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3214
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        affected_carbon.adjust_bodytemperature(25 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(affected_carbon.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3238
public partial class datum_reagent_consumable_ethanol_flip_cocktail : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3248
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (prob(10))
            await affected_mob.emote("flip");
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3253
public partial class datum_reagent_consumable_ethanol_aperitivo : datum_reagent_consumable_ethanol
{
    //This and some of the cocktails it gets mixed into stimulate the apetite, as an aperitivo should
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3262  (tg names the third parameter times_fired)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.adjust_nutrition(-1 * REM() * Dm.num(seconds_per_tick));
        drinker.overeatduration = 0;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3287
public partial class datum_reagent_consumable_ethanol_bartenders_handshake : datum_reagent_consumable_ethanol
{
    //Heals bartenders brute and burn, disgusts otherwise
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3297  (tg names the third parameter times_fired)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = drinker.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (HAS_TRAIT(liver, TRAIT_BARTENDER_METABOLISM))
        {
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: 1 * REM() * Dm.num(seconds_per_tick), burn: 1 * REM() * Dm.num(seconds_per_tick), updating_health: FALSE)))
                dot = UPDATE_MOB_HEALTH;
        }
        else
        {
            drinker.adjust_disgust(2 * REM() * Dm.num(seconds_per_tick));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3326
public partial class datum_reagent_consumable_ethanol_garibaldi : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3337
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.truthy(IS_REVOLUTIONARY(drinker)))
        {
            //status effect has a duration of 5 seconds which gets refreshed by this, falls off on it's own in case of running out of drink or deconversion
            await drinker.apply_status_effect(typeof(datum_status_effect_rev_resilience));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3365
public partial class datum_reagent_consumable_ethanol_last_word : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3375
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        mob_living drinker = exposed_mob;
        //Mutes people for 5 seconds on their first sip every 5 minutes. Requires ingest, you can't savor something that's injected into your eyes or whatever.
        if (!Dm.truthy(Dm.bitand(methods, INGEST)) || !iscarbon(drinker) || HAS_TRAIT(drinker, TRAIT_HAD_LAST_WORD))
            return;

        ADD_TRAIT(drinker, TRAIT_HAD_LAST_WORD, type);
        to_chat(drinker, span_notice(I18n.T("You take a moment to silently savor your drink...")));
        await drinker.set_silence_if_lower(5 * SECONDS);
        _addtimer(TRAIT_CALLBACK_REMOVE(drinker, TRAIT_HAD_LAST_WORD, type), 300 * SECONDS, file: "code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm", line: 3384);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3396
public partial class datum_reagent_consumable_ethanol_negroni : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3407  (tg names the third parameter times_fired)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.overeatduration -= 20;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3421
public partial class datum_reagent_consumable_ethanol_nuclear_daiquiri_thermo : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3431
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        mob_living living_mob = affected_mob;
        await living_mob.apply_status_effect(typeof(datum_status_effect_cherenkov_radiation)); //makes the drinker glow blue, and rarely emit high-energy nuclear particles.
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3435
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        mob_living living_mob = affected_mob;
        living_mob.remove_status_effect(typeof(datum_status_effect_cherenkov_radiation));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3439
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await drinker.set_drugginess(100 * SECONDS * k);
        await drinker.set_jitter_if_lower(20 * SECONDS * k);
        await drinker.set_dizzy_if_lower(10 * SECONDS * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3445
public partial class datum_reagent_consumable_ethanol_poets_dream : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3455
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (InertSeam.IsInertType(typeof(datum_status_effect_grouped_heretic_dreams)))
            Dm.Deferred("code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3457", "on_mob_metabolize: apply_status_effect(/datum/status_effect/grouped/heretic_dreams) (the heretic's dreams: the heretic is not ported, D333)");
        else
            await affected_mob.apply_status_effect(typeof(datum_status_effect_grouped_heretic_dreams), type);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3459
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_status_effect(typeof(datum_status_effect_grouped_heretic_dreams), type);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3474
public partial class datum_reagent_consumable_ethanol_spritz : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3485  (tg names the third parameter times_fired)
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        drinker.overeatduration -= 20;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3499
public partial class datum_reagent_consumable_ethanol_mouthwash : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3508
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living drinker = (mob_living)affected_mob;
        await drinker.apply_status_effect(typeof(datum_status_effect_throat_soothed));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3513
public partial class datum_reagent_consumable_ethanol_mouthwash_strong : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3521
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        await drinker.apply_status_effect(typeof(datum_status_effect_throat_soothed));
        if (SPT_PROB(5, seconds_per_tick) && !HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.adjust_hallucinations(8 * SECONDS * Dm.num(metabolization_ratio));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3536
public partial class datum_reagent_consumable_ethanol_stone_cold_stout : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3545
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        MODIFY_PHYSIOLOGY(drinker, BRUTE, 0.8f);
        MODIFY_PHYSIOLOGY(drinker, BURN, 0.8f);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3550
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        MODIFY_PHYSIOLOGY(drinker, BRUTE, 1.25f);
        MODIFY_PHYSIOLOGY(drinker, BURN, 1.25f);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3555
public partial class datum_reagent_consumable_ethanol_ramp_rager : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3565
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        object need_mob_update = null;
        if (SPT_PROB(15, seconds_per_tick) && !HAS_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE))
            await drinker.adjust_hallucinations(8 * SECONDS * Dm.num(metabolization_ratio));
        need_mob_update = Dm.add(need_mob_update, await drinker.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.75f * k));
        await drinker.AdjustAllImmobility(-1 * SECONDS * k);
        if (Dm.truthy(await drinker.adjust_stamina_loss(-15 * k, updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        if (Dm.truthy(need_mob_update))
            dot = UPDATE_MOB_HEALTH;
        if (SPT_PROB(5, seconds_per_tick))
            await drinker.emote(pick("twitch", "drool"));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3579
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        await drinker.apply_status_effect(typeof(datum_status_effect_spasms));
        RegisterSignal(drinker, COMSIG_LIVING_ENTER_STAMCRIT, nameof(on_stamcrit));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3584
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        mob_living drinker = affected_mob;
        drinker.remove_status_effect(typeof(datum_status_effect_spasms));
        UnregisterSignal(drinker, COMSIG_LIVING_ENTER_STAMCRIT);
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3589
    [SignalHandler]
    public virtual object on_stamcrit(mob_living drinker = null)
    {
        Waiting.NoSleep(drinker.set_stamina_loss(90, updating_stamina: TRUE));
        to_chat(drinker, span_message(I18n.T("This can't end like this... Blood rushes away from your head...")));
        volume -= (min(Dm.num(volume), 6));
        return STAMCRIT_CANCELLED;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3605
public partial class datum_reagent_consumable_ethanol_icegrave : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/alcohol_reagents.dm:3615
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon affected_carbon = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = affected_carbon.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_CORONER_METABOLISM)) //Coroners know your icegraves. Also, its made using formaldehyde, so.
            return dot;

        float icegrave_temp = -200;
        await affected_carbon.apply_status_effect(typeof(datum_status_effect_ice_block_talisman), 5 * SECONDS);
        float thermal_protection = 1 - Dm.num(affected_carbon.get_insulation_protection(affected_carbon.bodytemperature + icegrave_temp));
        float applied_temp = (thermal_protection * icegrave_temp) + icegrave_temp;
        affected_carbon.adjust_bodytemperature(applied_temp);
        return dot;
    }
}
