// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/holder/holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice A completed it: the mob branches of add_reagent and update_total, add_reagent_list,
// convert_reagent, multiply, get_master_reagent, spark_act's loop and get_reagent_log_string; earlier milestones had
// translated the rest: M2 P2 the var block, New, Destroy, create_reagents, has_reagent, clear_reagents and update_total,
// M4a P4a add_reagent and remove_reagent, M4b trans_to, copy_data, expose and expose_temperature, M4c spark_act's head,
// M5x del_reagent, M7 remove_all). GLOB.chemical_reagents_list is not built (add_reagent's GAP below).

// The [DmPath] of /datum/reagents is on __HELPERS/reagents.cs (rules 2.3).
// tg: code/modules/reagents/chemistry/holder/holder.dm:4
public partial class datum_reagents : datum
{
    /// The reagents being held
    // tg: code/modules/reagents/chemistry/holder/holder.dm:6  var/list/datum/reagent/reagent_list = list()
    public DmList reagent_list;
    /// Current volume of all the reagents
    // tg: code/modules/reagents/chemistry/holder/holder.dm:8
    public float total_volume = 0;
    /// Max volume of this holder
    // tg: code/modules/reagents/chemistry/holder/holder.dm:10
    public float maximum_volume = 100;
    /// The atom this holder is attached to
    // tg: code/modules/reagents/chemistry/holder/holder.dm:12
    public atom my_atom = null;
    /// Current temp of the holder volume
    // tg: code/modules/reagents/chemistry/holder/holder.dm:14
    public float chem_temp = 150;
    ///pH of the whole system
    // tg: code/modules/reagents/chemistry/holder/holder.dm:16
    public float ph = CHEMICAL_NORMAL_PH;
    /// various flags, see code\__DEFINES\reagents.dm
    // tg: code/modules/reagents/chemistry/holder/holder.dm:18
    public object flags;
    ///list of reactions currently on going, this is a lazylist for optimisation
    // tg: code/modules/reagents/chemistry/holder/holder.dm:20
    public DmList reaction_list;
    ///Hard check to see if the reagents is presently reacting
    // tg: code/modules/reagents/chemistry/holder/holder.dm:22
    public float is_reacting = FALSE;
    ///UI lookup stuff
    ///Keeps the id of the reaction displayed in the ui
    // tg: code/modules/reagents/chemistry/holder/holder.dm:25
    public object ui_reaction_id = null;
    ///Keeps the id of the reagent displayed in the ui
    // tg: code/modules/reagents/chemistry/holder/holder.dm:27
    public object ui_reagent_id = null;
    ///The bitflag of the currently selected tags in the ui
    // tg: code/modules/reagents/chemistry/holder/holder.dm:29
    public float ui_tags_selected = NONE;
    ///What index we're at if we have multiple reactions for a reagent product
    // tg: code/modules/reagents/chemistry/holder/holder.dm:31
    public float ui_reaction_index = 1;
    ///If we're syncing with the beaker - so return reactions that are actively happening
    // tg: code/modules/reagents/chemistry/holder/holder.dm:33
    public float ui_beaker_sync = FALSE;

