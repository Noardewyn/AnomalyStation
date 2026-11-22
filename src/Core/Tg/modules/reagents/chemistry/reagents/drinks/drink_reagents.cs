// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG1 (D333, D339): the whole file. The juices, milks, coffees, teas, sodas and energy drinks with their effects, and the
// wounds' tea_life_process (the cut, puncture and burn wounds that tea helps). Milk's and soda water's on_hydroponics_apply are
// gaps P2's (D298); laughter's and super laughter's on_mob_life the silicons' SI2 (D326: the clown cyborg's injector). Pwr Game's
// gaming skill is gaps SL1's (D333). The moodlets are live since gaps MS1.

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1
public partial class datum_reagent_consumable_orangejuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:11
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.get_oxy_loss()) && SPT_PROB(16, seconds_per_tick))
        {
            // tg passes FALSE positionally (updating_health) and again as updating_health = FALSE: one argument here
            if (Dm.truthy(await mob.adjust_oxy_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:17
public partial class datum_reagent_consumable_tomatojuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:26
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.get_fire_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(brute: 0, burn: 0.5f * Dm.num(metabolization_ratio), updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:32
public partial class datum_reagent_consumable_limejuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:42
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:48
public partial class datum_reagent_consumable_carrotjuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:56
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await mob.adjust_eye_blur(-1 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await mob.adjust_temp_blindness(-1 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        object need_mob_update = null;
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 21 && current_cycle <= 110)
        {
            if (SPT_PROB(100 * (1 - (sqrt(110 - current_cycle) / 10)), seconds_per_tick))
                need_mob_update = await mob.adjust_organ_loss(ORGAN_SLOT_EYES, -1 * Dm.num(metabolization_ratio));
        }
        else if (current_cycle >= 110)
        {
            need_mob_update = await mob.adjust_organ_loss(ORGAN_SLOT_EYES, -1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:85
public partial class datum_reagent_consumable_poisonberryjuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:93
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_tox_loss(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:116
public partial class datum_reagent_consumable_banana : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:124
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if ((liver != null && HAS_TRAIT(liver, TRAIT_COMEDY_METABOLISM)) || HAS_TRAIT(mob, TRAIT_SIMIAN))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), burn: 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:131
public partial class datum_reagent_consumable_nothing : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:142
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (ishuman(drinker) && HAS_MIND_TRAIT(drinker, TRAIT_MIMING))
        {
            await drinker.set_silence_if_lower(MIMEDRINK_SILENCE_DURATION);
            if (Dm.truthy(await drinker.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), burn: 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:149 (silicons SI2: the clown cyborg's laughter injector)
public partial class datum_reagent_consumable_laughter : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:158
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        await mob.emote("laugh");
        await mob.add_mood_event("chemical_laughter", typeof(datum_mood_event_chemical_laughter));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:163
public partial class datum_reagent_consumable_superlaughter : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:172
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        if (SPT_PROB(16, seconds_per_tick))
        {
            mob.visible_message(span_danger(I18n.T("[] bursts out into a fit of uncontrollable laughter!", mob)), span_userdanger(I18n.T("You burst out in a fit of uncontrollable laughter!")));
            await mob.Stun(5);
            await mob.add_mood_event("chemical_laughter", typeof(datum_mood_event_chemical_superlaughter));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:188
public partial class datum_reagent_consumable_pickle : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:197
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if ((liver != null && HAS_TRAIT(liver, TRAIT_CORONER_METABOLISM)))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:220
public partial class datum_reagent_consumable_milk : datum_reagent_consumable
{
    // Milk is good for humans, but bad for plants.
    // The sugars cannot be used by plants, and the milk fat harms growth. Except shrooms.
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:232
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume) * 0.3f));
        obj_item_seeds myseed = mytray.myseed;
        if (myseed == null || myseed.get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) != null)
            return;
        myseed.adjust_potency(-Dm.round(Dm.num(volume) * 0.5f));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:239
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = null;
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE)))
                dot = UPDATE_MOB_HEALTH;
        }
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_capsaicin))))
            await holder.remove_reagent(typeof(datum_reagent_consumable_capsaicin), seconds_per_tick);
        object parent = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        return Dm.truthy(parent) ? parent : dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:247
    public virtual async DmTask<object> used_on_fish(obj_item_fish fish = null)
    {
        // if(HAS_TRAIT(fish, TRAIT_FISH_MADE_OF_BONE)) fish.repair_damage(fish.max_integrity * max(fish.get_hunger() * 0.5, 0.12));
        // fish.sate_hunger(); return TRUE: a bone fish fed milk heals and is sated
        Dm.Deferred("code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:247", "used_on_fish: a bone fish fed milk repairs and is sated (fishing is not ported: the fish's sate_hunger and feeding, M10+)");
        return null;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:253
public partial class datum_reagent_consumable_soymilk : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:262
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(1, 0)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:268
public partial class datum_reagent_consumable_cream : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:277
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(10, seconds_per_tick) && Dm.truthy(await mob.heal_bodypart_damage(1, 0)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:282
public partial class datum_reagent_consumable_coffee : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:294
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:298
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        await mob.AdjustSleeping(-2 * SECONDS * k);
        //310.15 is the normal bodytemp.
        mob.adjust_bodytemperature(12.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_frostoil))))
            await holder.remove_reagent(typeof(datum_reagent_consumable_frostoil), 2.5f * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:308
public partial class datum_reagent_consumable_tea : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:320
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-2 * SECONDS * k);
        await mob.adjust_drowsiness(-1 * SECONDS * k);
        await mob.adjust_jitter(-3 * SECONDS * k);
        await mob.AdjustSleeping(-1 * SECONDS * k);
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
        }
        mob.adjust_bodytemperature(10 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));

        float to_chatted = FALSE;
        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(mob.all_wounds))
        {
            if (SPT_PROB(10, seconds_per_tick))
            {
                object helped = iter_wound.tea_life_process();
                if (!Dm.truthy(to_chatted) && Dm.truthy(helped))
                    to_chat(mob, span_notice(I18n.T("A calm, relaxed feeling suffuses you. Your wounds feel a little healthier.")));
                to_chatted = TRUE;
            }
        }
        return dot;
    }
}

