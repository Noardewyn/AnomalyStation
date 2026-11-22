// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/heal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the healing symptoms (starlight condensation, toxolysis, metabolic boost, nocturnal regeneration,
// regenerative coma, tissue hydration, plasma fixation, radioactive resonance, aggressive healing, mutated regeneration). The var
// blocks are generated (D111).

// tg: code/datums/diseases/advance/symptoms/heal.dm:1
public partial class datum_symptom_heal : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:16
    [DmParams("our_disease")]
    public override async DmTask<object> Activate(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = await base.Activate(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        healable_bodytypes = BODYTYPE_ORGANIC;
        if (Dm.truthy(Dm.bitand(our_disease.infectable_biotypes, MOB_ROBOTIC)))
            Dm.combine(ref healable_bodytypes, BODYTYPE_ROBOTIC); // Inorganic biology lets us heal more bodytypes
        if (Dm.truthy(Dm.bitand(our_disease.infectable_biotypes, MOB_MINERAL)))
            Dm.combine(ref healable_bodytypes, BODYTYPE_GOLEM);
        mob_living living_host = our_disease.affected_mob;
        if (our_disease.stage == 4 || our_disease.stage == 5)
        {
            object effectiveness = await CanHeal(our_disease);
            if (!Dm.truthy(effectiveness))
            {
                if (Dm.truthy(passive_message) && prob(2) && Dm.truthy(passive_message_condition(living_host)))
                    to_chat(living_host, I18n.T(Dm.str(passive_message)));
                return dot;
            }
            else
                await Heal(living_host, our_disease, effectiveness);
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:37
    public virtual async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        return power;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:40
    public virtual async DmTask<object> Heal(mob_living living_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:43
    public virtual object passive_message_condition(mob_living living_host = null)
    {
        return TRUE;
    }
}

/*Starlight Condensation
 * Slightly reduces stealth
 * Reduces resistance
 * No change to stage speed
 * Slightly increases transmissibility
 * Bonus: Heals host when exposed to starlight
*/
// tg: code/datums/diseases/advance/symptoms/heal.dm:54
public partial class datum_symptom_heal_starlight : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:68-71
    private const float STARLIGHT_CAN_HEAL = 2;
    private const float STARLIGHT_CAN_HEAL_WITH_PENALTY = 1;
    private const float STARLIGHT_CANNOT_HEAL = 0;
    private const float STARLIGHT_MAX_RANGE = 2;

    // tg: code/datums/diseases/advance/symptoms/heal.dm:74
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalTransmittable()) >= 6)
            nearspace_penalty = 1;
        if (Dm.num(our_disease.totalStageSpeed()) >= 6)
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:83
    public virtual object CanTileHealDirectional(turf turf_to_check = null, object direction = null)
    {
        if (Dm.eq(direction, UP))
        {
            turf_to_check = GET_TURF_ABOVE(turf_to_check) as turf;
            if (turf_to_check == null)
                return STARLIGHT_CANNOT_HEAL;
        }
        area area_to_check = get_area(turf_to_check) as area;
        float levels_of_glass = 0; // Since starlight condensation only works 2 tiles to the side anyways, it shouldn't work with like 100 z-levels of glass
        while (levels_of_glass <= STARLIGHT_MAX_RANGE)
        {
            // Outdoors covers lavaland and unroofed areas but with tiles under,
            // while space covers normal space and those caused by explosions,
            // if there is a floor tile when checking above, that means
            // a roof exists so the outdoors should only work downwards
            if (isspaceturf(turf_to_check) || (Dm.truthy(area_to_check.outdoors) && Dm.eq(direction, DOWN)))
            {
                if (Dm.truthy(levels_of_glass))
                    return STARLIGHT_CAN_HEAL_WITH_PENALTY; // Glass gives a penalty.
                return STARLIGHT_CAN_HEAL; // No glass = can heal fully.
            }

            // Our turf is transparent, but it's NOT openspace - it's something like glass which reduces power
            if (istransparentturf(turf_to_check) && !(turf_to_check is turf_open_openspace))
                levels_of_glass += 1;

            // Our turf is transparent OR openspace - we can check higher or lower z-levels
            if (istransparentturf(turf_to_check) || turf_to_check is turf_open_openspace)
            {
                // Check above or below us
                if (Dm.eq(direction, UP))
                    turf_to_check = GET_TURF_ABOVE(turf_to_check) as turf;
                else
                    turf_to_check = GET_TURF_BELOW(turf_to_check) as turf;

                // If we found a turf above or below us,
                // then we can rerun the loop on the newly found turf / area
                // (Probably, with +1 to levels_of_glass)
                if (turf_to_check != null)
                {
                    area_to_check = get_area(turf_to_check) as area;
                    continue;
                }

                // If we didn't find a turf above or below us -
                // Checking below, we assume that space is below us (as we're standing on station)
                // Checking above, we check that the area is "outdoors" before assuming if it is space or not.
                else
                {
                    if (Dm.eq(direction, DOWN) || (Dm.eq(direction, UP) && Dm.truthy(area_to_check.outdoors)))
                    {
                        if (Dm.truthy(levels_of_glass))
                            return STARLIGHT_CAN_HEAL_WITH_PENALTY;
                        return STARLIGHT_CAN_HEAL;
                    }
                }
            }

            return STARLIGHT_CANNOT_HEAL; // Hit a non-space, Non-transparent turf - no healsies
        }
        return null;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:130
    public virtual object CanTileHeal(turf original_turf = null, object satisfied_with_penalty = null)
    {
        object current_heal_level = CanTileHealDirectional(original_turf, DOWN);
        if (Dm.eq(current_heal_level, STARLIGHT_CAN_HEAL))
            return current_heal_level;
        if (Dm.truthy(current_heal_level) && Dm.truthy(satisfied_with_penalty)) // do not care if there is a healing penalty or no
            return current_heal_level;
        object heal_level_from_above = CanTileHealDirectional(original_turf, UP);
        if (Dm.gt(heal_level_from_above, current_heal_level))
            return heal_level_from_above;
        else
            return current_heal_level;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:142
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        mob_living living_host = our_disease.affected_mob;
        turf turf_of_mob = get_turf(living_host);
        object sw__1 = CanTileHeal(turf_of_mob, FALSE);
        if (Dm.eq(sw__1, STARLIGHT_CAN_HEAL_WITH_PENALTY))
            return Dm.num(power) * nearspace_penalty;
        else if (Dm.eq(sw__1, STARLIGHT_CAN_HEAL))
            return power;
        foreach (turf turf_to_check in Dm.each<turf>(view(living_host, STARLIGHT_MAX_RANGE)))
        {
            if (Dm.truthy(CanTileHeal(turf_to_check, TRUE)))
                return Dm.num(power) * nearspace_penalty;
        }
        return null;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:159
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        object heal_amt = actual_power;
        if (Dm.truthy(carbon_host.get_tox_loss()) && prob(5))
            to_chat(carbon_host, span_notice(I18n.T("Your skin tingles as the starlight seems to heal you.")));
        object needs_update = FALSE;
        needs_update = Dm.add(needs_update, await carbon_host.adjust_tox_loss(-4 * Dm.num(heal_amt), updating_health: FALSE, required_biotype: healable_bodytypes)); // Most effective on toxins
        needs_update = Dm.add(needs_update, await carbon_host.heal_overall_damage(Dm.num(heal_amt), Dm.num(heal_amt), required_bodytype: healable_bodytypes, updating_health: FALSE));
        if (Dm.truthy(needs_update))
            await carbon_host.updatehealth();
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:170
    public override object passive_message_condition(mob_living living_host = null)
    {
        if (Dm.truthy(living_host.get_brute_loss()) || Dm.truthy(living_host.get_fire_loss()) || Dm.truthy(living_host.get_tox_loss()))
            return TRUE;
        return FALSE;
    }
}

/*Toxolysis
 * No change to stealth
 * Reduces resistance
 * Increases stage speed
 * Reduces transmissibility
 * Bonus: Removes all reagents from the host
*/
// tg: code/datums/diseases/advance/symptoms/heal.dm:182
public partial class datum_symptom_heal_chem : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:197
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 6)
            food_conversion = TRUE;
        if (Dm.num(our_disease.totalResistance()) >= 7)
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:206
    public override async DmTask<object> Heal(mob_living living_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        foreach (datum_reagent each_reagent in Dm.each<datum_reagent>(((datum_reagents)living_host.reagents).reagent_list)) //Not just toxins!
        {
            object food = await ((datum_reagents)living_host.reagents).remove_reagent(each_reagent.type, actual_power);
            if (Dm.truthy(food_conversion))
                await living_host.adjust_nutrition(0.3f * Dm.num(food));
            if (prob(2))
                to_chat(living_host, span_notice(I18n.T("You feel a mild warmth as your blood purifies itself.")));
        }
        return TRUE;
    }
}

