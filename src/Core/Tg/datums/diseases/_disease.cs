// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/_disease.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): infection, the spread by air and by contact, the stages, the cures by reagents, the natural
// recovery with the sleep and spaceacillin bonuses, the immunity after a cure, the carrier state and the copies. The var block is
// generated (D111). Deviation: airborne_spread's mouth check is inverted at the snapshot (DV558).

// tg: code/datums/diseases/_disease.dm:1
public partial class datum_disease : datum
{
    // tg: code/datums/diseases/_disease.dm:62
    public override object Destroy(float? force = null)
    {
        object dot = null;
        dot = base.Destroy(force);
        if (affected_mob != null)
            remove_disease();
        SSdisease?.active_diseases?.Remove(this); // rules 10.6: a harness world may lack SSdisease
        return dot;
    }

    //add this disease if the host does not already have too many
    // tg: code/datums/diseases/_disease.dm:69
    public virtual async DmTask<object> try_infect(mob_living infectee = null, float? make_copy = null)
    {
        make_copy ??= TRUE;
        await infect(infectee, make_copy);
        return TRUE;
    }

    //add the disease with no checks
    // tg: code/datums/diseases/_disease.dm:74
    public virtual async DmTask infect(mob_living infectee = null, float? make_copy = null)
    {
        make_copy ??= TRUE;
        datum_disease D = Dm.truthy(make_copy) ? (datum_disease)await Copy() : this;
        LAZYADD(ref infectee.diseases, D);
        D.affected_mob = (mob_living_carbon)infectee;
        if (SSdisease != null) // rules 10.6: a harness world may lack SSdisease
            Dm.append(SSdisease.active_diseases, D); //Add it to the active diseases list, now that it's actually in a mob and being processed.

        await D.after_add();
        D.register_disease_signals();
        infectee.med_hud_set_status();

        turf source_turf = get_turf(infectee);
        log_virus(Dm.text("[] was infected by virus: [] at []", key_name(infectee), admin_details(), loc_name(source_turf)));

        if (Dm.ge(severity, DISEASE_SEVERITY_BIOHAZARD) && SSshuttle != null)
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_TOMBSTONE] = TRUE;
    }

    /// Updates the spread flags set, ensuring signals are updated as necessary
    // tg: code/datums/diseases/_disease.dm:91
    public virtual void update_spread_flags(object new_flags = null)
    {
        if (Dm.eq(spread_flags, new_flags))
            return;

        spread_flags = Dm.num(new_flags);
        unregister_disease_signals();
        register_disease_signals();
    }

    /// Register any relevant signals for the disease
    // tg: code/datums/diseases/_disease.dm:100
    public virtual void register_disease_signals()
    {
        if (affected_mob == null)
            return;
        RegisterSignal(affected_mob, COMSIG_LIVING_LIFE, nameof(on_life));
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_AIRBORNE)))
            RegisterSignal(affected_mob, COMSIG_CARBON_PRE_BREATHE, nameof(on_breath));
    }

    /// Unregister any relevant signals for the disease
    // tg: code/datums/diseases/_disease.dm:108
    public virtual void unregister_disease_signals()
    {
        if (affected_mob == null)
            return;
        UnregisterSignal(affected_mob, Dm.list(COMSIG_LIVING_LIFE, COMSIG_CARBON_PRE_BREATHE));
    }

    // Proc to determine if the virus can resist natural recovery
    // tg: code/datums/diseases/_disease.dm:114
    public virtual object get_recovery_failure_chance()
    {
        return 0;
    }

    // tg: code/datums/diseases/_disease.dm:117
    [SignalHandler]
    public virtual void on_life(datum source = null, object seconds_per_tick = null)
    {
        // PRIVATE_PROC(TRUE)

        if (HAS_TRAIT(affected_mob, TRAIT_STASIS) || QDELETED(this) || (affected_mob.stat == DEAD && !Dm.truthy(process_dead)))
            return;

        Waiting.NoSleep(stage_act(seconds_per_tick));
    }

    ///Proc to process the disease and decide on whether to advance, cure or make the symptoms appear. Returns a boolean on whether to continue acting on the symptoms or not.
    // tg: code/datums/diseases/_disease.dm:127
    public virtual async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        float slowdown = HAS_TRAIT(affected_mob, TRAIT_VIRUS_RESISTANCE) ? 0.5f : 1; // spaceacillin slows stage speed by 50%
        float recovery_prob = 0;
        float cure_mod;
        float bad_immune = HAS_TRAIT(affected_mob, TRAIT_IMMUNODEFICIENCY) ? 2 : 1;
        bool is_sleeping = Dm.truthy(affected_mob.IsSleeping());
        bool malnourished = !HAS_TRAIT(affected_mob, TRAIT_NOHUNGER) && (affected_mob.nutrition <= NUTRITION_LEVEL_STARVING);

        if (Dm.truthy(required_organ))
        {
            if (!Dm.truthy(has_required_infectious_organ(affected_mob, required_organ)))
            {
                await cure(add_resistance: FALSE);
                return FALSE;
            }
        }

        object cure_status = has_cure();
        if (Dm.truthy(cure_status))
        {
            cure_mod = cure_chance / bad_immune;
            if (this is datum_disease_advance)
                cure_mod = cure_mod * 2 * Dm.num(cure_status); // Advanced diseases can be cured up to 2x as fast if all symptoms are remedied
            if (Dm.truthy(Dm.bitand(disease_flags, CHRONIC)) && SPT_PROB(cure_mod, seconds_per_tick))
            {
                await update_stage(1);
                to_chat(affected_mob, span_notice(I18n.T("Your chronic illness is alleviated a little, though it can't be cured!")));
                return null;
            }
            if (Dm.truthy(Dm.bitand(disease_flags, CURABLE)) && SPT_PROB(cure_mod, seconds_per_tick))
            {
                if (Dm.truthy(Dm.bitand(disease_flags, INCREMENTAL_CURE)))
                {
                    if (!Dm.truthy(await update_stage(stage - 1)))
                        return FALSE;
                }
                else
                {
                    await cure();
                    return FALSE;
                }
            }
        }

        if (stage == max_stages && stage_peaked != TRUE) //mostly a sanity check in case we manually set a virus to max stages
            stage_peaked = TRUE;

        if (SPT_PROB(stage_prob * slowdown * bad_immune * 2, seconds_per_tick))
        {
            if (Dm.truthy(half_stage))
            {
                half_stage = FALSE;
                await update_stage(min(stage + 1, max_stages));
            }
            else if (stage <= max_stages)
                half_stage = TRUE;
        }

        if (!Dm.truthy(Dm.bitand(disease_flags, CHRONIC)) && Dm.truthy(Dm.bitand(disease_flags, CURABLE)) && bypasses_immunity != TRUE)
        {
            if (severity == DISEASE_SEVERITY_POSITIVE)
            {
                if (slowdown < 1 || malnourished)
                    cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_POSITIVE);
                else
                {
                    recovery_prob = 0;
                    return TRUE;
                }
            }
            else if (severity == DISEASE_SEVERITY_NONTHREAT)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_NONTHREAT);
            else if (severity == DISEASE_SEVERITY_MINOR)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_MINOR);
            else if (severity == DISEASE_SEVERITY_MEDIUM)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_MEDIUM);
            else if (severity == DISEASE_SEVERITY_DANGEROUS)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_DANGEROUS);
            else if (severity == DISEASE_SEVERITY_HARMFUL)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_HARMFUL);
            else if (severity == DISEASE_SEVERITY_BIOHAZARD)
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_BIOHAZARD);
            else
                cycles_to_beat = max(DISEASE_RECOVERY_SCALING, DISEASE_CYCLES_NONTHREAT);
            peaked_cycles += Dm.div(stage, max_stages); //every cycle we spend sick counts towards eventually curing the virus, faster at higher stages
            recovery_prob += DISEASE_RECOVERY_CONSTANT + Dm.div(peaked_cycles, cycles_to_beat / DISEASE_RECOVERY_SCALING); //more severe viruses are beaten back more aggressively after the peak
            if (Dm.truthy(stage_peaked))
                recovery_prob *= DISEASE_PEAKED_RECOVERY_MULTIPLIER;
            if (slowdown < 1) //using spaceacillin can help get them over the finish line to kill a virus with decreasing effect over time
            {
                recovery_prob += clamp((((1 - slowdown) * (DISEASE_SLOWDOWN_RECOVERY_BONUS * 2)) * ((DISEASE_SLOWDOWN_RECOVERY_BONUS_DURATION - chemical_offsets) / DISEASE_SLOWDOWN_RECOVERY_BONUS_DURATION)), 0, DISEASE_SLOWDOWN_RECOVERY_BONUS);
                chemical_offsets = min(chemical_offsets + 1, DISEASE_SLOWDOWN_RECOVERY_BONUS_DURATION);
            }
            if (malnourished) //being malnourished makes it a lot harder to defeat your illness
                recovery_prob -= DISEASE_MALNUTRITION_RECOVERY_PENALTY;


            if (affected_mob.mob_mood != null) // this and most other modifiers below a shameless rip from sleeping healing buffs, but feeling good helps make it go away quicker
            {
                float sw__sanity = Dm.num(affected_mob.mob_mood.sanity_level);
                if (sw__sanity == SANITY_LEVEL_GREAT)
                    recovery_prob += 0.4f;
                else if (sw__sanity == SANITY_LEVEL_NEUTRAL)
                    recovery_prob += 0.2f;
                else if (sw__sanity == SANITY_LEVEL_DISTURBED)
                    recovery_prob += 0;
                else if (sw__sanity == SANITY_LEVEL_UNSTABLE)
                    recovery_prob += 0;
                else if (sw__sanity == SANITY_LEVEL_CRAZY)
                    recovery_prob += -0.2f;
                else if (sw__sanity == SANITY_LEVEL_INSANE)
                    recovery_prob += -0.4f;
            }

            recovery_prob += Dm.num(get_immunity_recovery());

            if (!malnourished && is_sleeping) //resting starved won't help, but resting helps
            {
                turf rest_turf = get_turf(affected_mob);
                bool is_sleeping_in_darkness = !Dm.truthy(rest_turf.check_lumcount_above(LIGHTING_TILE_IS_DARK));

                if (Dm.truthy(affected_mob.is_blind_from(EYES_COVERED)) || is_sleeping_in_darkness)
                    recovery_prob += DISEASE_GOOD_SLEEPING_RECOVERY_BONUS;

                // sleeping in silence is always better
                if (HAS_TRAIT(affected_mob, TRAIT_DEAF))
                    recovery_prob += DISEASE_GOOD_SLEEPING_RECOVERY_BONUS;

                // check for beds
                if (Dm.locate<obj_structure_bed>(affected_mob.loc) != null)
                    recovery_prob += DISEASE_GOOD_SLEEPING_RECOVERY_BONUS;
                else if (Dm.locate<obj_structure_table>(affected_mob.loc) != null)
                    recovery_prob += (DISEASE_GOOD_SLEEPING_RECOVERY_BONUS / 2);

                // don't forget the bedsheet
                if (Dm.locate<obj_item_bedsheet>(affected_mob.loc) != null)
                    recovery_prob += DISEASE_GOOD_SLEEPING_RECOVERY_BONUS;

                // you forgot the pillow
                if (Dm.locate<obj_item_pillow>(affected_mob.loc) != null)
                    recovery_prob += DISEASE_GOOD_SLEEPING_RECOVERY_BONUS;

                recovery_prob *= DISEASE_SLEEPING_RECOVERY_MULTIPLIER; //any form of sleeping magnifies all effects a little bit
            }

            recovery_prob = clamp(recovery_prob / bad_immune, 0, 100);

            if (Dm.truthy(recovery_prob) && (bad_immune == 1))
            {
                float failure_chance = (1 - Dm.num(get_recovery_failure_chance()) / 100);
                if (SPT_PROB(recovery_prob * failure_chance, seconds_per_tick))
                {
                    if (stage == 1 && prob(cure_chance * DISEASE_FINAL_CURE_CHANCE_MULTIPLIER)) //if we reduce FROM stage == 1, cure the virus - after defeating its cure_chance in a final battle
                    {
                        if (severity == DISEASE_SEVERITY_POSITIVE && slowdown < 1)
                            return TRUE;
                        if (malnourished)
                        {
                            if (stage_peaked == FALSE) //if you didn't ride out the virus from its peak, if you're malnourished when it cures, you don't get resistance
                            {
                                await cure(add_resistance: FALSE);
                                return FALSE;
                            }
                            else if (prob(50)) //if you rode it out from the peak, challenge cure_chance on if you get resistance or not
                            {
                                await cure(add_resistance: TRUE);
                                return FALSE;
                            }
                        }
                        else
                        {
                            await cure(add_resistance: TRUE); //stay fed and cure it at any point, you're immune
                            return FALSE;
                        }
                    }
                    await update_stage(max(stage - 1, 1));
                }
            }

            if (is_sleeping || slowdown != 1) //sleeping and using spaceacillin lets us nosell applicable virus symptoms firing with decreasing effectiveness over time
            {
                if (prob(100 - min((100 * (symptom_offsets / DISEASE_SYMPTOM_OFFSET_DURATION)), 100 - cure_chance * DISEASE_FINAL_CURE_CHANCE_MULTIPLIER))) //viruses with higher cure_chance will ultimately be more possible to offset symptoms on
                {
                    symptom_offsets = min(symptom_offsets + 1, DISEASE_SYMPTOM_OFFSET_DURATION);
                    return FALSE;
                }
            }
        }

        return Dm.num(!Dm.truthy(carrier));
    }

    // tg: code/datums/diseases/_disease.dm:271
    public virtual async DmTask<object> update_stage(object new_stage = null)
    {
        stage = Dm.num(new_stage);
        if (Dm.eq(new_stage, max_stages) && !Dm.truthy(stage_peaked)) //once a virus has hit its peak, set it to have done so
            stage_peaked = TRUE;
        if (stage <= 0)
        {
            await cure();
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/datums/diseases/_disease.dm:280
    public virtual object has_cure()
    {
        if (!Dm.truthy(Dm.bitand(disease_flags, CURABLE | CHRONIC)))
            return FALSE;

        float dot = cures.len;
        foreach (object C_id in Dm.each(cures))
        {
            if (!Dm.truthy(((datum_reagents)affected_mob.reagents).has_reagent(target_reagent: C_id, check_subtypes: TRUE)))
                dot--;
        }
        if (!Dm.truthy(dot) || (Dm.truthy(needs_all_cures) && dot < cures.len))
            return FALSE;
        return dot;
    }

    /**
     * Handles performing a spread-via-air
     *
     * Checks for stuff like "is our mouth covered" for you
     *
     * * spread_range - How far the disease can spread
     * * force_spread - If TRUE, the disease will spread regardless of the spread_flags
     * * require_facing - If TRUE, the disease will only spread if the source mob is facing the target mob
     */
    // tg: code/datums/diseases/_disease.dm:300
    public virtual async DmTask<object> airborne_spread(float? spread_range = null, float? force_spread = null, float? require_facing = null)
    {
        spread_range ??= 2;
        force_spread ??= TRUE;
        require_facing ??= FALSE;
        if (affected_mob == null)
            return FALSE;
        if (!Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_AIRBORNE)) && !Dm.truthy(force_spread))
            return FALSE;
        // DV558: tg's snapshot reads `if(affected_mob.can_spread_airborne_diseases()) return FALSE`, which stops every host
        // whose mouth is free and lets only a masked or internals-breathing one spread; the port spreads when the host can.
        if (!Dm.truthy(affected_mob.can_spread_airborne_diseases()))
            return FALSE;
        if (!Dm.truthy(has_required_infectious_organ(affected_mob, ORGAN_SLOT_LUNGS))) //also if you lack lungs
            return FALSE;
        if (HAS_TRAIT(affected_mob, TRAIT_VIRUS_RESISTANCE)) //being full or on spaceacillin makes you less likely to spread a virus
            return FALSE;
        turf mob_loc = affected_mob.loc as turf;
        if (mob_loc == null)
            return FALSE;
        foreach (mob_living_carbon to_infect in Dm.each<mob_living_carbon>(oview(spread_range, affected_mob)))
        {
            turf infect_loc = to_infect.loc as turf;
            if (infect_loc == null)
                continue;
            if (Dm.truthy(require_facing) && !Dm.truthy(is_source_facing_target(affected_mob, to_infect)))
                continue;
            if (!Dm.truthy(disease_air_spread_walk(mob_loc, infect_loc)))
                continue;
            await to_infect.contract_airborne_disease(this);
        }
        return TRUE;
    }

    // tg: code/datums/diseases/_disease.dm:337
    public virtual async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= TRUE;
        if (severity == DISEASE_SEVERITY_UNCURABLE) //aw man :(
            return;
        if (affected_mob != null)
        {
            if (Dm.truthy(add_resistance) && Dm.truthy(Dm.bitand(disease_flags, CAN_RESIST)))
                LAZYOR(ref affected_mob.disease_resistances, GetDiseaseID());
            if (Dm.truthy(affected_mob.ckey))
            {
                turf cure_turf = get_turf(affected_mob);
                log_virus(Dm.text("[] was cured of virus: [] at []", key_name(affected_mob), admin_details(), loc_name(cure_turf)));
            }
        }
        qdel(this);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/diseases/_disease.dm:348
    public virtual object IsSame(datum_disease D = null)
    {
        if (istype(D, type))
            return TRUE;
        return FALSE;
    }


    // tg: code/datums/diseases/_disease.dm:354
    public virtual async DmTask<object> Copy()
    {
        //note that stage is not copied over - the copy starts over at stage 1
        // var/static/list/copy_vars: the port copies the listed vars by name (`vars` is not translated, rules 10.7); a list value is
        // copied with Copy(), as tg's loop does.

        datum_disease D = (datum_disease)(copy_type != null ? await Dm.new_async(copy_type) : await Dm.new_async(type));
        D.name = name;
        D.visibility_flags = visibility_flags;
        D.disease_flags = disease_flags;
        D.spread_flags = spread_flags;
        D.form = form;
        D.desc = desc;
        D.agent = agent;
        D.spread_text = spread_text;
        D.cure_text = cure_text is DmList cure_text__list ? cure_text__list.Copy() : cure_text;
        D.max_stages = max_stages;
        D.stage_prob = stage_prob;
        D.incubation_time = incubation_time;
        D.viable_mobtypes = viable_mobtypes?.Copy();
        D.cures = cures?.Copy();
        D.infectivity = infectivity;
        D.cure_chance = cure_chance;
        D.required_organ = required_organ;
        D.bypasses_immunity = bypasses_immunity;
        D.spreading_modifier = spreading_modifier;
        D.severity = severity;
        D.needs_all_cures = needs_all_cures;
        D.strain_data = strain_data?.Copy();
        D.infectable_biotypes = infectable_biotypes;
        D.process_dead = process_dead;
        return D;
    }

    // tg: code/datums/diseases/_disease.dm:370
    public virtual async DmTask after_add()
    {
        await DmTask.CompletedTask;
    }


    // tg: code/datums/diseases/_disease.dm:374
    public virtual object GetDiseaseID()
    {
        return Dm.text("[]", type);
    }

    // tg: code/datums/diseases/_disease.dm:377
    public virtual void remove_disease()
    {
        unregister_disease_signals();
        LAZYREMOVE(ref affected_mob.diseases, this); //remove the datum from the list
        affected_mob.med_hud_set_status();
        affected_mob = null;
    }

    /**
     * Checks the given typepath against the list of viable mobtypes.
     *
     * Returns TRUE if the mob_type path is derived from of any entry in the viable_mobtypes list.
     * Returns FALSE otherwise.
     *
     * Arguments:
     * * mob_type - Type path to check against the viable_mobtypes list.
     */
    // tg: code/datums/diseases/_disease.dm:392
    public virtual object is_viable_mobtype(object mob_type = null)
    {
        foreach (object viable_type in Dm.each(viable_mobtypes))
        {
            if (ispath(mob_type, (Type)viable_type))
                return TRUE;
        }

        // Let's only do this check if it fails. Did some genius coder pass in a non-type argument?
        if (!ispath(mob_type))
            stack_trace(Dm.text("Non-path argument passed to mob_type variable: []", mob_type), "code/datums/diseases/_disease.dm", 399);

        return FALSE;
    }

    /// Checks if the mob has the required organ and it's not robotic or affected by inorganic biology
    // tg: code/datums/diseases/_disease.dm:404
    public virtual object has_required_infectious_organ(mob_living_carbon target = null, object required_organ_slot = null)
    {
        if (!iscarbon(target))
            return FALSE;

        obj_item_organ target_organ = target.get_organ_slot(required_organ_slot) as obj_item_organ;
        if (target_organ == null)
            return FALSE;

        // snowflake organ checks: grants immunity to disease unless 'inorganic biology' symptom is present
        if (Dm.truthy(IS_ROBOTIC_ORGAN(target_organ)) && !Dm.truthy(Dm.bitand(infectable_biotypes, MOB_ROBOTIC)))
            return FALSE;
        if (Dm.truthy(IS_MINERAL_ORGAN(target_organ)) && !Dm.truthy(Dm.bitand(infectable_biotypes, MOB_MINERAL)))
            return FALSE;

        return TRUE;
    }

    /// Handles spreading via air when our mob breathes
    // tg: code/datums/diseases/_disease.dm:421
    [SignalHandler]
    public virtual void on_breath(datum source = null, object seconds_per_tick = null, params object[] __extra)
    {
        if (SPT_PROB(infectivity * 4, seconds_per_tick))
            Waiting.NoSleep(airborne_spread());
    }

    // Increases natural recovery based on prior immunities
    // tg: code/datums/diseases/_disease.dm:428
    public virtual object get_immunity_recovery()
    {
        return 0;
    }
}