// Different handling, different name.
// Returns FALSE by default so broken bones and 'loss' wounds don't give a false message
// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:341
public partial class datum_wound : datum
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:341
    public virtual object tea_life_process()
    {
        return FALSE;
    }
}

// Slowly increase (gauzed) clot rate
// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:345
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:345
    public override object tea_life_process()
    {
        gauzed_clot_rate = Dm.add(gauzed_clot_rate, 0.1f);
        return TRUE;
    }
}

// Slowly increase clot rate
// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:350
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:350
    public override object tea_life_process()
    {
        clot_rate = Dm.add(clot_rate, 0.2f);
        return TRUE;
    }
}

// There's a designated burn process, but I felt this would be better for consistency with the rest of the reagent's procs
// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:355
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:355
    public override object tea_life_process()
    {
        // Sanitizes and heals, but with a limit
        flesh_healing = (flesh_healing > 0.1f) ? flesh_healing : flesh_healing + 0.02f;
        infection_rate = max(infection_rate - 0.005f, 0);
        return TRUE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:371
public partial class datum_reagent_consumable_tea_arnold_palmer : datum_reagent_consumable_tea
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:381
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(2.5f, seconds_per_tick))
            to_chat(mob, span_notice(Dm.text("[]", pick(I18n.T("You remember to square your shoulders."), I18n.T("You remember to keep your head down."), I18n.T("You can't decide between squaring your shoulders and keeping your head down."), I18n.T("You remember to relax."), I18n.T("You think about how someday you'll get two strokes off your golf game.")))));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:386