/*Metabolic Boost
 * Slightly reduces stealth
 * Reduces resistance
 * Increases stage speed
 * Slightly increases transmissibility
 * Bonus: Doubles the rate of chemical metabolisation
 * Increases nutrition loss rate
*/
// tg: code/datums/diseases/advance/symptoms/heal.dm:224
public partial class datum_symptom_heal_metabolism : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:241
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 10)
            triple_metabolism = TRUE;
        if (Dm.num(our_disease.totalStealth()) >= 3)
            reduced_hunger = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:250
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float metabolic_boost = Dm.truthy(triple_metabolism) ? 2 : 1;
        // tg passes times_fired 0 before can_overdose; the port's metabolize has no times_fired (it is unused there)
        await ((datum_reagents)carbon_host.reagents).metabolize(carbon_host as mob_living_carbon, metabolic_boost * SSMOBS_DT(), can_overdose: TRUE); //this works even without a liver; it's intentional since the virus is metabolizing by itself
        carbon_host.overeatduration = max(carbon_host.overeatduration - 4 * SECONDS, 0);
        float lost_nutrition = 9 - (Dm.num(reduced_hunger) * 5);
        await carbon_host.adjust_nutrition(-lost_nutrition * HUNGER_FACTOR); //Hunger depletes at 10x the normal speed
        if (prob(2))
            to_chat(carbon_host, span_notice(I18n.T("You feel an odd gurgle in your stomach, as if it was working much faster than normal.")));
        return TRUE;
    }
}

