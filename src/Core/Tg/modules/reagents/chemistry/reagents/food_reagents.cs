// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/food_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG1 (D333, D339): the whole file, with the wounds' salt, flour and starch hooks (interleaved types, rules 1.9). Earlier: M8
// slice A the consumable's get_nutriment_factor; M8 slice B (D182) the consumable's New, capsaicin and condensed capsaicin (the
// pepper spray); gaps S1 (D291) the consumable's nutrition and drink quality, nutriment with its tastes and healing, vitamin, mineral
// slurry and sugar; gaps P2 (D298) the reagents' effects in a hydroponics tray. RG1: the fat and cooking oils (hot oil frying an
// item, a mob and a fire), cloth fibers, frost oil, salt (its pile and the wounds), garlic, tear juice, sprinkles, the ramens,
// flour and corn starch (their wounds and flour's pile), corn syrup, honey, Molt'Obeso, stabilized nutriment, the Lavaland flora's
// ichors, liquid electricity, astrotame, caramel, char, peanut butter, mint extract and guidoferrun. The moodlets are live since
// gaps MS1.

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:9
[DmPath("/datum/reagent/consumable")]
public partial class datum_reagent_consumable : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:21
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // All food reagents function at a fixed rate
        chemical_flags = Dm.bitor(chemical_flags, REAGENT_UNAFFECTED_BY_METABOLISM);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:26
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (!ishuman(affected_mob) || HAS_TRAIT(affected_mob, TRAIT_NOHUNGER))
            return dot;

        mob_living_carbon_human affected_human = (mob_living_carbon_human)affected_mob;
        await affected_human.adjust_nutrition(0.5f * Dm.num(get_nutriment_factor(affected_human)) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:34
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, INGEST)) || !Dm.truthy(quality) || HAS_TRAIT(exposed_mob, TRAIT_AGEUSIA))
            return;
        // switch(quality)
        if (Dm.eq(quality, DRINK_REVOLTING))
            await exposed_mob.add_mood_event("quality_drink", typeof(datum_mood_event_quality_revolting));
        else if (Dm.eq(quality, DRINK_NICE))
            await exposed_mob.add_mood_event("quality_drink", typeof(datum_mood_event_quality_nice));
        else if (Dm.eq(quality, DRINK_GOOD))
            await exposed_mob.add_mood_event("quality_drink", typeof(datum_mood_event_quality_good));
        else if (Dm.eq(quality, DRINK_VERYGOOD))
            await exposed_mob.add_mood_event("quality_drink", typeof(datum_mood_event_quality_verygood));
        else if (Dm.eq(quality, DRINK_FANTASTIC))
        {
            await exposed_mob.add_mood_event("quality_drink", typeof(datum_mood_event_quality_fantastic));
            exposed_mob.add_mob_memory(typeof(datum_memory_good_drink), Dm.named("drink", this));
        }
        else if (Dm.eq(quality, FOOD_AMAZING))
        {
            await exposed_mob.add_mood_event("quality_food", typeof(datum_mood_event_amazingtaste));
            // The food this was in was really tasty, not the reagent itself
            atom the_real_food = holder?.my_atom as atom;
            if (isitem(the_real_food) && !is_reagent_container(the_real_food))
                exposed_mob.add_mob_memory(typeof(datum_memory_good_food), Dm.named("food", the_real_food));
        }
    }

    /// Gets just how much nutrition this reagent supplies per server tick to the eater
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:58
    public virtual object get_nutriment_factor(mob_living_carbon eater = null)
    {
        return nutriment_factor * REAGENTS_METABOLISM * purity * 2;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:61
[DmPath("/datum/reagent/consumable/nutriment")]
public partial class datum_reagent_consumable_nutriment : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:78
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(30, seconds_per_tick))
        {
            float heal = 0.5f * brute_heal * Dm.num(metabolization_ratio);
            if (Dm.truthy(await ((mob_living)affected_mob).heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:85
    public override async DmTask on_new(object data = null)
    {
        DmList supplied_data = data as DmList;
        await base.on_new(data);
        if (!Dm.truthy(this.data))
        {
            if (Dm.truthy(carry_food_tastes))
                RegisterSignal(this, COMSIG_REAGENT_GROWN_IN_PLANT, nameof(grown_in_plant));
            return;
        }
        // taste data can sometimes be ("salt" = 3, "chips" = 1)
        // and we want it to be in the form ("salt" = 0.75, "chips" = 0.25)
        // which is called "normalizing"
        if (supplied_data == null)
            supplied_data = this.data as DmList;

        // if data isn't an associative list, this has some WEIRD side effects
        // TODO probably check for assoc list?

        this.data = counterlist_normalise(supplied_data);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:102
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        if (!Dm.islist(mix_data) || !Dm.truthy(mix_data.len))
            return;

        // data for nutriment is one or more (flavour -> ratio)
        // where all the ratio values adds up to 1

        DmList taste_amounts = Dm.list();
        if (Dm.truthy(data))
            taste_amounts = ((DmList)data).Copy();

        counterlist_scale(taste_amounts, volume);

        DmList other_taste_amounts = mix_data.Copy();
        counterlist_scale(other_taste_amounts, amount);

        counterlist_combine(taste_amounts, other_taste_amounts);

        counterlist_normalise(taste_amounts);

        data = taste_amounts;
    }

    /// Inherits all the tastes of plants that we are grown in
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:126
    [SignalHandler]
    public virtual void grown_in_plant(datum source = null, obj_item_seeds our_seeds = null, obj_item our_plant = null)
    {
        if (!Dm.istype(our_plant, typeof(obj_item_food_grown)))
            return;
        var plant_food = (obj_item_food_grown)our_plant;
        data = counterlist_normalise(LAZYCOPY(plant_food.tastes));
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:134
    public override object get_taste_description(mob_living taster = null)
    {
        if (Dm.truthy(Dm.length(data)))
            return data;
        return base.get_taste_description(taster);
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:139
public partial class datum_reagent_consumable_nutriment_vitamin : datum_reagent_consumable_nutriment
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:148
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon carbon = (mob_living_carbon)affected_mob;
        if (carbon.satiety < MAX_SATIETY)
            carbon.satiety += 15 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:165
public partial class datum_reagent_consumable_nutriment_fat : datum_reagent_consumable_nutriment
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:176
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        if (holder == null || (holder.chem_temp <= fry_temperature))
            return dot;
        if (!isitem(exposed_obj) || HAS_TRAIT(exposed_obj, TRAIT_FOOD_FRIED))
            return dot;
        if (Dm.truthy(is_type_in_typecache(exposed_obj, GLOB.oilfry_blacklisted_items)) || Dm.truthy(Dm.bitand(exposed_obj.resistance_flags, INDESTRUCTIBLE)))
        {
            exposed_obj.visible_message(span_notice(I18n.T("The hot oil has no effect on []!", exposed_obj)));
            return dot;
        }
        if (exposed_obj.atom_storage != null)
        {
            exposed_obj.visible_message(span_notice(I18n.T("The hot oil splatters about as [] touches it. It seems too full to cook properly!", exposed_obj)));
            return dot;
        }

        exposed_obj.visible_message(span_warning(I18n.T("[] rapidly fries as it's splashed with hot oil! Somehow.", exposed_obj)));
        await exposed_obj.AddElement(typeof(datum_element_fried_item), Dm.num(volume) * SECONDS);
        // tg runtimes here on an item that has no reagents holder (after the frying); the port skips the add for it
        if (exposed_obj.reagents is datum_reagents fried_reagents)
            await fried_reagents.add_reagent(type, reac_volume, data as DmList, holder.chem_temp);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:193
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) || holder == null || (holder.chem_temp < fry_temperature)) //Directly coats the mob, and doesn't go into their bloodstream
            return;

        float burn_damage = ((holder.chem_temp / fry_temperature) * 0.33f); //Damage taken per unit
        if (Dm.truthy(Dm.bitand(methods, TOUCH)))
            burn_damage *= max(1 - Dm.num(touch_protection), 0);
        float FryLoss = round(min(38, burn_damage * Dm.num(reac_volume)));
        if (HAS_TRAIT(exposed_mob, TRAIT_OIL_FRIED))
            return;

        exposed_mob.visible_message(span_warning(I18n.T("The boiling oil sizzles as it covers []!", exposed_mob)), span_userdanger(I18n.T("You're covered in boiling oil!")));
        if (Dm.truthy(FryLoss))
        {
            await exposed_mob.emote("scream");
            await exposed_mob.adjust_fire_loss(FryLoss);
        }
        playsound(exposed_mob, Dm.resource("sound/machines/fryer/deep_fryer_emerge.ogg"), 25, TRUE);
        ADD_TRAIT(exposed_mob, TRAIT_OIL_FRIED, "cooking_oil_react");
        _addtimer(Dm.@new<datum_callback>(exposed_mob, nameof(mob_living.unfry_mob)), 2 * SECONDS, file: "code/modules/reagents/chemistry/reagents/food_reagents.dm", line: 212);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:214
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (exposed_turf is not turf_open open_turf)
            return dot;
        await open_turf.MakeSlippery(TURF_WET_LUBE, min_wet_time: 10 * SECONDS, wet_time_to_add: Dm.num(reac_volume) * 2 * SECONDS);
        obj_effect_hotspot hotspot = Dm.locate<obj_effect_hotspot>(open_turf);
        if (hotspot != null)
        {
            datum_gas_mixture lowertemp = open_turf.remove_air(open_turf.air.total_moles()) as datum_gas_mixture;
            lowertemp.temperature = max(min(lowertemp.temperature - 2000, lowertemp.temperature / 2), 0);
            await lowertemp.react(this);
            await open_turf.assume_air(lowertemp);
            qdel(hotspot);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:269
public partial class datum_reagent_consumable_nutriment_cloth_fibers : datum_reagent_consumable_nutriment
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:280
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (mob.satiety < MAX_SATIETY)
        {
            await mob.adjust_nutrition(CLOTHING_NUTRITION_GAIN);
            delayed_satiety_drain += CLOTHING_NUTRITION_GAIN;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:286
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        if (!iscarbon(affected_mob))
            return;

        mob_living_carbon carbon_mob = (mob_living_carbon)affected_mob;
        await carbon_mob.adjust_nutrition(-delayed_satiety_drain);
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:294
public partial class datum_reagent_consumable_nutriment_mineral : datum_reagent_consumable_nutriment
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:303
    public override object get_nutriment_factor(mob_living_carbon eater = null)
    {
        if (HAS_TRAIT(eater, TRAIT_ROCK_EATER))
            return base.get_nutriment_factor(eater);

        // You cannot eat rocks, it gives no nutrition
        return 0;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:310
public partial class datum_reagent_consumable_sugar : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:329
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        to_chat(affected_mob, span_userdanger(I18n.T("You go into hyperglycemic shock! Lay off the twinkies!")));
        await affected_mob.AdjustSleeping(20 * SECONDS);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:334
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        await affected_mob.adjust_drowsiness_up_to((0.5f * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick)), 60 * SECONDS);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:338
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, INGEST)))
            await exposed_mob.check_allergic_reaction(SUGAR, chance: Dm.num(reac_volume) * 10, histamine_add: min(10, Dm.num(reac_volume) * 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:256
[DmPath("/datum/reagent/consumable/nutriment/organ_tissue")]
public partial class datum_reagent_consumable_nutriment_organ_tissue : datum_reagent_consumable_nutriment
{
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:263
[DmPath("/datum/reagent/consumable/nutriment/organ_tissue/stomach_lining")]
public partial class datum_reagent_consumable_nutriment_organ_tissue_stomach_lining : datum_reagent_consumable_nutriment_organ_tissue
{
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:386
public partial class datum_reagent_consumable_capsaicin : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:395
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float heating = 0;
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 1 && current_cycle <= 15)
        {
            heating = 5;
            if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_cryostylane))))
                await holder.remove_reagent(typeof(datum_reagent_cryostylane), 2.5f * k);
        }
        else if (current_cycle >= 15 && current_cycle <= 25)
        {
            heating = 10;
        }
        else if (current_cycle >= 25 && current_cycle <= 35)
        {
            heating = 15;
        }
        else if (current_cycle >= 35)
        {
            heating = 20;
        }
        mob.adjust_bodytemperature(0.5f * TEMPERATURE_DAMAGE_COEFFICIENT * heating * k);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:412
public partial class datum_reagent_consumable_frostoil : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:423
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float cooling = 0;
        // switch(current_cycle): the first matching range runs
        if (current_cycle >= 1 && current_cycle <= 15)
        {
            cooling = -10;
            if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_capsaicin))))
                await holder.remove_reagent(typeof(datum_reagent_consumable_capsaicin), 2.5f * k);
        }
        else if (current_cycle >= 15 && current_cycle <= 25)
        {
            cooling = -20;
        }
        else if (current_cycle >= 25 && current_cycle <= 35)
        {
            cooling = -30;
            if (prob(1))
                await mob.emote("shiver");
        }
        else if (current_cycle >= 35)
        {
            cooling = -40;
            if (prob(5))
                await mob.emote("shiver");
        }
        mob.adjust_bodytemperature(0.5f * TEMPERATURE_DAMAGE_COEFFICIENT * cooling * k, 50);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:443
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        float volume_in = Dm.num(reac_volume);
        if (volume_in < 1)
            return dot;
        if (isopenturf(exposed_turf))
        {
            turf_open exposed_open_turf = (turf_open)exposed_turf;
            await exposed_open_turf.MakeSlippery(wet_setting: TURF_WET_ICE, min_wet_time: 100, wet_time_to_add: volume_in * SECONDS); // Is less effective in high pressure/high heat capacity environments. More effective in low pressure.
            float temperature = exposed_open_turf.air.temperature;
            float heat_capacity = Dm.num(exposed_open_turf.air.heat_capacity());
            exposed_open_turf.air.temperature = max(exposed_open_turf.air.temperature - ((temperature - TCMB) * (heat_capacity * volume_in * specific_heat) / (heat_capacity + volume_in * specific_heat)) / heat_capacity, TCMB); // Exchanges environment temperature with reagent. Reagent is at 2.7K with a heat capacity of 40J per unit.
        }
        if (volume_in < 5)
            return dot;
        foreach (mob_living_basic_slime exposed_slime in Dm.each<mob_living_basic_slime>(exposed_turf))
            await exposed_slime.adjust_tox_loss(rand(15, 30));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:458