public partial class datum_reagent_consumable_icecoffee : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:397
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:401
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        await mob.AdjustSleeping(-2 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:408
public partial class datum_reagent_consumable_hot_ice_coffee : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:419
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:423
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        await mob.AdjustSleeping(-3 * SECONDS * k);
        mob.adjust_bodytemperature(-3.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(await mob.adjust_tox_loss(0.5f * k, updating_health: FALSE, required_biotype: affected_biotype)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:432
public partial class datum_reagent_consumable_icetea : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:442
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-2 * SECONDS * k);
        await mob.adjust_drowsiness(-1 * SECONDS * k);
        await mob.AdjustSleeping(-2 * SECONDS * k);
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                dot = UPDATE_MOB_HEALTH;
        }
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:452
public partial class datum_reagent_consumable_space_cola : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:460
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await mob.adjust_drowsiness(-5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:465
public partial class datum_reagent_consumable_roy_rogers : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:474
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await mob.set_jitter_if_lower(6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await mob.adjust_drowsiness(-5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:480
public partial class datum_reagent_consumable_nuka_cola : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:489
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nuka_cola));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:493
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nuka_cola));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:497
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.set_jitter_if_lower(20 * SECONDS * k);
        await mob.set_drugginess(30 * SECONDS * k);
        await mob.adjust_dizzy(1.5f * SECONDS * k);
        mob.remove_status_effect(typeof(datum_status_effect_drowsiness));
        await mob.AdjustSleeping(-2 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(SSradiation?.can_irradiate_basic(mob))) // rules 10.6: a harness world may lack SSradiation
            await mob.AddComponent(typeof(datum_component_irradiated));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:508