/*Nocturnal Regeneration
 * Increases stealth
 * Slightly reduces resistance
 * Reduces stage speed
 * Slightly reduces transmissibility
 * Bonus: Heals brute damage when in the dark
*/
// tg: code/datums/diseases/advance/symptoms/heal.dm:267
public partial class datum_symptom_heal_darkness : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:280
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 8)
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:287
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        mob_living living_host = our_disease.affected_mob;
        float light_amount = 0;
        if (isturf(living_host.loc)) //else, there's considered to be no light
        {
            turf host_turf = (turf)living_host.loc;
            light_amount = min(1, Dm.num(host_turf.get_lumcount())) - 0.5f;
            if (light_amount < SHADOW_SPECIES_LIGHT_THRESHOLD)
                return power;
        }
        return null;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:296
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float heal_amt = 2 * Dm.num(actual_power);
        await carbon_host.heal_overall_damage(heal_amt, heal_amt * 0.5f, required_bodytype: healable_bodytypes);
        if (prob(5))
            to_chat(carbon_host, span_notice(I18n.T("The darkness soothes and mends your wounds.")));
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:303
    public override object passive_message_condition(mob_living living_host = null)
    {
        if (Dm.truthy(living_host.get_brute_loss()) || Dm.truthy(living_host.get_fire_loss()))
            return TRUE;
        return FALSE;
    }
}

