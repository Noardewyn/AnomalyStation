// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/equilibrium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1: the gradual reactions of the reaction engine, SSreagents).

/*
* #/datum/equilibrium
*
* A dynamic reaction object that processes the reaction that it is set within it. Relies on a reagents holder to call and operate the functions.
*
* An object/datum to contain the vars for each of the reactions currently ongoing in a holder/reagents datum
* This way all information is kept within one accessable object
* equilibrium is a unique name as reaction is already too close to chemical_reaction
* This is set up this way to reduce holder.dm bloat as well as reduce confusing list overhead
* The crux of the fermimechanics are handled here
* Instant reactions AREN'T handled here. See holder.dm
*/
// tg: code/modules/reagents/chemistry/equilibrium.dm:13
public partial class datum_equilibrium : datum
{
    ///The chemical reaction that is presently being processed
    // tg: code/modules/reagents/chemistry/equilibrium.dm:15  (hand-declared: the generator types it as a path)
    public datum_chemical_reaction reaction;

    /*
    * Creates and sets up a new equlibrium object
    *
    * Arguments:
    * * input_reaction - the chemical_reaction datum that will be processed
    * * input_holder - the reagents datum that the output will be put into
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:59
    [DmParams("input_reaction", "input_holder")]
    public override async DmTask New(params object[] __extra)
    {
        datum_chemical_reaction input_reaction = Dm.arg<datum_chemical_reaction>(__extra, 0);
        datum_reagents input_holder = Dm.arg<datum_reagents>(__extra, 1);
        reaction = input_reaction;
        holder = input_holder;
        if (!check_inital_conditions()) //If we're outside of the scope of the reaction vars
        {
            to_delete = TRUE;
            return;
        }
        LAZYADD(ref holder.reaction_list, this);
        SSblackbox.record_feedback("tally", "chemical_reaction", 1, Dm.text("[] attempts", reaction.type));
    }

    // tg: code/modules/reagents/chemistry/equilibrium.dm:68
    public override object Destroy(float? force = null)
    {
        if (holder != null)
            LAZYREMOVE(ref holder.reaction_list, this);
        holder = null;
        reaction = null;
        to_delete = TRUE;
        return base.Destroy(force);
    }

    /*
    * Check to make sure our input vars are sensible - truncated version of check_reagent_properties()
    *
    * (as the setup in holder.dm checks for that already - this is a way to reduce calculations on New())
    * Don't call this unless you know what you're doing, this is an internal proc
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:81  PRIVATE_PROC(TRUE)
    public virtual bool check_inital_conditions()
    {
        if (QDELETED(holder))
        {
            stack_trace("an equilibrium is missing its holder.", "code/modules/reagents/chemistry/equilibrium.dm", 85);
            return false;
        }
        if (QDELETED(reaction))
        {
            stack_trace("an equilibrium is missing its reaction.", "code/modules/reagents/chemistry/equilibrium.dm", 88);
            return false;
        }
        if (!Dm.truthy(length(reaction.required_reagents)))
        {
            stack_trace("an equilibrium is missing required reagents.", "code/modules/reagents/chemistry/equilibrium.dm", 91);
            return false;
        }

        //Make sure we have the right multipler for on_reaction()
        foreach (object single_reagent in Dm.each(reaction.required_reagents))
            multiplier = Dm.min(multiplier, Dm.num(holder.get_reagent_amount(single_reagent)) / Dm.num(reaction.required_reagents[single_reagent]));
        multiplier = round(multiplier, CHEMICAL_QUANTISATION_LEVEL);
        if (multiplier == 0) //we have no more or very little reagents left
            return false;

        //To prevent reactions outside of the pH window from starting.
        if (holder.ph < (reaction.optimal_ph_min - reaction.determin_ph_range) || holder.ph > (reaction.optimal_ph_max + reaction.determin_ph_range))
            return false;

        //All checks pass. cache the product ratio
        if (Dm.truthy(length(reaction.results)))
        {
            product_ratio = 0;
            foreach (object product in Dm.each(reaction.results))
                product_ratio += Dm.num(reaction.results[product]);
        }
        else
            product_ratio = 1;
        min_rate = product_ratio * (CHEMICAL_VOLUME_ROUNDING / 2);

        return true;
    }

    /**
     * Check to make sure our input vars are sensible
     * 1) Is our atom in which this reaction is occuring still intact?
     * 2) Do we still have reagents to react with
     * 3) Do we have the required catalysts?
     * If you're adding more checks for reactions, this is the proc to edit
     * otherwise, generally, don't call this directed except internally
     */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:124  PRIVATE_PROC(TRUE)
    public virtual async DmTask<bool> check_reagent_properties()
    {
        //Have we exploded from on_reaction or did we run out of reagents?
        if (QDELETED(holder.my_atom) || !Dm.truthy(holder.reagent_list.len))
            return false;

        //Check for catalysts
        float total_matching_catalysts = 0;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            //this is done this way to reduce processing compared to holder.has_reagent(P)
            foreach (object catalyst in Dm.each(reaction.required_catalysts))
            {
                if (Dm.eq(catalyst, reagent.type))
                    total_matching_catalysts++;
            }
            if (istype(reagent, typeof(datum_reagent_catalyst_agent)))
            {
                datum_reagent_catalyst_agent catalyst_agent = reagent as datum_reagent_catalyst_agent;
                if (reagent.volume >= Dm.num(catalyst_agent.min_volume))
                {
                    catalyst_agent.consider_catalyst(this); // gaps P5: the catalyst agents are live
                }
            }
        }

