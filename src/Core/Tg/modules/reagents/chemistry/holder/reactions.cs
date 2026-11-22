// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/holder/reactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1, the chemistry's reaction engine for the foam and smoke reactions). The reaction index holds every live
// reaction (build_chemical_reactions_lists): the inert ones react when their package writes them.

// tg: code/modules/reagents/chemistry/holder/reactions.dm:5
public partial class datum_reagents : datum
{
    /**
     * Handle any reactions possible in this holder
     * Also UPDATES the reaction list
    */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:5
    public virtual async DmTask<object> handle_reactions()
    {
        if (QDELING(this))
        {
            throw Dm.CRASH(Dm.text("[] is trying to handle reactions while being flagged for deletion. It presently has [] number of reactants in it. If that is over 0 then something terrible happened.", my_atom, length(reagent_list)), "code/modules/reagents/chemistry/holder/reactions.dm", 7);
        }

        if (!Dm.truthy(length(reagent_list))) //The liver is calling this method a lot, and is often empty of reagents so it's pointless busywork. It should be an easy fix, but I'm nervous about touching things beyond scope. Also since everything is so handle_reactions() trigger happy it might be a good idea having this check anyways.
            return FALSE;

        if (Dm.truthy(Dm.bitand(flags, NO_REACT)))
        {
            if (Dm.truthy(is_reacting))
                await force_stop_reacting(); //Force anything that is trying to to stop
            return FALSE; //Yup, no reactions here. No siree.
        }

        // We assert that reagents will not need to react before the map is fully loaded
        // This is the best I can do, sorry :(
        if (!MC_RUNNING())
            return FALSE;

        DmList cached_reagents = Dm.list();
        foreach (datum_reagent target in Dm.each_cast<datum_reagent>(reagent_list))
            cached_reagents[target.type] = target.volume;
        DmList cached_reactions = GLOB.chemical_reactions_list_reactant_index;
        datum cached_my_atom = my_atom;

        float dot = 0;
        foreach (object reagent in Dm.each(cached_reagents))
        {
            // ?.: a world whose SSreagents has not built the index (a toy room with no Master's init), where tg always has it
            foreach (datum_chemical_reaction reaction in Dm.each_cast<datum_chemical_reaction>(cached_reactions?[reagent])) // Was a big list but now it should be smaller since we filtered it with our reagent id
            {
                //is this reaction already going on?
                bool next_reaction = false;
                foreach (datum_equilibrium E_exist in Dm.each_cast<datum_equilibrium>(reaction_list))
                {
                    if (E_exist.reaction == reaction) //Don't add duplicates
                    {
                        next_reaction = true;
                        break;
                    }
                }
                if (next_reaction)
                    continue;

                //do we have the required reagents?
                float granularity = 1;
                if (!Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_INSTANT)))
                    granularity = CHEMICAL_QUANTISATION_LEVEL;
                float present_volume = 0;
                DmList requirements = Dm.bitor((object)reaction.required_reagents, reaction.required_catalysts) as DmList;
                foreach (object requirement in Dm.each(requirements))
                {
                    present_volume = Dm.num(cached_reagents[requirement]);
                    if (present_volume == 0)
                    {
                        next_reaction = true;
                        break;
                    }

                    if (present_volume < Dm.num(requirements[requirement]) * granularity)
                    {
                        next_reaction = true;
                        break;
                    }
                }
                if (next_reaction)
                    continue;

                //do we have the required container?
                if (cached_my_atom != null)
                {
                    if (reaction.required_container != null)
                    {
                        if (Dm.truthy(reaction.required_container_accepts_subtypes))
                        {
                            if (!istype(cached_my_atom, reaction.required_container))
                                continue;
                        }
                        else if (cached_my_atom.type != reaction.required_container)
                            continue;
                    }
                    if (isliving(cached_my_atom) && !Dm.truthy(reaction.mob_react)) //Makes it so certain chemical reactions don't occur in mobs
                        continue;
                }
                else if (reaction.required_container != null)
                    continue;

                //do we have the required temps?
                float required_temp = reaction.required_temp;
                float is_cold_recipe = reaction.is_cold_recipe;
                // tg's precedence kept: (required_temp != 0 && (cold && hot)) || (!cold && too cold)
                if (required_temp != 0 && (Dm.truthy(is_cold_recipe) && chem_temp > required_temp) || (!Dm.truthy(is_cold_recipe) && chem_temp < required_temp))
                    continue;

                //do we have the required ph? in range of min - ph_range & max + ph_range
                // tg's && makes this never true (a value below the low bound cannot be above the high one); kept as tg's
                if (ph < reaction.optimal_ph_min - reaction.determin_ph_range && ph > reaction.optimal_ph_max + reaction.determin_ph_range)
                    continue;

                //user defined checks
                if (!Dm.truthy(await reaction.pre_reaction_other_checks(this)))
                    continue;

                //do the actual reactions
                if (Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_INSTANT)) || Dm.truthy(Dm.bitand(flags, REAGENT_HOLDER_INSTANT_REACT)) || !Dm.truthy(length(reaction.results))) //If we have instant reactions, we process them here
                {
                    await instant_react(reaction);
                    dot++;
                }
                else
                {
                    datum_equilibrium equilibrium = Dm.@new<datum_equilibrium>(reaction, this); //Otherwise we add them to the processing list.
                    if (Dm.truthy(equilibrium.to_delete)) //failed startup checks
                        qdel(equilibrium);
                    else
                    {
                        //Adding is done in new(), deletion is in qdel
                        is_reacting = TRUE; //Prevent any on_reaction() procs from infinite looping
                        await reaction.on_reaction(this, equilibrium, equilibrium.multiplier);
                        await equilibrium.react_timestep(1); //Get an initial
                    }
                }
            }
        }

        if (LAZYLEN(reaction_list) > 0)
        {
            is_reacting = TRUE; //We've entered the reaction phase - this is set here so any reagent handling called in on_reaction() doesn't cause infinite loops
            START_PROCESSING(SSreagents, this); //see process() to see how reactions are handled
        }
        else
            is_reacting = FALSE;

        // TEST_ONLY_ASSERT(!. || MC_RUNNING(), ...): a unit-test-only assert of tg (__DEFINES/unit_tests.dm), compiled out of a game build
        return dot;
    }

    /*
    * Main Reaction loop handler, Do not call this directly
    *
    * Checks to see if there's a reaction, then processes over the reaction list, removing them if flagged
    * If any are ended, it displays the reaction message and removes it from the reaction list
    * If the list is empty at the end it finishes reacting.
    * Arguments:
    * * seconds_per_tick - the time between each time step
    */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:122
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!Dm.truthy(is_reacting))
        {
            await force_stop_reacting();
            stack_trace(Dm.text("[] | [] was forced to stop reacting. This might be unintentional.", this, my_atom), "code/modules/reagents/chemistry/holder/reactions.dm", 125);
        }
        //sum of output messages.
        DmList mix_message = Dm.list();
        //Process over our reaction list
        //See equilibrium.dm for mechanics
        float num_reactions = 0;
        foreach (datum_equilibrium equilibrium in Dm.each_cast<datum_equilibrium>(reaction_list))
        {
            //Continue reacting
            await equilibrium.react_timestep(Dm.num(seconds_per_tick));
            num_reactions++;
            //if it's been flagged to delete
            if (Dm.truthy(equilibrium.to_delete))
            {
                object temp_mix_message = await end_reaction(equilibrium);
                if (!Dm.truthy(text_in_list(temp_mix_message, mix_message)))
                    Dm.append(ref mix_message, temp_mix_message);
                continue;
            }
            SSblackbox.record_feedback("tally", "chemical_reaction", 1, Dm.text("[] total reaction steps", equilibrium.reaction.type));
        }
        if (num_reactions > 0 && Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_REACTION_STEP]))
            _SendSignal(COMSIG_REAGENTS_REACTION_STEP, Dm.list(this, num_reactions, seconds_per_tick));

        if (Dm.truthy(length(mix_message)) && !HAS_TRAIT(my_atom, TRAIT_SILENT_REACTIONS)) //This is only at the end
        {
            // [icon2html(my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the holder's picture in the chat line (the chat icons, section 76)
            Dm.Deferred("code/modules/reagents/chemistry/holder/reactions.dm:143", "process: icon2html of the reacting container in the chat line (the 3D client's chat is text: the chat icons)");
            my_atom.audible_message(span_notice(Dm.text(" []", Dm.jointext(mix_message, ""))));
        }

        if (LAZYLEN(reaction_list) == 0)
            await finish_reacting();
        else
            await handle_reactions();
        return null;
    }

    /*
    * This ends a single instance of an ongoing reaction
    *
    * Arguments:
    * * [equilibrium][datum/equilibrium] - the equilibrium that will be ended
    * Returns:
    * * mix_message - the associated mix message of a reaction
    */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:159  PRIVATE_PROC(TRUE)
    public virtual async DmTask<object> end_reaction(datum_equilibrium equilibrium = null)
    {
        await equilibrium.reaction.reaction_finish(this, equilibrium, equilibrium.reacted_vol);
        if (equilibrium.holder == null || equilibrium.reaction == null) //Somehow I'm getting empty equilibrium. This is here to handle them
        {
            LAZYREMOVE(ref reaction_list, equilibrium);
            qdel(equilibrium);
            stack_trace(Dm.text("The equilibrium datum currently processing in this reagents datum had a nulled holder or nulled reaction. src holder:[] || src type:[] ", my_atom, my_atom?.type), "code/modules/reagents/chemistry/holder/reactions.dm", 166); //Shouldn't happen. Does happen
            return null;
        }
        if (equilibrium.holder != this) //When called from Destroy() eqs are nulled in smoke. This is very strange. This is probably causing it to spam smoke because of the runtime interupting the removal.
        {
            stack_trace(Dm.text("The equilibrium datum currently processing in this reagents datum had a desynced holder to the ending reaction. src holder:[] | equilibrium holder:[] || src type:[] | equilibrium holder:[]", my_atom, equilibrium.holder.my_atom, my_atom?.type, equilibrium.holder.my_atom?.type), "code/modules/reagents/chemistry/holder/reactions.dm", 169);
            LAZYREMOVE(ref reaction_list, equilibrium);
        }

        object reaction_message = null;

        if (my_atom != null && !HAS_TRAIT(my_atom, TRAIT_SILENT_REACTIONS))
        {
            reaction_message = equilibrium.reaction.mix_message;
            if (Dm.truthy(equilibrium.reaction.mix_sound))
                playsound(get_turf(my_atom), equilibrium.reaction.mix_sound, 80, TRUE);
        }
        qdel(equilibrium);
        await update_total();

        return reaction_message;
    }

    /*
    * This stops the holder from processing at the end of a series of reactions (i.e. when all the equilibriums are completed)
    * Also resets reaction variables to be null/empty/FALSE so that it can restart correctly in the future
    */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:187  PRIVATE_PROC(TRUE)
    public virtual async DmTask finish_reacting()
    {
        STOP_PROCESSING(SSreagents, this);
        is_reacting = FALSE;
        await update_total();
    }

    /*
    * Force stops the current holder/reagents datum from reacting
    * Calls end_reaction() for each equlilbrium datum in reaction_list and finish_reacting()
    * Usually only called when a datum is transferred into a NO_REACT container
    */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:199
    public virtual async DmTask force_stop_reacting()
    {
        DmList mix_message = Dm.list();
        foreach (datum_equilibrium equilibrium in Dm.each_cast<datum_equilibrium>(reaction_list))
            Dm.append(ref mix_message, await end_reaction(equilibrium));
        if (!QDELETED(my_atom) && Dm.truthy(length(mix_message)))
        {
            Dm.Deferred("code/modules/reagents/chemistry/holder/reactions.dm:204", "force_stop_reacting: icon2html of the container in the chat line (the 3D client's chat is text: the chat icons)");
            my_atom.audible_message(span_notice(Dm.text(" []", Dm.jointext(mix_message, ""))));
        }
        await finish_reacting();
    }

    /**
     * Old reaction mechanics, edited to work on one only
     * This is changed from the old - purity of the reagents will affect yield
     *
     * Arguments
     * * [selected_reaction][datum/chemical_reaction] - the chemical reaction to finish instantly
     */
    // tg: code/modules/reagents/chemistry/holder/reactions.dm:215  PRIVATE_PROC(TRUE)
    public virtual async DmTask<object> instant_react(datum_chemical_reaction selected_reaction = null)
    {
        DmList cached_required_reagents = selected_reaction.required_reagents;
        DmList cached_results = selected_reaction.results;
        datum cached_my_atom = my_atom;

        //find how much ration of products to create
        float multiplier = INFINITY;
        foreach (object requirement in Dm.each(cached_required_reagents))
            multiplier = Dm.min(multiplier, Dm.num(get_reagent_amount(requirement)) / Dm.num(cached_required_reagents[requirement]));
        multiplier = round(multiplier, CHEMICAL_QUANTISATION_LEVEL);
        if (multiplier == 0) //Incase we're missing reagents - usually from on_reaction being called in an equlibrium when the results.len == 0 handler catches a misflagged reaction
            return FALSE;

        //average purity to be used in scaling the yield of products formed
        float average_purity = Dm.num(get_average_purity());

        //remove the required reagents
        foreach (object requirement in Dm.each(cached_required_reagents)) //this is not an object
            await remove_reagent(requirement, Dm.num(cached_required_reagents[requirement]) * multiplier);

        //add the result reagents whose yield depend on the average purity
        float yield;
        foreach (object product in Dm.each(cached_results))
        {
            yield = Dm.num(cached_results[product]) * multiplier * average_purity;
            SSblackbox.record_feedback("tally", "chemical_reaction", yield, product);
            await add_reagent(product, yield, null, chem_temp, average_purity);
        }

        //play sounds on the target atom
        // var/list/seen = viewers(4, get_turf(my_atom)); var/iconhtml = icon2html(cached_my_atom, seen): the chat icon (section 76)
        if (cached_my_atom != null)
        {
            if (!ismob(cached_my_atom) && !HAS_TRAIT(my_atom, TRAIT_SILENT_REACTIONS)) // No bubbling mobs
            {
                if (Dm.truthy(selected_reaction.mix_sound))
                    playsound(get_turf(cached_my_atom as atom), selected_reaction.mix_sound, 80, TRUE);
                Dm.Deferred("code/modules/reagents/chemistry/holder/reactions.dm:241", "instant_react: icon2html of the container in the chat line (the 3D client's chat is text: the chat icons)");
                my_atom.audible_message(span_notice(Dm.text(" []", selected_reaction.mix_message)));
            }

            //use slime extract
            if (istype(cached_my_atom, typeof(obj_item_slime_extract)))
            {
                obj_item_slime_extract extract = my_atom as obj_item_slime_extract;
                extract.extract_uses--;
                if (extract.extract_uses <= 0) // give the notification that the slime core is dead
                {
                    my_atom.visible_message(span_notice(Dm.text(" \\The []'s power is consumed in the reaction.", my_atom)));
                    extract.name = "used slime extract";
                    extract.desc = "This extract has been used up.";
                    extract.can_grind = FALSE;
                }
            }
        }

        //finish the reaction
        await selected_reaction.on_reaction(this, null, multiplier);
        return null;
    }
}