/*Regen Coma
 * No effect on stealth
 * Increases resistance
 * Reduces stage speed greatly
 * Decreases transmissibility
 * Bonus: Puts the host into a coma when severely hurt, healing them
*/
// tg: code/datums/diseases/advance/symptoms/heal.dm:315
public partial class datum_symptom_heal_coma : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:333
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 7)
            power = 1.5f;
        if (Dm.num(our_disease.totalResistance()) >= 4)
            stabilize = TRUE;
        if (Dm.num(our_disease.totalStealth()) >= 2)
            deathgasp = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:344
    [DmParams("our_disease")]
    public override object on_stage_change(datum_disease_advance our_disease = null) //mostly copy+pasted from the code for self-respiration's TRAIT_NOBREATH stuff
    {
        object dot = null;
        dot = base.on_stage_change(our_disease);
        if (!Dm.truthy(dot))
            return FALSE;
        if (our_disease.stage >= 4 && Dm.truthy(stabilize))
            ADD_TRAIT(our_disease.affected_mob, TRAIT_NOCRITDAMAGE, DISEASE_TRAIT);
        else
            REMOVE_TRAIT(our_disease.affected_mob, TRAIT_NOCRITDAMAGE, DISEASE_TRAIT);
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:354
    [DmParams("our_disease")]
    public override async DmTask<object> End(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = await base.End(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(active_coma))
            uncoma();
        REMOVE_TRAIT(our_disease.affected_mob, TRAIT_NOCRITDAMAGE, DISEASE_TRAIT);
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:362
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        mob_living living_host = our_disease.affected_mob;
        if (HAS_TRAIT(living_host, TRAIT_DEATHCOMA))
            return power;
        if (Dm.truthy(living_host.IsSleeping()))
            return Dm.num(power) * 0.25f; //Voluntary unconsciousness yields lower healing.
        if (living_host.stat == HARD_CRIT)
            return Dm.num(power) * 0.9f;
        else if (living_host.stat == SOFT_CRIT)
            return Dm.num(power) * 0.5f;
        if (Dm.num(living_host.get_brute_loss()) + Dm.num(living_host.get_fire_loss()) >= living_host.maxHealth * 0.7f && !Dm.truthy(active_coma) && !(HAS_TRAIT(living_host, TRAIT_NOSOFTCRIT)))
        {
            to_chat(living_host, span_warning(I18n.T("You feel yourself slip into a regenerative coma...")));
            active_coma = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(coma), living_host), 6 * SECONDS, file: "code/datums/diseases/advance/symptoms/heal.dm", line: 377);
        }
        return null;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:379
    public virtual async DmTask coma(mob_living living_host = null)
    {
        if (QDELETED(living_host) || living_host.stat == DEAD || HAS_TRAIT(living_host, TRAIT_NOSOFTCRIT))
            return;
        await living_host.fakedeath("regenerative_coma", Dm.num(!Dm.truthy(deathgasp)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(uncoma), living_host), 30 * SECONDS, file: "code/datums/diseases/advance/symptoms/heal.dm", line: 383);
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:385
    public virtual void uncoma(mob_living living_host = null)
    {
        if (QDELETED(living_host) || !Dm.truthy(active_coma))
            return;
        active_coma = FALSE;
        living_host.cure_fakedeath("regenerative_coma");
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:392
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float heal_amt = 4 * Dm.num(actual_power);
        await carbon_host.heal_overall_damage(heal_amt, heal_amt, required_bodytype: healable_bodytypes);
        if (Dm.truthy(active_coma) && Dm.num(carbon_host.get_brute_loss()) + Dm.num(carbon_host.get_fire_loss()) == 0)
            uncoma(carbon_host);
        return 1;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:399
    public override object passive_message_condition(mob_living living_host = null)
    {
        if ((Dm.num(living_host.get_brute_loss()) + Dm.num(living_host.get_fire_loss())) > living_host.maxHealth * 0.3f)
            return TRUE;
        return FALSE;
    }
}

// tg: code/datums/diseases/advance/symptoms/heal.dm:404
public partial class datum_symptom_heal_water : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:420
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 7)
            power = 2;
        if (Dm.num(our_disease.totalResistance()) >= 5)
            absorption_coeff = 0.25f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:429
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        float dot = 0;
        mob_living_carbon carbon_host = our_disease.affected_mob;
        if (carbon_host.fire_stacks < 0)
        {
            await carbon_host.adjust_fire_stacks(min(absorption_coeff, -carbon_host.fire_stacks));
            dot += Dm.num(power);
        }
        if (Dm.truthy(((datum_reagents)carbon_host.reagents).has_reagent(typeof(datum_reagent_water_holywater), needs_metabolizing: FALSE)))
        {
            await ((datum_reagents)carbon_host.reagents).remove_reagent(typeof(datum_reagent_water_holywater), 0.5f * absorption_coeff);
            dot += Dm.num(power) * 0.75f;
        }
        else if (Dm.truthy(((datum_reagents)carbon_host.reagents).has_reagent(typeof(datum_reagent_water), needs_metabolizing: FALSE)))
        {
            await ((datum_reagents)carbon_host.reagents).remove_reagent(typeof(datum_reagent_water), 0.5f * absorption_coeff);
            dot += Dm.num(power) * 0.5f;
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:443
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float heal_amt = 2 * Dm.num(actual_power);
        if (Dm.truthy(await carbon_host.heal_overall_damage(heal_amt * 0.5f, heal_amt, required_bodytype: healable_bodytypes)) && prob(5))
            to_chat(carbon_host, span_notice(I18n.T("You feel yourself absorbing the water around you to soothe your damaged skin.")));
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:449
    [DmParams("carbon_host")]
    public override object passive_message_condition(mob_living carbon_host = null)
    {
        if (Dm.truthy(carbon_host.get_brute_loss()) || Dm.truthy(carbon_host.get_fire_loss()))
            return TRUE;
        return FALSE;
    }
}