        //Our present catalysts should match with our required catalyts
        return total_matching_catalysts == reaction.required_catalysts.len;
    }

    /*
    * Calculates how much we're aiming to create
    *
    * Specifically calcuates multiplier, product_ratio, step_target_vol
    * Also checks to see if these numbers are sane, returns a TRUE/FALSE
    * Generally an internal proc
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:153  PRIVATE_PROC(TRUE)
    public virtual bool calculate_yield()
    {
        multiplier = INFINITY;
        foreach (object reagent in Dm.each(reaction.required_reagents))
            multiplier = Dm.min(multiplier, Dm.num(holder.get_reagent_amount(reagent)) / Dm.num(reaction.required_reagents[reagent]));
        multiplier = round(multiplier, CHEMICAL_QUANTISATION_LEVEL);
        if (multiplier == 0) //we have no more or very little reagents left
            return false;

        //Incase of no reagent product
        if (!Dm.truthy(length(reaction.results)))
        {
            step_target_vol = INFINITY;
            foreach (object reagent in Dm.each(reaction.required_reagents))
                step_target_vol = Dm.min(step_target_vol, multiplier * Dm.num(reaction.required_reagents[reagent]));
            return true;
        }

        //If we have reagent products
        step_target_vol = 0;
        reacted_vol = 0; //Because volumes can be lost mid reactions
        foreach (object product in Dm.each(reaction.results))
        {
            step_target_vol += multiplier * Dm.num(reaction.results[product]);
            reacted_vol += Dm.num(holder.get_reagent_amount(product));
        }
        target_vol = reacted_vol + step_target_vol;
        return true;
    }

    /*
    * Main method of checking for explosive - or failed states
    * Checks overheated() and overly_impure() of a reaction
    * This was moved from the start, to the end - after a reaction, so post reaction temperature changes aren't ignored.
    * overheated() is first - so double explosions can't happen (i.e. explosions that blow up the holder)
    * step_volume_added is how much product (across all products) was added for this single step
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:186  PRIVATE_PROC(TRUE)
    public virtual async DmTask<bool> check_fail_states(float step_volume_added)
    {
        //Are we overheated?
        if (Dm.truthy(reaction.is_cold_recipe))
        {
            if (holder.chem_temp < reaction.overheat_temp && reaction.overheat_temp != NO_OVERHEAT) //This is before the process - this is here so that overly_impure and overheated() share the same code location (and therefore vars) for calls.
            {
                SSblackbox.record_feedback("tally", "chemical_reaction", 1, Dm.text("[] overheated reaction steps", reaction.type));
                await reaction.overheated(holder, this, step_volume_added);
            }
        }
        else
        {
            if (holder.chem_temp > reaction.overheat_temp)
            {
                SSblackbox.record_feedback("tally", "chemical_reaction", 1, Dm.text("[] overheated reaction steps", reaction.type));
                await reaction.overheated(holder, this, step_volume_added);
            }
        }

        //is our product too impure?
        foreach (object product in Dm.each(reaction?.results))
        {
            datum_reagent reagent = holder?.has_reagent(product) as datum_reagent;
            if (reagent == null) //might be missing from overheat exploding
                continue;
            if (reagent.purity < reaction.purity_min) //If purity is below the min, call the proc
            {
                SSblackbox.record_feedback("tally", "chemical_reaction", 1, Dm.text("[] overly impure reaction steps", reaction.type));
                await reaction.overly_impure(holder, this, step_volume_added);
            }
        }

        //did we explode or run out of reagents?
        return !QDELETED(holder?.my_atom) && Dm.truthy(holder.reagent_list.len);
    }

    /*
    * Deals with lag - allows a reaction to speed up to 3x from seconds_per_tick
    * "Charged" time (time_deficit) discharges by incrementing reactions by doubling them
    * If seconds_per_tick is greater than 1.5, then we save the extra time for the next ticks
    *
    * Arguments:
    * * seconds_per_tick - the time between the last proc in world.time
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:219  PRIVATE_PROC(TRUE)
    public virtual float deal_with_time(float seconds_per_tick)
    {
        if (seconds_per_tick > 1)
        {
            time_deficit = Dm.num(time_deficit) + seconds_per_tick - 1;
            seconds_per_tick = 1; //Lets make sure reactions aren't super speedy and blow people up from a big lag spike
        }
        else if (Dm.truthy(time_deficit))
        {
            if (Dm.num(time_deficit) < 0.25f)
            {
                seconds_per_tick += Dm.num(time_deficit);
                time_deficit = 0f;
            }
            else
            {
                seconds_per_tick += 0.25f;
                time_deficit = Dm.num(time_deficit) - 0.25f;
            }
        }
        return seconds_per_tick;
    }

    /*
    * Main reaction processor - Increments the reaction by a timestep
    *
    * First checks the holder to make sure it can continue
    * Then calculates the purity and volume produced.TRUE
    * Then adds/removes reagents
    * Then alters the holder pH and temperature, and calls reaction_step
    * Arguments:
    * * seconds_per_tick - the time displacement between the last call and the current, 1 is a standard step
    * * purity_modifier - how much to modify the step's purity by (0 - 1)
    */
    // tg: code/modules/reagents/chemistry/equilibrium.dm:245
    public virtual async DmTask<object> react_timestep(float seconds_per_tick = 0, float purity_modifier = 1)
    {
        if (Dm.truthy(to_delete)) //Sanity incase we try to complete a failed reaction
            return FALSE;
        if (!await check_reagent_properties()) //this is first because it'll call explosions first
        {
            to_delete = TRUE;
            return null;
        }
        if (!calculate_yield()) //So that this can detect if we're missing reagents
        {
            to_delete = TRUE;
            return null;
        }
        seconds_per_tick = deal_with_time(seconds_per_tick);

        delta_t = 0f; //how far off optimal temp we care
        delta_ph = 0f; //How far off the pH we are
        float cached_ph = holder.ph;
        float cached_temp = holder.chem_temp;
        float purity = 1; //purity of the current step

        //Begin checks
        //Calculate DeltapH (Deviation of pH from optimal)
        //Within mid range
        float acceptable_ph;
        if (cached_ph >= reaction.optimal_ph_min && cached_ph <= reaction.optimal_ph_max)
            delta_ph = 1f; //100% purity for this step
        //Lower range
        else if (cached_ph < reaction.optimal_ph_min) //If we're outside of the optimal lower bound
        {
            acceptable_ph = reaction.optimal_ph_min - reaction.determin_ph_range;
            if (cached_ph < acceptable_ph) //If we're outside of the deterministic bound
                delta_ph = 0f; //0% purity
            else //We're in the deterministic phase
                delta_ph = Dm.pow((cached_ph - acceptable_ph) / reaction.determin_ph_range, reaction.ph_exponent_factor);
        }
        //Upper range
        else if (cached_ph > reaction.optimal_ph_max) //If we're above of the optimal lower bound
        {
            acceptable_ph = reaction.optimal_ph_max + reaction.determin_ph_range;
            if (cached_ph > acceptable_ph) //If we're outside of the deterministic bound
                delta_ph = 0f; //0% purity
            else //We're in the deterministic phase
                delta_ph = Dm.pow((acceptable_ph - cached_ph) / reaction.determin_ph_range, reaction.ph_exponent_factor);
        }

        //Calculate DeltaT (Deviation of T from optimal)
        if (!Dm.truthy(reaction.is_cold_recipe))
        {
            if (cached_temp < reaction.optimal_temp && cached_temp >= reaction.required_temp)
                delta_t = Dm.pow((cached_temp - reaction.required_temp) / (reaction.optimal_temp - reaction.required_temp), reaction.temp_exponent_factor);
            else if (cached_temp >= reaction.optimal_temp)
                delta_t = 1f;
            else //too hot
            {
                delta_t = 0f;
                to_delete = TRUE;
                return null;
            }
        }
        else
        {
            if (cached_temp > reaction.optimal_temp && cached_temp <= reaction.required_temp)
                delta_t = Dm.pow((reaction.required_temp - cached_temp) / (reaction.required_temp - reaction.optimal_temp), reaction.temp_exponent_factor);
            else if (cached_temp <= reaction.optimal_temp)
                delta_t = 1f;
            else //Too cold
            {
                delta_t = 0f;
                to_delete = TRUE;
                return null;
            }
        }

        //Call any special reaction steps BEFORE addition
        if (Dm.eq(await reaction.reaction_step(holder, this, delta_t, delta_ph, step_target_vol), END_REACTION))
        {
            to_delete = TRUE;
            return null;
        }

        //Catalyst modifier
        delta_t = Dm.num(delta_t) * speed_mod;

        //set purity equal to pH offset
        purity = Dm.num(delta_ph);

        //Then adjust purity of result with beaker reagent purity.
        purity *= Dm.num(holder.get_average_purity());

        //Then adjust it from the input modifier
        purity *= purity_modifier;

        //Now we calculate how much to add - this is normalised to the rate up limiter
        float delta_chem_factor = reaction.rate_up_lim * Dm.num(delta_t) * seconds_per_tick;
        //keep limited
        if (delta_chem_factor > step_target_vol)
            delta_chem_factor = step_target_vol;
        //ensure its above minimum rate below which rounding errors occur
        else if (delta_chem_factor < Dm.num(min_rate))
            delta_chem_factor = Dm.num(min_rate);
        //Normalise to multiproducts
        delta_chem_factor = round(delta_chem_factor / product_ratio, CHEMICAL_VOLUME_ROUNDING);
        if (delta_chem_factor <= 0)
        {
            to_delete = TRUE;
            return null;
        }

        //Calculate how much product to make and how much reactant to remove factors..
        float required_amount;
        float pH_adjust;
        foreach (object requirement in Dm.each(reaction.required_reagents))
        {
            required_amount = Dm.num(reaction.required_reagents[requirement]);
            if (!Dm.truthy(await holder.remove_reagent(requirement, delta_chem_factor * required_amount)))
            {
                to_delete = TRUE;
                return null;
            }
            //Apply pH changes
            if (Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_PH_VOL_CONSTANT)))
                pH_adjust = ((delta_chem_factor * required_amount) / target_vol) * (reaction.H_ion_release * h_ion_mod);
            else //Default adds pH independant of volume
                pH_adjust = (delta_chem_factor * required_amount) * (reaction.H_ion_release * h_ion_mod);
            holder.adjust_specific_reagent_ph(requirement, pH_adjust);
        }

        float step_add;
        float total_step_added = 0;
        foreach (object product in Dm.each(reaction.results))
        {
            //create the products
            step_add = Dm.num(await holder.add_reagent(product, delta_chem_factor * Dm.num(reaction.results[product]), null, cached_temp, purity, override_base_ph: TRUE));
            if (step_add == 0)
            {
                to_delete = TRUE;
                return null;
            }

            //Apply pH changes
            if (Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_PH_VOL_CONSTANT)))
                pH_adjust = (step_add / target_vol) * (reaction.H_ion_release * h_ion_mod);
            else
                pH_adjust = step_add * (reaction.H_ion_release * h_ion_mod);
            holder.adjust_specific_reagent_ph(product, pH_adjust);

            //record amounts created
            reacted_vol += step_add;
            total_step_added += step_add;
        }

        //Apply thermal output of reaction to beaker
        float heat_energy = reaction.thermic_constant * total_step_added * thermic_mod;
        if (Dm.truthy(Dm.bitand(reaction.reaction_flags, REACTION_HEAT_ARBITARY))) //old method - for every bit added, the whole temperature is adjusted
            holder.set_temperature(Dm.clamp(holder.chem_temp + heat_energy, 0, CHEMICAL_MAXIMUM_TEMPERATURE));
        else //Standard mechanics - heat is relative to the beaker conditions
            holder.adjust_thermal_energy(heat_energy * SPECIFIC_HEAT_DEFAULT, 0, CHEMICAL_MAXIMUM_TEMPERATURE);

        //Give a chance of sounds
        if (prob(5) && !HAS_TRAIT(holder.my_atom, TRAIT_SILENT_REACTIONS))
        {
            Dm.Deferred("code/modules/reagents/chemistry/equilibrium.dm:383", "react_timestep: icon2html of the container in the chat line (the 3D client's chat is text: the chat icons)");
            holder.my_atom.audible_message(span_notice(Dm.text(" []", reaction.mix_message)));
            if (Dm.truthy(reaction.mix_sound))
                playsound(get_turf(holder.my_atom), reaction.mix_sound, 80, TRUE);
        }

        //Used for UI output
        reaction_quality = purity;

        //post reaction checks
        if (!await check_fail_states(total_step_added))
        {
            to_delete = TRUE;
            return null;
        }

        //If the volume of reagents created(total_step_added) >= volume of reagents still to be created(step_target_vol) then end
        //i.e. we have created all the reagents needed for this reaction
        //This is only accurate when a single reaction is present and we don't have multiple reactions where
        //reaction B consumes the products formed from reaction A(which can happen in add_reagent() as it also triggers handle_reactions() which can consume the reagent just added)
        //because total_step_added will be higher than the actual volume that was created leading to the reaction ending early
        //and yielding less products than intended
        if (total_step_added >= step_target_vol && LAZYLEN(holder.reaction_list) == 1)
            to_delete = TRUE;
        return null;
    }
}
