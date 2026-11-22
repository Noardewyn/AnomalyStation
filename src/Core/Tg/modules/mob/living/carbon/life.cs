// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the carbon's Life and breath chain translated (M2 P2, scope.md 2e "carbon": life.dm breathe, handle_organs,
// cardiac arrest): Life, handle_breathing, breathe, has_smoke_protection, handle_breath_temperature,
// get_breath_from_internal, handle_blood, handle_organs, get_heart_blood_regeneration_multiplier,
// handle_dead_metabolization, handle_liver, undergoing_liver_failure, handle_brain_damage and the heart attack procs; M8
// slice A: the reagent and stomach overrides (reagent_tick, reagent_expose, get_fullness, has_reagent). The carbon's own
// check_breath and handle_suffocation (a carbon without a species' lungs: monkeys, aliens) and the body temperature procs
// are M4b and later.

// tg: code/modules/mob/living/carbon/life.dm:68
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/life.dm:1
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = null;
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return dot;

        if (Dm.truthy(damageoverlaytemp))
        {
            damageoverlaytemp = 0;
            update_damage_hud();
        }

        foreach (datum_wound wound in Dm.each_cast<datum_wound>(all_wounds))
        {
            if (!Dm.truthy(wound.processes)) // meh
                continue;
            Waiting.NoSleep(wound.handle_process(seconds_per_tick));
        }

        if (HAS_TRAIT(this, TRAIT_STASIS))
        {
            dot = base.Life(seconds_per_tick, __extra);
            if (QDELETED(this))
                return dot;

            if (reagents != null)
                Waiting.NoSleep(((datum_reagents)reagents).handle_stasis_chems(this, seconds_per_tick));
        }
        else
        {
            //Reagent processing needs to come before breathing, to prevent edge cases.
            Waiting.NoSleep(handle_dead_metabolization(seconds_per_tick)); //Dead metabolization first since it can modify life metabolization.
            Waiting.NoSleep(handle_organs(seconds_per_tick));

            dot = base.Life(seconds_per_tick, __extra);
            if (QDELETED(this))
                return dot;

            if (Dm.truthy(dot)) //not dead
                Waiting.NoSleep(handle_blood(seconds_per_tick));

            if (stat != DEAD) // still not dead (blood could have changed that)
            {
                // tg: code/modules/mob/living/carbon/life.dm:32 (gaps RG2, D339: the addictions)
                foreach (object key in Dm.each(mind?.addiction_points))
                    Waiting.NoSleep(((datum_addiction)GLOB.addictions[key]).process_addiction(this, seconds_per_tick));
                Waiting.NoSleep(handle_brain_damage(seconds_per_tick));
            }
        }

        if (stat != DEAD)
            return TRUE;
        return dot;
    }

    ///////////////
    // BREATHING //
    ///////////////

    // Start of a breath chain, calls [carbon/proc/breathe()]
    // tg: code/modules/mob/living/carbon/life.dm:43
    public override async DmTask handle_breathing(object seconds_per_tick = null)
    {
        float next_breath = 4;
        obj_item_organ_lungs L = get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        obj_item_organ_heart H = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (L != null)
        {
            if (Dm.num(L.damage) > L.high_threshold)
                next_breath--;
        }
        if (H != null)
        {
            if (Dm.num(H.damage) > H.high_threshold)
                next_breath--;
        }

        if ((SSmobs.times_fired % next_breath) == 0 || Dm.truthy(failed_last_breath))
        {
            await breathe(seconds_per_tick); //Breathe per 4 ticks if healthy, down to 2 if our lungs or heart are damaged, unless suffocating
            if (Dm.truthy(failed_last_breath))
                await add_mood_event("suffocation", typeof(datum_mood_event_suffocation));
            else
                clear_mood_event("suffocation");
        }
        else
        {
            if (isobj(loc))
            {
                obj location_as_object = loc as obj;
                await location_as_object.handle_internal_lifeform(this, 0);
            }
        }
    }

    // tg: code/modules/mob/living/carbon/life.dm:68
    public virtual async DmTask breathe(object seconds_per_tick = null)
    {
        obj_item_organ lungs = get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ;
        float is_on_internals = FALSE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_CARBON_ATTEMPT_BREATHE]) ? NONE : _SendSignal(COMSIG_CARBON_ATTEMPT_BREATHE, Dm.list(this, seconds_per_tick))), COMSIG_CARBON_BLOCK_BREATH)))
            return;

        if (Dm.truthy(_listen_lookup?[COMSIG_CARBON_PRE_BREATHE]))
            _SendSignal(COMSIG_CARBON_PRE_BREATHE, Dm.list(this, seconds_per_tick));

        datum_gas_mixture environment = null;
        if (loc != null)
            environment = await loc.return_air() as datum_gas_mixture;

        datum_gas_mixture breath = null;

        if (Dm.truthy(Dm.bitand(lungs?.organ_flags, ORGAN_FAILING)))
            losebreath++;
        else if (!Dm.truthy(get_organ_slot(ORGAN_SLOT_BREATHING_TUBE)))
        {
            if (stat == HARD_CRIT || (pulledby != null && pulledby.grab_state >= GRAB_KILL))
                losebreath++; //You can't breath at all when in critical or when being choked, so you're going to miss a breath

            else if (stat == SOFT_CRIT)
                losebreath += 0.25f; //You're having trouble breathing in soft crit, so you'll miss a breath one in four times
        }

        //Suffocate
        if (losebreath >= 1) //You've missed a breath, take oxy damage
        {
            losebreath--;
            if (isobj(loc))
            {
                obj loc_as_obj = loc as obj;
                await loc_as_obj.handle_internal_lifeform(this, 0);
            }
        }

        else
        {
            //Breathe from internal
            object breath__internal = await get_breath_from_internal(BREATH_VOLUME);
            breath = breath__internal as datum_gas_mixture;

            if (breath__internal == null) //in case of 0 pressure internals
            {
                if (isobj(loc)) //Breathe from loc as object
                {
                    obj loc_as_obj = loc as obj;
                    breath = await loc_as_obj.handle_internal_lifeform(this, BREATH_VOLUME) as datum_gas_mixture;
                }

                else if (isturf(loc)) //Breathe from loc as turf
                {
                    float breath_moles = 0;
                    if (environment != null)
                        breath_moles = Dm.num(environment.total_moles()) * BREATH_PERCENTAGE;

                    breath = loc.remove_air(breath_moles) as datum_gas_mixture;
                }
            }
            else //Breathe from loc as obj again
            {
                is_on_internals = TRUE;

                if (isobj(loc))
                {
                    obj loc_as_obj = loc as obj;
                    await loc_as_obj.handle_internal_lifeform(this, 0);
                }
            }
        }

        if (Dm.truthy(await check_breath(breath)) && Dm.truthy(is_on_internals))
        {
            // successful breath from internals, try to play the breathing sound
            // DEVIATION(DV41): client?.prefs?.read_preference(/datum/preference/toggle/sound_breathing) is read as the
            // preference's default, TRUE (`/datum/preference/toggle`'s default_value, _preference.dm:561, sounds.dm:18): the preferences are
            // not translated (prefs is null, client/New's deferred leaf, M9), and the owner wants the loop now (D146).
            // tg: :119  if(!HAS_TRAIT(src, TRAIT_DEAF) && client?.prefs?.read_preference(/datum/preference/toggle/sound_breathing))
            if (!HAS_TRAIT(this, TRAIT_DEAF) && client != null)
                breathing_loop.start(); // tg: :120
        }

        if (breath != null)
            await loc.assume_air(breath);
    }

    // tg: code/modules/mob/living/carbon/life.dm:130
    public virtual object has_smoke_protection()
    {
        if (HAS_TRAIT(this, TRAIT_NOBREATH))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/life.dm:145
    public virtual async DmTask<object> check_breath(datum_gas_mixture breath = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/life.dm:145", "check_breath: not in M2 (M4b body temp; carbon check_breath (non-human) later; M8 liver/reagents)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/life.dm:401
    public virtual async DmTask<object> handle_suffocation(float? breath_pp = null, float? safe_breath_min = null, float? true_pp = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/life.dm:401", "handle_suffocation: not in M2 (M4b body temp; carbon check_breath (non-human) later; M8 liver/reagents)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/life.dm:431
    public virtual void handle_breath_temperature(datum_gas_mixture breath = null)
    {
        // The air you breathe out should match your body temperature
        breath.temperature = bodytemperature;
    }

    // tg: code/modules/mob/living/carbon/life.dm:436
    public virtual async DmTask<object> get_breath_from_internal(object volume_needed = null)
    {
        object dot = null;
        if (Dm.truthy(invalid_internals()))
        {
            // Unexpectely lost breathing apparatus and ability to breathe from the internal air tank.
            await cutoff_internals();
            return null;
        }
        if (external != null)
            dot = external.remove_air_volume(volume_needed);
        else if (@internal != null)
            dot = @internal.remove_air_volume(volume_needed);
        else
        {
            // Return without taking a breath if there is no air tank.
            return null;
        }
        // To differentiate between no internals and active, but empty internals.
        return (dot is var or__1 && Dm.truthy(or__1) ? or__1 : FALSE);
    }

    // tg: code/modules/mob/living/carbon/life.dm:451
    public virtual async DmTask handle_blood(object seconds_per_tick = null)
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/life.dm:454
    public override async DmTask<object> reagent_tick(datum_reagent chem = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.reagent_tick(chem, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)))
            return dot;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (blood_type == null)
            return dot;

        if (Dm.eq(chem.type, blood_type?.restoration_chem) && Dm.num(get_blood_volume()) < BLOOD_VOLUME_NORMAL)
        {
            // Don't clamp this to BLOOD_VOLUME_NORMAL. Reagents have quantization, making an clamped threshold janky.
            adjust_blood_volume(BLOOD_REGEN_FACTOR * Dm.num(seconds_per_tick));
            await ((datum_reagents)reagents).remove_reagent(chem.type, chem.metabolization_rate * Dm.num(seconds_per_tick));
            return COMSIG_MOB_STOP_REAGENT_TICK;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/life.dm:469
    public override async DmTask<object> reagent_expose(datum_reagent chem = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        object dot = await base.reagent_expose(chem, methods, reac_volume, show_message, touch_protection);

        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)))
            return dot;

        if (!Dm.truthy(Dm.bitand(methods, INJECT)) && !(Dm.truthy(Dm.bitand(methods, INGEST)) && HAS_TRAIT(this, TRAIT_DRINKS_BLOOD)))
            return dot;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (!Dm.eq(blood_type.reagent_type, chem.type))
            return dot;

        float cached_blood_volume = Dm.num(get_blood_volume());

        float blood_added = Dm.num(adjust_blood_volume(round(Dm.num(reac_volume), CHEMICAL_VOLUME_ROUNDING)));
        await ((datum_reagents)reagents).remove_reagent(chem.type, blood_added);

        DmList chem_data = chem.data as DmList;
        if (Dm.truthy(chem_data?[BLOOD_DATA_SYNTH_CONTENT]) && !IS_BLOOD_ALWAYS_SYNTHETIC(this))
        {
            float added_synth_volume = blood_added * Dm.num(chem_data[BLOOD_DATA_SYNTH_CONTENT]);
            float existing_synth_volume = cached_blood_volume * Dm.num(get_blood_synth_content());

            if (added_synth_volume != 0 || existing_synth_volume != 0)
            {
                // A simple weighted average that simplifies down to "total synth volume / total blood volume" i.e. "how much of our blood is synthetic"
                await AddComponent(typeof(datum_component_synth_blood), (added_synth_volume + existing_synth_volume) / (blood_added + cached_blood_volume));
            }
        }

        if (Dm.truthy(chem_data?[BLOOD_DATA_TYPE]))
        {
            datum_blood_type donor_type = chem_data[BLOOD_DATA_TYPE] as datum_blood_type;
            if (!Dm.@in(donor_type.type_key(), blood_type.compatible_types))
            {
                await ((datum_reagents)reagents).add_reagent(typeof(datum_reagent_toxin), Dm.num(reac_volume) * 0.5f);
                return COMPONENT_NO_EXPOSE_REAGENTS;
            }
        }

        return COMPONENT_NO_EXPOSE_REAGENTS;
    }

    // tg: code/modules/mob/living/carbon/life.dm:503
    public virtual async DmTask handle_organs(object seconds_per_tick = null)
    {
        if (stat == DEAD)
        {
            if (reagents != null && (Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_toxin_formaldehyde), 1)) || Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_cryostylane))))) // No organ decay if the body contains formaldehyde.
                return;
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(organs))
            {
                // On-death is where organ decay is handled
                if (organ?.owner != null) // organ + owner can be null due to reagent metabolization causing organ shuffling
                    await organ.on_death(seconds_per_tick);
                // We need to re-check the stat every organ, as one of our others may have revived us
                if (stat != DEAD)
                    break;
            }
            return;
        }

        // NOTE: organs_slot is sorted by GLOB.organ_process_order on insertion
        foreach (object slot in Dm.each(organs_slot))
        {
            // We don't use get_organ_slot here because we know we have the organ we want, since we're iterating the list containing em already
            // This code is hot enough that it's just not worth the time
            obj_item_organ organ = organs_slot[slot] as obj_item_organ;
            if (organ?.owner != null) // This exist mostly because reagent metabolization can cause organ reshuffling
                await organ.on_life(seconds_per_tick);
        }
    }

    // tg: code/modules/mob/living/carbon/life.dm:530
    public virtual object get_heart_blood_regeneration_multiplier()
    {
        if (!Dm.truthy(needs_heart()))
            return 1;
        obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart == null)
            return 0;

        return heart.get_blood_regeneration_multiplier();
    }

    // tg: code/modules/mob/living/carbon/life.dm:547
    public virtual async DmTask handle_dead_metabolization(object seconds_per_tick = null)
    {
        if (stat != DEAD)
            return;
        if (reagents != null)
            await ((datum_reagents)reagents).metabolize(this, seconds_per_tick, can_overdose: TRUE, liverless: TRUE, dead: TRUE); // Your liver doesn't work while you're dead.
    }

    /**
     * Used to stabilize the body temperature back to normal on living mobs
     *
     * Arguments:
     * - [environemnt][/datum/gas_mixture]: The environment gas mix
     * - seconds_per_tick: The amount of time that has elapsed since the last tick
     * - times_fired: The number of times SSmobs has ticked
     */
    // tg: code/modules/mob/living/carbon/life.dm:573
    public virtual async DmTask natural_bodytemperature_stabilization(datum_gas_mixture environment = null, object seconds_per_tick = null)
    {
        float areatemp = Dm.num(await get_temperature(environment));
        float body_temperature_difference = Dm.num(get_body_temp_normal()) - bodytemperature;
        float natural_change = 0;

        // We are very cold, increase body temperature
        if (bodytemperature <= BODYTEMP_COLD_DAMAGE_LIMIT)
        {
            natural_change = max((body_temperature_difference * metabolism_efficiency / BODYTEMP_AUTORECOVERY_DIVISOR),
                BODYTEMP_AUTORECOVERY_MINIMUM);
        }
        // we are cold, reduce the minimum increment and do not jump over the difference
        else if (bodytemperature > BODYTEMP_COLD_DAMAGE_LIMIT && bodytemperature < Dm.num(get_body_temp_normal()))
        {
            natural_change = max(body_temperature_difference * metabolism_efficiency / BODYTEMP_AUTORECOVERY_DIVISOR,
                min(body_temperature_difference, BODYTEMP_AUTORECOVERY_MINIMUM / 4));
        }
        // We are hot, reduce the minimum increment and do not jump below the difference
        else if (bodytemperature > Dm.num(get_body_temp_normal()) && bodytemperature <= BODYTEMP_HEAT_DAMAGE_LIMIT)
        {
            natural_change = min(body_temperature_difference * metabolism_efficiency / BODYTEMP_AUTORECOVERY_DIVISOR,
                max(body_temperature_difference, -(BODYTEMP_AUTORECOVERY_MINIMUM / 4)));
        }
        // We are very hot, reduce the body temperature
        else if (bodytemperature >= BODYTEMP_HEAT_DAMAGE_LIMIT)
            natural_change = min((body_temperature_difference / BODYTEMP_AUTORECOVERY_DIVISOR), -BODYTEMP_AUTORECOVERY_MINIMUM);

        float thermal_protection = 1 - Dm.num(get_insulation_protection(areatemp)); // invert the protection
        if (areatemp > bodytemperature) // It is hot here
        {
            if (bodytemperature < Dm.num(get_body_temp_normal()))
            {
                // Our bodytemp is below normal we are cold, insulation helps us retain body heat
                // and will reduce the heat we lose to the environment
                natural_change = (thermal_protection + 1) * natural_change;
            }
            else
            {
                // Our bodytemp is above normal and sweating, insulation hinders out ability to reduce heat
                // but will reduce the amount of heat we get from the environment
                natural_change = (1 / (thermal_protection + 1)) * natural_change;
            }
        }
        else // It is cold here
        {
            if (!Dm.truthy(on_fire)) // If on fire ignore ignore local temperature in cold areas
            {
                if (bodytemperature < Dm.num(get_body_temp_normal()))
                {
                    // Our bodytemp is below normal, insulation helps us retain body heat
                    // and will reduce the heat we lose to the environment
                    natural_change = (thermal_protection + 1) * natural_change;
                }
                else
                {
                    // Our bodytemp is above normal and sweating, insulation hinders out ability to reduce heat
                    // but will reduce the amount of heat we get from the environment
                    natural_change = (1 / (thermal_protection + 1)) * natural_change;
                }
            }
        }

        // Apply the natural stabilization changes
        adjust_bodytemperature(natural_change * Dm.num(seconds_per_tick));
    }

    /**
     * Get the insulation that is appropriate to the temperature you're being exposed to.
     * All clothing, natural insulation, and traits are combined returning a single value.
     *
     * required temperature The Temperature that you're being exposed to
     *
     * return the percentage of protection as a value from 0 - 1
    **/
    // tg: code/modules/mob/living/carbon/life.dm:629
    public virtual object get_insulation_protection(object temperature = null)
    {
        return (Dm.num(temperature) > bodytemperature) ? get_heat_protection(temperature) : get_cold_protection(temperature);
    }

    /// This returns the percentage of protection from heat as a value from 0 - 1
    /// temperature is the temperature you're being exposed to
    // tg: code/modules/mob/living/carbon/life.dm:634
    public virtual object get_heat_protection(object temperature = null)
    {
        return heat_protection;
    }

    /// This returns the percentage of protection from cold as a value from 0 - 1
    /// temperature is the temperature you're being exposed to
    // tg: code/modules/mob/living/carbon/life.dm:639
    public virtual object get_cold_protection(object temperature = null)
    {
        return cold_protection;
    }

    /**
     * Have two mobs share body heat between each other.
     * Account for the insulation and max temperature change range for the mob
     *
     * vars:
     * * M The mob/living/carbon that is sharing body heat
     */
    // tg: code/modules/mob/living/carbon/life.dm:649
    public virtual void share_bodytemperature(mob_living_carbon M = null)
    {
        float temp_diff = bodytemperature - M.bodytemperature;
        if (temp_diff > 0) // you are warm share the heat of life
        {
            M.adjust_bodytemperature((temp_diff * 0.5f), null, null, TRUE, TRUE); // warm up the giver (use_insulation, use_steps)
            adjust_bodytemperature((temp_diff * -0.5f), null, null, TRUE, TRUE); // cool down the reciver
        }
        else // they are warmer leech from them
        {
            adjust_bodytemperature((temp_diff * -0.5f), null, null, TRUE, TRUE); // warm up the reciver
            M.adjust_bodytemperature((temp_diff * 0.5f), null, null, TRUE, TRUE); // cool down the giver
        }
    }

    /**
     * Adjust the body temperature of a mob
     * expanded for carbon mobs allowing the use of insulation and change steps
     *
     * vars:
     * * amount The amount of degrees to change body temperature by
     * * min_temp (optional) The minimum body temperature after adjustment
     * * max_temp (optional) The maximum body temperature after adjustment
     * * use_insulation (optional) modifies the amount based on the amount of insulation the mob has
     * * use_steps (optional) Use the body temp divisors and max change rates
     * * capped (optional) default True used to cap step mode
     */
    // tg: code/modules/mob/living/carbon/life.dm:671  (the extra parameters use_insulation, use_steps and capped are the
    // overriding proc's own, read from __extra: rules 7.2)
    [DmParams("amount", "min_temp", "max_temp", "use_insulation", "use_steps", "capped")]
    public override object adjust_bodytemperature(object amount = null, float? min_temp = null, float? max_temp = null, params object[] __extra)
    {
        min_temp ??= 0;
        max_temp ??= INFINITY;
        object use_insulation = Dm.arg<object>(__extra, 0) ?? FALSE;
        object use_steps = Dm.arg<object>(__extra, 1) ?? FALSE;
        object capped = Dm.arg<object>(__extra, 2) ?? TRUE;
        if (HAS_TRAIT(this, TRAIT_HYPOTHERMIC) && Dm.num(amount) > 0) //Prevent warming up
            return null;
        // apply insulation to the amount of change
        if (Dm.truthy(use_insulation))
            amount = Dm.num(amount) * (1 - Dm.num(get_insulation_protection(bodytemperature + Dm.num(amount))));

        // Use the bodytemp divisors to get the change step, with max step size
        if (Dm.truthy(use_steps))
        {
            amount = (Dm.num(amount) > 0) ? (Dm.num(amount) / BODYTEMP_HEAT_DIVISOR) : (Dm.num(amount) / BODYTEMP_COLD_DIVISOR);
            // Clamp the results to the min and max step size
            if (Dm.truthy(capped))
                amount = (Dm.num(amount) > 0) ? min(Dm.num(amount), BODYTEMP_HEATING_MAX) : max(Dm.num(amount), BODYTEMP_COOLING_MAX);
        }

        if (bodytemperature >= Dm.num(min_temp) && bodytemperature <= Dm.num(max_temp))
            bodytemperature = clamp(bodytemperature + Dm.num(amount), Dm.num(min_temp), Dm.num(max_temp));
        return null;
    }

    ///////////
    //Stomach//
    ///////////

    // tg: code/modules/mob/living/carbon/life.dm:693
    public override object get_fullness(object only_consumable = null)
    {
        object dot = null;
        dot = base.get_fullness(only_consumable);

        obj_item_organ_stomach belly = get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        if (belly == null) //nothing to see here if we do not have a stomach
            return dot;

        foreach (datum_reagent bits in Dm.each_cast<datum_reagent>(((datum_reagents)belly.reagents).reagent_list))
        {
            // hack to get around stomachs having 5u stomach lining reagent ugugugu
            float effective_volume = Dm.num(bits.volume);
            if (Dm.truthy(belly.food_reagents[bits.type]))
                effective_volume -= Dm.num(belly.food_reagents[bits.type]);
            if (effective_volume <= 0)
                continue;
            if (bits is datum_reagent_consumable goodbit)
            {
                dot = Dm.num(dot) + Dm.num(goodbit.get_nutriment_factor(this)) * effective_volume / goodbit.metabolization_rate;
                continue;
            }
            if (!Dm.truthy(only_consumable))
                continue;
            dot = Dm.num(dot) + 0.6f * effective_volume / bits.metabolization_rate; //not food takes up space
        }

        return dot;
    }

    // tg: code/modules/mob/living/carbon/life.dm:717
    public override object has_reagent(object reagent = null, float? amount = null, float? needs_metabolizing = null)
    {
        amount ??= -1;
        needs_metabolizing ??= FALSE;
        object dot = base.has_reagent(reagent, amount, needs_metabolizing);
        if (Dm.truthy(dot))
            return dot;
        obj_item_organ_stomach belly = get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        if (belly == null)
            return FALSE;
        return ((datum_reagents)belly.reagents).has_reagent(reagent, amount, needs_metabolizing);
    }

    /////////
    //LIVER//
    /////////

    // tg: code/modules/mob/living/carbon/life.dm:732
    public virtual async DmTask handle_liver(object seconds_per_tick = null)
    {
        if (has_dna() == null)
            return;

        obj_item_organ_liver liver = get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null)
            return;

        await ((datum_reagents)reagents).end_metabolization(this, keep_liverless: TRUE); //Stops trait-based effects on reagents, to prevent permanent buffs
        await ((datum_reagents)reagents).metabolize(this, seconds_per_tick, can_overdose: TRUE, liverless: TRUE);

        if (HAS_TRAIT(this, TRAIT_STABLELIVER) || HAS_TRAIT(this, TRAIT_LIVERLESS_METABOLISM))
            return;

        await adjust_tox_loss(0.6f * Dm.num(seconds_per_tick), forced: TRUE); //liver failure should kill slimepeople (or any human mob with toxin-lover or toxin-immune traits)
        await adjust_organ_loss(pick(ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_STOMACH, ORGAN_SLOT_EYES, ORGAN_SLOT_EARS), 0.5f * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/mob/living/carbon/life.dm:749
    public virtual object undergoing_liver_failure()
    {
        obj_item_organ_liver liver = get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (Dm.truthy(Dm.bitand(liver?.organ_flags, ORGAN_FAILING)))
            return TRUE;
        return null;
    }

    // tg: code/modules/mob/living/carbon/life.dm:758
    public virtual async DmTask handle_brain_damage(object seconds_per_tick = null)
    {
        foreach (object T in Dm.each(get_traumas()))
        {
            datum_brain_trauma BT = T as datum_brain_trauma;
            await BT.on_life(seconds_per_tick);
        }
    }

    // tg: code/modules/mob/living/carbon/life.dm:767
    public virtual object can_heartattack()
    {
        if (!Dm.truthy(needs_heart()))
            return FALSE;
        obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart == null || Dm.truthy(IS_ROBOTIC_ORGAN(heart)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/life.dm:775
    public virtual object needs_heart()
    {
        if (HAS_TRAIT(this, TRAIT_STABLEHEART))
            return FALSE;
        if (dna != null && dna.species != null && (!Dm.truthy(CAN_HAVE_BLOOD(this)) || ((datum_species)dna.species).mutantheart == null)) //not all carbons have species!
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/life.dm:789
    public virtual object undergoing_cardiac_arrest()
    {
        obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart != null && Dm.truthy(heart.is_beating()))
            return FALSE;
        else if (!Dm.truthy(needs_heart()))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/life.dm:804
    public virtual object set_heartattack(object status = null)
    {
        if (Dm.truthy(status) && !Dm.truthy(can_heartattack()))
            return FALSE;

        obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (heart == null)
            return FALSE;

        if (Dm.truthy(status))
            return heart.Stop();

        return heart.Restart();
    }
}