// tg: code/datums/diseases/advance/symptoms/heal.dm:468
public partial class datum_symptom_heal_plasma : datum_symptom_heal
{
    /// Determines the rate at which Plasma Fixation heals based on the amount of plasma in the air
    // tg: code/datums/diseases/advance/symptoms/heal.dm:455
    private const float HEALING_PER_MOL = 1.1f;
    /// Determines the rate at which Plasma Fixation heals based on the amount of plasma being breathed through internals
    private const float HEALING_PER_BREATH_PRESSURE = 0.05f;
    /// Determines the highest amount you can be healed for when breathing plasma from internals
    private const float MAX_HEAL_COEFFICIENT_INTERNALS = 0.75f;
    /// Determines the highest amount you can be healed for from pulling plasma from the environment
    private const float MAX_HEAL_COEFFICIENT_ENVIRONMENT = 0.5f;
    /// Determines the highest amount you can be healed for when there is plasma in the bloodstream
    private const float MAX_HEAL_COEFFICIENT_BLOODSTREAM = 0.75f;
    /// This is the base heal amount before being multiplied by the healing coefficients
    private const float BASE_HEAL_PLASMA_FIXATION = 4;

    // tg: code/datums/diseases/advance/symptoms/heal.dm:484
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalStageSpeed()) >= 7)
            power = 2;
        if (Dm.num(our_disease.totalTransmittable()) >= 6)
            temp_rate = 4;
        return dot;
    }

    // We do this to prevent liver damage from injecting plasma when plasma fixation virus reaches stage 4 and beyond
    // tg: code/datums/diseases/advance/symptoms/heal.dm:494
    [DmParams("our_disease")]
    public override object on_stage_change(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.on_stage_change(our_disease);
        if (!Dm.truthy(dot))
            return FALSE;
        if (our_disease.stage >= 4)
            ADD_TRAIT(our_disease.affected_mob, TRAIT_PLASMA_LOVER_METABOLISM, DISEASE_TRAIT);
        else
            REMOVE_TRAIT(our_disease.affected_mob, TRAIT_PLASMA_LOVER_METABOLISM, DISEASE_TRAIT);
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:505
    [DmParams("our_disease")]
    public override async DmTask<object> End(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = await base.End(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(our_disease.affected_mob, TRAIT_PLASMA_LOVER_METABOLISM, DISEASE_TRAIT);
        return dot;
    }

    // Check internals breath, environmental plasma, and plasma in bloodstream to determine the heal power
    // tg: code/datums/diseases/advance/symptoms/heal.dm:513
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        mob_living_carbon carbon_host = our_disease.affected_mob;
        datum_gas_mixture environment = null;
        float dot = 0;
        // Check internals
        ///  the amount of mols in a breath is significantly lower than in the environment so we are just going to use the tank's
        ///  distribution pressure as an abstraction rather than calculate it using the ideal gas equation.
        ///  balanced around a tank set to 4kpa = about 0.2 healing power. maxes out at 0.75 healing power, or 15kpa.
        obj_item_tank_internals internals_tank = carbon_host.@internal as obj_item_tank_internals;
        if (internals_tank != null)
        {
            datum_gas_mixture tank_contents = await internals_tank.return_air() as datum_gas_mixture;
            if (tank_contents != null && Dm.truthy(round(Dm.num(tank_contents.return_pressure())))) // make sure the tank is not empty or 0 pressure
            {
                if (Dm.truthy(tank_contents.moles[typeof(datum_gas_plasma)]))
                    // higher tank distribution pressure leads to more healing, but once you get to about 15kpa you reach the max
                    dot += Dm.num(power) * min(MAX_HEAL_COEFFICIENT_INTERNALS, internals_tank.distribute_pressure * HEALING_PER_BREATH_PRESSURE);
            }
        }
        else // Check environment
        {
            if (carbon_host.loc != null)
                environment = await ((atom)carbon_host.loc).return_air() as datum_gas_mixture;
            if (environment != null && Dm.truthy(environment.moles[typeof(datum_gas_plasma)]))
                dot += Dm.num(power) * min(MAX_HEAL_COEFFICIENT_INTERNALS, Dm.num(environment.moles[typeof(datum_gas_plasma)]) * HEALING_PER_MOL);
        }
        // Check for reagents in bloodstream
        if (Dm.truthy(((datum_reagents)carbon_host.reagents).has_reagent(typeof(datum_reagent_toxin_plasma), needs_metabolizing: TRUE)))
            dot += Dm.num(power) * MAX_HEAL_COEFFICIENT_BLOODSTREAM; //Determines how much the symptom heals if injected or ingested
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:539
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float heal_amt = BASE_HEAL_PLASMA_FIXATION * Dm.num(actual_power);
        if (prob(5))
            to_chat(carbon_host, span_notice(I18n.T("You feel yourself absorbing plasma inside and around you...")));
        float difference = Dm.num(carbon_host.get_body_temp_normal()) - carbon_host.bodytemperature;
        if (prob(5))
        {
            if (difference > -1) // Yes, it's supposed to be -1 and not 0. Probably so you keep getting passive messages even at normal temperature.
                to_chat(carbon_host, span_notice(I18n.T("You feel warmer.")));
            if (difference < 0)
                to_chat(carbon_host, span_notice(I18n.T("You feel less hot.")));
        }
        carbon_host.adjust_bodytemperature(clamp(difference, -20 * temp_rate, 20 * temp_rate));
        object needs_update = FALSE;
        needs_update = Dm.add(needs_update, await carbon_host.adjust_tox_loss(-heal_amt, updating_health: FALSE, required_biotype: healable_bodytypes));
        object brute_burn_heal = await carbon_host.heal_overall_damage(heal_amt, heal_amt, required_bodytype: healable_bodytypes, updating_health: FALSE);
        needs_update = Dm.add(needs_update, brute_burn_heal);
        if (Dm.truthy(brute_burn_heal) && prob(5))
            to_chat(carbon_host, span_notice(I18n.T("The pain from your wounds fades rapidly.")));
        if (Dm.truthy(needs_update))
            await carbon_host.updatehealth();
        return TRUE;
    }
}