public partial class datum_reagent_consumable_condensedcapsaicin : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:469
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        if (!ishuman(exposed_mob))
            return;

        var victim = (mob_living_carbon)exposed_mob;
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), INHALE))))
        {
            //check for protection
            //actually handle the pepperspray effects
            if (!Dm.truthy(victim.is_pepper_proof())) // you need both eye and mouth protection
            {
                if (prob(5))
                    await victim.emote("scream");
                await victim.emote("cry");
                await victim.set_eye_blur_if_lower(10 * SECONDS);
                await victim.set_temp_blindness_if_lower(6 * SECONDS);
                await victim.set_confusion_if_lower(5 * SECONDS);
                await victim.Knockdown(3 * SECONDS);
                await victim.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_pepperspray));
                _addtimer(Dm.@new<datum_callback>(victim, nameof(mob.remove_movespeed_modifier), typeof(datum_movespeed_modifier_reagent_pepperspray)), 10 * SECONDS, file: "code/modules/reagents/chemistry/reagents/food_reagents.dm", line: 486);
                ADD_TRAIT(victim, TRAIT_ANOSMIA, type);
                _addtimer(TRAIT_CALLBACK_REMOVE(victim, TRAIT_ANOSMIA, type), 30 * SECONDS, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/modules/reagents/chemistry/reagents/food_reagents.dm", line: 488);
            }
            victim.update_damage_hud();
        }
        if (Dm.truthy(Dm.bitand(methods, INGEST)))
        {
            if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_milk))))
            {
                if (prob(15))
                    to_chat(exposed_mob, span_danger(Dm.text("[]", pick(I18n.T("Your head pounds."), I18n.T("Your mouth feels like it's on fire."), I18n.T("You feel dizzy.")))));
                if (prob(10))
                    await victim.set_eye_blur_if_lower(2 * SECONDS);
                if (prob(10))
                    await victim.set_dizzy_if_lower(2 * SECONDS);
                if (prob(5))
                    await victim.vomit(VOMIT_CATEGORY_DEFAULT);
            }
        }
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:502
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_milk))))
        {
            if (SPT_PROB(5, seconds_per_tick))
                mob.visible_message(span_warning(Dm.text("[] []", mob, pick(I18n.T("dry heaves!"), I18n.T("coughs!"), I18n.T("splutters!")))));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:509
public partial class datum_reagent_consumable_salt : datum_reagent_consumable
{
    //Creates an umbra-blocking salt pile
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:518
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (!istype(exposed_turf, typeof(turf)) || (Dm.num(reac_volume) < 1))
            return dot;
        exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_food_salt));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:524
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        mob_living_carbon carbies = (mob_living_carbon)exposed_mob;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(carbies.all_wounds))
            await iter_wound.on_salt(reac_volume, carbies);
    }
}

