// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/_plumb_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file.

/**
 * Specialized reagent container for plumbing. Uses the round robin approach of transferring reagents
 * so transfer 5 from 15 water, 15 sugar and 15 plasma becomes 10, 15, 15 instead of 13.3333, 13.3333 13.3333. Good if you hate floating point errors
 */
// tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:6
public partial class datum_reagents_plumbing : datum_reagents
{
    /**
     * Same as the parent trans_to except only a few arguments have impact here & the rest of the arguments are discarded.
     * Arguments
     *
     * * atom/target - the target we are transfering to
     * * amount - amount to transfer
     * * datum/reagent/target_id - the reagent id we want to transfer. if null everything gets transfered
     * * methods - this is key for deciding between round-robin or proportional transfer. It does not mean the same as the
     * parent proc. LINEAR for round robin(in this technique reagents are missing/lost/not preserved when there isn't enough space to hold them)
     * NONE means everything is transfered regardless of how much space is available in the receiver in proportions
     */
    // tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:19
    public override async DmTask<object> trans_to(object target = null, float? amount = null, float? multiplier = null, object target_id = null, float? preserve_data = null, float? no_react = null, mob transferred_by = null, object remove_blacklisted = null, float? methods = null, float? show_message = null, float? ignore_stomach = null, float? copy_only = null)
    {
        amount ??= 1;
        methods ??= LINEAR; //default round robin technique for transferring reagents
        if (QDELETED(target as datum) || !Dm.truthy(total_volume))
            return FALSE;

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to trans_to [] amount of reagents", amount), "code/modules/plumbing/plumbers/_plumb_reagents.dm", 37);
            return FALSE;
        }

        if (target_id != null && !ispath(target_id))
        {
            stack_trace(Dm.text("invalid target reagent id [] passed to trans_to", target_id), "code/modules/plumbing/plumbers/_plumb_reagents.dm", 41);
            return FALSE;
        }

        datum_reagents target_holder;
        if (istype(target, typeof(datum_reagents)))
            target_holder = (datum_reagents)target;
        else
            target_holder = ((atom)target).reagents as datum_reagents;

        // Prevents small amount problems, as well as zero and below zero amounts.
        amount = round(min(amount.Value, total_volume, target_holder.maximum_volume - target_holder.total_volume), CHEMICAL_QUANTISATION_LEVEL);
        if (amount <= 0)
            return FALSE;

        DmList cached_reagents = reagent_list;
        float transfer_amount;
        object transfered_amount;
        float total_transfered_amount = 0;

        float round_robin = Dm.bitand(methods.Value, LINEAR);
        float part = 0;
        float to_transfer = 0;
        if (Dm.truthy(round_robin))
            to_transfer = amount.Value;
        else
            part = amount.Value / total_volume;

        //first add reagents to target
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.truthy(round_robin) && !Dm.truthy(to_transfer))
                break;

            if (target_id != null)
            {
                if (Dm.eq(reagent.type, target_id))
                    transfer_amount = min(amount.Value, Dm.num(reagent.volume));
                else
                    continue;
            }
            else
            {
                if (Dm.truthy(round_robin))
                    transfer_amount = min(to_transfer, Dm.num(reagent.volume));
                else
                    transfer_amount = Dm.num(reagent.volume) * part;
            }

            if (Dm.truthy(await reagent.intercept_reagents_transfer(target_holder, transfer_amount)))
                continue;

            transfered_amount = await target_holder.add_reagent(reagent.type, transfer_amount, await copy_data(reagent) as DmList, chem_temp, reagent.purity, reagent.ph, no_react: TRUE); //we only handle reaction after every reagent has been transferred.
            if (!Dm.truthy(transfered_amount))
                continue;
            total_transfered_amount += Dm.num(transfered_amount);
            if (Dm.truthy(round_robin))
                to_transfer -= Dm.num(transfered_amount);
            reagent.volume = Dm.num(reagent.volume) - Dm.num(transfered_amount);

            if (target_id != null)
                break;
        }
        await update_total();

        return total_transfered_amount;
    }
}