// tg: code/datums/diseases/advance/symptoms/heal.dm:570
public partial class datum_symptom_heal_radiation : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:583
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalResistance()) >= 7)
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:590
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        return HAS_TRAIT(our_disease.affected_mob, TRAIT_IRRADIATED) ? power : 0;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:593
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        float heal_amt = Dm.num(actual_power);
        object needs_update = FALSE;
        needs_update = Dm.add(needs_update, await carbon_host.adjust_tox_loss(-2 * heal_amt, updating_health: FALSE));
        object brute_burn_heal = await carbon_host.heal_overall_damage(heal_amt, heal_amt, required_bodytype: healable_bodytypes, updating_health: FALSE);
        needs_update = Dm.add(needs_update, brute_burn_heal);
        if (Dm.truthy(needs_update))
            await carbon_host.updatehealth();
        if (Dm.truthy(brute_burn_heal) && prob(4))
            to_chat(carbon_host, span_notice(I18n.T("Your skin glows faintly, and you feel your wounds mending themselves.")));
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:605
    public override object can_generate_randomly()
    {
        return Dm.truthy(base.can_generate_randomly()) && !HAS_TRAIT(SSstation, STATION_TRAIT_RADIOACTIVE_NEBULA) ? TRUE : FALSE; // Because people can never really suffer enough
    }
}