    // tg: code/modules/reagents/chemistry/holder/holder.dm:6 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        reagent_list = Dm.list();
    }

    // tg: code/modules/reagents/chemistry/holder/holder.dm:35
    [DmParams("maximum", "new_flags")]
    public override async DmTask New(params object[] __extra)
    {
        float? maximum = Dm.arg<float?>(__extra, 0);
        float? new_flags = Dm.arg<float?>(__extra, 1);
        maximum ??= 100;
        new_flags ??= 0;
        maximum_volume = Dm.num(maximum);
        flags = new_flags;
    }

    // tg: code/modules/reagents/chemistry/holder/holder.dm:39
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        //We're about to delete all reagents, so lets cleanup
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
            qdel(reagent);
        reagent_list = null;
        if (Dm.truthy(is_reacting)) //If false, reaction list should be cleaned up
            Waiting.NoSleep(force_stop_reacting());
        QDEL_LAZYLIST(ref reaction_list);
        if (my_atom != null && Dm.eq(my_atom.reagents, this))
            my_atom.reagents = null;
        my_atom = null;
        return base.Destroy(force);
    }

    /**
     * Removes the specified amount of every reagent in the holder, in proportion to its volume
     *
     * Arguments
     *
     * * amount - the amount to remove
     * * relative - if TRUE amount is treated as an percentage between 0->1. If FALSE amount is the direct volume to remove
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:262  (M7 lighting slice L1: the glowstick burns its fluid down)
    public virtual async DmTask<object> remove_all(object amount = null, float? relative = null)
    {
        amount ??= 1;
        relative ??= FALSE;
        if (!Dm.truthy(total_volume))
            return FALSE;

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to remove all reagents []", amount), "code/modules/reagents/chemistry/holder/holder.dm", 267);
            return FALSE;
        }
        if (Dm.truthy(relative) && (Dm.num(amount) < 0 || Dm.num(amount) > 1))
        {
            stack_trace(Dm.text("illegal percentage value passed to remove all reagents []", amount), "code/modules/reagents/chemistry/holder/holder.dm", 270);
            return FALSE;
        }

        if (!Dm.truthy(relative))
            amount = min(Dm.num(amount), total_volume);
        amount = round(Dm.num(amount), CHEMICAL_QUANTISATION_LEVEL);
        if (Dm.num(amount) <= 0)
            return FALSE;

        DmList cached_reagents = reagent_list;
        float remove_amount;
        float total_removed_amount = 0;
        float part = Dm.num(amount);
        if (!Dm.truthy(relative))
            part /= total_volume;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            //reduce the volume
            remove_amount = cached_reagent.volume.GetValueOrDefault() * part;
            cached_reagent.volume -= remove_amount;

            total_removed_amount += remove_amount;
        }
        await update_total();

        return round(total_removed_amount, CHEMICAL_QUANTISATION_LEVEL);
    }

    /// Removes all reagents
    // tg: code/modules/reagents/chemistry/holder/holder.dm:397
    public virtual async DmTask clear_reagents()
    {
        DmList cached_reagents = reagent_list;

        //setting volume to 0 will allow update_total() to clean it up
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
            reagent.volume = 0;
        await update_total();
    }

    /**
     * Transfer some stuff from this holder to a target object
     *
     * Arguments:
     * * obj/target - Target to attempt transfer to
     * * amount - amount of reagent volume to transfer
     * * multiplier - multiplies each reagent amount by this number well byond their available volume before transfering. used to create reagents from thin air if you ever need to
     * * datum/reagent/target_id - transfer only this reagent in this holder leaving others untouched
     * * preserve_data - if preserve_data=0, the reagents data will be lost. Usefull if you use data for some strange stuff and don't want it to be transferred.
     * * no_react - passed through to [/datum/reagents/proc/add_reagent]
     * * mob/transferred_by - used for logging
     * * remove_blacklisted - skips transferring of reagents without REAGENT_CAN_BE_SYNTHESIZED in chemical_flags
     * * methods - passed through to [/datum/reagents/proc/expose] and [/datum/reagent/proc/on_transfer]
     * * show_message - passed through to [/datum/reagents/proc/expose]
     * * ignore_stomach - when using methods INGEST will not use the stomach as the target
     * * copy_only - transfers the reagents without removing it from this holder
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:422  (remove_blacklisted is object, rules 3.4: the plumbing filter's
    // override passes a list of reagent types in it, _plumb_reagents.dm:245)
    public virtual async DmTask<object> trans_to(object target = null, float? amount = null, float? multiplier = null, object target_id = null, float? preserve_data = null, float? no_react = null, mob transferred_by = null, object remove_blacklisted = null, float? methods = null, float? show_message = null, float? ignore_stomach = null, float? copy_only = null)
    {
        amount ??= 1;
        multiplier ??= 1;
        preserve_data ??= TRUE;
        no_react ??= FALSE;
        remove_blacklisted ??= FALSE;
        methods ??= NONE;
        show_message ??= TRUE;
        ignore_stomach ??= FALSE;
        copy_only ??= FALSE;
        if (QDELETED(target as datum) || !Dm.truthy(total_volume))
            return FALSE;

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to trans_to [] amount of reagents", amount), "code/modules/reagents/chemistry/holder/holder.dm", 440);
            return FALSE;
        }

        if (target_id != null && !ispath(target_id))
        {
            stack_trace(Dm.text("invalid target reagent id [] passed to trans_to", target_id), "code/modules/reagents/chemistry/holder/holder.dm", 444);
            return FALSE;
        }

        DmList cached_reagents = reagent_list;

        atom target_atom;
        datum_reagents target_holder;
        if (target is datum_reagents reagents_target)
        {
            target_holder = reagents_target;
            target_atom = target_holder.my_atom;
        }
        else
        {
            atom target_as_atom = target as atom;
            if (!Dm.truthy(ignore_stomach) && Dm.truthy(Dm.bitand(methods, INGEST)) && iscarbon(target))
            {
                mob_living_carbon eater = (mob_living_carbon)target;
                obj_item_organ_stomach belly = eater.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
                if (belly == null)
                {
                    float expel_amount = round(Dm.num(amount), CHEMICAL_QUANTISATION_LEVEL);
                    if (expel_amount > 0)
                        await eater.expel_ingested(my_atom, expel_amount);
                    return null;
                }
                target_holder = belly.reagents as datum_reagents;
                target_atom = belly;
            }
            else if (!Dm.truthy(target_as_atom.reagents))
                return null;
            else
            {
                target_holder = target_as_atom.reagents as datum_reagents;
                target_atom = target_as_atom;
            }
        }

        // Prevents small amount problems, as well as zero and below zero amounts.
        amount = round(min(Dm.num(amount), total_volume, target_holder.maximum_volume - target_holder.total_volume), CHEMICAL_QUANTISATION_LEVEL);
        if (amount <= 0)
            return FALSE;

        object trans_data = null;
        DmList r_to_send = Dm.truthy(methods) ? Dm.list() : null; // Validated list of reagents to be exposed
        // list(): only the combat log below reads it, so it is made only for a transfer with a transferrer (the stomach's drip
        // into the blood runs every Life tick, M8 slice A)
        DmList transfer_log = transferred_by != null ? Dm.list() : null;

        float part = target_id == null ? (Dm.num(amount) / total_volume) : 1;
        float transfer_amount;
        object transfered_amount;
        float total_transfered_amount = 0;

        //first add reagents to target
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.truthy(remove_blacklisted) && !Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_CAN_BE_SYNTHESIZED)))
                continue;

            if (target_id != null)
            {
                if (Dm.eq(reagent.type, target_id))
                    transfer_amount = min(Dm.num(amount), Dm.num(reagent.volume));
                else
                    continue;
            }
            else
                transfer_amount = Dm.num(reagent.volume) * part;

            if (Dm.truthy(preserve_data))
                trans_data = await copy_data(reagent);
            if (Dm.truthy(await reagent.intercept_reagents_transfer(target_holder, transfer_amount, copy_only)))
                continue;
            // CALLBACK(src, PROC_REF(_on_transfer_creation), ...): the callback only sends COMSIG_REAGENT_ON_TRANSFER, so it is made
            // only when something listens for it (a callback datum per reagent per stomach drip otherwise, M8 slice A)
            datum_callback creation_callback = Dm.truthy(reagent._listen_lookup?[COMSIG_REAGENT_ON_TRANSFER]) ? Dm.@new<datum_callback>(this, nameof(_on_transfer_creation), reagent, target_holder) : null;
            transfered_amount = await target_holder.add_reagent(reagent.type, transfer_amount * Dm.num(multiplier), trans_data as DmList, chem_temp, reagent.purity, reagent.ph, no_react: TRUE, reagent_added: r_to_send, creation_callback: creation_callback); //we only handle reaction after every reagent has been transferred.
            if (!Dm.truthy(transfered_amount))
                continue;

            total_transfered_amount += Dm.num(transfered_amount);
            if (!Dm.truthy(copy_only))
                reagent.volume = Dm.num(reagent.volume) - transfer_amount;
            transfer_log?.Add(Dm.text("[] ([]u, [] purity)", reagent.type, transfered_amount, reagent.purity));

            if (target_id != null)
                break;
        }
        if (!Dm.truthy(copy_only))
            await update_total();

        //expose target to reagent changes
        if (Dm.truthy(methods))
            await target_holder.expose(isorgan(target_atom) ? target as atom : target_atom, methods, 1, show_message, r_to_send);

        //combat log
        if (transferred_by != null && target_atom != null)
        {
            //logging mob holder
            atom log_target = target_atom;
            if (isorgan(target_atom))
            {
                obj_item_organ organ_item = (obj_item_organ)target_atom;
                log_target = organ_item.owner != null ? organ_item.owner : organ_item;
            }
            // tg: code/modules/reagents/chemistry/holder/holder.dm:522
            ((atom)log_target).add_hiddenprint(transferred_by); //log prints so admins can figure out who touched it last.

            //logging reagents
            log_combat(transferred_by, log_target, "transferred reagents to", my_atom, Dm.text("which had []", english_list(transfer_log)));
        }

        if (!Dm.truthy(no_react))
            await target_holder.handle_reactions();

        return total_transfered_amount;
    }

    ///For internal purposes. Sends a signal when a new reagent has been created in the target reagent holder upon transfer
    // tg: code/modules/reagents/chemistry/holder/holder.dm:538
    public virtual void _on_transfer_creation(datum_reagent reagent = null, datum_reagents target_holder = null, datum_reagent new_reagent = null)
    {
        // PRIVATE_PROC(TRUE)

        if (Dm.truthy(reagent._listen_lookup?[COMSIG_REAGENT_ON_TRANSFER]))
            reagent._SendSignal(COMSIG_REAGENT_ON_TRANSFER, Dm.list(reagent, target_holder, new_reagent));
    }

    /// Updates [/datum/reagents/var/total_volume]
    // tg: code/modules/reagents/chemistry/holder/holder.dm:584
    public virtual async DmTask<object> update_total()
    {
        object dot = null;
        DmList cached_reagents = reagent_list;
        DmList deleted_reagents = null; // list(): made at the first deletion (every Life tick updates every body's holder, M8 slice A)
        float chem_index = 1;
        float num_reagents = length(cached_reagents);
        float total_ph = 0;
        float reagent_volume = 0;
        dot = 0;

        //responsible for removing reagents and computing total ph & volume
        //all its code was taken out of del_reagent() initially for efficiency purposes
        while (chem_index <= num_reagents)
        {
            datum_reagent reagent = cached_reagents[chem_index] as datum_reagent;
            chem_index += 1;
            reagent_volume = round(Dm.num(reagent.volume), CHEMICAL_QUANTISATION_LEVEL); //round to this many decimal places

            //remove very small amounts of reagents
            if (reagent_volume <= 0 || (!Dm.truthy(is_reacting) && reagent_volume < CHEMICAL_VOLUME_ROUNDING))
            {
                //end metabolization
                if (isliving(my_atom))
                {
                    mob_living living_atom = (mob_living)my_atom;
                    if (Dm.truthy(reagent.metabolizing))
                    {
                        reagent.metabolizing = FALSE;
                        await reagent.on_mob_end_metabolize(living_atom);
                    }
                    await reagent.on_mob_delete(living_atom);
                }

                //removing it and store in a seperate list for processing later
                cached_reagents.Remove(reagent);
                (deleted_reagents ??= Dm.list()).Add(reagent);

                //move pointer back so we don't overflow & decrease length
                chem_index -= 1;
                num_reagents -= 1;
                continue;
            }

            //compute volume & ph like we would normally
            dot = Dm.num(dot) + reagent_volume;
            total_ph += reagent.ph * reagent_volume;

            //reasign rounded value
            reagent.volume = reagent_volume;
        }

        //assign the final values, rounding up can sometimes cause overflow so bring it down
        total_volume = min(round(Dm.num(dot), CHEMICAL_VOLUME_ROUNDING), maximum_volume);
        if (!Dm.truthy(total_volume))
            ph = CHEMICAL_NORMAL_PH;
        else
            ph = clamp(Dm.div(total_ph, total_volume), CHEMICAL_MIN_PH, CHEMICAL_MAX_PH);

        //clear out deleted reagents
        QDEL_LIST(deleted_reagents);

        //inform hooks about reagent changes
        if (Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_HOLDER_UPDATED]))
            _SendSignal(COMSIG_REAGENTS_HOLDER_UPDATED, Dm.list(this));
        return dot;
    }

    /**
     * Copies the reagent data from the passed reagent to this holder (or any other holder via the arguments)
     * Arguments
     * [current_reagent][datum/reagent] - the reagent(not typepath) to copy data from
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:643
    public virtual async DmTask<object> copy_data(datum_reagent current_reagent = null)
    {
        if (current_reagent == null || !Dm.truthy(current_reagent.data))
            return null;
        if (current_reagent.data is not DmList data)
            return current_reagent.data;

        DmList trans_data = data.Copy();

        // We do this so that introducing a virus to a blood sample
        // doesn't automagically infect all other blood samples from
        // the same donor.
        //
        // Technically we should probably copy all data lists, but
        // that could possibly eat up a lot of memory needlessly
        // if most data lists are read-only.
        if (Dm.truthy(trans_data["viruses"]))
        {
            DmList viruses = Dm.list(); // gaps VI1
            foreach (datum_disease disease in Dm.each_cast<datum_disease>(trans_data["viruses"]))
                Dm.append(viruses, await disease.Copy());
            trans_data["viruses"] = viruses;
        }

        return trans_data;
    }

    /**
     * Check if this holder contains this reagent.
     * Reagent takes a PATH to a reagent.
     * Amount checks for having a specific amount of that chemical.
     * Needs matabolizing takes into consideration if the chemical is matabolizing when it's checked.
     * Check subtypes controls whether it should it should also include subtypes: ispath(type, reagent) versus type == reagent.
     *
     * * [target_reagent][datum/reagent] - the reagent typepath to check for. can be null to return any reagent
     * * amount - checks for having a specific amount of that chemical
     * * needs_metabolizing - takes into consideration if the chemical is matabolizing when it's checked.
     * * check_subtypes - controls whether it should it should also include subtypes: ispath(type, reagent) versus type == reagent.
     * * chemical_flags - checks for reagent flags.
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:677
    public virtual object has_reagent(object target_reagent = null, float? amount = null, float? needs_metabolizing = null, float? check_subtypes = null, float? chemical_flags = null)
    {
        amount ??= -1;
        needs_metabolizing ??= FALSE;
        check_subtypes ??= FALSE;
        chemical_flags ??= NONE;
        if (target_reagent != null && !ispath(target_reagent))
        {
            stack_trace(Dm.text("invalid reagent path passed to has reagent []", target_reagent), "code/modules/reagents/chemistry/holder/holder.dm", 685);
            return FALSE;
        }

        DmList cached_reagents = reagent_list;
        foreach (datum_reagent holder_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            //finding for a specific reagent
            if (target_reagent != null)
            {
                //first find for specific type or subtype
                if (!Dm.truthy(check_subtypes))
                {
                    if (!Dm.eq(holder_reagent.type, target_reagent))
                        continue;
                }
                else if (!istype(holder_reagent, target_reagent as Type))
                    continue;
            }

            //next check if we have the requested amount
            if (amount > 0 && Dm.num(holder_reagent.volume) < amount)
                continue;

            //next check for metabolization
            if (Dm.truthy(needs_metabolizing) && !Dm.truthy(holder_reagent.metabolizing))
                continue;

            //next check if it has the specified flag
            if (Dm.truthy(chemical_flags) && !Dm.truthy(Dm.bitand(holder_reagent.chemical_flags, chemical_flags)))
                continue;

            //after all that if we get here then we have found our reagent
            return holder_reagent;
        }

        return FALSE;
    }

    //================================Exposure(to apply reagent effects)======================
    /**
     * Applies the relevant expose_ proc for every reagent in this holder
     * * [/datum/reagent/proc/expose_mob]
     * * [/datum/reagent/proc/expose_turf]
     * * [/datum/reagent/proc/expose_obj]
     *
     * Arguments
     * - Atom/target: What mob/turf/object is being exposed to reagents? This is your reaction target.
     * - Methods: What reaction type is the reagent itself going to call on the reaction target? Types are TOUCH, INGEST, VAPOR, PATCH, INJECT and INHALE.
     * - Volume_modifier: reagent volume modifier applied to the reagents(passed to r_to_expose or this holder) before exposing. Must be between 0->1
     * - Show_message: Whether to display anything to mobs when they are exposed.
     * - list/datum/reagent/r_to_expose: map of[/datum/reagent -> amount] when you to want to expose specific reagents with precise amounts
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:744
    public virtual async DmTask<object> expose(atom target = null, float? methods = null, float? volume_modifier = null, float? show_message = null, DmList r_to_expose = null)
    {
        methods ??= TOUCH;
        volume_modifier ??= 1;
        show_message ??= 1;
        if (target == null)
            return null;

        if (volume_modifier <= 0)
        {
            stack_trace(Dm.text("Volume modifier [] must be +ve", volume_modifier), "code/modules/reagents/chemistry/holder/holder.dm", 749);
            return null;
        }

        DmList reagents = Dm.list();
        if (r_to_expose == null)
        {
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
                reagents[reagent] = Dm.num(reagent.volume) * volume_modifier;
        }
        else
        {
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(r_to_expose))
                reagents[reagent] = Dm.num(r_to_expose[reagent]) * volume_modifier;
        }

        // DEVIATION(DV27): the interaction guard (D132) on reagents reaching an atom: a type whose own expose_reagents is not
        // translated (a fish, a hydroponics tray, a spider) is not exposed (its parent's expose_reagents does not run in its
        // place).
        if (InertSeam.MissingOverride(target, "expose_reagents"))
            return null;
        return await target.expose_reagents(reagents, this, methods, show_message);
    }

    /**
     * Applies heat to this holder
     * Arguments
     *
     * * temperature - the temperature we to heat/cool by
     * * coeff - multiplier to be applied on temp diff between param temp and current temp
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:769
    public virtual async DmTask expose_temperature(object temperature = null, float? coeff = null)
    {
        coeff ??= 0.02f;
        if (Dm.truthy(Dm.bitand(flags, NO_REACT))) //stasis holders IE cryobeaker
            return;
        float temp_delta = (Dm.num(temperature) - chem_temp) * Dm.num(coeff);
        if (temp_delta > 0)
            chem_temp = min(chem_temp + max(temp_delta, 1), Dm.num(temperature));
        else
            chem_temp = max(chem_temp + min(temp_delta, -1), Dm.num(temperature));
        set_temperature(round(chem_temp));
        await handle_reactions();
    }
}

// tg: code/modules/reagents/chemistry/holder/holder.dm:60 (interleaved types, rules 1.9)
public partial class atom : datum
{
    /**
     * Convenience proc to create a reagents holder for an atom
     *
     * Arguments:
     * * max_vol - maximum volume of holder
     * * flags - flags to pass to the holder
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:60
    public virtual void create_reagents(object max_vol = null, object flags = null)
    {
        if (Dm.truthy(reagents))
            qdel(reagents);
        reagents = Dm.@new<datum_reagents>(max_vol, flags);
        ((datum_reagents)reagents).my_atom = this;
    }
}

// tg: code/modules/reagents/chemistry/holder/holder.dm:66 (M4a P4a: add_reagent and remove_reagent, the welder's fuel)
public partial class datum_reagents : datum
{
    /**
     * Adds a reagent to this holder
     *
     * Arguments:
     * * reagent - The reagent id to add
     * * amount - Amount to add
     * * list/data - Any reagent data for this reagent, used for transferring data with reagents
     * * reagtemp - Temperature of this reagent, will be equalized
     * * no_react - prevents reactions being triggered by this addition
     * * added_purity - override to force a purity when added
     * * added_ph - override to force a pH when added
     * * override_base_ph - ingore the present pH of the reagent, and instead use the default (i.e. if buffers/reactions alter it)
     * * list/reagent_added - If not null will contain an map of [reagent datum->amount added] which holds the inverse chems added to mobs. Clear the list to erase old values
     * * creation_callback - Callback to invoke when the reagent is created
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:81
    public virtual async DmTask<object> add_reagent(object reagent_type = null, object amount = null, DmList data = null, float? reagtemp = null, object added_purity = null, object added_ph = null, float? no_react = null, float? override_base_ph = null, DmList reagent_added = null, datum_callback creation_callback = null)
    {
        reagtemp ??= DEFAULT_REAGENT_TEMPERATURE;
        no_react ??= FALSE;
        override_base_ph ??= FALSE;
        if (!ispath(reagent_type))
        {
            stack_trace(Dm.text("invalid reagent passed to add reagent []", reagent_type), "code/modules/reagents/chemistry/holder/holder.dm", 94);
            return FALSE;
        }

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to add reagent [] []", amount, reagent_type), "code/modules/reagents/chemistry/holder/holder.dm", 98);
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_PRE_ADD_REAGENT]) ? NONE : _SendSignal(COMSIG_REAGENTS_PRE_ADD_REAGENT, Dm.list(this, reagent_type, amount, reagtemp, data, no_react)), COMPONENT_CANCEL_REAGENT_ADD)))
            return FALSE;

        // var/datum/reagent/glob_reagent = GLOB.chemical_reagents_list[reagent_type]
        // GAP(lead): GLOB.chemical_reagents_list (_globalvars/lists/reagents.dm:50, init_chemical_reagent_list) is not
        // built: it creates one datum of every valid /datum/reagent subtype at boot, each with a random mass (rand, which
        // would shift every world's random sequence). Its entries are only read for vars tg never changes on them, so the
        // type's prototype (its initial values, read only) stands in for the entry, and "no entry" is the list's own test:
        // not a valid (non-abstract) /datum/reagent subtype.
        Type reagent_path = reagent_type as Type;
        if (!ispath(reagent_path, typeof(datum_reagent)) || reagent_path == typeof(datum_reagent) || Dm.@in(reagent_path, get_abstract_types()))
        {
            stack_trace(Dm.text("[] attempted to add a reagent called '[]' which doesn't exist. ([])", my_atom, reagent_type, usr), "code/modules/reagents/chemistry/holder/holder.dm", 105);
            return FALSE;
        }
        datum_reagent glob_reagent = Dm.initial(reagent_path, (datum_reagent p) => p);
        if (!Dm.truthy(added_purity)) //Because purity additions can be 0
            added_purity = glob_reagent.creation_purity; //Usually 1
        if (!Dm.truthy(added_ph))
            added_ph = glob_reagent.ph;

        //Split up the reagent if it's in a mob
        if (Dm.truthy(Dm.bitand(flags, REAGENT_HOLDER_ALIVE)))
        {
            amount = await process_mob_reagent_purity(glob_reagent, amount, added_purity, reagent_added);
            if (Dm.num(amount) <= 0) //Inverse or nothing was added. return true amount
                return Dm.num(amount) * -1;
        }

        float cached_total = total_volume;
        if (cached_total + Dm.num(amount) > maximum_volume)
            amount = maximum_volume - cached_total; //Doesnt fit in. Make it disappear. shouldn't happen. Will happen.
        amount = round(Dm.num(amount), CHEMICAL_QUANTISATION_LEVEL);
        if (Dm.num(amount) <= 0)
            return FALSE;

        float cached_temp = chem_temp;
        DmList cached_reagents = reagent_list;

        //Equalize temperature - Not using specific_heat() because the new chemical isn't in yet.
        float old_heat_capacity = 0;
        if (reagtemp != cached_temp)
        {
            foreach (datum_reagent iter_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
                old_heat_capacity += iter_reagent.specific_heat * Dm.num(iter_reagent.volume);
        }

        //add the reagent to the existing if it exists
        foreach (datum_reagent iter_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (iter_reagent.type == reagent_path)
            {
                if (Dm.truthy(override_base_ph))
                    added_ph = iter_reagent.ph;
                iter_reagent.purity = Dm.num(Dm.div((iter_reagent.creation_purity * Dm.num(iter_reagent.volume)) + (Dm.num(added_purity) * Dm.num(amount)), Dm.num(iter_reagent.volume) + Dm.num(amount))); //This should add the purity to the product
                iter_reagent.creation_purity = iter_reagent.purity;
                iter_reagent.ph = Dm.num(Dm.div((iter_reagent.ph * Dm.num(iter_reagent.volume)) + (Dm.num(added_ph) * Dm.num(amount)), Dm.num(iter_reagent.volume) + Dm.num(amount)));
                await iter_reagent.on_merge(data, amount); // Update this before updating volume. FIXME: Move all of the surrounding crap into this proc so implementations decide whether they go first or base code goes first.
                iter_reagent.volume = Dm.num(iter_reagent.volume) + Dm.num(amount);
                await update_total();

                if (reagtemp != cached_temp)
                {
                    object new_heat_capacity = heat_capacity();
                    if (Dm.truthy(new_heat_capacity))
                        set_temperature(Dm.div((old_heat_capacity * cached_temp) + (iter_reagent.specific_heat * Dm.num(amount) * Dm.num(reagtemp)), new_heat_capacity));
                    else
                        set_temperature(reagtemp);
                }

                if (reagent_added != null)
                    reagent_added[iter_reagent] = amount;
                if (!Dm.truthy(no_react) && !Dm.truthy(is_reacting)) //To reduce the amount of calculations for a reaction the reaction list is only updated on a reagents addition.
                    await handle_reactions();
                return amount;
            }
        }

        if (!Dm.truthy(is_reacting) && Dm.num(amount) < CHEMICAL_VOLUME_ROUNDING)
            return 0;

        //otherwise make a new one
        datum_reagent new_reagent = Dm.@new(reagent_path, data) as datum_reagent;
        cached_reagents.Add(new_reagent);
        new_reagent.holder = this;
        new_reagent.volume = Dm.num(amount);
        new_reagent.purity = Dm.num(added_purity);
        new_reagent.creation_purity = Dm.num(added_purity);
        new_reagent.ph = Dm.num(added_ph);
        await new_reagent.on_new(data);
        if (creation_callback != null)
            await creation_callback.Invoke(new_reagent);

        if (isliving(my_atom))
            await new_reagent.on_mob_add((mob_living)my_atom, amount); //Must occur before it could posibly run on_mob_delete

        await update_total();
        if (reagtemp != cached_temp)
        {
            object new_heat_capacity = heat_capacity();
            if (Dm.truthy(new_heat_capacity))
                set_temperature(Dm.div((old_heat_capacity * cached_temp) + (new_reagent.specific_heat * Dm.num(amount) * Dm.num(reagtemp)), new_heat_capacity));
            else
                set_temperature(reagtemp);
        }

        if (reagent_added != null)
            reagent_added[new_reagent] = amount;
        if (!Dm.truthy(no_react))
            await handle_reactions();
        return amount;
    }

    /**
     * Removes a specific reagent. can supress reactions if needed
     * Arguments
     *
     * * [reagent_type][datum/reagent] - the type of reagent
     * * amount - the volume to remove
     * * include_subtypes - if TRUE will remove the specified amount from all subtypes of reagent_type as well
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:212
    public virtual async DmTask<object> remove_reagent(object reagent_type = null, object amount = null, float? include_subtypes = null)
    {
        include_subtypes ??= FALSE;
        if (!ispath(reagent_type))
        {
            stack_trace(Dm.text("invalid reagent passed to remove reagent []", reagent_type), "code/modules/reagents/chemistry/holder/holder.dm", 214);
            return FALSE;
        }

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to remove reagent [] []", amount, reagent_type), "code/modules/reagents/chemistry/holder/holder.dm", 218);
            return FALSE;
        }

        amount = round(Dm.num(amount), CHEMICAL_QUANTISATION_LEVEL);
        if (Dm.num(amount) <= 0)
            return FALSE;

        float total_removed_amount = 0;
        float remove_amount = 0;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            //check for specific type or subtypes
            if (!Dm.truthy(include_subtypes))
            {
                if (!Dm.eq(cached_reagent.type, reagent_type))
                    continue;
            }
            else if (!istype(cached_reagent, reagent_type as Type))
                continue;

            //reduce the volume
            remove_amount = min(Dm.num(cached_reagent.volume), Dm.num(amount));
            cached_reagent.volume = Dm.num(cached_reagent.volume) - remove_amount;

            total_removed_amount += remove_amount;

            //if we reached here means we have found our specific reagent type so break
            if (!Dm.truthy(include_subtypes))
                break;
        }

        //update the holder & handle reactions
        await update_total();

        return total_removed_amount;
    }
}

// tg: code/modules/reagents/chemistry/holder/holder.dm:787 (M4c slice 2: a power cell's try_explode asks its reagents)
public partial class datum_reagents : datum
{
    // tg: code/modules/reagents/chemistry/holder/holder.dm:787
    public virtual async DmTask<object> spark_act(object power_charge = null, object spark_flags = null, object banned_reagents = null)
    {
        if (!islist(banned_reagents))
            banned_reagents = Dm.list(banned_reagents);
        object result = NONE;
        object update = FALSE;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
        {
            if (Dm.truthy(is_type_in_list(reagent, banned_reagents as DmList)))
                continue;
            object reagent_result = await reagent.on_spark_act(power_charge == null ? null : Dm.num(power_charge), spark_flags == null ? null : Dm.num(spark_flags));
            if (!Dm.truthy(reagent_result))
                continue;
            result = Dm.bitor(result, Dm.bitand(reagent_result, SPARK_ACT_RETURNS));
            if (!Dm.truthy(Dm.bitand(reagent_result, SPARK_ACT_KEEP_REAGENT)))
            {
                reagent.volume = 0;
                update = TRUE;
            }
        }

        if (Dm.truthy(Dm.bitand(result, SPARK_ACT_CLEAR_ALL)))
            await clear_reagents();
        else if (Dm.truthy(update))
            await update_total();
        return result;
    }

    //===============================Logging==========================================
    /// Outputs a log-friendly list of reagents based on the internal reagent_list.
    // tg: code/modules/reagents/chemistry/holder/holder.dm:811
    public virtual object get_reagent_log_string()
    {
        if (!Dm.truthy(length(reagent_list)))
            return "no reagents";

        DmList data = Dm.list();

        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
            data.Add(Dm.text("[] []u, [] purity)", reagent.type, reagent.volume, reagent.purity));

        return english_list(data);
    }
}

// tg: code/modules/reagents/chemistry/holder/holder.dm:295 (interleaved types, rules 1.9; M5x slice 3: the fuel tank's boom)
public partial class datum_reagents : datum
{
    /**
     * Removes all reagents of the specified type
     *
     * Arguments
     *
     * * [target_reagent_typepath][/datum/reagent] - the typepath of the reagent to remove
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:301
    public virtual async DmTask<object> del_reagent(object target_reagent_typepath = null)
    {
        if (!ispath(target_reagent_typepath))
        {
            stack_trace(Dm.text("invalid reagent path passed to del reagent []", target_reagent_typepath), "code/modules/reagents/chemistry/holder/holder.dm", 303);
            return FALSE;
        }

        //setting the volume to 0 will allow update_total() to clear it up for us
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (reagent.type == (target_reagent_typepath as Type))
            {
                reagent.volume = 0;
                await update_total();
                return TRUE;
            }
        }

        return FALSE;
    }
}

// tg: code/modules/reagents/chemistry/holder/holder.dm:199 (M8 slice A: add_reagent_list, convert_reagent, multiply and
// get_master_reagent)
public partial class datum_reagents : datum
{
    /**
     * Like add_reagent but you can enter a list.
     * Arguments
     *
     * * [list_reagents][list] - list to add. Format it like this: list(/datum/reagent/toxin = 10, "beer" = 15)
     * * [data][list] - additional data to add
     * * [added_purity][number] - an override to the default purity for each reagent to add.
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:199
    public virtual async DmTask add_reagent_list(DmList list_reagents = null, DmList data = null, object added_purity = null)
    {
        foreach (object r_id in Dm.each(list_reagents))
        {
            object amt = list_reagents[r_id];
            await add_reagent(r_id, amt, data, added_purity: added_purity);
        }
    }

    /**
     * Turn one reagent into another, preserving volume, temp, purity, ph
     * Arguments
     *
     * * [source_reagent_typepath][/datum/reagent] - the typepath of the reagent you are trying to convert
     * * [target_reagent_typepath][/datum/reagent] - the final typepath the source_reagent_typepath will be converted into
     * * conversion_volume - how much of the reagent volume to convert
     * * multiplier - the multiplier applied on the source_reagent_typepath volume before converting
     * * include_source_subtypes- if TRUE will convert all subtypes of source_reagent_typepath into target_reagent_typepath as well
     * * keep_data - works only when include_source_subtypes is FALSE. Transfers over the data of the converted reagent
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:327
    public virtual async DmTask<object> convert_reagent(object source_reagent_typepath = null, object target_reagent_typepath = null, object conversion_volume = null, float? multiplier = null, float? include_source_subtypes = null, float? keep_data = null)
    {
        conversion_volume ??= total_volume;
        multiplier ??= 1;
        include_source_subtypes ??= FALSE;
        keep_data ??= FALSE;
        if (!Dm.truthy(total_volume))
            return FALSE;
        if (!ispath(source_reagent_typepath))
        {
            stack_trace(Dm.text("invalid reagent path passed to convert reagent []", source_reagent_typepath), "code/modules/reagents/chemistry/holder/holder.dm", 339);
            return FALSE;
        }
        if (!ispath(target_reagent_typepath))
        {
            stack_trace(Dm.text("invalid reagent path passed to convert reagent []", target_reagent_typepath), "code/modules/reagents/chemistry/holder/holder.dm", 342);
            return FALSE;
        }
        if (Dm.num(conversion_volume) <= 0 || Dm.num(conversion_volume) > total_volume)
        {
            stack_trace(Dm.text("conversion volume [] out of bounds range is 0<value<=[]", conversion_volume, total_volume), "code/modules/reagents/chemistry/holder/holder.dm", 345);
            return FALSE;
        }
        keep_data = Dm.truthy(keep_data) && !Dm.truthy(include_source_subtypes) ? TRUE : FALSE;

        float weighted_volume = 0;
        float weighted_purity = 0;
        float weighted_ph = 0;
        float reagent_volume = 0;
        object reagent_data = null;

        DmList cached_reagents = reagent_list;
        foreach (datum_reagent cached_reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            //check for specific type or subtypes
            if (!Dm.truthy(include_source_subtypes))
            {
                if (!Dm.eq(cached_reagent.type, source_reagent_typepath))
                    continue;
            }
            else if (!istype(cached_reagent, source_reagent_typepath as Type))
                continue;

            //check conversion threshold. stop if we have reached our target
            reagent_volume = Dm.num(cached_reagent.volume);
            if (Dm.num(cached_reagent.volume) > Dm.num(conversion_volume))
            {
                reagent_volume = Dm.num(conversion_volume);
                cached_reagent.volume = Dm.num(cached_reagent.volume) - Dm.num(conversion_volume);
                conversion_volume = 0;
            }
            else
            {
                conversion_volume = Dm.num(conversion_volume) - Dm.num(cached_reagent.volume);
                cached_reagent.volume = 0;
            }

            //compute average of everything. preserve data if nessassary
            weighted_purity += cached_reagent.purity * reagent_volume;
            weighted_ph += cached_reagent.ph * reagent_volume;
            weighted_volume += reagent_volume;
            if (Dm.truthy(keep_data))
                reagent_data = await copy_data(cached_reagent);

            //stop if we found our specific reagent or reached the conversion threshold
            if (!Dm.truthy(include_source_subtypes) || !Dm.truthy(conversion_volume))
                break;
        }

        //add the new target reagent with the averaged values from the source reagents
        if (weighted_volume > 0)
        {
            await update_total();
            await add_reagent(target_reagent_typepath, weighted_volume * multiplier.Value, data: reagent_data as DmList, reagtemp: chem_temp, added_purity: weighted_purity / weighted_volume, added_ph: weighted_ph / weighted_volume);
        }
        return null;
    }

    /**
     * Multiplies reagents inside this holder by a specific amount
     * Arguments
     *
     * * multiplier - the amount to multiply each reagent, its a percentile value where < 1 will reduce the volume and
     * * > 1 will increase the volume. Final multiplier applied to the reagent volume is (1 - multiplier)
     * * datum/reagent/target_id - multiply only this reagent in this holder leaving others untouched
     */
    // tg: code/modules/reagents/chemistry/holder/holder.dm:551
    public virtual async DmTask multiply(float? multiplier = null, object target_id = null)
    {
        multiplier ??= 1;
        if (!Dm.truthy(total_volume))
            return;

        multiplier = round(min(multiplier.Value, maximum_volume / total_volume), CHEMICAL_QUANTISATION_LEVEL);
        if (multiplier < 0 || multiplier == 1)
            return;

        if (target_id != null && !ispath(target_id))
        {
            stack_trace(Dm.text("Bad reagent path [] passed to multiply", target_id), "code/modules/reagents/chemistry/holder/holder.dm", 560);
            return;
        }

        float change = (multiplier.Value - 1); //Get the % change
        float reagent_change;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (target_id != null && !Dm.eq(reagent.type, target_id))
                continue;

            reagent_change = Dm.num(reagent.volume) * change;
            if (change > 0)
                await add_reagent(reagent.type, reagent_change, added_purity: reagent.purity, added_ph: reagent.ph, no_react: TRUE);
            else
                reagent.volume = Dm.num(reagent.volume) + reagent_change;

            if (target_id != null)
                break;
        }

        if (change < 0)
            await update_total();
        await handle_reactions();
    }

    /// Get a reference to the reagent there is the most of in this holder
    // tg: code/modules/reagents/chemistry/holder/holder.dm:718
    public virtual object get_master_reagent()
    {
        DmList cached_reagents = reagent_list;
        datum_reagent master = null;
        float max_volume = 0;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.num(reagent.volume) > max_volume)
            {
                max_volume = Dm.num(reagent.volume);
                master = reagent;
            }
        }

        return master;
    }
}