///Excludes catalysts during the emptying process
// tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:102
public partial class datum_reagents_plumbing_reaction_chamber : datum_reagents_plumbing
{
    ///Returns the total volume of reagents without the catalysts
    // tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:105
    public virtual object get_catalyst_excluded_volume()
    {
        float dot = 0;

        //no reagents
        if (!Dm.truthy(total_volume))
            return dot;

        obj_machinery_plumbing_reaction_chamber reactor = (obj_machinery_plumbing_reaction_chamber)my_atom;
        DmList catalysts = reactor.catalysts;

        //no catalysts
        if (!Dm.truthy(catalysts.len))
            return total_volume;

        //filter out catalysts except when we have excess of them
        float working_volume;
        object catalyst_volume;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            catalyst_volume = catalysts[reagent.type];
            working_volume = Dm.num(reagent.volume);

            //regular reagent add to total as normal
            if (!Dm.truthy(catalyst_volume))
            {
                dot += working_volume;
                continue;
            }

            //only add the excess to total as that's what will get transferred
            if (working_volume > Dm.num(catalyst_volume))
                dot += working_volume - Dm.num(catalyst_volume);
        }
        dot = min(round(dot, CHEMICAL_VOLUME_ROUNDING), maximum_volume);
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:139
    public override async DmTask<object> trans_to(object target = null, float? amount = null, float? multiplier = null, object target_id = null, float? preserve_data = null, float? no_react = null, mob transferred_by = null, object remove_blacklisted = null, float? methods = null, float? show_message = null, float? ignore_stomach = null, float? copy_only = null)
    {
        amount ??= 1;
        methods ??= LINEAR;
        obj_machinery_plumbing_reaction_chamber reactor = (obj_machinery_plumbing_reaction_chamber)my_atom;
        DmList catalysts = reactor.catalysts;

        //usual stuff
        if (!Dm.truthy(catalysts.len))
            return await base.trans_to(target, amount, multiplier, target_id, preserve_data, no_react, transferred_by, remove_blacklisted, methods, show_message, ignore_stomach, copy_only);

        if (QDELETED(target as datum))
            return FALSE;

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to trans_to [] amount of reagents", amount), "code/modules/plumbing/plumbers/_plumb_reagents.dm", 164);
            return FALSE;
        }

        if (target_id != null && !ispath(target_id))
        {
            stack_trace(Dm.text("invalid target reagent id [] passed to trans_to", target_id), "code/modules/plumbing/plumbers/_plumb_reagents.dm", 168);
            return FALSE;
        }

        datum_reagents target_holder;
        if (istype(target, typeof(datum_reagents)))
            target_holder = (datum_reagents)target;
        else
            target_holder = ((atom)target).reagents as datum_reagents;
        DmList cached_reagents = reagent_list;

        float actual_volume = Dm.num(get_catalyst_excluded_volume());

        // Prevents small amount problems, as well as zero and below zero amounts.
        amount = round(min(amount.Value, actual_volume, target_holder.maximum_volume - target_holder.total_volume), CHEMICAL_QUANTISATION_LEVEL);
        if (amount <= 0)
            return FALSE;

        float working_volume;
        object catalyst_volume;
        float transfer_amount;
        object transfered_amount;
        float total_transfered_amount = 0;

        float round_robin = Dm.bitand(methods.Value, LINEAR);
        float part = 0;
        float to_transfer = 0;
        if (Dm.truthy(round_robin))
            to_transfer = amount.Value;
        else
            part = amount.Value / actual_volume;

        //first add reagents to target
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.truthy(round_robin) && !Dm.truthy(to_transfer))
                break;
            working_volume = Dm.num(reagent.volume);

            catalyst_volume = catalysts[reagent.type];
            if (Dm.truthy(catalyst_volume)) //we have a working catalyst
            {
                if (Dm.num(reagent.volume) <= Dm.num(catalyst_volume)) //dont transfer since we have the required volume
                    continue;
                else
                    working_volume -= Dm.num(catalyst_volume); //dump out the excess
            }

            if (target_id != null)
            {
                if (Dm.eq(reagent.type, target_id))
                    transfer_amount = min(amount.Value, working_volume);
                else
                    continue;
            }
            else
            {
                if (Dm.truthy(round_robin))
                    transfer_amount = min(to_transfer, working_volume);
                else
                    transfer_amount = working_volume * part;
            }

            if (Dm.truthy(await reagent.intercept_reagents_transfer(target_holder, transfer_amount)))
                continue;

            transfered_amount = await target_holder.add_reagent(reagent.type, transfer_amount, await copy_data(reagent) as DmList, chem_temp, reagent.purity, reagent.ph, no_react: TRUE); //we only handle reaction after every reagent has been transferred.
            if (!Dm.truthy(transfered_amount))
                continue;
            total_transfered_amount += Dm.num(transfered_amount);
            if (Dm.truthy(round_robin))
                to_transfer -= Dm.num(transfered_amount);
            reagent.volume = Dm.num(reagent.volume) - Dm.num(transfered_amount);

            if (target_id != null)
                break;
        }
        await update_total();

        return total_transfered_amount;
    }
}