// tg: code/datums/diseases/advance/symptoms/heal.dm:608
public partial class datum_symptom_heal_aggressive_healing : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:626
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        return Dm.num(power) + Dm.num(our_disease.totalSeverity()) * severity_heal_bonus;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:629
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        await carbon_host.heal_overall_damage(Dm.num(actual_power), Dm.num(actual_power), required_bodytype: healable_bodytypes);
        return TRUE;
    }
}

// tg: code/datums/diseases/advance/symptoms/heal.dm:633
public partial class datum_symptom_heal_genetic : datum_symptom_heal
{
    // tg: code/datums/diseases/advance/symptoms/heal.dm:648
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalResistance()) >= 9)
            power = 0.2f;
        if (Dm.num(our_disease.totalStageSpeed()) >= 6)
            stability_divisor = 12.5f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:657
    public override async DmTask<object> CanHeal(datum_disease_advance our_disease = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(our_disease.affected_mob.has_dna()))
            return power;
        float dna_stability = our_disease.affected_mob.dna.stability;
        if (dna_stability >= Dm.num(Dm.initial(our_disease.affected_mob.dna, (datum_dna p) => p.stability)))
            return Dm.num(power) + (dna_stability - Dm.num(Dm.initial(our_disease.affected_mob.dna, (datum_dna p) => p.stability))) / stability_divisor;
        return null;
    }

    // tg: code/datums/diseases/advance/symptoms/heal.dm:664
    [DmParams("carbon_host", "our_disease", "actual_power")]
    public override async DmTask<object> Heal(mob_living carbon_host = null, datum_disease_advance our_disease = null, object actual_power = null)
    {
        if (Dm.truthy(await carbon_host.heal_overall_damage(Dm.num(actual_power), Dm.num(actual_power), required_bodytype: healable_bodytypes)) && prob(5))
            to_chat(carbon_host, span_notice(I18n.T("You feel your injuries slowly fading away.")));
        return TRUE;
    }
}