public partial class datum_reagent_consumable_rootbeer : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:521
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        REMOVE_TRAIT(affected_mob, TRAIT_DOUBLE_TAP, type);
        if (current_cycle > 10)
        {
            to_chat(affected_mob, span_warning(I18n.T("You feel kinda tired as your sugar rush wears off...")));
            await affected_mob.adjust_stamina_loss(min(80, current_cycle * 3), required_biotype: affected_biotype);
            await affected_mob.adjust_drowsiness((current_cycle - 1) * 2 * SECONDS);
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:529
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (current_cycle > 3 && !Dm.truthy(effect_enabled)) // takes a few seconds for the bonus to kick in to prevent microdosing
        {
            to_chat(mob, span_notice(I18n.T("You feel your trigger finger getting itchy...")));
            ADD_TRAIT(mob, TRAIT_DOUBLE_TAP, type);
            effect_enabled = TRUE;
        }

        await mob.set_jitter_if_lower(1 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (prob(50))
            await mob.adjust_dizzy(0.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (current_cycle > 10)
            await mob.adjust_dizzy(0.75f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:542
public partial class datum_reagent_consumable_grey_bull : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:552
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living_carbon affected_atom = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = affected_atom.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (HAS_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM))
        {
            await affected_atom.add_mood_event("maintenance_fun", typeof(datum_mood_event_maintenance_high));
            metabolization_rate *= 0.8f;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:559
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.set_jitter_if_lower(20 * SECONDS * k);
        await mob.adjust_dizzy(1 * SECONDS * k);
        mob.remove_status_effect(typeof(datum_status_effect_drowsiness));
        await mob.AdjustSleeping(-2 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:567
public partial class datum_reagent_consumable_spacemountainwind : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:576
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_drowsiness(-7 * SECONDS * k);
        await mob.AdjustSleeping(-1 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        await mob.set_jitter_if_lower(5 * SECONDS * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:583
public partial class datum_reagent_consumable_dr_gibb : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:591
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await mob.adjust_drowsiness(-6 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:596
public partial class datum_reagent_consumable_space_up : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:604
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:608
public partial class datum_reagent_consumable_lemon_lime : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:616
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:620
public partial class datum_reagent_consumable_pwr_game : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:628
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.ge(exposed_mob?.mind?.get_skill_level(typeof(datum_skill_gaming)), SKILL_LEVEL_LEGENDARY) && Dm.truthy(Dm.bitand(methods, INGEST)) && !HAS_TRAIT(exposed_mob, TRAIT_GAMERGOD))
        {
            ADD_TRAIT(exposed_mob, TRAIT_GAMERGOD, "pwr_game");
            to_chat(exposed_mob, span_nicegreen(I18n.T("As you imbibe the Pwr Game, your gamer third eye opens... You feel as though a great secret of the universe has been made known to you...")));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:635
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = affected_mob as mob_living_carbon;
        mob.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num_or_null(mob.get_body_temp_normal()));
        if (SPT_PROB(5, seconds_per_tick))
            mob.mind?.adjust_experience(typeof(datum_skill_gaming), 5);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:641
public partial class datum_reagent_consumable_shamblers : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:649
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:653
public partial class datum_reagent_consumable_sodawater : datum_reagent_consumable
{
    // A variety of nutrients are dissolved in club soda, without sugar.
    // These nutrients include carbon, oxygen, hydrogen, phosphorous, potassium, sulfur and sodium, all of which are needed for healthy plant growth.
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:663
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_waterlevel(Dm.round(Dm.num(volume)));
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.1f));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:667
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:673
public partial class datum_reagent_consumable_tonic : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:681
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        await mob.AdjustSleeping(-2 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:688
public partial class datum_reagent_consumable_wellcheers : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:696
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await mob.adjust_drowsiness(1.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        object need_mob_update = null;
        // switch(affected_mob.mob_mood.sanity_level): a moodless mob's null read matches no range (tg would runtime there, DV1)
        float? sanity_level = mob.mob_mood?.sanity_level;
        if (sanity_level >= SANITY_LEVEL_GREAT && sanity_level <= SANITY_LEVEL_NEUTRAL)
            need_mob_update = await mob.adjust_brute_loss(-0.75f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE);
        else if (sanity_level >= SANITY_LEVEL_DISTURBED && sanity_level <= SANITY_LEVEL_UNSTABLE)
            await mob.add_mood_event("wellcheers", typeof(datum_mood_event_wellcheers));
        else if (sanity_level >= SANITY_LEVEL_CRAZY && sanity_level <= SANITY_LEVEL_INSANE)
            need_mob_update = await mob.adjust_stamina_loss(1.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_stamina: FALSE);
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:710
public partial class datum_reagent_consumable_monkey_energy : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:720
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.set_jitter_if_lower(40 * SECONDS * k);
        await mob.adjust_dizzy(1 * SECONDS * k);
        mob.remove_status_effect(typeof(datum_status_effect_drowsiness));
        await mob.AdjustSleeping(-2 * SECONDS * k);
        mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:728
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_SIMIAN))
            await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_monkey_energy));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:733
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_monkey_energy));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:737
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(7.5f, seconds_per_tick))
            await affected_mob.say(pick_list_replacements(BOOMER_FILE, "boomer"), forced: typeof(datum_reagent_consumable_monkey_energy));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:742
public partial class datum_reagent_consumable_ice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:751
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(mob.adjust_bodytemperature(-2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), FALSE, Dm.num(mob.get_body_temp_normal()))))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:756
public partial class datum_reagent_consumable_soy_latte : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:768
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:772
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.SetSleeping(0));
        mob.adjust_bodytemperature(2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
            need_mob_update += Dm.num(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:784
public partial class datum_reagent_consumable_cafe_latte : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:796
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:800
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-6 * SECONDS * k);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.SetSleeping(0));
        mob.adjust_bodytemperature(2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
            need_mob_update += Dm.num(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:812
public partial class datum_reagent_consumable_doctor_delight : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:821
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.25f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.25f * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await mob.adjust_oxy_loss(-0.25f * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(mob.nutrition) && (mob.nutrition - 2 > 0))
        {
            obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
            if (!(HAS_TRAIT(liver, TRAIT_MEDICAL_METABOLISM)))
            {
                // Drains the nutrition of the holder. Not medical doctors though, since it's the Doctor's Delight!
                await mob.adjust_nutrition(-1 * k);
            }
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:836
public partial class datum_reagent_consumable_cinderella : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:845
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_disgust(-2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:925
public partial class datum_reagent_consumable_pumpkin_latte : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:937
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.set_jitter_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:941
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_dizzy(-5 * SECONDS * k);
        await mob.adjust_drowsiness(-3 * SECONDS * k);
        float need_mob_update;
        need_mob_update = Dm.num(await mob.SetSleeping(0));
        mob.adjust_bodytemperature(2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
            need_mob_update += Dm.num(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:988
public partial class datum_reagent_consumable_grape_soda : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:996
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1009
public partial class datum_reagent_consumable_hot_coco : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1018
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = null;
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(mob.get_brute_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.heal_bodypart_damage(brute: 0.5f * Dm.num(metabolization_ratio), burn: 0, updating_health: FALSE)))
                dot = UPDATE_MOB_HEALTH;
        }
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_capsaicin))))
            await holder.remove_reagent(typeof(datum_reagent_consumable_capsaicin), 1 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        object parent = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        return Dm.truthy(parent) ? parent : dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1027
public partial class datum_reagent_consumable_italian_coco : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1037
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(2.5f * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1041
public partial class datum_reagent_consumable_menthol : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1050
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living mob = (mob_living)affected_mob;
        await mob.apply_status_effect(typeof(datum_status_effect_throat_soothed));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1054
public partial class datum_reagent_consumable_grenadine : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1062
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        mob_living drinker = affected_mob;
        if (Dm.truthy(IS_REVOLUTIONARY(drinker)))
            to_chat(drinker, span_warning(I18n.T("Antioxidants are weakening your radical spirit!")));
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1067
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        if (Dm.truthy(IS_REVOLUTIONARY(drinker)))
        {
            await drinker.set_dizzy_if_lower(5 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
            if (Dm.num(drinker.get_stamina_loss()) < 80)
                await drinker.adjust_stamina_loss(12, required_biotype: affected_biotype); //The pomegranate stops free radicals! Har har.
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1099
public partial class datum_reagent_consumable_cream_soda : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1108
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-2.5f * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1112
public partial class datum_reagent_consumable_sol_dry : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1121
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_disgust(-2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1125
public partial class datum_reagent_consumable_shirley_temple : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1134
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_disgust(-1.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1138
public partial class datum_reagent_consumable_red_queen : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1148
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (SPT_PROB(50, seconds_per_tick))
            return dot;

        float newsize = Dm.num(pick(0.5f, 0.75f, 1, 1.50f, 2));
        newsize *= RESIZE_DEFAULT_SIZE;
        await mob.update_transform(newsize / current_size);
        current_size = newsize;
        if (SPT_PROB(23, seconds_per_tick))
            await mob.emote("sneeze");
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1160
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.update_transform(RESIZE_DEFAULT_SIZE / current_size);
        current_size = RESIZE_DEFAULT_SIZE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1181
public partial class datum_reagent_consumable_aloejuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1189
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living mob = (mob_living)affected_mob;
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(16, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1195
public partial class datum_reagent_consumable_agua_fresca : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1204
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(mob.get_body_temp_normal()));
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-0.25f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1211
public partial class datum_reagent_consumable_mushroom_tea : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1220
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_LIZARD_METABOLISM))
        {
            if (Dm.truthy(await mob.adjust_oxy_loss(-0.25f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1307
public partial class datum_reagent_consumable_cucumberlemonade : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1317
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon doll = (mob_living_carbon)affected_mob;
        doll.adjust_bodytemperature(-4 * Dm.num(metabolization_ratio) * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(seconds_per_tick), Dm.num(doll.get_body_temp_normal()));
        if (Dm.truthy(doll.get_tox_loss()) && SPT_PROB(10, seconds_per_tick))
        {
            if (Dm.truthy(await doll.adjust_tox_loss(-0.25f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1324
public partial class datum_reagent_consumable_mississippi_queen : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1332
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon drinker = (mob_living_carbon)affected_mob;
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 11 && current_cycle <= 21)
        {
            await drinker.adjust_dizzy(2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        else if (current_cycle >= 21 && current_cycle <= 31)
        {
            if (SPT_PROB(15, seconds_per_tick))
                await drinker.adjust_confusion(2 * SECONDS * Dm.num(metabolization_ratio));
        }
        else if (current_cycle >= 31 && current_cycle <= 201)
        {
            await drinker.adjust_hallucinations(30 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1343
public partial class datum_reagent_consumable_t_letter : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1352
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (!HAS_MIND_TRAIT(mob, TRAIT_MIMING))
            return dot;
        await mob.set_silence_if_lower(MIMEDRINK_SILENCE_DURATION);
        await mob.adjust_drowsiness(-3 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        await mob.AdjustSleeping(-2 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (Dm.truthy(mob.get_tox_loss()) && SPT_PROB(25, seconds_per_tick))
        {
            if (Dm.truthy(await mob.adjust_tox_loss(-1 * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1387
public partial class datum_reagent_consumable_volt_energy : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1396
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

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1406
public partial class datum_reagent_consumable_fruit_punch : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1416
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living mob = (mob_living)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update = 0;
        float found_valid_cooler = FALSE;
        foreach (obj_structure_reagent_dispensers_water_cooler found_cooler in Dm.each<obj_structure_reagent_dispensers_water_cooler>(range(4, mob)))
        {
            if (Dm.truthy(found_cooler.anchored))
            {
                found_valid_cooler = TRUE;
                obj_effect_temp_visual_heal heal_effect = Dm.@new<obj_effect_temp_visual_heal>(get_turf(found_cooler));
                heal_effect.color = "#f7b2e3";
                break;
            }
        }

        if (Dm.truthy(found_valid_cooler))
        {
            mob.clear_alert("punch_bad");
            mob.throw_alert("punch_good", typeof(atom_movable_screen_alert_fruit_punch_good));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(-0.3f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_brute_loss(-0.3f * k, updating_health: FALSE));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.3f * k, updating_health: FALSE));
            mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_punch_punishment));
        }
        else
        {
            mob.clear_alert("punch_good");
            mob.throw_alert("punch_bad", typeof(atom_movable_screen_alert_fruit_punch_bad));
            need_mob_update += Dm.num(await mob.apply_damage(0.75f * k, TOX));
            await mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_punch_punishment));
            if (SPT_PROB(10, seconds_per_tick))
            {
                await mob.Knockdown(3 * SECONDS, 6 * SECONDS); //Gives daze effect. Using the cooler is a commitment and if you get jumped during it or have to run away to fight something, you should be vulnerable.
                to_chat(mob, span_warning(I18n.T("The overwhelming sweetness of the fruit punch disorients and confounds you!")));
            }
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1448
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.clear_alert("punch_bad");
        affected_mob.clear_alert("punch_good");
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_punch_punishment));
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1466
public partial class datum_reagent_consumable_ethanol_bitters_soda : datum_reagent_consumable_ethanol
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1476
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_disgust(-2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1480
public partial class datum_reagent_consumable_lean : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/drinks/drink_reagents.dm:1491
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.adjust_jitter(2.5f * SECONDS * k);
        await mob.adjust_stutter(2.25f * SECONDS * k);
        await mob.adjust_drugginess(2 * SECONDS * k);
        if (SPT_PROB(15, seconds_per_tick))
            await mob.emote(pick("taunt", "twitch", "shiver", "laugh", "moan", "blush", "stare"));
        if (current_cycle > 16 && SPT_PROB(3.5f, seconds_per_tick))
        {
            await mob.adjust_dizzy(15 * SECONDS * Dm.num(metabolization_ratio));
            await mob.adjust_drowsiness(6.5f * SECONDS * Dm.num(metabolization_ratio));
            await mob.emote("drool");
        }
        return dot;
    }
}