///Excludes blacklisted reagents during the transfer process
// tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:243
public partial class datum_reagents_plumbing_filter : datum_reagents_plumbing
{
    // tg: code/modules/plumbing/plumbers/_plumb_reagents.dm:245
    public override async DmTask<object> trans_to(object target = null, float? amount = null, float? multiplier = null, object target_id = null, float? preserve_data = null, float? no_react = null, mob transferred_by = null, object remove_blacklisted = null, float? methods = null, float? show_message = null, float? ignore_stomach = null, float? copy_only = null)
    {
        amount ??= 1;
        methods ??= LINEAR;
        //usual stuff
        if (!Dm.truthy(length(remove_blacklisted)) || (target_id != null && ispath(target_id)))
            return await base.trans_to(target, amount, multiplier, target_id, preserve_data, no_react, transferred_by, remove_blacklisted, methods, show_message, ignore_stomach, copy_only);

        if (QDELETED(target as datum))
            return FALSE;

        if (!IS_FINITE(amount))
        {
            stack_trace(Dm.text("non finite amount passed to trans_to [] amount of reagents", amount), "code/modules/plumbing/plumbers/_plumb_reagents.dm", 267);
            return FALSE;
        }

        datum_reagents target_holder;
        if (istype(target, typeof(datum_reagents)))
            target_holder = (datum_reagents)target;
        else
            target_holder = ((atom)target).reagents as datum_reagents;
        DmList cached_reagents = reagent_list;

        float actual_volume = 0;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.@in(reagent.type, remove_blacklisted))
                continue;
            actual_volume += Dm.num(reagent.volume);
        }
        actual_volume = min(round(actual_volume, CHEMICAL_VOLUME_ROUNDING), maximum_volume);

        // Prevents small amount problems, as well as zero and below zero amounts.
        amount = round(min(amount.Value, actual_volume, target_holder.maximum_volume - target_holder.total_volume), CHEMICAL_QUANTISATION_LEVEL);
        if (amount <= 0)
            return FALSE;

        float transfer_amount;
        object transfered_amount;
        float total_transfered_amount = 0;

        float round_robin = Dm.bitand(methods.Value, LINEAR);
        float part = 0;
        float to_transfer = 0;
        if (Dm.truthy(round_robin))
            to_transfer = amount.Value;
        else
            part = amount.Value / actual_volume;

        //first add reagents to target
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (Dm.truthy(round_robin) && !Dm.truthy(to_transfer))
                break;

            if (Dm.@in(reagent.type, remove_blacklisted))
                continue;
            else
            {
                if (Dm.truthy(round_robin))
                    transfer_amount = min(to_transfer, Dm.num(reagent.volume));
                else
                    transfer_amount = Dm.num(reagent.volume) * part;
            }

            if (Dm.truthy(await reagent.intercept_reagents_transfer(target_holder, transfer_amount)))
                continue;

            transfered_amount = await target_holder.add_reagent(reagent.type, transfer_amount, await copy_data(reagent) as DmList, chem_temp, reagent.purity, reagent.ph, no_react: TRUE); //we only handle reaction after every reagent has been transferred.
            if (!Dm.truthy(transfered_amount))
                continue;
            total_transfered_amount += Dm.num(transfered_amount);
            if (Dm.truthy(round_robin))
                to_transfer -= Dm.num(transfered_amount);
            reagent.volume = Dm.num(reagent.volume) - Dm.num(transfered_amount);
        }
        await update_total();

        return total_transfered_amount;
    }
}