public static partial class GlobalProcs
{
    /// Helper for checking if there is an air path between two turfs
    // tg: code/datums/diseases/_disease.dm:326
    public static object disease_air_spread_walk(turf start = null, turf end = null)
    {
        if (start == null || end == null)
            return FALSE;
        while (true)
        {
            if (end == start)
                return TRUE;
            turf Temp = get_step_towards(end, start);
            if (!TURFS_CAN_SHARE(end, Temp)) //Don't go through a wall
                return FALSE;
            end = Temp;
        }
    }

    //Use this to compare severities
    // tg: code/datums/diseases/_disease.dm:432
    public static object get_disease_severity_value(object severity = null)
    {
        if (Dm.eq(severity, DISEASE_SEVERITY_UNCURABLE))
            return 0;
        if (Dm.eq(severity, DISEASE_SEVERITY_POSITIVE))
            return 1;
        if (Dm.eq(severity, DISEASE_SEVERITY_NONTHREAT))
            return 2;
        if (Dm.eq(severity, DISEASE_SEVERITY_MINOR))
            return 3;
        if (Dm.eq(severity, DISEASE_SEVERITY_MEDIUM))
            return 4;
        if (Dm.eq(severity, DISEASE_SEVERITY_HARMFUL))
            return 5;
        if (Dm.eq(severity, DISEASE_SEVERITY_DANGEROUS))
            return 6;
        if (Dm.eq(severity, DISEASE_SEVERITY_BIOHAZARD))
            return 7;
        return null;
    }

    // tg: code/datums/diseases/_disease.dm:451
    public static object get_disease_spread_text(object spread_flags = null)
    {
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_AIRBORNE)))
            return "Airborne";
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
            return "Skin contact";
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
            return "Fluid contact";
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_BLOOD)))
            return "Blood";
        if (Dm.truthy(Dm.bitand(spread_flags, DISEASE_SPREAD_NON_CONTAGIOUS)))
            return "None";
        return "Unknown";
    }
}