// Salt can help with wounds by soaking up fluid, but undiluted salt will also cause irritation from the loose crystals, and it might soak up the body's water as well!
// A saltwater mixture would be best, but we're making improvised chems here, not real ones.
// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:536 (interleaved types, rules 1.9)
public partial class datum_wound : datum
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:536
    public virtual async DmTask on_salt(object reac_volume = null, mob_living_carbon carbies = null)
    {
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:539
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:539
    public override async DmTask on_salt(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.06f * Dm.num(reac_volume), Dm.num(initial_flow) * 0.6f); // 20u of a salt shacker * 0.1 = -1.6~ blood flow, but is always clamped to, at best, third blood loss from that wound.
        // Crystal irritation worsening recovery.
        gauzed_clot_rate = Dm.num(gauzed_clot_rate) * 0.65f;
        to_chat(carbies, span_notice(I18n.T("The salt bits seep in and stick to [], painfully irritating the skin but soaking up most of the blood.", LOWER_TEXT(this))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:545
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:545
    public override async DmTask on_salt(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.1f * Dm.num(reac_volume), Dm.num(initial_flow) * 0.5f); // 20u of a salt shacker * 0.1 = -2~ blood flow, but is always clamped to, at best, halve blood loss from that wound.
        // Crystal irritation worsening recovery.
        clot_rate = Dm.num(clot_rate) * 0.75f;
        to_chat(carbies, span_notice(I18n.T("The salt bits seep in and stick to [], painfully irritating the skin but soaking up most of the blood.", LOWER_TEXT(this))));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:551
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:551
    [DmParams("reac_volume")]
    public override async DmTask on_salt(object reac_volume = null, mob_living_carbon carbies = null)
    {
        // Slightly sanitizes and disinfects, but also increases infestation rate (some bacteria are aided by salt), and decreases flesh healing (can damage the skin from moisture absorption)
        sanitization += VALUE_PER(0.4f, 30) * Dm.num(reac_volume);
        infection = Dm.num(infection) - max(VALUE_PER(0.3f, 30) * Dm.num(reac_volume), 0);
        infection_rate += VALUE_PER(0.12f, 30) * Dm.num(reac_volume);
        flesh_healing -= max(VALUE_PER(5, 30) * Dm.num(reac_volume), 0);
        to_chat(victim, span_notice(I18n.T("The salt bits seep in and stick to [], painfully irritating the skin! After a few moments, it feels marginally better.", LOWER_TEXT(this))));
    }
}

//NOTE: having garlic in your blood stops vampires from biting you.
// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:578
public partial class datum_reagent_consumable_garlic : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:587
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_UNHOLY_BANEABLE)) //incapacitating but not lethal. Unfortunately, vampires cannot vomit.
        {
            if (SPT_PROB(min((current_cycle - 1) / 2, 12.5f), seconds_per_tick))
            {
                if (HAS_TRAIT(mob, TRAIT_ANOSMIA))
                    to_chat(mob, span_danger(I18n.T("You feel that something is wrong, your strength is leaving you! You can barely think...")));
                else
                    to_chat(mob, span_danger(I18n.T("You can't get the scent of garlic out of your nose! You can barely think...")));
                await mob.Paralyze(10);
                await mob.set_jitter_if_lower(20 * SECONDS);
            }
            return dot;
        }
        obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_CULINARY_METABOLISM))
        {
            if (SPT_PROB(10, seconds_per_tick)) //stays in the system much longer than sprinkles/banana juice, so heals slower to partially compensate
            {
                if (Dm.truthy(await mob.heal_bodypart_damage(brute: 3.34f * Dm.num(metabolization_ratio), burn: 3.34f * Dm.num(metabolization_ratio), updating_health: FALSE)))
                    return UPDATE_MOB_HEALTH;
            }
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:605
public partial class datum_reagent_consumable_tearjuice : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:611
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= INGEST;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!ishuman(exposed_mob))
            return;

        mob_living_carbon victim = (mob_living_carbon)exposed_mob;
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), INHALE))))
        {
            object tear_proof = victim.is_eyes_covered();
            if (!Dm.truthy(tear_proof))
            {
                to_chat(exposed_mob, span_warning(I18n.T("Your eyes sting!")));
                await victim.emote("cry");
                await victim.adjust_eye_blur(6 * SECONDS);
            }
        }
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:625
public partial class datum_reagent_consumable_sprinkles : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:632
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        obj_item_organ_liver liver = mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null && HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
        {
            float heal = 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
            if (Dm.truthy(await mob.heal_bodypart_damage(brute: heal, burn: heal, updating_health: FALSE)))
                return UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:658
public partial class datum_reagent_consumable_hot_ramen : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:676
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(5 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), 0, Dm.num(mob.get_body_temp_normal()));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:681
public partial class datum_reagent_consumable_hell_ramen : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:689
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        mob.adjust_bodytemperature(5 * TEMPERATURE_DAMAGE_COEFFICIENT * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:694
public partial class datum_reagent_consumable_flour : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:702
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        mob_living_carbon carbies = (mob_living_carbon)exposed_mob;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(carbies.all_wounds))
            await iter_wound.on_flour(reac_volume, carbies);
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:712 (interleaved types, rules 1.9)
public partial class datum_wound : datum
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:712
    public virtual async DmTask on_flour(object reac_volume = null, mob_living_carbon carbies = null)
    {
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:715
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:715
    public override async DmTask on_flour(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.015f * Dm.num(reac_volume)); // 30u of a flour sack * 0.015 = -0.45~ blood flow, prettay good
        to_chat(carbies, span_notice(I18n.T("The flour seeps into [], painfully drying it up and absorbing some of the blood.", LOWER_TEXT(this))));
        // When some nerd adds infection for wounds, make this increase the infection
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:720
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:720
    public override async DmTask on_flour(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.04f * Dm.num(reac_volume)); // 30u of a flour sack * 0.04 = -1.25~ blood flow, pretty good!
        to_chat(carbies, span_notice(I18n.T("The flour seeps into [], painfully drying some of it up and absorbing a little blood.", LOWER_TEXT(this))));
        // When some nerd adds infection for wounds, make this increase the infection
    }
}

// Don't pour flour onto burn wounds, it increases infection risk! Very unwise. Backed up by REAL info from REAL professionals.
// https://www.reuters.com/article/uk-factcheck-flour-burn-idUSKCN26F2N3
// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:727
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:727
    [DmParams("reac_volume")]
    public override async DmTask on_flour(object reac_volume = null, mob_living_carbon carbies = null)
    {
        to_chat(victim, span_notice(I18n.T("The flour seeps into [], spiking you with intense pain! That probably wasn't a good idea...", LOWER_TEXT(this))));
        sanitization -= min(0, 1); // tg's: subtracts nothing (min(0, 1) is 0), kept
        infection = Dm.num(infection) + 0.2f;
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:694
public partial class datum_reagent_consumable_flour : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:733
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (isspaceturf(exposed_turf))
            return dot;

        obj_effect_decal_cleanable_food_flour flour_decal = exposed_turf.spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_food_flour)) as obj_effect_decal_cleanable_food_flour;
        if (flour_decal != null)
            await flour_decal.init_reagents(typeof(datum_reagent_consumable_flour), reac_volume);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:805
public partial class datum_reagent_consumable_corn_starch : datum_reagent_consumable
{
    // Starch has similar absorbing properties to flour (Stronger here because it's rarer)
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:813
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!iscarbon(exposed_mob))
            return;
        mob_living_carbon carbies = (mob_living_carbon)exposed_mob;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(PATCH, TOUCH), VAPOR))))
            return;
        foreach (datum_wound iter_wound in Dm.each<datum_wound>(carbies.all_wounds))
            await iter_wound.on_starch(reac_volume, carbies);
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:823 (interleaved types, rules 1.9)
public partial class datum_wound : datum
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:823
    public virtual async DmTask on_starch(object reac_volume = null, mob_living_carbon carbies = null)
    {
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:826
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:826
    public override async DmTask on_starch(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.03f * Dm.num(reac_volume));
        to_chat(carbies, span_notice(I18n.T("The slimey starch seeps into [], painfully drying some of it up and absorbing a little blood.", LOWER_TEXT(this))));
        // When some nerd adds infection for wounds, make this increase the infection
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:832
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:832
    public override async DmTask on_starch(object reac_volume = null, mob_living_carbon carbies = null)
    {
        await adjust_blood_flow(-0.06f * Dm.num(reac_volume));
        to_chat(carbies, span_notice(I18n.T("The slimey starch seeps into [], painfully drying it up and absorbing some of the blood.", LOWER_TEXT(this))));
        // When some nerd adds infection for wounds, make this increase the infection
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:838
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:838
    public override async DmTask on_starch(object reac_volume = null, mob_living_carbon carbies = null)
    {
        to_chat(carbies, span_notice(I18n.T("The slimey starch seeps into [], spiking you with intense pain! That probably wasn't a good idea...", LOWER_TEXT(this))));
        sanitization -= min(0, 0.5f); // tg's: subtracts nothing (min(0, 0.5) is 0), kept
        infection = Dm.num(infection) + 0.1f;
        return;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:845
public partial class datum_reagent_consumable_corn_syrup : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:853
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        await holder.add_reagent(typeof(datum_reagent_consumable_sugar), 0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:858
public partial class datum_reagent_consumable_honey : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:876
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        await holder.add_reagent(typeof(datum_reagent_consumable_sugar), 1.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        float need_mob_update = 0;
        if (SPT_PROB(33, seconds_per_tick))
        {
            float health = -0.5f * Dm.num(metabolization_ratio);
            need_mob_update = Dm.num(await mob.adjust_brute_loss(health, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(health, updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_oxy_loss(health, updating_health: FALSE, required_biotype: affected_biotype));
            need_mob_update += Dm.num(await mob.adjust_tox_loss(health, updating_health: FALSE, required_biotype: affected_biotype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:889
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
            return;

        exposed_mob.add_surgery_speed_mod(type, 0.6f, min(Dm.num(reac_volume) * 1 * MINUTES, 5 * MINUTES));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:914
public partial class datum_reagent_consumable_moltobeso : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:925
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        foreach (datum_reagent_consumable food in Dm.each<datum_reagent_consumable>(((datum_reagents)mob.reagents).reagent_list))
        {
            if (food == this)
                continue;
            float food_factor = Dm.num(food.get_nutriment_factor(mob));
            if (food_factor <= 0)
                continue;
            await mob.adjust_nutrition(20 * food_factor * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:944
public partial class datum_reagent_consumable_nutriment_stabilized : datum_reagent_consumable_nutriment
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:951
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (mob.nutrition > NUTRITION_LEVEL_FULL - 25)
            await mob.adjust_nutrition(-1.5f * Dm.num(metabolization_ratio) * Dm.num(get_nutriment_factor(mob)) * Dm.num(seconds_per_tick));
        return dot;
    }
}

////Lavaland Flora Reagents////

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:960
public partial class datum_reagent_consumable_entpoly : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:968
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        bool need_mob_update = false;
        if (current_cycle > 10)
            await mob.Unconscious(20 * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), FALSE);
        if (SPT_PROB(10, seconds_per_tick))
        {
            mob.losebreath += 4;
            // tg passes the reagent's affected_biotype as adjust_organ_loss's required_organ_flag (kept)
            await mob.adjust_organ_loss(ORGAN_SLOT_BRAIN, 1 * Dm.num(metabolization_ratio), 150, affected_biotype);
            await mob.adjust_tox_loss(1.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_biotype: affected_biotype);
            await mob.adjust_stamina_loss(5 * Dm.num(metabolization_ratio), updating_stamina: FALSE, required_biotype: affected_biotype);
            await mob.set_eye_blur_if_lower(10 * SECONDS);
            need_mob_update = true;
        }
        if (need_mob_update)
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:984
public partial class datum_reagent_consumable_tinlux : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:993
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(exposed_mob.reagents)) // they won't process the reagent, but still benefit from its effects for a duration.
        {
            float amount = round(Dm.num(reac_volume) * clamp(1 - Dm.num(touch_protection), 0, 1));
            float duration = (amount / metabolization_rate) * SSmobs.wait;
            if (duration > 1 * SECONDS)
                await exposed_mob.adjust_timed_status_effect(duration, typeof(datum_status_effect_tinlux_light));
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1001
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        await affected_mob.apply_status_effect(typeof(datum_status_effect_tinlux_light)); //infinite duration
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1005
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        affected_mob.remove_status_effect(typeof(datum_status_effect_tinlux_light));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1010
public partial class datum_reagent_consumable_vitfro : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1019
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        float need_mob_update = 0;
        if (SPT_PROB(55, seconds_per_tick))
        {
            need_mob_update = Dm.num(await mob.adjust_brute_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_bodytype));
            need_mob_update += Dm.num(await mob.adjust_fire_loss(-0.5f * Dm.num(metabolization_ratio), updating_health: FALSE, required_bodytype: affected_bodytype));
        }
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1029
public partial class datum_reagent_consumable_liquidelectricity : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1037
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        RegisterSignal(this, COMSIG_REAGENT_GROWN_IN_PLANT, nameof(grown_in_plant));
        // Defaults to "enriched" state if data does not say otherwise
        if (LAZYACCESS(this.data, BLOOD_DATA_ENRICHED_ETHEREAL) == null)
        {
            DmList data_list = this.data as DmList;
            LAZYSET(ref data_list, BLOOD_DATA_ENRICHED_ETHEREAL, TRUE);
            this.data = data_list;
        }
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1044
    public override async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        await base.on_merge(mix_data, amount);
        // Inherits the incoming reagent's enriched state if it's greater in volume
        if (Dm.num(amount) > Dm.num(volume))
        {
            DmList data_list = data as DmList;
            LAZYSET(ref data_list, BLOOD_DATA_ENRICHED_ETHEREAL, LAZYACCESS(mix_data, BLOOD_DATA_ENRICHED_ETHEREAL));
            data = data_list;
        }
    }

    //can't be on life because of the way blood works.
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1050
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(LAZYACCESS(data, BLOOD_DATA_ENRICHED_ETHEREAL)))
            return;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(Dm.bitor(INGEST, INJECT), PATCH), INHALE))))
            return;

        if (exposed_mob.get_organ_slot(ORGAN_SLOT_STOMACH) is obj_item_organ_stomach_ethereal stomach)
            await stomach.adjust_charge(Dm.num(reac_volume) * 30 * ETHEREAL_DISCHARGE_RATE);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1061
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(LAZYACCESS(data, BLOOD_DATA_ENRICHED_ETHEREAL)))
            return dot;

        if (HAS_TRAIT(mob, TRAIT_ETHEREAL_METABOLISM))
        {
            mob.adjust_blood_volume(1 * Dm.num(seconds_per_tick));
        }
        else if (SPT_PROB(10, seconds_per_tick)) //lmao at the newbs who eat energy bars
        {
            await mob.electrocute_act(rand(5, 10), "Liquid Electricity in their body", 1, SHOCK_NOGLOVES); //the shock is coming from inside the house
            playsound(mob, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        }
        return dot;
    }

    /// Liquid Electricity in plants is unenriched unless the plant has the relevant gene
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1074
    [SignalHandler]
    public virtual void grown_in_plant(datum source = null, obj_item_seeds our_seeds = null, obj_item our_plant = null)
    {
        DmList data_list = data as DmList;
        LAZYSET(ref data_list, BLOOD_DATA_ENRICHED_ETHEREAL, Dm.truthy(our_seeds.get_gene(typeof(datum_plant_gene_trait_cell_charge))) ? TRUE : FALSE);
        data = data_list;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1080
public partial class datum_reagent_consumable_astrotame : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1091
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (mob.disgust < 80)
            mob.adjust_disgust(2.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1120
public partial class datum_reagent_consumable_caramel : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1129
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, INGEST)))
            await exposed_mob.check_allergic_reaction(SUGAR, chance: Dm.num(reac_volume) * 10, histamine_add: min(10, Dm.num(reac_volume) * 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1135
public partial class datum_reagent_consumable_char : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1145
    public override async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.overdose_process(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(13, seconds_per_tick))
            await affected_mob.say(pick_list_replacements(BOOMER_FILE, "boomer"), forced: typeof(datum_reagent_consumable_char));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1258
public partial class datum_reagent_consumable_peanut_butter : datum_reagent_consumable
{
    //ET loves peanut butter
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1267
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (isabductor(mob))
        {
            await mob.add_mood_event("ET_pieces", typeof(datum_mood_event_et_pieces), name);
            await mob.set_drugginess(15 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1328
public partial class datum_reagent_consumable_mintextract : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1335
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon mob = (mob_living_carbon)affected_mob;
        if (HAS_TRAIT(mob, TRAIT_FAT))
        {
            mob.investigate_log(Dm.text("has been gibbed by consuming [] while fat.", this), INVESTIGATE_DEATHS);
            mob.inflate_gib();
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1414
public partial class datum_reagent_consumable_guidoferrun : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1421
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        MODIFY_PHYSIOLOGY(affected_mob, BRUTE, 0.95f);
        MODIFY_PHYSIOLOGY(affected_mob, BURN, 0.95f);
    }

    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:1426
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        MODIFY_PHYSIOLOGY(affected_mob, BRUTE, (100f / 95));
        MODIFY_PHYSIOLOGY(affected_mob, BURN, (100f / 95));
    }
}

// Gaps P2 (D298): the reagents' effects in a hydroponics tray (on_hydroponics_apply).

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:75
public partial class datum_reagent_consumable_nutriment : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:75
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(Dm.round(Dm.num(volume) * 0.2f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:325
public partial class datum_reagent_consumable_sugar : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:325
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_weedlevel(rand(1, 2));
        mytray.adjust_pestlevel(rand(1, 2));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:353
public partial class datum_reagent_consumable_virus_food : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:353
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 0.5f));
    }
}

// tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:868
public partial class datum_reagent_consumable_honey : datum_reagent_consumable
{
    // tg: code/modules/reagents/chemistry/reagents/food_reagents.dm:868
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        if (mytray.myseed != null && prob(20))
        {
            await mytray.pollinate(range: 1);
            return;
        }
        mytray.adjust_weedlevel(rand(1, 2));
        mytray.adjust_pestlevel(rand(1, 2));
    }
}
